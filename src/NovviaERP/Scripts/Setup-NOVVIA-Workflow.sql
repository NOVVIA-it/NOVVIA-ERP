-- =====================================================
-- NOVVIA Workflow & Automatisierung
-- =====================================================
-- Ereignisbasierte Workflows mit Formeln
-- Beispiel: Kunde angelegt -> Kundennr = Debitorennr
-- =====================================================

-- Datenbank wird beim Aufruf angegeben: sqlcmd -d "Mandant_2"
GO

-- Schema sicherstellen
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'NOVVIA')
    EXEC('CREATE SCHEMA NOVVIA');
GO

-- =====================================================
-- Tabelle: NOVVIA.Workflow
-- =====================================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'NOVVIA' AND TABLE_NAME = 'Workflow')
BEGIN
    CREATE TABLE NOVVIA.Workflow (
        kWorkflow INT IDENTITY(1,1) PRIMARY KEY,
        cName NVARCHAR(100) NOT NULL,
        cBeschreibung NVARCHAR(500) NULL,
        cEntityTyp NVARCHAR(50) NOT NULL,       -- Kunde, Artikel, Auftrag, Lieferant, Rechnung, Lager
        cEreignis NVARCHAR(50) NOT NULL,        -- Angelegt, Geaendert, Geloescht, StatusGeaendert
        nAktiv BIT NOT NULL DEFAULT 1,
        nReihenfolge INT NOT NULL DEFAULT 100,
        dErstellt DATETIME NOT NULL DEFAULT GETDATE(),
        dGeaendert DATETIME NULL,
        kErstelltVon INT NULL,

        INDEX IX_Workflow_Entity (cEntityTyp, cEreignis),
        INDEX IX_Workflow_Aktiv (nAktiv)
    );
    PRINT 'Tabelle NOVVIA.Workflow erstellt.';
END
GO

-- =====================================================
-- Tabelle: NOVVIA.WorkflowBedingung
-- =====================================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'NOVVIA' AND TABLE_NAME = 'WorkflowBedingung')
BEGIN
    CREATE TABLE NOVVIA.WorkflowBedingung (
        kBedingung INT IDENTITY(1,1) PRIMARY KEY,
        kWorkflow INT NOT NULL,
        cFeld NVARCHAR(100) NOT NULL,           -- z.B. "cKundengruppe"
        cOperator NVARCHAR(20) NOT NULL,        -- =, <>, >, <, >=, <=, LIKE, IN, LEER, NICHT_LEER
        cWert NVARCHAR(500) NULL,               -- Vergleichswert (kann Formel sein)
        cVerknuepfung NVARCHAR(10) DEFAULT 'UND', -- UND, ODER
        nReihenfolge INT NOT NULL DEFAULT 1,

        CONSTRAINT FK_Bedingung_Workflow FOREIGN KEY (kWorkflow) REFERENCES NOVVIA.Workflow(kWorkflow) ON DELETE CASCADE
    );
    PRINT 'Tabelle NOVVIA.WorkflowBedingung erstellt.';
END
GO

-- =====================================================
-- Tabelle: NOVVIA.WorkflowAktion
-- =====================================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'NOVVIA' AND TABLE_NAME = 'WorkflowAktion')
BEGIN
    CREATE TABLE NOVVIA.WorkflowAktion (
        kAktion INT IDENTITY(1,1) PRIMARY KEY,
        kWorkflow INT NOT NULL,
        cAktionsTyp NVARCHAR(50) NOT NULL,      -- FeldSetzen, EMail, Log, StatusAendern, Berechnung
        cZielfeld NVARCHAR(100) NULL,           -- Zielfeld fuer FeldSetzen
        cFormel NVARCHAR(1000) NULL,            -- Formel: {Debitorennr}, HEUTE(), {Preis} * 1.19
        cParameter NVARCHAR(MAX) NULL,          -- JSON fuer komplexe Parameter
        nReihenfolge INT NOT NULL DEFAULT 1,

        CONSTRAINT FK_Aktion_Workflow FOREIGN KEY (kWorkflow) REFERENCES NOVVIA.Workflow(kWorkflow) ON DELETE CASCADE
    );
    PRINT 'Tabelle NOVVIA.WorkflowAktion erstellt.';
END
GO

-- =====================================================
-- Tabelle: NOVVIA.WorkflowLog
-- =====================================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'NOVVIA' AND TABLE_NAME = 'WorkflowLog')
BEGIN
    CREATE TABLE NOVVIA.WorkflowLog (
        kLog INT IDENTITY(1,1) PRIMARY KEY,
        kWorkflow INT NOT NULL,
        cEntityTyp NVARCHAR(50) NOT NULL,
        kEntity INT NOT NULL,
        cEreignis NVARCHAR(50) NOT NULL,
        cStatus NVARCHAR(20) NOT NULL,          -- Erfolg, Fehler, Uebersprungen
        cDetails NVARCHAR(MAX) NULL,
        dAusgefuehrt DATETIME NOT NULL DEFAULT GETDATE(),
        nDauerMs INT NULL,

        INDEX IX_WorkflowLog_Workflow (kWorkflow),
        INDEX IX_WorkflowLog_Entity (cEntityTyp, kEntity),
        INDEX IX_WorkflowLog_Datum (dAusgefuehrt)
    );
    PRINT 'Tabelle NOVVIA.WorkflowLog erstellt.';
END
GO

-- =====================================================
-- Tabelle: NOVVIA.Hilfe (Feld- und Funktionshilfe)
-- =====================================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'NOVVIA' AND TABLE_NAME = 'Hilfe')
BEGIN
    CREATE TABLE NOVVIA.Hilfe (
        kHilfe INT IDENTITY(1,1) PRIMARY KEY,
        cKategorie NVARCHAR(50) NOT NULL,       -- Feld, Funktion, Ereignis, Operator
        cEntityTyp NVARCHAR(50) NULL,           -- Kunde, Artikel, etc. (NULL = global)
        cName NVARCHAR(100) NOT NULL,           -- Feldname oder Funktionsname
        cAnzeigename NVARCHAR(100) NOT NULL,    -- Angezeigter Name
        cBeschreibung NVARCHAR(500) NOT NULL,
        cDatentyp NVARCHAR(50) NULL,            -- Text, Zahl, Datum, Boolean, Waehrung
        cBeispiel NVARCHAR(200) NULL,
        cHinweis NVARCHAR(500) NULL,
        nNurLesen BIT DEFAULT 0,                -- Feld kann nicht gesetzt werden
        nReihenfolge INT DEFAULT 100,

        INDEX IX_Hilfe_Kategorie (cKategorie, cEntityTyp),
        INDEX IX_Hilfe_Name (cName)
    );
    PRINT 'Tabelle NOVVIA.Hilfe erstellt.';
END
GO

-- =====================================================
-- SP: Workflow ausfuehren
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spWorkflowAusfuehren')
    DROP PROCEDURE NOVVIA.spWorkflowAusfuehren;
GO

CREATE PROCEDURE NOVVIA.spWorkflowAusfuehren
    @cEntityTyp NVARCHAR(50),
    @kEntity INT,
    @cEreignis NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    -- Alle aktiven Workflows fuer dieses Ereignis zurueckgeben
    SELECT
        w.kWorkflow,
        w.cName,
        w.cBeschreibung,
        w.nReihenfolge
    FROM NOVVIA.Workflow w
    WHERE w.cEntityTyp = @cEntityTyp
      AND w.cEreignis = @cEreignis
      AND w.nAktiv = 1
    ORDER BY w.nReihenfolge;
END
GO

PRINT 'SP NOVVIA.spWorkflowAusfuehren erstellt.';
GO

-- =====================================================
-- SP: Workflow-Aktionen laden
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spWorkflowAktionenLaden')
    DROP PROCEDURE NOVVIA.spWorkflowAktionenLaden;
GO

CREATE PROCEDURE NOVVIA.spWorkflowAktionenLaden
    @kWorkflow INT
AS
BEGIN
    SET NOCOUNT ON;

    -- Bedingungen
    SELECT
        kBedingung, cFeld, cOperator, cWert, cVerknuepfung, nReihenfolge
    FROM NOVVIA.WorkflowBedingung
    WHERE kWorkflow = @kWorkflow
    ORDER BY nReihenfolge;

    -- Aktionen
    SELECT
        kAktion, cAktionsTyp, cZielfeld, cFormel, cParameter, nReihenfolge
    FROM NOVVIA.WorkflowAktion
    WHERE kWorkflow = @kWorkflow
    ORDER BY nReihenfolge;
END
GO

PRINT 'SP NOVVIA.spWorkflowAktionenLaden erstellt.';
GO

-- =====================================================
-- SP: Workflow-Log schreiben
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spWorkflowLogSchreiben')
    DROP PROCEDURE NOVVIA.spWorkflowLogSchreiben;
GO

CREATE PROCEDURE NOVVIA.spWorkflowLogSchreiben
    @kWorkflow INT,
    @cEntityTyp NVARCHAR(50),
    @kEntity INT,
    @cEreignis NVARCHAR(50),
    @cStatus NVARCHAR(20),
    @cDetails NVARCHAR(MAX) = NULL,
    @nDauerMs INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO NOVVIA.WorkflowLog (kWorkflow, cEntityTyp, kEntity, cEreignis, cStatus, cDetails, nDauerMs)
    VALUES (@kWorkflow, @cEntityTyp, @kEntity, @cEreignis, @cStatus, @cDetails, @nDauerMs);

    SELECT SCOPE_IDENTITY() AS kLog;
END
GO

PRINT 'SP NOVVIA.spWorkflowLogSchreiben erstellt.';
GO

-- =====================================================
-- Hilfe-Daten: Felder
-- =====================================================
PRINT 'Importiere Hilfe-Daten...';

-- === KUNDE Felder ===
INSERT INTO NOVVIA.Hilfe (cKategorie, cEntityTyp, cName, cAnzeigename, cBeschreibung, cDatentyp, cBeispiel, nNurLesen, nReihenfolge)
VALUES
('Feld', 'Kunde', 'kKunde', 'Kunden-ID', 'Eindeutige ID des Kunden in der Datenbank', 'Zahl', '12345', 1, 1),
('Feld', 'Kunde', 'cKundenNr', 'Kundennummer', 'Alphanumerische Kundennummer', 'Text', 'K-00001', 0, 2),
('Feld', 'Kunde', 'cFirma', 'Firma', 'Firmenname des Kunden', 'Text', 'Musterfirma GmbH', 0, 3),
('Feld', 'Kunde', 'cAnrede', 'Anrede', 'Anrede (Herr, Frau, Firma)', 'Text', 'Herr', 0, 4),
('Feld', 'Kunde', 'cVorname', 'Vorname', 'Vorname des Ansprechpartners', 'Text', 'Max', 0, 5),
('Feld', 'Kunde', 'cName', 'Nachname', 'Nachname des Ansprechpartners', 'Text', 'Mustermann', 0, 6),
('Feld', 'Kunde', 'cStrasse', 'Strasse', 'Strassenname mit Hausnummer', 'Text', 'Musterstrasse 123', 0, 7),
('Feld', 'Kunde', 'cPLZ', 'PLZ', 'Postleitzahl', 'Text', '12345', 0, 8),
('Feld', 'Kunde', 'cOrt', 'Ort', 'Stadt/Ort', 'Text', 'Musterstadt', 0, 9),
('Feld', 'Kunde', 'cLand', 'Land', 'Laendercode (DE, AT, CH)', 'Text', 'DE', 0, 10),
('Feld', 'Kunde', 'cTel', 'Telefon', 'Telefonnummer', 'Text', '+49 123 456789', 0, 11),
('Feld', 'Kunde', 'cMail', 'E-Mail', 'E-Mail-Adresse', 'Text', 'max@firma.de', 0, 12),
('Feld', 'Kunde', 'cDebitorenNr', 'Debitorennummer', 'Debitorennummer fuer Buchhaltung', 'Text', '10001', 0, 13),
('Feld', 'Kunde', 'fKreditlimit', 'Kreditlimit', 'Maximales Kreditlimit in EUR', 'Waehrung', '5000.00', 0, 14),
('Feld', 'Kunde', 'fRabatt', 'Rabatt %', 'Standard-Rabatt in Prozent', 'Zahl', '5.0', 0, 15),
('Feld', 'Kunde', 'kKundengruppe', 'Kundengruppe ID', 'ID der zugeordneten Kundengruppe', 'Zahl', '1', 0, 16),
('Feld', 'Kunde', 'dErstellt', 'Erstellt am', 'Erstellungsdatum des Datensatzes', 'Datum', '2024-01-15', 1, 17),
('Feld', 'Kunde', 'nAktiv', 'Aktiv', 'Ist der Kunde aktiv? (1=Ja, 0=Nein)', 'Boolean', '1', 0, 18);

-- === ARTIKEL Felder ===
INSERT INTO NOVVIA.Hilfe (cKategorie, cEntityTyp, cName, cAnzeigename, cBeschreibung, cDatentyp, cBeispiel, nNurLesen, nReihenfolge)
VALUES
('Feld', 'Artikel', 'kArtikel', 'Artikel-ID', 'Eindeutige ID des Artikels', 'Zahl', '5678', 1, 1),
('Feld', 'Artikel', 'cArtNr', 'Artikelnummer', 'Alphanumerische Artikelnummer', 'Text', 'ART-001', 0, 2),
('Feld', 'Artikel', 'cName', 'Bezeichnung', 'Artikelbezeichnung', 'Text', 'Beispielprodukt', 0, 3),
('Feld', 'Artikel', 'cBeschreibung', 'Beschreibung', 'Ausfuehrliche Artikelbeschreibung', 'Text', 'Detailtext...', 0, 4),
('Feld', 'Artikel', 'cBarcode', 'EAN/Barcode', 'EAN-13 oder anderer Barcode', 'Text', '4012345678901', 0, 5),
('Feld', 'Artikel', 'cPZN', 'PZN', 'Pharmazentralnummer (Pharma)', 'Text', '1234567', 0, 6),
('Feld', 'Artikel', 'fVKNetto', 'VK Netto', 'Verkaufspreis Netto', 'Waehrung', '19.99', 0, 7),
('Feld', 'Artikel', 'fVKBrutto', 'VK Brutto', 'Verkaufspreis Brutto', 'Waehrung', '23.79', 0, 8),
('Feld', 'Artikel', 'fEKNetto', 'EK Netto', 'Einkaufspreis Netto', 'Waehrung', '10.00', 0, 9),
('Feld', 'Artikel', 'fLagerbestand', 'Lagerbestand', 'Aktueller Lagerbestand', 'Zahl', '150', 0, 10),
('Feld', 'Artikel', 'fMindestbestand', 'Mindestbestand', 'Mindestbestand fuer Warnung', 'Zahl', '20', 0, 11),
('Feld', 'Artikel', 'fGewicht', 'Gewicht (kg)', 'Gewicht in Kilogramm', 'Zahl', '0.5', 0, 12),
('Feld', 'Artikel', 'kSteuerklasse', 'Steuerklasse ID', 'ID der Steuerklasse', 'Zahl', '1', 0, 13),
('Feld', 'Artikel', 'kHersteller', 'Hersteller ID', 'ID des Herstellers', 'Zahl', '42', 0, 14),
('Feld', 'Artikel', 'nAktiv', 'Aktiv', 'Ist der Artikel aktiv?', 'Boolean', '1', 0, 15);

-- === AUFTRAG Felder ===
INSERT INTO NOVVIA.Hilfe (cKategorie, cEntityTyp, cName, cAnzeigename, cBeschreibung, cDatentyp, cBeispiel, nNurLesen, nReihenfolge)
VALUES
('Feld', 'Auftrag', 'kBestellung', 'Auftrags-ID', 'Eindeutige ID des Auftrags', 'Zahl', '9999', 1, 1),
('Feld', 'Auftrag', 'cBestellNr', 'Auftragsnummer', 'Alphanumerische Auftragsnummer', 'Text', 'AU-2024-0001', 0, 2),
('Feld', 'Auftrag', 'kKunde', 'Kunden-ID', 'ID des zugeordneten Kunden', 'Zahl', '12345', 0, 3),
('Feld', 'Auftrag', 'dErstellt', 'Bestelldatum', 'Datum der Bestellung', 'Datum', '2024-01-15', 0, 4),
('Feld', 'Auftrag', 'dLiefertermin', 'Liefertermin', 'Gewuenschter Liefertermin', 'Datum', '2024-01-20', 0, 5),
('Feld', 'Auftrag', 'fGesamtNetto', 'Gesamt Netto', 'Gesamtbetrag Netto', 'Waehrung', '500.00', 1, 6),
('Feld', 'Auftrag', 'fGesamtBrutto', 'Gesamt Brutto', 'Gesamtbetrag Brutto', 'Waehrung', '595.00', 1, 7),
('Feld', 'Auftrag', 'nStatus', 'Status', 'Auftragsstatus (0=Offen, 1=InBearbeitung, 2=Versendet, 3=Abgeschlossen)', 'Zahl', '1', 0, 8),
('Feld', 'Auftrag', 'kVersandart', 'Versandart ID', 'ID der Versandart', 'Zahl', '1', 0, 9),
('Feld', 'Auftrag', 'kZahlungsart', 'Zahlungsart ID', 'ID der Zahlungsart', 'Zahl', '2', 0, 10),
('Feld', 'Auftrag', 'cBemerkung', 'Bemerkung', 'Interne Bemerkung zum Auftrag', 'Text', 'Eilauftrag', 0, 11);

-- === LIEFERANT Felder ===
INSERT INTO NOVVIA.Hilfe (cKategorie, cEntityTyp, cName, cAnzeigename, cBeschreibung, cDatentyp, cBeispiel, nNurLesen, nReihenfolge)
VALUES
('Feld', 'Lieferant', 'kLieferant', 'Lieferanten-ID', 'Eindeutige ID des Lieferanten', 'Zahl', '100', 1, 1),
('Feld', 'Lieferant', 'cLieferantenNr', 'Lieferantennummer', 'Alphanumerische Lieferantennummer', 'Text', 'L-001', 0, 2),
('Feld', 'Lieferant', 'cFirma', 'Firma', 'Firmenname des Lieferanten', 'Text', 'Grosshandel AG', 0, 3),
('Feld', 'Lieferant', 'cKreditorenNr', 'Kreditorennummer', 'Kreditorennummer fuer Buchhaltung', 'Text', '70001', 0, 4),
('Feld', 'Lieferant', 'cStrasse', 'Strasse', 'Strasse mit Hausnummer', 'Text', 'Industrieweg 5', 0, 5),
('Feld', 'Lieferant', 'cPLZ', 'PLZ', 'Postleitzahl', 'Text', '54321', 0, 6),
('Feld', 'Lieferant', 'cOrt', 'Ort', 'Stadt', 'Text', 'Lieferstadt', 0, 7),
('Feld', 'Lieferant', 'cMail', 'E-Mail', 'E-Mail-Adresse', 'Text', 'bestellung@lieferant.de', 0, 8),
('Feld', 'Lieferant', 'nAmbient', 'Ambient (Pharma)', 'Ambient-Zertifizierung (15-25 Grad)', 'Boolean', '1', 0, 9),
('Feld', 'Lieferant', 'nCool', 'Kuehlkette (Pharma)', 'Kuehlketten-Zertifizierung (2-8 Grad)', 'Boolean', '0', 0, 10),
('Feld', 'Lieferant', 'cGDP', 'GDP-Zertifikat', 'GDP-Zertifikatsnummer', 'Text', 'GDP-2024-001', 0, 11);

-- === RECHNUNG Felder ===
INSERT INTO NOVVIA.Hilfe (cKategorie, cEntityTyp, cName, cAnzeigename, cBeschreibung, cDatentyp, cBeispiel, nNurLesen, nReihenfolge)
VALUES
('Feld', 'Rechnung', 'kRechnung', 'Rechnungs-ID', 'Eindeutige ID der Rechnung', 'Zahl', '5000', 1, 1),
('Feld', 'Rechnung', 'cRechnungsNr', 'Rechnungsnummer', 'Rechnungsnummer', 'Text', 'RE-2024-0001', 0, 2),
('Feld', 'Rechnung', 'kKunde', 'Kunden-ID', 'ID des Rechnungsempfaengers', 'Zahl', '12345', 0, 3),
('Feld', 'Rechnung', 'dRechnungsdatum', 'Rechnungsdatum', 'Datum der Rechnung', 'Datum', '2024-01-15', 0, 4),
('Feld', 'Rechnung', 'dFaellig', 'Faelligkeitsdatum', 'Datum der Faelligkeit', 'Datum', '2024-02-15', 0, 5),
('Feld', 'Rechnung', 'fNetto', 'Nettobetrag', 'Rechnungsbetrag Netto', 'Waehrung', '1000.00', 1, 6),
('Feld', 'Rechnung', 'fBrutto', 'Bruttobetrag', 'Rechnungsbetrag Brutto', 'Waehrung', '1190.00', 1, 7),
('Feld', 'Rechnung', 'fBezahlt', 'Bezahlt', 'Bereits bezahlter Betrag', 'Waehrung', '0.00', 0, 8),
('Feld', 'Rechnung', 'nMahnstufe', 'Mahnstufe', 'Aktuelle Mahnstufe (0-3)', 'Zahl', '0', 0, 9),
('Feld', 'Rechnung', 'nStorniert', 'Storniert', 'Ist storniert?', 'Boolean', '0', 0, 10);

-- === FUNKTIONEN ===
INSERT INTO NOVVIA.Hilfe (cKategorie, cEntityTyp, cName, cAnzeigename, cBeschreibung, cDatentyp, cBeispiel, nReihenfolge)
VALUES
('Funktion', NULL, 'HEUTE()', 'Heute', 'Gibt das aktuelle Datum zurueck', 'Datum', 'HEUTE() -> 2024-01-15', 1),
('Funktion', NULL, 'JETZT()', 'Jetzt', 'Gibt aktuelles Datum und Uhrzeit zurueck', 'Datum', 'JETZT() -> 2024-01-15 14:30:00', 2),
('Funktion', NULL, 'DATUM_PLUS(tage)', 'Datum Plus', 'Addiert Tage zum aktuellen Datum', 'Datum', 'DATUM_PLUS(7) -> 2024-01-22', 3),
('Funktion', NULL, 'DATUM_MINUS(tage)', 'Datum Minus', 'Subtrahiert Tage vom aktuellen Datum', 'Datum', 'DATUM_MINUS(30) -> 2023-12-16', 4),
('Funktion', NULL, 'MONATSANFANG()', 'Monatsanfang', 'Erster Tag des aktuellen Monats', 'Datum', 'MONATSANFANG() -> 2024-01-01', 5),
('Funktion', NULL, 'MONATSENDE()', 'Monatsende', 'Letzter Tag des aktuellen Monats', 'Datum', 'MONATSENDE() -> 2024-01-31', 6),
('Funktion', NULL, 'JAHRESANFANG()', 'Jahresanfang', 'Erster Tag des aktuellen Jahres', 'Datum', 'JAHRESANFANG() -> 2024-01-01', 7),
('Funktion', NULL, 'RUNDEN(zahl,stellen)', 'Runden', 'Rundet eine Zahl auf n Nachkommastellen', 'Zahl', 'RUNDEN(19.456, 2) -> 19.46', 10),
('Funktion', NULL, 'AUFRUNDEN(zahl)', 'Aufrunden', 'Rundet auf die naechste ganze Zahl auf', 'Zahl', 'AUFRUNDEN(4.2) -> 5', 11),
('Funktion', NULL, 'ABRUNDEN(zahl)', 'Abrunden', 'Rundet auf die naechste ganze Zahl ab', 'Zahl', 'ABRUNDEN(4.8) -> 4', 12),
('Funktion', NULL, 'ABS(zahl)', 'Absolutwert', 'Gibt den Absolutwert zurueck', 'Zahl', 'ABS(-5) -> 5', 13),
('Funktion', NULL, 'MIN(a,b)', 'Minimum', 'Gibt den kleineren Wert zurueck', 'Zahl', 'MIN(10, 20) -> 10', 14),
('Funktion', NULL, 'MAX(a,b)', 'Maximum', 'Gibt den groesseren Wert zurueck', 'Zahl', 'MAX(10, 20) -> 20', 15),
('Funktion', NULL, 'WENN(bedingung,dann,sonst)', 'Wenn-Dann-Sonst', 'Bedingte Auswertung', 'variabel', 'WENN({fRabatt}>5, "VIP", "Standard")', 20),
('Funktion', NULL, 'LEER(feld)', 'Ist Leer', 'Prueft ob ein Feld leer ist', 'Boolean', 'LEER({cMail}) -> Wahr/Falsch', 21),
('Funktion', NULL, 'NICHT_LEER(feld)', 'Nicht Leer', 'Prueft ob ein Feld einen Wert hat', 'Boolean', 'NICHT_LEER({cMail}) -> Wahr/Falsch', 22),
('Funktion', NULL, 'FORMAT(wert,format)', 'Formatieren', 'Formatiert Wert nach Muster', 'Text', 'FORMAT(HEUTE(), "dd.MM.yyyy")', 25),
('Funktion', NULL, 'VERKETTEN(a,b,...)', 'Verketten', 'Verbindet mehrere Texte', 'Text', 'VERKETTEN({cVorname}, " ", {cName})', 26),
('Funktion', NULL, 'GROSS(text)', 'Grossbuchstaben', 'Wandelt in Grossbuchstaben um', 'Text', 'GROSS("test") -> "TEST"', 27),
('Funktion', NULL, 'KLEIN(text)', 'Kleinbuchstaben', 'Wandelt in Kleinbuchstaben um', 'Text', 'KLEIN("TEST") -> "test"', 28),
('Funktion', NULL, 'ERSETZEN(text,alt,neu)', 'Ersetzen', 'Ersetzt Text', 'Text', 'ERSETZEN({cTel}, " ", "")', 29),
('Funktion', NULL, 'LAENGE(text)', 'Laenge', 'Gibt die Textlaenge zurueck', 'Zahl', 'LAENGE("Hallo") -> 5', 30),
('Funktion', NULL, 'NAECHSTE_NR(prefix)', 'Naechste Nummer', 'Generiert die naechste Nummer mit Prefix', 'Text', 'NAECHSTE_NR("K-") -> "K-00042"', 35),
('Funktion', NULL, 'BENUTZER()', 'Aktueller Benutzer', 'Name des angemeldeten Benutzers', 'Text', 'BENUTZER() -> "Max Mustermann"', 40),
('Funktion', NULL, 'BENUTZER_ID()', 'Benutzer-ID', 'ID des angemeldeten Benutzers', 'Zahl', 'BENUTZER_ID() -> 5', 41);

-- === EREIGNISSE ===
INSERT INTO NOVVIA.Hilfe (cKategorie, cEntityTyp, cName, cAnzeigename, cBeschreibung, cBeispiel, nReihenfolge)
VALUES
('Ereignis', 'Kunde', 'Angelegt', 'Kunde angelegt', 'Wird ausgeloest wenn ein neuer Kunde erstellt wird', 'Kundennr automatisch setzen', 1),
('Ereignis', 'Kunde', 'Geaendert', 'Kunde geaendert', 'Wird ausgeloest wenn Kundendaten geaendert werden', 'Aenderung protokollieren', 2),
('Ereignis', 'Kunde', 'Geloescht', 'Kunde geloescht', 'Wird ausgeloest bevor ein Kunde geloescht wird', 'Warnung bei offenen Auftraegen', 3),
('Ereignis', 'Artikel', 'Angelegt', 'Artikel angelegt', 'Wird ausgeloest wenn ein neuer Artikel erstellt wird', 'Standardwerte setzen', 1),
('Ereignis', 'Artikel', 'Geaendert', 'Artikel geaendert', 'Wird ausgeloest wenn Artikeldaten geaendert werden', 'Preis-Historie speichern', 2),
('Ereignis', 'Artikel', 'LagerUnter', 'Lager unter Minimum', 'Wird ausgeloest wenn Bestand unter Mindestbestand faellt', 'Nachbestellung ausloesen', 4),
('Ereignis', 'Auftrag', 'Angelegt', 'Auftrag angelegt', 'Wird ausgeloest wenn ein neuer Auftrag erstellt wird', 'Liefertermin berechnen', 1),
('Ereignis', 'Auftrag', 'StatusGeaendert', 'Auftragsstatus geaendert', 'Wird ausgeloest wenn sich der Status aendert', 'Kunde per Mail informieren', 2),
('Ereignis', 'Auftrag', 'Storniert', 'Auftrag storniert', 'Wird ausgeloest wenn ein Auftrag storniert wird', 'Lagerbestand zurueckbuchen', 3),
('Ereignis', 'Rechnung', 'Erstellt', 'Rechnung erstellt', 'Wird ausgeloest wenn eine Rechnung erstellt wird', 'Faelligkeit berechnen', 1),
('Ereignis', 'Rechnung', 'Bezahlt', 'Rechnung bezahlt', 'Wird ausgeloest wenn Zahlung eingeht', 'Dankes-Mail senden', 2),
('Ereignis', 'Rechnung', 'Ueberfaellig', 'Rechnung ueberfaellig', 'Wird ausgeloest wenn Faelligkeit ueberschritten', 'Mahnung erstellen', 3),
('Ereignis', 'Lieferant', 'Angelegt', 'Lieferant angelegt', 'Wird ausgeloest wenn ein neuer Lieferant erstellt wird', 'Kreditorennr vergeben', 1),
('Ereignis', 'Lieferant', 'QualifizierungAbgelaufen', 'Qualifizierung abgelaufen', 'GDP-Qualifizierung ist abgelaufen', 'Warnung an RP', 2);

-- === OPERATOREN ===
INSERT INTO NOVVIA.Hilfe (cKategorie, cEntityTyp, cName, cAnzeigename, cBeschreibung, cBeispiel, nReihenfolge)
VALUES
('Operator', NULL, '=', 'Gleich', 'Prueft auf Gleichheit', '{cLand} = "DE"', 1),
('Operator', NULL, '<>', 'Ungleich', 'Prueft auf Ungleichheit', '{nStatus} <> 0', 2),
('Operator', NULL, '>', 'Groesser als', 'Prueft ob Wert groesser ist', '{fBetrag} > 1000', 3),
('Operator', NULL, '<', 'Kleiner als', 'Prueft ob Wert kleiner ist', '{fLagerbestand} < {fMindestbestand}', 4),
('Operator', NULL, '>=', 'Groesser oder gleich', 'Prueft ob Wert groesser oder gleich ist', '{fRabatt} >= 10', 5),
('Operator', NULL, '<=', 'Kleiner oder gleich', 'Prueft ob Wert kleiner oder gleich ist', '{nMahnstufe} <= 2', 6),
('Operator', NULL, 'LIKE', 'Enthaelt', 'Textsuche mit Wildcards (%)', '{cMail} LIKE "%@firma.de"', 7),
('Operator', NULL, 'IN', 'In Liste', 'Prueft ob Wert in Liste enthalten', '{cLand} IN ("DE", "AT", "CH")', 8),
('Operator', NULL, 'LEER', 'Ist leer', 'Prueft ob Feld leer/NULL ist', '{cMail} LEER', 9),
('Operator', NULL, 'NICHT_LEER', 'Nicht leer', 'Prueft ob Feld einen Wert hat', '{cTel} NICHT_LEER', 10);

GO

PRINT '';
PRINT '=====================================================';
PRINT 'NOVVIA Workflow & Hilfe Setup abgeschlossen!';
PRINT '=====================================================';
PRINT '';
PRINT 'Tabellen:';
PRINT '  - NOVVIA.Workflow';
PRINT '  - NOVVIA.WorkflowBedingung';
PRINT '  - NOVVIA.WorkflowAktion';
PRINT '  - NOVVIA.WorkflowLog';
PRINT '  - NOVVIA.Hilfe';
PRINT '';
PRINT 'Stored Procedures:';
PRINT '  - NOVVIA.spWorkflowAusfuehren';
PRINT '  - NOVVIA.spWorkflowAktionenLaden';
PRINT '  - NOVVIA.spWorkflowLogSchreiben';
PRINT '';
PRINT 'Hilfe-Eintraege importiert:';
PRINT '  - Kunde: 18 Felder';
PRINT '  - Artikel: 15 Felder';
PRINT '  - Auftrag: 11 Felder';
PRINT '  - Lieferant: 11 Felder';
PRINT '  - Rechnung: 10 Felder';
PRINT '  - Funktionen: 25';
PRINT '  - Ereignisse: 14';
PRINT '  - Operatoren: 10';
PRINT '=====================================================';
GO

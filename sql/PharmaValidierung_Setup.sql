-- Pharma-Validierung Setup
-- Artikel duerfen nur von/an validierte Lieferanten/Kunden gehandelt werden
-- Nur aktiv wenn PHARM = 1 in NOVVIA-Einstellungen

-- Schema pruefen
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'NOVVIA')
    EXEC('CREATE SCHEMA NOVVIA');
GO

-- =====================================================
-- Artikel-Kategorien (BTM, MedCan, Rx, Kuehlpflichtig, etc.)
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PharmaKategorie' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    CREATE TABLE NOVVIA.PharmaKategorie (
        kPharmaKategorie INT IDENTITY(1,1) PRIMARY KEY,
        cKuerzel NVARCHAR(20) NOT NULL,           -- BTM, MEDCAN, RX, KUEHL, OTC
        cName NVARCHAR(100) NOT NULL,             -- Betaeubungsmittel, Med. Cannabis, etc.
        cBeschreibung NVARCHAR(500),
        cFarbe NVARCHAR(20) DEFAULT '#666666',    -- Fuer UI-Anzeige
        nErfordertValidierung BIT DEFAULT 1,      -- Muss Kunde/Lieferant validiert sein?
        nAktiv BIT DEFAULT 1,
        nSort INT DEFAULT 0,
        dErstellt DATETIME DEFAULT GETDATE(),

        CONSTRAINT UQ_PharmaKategorie_Kuerzel UNIQUE (cKuerzel)
    );

    -- Standard-Kategorien einfuegen
    INSERT INTO NOVVIA.PharmaKategorie (cKuerzel, cName, cBeschreibung, cFarbe, nErfordertValidierung, nSort) VALUES
    ('BTM', 'Betaeubungsmittel', 'BtM-pflichtige Arzneimittel nach BtMG', '#dc3545', 1, 1),
    ('MEDCAN', 'Medizinisches Cannabis', 'Cannabis zu medizinischen Zwecken', '#28a745', 1, 2),
    ('RX', 'Verschreibungspflichtig', 'Rezeptpflichtige Arzneimittel', '#ffc107', 1, 3),
    ('KUEHL', 'Kuehlpflichtig', 'Kuehlkettenpflichtige Artikel', '#17a2b8', 1, 4),
    ('T-REZ', 'T-Rezept', 'Arzneimittel mit T-Rezept-Pflicht', '#6f42c1', 1, 5),
    ('OTC', 'OTC / Freiverkaeuflich', 'Nicht verschreibungspflichtig', '#6c757d', 0, 10);

    PRINT 'Tabelle NOVVIA.PharmaKategorie erstellt mit Standard-Kategorien';
END
GO

-- =====================================================
-- Artikel-Kategorie-Zuordnung
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ArtikelPharmaKategorie' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    CREATE TABLE NOVVIA.ArtikelPharmaKategorie (
        kArtikelPharmaKategorie INT IDENTITY(1,1) PRIMARY KEY,
        kArtikel INT NOT NULL,                    -- JTL Artikel-ID
        kPharmaKategorie INT NOT NULL,
        dZugewiesen DATETIME DEFAULT GETDATE(),
        kZugewiesenVon INT,                       -- Benutzer-ID

        CONSTRAINT FK_ArtikelPharma_Kategorie FOREIGN KEY (kPharmaKategorie)
            REFERENCES NOVVIA.PharmaKategorie(kPharmaKategorie),
        CONSTRAINT UQ_ArtikelPharmaKategorie UNIQUE (kArtikel, kPharmaKategorie)
    );

    CREATE INDEX IX_ArtikelPharma_Artikel ON NOVVIA.ArtikelPharmaKategorie (kArtikel);

    PRINT 'Tabelle NOVVIA.ArtikelPharmaKategorie erstellt';
END
GO

-- =====================================================
-- Lieferanten-Validierung pro Kategorie
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LieferantValidierung' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    CREATE TABLE NOVVIA.LieferantValidierung (
        kLieferantValidierung INT IDENTITY(1,1) PRIMARY KEY,
        kLieferant INT NOT NULL,                  -- JTL Lieferant-ID
        kPharmaKategorie INT NOT NULL,
        nValidiert BIT DEFAULT 0,                 -- Ist fuer diese Kategorie validiert?
        dValidiert DATETIME,                      -- Wann validiert
        dGueltigBis DATETIME,                     -- Validierung laeuft ab am
        kValidiertVon INT,                        -- Benutzer-ID (muss Rolle RP haben)
        cBemerkung NVARCHAR(500),
        cZertifikatNr NVARCHAR(100),              -- Zertifikat/Lizenz-Nummer
        cZertifikatPfad NVARCHAR(500),            -- Pfad zum Zertifikat-Dokument
        dErstellt DATETIME DEFAULT GETDATE(),
        dGeaendert DATETIME DEFAULT GETDATE(),

        CONSTRAINT FK_LiefVal_Kategorie FOREIGN KEY (kPharmaKategorie)
            REFERENCES NOVVIA.PharmaKategorie(kPharmaKategorie),
        CONSTRAINT UQ_LieferantValidierung UNIQUE (kLieferant, kPharmaKategorie)
    );

    CREATE INDEX IX_LiefVal_Lieferant ON NOVVIA.LieferantValidierung (kLieferant);
    CREATE INDEX IX_LiefVal_Validiert ON NOVVIA.LieferantValidierung (nValidiert, dGueltigBis);

    PRINT 'Tabelle NOVVIA.LieferantValidierung erstellt';
END
GO

-- =====================================================
-- Kunden-Validierung pro Kategorie
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'KundeValidierung' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    CREATE TABLE NOVVIA.KundeValidierung (
        kKundeValidierung INT IDENTITY(1,1) PRIMARY KEY,
        kKunde INT NOT NULL,                      -- JTL Kunde-ID
        kPharmaKategorie INT NOT NULL,
        nValidiert BIT DEFAULT 0,                 -- Ist fuer diese Kategorie validiert?
        dValidiert DATETIME,                      -- Wann validiert
        dGueltigBis DATETIME,                     -- Validierung laeuft ab am
        kValidiertVon INT,                        -- Benutzer-ID (muss Rolle RP haben)
        cBemerkung NVARCHAR(500),
        cApothekennummer NVARCHAR(50),            -- IDF/Apothekennummer
        cBetriebserlaubnis NVARCHAR(100),         -- Betriebserlaubnis-Nr.
        cZertifikatPfad NVARCHAR(500),            -- Pfad zum Dokument
        dErstellt DATETIME DEFAULT GETDATE(),
        dGeaendert DATETIME DEFAULT GETDATE(),

        CONSTRAINT FK_KundeVal_Kategorie FOREIGN KEY (kPharmaKategorie)
            REFERENCES NOVVIA.PharmaKategorie(kPharmaKategorie),
        CONSTRAINT UQ_KundeValidierung UNIQUE (kKunde, kPharmaKategorie)
    );

    CREATE INDEX IX_KundeVal_Kunde ON NOVVIA.KundeValidierung (kKunde);
    CREATE INDEX IX_KundeVal_Validiert ON NOVVIA.KundeValidierung (nValidiert, dGueltigBis);

    PRINT 'Tabelle NOVVIA.KundeValidierung erstellt';
END
GO

-- =====================================================
-- Validierungs-Log (Audit Trail)
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ValidierungsLog' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    CREATE TABLE NOVVIA.ValidierungsLog (
        kValidierungsLog INT IDENTITY(1,1) PRIMARY KEY,
        cEntitaet NVARCHAR(20) NOT NULL,          -- 'LIEFERANT' oder 'KUNDE'
        kEntitaet INT NOT NULL,                   -- kLieferant oder kKunde
        kPharmaKategorie INT NOT NULL,
        cAktion NVARCHAR(50) NOT NULL,            -- VALIDIERT, ENTZOGEN, GEAENDERT, ABGELAUFEN
        nAlterWert BIT,
        nNeuerWert BIT,
        cBemerkung NVARCHAR(500),
        kBenutzer INT NOT NULL,
        cBenutzerName NVARCHAR(100),
        dZeitpunkt DATETIME DEFAULT GETDATE()
    );

    CREATE INDEX IX_ValLog_Entitaet ON NOVVIA.ValidierungsLog (cEntitaet, kEntitaet);

    PRINT 'Tabelle NOVVIA.ValidierungsLog erstellt';
END
GO

-- =====================================================
-- Textmeldungen-System (zentral, konfigurierbar)
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Textmeldung' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    CREATE TABLE NOVVIA.Textmeldung (
        kTextmeldung INT IDENTITY(1,1) PRIMARY KEY,
        cKategorie NVARCHAR(50) NOT NULL,         -- PHARMA, AUFTRAG, RECHNUNG, SYSTEM, etc.
        cSchluessel NVARCHAR(100) NOT NULL,       -- Eindeutiger Schluessel
        cText NVARCHAR(MAX) NOT NULL,             -- Der eigentliche Text mit Platzhaltern
        cBeschreibung NVARCHAR(500),              -- Beschreibung fuer Admin
        cSprache NVARCHAR(10) DEFAULT 'DE',       -- Sprachcode
        nSeverity INT DEFAULT 1,                  -- 0=Info, 1=Warnung, 2=Fehler, 3=Kritisch
        nAktiv BIT DEFAULT 1,
        dErstellt DATETIME DEFAULT GETDATE(),
        dGeaendert DATETIME DEFAULT GETDATE(),

        CONSTRAINT UQ_Textmeldung UNIQUE (cKategorie, cSchluessel, cSprache)
    );

    CREATE INDEX IX_Textmeldung_Kategorie ON NOVVIA.Textmeldung (cKategorie);
    CREATE INDEX IX_Textmeldung_Schluessel ON NOVVIA.Textmeldung (cSchluessel);

    PRINT 'Tabelle NOVVIA.Textmeldung erstellt';
END
GO

-- Standard-Pharma-Meldungen einfuegen
IF NOT EXISTS (SELECT 1 FROM NOVVIA.Textmeldung WHERE cKategorie = 'PHARMA')
BEGIN
    INSERT INTO NOVVIA.Textmeldung (cKategorie, cSchluessel, cText, cBeschreibung, nSeverity) VALUES
    ('PHARMA', 'LIEFERANT_NICHT_VALIDIERT',
     'Lieferant "{LieferantName}" ist nicht fuer {Kategorie} validiert. Bestellung nicht moeglich.',
     'Fehlermeldung wenn Lieferant nicht fuer Pharma-Kategorie validiert ist', 2),

    ('PHARMA', 'KUNDE_NICHT_VALIDIERT',
     'Kunde "{KundeName}" ist nicht fuer {Kategorie} validiert. Lieferung nicht moeglich.',
     'Fehlermeldung wenn Kunde nicht fuer Pharma-Kategorie validiert ist', 2),

    ('PHARMA', 'VALIDIERUNG_ABGELAUFEN',
     'Validierung fuer {Kategorie} ist abgelaufen am {Datum}. Bitte Validierung erneuern.',
     'Fehlermeldung wenn Validierung abgelaufen ist', 2),

    ('PHARMA', 'ARTIKEL_ERFORDERT_VALIDIERUNG',
     'Artikel {ArtNr} "{Bezeichnung}" ist als {Kategorie} klassifiziert und erfordert validierte Geschaeftspartner.',
     'Hinweis dass Artikel validierungspflichtig ist', 1),

    ('PHARMA', 'BTM_SONDERFREIGABE',
     'BTM-Artikel {ArtNr} erfordert Sonderfreigabe durch berechtigten Mitarbeiter.',
     'Fehlermeldung fuer BTM ohne Freigabe', 3),

    ('PHARMA', 'MEDCAN_LIZENZ_FEHLT',
     'Fuer Medizinisches Cannabis {ArtNr} fehlt die erforderliche Lizenz beim {Entitaet} "{Name}".',
     'Fehlermeldung fuer MedCan ohne Lizenz', 3),

    ('PHARMA', 'KUEHLKETTE_WARNUNG',
     'Artikel {ArtNr} ist kuehlpflichtig. Bitte Kuehlkette sicherstellen.',
     'Warnung fuer kuehlpflichtige Artikel', 1),

    ('PHARMA', 'VALIDIERUNG_BALD_ABGELAUFEN',
     'Validierung fuer {Kategorie} bei {Entitaet} "{Name}" laeuft in {Tage} Tagen ab.',
     'Vorwarnung vor ablaufender Validierung', 1);

    PRINT 'Standard-Pharma-Textmeldungen eingefuegt';
END
GO

-- Allgemeine System-Meldungen
IF NOT EXISTS (SELECT 1 FROM NOVVIA.Textmeldung WHERE cKategorie = 'SYSTEM')
BEGIN
    INSERT INTO NOVVIA.Textmeldung (cKategorie, cSchluessel, cText, cBeschreibung, nSeverity) VALUES
    ('SYSTEM', 'KEINE_BERECHTIGUNG',
     'Sie haben keine Berechtigung fuer diese Aktion. Erforderliche Rolle: {Rolle}.',
     'Fehlermeldung bei fehlender Berechtigung', 2),

    ('SYSTEM', 'SPEICHERN_ERFOLGREICH',
     'Daten wurden erfolgreich gespeichert.',
     'Erfolgsmeldung nach Speichern', 0),

    ('SYSTEM', 'LOESCHEN_BESTAETIGUNG',
     'Moechten Sie "{Name}" wirklich loeschen? Diese Aktion kann nicht rueckgaengig gemacht werden.',
     'Bestaetigung vor Loeschen', 1);

    PRINT 'Standard-System-Textmeldungen eingefuegt';
END
GO

-- =====================================================
-- Stored Procedure: Validierung pruefen
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spNOVVIA_PruefePharmaValidierung')
    DROP PROCEDURE spNOVVIA_PruefePharmaValidierung;
GO

CREATE PROCEDURE spNOVVIA_PruefePharmaValidierung
    @cEntitaet NVARCHAR(20),      -- 'LIEFERANT' oder 'KUNDE'
    @kEntitaet INT,               -- kLieferant oder kKunde
    @kArtikel INT,                -- Artikel-ID
    @nIstValidiert BIT OUTPUT,
    @cFehlermeldung NVARCHAR(500) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @nIstValidiert = 1;
    SET @cFehlermeldung = NULL;

    -- Pruefen ob Pharma-Modus aktiv
    DECLARE @nPharmaModus BIT = 0;
    SELECT @nPharmaModus = CASE WHEN cWert = '1' OR cWert = 'True' THEN 1 ELSE 0 END
    FROM NOVVIA.FirmaEinstellung WHERE cSchluessel = 'PharmaModus';

    IF @nPharmaModus = 0
    BEGIN
        RETURN; -- Keine Pruefung wenn Pharma-Modus deaktiviert
    END

    -- Artikel-Kategorien holen die Validierung erfordern
    DECLARE @kategorien TABLE (kPharmaKategorie INT, cKuerzel NVARCHAR(20), cName NVARCHAR(100));

    INSERT INTO @kategorien
    SELECT pk.kPharmaKategorie, pk.cKuerzel, pk.cName
    FROM NOVVIA.ArtikelPharmaKategorie apk
    INNER JOIN NOVVIA.PharmaKategorie pk ON pk.kPharmaKategorie = apk.kPharmaKategorie
    WHERE apk.kArtikel = @kArtikel AND pk.nErfordertValidierung = 1 AND pk.nAktiv = 1;

    IF NOT EXISTS (SELECT 1 FROM @kategorien)
    BEGIN
        RETURN; -- Artikel hat keine validierungspflichtigen Kategorien
    END

    -- Fuer jede Kategorie pruefen
    DECLARE @kKat INT, @cKat NVARCHAR(100);
    DECLARE cur CURSOR FOR SELECT kPharmaKategorie, cName FROM @kategorien;
    OPEN cur;
    FETCH NEXT FROM cur INTO @kKat, @cKat;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        DECLARE @nVal BIT = 0, @dGueltig DATETIME;

        IF @cEntitaet = 'LIEFERANT'
        BEGIN
            SELECT @nVal = nValidiert, @dGueltig = dGueltigBis
            FROM NOVVIA.LieferantValidierung
            WHERE kLieferant = @kEntitaet AND kPharmaKategorie = @kKat;
        END
        ELSE
        BEGIN
            SELECT @nVal = nValidiert, @dGueltig = dGueltigBis
            FROM NOVVIA.KundeValidierung
            WHERE kKunde = @kEntitaet AND kPharmaKategorie = @kKat;
        END

        -- Nicht validiert?
        IF ISNULL(@nVal, 0) = 0
        BEGIN
            SET @nIstValidiert = 0;
            SET @cFehlermeldung = 'Nicht fuer ' + @cKat + ' validiert';
            BREAK;
        END

        -- Abgelaufen?
        IF @dGueltig IS NOT NULL AND @dGueltig < GETDATE()
        BEGIN
            SET @nIstValidiert = 0;
            SET @cFehlermeldung = 'Validierung fuer ' + @cKat + ' abgelaufen am ' + CONVERT(NVARCHAR, @dGueltig, 104);
            BREAK;
        END

        FETCH NEXT FROM cur INTO @kKat, @cKat;
    END

    CLOSE cur;
    DEALLOCATE cur;
END
GO

PRINT 'Stored Procedure spNOVVIA_PruefePharmaValidierung erstellt';
GO

-- =====================================================
-- View: Lieferanten mit Validierungsstatus
-- =====================================================
IF EXISTS (SELECT * FROM sys.views WHERE name = 'lvLieferantValidierung' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP VIEW NOVVIA.lvLieferantValidierung;
GO

CREATE VIEW NOVVIA.lvLieferantValidierung AS
SELECT
    l.kLieferant,
    l.cFirma AS LieferantName,
    pk.kPharmaKategorie,
    pk.cKuerzel AS Kategorie,
    pk.cName AS KategorieName,
    pk.cFarbe,
    ISNULL(lv.nValidiert, 0) AS Validiert,
    lv.dValidiert,
    lv.dGueltigBis,
    CASE
        WHEN lv.dGueltigBis IS NOT NULL AND lv.dGueltigBis < GETDATE() THEN 1
        ELSE 0
    END AS Abgelaufen,
    lv.cZertifikatNr,
    lv.cBemerkung,
    lv.kValidiertVon
FROM dbo.tLieferant l
CROSS JOIN NOVVIA.PharmaKategorie pk
LEFT JOIN NOVVIA.LieferantValidierung lv
    ON lv.kLieferant = l.kLieferant AND lv.kPharmaKategorie = pk.kPharmaKategorie
WHERE pk.nAktiv = 1 AND pk.nErfordertValidierung = 1;
GO

PRINT 'View NOVVIA.lvLieferantValidierung erstellt';
GO

-- =====================================================
-- View: Kunden mit Validierungsstatus
-- =====================================================
IF EXISTS (SELECT * FROM sys.views WHERE name = 'lvKundeValidierung' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP VIEW NOVVIA.lvKundeValidierung;
GO

CREATE VIEW NOVVIA.lvKundeValidierung AS
SELECT
    k.kKunde,
    ISNULL(k.cFirma, k.cVorname + ' ' + k.cName) AS KundeName,
    pk.kPharmaKategorie,
    pk.cKuerzel AS Kategorie,
    pk.cName AS KategorieName,
    pk.cFarbe,
    ISNULL(kv.nValidiert, 0) AS Validiert,
    kv.dValidiert,
    kv.dGueltigBis,
    CASE
        WHEN kv.dGueltigBis IS NOT NULL AND kv.dGueltigBis < GETDATE() THEN 1
        ELSE 0
    END AS Abgelaufen,
    kv.cApothekennummer,
    kv.cBetriebserlaubnis,
    kv.cBemerkung,
    kv.kValidiertVon
FROM Kunde.tKunde k
CROSS JOIN NOVVIA.PharmaKategorie pk
LEFT JOIN NOVVIA.KundeValidierung kv
    ON kv.kKunde = k.kKunde AND kv.kPharmaKategorie = pk.kPharmaKategorie
WHERE pk.nAktiv = 1 AND pk.nErfordertValidierung = 1;
GO

PRINT 'View NOVVIA.lvKundeValidierung erstellt';
GO

PRINT '';
PRINT '=== Pharma-Validierung Setup abgeschlossen ===';
PRINT 'Standard-Kategorien: BTM, MEDCAN, RX, KUEHL, T-REZ, OTC';
PRINT 'Berechtigungspruefung: Nur Rolle RP darf Validierungen aendern';

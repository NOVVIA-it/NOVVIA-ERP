-- =====================================================
-- NOVVIA Benutzerrechte-System
-- =====================================================
-- Eigenes Benutzer- und Rechtesystem unabhaengig von JTL
-- Multi-Mandanten-faehig (Tabellen pro Mandant-DB)
-- =====================================================

-- WICHTIG: Mandant-Datenbank anpassen!
-- USE [Mandant_1]  -- Fuer Mandant 1
USE [Mandant_2]  -- Fuer Mandant 2
GO

-- Schema sicherstellen
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'NOVVIA')
    EXEC('CREATE SCHEMA NOVVIA');
GO

-- =====================================================
-- Tabelle: NOVVIA.Modul
-- Verfuegbare Module im System
-- =====================================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'NOVVIA' AND TABLE_NAME = 'Modul')
BEGIN
    CREATE TABLE NOVVIA.Modul (
        kModul INT IDENTITY(1,1) PRIMARY KEY,
        cName NVARCHAR(50) NOT NULL UNIQUE,          -- Technischer Name
        cBezeichnung NVARCHAR(100) NOT NULL,         -- Anzeigename
        cBeschreibung NVARCHAR(500) NULL,
        cIcon NVARCHAR(50) NULL,                     -- Icon-Name fuer UI
        nSortierung INT NOT NULL DEFAULT 0,
        nAktiv BIT NOT NULL DEFAULT 1
    );

    -- Standard-Module einfuegen
    INSERT INTO NOVVIA.Modul (cName, cBezeichnung, cIcon, nSortierung) VALUES
    ('Dashboard', 'Dashboard', 'Home', 10),
    ('Kunden', 'Kundenverwaltung', 'Users', 20),
    ('Artikel', 'Artikelverwaltung', 'Package', 30),
    ('Auftraege', 'Auftragsverwaltung', 'ShoppingCart', 40),
    ('Rechnungen', 'Rechnungsverwaltung', 'FileText', 50),
    ('Lieferscheine', 'Lieferscheine', 'Truck', 60),
    ('Lager', 'Lagerverwaltung', 'Warehouse', 70),
    ('Versand', 'Versand & Logistik', 'Send', 80),
    ('Einkauf', 'Einkauf & Bestellungen', 'ShoppingBag', 90),
    ('Lieferanten', 'Lieferantenverwaltung', 'Building', 100),
    ('Mahnungen', 'Mahnwesen', 'AlertTriangle', 110),
    ('Retouren', 'Retouren & RMA', 'RotateCcw', 120),
    ('Angebote', 'Angebotsverwaltung', 'FileEdit', 130),
    ('Berichte', 'Berichte & Auswertungen', 'BarChart', 140),
    ('WooCommerce', 'Shop-Synchronisation', 'Globe', 150),
    ('Einstellungen', 'Systemeinstellungen', 'Settings', 200),
    ('Benutzer', 'Benutzerverwaltung', 'UserCog', 210),
    ('Formulare', 'Formular-Designer', 'Layout', 220);

    PRINT 'Tabelle NOVVIA.Modul erstellt und befuellt.';
END
ELSE
    PRINT 'Tabelle NOVVIA.Modul existiert bereits.';
GO

-- =====================================================
-- Tabelle: NOVVIA.Aktion
-- Verfuegbare Aktionen pro Modul
-- =====================================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'NOVVIA' AND TABLE_NAME = 'Aktion')
BEGIN
    CREATE TABLE NOVVIA.Aktion (
        kAktion INT IDENTITY(1,1) PRIMARY KEY,
        cName NVARCHAR(50) NOT NULL UNIQUE,          -- Technischer Name
        cBezeichnung NVARCHAR(100) NOT NULL,         -- Anzeigename
        cBeschreibung NVARCHAR(500) NULL,
        nSortierung INT NOT NULL DEFAULT 0
    );

    -- Standard-Aktionen einfuegen
    INSERT INTO NOVVIA.Aktion (cName, cBezeichnung, nSortierung) VALUES
    ('Lesen', 'Anzeigen/Lesen', 10),
    ('Erstellen', 'Neu anlegen', 20),
    ('Bearbeiten', 'Bearbeiten/Aendern', 30),
    ('Loeschen', 'Loeschen', 40),
    ('Drucken', 'Drucken/PDF', 50),
    ('Email', 'E-Mail versenden', 60),
    ('Exportieren', 'Exportieren (CSV, Excel)', 70),
    ('Importieren', 'Importieren', 80),
    ('Stornieren', 'Stornieren/Gutschreiben', 90),
    ('Freigeben', 'Freigeben/Genehmigen', 100),
    ('Buchen', 'Buchen (Lager, Zahlung)', 110),
    ('Konfigurieren', 'Einstellungen aendern', 120),
    ('ValidierungBearbeiten', 'Pharma-Validierungsfelder bearbeiten', 130);

    PRINT 'Tabelle NOVVIA.Aktion erstellt und befuellt.';
END
ELSE
    PRINT 'Tabelle NOVVIA.Aktion existiert bereits.';
GO

-- =====================================================
-- Tabelle: NOVVIA.Recht
-- Kombinationen Modul + Aktion = Recht
-- =====================================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'NOVVIA' AND TABLE_NAME = 'Recht')
BEGIN
    CREATE TABLE NOVVIA.Recht (
        kRecht INT IDENTITY(1,1) PRIMARY KEY,
        kModul INT NOT NULL REFERENCES NOVVIA.Modul(kModul),
        kAktion INT NOT NULL REFERENCES NOVVIA.Aktion(kAktion),
        cSchluessel NVARCHAR(100) NOT NULL UNIQUE,   -- z.B. "Kunden.Lesen", "Auftraege.Erstellen"
        cBeschreibung NVARCHAR(500) NULL,
        nAktiv BIT NOT NULL DEFAULT 1,

        CONSTRAINT UQ_Recht_ModulAktion UNIQUE (kModul, kAktion)
    );

    -- Index fuer schnelle Abfragen
    CREATE INDEX IX_Recht_Schluessel ON NOVVIA.Recht(cSchluessel);

    PRINT 'Tabelle NOVVIA.Recht erstellt.';
END
ELSE
    PRINT 'Tabelle NOVVIA.Recht existiert bereits.';
GO

-- =====================================================
-- SP: Rechte generieren (Modul x Aktion)
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spRechteGenerieren')
    DROP PROCEDURE NOVVIA.spRechteGenerieren;
GO

CREATE PROCEDURE NOVVIA.spRechteGenerieren
AS
BEGIN
    SET NOCOUNT ON;

    -- Alle Kombinationen Modul x Aktion als Rechte anlegen
    INSERT INTO NOVVIA.Recht (kModul, kAktion, cSchluessel, cBeschreibung)
    SELECT
        m.kModul,
        a.kAktion,
        m.cName + '.' + a.cName,
        m.cBezeichnung + ': ' + a.cBezeichnung
    FROM NOVVIA.Modul m
    CROSS JOIN NOVVIA.Aktion a
    WHERE NOT EXISTS (
        SELECT 1 FROM NOVVIA.Recht r
        WHERE r.kModul = m.kModul AND r.kAktion = a.kAktion
    );

    PRINT 'Rechte generiert: ' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + ' neue Eintraege.';
END
GO

-- Rechte generieren
EXEC NOVVIA.spRechteGenerieren;
GO

-- =====================================================
-- Tabelle: NOVVIA.Rolle
-- Benutzerrollen (Admin, Verkauf, Lager, etc.)
-- =====================================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'NOVVIA' AND TABLE_NAME = 'Rolle')
BEGIN
    CREATE TABLE NOVVIA.Rolle (
        kRolle INT IDENTITY(1,1) PRIMARY KEY,
        cName NVARCHAR(50) NOT NULL UNIQUE,
        cBezeichnung NVARCHAR(100) NOT NULL,
        cBeschreibung NVARCHAR(500) NULL,
        nIstSystem BIT NOT NULL DEFAULT 0,           -- System-Rollen nicht loeschbar
        nIstAdmin BIT NOT NULL DEFAULT 0,            -- Hat alle Rechte
        nAktiv BIT NOT NULL DEFAULT 1,
        dErstellt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
        dGeaendert DATETIME2 NULL
    );

    -- Standard-Rollen einfuegen
    INSERT INTO NOVVIA.Rolle (cName, cBezeichnung, cBeschreibung, nIstSystem, nIstAdmin) VALUES
    ('Admin', 'Administrator', 'Vollzugriff auf alle Funktionen + Systemverwaltung', 1, 1),
    ('Alles', 'Alle Rechte', 'Vollzugriff auf alle Module und Aktionen (ohne Systemverwaltung)', 1, 0),
    ('Geschaeftsfuehrung', 'Geschaeftsfuehrung', 'Lese- und Genehmigungsrechte', 1, 0),
    ('Verkauf', 'Verkauf/Vertrieb', 'Kunden, Angebote, Auftraege, Rechnungen', 1, 0),
    ('Lager', 'Lager/Logistik', 'Lager, Versand, Wareneingang', 1, 0),
    ('Einkauf', 'Einkauf', 'Lieferanten, Bestellungen, Wareneingang', 1, 0),
    ('Buchhaltung', 'Buchhaltung', 'Rechnungen, Zahlungen, Mahnungen', 1, 0),
    ('Kundenservice', 'Kundenservice', 'Kunden, Auftraege (nur Lesen), Retouren', 1, 0),
    ('Readonly', 'Nur Lesen', 'Lesezugriff auf alle Module', 1, 0),
    ('RP', 'Responsible Person (Pharma)', 'Pharma-Validierungsfelder bei Kunden und Lieferanten bearbeiten', 1, 0);

    PRINT 'Tabelle NOVVIA.Rolle erstellt und befuellt.';
END
ELSE
    PRINT 'Tabelle NOVVIA.Rolle existiert bereits.';
GO

-- =====================================================
-- Tabelle: NOVVIA.RolleRecht
-- Zuordnung Rechte zu Rollen
-- =====================================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'NOVVIA' AND TABLE_NAME = 'RolleRecht')
BEGIN
    CREATE TABLE NOVVIA.RolleRecht (
        kRolleRecht INT IDENTITY(1,1) PRIMARY KEY,
        kRolle INT NOT NULL REFERENCES NOVVIA.Rolle(kRolle) ON DELETE CASCADE,
        kRecht INT NOT NULL REFERENCES NOVVIA.Recht(kRecht) ON DELETE CASCADE,
        nErlaubt BIT NOT NULL DEFAULT 1,             -- 1 = erlaubt, 0 = explizit verboten

        CONSTRAINT UQ_RolleRecht UNIQUE (kRolle, kRecht)
    );

    CREATE INDEX IX_RolleRecht_Rolle ON NOVVIA.RolleRecht(kRolle);

    PRINT 'Tabelle NOVVIA.RolleRecht erstellt.';
END
ELSE
    PRINT 'Tabelle NOVVIA.RolleRecht existiert bereits.';
GO

-- =====================================================
-- SP: Rollen-Rechte zuweisen (Batch)
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spRolleRechteZuweisen')
    DROP PROCEDURE NOVVIA.spRolleRechteZuweisen;
GO

CREATE PROCEDURE NOVVIA.spRolleRechteZuweisen
    @cRolleName NVARCHAR(50),
    @cModulPattern NVARCHAR(100) = '%',              -- z.B. 'Kunden' oder '%' fuer alle
    @cAktionPattern NVARCHAR(100) = '%'              -- z.B. 'Lesen' oder '%' fuer alle
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @kRolle INT;
    SELECT @kRolle = kRolle FROM NOVVIA.Rolle WHERE cName = @cRolleName;

    IF @kRolle IS NULL
    BEGIN
        RAISERROR('Rolle nicht gefunden: %s', 16, 1, @cRolleName);
        RETURN;
    END

    INSERT INTO NOVVIA.RolleRecht (kRolle, kRecht, nErlaubt)
    SELECT @kRolle, r.kRecht, 1
    FROM NOVVIA.Recht r
    JOIN NOVVIA.Modul m ON r.kModul = m.kModul
    JOIN NOVVIA.Aktion a ON r.kAktion = a.kAktion
    WHERE m.cName LIKE @cModulPattern
      AND a.cName LIKE @cAktionPattern
      AND NOT EXISTS (
          SELECT 1 FROM NOVVIA.RolleRecht rr
          WHERE rr.kRolle = @kRolle AND rr.kRecht = r.kRecht
      );

    PRINT 'Rechte zugewiesen: ' + CAST(@@ROWCOUNT AS NVARCHAR(10));
END
GO

-- Standard-Rechte fuer Rollen zuweisen

-- Alles: Alle Rechte ausser Benutzer-Konfiguration
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Alles', @cModulPattern = '%', @cAktionPattern = '%';
-- Benutzer-Konfiguration entfernen (nur Admin)
DELETE rr FROM NOVVIA.RolleRecht rr
JOIN NOVVIA.Rolle r ON rr.kRolle = r.kRolle
JOIN NOVVIA.Recht re ON rr.kRecht = re.kRecht
WHERE r.cName = 'Alles' AND re.cSchluessel IN ('Benutzer.Konfigurieren', 'Einstellungen.Konfigurieren');

-- Readonly: Nur Lesen auf alles
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Readonly', @cAktionPattern = 'Lesen';

-- Verkauf: Kunden, Angebote, Auftraege, Rechnungen - alles ausser Loeschen und Konfigurieren
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Verkauf', @cModulPattern = 'Kunden', @cAktionPattern = '%';
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Verkauf', @cModulPattern = 'Artikel', @cAktionPattern = 'Lesen';
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Verkauf', @cModulPattern = 'Angebote', @cAktionPattern = '%';
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Verkauf', @cModulPattern = 'Auftraege', @cAktionPattern = '%';
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Verkauf', @cModulPattern = 'Rechnungen', @cAktionPattern = '%';
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Verkauf', @cModulPattern = 'Dashboard', @cAktionPattern = 'Lesen';

-- Lager: Lager, Versand, Artikel
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Lager', @cModulPattern = 'Lager', @cAktionPattern = '%';
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Lager', @cModulPattern = 'Versand', @cAktionPattern = '%';
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Lager', @cModulPattern = 'Artikel', @cAktionPattern = 'Lesen';
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Lager', @cModulPattern = 'Auftraege', @cAktionPattern = 'Lesen';
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Lager', @cModulPattern = 'Lieferscheine', @cAktionPattern = '%';
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Lager', @cModulPattern = 'Dashboard', @cAktionPattern = 'Lesen';

-- Einkauf: Lieferanten, Einkauf, Artikel
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Einkauf', @cModulPattern = 'Einkauf', @cAktionPattern = '%';
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Einkauf', @cModulPattern = 'Lieferanten', @cAktionPattern = '%';
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Einkauf', @cModulPattern = 'Artikel', @cAktionPattern = '%';
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Einkauf', @cModulPattern = 'Lager', @cAktionPattern = 'Lesen';
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Einkauf', @cModulPattern = 'Dashboard', @cAktionPattern = 'Lesen';

-- Buchhaltung: Rechnungen, Mahnungen, Zahlungen
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Buchhaltung', @cModulPattern = 'Rechnungen', @cAktionPattern = '%';
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Buchhaltung', @cModulPattern = 'Mahnungen', @cAktionPattern = '%';
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Buchhaltung', @cModulPattern = 'Kunden', @cAktionPattern = 'Lesen';
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Buchhaltung', @cModulPattern = 'Auftraege', @cAktionPattern = 'Lesen';
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Buchhaltung', @cModulPattern = 'Dashboard', @cAktionPattern = 'Lesen';
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Buchhaltung', @cModulPattern = 'Berichte', @cAktionPattern = '%';

-- Kundenservice: Kunden, Auftraege (lesen), Retouren
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Kundenservice', @cModulPattern = 'Kunden', @cAktionPattern = 'Lesen';
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Kundenservice', @cModulPattern = 'Kunden', @cAktionPattern = 'Bearbeiten';
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Kundenservice', @cModulPattern = 'Auftraege', @cAktionPattern = 'Lesen';
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Kundenservice', @cModulPattern = 'Rechnungen', @cAktionPattern = 'Lesen';
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Kundenservice', @cModulPattern = 'Retouren', @cAktionPattern = '%';
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Kundenservice', @cModulPattern = 'Dashboard', @cAktionPattern = 'Lesen';

-- Geschaeftsfuehrung: Alles lesen + Freigeben + Berichte
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Geschaeftsfuehrung', @cAktionPattern = 'Lesen';
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Geschaeftsfuehrung', @cAktionPattern = 'Freigeben';
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'Geschaeftsfuehrung', @cModulPattern = 'Berichte', @cAktionPattern = '%';

-- RP (Responsible Person Pharma): Validierungsfelder bei Kunden und Lieferanten bearbeiten
-- Nur diese Rolle darf ValidierungBearbeiten wenn Firma PHARMA=1 hat
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'RP', @cModulPattern = 'Kunden', @cAktionPattern = 'ValidierungBearbeiten';
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'RP', @cModulPattern = 'Lieferanten', @cAktionPattern = 'ValidierungBearbeiten';
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'RP', @cModulPattern = 'Kunden', @cAktionPattern = 'Lesen';
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'RP', @cModulPattern = 'Lieferanten', @cAktionPattern = 'Lesen';
EXEC NOVVIA.spRolleRechteZuweisen @cRolleName = 'RP', @cModulPattern = 'Dashboard', @cAktionPattern = 'Lesen';
GO

-- =====================================================
-- Tabelle: NOVVIA.Benutzer
-- Eigene Benutzerverwaltung (unabhaengig von JTL)
-- =====================================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'NOVVIA' AND TABLE_NAME = 'Benutzer')
BEGIN
    CREATE TABLE NOVVIA.Benutzer (
        kBenutzer INT IDENTITY(1,1) PRIMARY KEY,
        cBenutzername NVARCHAR(50) NOT NULL UNIQUE,
        cPasswortHash NVARCHAR(255) NOT NULL,        -- BCrypt Hash
        cVorname NVARCHAR(100) NULL,
        cNachname NVARCHAR(100) NULL,
        cEmail NVARCHAR(255) NULL,
        cTelefon NVARCHAR(50) NULL,

        -- Verknuepfung zu JTL (optional)
        kJtlBenutzer INT NULL,                       -- FK zu tBenutzer (optional)

        -- Status
        nAktiv BIT NOT NULL DEFAULT 1,
        nGesperrt BIT NOT NULL DEFAULT 0,
        cSperrgrund NVARCHAR(500) NULL,
        nFehlversuche INT NOT NULL DEFAULT 0,
        dLetzteAnmeldung DATETIME2 NULL,
        dPasswortAblauf DATETIME2 NULL,              -- Passwort muss geaendert werden

        -- Einstellungen
        cSprache NVARCHAR(10) NOT NULL DEFAULT 'de',
        cTheme NVARCHAR(20) NOT NULL DEFAULT 'Light',
        cStartseite NVARCHAR(50) NULL,               -- Standard-Startseite
        cEinstellungen NVARCHAR(MAX) NULL,           -- JSON fuer weitere Einstellungen

        -- Timestamps
        dErstellt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
        dGeaendert DATETIME2 NULL,
        kErstelltVon INT NULL,
        kGeaendertVon INT NULL
    );

    -- Admin-Benutzer anlegen (Passwort: admin123 - MUSS geaendert werden!)
    -- BCrypt Hash fuer 'admin123': $2a$11$... (wird in C# generiert)
    INSERT INTO NOVVIA.Benutzer (cBenutzername, cPasswortHash, cVorname, cNachname, cEmail, dPasswortAblauf)
    VALUES ('admin', '$2a$11$placeholder_hash_must_be_set', 'System', 'Administrator', 'admin@novvia.local', DATEADD(DAY, 1, SYSDATETIME()));

    PRINT 'Tabelle NOVVIA.Benutzer erstellt.';
END
ELSE
    PRINT 'Tabelle NOVVIA.Benutzer existiert bereits.';
GO

-- =====================================================
-- Tabelle: NOVVIA.BenutzerRolle
-- Zuordnung Benutzer zu Rollen (n:m)
-- =====================================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'NOVVIA' AND TABLE_NAME = 'BenutzerRolle')
BEGIN
    CREATE TABLE NOVVIA.BenutzerRolle (
        kBenutzerRolle INT IDENTITY(1,1) PRIMARY KEY,
        kBenutzer INT NOT NULL REFERENCES NOVVIA.Benutzer(kBenutzer) ON DELETE CASCADE,
        kRolle INT NOT NULL REFERENCES NOVVIA.Rolle(kRolle) ON DELETE CASCADE,
        dGueltigVon DATETIME2 NULL,                  -- Zeitlich begrenzte Rolle
        dGueltigBis DATETIME2 NULL,
        dErstellt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
        kErstelltVon INT NULL,

        CONSTRAINT UQ_BenutzerRolle UNIQUE (kBenutzer, kRolle)
    );

    CREATE INDEX IX_BenutzerRolle_Benutzer ON NOVVIA.BenutzerRolle(kBenutzer);

    -- Admin-Benutzer Admin-Rolle zuweisen
    INSERT INTO NOVVIA.BenutzerRolle (kBenutzer, kRolle)
    SELECT b.kBenutzer, r.kRolle
    FROM NOVVIA.Benutzer b, NOVVIA.Rolle r
    WHERE b.cBenutzername = 'admin' AND r.cName = 'Admin';

    PRINT 'Tabelle NOVVIA.BenutzerRolle erstellt.';
END
ELSE
    PRINT 'Tabelle NOVVIA.BenutzerRolle existiert bereits.';
GO

-- =====================================================
-- Tabelle: NOVVIA.BenutzerRechtUeberschreibung
-- Individuelle Rechte-Ueberschreibungen pro Benutzer
-- =====================================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'NOVVIA' AND TABLE_NAME = 'BenutzerRechtUeberschreibung')
BEGIN
    CREATE TABLE NOVVIA.BenutzerRechtUeberschreibung (
        kUeberschreibung INT IDENTITY(1,1) PRIMARY KEY,
        kBenutzer INT NOT NULL REFERENCES NOVVIA.Benutzer(kBenutzer) ON DELETE CASCADE,
        kRecht INT NOT NULL REFERENCES NOVVIA.Recht(kRecht) ON DELETE CASCADE,
        nErlaubt BIT NOT NULL,                       -- 1 = zusaetzlich erlaubt, 0 = explizit verboten
        cGrund NVARCHAR(500) NULL,                   -- Begruendung
        dGueltigBis DATETIME2 NULL,                  -- Temporaere Berechtigung
        dErstellt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
        kErstelltVon INT NULL,

        CONSTRAINT UQ_BenutzerRecht UNIQUE (kBenutzer, kRecht)
    );

    PRINT 'Tabelle NOVVIA.BenutzerRechtUeberschreibung erstellt.';
END
ELSE
    PRINT 'Tabelle NOVVIA.BenutzerRechtUeberschreibung existiert bereits.';
GO

-- =====================================================
-- Tabelle: NOVVIA.BenutzerSession
-- Aktive Sitzungen
-- =====================================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'NOVVIA' AND TABLE_NAME = 'BenutzerSession')
BEGIN
    CREATE TABLE NOVVIA.BenutzerSession (
        kSession INT IDENTITY(1,1) PRIMARY KEY,
        cSessionToken NVARCHAR(255) NOT NULL UNIQUE,
        kBenutzer INT NOT NULL REFERENCES NOVVIA.Benutzer(kBenutzer) ON DELETE CASCADE,
        cRechnername NVARCHAR(100) NULL,
        cIP NVARCHAR(50) NULL,
        cUserAgent NVARCHAR(500) NULL,               -- Browser/Client Info
        cAnwendung NVARCHAR(50) NULL,                -- WPF, API, Web
        dErstellt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
        dLetzteAktivitaet DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
        dAblauf DATETIME2 NOT NULL,
        nAktiv BIT NOT NULL DEFAULT 1
    );

    CREATE INDEX IX_BenutzerSession_Benutzer ON NOVVIA.BenutzerSession(kBenutzer);
    CREATE INDEX IX_BenutzerSession_Token ON NOVVIA.BenutzerSession(cSessionToken);
    CREATE INDEX IX_BenutzerSession_Ablauf ON NOVVIA.BenutzerSession(dAblauf) WHERE nAktiv = 1;

    PRINT 'Tabelle NOVVIA.BenutzerSession erstellt.';
END
ELSE
    PRINT 'Tabelle NOVVIA.BenutzerSession existiert bereits.';
GO

-- =====================================================
-- Tabelle: NOVVIA.BenutzerLog
-- Login-Historie und Sicherheits-Events
-- =====================================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'NOVVIA' AND TABLE_NAME = 'BenutzerLog')
BEGIN
    CREATE TABLE NOVVIA.BenutzerLog (
        kBenutzerLog BIGINT IDENTITY(1,1) PRIMARY KEY,
        kBenutzer INT NULL REFERENCES NOVVIA.Benutzer(kBenutzer),
        cBenutzername NVARCHAR(50) NULL,             -- Auch bei fehlgeschlagenen Logins
        cAktion NVARCHAR(50) NOT NULL,               -- Login, Logout, PasswortAenderung, Gesperrt, etc.
        nErfolgreich BIT NOT NULL,
        cDetails NVARCHAR(MAX) NULL,
        cRechnername NVARCHAR(100) NULL,
        cIP NVARCHAR(50) NULL,
        dZeitpunkt DATETIME2 NOT NULL DEFAULT SYSDATETIME()
    );

    CREATE INDEX IX_BenutzerLog_Benutzer ON NOVVIA.BenutzerLog(kBenutzer, dZeitpunkt DESC);
    CREATE INDEX IX_BenutzerLog_Zeitpunkt ON NOVVIA.BenutzerLog(dZeitpunkt DESC);

    PRINT 'Tabelle NOVVIA.BenutzerLog erstellt.';
END
ELSE
    PRINT 'Tabelle NOVVIA.BenutzerLog existiert bereits.';
GO

-- =====================================================
-- View: NOVVIA.vBenutzerRechte
-- Effektive Rechte pro Benutzer (aus Rollen + Ueberschreibungen)
-- =====================================================
IF EXISTS (SELECT * FROM sys.views WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'vBenutzerRechte')
    DROP VIEW NOVVIA.vBenutzerRechte;
GO

CREATE VIEW NOVVIA.vBenutzerRechte AS
WITH RollenRechte AS (
    -- Rechte aus allen zugewiesenen Rollen
    SELECT DISTINCT
        br.kBenutzer,
        rr.kRecht,
        rr.nErlaubt
    FROM NOVVIA.BenutzerRolle br
    JOIN NOVVIA.Rolle r ON br.kRolle = r.kRolle
    JOIN NOVVIA.RolleRecht rr ON r.kRolle = rr.kRolle
    WHERE r.nAktiv = 1
      AND (br.dGueltigVon IS NULL OR br.dGueltigVon <= SYSDATETIME())
      AND (br.dGueltigBis IS NULL OR br.dGueltigBis >= SYSDATETIME())
),
AdminRechte AS (
    -- Admin-Rollen haben alle Rechte
    SELECT DISTINCT
        br.kBenutzer,
        re.kRecht,
        CAST(1 AS BIT) AS nErlaubt
    FROM NOVVIA.BenutzerRolle br
    JOIN NOVVIA.Rolle r ON br.kRolle = r.kRolle
    CROSS JOIN NOVVIA.Recht re
    WHERE r.nIstAdmin = 1 AND r.nAktiv = 1
      AND (br.dGueltigVon IS NULL OR br.dGueltigVon <= SYSDATETIME())
      AND (br.dGueltigBis IS NULL OR br.dGueltigBis >= SYSDATETIME())
),
AlleRollenRechte AS (
    SELECT * FROM RollenRechte
    UNION
    SELECT * FROM AdminRechte
)
SELECT
    b.kBenutzer,
    b.cBenutzername,
    re.kRecht,
    re.cSchluessel,
    m.cName AS cModul,
    a.cName AS cAktion,
    -- Ueberschreibung hat Vorrang
    COALESCE(u.nErlaubt, rr.nErlaubt, CAST(0 AS BIT)) AS nErlaubt,
    CASE WHEN u.kUeberschreibung IS NOT NULL THEN 'Ueberschreibung'
         WHEN rr.kRecht IS NOT NULL THEN 'Rolle'
         ELSE 'Keine' END AS cQuelle
FROM NOVVIA.Benutzer b
CROSS JOIN NOVVIA.Recht re
JOIN NOVVIA.Modul m ON re.kModul = m.kModul
JOIN NOVVIA.Aktion a ON re.kAktion = a.kAktion
LEFT JOIN AlleRollenRechte rr ON b.kBenutzer = rr.kBenutzer AND re.kRecht = rr.kRecht
LEFT JOIN NOVVIA.BenutzerRechtUeberschreibung u ON b.kBenutzer = u.kBenutzer AND re.kRecht = u.kRecht
    AND (u.dGueltigBis IS NULL OR u.dGueltigBis >= SYSDATETIME())
WHERE b.nAktiv = 1
  AND re.nAktiv = 1;
GO

PRINT 'View NOVVIA.vBenutzerRechte erstellt.';
GO

-- =====================================================
-- SP: NOVVIA.spHatRecht
-- Prueft ob Benutzer ein bestimmtes Recht hat
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spHatRecht')
    DROP PROCEDURE NOVVIA.spHatRecht;
GO

CREATE PROCEDURE NOVVIA.spHatRecht
    @kBenutzer INT,
    @cRechtSchluessel NVARCHAR(100),                 -- z.B. 'Kunden.Bearbeiten'
    @nHatRecht BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT @nHatRecht = COALESCE(MAX(CAST(nErlaubt AS INT)), 0)
    FROM NOVVIA.vBenutzerRechte
    WHERE kBenutzer = @kBenutzer
      AND cSchluessel = @cRechtSchluessel
      AND nErlaubt = 1;
END
GO

PRINT 'SP NOVVIA.spHatRecht erstellt.';
GO

-- =====================================================
-- SP: NOVVIA.spBenutzerRechteAbfragen
-- Alle Rechte eines Benutzers
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spBenutzerRechteAbfragen')
    DROP PROCEDURE NOVVIA.spBenutzerRechteAbfragen;
GO

CREATE PROCEDURE NOVVIA.spBenutzerRechteAbfragen
    @kBenutzer INT,
    @cModul NVARCHAR(50) = NULL                      -- Optional: Nur bestimmtes Modul
AS
BEGIN
    SET NOCOUNT ON;

    SELECT cSchluessel, cModul, cAktion, nErlaubt, cQuelle
    FROM NOVVIA.vBenutzerRechte
    WHERE kBenutzer = @kBenutzer
      AND nErlaubt = 1
      AND (@cModul IS NULL OR cModul = @cModul)
    ORDER BY cModul, cAktion;
END
GO

PRINT 'SP NOVVIA.spBenutzerRechteAbfragen erstellt.';
GO

-- =====================================================
-- SP: NOVVIA.spBenutzerAnmelden
-- Login mit Protokollierung
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spBenutzerAnmelden')
    DROP PROCEDURE NOVVIA.spBenutzerAnmelden;
GO

CREATE PROCEDURE NOVVIA.spBenutzerAnmelden
    @cBenutzername NVARCHAR(50),
    @cRechnername NVARCHAR(100) = NULL,
    @cIP NVARCHAR(50) = NULL,
    @cAnwendung NVARCHAR(50) = 'WPF',
    @nSessionDauerMinuten INT = 480,                 -- 8 Stunden Standard
    @kBenutzer INT OUTPUT,
    @cSessionToken NVARCHAR(255) OUTPUT,
    @cFehler NVARCHAR(500) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @kBenutzer = NULL;
    SET @cSessionToken = NULL;
    SET @cFehler = NULL;

    -- Benutzer suchen
    SELECT @kBenutzer = kBenutzer
    FROM NOVVIA.Benutzer
    WHERE cBenutzername = @cBenutzername;

    IF @kBenutzer IS NULL
    BEGIN
        SET @cFehler = 'Benutzer nicht gefunden';

        INSERT INTO NOVVIA.BenutzerLog (cBenutzername, cAktion, nErfolgreich, cDetails, cRechnername, cIP)
        VALUES (@cBenutzername, 'Login', 0, @cFehler, @cRechnername, @cIP);

        RETURN;
    END

    -- Pruefungen
    DECLARE @nAktiv BIT, @nGesperrt BIT, @cSperrgrund NVARCHAR(500), @nFehlversuche INT;

    SELECT @nAktiv = nAktiv, @nGesperrt = nGesperrt, @cSperrgrund = cSperrgrund, @nFehlversuche = nFehlversuche
    FROM NOVVIA.Benutzer
    WHERE kBenutzer = @kBenutzer;

    IF @nAktiv = 0
    BEGIN
        SET @cFehler = 'Benutzer ist deaktiviert';
        SET @kBenutzer = NULL;

        INSERT INTO NOVVIA.BenutzerLog (kBenutzer, cBenutzername, cAktion, nErfolgreich, cDetails, cRechnername, cIP)
        VALUES (@kBenutzer, @cBenutzername, 'Login', 0, @cFehler, @cRechnername, @cIP);

        RETURN;
    END

    IF @nGesperrt = 1
    BEGIN
        SET @cFehler = 'Benutzer ist gesperrt: ' + ISNULL(@cSperrgrund, 'Kein Grund angegeben');
        SET @kBenutzer = NULL;

        INSERT INTO NOVVIA.BenutzerLog (kBenutzer, cBenutzername, cAktion, nErfolgreich, cDetails, cRechnername, cIP)
        VALUES (@kBenutzer, @cBenutzername, 'Login', 0, @cFehler, @cRechnername, @cIP);

        RETURN;
    END

    -- Session erstellen
    SET @cSessionToken = CONVERT(NVARCHAR(255), NEWID()) + '-' + CONVERT(NVARCHAR(255), NEWID());

    INSERT INTO NOVVIA.BenutzerSession (cSessionToken, kBenutzer, cRechnername, cIP, cAnwendung, dAblauf)
    VALUES (@cSessionToken, @kBenutzer, @cRechnername, @cIP, @cAnwendung, DATEADD(MINUTE, @nSessionDauerMinuten, SYSDATETIME()));

    -- Letzte Anmeldung aktualisieren
    UPDATE NOVVIA.Benutzer
    SET dLetzteAnmeldung = SYSDATETIME(), nFehlversuche = 0
    WHERE kBenutzer = @kBenutzer;

    -- Erfolg loggen
    INSERT INTO NOVVIA.BenutzerLog (kBenutzer, cBenutzername, cAktion, nErfolgreich, cRechnername, cIP)
    VALUES (@kBenutzer, @cBenutzername, 'Login', 1, @cRechnername, @cIP);
END
GO

PRINT 'SP NOVVIA.spBenutzerAnmelden erstellt.';
GO

-- =====================================================
-- SP: NOVVIA.spBenutzerAbmelden
-- Logout
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spBenutzerAbmelden')
    DROP PROCEDURE NOVVIA.spBenutzerAbmelden;
GO

CREATE PROCEDURE NOVVIA.spBenutzerAbmelden
    @cSessionToken NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @kBenutzer INT, @cBenutzername NVARCHAR(50);

    SELECT @kBenutzer = s.kBenutzer, @cBenutzername = b.cBenutzername
    FROM NOVVIA.BenutzerSession s
    JOIN NOVVIA.Benutzer b ON s.kBenutzer = b.kBenutzer
    WHERE s.cSessionToken = @cSessionToken;

    -- Session deaktivieren
    UPDATE NOVVIA.BenutzerSession
    SET nAktiv = 0
    WHERE cSessionToken = @cSessionToken;

    -- Logout loggen
    IF @kBenutzer IS NOT NULL
    BEGIN
        INSERT INTO NOVVIA.BenutzerLog (kBenutzer, cBenutzername, cAktion, nErfolgreich)
        VALUES (@kBenutzer, @cBenutzername, 'Logout', 1);
    END
END
GO

PRINT 'SP NOVVIA.spBenutzerAbmelden erstellt.';
GO

-- =====================================================
-- SP: NOVVIA.spSessionValidieren
-- Session pruefen und verlaengern
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spSessionValidieren')
    DROP PROCEDURE NOVVIA.spSessionValidieren;
GO

CREATE PROCEDURE NOVVIA.spSessionValidieren
    @cSessionToken NVARCHAR(255),
    @nVerlaengernMinuten INT = 60,
    @kBenutzer INT OUTPUT,
    @nGueltig BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @kBenutzer = NULL;
    SET @nGueltig = 0;

    SELECT @kBenutzer = s.kBenutzer
    FROM NOVVIA.BenutzerSession s
    JOIN NOVVIA.Benutzer b ON s.kBenutzer = b.kBenutzer
    WHERE s.cSessionToken = @cSessionToken
      AND s.nAktiv = 1
      AND s.dAblauf > SYSDATETIME()
      AND b.nAktiv = 1
      AND b.nGesperrt = 0;

    IF @kBenutzer IS NOT NULL
    BEGIN
        SET @nGueltig = 1;

        -- Session verlaengern
        UPDATE NOVVIA.BenutzerSession
        SET dLetzteAktivitaet = SYSDATETIME(),
            dAblauf = DATEADD(MINUTE, @nVerlaengernMinuten, SYSDATETIME())
        WHERE cSessionToken = @cSessionToken;
    END
END
GO

PRINT 'SP NOVVIA.spSessionValidieren erstellt.';
GO

-- =====================================================
-- SP: NOVVIA.spFehlversuchRegistrieren
-- Bei falschem Passwort
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spFehlversuchRegistrieren')
    DROP PROCEDURE NOVVIA.spFehlversuchRegistrieren;
GO

CREATE PROCEDURE NOVVIA.spFehlversuchRegistrieren
    @cBenutzername NVARCHAR(50),
    @cRechnername NVARCHAR(100) = NULL,
    @cIP NVARCHAR(50) = NULL,
    @nMaxFehlversuche INT = 5
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @kBenutzer INT, @nFehlversuche INT;

    SELECT @kBenutzer = kBenutzer, @nFehlversuche = nFehlversuche
    FROM NOVVIA.Benutzer
    WHERE cBenutzername = @cBenutzername;

    IF @kBenutzer IS NOT NULL
    BEGIN
        SET @nFehlversuche = @nFehlversuche + 1;

        -- Benutzer sperren wenn zu viele Fehlversuche
        IF @nFehlversuche >= @nMaxFehlversuche
        BEGIN
            UPDATE NOVVIA.Benutzer
            SET nFehlversuche = @nFehlversuche,
                nGesperrt = 1,
                cSperrgrund = 'Automatisch gesperrt nach ' + CAST(@nFehlversuche AS NVARCHAR(10)) + ' Fehlversuchen'
            WHERE kBenutzer = @kBenutzer;

            INSERT INTO NOVVIA.BenutzerLog (kBenutzer, cBenutzername, cAktion, nErfolgreich, cDetails, cRechnername, cIP)
            VALUES (@kBenutzer, @cBenutzername, 'Gesperrt', 1, 'Automatisch nach Fehlversuchen', @cRechnername, @cIP);
        END
        ELSE
        BEGIN
            UPDATE NOVVIA.Benutzer
            SET nFehlversuche = @nFehlversuche
            WHERE kBenutzer = @kBenutzer;
        END
    END

    -- Fehlversuch loggen
    INSERT INTO NOVVIA.BenutzerLog (kBenutzer, cBenutzername, cAktion, nErfolgreich, cDetails, cRechnername, cIP)
    VALUES (@kBenutzer, @cBenutzername, 'Login', 0, 'Falsches Passwort (Versuch ' + CAST(ISNULL(@nFehlversuche, 0) AS NVARCHAR(10)) + ')', @cRechnername, @cIP);
END
GO

PRINT 'SP NOVVIA.spFehlversuchRegistrieren erstellt.';
GO

-- =====================================================
-- Aufraeum-Job: Abgelaufene Sessions loeschen
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spSessionsAufraeumen')
    DROP PROCEDURE NOVVIA.spSessionsAufraeumen;
GO

CREATE PROCEDURE NOVVIA.spSessionsAufraeumen
AS
BEGIN
    SET NOCOUNT ON;

    -- Abgelaufene Sessions deaktivieren
    UPDATE NOVVIA.BenutzerSession
    SET nAktiv = 0
    WHERE dAblauf < SYSDATETIME() AND nAktiv = 1;

    -- Alte Sessions loeschen (aelter als 30 Tage)
    DELETE FROM NOVVIA.BenutzerSession
    WHERE dErstellt < DATEADD(DAY, -30, SYSDATETIME());

    -- Alte Logs loeschen (aelter als 1 Jahr)
    DELETE FROM NOVVIA.BenutzerLog
    WHERE dZeitpunkt < DATEADD(YEAR, -1, SYSDATETIME());
END
GO

PRINT 'SP NOVVIA.spSessionsAufraeumen erstellt.';
GO

-- =====================================================
-- Tabelle: NOVVIA.FirmaEinstellung
-- Firmenweite Einstellungen (z.B. PHARMA-Modus)
-- =====================================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'NOVVIA' AND TABLE_NAME = 'FirmaEinstellung')
BEGIN
    CREATE TABLE NOVVIA.FirmaEinstellung (
        kFirmaEinstellung INT IDENTITY(1,1) PRIMARY KEY,
        cSchluessel NVARCHAR(100) NOT NULL UNIQUE,
        cWert NVARCHAR(MAX) NULL,
        cBeschreibung NVARCHAR(500) NULL,
        dGeaendert DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
        kGeaendertVon INT NULL
    );

    -- Standard-Einstellungen
    INSERT INTO NOVVIA.FirmaEinstellung (cSchluessel, cWert, cBeschreibung) VALUES
    ('PHARMA', '0', 'Pharma-Modus: 1 = Aktiv, nur RP darf Validierungsfelder bearbeiten'),
    ('FIRMENNAME', 'NOVVIA', 'Firmenname fuer Berichte und Dokumente');

    PRINT 'Tabelle NOVVIA.FirmaEinstellung erstellt.';
END
ELSE
    PRINT 'Tabelle NOVVIA.FirmaEinstellung existiert bereits.';
GO

-- =====================================================
-- SP: NOVVIA.spFirmaEinstellungLesen
-- Einstellung abfragen
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spFirmaEinstellungLesen')
    DROP PROCEDURE NOVVIA.spFirmaEinstellungLesen;
GO

CREATE PROCEDURE NOVVIA.spFirmaEinstellungLesen
    @cSchluessel NVARCHAR(100),
    @cWert NVARCHAR(MAX) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT @cWert = cWert FROM NOVVIA.FirmaEinstellung WHERE cSchluessel = @cSchluessel;
END
GO

PRINT 'SP NOVVIA.spFirmaEinstellungLesen erstellt.';
GO

-- =====================================================
-- SP: NOVVIA.spIstPharmaModusAktiv
-- Prueft ob Pharma-Modus aktiv ist
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spIstPharmaModusAktiv')
    DROP PROCEDURE NOVVIA.spIstPharmaModusAktiv;
GO

CREATE PROCEDURE NOVVIA.spIstPharmaModusAktiv
    @nAktiv BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @cWert NVARCHAR(MAX);
    SELECT @cWert = cWert FROM NOVVIA.FirmaEinstellung WHERE cSchluessel = 'PHARMA';
    SET @nAktiv = CASE WHEN @cWert = '1' THEN 1 ELSE 0 END;
END
GO

PRINT 'SP NOVVIA.spIstPharmaModusAktiv erstellt.';
GO

-- =====================================================
-- SP: NOVVIA.spDarfValidierungBearbeiten
-- Prueft ob Benutzer Validierungsfelder bearbeiten darf
-- Wenn PHARMA=1, braucht der Benutzer die RP-Rolle
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spDarfValidierungBearbeiten')
    DROP PROCEDURE NOVVIA.spDarfValidierungBearbeiten;
GO

CREATE PROCEDURE NOVVIA.spDarfValidierungBearbeiten
    @kBenutzer INT,
    @cModul NVARCHAR(50),                             -- 'Kunden' oder 'Lieferanten'
    @nDarf BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @nPharmaAktiv BIT;
    DECLARE @cRechtSchluessel NVARCHAR(100);

    EXEC NOVVIA.spIstPharmaModusAktiv @nAktiv = @nPharmaAktiv OUTPUT;

    IF @nPharmaAktiv = 0
    BEGIN
        -- Kein Pharma-Modus: Jeder mit Bearbeiten-Recht darf
        SET @cRechtSchluessel = @cModul + '.Bearbeiten';
        EXEC NOVVIA.spHatRecht @kBenutzer = @kBenutzer,
                              @cRechtSchluessel = @cRechtSchluessel,
                              @nHatRecht = @nDarf OUTPUT;
    END
    ELSE
    BEGIN
        -- Pharma-Modus: Nur RP-Rolle darf Validierung bearbeiten
        SET @cRechtSchluessel = @cModul + '.ValidierungBearbeiten';
        EXEC NOVVIA.spHatRecht @kBenutzer = @kBenutzer,
                              @cRechtSchluessel = @cRechtSchluessel,
                              @nHatRecht = @nDarf OUTPUT;
    END
END
GO

PRINT 'SP NOVVIA.spDarfValidierungBearbeiten erstellt.';
GO

-- =====================================================
-- Zusammenfassung
-- =====================================================
PRINT '';
PRINT '=====================================================';
PRINT 'NOVVIA Benutzerrechte-System installiert.';
PRINT '';
PRINT 'Tabellen:';
PRINT '  NOVVIA.Modul            - Verfuegbare Module';
PRINT '  NOVVIA.Aktion           - Verfuegbare Aktionen';
PRINT '  NOVVIA.Recht            - Modul + Aktion Kombinationen';
PRINT '  NOVVIA.Rolle            - Benutzerrollen';
PRINT '  NOVVIA.RolleRecht       - Rechte pro Rolle';
PRINT '  NOVVIA.Benutzer         - Benutzerstammdaten';
PRINT '  NOVVIA.BenutzerRolle    - Benutzer-Rollen-Zuordnung';
PRINT '  NOVVIA.BenutzerRechtUeberschreibung - Individuelle Rechte';
PRINT '  NOVVIA.BenutzerSession  - Aktive Sitzungen';
PRINT '  NOVVIA.BenutzerLog      - Login-Historie';
PRINT '';
PRINT 'Standard-Rollen:';
PRINT '  Admin, Geschaeftsfuehrung, Verkauf, Lager, Einkauf,';
PRINT '  Buchhaltung, Kundenservice, Readonly, RP (Responsible Person)';
PRINT '';
PRINT 'Pharma-Modus:';
PRINT '  UPDATE NOVVIA.FirmaEinstellung SET cWert=''1'' WHERE cSchluessel=''PHARMA''';
PRINT '  Wenn PHARMA=1, duerfen nur Benutzer mit RP-Rolle';
PRINT '  Validierungsfelder bei Kunden/Lieferanten bearbeiten.';
PRINT '';
PRINT 'Abfrage: EXEC NOVVIA.spBenutzerRechteAbfragen @kBenutzer = 1';
PRINT 'Pruefung: EXEC NOVVIA.spHatRecht @kBenutzer = 1, @cRechtSchluessel = ''Kunden.Bearbeiten''';
PRINT 'Validierung: EXEC NOVVIA.spDarfValidierungBearbeiten @kBenutzer = 1, @cModul = ''Kunden''';
PRINT '=====================================================';
GO

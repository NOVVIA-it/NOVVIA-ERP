-- =====================================================
-- NOVVIA ERP - VOLLSTÄNDIGE INSTALLATION
-- =====================================================
-- Dieses Script richtet alle NOVVIA-Tabellen, Views und
-- Stored Procedures ein.
--
-- VERWENDUNG:
--   1. Mandant-Datenbank auswählen (USE Mandant_X)
--   2. Script ausführen
--   3. Für jeden Mandanten wiederholen
--
-- REIHENFOLGE:
--   1. Schema erstellen
--   2. Basis-Tabellen (Auth, Worker, Import)
--   3. MSV3-Tabellen (Pharma-Großhandel)
--   4. Einkauf-Tabellen
--   5. Artikel-Erweiterungen
--   6. Views
--   7. Stored Procedures
--
-- VERSION: 2.0
-- DATUM: 2024-12-28
-- =====================================================

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;

PRINT '=====================================================';
PRINT 'NOVVIA ERP - Installation gestartet';
PRINT 'Datenbank: ' + DB_NAME();
PRINT 'Zeitpunkt: ' + CONVERT(VARCHAR, GETDATE(), 120);
PRINT '=====================================================';
PRINT '';

-- =====================================================
-- 1. SCHEMA ERSTELLEN
-- =====================================================
PRINT '[1/7] Schema NOVVIA erstellen...';

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'NOVVIA')
BEGIN
    EXEC('CREATE SCHEMA NOVVIA');
    PRINT '      Schema NOVVIA erstellt';
END
ELSE
    PRINT '      Schema NOVVIA existiert bereits';
GO

-- =====================================================
-- 2. BERECHTIGUNGSSYSTEM
-- =====================================================
PRINT '[2/7] Berechtigungssystem einrichten...';

-- Rollen
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='tRolle' AND xtype='U')
CREATE TABLE tRolle (
    kRolle INT IDENTITY(1,1) PRIMARY KEY,
    cName NVARCHAR(100) NOT NULL,
    cBeschreibung NVARCHAR(500),
    nIstAdmin BIT DEFAULT 0
);

-- Berechtigungen
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='tBerechtigung' AND xtype='U')
CREATE TABLE tBerechtigung (
    kBerechtigung INT IDENTITY(1,1) PRIMARY KEY,
    cModul NVARCHAR(50) NOT NULL,
    cAktion NVARCHAR(50) NOT NULL,
    cBeschreibung NVARCHAR(200)
);

-- Rolle-Berechtigung Zuordnung
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='tRolleBerechtigung' AND xtype='U')
CREATE TABLE tRolleBerechtigung (
    kRolleBerechtigung INT IDENTITY(1,1) PRIMARY KEY,
    kRolle INT NOT NULL,
    kBerechtigung INT NOT NULL
);

-- Benutzer erweitern
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tBenutzer') AND name = 'cSalt')
BEGIN
    ALTER TABLE tBenutzer ADD cSalt NVARCHAR(100);
    ALTER TABLE tBenutzer ADD kRolle INT;
    ALTER TABLE tBenutzer ADD nFehlversuche INT DEFAULT 0;
    ALTER TABLE tBenutzer ADD dGesperrtBis DATETIME;
END

-- Benutzer-Log
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='tBenutzerLog' AND xtype='U')
CREATE TABLE tBenutzerLog (
    kBenutzerLog INT IDENTITY(1,1) PRIMARY KEY,
    kBenutzer INT NOT NULL,
    cAktion NVARCHAR(50) NOT NULL,
    cModul NVARCHAR(50),
    cDetails NVARCHAR(500),
    cIP NVARCHAR(50),
    dZeitpunkt DATETIME DEFAULT GETDATE()
);

-- Standard-Rollen
IF NOT EXISTS (SELECT * FROM tRolle WHERE cName = 'Administrator')
INSERT INTO tRolle (cName, cBeschreibung, nIstAdmin) VALUES
    ('Administrator', 'Vollzugriff auf alle Funktionen', 1),
    ('Verkauf', 'Bestellungen, Kunden, Rechnungen', 0),
    ('Lager', 'Lager, Versand, Wareneingang', 0),
    ('Einkauf', 'Einkauf, Lieferanten', 0),
    ('Buchhaltung', 'Rechnungen, Mahnungen, DATEV', 0),
    ('Nur Lesen', 'Nur Lesezugriff', 0);

PRINT '      Berechtigungssystem OK';
GO

-- =====================================================
-- 3. NOVVIA BASIS-TABELLEN
-- =====================================================
PRINT '[3/7] Basis-Tabellen erstellen...';

-- Import-Vorlagen
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'NOVVIA.tImportVorlage'))
CREATE TABLE NOVVIA.tImportVorlage (
    kImportVorlage INT IDENTITY(1,1) PRIMARY KEY,
    cName NVARCHAR(100) NOT NULL,
    cTyp NVARCHAR(50) NOT NULL DEFAULT 'Auftrag',
    cTrennzeichen CHAR(1) DEFAULT ';',
    nKopfzeile BIT DEFAULT 1,
    cFeldzuordnungJson NVARCHAR(MAX),
    cStandardwerte NVARCHAR(MAX),
    dErstellt DATETIME DEFAULT GETDATE()
);

-- Import-Log
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'NOVVIA.tImportLog'))
CREATE TABLE NOVVIA.tImportLog (
    kImportLog INT IDENTITY(1,1) PRIMARY KEY,
    cDatei NVARCHAR(500),
    cTyp NVARCHAR(50),
    nZeilen INT,
    nErfolg INT,
    nFehler INT,
    cFehlerDetails NVARCHAR(MAX),
    dImport DATETIME DEFAULT GETDATE(),
    kBenutzer INT
);

-- Worker-Status
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'NOVVIA.tWorkerStatus'))
CREATE TABLE NOVVIA.tWorkerStatus (
    kWorkerStatus INT IDENTITY(1,1) PRIMARY KEY,
    cWorker NVARCHAR(100) NOT NULL UNIQUE,
    nStatus INT DEFAULT 0,
    dLetzterLauf DATETIME,
    nLaufzeit_ms INT,
    cLetzteFehler NVARCHAR(MAX),
    nAktiv BIT DEFAULT 1,
    cKonfigJson NVARCHAR(MAX)
);

-- Worker-Log
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'NOVVIA.tWorkerLog'))
CREATE TABLE NOVVIA.tWorkerLog (
    kWorkerLog INT IDENTITY(1,1) PRIMARY KEY,
    cWorker NVARCHAR(100) NOT NULL,
    cLevel NVARCHAR(20) NOT NULL DEFAULT 'INFO',
    cNachricht NVARCHAR(MAX),
    dZeitpunkt DATETIME DEFAULT GETDATE()
);

-- Ausgabe-Log
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'NOVVIA.tAusgabeLog'))
CREATE TABLE NOVVIA.tAusgabeLog (
    kAusgabeLog INT IDENTITY(1,1) PRIMARY KEY,
    cDokumentTyp NVARCHAR(50) NOT NULL,
    kDokument INT NOT NULL,
    cDokumentNr NVARCHAR(50),
    cAktionen NVARCHAR(200),
    nGedruckt BIT DEFAULT 0,
    nGespeichert BIT DEFAULT 0,
    nMailGesendet BIT DEFAULT 0,
    nVorschau BIT DEFAULT 0,
    cFehler NVARCHAR(MAX),
    dZeitpunkt DATETIME DEFAULT GETDATE(),
    kBenutzer INT
);

-- Dokument-Archiv
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'NOVVIA.tDokumentArchiv'))
CREATE TABLE NOVVIA.tDokumentArchiv (
    kDokumentArchiv INT IDENTITY(1,1) PRIMARY KEY,
    cDokumentTyp NVARCHAR(50) NOT NULL,
    kDokument INT NOT NULL,
    cDokumentNr NVARCHAR(50),
    cPfad NVARCHAR(500),
    bPdfDaten VARBINARY(MAX),
    nGroesse INT,
    dArchiviert DATETIME DEFAULT GETDATE()
);

PRINT '      Basis-Tabellen OK';
GO

-- =====================================================
-- 4. MSV3 TABELLEN (Pharma-Großhandel)
-- =====================================================
PRINT '[4/7] MSV3-Tabellen erstellen...';

-- MSV3 Lieferanten-Konfiguration
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MSV3Lieferant' AND schema_id = SCHEMA_ID('NOVVIA'))
CREATE TABLE NOVVIA.MSV3Lieferant (
    kMSV3Lieferant      INT IDENTITY(1,1) PRIMARY KEY,
    kLieferant          INT NOT NULL,
    cMSV3Url            NVARCHAR(500) NOT NULL,
    cMSV3Benutzer       NVARCHAR(100) NOT NULL,
    cMSV3Passwort       NVARCHAR(255) NOT NULL,
    cMSV3Kundennummer   NVARCHAR(50) NULL,
    cMSV3Filiale        NVARCHAR(20) NULL,
    nMSV3Version        INT DEFAULT 1,
    nAktiv              TINYINT DEFAULT 1,
    nPrioritaet         INT DEFAULT 1,
    dErstellt           DATETIME DEFAULT GETDATE(),
    dGeaendert          DATETIME NULL
);

-- MSV3 Bestellungen
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MSV3Bestellung' AND schema_id = SCHEMA_ID('NOVVIA'))
CREATE TABLE NOVVIA.MSV3Bestellung (
    kMSV3Bestellung         INT IDENTITY(1,1) PRIMARY KEY,
    kLieferantenBestellung  INT NOT NULL,
    kMSV3Lieferant          INT NOT NULL,
    cMSV3AuftragsId         NVARCHAR(100) NULL,
    cMSV3Status             NVARCHAR(50) NULL,
    nAnzahlPositionen       INT NULL,
    nAnzahlVerfuegbar       INT NULL,
    nAnzahlNichtVerfuegbar  INT NULL,
    dGesendet               DATETIME NULL,
    dBestaetigt             DATETIME NULL,
    cResponseXML            NVARCHAR(MAX) NULL,
    cFehler                 NVARCHAR(500) NULL,
    dErstellt               DATETIME DEFAULT GETDATE()
);

-- MSV3 Bestellpositionen
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MSV3BestellungPos' AND schema_id = SCHEMA_ID('NOVVIA'))
CREATE TABLE NOVVIA.MSV3BestellungPos (
    kMSV3BestellungPos      INT IDENTITY(1,1) PRIMARY KEY,
    kMSV3Bestellung         INT NOT NULL,
    cPZN                    NVARCHAR(20) NOT NULL,
    nMengeAngefragt         INT NOT NULL,
    nMengeBestaetigt        INT NULL,
    cVerfuegbarkeit         NVARCHAR(50) NULL,
    fPreis                  DECIMAL(10,2) NULL,
    dMHD                    DATE NULL,
    cCharge                 NVARCHAR(50) NULL,
    cHinweis                NVARCHAR(500) NULL
);

-- MSV3 Verfügbarkeits-Cache
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MSV3VerfuegbarkeitCache' AND schema_id = SCHEMA_ID('NOVVIA'))
CREATE TABLE NOVVIA.MSV3VerfuegbarkeitCache (
    kMSV3VerfuegbarkeitCache INT IDENTITY(1,1) PRIMARY KEY,
    kMSV3Lieferant          INT NOT NULL,
    cPZN                    NVARCHAR(20) NOT NULL,
    nVerfuegbar             INT NOT NULL,
    fPreis                  DECIMAL(10,2) NULL,
    cStatus                 NVARCHAR(50) NULL,
    dAbfrage                DATETIME NOT NULL DEFAULT GETDATE(),
    dGueltigBis             DATETIME NOT NULL
);

-- MSV3 Log
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MSV3Log' AND schema_id = SCHEMA_ID('NOVVIA'))
CREATE TABLE NOVVIA.MSV3Log (
    kMSV3Log            INT IDENTITY(1,1) PRIMARY KEY,
    kMSV3Lieferant      INT NULL,
    cAktion             NVARCHAR(50) NOT NULL,
    cRequest            NVARCHAR(MAX) NULL,
    cResponse           NVARCHAR(MAX) NULL,
    nHttpStatus         INT NULL,
    nDauer_ms           INT NULL,
    cFehler             NVARCHAR(MAX) NULL,
    dZeitpunkt          DATETIME DEFAULT GETDATE()
);

-- MSV3 Bestand Cache (TTL-basiert, 5 Minuten)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MSV3BestandCache' AND schema_id = SCHEMA_ID('NOVVIA'))
CREATE TABLE NOVVIA.MSV3BestandCache (
    kMSV3BestandCache  INT IDENTITY(1,1) NOT NULL,
    cPzn               NVARCHAR(50)  NOT NULL,
    kLieferant         INT           NOT NULL,
    nBestand           INT           NULL,
    nVerfuegbar        INT           NULL,
    cStatus            NVARCHAR(100) NULL,
    dAbfrage           DATETIME2(0)  NOT NULL DEFAULT GETDATE(),
    CONSTRAINT PK_MSV3BestandCache PRIMARY KEY CLUSTERED (kMSV3BestandCache),
    CONSTRAINT UQ_MSV3BestandCache_PznLief UNIQUE (cPzn, kLieferant)
);

-- ABdata Artikel (Pharma-Stammdaten)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ABdataArtikel' AND schema_id = SCHEMA_ID('NOVVIA'))
CREATE TABLE NOVVIA.ABdataArtikel (
    kABdataArtikel     INT IDENTITY(1,1) PRIMARY KEY,
    cPZN               NVARCHAR(20) NOT NULL UNIQUE,
    cName              NVARCHAR(500) NOT NULL,
    cHersteller        NVARCHAR(255) NULL,
    cDarreichungsform  NVARCHAR(100) NULL,
    cPackungsgroesse   NVARCHAR(50) NULL,
    fMenge             DECIMAL(18,4) NULL,
    cEinheit           NVARCHAR(50) NULL,
    fAEP               DECIMAL(18,4) NULL,
    fAVP               DECIMAL(18,4) NULL,
    fAEK               DECIMAL(18,4) NULL,
    nRezeptpflicht     TINYINT DEFAULT 0,
    nBTM               TINYINT DEFAULT 0,
    nKuehlpflichtig    TINYINT DEFAULT 0,
    cATC               NVARCHAR(20) NULL,
    cWirkstoff         NVARCHAR(500) NULL,
    dGueltigAb         DATE NULL,
    dGueltigBis        DATE NULL,
    dErstellt          DATETIME DEFAULT GETDATE(),
    dGeaendert         DATETIME NULL
);

-- ABdata Artikel Mapping (PZN zu JTL-Artikel)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ABdataArtikelMapping' AND schema_id = SCHEMA_ID('NOVVIA'))
CREATE TABLE NOVVIA.ABdataArtikelMapping (
    kABdataArtikelMapping INT IDENTITY(1,1) PRIMARY KEY,
    kArtikel           INT NOT NULL,
    cPZN               NVARCHAR(20) NOT NULL,
    nAutomatisch       BIT DEFAULT 0,
    dErstellt          DATETIME DEFAULT GETDATE()
);

PRINT '      MSV3-Tabellen OK';
GO

-- =====================================================
-- 5. EINKAUF-TABELLEN
-- =====================================================
PRINT '[5/7] Einkauf-Tabellen erstellen...';

-- Einkaufsliste (temporäre Bedarfe)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tEinkaufsliste' AND schema_id = SCHEMA_ID('NOVVIA'))
CREATE TABLE NOVVIA.tEinkaufsliste (
    kEinkaufsliste      INT IDENTITY(1,1) PRIMARY KEY,
    kArtikel            INT NOT NULL,
    kLieferant          INT NULL,
    fBedarf             DECIMAL(25,13) NOT NULL,
    fBestand            DECIMAL(25,13) NULL,
    fBestellt           DECIMAL(25,13) NULL DEFAULT 0,
    cStatus             NVARCHAR(50) DEFAULT 'Offen',
    dErstellt           DATETIME DEFAULT GETDATE(),
    kBenutzer           INT NULL
);

-- Lieferant erweiterte Felder
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tLieferantErweitert' AND schema_id = SCHEMA_ID('NOVVIA'))
CREATE TABLE NOVVIA.tLieferantErweitert (
    kLieferantErweitert INT IDENTITY(1,1) PRIMARY KEY,
    kLieferant          INT NOT NULL UNIQUE,
    cGLN                NVARCHAR(20) NULL,
    cILN                NVARCHAR(20) NULL,
    cEDIKennung         NVARCHAR(50) NULL,
    nIstPharmaGH        BIT DEFAULT 0,
    cAnsprechpartner    NVARCHAR(100) NULL,
    cAnsprechpartnerMail NVARCHAR(200) NULL,
    cAnsprechpartnerTel NVARCHAR(50) NULL,
    cNotizen            NVARCHAR(MAX) NULL,
    dGeaendert          DATETIME NULL
);

PRINT '      Einkauf-Tabellen OK';
GO

-- =====================================================
-- 6. VIEWS
-- =====================================================
PRINT '[6/7] Views erstellen...';

-- View: Lieferanten-Übersicht mit MSV3
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vLieferantenUebersicht')
    DROP VIEW NOVVIA.vLieferantenUebersicht;
GO

CREATE VIEW NOVVIA.vLieferantenUebersicht AS
SELECT
    l.kLieferant,
    l.cName AS LieferantName,
    l.cFirma,
    l.cStrasse,
    l.cPLZ,
    l.cOrt,
    l.cTel,
    l.cFax,
    l.cMail,
    l.cAnsprechpartner,
    l.cKundennummer,
    l.cWaehrung,
    l.cUSTID,
    l.nAktiv,
    m.kMSV3Lieferant,
    m.cMSV3Url,
    m.cMSV3Benutzer,
    m.cMSV3Kundennummer,
    m.cMSV3Filiale,
    m.nMSV3Version,
    m.nAktiv AS MSV3Aktiv,
    m.nPrioritaet AS MSV3Prioritaet,
    e.nIstPharmaGH,
    e.cGLN,
    e.cILN
FROM dbo.tLieferant l
LEFT JOIN NOVVIA.MSV3Lieferant m ON m.kLieferant = l.kLieferant
LEFT JOIN NOVVIA.tLieferantErweitert e ON e.kLieferant = l.kLieferant;
GO

-- View: MSV3 Bestellungen
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vMSV3Bestellungen')
    DROP VIEW NOVVIA.vMSV3Bestellungen;
GO

CREATE VIEW NOVVIA.vMSV3Bestellungen AS
SELECT
    b.kMSV3Bestellung,
    b.kLieferantenBestellung,
    b.cMSV3AuftragsId,
    b.cMSV3Status,
    b.nAnzahlPositionen,
    b.nAnzahlVerfuegbar,
    b.nAnzahlNichtVerfuegbar,
    b.dGesendet,
    b.dBestaetigt,
    b.cFehler,
    b.dErstellt,
    m.kLieferant,
    l.cName AS LieferantName
FROM NOVVIA.MSV3Bestellung b
JOIN NOVVIA.MSV3Lieferant m ON m.kMSV3Lieferant = b.kMSV3Lieferant
JOIN dbo.tLieferant l ON l.kLieferant = m.kLieferant;
GO

PRINT '      Views OK';
GO

-- =====================================================
-- 7. STORED PROCEDURES
-- =====================================================
PRINT '[7/7] Stored Procedures erstellen...';

-- SP: MSV3 Lieferant speichern
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spMSV3LieferantSpeichern' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spMSV3LieferantSpeichern;
GO

CREATE PROCEDURE NOVVIA.spMSV3LieferantSpeichern
    @kLieferant INT,
    @cMSV3Url NVARCHAR(500),
    @cMSV3Benutzer NVARCHAR(100),
    @cMSV3Passwort NVARCHAR(255),
    @cMSV3Kundennummer NVARCHAR(50) = NULL,
    @cMSV3Filiale NVARCHAR(20) = NULL,
    @nMSV3Version INT = 2,
    @nAktiv TINYINT = 1,
    @nPrioritaet INT = 1
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM NOVVIA.MSV3Lieferant WHERE kLieferant = @kLieferant)
    BEGIN
        UPDATE NOVVIA.MSV3Lieferant SET
            cMSV3Url = @cMSV3Url,
            cMSV3Benutzer = @cMSV3Benutzer,
            cMSV3Passwort = @cMSV3Passwort,
            cMSV3Kundennummer = @cMSV3Kundennummer,
            cMSV3Filiale = @cMSV3Filiale,
            nMSV3Version = @nMSV3Version,
            nAktiv = @nAktiv,
            nPrioritaet = @nPrioritaet,
            dGeaendert = GETDATE()
        WHERE kLieferant = @kLieferant;
    END
    ELSE
    BEGIN
        INSERT INTO NOVVIA.MSV3Lieferant (
            kLieferant, cMSV3Url, cMSV3Benutzer, cMSV3Passwort,
            cMSV3Kundennummer, cMSV3Filiale, nMSV3Version, nAktiv, nPrioritaet
        ) VALUES (
            @kLieferant, @cMSV3Url, @cMSV3Benutzer, @cMSV3Passwort,
            @cMSV3Kundennummer, @cMSV3Filiale, @nMSV3Version, @nAktiv, @nPrioritaet
        );
    END
END;
GO

-- SP: Artikel Eigene Felder lesen/schreiben (JTL-konform)
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spArtikelEigenesFeldSetzen' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spArtikelEigenesFeldSetzen;
GO

CREATE PROCEDURE NOVVIA.spArtikelEigenesFeldSetzen
    @kArtikel INT,
    @cFeldName NVARCHAR(100),
    @cWert NVARCHAR(MAX) = NULL,
    @nWert INT = NULL,
    @fWert DECIMAL(25,13) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @kArtikelAttribut INT;

    -- Attribut-ID ermitteln
    SELECT @kArtikelAttribut = kArtikelAttribut
    FROM dbo.tArtikelAttribut
    WHERE cName = @cFeldName;

    IF @kArtikelAttribut IS NULL
    BEGIN
        RAISERROR('Attribut nicht gefunden: %s', 16, 1, @cFeldName);
        RETURN;
    END

    -- Wert aktualisieren oder einfügen (für kSprache 0 und 1)
    MERGE dbo.tArtikelAttributSprache AS target
    USING (SELECT @kArtikel AS kArtikel, @kArtikelAttribut AS kArtikelAttribut, 0 AS kSprache
           UNION ALL
           SELECT @kArtikel, @kArtikelAttribut, 1) AS source
    ON target.kArtikel = source.kArtikel
       AND target.kArtikelAttribut = source.kArtikelAttribut
       AND target.kSprache = source.kSprache
    WHEN MATCHED THEN
        UPDATE SET
            cWertVarchar = @cWert,
            nWertInt = @nWert,
            fWertDecimal = @fWert
    WHEN NOT MATCHED THEN
        INSERT (kArtikel, kArtikelAttribut, kSprache, cWertVarchar, nWertInt, fWertDecimal)
        VALUES (source.kArtikel, source.kArtikelAttribut, source.kSprache, @cWert, @nWert, @fWert);
END;
GO

-- SP: Worker-Log schreiben
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spWorkerLogSchreiben' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spWorkerLogSchreiben;
GO

CREATE PROCEDURE NOVVIA.spWorkerLogSchreiben
    @cWorker NVARCHAR(100),
    @cLevel NVARCHAR(20) = 'INFO',
    @cNachricht NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO NOVVIA.tWorkerLog (cWorker, cLevel, cNachricht)
    VALUES (@cWorker, @cLevel, @cNachricht);
END;
GO

-- SP: Worker-Status aktualisieren
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spWorkerStatusAktualisieren' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spWorkerStatusAktualisieren;
GO

CREATE PROCEDURE NOVVIA.spWorkerStatusAktualisieren
    @cWorker NVARCHAR(100),
    @nStatus INT,
    @nLaufzeit_ms INT = NULL,
    @cFehler NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM NOVVIA.tWorkerStatus WHERE cWorker = @cWorker)
    BEGIN
        UPDATE NOVVIA.tWorkerStatus SET
            nStatus = @nStatus,
            dLetzterLauf = CASE WHEN @nStatus = 0 THEN GETDATE() ELSE dLetzterLauf END,
            nLaufzeit_ms = ISNULL(@nLaufzeit_ms, nLaufzeit_ms),
            cLetzteFehler = @cFehler
        WHERE cWorker = @cWorker;
    END
    ELSE
    BEGIN
        INSERT INTO NOVVIA.tWorkerStatus (cWorker, nStatus, dLetzterLauf, nLaufzeit_ms, cLetzteFehler)
        VALUES (@cWorker, @nStatus, GETDATE(), @nLaufzeit_ms, @cFehler);
    END
END;
GO

-- SP: MSV3 Bestand Cache Get (mit TTL)
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spMSV3BestandCache_Get' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spMSV3BestandCache_Get;
GO

CREATE PROCEDURE NOVVIA.spMSV3BestandCache_Get
(
    @cPzn NVARCHAR(50),
    @kLieferant INT,
    @nTtlMinuten INT = 5
)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT nBestand, nVerfuegbar, cStatus, dAbfrage
    FROM NOVVIA.MSV3BestandCache
    WHERE cPzn = @cPzn
      AND kLieferant = @kLieferant
      AND dAbfrage > DATEADD(MINUTE, -@nTtlMinuten, GETDATE());
END;
GO

-- SP: MSV3 Bestand Cache Upsert
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spMSV3BestandCache_Upsert' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spMSV3BestandCache_Upsert;
GO

CREATE PROCEDURE NOVVIA.spMSV3BestandCache_Upsert
(
    @cPzn NVARCHAR(50),
    @kLieferant INT,
    @nBestand INT = NULL,
    @nVerfuegbar INT = NULL,
    @cStatus NVARCHAR(100) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM NOVVIA.MSV3BestandCache WHERE cPzn = @cPzn AND kLieferant = @kLieferant)
    BEGIN
        UPDATE NOVVIA.MSV3BestandCache
        SET nBestand = @nBestand, nVerfuegbar = @nVerfuegbar, cStatus = @cStatus, dAbfrage = GETDATE()
        WHERE cPzn = @cPzn AND kLieferant = @kLieferant;
    END
    ELSE
    BEGIN
        INSERT INTO NOVVIA.MSV3BestandCache (cPzn, kLieferant, nBestand, nVerfuegbar, cStatus, dAbfrage)
        VALUES (@cPzn, @kLieferant, @nBestand, @nVerfuegbar, @cStatus, GETDATE());
    END
END;
GO

-- SP: MSV3 Bestand Cache Cleanup
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spMSV3BestandCache_Cleanup' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spMSV3BestandCache_Cleanup;
GO

CREATE PROCEDURE NOVVIA.spMSV3BestandCache_Cleanup
(
    @nMaxAlterStunden INT = 24
)
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM NOVVIA.MSV3BestandCache
    WHERE dAbfrage < DATEADD(HOUR, -@nMaxAlterStunden, GETDATE());
    SELECT @@ROWCOUNT AS GeloeschteEintraege;
END;
GO

-- TVP Type: Artikel Eigene Felder
IF EXISTS (SELECT 1 FROM sys.types WHERE name = 'TYPE_ArtikelEigenesFeldAnpassen' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    IF EXISTS (SELECT 1 FROM sys.procedures WHERE name = 'spArtikelEigenesFeldCreateOrUpdate' AND schema_id = SCHEMA_ID('NOVVIA'))
        DROP PROCEDURE NOVVIA.spArtikelEigenesFeldCreateOrUpdate;
    DROP TYPE NOVVIA.TYPE_ArtikelEigenesFeldAnpassen;
END
GO

CREATE TYPE NOVVIA.TYPE_ArtikelEigenesFeldAnpassen AS TABLE
(
    kArtikel INT           NOT NULL,
    cKey     NVARCHAR(255) NOT NULL,
    cValue   NVARCHAR(MAX) NULL
);
GO

-- SP: Artikel Eigene Felder Create/Update (JTL-Native Tabellen)
CREATE PROCEDURE NOVVIA.spArtikelEigenesFeldCreateOrUpdate
(
    @ArtikelEigenesFeldAnpassen NOVVIA.TYPE_ArtikelEigenesFeldAnpassen READONLY,
    @nAutoCreateAttribute BIT = 0
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM @ArtikelEigenesFeldAnpassen)
        RETURN;

    BEGIN TRY
        BEGIN TRAN;

        IF OBJECT_ID('tempdb..#Input') IS NOT NULL DROP TABLE #Input;
        CREATE TABLE #Input (kArtikel INT NOT NULL, cKey NVARCHAR(255) NOT NULL, cValue NVARCHAR(MAX) NULL, kAttribut INT NULL);

        INSERT INTO #Input (kArtikel, cKey, cValue)
        SELECT kArtikel, LTRIM(RTRIM(cKey)), cValue FROM @ArtikelEigenesFeldAnpassen;

        UPDATE i SET i.kAttribut = s.kAttribut
        FROM #Input i
        INNER JOIN dbo.tAttributSprache s ON s.cName = i.cKey AND s.kSprache = 0;

        DELETE FROM #Input WHERE kAttribut IS NULL;

        IF NOT EXISTS (SELECT 1 FROM #Input)
        BEGIN
            COMMIT TRAN;
            RETURN;
        END

        INSERT INTO dbo.tArtikelAttribut (kArtikel, kAttribut)
        SELECT DISTINCT i.kArtikel, i.kAttribut FROM #Input i
        WHERE NOT EXISTS (SELECT 1 FROM dbo.tArtikelAttribut aa WHERE aa.kArtikel = i.kArtikel AND aa.kAttribut = i.kAttribut);

        IF OBJECT_ID('tempdb..#Resolved') IS NOT NULL DROP TABLE #Resolved;
        CREATE TABLE #Resolved (kArtikelAttribut INT NOT NULL, cValue NVARCHAR(MAX) NULL);

        INSERT INTO #Resolved (kArtikelAttribut, cValue)
        SELECT aa.kArtikelAttribut, i.cValue
        FROM #Input i
        INNER JOIN dbo.tArtikelAttribut aa ON aa.kArtikel = i.kArtikel AND aa.kAttribut = i.kAttribut;

        UPDATE aas
        SET aas.nWertInt = TRY_CAST(r.cValue AS INT),
            aas.cWertVarchar = CASE WHEN TRY_CAST(r.cValue AS INT) IS NULL THEN r.cValue ELSE NULL END
        FROM dbo.tArtikelAttributSprache aas
        INNER JOIN #Resolved r ON r.kArtikelAttribut = aas.kArtikelAttribut
        WHERE aas.kSprache = 0;

        INSERT INTO dbo.tArtikelAttributSprache (kArtikelAttribut, kSprache, nWertInt, cWertVarchar)
        SELECT r.kArtikelAttribut, 0,
               TRY_CAST(r.cValue AS INT),
               CASE WHEN TRY_CAST(r.cValue AS INT) IS NULL THEN r.cValue ELSE NULL END
        FROM #Resolved r
        WHERE NOT EXISTS (SELECT 1 FROM dbo.tArtikelAttributSprache aas WHERE aas.kArtikelAttribut = r.kArtikelAttribut AND aas.kSprache = 0);

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRAN;
        DECLARE @ErrorMessage NVARCHAR(MAX) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();
        RAISERROR (@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END;
GO

-- TVP Type: Auftrag Eigene Felder
IF EXISTS (SELECT 1 FROM sys.types WHERE name = 'TYPE_AuftragEigenesFeldAnpassen' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    IF EXISTS (SELECT 1 FROM sys.procedures WHERE name = 'spAuftragEigenesFeldCreateOrUpdate' AND schema_id = SCHEMA_ID('NOVVIA'))
        DROP PROCEDURE NOVVIA.spAuftragEigenesFeldCreateOrUpdate;
    DROP TYPE NOVVIA.TYPE_AuftragEigenesFeldAnpassen;
END
GO

CREATE TYPE NOVVIA.TYPE_AuftragEigenesFeldAnpassen AS TABLE
(
    kAuftrag INT           NOT NULL,
    cKey     NVARCHAR(255) NOT NULL,
    cValue   NVARCHAR(MAX) NULL
);
GO

-- SP: Auftrag Eigene Felder Create/Update (JTL-Native Tabellen)
CREATE PROCEDURE NOVVIA.spAuftragEigenesFeldCreateOrUpdate
(
    @AuftragEigenesFeldAnpassen NOVVIA.TYPE_AuftragEigenesFeldAnpassen READONLY
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM @AuftragEigenesFeldAnpassen)
        RETURN;

    BEGIN TRY
        BEGIN TRAN;

        IF OBJECT_ID('tempdb..#Input') IS NOT NULL DROP TABLE #Input;
        CREATE TABLE #Input (kAuftrag INT NOT NULL, cKey NVARCHAR(255) NOT NULL, cValue NVARCHAR(MAX) NULL, kAttribut INT NULL);

        INSERT INTO #Input (kAuftrag, cKey, cValue)
        SELECT kAuftrag, LTRIM(RTRIM(cKey)), cValue FROM @AuftragEigenesFeldAnpassen;

        UPDATE i SET i.kAttribut = s.kAttribut
        FROM #Input i
        INNER JOIN dbo.tAttributSprache s ON s.cName = i.cKey AND s.kSprache = 0;

        DELETE FROM #Input WHERE kAttribut IS NULL;

        IF NOT EXISTS (SELECT 1 FROM #Input)
        BEGIN
            COMMIT TRAN;
            RETURN;
        END

        -- AuftragAttribut-Verknuepfungen erstellen falls nicht vorhanden
        INSERT INTO Verkauf.tAuftragAttribut (kAuftrag, kAttribut)
        SELECT DISTINCT i.kAuftrag, i.kAttribut FROM #Input i
        WHERE NOT EXISTS (SELECT 1 FROM Verkauf.tAuftragAttribut aa WHERE aa.kAuftrag = i.kAuftrag AND aa.kAttribut = i.kAttribut);

        IF OBJECT_ID('tempdb..#Resolved') IS NOT NULL DROP TABLE #Resolved;
        CREATE TABLE #Resolved (kAuftragAttribut INT NOT NULL, cValue NVARCHAR(MAX) NULL);

        INSERT INTO #Resolved (kAuftragAttribut, cValue)
        SELECT aa.kAuftragAttribut, i.cValue
        FROM #Input i
        INNER JOIN Verkauf.tAuftragAttribut aa ON aa.kAuftrag = i.kAuftrag AND aa.kAttribut = i.kAttribut;

        -- Werte in tAuftragAttributSprache upserten (kSprache = 0)
        UPDATE aas
        SET aas.nWertInt = CASE WHEN TRY_CAST(r.cValue AS DECIMAL(18,4)) IS NOT NULL AND CHARINDEX('.', r.cValue) = 0 AND CHARINDEX(',', r.cValue) = 0 THEN TRY_CAST(r.cValue AS INT) ELSE NULL END,
            aas.fWertDecimal = CASE WHEN TRY_CAST(REPLACE(r.cValue, ',', '.') AS DECIMAL(18,4)) IS NOT NULL AND (CHARINDEX('.', r.cValue) > 0 OR CHARINDEX(',', r.cValue) > 0) THEN TRY_CAST(REPLACE(r.cValue, ',', '.') AS DECIMAL(18,4)) ELSE NULL END,
            aas.cWertVarchar = CASE WHEN TRY_CAST(r.cValue AS DECIMAL(18,4)) IS NULL AND TRY_CAST(REPLACE(r.cValue, ',', '.') AS DECIMAL(18,4)) IS NULL THEN r.cValue ELSE NULL END
        FROM Verkauf.tAuftragAttributSprache aas
        INNER JOIN #Resolved r ON r.kAuftragAttribut = aas.kAuftragAttribut
        WHERE aas.kSprache = 0;

        INSERT INTO Verkauf.tAuftragAttributSprache (kAuftragAttribut, kSprache, nWertInt, fWertDecimal, cWertVarchar)
        SELECT r.kAuftragAttribut, 0,
               CASE WHEN TRY_CAST(r.cValue AS DECIMAL(18,4)) IS NOT NULL AND CHARINDEX('.', r.cValue) = 0 AND CHARINDEX(',', r.cValue) = 0 THEN TRY_CAST(r.cValue AS INT) ELSE NULL END,
               CASE WHEN TRY_CAST(REPLACE(r.cValue, ',', '.') AS DECIMAL(18,4)) IS NOT NULL AND (CHARINDEX('.', r.cValue) > 0 OR CHARINDEX(',', r.cValue) > 0) THEN TRY_CAST(REPLACE(r.cValue, ',', '.') AS DECIMAL(18,4)) ELSE NULL END,
               CASE WHEN TRY_CAST(r.cValue AS DECIMAL(18,4)) IS NULL AND TRY_CAST(REPLACE(r.cValue, ',', '.') AS DECIMAL(18,4)) IS NULL THEN r.cValue ELSE NULL END
        FROM #Resolved r
        WHERE NOT EXISTS (SELECT 1 FROM Verkauf.tAuftragAttributSprache aas WHERE aas.kAuftragAttribut = r.kAuftragAttribut AND aas.kSprache = 0);

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRAN;
        DECLARE @ErrorMessage NVARCHAR(MAX) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();
        RAISERROR (@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END;
GO

-- SP: ABdata Artikel Upsert
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spABdataArtikelUpsert' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spABdataArtikelUpsert;
GO

CREATE PROCEDURE NOVVIA.spABdataArtikelUpsert
    @cPZN NVARCHAR(20),
    @cName NVARCHAR(500),
    @cHersteller NVARCHAR(255) = NULL,
    @cDarreichungsform NVARCHAR(100) = NULL,
    @cPackungsgroesse NVARCHAR(50) = NULL,
    @fMenge DECIMAL(18,4) = NULL,
    @cEinheit NVARCHAR(50) = NULL,
    @fAEP DECIMAL(18,4) = NULL,
    @fAVP DECIMAL(18,4) = NULL,
    @fAEK DECIMAL(18,4) = NULL,
    @nRezeptpflicht TINYINT = 0,
    @nBTM TINYINT = 0,
    @nKuehlpflichtig TINYINT = 0,
    @cATC NVARCHAR(20) = NULL,
    @cWirkstoff NVARCHAR(500) = NULL,
    @dGueltigAb DATE = NULL,
    @dGueltigBis DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS (SELECT 1 FROM NOVVIA.ABdataArtikel WHERE cPZN = @cPZN)
    BEGIN
        UPDATE NOVVIA.ABdataArtikel SET
            cName = @cName, cHersteller = @cHersteller, cDarreichungsform = @cDarreichungsform,
            cPackungsgroesse = @cPackungsgroesse, fMenge = @fMenge, cEinheit = @cEinheit,
            fAEP = @fAEP, fAVP = @fAVP, fAEK = @fAEK, nRezeptpflicht = @nRezeptpflicht,
            nBTM = @nBTM, nKuehlpflichtig = @nKuehlpflichtig, cATC = @cATC,
            cWirkstoff = @cWirkstoff, dGueltigAb = @dGueltigAb, dGueltigBis = @dGueltigBis,
            dGeaendert = GETDATE()
        WHERE cPZN = @cPZN;
    END
    ELSE
    BEGIN
        INSERT INTO NOVVIA.ABdataArtikel (cPZN, cName, cHersteller, cDarreichungsform, cPackungsgroesse,
            fMenge, cEinheit, fAEP, fAVP, fAEK, nRezeptpflicht, nBTM, nKuehlpflichtig, cATC, cWirkstoff,
            dGueltigAb, dGueltigBis)
        VALUES (@cPZN, @cName, @cHersteller, @cDarreichungsform, @cPackungsgroesse, @fMenge, @cEinheit,
            @fAEP, @fAVP, @fAEK, @nRezeptpflicht, @nBTM, @nKuehlpflichtig, @cATC, @cWirkstoff,
            @dGueltigAb, @dGueltigBis);
    END
END;
GO

-- SP: ABdata Auto-Mapping
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spABdataAutoMapping' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spABdataAutoMapping;
GO

CREATE PROCEDURE NOVVIA.spABdataAutoMapping
    @nAnzahlGemappt INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @nAnzahlGemappt = 0;

    -- Mapping via Barcode = PZN
    INSERT INTO NOVVIA.ABdataArtikelMapping (kArtikel, cPZN, nAutomatisch)
    SELECT DISTINCT a.kArtikel, ab.cPZN, 1
    FROM dbo.tArtikel a
    INNER JOIN NOVVIA.ABdataArtikel ab ON a.cBarcode = ab.cPZN
    WHERE NOT EXISTS (SELECT 1 FROM NOVVIA.ABdataArtikelMapping m WHERE m.kArtikel = a.kArtikel);
    SET @nAnzahlGemappt = @nAnzahlGemappt + @@ROWCOUNT;

    -- Mapping via HAN = PZN
    INSERT INTO NOVVIA.ABdataArtikelMapping (kArtikel, cPZN, nAutomatisch)
    SELECT DISTINCT a.kArtikel, ab.cPZN, 1
    FROM dbo.tArtikel a
    INNER JOIN NOVVIA.ABdataArtikel ab ON a.cHAN = ab.cPZN
    WHERE NOT EXISTS (SELECT 1 FROM NOVVIA.ABdataArtikelMapping m WHERE m.kArtikel = a.kArtikel);
    SET @nAnzahlGemappt = @nAnzahlGemappt + @@ROWCOUNT;
END;
GO

PRINT '      Stored Procedures OK';
GO

-- =====================================================
-- 7b. RECHNUNG & RECHNUNGSKORREKTUR SPs
-- =====================================================
PRINT '[7b/8] Rechnung/Rechnungskorrektur SPs...';

-- -------------------------------------------------------
-- NOVVIA.spRechnungLesen
-- Liest eine einzelne Rechnung mit allen Details und Positionen
-- -------------------------------------------------------
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spRechnungLesen' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spRechnungLesen;
GO

CREATE PROCEDURE NOVVIA.spRechnungLesen
    -- ============================================================================
    -- Parameter
    -- ============================================================================
    @kRechnung INT                     -- Primaerschluessel der Rechnung (Pflicht)

    -- ============================================================================
    -- Beschreibung:
    --   Liest eine einzelne Rechnung mit allen Details und Positionen.
    --   Gibt zwei Resultsets zurueck:
    --     1. Rechnungskopf mit Adress- und Zahlungsdaten
    --     2. Rechnungspositionen mit Artikeldetails
    --
    -- Verwendet Views:
    --   - dbo.lvRechnungsverwaltung (JTL-Standardview fuer Rechnungen)
    --   - dbo.lvRechnungspositionsverwaltung (JTL-Standardview fuer Positionen)
    --
    -- Beispiel:
    --   EXEC NOVVIA.spRechnungLesen @kRechnung = 12345
    --
    -- Historie:
    --   2026-01-03 - Erstellt (NovviaERP, basierend auf JTL-Struktur)
    -- ============================================================================
AS
BEGIN
    SET NOCOUNT ON;

    -- Resultset 1: Rechnungskopf
    SELECT
        r.kRechnung, r.kKunde, r.kAuftrag, r.kBenutzer, r.kFirma, r.kZahlungsart, r.kVersandart,
        r.kPlattform, r.kShop, r.kSprache, r.cRechnungsnummer, r.nStorno, r.nIstEntwurf, r.nArchiv,
        r.nIstExterneRechnung, r.nKorrigiert, r.nRechnungskorrekturErstellt, r.dErstellt, r.dValutadatum,
        r.dBezahldatum, r.dDruckdatum, r.dMaildatum, r.dStorniert, r.dZahlungsziel, r.nZahlungszielInTagen,
        r.fGesamtNettopreis, r.fGesamtBruttopreis, r.fRechnungswertVersandlandNetto, r.fRechnungswertVersandlandBrutto,
        r.fOffenerWert, r.fBereitsgezahltWert, r.fGutgeschriebenerWert, r.nIstKomplettBezahlt,
        r.cWaehrung, r.fWaehrungsfaktor, r.cKundeNr, r.nDebitorennr, r.cKundengruppe,
        r.cZahlungsartname, r.cZahlungsart, r.cVersandart, r.nMahnstopp, r.nMahnstufe, r.dMahndatum, r.nIstAngemahnt,
        r.cStornoKommentar, r.cStornogrund, r.cStornoBenutzername,
        r.cRechnungsadresseFirma, r.cRechnungsadresseAnrede, r.cRechnungsadresseTitel, r.cRechnungsadresseVorname,
        r.cRechnungsadresseNachname, r.cRechnungsadresseStrasse, r.cRechnungsadresseAdresszusatz,
        r.cRechnungsadressePlz, r.cRechnungsadresseOrt, r.cRechnungsadresseLand, r.cRechnungsadresseLandIso,
        r.cRechnungsadresseBundesland, r.cRechnungsadresseTelefon, r.cRechnungsadresseMobilTelefon,
        r.cRechnungsadresseFax, r.cRechnungsadresseMail,
        r.cLieferadresseFirma, r.cLieferadresseAnrede, r.cLieferadresseTitel, r.cLieferadresseVorname,
        r.cLieferadresseNachname, r.cLieferadresseStrasse, r.cLieferadresseAdresszusatz,
        r.cLieferadressePlz, r.cLieferadresseOrt, r.cLieferadresseLand, r.cLieferadresseLandIso,
        r.cLieferadresseBundesland, r.cLieferadresseTelefon, r.cLieferadresseMobilTelefon,
        r.cLieferadresseFax, r.cLieferadresseMail,
        r.cFirmenname, r.cBenutzername, r.cAuftragsNr, r.cExterneAuftragsnummer, r.ceBayBenutzername,
        r.cShopname, r.nPlattformTyp, r.cAnmerkung, r.cSonstiges, r.nFarbcode, r.cFarbbedeutung,
        r.nSteuereinstellung, r.cUmsatzsteuerID, r.nZahlungStatus
    FROM Verkauf.lvRechnungsverwaltung r
    WHERE r.kRechnung = @kRechnung;

    -- Resultset 2: Rechnungspositionen
    SELECT p.kRechnungPosition, p.kRechnung, p.kArtikel, p.kAuftrag, p.kAuftragPosition,
        p.cArtNr, p.cName, p.cEinheit, p.fAnzahl, p.fMwSt,
        p.fNettoPreisEinzeln AS fVkNetto, p.fBruttoPreisEinzeln AS fVkBrutto,
        p.fRabattProzent AS fRabatt, p.fGewichtEinzeln AS fGewicht,
        p.fVersandgewichtEinzeln AS fVersandgewicht, p.nSort
    FROM Verkauf.lvRechnungsposition p
    WHERE p.kRechnung = @kRechnung
    ORDER BY p.nSort, p.kRechnungPosition;
END;
GO

-- -------------------------------------------------------
-- NOVVIA.spMahnstufen
-- Liefert alle Mahnstufen aus der JTL-Tabelle tMahnstufe
-- So bleibt die Anwendung unabhaengig von JTL-Aenderungen
-- -------------------------------------------------------
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spMahnstufen' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spMahnstufen;
GO

CREATE PROCEDURE NOVVIA.spMahnstufen
    -- ============================================================================
    -- NOVVIA.spMahnstufen - Mahnstufen aus JTL-Tabelle lesen
    -- ============================================================================
    -- Liefert alle Mahnstufen inkl. "Keine Mahnung" (nStufe=0)
    -- Quelle: dbo.tMahnstufe (JTL-Tabelle, nicht aendern!)
    --
    -- Rueckgabe:
    --   nStufe                  - Mahnstufe (0=keine, 1-n aus tMahnstufe)
    --   cName                   - Bezeichnung der Mahnstufe
    --   fGebuehrPauschal        - Mahngebuehr (pauschal)
    --   fGebuehrZinssatz        - Zinssatz fuer Mahnungen
    --   nKarenzzeit             - Karenzzeit in Tagen
    --   nZahlungsfristInTagen   - Zahlungsfrist nach Mahnung
    --
    -- Verwendung in C#:
    --   var mahnstufen = await coreService.GetMahnstufen();
    --   // Fuer Dropdown-Filter in RechnungenView
    --
    -- Changelog:
    --   2026-01-03 - Erstellt (NovviaERP)
    -- ============================================================================
AS
BEGIN
    SET NOCOUNT ON;

    -- Liefert alle Mahnstufen aus der JTL-Tabelle tMahnstufe
    -- So bleibt die Anwendung unabhaengig von JTL-Aenderungen
    SELECT
        0 AS nStufe,
        N'Keine Mahnung' AS cName,
        CAST(0 AS DECIMAL(18,13)) AS fGebuehrPauschal,
        CAST(0 AS DECIMAL(18,13)) AS fGebuehrZinssatz,
        0 AS nKarenzzeit,
        0 AS nZahlungsfristInTagen
    UNION ALL
    SELECT
        nStufe,
        cName,
        fGebuehrPauschal,
        fGebuehrZinssatz,
        nKarenzzeit,
        nZahlungsfristInTagen
    FROM dbo.tMahnstufe
    WHERE kFirma = 0  -- Standard-Mahnstufen (nicht kundengruppen-spezifisch)
    ORDER BY nStufe;
END;
GO

-- -------------------------------------------------------
-- NOVVIA.spRechnungenAuflisten
-- Listet Rechnungen mit diversen Filtermoeglichkeiten auf
-- -------------------------------------------------------
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spRechnungenAuflisten' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spRechnungenAuflisten;
GO

CREATE PROCEDURE NOVVIA.spRechnungenAuflisten
    @cSuche NVARCHAR(100) = NULL,       -- Suche in Rechnungsnummer, Kundennummer, Name
    @nStatus INT = NULL,                 -- 0=Offen, 1=Bezahlt, 2=Storniert, 3=Teilbezahlt, 4=Angemahnt
    @nMahnstufe INT = NULL,              -- Filter nach Mahnstufe (Werte aus NOVVIA.spMahnstufen, 0=keine)
    @kKunde INT = NULL,                  -- Filter nach Kunde
    @kPlattform INT = NULL,              -- Filter nach Plattform
    @dVon DATETIME = NULL,               -- Erstellt ab Datum
    @dBis DATETIME = NULL,               -- Erstellt bis Datum
    @nNurOffene BIT = 0,                 -- Nur offene Rechnungen (fOffenerWert > 0)
    @nNurStornierte BIT = 0,             -- Nur stornierte Rechnungen
    @nNurAngemahnt BIT = 0,              -- Nur angemahnte Rechnungen
    @nLimit INT = 1000                   -- Maximale Anzahl Ergebnisse
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@nLimit)
        r.kRechnung, r.kKunde, r.kAuftrag, r.cRechnungsnummer, r.dErstellt, r.dValutadatum,
        r.dBezahldatum, r.dZahlungsziel, r.fGesamtNettopreis, r.fGesamtBruttopreis,
        r.fOffenerWert, r.fBereitsgezahltWert, r.fGutgeschriebenerWert, r.nIstKomplettBezahlt,
        r.nStorno, r.nIstEntwurf, r.nIstAngemahnt, r.nMahnstufe, r.dMahndatum, r.nKorrigiert, r.nRechnungskorrekturErstellt,
        r.cKundeNr, r.nDebitorennr,
        ISNULL(r.cRechnungsadresseFirma, LTRIM(RTRIM(ISNULL(r.cRechnungsadresseVorname, '') + ' ' + ISNULL(r.cRechnungsadresseNachname, '')))) AS cKundeName,
        r.cRechnungsadresseOrt, r.cZahlungsartname, r.cVersandart, r.cWaehrung,
        r.kPlattform, r.kShop, r.cShopname, r.cAuftragsNr, r.cExterneAuftragsnummer,
        r.dDruckdatum, r.dMaildatum, r.dStorniert, r.cStornogrund, r.nFarbcode, r.cFarbbedeutung
    FROM Verkauf.lvRechnungsverwaltung r
    WHERE (@cSuche IS NULL OR r.cRechnungsnummer LIKE '%' + @cSuche + '%' OR r.cKundeNr LIKE '%' + @cSuche + '%'
           OR r.cRechnungsadresseFirma LIKE '%' + @cSuche + '%' OR r.cRechnungsadresseNachname LIKE '%' + @cSuche + '%'
           OR r.cAuftragsNr LIKE '%' + @cSuche + '%')
        AND (@nStatus IS NULL OR
             (@nStatus = 0 AND r.nIstKomplettBezahlt = 0 AND r.nStorno = 0) OR
             (@nStatus = 1 AND r.nIstKomplettBezahlt = 1 AND r.nStorno = 0) OR
             (@nStatus = 2 AND r.nStorno = 1) OR
             (@nStatus = 3 AND r.fBereitsgezahltWert > 0 AND r.fOffenerWert > 0) OR
             (@nStatus = 4 AND r.nIstAngemahnt = 1))
        -- Mahnstufen-Filter (Werte dynamisch aus dbo.tMahnstufe via NOVVIA.spMahnstufen)
        AND (@nMahnstufe IS NULL OR
             (@nMahnstufe = 0 AND r.nMahnstufe IS NULL) OR
             (@nMahnstufe > 0 AND r.nMahnstufe = @nMahnstufe))
        AND (@kKunde IS NULL OR r.kKunde = @kKunde)
        AND (@kPlattform IS NULL OR r.kPlattform = @kPlattform)
        AND (@dVon IS NULL OR r.dErstellt >= @dVon)
        AND (@dBis IS NULL OR r.dErstellt < DATEADD(DAY, 1, @dBis))
        AND (@nNurOffene = 0 OR r.fOffenerWert > 0)
        AND (@nNurStornierte = 0 OR r.nStorno = 1)
        AND (@nNurAngemahnt = 0 OR r.nIstAngemahnt = 1)
    ORDER BY r.dErstellt DESC, r.kRechnung DESC;
END;
GO

-- -------------------------------------------------------
-- NOVVIA.spRechnungStornieren
-- Storniert eine einzelne Rechnung (Wrapper um JTL-SP)
-- -------------------------------------------------------
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spRechnungStornieren' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spRechnungStornieren;
GO

CREATE PROCEDURE NOVVIA.spRechnungStornieren
    @kRechnung INT,                                  -- Primaerschluessel der Rechnung
    @kBenutzer INT,                                  -- Benutzer-ID
    @kRechnungStornogrund INT = -1,                  -- Stornogrund: -5=Steuern, -4=Preise, -3=Positionen, -2=Adresse, -1=Sonstiges
    @cKommentar NVARCHAR(100) = NULL,               -- Optionaler Kommentar
    @dStorniert DATETIME = NULL,                     -- Stornodatum (Standard: GETDATE())
    @nZahlungenZusammenfassen BIT = 1,               -- Zahlungen auf Auftrag umbuchen
    @kGutschrift INT = NULL OUTPUT                   -- Rueckgabe: ID des erstellten Stornobelegs
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @xResult XML;
    DECLARE @Rechnungen Rechnung.TYPE_spRechnungenStornieren;

    IF @dStorniert IS NULL SET @dStorniert = GETDATE();
    INSERT INTO @Rechnungen (kRechnung) VALUES (@kRechnung);

    EXEC Rechnung.spRechnungenStornieren
        @Rechnungen = @Rechnungen, @kRechnungStornogrund = @kRechnungStornogrund,
        @cKommentar = @cKommentar, @kBenutzer = @kBenutzer, @dStorniert = @dStorniert,
        @ZahlungenZusammenfassen = @nZahlungenZusammenfassen, @xResult = @xResult OUTPUT;

    SELECT @kGutschrift = T.C.value('kGutschrift[1]', 'INT')
    FROM @xResult.nodes('/StornoResults/StornoResult') AS T(C)
    WHERE T.C.value('kRechnung[1]', 'INT') = @kRechnung;

    SELECT T.C.value('kRechnung[1]', 'INT') AS kRechnung, T.C.value('cRechnungsnr[1]', 'NVARCHAR(50)') AS cRechnungsnr,
           T.C.value('nError[1]', 'INT') AS nError, T.C.value('kGutschrift[1]', 'INT') AS kGutschrift
    FROM @xResult.nodes('/StornoResults/StornoResult') AS T(C);

    EXEC NOVVIA.spLogSchreiben @cKategorie = 'Rechnung', @cAktion = 'Storniert', @cModul = 'Rechnungen',
        @cEntityTyp = 'Rechnung', @kEntity = @kRechnung, @cBeschreibung = @cKommentar, @kBenutzer = @kBenutzer, @nSeverity = 1;
END;
GO

-- -------------------------------------------------------
-- NOVVIA.spRechnungskorrekturLesen
-- Liest eine einzelne Rechnungskorrektur mit Positionen
-- -------------------------------------------------------
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spRechnungskorrekturLesen' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spRechnungskorrekturLesen;
GO

CREATE PROCEDURE NOVVIA.spRechnungskorrekturLesen
    @kGutschrift INT                     -- Primaerschluessel der Rechnungskorrektur
AS
BEGIN
    SET NOCOUNT ON;

    -- Resultset 1: Rechnungskorrektur-Kopf
    SELECT g.kGutschrift, g.kRechnung, g.kKunde, g.kBenutzer, g.kFirma, g.kPlattform, g.kShop, g.kSprache,
        g.kRechnungsadresse, g.cRechnungskorrekturnummer, g.cRechnungsnummer, g.nStorno, g.nStornoTyp,
        g.dErstellt, g.dDruckdatum, g.dMaildatum, g.dStorniert, g.fPreisNetto, g.fPreisBrutto, g.fMwst,
        g.cWaehrung, g.fFaktor, g.cKundeNr, g.cKundengruppe, g.cKurztext, g.cText, g.cAnmerkung, g.cSonstiges,
        g.cStatustext, g.cErloeskonto, g.cRechnungsadresseFirma, g.cRechnungsadresseAnrede, g.cRechnungsadresseTitel,
        g.cRechnungsadresseVorname, g.cRechnungsadresseNachname, g.cRechnungsadresseStrasse, g.cRechnungsadresseAdresszusatz,
        g.cRechnungsadressePlz, g.cRechnungsadresseOrt, g.cRechnungsadresseLand, g.cRechnungsadresseLandIso,
        g.cRechnungsadresseBundesland, g.cRechnungsadresseTelefon, g.cRechnungsadresseMobilTelefon,
        g.cRechnungsadresseFax, g.cRechnungsadresseMail, g.cLieferadresseFirma, g.cLieferadresseAnrede,
        g.cLieferadresseTitel, g.cLieferadresseVorname, g.cLieferadresseNachname, g.cLieferadresseStrasse,
        g.cLieferadresseAdresszusatz, g.cLieferadressePlz, g.cLieferadresseOrt, g.cLieferadresseLand,
        g.cLieferadresseLandIso, g.cLieferadresseBundesland, g.cLieferadresseTelefon, g.cLieferadresseMobilTelefon,
        g.cLieferadresseFax, g.cLieferadresseMail, g.cFirmenname, g.cBenutzername, g.ceBayBenutzername, g.cShopname,
        g.cStornoKommentar, g.cStornogrund, g.cStorniertVon
    FROM Verkauf.lvRechnungskorrekturverwaltung g
    WHERE g.kGutschrift = @kGutschrift;

    -- Resultset 2: Rechnungskorrektur-Positionen
    SELECT p.kGutschriftPos, p.kGutschrift, p.kArtikel, p.kAuftrag, p.kAuftragPosition, p.kRechnungPosition,
        p.cArtNr, p.cName, p.fAnzahl, p.fMwSt, p.fVKNetto, p.fVKBrutto, p.kStuecklistenVater, p.kKonfigurationsVater, p.nSort
    FROM Verkauf.lvRechnungskorrekturposition p
    WHERE p.kGutschrift = @kGutschrift
    ORDER BY p.nSort, p.kGutschriftPos;
END;
GO

-- -------------------------------------------------------
-- NOVVIA.spRechnungskorrekturenAuflisten
-- Listet Rechnungskorrekturen mit Filtermoeglichkeiten auf
-- -------------------------------------------------------
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spRechnungskorrekturenAuflisten' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spRechnungskorrekturenAuflisten;
GO

CREATE PROCEDURE NOVVIA.spRechnungskorrekturenAuflisten
    @cSuche NVARCHAR(100) = NULL,       -- Suche in Korrekturnummer, Rechnungsnummer, Kundennr
    @nNurStornierte BIT = NULL,         -- NULL=Alle, 0=Nicht storniert, 1=Nur storniert
    @kKunde INT = NULL,                  -- Filter nach Kunde
    @kPlattform INT = NULL,              -- Filter nach Plattform
    @dVon DATETIME = NULL,               -- Erstellt ab Datum
    @dBis DATETIME = NULL,               -- Erstellt bis Datum
    @nNurStornobelege BIT = 0,           -- Nur Stornobelege (nStornoTyp = 1)
    @nLimit INT = 1000                   -- Maximale Anzahl Ergebnisse
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@nLimit)
        g.kGutschrift, g.kRechnung, g.kKunde, g.cRechnungskorrekturnummer, g.cRechnungsnummer,
        g.dErstellt, g.dDruckdatum, g.dMaildatum, g.dStorniert, g.fPreisNetto, g.fPreisBrutto, g.fMwst,
        g.nStorno, g.nStornoTyp, g.cKundeNr,
        ISNULL(g.cRechnungsadresseFirma, LTRIM(RTRIM(ISNULL(g.cRechnungsadresseVorname, '') + ' ' + ISNULL(g.cRechnungsadresseNachname, '')))) AS cKundeName,
        g.cRechnungsadresseOrt, g.cKurztext, g.cText, g.cAnmerkung, g.cWaehrung,
        g.kPlattform, g.kShop, g.cShopname, g.cFirmenname, g.cBenutzername, g.cStornogrund, g.cStornoKommentar, g.cStorniertVon
    FROM Verkauf.lvRechnungskorrekturverwaltung g
    WHERE (@cSuche IS NULL OR g.cRechnungskorrekturnummer LIKE '%' + @cSuche + '%' OR g.cRechnungsnummer LIKE '%' + @cSuche + '%'
           OR g.cKundeNr LIKE '%' + @cSuche + '%' OR g.cRechnungsadresseFirma LIKE '%' + @cSuche + '%'
           OR g.cRechnungsadresseNachname LIKE '%' + @cSuche + '%')
        AND (@nNurStornierte IS NULL OR g.nStorno = @nNurStornierte)
        AND (@nNurStornobelege = 0 OR g.nStornoTyp = 1)
        AND (@kKunde IS NULL OR g.kKunde = @kKunde)
        AND (@kPlattform IS NULL OR g.kPlattform = @kPlattform)
        AND (@dVon IS NULL OR g.dErstellt >= @dVon)
        AND (@dBis IS NULL OR g.dErstellt < DATEADD(DAY, 1, @dBis))
    ORDER BY g.dErstellt DESC, g.kGutschrift DESC;
END;
GO

-- -------------------------------------------------------
-- NOVVIA.spRechnungskorrekturStornieren
-- Storniert eine einzelne Rechnungskorrektur (Wrapper um JTL-SP)
-- -------------------------------------------------------
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spRechnungskorrekturStornieren' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spRechnungskorrekturStornieren;
GO

CREATE PROCEDURE NOVVIA.spRechnungskorrekturStornieren
    @kGutschrift INT,                                -- Primaerschluessel der Rechnungskorrektur
    @kBenutzer INT,                                  -- Benutzer-ID
    @kGutschriftStornogrund INT = -1,                -- Stornogrund: -5=Steuern, -4=Preise, -3=Positionen, -2=Adresse, -1=Sonstiges
    @cKommentar NVARCHAR(100) = NULL,               -- Optionaler Kommentar
    @dStorniert DATETIME = NULL,                     -- Stornodatum (Standard: GETDATE())
    @kStornoGutschrift INT = NULL OUTPUT             -- Rueckgabe: ID des erstellten Gegen-Stornobelegs
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @xResult XML;
    DECLARE @Gutschriften dbo.TYPE_spGutschriftenStornieren;

    IF @dStorniert IS NULL SET @dStorniert = GETDATE();
    INSERT INTO @Gutschriften (kGutschrift) VALUES (@kGutschrift);

    EXEC dbo.spGutschriftenStornieren
        @Gutschriften = @Gutschriften, @kGutschriftStornogrund = @kGutschriftStornogrund,
        @cKommentar = @cKommentar, @kBenutzer = @kBenutzer, @dStorniert = @dStorniert, @xResult = @xResult OUTPUT;

    SELECT @kStornoGutschrift = T.C.value('kStornoGutschrift[1]', 'INT')
    FROM @xResult.nodes('/StornoResults/StornoResult') AS T(C)
    WHERE T.C.value('kGutschrift[1]', 'INT') = @kGutschrift;

    SELECT T.C.value('kGutschrift[1]', 'INT') AS kGutschrift, T.C.value('cGutschriftNr[1]', 'NVARCHAR(50)') AS cGutschriftNr,
           T.C.value('nError[1]', 'INT') AS nError, T.C.value('kStornoGutschrift[1]', 'INT') AS kStornoGutschrift
    FROM @xResult.nodes('/StornoResults/StornoResult') AS T(C);

    EXEC NOVVIA.spLogSchreiben @cKategorie = 'Rechnungskorrektur', @cAktion = 'Storniert', @cModul = 'Rechnungskorrekturen',
        @cEntityTyp = 'Gutschrift', @kEntity = @kGutschrift, @cBeschreibung = @cKommentar, @kBenutzer = @kBenutzer, @nSeverity = 1;
END;
GO

PRINT '      Rechnung/Rechnungskorrektur SPs OK';
PRINT '';
PRINT '      WICHTIG: Fuer JTL-unabhaengige Basistabellen-Version';
PRINT '      fuehre nach dieser Installation aus:';
PRINT '      Scripts/UPDATE-NOVVIA-BaseTables.sql';
PRINT '';
PRINT '      Neue SPs (Basistabellen):';
PRINT '      - spLieferscheineAuflisten, spLieferscheinLesen';
PRINT '      - spLieferantenbestellungenAuflisten, spLieferantenbestellungLesen';
PRINT '      - spEingangsrechnungenAuflisten, spEingangsrechnungLesen';
GO

-- =====================================================
-- INDIZES
-- =====================================================
PRINT '';
PRINT 'Indizes erstellen...';

-- Worker-Log Index
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_WorkerLog_Worker_Zeit')
CREATE NONCLUSTERED INDEX IX_WorkerLog_Worker_Zeit
ON NOVVIA.tWorkerLog(cWorker, dZeitpunkt DESC);

-- MSV3 Cache Index
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_MSV3Cache_Lieferant_PZN')
CREATE NONCLUSTERED INDEX IX_MSV3Cache_Lieferant_PZN
ON NOVVIA.MSV3VerfuegbarkeitCache(kMSV3Lieferant, cPZN);

-- MSV3 Log Index
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_MSV3Log_Zeitpunkt')
CREATE NONCLUSTERED INDEX IX_MSV3Log_Zeitpunkt
ON NOVVIA.MSV3Log(dZeitpunkt DESC);

-- MSV3 Bestand Cache Indizes
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_MSV3BestandCache_Pzn')
CREATE NONCLUSTERED INDEX IX_MSV3BestandCache_Pzn
ON NOVVIA.MSV3BestandCache(cPzn);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_MSV3BestandCache_Abfrage')
CREATE NONCLUSTERED INDEX IX_MSV3BestandCache_Abfrage
ON NOVVIA.MSV3BestandCache(dAbfrage);

-- ABdata Indizes
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ABdataArtikel_PZN')
CREATE NONCLUSTERED INDEX IX_ABdataArtikel_PZN
ON NOVVIA.ABdataArtikel(cPZN);

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ABdataArtikelMapping_Artikel')
CREATE NONCLUSTERED INDEX IX_ABdataArtikelMapping_Artikel
ON NOVVIA.ABdataArtikelMapping(kArtikel);

PRINT 'Indizes OK';
GO

-- =====================================================
-- 8. BENUTZERRECHTE-SYSTEM (SEPARAT)
-- =====================================================
PRINT '';
PRINT '[8/8] Benutzerrechte-System...';
PRINT '      HINWEIS: Benutzerrechte werden separat installiert!';
PRINT '      Script: Setup-NOVVIA-Benutzerrechte.sql';
PRINT '';
PRINT '      Das Benutzerrechte-System enthaelt:';
PRINT '      - Rollen (Admin, Verkauf, Lager, Einkauf, RP...)';
PRINT '      - Rechte pro Modul und Aktion';
PRINT '      - Pharma-Modus (ValidierungBearbeiten nur fuer RP)';
PRINT '      - Session-Management und Login-Protokollierung';
GO

-- =====================================================
-- ABSCHLUSS
-- =====================================================
PRINT '';
PRINT '=====================================================';
PRINT 'NOVVIA ERP - Basis-Installation abgeschlossen!';
PRINT '=====================================================';
PRINT '';
PRINT 'Erstellte Objekte:';
PRINT '';
PRINT '  Schema: NOVVIA';
PRINT '';
PRINT '  Auth-Tabellen:';
PRINT '    - tRolle, tBerechtigung, tRolleBerechtigung, tBenutzerLog';
PRINT '';
PRINT '  Basis-Tabellen:';
PRINT '    - NOVVIA.tImportVorlage, NOVVIA.tImportLog';
PRINT '    - NOVVIA.tWorkerStatus, NOVVIA.tWorkerLog';
PRINT '    - NOVVIA.tAusgabeLog, NOVVIA.tDokumentArchiv';
PRINT '';
PRINT '  MSV3-Tabellen (Pharma-Grosshandel):';
PRINT '    - NOVVIA.MSV3Lieferant, NOVVIA.MSV3Bestellung';
PRINT '    - NOVVIA.MSV3BestellungPos, NOVVIA.MSV3VerfuegbarkeitCache';
PRINT '    - NOVVIA.MSV3Log, NOVVIA.MSV3BestandCache';
PRINT '';
PRINT '  ABdata-Tabellen (Pharma-Stammdaten):';
PRINT '    - NOVVIA.ABdataArtikel, NOVVIA.ABdataArtikelMapping';
PRINT '';
PRINT '  Einkauf-Tabellen:';
PRINT '    - NOVVIA.tEinkaufsliste, NOVVIA.tLieferantErweitert';
PRINT '';
PRINT '  Views:';
PRINT '    - NOVVIA.vLieferantenUebersicht';
PRINT '    - NOVVIA.vMSV3Bestellungen';
PRINT '';
PRINT '  Stored Procedures:';
PRINT '    - NOVVIA.spMSV3LieferantSpeichern';
PRINT '    - NOVVIA.spArtikelEigenesFeldSetzen';
PRINT '    - NOVVIA.spWorkerLogSchreiben, NOVVIA.spWorkerStatusAktualisieren';
PRINT '    - NOVVIA.spMSV3BestandCache_Get/Upsert/Cleanup';
PRINT '    - NOVVIA.spArtikelEigenesFeldCreateOrUpdate';
PRINT '    - NOVVIA.spAuftragEigenesFeldCreateOrUpdate';
PRINT '    - NOVVIA.spABdataArtikelUpsert, NOVVIA.spABdataAutoMapping';
PRINT '';
PRINT '  Rechnung/Rechnungskorrektur SPs:';
PRINT '    - NOVVIA.spMahnstufen (dynamisch aus tMahnstufe)';
PRINT '    - NOVVIA.spRechnungLesen, spRechnungenAuflisten, spRechnungStornieren';
PRINT '    - NOVVIA.spRechnungskorrekturLesen, spRechnungskorrekturenAuflisten, spRechnungskorrekturStornieren';
PRINT '';
PRINT '  Einkauf/Versand SPs (nach UPDATE-NOVVIA-BaseTables.sql):';
PRINT '    - NOVVIA.spLieferscheineAuflisten, spLieferscheinLesen';
PRINT '    - NOVVIA.spLieferantenbestellungenAuflisten, spLieferantenbestellungLesen';
PRINT '    - NOVVIA.spEingangsrechnungenAuflisten, spEingangsrechnungLesen';
PRINT '';
PRINT '  Types:';
PRINT '    - NOVVIA.TYPE_ArtikelEigenesFeldAnpassen';
PRINT '    - NOVVIA.TYPE_AuftragEigenesFeldAnpassen';
PRINT '';
PRINT 'Naechste Schritte:';
PRINT '  1. UPDATE-NOVVIA-BaseTables.sql ausfuehren (JTL-unabhaengige SPs!)';
PRINT '  2. Setup-NOVVIA-Benutzerrechte.sql ausfuehren (Rollen & Rechte)';
PRINT '  3. Setup-NOVVIA-Log.sql ausfuehren (Logging-System)';
PRINT '  4. Setup-NOVVIA-Quarantaene.sql ausfuehren (Chargen-Sperren)';
PRINT '  5. MSV3-Lieferanten konfigurieren (Pharma-Grosshandel)';
PRINT '  6. ABdata-Stammdaten importieren (falls Pharma-Betrieb)';
PRINT '  7. Worker-Dienst starten';
PRINT '';
PRINT 'Pharma-Modus aktivieren:';
PRINT '  UPDATE NOVVIA.FirmaEinstellung SET cWert=''1'' WHERE cSchluessel=''PHARMA''';
PRINT '=====================================================';
GO

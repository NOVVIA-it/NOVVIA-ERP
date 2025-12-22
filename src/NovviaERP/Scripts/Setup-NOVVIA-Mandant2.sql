-- NOVVIA ERP - Einkauf Erweiterung für Mandant_2 (Produktion)
USE Mandant_2;
GO

-- Schema erstellen
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'NOVVIA')
BEGIN
    EXEC('CREATE SCHEMA NOVVIA');
END
GO

-- MSV3 Lieferanten-Konfiguration
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MSV3Lieferant' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
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
    PRINT 'Tabelle NOVVIA.MSV3Lieferant erstellt';
END
GO

-- MSV3 Bestellungen
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MSV3Bestellung' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
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
    PRINT 'Tabelle NOVVIA.MSV3Bestellung erstellt';
END
GO

-- MSV3 Bestellpositionen
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MSV3BestellungPos' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    CREATE TABLE NOVVIA.MSV3BestellungPos (
        kMSV3BestellungPos      INT IDENTITY(1,1) PRIMARY KEY,
        kMSV3Bestellung         INT NOT NULL,
        kLieferantenBestellungPos INT NULL,
        cPZN                    NVARCHAR(20) NOT NULL,
        fMengeAngefragt         DECIMAL(18,4) NOT NULL,
        fMengeVerfuegbar        DECIMAL(18,4) NULL,
        fPreisEK                DECIMAL(18,4) NULL,
        fPreisAEP               DECIMAL(18,4) NULL,
        fPreisAVP               DECIMAL(18,4) NULL,
        cStatus                 NVARCHAR(50) NULL,
        cChargenNr              NVARCHAR(50) NULL,
        dMHD                    DATE NULL,
        cLieferzeit             NVARCHAR(50) NULL,
        cFehler                 NVARCHAR(500) NULL
    );
    PRINT 'Tabelle NOVVIA.MSV3BestellungPos erstellt';
END
GO

-- ABdata Artikel
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ABdataArtikel' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    CREATE TABLE NOVVIA.ABdataArtikel (
        kABdataArtikel      INT IDENTITY(1,1) PRIMARY KEY,
        cPZN                NVARCHAR(20) NOT NULL UNIQUE,
        cName               NVARCHAR(255) NOT NULL,
        cHersteller         NVARCHAR(100) NULL,
        fAEP                DECIMAL(18,4) NULL,
        fAVP                DECIMAL(18,4) NULL,
        fAEK                DECIMAL(18,4) NULL,
        nRezeptpflicht      TINYINT DEFAULT 0,
        nBTM                TINYINT DEFAULT 0,
        nKuehlpflichtig     TINYINT DEFAULT 0,
        cATC                NVARCHAR(20) NULL,
        cWirkstoff          NVARCHAR(255) NULL,
        cDarreichungsform   NVARCHAR(100) NULL,
        cPackungsgroesse    NVARCHAR(50) NULL,
        dImportiert         DATETIME DEFAULT GETDATE(),
        dAktualisiert       DATETIME NULL
    );
    PRINT 'Tabelle NOVVIA.ABdataArtikel erstellt';
END
GO

-- ABdata Artikel Mapping
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ABdataArtikelMapping' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    CREATE TABLE NOVVIA.ABdataArtikelMapping (
        kABdataArtikelMapping   INT IDENTITY(1,1) PRIMARY KEY,
        kArtikel                INT NOT NULL,
        cPZN                    NVARCHAR(20) NOT NULL,
        nAutomatisch            TINYINT DEFAULT 0,
        dErstellt               DATETIME DEFAULT GETDATE()
    );
    PRINT 'Tabelle NOVVIA.ABdataArtikelMapping erstellt';
END
GO

-- ABdata Import Log
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ABdataImportLog' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    CREATE TABLE NOVVIA.ABdataImportLog (
        kABdataImportLog    INT IDENTITY(1,1) PRIMARY KEY,
        cDateiname          NVARCHAR(255) NOT NULL,
        nAnzahlNeu          INT DEFAULT 0,
        nAnzahlAktualisiert INT DEFAULT 0,
        nAnzahlFehler       INT DEFAULT 0,
        cStatus             NVARCHAR(50) NULL,
        cFehler             NVARCHAR(MAX) NULL,
        dStart              DATETIME DEFAULT GETDATE(),
        dEnde               DATETIME NULL
    );
    PRINT 'Tabelle NOVVIA.ABdataImportLog erstellt';
END
GO

-- Eingangsrechnung Erweiterung
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EingangsrechnungErweitert' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    CREATE TABLE NOVVIA.EingangsrechnungErweitert (
        kEingangsrechnungErweitert  INT IDENTITY(1,1) PRIMARY KEY,
        kEingangsrechnung           INT NOT NULL,
        nSkontoTage                 INT NULL,
        fSkontoProzent              DECIMAL(5,2) NULL,
        nGeprueft                   TINYINT DEFAULT 0,
        kPrueferBenutzer            INT NULL,
        dGeprueft                   DATETIME NULL,
        nFreigegeben                TINYINT DEFAULT 0,
        kFreigabeBenutzer           INT NULL,
        dFreigegeben                DATETIME NULL,
        cDokumentPfad               NVARCHAR(500) NULL,
        cAnmerkungIntern            NVARCHAR(MAX) NULL,
        dErstellt                   DATETIME DEFAULT GETDATE()
    );
    PRINT 'Tabelle NOVVIA.EingangsrechnungErweitert erstellt';
END
GO

-- SP: MSV3 Lieferant speichern
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spNOVVIA_MSV3LieferantSpeichern')
    DROP PROCEDURE spNOVVIA_MSV3LieferantSpeichern;
GO

CREATE PROCEDURE spNOVVIA_MSV3LieferantSpeichern
    @kLieferant INT,
    @cMSV3Url NVARCHAR(500),
    @cMSV3Benutzer NVARCHAR(100),
    @cMSV3Passwort NVARCHAR(255),
    @cMSV3Kundennummer NVARCHAR(50) = NULL,
    @cMSV3Filiale NVARCHAR(20) = NULL,
    @nMSV3Version INT = 1,
    @nPrioritaet INT = 1,
    @kMSV3Lieferant INT OUTPUT
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
            nPrioritaet = @nPrioritaet,
            nAktiv = 1,
            dGeaendert = GETDATE()
        WHERE kLieferant = @kLieferant;

        SELECT @kMSV3Lieferant = kMSV3Lieferant FROM NOVVIA.MSV3Lieferant WHERE kLieferant = @kLieferant;
    END
    ELSE
    BEGIN
        INSERT INTO NOVVIA.MSV3Lieferant (kLieferant, cMSV3Url, cMSV3Benutzer, cMSV3Passwort, cMSV3Kundennummer, cMSV3Filiale, nMSV3Version, nPrioritaet, nAktiv)
        VALUES (@kLieferant, @cMSV3Url, @cMSV3Benutzer, @cMSV3Passwort, @cMSV3Kundennummer, @cMSV3Filiale, @nMSV3Version, @nPrioritaet, 1);

        SET @kMSV3Lieferant = SCOPE_IDENTITY();
    END
END
GO

PRINT 'SP spNOVVIA_MSV3LieferantSpeichern erstellt';
GO

PRINT 'NOVVIA Schema fuer Mandant_2 eingerichtet!';
GO

-- ============================================
-- NOVVIA ERP - Einkauf Erweiterung
-- MSV3 + ABdata + Eingangsrechnung
-- Für Mandant_3 (PA)
-- ============================================

USE Mandant_3;
GO

-- Schema erstellen falls nicht vorhanden
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'NOVVIA')
BEGIN
    EXEC('CREATE SCHEMA NOVVIA');
END
GO

PRINT 'Schema NOVVIA erstellt/vorhanden';
GO

-- ============================================
-- MSV3 Lieferanten-Konfiguration
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MSV3Lieferant' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    CREATE TABLE NOVVIA.MSV3Lieferant (
        kMSV3Lieferant      INT IDENTITY(1,1) PRIMARY KEY,
        kLieferant          INT NOT NULL,                    -- FK zu tlieferant
        cMSV3Url            NVARCHAR(500) NOT NULL,          -- z.B. https://msv3.pharma-grosshandel.de/msv3/
        cMSV3Benutzer       NVARCHAR(100) NOT NULL,
        cMSV3Passwort       NVARCHAR(255) NOT NULL,          -- Verschlüsselt speichern!
        cMSV3Kundennummer   NVARCHAR(50) NULL,               -- Apotheken-IK oder Kundennr
        cMSV3Filiale        NVARCHAR(20) NULL,               -- Filialnummer
        nMSV3Version        INT DEFAULT 1,                   -- 1 oder 2
        nAktiv              TINYINT DEFAULT 1,
        nPrioritaet         INT DEFAULT 1,                   -- Reihenfolge bei Multi-Großhandel
        dErstellt           DATETIME DEFAULT GETDATE(),
        dGeaendert          DATETIME NULL,
        CONSTRAINT FK_MSV3Lieferant_Lieferant FOREIGN KEY (kLieferant) REFERENCES tlieferant(kLieferant)
    );
    CREATE INDEX IX_MSV3Lieferant_Lieferant ON NOVVIA.MSV3Lieferant(kLieferant);
    PRINT 'Tabelle NOVVIA.MSV3Lieferant erstellt';
END
GO

-- ============================================
-- MSV3 Bestellungen (Tracking)
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MSV3Bestellung' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    CREATE TABLE NOVVIA.MSV3Bestellung (
        kMSV3Bestellung         INT IDENTITY(1,1) PRIMARY KEY,
        kLieferantenBestellung  INT NOT NULL,                -- FK zu tLieferantenBestellung
        kMSV3Lieferant          INT NOT NULL,                -- FK zu NOVVIA.MSV3Lieferant
        cMSV3AuftragsId         NVARCHAR(100) NULL,          -- ID vom Großhandel
        cMSV3Status             NVARCHAR(50) NULL,           -- OFFEN, BESTAETIGT, GELIEFERT, STORNO
        nAnzahlPositionen       INT NULL,
        nAnzahlVerfuegbar       INT NULL,
        nAnzahlNichtVerfuegbar  INT NULL,
        dGesendet               DATETIME NULL,
        dBestaetigt             DATETIME NULL,
        dLieferung              DATETIME NULL,
        cResponseXML            NVARCHAR(MAX) NULL,          -- Komplette Antwort für Debugging
        cFehler                 NVARCHAR(1000) NULL,
        dErstellt               DATETIME DEFAULT GETDATE(),
        CONSTRAINT FK_MSV3Bestellung_LB FOREIGN KEY (kLieferantenBestellung) REFERENCES tLieferantenBestellung(kLieferantenBestellung),
        CONSTRAINT FK_MSV3Bestellung_ML FOREIGN KEY (kMSV3Lieferant) REFERENCES NOVVIA.MSV3Lieferant(kMSV3Lieferant)
    );
    CREATE INDEX IX_MSV3Bestellung_LB ON NOVVIA.MSV3Bestellung(kLieferantenBestellung);
    PRINT 'Tabelle NOVVIA.MSV3Bestellung erstellt';
END
GO

-- ============================================
-- MSV3 Positions-Antworten (Verfügbarkeit)
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MSV3BestellungPos' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    CREATE TABLE NOVVIA.MSV3BestellungPos (
        kMSV3BestellungPos      INT IDENTITY(1,1) PRIMARY KEY,
        kMSV3Bestellung         INT NOT NULL,
        kLieferantenBestellungPos INT NOT NULL,
        cPZN                    NVARCHAR(20) NULL,
        fMengeBestellt          DECIMAL(18,4) NOT NULL,
        fMengeVerfuegbar        DECIMAL(18,4) NULL,
        fMengeGeliefert         DECIMAL(18,4) NULL,
        fPreisEK                DECIMAL(18,4) NULL,          -- EK vom Großhandel
        fPreisAEP               DECIMAL(18,4) NULL,          -- Apothekeneinkaufspreis
        fPreisAVP               DECIMAL(18,4) NULL,          -- Apothekenverkaufspreis
        cStatus                 NVARCHAR(50) NULL,           -- VERFUEGBAR, TEILWEISE, NICHT_VERFUEGBAR
        cChargenNr              NVARCHAR(50) NULL,
        dMHD                    DATE NULL,
        cHinweis                NVARCHAR(500) NULL,
        CONSTRAINT FK_MSV3BPos_MB FOREIGN KEY (kMSV3Bestellung) REFERENCES NOVVIA.MSV3Bestellung(kMSV3Bestellung),
        CONSTRAINT FK_MSV3BPos_LBP FOREIGN KEY (kLieferantenBestellungPos) REFERENCES tLieferantenBestellungPos(kLieferantenBestellungPos)
    );
    PRINT 'Tabelle NOVVIA.MSV3BestellungPos erstellt';
END
GO

-- ============================================
-- ABdata Artikel-Stammdaten
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ABdataArtikel' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    CREATE TABLE NOVVIA.ABdataArtikel (
        kABdataArtikel      INT IDENTITY(1,1) PRIMARY KEY,
        cPZN                NVARCHAR(20) NOT NULL,           -- Pharmazentralnummer (unique)
        cName               NVARCHAR(500) NOT NULL,
        cHersteller         NVARCHAR(255) NULL,
        cDarreichungsform   NVARCHAR(100) NULL,              -- Tabletten, Kapseln, etc.
        cPackungsgroesse    NVARCHAR(50) NULL,               -- N1, N2, N3
        fMenge              DECIMAL(18,4) NULL,              -- Anzahl Einheiten
        cEinheit            NVARCHAR(50) NULL,               -- Stück, ml, g
        fAEP               DECIMAL(18,4) NULL,               -- Apothekeneinkaufspreis
        fAVP               DECIMAL(18,4) NULL,               -- Apothekenverkaufspreis
        fAEK               DECIMAL(18,4) NULL,               -- Apothekeneinkaufspreis netto
        fFAM               DECIMAL(18,4) NULL,               -- Festbetrag
        fZuzahlung         DECIMAL(18,4) NULL,
        nRezeptpflicht     TINYINT DEFAULT 0,                -- 0=OTC, 1=Rx
        nBTM               TINYINT DEFAULT 0,                -- Betäubungsmittel
        nKuehlpflichtig    TINYINT DEFAULT 0,
        cATC               NVARCHAR(20) NULL,                -- ATC-Code
        cWirkstoff         NVARCHAR(500) NULL,
        cWirkstaerke       NVARCHAR(100) NULL,
        nNegativliste      TINYINT DEFAULT 0,
        nAktiv             TINYINT DEFAULT 1,
        dGueltigAb         DATE NULL,
        dGueltigBis        DATE NULL,
        dImportiert        DATETIME DEFAULT GETDATE(),
        dGeaendert         DATETIME NULL,
        CONSTRAINT UQ_ABdata_PZN UNIQUE (cPZN)
    );
    CREATE INDEX IX_ABdata_Name ON NOVVIA.ABdataArtikel(cName);
    CREATE INDEX IX_ABdata_Hersteller ON NOVVIA.ABdataArtikel(cHersteller);
    CREATE INDEX IX_ABdata_ATC ON NOVVIA.ABdataArtikel(cATC);
    PRINT 'Tabelle NOVVIA.ABdataArtikel erstellt';
END
GO

-- ============================================
-- ABdata zu JTL Artikel Mapping
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ABdataArtikelMapping' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    CREATE TABLE NOVVIA.ABdataArtikelMapping (
        kABdataArtikelMapping INT IDENTITY(1,1) PRIMARY KEY,
        kArtikel              INT NOT NULL,                  -- FK zu tArtikel
        cPZN                  NVARCHAR(20) NOT NULL,         -- Pharmazentralnummer
        nAutomatisch          TINYINT DEFAULT 0,             -- 0=manuell, 1=auto via EAN/HAN
        dErstellt             DATETIME DEFAULT GETDATE(),
        CONSTRAINT FK_ABdataMapping_Artikel FOREIGN KEY (kArtikel) REFERENCES tArtikel(kArtikel),
        CONSTRAINT UQ_ABdataMapping UNIQUE (kArtikel, cPZN)
    );
    CREATE INDEX IX_ABdataMapping_PZN ON NOVVIA.ABdataArtikelMapping(cPZN);
    PRINT 'Tabelle NOVVIA.ABdataArtikelMapping erstellt';
END
GO

-- ============================================
-- ABdata Import-Log
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ABdataImportLog' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    CREATE TABLE NOVVIA.ABdataImportLog (
        kABdataImportLog    INT IDENTITY(1,1) PRIMARY KEY,
        cDateiname          NVARCHAR(255) NOT NULL,
        dImportStart        DATETIME NOT NULL,
        dImportEnde         DATETIME NULL,
        nAnzahlGesamt       INT NULL,
        nAnzahlNeu          INT NULL,
        nAnzahlAktualisiert INT NULL,
        nAnzahlFehler       INT NULL,
        cStatus             NVARCHAR(50) NULL,               -- GESTARTET, ABGESCHLOSSEN, FEHLER
        cFehlerDetails      NVARCHAR(MAX) NULL
    );
    PRINT 'Tabelle NOVVIA.ABdataImportLog erstellt';
END
GO

-- ============================================
-- Eingangsrechnung Erweiterung (für Zusatzinfos)
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'EingangsrechnungErweitert' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    CREATE TABLE NOVVIA.EingangsrechnungErweitert (
        kEingangsrechnungErweitert INT IDENTITY(1,1) PRIMARY KEY,
        kEingangsrechnung          INT NOT NULL,
        cSkontoBetrag              DECIMAL(18,4) NULL,
        nSkontoTage                INT NULL,
        fSkontoProzent             DECIMAL(5,2) NULL,
        cZahlungsreferenz          NVARCHAR(100) NULL,        -- Verwendungszweck
        cBankverbindung            NVARCHAR(500) NULL,        -- IBAN/BIC Lieferant
        dSkontoFrist               DATE NULL,
        nGeprueft                  TINYINT DEFAULT 0,
        kPrueferBenutzer           INT NULL,
        dGeprueft                  DATETIME NULL,
        cPruefHinweis              NVARCHAR(500) NULL,
        nFreigegeben               TINYINT DEFAULT 0,
        kFreigabeBenutzer          INT NULL,
        dFreigegeben               DATETIME NULL,
        cDokumentPfad              NVARCHAR(500) NULL,        -- Pfad zur PDF/Scan
        CONSTRAINT FK_EREweitert_ER FOREIGN KEY (kEingangsrechnung) REFERENCES tEingangsrechnung(kEingangsrechnung)
    );
    CREATE INDEX IX_ERErweitert_ER ON NOVVIA.EingangsrechnungErweitert(kEingangsrechnung);
    PRINT 'Tabelle NOVVIA.EingangsrechnungErweitert erstellt';
END
GO

PRINT '';
PRINT '============================================';
PRINT 'NOVVIA Einkauf-Tabellen erfolgreich erstellt!';
PRINT '============================================';
GO

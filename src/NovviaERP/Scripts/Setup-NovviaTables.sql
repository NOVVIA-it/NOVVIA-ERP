-- =====================================================
-- NOVVIA ERP - EIGENE TABELLEN
-- NUR Tabellen die JTL-Wawi NICHT hat!
-- Präfix: NOVVIA. zur Unterscheidung
-- =====================================================
-- WICHTIG: Diese Tabellen ergänzen JTL-Wawi,
-- sie ersetzen KEINE existierenden JTL-Tabellen!
-- =====================================================

USE [Mandant_1];
GO

-- Schema erstellen falls nicht vorhanden
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'NOVVIA')
    EXEC('CREATE SCHEMA NOVVIA');
GO

-- =====================================================
-- IMPORT-VORLAGEN (JTL hat das nicht)
-- =====================================================
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
GO

-- =====================================================
-- IMPORT-LOG
-- =====================================================
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
GO

-- =====================================================
-- WORKER-STATUS (Hintergrund-Jobs)
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'NOVVIA.tWorkerStatus'))
CREATE TABLE NOVVIA.tWorkerStatus (
    kWorkerStatus INT IDENTITY(1,1) PRIMARY KEY,
    cWorker NVARCHAR(100) NOT NULL UNIQUE,
    nStatus INT DEFAULT 0,  -- 0=Gestoppt, 1=Läuft, 2=Wartet, 3=Fehler
    dLetzterLauf DATETIME,
    nLaufzeit_ms INT,
    cLetzteFehler NVARCHAR(MAX),
    nAktiv BIT DEFAULT 1,
    cKonfigJson NVARCHAR(MAX)
);
GO

-- Standard-Worker einfügen
IF NOT EXISTS (SELECT * FROM NOVVIA.tWorkerStatus WHERE cWorker = 'Zahlungsabgleich')
BEGIN
    INSERT INTO NOVVIA.tWorkerStatus (cWorker, nAktiv) VALUES 
        ('Zahlungsabgleich', 1),
        ('WooCommerce-Sync', 1),
        ('Mahnlauf', 1),
        ('Lagerbestand-Prüfung', 1),
        ('USt-ID-Validierung', 1),
        ('Workflow-Queue', 1),
        ('Cleanup', 1);
END
GO

-- =====================================================
-- WORKER-LOG
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'NOVVIA.tWorkerLog'))
CREATE TABLE NOVVIA.tWorkerLog (
    kWorkerLog INT IDENTITY(1,1) PRIMARY KEY,
    cWorker NVARCHAR(100) NOT NULL,
    cLevel NVARCHAR(20) NOT NULL DEFAULT 'INFO',
    cNachricht NVARCHAR(MAX),
    dZeitpunkt DATETIME DEFAULT GETDATE()
);
GO

-- Index für schnelle Log-Abfragen
CREATE NONCLUSTERED INDEX IX_WorkerLog_Worker_Zeit 
ON NOVVIA.tWorkerLog(cWorker, dZeitpunkt DESC);
GO

-- =====================================================
-- AUSGABE-LOG (Druck/Mail/PDF Protokollierung)
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'NOVVIA.tAusgabeLog'))
CREATE TABLE NOVVIA.tAusgabeLog (
    kAusgabeLog INT IDENTITY(1,1) PRIMARY KEY,
    cDokumentTyp NVARCHAR(50) NOT NULL,  -- Rechnung, Lieferschein, Angebot, etc.
    kDokument INT NOT NULL,               -- ID aus JTL-Tabelle
    cDokumentNr NVARCHAR(50),
    cAktionen NVARCHAR(200),              -- Drucken,Speichern,Mail
    nGedruckt BIT DEFAULT 0,
    nGespeichert BIT DEFAULT 0,
    nMailGesendet BIT DEFAULT 0,
    nVorschau BIT DEFAULT 0,
    cFehler NVARCHAR(MAX),
    dZeitpunkt DATETIME DEFAULT GETDATE(),
    kBenutzer INT                         -- FK zu tBenutzer
);
GO

-- =====================================================
-- DOKUMENT-ARCHIV (PDF-Speicherung)
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'NOVVIA.tDokumentArchiv'))
CREATE TABLE NOVVIA.tDokumentArchiv (
    kDokumentArchiv INT IDENTITY(1,1) PRIMARY KEY,
    cDokumentTyp NVARCHAR(50) NOT NULL,
    kDokument INT NOT NULL,
    cDokumentNr NVARCHAR(50),
    cPfad NVARCHAR(500),                  -- Dateipfad wenn auf Disk
    bPdfDaten VARBINARY(MAX),             -- Oder direkt in DB
    nGroesse INT,
    dArchiviert DATETIME DEFAULT GETDATE()
);
GO

-- Unique Index: Pro Dokument nur ein Archiveintrag
CREATE UNIQUE INDEX IX_DokumentArchiv_Unique 
ON NOVVIA.tDokumentArchiv(cDokumentTyp, kDokument);
GO

-- =====================================================
-- PLATTFORM-SYNC-LOG (Erweitert JTL Shop-Sync)
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'NOVVIA.tPlattformSyncLog'))
CREATE TABLE NOVVIA.tPlattformSyncLog (
    kPlattformSyncLog INT IDENTITY(1,1) PRIMARY KEY,
    kShop INT NOT NULL,                   -- FK zu tShop
    cAktion NVARCHAR(50) NOT NULL,        -- Import, Export, Bestand, Preis
    nAnzahl INT,
    cDetails NVARCHAR(MAX),
    nErfolg BIT DEFAULT 1,
    cFehler NVARCHAR(MAX),
    dZeitpunkt DATETIME DEFAULT GETDATE()
);
GO

-- =====================================================
-- ARTIKEL-SHOP-BESCHREIBUNG (Falls JTL das nicht hat)
-- Separate Beschreibungen pro Shop
-- =====================================================
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'NOVVIA.tArtikelShopBeschreibung'))
CREATE TABLE NOVVIA.tArtikelShopBeschreibung (
    kArtikelShopBeschreibung INT IDENTITY(1,1) PRIMARY KEY,
    kArtikel INT NOT NULL,                -- FK zu tArtikel
    kShop INT NOT NULL,                   -- FK zu tShop
    cName NVARCHAR(200),
    cBeschreibung NVARCHAR(MAX),          -- Plain Text
    cBeschreibungHtml NVARCHAR(MAX),      -- HTML
    cKurztext NVARCHAR(500),
    fPreis DECIMAL(10,2),                 -- Shop-spezifischer Preis
    nAktiv BIT DEFAULT 1,
    dAktualisiert DATETIME,
    CONSTRAINT UK_ArtikelShop UNIQUE (kArtikel, kShop)
);
GO

-- =====================================================
-- CLEANUP: Alte Logs automatisch löschen (Job)
-- =====================================================
-- Worker-Logs älter als 30 Tage löschen
-- Sync-Logs älter als 90 Tage löschen
-- (Als SQL Agent Job oder in Worker implementieren)

PRINT '=====================================================';
PRINT 'NOVVIA-Tabellen erfolgreich erstellt!';
PRINT 'Schema: NOVVIA';
PRINT 'Tabellen: tImportVorlage, tImportLog, tWorkerStatus,';
PRINT '          tWorkerLog, tAusgabeLog, tDokumentArchiv,';
PRINT '          tPlattformSyncLog, tArtikelShopBeschreibung';
PRINT '=====================================================';
GO

-- WooCommerce Shop Tabelle fuer NovviaERP
-- Diese Tabelle speichert die Konfiguration fuer WooCommerce Shop-Anbindungen

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tWooCommerceShop')
BEGIN
    CREATE TABLE tWooCommerceShop (
        kWooCommerceShop INT IDENTITY(1,1) PRIMARY KEY,
        cName NVARCHAR(100) NOT NULL,
        cUrl NVARCHAR(500) NOT NULL,
        cConsumerKey NVARCHAR(200) NOT NULL,
        cConsumerSecret NVARCHAR(200) NOT NULL,
        cWebhookSecret NVARCHAR(100) NULL,
        cWebhookCallbackUrl NVARCHAR(500) NULL,
        nAktiv BIT NOT NULL DEFAULT 1,
        nWebhooksAktiv BIT NOT NULL DEFAULT 0,
        nSyncIntervallMinuten INT NOT NULL DEFAULT 15,
        dLetzterSync DATETIME NULL,
        kStandardWarenlager INT NULL,
        kStandardZahlungsart INT NULL,
        kStandardVersandart INT NULL,
        dErstellt DATETIME NOT NULL DEFAULT GETDATE(),
        dGeaendert DATETIME NULL
    );

    PRINT 'Tabelle tWooCommerceShop erstellt.';
END
ELSE
BEGIN
    -- Spalten hinzufuegen falls sie fehlen
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tWooCommerceShop') AND name = 'cWebhookSecret')
        ALTER TABLE tWooCommerceShop ADD cWebhookSecret NVARCHAR(100) NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tWooCommerceShop') AND name = 'cWebhookCallbackUrl')
        ALTER TABLE tWooCommerceShop ADD cWebhookCallbackUrl NVARCHAR(500) NULL;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tWooCommerceShop') AND name = 'nWebhooksAktiv')
        ALTER TABLE tWooCommerceShop ADD nWebhooksAktiv BIT NOT NULL DEFAULT 0;

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('tWooCommerceShop') AND name = 'nSyncIntervallMinuten')
        ALTER TABLE tWooCommerceShop ADD nSyncIntervallMinuten INT NOT NULL DEFAULT 15;

    PRINT 'Tabelle tWooCommerceShop existiert bereits. Fehlende Spalten wurden hinzugefuegt.';
END;

-- Artikel-WooCommerce Verknuepfungstabelle
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tArtikelWooCommerce')
BEGIN
    CREATE TABLE tArtikelWooCommerce (
        kArtikelWooCommerce INT IDENTITY(1,1) PRIMARY KEY,
        kArtikel INT NOT NULL,
        kWooCommerceShop INT NOT NULL,
        nWooCommerceProductId INT NOT NULL,
        dLetzterSync DATETIME NULL,
        dErstellt DATETIME NOT NULL DEFAULT GETDATE(),

        CONSTRAINT FK_ArtikelWC_Artikel FOREIGN KEY (kArtikel) REFERENCES tArtikel(kArtikel),
        CONSTRAINT FK_ArtikelWC_Shop FOREIGN KEY (kWooCommerceShop) REFERENCES tWooCommerceShop(kWooCommerceShop),
        CONSTRAINT UQ_ArtikelWC UNIQUE (kArtikel, kWooCommerceShop)
    );

    CREATE INDEX IX_ArtikelWC_Shop ON tArtikelWooCommerce(kWooCommerceShop);
    CREATE INDEX IX_ArtikelWC_Artikel ON tArtikelWooCommerce(kArtikel);

    PRINT 'Tabelle tArtikelWooCommerce erstellt.';
END;

-- WooCommerce Webhook Log Tabelle
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tWooCommerceWebhookLog')
BEGIN
    CREATE TABLE tWooCommerceWebhookLog (
        kLog INT IDENTITY(1,1) PRIMARY KEY,
        kWooCommerceShop INT NOT NULL,
        cTopic NVARCHAR(100) NOT NULL,
        cPayload NVARCHAR(MAX) NULL,
        cAktion NVARCHAR(500) NULL,
        nSuccess BIT NOT NULL DEFAULT 0,
        cFehler NVARCHAR(MAX) NULL,
        dErstellt DATETIME NOT NULL DEFAULT GETDATE(),

        CONSTRAINT FK_WCLog_Shop FOREIGN KEY (kWooCommerceShop) REFERENCES tWooCommerceShop(kWooCommerceShop)
    );

    CREATE INDEX IX_WCLog_Shop ON tWooCommerceWebhookLog(kWooCommerceShop);
    CREATE INDEX IX_WCLog_Datum ON tWooCommerceWebhookLog(dErstellt DESC);

    PRINT 'Tabelle tWooCommerceWebhookLog erstellt.';
END;

PRINT 'WooCommerce Tabellen-Setup abgeschlossen.';

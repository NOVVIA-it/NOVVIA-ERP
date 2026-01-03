-- ABData Pharma-Datenbank Setup
-- Erstellt die benötigten Tabellen und Stored Procedures für ABData-Integration

-- Schema prüfen
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'NOVVIA')
    EXEC('CREATE SCHEMA NOVVIA');
GO

-- ABData Artikel-Tabelle
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ABdataArtikel' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    CREATE TABLE NOVVIA.ABdataArtikel (
        kABdataArtikel INT IDENTITY(1,1) PRIMARY KEY,
        cPZN NVARCHAR(8) NOT NULL,
        cName NVARCHAR(500) NOT NULL,
        cHersteller NVARCHAR(200),
        cDarreichungsform NVARCHAR(100),
        cPackungsgroesse NVARCHAR(50),
        fMenge DECIMAL(18,4),
        cEinheit NVARCHAR(20),
        fAEP DECIMAL(18,4),  -- Apothekeneinkaufspreis (brutto)
        fAVP DECIMAL(18,4),  -- Apothekenverkaufspreis (brutto)
        fAEK DECIMAL(18,4),  -- Apothekeneinkaufspreis (netto)
        nRezeptpflicht BIT DEFAULT 0,
        nBTM BIT DEFAULT 0,
        nKuehlpflichtig BIT DEFAULT 0,
        cATC NVARCHAR(20),
        cWirkstoff NVARCHAR(500),
        cWirkstaerke NVARCHAR(100),
        dGueltigAb DATETIME,
        dGueltigBis DATETIME,
        dImportiert DATETIME DEFAULT GETDATE(),
        dAktualisiert DATETIME DEFAULT GETDATE(),

        CONSTRAINT UQ_ABdataArtikel_PZN UNIQUE (cPZN)
    );

    CREATE INDEX IX_ABdataArtikel_Name ON NOVVIA.ABdataArtikel (cName);
    CREATE INDEX IX_ABdataArtikel_Hersteller ON NOVVIA.ABdataArtikel (cHersteller);
    CREATE INDEX IX_ABdataArtikel_ATC ON NOVVIA.ABdataArtikel (cATC);
    CREATE INDEX IX_ABdataArtikel_Wirkstoff ON NOVVIA.ABdataArtikel (cWirkstoff);

    PRINT 'Tabelle NOVVIA.ABdataArtikel erstellt';
END
GO

-- ABData Import-Log
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ABdataImportLog' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    CREATE TABLE NOVVIA.ABdataImportLog (
        kABdataImportLog INT IDENTITY(1,1) PRIMARY KEY,
        cDateiname NVARCHAR(500),
        dImportStart DATETIME DEFAULT GETDATE(),
        dImportEnde DATETIME,
        nAnzahlGesamt INT DEFAULT 0,
        nAnzahlNeu INT DEFAULT 0,
        nAnzahlAktualisiert INT DEFAULT 0,
        nAnzahlFehler INT DEFAULT 0,
        cStatus NVARCHAR(50),
        cFehlerDetails NVARCHAR(MAX)
    );

    PRINT 'Tabelle NOVVIA.ABdataImportLog erstellt';
END
GO

-- ABData Mapping zu JTL-Artikeln
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ABdataArtikelMapping' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    CREATE TABLE NOVVIA.ABdataArtikelMapping (
        kABdataArtikelMapping INT IDENTITY(1,1) PRIMARY KEY,
        kArtikel INT NOT NULL,  -- JTL Artikel-ID
        cPZN NVARCHAR(8) NOT NULL,
        nAutomatisch BIT DEFAULT 0,
        dErstellt DATETIME DEFAULT GETDATE(),

        CONSTRAINT UQ_ABdataMapping UNIQUE (kArtikel, cPZN)
    );

    CREATE INDEX IX_ABdataMapping_Artikel ON NOVVIA.ABdataArtikelMapping (kArtikel);
    CREATE INDEX IX_ABdataMapping_PZN ON NOVVIA.ABdataArtikelMapping (cPZN);

    PRINT 'Tabelle NOVVIA.ABdataArtikelMapping erstellt';
END
GO

-- Upsert Stored Procedure
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spNOVVIA_ABdataArtikelUpsert')
    DROP PROCEDURE spNOVVIA_ABdataArtikelUpsert;
GO

CREATE PROCEDURE spNOVVIA_ABdataArtikelUpsert
    @cPZN NVARCHAR(8),
    @cName NVARCHAR(500),
    @cHersteller NVARCHAR(200) = NULL,
    @cDarreichungsform NVARCHAR(100) = NULL,
    @cPackungsgroesse NVARCHAR(50) = NULL,
    @fMenge DECIMAL(18,4) = NULL,
    @cEinheit NVARCHAR(20) = NULL,
    @fAEP DECIMAL(18,4) = NULL,
    @fAVP DECIMAL(18,4) = NULL,
    @fAEK DECIMAL(18,4) = NULL,
    @nRezeptpflicht BIT = 0,
    @nBTM BIT = 0,
    @nKuehlpflichtig BIT = 0,
    @cATC NVARCHAR(20) = NULL,
    @cWirkstoff NVARCHAR(500) = NULL,
    @dGueltigAb DATETIME = NULL,
    @dGueltigBis DATETIME = NULL,
    @nIsNew BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM NOVVIA.ABdataArtikel WHERE cPZN = @cPZN)
    BEGIN
        UPDATE NOVVIA.ABdataArtikel SET
            cName = @cName,
            cHersteller = ISNULL(@cHersteller, cHersteller),
            cDarreichungsform = ISNULL(@cDarreichungsform, cDarreichungsform),
            cPackungsgroesse = ISNULL(@cPackungsgroesse, cPackungsgroesse),
            fMenge = ISNULL(@fMenge, fMenge),
            cEinheit = ISNULL(@cEinheit, cEinheit),
            fAEP = ISNULL(@fAEP, fAEP),
            fAVP = ISNULL(@fAVP, fAVP),
            fAEK = ISNULL(@fAEK, fAEK),
            nRezeptpflicht = @nRezeptpflicht,
            nBTM = @nBTM,
            nKuehlpflichtig = @nKuehlpflichtig,
            cATC = ISNULL(@cATC, cATC),
            cWirkstoff = ISNULL(@cWirkstoff, cWirkstoff),
            dGueltigAb = ISNULL(@dGueltigAb, dGueltigAb),
            dGueltigBis = ISNULL(@dGueltigBis, dGueltigBis),
            dAktualisiert = GETDATE()
        WHERE cPZN = @cPZN;

        SET @nIsNew = 0;
    END
    ELSE
    BEGIN
        INSERT INTO NOVVIA.ABdataArtikel (
            cPZN, cName, cHersteller, cDarreichungsform, cPackungsgroesse,
            fMenge, cEinheit, fAEP, fAVP, fAEK,
            nRezeptpflicht, nBTM, nKuehlpflichtig, cATC, cWirkstoff,
            dGueltigAb, dGueltigBis
        ) VALUES (
            @cPZN, @cName, @cHersteller, @cDarreichungsform, @cPackungsgroesse,
            @fMenge, @cEinheit, @fAEP, @fAVP, @fAEK,
            @nRezeptpflicht, @nBTM, @nKuehlpflichtig, @cATC, @cWirkstoff,
            @dGueltigAb, @dGueltigBis
        );

        SET @nIsNew = 1;
    END
END
GO

PRINT 'Stored Procedure spNOVVIA_ABdataArtikelUpsert erstellt';
GO

-- Auto-Mapping Stored Procedure
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spNOVVIA_ABdataAutoMapping')
    DROP PROCEDURE spNOVVIA_ABdataAutoMapping;
GO

CREATE PROCEDURE spNOVVIA_ABdataAutoMapping
    @nAnzahlGemappt INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Mapping basierend auf ArtNr = PZN
    INSERT INTO NOVVIA.ABdataArtikelMapping (kArtikel, cPZN, nAutomatisch)
    SELECT a.kArtikel, ab.cPZN, 1
    FROM Artikel.tArtikel a
    INNER JOIN NOVVIA.ABdataArtikel ab ON ab.cPZN = a.cArtNr
    WHERE NOT EXISTS (
        SELECT 1 FROM NOVVIA.ABdataArtikelMapping m
        WHERE m.kArtikel = a.kArtikel AND m.cPZN = ab.cPZN
    );

    SET @nAnzahlGemappt = @@ROWCOUNT;
END
GO

PRINT 'Stored Procedure spNOVVIA_ABdataAutoMapping erstellt';
GO

PRINT '';
PRINT '=== ABData Setup abgeschlossen ===';

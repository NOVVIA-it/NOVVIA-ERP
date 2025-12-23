-- =============================================
-- NOVVIA ERP - Artikel und Kunde Erweiterung
-- Validierungsfelder: Ambient, Cool, Medcan, Tierarznei
-- Qualifikation: QualifiziertAm, QualifiziertVon, GDP, GMP
-- =============================================

-- Schema pruefen
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'NOVVIA')
BEGIN
    EXEC('CREATE SCHEMA NOVVIA');
    PRINT 'Schema NOVVIA erstellt';
END
GO

-- =============================================
-- ARTIKEL ERWEITERUNG
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ArtikelErweitert' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    CREATE TABLE NOVVIA.ArtikelErweitert (
        kArtikelErweitert       INT IDENTITY(1,1) PRIMARY KEY,
        kArtikel                INT NOT NULL,           -- FK zu tArtikel
        -- Validierung
        nAmbient                BIT DEFAULT 0,
        nCool                   BIT DEFAULT 0,
        nMedcan                 BIT DEFAULT 0,
        nTierarznei             BIT DEFAULT 0,
        -- Qualifikation
        dQualifiziertAm         DATE NULL,
        cQualifiziertVon        NVARCHAR(100) NULL,
        cGDP                    NVARCHAR(200) NULL,
        cGMP                    NVARCHAR(200) NULL,
        -- Meta
        dErstellt               DATETIME2 DEFAULT GETDATE(),
        dGeaendert              DATETIME2 DEFAULT GETDATE()
    );
    CREATE UNIQUE INDEX IX_ArtikelErweitert_Artikel ON NOVVIA.ArtikelErweitert(kArtikel);
    PRINT 'Tabelle NOVVIA.ArtikelErweitert erstellt';
END
ELSE
BEGIN
    PRINT 'Tabelle NOVVIA.ArtikelErweitert existiert bereits';
END
GO

-- =============================================
-- KUNDE ERWEITERUNG
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'KundeErweitert' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    CREATE TABLE NOVVIA.KundeErweitert (
        kKundeErweitert         INT IDENTITY(1,1) PRIMARY KEY,
        kKunde                  INT NOT NULL,           -- FK zu tKunde
        -- Validierung
        nAmbient                BIT DEFAULT 0,
        nCool                   BIT DEFAULT 0,
        nMedcan                 BIT DEFAULT 0,
        nTierarznei             BIT DEFAULT 0,
        -- Qualifikation
        dQualifiziertAm         DATE NULL,
        cQualifiziertVon        NVARCHAR(100) NULL,
        cGDP                    NVARCHAR(200) NULL,
        cGMP                    NVARCHAR(200) NULL,
        -- Meta
        dErstellt               DATETIME2 DEFAULT GETDATE(),
        dGeaendert              DATETIME2 DEFAULT GETDATE()
    );
    CREATE UNIQUE INDEX IX_KundeErweitert_Kunde ON NOVVIA.KundeErweitert(kKunde);
    PRINT 'Tabelle NOVVIA.KundeErweitert erstellt';
END
ELSE
BEGIN
    PRINT 'Tabelle NOVVIA.KundeErweitert existiert bereits';
END
GO

-- =============================================
-- STORED PROCEDURES - ARTIKEL
-- =============================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spNOVVIA_ArtikelErweitertLaden')
    DROP PROCEDURE spNOVVIA_ArtikelErweitertLaden;
GO

CREATE PROCEDURE spNOVVIA_ArtikelErweitertLaden
    @kArtikel INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        kArtikelErweitert,
        kArtikel,
        ISNULL(nAmbient, 0) AS nAmbient,
        ISNULL(nCool, 0) AS nCool,
        ISNULL(nMedcan, 0) AS nMedcan,
        ISNULL(nTierarznei, 0) AS nTierarznei,
        dQualifiziertAm,
        cQualifiziertVon,
        cGDP,
        cGMP
    FROM NOVVIA.ArtikelErweitert
    WHERE kArtikel = @kArtikel;
END
GO
PRINT 'SP spNOVVIA_ArtikelErweitertLaden erstellt';
GO

-- Speichern
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spNOVVIA_ArtikelErweitertSpeichern')
    DROP PROCEDURE spNOVVIA_ArtikelErweitertSpeichern;
GO

CREATE PROCEDURE spNOVVIA_ArtikelErweitertSpeichern
    @kArtikel INT,
    @nAmbient BIT,
    @nCool BIT,
    @nMedcan BIT,
    @nTierarznei BIT,
    @dQualifiziertAm DATE = NULL,
    @cQualifiziertVon NVARCHAR(100) = NULL,
    @cGDP NVARCHAR(200) = NULL,
    @cGMP NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM NOVVIA.ArtikelErweitert WHERE kArtikel = @kArtikel)
    BEGIN
        UPDATE NOVVIA.ArtikelErweitert
        SET nAmbient = @nAmbient,
            nCool = @nCool,
            nMedcan = @nMedcan,
            nTierarznei = @nTierarznei,
            dQualifiziertAm = @dQualifiziertAm,
            cQualifiziertVon = @cQualifiziertVon,
            cGDP = @cGDP,
            cGMP = @cGMP,
            dGeaendert = GETDATE()
        WHERE kArtikel = @kArtikel;
    END
    ELSE
    BEGIN
        INSERT INTO NOVVIA.ArtikelErweitert (kArtikel, nAmbient, nCool, nMedcan, nTierarznei, dQualifiziertAm, cQualifiziertVon, cGDP, cGMP)
        VALUES (@kArtikel, @nAmbient, @nCool, @nMedcan, @nTierarznei, @dQualifiziertAm, @cQualifiziertVon, @cGDP, @cGMP);
    END
END
GO
PRINT 'SP spNOVVIA_ArtikelErweitertSpeichern erstellt';
GO

-- =============================================
-- STORED PROCEDURES - KUNDE
-- =============================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spNOVVIA_KundeErweitertLaden')
    DROP PROCEDURE spNOVVIA_KundeErweitertLaden;
GO

CREATE PROCEDURE spNOVVIA_KundeErweitertLaden
    @kKunde INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        kKundeErweitert,
        kKunde,
        ISNULL(nAmbient, 0) AS nAmbient,
        ISNULL(nCool, 0) AS nCool,
        ISNULL(nMedcan, 0) AS nMedcan,
        ISNULL(nTierarznei, 0) AS nTierarznei,
        dQualifiziertAm,
        cQualifiziertVon,
        cGDP,
        cGMP
    FROM NOVVIA.KundeErweitert
    WHERE kKunde = @kKunde;
END
GO
PRINT 'SP spNOVVIA_KundeErweitertLaden erstellt';
GO

-- Speichern
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spNOVVIA_KundeErweitertSpeichern')
    DROP PROCEDURE spNOVVIA_KundeErweitertSpeichern;
GO

CREATE PROCEDURE spNOVVIA_KundeErweitertSpeichern
    @kKunde INT,
    @nAmbient BIT,
    @nCool BIT,
    @nMedcan BIT,
    @nTierarznei BIT,
    @dQualifiziertAm DATE = NULL,
    @cQualifiziertVon NVARCHAR(100) = NULL,
    @cGDP NVARCHAR(200) = NULL,
    @cGMP NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM NOVVIA.KundeErweitert WHERE kKunde = @kKunde)
    BEGIN
        UPDATE NOVVIA.KundeErweitert
        SET nAmbient = @nAmbient,
            nCool = @nCool,
            nMedcan = @nMedcan,
            nTierarznei = @nTierarznei,
            dQualifiziertAm = @dQualifiziertAm,
            cQualifiziertVon = @cQualifiziertVon,
            cGDP = @cGDP,
            cGMP = @cGMP,
            dGeaendert = GETDATE()
        WHERE kKunde = @kKunde;
    END
    ELSE
    BEGIN
        INSERT INTO NOVVIA.KundeErweitert (kKunde, nAmbient, nCool, nMedcan, nTierarznei, dQualifiziertAm, cQualifiziertVon, cGDP, cGMP)
        VALUES (@kKunde, @nAmbient, @nCool, @nMedcan, @nTierarznei, @dQualifiziertAm, @cQualifiziertVon, @cGDP, @cGMP);
    END
END
GO
PRINT 'SP spNOVVIA_KundeErweitertSpeichern erstellt';
GO

PRINT '';
PRINT '===========================================';
PRINT 'NOVVIA Artikel/Kunde Erweiterung eingerichtet!';
PRINT '===========================================';

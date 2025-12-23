-- ============================================
-- NOVVIA ERP - Lieferant Erweiterung
-- Zusätzliche Stammdatenfelder für Lieferanten
-- ============================================

USE Mandant_2;
GO

-- Schema erstellen falls nicht vorhanden
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'NOVVIA')
BEGIN
    EXEC('CREATE SCHEMA NOVVIA');
END
GO

-- ============================================
-- Lieferant Erweiterung (NOVVIA-spezifisch)
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LieferantErweitert' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    CREATE TABLE NOVVIA.LieferantErweitert (
        kLieferantErweitert     INT IDENTITY(1,1) PRIMARY KEY,
        kLieferant              INT NOT NULL UNIQUE,              -- FK zu tLieferant (1:1)

        -- Produktkategorien
        nAmbient                BIT DEFAULT 0,                    -- Liefert Ambient-Produkte
        nCool                   BIT DEFAULT 0,                    -- Liefert Kühlware
        nMedcan                 BIT DEFAULT 0,                    -- Liefert Medizinal-Cannabis
        nTierarznei             BIT DEFAULT 0,                    -- Liefert Tierarzneimittel

        -- Qualifizierung
        dQualifiziertAm         DATE NULL,                        -- Datum der Qualifizierung
        cQualifiziertVon        NVARCHAR(200) NULL,               -- Wer hat qualifiziert
        cQualifikationsDocs     NVARCHAR(500) NULL,               -- Pfad/Referenz zu Dokumenten
        cGDP                    NVARCHAR(200) NULL,               -- Good Distribution Practice
        cGMP                    NVARCHAR(200) NULL,               -- Good Manufacturing Practice

        -- Timestamps
        dErstellt               DATETIME DEFAULT GETDATE(),
        dGeaendert              DATETIME NULL,

        CONSTRAINT FK_LieferantErweitert_Lieferant FOREIGN KEY (kLieferant) REFERENCES tLieferant(kLieferant)
    );
    CREATE INDEX IX_LieferantErweitert_Lieferant ON NOVVIA.LieferantErweitert(kLieferant);
    PRINT 'Tabelle NOVVIA.LieferantErweitert erstellt';
END
ELSE
BEGIN
    PRINT 'Tabelle NOVVIA.LieferantErweitert existiert bereits';

    -- GDP und GMP Spalten hinzufügen falls nicht vorhanden
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('NOVVIA.LieferantErweitert') AND name = 'cGDP')
    BEGIN
        ALTER TABLE NOVVIA.LieferantErweitert ADD cGDP NVARCHAR(200) NULL;
        PRINT '  - Spalte cGDP hinzugefuegt';
    END

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('NOVVIA.LieferantErweitert') AND name = 'cGMP')
    BEGIN
        ALTER TABLE NOVVIA.LieferantErweitert ADD cGMP NVARCHAR(200) NULL;
        PRINT '  - Spalte cGMP hinzugefuegt';
    END
END
GO

-- ============================================
-- Stored Procedure: Lieferant Erweiterung speichern
-- ============================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spNOVVIA_LieferantErweitertSpeichern')
    DROP PROCEDURE spNOVVIA_LieferantErweitertSpeichern;
GO

CREATE PROCEDURE spNOVVIA_LieferantErweitertSpeichern
    @kLieferant INT,
    @nAmbient BIT = 0,
    @nCool BIT = 0,
    @nMedcan BIT = 0,
    @nTierarznei BIT = 0,
    @dQualifiziertAm DATE = NULL,
    @cQualifiziertVon NVARCHAR(200) = NULL,
    @cQualifikationsDocs NVARCHAR(500) = NULL,
    @cGDP NVARCHAR(200) = NULL,
    @cGMP NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM NOVVIA.LieferantErweitert WHERE kLieferant = @kLieferant)
    BEGIN
        -- Update
        UPDATE NOVVIA.LieferantErweitert
        SET nAmbient = @nAmbient,
            nCool = @nCool,
            nMedcan = @nMedcan,
            nTierarznei = @nTierarznei,
            dQualifiziertAm = @dQualifiziertAm,
            cQualifiziertVon = @cQualifiziertVon,
            cQualifikationsDocs = @cQualifikationsDocs,
            cGDP = @cGDP,
            cGMP = @cGMP,
            dGeaendert = GETDATE()
        WHERE kLieferant = @kLieferant;
    END
    ELSE
    BEGIN
        -- Insert
        INSERT INTO NOVVIA.LieferantErweitert (
            kLieferant, nAmbient, nCool, nMedcan, nTierarznei,
            dQualifiziertAm, cQualifiziertVon, cQualifikationsDocs, cGDP, cGMP
        )
        VALUES (
            @kLieferant, @nAmbient, @nCool, @nMedcan, @nTierarznei,
            @dQualifiziertAm, @cQualifiziertVon, @cQualifikationsDocs, @cGDP, @cGMP
        );
    END
END
GO

PRINT 'Stored Procedure spNOVVIA_LieferantErweitertSpeichern erstellt';
GO

-- ============================================
-- Stored Procedure: Lieferant Erweiterung laden
-- ============================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spNOVVIA_LieferantErweitertLaden')
    DROP PROCEDURE spNOVVIA_LieferantErweitertLaden;
GO

CREATE PROCEDURE spNOVVIA_LieferantErweitertLaden
    @kLieferant INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        kLieferantErweitert,
        kLieferant,
        ISNULL(nAmbient, 0) AS nAmbient,
        ISNULL(nCool, 0) AS nCool,
        ISNULL(nMedcan, 0) AS nMedcan,
        ISNULL(nTierarznei, 0) AS nTierarznei,
        dQualifiziertAm,
        cQualifiziertVon,
        cQualifikationsDocs,
        cGDP,
        cGMP,
        dErstellt,
        dGeaendert
    FROM NOVVIA.LieferantErweitert
    WHERE kLieferant = @kLieferant;
END
GO

PRINT 'Stored Procedure spNOVVIA_LieferantErweitertLaden erstellt';
GO

PRINT '';
PRINT '============================================';
PRINT 'NOVVIA Lieferant Erweiterung eingerichtet!';
PRINT '============================================';
GO

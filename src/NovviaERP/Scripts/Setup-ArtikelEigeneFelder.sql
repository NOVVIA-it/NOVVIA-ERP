-- =====================================================================
-- NOVVIA.ArtikelEigeneFelder - Eigene Felder für Artikel
-- =====================================================================

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'NOVVIA')
BEGIN
    EXEC('CREATE SCHEMA NOVVIA');
    PRINT 'Schema NOVVIA erstellt';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ArtikelEigeneFelder' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    CREATE TABLE NOVVIA.ArtikelEigeneFelder (
        kArtikelEigenesFeld  INT IDENTITY(1,1) NOT NULL,
        kArtikel             INT           NOT NULL,
        cKey                 NVARCHAR(100) NOT NULL,
        cValue               NVARCHAR(MAX) NULL,
        dErstellt            DATETIME2(0)  NOT NULL DEFAULT GETDATE(),
        dGeaendert           DATETIME2(0)  NULL,
        CONSTRAINT PK_ArtikelEigeneFelder PRIMARY KEY CLUSTERED (kArtikelEigenesFeld),
        CONSTRAINT UQ_ArtikelEigeneFelder_Key UNIQUE (kArtikel, cKey)
    );

    CREATE NONCLUSTERED INDEX IX_ArtikelEigeneFelder_Artikel ON NOVVIA.ArtikelEigeneFelder (kArtikel);
    CREATE NONCLUSTERED INDEX IX_ArtikelEigeneFelder_Key ON NOVVIA.ArtikelEigeneFelder (cKey);

    PRINT 'Tabelle NOVVIA.ArtikelEigeneFelder erstellt';
END
ELSE
    PRINT 'Tabelle NOVVIA.ArtikelEigeneFelder existiert bereits';
GO

-- =====================================================================
-- NOVVIA Artikel Eigene Felder - JTL Native Tabellen
-- Verwendet: tAttribut, tAttributSprache, tArtikelAttribut, tArtikelAttributSprache
-- =====================================================================

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'NOVVIA')
BEGIN
    EXEC('CREATE SCHEMA NOVVIA');
    PRINT 'Schema NOVVIA erstellt';
END
GO

-- TVP Type erstellen/neu erstellen
IF EXISTS (SELECT 1 FROM sys.types WHERE name = 'TYPE_ArtikelEigenesFeldAnpassen' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    -- Pruefen ob der Type in Verwendung ist (SP droppen falls noetig)
    IF EXISTS (SELECT 1 FROM sys.procedures WHERE name = 'spArtikelEigenesFeldCreateOrUpdate' AND schema_id = SCHEMA_ID('NOVVIA'))
    BEGIN
        DROP PROCEDURE NOVVIA.spArtikelEigenesFeldCreateOrUpdate;
        PRINT 'SP NOVVIA.spArtikelEigenesFeldCreateOrUpdate geloescht';
    END
    DROP TYPE NOVVIA.TYPE_ArtikelEigenesFeldAnpassen;
    PRINT 'TVP Type NOVVIA.TYPE_ArtikelEigenesFeldAnpassen geloescht';
END
GO

CREATE TYPE NOVVIA.TYPE_ArtikelEigenesFeldAnpassen AS TABLE
(
    kArtikel INT           NOT NULL,
    cKey     NVARCHAR(255) NOT NULL,
    cValue   NVARCHAR(MAX) NULL
);
GO
PRINT 'TVP Type NOVVIA.TYPE_ArtikelEigenesFeldAnpassen erstellt (kArtikel, cKey, cValue)';
GO

-- Stored Procedure erstellen
CREATE OR ALTER PROCEDURE [NOVVIA].[spArtikelEigenesFeldCreateOrUpdate]
(
    @ArtikelEigenesFeldAnpassen NOVVIA.TYPE_ArtikelEigenesFeldAnpassen READONLY,
    @nAutoCreateAttribute BIT = 0  -- Nicht mehr verwendet - Attribute muessen in JTL existieren
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM @ArtikelEigenesFeldAnpassen)
        RETURN;

    BEGIN TRY
        BEGIN TRAN;

        -- Temp-Tabelle fuer aufgeloeste Daten
        IF OBJECT_ID('tempdb..#Input') IS NOT NULL DROP TABLE #Input;
        CREATE TABLE #Input
        (
            kArtikel     INT           NOT NULL,
            cKey         NVARCHAR(255) NOT NULL,
            cValue       NVARCHAR(MAX) NULL,
            kAttribut    INT           NULL
        );

        INSERT INTO #Input (kArtikel, cKey, cValue)
        SELECT kArtikel, LTRIM(RTRIM(cKey)), cValue
        FROM @ArtikelEigenesFeldAnpassen;

        -- Attribut-IDs aufloesen (aus tAttributSprache mit kSprache = 0)
        UPDATE i
        SET i.kAttribut = s.kAttribut
        FROM #Input i
        INNER JOIN dbo.tAttributSprache s ON s.cName = i.cKey AND s.kSprache = 0;

        -- Eintraege ohne gefundenes Attribut entfernen (Attribut muss in JTL existieren)
        DELETE FROM #Input WHERE kAttribut IS NULL;

        IF NOT EXISTS (SELECT 1 FROM #Input)
        BEGIN
            COMMIT TRAN;
            RETURN;
        END

        -- ArtikelAttribut-Verknuepfungen erstellen falls nicht vorhanden
        INSERT INTO dbo.tArtikelAttribut (kArtikel, kAttribut)
        SELECT DISTINCT i.kArtikel, i.kAttribut
        FROM #Input i
        WHERE NOT EXISTS (
            SELECT 1 FROM dbo.tArtikelAttribut aa
            WHERE aa.kArtikel = i.kArtikel AND aa.kAttribut = i.kAttribut
        );

        -- kArtikelAttribut aufloesen
        IF OBJECT_ID('tempdb..#Resolved') IS NOT NULL DROP TABLE #Resolved;
        CREATE TABLE #Resolved
        (
            kArtikelAttribut INT NOT NULL,
            cValue           NVARCHAR(MAX) NULL
        );

        INSERT INTO #Resolved (kArtikelAttribut, cValue)
        SELECT aa.kArtikelAttribut, i.cValue
        FROM #Input i
        INNER JOIN dbo.tArtikelAttribut aa ON aa.kArtikel = i.kArtikel AND aa.kAttribut = i.kAttribut;

        -- Werte in tArtikelAttributSprache upserten (kSprache = 0)
        -- nWertInt fuer Integer/Boolean (0/1), cWertVarchar fuer Text
        UPDATE aas
        SET aas.nWertInt = TRY_CAST(r.cValue AS INT),
            aas.cWertVarchar = CASE WHEN TRY_CAST(r.cValue AS INT) IS NULL THEN r.cValue ELSE NULL END
        FROM dbo.tArtikelAttributSprache aas
        INNER JOIN #Resolved r ON r.kArtikelAttribut = aas.kArtikelAttribut
        WHERE aas.kSprache = 0;

        -- Insert neue
        INSERT INTO dbo.tArtikelAttributSprache (kArtikelAttribut, kSprache, nWertInt, cWertVarchar)
        SELECT r.kArtikelAttribut, 0,
               TRY_CAST(r.cValue AS INT),
               CASE WHEN TRY_CAST(r.cValue AS INT) IS NULL THEN r.cValue ELSE NULL END
        FROM #Resolved r
        WHERE NOT EXISTS (
            SELECT 1 FROM dbo.tArtikelAttributSprache aas
            WHERE aas.kArtikelAttribut = r.kArtikelAttribut AND aas.kSprache = 0
        );

        COMMIT TRAN;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRAN;

        DECLARE @ErrorMessage NVARCHAR(MAX) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();

        RAISERROR (@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END
GO

PRINT 'SP NOVVIA.spArtikelEigenesFeldCreateOrUpdate erstellt';
PRINT '';
PRINT '=== Setup abgeschlossen ===';
PRINT 'Verwendet JTL native Tabellen:';
PRINT '  - dbo.tAttribut';
PRINT '  - dbo.tAttributSprache';
PRINT '  - dbo.tArtikelAttribut';
PRINT '  - dbo.tArtikelAttributSprache';
GO

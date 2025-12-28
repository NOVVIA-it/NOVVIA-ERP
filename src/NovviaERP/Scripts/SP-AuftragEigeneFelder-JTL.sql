-- =====================================================================
-- NOVVIA Auftrag Eigene Felder - JTL Native Tabellen
-- Verwendet: tAttribut, tAttributSprache, Verkauf.tAuftragAttribut, Verkauf.tAuftragAttributSprache
-- =====================================================================

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'NOVVIA')
BEGIN
    EXEC('CREATE SCHEMA NOVVIA');
    PRINT 'Schema NOVVIA erstellt';
END
GO

-- TVP Type erstellen/neu erstellen
IF EXISTS (SELECT 1 FROM sys.types WHERE name = 'TYPE_AuftragEigenesFeldAnpassen' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    -- Pruefen ob der Type in Verwendung ist (SP droppen falls noetig)
    IF EXISTS (SELECT 1 FROM sys.procedures WHERE name = 'spAuftragEigenesFeldCreateOrUpdate' AND schema_id = SCHEMA_ID('NOVVIA'))
    BEGIN
        DROP PROCEDURE NOVVIA.spAuftragEigenesFeldCreateOrUpdate;
        PRINT 'SP NOVVIA.spAuftragEigenesFeldCreateOrUpdate geloescht';
    END
    DROP TYPE NOVVIA.TYPE_AuftragEigenesFeldAnpassen;
    PRINT 'TVP Type NOVVIA.TYPE_AuftragEigenesFeldAnpassen geloescht';
END
GO

CREATE TYPE NOVVIA.TYPE_AuftragEigenesFeldAnpassen AS TABLE
(
    kAuftrag INT           NOT NULL,
    cKey     NVARCHAR(255) NOT NULL,
    cValue   NVARCHAR(MAX) NULL
);
GO
PRINT 'TVP Type NOVVIA.TYPE_AuftragEigenesFeldAnpassen erstellt (kAuftrag, cKey, cValue)';
GO

-- Stored Procedure erstellen
CREATE OR ALTER PROCEDURE [NOVVIA].[spAuftragEigenesFeldCreateOrUpdate]
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

        -- Temp-Tabelle fuer aufgeloeste Daten
        IF OBJECT_ID('tempdb..#Input') IS NOT NULL DROP TABLE #Input;
        CREATE TABLE #Input
        (
            kAuftrag     INT           NOT NULL,
            cKey         NVARCHAR(255) NOT NULL,
            cValue       NVARCHAR(MAX) NULL,
            kAttribut    INT           NULL
        );

        INSERT INTO #Input (kAuftrag, cKey, cValue)
        SELECT kAuftrag, LTRIM(RTRIM(cKey)), cValue
        FROM @AuftragEigenesFeldAnpassen;

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

        -- AuftragAttribut-Verknuepfungen erstellen falls nicht vorhanden
        INSERT INTO Verkauf.tAuftragAttribut (kAuftrag, kAttribut)
        SELECT DISTINCT i.kAuftrag, i.kAttribut
        FROM #Input i
        WHERE NOT EXISTS (
            SELECT 1 FROM Verkauf.tAuftragAttribut aa
            WHERE aa.kAuftrag = i.kAuftrag AND aa.kAttribut = i.kAttribut
        );

        -- kAuftragAttribut aufloesen
        IF OBJECT_ID('tempdb..#Resolved') IS NOT NULL DROP TABLE #Resolved;
        CREATE TABLE #Resolved
        (
            kAuftragAttribut INT NOT NULL,
            cValue           NVARCHAR(MAX) NULL
        );

        INSERT INTO #Resolved (kAuftragAttribut, cValue)
        SELECT aa.kAuftragAttribut, i.cValue
        FROM #Input i
        INNER JOIN Verkauf.tAuftragAttribut aa ON aa.kAuftrag = i.kAuftrag AND aa.kAttribut = i.kAttribut;

        -- Werte in tAuftragAttributSprache upserten (kSprache = 0)
        -- nWertInt fuer Integer/Boolean (0/1), cWertVarchar fuer Text, fWertDecimal fuer Dezimal
        UPDATE aas
        SET aas.nWertInt = CASE WHEN TRY_CAST(r.cValue AS DECIMAL(18,4)) IS NOT NULL AND CHARINDEX('.', r.cValue) = 0 AND CHARINDEX(',', r.cValue) = 0 THEN TRY_CAST(r.cValue AS INT) ELSE NULL END,
            aas.fWertDecimal = CASE WHEN TRY_CAST(REPLACE(r.cValue, ',', '.') AS DECIMAL(18,4)) IS NOT NULL AND (CHARINDEX('.', r.cValue) > 0 OR CHARINDEX(',', r.cValue) > 0) THEN TRY_CAST(REPLACE(r.cValue, ',', '.') AS DECIMAL(18,4)) ELSE NULL END,
            aas.cWertVarchar = CASE WHEN TRY_CAST(r.cValue AS DECIMAL(18,4)) IS NULL AND TRY_CAST(REPLACE(r.cValue, ',', '.') AS DECIMAL(18,4)) IS NULL THEN r.cValue ELSE NULL END
        FROM Verkauf.tAuftragAttributSprache aas
        INNER JOIN #Resolved r ON r.kAuftragAttribut = aas.kAuftragAttribut
        WHERE aas.kSprache = 0;

        -- Insert neue
        INSERT INTO Verkauf.tAuftragAttributSprache (kAuftragAttribut, kSprache, nWertInt, fWertDecimal, cWertVarchar)
        SELECT r.kAuftragAttribut, 0,
               CASE WHEN TRY_CAST(r.cValue AS DECIMAL(18,4)) IS NOT NULL AND CHARINDEX('.', r.cValue) = 0 AND CHARINDEX(',', r.cValue) = 0 THEN TRY_CAST(r.cValue AS INT) ELSE NULL END,
               CASE WHEN TRY_CAST(REPLACE(r.cValue, ',', '.') AS DECIMAL(18,4)) IS NOT NULL AND (CHARINDEX('.', r.cValue) > 0 OR CHARINDEX(',', r.cValue) > 0) THEN TRY_CAST(REPLACE(r.cValue, ',', '.') AS DECIMAL(18,4)) ELSE NULL END,
               CASE WHEN TRY_CAST(r.cValue AS DECIMAL(18,4)) IS NULL AND TRY_CAST(REPLACE(r.cValue, ',', '.') AS DECIMAL(18,4)) IS NULL THEN r.cValue ELSE NULL END
        FROM #Resolved r
        WHERE NOT EXISTS (
            SELECT 1 FROM Verkauf.tAuftragAttributSprache aas
            WHERE aas.kAuftragAttribut = r.kAuftragAttribut AND aas.kSprache = 0
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

PRINT 'SP NOVVIA.spAuftragEigenesFeldCreateOrUpdate erstellt';
PRINT '';
PRINT '=== Setup abgeschlossen ===';
PRINT 'Verwendet JTL native Tabellen:';
PRINT '  - dbo.tAttribut';
PRINT '  - dbo.tAttributSprache';
PRINT '  - Verkauf.tAuftragAttribut';
PRINT '  - Verkauf.tAuftragAttributSprache';
GO

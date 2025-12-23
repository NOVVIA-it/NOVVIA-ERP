-- =====================================================================
-- NOVVIA Artikel Eigene Felder - Fix/Setup
-- Fuehren Sie dieses Script auf allen Mandanten aus
-- =====================================================================

-- 1. Schema erstellen falls nicht vorhanden
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'NOVVIA')
BEGIN
    EXEC('CREATE SCHEMA NOVVIA');
    PRINT 'Schema NOVVIA erstellt';
END
GO

-- 2. Tabelle tEigenesFeld erstellen falls nicht vorhanden
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'tEigenesFeld' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.tEigenesFeld (
        kEigenesFeld       INT IDENTITY(1,1) NOT NULL,
        cBereich           NVARCHAR(50)  NOT NULL,
        cName              NVARCHAR(255) NOT NULL,
        cIntName           NVARCHAR(255) NULL,
        cTyp               NVARCHAR(50)  NULL,
        cWerte             NVARCHAR(MAX) NULL,
        cStandardwert      NVARCHAR(MAX) NULL,
        nPflichtfeld       BIT           NOT NULL DEFAULT 0,
        nSichtbarInListe   BIT           NOT NULL DEFAULT 1,
        nSichtbarImDruck   BIT           NOT NULL DEFAULT 0,
        nSortierung        INT           NOT NULL DEFAULT 1000,
        nAktiv             BIT           NOT NULL DEFAULT 1,
        cValidierung       NVARCHAR(MAX) NULL,
        cHinweis           NVARCHAR(MAX) NULL,
        CONSTRAINT PK_tEigenesFeld PRIMARY KEY CLUSTERED (kEigenesFeld)
    );

    CREATE NONCLUSTERED INDEX IX_tEigenesFeld_Bereich ON dbo.tEigenesFeld (cBereich, cName);
    PRINT 'Tabelle dbo.tEigenesFeld erstellt';
END
ELSE
    PRINT 'Tabelle dbo.tEigenesFeld existiert bereits';
GO

-- 3. Tabelle tEigenesFeldWert erstellen falls nicht vorhanden
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'tEigenesFeldWert' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.tEigenesFeldWert (
        kEigenesFeldWert   INT IDENTITY(1,1) NOT NULL,
        kEigenesFeld       INT           NOT NULL,
        kKey               INT           NOT NULL,  -- kArtikel, kKunde, etc.
        cWert              NVARCHAR(MAX) NULL,
        dGeaendert         DATETIME2(0)  NULL,
        CONSTRAINT PK_tEigenesFeldWert PRIMARY KEY CLUSTERED (kEigenesFeldWert),
        CONSTRAINT UQ_tEigenesFeldWert UNIQUE (kEigenesFeld, kKey)
    );

    CREATE NONCLUSTERED INDEX IX_tEigenesFeldWert_Key ON dbo.tEigenesFeldWert (kKey);
    CREATE NONCLUSTERED INDEX IX_tEigenesFeldWert_Feld ON dbo.tEigenesFeldWert (kEigenesFeld);
    PRINT 'Tabelle dbo.tEigenesFeldWert erstellt';
END
ELSE
    PRINT 'Tabelle dbo.tEigenesFeldWert existiert bereits';
GO

-- 4. TVP Type erstellen/neu erstellen
IF EXISTS (SELECT 1 FROM sys.types WHERE name = 'TYPE_ArtikelEigenesFeldAnpassen' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    -- Pruefen ob der Type in Verwendung ist
    IF NOT EXISTS (SELECT 1 FROM sys.dm_exec_cached_plans cp
                   CROSS APPLY sys.dm_exec_sql_text(cp.plan_handle) st
                   WHERE st.text LIKE '%TYPE_ArtikelEigenesFeldAnpassen%')
    BEGIN
        DROP TYPE NOVVIA.TYPE_ArtikelEigenesFeldAnpassen;
        PRINT 'TVP Type NOVVIA.TYPE_ArtikelEigenesFeldAnpassen geloescht';
    END
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.types WHERE name = 'TYPE_ArtikelEigenesFeldAnpassen' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    CREATE TYPE NOVVIA.TYPE_ArtikelEigenesFeldAnpassen AS TABLE
    (
        cArtNr   NVARCHAR(50)  NOT NULL,
        cKey     NVARCHAR(255) NOT NULL,
        cValue   NVARCHAR(MAX) NULL
    );
    PRINT 'TVP Type NOVVIA.TYPE_ArtikelEigenesFeldAnpassen erstellt';
END
ELSE
    PRINT 'TVP Type NOVVIA.TYPE_ArtikelEigenesFeldAnpassen existiert bereits';
GO

-- 5. Stored Procedure erstellen/aktualisieren
CREATE OR ALTER PROCEDURE [NOVVIA].[spArtikelEigenesFeldCreateOrUpdate]
(
    @ArtikelEigenesFeldAnpassen NOVVIA.TYPE_ArtikelEigenesFeldAnpassen READONLY,
    @nFeldTyp INT = 1,
    @nAutoCreateField BIT = 1,
    @cBereich NVARCHAR(50) = N'Artikel'
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM @ArtikelEigenesFeldAnpassen)
        RETURN;

    BEGIN TRY
        BEGIN TRAN;

        -- A) Input normalisieren + kArtikel aufloesen
        IF OBJECT_ID('tempdb..#Input') IS NOT NULL DROP TABLE #Input;
        CREATE TABLE #Input
        (
            cArtNr   NVARCHAR(50)  NOT NULL,
            cKey     NVARCHAR(255) NOT NULL,
            cValue   NVARCHAR(MAX) NULL,
            kArtikel INT           NULL
        );

        INSERT INTO #Input (cArtNr, cKey, cValue)
        SELECT
            LTRIM(RTRIM(a.cArtNr)),
            LTRIM(RTRIM(a.cKey)),
            a.cValue
        FROM @ArtikelEigenesFeldAnpassen a;

        UPDATE i
        SET i.kArtikel = art.kArtikel
        FROM #Input i
        JOIN dbo.tArtikel art WITH (NOLOCK) ON art.cArtNr = i.cArtNr;

        -- Fehlende Artikelnummern ignorieren (kein Fehler, nur Warning im Log)
        DELETE FROM #Input WHERE kArtikel IS NULL;

        IF NOT EXISTS (SELECT 1 FROM #Input)
        BEGIN
            COMMIT TRAN;
            RETURN;
        END

        -- B) Felddefinitionen sicherstellen
        IF @nAutoCreateField = 1
        BEGIN
            INSERT INTO dbo.tEigenesFeld
            (
                cBereich, cName, cIntName, cTyp, cWerte, cStandardwert,
                nPflichtfeld, nSichtbarInListe, nSichtbarImDruck,
                nSortierung, nAktiv, cValidierung, cHinweis
            )
            SELECT DISTINCT
                @cBereich,
                i.cKey,
                i.cKey,
                CAST(@nFeldTyp AS NVARCHAR(50)),
                NULL,
                NULL,
                0, 1, 0,
                1000, 1,
                NULL,
                N'Auto-created by NOVVIA'
            FROM #Input i
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM dbo.tEigenesFeld ef WITH (NOLOCK)
                WHERE ef.cBereich = @cBereich
                  AND (ef.cIntName = i.cKey OR ef.cName = i.cKey)
            );
        END

        -- C) Werte upserten
        IF OBJECT_ID('tempdb..#Resolved') IS NOT NULL DROP TABLE #Resolved;
        CREATE TABLE #Resolved
        (
            kEigenesFeld INT NOT NULL,
            kArtikel     INT NOT NULL,
            cValue       NVARCHAR(MAX) NULL
        );

        INSERT INTO #Resolved (kEigenesFeld, kArtikel, cValue)
        SELECT
            ef.kEigenesFeld,
            i.kArtikel,
            i.cValue
        FROM #Input i
        JOIN dbo.tEigenesFeld ef WITH (NOLOCK)
          ON ef.cBereich = @cBereich
         AND (ef.cIntName = i.cKey OR ef.cName = i.cKey);

        -- Update existierende
        UPDATE w
        SET
            w.cWert = r.cValue,
            w.dGeaendert = GETDATE()
        FROM dbo.tEigenesFeldWert w
        JOIN #Resolved r
          ON r.kEigenesFeld = w.kEigenesFeld
         AND r.kArtikel     = w.kKey;

        -- Insert neue
        INSERT INTO dbo.tEigenesFeldWert (kEigenesFeld, kKey, cWert, dGeaendert)
        SELECT
            r.kEigenesFeld,
            r.kArtikel,
            r.cValue,
            GETDATE()
        FROM #Resolved r
        WHERE NOT EXISTS
        (
            SELECT 1
            FROM dbo.tEigenesFeldWert w WITH (NOLOCK)
            WHERE w.kEigenesFeld = r.kEigenesFeld
              AND w.kKey         = r.kArtikel
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

PRINT 'SP NOVVIA.spArtikelEigenesFeldCreateOrUpdate erstellt/aktualisiert';
GO

-- 6. Test
PRINT '';
PRINT '=== Setup abgeschlossen ===';
PRINT 'Tabellen: dbo.tEigenesFeld, dbo.tEigenesFeldWert';
PRINT 'TVP Type: NOVVIA.TYPE_ArtikelEigenesFeldAnpassen';
PRINT 'SP: NOVVIA.spArtikelEigenesFeldCreateOrUpdate';
GO

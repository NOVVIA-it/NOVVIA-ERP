-- =====================================================================
-- NOVVIA MSV3 Bestand Cache
-- TTL-basierter Cache für MSV3 Bestandsabfragen (5 Minuten)
-- =====================================================================

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'NOVVIA')
BEGIN
    EXEC('CREATE SCHEMA NOVVIA');
    PRINT 'Schema NOVVIA erstellt';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MSV3BestandCache' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    CREATE TABLE NOVVIA.MSV3BestandCache (
        kMSV3BestandCache  INT IDENTITY(1,1) NOT NULL,
        cPzn               NVARCHAR(50)  NOT NULL,
        kLieferant         INT           NOT NULL,
        nBestand           INT           NULL,
        nVerfuegbar        INT           NULL,  -- 0 = nicht verfügbar, 1 = verfügbar
        cStatus            NVARCHAR(100) NULL,  -- MSV3 Lieferstatus
        dAbfrage           DATETIME2(0)  NOT NULL DEFAULT GETDATE(),
        CONSTRAINT PK_MSV3BestandCache PRIMARY KEY CLUSTERED (kMSV3BestandCache),
        CONSTRAINT UQ_MSV3BestandCache_PznLief UNIQUE (cPzn, kLieferant)
    );

    CREATE NONCLUSTERED INDEX IX_MSV3BestandCache_Pzn ON NOVVIA.MSV3BestandCache (cPzn);
    CREATE NONCLUSTERED INDEX IX_MSV3BestandCache_Abfrage ON NOVVIA.MSV3BestandCache (dAbfrage);

    PRINT 'Tabelle NOVVIA.MSV3BestandCache erstellt';
END
ELSE
    PRINT 'Tabelle NOVVIA.MSV3BestandCache existiert bereits';
GO

-- Stored Procedure: Cache abfragen (mit TTL)
CREATE OR ALTER PROCEDURE NOVVIA.spMSV3BestandCache_Get
(
    @cPzn NVARCHAR(50),
    @kLieferant INT,
    @nTtlMinuten INT = 5
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT nBestand, nVerfuegbar, cStatus, dAbfrage
    FROM NOVVIA.MSV3BestandCache
    WHERE cPzn = @cPzn
      AND kLieferant = @kLieferant
      AND dAbfrage > DATEADD(MINUTE, -@nTtlMinuten, GETDATE());
END
GO

-- Stored Procedure: Cache upsert
CREATE OR ALTER PROCEDURE NOVVIA.spMSV3BestandCache_Upsert
(
    @cPzn NVARCHAR(50),
    @kLieferant INT,
    @nBestand INT = NULL,
    @nVerfuegbar INT = NULL,
    @cStatus NVARCHAR(100) = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM NOVVIA.MSV3BestandCache WHERE cPzn = @cPzn AND kLieferant = @kLieferant)
    BEGIN
        UPDATE NOVVIA.MSV3BestandCache
        SET nBestand = @nBestand,
            nVerfuegbar = @nVerfuegbar,
            cStatus = @cStatus,
            dAbfrage = GETDATE()
        WHERE cPzn = @cPzn AND kLieferant = @kLieferant;
    END
    ELSE
    BEGIN
        INSERT INTO NOVVIA.MSV3BestandCache (cPzn, kLieferant, nBestand, nVerfuegbar, cStatus, dAbfrage)
        VALUES (@cPzn, @kLieferant, @nBestand, @nVerfuegbar, @cStatus, GETDATE());
    END
END
GO

-- Stored Procedure: Alte Cache-Einträge löschen (Cleanup)
CREATE OR ALTER PROCEDURE NOVVIA.spMSV3BestandCache_Cleanup
(
    @nMaxAlterStunden INT = 24
)
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM NOVVIA.MSV3BestandCache
    WHERE dAbfrage < DATEADD(HOUR, -@nMaxAlterStunden, GETDATE());

    SELECT @@ROWCOUNT AS GeloeschteEintraege;
END
GO

PRINT '';
PRINT '=== MSV3 Bestand Cache Setup abgeschlossen ===';
PRINT 'Tabelle: NOVVIA.MSV3BestandCache';
PRINT 'SPs: spMSV3BestandCache_Get, spMSV3BestandCache_Upsert, spMSV3BestandCache_Cleanup';
GO

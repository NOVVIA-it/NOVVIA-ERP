-- =====================================================================
-- MSV3 Verfuegbarkeits-Cache Tabellen und Stored Procedures
-- Fuer JTL-konforme Speicherung von Grosshandel-Verfuegbarkeitsdaten
--
-- Status-Codes:
--   SOFORT_LIEFERBAR       - Vollstaendige Menge sofort verfuegbar
--   TEILLIEFERBAR          - Nur Teil der Menge verfuegbar
--   NACHLIEFERUNG_MOEGLICH - Aktuell nicht, aber spaeter lieferbar
--   NICHT_LIEFERBAR        - Nicht lieferbar (Hersteller-Engpass etc.)
--   UNBEKANNT              - Parsing-Fehler, Timeout, etc.
-- =====================================================================

-- USE [eazybusiness]  -- Auskommentiert - bitte manuell die richtige DB auswaehlen
-- GO

-- =====================================================================
-- Schema NOVVIA erstellen (falls nicht vorhanden)
-- =====================================================================
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'NOVVIA')
BEGIN
    EXEC('CREATE SCHEMA NOVVIA');
    PRINT 'Schema NOVVIA erstellt';
END
ELSE
    PRINT 'Schema NOVVIA existiert bereits';
GO

-- =====================================================================
-- Cache-Tabelle fuer Verfuegbarkeitsabfragen
-- =====================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MSV3VerfuegbarkeitCache' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    CREATE TABLE NOVVIA.MSV3VerfuegbarkeitCache (
        kMSV3VerfuegbarkeitCache INT IDENTITY(1,1) NOT NULL,
        cPZN                     VARCHAR(16)   NOT NULL,
        kLieferant               INT           NOT NULL,
        nRequestedQty            INT           NOT NULL DEFAULT 1,
        cStatusCode              VARCHAR(40)   NOT NULL,
        nAvailableQty            INT           NULL,
        cReasonCode              VARCHAR(80)   NULL,
        dNextDeliveryUtc         DATETIME2(0)  NULL,
        cRawTyp                  VARCHAR(120)  NULL,
        cRawXml                  NVARCHAR(MAX) NULL,
        dLastCheckedUtc          DATETIME2(0)  NOT NULL,
        dValidUntilUtc           DATETIME2(0)  NOT NULL,
        CONSTRAINT PK_MSV3VerfuegbarkeitCache PRIMARY KEY CLUSTERED (kMSV3VerfuegbarkeitCache),
        CONSTRAINT UQ_MSV3VerfuegbarkeitCache_PZN_Lieferant UNIQUE (cPZN, kLieferant)
    );

    CREATE NONCLUSTERED INDEX IX_MSV3VerfuegbarkeitCache_PZN ON NOVVIA.MSV3VerfuegbarkeitCache (cPZN);
    CREATE NONCLUSTERED INDEX IX_MSV3VerfuegbarkeitCache_ValidUntil ON NOVVIA.MSV3VerfuegbarkeitCache (dValidUntilUtc);

    PRINT 'Tabelle NOVVIA.MSV3VerfuegbarkeitCache erstellt';
END
ELSE
    PRINT 'Tabelle NOVVIA.MSV3VerfuegbarkeitCache existiert bereits';
GO

-- =====================================================================
-- Request-Log Tabelle (fuer Debugging und Support)
-- =====================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'MSV3RequestLog' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    CREATE TABLE NOVVIA.MSV3RequestLog (
        kMSV3RequestLog   INT IDENTITY(1,1) NOT NULL,
        dTimestamp        DATETIME2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
        kLieferant        INT           NULL,
        cEndpoint         VARCHAR(255)  NOT NULL,
        cAction           VARCHAR(50)   NOT NULL,
        nHttpStatus       INT           NULL,
        nSoapFault        BIT           NOT NULL DEFAULT 0,
        cCorrelationId    UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
        cRequestBody      NVARCHAR(MAX) NULL,
        cResponseBody     NVARCHAR(MAX) NULL,
        cErrorMessage     NVARCHAR(1000) NULL,
        nDurationMs       INT           NULL,
        CONSTRAINT PK_MSV3RequestLog PRIMARY KEY CLUSTERED (kMSV3RequestLog)
    );

    CREATE NONCLUSTERED INDEX IX_MSV3RequestLog_Timestamp ON NOVVIA.MSV3RequestLog (dTimestamp DESC);
    CREATE NONCLUSTERED INDEX IX_MSV3RequestLog_Lieferant ON NOVVIA.MSV3RequestLog (kLieferant, dTimestamp DESC);

    PRINT 'Tabelle NOVVIA.MSV3RequestLog erstellt';
END
ELSE
    PRINT 'Tabelle NOVVIA.MSV3RequestLog existiert bereits';
GO

-- =====================================================================
-- SP: Cache Upsert (JTL-konform: Speichern nur ueber SP)
-- =====================================================================
CREATE OR ALTER PROCEDURE NOVVIA.spMSV3VerfuegbarkeitCache_Upsert
    @cPZN             VARCHAR(16),
    @kLieferant       INT,
    @nRequestedQty    INT = 1,
    @cStatusCode      VARCHAR(40),
    @nAvailableQty    INT = NULL,
    @cReasonCode      VARCHAR(80) = NULL,
    @dNextDeliveryUtc DATETIME2(0) = NULL,
    @cRawTyp          VARCHAR(120) = NULL,
    @cRawXml          NVARCHAR(MAX) = NULL,
    @nTtlMinutes      INT = 10
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @now DATETIME2(0) = SYSUTCDATETIME();
    DECLARE @valid DATETIME2(0) = DATEADD(MINUTE, @nTtlMinutes, @now);

    MERGE NOVVIA.MSV3VerfuegbarkeitCache AS tgt
    USING (SELECT @cPZN AS cPZN, @kLieferant AS kLieferant) AS src
    ON tgt.cPZN = src.cPZN AND tgt.kLieferant = src.kLieferant
    WHEN MATCHED THEN
        UPDATE SET
            nRequestedQty    = @nRequestedQty,
            cStatusCode      = @cStatusCode,
            nAvailableQty    = @nAvailableQty,
            cReasonCode      = @cReasonCode,
            dNextDeliveryUtc = @dNextDeliveryUtc,
            cRawTyp          = @cRawTyp,
            cRawXml          = @cRawXml,
            dLastCheckedUtc  = @now,
            dValidUntilUtc   = @valid
    WHEN NOT MATCHED THEN
        INSERT (cPZN, kLieferant, nRequestedQty, cStatusCode, nAvailableQty, cReasonCode,
                dNextDeliveryUtc, cRawTyp, cRawXml, dLastCheckedUtc, dValidUntilUtc)
        VALUES (@cPZN, @kLieferant, @nRequestedQty, @cStatusCode, @nAvailableQty, @cReasonCode,
                @dNextDeliveryUtc, @cRawTyp, @cRawXml, @now, @valid);
END;
GO

PRINT 'SP NOVVIA.spMSV3VerfuegbarkeitCache_Upsert erstellt';
GO

-- =====================================================================
-- SP: Cache Get mit TTL-Pruefung
-- =====================================================================
CREATE OR ALTER PROCEDURE NOVVIA.spMSV3VerfuegbarkeitCache_Get
    @cPZN       VARCHAR(16),
    @kLieferant INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1
        kMSV3VerfuegbarkeitCache,
        cPZN,
        kLieferant,
        nRequestedQty,
        cStatusCode,
        nAvailableQty,
        cReasonCode,
        dNextDeliveryUtc,
        cRawTyp,
        dLastCheckedUtc,
        dValidUntilUtc,
        CASE WHEN dValidUntilUtc >= SYSUTCDATETIME() THEN 1 ELSE 0 END AS nIsValid
    FROM NOVVIA.MSV3VerfuegbarkeitCache
    WHERE cPZN = @cPZN
      AND (@kLieferant IS NULL OR kLieferant = @kLieferant)
    ORDER BY dLastCheckedUtc DESC;
END;
GO

PRINT 'SP NOVVIA.spMSV3VerfuegbarkeitCache_Get erstellt';
GO

-- =====================================================================
-- SP: Cache Bulk Get (mehrere PZNs auf einmal)
-- =====================================================================
CREATE OR ALTER PROCEDURE NOVVIA.spMSV3VerfuegbarkeitCache_GetBulk
    @cPZNList    VARCHAR(MAX),  -- Komma-getrennte Liste: '14036711,12345678,87654321'
    @kLieferant  INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH PZNs AS (
        SELECT TRIM(value) AS cPZN
        FROM STRING_SPLIT(@cPZNList, ',')
        WHERE TRIM(value) <> ''
    )
    SELECT
        c.cPZN,
        c.kLieferant,
        c.nRequestedQty,
        c.cStatusCode,
        c.nAvailableQty,
        c.cReasonCode,
        c.dNextDeliveryUtc,
        c.cRawTyp,
        c.dLastCheckedUtc,
        c.dValidUntilUtc,
        CASE WHEN c.dValidUntilUtc >= SYSUTCDATETIME() THEN 1 ELSE 0 END AS nIsValid
    FROM NOVVIA.MSV3VerfuegbarkeitCache c
    INNER JOIN PZNs p ON c.cPZN = p.cPZN
    WHERE (@kLieferant IS NULL OR c.kLieferant = @kLieferant);
END;
GO

PRINT 'SP NOVVIA.spMSV3VerfuegbarkeitCache_GetBulk erstellt';
GO

-- =====================================================================
-- SP: Request Log einfuegen
-- =====================================================================
CREATE OR ALTER PROCEDURE NOVVIA.spMSV3RequestLog_Insert
    @kLieferant       INT = NULL,
    @cEndpoint        VARCHAR(255),
    @cAction          VARCHAR(50),
    @nHttpStatus      INT = NULL,
    @nSoapFault       BIT = 0,
    @cRequestBody     NVARCHAR(MAX) = NULL,
    @cResponseBody    NVARCHAR(MAX) = NULL,
    @cErrorMessage    NVARCHAR(1000) = NULL,
    @nDurationMs      INT = NULL,
    @cCorrelationId   UNIQUEIDENTIFIER = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF @cCorrelationId IS NULL
        SET @cCorrelationId = NEWID();

    INSERT INTO NOVVIA.MSV3RequestLog
        (kLieferant, cEndpoint, cAction, nHttpStatus, nSoapFault, cCorrelationId,
         cRequestBody, cResponseBody, cErrorMessage, nDurationMs)
    VALUES
        (@kLieferant, @cEndpoint, @cAction, @nHttpStatus, @nSoapFault, @cCorrelationId,
         @cRequestBody, @cResponseBody, @cErrorMessage, @nDurationMs);
END;
GO

PRINT 'SP NOVVIA.spMSV3RequestLog_Insert erstellt';
GO

-- =====================================================================
-- SP: Alte Cache-Eintraege und Logs bereinigen
-- =====================================================================
CREATE OR ALTER PROCEDURE NOVVIA.spMSV3Cache_Cleanup
    @nCacheMaxAgeDays INT = 1,   -- Cache-Eintraege aelter als X Tage loeschen
    @nLogMaxAgeDays   INT = 30   -- Log-Eintraege aelter als X Tage loeschen
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @cacheDeleted INT, @logDeleted INT;

    -- Alte Cache-Eintraege loeschen
    DELETE FROM NOVVIA.MSV3VerfuegbarkeitCache
    WHERE dValidUntilUtc < DATEADD(DAY, -@nCacheMaxAgeDays, SYSUTCDATETIME());
    SET @cacheDeleted = @@ROWCOUNT;

    -- Alte Log-Eintraege loeschen
    DELETE FROM NOVVIA.MSV3RequestLog
    WHERE dTimestamp < DATEADD(DAY, -@nLogMaxAgeDays, SYSUTCDATETIME());
    SET @logDeleted = @@ROWCOUNT;

    SELECT @cacheDeleted AS CacheDeleted, @logDeleted AS LogDeleted;
END;
GO

PRINT 'SP NOVVIA.spMSV3Cache_Cleanup erstellt';
GO

-- =====================================================================
-- View: Aktuelle Verfuegbarkeit mit Artikeldaten
-- Hinweis: Falls tArtikel andere Spaltennamen hat, View anpassen
-- =====================================================================
CREATE OR ALTER VIEW NOVVIA.vMSV3VerfuegbarkeitAktuell
AS
SELECT
    c.cPZN,
    c.kLieferant,
    c.cStatusCode,
    c.nAvailableQty,
    c.nRequestedQty,
    c.cReasonCode,
    c.dNextDeliveryUtc,
    c.cRawTyp,
    c.dLastCheckedUtc,
    c.dValidUntilUtc,
    CASE WHEN c.dValidUntilUtc >= SYSUTCDATETIME() THEN 1 ELSE 0 END AS nIsValid,
    -- Status-Text fuer UI
    CASE c.cStatusCode
        WHEN 'SOFORT_LIEFERBAR' THEN 'Sofort lieferbar'
        WHEN 'TEILLIEFERBAR' THEN 'Teillieferung moeglich'
        WHEN 'NACHLIEFERUNG_MOEGLICH' THEN 'Nachlieferung moeglich'
        WHEN 'NICHT_LIEFERBAR' THEN 'Nicht lieferbar'
        ELSE 'Unbekannt'
    END AS cStatusText,
    -- Status-Farbe fuer UI (Hex)
    CASE c.cStatusCode
        WHEN 'SOFORT_LIEFERBAR' THEN '#00AA00'
        WHEN 'TEILLIEFERBAR' THEN '#FFAA00'
        WHEN 'NACHLIEFERUNG_MOEGLICH' THEN '#0066CC'
        WHEN 'NICHT_LIEFERBAR' THEN '#CC0000'
        ELSE '#888888'
    END AS cStatusFarbe,
    l.cFirma AS cLieferantName
FROM NOVVIA.MSV3VerfuegbarkeitCache c
LEFT JOIN tlieferant l ON c.kLieferant = l.kLieferant;
GO

PRINT 'View NOVVIA.vMSV3VerfuegbarkeitAktuell erstellt';
GO

PRINT '';
PRINT '=== MSV3 Verfuegbarkeits-Cache Setup abgeschlossen ===';
PRINT '';

-- ============================================
-- NOVVIA ERP - MSV3 Logging
-- Protokolliert alle MSV3-Anfragen und -Antworten
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
-- MSV3 Request/Response Log
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MSV3Log' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    CREATE TABLE NOVVIA.MSV3Log (
        kMSV3Log                BIGINT IDENTITY(1,1) PRIMARY KEY,
        kMSV3Lieferant          INT NOT NULL,                    -- FK zu NOVVIA.MSV3Lieferant
        kLieferantenBestellung  INT NULL,                        -- FK zu tLieferantenBestellung (optional)
        cAktion                 NVARCHAR(50) NOT NULL,           -- VerbindungTesten, VerfuegbarkeitAnfragen, Bestellen
        cRequestXML             NVARCHAR(MAX) NULL,              -- Gesendetes XML
        cResponseXML            NVARCHAR(MAX) NULL,              -- Empfangenes XML
        nHttpStatus             INT NULL,                        -- HTTP Status Code
        nErfolg                 BIT DEFAULT 0,                   -- 1=Erfolg, 0=Fehler
        cFehler                 NVARCHAR(1000) NULL,             -- Fehlermeldung
        cMSV3AuftragsId         NVARCHAR(100) NULL,              -- Auftrags-ID vom Großhandel
        cBestellSupportId       NVARCHAR(100) NULL,              -- Support-ID für Nachverfolgung
        nDauerMs                INT NULL,                        -- Dauer der Anfrage in ms
        dZeitpunkt              DATETIME DEFAULT GETDATE(),
        kBenutzer               INT NULL                         -- Wer hat die Anfrage ausgelöst
    );
    CREATE INDEX IX_MSV3Log_Lieferant ON NOVVIA.MSV3Log(kMSV3Lieferant);
    CREATE INDEX IX_MSV3Log_Bestellung ON NOVVIA.MSV3Log(kLieferantenBestellung);
    CREATE INDEX IX_MSV3Log_Zeitpunkt ON NOVVIA.MSV3Log(dZeitpunkt DESC);
    PRINT 'Tabelle NOVVIA.MSV3Log erstellt';
END
GO

-- ============================================
-- Stored Procedure: MSV3 Log schreiben
-- ============================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spNOVVIA_MSV3LogSchreiben')
    DROP PROCEDURE spNOVVIA_MSV3LogSchreiben;
GO

CREATE PROCEDURE spNOVVIA_MSV3LogSchreiben
    @kMSV3Lieferant INT,
    @kLieferantenBestellung INT = NULL,
    @cAktion NVARCHAR(50),
    @cRequestXML NVARCHAR(MAX) = NULL,
    @cResponseXML NVARCHAR(MAX) = NULL,
    @nHttpStatus INT = NULL,
    @nErfolg BIT = 0,
    @cFehler NVARCHAR(1000) = NULL,
    @cMSV3AuftragsId NVARCHAR(100) = NULL,
    @cBestellSupportId NVARCHAR(100) = NULL,
    @nDauerMs INT = NULL,
    @kBenutzer INT = NULL,
    @kMSV3Log BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO NOVVIA.MSV3Log (
        kMSV3Lieferant, kLieferantenBestellung, cAktion,
        cRequestXML, cResponseXML, nHttpStatus, nErfolg, cFehler,
        cMSV3AuftragsId, cBestellSupportId, nDauerMs, kBenutzer
    )
    VALUES (
        @kMSV3Lieferant, @kLieferantenBestellung, @cAktion,
        @cRequestXML, @cResponseXML, @nHttpStatus, @nErfolg, @cFehler,
        @cMSV3AuftragsId, @cBestellSupportId, @nDauerMs, @kBenutzer
    );

    SET @kMSV3Log = SCOPE_IDENTITY();
END
GO

PRINT 'Stored Procedure spNOVVIA_MSV3LogSchreiben erstellt';
GO

-- ============================================
-- View: Lieferanten für Artikel mit MSV3-Info
-- Zeigt alle Lieferanten pro Artikel die MSV3-fähig sind
-- ============================================
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vwArtikelMSV3Lieferanten' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP VIEW NOVVIA.vwArtikelMSV3Lieferanten;
GO

CREATE VIEW NOVVIA.vwArtikelMSV3Lieferanten
AS
SELECT
    la.tArtikel_kArtikel AS kArtikel,
    la.tLieferant_kLieferant AS kLieferant,
    l.cFirma AS LieferantName,
    l.cLiefNr AS LieferantNr,
    NULL AS LieferantenArtNr,
    ISNULL(la.fEKNetto, 0) AS EKNetto,
    0 AS Standardmenge,
    0 AS Prioritaet,
    ISNULL(la.nStandard, 0) AS IstStandard,
    m.kMSV3Lieferant,
    m.cMSV3Url AS MSV3Url,
    m.nMSV3Version AS MSV3Version,
    CASE WHEN m.kMSV3Lieferant IS NOT NULL AND m.nAktiv = 1 THEN 1 ELSE 0 END AS HatMSV3
FROM tLiefArtikel la
INNER JOIN tLieferant l ON la.tLieferant_kLieferant = l.kLieferant
LEFT JOIN NOVVIA.MSV3Lieferant m ON la.tLieferant_kLieferant = m.kLieferant AND m.nAktiv = 1
WHERE l.cAktiv = 'Y';
GO

PRINT 'View NOVVIA.vwArtikelMSV3Lieferanten erstellt';
GO

PRINT '';
PRINT '============================================';
PRINT 'NOVVIA MSV3 Logging erfolgreich eingerichtet!';
PRINT '============================================';
GO

-- =====================================================
-- NOVVIA Audit-Log System
-- =====================================================
-- Auswertbare Logs für:
-- 1. Stammdaten-Änderungen (Kunde, Artikel, Lieferant)
-- 2. Bewegungsdaten (Auftrag, Rechnung, Lieferschein)
-- 3. System-Events (Login, Fehler)
-- =====================================================

USE [Mandant_1]
GO

-- Schema sicherstellen
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'NOVVIA')
    EXEC('CREATE SCHEMA NOVVIA');
GO

-- =====================================================
-- Tabelle: NOVVIA.Log
-- Zentrale Log-Tabelle für alle Events
-- =====================================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'NOVVIA' AND TABLE_NAME = 'Log')
BEGIN
    CREATE TABLE NOVVIA.[Log] (
        kLog BIGINT IDENTITY(1,1) PRIMARY KEY,

        -- Event-Klassifizierung
        cKategorie NVARCHAR(50) NOT NULL,          -- Stammdaten, Bewegung, System
        cAktion NVARCHAR(100) NOT NULL,            -- Erstellt, Geaendert, Geloescht, etc.
        cModul NVARCHAR(50) NOT NULL,              -- Kunde, Artikel, Auftrag, Rechnung, etc.

        -- Betroffene Entität
        cEntityTyp NVARCHAR(50) NULL,              -- tKunde, tArtikel, tBestellung, etc.
        kEntity INT NULL,                           -- ID der betroffenen Entität
        cEntityNr NVARCHAR(100) NULL,              -- Kundennr, Artikelnr, Bestellnr, etc.

        -- Änderungsdetails
        cFeldname NVARCHAR(100) NULL,              -- Bei Feldänderung: Name des Felds
        cAlterWert NVARCHAR(MAX) NULL,             -- Alter Wert (bei Änderung)
        cNeuerWert NVARCHAR(MAX) NULL,             -- Neuer Wert (bei Änderung)

        -- Zusatzinformationen
        cBeschreibung NVARCHAR(MAX) NULL,          -- Beschreibung des Events
        cDetails NVARCHAR(MAX) NULL,               -- JSON mit zusätzlichen Details

        -- Beträge (für Bewegungsdaten)
        fBetragNetto DECIMAL(18,4) NULL,
        fBetragBrutto DECIMAL(18,4) NULL,

        -- Kontext
        kBenutzer INT NULL,                        -- NOVVIA Benutzer
        cBenutzerName NVARCHAR(100) NULL,
        cRechnername NVARCHAR(100) NULL,
        cIP NVARCHAR(50) NULL,

        -- Zeitstempel
        dZeitpunkt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),

        -- Für Suche/Filterung
        nSeverity INT NOT NULL DEFAULT 0,          -- 0=Info, 1=Warning, 2=Error
        nVerarbeitet BIT NOT NULL DEFAULT 0        -- Für Auswertungen die verarbeitet wurden
    );

    -- Indizes für schnelle Abfragen
    CREATE INDEX IX_Log_Zeitpunkt ON NOVVIA.[Log](dZeitpunkt DESC);
    CREATE INDEX IX_Log_Kategorie ON NOVVIA.[Log](cKategorie, dZeitpunkt DESC);
    CREATE INDEX IX_Log_Modul ON NOVVIA.[Log](cModul, dZeitpunkt DESC);
    CREATE INDEX IX_Log_Entity ON NOVVIA.[Log](cEntityTyp, kEntity);
    CREATE INDEX IX_Log_Benutzer ON NOVVIA.[Log](kBenutzer, dZeitpunkt DESC);

    PRINT 'Tabelle NOVVIA.Log erstellt.';
END
ELSE
    PRINT 'Tabelle NOVVIA.Log existiert bereits.';
GO

-- =====================================================
-- View: NOVVIA.vLogStammdaten
-- Nur Stammdaten-Änderungen
-- =====================================================
IF EXISTS (SELECT * FROM sys.views WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'vLogStammdaten')
    DROP VIEW NOVVIA.vLogStammdaten;
GO

CREATE VIEW NOVVIA.vLogStammdaten AS
SELECT kLog, cAktion, cModul, cEntityTyp, kEntity, cEntityNr,
       cFeldname, cAlterWert, cNeuerWert, cBeschreibung,
       cBenutzerName, dZeitpunkt
FROM NOVVIA.[Log]
WHERE cKategorie = 'Stammdaten';
GO

PRINT 'View NOVVIA.vLogStammdaten erstellt.';
GO

-- =====================================================
-- View: NOVVIA.vLogBewegung
-- Nur Bewegungsdaten (Aufträge, Rechnungen, etc.)
-- =====================================================
IF EXISTS (SELECT * FROM sys.views WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'vLogBewegung')
    DROP VIEW NOVVIA.vLogBewegung;
GO

CREATE VIEW NOVVIA.vLogBewegung AS
SELECT kLog, cAktion, cModul, cEntityTyp, kEntity, cEntityNr,
       cBeschreibung, fBetragNetto, fBetragBrutto,
       cBenutzerName, dZeitpunkt
FROM NOVVIA.[Log]
WHERE cKategorie = 'Bewegung';
GO

PRINT 'View NOVVIA.vLogBewegung erstellt.';
GO

-- =====================================================
-- View: NOVVIA.vLogFehler
-- Nur Fehler und Warnungen
-- =====================================================
IF EXISTS (SELECT * FROM sys.views WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'vLogFehler')
    DROP VIEW NOVVIA.vLogFehler;
GO

CREATE VIEW NOVVIA.vLogFehler AS
SELECT kLog, cKategorie, cAktion, cModul, cBeschreibung, cDetails,
       cBenutzerName, cRechnername, dZeitpunkt, nSeverity
FROM NOVVIA.[Log]
WHERE nSeverity > 0;
GO

PRINT 'View NOVVIA.vLogFehler erstellt.';
GO

-- =====================================================
-- SP: NOVVIA.spLogSchreiben
-- Zentraler Log-Eintrag
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spLogSchreiben')
    DROP PROCEDURE NOVVIA.spLogSchreiben;
GO

CREATE PROCEDURE NOVVIA.spLogSchreiben
    @cKategorie NVARCHAR(50),
    @cAktion NVARCHAR(100),
    @cModul NVARCHAR(50),
    @cEntityTyp NVARCHAR(50) = NULL,
    @kEntity INT = NULL,
    @cEntityNr NVARCHAR(100) = NULL,
    @cFeldname NVARCHAR(100) = NULL,
    @cAlterWert NVARCHAR(MAX) = NULL,
    @cNeuerWert NVARCHAR(MAX) = NULL,
    @cBeschreibung NVARCHAR(MAX) = NULL,
    @cDetails NVARCHAR(MAX) = NULL,
    @fBetragNetto DECIMAL(18,4) = NULL,
    @fBetragBrutto DECIMAL(18,4) = NULL,
    @kBenutzer INT = NULL,
    @cBenutzerName NVARCHAR(100) = NULL,
    @cRechnername NVARCHAR(100) = NULL,
    @cIP NVARCHAR(50) = NULL,
    @nSeverity INT = 0
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO NOVVIA.[Log] (
        cKategorie, cAktion, cModul, cEntityTyp, kEntity, cEntityNr,
        cFeldname, cAlterWert, cNeuerWert, cBeschreibung, cDetails,
        fBetragNetto, fBetragBrutto,
        kBenutzer, cBenutzerName, cRechnername, cIP, nSeverity
    ) VALUES (
        @cKategorie, @cAktion, @cModul, @cEntityTyp, @kEntity, @cEntityNr,
        @cFeldname, @cAlterWert, @cNeuerWert, @cBeschreibung, @cDetails,
        @fBetragNetto, @fBetragBrutto,
        @kBenutzer, @cBenutzerName, @cRechnername, @cIP, @nSeverity
    );

    SELECT SCOPE_IDENTITY() AS kLog;
END
GO

PRINT 'SP NOVVIA.spLogSchreiben erstellt.';
GO

-- =====================================================
-- SP: NOVVIA.spLogStammdatenAenderung
-- Shortcut für Stammdaten-Änderungen
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spLogStammdatenAenderung')
    DROP PROCEDURE NOVVIA.spLogStammdatenAenderung;
GO

CREATE PROCEDURE NOVVIA.spLogStammdatenAenderung
    @cModul NVARCHAR(50),
    @cAktion NVARCHAR(100),
    @cEntityTyp NVARCHAR(50),
    @kEntity INT,
    @cEntityNr NVARCHAR(100) = NULL,
    @cFeldname NVARCHAR(100) = NULL,
    @cAlterWert NVARCHAR(MAX) = NULL,
    @cNeuerWert NVARCHAR(MAX) = NULL,
    @cBeschreibung NVARCHAR(MAX) = NULL,
    @kBenutzer INT = NULL,
    @cBenutzerName NVARCHAR(100) = NULL
AS
BEGIN
    EXEC NOVVIA.spLogSchreiben
        @cKategorie = 'Stammdaten',
        @cAktion = @cAktion,
        @cModul = @cModul,
        @cEntityTyp = @cEntityTyp,
        @kEntity = @kEntity,
        @cEntityNr = @cEntityNr,
        @cFeldname = @cFeldname,
        @cAlterWert = @cAlterWert,
        @cNeuerWert = @cNeuerWert,
        @cBeschreibung = @cBeschreibung,
        @kBenutzer = @kBenutzer,
        @cBenutzerName = @cBenutzerName,
        @cRechnername = HOST_NAME();
END
GO

PRINT 'SP NOVVIA.spLogStammdatenAenderung erstellt.';
GO

-- =====================================================
-- SP: NOVVIA.spLogBewegung
-- Shortcut für Bewegungsdaten
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spLogBewegung')
    DROP PROCEDURE NOVVIA.spLogBewegung;
GO

CREATE PROCEDURE NOVVIA.spLogBewegung
    @cModul NVARCHAR(50),
    @cAktion NVARCHAR(100),
    @cEntityTyp NVARCHAR(50),
    @kEntity INT,
    @cEntityNr NVARCHAR(100) = NULL,
    @cBeschreibung NVARCHAR(MAX) = NULL,
    @fBetragNetto DECIMAL(18,4) = NULL,
    @fBetragBrutto DECIMAL(18,4) = NULL,
    @kBenutzer INT = NULL,
    @cBenutzerName NVARCHAR(100) = NULL
AS
BEGIN
    EXEC NOVVIA.spLogSchreiben
        @cKategorie = 'Bewegung',
        @cAktion = @cAktion,
        @cModul = @cModul,
        @cEntityTyp = @cEntityTyp,
        @kEntity = @kEntity,
        @cEntityNr = @cEntityNr,
        @cBeschreibung = @cBeschreibung,
        @fBetragNetto = @fBetragNetto,
        @fBetragBrutto = @fBetragBrutto,
        @kBenutzer = @kBenutzer,
        @cBenutzerName = @cBenutzerName,
        @cRechnername = HOST_NAME();
END
GO

PRINT 'SP NOVVIA.spLogBewegung erstellt.';
GO

-- =====================================================
-- SP: NOVVIA.spLogAbfragen
-- Log-Einträge mit Filtern abfragen
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spLogAbfragen')
    DROP PROCEDURE NOVVIA.spLogAbfragen;
GO

CREATE PROCEDURE NOVVIA.spLogAbfragen
    @cKategorie NVARCHAR(50) = NULL,
    @cModul NVARCHAR(50) = NULL,
    @cEntityTyp NVARCHAR(50) = NULL,
    @kEntity INT = NULL,
    @kBenutzer INT = NULL,
    @dVon DATETIME2 = NULL,
    @dBis DATETIME2 = NULL,
    @nSeverity INT = NULL,
    @nTop INT = 1000
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@nTop)
        kLog, cKategorie, cAktion, cModul, cEntityTyp, kEntity, cEntityNr,
        cFeldname, cAlterWert, cNeuerWert, cBeschreibung,
        fBetragNetto, fBetragBrutto,
        cBenutzerName, dZeitpunkt, nSeverity
    FROM NOVVIA.[Log]
    WHERE (@cKategorie IS NULL OR cKategorie = @cKategorie)
      AND (@cModul IS NULL OR cModul = @cModul)
      AND (@cEntityTyp IS NULL OR cEntityTyp = @cEntityTyp)
      AND (@kEntity IS NULL OR kEntity = @kEntity)
      AND (@kBenutzer IS NULL OR kBenutzer = @kBenutzer)
      AND (@dVon IS NULL OR dZeitpunkt >= @dVon)
      AND (@dBis IS NULL OR dZeitpunkt <= @dBis)
      AND (@nSeverity IS NULL OR nSeverity >= @nSeverity)
    ORDER BY dZeitpunkt DESC;
END
GO

PRINT 'SP NOVVIA.spLogAbfragen erstellt.';
GO

-- =====================================================
-- SP: NOVVIA.spLogStatistik
-- Statistik für Dashboard
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spLogStatistik')
    DROP PROCEDURE NOVVIA.spLogStatistik;
GO

CREATE PROCEDURE NOVVIA.spLogStatistik
    @nTage INT = 30
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @dVon DATETIME2 = DATEADD(DAY, -@nTage, SYSDATETIME());

    -- Übersicht nach Kategorie
    SELECT cKategorie, COUNT(*) AS Anzahl
    FROM NOVVIA.[Log]
    WHERE dZeitpunkt >= @dVon
    GROUP BY cKategorie;

    -- Übersicht nach Modul
    SELECT cModul, cKategorie, COUNT(*) AS Anzahl
    FROM NOVVIA.[Log]
    WHERE dZeitpunkt >= @dVon
    GROUP BY cModul, cKategorie
    ORDER BY Anzahl DESC;

    -- Aktivität pro Tag
    SELECT CAST(dZeitpunkt AS DATE) AS Datum, cKategorie, COUNT(*) AS Anzahl
    FROM NOVVIA.[Log]
    WHERE dZeitpunkt >= @dVon
    GROUP BY CAST(dZeitpunkt AS DATE), cKategorie
    ORDER BY Datum DESC;

    -- Top 10 Benutzer
    SELECT TOP 10 cBenutzerName, COUNT(*) AS Aktionen
    FROM NOVVIA.[Log]
    WHERE dZeitpunkt >= @dVon AND cBenutzerName IS NOT NULL
    GROUP BY cBenutzerName
    ORDER BY Aktionen DESC;
END
GO

PRINT 'SP NOVVIA.spLogStatistik erstellt.';
GO

PRINT '';
PRINT '=====================================================';
PRINT 'NOVVIA Audit-Log System installiert.';
PRINT '';
PRINT 'Kategorien:';
PRINT '  - Stammdaten: Kunde, Artikel, Lieferant angelegt/geaendert';
PRINT '  - Bewegung: Auftrag, Rechnung, Lieferschein erstellt';
PRINT '  - System: Login, Fehler, Wartung';
PRINT '';
PRINT 'Abfrage: EXEC NOVVIA.spLogAbfragen @cModul = ''Kunde''';
PRINT 'Statistik: EXEC NOVVIA.spLogStatistik @nTage = 7';
PRINT '=====================================================';
GO

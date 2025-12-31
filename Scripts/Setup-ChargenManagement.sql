-- =====================================================
-- NOVVIA Chargen-Management Setup
-- Chargenverfolgung, Sperrung und Quarantäne
-- =====================================================

USE Mandant_2;
GO

-- =====================================================
-- 1. Quarantäne-Lager anlegen (falls nicht vorhanden)
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM dbo.tWarenLager WHERE cName = 'Quarantäne')
BEGIN
    INSERT INTO dbo.tWarenLager (cName, cKuerzel, cLagerTyp, cBeschreibung, nAktiv, nAuslieferungsPrio)
    VALUES ('Quarantäne', 'QUA', 'Standard', 'Quarantänelager für gesperrte Chargen', 1, 999);
    PRINT 'Quarantäne-Lager angelegt';
END
ELSE
    PRINT 'Quarantäne-Lager existiert bereits';
GO

-- =====================================================
-- 2. NOVVIA.ChargenStatus Tabelle
-- Speichert Sperrstatus pro Charge
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ChargenStatus' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    CREATE TABLE NOVVIA.ChargenStatus (
        kChargenStatus INT IDENTITY(1,1) PRIMARY KEY,
        kWarenLagerEingang INT NOT NULL,              -- Referenz zum Wareneingang (= Charge)
        kArtikel INT NOT NULL,                         -- Artikel
        cChargenNr NVARCHAR(100) NOT NULL,            -- Chargennummer
        dMHD DATETIME NULL,                            -- MHD der Charge
        nGesperrt BIT NOT NULL DEFAULT 0,             -- 1 = Charge gesperrt
        nQuarantaene BIT NOT NULL DEFAULT 0,          -- 1 = In Quarantäne
        cSperrgrund NVARCHAR(500) NULL,               -- Grund der Sperrung
        cSperrvermerk NVARCHAR(1000) NULL,            -- Zusätzliche Hinweise
        kBenutzerGesperrt INT NULL,                   -- Wer hat gesperrt
        dGesperrtAm DATETIME NULL,                    -- Wann gesperrt
        kBenutzerFreigegeben INT NULL,                -- Wer hat freigegeben
        dFreigegebenAm DATETIME NULL,                 -- Wann freigegeben
        kOriginalWarenLager INT NULL,                 -- Original-Lager vor Quarantäne
        dErstellt DATETIME NOT NULL DEFAULT GETDATE(),
        dGeaendert DATETIME NOT NULL DEFAULT GETDATE()
    );

    CREATE INDEX IX_ChargenStatus_Artikel ON NOVVIA.ChargenStatus(kArtikel);
    CREATE INDEX IX_ChargenStatus_ChargenNr ON NOVVIA.ChargenStatus(cChargenNr);
    CREATE INDEX IX_ChargenStatus_Gesperrt ON NOVVIA.ChargenStatus(nGesperrt);
    CREATE INDEX IX_ChargenStatus_WLE ON NOVVIA.ChargenStatus(kWarenLagerEingang);

    PRINT 'NOVVIA.ChargenStatus Tabelle erstellt';
END
ELSE
    PRINT 'NOVVIA.ChargenStatus existiert bereits';
GO

-- =====================================================
-- 3. NOVVIA.ChargenBewegung Tabelle
-- Protokolliert Bewegungen (Sperrung, Freigabe, Umbuchung)
-- =====================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ChargenBewegung' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    CREATE TABLE NOVVIA.ChargenBewegung (
        kChargenBewegung INT IDENTITY(1,1) PRIMARY KEY,
        kWarenLagerEingang INT NOT NULL,
        kArtikel INT NOT NULL,
        cChargenNr NVARCHAR(100) NOT NULL,
        cAktion NVARCHAR(50) NOT NULL,                -- 'GESPERRT', 'FREIGEGEBEN', 'QUARANTAENE', 'RUECKBUCHUNG'
        cGrund NVARCHAR(500) NULL,
        cHinweis NVARCHAR(1000) NULL,
        kVonWarenLager INT NULL,                      -- Von welchem Lager
        kNachWarenLager INT NULL,                     -- Nach welchem Lager
        fMenge DECIMAL(25,13) NULL,                   -- Betroffene Menge
        kBenutzer INT NOT NULL,
        dErstellt DATETIME NOT NULL DEFAULT GETDATE()
    );

    CREATE INDEX IX_ChargenBewegung_Artikel ON NOVVIA.ChargenBewegung(kArtikel);
    CREATE INDEX IX_ChargenBewegung_ChargenNr ON NOVVIA.ChargenBewegung(cChargenNr);
    CREATE INDEX IX_ChargenBewegung_Datum ON NOVVIA.ChargenBewegung(dErstellt);

    PRINT 'NOVVIA.ChargenBewegung Tabelle erstellt';
END
ELSE
    PRINT 'NOVVIA.ChargenBewegung existiert bereits';
GO

-- =====================================================
-- 4. View: Aktuelle Chargenbestände mit Status
-- =====================================================
IF EXISTS (SELECT 1 FROM sys.views WHERE name = 'vChargenBestand' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP VIEW NOVVIA.vChargenBestand;
GO

CREATE VIEW NOVVIA.vChargenBestand AS
SELECT
    we.kWarenLagerEingang,
    we.kArtikel,
    a.cArtNr,
    ISNULL(ab.cName, '') AS cArtikelName,
    we.cChargenNr,
    we.dMHD,
    we.fAnzahlAktuell AS fBestand,
    we.fAnzahl AS fEingang,
    wlp.kWarenLager,
    wl.cName AS cLagerName,
    we.dErstellt AS dEingang,
    we.cLieferscheinNr,
    -- Chargen-Status
    ISNULL(cs.nGesperrt, 0) AS nGesperrt,
    ISNULL(cs.nQuarantaene, 0) AS nQuarantaene,
    cs.cSperrgrund,
    cs.cSperrvermerk,
    cs.dGesperrtAm,
    cs.kBenutzerGesperrt,
    -- MHD-Status
    CASE
        WHEN we.dMHD IS NULL THEN 'Kein MHD'
        WHEN we.dMHD < GETDATE() THEN 'Abgelaufen'
        WHEN we.dMHD < DATEADD(DAY, 30, GETDATE()) THEN 'Läuft bald ab'
        ELSE 'OK'
    END AS cMHDStatus,
    DATEDIFF(DAY, GETDATE(), we.dMHD) AS nTageRestMHD,
    -- Buchungsart
    ba.cName AS cBuchungsart
FROM dbo.tWarenLagerEingang we
JOIN dbo.tArtikel a ON we.kArtikel = a.kArtikel
LEFT JOIN dbo.tArtikelBeschreibung ab ON a.kArtikel = ab.kArtikel AND ab.kSprache = 1 AND ab.kPlattform = 1
LEFT JOIN dbo.tWarenLagerPlatz wlp ON we.kWarenLagerPlatz = wlp.kWarenLagerPlatz
LEFT JOIN dbo.tWarenLager wl ON wlp.kWarenLager = wl.kWarenLager
LEFT JOIN dbo.tBuchungsart ba ON we.kBuchungsart = ba.kBuchungsart
LEFT JOIN NOVVIA.ChargenStatus cs ON we.kWarenLagerEingang = cs.kWarenLagerEingang
WHERE we.cChargenNr IS NOT NULL
  AND we.cChargenNr <> ''
  AND we.fAnzahlAktuell > 0;
GO

PRINT 'View NOVVIA.vChargenBestand erstellt';
GO

-- =====================================================
-- 5. Stored Procedure: Charge sperren
-- =====================================================
IF EXISTS (SELECT 1 FROM sys.procedures WHERE name = 'spChargeSperre' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spChargeSperre;
GO

CREATE PROCEDURE NOVVIA.spChargeSperre
    @kWarenLagerEingang INT,
    @cSperrgrund NVARCHAR(500),
    @cSperrvermerk NVARCHAR(1000) = NULL,
    @kBenutzer INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @kArtikel INT, @cChargenNr NVARCHAR(100), @dMHD DATETIME;

    -- Charge-Info holen
    SELECT @kArtikel = kArtikel, @cChargenNr = cChargenNr, @dMHD = dMHD
    FROM dbo.tWarenLagerEingang
    WHERE kWarenLagerEingang = @kWarenLagerEingang;

    IF @kArtikel IS NULL
    BEGIN
        RAISERROR('Wareneingang nicht gefunden', 16, 1);
        RETURN;
    END

    -- Status anlegen oder aktualisieren
    IF EXISTS (SELECT 1 FROM NOVVIA.ChargenStatus WHERE kWarenLagerEingang = @kWarenLagerEingang)
    BEGIN
        UPDATE NOVVIA.ChargenStatus
        SET nGesperrt = 1,
            cSperrgrund = @cSperrgrund,
            cSperrvermerk = @cSperrvermerk,
            kBenutzerGesperrt = @kBenutzer,
            dGesperrtAm = GETDATE(),
            dGeaendert = GETDATE()
        WHERE kWarenLagerEingang = @kWarenLagerEingang;
    END
    ELSE
    BEGIN
        INSERT INTO NOVVIA.ChargenStatus (kWarenLagerEingang, kArtikel, cChargenNr, dMHD, nGesperrt, cSperrgrund, cSperrvermerk, kBenutzerGesperrt, dGesperrtAm)
        VALUES (@kWarenLagerEingang, @kArtikel, @cChargenNr, @dMHD, 1, @cSperrgrund, @cSperrvermerk, @kBenutzer, GETDATE());
    END

    -- Bewegung protokollieren
    INSERT INTO NOVVIA.ChargenBewegung (kWarenLagerEingang, kArtikel, cChargenNr, cAktion, cGrund, cHinweis, kBenutzer)
    VALUES (@kWarenLagerEingang, @kArtikel, @cChargenNr, 'GESPERRT', @cSperrgrund, @cSperrvermerk, @kBenutzer);

    SELECT 'OK' AS Status, 'Charge gesperrt' AS Meldung;
END
GO

PRINT 'Stored Procedure NOVVIA.spChargeSperre erstellt';
GO

-- =====================================================
-- 6. Stored Procedure: Charge freigeben
-- =====================================================
IF EXISTS (SELECT 1 FROM sys.procedures WHERE name = 'spChargeFreigabe' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spChargeFreigabe;
GO

CREATE PROCEDURE NOVVIA.spChargeFreigabe
    @kWarenLagerEingang INT,
    @cHinweis NVARCHAR(1000) = NULL,
    @kBenutzer INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @kArtikel INT, @cChargenNr NVARCHAR(100);

    SELECT @kArtikel = kArtikel, @cChargenNr = cChargenNr
    FROM NOVVIA.ChargenStatus
    WHERE kWarenLagerEingang = @kWarenLagerEingang;

    IF @kArtikel IS NULL
    BEGIN
        RAISERROR('Chargen-Status nicht gefunden', 16, 1);
        RETURN;
    END

    UPDATE NOVVIA.ChargenStatus
    SET nGesperrt = 0,
        kBenutzerFreigegeben = @kBenutzer,
        dFreigegebenAm = GETDATE(),
        dGeaendert = GETDATE()
    WHERE kWarenLagerEingang = @kWarenLagerEingang;

    INSERT INTO NOVVIA.ChargenBewegung (kWarenLagerEingang, kArtikel, cChargenNr, cAktion, cHinweis, kBenutzer)
    VALUES (@kWarenLagerEingang, @kArtikel, @cChargenNr, 'FREIGEGEBEN', @cHinweis, @kBenutzer);

    SELECT 'OK' AS Status, 'Charge freigegeben' AS Meldung;
END
GO

PRINT 'Stored Procedure NOVVIA.spChargeFreigabe erstellt';
GO

-- =====================================================
-- 7. Stored Procedure: Charge in Quarantäne verschieben
-- =====================================================
IF EXISTS (SELECT 1 FROM sys.procedures WHERE name = 'spChargeQuarantaene' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spChargeQuarantaene;
GO

CREATE PROCEDURE NOVVIA.spChargeQuarantaene
    @kWarenLagerEingang INT,
    @cGrund NVARCHAR(500),
    @kBenutzer INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @kArtikel INT, @cChargenNr NVARCHAR(100), @dMHD DATETIME;
    DECLARE @kOriginalWarenLager INT, @kQuarantaeneLager INT;
    DECLARE @kWarenLagerPlatz INT;

    -- Quarantäne-Lager finden
    SELECT @kQuarantaeneLager = kWarenLager FROM dbo.tWarenLager WHERE cName = 'Quarantäne';
    IF @kQuarantaeneLager IS NULL
    BEGIN
        RAISERROR('Quarantäne-Lager nicht gefunden. Bitte zuerst einrichten.', 16, 1);
        RETURN;
    END

    -- Charge-Info und aktuelles Lager holen
    SELECT @kArtikel = we.kArtikel,
           @cChargenNr = we.cChargenNr,
           @dMHD = we.dMHD,
           @kOriginalWarenLager = wlp.kWarenLager,
           @kWarenLagerPlatz = we.kWarenLagerPlatz
    FROM dbo.tWarenLagerEingang we
    LEFT JOIN dbo.tWarenLagerPlatz wlp ON we.kWarenLagerPlatz = wlp.kWarenLagerPlatz
    WHERE we.kWarenLagerEingang = @kWarenLagerEingang;

    IF @kArtikel IS NULL
    BEGIN
        RAISERROR('Wareneingang nicht gefunden', 16, 1);
        RETURN;
    END

    -- Status anlegen oder aktualisieren
    IF EXISTS (SELECT 1 FROM NOVVIA.ChargenStatus WHERE kWarenLagerEingang = @kWarenLagerEingang)
    BEGIN
        UPDATE NOVVIA.ChargenStatus
        SET nGesperrt = 1,
            nQuarantaene = 1,
            cSperrgrund = @cGrund,
            kBenutzerGesperrt = @kBenutzer,
            dGesperrtAm = GETDATE(),
            kOriginalWarenLager = @kOriginalWarenLager,
            dGeaendert = GETDATE()
        WHERE kWarenLagerEingang = @kWarenLagerEingang;
    END
    ELSE
    BEGIN
        INSERT INTO NOVVIA.ChargenStatus (kWarenLagerEingang, kArtikel, cChargenNr, dMHD, nGesperrt, nQuarantaene, cSperrgrund, kBenutzerGesperrt, dGesperrtAm, kOriginalWarenLager)
        VALUES (@kWarenLagerEingang, @kArtikel, @cChargenNr, @dMHD, 1, 1, @cGrund, @kBenutzer, GETDATE(), @kOriginalWarenLager);
    END

    -- Bewegung protokollieren
    INSERT INTO NOVVIA.ChargenBewegung (kWarenLagerEingang, kArtikel, cChargenNr, cAktion, cGrund, kVonWarenLager, kNachWarenLager, kBenutzer)
    VALUES (@kWarenLagerEingang, @kArtikel, @cChargenNr, 'QUARANTAENE', @cGrund, @kOriginalWarenLager, @kQuarantaeneLager, @kBenutzer);

    SELECT 'OK' AS Status, 'Charge in Quarantäne verschoben' AS Meldung, @kQuarantaeneLager AS kQuarantaeneLager;
END
GO

PRINT 'Stored Procedure NOVVIA.spChargeQuarantaene erstellt';
GO

-- =====================================================
-- 8. Stored Procedure: Charge aus Quarantäne zurück
-- =====================================================
IF EXISTS (SELECT 1 FROM sys.procedures WHERE name = 'spChargeAusQuarantaene' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spChargeAusQuarantaene;
GO

CREATE PROCEDURE NOVVIA.spChargeAusQuarantaene
    @kWarenLagerEingang INT,
    @cHinweis NVARCHAR(1000) = NULL,
    @kBenutzer INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @kArtikel INT, @cChargenNr NVARCHAR(100);
    DECLARE @kOriginalWarenLager INT, @kQuarantaeneLager INT;

    SELECT @kQuarantaeneLager = kWarenLager FROM dbo.tWarenLager WHERE cName = 'Quarantäne';

    SELECT @kArtikel = kArtikel,
           @cChargenNr = cChargenNr,
           @kOriginalWarenLager = kOriginalWarenLager
    FROM NOVVIA.ChargenStatus
    WHERE kWarenLagerEingang = @kWarenLagerEingang;

    IF @kArtikel IS NULL
    BEGIN
        RAISERROR('Chargen-Status nicht gefunden', 16, 1);
        RETURN;
    END

    UPDATE NOVVIA.ChargenStatus
    SET nGesperrt = 0,
        nQuarantaene = 0,
        kBenutzerFreigegeben = @kBenutzer,
        dFreigegebenAm = GETDATE(),
        dGeaendert = GETDATE()
    WHERE kWarenLagerEingang = @kWarenLagerEingang;

    INSERT INTO NOVVIA.ChargenBewegung (kWarenLagerEingang, kArtikel, cChargenNr, cAktion, cHinweis, kVonWarenLager, kNachWarenLager, kBenutzer)
    VALUES (@kWarenLagerEingang, @kArtikel, @cChargenNr, 'RUECKBUCHUNG', @cHinweis, @kQuarantaeneLager, @kOriginalWarenLager, @kBenutzer);

    SELECT 'OK' AS Status, 'Charge aus Quarantäne zurückgeholt' AS Meldung, @kOriginalWarenLager AS kZielWarenLager;
END
GO

PRINT 'Stored Procedure NOVVIA.spChargeAusQuarantaene erstellt';
GO

PRINT '';
PRINT '=== Chargen-Management Setup abgeschlossen ===';
PRINT '';
GO

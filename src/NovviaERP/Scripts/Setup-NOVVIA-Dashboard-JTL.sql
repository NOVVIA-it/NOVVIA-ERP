-- =====================================================
-- NOVVIA Dashboard Stored Procedures (JTL Schema)
-- =====================================================
-- Daten fuer das graphische Dashboard
-- Verwendet JTL Verkauf/Rechnung Schema
-- =====================================================

USE [Mandant_1]
GO

-- Schema sicherstellen
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'NOVVIA')
    EXEC('CREATE SCHEMA NOVVIA');
GO

-- =====================================================
-- SP: NOVVIA.spDashboardKPIs
-- Haupt-KPIs fuer das Dashboard
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spDashboardKPIs')
    DROP PROCEDURE NOVVIA.spDashboardKPIs;
GO

CREATE PROCEDURE NOVVIA.spDashboardKPIs
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Heute DATE = CAST(GETDATE() AS DATE);
    DECLARE @MonatStart DATE = DATEFROMPARTS(YEAR(@Heute), MONTH(@Heute), 1);
    DECLARE @JahrStart DATE = DATEFROMPARTS(YEAR(@Heute), 1, 1);
    DECLARE @VormonatStart DATE = DATEADD(MONTH, -1, @MonatStart);
    DECLARE @VormonatEnde DATE = DATEADD(DAY, -1, @MonatStart);

    SELECT
        ISNULL((SELECT SUM(e.fSummeNetto) FROM Rechnung.tRechnung r
                JOIN Rechnung.tRechnungEckdaten e ON r.kRechnung = e.kRechnung
                WHERE CAST(r.dErstellt AS DATE) = @Heute AND r.nStorno = 0), 0) AS UmsatzHeute,
        ISNULL((SELECT SUM(e.fSummeNetto) FROM Rechnung.tRechnung r
                JOIN Rechnung.tRechnungEckdaten e ON r.kRechnung = e.kRechnung
                WHERE r.dErstellt >= DATEADD(DAY, -DATEPART(WEEKDAY, @Heute)+1, @Heute) AND r.nStorno = 0), 0) AS UmsatzWoche,
        ISNULL((SELECT SUM(e.fSummeNetto) FROM Rechnung.tRechnung r
                JOIN Rechnung.tRechnungEckdaten e ON r.kRechnung = e.kRechnung
                WHERE r.dErstellt >= @MonatStart AND r.nStorno = 0), 0) AS UmsatzMonat,
        ISNULL((SELECT SUM(e.fSummeNetto) FROM Rechnung.tRechnung r
                JOIN Rechnung.tRechnungEckdaten e ON r.kRechnung = e.kRechnung
                WHERE r.dErstellt >= @VormonatStart AND r.dErstellt <= @VormonatEnde AND r.nStorno = 0), 0) AS UmsatzVormonat,
        ISNULL((SELECT SUM(e.fSummeNetto) FROM Rechnung.tRechnung r
                JOIN Rechnung.tRechnungEckdaten e ON r.kRechnung = e.kRechnung
                WHERE r.dErstellt >= @JahrStart AND r.nStorno = 0), 0) AS UmsatzJahr,
        (SELECT COUNT(*) FROM Verkauf.tAuftrag WHERE CAST(dErstellt AS DATE) = @Heute AND nStorno = 0) AS AuftraegeHeute,
        (SELECT COUNT(*) FROM Verkauf.tAuftrag WHERE nAuftragStatus IN (1,2) AND nStorno = 0) AS AuftraegeOffen,
        ISNULL((SELECT SUM(e.fWertNetto) FROM Verkauf.tAuftrag a
                JOIN Verkauf.tAuftragEckdaten e ON a.kAuftrag = e.kAuftrag
                WHERE a.nAuftragStatus IN (1,2) AND a.nStorno = 0), 0) AS AuftraegeOffenWert,
        (SELECT COUNT(*) FROM Rechnung.tRechnung r
                JOIN Rechnung.tRechnungEckdaten e ON r.kRechnung = e.kRechnung
                WHERE e.nZahlungStatus < 2 AND r.nStorno = 0) AS RechnungenOffen,
        ISNULL((SELECT SUM(e.fSummeBrutto - ISNULL(e.fZahlung, 0)) FROM Rechnung.tRechnung r
                JOIN Rechnung.tRechnungEckdaten e ON r.kRechnung = e.kRechnung
                WHERE e.nZahlungStatus < 2 AND r.nStorno = 0), 0) AS RechnungenOffenWert,
        (SELECT COUNT(*) FROM Rechnung.tRechnung r
                JOIN Rechnung.tRechnungEckdaten e ON r.kRechnung = e.kRechnung
                WHERE e.nZahlungStatus < 2 AND r.nStorno = 0
                AND DATEADD(DAY, ISNULL(r.nZahlungszielTage, 14), r.dErstellt) < @Heute) AS RechnungenUeberfaellig,
        ISNULL((SELECT SUM(e.fSummeBrutto - ISNULL(e.fZahlung, 0)) FROM Rechnung.tRechnung r
                JOIN Rechnung.tRechnungEckdaten e ON r.kRechnung = e.kRechnung
                WHERE e.nZahlungStatus < 2 AND r.nStorno = 0
                AND DATEADD(DAY, ISNULL(r.nZahlungszielTage, 14), r.dErstellt) < @Heute), 0) AS RechnungenUeberfaelligWert,
        (SELECT COUNT(*) FROM Lieferschein.tLieferschein WHERE CAST(dErstellt AS DATE) = @Heute AND nStorno = 0) AS VersandHeute,
        (SELECT COUNT(*) FROM Verkauf.tAuftrag a
                JOIN Verkauf.tAuftragEckdaten e ON a.kAuftrag = e.kAuftrag
                WHERE e.nLieferstatus < 2 AND a.nStorno = 0) AS VersandOffen,
        (SELECT COUNT(*) FROM dbo.tKunde WHERE CAST(dErstellt AS DATE) >= @MonatStart) AS NeueKundenMonat,
        (SELECT COUNT(*) FROM dbo.tKunde WHERE nRegistriert = 1) AS KundenGesamt;
END
GO

PRINT 'SP NOVVIA.spDashboardKPIs erstellt (JTL Schema).';
GO

-- =====================================================
-- SP: NOVVIA.spDashboardUmsatzVerlauf
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spDashboardUmsatzVerlauf')
    DROP PROCEDURE NOVVIA.spDashboardUmsatzVerlauf;
GO

CREATE PROCEDURE NOVVIA.spDashboardUmsatzVerlauf @nMonate INT = 12
AS
BEGIN
    SET NOCOUNT ON;
    ;WITH Monate AS (
        SELECT DATEFROMPARTS(YEAR(DATEADD(MONTH, -n, GETDATE())), MONTH(DATEADD(MONTH, -n, GETDATE())), 1) AS MonatStart,
               EOMONTH(DATEADD(MONTH, -n, GETDATE())) AS MonatEnde,
               FORMAT(DATEADD(MONTH, -n, GETDATE()), 'MMM yy', 'de-DE') AS MonatLabel,
               DATEADD(MONTH, -n, GETDATE()) AS SortDatum
        FROM (SELECT TOP (@nMonate) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS n FROM sys.objects) Numbers
    )
    SELECT m.MonatLabel, m.MonatStart, ISNULL(SUM(e.fSummeNetto), 0) AS Umsatz, COUNT(r.kRechnung) AS AnzahlRechnungen
    FROM Monate m
    LEFT JOIN Rechnung.tRechnung r ON r.dErstellt >= m.MonatStart AND r.dErstellt <= m.MonatEnde AND r.nStorno = 0
    LEFT JOIN Rechnung.tRechnungEckdaten e ON r.kRechnung = e.kRechnung
    GROUP BY m.MonatLabel, m.MonatStart, m.SortDatum ORDER BY m.SortDatum;
END
GO

-- =====================================================
-- SP: NOVVIA.spDashboardTopKunden
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spDashboardTopKunden')
    DROP PROCEDURE NOVVIA.spDashboardTopKunden;
GO

CREATE PROCEDURE NOVVIA.spDashboardTopKunden @nTop INT = 10, @nTage INT = 365
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@nTop) k.kKunde, COALESCE(k.cFirma, k.cVorname + ' ' + k.cNachname, k.cNachname) AS KundeName,
        k.cKundenNr, SUM(e.fSummeNetto) AS Umsatz, COUNT(r.kRechnung) AS AnzahlRechnungen
    FROM dbo.tKunde k
    JOIN Rechnung.tRechnung r ON k.kKunde = r.tKunde_kKunde
    JOIN Rechnung.tRechnungEckdaten e ON r.kRechnung = e.kRechnung
    WHERE r.dErstellt >= DATEADD(DAY, -@nTage, GETDATE()) AND r.nStorno = 0
    GROUP BY k.kKunde, k.cFirma, k.cVorname, k.cNachname, k.cKundenNr ORDER BY Umsatz DESC;
END
GO

-- =====================================================
-- SP: NOVVIA.spDashboardTopArtikel
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spDashboardTopArtikel')
    DROP PROCEDURE NOVVIA.spDashboardTopArtikel;
GO

CREATE PROCEDURE NOVVIA.spDashboardTopArtikel @nTop INT = 10, @nTage INT = 30, @cSortierung NVARCHAR(10) = 'Umsatz'
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@nTop) a.kArtikel, a.cArtNr, COALESCE(ab.cName, a.cArtNr) AS ArtikelName,
        SUM(ap.fAnzahl) AS Menge, SUM(pe.fVKNetto * ap.fAnzahl) AS Umsatz
    FROM dbo.tArtikel a
    LEFT JOIN dbo.tArtikelBeschreibung ab ON a.kArtikel = ab.kArtikel AND ab.kSprache = 1
    JOIN Verkauf.tAuftragPosition ap ON a.kArtikel = ap.kArtikel
    JOIN Verkauf.tAuftragPositionEckdaten pe ON ap.kAuftragPosition = pe.kAuftragPosition
    JOIN Verkauf.tAuftrag au ON ap.kAuftrag = au.kAuftrag
    WHERE au.dErstellt >= DATEADD(DAY, -@nTage, GETDATE()) AND au.nStorno = 0
    GROUP BY a.kArtikel, a.cArtNr, ab.cName
    ORDER BY CASE WHEN @cSortierung = 'Menge' THEN SUM(ap.fAnzahl) ELSE SUM(pe.fVKNetto * ap.fAnzahl) END DESC;
END
GO

-- =====================================================
-- SP: NOVVIA.spDashboardAuftragStatus
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spDashboardAuftragStatus')
    DROP PROCEDURE NOVVIA.spDashboardAuftragStatus;
GO

CREATE PROCEDURE NOVVIA.spDashboardAuftragStatus
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CASE a.nAuftragStatus WHEN 1 THEN 'Offen' WHEN 2 THEN 'In Bearbeitung' WHEN 3 THEN 'Versendet' 
           WHEN 4 THEN 'Teilversendet' WHEN 5 THEN 'Abgeschlossen' ELSE 'Sonstige' END AS Status,
        a.nAuftragStatus AS StatusCode, COUNT(*) AS Anzahl, SUM(e.fWertNetto) AS Wert
    FROM Verkauf.tAuftrag a
    LEFT JOIN Verkauf.tAuftragEckdaten e ON a.kAuftrag = e.kAuftrag
    WHERE a.dErstellt >= DATEADD(DAY, -30, GETDATE())
    GROUP BY a.nAuftragStatus ORDER BY a.nAuftragStatus;
END
GO

-- =====================================================
-- SP: NOVVIA.spDashboardAktivitaeten
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spDashboardAktivitaeten')
    DROP PROCEDURE NOVVIA.spDashboardAktivitaeten;
GO

CREATE PROCEDURE NOVVIA.spDashboardAktivitaeten @nTop INT = 20
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@nTop) 'Auftrag' AS Typ, 'Auftrag ' + a.cAuftragsnr + ' erstellt' AS Beschreibung,
        COALESCE(k.cFirma, k.cVorname + ' ' + k.cNachname) AS Kunde, e.fWertBrutto AS Betrag, a.dErstellt AS Zeitpunkt
    FROM Verkauf.tAuftrag a
    LEFT JOIN dbo.tKunde k ON a.tKunde_kKunde = k.kKunde
    LEFT JOIN Verkauf.tAuftragEckdaten e ON a.kAuftrag = e.kAuftrag
    WHERE a.nStorno = 0 ORDER BY a.dErstellt DESC;
END
GO

PRINT 'Alle Dashboard SPs auf JTL Schema aktualisiert.';
GO

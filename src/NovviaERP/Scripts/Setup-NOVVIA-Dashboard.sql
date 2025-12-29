-- =====================================================
-- NOVVIA Dashboard Stored Procedures
-- =====================================================
-- Daten fuer das graphische Dashboard
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

    -- Umsatz
    SELECT
        -- Heute
        ISNULL((SELECT SUM(fGesamtNetto) FROM tRechnung WHERE CAST(dErstellt AS DATE) = @Heute AND nStorno = 0), 0) AS UmsatzHeute,
        -- Diese Woche
        ISNULL((SELECT SUM(fGesamtNetto) FROM tRechnung WHERE dErstellt >= DATEADD(DAY, -DATEPART(WEEKDAY, @Heute)+1, @Heute) AND nStorno = 0), 0) AS UmsatzWoche,
        -- Dieser Monat
        ISNULL((SELECT SUM(fGesamtNetto) FROM tRechnung WHERE dErstellt >= @MonatStart AND nStorno = 0), 0) AS UmsatzMonat,
        -- Vormonat (zum Vergleich)
        ISNULL((SELECT SUM(fGesamtNetto) FROM tRechnung WHERE dErstellt >= @VormonatStart AND dErstellt <= @VormonatEnde AND nStorno = 0), 0) AS UmsatzVormonat,
        -- Dieses Jahr
        ISNULL((SELECT SUM(fGesamtNetto) FROM tRechnung WHERE dErstellt >= @JahrStart AND nStorno = 0), 0) AS UmsatzJahr,

        -- Auftraege
        (SELECT COUNT(*) FROM tBestellung WHERE CAST(dErstellt AS DATE) = @Heute AND nStorno = 0) AS AuftraegeHeute,
        (SELECT COUNT(*) FROM tBestellung WHERE nStatus IN (1,2) AND nStorno = 0) AS AuftraegeOffen,
        ISNULL((SELECT SUM(fGesamtNetto) FROM tBestellung WHERE nStatus IN (1,2) AND nStorno = 0), 0) AS AuftraegeOffenWert,

        -- Rechnungen
        (SELECT COUNT(*) FROM tRechnung WHERE nStatus = 1 AND nStorno = 0) AS RechnungenOffen,
        ISNULL((SELECT SUM(fGesamtBrutto - ISNULL(fBezahlt, 0)) FROM tRechnung WHERE nStatus = 1 AND nStorno = 0), 0) AS RechnungenOffenWert,

        -- Ueberfaellige Rechnungen
        (SELECT COUNT(*) FROM tRechnung WHERE nStatus = 1 AND dFaellig < @Heute AND nStorno = 0) AS RechnungenUeberfaellig,
        ISNULL((SELECT SUM(fGesamtBrutto - ISNULL(fBezahlt, 0)) FROM tRechnung WHERE nStatus = 1 AND dFaellig < @Heute AND nStorno = 0), 0) AS RechnungenUeberfaelligWert,

        -- Versand heute
        (SELECT COUNT(*) FROM tVersand WHERE CAST(dErstellt AS DATE) = @Heute) AS VersandHeute,
        (SELECT COUNT(*) FROM tBestellung WHERE nStatus = 2 AND nStorno = 0) AS VersandOffen,

        -- Kunden
        (SELECT COUNT(*) FROM tKunde WHERE CAST(dErstellt AS DATE) >= @MonatStart) AS NeueKundenMonat,
        (SELECT COUNT(*) FROM tKunde WHERE nAktiv = 1) AS KundenGesamt;
END
GO

PRINT 'SP NOVVIA.spDashboardKPIs erstellt.';
GO

-- =====================================================
-- SP: NOVVIA.spDashboardUmsatzVerlauf
-- Umsatz der letzten 12 Monate
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spDashboardUmsatzVerlauf')
    DROP PROCEDURE NOVVIA.spDashboardUmsatzVerlauf;
GO

CREATE PROCEDURE NOVVIA.spDashboardUmsatzVerlauf
    @nMonate INT = 12
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH Monate AS (
        SELECT
            DATEFROMPARTS(YEAR(DATEADD(MONTH, -n, GETDATE())), MONTH(DATEADD(MONTH, -n, GETDATE())), 1) AS MonatStart,
            EOMONTH(DATEADD(MONTH, -n, GETDATE())) AS MonatEnde,
            FORMAT(DATEADD(MONTH, -n, GETDATE()), 'MMM yy', 'de-DE') AS MonatLabel,
            DATEADD(MONTH, -n, GETDATE()) AS SortDatum
        FROM (SELECT TOP (@nMonate) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS n FROM sys.objects) Numbers
    )
    SELECT
        m.MonatLabel,
        m.MonatStart,
        ISNULL(SUM(r.fGesamtNetto), 0) AS Umsatz,
        COUNT(r.kRechnung) AS AnzahlRechnungen
    FROM Monate m
    LEFT JOIN tRechnung r ON r.dErstellt >= m.MonatStart AND r.dErstellt <= m.MonatEnde AND r.nStorno = 0
    GROUP BY m.MonatLabel, m.MonatStart, m.SortDatum
    ORDER BY m.SortDatum;
END
GO

PRINT 'SP NOVVIA.spDashboardUmsatzVerlauf erstellt.';
GO

-- =====================================================
-- SP: NOVVIA.spDashboardTopKunden
-- Top Kunden nach Umsatz
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spDashboardTopKunden')
    DROP PROCEDURE NOVVIA.spDashboardTopKunden;
GO

CREATE PROCEDURE NOVVIA.spDashboardTopKunden
    @nTop INT = 10,
    @nTage INT = 365
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@nTop)
        k.kKunde,
        COALESCE(k.cFirma, k.cVorname + ' ' + k.cName, k.cName) AS KundeName,
        k.cKundenNr,
        SUM(r.fGesamtNetto) AS Umsatz,
        COUNT(r.kRechnung) AS AnzahlRechnungen
    FROM tKunde k
    JOIN tRechnung r ON k.kKunde = r.kKunde
    WHERE r.dErstellt >= DATEADD(DAY, -@nTage, GETDATE())
      AND r.nStorno = 0
    GROUP BY k.kKunde, k.cFirma, k.cVorname, k.cName, k.cKundenNr
    ORDER BY Umsatz DESC;
END
GO

PRINT 'SP NOVVIA.spDashboardTopKunden erstellt.';
GO

-- =====================================================
-- SP: NOVVIA.spDashboardTopArtikel
-- Top Artikel nach Umsatz/Menge
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spDashboardTopArtikel')
    DROP PROCEDURE NOVVIA.spDashboardTopArtikel;
GO

CREATE PROCEDURE NOVVIA.spDashboardTopArtikel
    @nTop INT = 10,
    @nTage INT = 30,
    @cSortierung NVARCHAR(10) = 'Umsatz'  -- 'Umsatz' oder 'Menge'
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@nTop)
        a.kArtikel,
        a.cArtNr,
        COALESCE(ab.cName, a.cArtNr) AS ArtikelName,
        SUM(bp.nAnzahl) AS Menge,
        SUM(bp.fVKNetto * bp.nAnzahl) AS Umsatz
    FROM tArtikel a
    LEFT JOIN tArtikelBeschreibung ab ON a.kArtikel = ab.kArtikel AND ab.kSprache = 1
    JOIN tBestellPos bp ON a.kArtikel = bp.kArtikel
    JOIN tBestellung b ON bp.kBestellung = b.kBestellung
    WHERE b.dErstellt >= DATEADD(DAY, -@nTage, GETDATE())
      AND b.nStorno = 0
    GROUP BY a.kArtikel, a.cArtNr, ab.cName
    ORDER BY
        CASE WHEN @cSortierung = 'Menge' THEN SUM(bp.nAnzahl) ELSE SUM(bp.fVKNetto * bp.nAnzahl) END DESC;
END
GO

PRINT 'SP NOVVIA.spDashboardTopArtikel erstellt.';
GO

-- =====================================================
-- SP: NOVVIA.spDashboardAuftragStatus
-- Auftraege nach Status (fuer Pie Chart)
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spDashboardAuftragStatus')
    DROP PROCEDURE NOVVIA.spDashboardAuftragStatus;
GO

CREATE PROCEDURE NOVVIA.spDashboardAuftragStatus
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        CASE nStatus
            WHEN 1 THEN 'Offen'
            WHEN 2 THEN 'In Bearbeitung'
            WHEN 3 THEN 'Versendet'
            WHEN 4 THEN 'Bezahlt'
            WHEN 5 THEN 'Storniert'
            ELSE 'Sonstige'
        END AS Status,
        nStatus AS StatusCode,
        COUNT(*) AS Anzahl,
        SUM(fGesamtNetto) AS Wert
    FROM tBestellung
    WHERE dErstellt >= DATEADD(DAY, -30, GETDATE())
    GROUP BY nStatus
    ORDER BY nStatus;
END
GO

PRINT 'SP NOVVIA.spDashboardAuftragStatus erstellt.';
GO

-- =====================================================
-- SP: NOVVIA.spDashboardZahlungseingang
-- Zahlungseingaenge der letzten Wochen
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spDashboardZahlungseingang')
    DROP PROCEDURE NOVVIA.spDashboardZahlungseingang;
GO

CREATE PROCEDURE NOVVIA.spDashboardZahlungseingang
    @nWochen INT = 8
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH Wochen AS (
        SELECT
            DATEADD(WEEK, -n, CAST(GETDATE() AS DATE)) AS WocheStart,
            DATEADD(DAY, 6, DATEADD(WEEK, -n, CAST(GETDATE() AS DATE))) AS WocheEnde,
            'KW ' + CAST(DATEPART(WEEK, DATEADD(WEEK, -n, GETDATE())) AS NVARCHAR(2)) AS WocheLabel,
            n AS SortNr
        FROM (SELECT TOP (@nWochen) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS n FROM sys.objects) Numbers
    )
    SELECT
        w.WocheLabel,
        w.WocheStart,
        ISNULL(SUM(z.fBetrag), 0) AS Betrag,
        COUNT(z.kZahlungseingang) AS Anzahl
    FROM Wochen w
    LEFT JOIN tZahlungseingang z ON z.dZahlung >= w.WocheStart AND z.dZahlung <= w.WocheEnde
    GROUP BY w.WocheLabel, w.WocheStart, w.SortNr
    ORDER BY w.SortNr DESC;
END
GO

PRINT 'SP NOVVIA.spDashboardZahlungseingang erstellt.';
GO

-- =====================================================
-- SP: NOVVIA.spDashboardLagerAlarm
-- Artikel mit niedrigem Bestand
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spDashboardLagerAlarm')
    DROP PROCEDURE NOVVIA.spDashboardLagerAlarm;
GO

CREATE PROCEDURE NOVVIA.spDashboardLagerAlarm
    @nTop INT = 20
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@nTop)
        a.kArtikel,
        a.cArtNr,
        COALESCE(ab.cName, a.cArtNr) AS ArtikelName,
        ISNULL(SUM(lb.fBestand), 0) AS Bestand,
        ISNULL(a.fMindestbestand, 0) AS Mindestbestand,
        ISNULL(a.fMindestbestand, 0) - ISNULL(SUM(lb.fBestand), 0) AS Differenz
    FROM tArtikel a
    LEFT JOIN tArtikelBeschreibung ab ON a.kArtikel = ab.kArtikel AND ab.kSprache = 1
    LEFT JOIN tLagerbestand lb ON a.kArtikel = lb.kArtikel
    WHERE a.cAktiv = 'Y'
      AND a.fMindestbestand > 0
    GROUP BY a.kArtikel, a.cArtNr, ab.cName, a.fMindestbestand
    HAVING ISNULL(SUM(lb.fBestand), 0) < a.fMindestbestand
    ORDER BY Differenz DESC;
END
GO

PRINT 'SP NOVVIA.spDashboardLagerAlarm erstellt.';
GO

-- =====================================================
-- SP: NOVVIA.spDashboardAktivitaeten
-- Letzte Aktivitaeten (aus Log oder Bestellungen)
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spDashboardAktivitaeten')
    DROP PROCEDURE NOVVIA.spDashboardAktivitaeten;
GO

CREATE PROCEDURE NOVVIA.spDashboardAktivitaeten
    @nTop INT = 20
AS
BEGIN
    SET NOCOUNT ON;

    -- Letzte Bestellungen
    SELECT TOP (@nTop)
        'Auftrag' AS Typ,
        'Auftrag ' + b.cBestellNr + ' erstellt' AS Beschreibung,
        COALESCE(k.cFirma, k.cVorname + ' ' + k.cName) AS Kunde,
        b.fGesamtBrutto AS Betrag,
        b.dErstellt AS Zeitpunkt
    FROM tBestellung b
    LEFT JOIN tKunde k ON b.kKunde = k.kKunde
    WHERE b.nStorno = 0
    ORDER BY b.dErstellt DESC;
END
GO

PRINT 'SP NOVVIA.spDashboardAktivitaeten erstellt.';
GO

-- =====================================================
-- SP: NOVVIA.spDashboardMahnungen
-- Offene Mahnungen
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spDashboardMahnungen')
    DROP PROCEDURE NOVVIA.spDashboardMahnungen;
GO

CREATE PROCEDURE NOVVIA.spDashboardMahnungen
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        CASE nMahnstufe
            WHEN 1 THEN 'Mahnstufe 1'
            WHEN 2 THEN 'Mahnstufe 2'
            WHEN 3 THEN 'Mahnstufe 3'
            ELSE 'Inkasso'
        END AS Mahnstufe,
        nMahnstufe AS MahnstufeNr,
        COUNT(*) AS Anzahl,
        SUM(r.fGesamtBrutto - ISNULL(r.fBezahlt, 0)) AS OffenerBetrag
    FROM tRechnung r
    WHERE r.nMahnstufe > 0
      AND r.nStatus = 1
      AND r.nStorno = 0
    GROUP BY nMahnstufe
    ORDER BY nMahnstufe;
END
GO

PRINT 'SP NOVVIA.spDashboardMahnungen erstellt.';
GO

-- =====================================================
-- Zusammenfassung
-- =====================================================
PRINT '';
PRINT '=====================================================';
PRINT 'NOVVIA Dashboard Stored Procedures installiert.';
PRINT '';
PRINT 'Verfuegbare SPs:';
PRINT '  NOVVIA.spDashboardKPIs           - Haupt-Kennzahlen';
PRINT '  NOVVIA.spDashboardUmsatzVerlauf  - Umsatz 12 Monate';
PRINT '  NOVVIA.spDashboardTopKunden      - Top Kunden';
PRINT '  NOVVIA.spDashboardTopArtikel     - Top Artikel';
PRINT '  NOVVIA.spDashboardAuftragStatus  - Status-Verteilung';
PRINT '  NOVVIA.spDashboardZahlungseingang- Zahlungen/Woche';
PRINT '  NOVVIA.spDashboardLagerAlarm     - Niedrige Bestaende';
PRINT '  NOVVIA.spDashboardAktivitaeten   - Letzte Aktivitaeten';
PRINT '  NOVVIA.spDashboardMahnungen      - Mahnungen';
PRINT '=====================================================';
GO

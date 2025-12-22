-- ============================================
-- NOVVIA ERP - Bestellung View mit MSV3/MSVE Daten
-- JTL-konform mit MSV3-Verfügbarkeit, MHD, Chargen
-- ============================================

USE Mandant_3;
GO

-- ============================================
-- View: vNOVVIA_BestellungMSV3
-- Lieferantenbestellungen mit MSV3-Daten
-- ============================================
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vNOVVIA_BestellungMSV3' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    DROP VIEW NOVVIA.vNOVVIA_BestellungMSV3;
END
GO

CREATE VIEW NOVVIA.vNOVVIA_BestellungMSV3
AS
SELECT
    -- Bestellkopf
    lb.kLieferantenBestellung,
    lb.cBestellNr,
    lb.dErstellt                    AS dBestellDatum,
    lb.dLiefertermin,
    lb.nStatus                      AS nBestellStatus,
    CASE lb.nStatus
        WHEN 0 THEN 'Offen'
        WHEN 1 THEN 'In Bearbeitung'
        WHEN 2 THEN 'Bestellt'
        WHEN 3 THEN 'Teilgeliefert'
        WHEN 4 THEN 'Geliefert'
        WHEN 5 THEN 'Abgeschlossen'
        WHEN 6 THEN 'Storniert'
        ELSE 'Unbekannt'
    END                             AS cBestellStatusText,
    lb.fGesamtNetto,
    lb.fGesamtBrutto,
    lb.cWaehrung,
    lb.cAnmerkung                   AS cBestellAnmerkung,

    -- Lieferant
    l.kLieferant,
    l.cLiefNr                       AS cLieferantenNr,
    l.cFirma                        AS cLieferantFirma,
    l.cStrasse                      AS cLieferantStrasse,
    l.cPLZ                          AS cLieferantPLZ,
    l.cOrt                          AS cLieferantOrt,
    l.cLand                         AS cLieferantLand,
    l.cTel                          AS cLieferantTel,
    l.cMail                         AS cLieferantMail,

    -- MSV3 Lieferanten-Config
    msv3l.kMSV3Lieferant,
    msv3l.cMSV3Url,
    msv3l.cMSV3Kundennummer,
    msv3l.cMSV3Filiale,
    msv3l.nMSV3Version,
    CASE WHEN msv3l.kMSV3Lieferant IS NOT NULL THEN 1 ELSE 0 END AS nHatMSV3,

    -- MSV3 Bestellung
    msv3b.kMSV3Bestellung,
    msv3b.cMSV3AuftragsId,
    msv3b.cMSV3Status,
    msv3b.dGesendet                 AS dMSV3Gesendet,
    msv3b.dBestaetigt               AS dMSV3Bestaetigt,
    msv3b.dLieferung                AS dMSV3Lieferung,
    msv3b.nAnzahlPositionen         AS nMSV3AnzahlPos,
    msv3b.nAnzahlVerfuegbar         AS nMSV3Verfuegbar,
    msv3b.nAnzahlNichtVerfuegbar    AS nMSV3NichtVerfuegbar,
    msv3b.cFehler                   AS cMSV3Fehler,

    -- Position
    lbp.kLieferantenBestellungPos,
    lbp.nPos                        AS nPosNr,
    lbp.fAnzahl                     AS fMengeBestellt,
    lbp.fEKNetto                    AS fEKNetto,
    lbp.fMwSt,

    -- Artikel
    a.kArtikel,
    a.cArtNr,
    a.cName                         AS cArtikelName,
    a.cBarcode                      AS cEAN,
    a.cHAN                          AS cHerstellerArtNr,
    a.fLagerbestand,
    a.fMindestbestand,

    -- ABdata PZN Mapping
    abm.cPZN,

    -- ABdata Pharma-Stammdaten
    abd.cName                       AS cABdataName,
    abd.cHersteller                 AS cPharmaHersteller,
    abd.cDarreichungsform,
    abd.cPackungsgroesse,
    abd.fAEP                        AS fABdataAEP,
    abd.fAVP                        AS fABdataAVP,
    abd.nRezeptpflicht,
    abd.nBTM,
    abd.nKuehlpflichtig,
    abd.cATC,
    abd.cWirkstoff,

    -- MSV3 Positions-Daten (MSVE Verfügbarkeit + MHD)
    msv3p.kMSV3BestellungPos,
    msv3p.fMengeVerfuegbar          AS fMSVEBestand,         -- MSVE Bestand vom Großhandel
    msv3p.fMengeGeliefert           AS fMengeGeliefert,
    msv3p.cStatus                   AS cMSV3PosStatus,
    msv3p.fPreisEK                  AS fMSV3PreisEK,
    msv3p.fPreisAEP                 AS fMSV3PreisAEP,
    msv3p.fPreisAVP                 AS fMSV3PreisAVP,
    msv3p.dMHD,                                              -- Mindesthaltbarkeitsdatum
    msv3p.cChargenNr,                                        -- Chargennummer
    msv3p.cHinweis                  AS cMSV3Hinweis,

    -- Berechnete Felder
    CASE
        WHEN msv3p.fMengeVerfuegbar IS NULL THEN NULL
        WHEN msv3p.fMengeVerfuegbar >= lbp.fAnzahl THEN 'Verfuegbar'
        WHEN msv3p.fMengeVerfuegbar > 0 THEN 'Teilweise'
        ELSE 'Nicht verfuegbar'
    END                             AS cVerfuegbarkeitsStatus,

    CASE
        WHEN msv3p.dMHD IS NULL THEN NULL
        WHEN msv3p.dMHD < GETDATE() THEN 'ABGELAUFEN'
        WHEN msv3p.dMHD < DATEADD(MONTH, 3, GETDATE()) THEN 'KURZ_MHD'
        WHEN msv3p.dMHD < DATEADD(MONTH, 6, GETDATE()) THEN 'MITTEL_MHD'
        ELSE 'LANG_MHD'
    END                             AS cMHDStatus,

    DATEDIFF(DAY, GETDATE(), msv3p.dMHD) AS nRestlaufzeitTage

FROM tLieferantenBestellung lb
    INNER JOIN tLieferant l ON lb.kLieferant = l.kLieferant
    INNER JOIN tLieferantenBestellungPos lbp ON lb.kLieferantenBestellung = lbp.kLieferantenBestellung
    INNER JOIN tArtikel a ON lbp.kArtikel = a.kArtikel

    -- MSV3 Lieferanten-Config (optional)
    LEFT JOIN NOVVIA.MSV3Lieferant msv3l ON l.kLieferant = msv3l.kLieferant AND msv3l.nAktiv = 1

    -- MSV3 Bestellung (optional)
    LEFT JOIN NOVVIA.MSV3Bestellung msv3b ON lb.kLieferantenBestellung = msv3b.kLieferantenBestellung

    -- MSV3 Position (optional)
    LEFT JOIN NOVVIA.MSV3BestellungPos msv3p ON msv3b.kMSV3Bestellung = msv3p.kMSV3Bestellung
        AND lbp.kLieferantenBestellungPos = msv3p.kLieferantenBestellungPos

    -- ABdata PZN Mapping (optional)
    LEFT JOIN NOVVIA.ABdataArtikelMapping abm ON a.kArtikel = abm.kArtikel

    -- ABdata Pharma-Stammdaten (optional, via PZN)
    LEFT JOIN NOVVIA.ABdataArtikel abd ON abm.cPZN = abd.cPZN AND abd.nAktiv = 1
GO

PRINT 'View NOVVIA.vNOVVIA_BestellungMSV3 erstellt';
GO

-- ============================================
-- View: vNOVVIA_BestellungMSV3Kopf
-- Aggregierte Kopfdaten mit MSV3-Summary
-- ============================================
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vNOVVIA_BestellungMSV3Kopf' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    DROP VIEW NOVVIA.vNOVVIA_BestellungMSV3Kopf;
END
GO

CREATE VIEW NOVVIA.vNOVVIA_BestellungMSV3Kopf
AS
SELECT
    lb.kLieferantenBestellung,
    lb.cBestellNr,
    lb.dErstellt                    AS dBestellDatum,
    lb.dLiefertermin,
    lb.nStatus,
    CASE lb.nStatus
        WHEN 0 THEN 'Offen'
        WHEN 1 THEN 'In Bearbeitung'
        WHEN 2 THEN 'Bestellt'
        WHEN 3 THEN 'Teilgeliefert'
        WHEN 4 THEN 'Geliefert'
        WHEN 5 THEN 'Abgeschlossen'
        WHEN 6 THEN 'Storniert'
        ELSE 'Unbekannt'
    END                             AS cStatusText,
    lb.fGesamtNetto,
    lb.fGesamtBrutto,
    lb.cWaehrung,

    -- Lieferant
    l.kLieferant,
    l.cLiefNr,
    l.cFirma                        AS cLieferantFirma,

    -- MSV3 Status
    CASE WHEN msv3l.kMSV3Lieferant IS NOT NULL THEN 1 ELSE 0 END AS nHatMSV3,
    msv3b.cMSV3Status,
    msv3b.cMSV3AuftragsId,
    msv3b.dGesendet                 AS dMSV3Gesendet,
    msv3b.dBestaetigt               AS dMSV3Bestaetigt,

    -- Aggregierte Position-Counts
    (SELECT COUNT(*) FROM tLieferantenBestellungPos WHERE kLieferantenBestellung = lb.kLieferantenBestellung) AS nAnzahlPositionen,
    msv3b.nAnzahlVerfuegbar,
    msv3b.nAnzahlNichtVerfuegbar,

    -- MHD Warnungen
    (SELECT COUNT(*)
     FROM NOVVIA.MSV3BestellungPos p
     WHERE p.kMSV3Bestellung = msv3b.kMSV3Bestellung
       AND p.dMHD IS NOT NULL
       AND p.dMHD < DATEADD(MONTH, 3, GETDATE())) AS nAnzahlKurzMHD,

    -- Chargen mit MHD
    (SELECT COUNT(*)
     FROM NOVVIA.MSV3BestellungPos p
     WHERE p.kMSV3Bestellung = msv3b.kMSV3Bestellung
       AND p.cChargenNr IS NOT NULL) AS nAnzahlMitCharge

FROM tLieferantenBestellung lb
    INNER JOIN tLieferant l ON lb.kLieferant = l.kLieferant
    LEFT JOIN NOVVIA.MSV3Lieferant msv3l ON l.kLieferant = msv3l.kLieferant AND msv3l.nAktiv = 1
    LEFT JOIN NOVVIA.MSV3Bestellung msv3b ON lb.kLieferantenBestellung = msv3b.kLieferantenBestellung
GO

PRINT 'View NOVVIA.vNOVVIA_BestellungMSV3Kopf erstellt';
GO

-- ============================================
-- View: vNOVVIA_MSVEBestandMHD
-- Fokussiert auf MSVE Bestand und MHD-Daten
-- ============================================
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vNOVVIA_MSVEBestandMHD' AND schema_id = SCHEMA_ID('NOVVIA'))
BEGIN
    DROP VIEW NOVVIA.vNOVVIA_MSVEBestandMHD;
END
GO

CREATE VIEW NOVVIA.vNOVVIA_MSVEBestandMHD
AS
SELECT
    -- Artikel-Identifikation
    a.kArtikel,
    a.cArtNr,
    a.cName                         AS cArtikelName,
    a.cBarcode                      AS cEAN,
    abm.cPZN,

    -- JTL Lagerbestand
    a.fLagerbestand                 AS fJTLBestand,
    a.fMindestbestand,

    -- MSVE Bestand vom Großhandel (letzte Abfrage)
    msv3p.fMengeVerfuegbar          AS fMSVEBestand,
    msv3p.cStatus                   AS cMSVEStatus,

    -- MHD und Charge
    msv3p.dMHD,
    msv3p.cChargenNr,
    DATEDIFF(DAY, GETDATE(), msv3p.dMHD) AS nRestlaufzeitTage,
    CASE
        WHEN msv3p.dMHD IS NULL THEN 'KEIN_MHD'
        WHEN msv3p.dMHD < GETDATE() THEN 'ABGELAUFEN'
        WHEN msv3p.dMHD < DATEADD(MONTH, 3, GETDATE()) THEN 'KURZ'
        WHEN msv3p.dMHD < DATEADD(MONTH, 6, GETDATE()) THEN 'MITTEL'
        ELSE 'LANG'
    END                             AS cMHDKategorie,

    -- Preise vom Großhandel
    msv3p.fPreisEK,
    msv3p.fPreisAEP,
    msv3p.fPreisAVP,

    -- Pharma-Stammdaten
    abd.cHersteller,
    abd.cDarreichungsform,
    abd.cPackungsgroesse,
    abd.nRezeptpflicht,
    abd.nKuehlpflichtig,
    abd.nBTM,
    abd.cATC,
    abd.cWirkstoff,

    -- Lieferant
    l.kLieferant,
    l.cFirma                        AS cLieferant,

    -- Zeitstempel
    msv3b.dGesendet                 AS dLetzteAbfrage,
    msv3b.cMSV3Status

FROM tArtikel a
    -- PZN Mapping
    LEFT JOIN NOVVIA.ABdataArtikelMapping abm ON a.kArtikel = abm.kArtikel

    -- ABdata Stammdaten
    LEFT JOIN NOVVIA.ABdataArtikel abd ON abm.cPZN = abd.cPZN AND abd.nAktiv = 1

    -- Letzte MSV3-Abfrage pro Artikel (via Bestellposition)
    OUTER APPLY (
        SELECT TOP 1
            mp.fMengeVerfuegbar,
            mp.cStatus,
            mp.dMHD,
            mp.cChargenNr,
            mp.fPreisEK,
            mp.fPreisAEP,
            mp.fPreisAVP,
            mb.dGesendet,
            mb.cMSV3Status,
            mb.kMSV3Lieferant
        FROM NOVVIA.MSV3BestellungPos mp
            INNER JOIN NOVVIA.MSV3Bestellung mb ON mp.kMSV3Bestellung = mb.kMSV3Bestellung
            INNER JOIN tLieferantenBestellungPos lbp ON mp.kLieferantenBestellungPos = lbp.kLieferantenBestellungPos
        WHERE lbp.kArtikel = a.kArtikel
        ORDER BY mb.dGesendet DESC
    ) msv3p

    -- Lieferant
    LEFT JOIN NOVVIA.MSV3Lieferant msv3l ON msv3p.kMSV3Lieferant = msv3l.kMSV3Lieferant
    LEFT JOIN tLieferant l ON msv3l.kLieferant = l.kLieferant

WHERE abm.cPZN IS NOT NULL  -- Nur Artikel mit PZN-Mapping
GO

PRINT 'View NOVVIA.vNOVVIA_MSVEBestandMHD erstellt';
GO

-- ============================================
-- Index-Empfehlungen für Performance
-- ============================================
/*
-- Für bessere Performance der Views empfohlen:

CREATE INDEX IX_LiefBestPos_Artikel ON tLieferantenBestellungPos(kArtikel);
CREATE INDEX IX_MSV3BestPos_MHD ON NOVVIA.MSV3BestellungPos(dMHD);
CREATE INDEX IX_MSV3BestPos_Charge ON NOVVIA.MSV3BestellungPos(cChargenNr);
CREATE INDEX IX_MSV3Bestellung_Gesendet ON NOVVIA.MSV3Bestellung(dGesendet DESC);
*/

PRINT '';
PRINT '============================================';
PRINT 'NOVVIA Bestellung Views erfolgreich erstellt!';
PRINT '';
PRINT 'Verfuegbare Views:';
PRINT '  - NOVVIA.vNOVVIA_BestellungMSV3      (Detail-View)';
PRINT '  - NOVVIA.vNOVVIA_BestellungMSV3Kopf  (Kopf-View aggregiert)';
PRINT '  - NOVVIA.vNOVVIA_MSVEBestandMHD      (MSVE Bestand + MHD)';
PRINT '============================================';
GO

-- =====================================================
-- NOVVIA ERP - Update auf Basistabellen
-- =====================================================
-- Ersetzt alle JTL-View-Abhaengigkeiten durch Basistabellen
-- fuer maximale Stabilitaet bei JTL-Updates
--
-- Datum: 2026-01-03
-- =====================================================

USE Mandant_2;
GO

PRINT '=====================================================';
PRINT 'NOVVIA ERP - Update auf Basistabellen';
PRINT '=====================================================';
PRINT '';
PRINT 'Dieses Script ersetzt alle JTL-View-Abhaengigkeiten';
PRINT 'durch direkte Tabellenzugriffe.';
PRINT '';

-- =====================================================
-- MAHNSTUFEN (aus tMahnstufe)
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spMahnstufen' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spMahnstufen;
GO

CREATE PROCEDURE NOVVIA.spMahnstufen
AS
BEGIN
    SET NOCOUNT ON;
    SELECT 0 AS nStufe, N'Keine Mahnung' AS cName,
        CAST(0 AS DECIMAL(18,13)) AS fGebuehrPauschal, CAST(0 AS DECIMAL(18,13)) AS fGebuehrZinssatz,
        0 AS nKarenzzeit, 0 AS nZahlungsfristInTagen
    UNION ALL
    SELECT nStufe, cName, fGebuehrPauschal, fGebuehrZinssatz, nKarenzzeit, nZahlungsfristInTagen
    FROM dbo.tMahnstufe WHERE kFirma = 0
    ORDER BY nStufe;
END;
GO

PRINT 'spMahnstufen OK';

-- =====================================================
-- RECHNUNG - Basistabellen
-- =====================================================
-- Tabellen: Rechnung.tRechnung, Rechnung.tRechnungEckdaten,
--           Rechnung.tRechnungAdresse, Rechnung.tRechnungPosition

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spRechnungenAuflisten' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spRechnungenAuflisten;
GO

CREATE PROCEDURE NOVVIA.spRechnungenAuflisten
    @cSuche NVARCHAR(100) = NULL,
    @nStatus INT = NULL,                 -- 0=Offen, 1=Bezahlt, 2=Storniert, 3=Teilbezahlt, 4=Angemahnt
    @nMahnstufe INT = NULL,              -- aus NOVVIA.spMahnstufen, 0=keine
    @kKunde INT = NULL,
    @kPlattform INT = NULL,
    @dVon DATETIME = NULL,
    @dBis DATETIME = NULL,
    @nNurOffene BIT = 0,
    @nNurStornierte BIT = 0,
    @nNurAngemahnt BIT = 0,
    @nLimit INT = 1000
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@nLimit)
        r.kRechnung, r.kKunde, r.cRechnungsnr AS cRechnungsnummer, r.dErstellt, r.dValutadatum,
        r.nStorno, r.nIstEntwurf, r.cKundennr AS cKundeNr, r.nDebitorennr,
        r.cWaehrung, r.kPlattform, r.kZahlungsart, r.cZahlungsart AS cZahlungsartname, r.cVersandart, r.kVersandArt,
        e.fVkNettoGesamt AS fGesamtNettopreis, e.fVkBruttoGesamt AS fGesamtBruttopreis,
        e.fOffenerWert, e.fZahlung AS fBereitsgezahltWert, e.fGutschrift AS fGutgeschriebenerWert,
        CASE WHEN e.fOffenerWert <= 0 AND r.nStorno = 0 THEN 1 ELSE 0 END AS nIstKomplettBezahlt,
        e.nIstAngemahnt, e.nMahnstufe, e.dMahndatum, e.dZahlungsziel, e.dBezahlt AS dBezahldatum,
        e.dDruckdatum, e.dMaildatum, e.nKorrigiert, e.nHasRechnungskorrektur AS nRechnungskorrekturErstellt,
        ISNULL(a.cFirma, LTRIM(RTRIM(ISNULL(a.cVorname, '') + ' ' + ISNULL(a.cName, '')))) AS cKundeName,
        a.cFirma AS cRechnungsadresseFirma, a.cVorname AS cRechnungsadresseVorname,
        a.cName AS cRechnungsadresseNachname, a.cOrt AS cRechnungsadresseOrt,
        s.dStorniert, sg.cStornogrund AS cStornogrund
    FROM Rechnung.tRechnung r
    INNER JOIN Rechnung.tRechnungEckdaten e ON r.kRechnung = e.kRechnung
    LEFT JOIN Rechnung.tRechnungAdresse a ON r.kRechnung = a.kRechnung AND a.nTyp = 0
    LEFT JOIN Rechnung.tRechnungStorno s ON r.kRechnung = s.kRechnung
    LEFT JOIN Rechnung.tRechnungStornogrund sg ON s.kRechnungStornogrund = sg.kRechnungStornogrund
    WHERE (@cSuche IS NULL OR r.cRechnungsnr LIKE '%' + @cSuche + '%' OR r.cKundennr LIKE '%' + @cSuche + '%'
           OR a.cFirma LIKE '%' + @cSuche + '%' OR a.cName LIKE '%' + @cSuche + '%')
        AND (@nStatus IS NULL OR
             (@nStatus = 0 AND e.fOffenerWert > 0 AND r.nStorno = 0) OR
             (@nStatus = 1 AND e.fOffenerWert <= 0 AND r.nStorno = 0) OR
             (@nStatus = 2 AND r.nStorno = 1) OR
             (@nStatus = 3 AND e.fZahlung > 0 AND e.fOffenerWert > 0) OR
             (@nStatus = 4 AND e.nIstAngemahnt = 1))
        AND (@nMahnstufe IS NULL OR (@nMahnstufe = 0 AND e.nMahnstufe IS NULL) OR (@nMahnstufe > 0 AND e.nMahnstufe = @nMahnstufe))
        AND (@kKunde IS NULL OR r.kKunde = @kKunde)
        AND (@kPlattform IS NULL OR r.kPlattform = @kPlattform)
        AND (@dVon IS NULL OR r.dErstellt >= @dVon)
        AND (@dBis IS NULL OR r.dErstellt < DATEADD(DAY, 1, @dBis))
        AND (@nNurOffene = 0 OR e.fOffenerWert > 0)
        AND (@nNurStornierte = 0 OR r.nStorno = 1)
        AND (@nNurAngemahnt = 0 OR e.nIstAngemahnt = 1)
    ORDER BY r.dErstellt DESC, r.kRechnung DESC;
END;
GO

PRINT 'spRechnungenAuflisten OK';

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spRechnungLesen' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spRechnungLesen;
GO

CREATE PROCEDURE NOVVIA.spRechnungLesen
    @kRechnung INT
AS
BEGIN
    SET NOCOUNT ON;
    -- Kopf
    SELECT r.kRechnung, r.kKunde, r.kBenutzer, r.kFirmaHistory AS kFirma, r.kZahlungsart, r.kVersandArt,
        r.kPlattform, r.kShop, r.kSprache, r.cRechnungsnr AS cRechnungsnummer, r.nStorno, r.nIstEntwurf, r.nArchiv,
        r.dErstellt, r.dValutadatum, r.cKundennr AS cKundeNr, r.nDebitorennr, r.cKundengruppe, r.cWaehrung,
        r.cZahlungsart AS cZahlungsartname, r.cVersandart, r.nZahlungszielTage AS nZahlungszielInTagen,
        e.fVkNettoGesamt AS fGesamtNettopreis, e.fVkBruttoGesamt AS fGesamtBruttopreis,
        e.fOffenerWert, e.fZahlung AS fBereitsgezahltWert, e.fGutschrift AS fGutgeschriebenerWert,
        CASE WHEN e.fOffenerWert <= 0 AND r.nStorno = 0 THEN 1 ELSE 0 END AS nIstKomplettBezahlt,
        e.nIstAngemahnt, e.nMahnstufe, e.dMahndatum, e.dZahlungsziel, e.dBezahlt AS dBezahldatum,
        e.dDruckdatum, e.dMaildatum, e.nKorrigiert, e.nHasRechnungskorrektur AS nRechnungskorrekturErstellt,
        ra.cFirma AS cRechnungsadresseFirma, ra.cAnrede AS cRechnungsadresseAnrede,
        ra.cVorname AS cRechnungsadresseVorname, ra.cName AS cRechnungsadresseNachname,
        ra.cStrasse AS cRechnungsadresseStrasse, ra.cPLZ AS cRechnungsadressePlz,
        ra.cOrt AS cRechnungsadresseOrt, ra.cLand AS cRechnungsadresseLand, ra.cISO AS cRechnungsadresseLandIso,
        ra.cTel AS cRechnungsadresseTelefon, ra.cMail AS cRechnungsadresseMail,
        la.cFirma AS cLieferadresseFirma, la.cVorname AS cLieferadresseVorname, la.cName AS cLieferadresseNachname,
        la.cStrasse AS cLieferadresseStrasse, la.cPLZ AS cLieferadressePlz,
        la.cOrt AS cLieferadresseOrt, la.cLand AS cLieferadresseLand,
        s.dStorniert, s.cKommentar AS cStornoKommentar, sg.cStornogrund,
        au.kAuftrag, au.cAuftragsNr AS cAuftragsNr, au.cExterneAuftragsnummer
    FROM Rechnung.tRechnung r
    INNER JOIN Rechnung.tRechnungEckdaten e ON r.kRechnung = e.kRechnung
    LEFT JOIN Rechnung.tRechnungAdresse ra ON r.kRechnung = ra.kRechnung AND ra.nTyp = 0
    LEFT JOIN Rechnung.tRechnungAdresse la ON r.kRechnung = la.kRechnung AND la.nTyp = 1
    LEFT JOIN Rechnung.tRechnungStorno s ON r.kRechnung = s.kRechnung
    LEFT JOIN Rechnung.tRechnungStornogrund sg ON s.kRechnungStornogrund = sg.kRechnungStornogrund
    LEFT JOIN Verkauf.tAuftragRechnung ar ON r.kRechnung = ar.kRechnung
    LEFT JOIN Verkauf.tAuftrag au ON ar.kAuftrag = au.kAuftrag
    WHERE r.kRechnung = @kRechnung;
    -- Positionen
    SELECT p.kRechnungPosition, p.kRechnung, p.kArtikel, p.kAuftrag, p.kAuftragPosition,
        p.cArtNr, p.cName, p.cEinheit, p.fAnzahl, p.fMwSt, p.fVkNetto, p.fRabatt,
        p.fGewicht, p.fVersandgewicht, p.fEkNetto, p.nType, p.nSort
    FROM Rechnung.tRechnungPosition p WHERE p.kRechnung = @kRechnung
    ORDER BY p.nSort, p.kRechnungPosition;
END;
GO

PRINT 'spRechnungLesen OK';

-- =====================================================
-- RECHNUNGSKORREKTUR - Basistabellen
-- =====================================================
-- Tabellen: dbo.tgutschrift, dbo.tGutschriftPos

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spRechnungskorrekturenAuflisten' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spRechnungskorrekturenAuflisten;
GO

CREATE PROCEDURE NOVVIA.spRechnungskorrekturenAuflisten
    @cSuche NVARCHAR(100) = NULL,
    @nNurStornierte BIT = NULL,
    @kKunde INT = NULL,
    @kPlattform INT = NULL,
    @dVon DATETIME = NULL,
    @dBis DATETIME = NULL,
    @nNurStornobelege BIT = 0,
    @nLimit INT = 1000
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@nLimit)
        g.kGutschrift, g.kRechnung, g.kKunde, g.cGutschriftNr, g.cKurzText, g.cText,
        g.fPreis AS fPreisNetto, g.fPreis * (1 + g.fMwSt/100) AS fPreisBrutto,
        g.fMwSt, g.dErstellt, g.cWaehrung, g.kPlattform, g.kBenutzer,
        g.nStorno, g.nStornoTyp, g.dDruckdatum, g.dMaildatum, g.cStatus,
        k.cKundenNr, ISNULL(k.cFirma, LTRIM(RTRIM(ISNULL(k.cVorname, '') + ' ' + ISNULL(k.cName, '')))) AS cKundeName,
        gs.dStorniert, gsg.cStornogrund
    FROM dbo.tgutschrift g
    LEFT JOIN Kunde.tKunde k ON g.kKunde = k.kKunde
    LEFT JOIN dbo.tGutschriftStorno gs ON g.kGutschrift = gs.kGutschrift
    LEFT JOIN dbo.tGutschriftStornogrund gsg ON gs.kGutschriftStornogrund = gsg.kGutschriftStornogrund
    WHERE (@cSuche IS NULL OR g.cGutschriftNr LIKE '%' + @cSuche + '%' OR k.cKundenNr LIKE '%' + @cSuche + '%'
           OR k.cFirma LIKE '%' + @cSuche + '%' OR k.cName LIKE '%' + @cSuche + '%')
        AND (@nNurStornierte IS NULL OR g.nStorno = @nNurStornierte)
        AND (@kKunde IS NULL OR g.kKunde = @kKunde)
        AND (@kPlattform IS NULL OR g.kPlattform = @kPlattform)
        AND (@dVon IS NULL OR g.dErstellt >= @dVon)
        AND (@dBis IS NULL OR g.dErstellt < DATEADD(DAY, 1, @dBis))
        AND (@nNurStornobelege = 0 OR g.nStornoTyp = 1)
    ORDER BY g.dErstellt DESC, g.kGutschrift DESC;
END;
GO

PRINT 'spRechnungskorrekturenAuflisten OK';

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spRechnungskorrekturLesen' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spRechnungskorrekturLesen;
GO

CREATE PROCEDURE NOVVIA.spRechnungskorrekturLesen
    @kGutschrift INT
AS
BEGIN
    SET NOCOUNT ON;
    -- Kopf
    SELECT g.kGutschrift, g.kRechnung, g.kKunde, g.cGutschriftNr, g.cKurzText, g.cText,
        g.fPreis AS fPreisNetto, g.fPreis * (1 + g.fMwSt/100) AS fPreisBrutto,
        g.fMwSt, g.dErstellt, g.cWaehrung, g.fFaktor, g.kFirma, g.kSprache, g.kBenutzer,
        g.cStatus, g.kRechnungsAdresse, g.kPlattform, g.dDruckdatum, g.dMaildatum,
        g.nStorno, g.nStornoTyp, g.nIstExtern, g.cKundeUstId, g.nGutschriftStatus,
        k.cKundenNr, ISNULL(k.cFirma, LTRIM(RTRIM(ISNULL(k.cVorname, '') + ' ' + ISNULL(k.cName, '')))) AS cKundeName,
        gs.dStorniert, gs.cKommentar AS cStornoKommentar, gsg.cStornogrund
    FROM dbo.tgutschrift g
    LEFT JOIN Kunde.tKunde k ON g.kKunde = k.kKunde
    LEFT JOIN dbo.tGutschriftStorno gs ON g.kGutschrift = gs.kGutschrift
    LEFT JOIN dbo.tGutschriftStornogrund gsg ON gs.kGutschriftStornogrund = gsg.kGutschriftStornogrund
    WHERE g.kGutschrift = @kGutschrift;
    -- Positionen
    SELECT p.kGutschriftPos, p.tGutschrift_kGutschrift AS kGutschrift, p.tArtikel_kArtikel AS kArtikel,
        p.cArtNr, p.cString AS cName, p.nAnzahl AS fAnzahl, p.fMwSt,
        p.fVKNetto, p.fVKPreis AS fVKBrutto, p.fRabatt, p.nSort, p.kRechnungPosition
    FROM dbo.tGutschriftPos p WHERE p.tGutschrift_kGutschrift = @kGutschrift
    ORDER BY p.nSort, p.kGutschriftPos;
END;
GO

PRINT 'spRechnungskorrekturLesen OK';

-- =====================================================
-- LIEFERSCHEIN - Basistabellen
-- =====================================================
-- Tabellen: dbo.tLieferschein, dbo.tLieferscheinEckdaten, dbo.tLieferscheinPos

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spLieferscheineAuflisten' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spLieferscheineAuflisten;
GO

CREATE PROCEDURE NOVVIA.spLieferscheineAuflisten
    @cSuche NVARCHAR(100) = NULL,
    @kBestellung INT = NULL,
    @dVon DATETIME = NULL,
    @dBis DATETIME = NULL,
    @nLimit INT = 1000
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@nLimit)
        l.kLieferschein, l.kBestellung, l.kBenutzer, l.cLieferscheinNr, l.cHinweis,
        l.dErstellt, l.dGedruckt, l.dMailVersand, l.nFulfillment,
        b.cKundenNr,
        e.fArtikelGewicht, e.fVersandGewicht, e.nAnzahlPakete, e.nVersandStatus
    FROM dbo.tLieferschein l
    LEFT JOIN dbo.tBestellung b ON l.kBestellung = b.kBestellung
    LEFT JOIN dbo.tLieferscheinEckdaten e ON l.kLieferschein = e.kLieferschein
    WHERE (@cSuche IS NULL OR l.cLieferscheinNr LIKE '%' + @cSuche + '%' OR b.cKundenNr LIKE '%' + @cSuche + '%')
        AND (@kBestellung IS NULL OR l.kBestellung = @kBestellung)
        AND (@dVon IS NULL OR l.dErstellt >= @dVon)
        AND (@dBis IS NULL OR l.dErstellt < DATEADD(DAY, 1, @dBis))
    ORDER BY l.dErstellt DESC, l.kLieferschein DESC;
END;
GO

PRINT 'spLieferscheineAuflisten OK';

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spLieferscheinLesen' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spLieferscheinLesen;
GO

CREATE PROCEDURE NOVVIA.spLieferscheinLesen
    @kLieferschein INT
AS
BEGIN
    SET NOCOUNT ON;
    -- Kopf
    SELECT l.*, e.fArtikelGewicht, e.fVersandGewicht, e.nAnzahlPakete, e.nVersandStatus
    FROM dbo.tLieferschein l
    LEFT JOIN dbo.tLieferscheinEckdaten e ON l.kLieferschein = e.kLieferschein
    WHERE l.kLieferschein = @kLieferschein;
    -- Positionen
    SELECT lp.kLieferscheinPos, lp.kLieferschein, lp.kBestellPos, lp.fAnzahl, lp.cHinweis,
        bp.tArtikel_kArtikel AS kArtikel, bp.cArtNr, bp.cString AS cName, bp.fVkNetto, bp.fMwSt
    FROM dbo.tLieferscheinPos lp
    LEFT JOIN dbo.tBestellpos bp ON lp.kBestellPos = bp.kBestellPos
    WHERE lp.kLieferschein = @kLieferschein;
END;
GO

PRINT 'spLieferscheinLesen OK';

-- =====================================================
-- LIEFERANTENBESTELLUNG - Basistabellen
-- =====================================================
-- Tabellen: dbo.tLieferantenBestellung, dbo.tLieferantenBestellungPos, dbo.tLieferant

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spLieferantenbestellungenAuflisten' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spLieferantenbestellungenAuflisten;
GO

CREATE PROCEDURE NOVVIA.spLieferantenbestellungenAuflisten
    @cSuche NVARCHAR(100) = NULL,
    @kLieferant INT = NULL,
    @nStatus INT = NULL,
    @dVon DATETIME = NULL,
    @dBis DATETIME = NULL,
    @nNurOffen BIT = 0,
    @nLimit INT = 1000
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@nLimit)
        lb.kLieferantenBestellung, lb.kLieferant, lb.cEigeneBestellnummer, lb.cFremdbelegnummer,
        lb.cWaehrungISO, lb.nStatus, lb.dErstellt, lb.dLieferdatum, lb.dGedruckt, lb.dGemailt,
        lb.dAngemahnt, lb.nDropShipping, lb.nBestaetigt, lb.nManuellAbgeschlossen,
        lb.cInternerKommentar, lb.cDruckAnmerkung, lb.cBezugsAuftragsNummer,
        lf.cLiefNr AS cLieferantenNr, lf.cFirma AS cLieferantName,
        (SELECT SUM(p.fMenge * p.fEKNetto) FROM dbo.tLieferantenBestellungPos p WHERE p.kLieferantenBestellung = lb.kLieferantenBestellung) AS fSummeNetto,
        (SELECT SUM(p.fAnzahlOffen) FROM dbo.tLieferantenBestellungPos p WHERE p.kLieferantenBestellung = lb.kLieferantenBestellung) AS fOffeneMenge
    FROM dbo.tLieferantenBestellung lb
    LEFT JOIN dbo.tLieferant lf ON lb.kLieferant = lf.kLieferant
    WHERE lb.nDeleted = 0
        AND (@cSuche IS NULL OR lb.cEigeneBestellnummer LIKE '%' + @cSuche + '%'
             OR lb.cFremdbelegnummer LIKE '%' + @cSuche + '%' OR lf.cFirma LIKE '%' + @cSuche + '%')
        AND (@kLieferant IS NULL OR lb.kLieferant = @kLieferant)
        AND (@nStatus IS NULL OR lb.nStatus = @nStatus)
        AND (@dVon IS NULL OR lb.dErstellt >= @dVon)
        AND (@dBis IS NULL OR lb.dErstellt < DATEADD(DAY, 1, @dBis))
        AND (@nNurOffen = 0 OR lb.nStatus < 3)
    ORDER BY lb.dErstellt DESC, lb.kLieferantenBestellung DESC;
END;
GO

PRINT 'spLieferantenbestellungenAuflisten OK';

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spLieferantenbestellungLesen' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spLieferantenbestellungLesen;
GO

CREATE PROCEDURE NOVVIA.spLieferantenbestellungLesen
    @kLieferantenBestellung INT
AS
BEGIN
    SET NOCOUNT ON;
    -- Kopf
    SELECT lb.*, lf.cLiefNr AS cLieferantenNr, lf.cFirma AS cLieferantName
    FROM dbo.tLieferantenBestellung lb
    LEFT JOIN dbo.tLieferant lf ON lb.kLieferant = lf.kLieferant
    WHERE lb.kLieferantenBestellung = @kLieferantenBestellung;
    -- Positionen
    SELECT p.kLieferantenBestellungPos, p.kLieferantenBestellung, p.kArtikel,
        p.cArtNr, p.cLieferantenArtNr, p.cName, p.cLieferantenBezeichnung,
        p.fMenge, p.fMengeGeliefert, p.fAnzahlOffen, p.fEKNetto, p.fUST,
        p.nPosTyp, p.dLieferdatum, p.nLiefertage, p.cHinweis, p.nSort
    FROM dbo.tLieferantenBestellungPos p
    WHERE p.kLieferantenBestellung = @kLieferantenBestellung
    ORDER BY p.nSort, p.kLieferantenBestellungPos;
END;
GO

PRINT 'spLieferantenbestellungLesen OK';

-- =====================================================
-- EINGANGSRECHNUNG - Basistabellen
-- =====================================================
-- Tabellen: dbo.tEingangsrechnung, dbo.tEingangsrechnungPos

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spEingangsrechnungenAuflisten' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spEingangsrechnungenAuflisten;
GO

CREATE PROCEDURE NOVVIA.spEingangsrechnungenAuflisten
    @cSuche NVARCHAR(100) = NULL,
    @kLieferant INT = NULL,
    @nStatus INT = NULL,
    @dVon DATETIME = NULL,
    @dBis DATETIME = NULL,
    @nNurUnbezahlt BIT = 0,
    @nLimit INT = 1000
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@nLimit)
        er.kEingangsrechnung, er.kLieferant, er.cFremdbelegnummer, er.cEigeneRechnungsnummer,
        er.cLieferant AS cLieferantName, er.nStatus, er.dErstellt, er.dBelegdatum, er.dZahlungsziel, er.dBezahlt,
        er.nZahlungFreigegeben, er.cHinweise,
        er.cStrasse, er.cPLZ, er.cOrt, er.cLandISO, er.cMail,
        lf.cLiefNr AS cLieferantenNr, lf.cFirma AS cLieferantFirma,
        (SELECT SUM(p.fMenge * p.fEKNetto) FROM dbo.tEingangsrechnungPos p WHERE p.kEingangsrechnung = er.kEingangsrechnung) AS fSummeNetto,
        (SELECT SUM(p.fMenge * p.fEKNetto * (1 + p.fMwSt/100)) FROM dbo.tEingangsrechnungPos p WHERE p.kEingangsrechnung = er.kEingangsrechnung) AS fSummeBrutto
    FROM dbo.tEingangsrechnung er
    LEFT JOIN dbo.tLieferant lf ON er.kLieferant = lf.kLieferant
    WHERE er.nDeleted = 0
        AND (@cSuche IS NULL OR er.cFremdbelegnummer LIKE '%' + @cSuche + '%'
             OR er.cEigeneRechnungsnummer LIKE '%' + @cSuche + '%' OR er.cLieferant LIKE '%' + @cSuche + '%')
        AND (@kLieferant IS NULL OR er.kLieferant = @kLieferant)
        AND (@nStatus IS NULL OR er.nStatus = @nStatus)
        AND (@dVon IS NULL OR er.dErstellt >= @dVon)
        AND (@dBis IS NULL OR er.dErstellt < DATEADD(DAY, 1, @dBis))
        AND (@nNurUnbezahlt = 0 OR er.dBezahlt IS NULL)
    ORDER BY er.dErstellt DESC, er.kEingangsrechnung DESC;
END;
GO

PRINT 'spEingangsrechnungenAuflisten OK';

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spEingangsrechnungLesen' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spEingangsrechnungLesen;
GO

CREATE PROCEDURE NOVVIA.spEingangsrechnungLesen
    @kEingangsrechnung INT
AS
BEGIN
    SET NOCOUNT ON;
    -- Kopf
    SELECT er.*, lf.cLiefNr AS cLieferantenNr, lf.cFirma AS cLieferantFirma
    FROM dbo.tEingangsrechnung er
    LEFT JOIN dbo.tLieferant lf ON er.kLieferant = lf.kLieferant
    WHERE er.kEingangsrechnung = @kEingangsrechnung;
    -- Positionen
    SELECT p.kEingangsrechnungPos, p.kEingangsrechnung, p.kArtikel, p.kLieferantenbestellung,
        p.cArtNr, p.cLieferantenArtNr, p.cName, p.cLieferantenBezeichnung, p.cEinheit,
        p.fMenge, p.fEKNetto, p.fMwSt, p.nPosTyp, p.cHinweis
    FROM dbo.tEingangsrechnungPos p
    WHERE p.kEingangsrechnung = @kEingangsrechnung;
END;
GO

PRINT 'spEingangsrechnungLesen OK';

-- =====================================================
-- ABSCHLUSS
-- =====================================================
PRINT '';
PRINT '=====================================================';
PRINT 'Update auf Basistabellen abgeschlossen!';
PRINT '=====================================================';
PRINT '';
PRINT 'Alle SPs nutzen jetzt JTL-Basistabellen statt Views.';
PRINT 'Bei JTL-Updates sind die SPs dadurch stabiler.';
PRINT '';
PRINT 'Verwendete Basistabellen:';
PRINT '  Rechnung: Rechnung.tRechnung, Rechnung.tRechnungEckdaten,';
PRINT '            Rechnung.tRechnungAdresse, Rechnung.tRechnungPosition';
PRINT '  Gutschrift: dbo.tgutschrift, dbo.tGutschriftPos';
PRINT '  Lieferschein: dbo.tLieferschein, dbo.tLieferscheinPos';
PRINT '  Lieferantenbestellung: dbo.tLieferantenBestellung, dbo.tLieferantenBestellungPos';
PRINT '  Eingangsrechnung: dbo.tEingangsrechnung, dbo.tEingangsrechnungPos';
PRINT '  Mahnstufen: dbo.tMahnstufe';
PRINT '';
GO

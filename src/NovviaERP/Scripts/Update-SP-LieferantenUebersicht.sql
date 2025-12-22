-- Update spNOVVIA_LieferantenUebersicht mit zusaetzlichen Feldern

ALTER PROCEDURE spNOVVIA_LieferantenUebersicht
    @cSuche NVARCHAR(100) = NULL,
    @nNurAktive BIT = 1,
    @nNurMSV3 BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        l.kLieferant AS KLieferant,
        l.cLiefNr AS CLiefNr,
        l.cFirma AS CFirma,
        l.cStrasse AS CStrasse,
        l.cPLZ AS CPLZ,
        l.cOrt AS COrt,
        ISNULL(l.cTelZentralle, l.cTelDurchwahl) AS CTel,
        l.cEMail AS CEmail,
        l.cAktiv AS CAktiv,
        ISNULL(l.fMindestbestellwert, 0) AS FMindestbestellwert,
        ISNULL(l.nZahlungsziel, 0) AS NZahlungsziel,
        ISNULL(l.fSkonto, 0) AS FSkonto,
        m.kMSV3Lieferant AS KMSV3Lieferant,
        m.cMSV3Url AS CMSV3Url,
        ISNULL(m.nMSV3Version, 1) AS NMSV3Version,
        CAST(CASE WHEN m.kMSV3Lieferant IS NOT NULL AND m.nAktiv = 1 THEN 1 ELSE 0 END AS BIT) AS NHatMSV3,
        ISNULL((SELECT COUNT(*) FROM tLieferantenBestellung b WHERE b.kLieferant = l.kLieferant AND b.nStatus IN (1,2) AND ISNULL(b.nDeleted,0) = 0), 0) AS NOffeneBestellungen,
        ISNULL((SELECT COUNT(*) FROM tEingangsrechnung e WHERE e.kLieferant = l.kLieferant AND e.nStatus = 0 AND ISNULL(e.nDeleted,0) = 0), 0) AS NOffeneRechnungen
    FROM tlieferant l
    LEFT JOIN NOVVIA.MSV3Lieferant m ON l.kLieferant = m.kLieferant
    WHERE (@nNurAktive = 0 OR l.cAktiv = 'Y')
      AND (@nNurMSV3 = 0 OR m.kMSV3Lieferant IS NOT NULL)
      AND (@cSuche IS NULL OR l.cFirma LIKE '%' + @cSuche + '%' OR l.cLiefNr LIKE '%' + @cSuche + '%' OR l.cOrt LIKE '%' + @cSuche + '%')
    ORDER BY l.cFirma;
END
GO

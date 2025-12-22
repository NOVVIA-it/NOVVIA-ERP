-- Stored Procedure: Lieferanten Übersicht für NovviaERP
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_LieferantenUebersicht')
    DROP PROCEDURE sp_LieferantenUebersicht
GO

CREATE PROCEDURE sp_LieferantenUebersicht
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
        COALESCE(l.cTelZentralle, l.cTelDurchwahl, '') AS CTel,
        l.cEMail AS CEmail,
        CASE WHEN m.kMSV3Lieferant IS NOT NULL AND m.nAktiv = 1 THEN 1 ELSE 0 END AS NHatMSV3,
        ISNULL((SELECT COUNT(*) FROM tLieferantenBestellung b WHERE b.kLieferant = l.kLieferant AND b.nStatus IN (1,2)), 0) AS NOffeneBestellungen,
        ISNULL((SELECT COUNT(*) FROM tEingangsrechnung r WHERE r.kLieferant = l.kLieferant AND r.dBezahlt IS NULL), 0) AS NOffeneRechnungen
    FROM tLieferant l
    LEFT JOIN NOVVIA.MSV3Lieferant m ON m.kLieferant = l.kLieferant
    WHERE l.cAktiv = 'Y'
    ORDER BY l.cFirma
END
GO

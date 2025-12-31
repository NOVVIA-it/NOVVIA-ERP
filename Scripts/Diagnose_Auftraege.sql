-- Diagnose: Vergleich Auftrag 2901 (JTL) vs 2902 (NovviaERP)
USE [Mandant_1]
GO

PRINT '=== 1. AUFTRAG HEADER ==='
SELECT kAuftrag, cAuftragsnr, kKunde, dErstellt, nStorno, nAuftragStatus, nKomplettAusgeliefert
FROM Verkauf.tAuftrag WHERE kAuftrag IN (2901, 2902) ORDER BY kAuftrag;

PRINT ''
PRINT '=== 2. ECKDATEN (WICHTIG!) ==='
SELECT e.kAuftrag, e.fWertNetto, e.fWertBrutto, e.fZahlung, e.fOffenerWert, 
       e.nZahlungStatus, e.nRechnungStatus, e.nLieferstatus, e.cRechnungsnummern
FROM Verkauf.tAuftragEckdaten e WHERE e.kAuftrag IN (2901, 2902) ORDER BY e.kAuftrag;

PRINT ''
PRINT '=== 3. POSITIONEN ==='
SELECT ap.kAuftrag, ap.kAuftragPosition, ap.cArtNr, ap.cName, ap.fAnzahl, ap.fVKNetto, ap.fMwSt
FROM Verkauf.tAuftragPosition ap WHERE ap.kAuftrag IN (2901, 2902) ORDER BY ap.kAuftrag, ap.nSort;

PRINT ''
PRINT '=== 4. POSITIONS-ECKDATEN ==='
SELECT ap.kAuftrag, pe.kAuftragPosition, pe.fVKNetto, pe.fVKBrutto, pe.fPreis, pe.fPreisBrutto
FROM Verkauf.tAuftragPositionEckdaten pe
JOIN Verkauf.tAuftragPosition ap ON pe.kAuftragPosition = ap.kAuftragPosition
WHERE ap.kAuftrag IN (2901, 2902) ORDER BY ap.kAuftrag;

PRINT ''
PRINT '=== 5. CHECK: FEHLENDE ECKDATEN? ==='
SELECT 'Auftrag ' + CAST(a.kAuftrag AS VARCHAR) AS Problem,
       CASE WHEN e.kAuftrag IS NULL THEN 'KEINE ECKDATEN!' ELSE 'OK' END AS Status
FROM Verkauf.tAuftrag a
LEFT JOIN Verkauf.tAuftragEckdaten e ON a.kAuftrag = e.kAuftrag
WHERE a.kAuftrag IN (2901, 2902);

PRINT ''
PRINT '=== 6. FIX: ECKDATEN NEU BERECHNEN (für 2902) ==='
DECLARE @Auftraege Verkauf.TYPE_spAuftragEckdatenBerechnen;
INSERT INTO @Auftraege (kAuftrag) VALUES (2902);
EXEC Verkauf.spAuftragEckdatenBerechnen @Auftrag = @Auftraege;
PRINT 'spAuftragEckdatenBerechnen für 2902 ausgeführt!'

PRINT ''
PRINT '=== 7. ECKDATEN NACH FIX ==='
SELECT e.kAuftrag, e.fWertNetto, e.fWertBrutto, e.fOffenerWert, e.nZahlungStatus, e.nLieferstatus
FROM Verkauf.tAuftragEckdaten e WHERE e.kAuftrag IN (2901, 2902) ORDER BY e.kAuftrag;
GO

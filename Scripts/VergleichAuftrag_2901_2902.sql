-- Vergleich Auftrag 2901 (JTL) vs 2902 (NovviaERP)
-- Und Fix durch spAuftragEckdatenBerechnen

-- 1. Auftragsdaten vergleichen
PRINT '=== VERGLEICH AUFTRAGSDATEN ==='
SELECT
    a.kAuftrag,
    a.cAuftragsnr,
    a.kKunde,
    a.dErstellt,
    a.nStorno,
    a.nAuftragStatus,
    a.nKomplettAusgeliefert,
    'Verkauf.tAuftrag' AS Quelle
FROM Verkauf.tAuftrag a
WHERE a.kAuftrag IN (2901, 2902)
ORDER BY a.kAuftrag;

-- 2. Eckdaten vergleichen (WICHTIG - hier fehlen die Werte bei 2902!)
PRINT '=== VERGLEICH ECKDATEN ==='
SELECT
    e.kAuftrag,
    e.fWertNetto,
    e.fWertBrutto,
    e.fZahlung,
    e.fGutschrift,
    e.fOffenerWert,
    e.nZahlungStatus,
    e.nRechnungStatus,
    e.nLieferstatus,
    e.nAnzahlPakete,
    e.nAnzahlVersendetePakete,
    e.cRechnungsnummern,
    'Verkauf.tAuftragEckdaten' AS Quelle
FROM Verkauf.tAuftragEckdaten e
WHERE e.kAuftrag IN (2901, 2902)
ORDER BY e.kAuftrag;

-- 3. Positionen vergleichen
PRINT '=== VERGLEICH POSITIONEN ==='
SELECT
    ap.kAuftrag,
    ap.kAuftragPosition,
    ap.kArtikel,
    ap.cArtNr,
    ap.cName,
    ap.fAnzahl,
    ap.fVKNetto,
    ap.fRabatt,
    ap.fMwSt,
    ap.nSort,
    'Verkauf.tAuftragPosition' AS Quelle
FROM Verkauf.tAuftragPosition ap
WHERE ap.kAuftrag IN (2901, 2902)
ORDER BY ap.kAuftrag, ap.nSort;

-- 4. Positions-Eckdaten vergleichen (WICHTIG!)
PRINT '=== VERGLEICH POSITIONS-ECKDATEN ==='
SELECT
    pe.kAuftragPosition,
    ap.kAuftrag,
    pe.fVKNetto,
    pe.fVKBrutto,
    pe.fRabatt,
    pe.fMwSt,
    pe.fPreis,
    pe.fPreisBrutto,
    'Verkauf.tAuftragPositionEckdaten' AS Quelle
FROM Verkauf.tAuftragPositionEckdaten pe
JOIN Verkauf.tAuftragPosition ap ON pe.kAuftragPosition = ap.kAuftragPosition
WHERE ap.kAuftrag IN (2901, 2902)
ORDER BY ap.kAuftrag, ap.nSort;

-- 5. Adressen vergleichen
PRINT '=== VERGLEICH ADRESSEN ==='
SELECT
    aa.kAuftrag,
    aa.nTyp,
    CASE aa.nTyp WHEN 0 THEN 'Rechnungsadresse' WHEN 1 THEN 'Lieferadresse' ELSE 'Sonstige' END AS AdressTyp,
    aa.cFirma,
    aa.cVorname,
    aa.cName,
    aa.cStrasse,
    aa.cPLZ,
    aa.cOrt,
    'Verkauf.tAuftragAdresse' AS Quelle
FROM Verkauf.tAuftragAdresse aa
WHERE aa.kAuftrag IN (2901, 2902)
ORDER BY aa.kAuftrag, aa.nTyp;

-- 6. PRUEFEN: Hat 2902 ueberhaupt Eckdaten?
PRINT '=== CHECK: HAT 2902 ECKDATEN? ==='
IF NOT EXISTS (SELECT 1 FROM Verkauf.tAuftragEckdaten WHERE kAuftrag = 2902)
BEGIN
    PRINT 'FEHLER: Auftrag 2902 hat KEINE Eckdaten in tAuftragEckdaten!'
    PRINT 'Loesung: spAuftragEckdatenBerechnen ausfuehren!'
END
ELSE
BEGIN
    PRINT 'OK: Auftrag 2902 hat Eckdaten'
END

-- 7. PRUEFEN: Hat 2902 Positions-Eckdaten?
PRINT '=== CHECK: HAT 2902 POSITIONS-ECKDATEN? ==='
SELECT
    'Auftrag ' + CAST(ap.kAuftrag AS VARCHAR) AS Auftrag,
    COUNT(ap.kAuftragPosition) AS AnzahlPositionen,
    COUNT(pe.kAuftragPosition) AS AnzahlMitEckdaten
FROM Verkauf.tAuftragPosition ap
LEFT JOIN Verkauf.tAuftragPositionEckdaten pe ON ap.kAuftragPosition = pe.kAuftragPosition
WHERE ap.kAuftrag IN (2901, 2902)
GROUP BY ap.kAuftrag;

GO

-- ==============================================================
-- FIX: Eckdaten fuer Auftrag 2902 neu berechnen
-- ==============================================================
PRINT ''
PRINT '=== JETZT ECKDATEN NEU BERECHNEN ==='
PRINT ''

-- Table-Valued Parameter fuer die SP erstellen
DECLARE @Auftraege Verkauf.TYPE_spAuftragEckdatenBerechnen;
INSERT INTO @Auftraege (kAuftrag) VALUES (2902);

-- SP ausfuehren
EXEC Verkauf.spAuftragEckdatenBerechnen @Auftrag = @Auftraege;

PRINT 'spAuftragEckdatenBerechnen fuer Auftrag 2902 ausgefuehrt!'

GO

-- 8. NACH dem Fix: Eckdaten nochmal anzeigen
PRINT ''
PRINT '=== ECKDATEN NACH DEM FIX ==='
SELECT
    e.kAuftrag,
    e.fWertNetto,
    e.fWertBrutto,
    e.fOffenerWert,
    e.nZahlungStatus,
    e.nRechnungStatus,
    e.nLieferstatus
FROM Verkauf.tAuftragEckdaten e
WHERE e.kAuftrag IN (2901, 2902)
ORDER BY e.kAuftrag;

-- 9. Positions-Eckdaten nach dem Fix
PRINT '=== POSITIONS-ECKDATEN NACH DEM FIX ==='
SELECT
    ap.kAuftrag,
    ap.kAuftragPosition,
    ap.cArtNr,
    pe.fVKNetto,
    pe.fVKBrutto,
    pe.fPreis,
    pe.fPreisBrutto
FROM Verkauf.tAuftragPosition ap
LEFT JOIN Verkauf.tAuftragPositionEckdaten pe ON ap.kAuftragPosition = pe.kAuftragPosition
WHERE ap.kAuftrag IN (2901, 2902)
ORDER BY ap.kAuftrag, ap.nSort;

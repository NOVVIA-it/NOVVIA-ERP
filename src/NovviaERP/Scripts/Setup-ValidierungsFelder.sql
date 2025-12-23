-- =============================================
-- NOVVIA ERP - Validierungsfelder für Artikel und Kunde
-- Felder: Ambient, Cool, Medcan, Tierarznei, QualifiziertAm, QualifiziertVon, GDP, GMP
-- =============================================

-- Prüfen ob tEigenesFeld existiert
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'tEigenesFeld')
BEGIN
    PRINT 'FEHLER: Tabelle tEigenesFeld existiert nicht!';
    RETURN;
END
GO

-- =============================================
-- ARTIKEL FELDER
-- =============================================
PRINT 'Erstelle Validierungsfelder für Artikel...';

-- Ambient
IF NOT EXISTS (SELECT 1 FROM tEigenesFeld WHERE cBereich = 'Artikel' AND cIntName = 'val_ambient')
BEGIN
    INSERT INTO tEigenesFeld (cBereich, cName, cIntName, cTyp, nPflichtfeld, nSichtbarInListe, nSichtbarImDruck, nSortierung, nAktiv, cHinweis)
    VALUES ('Artikel', 'Ambient', 'val_ambient', 'Bool', 0, 0, 0, 100, 1, 'Validierung: Ambient-Lagerung');
    PRINT '  - Ambient erstellt';
END

-- Cool
IF NOT EXISTS (SELECT 1 FROM tEigenesFeld WHERE cBereich = 'Artikel' AND cIntName = 'val_cool')
BEGIN
    INSERT INTO tEigenesFeld (cBereich, cName, cIntName, cTyp, nPflichtfeld, nSichtbarInListe, nSichtbarImDruck, nSortierung, nAktiv, cHinweis)
    VALUES ('Artikel', 'Cool', 'val_cool', 'Bool', 0, 0, 0, 101, 1, 'Validierung: Kühl-Lagerung');
    PRINT '  - Cool erstellt';
END

-- Medcan
IF NOT EXISTS (SELECT 1 FROM tEigenesFeld WHERE cBereich = 'Artikel' AND cIntName = 'val_medcan')
BEGIN
    INSERT INTO tEigenesFeld (cBereich, cName, cIntName, cTyp, nPflichtfeld, nSichtbarInListe, nSichtbarImDruck, nSortierung, nAktiv, cHinweis)
    VALUES ('Artikel', 'Medcan', 'val_medcan', 'Bool', 0, 0, 0, 102, 1, 'Validierung: Medizinalcannabis');
    PRINT '  - Medcan erstellt';
END

-- Tierarznei
IF NOT EXISTS (SELECT 1 FROM tEigenesFeld WHERE cBereich = 'Artikel' AND cIntName = 'val_tierarznei')
BEGIN
    INSERT INTO tEigenesFeld (cBereich, cName, cIntName, cTyp, nPflichtfeld, nSichtbarInListe, nSichtbarImDruck, nSortierung, nAktiv, cHinweis)
    VALUES ('Artikel', 'Tierarznei', 'val_tierarznei', 'Bool', 0, 0, 0, 103, 1, 'Validierung: Tierarzneimittel');
    PRINT '  - Tierarznei erstellt';
END

-- QualifiziertAm
IF NOT EXISTS (SELECT 1 FROM tEigenesFeld WHERE cBereich = 'Artikel' AND cIntName = 'qualifiziert_am')
BEGIN
    INSERT INTO tEigenesFeld (cBereich, cName, cIntName, cTyp, nPflichtfeld, nSichtbarInListe, nSichtbarImDruck, nSortierung, nAktiv, cHinweis)
    VALUES ('Artikel', 'QualifiziertAm', 'qualifiziert_am', 'Date', 0, 0, 0, 110, 1, 'Datum der Qualifizierung');
    PRINT '  - QualifiziertAm erstellt';
END

-- QualifiziertVon
IF NOT EXISTS (SELECT 1 FROM tEigenesFeld WHERE cBereich = 'Artikel' AND cIntName = 'qualifiziert_von')
BEGIN
    INSERT INTO tEigenesFeld (cBereich, cName, cIntName, cTyp, nPflichtfeld, nSichtbarInListe, nSichtbarImDruck, nSortierung, nAktiv, cHinweis)
    VALUES ('Artikel', 'QualifiziertVon', 'qualifiziert_von', 'Text', 0, 0, 0, 111, 1, 'Qualifiziert durch (Person/Stelle)');
    PRINT '  - QualifiziertVon erstellt';
END

-- GDP
IF NOT EXISTS (SELECT 1 FROM tEigenesFeld WHERE cBereich = 'Artikel' AND cIntName = 'gdp')
BEGIN
    INSERT INTO tEigenesFeld (cBereich, cName, cIntName, cTyp, nPflichtfeld, nSichtbarInListe, nSichtbarImDruck, nSortierung, nAktiv, cHinweis)
    VALUES ('Artikel', 'GDP', 'gdp', 'Text', 0, 0, 0, 120, 1, 'Good Distribution Practice');
    PRINT '  - GDP erstellt';
END

-- GMP
IF NOT EXISTS (SELECT 1 FROM tEigenesFeld WHERE cBereich = 'Artikel' AND cIntName = 'gmp')
BEGIN
    INSERT INTO tEigenesFeld (cBereich, cName, cIntName, cTyp, nPflichtfeld, nSichtbarInListe, nSichtbarImDruck, nSortierung, nAktiv, cHinweis)
    VALUES ('Artikel', 'GMP', 'gmp', 'Text', 0, 0, 0, 121, 1, 'Good Manufacturing Practice');
    PRINT '  - GMP erstellt';
END
GO

-- =============================================
-- KUNDE FELDER
-- =============================================
PRINT '';
PRINT 'Erstelle Validierungsfelder für Kunde...';

-- Ambient
IF NOT EXISTS (SELECT 1 FROM tEigenesFeld WHERE cBereich = 'Kunde' AND cIntName = 'val_ambient')
BEGIN
    INSERT INTO tEigenesFeld (cBereich, cName, cIntName, cTyp, nPflichtfeld, nSichtbarInListe, nSichtbarImDruck, nSortierung, nAktiv, cHinweis)
    VALUES ('Kunde', 'Ambient', 'val_ambient', 'Bool', 0, 0, 0, 100, 1, 'Validierung: Ambient-Berechtigung');
    PRINT '  - Ambient erstellt';
END

-- Cool
IF NOT EXISTS (SELECT 1 FROM tEigenesFeld WHERE cBereich = 'Kunde' AND cIntName = 'val_cool')
BEGIN
    INSERT INTO tEigenesFeld (cBereich, cName, cIntName, cTyp, nPflichtfeld, nSichtbarInListe, nSichtbarImDruck, nSortierung, nAktiv, cHinweis)
    VALUES ('Kunde', 'Cool', 'val_cool', 'Bool', 0, 0, 0, 101, 1, 'Validierung: Kühlware-Berechtigung');
    PRINT '  - Cool erstellt';
END

-- Medcan
IF NOT EXISTS (SELECT 1 FROM tEigenesFeld WHERE cBereich = 'Kunde' AND cIntName = 'val_medcan')
BEGIN
    INSERT INTO tEigenesFeld (cBereich, cName, cIntName, cTyp, nPflichtfeld, nSichtbarInListe, nSichtbarImDruck, nSortierung, nAktiv, cHinweis)
    VALUES ('Kunde', 'Medcan', 'val_medcan', 'Bool', 0, 0, 0, 102, 1, 'Validierung: Medizinalcannabis-Berechtigung');
    PRINT '  - Medcan erstellt';
END

-- Tierarznei
IF NOT EXISTS (SELECT 1 FROM tEigenesFeld WHERE cBereich = 'Kunde' AND cIntName = 'val_tierarznei')
BEGIN
    INSERT INTO tEigenesFeld (cBereich, cName, cIntName, cTyp, nPflichtfeld, nSichtbarInListe, nSichtbarImDruck, nSortierung, nAktiv, cHinweis)
    VALUES ('Kunde', 'Tierarznei', 'val_tierarznei', 'Bool', 0, 0, 0, 103, 1, 'Validierung: Tierarzneimittel-Berechtigung');
    PRINT '  - Tierarznei erstellt';
END

-- QualifiziertAm
IF NOT EXISTS (SELECT 1 FROM tEigenesFeld WHERE cBereich = 'Kunde' AND cIntName = 'qualifiziert_am')
BEGIN
    INSERT INTO tEigenesFeld (cBereich, cName, cIntName, cTyp, nPflichtfeld, nSichtbarInListe, nSichtbarImDruck, nSortierung, nAktiv, cHinweis)
    VALUES ('Kunde', 'QualifiziertAm', 'qualifiziert_am', 'Date', 0, 0, 0, 110, 1, 'Datum der Kundenqualifizierung');
    PRINT '  - QualifiziertAm erstellt';
END

-- QualifiziertVon
IF NOT EXISTS (SELECT 1 FROM tEigenesFeld WHERE cBereich = 'Kunde' AND cIntName = 'qualifiziert_von')
BEGIN
    INSERT INTO tEigenesFeld (cBereich, cName, cIntName, cTyp, nPflichtfeld, nSichtbarInListe, nSichtbarImDruck, nSortierung, nAktiv, cHinweis)
    VALUES ('Kunde', 'QualifiziertVon', 'qualifiziert_von', 'Text', 0, 0, 0, 111, 1, 'Qualifiziert durch (Person/Stelle)');
    PRINT '  - QualifiziertVon erstellt';
END

-- GDP
IF NOT EXISTS (SELECT 1 FROM tEigenesFeld WHERE cBereich = 'Kunde' AND cIntName = 'gdp')
BEGIN
    INSERT INTO tEigenesFeld (cBereich, cName, cIntName, cTyp, nPflichtfeld, nSichtbarInListe, nSichtbarImDruck, nSortierung, nAktiv, cHinweis)
    VALUES ('Kunde', 'GDP', 'gdp', 'Text', 0, 0, 0, 120, 1, 'Good Distribution Practice Zertifikat');
    PRINT '  - GDP erstellt';
END

-- GMP
IF NOT EXISTS (SELECT 1 FROM tEigenesFeld WHERE cBereich = 'Kunde' AND cIntName = 'gmp')
BEGIN
    INSERT INTO tEigenesFeld (cBereich, cName, cIntName, cTyp, nPflichtfeld, nSichtbarInListe, nSichtbarImDruck, nSortierung, nAktiv, cHinweis)
    VALUES ('Kunde', 'GMP', 'gmp', 'Text', 0, 0, 0, 121, 1, 'Good Manufacturing Practice Zertifikat');
    PRINT '  - GMP erstellt';
END
GO

PRINT '';
PRINT '===========================================';
PRINT 'Validierungsfelder eingerichtet!';
PRINT 'Felder werden automatisch in der UI angezeigt.';
PRINT '===========================================';

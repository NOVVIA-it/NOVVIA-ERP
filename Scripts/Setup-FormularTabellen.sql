-- NOVVIA ERP - Formular-Tabellen Setup
-- Erstellt Tabellen fuer den Formular-Designer und Vorlagen-Import

-- Schema erstellen falls nicht vorhanden
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'NOVVIA')
    EXEC('CREATE SCHEMA NOVVIA');
GO

-- NOVVIA.Formular - Formular-Definitionen
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='NOVVIA' AND TABLE_NAME='Formular')
BEGIN
    CREATE TABLE NOVVIA.Formular (
        kFormular INT IDENTITY(1,1) PRIMARY KEY,
        cName NVARCHAR(200) NOT NULL,
        cTyp NVARCHAR(50) NOT NULL,
        cPapierFormat NVARCHAR(50) DEFAULT 'A4',
        cElementeJson NVARCHAR(MAX),
        nAktiv BIT DEFAULT 1,
        dErstellt DATETIME DEFAULT GETDATE(),
        dGeaendert DATETIME DEFAULT GETDATE()
    );
    PRINT 'Tabelle NOVVIA.Formular erstellt';
END
GO

-- NOVVIA.FormularBild - Importierte Bilder/Logos
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='NOVVIA' AND TABLE_NAME='FormularBild')
BEGIN
    CREATE TABLE NOVVIA.FormularBild (
        kFormularBild INT IDENTITY(1,1) PRIMARY KEY,
        cName NVARCHAR(200) NOT NULL,
        bDaten VARBINARY(MAX) NOT NULL,
        dErstellt DATETIME DEFAULT GETDATE()
    );
    PRINT 'Tabelle NOVVIA.FormularBild erstellt';
END
GO

-- Standard-Formulare einfuegen falls leer
IF NOT EXISTS (SELECT 1 FROM NOVVIA.Formular)
BEGIN
    INSERT INTO NOVVIA.Formular (cName, cTyp, cPapierFormat, nAktiv) VALUES
    ('Rechnung', 'Rechnung', 'A4', 1),
    ('Lieferschein', 'Lieferschein', 'A4', 1),
    ('Versandetikett', 'Etikett', '100x150', 1),
    ('Artikeletikett', 'Etikett', '62x29', 1),
    ('Mahnung', 'Mahnung', 'A4', 1),
    ('Gutschrift', 'Gutschrift', 'A4', 1),
    ('Angebot', 'Angebot', 'A4', 1),
    ('Auftragsbestaetigung', 'Auftrag', 'A4', 1),
    ('Pickliste', 'Pickliste', 'A4', 1),
    ('Packliste', 'Packliste', 'A4', 1),
    ('Ruecksendeformular', 'Ruecksendung', 'A4', 1),
    ('Barcode-Etikett 40x20', 'Etikett', '40x20', 1),
    ('Regaletikett', 'Etikett', '62x29', 1),
    ('Preisetikett', 'Etikett', '40x30', 1);
    PRINT 'Standard-Formulare eingefuegt';
END
GO

-- Info zu JTL Vorlagen
PRINT '';
PRINT '=== JTL Vorlagen Info ===';
PRINT 'JTL List & Label Vorlagen sind in Report.tVorlage gespeichert:';
SELECT
    cTyp AS [Typ],
    COUNT(*) AS [Anzahl]
FROM Report.tVorlage
GROUP BY cTyp
ORDER BY cTyp;

PRINT '';
PRINT 'Bilder/Logos koennen ueber Einstellungen > Vorlagen-Import exportiert werden.';
GO

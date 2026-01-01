-- NOVVIA Konfigurationstabelle pro Mandant
-- Speichert alle Einstellungen als Key-Value-Paare

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'NOVVIA')
BEGIN
    EXEC('CREATE SCHEMA NOVVIA')
END
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'NOVVIA.Config') AND type = 'U')
BEGIN
    CREATE TABLE NOVVIA.Config (
        kConfig INT IDENTITY(1,1) PRIMARY KEY,
        cKategorie NVARCHAR(50) NOT NULL,           -- z.B. 'Theme', 'Allgemein', 'Druck'
        cSchluessel NVARCHAR(100) NOT NULL,         -- z.B. 'PrimaryColor', 'FirmaName'
        cWert NVARCHAR(MAX) NULL,                   -- Der Wert
        cBeschreibung NVARCHAR(255) NULL,           -- Beschreibung fuer Admin
        dErstellt DATETIME2 DEFAULT GETDATE(),
        dGeaendert DATETIME2 DEFAULT GETDATE(),
        CONSTRAINT UQ_Config_KategorieSchluessel UNIQUE (cKategorie, cSchluessel)
    )

    PRINT 'Tabelle NOVVIA.Config wurde erstellt.'
END
ELSE
BEGIN
    PRINT 'Tabelle NOVVIA.Config existiert bereits.'
END
GO

-- Stored Procedure zum Abrufen einer Konfiguration
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spConfigGet' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spConfigGet
GO

CREATE PROCEDURE NOVVIA.spConfigGet
    @cKategorie NVARCHAR(50),
    @cSchluessel NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @cSchluessel IS NULL
        SELECT cSchluessel, cWert FROM NOVVIA.Config WHERE cKategorie = @cKategorie
    ELSE
        SELECT cWert FROM NOVVIA.Config WHERE cKategorie = @cKategorie AND cSchluessel = @cSchluessel
END
GO

-- Stored Procedure zum Setzen einer Konfiguration
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spConfigSet' AND schema_id = SCHEMA_ID('NOVVIA'))
    DROP PROCEDURE NOVVIA.spConfigSet
GO

CREATE PROCEDURE NOVVIA.spConfigSet
    @cKategorie NVARCHAR(50),
    @cSchluessel NVARCHAR(100),
    @cWert NVARCHAR(MAX),
    @cBeschreibung NVARCHAR(255) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM NOVVIA.Config WHERE cKategorie = @cKategorie AND cSchluessel = @cSchluessel)
    BEGIN
        UPDATE NOVVIA.Config
        SET cWert = @cWert, dGeaendert = GETDATE()
        WHERE cKategorie = @cKategorie AND cSchluessel = @cSchluessel
    END
    ELSE
    BEGIN
        INSERT INTO NOVVIA.Config (cKategorie, cSchluessel, cWert, cBeschreibung)
        VALUES (@cKategorie, @cSchluessel, @cWert, @cBeschreibung)
    END
END
GO

-- Standard-Theme-Werte einfuegen (falls nicht vorhanden)
EXEC NOVVIA.spConfigSet 'Theme', 'PrimaryColor', '#0078D4', 'Primaerfarbe (Buttons, Links)'
EXEC NOVVIA.spConfigSet 'Theme', 'SecondaryColor', '#6C757D', 'Sekundaerfarbe'
EXEC NOVVIA.spConfigSet 'Theme', 'BackgroundColor', '#FFFFFF', 'Hintergrundfarbe'
EXEC NOVVIA.spConfigSet 'Theme', 'HeaderBackgroundColor', '#F8F9FA', 'Header-Hintergrund'
EXEC NOVVIA.spConfigSet 'Theme', 'FilterBackgroundColor', '#F5F5F5', 'Filter-Hintergrund'
EXEC NOVVIA.spConfigSet 'Theme', 'TextColor', '#212529', 'Textfarbe'
EXEC NOVVIA.spConfigSet 'Theme', 'HeaderTextColor', '#1A1A1A', 'Header-Textfarbe'
EXEC NOVVIA.spConfigSet 'Theme', 'MutedTextColor', '#6C757D', 'Gedaempfte Textfarbe'
EXEC NOVVIA.spConfigSet 'Theme', 'BorderColor', '#DDDDDD', 'Rahmenfarbe'
EXEC NOVVIA.spConfigSet 'Theme', 'SuccessColor', '#28A745', 'Erfolgsfarbe (gruen)'
EXEC NOVVIA.spConfigSet 'Theme', 'WarningColor', '#FFC107', 'Warnfarbe (gelb)'
EXEC NOVVIA.spConfigSet 'Theme', 'DangerColor', '#DC3545', 'Fehlerfarbe (rot)'
EXEC NOVVIA.spConfigSet 'Theme', 'InfoColor', '#17A2B8', 'Info-Farbe (blau)'
EXEC NOVVIA.spConfigSet 'Theme', 'AlternateRowColor', '#FAFAFA', 'Alternierende Zeilenfarbe'
EXEC NOVVIA.spConfigSet 'Theme', 'SelectedRowColor', '#E3F2FD', 'Ausgewaehlte Zeile'
GO

PRINT 'NOVVIA.Config Setup abgeschlossen.'

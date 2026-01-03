-- =====================================================
-- NOVVIA Sprache/Lokalisierung
-- =====================================================
-- Texte fuer UI-Elemente in der Datenbank
-- Ermoeglicht Anpassung ohne Code-Aenderung
-- =====================================================

-- Datenbank wird beim Aufruf angegeben: sqlcmd -d "Mandant_2"
-- USE [Mandant_2]
GO

-- Schema sicherstellen
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'NOVVIA')
    EXEC('CREATE SCHEMA NOVVIA');
GO

-- =====================================================
-- Tabelle: NOVVIA.Sprache
-- =====================================================
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'NOVVIA' AND TABLE_NAME = 'Sprache')
BEGIN
    CREATE TABLE NOVVIA.Sprache (
        kSprache INT IDENTITY(1,1) PRIMARY KEY,
        cSchluessel NVARCHAR(200) NOT NULL,      -- z.B. "Buttons.Speichern"
        cSprache NVARCHAR(10) NOT NULL DEFAULT 'de',  -- "de", "en", etc.
        cWert NVARCHAR(500) NOT NULL,            -- Der angezeigte Text
        cBeschreibung NVARCHAR(500) NULL,        -- Wo wird der Text verwendet
        dErstellt DATETIME NOT NULL DEFAULT GETDATE(),
        dGeaendert DATETIME NULL,

        CONSTRAINT UQ_Sprache_SchluesselSprache UNIQUE (cSchluessel, cSprache)
    );

    CREATE INDEX IX_Sprache_Sprache ON NOVVIA.Sprache(cSprache);
    CREATE INDEX IX_Sprache_Schluessel ON NOVVIA.Sprache(cSchluessel);

    PRINT 'Tabelle NOVVIA.Sprache erstellt.';
END
ELSE
    PRINT 'Tabelle NOVVIA.Sprache existiert bereits.';
GO

-- =====================================================
-- SP: NOVVIA.spSpracheImportieren
-- Importiert Texte (fuer Bulk-Import)
-- =====================================================
IF EXISTS (SELECT * FROM sys.procedures WHERE schema_id = SCHEMA_ID('NOVVIA') AND name = 'spSpracheImportieren')
    DROP PROCEDURE NOVVIA.spSpracheImportieren;
GO

CREATE PROCEDURE NOVVIA.spSpracheImportieren
    @cSchluessel NVARCHAR(200),
    @cSprache NVARCHAR(10) = 'de',
    @cWert NVARCHAR(500),
    @cBeschreibung NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    MERGE INTO NOVVIA.Sprache AS target
    USING (SELECT @cSchluessel AS cSchluessel, @cSprache AS cSprache) AS source
    ON target.cSchluessel = source.cSchluessel AND target.cSprache = source.cSprache
    WHEN MATCHED THEN
        UPDATE SET cWert = @cWert, cBeschreibung = ISNULL(@cBeschreibung, target.cBeschreibung), dGeaendert = GETDATE()
    WHEN NOT MATCHED THEN
        INSERT (cSchluessel, cSprache, cWert, cBeschreibung)
        VALUES (@cSchluessel, @cSprache, @cWert, @cBeschreibung);
END
GO

PRINT 'SP NOVVIA.spSpracheImportieren erstellt.';
GO

-- =====================================================
-- Standard-Texte (Deutsch)
-- =====================================================
PRINT 'Importiere Standard-Texte...';

-- Buttons
EXEC NOVVIA.spSpracheImportieren 'Buttons.Speichern', 'de', 'Speichern', 'Button';
EXEC NOVVIA.spSpracheImportieren 'Buttons.Abbrechen', 'de', 'Abbrechen', 'Button';
EXEC NOVVIA.spSpracheImportieren 'Buttons.Bearbeiten', 'de', 'Bearbeiten', 'Button';
EXEC NOVVIA.spSpracheImportieren 'Buttons.Loeschen', 'de', 'Loeschen', 'Button';
EXEC NOVVIA.spSpracheImportieren 'Buttons.Neu', 'de', 'Neu', 'Button';
EXEC NOVVIA.spSpracheImportieren 'Buttons.Suchen', 'de', 'Suchen', 'Button';
EXEC NOVVIA.spSpracheImportieren 'Buttons.Aktualisieren', 'de', 'Aktualisieren', 'Button';
EXEC NOVVIA.spSpracheImportieren 'Buttons.Schliessen', 'de', 'Schliessen', 'Button';
EXEC NOVVIA.spSpracheImportieren 'Buttons.Ja', 'de', 'Ja', 'Button';
EXEC NOVVIA.spSpracheImportieren 'Buttons.Nein', 'de', 'Nein', 'Button';
EXEC NOVVIA.spSpracheImportieren 'Buttons.OK', 'de', 'OK', 'Button';
EXEC NOVVIA.spSpracheImportieren 'Buttons.Exportieren', 'de', 'Exportieren', 'Button';
EXEC NOVVIA.spSpracheImportieren 'Buttons.Importieren', 'de', 'Importieren', 'Button';
EXEC NOVVIA.spSpracheImportieren 'Buttons.Drucken', 'de', 'Drucken', 'Button';

-- Navigation
EXEC NOVVIA.spSpracheImportieren 'Navigation.Dashboard', 'de', 'Dashboard', 'Navigation';
EXEC NOVVIA.spSpracheImportieren 'Navigation.Auftraege', 'de', 'Auftraege', 'Navigation';
EXEC NOVVIA.spSpracheImportieren 'Navigation.Kunden', 'de', 'Kunden', 'Navigation';
EXEC NOVVIA.spSpracheImportieren 'Navigation.Artikel', 'de', 'Artikel', 'Navigation';
EXEC NOVVIA.spSpracheImportieren 'Navigation.Lieferanten', 'de', 'Lieferanten', 'Navigation';
EXEC NOVVIA.spSpracheImportieren 'Navigation.Einkauf', 'de', 'Einkauf', 'Navigation';
EXEC NOVVIA.spSpracheImportieren 'Navigation.Rechnungen', 'de', 'Rechnungen', 'Navigation';
EXEC NOVVIA.spSpracheImportieren 'Navigation.Lager', 'de', 'Lager', 'Navigation';
EXEC NOVVIA.spSpracheImportieren 'Navigation.Einstellungen', 'de', 'Einstellungen', 'Navigation';

-- Pharma
EXEC NOVVIA.spSpracheImportieren 'Pharma.Titel', 'de', 'Lieferanten-Qualifizierung (GDP)', 'Pharma-Tab';
EXEC NOVVIA.spSpracheImportieren 'Pharma.Produktkategorien', 'de', 'Produktkategorien', 'Pharma-Tab';
EXEC NOVVIA.spSpracheImportieren 'Pharma.Ambient', 'de', 'Ambient (15-25 Grad)', 'Pharma-Tab';
EXEC NOVVIA.spSpracheImportieren 'Pharma.Cool', 'de', 'Cool (2-8 Grad)', 'Pharma-Tab';
EXEC NOVVIA.spSpracheImportieren 'Pharma.Medcan', 'de', 'Medizin. Cannabis', 'Pharma-Tab';
EXEC NOVVIA.spSpracheImportieren 'Pharma.Tierarznei', 'de', 'Tierarzneimittel', 'Pharma-Tab';
EXEC NOVVIA.spSpracheImportieren 'Pharma.NurRPBerechtigt', 'de', 'Nur RP-Berechtigte koennen aendern', 'Pharma-Tab';

-- Meldungen
EXEC NOVVIA.spSpracheImportieren 'Meldungen.Erfolg.Gespeichert', 'de', 'Erfolgreich gespeichert!', 'Meldung';
EXEC NOVVIA.spSpracheImportieren 'Meldungen.Erfolg.Geloescht', 'de', 'Erfolgreich geloescht!', 'Meldung';
EXEC NOVVIA.spSpracheImportieren 'Meldungen.Fehler.Allgemein', 'de', 'Ein Fehler ist aufgetreten', 'Meldung';
EXEC NOVVIA.spSpracheImportieren 'Meldungen.Fehler.Laden', 'de', 'Fehler beim Laden', 'Meldung';
EXEC NOVVIA.spSpracheImportieren 'Meldungen.Fehler.Speichern', 'de', 'Fehler beim Speichern', 'Meldung';
EXEC NOVVIA.spSpracheImportieren 'Meldungen.Fehler.Berechtigung', 'de', 'Keine Berechtigung', 'Meldung';
EXEC NOVVIA.spSpracheImportieren 'Meldungen.Warnung.WirklichLoeschen', 'de', 'Wirklich loeschen?', 'Meldung';
EXEC NOVVIA.spSpracheImportieren 'Meldungen.Info.BitteAuswaehlen', 'de', 'Bitte zuerst einen Eintrag auswaehlen', 'Meldung';
EXEC NOVVIA.spSpracheImportieren 'Meldungen.Info.KeineErgebnisse', 'de', 'Keine Ergebnisse gefunden', 'Meldung';
EXEC NOVVIA.spSpracheImportieren 'Meldungen.Info.LadeVorgang', 'de', 'Lade...', 'Meldung';

-- Status
EXEC NOVVIA.spSpracheImportieren 'Status.Offen', 'de', 'Offen', 'Status';
EXEC NOVVIA.spSpracheImportieren 'Status.InBearbeitung', 'de', 'In Bearbeitung', 'Status';
EXEC NOVVIA.spSpracheImportieren 'Status.Abgeschlossen', 'de', 'Abgeschlossen', 'Status';
EXEC NOVVIA.spSpracheImportieren 'Status.Storniert', 'de', 'Storniert', 'Status';
EXEC NOVVIA.spSpracheImportieren 'Status.Aktiv', 'de', 'Aktiv', 'Status';
EXEC NOVVIA.spSpracheImportieren 'Status.Inaktiv', 'de', 'Inaktiv', 'Status';

-- Labels
EXEC NOVVIA.spSpracheImportieren 'Labels.Datum', 'de', 'Datum', 'Label';
EXEC NOVVIA.spSpracheImportieren 'Labels.Von', 'de', 'Von', 'Label';
EXEC NOVVIA.spSpracheImportieren 'Labels.Bis', 'de', 'Bis', 'Label';
EXEC NOVVIA.spSpracheImportieren 'Labels.Status', 'de', 'Status', 'Label';
EXEC NOVVIA.spSpracheImportieren 'Labels.Bemerkung', 'de', 'Bemerkung', 'Label';
EXEC NOVVIA.spSpracheImportieren 'Labels.Anzahl', 'de', 'Anzahl', 'Label';
EXEC NOVVIA.spSpracheImportieren 'Labels.Menge', 'de', 'Menge', 'Label';
EXEC NOVVIA.spSpracheImportieren 'Labels.Preis', 'de', 'Preis', 'Label';
EXEC NOVVIA.spSpracheImportieren 'Labels.Summe', 'de', 'Summe', 'Label';
EXEC NOVVIA.spSpracheImportieren 'Labels.Netto', 'de', 'Netto', 'Label';
EXEC NOVVIA.spSpracheImportieren 'Labels.Brutto', 'de', 'Brutto', 'Label';
EXEC NOVVIA.spSpracheImportieren 'Labels.MwSt', 'de', 'MwSt', 'Label';

PRINT '';
PRINT '=====================================================';
PRINT 'NOVVIA Sprache Setup abgeschlossen!';
PRINT '=====================================================';
PRINT '';
PRINT 'Tabelle: NOVVIA.Sprache';
PRINT 'SP: NOVVIA.spSpracheImportieren';
PRINT '';
PRINT 'Verwendung in C#:';
PRINT '  await Lang.InitAsync(connectionString, "de");';
PRINT '  var text = Lang.Get("Buttons.Speichern");';
PRINT '  await Lang.SetAsync("Buttons.Speichern", "Save");';
PRINT '';
PRINT 'Texte bearbeiten:';
PRINT '  UPDATE NOVVIA.Sprache SET cWert = ''Save'' WHERE cSchluessel = ''Buttons.Speichern'' AND cSprache = ''en''';
PRINT '=====================================================';
GO

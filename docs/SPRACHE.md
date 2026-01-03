# Sprachsystem / Lokalisierung - NOVVIA ERP

## Uebersicht

Das Sprachsystem ermoeglicht die Verwaltung aller UI-Texte in der Datenbank.
Texte koennen ohne Code-Aenderung angepasst und uebersetzt werden.

## Architektur

```
┌─────────────────────────────────────────────────────────┐
│                    WPF Application                       │
│                                                          │
│   Lang.Get("Buttons.Speichern") → "Speichern"           │
│                                                          │
└────────────────────────┬────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────┐
│              LanguageService (Lang)                      │
│                                                          │
│   - Primaer: DB (NOVVIA.Sprache)                        │
│   - Fallback: JSON (Resources/Lang/de.json)             │
│   - Cache: Dictionary<string, string>                   │
│                                                          │
└────────────────────────┬────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────┐
│              NOVVIA.Sprache (DB)                         │
│                                                          │
│   kSprache | cSchluessel        | cSprache | cWert      │
│   ---------|-------------------|----------|-------------|
│   1        | Buttons.Speichern | de       | Speichern   │
│   2        | Buttons.Speichern | en       | Save        │
│                                                          │
└─────────────────────────────────────────────────────────┘
```

## Verwendung in C#

### Initialisierung (App.xaml.cs)

```csharp
// Beim Start der Anwendung
await Lang.InitAsync(connectionString, "de");
```

### Text abrufen

```csharp
// Einfacher Text
var text = Lang.Get("Buttons.Speichern");  // → "Speichern"

// Mit Fallback
var text = Lang.Get("Buttons.Unbekannt", "Standard");  // → "Standard"

// Kurzform
var text = Lang.T("Buttons.Speichern");  // → "Speichern"

// Mit Platzhaltern
var text = Lang.Format("Meldungen.Willkommen", userName);  // → "Hallo Max!"
```

### Text speichern

```csharp
// Text in DB speichern
await Lang.SetAsync("Buttons.MeinButton", "Mein Button");

// Cache neu laden
await Lang.LoadFromDbAsync();
```

## Schluessel-Konvention

| Prefix | Verwendung | Beispiel |
|--------|------------|----------|
| `Buttons.*` | Button-Texte | Buttons.Speichern |
| `Labels.*` | Feldbezeichnungen | Labels.Datum |
| `Navigation.*` | Menue-Eintraege | Navigation.Dashboard |
| `Meldungen.Erfolg.*` | Erfolgsmeldungen | Meldungen.Erfolg.Gespeichert |
| `Meldungen.Fehler.*` | Fehlermeldungen | Meldungen.Fehler.Laden |
| `Meldungen.Warnung.*` | Warnungen | Meldungen.Warnung.WirklichLoeschen |
| `Meldungen.Info.*` | Infomeldungen | Meldungen.Info.BitteWarten |
| `Status.*` | Status-Texte | Status.Offen |
| `Pharma.*` | Pharma-Bereich | Pharma.GDP |
| `Kunden.*` | Kunden-Modul | Kunden.Kundennummer |
| `Artikel.*` | Artikel-Modul | Artikel.EAN |
| `Auftraege.*` | Auftrags-Modul | Auftraege.Positionen |
| `Lieferanten.*` | Lieferanten-Modul | Lieferanten.MSV3 |
| `Einstellungen.*` | Einstellungen | Einstellungen.Design |

## Datenbank

### Tabelle: NOVVIA.Sprache

```sql
CREATE TABLE NOVVIA.Sprache (
    kSprache INT IDENTITY(1,1) PRIMARY KEY,
    cSchluessel NVARCHAR(200) NOT NULL,      -- z.B. "Buttons.Speichern"
    cSprache NVARCHAR(10) NOT NULL DEFAULT 'de',
    cWert NVARCHAR(500) NOT NULL,            -- Der angezeigte Text
    cBeschreibung NVARCHAR(500) NULL,        -- Verwendungshinweis
    dErstellt DATETIME NOT NULL DEFAULT GETDATE(),
    dGeaendert DATETIME NULL,

    CONSTRAINT UQ_Sprache_SchluesselSprache UNIQUE (cSchluessel, cSprache)
);
```

### SP: NOVVIA.spSpracheImportieren

```sql
EXEC NOVVIA.spSpracheImportieren
    @cSchluessel = 'Buttons.MeinButton',
    @cSprache = 'de',
    @cWert = 'Mein Button',
    @cBeschreibung = 'Eigener Button';
```

## UI: Einstellungen → Sprache

In den Einstellungen gibt es einen Tab "Sprache" mit:

- **DataGrid** - Alle Texte mit Schluessel, Wert, Beschreibung
- **Filter** - Nach Kategorie (Buttons, Labels, etc.)
- **Suche** - Volltextsuche in Schluessel/Wert
- **Bearbeiten** - Inline-Bearbeitung oder Formular
- **Neu/Loeschen** - Texte hinzufuegen/entfernen

## JSON-Fallback

Wenn die DB nicht erreichbar ist oder die Tabelle nicht existiert,
werden Texte aus `Resources/Lang/de.json` geladen:

```json
{
  "Buttons": {
    "Speichern": "Speichern",
    "Abbrechen": "Abbrechen"
  },
  "Meldungen": {
    "Erfolg": {
      "Gespeichert": "Erfolgreich gespeichert!"
    }
  }
}
```

## Setup-Script

```bash
sqlcmd -S "SERVER" -d "Mandant_2" -E -i "Scripts/Setup-NOVVIA-Sprache.sql"
```

Das Script:
1. Erstellt NOVVIA Schema (falls nicht vorhanden)
2. Erstellt Tabelle NOVVIA.Sprache
3. Erstellt SP spSpracheImportieren
4. Importiert 58 deutsche Standard-Texte

## Mehrsprachigkeit

Fuer andere Sprachen:

1. Texte mit `cSprache = 'en'` einfuegen
2. `Lang.InitAsync(connectionString, "en")` aufrufen
3. Sprachauswahl in Einstellungen anbieten

```sql
-- Englische Texte hinzufuegen
EXEC NOVVIA.spSpracheImportieren 'Buttons.Speichern', 'en', 'Save', 'Button';
EXEC NOVVIA.spSpracheImportieren 'Buttons.Abbrechen', 'en', 'Cancel', 'Button';
```

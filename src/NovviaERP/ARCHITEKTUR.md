# NovviaERP Modulare Architektur

## Grundprinzipien

1. **JTL Datumslogik** - Immer JTL-konforme Zeitraeume verwenden
2. **Sofortige Anzeige** - Daten sofort laden, kein Aktualisieren-Button
3. **Filter oben, DataGrid unten** - Einheitliches Layout
4. **Neuanlage als Popup** - Dialoge fuer Neuanlage/Bearbeitung

---

## Ordnerstruktur

```
NovviaERP.Core/
├── Services/
│   ├── Base/
│   │   ├── BaseDatabaseService.cs      # Basis fuer alle DB-Services
│   │   └── BaseEntityModels.cs         # Gemeinsame DTOs
│   ├── CoreService.cs
│   └── ...

NovviaERP.WPF/
├── Controls/
│   ├── Base/
│   │   ├── FilterBarControl.xaml       # Wiederverwendbare Filterleiste
│   │   └── EntityListControl.xaml      # Wiederverwendbare Listenseite
│   ├── EntitySearchControl.xaml        # Entity-Suche inline
│   └── ...
├── Dialogs/
│   └── EntitySucheDialog.xaml          # Entity-Suche als Popup
├── Views/
│   └── ...                             # Seiten
└── ViewModels/
    └── ...                             # MVVM ViewModels
```

---

## Basis-Komponenten

### 1. BaseDatabaseService (NovviaERP.Core/Services/Base/)

Basis-Klasse fuer alle Datenbankservices:

```csharp
public abstract class BaseDatabaseService : IDisposable
{
    // Verbindungsmanagement
    protected async Task<SqlConnection> GetConnectionAsync()

    // Query-Methoden
    protected async Task<List<T>> QueryListAsync<T>(string sql, object? param = null)
    protected async Task<T?> QuerySingleAsync<T>(string sql, object? param = null)
    protected async Task<int> ExecuteAsync(string sql, object? param = null)
    protected async Task<int> InsertAndGetIdAsync(string sql, object? param = null)

    // JTL Datumslogik
    protected static DateTime JtlHeute
    protected static DateTime JtlWocheStart
    protected static DateTime JtlMonatStart
    protected static (DateTime von, DateTime bis) JtlZeitraumZuDatum(string zeitraum)
    public static readonly string[] JtlZeitraumOptionen
}
```

**Verwendung:**
```csharp
public class MeinService : BaseDatabaseService
{
    public MeinService(string connectionString) : base(connectionString) { }

    public async Task<List<MeinDto>> GetAlleAsync()
    {
        return await QueryListAsync<MeinDto>("SELECT * FROM MeineTabelle");
    }
}
```

### 2. FilterBarControl (NovviaERP.WPF/Controls/Base/)

Wiederverwendbare Filterleiste mit:
- Suchfeld + Suchen-Button
- Zeitraum-Dropdown (JTL-Optionen)
- Dynamische Extra-Filter
- Neu-Button
- Anzahl-Anzeige

**WICHTIG: Keine automatische Aktualisierung bei Filter-Auswahl!**
- Aktualisierung NUR bei Suchen-Button oder Enter im Suchfeld
- Benutzer kann Filter in Ruhe auswaehlen, dann suchen

**XAML:**
```xml
<base:FilterBarControl x:Name="filterBar"
    ZeigeZeitraum="True"
    NeuButtonText="+ Neu"
    AnzahlText="Kunden"
    SucheGestartet="FilterBar_SucheGestartet"
    FilterGeaendert="FilterBar_FilterGeaendert"
    NeuGeklickt="FilterBar_NeuGeklickt"/>
```

**Extra-Filter hinzufuegen:**
```csharp
var cmbGruppe = filterBar.AddFilter("Gruppe:", gruppen, "CName");
var chkAktiv = filterBar.AddCheckFilter("Nur aktive", true);
```

### 3. EntityListControl (NovviaERP.WPF/Controls/Base/)

Komplette Listenseite mit:
- FilterBarControl
- DataGrid mit Standard-Styling
- Status-Leiste mit Aktions-Buttons
- Loading-Overlay

**XAML:**
```xml
<base:EntityListControl x:Name="liste"
    Titel="Kunden"
    AnzahlText="Kunden"
    ZeigeZeitraum="True"
    ZeigeLoeschen="False"
    NeuButtonText="+ Neuer Kunde"
    DatenLaden="Liste_DatenLaden"
    NeuGeklickt="Liste_NeuGeklickt"
    DoppelklickAufItem="Liste_DoppelklickAufItem"/>
```

**Spalten definieren:**
```csharp
liste.AddTextColumn("Nr", "CKundenNr", 100);
liste.AddTextColumn("Firma", "CFirma", 0); // 0 = Stretch
liste.AddCurrencyColumn("Umsatz", "Umsatz", 120);
liste.AddDateColumn("Erstellt", "DErstellt", 100);
```

**Daten laden:**
```csharp
private async Task Liste_DatenLaden()
{
    var filter = new ListFilterParameter
    {
        Suchbegriff = liste.Suchbegriff,
        Zeitraum = liste.Zeitraum
    };
    var daten = await _service.GetKundenAsync(filter);
    liste.SetItemsSource(daten, daten.Count);
}
```

### 4. EntitySucheDialog (NovviaERP.WPF/Dialogs/)

Popup-Dialog fuer Entity-Suche mit Filtern:

**Verwendung:**
```csharp
// Kunden suchen
var result = EntitySucheDialog.Suchen(EntitySucheDialog.EntityTyp.Kunde, this);
if (result.HasValue)
{
    int kundeId = result.Value.Id!.Value;
    string kundeNr = result.Value.Nr;
    string kundeName = result.Value.Name;
}

// Artikel suchen
var result = EntitySucheDialog.Suchen(EntitySucheDialog.EntityTyp.Artikel, this);

// Lieferant suchen
var result = EntitySucheDialog.Suchen(EntitySucheDialog.EntityTyp.Lieferant, this);
```

---

## JTL Zeitraum-Optionen

Standard-Optionen fuer Zeitraum-Filter:
- Alle
- Heute
- Gestern
- Diese Woche
- Letzte Woche
- Dieser Monat
- Letzter Monat
- Letzte 7 Tage
- Letzte 30 Tage
- Letzte 90 Tage
- Dieses Jahr
- Letztes Jahr

**Verwendung in SQL:**
```csharp
var (von, bis) = BaseDatabaseService.JtlZeitraumZuDatum(zeitraum);
var sql = "SELECT * FROM tBestellung WHERE dErstellt BETWEEN @Von AND @Bis";
await QueryListAsync<T>(sql, new { Von = von, Bis = bis });
```

---

## Neue Seite erstellen (Beispiel)

### 1. Service erstellen
```csharp
public class MeinEntityService : BaseDatabaseService
{
    public async Task<List<MeinEntityUebersicht>> GetAlleAsync(ListFilterParameter filter)
    {
        var (von, bis) = JtlZeitraumZuDatum(filter.Zeitraum);
        return await QueryListAsync<MeinEntityUebersicht>(@"
            SELECT * FROM MeineTabelle
            WHERE (@Suche = '' OR Name LIKE @Suche)
              AND dErstellt BETWEEN @Von AND @Bis
            ORDER BY Name",
            new {
                Suche = string.IsNullOrEmpty(filter.Suchbegriff) ? "" : $"%{filter.Suchbegriff}%",
                Von = von, Bis = bis
            });
    }
}
```

### 2. Listenseite erstellen (XAML)
```xml
<Page x:Class="NovviaERP.WPF.Views.MeinePage"
      xmlns:base="clr-namespace:NovviaERP.WPF.Controls.Base">
    <base:EntityListControl x:Name="liste"
        AnzahlText="Eintraege"
        DatenLaden="Liste_DatenLaden"
        NeuGeklickt="Liste_NeuGeklickt"
        DoppelklickAufItem="Liste_Bearbeiten"/>
</Page>
```

### 3. Code-Behind
```csharp
public partial class MeinePage : Page
{
    private readonly MeinEntityService _service;

    public MeinePage()
    {
        InitializeComponent();
        _service = new MeinEntityService(App.ConnectionString);

        // Spalten definieren
        liste.AddTextColumn("Name", "Name", 0);
        liste.AddDateColumn("Erstellt", "DErstellt", 100);
    }

    private async Task Liste_DatenLaden()
    {
        var filter = new ListFilterParameter
        {
            Suchbegriff = liste.Suchbegriff,
            Zeitraum = liste.Zeitraum
        };
        var daten = await _service.GetAlleAsync(filter);
        liste.SetItemsSource(daten, daten.Count);
    }

    private void Liste_NeuGeklickt(object? sender, EventArgs e)
    {
        var dialog = new MeineEntityDialog();
        if (dialog.ShowDialog() == true)
        {
            // Speichern und Liste aktualisieren
        }
    }

    private void Liste_Bearbeiten(object? sender, object? item)
    {
        if (item is MeinEntityUebersicht entity)
        {
            var dialog = new MeineEntityDialog(entity.Id);
            if (dialog.ShowDialog() == true)
            {
                // Aktualisieren
            }
        }
    }
}
```

---

## Namenskonventionen

| Typ | Konvention | Beispiel |
|-----|------------|----------|
| Service | `{Entity}Service` | `KundenService` |
| Uebersicht-DTO | `{Entity}Uebersicht` | `KundeUebersicht` |
| Detail-DTO | `{Entity}Detail` | `KundeDetail` |
| Listenseite | `{Entity}Page` | `KundenPage` |
| Detail-Seite | `{Entity}DetailPage` | `KundeDetailPage` |
| Dialog | `{Entity}Dialog` | `KundeDialog` |
| Such-Dialog | `EntitySucheDialog` | (generisch) |

---

## NOVVIA Konfiguration (pro Mandant)

### ConfigService (Core/Services/)

Speichert alle Einstellungen in NOVVIA.Config Tabelle:

```csharp
using var config = new ConfigService(connectionString);

// Einzelwert
var wert = await config.GetAsync("Theme", "PrimaryColor");
await config.SetAsync("Theme", "PrimaryColor", "#0078D4");

// Alle Werte einer Kategorie
var alle = await config.GetAllAsync("Theme");
await config.SetAllAsync("Theme", werte);

// Typisiert
int zahl = await config.GetIntAsync("Allgemein", "MaxItems", 100);
bool aktiv = await config.GetBoolAsync("Allgemein", "DebugMode", false);
```

### ThemeService (WPF/Services/)

Farben und Design pro Mandant:

```csharp
// Beim App-Start laden
await ThemeService.LoadSettingsAsync(connectionString);

// Speichern
await ThemeService.SaveSettingsAsync(connectionString);

// Zuruecksetzen
await ThemeService.ResetToDefaultAsync(connectionString);

// Theme anwenden (nach Aenderungen)
ThemeService.ApplyTheme();
```

### ThemeSettingsControl (WPF/Controls/Base/)

UserControl fuer Einstellungen-Seite:
```xml
<base:ThemeSettingsControl/>
```

### Verfuegbare Theme-Ressourcen (in XAML verwenden):

| Ressource | Beschreibung |
|-----------|--------------|
| `{DynamicResource PrimaryBrush}` | Primaerfarbe (Buttons) |
| `{DynamicResource SecondaryBrush}` | Sekundaerfarbe |
| `{DynamicResource BackgroundBrush}` | Hintergrund |
| `{DynamicResource HeaderBackgroundBrush}` | Header-Hintergrund |
| `{DynamicResource FilterBackgroundBrush}` | Filter-Hintergrund |
| `{DynamicResource TextBrush}` | Textfarbe |
| `{DynamicResource BorderBrush}` | Rahmenfarbe |
| `{DynamicResource SuccessBrush}` | Erfolg (gruen) |
| `{DynamicResource WarningBrush}` | Warnung (gelb) |
| `{DynamicResource DangerBrush}` | Fehler (rot) |
| `{DynamicResource InfoBrush}` | Info (blau) |
| `{DynamicResource AlternateRowBrush}` | Alternierende Zeile |
| `{DynamicResource SelectedRowBrush}` | Ausgewaehlte Zeile |

---

## SQL Scripts

### NOVVIA.Config Tabelle erstellen:
`Scripts/CreateNovviaConfigTable.sql`

---

## Dateien

### Core/Services/Base/
- `BaseDatabaseService.cs` - DB-Basis mit JTL-Logik
- `BaseEntityModels.cs` - Gemeinsame DTOs

### Core/Services/
- `ConfigService.cs` - NOVVIA Konfiguration

### WPF/Services/
- `ThemeService.cs` - Theme/Farben Management

### WPF/Controls/Base/
- `FilterBarControl.xaml(.cs)` - Filter-Leiste
- `EntityListControl.xaml(.cs)` - Listen-Template
- `ThemeSettingsControl.xaml(.cs)` - Design-Einstellungen

### WPF/Dialogs/
- `EntitySucheDialog.xaml(.cs)` - Entity-Suche Popup

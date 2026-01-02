# DataGrid Spalten-Konfiguration - Projektregel

## Regel
**Jedes DataGrid in der Anwendung MUSS die Spalten-Konfiguration aktiviert haben.**

## Implementierung

### 1. Im Code-Behind der View (InitAsync oder Loaded-Event):
```csharp
using NovviaERP.WPF.Controls;

// Für jedes DataGrid in der View:
DataGridColumnConfig.EnableColumnChooser(dgMeinGrid, "ViewName.GridName");
```

### 2. Namenskonvention für viewName:
- Format: `{ViewName}.{GridName}` oder nur `{ViewName}` wenn nur ein Grid
- Beispiele:
  - `KundenView` - Hauptliste
  - `KundenView.Auftraege` - Aufträge-Tab
  - `BestellungenView` - Bestellungsliste
  - `ArtikelView` - Artikelliste

### 3. Features die automatisch aktiviert werden:
- ✅ Rechtsklick auf Spalten-Header → Kontextmenü
- ✅ Spalten ein-/ausblenden per Checkbox
- ✅ "Alle anzeigen" Button
- ✅ "Zurücksetzen" Button
- ✅ Spaltenbreiten werden gespeichert
- ✅ Spaltenreihenfolge wird gespeichert
- ✅ Einstellungen pro Benutzer in DB (NOVVIA.BenutzerEinstellung)

## Beispiel
```csharp
private async Task InitAsync()
{
    // Spalten-Konfiguration für alle DataGrids
    DataGridColumnConfig.EnableColumnChooser(dgHauptliste, "MeineView");
    DataGridColumnConfig.EnableColumnChooser(dgDetails, "MeineView.Details");
    DataGridColumnConfig.EnableColumnChooser(dgHistorie, "MeineView.Historie");

    // Rest der Initialisierung...
}
```

## Checkliste für neue Views
- [ ] `using NovviaERP.WPF.Controls;` hinzufügen
- [ ] Für jedes `DataGrid x:Name="..."` den EnableColumnChooser aufrufen
- [ ] Eindeutigen viewName vergeben

## Technische Details
- Speicherort: `DataGridColumnConfig.cs` in `NovviaERP.WPF.Controls`
- DB-Tabelle: `NOVVIA.BenutzerEinstellung`
- Schlüssel: `Spalten.AllViews` (JSON mit allen View-Einstellungen)

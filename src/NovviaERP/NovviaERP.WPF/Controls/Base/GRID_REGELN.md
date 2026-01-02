# Grid-Regeln für NovviaERP

## WICHTIG: Immer GridStyleHelper verwenden!

Jedes DataGrid in der Anwendung MUSS mit `GridStyleHelper.InitializeGrid()` initialisiert werden.

### Verwendung in Views

```csharp
// Im Loaded-Event oder InitAsync:
await GridStyleHelper.Instance.LoadSettingsAsync(_core, App.BenutzerId);
GridStyleHelper.InitializeGrid(dgMeinGrid, "MeinView");
```

Oder kurz:
```csharp
await GridStyleHelper.InitializeGridAsync(dgMeinGrid, "MeinView", _core, App.BenutzerId);
```

### Was diese Methode macht

1. **ApplyStyle()** - Setzt Zeilenhöhe, Schriftart, Farben aus Einstellungen
2. **EnableColumnChooser()** - Aktiviert Spalten-Konfiguration (Rechtsklick auf Header)

### VERBOTEN in XAML

Folgende Properties NICHT im XAML setzen (werden von GridStyleHelper überschrieben):
- `RowHeight`
- `FontSize`
- `FontFamily`
- `RowBackground`
- `AlternatingRowBackground`
- `GridLinesVisibility`
- `ColumnHeaderStyle`

### Erlaubt in XAML

- `AutoGenerateColumns`
- `IsReadOnly`
- `SelectionMode`
- `SelectionUnit`
- `CanUserResizeColumns`
- `CanUserReorderColumns`
- Spalten-Definitionen

### Status-Farben für Zellen

Für farbige Status-Badges verwende die Farben aus `GridStyleHelper.Instance.Settings`:
- `StatusErfolg` = #28a745 (Grün)
- `StatusWarnung` = #ffc107 (Gelb)
- `StatusFehler` = #dc3545 (Rot)
- `StatusInfo` = #17a2b8 (Türkis)
- `StatusNeutral` = #6c757d (Grau)

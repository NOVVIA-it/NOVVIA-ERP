# Claude Session Status - 2026-01-01

## Aktuelles Problem: Lager speichern funktioniert nicht

### Symptom
- User waehlt Lager in der Liste aus
- Beim Klick auf Formularfelder verliert die Auswahl die Markierung
- Speichern funktioniert nicht

### Bisherige Fixes (bereits committed)
1. ListBox-Styling hinzugefuegt damit Markierung sichtbar bleibt (InactiveSelectionHighlightBrushKey)
2. _selectedLagerId wird nicht mehr geloescht bei Fokuswechsel
3. LagerNeu_Click verwendet _isLoadingLager Flag
4. Debug-Ausgabe in LagerSpeichern_Click hinzugefuegt

### Dateien geaendert
- `NovviaERP.WPF/Views/EinstellungenView.xaml` - ListBox Styling
- `NovviaERP.WPF/Views/EinstellungenView.xaml.cs` - Selection Handler, Save Logic

### Naechste Schritte zum Debuggen
1. App starten und in Einstellungen -> Lager gehen
2. Lager auswaehlen und pruefen ob Markierung blau bleibt
3. Auf Textfeld klicken und pruefen ob Markierung bleibt
4. Speichern klicken und Statuszeile beobachten
   - Sollte zeigen: "Speichern... (ID: X)" oder "Speichern... (ID: NEU)"
5. Falls Fehler erscheint, Fehlermeldung dokumentieren

### Moegliche Ursachen
- UpdateWarenlagerAsync in CoreService koennte fehlschlagen
- Datenbank-Verbindungsproblem
- Spalten-Mismatch (unwahrscheinlich, Spalten existieren alle)

### Git Status
- Branch: main
- Letzte Commits:
  - 73ea250: Add debug output for Lager save troubleshooting
  - cd65601: Fix Lager selection persistence and save functionality
- Ahead of origin/main by 3 commits

### Andere aktuelle Features
- ZahlungsabgleichView JTL-konform gemacht
- Logo-Pfad in NOVVIA Einstellungen
- BenutzerEinstellung Tabelle fuer Spalteneinstellungen

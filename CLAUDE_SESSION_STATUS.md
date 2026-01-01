# Claude Session Status - 2026-01-01

## Zuletzt abgeschlossen

### Spalteneinstellungen in Datenbank - ERLEDIGT
- DataGridColumnConfig speichert jetzt in NOVVIA.BenutzerEinstellung
- Spaltenbreite, Sichtbarkeit und Reihenfolge werden persistiert
- Async Laden beim View-Start
- Debounced Speichern (500ms) bei Aenderungen

### NOVVIA FirmaEinstellung Pharma - BEHOBEN
- Fehler: "ungueltig dargestellt" beim Speichern
- Ursache: MERGE-Statement referenzierte nicht-existente Spalte `dErstellt`
- Loesung: Spaltenname auf `dGeaendert` geaendert

### Lager speichern - ERLEDIGT
- ListBox-Markierung bleibt sichtbar bei Fokusverlust
- UpdateWarenlagerAsync funktioniert korrekt
- Checkboxen "Bestand/Auslieferung sperren" entfernt (keine DB-Spalten)

### Zahlungsabgleich JTL-konform - ERLEDIGT
- JTL-Style Filter und Zeitraum-Dropdown
- Tabs: Kontobewegungen, Zugewiesene Zahlungen, SEPA Lastschriften

## Dateien geaendert (diese Session)

**NovviaERP.WPF/Controls/DataGridColumnConfig.cs**
- Speicherung in DB statt lokal
- Async Laden/Speichern
- DisplayIndex-Unterstuetzung

**NovviaERP.Core/Services/CoreService.cs**
- SaveNovviaEinstellungenAsync: MERGE korrigiert
- UpdateWarenlagerAsync: Gibt int zurueck
- LagerUebersicht: Aufgeraeumt

**NovviaERP.WPF/Views/EinstellungenView.xaml**
- Lager-ListBox mit persistenter Markierung
- Checkboxen fuer Bestand/Auslieferung entfernt

## Git Status
- Branch: main (up to date)
- Letzter Commit: c1bc223 "Fix NOVVIA FirmaEinstellung save"
- Alle Aenderungen committed und gepusht

## Technische Notizen
- NOVVIA.BenutzerEinstellung: Spalteneinstellungen pro Benutzer
- NOVVIA.FirmaEinstellung: Firma-weite Einstellungen
- tWarenLager: JTL-native Tabelle, keine Custom-Spalten

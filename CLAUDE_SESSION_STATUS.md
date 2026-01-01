# Claude Session Status - 2026-01-01

## Zuletzt abgeschlossen

### Lager speichern - GELOEST
- Problem: Lager-Auswahl verlor Markierung, Speichern funktionierte nicht
- Loesung:
  1. ListBox-Styling hinzugefuegt (Markierung bleibt sichtbar bei Fokusverlust)
  2. `_selectedLagerId` wird nicht mehr geloescht bei Fokuswechsel
  3. UpdateWarenlagerAsync gibt jetzt int (rows affected) zurueck
  4. Checkboxen "Bestand sperren" / "Auslieferung sperren" entfernt (Spalten existieren nicht in JTL)

### Zahlungsabgleich JTL-konform
- JTL-Style Filter (Konto, Transaktions-ID, Betrag, etc.)
- Zeitraum-Dropdown statt Datepicker (Heute, Gestern, 7 Tage, 30 Tage, Monat, Jahr, Alle)
- Tabs: Kontobewegungen, Zugewiesene Zahlungen, SEPA Lastschriften

### Weitere erledigte Features
- Logo-Pfad in NOVVIA.FirmaEinstellung
- NOVVIA.BenutzerEinstellung Tabelle fuer Spalteneinstellungen
- ZahlungsabgleichService erweitert

## Dateien geaendert (diese Session)

**NovviaERP.WPF/Views/EinstellungenView.xaml**
- ListBox mit persistenter Markierung (InactiveSelectionHighlightBrushKey)
- Checkboxen fuer Bestand/Auslieferung sperren entfernt

**NovviaERP.WPF/Views/EinstellungenView.xaml.cs**
- LstLager_SelectionChanged: _selectedLagerId bleibt erhalten
- LagerNeu_Click: Verwendet _isLoadingLager Flag
- LagerSpeichern_Click: Aufgeraeumt, funktioniert jetzt

**NovviaERP.Core/Services/CoreService.cs**
- UpdateWarenlagerAsync: Gibt int zurueck, keine COALESCE mehr
- LagerUebersicht: NBestandGesperrt/NAuslieferungGesperrt entfernt

**NovviaERP.WPF/Views/ZahlungsabgleichView.xaml**
- JTL-Style Filter und Tabs

## Git Status
- Branch: main
- Alle Aenderungen committed und gepusht
- Letzter Commit: bf43a0d "Fix Lager speichern - remove non-existent checkbox fields"

## Offene Punkte / Naechste Schritte
- Spalteneinstellungen in Views mit NOVVIA.BenutzerEinstellung implementieren
- Zahlungsabgleich Auto-Matching testen
- SEPA Lastschrift Export implementieren

## Technische Notizen
- tWarenLager hat KEINE Spalten fuer BestandGesperrt/AuslieferungGesperrt
- Verbindung zur DB funktioniert korrekt (Update wird ausgefuehrt)
- JTL Mandant: Mandant_2 auf 24.134.81.65,2107\NOVVIAS05

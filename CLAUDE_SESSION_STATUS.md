# Claude Session Status - 2026-01-01

## Zuletzt abgeschlossen

### Dashboard KPIs auf 30 Tage - ERLEDIGT
- Umsatz, Auftraege, Lieferscheine, Neue Kunden zeigen letzte 30 Tage
- SQL-Queries verwenden DATEADD(DAY, -30, GETDATE())
- Labels in XAML angepasst "(30 Tage)" / "(30T)"

### Benutzer & Rollen Verwaltung - ERLEDIGT
- Neuer Tab "Benutzer & Rollen" in Einstellungen
- Sub-Tab Benutzer: CRUD, Passwort-Reset, Rollen-Zuweisung
- Sub-Tab Rollen: CRUD, Admin-Flag, System-Rolle-Schutz
- Nutzt NOVVIA.Benutzer und NOVVIA.Rolle Tabellen
- Passwort-Hashing mit SHA256

### Spalteneinstellungen in Datenbank - ERLEDIGT
- DataGridColumnConfig speichert in NOVVIA.BenutzerEinstellung
- Spaltenbreite, Sichtbarkeit und Reihenfolge persistiert

### NOVVIA FirmaEinstellung Pharma - BEHOBEN
- MERGE-Statement Spaltenname korrigiert

### Lager speichern - ERLEDIGT
- ListBox-Markierung bleibt sichtbar
- UpdateWarenlagerAsync funktioniert korrekt

## Dateien geaendert (diese Session)

**NovviaERP.WPF/Views/EinstellungenView.xaml**
- Neuer Tab "Benutzer & Rollen" mit Sub-Tabs
- Benutzer-Liste, Details, Rollen-Zuweisung
- Rollen-Liste, Details, Benutzer-Anzeige

**NovviaERP.WPF/Views/EinstellungenView.xaml.cs**
- LadeBenutzerAsync/LadeRollenAsync
- Benutzer CRUD Event-Handler
- Rollen CRUD Event-Handler

**NovviaERP.Core/Services/CoreService.cs**
- NovviaBenutzer, NovviaRolle, RolleSelection DTOs
- GetNovviaBenutzerAsync, GetNovviaRollenAsync
- CreateNovviaBenutzerAsync, UpdateNovviaBenutzerAsync
- CreateNovviaRolleAsync, UpdateNovviaRolleAsync
- SetBenutzerRollenAsync, SetBenutzerPasswortAsync
- DeleteNovviaBenutzerAsync, DeleteNovviaRolleAsync

**NovviaERP.WPF/Views/DashboardPage.xaml**
- Labels auf "(30 Tage)" geaendert

**NovviaERP.WPF/Views/DashboardPage.xaml.cs**
- KPI-Queries auf 30-Tage-Zeitraum umgestellt

## Git Status
- Branch: main (up to date)
- Letzter Commit: 642972a "Benutzer & Rollen Verwaltung in Einstellungen"
- Alle Aenderungen committed und gepusht

## NOVVIA Schema Tabellen
- NOVVIA.Benutzer - Benutzer-Stammdaten
- NOVVIA.Rolle - Rollen-Definitionen
- NOVVIA.BenutzerRolle - M:N Zuordnung
- NOVVIA.Recht - Rechte-Katalog
- NOVVIA.RolleRecht - Rechte pro Rolle
- NOVVIA.BenutzerEinstellung - Benutzer-Einstellungen
- NOVVIA.FirmaEinstellung - Firma-weite Einstellungen

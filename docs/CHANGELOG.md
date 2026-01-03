# CHANGELOG - NOVVIA ERP

## [1.0.8] - 2026-01-03

### Hinzugefuegt
- **Sprachverwaltung in Einstellungen**
  - Neuer Tab "Sprache" in Einstellungen
  - Alle UI-Texte in Datenbank editierbar
  - Filter nach Kategorie (Buttons, Labels, Navigation, etc.)
  - Suchfunktion
  - Live-Aktualisierung nach Aenderungen

- **LanguageService (Lang)**
  - `NovviaERP.Core.Services.Lang` - Statischer Sprachdienst
  - Primaer: Texte aus DB (NOVVIA.Sprache)
  - Fallback: JSON-Datei (Resources/Lang/de.json)
  - Methoden: `Get()`, `SetAsync()`, `InitAsync()`, `LoadFromDbAsync()`

- **Datenbank-Tabelle NOVVIA.Sprache**
  - 58 deutsche Standard-Texte
  - Unterstuetzt mehrere Sprachen (cSprache: "de", "en", etc.)
  - SP: `spSpracheImportieren` fuer Bulk-Import

### Geaendert
- **de.json** - Alle Umlaute korrigiert (ae->ä, oe->ö, ue->ü, ss->ß)

---

## [1.0.7] - 2026-01-03

### Hinzugefuegt
- **PHARM-Validierung Lieferanten**
  - Tab "Pharma-Validierung" in LieferantenView
  - Felder: Ambient, Cool, Medcan, Tierarznei
  - GDP/GMP Zertifizierung
  - Qualifizierungsdaten
  - Nur RP-Berechtigte koennen bearbeiten

- **ABdata Integration**
  - ABDataPage fuer Pharma-Stammdaten
  - ABdataService.cs

### Geaendert
- LieferantenView.xaml - Pharma-Tab Felder statt DataGrid
- EinkaufService.cs - Pharma-Daten laden/speichern

---

## [1.0.6] - 2026-01-02

### Hinzugefuegt
- Rechnungen SPs (spRechnungLesen, spRechnungenAuflisten)
- Rechnungskorrekturen SPs
- Lieferschein SPs
- Lieferantenbestellungen SPs
- Eingangsrechnungen SPs

---

## [1.0.5] - 2026-01-01

### Hinzugefuegt
- Dashboard mit KPIs
- MSV3 Integration
- Textmeldungen-System

---

## Versionshistorie

| Version | Datum | Beschreibung |
|---------|-------|--------------|
| 1.0.8 | 2026-01-03 | Sprachverwaltung, Umlaute Fix |
| 1.0.7 | 2026-01-03 | PHARM-Validierung, ABdata |
| 1.0.6 | 2026-01-02 | Rechnungen, Lieferscheine, Einkauf SPs |
| 1.0.5 | 2026-01-01 | Dashboard, MSV3, Textmeldungen |

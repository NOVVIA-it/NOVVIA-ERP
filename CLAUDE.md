# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build Commands

```powershell
# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Build release
dotnet build -c Release

# Run WPF client (development)
dotnet run --project NovviaERP.WPF

# Run Worker service
dotnet run --project NovviaERP.Worker

# Run API
dotnet run --project NovviaERP.API

# Full build & deploy (requires Admin)
.\Scripts\Build-And-Deploy.ps1

# Build only (no installation)
.\Scripts\Build-And-Deploy.ps1 -NurBuild
```

## Architecture

### Solution Structure
- **NovviaERP.Core** - Business logic layer (shared library)
  - `Data/JtlDbContext.cs` - Central data access with Dapper/raw SQL
  - `Services/` - Business services (AngebotService, AusgabeService, etc.)
  - `Entities/` - Data models (JtlEntities for JTL-Wawi tables, NovviaEntities for custom)
- **NovviaERP.WPF** - Windows desktop client (code-behind pattern)
  - `Views/` - XAML pages and dialogs with code-behind
  - `App.xaml.cs` - DI container setup, session management
- **NovviaERP.Worker** - Background services (Windows Service)
  - Workers: ZahlungsabgleichWorker, WooCommerceSyncWorker, MahnlaufWorker, WorkflowQueueWorker
- **NovviaERP.API** - REST API with JWT authentication and Swagger
- **Scripts/** - PowerShell deployment and SQL setup scripts

### Data Access Pattern
- `JtlDbContext` uses Dapper for all database operations
- Raw SQL queries (no Entity Framework)
- Connection string set at runtime after login
- Multi-tenant: Connection string includes mandant database (Mandant_1, Mandant_2, etc.)

### JTL-Wawi Compatibility
- READ-ONLY access to JTL tables (tArtikel, tKunde, tAuftrag, etc.)
- Custom tables use `NOVVIA.` schema prefix
- SQL scripts in `Scripts/Setup-NovviaTables.sql`

### Dependency Injection
- WPF: Configured in `App.xaml.cs` via `IServiceCollection`
- Worker: Standard .NET Host builder pattern
- `JtlDbContext` registered as singleton with runtime connection string

## Key Technologies
- .NET 8.0, WPF, Dapper, MS SQL Server
- QuestPDF (document generation)
- Serilog (logging to file)
- BCrypt.Net (password hashing)
- CsvHelper (import/export)

## Database
- Server configured at runtime via profile selection
- Mandanten: Mandant_1 (NOVVIA), Mandant_2 (NOVVIA_PHARM), Mandant_3, Mandant_5 (Test)
- User profiles stored in `%APPDATA%\NovviaERP\profile.json`

### Direkter JTL-DB Zugriff (für Entwicklung/Recherche)
```powershell
# SQL Server Instanz: S03NOVVIA (nicht JTLWAWI!)
# Testdatenbank - kann frei abgefragt werden

# Tabellen auflisten
sqlcmd -S "localhost\S03NOVVIA" -d "Mandant_1" -E -Q "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME LIKE '%Suchbegriff%'"

# Spalten einer Tabelle
sqlcmd -S "localhost\S03NOVVIA" -d "Mandant_1" -E -Q "SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'tBestellung'" -s "|" -W

# Daten abfragen
sqlcmd -S "localhost\S03NOVVIA" -d "Mandant_1" -E -Q "SELECT TOP 5 * FROM tBestellung" -s "|" -W
```

## German Language
The codebase uses German naming conventions:
- Auftrag = Order, Kunde = Customer, Artikel = Article/Product
- Angebot = Quote, Rechnung = Invoice, Lager = Warehouse
- Bestellung = Purchase Order, Versand = Shipping

---

## Detaillierte Projekt-Dokumentation

### Core Services (NovviaERP.Core/Services/)

| Service | Beschreibung | Wichtige Methoden |
|---------|-------------|-------------------|
| `AngebotService` | Angebote CRUD, Angebot→Auftrag | `CreateAngebotAsync`, `AngebotToAuftragAsync` |
| `AusgabeService` | Dokument-Ausgabe (Druck, E-Mail, PDF, Archiv) | `DruckenAsync`, `EmailSendenAsync`, `PdfSpeichernAsync` |
| `WooCommerceService` | Shop-Sync (Produkte, Bestellungen) | `SyncProductsAsync`, `ImportOrdersAsync` |
| `ShippingService` | Labels DHL, DPD, GLS, UPS | `CreateShipmentAsync`, `GetTrackingUrl` |
| `MSV3Service` | Pharma-Großhandel (SOAP) | `CheckVerfuegbarkeitAsync`, `SendBestellungAsync` |
| `EinkaufService` | Lieferantenbestellungen | `GetEinkaufslisteAsync`, `CreateBestellungAsync` |
| `ABdataService` | Pharma-Stammdaten Import | `ImportArtikelstammAsync`, `AutoMappingAsync` |
| `PaymentService` | Zahlungsanbieter | `ProcessPaymentAsync` |
| `WorkflowService` | Workflow-Engine | `ExecuteWorkflowAsync` |
| `EigeneFelderService` | Custom Fields | `GetFelderAsync`, `SetWertAsync` |

### Entities (NovviaERP.Core/Entities/)

#### JTL-Wawi Tabellen (Read-Only)
```
tArtikel, tKunde, tBestellung, tBestellPos, tRechnung, tLieferschein
tAngebot, tFirma, tBenutzer, tShop, tWarenLager, tLieferant
```

#### NOVVIA Custom Tabellen (Schema: `NOVVIA.`)
```
NOVVIA.MSV3Lieferant      - MSV3-Konfiguration pro Lieferant
NOVVIA.MSV3Bestellung     - MSV3-Bestellungen
NOVVIA.MSV3BestellungPos  - MSV3-Bestellpositionen
NOVVIA.ABdataArtikel      - Pharma-Stammdaten
NOVVIA.ABdataArtikelMapping - PZN↔Artikel Zuordnung
NOVVIA.ImportVorlage      - Import-Templates
NOVVIA.ImportLog          - Import-Historie
NOVVIA.WorkerStatus       - Worker-Status
NOVVIA.AusgabeLog         - Ausgabe-Historie
NOVVIA.DokumentArchiv     - Archivierte Dokumente
```

#### Business Entities
- **Artikel**: Beschreibungen, Merkmale, Preise, Staffelpreise, Bilder, Kategorien, Lieferanten, Stücklisten
- **Kunde**: KundeAdresse, Bestellungen
- **Bestellung**: BestellPosition, BestellAdresse, Rechnungen, Lieferscheine
- **Rechnung/Gutschrift**: RechnungsPosition, Zahlungseingang
- **Einkauf**: EinkaufsBestellung, EinkaufsBestellPosition, Wareneingang
- **RMA/Retouren**: RMA, RMAPosition, ArtikelZustand

### MSV3-Anbindung (Pharma-Großhandel) - FUNKTIONIERT

**Status**: GEHE Bestandsabfrage funktioniert mit Version 1

**Letzter Stand (23.12.2024):**
- GEHE funktioniert mit **Version 1** (nicht Version 2!)
- Cookie-Warmup und User-Agent Header für Incapsula WAF
- Bestandsabfrage in Bestellungen funktioniert

**Dateien:**
- `Services/MSV3Service.cs` - SOAP-Kommunikation (V1+V2)
- `Views/LieferantenPage.xaml` - UI mit Versions-ComboBox
- `Views/LieferantenPage.xaml.cs` - Code-Behind mit Debug-Output
- `Entities/BestellungMSV3ViewEntities.cs` - View-Entities
- `Scripts/SP-MSV3LieferantSpeichern.sql` - DB Stored Procedure

**Code-Änderungen Session 19.12.2024:**
1. `MSV3Service.cs`:
   - User-Agent Header: `Mozilla/5.0 (Windows NT 10.0; Win64; x64) NovviaERP/1.0 MSV3Client`
   - Accept Header: `application/soap+xml, text/xml, */*`
   - Bessere Fehler-Rückmeldung mit URL die versucht wurde

2. `LieferantenPage.xaml`:
   - Versions-ComboBox hinzugefügt (Row 5)

3. `LieferantenPage.xaml.cs`:
   - Version aus ComboBox lesen statt hardcoded
   - Version beim Laden aus DB setzen

**Unterstützte Operationen:**
- `VerbindungTestenAsync` - Verbindungstest
- `CheckVerfuegbarkeitAsync` - Verfügbarkeitsabfrage
- `SendBestellungAsync` - Bestellung senden (mit MinMHD)
- Automatische URL-Pfad-Erkennung (/msv3, /msv3/v2.0/ActionName)
- SOAP 1.1 + 1.2 Support
- Preemptive HTTP Basic Auth + SOAP Body Credentials

**Bekannte Großhändler-URLs:**
```
GEHE:      https://www.gehe-auftragsservice.de/msv3  (v1.0! Incapsula WAF)
Phoenix:   https://msv3.phoenixgroup.eu/msv3         (v1.0 + v2.0)
Sanacorp:  https://msv3.sanacorp.de/msv3             (v1.0 + v2.0)
Noweda:    https://msv3.noweda.de/msv3
Alliance:  https://webservice.alliance-healthcare.de/web/msv3
```

**MSV3 v2.0 URL-Struktur:**
```
Basis:     /msv3
Endpoints: /msv3/v2.0/VerbindungTesten
           /msv3/v2.0/VerfuegbarkeitAnfragen
           /msv3/v2.0/Bestellen
```

**SOAP-Format (MSV3 v2):**
```xml
<?xml version="1.0" encoding="UTF-8"?>
<soap:Envelope xmlns:soap="http://www.w3.org/2003/05/soap-envelope" xmlns:msv="urn:msv3:v2">
   <soap:Body>
      <msv:VerbindungTesten>
         <msv:Clientsystem>NovviaERP</msv:Clientsystem>
         <msv:Benutzerkennung>BENUTZERNAME</msv:Benutzerkennung>
         <msv:Kennwort>PASSWORT</msv:Kennwort>
      </msv:VerbindungTesten>
   </soap:Body>
</soap:Envelope>
```

**GEHE Konfiguration (funktioniert!):**
```
URL:      https://www.gehe-auftragsservice.de/msv3
Benutzer: 152776
Passwort: rkwoib63
Version:  1   <-- WICHTIG: Version 1, nicht 2!
```

**Incapsula WAF (GEHE) - GELÖST:**
- GEHE verwendet Incapsula/Imperva WAF
- Lösung: Cookie-Warmup + User-Agent Header `Embarcadero URI Client/1.0`
- Mit Version 1 funktioniert die Bestandsabfrage

**Build-Hinweise:**
1. **WICHTIG: Alle NovviaERP Prozesse beenden** vor dem Build:
   ```powershell
   Get-Process | Where-Object { $_.ProcessName -like "*Novvia*" } | Stop-Process -Force
   ```
2. Build ausführen:
   ```powershell
   cd C:\NovviaERP\src\NovviaERP\NovviaERP.WPF
   dotnet build
   ```
3. Debug-Version starten: `bin\Debug\net8.0-windows\NovviaERP.WPF.exe`

**Häufiger Build-Fehler:**
```
error MSB3021: Access to the path '...\NovviaERP.Core.dll' is denied.
```
→ Lösung: Task-Manager öffnen, alle `NovviaERP.WPF.exe` beenden

**UI:** `Views/LieferantenPage.xaml` → Tab "Lieferanten" → MSV3-Konfiguration
- URL, Benutzer, Passwort, Kundennr, Filiale, **Version**, Aktiv

### WPF Views (NovviaERP.WPF/Views/)

| View | Beschreibung |
|------|-------------|
| `DashboardPage` | Startseite mit KPIs |
| `KundenPage` | Kundenverwaltung |
| `ArtikelPage` | Artikelverwaltung |
| `BestellungenPage` | Kundenbestellungen |
| `RechnungenPage` | Rechnungsverwaltung |
| `LagerPage` | Lagerverwaltung |
| `VersandPage` | Versand + Label-Druck |
| `MahnungenPage` | Mahnwesen |
| `RetourenPage` | RMA/Retouren |
| `LieferantenPage` | Lieferanten + MSV3 + Einkauf |
| `WooCommercePage` | Shop-Synchronisation |
| `EMailVorlagenPage` | E-Mail Templates |
| `EinstellungenPage` | Systemeinstellungen |
| `BenutzerPage` | Benutzerverwaltung |
| `FormularDesignerPage` | Formular-Editor |

### Worker Services (NovviaERP.Worker/)

| Worker | Intervall | Beschreibung |
|--------|-----------|-------------|
| `ZahlungsabgleichWorker` | 15 Min | Bank-Zahlungen abgleichen |
| `WooCommerceSyncWorker` | 5 Min | Shop-Bestellungen importieren |
| `MahnlaufWorker` | 1x täglich | Mahnungen erstellen |
| `WorkflowQueueWorker` | 1 Min | Workflow-Jobs verarbeiten |

### Wichtige Dateipfade

```
src/NovviaERP/
├── NovviaERP.Core/
│   ├── Data/JtlDbContext.cs          # Zentraler DB-Zugriff
│   ├── Infrastructure/Jtl/           # JTL SP-Clients
│   │   ├── JtlOrderClient.cs         # Auftrags-Eckdaten
│   │   └── JtlStockBookingClient.cs  # Lagerbuchungen
│   ├── Services/                      # Business-Logik
│   └── Entities/                      # Datenmodelle
├── NovviaERP.WPF/
│   ├── App.xaml.cs                   # DI-Setup, Session
│   └── Views/                        # UI-Pages
├── NovviaERP.Worker/
│   └── Program.cs                    # Worker-Host
├── NovviaERP.API/
│   └── Controllers/                  # REST-Endpoints
└── Scripts/
    ├── INSTALL-NOVVIA-Complete.sql   # Master-Setup (Neuinstallation)
    ├── README.md                     # Setup-Anleitung
    ├── Setup-NovviaTables.sql        # Basis-Tabellen
    ├── Setup-NOVVIA-Mandant2.sql     # Pharma-spezifisch
    └── SP-*.sql                      # Stored Procedures
```

### Offene Aufgaben / Known Issues

1. **MSVE** (Elektronischer Lieferschein) - Noch nicht implementiert

2. **Automatische Nachbestellung** - Workflow für Mindestbestand → Bestellung

### Letzte Code-Änderungen (29.12.2024)

**VersandPage - Komplett überarbeitet:**
- Kompletter Versand-Workflow: Lieferschein + tVersand + Tracking in einem Schritt
- `VersandBuchenAsync` - Automatisch: Lieferschein erstellen, tVersand anlegen, Label speichern
- `GetShippingConfigAsync` / `SaveShippingConfigAsync` - Config aus NOVVIA.Einstellungen
- `GetOrCreateLieferscheinAsync` - Lieferschein holen oder erstellen
- `GetAuftragLieferadresseAsync` / `GetAuftragRechnungsadresseAsync` - Adressen laden
- `GetVersandLabelAsync` - Label-PDF aus DB laden
- Neuer `VersandEinstellungenDialog` - DHL/DPD/GLS/UPS Zugangsdaten konfigurieren
- Labels werden gespeichert: DB (tVersand.bLabel) + lokal (Dokumente\NovviaERP\Labels\)

**SP-NOVVIA-OrderCreateUpdateDelete.sql (NEU):**
- `NOVVIA.spOrderCreateUpdateDelete` - CREATE/UPDATE/DELETE für Aufträge
  - CREATE: Nächste Auftragsnummer aus tLaufendeNummern, Adressen anlegen
  - UPDATE: Auftrag + Adressen aktualisieren
  - DELETE: Soft-Delete (nStorno=1), Storno-Eintrag in tAuftragStorno
- `NOVVIA.spOrderPositionAddUpdate` - ADD/UPDATE/DELETE für Positionen
  - Lädt Artikeldaten automatisch (Name, Preis, MwSt)
  - Ruft Verkauf.spAuftragEckdatenBerechnen auf

**AuftragsstapelimportView - Excel/CSV Import:**
- Excel-Import mit ClosedXML (flexible Spalten-Erkennung)
- CSV-Import
- Liest JTL-Nummernkreise aus tLaufendeNummern (kLaufendeNummer = 3)
- Mindest-MHD Berechnung (Datum oder Offset)
- Gruppiert Positionen nach Kunde → ein Auftrag pro Kunde

**LieferantenBestellungDetailView - Einkauf komplett:**
- Artikel suchen (ArtNr oder Lieferanten-ArtNr)
- Lieferadresse (gleich Firma oder abweichend)
- Wareneingang buchen
- Eingangsrechnung erstellen (alle oder nur gelieferte Positionen)

### Code-Änderungen (28.12.2024)

**Einstellungen - JTL-Tabellenstruktur Fix:**
- SQL-Queries an echte JTL-Wawi 1.11 Tabellenstruktur angepasst
- `tFirma`: cEMail statt cMail, cUnternehmer statt cZusatz
- `tZahlungsart`: cPaymentOption statt cModulId
- `tVersandart`: fPrice statt fPreis, cDruckText für Lieferzeit
- USt-ID aus `tFirmaUStIdNr` (separate Tabelle, nicht in tFirma)
- Steuersätze: JOIN tSteuerklasse mit tSteuersatz (kSteuerzone=3 für DE)

**Einstellungen - Eigene Felder Tab:**
- Neuer Tab "Eigene Felder" in EinstellungenView
- Zeigt `Firma.tFirmaEigenesFeld` mit Attribut-Namen aus `tAttributSprache`
- Spalten: Attribut, Text, Zahl (Int), Zahl (Dec), Datum

**Rechnung Stornieren - Logik korrigiert:**
- `StornoRechnungAsync`: Nur Rechnung auf Status 5 setzen, KEINE Gutschrift
- Auftrag wird wieder bearbeitbar (Preis, Anschrift)
- KEINE Lagerbewegungen - nur über Retoure änderbar
- "Rechnungskorrektur" umbenannt zu "Gutschrift"

**CoreService.cs - Neue Methoden:**
- `GetFirmaEigeneFelderAsync()` - Firma eigene Felder laden
- `GetFirmendatenAsync()` - USt-ID aus tFirmaUStIdNr JOIN
- `UpdateFirmendatenAsync()` - USt-ID in tFirmaUStIdNr speichern

**DB-Setup: INSTALL-NOVVIA-Complete.sql**
- Master-Installationsscript für Neuinstallationen
- Enthält alle NOVVIA-Tabellen, Views, SPs, Types
- README.md mit Installationsanleitung
- Aufruf: `USE Mandant_X; GO` dann Script ausführen

**JtlOrderClient (Infrastructure/Jtl/):**
- Neuer Client für JTL Auftrags-Stored-Procedures
- `BerechneAuftragEckdatenAsync` - ruft Verkauf.spAuftragEckdatenBerechnen
- `GetAuftragEckdatenAsync` - liest berechnete Eckdaten
- Status-Helper: GetZahlungStatusText, GetLieferStatusText, GetLieferStatusColor

**BestellungDetailView (Views/):**
- Komplett neues Layout (JTL-Style)
- Positionen auf erster Seite sichtbar
- Tabs: Auftragsdaten, Anhänge, Kosten (links)
- Tabs: Details, Texte, Eigene Felder (rechts)
- Zusammenfassung: Gewichte + Auftragswert

**ArtikelDetailView.xaml.cs:**
- MSV3 Bestandsabfrage für GEHE/Alliance korrigiert
- GEHE/Alliance verwendet jetzt `CheckVerfuegbarkeitViaBestellenAsync` (wie in Bestellungen)
- Andere Lieferanten verwenden weiterhin `CheckVerfuegbarkeitAsync`
- GEHE-Erkennung über URL: "gehe" oder "alliance" im Hostnamen

**Msv3SinglePznJob.cs (Worker):**
- Neuer CLI-Modus für On-Demand PZN-Bestandsabfrage
- Aufruf: `NovviaERP.Worker.exe --mode msv3-stock --pzn 14036711`
- Cookie-Warmup für Incapsula WAF
- SOAP v1.0 und v2.0 Support
- Cache in `NOVVIA.MSV3BestandCache` (5 Minuten TTL)

### Nach PC-Neustart

1. Build ausführen:
   ```powershell
   cd C:\NovviaERP\src\NovviaERP\NovviaERP.WPF
   dotnet build
   ```

2. Debug-Version starten:
   ```
   C:\NovviaERP\src\NovviaERP\NovviaERP.WPF\bin\Debug\net8.0-windows\NovviaERP.WPF.exe
   ```

3. MSV3 testen (Lieferanten → GEHE → Version 1 → Testen)

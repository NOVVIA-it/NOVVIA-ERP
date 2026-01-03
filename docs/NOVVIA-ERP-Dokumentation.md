# NOVVIA ERP - Systemdokumentation

> **Version:** 1.0.3
> **Letzte Aktualisierung:** 2026-01-03
> **Autor:** NOVVIA IT / Claude AI

---

## Inhaltsverzeichnis

1. [Uebersicht](#1-uebersicht)
2. [Technische Architektur](#2-technische-architektur)
3. [Module & Funktionen](#3-module--funktionen)
4. [Services (Backend)](#4-services-backend)
5. [Views (Frontend)](#5-views-frontend)
6. [Datenmodell (Entities)](#6-datenmodell-entities)
7. [Datenbank](#7-datenbank)
8. [Integrationen](#8-integrationen)
9. [Konfiguration](#9-konfiguration)
10. [Aenderungsprotokoll](#10-aenderungsprotokoll)

---

## 1. Uebersicht

### 1.1 Was ist NOVVIA ERP?

NOVVIA ERP ist ein deutschsprachiges Enterprise Resource Planning System, entwickelt als WPF-Desktop-Anwendung fuer:

- **E-Commerce Unternehmen** mit Multi-Channel-Vertrieb
- **Pharma-Grosshandel** mit MSV3-Integration
- **Lagerverwaltung** mit Chargen-/Chargennummern-Tracking
- **B2B und B2C Handel** mit JTL-Wawi Datenbank-Kompatibilitaet

### 1.2 Kernfunktionen

| Bereich | Funktionen |
|---------|-----------|
| **Stammdaten** | Kunden, Artikel, Lieferanten, Kategorien |
| **Verkauf** | Auftraege, Angebote, Rechnungen, Gutschriften |
| **Einkauf** | Lieferantenbestellungen, Eingangsrechnungen |
| **Lager** | Bestandsfuehrung, Chargen, Ein-/Ausgaenge |
| **Versand** | DHL, DPD, GLS Label-Erstellung |
| **Finanzen** | Zahlungsabgleich, SEPA, MT940/CAMT Import |
| **Pharma** | MSV3-Protokoll, ABdata-Integration |
| **E-Commerce** | WooCommerce, Plattform-Sync |

### 1.3 Systemanforderungen

- Windows 10/11 (64-bit)
- .NET 8.0 Runtime
- SQL Server 2019+ (JTL-kompatibel)
- Min. 8 GB RAM, 10 GB Festplatte

---

## 2. Technische Architektur

### 2.1 Technologie-Stack

```
┌─────────────────────────────────────────────────┐
│                 NovviaERP.WPF                   │
│         (WPF Desktop Application)               │
│    ┌─────────────────────────────────────┐      │
│    │  Views (66 XAML)  │  Controls       │      │
│    └─────────────────────────────────────┘      │
└─────────────────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────┐
│                NovviaERP.Core                   │
│    ┌─────────────┐  ┌─────────────────────┐     │
│    │ Services    │  │ Entities            │     │
│    │ (30 Klassen)│  │ (180+ Datenmodelle) │     │
│    └─────────────┘  └─────────────────────┘     │
│    ┌─────────────────────────────────────┐      │
│    │         JtlDbContext (Dapper)       │      │
│    └─────────────────────────────────────┘      │
└─────────────────────────────────────────────────┘
                        │
                        ▼
┌─────────────────────────────────────────────────┐
│              SQL Server Datenbank               │
│         (JTL-Wawi kompatibles Schema)           │
└─────────────────────────────────────────────────┘
```

### 2.2 Projektstruktur

```
C:\NovviaERP\
├── src\NovviaERP\
│   ├── NovviaERP.WPF\           # WPF Anwendung
│   │   ├── Views\               # 66 XAML Views
│   │   ├── Controls\            # Wiederverwendbare Controls
│   │   └── App.xaml.cs          # DI Container, Startup
│   │
│   ├── NovviaERP.Core\          # Business Logic
│   │   ├── Services\            # 30 Service-Klassen
│   │   ├── Entities\            # Datenmodelle
│   │   ├── Data\                # Datenbankzugriff
│   │   └── Modules\             # Spezial-Module
│   │
│   └── NovviaERP.Worker\        # Hintergrund-Jobs
│
├── Scripts\                     # SQL/JS Skripte
├── Docs\                        # Dokumentation
├── TestData\                    # Testdaten
├── logs\                        # Serilog Logs
├── START.bat                    # Start-Skript
└── run_worker_pzn.bat           # Worker-Skript
```

### 2.3 Dependency Injection

Services werden in `App.xaml.cs` registriert:

**Singleton (eine Instanz):**
- JtlDbContext, CoreService, EinkaufService
- MSV3Service, ABdataService
- ZahlungsabgleichService, SepaService
- AppDataService

**Transient (neue Instanz pro Aufruf):**
- AngebotService, AusgabeService, AuftragsImportService
- PlattformService, EmailVorlageService, ReportService
- DruckerService, ShippingService, WooCommerceService
- PaymentService, StammdatenService, WorkflowService
- EigeneFelderService

---

## 3. Module & Funktionen

### 3.1 Dashboard

**View:** `DashboardPage.xaml`

- Uebersicht Tagesumsatz, offene Auftraege
- Schnellzugriff auf haeufige Aktionen
- Lagerbestandswarnungen
- Offene Zahlungen

### 3.2 Stammdaten

#### 3.2.1 Kundenverwaltung

**Views:** `KundenView.xaml`, `KundeDetailView.xaml`
**Service:** `CoreService.cs`

| Funktion | Beschreibung |
|----------|-------------|
| Kundensuche | Nach Name, Nr, Email, Ort |
| Kundenanlage | Privat/Firma mit Adressen |
| Adressen | Mehrere Liefer-/Rechnungsadressen |
| Bankverbindung | IBAN, BIC, Mandatsreferenz |
| Kundenhistorie | Bestellungen, Rechnungen, Zahlungen |
| Eigene Felder | Benutzerdefinierte Attribute |
| 360-Grad-Sicht | Umsatz, letzte Bestellung, Status |

#### 3.2.2 Artikelverwaltung

**Views:** `ArtikelView.xaml`, `ArtikelDetailView.xaml`
**Service:** `CoreService.cs`

| Funktion | Beschreibung |
|----------|-------------|
| Artikelsuche | EAN, SKU, Name, Kategorie |
| Preisgestaltung | VK, EK, Staffelpreise, Sonderpreise |
| Lagerbestand | Multi-Lager, Chargen, MHD |
| Kategorien | Hierarchische Kategoriestruktur |
| Lieferanten | Mehrere Lieferanten pro Artikel |
| Stuecklisten | Komponenten, Bundles |
| Eigene Felder | Benutzerdefinierte Attribute |
| WooCommerce | Sync-Status, Shop-Beschreibungen |

#### 3.2.3 Lieferantenverwaltung

**Views:** `LieferantenView.xaml`, `LieferantenAuswahlDialog.xaml`
**Service:** `CoreService.cs`, `EinkaufService.cs`

| Funktion | Beschreibung |
|----------|-------------|
| Lieferantensuche | Name, Nr, Kontakt |
| Lieferantenanlage | Firma, Ansprechpartner, Konditionen |
| Artikelzuordnung | Welcher Artikel von welchem Lieferant |
| MSV3/ABdata | Pharma-Grosshandel Integration |
| Bestellhistorie | Vergangene Einkaufsbestellungen |
| Eigene Felder | Benutzerdefinierte Attribute |

### 3.3 Verkauf

#### 3.3.1 Auftragsverwaltung

**Views:** `BestellungenView.xaml`, `BestellungDetailView.xaml`
**Service:** `CoreService.cs`, `AngebotService.cs`

| Funktion | Beschreibung |
|----------|-------------|
| Auftragssuche | Status, Datum, Kunde, Nummer |
| Auftragserfassung | Positionen, Rabatte, Versand |
| Angebote | Erstellen, In Auftrag wandeln |
| Positionen | Artikel, Freitext, Rabatte |
| Adresswahl | Abweichende Liefer-/Rechnungsadresse |
| Statusverfolgung | Offen, In Bearbeitung, Versendet |
| Dokumente | Auftragsbestaetigung, Lieferschein |
| Eigene Felder | Benutzerdefinierte Attribute |

#### 3.3.2 Rechnungsverwaltung

**Views:** `RechnungenView.xaml`, `RechnungDetailView.xaml`
**Service:** `CoreService.cs`, `ReportService.cs`

| Funktion | Beschreibung |
|----------|-------------|
| Rechnungssuche | Status, Datum, Kunde, Nummer |
| Rechnungserstellung | Aus Auftrag, Manuell |
| Zahlungsstatus | Offen, Teilbezahlt, Bezahlt |
| PDF-Generierung | QuestPDF mit Firmenbranding |
| Mahnwesen | Mahnstufen, Mahngebühren |
| Storno | Rechnungsstorno mit Gutschrift |

### 3.4 Einkauf

#### 3.4.1 Einkaufsbestellungen

**Views:** `LieferantenBestellungPage.xaml`, `LieferantenBestellungDetailView.xaml`
**Service:** `EinkaufService.cs`

| Funktion | Beschreibung |
|----------|-------------|
| Bestellvorschlag | Basierend auf Mindestbestand |
| Bestellerfassung | Positionen, Konditionen |
| MSV3-Bestellung | Pharma-Grosshandel Direktbestellung |
| Statusverfolgung | Bestellt, Teilgeliefert, Geliefert |
| Wareneingang | Erfassung mit Chargennummern |

#### 3.4.2 Eingangsrechnungen

**Views:** `EingangsrechnungenPage.xaml`, `EingangsrechnungDetailView.xaml`
**Service:** `EinkaufService.cs`

| Funktion | Beschreibung |
|----------|-------------|
| Rechnungserfassung | Manuell, Per Scan |
| Zuordnung | Zu Einkaufsbestellung |
| Zahlungsstatus | Offen, Bezahlt |
| SEPA-Export | Sammellastschrift |

### 3.5 Lager & Logistik

#### 3.5.1 Lagerverwaltung

**Views:** `LagerView.xaml`, `LagerChargenView.xaml`
**Service:** `CoreService.cs`

| Funktion | Beschreibung |
|----------|-------------|
| Bestandsuebersicht | Multi-Lager, Lagerplaetze |
| Chargen | Chargennummern, MHD-Verwaltung |
| Bewegungen | Ein-/Ausgaenge, Umbuchungen |
| Filter | Nur mit Bestand, Kategorie |
| Chargen-Ausgaenge | Tracking fuer Rueckrufaktionen |
| Inventur | Bestandskorrekturen |

#### 3.5.2 Versand

**Views:** `VersandPage.xaml`, `VersandView.xaml`
**Service:** `ShippingService.cs`, `DruckerService.cs`

| Funktion | Beschreibung |
|----------|-------------|
| Versandauftraege | Aus Auftraegen erstellen |
| Label-Erstellung | DHL, DPD, GLS |
| Tracking | Sendungsverfolgung |
| Packliste | PDF-Generierung |
| Etikettendruck | Direkt oder PDF |

### 3.6 Finanzen

#### 3.6.1 Zahlungsabgleich

**Views:** `ZahlungsabgleichView.xaml`, `ZahlungZuordnenDialog.xaml`
**Service:** `ZahlungsabgleichService.cs`, `SepaService.cs`

| Funktion | Beschreibung |
|----------|-------------|
| MT940 Import | SWIFT Kontoauszuege |
| CAMT.053 Import | XML Kontoauszuege |
| Auto-Matching | Rechnungsnr, Betrag, IBAN |
| Manuelle Zuordnung | Dialog mit Rechnungssuche |
| SEPA Lastschrift | XML Export (pain.008) |
| PayPal/Mollie | Provider-Sync (geplant) |

### 3.7 Tools

#### 3.7.1 Ameise (Import/Export)

**Views:** `AmeiseView.xaml`
**Service:** `ImportService.cs`, `AmeiseService.cs`

| Funktion | Beschreibung |
|----------|-------------|
| CSV Import | Kunden, Artikel, Auftraege |
| Excel Import | Spalten-Mapping |
| Auto-Erkennung | Spaltentypen |
| Vorlagen | Speicherbare Import-Mappings |
| Export | Datenexport in verschiedene Formate |

#### 3.7.2 Eigene Felder

**Views:** `EigeneFelderView.xaml`
**Service:** `EigeneFelderService.cs`

| Funktion | Beschreibung |
|----------|-------------|
| Felddefinition | Text, Zahl, Datum, Auswahl |
| Bereiche | Artikel, Kunde, Auftrag, etc. |
| Pflichtfelder | Optional/Pflicht |
| Sortierung | Reihenfolge der Anzeige |

### 3.8 Einstellungen

**Views:** `EinstellungenView.xaml`, `BenutzerPage.xaml`
**Service:** `AuthService.cs`, `AppDataService.cs`

| Funktion | Beschreibung |
|----------|-------------|
| Firmendaten | Name, Adresse, Logo |
| Benutzer | Anlegen, Rollen, Rechte |
| Nummernkreise | RE-, AU-, LS-Nummern |
| Drucker | Standard-Drucker pro Dokument |
| Email | SMTP-Konfiguration |
| Plattformen | Shop-Verbindungen |

---

## 4. Services (Backend)

### 4.1 Uebersicht aller Services

| Service | Zweck | Typ |
|---------|-------|-----|
| **CoreService** | Haupt-Geschaeftslogik (Kunden, Artikel, Auftraege) | Singleton |
| **AngebotService** | Angebotsverwaltung | Transient |
| **EinkaufService** | Einkauf/Beschaffung | Singleton |
| **MSV3Service** | Pharma-Grosshandel API | Singleton |
| **ABdataService** | Pharma-Datenservice | Singleton |
| **ZahlungsabgleichService** | Bank-Transaktionen, Matching | Singleton |
| **SepaService** | SEPA Lastschrift XML | Singleton |
| **PaymentService** | PayPal, Mollie | Transient |
| **ShippingService** | DHL, DPD, GLS | Transient |
| **ReportService** | PDF-Generierung (QuestPDF) | Transient |
| **DruckerService** | Drucker-Verwaltung | Transient |
| **PlattformService** | E-Commerce Plattformen | Transient |
| **WooCommerceService** | WooCommerce Sync | Transient |
| **EmailVorlageService** | Email-Templates | Transient |
| **WorkflowService** | Automatisierung | Transient |
| **StammdatenService** | Firma, Lager, Kategorien | Transient |
| **EigeneFelderService** | Benutzerdefinierte Felder | Transient |
| **AuthService** | Login, JWT, Rechte | (Core) |
| **AppDataService** | Lokale Einstellungen | Singleton |
| **LogService** | Audit-Logging | (Core) |
| **ImportService** | CSV/Excel Import | (Core) |
| **AuftragsImportService** | Auftrags-Import | Transient |
| **AmeiseService** | Import/Export Tool | (Core) |
| **AusgabeService** | Chargen-Ausgaben | Transient |
| **FormularService** | Formular-Templates | (Core) |
| **SteuerService** | Steuerberechnung | (Core) |
| **SchnellerfassungService** | Quick-Entry | (Core) |
| **WorkerService** | Hintergrund-Jobs (Stub) | (Core) |
| **BenutzerService** | Benutzerverwaltung | (Core) |
| **ZahlungsanbieterService** | Payment Provider | (Core) |

### 4.2 Wichtige Service-Methoden

#### CoreService.cs (Haupt-Service)

```csharp
// Kunden
GetKundenAsync(suchbegriff, limit, offset)
GetKundeByIdAsync(id, mitDetails)
CreateKundeAsync(kunde)
UpdateKundeAsync(kunde)
GetKundenStatistikAsync(kundeId)  // 360-Grad-Sicht

// Artikel
GetArtikelAsync(suchbegriff, kategorieId, nurAktive)
GetArtikelByIdAsync(id, mitDetails)
CreateArtikelAsync(artikel)
UpdateArtikelAsync(artikel)
GetArtikelLagerbestandAsync(artikelId)

// Auftraege
GetAuftraegeAsync(von, bis, status, kundeId)
GetAuftragByIdAsync(id)
CreateAuftragAsync(auftrag)
UpdateAuftragAsync(auftrag)
DeleteAuftragAsync(id)

// Rechnungen
GetRechnungenAsync(von, bis, status)
GetRechnungByIdAsync(id)
RechnungAusAuftragAsync(auftragId)

// Lager
GetLagerbestaendeAsync(lagerplatzId)
GetChargenAsync(artikelId)
BucheChargenAusgangAsync(chargenId, menge, grund)
```

#### ZahlungsabgleichService.cs

```csharp
ImportMT940Async(filePath)       // SWIFT Import
ImportCAMTAsync(filePath)        // XML Import
GetAllTransaktionenAsync(von, bis, nurUnmatched)
MatchZahlungenAsync()            // Auto-Matching
FindBestMatchAsync(zahlung)      // Einzelne Zahlung matchen
SucheOffeneRechnungenAsync(suchbegriff)
ZuordnenAsync(zahlungsId, rechnungId, betrag)
```

#### SepaService.cs

```csharp
GetSepaFaelligAsync()            // Faellige Lastschriften
GenerateSepaDirectDebitXmlAsync(rechnungIds, config, datum)
ValidateIBAN(iban)               // Modulo 97 Pruefung
```

---

## 5. Views (Frontend)

### 5.1 Uebersicht nach Kategorie

#### Hauptfenster
| View | Datei | Beschreibung |
|------|-------|-------------|
| Hauptfenster | `MainWindow.xaml` | Shell mit Navigation |
| Login | `LoginWindow.xaml` | Anmeldung |
| Dashboard | `DashboardPage.xaml` | Startseite |

#### Stammdaten (8 Views)
| View | Datei | Beschreibung |
|------|-------|-------------|
| Kundenliste | `KundenView.xaml` | Kundenübersicht |
| Kundendetail | `KundeDetailView.xaml` | Einzelkunde |
| Kundensuche | `KundenSuchDialog.xaml` | Suchdialog |
| Artikelliste | `ArtikelView.xaml` | Artikelübersicht |
| Artikeldetail | `ArtikelDetailView.xaml` | Einzelartikel |
| Artikelsuche | `ArtikelSuchDialog.xaml` | Suchdialog |
| Lieferanten | `LieferantenView.xaml` | Lieferantenübersicht |
| Lieferantenauswahl | `LieferantenAuswahlDialog.xaml` | Auswahldialog |

#### Verkauf (8 Views)
| View | Datei | Beschreibung |
|------|-------|-------------|
| Auftragsliste | `BestellungenView.xaml` | Auftragsübersicht |
| Auftragsdetail | `BestellungDetailView.xaml` | Einzelauftrag |
| Neuer Auftrag | `NeueBestellungView.xaml` | Auftragserfassung |
| Angebot | `AngebotDetailDialog.xaml` | Angebotsdialog |
| Rechnungsliste | `RechnungenView.xaml` | Rechnungsübersicht |
| Rechnungsdetail | `RechnungDetailView.xaml` | Einzelrechnung |
| Adressbearbeitung | `AdresseBearbeitenDialog.xaml` | Adressdialog |
| Adressauswahl | `AdresseAuswahlDialog.xaml` | Auswahldialog |

#### Einkauf (5 Views)
| View | Datei | Beschreibung |
|------|-------|-------------|
| Bestellungen | `LieferantenBestellungPage.xaml` | Einkaufsbestellungen |
| Bestelldetail | `LieferantenBestellungDetailView.xaml` | Einzelbestellung |
| Eingangsrechnungen | `EingangsrechnungenPage.xaml` | Lieferantenrechnungen |
| Eingangsrechnung | `EingangsrechnungDetailView.xaml` | Einzelrechnung |
| Dialog | `EingangsrechnungDialog.xaml` | Erfassungsdialog |

#### Lager (6 Views)
| View | Datei | Beschreibung |
|------|-------|-------------|
| Lagerbestand | `LagerView.xaml` | Bestandsübersicht |
| Chargen | `LagerChargenView.xaml` | Chargenverwaltung |
| Wareneingang | `WareneingangPage.xaml` | Wareneingangsseite |
| Wareneingang Dialog | `WareneingangDialog.xaml` | Erfassungsdialog |
| Versand | `VersandView.xaml` | Versandübersicht |
| Versand-Einstellungen | `VersandEinstellungenDialog.xaml` | Konfiguration |

#### Finanzen (2 Views)
| View | Datei | Beschreibung |
|------|-------|-------------|
| Zahlungsabgleich | `ZahlungsabgleichView.xaml` | Bank-Transaktionen |
| Zuordnungsdialog | `ZahlungZuordnenDialog.xaml` | Manuelle Zuordnung |

#### Tools (4 Views)
| View | Datei | Beschreibung |
|------|-------|-------------|
| Ameise | `AmeiseView.xaml` | Import/Export |
| Stapelimport | `AuftragsstapelimportView.xaml` | Batch-Import |
| Import | `ImportView.xaml` | Import-Wizard |
| Eigene Felder | `EigeneFelderView.xaml` | Feldverwaltung |

#### System (10 Views)
| View | Datei | Beschreibung |
|------|-------|-------------|
| Einstellungen | `EinstellungenView.xaml` | Konfiguration |
| Benutzer | `BenutzerPage.xaml` | Benutzerverwaltung |
| Profil-Manager | `ProfilManagerWindow.xaml` | Mandanten |
| Plattformen | `PlattformPage.xaml` | Shop-Verbindungen |
| Email-Vorlagen | `EmailVorlagenPage.xaml` | Templates |
| Stammdaten | `StammdatenPage.xaml` | Master-Data |
| Worker | `WorkerControlPage.xaml` | Hintergrund-Jobs |
| Berichte | `BerichtePage.xaml` | Reports |
| WooCommerce | `WooCommercePage.xaml` | Shop-Sync |
| Formulardesigner | `FormularDesignerPage.xaml` | Templates |

---

## 6. Datenmodell (Entities)

### 6.1 Entity-Dateien

| Datei | Anzahl Klassen | Beschreibung |
|-------|---------------|-------------|
| `BaseEntities.cs` | 23 | Kern-Domaenobjekte |
| `Entities.cs` | 10 | Erweiterte Modelle |
| `Stammdaten.cs` | 35 | Master-Data |
| `SystemEntities.cs` | 17 | System/Admin |
| `ErweiterteStammdaten.cs` | 11 | Bank, Steuer |
| `JtlEntities.cs` | 20 | JTL-Kompatibilitaet |
| `NovviaEntities.cs` | 8 | NOVVIA-spezifisch |

### 6.2 Wichtige Entities

#### Artikel
```
Artikel
├── ArtikelAttribut (Eigenschaften)
├── ArtikelBeschreibung (Sprachen)
├── ArtikelBild (Bilder)
├── ArtikelKategorie (Zuordnungen)
├── ArtikelLagerbestand (Bestaende)
├── ArtikelLieferant (Bezugsquellen)
├── ArtikelMerkmal (Merkmale)
├── ArtikelPreis (Preise)
├── ArtikelSonderpreis (Aktionen)
├── ArtikelStaffelpreis (Mengenrabatt)
├── ArtikelWooCommerce (Shop-Sync)
└── Stueckliste (Komponenten)
```

#### Bestellung/Auftrag
```
Bestellung
├── BestellPosition (Positionen)
├── BestellAdresse (Adressen)
└── Zahlung (Zahlungen)
```

#### Kunde
```
Kunde
├── KundeAdresse (Adressen)
├── Bankverbindung (SEPA)
└── KundeSteuereinstellung (USt)
```

---

## 7. Datenbank

### 7.1 Verbindung

**Datei:** `NovviaERP.Core/Data/JtlDbContext.cs`

- ORM: **Dapper** (kein Entity Framework)
- Datenbank: SQL Server 2019+
- Schema: JTL-Wawi kompatibel
- Connection: Pro Mandant konfigurierbar

### 7.2 Wichtige Tabellen

#### Stammdaten
| Tabelle | Beschreibung |
|---------|-------------|
| tArtikel | Artikel/Produkte |
| tKunde | Kunden |
| tLieferant | Lieferanten |
| tKategorie | Kategorien |
| tWarenlager | Lagerplaetze |
| tFirma | Firmendaten |

#### Verkauf
| Tabelle | Schema | Beschreibung |
|---------|--------|-------------|
| tAuftrag | Verkauf | Auftraege |
| tAuftragPos | Verkauf | Auftragspositionen |
| tRechnung | Rechnung | Rechnungen |
| tRechnungEckdaten | Rechnung | Rechnungs-Summen |
| tLieferschein | dbo | Lieferscheine |

#### Einkauf
| Tabelle | Schema | Beschreibung |
|---------|--------|-------------|
| tLieferantenBestellung | dbo | Einkaufsbestellungen |
| tLieferantenBestellungPos | dbo | Positionen |

#### Lager
| Tabelle | Beschreibung |
|---------|-------------|
| tLagerbestand | Artikelbestaende |
| tChargen | Chargennummern |
| tWareneingang | Wareneingaenge |

#### Finanzen
| Tabelle | Beschreibung |
|---------|-------------|
| tZahlung | Zahlungen |
| tZahlungsabgleichUmsatz | Bank-Transaktionen |
| tZahlungsart | Zahlungsarten |

#### NOVVIA-Schema
| Tabelle | Beschreibung |
|---------|-------------|
| NOVVIA.Log | Audit-Log |
| NOVVIA.EigeneFelder | Feldwerte |
| NOVVIA.ChargenAusgang | Chargen-Tracking |
| NOVVIA.ArtikelShopBeschreibung | Shop-Texte |
| NOVVIA.LieferantAttribut | Lieferanten-Felder |

---

## 8. Integrationen

### 8.1 Versanddienstleister

| Anbieter | Status | Funktionen |
|----------|--------|-----------|
| **DHL** | Aktiv | Label, Tracking, Retoure |
| **DPD** | Aktiv | Label, Tracking |
| **GLS** | Aktiv | Label, Tracking |

**Service:** `ShippingService.cs`

### 8.2 Payment Provider

| Anbieter | Status | Funktionen |
|----------|--------|-----------|
| **PayPal** | Geplant | Transaktionen, Links |
| **Mollie** | Geplant | Transaktionen, Checkout |
| **SEPA** | Aktiv | Lastschrift XML (pain.008) |

**Services:** `PaymentService.cs`, `SepaService.cs`

### 8.3 Bank-Import

| Format | Status | Beschreibung |
|--------|--------|-------------|
| **MT940** | Aktiv | SWIFT Kontoauszuege |
| **CAMT.053** | Aktiv | XML Kontoauszuege |
| **HBCI/FinTS** | Geplant | Direktabruf |

**Service:** `ZahlungsabgleichService.cs`

### 8.4 E-Commerce

| Plattform | Status | Funktionen |
|-----------|--------|-----------|
| **WooCommerce** | Aktiv | Artikel-Sync, Bestand, Auftraege |
| **JTL-Shop** | Nativ | Via JTL-Datenbank |

**Services:** `WooCommerceService.cs`, `PlattformService.cs`

### 8.5 Pharma (MSV3)

| Grosshaendler | Status | Beschreibung |
|---------------|--------|-------------|
| **Sanacorp** | Aktiv | MSV3-Protokoll |
| **Phoenix** | Aktiv | MSV3-Protokoll |
| **Alliance** | Aktiv | MSV3-Protokoll |

**Services:** `MSV3Service.cs`, `ABdataService.cs`

### 8.6 Ameise (Import/Export)

**Service:** `AmeiseService.cs`
**View:** `AmeiseView.xaml`

NOVVIA ERP bietet einen Import/Export-Dienst (aehnlich JTL-Ameise) fuer Massendatenverarbeitung.

#### Import-Vorlagen

| Vorlage | Tabelle | Key-Spalte | Beschreibung |
|---------|---------|------------|--------------|
| **Artikel** | tArtikel | cArtNr | Artikel-Stammdaten |
| **Kunden** | tKunde | cKundenNr | Kunden-Stammdaten |
| **Preise** | tArtikel | cArtNr | Preise aktualisieren |
| **Bestaende** | tLagerbestand | cArtNr | Lagerbestaende |
| **Lieferanten** | tLieferant | cFirma | Lieferanten-Stammdaten |

#### Artikel-Spalten

| CSV-Spalte | DB-Spalte | Typ | Pflicht | Beschreibung |
|------------|-----------|-----|---------|--------------|
| ArtNr | cArtNr | String | Ja | Artikelnummer (Key) |
| Name | cName | String | Ja | Artikelname |
| Beschreibung | cBeschreibung | String | - | Langtext |
| VKBrutto | fVKBrutto | Decimal | - | Verkaufspreis Brutto |
| VKNetto | fVKNetto | Decimal | - | Verkaufspreis Netto |
| EKNetto | fEKNetto | Decimal | - | Einkaufspreis Netto |
| Barcode | cBarcode | String | - | Barcode/EAN |
| Gewicht | fGewicht | Decimal | - | Gewicht in kg |
| Lagerbestand | fLagerbestand | Decimal | - | Aktueller Bestand |
| Hersteller | kHersteller | FK | - | -> tHersteller.cName |
| Kategorie | kKategorie | FK | - | -> tKategorieSprache.cName |
| Aktiv | nAktiv | Bool | - | Artikel aktiv (1/0/ja/nein) |

#### Kunden-Spalten

| CSV-Spalte | DB-Spalte | Typ | Pflicht |
|------------|-----------|-----|---------|
| KundenNr | cKundenNr | String | Ja |
| Anrede | cAnrede | String | - |
| Firma | cFirma | String | - |
| Vorname | cVorname | String | - |
| Nachname | cNachname | String | Ja |
| Strasse | cStrasse | String | - |
| PLZ | cPLZ | String | - |
| Ort | cOrt | String | - |
| Land | cLand | String | - |
| Email | cMail | String | - |
| Telefon | cTel | String | - |
| UStID | cUStID | String | - |
| Kundengruppe | kKundenGruppe | FK | - |
| Rabatt | fRabatt | Decimal | - |

#### Import-Optionen

```csharp
var optionen = new AmeiseImportOptionen
{
    Trennzeichen = ";",           // CSV-Trennzeichen
    UpdateExistierend = true,     // Bestehende Datensaetze aktualisieren
    FehlerIgnorieren = false,     // Bei Fehler abbrechen?
    Transaktion = true            // Alle in einer Transaktion?
};
```

#### Export-Funktionen

```csharp
// CSV Export
var csv = await _ameise.ExportCsvAsync("Artikel", new ExportOptionen
{
    Trennzeichen = ";",
    Filter = "nAktiv = 1",
    Sortierung = "cArtNr",
    Limit = 1000
});

// Artikelbilder importieren (Dateiname = ArtNr)
var anzahl = await _ameise.ImportArtikelBilderAsync("C:\\Import\\Bilder");

// Massenupdate
var updated = await _ameise.MassenUpdateAsync("tArtikel", "fVKBrutto", 19.99m, "kKategorie = 5");
```

### 8.7 REST API

**Projekt:** `NovviaERP.API`
**Authentifizierung:** JWT Bearer Token

NOVVIA ERP bietet eine REST API fuer externe Integrationen (Shops, Apps, etc.).

#### Basis-URL

```
https://[server]:5001/api
```

#### Authentifizierung

```http
POST /api/auth/login
Content-Type: application/json

{
  "login": "admin",
  "passwort": "geheim123"
}

Response:
{
  "token": "eyJhbGciOiJIUzI1...",
  "benutzer": { "id": 1, "login": "admin", "name": "Administrator" },
  "rolle": "Admin",
  "berechtigungen": ["artikel:read", "artikel:write", ...]
}
```

#### Endpunkte

##### Auth Controller (`/api/auth`)

| Methode | Route | Auth | Beschreibung |
|---------|-------|------|--------------|
| POST | `/login` | - | Anmelden, Token erhalten |
| POST | `/logout` | Ja | Abmelden |
| POST | `/change-password` | Ja | Passwort aendern |
| GET | `/me` | Ja | Aktueller Benutzer |
| GET | `/check/{modul}/{aktion}` | Ja | Berechtigung pruefen |

##### Artikel Controller (`/api/artikel`)

| Methode | Route | Auth | Beschreibung |
|---------|-------|------|--------------|
| GET | `/` | - | Alle Artikel (suche, limit) |
| GET | `/{id}` | - | Artikel by ID |
| GET | `/barcode/{barcode}` | - | Artikel by Barcode |
| POST | `/` | Ja | Artikel erstellen |
| PUT | `/{id}` | Ja | Artikel aktualisieren |
| PATCH | `/{id}/bestand` | Ja | Bestand aktualisieren |

##### Bestellungen Controller (`/api/bestellungen`)

| Methode | Route | Auth | Beschreibung |
|---------|-------|------|--------------|
| GET | `/` | - | Alle Bestellungen (status, limit) |
| GET | `/{id}` | - | Bestellung by ID |
| POST | `/` | Ja | Bestellung erstellen |
| PATCH | `/{id}/status` | Ja | Status aendern |
| POST | `/{id}/rechnung` | Ja | Rechnung erstellen |
| POST | `/{id}/lieferschein` | Ja | Lieferschein erstellen |

##### Kunden Controller (`/api/kunden`)

| Methode | Route | Auth | Beschreibung |
|---------|-------|------|--------------|
| GET | `/` | - | Alle Kunden (suche, limit) |
| GET | `/{id}` | - | Kunde by ID |
| POST | `/` | Ja | Kunde erstellen |
| PUT | `/{id}` | Ja | Kunde aktualisieren |

##### Dashboard Controller (`/api/dashboard`)

| Methode | Route | Auth | Beschreibung |
|---------|-------|------|--------------|
| GET | `/stats` | Ja | Kennzahlen |
| GET | `/umsatz` | Ja | Umsatzstatistik |

#### Beispiele

```bash
# Artikel suchen
curl -X GET "https://server:5001/api/artikel?suche=laptop&limit=10"

# Artikel mit Token erstellen
curl -X POST "https://server:5001/api/artikel" \
  -H "Authorization: Bearer eyJhbGc..." \
  -H "Content-Type: application/json" \
  -d '{"cArtNr": "ART-001", "cName": "Test Artikel", "fVKBrutto": 99.99}'

# Bestand aktualisieren
curl -X PATCH "https://server:5001/api/artikel/123/bestand" \
  -H "Authorization: Bearer eyJhbGc..." \
  -d '{"menge": 50, "lagerId": 1, "grund": "Wareneingang"}'

# Bestellstatus aendern
curl -X PATCH "https://server:5001/api/bestellungen/456/status" \
  -H "Authorization: Bearer eyJhbGc..." \
  -d '{"status": "Versendet"}'
```

---

## 9. Konfiguration

### 9.1 Anwendungsstart

**Datei:** `App.xaml.cs`

1. Serilog konfigurieren (Log-Pfad: `C:\NovviaERP\logs\`)
2. Globale Exception Handler registrieren
3. Login-Fenster anzeigen
4. DI Container konfigurieren
5. Hauptfenster oeffnen

### 9.2 Logging

**Bibliothek:** Serilog
**Pfad:** `C:\NovviaERP\logs\novviaerp-{datum}.log`

```
{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}
```

### 9.3 Lokale Einstellungen

**Pfad:** `%APPDATA%\NovviaERP\`

- `profiles.json` - Mandanten/Login-Profile
- `settings.json` - Benutzereinstellungen
- `cache\` - Lokaler Cache

---

## 10. Aenderungsprotokoll

### Version 1.0.3 (2026-01-03)

**Neue Controls:**
- **NovviaGrid** (`Controls/NovviaGrid.xaml`): Universelles Grid-Control mit integriertem Datumsfilter, Spaltenauswahl und JTL-Style Styling
- **MonthYearNavigator** (`Controls/Base/MonthYearNavigator.xaml`): Monats-/Jahresnavigation mit Dropdown (Heute, Woche, Monat, Jahr, Alle anzeigen)

**Benutzereinstellungen speichern:**
- MainWindow: Sidebar-Breite wird pro Benutzer gespeichert
- BestellungenView: Status-Filter, Sidebar-Breite, Auftraege/Positionen-Splitter
- KundenView: Alle Splitter-Positionen (Kundenliste, Details, 360-Sicht, Tabs)

**BestellungenView Redesign:**
- Status-Sidebar links mit farbigen Status-Icons
- Automatisches Neuladen bei Filter-Aenderung
- Summen aktualisieren bei Datumsfilter-Aenderung

**KundenView Erweiterungen:**
- Tickets-Tab entfernt
- Auftraege-Tab: Offener Betrag, Zahlungsart, Versandart hinzugefuegt
- 1000 Kunden statt 100 laden

**Dateien geaendert:**
- `+` NovviaGrid.xaml/cs (Neues Universal-Grid)
- `+` MonthYearNavigator.xaml/cs (JTL-Style Datumsnavigation)
- `~` BestellungenView.xaml/cs (Status-Sidebar, Splitter speichern)
- `~` KundenView.xaml/cs (Splitter speichern, Auftragsfelder)
- `~` MainWindow.xaml/cs (Sidebar-Breite speichern)
- `~` CoreService.cs (KundeAuftragKurz erweitert)

### Version 1.0.2 (2026-01-01)

**Neue Features:**
- PayPal-Zahlungslinks erstellen (CreatePayPalPaymentLinkAsync)
- Mollie-Checkout erstellen (CreateMollieCheckoutAsync)
- Zahlungsanbieter-Konfiguration in Einstellungen
- Verbindungstest fuer PayPal/Mollie

**Dateien geaendert:**
- `~` PaymentService.cs (PayPal/Mollie Links)
- `~` EinstellungenView.xaml (Zahlungsanbieter Tab)
- `~` ZahlungsabgleichView.xaml.cs (Sync implementiert)

### Version 1.0.1 (2025-12-31)

**Dokumentation:**
- Ameise Import/Export Dokumentation (Abschnitt 8.6)
- REST API Dokumentation (Abschnitt 8.7)

**Verbesserungen:**
- JTL Datumslogik in Auftraege/Bestellungen

### Version 1.0.0 (2025-12-31)

**Neue Features:**
- Zahlungsintegration (MT940, CAMT, SEPA)
- ZahlungZuordnenDialog fuer manuelle Zuordnung
- Auto-Matching Algorithmus

**Verbesserungen:**
- Chargen-Ausgaenge Tracking
- Lager "Nur mit Bestand" Filter
- JTL Datum-Logik korrigiert

**Dateien geaendert:**
- `+` ZahlungsabgleichService.cs
- `+` SepaService.cs
- `+` ZahlungsabgleichView.xaml
- `+` ZahlungZuordnenDialog.xaml
- `~` MainWindow.xaml (Finanzen-Menu)
- `~` App.xaml.cs (DI Registration)

---

## Anhang

### A. Dateistruktur-Konventionen

- **Views:** `{Name}View.xaml` (UserControl) oder `{Name}Page.xaml` (Page)
- **Dialoge:** `{Name}Dialog.xaml`
- **Services:** `{Bereich}Service.cs`
- **Entities:** Gruppiert in `{Bereich}Entities.cs`

### B. Namenskonventionen

- **Spalten (JTL):** `k{Entity}` (Key), `c{Feld}` (Char), `n{Feld}` (Number), `d{Feld}` (Date), `f{Feld}` (Float)
- **Properties:** PascalCase
- **Private Felder:** `_camelCase`
- **Async-Methoden:** `{Name}Async`

### C. Kontakt

- **Repository:** https://github.com/NOVVIA-it/NOVVIA-ERP
- **Support:** support@novvia.de

---

*Diese Dokumentation wird automatisch aktualisiert. Fuer Aenderungen, bitte nur Abschnitt 10 (Aenderungsprotokoll) und relevante Abschnitte aktualisieren.*

# NOVVIA ERP System

Enterprise Resource Planning System für NOVVIA GmbH - Medizinisches Kosmetikinstitut

## 🏗️ Architektur

```
NovviaERP/
├── NovviaERP.Core/           # Geschäftslogik & Services
│   ├── Data/                 # Datenbankzugriff (JTL-Wawi kompatibel)
│   ├── Entities/             # Datenmodelle
│   └── Services/             # Business Services
├── NovviaERP.WPF/            # Windows Desktop Client
│   ├── Views/                # XAML Views & Pages
│   └── App.xaml              # Anwendungskonfiguration
├── NovviaERP.Workers/        # Hintergrund-Prozesse
└── Scripts/                  # SQL Setup-Skripte
```

## 🔧 Features

### Kern-Module
- **Auftragsverwaltung** - Aufträge, Angebote, Rechnungen
- **Lagerverwaltung** - Bestand, Reservierungen, MHD
- **Kundenverwaltung** - CRM, Kontakte, Historie
- **Artikelstamm** - Produkte, Varianten, Preise

### Erweiterte Features
- **Packtisch+** - Scanner-gestützte Kommissionierung
- **Multi-Carrier Shipping** - DHL, DPD, GLS, UPS Integration
- **Shop-Connector** - WooCommerce, Shopify (novvia.de, oeksline.de)
- **Zahlungsintegration** - Sparkasse HBCI/FinTS, PayPal, Mollie
- **Workflow-Engine** - Automatisierte Prozesse

### Neue Features (Dezember 2024)
- **Auftrags-Import** - CSV/Excel mit flexibler Feldzuordnung (wie VARIO 8)
- **Plattform-Verwaltung** - Shop-Connector in einem Fenster
- **E-Mail-Vorlagen** - Vordefinierte Texte mit Anhängen
- **Angebote → Aufträge** - Ein-Klick Umwandlung
- **Artikelbeschreibung je Plattform** - Text + HTML getrennt
- **Bilder je Plattform** - Unterschiedliche Produktbilder
- **Worker-Steuerung** - GUI für Hintergrund-Jobs

## 💾 Datenbank

- **Server**: MS-SQL Server (192.168.0.220)
- **Datenbanken**: Mandant_1, Mandant_2
- **Kompatibilität**: 100% JTL-Wawi 1.11
- **Eigene Tabellen**: Präfix `NOVVIA.xxxx`

## 🚀 Installation

1. SQL-Skripte ausführen:
   ```sql
   -- Basis-Tabellen
   Scripts/Setup-NovviaTables.sql
   -- Erweiterte Tabellen
   Scripts/Setup-Erweitert.sql
   ```

2. Connection String in `appsettings.json`:
   ```json
   {
     "ConnectionStrings": {
       "JtlDb": "Server=192.168.0.220;Database=Mandant_1;User Id=sa;Password=xxx;TrustServerCertificate=True"
     }
   }
   ```

3. Worker-Dienst starten:
   ```bash
   dotnet run --project NovviaERP.Workers
   ```

4. WPF-Client starten:
   ```bash
   dotnet run --project NovviaERP.WPF
   ```

## 📦 Abhängigkeiten

- .NET 8.0
- Dapper (ORM)
- QuestPDF (Dokumente)
- Serilog (Logging)
- Microsoft.Data.SqlClient

## 📋 Services-Übersicht

| Service | Beschreibung |
|---------|--------------|
| AuftragService | Aufträge, Bestellungen |
| AngebotService | Angebote → Aufträge |
| ArtikelService | Artikelstamm, Varianten |
| KundeService | Kunden, Adressen |
| LagerService | Bestand, Buchungen |
| VersandService | Carrier-Integration |
| ZahlungService | Zahlungsabgleich |
| PlattformService | Shop-Connector |
| EmailVorlageService | E-Mail-Vorlagen |
| AuftragsImportService | CSV/Excel Import |
| AusgabeService | Druck/PDF/Mail |
| WorkflowService | Automatisierung |

## 🔐 Benutzer

- 12 Arbeitsplätze
- Rollen: Admin, Verkauf, Lager, Buchhaltung
- JWT-Authentifizierung

## 📞 Support

NOVVIA GmbH - IT-Abteilung

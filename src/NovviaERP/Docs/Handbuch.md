# NOVVIA ERP V2.0 - Benutzerhandbuch

## Inhaltsverzeichnis
1. [Einführung](#einführung)
2. [Installation](#installation)
3. [Erste Schritte](#erste-schritte)
4. [Module](#module)
5. [Konfiguration](#konfiguration)
6. [API-Dokumentation](#api-dokumentation)
7. [Troubleshooting](#troubleshooting)

---

## 1. Einführung

NOVVIA ERP V2.0 ist eine vollständige Warenwirtschaftslösung, die nahtlos mit JTL-Wawi zusammenarbeitet. Das System nutzt die bestehende JTL-Datenbank und kann parallel zu JTL-Wawi betrieben werden.

### Hauptfunktionen
- **Artikel-Management** mit Stücklisten, Variationen, Attributen
- **Kunden-Verwaltung** mit Zusammenführung und Kundengruppen
- **Bestellabwicklung** mit Scanner-Unterstützung (Packtisch)
- **Multi-Carrier-Versand** (DHL, DPD, GLS, UPS)
- **Zahlungsabgleich** (PayPal, Mollie, Bankimport)
- **WooCommerce-Integration** (novvia.de, novvia-cosmetic.de)
- **Mahnwesen** mit automatischem Mahnlauf
- **PDF-Generierung** (Rechnungen, Lieferscheine, Etiketten)
- **DATEV-Export** für die Buchhaltung

### Systemanforderungen
- Windows 10/11 oder Windows Server 2019/2022
- .NET 8.0 Runtime
- SQL Server (bestehende JTL-Wawi Installation)
- Min. 4 GB RAM, empfohlen 8 GB
- 500 MB Festplattenspeicher

---

## 2. Installation

### Schnellinstallation
```powershell
# Als Administrator ausführen
.\Scripts\Install-NovviaERP.ps1 -InstallDotNet -CreateFirewallRules
```

### Manuelle Installation
1. .NET 8.0 Runtime installieren
2. Verzeichnis erstellen: `C:\NovviaERP`
3. Dateien kopieren
4. `config\appsettings.json` anpassen
5. `NovviaERP.WPF.exe` starten

### Konfiguration (appsettings.json)
```json
{
  "ConnectionStrings": {
    "JTL": "Server=192.168.0.220;Database=Mandant_1;User Id=sa;Password=xxx;TrustServerCertificate=True;"
  }
}
```

---

## 3. Erste Schritte

### Programmstart
Starten Sie `NovviaERP.WPF.exe`. Das Dashboard zeigt sofort die wichtigsten Kennzahlen:
- Bestellungen heute
- Offene Bestellungen
- Zu versenden
- Offene Rechnungen

### Navigation
Die linke Seitenleiste enthält alle Module:
- 📊 Dashboard
- 📦 Bestellungen
- 👥 Kunden
- 🏷️ Artikel
- 📄 Rechnungen
- 🚚 Versand
- 📋 Packtisch
- ...

---

## 4. Module

### 4.1 Dashboard
Das Dashboard zeigt:
- Tagesstatistiken (Bestellungen, Umsatz)
- Offene Vorgänge
- Artikel unter Mindestbestand
- Letzte Bestellungen

### 4.2 Bestellungen
**Neue Bestellung erstellen:**
1. Klicken Sie auf "➕ Neue Bestellung"
2. Wählen Sie einen Kunden aus
3. Scannen oder suchen Sie Artikel
4. Speichern Sie die Bestellung

**Bestellung bearbeiten:**
- Doppelklick auf Bestellung öffnet Details
- Status ändern über Dropdown
- Rechnung/Lieferschein erstellen mit Buttons

### 4.3 Kunden
**Funktionen:**
- Kunden suchen und filtern
- Neue Kunden anlegen
- Kundendaten bearbeiten
- Kunden zusammenführen (bei Duplikaten)

### 4.4 Artikel
**Artikelstamm:**
- Artikelnummer, Barcode, EAN
- Preise (VK Brutto, EK Netto)
- Lagerbestand, Mindestbestand
- Beschreibungen, Bilder

**Stücklisten:**
- Artikel können aus Komponenten bestehen
- Automatische Bestandsführung

### 4.5 Packtisch (Scanner)
Der Packtisch ist für das Kommissionieren optimiert:

1. **Bestellung wählen:** Doppelklick auf offene Bestellung
2. **Artikel scannen:** Barcode scannen, System zeigt Soll/Ist
3. **Abschließen:** Wenn alle Artikel gescannt → "Abschließen & Versenden"

**Tastaturkürzel:**
- `Enter` nach Scan: Artikel hinzufügen
- `F5`: Aktualisieren

### 4.6 Versand
**Unterstützte Carrier:**
- DHL (Paket, Warenpost)
- DPD (Classic, Express)
- GLS (Standard)
- UPS (Standard, Express)

**Label erstellen:**
1. Bestellung auswählen
2. Carrier-Button klicken
3. Label wird automatisch erstellt und gespeichert

### 4.7 Rechnungen
- Übersicht aller Rechnungen
- Filter nach Status (Offen, Bezahlt, Überfällig)
- PDF-Erstellung mit einem Klick
- Zahlungseingang buchen

### 4.8 Mahnwesen
**Automatischer Mahnlauf:**
- System erkennt überfällige Rechnungen
- Mahnstufen konfigurierbar
- PDF-Mahnungen generieren

### 4.9 Einkauf
- Lieferanten verwalten
- Bestellvorschläge (unter Mindestbestand)
- Einkaufsbestellungen erstellen
- Wareneingang buchen

### 4.10 WooCommerce
**Synchronisation:**
- Artikel → WooCommerce (Preise, Bestand, Bilder)
- Bestellungen ← WooCommerce (automatischer Import)
- Kategorien bidirektional

**Konfiguration:**
```json
"WooCommerce": {
  "Shops": [{
    "Name": "novvia.de",
    "Url": "https://novvia.de",
    "ConsumerKey": "ck_xxx",
    "ConsumerSecret": "cs_xxx"
  }]
}
```

---

## 5. Konfiguration

### Versand-Credentials
```json
"Shipping": {
  "DHL": {
    "User": "app_id",
    "Password": "token",
    "BillingNumber": "22222222220101"
  }
}
```

### Zahlungsanbieter
```json
"Payment": {
  "PayPalClientId": "xxx",
  "PayPalSecret": "xxx",
  "MollieApiKey": "live_xxx"
}
```

### E-Mail (für Workflows)
```json
"Email": {
  "SmtpHost": "smtp.office365.com",
  "SmtpPort": 587,
  "Username": "info@novvia.de",
  "Password": "xxx"
}
```

---

## 6. API-Dokumentation

Die REST-API läuft auf Port 5000/5001.

### Endpoints

**Artikel:**
```
GET    /api/artikel              - Alle Artikel
GET    /api/artikel/{id}         - Artikel by ID
GET    /api/artikel/barcode/{bc} - Artikel by Barcode
POST   /api/artikel              - Neuer Artikel
PUT    /api/artikel/{id}         - Artikel aktualisieren
PATCH  /api/artikel/{id}/bestand - Bestand ändern
```

**Bestellungen:**
```
GET    /api/bestellungen         - Alle Bestellungen
GET    /api/bestellungen/{id}    - Bestellung by ID
POST   /api/bestellungen         - Neue Bestellung
PATCH  /api/bestellungen/{id}/status - Status ändern
POST   /api/bestellungen/{id}/rechnung - Rechnung erstellen
```

**Kunden:**
```
GET    /api/kunden               - Alle Kunden
GET    /api/kunden/{id}          - Kunde by ID
POST   /api/kunden               - Neuer Kunde
PUT    /api/kunden/{id}          - Kunde aktualisieren
```

**Dashboard:**
```
GET    /api/dashboard/stats      - Statistiken
```

### Authentifizierung
Die API verwendet JWT-Bearer-Tokens:
```
Authorization: Bearer <token>
```

---

## 7. Troubleshooting

### Verbindungsprobleme SQL Server
- Prüfen Sie die Connection-String in appsettings.json
- SQL Server Browser-Dienst muss laufen
- Firewall Port 1433 freigeben

### WooCommerce-Sync funktioniert nicht
- API-Credentials prüfen (Consumer Key/Secret)
- WooCommerce REST API aktiviert?
- SSL-Zertifikat gültig?

### Versand-Labels werden nicht erstellt
- Carrier-Credentials prüfen
- Testmodus vs. Produktivmodus
- Absenderadresse vollständig?

### Logs
Logs befinden sich in: `C:\NovviaERP\logs\`
- `novvia-erp-YYYYMMDD.log` - Tägliche Logfiles

---

## Support

Bei Fragen wenden Sie sich an:
- E-Mail: support@novvia.de
- Tel: [Telefonnummer]

---

*NOVVIA ERP V2.0 - © 2024 NOVVIA GmbH*

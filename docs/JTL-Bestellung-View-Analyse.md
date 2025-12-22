# JTL Bestellung View - Analyse und Struktur

## Übersicht

Diese Dokumentation enthält die Analyse der JTL-Datenbankstruktur für die Erstellung einer Bestellungs-View mit MSV3-Daten, MSVE-Bestand und MHD.

---

## 1. Relevante JTL-Tabellen

### tBestellung (Bestellungen/Aufträge)
| Spalte | Typ | Beschreibung |
|--------|-----|--------------|
| kBestellung | int | Primary Key |
| cBestellNr | nvarchar | Bestellnummer |
| cInetBestellNr | nvarchar | Internet-Bestellnummer |
| tKunde_kKunde | int | FK zu tKunde |
| cWaehrung | nvarchar | Währung (EUR) |
| tVersandArt_kVersandArt | int | FK Versandart |
| kZahlungsart | int | FK Zahlungsart |
| dLieferdatum | datetime | Lieferdatum |
| dVersandt | datetime | Versanddatum |
| dBezahlt | datetime | Bezahldatum |
| nStatus | int | Status (0=Offen, etc.) |
| cAnmerkung | nvarchar | Anmerkung |

### tBestellPos (Bestellpositionen)
| Spalte | Typ | Beschreibung |
|--------|-----|--------------|
| kBestellPos | int | Primary Key |
| tBestellung_kBestellung | int | FK zu tBestellung |
| tArtikel_kArtikel | int | FK zu tArtikel |
| cArtNr | nvarchar | Artikelnummer |
| cName | nvarchar | Artikelname |
| fAnzahl | decimal | Menge |
| fVKNetto | decimal | VK Netto |
| fVKBrutto | decimal | VK Brutto |
| fRabatt | decimal | Rabatt % |
| fMwSt | decimal | MwSt % |
| nPosTyp | int | Positionstyp |
| cEinheit | nvarchar | Einheit |

### tArtikel (Artikel)
| Spalte | Typ | Beschreibung |
|--------|-----|--------------|
| kArtikel | int | Primary Key |
| cArtNr | nvarchar | Artikelnummer |
| cName | nvarchar | Artikelname |
| cBarcode | nvarchar | EAN/Barcode |
| fLagerbestand | decimal | Lagerbestand |
| fMindestbestand | decimal | Mindestbestand |
| kHersteller | int | FK Hersteller |

---

## 2. NOVVIA MSV3-Tabellen

### NOVVIA.MSV3Lieferant
| Spalte | Typ | Beschreibung |
|--------|-----|--------------|
| kMSV3Lieferant | int | Primary Key |
| kLieferant | int | FK zu tLieferer |
| cMSV3Url | nvarchar | MSV3 Endpoint URL |
| cMSV3Benutzer | nvarchar | Benutzername |
| cMSV3Passwort | nvarchar | Passwort (verschlüsselt) |
| cMSV3Kundennummer | nvarchar | Kundennummer beim Lieferanten |
| cMSV3Filiale | nvarchar | Filiale |
| nMSV3Version | int | 1 oder 2 |
| nPrioritaet | int | Priorität |
| nAktiv | bit | Aktiv |

### NOVVIA.MSV3Bestellung
| Spalte | Typ | Beschreibung |
|--------|-----|--------------|
| kMSV3Bestellung | int | Primary Key |
| kMSV3Lieferant | int | FK zu MSV3Lieferant |
| cAuftragsId | nvarchar | Auftrags-ID vom Lieferanten |
| cStatus | nvarchar | OFFEN, BESTAETIGT, GELIEFERT, STORNO, GESENDET, FEHLER |
| dErstellt | datetime | Erstelldatum |
| dGesendet | datetime | Sendedatum |
| cResponseXml | nvarchar(max) | Antwort-XML |
| nAnzahlVerfuegbar | int | Anzahl verfügbar |
| nAnzahlNichtVerfuegbar | int | Anzahl nicht verfügbar |

### NOVVIA.MSV3BestellungPos
| Spalte | Typ | Beschreibung |
|--------|-----|--------------|
| kMSV3BestellungPos | int | Primary Key |
| kMSV3Bestellung | int | FK zu MSV3Bestellung |
| cPZN | nvarchar(10) | Pharmazentralnummer |
| cArtNr | nvarchar | Artikelnummer |
| cStatus | nvarchar | VERFUEGBAR, TEILWEISE, NICHT_VERFUEGBAR |
| fMengeBestellt | decimal | Bestellte Menge |
| fMengeVerfuegbar | decimal | Verfügbare Menge |
| fMengeGeliefert | decimal | Gelieferte Menge |
| fPreisEK | decimal | Einkaufspreis |
| fPreisAEP | decimal | Apothekeneinkaufspreis |
| fPreisAVP | decimal | Apothekenverkaufspreis |
| dMHD | date | Mindesthaltbarkeitsdatum |
| cChargenNr | nvarchar(50) | Chargennummer |

---

## 3. ABdata Pharma-Stammdaten

### NOVVIA.ABdataArtikel
| Spalte | Typ | Beschreibung |
|--------|-----|--------------|
| kABdataArtikel | int | Primary Key |
| cPZN | nvarchar(10) | Pharmazentralnummer (unique) |
| cName | nvarchar | Artikelname |
| cHersteller | nvarchar | Hersteller |
| cDarreichungsform | nvarchar | Darreichungsform |
| cPackungsgroesse | nvarchar | Packungsgröße |
| fMenge | decimal | Menge |
| cEinheit | nvarchar | Einheit |
| fAEP | decimal | Apothekeneinkaufspreis |
| fAVP | decimal | Apothekenverkaufspreis |
| nRezeptpflicht | bit | 0=OTC, 1=Rx |
| nBTM | bit | Betäubungsmittel |
| nKuehlpflichtig | bit | Kühlpflichtig |
| cATC | nvarchar | ATC-Code |
| cWirkstoff | nvarchar | Wirkstoff |
| nAktiv | bit | Aktiv |

### NOVVIA.ABdataArtikelMapping
| Spalte | Typ | Beschreibung |
|--------|-----|--------------|
| kMapping | int | Primary Key |
| kArtikel | int | FK zu tArtikel |
| cPZN | nvarchar(10) | PZN |
| nAutomatisch | bit | Automatisch gemappt |

---

## 4. MHD und Chargen-Unterstützung

### Artikel-Ebene
- `nMHD` (bool) - Artikel hat MHD-Tracking
- `nCharge` (bool) - Artikel hat Chargen-Tracking

### Positions-Ebene (in allen relevanten Tabellen)
- `dMHD` (datetime) - Mindesthaltbarkeitsdatum
- `cChargenNr` (nvarchar) - Chargennummer

---

## 5. MSVE (Elektronischer Lieferschein)

MSVE ist das elektronische Lieferschein-Format für Pharma-Großhandel.
Enthält:
- Lieferanten-Informationen
- Bestellpositionen mit Mengen
- **Bestandsinformationen** pro Position
- **MHD** pro Charge
- **Chargennummern**

---

## 6. Geplante View: vNOVVIA_BestellungMSV3

Die View soll folgende Daten zusammenführen:

1. **Bestellkopf** (tBestellung)
2. **Bestellpositionen** (tBestellPos)
3. **Artikel-Stammdaten** (tArtikel)
4. **MSV3-Bestellstatus** (NOVVIA.MSV3Bestellung)
5. **MSV3-Positionsstatus** (NOVVIA.MSV3BestellungPos)
6. **ABdata-Pharma-Daten** (NOVVIA.ABdataArtikel via PZN-Mapping)
7. **MSVE-Bestand** (verfügbare Menge vom Lieferanten)
8. **MHD** (Mindesthaltbarkeitsdatum)
9. **Chargennummer**

---

## 7. Status-Werte

### Bestellstatus (nStatus in tBestellung)
| Wert | Bedeutung |
|------|-----------|
| 0 | Offen |
| 1 | In Bearbeitung |
| 2 | Versendet |
| 3 | Abgeschlossen |
| 4 | Storniert |

### MSV3 Status
| Wert | Bedeutung |
|------|-----------|
| OFFEN | Noch nicht gesendet |
| GESENDET | An Lieferant gesendet |
| BESTAETIGT | Vom Lieferant bestätigt |
| GELIEFERT | Ware geliefert |
| STORNO | Storniert |
| FEHLER | Fehler bei Übertragung |

### MSV3 Positions-Status
| Wert | Bedeutung |
|------|-----------|
| VERFUEGBAR | Vollständig verfügbar |
| TEILWEISE | Teilweise verfügbar |
| NICHT_VERFUEGBAR | Nicht verfügbar |

---

## Erstellte Dateien

### 1. SQL-View Script
**Datei:** `src/NovviaERP/Scripts/View-BestellungMSV3.sql`

Enthält 3 Views:
- `NOVVIA.vNOVVIA_BestellungMSV3` - Detail-View mit allen Positionen
- `NOVVIA.vNOVVIA_BestellungMSV3Kopf` - Aggregierte Kopfdaten
- `NOVVIA.vNOVVIA_MSVEBestandMHD` - MSVE Bestand + MHD fokussiert

### 2. Entity-Klassen
**Datei:** `src/NovviaERP/NovviaERP.Core/Entities/BestellungMSV3ViewEntities.cs`

Klassen:
- `BestellungMSV3Detail` - Detail-View Entity
- `BestellungMSV3Kopf` - Kopf-View Entity
- `MSVEBestandMHD` - MSVE Bestand Entity
- Enums: `MHDKategorie`, `MSV3PosStatus`, `LieferantenBestellStatus`

### 3. Service-Methoden
**Datei:** `src/NovviaERP/NovviaERP.Core/Services/MSV3Service.cs`

Neue Methoden:
- `GetBestellungenMSV3DetailAsync()` - Alle Bestellungen mit MSV3-Daten
- `GetBestellungenMSV3KopfAsync()` - Kopfdaten mit Summary
- `GetBestellungMSV3DetailAsync(id)` - Einzelne Bestellung
- `GetMSVEBestandMHDAsync()` - MSVE Bestand + MHD
- `GetArtikelMSVEBestandAsync(id)` - Einzelner Artikel
- `GetArtikelMSVEBestandByPZNAsync(pzn)` - Artikel via PZN
- `GetArtikelMitKritischemMHDAsync()` - Kritische MHD-Artikel
- `GetMHDStatistikAsync()` - Dashboard-Statistik

---

## Verwendung

### SQL-View auf Datenbank ausführen
```sql
-- Auf gewünschtem Mandant ausführen
USE Mandant_3;
GO
-- Script ausführen: View-BestellungMSV3.sql
```

### C# Service-Aufruf
```csharp
var msv3Service = new MSV3Service(connectionString);

// Alle Bestellungen mit MSV3-Daten
var bestellungen = await msv3Service.GetBestellungenMSV3DetailAsync();

// Nur offene Bestellungen eines Lieferanten
var offene = await msv3Service.GetBestellungenMSV3DetailAsync(
    kLieferant: 123,
    nStatus: 0);

// MSVE Bestand + MHD für alle Pharma-Artikel
var bestand = await msv3Service.GetMSVEBestandMHDAsync();

// Nur kritische MHD-Artikel
var kritisch = await msv3Service.GetArtikelMitKritischemMHDAsync();

// Dashboard-Statistik
var (abgelaufen, kurz, mittel) = await msv3Service.GetMHDStatistikAsync();
```

---

## Nächste Schritte (optional)

1. WPF-View für Anzeige erstellen
2. Dashboard-Widget für MHD-Warnung
3. Export-Funktion für MSVE-Bestandsliste

# JTL-Wawi 1.11 - Bekannte Tabellenstruktur

## Aus Suche und Dokumentation ermittelt:

### tKunde
```
kKunde              - INT (PK)
cKundenNr           - Kundennummer
cAnrede             - Anrede
cTitel              - Titel
cVorname            - Vorname
cNachname           - Nachname
cFirma              - Firmenname
cZusatz             - Zusatz
cStrasse            - Straße
cHausnummer         - Hausnummer (neu in 1.11?)
cAdressZusatz       - Adresszusatz
cPLZ                - PLZ
cOrt                - Ort
cBundesland         - Bundesland
cLand               - Land
cTel                - Telefon
cFax                - Fax
cMobil              - Mobilnummer
cMail               - E-Mail
cWWW                - Website
cUSTID              - USt-ID
cGeburtstag         - Geburtstag
cHerkunft           - Herkunft (Shop, Telefon, etc.)
kKundengruppe       - FK Kundengruppe
```

### tBestellung
```
kBestellung         - INT (PK)
cBestellNr          - Bestellnummer
tKunde_kKunde       - FK Kunde (ACHTUNG: nicht kKunde!)
dErstellt           - Erstelldatum
nStatus             - Status
cHerkunft           - Herkunft
```

### tBestellungEckDaten
```
kBestellung         - FK
fWert               - Bestellwert
fGutschrift         - Gutschriften
fGutschein          - Gutscheine
```

### tRechnung
```
kRechnung           - INT (PK)
cRechnungsNr        - Rechnungsnummer
tBestellung_kBestellung - FK Bestellung
dRechnungsDatum     - Datum
```

### tArtikel
```
kArtikel            - INT (PK)
cArtNr              - Artikelnummer
cName               - Artikelname
cBeschreibung       - Beschreibung
fVKBrutto           - VK Brutto
fVKNetto            - VK Netto
fLagerbestand       - Lagerbestand
```

---

## Was ich brauche von dir:

Bitte führe diese Abfrage aus und teile das Ergebnis:

```sql
-- Alle Spalten der wichtigsten Tabellen
SELECT 
    t.name AS Tabelle,
    c.name AS Spalte,
    ty.name AS Datentyp,
    c.max_length AS MaxLaenge,
    c.is_nullable AS Nullable
FROM sys.tables t
INNER JOIN sys.columns c ON t.object_id = c.object_id
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE t.name IN (
    'tKunde', 'tArtikel', 'tBestellung', 'tBestellPos',
    'tAngebot', 'tAngebotPos', 'tRechnung', 'tLieferschein',
    'tPlattform', 'tverkaufskanal', 'tShop',
    'tArtikelBeschreibung', 'tArtikelBild',
    'tEmailVorlage', 'tBenutzer'
)
ORDER BY t.name, c.column_id;
```

Oder einfach Screenshot von wawi-db.jtl-software.de nach Login!

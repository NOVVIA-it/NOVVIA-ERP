# NOVVIA ERP - Datenbank Setup

## Voraussetzungen

- SQL Server 2016 oder neuer
- JTL-Wawi Mandanten-Datenbank bereits eingerichtet
- SQL Server Management Studio (SSMS) oder sqlcmd

## Installation

### Schnellstart

1. **Mandant-Datenbank auswahlen** (z.B. `Mandant_1`)
2. **INSTALL-NOVVIA-Complete.sql** ausfuhren
3. Fertig!

### Via SSMS

```sql
USE Mandant_1;  -- oder Mandant_2, Mandant_3, etc.
GO

-- Script ausfuhren
:r "C:\NovviaERP\src\NovviaERP\Scripts\INSTALL-NOVVIA-Complete.sql"
```

### Via sqlcmd

```cmd
sqlcmd -S SERVERNAME -d Mandant_1 -i "INSTALL-NOVVIA-Complete.sql"
```

## Enthaltene Objekte

### Schema
- `NOVVIA` - Eigenes Schema fuer alle NOVVIA-Objekte

### Tabellen

| Bereich | Tabellen |
|---------|----------|
| Auth | tRolle, tBerechtigung, tRolleBerechtigung, tBenutzerLog |
| Basis | NOVVIA.tImportVorlage, NOVVIA.tImportLog, NOVVIA.tWorkerStatus, NOVVIA.tWorkerLog, NOVVIA.tAusgabeLog, NOVVIA.tDokumentArchiv |
| MSV3 | NOVVIA.MSV3Lieferant, NOVVIA.MSV3Bestellung, NOVVIA.MSV3BestellungPos, NOVVIA.MSV3VerfuegbarkeitCache, NOVVIA.MSV3Log, NOVVIA.MSV3BestandCache |
| ABdata | NOVVIA.ABdataArtikel, NOVVIA.ABdataArtikelMapping |
| Einkauf | NOVVIA.tEinkaufsliste, NOVVIA.tLieferantErweitert |

### Views

- `NOVVIA.vLieferantenUebersicht` - Lieferanten mit MSV3-Status
- `NOVVIA.vMSV3Bestellungen` - MSV3-Bestellungen Ubersicht

### Stored Procedures

| SP | Beschreibung |
|----|--------------|
| NOVVIA.spMSV3LieferantSpeichern | MSV3-Konfiguration speichern |
| NOVVIA.spArtikelEigenesFeldSetzen | JTL-Eigene-Felder setzen |
| NOVVIA.spWorkerLogSchreiben | Worker-Log schreiben |
| NOVVIA.spWorkerStatusAktualisieren | Worker-Status aktualisieren |
| NOVVIA.spMSV3BestandCache_Get | Bestand-Cache abfragen (TTL) |
| NOVVIA.spMSV3BestandCache_Upsert | Bestand-Cache aktualisieren |
| NOVVIA.spMSV3BestandCache_Cleanup | Alte Cache-Eintraege loeschen |
| NOVVIA.spArtikelEigenesFeldCreateOrUpdate | Bulk-Update Eigene Felder |
| NOVVIA.spABdataArtikelUpsert | ABdata-Artikel importieren |
| NOVVIA.spABdataAutoMapping | Auto-Mapping PZN zu Artikel |

### Types (TVP)

- `NOVVIA.TYPE_ArtikelEigenesFeldAnpassen` - Bulk-Update fuer Eigene Felder

## Einzelne Scripts

Falls nur bestimmte Komponenten benoetigt werden:

| Script | Beschreibung |
|--------|--------------|
| Setup-Auth-Tables.sql | Nur Berechtigungssystem |
| Setup-NovviaTables.sql | Basis-Tabellen |
| Setup-MSV3-*.sql | MSV3-Komponenten |
| Setup-Einkauf-*.sql | Einkauf-Komponenten |
| SP-*.sql | Einzelne Stored Procedures |

## Mandanten

NOVVIA unterstuetzt mehrere JTL-Mandanten:

| Mandant | Datenbank | Beschreibung |
|---------|-----------|--------------|
| 1 | Mandant_1 | NOVVIA Hauptmandant |
| 2 | Mandant_2 | NOVVIA_PHARM (Pharma) |
| 3 | Mandant_3 | Entwicklung/Test |
| 5 | Mandant_5 | Test |

Das Script muss fuer jeden Mandanten separat ausgefuhrt werden.

## Troubleshooting

### Fehler: Schema NOVVIA existiert nicht

```sql
CREATE SCHEMA NOVVIA;
```

### Fehler: Berechtigung verweigert

Stellen Sie sicher, dass der SQL-Benutzer `db_owner` Rechte hat:
```sql
ALTER ROLE db_owner ADD MEMBER [IhrBenutzer];
```

### Fehler: Objekt existiert bereits

Das Script verwendet `IF NOT EXISTS` Pruefungen. Bei Problemen:
```sql
-- Vor dem erneuten Ausfuhren:
DROP SCHEMA NOVVIA CASCADE;  -- VORSICHT: Loescht alle NOVVIA-Objekte!
```

## Nach der Installation

1. **Admin-Benutzer anlegen** (in NovviaERP.WPF)
2. **MSV3-Lieferanten konfigurieren** (falls Pharma-Grosshandel)
3. **ABdata-Stammdaten importieren** (falls Pharma)
4. **Worker-Dienst starten** (NovviaERP.Worker)

## Version

- Script-Version: 2.0
- Datum: 2024-12-28
- Kompatibel mit: JTL-Wawi 1.5+, SQL Server 2016+

# JTL-Wawi Tabellen-Referenz

## Fragen an Peter:

Kannst du mir die Tabellenstruktur aus JTL-Wawi exportieren? 

### Option 1: SQL-Abfrage ausführen

```sql
-- Alle Tabellen mit Spalten auflisten
SELECT 
    t.TABLE_NAME,
    c.COLUMN_NAME,
    c.DATA_TYPE,
    c.CHARACTER_MAXIMUM_LENGTH,
    c.IS_NULLABLE
FROM INFORMATION_SCHEMA.TABLES t
INNER JOIN INFORMATION_SCHEMA.COLUMNS c ON t.TABLE_NAME = c.TABLE_NAME
WHERE t.TABLE_TYPE = 'BASE TABLE'
  AND t.TABLE_NAME LIKE 't%'
ORDER BY t.TABLE_NAME, c.ORDINAL_POSITION;
```

### Option 2: Wichtigste Tabellen einzeln

```sql
-- Kunden
SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'tKunde';

-- Artikel
SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'tArtikel';

-- Bestellungen
SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'tBestellung';

-- Angebote
SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'tAngebot';

-- Plattformen
SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'tPlattform';

-- E-Mail Vorlagen
SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'tEmailVorlage';

-- Benutzer
SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'tBenutzer';
```

### Option 3: Ergebnis als CSV exportieren

In SSMS: Abfrage ausführen → Rechtsklick auf Ergebnis → "Ergebnisse speichern unter..." → CSV

---

Sobald ich die echte Struktur habe, passe ich alle Services an die korrekten JTL-Spaltennamen an!

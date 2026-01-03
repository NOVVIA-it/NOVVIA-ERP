# NOVVIA Stored Procedures - Rechnung & Rechnungskorrektur

**Erstellt:** 2026-01-03
**Version:** 1.0
**Autor:** NovviaERP

---

## Ueberblick

Diese Dokumentation beschreibt die NOVVIA Stored Procedures fuer die Verwaltung von Rechnungen und Rechnungskorrekturen (Gutschriften).

**WICHTIG:** Die SPs nutzen JTL-Basistabellen (NICHT Views!) fuer maximale Stabilitaet bei JTL-Updates.

### Verwendete JTL-Basistabellen

**Rechnung:**
- `Rechnung.tRechnung` - Haupttabelle
- `Rechnung.tRechnungEckdaten` - Betraege, Mahnstufe, Zahlungsstatus
- `Rechnung.tRechnungAdresse` - Rechnungs-/Lieferadresse (nTyp: 0=Rechnung, 1=Liefer)
- `Rechnung.tRechnungPosition` - Positionen
- `Rechnung.tRechnungStorno` - Storno-Info
- `Rechnung.tRechnungStornogrund` - Stornogruende

**Rechnungskorrektur/Gutschrift:**
- `dbo.tgutschrift` - Haupttabelle
- `dbo.tGutschriftPos` - Positionen
- `dbo.tGutschriftStorno` - Storno-Info
- `dbo.tGutschriftStornogrund` - Stornogruende

**Mahnstufen:**
- `dbo.tMahnstufe` - Dynamische Mahnstufen (via NOVVIA.spMahnstufen)

### Aufgerufene JTL SPs (nur fuer Storno-Operationen!)
- `Rechnung.spRechnungenStornieren` - Rechnung stornieren (komplexe JTL-Logik)
- `dbo.spGutschriftenStornieren` - Gutschrift stornieren (komplexe JTL-Logik)

---

## Terminologie

| Begriff | JTL intern | JTL Oberflaeche | NOVVIA |
|---------|------------|-----------------|--------|
| Gutschrift | tGutschrift | Rechnungskorrektur | Rechnungskorrektur |
| Stornobeleg | tGutschrift (nStornoTyp=1) | Stornorechnung | Stornobeleg |

### StornoTyp-Werte
- `0` = Normale Rechnungskorrektur (manuell erstellt)
- `1` = Stornobeleg einer Rechnung (automatisch bei Rechnungs-Storno)
- `2` = Stornobeleg einer Rechnungskorrektur (Gegen-Gutschrift)

---

## Stored Procedures

### 1. NOVVIA.spRechnungLesen

**Zweck:** Liest eine einzelne Rechnung mit allen Details und Positionen.

**Signatur:**
```sql
EXEC NOVVIA.spRechnungLesen @kRechnung = <INT>
```

**Parameter:**
| Parameter | Typ | Pflicht | Beschreibung |
|-----------|-----|---------|--------------|
| @kRechnung | INT | Ja | Primaerschluessel der Rechnung |

**Rueckgabe:**
- **Resultset 1:** Rechnungskopf (1 Zeile) aus Rechnung.tRechnung + tRechnungEckdaten + tRechnungAdresse
- **Resultset 2:** Rechnungspositionen (n Zeilen) aus Rechnung.tRechnungPosition

**Beispiel:**
```sql
EXEC NOVVIA.spRechnungLesen @kRechnung = 12345
```

---

### 2. NOVVIA.spRechnungenAuflisten

**Zweck:** Listet Rechnungen mit diversen Filtermoeglichkeiten.

**Signatur:**
```sql
EXEC NOVVIA.spRechnungenAuflisten
    @cSuche = NULL,
    @nStatus = NULL,
    @kKunde = NULL,
    @kPlattform = NULL,
    @dVon = NULL,
    @dBis = NULL,
    @nNurOffene = 0,
    @nNurStornierte = 0,
    @nNurAngemahnt = 0,
    @nLimit = 1000
```

**Parameter:**
| Parameter | Typ | Standard | Beschreibung |
|-----------|-----|----------|--------------|
| @cSuche | NVARCHAR(100) | NULL | Suche in Rechnungsnummer, Kundennummer, Kundenname, Auftragsnummer |
| @nStatus | INT | NULL | Statusfilter (siehe unten) |
| @nMahnstufe | INT | NULL | Filter nach Mahnstufe (siehe unten) |
| @kKunde | INT | NULL | Filter nach Kunde |
| @kPlattform | INT | NULL | Filter nach Plattform |
| @dVon | DATETIME | NULL | Erstellt ab Datum |
| @dBis | DATETIME | NULL | Erstellt bis Datum |
| @nNurOffene | BIT | 0 | Nur Rechnungen mit fOffenerWert > 0 |
| @nNurStornierte | BIT | 0 | Nur stornierte Rechnungen |
| @nNurAngemahnt | BIT | 0 | Nur angemahnte Rechnungen |
| @nLimit | INT | 1000 | Maximale Anzahl Ergebnisse |

**Status-Werte (@nStatus):**
| Wert | Bedeutung |
|------|-----------|
| NULL | Alle Rechnungen |
| 0 | Offen (nicht bezahlt, nicht storniert) |
| 1 | Bezahlt (komplett) |
| 2 | Storniert |
| 3 | Teilbezahlt (Betrag eingegangen, aber offener Rest) |
| 4 | Angemahnt |

**Mahnstufen (@nMahnstufe):** *(dynamisch aus NOVVIA.spMahnstufen)*

Die Mahnstufen werden dynamisch aus der JTL-Tabelle `dbo.tMahnstufe` gelesen. Nutze `NOVVIA.spMahnstufen` um die aktuellen Werte abzurufen:

```sql
EXEC NOVVIA.spMahnstufen
```

**Typische Rueckgabe:**
| nStufe | cName |
|--------|-------|
| 0 | Keine Mahnung |
| 1 | Zahlungserinnerung |
| 2 | 1. Mahnung |
| 3 | 2. Mahnung |
| 4 | 3. Mahnung |
| 5 | Inkasso/RA |

**WICHTIG:** Die Werte koennen pro Mandant unterschiedlich sein! Niemals hartcodieren, immer `spMahnstufen` verwenden.

**Beispiele:**
```sql
-- Alle offenen Rechnungen eines Kunden
EXEC NOVVIA.spRechnungenAuflisten @kKunde = 123, @nStatus = 0

-- Rechnungen im Dezember 2025
EXEC NOVVIA.spRechnungenAuflisten @dVon = '2025-12-01', @dBis = '2025-12-31'

-- Suche nach Rechnungsnummer
EXEC NOVVIA.spRechnungenAuflisten @cSuche = 'RE-2025'
```

---

### 3. NOVVIA.spRechnungStornieren

**Zweck:** Storniert eine Rechnung und erstellt automatisch einen Stornobeleg.

**Art:** NOVVIA-Wrapper um `Rechnung.spRechnungenStornieren`

**Signatur:**
```sql
DECLARE @kGutschrift INT;
EXEC NOVVIA.spRechnungStornieren
    @kRechnung = <INT>,
    @kBenutzer = <INT>,
    @kRechnungStornogrund = -1,
    @cKommentar = NULL,
    @dStorniert = NULL,
    @nZahlungenZusammenfassen = 1,
    @kGutschrift = @kGutschrift OUTPUT
```

**Parameter:**
| Parameter | Typ | Standard | Beschreibung |
|-----------|-----|----------|--------------|
| @kRechnung | INT | - | Primaerschluessel der Rechnung (Pflicht) |
| @kBenutzer | INT | - | Benutzer-ID (Pflicht) |
| @kRechnungStornogrund | INT | -1 | Stornogrund |
| @cKommentar | NVARCHAR(100) | NULL | Optionaler Kommentar |
| @dStorniert | DATETIME | GETDATE() | Stornodatum |
| @nZahlungenZusammenfassen | BIT | 1 | Zahlungen auf Auftrag umbuchen |
| @kGutschrift | INT OUTPUT | - | ID des erstellten Stornobelegs |

**Stornogruende (@kRechnungStornogrund):**
| Wert | Bedeutung |
|------|-----------|
| -5 | Steuern falsch |
| -4 | Preise falsch |
| -3 | Positionen falsch |
| -2 | Adresse falsch |
| -1 | Sonstiges (Standard) |
| 0 | Kein Grund angegeben |

**Rueckgabe:**
- OUTPUT-Parameter: @kGutschrift mit ID des Stornobelegs
- Resultset: kRechnung, cRechnungsnr, nError, kGutschrift

**Beispiel:**
```sql
DECLARE @kGs INT;
EXEC NOVVIA.spRechnungStornieren
    @kRechnung = 12345,
    @kBenutzer = 1,
    @kRechnungStornogrund = -1,
    @cKommentar = 'Kunde hat Bestellung storniert',
    @kGutschrift = @kGs OUTPUT;

SELECT @kGs AS ErstellteGutschriftID;
```

**Logging:** Schreibt automatisch in NOVVIA.Log via `spLogSchreiben`

---

### 4. NOVVIA.spRechnungskorrekturLesen

**Zweck:** Liest eine einzelne Rechnungskorrektur mit allen Details und Positionen.

**Signatur:**
```sql
EXEC NOVVIA.spRechnungskorrekturLesen @kGutschrift = <INT>
```

**Parameter:**
| Parameter | Typ | Pflicht | Beschreibung |
|-----------|-----|---------|--------------|
| @kGutschrift | INT | Ja | Primaerschluessel der Rechnungskorrektur |

**Rueckgabe:**
- **Resultset 1:** Rechnungskorrektur-Kopf (1 Zeile)
- **Resultset 2:** Korrektur-Positionen (n Zeilen)

---

### 5. NOVVIA.spRechnungskorrekturenAuflisten

**Zweck:** Listet Rechnungskorrekturen mit Filtermoeglichkeiten.

**Signatur:**
```sql
EXEC NOVVIA.spRechnungskorrekturenAuflisten
    @cSuche = NULL,
    @nNurStornierte = NULL,
    @kKunde = NULL,
    @kPlattform = NULL,
    @dVon = NULL,
    @dBis = NULL,
    @nNurStornobelege = 0,
    @nLimit = 1000
```

**Parameter:**
| Parameter | Typ | Standard | Beschreibung |
|-----------|-----|----------|--------------|
| @cSuche | NVARCHAR(100) | NULL | Suche in Korrekturnummer, Rechnungsnummer, Kundennr |
| @nNurStornierte | BIT | NULL | NULL=Alle, 0=Nicht storniert, 1=Nur storniert |
| @kKunde | INT | NULL | Filter nach Kunde |
| @kPlattform | INT | NULL | Filter nach Plattform |
| @dVon | DATETIME | NULL | Erstellt ab Datum |
| @dBis | DATETIME | NULL | Erstellt bis Datum |
| @nNurStornobelege | BIT | 0 | Nur Stornobelege (nStornoTyp = 1) |
| @nLimit | INT | 1000 | Maximale Anzahl Ergebnisse |

**Beispiele:**
```sql
-- Alle Rechnungskorrekturen eines Kunden
EXEC NOVVIA.spRechnungskorrekturenAuflisten @kKunde = 123

-- Nur Stornobelege (von stornierten Rechnungen)
EXEC NOVVIA.spRechnungskorrekturenAuflisten @nNurStornobelege = 1
```

---

### 6. NOVVIA.spRechnungskorrekturStornieren

**Zweck:** Storniert eine Rechnungskorrektur und erstellt einen Gegen-Stornobeleg.

**Art:** NOVVIA-Wrapper um `dbo.spGutschriftenStornieren`

**Signatur:**
```sql
DECLARE @kStornoGutschrift INT;
EXEC NOVVIA.spRechnungskorrekturStornieren
    @kGutschrift = <INT>,
    @kBenutzer = <INT>,
    @kGutschriftStornogrund = -1,
    @cKommentar = NULL,
    @dStorniert = NULL,
    @kStornoGutschrift = @kStornoGutschrift OUTPUT
```

**Parameter:**
| Parameter | Typ | Standard | Beschreibung |
|-----------|-----|----------|--------------|
| @kGutschrift | INT | - | Primaerschluessel der Rechnungskorrektur (Pflicht) |
| @kBenutzer | INT | - | Benutzer-ID (Pflicht) |
| @kGutschriftStornogrund | INT | -1 | Stornogrund (wie bei Rechnung) |
| @cKommentar | NVARCHAR(100) | NULL | Optionaler Kommentar |
| @dStorniert | DATETIME | GETDATE() | Stornodatum |
| @kStornoGutschrift | INT OUTPUT | - | ID des Gegen-Stornobelegs |

**Rueckgabe:**
- OUTPUT-Parameter: @kStornoGutschrift mit ID des Gegen-Stornobelegs
- Resultset: kGutschrift, cGutschriftNr, nError, kStornoGutschrift

**Hinweis:** Der Gegen-Stornobeleg hat nStornoTyp = 2 und negative Betraege.

---

## Installation

Die SPs werden ueber `INSTALL-NOVVIA-Complete.sql` installiert (Abschnitt 7b).

```sql
-- Manuell einzelne SP installieren:
USE Mandant_2;
GO
-- Dann SP-Code aus INSTALL-NOVVIA-Complete.sql kopieren
```

---

## Integration in C#

**CoreService.cs:**
```csharp
// Rechnungen auflisten
public async Task<IEnumerable<RechnungUebersicht>> GetRechnungenAsync(
    string? suche = null, int? status = null, int? kKunde = null,
    DateTime? vonDatum = null, DateTime? bisDatum = null, int limit = 1000)
{
    using var cmd = _connection.CreateCommand();
    cmd.CommandText = "NOVVIA.spRechnungenAuflisten";
    cmd.CommandType = CommandType.StoredProcedure;
    cmd.Parameters.AddWithValue("@cSuche", suche ?? (object)DBNull.Value);
    cmd.Parameters.AddWithValue("@nStatus", status ?? (object)DBNull.Value);
    // ... weitere Parameter

    using var reader = await cmd.ExecuteReaderAsync();
    var liste = new List<RechnungUebersicht>();
    while (await reader.ReadAsync())
    {
        liste.Add(new RechnungUebersicht { ... });
    }
    return liste;
}

// Rechnung stornieren
public async Task<int> StorniereRechnungAsync(int kRechnung, int kBenutzer,
    int stornoGrund = -1, string? kommentar = null)
{
    using var cmd = _connection.CreateCommand();
    cmd.CommandText = "NOVVIA.spRechnungStornieren";
    cmd.CommandType = CommandType.StoredProcedure;
    cmd.Parameters.AddWithValue("@kRechnung", kRechnung);
    cmd.Parameters.AddWithValue("@kBenutzer", kBenutzer);
    cmd.Parameters.AddWithValue("@kRechnungStornogrund", stornoGrund);
    cmd.Parameters.AddWithValue("@cKommentar", kommentar ?? (object)DBNull.Value);

    var outputParam = new SqlParameter("@kGutschrift", SqlDbType.Int)
        { Direction = ParameterDirection.Output };
    cmd.Parameters.Add(outputParam);

    await cmd.ExecuteNonQueryAsync();
    return (int)outputParam.Value;
}
```

---

## Fehler-Codes

Bei Storno-Operationen gibt nError die Fehlermeldung an:
| nError | Bedeutung |
|--------|-----------|
| 0 | Erfolgreich |
| 1 | Rechnung bereits storniert |
| 2 | Rechnung hat Teilzahlungen |
| 254 | Rechnung/Gutschrift nicht gefunden |

---

## Abhaengigkeiten

- JTL-Wawi Basistabellen (keine Views!)
- JTL Table-Valued Parameters (nur fuer Storno):
  - `Rechnung.TYPE_spRechnungenStornieren`
  - `dbo.TYPE_spGutschriftenStornieren`
- NOVVIA.spLogSchreiben muss vorhanden sein
- NOVVIA.spMahnstufen fuer dynamische Mahnstufen-Werte

---

## Changelog

| Datum | Version | Aenderung |
|-------|---------|-----------|
| 2026-01-03 | 1.0 | Initiale Erstellung aller 6 SPs |
| 2026-01-03 | 1.1 | Umstellung auf JTL-Basistabellen (keine Views mehr!) |
| 2026-01-03 | 1.2 | Neue SPs: spLieferscheineAuflisten, spLieferantenbestellungenAuflisten, spEingangsrechnungenAuflisten |

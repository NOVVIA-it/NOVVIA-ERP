# Workflow-System / Automatisierung - NOVVIA ERP

## Uebersicht

Das Workflow-System ermoeglicht ereignisbasierte Automatisierung.
Beim Anlegen, Aendern oder Loeschen von Datensaetzen koennen automatisch Aktionen ausgefuehrt werden.

## Architektur

```
┌─────────────────────────────────────────────────────────────┐
│                    Ereignis ausgeloest                       │
│        (z.B. Kunde angelegt, Artikel geaendert)             │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              WorkflowService.AusfuehrenAsync()               │
│                                                              │
│   1. Passende Workflows suchen (Entity + Ereignis)          │
│   2. Bedingungen pruefen                                     │
│   3. Aktionen ausfuehren                                     │
│   4. Log schreiben                                           │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────┐
│              NOVVIA.Workflow (DB)                            │
│                                                              │
│   kWorkflow | cName              | cEntityTyp | cEreignis   │
│   ----------|--------------------|-----------:|-------------|
│   1         | Debitorennr setzen | Kunde      | Angelegt    │
│   2         | Preis berechnen    | Artikel    | Geaendert   │
└─────────────────────────────────────────────────────────────┘
```

## Entities und Ereignisse

### Unterstuetzte Entities

| Entity | Beschreibung |
|--------|--------------|
| Kunde | Kundenstammdaten |
| Artikel | Artikelstammdaten |
| Auftrag | Verkaufsauftraege |
| Lieferant | Lieferantenstammdaten |
| Rechnung | Rechnungen |

### Ereignisse

| Ereignis | Beschreibung |
|----------|--------------|
| Angelegt | Neuer Datensatz wurde erstellt |
| Geaendert | Datensatz wurde aktualisiert |
| Geloescht | Datensatz wurde entfernt |
| StatusGeaendert | Status des Datensatzes hat sich geaendert |

## Aktionstypen

### FeldSetzen

Setzt ein Feld auf einen Wert oder Formel-Ergebnis.

```
Zielfeld: cDebitorenNr
Formel: {cKundenNr}
```

### Log

Schreibt einen Eintrag ins Protokoll.

```
Beschreibung: Kunde {cKundenNr} wurde angelegt
```

### StatusAendern

Aendert den Status eines Datensatzes.

```
Neuer Status: Aktiv
```

## Formeln

### Feldverweise

Felder werden in geschweiften Klammern angegeben:

```
{cKundenNr}         → Kundennummer
{fVKNetto}          → VK Netto Preis
{Preis}*1.19        → Preis mit MwSt
```

### Funktionen

| Funktion | Beschreibung | Beispiel |
|----------|--------------|----------|
| HEUTE() | Aktuelles Datum | HEUTE() |
| JETZT() | Aktueller Zeitpunkt | JETZT() |
| JAHR(datum) | Jahr extrahieren | JAHR(HEUTE()) |
| MONAT(datum) | Monat extrahieren | MONAT({dErstelldatum}) |
| RUNDEN(zahl,stellen) | Kaufmaennisches Runden | RUNDEN({fPreis},2) |
| WENN(bed,wahr,falsch) | Bedingte Auswertung | WENN({fPreis}>100,"Teuer","Guenstig") |
| VERKETTEN(a,b,...) | Texte verbinden | VERKETTEN({cVorname}," ",{cName}) |
| GROSS(text) | In Grossbuchstaben | GROSS({cName}) |
| KLEIN(text) | In Kleinbuchstaben | KLEIN({cEmail}) |
| LINKS(text,n) | Erste n Zeichen | LINKS({cArtNr},3) |
| RECHTS(text,n) | Letzte n Zeichen | RECHTS({cEAN},4) |
| LAENGE(text) | Textlaenge | LAENGE({cName}) |
| ERSETZEN(text,alt,neu) | Text ersetzen | ERSETZEN({cName},"GmbH","AG") |
| ABS(zahl) | Absolutwert | ABS({fDifferenz}) |
| MIN(a,b) | Minimum | MIN({fEK},{fVK}) |
| MAX(a,b) | Maximum | MAX({fBestand},0) |

### Operatoren

| Operator | Beschreibung |
|----------|--------------|
| + | Addition |
| - | Subtraktion |
| * | Multiplikation |
| / | Division |
| = | Gleich |
| <> | Ungleich |
| > | Groesser |
| < | Kleiner |
| >= | Groesser gleich |
| <= | Kleiner gleich |
| UND | Logisches Und |
| ODER | Logisches Oder |
| NICHT | Logisches Nicht |

## Beispiel-Workflows

### 1. Debitorennummer = Kundennummer

```
Name: Debitorennr automatisch setzen
Entity: Kunde
Ereignis: Angelegt
Aktion: FeldSetzen
Zielfeld: cDebitorenNr
Formel: {cKundenNr}
```

### 2. VK mit Aufschlag berechnen

```
Name: VK aus EK berechnen
Entity: Artikel
Ereignis: Geaendert
Bedingung: {fEKNetto} > 0
Aktion: FeldSetzen
Zielfeld: fVKNetto
Formel: RUNDEN({fEKNetto}*1.4,2)
```

### 3. Protokoll bei Auftragsanlage

```
Name: Auftrag protokollieren
Entity: Auftrag
Ereignis: Angelegt
Aktion: Log
Beschreibung: Neuer Auftrag {cAuftragNr} fuer Kunde {kKunde}
```

## Datenbank-Tabellen

### NOVVIA.Workflow

| Spalte | Typ | Beschreibung |
|--------|-----|--------------|
| kWorkflow | INT | Primaerschluessel |
| cName | NVARCHAR(100) | Name des Workflows |
| cBeschreibung | NVARCHAR(500) | Beschreibung |
| cEntityTyp | NVARCHAR(50) | Kunde, Artikel, etc. |
| cEreignis | NVARCHAR(50) | Angelegt, Geaendert, etc. |
| nAktiv | BIT | Workflow aktiv |
| nReihenfolge | INT | Ausfuehrungsreihenfolge |

### NOVVIA.WorkflowBedingung

| Spalte | Typ | Beschreibung |
|--------|-----|--------------|
| kWorkflowBedingung | INT | Primaerschluessel |
| kWorkflow | INT | FK zu Workflow |
| cFeld | NVARCHAR(100) | Zu pruefendes Feld |
| cOperator | NVARCHAR(20) | =, <>, >, <, etc. |
| cWert | NVARCHAR(200) | Vergleichswert |
| cVerknuepfung | NVARCHAR(10) | UND, ODER |

### NOVVIA.WorkflowAktion

| Spalte | Typ | Beschreibung |
|--------|-----|--------------|
| kWorkflowAktion | INT | Primaerschluessel |
| kWorkflow | INT | FK zu Workflow |
| cAktionsTyp | NVARCHAR(50) | FeldSetzen, Log, StatusAendern |
| cZielfeld | NVARCHAR(100) | Zielfeld bei FeldSetzen |
| cFormel | NVARCHAR(500) | Formel zur Berechnung |
| cWert | NVARCHAR(500) | Fester Wert |
| nReihenfolge | INT | Aktionsreihenfolge |

### NOVVIA.WorkflowLog

| Spalte | Typ | Beschreibung |
|--------|-----|--------------|
| kWorkflowLog | INT | Primaerschluessel |
| kWorkflow | INT | FK zu Workflow |
| cEntityTyp | NVARCHAR(50) | Betroffene Entity |
| kEntity | INT | ID des Datensatzes |
| cEreignis | NVARCHAR(50) | Ausgelöstes Ereignis |
| cErgebnis | NVARCHAR(20) | Erfolg, Fehler |
| cDetails | NVARCHAR(MAX) | Details/Fehler |
| dAusgefuehrt | DATETIME | Ausfuehrungszeitpunkt |

## UI: Einstellungen → Workflows

In den Einstellungen gibt es einen Tab "Workflows" mit:

- **DataGrid** - Alle Workflows mit Status, Name, Entity, Ereignis
- **Filter** - Nach Entity filtern
- **Bearbeiten** - Name, Entity, Ereignis, Aktion, Formel
- **Neu/Speichern/Loeschen** - Workflow-Verwaltung
- **?-Button** - Oeffnet Hilfe-Tab fuer Feldliste

## Setup-Script

```bash
sqlcmd -S "SERVER" -d "Mandant_2" -E -i "Scripts/Setup-NOVVIA-Workflow.sql"
```

Das Script:
1. Erstellt NOVVIA Schema (falls nicht vorhanden)
2. Erstellt Tabellen Workflow, WorkflowBedingung, WorkflowAktion, WorkflowLog
3. Erstellt Hilfe-Tabelle mit 114 Eintraegen
4. Erstellt Stored Procedures

## Hilfe-System

Die Hilfe-Tabelle (NOVVIA.Hilfe) enthaelt alle verfuegbaren:

- **Felder** - Alle Entity-Felder mit Datentyp
- **Funktionen** - Verfuegbare Formelfunktionen
- **Ereignisse** - Workflow-Trigger
- **Operatoren** - Vergleichs- und Rechenoperatoren

Zugriff ueber: Einstellungen → Hilfe (Felder)

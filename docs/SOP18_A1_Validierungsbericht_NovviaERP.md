# Validierungsbericht NovviaERP

## 1. Art der Validierung
Computergestützte Systemvalidierung (CSV) des NovviaERP Warenwirtschaftssystems für den pharmazeutischen Großhandel gemäß GDP/GMP-Anforderungen.

## 2. Beschreibung des Prozesses oder des Gerätes / Anlage
NovviaERP ist ein integriertes Warenwirtschaftssystem für den pharmazeutischen Großhandel mit folgenden Kernfunktionen:
- Artikelverwaltung mit Pharma-Eigenschaften (PZN, Chargen, MHD, Seriennummern)
- Kundenverwaltung mit GDP-Zertifikatsprüfung
- Lieferantenverwaltung mit MSV3-Schnittstelle
- Auftragsabwicklung (Verkauf und Einkauf)
- Lagerverwaltung mit Chargenrückverfolgbarkeit
- Rechnungswesen und Dokumentenmanagement
- Integration mit ABData für Pharma-Artikeldaten

## 3. Verantwortliche für die Durchführung der Validierung
| Rolle | Name | Datum | Unterschrift |
|-------|------|-------|--------------|
| Verantwortliche Person (VP) | | | |
| IT-Verantwortlicher | | | |
| Qualitätsbeauftragter | | | |

## 4. Risikoanalyse / kritische Parameter

### Chargenrückverfolgbarkeit
**Risiko:** Unzureichende Rückverfolgbarkeit kann zu Verlusten von Produkten und Lücken in der Lieferkette führen und somit Auswirkungen auf die Patientensicherheit im Falle einer Fälschung und Rückruf haben.

**Maßnahmen in NovviaERP:**
- Pflichtfelder für Charge/MHD bei pharmazeutischen Artikeln
- Automatische FEFO-Reservierung bei Kommissionierung
- Chargenhistorie mit vollständiger Bewegungsprotokollierung
- Quarantäne-Lager für gesperrte Chargen

### Lagerverwaltung und -kontrolle
**Risiko:** Unzuverlässige Lagerverwaltung kann zu falschen Bestandsmengen, Verlust von Arzneimitteln und möglicherweise zu Engpässen führen, was die Lieferkette beeinträchtigen könnte.

**Maßnahmen in NovviaERP:**
- Echtzeit-Bestandsführung pro Lagerort
- Mindestbestandsüberwachung mit automatischen Bestellvorschlägen
- Reservierungssystem für offene Aufträge
- Inventurfunktion mit Differenzprotokollierung

### Sicherheit und Zugriffskontrolle
**Risiko:** Unzureichende Sicherheitsmaßnahmen können zu unbefugtem Zugriff, Datenmanipulation und möglichen Datenschutzverletzungen führen.

**Maßnahmen in NovviaERP:**
- Rollenbasierte Zugriffssteuerung
- Verschlüsselte Passwörter in der Datenbank
- Audit-Trail für alle kritischen Änderungen
- Separate Berechtigungen für VP-Funktionen (Pharma-Freigaben)

### Datenverlust & Wiederherstellungspläne
**Risiko:** Wenn die Dokumentation nicht vollständig und aktuell ist, kann dies zu Compliance-Verstößen und möglicherweise zu Qualitätseinbußen führen. Fehlende Wiederherstellungspläne und Überwachungsmechanismen können zu Betriebsunterbrechungen und möglicherweise Verlust von Produkten und Daten führen.

**Maßnahmen in NovviaERP:**
- Automatische stündliche Datenbanksicherung
- Externe Backup-Speicherung
- Dokumentierte Wiederherstellungsprozedur
- Regelmäßige Backup-Tests

---

## 5. Testplan mit Akzeptanzkriterien

### Dokumentation der Anforderungen

#### 1. Welche Parteien werden für die CSV hinzugezogen?
- NOVVIA PHARM (Anwender)
- Interne IT-Abteilung
- Ggf. externer Qualitätsberater

#### 2. Unter welchem Betriebssystem läuft die Software?
| Komponente | Betriebssystem |
|------------|----------------|
| Server | Windows Server 2019 / 2022 / 2025 |
| Clients | Windows 10 / Windows 11 (deutsch) |

#### 3. Was für eine Hardwareumgebung ist vorhanden und notwendig?

**Server-Anforderungen:**
- Betriebssystem: Windows Server 2019/2022/2025
- Prozessor: Intel-kompatibler Prozessor, mindestens 4 Kerne, 3 GHz oder höher
- RAM: Mindestens 16 GB, empfohlen 32 GB
- Festplatte: M.2 PCIe NVMe oder NVMe-SSD, mindestens 250 GB für Datenbank
- SQL Server 2019 oder höher
- RAID-Verbund empfohlen

**Client-Anforderungen:**
- Betriebssystem: Windows 10/11 (deutsch)
- Prozessor: Intel i5 oder vergleichbar
- RAM: Mindestens 8 GB
- Festplatte: SSD empfohlen, ca. 2 GB Speicherplatz

**Aktuelle Installation:**
- 1 Server
- 3 Clients

#### 4. Wie viele Installationen wurden vom Anbieter bisher ausgeliefert?
NovviaERP ist eine Eigenentwicklung für NOVVIA PHARM und wird ausschließlich intern betrieben.

#### 5. In welcher Programmiersprache wurde die Software erstellt?
- Backend: C# / .NET 8
- Frontend: WPF (Windows Presentation Foundation)
- Datenbank: Microsoft SQL Server mit T-SQL

#### 6. Ist der Quellcode offen?
Der Quellcode wird intern verwaltet und ist nicht öffentlich zugänglich. Die vollständige Dokumentation und der Quellcode sind bei NOVVIA PHARM archiviert.

#### 7. Worst Case Szenario - Kann die Software weiter betrieben werden?
Ja, da NovviaERP eine Eigenentwicklung ist:
- Vollständiger Quellcode liegt vor
- Dokumentation ist intern verfügbar
- Keine Abhängigkeit von externen Lizenzgebern
- Standard-Technologien (.NET, SQL Server) werden verwendet

#### 8. Regulatorischer Rahmen
NovviaERP wurde unter Berücksichtigung folgender Anforderungen entwickelt:
- GDP (Good Distribution Practice) für Arzneimittel
- GoBD (Grundsätze zur ordnungsmäßigen Führung und Aufbewahrung von Büchern)
- DSGVO (Datenschutz-Grundverordnung)
- Arzneimittelgesetz (AMG)

#### 9. Wie werden Betriebssystem-Updates durchgeführt?
Das Betriebssystem schlägt Updates vor. Der IT-Verantwortliche wählt aus, welche Updates durchgeführt werden sollen. Vor größeren Updates wird ein vollständiges Backup erstellt.

#### 10. Wird die Software nach Betriebssystem-Updates geprüft?
Ja, nach jedem größeren Betriebssystem-Update werden die Kernfunktionen getestet:
- Datenbankverbindung
- Benutzeranmeldung
- Artikel- und Kundenverwaltung
- Auftragsabwicklung
- Druckfunktionen

#### 11. Vorkehrungen zum Schutz vor Cyberangriffen
- Rollenbasierte Berechtigungen begrenzen Zugriff
- Stündliche vollständige Backups auf externe Datenträger
- Verschlüsselte Datenbankverbindungen
- Windows-Firewall aktiv
- Aktueller Virenschutz

#### 12. Gibt es Heimarbeitsplätze?
Nein, es gibt keine Heimarbeitsplätze. Das System ist nur im lokalen Netzwerk erreichbar.

#### 13. Passwortverwaltung
- Passwörter werden bei Benutzeranlage vom Administrator vergeben
- Passwörter werden verschlüsselt (gehashed) in der Datenbank gespeichert
- Keine Klartext-Speicherung

#### 14. Wie werden Software-Updates eingespielt?
1. Backup der aktuellen Datenbank erstellen
2. Alle Benutzer abmelden
3. Neue Version deployen
4. Datenbank-Migration ausführen (falls erforderlich)
5. Funktionstests durchführen
6. Freigabe für Benutzer

#### 15. Wie erfahren Anwender von Änderungen?
- Changelog wird bei jedem Release gepflegt
- Wichtige Änderungen werden per E-Mail kommuniziert
- Versionsnummer und Änderungsdatum in der Anwendung sichtbar

#### 16. Werden Software-Updates zuvor getestet?
Ja, alle Updates werden in einer Testumgebung getestet bevor sie produktiv eingesetzt werden.

#### 17. Kundenindividuelle Änderungen
Alle Änderungen werden dokumentiert:
- Git-Versionsverwaltung für Quellcode
- Änderungshistorie pro Commit
- Test nach jeder Änderung

#### 18. Datensicherung
- Automatische stündliche Sicherung
- Speicherung auf externen Datenträgern
- Manuelle Sicherung vor wichtigen Ereignissen (Updates) möglich
- Sicherungen werden regelmäßig auf Wiederherstellbarkeit getestet

#### 19. Support
- Interner IT-Support
- Dokumentation im System verfügbar
- Bei Bedarf: Zugriff auf Quellcode für Fehlerbehebung

#### 20. Schnittstellen zu anderen Systemen

| Schnittstelle | Richtung | Beschreibung |
|---------------|----------|--------------|
| MSV3 | Input/Output | Bestellung und Lieferavis bei Pharma-Großhändlern |
| ABData | Input | Pharma-Artikeldatenbank (PZN, Preise, Wirkstoffsuche) |
| JTL-Wawi | Input/Output | Artikeldaten, Bestellungen, Lagerbestand |
| Excel/CSV | Input | Import von Aufträgen und Bestellungen |

#### 21. Wiederherstellung älterer Datensicherungen
Ja, ältere Datensicherungen können wiederhergestellt werden. Die Datenbank wird automatisch auf die aktuelle Schemaversion migriert.

#### 22. Erlaubte andere Software
- Windows-Standardprogramme
- Microsoft Office
- PDF-Reader
- Virenschutz (empfohlen)

#### 23. Vorausgesetzte Software
- Microsoft SQL Server 2019 oder höher (Server)
- .NET 8 Runtime (Clients)
- Windows 10/11 (Clients)

---

### Spezifizierung der Testfälle

#### 1. Dateneingabe
Die Benutzer können folgende Datentypen eingeben:
- Numerisch (Mengen, Preise)
- Alphanumerisch (Namen, Beschreibungen)
- Datum und Zeit
- Auswahllisten (Dropdown)

Jede Eingabe wird nach Feldart und logisch geprüft.

#### 2. Plausibilitätsprüfung
- Pflichtfelder werden bei Speichern geprüft
- Formatvalidierung (z.B. E-Mail, PLZ)
- Logische Prüfungen (z.B. MHD in der Zukunft)
- Fehlermeldungen bei ungültigen Eingaben

#### 3. Benutzeranmeldung und Rechte

**Benutzerrollen:**

| Rolle | Berechtigungen |
|-------|----------------|
| Administrator | Vollzugriff auf alle Funktionen |
| Verkauf | Kunden, Aufträge, Rechnungen |
| Einkauf | Lieferanten, Bestellungen, Wareneingang |
| Lager | Lagerbewegungen, Kommissionierung, Inventur |
| VP (Verantwortliche Person) | Pharma-Freigaben, Chargen-Sperrung, GDP-Zertifikatsprüfung |

Jeder Benutzer meldet sich mit Benutzername und Passwort an.

#### 4. Kundenanlage
Kunden werden über die Kundenverwaltung angelegt:
1. Navigation: Stammdaten → Kunden → Neu
2. Pflichtfelder: Name/Firma, Adresse
3. Optional: Kontaktdaten, Konditionen, Pharma-Zertifikate
4. Speichern: Kundennummer wird automatisch vergeben

#### 5. Lieferantenanlage
Lieferanten werden analog zu Kunden angelegt:
1. Navigation: Stammdaten → Lieferanten → Neu
2. Zusätzlich: MSV3-Zugangsdaten falls vorhanden
3. Pharma-Eigenschaften für GDP-Prüfung

#### 6. Artikelbearbeitung
Artikel werden über die Artikelverwaltung bearbeitet:
1. Navigation: Stammdaten → Artikel
2. Suche per Artikelnummer, PZN, Name, Barcode
3. Bearbeitung: Stammdaten, Preise, Lager, Lieferanten, Pharma-Eigenschaften

**ABData-Integration:**
- Wirkstoffsuche verfügbar
- Automatische Artikelanlage aus ABData-Daten
- PZN-Prüfung

#### 7. Kunden/Lieferanten sperren
1. Adresse öffnen
2. Tab "Konditionen" → Checkbox "Gesperrt" aktivieren
3. Speichern
4. Gesperrte Adressen werden rot markiert
5. Aufträge/Bestellungen für gesperrte Adressen werden blockiert

**Pharma-Zertifikatsprüfung:**
- GDP-Zertifikat muss gültig sein (Prüfung innerhalb 12 Monate)
- Status "Freigabe" erforderlich
- Prüfung nur durch VP möglich

#### 8. Chargen sperren/freigeben
1. Navigation: Lager → Chargensuche
2. Charge auswählen
3. Rechtsklick → "In Quarantäne-Lager umbuchen"
4. Charge ist für Verkauf/Kommissionierung gesperrt

#### 9. Pflichtfelder bei Auftragsbearbeitung
- Chargen-/Seriennummernpflichtige Artikel erfordern entsprechende Eingabe
- MHD bei chargenpflichtigen Artikeln
- Keine Lagerbewegung ohne vollständige Daten möglich

#### 10. FEFO-Sicherstellung
- Aktivierung über Parameter: "Bei Kommissionierung älteste Chargen reservieren (FEFO)"
- Automatische Reservierung nach Verfallsdatum
- Warnung bei überschrittenem MHD

#### 11. Auftragsabwicklung

| Schritt | Dokument |
|---------|----------|
| 1. Auftragserfassung | Auftragsbestätigung |
| 2. Kommissionierung | Kommissionierschein |
| 3. Versand | Lieferschein |
| 4. Fakturierung | Rechnung |

#### 12. Kundenretouren

**Retourentypen:**
1. **Mit Warenrücknahme und Gutschrift** - Ware zurück, Gutschrift erstellen
2. **Mit Warenrücknahme und Austausch nach Wareneingang** - Ware zurück, dann Ersatz senden
3. **Mit Warenrücknahme und Sofortaustausch** - Ersatz sofort senden, Ware zurück
4. **Ohne Warenrücknahme mit Austausch** - Nur Ersatz senden
5. **Ohne Warenrücknahme mit Gutschrift** - Nur Gutschrift erstellen

Ablauf:
1. Lieferschein auswählen
2. Position auswählen → "Retournieren"
3. Retourentyp und Menge wählen
4. Rücksendeschein wird erstellt
5. Nach Wareneingang: Warenrücknahme → Gutschrift/Austausch

#### 13. Änderungsnachvollziehbarkeit
- Nachträgliche Änderungen am Auftrag nach Versand sind nicht möglich
- Alle Änderungen werden im Audit-Trail protokolliert
- Änderungshistorie pro Beleg einsehbar

#### 14. Securpharm/Verifizierungspflichtige Ware
Securpharm wird vom Logistikdienstleister durchgeführt.

---

## 6. Risikoanalyse

| Bereich | Risiko | Bewertung | Maßnahme |
|---------|--------|-----------|----------|
| Geschäftsprozesse | Systemausfall | Mittel | Stündliche Backups, Wiederherstellungsplan |
| Wiederherstellung | Datenverlust | Niedrig | Externe Backup-Speicherung, regelmäßige Tests |
| Datensensibilität | Kundendaten, Pharma-Daten | Hoch | Zugriffssteuerung, Verschlüsselung |
| Datenintegrität | Manipulation | Niedrig | Audit-Trail, Berechtigungssystem |
| Ausdrucke | Rechnungen, Lieferscheine | Mittel | PDF-Archivierung, GoBD-konforme Speicherung |

---

## 7. Bewertung der Testfälle

| Testfall | Ergebnis | Bemerkung |
|----------|----------|-----------|
| Benutzeranmeldung | | |
| Kundenanlage | | |
| Lieferantenanlage | | |
| Artikelbearbeitung | | |
| Auftragserfassung | | |
| Kommissionierung | | |
| Lieferscheinerstellung | | |
| Rechnungserstellung | | |
| Chargenrückverfolgung | | |
| FEFO-Prüfung | | |
| Retouren | | |
| Backup/Restore | | |
| Berechtigungen | | |

**Allgemein:**

**Abweichungen:**

---

## 8. Maßnahmen

| Nr. | Abweichung | Maßnahme | Verantwortlich | Termin |
|-----|------------|----------|----------------|--------|
| | | | | |

---

## 9. Überprüfung der Abstellung der Abweichungen

| Nr. | Maßnahme durchgeführt | Wirksamkeit geprüft | Datum | Unterschrift VP |
|-----|----------------------|---------------------|-------|-----------------|
| | | | | |

---

## 10. Freigabe

In der Gesamtbetrachtung ist das System **NovviaERP** für die Verwaltung des Warenflusses sowie der Chargenrückverfolgbarkeit im pharmazeutischen Großhandel geeignet und wurde nach der vollständigen Behebung der in Punkt 7 respektive Punkt 9 genannten Mängel sowie einer erneuten Überprüfung und/oder Wirksamkeitskontrolle durch die VP freigegeben.

---

| Name | Funktion | Datum | Unterschrift |
|------|----------|-------|--------------|
| | Verantwortliche Person (VP) | | |
| | Geschäftsführung | | |
| | Qualitätsbeauftragter | | |

---

*Dokumentenversion: 1.0*
*Erstellungsdatum: [Datum]*
*Nächste Überprüfung: [Datum + 1 Jahr]*

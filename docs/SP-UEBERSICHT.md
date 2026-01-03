# Stored Procedures Uebersicht - NOVVIA ERP

## Legende
- **[JTL]** = JTL Native SP - NICHT modifizieren! Bei Problemen: NOVVIA-Wrapper erstellen
- **[NOVVIA]** = Eigene SP im NOVVIA Schema
- **[NOVVIA-Wrapper]** = NOVVIA SP die JTL SP aufruft

---

## JTL Native SPs (Schema: dbo/Kunde/Verkauf/Versand/Rechnung)

| SP Name | Schema | Verwendet in | Beschreibung |
|---------|--------|--------------|--------------|
| **[JTL]** `spKundeInsert` | Kunde | CoreService.cs:928, JtlDbContext.cs:291 | Kunde anlegen |
| **[JTL]** `spKundeUpdate` | Kunde | CoreService.cs:1071, JtlDbContext.cs:348 | Kunde aktualisieren |
| **[JTL]** `spKundenEigenesFeldCreateOrUpdate` | Kunde | CoreService.cs:4777, 4822 | Eigene Felder Kunde |
| **[JTL]** `spAuftragEckdatenBerechnen` | Verkauf | CoreService.cs:6269, 6456, JtlDbContext.cs:638 | Auftrag Summen neu berechnen |
| **[JTL]** `spLieferscheinErstellen` | Versand | CoreService.cs:3369 | Lieferschein erstellen |
| **[JTL]** `spLieferscheinPosErstellen` | Versand | CoreService.cs:3397 | Lieferschein Position |
| **[JTL]** `spRechnungEckdatenBerechnen` | Rechnung | CoreService.cs:3848, 3968, JtlDbContext.cs:1402 | Rechnung Summen berechnen |
| **[JTL]** `spWarenlagerEingangSchreiben` | dbo | CoreService.cs:6579, JtlStockBookingClient.cs:59 | Wareneingang buchen |
| **[JTL]** `spWarenlagerAusgangSchreiben` | dbo | CoreService.cs:6618, JtlStockBookingClient.cs:169 | Warenausgang buchen |
| **[JTL]** `spPlatzUmbuchen` | dbo | CoreService.cs:8320, 8406 | Lagerplatz umbuchen |

---

## NOVVIA Eigene SPs

### Artikel
| SP Name | Verwendet in | Beschreibung |
|---------|--------------|--------------|
| **[NOVVIA]** `spArtikelInsert` | CoreService.cs:2888 | Artikel anlegen |
| **[NOVVIA]** `spArtikelUpdate` | CoreService.cs:2773 | Artikel aktualisieren |
| **[NOVVIA-Wrapper]** `spArtikelEigenesFeldCreateOrUpdate` | CoreService.cs:4714 | Eigene Felder Artikel (nutzt JTL Tabellen) |

### Auftraege
| SP Name | Verwendet in | Beschreibung |
|---------|--------------|--------------|
| **[NOVVIA]** `spOrderCreateUpdateDelete` | SP-NOVVIA-OrderCreateUpdateDelete.sql | Auftrag CRUD (ruft JTL `Verkauf.spAuftragEckdatenBerechnen`) |
| **[NOVVIA]** `spOrderPositionAddUpdate` | SP-NOVVIA-OrderCreateUpdateDelete.sql | Auftragsposition (ruft JTL `Verkauf.spAuftragEckdatenBerechnen`) |
| **[NOVVIA-Wrapper]** `spAuftragEigenesFeldCreateOrUpdate` | CoreService.cs:4916, 4952 | Eigene Felder Auftrag (nutzt JTL Tabellen) |

### Lieferanten
| SP Name | Verwendet in | Beschreibung |
|---------|--------------|--------------|
| **[NOVVIA]** `spLieferantEigenesFeldSpeichern` | CoreService.cs:7185 | Eigene Felder Lieferant |
| **[NOVVIA]** `spLieferantAttributSpeichern` | CoreService.cs:7201 | Lieferant Attribute |
| **[NOVVIA]** `spNOVVIA_LieferantErweitertLaden` | EinkaufService.cs:172 | Pharma-Daten Lieferant laden |
| **[NOVVIA]** `spNOVVIA_LieferantErweitertSpeichern` | EinkaufService.cs:190 | Pharma-Daten Lieferant speichern |

### Pharma-Validierung (GDP/GMP)
| Tabelle | Beschreibung |
|---------|--------------|
| `NOVVIA.LieferantErweitert` | Pharma-Validierungsdaten pro Lieferant |

**Felder in LieferantErweitert:**
- `nAmbient` - Ambient (15-25 Grad)
- `nCool` - Kuehlkette (2-8 Grad)
- `nMedcan` - Medizin. Cannabis
- `nTierarznei` - Tierarzneimittel
- `cGDP` - GDP-Zertifikat
- `cGMP` - GMP-Zertifikat
- `dQualifiziertAm` - Qualifizierungsdatum
- `cQualifiziertVon` - Qualifiziert von
- `cQualifikationsDocs` - Dokumentenpfad

**Aktivierung:** `PHARMA=1` in `NOVVIA.FirmaEinstellung`
**Berechtigung:** Nur RP-Berechtigte duerfen bearbeiten (via `spDarfValidierungBearbeiten`)

### Benutzer & Rechte
| SP Name | Verwendet in | Beschreibung |
|---------|--------------|--------------|
| **[NOVVIA]** `spBenutzerAnmelden` | BenutzerService.cs:91 | Login |
| **[NOVVIA]** `spBenutzerAbmelden` | BenutzerService.cs:136 | Logout |
| **[NOVVIA]** `spSessionValidieren` | BenutzerService.cs:169 | Session pruefen |
| **[NOVVIA]** `spFehlversuchRegistrieren` | BenutzerService.cs:73 | Login-Fehlversuche |
| **[NOVVIA]** `spHatRecht` | CoreService.cs:8587, BenutzerService.cs:263 | Rechte pruefen |
| **[NOVVIA]** `spDarfValidierungBearbeiten` | CoreService.cs:8614 | Validierungsrechte |
| **[NOVVIA]** `spRechteGenerieren` | Setup-NOVVIA-Benutzerrechte.sql | Rechte generieren |
| **[NOVVIA]** `spRolleRechteZuweisen` | Setup-NOVVIA-Benutzerrechte.sql | Rollen zuweisen |

### Konfiguration
| SP Name | Verwendet in | Beschreibung |
|---------|--------------|--------------|
| **[NOVVIA]** `spConfigGet` | ConfigService.cs:44, 62 | Config lesen |
| **[NOVVIA]** `spConfigSet` | ConfigService.cs:87 | Config schreiben |

### Dashboard
| SP Name | Verwendet in | Beschreibung |
|---------|--------------|--------------|
| **[NOVVIA]** `spDashboardKPIs` | Setup-NOVVIA-Dashboard.sql | KPIs |
| **[NOVVIA]** `spDashboardUmsatzVerlauf` | Setup-NOVVIA-Dashboard.sql | Umsatz 12 Monate |
| **[NOVVIA]** `spDashboardTopKunden` | Setup-NOVVIA-Dashboard.sql | Top Kunden |
| **[NOVVIA]** `spDashboardTopArtikel` | Setup-NOVVIA-Dashboard.sql | Top Artikel |
| **[NOVVIA]** `spDashboardAuftragStatus` | Setup-NOVVIA-Dashboard.sql | Status-Verteilung |
| **[NOVVIA]** `spDashboardZahlungseingang` | Setup-NOVVIA-Dashboard.sql | Zahlungen/Woche |
| **[NOVVIA]** `spDashboardLagerAlarm` | Setup-NOVVIA-Dashboard.sql | Niedrige Bestaende |
| **[NOVVIA]** `spDashboardAktivitaeten` | Setup-NOVVIA-Dashboard.sql | Letzte Aktivitaeten |
| **[NOVVIA]** `spDashboardMahnungen` | Setup-NOVVIA-Dashboard.sql | Mahnungen |

### Logging
| SP Name | Verwendet in | Beschreibung |
|---------|--------------|--------------|
| **[NOVVIA]** `spLogSchreiben` | Setup-NOVVIA-Log.sql | Log-Eintrag schreiben |
| **[NOVVIA]** `spLogStammdatenAenderung` | Setup-NOVVIA-Log.sql | Stammdaten-Aenderung |
| **[NOVVIA]** `spLogBewegung` | Setup-NOVVIA-Log.sql | Bewegung loggen |
| **[NOVVIA]** `spLogAbfragen` | Setup-NOVVIA-Log.sql | Log abfragen |
| **[NOVVIA]** `spLogStatistik` | Setup-NOVVIA-Log.sql | Log-Statistik |

### Rechnungen & Rechnungskorrekturen (NEU 2026-01-03) - **Basistabellen!**
| SP Name | Tabellen | Beschreibung |
|---------|----------|--------------|
| **[NOVVIA]** `spMahnstufen` | dbo.tMahnstufe | Mahnstufen dynamisch lesen |
| **[NOVVIA]** `spRechnungLesen` | Rechnung.tRechnung, tRechnungEckdaten, tRechnungAdresse, tRechnungPosition | Einzelrechnung mit Positionen |
| **[NOVVIA]** `spRechnungenAuflisten` | Rechnung.tRechnung, tRechnungEckdaten, tRechnungAdresse, tRechnungStorno | Rechnungsliste mit Filter |
| **[NOVVIA-Wrapper]** `spRechnungStornieren` | Rechnung.spRechnungenStornieren | Rechnung stornieren |
| **[NOVVIA]** `spRechnungskorrekturLesen` | dbo.tgutschrift, tGutschriftPos, Kunde.tKunde | Einzelkorrektur mit Positionen |
| **[NOVVIA]** `spRechnungskorrekturenAuflisten` | dbo.tgutschrift, Kunde.tKunde | Korrekturliste mit Filter |
| **[NOVVIA-Wrapper]** `spRechnungskorrekturStornieren` | dbo.spGutschriftenStornieren | Korrektur stornieren |

### Lieferschein (NEU 2026-01-03) - **Basistabellen!**
| SP Name | Tabellen | Beschreibung |
|---------|----------|--------------|
| **[NOVVIA]** `spLieferscheineAuflisten` | dbo.tLieferschein, tLieferscheinEckdaten, tBestellung | Lieferscheinliste mit Filter |
| **[NOVVIA]** `spLieferscheinLesen` | dbo.tLieferschein, tLieferscheinPos, tBestellpos | Einzellieferschein mit Positionen |

### Lieferantenbestellung / Einkauf (NEU 2026-01-03) - **Basistabellen!**
| SP Name | Tabellen | Beschreibung |
|---------|----------|--------------|
| **[NOVVIA]** `spLieferantenbestellungenAuflisten` | dbo.tLieferantenBestellung, tLieferant | Bestellungsliste mit Filter |
| **[NOVVIA]** `spLieferantenbestellungLesen` | dbo.tLieferantenBestellung, tLieferantenBestellungPos | Einzelbestellung mit Positionen |
| **[NOVVIA]** `spEingangsrechnungenAuflisten` | dbo.tEingangsrechnung, tLieferant | Eingangsrechnungsliste mit Filter |
| **[NOVVIA]** `spEingangsrechnungLesen` | dbo.tEingangsrechnung, tEingangsrechnungPos | Einzelrechnung mit Positionen |

### MSV3
| SP Name | Verwendet in | Beschreibung |
|---------|--------------|--------------|
| **[NOVVIA]** `spMSV3VerfuegbarkeitCache_Get` | MSV3Service.cs:1395 | Cache lesen |
| **[NOVVIA]** `spMSV3VerfuegbarkeitCache_GetBulk` | MSV3Service.cs:1420 | Cache Bulk |
| **[NOVVIA]** `spMSV3VerfuegbarkeitCache_Upsert` | MSV3Service.cs:1448 | Cache schreiben |
| **[NOVVIA]** `spMSV3RequestLog_Insert` | MSV3Service.cs:1497 | Request Log |
| **[NOVVIA]** `spMSV3Cache_Cleanup` | MSV3Service.cs:1518 | Cache aufraumen |

### Sprache / Lokalisierung (NEU 2026-01-03)
| SP Name | Tabellen | Beschreibung |
|---------|----------|--------------|
| **[NOVVIA]** `spSpracheImportieren` | NOVVIA.Sprache | Text importieren (MERGE) |

**Tabelle NOVVIA.Sprache:**
| Spalte | Typ | Beschreibung |
|--------|-----|--------------|
| kSprache | INT | Primary Key |
| cSchluessel | NVARCHAR(200) | Key (z.B. "Buttons.Speichern") |
| cSprache | NVARCHAR(10) | Sprachcode ("de", "en") |
| cWert | NVARCHAR(500) | Angezeigter Text |
| cBeschreibung | NVARCHAR(500) | Verwendungshinweis |
| dErstellt | DATETIME | Erstellungsdatum |
| dGeaendert | DATETIME | Aenderungsdatum |

**C# Service:** `NovviaERP.Core.Services.Lang`
- `Lang.InitAsync(connectionString, "de")` - Initialisieren
- `Lang.Get("Buttons.Speichern")` - Text abrufen
- `Lang.SetAsync("key", "wert")` - Text speichern
- `Lang.LoadFromDbAsync()` - Aus DB neu laden

**UI:** Einstellungen → Sprache Tab

---

## WICHTIG: Regel fuer JTL SPs

**NIEMALS JTL SPs direkt modifizieren!**

Wenn eine JTL SP angepasst werden muss:
1. NOVVIA-Wrapper erstellen: `NOVVIA.spXxx`
2. Wrapper ruft original JTL SP auf
3. Zusaetzliche Logik im Wrapper

Beispiel: `NOVVIA.spOrderCreateUpdateDelete` ruft `Verkauf.spAuftragEckdatenBerechnen` auf.

---

## TODO: Diese JTL SPs sollten NOVVIA-Wrapper bekommen

| JTL SP | Grund |
|--------|-------|
| `dbo.spPlatzUmbuchen` | Wird direkt in CoreService aufgerufen - Wrapper fuer Logging |
| `dbo.spWarenlagerEingangSchreiben` | Wird direkt aufgerufen - Wrapper fuer Validierung |
| `dbo.spWarenlagerAusgangSchreiben` | Wird direkt aufgerufen - Wrapper fuer Validierung |

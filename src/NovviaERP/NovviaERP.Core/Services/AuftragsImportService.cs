using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using Dapper;
using NovviaERP.Core.Data;
using NovviaERP.Core.Entities;
using Serilog;

namespace NovviaERP.Core.Services
{
    /// <summary>
    /// Auftrags-Import aus CSV/Excel mit flexibler Feldzuordnung
    /// Ähnlich VARIO 8 Schnellerfassung
    /// </summary>
    public class AuftragsImportService
    {
        private readonly JtlDbContext _db;
        private static readonly ILogger _log = Log.ForContext<AuftragsImportService>();

        public AuftragsImportService(JtlDbContext db) => _db = db;

        #region Import-Vorlagen
        /// <summary>
        /// Verfügbare Felder für Auftragsimport
        /// </summary>
        public static readonly List<ImportFeldDefinition> VerfuegbareFelder = new()
        {
            // Auftragskopf
            new("BestellNr", "Bestellnummer", "Kopf", true),
            new("BestellDatum", "Bestelldatum", "Kopf"),
            new("Bemerkung", "Bemerkung", "Kopf"),
            new("Lieferdatum", "Gewünschtes Lieferdatum", "Kopf"),
            
            // Kunde
            new("KundenNr", "Kundennummer", "Kunde"),
            new("KundeAnrede", "Anrede", "Kunde"),
            new("KundeFirma", "Firma", "Kunde"),
            new("KundeVorname", "Vorname", "Kunde"),
            new("KundeNachname", "Nachname", "Kunde", true),
            new("KundeStrasse", "Straße", "Kunde"),
            new("KundePLZ", "PLZ", "Kunde"),
            new("KundeOrt", "Ort", "Kunde"),
            new("KundeLand", "Land", "Kunde"),
            new("KundeEmail", "E-Mail", "Kunde"),
            new("KundeTelefon", "Telefon", "Kunde"),
            
            // Lieferadresse
            new("LieferFirma", "Lieferadresse Firma", "Lieferung"),
            new("LieferName", "Lieferadresse Name", "Lieferung"),
            new("LieferStrasse", "Lieferadresse Straße", "Lieferung"),
            new("LieferPLZ", "Lieferadresse PLZ", "Lieferung"),
            new("LieferOrt", "Lieferadresse Ort", "Lieferung"),
            new("LieferLand", "Lieferadresse Land", "Lieferung"),
            
            // Positionen
            new("ArtNr", "Artikelnummer", "Position", true),
            new("ArtikelName", "Artikelbezeichnung", "Position"),
            new("Menge", "Menge", "Position", true),
            new("Einzelpreis", "Einzelpreis", "Position"),
            new("Rabatt", "Rabatt %", "Position"),
            new("PosText", "Positionstext", "Position"),
            
            // Sonstiges
            new("Versandart", "Versandart", "Versand"),
            new("Zahlungsart", "Zahlungsart", "Zahlung"),
            new("ExterneAuftragNr", "Externe Auftragsnummer", "Extern")
        };

        /// <summary>
        /// Lädt gespeicherte Import-Vorlagen
        /// </summary>
        public async Task<IEnumerable<ImportVorlage>> GetVorlagenAsync()
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QueryAsync<ImportVorlage>(
                "SELECT * FROM tImportVorlage WHERE cTyp = 'Auftrag' ORDER BY cName");
        }

        /// <summary>
        /// Speichert eine Import-Vorlage
        /// </summary>
        public async Task<int> SaveVorlageAsync(ImportVorlage vorlage)
        {
            var conn = await _db.GetConnectionAsync();
            vorlage.Typ = "Auftrag";
            
            if (vorlage.Id == 0)
            {
                return await conn.QuerySingleAsync<int>(@"
                    INSERT INTO tImportVorlage (cName, cTyp, cTrennzeichen, cDateiendung, nKopfzeile, cFeldzuordnungJson, cStandardwerte)
                    VALUES (@Name, @Typ, @Trennzeichen, @Dateiendung, @HatKopfzeile, @FeldzuordnungJson, @StandardwerteJson);
                    SELECT SCOPE_IDENTITY();", vorlage);
            }
            await conn.ExecuteAsync(@"
                UPDATE tImportVorlage SET cName=@Name, cTrennzeichen=@Trennzeichen, cDateiendung=@Dateiendung,
                    nKopfzeile=@HatKopfzeile, cFeldzuordnungJson=@FeldzuordnungJson, cStandardwerte=@StandardwerteJson
                WHERE kImportVorlage=@Id", vorlage);
            return vorlage.Id;
        }
        #endregion

        #region CSV Import
        /// <summary>
        /// Liest CSV-Header für Feldzuordnung
        /// </summary>
        public async Task<string[]> GetCsvHeaderAsync(string dateiPfad, string trennzeichen = ";")
        {
            using var reader = new StreamReader(dateiPfad, Encoding.UTF8);
            var config = new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = trennzeichen };
            using var csv = new CsvReader(reader, config);
            await csv.ReadAsync();
            csv.ReadHeader();
            return csv.HeaderRecord ?? Array.Empty<string>();
        }

        /// <summary>
        /// Vorschau der Import-Daten
        /// </summary>
        public async Task<ImportVorschau> GetVorschauAsync(string dateiPfad, ImportVorlage vorlage, int maxZeilen = 10)
        {
            var vorschau = new ImportVorschau();
            var config = new CsvConfiguration(CultureInfo.InvariantCulture) 
            { 
                Delimiter = vorlage.Trennzeichen,
                HasHeaderRecord = vorlage.HatKopfzeile
            };

            using var reader = new StreamReader(dateiPfad, Encoding.UTF8);
            using var csv = new CsvReader(reader, config);
            
            if (vorlage.HatKopfzeile)
            {
                await csv.ReadAsync();
                csv.ReadHeader();
                vorschau.Header = csv.HeaderRecord?.ToList() ?? new();
            }

            var feldzuordnung = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
                vorlage.FeldzuordnungJson ?? "{}") ?? new();

            var zeilen = 0;
            while (await csv.ReadAsync() && zeilen < maxZeilen)
            {
                var zeile = new Dictionary<string, string>();
                foreach (var zuordnung in feldzuordnung)
                {
                    var wert = vorlage.HatKopfzeile 
                        ? csv.GetField(zuordnung.Value) 
                        : csv.GetField(int.Parse(zuordnung.Value));
                    zeile[zuordnung.Key] = wert ?? "";
                }
                vorschau.Zeilen.Add(zeile);
                zeilen++;
            }

            // Validierung
            foreach (var zeile in vorschau.Zeilen)
            {
                var fehler = new List<string>();
                foreach (var pflicht in VerfuegbareFelder.Where(f => f.IstPflicht))
                {
                    if (!zeile.ContainsKey(pflicht.FeldName) || string.IsNullOrEmpty(zeile[pflicht.FeldName]))
                        fehler.Add($"Pflichtfeld '{pflicht.Anzeigename}' fehlt");
                }
                if (fehler.Any())
                    vorschau.Warnungen.Add($"Zeile {vorschau.Zeilen.IndexOf(zeile) + 1}: {string.Join(", ", fehler)}");
            }

            return vorschau;
        }

        /// <summary>
        /// Führt den Import durch
        /// </summary>
        public async Task<ImportErgebnis> ImportierenAsync(string dateiPfad, ImportVorlage vorlage, ImportOptionen optionen)
        {
            var ergebnis = new ImportErgebnis();
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = vorlage.Trennzeichen,
                HasHeaderRecord = vorlage.HatKopfzeile
            };

            var feldzuordnung = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
                vorlage.FeldzuordnungJson ?? "{}") ?? new();
            var standardwerte = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
                vorlage.StandardwerteJson ?? "{}") ?? new();

            using var reader = new StreamReader(dateiPfad, Encoding.UTF8);
            using var csv = new CsvReader(reader, config);

            if (vorlage.HatKopfzeile)
            {
                await csv.ReadAsync();
                csv.ReadHeader();
            }

            var conn = await _db.GetConnectionAsync();
            var auftraege = new Dictionary<string, int>(); // BestellNr -> AuftragId

            while (await csv.ReadAsync())
            {
                ergebnis.GesamtZeilen++;
                try
                {
                    var daten = new Dictionary<string, string>();
                    
                    // Standardwerte
                    foreach (var sw in standardwerte)
                        daten[sw.Key] = sw.Value;
                    
                    // CSV-Daten
                    foreach (var zuordnung in feldzuordnung)
                    {
                        var wert = vorlage.HatKopfzeile
                            ? csv.GetField(zuordnung.Value)
                            : csv.GetField(int.Parse(zuordnung.Value));
                        if (!string.IsNullOrEmpty(wert))
                            daten[zuordnung.Key] = wert;
                    }

                    // Auftrag erstellen/finden
                    var bestellNr = daten.GetValueOrDefault("BestellNr") ?? $"IMP-{DateTime.Now:yyyyMMddHHmmss}-{ergebnis.GesamtZeilen}";
                    
                    if (!auftraege.TryGetValue(bestellNr, out var auftragId))
                    {
                        // Kunde finden oder erstellen
                        var kundeId = await FindeOderErstelleKundeAsync(conn, daten, optionen);
                        
                        // Auftrag erstellen
                        auftragId = await conn.QuerySingleAsync<int>(@"
                            INSERT INTO tBestellung (cBestellNr, kKunde, dErstellt, nStatus, cBemerkung, 
                                cLieferFirma, cLieferName, cLieferStrasse, cLieferPLZ, cLieferOrt, cLieferLand,
                                cVersandart, cZahlungsart, cExterneAuftragNr, kQuelle)
                            VALUES (@BestellNr, @KundeId, @Datum, 1, @Bemerkung,
                                @LieferFirma, @LieferName, @LieferStrasse, @LieferPLZ, @LieferOrt, @LieferLand,
                                @Versandart, @Zahlungsart, @ExternNr, @QuelleId);
                            SELECT SCOPE_IDENTITY();", new
                        {
                            BestellNr = bestellNr,
                            KundeId = kundeId,
                            Datum = ParseDatum(daten.GetValueOrDefault("BestellDatum")) ?? DateTime.Now,
                            Bemerkung = daten.GetValueOrDefault("Bemerkung"),
                            LieferFirma = daten.GetValueOrDefault("LieferFirma"),
                            LieferName = daten.GetValueOrDefault("LieferName"),
                            LieferStrasse = daten.GetValueOrDefault("LieferStrasse"),
                            LieferPLZ = daten.GetValueOrDefault("LieferPLZ"),
                            LieferOrt = daten.GetValueOrDefault("LieferOrt"),
                            LieferLand = daten.GetValueOrDefault("LieferLand") ?? "DE",
                            Versandart = daten.GetValueOrDefault("Versandart"),
                            Zahlungsart = daten.GetValueOrDefault("Zahlungsart"),
                            ExternNr = daten.GetValueOrDefault("ExterneAuftragNr"),
                            QuelleId = optionen.QuelleId
                        });
                        
                        auftraege[bestellNr] = auftragId;
                        ergebnis.ErstellteAuftraege++;
                    }

                    // Position hinzufügen
                    var artNr = daten.GetValueOrDefault("ArtNr");
                    if (!string.IsNullOrEmpty(artNr))
                    {
                        var artikel = await conn.QuerySingleOrDefaultAsync<(int Id, string Name, decimal Preis)>(
                            "SELECT kArtikel, cName, fVKBrutto FROM tArtikel WHERE cArtNr = @ArtNr",
                            new { ArtNr = artNr });

                        if (artikel.Id > 0 || optionen.UnbekannteArtikelErstellen)
                        {
                            var artikelId = artikel.Id;
                            var einzelpreis = ParseDecimal(daten.GetValueOrDefault("Einzelpreis")) ?? artikel.Preis;
                            var menge = ParseDecimal(daten.GetValueOrDefault("Menge")) ?? 1;

                            if (artikelId == 0 && optionen.UnbekannteArtikelErstellen)
                            {
                                artikelId = await conn.QuerySingleAsync<int>(@"
                                    INSERT INTO tArtikel (cArtNr, cName, fVKBrutto, nAktiv) VALUES (@ArtNr, @Name, @Preis, 1);
                                    SELECT SCOPE_IDENTITY();",
                                    new { ArtNr = artNr, Name = daten.GetValueOrDefault("ArtikelName") ?? artNr, Preis = einzelpreis });
                            }

                            await conn.ExecuteAsync(@"
                                INSERT INTO tBestellungPos (kBestellung, kArtikel, cArtNr, cName, fMenge, fPreis, fRabatt, cText)
                                VALUES (@AuftragId, @ArtikelId, @ArtNr, @Name, @Menge, @Preis, @Rabatt, @Text)",
                                new
                                {
                                    AuftragId = auftragId,
                                    ArtikelId = artikelId,
                                    ArtNr = artNr,
                                    Name = daten.GetValueOrDefault("ArtikelName") ?? artikel.Name,
                                    Menge = menge,
                                    Preis = einzelpreis,
                                    Rabatt = ParseDecimal(daten.GetValueOrDefault("Rabatt")) ?? 0,
                                    Text = daten.GetValueOrDefault("PosText")
                                });

                            ergebnis.ImportiertePositionen++;
                        }
                        else
                        {
                            ergebnis.Warnungen.Add($"Zeile {ergebnis.GesamtZeilen}: Artikel '{artNr}' nicht gefunden");
                        }
                    }

                    ergebnis.ErfolgreicheZeilen++;
                }
                catch (Exception ex)
                {
                    ergebnis.FehlerhafteZeilen++;
                    ergebnis.Fehler.Add($"Zeile {ergebnis.GesamtZeilen}: {ex.Message}");
                    if (!optionen.FehlerIgnorieren) throw;
                }
            }

            // Auftragssummen aktualisieren
            foreach (var auftragId in auftraege.Values)
            {
                await conn.ExecuteAsync(@"
                    UPDATE tBestellung SET 
                        fNetto = (SELECT SUM(fMenge * fPreis * (1 - fRabatt/100)) FROM tBestellungPos WHERE kBestellung = @Id),
                        fBrutto = (SELECT SUM(fMenge * fPreis * (1 - fRabatt/100) * 1.19) FROM tBestellungPos WHERE kBestellung = @Id)
                    WHERE kBestellung = @Id", new { Id = auftragId });
            }

            _log.Information("Auftragsimport: {Auftraege} Aufträge, {Positionen} Positionen aus {Datei}",
                ergebnis.ErstellteAuftraege, ergebnis.ImportiertePositionen, Path.GetFileName(dateiPfad));

            return ergebnis;
        }

        private async Task<int> FindeOderErstelleKundeAsync(System.Data.IDbConnection conn, Dictionary<string, string> daten, ImportOptionen optionen)
        {
            // Nach Kundennummer suchen
            var kundenNr = daten.GetValueOrDefault("KundenNr");
            if (!string.IsNullOrEmpty(kundenNr))
            {
                var existierend = await conn.QuerySingleOrDefaultAsync<int?>(
                    "SELECT kKunde FROM tKunde WHERE cKundenNr = @Nr", new { Nr = kundenNr });
                if (existierend.HasValue) return existierend.Value;
            }

            // Nach Email suchen
            var email = daten.GetValueOrDefault("KundeEmail");
            if (!string.IsNullOrEmpty(email))
            {
                var existierend = await conn.QuerySingleOrDefaultAsync<int?>(
                    "SELECT kKunde FROM tKunde WHERE cMail = @Email", new { Email = email });
                if (existierend.HasValue) return existierend.Value;
            }

            // Neuen Kunden erstellen
            if (optionen.NeueKundenErstellen)
            {
                return await conn.QuerySingleAsync<int>(@"
                    INSERT INTO tKunde (cKundenNr, cAnrede, cFirma, cVorname, cNachname, cStrasse, cPLZ, cOrt, cLand, cMail, cTel)
                    VALUES (@Nr, @Anrede, @Firma, @Vorname, @Nachname, @Strasse, @PLZ, @Ort, @Land, @Email, @Telefon);
                    SELECT SCOPE_IDENTITY();", new
                {
                    Nr = kundenNr ?? $"K-{DateTime.Now:yyyyMMddHHmmss}",
                    Anrede = daten.GetValueOrDefault("KundeAnrede"),
                    Firma = daten.GetValueOrDefault("KundeFirma"),
                    Vorname = daten.GetValueOrDefault("KundeVorname"),
                    Nachname = daten.GetValueOrDefault("KundeNachname") ?? "Import",
                    Strasse = daten.GetValueOrDefault("KundeStrasse"),
                    PLZ = daten.GetValueOrDefault("KundePLZ"),
                    Ort = daten.GetValueOrDefault("KundeOrt"),
                    Land = daten.GetValueOrDefault("KundeLand") ?? "DE",
                    Email = email,
                    Telefon = daten.GetValueOrDefault("KundeTelefon")
                });
            }

            throw new Exception("Kunde nicht gefunden und Erstellung deaktiviert");
        }

        private DateTime? ParseDatum(string? wert)
        {
            if (string.IsNullOrEmpty(wert)) return null;
            if (DateTime.TryParse(wert, out var dt)) return dt;
            if (DateTime.TryParseExact(wert, new[] { "dd.MM.yyyy", "yyyy-MM-dd", "dd/MM/yyyy" }, 
                CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) return dt;
            return null;
        }

        private decimal? ParseDecimal(string? wert)
        {
            if (string.IsNullOrEmpty(wert)) return null;
            wert = wert.Replace(",", ".");
            return decimal.TryParse(wert, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
        }
        #endregion
    }

    #region DTOs
    public class ImportFeldDefinition
    {
        public string FeldName { get; set; }
        public string Anzeigename { get; set; }
        public string Gruppe { get; set; }
        public bool IstPflicht { get; set; }
        public ImportFeldDefinition(string name, string anzeige, string gruppe, bool pflicht = false)
        { FeldName = name; Anzeigename = anzeige; Gruppe = gruppe; IstPflicht = pflicht; }
    }

    public class AuftragsImportVorlage
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Typ { get; set; } = "Auftrag";
        public string Trennzeichen { get; set; } = ";";
        public string Dateiendung { get; set; } = "csv";
        public bool HatKopfzeile { get; set; } = true;
        public string? FeldzuordnungJson { get; set; }
        public string? StandardwerteJson { get; set; }
    }

    public class AuftragsImportVorschau
    {
        public List<string> Header { get; set; } = new();
        public List<Dictionary<string, string>> Zeilen { get; set; } = new();
        public List<string> Warnungen { get; set; } = new();
    }

    public class AuftragsImportOptionen
    {
        public bool NeueKundenErstellen { get; set; } = true;
        public bool UnbekannteArtikelErstellen { get; set; } = false;
        public bool FehlerIgnorieren { get; set; } = false;
        public int? QuelleId { get; set; }
    }

    public class AuftragsImportErgebnis
    {
        public int GesamtZeilen { get; set; }
        public int ErfolgreicheZeilen { get; set; }
        public int FehlerhafteZeilen { get; set; }
        public int ErstellteAuftraege { get; set; }
        public int ImportiertePositionen { get; set; }
        public List<string> Fehler { get; set; } = new();
        public List<string> Warnungen { get; set; } = new();
    }
    #endregion
}

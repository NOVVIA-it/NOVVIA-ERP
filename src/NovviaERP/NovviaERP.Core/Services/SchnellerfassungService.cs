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
    /// Schnellerfassung und Auftragsimport (wie VARIO 8)
    /// CSV/Excel Import mit flexibler Feldzuordnung
    /// </summary>
    public class SchnellerfassungService
    {
        private readonly JtlDbContext _db;
        private static readonly ILogger _log = Log.ForContext<SchnellerfassungService>();

        public SchnellerfassungService(JtlDbContext db) => _db = db;

        #region CSV-Import mit Feldzuordnung
        /// <summary>
        /// Liest CSV-Header für Feldzuordnung
        /// </summary>
        public async Task<CsvVorschau> LeseCsvVorschauAsync(Stream csvStream, string trennzeichen = ";", Encoding? encoding = null)
        {
            encoding ??= Encoding.UTF8;
            using var reader = new StreamReader(csvStream, encoding);
            
            var config = new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = trennzeichen, HasHeaderRecord = true };
            using var csv = new CsvReader(reader, config);
            
            csv.Read();
            csv.ReadHeader();
            
            var vorschau = new CsvVorschau
            {
                Spalten = csv.HeaderRecord?.ToList() ?? new List<string>(),
                Trennzeichen = trennzeichen
            };

            // Erste 5 Zeilen als Vorschau
            var zeilen = 0;
            while (csv.Read() && zeilen < 5)
            {
                var zeile = new Dictionary<string, string>();
                foreach (var spalte in vorschau.Spalten)
                {
                    zeile[spalte] = csv.GetField(spalte) ?? "";
                }
                vorschau.VorschauZeilen.Add(zeile);
                zeilen++;
            }

            return vorschau;
        }

        /// <summary>
        /// Importiert Aufträge aus CSV mit definierter Feldzuordnung
        /// </summary>
        public async Task<ImportErgebnis> ImportiereAuftraegeAsync(Stream csvStream, FeldzuordnungConfig config)
        {
            var ergebnis = new ImportErgebnis();
            var conn = await _db.GetConnectionAsync();

            using var reader = new StreamReader(csvStream, config.Encoding ?? Encoding.UTF8);
            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture) 
            { 
                Delimiter = config.Trennzeichen, 
                HasHeaderRecord = true,
                MissingFieldFound = null
            };
            using var csv = new CsvReader(reader, csvConfig);

            csv.Read();
            csv.ReadHeader();

            while (csv.Read())
            {
                ergebnis.GesamtZeilen++;
                try
                {
                    var auftrag = await ErstelleAuftragAusCsvAsync(csv, config, conn);
                    if (auftrag != null)
                    {
                        ergebnis.Erfolgreich++;
                        ergebnis.ImportierteIds.Add(auftrag.Id);
                    }
                }
                catch (Exception ex)
                {
                    ergebnis.Fehler.Add($"Zeile {ergebnis.GesamtZeilen}: {ex.Message}");
                    ergebnis.Fehlgeschlagen++;
                }
            }

            _log.Information("Auftragsimport: {Erfolg}/{Gesamt} importiert", ergebnis.Erfolgreich, ergebnis.GesamtZeilen);
            return ergebnis;
        }

        private async Task<Bestellung?> ErstelleAuftragAusCsvAsync(CsvReader csv, FeldzuordnungConfig config, System.Data.IDbConnection conn)
        {
            // Kunde ermitteln oder anlegen
            var kundenNr = GetMappedValue(csv, config, "KundenNr");
            var email = GetMappedValue(csv, config, "Email");
            
            int kundeId;
            if (!string.IsNullOrEmpty(kundenNr))
            {
                kundeId = await conn.QuerySingleOrDefaultAsync<int>(
                    "SELECT kKunde FROM tKunde WHERE cKundenNr = @Nr", new { Nr = kundenNr });
            }
            else if (!string.IsNullOrEmpty(email))
            {
                kundeId = await conn.QuerySingleOrDefaultAsync<int>(
                    "SELECT kKunde FROM tKunde WHERE cMail = @Mail", new { Mail = email });
            }
            else
            {
                kundeId = 0;
            }

            // Neuen Kunden anlegen wenn nicht gefunden
            if (kundeId == 0 && config.KundenAnlegen)
            {
                kundeId = await conn.QuerySingleAsync<int>(@"
                    INSERT INTO tKunde (cKundenNr, cVorname, cNachname, cFirma, cStrasse, cPLZ, cOrt, cLand, cMail, cTel, dErstellt)
                    VALUES (@Nr, @Vorname, @Nachname, @Firma, @Strasse, @PLZ, @Ort, @Land, @Mail, @Tel, GETDATE());
                    SELECT SCOPE_IDENTITY();",
                    new {
                        Nr = kundenNr ?? await GeneriereKundenNrAsync(conn),
                        Vorname = GetMappedValue(csv, config, "Vorname"),
                        Nachname = GetMappedValue(csv, config, "Nachname"),
                        Firma = GetMappedValue(csv, config, "Firma"),
                        Strasse = GetMappedValue(csv, config, "Strasse"),
                        PLZ = GetMappedValue(csv, config, "PLZ"),
                        Ort = GetMappedValue(csv, config, "Ort"),
                        Land = GetMappedValue(csv, config, "Land") ?? "DE",
                        Mail = email,
                        Tel = GetMappedValue(csv, config, "Telefon")
                    });
            }

            if (kundeId == 0) throw new Exception("Kunde nicht gefunden und Anlegen deaktiviert");

            // Auftrag erstellen
            var bestellNr = GetMappedValue(csv, config, "BestellNr") ?? await GeneriereBestellNrAsync(conn);
            var bestellungId = await conn.QuerySingleAsync<int>(@"
                INSERT INTO tBestellung (cBestellNr, kKunde, cExterneBestellNr, dErstellt, dLiefertermin, 
                    cLieferStrasse, cLieferPLZ, cLieferOrt, cLieferLand, cKommentar, nStatus)
                VALUES (@Nr, @KundeId, @ExternNr, GETDATE(), @Liefertermin, @LStrasse, @LPLZ, @LOrt, @LLand, @Kommentar, 1);
                SELECT SCOPE_IDENTITY();",
                new {
                    Nr = bestellNr,
                    KundeId = kundeId,
                    ExternNr = GetMappedValue(csv, config, "ExterneBestellNr"),
                    Liefertermin = ParseDate(GetMappedValue(csv, config, "Liefertermin")),
                    LStrasse = GetMappedValue(csv, config, "LieferStrasse") ?? GetMappedValue(csv, config, "Strasse"),
                    LPLZ = GetMappedValue(csv, config, "LieferPLZ") ?? GetMappedValue(csv, config, "PLZ"),
                    LOrt = GetMappedValue(csv, config, "LieferOrt") ?? GetMappedValue(csv, config, "Ort"),
                    LLand = GetMappedValue(csv, config, "LieferLand") ?? GetMappedValue(csv, config, "Land") ?? "DE",
                    Kommentar = GetMappedValue(csv, config, "Kommentar")
                });

            // Positionen importieren (wenn Artikel in gleicher Zeile)
            var artNr = GetMappedValue(csv, config, "ArtikelNr");
            if (!string.IsNullOrEmpty(artNr))
            {
                await ImportierePositionAsync(conn, bestellungId, csv, config);
            }

            return new Bestellung { Id = bestellungId, BestellNr = bestellNr };
        }

        private async Task ImportierePositionAsync(System.Data.IDbConnection conn, int bestellungId, CsvReader csv, FeldzuordnungConfig config)
        {
            var artNr = GetMappedValue(csv, config, "ArtikelNr");
            var artikel = await conn.QuerySingleOrDefaultAsync<(int Id, decimal VKBrutto)>(
                "SELECT kArtikel, fVKBrutto FROM tArtikel WHERE cArtNr = @Nr OR cBarcode = @Nr OR cEAN = @Nr",
                new { Nr = artNr });

            if (artikel.Id == 0) throw new Exception($"Artikel {artNr} nicht gefunden");

            var menge = ParseDecimal(GetMappedValue(csv, config, "Menge")) ?? 1;
            var preis = ParseDecimal(GetMappedValue(csv, config, "Einzelpreis")) ?? artikel.VKBrutto;

            await conn.ExecuteAsync(@"
                INSERT INTO tBestellungPos (kBestellung, kArtikel, cArtNr, fMenge, fPreis, fRabatt)
                VALUES (@BestId, @ArtId, @ArtNr, @Menge, @Preis, @Rabatt)",
                new { 
                    BestId = bestellungId, 
                    ArtId = artikel.Id, 
                    ArtNr = artNr, 
                    Menge = menge, 
                    Preis = preis,
                    Rabatt = ParseDecimal(GetMappedValue(csv, config, "Rabatt")) ?? 0
                });
        }

        private string? GetMappedValue(CsvReader csv, FeldzuordnungConfig config, string zielFeld)
        {
            if (!config.Zuordnungen.TryGetValue(zielFeld, out var quellSpalte)) return null;
            if (string.IsNullOrEmpty(quellSpalte)) return null;
            return csv.GetField(quellSpalte);
        }

        private async Task<string> GeneriereKundenNrAsync(System.Data.IDbConnection conn)
        {
            var max = await conn.QuerySingleAsync<int>("SELECT ISNULL(MAX(CAST(cKundenNr AS INT)), 10000) FROM tKunde WHERE ISNUMERIC(cKundenNr) = 1");
            return (max + 1).ToString();
        }

        private async Task<string> GeneriereBestellNrAsync(System.Data.IDbConnection conn)
        {
            var prefix = DateTime.Now.ToString("yyMM");
            var max = await conn.QuerySingleAsync<int>(
                "SELECT ISNULL(MAX(CAST(RIGHT(cBestellNr, 4) AS INT)), 0) FROM tBestellung WHERE cBestellNr LIKE @Prefix + '%'",
                new { Prefix = prefix });
            return $"{prefix}{(max + 1):D4}";
        }

        private DateTime? ParseDate(string? value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            if (DateTime.TryParse(value, out var dt)) return dt;
            if (DateTime.TryParseExact(value, new[] { "dd.MM.yyyy", "yyyy-MM-dd", "dd/MM/yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) return dt;
            return null;
        }

        private decimal? ParseDecimal(string? value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            value = value.Replace(",", ".");
            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) return d;
            return null;
        }
        #endregion

        #region Schnellerfassung (manuelle Eingabe)
        /// <summary>
        /// Erstellt Auftrag über Schnellerfassung
        /// </summary>
        public async Task<int> SchnellerfassungAuftragAsync(SchnellerfassungDaten daten)
        {
            var conn = await _db.GetConnectionAsync();
            using var tx = conn.BeginTransaction();

            try
            {
                // Kunde suchen oder anlegen
                int kundeId = 0;
                if (!string.IsNullOrEmpty(daten.KundenNr))
                {
                    kundeId = await conn.QuerySingleOrDefaultAsync<int>(
                        "SELECT kKunde FROM tKunde WHERE cKundenNr = @Nr", new { Nr = daten.KundenNr }, tx);
                }
                
                if (kundeId == 0 && !string.IsNullOrEmpty(daten.Email))
                {
                    kundeId = await conn.QuerySingleOrDefaultAsync<int>(
                        "SELECT kKunde FROM tKunde WHERE cMail = @Mail", new { Mail = daten.Email }, tx);
                }

                if (kundeId == 0)
                {
                    kundeId = await conn.QuerySingleAsync<int>(@"
                        INSERT INTO tKunde (cKundenNr, cAnrede, cVorname, cNachname, cFirma, cStrasse, cPLZ, cOrt, cLand, cMail, cTel, dErstellt)
                        VALUES (@KundenNr, @Anrede, @Vorname, @Nachname, @Firma, @Strasse, @PLZ, @Ort, @Land, @Email, @Telefon, GETDATE());
                        SELECT SCOPE_IDENTITY();", daten, tx);
                }

                // Auftrag erstellen
                var bestellNr = await GeneriereBestellNrAsync(conn);
                var bestellungId = await conn.QuerySingleAsync<int>(@"
                    INSERT INTO tBestellung (cBestellNr, kKunde, dErstellt, cLieferStrasse, cLieferPLZ, cLieferOrt, cLieferLand, cKommentar, nStatus)
                    VALUES (@Nr, @KundeId, GETDATE(), @LStrasse, @LPLZ, @LOrt, @LLand, @Kommentar, 1);
                    SELECT SCOPE_IDENTITY();",
                    new { Nr = bestellNr, KundeId = kundeId, 
                          LStrasse = daten.LieferStrasse ?? daten.Strasse,
                          LPLZ = daten.LieferPLZ ?? daten.PLZ,
                          LOrt = daten.LieferOrt ?? daten.Ort,
                          LLand = daten.LieferLand ?? daten.Land ?? "DE",
                          Kommentar = daten.Kommentar }, tx);

                // Positionen
                foreach (var pos in daten.Positionen)
                {
                    var artikel = await conn.QuerySingleOrDefaultAsync<(int Id, string Name, decimal VKBrutto)>(
                        "SELECT kArtikel, cName, fVKBrutto FROM tArtikel WHERE cArtNr = @Nr OR cBarcode = @Nr",
                        new { Nr = pos.ArtNr }, tx);

                    if (artikel.Id == 0) throw new Exception($"Artikel {pos.ArtNr} nicht gefunden");

                    await conn.ExecuteAsync(@"
                        INSERT INTO tBestellungPos (kBestellung, kArtikel, cArtNr, cName, fMenge, fPreis, fRabatt)
                        VALUES (@BestId, @ArtId, @ArtNr, @Name, @Menge, @Preis, @Rabatt)",
                        new { BestId = bestellungId, ArtId = artikel.Id, ArtNr = pos.ArtNr, 
                              Name = artikel.Name, Menge = pos.Menge, Preis = pos.Preis ?? artikel.VKBrutto, Rabatt = pos.Rabatt }, tx);
                }

                tx.Commit();
                _log.Information("Schnellerfassung: Auftrag {Nr} erstellt", bestellNr);
                return bestellungId;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
        #endregion

        #region Feldzuordnungs-Vorlagen
        /// <summary>
        /// Holt gespeicherte Feldzuordnungs-Vorlagen
        /// </summary>
        public async Task<IEnumerable<FeldzuordnungVorlage>> GetVorlagenAsync()
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QueryAsync<FeldzuordnungVorlage>("SELECT * FROM tImportVorlage ORDER BY cName");
        }

        /// <summary>
        /// Speichert eine Feldzuordnungs-Vorlage
        /// </summary>
        public async Task<int> SaveVorlageAsync(FeldzuordnungVorlage vorlage)
        {
            var conn = await _db.GetConnectionAsync();
            vorlage.ZuordnungenJson = System.Text.Json.JsonSerializer.Serialize(vorlage.Zuordnungen);
            
            if (vorlage.Id == 0)
            {
                return await conn.QuerySingleAsync<int>(@"
                    INSERT INTO tImportVorlage (cName, cBeschreibung, cTrennzeichen, cZuordnungen, nKundenAnlegen)
                    VALUES (@Name, @Beschreibung, @Trennzeichen, @ZuordnungenJson, @KundenAnlegen);
                    SELECT SCOPE_IDENTITY();", vorlage);
            }

            await conn.ExecuteAsync(@"
                UPDATE tImportVorlage SET cName=@Name, cBeschreibung=@Beschreibung, cTrennzeichen=@Trennzeichen,
                    cZuordnungen=@ZuordnungenJson, nKundenAnlegen=@KundenAnlegen WHERE kImportVorlage=@Id", vorlage);
            return vorlage.Id;
        }

        /// <summary>
        /// Verfügbare Zielfelder für Auftragsimport
        /// </summary>
        public static List<ZielFeldInfo> GetVerfuegbareZielfelder() => new()
        {
            // Kunde
            new("KundenNr", "Kundennummer", "Kunde"),
            new("Anrede", "Anrede", "Kunde"),
            new("Vorname", "Vorname", "Kunde"),
            new("Nachname", "Nachname", "Kunde"),
            new("Firma", "Firma", "Kunde"),
            new("Strasse", "Straße", "Kunde"),
            new("PLZ", "PLZ", "Kunde"),
            new("Ort", "Ort", "Kunde"),
            new("Land", "Land (ISO)", "Kunde"),
            new("Email", "E-Mail", "Kunde"),
            new("Telefon", "Telefon", "Kunde"),
            // Lieferadresse
            new("LieferStrasse", "Lieferstraße", "Lieferadresse"),
            new("LieferPLZ", "Liefer-PLZ", "Lieferadresse"),
            new("LieferOrt", "Lieferort", "Lieferadresse"),
            new("LieferLand", "Lieferland", "Lieferadresse"),
            // Auftrag
            new("BestellNr", "Bestellnummer", "Auftrag"),
            new("ExterneBestellNr", "Externe Bestellnr.", "Auftrag"),
            new("Liefertermin", "Liefertermin", "Auftrag"),
            new("Kommentar", "Kommentar", "Auftrag"),
            // Artikel/Position
            new("ArtikelNr", "Artikelnummer", "Position"),
            new("Menge", "Menge", "Position"),
            new("Einzelpreis", "Einzelpreis", "Position"),
            new("Rabatt", "Rabatt %", "Position")
        };
        #endregion
    }

    #region DTOs
    public class CsvVorschau
    {
        public List<string> Spalten { get; set; } = new();
        public List<Dictionary<string, string>> VorschauZeilen { get; set; } = new();
        public string Trennzeichen { get; set; } = ";";
    }

    public class FeldzuordnungConfig
    {
        public string Trennzeichen { get; set; } = ";";
        public Encoding? Encoding { get; set; }
        public Dictionary<string, string> Zuordnungen { get; set; } = new(); // ZielFeld -> QuellSpalte
        public bool KundenAnlegen { get; set; } = true;
    }

    public class FeldzuordnungVorlage
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string? Beschreibung { get; set; }
        public string Trennzeichen { get; set; } = ";";
        public Dictionary<string, string> Zuordnungen { get; set; } = new();
        public string? ZuordnungenJson { get; set; }
        public bool KundenAnlegen { get; set; } = true;
    }

    public class ZielFeldInfo
    {
        public string Feld { get; set; }
        public string Bezeichnung { get; set; }
        public string Gruppe { get; set; }
        public ZielFeldInfo(string feld, string bezeichnung, string gruppe) { Feld = feld; Bezeichnung = bezeichnung; Gruppe = gruppe; }
    }

    public class SchnellerfassungDaten
    {
        public string? KundenNr { get; set; }
        public string? Anrede { get; set; }
        public string? Vorname { get; set; }
        public string? Nachname { get; set; }
        public string? Firma { get; set; }
        public string? Strasse { get; set; }
        public string? PLZ { get; set; }
        public string? Ort { get; set; }
        public string? Land { get; set; }
        public string? Email { get; set; }
        public string? Telefon { get; set; }
        public string? LieferStrasse { get; set; }
        public string? LieferPLZ { get; set; }
        public string? LieferOrt { get; set; }
        public string? LieferLand { get; set; }
        public string? Kommentar { get; set; }
        public List<SchnellerfassungPosition> Positionen { get; set; } = new();
    }

    public class SchnellerfassungPosition
    {
        public string ArtNr { get; set; } = "";
        public decimal Menge { get; set; } = 1;
        public decimal? Preis { get; set; }
        public decimal Rabatt { get; set; }
    }

    public class SchnellerfassungImportErgebnis
    {
        public int GesamtZeilen { get; set; }
        public int Erfolgreich { get; set; }
        public int Fehlgeschlagen { get; set; }
        public List<int> ImportierteIds { get; set; } = new();
        public List<string> Fehler { get; set; } = new();
    }
    #endregion
}

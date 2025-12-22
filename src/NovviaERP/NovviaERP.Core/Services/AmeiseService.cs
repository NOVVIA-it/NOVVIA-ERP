using System;
using System.Collections.Generic;
using System.Data;
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
    /// Import/Export Service (ähnlich JTL-Ameise)
    /// Unterstützt CSV, Excel und direkten DB-Import
    /// </summary>
    public class AmeiseService
    {
        private readonly JtlDbContext _db;
        private static readonly ILogger _log = Log.ForContext<AmeiseService>();

        public AmeiseService(JtlDbContext db) => _db = db;

        #region Import-Vorlagen
        /// <summary>
        /// Verfügbare Import-Vorlagen
        /// </summary>
        public static readonly Dictionary<string, AmeiseImportVorlage> ImportVorlagen = new()
        {
            ["Artikel"] = new AmeiseImportVorlage
            {
                Name = "Artikel",
                Tabelle = "tArtikel",
                KeySpalte = "cArtNr",
                Spalten = new()
                {
                    { "ArtNr", new SpalteDef("cArtNr", typeof(string), true) },
                    { "Name", new SpalteDef("cName", typeof(string), true) },
                    { "Beschreibung", new SpalteDef("cBeschreibung", typeof(string)) },
                    { "VKBrutto", new SpalteDef("fVKBrutto", typeof(decimal)) },
                    { "VKNetto", new SpalteDef("fVKNetto", typeof(decimal)) },
                    { "EKNetto", new SpalteDef("fEKNetto", typeof(decimal)) },
                    { "Barcode", new SpalteDef("cBarcode", typeof(string)) },
                    { "EAN", new SpalteDef("cEAN", typeof(string)) },
                    { "Gewicht", new SpalteDef("fGewicht", typeof(decimal)) },
                    { "Lagerbestand", new SpalteDef("fLagerbestand", typeof(decimal)) },
                    { "Mindestbestand", new SpalteDef("fMindestbestand", typeof(decimal)) },
                    { "Hersteller", new SpalteDef("kHersteller", typeof(int), false, "tHersteller", "cName") },
                    { "Kategorie", new SpalteDef("kKategorie", typeof(int), false, "tKategorieSprache", "cName") },
                    { "Steuerklasse", new SpalteDef("kSteuerKlasse", typeof(int)) },
                    { "Aktiv", new SpalteDef("nAktiv", typeof(bool)) }
                }
            },
            ["Kunden"] = new AmeiseImportVorlage
            {
                Name = "Kunden",
                Tabelle = "tKunde",
                KeySpalte = "cKundenNr",
                Spalten = new()
                {
                    { "KundenNr", new SpalteDef("cKundenNr", typeof(string), true) },
                    { "Anrede", new SpalteDef("cAnrede", typeof(string)) },
                    { "Firma", new SpalteDef("cFirma", typeof(string)) },
                    { "Vorname", new SpalteDef("cVorname", typeof(string)) },
                    { "Nachname", new SpalteDef("cNachname", typeof(string), true) },
                    { "Strasse", new SpalteDef("cStrasse", typeof(string)) },
                    { "PLZ", new SpalteDef("cPLZ", typeof(string)) },
                    { "Ort", new SpalteDef("cOrt", typeof(string)) },
                    { "Land", new SpalteDef("cLand", typeof(string)) },
                    { "Email", new SpalteDef("cMail", typeof(string)) },
                    { "Telefon", new SpalteDef("cTel", typeof(string)) },
                    { "UStID", new SpalteDef("cUStID", typeof(string)) },
                    { "Kundengruppe", new SpalteDef("kKundenGruppe", typeof(int), false, "tKundenGruppe", "cName") },
                    { "Rabatt", new SpalteDef("fRabatt", typeof(decimal)) }
                }
            },
            ["Preise"] = new AmeiseImportVorlage
            {
                Name = "Preise",
                Tabelle = "tArtikel",
                KeySpalte = "cArtNr",
                Spalten = new()
                {
                    { "ArtNr", new SpalteDef("cArtNr", typeof(string), true) },
                    { "VKBrutto", new SpalteDef("fVKBrutto", typeof(decimal)) },
                    { "VKNetto", new SpalteDef("fVKNetto", typeof(decimal)) },
                    { "EKNetto", new SpalteDef("fEKNetto", typeof(decimal)) },
                    { "UVP", new SpalteDef("fUVP", typeof(decimal)) }
                }
            },
            ["Bestaende"] = new AmeiseImportVorlage
            {
                Name = "Bestände",
                Tabelle = "tLagerbestand",
                KeySpalte = "cArtNr",
                Spalten = new()
                {
                    { "ArtNr", new SpalteDef("kArtikel", typeof(int), true, "tArtikel", "cArtNr") },
                    { "Lager", new SpalteDef("kWarenLager", typeof(int), false, "tWarenLager", "cName") },
                    { "Bestand", new SpalteDef("fBestand", typeof(decimal), true) }
                }
            },
            ["Lieferanten"] = new AmeiseImportVorlage
            {
                Name = "Lieferanten",
                Tabelle = "tLieferant",
                KeySpalte = "cFirma",
                Spalten = new()
                {
                    { "Firma", new SpalteDef("cFirma", typeof(string), true) },
                    { "Ansprechpartner", new SpalteDef("cAnsprechpartner", typeof(string)) },
                    { "Strasse", new SpalteDef("cStrasse", typeof(string)) },
                    { "PLZ", new SpalteDef("cPLZ", typeof(string)) },
                    { "Ort", new SpalteDef("cOrt", typeof(string)) },
                    { "Land", new SpalteDef("cLand", typeof(string)) },
                    { "Email", new SpalteDef("cMail", typeof(string)) },
                    { "Telefon", new SpalteDef("cTel", typeof(string)) },
                    { "Kundennummer", new SpalteDef("cKundennummer", typeof(string)) },
                    { "Lieferzeit", new SpalteDef("nLieferzeitTage", typeof(int)) }
                }
            }
        };
        #endregion

        #region CSV Import
        /// <summary>
        /// Importiert eine CSV-Datei
        /// </summary>
        public async Task<AmeiseImportErgebnis> ImportCsvAsync(string vorlage, Stream csvStream, AmeiseImportOptionen? optionen = null)
        {
            optionen ??= new AmeiseImportOptionen();
            var ergebnis = new AmeiseImportErgebnis { Vorlage = vorlage };

            if (!ImportVorlagen.TryGetValue(vorlage, out var def))
            {
                ergebnis.Fehler.Add($"Unbekannte Import-Vorlage: {vorlage}");
                return ergebnis;
            }

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = optionen.Trennzeichen,
                HasHeaderRecord = true,
                MissingFieldFound = null,
                BadDataFound = null
            };

            using var reader = new StreamReader(csvStream, Encoding.UTF8);
            using var csv = new CsvReader(reader, config);

            var records = new List<Dictionary<string, string>>();
            csv.Read();
            csv.ReadHeader();
            var headers = csv.HeaderRecord ?? Array.Empty<string>();

            while (csv.Read())
            {
                var record = new Dictionary<string, string>();
                foreach (var header in headers)
                {
                    record[header] = csv.GetField(header) ?? "";
                }
                records.Add(record);
            }

            ergebnis.GesamtZeilen = records.Count;
            _log.Information("CSV Import gestartet: {Vorlage}, {Zeilen} Zeilen", vorlage, records.Count);

            var conn = await _db.GetConnectionAsync();
            using var tx = optionen.Transaktion ? conn.BeginTransaction() : null;

            try
            {
                foreach (var record in records)
                {
                    try
                    {
                        await ImportZeileAsync(conn, def, record, optionen, tx);
                        ergebnis.Erfolgreich++;
                    }
                    catch (Exception ex)
                    {
                        ergebnis.Fehlgeschlagen++;
                        ergebnis.Fehler.Add($"Zeile {ergebnis.Erfolgreich + ergebnis.Fehlgeschlagen}: {ex.Message}");
                        
                        if (!optionen.FehlerIgnorieren)
                        {
                            tx?.Rollback();
                            throw;
                        }
                    }
                }

                tx?.Commit();
            }
            catch
            {
                tx?.Rollback();
                throw;
            }

            _log.Information("CSV Import abgeschlossen: {Erfolg}/{Gesamt}", ergebnis.Erfolgreich, ergebnis.GesamtZeilen);
            return ergebnis;
        }

        private async Task ImportZeileAsync(System.Data.IDbConnection conn, AmeiseImportVorlage vorlage,
            Dictionary<string, string> daten, AmeiseImportOptionen optionen, System.Data.IDbTransaction? tx)
        {
            // Key-Wert ermitteln
            var keyDef = vorlage.Spalten.First(s => s.Value.IstKey);
            if (!daten.TryGetValue(keyDef.Key, out var keyWert) || string.IsNullOrEmpty(keyWert))
                throw new Exception($"Key-Spalte '{keyDef.Key}' fehlt oder ist leer");

            // Prüfen ob Datensatz existiert
            var existiert = await conn.QuerySingleAsync<int>(
                $"SELECT COUNT(*) FROM {vorlage.Tabelle} WHERE {vorlage.KeySpalte} = @Key",
                new { Key = keyWert }, tx);

            // Spalten-Mapping
            var sqlParams = new DynamicParameters();
            var setSpalten = new List<string>();

            foreach (var (csvSpalte, def) in vorlage.Spalten)
            {
                if (!daten.TryGetValue(csvSpalte, out var wert)) continue;
                if (string.IsNullOrEmpty(wert) && !def.IstPflicht) continue;

                // Fremdschlüssel auflösen
                object? dbWert = wert;
                if (!string.IsNullOrEmpty(def.FKTabelle))
                {
                    dbWert = await conn.QuerySingleOrDefaultAsync<int?>(
                        $"SELECT {def.DBSpalte.Replace("k", "k")} FROM {def.FKTabelle} WHERE {def.FKSpalte} = @Wert",
                        new { Wert = wert }, tx);
                    if (dbWert == null && def.IstPflicht)
                        throw new Exception($"FK nicht gefunden: {csvSpalte}='{wert}'");
                }
                else
                {
                    // Typ-Konvertierung
                    dbWert = def.Typ.Name switch
                    {
                        "Decimal" => decimal.TryParse(wert.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0m,
                        "Int32" => int.TryParse(wert, out var i) ? i : 0,
                        "Boolean" => wert == "1" || wert.ToLower() == "ja" || wert.ToLower() == "true",
                        _ => wert
                    };
                }

                sqlParams.Add(def.DBSpalte, dbWert);
                setSpalten.Add(def.DBSpalte);
            }

            if (existiert > 0 && optionen.UpdateExistierend)
            {
                // UPDATE
                var updateSql = $"UPDATE {vorlage.Tabelle} SET {string.Join(", ", setSpalten.Select(s => $"{s} = @{s}"))} WHERE {vorlage.KeySpalte} = @{vorlage.KeySpalte}";
                await conn.ExecuteAsync(updateSql, sqlParams, tx);
            }
            else if (existiert == 0)
            {
                // INSERT
                var insertSql = $"INSERT INTO {vorlage.Tabelle} ({string.Join(", ", setSpalten)}) VALUES ({string.Join(", ", setSpalten.Select(s => $"@{s}"))})";
                await conn.ExecuteAsync(insertSql, sqlParams, tx);
            }
        }
        #endregion

        #region CSV Export
        /// <summary>
        /// Exportiert Daten als CSV
        /// </summary>
        public async Task<byte[]> ExportCsvAsync(string vorlage, ExportOptionen? optionen = null)
        {
            optionen ??= new ExportOptionen();

            if (!ImportVorlagen.TryGetValue(vorlage, out var def))
                throw new ArgumentException($"Unbekannte Export-Vorlage: {vorlage}");

            var conn = await _db.GetConnectionAsync();
            var spalten = def.Spalten.Keys.ToList();
            var dbSpalten = def.Spalten.Values.Select(s => s.DBSpalte).ToList();

            // SQL mit Joins für FK-Auflösung generieren
            var sql = $"SELECT {string.Join(", ", dbSpalten)} FROM {def.Tabelle}";
            if (!string.IsNullOrEmpty(optionen.Filter))
                sql += $" WHERE {optionen.Filter}";
            if (!string.IsNullOrEmpty(optionen.Sortierung))
                sql += $" ORDER BY {optionen.Sortierung}";
            if (optionen.Limit > 0)
                sql += $" LIMIT {optionen.Limit}";

            var daten = await conn.QueryAsync(sql);

            using var ms = new MemoryStream();
            using var writer = new StreamWriter(ms, Encoding.UTF8);
            using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = optionen.Trennzeichen
            });

            // Header
            foreach (var spalte in spalten)
                csv.WriteField(spalte);
            csv.NextRecord();

            // Daten
            foreach (IDictionary<string, object> row in daten)
            {
                foreach (var dbSpalte in dbSpalten)
                {
                    var wert = row.TryGetValue(dbSpalte, out var v) ? v : null;
                    csv.WriteField(wert?.ToString() ?? "");
                }
                csv.NextRecord();
            }

            writer.Flush();
            return ms.ToArray();
        }
        #endregion

        #region Spezial-Importe
        /// <summary>
        /// Importiert Artikelbilder aus Verzeichnis
        /// </summary>
        public async Task<int> ImportArtikelBilderAsync(string verzeichnis)
        {
            var conn = await _db.GetConnectionAsync();
            var importiert = 0;
            var dateien = Directory.GetFiles(verzeichnis, "*.*")
                .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || 
                           f.EndsWith(".png", StringComparison.OrdinalIgnoreCase));

            foreach (var datei in dateien)
            {
                // Dateiname = ArtNr
                var artNr = Path.GetFileNameWithoutExtension(datei);
                var artikelId = await conn.QuerySingleOrDefaultAsync<int?>(
                    "SELECT kArtikel FROM tArtikel WHERE cArtNr = @ArtNr", new { ArtNr = artNr });

                if (artikelId.HasValue)
                {
                    var bytes = await File.ReadAllBytesAsync(datei);
                    var ziel = Path.Combine("Bilder", "Artikel", $"{artikelId}.jpg");
                    await File.WriteAllBytesAsync(ziel, bytes);
                    
                    await conn.ExecuteAsync(
                        "UPDATE tArtikel SET cBildPfad = @Pfad WHERE kArtikel = @Id",
                        new { Pfad = ziel, Id = artikelId });
                    importiert++;
                }
            }

            return importiert;
        }

        /// <summary>
        /// Massenupdate für ein Feld
        /// </summary>
        public async Task<int> MassenUpdateAsync(string tabelle, string spalte, object wert, string whereBedingung)
        {
            var conn = await _db.GetConnectionAsync();
            var sql = $"UPDATE {tabelle} SET {spalte} = @Wert WHERE {whereBedingung}";
            return await conn.ExecuteAsync(sql, new { Wert = wert });
        }
        #endregion
    }

    #region DTOs
    public class AmeiseImportVorlage
    {
        public string Name { get; set; } = "";
        public string Tabelle { get; set; } = "";
        public string KeySpalte { get; set; } = "";
        public Dictionary<string, SpalteDef> Spalten { get; set; } = new();
    }

    public class SpalteDef
    {
        public string DBSpalte { get; set; }
        public Type Typ { get; set; }
        public bool IstPflicht { get; set; }
        public bool IstKey { get; set; }
        public string? FKTabelle { get; set; }
        public string? FKSpalte { get; set; }

        public SpalteDef(string dbSpalte, Type typ, bool istPflicht = false, string? fkTabelle = null, string? fkSpalte = null)
        {
            DBSpalte = dbSpalte; Typ = typ; IstPflicht = istPflicht; IstKey = istPflicht;
            FKTabelle = fkTabelle; FKSpalte = fkSpalte;
        }
    }

    public class AmeiseImportOptionen
    {
        public string Trennzeichen { get; set; } = ";";
        public bool UpdateExistierend { get; set; } = true;
        public bool FehlerIgnorieren { get; set; } = false;
        public bool Transaktion { get; set; } = true;
    }

    public class ExportOptionen
    {
        public string Trennzeichen { get; set; } = ";";
        public string? Filter { get; set; }
        public string? Sortierung { get; set; }
        public int Limit { get; set; } = 0;
    }

    public class AmeiseImportErgebnis
    {
        public string Vorlage { get; set; } = "";
        public int GesamtZeilen { get; set; }
        public int Erfolgreich { get; set; }
        public int Fehlgeschlagen { get; set; }
        public List<string> Fehler { get; set; } = new();
    }
    #endregion
}

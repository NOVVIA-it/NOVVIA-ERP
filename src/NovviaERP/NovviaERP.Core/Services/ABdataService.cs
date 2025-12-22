using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Dapper;
using CsvHelper;
using CsvHelper.Configuration;
using Serilog;

namespace NovviaERP.Core.Services
{
    /// <summary>
    /// ABdata Pharma-Daten-Service
    /// Import von Artikelstammdaten (PZN, Preise, etc.)
    /// </summary>
    public class ABdataService
    {
        private readonly string _connectionString;
        private static readonly ILogger _log = Log.ForContext<ABdataService>();

        public ABdataService(string connectionString)
        {
            _connectionString = connectionString;
        }

        #region Import

        /// <summary>
        /// ABdata-Artikelstamm aus CSV/TXT importieren
        /// Format: PZN;Name;Hersteller;AEP;AVP;...
        /// </summary>
        public async Task<ABdataImportResult> ImportArtikelstammAsync(string filePath, ABdataImportOptions? options = null)
        {
            options ??= new ABdataImportOptions();
            var result = new ABdataImportResult
            {
                Dateiname = Path.GetFileName(filePath),
                StartZeit = DateTime.Now
            };

            try
            {
                _log.Information("Starte ABdata-Import: {File}", filePath);

                // Import-Log erstellen
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                result.ImportLogId = await conn.QuerySingleAsync<int>(@"
                    INSERT INTO NOVVIA.ABdataImportLog (cDateiname, dImportStart, cStatus)
                    VALUES (@Datei, GETDATE(), 'GESTARTET'); SELECT SCOPE_IDENTITY();",
                    new { Datei = result.Dateiname });

                // Datei einlesen
                var artikel = await ReadArtikelFromFileAsync(filePath, options);
                result.AnzahlGesamt = artikel.Count;

                // In DB importieren
                foreach (var art in artikel)
                {
                    try
                    {
                        var p = new DynamicParameters();
                        p.Add("@cPZN", art.PZN);
                        p.Add("@cName", art.Name);
                        p.Add("@cHersteller", art.Hersteller);
                        p.Add("@cDarreichungsform", art.Darreichungsform);
                        p.Add("@cPackungsgroesse", art.Packungsgroesse);
                        p.Add("@fMenge", art.Menge);
                        p.Add("@cEinheit", art.Einheit);
                        p.Add("@fAEP", art.AEP);
                        p.Add("@fAVP", art.AVP);
                        p.Add("@fAEK", art.AEK);
                        p.Add("@nRezeptpflicht", art.Rezeptpflicht ? 1 : 0);
                        p.Add("@nBTM", art.BTM ? 1 : 0);
                        p.Add("@nKuehlpflichtig", art.Kuehlpflichtig ? 1 : 0);
                        p.Add("@cATC", art.ATC);
                        p.Add("@cWirkstoff", art.Wirkstoff);
                        p.Add("@dGueltigAb", art.GueltigAb);
                        p.Add("@dGueltigBis", art.GueltigBis);
                        p.Add("@nIsNew", dbType: DbType.Boolean, direction: ParameterDirection.Output);

                        await conn.ExecuteAsync("spNOVVIA_ABdataArtikelUpsert", p, commandType: CommandType.StoredProcedure);

                        if (p.Get<bool>("@nIsNew"))
                            result.AnzahlNeu++;
                        else
                            result.AnzahlAktualisiert++;
                    }
                    catch (Exception ex)
                    {
                        result.AnzahlFehler++;
                        result.Fehler.Add($"PZN {art.PZN}: {ex.Message}");
                        _log.Warning("ABdata Import-Fehler PZN {PZN}: {Error}", art.PZN, ex.Message);
                    }
                }

                // Import-Log aktualisieren
                result.EndeZeit = DateTime.Now;
                result.Success = result.AnzahlFehler == 0;

                await conn.ExecuteAsync(@"
                    UPDATE NOVVIA.ABdataImportLog SET
                        dImportEnde = GETDATE(),
                        nAnzahlGesamt = @Gesamt,
                        nAnzahlNeu = @Neu,
                        nAnzahlAktualisiert = @Aktualisiert,
                        nAnzahlFehler = @Fehler,
                        cStatus = @Status,
                        cFehlerDetails = @Details
                    WHERE kABdataImportLog = @Id",
                    new
                    {
                        Id = result.ImportLogId,
                        Gesamt = result.AnzahlGesamt,
                        Neu = result.AnzahlNeu,
                        Aktualisiert = result.AnzahlAktualisiert,
                        Fehler = result.AnzahlFehler,
                        Status = result.Success ? "ABGESCHLOSSEN" : "MIT_FEHLERN",
                        Details = result.Fehler.Any() ? string.Join("\n", result.Fehler.Take(100)) : null
                    });

                _log.Information("ABdata-Import abgeschlossen: {Neu} neu, {Aktualisiert} aktualisiert, {Fehler} Fehler",
                    result.AnzahlNeu, result.AnzahlAktualisiert, result.AnzahlFehler);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "ABdata-Import fehlgeschlagen");
                result.Success = false;
                result.Fehler.Add(ex.Message);
            }

            return result;
        }

        private async Task<List<ABdataArtikel>> ReadArtikelFromFileAsync(string filePath, ABdataImportOptions options)
        {
            var artikel = new List<ABdataArtikel>();
            var extension = Path.GetExtension(filePath).ToLower();

            await Task.Run(() =>
            {
                using var reader = new StreamReader(filePath, System.Text.Encoding.GetEncoding("ISO-8859-1"));

                if (extension == ".csv" || options.CsvFormat)
                {
                    var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                    {
                        Delimiter = options.Delimiter ?? ";",
                        HasHeaderRecord = options.HasHeader,
                        BadDataFound = null,
                        MissingFieldFound = null
                    };

                    using var csv = new CsvReader(reader, config);

                    if (options.HasHeader)
                    {
                        csv.Read();
                        csv.ReadHeader();
                    }

                    while (csv.Read())
                    {
                        try
                        {
                            var art = new ABdataArtikel
                            {
                                PZN = GetField(csv, options.FeldPZN, 0),
                                Name = GetField(csv, options.FeldName, 1),
                                Hersteller = GetField(csv, options.FeldHersteller, 2),
                                AEP = ParseDecimal(GetField(csv, options.FeldAEP, 3)),
                                AVP = ParseDecimal(GetField(csv, options.FeldAVP, 4)),
                                AEK = ParseDecimal(GetField(csv, options.FeldAEK, 5)),
                                Darreichungsform = GetField(csv, options.FeldDarreichungsform, -1),
                                Packungsgroesse = GetField(csv, options.FeldPackungsgroesse, -1),
                                ATC = GetField(csv, options.FeldATC, -1),
                                Wirkstoff = GetField(csv, options.FeldWirkstoff, -1),
                                Rezeptpflicht = GetField(csv, options.FeldRezeptpflicht, -1) == "1" ||
                                                GetField(csv, options.FeldRezeptpflicht, -1)?.ToLower() == "ja",
                                BTM = GetField(csv, options.FeldBTM, -1) == "1",
                                Kuehlpflichtig = GetField(csv, options.FeldKuehlpflichtig, -1) == "1"
                            };

                            // PZN normalisieren (führende Nullen)
                            if (!string.IsNullOrEmpty(art.PZN))
                            {
                                art.PZN = art.PZN.PadLeft(8, '0');
                                if (art.PZN.Length <= 8 && !string.IsNullOrEmpty(art.Name))
                                    artikel.Add(art);
                            }
                        }
                        catch (Exception ex)
                        {
                            _log.Debug("Zeile übersprungen: {Error}", ex.Message);
                        }
                    }
                }
                else
                {
                    // Festbreitenformat (klassisches ABDATA-Format)
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Length < 50) continue;

                        try
                        {
                            var art = new ABdataArtikel
                            {
                                PZN = line.Substring(0, 8).Trim(),
                                Name = line.Length > 58 ? line.Substring(8, 50).Trim() : "",
                                Hersteller = line.Length > 88 ? line.Substring(58, 30).Trim() : "",
                                AEP = line.Length > 98 ? ParseDecimal(line.Substring(88, 10).Trim()) : 0,
                                AVP = line.Length > 108 ? ParseDecimal(line.Substring(98, 10).Trim()) : 0
                            };

                            if (!string.IsNullOrEmpty(art.PZN) && !string.IsNullOrEmpty(art.Name))
                                artikel.Add(art);
                        }
                        catch { }
                    }
                }
            });

            return artikel;
        }

        private static string GetField(CsvReader csv, string? fieldName, int defaultIndex)
        {
            if (!string.IsNullOrEmpty(fieldName))
            {
                try { return csv.GetField(fieldName) ?? ""; } catch { }
            }
            if (defaultIndex >= 0)
            {
                try { return csv.GetField(defaultIndex) ?? ""; } catch { }
            }
            return "";
        }

        #endregion

        #region Auto-Mapping

        /// <summary>
        /// Automatisches Mapping von ABdata-PZN zu JTL-Artikeln
        /// </summary>
        public async Task<int> AutoMappingAsync()
        {
            using var conn = new SqlConnection(_connectionString);
            var p = new DynamicParameters();
            p.Add("@nAnzahlGemappt", dbType: DbType.Int32, direction: ParameterDirection.Output);

            await conn.ExecuteAsync("spNOVVIA_ABdataAutoMapping", p, commandType: CommandType.StoredProcedure);

            var anzahl = p.Get<int>("@nAnzahlGemappt");
            _log.Information("ABdata Auto-Mapping: {Anzahl} Artikel zugeordnet", anzahl);
            return anzahl;
        }

        /// <summary>
        /// Manuelles Mapping PZN zu Artikel
        /// </summary>
        public async Task MapArtikelAsync(int kArtikel, string pzn)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.ExecuteAsync(@"
                IF NOT EXISTS (SELECT 1 FROM NOVVIA.ABdataArtikelMapping WHERE kArtikel = @Artikel AND cPZN = @PZN)
                    INSERT INTO NOVVIA.ABdataArtikelMapping (kArtikel, cPZN, nAutomatisch) VALUES (@Artikel, @PZN, 0)",
                new { Artikel = kArtikel, PZN = pzn });
        }

        #endregion

        #region Abfragen

        /// <summary>ABdata-Artikel suchen</summary>
        public async Task<IEnumerable<ABdataArtikel>> SucheArtikelAsync(string suche, int limit = 100)
        {
            using var conn = new SqlConnection(_connectionString);
            return await conn.QueryAsync<ABdataArtikel>(@"
                SELECT TOP (@Limit) * FROM NOVVIA.ABdataArtikel
                WHERE cPZN LIKE @Suche OR cName LIKE @Suche OR cHersteller LIKE @Suche
                ORDER BY cName",
                new { Limit = limit, Suche = $"%{suche}%" });
        }

        /// <summary>ABdata-Artikel nach PZN laden</summary>
        public async Task<ABdataArtikel?> GetArtikelByPZNAsync(string pzn)
        {
            using var conn = new SqlConnection(_connectionString);
            return await conn.QuerySingleOrDefaultAsync<ABdataArtikel>(
                "SELECT * FROM NOVVIA.ABdataArtikel WHERE cPZN = @PZN",
                new { PZN = pzn.PadLeft(8, '0') });
        }

        /// <summary>Import-Historie laden</summary>
        public async Task<IEnumerable<ABdataImportLog>> GetImportHistorieAsync(int limit = 50)
        {
            using var conn = new SqlConnection(_connectionString);
            return await conn.QueryAsync<ABdataImportLog>(@"
                SELECT TOP (@Limit) * FROM NOVVIA.ABdataImportLog ORDER BY dImportStart DESC",
                new { Limit = limit });
        }

        #endregion

        #region Hilfsfunktionen

        private static decimal ParseDecimal(string? value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            value = value.Replace(",", ".").Trim();
            return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0;
        }

        #endregion
    }

    #region DTOs

    public class ABdataArtikel
    {
        public int KABdataArtikel { get; set; }
        public string PZN { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Hersteller { get; set; }
        public string? Darreichungsform { get; set; }
        public string? Packungsgroesse { get; set; }
        public decimal Menge { get; set; }
        public string? Einheit { get; set; }
        public decimal AEP { get; set; }  // Apothekeneinkaufspreis
        public decimal AVP { get; set; }  // Apothekenverkaufspreis
        public decimal AEK { get; set; }  // Apothekeneinkaufspreis netto
        public bool Rezeptpflicht { get; set; }
        public bool BTM { get; set; }
        public bool Kuehlpflichtig { get; set; }
        public string? ATC { get; set; }
        public string? Wirkstoff { get; set; }
        public string? Wirkstaerke { get; set; }
        public DateTime? GueltigAb { get; set; }
        public DateTime? GueltigBis { get; set; }
        public DateTime? Importiert { get; set; }
    }

    public class ABdataImportOptions
    {
        public bool CsvFormat { get; set; } = true;
        public string? Delimiter { get; set; } = ";";
        public bool HasHeader { get; set; } = true;

        // Feldnamen oder null für Standardpositionen
        public string? FeldPZN { get; set; }
        public string? FeldName { get; set; }
        public string? FeldHersteller { get; set; }
        public string? FeldAEP { get; set; }
        public string? FeldAVP { get; set; }
        public string? FeldAEK { get; set; }
        public string? FeldDarreichungsform { get; set; }
        public string? FeldPackungsgroesse { get; set; }
        public string? FeldATC { get; set; }
        public string? FeldWirkstoff { get; set; }
        public string? FeldRezeptpflicht { get; set; }
        public string? FeldBTM { get; set; }
        public string? FeldKuehlpflichtig { get; set; }
    }

    public class ABdataImportResult
    {
        public bool Success { get; set; }
        public int ImportLogId { get; set; }
        public string Dateiname { get; set; } = "";
        public DateTime StartZeit { get; set; }
        public DateTime? EndeZeit { get; set; }
        public int AnzahlGesamt { get; set; }
        public int AnzahlNeu { get; set; }
        public int AnzahlAktualisiert { get; set; }
        public int AnzahlFehler { get; set; }
        public List<string> Fehler { get; set; } = new();
    }

    public class ABdataImportLog
    {
        public int KABdataImportLog { get; set; }
        public string Dateiname { get; set; } = "";
        public DateTime ImportStart { get; set; }
        public DateTime? ImportEnde { get; set; }
        public int AnzahlGesamt { get; set; }
        public int AnzahlNeu { get; set; }
        public int AnzahlAktualisiert { get; set; }
        public int AnzahlFehler { get; set; }
        public string? Status { get; set; }
    }

    #endregion
}

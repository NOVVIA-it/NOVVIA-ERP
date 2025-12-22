using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Dapper;
using Serilog;

namespace NovviaERP.Core.Services
{
    /// <summary>
    /// Import-Service für Aufträge und Lieferantenbestellungen aus Excel/CSV
    /// Variabel konfigurierbare Spaltenzuordnung
    /// </summary>
    public class ImportService : IDisposable
    {
        private readonly string _connectionString;
        private SqlConnection? _connection;
        private static readonly ILogger _log = Log.ForContext<ImportService>();

        public ImportService(string connectionString) => _connectionString = connectionString;

        private async Task<SqlConnection> GetConnectionAsync()
        {
            if (_connection == null || _connection.State != ConnectionState.Open)
            {
                _connection?.Dispose();
                _connection = new SqlConnection(_connectionString);
                await _connection.OpenAsync();
            }
            return _connection;
        }

        public void Dispose() { _connection?.Dispose(); _connection = null; }

        #region DTOs

        public class ImportKonfiguration
        {
            public ImportTyp Typ { get; set; }
            public string DateiPfad { get; set; } = "";
            public char CsvTrennzeichen { get; set; } = ';';
            public bool ErsteZeileIstHeader { get; set; } = true;
            public Encoding Encoding { get; set; } = Encoding.UTF8;
            public string? ExcelTabellenblatt { get; set; }

            // Spaltenzuordnung: Key = Ziel-Feld, Value = Spaltenname oder Index
            public Dictionary<string, string> Spaltenzuordnung { get; set; } = new();

            // Standardwerte für fehlende Spalten
            public Dictionary<string, object> Standardwerte { get; set; } = new();
        }

        public enum ImportTyp
        {
            Auftrag,              // Kundenbestellung/Verkaufsauftrag
            LieferantenBestellung // Einkaufsbestellung
        }

        public class ImportErgebnis
        {
            public int AnzahlZeilen { get; set; }
            public int AnzahlErfolgreich { get; set; }
            public int AnzahlFehler { get; set; }
            public int AnzahlUebersprungen { get; set; }
            public List<ImportFehler> Fehler { get; set; } = new();
            public List<int> ErstellteIds { get; set; } = new();
        }

        public class ImportFehler
        {
            public int Zeile { get; set; }
            public string Spalte { get; set; } = "";
            public string Fehlertext { get; set; } = "";
            public string? Rohdaten { get; set; }
        }

        public class ImportAuftragZeile
        {
            // Pflichtfelder
            public string? ExterneBestellnummer { get; set; }

            // Kunde (einer davon muss vorhanden sein)
            public string? KundenNr { get; set; }
            public string? KundeMail { get; set; }
            public string? KundeFirma { get; set; }
            public string? KundeVorname { get; set; }
            public string? KundeNachname { get; set; }
            public string? KundeStrasse { get; set; }
            public string? KundePLZ { get; set; }
            public string? KundeOrt { get; set; }
            public string? KundeLand { get; set; }
            public string? KundeTelefon { get; set; }

            // Lieferadresse (optional)
            public string? LieferFirma { get; set; }
            public string? LieferVorname { get; set; }
            public string? LieferNachname { get; set; }
            public string? LieferStrasse { get; set; }
            public string? LieferPLZ { get; set; }
            public string? LieferOrt { get; set; }
            public string? LieferLand { get; set; }

            // Auftragsdaten
            public DateTime? Bestelldatum { get; set; }
            public string? Zahlungsart { get; set; }
            public string? Versandart { get; set; }
            public string? Anmerkung { get; set; }
            public string? InterneNotiz { get; set; }

            // Position (wenn nur eine Position pro Zeile)
            public string? ArtikelNr { get; set; }
            public string? ArtikelBarcode { get; set; }
            public string? ArtikelName { get; set; }
            public decimal Menge { get; set; } = 1;
            public decimal? Preis { get; set; }
            public decimal? MwStSatz { get; set; }
        }

        public class ImportLieferantenBestellungZeile
        {
            // Lieferant
            public string? LieferantenNr { get; set; }
            public string? LieferantName { get; set; }

            // Bestellung
            public string? BestellNr { get; set; }
            public DateTime? Bestelldatum { get; set; }
            public DateTime? Liefertermin { get; set; }
            public string? Anmerkung { get; set; }

            // Position
            public string? ArtikelNr { get; set; }
            public string? ArtikelBarcode { get; set; }
            public string? LieferantenArtikelNr { get; set; }
            public string? ArtikelName { get; set; }
            public decimal Menge { get; set; } = 1;
            public decimal? EKPreis { get; set; }
        }

        #endregion

        #region Spaltenerkennung

        /// <summary>
        /// Liest die ersten Zeilen einer Datei und gibt die erkannten Spalten zurück
        /// </summary>
        public async Task<List<string>> LeseSpaltennamenAsync(ImportKonfiguration config)
        {
            var ext = Path.GetExtension(config.DateiPfad).ToLower();

            if (ext == ".csv" || ext == ".txt")
            {
                return await LeseCsvSpaltennamenAsync(config);
            }
            else if (ext == ".xlsx" || ext == ".xls")
            {
                return await LeseExcelSpaltennamenAsync(config);
            }

            throw new NotSupportedException($"Dateiformat {ext} wird nicht unterstuetzt");
        }

        private async Task<List<string>> LeseCsvSpaltennamenAsync(ImportKonfiguration config)
        {
            return await Task.Run(() =>
            {
                using var reader = new StreamReader(config.DateiPfad, config.Encoding);
                var headerLine = reader.ReadLine();
                if (string.IsNullOrEmpty(headerLine)) return new List<string>();

                var spalten = headerLine.Split(config.CsvTrennzeichen)
                    .Select(s => s.Trim().Trim('"'))
                    .ToList();

                return spalten;
            });
        }

        private async Task<List<string>> LeseExcelSpaltennamenAsync(ImportKonfiguration config)
        {
            // Excel-Import mit EPPlus oder ähnlich
            // Hier vereinfachte CSV-basierte Lösung
            return await Task.Run(() =>
            {
                // Nutze Microsoft.Office.Interop oder EPPlus
                // Hier Platzhalter - in Produktion EPPlus NuGet verwenden
                _log.Warning("Excel-Import benoetigt EPPlus NuGet-Paket");
                return new List<string> { "Spalte1", "Spalte2", "Spalte3" };
            });
        }

        /// <summary>
        /// Gibt Standard-Spaltenzuordnung für den Import-Typ zurück
        /// </summary>
        public Dictionary<string, string[]> GetStandardSpaltennamen(ImportTyp typ)
        {
            if (typ == ImportTyp.Auftrag)
            {
                return new Dictionary<string, string[]>
                {
                    ["ExterneBestellnummer"] = new[] { "OrderID", "BestellNr", "Bestellnummer", "Order Number", "Auftragsnummer" },
                    ["KundenNr"] = new[] { "CustomerID", "KundenNr", "Kundennummer", "Customer Number" },
                    ["KundeMail"] = new[] { "Email", "E-Mail", "Kunden-Email", "CustomerEmail" },
                    ["KundeFirma"] = new[] { "Company", "Firma", "Firmenname" },
                    ["KundeVorname"] = new[] { "FirstName", "Vorname", "First Name" },
                    ["KundeNachname"] = new[] { "LastName", "Nachname", "Last Name", "Name" },
                    ["KundeStrasse"] = new[] { "Street", "Strasse", "Address", "Adresse" },
                    ["KundePLZ"] = new[] { "ZIP", "PLZ", "Postleitzahl", "PostalCode" },
                    ["KundeOrt"] = new[] { "City", "Ort", "Stadt" },
                    ["KundeLand"] = new[] { "Country", "Land" },
                    ["KundeTelefon"] = new[] { "Phone", "Telefon", "Tel" },
                    ["Bestelldatum"] = new[] { "OrderDate", "Bestelldatum", "Datum", "Date" },
                    ["ArtikelNr"] = new[] { "SKU", "ArtNr", "Artikelnummer", "ArticleNumber", "ProductID" },
                    ["ArtikelBarcode"] = new[] { "EAN", "Barcode", "GTIN", "UPC" },
                    ["ArtikelName"] = new[] { "ProductName", "Artikelname", "Artikel", "Name", "Bezeichnung" },
                    ["Menge"] = new[] { "Quantity", "Menge", "Anzahl", "Qty" },
                    ["Preis"] = new[] { "Price", "Preis", "UnitPrice", "Einzelpreis" },
                    ["MwStSatz"] = new[] { "VAT", "MwSt", "Tax", "Steuer" },
                    ["Zahlungsart"] = new[] { "PaymentMethod", "Zahlungsart", "Payment" },
                    ["Versandart"] = new[] { "ShippingMethod", "Versandart", "Shipping" }
                };
            }
            else
            {
                return new Dictionary<string, string[]>
                {
                    ["LieferantenNr"] = new[] { "SupplierID", "LieferantenNr", "Lieferantennummer", "VendorID" },
                    ["LieferantName"] = new[] { "SupplierName", "Lieferant", "Vendor" },
                    ["BestellNr"] = new[] { "OrderID", "BestellNr", "PONumber" },
                    ["Bestelldatum"] = new[] { "OrderDate", "Bestelldatum", "Datum" },
                    ["Liefertermin"] = new[] { "DeliveryDate", "Liefertermin", "LieferDatum" },
                    ["ArtikelNr"] = new[] { "SKU", "ArtNr", "Artikelnummer", "ItemNumber" },
                    ["LieferantenArtikelNr"] = new[] { "SupplierSKU", "LieferantenArtNr", "VendorSKU" },
                    ["ArtikelName"] = new[] { "ProductName", "Artikelname", "Artikel", "Description" },
                    ["Menge"] = new[] { "Quantity", "Menge", "Anzahl", "Qty" },
                    ["EKPreis"] = new[] { "Price", "EKPreis", "Einkaufspreis", "UnitCost" }
                };
            }
        }

        /// <summary>
        /// Versucht automatische Spaltenzuordnung basierend auf Spaltenüberschriften
        /// </summary>
        public Dictionary<string, string> AutoMapSpalten(List<string> vorhandeneSpalten, ImportTyp typ)
        {
            var zuordnung = new Dictionary<string, string>();
            var standardNamen = GetStandardSpaltennamen(typ);

            foreach (var (zielFeld, moeglicheNamen) in standardNamen)
            {
                var match = vorhandeneSpalten.FirstOrDefault(s =>
                    moeglicheNamen.Any(m => s.Equals(m, StringComparison.OrdinalIgnoreCase)));

                if (match != null)
                {
                    zuordnung[zielFeld] = match;
                }
            }

            return zuordnung;
        }

        #endregion

        #region Import Aufträge

        public async Task<ImportErgebnis> ImportiereAuftraegeAsync(ImportKonfiguration config)
        {
            var ergebnis = new ImportErgebnis();
            var zeilen = await LeseDateiAsync(config);
            ergebnis.AnzahlZeilen = zeilen.Count;

            // Gruppiere nach Bestellnummer (mehrere Positionen pro Auftrag)
            var auftraege = new Dictionary<string, List<ImportAuftragZeile>>();

            foreach (var (zeile, rowNum) in zeilen.Select((z, i) => (z, i + 2)))
            {
                try
                {
                    var auftragZeile = MappeAuftragZeile(zeile, config.Spaltenzuordnung);
                    var key = auftragZeile.ExterneBestellnummer ?? $"ROW_{rowNum}";

                    if (!auftraege.ContainsKey(key))
                        auftraege[key] = new List<ImportAuftragZeile>();

                    auftraege[key].Add(auftragZeile);
                }
                catch (Exception ex)
                {
                    ergebnis.Fehler.Add(new ImportFehler
                    {
                        Zeile = rowNum,
                        Fehlertext = ex.Message
                    });
                    ergebnis.AnzahlFehler++;
                }
            }

            // Speichere Aufträge
            var conn = await GetConnectionAsync();

            foreach (var (externeNr, positionen) in auftraege)
            {
                try
                {
                    // Prüfe ob Auftrag schon existiert
                    var existiert = await conn.QuerySingleOrDefaultAsync<int?>(
                        "SELECT kBestellung FROM tBestellung WHERE cInetBestellNr = @Nr",
                        new { Nr = externeNr });

                    if (existiert.HasValue)
                    {
                        ergebnis.AnzahlUebersprungen++;
                        continue;
                    }

                    var hauptZeile = positionen.First();
                    var kundeId = await FindeOderErstelleKundeAsync(conn, hauptZeile);

                    if (kundeId == null)
                    {
                        ergebnis.Fehler.Add(new ImportFehler
                        {
                            Zeile = 0,
                            Fehlertext = $"Kunde fuer Auftrag {externeNr} nicht gefunden/erstellbar"
                        });
                        ergebnis.AnzahlFehler++;
                        continue;
                    }

                    using var tx = conn.BeginTransaction();
                    try
                    {
                        // Auftrag anlegen
                        var bestellungId = await conn.QuerySingleAsync<int>(@"
                            INSERT INTO tBestellung (tKunde_kKunde, cBestellNr, cInetBestellNr, dErstellt, cStatus,
                                                    cWaehrung, cAnmerkung)
                            VALUES (@KundeId, @BestellNr, @ExterneNr, @Datum, 'Offen', 'EUR', @Anmerkung);
                            SELECT SCOPE_IDENTITY();",
                            new
                            {
                                KundeId = kundeId,
                                BestellNr = await GetNaechsteBestellnummerAsync(conn, tx),
                                ExterneNr = externeNr,
                                Datum = hauptZeile.Bestelldatum ?? DateTime.Now,
                                hauptZeile.Anmerkung
                            }, tx);

                        // Positionen anlegen
                        foreach (var pos in positionen)
                        {
                            var artikelId = await FindeArtikelAsync(conn, tx, pos.ArtikelNr, pos.ArtikelBarcode);

                            await conn.ExecuteAsync(@"
                                INSERT INTO tbestellpos (tBestellung_kBestellung, tArtikel_kArtikel, cArtNr, cName,
                                                        fAnzahl, fVKNetto, fMwSt)
                                VALUES (@BestellungId, @ArtikelId, @ArtNr, @Name, @Menge, @Preis, @MwSt)",
                                new
                                {
                                    BestellungId = bestellungId,
                                    ArtikelId = artikelId,
                                    ArtNr = pos.ArtikelNr,
                                    Name = pos.ArtikelName,
                                    pos.Menge,
                                    Preis = pos.Preis ?? 0,
                                    MwSt = pos.MwStSatz ?? 19
                                }, tx);
                        }

                        tx.Commit();
                        ergebnis.ErstellteIds.Add(bestellungId);
                        ergebnis.AnzahlErfolgreich++;
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    ergebnis.Fehler.Add(new ImportFehler
                    {
                        Fehlertext = $"Auftrag {externeNr}: {ex.Message}"
                    });
                    ergebnis.AnzahlFehler++;
                }
            }

            _log.Information("Import abgeschlossen: {Erfolg} erstellt, {Fehler} Fehler, {Skip} uebersprungen",
                ergebnis.AnzahlErfolgreich, ergebnis.AnzahlFehler, ergebnis.AnzahlUebersprungen);

            return ergebnis;
        }

        private async Task<int?> FindeOderErstelleKundeAsync(SqlConnection conn, ImportAuftragZeile zeile)
        {
            // Suche nach Kundennummer
            if (!string.IsNullOrEmpty(zeile.KundenNr))
            {
                var id = await conn.QuerySingleOrDefaultAsync<int?>(
                    "SELECT kKunde FROM tkunde WHERE cKundenNr = @Nr", new { Nr = zeile.KundenNr });
                if (id.HasValue) return id;
            }

            // Suche nach E-Mail
            if (!string.IsNullOrEmpty(zeile.KundeMail))
            {
                var id = await conn.QuerySingleOrDefaultAsync<int?>(
                    "SELECT k.kKunde FROM tkunde k INNER JOIN tAdresse a ON a.kKunde = k.kKunde WHERE a.cMail = @Mail",
                    new { Mail = zeile.KundeMail });
                if (id.HasValue) return id;
            }

            // Kunde anlegen wenn genug Daten vorhanden
            if (!string.IsNullOrEmpty(zeile.KundeNachname) || !string.IsNullOrEmpty(zeile.KundeFirma))
            {
                using var tx = conn.BeginTransaction();
                try
                {
                    var kundeId = await conn.QuerySingleAsync<int>(@"
                        INSERT INTO tkunde (cKundenNr, dErstellt) VALUES (@Nr, GETDATE());
                        SELECT SCOPE_IDENTITY();",
                        new { Nr = await GetNaechsteKundennummerAsync(conn, tx) }, tx);

                    await conn.ExecuteAsync(@"
                        INSERT INTO tAdresse (kKunde, cFirma, cVorname, cName, cStrasse, cPLZ, cOrt, cLand, cMail, cTel, nStandard)
                        VALUES (@KundeId, @Firma, @Vorname, @Nachname, @Strasse, @PLZ, @Ort, @Land, @Mail, @Tel, 1)",
                        new
                        {
                            KundeId = kundeId,
                            Firma = zeile.KundeFirma,
                            Vorname = zeile.KundeVorname,
                            Nachname = zeile.KundeNachname,
                            Strasse = zeile.KundeStrasse,
                            PLZ = zeile.KundePLZ,
                            Ort = zeile.KundeOrt,
                            Land = zeile.KundeLand ?? "DE",
                            Mail = zeile.KundeMail,
                            Tel = zeile.KundeTelefon
                        }, tx);

                    tx.Commit();
                    return kundeId;
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }

            return null;
        }

        private async Task<int?> FindeArtikelAsync(SqlConnection conn, SqlTransaction tx, string? artNr, string? barcode)
        {
            if (!string.IsNullOrEmpty(artNr))
            {
                var id = await conn.QuerySingleOrDefaultAsync<int?>(
                    "SELECT kArtikel FROM tArtikel WHERE cArtNr = @Nr", new { Nr = artNr }, tx);
                if (id.HasValue) return id;
            }

            if (!string.IsNullOrEmpty(barcode))
            {
                var id = await conn.QuerySingleOrDefaultAsync<int?>(
                    "SELECT kArtikel FROM tArtikel WHERE cBarcode = @Code", new { Code = barcode }, tx);
                if (id.HasValue) return id;
            }

            return null;
        }

        #endregion

        #region Import Lieferantenbestellungen

        public async Task<ImportErgebnis> ImportiereLieferantenBestellungenAsync(ImportKonfiguration config)
        {
            var ergebnis = new ImportErgebnis();
            var zeilen = await LeseDateiAsync(config);
            ergebnis.AnzahlZeilen = zeilen.Count;

            var conn = await GetConnectionAsync();

            // Gruppiere nach Bestellnummer
            var bestellungen = new Dictionary<string, List<ImportLieferantenBestellungZeile>>();

            foreach (var (zeile, rowNum) in zeilen.Select((z, i) => (z, i + 2)))
            {
                try
                {
                    var bestellZeile = MappeLieferantenBestellungZeile(zeile, config.Spaltenzuordnung);
                    var key = bestellZeile.BestellNr ?? $"ROW_{rowNum}";

                    if (!bestellungen.ContainsKey(key))
                        bestellungen[key] = new List<ImportLieferantenBestellungZeile>();

                    bestellungen[key].Add(bestellZeile);
                }
                catch (Exception ex)
                {
                    ergebnis.Fehler.Add(new ImportFehler { Zeile = rowNum, Fehlertext = ex.Message });
                    ergebnis.AnzahlFehler++;
                }
            }

            foreach (var (bestellNr, positionen) in bestellungen)
            {
                try
                {
                    var hauptZeile = positionen.First();

                    // Lieferant finden
                    var lieferantId = await FindeLieferantAsync(conn, hauptZeile.LieferantenNr, hauptZeile.LieferantName);
                    if (lieferantId == null)
                    {
                        ergebnis.Fehler.Add(new ImportFehler
                        {
                            Fehlertext = $"Lieferant nicht gefunden fuer Bestellung {bestellNr}"
                        });
                        ergebnis.AnzahlFehler++;
                        continue;
                    }

                    using var tx = conn.BeginTransaction();
                    try
                    {
                        var bestellungId = await conn.QuerySingleAsync<int>(@"
                            INSERT INTO tLieferantenBestellung (kLieferant, cBestellNr, dErstellt, dLiefertermin, cAnmerkung, nStatus)
                            VALUES (@LieferantId, @BestellNr, @Datum, @Liefertermin, @Anmerkung, 1);
                            SELECT SCOPE_IDENTITY();",
                            new
                            {
                                LieferantId = lieferantId,
                                BestellNr = bestellNr,
                                Datum = hauptZeile.Bestelldatum ?? DateTime.Now,
                                Liefertermin = hauptZeile.Liefertermin,
                                hauptZeile.Anmerkung
                            }, tx);

                        foreach (var pos in positionen)
                        {
                            var artikelId = await FindeArtikelAsync(conn, tx, pos.ArtikelNr, pos.ArtikelBarcode);

                            await conn.ExecuteAsync(@"
                                INSERT INTO tLieferantenBestellungPos (kLieferantenBestellung, kArtikel, cArtNr, cLiefArtNr, cName, fMenge, fEKNetto)
                                VALUES (@BestellungId, @ArtikelId, @ArtNr, @LiefArtNr, @Name, @Menge, @EK)",
                                new
                                {
                                    BestellungId = bestellungId,
                                    ArtikelId = artikelId,
                                    ArtNr = pos.ArtikelNr,
                                    LiefArtNr = pos.LieferantenArtikelNr,
                                    Name = pos.ArtikelName,
                                    pos.Menge,
                                    EK = pos.EKPreis ?? 0
                                }, tx);
                        }

                        tx.Commit();
                        ergebnis.ErstellteIds.Add(bestellungId);
                        ergebnis.AnzahlErfolgreich++;
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    ergebnis.Fehler.Add(new ImportFehler { Fehlertext = $"Bestellung {bestellNr}: {ex.Message}" });
                    ergebnis.AnzahlFehler++;
                }
            }

            return ergebnis;
        }

        private async Task<int?> FindeLieferantAsync(SqlConnection conn, string? lieferantenNr, string? name)
        {
            if (!string.IsNullOrEmpty(lieferantenNr))
            {
                var id = await conn.QuerySingleOrDefaultAsync<int?>(
                    "SELECT kLieferant FROM tLieferant WHERE cLieferantenNr = @Nr", new { Nr = lieferantenNr });
                if (id.HasValue) return id;
            }

            if (!string.IsNullOrEmpty(name))
            {
                var id = await conn.QuerySingleOrDefaultAsync<int?>(
                    "SELECT kLieferant FROM tLieferant WHERE cFirma LIKE @Name", new { Name = $"%{name}%" });
                if (id.HasValue) return id;
            }

            return null;
        }

        #endregion

        #region Hilfsmethoden

        private async Task<List<Dictionary<string, string>>> LeseDateiAsync(ImportKonfiguration config)
        {
            var ext = Path.GetExtension(config.DateiPfad).ToLower();

            if (ext == ".csv" || ext == ".txt")
            {
                return await LeseCsvAsync(config);
            }
            else if (ext == ".xlsx" || ext == ".xls")
            {
                return await LeseExcelAsync(config);
            }

            throw new NotSupportedException($"Dateiformat {ext} nicht unterstuetzt");
        }

        private async Task<List<Dictionary<string, string>>> LeseCsvAsync(ImportKonfiguration config)
        {
            return await Task.Run(() =>
            {
                var zeilen = new List<Dictionary<string, string>>();
                var spaltenNamen = new List<string>();

                using var reader = new StreamReader(config.DateiPfad, config.Encoding);
                int lineNum = 0;

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    lineNum++;
                    var werte = ParseCsvZeile(line, config.CsvTrennzeichen);

                    if (lineNum == 1 && config.ErsteZeileIstHeader)
                    {
                        spaltenNamen = werte;
                        continue;
                    }

                    var zeile = new Dictionary<string, string>();
                    for (int i = 0; i < werte.Count; i++)
                    {
                        var spalte = spaltenNamen.Count > i ? spaltenNamen[i] : $"Spalte{i + 1}";
                        zeile[spalte] = werte[i];
                    }
                    zeilen.Add(zeile);
                }

                return zeilen;
            });
        }

        private List<string> ParseCsvZeile(string line, char trennzeichen)
        {
            var werte = new List<string>();
            var current = new StringBuilder();
            bool inQuotes = false;

            foreach (char c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == trennzeichen && !inQuotes)
                {
                    werte.Add(current.ToString().Trim());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            werte.Add(current.ToString().Trim());

            return werte;
        }

        private async Task<List<Dictionary<string, string>>> LeseExcelAsync(ImportKonfiguration config)
        {
            // TODO: Mit EPPlus implementieren
            // Hier Fallback auf CSV wenn möglich
            _log.Warning("Excel-Import benoetigt EPPlus - verwende CSV-Export der Excel-Datei");
            return await Task.FromResult(new List<Dictionary<string, string>>());
        }

        private ImportAuftragZeile MappeAuftragZeile(Dictionary<string, string> zeile, Dictionary<string, string> zuordnung)
        {
            var result = new ImportAuftragZeile();

            result.ExterneBestellnummer = GetWert(zeile, zuordnung, "ExterneBestellnummer");
            result.KundenNr = GetWert(zeile, zuordnung, "KundenNr");
            result.KundeMail = GetWert(zeile, zuordnung, "KundeMail");
            result.KundeFirma = GetWert(zeile, zuordnung, "KundeFirma");
            result.KundeVorname = GetWert(zeile, zuordnung, "KundeVorname");
            result.KundeNachname = GetWert(zeile, zuordnung, "KundeNachname");
            result.KundeStrasse = GetWert(zeile, zuordnung, "KundeStrasse");
            result.KundePLZ = GetWert(zeile, zuordnung, "KundePLZ");
            result.KundeOrt = GetWert(zeile, zuordnung, "KundeOrt");
            result.KundeLand = GetWert(zeile, zuordnung, "KundeLand");
            result.KundeTelefon = GetWert(zeile, zuordnung, "KundeTelefon");
            result.ArtikelNr = GetWert(zeile, zuordnung, "ArtikelNr");
            result.ArtikelBarcode = GetWert(zeile, zuordnung, "ArtikelBarcode");
            result.ArtikelName = GetWert(zeile, zuordnung, "ArtikelName");
            result.Zahlungsart = GetWert(zeile, zuordnung, "Zahlungsart");
            result.Versandart = GetWert(zeile, zuordnung, "Versandart");
            result.Anmerkung = GetWert(zeile, zuordnung, "Anmerkung");

            var datumStr = GetWert(zeile, zuordnung, "Bestelldatum");
            if (!string.IsNullOrEmpty(datumStr) && DateTime.TryParse(datumStr, out var datum))
                result.Bestelldatum = datum;

            var mengeStr = GetWert(zeile, zuordnung, "Menge");
            if (!string.IsNullOrEmpty(mengeStr) && decimal.TryParse(mengeStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var menge))
                result.Menge = menge;

            var preisStr = GetWert(zeile, zuordnung, "Preis");
            if (!string.IsNullOrEmpty(preisStr) && decimal.TryParse(preisStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var preis))
                result.Preis = preis;

            return result;
        }

        private ImportLieferantenBestellungZeile MappeLieferantenBestellungZeile(Dictionary<string, string> zeile, Dictionary<string, string> zuordnung)
        {
            var result = new ImportLieferantenBestellungZeile();

            result.LieferantenNr = GetWert(zeile, zuordnung, "LieferantenNr");
            result.LieferantName = GetWert(zeile, zuordnung, "LieferantName");
            result.BestellNr = GetWert(zeile, zuordnung, "BestellNr");
            result.ArtikelNr = GetWert(zeile, zuordnung, "ArtikelNr");
            result.ArtikelBarcode = GetWert(zeile, zuordnung, "ArtikelBarcode");
            result.LieferantenArtikelNr = GetWert(zeile, zuordnung, "LieferantenArtikelNr");
            result.ArtikelName = GetWert(zeile, zuordnung, "ArtikelName");
            result.Anmerkung = GetWert(zeile, zuordnung, "Anmerkung");

            var datumStr = GetWert(zeile, zuordnung, "Bestelldatum");
            if (!string.IsNullOrEmpty(datumStr) && DateTime.TryParse(datumStr, out var datum))
                result.Bestelldatum = datum;

            var lieferStr = GetWert(zeile, zuordnung, "Liefertermin");
            if (!string.IsNullOrEmpty(lieferStr) && DateTime.TryParse(lieferStr, out var lieferDatum))
                result.Liefertermin = lieferDatum;

            var mengeStr = GetWert(zeile, zuordnung, "Menge");
            if (!string.IsNullOrEmpty(mengeStr) && decimal.TryParse(mengeStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var menge))
                result.Menge = menge;

            var ekStr = GetWert(zeile, zuordnung, "EKPreis");
            if (!string.IsNullOrEmpty(ekStr) && decimal.TryParse(ekStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var ek))
                result.EKPreis = ek;

            return result;
        }

        private string? GetWert(Dictionary<string, string> zeile, Dictionary<string, string> zuordnung, string feldName)
        {
            if (zuordnung.TryGetValue(feldName, out var spalte) && zeile.TryGetValue(spalte, out var wert))
                return string.IsNullOrWhiteSpace(wert) ? null : wert.Trim();
            return null;
        }

        private async Task<string> GetNaechsteBestellnummerAsync(SqlConnection conn, SqlTransaction tx)
        {
            var maxNr = await conn.QuerySingleOrDefaultAsync<int?>(
                "SELECT MAX(CAST(cBestellNr AS INT)) FROM tBestellung WHERE ISNUMERIC(cBestellNr) = 1", transaction: tx);
            return ((maxNr ?? 0) + 1).ToString();
        }

        private async Task<string> GetNaechsteKundennummerAsync(SqlConnection conn, SqlTransaction tx)
        {
            var maxNr = await conn.QuerySingleOrDefaultAsync<int?>(
                "SELECT MAX(CAST(cKundenNr AS INT)) FROM tkunde WHERE ISNUMERIC(cKundenNr) = 1", transaction: tx);
            return ((maxNr ?? 0) + 1).ToString();
        }

        #endregion
    }
}

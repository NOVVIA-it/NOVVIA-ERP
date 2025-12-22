using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Dapper;
using NovviaERP.Core.Data;
using NovviaERP.Core.Entities;
using Serilog;

namespace NovviaERP.Core.Services
{
    /// <summary>
    /// Service für Steuerberechnung inkl. EU/Nicht-EU, Reverse Charge, OSS
    /// </summary>
    public class SteuerService
    {
        private readonly JtlDbContext _db;
        private readonly HttpClient _http;
        private static readonly ILogger _log = Log.ForContext<SteuerService>();

        public SteuerService(JtlDbContext db)
        {
            _db = db;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        #region Steuerzone ermitteln
        /// <summary>
        /// Ermittelt die Steuerzone für einen Kunden basierend auf Land und USt-ID
        /// </summary>
        public async Task<(SteuerzoneTyp Zone, decimal Steuersatz, string? Befreiungstext)> ErmittleSteuerAsync(
            string landISO, string? ustId, bool istPrivatkunde, int steuerklasseId = 1)
        {
            var conn = await _db.GetConnectionAsync();
            
            // EU-Länder
            var euLaender = new[] { "AT", "BE", "BG", "HR", "CY", "CZ", "DK", "EE", "FI", "FR", "GR", "HU", 
                "IE", "IT", "LV", "LT", "LU", "MT", "NL", "PL", "PT", "RO", "SK", "SI", "ES", "SE" };
            
            var istEU = euLaender.Contains(landISO.ToUpper());
            var istInland = landISO.ToUpper() == "DE";

            // Standard-Steuersatz laden
            var standardSatz = await conn.QuerySingleOrDefaultAsync<decimal>(
                "SELECT TOP 1 fSteuersatz FROM tSteuerSatz WHERE kSteuerKlasse = @Id ORDER BY nPrioritaet", 
                new { Id = steuerklasseId });
            if (standardSatz == 0) standardSatz = 19m;

            // 1. Inland (Deutschland)
            if (istInland)
            {
                return (SteuerzoneTyp.Inland, standardSatz, null);
            }

            // 2. EU mit gültiger USt-ID → Reverse Charge (0%)
            if (istEU && !string.IsNullOrEmpty(ustId))
            {
                var ustIdGueltig = await PruefeUStIDAsync(ustId);
                if (ustIdGueltig)
                {
                    return (SteuerzoneTyp.EUMitUStID, 0m, 
                        "Steuerfreie innergemeinschaftliche Lieferung gem. §4 Nr. 1b i.V.m. §6a UStG. " +
                        "Die Steuerschuld geht auf den Leistungsempfänger über (Reverse Charge).");
                }
            }

            // 3. EU Privatkunde oder ohne gültige USt-ID
            if (istEU && (istPrivatkunde || string.IsNullOrEmpty(ustId)))
            {
                // OSS prüfen - bei Überschreitung der Lieferschwelle gilt der Steuersatz des Ziellandes
                var ossAktiv = await conn.QuerySingleOrDefaultAsync<bool>(
                    "SELECT COUNT(*) FROM tOSS WHERE cLandISO = @Land AND nAktiv = 1", new { Land = landISO });
                
                if (ossAktiv)
                {
                    var ossSatz = await conn.QuerySingleOrDefaultAsync<decimal>(
                        "SELECT fSteuersatzNormal FROM tOSS WHERE cLandISO = @Land", new { Land = landISO });
                    return (SteuerzoneTyp.EUPrivat, ossSatz > 0 ? ossSatz : standardSatz, null);
                }
                
                return (SteuerzoneTyp.EUOhneUStID, standardSatz, null);
            }

            // 4. Drittland (Nicht-EU) → Steuerfreier Export
            return (SteuerzoneTyp.DrittlandExport, 0m, 
                "Steuerfreie Ausfuhrlieferung gem. §4 Nr. 1a i.V.m. §6 UStG.");
        }

        /// <summary>
        /// Berechnet die Steuer für eine Position
        /// </summary>
        public async Task<(decimal Netto, decimal Steuer, decimal Brutto, decimal Steuersatz, string? Hinweis)> 
            BerechneSteuerAsync(decimal betragBrutto, string landISO, string? ustId, bool istPrivatkunde, int steuerklasseId = 1)
        {
            var (zone, satz, hinweis) = await ErmittleSteuerAsync(landISO, ustId, istPrivatkunde, steuerklasseId);
            
            if (satz == 0)
            {
                return (betragBrutto, 0, betragBrutto, 0, hinweis);
            }
            
            var netto = Math.Round(betragBrutto / (1 + satz / 100), 2);
            var steuer = betragBrutto - netto;
            
            return (netto, steuer, betragBrutto, satz, hinweis);
        }
        #endregion

        #region USt-ID Prüfung (VIES)
        /// <summary>
        /// Prüft eine USt-ID über den EU VIES-Dienst
        /// </summary>
        public async Task<bool> PruefeUStIDAsync(string ustId, int? kundeId = null, int? lieferantId = null)
        {
            if (string.IsNullOrWhiteSpace(ustId)) return false;

            // Format bereinigen
            ustId = Regex.Replace(ustId.ToUpper(), @"[^A-Z0-9]", "");
            if (ustId.Length < 4) return false;

            var landCode = ustId.Substring(0, 2);
            var nummer = ustId.Substring(2);

            try
            {
                // VIES SOAP Request
                var soapRequest = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
                    <soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" 
                                      xmlns:urn=""urn:ec.europa.eu:taxud:vies:services:checkVat:types"">
                        <soapenv:Body>
                            <urn:checkVat>
                                <urn:countryCode>{landCode}</urn:countryCode>
                                <urn:vatNumber>{nummer}</urn:vatNumber>
                            </urn:checkVat>
                        </soapenv:Body>
                    </soapenv:Envelope>";

                var content = new StringContent(soapRequest, System.Text.Encoding.UTF8, "text/xml");
                var response = await _http.PostAsync("https://ec.europa.eu/taxation_customs/vies/services/checkVatService", content);
                var xml = await response.Content.ReadAsStringAsync();

                var doc = XDocument.Parse(xml);
                XNamespace ns = "urn:ec.europa.eu:taxud:vies:services:checkVat:types";
                
                var valid = doc.Descendants(ns + "valid").FirstOrDefault()?.Value == "true";
                var name = doc.Descendants(ns + "name").FirstOrDefault()?.Value;
                var address = doc.Descendants(ns + "address").FirstOrDefault()?.Value;
                var requestId = doc.Descendants(ns + "requestIdentifier").FirstOrDefault()?.Value;

                // Ergebnis speichern
                var conn = await _db.GetConnectionAsync();
                await conn.ExecuteAsync(@"INSERT INTO tUStIDPruefung 
                    (kKunde, kLieferant, cUStID, cLandISO, cFirmenname, cAdresse, nGueltig, dPruefung, cRequestID)
                    VALUES (@KundeId, @LieferantId, @UStID, @Land, @Name, @Adresse, @Gueltig, GETDATE(), @RequestId)",
                    new { KundeId = kundeId, LieferantId = lieferantId, UStID = ustId, Land = landCode, 
                          Name = name, Adresse = address, Gueltig = valid, RequestId = requestId });

                _log.Information("USt-ID Prüfung: {UStID} = {Gueltig}", ustId, valid);
                return valid;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "USt-ID Prüfung fehlgeschlagen für {UStID}", ustId);
                return false;
            }
        }

        /// <summary>
        /// Holt die letzte Prüfung einer USt-ID
        /// </summary>
        public async Task<UStIDPruefung?> GetLetztePruefungAsync(string ustId)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QuerySingleOrDefaultAsync<UStIDPruefung>(
                "SELECT TOP 1 * FROM tUStIDPruefung WHERE cUStID = @UStID ORDER BY dPruefung DESC",
                new { UStID = ustId.ToUpper().Replace(" ", "") });
        }
        #endregion

        #region Steuerklassen & Steuersätze
        public async Task<IEnumerable<SteuerklasseErweitert>> GetSteuerklassenAsync()
        {
            var conn = await _db.GetConnectionAsync();
            var klassen = await conn.QueryAsync<SteuerklasseErweitert>("SELECT * FROM tSteuerKlasse ORDER BY nStandard DESC, cName");
            foreach (var k in klassen)
            {
                k.Steuersaetze = (await conn.QueryAsync<SteuersatzErweitert>(
                    "SELECT * FROM tSteuerSatz WHERE kSteuerKlasse = @Id ORDER BY nPrioritaet",
                    new { Id = k.Id })).AsList();
            }
            return klassen;
        }

        public async Task<decimal> GetSteuersatzAsync(int steuerklasseId, string? landISO = null, int? steuerzoneId = null)
        {
            var conn = await _db.GetConnectionAsync();
            var sql = @"SELECT TOP 1 fSteuersatz FROM tSteuerSatz 
                        WHERE kSteuerKlasse = @KlasseId 
                        AND (cLandISO IS NULL OR cLandISO = @Land)
                        AND (kSteuerzone IS NULL OR kSteuerzone = @ZoneId)
                        AND (dGueltigVon IS NULL OR dGueltigVon <= GETDATE())
                        AND (dGueltigBis IS NULL OR dGueltigBis >= GETDATE())
                        ORDER BY nPrioritaet";
            return await conn.QuerySingleOrDefaultAsync<decimal>(sql,
                new { KlasseId = steuerklasseId, Land = landISO, ZoneId = steuerzoneId });
        }
        #endregion

        #region OSS (One Stop Shop)
        public async Task<IEnumerable<OSSRegistrierung>> GetOSSLaenderAsync()
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QueryAsync<OSSRegistrierung>("SELECT * FROM tOSS WHERE nAktiv = 1 ORDER BY cLandName");
        }

        public async Task UpdateOSSAsync(OSSRegistrierung oss)
        {
            var conn = await _db.GetConnectionAsync();
            await conn.ExecuteAsync(@"UPDATE tOSS SET fSteuersatzNormal = @SteuersatzNormal, 
                fSteuersatzErmaessigt = @SteuersatzErmaessigt, nAktiv = @Aktiv WHERE kOSS = @Id", oss);
        }
        #endregion

        #region Steuerbefreiungen
        public async Task<IEnumerable<Steuerbefreiung>> GetSteuerbefreiungenAsync()
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QueryAsync<Steuerbefreiung>("SELECT * FROM tSteuerbefreiung ORDER BY cCode");
        }

        public async Task<string?> GetBefreiungstextAsync(int befreiungId, string sprache = "DE")
        {
            var conn = await _db.GetConnectionAsync();
            var befreiung = await conn.QuerySingleOrDefaultAsync<Steuerbefreiung>(
                "SELECT * FROM tSteuerbefreiung WHERE kSteuerbefreiung = @Id", new { Id = befreiungId });
            
            return sprache.ToUpper() == "EN" ? befreiung?.RechnungstextEN : befreiung?.Rechnungstext;
        }
        #endregion
    }
}

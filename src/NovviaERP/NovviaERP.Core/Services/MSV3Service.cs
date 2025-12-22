using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Data.SqlClient;
using Dapper;
using Serilog;

namespace NovviaERP.Core.Services
{
    /// <summary>
    /// MSV3 (Medium Speed Version 3) - Pharma-Großhandel Schnittstelle
    /// Für Bestellungen bei Pharma-Großhändlern (Sanacorp, Phoenix, etc.)
    /// </summary>
    public class MSV3Service : IDisposable
    {
        private readonly string _connectionString;
        private readonly HttpClient _http;
        private static readonly ILogger _log = Log.ForContext<MSV3Service>();

        public MSV3Service(string connectionString)
        {
            _connectionString = connectionString;
            var handler = new HttpClientHandler();
            _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
            // User-Agent setzen (wichtig für WAF/Incapsula)
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("NovviaERP/1.0 MSV3Client");
            _http.DefaultRequestHeaders.Accept.ParseAdd("application/soap+xml, application/xml, text/xml");
        }

        public void Dispose() => _http.Dispose();

        #region Lieferanten-Konfiguration

        /// <summary>Alle MSV3-fähigen Lieferanten laden</summary>
        public async Task<IEnumerable<MSV3Lieferant>> GetMSV3LieferantenAsync()
        {
            using var conn = new SqlConnection(_connectionString);
            return await conn.QueryAsync<MSV3Lieferant>("EXEC spNOVVIA_MSV3LieferantLaden");
        }

        /// <summary>MSV3-Konfiguration für Lieferant laden</summary>
        public async Task<MSV3Lieferant?> GetMSV3LieferantAsync(int kLieferant)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                return await conn.QuerySingleOrDefaultAsync<MSV3Lieferant>(
                    "SELECT m.*, l.cFirma AS LieferantName FROM NOVVIA.MSV3Lieferant m INNER JOIN tlieferant l ON m.kLieferant = l.kLieferant WHERE m.kLieferant = @kLieferant AND m.nAktiv = 1",
                    new { kLieferant });
            }
            catch
            {
                return null; // NOVVIA.MSV3Lieferant Tabelle existiert nicht
            }
        }

        /// <summary>MSV3-Konfiguration speichern</summary>
        public async Task<int> SaveMSV3LieferantAsync(MSV3Lieferant config)
        {
            using var conn = new SqlConnection(_connectionString);
            var p = new DynamicParameters();
            p.Add("@kLieferant", config.KLieferant);
            p.Add("@cMSV3Url", config.MSV3Url);
            p.Add("@cMSV3Benutzer", config.MSV3Benutzer);
            p.Add("@cMSV3Passwort", config.MSV3Passwort);
            p.Add("@cMSV3Kundennummer", config.MSV3Kundennummer);
            p.Add("@cMSV3Filiale", config.MSV3Filiale);
            p.Add("@nMSV3Version", config.MSV3Version);
            p.Add("@nPrioritaet", config.Prioritaet);
            p.Add("@kMSV3Lieferant", dbType: DbType.Int32, direction: ParameterDirection.Output);

            await conn.ExecuteAsync("spNOVVIA_MSV3LieferantSpeichern", p, commandType: CommandType.StoredProcedure);
            return p.Get<int>("@kMSV3Lieferant");
        }

        #endregion

        #region Verbindungstest

        /// <summary>
        /// Verbindung zum Großhandel testen (MSV3 VerbindungTesten)
        /// Gibt true zurück wenn Authentifizierung erfolgreich
        /// </summary>
        public async Task<MSV3VerbindungResult> VerbindungTestenAsync(MSV3Lieferant config)
        {
            var result = new MSV3VerbindungResult();

            try
            {
                _log.Information("MSV3 VerbindungTesten für {Lieferant}", config.LieferantName ?? config.KLieferant.ToString());

                // Leerer Request-Body für VerbindungTesten
                var response = await SendMSV3RequestAsync(config, "", "VerbindungTesten");

                if (response.Success)
                {
                    result.Success = true;
                    result.Meldung = "Verbindung erfolgreich";
                    _log.Information("MSV3 Verbindungstest erfolgreich für {Lieferant}", config.LieferantName);
                }
                else
                {
                    result.Success = false;
                    result.Fehler = response.Fehler;
                    _log.Warning("MSV3 Verbindungstest fehlgeschlagen: {Fehler}", response.Fehler);
                }

                result.ResponseXml = response.ResponseXml;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "MSV3 VerbindungTesten Fehler");
                result.Success = false;
                result.Fehler = ex.Message;
            }

            return result;
        }

        #endregion

        #region Verfügbarkeitsabfrage

        /// <summary>
        /// Verfügbarkeit bei Großhandel prüfen (MSV3 VerfuegbarkeitAbfragen) - Einfache PZN-Liste
        /// </summary>
        public async Task<MSV3VerfuegbarkeitResult> CheckVerfuegbarkeitAsync(MSV3Lieferant config, IEnumerable<string> pzns)
        {
            var artikel = pzns.Select((pzn, i) => new MSV3ArtikelAnfrage
            {
                PZN = pzn,
                Menge = 1,
                Id = (i + 1).ToString()
            });
            return await CheckVerfuegbarkeitAsync(config, artikel);
        }

        /// <summary>
        /// Verfügbarkeit bei Großhandel prüfen (MSV3 VerfuegbarkeitAbfragen) - Vollständige Artikelliste
        /// </summary>
        public async Task<MSV3VerfuegbarkeitResult> CheckVerfuegbarkeitAsync(MSV3Lieferant config, IEnumerable<MSV3ArtikelAnfrage> artikel)
        {
            var result = new MSV3VerfuegbarkeitResult();

            try
            {
                var xml = BuildVerfuegbarkeitRequest(config, artikel);
                _log.Debug("MSV3 Verfügbarkeitsanfrage: {Url}", config.MSV3Url);

                var response = await SendMSV3RequestAsync(config, xml, "VerfuegbarkeitAbfragen");

                if (response.Success)
                {
                    result = ParseVerfuegbarkeitResponse(response.ResponseXml!);
                    result.Success = true;
                }
                else
                {
                    result.Success = false;
                    result.Fehler = response.Fehler;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "MSV3 Verfügbarkeitsabfrage fehlgeschlagen");
                result.Success = false;
                result.Fehler = ex.Message;
            }

            return result;
        }

        private string GetMSV3Namespace(int version) => version switch
        {
            2 => "urn:msv3:v2",
            _ => "urn:msv3:v1"
        };

        private string BuildVerfuegbarkeitRequest(MSV3Lieferant config, IEnumerable<MSV3ArtikelAnfrage> artikel)
        {
            var ns = XNamespace.Get(GetMSV3Namespace(config.MSV3Version));
            var artikelElements = artikel.Select((a, i) => new XElement(ns + "Artikel",
                new XElement(ns + "PZN", a.PZN),
                new XElement(ns + "Menge", a.Menge),
                new XElement(ns + "Id", a.Id ?? (i + 1).ToString())
            ));

            var doc = new XDocument(
                new XElement(ns + "VerfuegbarkeitAbfragen",
                    new XElement(ns + "Kundennummer", config.MSV3Kundennummer),
                    new XElement(ns + "Filiale", config.MSV3Filiale ?? "001"),
                    new XElement(ns + "Artikelliste", artikelElements)
                )
            );

            return doc.ToString();
        }

        private MSV3VerfuegbarkeitResult ParseVerfuegbarkeitResponse(string xml)
        {
            var result = new MSV3VerfuegbarkeitResult { Positionen = new List<MSV3VerfuegbarkeitPosition>() };

            try
            {
                var doc = XDocument.Parse(xml);
                var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;

                foreach (var artikel in doc.Descendants(ns + "Artikel"))
                {
                    var pos = new MSV3VerfuegbarkeitPosition
                    {
                        PZN = artikel.Element(ns + "PZN")?.Value ?? "",
                        Id = artikel.Element(ns + "Id")?.Value,
                        MengeAngefragt = ParseDecimal(artikel.Element(ns + "MengeAngefragt")?.Value),
                        MengeVerfuegbar = ParseDecimal(artikel.Element(ns + "MengeVerfuegbar")?.Value),
                        PreisEK = ParseDecimal(artikel.Element(ns + "Preis")?.Value ?? artikel.Element(ns + "EK")?.Value),
                        PreisAEP = ParseDecimal(artikel.Element(ns + "AEP")?.Value),
                        PreisAVP = ParseDecimal(artikel.Element(ns + "AVP")?.Value),
                        Verfuegbar = artikel.Element(ns + "Verfuegbar")?.Value == "true" ||
                                     artikel.Element(ns + "Status")?.Value == "VERFUEGBAR",
                        Hinweis = artikel.Element(ns + "Hinweis")?.Value ?? artikel.Element(ns + "Meldung")?.Value,
                        ChargenNr = artikel.Element(ns + "ChargenNr")?.Value ?? artikel.Element(ns + "Charge")?.Value,
                        MHD = ParseDate(artikel.Element(ns + "MHD")?.Value ?? artikel.Element(ns + "Verfall")?.Value),
                        Lieferzeit = artikel.Element(ns + "Lieferzeit")?.Value ?? artikel.Element(ns + "LieferzeitTage")?.Value
                    };

                    // Status ableiten
                    if (pos.MengeVerfuegbar >= pos.MengeAngefragt)
                        pos.Status = "VERFUEGBAR";
                    else if (pos.MengeVerfuegbar > 0)
                        pos.Status = "TEILWEISE";
                    else
                        pos.Status = "NICHT_VERFUEGBAR";

                    result.Positionen.Add(pos);
                }

                result.AnzahlVerfuegbar = result.Positionen.Count(p => p.Status == "VERFUEGBAR");
                result.AnzahlTeilweise = result.Positionen.Count(p => p.Status == "TEILWEISE");
                result.AnzahlNichtVerfuegbar = result.Positionen.Count(p => p.Status == "NICHT_VERFUEGBAR");
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Fehler beim Parsen der MSV3-Antwort");
                result.Fehler = $"Parse-Fehler: {ex.Message}";
            }

            return result;
        }

        #endregion

        #region Bestellung

        /// <summary>
        /// Bestellung an Großhandel senden (MSV3 BestellungAbsenden) - Mit Positionen und MinMHD
        /// </summary>
        public async Task<MSV3BestellungResult> SendBestellungAsync(MSV3Lieferant config, string bestellNummer, IEnumerable<MSV3BestellPosition> positionen)
        {
            var result = new MSV3BestellungResult();

            try
            {
                var xml = BuildBestellungRequestFromPositions(config, bestellNummer, positionen);
                _log.Debug("MSV3 Bestellung senden: {Url}, BestellNr: {Nr}", config.MSV3Url, bestellNummer);

                var response = await SendMSV3RequestAsync(config, xml, "BestellungAbsenden");

                if (response.Success)
                {
                    result = ParseBestellungResponse(response.ResponseXml!);
                    result.Success = true;
                    _log.Information("MSV3 Bestellung {BestellNr} gesendet, AuftragsId: {AuftragsId}", bestellNummer, result.AuftragsId);
                }
                else
                {
                    result.Success = false;
                    result.Fehler = response.Fehler;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "MSV3 Bestellung fehlgeschlagen");
                result.Success = false;
                result.Fehler = ex.Message;
            }

            return result;
        }

        private string BuildBestellungRequestFromPositions(MSV3Lieferant config, string bestellNummer, IEnumerable<MSV3BestellPosition> positionen)
        {
            var ns = XNamespace.Get(GetMSV3Namespace(config.MSV3Version));
            int posNr = 0;

            var positionenElements = positionen.Select(p =>
            {
                posNr++;
                var posElement = new XElement(ns + "Position",
                    new XElement(ns + "PZN", p.PZN),
                    new XElement(ns + "Menge", p.Menge),
                    new XElement(ns + "PosNr", posNr)
                );

                // MinMHD hinzufügen wenn vorhanden
                if (p.MinMHD.HasValue)
                {
                    posElement.Add(new XElement(ns + "MinMHD", p.MinMHD.Value.ToString("yyyy-MM-dd")));
                }

                return posElement;
            });

            var doc = new XDocument(
                new XElement(ns + "BestellungAbsenden",
                    new XElement(ns + "Kundennummer", config.MSV3Kundennummer),
                    new XElement(ns + "Filiale", config.MSV3Filiale ?? "001"),
                    new XElement(ns + "BestellNr", bestellNummer),
                    new XElement(ns + "Lieferart", "NORMAL"),
                    new XElement(ns + "Positionen", positionenElements)
                )
            );

            return doc.ToString();
        }

        /// <summary>
        /// Bestellung an Großhandel senden (MSV3 BestellungAbsenden) - Aus DB laden
        /// </summary>
        public async Task<MSV3BestellungResult> SendBestellungAsync(int kLieferantenBestellung)
        {
            var result = new MSV3BestellungResult();

            try
            {
                using var conn = new SqlConnection(_connectionString);

                // Bestellung und MSV3-Daten laden
                var bestellung = await conn.QuerySingleOrDefaultAsync<dynamic>(@"
                    SELECT lb.*, m.*, l.cFirma AS LieferantName
                    FROM tLieferantenBestellung lb
                    INNER JOIN NOVVIA.MSV3Bestellung mb ON lb.kLieferantenBestellung = mb.kLieferantenBestellung
                    INNER JOIN NOVVIA.MSV3Lieferant m ON mb.kMSV3Lieferant = m.kMSV3Lieferant
                    INNER JOIN tlieferant l ON lb.kLieferant = l.kLieferant
                    WHERE lb.kLieferantenBestellung = @Id", new { Id = kLieferantenBestellung });

                if (bestellung == null)
                {
                    result.Success = false;
                    result.Fehler = "Bestellung oder MSV3-Konfiguration nicht gefunden";
                    return result;
                }

                // Positionen laden
                var positionen = await conn.QueryAsync<dynamic>(@"
                    SELECT p.*, mp.cPZN, mp.kMSV3BestellungPos
                    FROM tLieferantenBestellungPos p
                    INNER JOIN NOVVIA.MSV3BestellungPos mp ON p.kLieferantenBestellungPos = mp.kLieferantenBestellungPos
                    WHERE p.kLieferantenBestellung = @Id", new { Id = kLieferantenBestellung });

                var config = new MSV3Lieferant
                {
                    MSV3Url = bestellung.cMSV3Url,
                    MSV3Benutzer = bestellung.cMSV3Benutzer,
                    MSV3Passwort = bestellung.cMSV3Passwort,
                    MSV3Kundennummer = bestellung.cMSV3Kundennummer,
                    MSV3Filiale = bestellung.cMSV3Filiale
                };

                // XML erstellen
                var xml = BuildBestellungRequest(config, bestellung, positionen);

                // An Großhandel senden
                var response = await SendMSV3RequestAsync(config, xml, "BestellungAbsenden");

                if (response.Success)
                {
                    result = ParseBestellungResponse(response.ResponseXml!);
                    result.Success = true;

                    // Status in DB aktualisieren
                    var p = new DynamicParameters();
                    p.Add("@kMSV3Bestellung", (int)bestellung.kMSV3Bestellung);
                    p.Add("@cMSV3Status", "GESENDET");
                    p.Add("@cMSV3AuftragsId", result.AuftragsId);
                    p.Add("@cResponseXML", response.ResponseXml);
                    await conn.ExecuteAsync("spNOVVIA_MSV3BestellungStatusUpdate", p, commandType: CommandType.StoredProcedure);

                    _log.Information("MSV3 Bestellung {BestellNr} gesendet, AuftragsId: {AuftragsId}",
                        bestellung.cEigeneBestellnummer, result.AuftragsId);
                }
                else
                {
                    result.Success = false;
                    result.Fehler = response.Fehler;

                    // Fehler in DB speichern
                    await conn.ExecuteAsync(@"
                        UPDATE NOVVIA.MSV3Bestellung SET cMSV3Status = 'FEHLER', cFehler = @Fehler
                        WHERE kLieferantenBestellung = @Id",
                        new { Id = kLieferantenBestellung, Fehler = response.Fehler });
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "MSV3 Bestellung fehlgeschlagen");
                result.Success = false;
                result.Fehler = ex.Message;
            }

            return result;
        }

        private string BuildBestellungRequest(MSV3Lieferant config, dynamic bestellung, IEnumerable<dynamic> positionen)
        {
            var ns = XNamespace.Get(GetMSV3Namespace(config.MSV3Version));

            var positionenElements = positionen.Select(p =>
            {
                var posElement = new XElement(ns + "Position",
                    new XElement(ns + "PZN", (string)p.cPZN),
                    new XElement(ns + "Menge", (decimal)p.fMenge),
                    new XElement(ns + "PosNr", (int)p.nSort)
                );

                // MinMHD hinzufügen wenn vorhanden (optionales Feld)
                DateTime? minMhd = p.dMinMHD as DateTime?;
                if (minMhd.HasValue)
                {
                    posElement.Add(new XElement(ns + "MinMHD", minMhd.Value.ToString("yyyy-MM-dd")));
                }

                return posElement;
            });

            var doc = new XDocument(
                new XElement(ns + "BestellungAbsenden",
                    new XElement(ns + "Kundennummer", config.MSV3Kundennummer),
                    new XElement(ns + "Filiale", config.MSV3Filiale ?? "001"),
                    new XElement(ns + "BestellNr", (string)bestellung.cEigeneBestellnummer),
                    new XElement(ns + "Lieferart", "NORMAL"),  // NORMAL, EXPRESS, NACHT
                    new XElement(ns + "Positionen", positionenElements)
                )
            );

            return doc.ToString();
        }

        private MSV3BestellungResult ParseBestellungResponse(string xml)
        {
            var result = new MSV3BestellungResult();

            try
            {
                var doc = XDocument.Parse(xml);
                var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;

                result.AuftragsId = doc.Descendants(ns + "AuftragsId").FirstOrDefault()?.Value ??
                                    doc.Descendants(ns + "BestellId").FirstOrDefault()?.Value;
                result.Status = doc.Descendants(ns + "Status").FirstOrDefault()?.Value ?? "ANGENOMMEN";

                var fehler = doc.Descendants(ns + "Fehler").FirstOrDefault()?.Value ??
                             doc.Descendants(ns + "Error").FirstOrDefault()?.Value;
                if (!string.IsNullOrEmpty(fehler))
                {
                    result.Success = false;
                    result.Fehler = fehler;
                }
            }
            catch (Exception ex)
            {
                result.Fehler = $"Parse-Fehler: {ex.Message}";
            }

            return result;
        }

        #endregion

        #region HTTP-Kommunikation

        private async Task<MSV3Response> SendMSV3RequestAsync(MSV3Lieferant config, string requestXml, string action)
        {
            var result = new MSV3Response();

            try
            {
                // Namespace basierend auf MSV3-Version
                var nsUri = GetMSV3Namespace(config.MSV3Version);
                var baseUrl = config.MSV3Url.TrimEnd('/');

                // SOAP 1.2 Request mit korrektem MSV3 Namespace
                var soapRequest = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<soap:Envelope xmlns:soap=""http://www.w3.org/2003/05/soap-envelope"" xmlns:msv=""{nsUri}"">
   <soap:Header/>
   <soap:Body>
      <msv:{action}>
         <msv:Clientsystem>NovviaERP</msv:Clientsystem>
         <msv:Benutzerkennung>{SecurityElement.Escape(config.MSV3Benutzer)}</msv:Benutzerkennung>
         <msv:Kennwort>{SecurityElement.Escape(config.MSV3Passwort)}</msv:Kennwort>
         {requestXml}
      </msv:{action}>
   </soap:Body>
</soap:Envelope>";

                // Auch SOAP 1.1 Variante vorbereiten (manche Großhändler wollen das)
                var soapRequest11 = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:msv=""{nsUri}"">
   <soap:Header/>
   <soap:Body>
      <msv:{action}>
         <msv:Clientsystem>NovviaERP</msv:Clientsystem>
         <msv:Benutzerkennung>{SecurityElement.Escape(config.MSV3Benutzer)}</msv:Benutzerkennung>
         <msv:Kennwort>{SecurityElement.Escape(config.MSV3Passwort)}</msv:Kennwort>
         {requestXml}
      </msv:{action}>
   </soap:Body>
</soap:Envelope>";

                _log.Information("MSV3 Request an: {Url}, Action: {Action}", baseUrl, action);
                _log.Debug("MSV3 SOAP Request:\n{Body}", soapRequest);

                // MSV3 v2 erwartet den Action-Namen in der URL!
                // Versuche verschiedene URL-Kombinationen
                string[] urlsToTry = {
                    $"{baseUrl}/v{config.MSV3Version}.0/{action}",       // /msv3/v2.0/VerfuegbarkeitAnfragen (Standard für v2)
                    $"{baseUrl}/{config.MSV3Version}.0/{action}",        // /msv3/2.0/VerfuegbarkeitAnfragen
                    $"{baseUrl}/v{config.MSV3Version}.0",                // /msv3/v2.0
                    $"{baseUrl}/{action}",                               // /msv3/VerfuegbarkeitAnfragen (für v1)
                    baseUrl,                                             // /msv3 (Fallback)
                };

                // Content-Types die wir versuchen
                string[] contentTypes = { "application/soap+xml", "text/xml" };

                HttpResponseMessage response = null!;
                string responseXml = "";
                string usedUrl = baseUrl;
                Exception? lastError = null;

                // Preemptive Basic Auth Header vorbereiten
                var authBytes = Encoding.UTF8.GetBytes($"{config.MSV3Benutzer}:{config.MSV3Passwort}");
                var basicAuthHeader = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));

                foreach (var url in urlsToTry)
                {
                    usedUrl = url;

                    foreach (var contentType in contentTypes)
                    {
                        // Wähle SOAP 1.1 oder 1.2 basierend auf Content-Type
                        var currentSoapRequest = contentType == "text/xml" ? soapRequest11 : soapRequest;

                        try
                        {
                            // IMMER mit Basic Auth versuchen (preemptive) - viele Großhändler erwarten das
                            var request = new HttpRequestMessage(HttpMethod.Post, url);
                            request.Content = new StringContent(currentSoapRequest, Encoding.UTF8, contentType);
                            request.Headers.Authorization = basicAuthHeader;

                            // User-Agent Header - wichtig für Incapsula WAF Bypass
                            request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) NovviaERP/1.0 MSV3Client");

                            // Accept Header
                            request.Headers.TryAddWithoutValidation("Accept", "application/soap+xml, text/xml, */*");

                            // SOAPAction Header hinzufügen (manche Services erwarten das)
                            request.Headers.Add("SOAPAction", $"\"{nsUri}/{action}\"");

                            response = await _http.SendAsync(request);
                            responseXml = await response.Content.ReadAsStringAsync();

                            _log.Debug("MSV3 Versuch URL: {Url}, ContentType: {CT} -> Status: {Status}", url, contentType, response.StatusCode);

                            // Bei 200 OK oder SOAP-Antwort (auch 500 kann gültige SOAP-Antwort sein) -> verwenden
                            if (response.IsSuccessStatusCode || responseXml.Contains("soap:Envelope") || responseXml.Contains("Envelope") || responseXml.Contains("SOAP-ENV"))
                            {
                                _log.Information("MSV3 Antwort von: {Url} (ContentType: {CT})", url, contentType);
                                goto FoundResponse; // Aus beiden Schleifen raus
                            }

                            // Bei 401 trotz Basic Auth: Credentials falsch oder anderes Auth-Problem
                            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                            {
                                _log.Warning("MSV3 401 trotz Basic Auth bei URL: {Url}", url);
                                // Weiter versuchen mit anderem Content-Type
                                continue;
                            }

                            // Bei 404/405 nächste URL versuchen
                            if (response.StatusCode == System.Net.HttpStatusCode.NotFound ||
                                response.StatusCode == System.Net.HttpStatusCode.MethodNotAllowed)
                            {
                                break; // Aus Content-Type Schleife, nächste URL
                            }
                        }
                        catch (Exception ex)
                        {
                            lastError = ex;
                            _log.Warning("MSV3 Fehler bei URL {Url}: {Error}", url, ex.Message);
                        }
                    }
                }
                FoundResponse:

                if (response == null && lastError != null)
                {
                    throw lastError;
                }

                _log.Debug("MSV3 Response Status: {Status}", response?.StatusCode);
                _log.Debug("MSV3 Response Body:\n{Body}", responseXml);

                // SOAP-Antwort parsen (auch bei HTTP 500 - SOAP-Faults kommen mit 500)
                if (!string.IsNullOrEmpty(responseXml) && (responseXml.Contains("Envelope") || responseXml.Contains("soap")))
                {
                    try
                    {
                        var doc = XDocument.Parse(responseXml);

                        // Auf SOAP-Fault prüfen
                        var fault = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Fault");
                        if (fault != null)
                        {
                            var faultString = fault.Descendants().FirstOrDefault(e => e.Name.LocalName == "faultstring" || e.Name.LocalName == "Text")?.Value;
                            var faultDetail = fault.Descendants().FirstOrDefault(e => e.Name.LocalName == "detail" || e.Name.LocalName == "Detail")?.Value;
                            var endanwenderText = fault.Descendants().FirstOrDefault(e => e.Name.LocalName == "EndanwenderFehlertext")?.Value;

                            result.Success = false;
                            result.Fehler = endanwenderText ?? faultString ?? faultDetail ?? "SOAP Fault";
                            result.ResponseXml = responseXml;
                            _log.Warning("MSV3 SOAP-Fault: {Fehler}", result.Fehler);
                            return result;
                        }

                        // SOAP-Body extrahieren
                        var body = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Body");
                        result.ResponseXml = body?.FirstNode?.ToString() ?? responseXml;
                        result.Success = response?.IsSuccessStatusCode ?? false;
                    }
                    catch (Exception parseEx)
                    {
                        _log.Warning("MSV3 XML-Parse-Fehler: {Error}", parseEx.Message);
                        result.ResponseXml = responseXml;
                        result.Success = response?.IsSuccessStatusCode ?? false;
                    }
                }
                else if (response?.IsSuccessStatusCode == true)
                {
                    result.ResponseXml = responseXml;
                    result.Success = true;
                }
                else
                {
                    result.Success = false;
                    result.Fehler = $"HTTP {(int)(response?.StatusCode ?? 0)}: {response?.ReasonPhrase}\n\nURL: {usedUrl}\n\nAntwort: {(responseXml.Length > 1000 ? responseXml.Substring(0, 1000) + "..." : responseXml)}";
                    result.ResponseXml = responseXml;
                }
            }
            catch (HttpRequestException ex)
            {
                result.Success = false;
                result.Fehler = $"Verbindungsfehler: {ex.Message}";
                _log.Error(ex, "MSV3 HTTP-Fehler");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Fehler = ex.Message;
                _log.Error(ex, "MSV3 Fehler");
            }

            return result;
        }

        #endregion

        #region Hilfsfunktionen

        private static decimal ParseDecimal(string? value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            // Deutsche und englische Dezimaltrennzeichen
            value = value.Replace(",", ".");
            return decimal.TryParse(value, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : 0;
        }

        private static DateTime? ParseDate(string? value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            // MSV3 verwendet Format: YYYY-MM-DD oder DD.MM.YYYY
            if (DateTime.TryParse(value, out var result)) return result;
            if (DateTime.TryParseExact(value, new[] { "yyyy-MM-dd", "dd.MM.yyyy", "yyyyMMdd" },
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out result)) return result;
            return null;
        }

        #endregion

        #region Bestellen-Response Parser (GEHE-Format)

        /// <summary>
        /// Verfuegbarkeit per "bestellen"-Operation pruefen und parsen (GEHE-Format)
        /// Dies ist die Methode die von VARIO8 verwendet wird
        /// </summary>
        public async Task<MSV3BestellenVerfuegbarkeitResult> CheckVerfuegbarkeitViaBestellenAsync(
            MSV3Lieferant config,
            IEnumerable<MSV3BestellPosition> positionen,
            string? bestellSupportId = null)
        {
            var result = new MSV3BestellenVerfuegbarkeitResult();

            try
            {
                var supportId = bestellSupportId ?? DateTime.Now.Ticks.ToString().Substring(0, 6);
                var xml = BuildBestellenRequest(config, positionen, supportId);

                _log.Debug("MSV3 Verfuegbarkeit via bestellen: {Url}", config.MSV3Url);

                var response = await SendMSV3RequestAsync(config, xml, "bestellen");
                result.ResponseXml = response.ResponseXml;

                if (response.Success && !string.IsNullOrEmpty(response.ResponseXml))
                {
                    result = ParseBestellenVerfuegbarkeitResponse(response.ResponseXml);
                    result.Success = true;

                    // Cache aktualisieren
                    await CacheVerfuegbarkeitAsync(config.KLieferant, result.Positionen);
                }
                else
                {
                    result.Success = false;
                    result.Fehler = response.Fehler;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "MSV3 Verfuegbarkeit via bestellen fehlgeschlagen");
                result.Success = false;
                result.Fehler = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// SOAP-Request fuer bestellen-Operation bauen (GEHE/VARIO8-Format)
        /// </summary>
        private string BuildBestellenRequest(MSV3Lieferant config, IEnumerable<MSV3BestellPosition> positionen, string supportId)
        {
            var bestellungId = Guid.NewGuid().ToString().ToUpper();
            var auftragId = Guid.NewGuid().ToString().ToUpper();

            var sb = new StringBuilder();
            sb.AppendLine($"<bestellung xmlns=\"\" Id=\"{bestellungId}\" BestellSupportId=\"{supportId}\">");
            sb.AppendLine($"  <Auftraege xmlns=\"urn:msv3:v2\" Id=\"{auftragId}\" Auftragsart=\"NORMAL\" Auftragskennung=\"{supportId}\" AuftragsSupportID=\"{supportId}\">");

            foreach (var pos in positionen)
            {
                sb.AppendLine("    <Positionen>");
                sb.AppendLine($"      <Pzn>{pos.PZN}</Pzn>");
                sb.AppendLine($"      <Menge>{pos.Menge}</Menge>");
                sb.AppendLine("      <Liefervorgabe>Normal</Liefervorgabe>");
                sb.AppendLine("    </Positionen>");
            }

            sb.AppendLine("  </Auftraege>");
            sb.AppendLine("</bestellung>");

            return sb.ToString();
        }

        /// <summary>
        /// bestellen-Response parsen (GEHE-Format mit Anteilen und Typ)
        /// </summary>
        private MSV3BestellenVerfuegbarkeitResult ParseBestellenVerfuegbarkeitResponse(string xml)
        {
            var result = new MSV3BestellenVerfuegbarkeitResult
            {
                ResponseXml = xml,
                Positionen = new List<MSV3BestellenPosition>()
            };

            try
            {
                var doc = XDocument.Parse(xml);

                // Namespace ermitteln (ns2="urn:msv3:v2")
                XNamespace ns2 = "urn:msv3:v2";

                // Return-Element mit Attributen
                var returnElement = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "return");
                if (returnElement != null)
                {
                    result.BestellSupportId = returnElement.Attribute("BestellSupportId")?.Value;
                    result.NachtBetrieb = returnElement.Attribute("NachtBetrieb")?.Value == "true";
                }

                // Positionen parsen
                foreach (var posElement in doc.Descendants().Where(e => e.Name.LocalName == "Positionen"))
                {
                    var position = new MSV3BestellenPosition
                    {
                        PZN = posElement.Descendants().FirstOrDefault(e => e.Name.LocalName == "BestellPzn")?.Value ?? "",
                        BestellMenge = ParseInt(posElement.Descendants().FirstOrDefault(e => e.Name.LocalName == "BestellMenge")?.Value),
                        Liefervorgabe = posElement.Descendants().FirstOrDefault(e => e.Name.LocalName == "BestellLiefervorgabe")?.Value,
                        Anteile = new List<MSV3BestellenAnteil>()
                    };

                    // Anteile parsen
                    foreach (var anteilElement in posElement.Descendants().Where(e => e.Name.LocalName == "Anteile"))
                    {
                        var anteil = new MSV3BestellenAnteil
                        {
                            Menge = ParseInt(anteilElement.Descendants().FirstOrDefault(e => e.Name.LocalName == "Menge")?.Value),
                            Typ = anteilElement.Descendants().FirstOrDefault(e => e.Name.LocalName == "Typ")?.Value,
                            Grund = anteilElement.Descendants().FirstOrDefault(e => e.Name.LocalName == "Grund")?.Value,
                            Tourabweichung = anteilElement.Descendants().FirstOrDefault(e => e.Name.LocalName == "Tourabweichung")?.Value == "true"
                        };

                        // Lieferzeitpunkt (kann nil sein)
                        var lieferzeitpunktElement = anteilElement.Descendants().FirstOrDefault(e => e.Name.LocalName == "Lieferzeitpunkt");
                        if (lieferzeitpunktElement != null)
                        {
                            XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";
                            var nilAttr = lieferzeitpunktElement.Attribute(xsi + "nil");
                            if (nilAttr?.Value != "true" && !string.IsNullOrEmpty(lieferzeitpunktElement.Value))
                            {
                                anteil.Lieferzeitpunkt = ParseDate(lieferzeitpunktElement.Value);
                            }
                        }

                        position.Anteile.Add(anteil);
                    }

                    result.Positionen.Add(position);
                }

                _log.Information("MSV3 bestellen-Response geparst: {Anzahl} Positionen", result.Positionen.Count);

                foreach (var pos in result.Positionen)
                {
                    _log.Debug("  PZN {PZN}: Status={Status}, Verfuegbar={Verfuegbar}/{Bestellt}, Typ={Typ}, Grund={Grund}",
                        pos.PZN, pos.StatusCode, pos.VerfuegbareMenge, pos.BestellMenge, pos.HauptTyp, pos.HauptGrund);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Fehler beim Parsen der bestellen-Response");
                result.Fehler = $"Parse-Fehler: {ex.Message}";
            }

            return result;
        }

        private static int ParseInt(string? value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            return int.TryParse(value, out var result) ? result : 0;
        }

        #endregion

        #region Verfuegbarkeits-Cache

        /// <summary>
        /// Verfuegbarkeit aus Cache laden (wenn gueltig)
        /// </summary>
        public async Task<MSV3VerfuegbarkeitCacheEntry?> GetCachedVerfuegbarkeitAsync(string pzn, int? kLieferant = null)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                var p = new DynamicParameters();
                p.Add("@cPZN", pzn);
                p.Add("@kLieferant", kLieferant);

                var entry = await conn.QuerySingleOrDefaultAsync<MSV3VerfuegbarkeitCacheEntry>(
                    "NOVVIA.spMSV3VerfuegbarkeitCache_Get", p, commandType: CommandType.StoredProcedure);

                return entry;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Fehler beim Laden des Verfuegbarkeits-Cache fuer PZN {PZN}", pzn);
                return null;
            }
        }

        /// <summary>
        /// Mehrere Verfuegbarkeiten aus Cache laden
        /// </summary>
        public async Task<IEnumerable<MSV3VerfuegbarkeitCacheEntry>> GetCachedVerfuegbarkeitenAsync(
            IEnumerable<string> pzns, int? kLieferant = null)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                var p = new DynamicParameters();
                p.Add("@cPZNList", string.Join(",", pzns));
                p.Add("@kLieferant", kLieferant);

                return await conn.QueryAsync<MSV3VerfuegbarkeitCacheEntry>(
                    "NOVVIA.spMSV3VerfuegbarkeitCache_GetBulk", p, commandType: CommandType.StoredProcedure);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Fehler beim Bulk-Laden des Verfuegbarkeits-Cache");
                return Enumerable.Empty<MSV3VerfuegbarkeitCacheEntry>();
            }
        }

        /// <summary>
        /// Verfuegbarkeit im Cache speichern
        /// </summary>
        public async Task CacheVerfuegbarkeitAsync(int kLieferant, MSV3BestellenPosition position, int ttlMinutes = 10)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                var p = new DynamicParameters();
                p.Add("@cPZN", position.PZN);
                p.Add("@kLieferant", kLieferant);
                p.Add("@nRequestedQty", position.BestellMenge);
                p.Add("@cStatusCode", position.StatusCode);
                p.Add("@nAvailableQty", position.VerfuegbareMenge);
                p.Add("@cReasonCode", position.HauptGrund);
                p.Add("@dNextDeliveryUtc", position.NaechsterLieferzeitpunkt);
                p.Add("@cRawTyp", position.HauptTyp);
                p.Add("@nTtlMinutes", ttlMinutes);

                await conn.ExecuteAsync("NOVVIA.spMSV3VerfuegbarkeitCache_Upsert", p, commandType: CommandType.StoredProcedure);

                _log.Debug("Verfuegbarkeit gecached: PZN={PZN}, Status={Status}", position.PZN, position.StatusCode);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Fehler beim Cachen der Verfuegbarkeit fuer PZN {PZN}", position.PZN);
            }
        }

        /// <summary>
        /// Mehrere Verfuegbarkeiten im Cache speichern
        /// </summary>
        public async Task CacheVerfuegbarkeitAsync(int kLieferant, IEnumerable<MSV3BestellenPosition> positionen, int ttlMinutes = 10)
        {
            foreach (var pos in positionen)
            {
                await CacheVerfuegbarkeitAsync(kLieferant, pos, ttlMinutes);
            }
        }

        /// <summary>
        /// Request/Response loggen (fuer Debugging und Support)
        /// </summary>
        public async Task LogRequestAsync(
            int? kLieferant,
            string endpoint,
            string action,
            int? httpStatus,
            bool soapFault,
            string? requestBody,
            string? responseBody,
            string? errorMessage,
            int? durationMs)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                var p = new DynamicParameters();
                p.Add("@kLieferant", kLieferant);
                p.Add("@cEndpoint", endpoint);
                p.Add("@cAction", action);
                p.Add("@nHttpStatus", httpStatus);
                p.Add("@nSoapFault", soapFault);
                p.Add("@cRequestBody", requestBody?.Length > 8000 ? requestBody.Substring(0, 8000) : requestBody);
                p.Add("@cResponseBody", responseBody?.Length > 8000 ? responseBody.Substring(0, 8000) : responseBody);
                p.Add("@cErrorMessage", errorMessage);
                p.Add("@nDurationMs", durationMs);

                await conn.ExecuteAsync("NOVVIA.spMSV3RequestLog_Insert", p, commandType: CommandType.StoredProcedure);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Fehler beim Loggen des MSV3-Requests");
            }
        }

        /// <summary>
        /// Alte Cache-Eintraege und Logs bereinigen
        /// </summary>
        public async Task CleanupCacheAsync(int cacheMaxAgeDays = 1, int logMaxAgeDays = 30)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                var p = new DynamicParameters();
                p.Add("@nCacheMaxAgeDays", cacheMaxAgeDays);
                p.Add("@nLogMaxAgeDays", logMaxAgeDays);

                var result = await conn.QuerySingleOrDefaultAsync<dynamic>(
                    "NOVVIA.spMSV3Cache_Cleanup", p, commandType: CommandType.StoredProcedure);

                if (result != null)
                {
                    _log.Information("MSV3 Cache Cleanup: {CacheDeleted} Cache-Eintraege, {LogDeleted} Log-Eintraege geloescht",
                        (int)result.CacheDeleted, (int)result.LogDeleted);
                }
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Fehler beim Cache-Cleanup");
            }
        }

        #endregion

        #region View-Abfragen (MSVE Bestand + MHD)

        /// <summary>
        /// Alle Bestellungen mit MSV3-Daten laden (Detail-View)
        /// </summary>
        public async Task<IEnumerable<Entities.BestellungMSV3Detail>> GetBestellungenMSV3DetailAsync(
            int? kLieferant = null,
            int? nStatus = null,
            DateTime? vonDatum = null,
            DateTime? bisDatum = null)
        {
            using var conn = new SqlConnection(_connectionString);

            var sql = @"SELECT * FROM NOVVIA.vNOVVIA_BestellungMSV3 WHERE 1=1";
            var p = new DynamicParameters();

            if (kLieferant.HasValue)
            {
                sql += " AND kLieferant = @kLieferant";
                p.Add("@kLieferant", kLieferant.Value);
            }

            if (nStatus.HasValue)
            {
                sql += " AND nBestellStatus = @nStatus";
                p.Add("@nStatus", nStatus.Value);
            }

            if (vonDatum.HasValue)
            {
                sql += " AND dBestellDatum >= @vonDatum";
                p.Add("@vonDatum", vonDatum.Value);
            }

            if (bisDatum.HasValue)
            {
                sql += " AND dBestellDatum <= @bisDatum";
                p.Add("@bisDatum", bisDatum.Value);
            }

            sql += " ORDER BY dBestellDatum DESC, nPosNr";

            return await conn.QueryAsync<Entities.BestellungMSV3Detail>(sql, p);
        }

        /// <summary>
        /// Bestellungen Kopf-Daten mit MSV3-Summary laden
        /// </summary>
        public async Task<IEnumerable<Entities.BestellungMSV3Kopf>> GetBestellungenMSV3KopfAsync(
            int? kLieferant = null,
            int? nStatus = null,
            bool? nurMSV3 = null)
        {
            using var conn = new SqlConnection(_connectionString);

            var sql = @"SELECT * FROM NOVVIA.vNOVVIA_BestellungMSV3Kopf WHERE 1=1";
            var p = new DynamicParameters();

            if (kLieferant.HasValue)
            {
                sql += " AND kLieferant = @kLieferant";
                p.Add("@kLieferant", kLieferant.Value);
            }

            if (nStatus.HasValue)
            {
                sql += " AND nStatus = @nStatus";
                p.Add("@nStatus", nStatus.Value);
            }

            if (nurMSV3 == true)
            {
                sql += " AND nHatMSV3 = 1";
            }

            sql += " ORDER BY dBestellDatum DESC";

            return await conn.QueryAsync<Entities.BestellungMSV3Kopf>(sql, p);
        }

        /// <summary>
        /// Einzelne Bestellung mit allen MSV3-Positionen laden
        /// </summary>
        public async Task<IEnumerable<Entities.BestellungMSV3Detail>> GetBestellungMSV3DetailAsync(int kLieferantenBestellung)
        {
            using var conn = new SqlConnection(_connectionString);

            return await conn.QueryAsync<Entities.BestellungMSV3Detail>(
                "SELECT * FROM NOVVIA.vNOVVIA_BestellungMSV3 WHERE kLieferantenBestellung = @id ORDER BY nPosNr",
                new { id = kLieferantenBestellung });
        }

        /// <summary>
        /// MSVE Bestand und MHD für alle Pharma-Artikel laden
        /// </summary>
        public async Task<IEnumerable<Entities.MSVEBestandMHD>> GetMSVEBestandMHDAsync(
            string? mhdKategorie = null,
            int? kLieferant = null,
            bool? nurKritischeMHD = null)
        {
            using var conn = new SqlConnection(_connectionString);

            var sql = @"SELECT * FROM NOVVIA.vNOVVIA_MSVEBestandMHD WHERE 1=1";
            var p = new DynamicParameters();

            if (!string.IsNullOrEmpty(mhdKategorie))
            {
                sql += " AND cMHDKategorie = @mhdKategorie";
                p.Add("@mhdKategorie", mhdKategorie);
            }

            if (kLieferant.HasValue)
            {
                sql += " AND kLieferant = @kLieferant";
                p.Add("@kLieferant", kLieferant.Value);
            }

            if (nurKritischeMHD == true)
            {
                sql += " AND cMHDKategorie IN ('ABGELAUFEN', 'KURZ')";
            }

            sql += " ORDER BY nRestlaufzeitTage ASC, cArtikelName";

            return await conn.QueryAsync<Entities.MSVEBestandMHD>(sql, p);
        }

        /// <summary>
        /// MSVE Bestand für einzelnen Artikel laden
        /// </summary>
        public async Task<Entities.MSVEBestandMHD?> GetArtikelMSVEBestandAsync(int kArtikel)
        {
            using var conn = new SqlConnection(_connectionString);

            return await conn.QuerySingleOrDefaultAsync<Entities.MSVEBestandMHD>(
                "SELECT * FROM NOVVIA.vNOVVIA_MSVEBestandMHD WHERE kArtikel = @id",
                new { id = kArtikel });
        }

        /// <summary>
        /// MSVE Bestand für Artikel über PZN laden
        /// </summary>
        public async Task<Entities.MSVEBestandMHD?> GetArtikelMSVEBestandByPZNAsync(string pzn)
        {
            using var conn = new SqlConnection(_connectionString);

            return await conn.QuerySingleOrDefaultAsync<Entities.MSVEBestandMHD>(
                "SELECT * FROM NOVVIA.vNOVVIA_MSVEBestandMHD WHERE cPZN = @pzn",
                new { pzn });
        }

        /// <summary>
        /// Artikel mit kritischem MHD (abgelaufen oder < 3 Monate) laden
        /// </summary>
        public async Task<IEnumerable<Entities.MSVEBestandMHD>> GetArtikelMitKritischemMHDAsync()
        {
            using var conn = new SqlConnection(_connectionString);

            return await conn.QueryAsync<Entities.MSVEBestandMHD>(
                @"SELECT * FROM NOVVIA.vNOVVIA_MSVEBestandMHD
                  WHERE cMHDKategorie IN ('ABGELAUFEN', 'KURZ')
                  ORDER BY nRestlaufzeitTage ASC");
        }

        /// <summary>
        /// Anzahl Artikel mit MHD-Problemen für Dashboard
        /// </summary>
        public async Task<(int Abgelaufen, int KurzMHD, int MittelMHD)> GetMHDStatistikAsync()
        {
            using var conn = new SqlConnection(_connectionString);

            var stats = await conn.QueryAsync<dynamic>(@"
                SELECT cMHDKategorie, COUNT(*) AS Anzahl
                FROM NOVVIA.vNOVVIA_MSVEBestandMHD
                WHERE cMHDKategorie IN ('ABGELAUFEN', 'KURZ', 'MITTEL')
                GROUP BY cMHDKategorie");

            var dict = stats.ToDictionary(s => (string)s.cMHDKategorie, s => (int)s.Anzahl);

            return (
                dict.GetValueOrDefault("ABGELAUFEN", 0),
                dict.GetValueOrDefault("KURZ", 0),
                dict.GetValueOrDefault("MITTEL", 0)
            );
        }

        #endregion
    }

    #region Verfuegbarkeits-Status Enums

    /// <summary>
    /// Status-Codes fuer Grosshandel-Verfuegbarkeit (JTL-konform)
    /// </summary>
    public static class MSV3VerfuegbarkeitStatus
    {
        public const string SOFORT_LIEFERBAR = "SOFORT_LIEFERBAR";
        public const string TEILLIEFERBAR = "TEILLIEFERBAR";
        public const string NACHLIEFERUNG_MOEGLICH = "NACHLIEFERUNG_MOEGLICH";
        public const string NICHT_LIEFERBAR = "NICHT_LIEFERBAR";
        public const string UNBEKANNT = "UNBEKANNT";
    }

    #endregion

    #region DTOs

    public class MSV3Lieferant
    {
        public int KMSV3Lieferant { get; set; }
        public int KLieferant { get; set; }
        public string? LieferantName { get; set; }
        public string MSV3Url { get; set; } = "";
        public string MSV3Benutzer { get; set; } = "";
        public string MSV3Passwort { get; set; } = "";
        public string? MSV3Kundennummer { get; set; }
        public string? MSV3Filiale { get; set; }
        public int MSV3Version { get; set; } = 1;
        public int Prioritaet { get; set; } = 1;
        public bool Aktiv { get; set; } = true;
        // Aliasse für LieferantenPage
        public string CUrl { get => MSV3Url; set => MSV3Url = value; }
        public string CMSV3Url { get => MSV3Url; set => MSV3Url = value; }
        public string CBenutzer { get => MSV3Benutzer; set => MSV3Benutzer = value; }
        public string CMSV3Benutzer { get => MSV3Benutzer; set => MSV3Benutzer = value; }
        public string CPasswort { get => MSV3Passwort; set => MSV3Passwort = value; }
        public string CMSV3Passwort { get => MSV3Passwort; set => MSV3Passwort = value; }
        public string? CKundennummer { get => MSV3Kundennummer; set => MSV3Kundennummer = value; }
        public string? CMSV3Kundennummer { get => MSV3Kundennummer; set => MSV3Kundennummer = value; }
        public string? CFiliale { get => MSV3Filiale; set => MSV3Filiale = value; }
        public string? CMSV3Filiale { get => MSV3Filiale; set => MSV3Filiale = value; }
        public bool NAktiv { get => Aktiv; set => Aktiv = value; }
        public int NMSV3Version { get => MSV3Version; set => MSV3Version = value; }
    }

    public class MSV3ArtikelAnfrage
    {
        public string PZN { get; set; } = "";
        public decimal Menge { get; set; }
        public string? Id { get; set; }
        public DateTime? MinMHD { get; set; }  // Mindest-Haltbarkeitsdatum
    }

    public class MSV3BestellPosition
    {
        public string PZN { get; set; } = "";
        public int Menge { get; set; }
        public string? LieferantenArtNr { get; set; }
        public DateTime? MinMHD { get; set; }  // Mindest-MHD für Bestellung
    }

    public class MSV3VerfuegbarkeitResult
    {
        public bool Success { get; set; }
        public string? Fehler { get; set; }
        public List<MSV3VerfuegbarkeitPosition> Positionen { get; set; } = new();
        public int AnzahlVerfuegbar { get; set; }
        public int AnzahlTeilweise { get; set; }
        public int AnzahlNichtVerfuegbar { get; set; }
    }

    public class MSV3VerfuegbarkeitPosition
    {
        public string PZN { get; set; } = "";
        public string? Id { get; set; }
        public decimal MengeAngefragt { get; set; }
        public decimal MengeVerfuegbar { get; set; }
        public decimal PreisEK { get; set; }
        public decimal PreisAEP { get; set; }
        public decimal PreisAVP { get; set; }
        public bool Verfuegbar { get; set; }
        public string Status { get; set; } = "";  // VERFUEGBAR, TEILWEISE, NICHT_VERFUEGBAR
        public string? Hinweis { get; set; }
        public string? ChargenNr { get; set; }
        public DateTime? MHD { get; set; }
        public string? Lieferzeit { get; set; }

        // Für UI-Anzeige
        public int Bestand => (int)MengeVerfuegbar;
    }

    public class MSV3BestellungResult
    {
        public bool Success { get; set; }
        public string? Fehler { get; set; }
        public string? Fehlermeldung => Fehler; // Alias
        public string? AuftragsId { get; set; }
        public string? MSV3Bestellnummer => AuftragsId; // Alias
        public string? Status { get; set; }
    }

    public class MSV3Response
    {
        public bool Success { get; set; }
        public string? ResponseXml { get; set; }
        public string? Fehler { get; set; }
    }

    public class MSV3VerbindungResult
    {
        public bool Success { get; set; }
        public string? Meldung { get; set; }
        public string? Fehler { get; set; }
        public string? ResponseXml { get; set; }
    }

    /// <summary>
    /// Geparste Verfuegbarkeits-Antwort von bestellen-Operation (GEHE-Format)
    /// </summary>
    public class MSV3BestellenVerfuegbarkeitResult
    {
        public bool Success { get; set; }
        public string? Fehler { get; set; }
        public string? ResponseXml { get; set; }
        public string? BestellSupportId { get; set; }
        public bool NachtBetrieb { get; set; }
        public List<MSV3BestellenPosition> Positionen { get; set; } = new();
    }

    /// <summary>
    /// Einzelne Position aus bestellen-Response
    /// </summary>
    public class MSV3BestellenPosition
    {
        public string PZN { get; set; } = "";
        public int BestellMenge { get; set; }
        public string? Liefervorgabe { get; set; }
        public List<MSV3BestellenAnteil> Anteile { get; set; } = new();

        // Berechnete Felder
        public string StatusCode => BerechneStatus();
        public int VerfuegbareMenge => Anteile.Where(a => IstSofortLieferbar(a.Typ)).Sum(a => a.Menge);
        public string? HauptGrund => Anteile.FirstOrDefault()?.Grund;
        public string? HauptTyp => Anteile.FirstOrDefault()?.Typ;
        public DateTime? NaechsterLieferzeitpunkt => Anteile
            .Where(a => a.Lieferzeitpunkt.HasValue)
            .OrderBy(a => a.Lieferzeitpunkt)
            .FirstOrDefault()?.Lieferzeitpunkt;

        private string BerechneStatus()
        {
            if (Anteile.Count == 0)
                return MSV3VerfuegbarkeitStatus.UNBEKANNT;

            int sofortLieferbar = Anteile.Where(a => IstSofortLieferbar(a.Typ)).Sum(a => a.Menge);

            if (sofortLieferbar >= BestellMenge)
                return MSV3VerfuegbarkeitStatus.SOFORT_LIEFERBAR;

            if (sofortLieferbar > 0)
                return MSV3VerfuegbarkeitStatus.TEILLIEFERBAR;

            // Keine sofortige Lieferung moeglich - pruefen ob Nachlieferung
            if (Anteile.Any(a => a.Typ?.Contains("NachlieferungMoeglich", StringComparison.OrdinalIgnoreCase) == true))
                return MSV3VerfuegbarkeitStatus.NACHLIEFERUNG_MOEGLICH;

            if (Anteile.Any(a => a.Typ?.StartsWith("KeineLieferung", StringComparison.OrdinalIgnoreCase) == true))
                return MSV3VerfuegbarkeitStatus.NICHT_LIEFERBAR;

            return MSV3VerfuegbarkeitStatus.UNBEKANNT;
        }

        private static bool IstSofortLieferbar(string? typ)
        {
            if (string.IsNullOrEmpty(typ)) return false;
            // Typen die sofortige Lieferung bedeuten
            return typ.Contains("Lieferung", StringComparison.OrdinalIgnoreCase)
                && !typ.Contains("KeineLieferung", StringComparison.OrdinalIgnoreCase)
                && !typ.Contains("Nachlieferung", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Anteil aus bestellen-Response (Lieferaufteilung)
    /// </summary>
    public class MSV3BestellenAnteil
    {
        public int Menge { get; set; }
        public string? Typ { get; set; }           // z.B. "KeineLieferungAberNachlieferungMoeglich"
        public string? Grund { get; set; }         // z.B. "HerstellerNichtLieferbar"
        public DateTime? Lieferzeitpunkt { get; set; }
        public bool Tourabweichung { get; set; }
    }

    /// <summary>
    /// Cache-Eintrag fuer Verfuegbarkeitsabfrage
    /// </summary>
    public class MSV3VerfuegbarkeitCacheEntry
    {
        public int Id { get; set; }
        public string PZN { get; set; } = "";
        public int KLieferant { get; set; }
        public int RequestedQty { get; set; }
        public string StatusCode { get; set; } = "";
        public int? AvailableQty { get; set; }
        public string? ReasonCode { get; set; }
        public DateTime? NextDeliveryUtc { get; set; }
        public string? RawTyp { get; set; }
        public DateTime LastCheckedUtc { get; set; }
        public DateTime ValidUntilUtc { get; set; }
        public bool IsValid { get; set; }

        // Aliase fuer Dapper (SQL-Spaltennamen)
        public int KMSV3VerfuegbarkeitCache { get => Id; set => Id = value; }
        public string CPZN { get => PZN; set => PZN = value; }
        public int NRequestedQty { get => RequestedQty; set => RequestedQty = value; }
        public string CStatusCode { get => StatusCode; set => StatusCode = value; }
        public int? NAvailableQty { get => AvailableQty; set => AvailableQty = value; }
        public string? CReasonCode { get => ReasonCode; set => ReasonCode = value; }
        public DateTime? DNextDeliveryUtc { get => NextDeliveryUtc; set => NextDeliveryUtc = value; }
        public string? CRawTyp { get => RawTyp; set => RawTyp = value; }
        public DateTime DLastCheckedUtc { get => LastCheckedUtc; set => LastCheckedUtc = value; }
        public DateTime DValidUntilUtc { get => ValidUntilUtc; set => ValidUntilUtc = value; }
        public bool NIsValid { get => IsValid; set => IsValid = value; }
    }

    #endregion
}

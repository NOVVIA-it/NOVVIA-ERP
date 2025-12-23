using System.Data;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;
using Microsoft.Data.SqlClient;

namespace NovviaERP.Worker.Jobs;

/// <summary>
/// MSV3 Bestandsabfrage für eine einzelne PZN
/// Aufruf: NovviaERP.Worker.exe --mode msv3-stock --pzn 14036711
/// </summary>
public sealed class Msv3SinglePznJob
{
    private readonly string _connectionString;
    private static readonly HttpClient _httpClient = new();

    public Msv3SinglePznJob(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<int> RunAsync(string pzn)
    {
        Console.WriteLine($"[MSV3] Starte Bestandsabfrage für PZN={pzn}");

        var cred = await GetMsv3CredentialsForPznAsync(pzn);
        if (cred is null)
        {
            Console.WriteLine($"[MSV3] Kein MSV3-Lieferant für PZN={pzn} gefunden (tLiefArtikel + NOVVIA.MSV3Lieferant)");
            return 1;
        }

        Console.WriteLine($"[MSV3] Lieferant gefunden: kLieferant={cred.SupplierId}, URL={cred.Url}");

        // Cache prüfen (TTL 5 Minuten)
        var cached = await GetCachedStockAsync(pzn, cred.SupplierId);
        if (cached != null)
        {
            Console.WriteLine($"[MSV3] Cache-Hit für PZN={pzn}: Bestand={cached.Bestand}, Verfuegbar={cached.Verfuegbar}");
            return 0;
        }

        // MSV3 Abfrage
        var result = await QueryMsv3StockAsync(pzn, cred);
        if (result != null)
        {
            Console.WriteLine($"[MSV3] Ergebnis für PZN={pzn}: Bestand={result.Bestand}, Verfuegbar={result.Verfuegbar}, Status={result.Status}");

            // In Cache speichern
            await SaveStockToCacheAsync(pzn, cred.SupplierId, result);
        }
        else
        {
            Console.WriteLine($"[MSV3] Keine Verfügbarkeit für PZN={pzn}");
        }

        return 0;
    }

    private async Task<Msv3Cred?> GetMsv3CredentialsForPznAsync(string pzn)
    {
        const string sql = @"
SELECT TOP (1)
    ms.kLieferant,
    ms.cMSV3Url,
    ms.cMSV3Benutzer,
    ms.cMSV3Passwort,
    ms.cMSV3Kundennummer,
    ms.cMSV3Filiale,
    ms.nMSV3Version
FROM dbo.tArtikel a
JOIN dbo.tLiefArtikel la ON la.tArtikel_kArtikel = a.kArtikel
JOIN NOVVIA.MSV3Lieferant ms ON ms.kLieferant = la.tLieferant_kLieferant
WHERE a.cArtNr = @pzn AND ms.nAktiv = 1
ORDER BY ISNULL(la.nStandard,0) DESC, ISNULL(ms.nPrioritaet,9999) ASC, la.kLiefArtikel DESC;";

        await using var con = new SqlConnection(_connectionString);
        await con.OpenAsync();

        await using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.Add(new SqlParameter("@pzn", SqlDbType.NVarChar, 255) { Value = pzn });

        await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
        if (!await r.ReadAsync()) return null;

        return new Msv3Cred
        {
            SupplierId = r.GetInt32(0),
            Url = r.GetString(1),
            User = r.GetString(2),
            Pass = r.GetString(3),
            CustomerNo = r.IsDBNull(4) ? null : r.GetString(4),
            Branch = r.IsDBNull(5) ? null : r.GetString(5),
            Version = r.IsDBNull(6) ? 1 : r.GetInt32(6),
        };
    }

    private async Task<StockResult?> GetCachedStockAsync(string pzn, int supplierId)
    {
        const string sql = @"
SELECT nBestand, nVerfuegbar, cStatus, dAbfrage
FROM NOVVIA.MSV3BestandCache
WHERE cPzn = @pzn AND kLieferant = @supplierId
  AND dAbfrage > DATEADD(MINUTE, -5, GETDATE())";

        try
        {
            await using var con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            await using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@pzn", pzn);
            cmd.Parameters.AddWithValue("@supplierId", supplierId);

            await using var r = await cmd.ExecuteReaderAsync(CommandBehavior.SingleRow);
            if (!await r.ReadAsync()) return null;

            return new StockResult
            {
                Bestand = r.IsDBNull(0) ? 0 : r.GetInt32(0),
                Verfuegbar = r.IsDBNull(1) ? false : r.GetInt32(1) > 0,
                Status = r.IsDBNull(2) ? null : r.GetString(2)
            };
        }
        catch
        {
            // Tabelle existiert möglicherweise nicht - kein Fehler
            return null;
        }
    }

    private async Task SaveStockToCacheAsync(string pzn, int supplierId, StockResult result)
    {
        const string sql = @"
IF EXISTS (SELECT 1 FROM NOVVIA.MSV3BestandCache WHERE cPzn = @pzn AND kLieferant = @supplierId)
    UPDATE NOVVIA.MSV3BestandCache
    SET nBestand = @bestand, nVerfuegbar = @verfuegbar, cStatus = @status, dAbfrage = GETDATE()
    WHERE cPzn = @pzn AND kLieferant = @supplierId
ELSE
    INSERT INTO NOVVIA.MSV3BestandCache (cPzn, kLieferant, nBestand, nVerfuegbar, cStatus, dAbfrage)
    VALUES (@pzn, @supplierId, @bestand, @verfuegbar, @status, GETDATE())";

        try
        {
            await using var con = new SqlConnection(_connectionString);
            await con.OpenAsync();

            await using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@pzn", pzn);
            cmd.Parameters.AddWithValue("@supplierId", supplierId);
            cmd.Parameters.AddWithValue("@bestand", result.Bestand);
            cmd.Parameters.AddWithValue("@verfuegbar", result.Verfuegbar ? 1 : 0);
            cmd.Parameters.AddWithValue("@status", (object?)result.Status ?? DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MSV3] Cache-Speichern fehlgeschlagen: {ex.Message}");
        }
    }

    private async Task<StockResult?> QueryMsv3StockAsync(string pzn, Msv3Cred cred)
    {
        try
        {
            var version = cred.Version ?? 1;
            var endpoint = version == 2
                ? $"{cred.Url.TrimEnd('/')}/v2.0/VerfuegbarkeitAnfragen"
                : $"{cred.Url.TrimEnd('/')}/v1.0/verfuegbarkeitAnfragenBulk";

            var soapRequest = BuildSoapRequest(pzn, cred, version);

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Content = new StringContent(soapRequest, Encoding.UTF8, "application/soap+xml");

            // HTTP Basic Auth
            var authBytes = Encoding.ASCII.GetBytes($"{cred.User}:{cred.Pass}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
            request.Headers.Add("User-Agent", "Embarcadero URI Client/1.0");

            Console.WriteLine($"[MSV3] Request an {endpoint}");

            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"[MSV3] Response Status: {response.StatusCode}");

            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.InternalServerError)
            {
                Console.WriteLine($"[MSV3] HTTP-Fehler: {response.StatusCode}");
                return null;
            }

            // SOAP Response parsen
            return ParseSoapResponse(responseBody);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MSV3] Fehler: {ex.Message}");
            return null;
        }
    }

    private static string BuildSoapRequest(string pzn, Msv3Cred cred, int version)
    {
        var ns = version == 2 ? "urn:msv3:v2" : "urn:msv3:v1";
        var operation = version == 2 ? "VerfuegbarkeitAnfragen" : "verfuegbarkeitAnfragenBulk";

        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<soap:Envelope xmlns:soap=""http://www.w3.org/2003/05/soap-envelope"">
   <soap:Body>
      <{operation} xmlns=""{ns}"">
         <clientSoftwareKennung>NovviaERP</clientSoftwareKennung>
         <Benutzerkennung>{cred.User}</Benutzerkennung>
         <Kennwort>{cred.Pass}</Kennwort>
         <anfrage Id=""{Guid.NewGuid()}"">
            <Positionen>
               <Pzn>{pzn}</Pzn>
               <Menge>1</Menge>
               <Liefervorgabe>Normal</Liefervorgabe>
            </Positionen>
         </anfrage>
      </{operation}>
   </soap:Body>
</soap:Envelope>";
    }

    private static StockResult? ParseSoapResponse(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);

            // Prüfen auf SOAP Fault
            var fault = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Fault");
            if (fault != null)
            {
                var reason = fault.Descendants().FirstOrDefault(e => e.Name.LocalName == "Text")?.Value;
                Console.WriteLine($"[MSV3] SOAP-Fault: {reason}");
                return null;
            }

            // Verfügbarkeit parsen
            var verfuegbar = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Verfuegbarkeit");
            if (verfuegbar != null)
            {
                var menge = verfuegbar.Descendants().FirstOrDefault(e => e.Name.LocalName == "Menge")?.Value;
                var status = verfuegbar.Descendants().FirstOrDefault(e => e.Name.LocalName == "Lieferstatus")?.Value;

                int.TryParse(menge, out int bestand);

                return new StockResult
                {
                    Bestand = bestand,
                    Verfuegbar = bestand > 0,
                    Status = status
                };
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[MSV3] XML-Parse-Fehler: {ex.Message}");
            return null;
        }
    }

    private sealed class Msv3Cred
    {
        public int SupplierId { get; set; }
        public string Url { get; set; } = "";
        public string User { get; set; } = "";
        public string Pass { get; set; } = "";
        public string? CustomerNo { get; set; }
        public string? Branch { get; set; }
        public int? Version { get; set; }
    }

    private sealed class StockResult
    {
        public int Bestand { get; set; }
        public bool Verfuegbar { get; set; }
        public string? Status { get; set; }
    }
}

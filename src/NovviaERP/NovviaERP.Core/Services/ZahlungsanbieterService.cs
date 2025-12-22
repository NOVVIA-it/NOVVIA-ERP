using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Dapper;
using NovviaERP.Core.Data;
using NovviaERP.Core.Entities;
using Serilog;

namespace NovviaERP.Core.Services
{
    /// <summary>
    /// Service für Zahlungsanbieter-Integration (PayPal, Stripe, Mollie, Klarna etc.)
    /// </summary>
    public class ZahlungsanbieterService : IDisposable
    {
        private readonly JtlDbContext _db;
        private readonly HttpClient _http;
        private static readonly ILogger _log = Log.ForContext<ZahlungsanbieterService>();

        public ZahlungsanbieterService(JtlDbContext db)
        {
            _db = db;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        }

        public void Dispose() => _http.Dispose();

        #region Anbieter-Verwaltung
        public async Task<IEnumerable<Zahlungsanbieter>> GetAnbieterAsync(bool nurAktive = true)
        {
            var conn = await _db.GetConnectionAsync();
            var sql = "SELECT * FROM tZahlungsanbieter";
            if (nurAktive) sql += " WHERE nAktiv = 1";
            sql += " ORDER BY cName";
            return await conn.QueryAsync<Zahlungsanbieter>(sql);
        }

        public async Task<Zahlungsanbieter?> GetAnbieterByIdAsync(int id)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QuerySingleOrDefaultAsync<Zahlungsanbieter>(
                "SELECT * FROM tZahlungsanbieter WHERE kZahlungsanbieter = @Id", new { Id = id });
        }

        public async Task<int> CreateAnbieterAsync(Zahlungsanbieter anbieter)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QuerySingleAsync<int>(@"
                INSERT INTO tZahlungsanbieter (cName, cAnbieterTyp, cApiUrl, cApiKey, cApiSecret, 
                    cMerchantId, cWebhookSecret, nTestmodus, nAktiv, cKonfiguration)
                VALUES (@Name, @Typ, @ApiUrl, @ApiKey, @ApiSecret, @MerchantId, @WebhookSecret, 
                    @Testmodus, @Aktiv, @KonfigurationJson);
                SELECT SCOPE_IDENTITY();", anbieter);
        }

        public async Task UpdateAnbieterAsync(Zahlungsanbieter anbieter)
        {
            var conn = await _db.GetConnectionAsync();
            await conn.ExecuteAsync(@"
                UPDATE tZahlungsanbieter SET cName = @Name, cApiUrl = @ApiUrl, cApiKey = @ApiKey,
                    cApiSecret = @ApiSecret, cMerchantId = @MerchantId, cWebhookSecret = @WebhookSecret,
                    nTestmodus = @Testmodus, nAktiv = @Aktiv, cKonfiguration = @KonfigurationJson
                WHERE kZahlungsanbieter = @Id", anbieter);
        }

        public async Task<IEnumerable<ZahlungsanbieterMethode>> GetMethodenAsync(int anbieterId)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QueryAsync<ZahlungsanbieterMethode>(
                "SELECT * FROM tZahlungsanbieterMethode WHERE kZahlungsanbieter = @Id AND nAktiv = 1 ORDER BY nSortierung",
                new { Id = anbieterId });
        }
        #endregion

        #region PayPal
        public async Task<ZahlungsanbieterTransaktion> CreatePayPalPaymentAsync(int anbieterId, decimal betrag, string waehrung, 
            string beschreibung, string returnUrl, string cancelUrl, int? bestellungId = null)
        {
            var anbieter = await GetAnbieterByIdAsync(anbieterId);
            if (anbieter == null || anbieter.Typ != ZahlungsanbieterTyp.PayPal)
                throw new ArgumentException("PayPal-Anbieter nicht gefunden");

            var baseUrl = anbieter.Testmodus 
                ? "https://api-m.sandbox.paypal.com" 
                : "https://api-m.paypal.com";

            // Token holen
            var tokenRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/oauth2/token");
            tokenRequest.Headers.Add("Authorization", $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{anbieter.ApiKey}:{anbieter.ApiSecret}"))}");
            tokenRequest.Content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded");
            var tokenResponse = await _http.SendAsync(tokenRequest);
            var tokenData = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
            var accessToken = tokenData.GetProperty("access_token").GetString();

            // Payment erstellen
            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

            var order = new
            {
                intent = "CAPTURE",
                purchase_units = new[] { new {
                    amount = new { currency_code = waehrung, value = betrag.ToString("F2") },
                    description = beschreibung
                }},
                application_context = new {
                    return_url = returnUrl,
                    cancel_url = cancelUrl
                }
            };

            var response = await _http.PostAsJsonAsync($"{baseUrl}/v2/checkout/orders", order);
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();

            var transaktionsId = result.GetProperty("id").GetString() ?? "";
            var approveUrl = result.GetProperty("links").EnumerateArray()
                .FirstOrDefault(l => l.GetProperty("rel").GetString() == "approve")
                .GetProperty("href").GetString();

            // Transaktion speichern
            var transaktion = new ZahlungsanbieterTransaktion
            {
                ZahlungsanbieterId = anbieterId,
                BestellungId = bestellungId,
                TransaktionsId = transaktionsId,
                Status = ZahlungstransaktionStatus.Ausstehend,
                Betrag = betrag,
                Waehrung = waehrung,
                Methode = "paypal",
                RawResponse = result.ToString()
            };

            var conn = await _db.GetConnectionAsync();
            transaktion.Id = await conn.QuerySingleAsync<int>(@"
                INSERT INTO tZahlungsanbieterTransaktion (kZahlungsanbieter, kBestellung, cTransaktionsId, 
                    cStatus, fBetrag, cWaehrung, cMethode, dErstellt, cRawResponse)
                VALUES (@ZahlungsanbieterId, @BestellungId, @TransaktionsId, @Status, @Betrag, 
                    @Waehrung, @Methode, GETDATE(), @RawResponse);
                SELECT SCOPE_IDENTITY();", transaktion);

            _log.Information("PayPal Order erstellt: {OrderId}", transaktionsId);
            return transaktion;
        }

        public async Task<ZahlungsanbieterTransaktion> CapturePayPalPaymentAsync(int transaktionId)
        {
            var conn = await _db.GetConnectionAsync();
            var transaktion = await conn.QuerySingleOrDefaultAsync<ZahlungsanbieterTransaktion>(
                "SELECT * FROM tZahlungsanbieterTransaktion WHERE kZahlungsanbieterTransaktion = @Id", 
                new { Id = transaktionId });

            if (transaktion == null) throw new ArgumentException("Transaktion nicht gefunden");

            var anbieter = await GetAnbieterByIdAsync(transaktion.ZahlungsanbieterId);
            if (anbieter == null) throw new ArgumentException("Anbieter nicht gefunden");

            var baseUrl = anbieter.Testmodus ? "https://api-m.sandbox.paypal.com" : "https://api-m.paypal.com";

            // Token holen (wie oben)
            var tokenRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/oauth2/token");
            tokenRequest.Headers.Add("Authorization", $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{anbieter.ApiKey}:{anbieter.ApiSecret}"))}");
            tokenRequest.Content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded");
            var tokenResponse = await _http.SendAsync(tokenRequest);
            var tokenData = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
            var accessToken = tokenData.GetProperty("access_token").GetString();

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

            var response = await _http.PostAsync($"{baseUrl}/v2/checkout/orders/{transaktion.TransaktionsId}/capture", new StringContent("{}", Encoding.UTF8, "application/json"));
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();

            var status = result.GetProperty("status").GetString();
            transaktion.Status = status == "COMPLETED" ? ZahlungstransaktionStatus.Bezahlt : ZahlungstransaktionStatus.Fehlgeschlagen;
            transaktion.Abgeschlossen = DateTime.Now;
            transaktion.RawResponse = result.ToString();

            if (result.TryGetProperty("payer", out var payer))
            {
                transaktion.PayerEmail = payer.GetProperty("email_address").GetString();
                transaktion.PayerId = payer.GetProperty("payer_id").GetString();
            }

            await conn.ExecuteAsync(@"
                UPDATE tZahlungsanbieterTransaktion SET cStatus = @Status, dAbgeschlossen = @Abgeschlossen,
                    cPayerEmail = @PayerEmail, cPayerId = @PayerId, cRawResponse = @RawResponse
                WHERE kZahlungsanbieterTransaktion = @Id", transaktion);

            _log.Information("PayPal Payment captured: {OrderId} = {Status}", transaktion.TransaktionsId, status);
            return transaktion;
        }
        #endregion

        #region Stripe
        public async Task<ZahlungsanbieterTransaktion> CreateStripePaymentIntentAsync(int anbieterId, decimal betrag, 
            string waehrung, int? bestellungId = null, string? kundeEmail = null)
        {
            var anbieter = await GetAnbieterByIdAsync(anbieterId);
            if (anbieter == null || anbieter.Typ != ZahlungsanbieterTyp.Stripe)
                throw new ArgumentException("Stripe-Anbieter nicht gefunden");

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {anbieter.ApiKey}");

            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["amount"] = ((int)(betrag * 100)).ToString(), // Stripe arbeitet mit Cents
                ["currency"] = waehrung.ToLower(),
                ["receipt_email"] = kundeEmail ?? ""
            });

            var response = await _http.PostAsync("https://api.stripe.com/v1/payment_intents", content);
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();

            var transaktion = new ZahlungsanbieterTransaktion
            {
                ZahlungsanbieterId = anbieterId,
                BestellungId = bestellungId,
                TransaktionsId = result.GetProperty("id").GetString() ?? "",
                Status = ZahlungstransaktionStatus.Ausstehend,
                Betrag = betrag,
                Waehrung = waehrung,
                Methode = "stripe",
                PayerEmail = kundeEmail,
                RawResponse = result.ToString()
            };

            var conn = await _db.GetConnectionAsync();
            transaktion.Id = await conn.QuerySingleAsync<int>(@"
                INSERT INTO tZahlungsanbieterTransaktion (kZahlungsanbieter, kBestellung, cTransaktionsId, 
                    cStatus, fBetrag, cWaehrung, cMethode, cPayerEmail, dErstellt, cRawResponse)
                VALUES (@ZahlungsanbieterId, @BestellungId, @TransaktionsId, @Status, @Betrag, 
                    @Waehrung, @Methode, @PayerEmail, GETDATE(), @RawResponse);
                SELECT SCOPE_IDENTITY();", transaktion);

            return transaktion;
        }
        #endregion

        #region Mollie
        public async Task<ZahlungsanbieterTransaktion> CreateMolliePaymentAsync(int anbieterId, decimal betrag,
            string waehrung, string beschreibung, string redirectUrl, int? bestellungId = null)
        {
            var anbieter = await GetAnbieterByIdAsync(anbieterId);
            if (anbieter == null || anbieter.Typ != ZahlungsanbieterTyp.Mollie)
                throw new ArgumentException("Mollie-Anbieter nicht gefunden");

            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {anbieter.ApiKey}");

            var payment = new
            {
                amount = new { currency = waehrung, value = betrag.ToString("F2") },
                description = beschreibung,
                redirectUrl = redirectUrl,
                metadata = new { order_id = bestellungId }
            };

            var response = await _http.PostAsJsonAsync("https://api.mollie.com/v2/payments", payment);
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();

            var transaktion = new ZahlungsanbieterTransaktion
            {
                ZahlungsanbieterId = anbieterId,
                BestellungId = bestellungId,
                TransaktionsId = result.GetProperty("id").GetString() ?? "",
                Status = ZahlungstransaktionStatus.Ausstehend,
                Betrag = betrag,
                Waehrung = waehrung,
                Methode = "mollie",
                RawResponse = result.ToString()
            };

            var conn = await _db.GetConnectionAsync();
            transaktion.Id = await conn.QuerySingleAsync<int>(@"
                INSERT INTO tZahlungsanbieterTransaktion (kZahlungsanbieter, kBestellung, cTransaktionsId, 
                    cStatus, fBetrag, cWaehrung, cMethode, dErstellt, cRawResponse)
                VALUES (@ZahlungsanbieterId, @BestellungId, @TransaktionsId, @Status, @Betrag, 
                    @Waehrung, @Methode, GETDATE(), @RawResponse);
                SELECT SCOPE_IDENTITY();", transaktion);

            return transaktion;
        }
        #endregion

        #region Transaktionen
        public async Task<IEnumerable<ZahlungsanbieterTransaktion>> GetTransaktionenAsync(
            int? anbieterId = null, int? bestellungId = null, ZahlungstransaktionStatus? status = null)
        {
            var conn = await _db.GetConnectionAsync();
            var sql = "SELECT * FROM tZahlungsanbieterTransaktion WHERE 1=1";
            if (anbieterId.HasValue) sql += " AND kZahlungsanbieter = @AnbieterId";
            if (bestellungId.HasValue) sql += " AND kBestellung = @BestellungId";
            if (status.HasValue) sql += " AND cStatus = @Status";
            sql += " ORDER BY dErstellt DESC";

            return await conn.QueryAsync<ZahlungsanbieterTransaktion>(sql,
                new { AnbieterId = anbieterId, BestellungId = bestellungId, Status = status });
        }

        public async Task UpdateTransaktionStatusAsync(int transaktionId, ZahlungstransaktionStatus status)
        {
            var conn = await _db.GetConnectionAsync();
            await conn.ExecuteAsync(@"
                UPDATE tZahlungsanbieterTransaktion SET cStatus = @Status, 
                    dAbgeschlossen = CASE WHEN @Status IN (2,3,4,5,6,7) THEN GETDATE() ELSE dAbgeschlossen END
                WHERE kZahlungsanbieterTransaktion = @Id",
                new { Id = transaktionId, Status = status });
        }
        #endregion
    }
}

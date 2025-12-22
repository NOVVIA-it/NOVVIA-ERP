using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NovviaERP.Core.Data;
using NovviaERP.Core.Entities;
using Serilog;

namespace NovviaERP.Core.Services
{
    public class PaymentService : IDisposable
    {
        private readonly JtlDbContext _db;
        private readonly PaymentConfig _config;
        private readonly HttpClient _http;
        private static readonly ILogger _log = Log.ForContext<PaymentService>();

        public PaymentService(JtlDbContext db, PaymentConfig config) { _db = db; _config = config; _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) }; }
        public void Dispose() => _http.Dispose();

        #region PayPal
        public async Task<string> GetPayPalTokenAsync()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "https://api-m.paypal.com/v1/oauth2/token");
            request.Headers.Add("Authorization", $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_config.PayPalClientId}:{_config.PayPalSecret}"))}");
            request.Content = new StringContent("grant_type=client_credentials", Encoding.UTF8, "application/x-www-form-urlencoded");
            var response = await _http.SendAsync(request);
            var data = await response.Content.ReadFromJsonAsync<JsonElement>();
            return data.GetProperty("access_token").GetString() ?? "";
        }

        public async Task<List<PayPalTransaction>> GetPayPalTransactionsAsync(DateTime von, DateTime bis)
        {
            var list = new List<PayPalTransaction>();
            try
            {
                var token = await GetPayPalTokenAsync();
                _http.DefaultRequestHeaders.Clear();
                _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
                var url = $"https://api-m.paypal.com/v1/reporting/transactions?start_date={von:yyyy-MM-ddTHH:mm:ssZ}&end_date={bis:yyyy-MM-ddTHH:mm:ssZ}&fields=all";
                var response = await _http.GetFromJsonAsync<JsonElement>(url);
                if (response.TryGetProperty("transaction_details", out var details))
                    foreach (var tx in details.EnumerateArray())
                    {
                        var info = tx.GetProperty("transaction_info");
                        list.Add(new PayPalTransaction
                        {
                            TransactionId = info.GetProperty("transaction_id").GetString() ?? "",
                            Amount = decimal.Parse(info.GetProperty("transaction_amount").GetProperty("value").GetString() ?? "0"),
                            Currency = info.GetProperty("transaction_amount").GetProperty("currency_code").GetString() ?? "EUR",
                            Status = info.GetProperty("transaction_status").GetString() ?? "",
                            Date = DateTime.Parse(info.GetProperty("transaction_initiation_date").GetString() ?? DateTime.Now.ToString()),
                            PayerEmail = tx.TryGetProperty("payer_info", out var p) ? p.GetProperty("email_address").GetString() : null,
                            Note = info.TryGetProperty("transaction_note", out var n) ? n.GetString() : null
                        });
                    }
            }
            catch (Exception ex) { _log.Error(ex, "PayPal Fehler"); }
            return list;
        }
        #endregion

        #region Mollie
        public async Task<List<MolliePayment>> GetMolliePaymentsAsync(DateTime von)
        {
            var list = new List<MolliePayment>();
            try
            {
                _http.DefaultRequestHeaders.Clear();
                _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.MollieApiKey}");
                var response = await _http.GetFromJsonAsync<JsonElement>("https://api.mollie.com/v2/payments?limit=250");
                if (response.TryGetProperty("_embedded", out var emb) && emb.TryGetProperty("payments", out var payments))
                    foreach (var p in payments.EnumerateArray())
                    {
                        var created = DateTime.Parse(p.GetProperty("createdAt").GetString() ?? DateTime.Now.ToString());
                        if (created < von) continue;
                        list.Add(new MolliePayment
                        {
                            Id = p.GetProperty("id").GetString() ?? "",
                            Amount = decimal.Parse(p.GetProperty("amount").GetProperty("value").GetString() ?? "0"),
                            Status = p.GetProperty("status").GetString() ?? "",
                            Method = p.TryGetProperty("method", out var m) ? m.GetString() : null,
                            Description = p.TryGetProperty("description", out var d) ? d.GetString() : null,
                            CreatedAt = created,
                            OrderId = p.TryGetProperty("metadata", out var meta) && meta.TryGetProperty("order_id", out var oid) ? oid.GetString() : null
                        });
                    }
            }
            catch (Exception ex) { _log.Error(ex, "Mollie Fehler"); }
            return list;
        }
        #endregion

        #region Matching
        public async Task<int> ProcessPaymentsAsync()
        {
            int count = 0;
            var offene = (await _db.GetOffeneRechnungenAsync()).ToList();
            var paypal = await GetPayPalTransactionsAsync(DateTime.Now.AddDays(-7), DateTime.Now);
            foreach (var tx in paypal.Where(t => t.Status == "S" && t.Amount > 0))
            {
                var match = MatchRechnung(offene, tx.Amount, tx.Note);
                if (match != null) { await _db.BucheZahlungseingangAsync(match.Id, tx.Amount, $"PayPal:{tx.TransactionId}"); count++; offene.Remove(match); }
            }
            var mollie = await GetMolliePaymentsAsync(DateTime.Now.AddDays(-7));
            foreach (var p in mollie.Where(p => p.Status == "paid"))
            {
                var match = MatchRechnung(offene, p.Amount, p.Description ?? p.OrderId);
                if (match != null) { await _db.BucheZahlungseingangAsync(match.Id, p.Amount, $"Mollie:{p.Id}"); count++; offene.Remove(match); }
            }
            _log.Information("Payments: {Count} gebucht", count);
            return count;
        }

        private Rechnung? MatchRechnung(List<Rechnung> rechnungen, decimal betrag, string? text)
        {
            if (string.IsNullOrEmpty(text)) return rechnungen.FirstOrDefault(r => Math.Abs(r.Offen - betrag) < 0.01m);
            foreach (var r in rechnungen)
                if (text.Contains(r.RechnungsNr, StringComparison.OrdinalIgnoreCase) && Math.Abs(r.Offen - betrag) < 0.01m) return r;
            return rechnungen.FirstOrDefault(r => Math.Abs(r.Offen - betrag) < 0.01m);
        }
        #endregion

        #region SEPA Export
        public string GenerateSepaXml(List<SepaPayment> zahlungen, Firma firma)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine("<Document xmlns=\"urn:iso:std:iso:20022:tech:xsd:pain.001.003.03\"><CstmrCdtTrfInitn>");
            sb.AppendLine($"<GrpHdr><MsgId>NOVVIA-{DateTime.Now:yyyyMMddHHmmss}</MsgId><CreDtTm>{DateTime.Now:yyyy-MM-ddTHH:mm:ss}</CreDtTm>");
            sb.AppendLine($"<NbOfTxs>{zahlungen.Count}</NbOfTxs><CtrlSum>{zahlungen.Sum(z => z.Betrag):F2}</CtrlSum>");
            sb.AppendLine($"<InitgPty><Nm>{firma.Name}</Nm></InitgPty></GrpHdr>");
            sb.AppendLine($"<PmtInf><PmtInfId>PMT-{DateTime.Now:yyyyMMdd}</PmtInfId><PmtMtd>TRF</PmtMtd><NbOfTxs>{zahlungen.Count}</NbOfTxs>");
            sb.AppendLine($"<CtrlSum>{zahlungen.Sum(z => z.Betrag):F2}</CtrlSum><PmtTpInf><SvcLvl><Cd>SEPA</Cd></SvcLvl></PmtTpInf>");
            sb.AppendLine($"<ReqdExctnDt>{DateTime.Now.AddDays(1):yyyy-MM-dd}</ReqdExctnDt>");
            sb.AppendLine($"<Dbtr><Nm>{firma.Name}</Nm></Dbtr><DbtrAcct><Id><IBAN>{firma.IBAN?.Replace(" ", "")}</IBAN></Id></DbtrAcct>");
            sb.AppendLine($"<DbtrAgt><FinInstnId><BIC>{firma.BIC}</BIC></FinInstnId></DbtrAgt><ChrgBr>SLEV</ChrgBr>");
            foreach (var z in zahlungen)
            {
                sb.AppendLine($"<CdtTrfTxInf><PmtId><EndToEndId>{z.EndToEndId}</EndToEndId></PmtId>");
                sb.AppendLine($"<Amt><InstdAmt Ccy=\"EUR\">{z.Betrag:F2}</InstdAmt></Amt>");
                sb.AppendLine($"<CdtrAgt><FinInstnId><BIC>{z.BIC}</BIC></FinInstnId></CdtrAgt>");
                sb.AppendLine($"<Cdtr><Nm>{z.Name}</Nm></Cdtr><CdtrAcct><Id><IBAN>{z.IBAN}</IBAN></Id></CdtrAcct>");
                sb.AppendLine($"<RmtInf><Ustrd>{z.Verwendungszweck}</Ustrd></RmtInf></CdtTrfTxInf>");
            }
            sb.AppendLine("</PmtInf></CstmrCdtTrfInitn></Document>");
            return sb.ToString();
        }
        #endregion
    }

    public class PaymentConfig
    {
        public string PayPalClientId { get; set; } = "";
        public string PayPalSecret { get; set; } = "";
        public string MollieApiKey { get; set; } = "";
        public string SparkasseBLZ { get; set; } = "";
        public string SparkasseKonto { get; set; } = "";
    }
    public class PayPalTransaction { public string TransactionId { get; set; } = ""; public decimal Amount { get; set; } public string Currency { get; set; } = "EUR"; public string Status { get; set; } = ""; public DateTime Date { get; set; } public string? PayerEmail { get; set; } public string? Note { get; set; } }
    public class MolliePayment { public string Id { get; set; } = ""; public decimal Amount { get; set; } public string Status { get; set; } = ""; public string? Method { get; set; } public string? Description { get; set; } public DateTime CreatedAt { get; set; } public string? OrderId { get; set; } }
    public class SepaPayment { public string EndToEndId { get; set; } = ""; public decimal Betrag { get; set; } public string Name { get; set; } = ""; public string IBAN { get; set; } = ""; public string BIC { get; set; } = ""; public string Verwendungszweck { get; set; } = ""; }
}

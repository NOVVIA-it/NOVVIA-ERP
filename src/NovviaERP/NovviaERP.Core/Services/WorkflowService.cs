using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text.Json;
using System.Threading.Tasks;
using NovviaERP.Core.Data;
using NovviaERP.Core.Entities;
using Serilog;

namespace NovviaERP.Core.Services
{
    public class WorkflowService
    {
        private readonly JtlDbContext _db;
        private readonly EmailConfig _emailConfig;
        private static readonly ILogger _log = Log.ForContext<WorkflowService>();

        public WorkflowService(JtlDbContext db, EmailConfig emailConfig) { _db = db; _emailConfig = emailConfig; }

        public async Task TriggerEventAsync(string eventTyp, object data, string? referenz = null)
        {
            var conn = await _db.GetConnectionAsync();
            var workflows = await Dapper.SqlMapper.QueryAsync<Workflow>(conn,
                "SELECT * FROM tWorkflow WHERE nAktiv = 1 AND cEvent = @Event ORDER BY nPrioritaet",
                new { Event = eventTyp });

            foreach (var wf in workflows)
            {
                try
                {
                    if (!EvaluateBedingung(wf.Bedingung, data)) continue;
                    await ExecuteAktionAsync(wf.Aktion, data, referenz);
                    await LogWorkflowAsync(wf.Id, eventTyp, referenz, true);
                    _log.Information("Workflow {Name} ausgeführt für {Event}", wf.Name, eventTyp);
                }
                catch (Exception ex)
                {
                    await LogWorkflowAsync(wf.Id, eventTyp, referenz, false, ex.Message);
                    _log.Error(ex, "Workflow {Name} fehlgeschlagen", wf.Name);
                }
            }
        }

        private bool EvaluateBedingung(string? bedingung, object data)
        {
            if (string.IsNullOrEmpty(bedingung)) return true;
            try
            {
                var json = JsonSerializer.Serialize(data);
                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                // Einfache Bedingungsauswertung: "Status=3" oder "Betrag>100"
                var parts = bedingung.Split(new[] { "=", ">", "<", "!=" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2 || dict == null) return true;
                var field = parts[0].Trim();
                var value = parts[1].Trim();
                if (!dict.TryGetValue(field, out var elem)) return false;
                var actual = elem.ToString();
                if (bedingung.Contains("!=")) return actual != value;
                if (bedingung.Contains(">=")) return decimal.TryParse(actual, out var a1) && decimal.TryParse(value, out var v1) && a1 >= v1;
                if (bedingung.Contains("<=")) return decimal.TryParse(actual, out var a2) && decimal.TryParse(value, out var v2) && a2 <= v2;
                if (bedingung.Contains(">")) return decimal.TryParse(actual, out var a3) && decimal.TryParse(value, out var v3) && a3 > v3;
                if (bedingung.Contains("<")) return decimal.TryParse(actual, out var a4) && decimal.TryParse(value, out var v4) && a4 < v4;
                return actual == value;
            }
            catch { return true; }
        }

        private async Task ExecuteAktionAsync(string? aktion, object data, string? referenz)
        {
            if (string.IsNullOrEmpty(aktion)) return;
            var json = JsonSerializer.Serialize(data);
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) ?? new();

            // Format: "EMAIL:empfaenger@test.de:Betreff:Text" oder "WEBHOOK:url" oder "STATUS:5"
            var parts = aktion.Split(':');
            var typ = parts[0].ToUpper();

            switch (typ)
            {
                case "EMAIL":
                    if (parts.Length >= 4)
                    {
                        var to = ReplacePlaceholders(parts[1], dict);
                        var subject = ReplacePlaceholders(parts[2], dict);
                        var body = ReplacePlaceholders(string.Join(":", parts.Skip(3)), dict);
                        await SendEmailAsync(to, subject, body);
                    }
                    break;
                case "WEBHOOK":
                    if (parts.Length >= 2)
                    {
                        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                        await http.PostAsJsonAsync(parts[1], data);
                    }
                    break;
                case "STATUS":
                    if (parts.Length >= 2 && int.TryParse(parts[1], out var status) && dict.TryGetValue("Id", out var idElem))
                    {
                        var id = idElem.GetInt32();
                        await _db.UpdateBestellStatusAsync(id, (BestellStatus)status);
                    }
                    break;
            }
        }

        private string ReplacePlaceholders(string text, Dictionary<string, JsonElement> data)
        {
            foreach (var kvp in data)
                text = text.Replace($"{{{kvp.Key}}}", kvp.Value.ToString());
            return text;
        }

        private async Task SendEmailAsync(string to, string subject, string body)
        {
            using var client = new SmtpClient(_emailConfig.SmtpHost, _emailConfig.SmtpPort)
            {
                EnableSsl = _emailConfig.UseSsl,
                Credentials = new System.Net.NetworkCredential(_emailConfig.Username, _emailConfig.Password)
            };
            var message = new MailMessage(_emailConfig.FromAddress, to, subject, body) { IsBodyHtml = true };
            await client.SendMailAsync(message);
            _log.Information("E-Mail gesendet an {To}: {Subject}", to, subject);
        }

        private async Task LogWorkflowAsync(int workflowId, string eventTyp, string? referenz, bool success, string? error = null)
        {
            var conn = await _db.GetConnectionAsync();
            await Dapper.SqlMapper.ExecuteAsync(conn,
                "INSERT INTO tWorkflowLog (kWorkflow, dAusgefuehrt, cEvent, cReferenz, nErfolgreich, cFehler) VALUES (@WfId, GETDATE(), @Event, @Ref, @Ok, @Err)",
                new { WfId = workflowId, Event = eventTyp, Ref = referenz, Ok = success, Err = error });
        }

        #region Vordefinierte Events
        public async Task OnBestellungErstelltAsync(Bestellung bestellung) =>
            await TriggerEventAsync("BestellungErstellt", bestellung, bestellung.BestellNr);

        public async Task OnZahlungEingangAsync(Rechnung rechnung, decimal betrag) =>
            await TriggerEventAsync("ZahlungEingang", new { rechnung.Id, rechnung.RechnungsNr, Betrag = betrag, rechnung.Status }, rechnung.RechnungsNr);

        public async Task OnVersandAsync(Bestellung bestellung, string trackingNr) =>
            await TriggerEventAsync("Versand", new { bestellung.Id, bestellung.BestellNr, TrackingNr = trackingNr, bestellung.VersandDienstleister }, bestellung.BestellNr);

        public async Task OnRMAErstelltAsync(RMA rma) =>
            await TriggerEventAsync("RMAErstellt", rma, rma.RMANr);

        public async Task OnLagerbestandNiedrigAsync(Artikel artikel) =>
            await TriggerEventAsync("LagerbestandNiedrig", new { artikel.Id, artikel.ArtNr, Name = artikel.Beschreibung?.Name, artikel.Lagerbestand, artikel.Mindestbestand }, artikel.ArtNr);
        #endregion
    }

    public class EmailConfig
    {
        public string SmtpHost { get; set; } = "smtp.office365.com";
        public int SmtpPort { get; set; } = 587;
        public bool UseSsl { get; set; } = true;
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string FromAddress { get; set; } = "";
    }
}

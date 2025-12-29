using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Dapper;
using NovviaERP.Core.Data;
using Serilog;

namespace NovviaERP.Core.Services
{
    /// <summary>
    /// Zentrale Ausgabe-Steuerung: Drucken, Speichern, E-Mail, Vorschau
    /// </summary>
    public class AusgabeService
    {
        private readonly JtlDbContext _db;
        private readonly DruckerService _drucker;
        private readonly ReportService _report;
        private static readonly ILogger _log = Log.ForContext<AusgabeService>();

        public AusgabeService(JtlDbContext db, DruckerService drucker, ReportService report)
        {
            _db = db;
            _drucker = drucker;
            _report = report;
        }

        #region Ausgabe-Dialog (wie JTL)

        /// <summary>
        /// Generiert PDF für Vorschau (kann mit Formular-Vorlage arbeiten)
        /// </summary>
        public async Task<byte[]?> GeneratePdfAsync(DokumentTyp dokumentTyp, int dokumentId, int? formularVorlageId = null)
        {
            // TODO: Wenn formularVorlageId gesetzt, combit List & Label verwenden
            // Für jetzt: QuestPDF-Fallback
            return dokumentTyp switch
            {
                DokumentTyp.Rechnung => await _report.GenerateRechnungPdfAsync(dokumentId),
                DokumentTyp.Lieferschein => await _report.GenerateLieferscheinPdfAsync(dokumentId),
                DokumentTyp.Mahnung => await _report.GenerateMahnungPdfAsync(dokumentId),
                DokumentTyp.Angebot => await _report.GenerateAngebotPdfAsync(dokumentId),
                DokumentTyp.Bestellung => await _report.GenerateBestellungPdfAsync(dokumentId),
                DokumentTyp.Auftragsbestaetigung => await _report.GenerateBestellungPdfAsync(dokumentId),
                DokumentTyp.Gutschrift => await _report.GenerateGutschriftPdfAsync(dokumentId),
                DokumentTyp.Packliste => await _report.GeneratePacklistePdfAsync(dokumentId),
                DokumentTyp.Versandetikett => await GetVersandetikettAsync(dokumentId),
                _ => null
            };
        }

        /// <summary>
        /// Zeigt Ausgabe-Optionen und führt gewählte Aktion aus
        /// </summary>
        public async Task<AusgabeErgebnis> AusgabeAsync(AusgabeAnfrage anfrage)
        {
            var ergebnis = new AusgabeErgebnis { DokumentTyp = anfrage.DokumentTyp };

            // Vorhandenes PDF verwenden oder neu generieren
            byte[]? pdf = anfrage.VorhandenesPdf;
            if (pdf == null || pdf.Length == 0)
            {
                pdf = await GeneratePdfAsync(anfrage.DokumentTyp, anfrage.DokumentId, anfrage.FormularVorlageId);
            }

            if (pdf == null)
            {
                ergebnis.Fehler = "PDF konnte nicht generiert werden";
                return ergebnis;
            }

            ergebnis.PdfDaten = pdf;

            // Aktionen ausführen
            foreach (var aktion in anfrage.Aktionen)
            {
                try
                {
                    switch (aktion)
                    {
                        case AusgabeAktion.Vorschau:
                            ergebnis.VorschauPfad = await SpeichereTemporaerAsync(pdf, anfrage.DokumentTyp);
                            break;

                        case AusgabeAktion.Drucken:
                            await DruckenAsync(pdf, anfrage);
                            ergebnis.Gedruckt = true;
                            break;

                        case AusgabeAktion.Speichern:
                            ergebnis.GespeicherterPfad = await SpeichernAsync(pdf, anfrage);
                            break;

                        case AusgabeAktion.EMail:
                            await SendeEmailAsync(pdf, anfrage);
                            ergebnis.EmailGesendet = true;
                            break;

                        case AusgabeAktion.Archivieren:
                            await ArchivierenAsync(pdf, anfrage);
                            ergebnis.Archiviert = true;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Ausgabe-Aktion {Aktion} fehlgeschlagen", aktion);
                    ergebnis.Fehler = ex.Message;
                }
            }

            // Ausgabe-Log speichern
            await LogAusgabeAsync(anfrage, ergebnis);

            return ergebnis;
        }
        #endregion

        #region Drucken
        private async Task DruckenAsync(byte[] pdf, AusgabeAnfrage anfrage)
        {
            var druckerTyp = anfrage.DokumentTyp switch
            {
                DokumentTyp.Rechnung => DruckerTyp.Rechnung,
                DokumentTyp.Lieferschein => DruckerTyp.Lieferschein,
                DokumentTyp.Mahnung => DruckerTyp.Mahnung,
                DokumentTyp.Versandetikett => DruckerTyp.Versandetikett,
                DokumentTyp.Packliste => DruckerTyp.Pickliste,
                _ => DruckerTyp.Sonstige
            };

            if (anfrage.DokumentTyp == DokumentTyp.Versandetikett)
            {
                await _drucker.DruckeVersandetikettAsync(pdf, anfrage.DruckerName, anfrage.Kopien);
            }
            else
            {
                await _drucker.DruckeDokumentAsync(pdf, druckerTyp, anfrage.Kopien);
            }

            _log.Information("{Typ} {Id} gedruckt ({Kopien}x)", anfrage.DokumentTyp, anfrage.DokumentId, anfrage.Kopien);
        }
        #endregion

        #region Speichern
        private async Task<string> SpeichernAsync(byte[] pdf, AusgabeAnfrage anfrage)
        {
            var conn = await _db.GetConnectionAsync();
            
            // Dokumenten-Pfad aus Einstellungen
            var basisPfad = await conn.QuerySingleOrDefaultAsync<string>(
                "SELECT cWert FROM tEinstellungen WHERE cKey = 'DokumentenPfad'") ?? @"C:\NOVVIA\Dokumente";

            // Unterordner nach Typ
            var unterordner = anfrage.DokumentTyp.ToString();
            var jahr = DateTime.Now.Year.ToString();
            var zielOrdner = Path.Combine(basisPfad, unterordner, jahr);
            Directory.CreateDirectory(zielOrdner);

            // Dateiname generieren
            var dokumentNr = await GetDokumentNummerAsync(anfrage.DokumentTyp, anfrage.DokumentId);
            var dateiname = $"{anfrage.DokumentTyp}_{dokumentNr}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
            
            if (!string.IsNullOrEmpty(anfrage.DateinameSuffix))
                dateiname = $"{anfrage.DokumentTyp}_{dokumentNr}_{anfrage.DateinameSuffix}.pdf";

            var vollpfad = Path.Combine(zielOrdner, dateiname);
            await File.WriteAllBytesAsync(vollpfad, pdf);

            _log.Information("{Typ} {Id} gespeichert: {Pfad}", anfrage.DokumentTyp, anfrage.DokumentId, vollpfad);
            return vollpfad;
        }

        private async Task<string> SpeichereTemporaerAsync(byte[] pdf, DokumentTyp typ)
        {
            var tempPfad = Path.Combine(Path.GetTempPath(), $"NOVVIA_{typ}_{Guid.NewGuid()}.pdf");
            await File.WriteAllBytesAsync(tempPfad, pdf);
            return tempPfad;
        }
        #endregion

        #region E-Mail
        private async Task SendeEmailAsync(byte[] pdf, AusgabeAnfrage anfrage)
        {
            var conn = await _db.GetConnectionAsync();
            
            // E-Mail Einstellungen laden
            var smtp = await conn.QuerySingleOrDefaultAsync<SmtpEinstellungen>(
                "SELECT cSmtpServer, nSmtpPort, cSmtpUser, cSmtpPasswort, nSmtpSSL, cAbsenderEmail, cAbsenderName FROM tEinstellungen WHERE kFirma = 1");
            
            if (smtp == null || string.IsNullOrEmpty(smtp.SmtpServer))
                throw new Exception("SMTP nicht konfiguriert");

            // Empfänger ermitteln
            var empfaenger = anfrage.EmailEmpfaenger;
            if (string.IsNullOrEmpty(empfaenger))
            {
                empfaenger = await GetKundenEmailAsync(anfrage.DokumentTyp, anfrage.DokumentId);
            }

            if (string.IsNullOrEmpty(empfaenger))
                throw new Exception("Kein E-Mail-Empfänger gefunden");

            // Vorlage laden
            var vorlage = await GetEmailVorlageAsync(anfrage.DokumentTyp);
            var dokumentNr = await GetDokumentNummerAsync(anfrage.DokumentTyp, anfrage.DokumentId);

            // Platzhalter ersetzen
            var betreff = ErsetzePlatzhalter(vorlage.Betreff, anfrage, dokumentNr);
            var text = ErsetzePlatzhalter(vorlage.Text, anfrage, dokumentNr);
            var htmlText = ErsetzePlatzhalter(vorlage.HtmlText ?? "", anfrage, dokumentNr);

            // E-Mail erstellen
            using var client = new SmtpClient(smtp.SmtpServer, smtp.SmtpPort)
            {
                Credentials = new NetworkCredential(smtp.SmtpUser, smtp.SmtpPasswort),
                EnableSsl = smtp.SmtpSSL
            };

            using var mail = new MailMessage
            {
                From = new MailAddress(smtp.AbsenderEmail, smtp.AbsenderName),
                Subject = betreff,
                IsBodyHtml = !string.IsNullOrEmpty(htmlText)
            };

            mail.To.Add(empfaenger);
            
            if (!string.IsNullOrEmpty(anfrage.EmailCC))
                mail.CC.Add(anfrage.EmailCC);
            if (!string.IsNullOrEmpty(anfrage.EmailBCC))
                mail.Bcc.Add(anfrage.EmailBCC);

            mail.Body = !string.IsNullOrEmpty(htmlText) ? htmlText : text;

            // PDF anhängen
            var dateiname = $"{anfrage.DokumentTyp}_{dokumentNr}.pdf";
            mail.Attachments.Add(new Attachment(new MemoryStream(pdf), dateiname, "application/pdf"));

            // Zusätzliche Anhänge
            foreach (var anhang in anfrage.ZusaetzlicheAnhaenge)
            {
                if (File.Exists(anhang))
                    mail.Attachments.Add(new Attachment(anhang));
            }

            await client.SendMailAsync(mail);

            // E-Mail-Versand loggen
            await conn.ExecuteAsync(@"
                INSERT INTO tEmailLog (kDokument, cDokumentTyp, cEmpfaenger, cBetreff, dGesendet, nErfolgreich)
                VALUES (@DokId, @Typ, @Empf, @Betreff, GETDATE(), 1)",
                new { DokId = anfrage.DokumentId, Typ = anfrage.DokumentTyp.ToString(), Empf = empfaenger, Betreff = betreff });

            _log.Information("{Typ} {Id} per E-Mail gesendet an {Empfaenger}", anfrage.DokumentTyp, anfrage.DokumentId, empfaenger);
        }

        /// <summary>
        /// Erstellt E-Mail-Vorschau ohne zu senden
        /// </summary>
        public async Task<EmailVorschau> ErstelleEmailVorschauAsync(AusgabeAnfrage anfrage)
        {
            var dokumentNr = await GetDokumentNummerAsync(anfrage.DokumentTyp, anfrage.DokumentId);
            var vorlage = await GetEmailVorlageAsync(anfrage.DokumentTyp);
            var empfaenger = anfrage.EmailEmpfaenger ?? await GetKundenEmailAsync(anfrage.DokumentTyp, anfrage.DokumentId);

            return new EmailVorschau
            {
                Empfaenger = empfaenger ?? "",
                CC = anfrage.EmailCC,
                BCC = anfrage.EmailBCC,
                Betreff = ErsetzePlatzhalter(vorlage.Betreff, anfrage, dokumentNr),
                Text = ErsetzePlatzhalter(vorlage.Text, anfrage, dokumentNr),
                HtmlText = ErsetzePlatzhalter(vorlage.HtmlText ?? "", anfrage, dokumentNr),
                Anhang = $"{anfrage.DokumentTyp}_{dokumentNr}.pdf"
            };
        }
        #endregion

        #region Archivieren
        private async Task ArchivierenAsync(byte[] pdf, AusgabeAnfrage anfrage)
        {
            var conn = await _db.GetConnectionAsync();
            var dokumentNr = await GetDokumentNummerAsync(anfrage.DokumentTyp, anfrage.DokumentId);

            await conn.ExecuteAsync(@"
                INSERT INTO tDokumentArchiv (cDokumentTyp, kDokument, cDokumentNr, dArchiviert, bPdfDaten, nGroesse)
                VALUES (@Typ, @Id, @Nr, GETDATE(), @Pdf, @Groesse)",
                new { Typ = anfrage.DokumentTyp.ToString(), Id = anfrage.DokumentId, Nr = dokumentNr, Pdf = pdf, Groesse = pdf.Length });
        }
        #endregion

        #region Hilfsmethoden
        private async Task<string> GetDokumentNummerAsync(DokumentTyp typ, int id)
        {
            var conn = await _db.GetConnectionAsync();
            return typ switch
            {
                DokumentTyp.Rechnung => await conn.QuerySingleOrDefaultAsync<string>("SELECT cRechnungNr FROM tRechnung WHERE kRechnung = @Id", new { Id = id }) ?? id.ToString(),
                DokumentTyp.Lieferschein => await conn.QuerySingleOrDefaultAsync<string>("SELECT cLieferscheinNr FROM tLieferschein WHERE kLieferschein = @Id", new { Id = id }) ?? id.ToString(),
                DokumentTyp.Angebot => await conn.QuerySingleOrDefaultAsync<string>("SELECT cAngebotNr FROM tAngebot WHERE kAngebot = @Id", new { Id = id }) ?? id.ToString(),
                DokumentTyp.Bestellung => await conn.QuerySingleOrDefaultAsync<string>("SELECT cBestellNr FROM tBestellung WHERE kBestellung = @Id", new { Id = id }) ?? id.ToString(),
                _ => id.ToString()
            };
        }

        private async Task<string?> GetKundenEmailAsync(DokumentTyp typ, int id)
        {
            var conn = await _db.GetConnectionAsync();
            return typ switch
            {
                DokumentTyp.Rechnung => await conn.QuerySingleOrDefaultAsync<string>(
                    "SELECT k.cMail FROM tRechnung r INNER JOIN tKunde k ON r.kKunde = k.kKunde WHERE r.kRechnung = @Id", new { Id = id }),
                DokumentTyp.Lieferschein => await conn.QuerySingleOrDefaultAsync<string>(
                    "SELECT k.cMail FROM tLieferschein l INNER JOIN tKunde k ON l.kKunde = k.kKunde WHERE l.kLieferschein = @Id", new { Id = id }),
                DokumentTyp.Bestellung => await conn.QuerySingleOrDefaultAsync<string>(
                    "SELECT cEmail FROM tBestellung WHERE kBestellung = @Id", new { Id = id }),
                _ => null
            };
        }

        private async Task<EmailVorlage> GetEmailVorlageAsync(DokumentTyp typ)
        {
            var conn = await _db.GetConnectionAsync();
            var vorlage = await conn.QuerySingleOrDefaultAsync<EmailVorlage>(
                "SELECT * FROM tEmailVorlage WHERE cTyp = @Typ AND nAktiv = 1", new { Typ = typ.ToString() });

            return vorlage ?? new EmailVorlage
            {
                Betreff = $"Ihre {typ} {{DokumentNr}}",
                Text = $"Sehr geehrte Damen und Herren,\n\nanbei erhalten Sie Ihre {typ}.\n\nMit freundlichen Grüßen\n{{Firma}}"
            };
        }

        private string ErsetzePlatzhalter(string text, AusgabeAnfrage anfrage, string dokumentNr)
        {
            return text
                .Replace("{DokumentNr}", dokumentNr)
                .Replace("{DokumentTyp}", anfrage.DokumentTyp.ToString())
                .Replace("{Datum}", DateTime.Now.ToString("dd.MM.yyyy"))
                .Replace("{Firma}", "NOVVIA GmbH"); // TODO: Aus Einstellungen
        }

        private async Task<byte[]?> GetVersandetikettAsync(int lieferscheinId)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QuerySingleOrDefaultAsync<byte[]>(
                "SELECT bLabel FROM tVersand WHERE kLieferschein = @Id", new { Id = lieferscheinId });
        }

        private async Task LogAusgabeAsync(AusgabeAnfrage anfrage, AusgabeErgebnis ergebnis)
        {
            var conn = await _db.GetConnectionAsync();
            await conn.ExecuteAsync(@"
                INSERT INTO tAusgabeLog (cDokumentTyp, kDokument, cAktionen, nErfolgreich, cFehler, dZeitpunkt)
                VALUES (@Typ, @Id, @Aktionen, @Erfolg, @Fehler, GETDATE())",
                new { 
                    Typ = anfrage.DokumentTyp.ToString(), 
                    Id = anfrage.DokumentId, 
                    Aktionen = string.Join(",", anfrage.Aktionen),
                    Erfolg = string.IsNullOrEmpty(ergebnis.Fehler),
                    Fehler = ergebnis.Fehler
                });
        }
        #endregion
    }

    #region DTOs
    public class AusgabeAnfrage
    {
        public DokumentTyp DokumentTyp { get; set; }
        public int DokumentId { get; set; }
        public List<AusgabeAktion> Aktionen { get; set; } = new();

        // Drucken
        public string? DruckerName { get; set; }
        public int Kopien { get; set; } = 1;

        // Speichern
        public string? SpeicherPfad { get; set; }
        public string? DateinameSuffix { get; set; }

        // E-Mail
        public string? EmailEmpfaenger { get; set; }
        public string? EmpfaengerEmail { get => EmailEmpfaenger; set => EmailEmpfaenger = value; }
        public string? EmailCC { get; set; }
        public string? EmailBCC { get; set; }
        public string? EmailBetreffOverride { get; set; }
        public string? EmailTextOverride { get; set; }
        public List<string> ZusaetzlicheAnhaenge { get; set; } = new();

        // Zusätzliche Optionen
        public bool Archivieren { get; set; }
        public int? EmailVorlageId { get; set; }
        public bool EmailVorschau { get; set; }

        // Formular-Auswahl und vorhandenes PDF
        public int? FormularVorlageId { get; set; }
        public byte[]? VorhandenesPdf { get; set; }
    }

    public class AusgabeErgebnis
    {
        public DokumentTyp DokumentTyp { get; set; }
        public byte[]? PdfDaten { get; set; }
        public string? VorschauPfad { get; set; }
        public bool Gedruckt { get; set; }
        public string? GespeicherterPfad { get; set; }
        public bool Gespeichert { get => !string.IsNullOrEmpty(GespeicherterPfad); }
        public bool EmailGesendet { get; set; }
        public bool Archiviert { get; set; }
        public bool VorschauAngezeigt { get; set; }
        public string? Fehler { get; set; }
    }

    public class EmailVorschau
    {
        public string Empfaenger { get; set; } = "";
        public string? CC { get; set; }
        public string? BCC { get; set; }
        public string Betreff { get; set; } = "";
        public string Text { get; set; } = "";
        public string? HtmlText { get; set; }
        public string Anhang { get; set; } = "";
    }

    public class EmailVorlage
    {
        public int Id { get; set; }
        public string Typ { get; set; } = "";
        public string Betreff { get; set; } = "";
        public string Text { get; set; } = "";
        public string? HtmlText { get; set; }
        public bool Aktiv { get; set; } = true;
    }

    public class SmtpEinstellungen
    {
        public string SmtpServer { get; set; } = "";
        public int SmtpPort { get; set; } = 587;
        public string SmtpUser { get; set; } = "";
        public string SmtpPasswort { get; set; } = "";
        public bool SmtpSSL { get; set; } = true;
        public string AbsenderEmail { get; set; } = "";
        public string AbsenderName { get; set; } = "";
    }

    public enum DokumentTyp
    {
        Rechnung, Lieferschein, Mahnung, Angebot, 
        Bestellung, Gutschrift, Packliste, Versandetikett,
        Auftragsbestaetigung, Retourenschein
    }

    public enum AusgabeAktion
    {
        Vorschau, Drucken, Speichern, EMail, Archivieren
    }
    #endregion
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using NovviaERP.Core.Data;
using Serilog;

namespace NovviaERP.Core.Services
{
    /// <summary>
    /// Vordefinierte E-Mail-Vorlagen mit Anhängen
    /// </summary>
    public class EmailVorlageService
    {
        private readonly JtlDbContext _db;
        private static readonly ILogger _log = Log.ForContext<EmailVorlageService>();

        public EmailVorlageService(JtlDbContext db) => _db = db;

        #region Vorlagen-Verwaltung
        /// <summary>
        /// Alle Vorlagen laden
        /// </summary>
        public async Task<IEnumerable<EmailVorlageErweitert>> GetVorlagenAsync(string? typ = null)
        {
            var conn = await _db.GetConnectionAsync();
            var sql = "SELECT * FROM tEmailVorlage WHERE 1=1";
            if (!string.IsNullOrEmpty(typ)) sql += " AND cTyp = @Typ";
            sql += " ORDER BY cTyp, cName";
            
            var vorlagen = await conn.QueryAsync<EmailVorlageErweitert>(sql, new { Typ = typ });
            
            // Anhänge laden
            foreach (var v in vorlagen)
            {
                v.Anhaenge = (await GetAnhaengeAsync(v.Id)).ToList();
            }
            
            return vorlagen;
        }

        /// <summary>
        /// Standard-Vorlage für Typ
        /// </summary>
        public async Task<EmailVorlageErweitert?> GetStandardVorlageAsync(string typ)
        {
            var conn = await _db.GetConnectionAsync();
            var vorlage = await conn.QuerySingleOrDefaultAsync<EmailVorlageErweitert>(
                "SELECT TOP 1 * FROM tEmailVorlage WHERE cTyp = @Typ AND nStandard = 1 AND nAktiv = 1",
                new { Typ = typ });
            
            if (vorlage != null)
                vorlage.Anhaenge = (await GetAnhaengeAsync(vorlage.Id)).ToList();
            
            return vorlage;
        }

        /// <summary>
        /// Vorlage speichern
        /// </summary>
        public async Task<int> SaveVorlageAsync(EmailVorlageErweitert vorlage)
        {
            var conn = await _db.GetConnectionAsync();
            
            if (vorlage.Id == 0)
            {
                vorlage.Id = await conn.QuerySingleAsync<int>(@"
                    INSERT INTO tEmailVorlage (cName, cTyp, cBetreff, cText, cHtmlText, nIsHtml, nStandard, nAktiv,
                        cAbsenderName, cAbsenderEmail, cAntwortAn, cCC, cBCC, nSignaturAnhaengen, cSprache)
                    VALUES (@Name, @Typ, @Betreff, @Text, @HtmlText, @IsHtml, @IstStandard, @Aktiv,
                        @AbsenderName, @AbsenderEmail, @AntwortAn, @CC, @BCC, @SignaturAnhaengen, @Sprache);
                    SELECT SCOPE_IDENTITY();", vorlage);
            }
            else
            {
                await conn.ExecuteAsync(@"
                    UPDATE tEmailVorlage SET cName=@Name, cBetreff=@Betreff, cText=@Text, cHtmlText=@HtmlText,
                        nIsHtml=@IsHtml, nStandard=@IstStandard, nAktiv=@Aktiv, cAbsenderName=@AbsenderName,
                        cAbsenderEmail=@AbsenderEmail, cAntwortAn=@AntwortAn, cCC=@CC, cBCC=@BCC,
                        nSignaturAnhaengen=@SignaturAnhaengen, cSprache=@Sprache
                    WHERE kEmailVorlage=@Id", vorlage);
            }

            // Wenn Standard, andere zurücksetzen
            if (vorlage.IstStandard)
            {
                await conn.ExecuteAsync(
                    "UPDATE tEmailVorlage SET nStandard = 0 WHERE cTyp = @Typ AND kEmailVorlage != @Id",
                    new { vorlage.Typ, vorlage.Id });
            }

            return vorlage.Id;
        }

        /// <summary>
        /// Vorlage duplizieren
        /// </summary>
        public async Task<int> DupliziereVorlageAsync(int vorlageId, string neuerName)
        {
            var conn = await _db.GetConnectionAsync();
            var original = await conn.QuerySingleOrDefaultAsync<EmailVorlageErweitert>(
                "SELECT * FROM tEmailVorlage WHERE kEmailVorlage = @Id", new { Id = vorlageId });
            
            if (original == null) throw new Exception("Vorlage nicht gefunden");
            
            original.Id = 0;
            original.Name = neuerName;
            original.IstStandard = false;
            
            var neueId = await SaveVorlageAsync(original);
            
            // Anhänge kopieren
            var anhaenge = await GetAnhaengeAsync(vorlageId);
            foreach (var a in anhaenge)
            {
                a.Id = 0;
                a.VorlageId = neueId;
                await AddAnhangAsync(a);
            }
            
            return neueId;
        }
        #endregion

        #region Anhänge
        /// <summary>
        /// Anhänge einer Vorlage
        /// </summary>
        public async Task<IEnumerable<EmailVorlageAnhang>> GetAnhaengeAsync(int vorlageId)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QueryAsync<EmailVorlageAnhang>(
                "SELECT * FROM tEmailVorlageAnhang WHERE kEmailVorlage = @VorlageId ORDER BY nSortierung",
                new { VorlageId = vorlageId });
        }

        /// <summary>
        /// Anhang hinzufügen
        /// </summary>
        public async Task<int> AddAnhangAsync(EmailVorlageAnhang anhang)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QuerySingleAsync<int>(@"
                INSERT INTO tEmailVorlageAnhang (kEmailVorlage, cName, cPfad, cTyp, nGroesse, nSortierung, nAktiv)
                VALUES (@VorlageId, @Name, @Pfad, @MimeType, @Groesse, @Sortierung, @Aktiv);
                SELECT SCOPE_IDENTITY();", anhang);
        }

        /// <summary>
        /// Anhang entfernen
        /// </summary>
        public async Task RemoveAnhangAsync(int anhangId)
        {
            var conn = await _db.GetConnectionAsync();
            await conn.ExecuteAsync("DELETE FROM tEmailVorlageAnhang WHERE kEmailVorlageAnhang = @Id", new { Id = anhangId });
        }
        #endregion

        #region Platzhalter
        /// <summary>
        /// Verfügbare Platzhalter nach Typ
        /// </summary>
        public Dictionary<string, List<string>> GetPlatzhalter(string typ)
        {
            var platzhalter = new Dictionary<string, List<string>>
            {
                ["Allgemein"] = new()
                {
                    "{Datum}", "{Zeit}", "{Jahr}",
                    "{Firma.Name}", "{Firma.Strasse}", "{Firma.PLZ}", "{Firma.Ort}",
                    "{Firma.Telefon}", "{Firma.Email}", "{Firma.Website}"
                },
                ["Kunde"] = new()
                {
                    "{Kunde.Anrede}", "{Kunde.Name}", "{Kunde.Vorname}", "{Kunde.Nachname}",
                    "{Kunde.Firma}", "{Kunde.KundenNr}", "{Kunde.Email}"
                }
            };

            switch (typ)
            {
                case "Rechnung":
                    platzhalter["Rechnung"] = new()
                    {
                        "{Rechnung.Nummer}", "{Rechnung.Datum}", "{Rechnung.Faellig}",
                        "{Rechnung.Netto}", "{Rechnung.MwSt}", "{Rechnung.Brutto}"
                    };
                    break;

                case "Lieferschein":
                    platzhalter["Lieferschein"] = new()
                    {
                        "{Lieferschein.Nummer}", "{Lieferschein.Datum}",
                        "{Versand.TrackingNr}", "{Versand.TrackingLink}", "{Versand.Carrier}"
                    };
                    break;

                case "Auftragsbestaetigung":
                    platzhalter["Auftrag"] = new()
                    {
                        "{Auftrag.Nummer}", "{Auftrag.Datum}",
                        "{Auftrag.Netto}", "{Auftrag.Brutto}", "{Auftrag.Lieferdatum}"
                    };
                    break;

                case "Mahnung":
                    platzhalter["Mahnung"] = new()
                    {
                        "{Mahnung.Stufe}", "{Mahnung.Gebuehr}",
                        "{Rechnung.Nummer}", "{Rechnung.Offen}", "{Rechnung.Faellig}"
                    };
                    break;

                case "Angebot":
                    platzhalter["Angebot"] = new()
                    {
                        "{Angebot.Nummer}", "{Angebot.Datum}", "{Angebot.GueltigBis}",
                        "{Angebot.Netto}", "{Angebot.Brutto}"
                    };
                    break;
            }

            return platzhalter;
        }

        /// <summary>
        /// Platzhalter ersetzen
        /// </summary>
        public async Task<string> ErsetzePlatzhalterAsync(string text, string typ, int dokumentId)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var conn = await _db.GetConnectionAsync();
            
            // Firma laden
            var firma = await conn.QuerySingleOrDefaultAsync(
                "SELECT * FROM tFirma WHERE nStandard = 1");
            if (firma != null)
            {
                text = text.Replace("{Firma.Name}", firma.cName ?? "")
                           .Replace("{Firma.Strasse}", firma.cStrasse ?? "")
                           .Replace("{Firma.PLZ}", firma.cPLZ ?? "")
                           .Replace("{Firma.Ort}", firma.cOrt ?? "")
                           .Replace("{Firma.Telefon}", firma.cTelefon ?? "")
                           .Replace("{Firma.Email}", firma.cEmail ?? "")
                           .Replace("{Firma.Website}", firma.cWebsite ?? "");
            }

            // Allgemein
            text = text.Replace("{Datum}", DateTime.Now.ToString("dd.MM.yyyy"))
                       .Replace("{Zeit}", DateTime.Now.ToString("HH:mm"))
                       .Replace("{Jahr}", DateTime.Now.Year.ToString());

            // Dokument-spezifisch
            switch (typ)
            {
                case "Rechnung":
                    var rechnung = await conn.QuerySingleOrDefaultAsync(
                        "SELECT r.*, k.* FROM tRechnung r INNER JOIN tKunde k ON r.kKunde = k.kKunde WHERE r.kRechnung = @Id",
                        new { Id = dokumentId });
                    if (rechnung != null)
                    {
                        text = ErsetzKundenPlatzhalter(text, rechnung);
                        text = text.Replace("{Rechnung.Nummer}", rechnung.cRechnungNr ?? "")
                                   .Replace("{Rechnung.Datum}", ((DateTime?)rechnung.dRechnungsDatum)?.ToString("dd.MM.yyyy") ?? "")
                                   .Replace("{Rechnung.Faellig}", ((DateTime?)rechnung.dFaellig)?.ToString("dd.MM.yyyy") ?? "")
                                   .Replace("{Rechnung.Netto}", ((decimal?)rechnung.fNetto)?.ToString("N2") ?? "")
                                   .Replace("{Rechnung.MwSt}", ((decimal?)rechnung.fMwSt)?.ToString("N2") ?? "")
                                   .Replace("{Rechnung.Brutto}", ((decimal?)rechnung.fBrutto)?.ToString("N2") ?? "")
                                   .Replace("{Rechnung.Offen}", ((decimal?)rechnung.fOffen)?.ToString("N2") ?? "");
                    }
                    break;

                // ... weitere Typen analog
            }

            return text;
        }

        private string ErsetzKundenPlatzhalter(string text, dynamic kunde)
        {
            return text.Replace("{Kunde.Anrede}", kunde.cAnrede ?? "")
                       .Replace("{Kunde.Name}", $"{kunde.cVorname} {kunde.cNachname}".Trim())
                       .Replace("{Kunde.Vorname}", kunde.cVorname ?? "")
                       .Replace("{Kunde.Nachname}", kunde.cNachname ?? "")
                       .Replace("{Kunde.Firma}", kunde.cFirma ?? "")
                       .Replace("{Kunde.KundenNr}", kunde.cKundenNr ?? "")
                       .Replace("{Kunde.Email}", kunde.cMail ?? "");
        }
        #endregion

        #region Standard-Vorlagen erstellen
        public async Task ErstelleStandardVorlagenAsync()
        {
            var conn = await _db.GetConnectionAsync();
            var vorhanden = await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM tEmailVorlage");
            if (vorhanden > 0) return;

            var vorlagen = new List<EmailVorlageErweitert>
            {
                new()
                {
                    Name = "Rechnung Standard",
                    Typ = "Rechnung",
                    Betreff = "Ihre Rechnung {Rechnung.Nummer}",
                    Text = "Sehr geehrte(r) {Kunde.Anrede} {Kunde.Nachname},\n\nanbei erhalten Sie Ihre Rechnung {Rechnung.Nummer} vom {Rechnung.Datum}.\n\nGesamtbetrag: {Rechnung.Brutto} EUR\nZahlbar bis: {Rechnung.Faellig}\n\nMit freundlichen Grüßen\n{Firma.Name}",
                    HtmlText = "<p>Sehr geehrte(r) {Kunde.Anrede} {Kunde.Nachname},</p><p>anbei erhalten Sie Ihre Rechnung <strong>{Rechnung.Nummer}</strong> vom {Rechnung.Datum}.</p><p>Gesamtbetrag: <strong>{Rechnung.Brutto} EUR</strong><br/>Zahlbar bis: {Rechnung.Faellig}</p><p>Mit freundlichen Grüßen<br/>{Firma.Name}</p>",
                    IsHtml = true,
                    IstStandard = true,
                    Aktiv = true
                },
                new()
                {
                    Name = "Auftragsbestätigung",
                    Typ = "Auftragsbestaetigung",
                    Betreff = "Ihre Bestellung {Auftrag.Nummer} - Bestätigung",
                    Text = "Sehr geehrte(r) {Kunde.Anrede} {Kunde.Nachname},\n\nvielen Dank für Ihre Bestellung.\n\nBestellnummer: {Auftrag.Nummer}\nBestelldatum: {Auftrag.Datum}\nGesamtbetrag: {Auftrag.Brutto} EUR\n\nWir werden Ihre Bestellung schnellstmöglich bearbeiten.\n\nMit freundlichen Grüßen\n{Firma.Name}",
                    IstStandard = true,
                    Aktiv = true
                },
                new()
                {
                    Name = "Versandbestätigung",
                    Typ = "Lieferschein",
                    Betreff = "Ihre Bestellung wurde versandt - Tracking: {Versand.TrackingNr}",
                    Text = "Sehr geehrte(r) {Kunde.Anrede} {Kunde.Nachname},\n\nIhre Bestellung wurde versandt.\n\nVersanddienstleister: {Versand.Carrier}\nTracking-Nummer: {Versand.TrackingNr}\n\nSie können Ihre Sendung hier verfolgen:\n{Versand.TrackingLink}\n\nMit freundlichen Grüßen\n{Firma.Name}",
                    IstStandard = true,
                    Aktiv = true
                },
                new()
                {
                    Name = "Mahnung Stufe 1",
                    Typ = "Mahnung",
                    Betreff = "Zahlungserinnerung - Rechnung {Rechnung.Nummer}",
                    Text = "Sehr geehrte(r) {Kunde.Anrede} {Kunde.Nachname},\n\nbei der Überprüfung unserer Buchhaltung haben wir festgestellt, dass die folgende Rechnung noch nicht beglichen wurde:\n\nRechnung: {Rechnung.Nummer}\nFällig seit: {Rechnung.Faellig}\nOffener Betrag: {Rechnung.Offen} EUR\n\nSollte sich Ihre Zahlung mit diesem Schreiben überschnitten haben, betrachten Sie es bitte als gegenstandslos.\n\nMit freundlichen Grüßen\n{Firma.Name}",
                    IstStandard = true,
                    Aktiv = true
                },
                new()
                {
                    Name = "Angebot",
                    Typ = "Angebot",
                    Betreff = "Ihr Angebot {Angebot.Nummer}",
                    Text = "Sehr geehrte(r) {Kunde.Anrede} {Kunde.Nachname},\n\nvielen Dank für Ihre Anfrage. Anbei erhalten Sie unser Angebot.\n\nAngebotsnummer: {Angebot.Nummer}\nGültig bis: {Angebot.GueltigBis}\nGesamtbetrag: {Angebot.Brutto} EUR\n\nBei Fragen stehen wir Ihnen gerne zur Verfügung.\n\nMit freundlichen Grüßen\n{Firma.Name}",
                    IstStandard = true,
                    Aktiv = true
                }
            };

            foreach (var v in vorlagen)
            {
                await SaveVorlageAsync(v);
            }

            _log.Information("Standard E-Mail-Vorlagen erstellt");
        }
        #endregion
    }

    #region DTOs
    public class EmailVorlageErweitert
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Typ { get; set; } = "";
        public string Betreff { get; set; } = "";
        public string Text { get; set; } = "";
        public string? HtmlText { get; set; }
        public bool IsHtml { get; set; }
        public bool IstHtml { get => IsHtml; set => IsHtml = value; }
        public bool IstStandard { get; set; }
        public bool Aktiv { get; set; } = true;
        public string? AbsenderName { get; set; }
        public string? AbsenderEmail { get; set; }
        public string? AntwortAn { get; set; }
        public string? CC { get; set; }
        public string? BCC { get; set; }
        public bool SignaturAnhaengen { get; set; } = true;
        public string Sprache { get; set; } = "DE";
        public List<EmailVorlageAnhang> Anhaenge { get; set; } = new();
        // Property for cross-service compatibility with EmailVorlagenService
        private System.Collections.IList? _anlagen;
        public System.Collections.IList Anlagen
        {
            get => _anlagen ?? new System.Collections.ArrayList();
            set => _anlagen = value;
        }
    }

    // Base class for email attachments to allow polymorphism between services
    public class EmailAnlageBase
    {
        public int Id { get; set; }
        public int VorlageId { get; set; }
        public string Name { get; set; } = "";
        public string Pfad { get; set; } = "";
        public int Sortierung { get; set; }
    }

    public class EmailVorlageAnhang
    {
        public int Id { get; set; }
        public int VorlageId { get; set; }
        public string Name { get; set; } = "";
        public string Pfad { get; set; } = "";
        public string? MimeType { get; set; }
        public int Groesse { get; set; }
        public int Sortierung { get; set; }
        public bool Aktiv { get; set; } = true;
    }
    #endregion
}

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
    /// E-Mail-Vorlagen Verwaltung mit Anlagen
    /// </summary>
    public class EmailVorlagenService
    {
        private readonly JtlDbContext _db;
        private static readonly ILogger _log = Log.ForContext<EmailVorlagenService>();

        public EmailVorlagenService(JtlDbContext db) => _db = db;

        #region Vorlagen CRUD
        /// <summary>
        /// Holt alle E-Mail-Vorlagen
        /// </summary>
        public async Task<IEnumerable<EmailVorlageErweitert>> GetVorlagenAsync(EmailVorlageTyp? typ = null)
        {
            var conn = await _db.GetConnectionAsync();
            var sql = "SELECT * FROM tEmailVorlage WHERE 1=1";
            if (typ.HasValue) sql += " AND cTyp = @Typ";
            sql += " ORDER BY cTyp, cName";
            
            var vorlagen = await conn.QueryAsync<EmailVorlageErweitert>(sql, new { Typ = typ?.ToString() });
            
            // Anlagen laden
            foreach (var v in vorlagen)
            {
                v.Anlagen = (await conn.QueryAsync<EmailVorlageAnlage>(
                    "SELECT * FROM tEmailVorlageAnlage WHERE kEmailVorlage = @Id ORDER BY nSortierung",
                    new { Id = v.Id })).ToList();
            }

            return vorlagen;
        }

        /// <summary>
        /// Holt eine E-Mail-Vorlage
        /// </summary>
        public async Task<EmailVorlageErweitert?> GetVorlageAsync(int id)
        {
            var conn = await _db.GetConnectionAsync();
            var vorlage = await conn.QuerySingleOrDefaultAsync<EmailVorlageErweitert>(
                "SELECT * FROM tEmailVorlage WHERE kEmailVorlage = @Id", new { Id = id });
            
            if (vorlage != null)
            {
                vorlage.Anlagen = (await conn.QueryAsync<EmailVorlageAnlage>(
                    "SELECT * FROM tEmailVorlageAnlage WHERE kEmailVorlage = @Id ORDER BY nSortierung",
                    new { Id = id })).ToList();
            }

            return vorlage;
        }

        /// <summary>
        /// Holt die Standard-Vorlage für einen Typ
        /// </summary>
        public async Task<EmailVorlageErweitert?> GetStandardVorlageAsync(EmailVorlageTyp typ)
        {
            var conn = await _db.GetConnectionAsync();
            var vorlage = await conn.QuerySingleOrDefaultAsync<EmailVorlageErweitert>(
                "SELECT TOP 1 * FROM tEmailVorlage WHERE cTyp = @Typ AND nStandard = 1 AND nAktiv = 1",
                new { Typ = typ.ToString() });
            
            if (vorlage != null)
            {
                vorlage.Anlagen = (await conn.QueryAsync<EmailVorlageAnlage>(
                    "SELECT * FROM tEmailVorlageAnlage WHERE kEmailVorlage = @Id ORDER BY nSortierung",
                    new { Id = vorlage.Id })).ToList();
            }

            return vorlage;
        }

        /// <summary>
        /// Speichert eine E-Mail-Vorlage
        /// </summary>
        public async Task<int> SaveVorlageAsync(EmailVorlageErweitert vorlage)
        {
            var conn = await _db.GetConnectionAsync();

            // Wenn Standard, andere Standard-Markierung entfernen
            if (vorlage.IstStandard)
            {
                await conn.ExecuteAsync(
                    "UPDATE tEmailVorlage SET nStandard = 0 WHERE cTyp = @Typ AND kEmailVorlage != @Id",
                    new { Typ = vorlage.Typ.ToString(), Id = vorlage.Id });
            }

            if (vorlage.Id == 0)
            {
                vorlage.Id = await conn.QuerySingleAsync<int>(@"
                    INSERT INTO tEmailVorlage (cName, cTyp, cBetreff, cText, cHtmlText, nHtml, 
                        nStandard, nAktiv, cSprache, dErstellt)
                    VALUES (@Name, @TypString, @Betreff, @Text, @HtmlText, @IstHtml, 
                        @IstStandard, @Aktiv, @Sprache, GETDATE());
                    SELECT SCOPE_IDENTITY();", 
                    new { vorlage.Name, TypString = vorlage.Typ.ToString(), vorlage.Betreff, vorlage.Text, 
                          vorlage.HtmlText, vorlage.IstHtml, vorlage.IstStandard, vorlage.Aktiv, vorlage.Sprache });
            }
            else
            {
                await conn.ExecuteAsync(@"
                    UPDATE tEmailVorlage SET cName=@Name, cBetreff=@Betreff, cText=@Text, cHtmlText=@HtmlText,
                        nHtml=@IstHtml, nStandard=@IstStandard, nAktiv=@Aktiv, cSprache=@Sprache, dGeaendert=GETDATE()
                    WHERE kEmailVorlage=@Id", vorlage);
            }

            // Anlagen aktualisieren
            await conn.ExecuteAsync("DELETE FROM tEmailVorlageAnlage WHERE kEmailVorlage = @Id", new { Id = vorlage.Id });
            var sortierung = 1;
            foreach (EmailVorlageAnlage anlage in vorlage.Anlagen)
            {
                anlage.VorlageId = vorlage.Id;
                anlage.Sortierung = sortierung++;
                await conn.ExecuteAsync(@"
                    INSERT INTO tEmailVorlageAnlage (kEmailVorlage, cName, cPfad, cDateiname, nSortierung, nImmerAnhaengen)
                    VALUES (@VorlageId, @Name, @Pfad, @Dateiname, @Sortierung, @ImmerAnhaengen)", anlage);
            }

            return vorlage.Id;
        }

        /// <summary>
        /// Löscht eine E-Mail-Vorlage
        /// </summary>
        public async Task DeleteVorlageAsync(int id)
        {
            var conn = await _db.GetConnectionAsync();
            await conn.ExecuteAsync("DELETE FROM tEmailVorlageAnlage WHERE kEmailVorlage = @Id", new { Id = id });
            await conn.ExecuteAsync("DELETE FROM tEmailVorlage WHERE kEmailVorlage = @Id", new { Id = id });
        }
        #endregion

        #region Platzhalter
        /// <summary>
        /// Ersetzt Platzhalter in einer Vorlage
        /// </summary>
        public string ErsetzePlatzhalter(string text, Dictionary<string, string?> werte)
        {
            if (string.IsNullOrEmpty(text)) return text;
            
            foreach (var (key, value) in werte)
            {
                text = text.Replace($"{{{key}}}", value ?? "");
            }

            // Standard-Platzhalter
            text = text.Replace("{Datum}", DateTime.Now.ToString("dd.MM.yyyy"));
            text = text.Replace("{Zeit}", DateTime.Now.ToString("HH:mm"));
            text = text.Replace("{Jahr}", DateTime.Now.Year.ToString());

            return text;
        }

        /// <summary>
        /// Holt Platzhalter-Werte für ein Dokument
        /// </summary>
        public async Task<Dictionary<string, string?>> GetPlatzhalterWerteAsync(EmailVorlageTyp typ, int dokumentId)
        {
            var conn = await _db.GetConnectionAsync();
            var werte = new Dictionary<string, string?>();

            // Firma laden
            var firma = await conn.QuerySingleOrDefaultAsync<dynamic>(
                "SELECT TOP 1 * FROM tFirma WHERE nStandard = 1");
            if (firma != null)
            {
                werte["Firma.Name"] = firma.cName;
                werte["Firma.Strasse"] = firma.cStrasse;
                werte["Firma.PLZ"] = firma.cPLZ;
                werte["Firma.Ort"] = firma.cOrt;
                werte["Firma.Land"] = firma.cLand;
                werte["Firma.Telefon"] = firma.cTelefon;
                werte["Firma.Email"] = firma.cEmail;
                werte["Firma.Website"] = firma.cWebsite;
            }

            // Dokumentspezifische Daten laden
            switch (typ)
            {
                case EmailVorlageTyp.Rechnung:
                    var rechnung = await conn.QuerySingleOrDefaultAsync<dynamic>(
                        "SELECT r.*, k.* FROM tRechnung r INNER JOIN tKunde k ON r.kKunde = k.kKunde WHERE r.kRechnung = @Id",
                        new { Id = dokumentId });
                    if (rechnung != null)
                    {
                        werte["Rechnung.Nummer"] = rechnung.cRechnungNr;
                        werte["Rechnung.Datum"] = ((DateTime)rechnung.dRechnungDatum).ToString("dd.MM.yyyy");
                        werte["Rechnung.Brutto"] = $"{rechnung.fBrutto:N2} €";
                        werte["Kunde.Name"] = $"{rechnung.cVorname} {rechnung.cNachname}".Trim();
                        werte["Kunde.Firma"] = rechnung.cFirma;
                        werte["Kunde.Anrede"] = rechnung.cAnrede;
                    }
                    break;

                case EmailVorlageTyp.Lieferschein:
                    var ls = await conn.QuerySingleOrDefaultAsync<dynamic>(
                        "SELECT l.*, k.* FROM tLieferschein l INNER JOIN tKunde k ON l.kKunde = k.kKunde WHERE l.kLieferschein = @Id",
                        new { Id = dokumentId });
                    if (ls != null)
                    {
                        werte["Lieferschein.Nummer"] = ls.cLieferscheinNr;
                        werte["Lieferschein.TrackingNr"] = ls.cTrackingNr;
                        werte["Kunde.Name"] = $"{ls.cVorname} {ls.cNachname}".Trim();
                    }
                    break;

                case EmailVorlageTyp.Angebot:
                    var angebot = await conn.QuerySingleOrDefaultAsync<dynamic>(
                        "SELECT a.*, k.* FROM tAngebot a INNER JOIN tKunde k ON a.kKunde = k.kKunde WHERE a.kAngebot = @Id",
                        new { Id = dokumentId });
                    if (angebot != null)
                    {
                        werte["Angebot.Nummer"] = angebot.cAngebotNr;
                        werte["Angebot.GueltigBis"] = ((DateTime)angebot.dGueltigBis).ToString("dd.MM.yyyy");
                        werte["Angebot.Brutto"] = $"{angebot.fBrutto:N2} €";
                        werte["Kunde.Name"] = $"{angebot.cVorname} {angebot.cNachname}".Trim();
                    }
                    break;
            }

            return werte;
        }

        /// <summary>
        /// Verfügbare Platzhalter für einen Typ
        /// </summary>
        public static List<PlatzhalterInfo> GetVerfuegbarePlatzhalter(EmailVorlageTyp typ)
        {
            var platzhalter = new List<PlatzhalterInfo>
            {
                // Allgemein
                new("{Datum}", "Aktuelles Datum"),
                new("{Zeit}", "Aktuelle Uhrzeit"),
                new("{Jahr}", "Aktuelles Jahr"),
                
                // Firma
                new("{Firma.Name}", "Firmenname"),
                new("{Firma.Strasse}", "Firmenstraße"),
                new("{Firma.PLZ}", "Firmen-PLZ"),
                new("{Firma.Ort}", "Firmenort"),
                new("{Firma.Telefon}", "Firmentelefon"),
                new("{Firma.Email}", "Firmen-E-Mail"),
                new("{Firma.Website}", "Firmen-Website"),
                
                // Kunde
                new("{Kunde.Anrede}", "Kundenanrede"),
                new("{Kunde.Name}", "Kundenname (Vorname Nachname)"),
                new("{Kunde.Firma}", "Kundenfirma"),
                new("{Kunde.Email}", "Kunden-E-Mail")
            };

            // Dokumentspezifisch
            switch (typ)
            {
                case EmailVorlageTyp.Rechnung:
                    platzhalter.Add(new("{Rechnung.Nummer}", "Rechnungsnummer"));
                    platzhalter.Add(new("{Rechnung.Datum}", "Rechnungsdatum"));
                    platzhalter.Add(new("{Rechnung.Brutto}", "Rechnungsbetrag"));
                    platzhalter.Add(new("{Rechnung.Faellig}", "Fälligkeitsdatum"));
                    break;
                    
                case EmailVorlageTyp.Lieferschein:
                    platzhalter.Add(new("{Lieferschein.Nummer}", "Lieferscheinnummer"));
                    platzhalter.Add(new("{Lieferschein.TrackingNr}", "Tracking-Nummer"));
                    platzhalter.Add(new("{Lieferschein.Versandart}", "Versandart"));
                    break;
                    
                case EmailVorlageTyp.Angebot:
                    platzhalter.Add(new("{Angebot.Nummer}", "Angebotsnummer"));
                    platzhalter.Add(new("{Angebot.GueltigBis}", "Gültig bis"));
                    platzhalter.Add(new("{Angebot.Brutto}", "Angebotsbetrag"));
                    break;
                    
                case EmailVorlageTyp.Mahnung:
                    platzhalter.Add(new("{Mahnung.Stufe}", "Mahnstufe"));
                    platzhalter.Add(new("{Mahnung.Betrag}", "Offener Betrag"));
                    break;
                    
                case EmailVorlageTyp.Bestellbestaetigung:
                    platzhalter.Add(new("{Bestellung.Nummer}", "Bestellnummer"));
                    platzhalter.Add(new("{Bestellung.Datum}", "Bestelldatum"));
                    platzhalter.Add(new("{Bestellung.Brutto}", "Bestellbetrag"));
                    break;
                    
                case EmailVorlageTyp.Versandbestaetigung:
                    platzhalter.Add(new("{Versand.TrackingNr}", "Tracking-Nummer"));
                    platzhalter.Add(new("{Versand.TrackingUrl}", "Tracking-Link"));
                    platzhalter.Add(new("{Versand.Carrier}", "Versanddienstleister"));
                    break;
            }

            return platzhalter;
        }
        #endregion

        #region Standard-Vorlagen erstellen
        /// <summary>
        /// Erstellt Standard-E-Mail-Vorlagen
        /// </summary>
        public async Task ErstelleStandardVorlagenAsync()
        {
            var conn = await _db.GetConnectionAsync();
            var vorhanden = await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM tEmailVorlage");
            if (vorhanden > 0) return;

            var vorlagen = new List<EmailVorlageErweitert>
            {
                new() {
                    Name = "Rechnung Standard",
                    Typ = EmailVorlageTyp.Rechnung.ToString(),
                    IstStandard = true,
                    Betreff = "Ihre Rechnung {Rechnung.Nummer}",
                    Text = @"Sehr geehrte Damen und Herren,

anbei erhalten Sie Ihre Rechnung {Rechnung.Nummer} vom {Rechnung.Datum}.

Rechnungsbetrag: {Rechnung.Brutto}
Fällig bis: {Rechnung.Faellig}

Bei Fragen stehen wir Ihnen gerne zur Verfügung.

Mit freundlichen Grüßen
{Firma.Name}",
                    IstHtml = false
                },
                new() {
                    Name = "Lieferschein Standard",
                    Typ = EmailVorlageTyp.Lieferschein.ToString(),
                    IstStandard = true,
                    Betreff = "Ihre Lieferung {Lieferschein.Nummer}",
                    Text = @"Sehr geehrte Damen und Herren,

anbei erhalten Sie Ihren Lieferschein {Lieferschein.Nummer}.

Mit freundlichen Grüßen
{Firma.Name}"
                },
                new() {
                    Name = "Versandbestätigung",
                    Typ = EmailVorlageTyp.Versandbestaetigung.ToString(),
                    IstStandard = true,
                    Betreff = "Ihre Bestellung wurde versendet",
                    Text = @"Sehr geehrte Damen und Herren,

Ihre Bestellung wurde heute versendet.

Versand über: {Versand.Carrier}
Tracking-Nummer: {Versand.TrackingNr}
Sendungsverfolgung: {Versand.TrackingUrl}

Mit freundlichen Grüßen
{Firma.Name}"
                },
                new() {
                    Name = "Angebot Standard",
                    Typ = EmailVorlageTyp.Angebot.ToString(),
                    IstStandard = true,
                    Betreff = "Ihr Angebot {Angebot.Nummer}",
                    Text = @"Sehr geehrte Damen und Herren,

anbei erhalten Sie Ihr angefordertes Angebot {Angebot.Nummer}.

Angebotssumme: {Angebot.Brutto}
Gültig bis: {Angebot.GueltigBis}

Bei Fragen stehen wir Ihnen gerne zur Verfügung.

Mit freundlichen Grüßen
{Firma.Name}"
                },
                new() {
                    Name = "Mahnung 1. Stufe",
                    Typ = EmailVorlageTyp.Mahnung.ToString(),
                    IstStandard = true,
                    Betreff = "Zahlungserinnerung - Rechnung {Rechnung.Nummer}",
                    Text = @"Sehr geehrte Damen und Herren,

bei Durchsicht unserer Konten haben wir festgestellt, dass folgende Rechnung noch nicht beglichen wurde:

Rechnung: {Rechnung.Nummer}
Betrag: {Mahnung.Betrag}

Sollte sich Ihre Zahlung mit diesem Schreiben überschneiden, betrachten Sie es bitte als gegenstandslos.

Mit freundlichen Grüßen
{Firma.Name}"
                }
            };

            foreach (var vorlage in vorlagen)
            {
                vorlage.Aktiv = true;
                vorlage.Sprache = "DE";
                await SaveVorlageAsync(vorlage);
            }

            _log.Information("Standard-E-Mail-Vorlagen erstellt");
        }
        #endregion
    }

    #region DTOs
    public class EmailVorlageMitAnlagen
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public EmailVorlageTyp Typ { get; set; }
        public string Betreff { get; set; } = "";
        public string Text { get; set; } = "";
        public string? HtmlText { get; set; }
        public bool IstHtml { get; set; }
        public bool IstStandard { get; set; }
        public bool Aktiv { get; set; } = true;
        public string Sprache { get; set; } = "DE";
        public List<EmailVorlageAnlage> Anlagen { get; set; } = new();
    }

    public class EmailVorlageAnlage
    {
        public int Id { get; set; }
        public int VorlageId { get; set; }
        public string Name { get; set; } = "";
        public string Pfad { get; set; } = "";
        public string? Dateiname { get; set; }
        public int Sortierung { get; set; }
        public bool ImmerAnhaengen { get; set; } = true;
    }

    public class PlatzhalterInfo
    {
        public string Code { get; set; }
        public string Beschreibung { get; set; }
        public PlatzhalterInfo(string code, string beschreibung) { Code = code; Beschreibung = beschreibung; }
    }

    public enum EmailVorlageTyp
    {
        Rechnung,
        Lieferschein,
        Angebot,
        Mahnung,
        Bestellbestaetigung,
        Versandbestaetigung,
        Gutschrift,
        Retoureneingang,
        Allgemein
    }
    #endregion
}

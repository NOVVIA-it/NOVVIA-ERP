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
    /// Formular-Verwaltung für Druckformulare (wie JTL)
    /// Unterstützt: Rechnung, Lieferschein, Angebot, Mahnung, Etiketten etc.
    /// </summary>
    public class FormularService
    {
        private readonly JtlDbContext _db;
        private static readonly ILogger _log = Log.ForContext<FormularService>();

        public FormularService(JtlDbContext db) => _db = db;

        #region Formular-Verwaltung
        /// <summary>
        /// Holt alle Formulare für einen Typ
        /// </summary>
        public async Task<IEnumerable<Formular>> GetFormulareAsync(FormularTyp? typ = null)
        {
            var conn = await _db.GetConnectionAsync();
            var sql = "SELECT * FROM tFormular WHERE 1=1";
            if (typ.HasValue) sql += " AND cTyp = @Typ";
            sql += " ORDER BY cTyp, nStandard DESC, cName";
            return await conn.QueryAsync<Formular>(sql, new { Typ = typ?.ToString() });
        }

        /// <summary>
        /// Holt das Standard-Formular für einen Typ
        /// </summary>
        public async Task<Formular?> GetStandardFormularAsync(FormularTyp typ)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QuerySingleOrDefaultAsync<Formular>(
                "SELECT TOP 1 * FROM tFormular WHERE cTyp = @Typ AND nStandard = 1", 
                new { Typ = typ.ToString() });
        }

        /// <summary>
        /// Speichert ein Formular
        /// </summary>
        public async Task<int> SaveFormularAsync(Formular formular)
        {
            var conn = await _db.GetConnectionAsync();
            
            if (formular.Id == 0)
            {
                return await conn.QuerySingleAsync<int>(@"
                    INSERT INTO tFormular (cName, cTyp, cBeschreibung, nStandard, nAktiv, 
                        nBreiteMM, nHoeheMM, cOrientation, nRandOben, nRandUnten, nRandLinks, nRandRechts,
                        cKopfzeile, cFusszeile, cInhalt, cCSS, dErstellt, dGeaendert)
                    VALUES (@Name, @Typ, @Beschreibung, @IstStandard, @Aktiv,
                        @BreiteMM, @HoeheMM, @Orientation, @RandOben, @RandUnten, @RandLinks, @RandRechts,
                        @Kopfzeile, @Fusszeile, @Inhalt, @CSS, GETDATE(), GETDATE());
                    SELECT SCOPE_IDENTITY();", formular);
            }

            await conn.ExecuteAsync(@"
                UPDATE tFormular SET cName=@Name, cBeschreibung=@Beschreibung, nStandard=@IstStandard, nAktiv=@Aktiv,
                    nBreiteMM=@BreiteMM, nHoeheMM=@HoeheMM, cOrientation=@Orientation,
                    nRandOben=@RandOben, nRandUnten=@RandUnten, nRandLinks=@RandLinks, nRandRechts=@RandRechts,
                    cKopfzeile=@Kopfzeile, cFusszeile=@Fusszeile, cInhalt=@Inhalt, cCSS=@CSS, dGeaendert=GETDATE()
                WHERE kFormular=@Id", formular);
            return formular.Id;
        }

        /// <summary>
        /// Dupliziert ein Formular
        /// </summary>
        public async Task<int> DupliziereFormularAsync(int formularId, string neuerName)
        {
            var conn = await _db.GetConnectionAsync();
            var original = await conn.QuerySingleOrDefaultAsync<Formular>(
                "SELECT * FROM tFormular WHERE kFormular = @Id", new { Id = formularId });
            
            if (original == null) throw new Exception("Formular nicht gefunden");
            
            original.Id = 0;
            original.Name = neuerName;
            original.IstStandard = false;
            
            return await SaveFormularAsync(original);
        }

        /// <summary>
        /// Exportiert ein Formular als JSON
        /// </summary>
        public async Task<string> ExportFormularAsync(int formularId)
        {
            var conn = await _db.GetConnectionAsync();
            var formular = await conn.QuerySingleOrDefaultAsync<Formular>(
                "SELECT * FROM tFormular WHERE kFormular = @Id", new { Id = formularId });
            return System.Text.Json.JsonSerializer.Serialize(formular, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }

        /// <summary>
        /// Importiert ein Formular aus JSON
        /// </summary>
        public async Task<int> ImportFormularAsync(string json)
        {
            var formular = System.Text.Json.JsonSerializer.Deserialize<Formular>(json);
            if (formular == null) throw new Exception("Ungültiges Formular-Format");
            formular.Id = 0;
            formular.IstStandard = false;
            return await SaveFormularAsync(formular);
        }
        #endregion

        #region Formular-Elemente
        /// <summary>
        /// Verfügbare Platzhalter für ein Formular
        /// </summary>
        public Dictionary<string, List<FormularPlatzhalter>> GetPlatzhalter(FormularTyp typ)
        {
            var platzhalter = new Dictionary<string, List<FormularPlatzhalter>>();

            // Firma
            platzhalter["Firma"] = new()
            {
                new("{Firma.Name}", "Firmenname"),
                new("{Firma.Strasse}", "Straße"),
                new("{Firma.PLZ}", "PLZ"),
                new("{Firma.Ort}", "Ort"),
                new("{Firma.Land}", "Land"),
                new("{Firma.Telefon}", "Telefon"),
                new("{Firma.Email}", "E-Mail"),
                new("{Firma.Website}", "Website"),
                new("{Firma.UStID}", "USt-ID"),
                new("{Firma.Steuernummer}", "Steuernummer"),
                new("{Firma.IBAN}", "IBAN"),
                new("{Firma.BIC}", "BIC"),
                new("{Firma.Bank}", "Bank"),
                new("{Firma.Logo}", "Logo (als Bild)")
            };

            // Kunde
            platzhalter["Kunde"] = new()
            {
                new("{Kunde.KundenNr}", "Kundennummer"),
                new("{Kunde.Anrede}", "Anrede"),
                new("{Kunde.Firma}", "Firma"),
                new("{Kunde.Name}", "Vollständiger Name"),
                new("{Kunde.Vorname}", "Vorname"),
                new("{Kunde.Nachname}", "Nachname"),
                new("{Kunde.Strasse}", "Straße"),
                new("{Kunde.PLZ}", "PLZ"),
                new("{Kunde.Ort}", "Ort"),
                new("{Kunde.Land}", "Land"),
                new("{Kunde.Email}", "E-Mail"),
                new("{Kunde.Telefon}", "Telefon"),
                new("{Kunde.UStID}", "USt-ID")
            };

            // Dokument-spezifisch
            switch (typ)
            {
                case FormularTyp.Rechnung:
                    platzhalter["Rechnung"] = new()
                    {
                        new("{Rechnung.Nummer}", "Rechnungsnummer"),
                        new("{Rechnung.Datum}", "Rechnungsdatum"),
                        new("{Rechnung.Faellig}", "Fälligkeitsdatum"),
                        new("{Rechnung.Netto}", "Nettobetrag"),
                        new("{Rechnung.MwSt}", "MwSt-Betrag"),
                        new("{Rechnung.Brutto}", "Bruttobetrag"),
                        new("{Rechnung.Bezahlt}", "Bereits bezahlt"),
                        new("{Rechnung.Offen}", "Offener Betrag"),
                        new("{Rechnung.Zahlungsbedingung}", "Zahlungsbedingung"),
                        new("{Rechnung.Skonto}", "Skonto"),
                        new("{Rechnung.SkontoDatum}", "Skonto bis"),
                        new("{Rechnung.Lieferdatum}", "Lieferdatum"),
                        new("{Rechnung.BestellNr}", "Bestellnummer Kunde")
                    };
                    break;

                case FormularTyp.Lieferschein:
                    platzhalter["Lieferschein"] = new()
                    {
                        new("{Lieferschein.Nummer}", "Lieferscheinnummer"),
                        new("{Lieferschein.Datum}", "Datum"),
                        new("{Lieferschein.Versandart}", "Versandart"),
                        new("{Lieferschein.TrackingNr}", "Tracking-Nummer"),
                        new("{Lieferschein.Gewicht}", "Gesamtgewicht"),
                        new("{Lieferschein.Pakete}", "Anzahl Pakete")
                    };
                    break;

                case FormularTyp.Angebot:
                    platzhalter["Angebot"] = new()
                    {
                        new("{Angebot.Nummer}", "Angebotsnummer"),
                        new("{Angebot.Datum}", "Angebotsdatum"),
                        new("{Angebot.GueltigBis}", "Gültig bis"),
                        new("{Angebot.Netto}", "Nettobetrag"),
                        new("{Angebot.Brutto}", "Bruttobetrag")
                    };
                    break;

                case FormularTyp.Mahnung:
                    platzhalter["Mahnung"] = new()
                    {
                        new("{Mahnung.Stufe}", "Mahnstufe"),
                        new("{Mahnung.Datum}", "Mahndatum"),
                        new("{Mahnung.Gebuehr}", "Mahngebühr"),
                        new("{Mahnung.Zinsen}", "Verzugszinsen"),
                        new("{Mahnung.Gesamtforderung}", "Gesamtforderung")
                    };
                    break;
            }

            // Positionen (für alle Dokumente mit Positionen)
            if (typ != FormularTyp.Versandetikett && typ != FormularTyp.Artikeletikett)
            {
                platzhalter["Position"] = new()
                {
                    new("{Pos.Nr}", "Positionsnummer"),
                    new("{Pos.ArtNr}", "Artikelnummer"),
                    new("{Pos.Name}", "Artikelname"),
                    new("{Pos.Beschreibung}", "Beschreibung"),
                    new("{Pos.Menge}", "Menge"),
                    new("{Pos.Einheit}", "Einheit"),
                    new("{Pos.Einzelpreis}", "Einzelpreis"),
                    new("{Pos.Rabatt}", "Rabatt %"),
                    new("{Pos.Netto}", "Netto Gesamt"),
                    new("{Pos.MwStSatz}", "MwSt-Satz"),
                    new("{Pos.MwSt}", "MwSt-Betrag"),
                    new("{Pos.Brutto}", "Brutto Gesamt")
                };
            }

            // Sonstiges
            platzhalter["System"] = new()
            {
                new("{Heute}", "Aktuelles Datum"),
                new("{Zeit}", "Aktuelle Uhrzeit"),
                new("{Jahr}", "Aktuelles Jahr"),
                new("{Seite}", "Seitennummer"),
                new("{Seiten}", "Gesamtseiten"),
                new("{Barcode}", "Barcode der Dokumentnummer"),
                new("{QRCode}", "QR-Code")
            };

            return platzhalter;
        }
        #endregion

        #region Standard-Formulare erstellen
        /// <summary>
        /// Erstellt Standard-Formulare wenn keine vorhanden
        /// </summary>
        public async Task ErstelleStandardFormulareAsync()
        {
            var conn = await _db.GetConnectionAsync();
            var vorhanden = await conn.QuerySingleAsync<int>("SELECT COUNT(*) FROM tFormular");
            if (vorhanden > 0) return;

            // Rechnung
            await SaveFormularAsync(new Formular
            {
                Name = "Rechnung Standard",
                Typ = FormularTyp.Rechnung.ToString(),
                IstStandard = true,
                BreiteMM = 210,
                HoeheMM = 297,
                RandOben = 20,
                RandUnten = 20,
                RandLinks = 20,
                RandRechts = 15,
                Kopfzeile = GetStandardKopfzeile(),
                Fusszeile = GetStandardFusszeile(),
                Inhalt = GetRechnungInhalt(),
                CSS = GetStandardCSS()
            });

            // Lieferschein
            await SaveFormularAsync(new Formular
            {
                Name = "Lieferschein Standard",
                Typ = FormularTyp.Lieferschein.ToString(),
                IstStandard = true,
                BreiteMM = 210,
                HoeheMM = 297,
                Kopfzeile = GetStandardKopfzeile(),
                Fusszeile = GetStandardFusszeile(),
                Inhalt = GetLieferscheinInhalt(),
                CSS = GetStandardCSS()
            });

            // Versandetikett
            await SaveFormularAsync(new Formular
            {
                Name = "Versandetikett 100x150",
                Typ = FormularTyp.Versandetikett.ToString(),
                IstStandard = true,
                BreiteMM = 100,
                HoeheMM = 150,
                RandOben = 5,
                RandUnten = 5,
                RandLinks = 5,
                RandRechts = 5,
                Inhalt = GetVersandetikettInhalt()
            });

            _log.Information("Standard-Formulare erstellt");
        }

        private string GetStandardKopfzeile() => @"
<div class='kopfzeile'>
    <div class='logo'>{Firma.Logo}</div>
    <div class='firmeninfo'>
        <strong>{Firma.Name}</strong><br/>
        {Firma.Strasse}<br/>
        {Firma.PLZ} {Firma.Ort}
    </div>
</div>";

        private string GetStandardFusszeile() => @"
<div class='fusszeile'>
    <div class='spalte'>{Firma.Name}<br/>{Firma.Strasse}<br/>{Firma.PLZ} {Firma.Ort}</div>
    <div class='spalte'>Tel: {Firma.Telefon}<br/>E-Mail: {Firma.Email}<br/>Web: {Firma.Website}</div>
    <div class='spalte'>Bank: {Firma.Bank}<br/>IBAN: {Firma.IBAN}<br/>BIC: {Firma.BIC}</div>
    <div class='spalte'>USt-ID: {Firma.UStID}<br/>Steuernr: {Firma.Steuernummer}</div>
</div>";

        private string GetRechnungInhalt() => @"
<div class='adresse'>{Kunde.Firma}<br/>{Kunde.Name}<br/>{Kunde.Strasse}<br/>{Kunde.PLZ} {Kunde.Ort}</div>
<h1>Rechnung {Rechnung.Nummer}</h1>
<div class='meta'>
    <span>Rechnungsdatum: {Rechnung.Datum}</span>
    <span>Kundennummer: {Kunde.KundenNr}</span>
    <span>Fällig: {Rechnung.Faellig}</span>
</div>
<table class='positionen'>
    <thead><tr><th>Pos</th><th>Art.Nr.</th><th>Bezeichnung</th><th>Menge</th><th>Einzelpreis</th><th>Gesamt</th></tr></thead>
    <tbody><!-- POSITIONEN --></tbody>
</table>
<div class='summen'>
    <div>Netto: {Rechnung.Netto}</div>
    <div>MwSt: {Rechnung.MwSt}</div>
    <div class='brutto'>Brutto: {Rechnung.Brutto}</div>
</div>
<div class='zahlungshinweis'>{Rechnung.Zahlungsbedingung}</div>";

        private string GetLieferscheinInhalt() => @"
<div class='adresse'>{Kunde.Firma}<br/>{Kunde.Name}<br/>{Kunde.Strasse}<br/>{Kunde.PLZ} {Kunde.Ort}</div>
<h1>Lieferschein {Lieferschein.Nummer}</h1>
<table class='positionen'>
    <thead><tr><th>Pos</th><th>Art.Nr.</th><th>Bezeichnung</th><th>Menge</th></tr></thead>
    <tbody><!-- POSITIONEN --></tbody>
</table>";

        private string GetVersandetikettInhalt() => @"
<div class='absender'>{Firma.Name}, {Firma.Strasse}, {Firma.PLZ} {Firma.Ort}</div>
<div class='empfaenger'>
    <strong>{Kunde.Name}</strong><br/>
    {Kunde.Strasse}<br/>
    <span class='plz'>{Kunde.PLZ}</span> <span class='ort'>{Kunde.Ort}</span><br/>
    {Kunde.Land}
</div>
<div class='barcode'>{Barcode}</div>";

        private string GetStandardCSS() => @"
body { font-family: Arial, sans-serif; font-size: 10pt; }
.kopfzeile { display: flex; justify-content: space-between; margin-bottom: 30px; }
.logo img { max-height: 60px; }
.adresse { margin: 40px 0 20px 0; }
h1 { font-size: 16pt; margin: 20px 0; }
.meta { display: flex; gap: 30px; margin-bottom: 20px; color: #666; }
.positionen { width: 100%; border-collapse: collapse; margin: 20px 0; }
.positionen th, .positionen td { border-bottom: 1px solid #ddd; padding: 8px; text-align: left; }
.positionen th { background: #f5f5f5; }
.summen { text-align: right; margin-top: 20px; }
.brutto { font-size: 14pt; font-weight: bold; margin-top: 10px; }
.fusszeile { display: flex; justify-content: space-between; font-size: 8pt; color: #666; border-top: 1px solid #ddd; padding-top: 10px; }";
        #endregion
    }

    #region DTOs
    public class Formular
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Typ { get; set; } = "";
        public string? Beschreibung { get; set; }
        public bool IstStandard { get; set; }
        public bool Aktiv { get; set; } = true;
        public int BreiteMM { get; set; } = 210;
        public int HoeheMM { get; set; } = 297;
        public string Orientation { get; set; } = "Portrait";
        public int RandOben { get; set; } = 20;
        public int RandUnten { get; set; } = 20;
        public int RandLinks { get; set; } = 20;
        public int RandRechts { get; set; } = 15;
        public string? Kopfzeile { get; set; }
        public string? Fusszeile { get; set; }
        public string? Inhalt { get; set; }
        public string? CSS { get; set; }
        public DateTime Erstellt { get; set; }
        public DateTime Geaendert { get; set; }
    }

    public class FormularPlatzhalter
    {
        public string Code { get; set; }
        public string Beschreibung { get; set; }
        public FormularPlatzhalter(string code, string beschreibung) { Code = code; Beschreibung = beschreibung; }
    }

    public enum FormularTyp
    {
        Rechnung, Lieferschein, Angebot, Mahnung, Gutschrift,
        Auftragsbestaetigung, Bestellung, Packliste,
        Versandetikett, Artikeletikett, Retourenschein
    }
    #endregion
}

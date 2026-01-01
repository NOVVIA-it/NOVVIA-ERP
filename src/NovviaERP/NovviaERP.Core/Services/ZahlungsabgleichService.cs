using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NovviaERP.Core.Data;
using Serilog;
using static NovviaERP.Core.Data.JtlDbContext;

namespace NovviaERP.Core.Services
{
    /// <summary>
    /// Service fuer JTL-nativen Zahlungsabgleich (tZahlungsabgleichUmsatz, tZahlung)
    /// </summary>
    public class ZahlungsabgleichService
    {
        private readonly JtlDbContext _db;
        private readonly LogService _logService;
        private static readonly ILogger _log = Log.ForContext<ZahlungsabgleichService>();

        // Einstellungs-Schluessel
        public const string SETTING_AUTOMATCH_SCHWELLE = "Zahlungsabgleich.AutoMatchSchwelle";
        public const int DEFAULT_AUTOMATCH_SCHWELLE = 90;

        public ZahlungsabgleichService(JtlDbContext db, LogService? logService = null)
        {
            _db = db;
            _logService = logService ?? new LogService(db);
        }

        /// <summary>Liest die Auto-Matching Schwelle aus den Einstellungen</summary>
        public async Task<int> GetAutoMatchSchwelleAsync()
        {
            return await _db.GetEinstellungIntAsync(SETTING_AUTOMATCH_SCHWELLE, DEFAULT_AUTOMATCH_SCHWELLE);
        }

        /// <summary>Setzt die Auto-Matching Schwelle</summary>
        public async Task SetAutoMatchSchwelleAsync(int schwelle, int? kBenutzer = null)
        {
            await _db.SetEinstellungAsync(SETTING_AUTOMATCH_SCHWELLE, schwelle.ToString(),
                "Auto-Matching Schwelle fuer Zahlungsabgleich (0-100%)", kBenutzer);
        }

        #region Auto-Matching

        /// <summary>
        /// Fuehrt Auto-Matching fuer alle offenen Transaktionen durch
        /// </summary>
        public async Task<AutoMatchResult> AutoMatchAsync(int kBenutzer, bool nurVorschlaege = false)
        {
            var result = new AutoMatchResult();

            // Auto-Match Schwelle aus Einstellungen laden
            var schwelle = await GetAutoMatchSchwelleAsync();
            result.SchwelleVerwendet = schwelle;

            // Offene Transaktionen laden
            var offeneUmsaetze = await _db.GetOffeneUmsaetzeAsync();
            if (!offeneUmsaetze.Any())
            {
                _log.Information("Keine offenen Transaktionen zum Matchen");
                return result;
            }

            // Offene Rechnungen und Auftraege laden
            var offeneRechnungen = await _db.GetOffeneRechnungenFuerMatchingAsync();
            var offeneAuftraege = await _db.GetOffeneAuftraegeFuerMatchingAsync();

            _log.Information("Auto-Matching: {Umsaetze} Transaktionen gegen {Rechnungen} Rechnungen und {Auftraege} Auftraege (Schwelle: {Schwelle}%)",
                offeneUmsaetze.Count, offeneRechnungen.Count, offeneAuftraege.Count, schwelle);

            await _logService.LogAsync("Zahlungsabgleich", "AutoMatch Start", "Zahlungsabgleich",
                beschreibung: $"Auto-Matching gestartet: {offeneUmsaetze.Count} Transaktionen (Schwelle: {schwelle}%)",
                kBenutzer: kBenutzer);

            foreach (var umsatz in offeneUmsaetze)
            {
                var match = FindBestMatch(umsatz, offeneRechnungen, offeneAuftraege);
                if (match == null) continue;

                result.Vorschlaege.Add(match);

                // Bei Konfidenz >= Schwelle und nicht nur Vorschlaege: automatisch zuordnen
                if (!nurVorschlaege && match.Konfidenz >= schwelle)
                {
                    try
                    {
                        if (match.KRechnung.HasValue)
                        {
                            await _db.ZuordnenZuRechnungAsync(
                                umsatz.KZahlungsabgleichUmsatz,
                                match.KRechnung.Value,
                                umsatz.FBetrag,
                                kBenutzer,
                                match.Konfidenz);

                            await _logService.LogAsync("Zahlungsabgleich", "AutoMatch", "Zahlungsabgleich",
                                entityTyp: "Rechnung", kEntity: match.KRechnung.Value, entityNr: match.RechnungsNr,
                                beschreibung: $"Auto-Zuordnung: {umsatz.FBetrag:N2} EUR von {umsatz.CName}",
                                details: string.Join(", ", match.MatchGruende),
                                betragBrutto: umsatz.FBetrag, kBenutzer: kBenutzer);

                            // Aus offenen Rechnungen entfernen
                            offeneRechnungen.RemoveAll(r => r.KRechnung == match.KRechnung.Value);
                            result.AutoGematcht++;
                        }
                        else if (match.KAuftrag.HasValue)
                        {
                            await _db.ZuordnenZuAuftragAsync(
                                umsatz.KZahlungsabgleichUmsatz,
                                match.KAuftrag.Value,
                                umsatz.FBetrag,
                                kBenutzer,
                                match.Konfidenz);

                            await _logService.LogAsync("Zahlungsabgleich", "AutoMatch", "Zahlungsabgleich",
                                entityTyp: "Auftrag", kEntity: match.KAuftrag.Value, entityNr: match.AuftragNr,
                                beschreibung: $"Auto-Zuordnung: {umsatz.FBetrag:N2} EUR von {umsatz.CName}",
                                betragBrutto: umsatz.FBetrag, kBenutzer: kBenutzer);

                            offeneAuftraege.RemoveAll(a => a.KAuftrag == match.KAuftrag.Value);
                            result.AutoGematcht++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.Warning(ex, "Fehler beim Auto-Matching von Transaktion {Id}", umsatz.KZahlungsabgleichUmsatz);
                    }
                }
            }

            await _logService.LogAsync("Zahlungsabgleich", "AutoMatch Ende", "Zahlungsabgleich",
                beschreibung: $"Auto-Matching abgeschlossen: {result.AutoGematcht} automatisch, {result.Vorschlaege.Count} Vorschlaege",
                kBenutzer: kBenutzer);

            _log.Information("Auto-Matching abgeschlossen: {Auto} automatisch, {Vorschlaege} Vorschlaege",
                result.AutoGematcht, result.Vorschlaege.Count);

            return result;
        }

        /// <summary>
        /// Findet den besten Match fuer eine Transaktion
        /// </summary>
        private MatchVorschlag? FindBestMatch(
            ZahlungsabgleichUmsatz umsatz,
            List<OffeneRechnungInfo> rechnungen,
            List<OffenerAuftragInfo> auftraege)
        {
            var verwendung = umsatz.CVerwendungszweck ?? "";
            var zahlerName = umsatz.CName ?? "";
            var zahlerIBAN = umsatz.CKonto ?? "";
            var betrag = umsatz.FBetrag;

            MatchVorschlag? bestMatch = null;
            int bestScore = 0;

            // 1. Rechnungen pruefen
            foreach (var rechnung in rechnungen)
            {
                int score = 0;
                var gruende = new List<string>();

                // Rechnungsnummer im Verwendungszweck
                if (ContainsRechnungsNr(verwendung, rechnung.RechnungsNr))
                {
                    score += 50;
                    gruende.Add($"Rechnungsnummer '{rechnung.RechnungsNr}' gefunden");
                }

                // Kundennummer im Verwendungszweck
                if (!string.IsNullOrEmpty(rechnung.KundenNr) && verwendung.Contains(rechnung.KundenNr, StringComparison.OrdinalIgnoreCase))
                {
                    score += 20;
                    gruende.Add($"Kundennummer '{rechnung.KundenNr}' gefunden");
                }

                // Betrag exakt gleich
                if (Math.Abs(betrag - rechnung.OffenerBetrag) < 0.01m)
                {
                    score += 30;
                    gruende.Add("Betrag exakt");
                }
                // Betrag nahe (Teilzahlung oder Rundung)
                else if (Math.Abs(betrag - rechnung.OffenerBetrag) < 1.00m)
                {
                    score += 15;
                    gruende.Add("Betrag nahezu gleich");
                }

                // Name/Firma pruefen
                if (NameMatch(zahlerName, rechnung.KundeFirma, rechnung.KundeNachname))
                {
                    score += 20;
                    gruende.Add("Name passt");
                }

                // IBAN pruefen
                if (!string.IsNullOrEmpty(rechnung.KundeIBAN) &&
                    !string.IsNullOrEmpty(zahlerIBAN) &&
                    NormalizeIBAN(zahlerIBAN) == NormalizeIBAN(rechnung.KundeIBAN))
                {
                    score += 25;
                    gruende.Add("IBAN passt");
                }

                if (score > bestScore && score >= 30)
                {
                    bestScore = score;
                    bestMatch = new MatchVorschlag
                    {
                        KZahlungsabgleichUmsatz = umsatz.KZahlungsabgleichUmsatz,
                        Betrag = betrag,
                        ZahlerName = zahlerName,
                        Verwendungszweck = verwendung,
                        KRechnung = rechnung.KRechnung,
                        RechnungsNr = rechnung.RechnungsNr,
                        KKunde = rechnung.KKunde,
                        KundeName = rechnung.KundeFirma ?? rechnung.KundeNachname ?? "",
                        OffenerBetrag = rechnung.OffenerBetrag,
                        Konfidenz = Math.Min(100, score),
                        MatchGruende = gruende
                    };
                }
            }

            // 2. Auftraege pruefen (wenn keine gute Rechnung gefunden)
            if (bestScore < 70)
            {
                foreach (var auftrag in auftraege)
                {
                    int score = 0;
                    var gruende = new List<string>();

                    // Auftragsnummer im Verwendungszweck
                    if (ContainsAuftragNr(verwendung, auftrag.AuftragNr))
                    {
                        score += 50;
                        gruende.Add($"Auftragsnummer '{auftrag.AuftragNr}' gefunden");
                    }

                    // Kundennummer
                    if (!string.IsNullOrEmpty(auftrag.KundenNr) && verwendung.Contains(auftrag.KundenNr, StringComparison.OrdinalIgnoreCase))
                    {
                        score += 20;
                        gruende.Add($"Kundennummer '{auftrag.KundenNr}' gefunden");
                    }

                    // Betrag
                    if (Math.Abs(betrag - auftrag.OffenerBetrag) < 0.01m)
                    {
                        score += 30;
                        gruende.Add("Betrag exakt");
                    }
                    else if (Math.Abs(betrag - auftrag.OffenerBetrag) < 1.00m)
                    {
                        score += 15;
                        gruende.Add("Betrag nahezu gleich");
                    }

                    // Name
                    if (NameMatch(zahlerName, auftrag.KundeFirma, auftrag.KundeNachname))
                    {
                        score += 20;
                        gruende.Add("Name passt");
                    }

                    // IBAN
                    if (!string.IsNullOrEmpty(auftrag.KundeIBAN) &&
                        !string.IsNullOrEmpty(zahlerIBAN) &&
                        NormalizeIBAN(zahlerIBAN) == NormalizeIBAN(auftrag.KundeIBAN))
                    {
                        score += 25;
                        gruende.Add("IBAN passt");
                    }

                    if (score > bestScore && score >= 30)
                    {
                        bestScore = score;
                        bestMatch = new MatchVorschlag
                        {
                            KZahlungsabgleichUmsatz = umsatz.KZahlungsabgleichUmsatz,
                            Betrag = betrag,
                            ZahlerName = zahlerName,
                            Verwendungszweck = verwendung,
                            KAuftrag = auftrag.KAuftrag,
                            AuftragNr = auftrag.AuftragNr,
                            KKunde = auftrag.KKunde,
                            KundeName = auftrag.KundeFirma ?? auftrag.KundeNachname ?? "",
                            OffenerBetrag = auftrag.OffenerBetrag,
                            Konfidenz = Math.Min(100, score),
                            MatchGruende = gruende
                        };
                    }
                }
            }

            return bestMatch;
        }

        /// <summary>
        /// Prueft ob Verwendungszweck eine Rechnungsnummer enthaelt
        /// </summary>
        private bool ContainsRechnungsNr(string verwendung, string rechnungsNr)
        {
            if (string.IsNullOrEmpty(verwendung) || string.IsNullOrEmpty(rechnungsNr))
                return false;

            // Exakte Suche
            if (verwendung.Contains(rechnungsNr, StringComparison.OrdinalIgnoreCase))
                return true;

            // Ohne Prefix (RE-, RG-, etc.)
            var nrOhnePrefix = Regex.Replace(rechnungsNr, @"^[A-Za-z]{1,3}[-_]?", "");
            if (!string.IsNullOrEmpty(nrOhnePrefix) && verwendung.Contains(nrOhnePrefix))
                return true;

            // Typische Muster: RE12345, R-12345, Rechnung 12345
            var patterns = new[]
            {
                $@"\b{Regex.Escape(rechnungsNr)}\b",
                $@"RE[-_]?{Regex.Escape(nrOhnePrefix)}",
                $@"RG[-_]?{Regex.Escape(nrOhnePrefix)}",
                $@"Rechnung\s*[-:#]?\s*{Regex.Escape(nrOhnePrefix)}"
            };

            return patterns.Any(p => Regex.IsMatch(verwendung, p, RegexOptions.IgnoreCase));
        }

        /// <summary>
        /// Prueft ob Verwendungszweck eine Auftragsnummer enthaelt
        /// </summary>
        private bool ContainsAuftragNr(string verwendung, string auftragNr)
        {
            if (string.IsNullOrEmpty(verwendung) || string.IsNullOrEmpty(auftragNr))
                return false;

            if (verwendung.Contains(auftragNr, StringComparison.OrdinalIgnoreCase))
                return true;

            var nrOhnePrefix = Regex.Replace(auftragNr, @"^[A-Za-z]{1,3}[-_]?", "");
            if (!string.IsNullOrEmpty(nrOhnePrefix) && verwendung.Contains(nrOhnePrefix))
                return true;

            var patterns = new[]
            {
                $@"\b{Regex.Escape(auftragNr)}\b",
                $@"AU[-_]?{Regex.Escape(nrOhnePrefix)}",
                $@"Auftrag\s*[-:#]?\s*{Regex.Escape(nrOhnePrefix)}",
                $@"Best[-.]?\s*[-:#]?\s*{Regex.Escape(nrOhnePrefix)}"
            };

            return patterns.Any(p => Regex.IsMatch(verwendung, p, RegexOptions.IgnoreCase));
        }

        /// <summary>
        /// Prueft ob Name uebereinstimmt
        /// </summary>
        private bool NameMatch(string zahlerName, string? firma, string? nachname)
        {
            if (string.IsNullOrEmpty(zahlerName))
                return false;

            zahlerName = zahlerName.ToUpperInvariant();

            if (!string.IsNullOrEmpty(firma) && zahlerName.Contains(firma.ToUpperInvariant()))
                return true;

            if (!string.IsNullOrEmpty(nachname) && zahlerName.Contains(nachname.ToUpperInvariant()))
                return true;

            return false;
        }

        /// <summary>
        /// Normalisiert IBAN fuer Vergleich
        /// </summary>
        private string NormalizeIBAN(string iban)
        {
            return Regex.Replace(iban ?? "", @"[\s\-]", "").ToUpperInvariant();
        }

        #endregion

        #region Manuelle Zuordnung

        /// <summary>
        /// Ordnet Transaktion manuell einer Rechnung zu
        /// </summary>
        public async Task<int> ZuordnenZuRechnungAsync(int kZahlungsabgleichUmsatz, int kRechnung, int kBenutzer)
        {
            var umsatz = (await _db.GetOffeneUmsaetzeAsync()).FirstOrDefault(u => u.KZahlungsabgleichUmsatz == kZahlungsabgleichUmsatz);
            if (umsatz == null)
                throw new Exception($"Transaktion {kZahlungsabgleichUmsatz} nicht gefunden oder bereits zugeordnet");

            var result = await _db.ZuordnenZuRechnungAsync(kZahlungsabgleichUmsatz, kRechnung, umsatz.FBetrag, kBenutzer, 100);

            await _logService.LogAsync("Zahlungsabgleich", "Manuell Zuordnen", "Zahlungsabgleich",
                entityTyp: "Rechnung", kEntity: kRechnung,
                beschreibung: $"Manuelle Zuordnung: {umsatz.FBetrag:N2} EUR von {umsatz.CName}",
                betragBrutto: umsatz.FBetrag, kBenutzer: kBenutzer);

            return result;
        }

        /// <summary>
        /// Ordnet Transaktion manuell einem Auftrag zu
        /// </summary>
        public async Task<int> ZuordnenZuAuftragAsync(int kZahlungsabgleichUmsatz, int kAuftrag, int kBenutzer)
        {
            var umsatz = (await _db.GetOffeneUmsaetzeAsync()).FirstOrDefault(u => u.KZahlungsabgleichUmsatz == kZahlungsabgleichUmsatz);
            if (umsatz == null)
                throw new Exception($"Transaktion {kZahlungsabgleichUmsatz} nicht gefunden oder bereits zugeordnet");

            var result = await _db.ZuordnenZuAuftragAsync(kZahlungsabgleichUmsatz, kAuftrag, umsatz.FBetrag, kBenutzer, 100);

            await _logService.LogAsync("Zahlungsabgleich", "Manuell Zuordnen", "Zahlungsabgleich",
                entityTyp: "Auftrag", kEntity: kAuftrag,
                beschreibung: $"Manuelle Zuordnung: {umsatz.FBetrag:N2} EUR von {umsatz.CName}",
                betragBrutto: umsatz.FBetrag, kBenutzer: kBenutzer);

            return result;
        }

        /// <summary>
        /// Ignoriert eine Transaktion (z.B. Gebuehren, Umbuchungen)
        /// </summary>
        public async Task IgnorierenAsync(int kZahlungsabgleichUmsatz, int kBenutzer)
        {
            await _db.IgnoriereUmsatzAsync(kZahlungsabgleichUmsatz, kBenutzer);

            await _logService.LogAsync("Zahlungsabgleich", "Ignorieren", "Zahlungsabgleich",
                entityTyp: "Transaktion", kEntity: kZahlungsabgleichUmsatz,
                beschreibung: "Transaktion als ignoriert markiert", kBenutzer: kBenutzer);

            _log.Information("Transaktion {Id} als ignoriert markiert", kZahlungsabgleichUmsatz);
        }

        /// <summary>
        /// Hebt Zuordnung auf
        /// </summary>
        public async Task ZuordnungAufhebenAsync(int kZahlungsabgleichUmsatz, int kBenutzer)
        {
            await _db.ZuordnungAufhebenAsync(kZahlungsabgleichUmsatz);

            await _logService.LogAsync("Zahlungsabgleich", "Zuordnung aufheben", "Zahlungsabgleich",
                entityTyp: "Transaktion", kEntity: kZahlungsabgleichUmsatz,
                beschreibung: "Zuordnung aufgehoben", kBenutzer: kBenutzer);

            _log.Information("Zuordnung fuer Transaktion {Id} aufgehoben", kZahlungsabgleichUmsatz);
        }

        #endregion

        #region Import

        /// <summary>
        /// Importiert PayPal-Transaktionen in tZahlungsabgleichUmsatz
        /// </summary>
        public async Task<ImportResult> ImportPayPalTransaktionenAsync(List<PayPalTransaktion> transaktionen, int kModul, int kBenutzer)
        {
            var result = new ImportResult();

            foreach (var tx in transaktionen)
            {
                // Nur eingehende Zahlungen
                if (tx.Betrag <= 0) continue;

                var umsatz = new ZahlungsabgleichUmsatz
                {
                    KZahlungsabgleichModul = kModul,
                    CTransaktionID = tx.TransaktionId,
                    DBuchungsdatum = tx.Datum,
                    FBetrag = tx.Betrag,
                    CWaehrungISO = tx.Waehrung ?? "EUR",
                    CName = tx.ZahlerName,
                    CKonto = tx.ZahlerEmail,
                    CVerwendungszweck = tx.Beschreibung
                };

                var id = await _db.ImportUmsatzAsync(umsatz);
                if (id > 0)
                    result.Importiert++;
                else
                    result.Uebersprungen++;
            }

            await _logService.LogAsync("Zahlungsabgleich", "PayPal Import", "PayPal",
                beschreibung: $"PayPal-Import: {result.Importiert} importiert, {result.Uebersprungen} uebersprungen",
                kBenutzer: kBenutzer);

            _log.Information("PayPal-Import: {Importiert} importiert, {Uebersprungen} uebersprungen",
                result.Importiert, result.Uebersprungen);

            return result;
        }

        /// <summary>
        /// Importiert Mollie-Transaktionen
        /// </summary>
        public async Task<ImportResult> ImportMollieTransaktionenAsync(List<MollieTransaktion> transaktionen, int kModul, int kBenutzer)
        {
            var result = new ImportResult();

            foreach (var tx in transaktionen)
            {
                if (tx.Betrag <= 0 || tx.Status != "paid") continue;

                var umsatz = new ZahlungsabgleichUmsatz
                {
                    KZahlungsabgleichModul = kModul,
                    CTransaktionID = tx.Id,
                    DBuchungsdatum = tx.PaidAt ?? tx.CreatedAt,
                    FBetrag = tx.Betrag,
                    CWaehrungISO = tx.Waehrung ?? "EUR",
                    CName = tx.Beschreibung,
                    CVerwendungszweck = tx.Beschreibung
                };

                var id = await _db.ImportUmsatzAsync(umsatz);
                if (id > 0)
                    result.Importiert++;
                else
                    result.Uebersprungen++;
            }

            await _logService.LogAsync("Zahlungsabgleich", "Mollie Import", "Mollie",
                beschreibung: $"Mollie-Import: {result.Importiert} importiert, {result.Uebersprungen} uebersprungen",
                kBenutzer: kBenutzer);

            _log.Information("Mollie-Import: {Importiert} importiert, {Uebersprungen} uebersprungen",
                result.Importiert, result.Uebersprungen);

            return result;
        }

        /// <summary>
        /// Importiert MT940-Bankumsaetze (Sparkasse, etc.)
        /// </summary>
        public async Task<ImportResult> ImportMT940Async(List<MT940Umsatz> umsaetze, int kModul, int kBenutzer)
        {
            var result = new ImportResult();

            foreach (var u in umsaetze)
            {
                // Nur Eingaenge (Gutschriften)
                if (u.Betrag <= 0) continue;

                var umsatz = new ZahlungsabgleichUmsatz
                {
                    KZahlungsabgleichModul = kModul,
                    CKontoIdentifikation = u.KontoNr,
                    CTransaktionID = u.Referenz ?? $"{u.Buchungsdatum:yyyyMMdd}_{u.Betrag:0.00}_{u.Name?.GetHashCode()}",
                    DBuchungsdatum = u.Buchungsdatum,
                    FBetrag = u.Betrag,
                    CWaehrungISO = "EUR",
                    CName = u.Name,
                    CKonto = u.IBAN,
                    CVerwendungszweck = u.Verwendungszweck
                };

                var id = await _db.ImportUmsatzAsync(umsatz);
                if (id > 0)
                    result.Importiert++;
                else
                    result.Uebersprungen++;
            }

            await _logService.LogAsync("Zahlungsabgleich", "MT940 Import", "Bank",
                beschreibung: $"MT940-Import: {result.Importiert} importiert, {result.Uebersprungen} uebersprungen",
                kBenutzer: kBenutzer);

            _log.Information("MT940-Import: {Importiert} importiert, {Uebersprungen} uebersprungen",
                result.Importiert, result.Uebersprungen);

            return result;
        }

        #endregion

        #region Statistik

        public async Task<ZahlungsabgleichStats> GetStatsAsync()
        {
            return await _db.GetZahlungsabgleichStatsAsync();
        }

        public async Task<List<ZahlungsabgleichUmsatz>> GetOffeneTransaktionenAsync(int? kModul = null)
        {
            return await _db.GetOffeneUmsaetzeAsync(kModul);
        }

        public async Task<List<ZahlungsabgleichUmsatzMitZuordnung>> GetGematchteTransaktionenAsync(DateTime? von = null, DateTime? bis = null)
        {
            return await _db.GetGematchteUmsaetzeAsync(von, bis);
        }

        public async Task<List<OffeneRechnungInfo>> GetOffeneRechnungenAsync()
        {
            return await _db.GetOffeneRechnungenFuerMatchingAsync();
        }

        public async Task<List<ZahlungsabgleichModul>> GetModuleAsync()
        {
            return await _db.GetZahlungsabgleichModuleAsync();
        }

        #endregion

        #region View-Kompatible Methoden

        /// <summary>
        /// Holt alle Transaktionen (kompatibel mit ZahlungsabgleichView)
        /// </summary>
        public async Task<List<ZahlungsabgleichEintrag>> GetAllTransaktionenAsync(
            DateTime? von = null, DateTime? bis = null, bool nurUnmatched = false)
        {
            var offene = await _db.GetOffeneUmsaetzeAsync();
            var gematchte = !nurUnmatched ? await _db.GetGematchteUmsaetzeAsync(von, bis) : new List<ZahlungsabgleichUmsatzMitZuordnung>();

            var result = new List<ZahlungsabgleichEintrag>();

            // Offene Transaktionen
            foreach (var u in offene)
            {
                if (von.HasValue && u.DBuchungsdatum < von) continue;
                if (bis.HasValue && u.DBuchungsdatum > bis) continue;

                result.Add(new ZahlungsabgleichEintrag
                {
                    Id = u.KZahlungsabgleichUmsatz,
                    TransaktionsId = u.CTransaktionID ?? "",
                    Buchungsdatum = u.DBuchungsdatum ?? DateTime.Now,
                    Betrag = u.FBetrag,
                    Waehrung = u.CWaehrungISO,
                    Name = u.CName,
                    Konto = u.CKonto,
                    Verwendungszweck = u.CVerwendungszweck,
                    Status = u.NStatus,
                    MatchKonfidenz = 0
                });
            }

            // Gematchte Transaktionen
            foreach (var g in gematchte)
            {
                result.Add(new ZahlungsabgleichEintrag
                {
                    Id = g.KZahlungsabgleichUmsatz,
                    TransaktionsId = "",
                    Buchungsdatum = g.DBuchungsdatum ?? DateTime.Now,
                    Betrag = g.FBetrag,
                    Waehrung = g.CWaehrungISO,
                    Name = g.CName,
                    Verwendungszweck = g.CVerwendungszweck,
                    Status = g.NStatus,
                    MatchKonfidenz = g.NZuweisungswertung,
                    ZugeordneteRechnungNr = g.RechnungsNr,
                    ZugeordneteAuftragNr = g.BestellNr
                });
            }

            return result.OrderByDescending(r => r.Buchungsdatum).ToList();
        }

        /// <summary>
        /// Sucht offene Rechnungen (kompatibel mit ZahlungZuordnenDialog)
        /// </summary>
        public async Task<List<OffeneRechnung>> SucheOffeneRechnungenAsync(string? suchbegriff = null)
        {
            var rechnungen = await _db.GetOffeneRechnungenFuerMatchingAsync();

            if (!string.IsNullOrEmpty(suchbegriff))
            {
                suchbegriff = suchbegriff.ToUpperInvariant();
                rechnungen = rechnungen.Where(r =>
                    (r.RechnungsNr?.ToUpperInvariant().Contains(suchbegriff) == true) ||
                    (r.KundenNr?.ToUpperInvariant().Contains(suchbegriff) == true) ||
                    (r.KundeFirma?.ToUpperInvariant().Contains(suchbegriff) == true) ||
                    (r.KundeNachname?.ToUpperInvariant().Contains(suchbegriff) == true)
                ).ToList();
            }

            return rechnungen.Select(r => new OffeneRechnung
            {
                KRechnung = r.KRechnung,
                CRechnungsnummer = r.RechnungsNr,
                Brutto = r.Brutto,
                Offen = r.OffenerBetrag,
                Faelligkeit = r.DFaellig,
                KKunde = r.KKunde,
                CKundenNr = r.KundenNr ?? "",
                KundeDisplay = r.KundeFirma ?? r.KundeNachname ?? "",
                CIBAN = r.KundeIBAN
            }).ToList();
        }

        /// <summary>
        /// Ordnet Zahlung einer Rechnung zu (kompatibel mit ZahlungZuordnenDialog)
        /// </summary>
        public async Task ZuordnenAsync(int zahlungId, int kRechnung, decimal betrag)
        {
            await _db.ZuordnenZuRechnungAsync(zahlungId, kRechnung, betrag, 1, 100);
            await _logService.LogAsync("Zahlungsabgleich", "Manuell Zuordnen", "Zahlungsabgleich",
                entityTyp: "Rechnung", kEntity: kRechnung,
                beschreibung: $"Manuelle Zuordnung: {betrag:N2} EUR",
                betragBrutto: betrag);
        }

        /// <summary>
        /// Fuehrt Auto-Matching durch (kompatibel mit View)
        /// </summary>
        public async Task<MatchResult> MatchZahlungenAsync()
        {
            var result = await AutoMatchAsync(1, false);
            return new MatchResult
            {
                AutoGematchedAnzahl = result.AutoGematcht,
                VorschlaegeAnzahl = result.Vorschlaege.Count,
                GesamtAnzahl = result.Vorschlaege.Count + result.AutoGematcht
            };
        }

        /// <summary>
        /// Importiert MT940-Datei
        /// </summary>
        public async Task<FileImportResult> ImportMT940Async(string filePath)
        {
            try
            {
                if (!System.IO.File.Exists(filePath))
                    return new FileImportResult { Erfolg = false, Fehler = "Datei nicht gefunden" };

                var content = await System.IO.File.ReadAllTextAsync(filePath);
                var umsaetze = ParseMT940(content);

                // Modul ID (Bank = 1)
                var kModul = await GetOrCreateModulAsync("MT940");

                var result = await ImportMT940Async(umsaetze, kModul, 1);

                return new FileImportResult
                {
                    Erfolg = true,
                    ImportiertAnzahl = result.Importiert,
                    UebersprungAnzahl = result.Uebersprungen
                };
            }
            catch (Exception ex)
            {
                return new FileImportResult { Erfolg = false, Fehler = ex.Message };
            }
        }

        /// <summary>
        /// Importiert CAMT-Datei
        /// </summary>
        public async Task<FileImportResult> ImportCAMTAsync(string filePath)
        {
            try
            {
                if (!System.IO.File.Exists(filePath))
                    return new FileImportResult { Erfolg = false, Fehler = "Datei nicht gefunden" };

                var content = await System.IO.File.ReadAllTextAsync(filePath);
                var umsaetze = ParseCAMT(content);

                var kModul = await GetOrCreateModulAsync("CAMT");

                var result = await ImportMT940Async(umsaetze, kModul, 1);

                return new FileImportResult
                {
                    Erfolg = true,
                    ImportiertAnzahl = result.Importiert,
                    UebersprungAnzahl = result.Uebersprungen
                };
            }
            catch (Exception ex)
            {
                return new FileImportResult { Erfolg = false, Fehler = ex.Message };
            }
        }

        private async Task<int> GetOrCreateModulAsync(string modulId)
        {
            return await _db.SaveZahlungsabgleichModulAsync(modulId);
        }

        private List<MT940Umsatz> ParseMT940(string content)
        {
            var result = new List<MT940Umsatz>();
            // Einfacher MT940-Parser
            var lines = content.Split('\n');
            MT940Umsatz? current = null;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith(":61:"))
                {
                    // Umsatzzeile
                    current = new MT940Umsatz();
                    // Format: :61:YYMMDDYYMMDDDC123,45Nxxx
                    var match = Regex.Match(trimmed, @":61:(\d{6})\d{6}(C|D)([\d,\.]+)");
                    if (match.Success)
                    {
                        var dateStr = match.Groups[1].Value;
                        current.Buchungsdatum = DateTime.ParseExact("20" + dateStr, "yyyyMMdd", null);
                        var vorzeichen = match.Groups[2].Value == "C" ? 1 : -1;
                        current.Betrag = decimal.Parse(match.Groups[3].Value.Replace(",", "."),
                            System.Globalization.CultureInfo.InvariantCulture) * vorzeichen;
                    }
                }
                else if (trimmed.StartsWith(":86:") && current != null)
                {
                    // Verwendungszweck
                    var vzweck = trimmed[4..];
                    var nameMatch = Regex.Match(vzweck, @"(?:32|33):([^+]+)");
                    if (nameMatch.Success) current.Name = nameMatch.Groups[1].Value;

                    var ibanMatch = Regex.Match(vzweck, @"(?:31|30):([A-Z]{2}[0-9]{2}[A-Z0-9]+)");
                    if (ibanMatch.Success) current.IBAN = ibanMatch.Groups[1].Value;

                    current.Verwendungszweck = Regex.Replace(vzweck, @"\+\d{2}:", " ").Trim();

                    if (current.Betrag != 0)
                        result.Add(current);
                    current = null;
                }
            }

            return result;
        }

        private List<MT940Umsatz> ParseCAMT(string xmlContent)
        {
            var result = new List<MT940Umsatz>();
            try
            {
                var doc = System.Xml.Linq.XDocument.Parse(xmlContent);
                var ns = doc.Root?.Name.Namespace ?? "";

                var entries = doc.Descendants(ns + "Ntry");
                foreach (var entry in entries)
                {
                    var amt = entry.Element(ns + "Amt")?.Value;
                    var cdtDbtInd = entry.Element(ns + "CdtDbtInd")?.Value;
                    var bookgDt = entry.Element(ns + "BookgDt")?.Element(ns + "Dt")?.Value;

                    var txDetails = entry.Descendants(ns + "TxDtls").FirstOrDefault();
                    var rmtInf = txDetails?.Element(ns + "RmtInf")?.Element(ns + "Ustrd")?.Value;
                    var dbtrNm = txDetails?.Descendants(ns + "Dbtr").FirstOrDefault()?.Element(ns + "Nm")?.Value;
                    var iban = txDetails?.Descendants(ns + "DbtrAcct").FirstOrDefault()?.Element(ns + "Id")?.Element(ns + "IBAN")?.Value;

                    if (!string.IsNullOrEmpty(amt) && decimal.TryParse(amt, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var betrag))
                    {
                        if (cdtDbtInd == "DBIT") betrag = -betrag;

                        result.Add(new MT940Umsatz
                        {
                            Buchungsdatum = DateTime.TryParse(bookgDt, out var dt) ? dt : DateTime.Today,
                            Betrag = betrag,
                            Name = dbtrNm,
                            IBAN = iban,
                            Verwendungszweck = rmtInf
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Fehler beim Parsen der CAMT-Datei");
            }
            return result;
        }

        #endregion

        #region DTOs

        // Typen fuer View-Kompatibilitaet
        public class ZahlungsabgleichEintrag
        {
            public int Id { get; set; }
            public string TransaktionsId { get; set; } = "";
            public DateTime Buchungsdatum { get; set; }
            public decimal Betrag { get; set; }
            public string Waehrung { get; set; } = "EUR";
            public string? Name { get; set; }
            public string? Konto { get; set; }
            public string? Verwendungszweck { get; set; }
            public int Status { get; set; }
            public int MatchKonfidenz { get; set; }
            public string? ZugeordneteRechnungNr { get; set; }
            public string? ZugeordneteAuftragNr { get; set; }
        }

        public class OffeneRechnung
        {
            public int KRechnung { get; set; }
            public string? CRechnungsnummer { get; set; }
            public decimal Brutto { get; set; }
            public decimal Offen { get; set; }
            public DateTime? Faelligkeit { get; set; }
            public int KKunde { get; set; }
            public string CKundenNr { get; set; } = "";
            public string KundeDisplay { get; set; } = "";
            public string? CIBAN { get; set; }
        }

        public class MatchResult
        {
            public int AutoGematchedAnzahl { get; set; }
            public int VorschlaegeAnzahl { get; set; }
            public int GesamtAnzahl { get; set; }
        }

        public class FileImportResult
        {
            public bool Erfolg { get; set; }
            public int ImportiertAnzahl { get; set; }
            public int UebersprungAnzahl { get; set; }
            public string? Fehler { get; set; }
        }

        public class AutoMatchResult
        {
            public int AutoGematcht { get; set; }
            public int SchwelleVerwendet { get; set; }
            public List<MatchVorschlag> Vorschlaege { get; set; } = new();
        }

        public class MatchVorschlag
        {
            public int KZahlungsabgleichUmsatz { get; set; }
            public decimal Betrag { get; set; }
            public string? ZahlerName { get; set; }
            public string? Verwendungszweck { get; set; }
            public int? KRechnung { get; set; }
            public string? RechnungsNr { get; set; }
            public int? KAuftrag { get; set; }
            public string? AuftragNr { get; set; }
            public int KKunde { get; set; }
            public string? KundeName { get; set; }
            public decimal OffenerBetrag { get; set; }
            public int Konfidenz { get; set; } // 0-100
            public List<string> MatchGruende { get; set; } = new();
        }

        public class ImportResult
        {
            public int Importiert { get; set; }
            public int Uebersprungen { get; set; }
            public int Fehler { get; set; }
        }

        public class PayPalTransaktion
        {
            public string TransaktionId { get; set; } = "";
            public DateTime Datum { get; set; }
            public decimal Betrag { get; set; }
            public string? Waehrung { get; set; }
            public string? ZahlerName { get; set; }
            public string? ZahlerEmail { get; set; }
            public string? Beschreibung { get; set; }
        }

        public class MollieTransaktion
        {
            public string Id { get; set; } = "";
            public DateTime CreatedAt { get; set; }
            public DateTime? PaidAt { get; set; }
            public decimal Betrag { get; set; }
            public string? Waehrung { get; set; }
            public string? Status { get; set; }
            public string? Beschreibung { get; set; }
        }

        public class MT940Umsatz
        {
            public string? KontoNr { get; set; }
            public DateTime Buchungsdatum { get; set; }
            public decimal Betrag { get; set; }
            public string? Name { get; set; }
            public string? IBAN { get; set; }
            public string? BIC { get; set; }
            public string? Verwendungszweck { get; set; }
            public string? Referenz { get; set; }
        }

        #endregion
    }
}

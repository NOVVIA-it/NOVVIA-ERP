using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using NovviaERP.Core.Data;

namespace NovviaERP.Core.Services
{
    /// <summary>
    /// Service für NOVVIA Audit-Log System
    /// </summary>
    public class LogService
    {
        private readonly JtlDbContext _db;

        public LogService(JtlDbContext db) => _db = db;

        #region Log schreiben

        /// <summary>
        /// Allgemeiner Log-Eintrag
        /// </summary>
        public async Task<long> LogAsync(
            string kategorie,
            string aktion,
            string modul,
            string? entityTyp = null,
            int? kEntity = null,
            string? entityNr = null,
            string? feldname = null,
            string? alterWert = null,
            string? neuerWert = null,
            string? beschreibung = null,
            string? details = null,
            decimal? betragNetto = null,
            decimal? betragBrutto = null,
            int? kBenutzer = null,
            string? benutzerName = null,
            int severity = 0)
        {
            var conn = await _db.GetConnectionAsync();
            var result = await conn.QuerySingleOrDefaultAsync<long?>(
                "EXEC NOVVIA.spLogSchreiben @cKategorie, @cAktion, @cModul, @cEntityTyp, @kEntity, @cEntityNr, " +
                "@cFeldname, @cAlterWert, @cNeuerWert, @cBeschreibung, @cDetails, " +
                "@fBetragNetto, @fBetragBrutto, @kBenutzer, @cBenutzerName, @cRechnername, @cIP, @nSeverity",
                new
                {
                    cKategorie = kategorie,
                    cAktion = aktion,
                    cModul = modul,
                    cEntityTyp = entityTyp,
                    kEntity = kEntity,
                    cEntityNr = entityNr,
                    cFeldname = feldname,
                    cAlterWert = alterWert,
                    cNeuerWert = neuerWert,
                    cBeschreibung = beschreibung,
                    cDetails = details,
                    fBetragNetto = betragNetto,
                    fBetragBrutto = betragBrutto,
                    kBenutzer = kBenutzer,
                    cBenutzerName = benutzerName,
                    cRechnername = Environment.MachineName,
                    cIP = (string?)null,
                    nSeverity = severity
                });
            return result ?? 0;
        }

        /// <summary>
        /// Stammdaten-Änderung loggen (Kunde, Artikel, Lieferant)
        /// </summary>
        public async Task LogStammdatenAsync(
            string modul,
            string aktion,
            string entityTyp,
            int kEntity,
            string? entityNr = null,
            string? feldname = null,
            string? alterWert = null,
            string? neuerWert = null,
            string? beschreibung = null,
            int? kBenutzer = null,
            string? benutzerName = null)
        {
            await LogAsync("Stammdaten", aktion, modul, entityTyp, kEntity, entityNr,
                feldname, alterWert, neuerWert, beschreibung, null, null, null,
                kBenutzer, benutzerName, 0);
        }

        /// <summary>
        /// Bewegungsdaten loggen (Auftrag, Rechnung, Lieferschein)
        /// </summary>
        public async Task LogBewegungAsync(
            string modul,
            string aktion,
            string entityTyp,
            int kEntity,
            string? entityNr = null,
            string? beschreibung = null,
            decimal? betragNetto = null,
            decimal? betragBrutto = null,
            int? kBenutzer = null,
            string? benutzerName = null)
        {
            await LogAsync("Bewegung", aktion, modul, entityTyp, kEntity, entityNr,
                null, null, null, beschreibung, null, betragNetto, betragBrutto,
                kBenutzer, benutzerName, 0);
        }

        /// <summary>
        /// System-Event loggen (Login, Fehler, etc.)
        /// </summary>
        public async Task LogSystemAsync(
            string aktion,
            string modul,
            string? beschreibung = null,
            string? details = null,
            int? kBenutzer = null,
            string? benutzerName = null,
            int severity = 0)
        {
            await LogAsync("System", aktion, modul, null, null, null,
                null, null, null, beschreibung, details, null, null,
                kBenutzer, benutzerName, severity);
        }

        /// <summary>
        /// Fehler loggen
        /// </summary>
        public async Task LogFehlerAsync(
            string modul,
            string beschreibung,
            string? details = null,
            int? kBenutzer = null,
            string? benutzerName = null)
        {
            await LogSystemAsync("Fehler", modul, beschreibung, details, kBenutzer, benutzerName, 2);
        }

        /// <summary>
        /// Warnung loggen
        /// </summary>
        public async Task LogWarnungAsync(
            string modul,
            string beschreibung,
            string? details = null,
            int? kBenutzer = null,
            string? benutzerName = null)
        {
            await LogSystemAsync("Warnung", modul, beschreibung, details, kBenutzer, benutzerName, 1);
        }

        #endregion

        #region Log abfragen

        /// <summary>
        /// Log-Einträge abfragen
        /// </summary>
        public async Task<IEnumerable<LogEintrag>> GetLogsAsync(
            string? kategorie = null,
            string? modul = null,
            string? entityTyp = null,
            int? kEntity = null,
            int? kBenutzer = null,
            DateTime? von = null,
            DateTime? bis = null,
            int? minSeverity = null,
            int top = 1000)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QueryAsync<LogEintrag>(
                "EXEC NOVVIA.spLogAbfragen @cKategorie, @cModul, @cEntityTyp, @kEntity, @kBenutzer, @dVon, @dBis, @nSeverity, @nTop",
                new
                {
                    cKategorie = kategorie,
                    cModul = modul,
                    cEntityTyp = entityTyp,
                    kEntity = kEntity,
                    kBenutzer = kBenutzer,
                    dVon = von,
                    dBis = bis,
                    nSeverity = minSeverity,
                    nTop = top
                });
        }

        /// <summary>
        /// Log-Einträge für eine bestimmte Entität
        /// </summary>
        public async Task<IEnumerable<LogEintrag>> GetEntityLogsAsync(string entityTyp, int kEntity, int top = 100)
        {
            return await GetLogsAsync(entityTyp: entityTyp, kEntity: kEntity, top: top);
        }

        /// <summary>
        /// Fehler-Logs abfragen
        /// </summary>
        public async Task<IEnumerable<LogEintrag>> GetFehlerLogsAsync(DateTime? von = null, int top = 100)
        {
            return await GetLogsAsync(minSeverity: 2, von: von, top: top);
        }

        /// <summary>
        /// Warnungen und Fehler abfragen
        /// </summary>
        public async Task<IEnumerable<LogEintrag>> GetWarnungenUndFehlerAsync(DateTime? von = null, int top = 100)
        {
            return await GetLogsAsync(minSeverity: 1, von: von, top: top);
        }

        /// <summary>
        /// Benutzer-Aktivitäten abfragen
        /// </summary>
        public async Task<IEnumerable<LogEintrag>> GetBenutzerAktivitaetenAsync(int kBenutzer, DateTime? von = null, int top = 100)
        {
            return await GetLogsAsync(kBenutzer: kBenutzer, von: von, top: top);
        }

        #endregion

        #region Shortcuts für häufige Aktionen

        public Task LogKundeErstelltAsync(int kKunde, string kundenNr, string? benutzerName = null)
            => LogStammdatenAsync("Kunde", "Erstellt", "tKunde", kKunde, kundenNr, beschreibung: $"Kunde {kundenNr} erstellt", benutzerName: benutzerName);

        public Task LogKundeGeaendertAsync(int kKunde, string kundenNr, string feld, string? alterWert, string? neuerWert, string? benutzerName = null)
            => LogStammdatenAsync("Kunde", "Geaendert", "tKunde", kKunde, kundenNr, feld, alterWert, neuerWert, benutzerName: benutzerName);

        public Task LogArtikelErstelltAsync(int kArtikel, string artikelNr, string? benutzerName = null)
            => LogStammdatenAsync("Artikel", "Erstellt", "tArtikel", kArtikel, artikelNr, beschreibung: $"Artikel {artikelNr} erstellt", benutzerName: benutzerName);

        public Task LogArtikelGeaendertAsync(int kArtikel, string artikelNr, string feld, string? alterWert, string? neuerWert, string? benutzerName = null)
            => LogStammdatenAsync("Artikel", "Geaendert", "tArtikel", kArtikel, artikelNr, feld, alterWert, neuerWert, benutzerName: benutzerName);

        public Task LogAuftragErstelltAsync(int kBestellung, string bestellNr, decimal? netto = null, decimal? brutto = null, string? benutzerName = null)
            => LogBewegungAsync("Auftrag", "Erstellt", "tBestellung", kBestellung, bestellNr, $"Auftrag {bestellNr} erstellt", netto, brutto, benutzerName: benutzerName);

        public Task LogRechnungErstelltAsync(int kRechnung, string rechnungsNr, decimal? netto = null, decimal? brutto = null, string? benutzerName = null)
            => LogBewegungAsync("Rechnung", "Erstellt", "tRechnung", kRechnung, rechnungsNr, $"Rechnung {rechnungsNr} erstellt", netto, brutto, benutzerName: benutzerName);

        public Task LogLieferscheinErstelltAsync(int kLieferschein, string lieferscheinNr, string? benutzerName = null)
            => LogBewegungAsync("Lieferschein", "Erstellt", "tLieferschein", kLieferschein, lieferscheinNr, $"Lieferschein {lieferscheinNr} erstellt", benutzerName: benutzerName);

        public Task LogLoginAsync(int kBenutzer, string benutzerName)
            => LogSystemAsync("Login", "Benutzer", $"Benutzer {benutzerName} angemeldet", kBenutzer: kBenutzer, benutzerName: benutzerName);

        public Task LogLogoutAsync(int kBenutzer, string benutzerName)
            => LogSystemAsync("Logout", "Benutzer", $"Benutzer {benutzerName} abgemeldet", kBenutzer: kBenutzer, benutzerName: benutzerName);

        #endregion
    }

    /// <summary>
    /// Log-Eintrag Entity
    /// </summary>
    public class LogEintrag
    {
        public long kLog { get; set; }
        public string cKategorie { get; set; } = "";
        public string cAktion { get; set; } = "";
        public string cModul { get; set; } = "";
        public string? cEntityTyp { get; set; }
        public int? kEntity { get; set; }
        public string? cEntityNr { get; set; }
        public string? cFeldname { get; set; }
        public string? cAlterWert { get; set; }
        public string? cNeuerWert { get; set; }
        public string? cBeschreibung { get; set; }
        public decimal? fBetragNetto { get; set; }
        public decimal? fBetragBrutto { get; set; }
        public string? cBenutzerName { get; set; }
        public DateTime dZeitpunkt { get; set; }
        public int nSeverity { get; set; }
    }
}

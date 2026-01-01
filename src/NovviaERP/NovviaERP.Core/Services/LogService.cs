using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using NovviaERP.Core.Data;
using Dapper;

namespace NovviaERP.Core.Services
{
    /// <summary>
    /// Service fuer NOVVIA.Log - zentrales Logging fuer alle Module
    /// Kategorien: Shop, Zahlungsabgleich, Stammdaten, Bewegungsdaten, System
    /// </summary>
    public class LogService
    {
        private readonly JtlDbContext _db;
        private static string? _rechnername;
        private static string? _ip;

        public LogService(JtlDbContext db)
        {
            _db = db;
            if (_rechnername == null)
            {
                try
                {
                    _rechnername = Environment.MachineName;
                    _ip = Dns.GetHostEntry(Dns.GetHostName()).AddressList
                        .FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.ToString();
                }
                catch { _rechnername = "Unknown"; }
            }
        }

        #region Log schreiben

        /// <summary>
        /// Schreibt einen Eintrag in NOVVIA.Log
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
            return await conn.QuerySingleAsync<long>(@"
                INSERT INTO NOVVIA.[Log] (
                    cKategorie, cAktion, cModul, cEntityTyp, kEntity, cEntityNr,
                    cFeldname, cAlterWert, cNeuerWert, cBeschreibung, cDetails,
                    fBetragNetto, fBetragBrutto,
                    kBenutzer, cBenutzerName, cRechnername, cIP, dZeitpunkt, nSeverity
                ) VALUES (
                    @kategorie, @aktion, @modul, @entityTyp, @kEntity, @entityNr,
                    @feldname, @alterWert, @neuerWert, @beschreibung, @details,
                    @betragNetto, @betragBrutto,
                    @kBenutzer, @benutzerName, @rechnername, @ip, GETDATE(), @severity
                );
                SELECT SCOPE_IDENTITY();",
                new {
                    kategorie, aktion, modul, entityTyp, kEntity, entityNr,
                    feldname, alterWert, neuerWert, beschreibung, details,
                    betragNetto, betragBrutto,
                    kBenutzer, benutzerName,
                    rechnername = _rechnername, ip = _ip, severity
                });
        }

        /// <summary>
        /// Loggt eine Stammdaten-Aenderung (Artikel, Kunde, etc.)
        /// </summary>
        public async Task LogStammdatenAsync(
            string entityTyp,
            int kEntity,
            string? entityNr,
            string aktion,
            string? feldname = null,
            string? alterWert = null,
            string? neuerWert = null,
            string? beschreibung = null,
            int? kBenutzer = null)
        {
            await LogAsync(
                kategorie: "Stammdaten",
                aktion: aktion,
                modul: entityTyp,
                entityTyp: entityTyp,
                kEntity: kEntity,
                entityNr: entityNr,
                feldname: feldname,
                alterWert: alterWert,
                neuerWert: neuerWert,
                beschreibung: beschreibung,
                kBenutzer: kBenutzer);
        }

        /// <summary>
        /// Loggt eine Bewegungsdaten-Aenderung (Bestellung, Rechnung, Zahlung, etc.)
        /// </summary>
        public async Task LogBewegungsdatenAsync(
            string entityTyp,
            int kEntity,
            string? entityNr,
            string aktion,
            string? beschreibung = null,
            string? details = null,
            decimal? betragNetto = null,
            decimal? betragBrutto = null,
            int? kBenutzer = null)
        {
            await LogAsync(
                kategorie: "Bewegungsdaten",
                aktion: aktion,
                modul: entityTyp,
                entityTyp: entityTyp,
                kEntity: kEntity,
                entityNr: entityNr,
                beschreibung: beschreibung,
                details: details,
                betragNetto: betragNetto,
                betragBrutto: betragBrutto,
                kBenutzer: kBenutzer);
        }

        /// <summary>
        /// Loggt eine Shop-Sync-Aktion
        /// </summary>
        public async Task LogShopAsync(
            int kShop,
            string shopName,
            string aktion,
            string? entityTyp = null,
            int? kEntity = null,
            string? entityNr = null,
            string? beschreibung = null,
            string? details = null,
            decimal? betragBrutto = null,
            int? kBenutzer = null,
            int severity = 0)
        {
            await LogAsync(
                kategorie: "Shop",
                aktion: aktion,
                modul: shopName,
                entityTyp: entityTyp,
                kEntity: kEntity ?? kShop,
                entityNr: entityNr,
                beschreibung: beschreibung,
                details: details,
                betragBrutto: betragBrutto,
                kBenutzer: kBenutzer,
                severity: severity);
        }

        /// <summary>
        /// Loggt einen Fehler
        /// </summary>
        public async Task LogErrorAsync(
            string modul,
            string aktion,
            string fehler,
            string? details = null,
            int? kBenutzer = null)
        {
            await LogAsync(
                kategorie: "Fehler",
                aktion: aktion,
                modul: modul,
                beschreibung: fehler,
                details: details,
                kBenutzer: kBenutzer,
                severity: 3);
        }

        #endregion

        #region Log lesen

        /// <summary>
        /// Holt Log-Eintraege mit Filtern
        /// </summary>
        public async Task<List<LogEintrag>> GetLogsAsync(LogFilter? filter = null, int limit = 500)
        {
            filter ??= new LogFilter();
            var conn = await _db.GetConnectionAsync();

            var where = "WHERE 1=1";
            if (!string.IsNullOrEmpty(filter.Kategorie))
                where += " AND cKategorie = @Kategorie";
            if (!string.IsNullOrEmpty(filter.Modul))
                where += " AND cModul = @Modul";
            if (!string.IsNullOrEmpty(filter.EntityTyp))
                where += " AND cEntityTyp = @EntityTyp";
            if (filter.KEntity.HasValue)
                where += " AND kEntity = @KEntity";
            if (!string.IsNullOrEmpty(filter.Suche))
                where += " AND (cBeschreibung LIKE @Suche OR cEntityNr LIKE @Suche OR cAktion LIKE @Suche)";
            if (filter.Von.HasValue)
                where += " AND dZeitpunkt >= @Von";
            if (filter.Bis.HasValue)
                where += " AND dZeitpunkt <= @Bis";
            if (filter.KBenutzer.HasValue)
                where += " AND kBenutzer = @KBenutzer";
            if (filter.MinSeverity.HasValue)
                where += " AND nSeverity >= @MinSeverity";

            return (await conn.QueryAsync<LogEintrag>($@"
                SELECT TOP (@limit)
                    kLog, cKategorie, cAktion, cModul, cEntityTyp, kEntity, cEntityNr,
                    cFeldname, cAlterWert, cNeuerWert, cBeschreibung, cDetails,
                    fBetragNetto, fBetragBrutto,
                    kBenutzer, cBenutzerName, cRechnername, cIP, dZeitpunkt, nSeverity
                FROM NOVVIA.[Log]
                {where}
                ORDER BY dZeitpunkt DESC",
                new {
                    limit,
                    filter.Kategorie,
                    filter.Modul,
                    filter.EntityTyp,
                    filter.KEntity,
                    Suche = $"%{filter.Suche}%",
                    filter.Von,
                    filter.Bis,
                    filter.KBenutzer,
                    filter.MinSeverity
                })).ToList();
        }

        /// <summary>
        /// Holt Kategorien fuer Filter-Dropdown
        /// </summary>
        public async Task<List<string>> GetKategorienAsync()
        {
            var conn = await _db.GetConnectionAsync();
            return (await conn.QueryAsync<string>(
                "SELECT DISTINCT cKategorie FROM NOVVIA.[Log] WHERE cKategorie IS NOT NULL ORDER BY cKategorie")).ToList();
        }

        /// <summary>
        /// Holt Module fuer Filter-Dropdown
        /// </summary>
        public async Task<List<string>> GetModuleAsync(string? kategorie = null)
        {
            var conn = await _db.GetConnectionAsync();
            var where = kategorie != null ? "WHERE cKategorie = @kategorie" : "";
            return (await conn.QueryAsync<string>($@"
                SELECT DISTINCT cModul FROM NOVVIA.[Log] {where} AND cModul IS NOT NULL ORDER BY cModul",
                new { kategorie })).ToList();
        }

        /// <summary>
        /// Holt Log-Statistik
        /// </summary>
        public async Task<LogStats> GetStatsAsync(int? tage = 7)
        {
            var conn = await _db.GetConnectionAsync();
            var seit = DateTime.Now.AddDays(-(tage ?? 7));
            return await conn.QuerySingleAsync<LogStats>(@"
                SELECT
                    (SELECT COUNT(*) FROM NOVVIA.[Log] WHERE dZeitpunkt >= @seit) AS Gesamt,
                    (SELECT COUNT(*) FROM NOVVIA.[Log] WHERE dZeitpunkt >= @seit AND cKategorie = 'Shop') AS Shop,
                    (SELECT COUNT(*) FROM NOVVIA.[Log] WHERE dZeitpunkt >= @seit AND cKategorie = 'Zahlungsabgleich') AS Zahlungsabgleich,
                    (SELECT COUNT(*) FROM NOVVIA.[Log] WHERE dZeitpunkt >= @seit AND cKategorie = 'Stammdaten') AS Stammdaten,
                    (SELECT COUNT(*) FROM NOVVIA.[Log] WHERE dZeitpunkt >= @seit AND cKategorie = 'Bewegungsdaten') AS Bewegungsdaten,
                    (SELECT COUNT(*) FROM NOVVIA.[Log] WHERE dZeitpunkt >= @seit AND nSeverity >= 3) AS Fehler",
                new { seit });
        }

        /// <summary>
        /// Loescht alte Log-Eintraege
        /// </summary>
        public async Task<int> BereinigeLogsAsync(int tageAlt = 90)
        {
            var conn = await _db.GetConnectionAsync();
            var bis = DateTime.Now.AddDays(-tageAlt);
            return await conn.ExecuteAsync(
                "DELETE FROM NOVVIA.[Log] WHERE dZeitpunkt < @bis AND nVerarbeitet = 1",
                new { bis });
        }

        #endregion

        #region DTOs

        public class LogEintrag
        {
            public long KLog { get; set; }
            public string? CKategorie { get; set; }
            public string? CAktion { get; set; }
            public string? CModul { get; set; }
            public string? CEntityTyp { get; set; }
            public int? KEntity { get; set; }
            public string? CEntityNr { get; set; }
            public string? CFeldname { get; set; }
            public string? CAlterWert { get; set; }
            public string? CNeuerWert { get; set; }
            public string? CBeschreibung { get; set; }
            public string? CDetails { get; set; }
            public decimal? FBetragNetto { get; set; }
            public decimal? FBetragBrutto { get; set; }
            public int? KBenutzer { get; set; }
            public string? CBenutzerName { get; set; }
            public string? CRechnername { get; set; }
            public string? CIP { get; set; }
            public DateTime DZeitpunkt { get; set; }
            public int NSeverity { get; set; }

            // UI-Helfer
            public string KategorieAnzeige => CKategorie ?? "-";
            public string ZeitpunktAnzeige => DZeitpunkt.ToString("dd.MM.yyyy HH:mm:ss");
            public string SeverityIcon => NSeverity switch
            {
                0 => "Info",
                1 => "Warning",
                2 => "Error",
                3 => "Critical",
                _ => "Info"
            };
        }

        public class LogFilter
        {
            public string? Kategorie { get; set; }
            public string? Modul { get; set; }
            public string? EntityTyp { get; set; }
            public int? KEntity { get; set; }
            public string? Suche { get; set; }
            public DateTime? Von { get; set; }
            public DateTime? Bis { get; set; }
            public int? KBenutzer { get; set; }
            public int? MinSeverity { get; set; }
        }

        public class LogStats
        {
            public int Gesamt { get; set; }
            public int Shop { get; set; }
            public int Zahlungsabgleich { get; set; }
            public int Stammdaten { get; set; }
            public int Bewegungsdaten { get; set; }
            public int Fehler { get; set; }
        }

        #endregion
    }
}

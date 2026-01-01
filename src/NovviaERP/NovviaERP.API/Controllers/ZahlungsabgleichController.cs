using Microsoft.AspNetCore.Mvc;
using NovviaERP.Core.Data;
using NovviaERP.Core.Services;
using static NovviaERP.Core.Data.JtlDbContext;
using static NovviaERP.Core.Services.ZahlungsabgleichService;

namespace NovviaERP.API.Controllers
{
    /// <summary>
    /// API Controller fuer JTL-nativen Zahlungsabgleich
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ZahlungsabgleichController : ControllerBase
    {
        private readonly JtlDbContext _db;
        private readonly ZahlungsabgleichService _service;
        private readonly LogService _logService;

        public ZahlungsabgleichController(JtlDbContext db)
        {
            _db = db;
            _logService = new LogService(db);
            _service = new ZahlungsabgleichService(db, _logService);
        }

        #region Statistik & Uebersicht

        /// <summary>
        /// Holt Zahlungsabgleich-Statistik
        /// </summary>
        [HttpGet("stats")]
        public async Task<ActionResult<ZahlungsabgleichStats>> GetStats()
        {
            try
            {
                return Ok(await _service.GetStatsAsync());
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Holt offene (nicht zugeordnete) Transaktionen
        /// </summary>
        [HttpGet("offen")]
        public async Task<ActionResult<List<ZahlungsabgleichUmsatz>>> GetOffene([FromQuery] int? kModul = null)
        {
            try
            {
                return Ok(await _service.GetOffeneTransaktionenAsync(kModul));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Holt bereits zugeordnete Transaktionen
        /// </summary>
        [HttpGet("gematcht")]
        public async Task<ActionResult<List<ZahlungsabgleichUmsatzMitZuordnung>>> GetGematcht(
            [FromQuery] DateTime? von = null,
            [FromQuery] DateTime? bis = null)
        {
            try
            {
                return Ok(await _service.GetGematchteTransaktionenAsync(von, bis));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Holt offene Rechnungen fuer manuelles Matching
        /// </summary>
        [HttpGet("rechnungen/offen")]
        public async Task<ActionResult<List<OffeneRechnungInfo>>> GetOffeneRechnungen()
        {
            try
            {
                return Ok(await _service.GetOffeneRechnungenAsync());
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Holt Zahlungsabgleich-Module (PayPal, Bank, etc.)
        /// </summary>
        [HttpGet("module")]
        public async Task<ActionResult<List<ZahlungsabgleichModul>>> GetModule()
        {
            try
            {
                return Ok(await _service.GetModuleAsync());
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion

        #region Auto-Matching

        /// <summary>
        /// Fuehrt Auto-Matching durch
        /// </summary>
        [HttpPost("auto-match")]
        public async Task<ActionResult<AutoMatchResult>> AutoMatch(
            [FromQuery] int kBenutzer = 1,
            [FromQuery] bool nurVorschlaege = false)
        {
            try
            {
                var result = await _service.AutoMatchAsync(kBenutzer, nurVorschlaege);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Holt nur Matching-Vorschlaege ohne automatische Zuordnung
        /// </summary>
        [HttpGet("vorschlaege")]
        public async Task<ActionResult<List<MatchVorschlag>>> GetVorschlaege([FromQuery] int kBenutzer = 1)
        {
            try
            {
                var result = await _service.AutoMatchAsync(kBenutzer, nurVorschlaege: true);
                return Ok(result.Vorschlaege);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion

        #region Manuelle Zuordnung

        /// <summary>
        /// Ordnet Transaktion manuell einer Rechnung zu
        /// </summary>
        [HttpPost("{kUmsatz}/zuordnen/rechnung/{kRechnung}")]
        public async Task<ActionResult> ZuordnenZuRechnung(
            int kUmsatz,
            int kRechnung,
            [FromQuery] int kBenutzer = 1)
        {
            try
            {
                var kZahlung = await _service.ZuordnenZuRechnungAsync(kUmsatz, kRechnung, kBenutzer);
                return Ok(new { success = true, kZahlung });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Ordnet Transaktion manuell einem Auftrag zu
        /// </summary>
        [HttpPost("{kUmsatz}/zuordnen/auftrag/{kAuftrag}")]
        public async Task<ActionResult> ZuordnenZuAuftrag(
            int kUmsatz,
            int kAuftrag,
            [FromQuery] int kBenutzer = 1)
        {
            try
            {
                var kZahlung = await _service.ZuordnenZuAuftragAsync(kUmsatz, kAuftrag, kBenutzer);
                return Ok(new { success = true, kZahlung });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Ignoriert eine Transaktion
        /// </summary>
        [HttpPost("{kUmsatz}/ignorieren")]
        public async Task<ActionResult> Ignorieren(int kUmsatz, [FromQuery] int kBenutzer = 1)
        {
            try
            {
                await _service.IgnorierenAsync(kUmsatz, kBenutzer);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Hebt Zuordnung auf
        /// </summary>
        [HttpPost("{kUmsatz}/aufheben")]
        public async Task<ActionResult> ZuordnungAufheben(int kUmsatz, [FromQuery] int kBenutzer = 1)
        {
            try
            {
                await _service.ZuordnungAufhebenAsync(kUmsatz, kBenutzer);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion

        #region Import

        /// <summary>
        /// Importiert PayPal-Transaktionen
        /// </summary>
        [HttpPost("import/paypal")]
        public async Task<ActionResult<ImportResult>> ImportPayPal(
            [FromBody] List<PayPalTransaktion> transaktionen,
            [FromQuery] int kModul,
            [FromQuery] int kBenutzer = 1)
        {
            try
            {
                var result = await _service.ImportPayPalTransaktionenAsync(transaktionen, kModul, kBenutzer);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Importiert Mollie-Transaktionen
        /// </summary>
        [HttpPost("import/mollie")]
        public async Task<ActionResult<ImportResult>> ImportMollie(
            [FromBody] List<MollieTransaktion> transaktionen,
            [FromQuery] int kModul,
            [FromQuery] int kBenutzer = 1)
        {
            try
            {
                var result = await _service.ImportMollieTransaktionenAsync(transaktionen, kModul, kBenutzer);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Importiert MT940-Bankumsaetze
        /// </summary>
        [HttpPost("import/mt940")]
        public async Task<ActionResult<ImportResult>> ImportMT940(
            [FromBody] List<MT940Umsatz> umsaetze,
            [FromQuery] int kModul,
            [FromQuery] int kBenutzer = 1)
        {
            try
            {
                var result = await _service.ImportMT940Async(umsaetze, kModul, kBenutzer);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion

        #region Logs

        /// <summary>
        /// Holt Log-Eintraege mit Filtern
        /// </summary>
        [HttpGet("logs")]
        public async Task<ActionResult<List<LogService.LogEintrag>>> GetLogs(
            [FromQuery] string? kategorie = null,
            [FromQuery] string? modul = null,
            [FromQuery] string? suche = null,
            [FromQuery] DateTime? von = null,
            [FromQuery] DateTime? bis = null,
            [FromQuery] int limit = 500)
        {
            try
            {
                var filter = new LogService.LogFilter
                {
                    Kategorie = kategorie,
                    Modul = modul,
                    Suche = suche,
                    Von = von,
                    Bis = bis
                };
                return Ok(await _logService.GetLogsAsync(filter, limit));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Holt Log-Statistik
        /// </summary>
        [HttpGet("logs/stats")]
        public async Task<ActionResult<LogService.LogStats>> GetLogStats([FromQuery] int tage = 7)
        {
            try
            {
                return Ok(await _logService.GetStatsAsync(tage));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        /// <summary>
        /// Holt verfuegbare Kategorien
        /// </summary>
        [HttpGet("logs/kategorien")]
        public async Task<ActionResult<List<string>>> GetLogKategorien()
        {
            try
            {
                return Ok(await _logService.GetKategorienAsync());
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        #endregion
    }
}

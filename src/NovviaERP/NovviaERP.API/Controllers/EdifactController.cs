using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NovviaERP.Core.Services;

namespace NovviaERP.API.Controllers
{
    /// <summary>
    /// EDIFACT API fuer Pharma-Grosshandel EDI Integration
    /// Unterstuetzte Nachrichtentypen: ORDERS, ORDRSP, DESADV, INVOIC (D96A)
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class EdifactController : ControllerBase
    {
        private readonly EdifactService _edifact;

        public EdifactController(EdifactService edifact)
        {
            _edifact = edifact;
        }

        #region Partner

        /// <summary>Alle EDIFACT-Partner abrufen</summary>
        [HttpGet("partner")]
        public async Task<IActionResult> GetPartner([FromQuery] bool? nurAktive = true)
        {
            var partner = await _edifact.GetPartnerAsync(nurAktive);
            return Ok(partner);
        }

        /// <summary>Partner nach ID abrufen</summary>
        [HttpGet("partner/{id}")]
        public async Task<IActionResult> GetPartnerById(int id)
        {
            var partner = await _edifact.GetPartnerByIdAsync(id);
            return partner == null ? NotFound() : Ok(partner);
        }

        /// <summary>Partner nach GLN abrufen</summary>
        [HttpGet("partner/gln/{gln}")]
        public async Task<IActionResult> GetPartnerByGln(string gln)
        {
            var partner = await _edifact.GetPartnerByGlnAsync(gln);
            return partner == null ? NotFound() : Ok(partner);
        }

        /// <summary>Partner erstellen oder aktualisieren</summary>
        [HttpPost("partner")]
        [Authorize]
        public async Task<IActionResult> SavePartner([FromBody] EdifactService.EdifactPartner partner)
        {
            var id = await _edifact.SavePartnerAsync(partner);
            return Ok(new { id, message = "Partner gespeichert" });
        }

        /// <summary>Partner loeschen</summary>
        [HttpDelete("partner/{id}")]
        [Authorize]
        public async Task<IActionResult> DeletePartner(int id)
        {
            await _edifact.DeletePartnerAsync(id);
            return NoContent();
        }

        /// <summary>Verbindung zu Partner testen</summary>
        [HttpPost("partner/{id}/test")]
        [Authorize]
        public async Task<IActionResult> TestConnection(int id)
        {
            var success = await _edifact.TestConnectionAsync(id);
            return Ok(new { success, message = success ? "Verbindung erfolgreich" : "Verbindung fehlgeschlagen" });
        }

        #endregion

        #region Nachrichten-Log

        /// <summary>EDIFACT Nachrichten-Log abrufen</summary>
        [HttpGet("log")]
        public async Task<IActionResult> GetLog(
            [FromQuery] int? partnerId = null,
            [FromQuery] string? richtung = null,
            [FromQuery] string? nachrichtentyp = null,
            [FromQuery] string? status = null,
            [FromQuery] int limit = 100)
        {
            var logs = await _edifact.GetLogAsync(partnerId, richtung, nachrichtentyp, status, limit);
            return Ok(logs);
        }

        /// <summary>Log-Eintrag nach ID abrufen</summary>
        [HttpGet("log/{id}")]
        public async Task<IActionResult> GetLogById(int id)
        {
            var log = await _edifact.GetLogByIdAsync(id);
            return log == null ? NotFound() : Ok(log);
        }

        #endregion

        #region ORDERS (Eingehende Bestellungen)

        /// <summary>ORDERS Nachricht parsen und als Bestellung importieren</summary>
        /// <remarks>
        /// Erwartet EDIFACT ORDERS D96A im Body.
        /// Beispiel-Partner: Phoenix (GLN 4300001000001), Sanacorp (GLN 4300002000001)
        /// </remarks>
        [HttpPost("orders/parse")]
        [Authorize]
        public async Task<IActionResult> ParseOrders([FromBody] string edifactContent, [FromQuery] int partnerId)
        {
            try
            {
                var result = await _edifact.ParseOrdersAsync(edifactContent, partnerId);
                return Ok(new
                {
                    success = true,
                    bestellNummer = result.BestellNummer,
                    bestellDatum = result.BestellDatum,
                    lieferDatum = result.LieferDatum,
                    positionen = result.Positionen.Count,
                    lieferAdresse = result.LieferAdresse
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        /// <summary>ORDERS aus Datei importieren (multipart/form-data)</summary>
        [HttpPost("orders/import")]
        [Authorize]
        public async Task<IActionResult> ImportOrders([FromForm] IFormFile file, [FromQuery] int partnerId)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "Keine Datei hochgeladen" });

            try
            {
                using var reader = new StreamReader(file.OpenReadStream());
                var content = await reader.ReadToEndAsync();
                var result = await _edifact.ParseOrdersAsync(content, partnerId);

                return Ok(new
                {
                    success = true,
                    fileName = file.FileName,
                    bestellNummer = result.BestellNummer,
                    positionen = result.Positionen.Count
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        #endregion

        #region ORDRSP (Bestellbestaetigung)

        /// <summary>ORDRSP Nachricht fuer Bestellung generieren</summary>
        [HttpGet("ordrsp/{bestellungId}")]
        [Authorize]
        public async Task<IActionResult> GenerateOrdrsp(int bestellungId, [FromQuery] int partnerId)
        {
            try
            {
                var edifact = await _edifact.GenerateOrdrspAsync(bestellungId, partnerId);
                return Content(edifact, "text/plain");
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>ORDRSP als Datei herunterladen</summary>
        [HttpGet("ordrsp/{bestellungId}/download")]
        [Authorize]
        public async Task<IActionResult> DownloadOrdrsp(int bestellungId, [FromQuery] int partnerId)
        {
            try
            {
                var edifact = await _edifact.GenerateOrdrspAsync(bestellungId, partnerId);
                var fileName = $"ORDRSP_{bestellungId}_{DateTime.Now:yyyyMMdd_HHmmss}.edi";
                return File(System.Text.Encoding.UTF8.GetBytes(edifact), "application/octet-stream", fileName);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        #endregion

        #region DESADV (Lieferavis)

        /// <summary>DESADV Nachricht fuer Lieferung generieren</summary>
        [HttpGet("desadv/{lieferungId}")]
        [Authorize]
        public async Task<IActionResult> GenerateDesadv(int lieferungId, [FromQuery] int partnerId)
        {
            try
            {
                var edifact = await _edifact.GenerateDesadvAsync(lieferungId, partnerId);
                return Content(edifact, "text/plain");
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>DESADV als Datei herunterladen</summary>
        [HttpGet("desadv/{lieferungId}/download")]
        [Authorize]
        public async Task<IActionResult> DownloadDesadv(int lieferungId, [FromQuery] int partnerId)
        {
            try
            {
                var edifact = await _edifact.GenerateDesadvAsync(lieferungId, partnerId);
                var fileName = $"DESADV_{lieferungId}_{DateTime.Now:yyyyMMdd_HHmmss}.edi";
                return File(System.Text.Encoding.UTF8.GetBytes(edifact), "application/octet-stream", fileName);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        #endregion

        #region INVOIC (Rechnung)

        /// <summary>INVOIC Nachricht fuer Rechnung generieren</summary>
        [HttpGet("invoic/{rechnungId}")]
        [Authorize]
        public async Task<IActionResult> GenerateInvoic(int rechnungId, [FromQuery] int partnerId)
        {
            try
            {
                var edifact = await _edifact.GenerateInvoicAsync(rechnungId, partnerId);
                return Content(edifact, "text/plain");
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>INVOIC als Datei herunterladen</summary>
        [HttpGet("invoic/{rechnungId}/download")]
        [Authorize]
        public async Task<IActionResult> DownloadInvoic(int rechnungId, [FromQuery] int partnerId)
        {
            try
            {
                var edifact = await _edifact.GenerateInvoicAsync(rechnungId, partnerId);
                var fileName = $"INVOIC_{rechnungId}_{DateTime.Now:yyyyMMdd_HHmmss}.edi";
                return File(System.Text.Encoding.UTF8.GetBytes(edifact), "application/octet-stream", fileName);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        #endregion
    }
}

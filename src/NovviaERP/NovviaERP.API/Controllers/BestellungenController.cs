using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NovviaERP.Core.Data;
using NovviaERP.Core.Entities;

namespace NovviaERP.API.Controllers
{
    [ApiController][Route("api/[controller]")]
    public class BestellungenController : ControllerBase
    {
        private readonly JtlDbContext _db;
        public BestellungenController(JtlDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] BestellStatus? status, [FromQuery] int limit = 100) =>
            Ok(await _db.GetBestellungenAsync(status: status, limit: limit));

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id) {
            var b = await _db.GetBestellungByIdAsync(id);
            return b == null ? NotFound() : Ok(b);
        }

        [HttpPost][Authorize]
        public async Task<IActionResult> Create([FromBody] Bestellung bestellung) {
            var id = await _db.CreateBestellungAsync(bestellung);
            return CreatedAtAction(nameof(GetById), new { id }, bestellung);
        }

        [HttpPatch("{id}/status")][Authorize]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] StatusUpdate update) {
            await _db.UpdateBestellStatusAsync(id, update.Status);
            return NoContent();
        }

        [HttpPost("{id}/rechnung")][Authorize]
        public async Task<IActionResult> CreateRechnung(int id) {
            var rechnungId = await _db.CreateRechnungAsync(id);
            return Ok(new { rechnungId });
        }

        [HttpPost("{id}/lieferschein")][Authorize]
        public async Task<IActionResult> CreateLieferschein(int id) {
            var lsId = await _db.CreateLieferscheinAsync(id);
            return Ok(new { lieferscheinId = lsId });
        }
    }
    public record StatusUpdate(BestellStatus Status);
}

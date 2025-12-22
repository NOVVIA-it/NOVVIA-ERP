using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NovviaERP.Core.Data;
using NovviaERP.Core.Entities;

namespace NovviaERP.API.Controllers
{
    [ApiController][Route("api/[controller]")]
    public class ArtikelController : ControllerBase
    {
        private readonly JtlDbContext _db;
        public ArtikelController(JtlDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? suche, [FromQuery] int limit = 100) =>
            Ok(await _db.GetArtikelAsync(suche, limit: limit));

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id) {
            var artikel = await _db.GetArtikelByIdAsync(id);
            return artikel == null ? NotFound() : Ok(artikel);
        }

        [HttpGet("barcode/{barcode}")]
        public async Task<IActionResult> GetByBarcode(string barcode) {
            var artikel = await _db.GetArtikelByBarcodeAsync(barcode);
            return artikel == null ? NotFound() : Ok(artikel);
        }

        [HttpPost][Authorize]
        public async Task<IActionResult> Create([FromBody] Artikel artikel) {
            var id = await _db.CreateArtikelAsync(artikel);
            return CreatedAtAction(nameof(GetById), new { id }, artikel);
        }

        [HttpPut("{id}")][Authorize]
        public async Task<IActionResult> Update(int id, [FromBody] Artikel artikel) {
            artikel.Id = id;
            await _db.UpdateArtikelAsync(artikel);
            return NoContent();
        }

        [HttpPatch("{id}/bestand")][Authorize]
        public async Task<IActionResult> UpdateBestand(int id, [FromBody] BestandUpdate update) {
            await _db.UpdateLagerbestandAsync(id, update.Menge, update.LagerId, update.Grund);
            return NoContent();
        }
    }
    public record BestandUpdate(decimal Menge, int? LagerId = null, string? Grund = null);
}

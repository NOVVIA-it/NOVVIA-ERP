using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NovviaERP.Core.Data;
using NovviaERP.Core.Entities;

namespace NovviaERP.API.Controllers
{
    [ApiController][Route("api/[controller]")]
    public class KundenController : ControllerBase
    {
        private readonly JtlDbContext _db;
        public KundenController(JtlDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? suche, [FromQuery] int limit = 100) =>
            Ok(await _db.GetKundenAsync(suche, limit: limit));

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id) {
            var k = await _db.GetKundeByIdAsync(id);
            return k == null ? NotFound() : Ok(k);
        }

        [HttpPost][Authorize]
        public async Task<IActionResult> Create([FromBody] Kunde kunde) {
            var id = await _db.CreateKundeAsync(kunde);
            return CreatedAtAction(nameof(GetById), new { id }, kunde);
        }

        [HttpPut("{id}")][Authorize]
        public async Task<IActionResult> Update(int id, [FromBody] Kunde kunde) {
            kunde.Id = id;
            await _db.UpdateKundeAsync(kunde);
            return NoContent();
        }
    }
}

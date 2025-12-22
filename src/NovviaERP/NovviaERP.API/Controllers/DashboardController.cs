using Microsoft.AspNetCore.Mvc;
using NovviaERP.Core.Data;

namespace NovviaERP.API.Controllers
{
    [ApiController][Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly JtlDbContext _db;
        public DashboardController(JtlDbContext db) => _db = db;

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats() => Ok(await _db.GetDashboardStatsAsync());
    }
}

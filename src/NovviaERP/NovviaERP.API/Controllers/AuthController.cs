using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NovviaERP.Core.Data;
using NovviaERP.Core.Services;

namespace NovviaERP.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _auth;

        public AuthController(JtlDbContext db)
        {
            _auth = new AuthService(db);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await _auth.LoginAsync(request.Login, request.Passwort, ip);
            
            if (!result.Success)
                return Unauthorized(new { error = result.Error });

            return Ok(new
            {
                token = result.Token,
                benutzer = new
                {
                    id = result.Benutzer!.Id,
                    login = result.Benutzer.Login,
                    name = $"{result.Benutzer.Vorname} {result.Benutzer.Nachname}".Trim(),
                    email = result.Benutzer.Email
                },
                rolle = result.Rolle?.Name,
                berechtigungen = result.Berechtigungen
            });
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _auth.LogoutAsync(userId, ip);
            return Ok();
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
            try
            {
                await _auth.ChangePasswordAsync(userId, request.AltesPasswort, request.NeuesPasswort);
                return Ok(new { message = "Passwort geändert" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("me")]
        [Authorize]
        public IActionResult GetCurrentUser()
        {
            return Ok(new
            {
                id = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
                login = User.Identity?.Name,
                name = User.FindFirst("fullname")?.Value,
                rolle = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value,
                isAdmin = User.HasClaim("isAdmin", "true"),
                berechtigungen = User.Claims.Where(c => c.Type == "permission").Select(c => c.Value)
            });
        }

        [HttpGet("check/{modul}/{aktion}")]
        [Authorize]
        public IActionResult CheckPermission(string modul, string aktion)
        {
            var hatBerechtigung = AuthService.HatBerechtigung(User, modul, aktion);
            return Ok(new { hatBerechtigung });
        }
    }

    public record LoginRequest(string Login, string Passwort);
    public record ChangePasswordRequest(string AltesPasswort, string NeuesPasswort);
}

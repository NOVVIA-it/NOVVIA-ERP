using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using NovviaERP.Core.Data;
using NovviaERP.Core.Entities;
using Dapper;
using Serilog;

namespace NovviaERP.Core.Services
{
    public class AuthService
    {
        private readonly JtlDbContext _db;
        private readonly string _jwtSecret;
        private readonly int _tokenExpirationHours;
        private static readonly ILogger _log = Log.ForContext<AuthService>();

        public AuthService(JtlDbContext db, string jwtSecret = "YourSuperSecretKeyHere32Chars!!", int tokenExpirationHours = 8)
        {
            _db = db;
            _jwtSecret = jwtSecret;
            _tokenExpirationHours = tokenExpirationHours;
        }

        #region Authentifizierung
        public async Task<AuthResult> LoginAsync(string login, string passwort, string? ip = null)
        {
            var conn = await _db.GetConnectionAsync();
            var benutzer = await conn.QuerySingleOrDefaultAsync<Benutzer>(
                "SELECT * FROM tBenutzer WHERE cLogin = @Login AND nAktiv = 1", new { Login = login });

            if (benutzer == null)
            {
                await LogAsync(0, "LOGIN_FAILED", "Auth", $"Unbekannter Benutzer: {login}", ip);
                return new AuthResult { Success = false, Error = "Benutzername oder Passwort falsch" };
            }

            // Sperre prüfen
            if (benutzer.GesperrtBis.HasValue && benutzer.GesperrtBis > DateTime.Now)
            {
                return new AuthResult { Success = false, Error = $"Konto gesperrt bis {benutzer.GesperrtBis:HH:mm}" };
            }

            // Passwort prüfen
            var hash = HashPasswort(passwort, benutzer.Salt);
            if (hash != benutzer.PasswortHash)
            {
                benutzer.Fehlversuche++;
                if (benutzer.Fehlversuche >= 5)
                {
                    benutzer.GesperrtBis = DateTime.Now.AddMinutes(15);
                    await conn.ExecuteAsync("UPDATE tBenutzer SET nFehlversuche = @F, dGesperrtBis = @G WHERE kBenutzer = @Id",
                        new { F = benutzer.Fehlversuche, G = benutzer.GesperrtBis, Id = benutzer.Id });
                    await LogAsync(benutzer.Id, "ACCOUNT_LOCKED", "Auth", "5 Fehlversuche - 15 Min Sperre", ip);
                    return new AuthResult { Success = false, Error = "Zu viele Fehlversuche. Konto für 15 Minuten gesperrt." };
                }
                await conn.ExecuteAsync("UPDATE tBenutzer SET nFehlversuche = @F WHERE kBenutzer = @Id",
                    new { F = benutzer.Fehlversuche, Id = benutzer.Id });
                await LogAsync(benutzer.Id, "LOGIN_FAILED", "Auth", $"Falsches Passwort (Versuch {benutzer.Fehlversuche})", ip);
                return new AuthResult { Success = false, Error = "Benutzername oder Passwort falsch" };
            }

            // Erfolgreicher Login
            await conn.ExecuteAsync("UPDATE tBenutzer SET nFehlversuche = 0, dGesperrtBis = NULL, dLetzterLogin = GETDATE() WHERE kBenutzer = @Id",
                new { Id = benutzer.Id });

            // Rolle & Berechtigungen laden
            var rolle = await conn.QuerySingleOrDefaultAsync<Rolle>("SELECT * FROM tRolle WHERE kRolle = @Id", new { Id = benutzer.RolleId });
            var berechtigungen = await conn.QueryAsync<string>(
                @"SELECT b.cModul + ':' + b.cAktion FROM tRolleBerechtigung rb 
                  INNER JOIN tBerechtigung b ON rb.kBerechtigung = b.kBerechtigung 
                  WHERE rb.kRolle = @RolleId", new { RolleId = benutzer.RolleId });

            // JWT Token erstellen
            var token = GenerateJwtToken(benutzer, rolle, berechtigungen.ToList());

            await LogAsync(benutzer.Id, "LOGIN_SUCCESS", "Auth", null, ip);
            _log.Information("Login erfolgreich: {Login}", login);

            return new AuthResult
            {
                Success = true,
                Token = token,
                Benutzer = benutzer,
                Rolle = rolle,
                Berechtigungen = berechtigungen.ToList()
            };
        }

        public async Task LogoutAsync(int benutzerId, string? ip = null)
        {
            await LogAsync(benutzerId, "LOGOUT", "Auth", null, ip);
        }

        private string GenerateJwtToken(Benutzer benutzer, Rolle? rolle, List<string> berechtigungen)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, benutzer.Id.ToString()),
                new Claim(ClaimTypes.Name, benutzer.Login),
                new Claim("fullname", $"{benutzer.Vorname} {benutzer.Nachname}".Trim()),
                new Claim(ClaimTypes.Role, rolle?.Name ?? "Benutzer")
            };

            if (rolle?.IstAdmin == true)
                claims.Add(new Claim("isAdmin", "true"));

            foreach (var berechtigung in berechtigungen)
                claims.Add(new Claim("permission", berechtigung));

            var token = new JwtSecurityToken(
                issuer: "NovviaERP",
                audience: "NovviaERP",
                claims: claims,
                expires: DateTime.Now.AddHours(_tokenExpirationHours),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
        #endregion

        #region Benutzerverwaltung
        public async Task<int> CreateBenutzerAsync(Benutzer benutzer, string passwort)
        {
            var conn = await _db.GetConnectionAsync();
            benutzer.Salt = GenerateSalt();
            benutzer.PasswortHash = HashPasswort(passwort, benutzer.Salt);
            
            var id = await conn.QuerySingleAsync<int>(
                @"INSERT INTO tBenutzer (cLogin, cPasswortHash, cSalt, cVorname, cNachname, cMail, kRolle, nAktiv, dErstellt)
                  VALUES (@Login, @PasswortHash, @Salt, @Vorname, @Nachname, @Email, @RolleId, @Aktiv, GETDATE());
                  SELECT SCOPE_IDENTITY();", benutzer);
            
            _log.Information("Benutzer erstellt: {Login} (ID: {Id})", benutzer.Login, id);
            return id;
        }

        public async Task ChangePasswordAsync(int benutzerId, string altesPasswort, string neuesPasswort)
        {
            var conn = await _db.GetConnectionAsync();
            var benutzer = await conn.QuerySingleOrDefaultAsync<Benutzer>("SELECT * FROM tBenutzer WHERE kBenutzer = @Id", new { Id = benutzerId });
            
            if (benutzer == null)
                throw new Exception("Benutzer nicht gefunden");

            var altHash = HashPasswort(altesPasswort, benutzer.Salt);
            if (altHash != benutzer.PasswortHash)
                throw new Exception("Altes Passwort ist falsch");

            var neuerSalt = GenerateSalt();
            var neuerHash = HashPasswort(neuesPasswort, neuerSalt);

            await conn.ExecuteAsync("UPDATE tBenutzer SET cPasswortHash = @Hash, cSalt = @Salt WHERE kBenutzer = @Id",
                new { Hash = neuerHash, Salt = neuerSalt, Id = benutzerId });

            await LogAsync(benutzerId, "PASSWORD_CHANGED", "Auth", null, null);
        }

        public async Task ResetPasswordAsync(int benutzerId, string neuesPasswort)
        {
            var conn = await _db.GetConnectionAsync();
            var neuerSalt = GenerateSalt();
            var neuerHash = HashPasswort(neuesPasswort, neuerSalt);

            await conn.ExecuteAsync(
                "UPDATE tBenutzer SET cPasswortHash = @Hash, cSalt = @Salt, nFehlversuche = 0, dGesperrtBis = NULL WHERE kBenutzer = @Id",
                new { Hash = neuerHash, Salt = neuerSalt, Id = benutzerId });

            await LogAsync(benutzerId, "PASSWORD_RESET", "Auth", "Admin-Reset", null);
        }

        public async Task<IEnumerable<Benutzer>> GetBenutzerAsync()
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QueryAsync<Benutzer>(
                @"SELECT b.*, r.cName AS RolleName FROM tBenutzer b 
                  LEFT JOIN tRolle r ON b.kRolle = r.kRolle ORDER BY b.cNachname");
        }

        public async Task SetBenutzerAktivAsync(int benutzerId, bool aktiv)
        {
            var conn = await _db.GetConnectionAsync();
            await conn.ExecuteAsync("UPDATE tBenutzer SET nAktiv = @Aktiv WHERE kBenutzer = @Id", new { Aktiv = aktiv, Id = benutzerId });
        }
        #endregion

        #region Rollenverwaltung
        public async Task<IEnumerable<Rolle>> GetRollenAsync()
        {
            var conn = await _db.GetConnectionAsync();
            var rollen = await conn.QueryAsync<Rolle>("SELECT * FROM tRolle ORDER BY cName");
            foreach (var rolle in rollen)
            {
                rolle.Berechtigungen = (await conn.QueryAsync<RolleBerechtigung>(
                    @"SELECT rb.*, b.cModul, b.cAktion, b.cBeschreibung FROM tRolleBerechtigung rb 
                      INNER JOIN tBerechtigung b ON rb.kBerechtigung = b.kBerechtigung 
                      WHERE rb.kRolle = @RolleId", new { RolleId = rolle.Id })).ToList();
            }
            return rollen;
        }

        public async Task<int> CreateRolleAsync(Rolle rolle, List<int> berechtigungIds)
        {
            var conn = await _db.GetConnectionAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                var id = await conn.QuerySingleAsync<int>(
                    "INSERT INTO tRolle (cName, cBeschreibung, nIstAdmin) VALUES (@Name, @Beschreibung, @IstAdmin); SELECT SCOPE_IDENTITY();",
                    rolle, tx);

                foreach (var bId in berechtigungIds)
                    await conn.ExecuteAsync("INSERT INTO tRolleBerechtigung (kRolle, kBerechtigung) VALUES (@RolleId, @BerId)",
                        new { RolleId = id, BerId = bId }, tx);

                tx.Commit();
                return id;
            }
            catch { tx.Rollback(); throw; }
        }

        public async Task UpdateRolleBerechtigungenAsync(int rolleId, List<int> berechtigungIds)
        {
            var conn = await _db.GetConnectionAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                await conn.ExecuteAsync("DELETE FROM tRolleBerechtigung WHERE kRolle = @Id", new { Id = rolleId }, tx);
                foreach (var bId in berechtigungIds)
                    await conn.ExecuteAsync("INSERT INTO tRolleBerechtigung (kRolle, kBerechtigung) VALUES (@RolleId, @BerId)",
                        new { RolleId = rolleId, BerId = bId }, tx);
                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }

        public async Task<IEnumerable<Berechtigung>> GetAlleBerechtigungenAsync()
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QueryAsync<Berechtigung>("SELECT * FROM tBerechtigung ORDER BY cModul, cAktion");
        }
        #endregion

        #region Berechtigungsprüfung
        public async Task<bool> HatBerechtigungAsync(int benutzerId, string modul, string aktion)
        {
            var conn = await _db.GetConnectionAsync();
            
            // Admin hat alles
            var istAdmin = await conn.QuerySingleOrDefaultAsync<bool>(
                @"SELECT r.nIstAdmin FROM tBenutzer b 
                  INNER JOIN tRolle r ON b.kRolle = r.kRolle 
                  WHERE b.kBenutzer = @Id", new { Id = benutzerId });
            if (istAdmin) return true;

            // Spezifische Berechtigung prüfen
            var hat = await conn.QuerySingleOrDefaultAsync<int>(
                @"SELECT COUNT(*) FROM tRolleBerechtigung rb
                  INNER JOIN tBerechtigung b ON rb.kBerechtigung = b.kBerechtigung
                  INNER JOIN tBenutzer u ON rb.kRolle = u.kRolle
                  WHERE u.kBenutzer = @BenutzerId AND b.cModul = @Modul AND b.cAktion = @Aktion",
                new { BenutzerId = benutzerId, Modul = modul, Aktion = aktion });

            return hat > 0;
        }

        public static bool HatBerechtigung(ClaimsPrincipal user, string modul, string aktion)
        {
            if (user.HasClaim("isAdmin", "true")) return true;
            return user.HasClaim("permission", $"{modul}:{aktion}");
        }
        #endregion

        #region Logging
        private async Task LogAsync(int benutzerId, string aktion, string? modul, string? details, string? ip)
        {
            var conn = await _db.GetConnectionAsync();
            await conn.ExecuteAsync(
                @"INSERT INTO tBenutzerLog (kBenutzer, cAktion, cModul, cDetails, cIP, dZeitpunkt) 
                  VALUES (@BenutzerId, @Aktion, @Modul, @Details, @IP, GETDATE())",
                new { BenutzerId = benutzerId, Aktion = aktion, Modul = modul, Details = details, IP = ip });
        }

        public async Task<IEnumerable<BenutzerLog>> GetBenutzerLogsAsync(int? benutzerId = null, DateTime? von = null, DateTime? bis = null, int limit = 100)
        {
            var conn = await _db.GetConnectionAsync();
            var sql = @"SELECT TOP (@Limit) l.*, b.cLogin FROM tBenutzerLog l 
                        LEFT JOIN tBenutzer b ON l.kBenutzer = b.kBenutzer WHERE 1=1";
            if (benutzerId.HasValue) sql += " AND l.kBenutzer = @BenutzerId";
            if (von.HasValue) sql += " AND l.dZeitpunkt >= @Von";
            if (bis.HasValue) sql += " AND l.dZeitpunkt <= @Bis";
            sql += " ORDER BY l.dZeitpunkt DESC";
            return await conn.QueryAsync<BenutzerLog>(sql, new { Limit = limit, BenutzerId = benutzerId, Von = von, Bis = bis });
        }
        #endregion

        #region Hilfsfunktionen
        private static string GenerateSalt()
        {
            var bytes = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        private static string HashPasswort(string passwort, string salt)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(passwort + salt);
            var hash = sha.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
        #endregion
    }

    public class AuthResult
    {
        public bool Success { get; set; }
        public string? Token { get; set; }
        public string? Error { get; set; }
        public Benutzer? Benutzer { get; set; }
        public Rolle? Rolle { get; set; }
        public List<string> Berechtigungen { get; set; } = new();
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using NovviaERP.Core.Data;
using Serilog;

namespace NovviaERP.Core.Services
{
    /// <summary>
    /// Service fuer NOVVIA Benutzerverwaltung und Rechte
    /// </summary>
    public class BenutzerService
    {
        private readonly JtlDbContext _db;
        private static readonly ILogger _log = Log.ForContext<BenutzerService>();

        // Aktueller eingeloggter Benutzer (Session-Scope)
        public NovviaBenutzer? AktuellerBenutzer { get; private set; }
        public string? SessionToken { get; private set; }
        private HashSet<string> _benutzerRechte = new();

        public BenutzerService(JtlDbContext db)
        {
            _db = db;
        }

        #region Anmeldung / Session

        /// <summary>
        /// Benutzer anmelden
        /// </summary>
        public async Task<AnmeldeErgebnis> AnmeldenAsync(string benutzername, string passwort, string? rechnername = null, string? ip = null)
        {
            try
            {
                var conn = await _db.GetConnectionAsync();

                // Benutzer laden
                var benutzer = await conn.QueryFirstOrDefaultAsync<NovviaBenutzer>(
                    @"SELECT kBenutzer AS Id, cBenutzername AS Benutzername, cPasswortHash AS PasswortHash,
                             cVorname AS Vorname, cNachname AS Nachname, cEmail AS Email,
                             nAktiv AS Aktiv, nGesperrt AS Gesperrt, cSperrgrund AS Sperrgrund,
                             nFehlversuche AS Fehlversuche, dPasswortAblauf AS PasswortAblauf
                      FROM NOVVIA.Benutzer WHERE cBenutzername = @benutzername",
                    new { benutzername });

                if (benutzer == null)
                {
                    _log.Warning("Login fehlgeschlagen: Benutzer {Benutzername} nicht gefunden", benutzername);
                    return new AnmeldeErgebnis { Erfolg = false, Fehler = "Benutzer nicht gefunden" };
                }

                // Status pruefen
                if (!benutzer.Aktiv)
                {
                    _log.Warning("Login fehlgeschlagen: Benutzer {Benutzername} ist deaktiviert", benutzername);
                    return new AnmeldeErgebnis { Erfolg = false, Fehler = "Benutzer ist deaktiviert" };
                }

                if (benutzer.Gesperrt)
                {
                    _log.Warning("Login fehlgeschlagen: Benutzer {Benutzername} ist gesperrt", benutzername);
                    return new AnmeldeErgebnis { Erfolg = false, Fehler = $"Benutzer ist gesperrt: {benutzer.Sperrgrund}" };
                }

                // Passwort pruefen (BCrypt)
                if (!BCrypt.Net.BCrypt.Verify(passwort, benutzer.PasswortHash))
                {
                    // Fehlversuch registrieren
                    await conn.ExecuteAsync(
                        "EXEC NOVVIA.spFehlversuchRegistrieren @cBenutzername = @benutzername, @cRechnername = @rechnername, @cIP = @ip",
                        new { benutzername, rechnername, ip });

                    _log.Warning("Login fehlgeschlagen: Falsches Passwort fuer {Benutzername}", benutzername);
                    return new AnmeldeErgebnis { Erfolg = false, Fehler = "Falsches Passwort" };
                }

                // Session erstellen via SP
                var p = new DynamicParameters();
                p.Add("@cBenutzername", benutzername);
                p.Add("@cRechnername", rechnername ?? Environment.MachineName);
                p.Add("@cIP", ip);
                p.Add("@cAnwendung", "WPF");
                p.Add("@nSessionDauerMinuten", 480);
                p.Add("@kBenutzer", dbType: DbType.Int32, direction: ParameterDirection.Output);
                p.Add("@cSessionToken", dbType: DbType.String, size: 255, direction: ParameterDirection.Output);
                p.Add("@cFehler", dbType: DbType.String, size: 500, direction: ParameterDirection.Output);

                await conn.ExecuteAsync("NOVVIA.spBenutzerAnmelden", p, commandType: CommandType.StoredProcedure);

                var fehler = p.Get<string?>("@cFehler");
                if (!string.IsNullOrEmpty(fehler))
                {
                    return new AnmeldeErgebnis { Erfolg = false, Fehler = fehler };
                }

                var sessionToken = p.Get<string>("@cSessionToken");
                var kBenutzer = p.Get<int>("@kBenutzer");

                // Benutzer und Rechte laden
                AktuellerBenutzer = benutzer;
                AktuellerBenutzer.Id = kBenutzer;
                SessionToken = sessionToken;

                await LadeBenutzerRechteAsync();

                _log.Information("Login erfolgreich: {Benutzername} (ID: {kBenutzer})", benutzername, kBenutzer);

                return new AnmeldeErgebnis
                {
                    Erfolg = true,
                    Benutzer = AktuellerBenutzer,
                    SessionToken = sessionToken,
                    PasswortAblauf = benutzer.PasswortAblauf.HasValue && benutzer.PasswortAblauf <= DateTime.Now
                };
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Fehler bei Anmeldung fuer {Benutzername}", benutzername);
                return new AnmeldeErgebnis { Erfolg = false, Fehler = ex.Message };
            }
        }

        /// <summary>
        /// Benutzer abmelden
        /// </summary>
        public async Task AbmeldenAsync()
        {
            if (string.IsNullOrEmpty(SessionToken)) return;

            try
            {
                var conn = await _db.GetConnectionAsync();
                await conn.ExecuteAsync("EXEC NOVVIA.spBenutzerAbmelden @cSessionToken = @token", new { token = SessionToken });

                _log.Information("Logout: {Benutzername}", AktuellerBenutzer?.Benutzername);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Fehler bei Abmeldung");
            }
            finally
            {
                AktuellerBenutzer = null;
                SessionToken = null;
                _benutzerRechte.Clear();
            }
        }

        /// <summary>
        /// Session validieren
        /// </summary>
        public async Task<bool> SessionValidierenAsync()
        {
            if (string.IsNullOrEmpty(SessionToken)) return false;

            try
            {
                var conn = await _db.GetConnectionAsync();

                var p = new DynamicParameters();
                p.Add("@cSessionToken", SessionToken);
                p.Add("@nVerlaengernMinuten", 60);
                p.Add("@kBenutzer", dbType: DbType.Int32, direction: ParameterDirection.Output);
                p.Add("@nGueltig", dbType: DbType.Boolean, direction: ParameterDirection.Output);

                await conn.ExecuteAsync("NOVVIA.spSessionValidieren", p, commandType: CommandType.StoredProcedure);

                return p.Get<bool>("@nGueltig");
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Fehler bei Session-Validierung");
                return false;
            }
        }

        #endregion

        #region Rechte-Pruefung

        /// <summary>
        /// Prueft ob aktueller Benutzer ein Recht hat
        /// </summary>
        public bool HatRecht(string modul, string aktion)
        {
            var schluessel = $"{modul}.{aktion}";
            return _benutzerRechte.Contains(schluessel);
        }

        /// <summary>
        /// Prueft ob aktueller Benutzer Lesezugriff auf Modul hat
        /// </summary>
        public bool DarfLesen(string modul) => HatRecht(modul, "Lesen");

        /// <summary>
        /// Prueft ob aktueller Benutzer Schreibzugriff auf Modul hat
        /// </summary>
        public bool DarfBearbeiten(string modul) => HatRecht(modul, "Bearbeiten");

        /// <summary>
        /// Prueft ob aktueller Benutzer Erstellen darf
        /// </summary>
        public bool DarfErstellen(string modul) => HatRecht(modul, "Erstellen");

        /// <summary>
        /// Prueft ob aktueller Benutzer Loeschen darf
        /// </summary>
        public bool DarfLoeschen(string modul) => HatRecht(modul, "Loeschen");

        /// <summary>
        /// Alle Rechte des aktuellen Benutzers laden
        /// </summary>
        private async Task LadeBenutzerRechteAsync()
        {
            _benutzerRechte.Clear();

            if (AktuellerBenutzer == null) return;

            try
            {
                var conn = await _db.GetConnectionAsync();

                var rechte = await conn.QueryAsync<string>(
                    @"SELECT cSchluessel FROM NOVVIA.vBenutzerRechte
                      WHERE kBenutzer = @kBenutzer AND nErlaubt = 1",
                    new { kBenutzer = AktuellerBenutzer.Id });

                foreach (var recht in rechte)
                {
                    _benutzerRechte.Add(recht);
                }

                _log.Debug("Rechte geladen fuer {Benutzername}: {Anzahl} Rechte", AktuellerBenutzer.Benutzername, _benutzerRechte.Count);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Fehler beim Laden der Benutzerrechte");
            }
        }

        /// <summary>
        /// Alle Rechte des aktuellen Benutzers
        /// </summary>
        public IEnumerable<string> GetAlleRechte() => _benutzerRechte;

        /// <summary>
        /// Prueft Recht via Stored Procedure (fuer externe Aufrufe)
        /// </summary>
        public async Task<bool> HatRechtAsync(int kBenutzer, string rechtSchluessel)
        {
            try
            {
                var conn = await _db.GetConnectionAsync();

                var p = new DynamicParameters();
                p.Add("@kBenutzer", kBenutzer);
                p.Add("@cRechtSchluessel", rechtSchluessel);
                p.Add("@nHatRecht", dbType: DbType.Boolean, direction: ParameterDirection.Output);

                await conn.ExecuteAsync("NOVVIA.spHatRecht", p, commandType: CommandType.StoredProcedure);

                return p.Get<bool>("@nHatRecht");
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Fehler bei Rechte-Pruefung");
                return false;
            }
        }

        #endregion

        #region Benutzer-Verwaltung

        /// <summary>
        /// Alle Benutzer laden
        /// </summary>
        public async Task<IEnumerable<NovviaBenutzer>> GetAlleBenutzerAsync()
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QueryAsync<NovviaBenutzer>(
                @"SELECT b.kBenutzer AS Id, b.cBenutzername AS Benutzername, b.cVorname AS Vorname,
                         b.cNachname AS Nachname, b.cEmail AS Email, b.nAktiv AS Aktiv,
                         b.nGesperrt AS Gesperrt, b.dLetzteAnmeldung AS LetzteAnmeldung,
                         STRING_AGG(r.cBezeichnung, ', ') AS RollenText
                  FROM NOVVIA.Benutzer b
                  LEFT JOIN NOVVIA.BenutzerRolle br ON b.kBenutzer = br.kBenutzer
                  LEFT JOIN NOVVIA.Rolle r ON br.kRolle = r.kRolle
                  GROUP BY b.kBenutzer, b.cBenutzername, b.cVorname, b.cNachname, b.cEmail,
                           b.nAktiv, b.nGesperrt, b.dLetzteAnmeldung
                  ORDER BY b.cBenutzername");
        }

        /// <summary>
        /// Benutzer anlegen
        /// </summary>
        public async Task<int> BenutzerAnlegenAsync(NovviaBenutzer benutzer, string passwort, IEnumerable<int> rollenIds)
        {
            var conn = await _db.GetConnectionAsync();
            using var trans = conn.BeginTransaction();

            try
            {
                // Passwort hashen
                var hash = BCrypt.Net.BCrypt.HashPassword(passwort);

                // Benutzer einfuegen
                var kBenutzer = await conn.QuerySingleAsync<int>(
                    @"INSERT INTO NOVVIA.Benutzer (cBenutzername, cPasswortHash, cVorname, cNachname, cEmail, kErstelltVon)
                      OUTPUT INSERTED.kBenutzer
                      VALUES (@Benutzername, @hash, @Vorname, @Nachname, @Email, @ErstelltVon)",
                    new
                    {
                        benutzer.Benutzername,
                        hash,
                        benutzer.Vorname,
                        benutzer.Nachname,
                        benutzer.Email,
                        ErstelltVon = AktuellerBenutzer?.Id
                    },
                    trans);

                // Rollen zuweisen
                foreach (var rolleId in rollenIds)
                {
                    await conn.ExecuteAsync(
                        @"INSERT INTO NOVVIA.BenutzerRolle (kBenutzer, kRolle, kErstelltVon)
                          VALUES (@kBenutzer, @kRolle, @kErstelltVon)",
                        new { kBenutzer, kRolle = rolleId, kErstelltVon = AktuellerBenutzer?.Id },
                        trans);
                }

                trans.Commit();

                _log.Information("Benutzer angelegt: {Benutzername} (ID: {kBenutzer})", benutzer.Benutzername, kBenutzer);

                return kBenutzer;
            }
            catch
            {
                trans.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Passwort aendern
        /// </summary>
        public async Task PasswortAendernAsync(int kBenutzer, string neuesPasswort)
        {
            var hash = BCrypt.Net.BCrypt.HashPassword(neuesPasswort);

            var conn = await _db.GetConnectionAsync();
            await conn.ExecuteAsync(
                @"UPDATE NOVVIA.Benutzer
                  SET cPasswortHash = @hash, dPasswortAblauf = NULL, dGeaendert = SYSDATETIME(), kGeaendertVon = @kGeaendertVon
                  WHERE kBenutzer = @kBenutzer",
                new { kBenutzer, hash, kGeaendertVon = AktuellerBenutzer?.Id });

            // Log
            await conn.ExecuteAsync(
                @"INSERT INTO NOVVIA.BenutzerLog (kBenutzer, cAktion, nErfolgreich, cDetails)
                  VALUES (@kBenutzer, 'PasswortAenderung', 1, 'Passwort geaendert')",
                new { kBenutzer });

            _log.Information("Passwort geaendert fuer Benutzer ID {kBenutzer}", kBenutzer);
        }

        /// <summary>
        /// Benutzer sperren/entsperren
        /// </summary>
        public async Task BenutzerSperrenAsync(int kBenutzer, bool sperren, string? grund = null)
        {
            var conn = await _db.GetConnectionAsync();
            await conn.ExecuteAsync(
                @"UPDATE NOVVIA.Benutzer
                  SET nGesperrt = @sperren, cSperrgrund = @grund, nFehlversuche = 0, dGeaendert = SYSDATETIME()
                  WHERE kBenutzer = @kBenutzer",
                new { kBenutzer, sperren, grund });

            // Log
            await conn.ExecuteAsync(
                @"INSERT INTO NOVVIA.BenutzerLog (kBenutzer, cAktion, nErfolgreich, cDetails)
                  VALUES (@kBenutzer, @aktion, 1, @grund)",
                new { kBenutzer, aktion = sperren ? "Gesperrt" : "Entsperrt", grund });

            _log.Information("Benutzer {kBenutzer} {aktion}", kBenutzer, sperren ? "gesperrt" : "entsperrt");
        }

        #endregion

        #region Rollen-Verwaltung

        /// <summary>
        /// Alle Rollen laden
        /// </summary>
        public async Task<IEnumerable<NovviaRolle>> GetAlleRollenAsync()
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QueryAsync<NovviaRolle>(
                @"SELECT kRolle AS Id, cName AS Name, cBezeichnung AS Bezeichnung,
                         cBeschreibung AS Beschreibung, nIstSystem AS IstSystem, nIstAdmin AS IstAdmin,
                         (SELECT COUNT(*) FROM NOVVIA.BenutzerRolle br WHERE br.kRolle = r.kRolle) AS BenutzerAnzahl
                  FROM NOVVIA.Rolle r
                  WHERE nAktiv = 1
                  ORDER BY cName");
        }

        /// <summary>
        /// Rollen eines Benutzers laden
        /// </summary>
        public async Task<IEnumerable<NovviaRolle>> GetBenutzerRollenAsync(int kBenutzer)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QueryAsync<NovviaRolle>(
                @"SELECT r.kRolle AS Id, r.cName AS Name, r.cBezeichnung AS Bezeichnung
                  FROM NOVVIA.Rolle r
                  JOIN NOVVIA.BenutzerRolle br ON r.kRolle = br.kRolle
                  WHERE br.kBenutzer = @kBenutzer AND r.nAktiv = 1",
                new { kBenutzer });
        }

        /// <summary>
        /// Rollen eines Benutzers setzen
        /// </summary>
        public async Task SetBenutzerRollenAsync(int kBenutzer, IEnumerable<int> rollenIds)
        {
            var conn = await _db.GetConnectionAsync();
            using var trans = conn.BeginTransaction();

            try
            {
                // Alte Rollen loeschen
                await conn.ExecuteAsync("DELETE FROM NOVVIA.BenutzerRolle WHERE kBenutzer = @kBenutzer", new { kBenutzer }, trans);

                // Neue Rollen einfuegen
                foreach (var rolleId in rollenIds)
                {
                    await conn.ExecuteAsync(
                        @"INSERT INTO NOVVIA.BenutzerRolle (kBenutzer, kRolle, kErstelltVon)
                          VALUES (@kBenutzer, @kRolle, @kErstelltVon)",
                        new { kBenutzer, kRolle = rolleId, kErstelltVon = AktuellerBenutzer?.Id },
                        trans);
                }

                trans.Commit();
            }
            catch
            {
                trans.Rollback();
                throw;
            }
        }

        #endregion

        #region Module/Rechte

        /// <summary>
        /// Alle Module laden
        /// </summary>
        public async Task<IEnumerable<NovviaModul>> GetModuleAsync()
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QueryAsync<NovviaModul>(
                @"SELECT kModul AS Id, cName AS Name, cBezeichnung AS Bezeichnung, cIcon AS Icon, nSortierung AS Sortierung
                  FROM NOVVIA.Modul WHERE nAktiv = 1 ORDER BY nSortierung");
        }

        /// <summary>
        /// Alle Aktionen laden
        /// </summary>
        public async Task<IEnumerable<NovviaAktion>> GetAktionenAsync()
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QueryAsync<NovviaAktion>(
                @"SELECT kAktion AS Id, cName AS Name, cBezeichnung AS Bezeichnung
                  FROM NOVVIA.Aktion ORDER BY nSortierung");
        }

        /// <summary>
        /// Rechte einer Rolle laden
        /// </summary>
        public async Task<IEnumerable<NovviaRechtZuweisung>> GetRolleRechteAsync(int kRolle)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QueryAsync<NovviaRechtZuweisung>(
                @"SELECT r.kRecht AS RechtId, r.cSchluessel AS Schluessel, m.cName AS Modul, a.cName AS Aktion,
                         ISNULL(rr.nErlaubt, 0) AS Erlaubt
                  FROM NOVVIA.Recht r
                  JOIN NOVVIA.Modul m ON r.kModul = m.kModul
                  JOIN NOVVIA.Aktion a ON r.kAktion = a.kAktion
                  LEFT JOIN NOVVIA.RolleRecht rr ON r.kRecht = rr.kRecht AND rr.kRolle = @kRolle
                  WHERE r.nAktiv = 1 AND m.nAktiv = 1
                  ORDER BY m.nSortierung, a.nSortierung",
                new { kRolle });
        }

        /// <summary>
        /// Rechte einer Rolle setzen
        /// </summary>
        public async Task SetRolleRechteAsync(int kRolle, IEnumerable<int> rechtIds)
        {
            var conn = await _db.GetConnectionAsync();
            using var trans = conn.BeginTransaction();

            try
            {
                // Alte Rechte loeschen
                await conn.ExecuteAsync("DELETE FROM NOVVIA.RolleRecht WHERE kRolle = @kRolle", new { kRolle }, trans);

                // Neue Rechte einfuegen
                foreach (var rechtId in rechtIds)
                {
                    await conn.ExecuteAsync(
                        "INSERT INTO NOVVIA.RolleRecht (kRolle, kRecht, nErlaubt) VALUES (@kRolle, @kRecht, 1)",
                        new { kRolle, kRecht = rechtId },
                        trans);
                }

                trans.Commit();
            }
            catch
            {
                trans.Rollback();
                throw;
            }
        }

        #endregion
    }

    #region DTOs

    public class NovviaBenutzer
    {
        public int Id { get; set; }
        public string Benutzername { get; set; } = "";
        public string? PasswortHash { get; set; }
        public string? Vorname { get; set; }
        public string? Nachname { get; set; }
        public string? Email { get; set; }
        public bool Aktiv { get; set; } = true;
        public bool Gesperrt { get; set; }
        public string? Sperrgrund { get; set; }
        public int Fehlversuche { get; set; }
        public DateTime? LetzteAnmeldung { get; set; }
        public DateTime? PasswortAblauf { get; set; }
        public string? RollenText { get; set; }

        public string VollerName => $"{Vorname} {Nachname}".Trim();
    }

    public class NovviaRolle
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Bezeichnung { get; set; } = "";
        public string? Beschreibung { get; set; }
        public bool IstSystem { get; set; }
        public bool IstAdmin { get; set; }
        public int BenutzerAnzahl { get; set; }
    }

    public class NovviaModul
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Bezeichnung { get; set; } = "";
        public string? Icon { get; set; }
        public int Sortierung { get; set; }
    }

    public class NovviaAktion
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Bezeichnung { get; set; } = "";
    }

    public class NovviaRechtZuweisung
    {
        public int RechtId { get; set; }
        public string Schluessel { get; set; } = "";
        public string Modul { get; set; } = "";
        public string Aktion { get; set; } = "";
        public bool Erlaubt { get; set; }
    }

    public class AnmeldeErgebnis
    {
        public bool Erfolg { get; set; }
        public string? Fehler { get; set; }
        public NovviaBenutzer? Benutzer { get; set; }
        public string? SessionToken { get; set; }
        public bool PasswortAblauf { get; set; }
    }

    #endregion
}

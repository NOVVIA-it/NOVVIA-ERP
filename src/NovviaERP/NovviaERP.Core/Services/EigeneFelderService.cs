using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using NovviaERP.Core.Data;
using NovviaERP.Core.Entities;
using Serilog;

namespace NovviaERP.Core.Services
{
    /// <summary>
    /// Service für benutzerdefinierte Felder (Custom Fields)
    /// </summary>
    public class EigeneFelderService
    {
        private readonly JtlDbContext _db;
        private static readonly ILogger _log = Log.ForContext<EigeneFelderService>();

        // Gültige Bereiche für eigene Felder
        public static readonly string[] GueltigeBereiche = { 
            "Artikel", "Kunde", "Auftrag", "Rechnung", "Lieferschein", 
            "Lieferant", "RMA", "Einkaufsbestellung", "Kategorie" 
        };

        public EigeneFelderService(JtlDbContext db) => _db = db;

        #region Felddefinitionen
        /// <summary>
        /// Holt alle Felddefinitionen für einen Bereich
        /// </summary>
        public async Task<IEnumerable<EigenesFeldDefinition>> GetFelderAsync(string bereich, bool nurAktive = true)
        {
            var conn = await _db.GetConnectionAsync();
            var sql = "SELECT * FROM tEigenesFeld WHERE cBereich = @Bereich";
            if (nurAktive) sql += " AND nAktiv = 1";
            sql += " ORDER BY nSortierung, cName";
            return await conn.QueryAsync<EigenesFeldDefinition>(sql, new { Bereich = bereich });
        }

        /// <summary>
        /// Erstellt eine neue Felddefinition
        /// </summary>
        public async Task<int> CreateFeldAsync(EigenesFeldDefinition feld)
        {
            if (!GueltigeBereiche.Contains(feld.Bereich))
                throw new ArgumentException($"Ungültiger Bereich: {feld.Bereich}");

            var conn = await _db.GetConnectionAsync();
            
            // Internen Namen generieren falls nicht vorhanden
            if (string.IsNullOrEmpty(feld.InternerName))
                feld.InternerName = GenerateInternerName(feld.Name);

            var id = await conn.QuerySingleAsync<int>(@"
                INSERT INTO tEigenesFeld (cBereich, cName, cIntName, cTyp, cWerte, cStandardwert, 
                    nPflichtfeld, nSichtbarInListe, nSichtbarImDruck, nSortierung, nAktiv, cValidierung, cHinweis)
                VALUES (@Bereich, @Name, @InternerName, @Typ, @AuswahlWerte, @Standardwert,
                    @IstPflichtfeld, @SichtbarInListe, @SichtbarImDruck, @Sortierung, @Aktiv, @Validierung, @Hinweis);
                SELECT SCOPE_IDENTITY();", feld);

            _log.Information("Eigenes Feld erstellt: {Bereich}.{Name} (ID: {Id})", feld.Bereich, feld.Name, id);
            return id;
        }

        /// <summary>
        /// Aktualisiert eine Felddefinition
        /// </summary>
        public async Task UpdateFeldAsync(EigenesFeldDefinition feld)
        {
            var conn = await _db.GetConnectionAsync();
            await conn.ExecuteAsync(@"
                UPDATE tEigenesFeld SET cName = @Name, cTyp = @Typ, cWerte = @AuswahlWerte,
                    cStandardwert = @Standardwert, nPflichtfeld = @IstPflichtfeld, 
                    nSichtbarInListe = @SichtbarInListe, nSichtbarImDruck = @SichtbarImDruck,
                    nSortierung = @Sortierung, nAktiv = @Aktiv, cValidierung = @Validierung, cHinweis = @Hinweis
                WHERE kEigenesFeld = @Id", feld);
        }

        /// <summary>
        /// Löscht eine Felddefinition (nur wenn keine Werte vorhanden)
        /// </summary>
        public async Task<bool> DeleteFeldAsync(int feldId)
        {
            var conn = await _db.GetConnectionAsync();
            
            // Prüfen ob Werte existieren
            var hatWerte = await conn.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM tEigenesFeldWert WHERE kEigenesFeld = @Id", new { Id = feldId });
            
            if (hatWerte > 0)
            {
                _log.Warning("Eigenes Feld {Id} kann nicht gelöscht werden - {Count} Werte vorhanden", feldId, hatWerte);
                return false;
            }

            await conn.ExecuteAsync("DELETE FROM tEigenesFeld WHERE kEigenesFeld = @Id", new { Id = feldId });
            return true;
        }

        private string GenerateInternerName(string name)
        {
            var intern = name.ToLower()
                .Replace(" ", "_")
                .Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss");
            return System.Text.RegularExpressions.Regex.Replace(intern, @"[^a-z0-9_]", "");
        }
        #endregion

        #region Feldwerte
        /// <summary>
        /// Holt alle Feldwerte für einen Datensatz
        /// </summary>
        public async Task<Dictionary<string, string?>> GetWerteAsync(string bereich, int keyId)
        {
            var conn = await _db.GetConnectionAsync();
            var result = new Dictionary<string, string?>();

            var felder = await GetFelderAsync(bereich);
            var werte = await conn.QueryAsync<(int FeldId, string? Wert)>(
                "SELECT kEigenesFeld, cWert FROM tEigenesFeldWert WHERE kKey = @KeyId",
                new { KeyId = keyId });

            var werteDict = werte.ToDictionary(w => w.FeldId, w => w.Wert);

            foreach (var feld in felder)
            {
                var wert = werteDict.TryGetValue(feld.Id, out var v) ? v : feld.Standardwert;
                result[feld.InternerName ?? feld.Name] = wert;
            }

            return result;
        }

        /// <summary>
        /// Holt einen einzelnen Feldwert
        /// </summary>
        public async Task<string?> GetWertAsync(int feldId, int keyId)
        {
            var conn = await _db.GetConnectionAsync();
            var wert = await conn.QuerySingleOrDefaultAsync<string?>(
                "SELECT cWert FROM tEigenesFeldWert WHERE kEigenesFeld = @FeldId AND kKey = @KeyId",
                new { FeldId = feldId, KeyId = keyId });
            
            if (wert == null)
            {
                // Standardwert zurückgeben
                wert = await conn.QuerySingleOrDefaultAsync<string?>(
                    "SELECT cStandardwert FROM tEigenesFeld WHERE kEigenesFeld = @Id", new { Id = feldId });
            }
            
            return wert;
        }

        /// <summary>
        /// Setzt einen Feldwert
        /// </summary>
        public async Task SetWertAsync(int feldId, int keyId, string? wert)
        {
            var conn = await _db.GetConnectionAsync();

            // Validierung
            var feld = await conn.QuerySingleOrDefaultAsync<EigenesFeldDefinition>(
                "SELECT * FROM tEigenesFeld WHERE kEigenesFeld = @Id", new { Id = feldId });
            
            if (feld == null)
                throw new ArgumentException($"Feld {feldId} nicht gefunden");

            if (feld.IstPflichtfeld && string.IsNullOrEmpty(wert))
                throw new ArgumentException($"Feld '{feld.Name}' ist ein Pflichtfeld");

            // Typvalidierung
            if (!string.IsNullOrEmpty(wert))
            {
                var valid = feld.Typ switch
                {
                    EigenesFeldTyp.Int => int.TryParse(wert, out _),
                    EigenesFeldTyp.Decimal => decimal.TryParse(wert, out _),
                    EigenesFeldTyp.Date or EigenesFeldTyp.DateTime => DateTime.TryParse(wert, out _),
                    EigenesFeldTyp.Bool => bool.TryParse(wert, out _) || wert == "0" || wert == "1",
                    EigenesFeldTyp.Select => string.IsNullOrEmpty(feld.AuswahlWerte) || 
                                             feld.AuswahlWerte.Split('|').Contains(wert),
                    _ => true
                };

                if (!valid)
                    throw new ArgumentException($"Ungültiger Wert '{wert}' für Feld '{feld.Name}' (Typ: {feld.Typ})");

                // Regex-Validierung
                if (!string.IsNullOrEmpty(feld.Validierung))
                {
                    if (!System.Text.RegularExpressions.Regex.IsMatch(wert, feld.Validierung))
                        throw new ArgumentException($"Wert '{wert}' entspricht nicht dem Format für '{feld.Name}'");
                }
            }

            // Speichern
            var exists = await conn.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM tEigenesFeldWert WHERE kEigenesFeld = @FeldId AND kKey = @KeyId",
                new { FeldId = feldId, KeyId = keyId });

            if (exists > 0)
            {
                await conn.ExecuteAsync(
                    "UPDATE tEigenesFeldWert SET cWert = @Wert, dGeaendert = GETDATE() WHERE kEigenesFeld = @FeldId AND kKey = @KeyId",
                    new { Wert = wert, FeldId = feldId, KeyId = keyId });
            }
            else
            {
                await conn.ExecuteAsync(
                    "INSERT INTO tEigenesFeldWert (kEigenesFeld, kKey, cWert, dGeaendert) VALUES (@FeldId, @KeyId, @Wert, GETDATE())",
                    new { FeldId = feldId, KeyId = keyId, Wert = wert });
            }
        }

        /// <summary>
        /// Setzt mehrere Feldwerte auf einmal
        /// </summary>
        public async Task SetWerteAsync(string bereich, int keyId, Dictionary<string, string?> werte)
        {
            var felder = (await GetFelderAsync(bereich)).ToList();
            
            foreach (var kvp in werte)
            {
                var feld = felder.FirstOrDefault(f => f.InternerName == kvp.Key || f.Name == kvp.Key);
                if (feld != null)
                {
                    await SetWertAsync(feld.Id, keyId, kvp.Value);
                }
            }
        }

        /// <summary>
        /// Löscht alle Feldwerte für einen Datensatz
        /// </summary>
        public async Task DeleteWerteAsync(int keyId)
        {
            var conn = await _db.GetConnectionAsync();
            await conn.ExecuteAsync("DELETE FROM tEigenesFeldWert WHERE kKey = @KeyId", new { KeyId = keyId });
        }
        #endregion

        #region Hilfsmethoden
        /// <summary>
        /// Holt die Auswahlwerte für ein SELECT-Feld
        /// </summary>
        public async Task<string[]> GetAuswahlwerteAsync(int feldId)
        {
            var conn = await _db.GetConnectionAsync();
            var werte = await conn.QuerySingleOrDefaultAsync<string?>(
                "SELECT cWerte FROM tEigenesFeld WHERE kEigenesFeld = @Id", new { Id = feldId });
            
            return string.IsNullOrEmpty(werte) ? Array.Empty<string>() : werte.Split('|');
        }

        /// <summary>
        /// Sucht Datensätze nach Feldwerten
        /// </summary>
        public async Task<IEnumerable<int>> SucheNachWertAsync(int feldId, string wert)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QueryAsync<int>(
                "SELECT kKey FROM tEigenesFeldWert WHERE kEigenesFeld = @FeldId AND cWert LIKE @Wert",
                new { FeldId = feldId, Wert = $"%{wert}%" });
        }
        #endregion
    }
}

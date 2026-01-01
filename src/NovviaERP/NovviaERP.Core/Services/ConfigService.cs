using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;

namespace NovviaERP.Core.Services
{
    /// <summary>
    /// Service fuer NOVVIA Konfiguration pro Mandant
    /// </summary>
    public class ConfigService : IDisposable
    {
        private readonly string _connectionString;
        private SqlConnection? _connection;

        public ConfigService(string connectionString)
        {
            _connectionString = connectionString;
        }

        private async Task<SqlConnection> GetConnectionAsync()
        {
            if (_connection == null)
            {
                _connection = new SqlConnection(_connectionString);
            }
            if (_connection.State != System.Data.ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }
            return _connection;
        }

        /// <summary>
        /// Holt einen Konfigurationswert
        /// </summary>
        public async Task<string?> GetAsync(string kategorie, string schluessel)
        {
            try
            {
                var conn = await GetConnectionAsync();
                return await conn.QuerySingleOrDefaultAsync<string>(
                    "EXEC NOVVIA.spConfigGet @cKategorie, @cSchluessel",
                    new { cKategorie = kategorie, cSchluessel = schluessel });
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Holt alle Konfigurationswerte einer Kategorie
        /// </summary>
        public async Task<Dictionary<string, string>> GetAllAsync(string kategorie)
        {
            try
            {
                var conn = await GetConnectionAsync();
                var result = await conn.QueryAsync<(string cSchluessel, string cWert)>(
                    "EXEC NOVVIA.spConfigGet @cKategorie",
                    new { cKategorie = kategorie });

                var dict = new Dictionary<string, string>();
                foreach (var item in result)
                {
                    dict[item.cSchluessel] = item.cWert;
                }
                return dict;
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }

        /// <summary>
        /// Setzt einen Konfigurationswert
        /// </summary>
        public async Task SetAsync(string kategorie, string schluessel, string wert, string? beschreibung = null)
        {
            try
            {
                var conn = await GetConnectionAsync();
                await conn.ExecuteAsync(
                    "EXEC NOVVIA.spConfigSet @cKategorie, @cSchluessel, @cWert, @cBeschreibung",
                    new { cKategorie = kategorie, cSchluessel = schluessel, cWert = wert, cBeschreibung = beschreibung });
            }
            catch { }
        }

        /// <summary>
        /// Setzt mehrere Konfigurationswerte einer Kategorie
        /// </summary>
        public async Task SetAllAsync(string kategorie, Dictionary<string, string> werte)
        {
            foreach (var kv in werte)
            {
                await SetAsync(kategorie, kv.Key, kv.Value);
            }
        }

        /// <summary>
        /// Loescht einen Konfigurationswert
        /// </summary>
        public async Task DeleteAsync(string kategorie, string schluessel)
        {
            try
            {
                var conn = await GetConnectionAsync();
                await conn.ExecuteAsync(
                    "DELETE FROM NOVVIA.Config WHERE cKategorie = @Kategorie AND cSchluessel = @Schluessel",
                    new { Kategorie = kategorie, Schluessel = schluessel });
            }
            catch { }
        }

        // Hilfsmethoden fuer typische Datentypen

        public async Task<int> GetIntAsync(string kategorie, string schluessel, int defaultValue = 0)
        {
            var val = await GetAsync(kategorie, schluessel);
            return int.TryParse(val, out var result) ? result : defaultValue;
        }

        public async Task<bool> GetBoolAsync(string kategorie, string schluessel, bool defaultValue = false)
        {
            var val = await GetAsync(kategorie, schluessel);
            return bool.TryParse(val, out var result) ? result : defaultValue;
        }

        public async Task<decimal> GetDecimalAsync(string kategorie, string schluessel, decimal defaultValue = 0)
        {
            var val = await GetAsync(kategorie, schluessel);
            return decimal.TryParse(val, out var result) ? result : defaultValue;
        }

        public void Dispose()
        {
            _connection?.Dispose();
            _connection = null;
        }
    }
}

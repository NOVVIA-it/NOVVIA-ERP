using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.SqlClient;

namespace NovviaERP.Core.Services.Base
{
    /// <summary>
    /// Basis-Klasse fuer alle Datenbankservices mit einheitlicher Verbindungslogik
    /// </summary>
    public abstract class BaseDatabaseService : IDisposable
    {
        protected readonly string ConnectionString;
        private SqlConnection? _connection;
        private bool _disposed;

        protected BaseDatabaseService(string connectionString)
        {
            ConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        /// <summary>
        /// Gibt eine offene Datenbankverbindung zurueck (wiederverwendet bestehende)
        /// </summary>
        protected async Task<SqlConnection> GetConnectionAsync()
        {
            if (_connection == null)
            {
                _connection = new SqlConnection(ConnectionString);
            }

            if (_connection.State != ConnectionState.Open)
            {
                await _connection.OpenAsync();
            }

            return _connection;
        }

        /// <summary>
        /// Fuehrt eine Query aus und gibt eine Liste zurueck
        /// </summary>
        protected async Task<List<T>> QueryListAsync<T>(string sql, object? param = null)
        {
            var conn = await GetConnectionAsync();
            var result = await conn.QueryAsync<T>(sql, param);
            return result.ToList();
        }

        /// <summary>
        /// Fuehrt eine Query aus und gibt eine IEnumerable zurueck
        /// </summary>
        protected async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null)
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<T>(sql, param);
        }

        /// <summary>
        /// Fuehrt eine Query aus und gibt ein einzelnes Ergebnis zurueck
        /// </summary>
        protected async Task<T?> QuerySingleAsync<T>(string sql, object? param = null) where T : class
        {
            var conn = await GetConnectionAsync();
            return await conn.QuerySingleOrDefaultAsync<T>(sql, param);
        }

        /// <summary>
        /// Fuehrt eine Query aus und gibt einen Skalarwert zurueck
        /// </summary>
        protected async Task<T> QueryScalarAsync<T>(string sql, object? param = null)
        {
            var conn = await GetConnectionAsync();
            return await conn.ExecuteScalarAsync<T>(sql, param);
        }

        /// <summary>
        /// Fuehrt einen SQL-Befehl aus und gibt die Anzahl betroffener Zeilen zurueck
        /// </summary>
        protected async Task<int> ExecuteAsync(string sql, object? param = null)
        {
            var conn = await GetConnectionAsync();
            return await conn.ExecuteAsync(sql, param);
        }

        /// <summary>
        /// Fuehrt einen INSERT aus und gibt die neue ID zurueck (SCOPE_IDENTITY)
        /// </summary>
        protected async Task<int> InsertAndGetIdAsync(string sql, object? param = null)
        {
            var conn = await GetConnectionAsync();
            var result = await conn.QuerySingleAsync<int>(sql + "; SELECT CAST(SCOPE_IDENTITY() AS INT);", param);
            return result;
        }

        /// <summary>
        /// Startet eine Transaktion
        /// </summary>
        protected async Task<SqlTransaction> BeginTransactionAsync()
        {
            var conn = await GetConnectionAsync();
            return (SqlTransaction)await conn.BeginTransactionAsync();
        }

        // JTL Datumslogik Hilfsmethoden

        /// <summary>
        /// JTL Datum von heute
        /// </summary>
        protected static DateTime JtlHeute => DateTime.Today;

        /// <summary>
        /// JTL Datum von gestern
        /// </summary>
        protected static DateTime JtlGestern => DateTime.Today.AddDays(-1);

        /// <summary>
        /// JTL Datum: Anfang dieser Woche (Montag)
        /// </summary>
        protected static DateTime JtlWocheStart
        {
            get
            {
                var heute = DateTime.Today;
                int diff = (7 + (heute.DayOfWeek - DayOfWeek.Monday)) % 7;
                return heute.AddDays(-diff);
            }
        }

        /// <summary>
        /// JTL Datum: Ende dieser Woche (Sonntag)
        /// </summary>
        protected static DateTime JtlWocheEnde => JtlWocheStart.AddDays(6);

        /// <summary>
        /// JTL Datum: Anfang dieses Monats
        /// </summary>
        protected static DateTime JtlMonatStart => new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        /// <summary>
        /// JTL Datum: Ende dieses Monats
        /// </summary>
        protected static DateTime JtlMonatEnde => JtlMonatStart.AddMonths(1).AddDays(-1);

        /// <summary>
        /// JTL Datum: Anfang dieses Jahres
        /// </summary>
        protected static DateTime JtlJahrStart => new DateTime(DateTime.Today.Year, 1, 1);

        /// <summary>
        /// JTL Datum: Ende dieses Jahres
        /// </summary>
        protected static DateTime JtlJahrEnde => new DateTime(DateTime.Today.Year, 12, 31);

        /// <summary>
        /// JTL Datumsfilter: Letzte X Tage
        /// </summary>
        protected static DateTime JtlLetzteTage(int tage) => DateTime.Today.AddDays(-tage);

        /// <summary>
        /// Konvertiert JTL Zeitraum-String zu Datumsgrenzen
        /// </summary>
        public static (DateTime von, DateTime bis) JtlZeitraumZuDatum(string zeitraum)
        {
            return zeitraum switch
            {
                "Heute" => (JtlHeute, JtlHeute.AddDays(1).AddSeconds(-1)),
                "Gestern" => (JtlGestern, JtlGestern.AddDays(1).AddSeconds(-1)),
                "Diese Woche" => (JtlWocheStart, JtlWocheEnde.AddDays(1).AddSeconds(-1)),
                "Letzte Woche" => (JtlWocheStart.AddDays(-7), JtlWocheStart.AddSeconds(-1)),
                "Dieser Monat" => (JtlMonatStart, JtlMonatEnde.AddDays(1).AddSeconds(-1)),
                "Letzter Monat" => (JtlMonatStart.AddMonths(-1), JtlMonatStart.AddSeconds(-1)),
                "Dieses Jahr" => (JtlJahrStart, JtlJahrEnde.AddDays(1).AddSeconds(-1)),
                "Letztes Jahr" => (JtlJahrStart.AddYears(-1), JtlJahrStart.AddSeconds(-1)),
                "Letzte 7 Tage" => (JtlLetzteTage(7), DateTime.Now),
                "Letzte 30 Tage" => (JtlLetzteTage(30), DateTime.Now),
                "Letzte 90 Tage" => (JtlLetzteTage(90), DateTime.Now),
                _ => (DateTime.MinValue, DateTime.MaxValue) // Alle
            };
        }

        /// <summary>
        /// Standard JTL Zeitraum-Optionen fuer Dropdowns
        /// </summary>
        public static readonly string[] JtlZeitraumOptionen = new[]
        {
            "Alle",
            "Heute",
            "Gestern",
            "Diese Woche",
            "Letzte Woche",
            "Dieser Monat",
            "Letzter Monat",
            "Letzte 7 Tage",
            "Letzte 30 Tage",
            "Letzte 90 Tage",
            "Dieses Jahr",
            "Letztes Jahr"
        };

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _connection?.Dispose();
                    _connection = null;
                }
                _disposed = true;
            }
        }
    }
}

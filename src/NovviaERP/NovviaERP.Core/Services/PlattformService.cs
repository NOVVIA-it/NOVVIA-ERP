using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using NovviaERP.Core.Data;
using Serilog;

namespace NovviaERP.Core.Services
{
    /// <summary>
    /// Plattform/Shop-Connector Verwaltung (wie JTL Plattformen)
    /// Unterstützt: WooCommerce, eigene Shops (oeksline.de), externe Marktplätze
    /// </summary>
    public class PlattformService
    {
        private readonly JtlDbContext _db;
        private static readonly ILogger _log = Log.ForContext<PlattformService>();

        public PlattformService(JtlDbContext db) => _db = db;

        #region Plattform-Verwaltung
        /// <summary>
        /// Alle konfigurierten Plattformen
        /// </summary>
        public async Task<IEnumerable<Plattform>> GetPlattformenAsync(bool nurAktive = true)
        {
            var conn = await _db.GetConnectionAsync();
            var sql = "SELECT * FROM tPlattform";
            if (nurAktive) sql += " WHERE nAktiv = 1";
            sql += " ORDER BY nSortierung, cName";
            return await conn.QueryAsync<Plattform>(sql);
        }

        /// <summary>
        /// Einzelne Plattform laden
        /// </summary>
        public async Task<Plattform?> GetPlattformAsync(int id)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QuerySingleOrDefaultAsync<Plattform>(
                "SELECT * FROM tPlattform WHERE kPlattform = @Id", new { Id = id });
        }

        /// <summary>
        /// Plattform speichern
        /// </summary>
        public async Task<int> SavePlattformAsync(Plattform plattform)
        {
            var conn = await _db.GetConnectionAsync();
            
            if (plattform.Id == 0)
            {
                return await conn.QuerySingleAsync<int>(@"
                    INSERT INTO tPlattform (cName, cTyp, cUrl, cApiKey, cApiSecret, cBenutzername, cPasswort,
                        cWebhookUrl, nAktiv, nSortierung, cWaehrung, nSteuerInklusive, kStandardLager,
                        nAutoSync, nSyncIntervallMin, dLetzterSync, cKonfigurationJson)
                    VALUES (@Name, @Typ, @Url, @ApiKey, @ApiSecret, @Benutzername, @Passwort,
                        @WebhookUrl, @Aktiv, @Sortierung, @Waehrung, @SteuerInklusive, @StandardLagerId,
                        @AutoSync, @SyncIntervallMin, @LetzterSync, @KonfigurationJson);
                    SELECT SCOPE_IDENTITY();", plattform);
            }

            await conn.ExecuteAsync(@"
                UPDATE tPlattform SET cName=@Name, cUrl=@Url, cApiKey=@ApiKey, cApiSecret=@ApiSecret,
                    cBenutzername=@Benutzername, cPasswort=@Passwort, cWebhookUrl=@WebhookUrl,
                    nAktiv=@Aktiv, nSortierung=@Sortierung, cWaehrung=@Waehrung, nSteuerInklusive=@SteuerInklusive,
                    kStandardLager=@StandardLagerId, nAutoSync=@AutoSync, nSyncIntervallMin=@SyncIntervallMin,
                    cKonfigurationJson=@KonfigurationJson
                WHERE kPlattform=@Id", plattform);
            return plattform.Id;
        }

        /// <summary>
        /// Verbindung testen
        /// </summary>
        public async Task<(bool Erfolg, string Meldung)> TestVerbindungAsync(int plattformId)
        {
            var plattform = await GetPlattformAsync(plattformId);
            if (plattform == null) return (false, "Plattform nicht gefunden");

            // TODO: Je nach Typ tatsächliche Verbindung testen
            return plattform.Typ switch
            {
                PlattformTyp.WooCommerce => await TestWooCommerceAsync(plattform),
                PlattformTyp.Shopify => await TestShopifyAsync(plattform),
                PlattformTyp.Custom => await TestCustomApiAsync(plattform),
                _ => (false, $"Unbekannter Plattformtyp: {plattform.Typ}")
            };
        }

        private async Task<(bool, string)> TestWooCommerceAsync(Plattform p)
        {
            // WooCommerce REST API Test
            try
            {
                using var http = new System.Net.Http.HttpClient();
                var auth = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{p.ApiKey}:{p.ApiSecret}"));
                http.DefaultRequestHeaders.Add("Authorization", $"Basic {auth}");
                var response = await http.GetAsync($"{p.Url}/wp-json/wc/v3/system_status");
                return response.IsSuccessStatusCode 
                    ? (true, "Verbindung erfolgreich") 
                    : (false, $"HTTP {response.StatusCode}");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        private async Task<(bool, string)> TestShopifyAsync(Plattform p)
        {
            try
            {
                using var http = new System.Net.Http.HttpClient();
                http.DefaultRequestHeaders.Add("X-Shopify-Access-Token", p.ApiKey);
                var response = await http.GetAsync($"{p.Url}/admin/api/2024-01/shop.json");
                return response.IsSuccessStatusCode ? (true, "Verbindung erfolgreich") : (false, $"HTTP {response.StatusCode}");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        private async Task<(bool, string)> TestCustomApiAsync(Plattform p)
        {
            // Generischer API-Test
            try
            {
                using var http = new System.Net.Http.HttpClient();
                if (!string.IsNullOrEmpty(p.ApiKey))
                    http.DefaultRequestHeaders.Add("Authorization", $"Bearer {p.ApiKey}");
                var response = await http.GetAsync(p.Url);
                return response.IsSuccessStatusCode ? (true, "Verbindung erfolgreich") : (false, $"HTTP {response.StatusCode}");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }
        #endregion

        #region Artikel-Plattform-Zuordnung
        /// <summary>
        /// Plattform-spezifische Artikeldaten laden
        /// </summary>
        public async Task<ArtikelPlattformDaten?> GetArtikelPlattformDatenAsync(int artikelId, int plattformId)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QuerySingleOrDefaultAsync<ArtikelPlattformDaten>(
                "SELECT * FROM tArtikelPlattform WHERE kArtikel = @ArtikelId AND kPlattform = @PlattformId",
                new { ArtikelId = artikelId, PlattformId = plattformId });
        }

        /// <summary>
        /// Alle Plattform-Daten für einen Artikel
        /// </summary>
        public async Task<IEnumerable<ArtikelPlattformDaten>> GetArtikelPlattformDatenAlleAsync(int artikelId)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QueryAsync<ArtikelPlattformDaten>(@"
                SELECT ap.*, p.cName AS PlattformName 
                FROM tArtikelPlattform ap
                INNER JOIN tPlattform p ON ap.kPlattform = p.kPlattform
                WHERE ap.kArtikel = @ArtikelId",
                new { ArtikelId = artikelId });
        }

        /// <summary>
        /// Plattform-spezifische Artikeldaten speichern
        /// </summary>
        public async Task SaveArtikelPlattformDatenAsync(ArtikelPlattformDaten daten)
        {
            var conn = await _db.GetConnectionAsync();
            var existiert = await conn.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM tArtikelPlattform WHERE kArtikel = @ArtikelId AND kPlattform = @PlattformId",
                new { daten.ArtikelId, daten.PlattformId });

            if (existiert == 0)
            {
                await conn.ExecuteAsync(@"
                    INSERT INTO tArtikelPlattform (kArtikel, kPlattform, cExterneId, cName, cBeschreibung, cBeschreibungHtml,
                        cKurztext, fPreis, nAktiv, nLagerSync, nPreisSync, cKategorieExtern, cAttributeJson, dAktualisiert)
                    VALUES (@ArtikelId, @PlattformId, @ExterneId, @Name, @Beschreibung, @BeschreibungHtml,
                        @Kurztext, @Preis, @Aktiv, @LagerSync, @PreisSync, @KategorieExtern, @AttributeJson, GETDATE())", daten);
            }
            else
            {
                await conn.ExecuteAsync(@"
                    UPDATE tArtikelPlattform SET cExterneId=@ExterneId, cName=@Name, cBeschreibung=@Beschreibung,
                        cBeschreibungHtml=@BeschreibungHtml, cKurztext=@Kurztext, fPreis=@Preis, nAktiv=@Aktiv,
                        nLagerSync=@LagerSync, nPreisSync=@PreisSync, cKategorieExtern=@KategorieExtern,
                        cAttributeJson=@AttributeJson, dAktualisiert=GETDATE()
                    WHERE kArtikel=@ArtikelId AND kPlattform=@PlattformId", daten);
            }
        }
        #endregion

        #region Bilder-Verwaltung je Plattform
        /// <summary>
        /// Bilder für einen Artikel/Plattform laden
        /// </summary>
        public async Task<IEnumerable<ArtikelBild>> GetArtikelBilderAsync(int artikelId, int? plattformId = null)
        {
            var conn = await _db.GetConnectionAsync();
            var sql = "SELECT * FROM tArtikelBild WHERE kArtikel = @ArtikelId";
            if (plattformId.HasValue)
                sql += " AND (kPlattform IS NULL OR kPlattform = @PlattformId)";
            sql += " ORDER BY nSortierung";
            return await conn.QueryAsync<ArtikelBild>(sql, new { ArtikelId = artikelId, PlattformId = plattformId });
        }

        /// <summary>
        /// Bild hinzufügen
        /// </summary>
        public async Task<int> AddArtikelBildAsync(ArtikelBild bild)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QuerySingleAsync<int>(@"
                INSERT INTO tArtikelBild (kArtikel, kPlattform, cPfad, cUrl, cAltText, nSortierung, nHauptbild)
                VALUES (@ArtikelId, @PlattformId, @Pfad, @Url, @AltText, @Sortierung, @IstHauptbild);
                SELECT SCOPE_IDENTITY();", bild);
        }

        /// <summary>
        /// Bild für Plattform überschreiben
        /// </summary>
        public async Task SetPlattformBildAsync(int artikelId, int plattformId, int standardBildId, string? alternativPfad)
        {
            var conn = await _db.GetConnectionAsync();
            
            // Prüfen ob schon Override existiert
            var existiert = await conn.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM tArtikelBild WHERE kArtikel = @ArtikelId AND kPlattform = @PlattformId",
                new { ArtikelId = artikelId, PlattformId = plattformId });

            if (!string.IsNullOrEmpty(alternativPfad))
            {
                if (existiert == 0)
                {
                    await conn.ExecuteAsync(@"
                        INSERT INTO tArtikelBild (kArtikel, kPlattform, cPfad, nSortierung, nHauptbild)
                        VALUES (@ArtikelId, @PlattformId, @Pfad, 1, 1)",
                        new { ArtikelId = artikelId, PlattformId = plattformId, Pfad = alternativPfad });
                }
                else
                {
                    await conn.ExecuteAsync(@"
                        UPDATE tArtikelBild SET cPfad = @Pfad WHERE kArtikel = @ArtikelId AND kPlattform = @PlattformId",
                        new { ArtikelId = artikelId, PlattformId = plattformId, Pfad = alternativPfad });
                }
            }
        }
        #endregion

        #region Sync-Status
        /// <summary>
        /// Sync-Status aktualisieren
        /// </summary>
        public async Task UpdateSyncStatusAsync(int plattformId, bool erfolg, string? fehler = null)
        {
            var conn = await _db.GetConnectionAsync();
            await conn.ExecuteAsync(@"
                UPDATE tPlattform SET dLetzterSync = GETDATE(), 
                    nLetzterSyncErfolg = @Erfolg, cLetzterSyncFehler = @Fehler
                WHERE kPlattform = @Id",
                new { Id = plattformId, Erfolg = erfolg, Fehler = fehler });
        }

        /// <summary>
        /// Sync-Log schreiben
        /// </summary>
        public async Task LogSyncAsync(int plattformId, string aktion, int anzahl, string? details = null)
        {
            var conn = await _db.GetConnectionAsync();
            await conn.ExecuteAsync(@"
                INSERT INTO tPlattformSyncLog (kPlattform, cAktion, nAnzahl, cDetails, dZeitpunkt)
                VALUES (@PlattformId, @Aktion, @Anzahl, @Details, GETDATE())",
                new { PlattformId = plattformId, Aktion = aktion, Anzahl = anzahl, Details = details });
        }
        #endregion
    }

    #region DTOs
    public class Plattform
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public PlattformTyp Typ { get; set; }
        public string Url { get; set; } = "";
        public string? ApiKey { get; set; }
        public string? ApiSecret { get; set; }
        public string? Benutzername { get; set; }
        public string? Passwort { get; set; }
        public string? WebhookUrl { get; set; }
        public bool Aktiv { get; set; } = true;
        public int Sortierung { get; set; }
        public string Waehrung { get; set; } = "EUR";
        public bool SteuerInklusive { get; set; } = true;
        public int? StandardLagerId { get; set; }
        public bool AutoSync { get; set; } = true;
        public int SyncIntervallMin { get; set; } = 15;
        public DateTime? LetzterSync { get; set; }
        public bool LetzterSyncErfolg { get; set; }
        public string? LetzterSyncFehler { get; set; }
        public string? KonfigurationJson { get; set; }
    }

    public enum PlattformTyp
    {
        WooCommerce = 1,
        Shopify = 2,
        Magento = 3,
        Shopware = 4,
        Amazon = 10,
        eBay = 11,
        Kaufland = 12,
        Otto = 13,
        Custom = 99
    }

    public class ArtikelPlattformDaten
    {
        public int ArtikelId { get; set; }
        public int PlattformId { get; set; }
        public string? PlattformName { get; set; }
        public string? ExterneId { get; set; }
        public string? Name { get; set; }
        public string? Beschreibung { get; set; } // Plaintext
        public string? BeschreibungHtml { get; set; } // HTML
        public string? Kurztext { get; set; }
        public decimal? Preis { get; set; }
        public bool Aktiv { get; set; } = true;
        public bool LagerSync { get; set; } = true;
        public bool PreisSync { get; set; } = true;
        public string? KategorieExtern { get; set; }
        public string? AttributeJson { get; set; }
        public DateTime? Aktualisiert { get; set; }
    }

    public class ArtikelBild
    {
        public int Id { get; set; }
        public int ArtikelId { get; set; }
        public int? PlattformId { get; set; } // NULL = alle Plattformen
        public string? Pfad { get; set; } // Lokaler Pfad
        public string? Url { get; set; } // Externe URL
        public string? AltText { get; set; }
        public int Sortierung { get; set; }
        public bool IstHauptbild { get; set; }
    }
    #endregion
}

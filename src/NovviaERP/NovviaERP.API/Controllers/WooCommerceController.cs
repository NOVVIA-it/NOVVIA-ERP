using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NovviaERP.Core.Data;
using NovviaERP.Core.Entities;
using NovviaERP.Core.Services;
using System.Text.Json;

namespace NovviaERP.API.Controllers
{
    /// <summary>
    /// WooCommerce Shop-Anbindung REST API
    /// - Kann vom JTL Worker aufgerufen werden
    /// - Webhooks von WooCommerce empfangen
    /// - Shop-Synchronisation steuern
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class WooCommerceController : ControllerBase
    {
        private readonly JtlDbContext _db;
        private readonly ILogger<WooCommerceController> _logger;

        public WooCommerceController(JtlDbContext db, ILogger<WooCommerceController> logger)
        {
            _db = db;
            _logger = logger;
        }

        #region Shop-Verwaltung

        /// <summary>
        /// Alle konfigurierten Shops abrufen
        /// </summary>
        [HttpGet("shops")]
        [Authorize]
        public async Task<IActionResult> GetShops()
        {
            var shops = await _db.GetWooCommerceShopsAsync();
            return Ok(shops.Select(s => new
            {
                s.Id,
                s.Name,
                s.Url,
                s.Aktiv,
                s.WebhooksAktiv,
                s.LetzterSync,
                s.SyncIntervallMinuten
            }));
        }

        /// <summary>
        /// Shop-Verbindung testen
        /// </summary>
        [HttpPost("shops/{shopId}/test")]
        [Authorize]
        public async Task<IActionResult> TestConnection(int shopId, [FromQuery] bool testModus = false)
        {
            var shop = await _db.GetWooCommerceShopByIdAsync(shopId);
            if (shop == null) return NotFound("Shop nicht gefunden");

            using var woo = new WooCommerceService(_db, testModus);
            var ok = await woo.TestConnectionAsync(shop);

            return Ok(new
            {
                Success = ok,
                Shop = shop.Name,
                Url = shop.Url,
                ApiLog = testModus ? woo.GetApiLog() : null
            });
        }

        #endregion

        #region Synchronisation

        /// <summary>
        /// Alle Artikel zum Shop synchronisieren
        /// </summary>
        [HttpPost("shops/{shopId}/sync/artikel")]
        [Authorize]
        public async Task<IActionResult> SyncArtikel(int shopId, [FromQuery] bool testModus = false, [FromBody] List<int>? artikelIds = null)
        {
            var shop = await _db.GetWooCommerceShopByIdAsync(shopId);
            if (shop == null) return NotFound("Shop nicht gefunden");

            using var woo = new WooCommerceService(_db, testModus);
            _logger.LogInformation("WooCommerce Sync Artikel gestartet: {Shop} (TestModus: {Test})", shop.Name, testModus);

            // Artikel laden
            var artikel = artikelIds?.Count > 0
                ? await _db.GetArtikelByIdsAsync(artikelIds)
                : await _db.GetArtikelAsync(null, aktiv: true, limit: 10000);

            var result = await woo.SyncAllProductsAsync(shop, artikel);

            // LetzterSync aktualisieren
            await _db.UpdateWooCommerceSyncTimeAsync(shopId);

            _logger.LogInformation("WooCommerce Sync Artikel abgeschlossen: {Erstellt} erstellt, {Aktualisiert} aktualisiert, {Fehler} Fehler",
                result.Erstellt, result.Aktualisiert, result.Fehler);

            return Ok(new
            {
                result.Erstellt,
                result.Aktualisiert,
                result.Fehler,
                result.FehlerMeldung,
                ApiLog = testModus ? woo.GetApiLog() : null
            });
        }

        /// <summary>
        /// Nur Bestaende synchronisieren (schneller als Vollartikel)
        /// </summary>
        [HttpPost("shops/{shopId}/sync/bestaende")]
        [Authorize]
        public async Task<IActionResult> SyncBestaende(int shopId, [FromQuery] bool testModus = false)
        {
            var shop = await _db.GetWooCommerceShopByIdAsync(shopId);
            if (shop == null) return NotFound("Shop nicht gefunden");

            using var woo = new WooCommerceService(_db, testModus);
            _logger.LogInformation("WooCommerce Sync Bestaende gestartet: {Shop}", shop.Name);

            // Alle Bestaende laden
            var bestaende = await _db.GetAllBestaendeAsync();

            var result = await woo.SyncAllStocksAsync(shop, bestaende);

            _logger.LogInformation("WooCommerce Sync Bestaende: {Aktualisiert} aktualisiert, {Fehler} Fehler",
                result.Aktualisiert, result.Fehler);

            return Ok(new
            {
                result.Aktualisiert,
                result.Uebersprungen,
                result.Fehler,
                ApiLog = testModus ? woo.GetApiLog() : null
            });
        }

        /// <summary>
        /// Nur Preise synchronisieren
        /// </summary>
        [HttpPost("shops/{shopId}/sync/preise")]
        [Authorize]
        public async Task<IActionResult> SyncPreise(int shopId, [FromQuery] bool testModus = false)
        {
            var shop = await _db.GetWooCommerceShopByIdAsync(shopId);
            if (shop == null) return NotFound("Shop nicht gefunden");

            using var woo = new WooCommerceService(_db, testModus);
            _logger.LogInformation("WooCommerce Sync Preise gestartet: {Shop}", shop.Name);

            var preise = await _db.GetAllPreiseAsync();

            var result = await woo.SyncAllPricesAsync(shop, preise);

            _logger.LogInformation("WooCommerce Sync Preise: {Aktualisiert} aktualisiert, {Fehler} Fehler",
                result.Aktualisiert, result.Fehler);

            return Ok(new
            {
                result.Aktualisiert,
                result.Uebersprungen,
                result.Fehler,
                ApiLog = testModus ? woo.GetApiLog() : null
            });
        }

        /// <summary>
        /// Kategorien zum Shop synchronisieren
        /// </summary>
        [HttpPost("shops/{shopId}/sync/kategorien")]
        [Authorize]
        public async Task<IActionResult> SyncKategorien(int shopId, [FromQuery] bool testModus = false)
        {
            var shop = await _db.GetWooCommerceShopByIdAsync(shopId);
            if (shop == null) return NotFound("Shop nicht gefunden");

            using var woo = new WooCommerceService(_db, testModus);
            _logger.LogInformation("WooCommerce Sync Kategorien gestartet: {Shop}", shop.Name);

            var kategorien = await _db.GetKategorienAsync();

            var result = await woo.SyncAllCategoriesAsync(shop, kategorien);

            _logger.LogInformation("WooCommerce Sync Kategorien: {Erstellt} erstellt, {Uebersprungen} uebersprungen",
                result.Erstellt, result.Uebersprungen);

            return Ok(new
            {
                result.Erstellt,
                result.Uebersprungen,
                result.Fehler,
                result.Details,
                ApiLog = testModus ? woo.GetApiLog() : null
            });
        }

        /// <summary>
        /// Bestellungen vom Shop importieren
        /// </summary>
        [HttpPost("shops/{shopId}/sync/bestellungen")]
        [Authorize]
        public async Task<IActionResult> ImportBestellungen(int shopId, [FromQuery] bool testModus = false, [FromQuery] DateTime? seit = null)
        {
            var shop = await _db.GetWooCommerceShopByIdAsync(shopId);
            if (shop == null) return NotFound("Shop nicht gefunden");

            using var woo = new WooCommerceService(_db, testModus);
            _logger.LogInformation("WooCommerce Import Bestellungen gestartet: {Shop}", shop.Name);

            var result = await woo.ImportAllOrdersAsync(shop, seit);

            _logger.LogInformation("WooCommerce Import: {Erstellt} importiert, {Uebersprungen} uebersprungen, {Fehler} Fehler",
                result.Erstellt, result.Uebersprungen, result.Fehler);

            return Ok(new
            {
                Importiert = result.Erstellt,
                result.Uebersprungen,
                result.Fehler,
                result.Details,
                ApiLog = testModus ? woo.GetApiLog() : null
            });
        }

        /// <summary>
        /// Vollsync: Kategorien, Artikel, Bestaende, Preise, Bestellungen
        /// </summary>
        [HttpPost("shops/{shopId}/sync/full")]
        [Authorize]
        public async Task<IActionResult> FullSync(int shopId, [FromQuery] bool testModus = false)
        {
            var shop = await _db.GetWooCommerceShopByIdAsync(shopId);
            if (shop == null) return NotFound("Shop nicht gefunden");

            using var woo = new WooCommerceService(_db, testModus);
            _logger.LogInformation("WooCommerce FULL Sync gestartet: {Shop}", shop.Name);

            var results = new Dictionary<string, object>();

            // 1. Kategorien
            var kategorien = await _db.GetKategorienAsync();
            var katResult = await woo.SyncAllCategoriesAsync(shop, kategorien);
            results["Kategorien"] = new { katResult.Erstellt, katResult.Fehler };

            // 2. Artikel
            var artikel = await _db.GetArtikelAsync(null, aktiv: true, limit: 10000);
            var artResult = await woo.SyncAllProductsAsync(shop, artikel);
            results["Artikel"] = new { artResult.Erstellt, artResult.Aktualisiert, artResult.Fehler };

            // 3. Bestellungen importieren
            var ordResult = await woo.ImportAllOrdersAsync(shop);
            results["Bestellungen"] = new { Importiert = ordResult.Erstellt, ordResult.Uebersprungen, ordResult.Fehler };

            // LetzterSync aktualisieren
            await _db.UpdateWooCommerceSyncTimeAsync(shopId);

            _logger.LogInformation("WooCommerce FULL Sync abgeschlossen: {Shop}", shop.Name);

            return Ok(new
            {
                Shop = shop.Name,
                Results = results,
                ApiLog = testModus ? woo.GetApiLog() : null
            });
        }

        #endregion

        #region Webhooks

        /// <summary>
        /// Webhooks im Shop registrieren
        /// </summary>
        [HttpPost("shops/{shopId}/webhooks/setup")]
        [Authorize]
        public async Task<IActionResult> SetupWebhooks(int shopId, [FromBody] WebhookSetupRequest request)
        {
            var shop = await _db.GetWooCommerceShopByIdAsync(shopId);
            if (shop == null) return NotFound("Shop nicht gefunden");

            using var woo = new WooCommerceService(_db, true);
            var success = await woo.SetupWebhooksAsync(shop, request.CallbackBaseUrl);

            if (success)
            {
                // Webhook-Secret und URL in DB speichern
                shop.WebhookSecret = shop.WebhookSecret ?? Guid.NewGuid().ToString("N");
                shop.WebhookCallbackUrl = request.CallbackBaseUrl;
                shop.WebhooksAktiv = true;
                await _db.UpdateWooCommerceShopAsync(shop);
            }

            return Ok(new
            {
                Success = success,
                WebhooksRegistered = success ? 4 : 0,
                CallbackUrl = $"{request.CallbackBaseUrl.TrimEnd('/')}/api/woocommerce/webhook/{shopId}",
                ApiLog = woo.GetApiLog()
            });
        }

        /// <summary>
        /// Registrierte Webhooks abrufen
        /// </summary>
        [HttpGet("shops/{shopId}/webhooks")]
        [Authorize]
        public async Task<IActionResult> GetWebhooks(int shopId)
        {
            var shop = await _db.GetWooCommerceShopByIdAsync(shopId);
            if (shop == null) return NotFound("Shop nicht gefunden");

            using var woo = new WooCommerceService(_db);
            var webhooks = await woo.GetWebhooksAsync(shop);

            return Ok(webhooks.Select(w => new
            {
                w.Id,
                w.Name,
                w.Topic,
                w.DeliveryUrl,
                w.Status
            }));
        }

        /// <summary>
        /// Webhook loeschen
        /// </summary>
        [HttpDelete("shops/{shopId}/webhooks/{webhookId}")]
        [Authorize]
        public async Task<IActionResult> DeleteWebhook(int shopId, int webhookId)
        {
            var shop = await _db.GetWooCommerceShopByIdAsync(shopId);
            if (shop == null) return NotFound("Shop nicht gefunden");

            using var woo = new WooCommerceService(_db);
            var success = await woo.DeleteWebhookAsync(shop, webhookId);

            return success ? Ok() : BadRequest("Webhook konnte nicht geloescht werden");
        }

        /// <summary>
        /// Webhook-Empfaenger (wird von WooCommerce aufgerufen)
        /// </summary>
        [HttpPost("webhook/{shopId}")]
        [AllowAnonymous]
        public async Task<IActionResult> ReceiveWebhook(int shopId)
        {
            var shop = await _db.GetWooCommerceShopByIdAsync(shopId);
            if (shop == null)
            {
                _logger.LogWarning("Webhook empfangen fuer unbekannten Shop: {ShopId}", shopId);
                return NotFound();
            }

            // Payload lesen
            using var reader = new StreamReader(Request.Body);
            var payload = await reader.ReadToEndAsync();

            // Signatur validieren
            var signature = Request.Headers["X-WC-Webhook-Signature"].FirstOrDefault();
            if (!string.IsNullOrEmpty(shop.WebhookSecret) && !string.IsNullOrEmpty(signature))
            {
                if (!WooCommerceService.ValidateWebhookSignature(payload, signature, shop.WebhookSecret))
                {
                    _logger.LogWarning("Webhook Signatur ungueltig: {ShopId}", shopId);
                    return Unauthorized("Ungueltige Signatur");
                }
            }

            // Topic ermitteln
            var topic = Request.Headers["X-WC-Webhook-Topic"].FirstOrDefault() ?? "";
            var resource = Request.Headers["X-WC-Webhook-Resource"].FirstOrDefault() ?? "";
            var event_ = Request.Headers["X-WC-Webhook-Event"].FirstOrDefault() ?? "";

            _logger.LogInformation("Webhook empfangen: {Shop} - {Topic} ({Resource}.{Event})",
                shop.Name, topic, resource, event_);

            // Verarbeiten
            using var woo = new WooCommerceService(_db, true);
            var result = await woo.ProcessWebhookAsync(shop, topic, payload);

            _logger.LogInformation("Webhook verarbeitet: {Aktion} (Success: {Success})", result.Aktion, result.Success);

            return Ok(new { result.Aktion, result.Success });
        }

        #endregion

        #region Einzelprodukt-Sync (fuer Worker)

        /// <summary>
        /// Einzelnes Produkt zum Shop synchronisieren
        /// </summary>
        [HttpPost("shops/{shopId}/produkt/{artikelId}")]
        [Authorize]
        public async Task<IActionResult> SyncEinzelprodukt(int shopId, int artikelId, [FromQuery] bool testModus = false)
        {
            var shop = await _db.GetWooCommerceShopByIdAsync(shopId);
            if (shop == null) return NotFound("Shop nicht gefunden");

            var artikel = await _db.GetArtikelByIdAsync(artikelId);
            if (artikel == null) return NotFound("Artikel nicht gefunden");

            using var woo = new WooCommerceService(_db, testModus);
            _logger.LogInformation("WooCommerce Sync Einzelprodukt: {ArtNr} -> {Shop}", artikel.ArtNr, shop.Name);

            var wcId = await woo.SyncProductAsync(shop, artikel);

            return Ok(new
            {
                ArtikelId = artikelId,
                ArtNr = artikel.ArtNr,
                WooCommerceId = wcId,
                Shop = shop.Name,
                ApiLog = testModus ? woo.GetApiLog() : null
            });
        }

        /// <summary>
        /// Bestand fuer einzelnes Produkt aktualisieren
        /// </summary>
        [HttpPatch("shops/{shopId}/produkt/{artikelId}/bestand")]
        [Authorize]
        public async Task<IActionResult> UpdateBestand(int shopId, int artikelId, [FromBody] BestandUpdateRequest request)
        {
            var shop = await _db.GetWooCommerceShopByIdAsync(shopId);
            if (shop == null) return NotFound("Shop nicht gefunden");

            var artikel = await _db.GetArtikelByIdAsync(artikelId);
            if (artikel == null) return NotFound("Artikel nicht gefunden");

            // WooCommerce Produkt-ID ermitteln
            var link = artikel.WooCommerceLinks.FirstOrDefault(l => l.ShopId == shopId);
            if (link == null) return BadRequest("Artikel ist nicht mit diesem Shop verknuepft");

            using var woo = new WooCommerceService(_db, request.TestModus);
            await woo.SyncStockAsync(shop, link.WooCommerceProductId, request.Bestand);

            _logger.LogInformation("WooCommerce Bestand aktualisiert: {ArtNr} = {Bestand} ({Shop})",
                artikel.ArtNr, request.Bestand, shop.Name);

            return Ok(new
            {
                ArtikelId = artikelId,
                ArtNr = artikel.ArtNr,
                NeuerBestand = request.Bestand,
                ApiLog = request.TestModus ? woo.GetApiLog() : null
            });
        }

        #endregion

        #region Bestellstatus

        /// <summary>
        /// Bestellstatus im Shop aktualisieren
        /// </summary>
        [HttpPatch("shops/{shopId}/bestellung/{wooOrderId}/status")]
        [Authorize]
        public async Task<IActionResult> UpdateOrderStatus(int shopId, int wooOrderId, [FromBody] OrderStatusRequest request)
        {
            var shop = await _db.GetWooCommerceShopByIdAsync(shopId);
            if (shop == null) return NotFound("Shop nicht gefunden");

            using var woo = new WooCommerceService(_db, request.TestModus);
            await woo.UpdateOrderStatusAsync(shop, wooOrderId, request.Status, request.TrackingNummer);

            _logger.LogInformation("WooCommerce Bestellstatus aktualisiert: {OrderId} -> {Status} ({Shop})",
                wooOrderId, request.Status, shop.Name);

            return Ok(new
            {
                WooOrderId = wooOrderId,
                Status = request.Status,
                TrackingNummer = request.TrackingNummer,
                ApiLog = request.TestModus ? woo.GetApiLog() : null
            });
        }

        #endregion

        #region JTL-Native Sync (tArtikelShop)

        /// <summary>
        /// Holt JTL Shops aus tShop Tabelle
        /// </summary>
        [HttpGet("jtl/shops")]
        [Authorize]
        public async Task<IActionResult> GetJtlShops()
        {
            var shops = await _db.GetJtlShopsAsync();
            return Ok(shops);
        }

        /// <summary>
        /// Holt Sync-Statistik fuer einen Shop (aus tArtikelShop)
        /// </summary>
        [HttpGet("jtl/shops/{kShop}/stats")]
        [Authorize]
        public async Task<IActionResult> GetSyncStats(int kShop)
        {
            var stats = await _db.GetShopSyncStatsAsync(kShop);
            return Ok(stats);
        }

        /// <summary>
        /// Synchronisiert nur Artikel die in tArtikelShop mit nAktion > 0 markiert sind
        /// Dies ist die JTL-native Methode - nur geaenderte Artikel werden uebertragen
        /// </summary>
        [HttpPost("jtl/shops/{kShop}/sync")]
        [Authorize]
        public async Task<IActionResult> SyncPending(int kShop, [FromQuery] bool testModus = false, [FromQuery] int batchSize = 50)
        {
            var jtlShop = await _db.GetJtlShopByIdAsync(kShop);
            if (jtlShop == null) return NotFound("JTL Shop nicht gefunden");

            // Konvertiere zu WooCommerceShop fuer den Service
            var shop = new WooCommerceShop
            {
                Id = jtlShop.KShop,
                Name = jtlShop.Name,
                Url = jtlShop.Url ?? "",
                ConsumerKey = jtlShop.ConsumerKey ?? "",
                ConsumerSecret = jtlShop.ConsumerSecret ?? "",
                Aktiv = jtlShop.Aktiv
            };

            using var woo = new WooCommerceService(_db, testModus);
            var result = await woo.SyncPendingProductsAsync(shop, batchSize);

            return Ok(new
            {
                Shop = shop.Name,
                result.Erstellt,
                result.Aktualisiert,
                result.Fehler,
                result.Details,
                ApiLog = testModus ? woo.GetApiLog() : null
            });
        }

        /// <summary>
        /// Markiert alle Artikel fuer Vollsync
        /// </summary>
        [HttpPost("jtl/shops/{kShop}/trigger-full-sync")]
        [Authorize]
        public async Task<IActionResult> TriggerFullSync(int kShop)
        {
            var jtlShop = await _db.GetJtlShopByIdAsync(kShop);
            if (jtlShop == null) return NotFound("JTL Shop nicht gefunden");

            await _db.SetAlleArtikelSyncNoetigAsync(kShop);

            var stats = await _db.GetShopSyncStatsAsync(kShop);

            return Ok(new
            {
                Message = $"Vollsync getriggert fuer {jtlShop.Name}",
                ArtikelZuSyncen = stats.UpdateNoetig
            });
        }

        /// <summary>
        /// Markiert einzelnen Artikel fuer Sync
        /// </summary>
        [HttpPost("jtl/shops/{kShop}/artikel/{kArtikel}/trigger")]
        [Authorize]
        public async Task<IActionResult> TriggerArtikelSync(int kShop, int kArtikel)
        {
            await _db.SetArtikelSyncNoetigAsync(kShop, kArtikel, 1);
            return Ok(new { Message = $"Artikel {kArtikel} fuer Sync markiert" });
        }

        /// <summary>
        /// Holt Artikel die synchronisiert werden muessen
        /// </summary>
        [HttpGet("jtl/shops/{kShop}/pending")]
        [Authorize]
        public async Task<IActionResult> GetPendingArticles(int kShop, [FromQuery] int limit = 100)
        {
            var pending = await _db.GetArtikelZuSyncenAsync(kShop, limit);
            return Ok(pending);
        }

        /// <summary>
        /// Holt Zahlungsabgleich-Transaktionen aus tZahlungsabgleichUmsatz
        /// </summary>
        [HttpGet("jtl/zahlungen")]
        [Authorize]
        public async Task<IActionResult> GetZahlungsabgleichUmsaetze([FromQuery] int? kModul = null, [FromQuery] int limit = 100)
        {
            var umsaetze = await _db.GetZahlungsabgleichUmsaetzeAsync(kModul, limit);
            return Ok(umsaetze);
        }

        #endregion

        #region DTOs

        public record WebhookSetupRequest(string CallbackBaseUrl);

        public record BestandUpdateRequest(int Bestand, bool TestModus = false);

        public record OrderStatusRequest(string Status, string? TrackingNummer = null, bool TestModus = false);

        #endregion
    }
}

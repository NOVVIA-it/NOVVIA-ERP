using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using NovviaERP.Core.Data;
using NovviaERP.Core.Entities;
using Serilog;

namespace NovviaERP.Core.Services
{
    /// <summary>
    /// WooCommerce Shop-Anbindung fuer NovviaERP
    /// - Kategorien, Artikel, Preise, Bilder, Bestaende zum Shop synchronisieren
    /// - Bestellungen und Zahlungen vom Shop importieren
    /// - Webhooks fuer Echtzeit-Updates
    /// </summary>
    public class WooCommerceService : IDisposable
    {
        private readonly JtlDbContext _db;
        private readonly HttpClient _http;
        private static readonly ILogger _log = Log.ForContext<WooCommerceService>();

        private bool _testModus;
        private readonly List<ApiLogEntry> _apiLog = new();

        public WooCommerceService(JtlDbContext db, bool testModus = false)
        {
            _db = db;
            _testModus = testModus;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        }

        public void Dispose() => _http.Dispose();

        public void SetTestModus(bool aktiv) => _testModus = aktiv;
        public List<ApiLogEntry> GetApiLog() => _apiLog.ToList();
        public void ClearApiLog() => _apiLog.Clear();

        public class ApiLogEntry
        {
            public DateTime Zeitpunkt { get; set; } = DateTime.Now;
            public string Methode { get; set; } = "";
            public string Url { get; set; } = "";
            public string? RequestBody { get; set; }
            public int StatusCode { get; set; }
            public string? ResponseBody { get; set; }
            public long DauerMs { get; set; }
            public string? Fehler { get; set; }
        }

        private async Task<HttpResponseMessage> SendWithLoggingAsync(HttpMethod method, string url, object? body = null)
        {
            var entry = new ApiLogEntry { Methode = method.Method, Url = url };
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                HttpResponseMessage response;
                if (body != null)
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(body);
                    entry.RequestBody = json;

                    if (_testModus)
                        _log.Debug("WooCommerce API {Method} {Url}\nRequest: {Body}", method.Method, url, json);

                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var request = new HttpRequestMessage(method, url) { Content = content };
                    response = await _http.SendAsync(request);
                }
                else
                {
                    if (_testModus)
                        _log.Debug("WooCommerce API {Method} {Url}", method.Method, url);

                    var request = new HttpRequestMessage(method, url);
                    response = await _http.SendAsync(request);
                }

                sw.Stop();
                entry.StatusCode = (int)response.StatusCode;
                entry.DauerMs = sw.ElapsedMilliseconds;

                if (_testModus)
                {
                    entry.ResponseBody = await response.Content.ReadAsStringAsync();
                    _log.Debug("WooCommerce Response {Status} ({Ms}ms)\n{Body}",
                        response.StatusCode, sw.ElapsedMilliseconds,
                        entry.ResponseBody.Length > 2000 ? entry.ResponseBody[..2000] + "..." : entry.ResponseBody);
                }

                _apiLog.Add(entry);
                return response;
            }
            catch (Exception ex)
            {
                sw.Stop();
                entry.DauerMs = sw.ElapsedMilliseconds;
                entry.Fehler = ex.Message;
                entry.StatusCode = 0;
                _apiLog.Add(entry);

                if (_testModus)
                    _log.Error(ex, "WooCommerce API Fehler: {Method} {Url}", method.Method, url);

                throw;
            }
        }

        #region Sync Results
        public class SyncResult
        {
            public string Typ { get; set; } = "";
            public int Erstellt { get; set; }
            public int Aktualisiert { get; set; }
            public int Fehler { get; set; }
            public int Uebersprungen { get; set; }
            public string? FehlerMeldung { get; set; }
            public List<string> Details { get; set; } = new();
        }
        #endregion

        private void SetAuth(WooCommerceShop shop)
        {
            _http.DefaultRequestHeaders.Clear();
            _http.DefaultRequestHeaders.Add("Authorization", $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{shop.ConsumerKey}:{shop.ConsumerSecret}"))}");
        }

        #region Products
        public async Task<int> SyncProductAsync(WooCommerceShop shop, Artikel artikel)
        {
            SetAuth(shop);
            var link = artikel.WooCommerceLinks.FirstOrDefault(l => l.ShopId == shop.Id);
            var data = new Dictionary<string, object>
            {
                ["name"] = artikel.Beschreibung?.Name ?? artikel.ArtNr,
                ["sku"] = artikel.ArtNr,
                ["regular_price"] = artikel.VKBrutto.ToString("F2"),
                ["description"] = artikel.Beschreibung?.Beschreibung ?? "",
                ["short_description"] = artikel.Beschreibung?.KurzBeschreibung ?? "",
                ["manage_stock"] = true,
                ["stock_quantity"] = (int)artikel.Lagerbestand,
                ["status"] = artikel.Aktiv == "Y" ? "publish" : "draft",
                ["weight"] = (artikel.Gewicht ?? 0).ToString("F2")
            };
            if (artikel.Kategorien.Any())
                data["categories"] = artikel.Kategorien.Select(k => new { id = k.KategorieId }).ToArray();
            if (artikel.Bilder.Any())
                data["images"] = artikel.Bilder.OrderBy(b => b.Nummer).Select(b => new { src = b.Pfad }).ToArray();
            if (artikel.Merkmale.Any())
                data["attributes"] = artikel.Merkmale.GroupBy(m => m.MerkmalName).Select(g => new { name = g.Key, options = g.Select(m => m.WertName).ToArray(), visible = true }).ToArray();

            HttpResponseMessage response;
            if (link != null && link.WooCommerceProductId > 0)
                response = await _http.PutAsJsonAsync($"{shop.Url}/wp-json/wc/v3/products/{link.WooCommerceProductId}", data);
            else
                response = await _http.PostAsJsonAsync($"{shop.Url}/wp-json/wc/v3/products", data);

            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            var wcId = json.GetProperty("id").GetInt32();
            _log.Information("WooCommerce Sync: {ArtNr} -> {WcId} ({Shop})", artikel.ArtNr, wcId, shop.Name);
            return wcId;
        }

        public async Task SyncStockAsync(WooCommerceShop shop, int wcProductId, int stock)
        {
            SetAuth(shop);
            await _http.PutAsJsonAsync($"{shop.Url}/wp-json/wc/v3/products/{wcProductId}", new { stock_quantity = stock });
        }

        public async Task<int> BatchSyncProductsAsync(WooCommerceShop shop, List<Artikel> artikel)
        {
            SetAuth(shop);
            var create = new List<object>();
            var update = new List<object>();
            foreach (var a in artikel)
            {
                var link = a.WooCommerceLinks.FirstOrDefault(l => l.ShopId == shop.Id);
                var data = new Dictionary<string, object>
                {
                    ["sku"] = a.ArtNr, ["name"] = a.Beschreibung?.Name ?? a.ArtNr, ["regular_price"] = a.VKBrutto.ToString("F2"),
                    ["manage_stock"] = true, ["stock_quantity"] = (int)a.Lagerbestand, ["status"] = a.Aktiv == "Y" ? "publish" : "draft"
                };
                if (link != null && link.WooCommerceProductId > 0) { data["id"] = link.WooCommerceProductId; update.Add(data); }
                else create.Add(data);
            }
            var batch = new { create, update };
            var response = await _http.PostAsJsonAsync($"{shop.Url}/wp-json/wc/v3/products/batch", batch);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            int count = 0;
            if (json.TryGetProperty("create", out var c)) count += c.GetArrayLength();
            if (json.TryGetProperty("update", out var u)) count += u.GetArrayLength();
            _log.Information("WooCommerce Batch: {Count} Produkte synchronisiert ({Shop})", count, shop.Name);
            return count;
        }
        #endregion

        #region Orders
        public async Task<List<WooOrder>> GetOrdersAsync(WooCommerceShop shop, string status = "processing", int limit = 100)
        {
            SetAuth(shop);
            var orders = new List<WooOrder>();
            var response = await _http.GetFromJsonAsync<JsonElement>($"{shop.Url}/wp-json/wc/v3/orders?status={status}&per_page={limit}");
            foreach (var o in response.EnumerateArray())
            {
                var order = new WooOrder
                {
                    Id = o.GetProperty("id").GetInt32(),
                    Number = o.GetProperty("number").GetString() ?? "",
                    Status = o.GetProperty("status").GetString() ?? "",
                    Total = decimal.Parse(o.GetProperty("total").GetString() ?? "0"),
                    Currency = o.GetProperty("currency").GetString() ?? "EUR",
                    DateCreated = DateTime.Parse(o.GetProperty("date_created").GetString() ?? DateTime.Now.ToString()),
                    PaymentMethod = o.TryGetProperty("payment_method_title", out var pm) ? pm.GetString() : null,
                    CustomerNote = o.TryGetProperty("customer_note", out var cn) ? cn.GetString() : null
                };
                if (o.TryGetProperty("billing", out var b))
                {
                    order.BillingFirstName = b.GetProperty("first_name").GetString();
                    order.BillingLastName = b.GetProperty("last_name").GetString();
                    order.BillingCompany = b.TryGetProperty("company", out var bc) ? bc.GetString() : null;
                    order.BillingAddress = b.GetProperty("address_1").GetString();
                    order.BillingCity = b.GetProperty("city").GetString();
                    order.BillingPostcode = b.GetProperty("postcode").GetString();
                    order.BillingCountry = b.GetProperty("country").GetString();
                    order.BillingEmail = b.GetProperty("email").GetString();
                    order.BillingPhone = b.TryGetProperty("phone", out var bp) ? bp.GetString() : null;
                }
                if (o.TryGetProperty("shipping", out var s))
                {
                    order.ShippingFirstName = s.GetProperty("first_name").GetString();
                    order.ShippingLastName = s.GetProperty("last_name").GetString();
                    order.ShippingCompany = s.TryGetProperty("company", out var sc) ? sc.GetString() : null;
                    order.ShippingAddress = s.GetProperty("address_1").GetString();
                    order.ShippingCity = s.GetProperty("city").GetString();
                    order.ShippingPostcode = s.GetProperty("postcode").GetString();
                    order.ShippingCountry = s.GetProperty("country").GetString();
                }
                if (o.TryGetProperty("line_items", out var items))
                    foreach (var i in items.EnumerateArray())
                        order.Items.Add(new WooOrderItem
                        {
                            ProductId = i.GetProperty("product_id").GetInt32(),
                            Sku = i.TryGetProperty("sku", out var sku) ? sku.GetString() ?? "" : "",
                            Name = i.GetProperty("name").GetString() ?? "",
                            Quantity = i.GetProperty("quantity").GetInt32(),
                            Total = decimal.Parse(i.GetProperty("total").GetString() ?? "0")
                        });
                orders.Add(order);
            }
            return orders;
        }

        public async Task UpdateOrderStatusAsync(WooCommerceShop shop, int orderId, string status, string? trackingNumber = null)
        {
            SetAuth(shop);
            var data = new Dictionary<string, object> { ["status"] = status };
            if (!string.IsNullOrEmpty(trackingNumber))
                data["meta_data"] = new[] { new { key = "_tracking_number", value = trackingNumber } };
            await _http.PutAsJsonAsync($"{shop.Url}/wp-json/wc/v3/orders/{orderId}", data);
            _log.Information("WooCommerce Order {Id} -> Status: {Status}", orderId, status);
        }

        public async Task<Bestellung> ImportOrderAsync(WooCommerceShop shop, WooOrder wooOrder)
        {
            var conn = await _db.GetConnectionAsync();
            // Kunde finden oder anlegen
            var kunde = (await _db.GetKundenAsync(wooOrder.BillingEmail)).FirstOrDefault();
            if (kunde == null)
            {
                var kundeId = await _db.CreateKundeAsync(new Kunde
                {
                    Vorname = wooOrder.BillingFirstName,
                    Nachname = wooOrder.BillingLastName ?? "Unbekannt",
                    Firma = wooOrder.BillingCompany,
                    Strasse = wooOrder.BillingAddress,
                    PLZ = wooOrder.BillingPostcode,
                    Ort = wooOrder.BillingCity,
                    Land = wooOrder.BillingCountry ?? "DE",
                    Email = wooOrder.BillingEmail,
                    Telefon = wooOrder.BillingPhone
                });
                kunde = await _db.GetKundeByIdAsync(kundeId, false);
            }
            var bestellung = new Bestellung
            {
                KundeId = kunde!.Id,
                ExterneAuftragsnummer = wooOrder.Number,
                Platform = shop.Id,
                PlattformName = shop.Name,
                GesamtBrutto = wooOrder.Total,
                GesamtNetto = wooOrder.Total / 1.19m,
                Waehrung = wooOrder.Currency,
                Kommentar = wooOrder.CustomerNote,
                Status = (int)BestellStatus.Offen,
                Lieferadresse = new BestellAdresse
                {
                    Vorname = wooOrder.ShippingFirstName, Nachname = wooOrder.ShippingLastName,
                    Firma = wooOrder.ShippingCompany, Strasse = wooOrder.ShippingAddress,
                    PLZ = wooOrder.ShippingPostcode, Ort = wooOrder.ShippingCity, Land = wooOrder.ShippingCountry ?? "DE"
                },
                Rechnungsadresse = new BestellAdresse
                {
                    Vorname = wooOrder.BillingFirstName, Nachname = wooOrder.BillingLastName,
                    Firma = wooOrder.BillingCompany, Strasse = wooOrder.BillingAddress,
                    PLZ = wooOrder.BillingPostcode, Ort = wooOrder.BillingCity, Land = wooOrder.BillingCountry ?? "DE",
                    Email = wooOrder.BillingEmail, Telefon = wooOrder.BillingPhone
                }
            };
            foreach (var item in wooOrder.Items)
            {
                var artikel = await _db.GetArtikelByBarcodeAsync(item.Sku);
                bestellung.Positionen.Add(new BestellPosition
                {
                    ArtikelId = artikel?.Id, ArtNr = item.Sku, Name = item.Name,
                    Menge = item.Quantity, VKBrutto = item.Total / item.Quantity, VKNetto = item.Total / item.Quantity / 1.19m, MwSt = 19
                });
            }
            var bestellungId = await _db.CreateBestellungAsync(bestellung);
            bestellung.Id = bestellungId;
            _log.Information("WooCommerce Order {WcNr} importiert als Bestellung {Nr}", wooOrder.Number, bestellung.BestellNr);
            return bestellung;
        }
        #endregion

        #region Categories
        public async Task<List<(int WcId, string Name)>> GetCategoriesAsync(WooCommerceShop shop)
        {
            SetAuth(shop);
            var cats = new List<(int, string)>();
            var response = await _http.GetFromJsonAsync<JsonElement>($"{shop.Url}/wp-json/wc/v3/products/categories?per_page=100");
            foreach (var c in response.EnumerateArray())
                cats.Add((c.GetProperty("id").GetInt32(), c.GetProperty("name").GetString() ?? ""));
            return cats;
        }

        public async Task<int> CreateCategoryAsync(WooCommerceShop shop, string name, int? parentId = null)
        {
            SetAuth(shop);
            var data = new Dictionary<string, object> { ["name"] = name };
            if (parentId.HasValue) data["parent"] = parentId.Value;
            var response = await _http.PostAsJsonAsync($"{shop.Url}/wp-json/wc/v3/products/categories", data);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            return json.GetProperty("id").GetInt32();
        }

        public async Task<SyncResult> SyncAllCategoriesAsync(WooCommerceShop shop, List<JtlDbContext.KategorieSync> kategorien)
        {
            SetAuth(shop);
            var result = new SyncResult { Typ = "Kategorien" };
            var existing = await GetCategoriesAsync(shop);
            var existingByName = existing.ToDictionary(c => c.Name.ToLower(), c => c.WcId);

            // Mapping von JTL Kategorie ID zu Name fuer Parent-Lookup
            var kategorieIdToName = kategorien.ToDictionary(k => k.Id, k => k.Name);

            foreach (var kat in kategorien.OrderBy(k => k.Ebene))
            {
                try
                {
                    if (existingByName.ContainsKey(kat.Name.ToLower()))
                    {
                        result.Uebersprungen++;
                        continue;
                    }
                    int? parentWcId = null;
                    if (kat.ParentId.HasValue && kategorieIdToName.TryGetValue(kat.ParentId.Value, out var parentName))
                    {
                        parentWcId = existingByName.GetValueOrDefault(parentName.ToLower());
                    }
                    var wcId = await CreateCategoryAsync(shop, kat.Name, parentWcId);
                    existingByName[kat.Name.ToLower()] = wcId;
                    result.Erstellt++;
                }
                catch (Exception ex)
                {
                    result.Fehler++;
                    result.Details.Add($"{kat.Name}: {ex.Message}");
                }
            }
            return result;
        }
        #endregion

        #region Webhooks
        public class WooWebhook
        {
            [JsonPropertyName("id")] public int Id { get; set; }
            [JsonPropertyName("name")] public string Name { get; set; } = "";
            [JsonPropertyName("topic")] public string Topic { get; set; } = "";
            [JsonPropertyName("delivery_url")] public string DeliveryUrl { get; set; } = "";
            [JsonPropertyName("secret")] public string Secret { get; set; } = "";
            [JsonPropertyName("status")] public string Status { get; set; } = "active";
        }

        /// <summary>
        /// Registriert Webhooks fuer Echtzeit-Updates im Shop
        /// </summary>
        public async Task<bool> SetupWebhooksAsync(WooCommerceShop shop, string callbackBaseUrl)
        {
            SetAuth(shop);
            try
            {
                var webhookSecret = shop.WebhookSecret ?? Guid.NewGuid().ToString("N");
                var webhooks = new[]
                {
                    new { name = "NovviaERP - Order Created", topic = "order.created" },
                    new { name = "NovviaERP - Order Updated", topic = "order.updated" },
                    new { name = "NovviaERP - Product Updated", topic = "product.updated" },
                    new { name = "NovviaERP - Product Deleted", topic = "product.deleted" }
                };

                foreach (var wh in webhooks)
                {
                    var data = new
                    {
                        name = wh.name,
                        topic = wh.topic,
                        delivery_url = $"{callbackBaseUrl.TrimEnd('/')}/api/woocommerce/webhook/{shop.Id}",
                        secret = webhookSecret,
                        status = "active"
                    };
                    await _http.PostAsJsonAsync($"{shop.Url}/wp-json/wc/v3/webhooks", data);
                    _log.Information("Webhook erstellt: {Topic} -> {Url}", wh.topic, data.delivery_url);
                }
                return true;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Fehler beim Erstellen der Webhooks");
                return false;
            }
        }

        /// <summary>
        /// Listet alle registrierten Webhooks
        /// </summary>
        public async Task<List<WooWebhook>> GetWebhooksAsync(WooCommerceShop shop)
        {
            SetAuth(shop);
            try
            {
                var response = await _http.GetFromJsonAsync<List<WooWebhook>>($"{shop.Url}/wp-json/wc/v3/webhooks");
                return response ?? new List<WooWebhook>();
            }
            catch
            {
                return new List<WooWebhook>();
            }
        }

        /// <summary>
        /// Loescht einen Webhook
        /// </summary>
        public async Task<bool> DeleteWebhookAsync(WooCommerceShop shop, int webhookId)
        {
            SetAuth(shop);
            try
            {
                var response = await _http.DeleteAsync($"{shop.Url}/wp-json/wc/v3/webhooks/{webhookId}?force=true");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validiert eine Webhook-Signatur
        /// </summary>
        public static bool ValidateWebhookSignature(string payload, string signature, string secret)
        {
            if (string.IsNullOrEmpty(secret)) return false;
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var computed = Convert.ToBase64String(hash);
            return computed == signature;
        }

        /// <summary>
        /// Verarbeitet eingehenden Webhook
        /// </summary>
        public async Task<WebhookProcessResult> ProcessWebhookAsync(WooCommerceShop shop, string topic, string payload)
        {
            var result = new WebhookProcessResult { Topic = topic };
            try
            {
                switch (topic)
                {
                    case "order.created":
                    case "order.updated":
                        var orderJson = JsonSerializer.Deserialize<JsonElement>(payload);
                        var orderId = orderJson.GetProperty("id").GetInt32();
                        var orderNumber = orderJson.GetProperty("number").GetString() ?? "";
                        var status = orderJson.GetProperty("status").GetString() ?? "";

                        // Pruefen ob bereits importiert
                        var existing = await _db.GetBestellungByExternerNrAsync(orderNumber);
                        if (existing != null)
                        {
                            result.Aktion = "Status aktualisiert";
                            result.Success = true;
                        }
                        else if (status == "processing" || status == "completed" || status == "on-hold")
                        {
                            // Neue Bestellung importieren
                            var orders = await GetOrdersAsync(shop, status, 1);
                            var order = orders.FirstOrDefault(o => o.Id == orderId);
                            if (order != null)
                            {
                                var bestellung = await ImportOrderAsync(shop, order);
                                result.Aktion = $"Bestellung {bestellung.BestellNr} importiert";
                                result.Success = true;
                            }
                        }
                        else
                        {
                            result.Aktion = $"Status '{status}' ignoriert";
                            result.Success = true;
                        }
                        break;

                    case "product.updated":
                        result.Aktion = "Produkt-Update empfangen (keine Aktion)";
                        result.Success = true;
                        break;

                    case "product.deleted":
                        result.Aktion = "Produkt geloescht (keine Aktion)";
                        result.Success = true;
                        break;

                    default:
                        result.Aktion = $"Unbekanntes Topic: {topic}";
                        result.Success = false;
                        break;
                }
            }
            catch (Exception ex)
            {
                result.Aktion = $"Fehler: {ex.Message}";
                result.Success = false;
                _log.Error(ex, "Webhook-Verarbeitung fehlgeschlagen: {Topic}", topic);
            }
            return result;
        }

        public class WebhookProcessResult
        {
            public string Topic { get; set; } = "";
            public string Aktion { get; set; } = "";
            public bool Success { get; set; }
        }
        #endregion

        #region Full Sync
        /// <summary>
        /// Synchronisiert alle Artikel zum Shop
        /// </summary>
        public async Task<SyncResult> SyncAllProductsAsync(WooCommerceShop shop, List<Artikel> artikel)
        {
            var result = new SyncResult { Typ = "Produkte" };
            SetAuth(shop);

            try
            {
                // Batch-Sync fuer bessere Performance
                const int batchSize = 100;
                for (int i = 0; i < artikel.Count; i += batchSize)
                {
                    var batch = artikel.Skip(i).Take(batchSize).ToList();
                    var synced = await BatchSyncProductsAsync(shop, batch);
                    result.Aktualisiert += synced;
                }
                _log.Information("WooCommerce Full Sync: {Count} Produkte", result.Aktualisiert);
            }
            catch (Exception ex)
            {
                result.FehlerMeldung = ex.Message;
                _log.Error(ex, "WooCommerce Full Sync fehlgeschlagen");
            }

            return result;
        }

        /// <summary>
        /// Synchronisiert nur Bestaende zum Shop
        /// </summary>
        public async Task<SyncResult> SyncAllStocksAsync(WooCommerceShop shop, Dictionary<string, int> bestaende)
        {
            var result = new SyncResult { Typ = "Bestaende" };
            SetAuth(shop);

            try
            {
                // Produkte aus Shop laden
                var products = await GetAllProductsAsync(shop);
                var productBySku = products.ToDictionary(p => p.Sku, p => p.Id);

                foreach (var kvp in bestaende)
                {
                    if (!productBySku.TryGetValue(kvp.Key, out var wcId))
                    {
                        result.Uebersprungen++;
                        continue;
                    }

                    try
                    {
                        await SyncStockAsync(shop, wcId, kvp.Value);
                        result.Aktualisiert++;
                    }
                    catch
                    {
                        result.Fehler++;
                    }
                }
            }
            catch (Exception ex)
            {
                result.FehlerMeldung = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Synchronisiert nur Preise zum Shop
        /// </summary>
        public async Task<SyncResult> SyncAllPricesAsync(WooCommerceShop shop, Dictionary<string, decimal> preise)
        {
            var result = new SyncResult { Typ = "Preise" };
            SetAuth(shop);

            try
            {
                var products = await GetAllProductsAsync(shop);
                var productBySku = products.ToDictionary(p => p.Sku, p => p.Id);

                foreach (var kvp in preise)
                {
                    if (!productBySku.TryGetValue(kvp.Key, out var wcId))
                    {
                        result.Uebersprungen++;
                        continue;
                    }

                    try
                    {
                        await _http.PutAsJsonAsync($"{shop.Url}/wp-json/wc/v3/products/{wcId}",
                            new { regular_price = kvp.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) });
                        result.Aktualisiert++;
                    }
                    catch
                    {
                        result.Fehler++;
                    }
                }
            }
            catch (Exception ex)
            {
                result.FehlerMeldung = ex.Message;
            }

            return result;
        }

        private class WcProductBasic { public int Id { get; set; } public string Sku { get; set; } = ""; }

        private async Task<List<WcProductBasic>> GetAllProductsAsync(WooCommerceShop shop)
        {
            var products = new List<WcProductBasic>();
            int page = 1;

            while (true)
            {
                var response = await _http.GetFromJsonAsync<JsonElement>(
                    $"{shop.Url}/wp-json/wc/v3/products?per_page=100&page={page}");

                if (response.ValueKind != JsonValueKind.Array || response.GetArrayLength() == 0)
                    break;

                foreach (var p in response.EnumerateArray())
                {
                    products.Add(new WcProductBasic
                    {
                        Id = p.GetProperty("id").GetInt32(),
                        Sku = p.TryGetProperty("sku", out var sku) ? sku.GetString() ?? "" : ""
                    });
                }
                page++;
            }

            return products;
        }

        /// <summary>
        /// Importiert alle offenen Bestellungen
        /// </summary>
        public async Task<SyncResult> ImportAllOrdersAsync(WooCommerceShop shop, DateTime? seit = null)
        {
            var result = new SyncResult { Typ = "Bestellungen" };

            try
            {
                var statuses = new[] { "processing", "on-hold", "completed" };
                foreach (var status in statuses)
                {
                    var orders = await GetOrdersAsync(shop, status, 100);
                    foreach (var order in orders)
                    {
                        // Pruefen ob bereits importiert
                        var existing = await _db.GetBestellungByExternerNrAsync(order.Number);
                        if (existing != null)
                        {
                            result.Uebersprungen++;
                            continue;
                        }

                        if (seit.HasValue && order.DateCreated < seit.Value)
                        {
                            result.Uebersprungen++;
                            continue;
                        }

                        try
                        {
                            await ImportOrderAsync(shop, order);
                            result.Erstellt++;
                        }
                        catch (Exception ex)
                        {
                            result.Fehler++;
                            result.Details.Add($"Order {order.Number}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.FehlerMeldung = ex.Message;
            }

            return result;
        }
        #endregion

        #region Connection Test
        public async Task<bool> TestConnectionAsync(WooCommerceShop shop)
        {
            SetAuth(shop);
            try
            {
                var response = await _http.GetAsync($"{shop.Url}/wp-json/wc/v3/system_status");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
        #endregion
    }

    public class WooOrder
    {
        public int Id { get; set; }
        public string Number { get; set; } = "";
        public string Status { get; set; } = "";
        public decimal Total { get; set; }
        public string Currency { get; set; } = "EUR";
        public DateTime DateCreated { get; set; }
        public string? PaymentMethod { get; set; }
        public string? CustomerNote { get; set; }
        public string? BillingFirstName { get; set; }
        public string? BillingLastName { get; set; }
        public string? BillingCompany { get; set; }
        public string? BillingAddress { get; set; }
        public string? BillingCity { get; set; }
        public string? BillingPostcode { get; set; }
        public string? BillingCountry { get; set; }
        public string? BillingEmail { get; set; }
        public string? BillingPhone { get; set; }
        public string? ShippingFirstName { get; set; }
        public string? ShippingLastName { get; set; }
        public string? ShippingCompany { get; set; }
        public string? ShippingAddress { get; set; }
        public string? ShippingCity { get; set; }
        public string? ShippingPostcode { get; set; }
        public string? ShippingCountry { get; set; }
        public List<WooOrderItem> Items { get; set; } = new();
    }

    public class WooOrderItem { public int ProductId { get; set; } public string Sku { get; set; } = ""; public string Name { get; set; } = ""; public int Quantity { get; set; } public decimal Total { get; set; } }
}

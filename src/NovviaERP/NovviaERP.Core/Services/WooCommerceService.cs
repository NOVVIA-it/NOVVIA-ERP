using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using NovviaERP.Core.Data;
using NovviaERP.Core.Entities;
using Serilog;

namespace NovviaERP.Core.Services
{
    public class WooCommerceService : IDisposable
    {
        private readonly JtlDbContext _db;
        private readonly HttpClient _http;
        private static readonly ILogger _log = Log.ForContext<WooCommerceService>();

        public WooCommerceService(JtlDbContext db) { _db = db; _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) }; }
        public void Dispose() => _http.Dispose();

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

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;

namespace NovviaERP.Core.Services
{
    public class ShippingService : IDisposable
    {
        private readonly ShippingConfig _config;
        private readonly HttpClient _http;
        private static readonly ILogger _log = Log.ForContext<ShippingService>();

        public ShippingService(ShippingConfig config) { _config = config; _http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) }; }
        public void Dispose() => _http.Dispose();

        #region DHL
        public async Task<ShipmentResult> CreateDHLShipmentAsync(ShipmentRequest req)
        {
            try
            {
                _http.DefaultRequestHeaders.Clear();
                _http.DefaultRequestHeaders.Add("Authorization", $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_config.DHLUser}:{_config.DHLPassword}"))}");
                var payload = new
                {
                    profile = _config.DHLProfile,
                    shipments = new[] { new {
                        product = "V01PAK",
                        billingNumber = _config.DHLBillingNumber,
                        refNo = req.Referenz,
                        shipper = new { name1 = _config.AbsenderName, addressStreet = _config.AbsenderStrasse, postalCode = _config.AbsenderPLZ, city = _config.AbsenderOrt, country = "DEU", email = _config.AbsenderEmail },
                        consignee = new { name1 = req.EmpfaengerName, addressStreet = req.EmpfaengerStrasse, postalCode = req.EmpfaengerPLZ, city = req.EmpfaengerOrt, country = req.EmpfaengerLand ?? "DEU", email = req.EmpfaengerEmail },
                        details = new { weight = new { uom = "kg", value = req.GewichtKg } }
                    }}
                };
                var response = await _http.PostAsJsonAsync("https://api-eu.dhl.com/parcel/de/shipping/v2/orders", payload);
                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                if (json.TryGetProperty("items", out var items) && items.GetArrayLength() > 0)
                {
                    var item = items[0];
                    return new ShipmentResult
                    {
                        Success = true,
                        TrackingNumber = item.GetProperty("shipmentNo").GetString() ?? "",
                        LabelPdf = item.TryGetProperty("label", out var lbl) ? Convert.FromBase64String(lbl.GetProperty("b64").GetString() ?? "") : null,
                        Carrier = "DHL"
                    };
                }
                return new ShipmentResult { Success = false, Error = json.ToString() };
            }
            catch (Exception ex) { _log.Error(ex, "DHL Fehler"); return new ShipmentResult { Success = false, Error = ex.Message }; }
        }
        #endregion

        #region DPD
        public async Task<ShipmentResult> CreateDPDShipmentAsync(ShipmentRequest req)
        {
            try
            {
                _http.DefaultRequestHeaders.Clear();
                _http.DefaultRequestHeaders.Add("Authorization", $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_config.DPDUser}:{_config.DPDPassword}"))}");
                var payload = new
                {
                    orders = new[] { new {
                        sendingDepot = _config.DPDDepot,
                        product = "CL",
                        orderType = "consignment",
                        sender = new { name1 = _config.AbsenderName, street = _config.AbsenderStrasse, zipCode = _config.AbsenderPLZ, city = _config.AbsenderOrt, country = "DE" },
                        recipient = new { name1 = req.EmpfaengerName, street = req.EmpfaengerStrasse, zipCode = req.EmpfaengerPLZ, city = req.EmpfaengerOrt, country = req.EmpfaengerLand ?? "DE" },
                        parcels = new[] { new { weight = req.GewichtKg, customerReference1 = req.Referenz } }
                    }}
                };
                var response = await _http.PostAsJsonAsync("https://api.dpd.de/rest/v1/orders", payload);
                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                if (json.TryGetProperty("shipmentData", out var data) && data.GetArrayLength() > 0)
                {
                    var item = data[0];
                    return new ShipmentResult
                    {
                        Success = true,
                        TrackingNumber = item.GetProperty("parcelNo").GetString() ?? "",
                        LabelPdf = item.TryGetProperty("label", out var lbl) ? Convert.FromBase64String(lbl.GetString() ?? "") : null,
                        Carrier = "DPD"
                    };
                }
                return new ShipmentResult { Success = false, Error = json.ToString() };
            }
            catch (Exception ex) { _log.Error(ex, "DPD Fehler"); return new ShipmentResult { Success = false, Error = ex.Message }; }
        }
        #endregion

        #region GLS
        public async Task<ShipmentResult> CreateGLSShipmentAsync(ShipmentRequest req)
        {
            try
            {
                _http.DefaultRequestHeaders.Clear();
                _http.DefaultRequestHeaders.Add("Authorization", $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_config.GLSUser}:{_config.GLSPassword}"))}");
                var payload = new
                {
                    shipperId = _config.GLSShipperId,
                    references = new[] { req.Referenz },
                    addresses = new {
                        delivery = new { name1 = req.EmpfaengerName, street1 = req.EmpfaengerStrasse, zipCode = req.EmpfaengerPLZ, city = req.EmpfaengerOrt, countryCode = req.EmpfaengerLand ?? "DE" }
                    },
                    parcels = new[] { new { weight = req.GewichtKg } }
                };
                var response = await _http.PostAsJsonAsync("https://api.gls-group.eu/public/v1/shipments", payload);
                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                if (json.TryGetProperty("parcels", out var parcels) && parcels.GetArrayLength() > 0)
                {
                    return new ShipmentResult
                    {
                        Success = true,
                        TrackingNumber = parcels[0].GetProperty("trackId").GetString() ?? "",
                        LabelPdf = json.TryGetProperty("labels", out var lbls) ? Convert.FromBase64String(lbls.GetString() ?? "") : null,
                        Carrier = "GLS"
                    };
                }
                return new ShipmentResult { Success = false, Error = json.ToString() };
            }
            catch (Exception ex) { _log.Error(ex, "GLS Fehler"); return new ShipmentResult { Success = false, Error = ex.Message }; }
        }
        #endregion

        #region UPS
        public async Task<ShipmentResult> CreateUPSShipmentAsync(ShipmentRequest req)
        {
            try
            {
                _http.DefaultRequestHeaders.Clear();
                _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.UPSToken}");
                _http.DefaultRequestHeaders.Add("transId", Guid.NewGuid().ToString());
                var payload = new
                {
                    ShipmentRequest = new {
                        Shipment = new {
                            Shipper = new { Name = _config.AbsenderName, ShipperNumber = _config.UPSAccountNumber, Address = new { AddressLine = _config.AbsenderStrasse, City = _config.AbsenderOrt, PostalCode = _config.AbsenderPLZ, CountryCode = "DE" } },
                            ShipTo = new { Name = req.EmpfaengerName, Address = new { AddressLine = req.EmpfaengerStrasse, City = req.EmpfaengerOrt, PostalCode = req.EmpfaengerPLZ, CountryCode = req.EmpfaengerLand ?? "DE" } },
                            Service = new { Code = "11" },
                            Package = new[] { new { PackagingType = new { Code = "02" }, PackageWeight = new { UnitOfMeasurement = new { Code = "KGS" }, Weight = req.GewichtKg.ToString("F1") } } }
                        },
                        LabelSpecification = new { LabelImageFormat = new { Code = "PDF" } }
                    }
                };
                var response = await _http.PostAsJsonAsync("https://onlinetools.ups.com/api/shipments/v1/ship", payload);
                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                if (json.TryGetProperty("ShipmentResponse", out var sr) && sr.TryGetProperty("ShipmentResults", out var results))
                {
                    var pkg = results.GetProperty("PackageResults");
                    return new ShipmentResult
                    {
                        Success = true,
                        TrackingNumber = pkg.GetProperty("TrackingNumber").GetString() ?? "",
                        LabelPdf = pkg.TryGetProperty("ShippingLabel", out var lbl) ? Convert.FromBase64String(lbl.GetProperty("GraphicImage").GetString() ?? "") : null,
                        Carrier = "UPS"
                    };
                }
                return new ShipmentResult { Success = false, Error = json.ToString() };
            }
            catch (Exception ex) { _log.Error(ex, "UPS Fehler"); return new ShipmentResult { Success = false, Error = ex.Message }; }
        }
        #endregion

        public async Task<ShipmentResult> CreateShipmentAsync(ShipmentRequest req, string carrier)
        {
            return carrier.ToUpper() switch
            {
                "DHL" => await CreateDHLShipmentAsync(req),
                "DPD" => await CreateDPDShipmentAsync(req),
                "GLS" => await CreateGLSShipmentAsync(req),
                "UPS" => await CreateUPSShipmentAsync(req),
                _ => new ShipmentResult { Success = false, Error = $"Unbekannter Carrier: {carrier}" }
            };
        }

        public string GetTrackingUrl(string carrier, string trackingNumber)
        {
            return carrier.ToUpper() switch
            {
                "DHL" => $"https://www.dhl.de/de/privatkunden/pakete-empfangen/verfolgen.html?piececode={trackingNumber}",
                "DPD" => $"https://tracking.dpd.de/parcelstatus?query={trackingNumber}&locale=de_DE",
                "GLS" => $"https://gls-group.eu/DE/de/paketverfolgung?match={trackingNumber}",
                "UPS" => $"https://www.ups.com/track?tracknum={trackingNumber}",
                _ => ""
            };
        }
    }

    public class ShippingConfig
    {
        public string AbsenderName { get; set; } = "NOVVIA GmbH";
        public string AbsenderStrasse { get; set; } = "";
        public string AbsenderPLZ { get; set; } = "";
        public string AbsenderOrt { get; set; } = "";
        public string AbsenderEmail { get; set; } = "";
        public string DHLUser { get; set; } = "";
        public string DHLPassword { get; set; } = "";
        public string DHLProfile { get; set; } = "";
        public string DHLBillingNumber { get; set; } = "";
        public string DPDUser { get; set; } = "";
        public string DPDPassword { get; set; } = "";
        public string DPDDepot { get; set; } = "";
        public string GLSUser { get; set; } = "";
        public string GLSPassword { get; set; } = "";
        public string GLSShipperId { get; set; } = "";
        public string UPSToken { get; set; } = "";
        public string UPSAccountNumber { get; set; } = "";
    }

    public class ShipmentRequest
    {
        public string Referenz { get; set; } = "";
        public string EmpfaengerName { get; set; } = "";
        public string EmpfaengerStrasse { get; set; } = "";
        public string EmpfaengerPLZ { get; set; } = "";
        public string EmpfaengerOrt { get; set; } = "";
        public string? EmpfaengerLand { get; set; }
        public string? EmpfaengerEmail { get; set; }
        public decimal GewichtKg { get; set; } = 1;
    }

    public class ShipmentResult
    {
        public bool Success { get; set; }
        public string TrackingNumber { get; set; } = "";
        public byte[]? LabelPdf { get; set; }
        public string Carrier { get; set; } = "";
        public string? Error { get; set; }
    }
}

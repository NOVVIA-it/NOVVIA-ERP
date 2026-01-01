using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dapper;
using NovviaERP.Core.Data;
using Serilog;

namespace NovviaERP.Core.Services
{
    /// <summary>
    /// EDIFACT Service fuer Pharma-Grosshandel und allgemeine EDI-Partner
    /// Unterstuetzte Nachrichtentypen: ORDERS, ORDRSP, DESADV, INVOIC
    /// </summary>
    public class EdifactService : IDisposable
    {
        private readonly JtlDbContext _db;
        private static readonly ILogger _log = Log.ForContext<EdifactService>();

        // EDIFACT Trennzeichen (UNOA)
        private const char SEGMENT_TERMINATOR = '\'';
        private const char ELEMENT_SEPARATOR = '+';
        private const char COMPONENT_SEPARATOR = ':';
        private const char RELEASE_CHARACTER = '?';

        public EdifactService(JtlDbContext db)
        {
            _db = db;
        }

        public EdifactService(string connectionString)
        {
            _db = new JtlDbContext(connectionString);
        }

        public void Dispose() { }

        #region ORDERS - Bestellungen empfangen

        /// <summary>
        /// Parst eine EDIFACT ORDERS Nachricht
        /// </summary>
        public async Task<EdifactOrdersResult> ParseOrdersAsync(string edifactContent, int partnerId)
        {
            var result = new EdifactOrdersResult();

            try
            {
                var segments = ParseSegments(edifactContent);
                var order = new EdifactOrder();

                string currentItem = "";
                EdifactOrderPosition? currentPos = null;

                foreach (var seg in segments)
                {
                    var elements = ParseElements(seg);
                    var tag = elements[0];

                    switch (tag)
                    {
                        case "UNB":
                            // Interchange Header
                            order.SenderId = GetComponent(elements, 2, 0);
                            order.ReceiverId = GetComponent(elements, 3, 0);
                            order.InterchangeDate = ParseEdifactDate(GetComponent(elements, 4, 0) + GetComponent(elements, 4, 1));
                            order.InterchangeRef = GetElement(elements, 5);
                            break;

                        case "UNH":
                            // Message Header
                            order.MessageRef = GetElement(elements, 1);
                            order.MessageType = GetComponent(elements, 2, 0);
                            break;

                        case "BGM":
                            // Beginning of Message
                            order.DocumentNumber = GetElement(elements, 2);
                            order.DocumentFunction = GetElement(elements, 3); // 9=Original, 5=Replace
                            break;

                        case "DTM":
                            // Date/Time
                            var qualifier = GetComponent(elements, 1, 0);
                            var dateValue = GetComponent(elements, 1, 1);
                            var format = GetComponent(elements, 1, 2);

                            if (qualifier == "137") // Document date
                                order.DocumentDate = ParseEdifactDate(dateValue, format);
                            else if (qualifier == "2") // Delivery date
                                order.RequestedDeliveryDate = ParseEdifactDate(dateValue, format);
                            break;

                        case "NAD":
                            // Name and Address
                            var nadQualifier = GetElement(elements, 1);
                            var nadId = GetComponent(elements, 2, 0);
                            var nadName = GetElement(elements, 4);

                            if (nadQualifier == "BY") // Buyer
                            {
                                order.BuyerId = nadId;
                                order.BuyerName = nadName;
                            }
                            else if (nadQualifier == "SU") // Supplier
                            {
                                order.SupplierId = nadId;
                            }
                            else if (nadQualifier == "DP") // Delivery Party
                            {
                                order.DeliveryPartyId = nadId;
                                order.DeliveryPartyName = nadName;
                            }
                            break;

                        case "LIN":
                            // Line Item
                            if (currentPos != null)
                                order.Positions.Add(currentPos);

                            currentPos = new EdifactOrderPosition
                            {
                                LineNumber = int.TryParse(GetElement(elements, 1), out var ln) ? ln : order.Positions.Count + 1,
                                EAN = GetComponent(elements, 3, 0),
                                ProductIdQualifier = GetComponent(elements, 3, 1) // EN = EAN
                            };
                            break;

                        case "PIA":
                            // Additional Product ID
                            if (currentPos != null)
                            {
                                var piaQualifier = GetComponent(elements, 2, 1);
                                var piaValue = GetComponent(elements, 2, 0);

                                if (piaQualifier == "SA") // Supplier Article Number
                                    currentPos.SupplierArticleNo = piaValue;
                                else if (piaQualifier == "BP") // Buyer Part Number
                                    currentPos.BuyerArticleNo = piaValue;
                                else if (piaQualifier == "IN") // Buyer's internal product number
                                    currentPos.BuyerArticleNo = piaValue;
                            }
                            break;

                        case "IMD":
                            // Item Description
                            if (currentPos != null)
                            {
                                var desc = GetComponent(elements, 3, 3);
                                if (!string.IsNullOrEmpty(desc))
                                    currentPos.Description = desc;
                            }
                            break;

                        case "QTY":
                            // Quantity
                            if (currentPos != null)
                            {
                                var qtyQualifier = GetComponent(elements, 1, 0);
                                var qtyValue = GetComponent(elements, 1, 1);

                                if (qtyQualifier == "21") // Ordered quantity
                                    currentPos.Quantity = ParseDecimal(qtyValue);
                            }
                            break;

                        case "PRI":
                            // Price
                            if (currentPos != null)
                            {
                                var priceQualifier = GetComponent(elements, 1, 0);
                                var priceValue = GetComponent(elements, 1, 1);

                                if (priceQualifier == "AAA") // Net price
                                    currentPos.NetPrice = ParseDecimal(priceValue);
                                else if (priceQualifier == "AAB") // Gross price
                                    currentPos.GrossPrice = ParseDecimal(priceValue);
                            }
                            break;

                        case "UNS":
                            // Section Control - Ende Positionen
                            if (currentPos != null)
                            {
                                order.Positions.Add(currentPos);
                                currentPos = null;
                            }
                            break;

                        case "CNT":
                            // Control Total
                            var cntQualifier = GetComponent(elements, 1, 0);
                            var cntValue = GetComponent(elements, 1, 1);

                            if (cntQualifier == "2") // Line items
                                order.TotalLineItems = int.TryParse(cntValue, out var cnt) ? cnt : 0;
                            break;
                    }
                }

                // Letzte Position hinzufuegen
                if (currentPos != null)
                    order.Positions.Add(currentPos);

                order.PartnerId = partnerId;
                result.Order = order;
                result.Erfolg = true;

                _log.Information("ORDERS geparst: {DocumentNumber}, {PositionCount} Positionen",
                    order.DocumentNumber, order.Positions.Count);
            }
            catch (Exception ex)
            {
                result.Erfolg = false;
                result.Fehler = ex.Message;
                _log.Error(ex, "ORDERS Parse-Fehler");
            }

            return result;
        }

        /// <summary>
        /// Importiert eine geparste ORDERS Nachricht als Auftrag
        /// </summary>
        public async Task<int?> ImportOrdersAsync(EdifactOrder order)
        {
            try
            {
                var conn = await _db.GetConnectionAsync();

                // Partner-Mapping laden
                var partner = await conn.QueryFirstOrDefaultAsync<EdifactPartner>(
                    "SELECT * FROM NOVVIA.EdifactPartner WHERE kPartner = @Id",
                    new { Id = order.PartnerId });

                if (partner == null)
                {
                    _log.Warning("EDIFACT Partner nicht gefunden: {PartnerId}", order.PartnerId);
                    return null;
                }

                // Kunde ermitteln (ueber GLN/ILN)
                var kundeId = await conn.QueryFirstOrDefaultAsync<int?>(
                    "SELECT kKunde FROM dbo.tKunde WHERE cGLN = @GLN",
                    new { GLN = order.BuyerId });

                if (!kundeId.HasValue)
                {
                    _log.Warning("Kunde nicht gefunden fuer GLN: {GLN}", order.BuyerId);
                    return null;
                }

                // TODO: Auftrag in JTL anlegen
                // Dies haengt von der JTL-Struktur ab

                _log.Information("ORDERS importiert: {DocumentNumber} -> Kunde {KundeId}",
                    order.DocumentNumber, kundeId);

                return kundeId;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "ORDERS Import-Fehler");
                return null;
            }
        }

        #endregion

        #region ORDRSP - Auftragsbestaetigung senden

        /// <summary>
        /// Generiert eine EDIFACT ORDRSP Nachricht
        /// </summary>
        public async Task<string> GenerateOrdrspAsync(int auftragId, int partnerId, OrdrspType type = OrdrspType.Accepted)
        {
            var sb = new StringBuilder();

            try
            {
                var conn = await _db.GetConnectionAsync();

                // Partner laden
                var partner = await conn.QueryFirstOrDefaultAsync<EdifactPartner>(
                    "SELECT * FROM NOVVIA.EdifactPartner WHERE kPartner = @Id",
                    new { Id = partnerId });

                if (partner == null)
                    throw new Exception($"Partner {partnerId} nicht gefunden");

                // Auftrag laden
                var auftrag = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
                    SELECT b.kBestellung, b.cBestellNr, b.dErstellt,
                           k.cKundenNr, k.cGLN,
                           COALESCE(a.cFirma, a.cVorname + ' ' + a.cName) AS KundeName
                    FROM dbo.tBestellung b
                    JOIN dbo.tKunde k ON b.kKunde = k.kKunde
                    LEFT JOIN dbo.tAdresse a ON a.kKunde = k.kKunde AND a.nStandard = 1
                    WHERE b.kBestellung = @Id",
                    new { Id = auftragId });

                if (auftrag == null)
                    throw new Exception($"Auftrag {auftragId} nicht gefunden");

                // Positionen laden
                var positionen = await conn.QueryAsync<dynamic>(@"
                    SELECT bp.nPosNr, bp.fAnzahl, bp.fVKNetto,
                           ar.cArtNr, ar.cBarcode, ar.cName
                    FROM dbo.tBestellpos bp
                    JOIN dbo.tArtikel ar ON bp.kArtikel = ar.kArtikel
                    WHERE bp.kBestellung = @Id
                    ORDER BY bp.nPosNr",
                    new { Id = auftragId });

                var posList = positionen.ToList();
                var now = DateTime.Now;
                var interchangeRef = GenerateInterchangeRef();
                var messageRef = GenerateMessageRef();

                // UNA - Service String Advice
                sb.Append("UNA:+.? '");

                // UNB - Interchange Header
                sb.Append($"UNB+UNOA:2+{partner.CEigeneGLN}:14+{partner.CPartnerGLN}:14+{now:yyMMdd}:{now:HHmm}+{interchangeRef}'");

                // UNH - Message Header
                sb.Append($"UNH+{messageRef}+ORDRSP:D:96A:UN'");

                // BGM - Beginning of Message
                var bgmCode = type switch
                {
                    OrdrspType.Accepted => "231",      // Order response
                    OrdrspType.AcceptedWithChange => "231",
                    OrdrspType.Rejected => "232",
                    _ => "231"
                };
                sb.Append($"BGM+{bgmCode}+{auftrag.cBestellNr}+29'"); // 29 = Accepted

                // DTM - Document Date
                sb.Append($"DTM+137:{now:yyyyMMdd}:102'");

                // RFF - Reference (Original Order)
                sb.Append($"RFF+ON:{auftrag.cBestellNr}'");

                // NAD - Buyer
                sb.Append($"NAD+BY+{auftrag.cGLN ?? ""}::9'");

                // NAD - Supplier
                sb.Append($"NAD+SU+{partner.CEigeneGLN}::9'");

                // Positionen
                int segCount = 7;
                foreach (var pos in posList)
                {
                    // LIN - Line Item
                    sb.Append($"LIN+{pos.nPosNr}++{pos.cBarcode ?? ""}:EN'");
                    segCount++;

                    // PIA - Supplier Article Number
                    if (!string.IsNullOrEmpty(pos.cArtNr))
                    {
                        sb.Append($"PIA+5+{pos.cArtNr}:SA'");
                        segCount++;
                    }

                    // QTY - Confirmed Quantity
                    sb.Append($"QTY+21:{FormatDecimal(pos.fAnzahl)}'");
                    segCount++;

                    // PRI - Price
                    sb.Append($"PRI+AAA:{FormatDecimal(pos.fVKNetto)}'");
                    segCount++;
                }

                // UNS - Section Control
                sb.Append("UNS+S'");
                segCount++;

                // UNT - Message Trailer
                sb.Append($"UNT+{segCount + 1}+{messageRef}'");

                // UNZ - Interchange Trailer
                sb.Append($"UNZ+1+{interchangeRef}'");

                _log.Information("ORDRSP generiert: Auftrag {AuftragNr}, {PosCount} Positionen",
                    auftrag.cBestellNr, posList.Count);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "ORDRSP Generierung fehlgeschlagen");
                throw;
            }

            return sb.ToString();
        }

        #endregion

        #region DESADV - Lieferavis senden

        /// <summary>
        /// Generiert eine EDIFACT DESADV Nachricht
        /// </summary>
        public async Task<string> GenerateDesadvAsync(int lieferscheinId, int partnerId)
        {
            var sb = new StringBuilder();

            try
            {
                var conn = await _db.GetConnectionAsync();

                // Partner laden
                var partner = await conn.QueryFirstOrDefaultAsync<EdifactPartner>(
                    "SELECT * FROM NOVVIA.EdifactPartner WHERE kPartner = @Id",
                    new { Id = partnerId });

                if (partner == null)
                    throw new Exception($"Partner {partnerId} nicht gefunden");

                // Lieferschein laden
                var lieferschein = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
                    SELECT l.kLieferschein, l.cLieferscheinNr, l.dErstellt,
                           b.cBestellNr,
                           k.cKundenNr, k.cGLN,
                           COALESCE(a.cFirma, a.cVorname + ' ' + a.cName) AS KundeName,
                           la.cFirma AS LieferFirma, la.cStrasse AS LieferStrasse,
                           la.cPLZ AS LieferPLZ, la.cOrt AS LieferOrt
                    FROM dbo.tLieferschein l
                    JOIN dbo.tBestellung b ON l.kBestellung = b.kBestellung
                    JOIN dbo.tKunde k ON b.kKunde = k.kKunde
                    LEFT JOIN dbo.tAdresse a ON a.kKunde = k.kKunde AND a.nStandard = 1
                    LEFT JOIN dbo.tLieferadresse la ON la.kLieferadresse = b.kLieferadresse
                    WHERE l.kLieferschein = @Id",
                    new { Id = lieferscheinId });

                if (lieferschein == null)
                    throw new Exception($"Lieferschein {lieferscheinId} nicht gefunden");

                // Positionen laden
                var positionen = await conn.QueryAsync<dynamic>(@"
                    SELECT lp.nPosNr, lp.fAnzahl,
                           ar.cArtNr, ar.cBarcode, ar.cName
                    FROM dbo.tLieferscheinpos lp
                    JOIN dbo.tArtikel ar ON lp.kArtikel = ar.kArtikel
                    WHERE lp.kLieferschein = @Id
                    ORDER BY lp.nPosNr",
                    new { Id = lieferscheinId });

                var posList = positionen.ToList();
                var now = DateTime.Now;
                var interchangeRef = GenerateInterchangeRef();
                var messageRef = GenerateMessageRef();

                // UNA
                sb.Append("UNA:+.? '");

                // UNB
                sb.Append($"UNB+UNOA:2+{partner.CEigeneGLN}:14+{partner.CPartnerGLN}:14+{now:yyMMdd}:{now:HHmm}+{interchangeRef}'");

                // UNH
                sb.Append($"UNH+{messageRef}+DESADV:D:96A:UN'");

                // BGM - Despatch Advice
                sb.Append($"BGM+351+{lieferschein.cLieferscheinNr}+9'");

                // DTM - Despatch Date
                sb.Append($"DTM+11:{now:yyyyMMdd}:102'");

                // RFF - Order Reference
                sb.Append($"RFF+ON:{lieferschein.cBestellNr}'");

                // NAD - Consignee (Empfaenger)
                sb.Append($"NAD+CN+{lieferschein.cGLN ?? ""}::9++{CleanEdifact(lieferschein.KundeName ?? "")}+{CleanEdifact(lieferschein.LieferStrasse ?? "")}+{CleanEdifact(lieferschein.LieferOrt ?? "")}++{lieferschein.LieferPLZ ?? ""}'");

                // NAD - Consignor (Versender)
                sb.Append($"NAD+CZ+{partner.CEigeneGLN}::9'");

                int segCount = 7;

                // CPS - Consignment Packing Sequence
                sb.Append("CPS+1'");
                segCount++;

                // Positionen
                foreach (var pos in posList)
                {
                    // LIN
                    sb.Append($"LIN+{pos.nPosNr}++{pos.cBarcode ?? ""}:EN'");
                    segCount++;

                    // PIA
                    if (!string.IsNullOrEmpty(pos.cArtNr))
                    {
                        sb.Append($"PIA+5+{pos.cArtNr}:SA'");
                        segCount++;
                    }

                    // QTY - Despatch Quantity
                    sb.Append($"QTY+12:{FormatDecimal(pos.fAnzahl)}'");
                    segCount++;
                }

                // UNT
                sb.Append($"UNT+{segCount + 1}+{messageRef}'");

                // UNZ
                sb.Append($"UNZ+1+{interchangeRef}'");

                _log.Information("DESADV generiert: Lieferschein {LsNr}, {PosCount} Positionen",
                    lieferschein.cLieferscheinNr, posList.Count);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "DESADV Generierung fehlgeschlagen");
                throw;
            }

            return sb.ToString();
        }

        #endregion

        #region INVOIC - Rechnung senden

        /// <summary>
        /// Generiert eine EDIFACT INVOIC Nachricht
        /// </summary>
        public async Task<string> GenerateInvoicAsync(int rechnungId, int partnerId)
        {
            var sb = new StringBuilder();

            try
            {
                var conn = await _db.GetConnectionAsync();

                // Partner laden
                var partner = await conn.QueryFirstOrDefaultAsync<EdifactPartner>(
                    "SELECT * FROM NOVVIA.EdifactPartner WHERE kPartner = @Id",
                    new { Id = partnerId });

                if (partner == null)
                    throw new Exception($"Partner {partnerId} nicht gefunden");

                // Rechnung laden
                var rechnung = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
                    SELECT r.kRechnung, r.cRechnungsnummer, r.dErstellt,
                           re.fVKNettoGesamt, re.fVKBruttoGesamt, re.fSteuerGesamt,
                           b.cBestellNr,
                           k.cKundenNr, k.cGLN, k.cUstId,
                           COALESCE(a.cFirma, a.cVorname + ' ' + a.cName) AS KundeName,
                           a.cStrasse, a.cPLZ, a.cOrt, a.cLand
                    FROM Rechnung.tRechnung r
                    JOIN Rechnung.tRechnungEckdaten re ON r.kRechnung = re.kRechnung
                    JOIN dbo.tBestellung b ON r.kBestellung = b.kBestellung
                    JOIN dbo.tKunde k ON r.kKunde = k.kKunde
                    LEFT JOIN dbo.tAdresse a ON a.kKunde = k.kKunde AND a.nStandard = 1
                    WHERE r.kRechnung = @Id",
                    new { Id = rechnungId });

                if (rechnung == null)
                    throw new Exception($"Rechnung {rechnungId} nicht gefunden");

                // Positionen laden
                var positionen = await conn.QueryAsync<dynamic>(@"
                    SELECT rp.nPosNr, rp.fAnzahl, rp.fVKNetto, rp.fMwSt,
                           ar.cArtNr, ar.cBarcode, ar.cName
                    FROM Rechnung.tRechnungPos rp
                    JOIN dbo.tArtikel ar ON rp.kArtikel = ar.kArtikel
                    WHERE rp.kRechnung = @Id
                    ORDER BY rp.nPosNr",
                    new { Id = rechnungId });

                var posList = positionen.ToList();
                var now = DateTime.Now;
                var interchangeRef = GenerateInterchangeRef();
                var messageRef = GenerateMessageRef();

                // UNA
                sb.Append("UNA:+.? '");

                // UNB
                sb.Append($"UNB+UNOA:2+{partner.CEigeneGLN}:14+{partner.CPartnerGLN}:14+{now:yyMMdd}:{now:HHmm}+{interchangeRef}'");

                // UNH
                sb.Append($"UNH+{messageRef}+INVOIC:D:96A:UN'");

                // BGM - Invoice
                sb.Append($"BGM+380+{rechnung.cRechnungsnummer}+9'");

                // DTM - Invoice Date
                sb.Append($"DTM+137:{((DateTime)rechnung.dErstellt):yyyyMMdd}:102'");

                // RFF - Order Reference
                sb.Append($"RFF+ON:{rechnung.cBestellNr}'");

                // NAD - Buyer
                sb.Append($"NAD+BY+{rechnung.cGLN ?? ""}::9++{CleanEdifact(rechnung.KundeName ?? "")}+{CleanEdifact(rechnung.cStrasse ?? "")}+{CleanEdifact(rechnung.cOrt ?? "")}++{rechnung.cPLZ ?? ""}+{rechnung.cLand ?? "DE"}'");

                // NAD - Supplier
                sb.Append($"NAD+SU+{partner.CEigeneGLN}::9++{CleanEdifact(partner.CEigeneFirma ?? "")}+{CleanEdifact(partner.CEigeneStrasse ?? "")}+{CleanEdifact(partner.CEigeneOrt ?? "")}++{partner.CEigenePLZ ?? ""}+DE'");

                // RFF - VAT Number
                if (!string.IsNullOrEmpty(rechnung.cUstId))
                    sb.Append($"RFF+VA:{rechnung.cUstId}'");

                int segCount = 8;

                // Positionen
                foreach (var pos in posList)
                {
                    // LIN
                    sb.Append($"LIN+{pos.nPosNr}++{pos.cBarcode ?? ""}:EN'");
                    segCount++;

                    // PIA
                    if (!string.IsNullOrEmpty(pos.cArtNr))
                    {
                        sb.Append($"PIA+5+{pos.cArtNr}:SA'");
                        segCount++;
                    }

                    // IMD - Description
                    sb.Append($"IMD+F++:::{CleanEdifact(pos.cName ?? "")}'");
                    segCount++;

                    // QTY
                    sb.Append($"QTY+47:{FormatDecimal(pos.fAnzahl)}'");
                    segCount++;

                    // MOA - Line Amount
                    decimal lineAmount = (decimal)pos.fAnzahl * (decimal)pos.fVKNetto;
                    sb.Append($"MOA+203:{FormatDecimal(lineAmount)}'");
                    segCount++;

                    // PRI - Price
                    sb.Append($"PRI+AAA:{FormatDecimal(pos.fVKNetto)}'");
                    segCount++;

                    // TAX - VAT
                    sb.Append($"TAX+7+VAT+++:::{FormatDecimal(pos.fMwSt)}'");
                    segCount++;
                }

                // UNS - Summary Section
                sb.Append("UNS+S'");
                segCount++;

                // MOA - Total Amounts
                sb.Append($"MOA+79:{FormatDecimal(rechnung.fVKNettoGesamt)}'");  // Net
                segCount++;
                sb.Append($"MOA+77:{FormatDecimal(rechnung.fSteuerGesamt)}'");   // Tax
                segCount++;
                sb.Append($"MOA+86:{FormatDecimal(rechnung.fVKBruttoGesamt)}'"); // Gross
                segCount++;

                // UNT
                sb.Append($"UNT+{segCount + 1}+{messageRef}'");

                // UNZ
                sb.Append($"UNZ+1+{interchangeRef}'");

                _log.Information("INVOIC generiert: Rechnung {RechnungNr}, {PosCount} Positionen, {Brutto} EUR",
                    rechnung.cRechnungsnummer, posList.Count, rechnung.fVKBruttoGesamt);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "INVOIC Generierung fehlgeschlagen");
                throw;
            }

            return sb.ToString();
        }

        #endregion

        #region Partner-Verwaltung

        /// <summary>
        /// Holt alle EDIFACT-Partner
        /// </summary>
        public async Task<IEnumerable<EdifactPartner>> GetPartnerAsync()
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QueryAsync<EdifactPartner>(
                "SELECT * FROM NOVVIA.EdifactPartner ORDER BY cName");
        }

        /// <summary>
        /// Speichert einen EDIFACT-Partner
        /// </summary>
        public async Task<int> SavePartnerAsync(EdifactPartner partner)
        {
            var conn = await _db.GetConnectionAsync();

            if (partner.KPartner == 0)
            {
                return await conn.QuerySingleAsync<int>(@"
                    INSERT INTO NOVVIA.EdifactPartner
                    (cName, cPartnerGLN, cEigeneGLN, cEigeneFirma, cEigeneStrasse, cEigenePLZ, cEigeneOrt,
                     cProtokoll, cHost, nPort, cBenutzer, cPasswort, cVerzeichnisIn, cVerzeichnisOut, nAktiv)
                    VALUES (@CName, @CPartnerGLN, @CEigeneGLN, @CEigeneFirma, @CEigeneStrasse, @CEigenePLZ, @CEigeneOrt,
                            @CProtokoll, @CHost, @NPort, @CBenutzer, @CPasswort, @CVerzeichnisIn, @CVerzeichnisOut, @NAktiv);
                    SELECT SCOPE_IDENTITY()", partner);
            }
            else
            {
                await conn.ExecuteAsync(@"
                    UPDATE NOVVIA.EdifactPartner SET
                        cName = @CName, cPartnerGLN = @CPartnerGLN, cEigeneGLN = @CEigeneGLN,
                        cEigeneFirma = @CEigeneFirma, cEigeneStrasse = @CEigeneStrasse,
                        cEigenePLZ = @CEigenePLZ, cEigeneOrt = @CEigeneOrt,
                        cProtokoll = @CProtokoll, cHost = @CHost, nPort = @NPort,
                        cBenutzer = @CBenutzer, cPasswort = @CPasswort,
                        cVerzeichnisIn = @CVerzeichnisIn, cVerzeichnisOut = @CVerzeichnisOut,
                        nAktiv = @NAktiv
                    WHERE kPartner = @KPartner", partner);
                return partner.KPartner;
            }
        }

        /// <summary>
        /// Loescht einen EDIFACT-Partner
        /// </summary>
        public async Task DeletePartnerAsync(int partnerId)
        {
            var conn = await _db.GetConnectionAsync();
            await conn.ExecuteAsync("DELETE FROM NOVVIA.EdifactPartner WHERE kPartner = @Id", new { Id = partnerId });
        }

        /// <summary>
        /// Testet die Verbindung zu einem Partner
        /// </summary>
        public async Task<bool> TestConnectionAsync(int partnerId)
        {
            var conn = await _db.GetConnectionAsync();
            var partner = await conn.QueryFirstOrDefaultAsync<EdifactPartner>(
                "SELECT * FROM NOVVIA.EdifactPartner WHERE kPartner = @Id", new { Id = partnerId });

            if (partner == null) return false;

            // TODO: Echte Verbindungspruefung je nach Protokoll
            // Fuer jetzt: Dummy-Implementierung
            return !string.IsNullOrEmpty(partner.CHost);
        }

        /// <summary>
        /// Holt Log-Eintraege mit optionalen Filtern
        /// </summary>
        public async Task<IEnumerable<EdifactLogEntry>> GetLogAsync(int? partnerId = null, string? richtung = null, string? typ = null, string? status = null)
        {
            var conn = await _db.GetConnectionAsync();
            var sql = @"
                SELECT l.*, p.cName AS PartnerName
                FROM NOVVIA.EdifactLog l
                LEFT JOIN NOVVIA.EdifactPartner p ON l.kPartner = p.kPartner
                WHERE 1=1";

            if (partnerId.HasValue)
                sql += " AND l.kPartner = @PartnerId";
            if (!string.IsNullOrEmpty(richtung))
                sql += " AND l.cRichtung = @Richtung";
            if (!string.IsNullOrEmpty(typ))
                sql += " AND l.cNachrichtentyp = @Typ";
            if (!string.IsNullOrEmpty(status))
                sql += " AND l.cStatus = @Status";

            sql += " ORDER BY l.dErstellt DESC";

            return await conn.QueryAsync<EdifactLogEntry>(sql, new { PartnerId = partnerId, Richtung = richtung, Typ = typ, Status = status });
        }

        #endregion

        #region Datei-Import/Export

        /// <summary>
        /// Importiert EDIFACT-Dateien aus einem Verzeichnis
        /// </summary>
        public async Task<EdifactImportResult> ImportFromDirectoryAsync(string directory, int partnerId)
        {
            var result = new EdifactImportResult();

            try
            {
                var files = Directory.GetFiles(directory, "*.edi")
                    .Concat(Directory.GetFiles(directory, "*.txt"))
                    .Concat(Directory.GetFiles(directory, "*.edifact"));

                foreach (var file in files)
                {
                    try
                    {
                        var content = await File.ReadAllTextAsync(file);
                        var messageType = DetectMessageType(content);

                        if (messageType == "ORDERS")
                        {
                            var parseResult = await ParseOrdersAsync(content, partnerId);
                            if (parseResult.Erfolg)
                            {
                                result.ImportierteNachrichten++;
                                // Datei ins Archiv verschieben
                                var archivePath = Path.Combine(directory, "archiv", Path.GetFileName(file));
                                Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);
                                File.Move(file, archivePath, true);
                            }
                            else
                            {
                                result.FehlerNachrichten.Add($"{Path.GetFileName(file)}: {parseResult.Fehler}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        result.FehlerNachrichten.Add($"{Path.GetFileName(file)}: {ex.Message}");
                    }
                }

                result.Erfolg = result.FehlerNachrichten.Count == 0;
            }
            catch (Exception ex)
            {
                result.Erfolg = false;
                result.FehlerNachrichten.Add(ex.Message);
            }

            return result;
        }

        /// <summary>
        /// Exportiert eine EDIFACT-Nachricht in eine Datei
        /// </summary>
        public async Task ExportToFileAsync(string content, string directory, string prefix)
        {
            var fileName = $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}.edi";
            var path = Path.Combine(directory, fileName);

            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(path, content, Encoding.UTF8);

            _log.Information("EDIFACT exportiert: {Path}", path);
        }

        #endregion

        #region Helpers

        private List<string> ParseSegments(string edifact)
        {
            // Release Character beachten
            var segments = new List<string>();
            var current = new StringBuilder();
            bool escaped = false;

            foreach (var c in edifact)
            {
                if (escaped)
                {
                    current.Append(c);
                    escaped = false;
                }
                else if (c == RELEASE_CHARACTER)
                {
                    escaped = true;
                }
                else if (c == SEGMENT_TERMINATOR)
                {
                    var seg = current.ToString().Trim();
                    if (!string.IsNullOrEmpty(seg))
                        segments.Add(seg);
                    current.Clear();
                }
                else if (c != '\r' && c != '\n')
                {
                    current.Append(c);
                }
            }

            return segments;
        }

        private List<string> ParseElements(string segment)
        {
            var elements = new List<string>();
            var current = new StringBuilder();
            bool escaped = false;

            foreach (var c in segment)
            {
                if (escaped)
                {
                    current.Append(c);
                    escaped = false;
                }
                else if (c == RELEASE_CHARACTER)
                {
                    escaped = true;
                }
                else if (c == ELEMENT_SEPARATOR)
                {
                    elements.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            elements.Add(current.ToString());
            return elements;
        }

        private string GetElement(List<string> elements, int index)
        {
            return index < elements.Count ? elements[index] : "";
        }

        private string GetComponent(List<string> elements, int elementIndex, int componentIndex)
        {
            if (elementIndex >= elements.Count) return "";

            var parts = elements[elementIndex].Split(COMPONENT_SEPARATOR);
            return componentIndex < parts.Length ? parts[componentIndex] : "";
        }

        private DateTime? ParseEdifactDate(string value, string format = "102")
        {
            if (string.IsNullOrEmpty(value)) return null;

            try
            {
                return format switch
                {
                    "102" => DateTime.ParseExact(value, "yyyyMMdd", CultureInfo.InvariantCulture),
                    "203" => DateTime.ParseExact(value, "yyyyMMddHHmm", CultureInfo.InvariantCulture),
                    "204" => DateTime.ParseExact(value, "yyyyMMddHHmmss", CultureInfo.InvariantCulture),
                    _ => DateTime.ParseExact(value.PadRight(8, '0').Substring(0, 8), "yyyyMMdd", CultureInfo.InvariantCulture)
                };
            }
            catch
            {
                return null;
            }
        }

        private decimal ParseDecimal(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            value = value.Replace(",", ".");
            return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0;
        }

        private string FormatDecimal(decimal value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private string CleanEdifact(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            // Sonderzeichen escapen oder entfernen
            return Regex.Replace(text, @"['+:?]", "")
                .Replace("\r", "").Replace("\n", " ")
                .Trim();
        }

        private string GenerateInterchangeRef()
        {
            return DateTime.Now.ToString("yyMMddHHmmss") + new Random().Next(100, 999).ToString();
        }

        private string GenerateMessageRef()
        {
            return "MSG" + DateTime.Now.ToString("HHmmss") + new Random().Next(10, 99).ToString();
        }

        private string DetectMessageType(string content)
        {
            if (content.Contains("ORDERS:")) return "ORDERS";
            if (content.Contains("ORDRSP:")) return "ORDRSP";
            if (content.Contains("DESADV:")) return "DESADV";
            if (content.Contains("INVOIC:")) return "INVOIC";
            return "UNKNOWN";
        }

        #endregion
    }

    #region Enums

    public enum OrdrspType
    {
        Accepted,
        AcceptedWithChange,
        Rejected
    }

    #endregion

    #region DTOs

    public class EdifactPartner
    {
        public int KPartner { get; set; }
        public string CName { get; set; } = "";
        public string CPartnerGLN { get; set; } = "";
        public string CEigeneGLN { get; set; } = "";
        public string? CEigeneFirma { get; set; }
        public string? CEigeneStrasse { get; set; }
        public string? CEigenePLZ { get; set; }
        public string? CEigeneOrt { get; set; }
        public string CProtokoll { get; set; } = "SFTP"; // SFTP, FTP, AS2, Verzeichnis
        public string? CHost { get; set; }
        public int NPort { get; set; } = 22;
        public string? CBenutzer { get; set; }
        public string? CPasswort { get; set; }
        public string? CVerzeichnisIn { get; set; }
        public string? CVerzeichnisOut { get; set; }
        public bool NAktiv { get; set; } = true;
    }

    public class EdifactOrder
    {
        public int PartnerId { get; set; }
        public string SenderId { get; set; } = "";
        public string ReceiverId { get; set; } = "";
        public DateTime? InterchangeDate { get; set; }
        public string InterchangeRef { get; set; } = "";
        public string MessageRef { get; set; } = "";
        public string MessageType { get; set; } = "";
        public string DocumentNumber { get; set; } = "";
        public string DocumentFunction { get; set; } = "";
        public DateTime? DocumentDate { get; set; }
        public DateTime? RequestedDeliveryDate { get; set; }
        public string BuyerId { get; set; } = "";
        public string BuyerName { get; set; } = "";
        public string SupplierId { get; set; } = "";
        public string DeliveryPartyId { get; set; } = "";
        public string DeliveryPartyName { get; set; } = "";
        public int TotalLineItems { get; set; }
        public List<EdifactOrderPosition> Positions { get; set; } = new();
    }

    public class EdifactOrderPosition
    {
        public int LineNumber { get; set; }
        public string EAN { get; set; } = "";
        public string ProductIdQualifier { get; set; } = "";
        public string? SupplierArticleNo { get; set; }
        public string? BuyerArticleNo { get; set; }
        public string? Description { get; set; }
        public decimal Quantity { get; set; }
        public decimal NetPrice { get; set; }
        public decimal GrossPrice { get; set; }
    }

    public class EdifactOrdersResult
    {
        public bool Erfolg { get; set; }
        public EdifactOrder? Order { get; set; }
        public string? Fehler { get; set; }
    }

    public class EdifactImportResult
    {
        public bool Erfolg { get; set; }
        public int ImportierteNachrichten { get; set; }
        public List<string> FehlerNachrichten { get; set; } = new();
    }

    public class EdifactLogEntry
    {
        public int KLog { get; set; }
        public int KPartner { get; set; }
        public string? PartnerName { get; set; }
        public string CRichtung { get; set; } = "";
        public string CNachrichtentyp { get; set; } = "";
        public string? CInterchangeRef { get; set; }
        public string? CMessageRef { get; set; }
        public string? CDokumentNr { get; set; }
        public string? CDateiname { get; set; }
        public string CStatus { get; set; } = "NEU";
        public string? CFehler { get; set; }
        public int? KBestellung { get; set; }
        public int? KRechnung { get; set; }
        public int? KLieferschein { get; set; }
        public DateTime DErstellt { get; set; }
    }

    #endregion
}

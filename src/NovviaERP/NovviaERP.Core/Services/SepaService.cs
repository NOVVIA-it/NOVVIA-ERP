using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Dapper;
using NovviaERP.Core.Data;
using Serilog;

namespace NovviaERP.Core.Services
{
    /// <summary>
    /// SEPA Service fuer Lastschrift-XML Generierung (pain.008)
    /// </summary>
    public class SepaService : IDisposable
    {
        private readonly JtlDbContext _db;
        private static readonly ILogger _log = Log.ForContext<SepaService>();

        // SEPA XML Namespaces
        private static readonly XNamespace NsPain008 = "urn:iso:std:iso:20022:tech:xsd:pain.008.001.02";
        private static readonly XNamespace NsXsi = "http://www.w3.org/2001/XMLSchema-instance";

        public SepaService(JtlDbContext db)
        {
            _db = db;
        }

        public void Dispose() { }

        #region SEPA Lastschrift XML

        /// <summary>
        /// Generiert SEPA Lastschrift XML (pain.008.001.02)
        /// </summary>
        public async Task<SepaExportResult> GenerateSepaDirectDebitXmlAsync(
            List<int> rechnungIds,
            SepaConfig config,
            DateTime ausfuehrungsDatum)
        {
            var result = new SepaExportResult();

            try
            {
                var conn = await _db.GetConnectionAsync();

                // Rechnungen mit SEPA-Mandat laden
                var rechnungen = await conn.QueryAsync<SepaRechnung>(@"
                    SELECT
                        r.kRechnung,
                        r.cRechnungsnummer,
                        re.fVKBruttoGesamt AS Brutto,
                        re.fOffenerWert AS Offen,
                        k.kKunde,
                        k.cKundenNr,
                        COALESCE(a.cFirma, a.cVorname + ' ' + a.cName) AS KundeName,
                        kb.cIBAN,
                        kb.cBIC,
                        kb.cKontoinhaber,
                        kb.cMandatsreferenz,
                        kb.dMandatsDatum
                    FROM Rechnung.tRechnung r
                    JOIN Rechnung.tRechnungEckdaten re ON r.kRechnung = re.kRechnung
                    JOIN dbo.tKunde k ON r.kKunde = k.kKunde
                    LEFT JOIN dbo.tAdresse a ON a.kKunde = k.kKunde AND a.nStandard = 1
                    LEFT JOIN dbo.tKundeBankverbindung kb ON k.kKunde = kb.kKunde AND kb.nStandard = 1
                    WHERE r.kRechnung IN @Ids
                      AND re.nZahlungStatus = 1
                      AND r.nStorno = 0",
                    new { Ids = rechnungIds });

                var liste = rechnungen.ToList();

                // Validierung
                foreach (var r in liste)
                {
                    if (string.IsNullOrEmpty(r.CIBAN))
                    {
                        result.Fehler.Add($"Rechnung {r.CRechnungsnummer}: Keine IBAN hinterlegt");
                        continue;
                    }
                    if (string.IsNullOrEmpty(r.CMandatsreferenz))
                    {
                        result.Fehler.Add($"Rechnung {r.CRechnungsnummer}: Kein SEPA-Mandat vorhanden");
                        continue;
                    }
                    if (!ValidateIBAN(r.CIBAN))
                    {
                        result.Fehler.Add($"Rechnung {r.CRechnungsnummer}: Ungueltige IBAN");
                        continue;
                    }

                    result.GueltigeRechnungen.Add(r);
                }

                if (result.GueltigeRechnungen.Count == 0)
                {
                    result.Erfolg = false;
                    return result;
                }

                // XML generieren
                var xml = BuildSepaXml(result.GueltigeRechnungen, config, ausfuehrungsDatum);
                result.XmlContent = xml;
                result.Erfolg = true;
                result.GesamtBetrag = result.GueltigeRechnungen.Sum(r => r.Offen);

                _log.Information("SEPA XML generiert: {Anzahl} Lastschriften, {Betrag} EUR",
                    result.GueltigeRechnungen.Count, result.GesamtBetrag);
            }
            catch (Exception ex)
            {
                result.Erfolg = false;
                result.Fehler.Add($"Fehler: {ex.Message}");
                _log.Error(ex, "SEPA XML Generierung Fehler");
            }

            return result;
        }

        private string BuildSepaXml(List<SepaRechnung> rechnungen, SepaConfig config, DateTime ausfuehrungsDatum)
        {
            var msgId = $"MSG-{DateTime.Now:yyyyMMddHHmmss}";
            var pmtInfId = $"PMT-{DateTime.Now:yyyyMMddHHmmss}";
            var gesamtBetrag = rechnungen.Sum(r => r.Offen);
            var anzahl = rechnungen.Count;

            var doc = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement(NsPain008 + "Document",
                    new XAttribute(XNamespace.Xmlns + "xsi", NsXsi),
                    new XElement(NsPain008 + "CstmrDrctDbtInitn",
                        // Group Header
                        new XElement(NsPain008 + "GrpHdr",
                            new XElement(NsPain008 + "MsgId", msgId),
                            new XElement(NsPain008 + "CreDtTm", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")),
                            new XElement(NsPain008 + "NbOfTxs", anzahl),
                            new XElement(NsPain008 + "CtrlSum", gesamtBetrag.ToString("F2", CultureInfo.InvariantCulture)),
                            new XElement(NsPain008 + "InitgPty",
                                new XElement(NsPain008 + "Nm", CleanSepaText(config.FirmaName))
                            )
                        ),
                        // Payment Information
                        new XElement(NsPain008 + "PmtInf",
                            new XElement(NsPain008 + "PmtInfId", pmtInfId),
                            new XElement(NsPain008 + "PmtMtd", "DD"), // Direct Debit
                            new XElement(NsPain008 + "BtchBookg", "true"),
                            new XElement(NsPain008 + "NbOfTxs", anzahl),
                            new XElement(NsPain008 + "CtrlSum", gesamtBetrag.ToString("F2", CultureInfo.InvariantCulture)),
                            new XElement(NsPain008 + "PmtTpInf",
                                new XElement(NsPain008 + "SvcLvl",
                                    new XElement(NsPain008 + "Cd", "SEPA")
                                ),
                                new XElement(NsPain008 + "LclInstrm",
                                    new XElement(NsPain008 + "Cd", "CORE") // CORE oder B2B
                                ),
                                new XElement(NsPain008 + "SeqTp", "RCUR") // FRST, RCUR, OOFF, FNAL
                            ),
                            new XElement(NsPain008 + "ReqdColltnDt", ausfuehrungsDatum.ToString("yyyy-MM-dd")),
                            new XElement(NsPain008 + "Cdtr",
                                new XElement(NsPain008 + "Nm", CleanSepaText(config.FirmaName))
                            ),
                            new XElement(NsPain008 + "CdtrAcct",
                                new XElement(NsPain008 + "Id",
                                    new XElement(NsPain008 + "IBAN", config.IBAN.Replace(" ", ""))
                                )
                            ),
                            new XElement(NsPain008 + "CdtrAgt",
                                new XElement(NsPain008 + "FinInstnId",
                                    new XElement(NsPain008 + "BIC", config.BIC)
                                )
                            ),
                            new XElement(NsPain008 + "ChrgBr", "SLEV"),
                            new XElement(NsPain008 + "CdtrSchmeId",
                                new XElement(NsPain008 + "Id",
                                    new XElement(NsPain008 + "PrvtId",
                                        new XElement(NsPain008 + "Othr",
                                            new XElement(NsPain008 + "Id", config.GlaeubigerId),
                                            new XElement(NsPain008 + "SchmeNm",
                                                new XElement(NsPain008 + "Prtry", "SEPA")
                                            )
                                        )
                                    )
                                )
                            ),
                            // Einzelne Transaktionen
                            rechnungen.Select(r => BuildDrctDbtTxInf(r))
                        )
                    )
                )
            );

            using var sw = new StringWriter();
            doc.Save(sw);
            return sw.ToString();
        }

        private XElement BuildDrctDbtTxInf(SepaRechnung rechnung)
        {
            var endToEndId = $"RE-{rechnung.CRechnungsnummer}";

            return new XElement(NsPain008 + "DrctDbtTxInf",
                new XElement(NsPain008 + "PmtId",
                    new XElement(NsPain008 + "EndToEndId", endToEndId)
                ),
                new XElement(NsPain008 + "InstdAmt",
                    new XAttribute("Ccy", "EUR"),
                    rechnung.Offen.ToString("F2", CultureInfo.InvariantCulture)
                ),
                new XElement(NsPain008 + "DrctDbtTx",
                    new XElement(NsPain008 + "MndtRltdInf",
                        new XElement(NsPain008 + "MndtId", rechnung.CMandatsreferenz),
                        new XElement(NsPain008 + "DtOfSgntr", rechnung.DMandatsDatum?.ToString("yyyy-MM-dd") ?? "2020-01-01")
                    )
                ),
                new XElement(NsPain008 + "DbtrAgt",
                    new XElement(NsPain008 + "FinInstnId",
                        string.IsNullOrEmpty(rechnung.CBIC)
                            ? new XElement(NsPain008 + "Othr", new XElement(NsPain008 + "Id", "NOTPROVIDED"))
                            : new XElement(NsPain008 + "BIC", rechnung.CBIC)
                    )
                ),
                new XElement(NsPain008 + "Dbtr",
                    new XElement(NsPain008 + "Nm", CleanSepaText(rechnung.CKontoinhaber ?? rechnung.KundeName))
                ),
                new XElement(NsPain008 + "DbtrAcct",
                    new XElement(NsPain008 + "Id",
                        new XElement(NsPain008 + "IBAN", rechnung.CIBAN.Replace(" ", ""))
                    )
                ),
                new XElement(NsPain008 + "RmtInf",
                    new XElement(NsPain008 + "Ustrd", CleanSepaText($"Rechnung {rechnung.CRechnungsnummer}"))
                )
            );
        }

        #endregion

        #region Faellige SEPA-Lastschriften

        /// <summary>
        /// Holt alle faelligen Rechnungen mit SEPA-Mandat
        /// </summary>
        public async Task<IEnumerable<SepaRechnung>> GetSepaFaelligAsync(int? tageVorFaelligkeit = 3)
        {
            var conn = await _db.GetConnectionAsync();

            return await conn.QueryAsync<SepaRechnung>(@"
                SELECT
                    r.kRechnung,
                    r.cRechnungsnummer,
                    re.fVKBruttoGesamt AS Brutto,
                    re.fOffenerWert AS Offen,
                    re.dZahlungsziel AS Faelligkeit,
                    k.kKunde,
                    k.cKundenNr,
                    COALESCE(a.cFirma, a.cVorname + ' ' + a.cName) AS KundeName,
                    kb.cIBAN,
                    kb.cBIC,
                    kb.cKontoinhaber,
                    kb.cMandatsreferenz,
                    kb.dMandatsDatum
                FROM Rechnung.tRechnung r
                JOIN Rechnung.tRechnungEckdaten re ON r.kRechnung = re.kRechnung
                JOIN dbo.tKunde k ON r.kKunde = k.kKunde
                LEFT JOIN dbo.tAdresse a ON a.kKunde = k.kKunde AND a.nStandard = 1
                LEFT JOIN dbo.tKundeBankverbindung kb ON k.kKunde = kb.kKunde AND kb.nStandard = 1
                WHERE re.nZahlungStatus = 1
                  AND r.nStorno = 0
                  AND kb.cMandatsreferenz IS NOT NULL
                  AND kb.cIBAN IS NOT NULL
                  AND DATEDIFF(DAY, GETDATE(), re.dZahlungsziel) <= @Tage
                ORDER BY re.dZahlungsziel",
                new { Tage = tageVorFaelligkeit });
        }

        #endregion

        #region IBAN Validierung

        /// <summary>
        /// Validiert eine IBAN
        /// </summary>
        public bool ValidateIBAN(string iban)
        {
            if (string.IsNullOrEmpty(iban)) return false;

            // Leerzeichen entfernen
            iban = iban.Replace(" ", "").ToUpper();

            // Laenge pruefen (DE = 22 Zeichen)
            if (iban.Length < 15 || iban.Length > 34) return false;

            // Nur alphanumerisch
            if (!Regex.IsMatch(iban, @"^[A-Z0-9]+$")) return false;

            // Modulo 97 Pruefung
            try
            {
                // Laendercode und Pruefziffer ans Ende
                var rearranged = iban.Substring(4) + iban.Substring(0, 4);

                // Buchstaben zu Zahlen (A=10, B=11, etc.)
                var numeric = new StringBuilder();
                foreach (var c in rearranged)
                {
                    if (char.IsDigit(c))
                        numeric.Append(c);
                    else
                        numeric.Append(c - 'A' + 10);
                }

                // Modulo 97
                var remainder = 0;
                foreach (var c in numeric.ToString())
                {
                    remainder = (remainder * 10 + (c - '0')) % 97;
                }

                return remainder == 1;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Formatiert eine IBAN mit Leerzeichen
        /// </summary>
        public string FormatIBAN(string iban)
        {
            if (string.IsNullOrEmpty(iban)) return "";

            iban = iban.Replace(" ", "").ToUpper();

            // Alle 4 Zeichen ein Leerzeichen
            var formatted = new StringBuilder();
            for (int i = 0; i < iban.Length; i++)
            {
                if (i > 0 && i % 4 == 0)
                    formatted.Append(' ');
                formatted.Append(iban[i]);
            }

            return formatted.ToString();
        }

        /// <summary>
        /// Ermittelt BIC aus IBAN (fuer deutsche Banken)
        /// </summary>
        public async Task<string?> GetBICFromIBANAsync(string iban)
        {
            if (string.IsNullOrEmpty(iban) || !iban.StartsWith("DE", StringComparison.OrdinalIgnoreCase))
                return null;

            iban = iban.Replace(" ", "");
            if (iban.Length < 12) return null;

            var blz = iban.Substring(4, 8);

            var conn = await _db.GetConnectionAsync();

            // BLZ in tBank suchen (falls Tabelle existiert)
            try
            {
                var bic = await conn.QueryFirstOrDefaultAsync<string>(
                    "SELECT TOP 1 cBIC FROM dbo.tBank WHERE cBLZ = @BLZ",
                    new { BLZ = blz });
                return bic;
            }
            catch
            {
                return null;
            }
        }

        #endregion

        #region Helpers

        private string CleanSepaText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            // Nur SEPA-erlaubte Zeichen
            // a-z, A-Z, 0-9, Leerzeichen, einige Sonderzeichen
            var cleaned = Regex.Replace(text, @"[^a-zA-Z0-9\s\-\.,\+\(\)/\?:]", "");

            // Umlaute ersetzen
            cleaned = cleaned
                .Replace("ae", "ae").Replace("oe", "oe").Replace("ue", "ue")
                .Replace("Ae", "Ae").Replace("Oe", "Oe").Replace("Ue", "Ue")
                .Replace("ss", "ss");

            // Max 70 Zeichen
            if (cleaned.Length > 70)
                cleaned = cleaned.Substring(0, 70);

            return cleaned.Trim();
        }

        #endregion
    }

    #region DTOs

    public class SepaConfig
    {
        public string FirmaName { get; set; } = "";
        public string IBAN { get; set; } = "";
        public string BIC { get; set; } = "";
        public string GlaeubigerId { get; set; } = ""; // DE-Glaeubiger-ID
    }

    public class SepaRechnung
    {
        public int KRechnung { get; set; }
        public string CRechnungsnummer { get; set; } = "";
        public decimal Brutto { get; set; }
        public decimal Offen { get; set; }
        public DateTime? Faelligkeit { get; set; }
        public int KKunde { get; set; }
        public string CKundenNr { get; set; } = "";
        public string KundeName { get; set; } = "";
        public string CIBAN { get; set; } = "";
        public string? CBIC { get; set; }
        public string? CKontoinhaber { get; set; }
        public string? CMandatsreferenz { get; set; }
        public DateTime? DMandatsDatum { get; set; }
    }

    public class SepaExportResult
    {
        public bool Erfolg { get; set; }
        public string XmlContent { get; set; } = "";
        public decimal GesamtBetrag { get; set; }
        public List<SepaRechnung> GueltigeRechnungen { get; set; } = new();
        public List<string> Fehler { get; set; } = new();
    }

    #endregion
}

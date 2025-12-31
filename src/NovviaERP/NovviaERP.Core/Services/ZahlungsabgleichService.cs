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
    /// Zahlungsabgleich-Service fuer Bank-Transaktionen
    /// - MT940/CAMT Import
    /// - HBCI/FinTS Abruf (Sparkasse)
    /// - Auto-Matching mit Rechnungen
    /// </summary>
    public class ZahlungsabgleichService : IDisposable
    {
        private readonly JtlDbContext _db;
        private static readonly ILogger _log = Log.ForContext<ZahlungsabgleichService>();

        public ZahlungsabgleichService(JtlDbContext db)
        {
            _db = db;
        }

        public void Dispose() { }

        #region MT940/CAMT Import

        /// <summary>
        /// Importiert MT940-Datei (SWIFT-Format fuer Kontoauszuege)
        /// </summary>
        public async Task<ImportResult> ImportMT940Async(string filePath)
        {
            var result = new ImportResult();

            try
            {
                if (!File.Exists(filePath))
                    throw new FileNotFoundException($"Datei nicht gefunden: {filePath}");

                var content = await File.ReadAllTextAsync(filePath, Encoding.GetEncoding("ISO-8859-1"));
                var transaktionen = ParseMT940(content);

                result.GesamtAnzahl = transaktionen.Count;

                foreach (var tx in transaktionen)
                {
                    var imported = await ImportTransaktionAsync(tx);
                    if (imported)
                        result.ImportiertAnzahl++;
                    else
                        result.UebersprungAnzahl++;
                }

                result.Erfolg = true;
                _log.Information("MT940 Import: {Importiert}/{Gesamt} Transaktionen",
                    result.ImportiertAnzahl, result.GesamtAnzahl);
            }
            catch (Exception ex)
            {
                result.Fehler = ex.Message;
                _log.Error(ex, "MT940 Import Fehler");
            }

            return result;
        }

        /// <summary>
        /// Importiert CAMT.053-Datei (XML-Format fuer Kontoauszuege)
        /// </summary>
        public async Task<ImportResult> ImportCAMTAsync(string filePath)
        {
            var result = new ImportResult();

            try
            {
                if (!File.Exists(filePath))
                    throw new FileNotFoundException($"Datei nicht gefunden: {filePath}");

                var content = await File.ReadAllTextAsync(filePath);
                var transaktionen = ParseCAMT(content);

                result.GesamtAnzahl = transaktionen.Count;

                foreach (var tx in transaktionen)
                {
                    var imported = await ImportTransaktionAsync(tx);
                    if (imported)
                        result.ImportiertAnzahl++;
                    else
                        result.UebersprungAnzahl++;
                }

                result.Erfolg = true;
                _log.Information("CAMT Import: {Importiert}/{Gesamt} Transaktionen",
                    result.ImportiertAnzahl, result.GesamtAnzahl);
            }
            catch (Exception ex)
            {
                result.Fehler = ex.Message;
                _log.Error(ex, "CAMT Import Fehler");
            }

            return result;
        }

        private List<BankTransaktion> ParseMT940(string content)
        {
            var transaktionen = new List<BankTransaktion>();

            // MT940 besteht aus Bloecken, getrennt durch :-Tags
            // :20: = Referenz
            // :25: = Kontonummer
            // :60F: = Anfangssaldo
            // :61: = Umsatzzeile
            // :86: = Verwendungszweck
            // :62F: = Schlusssaldo

            string kontoNr = "";
            BankTransaktion? current = null;

            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var buffer = new StringBuilder();

            foreach (var line in lines)
            {
                // Neue Tag-Zeile
                if (line.StartsWith(":"))
                {
                    // Vorherigen Buffer verarbeiten
                    if (current != null && buffer.Length > 0)
                    {
                        current.Verwendungszweck = CleanVerwendungszweck(buffer.ToString());
                        buffer.Clear();
                    }

                    if (line.StartsWith(":25:"))
                    {
                        // Kontonummer: :25:BLZKONTONR oder :25:IBAN
                        kontoNr = line.Substring(4).Trim();
                    }
                    else if (line.StartsWith(":61:"))
                    {
                        // Umsatzzeile: :61:JJMMTTJJMMTTCD12345,67NTRF...
                        // Format: Datum(6) + Buchungsdatum(4, optional) + C/D + Betrag + N + Buchungsschluessel
                        var umsatz = line.Substring(4);
                        current = ParseMT940Umsatz(umsatz, kontoNr);
                        if (current != null)
                            transaktionen.Add(current);
                    }
                    else if (line.StartsWith(":86:") && current != null)
                    {
                        // Verwendungszweck beginnt
                        buffer.Append(line.Substring(4));
                    }
                }
                else if (current != null && buffer.Length > 0)
                {
                    // Fortsetzung Verwendungszweck
                    buffer.Append(line);
                }
            }

            // Letzten Eintrag abschliessen
            if (current != null && buffer.Length > 0)
            {
                current.Verwendungszweck = CleanVerwendungszweck(buffer.ToString());
            }

            return transaktionen;
        }

        private BankTransaktion? ParseMT940Umsatz(string umsatz, string kontoNr)
        {
            try
            {
                // Format: JJMMTT[MMTT]C/D[RC]Betrag[,NN]NBuchungsschluessel//Referenz
                // Beispiel: 2312150C1234,56NTRFNONREF//

                if (umsatz.Length < 12)
                    return null;

                // Datum extrahieren (JJMMTT)
                var datumStr = umsatz.Substring(0, 6);
                var jahr = 2000 + int.Parse(datumStr.Substring(0, 2));
                var monat = int.Parse(datumStr.Substring(2, 2));
                var tag = int.Parse(datumStr.Substring(4, 2));
                var datum = new DateTime(jahr, monat, tag);

                // Soll/Haben (C = Credit/Haben, D = Debit/Soll)
                var pos = 6;
                if (char.IsDigit(umsatz[pos])) pos += 4; // Buchungsdatum ueberspringen

                var sollHaben = umsatz[pos];
                pos++;
                if (umsatz[pos] == 'R') pos++; // Storno-Kennzeichen

                // Betrag extrahieren
                var betragEnd = umsatz.IndexOf('N', pos);
                if (betragEnd < 0) return null;

                var betragStr = umsatz.Substring(pos, betragEnd - pos).Replace(",", ".");
                var betrag = decimal.Parse(betragStr, CultureInfo.InvariantCulture);

                // Bei Soll (D) negativ
                if (sollHaben == 'D')
                    betrag = -betrag;

                return new BankTransaktion
                {
                    KontoIdentifikation = kontoNr,
                    Buchungsdatum = datum,
                    Betrag = betrag,
                    Waehrung = "EUR"
                };
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "MT940 Umsatz Parse-Fehler: {Umsatz}", umsatz);
                return null;
            }
        }

        private List<BankTransaktion> ParseCAMT(string xml)
        {
            var transaktionen = new List<BankTransaktion>();

            try
            {
                // Einfacher XML-Parser fuer CAMT.053
                // Suche nach <Ntry> (Entry) Elementen

                var doc = System.Xml.Linq.XDocument.Parse(xml);
                var ns = doc.Root?.GetDefaultNamespace() ?? System.Xml.Linq.XNamespace.None;

                var entries = doc.Descendants(ns + "Ntry");

                foreach (var entry in entries)
                {
                    var tx = new BankTransaktion();

                    // Buchungsdatum
                    var bookgDt = entry.Element(ns + "BookgDt")?.Element(ns + "Dt")?.Value;
                    if (DateTime.TryParse(bookgDt, out var datum))
                        tx.Buchungsdatum = datum;

                    // Betrag
                    var amtElement = entry.Element(ns + "Amt");
                    if (decimal.TryParse(amtElement?.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var betrag))
                        tx.Betrag = betrag;
                    tx.Waehrung = amtElement?.Attribute("Ccy")?.Value ?? "EUR";

                    // Credit/Debit
                    var cdtDbt = entry.Element(ns + "CdtDbtInd")?.Value;
                    if (cdtDbt == "DBIT")
                        tx.Betrag = -tx.Betrag;

                    // Verwendungszweck
                    var rmtInf = entry.Descendants(ns + "Ustrd").FirstOrDefault()?.Value;
                    tx.Verwendungszweck = rmtInf ?? "";

                    // Name Gegenkonto
                    var nm = entry.Descendants(ns + "Nm").FirstOrDefault()?.Value;
                    tx.Name = nm ?? "";

                    // IBAN Gegenkonto
                    var iban = entry.Descendants(ns + "IBAN").FirstOrDefault()?.Value;
                    tx.Konto = iban ?? "";

                    // Transaktions-ID
                    var txId = entry.Descendants(ns + "TxId").FirstOrDefault()?.Value
                            ?? entry.Descendants(ns + "AcctSvcrRef").FirstOrDefault()?.Value;
                    tx.TransaktionsId = txId ?? Guid.NewGuid().ToString("N").Substring(0, 20);

                    transaktionen.Add(tx);
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "CAMT Parse-Fehler");
            }

            return transaktionen;
        }

        private string CleanVerwendungszweck(string raw)
        {
            // MT940 Verwendungszweck bereinigen
            // Entferne Strukturcodes wie ?20, ?21, etc.
            var result = Regex.Replace(raw, @"\?\d{2}", " ");
            result = Regex.Replace(result, @"\s+", " ");
            return result.Trim();
        }

        private async Task<bool> ImportTransaktionAsync(BankTransaktion tx)
        {
            var conn = await _db.GetConnectionAsync();

            // Pruefen ob bereits importiert (via TransaktionsId)
            var exists = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM dbo.tZahlungsabgleichUmsatz WHERE cTransaktionID = @Id",
                new { Id = tx.TransaktionsId });

            if (exists > 0)
                return false; // Bereits vorhanden

            // In JTL-Tabelle einfuegen
            await conn.ExecuteAsync(@"
                INSERT INTO dbo.tZahlungsabgleichUmsatz
                    (kZahlungsabgleichModul, cKontoIdentifikation, cTransaktionID, dBuchungsdatum,
                     fBetrag, cWaehrungISO, cName, cKonto, cVerwendungszweck, nSichtbar, nBuchungstyp, nStatus)
                VALUES
                    (1, @KontoId, @TxId, @Datum, @Betrag, @Waehrung, @Name, @Konto, @Vzweck, 1, @Typ, 0)",
                new
                {
                    KontoId = tx.KontoIdentifikation,
                    TxId = tx.TransaktionsId,
                    Datum = tx.Buchungsdatum,
                    Betrag = tx.Betrag,
                    Waehrung = tx.Waehrung,
                    Name = tx.Name ?? "",
                    Konto = tx.Konto ?? "",
                    Vzweck = tx.Verwendungszweck ?? "",
                    Typ = tx.Betrag >= 0 ? 0 : 2 // 0=Eingang, 2=Ausgang
                });

            return true;
        }

        #endregion

        #region Unmatched Transactions

        /// <summary>
        /// Holt alle nicht zugeordneten Zahlungseingaenge
        /// </summary>
        public async Task<IEnumerable<ZahlungsabgleichEintrag>> GetUnmatchedAsync()
        {
            var conn = await _db.GetConnectionAsync();

            return await conn.QueryAsync<ZahlungsabgleichEintrag>(@"
                SELECT
                    z.kZahlungsabgleichUmsatz AS Id,
                    z.cTransaktionID AS TransaktionsId,
                    z.dBuchungsdatum AS Buchungsdatum,
                    z.fBetrag AS Betrag,
                    z.cWaehrungISO AS Waehrung,
                    z.cName AS Name,
                    z.cKonto AS Konto,
                    z.cVerwendungszweck AS Verwendungszweck,
                    z.nStatus AS Status,
                    NULL AS RechnungsNr,
                    NULL AS KundenNr,
                    NULL AS MatchKonfidenz
                FROM dbo.tZahlungsabgleichUmsatz z
                WHERE z.nSichtbar = 1
                  AND z.fBetrag > 0
                  AND NOT EXISTS (
                      SELECT 1 FROM dbo.tZahlung za
                      WHERE za.kZahlungsabgleichUmsatz = z.kZahlungsabgleichUmsatz
                  )
                ORDER BY z.dBuchungsdatum DESC");
        }

        /// <summary>
        /// Holt alle Transaktionen (fuer Uebersicht)
        /// </summary>
        public async Task<IEnumerable<ZahlungsabgleichEintrag>> GetAllTransaktionenAsync(
            DateTime? von = null, DateTime? bis = null, bool nurUnmatched = false)
        {
            var conn = await _db.GetConnectionAsync();

            var sql = @"
                SELECT
                    z.kZahlungsabgleichUmsatz AS Id,
                    z.cTransaktionID AS TransaktionsId,
                    z.dBuchungsdatum AS Buchungsdatum,
                    z.fBetrag AS Betrag,
                    z.cWaehrungISO AS Waehrung,
                    z.cName AS Name,
                    z.cKonto AS Konto,
                    z.cVerwendungszweck AS Verwendungszweck,
                    z.nStatus AS Status,
                    r.cRechnungsnr AS RechnungsNr,
                    k.cKundenNr AS KundenNr,
                    CASE WHEN za.kZahlung IS NOT NULL THEN 100 ELSE 0 END AS MatchKonfidenz
                FROM dbo.tZahlungsabgleichUmsatz z
                LEFT JOIN dbo.tZahlung za ON za.kZahlungsabgleichUmsatz = z.kZahlungsabgleichUmsatz
                LEFT JOIN Rechnung.tRechnung r ON r.kRechnung = za.kRechnung
                LEFT JOIN dbo.tKunde k ON k.kKunde = r.kKunde
                WHERE z.nSichtbar = 1";

            if (von.HasValue) sql += " AND z.dBuchungsdatum >= @Von";
            if (bis.HasValue) sql += " AND z.dBuchungsdatum <= @Bis";
            if (nurUnmatched) sql += " AND za.kZahlung IS NULL AND z.fBetrag > 0";

            sql += " ORDER BY z.dBuchungsdatum DESC";

            return await conn.QueryAsync<ZahlungsabgleichEintrag>(sql, new { Von = von, Bis = bis });
        }

        #endregion

        #region Auto-Matching

        /// <summary>
        /// Fuehrt Auto-Matching fuer alle nicht zugeordneten Zahlungen durch
        /// </summary>
        public async Task<MatchingResult> MatchZahlungenAsync()
        {
            var result = new MatchingResult();
            var unmatched = await GetUnmatchedAsync();

            foreach (var zahlung in unmatched)
            {
                result.GesamtAnzahl++;

                var match = await FindBestMatchAsync(zahlung);

                if (match != null && match.Konfidenz >= 90)
                {
                    // Auto-Zuordnung bei hoher Konfidenz
                    await ZuordnenAsync(zahlung.Id, match.RechnungId, match.Betrag);
                    result.AutoGematchedAnzahl++;
                }
                else if (match != null && match.Konfidenz >= 50)
                {
                    // Vorschlag speichern
                    result.VorschlaegeAnzahl++;
                }
            }

            _log.Information("Auto-Matching: {Auto} auto, {Vorschlaege} Vorschlaege von {Gesamt}",
                result.AutoGematchedAnzahl, result.VorschlaegeAnzahl, result.GesamtAnzahl);

            return result;
        }

        /// <summary>
        /// Sucht die beste Rechnung fuer eine Zahlung
        /// </summary>
        public async Task<MatchVorschlag?> FindBestMatchAsync(ZahlungsabgleichEintrag zahlung)
        {
            var conn = await _db.GetConnectionAsync();

            // 1. Rechnungsnummer im Verwendungszweck suchen
            var rechnungsNr = ExtractRechnungsNr(zahlung.Verwendungszweck);
            if (!string.IsNullOrEmpty(rechnungsNr))
            {
                var rechnung = await conn.QueryFirstOrDefaultAsync<OffeneRechnung>(@"
                    SELECT r.kRechnung, r.cRechnungsnr AS CRechnungsnummer, re.fVkBruttoGesamt AS Brutto,
                           re.fOffenerWert AS Offen, k.cKundenNr, k.kKunde
                    FROM Rechnung.tRechnung r
                    JOIN Rechnung.tRechnungEckdaten re ON r.kRechnung = re.kRechnung
                    JOIN dbo.tKunde k ON r.kKunde = k.kKunde
                    WHERE r.cRechnungsnr LIKE @Nr AND re.nZahlungStatus = 1 AND r.nStorno = 0",
                    new { Nr = $"%{rechnungsNr}%" });

                if (rechnung != null)
                {
                    return new MatchVorschlag
                    {
                        RechnungId = rechnung.KRechnung,
                        RechnungsNr = rechnung.CRechnungsnummer,
                        KundeId = rechnung.KKunde,
                        KundenNr = rechnung.CKundenNr,
                        RechnungsBetrag = rechnung.Offen,
                        Betrag = Math.Min(zahlung.Betrag, rechnung.Offen),
                        Konfidenz = zahlung.Betrag == rechnung.Offen ? 100 : 90
                    };
                }
            }

            // 2. Bestellnummer im Verwendungszweck suchen
            var bestellNr = ExtractBestellNr(zahlung.Verwendungszweck);
            if (!string.IsNullOrEmpty(bestellNr))
            {
                var rechnung = await conn.QueryFirstOrDefaultAsync<OffeneRechnung>(@"
                    SELECT r.kRechnung, r.cRechnungsnr AS CRechnungsnummer, re.fVkBruttoGesamt AS Brutto,
                           re.fOffenerWert AS Offen, k.cKundenNr, k.kKunde
                    FROM Rechnung.tRechnung r
                    JOIN Rechnung.tRechnungEckdaten re ON r.kRechnung = re.kRechnung
                    JOIN dbo.tKunde k ON r.kKunde = k.kKunde
                    JOIN Verkauf.tAuftragRechnung ar ON ar.kRechnung = r.kRechnung
                    JOIN Verkauf.tAuftrag b ON ar.kAuftrag = b.kAuftrag
                    WHERE (b.cAuftragsNr LIKE @Nr OR b.cExterneAuftragsnummer LIKE @Nr)
                      AND re.nZahlungStatus = 1 AND r.nStorno = 0",
                    new { Nr = $"%{bestellNr}%" });

                if (rechnung != null)
                {
                    return new MatchVorschlag
                    {
                        RechnungId = rechnung.KRechnung,
                        RechnungsNr = rechnung.CRechnungsnummer,
                        KundeId = rechnung.KKunde,
                        KundenNr = rechnung.CKundenNr,
                        RechnungsBetrag = rechnung.Offen,
                        Betrag = Math.Min(zahlung.Betrag, rechnung.Offen),
                        Konfidenz = zahlung.Betrag == rechnung.Offen ? 95 : 80
                    };
                }
            }

            // 3. IBAN-Abgleich mit Rechnungs-Zahlungsinfo
            if (!string.IsNullOrEmpty(zahlung.Konto))
            {
                var rechnung = await conn.QueryFirstOrDefaultAsync<OffeneRechnung>(@"
                    SELECT TOP 1 r.kRechnung, r.cRechnungsnr AS CRechnungsnummer, re.fVkBruttoGesamt AS Brutto,
                           re.fOffenerWert AS Offen, k.cKundenNr, k.kKunde
                    FROM Rechnung.tRechnung r
                    JOIN Rechnung.tRechnungEckdaten re ON r.kRechnung = re.kRechnung
                    JOIN dbo.tKunde k ON r.kKunde = k.kKunde
                    LEFT JOIN Rechnung.tRechnungZahlungsinfo rzi ON r.kRechnung = rzi.kRechnung
                    WHERE rzi.cIBAN = @IBAN AND re.nZahlungStatus = 1 AND r.nStorno = 0
                    ORDER BY ABS(re.fOffenerWert - @Betrag)",
                    new { IBAN = zahlung.Konto.Replace(" ", ""), Betrag = zahlung.Betrag });

                if (rechnung != null)
                {
                    var konfidenz = zahlung.Betrag == rechnung.Offen ? 85 : 70;
                    return new MatchVorschlag
                    {
                        RechnungId = rechnung.KRechnung,
                        RechnungsNr = rechnung.CRechnungsnummer,
                        KundeId = rechnung.KKunde,
                        KundenNr = rechnung.CKundenNr,
                        RechnungsBetrag = rechnung.Offen,
                        Betrag = Math.Min(zahlung.Betrag, rechnung.Offen),
                        Konfidenz = konfidenz
                    };
                }
            }

            // 4. Betragsabgleich (nur bei exaktem Match)
            var betragMatch = await conn.QueryFirstOrDefaultAsync<OffeneRechnung>(@"
                SELECT TOP 1 r.kRechnung, r.cRechnungsnr AS CRechnungsnummer, re.fVkBruttoGesamt AS Brutto,
                       re.fOffenerWert AS Offen, k.cKundenNr, k.kKunde
                FROM Rechnung.tRechnung r
                JOIN Rechnung.tRechnungEckdaten re ON r.kRechnung = re.kRechnung
                JOIN dbo.tKunde k ON r.kKunde = k.kKunde
                WHERE ABS(re.fOffenerWert - @Betrag) < 0.01
                  AND re.nZahlungStatus = 1 AND r.nStorno = 0
                ORDER BY r.dErstellt DESC",
                new { Betrag = zahlung.Betrag });

            if (betragMatch != null)
            {
                return new MatchVorschlag
                {
                    RechnungId = betragMatch.KRechnung,
                    RechnungsNr = betragMatch.CRechnungsnummer,
                    KundeId = betragMatch.KKunde,
                    KundenNr = betragMatch.CKundenNr,
                    RechnungsBetrag = betragMatch.Offen,
                    Betrag = zahlung.Betrag,
                    Konfidenz = 50 // Nur Betrag passt - niedrige Konfidenz
                };
            }

            return null;
        }

        private string? ExtractRechnungsNr(string? verwendungszweck)
        {
            if (string.IsNullOrEmpty(verwendungszweck)) return null;

            // Muster: RE-12345, RG12345, RG-12345, Rechnung 12345, etc.
            var patterns = new[]
            {
                @"RE[-\s]?(\d{4,})",
                @"RG[-\s]?(\d{4,})",
                @"Rechnung\s*[-:]?\s*(\d{4,})",
                @"Invoice\s*[-:]?\s*(\d{4,})"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(verwendungszweck, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                    return match.Groups[1].Value;
            }

            return null;
        }

        private string? ExtractBestellNr(string? verwendungszweck)
        {
            if (string.IsNullOrEmpty(verwendungszweck)) return null;

            // Muster: AU-12345, B12345, Bestellung 12345, Order 12345, etc.
            var patterns = new[]
            {
                @"AU[-\s]?(\d{4,})",
                @"B[-\s]?(\d{4,})",
                @"Bestellung\s*[-:]?\s*(\d{4,})",
                @"Order\s*[-:]?\s*(\d{4,})",
                @"POR[-\s]?(\d{4,})"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(verwendungszweck, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                    return match.Groups[1].Value;
            }

            return null;
        }

        #endregion

        #region Manuelle Zuordnung

        /// <summary>
        /// Sucht offene Rechnungen fuer manuelle Zuordnung
        /// </summary>
        public async Task<IEnumerable<OffeneRechnung>> SucheOffeneRechnungenAsync(string? suchbegriff = null)
        {
            var conn = await _db.GetConnectionAsync();

            var sql = @"
                SELECT TOP 100 r.kRechnung, r.cRechnungsnr AS CRechnungsnummer,
                       re.fVkBruttoGesamt AS Brutto, re.fOffenerWert AS Offen,
                       k.cKundenNr, k.kKunde,
                       k.cFirma, k.cVorname, k.cName AS KundeName
                FROM Rechnung.tRechnung r
                JOIN Rechnung.tRechnungEckdaten re ON r.kRechnung = re.kRechnung
                JOIN dbo.tKunde k ON r.kKunde = k.kKunde
                WHERE re.nZahlungStatus = 1 AND r.nStorno = 0 AND re.fOffenerWert > 0";

            if (!string.IsNullOrEmpty(suchbegriff))
            {
                sql += @" AND (r.cRechnungsnr LIKE @Such
                        OR k.cKundenNr LIKE @Such
                        OR k.cFirma LIKE @Such
                        OR k.cName LIKE @Such)";
            }

            sql += " ORDER BY r.dErstellt DESC";

            return await conn.QueryAsync<OffeneRechnung>(sql, new { Such = $"%{suchbegriff}%" });
        }

        /// <summary>
        /// Ordnet eine Zahlung manuell einer Rechnung zu
        /// </summary>
        public async Task ZuordnenAsync(int zahlungsabgleichId, int rechnungId, decimal betrag)
        {
            var conn = await _db.GetConnectionAsync();

            // Rechnung laden
            var rechnung = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT r.kRechnung, r.kKunde, r.kAuftrag, re.fOffenerWert
                FROM Rechnung.tRechnung r
                JOIN Rechnung.tRechnungEckdaten re ON r.kRechnung = re.kRechnung
                WHERE r.kRechnung = @Id",
                new { Id = rechnungId });

            if (rechnung == null)
                throw new Exception($"Rechnung {rechnungId} nicht gefunden");

            // Zahlung in tZahlung einfuegen
            await conn.ExecuteAsync(@"
                INSERT INTO dbo.tZahlung
                    (cName, dDatum, fBetrag, kBestellung, kRechnung, kZahlungsart, kZahlungsabgleichUmsatz, nZahlungstyp)
                SELECT
                    'Bankeingang', z.dBuchungsdatum, @Betrag, @Bestellung, @Rechnung, 2, z.kZahlungsabgleichUmsatz, 0
                FROM dbo.tZahlungsabgleichUmsatz z
                WHERE z.kZahlungsabgleichUmsatz = @ZaId",
                new {
                    Betrag = betrag,
                    Bestellung = (int?)rechnung.kAuftrag,
                    Rechnung = rechnungId,
                    ZaId = zahlungsabgleichId
                });

            // Rechnung Zahlungsstatus aktualisieren
            var offen = (decimal)rechnung.fOffenerWert - betrag;
            var status = offen <= 0.01m ? 3 : 2; // 3=bezahlt, 2=teilweise bezahlt

            await conn.ExecuteAsync(@"
                UPDATE Rechnung.tRechnungEckdaten
                SET fOffenerWert = @Offen, nZahlungStatus = @Status, dBezahlt = GETDATE()
                WHERE kRechnung = @Id",
                new { Offen = Math.Max(0, offen), Status = status, Id = rechnungId });

            _log.Information("Zahlung {ZaId} zugeordnet zu Rechnung {ReId}, Betrag {Betrag}",
                zahlungsabgleichId, rechnungId, betrag);
        }

        #endregion
    }

    #region DTOs

    public class BankTransaktion
    {
        public string TransaktionsId { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 20);
        public string KontoIdentifikation { get; set; } = "";
        public DateTime Buchungsdatum { get; set; }
        public decimal Betrag { get; set; }
        public string Waehrung { get; set; } = "EUR";
        public string? Name { get; set; }
        public string? Konto { get; set; }
        public string? Verwendungszweck { get; set; }
    }

    public class ZahlungsabgleichEintrag
    {
        public int Id { get; set; }
        public string TransaktionsId { get; set; } = "";
        public DateTime Buchungsdatum { get; set; }
        public decimal Betrag { get; set; }
        public string Waehrung { get; set; } = "EUR";
        public string? Name { get; set; }
        public string? Konto { get; set; }
        public string? Verwendungszweck { get; set; }
        public int Status { get; set; }
        public string? RechnungsNr { get; set; }
        public string? KundenNr { get; set; }
        public int MatchKonfidenz { get; set; }
    }

    public class MatchVorschlag
    {
        public int RechnungId { get; set; }
        public string RechnungsNr { get; set; } = "";
        public int KundeId { get; set; }
        public string KundenNr { get; set; } = "";
        public decimal RechnungsBetrag { get; set; }
        public decimal Betrag { get; set; }
        public int Konfidenz { get; set; } // 0-100
    }

    public class OffeneRechnung
    {
        public int KRechnung { get; set; }
        public string CRechnungsnummer { get; set; } = "";
        public decimal Brutto { get; set; }
        public decimal Offen { get; set; }
        public int KKunde { get; set; }
        public string CKundenNr { get; set; } = "";
        public string? CFirma { get; set; }
        public string? CVorname { get; set; }
        public string? KundeName { get; set; }

        public string KundeDisplay => !string.IsNullOrEmpty(CFirma) ? CFirma :
            $"{CVorname} {KundeName}".Trim();
    }

    public class ImportResult
    {
        public bool Erfolg { get; set; }
        public int GesamtAnzahl { get; set; }
        public int ImportiertAnzahl { get; set; }
        public int UebersprungAnzahl { get; set; }
        public string? Fehler { get; set; }
    }

    public class MatchingResult
    {
        public int GesamtAnzahl { get; set; }
        public int AutoGematchedAnzahl { get; set; }
        public int VorschlaegeAnzahl { get; set; }
    }

    #endregion
}

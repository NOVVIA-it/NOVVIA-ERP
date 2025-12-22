using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Dapper;
using Serilog;

namespace NovviaERP.Core.Services
{
    /// <summary>
    /// Einkauf-Service für Lieferantenbestellungen, Eingangsrechnungen
    /// Nutzt JTL Stored Procedures wo vorhanden
    /// </summary>
    public class EinkaufService
    {
        private readonly string _connectionString;
        private static readonly ILogger _log = Log.ForContext<EinkaufService>();

        public EinkaufService(string connectionString)
        {
            _connectionString = connectionString;
        }

        #region Lieferanten

        /// <summary>Lieferanten-Übersicht</summary>
        public async Task<IEnumerable<LieferantUebersicht>> GetLieferantenUebersichtAsync(string? suche = null, bool nurAktive = true, bool nurMSV3 = false)
        {
            using var conn = new SqlConnection(_connectionString);
            var sql = @"
                SELECT
                    l.kLieferant AS KLieferant,
                    l.cLiefNr AS CLiefNr,
                    l.cFirma AS CFirma,
                    l.cStrasse AS CStrasse,
                    l.cPLZ AS CPLZ,
                    l.cOrt AS COrt,
                    COALESCE(l.cTelZentralle, l.cTelDurchwahl, '') AS CTel,
                    l.cEMail AS CEmail,
                    CAST(CASE WHEN m.kMSV3Lieferant IS NOT NULL AND m.nAktiv = 1 THEN 1 ELSE 0 END AS BIT) AS NHatMSV3,
                    0 AS NOffeneBestellungen,
                    0 AS NOffeneRechnungen
                FROM tLieferant l
                LEFT JOIN NOVVIA.MSV3Lieferant m ON l.kLieferant = m.kLieferant
                WHERE 1=1";
            if (nurAktive) sql += " AND l.cAktiv = 'Y'";
            if (!string.IsNullOrEmpty(suche)) sql += " AND (l.cFirma LIKE @Suche OR l.cLiefNr LIKE @Suche)";
            if (nurMSV3) sql += " AND m.kMSV3Lieferant IS NOT NULL AND m.nAktiv = 1";
            sql += " ORDER BY l.cFirma";
            return await conn.QueryAsync<LieferantUebersicht>(sql, new { Suche = $"%{suche}%" });
        }

        /// <summary>Alle Lieferanten laden (für Dropdowns)</summary>
        public async Task<IEnumerable<Lieferant>> GetLieferantenAsync()
        {
            using var conn = new SqlConnection(_connectionString);
            return await conn.QueryAsync<Lieferant>(@"
                SELECT kLieferant AS KLieferant, cLiefNr AS LiefNr, cFirma AS Firma, cStrasse AS Strasse,
                       cPLZ AS PLZ, cOrt AS Ort, ISNULL(cTelZentralle, cTelDurchwahl) AS Tel, cEMail AS EMail
                FROM tlieferant WHERE cAktiv = 'Y' ORDER BY cFirma");
        }

        /// <summary>Lieferant Details laden</summary>
        public async Task<Lieferant?> GetLieferantAsync(int kLieferant)
        {
            using var conn = new SqlConnection(_connectionString);
            return await conn.QuerySingleOrDefaultAsync<Lieferant>(
                "SELECT * FROM tlieferant WHERE kLieferant = @Id", new { Id = kLieferant });
        }

        /// <summary>Vollständige Lieferanten-Stammdaten laden</summary>
        public async Task<LieferantStammdaten?> GetLieferantStammdatenAsync(int kLieferant)
        {
            using var conn = new SqlConnection(_connectionString);
            return await conn.QuerySingleOrDefaultAsync<LieferantStammdaten>(@"
                SELECT
                    l.kLieferant AS KLieferant,
                    l.cLiefNr AS LiefNr,
                    l.cFirma AS Firma,
                    l.cAnsprechpartner AS Ansprechpartner,
                    l.cStrasse AS Strasse,
                    l.cPLZ AS PLZ,
                    l.cOrt AS Ort,
                    l.cLand AS Land,
                    COALESCE(l.cTelZentralle, l.cTelDurchwahl) AS Tel,
                    l.cFax AS Fax,
                    l.cEMail AS EMail,
                    l.cHomepage AS Homepage,
                    l.cUstId AS UstId,
                    l.cGLN AS GLN,
                    l.nKreditorNr AS KreditorNr,
                    l.cBankname AS Bankname,
                    l.cIBAN AS IBAN,
                    l.cBIC AS BIC,
                    l.cKontoInhaber AS KontoInhaber,
                    l.nZahlungsziel AS Zahlungsziel,
                    ISNULL(l.fSkonto, 0) AS Skonto,
                    ISNULL(l.nSkontoTage, 0) AS SkontoTage,
                    ISNULL(l.fMindestbestellwert, 0) AS Mindestbestellwert,
                    ISNULL(l.nLieferzeit, 0) AS Lieferzeit,
                    ISNULL(l.fRabatt, 0) AS Rabatt,
                    l.cAktiv AS Aktiv,
                    l.cNotiz AS Notiz
                FROM tlieferant l
                WHERE l.kLieferant = @Id", new { Id = kLieferant });
        }

        /// <summary>NOVVIA-Erweiterung der Lieferanten-Stammdaten laden</summary>
        public async Task<LieferantErweitert?> GetLieferantErweitertAsync(int kLieferant)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                return await conn.QuerySingleOrDefaultAsync<LieferantErweitert>(
                    "spNOVVIA_LieferantErweitertLaden",
                    new { kLieferant },
                    commandType: CommandType.StoredProcedure);
            }
            catch
            {
                // Tabelle existiert noch nicht
                return null;
            }
        }

        /// <summary>NOVVIA-Erweiterung der Lieferanten-Stammdaten speichern</summary>
        public async Task SaveLieferantErweitertAsync(LieferantErweitert data)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.ExecuteAsync(
                "spNOVVIA_LieferantErweitertSpeichern",
                new
                {
                    kLieferant = data.KLieferant,
                    nAmbient = data.Ambient,
                    nCool = data.Cool,
                    nMedcan = data.Medcan,
                    nTierarznei = data.Tierarznei,
                    dQualifiziertAm = data.QualifiziertAm,
                    cQualifiziertVon = data.QualifiziertVon,
                    cQualifikationsDocs = data.QualifikationsDocs
                },
                commandType: CommandType.StoredProcedure);
        }

        #endregion

        #region Lieferantenbestellungen

        /// <summary>Lieferantenbestellung erstellen (via JTL SP)</summary>
        public async Task<int> CreateBestellungAsync(int kLieferant, int kBenutzer, int kFirma = 1, int kLager = 1,
            string? bestellnummer = null, string? kommentar = null, DateTime? lieferdatum = null, bool viaMSV3 = false)
        {
            using var conn = new SqlConnection(_connectionString);
            var p = new DynamicParameters();
            p.Add("@kLieferant", kLieferant);
            p.Add("@kBenutzer", kBenutzer);
            p.Add("@kFirma", kFirma);
            p.Add("@kLager", kLager);
            p.Add("@cEigeneBestellnummer", bestellnummer);
            p.Add("@cInternerKommentar", kommentar);
            p.Add("@dLieferdatum", lieferdatum);
            p.Add("@nViaMSV3", viaMSV3 ? 1 : 0);
            p.Add("@kLieferantenBestellung", dbType: DbType.Int32, direction: ParameterDirection.Output);

            await conn.ExecuteAsync("spNOVVIA_LieferantenBestellungErstellen", p, commandType: CommandType.StoredProcedure);
            return p.Get<int>("@kLieferantenBestellung");
        }

        /// <summary>Position hinzufügen (via JTL SP)</summary>
        public async Task<int> AddPositionAsync(int kLieferantenBestellung, int kArtikel, decimal menge, decimal? ekNetto = null, string? lieferantenArtNr = null, string? hinweis = null)
        {
            using var conn = new SqlConnection(_connectionString);
            var p = new DynamicParameters();
            p.Add("@kLieferantenBestellung", kLieferantenBestellung);
            p.Add("@kArtikel", kArtikel);
            p.Add("@fMenge", menge);
            p.Add("@fEKNetto", ekNetto);
            p.Add("@cLieferantenArtNr", lieferantenArtNr);
            p.Add("@cHinweis", hinweis);
            p.Add("@kLieferantenBestellungPos", dbType: DbType.Int32, direction: ParameterDirection.Output);

            await conn.ExecuteAsync("spNOVVIA_LieferantenBestellungPosErstellen", p, commandType: CommandType.StoredProcedure);
            return p.Get<int>("@kLieferantenBestellungPos");
        }

        /// <summary>Status ändern (via JTL SP)</summary>
        public async Task UpdateStatusAsync(int kLieferantenBestellung, int nStatus)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.ExecuteAsync("spNOVVIA_LieferantenBestellungStatusAendern",
                new { kLieferantenBestellung, nStatus }, commandType: CommandType.StoredProcedure);
        }

        /// <summary>Offene Bestellungen laden</summary>
        public async Task<IEnumerable<LieferantenBestellungUebersicht>> GetBestellungenAsync(int? status = null, int? kLieferant = null)
        {
            using var conn = new SqlConnection(_connectionString);
            var sql = @"SELECT b.kLieferantenBestellung, b.kLieferant, b.cEigeneBestellnummer AS CEigeneBestellnummer,
                        b.dErstellt AS DErstellt, b.dLieferdatum AS DLieferdatum, b.nStatus AS NStatus,
                        b.cInternerKommentar AS CInternerKommentar, l.cFirma AS LieferantName,
                        (SELECT COUNT(*) FROM tLieferantenBestellungPos WHERE kLieferantenBestellung = b.kLieferantenBestellung) AS AnzahlPositionen,
                        ISNULL((SELECT SUM(fMenge * fEKNetto) FROM tLieferantenBestellungPos WHERE kLieferantenBestellung = b.kLieferantenBestellung), 0) AS Summe
                        FROM tLieferantenBestellung b
                        INNER JOIN tlieferant l ON b.kLieferant = l.kLieferant
                        WHERE ISNULL(b.nDeleted, 0) = 0";
            if (status.HasValue) sql += " AND b.nStatus = @Status";
            if (kLieferant.HasValue) sql += " AND b.kLieferant = @Lieferant";
            sql += " ORDER BY b.dErstellt DESC";

            return await conn.QueryAsync<LieferantenBestellungUebersicht>(sql, new { Status = status, Lieferant = kLieferant });
        }

        /// <summary>Bestellung mit Positionen laden</summary>
        public async Task<LieferantenBestellung?> GetBestellungAsync(int kLieferantenBestellung, bool mitLieferantenAuswahl = false)
        {
            using var conn = new SqlConnection(_connectionString);
            var bestellung = await conn.QuerySingleOrDefaultAsync<LieferantenBestellung>(@"
                SELECT b.*, l.cFirma AS LieferantName FROM tLieferantenBestellung b
                INNER JOIN tlieferant l ON b.kLieferant = l.kLieferant
                WHERE b.kLieferantenBestellung = @Id", new { Id = kLieferantenBestellung });

            if (bestellung != null)
            {
                bestellung.Positionen = (await GetBestellungPositionenAsync(kLieferantenBestellung)).ToList();

                // Verfügbare Lieferanten pro Position laden (für MSV3-Auswahl)
                if (mitLieferantenAuswahl && bestellung.Positionen.Any())
                {
                    var artikelIds = bestellung.Positionen.Select(p => p.KArtikel).Distinct().ToList();
                    var lieferantenMap = await GetArtikelLieferantenMapAsync(artikelIds);

                    foreach (var pos in bestellung.Positionen)
                    {
                        if (lieferantenMap.TryGetValue(pos.KArtikel, out var lieferanten))
                        {
                            pos.VerfuegbareLieferanten = lieferanten;
                            // Günstigsten MSV3-Lieferant vorselektieren (nach EK-Preis)
                            var msv3Lieferanten = lieferanten.Where(l => l.HatMSV3).ToList();
                            var defaultLief = msv3Lieferanten.OrderBy(l => l.EKNetto).FirstOrDefault()
                                ?? lieferanten.OrderBy(l => l.EKNetto).FirstOrDefault(l => l.IstStandard)
                                ?? lieferanten.OrderBy(l => l.EKNetto).FirstOrDefault();
                            if (defaultLief != null)
                            {
                                pos.SelectedKLieferant = defaultLief.KLieferant;
                                pos.SelectedKMSV3Lieferant = defaultLief.KMSV3Lieferant;
                                pos.SelectedLieferantName = defaultLief.LieferantName;
                                // EK-Preis NICHT überschreiben - bleibt aus tLieferantenBestellungPos
                                pos.IstAusgewaehlt = true; // Standardmäßig ausgewählt
                            }
                        }
                    }
                }
            }

            return bestellung;
        }

        /// <summary>Bestellpositionen laden</summary>
        public async Task<IEnumerable<LieferantenBestellungPos>> GetBestellungPositionenAsync(int kLieferantenBestellung)
        {
            using var conn = new SqlConnection(_connectionString);
            // PZN aus verschiedenen Quellen: ABdataMapping, HAN (Hersteller-Art-Nr), oder Attribut
            return await conn.QueryAsync<LieferantenBestellungPos>(@"
                SELECT p.kLieferantenBestellungPos, p.kArtikel, a.cArtNr AS CArtNr, ab.cName AS ArtikelName,
                       p.cLieferantenArtNr AS CLieferantenArtNr, p.fMenge AS FMenge,
                       ISNULL(p.fMengeGeliefert, 0) AS FMengeGeliefert, p.fEKNetto AS FEKNetto,
                       p.cHinweis AS CHinweis, p.nSort AS NSort,
                       a.cArtNr AS CPZN
                FROM tLieferantenBestellungPos p
                INNER JOIN tArtikel a ON p.kArtikel = a.kArtikel
                LEFT JOIN tArtikelBeschreibung ab ON a.kArtikel = ab.kArtikel AND ab.kSprache = 1
                WHERE p.kLieferantenBestellung = @Id ORDER BY p.nSort",
                new { Id = kLieferantenBestellung });
        }

        /// <summary>Verfügbare Lieferanten für mehrere Artikel laden (aus tLiefArtikel + MSV3-Info)</summary>
        public async Task<Dictionary<int, List<ArtikelMSV3Lieferant>>> GetArtikelLieferantenMapAsync(IEnumerable<int> artikelIds)
        {
            var result = new Dictionary<int, List<ArtikelMSV3Lieferant>>();
            if (!artikelIds.Any()) return result;

            try
            {
                using var conn = new SqlConnection(_connectionString);
                var lieferanten = await conn.QueryAsync<ArtikelMSV3Lieferant>(@"
                    SELECT
                        la.tArtikel_kArtikel AS KArtikel,
                        la.tLieferant_kLieferant AS KLieferant,
                        l.cFirma AS LieferantName,
                        l.cLiefNr AS LieferantNr,
                        NULL AS LieferantenArtNr,
                        ISNULL(la.fEKNetto, 0) AS EKNetto,
                        0 AS Prioritaet,
                        ISNULL(la.nStandard, 0) AS IstStandard,
                        m.kMSV3Lieferant AS KMSV3Lieferant,
                        m.cMSV3Url AS MSV3Url,
                        m.nMSV3Version AS MSV3Version,
                        CAST(CASE WHEN m.kMSV3Lieferant IS NOT NULL AND m.nAktiv = 1 THEN 1 ELSE 0 END AS BIT) AS HatMSV3
                    FROM tLiefArtikel la
                    INNER JOIN tLieferant l ON la.tLieferant_kLieferant = l.kLieferant
                    LEFT JOIN NOVVIA.MSV3Lieferant m ON la.tLieferant_kLieferant = m.kLieferant AND m.nAktiv = 1
                    WHERE la.tArtikel_kArtikel IN @artikelIds AND l.cAktiv = 'Y'
                    ORDER BY la.tArtikel_kArtikel, ISNULL(m.nPrioritaet, 99), la.nStandard DESC",
                    new { artikelIds });

                foreach (var l in lieferanten)
                {
                    if (!result.ContainsKey(l.KArtikel))
                        result[l.KArtikel] = new List<ArtikelMSV3Lieferant>();
                    result[l.KArtikel].Add(l);
                }
            }
            catch { }

            return result;
        }

        #endregion

        #region Eingangsrechnungen

        /// <summary>Eingangsrechnungen laden</summary>
        public async Task<IEnumerable<EingangsrechnungUebersicht>> GetEingangsrechnungenAsync(bool? geprueft = null, bool? freigegeben = null, int? kLieferant = null)
        {
            using var conn = new SqlConnection(_connectionString);
            var sql = @"SELECT e.kEingangsrechnung AS KEingangsrechnung, e.kLieferant AS KLieferant,
                        e.cFremdbelegnummer AS CFremdbelegnummer, e.cEigeneRechnungsnummer AS CEigeneRechnungsnummer,
                        e.dErstellt AS DErstellt, e.dBelegdatum AS DBelegdatum, e.dZahlungsziel AS DZahlungsziel,
                        e.nStatus AS NStatus, l.cFirma AS LieferantName,
                        ISNULL(x.nGeprueft, 0) AS NGeprueft, ISNULL(x.nFreigegeben, 0) AS NFreigegeben, x.dSkontoFrist AS DSkontoFrist
                        FROM tEingangsrechnung e
                        INNER JOIN tlieferant l ON e.kLieferant = l.kLieferant
                        LEFT JOIN NOVVIA.EingangsrechnungErweitert x ON e.kEingangsrechnung = x.kEingangsrechnung
                        WHERE ISNULL(e.nDeleted, 0) = 0";
            if (geprueft.HasValue) sql += $" AND ISNULL(x.nGeprueft, 0) = {(geprueft.Value ? 1 : 0)}";
            if (freigegeben.HasValue) sql += $" AND ISNULL(x.nFreigegeben, 0) = {(freigegeben.Value ? 1 : 0)}";
            if (kLieferant.HasValue) sql += " AND e.kLieferant = @Lieferant";
            sql += " ORDER BY e.dErstellt DESC";

            return await conn.QueryAsync<EingangsrechnungUebersicht>(sql, new { Lieferant = kLieferant });
        }

        /// <summary>Eingangsrechnung Details speichern</summary>
        public async Task SaveEingangsrechnungErweitertAsync(int kEingangsrechnung, int? skontoTage = null,
            decimal? skontoProzent = null, string? zahlungsreferenz = null, string? bankverbindung = null, string? dokumentPfad = null)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.ExecuteAsync("spNOVVIA_EingangsrechnungErweitertSpeichern",
                new { kEingangsrechnung, nSkontoTage = skontoTage, fSkontoProzent = skontoProzent,
                      cZahlungsreferenz = zahlungsreferenz, cBankverbindung = bankverbindung, cDokumentPfad = dokumentPfad },
                commandType: CommandType.StoredProcedure);
        }

        /// <summary>Eingangsrechnung prüfen und freigeben</summary>
        public async Task PruefenUndFreigebenAsync(int kEingangsrechnung, bool pruefen, bool freigeben, int kBenutzer, string? hinweis = null)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.ExecuteAsync("spNOVVIA_EingangsrechnungPruefenUndFreigeben",
                new { kEingangsrechnung, kBenutzer, nPruefen = pruefen ? 1 : 0, nFreigeben = freigeben ? 1 : 0, cPruefHinweis = hinweis },
                commandType: CommandType.StoredProcedure);
        }

        #endregion

        #region Einkaufsliste

        /// <summary>Einkaufsliste mit Pharma-Infos</summary>
        public async Task<IEnumerable<EinkaufslisteItem>> GetEinkaufslisteAsync(int? kBenutzer = null, int? kLieferant = null)
        {
            using var conn = new SqlConnection(_connectionString);
            return await conn.QueryAsync<EinkaufslisteItem>(
                "EXEC spNOVVIA_EinkaufslisteMitPharmaInfos @kBenutzer, @kLieferant",
                new { kBenutzer, kLieferant });
        }

        /// <summary>Aus Einkaufsliste bestellen</summary>
        public async Task<int> BestelleAusEinkaufslisteAsync(int kLieferant, int kBenutzer, IEnumerable<int> artikelIds, bool viaMSV3 = false)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();

            try
            {
                // Bestellung erstellen
                var p = new DynamicParameters();
                p.Add("@kLieferant", kLieferant);
                p.Add("@kBenutzer", kBenutzer);
                p.Add("@kFirma", 1);
                p.Add("@kLager", 1);
                p.Add("@nViaMSV3", viaMSV3 ? 1 : 0);
                p.Add("@kLieferantenBestellung", dbType: DbType.Int32, direction: ParameterDirection.Output);

                await conn.ExecuteAsync("spNOVVIA_LieferantenBestellungErstellen", p, tx, commandType: CommandType.StoredProcedure);
                var bestellungId = p.Get<int>("@kLieferantenBestellung");

                // Positionen aus Einkaufsliste hinzufügen
                foreach (var artikelId in artikelIds)
                {
                    var eintrag = await conn.QuerySingleOrDefaultAsync<dynamic>(
                        "SELECT kArtikel, fAnzahl, fEKNettoLieferant FROM tArtikelEinkaufsliste WHERE kArtikelEinkaufsliste = @Id",
                        new { Id = artikelId }, tx);

                    if (eintrag != null)
                    {
                        var pp = new DynamicParameters();
                        pp.Add("@kLieferantenBestellung", bestellungId);
                        pp.Add("@kArtikel", (int)eintrag.kArtikel);
                        pp.Add("@fMenge", (decimal)eintrag.fAnzahl);
                        pp.Add("@fEKNetto", eintrag.fEKNettoLieferant as decimal?);
                        pp.Add("@kLieferantenBestellungPos", dbType: DbType.Int32, direction: ParameterDirection.Output);

                        await conn.ExecuteAsync("spNOVVIA_LieferantenBestellungPosErstellen", pp, tx, commandType: CommandType.StoredProcedure);

                        // Aus Einkaufsliste entfernen
                        await conn.ExecuteAsync("DELETE FROM tArtikelEinkaufsliste WHERE kArtikelEinkaufsliste = @Id",
                            new { Id = artikelId }, tx);
                    }
                }

                tx.Commit();
                _log.Information("Bestellung {Id} aus Einkaufsliste erstellt", bestellungId);
                return bestellungId;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        #endregion
    }

    #region DTOs

    public class Lieferant
    {
        public int KLieferant { get; set; }
        public string? LiefNr { get; set; }
        public string? Firma { get; set; }
        public string? CFirma => Firma; // Alias für Dropdown-Binding
        public string? Kontakt { get; set; }
        public string? Strasse { get; set; }
        public string? PLZ { get; set; }
        public string? Ort { get; set; }
        public string? Land { get; set; }
        public string? Tel { get; set; }
        public string? EMail { get; set; }
        public string? Aktiv { get; set; }
        public decimal Mindestbestellwert { get; set; }
        public int Zahlungsziel { get; set; }
        public decimal Skonto { get; set; }
    }

    /// <summary>Vollständige Lieferanten-Stammdaten</summary>
    public class LieferantStammdaten
    {
        public int KLieferant { get; set; }
        public string? LiefNr { get; set; }
        public string? Firma { get; set; }
        public string? Ansprechpartner { get; set; }
        public string? Strasse { get; set; }
        public string? PLZ { get; set; }
        public string? Ort { get; set; }
        public string? Land { get; set; }
        public string? Tel { get; set; }
        public string? Fax { get; set; }
        public string? EMail { get; set; }
        public string? Homepage { get; set; }
        public string? UstId { get; set; }
        public string? GLN { get; set; }
        public int? KreditorNr { get; set; }
        public string? Bankname { get; set; }
        public string? IBAN { get; set; }
        public string? BIC { get; set; }
        public string? KontoInhaber { get; set; }
        public int Zahlungsziel { get; set; }
        public decimal Skonto { get; set; }
        public int SkontoTage { get; set; }
        public decimal Mindestbestellwert { get; set; }
        public int Lieferzeit { get; set; }
        public decimal Rabatt { get; set; }
        public string? Aktiv { get; set; }
        public string? Notiz { get; set; }

        // Formatierte Ausgaben
        public string ZahlungszielText => Zahlungsziel > 0 ? $"{Zahlungsziel} Tage" : "-";
        public string SkontoText => Skonto > 0 ? $"{Skonto:N2}% bei {SkontoTage} Tagen" : "-";
        public string MindestbestellwertText => Mindestbestellwert > 0 ? $"{Mindestbestellwert:C}" : "-";
        public string LieferzeitText => Lieferzeit > 0 ? $"{Lieferzeit} Tage" : "-";
    }

    /// <summary>NOVVIA-Erweiterung der Lieferanten-Stammdaten</summary>
    public class LieferantErweitert
    {
        public int KLieferantErweitert { get; set; }
        public int KLieferant { get; set; }

        // Produktkategorien
        public bool Ambient { get; set; }
        public bool Cool { get; set; }
        public bool Medcan { get; set; }
        public bool Tierarznei { get; set; }

        // Qualifizierung
        public DateTime? QualifiziertAm { get; set; }
        public string? QualifiziertVon { get; set; }
        public string? QualifikationsDocs { get; set; }

        public DateTime? Erstellt { get; set; }
        public DateTime? Geaendert { get; set; }

        // Aliase für Dapper (SQL-Spaltennamen)
        public bool NAmbient { get => Ambient; set => Ambient = value; }
        public bool NCool { get => Cool; set => Cool = value; }
        public bool NMedcan { get => Medcan; set => Medcan = value; }
        public bool NTierarznei { get => Tierarznei; set => Tierarznei = value; }
        public DateTime? DQualifiziertAm { get => QualifiziertAm; set => QualifiziertAm = value; }
        public string? CQualifiziertVon { get => QualifiziertVon; set => QualifiziertVon = value; }
        public string? CQualifikationsDocs { get => QualifikationsDocs; set => QualifikationsDocs = value; }
        public DateTime? DErstellt { get => Erstellt; set => Erstellt = value; }
        public DateTime? DGeaendert { get => Geaendert; set => Geaendert = value; }

        // Formatierte Anzeige
        public string QualifiziertAmText => QualifiziertAm?.ToString("dd.MM.yyyy") ?? "-";
    }

    public class LieferantUebersicht
    {
        public int KLieferant { get; set; }
        public string? CLiefNr { get; set; }
        public string? CFirma { get; set; }
        public string? CStrasse { get; set; }
        public string? CPLZ { get; set; }
        public string? COrt { get; set; }
        public string? CTel { get; set; }
        public string? CEmail { get; set; }
        public string? CAktiv { get; set; }
        public decimal FMindestbestellwert { get; set; }
        public int NZahlungsziel { get; set; }
        public decimal FSkonto { get; set; }
        public int? KMSV3Lieferant { get; set; }
        public string? CMSV3Url { get; set; }
        public int NMSV3Version { get; set; }
        public bool NHatMSV3 { get; set; }
        public int NOffeneBestellungen { get; set; }
        public int NOffeneRechnungen { get; set; }
    }

    public class LieferantenBestellung
    {
        public int KLieferantenBestellung { get; set; }
        public int KLieferant { get; set; }
        public string? LieferantName { get; set; }
        public string? CEigeneBestellnummer { get; set; }
        public DateTime DErstellt { get; set; }
        public DateTime? DLieferdatum { get; set; }
        public int NStatus { get; set; }
        public string? CInternerKommentar { get; set; }
        public int AnzahlPositionen { get; set; }
        public decimal Summe { get; set; }
        public List<LieferantenBestellungPos> Positionen { get; set; } = new();
    }

    public class LieferantenBestellungPos
    {
        public int KLieferantenBestellungPos { get; set; }
        public int KArtikel { get; set; }
        public string? CArtNr { get; set; }
        public string? ArtikelName { get; set; }
        public string? CLieferantenArtNr { get; set; }
        public string? CPZN { get; set; }
        public decimal FMenge { get; set; }
        public decimal FMengeGeliefert { get; set; }
        public decimal FEKNetto { get; set; }
        public string? CHinweis { get; set; }
        public int NSort { get; set; }

        // Auswahl-Checkbox für Bestellung
        public bool IstAusgewaehlt { get; set; }

        // Wunsch-MHD (editierbar)
        public DateTime? WunschMHD { get; set; }

        // Positionstext
        public string? CPositionsText { get; set; }

        // Lieferant-Auswahl (für MSV3-Bestellung)
        public int? SelectedKLieferant { get; set; }
        public int? SelectedKMSV3Lieferant { get; set; }
        public string? SelectedLieferantName { get; set; }
        public List<ArtikelMSV3Lieferant> VerfuegbareLieferanten { get; set; } = new();

        // Anzeige für ausgewählten Lieferanten
        public string LieferantDisplayText => SelectedLieferantName ??
            (VerfuegbareLieferanten.FirstOrDefault(l => l.KLieferant == SelectedKLieferant)?.DisplayText ?? "-");

        // MSV3 Verfügbarkeitsdaten (werden zur Laufzeit befüllt)
        public int? MSV3Bestand { get; set; }
        public bool? MSV3Verfuegbar { get; set; }
        public string? MSV3StatusText { get; set; }
        public string? MSV3Lieferzeit { get; set; }
        public string? MSV3Fehler { get; set; }
        public DateTime? MSV3MHD { get; set; }
        public string? MSV3ChargenNr { get; set; }

        // MHD-Anzeige formatiert
        public string MSV3MHDText => MSV3MHD?.ToString("dd.MM.yyyy") ?? "";

        // Alias für Charge
        public string? MSV3Charge => MSV3ChargenNr;

        // Farben für UI
        public string MSV3BestandFarbe => MSV3Bestand > 0 ? "Green" : (MSV3Bestand.HasValue ? "Red" : "Gray");
        public string MSV3StatusFarbe => MSV3StatusText switch
        {
            "VERFUEGBAR" => "Green",
            "TEILWEISE" => "Orange",
            "NICHT_VERFUEGBAR" => "Red",
            _ => "Gray"
        };

        // Mindest-MHD für Bestellung (vom Benutzer eingebbar)
        public DateTime? MinMHD { get; set; }
    }

    /// <summary>
    /// Flache Darstellung: Eine Zeile pro Artikel-Lieferant-Kombination
    /// </summary>
    public class BestellPositionLieferantZeile
    {
        // Artikel-Daten
        public int KLieferantenBestellungPos { get; set; }
        public int KArtikel { get; set; }
        public string? CArtNr { get; set; }
        public string? ArtikelName { get; set; }
        public string? CPZN { get; set; }
        public decimal FMenge { get; set; }
        public string? CHinweis { get; set; }

        // Lieferant-Daten
        public int KLieferant { get; set; }
        public int? KMSV3Lieferant { get; set; }
        public string? LieferantName { get; set; }
        public string? LieferantenArtNr { get; set; }
        public decimal EKNetto { get; set; }
        public bool HatMSV3 { get; set; }

        // Auswahl-Checkbox
        public bool IstAusgewaehlt { get; set; }

        // Wunsch-MHD (editierbar)
        public DateTime? WunschMHD { get; set; }

        // Positionstext (editierbar)
        public string? CPositionsText { get; set; }

        // MSV3 Verfügbarkeitsdaten
        public int? MSV3Bestand { get; set; }
        public bool? MSV3Verfuegbar { get; set; }
        public string? MSV3StatusText { get; set; }
        public DateTime? MSV3MHD { get; set; }
        public string? MSV3ChargenNr { get; set; }
        public DateTime? MSV3Lieferzeit { get; set; }

        // Formatierte Anzeigen
        public string EKNettoText => EKNetto.ToString("N2") + " €";
        public string MSV3MHDText => MSV3MHD?.ToString("dd.MM.yyyy") ?? "";
        public string WunschMHDText => WunschMHD?.ToString("dd.MM.yyyy") ?? "";
        public string MSV3BestandFarbe => MSV3Bestand > 0 ? "Green" : (MSV3Bestand.HasValue ? "Red" : "Gray");
        public string MSV3StatusFarbe => MSV3StatusText switch
        {
            "VERFUEGBAR" or "SOFORT_LIEFERBAR" => "Green",
            "TEILWEISE" or "TEILWEISE_LIEFERBAR" => "Orange",
            "NICHT_VERFUEGBAR" or "NICHT_LIEFERBAR" => "Red",
            _ => "Gray"
        };

        // Für Gruppierung nach Artikel
        public string ArtikelKey => $"{KArtikel}_{CPZN}";
    }

    public class Eingangsrechnung
    {
        public int KEingangsrechnung { get; set; }
        public int KLieferant { get; set; }
        public string? LieferantName { get; set; }
        public string? CFremdbelegnummer { get; set; }
        public string? CEigeneRechnungsnummer { get; set; }
        public DateTime DErstellt { get; set; }
        public DateTime? DBelegdatum { get; set; }
        public DateTime? DZahlungsziel { get; set; }
        public int NStatus { get; set; }
        public bool NGeprueft { get; set; }
        public bool NFreigegeben { get; set; }
        public DateTime? DSkontoFrist { get; set; }
    }

    public class EinkaufslisteEintrag
    {
        public long KArtikelEinkaufsliste { get; set; }
        public int KArtikel { get; set; }
        public string? CArtNr { get; set; }
        public string? ArtikelName { get; set; }
        public decimal FAnzahl { get; set; }
        public int KLieferant { get; set; }
        public string? LieferantName { get; set; }
        public decimal? FEKNettoLieferant { get; set; }
        public string? CLieferantenArtNr { get; set; }
        public string? CStatus { get; set; }
        public DateTime DErstellt { get; set; }
        public string? CPZN { get; set; }
        public string? CHersteller { get; set; }
        public decimal? FAEP { get; set; }
        public decimal? FAVP { get; set; }
        public bool NRezeptpflicht { get; set; }
        public bool NBTM { get; set; }
        public bool NKuehlpflichtig { get; set; }
        public bool NMSV3Verfuegbar { get; set; }
        public bool IsSelected { get; set; }
    }

    /// <summary>Typ-Alias für Bestellungsübersicht</summary>
    public class LieferantenBestellungUebersicht : LieferantenBestellung { }

    /// <summary>Typ-Alias für Eingangsrechnung-Übersicht</summary>
    public class EingangsrechnungUebersicht : Eingangsrechnung { }

    /// <summary>Typ-Alias für Einkaufsliste-Item (mit Selection)</summary>
    public class EinkaufslisteItem : EinkaufslisteEintrag { }

    #endregion
}

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Dapper;
using Serilog;

namespace NovviaERP.Core.Services
{
    /// <summary>
    /// Core Service für Kunden, Artikel und Bestellungen (Verkauf)
    /// Basierend auf JTL-Wawi 1.11 Tabellenstruktur
    /// </summary>
    public class CoreService : IDisposable
    {
        private readonly string _connectionString;
        private SqlConnection? _connection;
        private static readonly ILogger _log = Log.ForContext<CoreService>();

        public CoreService(string connectionString) => _connectionString = connectionString;

        private async Task<SqlConnection> GetConnectionAsync()
        {
            if (_connection == null || _connection.State != ConnectionState.Open)
            {
                _connection?.Dispose();
                _connection = new SqlConnection(_connectionString);
                await _connection.OpenAsync();
            }
            return _connection;
        }

        public void Dispose() { _connection?.Dispose(); _connection = null; }

        #region DTOs

        public class KundeUebersicht
        {
            public int KKunde { get; set; }
            public string? CKundenNr { get; set; }
            public string? CFirma { get; set; }
            public string? CVorname { get; set; }
            public string? CName { get; set; }  // Nachname
            public string? COrt { get; set; }
            public string? CMail { get; set; }
            public string? CTel { get; set; }
            public string? CSperre { get; set; }
            public int? KKundenGruppe { get; set; }
            public string? Kundengruppe { get; set; }
            public DateTime? DErstellt { get; set; }
            public decimal Umsatz { get; set; }

            // Berechnete Felder
            public string Anzeigename => !string.IsNullOrEmpty(CFirma) ? CFirma : $"{CVorname} {CName}".Trim();
            public bool IstGesperrt => CSperre == "Y";
        }

        public class KundeDetail
        {
            // Aus tKunde
            public int KKunde { get; set; }
            public string? CKundenNr { get; set; }
            public int? KKundenGruppe { get; set; }
            public int? KKundenKategorie { get; set; }
            public decimal FRabatt { get; set; }
            public string CNewsletter { get; set; } = "N";
            public string? CEbayName { get; set; }
            public string? CGeburtstag { get; set; }
            public string? CWWW { get; set; }
            public string CSperre { get; set; } = "N";
            public int? NZahlungsziel { get; set; }
            public int? KSprache { get; set; }
            public string? CHerkunft { get; set; }
            public string? CHRNr { get; set; }
            public int? KZahlungsart { get; set; }
            public int NDebitorennr { get; set; }
            public string? CSteuerNr { get; set; }
            public int NKreditlimit { get; set; }
            public byte NMahnstopp { get; set; }
            public int NMahnrhythmus { get; set; }
            public decimal FSkonto { get; set; }
            public int NSkontoInTagen { get; set; }
            public DateTime? DErstellt { get; set; }

            // Standard-Adresse (aus tAdresse)
            public AdresseDetail? StandardAdresse { get; set; }

            // Alle Adressen
            public List<AdresseDetail> Adressen { get; set; } = new();

            // Kundengruppe
            public string? Kundengruppe { get; set; }

            // Statistik
            public int AnzahlBestellungen { get; set; }
            public decimal GesamtUmsatz { get; set; }
        }

        public class AdresseDetail
        {
            public int KAdresse { get; set; }
            public int? KKunde { get; set; }
            public string? CFirma { get; set; }
            public string? CAnrede { get; set; }
            public string? CTitel { get; set; }
            public string? CVorname { get; set; }
            public string? CName { get; set; }
            public string? CStrasse { get; set; }
            public string? CPLZ { get; set; }
            public string? COrt { get; set; }
            public string? CLand { get; set; }
            public string? CISO { get; set; }
            public string? CBundesland { get; set; }
            public string? CTel { get; set; }
            public string? CMobil { get; set; }
            public string? CFax { get; set; }
            public string? CMail { get; set; }
            public string? CZusatz { get; set; }
            public string? CAdressZusatz { get; set; }
            public string? CUSTID { get; set; }
            public byte NStandard { get; set; }
            public byte NTyp { get; set; }  // 1=Rechnung, 2=Lieferung

            public bool IstStandard => NStandard == 1;
            public string AdressTypText => NTyp == 1 ? "Rechnungsadresse" : NTyp == 2 ? "Lieferadresse" : "Adresse";
        }

        public class ArtikelUebersicht
        {
            public int KArtikel { get; set; }
            public string? CArtNr { get; set; }
            public string? CBarcode { get; set; }
            public string? Name { get; set; }
            public decimal FVKNetto { get; set; }
            public decimal FVKBrutto { get; set; }
            public decimal FEKNetto { get; set; }
            public decimal NLagerbestand { get; set; }
            public decimal NMidestbestand { get; set; }
            public string CAktiv { get; set; } = "Y";
            public string CLagerArtikel { get; set; } = "Y";
            public int? KHersteller { get; set; }
            public string? Hersteller { get; set; }

            public bool Aktiv => CAktiv == "Y";
            public bool UnterMindestbestand => NLagerbestand < NMidestbestand;
        }

        public class ArtikelDetail
        {
            // Grunddaten
            public int KArtikel { get; set; }
            public string? CArtNr { get; set; }
            public string? CBarcode { get; set; }
            public string? CHAN { get; set; }  // Hersteller-Artikelnummer
            public string? CISBN { get; set; }
            public string? CUPC { get; set; }
            public string? CASIN { get; set; }
            public string? CEpid { get; set; }

            // Beschreibung (aus tArtikelBeschreibung)
            public string? Name { get; set; }
            public string? Beschreibung { get; set; }
            public string? KurzBeschreibung { get; set; }
            public string? CSeo { get; set; }

            // Preise
            public decimal FVKNetto { get; set; }
            public decimal FUVP { get; set; }
            public decimal FEKNetto { get; set; }
            public decimal FLetzterEK { get; set; }

            // Lager
            public decimal NLagerbestand { get; set; }
            public decimal NMidestbestand { get; set; }
            public string CLagerArtikel { get; set; } = "Y";
            public string CLagerAktiv { get; set; } = "Y";
            public string CLagerKleinerNull { get; set; } = "N";
            public decimal FPackeinheit { get; set; } = 1;

            // Maße & Gewicht
            public decimal? FGewicht { get; set; }
            public decimal? FArtGewicht { get; set; }
            public decimal? FBreite { get; set; }
            public decimal? FHoehe { get; set; }
            public decimal? FLaenge { get; set; }

            // Klassifikation
            public int? KSteuerklasse { get; set; }
            public int? KHersteller { get; set; }
            public int? KWarengruppe { get; set; }
            public int? KVersandklasse { get; set; }
            public int? KMassEinheit { get; set; }

            // Flags
            public string CAktiv { get; set; } = "Y";
            public string CTopArtikel { get; set; } = "N";
            public string CNeu { get; set; } = "N";
            public string CTeilbar { get; set; } = "N";
            public byte NMHD { get; set; }  // MHD-Verfolgung
            public byte NCharge { get; set; }  // Chargenverfolgung
            public byte NSeriennummernVerfolgung { get; set; }

            // Vater/Kind
            public byte NIstVater { get; set; }
            public int? KVaterArtikel { get; set; }

            // Stückliste
            public int? KStueckliste { get; set; }

            // Zoll
            public string? CTaric { get; set; }
            public string? CHerkunftsland { get; set; }
            public string? CUNNummer { get; set; }
            public string? CGefahrnr { get; set; }

            // Suchbegriffe
            public string? CSuchbegriffe { get; set; }

            // Timestamps
            public DateTime? DErstelldatum { get; set; }
            public DateTime? DMod { get; set; }

            // Referenzen (Namen)
            public string? Hersteller { get; set; }
            public string? Steuerklasse { get; set; }
            public string? Warengruppe { get; set; }

            // Bestand pro Lager
            public List<LagerbestandDetail> Lagerbestaende { get; set; } = new();

            // Kategorien
            public List<KategorieRef> Kategorien { get; set; } = new();

            // Lieferanten
            public List<ArtikelLieferantRef> Lieferanten { get; set; } = new();
        }

        public class LagerbestandDetail
        {
            public int KWarenLager { get; set; }
            public string? LagerName { get; set; }
            public decimal Bestand { get; set; }
            public decimal Reserviert { get; set; }
            public decimal Verfuegbar => Bestand - Reserviert;
        }

        public class KategorieRef
        {
            public int KKategorie { get; set; }
            public string? Name { get; set; }
        }

        public class ArtikelLieferantRef
        {
            public int KLieferant { get; set; }
            public string? LieferantName { get; set; }
            public string? CArtNr { get; set; }
            public decimal FEKNetto { get; set; }
            public int NPrioritaet { get; set; }
        }

        public class BestellungUebersicht
        {
            public int KBestellung { get; set; }
            public string? CBestellNr { get; set; }
            public string? CInetBestellNr { get; set; }
            public DateTime DErstellt { get; set; }
            public string? CStatus { get; set; }
            public int TKunde_KKunde { get; set; }
            public string? KundeName { get; set; }
            public string? KundeFirma { get; set; }
            public string? CKundenNr { get; set; }
            public decimal GesamtNetto { get; set; }
            public decimal GesamtBrutto { get; set; }
            public DateTime? DVersandt { get; set; }
            public DateTime? DBezahlt { get; set; }
            public string? CIdentCode { get; set; }
            public int? KShop { get; set; }
            public string? ShopName { get; set; }
            public byte NStorno { get; set; }

            public bool Storniert => NStorno == 1;
            public bool Versendet => DVersandt.HasValue;
            public bool Bezahlt => DBezahlt.HasValue;
        }

        public class BestellungDetail
        {
            public int KBestellung { get; set; }
            public string? CBestellNr { get; set; }
            public string? CInetBestellNr { get; set; }
            public DateTime DErstellt { get; set; }
            public string? CStatus { get; set; }
            public int TKunde_KKunde { get; set; }

            // Kunde
            public string? KundeName { get; set; }
            public string? KundeFirma { get; set; }
            public string? CKundenNr { get; set; }
            public string? KundeMail { get; set; }
            public string? KundeTel { get; set; }

            // Beträge
            public decimal GesamtNetto { get; set; }
            public decimal GesamtBrutto { get; set; }
            public decimal FVersandBruttoPreis { get; set; }
            public decimal FRabatt { get; set; }
            public decimal FGutschein { get; set; }
            public string CWaehrung { get; set; } = "EUR";

            // Versand
            public int? TVersandArt_KVersandArt { get; set; }
            public string? VersandartName { get; set; }
            public DateTime? DVersandt { get; set; }
            public string? CIdentCode { get; set; }
            public string? TrackingNr { get => CIdentCode; set => CIdentCode = value; }
            public string? CVersandInfo { get; set; }

            // Zahlung
            public int? KZahlungsart { get; set; }
            public string? ZahlungsartName { get; set; }
            public DateTime? DBezahlt { get; set; }
            public int NZahlungsZiel { get; set; }

            // Adressen
            public AdresseDetail? Rechnungsadresse { get; set; }
            public AdresseDetail? Lieferadresse { get; set; }

            // Shop
            public int? KShop { get; set; }
            public string? ShopName { get; set; }

            // Anmerkungen
            public string? CAnmerkung { get; set; }
            public string? CVerwendungszweck { get; set; }

            // Status
            public byte NStorno { get; set; }
            public byte NKomplettAusgeliefert { get; set; }

            // Positionen
            public List<BestellPositionDetail> Positionen { get; set; } = new();
        }

        public class BestellPositionDetail
        {
            public int KBestellPos { get; set; }
            public int? TArtikel_KArtikel { get; set; }
            public string? CArtNr { get; set; }
            public string? CName { get; set; }
            public decimal FAnzahl { get; set; }
            public decimal FVKNetto { get; set; }
            public decimal? FVKBrutto { get; set; }
            public decimal? FRabatt { get; set; }
            public decimal FMwSt { get; set; }
            public string? CEinheit { get; set; }
            public int? NPosTyp { get; set; }

            public decimal Summe => FAnzahl * (FVKBrutto ?? FVKNetto * (1 + FMwSt / 100));
        }

        public class KundengruppeRef
        {
            public int KKundenGruppe { get; set; }
            public string? CName { get; set; }
            public decimal FRabatt { get; set; }
        }

        public class ZahlungsartRef
        {
            public int KZahlungsart { get; set; }
            public string? CName { get; set; }
        }

        public class VersandartRef
        {
            public int KVersandArt { get; set; }
            public string? CName { get; set; }
        }

        public class HerstellerRef
        {
            public int KHersteller { get; set; }
            public string? CName { get; set; }
        }

        public class SteuerklasseRef
        {
            public int KSteuerklasse { get; set; }
            public string? CName { get; set; }
        }

        public class WarengruppeRef
        {
            public int KWarengruppe { get; set; }
            public string? CName { get; set; }
        }

        #endregion

        #region Kunden

        public async Task<IEnumerable<KundeUebersicht>> GetKundenAsync(string? suche = null, int? kundengruppeId = null, bool nurAktive = false, int limit = 200)
        {
            var conn = await GetConnectionAsync();
            var sql = @"
                SELECT TOP (@Limit)
                    k.kKunde, k.cKundenNr, k.cSperre, k.kKundenGruppe, k.dErstellt,
                    a.cFirma, a.cVorname, a.cName, a.cOrt, a.cMail, a.cTel,
                    kg.cName AS Kundengruppe,
                    ISNULL((SELECT SUM(bp.nAnzahl * bp.fVkNetto)
                            FROM tBestellung b
                            INNER JOIN tbestellpos bp ON b.kBestellung = bp.tBestellung_kBestellung
                            WHERE b.tKunde_kKunde = k.kKunde AND b.nStorno = 0), 0) AS Umsatz
                FROM tkunde k
                LEFT JOIN tAdresse a ON a.kKunde = k.kKunde AND a.nStandard = 1
                LEFT JOIN tKundenGruppe kg ON kg.kKundenGruppe = k.kKundenGruppe
                WHERE 1=1";

            if (nurAktive) sql += " AND k.cSperre != 'Y'";
            if (kundengruppeId.HasValue) sql += " AND k.kKundenGruppe = @KundengruppeId";
            if (!string.IsNullOrEmpty(suche))
            {
                sql += @" AND (k.cKundenNr LIKE @Suche
                         OR a.cFirma LIKE @Suche
                         OR a.cName LIKE @Suche
                         OR a.cVorname LIKE @Suche
                         OR a.cMail LIKE @Suche
                         OR a.cOrt LIKE @Suche)";
            }
            sql += " ORDER BY ISNULL(a.cFirma, a.cName), a.cVorname";

            return await conn.QueryAsync<KundeUebersicht>(sql, new { Limit = limit, Suche = $"%{suche}%", KundengruppeId = kundengruppeId });
        }

        public async Task<KundeDetail?> GetKundeByIdAsync(int kundeId)
        {
            var conn = await GetConnectionAsync();

            // Kundendaten
            var kunde = await conn.QuerySingleOrDefaultAsync<KundeDetail>(@"
                SELECT k.*, kg.cName AS Kundengruppe,
                       (SELECT COUNT(*) FROM tBestellung WHERE tKunde_kKunde = k.kKunde) AS AnzahlBestellungen,
                       ISNULL((SELECT SUM(bp.nAnzahl * bp.fVkNetto)
                               FROM tBestellung b
                               INNER JOIN tbestellpos bp ON b.kBestellung = bp.tBestellung_kBestellung
                               WHERE b.tKunde_kKunde = k.kKunde AND b.nStorno = 0), 0) AS GesamtUmsatz
                FROM tkunde k
                LEFT JOIN tKundenGruppe kg ON kg.kKundenGruppe = k.kKundenGruppe
                WHERE k.kKunde = @Id", new { Id = kundeId });

            if (kunde == null) return null;

            // Adressen
            kunde.Adressen = (await conn.QueryAsync<AdresseDetail>(@"
                SELECT * FROM tAdresse WHERE kKunde = @Id ORDER BY nStandard DESC, nTyp", new { Id = kundeId })).ToList();

            kunde.StandardAdresse = kunde.Adressen.FirstOrDefault(a => a.NStandard == 1) ?? kunde.Adressen.FirstOrDefault();

            return kunde;
        }

        public async Task<int> CreateKundeAsync(KundeDetail kunde, AdresseDetail adresse)
        {
            var conn = await GetConnectionAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                // Nächste Kundennummer
                if (string.IsNullOrEmpty(kunde.CKundenNr))
                {
                    var maxNr = await conn.QuerySingleOrDefaultAsync<string>(
                        "SELECT MAX(cKundenNr) FROM tkunde WHERE cKundenNr LIKE '[0-9]%'", transaction: tx);
                    int next = 1;
                    if (!string.IsNullOrEmpty(maxNr) && int.TryParse(maxNr, out var num)) next = num + 1;
                    kunde.CKundenNr = next.ToString();
                }

                var kundeId = await conn.QuerySingleAsync<int>(@"
                    INSERT INTO tkunde (cKundenNr, kKundenGruppe, fRabatt, cNewsletter, cSperre,
                                       nZahlungsziel, kZahlungsart, nDebitorennr, nKreditlimit,
                                       nMahnstopp, nMahnrhythmus, fSkonto, nSkontoInTagen, dErstellt)
                    VALUES (@CKundenNr, @KKundenGruppe, @FRabatt, @CNewsletter, @CSperre,
                            @NZahlungsziel, @KZahlungsart, @NDebitorennr, @NKreditlimit,
                            @NMahnstopp, @NMahnrhythmus, @FSkonto, @NSkontoInTagen, GETDATE());
                    SELECT SCOPE_IDENTITY();", kunde, tx);

                // Adresse anlegen
                adresse.KKunde = kundeId;
                adresse.NStandard = 1;
                await conn.ExecuteAsync(@"
                    INSERT INTO tAdresse (kKunde, cFirma, cAnrede, cTitel, cVorname, cName, cStrasse,
                                         cPLZ, cOrt, cLand, cISO, cBundesland, cTel, cMobil, cFax, cMail,
                                         cZusatz, cAdressZusatz, cUSTID, nStandard, nTyp)
                    VALUES (@KKunde, @CFirma, @CAnrede, @CTitel, @CVorname, @CName, @CStrasse,
                            @CPLZ, @COrt, @CLand, @CISO, @CBundesland, @CTel, @CMobil, @CFax, @CMail,
                            @CZusatz, @CAdressZusatz, @CUSTID, @NStandard, @NTyp)", adresse, tx);

                tx.Commit();
                _log.Information("Kunde {KundenNr} angelegt (ID: {Id})", kunde.CKundenNr, kundeId);
                return kundeId;
            }
            catch { tx.Rollback(); throw; }
        }

        public async Task UpdateKundeAsync(KundeDetail kunde)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync(@"
                UPDATE tkunde SET
                    kKundenGruppe = @KKundenGruppe, fRabatt = @FRabatt, cNewsletter = @CNewsletter,
                    cSperre = @CSperre, nZahlungsziel = @NZahlungsziel, kZahlungsart = @KZahlungsart,
                    nDebitorennr = @NDebitorennr, nKreditlimit = @NKreditlimit, nMahnstopp = @NMahnstopp,
                    nMahnrhythmus = @NMahnrhythmus, fSkonto = @FSkonto, nSkontoInTagen = @NSkontoInTagen
                WHERE kKunde = @KKunde", kunde);
        }

        public async Task UpdateAdresseAsync(AdresseDetail adresse)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync(@"
                UPDATE tAdresse SET
                    cFirma = @CFirma, cAnrede = @CAnrede, cTitel = @CTitel, cVorname = @CVorname,
                    cName = @CName, cStrasse = @CStrasse, cPLZ = @CPLZ, cOrt = @COrt,
                    cLand = @CLand, cISO = @CISO, cBundesland = @CBundesland, cTel = @CTel,
                    cMobil = @CMobil, cFax = @CFax, cMail = @CMail, cZusatz = @CZusatz,
                    cAdressZusatz = @CAdressZusatz, cUSTID = @CUSTID
                WHERE kAdresse = @KAdresse", adresse);
        }

        public async Task<int> CreateAdresseAsync(AdresseDetail adresse)
        {
            var conn = await GetConnectionAsync();
            return await conn.QuerySingleAsync<int>(@"
                INSERT INTO tAdresse (kKunde, cFirma, cAnrede, cTitel, cVorname, cName, cStrasse,
                                     cPLZ, cOrt, cLand, cISO, cBundesland, cTel, cMobil, cFax, cMail,
                                     cZusatz, cAdressZusatz, cUSTID, nStandard, nTyp)
                VALUES (@KKunde, @CFirma, @CAnrede, @CTitel, @CVorname, @CName, @CStrasse,
                        @CPLZ, @COrt, @CLand, @CISO, @CBundesland, @CTel, @CMobil, @CFax, @CMail,
                        @CZusatz, @CAdressZusatz, @CUSTID, @NStandard, @NTyp);
                SELECT SCOPE_IDENTITY();", adresse);
        }

        public async Task<IEnumerable<KundengruppeRef>> GetKundengruppenAsync()
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<KundengruppeRef>("SELECT kKundenGruppe, cName, fRabatt FROM tKundenGruppe ORDER BY cName");
        }

        #endregion

        #region Artikel

        public async Task<IEnumerable<ArtikelUebersicht>> GetArtikelAsync(string? suche = null, int? herstellerId = null, int? warengruppeId = null, bool nurAktive = true, bool nurUnterMindestbestand = false, int limit = 200)
        {
            var conn = await GetConnectionAsync();
            var sql = @"
                SELECT TOP (@Limit)
                    a.kArtikel, a.cArtNr, a.cBarcode, a.fVKNetto, a.fEKNetto,
                    a.nLagerbestand, a.nMidestbestand, a.cAktiv, a.cLagerArtikel, a.kHersteller,
                    ab.cName AS Name,
                    h.cName AS Hersteller,
                    ISNULL(a.fVKNetto * (1 + ISNULL((SELECT TOP 1 fSteuersatz FROM tSteuersatz WHERE kSteuerklasse = a.kSteuerklasse ORDER BY nPrio DESC), 19) / 100), a.fVKNetto) AS FVKBrutto
                FROM tArtikel a
                LEFT JOIN tArtikelBeschreibung ab ON a.kArtikel = ab.kArtikel AND ab.kSprache = 1
                LEFT JOIN tHersteller h ON a.kHersteller = h.kHersteller
                WHERE a.nDelete = 0";

            if (nurAktive) sql += " AND a.cAktiv = 'Y'";
            if (herstellerId.HasValue) sql += " AND a.kHersteller = @HerstellerId";
            if (warengruppeId.HasValue) sql += " AND a.kWarengruppe = @WarengruppeId";
            if (nurUnterMindestbestand) sql += " AND a.nLagerbestand < a.nMidestbestand";
            if (!string.IsNullOrEmpty(suche))
            {
                sql += @" AND (a.cArtNr LIKE @Suche
                         OR a.cBarcode LIKE @Suche
                         OR ab.cName LIKE @Suche
                         OR a.cHAN LIKE @Suche)";
            }
            sql += " ORDER BY ab.cName, a.cArtNr";

            return await conn.QueryAsync<ArtikelUebersicht>(sql, new { Limit = limit, Suche = $"%{suche}%", HerstellerId = herstellerId, WarengruppeId = warengruppeId });
        }

        public async Task<ArtikelDetail?> GetArtikelByIdAsync(int artikelId)
        {
            var conn = await GetConnectionAsync();

            var artikel = await conn.QuerySingleOrDefaultAsync<ArtikelDetail>(@"
                SELECT a.kArtikel, a.cArtNr, a.cBarcode, a.cHAN, a.cISBN, a.cUPC, a.cASIN,
                       a.fVKNetto, a.fUVP, a.fEKNetto, a.nLagerbestand, a.nMidestbestand,
                       a.cLagerArtikel, a.cAktiv, a.cTopArtikel, a.cNeu, a.cTeilbar,
                       a.fGewicht, a.fArtGewicht, a.fBreite, a.fHoehe, a.fLaenge, a.fPackeinheit,
                       a.kSteuerklasse, a.kHersteller, a.kWarengruppe, a.kVersandklasse,
                       a.nMHD, a.nCharge, a.cTaric, a.cHerkunftsland, a.cSuchbegriffe,
                       ab.cName AS Name, ab.cBeschreibung AS Beschreibung, ab.cKurzBeschreibung AS KurzBeschreibung, ab.cUrlPfad AS CSeo,
                       h.cName AS Hersteller,
                       sk.cName AS Steuerklasse,
                       wg.cName AS Warengruppe
                FROM tArtikel a
                LEFT JOIN tArtikelBeschreibung ab ON a.kArtikel = ab.kArtikel AND ab.kSprache = 1
                LEFT JOIN tHersteller h ON a.kHersteller = h.kHersteller
                LEFT JOIN tSteuerklasse sk ON a.kSteuerklasse = sk.kSteuerklasse
                LEFT JOIN tWarengruppe wg ON a.kWarengruppe = wg.kWarengruppe
                WHERE a.kArtikel = @Id", new { Id = artikelId });

            if (artikel == null) return null;

            // Sub-Abfragen deaktiviert - Performance-Problem
            artikel.Lagerbestaende = new List<LagerbestandDetail>();
            artikel.Kategorien = new List<KategorieRef>();
            artikel.Lieferanten = new List<ArtikelLieferantRef>();

            return artikel;
        }

        public async Task<ArtikelDetail?> GetArtikelByBarcodeAsync(string barcode)
        {
            var conn = await GetConnectionAsync();
            var id = await conn.QuerySingleOrDefaultAsync<int?>(
                "SELECT kArtikel FROM tArtikel WHERE cBarcode = @B OR cArtNr = @B", new { B = barcode });
            return id.HasValue ? await GetArtikelByIdAsync(id.Value) : null;
        }

        public async Task UpdateArtikelAsync(ArtikelDetail artikel)
        {
            var conn = await GetConnectionAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                await conn.ExecuteAsync(@"
                    UPDATE tArtikel SET
                        cArtNr = @CArtNr, cBarcode = @CBarcode, cHAN = @CHAN, cISBN = @CISBN,
                        fVKNetto = @FVKNetto, fUVP = @FUVP, fEKNetto = @FEKNetto,
                        nMidestbestand = @NMidestbestand, cLagerArtikel = @CLagerArtikel,
                        fGewicht = @FGewicht, fBreite = @FBreite, fHoehe = @FHoehe, fLaenge = @FLaenge,
                        kSteuerklasse = @KSteuerklasse, kHersteller = @KHersteller, kWarengruppe = @KWarengruppe,
                        cAktiv = @CAktiv, cTopArtikel = @CTopArtikel, nMHD = @NMHD, nCharge = @NCharge,
                        cTaric = @CTaric, cHerkunftsland = @CHerkunftsland, cSuchbegriffe = @CSuchbegriffe,
                        dMod = GETDATE()
                    WHERE kArtikel = @KArtikel", artikel, tx);

                // Beschreibung
                var hasDesc = await conn.QuerySingleAsync<int>(
                    "SELECT COUNT(*) FROM tArtikelBeschreibung WHERE kArtikel = @Id AND kSprache = 1",
                    new { Id = artikel.KArtikel }, tx);

                if (hasDesc > 0)
                {
                    await conn.ExecuteAsync(@"
                        UPDATE tArtikelBeschreibung SET cName = @Name, cBeschreibung = @Beschreibung,
                               cKurzBeschreibung = @KurzBeschreibung, cUrlPfad = @CSeo
                        WHERE kArtikel = @KArtikel AND kSprache = 1", artikel, tx);
                }
                else
                {
                    await conn.ExecuteAsync(@"
                        INSERT INTO tArtikelBeschreibung (kArtikel, kSprache, kPlattform, cName, cBeschreibung, cKurzBeschreibung, cUrlPfad)
                        VALUES (@KArtikel, 1, 1, @Name, @Beschreibung, @KurzBeschreibung, @CSeo)", artikel, tx);
                }

                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }

        public async Task<IEnumerable<HerstellerRef>> GetHerstellerAsync()
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<HerstellerRef>("SELECT kHersteller, cName FROM tHersteller ORDER BY cName");
        }

        public async Task<IEnumerable<SteuerklasseRef>> GetSteuerklassenAsync()
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<SteuerklasseRef>("SELECT kSteuerklasse, cName FROM tSteuerklasse ORDER BY cName");
        }

        public async Task<IEnumerable<WarengruppeRef>> GetWarengruppenAsync()
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<WarengruppeRef>("SELECT kWarengruppe, cName FROM tWarengruppe ORDER BY cName");
        }

        public async Task<int> CreateArtikelAsync(ArtikelDetail artikel)
        {
            var conn = await GetConnectionAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                // Artikelnummer generieren wenn leer
                if (string.IsNullOrEmpty(artikel.CArtNr))
                {
                    var maxNr = await conn.QuerySingleOrDefaultAsync<string>(
                        "SELECT TOP 1 cArtNr FROM tArtikel WHERE cArtNr LIKE 'ART%' ORDER BY cArtNr DESC",
                        transaction: tx);
                    if (maxNr != null && int.TryParse(maxNr.Replace("ART", ""), out var nr))
                        artikel.CArtNr = $"ART{(nr + 1):D6}";
                    else
                        artikel.CArtNr = "ART000001";
                }

                // Artikel anlegen
                var artikelId = await conn.QuerySingleAsync<int>(@"
                    INSERT INTO tArtikel (cArtNr, cBarcode, cHAN, cISBN, cUPC, cASIN,
                        fVKNetto, fUVP, fEKNetto, nMidestbestand, cLagerArtikel, cLagerAktiv, cLagerKleinerNull,
                        fGewicht, fArtGewicht, fBreite, fHoehe, fLaenge, fPackeinheit,
                        kSteuerklasse, kHersteller, kWarengruppe, kVersandklasse,
                        cAktiv, cTopArtikel, cNeu, cTeilbar, nMHD, nCharge, nSeriennr,
                        cTaric, cHerkunftsland, cSuchbegriffe, dErstellt, dMod)
                    VALUES (@CArtNr, @CBarcode, @CHAN, @CISBN, @CUPC, @CASIN,
                        @FVKNetto, @FUVP, @FEKNetto, @NMidestbestand, @CLagerArtikel, @CLagerAktiv, @CLagerKleinerNull,
                        @FGewicht, @FArtGewicht, @FBreite, @FHoehe, @FLaenge, @FPackeinheit,
                        @KSteuerklasse, @KHersteller, @KWarengruppe, @KVersandklasse,
                        @CAktiv, @CTopArtikel, @CNeu, @CTeilbar, @NMHD, @NCharge, @NSeriennummernVerfolgung,
                        @CTaric, @CHerkunftsland, @CSuchbegriffe, GETDATE(), GETDATE());
                    SELECT SCOPE_IDENTITY();", artikel, tx);

                artikel.KArtikel = artikelId;

                // Beschreibung anlegen
                await conn.ExecuteAsync(@"
                    INSERT INTO tArtikelBeschreibung (kArtikel, kSprache, kPlattform, cName, cBeschreibung, cKurzBeschreibung, cUrlPfad)
                    VALUES (@KArtikel, 1, 1, @Name, @Beschreibung, @KurzBeschreibung, @CSeo)", artikel, tx);

                tx.Commit();
                return artikelId;
            }
            catch { tx.Rollback(); throw; }
        }

        #endregion

        #region Bestellungen (Verkauf)

        public async Task<IEnumerable<BestellungUebersicht>> GetBestellungenAsync(
            string? suche = null, string? status = null, DateTime? von = null, DateTime? bis = null,
            int? kundeId = null, int? shopId = null, bool nurOffene = false, int limit = 200)
        {
            var conn = await GetConnectionAsync();
            var sql = @"
                SELECT TOP (@Limit)
                    b.kBestellung, b.cBestellNr, b.cInetBestellNr, b.dErstellt, b.cStatus,
                    b.tKunde_kKunde, b.dVersandt, b.dBezahlt, b.cIdentCode, b.nStorno, b.kShop,
                    k.cKundenNr,
                    a.cName AS KundeName, a.cFirma AS KundeFirma,
                    s.cName AS ShopName,
                    ISNULL((SELECT SUM(bp.nAnzahl * bp.fVkNetto) FROM tbestellpos bp WHERE bp.tBestellung_kBestellung = b.kBestellung), 0) AS GesamtNetto,
                    ISNULL((SELECT SUM(bp.nAnzahl * bp.fVkNetto * (1 + bp.fMwSt/100)) FROM tbestellpos bp WHERE bp.tBestellung_kBestellung = b.kBestellung), 0) AS GesamtBrutto
                FROM tBestellung b
                LEFT JOIN tkunde k ON b.tKunde_kKunde = k.kKunde
                LEFT JOIN tAdresse a ON a.kKunde = k.kKunde AND a.nStandard = 1
                LEFT JOIN tShop s ON b.kShop = s.kShop
                WHERE b.nStorno = 0";

            if (!string.IsNullOrEmpty(status)) sql += " AND b.cStatus = @Status";
            if (von.HasValue) sql += " AND b.dErstellt >= @Von";
            if (bis.HasValue) sql += " AND b.dErstellt <= @Bis";
            if (kundeId.HasValue) sql += " AND b.tKunde_kKunde = @KundeId";
            if (shopId.HasValue) sql += " AND b.kShop = @ShopId";
            if (nurOffene) sql += " AND b.dVersandt IS NULL";
            if (!string.IsNullOrEmpty(suche))
            {
                sql += @" AND (b.cBestellNr LIKE @Suche
                         OR b.cInetBestellNr LIKE @Suche
                         OR k.cKundenNr LIKE @Suche
                         OR a.cFirma LIKE @Suche
                         OR a.cName LIKE @Suche)";
            }
            sql += " ORDER BY b.dErstellt DESC";

            return await conn.QueryAsync<BestellungUebersicht>(sql, new {
                Limit = limit, Suche = $"%{suche}%", Status = status,
                Von = von, Bis = bis, KundeId = kundeId, ShopId = shopId
            });
        }

        public async Task<BestellungDetail?> GetBestellungByIdAsync(int bestellungId)
        {
            var conn = await GetConnectionAsync();

            var bestellung = await conn.QuerySingleOrDefaultAsync<BestellungDetail>(@"
                SELECT b.*,
                       k.cKundenNr,
                       a.cName AS KundeName, a.cFirma AS KundeFirma, a.cMail AS KundeMail, a.cTel AS KundeTel,
                       v.cName AS VersandartName,
                       z.cName AS ZahlungsartName,
                       s.cName AS ShopName,
                       ISNULL((SELECT SUM(bp.nAnzahl * bp.fVkNetto) FROM tbestellpos bp WHERE bp.tBestellung_kBestellung = b.kBestellung), 0) AS GesamtNetto,
                       ISNULL((SELECT SUM(bp.nAnzahl * bp.fVkNetto * (1 + bp.fMwSt/100)) FROM tbestellpos bp WHERE bp.tBestellung_kBestellung = b.kBestellung), 0) AS GesamtBrutto
                FROM tBestellung b
                LEFT JOIN tkunde k ON b.tKunde_kKunde = k.kKunde
                LEFT JOIN tAdresse a ON a.kKunde = k.kKunde AND a.nStandard = 1
                LEFT JOIN tVersandArt v ON b.tVersandArt_kVersandArt = v.kVersandArt
                LEFT JOIN tZahlungsArt z ON b.kZahlungsart = z.kZahlungsart
                LEFT JOIN tShop s ON b.kShop = s.kShop
                WHERE b.kBestellung = @Id", new { Id = bestellungId });

            if (bestellung == null) return null;

            // Positionen
            bestellung.Positionen = (await conn.QueryAsync<BestellPositionDetail>(@"
                SELECT bp.*, ab.cName AS CName
                FROM tbestellpos bp
                LEFT JOIN tArtikel a ON bp.tArtikel_kArtikel = a.kArtikel
                LEFT JOIN tArtikelBeschreibung ab ON a.kArtikel = ab.kArtikel AND ab.kSprache = 1
                WHERE bp.tBestellung_kBestellung = @Id
                ORDER BY bp.kBestellPos", new { Id = bestellungId })).ToList();

            // Falls Position keinen Namen hat, Artikelname verwenden
            foreach (var pos in bestellung.Positionen.Where(p => string.IsNullOrEmpty(p.CName)))
            {
                pos.CName = pos.CArtNr;
            }

            // Rechnungsadresse
            bestellung.Rechnungsadresse = await conn.QuerySingleOrDefaultAsync<AdresseDetail>(
                "SELECT * FROM tAdresse WHERE kAdresse = @Id", new { Id = bestellung.KZahlungsart }); // TODO: Richtigen FK verwenden

            // Lieferadresse
            // In JTL sind Adressen an der Bestellung gespeichert (kLieferadresse, kRechnungsadresse)

            return bestellung;
        }

        public async Task UpdateBestellStatusAsync(int bestellungId, string status)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync("UPDATE tBestellung SET cStatus = @Status WHERE kBestellung = @Id",
                new { Status = status, Id = bestellungId });
        }

        public async Task UpdateBestellungAsync(BestellungDetail bestellung)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync(@"
                UPDATE tBestellung SET
                    cStatus = @CStatus,
                    dBezahlt = @DBezahlt,
                    dVersandt = @DVersandt,
                    cIdentCode = @CIdentCode,
                    cAnmerkung = @CAnmerkung
                WHERE kBestellung = @KBestellung",
                new {
                    bestellung.KBestellung,
                    bestellung.CStatus,
                    bestellung.DBezahlt,
                    bestellung.DVersandt,
                    bestellung.CIdentCode,
                    bestellung.CAnmerkung
                });
        }

        public async Task<IEnumerable<ZahlungsartRef>> GetZahlungsartenAsync()
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<ZahlungsartRef>("SELECT kZahlungsart, cName FROM tZahlungsArt ORDER BY cName");
        }

        public async Task<IEnumerable<VersandartRef>> GetVersandartenAsync()
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<VersandartRef>("SELECT kVersandArt, cName FROM tVersandArt ORDER BY cName");
        }

        #endregion

        #region Eigene Felder (JTL native Tabellen)

        /// <summary>
        /// Lädt eigene Felder für einen Artikel
        /// JTL native: tAttribut, tAttributSprache, tArtikelAttribut, tArtikelAttributSprache
        /// </summary>
        public async Task<Dictionary<string, string?>> GetArtikelEigeneFelderAsync(int kArtikel)
        {
            var conn = await GetConnectionAsync();
            var result = new Dictionary<string, string?>();

            try
            {
                // JTL native Tabellen - wie in JTL-Wawi verwendet
                // Spaltennamen: nWertInt (Integer/Boolean), cWertVarchar (Text), fWertDecimal (Zahl)
                // kSprache IN (0, 1) für beide Sprachen
                var felder = await conn.QueryAsync<(string Name, string? Wert)>(
                    @"SELECT AttributS.cName AS Name,
                             COALESCE(
                                 CAST(ArtikelAttributS.nWertInt AS NVARCHAR(50)),
                                 ArtikelAttributS.cWertVarchar,
                                 CAST(ArtikelAttributS.fWertDecimal AS NVARCHAR(50))
                             ) AS Wert
                      FROM dbo.tAttribut AS Attribut
                      INNER JOIN dbo.tAttributSprache AS AttributS ON Attribut.kAttribut = AttributS.kAttribut
                      INNER JOIN dbo.tArtikelAttribut AS ArtikelAttribut ON Attribut.kAttribut = ArtikelAttribut.kAttribut
                      INNER JOIN dbo.tArtikelAttributSprache AS ArtikelAttributS ON ArtikelAttribut.kArtikelAttribut = ArtikelAttributS.kArtikelAttribut
                      WHERE ArtikelAttribut.kArtikel = @kArtikel
                        AND AttributS.kSprache IN (0, 1)
                        AND ArtikelAttributS.kSprache IN (0, 1)",
                    new { kArtikel });

                foreach (var f in felder)
                {
                    if (!string.IsNullOrEmpty(f.Name) && !result.ContainsKey(f.Name))
                    {
                        result[f.Name] = f.Wert;
                    }
                }

                _log.Debug("GetArtikelEigeneFelderAsync: kArtikel={kArtikel}, gefunden={Anzahl} Felder", kArtikel, result.Count);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Fehler beim Laden der Artikel-Eigenfelder für {kArtikel}", kArtikel);
            }

            return result;
        }

        /// <summary>
        /// Speichert ein eigenes Feld für einen Artikel
        /// Verwendet NOVVIA.spArtikelEigenesFeldCreateOrUpdate (JTL native Tabellen)
        /// </summary>
        public async Task SetArtikelEigenesFeldAsync(int kArtikel, string feldName, string? wert)
        {
            var conn = await GetConnectionAsync();

            _log.Debug("SetArtikelEigenesFeldAsync: kArtikel={kArtikel}, Feld={Feld}, Wert={Wert}", kArtikel, feldName, wert);

            try
            {
                // NOVVIA TVP verwenden - schreibt in JTL native Tabellen
                var dt = new System.Data.DataTable();
                dt.Columns.Add("kArtikel", typeof(int));
                dt.Columns.Add("cKey", typeof(string));
                dt.Columns.Add("cValue", typeof(string));
                dt.Rows.Add(kArtikel, feldName, wert ?? "");

                var p = new DynamicParameters();
                p.Add("@ArtikelEigenesFeldAnpassen", dt.AsTableValuedParameter("NOVVIA.TYPE_ArtikelEigenesFeldAnpassen"));
                p.Add("@nAutoCreateAttribute", 1);

                await conn.ExecuteAsync("NOVVIA.spArtikelEigenesFeldCreateOrUpdate", p, commandType: CommandType.StoredProcedure);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Fehler beim Speichern des Artikel-Eigenfelds {feldName} für {kArtikel}", feldName, kArtikel);
                throw;
            }
        }

        /// <summary>
        /// Lädt eigene Felder für einen Kunden (JTL Kunde.tKundeEigenesFeld)
        /// </summary>
        public async Task<Dictionary<string, string?>> GetKundeEigeneFelderAsync(int kKunde)
        {
            var conn = await GetConnectionAsync();
            var result = new Dictionary<string, string?>();

            try
            {
                // JTL speichert eigene Felder in Kunde.tKundeEigenesFeld mit Attribut-Verknüpfung
                var felder = await conn.QueryAsync<(string Name, string? Wert)>(
                    @"SELECT s.cName AS Name, k.cWertVarchar AS Wert
                      FROM Kunde.tKundeEigenesFeld k
                      INNER JOIN dbo.tAttribut a ON k.kAttribut = a.kAttribut
                      INNER JOIN dbo.tAttributSprache s ON a.kAttribut = s.kAttribut AND s.kSprache = 0
                      WHERE k.kKunde = @kKunde AND a.nIstFreifeld = 1",
                    new { kKunde });

                foreach (var f in felder)
                {
                    result[f.Name] = f.Wert;
                }
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Fehler beim Laden der Kunden-Eigenfelder für {kKunde}", kKunde);
            }

            return result;
        }

        /// <summary>
        /// Speichert ein eigenes Feld für einen Kunden via JTL TVP
        /// TVP: Kunde.TYPE_KundenEigenesFeldAnpassen (kKunde, cKey, cValue)
        /// SP: Kunde.spKundenEigenesFeldCreateOrUpdate @Data, @nFeldTyp
        /// </summary>
        public async Task SetKundeEigenesFeldAsync(int kKunde, string feldName, string? wert)
        {
            var conn = await GetConnectionAsync();

            // JTL TVP verwenden - Parameter: @KundenEigenesFeldAnpassen, @nFeldTyp
            var dt = new System.Data.DataTable();
            dt.Columns.Add("kKunde", typeof(int));
            dt.Columns.Add("cKey", typeof(string));
            dt.Columns.Add("cValue", typeof(string));
            dt.Rows.Add(kKunde, feldName, wert ?? "");

            _log.Debug("SetKundeEigenesFeldAsync: kKunde={kKunde}, Feld={Feld}, Wert={Wert}", kKunde, feldName, wert);

            var p = new DynamicParameters();
            p.Add("@KundenEigenesFeldAnpassen", dt.AsTableValuedParameter("Kunde.TYPE_KundenEigenesFeldAnpassen"));
            p.Add("@nFeldTyp", 12); // 12 = Default laut SP

            await conn.ExecuteAsync("Kunde.spKundenEigenesFeldCreateOrUpdate", p, commandType: CommandType.StoredProcedure);
        }

        /// <summary>
        /// Speichert mehrere eigene Felder für einen Artikel (JTL native Tabellen)
        /// </summary>
        public async Task SetArtikelEigeneFelderAsync(int kArtikel, Dictionary<string, string?> felder)
        {
            if (felder == null || felder.Count == 0) return;

            _log.Debug("SetArtikelEigeneFelderAsync: kArtikel={kArtikel}, {Anzahl} Felder", kArtikel, felder.Count);

            // Einzeln speichern - JTL native Tabellen verwenden
            foreach (var kvp in felder)
            {
                await SetArtikelEigenesFeldAsync(kArtikel, kvp.Key, kvp.Value);
            }
        }

        /// <summary>
        /// Speichert mehrere eigene Felder für einen Kunden via JTL TVP (effizient, nur ein DB-Aufruf)
        /// </summary>
        public async Task SetKundeEigeneFelderAsync(int kKunde, Dictionary<string, string?> felder)
        {
            if (felder == null || felder.Count == 0) return;

            var conn = await GetConnectionAsync();

            try
            {
                // JTL TVP mit allen Feldern auf einmal - Parameter: @KundenEigenesFeldAnpassen, @nFeldTyp
                var dt = new System.Data.DataTable();
                dt.Columns.Add("kKunde", typeof(int));
                dt.Columns.Add("cKey", typeof(string));
                dt.Columns.Add("cValue", typeof(string));

                foreach (var kvp in felder)
                {
                    dt.Rows.Add(kKunde, kvp.Key, kvp.Value ?? "");
                }

                var p = new DynamicParameters();
                p.Add("@KundenEigenesFeldAnpassen", dt.AsTableValuedParameter("Kunde.TYPE_KundenEigenesFeldAnpassen"));
                p.Add("@nFeldTyp", 12); // 12 = Default laut SP

                await conn.ExecuteAsync("Kunde.spKundenEigenesFeldCreateOrUpdate", p, commandType: CommandType.StoredProcedure);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "JTL TVP Bulk fehlgeschlagen für Kunde {kKunde}, verwende Fallback", kKunde);

                // Fallback: Einzelne Felder speichern
                foreach (var kvp in felder)
                {
                    await SetKundeEigenesFeldAsync(kKunde, kvp.Key, kvp.Value);
                }
            }
        }

        #endregion

        #region Lieferantenbestellung (JTL Native SPs)

        /// <summary>
        /// Lieferanten laden (tLieferant)
        /// </summary>
        public async Task<IEnumerable<LieferantRef>> GetLieferantenAsync()
        {
            var conn = await GetConnectionAsync();
            const string sql = @"
                SELECT kLieferant AS KLieferant, cFirma AS CFirma, cStrasse AS CStrasse,
                       cPLZ AS CPLZ, cOrt AS COrt, cLand AS CLand,
                       COALESCE(cTelZentralle, cTelDurchwahl, '') AS CTelefon,
                       cFax AS CFax, cEMail AS CEmail
                FROM dbo.tLieferant
                WHERE cAktiv = 'Y'
                ORDER BY cFirma";
            return await conn.QueryAsync<LieferantRef>(sql);
        }

        /// <summary>
        /// Warenlager laden (tWarenLager)
        /// </summary>
        public async Task<IEnumerable<WarenlagerRef>> GetWarenlagerAsync()
        {
            var conn = await GetConnectionAsync();
            const string sql = @"
                SELECT kWarenLager AS KWarenLager, cName AS CName
                FROM dbo.tWarenLager
                WHERE nAktiv = 1
                ORDER BY nStandard DESC, cName";
            return await conn.QueryAsync<WarenlagerRef>(sql);
        }

        /// <summary>
        /// Firmen laden (tFirma)
        /// </summary>
        public async Task<IEnumerable<FirmaRef>> GetFirmenAsync()
        {
            var conn = await GetConnectionAsync();
            const string sql = @"
                SELECT kFirma AS KFirma, cName AS CFirma, cStrasse AS CStrasse,
                       cPLZ AS CPLZ, cOrt AS COrt
                FROM dbo.tFirma
                ORDER BY cName";
            return await conn.QueryAsync<FirmaRef>(sql);
        }

        /// <summary>
        /// Lieferantenbestellung laden
        /// </summary>
        public async Task<LieferantenBestellungDto?> GetLieferantenBestellungAsync(int kLieferantenBestellung)
        {
            var conn = await GetConnectionAsync();
            const string sql = @"
                SELECT kLieferantenBestellung AS KLieferantenBestellung,
                       kLieferant AS KLieferant, kSprache AS KSprache,
                       cWaehrungISO AS CWaehrungISO, cInternerKommentar AS CInternerKommentar,
                       cDruckAnmerkung AS CDruckAnmerkung, nStatus AS NStatus,
                       dErstellt AS DErstellt, kFirma AS KFirma, kLager AS KLager,
                       kKunde AS KKunde, dLieferdatum AS DLieferdatum,
                       cEigeneBestellnummer AS CEigeneBestellnummer,
                       cBezugsAuftragsNummer AS CBezugsAuftragsNummer,
                       nDropShipping AS NDropShipping, cFremdbelegnummer AS CFremdbelegnummer
                FROM dbo.tLieferantenBestellung
                WHERE kLieferantenBestellung = @id";
            return await conn.QueryFirstOrDefaultAsync<LieferantenBestellungDto>(sql, new { id = kLieferantenBestellung });
        }

        /// <summary>
        /// Lieferantenbestellung Positionen laden
        /// </summary>
        public async Task<IEnumerable<LieferantenBestellungPosition>> GetLieferantenBestellungPositionenAsync(int kLieferantenBestellung)
        {
            var conn = await GetConnectionAsync();
            const string sql = @"
                SELECT kLieferantenBestellungPos AS KLieferantenBestellungPos,
                       kLieferantenBestellung AS KLieferantenBestellung,
                       kArtikel AS KArtikel, cArtNr AS CArtNr,
                       cLieferantenArtNr AS CLieferantenArtNr, cName AS CName,
                       cLieferantenBezeichnung AS CLieferantenBezeichnung,
                       fUST AS FUST, fMenge AS FMenge, cHinweis AS CHinweis,
                       fEKNetto AS FEKNetto, nPosTyp AS NPosTyp,
                       cNameLieferant AS CNameLieferant, nLiefertage AS NLiefertage,
                       dLieferdatum AS DLieferdatum, nSort AS NSort,
                       fMengeGeliefert AS FMengeGeliefert,
                       cVPEEinheit AS CVPEEinheit, nVPEMenge AS NVPEMenge
                FROM dbo.tLieferantenBestellungPos
                WHERE kLieferantenBestellung = @id
                ORDER BY nSort";
            return await conn.QueryAsync<LieferantenBestellungPosition>(sql, new { id = kLieferantenBestellung });
        }

        /// <summary>
        /// Lieferantenbestellung uebersicht laden
        /// </summary>
        public async Task<IEnumerable<LieferantenBestellungUebersicht>> GetLieferantenBestellungenAsync(int? kLieferant = null, int? status = null, int limit = 500)
        {
            var conn = await GetConnectionAsync();
            var sql = @"
                SELECT TOP (@limit)
                       lb.kLieferantenBestellung, lb.kLieferant, l.cFirma AS LieferantName,
                       lb.cEigeneBestellnummer, lb.cFremdbelegnummer, lb.nStatus,
                       lb.dErstellt, lb.dLieferdatum,
                       (SELECT SUM(p.fMenge * p.fEKNetto) FROM dbo.tLieferantenBestellungPos p
                        WHERE p.kLieferantenBestellung = lb.kLieferantenBestellung) AS NettoGesamt,
                       (SELECT COUNT(*) FROM dbo.tLieferantenBestellungPos p
                        WHERE p.kLieferantenBestellung = lb.kLieferantenBestellung) AS AnzahlPositionen
                FROM dbo.tLieferantenBestellung lb
                LEFT JOIN dbo.tLieferant l ON l.kLieferant = lb.kLieferant
                WHERE lb.nDeleted = 0
                  AND (@kLieferant IS NULL OR lb.kLieferant = @kLieferant)
                  AND (@status IS NULL OR lb.nStatus = @status)
                ORDER BY lb.dErstellt DESC";
            return await conn.QueryAsync<LieferantenBestellungUebersicht>(sql, new { limit, kLieferant, status });
        }

        /// <summary>
        /// Artikel fuer Lieferant finden (cArtNr oder cLieferantenArtNr)
        /// </summary>
        public async Task<ArtikelFuerLieferant?> FindeArtikelFuerLieferantAsync(string artNrOderLiefArtNr, int kLieferant)
        {
            var conn = await GetConnectionAsync();
            const string sql = @"
                SELECT TOP 1
                       a.kArtikel AS KArtikel, a.cArtNr AS CArtNr,
                       la.cLieferantenArtNr AS CLieferantenArtNr,
                       COALESCE(ab.cName, a.cArtNr) AS CName,
                       COALESCE(la.fUSt, 19.0) AS FUST,
                       COALESCE(la.fEKNetto, a.fEKNetto, 0) AS FEKNetto
                FROM dbo.tArtikel a
                LEFT JOIN dbo.tArtikelBeschreibung ab ON ab.kArtikel = a.kArtikel AND ab.kSprache = 1
                LEFT JOIN dbo.tLiefArtikel la ON la.tArtikel_kArtikel = a.kArtikel AND la.tLieferant_kLieferant = @kLieferant
                WHERE a.cArtNr = @suche OR la.cLieferantenArtNr = @suche OR a.cBarcode = @suche
                ORDER BY CASE WHEN la.tLieferant_kLieferant IS NOT NULL THEN 0 ELSE 1 END";
            return await conn.QueryFirstOrDefaultAsync<ArtikelFuerLieferant>(sql, new { suche = artNrOderLiefArtNr, kLieferant });
        }

        /// <summary>
        /// Lieferantenbestellung anlegen via JTL SP
        /// </summary>
        public async Task<int> CreateLieferantenBestellungAsync(LieferantenBestellungDto bestellung)
        {
            var conn = await GetConnectionAsync();

            // XML fuer Positionen erstellen
            var posXml = BuildPositionenXml(bestellung.Positionen);

            var p = new DynamicParameters();
            p.Add("@kLieferant", bestellung.KLieferant);
            p.Add("@kSprache", bestellung.KSprache);
            p.Add("@kLieferantenBestellungRA", 0);
            p.Add("@kLieferantenBestellungLA", 0);
            p.Add("@cWaehrungISO", bestellung.CWaehrungISO ?? "EUR");
            p.Add("@cInternerKommentar", bestellung.CInternerKommentar ?? "");
            p.Add("@cDruckAnmerkung", bestellung.CDruckAnmerkung ?? "");
            p.Add("@nStatus", bestellung.NStatus);
            p.Add("@kFirma", bestellung.KFirma);
            p.Add("@kLager", bestellung.KLager);
            p.Add("@kKunde", bestellung.KKunde);
            p.Add("@dLieferdatum", bestellung.DLieferdatum);
            p.Add("@cEigeneBestellnummer", bestellung.CEigeneBestellnummer ?? "");
            p.Add("@cBezugsAuftragsNummer", bestellung.CBezugsAuftragsNummer ?? "");
            p.Add("@nDropShipping", bestellung.NDropShipping);
            p.Add("@kLieferantenBestellungLieferant", 0);
            p.Add("@kBenutzer", 1); // TODO: Aktuellen Benutzer
            p.Add("@fFaktor", 1.0m);
            p.Add("@cFremdbelegnummer", bestellung.CFremdbelegnummer ?? "");
            p.Add("@kLieferschein", 0);
            p.Add("@nBestaetigt", 0);
            p.Add("@istGedruckt", 0);
            p.Add("@istGemailt", 0);
            p.Add("@istGefaxt", 0);
            p.Add("@nAngelegtDurchWMS", 0);
            p.Add("@xLieferantenbestellungPos", posXml);
            p.Add("@kLieferantenbestellung", dbType: DbType.Int32, direction: ParameterDirection.Output);

            await conn.ExecuteAsync("[Lieferantenbestellung].[spLieferantenBestellungErstellen]", p, commandType: CommandType.StoredProcedure);

            return p.Get<int>("@kLieferantenbestellung");
        }

        /// <summary>
        /// Lieferantenbestellung aktualisieren via JTL SP
        /// </summary>
        public async Task UpdateLieferantenBestellungAsync(LieferantenBestellungDto bestellung)
        {
            var conn = await GetConnectionAsync();

            var p = new DynamicParameters();
            p.Add("@kLieferantenBestellung", bestellung.KLieferantenBestellung);
            p.Add("@kLieferant", bestellung.KLieferant);
            p.Add("@kSprache", bestellung.KSprache);
            p.Add("@kLieferantenBestellungRA", 0);
            p.Add("@kLieferantenBestellungLA", 0);
            p.Add("@cWaehrungISO", bestellung.CWaehrungISO ?? "EUR");
            p.Add("@cInternerKommentar", bestellung.CInternerKommentar ?? "");
            p.Add("@cDruckAnmerkung", bestellung.CDruckAnmerkung ?? "");
            p.Add("@dGedruckt", DBNull.Value);
            p.Add("@dGemailt", DBNull.Value);
            p.Add("@dGefaxt", DBNull.Value);
            p.Add("@nStatus", bestellung.NStatus);
            p.Add("@dErstellt", bestellung.DErstellt);
            p.Add("@kFirma", bestellung.KFirma);
            p.Add("@kLager", bestellung.KLager);
            p.Add("@kKunde", bestellung.KKunde);
            p.Add("@dLieferdatum", bestellung.DLieferdatum);
            p.Add("@cEigeneBestellnummer", bestellung.CEigeneBestellnummer ?? "");
            p.Add("@cBezugsAuftragsNummer", bestellung.CBezugsAuftragsNummer ?? "");
            p.Add("@nDropShipping", bestellung.NDropShipping);
            p.Add("@kLieferantenBestellungLieferant", 0);
            p.Add("@kBenutzer", 1);
            p.Add("@fFaktor", 1.0m);
            p.Add("@dAngemahnt", DBNull.Value);
            p.Add("@dInBearbeitung", DBNull.Value);
            p.Add("@nDeleted", 0);
            p.Add("@nManuellAbgeschlossen", 0);
            p.Add("@cFremdbelegnummer", bestellung.CFremdbelegnummer ?? "");
            p.Add("@kLieferschein", 0);
            p.Add("@nBestaetigt", 0);
            p.Add("@dExportiert", DBNull.Value);

            await conn.ExecuteAsync("[Lieferantenbestellung].[spLieferantenBestellungBearbeiten]", p, commandType: CommandType.StoredProcedure);

            // Positionen aktualisieren (erst loeschen, dann neu anlegen)
            await UpdateLieferantenBestellungPositionenAsync(bestellung.KLieferantenBestellung, bestellung.Positionen);
        }

        /// <summary>
        /// Lieferantenbestellung loeschen (soft delete)
        /// </summary>
        public async Task DeleteLieferantenBestellungAsync(int kLieferantenBestellung)
        {
            var conn = await GetConnectionAsync();
            const string sql = "UPDATE dbo.tLieferantenBestellung SET nDeleted = 1 WHERE kLieferantenBestellung = @id";
            await conn.ExecuteAsync(sql, new { id = kLieferantenBestellung });
        }

        /// <summary>
        /// Lieferantenbestellung duplizieren
        /// </summary>
        public async Task<int> DuplicateLieferantenBestellungAsync(int kLieferantenBestellung)
        {
            // Original laden
            var original = await GetLieferantenBestellungAsync(kLieferantenBestellung);
            if (original == null) throw new Exception("Bestellung nicht gefunden");

            var positionen = (await GetLieferantenBestellungPositionenAsync(kLieferantenBestellung)).ToList();

            // Als neue Bestellung anlegen
            original.KLieferantenBestellung = 0;
            original.DErstellt = DateTime.Now;
            original.NStatus = 5; // Entwurf
            original.CEigeneBestellnummer = "";
            original.CFremdbelegnummer = "";
            original.Positionen = positionen;

            // Positionen zuruecksetzen
            foreach (var pos in original.Positionen)
            {
                pos.KLieferantenBestellungPos = 0;
                pos.KLieferantenBestellung = 0;
                pos.FMengeGeliefert = 0;
            }

            return await CreateLieferantenBestellungAsync(original);
        }

        private async Task UpdateLieferantenBestellungPositionenAsync(int kLieferantenBestellung, List<LieferantenBestellungPosition> positionen)
        {
            var conn = await GetConnectionAsync();

            // Vorhandene Positionen laden
            var existingPos = (await GetLieferantenBestellungPositionenAsync(kLieferantenBestellung)).ToList();
            var existingIds = existingPos.Select(p => p.KLieferantenBestellungPos).ToHashSet();
            var newIds = positionen.Where(p => p.KLieferantenBestellungPos > 0).Select(p => p.KLieferantenBestellungPos).ToHashSet();

            // Geloeschte Positionen entfernen
            var toDelete = existingIds.Except(newIds);
            foreach (var posId in toDelete)
            {
                await conn.ExecuteAsync("DELETE FROM dbo.tLieferantenBestellungPos WHERE kLieferantenBestellungPos = @id", new { id = posId });
            }

            // Positionen aktualisieren oder neu anlegen
            foreach (var pos in positionen)
            {
                if (pos.KLieferantenBestellungPos > 0)
                {
                    // Update via SP
                    var p = new DynamicParameters();
                    p.Add("@kLieferantenbestellungPos", pos.KLieferantenBestellungPos);
                    p.Add("@kLieferantenbestellung", kLieferantenBestellung);
                    p.Add("@kArtikel", pos.KArtikel);
                    p.Add("@cArtNr", pos.CArtNr);
                    p.Add("@cLieferantenArtNr", pos.CLieferantenArtNr);
                    p.Add("@cName", pos.CName);
                    p.Add("@cLieferantenBezeichnung", pos.CLieferantenBezeichnung);
                    p.Add("@fUST", pos.FUST);
                    p.Add("@fMenge", pos.FMenge);
                    p.Add("@cHinweis", pos.CHinweis);
                    p.Add("@fEKNetto", pos.FEKNetto);
                    p.Add("@nPosTyp", pos.NPosTyp);
                    p.Add("@cNameLieferant", pos.CNameLieferant);
                    p.Add("@nLiefertage", pos.NLiefertage);
                    p.Add("@dLieferdatum", pos.DLieferdatum);
                    p.Add("@nSort", pos.NSort);
                    p.Add("@kLieferscheinPos", 0);
                    p.Add("@fMengeGeliefert", pos.FMengeGeliefert);
                    p.Add("@cVPEEinheit", pos.CVPEEinheit);
                    p.Add("@nVPEMenge", pos.NVPEMenge);

                    await conn.ExecuteAsync("[Lieferantenbestellung].[spLieferantenBestellungPosBearbeiten]", p, commandType: CommandType.StoredProcedure);
                }
                else
                {
                    // Neu anlegen via SP
                    var p = new DynamicParameters();
                    p.Add("@kLieferantenbestellung", kLieferantenBestellung);
                    p.Add("@kArtikel", pos.KArtikel);
                    p.Add("@cArtNr", pos.CArtNr);
                    p.Add("@cLieferantenArtNr", pos.CLieferantenArtNr);
                    p.Add("@cName", pos.CName);
                    p.Add("@cLieferantenBezeichnung", pos.CLieferantenBezeichnung);
                    p.Add("@fUST", pos.FUST);
                    p.Add("@fMenge", pos.FMenge);
                    p.Add("@cHinweis", pos.CHinweis);
                    p.Add("@fEKNetto", pos.FEKNetto);
                    p.Add("@nPosTyp", pos.NPosTyp);
                    p.Add("@cNameLieferant", pos.CNameLieferant);
                    p.Add("@nLiefertage", pos.NLiefertage);
                    p.Add("@dLieferdatum", pos.DLieferdatum);
                    p.Add("@nSort", pos.NSort);
                    p.Add("@kLieferscheinPos", 0);
                    p.Add("@cVPEEinheit", pos.CVPEEinheit);
                    p.Add("@nVPEMenge", pos.NVPEMenge);
                    p.Add("@nStatus", DBNull.Value);
                    p.Add("@kLieferantenbestellungPos", dbType: DbType.Int32, direction: ParameterDirection.Output);

                    await conn.ExecuteAsync("[Lieferantenbestellung].[spLieferantenBestellungPosErstellen]", p, commandType: CommandType.StoredProcedure);
                }
            }
        }

        private string BuildPositionenXml(List<LieferantenBestellungPosition> positionen)
        {
            if (positionen == null || !positionen.Any()) return null!;

            var sb = new System.Text.StringBuilder();
            foreach (var pos in positionen)
            {
                sb.AppendLine($@"<LieferantenbestellungPos>
                    <kArtikel>{pos.KArtikel}</kArtikel>
                    <cArtNr>{System.Security.SecurityElement.Escape(pos.CArtNr)}</cArtNr>
                    <cLieferantenArtNr>{System.Security.SecurityElement.Escape(pos.CLieferantenArtNr ?? "")}</cLieferantenArtNr>
                    <cName>{System.Security.SecurityElement.Escape(pos.CName)}</cName>
                    <cLieferantenBezeichnung>{System.Security.SecurityElement.Escape(pos.CLieferantenBezeichnung ?? "")}</cLieferantenBezeichnung>
                    <fUST>{pos.FUST}</fUST>
                    <fMenge>{pos.FMenge}</fMenge>
                    <cHinweis>{System.Security.SecurityElement.Escape(pos.CHinweis ?? "")}</cHinweis>
                    <fEKNetto>{pos.FEKNetto}</fEKNetto>
                    <nPosTyp>{pos.NPosTyp}</nPosTyp>
                    <cNameLieferant>{System.Security.SecurityElement.Escape(pos.CNameLieferant ?? "")}</cNameLieferant>
                    <nLiefertage>{pos.NLiefertage}</nLiefertage>
                    <dLieferdatum>{pos.DLieferdatum?.ToString("yyyy-MM-ddTHH:mm:ss") ?? ""}</dLieferdatum>
                    <nSort>{pos.NSort}</nSort>
                    <kLieferscheinPos>0</kLieferscheinPos>
                    <cVPEEinheit>{System.Security.SecurityElement.Escape(pos.CVPEEinheit ?? "")}</cVPEEinheit>
                    <nVPEMenge>{pos.NVPEMenge}</nVPEMenge>
                </LieferantenbestellungPos>");
            }
            return sb.ToString();
        }

        public class LieferantenBestellungUebersicht
        {
            public int KLieferantenBestellung { get; set; }
            public int KLieferant { get; set; }
            public string? LieferantName { get; set; }
            public string? CEigeneBestellnummer { get; set; }
            public string? CFremdbelegnummer { get; set; }
            public int NStatus { get; set; }
            public DateTime? DErstellt { get; set; }
            public DateTime? DLieferdatum { get; set; }
            public decimal NettoGesamt { get; set; }
            public int AnzahlPositionen { get; set; }

            public string StatusText => NStatus switch
            {
                5 => "Entwurf",
                10 => "Offen",
                20 => "In Bearbeitung",
                30 => "Teilgeliefert",
                50 => "Abgeschlossen",
                100 => "Storniert",
                _ => $"Status {NStatus}"
            };
        }

        public class LieferantRef
        {
            public int KLieferant { get; set; }
            public string CFirma { get; set; } = "";
            public string CStrasse { get; set; } = "";
            public string CPLZ { get; set; } = "";
            public string COrt { get; set; } = "";
            public string CLand { get; set; } = "";
            public string CTelefon { get; set; } = "";
            public string CFax { get; set; } = "";
            public string CEmail { get; set; } = "";
        }

        public class WarenlagerRef
        {
            public int KWarenLager { get; set; }
            public string CName { get; set; } = "";
        }

        public class FirmaRef
        {
            public int KFirma { get; set; }
            public string CFirma { get; set; } = "";
            public string CStrasse { get; set; } = "";
            public string CPLZ { get; set; } = "";
            public string COrt { get; set; } = "";
        }

        public class LieferantenBestellungPosition
        {
            public int KLieferantenBestellungPos { get; set; }
            public int KLieferantenBestellung { get; set; }
            public int KArtikel { get; set; }
            public string CArtNr { get; set; } = "";
            public string CLieferantenArtNr { get; set; } = "";
            public string CName { get; set; } = "";
            public string CLieferantenBezeichnung { get; set; } = "";
            public decimal FUST { get; set; }
            public decimal FMenge { get; set; }
            public string CHinweis { get; set; } = "";
            public decimal FEKNetto { get; set; }
            public int NPosTyp { get; set; } = 1;
            public string CNameLieferant { get; set; } = "";
            public int NLiefertage { get; set; }
            public DateTime? DLieferdatum { get; set; }
            public int NSort { get; set; }
            public decimal FMengeGeliefert { get; set; }
            public string CVPEEinheit { get; set; } = "";
            public decimal NVPEMenge { get; set; }
            public decimal NettoGesamt => FMenge * FEKNetto;
        }

        public class LieferantenBestellungDto
        {
            public int KLieferantenBestellung { get; set; }
            public int KLieferant { get; set; }
            public int KSprache { get; set; } = 1;
            public string CWaehrungISO { get; set; } = "EUR";
            public string CInternerKommentar { get; set; } = "";
            public string CDruckAnmerkung { get; set; } = "";
            public int NStatus { get; set; }
            public DateTime DErstellt { get; set; }
            public int KFirma { get; set; }
            public int KLager { get; set; }
            public int? KKunde { get; set; }
            public DateTime? DLieferdatum { get; set; }
            public string CEigeneBestellnummer { get; set; } = "";
            public string CBezugsAuftragsNummer { get; set; } = "";
            public int NDropShipping { get; set; }
            public string CFremdbelegnummer { get; set; } = "";
            public List<LieferantenBestellungPosition> Positionen { get; set; } = new();
        }

        public class ArtikelFuerLieferant
        {
            public int KArtikel { get; set; }
            public string CArtNr { get; set; } = "";
            public string CLieferantenArtNr { get; set; } = "";
            public string CName { get; set; } = "";
            public decimal FUST { get; set; }
            public decimal FEKNetto { get; set; }
        }

        #endregion
    }
}

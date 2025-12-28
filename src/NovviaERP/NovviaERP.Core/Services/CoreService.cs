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

        public class KundeStatistik
        {
            public int KKunde { get; set; }
            public decimal UmsatzGesamt { get; set; }
            public decimal UmsatzJahr { get; set; }
            public int AnzahlAuftraege { get; set; }
            public decimal DurchschnittWarenkorb { get; set; }
            public decimal OffenePosten { get; set; }
            public int AnzahlRetouren { get; set; }
            public int AnzahlMahnungen { get; set; }
            public DateTime? ErstBestellung { get; set; }
            public DateTime? LetzteBestellung { get; set; }
        }

        public class KundeAuftragKurz
        {
            public int KBestellung { get; set; }
            public string CBestellNr { get; set; } = "";
            public DateTime DErstellt { get; set; }
            public string CStatus { get; set; } = "";
            public decimal GesamtBrutto { get; set; }
        }

        public class KundeRechnungKurz
        {
            public int KRechnung { get; set; }
            public string CRechnungsNr { get; set; } = "";
            public DateTime? DRechnungsdatum { get; set; }
            public decimal FBetragBrutto { get; set; }
            public decimal FBezahlt { get; set; }
            public decimal Offen => FBetragBrutto - FBezahlt;
        }

        public class KundeAdresseKurz
        {
            public int KAdresse { get; set; }
            public int NTyp { get; set; }
            public string CName { get; set; } = "";
            public string CStrasse { get; set; } = "";
            public string CPLZ { get; set; } = "";
            public string COrt { get; set; } = "";
            public bool NStandard { get; set; }
            public string AdressTypText => NTyp switch { 1 => "Rechnung", 2 => "Lieferung", _ => "Sonstige" };
        }

        public class KundeHistorieEintrag
        {
            public DateTime Datum { get; set; }
            public string Typ { get; set; } = "";
            public string Beschreibung { get; set; } = "";
        }

        public class KundeDetail
        {
            // Aus tKunde
            public int KKunde { get; set; }
            public int? KInetKunde { get; set; }
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

            // Ansprechpartner
            public List<AnsprechpartnerDetail> Ansprechpartner { get; set; } = new();

            // Bankverbindungen
            public List<BankverbindungDetail> Bankverbindungen { get; set; } = new();

            // Onlineshop-Kunden
            public List<OnlineshopKundeDetail> OnlineshopKunden { get; set; } = new();

            // Kundengruppe
            public string? Kundengruppe { get; set; }
            public string? Kundenkategorie { get; set; }
            public string? Zahlungsart { get; set; }
            public string? Sprache { get; set; }
            public string CKassenKunde { get; set; } = "N";

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

        public class AnsprechpartnerDetail
        {
            public int KAnsprechpartner { get; set; }
            public int? KKunde { get; set; }
            public int? KLieferant { get; set; }
            public string? CAnrede { get; set; }
            public string? CVorname { get; set; }
            public string? CName { get; set; }
            public string? CAbteilung { get; set; }
            public string? CTel { get; set; }
            public string? CMobil { get; set; }
            public string? CFax { get; set; }
            public string? CMail { get; set; }
        }

        public class BankverbindungDetail
        {
            public int KKontoDaten { get; set; }
            public int? KKunde { get; set; }
            public int? KLieferant { get; set; }
            public string? CBankName { get; set; }
            public string? CBLZ { get; set; }
            public string? CKontoNr { get; set; }
            public string? CInhaber { get; set; }
            public string? CIBAN { get; set; }
            public string? CBIC { get; set; }
            public byte NStandard { get; set; }
            public bool IstStandard => NStandard == 1;
        }

        public class OnlineshopKundeDetail
        {
            public int KInetKunde { get; set; }
            public int? KShop { get; set; }
            public string? ShopName { get; set; }
            public string? CKundenNr { get; set; }
            public string? CBenutzername { get; set; }
            public string? CAnrede { get; set; }
            public string? CVorname { get; set; }
            public string? CNachname { get; set; }
            public string? CFirma { get; set; }
            public string? CMail { get; set; }
            public string? CTel { get; set; }
            public byte NAktiv { get; set; }
            public decimal FRabatt { get; set; }
            public int? KKundenGruppe { get; set; }
            public bool IstAktiv => NAktiv == 1;
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
            public DateTime? DVoraussichtlichesLieferdatum { get; set; }
            public int NLieferPrioritaet { get; set; }  // 0=Normal
            public decimal FZusatzGewicht { get; set; }
            public int? KArtikelKarton { get; set; }    // Karton

            // Zahlung
            public int? KZahlungsart { get; set; }
            public string? ZahlungsartName { get; set; }
            public DateTime? DBezahlt { get; set; }
            public int NZahlungsZiel { get; set; }
            public decimal FSkonto { get; set; }        // Skonto-Prozentsatz
            public int NSkontoTage { get; set; }        // Skonto-Tage

            // Steuern
            public int NSteuereinstellung { get; set; } // 0=Steuerpflichtig, 10=InnergemLief, 15=Export, 20=Differenzbesteuert
            public string? SteuerartName { get; set; }  // Berechneter Name

            // Auftragsstatus (Vorgangssteuerung)
            public int? KVorgangsstatus { get; set; }
            public string? VorgangsstatusName { get; set; }
            public int? KRueckhaltegrund { get; set; }
            public string? RueckhaltegrundName { get; set; }
            public int? KFarbe { get; set; }            // Vorgangsfarbe

            // Adressen
            public AdresseDetail? Rechnungsadresse { get; set; }
            public AdresseDetail? Lieferadresse { get; set; }

            // Shop
            public int? KShop { get; set; }
            public string? ShopName { get; set; }

            // Referenzen
            public int KSprache { get; set; }           // Sprache (0=Deutsch, 1=Englisch, etc.)
            public string? SpracheName { get; set; }

            // Anmerkungen (aus tBestellung)
            public string? CAnmerkung { get; set; }
            public string? CVerwendungszweck { get; set; }

            // Verkauftexte (aus Verkauf.tAuftragText)
            public string? CDrucktext { get; set; }      // Kopf-/Fusstext fuer Dokumente
            public string? CHinweis { get; set; }        // Interner Hinweis
            public string? CVorgangsstatus { get; set; } // Vorgangsstatus

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

            // Netto-VK (ges.) mit Rabatt: Anzahl * Netto * (1 - Rabatt/100)
            public decimal SummeNetto => FAnzahl * FVKNetto * (1 - (FRabatt ?? 0) / 100);
            // Brutto-VK (ges.) mit Rabatt
            public decimal Summe => SummeNetto * (1 + FMwSt / 100);
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
                SELECT k.*,
                       kg.cName AS Kundengruppe,
                       kk.cName AS Kundenkategorie,
                       za.cName AS Zahlungsart,
                       (SELECT COUNT(*) FROM tBestellung WHERE tKunde_kKunde = k.kKunde) AS AnzahlBestellungen,
                       ISNULL((SELECT SUM(bp.nAnzahl * bp.fVkNetto)
                               FROM tBestellung b
                               INNER JOIN tbestellpos bp ON b.kBestellung = bp.tBestellung_kBestellung
                               WHERE b.tKunde_kKunde = k.kKunde AND b.nStorno = 0), 0) AS GesamtUmsatz
                FROM tkunde k
                LEFT JOIN tKundenGruppe kg ON kg.kKundenGruppe = k.kKundenGruppe
                LEFT JOIN tKundenKategorie kk ON kk.kKundenKategorie = k.kKundenKategorie
                LEFT JOIN tZahlungsArt za ON za.kZahlungsart = k.kZahlungsart
                WHERE k.kKunde = @Id", new { Id = kundeId });

            if (kunde == null) return null;

            // Adressen
            kunde.Adressen = (await conn.QueryAsync<AdresseDetail>(@"
                SELECT * FROM tAdresse WHERE kKunde = @Id ORDER BY nStandard DESC, nTyp", new { Id = kundeId })).ToList();

            kunde.StandardAdresse = kunde.Adressen.FirstOrDefault(a => a.NStandard == 1) ?? kunde.Adressen.FirstOrDefault();

            // Ansprechpartner
            kunde.Ansprechpartner = (await conn.QueryAsync<AnsprechpartnerDetail>(@"
                SELECT * FROM tansprechpartner WHERE kKunde = @Id ORDER BY cName", new { Id = kundeId })).ToList();

            // Bankverbindungen
            kunde.Bankverbindungen = (await conn.QueryAsync<BankverbindungDetail>(@"
                SELECT * FROM tkontodaten WHERE kKunde = @Id ORDER BY nStandard DESC", new { Id = kundeId })).ToList();

            // Onlineshop-Kunden
            kunde.OnlineshopKunden = (await conn.QueryAsync<OnlineshopKundeDetail>(@"
                SELECT ik.*, s.cName AS ShopName
                FROM tinetkunde ik
                LEFT JOIN tShop s ON s.kShop = ik.kShop
                WHERE ik.kInetKunde = @InetKundeId OR ik.cKundenNr = @KundenNr
                ORDER BY s.cName",
                new { InetKundeId = kunde.KInetKunde, KundenNr = kunde.CKundenNr })).ToList();

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

        /// <summary>
        /// Kunden-Statistiken für 360°-Ansicht
        /// </summary>
        public async Task<KundeStatistik> GetKundeStatistikAsync(int kundeId)
        {
            var conn = await GetConnectionAsync();
            var stats = new KundeStatistik { KKunde = kundeId };

            try
            {
                // Aufträge zählen
                var anzahl = await conn.QueryFirstOrDefaultAsync<int>(
                    "SELECT COUNT(*) FROM tBestellung WHERE tKunde_kKunde = @kundeId AND nStorno = 0", new { kundeId });
                stats.AnzahlAuftraege = anzahl;

                // Umsatz berechnen (wie in anderen Queries - aus Positionen)
                var umsatz = await conn.QueryFirstOrDefaultAsync<decimal?>(@"
                    SELECT SUM(bp.nAnzahl * bp.fVkNetto * (1 + bp.fMwSt/100))
                    FROM tBestellung b
                    INNER JOIN tbestellpos bp ON b.kBestellung = bp.tBestellung_kBestellung
                    WHERE b.tKunde_kKunde = @kundeId AND b.nStorno = 0", new { kundeId });
                stats.UmsatzGesamt = umsatz ?? 0;

                // Durchschnitt
                stats.DurchschnittWarenkorb = stats.AnzahlAuftraege > 0
                    ? stats.UmsatzGesamt / stats.AnzahlAuftraege : 0;

                // Erste/Letzte Bestellung
                var datumStats = await conn.QueryFirstOrDefaultAsync<(DateTime? Erste, DateTime? Letzte)?>(@"
                    SELECT MIN(dErstellt) AS Erste, MAX(dErstellt) AS Letzte
                    FROM tBestellung WHERE tKunde_kKunde = @kundeId AND nStorno = 0", new { kundeId });
                if (datumStats.HasValue)
                {
                    stats.ErstBestellung = datumStats.Value.Erste;
                    stats.LetzteBestellung = datumStats.Value.Letzte;
                }

                // Offene Posten aus Rechnungen (fOffen ist in JTL direkt verfügbar)
                var offen = await conn.QueryFirstOrDefaultAsync<decimal?>(@"
                    SELECT ISNULL(SUM(fOffen), 0)
                    FROM tRechnung
                    WHERE kKunde = @kundeId AND nStatus IN (1,2) AND fOffen > 0", new { kundeId });
                stats.OffenePosten = offen ?? 0;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Fehler beim Laden der Kundenstatistik für Kunde {KundeId}", kundeId);
            }

            return stats;
        }

        /// <summary>
        /// Aufträge eines Kunden (für 360°-Ansicht)
        /// </summary>
        public async Task<IEnumerable<KundeAuftragKurz>> GetKundeAuftraegeAsync(int kundeId, int limit = 20)
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<KundeAuftragKurz>($@"
                SELECT TOP {limit}
                    b.kBestellung AS KBestellung,
                    ISNULL(b.cBestellNr, CAST(b.kBestellung AS VARCHAR)) AS CBestellNr,
                    b.dErstellt AS DErstellt,
                    ISNULL(b.cStatus, 'Offen') AS CStatus,
                    ISNULL((SELECT SUM(bp.nAnzahl * bp.fVkNetto * (1 + bp.fMwSt/100))
                            FROM tBestellPos bp WHERE bp.tBestellung_kBestellung = b.kBestellung), 0) AS GesamtBrutto
                FROM tBestellung b
                WHERE b.tKunde_kKunde = @kundeId
                ORDER BY b.dErstellt DESC", new { kundeId });
        }

        /// <summary>
        /// Rechnungen eines Kunden (für 360°-Ansicht)
        /// </summary>
        public async Task<IEnumerable<KundeRechnungKurz>> GetKundeRechnungenAsync(int kundeId, int limit = 20)
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<KundeRechnungKurz>($@"
                SELECT TOP {limit}
                    r.kRechnung AS KRechnung,
                    ISNULL(r.cRechnungsNr, CAST(r.kRechnung AS VARCHAR)) AS CRechnungsNr,
                    r.dErstellt AS DRechnungsdatum,
                    ISNULL(r.fBrutto, 0) AS FBetragBrutto,
                    ISNULL(r.fBezahlt, 0) AS FBezahlt
                FROM tRechnung r
                WHERE r.kKunde = @kundeId
                ORDER BY r.dErstellt DESC", new { kundeId });
        }

        /// <summary>
        /// Adressen eines Kunden (für 360°-Ansicht)
        /// </summary>
        public async Task<IEnumerable<KundeAdresseKurz>> GetKundeAdressenKurzAsync(int kundeId)
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<KundeAdresseKurz>(@"
                SELECT kAdresse AS KAdresse, ISNULL(nTyp, 0) AS NTyp,
                       ISNULL(cName, '') + CASE WHEN cVorname IS NOT NULL THEN ', ' + cVorname ELSE '' END AS CName,
                       ISNULL(cStrasse, '') AS CStrasse, ISNULL(cPLZ, '') AS CPLZ, ISNULL(cOrt, '') AS COrt,
                       CASE WHEN nStandard = 1 THEN 1 ELSE 0 END AS NStandard
                FROM tAdresse
                WHERE kKunde = @kundeId
                ORDER BY nStandard DESC, nTyp", new { kundeId });
        }

        /// <summary>
        /// Historie eines Kunden (für 360°-Ansicht)
        /// </summary>
        public async Task<IEnumerable<KundeHistorieEintrag>> GetKundeHistorieAsync(int kundeId, int limit = 30)
        {
            var conn = await GetConnectionAsync();

            // Kombiniere Aufträge, Rechnungen, etc. in eine Timeline
            var historie = new List<KundeHistorieEintrag>();

            // Aufträge
            try
            {
                var auftraege = await conn.QueryAsync<KundeHistorieEintrag>($@"
                    SELECT TOP {limit} dErstellt AS Datum, 'Auftrag' AS Typ,
                           'Auftrag ' + ISNULL(cBestellNr, CAST(kBestellung AS VARCHAR)) AS Beschreibung
                    FROM tBestellung WHERE tKunde_kKunde = @kundeId
                    ORDER BY dErstellt DESC", new { kundeId });
                historie.AddRange(auftraege);
            }
            catch { /* Aufträge optional */ }

            // Rechnungen
            try
            {
                var rechnungen = await conn.QueryAsync<KundeHistorieEintrag>($@"
                    SELECT TOP {limit} dErstellt AS Datum, 'Rechnung' AS Typ,
                           'Rechnung ' + ISNULL(cRechnungsNr, CAST(kRechnung AS VARCHAR)) AS Beschreibung
                    FROM tRechnung WHERE kKunde = @kundeId
                    ORDER BY dErstellt DESC", new { kundeId });
                historie.AddRange(rechnungen);
            }
            catch { /* Rechnungen optional */ }

            return historie.OrderByDescending(h => h.Datum).Take(limit);
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
                    ISNULL((SELECT SUM(bp.nAnzahl * bp.fVkNetto * (1 - ISNULL(bp.fRabatt,0)/100)) FROM tbestellpos bp WHERE bp.tBestellung_kBestellung = b.kBestellung), 0) AS GesamtNetto,
                    ISNULL((SELECT SUM(bp.nAnzahl * bp.fVkNetto * (1 - ISNULL(bp.fRabatt,0)/100) * (1 + bp.fMwSt/100)) FROM tbestellpos bp WHERE bp.tBestellung_kBestellung = b.kBestellung), 0) AS GesamtBrutto
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

            var bestellung = await conn.QueryFirstOrDefaultAsync<BestellungDetail>(@"
                SELECT b.*,
                       k.cKundenNr,
                       a.cName AS KundeName, a.cFirma AS KundeFirma, a.cMail AS KundeMail, a.cTel AS KundeTel,
                       v.cName AS VersandartName,
                       z.cName AS ZahlungsartName,
                       s.cName AS ShopName,
                       ISNULL((SELECT SUM(bp.nAnzahl * bp.fVkNetto * (1 - ISNULL(bp.fRabatt,0)/100)) FROM tbestellpos bp WHERE bp.tBestellung_kBestellung = b.kBestellung), 0) AS GesamtNetto,
                       ISNULL((SELECT SUM(bp.nAnzahl * bp.fVkNetto * (1 - ISNULL(bp.fRabatt,0)/100) * (1 + bp.fMwSt/100)) FROM tbestellpos bp WHERE bp.tBestellung_kBestellung = b.kBestellung), 0) AS GesamtBrutto
                FROM tBestellung b
                LEFT JOIN tkunde k ON b.tKunde_kKunde = k.kKunde
                OUTER APPLY (SELECT TOP 1 * FROM tAdresse WHERE kKunde = k.kKunde AND nStandard = 1 ORDER BY nTyp) a
                LEFT JOIN tVersandArt v ON b.tVersandArt_kVersandArt = v.kVersandArt
                LEFT JOIN tZahlungsArt z ON b.kZahlungsart = z.kZahlungsart
                LEFT JOIN tShop s ON b.kShop = s.kShop
                WHERE b.kBestellung = @Id", new { Id = bestellungId });

            if (bestellung == null) return null;

            // Positionen - cString enthält den Positionstext, ab.cName ist der Artikelname
            bestellung.Positionen = (await conn.QueryAsync<BestellPositionDetail>(@"
                SELECT bp.kBestellPos, bp.tArtikel_kArtikel, bp.cArtNr,
                       COALESCE(bp.cString, ab.cName, bp.cArtNr) AS CName,
                       bp.nAnzahl AS FAnzahl, bp.fVkNetto AS FVKNetto, bp.fMwSt,
                       CAST(bp.fVkNetto * (1 + bp.fMwSt / 100) AS DECIMAL(18,2)) AS FVKBrutto,
                       bp.fRabatt, bp.cEinheit, bp.nType AS NPosTyp
                FROM tbestellpos bp
                LEFT JOIN tArtikel a ON bp.tArtikel_kArtikel = a.kArtikel
                LEFT JOIN tArtikelBeschreibung ab ON a.kArtikel = ab.kArtikel AND ab.kSprache = 1
                WHERE bp.tBestellung_kBestellung = @Id
                ORDER BY bp.nSort, bp.kBestellPos", new { Id = bestellungId })).ToList();

            // Rechnungsadresse aus Verkauf.tAuftragAdresse (nTyp = 1)
            bestellung.Rechnungsadresse = await conn.QuerySingleOrDefaultAsync<AdresseDetail>(@"
                SELECT kAuftrag AS KAdresse, kKunde AS KKunde, cFirma AS CFirma, cAnrede AS CAnrede,
                       cTitel AS CTitel, cVorname AS CVorname, cName AS CName, cStrasse AS CStrasse,
                       cPLZ AS CPLZ, cOrt AS COrt, cLand AS CLand, cTel AS CTel, cMobil AS CMobil,
                       cFax AS CFax, cMail AS CMail, cZusatz AS CZusatz, cAdressZusatz AS CAdressZusatz,
                       cBundesland AS CBundesland, cISO AS CISO, nTyp AS NTyp
                FROM Verkauf.tAuftragAdresse
                WHERE kAuftrag = @Id AND nTyp = 1", new { Id = bestellungId });

            // Lieferadresse aus Verkauf.tAuftragAdresse (nTyp = 0)
            bestellung.Lieferadresse = await conn.QuerySingleOrDefaultAsync<AdresseDetail>(@"
                SELECT kAuftrag AS KAdresse, kKunde AS KKunde, cFirma AS CFirma, cAnrede AS CAnrede,
                       cTitel AS CTitel, cVorname AS CVorname, cName AS CName, cStrasse AS CStrasse,
                       cPLZ AS CPLZ, cOrt AS COrt, cLand AS CLand, cTel AS CTel, cMobil AS CMobil,
                       cFax AS CFax, cMail AS CMail, cZusatz AS CZusatz, cAdressZusatz AS CAdressZusatz,
                       cBundesland AS CBundesland, cISO AS CISO, nTyp AS NTyp
                FROM Verkauf.tAuftragAdresse
                WHERE kAuftrag = @Id AND nTyp = 0", new { Id = bestellungId });

            // Verkauf.tAuftrag Details laden (Steuern, Zahlung, Versand, Vorgangsstatus)
            try
            {
                var auftrag = await conn.QuerySingleOrDefaultAsync<dynamic>(@"
                    SELECT a.nSteuereinstellung, a.nZahlungszielTage, a.fSkonto, a.nSkontoTage,
                           a.kVorgangsstatus, a.kRueckhaltegrund, a.kFarbe,
                           a.dVoraussichtlichesLieferdatum, a.nLieferPrioritaet, a.fZusatzGewicht,
                           a.kArtikelKarton, a.kSprache,
                           vs.cName AS VorgangsstatusName,
                           rg.cName AS RueckhaltegrundName,
                           sp.cNameDeutsch AS SpracheName
                    FROM Verkauf.tAuftrag a
                    LEFT JOIN Verkauf.tVorgangsstatus vs ON a.kVorgangsstatus = vs.kVorgangsstatus
                    LEFT JOIN tRueckhalteGrund rg ON a.kRueckhaltegrund = rg.kRueckhalteGrund
                    LEFT JOIN tSprache sp ON a.kSprache = sp.kSprache
                    WHERE a.kAuftrag = @Id", new { Id = bestellungId });

                if (auftrag != null)
                {
                    bestellung.NSteuereinstellung = (int)(auftrag.nSteuereinstellung ?? 0);
                    bestellung.NZahlungsZiel = (int)(auftrag.nZahlungszielTage ?? 0);
                    bestellung.FSkonto = (decimal)(auftrag.fSkonto ?? 0m);
                    bestellung.NSkontoTage = (int)(auftrag.nSkontoTage ?? 0);
                    bestellung.KVorgangsstatus = (int?)auftrag.kVorgangsstatus;
                    bestellung.VorgangsstatusName = (string?)auftrag.VorgangsstatusName;
                    bestellung.KRueckhaltegrund = (int?)auftrag.kRueckhaltegrund;
                    bestellung.RueckhaltegrundName = (string?)auftrag.RueckhaltegrundName;
                    bestellung.KFarbe = (int?)auftrag.kFarbe;
                    bestellung.DVoraussichtlichesLieferdatum = (DateTime?)auftrag.dVoraussichtlichesLieferdatum;
                    bestellung.NLieferPrioritaet = (int)(auftrag.nLieferPrioritaet ?? 0);
                    bestellung.FZusatzGewicht = (decimal)(auftrag.fZusatzGewicht ?? 0m);
                    bestellung.KArtikelKarton = (int?)auftrag.kArtikelKarton;
                    bestellung.KSprache = (int)(auftrag.kSprache ?? 0);
                    bestellung.SpracheName = (string?)auftrag.SpracheName ?? "Deutsch";

                    // Steuerart-Name berechnen
                    bestellung.SteuerartName = bestellung.NSteuereinstellung switch
                    {
                        0 => "Steuerpflichtige Lieferung",
                        10 => "Innergemeinschaftliche Lieferung",
                        15 => "Ausfuhrlieferung (Export)",
                        20 => "Differenzbesteuert",
                        _ => $"Steuereinstellung {bestellung.NSteuereinstellung}"
                    };
                }
            }
            catch
            {
                // Verkauf.tAuftrag existiert moeglicherweise nicht
            }

            // Verkauftexte aus Verkauf.tAuftragText laden
            try
            {
                var texte = await conn.QuerySingleOrDefaultAsync<(string? CAnmerkung, string? CDrucktext, string? CHinweis, string? CVorgangsstatus)>(
                    @"SELECT cAnmerkung, cDrucktext, cHinweis, cVorgangsstatus
                      FROM Verkauf.tAuftragText
                      WHERE kAuftrag = @Id", new { Id = bestellungId });

                if (texte != default)
                {
                    if (!string.IsNullOrEmpty(texte.CAnmerkung))
                        bestellung.CAnmerkung = texte.CAnmerkung;
                    bestellung.CDrucktext = texte.CDrucktext;
                    bestellung.CHinweis = texte.CHinweis;
                    bestellung.CVorgangsstatus = texte.CVorgangsstatus;
                }
            }
            catch
            {
                // Verkauf.tAuftragText existiert moeglicherweise nicht
            }

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

        /// <summary>
        /// Speichert Verkauftexte in Verkauf.tAuftragText
        /// </summary>
        public async Task UpdateAuftragTexteAsync(int kAuftrag, string? cAnmerkung, string? cDrucktext, string? cHinweis, string? cVorgangsstatus)
        {
            var conn = await GetConnectionAsync();

            // Prüfen ob Eintrag existiert
            var exists = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Verkauf.tAuftragText WHERE kAuftrag = @kAuftrag",
                new { kAuftrag });

            if (exists > 0)
            {
                await conn.ExecuteAsync(@"
                    UPDATE Verkauf.tAuftragText SET
                        cAnmerkung = @cAnmerkung,
                        cDrucktext = @cDrucktext,
                        cHinweis = @cHinweis,
                        cVorgangsstatus = @cVorgangsstatus
                    WHERE kAuftrag = @kAuftrag",
                    new { kAuftrag, cAnmerkung, cDrucktext, cHinweis, cVorgangsstatus });
            }
            else
            {
                await conn.ExecuteAsync(@"
                    INSERT INTO Verkauf.tAuftragText (kAuftrag, cAnmerkung, cDrucktext, cHinweis, cVorgangsstatus)
                    VALUES (@kAuftrag, @cAnmerkung, @cDrucktext, @cHinweis, @cVorgangsstatus)",
                    new { kAuftrag, cAnmerkung, cDrucktext, cHinweis, cVorgangsstatus });
            }
        }

        /// <summary>
        /// Erstellt einen Lieferschein für eine Bestellung via JTL Stored Procedures
        /// </summary>
        /// <param name="kBestellung">Bestellungs-ID</param>
        /// <param name="kBenutzer">Benutzer-ID (optional, default 1)</param>
        /// <param name="hinweis">Optionaler Hinweis</param>
        /// <returns>Die neue Lieferschein-ID</returns>
        public async Task<int> CreateLieferscheinAsync(int kBestellung, int kBenutzer = 1, string? hinweis = null)
        {
            var conn = await GetConnectionAsync();

            // Bestellung laden um Auftragsnummer zu bekommen
            var bestellung = await conn.QuerySingleOrDefaultAsync<(string? CBestellNr, int KBestellung)>(
                "SELECT cBestellNr, kBestellung FROM tBestellung WHERE kBestellung = @kBestellung",
                new { kBestellung });

            if (bestellung.KBestellung == 0)
                throw new InvalidOperationException($"Bestellung {kBestellung} nicht gefunden");

            // Nächste Lieferscheinnummer generieren (Format: AuftragNr-001, -002, etc.)
            var auftragNr = bestellung.CBestellNr ?? kBestellung.ToString();
            var existingCount = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM tLieferschein WHERE kBestellung = @kBestellung",
                new { kBestellung });
            var lieferscheinNr = $"{auftragNr}-{(existingCount + 1):D3}";

            // Lieferschein via SP erstellen
            var parameters = new DynamicParameters();
            parameters.Add("@xLieferschein", null);
            parameters.Add("@kBestellung", kBestellung);
            parameters.Add("@kBenutzer", kBenutzer);
            parameters.Add("@cLieferscheinNr", lieferscheinNr);
            parameters.Add("@cHinweis", hinweis);
            parameters.Add("@dMailVersand", (DateTime?)null);
            parameters.Add("@dGedruckt", (DateTime?)null);
            parameters.Add("@nFulfillment", 0);
            parameters.Add("@kLieferantenBestellung", 0);
            parameters.Add("@kSessionId", 0);
            parameters.Add("@kLieferschein", dbType: System.Data.DbType.Int32, direction: System.Data.ParameterDirection.Output);

            await conn.ExecuteAsync("Versand.spLieferscheinErstellen", parameters, commandType: System.Data.CommandType.StoredProcedure);

            var kLieferschein = parameters.Get<int>("@kLieferschein");

            if (kLieferschein <= 0)
                throw new InvalidOperationException($"Lieferschein konnte nicht erstellt werden (kLieferschein={kLieferschein})");

            // Positionen erstellen - alle offenen Positionen der Bestellung
            var positionen = await conn.QueryAsync<(int KBestellPos, decimal FAnzahl, decimal FGeliefert)>(
                @"SELECT kBestellPos, fAnzahl, ISNULL(fGeliefert, 0) as fGeliefert
                  FROM tBestellPos
                  WHERE kBestellung = @kBestellung",
                new { kBestellung });

            foreach (var pos in positionen)
            {
                var offeneMenge = pos.FAnzahl - pos.FGeliefert;
                if (offeneMenge <= 0) continue;

                var posParams = new DynamicParameters();
                posParams.Add("@xLieferscheinPos", null);
                posParams.Add("@kLieferschein", kLieferschein);
                posParams.Add("@kBestellPos", pos.KBestellPos);
                posParams.Add("@fAnzahl", offeneMenge);
                posParams.Add("@cHinweis", (string?)null);
                posParams.Add("@nLagerbestandNichtBerechnen", 0);
                posParams.Add("@kLieferscheinPos", dbType: System.Data.DbType.Int32, direction: System.Data.ParameterDirection.Output);

                await conn.ExecuteAsync("Versand.spLieferscheinPosErstellen", posParams, commandType: System.Data.CommandType.StoredProcedure);
            }

            return kLieferschein;
        }

        /// <summary>
        /// Holt alle Lieferscheine zu einer Bestellung
        /// </summary>
        public async Task<IEnumerable<LieferscheinInfo>> GetLieferscheineAsync(int kBestellung)
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<LieferscheinInfo>(
                @"SELECT kLieferschein, kBestellung, cLieferscheinNr, dErstellt, dGedruckt, dMailVersand, cHinweis
                  FROM tLieferschein
                  WHERE kBestellung = @kBestellung
                  ORDER BY dErstellt DESC",
                new { kBestellung });
        }

        public class LieferscheinInfo
        {
            public int KLieferschein { get; set; }
            public int KBestellung { get; set; }
            public string? CLieferscheinNr { get; set; }
            public DateTime? DErstellt { get; set; }
            public DateTime? DGedruckt { get; set; }
            public DateTime? DMailVersand { get; set; }
            public string? CHinweis { get; set; }
        }

        /// <summary>
        /// Erstellt eine Rechnung für einen Auftrag via JTL Stored Procedure
        /// WICHTIG: Es muss mindestens ein Lieferschein existieren!
        /// </summary>
        public async Task<int> CreateRechnungAsync(int kAuftrag, int kBenutzer = 1)
        {
            var conn = await GetConnectionAsync();

            // Prüfen ob Lieferschein existiert
            var lieferscheine = await conn.QueryAsync<(int KLieferschein, int KLieferscheinPos, decimal FAnzahl)>(
                @"SELECT ls.kLieferschein, lsp.kLieferscheinPos, lsp.fAnzahl
                  FROM tLieferschein ls
                  JOIN tLieferscheinPos lsp ON ls.kLieferschein = lsp.kLieferschein
                  WHERE ls.kBestellung = @kAuftrag",
                new { kAuftrag });

            var lieferscheinPosList = lieferscheine.ToList();
            if (!lieferscheinPosList.Any())
                throw new InvalidOperationException("Kein Lieferschein vorhanden! Bitte zuerst Lieferschein erstellen.");

            // Rechnungsnummer generieren
            var nextNr = await conn.ExecuteScalarAsync<int>(
                "SELECT ISNULL(MAX(CAST(REPLACE(cRechnungsnr, 'IN-', '') AS INT)), 0) + 1 FROM Rechnung.tRechnung WHERE cRechnungsnr LIKE 'IN-%'");
            var rechnungsNr = $"IN-{nextNr}";

            // Auftragsdaten laden
            var auftrag = await conn.QuerySingleAsync<(int KKunde, string? CKundennr, int? KZahlungsart, int NZahlungsZiel, decimal FSkonto, int NSkontoTage)>(
                @"SELECT kKunde, cKundenNr, kZahlungsart, nZahlungsziel, ISNULL(fSkonto, 0), ISNULL(nSkontoTage, 0)
                  FROM tBestellung WHERE kBestellung = @kAuftrag",
                new { kAuftrag });

            // Kundendaten laden
            var kunde = await conn.QuerySingleOrDefaultAsync<(string? CFirma, int? KKundenGruppe, string? CKundengruppe)>(
                @"SELECT a.cFirma, k.kKundenGruppe, kg.cName
                  FROM tkunde k
                  LEFT JOIN tAdresse a ON k.kKunde = a.kKunde AND a.nStandard = 1
                  LEFT JOIN tKundenGruppe kg ON k.kKundenGruppe = kg.kKundenGruppe
                  WHERE k.kKunde = @kKunde",
                new { kKunde = auftrag.KKunde });

            // Rechnung erstellen
            var kRechnung = await conn.ExecuteScalarAsync<int>(
                @"INSERT INTO Rechnung.tRechnung
                  (kBenutzer, kKunde, cRechnungsnr, dErstellt, dValutadatum, cKundennr, cKundengruppe, kKundengruppe,
                   cFirma, nZahlungszielTage, fSkonto, nSkontoInTage, nMahnstop, nStatus, cWaehrung, fWaehrungsfaktor,
                   kZahlungsart, kSprache, nSteuereinstellung, nRechnungStatus, dLeistungsdatum)
                  OUTPUT INSERTED.kRechnung
                  VALUES
                  (@kBenutzer, @kKunde, @cRechnungsnr, GETDATE(), GETDATE(), @cKundennr, @cKundengruppe, @kKundengruppe,
                   @cFirma, @nZahlungszielTage, @fSkonto, @nSkontoInTage, 0, 0, 'EUR', 1.0,
                   @kZahlungsart, 1, 0, 0, GETDATE())",
                new
                {
                    kBenutzer,
                    kKunde = auftrag.KKunde,
                    cRechnungsnr = rechnungsNr,
                    cKundennr = auftrag.CKundennr,
                    cKundengruppe = kunde.CKundengruppe,
                    kKundengruppe = kunde.KKundenGruppe,
                    cFirma = kunde.CFirma,
                    nZahlungszielTage = auftrag.NZahlungsZiel,
                    fSkonto = auftrag.FSkonto,
                    nSkontoInTage = auftrag.NSkontoTage,
                    kZahlungsart = auftrag.KZahlungsart
                });

            // Positionen aus Lieferschein-Positionen erstellen
            var bestellPositionen = await conn.QueryAsync<(int KBestellPos, int KArtikel, string? CArtNr, string? CName, string? CEinheit, decimal FAnzahl, decimal FMwSt, decimal FVkNetto, decimal FRabatt, decimal FGewicht, decimal FEkNetto)>(
                @"SELECT bp.kBestellPos, bp.kArtikel, bp.cArtNr, bp.cName, bp.cEinheit,
                         lsp.fAnzahl, bp.fMwSt, bp.fVKNetto, ISNULL(bp.fRabatt, 0),
                         ISNULL(bp.fGewicht, 0), ISNULL(bp.fEKNetto, 0)
                  FROM tLieferschein ls
                  JOIN tLieferscheinPos lsp ON ls.kLieferschein = lsp.kLieferschein
                  JOIN tBestellPos bp ON lsp.kBestellPos = bp.kBestellPos
                  WHERE ls.kBestellung = @kAuftrag",
                new { kAuftrag });

            int nSort = 0;
            foreach (var pos in bestellPositionen)
            {
                nSort++;
                var kRechnungPosition = await conn.ExecuteScalarAsync<int>(
                    @"INSERT INTO Rechnung.tRechnungPosition
                      (kRechnung, kAuftrag, kAuftragPosition, kArtikel, cArtNr, cName, cEinheit,
                       fAnzahl, fMwSt, fVkNetto, fRabatt, nType, fGewicht, fEkNetto, nSort)
                      OUTPUT INSERTED.kRechnungPosition
                      VALUES
                      (@kRechnung, @kAuftrag, @kBestellPos, @kArtikel, @cArtNr, @cName, @cEinheit,
                       @fAnzahl, @fMwSt, @fVkNetto, @fRabatt, 1, @fGewicht, @fEkNetto, @nSort)",
                    new
                    {
                        kRechnung,
                        kAuftrag,
                        kBestellPos = pos.KBestellPos,
                        kArtikel = pos.KArtikel,
                        cArtNr = pos.CArtNr,
                        cName = pos.CName,
                        cEinheit = pos.CEinheit,
                        fAnzahl = pos.FAnzahl,
                        fMwSt = pos.FMwSt,
                        fVkNetto = pos.FVkNetto,
                        fRabatt = pos.FRabatt,
                        fGewicht = pos.FGewicht,
                        fEkNetto = pos.FEkNetto,
                        nSort
                    });

                // Link zur Lieferschein-Position erstellen
                var lieferscheinPos = lieferscheinPosList.FirstOrDefault(l => l.KLieferscheinPos > 0);
                if (lieferscheinPos.KLieferscheinPos > 0)
                {
                    await conn.ExecuteAsync(
                        @"INSERT INTO Rechnung.tRechnungLieferscheinPosition (kRechnungPosition, kLieferscheinPosition, fAnzahlAufRechnung)
                          VALUES (@kRechnungPosition, @kLieferscheinPos, @fAnzahl)",
                        new { kRechnungPosition, lieferscheinPos.KLieferscheinPos, fAnzahl = pos.FAnzahl });
                }
            }

            // Eckdaten berechnen (falls SP existiert)
            try
            {
                await conn.ExecuteAsync("Rechnung.spRechnungEckdatenBerechnen",
                    new { kRechnung },
                    commandType: System.Data.CommandType.StoredProcedure);
            }
            catch
            {
                // SP eventuell nicht vorhanden - ignorieren
            }

            return kRechnung;
        }

        /// <summary>
        /// Holt alle Rechnungen zu einem Auftrag
        /// </summary>
        public async Task<IEnumerable<RechnungInfo>> GetRechnungenAsync(int kAuftrag)
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<RechnungInfo>(
                @"SELECT DISTINCT r.kRechnung, r.cRechnungsnr, r.dErstellt, r.nRechnungStatus,
                         r.kKunde, r.cKundennr, r.cFirma
                  FROM Rechnung.tRechnung r
                  JOIN Rechnung.tRechnungPosition rp ON r.kRechnung = rp.kRechnung
                  WHERE rp.kAuftrag = @kAuftrag
                  ORDER BY r.dErstellt DESC",
                new { kAuftrag });
        }

        public class RechnungInfo
        {
            public int KRechnung { get; set; }
            public string? CRechnungsnr { get; set; }
            public DateTime? DErstellt { get; set; }
            public byte NRechnungStatus { get; set; }
            public int KKunde { get; set; }
            public string? CKundennr { get; set; }
            public string? CFirma { get; set; }
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

        /// <summary>
        /// Lädt eigene Felder (Attribute) für einen Auftrag
        /// JTL native: tAttribut, tAttributSprache, Verkauf.tAuftragAttribut, Verkauf.tAuftragAttributSprache
        /// </summary>
        public async Task<List<AuftragEigenesFeld>> GetAuftragEigeneFelderAsync(int kAuftrag)
        {
            var conn = await GetConnectionAsync();
            var result = new List<AuftragEigenesFeld>();

            try
            {
                // JTL native Tabellen - Verkauf.tAuftragAttribut für Auftrags-Attribute
                var felder = await conn.QueryAsync<AuftragEigenesFeld>(
                    @"SELECT DISTINCT
                         a.kAttribut,
                         aS.cName AS FeldName,
                         vS.cWertVarchar,
                         vS.fWertDecimal,
                         vS.nWertInt,
                         COALESCE(
                             vS.cWertVarchar,
                             CASE WHEN vS.fWertDecimal IS NOT NULL THEN CAST(vS.fWertDecimal AS NVARCHAR(50)) END,
                             CASE WHEN vS.nWertInt IS NOT NULL THEN CAST(vS.nWertInt AS NVARCHAR(50)) END
                         ) AS Wert
                      FROM dbo.tAttribut a
                      INNER JOIN dbo.tAttributSprache aS ON a.kAttribut = aS.kAttribut
                      INNER JOIN Verkauf.tAuftragAttribut v ON a.kAttribut = v.kAttribut
                      INNER JOIN Verkauf.tAuftragAttributSprache vS ON v.kAuftragAttribut = vS.kAuftragAttribut
                      WHERE v.kAuftrag = @kAuftrag
                        AND aS.kSprache IN (0, 1)
                        AND vS.kSprache IN (0, 1)
                      ORDER BY aS.cName",
                    new { kAuftrag });

                result = felder.ToList();
                _log.Debug("GetAuftragEigeneFelderAsync: kAuftrag={kAuftrag}, gefunden={Anzahl} Felder", kAuftrag, result.Count);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Fehler beim Laden der Auftrags-Eigenfelder für {kAuftrag}", kAuftrag);
            }

            return result;
        }

        /// <summary>
        /// DTO für Auftrags-Eigene Felder
        /// </summary>
        public class AuftragEigenesFeld
        {
            public int KAttribut { get; set; }
            public string FeldName { get; set; } = "";
            public string? CWertVarchar { get; set; }
            public decimal? FWertDecimal { get; set; }
            public int? NWertInt { get; set; }
            public string? Wert { get; set; }
        }

        /// <summary>
        /// Speichert ein eigenes Feld für einen Auftrag
        /// Verwendet NOVVIA.spAuftragEigenesFeldCreateOrUpdate (JTL native Tabellen)
        /// </summary>
        public async Task SetAuftragEigenesFeldAsync(int kAuftrag, string feldName, string? wert)
        {
            var conn = await GetConnectionAsync();

            _log.Debug("SetAuftragEigenesFeldAsync: kAuftrag={kAuftrag}, Feld={Feld}, Wert={Wert}", kAuftrag, feldName, wert);

            try
            {
                // NOVVIA TVP verwenden - schreibt in JTL native Tabellen
                var dt = new System.Data.DataTable();
                dt.Columns.Add("kAuftrag", typeof(int));
                dt.Columns.Add("cKey", typeof(string));
                dt.Columns.Add("cValue", typeof(string));
                dt.Rows.Add(kAuftrag, feldName, wert ?? "");

                var p = new DynamicParameters();
                p.Add("@AuftragEigenesFeldAnpassen", dt.AsTableValuedParameter("NOVVIA.TYPE_AuftragEigenesFeldAnpassen"));

                await conn.ExecuteAsync("NOVVIA.spAuftragEigenesFeldCreateOrUpdate", p, commandType: CommandType.StoredProcedure);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Fehler beim Speichern des Auftrags-Eigenfelds {feldName} für {kAuftrag}", feldName, kAuftrag);
                throw;
            }
        }

        /// <summary>
        /// Speichert mehrere eigene Felder für einen Auftrag (JTL native Tabellen)
        /// </summary>
        public async Task SetAuftragEigeneFelderAsync(int kAuftrag, Dictionary<string, string?> felder)
        {
            if (felder == null || felder.Count == 0) return;

            var conn = await GetConnectionAsync();

            _log.Debug("SetAuftragEigeneFelderAsync: kAuftrag={kAuftrag}, {Anzahl} Felder", kAuftrag, felder.Count);

            try
            {
                // JTL TVP mit allen Feldern auf einmal
                var dt = new System.Data.DataTable();
                dt.Columns.Add("kAuftrag", typeof(int));
                dt.Columns.Add("cKey", typeof(string));
                dt.Columns.Add("cValue", typeof(string));

                foreach (var kvp in felder)
                {
                    dt.Rows.Add(kAuftrag, kvp.Key, kvp.Value ?? "");
                }

                var p = new DynamicParameters();
                p.Add("@AuftragEigenesFeldAnpassen", dt.AsTableValuedParameter("NOVVIA.TYPE_AuftragEigenesFeldAnpassen"));

                await conn.ExecuteAsync("NOVVIA.spAuftragEigenesFeldCreateOrUpdate", p, commandType: CommandType.StoredProcedure);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Bulk-Speichern fehlgeschlagen für Auftrag {kAuftrag}, verwende Fallback", kAuftrag);

                // Fallback: Einzelne Felder speichern
                foreach (var kvp in felder)
                {
                    await SetAuftragEigenesFeldAsync(kAuftrag, kvp.Key, kvp.Value);
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
        /// Lieferanten laden, die für Artikel im Auftrag hinterlegt sind (tLieferantenArtikel)
        /// </summary>
        public async Task<IEnumerable<LieferantRef>> GetLieferantenForBestellungAsync(int kBestellung)
        {
            var conn = await GetConnectionAsync();
            const string sql = @"
                SELECT l.kLieferant AS KLieferant, l.cFirma AS CFirma, l.cStrasse AS CStrasse,
                       l.cPLZ AS CPLZ, l.cOrt AS COrt, l.cLand AS CLand,
                       COALESCE(l.cTelZentralle, l.cTelDurchwahl, '') AS CTelefon,
                       l.cFax AS CFax, l.cEMail AS CEmail,
                       COUNT(DISTINCT bp.tArtikel_kArtikel) AS ArtikelAnzahl
                FROM dbo.tLieferant l
                JOIN dbo.tLieferantenArtikel la ON l.kLieferant = la.tLieferant_kLieferant
                JOIN dbo.tBestellPos bp ON bp.tArtikel_kArtikel = la.tArtikel_kArtikel
                WHERE bp.tBestellung_kBestellung = @kBestellung
                  AND l.cAktiv = 'Y'
                GROUP BY l.kLieferant, l.cFirma, l.cStrasse, l.cPLZ, l.cOrt, l.cLand,
                         l.cTelZentralle, l.cTelDurchwahl, l.cFax, l.cEMail
                ORDER BY l.cFirma";
            return await conn.QueryAsync<LieferantRef>(sql, new { kBestellung });
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
                ORDER BY cName";
            return await conn.QueryAsync<WarenlagerRef>(sql);
        }

        /// <summary>
        /// Lagerplätze für ein Warenlager laden
        /// </summary>
        public async Task<IEnumerable<WarenlagerPlatzRef>> GetWarenlagerPlaetzeAsync(int? kWarenLager = null)
        {
            var conn = await GetConnectionAsync();
            var sql = @"
                SELECT kWarenlagerPlatz AS KWarenlagerPlatz, kWarenLager AS KWarenLager,
                       cPlatz AS CPlatz, ISNULL(cRegal, '') AS CRegal, ISNULL(cFach, '') AS CFach
                FROM dbo.tWarenlagerPlatz
                WHERE (@kWarenLager IS NULL OR kWarenLager = @kWarenLager)
                ORDER BY cRegal, cFach, cPlatz";
            return await conn.QueryAsync<WarenlagerPlatzRef>(sql, new { kWarenLager });
        }

        /// <summary>
        /// Offene Lieferantenbestellungen laden (für Wareneingang-Referenz)
        /// </summary>
        public async Task<IEnumerable<OffeneLieferantenBestellung>> GetOffeneLieferantenBestellungenAsync()
        {
            var conn = await GetConnectionAsync();
            const string sql = @"
                SELECT lb.kLieferantenBestellung AS KLieferantenBestellung,
                       ISNULL(lb.cEigeneBestellnummer, CAST(lb.kLieferantenBestellung AS VARCHAR)) AS CEigeneBestellnummer,
                       ISNULL(l.cName, 'Unbekannt') AS CLieferantName,
                       lb.dErstellt AS DErstellt,
                       (SELECT COUNT(*) FROM dbo.tLieferantenBestellungPos p WHERE p.kLieferantenBestellung = lb.kLieferantenBestellung) AS AnzahlPositionen,
                       (SELECT COUNT(*) FROM dbo.tLieferantenBestellungPos p WHERE p.kLieferantenBestellung = lb.kLieferantenBestellung AND p.fMenge > ISNULL(p.fMengeGeliefert, 0)) AS OffenePositionen
                FROM dbo.tLieferantenBestellung lb
                LEFT JOIN dbo.tLieferant l ON lb.kLieferant = l.kLieferant
                WHERE lb.nStatus < 4
                ORDER BY lb.dErstellt DESC";
            return await conn.QueryAsync<OffeneLieferantenBestellung>(sql);
        }

        /// <summary>
        /// Letzte Wareneingänge laden (Historie)
        /// </summary>
        public async Task<IEnumerable<WareneingangHistorie>> GetWareneingaengeAsync(int limit = 50)
        {
            var conn = await GetConnectionAsync();
            var sql = $@"
                SELECT TOP {limit} we.kWarenlagerEingang AS KWarenlagerEingang,
                       we.kArtikel AS KArtikel,
                       a.cArtNr AS CArtNr,
                       ISNULL(ab.cName, a.cArtNr) AS CArtikelName,
                       we.fAnzahl AS FAnzahl,
                       ISNULL(we.cChargenNr, '') AS CChargenNr,
                       we.dMHD AS DMHD,
                       we.dErstellt AS DErstellt,
                       ISNULL(we.cKommentar, '') AS CKommentar,
                       ISNULL(wl.cName, '') AS CLagerName
                FROM dbo.tWarenlagerEingang we
                LEFT JOIN dbo.tArtikel a ON we.kArtikel = a.kArtikel
                LEFT JOIN dbo.tArtikelBeschreibung ab ON a.kArtikel = ab.kArtikel AND ab.kSprache = 1
                LEFT JOIN dbo.tWarenlagerPlatz wp ON we.kWarenlagerPlatz = wp.kWarenlagerPlatz
                LEFT JOIN dbo.tWarenLager wl ON wp.kWarenLager = wl.kWarenLager
                ORDER BY we.dErstellt DESC";
            return await conn.QueryAsync<WareneingangHistorie>(sql);
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
                       kLieferantenBestellungLA AS KLieferantenBestellungLA,
                       cWaehrungISO AS CWaehrungISO, cInternerKommentar AS CInternerKommentar,
                       cDruckAnmerkung AS CDruckAnmerkung, nStatus AS NStatus,
                       dErstellt AS DErstellt, kFirma AS KFirma, kLager AS KLager,
                       kKunde AS KKunde, dLieferdatum AS DLieferdatum,
                       cEigeneBestellnummer AS CEigeneBestellnummer,
                       cBezugsAuftragsNummer AS CBezugsAuftragsNummer,
                       nDropShipping AS NDropShipping, cFremdbelegnummer AS CFremdbelegnummer
                FROM dbo.tLieferantenBestellung
                WHERE kLieferantenBestellung = @id";
            var bestellung = await conn.QueryFirstOrDefaultAsync<LieferantenBestellungDto>(sql, new { id = kLieferantenBestellung });

            // Lieferadresse laden wenn vorhanden
            if (bestellung != null && bestellung.KLieferantenBestellungLA > 0)
            {
                bestellung.Lieferadresse = await GetLieferantenBestellungAdresseAsync(bestellung.KLieferantenBestellungLA);
                bestellung.LieferadresseGleichRechnungsadresse = false;
            }
            else if (bestellung != null)
            {
                bestellung.LieferadresseGleichRechnungsadresse = true;
            }

            return bestellung;
        }

        /// <summary>
        /// Lieferadresse einer Lieferantenbestellung laden
        /// </summary>
        private async Task<LieferantenBestellungAdresse?> GetLieferantenBestellungAdresseAsync(int kLieferantenBestellungLA)
        {
            var conn = await GetConnectionAsync();
            const string sql = @"
                SELECT kLieferantenBestellungLA AS KLieferantenBestellungLA,
                       ISNULL(cKundennummer, '') AS CKundennummer,
                       ISNULL(cFirma, '') AS CFirma,
                       ISNULL(cFirmenZusatz, '') AS CFirmenZusatz,
                       ISNULL(cAnrede, '') AS CAnrede,
                       ISNULL(cTitel, '') AS CTitel,
                       ISNULL(cVorname, '') AS CVorname,
                       ISNULL(cNachname, '') AS CNachname,
                       ISNULL(cStrasse, '') AS CStrasse,
                       ISNULL(cAdresszusatz, '') AS CAdresszusatz,
                       ISNULL(cPLZ, '') AS CPLZ,
                       ISNULL(cOrt, '') AS COrt,
                       ISNULL(cBundesland, '') AS CBundesland,
                       ISNULL(cLandISO, 'DE') AS CLandISO,
                       ISNULL(cTel, '') AS CTel,
                       ISNULL(cFax, '') AS CFax,
                       ISNULL(cMobil, '') AS CMobil,
                       ISNULL(cMail, '') AS CMail
                FROM dbo.tLieferantenBestellungLA
                WHERE kLieferantenBestellungLA = @id";
            return await conn.QueryFirstOrDefaultAsync<LieferantenBestellungAdresse>(sql, new { id = kLieferantenBestellungLA });
        }

        /// <summary>
        /// Lieferadresse einer Lieferantenbestellung erstellen oder aktualisieren
        /// </summary>
        private async Task<int> SaveLieferantenBestellungAdresseAsync(LieferantenBestellungAdresse adresse)
        {
            var conn = await GetConnectionAsync();

            if (adresse.KLieferantenBestellungLA > 0)
            {
                // Update existing
                const string updateSql = @"
                    UPDATE dbo.tLieferantenBestellungLA SET
                        cKundennummer = @CKundennummer,
                        cFirma = @CFirma,
                        cFirmenZusatz = @CFirmenZusatz,
                        cAnrede = @CAnrede,
                        cTitel = @CTitel,
                        cVorname = @CVorname,
                        cNachname = @CNachname,
                        cStrasse = @CStrasse,
                        cAdresszusatz = @CAdresszusatz,
                        cPLZ = @CPLZ,
                        cOrt = @COrt,
                        cBundesland = @CBundesland,
                        cLandISO = @CLandISO,
                        cTel = @CTel,
                        cFax = @CFax,
                        cMobil = @CMobil,
                        cMail = @CMail
                    WHERE kLieferantenBestellungLA = @KLieferantenBestellungLA";
                await conn.ExecuteAsync(updateSql, adresse);
                return adresse.KLieferantenBestellungLA;
            }
            else
            {
                // Insert new
                const string insertSql = @"
                    INSERT INTO dbo.tLieferantenBestellungLA (
                        cKundennummer, cFirma, cFirmenZusatz, cAnrede, cTitel, cVorname, cNachname,
                        cStrasse, cAdresszusatz, cPLZ, cOrt, cBundesland, cLandISO,
                        cTel, cFax, cMobil, cMail
                    ) VALUES (
                        @CKundennummer, @CFirma, @CFirmenZusatz, @CAnrede, @CTitel, @CVorname, @CNachname,
                        @CStrasse, @CAdresszusatz, @CPLZ, @COrt, @CBundesland, @CLandISO,
                        @CTel, @CFax, @CMobil, @CMail
                    );
                    SELECT SCOPE_IDENTITY();";
                return await conn.QuerySingleAsync<int>(insertSql, adresse);
            }
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

            // Lieferadresse speichern wenn abweichend
            int kLieferantenBestellungLA = 0;
            if (!bestellung.LieferadresseGleichRechnungsadresse && bestellung.Lieferadresse != null)
            {
                kLieferantenBestellungLA = await SaveLieferantenBestellungAdresseAsync(bestellung.Lieferadresse);
            }

            // XML fuer Positionen erstellen
            var posXml = BuildPositionenXml(bestellung.Positionen);

            var p = new DynamicParameters();
            p.Add("@kLieferant", bestellung.KLieferant);
            p.Add("@kSprache", bestellung.KSprache);
            p.Add("@kLieferantenBestellungRA", 0);
            p.Add("@kLieferantenBestellungLA", kLieferantenBestellungLA);
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

            // Lieferadresse speichern/aktualisieren wenn abweichend
            int kLieferantenBestellungLA = 0;
            if (!bestellung.LieferadresseGleichRechnungsadresse && bestellung.Lieferadresse != null)
            {
                // Bestehende Adress-ID verwenden falls vorhanden
                if (bestellung.KLieferantenBestellungLA > 0)
                {
                    bestellung.Lieferadresse.KLieferantenBestellungLA = bestellung.KLieferantenBestellungLA;
                }
                kLieferantenBestellungLA = await SaveLieferantenBestellungAdresseAsync(bestellung.Lieferadresse);
            }

            var p = new DynamicParameters();
            p.Add("@kLieferantenBestellung", bestellung.KLieferantenBestellung);
            p.Add("@kLieferant", bestellung.KLieferant);
            p.Add("@kSprache", bestellung.KSprache);
            p.Add("@kLieferantenBestellungRA", 0);
            p.Add("@kLieferantenBestellungLA", kLieferantenBestellungLA);
            p.Add("@cWaehrungISO", bestellung.CWaehrungISO ?? "EUR");
            p.Add("@cInternerKommentar", bestellung.CInternerKommentar ?? "");
            p.Add("@cDruckAnmerkung", bestellung.CDruckAnmerkung ?? "");
            p.Add("@dGedruckt", (DateTime?)null);
            p.Add("@dGemailt", (DateTime?)null);
            p.Add("@dGefaxt", (DateTime?)null);
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
            p.Add("@dAngemahnt", (DateTime?)null);
            p.Add("@dInBearbeitung", (DateTime?)null);
            p.Add("@nDeleted", 0);
            p.Add("@nManuellAbgeschlossen", 0);
            p.Add("@cFremdbelegnummer", bestellung.CFremdbelegnummer ?? "");
            p.Add("@kLieferschein", 0);
            p.Add("@nBestaetigt", 0);
            p.Add("@dExportiert", (DateTime?)null);

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
        /// Lieferantenbestellung aus Kundenauftrag erstellen
        /// </summary>
        public async Task<int> CreateLieferantenbestellungFromAuftragAsync(int kBestellung, int kLieferant)
        {
            var conn = await GetConnectionAsync();

            // Auftrag mit Positionen laden
            var auftrag = await GetBestellungByIdAsync(kBestellung);
            if (auftrag == null)
                throw new Exception("Auftrag nicht gefunden");

            var positionen = auftrag.Positionen;
            if (!positionen.Any())
                throw new Exception("Keine Positionen im Auftrag gefunden");

            // Lieferanten-Artikeldaten laden (EK-Preise)
            var liefPositionen = new List<LieferantenBestellungPosition>();
            int sort = 1;

            foreach (var pos in positionen)
            {
                if (pos.TArtikel_KArtikel == null || pos.TArtikel_KArtikel <= 0) continue;

                // EK-Preis vom Lieferanten holen
                var ekInfo = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
                    SELECT la.fEKNetto, la.cArtNr AS cLieferantenArtNr, la.cBezeichnung AS cLieferantenBezeichnung,
                           la.nLiefertage
                    FROM tLieferantenArtikel la
                    WHERE la.tArtikel_kArtikel = @kArtikel AND la.tLieferant_kLieferant = @kLieferant",
                    new { kArtikel = pos.TArtikel_KArtikel, kLieferant });

                var liefPos = new LieferantenBestellungPosition
                {
                    KArtikel = pos.TArtikel_KArtikel.Value,
                    CArtNr = pos.CArtNr ?? "",
                    CName = pos.CName ?? "",
                    FMenge = pos.FAnzahl,
                    FEKNetto = ekInfo?.fEKNetto ?? 0,
                    CLieferantenArtNr = ekInfo?.cLieferantenArtNr ?? "",
                    CLieferantenBezeichnung = ekInfo?.cLieferantenBezeichnung ?? pos.CName ?? "",
                    NLiefertage = ekInfo?.nLiefertage ?? 0,
                    NPosTyp = 1,
                    NSort = sort++
                };
                liefPositionen.Add(liefPos);
            }

            if (!liefPositionen.Any())
                throw new Exception("Keine Artikel konnten dem Lieferanten zugeordnet werden");

            // Lieferantenbestellung erstellen
            var bestellung = new LieferantenBestellungDto
            {
                KLieferant = kLieferant,
                KSprache = 1,
                CWaehrungISO = "EUR",
                CInternerKommentar = $"Erstellt aus Auftrag {kBestellung}",
                NStatus = 5, // Entwurf
                DErstellt = DateTime.Now,
                KFirma = 1,
                KLager = 0,
                Positionen = liefPositionen,
                LieferadresseGleichRechnungsadresse = true
            };

            return await CreateLieferantenBestellungAsync(bestellung);
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
                    p.Add("@nStatus", (int?)null);
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
            public int ArtikelAnzahl { get; set; }
        }

        public class WarenlagerRef
        {
            public int KWarenLager { get; set; }
            public string CName { get; set; } = "";
        }

        public class WarenlagerPlatzRef
        {
            public int KWarenlagerPlatz { get; set; }
            public int KWarenLager { get; set; }
            public string CPlatz { get; set; } = "";
            public string CRegal { get; set; } = "";
            public string CFach { get; set; } = "";
            public string AnzeigeName => !string.IsNullOrEmpty(CRegal)
                ? $"{CRegal}/{CFach}/{CPlatz}"
                : CPlatz;
        }

        public class WareneingangHistorie
        {
            public int KWarenlagerEingang { get; set; }
            public int KArtikel { get; set; }
            public string CArtNr { get; set; } = "";
            public string CArtikelName { get; set; } = "";
            public decimal FAnzahl { get; set; }
            public string CChargenNr { get; set; } = "";
            public DateTime? DMHD { get; set; }
            public DateTime DErstellt { get; set; }
            public string CKommentar { get; set; } = "";
            public string CLagerName { get; set; } = "";
        }

        public class OffeneLieferantenBestellung
        {
            public int KLieferantenBestellung { get; set; }
            public string CEigeneBestellnummer { get; set; } = "";
            public string CLieferantName { get; set; } = "";
            public DateTime DErstellt { get; set; }
            public int AnzahlPositionen { get; set; }
            public int OffenePositionen { get; set; }
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
            public int KLieferantenBestellungLA { get; set; }
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

            // Lieferadresse - wenn null oder LieferadresseGleichRechnungsadresse=true wird Firmenadresse verwendet
            public bool LieferadresseGleichRechnungsadresse { get; set; } = true;
            public LieferantenBestellungAdresse? Lieferadresse { get; set; }
        }

        public class LieferantenBestellungAdresse
        {
            public int KLieferantenBestellungLA { get; set; }
            public string CKundennummer { get; set; } = "";
            public string CFirma { get; set; } = "";
            public string CFirmenZusatz { get; set; } = "";
            public string CAnrede { get; set; } = "";
            public string CTitel { get; set; } = "";
            public string CVorname { get; set; } = "";
            public string CNachname { get; set; } = "";
            public string CStrasse { get; set; } = "";
            public string CAdresszusatz { get; set; } = "";
            public string CPLZ { get; set; } = "";
            public string COrt { get; set; } = "";
            public string CBundesland { get; set; } = "";
            public string CLandISO { get; set; } = "DE";
            public string CTel { get; set; } = "";
            public string CFax { get; set; } = "";
            public string CMobil { get; set; } = "";
            public string CMail { get; set; } = "";
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

        #region Auftragsstapelimport

        /// <summary>
        /// Artikel nach Artikelnummer suchen
        /// </summary>
        public async Task<ArtikelRef?> GetArtikelByArtNrAsync(string artNr)
        {
            var conn = await GetConnectionAsync();
            const string sql = @"
                SELECT TOP 1 a.kArtikel AS KArtikel, a.cArtNr AS CArtNr,
                       ab.cName AS CName, a.fVKNetto AS FVKNetto, a.fMwSt AS FMwSt
                FROM dbo.tArtikel a
                LEFT JOIN dbo.tArtikelBeschreibung ab ON a.kArtikel = ab.kArtikel AND ab.kSprache = 1
                WHERE a.cArtNr = @artNr OR a.cBarcode = @artNr";
            return await conn.QueryFirstOrDefaultAsync<ArtikelRef>(sql, new { artNr });
        }

        /// <summary>
        /// Auftrag aus Import-Daten erstellen
        /// </summary>
        public async Task<int> CreateAuftragFromImportAsync(string kundenNr, List<AuftragImportPosition> positionen, string zusatztext, bool ueberPositionen, DateTime? mindestMHD = null)
        {
            var conn = await GetConnectionAsync();

            // Kunde suchen
            var kunde = await conn.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT kKunde, cFirma, cVorname, cName FROM dbo.tKunde WHERE cKundenNr = @nr OR CAST(kKunde AS VARCHAR) = @nr",
                new { nr = kundenNr });

            if (kunde == null)
            {
                throw new Exception($"Kunde mit Nr. {kundenNr} nicht gefunden");
            }

            // Bestellnummer generieren
            var maxNr = await conn.QueryFirstOrDefaultAsync<int?>(
                "SELECT MAX(CAST(cBestellNr AS INT)) FROM dbo.tBestellung WHERE ISNUMERIC(cBestellNr) = 1");
            var neueBestellNr = ((maxNr ?? 0) + 1).ToString();

            // Anmerkung mit MHD ergänzen
            var anmerkung = zusatztext;
            if (mindestMHD.HasValue)
            {
                var mhdText = $"[Mindest-MHD: {mindestMHD.Value:dd.MM.yyyy}]";
                anmerkung = string.IsNullOrEmpty(anmerkung) ? mhdText : $"{mhdText}\n{anmerkung}";
            }

            // Bestellung anlegen
            var bestellungId = await conn.QuerySingleAsync<int>(@"
                INSERT INTO dbo.tBestellung (
                    cBestellNr, kKunde, dErstellt, cStatus, cWaehrung,
                    cAnmerkung, fGesamtNetto, fGesamtBrutto
                ) VALUES (
                    @BestellNr, @KundeId, GETDATE(), 'Offen', 'EUR',
                    @Anmerkung, 0, 0
                );
                SELECT SCOPE_IDENTITY();",
                new {
                    BestellNr = neueBestellNr,
                    KundeId = (int)kunde.kKunde,
                    Anmerkung = anmerkung
                });

            decimal gesamtNetto = 0;
            decimal gesamtBrutto = 0;
            int posNr = 1;

            // Positionen anlegen
            foreach (var pos in positionen)
            {
                var artikel = await GetArtikelByArtNrAsync(pos.ArtNr);
                if (artikel == null) continue;

                var preis = pos.Preis > 0 ? pos.Preis : artikel.FVKNetto;
                var mwst = artikel.FMwSt;
                var netto = preis * pos.Menge;
                var brutto = netto * (1 + mwst / 100);

                await conn.ExecuteAsync(@"
                    INSERT INTO dbo.tBestellPos (
                        kBestellung, kArtikel, cArtNr, cName,
                        fAnzahl, fVKNetto, fMwSt, nPosNr
                    ) VALUES (
                        @BestellungId, @ArtikelId, @ArtNr, @Name,
                        @Menge, @Preis, @MwSt, @PosNr
                    )",
                    new {
                        BestellungId = bestellungId,
                        ArtikelId = artikel.KArtikel,
                        ArtNr = artikel.CArtNr,
                        Name = artikel.CName,
                        Menge = pos.Menge,
                        Preis = preis,
                        MwSt = mwst,
                        PosNr = posNr++
                    });

                gesamtNetto += netto;
                gesamtBrutto += brutto;
            }

            // Summen aktualisieren
            await conn.ExecuteAsync(@"
                UPDATE dbo.tBestellung
                SET fGesamtNetto = @Netto, fGesamtBrutto = @Brutto
                WHERE kBestellung = @Id",
                new { Netto = gesamtNetto, Brutto = gesamtBrutto, Id = bestellungId });

            return bestellungId;
        }

        /// <summary>
        /// Auftrag buchen/freigeben
        /// </summary>
        public async Task AuftragBuchenAsync(int bestellungId)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync(@"
                UPDATE dbo.tBestellung
                SET cStatus = 'In Bearbeitung', dGeaendert = GETDATE()
                WHERE kBestellung = @Id",
                new { Id = bestellungId });
        }

        public class ArtikelRef
        {
            public int KArtikel { get; set; }
            public string CArtNr { get; set; } = "";
            public string CName { get; set; } = "";
            public decimal FVKNetto { get; set; }
            public decimal FMwSt { get; set; }
        }

        public class AuftragImportPosition
        {
            public string AdressNr { get; set; } = "";
            public string ArtNr { get; set; } = "";
            public decimal Menge { get; set; }
            public decimal Preis { get; set; }
            public DateTime? MindestMHD { get; set; }
        }

        #endregion

        #region Lagerbuchungen (JTL-konform via Stored Procedures)

        /// <summary>
        /// Baut XML-Payload für JTL spWarenlagerEingangSchreiben
        /// </summary>
        private static string BuildWareneingangXml(
            int artikelId,
            int lagerPlatzId,
            decimal menge,
            string? chargenNr = null,
            DateTime? mhd = null)
        {
            var chargenTag = !string.IsNullOrEmpty(chargenNr)
                ? $"<cChargenNr>{chargenNr}</cChargenNr>" : "";
            var mhdTag = mhd.HasValue
                ? $"<dMHD>{mhd.Value:yyyy-MM-dd}</dMHD>" : "";

            return $@"<WarenlagerEingaenge>
  <Eingang>
    <kArtikel>{artikelId}</kArtikel>
    <kWarenlagerPlatz>{lagerPlatzId}</kWarenlagerPlatz>
    <fAnzahl>{menge.ToString(System.Globalization.CultureInfo.InvariantCulture)}</fAnzahl>
    {chargenTag}
    {mhdTag}
  </Eingang>
</WarenlagerEingaenge>";
        }

        /// <summary>
        /// Baut XML-Payload für JTL spWarenlagerAusgangSchreiben
        /// </summary>
        private static string BuildWarenausgangXml(decimal menge, int? warenlagerEingangId = null)
        {
            var eingangTag = warenlagerEingangId.HasValue
                ? $"<kWarenlagerEingang>{warenlagerEingangId.Value}</kWarenlagerEingang>" : "";

            return $@"<WarenlagerAusgaenge>
  <Ausgang>
    <fAnzahl>{menge.ToString(System.Globalization.CultureInfo.InvariantCulture)}</fAnzahl>
    {eingangTag}
  </Ausgang>
</WarenlagerAusgaenge>";
        }

        /// <summary>
        /// Wareneingang über JTL Stored Procedure buchen (100% JTL-konform)
        /// Nutzt dbo.spWarenlagerEingangSchreiben mit XML-Payload
        /// </summary>
        public async Task BucheWareneingangAsync(
            int artikelId,
            int lagerPlatzId,
            decimal menge,
            int benutzerId,
            string? kommentar = null,
            string? lieferscheinNr = null,
            string? chargenNr = null,
            DateTime? mhd = null,
            decimal? ekEinzel = null,
            int? lieferantenBestellungPosId = null)
        {
            var conn = await GetConnectionAsync();

            var xml = BuildWareneingangXml(artikelId, lagerPlatzId, menge, chargenNr, mhd);

            _log.Information("Wareneingang: Artikel={ArtikelId}, Platz={PlatzId}, Menge={Menge}, XML={Xml}",
                artikelId, lagerPlatzId, menge, xml);

            var parameters = new DynamicParameters();
            parameters.Add("@xWarenlagerEingaenge", xml, DbType.Xml);
            parameters.Add("@kArtikel", artikelId);
            parameters.Add("@kWarenlagerPlatz", lagerPlatzId);
            parameters.Add("@kBenutzer", benutzerId);
            parameters.Add("@fAnzahl", menge);
            parameters.Add("@fEKEinzel", ekEinzel);
            parameters.Add("@cLieferscheinNr", lieferscheinNr);
            parameters.Add("@cChargenNr", chargenNr);
            parameters.Add("@dMHD", mhd);
            parameters.Add("@cKommentar", kommentar);
            parameters.Add("@kLieferantenBestellungPos", lieferantenBestellungPosId);
            parameters.Add("@dGeliefertAm", DateTime.Now);

            await conn.ExecuteAsync(
                "dbo.spWarenlagerEingangSchreiben",
                parameters,
                commandType: CommandType.StoredProcedure);

            _log.Information("Wareneingang erfolgreich gebucht: Artikel={ArtikelId}, Menge={Menge}", artikelId, menge);
        }

        /// <summary>
        /// Warenausgang über JTL Stored Procedure buchen (100% JTL-konform)
        /// Nutzt dbo.spWarenlagerAusgangSchreiben mit XML-Payload
        /// </summary>
        /// <returns>kWarenlagerAusgang (OUTPUT Parameter)</returns>
        public async Task<int?> BucheWarenausgangAsync(
            decimal menge,
            int benutzerId,
            int buchungsart,
            string? kommentar = null,
            int? warenlagerEingangId = null,
            int? lieferscheinPosId = null)
        {
            var conn = await GetConnectionAsync();

            var xml = BuildWarenausgangXml(menge, warenlagerEingangId);

            _log.Information("Warenausgang: Menge={Menge}, Buchungsart={Buchungsart}, XML={Xml}",
                menge, buchungsart, xml);

            var parameters = new DynamicParameters();
            parameters.Add("@xWarenlagerAusgaenge", xml, DbType.Xml);
            parameters.Add("@kWarenlagerEingang", warenlagerEingangId);
            parameters.Add("@kLieferscheinPos", lieferscheinPosId);
            parameters.Add("@fAnzahl", menge);
            parameters.Add("@cKommentar", kommentar);
            parameters.Add("@kBenutzer", benutzerId);
            parameters.Add("@kBuchungsart", buchungsart);
            parameters.Add("@nHistorieNichtSchreiben", 0);
            parameters.Add("@kWarenlagerAusgang", dbType: DbType.Int32, direction: ParameterDirection.Output);

            await conn.ExecuteAsync(
                "dbo.spWarenlagerAusgangSchreiben",
                parameters,
                commandType: CommandType.StoredProcedure);

            var ausgangId = parameters.Get<int?>("@kWarenlagerAusgang");
            _log.Information("Warenausgang erfolgreich gebucht: kWarenlagerAusgang={AusgangId}, Menge={Menge}", ausgangId, menge);

            return ausgangId;
        }

        /// <summary>
        /// Standard-Buchungsarten für Warenausgang
        /// </summary>
        public static class Buchungsart
        {
            public const int Verkauf = 1;
            public const int Inventur = 2;
            public const int Schwund = 3;
            public const int Retoure = 4;
            public const int Umlagerung = 5;
            public const int Korrektur = 6;
        }

        #endregion

        #region JTL Auftrags-Eckdaten (via SP)

        /// <summary>
        /// Berechnet die Eckdaten (Summen, Status, etc.) für einen Auftrag neu.
        /// MUSS nach jeder Auftragsänderung aufgerufen werden!
        /// </summary>
        public async Task<bool> BerechneAuftragEckdatenAsync(int kAuftrag)
        {
            try
            {
                var client = new Infrastructure.Jtl.JtlOrderClient(_connectionString);
                var result = await client.BerechneAuftragEckdatenAsync(kAuftrag);
                if (!result.Success)
                {
                    _log.Warning("Eckdaten-Berechnung fehlgeschlagen für Auftrag {KAuftrag}: {Message}",
                        kAuftrag, result.Message);
                }
                return result.Success;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Fehler bei Eckdaten-Berechnung für Auftrag {KAuftrag}", kAuftrag);
                return false;
            }
        }

        /// <summary>
        /// Berechnet die Eckdaten für mehrere Aufträge
        /// </summary>
        public async Task<bool> BerechneAuftragEckdatenAsync(IEnumerable<int> auftragIds)
        {
            try
            {
                var client = new Infrastructure.Jtl.JtlOrderClient(_connectionString);
                var result = await client.BerechneAuftragEckdatenAsync(auftragIds);
                return result.Success;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Fehler bei Eckdaten-Berechnung für mehrere Aufträge");
                return false;
            }
        }

        /// <summary>
        /// Holt die berechneten Eckdaten eines Auftrags (aus tAuftragEckdaten)
        /// </summary>
        public async Task<AuftragEckdatenDto?> GetAuftragEckdatenAsync(int kAuftrag)
        {
            try
            {
                var client = new Infrastructure.Jtl.JtlOrderClient(_connectionString);
                var eckdaten = await client.GetAuftragEckdatenAsync(kAuftrag);
                if (eckdaten == null) return null;

                return new AuftragEckdatenDto
                {
                    KAuftrag = eckdaten.KAuftrag,
                    WertNetto = eckdaten.FWertNetto,
                    WertBrutto = eckdaten.FWertBrutto,
                    Zahlung = eckdaten.FZahlung,
                    Gutschrift = eckdaten.FGutschrift,
                    OffenerWert = eckdaten.FOffenerWert,
                    ZahlungStatus = eckdaten.NZahlungStatus,
                    ZahlungStatusText = Infrastructure.Jtl.JtlOrderClient.GetZahlungStatusText(eckdaten.NZahlungStatus),
                    RechnungStatus = eckdaten.NRechnungStatus,
                    RechnungStatusText = Infrastructure.Jtl.JtlOrderClient.GetRechnungStatusText(eckdaten.NRechnungStatus),
                    LieferStatus = eckdaten.NLieferstatus,
                    LieferStatusText = Infrastructure.Jtl.JtlOrderClient.GetLieferStatusText(eckdaten.NLieferstatus),
                    LieferStatusColor = Infrastructure.Jtl.JtlOrderClient.GetLieferStatusColor(eckdaten.NLieferstatus),
                    KomplettAusgeliefert = eckdaten.NKomplettAusgeliefert > 0,
                    Bezahlt = eckdaten.DBezahlt,
                    LetzterVersand = eckdaten.DLetzterVersand,
                    AnzahlPakete = eckdaten.NAnzahlPakete,
                    AnzahlVersendetePakete = eckdaten.NAnzahlVersendetePakete,
                    Rechnungsnummern = eckdaten.CRechnungsnummern
                };
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Fehler beim Laden der Eckdaten für Auftrag {KAuftrag}", kAuftrag);
                return null;
            }
        }

        /// <summary>
        /// DTO für Auftrags-Eckdaten (berechnete Werte aus JTL)
        /// </summary>
        public class AuftragEckdatenDto
        {
            public int KAuftrag { get; set; }
            public decimal WertNetto { get; set; }
            public decimal WertBrutto { get; set; }
            public decimal Zahlung { get; set; }
            public decimal Gutschrift { get; set; }
            public decimal OffenerWert { get; set; }
            public int ZahlungStatus { get; set; }
            public string ZahlungStatusText { get; set; } = "";
            public int RechnungStatus { get; set; }
            public string RechnungStatusText { get; set; } = "";
            public int LieferStatus { get; set; }
            public string LieferStatusText { get; set; } = "";
            public string LieferStatusColor { get; set; } = "";
            public bool KomplettAusgeliefert { get; set; }
            public DateTime? Bezahlt { get; set; }
            public DateTime? LetzterVersand { get; set; }
            public int AnzahlPakete { get; set; }
            public int AnzahlVersendetePakete { get; set; }
            public string? Rechnungsnummern { get; set; }
        }

        #endregion
    }
}

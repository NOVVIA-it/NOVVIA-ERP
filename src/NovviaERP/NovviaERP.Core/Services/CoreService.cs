using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Dapper;
using Serilog;
using NovviaERP.Core.Entities;

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
        private readonly LogService? _logService;

        public CoreService(string connectionString) => _connectionString = connectionString;

        public CoreService(string connectionString, LogService logService) : this(connectionString)
        {
            _logService = logService;
        }

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
            public string? CAnrede { get; set; }
            public string? CVorname { get; set; }
            public string? CName { get; set; }  // Nachname
            public string? CStrasse { get; set; }
            public string? CPLZ { get; set; }
            public string? COrt { get; set; }
            public string? CISO { get; set; }  // Land ISO
            public string? CLand { get; set; }
            public string? CMail { get; set; }
            public string? CTel { get; set; }
            public string? CMobil { get; set; }
            public string? CFax { get; set; }
            public string? CSperre { get; set; }
            public int? KKundenGruppe { get; set; }
            public string? Kundengruppe { get; set; }
            public string? CWww { get; set; }
            public string? CLabel { get; set; }
            public string? CUstId { get; set; }
            public DateTime? DErstellt { get; set; }
            public decimal Umsatz { get; set; }

            // Berechnete Felder
            public string Anzeigename => !string.IsNullOrEmpty(CFirma) ? CFirma : $"{CVorname} {CName}".Trim();
            public string AdressZeile => $"{CPLZ} {COrt} {CISO}".Trim();
            public bool IstGesperrt => CSperre == "Y";
        }

        public class KundeStatistik
        {
            public int KKunde { get; set; }
            public decimal UmsatzGesamt { get; set; }
            public decimal UmsatzJahr { get; set; }
            public decimal GewinnGesamt { get; set; }
            public int AnzahlAuftraege { get; set; }
            public decimal DurchschnittWarenkorb { get; set; }
            public decimal OffenePosten { get; set; }
            public int OffeneAuftraege { get; set; }
            public decimal OffeneAuftraegeWert { get; set; }
            public int OffeneRechnungen { get; set; }
            public int AnzahlRetouren { get; set; }
            public int AnzahlStornos { get; set; }
            public int AnzahlMahnungen { get; set; }
            public int CouponKaeufe { get; set; }
            public decimal Guthaben { get; set; }
            public decimal Rabatt { get; set; }
            public string? Anmerkung { get; set; }
            public bool Newsletter { get; set; }
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
            public string? CLand { get; set; }
            public string? CISO { get; set; }
            public bool NStandard { get; set; }
            public string AdressTypText => NTyp switch { 1 => "🧾 Rechnung", 2 => "📦 Lieferung", _ => "📍 Sonstige" };
            public bool IstStandard => NStandard;
            public string StandardIcon => NStandard ? "⭐" : "";
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
            public string? CUstIdNr { get; set; }
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
            public string AdressTypText => NTyp == 1 ? "🧾 Rechnungsadresse" : NTyp == 2 ? "📦 Lieferadresse" : "📍 Adresse";
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
            public decimal FUVP { get; set; }
            public decimal FMwSt { get; set; } = 19m;
            public decimal NLagerbestand { get; set; }
            public decimal NMidestbestand { get; set; }
            public string CAktiv { get; set; } = "Y";
            public string CLagerArtikel { get; set; } = "Y";
            public int? KHersteller { get; set; }
            public string? Hersteller { get; set; }
            public int? KWarengruppe { get; set; }
            public string? Warengruppe { get; set; }

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
            public string? CGTIN { get; set; }
            public int NSort { get; set; }

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

            // Suchbegriffe & Anmerkung
            public string? CSuchbegriffe { get; set; }
            public string? CAnmerkung { get; set; }

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
            public DateTime? DExternesDatum { get; set; }  // JTL: dExternesErstelldatum
            public DateTime? DErstelltWawi { get; set; }   // JTL: dErstelltWawi

            /// <summary>JTL-Datumslogik: Externes Datum (Shop) > Erstellt > ErstelltWawi</summary>
            public DateTime DatumAnzeige => DExternesDatum ?? DErstellt;

            public string? CStatus { get; set; }
            public byte? NAuftragStatus { get; set; }  // JTL nAuftragStatus
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

            // Status-Helper für UI-Farben
            public string StatusFarbe => CStatus switch
            {
                "Offen" => "#FFA500",        // Orange
                "In Bearbeitung" => "#1E90FF", // Blau
                "Versandbereit" => "#9370DB",  // Lila
                "Versendet" => "#32CD32",      // Grün
                "Abgeschlossen" => "#228B22",  // Dunkelgrün
                "Storniert" => "#DC143C",      // Rot
                _ => "#808080"                 // Grau
            };

            // Rechnungsadresse
            public string? ReFirma { get; set; }
            public string? ReName { get; set; }
            public string? ReVorname { get; set; }
            public string? ReStrasse { get; set; }
            public string? RePlz { get; set; }
            public string? ReOrt { get; set; }
            public string? ReLand { get; set; }
            public string? ReMail { get; set; }
            public string? ReTel { get; set; }

            // Lieferadresse
            public string? LiFirma { get; set; }
            public string? LiName { get; set; }
            public string? LiVorname { get; set; }
            public string? LiStrasse { get; set; }
            public string? LiPlz { get; set; }
            public string? LiOrt { get; set; }
            public string? LiLand { get; set; }

            // Texte
            public string? CAnmerkung { get; set; }

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
            public byte? NAuftragStatus { get; set; }  // JTL nAuftragStatus: 0=Offen,1=InBearbeitung,2=Versandbereit,3=Versendet,4=Abgeschlossen,255=Storniert
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
            public string? CHinweis { get; set; }
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

        public class VersandklasseRef
        {
            public int KVersandklasse { get; set; }
            public string? CName { get; set; }
        }

        public class WarengruppeRef
        {
            public int KWarengruppe { get; set; }
            public string? CName { get; set; }
        }

        #endregion

        #region Kunden

        public async Task<IEnumerable<KundeUebersicht>> GetKundenAsync(string? suche = null, string? plz = null, int? kundengruppeId = null, bool nurAktive = false, int limit = 200, int offset = 0)
        {
            var conn = await GetConnectionAsync();
            var sql = @"
                SELECT
                    k.kKunde, k.cKundenNr, k.cSperre, k.kKundenGruppe, k.dErstellt, k.cWWW AS CWww,
                    a.cFirma, a.cVorname, a.cName, a.cAnrede, a.cStrasse, a.cPLZ, a.cOrt, a.cISO, a.cMail, a.cTel, a.cMobil, a.cFax, a.cUSTID AS CUstId,
                    l.cName AS CLand,
                    kg.cName AS Kundengruppe,
                    ISNULL((SELECT TOP 1 lb.cName FROM tKundeLabel kl JOIN tLabel lb ON kl.kLabel = lb.kLabel WHERE kl.kKunde = k.kKunde), '') AS CLabel,
                    ISNULL((SELECT SUM(ae.fWertNetto)
                            FROM Verkauf.tAuftrag au
                            JOIN Verkauf.tAuftragEckdaten ae ON au.kAuftrag = ae.kAuftrag
                            WHERE au.kKunde = k.kKunde AND au.nStorno = 0), 0) AS Umsatz
                FROM dbo.tKunde k
                LEFT JOIN tAdresse a ON a.kKunde = k.kKunde AND a.nStandard = 1
                LEFT JOIN tKundenGruppe kg ON kg.kKundenGruppe = k.kKundenGruppe
                LEFT JOIN tLand l ON l.cISO = a.cISO
                WHERE 1=1";

            if (nurAktive) sql += " AND k.cSperre != 'Y'";
            if (kundengruppeId.HasValue) sql += " AND k.kKundenGruppe = @KundengruppeId";
            if (!string.IsNullOrEmpty(plz)) sql += " AND a.cPLZ LIKE @PLZ";
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
            sql += " OFFSET @Offset ROWS FETCH NEXT @Limit ROWS ONLY";

            return await conn.QueryAsync<KundeUebersicht>(sql, new { Limit = limit, Offset = offset, Suche = $"%{suche}%", PLZ = $"{plz}%", KundengruppeId = kundengruppeId });
        }

        /// <summary>Kundensuche fuer Dialoge - gibt Liste zurueck</summary>
        public async Task<List<KundeUebersicht>> SearchKundenAsync(string suchbegriff, int limit = 50)
        {
            var conn = await GetConnectionAsync();
            var sql = @"
                SELECT TOP (@Limit)
                    k.kKunde, k.cKundenNr, k.cSperre, k.kKundenGruppe, k.dErstellt,
                    a.cFirma, a.cVorname, a.cName, a.cStrasse, a.cPLZ, a.cOrt, a.cISO, a.cMail, a.cTel,
                    kg.cName AS Kundengruppe,
                    0 AS Umsatz
                FROM tkunde k
                LEFT JOIN tAdresse a ON a.kKunde = k.kKunde AND a.nStandard = 1
                LEFT JOIN tKundenGruppe kg ON kg.kKundenGruppe = k.kKundenGruppe
                WHERE k.cSperre != 'Y'
                  AND (k.cKundenNr LIKE @Suche
                       OR a.cFirma LIKE @Suche
                       OR a.cName LIKE @Suche
                       OR a.cVorname LIKE @Suche
                       OR a.cPLZ LIKE @Suche
                       OR a.cOrt LIKE @Suche)
                ORDER BY k.cKundenNr";

            var result = await conn.QueryAsync<KundeUebersicht>(sql, new { Limit = limit, Suche = $"%{suchbegriff}%" });
            return result.ToList();
        }

        /// <summary>Erweiterte Kundensuche mit Kategorie und Gruppe Filter</summary>
        public async Task<List<KundeUebersicht>> SearchKundenErweitertAsync(string suchbegriff, int? kategorieId = null, int? gruppeId = null, int limit = 50)
        {
            var conn = await GetConnectionAsync();
            var sql = @"
                SELECT TOP (@Limit)
                    k.kKunde, k.cKundenNr, k.cSperre, k.kKundenGruppe, k.dErstellt,
                    a.cFirma, a.cVorname, a.cName, a.cStrasse, a.cPLZ, a.cOrt, a.cISO, a.cMail, a.cTel,
                    kg.cName AS Kundengruppe,
                    0 AS Umsatz
                FROM tkunde k
                LEFT JOIN tAdresse a ON a.kKunde = k.kKunde AND a.nStandard = 1
                LEFT JOIN tKundenGruppe kg ON kg.kKundenGruppe = k.kKundenGruppe
                WHERE k.cSperre != 'Y'
                  AND (@Suche = '' OR k.cKundenNr LIKE @Suche
                       OR a.cFirma LIKE @Suche
                       OR a.cName LIKE @Suche
                       OR a.cVorname LIKE @Suche
                       OR a.cStrasse LIKE @Suche
                       OR a.cPLZ LIKE @Suche
                       OR a.cOrt LIKE @Suche)
                  AND (@KategorieId IS NULL OR k.kKundenKategorie = @KategorieId)
                  AND (@GruppeId IS NULL OR k.kKundenGruppe = @GruppeId)
                ORDER BY k.cKundenNr";

            var result = await conn.QueryAsync<KundeUebersicht>(sql, new {
                Limit = limit,
                Suche = string.IsNullOrEmpty(suchbegriff) ? "" : $"%{suchbegriff}%",
                KategorieId = kategorieId,
                GruppeId = gruppeId
            });
            return result.ToList();
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

            // Nächste Kundennummer
            if (string.IsNullOrEmpty(kunde.CKundenNr))
            {
                var maxNr = await conn.QuerySingleOrDefaultAsync<string>(
                    "SELECT MAX(cKundenNr) FROM tkunde WHERE cKundenNr LIKE '[0-9]%'");
                int next = 1;
                if (!string.IsNullOrEmpty(maxNr) && int.TryParse(maxNr, out var num)) next = num + 1;
                kunde.CKundenNr = next.ToString();
            }

            // DataTable für Kunde.spKundeInsert
            var dt = new DataTable();
            dt.Columns.Add("kInternalId", typeof(int));
            dt.Columns.Add("kInetKunde", typeof(int));
            dt.Columns.Add("kKundenKategorie", typeof(int));
            dt.Columns.Add("cKundenNr", typeof(string));
            dt.Columns.Add("cFirma", typeof(string));
            dt.Columns.Add("cAnrede", typeof(string));
            dt.Columns.Add("cTitel", typeof(string));
            dt.Columns.Add("cVorname", typeof(string));
            dt.Columns.Add("cName", typeof(string));
            dt.Columns.Add("cStrasse", typeof(string));
            dt.Columns.Add("cPLZ", typeof(string));
            dt.Columns.Add("cOrt", typeof(string));
            dt.Columns.Add("cLand", typeof(string));
            dt.Columns.Add("cTel", typeof(string));
            dt.Columns.Add("cFax", typeof(string));
            dt.Columns.Add("cEMail", typeof(string));
            dt.Columns.Add("dErstellt", typeof(DateTime));
            dt.Columns.Add("cMobil", typeof(string));
            dt.Columns.Add("fRabatt", typeof(decimal));
            dt.Columns.Add("cUSTID", typeof(string));
            dt.Columns.Add("cNewsletter", typeof(string));
            dt.Columns.Add("cZusatz", typeof(string));
            dt.Columns.Add("cEbayName", typeof(string));
            dt.Columns.Add("kBuyer", typeof(int));
            dt.Columns.Add("cAdressZusatz", typeof(string));
            dt.Columns.Add("cGeburtstag", typeof(string));
            dt.Columns.Add("cWWW", typeof(string));
            dt.Columns.Add("cSperre", typeof(string));
            dt.Columns.Add("cPostID", typeof(string));
            dt.Columns.Add("kKundenGruppe", typeof(int));
            dt.Columns.Add("nZahlungsziel", typeof(int));
            dt.Columns.Add("kSprache", typeof(int));
            dt.Columns.Add("cISO", typeof(string));
            dt.Columns.Add("cBundesland", typeof(string));
            dt.Columns.Add("cHerkunft", typeof(string));
            dt.Columns.Add("cKassenKunde", typeof(string));
            dt.Columns.Add("cHRNr", typeof(string));
            dt.Columns.Add("kZahlungsart", typeof(int));
            dt.Columns.Add("nDebitorennr", typeof(int));
            dt.Columns.Add("cSteuerNr", typeof(string));
            dt.Columns.Add("nKreditlimit", typeof(int));
            dt.Columns.Add("kKundenDrucktext", typeof(int));
            dt.Columns.Add("nMahnstopp", typeof(byte));
            dt.Columns.Add("nMahnrhythmus", typeof(int));
            dt.Columns.Add("kFirma", typeof(byte));
            dt.Columns.Add("fProvision", typeof(decimal));
            dt.Columns.Add("nVertreter", typeof(byte));
            dt.Columns.Add("fSkonto", typeof(decimal));
            dt.Columns.Add("nSkontoInTagen", typeof(int));
            dt.Columns.Add("dGeaendert", typeof(DateTime));

            var row = dt.NewRow();
            row["kInternalId"] = 1;
            row["kKundenKategorie"] = kunde.KKundenKategorie ?? (object)DBNull.Value;
            row["cKundenNr"] = kunde.CKundenNr ?? "";
            row["cFirma"] = adresse.CFirma ?? "";
            row["cAnrede"] = adresse.CAnrede ?? "";
            row["cTitel"] = adresse.CTitel ?? "";
            row["cVorname"] = adresse.CVorname ?? "";
            row["cName"] = adresse.CName ?? "";
            row["cStrasse"] = adresse.CStrasse ?? "";
            row["cPLZ"] = adresse.CPLZ ?? "";
            row["cOrt"] = adresse.COrt ?? "";
            row["cLand"] = adresse.CLand ?? "Deutschland";
            row["cTel"] = adresse.CTel ?? "";
            row["cFax"] = adresse.CFax ?? "";
            row["cEMail"] = adresse.CMail ?? "";
            row["dErstellt"] = DateTime.Now;
            row["cMobil"] = adresse.CMobil ?? "";
            row["fRabatt"] = kunde.FRabatt;
            row["cUSTID"] = adresse.CUSTID ?? "";
            row["cNewsletter"] = kunde.CNewsletter ?? "N";
            row["cZusatz"] = adresse.CZusatz ?? "";
            row["cAdressZusatz"] = adresse.CAdressZusatz ?? "";
            row["cSperre"] = kunde.CSperre ?? "N";
            row["kKundenGruppe"] = kunde.KKundenGruppe ?? 1;
            row["nZahlungsziel"] = kunde.NZahlungsziel ?? 14;
            row["kSprache"] = 1;
            row["cISO"] = adresse.CISO ?? "DE";
            row["cBundesland"] = adresse.CBundesland ?? "";
            row["kZahlungsart"] = kunde.KZahlungsart ?? (object)DBNull.Value;
            row["nDebitorennr"] = kunde.NDebitorennr;
            row["nKreditlimit"] = kunde.NKreditlimit;
            row["nMahnstopp"] = kunde.NMahnstopp;
            row["nMahnrhythmus"] = kunde.NMahnrhythmus;
            row["kFirma"] = (byte)1;
            row["fSkonto"] = kunde.FSkonto;
            row["nSkontoInTagen"] = kunde.NSkontoInTagen;
            row["dGeaendert"] = DateTime.Now;
            dt.Rows.Add(row);

            var p = new DynamicParameters();
            p.Add("@Daten", dt.AsTableValuedParameter("dbo.TYPE_spkundeInsert"));

            await conn.ExecuteAsync("Kunde.spKundeInsert", p, commandType: CommandType.StoredProcedure);

            // Neue kKunde ermitteln
            var kundeId = await conn.QuerySingleAsync<int>(
                "SELECT TOP 1 kKunde FROM dbo.tKunde WHERE cKundenNr = @Nr ORDER BY kKunde DESC",
                new { Nr = kunde.CKundenNr });

            _log.Information("Kunde {KundenNr} angelegt via SP (ID: {Id})", kunde.CKundenNr, kundeId);

            // Log in NOVVIA.Log
            if (_logService != null)
            {
                await _logService.LogStammdatenAsync(
                    entityTyp: "Kunde",
                    kEntity: kundeId,
                    entityNr: kunde.CKundenNr,
                    aktion: "Erstellt",
                    beschreibung: $"Neuer Kunde: {adresse.CFirma ?? ""} {adresse.CVorname ?? ""} {adresse.CName ?? ""} ({adresse.CPLZ} {adresse.COrt})".Trim());
            }

            return kundeId;
        }

        public async Task UpdateKundeAsync(KundeDetail kunde)
        {
            var conn = await GetConnectionAsync();

            // DataTable für Kunde.spKundeUpdate - alle 61 Spalten
            var dt = new DataTable();
            dt.Columns.Add("kKunde", typeof(int));
            dt.Columns.Add("kInetKunde", typeof(int));
            dt.Columns.Add("xFlag_kInetKunde", typeof(bool));
            dt.Columns.Add("kKundenKategorie", typeof(int));
            dt.Columns.Add("xFlag_kKundenKategorie", typeof(bool));
            dt.Columns.Add("cKundenNr", typeof(string));
            dt.Columns.Add("xFlag_cKundenNr", typeof(bool));
            dt.Columns.Add("dErstellt", typeof(DateTime));
            dt.Columns.Add("xFlag_dErstellt", typeof(bool));
            dt.Columns.Add("fRabatt", typeof(decimal));
            dt.Columns.Add("xFlag_fRabatt", typeof(bool));
            dt.Columns.Add("cNewsletter", typeof(string));
            dt.Columns.Add("xFlag_cNewsletter", typeof(bool));
            dt.Columns.Add("cEbayName", typeof(string));
            dt.Columns.Add("xFlag_cEbayName", typeof(bool));
            dt.Columns.Add("kBuyer", typeof(int));
            dt.Columns.Add("xFlag_kBuyer", typeof(bool));
            dt.Columns.Add("cGeburtstag", typeof(string));
            dt.Columns.Add("xFlag_cGeburtstag", typeof(bool));
            dt.Columns.Add("cWWW", typeof(string));
            dt.Columns.Add("xFlag_cWWW", typeof(bool));
            dt.Columns.Add("cSperre", typeof(string));
            dt.Columns.Add("xFlag_cSperre", typeof(bool));
            dt.Columns.Add("kKundenGruppe", typeof(int));
            dt.Columns.Add("xFlag_kKundenGruppe", typeof(bool));
            dt.Columns.Add("nZahlungsziel", typeof(int));
            dt.Columns.Add("xFlag_nZahlungsziel", typeof(bool));
            dt.Columns.Add("kSprache", typeof(int));
            dt.Columns.Add("xFlag_kSprache", typeof(bool));
            dt.Columns.Add("cHerkunft", typeof(string));
            dt.Columns.Add("xFlag_cHerkunft", typeof(bool));
            dt.Columns.Add("cKassenKunde", typeof(string));
            dt.Columns.Add("xFlag_cKassenKunde", typeof(bool));
            dt.Columns.Add("cHRNr", typeof(string));
            dt.Columns.Add("xFlag_cHRNr", typeof(bool));
            dt.Columns.Add("kZahlungsart", typeof(int));
            dt.Columns.Add("xFlag_kZahlungsart", typeof(bool));
            dt.Columns.Add("nDebitorennr", typeof(int));
            dt.Columns.Add("xFlag_nDebitorennr", typeof(bool));
            dt.Columns.Add("cSteuerNr", typeof(string));
            dt.Columns.Add("xFlag_cSteuerNr", typeof(bool));
            dt.Columns.Add("nKreditlimit", typeof(int));
            dt.Columns.Add("xFlag_nKreditlimit", typeof(bool));
            dt.Columns.Add("kKundenDrucktext", typeof(int));
            dt.Columns.Add("xFlag_kKundenDrucktext", typeof(bool));
            dt.Columns.Add("nMahnstopp", typeof(byte));
            dt.Columns.Add("xFlag_nMahnstopp", typeof(bool));
            dt.Columns.Add("nMahnrhythmus", typeof(int));
            dt.Columns.Add("xFlag_nMahnrhythmus", typeof(bool));
            dt.Columns.Add("kFirma", typeof(byte));
            dt.Columns.Add("xFlag_kFirma", typeof(bool));
            dt.Columns.Add("fProvision", typeof(decimal));
            dt.Columns.Add("xFlag_fProvision", typeof(bool));
            dt.Columns.Add("nVertreter", typeof(byte));
            dt.Columns.Add("xFlag_nVertreter", typeof(bool));
            dt.Columns.Add("fSkonto", typeof(decimal));
            dt.Columns.Add("xFlag_fSkonto", typeof(bool));
            dt.Columns.Add("nSkontoInTagen", typeof(int));
            dt.Columns.Add("xFlag_nSkontoInTagen", typeof(bool));
            dt.Columns.Add("dGeaendert", typeof(DateTime));
            dt.Columns.Add("xFlag_dGeaendert", typeof(bool));

            var row = dt.NewRow();
            row["kKunde"] = kunde.KKunde;
            row["xFlag_kInetKunde"] = false;
            row["kKundenKategorie"] = kunde.KKundenKategorie ?? (object)DBNull.Value;
            row["xFlag_kKundenKategorie"] = kunde.KKundenKategorie.HasValue;
            row["xFlag_cKundenNr"] = false;
            row["xFlag_dErstellt"] = false;
            row["fRabatt"] = kunde.FRabatt;
            row["xFlag_fRabatt"] = true;
            row["cNewsletter"] = kunde.CNewsletter ?? "N";
            row["xFlag_cNewsletter"] = true;
            row["xFlag_cEbayName"] = false;
            row["xFlag_kBuyer"] = false;
            row["xFlag_cGeburtstag"] = false;
            row["xFlag_cWWW"] = false;
            row["cSperre"] = kunde.CSperre ?? "N";
            row["xFlag_cSperre"] = true;
            row["kKundenGruppe"] = kunde.KKundenGruppe ?? 1;
            row["xFlag_kKundenGruppe"] = true;
            row["nZahlungsziel"] = kunde.NZahlungsziel ?? 14;
            row["xFlag_nZahlungsziel"] = true;
            row["xFlag_kSprache"] = false;
            row["xFlag_cHerkunft"] = false;
            row["xFlag_cKassenKunde"] = false;
            row["xFlag_cHRNr"] = false;
            row["kZahlungsart"] = kunde.KZahlungsart ?? (object)DBNull.Value;
            row["xFlag_kZahlungsart"] = kunde.KZahlungsart.HasValue;
            row["nDebitorennr"] = kunde.NDebitorennr;
            row["xFlag_nDebitorennr"] = true;
            row["xFlag_cSteuerNr"] = false;
            row["nKreditlimit"] = kunde.NKreditlimit;
            row["xFlag_nKreditlimit"] = true;
            row["xFlag_kKundenDrucktext"] = false;
            row["nMahnstopp"] = kunde.NMahnstopp;
            row["xFlag_nMahnstopp"] = true;
            row["nMahnrhythmus"] = kunde.NMahnrhythmus;
            row["xFlag_nMahnrhythmus"] = true;
            row["xFlag_kFirma"] = false;
            row["xFlag_fProvision"] = false;
            row["xFlag_nVertreter"] = false;
            row["fSkonto"] = kunde.FSkonto;
            row["xFlag_fSkonto"] = true;
            row["nSkontoInTagen"] = kunde.NSkontoInTagen;
            row["xFlag_nSkontoInTagen"] = true;
            row["dGeaendert"] = DateTime.Now;
            row["xFlag_dGeaendert"] = true;
            dt.Rows.Add(row);

            var p = new DynamicParameters();
            p.Add("@Daten", dt.AsTableValuedParameter("dbo.TYPE_spkundeUpdate"));
            p.Add("@kBenutzer", 1);  // Standard-Benutzer

            await conn.ExecuteAsync("Kunde.spKundeUpdate", p, commandType: CommandType.StoredProcedure);

            // Log in NOVVIA.Log
            if (_logService != null)
            {
                var adressInfo = kunde.StandardAdresse != null
                    ? $"{kunde.StandardAdresse.CFirma ?? ""} {kunde.StandardAdresse.CVorname ?? ""} {kunde.StandardAdresse.CName ?? ""}".Trim()
                    : "";
                await _logService.LogStammdatenAsync(
                    entityTyp: "Kunde",
                    kEntity: kunde.KKunde,
                    entityNr: kunde.CKundenNr,
                    aktion: "Geaendert",
                    beschreibung: $"Kunde aktualisiert: {adressInfo}".Trim());
            }
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

        public async Task<IEnumerable<KundenkategorieRef>> GetKundenkategorienAsync()
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<KundenkategorieRef>("SELECT kKundenKategorie, cName FROM tKundenKategorie ORDER BY cName");
        }

        public class KundenkategorieRef
        {
            public int KKundenKategorie { get; set; }
            public string CName { get; set; } = "";
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
                    "SELECT COUNT(*) FROM Verkauf.tAuftrag WHERE kKunde = @kundeId AND nStorno = 0", new { kundeId });
                stats.AnzahlAuftraege = anzahl;

                // Umsatz berechnen (wie in anderen Queries - aus Positionen)
                var umsatz = await conn.QueryFirstOrDefaultAsync<decimal?>(@"
                    SELECT SUM(ae.fWertBrutto) FROM Verkauf.tAuftrag a JOIN Verkauf.tAuftragEckdaten ae ON a.kAuftrag = ae.kAuftrag WHERE a.kKunde = @kundeId AND a.nStorno = 0", new { kundeId });
                stats.UmsatzGesamt = umsatz ?? 0;

                // Durchschnitt
                stats.DurchschnittWarenkorb = stats.AnzahlAuftraege > 0
                    ? stats.UmsatzGesamt / stats.AnzahlAuftraege : 0;

                // Erste/Letzte Bestellung
                var datumStats = await conn.QueryFirstOrDefaultAsync<(DateTime? Erste, DateTime? Letzte)?>(@"
                    SELECT MIN(dErstellt) AS Erste, MAX(dErstellt) AS Letzte FROM Verkauf.tAuftrag WHERE kKunde = @kundeId AND nStorno = 0", new { kundeId });
                if (datumStats.HasValue)
                {
                    stats.ErstBestellung = datumStats.Value.Erste;
                    stats.LetzteBestellung = datumStats.Value.Letzte;
                }

                // Offene Posten aus Rechnungen (JTL-Wawi 1.x Schema)
                var offen = await conn.QueryFirstOrDefaultAsync<decimal?>(@"
                    SELECT ISNULL(SUM(re.fOffenerWert), 0)
                    FROM Rechnung.tRechnung r
                    JOIN Rechnung.tRechnungEckdaten re ON r.kRechnung = re.kRechnung
                    WHERE r.kKunde = @kundeId AND re.nZahlungStatus < 2 AND r.nStorno = 0", new { kundeId });
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
                    a.kAuftrag AS KBestellung,
                    ISNULL(a.cAuftragsnr, CAST(a.kAuftrag AS VARCHAR)) AS CBestellNr,
                    a.dErstellt AS DErstellt,
                    CASE a.nAuftragStatus WHEN 0 THEN 'Neu' WHEN 1 THEN 'In Bearbeitung' WHEN 2 THEN 'Versandbereit' WHEN 3 THEN 'Versendet' WHEN 4 THEN 'Abgeschlossen' ELSE 'Offen' END AS CStatus,
                    ISNULL(ae.fWertBrutto, 0) AS GesamtBrutto
                FROM Verkauf.tAuftrag a
                LEFT JOIN Verkauf.tAuftragEckdaten ae ON a.kAuftrag = ae.kAuftrag
                WHERE a.kKunde = @kundeId AND a.nStorno = 0
                ORDER BY a.dErstellt DESC", new { kundeId });
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
                    ISNULL(re.fVKBruttoGesamt, 0) AS FBetragBrutto,
                    ISNULL(re.fVKBruttoGesamt - re.fOffenerWert, 0) AS FBezahlt
                FROM Rechnung.tRechnung r
                JOIN Rechnung.tRechnungEckdaten re ON r.kRechnung = re.kRechnung
                WHERE r.kKunde = @kundeId AND r.nStorno = 0
                ORDER BY r.dErstellt DESC", new { kundeId });
        }

        /// <summary>
        /// Adressen eines Kunden (für 360°-Ansicht, JTL-Wawi 1.x Schema)
        /// </summary>
        public async Task<IEnumerable<KundeAdresseKurz>> GetKundeAdressenKurzAsync(int kundeId)
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<KundeAdresseKurz>(@"
                SELECT kAdresse AS KAdresse, ISNULL(nTyp, 0) AS NTyp,
                       ISNULL(cName, '') + CASE WHEN cVorname IS NOT NULL THEN ', ' + cVorname ELSE '' END AS CName,
                       ISNULL(cStrasse, '') AS CStrasse, ISNULL(cPLZ, '') AS CPLZ, ISNULL(cOrt, '') AS COrt,
                       cLand AS CLand, cISO AS CISO,
                       CASE WHEN nStandard = 1 THEN 1 ELSE 0 END AS NStandard
                FROM dbo.tAdresse
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
                           'Auftrag ' + ISNULL(cAuftragsnr, CAST(kAuftrag AS VARCHAR)) AS Beschreibung
                    FROM Verkauf.tAuftrag WHERE kKunde = @kundeId AND nStorno = 0
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
                    FROM Rechnung.tRechnung WHERE kKunde = @kundeId AND nStorno = 0
                    ORDER BY dErstellt DESC", new { kundeId });
                historie.AddRange(rechnungen);
            }
            catch { /* Rechnungen optional */ }

            return historie.OrderByDescending(h => h.Datum).Take(limit);
        }

        #endregion

        #region Kunden - Adressen, Ansprechpartner, Bankverbindungen CRUD

        /// <summary>
        /// Adresse hinzufuegen
        /// </summary>
        public async Task<int> AddKundeAdresseAsync(int kundeId, AdresseDetail adresse)
        {
            var conn = await GetConnectionAsync();
            var sql = @"
                INSERT INTO dbo.tAdresse (kKunde, nTyp, nStandard, cFirma, cAnrede, cTitel, cVorname, cName,
                    cStrasse, cAdressZusatz, cPLZ, cOrt, cLand, cTel, cMobil, cFax, cMail)
                VALUES (@KKunde, @NTyp, @NStandard, @CFirma, @CAnrede, @CTitel, @CVorname, @CName,
                    @CStrasse, @CAdressZusatz, @CPLZ, @COrt, @CLand, @CTel, @CMobil, @CFax, @CMail);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var id = await conn.ExecuteScalarAsync<int>(sql, new
            {
                KKunde = kundeId,
                NTyp = adresse.NTyp,
                NStandard = adresse.NStandard,
                CFirma = adresse.CFirma,
                CAnrede = adresse.CAnrede,
                CTitel = adresse.CTitel,
                CVorname = adresse.CVorname,
                CName = adresse.CName,
                CStrasse = adresse.CStrasse,
                CAdressZusatz = adresse.CAdressZusatz,
                CPLZ = adresse.CPLZ,
                COrt = adresse.COrt,
                CLand = adresse.CLand ?? "Deutschland",
                CTel = adresse.CTel,
                CMobil = adresse.CMobil,
                CFax = adresse.CFax,
                CMail = adresse.CMail
            });

            await _logService.LogStammdatenAsync("Kunde", kundeId, null, "Adresse hinzugefuegt",
                $"Neue Adresse: {adresse.CStrasse}, {adresse.CPLZ} {adresse.COrt}");

            return id;
        }

        /// <summary>
        /// Adresse aktualisieren
        /// </summary>
        public async Task UpdateKundeAdresseAsync(AdresseDetail adresse)
        {
            var conn = await GetConnectionAsync();
            var sql = @"
                UPDATE dbo.tAdresse SET
                    nTyp = @NTyp, nStandard = @NStandard, cFirma = @CFirma, cAnrede = @CAnrede, cTitel = @CTitel,
                    cVorname = @CVorname, cName = @CName, cStrasse = @CStrasse, cAdressZusatz = @CAdressZusatz,
                    cPLZ = @CPLZ, cOrt = @COrt, cLand = @CLand, cTel = @CTel, cMobil = @CMobil, cFax = @CFax, cMail = @CMail
                WHERE kAdresse = @KAdresse";

            await conn.ExecuteAsync(sql, new
            {
                KAdresse = adresse.KAdresse,
                NTyp = adresse.NTyp,
                NStandard = adresse.NStandard,
                CFirma = adresse.CFirma,
                CAnrede = adresse.CAnrede,
                CTitel = adresse.CTitel,
                CVorname = adresse.CVorname,
                CName = adresse.CName,
                CStrasse = adresse.CStrasse,
                CAdressZusatz = adresse.CAdressZusatz,
                CPLZ = adresse.CPLZ,
                COrt = adresse.COrt,
                CLand = adresse.CLand,
                CTel = adresse.CTel,
                CMobil = adresse.CMobil,
                CFax = adresse.CFax,
                CMail = adresse.CMail
            });

            await _logService.LogStammdatenAsync("Kunde", adresse.KKunde ?? 0, null, "Adresse geaendert",
                $"Adresse aktualisiert: {adresse.CStrasse}, {adresse.CPLZ} {adresse.COrt}");
        }

        /// <summary>
        /// Adresse loeschen
        /// </summary>
        public async Task DeleteKundeAdresseAsync(int adresseId)
        {
            var conn = await GetConnectionAsync();

            // Erst Kunde-ID und Adresse für Log holen
            var adresse = await conn.QueryFirstOrDefaultAsync<(int KKunde, string? Strasse, string? Ort)>(
                "SELECT kKunde, cStrasse, cOrt FROM dbo.tAdresse WHERE kAdresse = @Id", new { Id = adresseId });

            await conn.ExecuteAsync("DELETE FROM dbo.tAdresse WHERE kAdresse = @Id", new { Id = adresseId });

            if (adresse.KKunde > 0)
                await _logService.LogStammdatenAsync("Kunde", adresse.KKunde, null, "Adresse geloescht",
                    $"Adresse geloescht: {adresse.Strasse}, {adresse.Ort}");
        }

        /// <summary>
        /// Ansprechpartner hinzufuegen
        /// </summary>
        public async Task<int> AddKundeAnsprechpartnerAsync(int kundeId, AnsprechpartnerDetail ansprechpartner)
        {
            var conn = await GetConnectionAsync();
            var sql = @"
                INSERT INTO dbo.tAnsprechpartner (kKunde, cAnrede, cVorName, cName, cAbteilung, cTel, cMobil, cFax, cMail)
                VALUES (@KKunde, @CAnrede, @CVorName, @CName, @CAbteilung, @CTel, @CMobil, @CFax, @CMail);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var id = await conn.ExecuteScalarAsync<int>(sql, new
            {
                KKunde = kundeId,
                CAnrede = ansprechpartner.CAnrede,
                CVorName = ansprechpartner.CVorname,
                CName = ansprechpartner.CName,
                CAbteilung = ansprechpartner.CAbteilung,
                CTel = ansprechpartner.CTel,
                CMobil = ansprechpartner.CMobil,
                CFax = ansprechpartner.CFax,
                CMail = ansprechpartner.CMail
            });

            await _logService.LogStammdatenAsync("Kunde", kundeId, null, "Ansprechpartner hinzugefuegt",
                $"Neuer Ansprechpartner: {ansprechpartner.CVorname} {ansprechpartner.CName}");

            return id;
        }

        /// <summary>
        /// Ansprechpartner aktualisieren
        /// </summary>
        public async Task UpdateKundeAnsprechpartnerAsync(AnsprechpartnerDetail ansprechpartner)
        {
            var conn = await GetConnectionAsync();
            var sql = @"
                UPDATE dbo.tAnsprechpartner SET
                    cAnrede = @CAnrede, cVorName = @CVorName, cName = @CName, cAbteilung = @CAbteilung,
                    cTel = @CTel, cMobil = @CMobil, cFax = @CFax, cMail = @CMail
                WHERE kAnsprechpartner = @KAnsprechpartner";

            await conn.ExecuteAsync(sql, new
            {
                KAnsprechpartner = ansprechpartner.KAnsprechpartner,
                CAnrede = ansprechpartner.CAnrede,
                CVorName = ansprechpartner.CVorname,
                CName = ansprechpartner.CName,
                CAbteilung = ansprechpartner.CAbteilung,
                CTel = ansprechpartner.CTel,
                CMobil = ansprechpartner.CMobil,
                CFax = ansprechpartner.CFax,
                CMail = ansprechpartner.CMail
            });

            await _logService.LogStammdatenAsync("Kunde", ansprechpartner.KKunde ?? 0, null, "Ansprechpartner geaendert",
                $"Ansprechpartner aktualisiert: {ansprechpartner.CVorname} {ansprechpartner.CName}");
        }

        /// <summary>
        /// Ansprechpartner loeschen
        /// </summary>
        public async Task DeleteKundeAnsprechpartnerAsync(int ansprechpartnerId)
        {
            var conn = await GetConnectionAsync();

            var ap = await conn.QueryFirstOrDefaultAsync<(int KKunde, string? Vorname, string? Name)>(
                "SELECT kKunde, cVorName, cName FROM dbo.tAnsprechpartner WHERE kAnsprechpartner = @Id", new { Id = ansprechpartnerId });

            await conn.ExecuteAsync("DELETE FROM dbo.tAnsprechpartner WHERE kAnsprechpartner = @Id", new { Id = ansprechpartnerId });

            if (ap.KKunde > 0)
                await _logService.LogStammdatenAsync("Kunde", ap.KKunde, null, "Ansprechpartner geloescht",
                    $"Ansprechpartner geloescht: {ap.Vorname} {ap.Name}");
        }

        /// <summary>
        /// Bankverbindung hinzufuegen
        /// </summary>
        public async Task<int> AddKundeBankverbindungAsync(int kundeId, BankverbindungDetail bankverbindung)
        {
            var conn = await GetConnectionAsync();
            var sql = @"
                INSERT INTO dbo.tkontodaten (kKunde, nStandard, cBankName, cInhaber, cIBAN, cBIC, cBLZ, cKontoNr)
                VALUES (@KKunde, @NStandard, @CBankName, @CInhaber, @CIBAN, @CBIC, @CBLZ, @CKontoNr);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var id = await conn.ExecuteScalarAsync<int>(sql, new
            {
                KKunde = kundeId,
                NStandard = bankverbindung.NStandard,
                CBankName = bankverbindung.CBankName,
                CInhaber = bankverbindung.CInhaber,
                CIBAN = bankverbindung.CIBAN,
                CBIC = bankverbindung.CBIC,
                CBLZ = bankverbindung.CBLZ,
                CKontoNr = bankverbindung.CKontoNr
            });

            await _logService.LogStammdatenAsync("Kunde", kundeId, null, "Bankverbindung hinzugefuegt",
                $"Neue Bankverbindung: {bankverbindung.CBankName} - {MaskIBAN(bankverbindung.CIBAN)}");

            return id;
        }

        /// <summary>
        /// Bankverbindung aktualisieren
        /// </summary>
        public async Task UpdateKundeBankverbindungAsync(BankverbindungDetail bankverbindung)
        {
            var conn = await GetConnectionAsync();
            var sql = @"
                UPDATE dbo.tkontodaten SET
                    nStandard = @NStandard, cBankName = @CBankName, cInhaber = @CInhaber,
                    cIBAN = @CIBAN, cBIC = @CBIC, cBLZ = @CBLZ, cKontoNr = @CKontoNr
                WHERE kKontoDaten = @KKontoDaten";

            await conn.ExecuteAsync(sql, new
            {
                KKontoDaten = bankverbindung.KKontoDaten,
                NStandard = bankverbindung.NStandard,
                CBankName = bankverbindung.CBankName,
                CInhaber = bankverbindung.CInhaber,
                CIBAN = bankverbindung.CIBAN,
                CBIC = bankverbindung.CBIC,
                CBLZ = bankverbindung.CBLZ,
                CKontoNr = bankverbindung.CKontoNr
            });

            await _logService.LogStammdatenAsync("Kunde", bankverbindung.KKunde ?? 0, null, "Bankverbindung geaendert",
                $"Bankverbindung aktualisiert: {bankverbindung.CBankName}");
        }

        /// <summary>
        /// Bankverbindung loeschen
        /// </summary>
        public async Task DeleteKundeBankverbindungAsync(int kontodatenId)
        {
            var conn = await GetConnectionAsync();

            var bv = await conn.QueryFirstOrDefaultAsync<(int KKunde, string? BankName)>(
                "SELECT kKunde, cBankName FROM dbo.tkontodaten WHERE kKontoDaten = @Id", new { Id = kontodatenId });

            await conn.ExecuteAsync("DELETE FROM dbo.tkontodaten WHERE kKontoDaten = @Id", new { Id = kontodatenId });

            if (bv.KKunde > 0)
                await _logService.LogStammdatenAsync("Kunde", bv.KKunde, null, "Bankverbindung geloescht",
                    $"Bankverbindung geloescht: {bv.BankName}");
        }

        private static string MaskIBAN(string? iban)
        {
            if (string.IsNullOrEmpty(iban) || iban.Length < 10) return iban ?? "";
            return iban.Substring(0, 4) + "****" + iban.Substring(iban.Length - 4);
        }

        #endregion

        #region Artikel

        public async Task<IEnumerable<ArtikelUebersicht>> GetArtikelAsync(string? suche = null, int? herstellerId = null, int? warengruppeId = null, int? kategorieId = null, bool nurAktive = true, bool nurUnterMindestbestand = false, int limit = 200)
        {
            var conn = await GetConnectionAsync();
            var sql = @"
                SELECT TOP (@Limit)
                    a.kArtikel, a.cArtNr, a.cBarcode, a.fVKNetto, a.fEKNetto, a.fUVP AS FUVP,
                    ISNULL(lb.fLagerbestand, 0) AS NLagerbestand,
                    ISNULL(a.nMidestbestand, 0) AS NMidestbestand,
                    a.cAktiv, a.cLagerArtikel, a.kHersteller,
                    ab.cName AS Name,
                    h.cName AS Hersteller,
                    ISNULL((SELECT TOP 1 fSteuersatz FROM tSteuersatz WHERE kSteuerklasse = a.kSteuerklasse ORDER BY nPrio DESC), 19) AS FMwSt,
                    ISNULL(a.fVKNetto * (1 + ISNULL((SELECT TOP 1 fSteuersatz FROM tSteuersatz WHERE kSteuerklasse = a.kSteuerklasse ORDER BY nPrio DESC), 19) / 100), a.fVKNetto) AS FVKBrutto
                FROM tArtikel a
                LEFT JOIN tArtikelBeschreibung ab ON a.kArtikel = ab.kArtikel AND ab.kSprache = 1
                LEFT JOIN tHersteller h ON a.kHersteller = h.kHersteller
                LEFT JOIN tlagerbestand lb ON a.kArtikel = lb.kArtikel";

            if (kategorieId.HasValue)
                sql += " INNER JOIN tkategorieartikel ka ON a.kArtikel = ka.kArtikel AND ka.kKategorie = @KategorieId";

            sql += " WHERE a.nDelete = 0";

            if (nurAktive) sql += " AND a.cAktiv = 'Y'";
            if (herstellerId.HasValue) sql += " AND a.kHersteller = @HerstellerId";
            if (warengruppeId.HasValue) sql += " AND a.kWarengruppe = @WarengruppeId";
            if (nurUnterMindestbestand) sql += " AND ISNULL(lb.fLagerbestand, 0) < ISNULL(a.nMidestbestand, 0)";
            if (!string.IsNullOrEmpty(suche))
            {
                sql += @" AND (a.cArtNr LIKE @Suche
                         OR a.cBarcode LIKE @Suche
                         OR ab.cName LIKE @Suche
                         OR a.cHAN LIKE @Suche)";
            }
            sql += " ORDER BY ab.cName, a.cArtNr";

            return await conn.QueryAsync<ArtikelUebersicht>(sql, new { Limit = limit, Suche = $"%{suche}%", HerstellerId = herstellerId, WarengruppeId = warengruppeId, KategorieId = kategorieId });
        }

        /// <summary>Erweiterte Artikelsuche mit Warengruppe Filter</summary>
        public async Task<List<ArtikelUebersicht>> SearchArtikelErweitertAsync(string suchbegriff, int? warengruppeId = null, int limit = 50)
        {
            var conn = await GetConnectionAsync();
            var sql = @"
                SELECT TOP (@Limit)
                    a.kArtikel, a.cArtNr, a.cBarcode, a.fVKNetto, a.fEKNetto, a.kWarengruppe,
                    ISNULL(lb.fLagerbestand, 0) AS NLagerbestand,
                    ISNULL(a.nMidestbestand, 0) AS NMidestbestand,
                    a.cAktiv, a.cLagerArtikel, a.kHersteller,
                    ab.cName AS Name,
                    h.cName AS Hersteller,
                    wg.cName AS Warengruppe,
                    ISNULL((SELECT TOP 1 fSteuersatz FROM tSteuersatz WHERE kSteuerklasse = a.kSteuerklasse ORDER BY nPrio DESC), 19) AS FMwSt,
                    ISNULL(a.fVKNetto * (1 + ISNULL((SELECT TOP 1 fSteuersatz FROM tSteuersatz WHERE kSteuerklasse = a.kSteuerklasse ORDER BY nPrio DESC), 19) / 100), a.fVKNetto) AS FVKBrutto
                FROM tArtikel a
                LEFT JOIN tArtikelBeschreibung ab ON a.kArtikel = ab.kArtikel AND ab.kSprache = 1
                LEFT JOIN tHersteller h ON a.kHersteller = h.kHersteller
                LEFT JOIN tWarengruppe wg ON a.kWarengruppe = wg.kWarengruppe
                LEFT JOIN tlagerbestand lb ON a.kArtikel = lb.kArtikel
                WHERE a.nDelete = 0 AND a.cAktiv = 'Y'
                  AND (@Suche = '' OR a.cArtNr LIKE @Suche
                       OR a.cBarcode LIKE @Suche
                       OR ab.cName LIKE @Suche
                       OR a.cHAN LIKE @Suche)
                  AND (@WarengruppeId IS NULL OR a.kWarengruppe = @WarengruppeId)
                ORDER BY ab.cName, a.cArtNr";

            var result = await conn.QueryAsync<ArtikelUebersicht>(sql, new {
                Limit = limit,
                Suche = string.IsNullOrEmpty(suchbegriff) ? "" : $"%{suchbegriff}%",
                WarengruppeId = warengruppeId
            });
            return result.ToList();
        }

        public async Task<ArtikelDetail?> GetArtikelByIdAsync(int artikelId)
        {
            var conn = await GetConnectionAsync();

            var artikel = await conn.QuerySingleOrDefaultAsync<ArtikelDetail>(@"
                SELECT a.kArtikel, a.cArtNr, a.cBarcode, a.cBarcode AS CGTIN, a.cHAN, a.cISBN, a.cUPC, a.cASIN, a.nSort,
                       a.fVKNetto, a.fUVP, a.fEKNetto,
                       ISNULL(lb.fLagerbestand, 0) AS NLagerbestand,
                       ISNULL(a.nMidestbestand, 0) AS NMidestbestand,
                       a.cLagerArtikel, a.cAktiv, a.cTopArtikel, a.cNeu, a.cTeilbar,
                       a.fGewicht, a.fArtGewicht, a.fBreite, a.fHoehe, a.fLaenge, a.fPackeinheit,
                       a.kSteuerklasse, a.kHersteller, a.kWarengruppe, a.kVersandklasse,
                       a.nMHD, a.nCharge, ISNULL(a.nSeriennummernVerfolgung, 0) AS NSeriennummernVerfolgung,
                       a.cTaric, a.cHerkunftsland, a.cUNNummer, a.cGefahrnr, a.cSuchbegriffe, a.cAnmerkung,
                       ab.cName AS Name, ab.cBeschreibung AS Beschreibung, ab.cKurzBeschreibung AS KurzBeschreibung, ab.cUrlPfad AS CSeo,
                       h.cName AS Hersteller,
                       sk.cName AS Steuerklasse,
                       wg.cName AS Warengruppe
                FROM tArtikel a
                LEFT JOIN tArtikelBeschreibung ab ON a.kArtikel = ab.kArtikel AND ab.kSprache = 1 AND ab.kPlattform = 1
                LEFT JOIN tHersteller h ON a.kHersteller = h.kHersteller
                LEFT JOIN tSteuerklasse sk ON a.kSteuerklasse = sk.kSteuerklasse
                LEFT JOIN tWarengruppe wg ON a.kWarengruppe = wg.kWarengruppe
                LEFT JOIN tlagerbestand lb ON a.kArtikel = lb.kArtikel
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

        /// <summary>
        /// Kundengruppen-Preise fuer einen Artikel laden (alle Kundengruppen, auch ohne Preise)
        /// </summary>
        public async Task<IEnumerable<KundengruppePreis>> GetArtikelKundengruppenPreiseAsync(int kArtikel)
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<KundengruppePreis>(@"
                SELECT
                    kg.kKundenGruppe AS KKundengruppe,
                    kg.cName AS CKundengruppe,
                    ISNULL(kg.fRabatt, 0) AS FRabatt,
                    ISNULL(pd.nAnzahlAb, 0) AS NAnzahlAb,
                    ISNULL(pd.fNettoPreis, 0) AS FNettoPreis,
                    ISNULL(pd.fProzent, 0) AS FProzent,
                    p.kPreis AS KPreis
                FROM dbo.tKundenGruppe kg
                LEFT JOIN dbo.tPreis p ON p.kKundenGruppe = kg.kKundenGruppe AND p.kArtikel = @KArtikel
                LEFT JOIN dbo.tPreisDetail pd ON p.kPreis = pd.kPreis AND pd.nAnzahlAb = 0
                ORDER BY kg.cName",
                new { KArtikel = kArtikel });
        }

        /// <summary>
        /// Staffelpreise fuer einen Artikel und eine Kundengruppe laden
        /// </summary>
        public async Task<IEnumerable<StaffelpreisDetail>> GetArtikelStaffelpreiseAsync(int kArtikel, int kKundenGruppe)
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<StaffelpreisDetail>(@"
                SELECT
                    pd.kPreis AS KPreis,
                    pd.nAnzahlAb AS NAnzahlAb,
                    pd.fNettoPreis AS FNettoPreis,
                    pd.fProzent AS FProzent
                FROM dbo.tPreis p
                JOIN dbo.tPreisDetail pd ON p.kPreis = pd.kPreis
                WHERE p.kArtikel = @KArtikel AND p.kKundenGruppe = @KKundenGruppe
                ORDER BY pd.nAnzahlAb",
                new { KArtikel = kArtikel, KKundenGruppe = kKundenGruppe });
        }

        /// <summary>
        /// Staffelpreise fuer einen Artikel und eine Kundengruppe speichern
        /// </summary>
        public async Task SaveStaffelpreiseAsync(int kArtikel, int kKundenGruppe, IEnumerable<StaffelpreisDetail> staffelpreise)
        {
            var conn = await GetConnectionAsync();
            using var tx = conn.BeginTransaction();

            try
            {
                // Pruefen ob Preis-Eintrag existiert
                var kPreis = await conn.QueryFirstOrDefaultAsync<int?>(
                    "SELECT kPreis FROM dbo.tPreis WHERE kArtikel = @kArtikel AND kKundenGruppe = @kKundenGruppe",
                    new { kArtikel, kKundenGruppe }, tx);

                var staffelList = staffelpreise.Where(s => s.FNettoPreis > 0 || s.FProzent != 0).ToList();

                if (staffelList.Any())
                {
                    if (!kPreis.HasValue)
                    {
                        // Neuen Preis-Eintrag anlegen
                        kPreis = await conn.QuerySingleAsync<int>(@"
                            INSERT INTO dbo.tPreis (kArtikel, kKundenGruppe, kShop, kKunde)
                            OUTPUT INSERTED.kPreis
                            VALUES (@kArtikel, @kKundenGruppe, 0, 0)",
                            new { kArtikel, kKundenGruppe }, tx);
                    }

                    // Alte Staffelpreise loeschen
                    await conn.ExecuteAsync("DELETE FROM dbo.tPreisDetail WHERE kPreis = @kPreis", new { kPreis = kPreis.Value }, tx);

                    // Neue Staffelpreise einfuegen
                    foreach (var staffel in staffelList)
                    {
                        await conn.ExecuteAsync(@"
                            INSERT INTO dbo.tPreisDetail (kPreis, nAnzahlAb, fNettoPreis, fProzent)
                            VALUES (@kPreis, @nAnzahlAb, @fNettoPreis, @fProzent)",
                            new { kPreis = kPreis.Value, nAnzahlAb = staffel.NAnzahlAb, fNettoPreis = staffel.FNettoPreis, fProzent = staffel.FProzent }, tx);
                    }
                }
                else if (kPreis.HasValue)
                {
                    // Preis komplett loeschen wenn keine Staffelpreise mehr
                    await conn.ExecuteAsync("DELETE FROM dbo.tPreisDetail WHERE kPreis = @kPreis", new { kPreis = kPreis.Value }, tx);
                    await conn.ExecuteAsync("DELETE FROM dbo.tPreis WHERE kPreis = @kPreis", new { kPreis = kPreis.Value }, tx);
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Kundengruppen-Preise fuer einen Artikel speichern
        /// </summary>
        public async Task SaveArtikelKundengruppenPreiseAsync(int kArtikel, IEnumerable<dynamic> preise)
        {
            var conn = await GetConnectionAsync();
            using var tx = conn.BeginTransaction();

            try
            {
                foreach (var preis in preise)
                {
                    int kKundengruppe = (int)preis.KKundengruppe;
                    decimal? fNettoPreis = preis.FNettoPreis as decimal?;
                    decimal? fProzent = preis.FProzent as decimal?;

                    // Pruefen ob Preis existiert
                    var kPreis = await conn.QueryFirstOrDefaultAsync<int?>(
                        "SELECT kPreis FROM dbo.tPreis WHERE kArtikel = @kArtikel AND kKundenGruppe = @kKundengruppe",
                        new { kArtikel, kKundengruppe }, tx);

                    if (fNettoPreis.HasValue || fProzent.HasValue)
                    {
                        if (kPreis.HasValue)
                        {
                            // Update existierenden Preis
                            await conn.ExecuteAsync(@"
                                UPDATE pd SET fNettoPreis = @fNettoPreis, fProzent = @fProzent
                                FROM dbo.tPreisDetail pd
                                WHERE pd.kPreis = @kPreis AND pd.nAnzahlAb = 0",
                                new { kPreis = kPreis.Value, fNettoPreis = fNettoPreis ?? 0, fProzent = fProzent ?? 0 }, tx);
                        }
                        else
                        {
                            // Neuen Preis anlegen
                            var newKPreis = await conn.QuerySingleAsync<int>(@"
                                INSERT INTO dbo.tPreis (kArtikel, kKundenGruppe)
                                OUTPUT INSERTED.kPreis
                                VALUES (@kArtikel, @kKundengruppe)",
                                new { kArtikel, kKundengruppe }, tx);

                            await conn.ExecuteAsync(@"
                                INSERT INTO dbo.tPreisDetail (kPreis, nAnzahlAb, fNettoPreis, fProzent)
                                VALUES (@kPreis, 0, @fNettoPreis, @fProzent)",
                                new { kPreis = newKPreis, fNettoPreis = fNettoPreis ?? 0, fProzent = fProzent ?? 0 }, tx);
                        }
                    }
                    else if (kPreis.HasValue)
                    {
                        // Preis loeschen wenn keine Werte mehr
                        await conn.ExecuteAsync("DELETE FROM dbo.tPreisDetail WHERE kPreis = @kPreis", new { kPreis = kPreis.Value }, tx);
                        await conn.ExecuteAsync("DELETE FROM dbo.tPreis WHERE kPreis = @kPreis", new { kPreis = kPreis.Value }, tx);
                    }
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public class KundengruppePreis
        {
            public int KKundengruppe { get; set; }
            public string? CKundengruppe { get; set; }
            public decimal FRabatt { get; set; }
            public int NAnzahlAb { get; set; }
            public decimal FNettoPreis { get; set; }
            public decimal FProzent { get; set; }
            public int? KPreis { get; set; }
        }

        public class StaffelpreisDetail
        {
            public int? KPreis { get; set; }
            public int NAnzahlAb { get; set; }
            public decimal FNettoPreis { get; set; }
            public decimal FProzent { get; set; }
        }

        #region Kundeneinzelpreise

        /// <summary>
        /// Kundeneinzelpreise fuer einen Artikel laden
        /// </summary>
        public async Task<IEnumerable<KundeneinzelpreisInfo>> GetArtikelKundeneinzelpreiseAsync(int kArtikel)
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<KundeneinzelpreisInfo>(@"
                SELECT
                    p.kPreis AS KPreis,
                    p.kKunde AS KKunde,
                    k.cKundenNr AS CKundenNr,
                    ISNULL(a.cFirma, '') AS CFirma,
                    ISNULL(a.cVorname, '') AS CVorname,
                    ISNULL(a.cName, '') AS CNachname,
                    ISNULL(pd.fNettoPreis, 0) AS FNettoPreis,
                    ISNULL(pd.fProzent, 0) AS FProzent
                FROM dbo.tPreis p
                JOIN dbo.tKunde k ON p.kKunde = k.kKunde
                JOIN dbo.tAdresse a ON k.kKunde = a.kKunde AND a.nStandard = 1
                LEFT JOIN dbo.tPreisDetail pd ON p.kPreis = pd.kPreis AND pd.nAnzahlAb = 0
                WHERE p.kArtikel = @KArtikel AND p.kKunde > 0
                ORDER BY a.cFirma, a.cName",
                new { KArtikel = kArtikel });
        }

        /// <summary>
        /// Kundeneinzelpreis hinzufuegen oder aktualisieren (inkl. Staffelpreise)
        /// </summary>
        public async Task SaveKundeneinzelpreisAsync(int kArtikel, int kKunde, decimal? fNettoPreis, decimal? fProzent, List<StaffelpreisDto>? staffelpreise = null)
        {
            var conn = await GetConnectionAsync();
            using var tx = conn.BeginTransaction();

            try
            {
                // Pruefen ob Preis existiert
                var kPreis = await conn.QueryFirstOrDefaultAsync<int?>(
                    "SELECT kPreis FROM dbo.tPreis WHERE kArtikel = @kArtikel AND kKunde = @kKunde",
                    new { kArtikel, kKunde }, tx);

                bool hatPreise = (fNettoPreis.HasValue && fNettoPreis.Value > 0) ||
                                 (fProzent.HasValue && fProzent.Value != 0) ||
                                 (staffelpreise != null && staffelpreise.Any());

                if (hatPreise)
                {
                    int preisId;
                    if (kPreis.HasValue)
                    {
                        preisId = kPreis.Value;
                        // Alte PreisDetails loeschen (werden neu eingefuegt)
                        await conn.ExecuteAsync("DELETE FROM dbo.tPreisDetail WHERE kPreis = @kPreis", new { kPreis = preisId }, tx);
                    }
                    else
                    {
                        // Neuen Preis anlegen (kKundenGruppe=0 fuer Einzelkundenpreis)
                        preisId = await conn.QuerySingleAsync<int>(@"
                            INSERT INTO dbo.tPreis (kArtikel, kKundenGruppe, kShop, kKunde)
                            OUTPUT INSERTED.kPreis
                            VALUES (@kArtikel, 0, 0, @kKunde)",
                            new { kArtikel, kKunde }, tx);
                    }

                    // Grundpreis (nAnzahlAb=0) einfuegen
                    await conn.ExecuteAsync(@"
                        INSERT INTO dbo.tPreisDetail (kPreis, nAnzahlAb, fNettoPreis, fProzent)
                        VALUES (@kPreis, 0, @fNettoPreis, @fProzent)",
                        new { kPreis = preisId, fNettoPreis = fNettoPreis ?? 0, fProzent = fProzent ?? 0 }, tx);

                    // Staffelpreise einfuegen
                    if (staffelpreise != null)
                    {
                        foreach (var staffel in staffelpreise.Where(s => s.NAnzahlAb > 0))
                        {
                            await conn.ExecuteAsync(@"
                                INSERT INTO dbo.tPreisDetail (kPreis, nAnzahlAb, fNettoPreis, fProzent)
                                VALUES (@kPreis, @nAnzahlAb, @fNettoPreis, @fProzent)",
                                new { kPreis = preisId, nAnzahlAb = staffel.NAnzahlAb, fNettoPreis = staffel.FNettoPreis, fProzent = staffel.FProzent }, tx);
                        }
                    }
                }
                else if (kPreis.HasValue)
                {
                    // Preis loeschen
                    await conn.ExecuteAsync("DELETE FROM dbo.tPreisDetail WHERE kPreis = @kPreis", new { kPreis = kPreis.Value }, tx);
                    await conn.ExecuteAsync("DELETE FROM dbo.tPreis WHERE kPreis = @kPreis", new { kPreis = kPreis.Value }, tx);
                }

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Kundeneinzelpreis loeschen
        /// </summary>
        public async Task DeleteKundeneinzelpreisAsync(int kArtikel, int kKunde)
        {
            var conn = await GetConnectionAsync();
            var kPreis = await conn.QueryFirstOrDefaultAsync<int?>(
                "SELECT kPreis FROM dbo.tPreis WHERE kArtikel = @kArtikel AND kKunde = @kKunde",
                new { kArtikel, kKunde });

            if (kPreis.HasValue)
            {
                await conn.ExecuteAsync("DELETE FROM dbo.tPreisDetail WHERE kPreis = @kPreis", new { kPreis = kPreis.Value });
                await conn.ExecuteAsync("DELETE FROM dbo.tPreis WHERE kPreis = @kPreis", new { kPreis = kPreis.Value });
            }
        }

        public class KundeneinzelpreisInfo
        {
            public int KPreis { get; set; }
            public int KKunde { get; set; }
            public string CKundenNr { get; set; } = "";
            public string CFirma { get; set; } = "";
            public string CVorname { get; set; } = "";
            public string CNachname { get; set; } = "";
            public decimal FNettoPreis { get; set; }
            public decimal FProzent { get; set; }
        }

        public class StaffelpreisDto
        {
            public int NAnzahlAb { get; set; }
            public decimal FNettoPreis { get; set; }
            public decimal FProzent { get; set; }
        }

        public class KundenStaffelpreisInfo
        {
            public int NAnzahlAb { get; set; }
            public decimal FNettoPreis { get; set; }
            public decimal FProzent { get; set; }
        }

        /// <summary>
        /// Staffelpreise fuer einen Kunden und Artikel laden
        /// </summary>
        public async Task<IEnumerable<KundenStaffelpreisInfo>> GetKundenStaffelpreiseAsync(int kArtikel, int kKunde)
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<KundenStaffelpreisInfo>(@"
                SELECT pd.nAnzahlAb AS NAnzahlAb, pd.fNettoPreis AS FNettoPreis, pd.fProzent AS FProzent
                FROM dbo.tPreisDetail pd
                JOIN dbo.tPreis p ON pd.kPreis = p.kPreis
                WHERE p.kArtikel = @kArtikel AND p.kKunde = @kKunde
                ORDER BY pd.nAnzahlAb",
                new { kArtikel, kKunde });
        }

        #endregion

        #region Artikel-Detailmethoden (fuer ArtikelDetailView)

        /// <summary>
        /// Kategorien eines Artikels laden
        /// </summary>
        public async Task<IEnumerable<ArtikelKategorieZuweisung>> GetArtikelKategorienAsync(int kArtikel)
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<ArtikelKategorieZuweisung>(@"
                SELECT
                    ka.kKategorieArtikel, ka.kArtikel, ka.kKategorie,
                    ks.cName AS CKategorieName,
                    ks.cName AS CKategoriePfad
                FROM dbo.tkategorieartikel ka
                JOIN dbo.tkategorie k ON ka.kKategorie = k.kKategorie
                LEFT JOIN dbo.tKategorieSprache ks ON k.kKategorie = ks.kKategorie AND ks.kSprache = 1 AND ks.kPlattform = 1
                WHERE ka.kArtikel = @KArtikel
                ORDER BY ks.cName",
                new { KArtikel = kArtikel });
        }

        public class ArtikelKategorieZuweisung
        {
            public int KKategorieArtikel { get; set; }
            public int KArtikel { get; set; }
            public int KKategorie { get; set; }
            public string? CKategorieName { get; set; }
            public string? CKategoriePfad { get; set; }
        }

        /// <summary>
        /// Alle Kategorien laden (fuer Kategorie-Auswahl)
        /// </summary>
        public async Task<IEnumerable<KategorieInfo>> GetKategorienAsync()
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<KategorieInfo>(@"
                SELECT
                    k.kKategorie AS KKategorie,
                    k.kOberKategorie AS KOberKategorie,
                    ks.cName AS CName,
                    k.nSort AS NSort
                FROM dbo.tkategorie k
                LEFT JOIN dbo.tKategorieSprache ks ON k.kKategorie = ks.kKategorie AND ks.kSprache = 1 AND ks.kPlattform = 1
                ORDER BY k.nSort, ks.cName");
        }

        public class KategorieInfo
        {
            public int KKategorie { get; set; }
            public int? KOberKategorie { get; set; }
            public string? CName { get; set; }
            public int NSort { get; set; }
        }

        /// <summary>
        /// Artikel einer Kategorie zuweisen
        /// </summary>
        public async Task AddArtikelKategorieAsync(int kArtikel, int kKategorie)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync(@"
                IF NOT EXISTS (SELECT 1 FROM dbo.tkategorieartikel WHERE kArtikel = @kArtikel AND kKategorie = @kKategorie)
                INSERT INTO dbo.tkategorieartikel (kArtikel, kKategorie) VALUES (@kArtikel, @kKategorie)",
                new { kArtikel, kKategorie });
        }

        /// <summary>
        /// Artikel aus Kategorie entfernen
        /// </summary>
        public async Task RemoveArtikelKategorieAsync(int kArtikel, int kKategorie)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync(
                "DELETE FROM dbo.tkategorieartikel WHERE kArtikel = @kArtikel AND kKategorie = @kKategorie",
                new { kArtikel, kKategorie });
        }

        /// <summary>
        /// Artikelbeschreibung fuer Sprache und Plattform laden
        /// </summary>
        public async Task<ArtikelBeschreibungDetail?> GetArtikelBeschreibungAsync(int kArtikel, int kSprache = 1, int kPlattform = 1)
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryFirstOrDefaultAsync<ArtikelBeschreibungDetail>(@"
                SELECT
                    ab.kArtikel, ab.kSprache, ab.kPlattform,
                    ab.cName, ab.cBeschreibung, ab.cKurzBeschreibung,
                    ab.cTitleTag, ab.cMetaDescription, ab.cMetaKeywords, ab.cUrlPfad
                FROM dbo.tArtikelBeschreibung ab
                WHERE ab.kArtikel = @KArtikel AND ab.kSprache = @KSprache AND ab.kPlattform = @KPlattform",
                new { KArtikel = kArtikel, KSprache = kSprache, KPlattform = kPlattform });
        }

        public class ArtikelBeschreibungDetail
        {
            public int KArtikel { get; set; }
            public int KSprache { get; set; }
            public int KPlattform { get; set; }
            public string? CName { get; set; }
            public string? CBeschreibung { get; set; }
            public string? CKurzBeschreibung { get; set; }
            public string? CTitleTag { get; set; }
            public string? CMetaDescription { get; set; }
            public string? CMetaKeywords { get; set; }
            public string? CUrlPfad { get; set; }
        }

        /// <summary>
        /// Bestaende eines Artikels pro Lager laden
        /// </summary>
        public async Task<IEnumerable<ArtikelBestandDetail>> GetArtikelBestaendeAsync(int kArtikel)
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<ArtikelBestandDetail>(@"
                SELECT
                    wl.kWarenLager AS KWarenLager,
                    wl.cName AS CLagerName,
                    SUM(we.fAnzahlAktuell) AS FAufLager,
                    wp.cKommentar AS CKommentar1,
                    '' AS CKommentar2,
                    wp.cName AS CLagerplatz
                FROM dbo.tWarenLagerEingang we
                JOIN dbo.tWarenLagerPlatz wp ON we.kWarenLagerPlatz = wp.kWarenLagerPlatz
                JOIN dbo.tWarenLager wl ON wp.kWarenLager = wl.kWarenLager
                WHERE we.kArtikel = @KArtikel AND we.fAnzahlAktuell > 0
                GROUP BY wl.kWarenLager, wl.cName, wp.cKommentar, wp.cName
                ORDER BY wl.cName",
                new { KArtikel = kArtikel });
        }

        public class ArtikelBestandDetail
        {
            public int KWarenLager { get; set; }
            public string? CLagerName { get; set; }
            public decimal FAufLager { get; set; }
            public string? CKommentar1 { get; set; }
            public string? CKommentar2 { get; set; }
            public string? CLagerplatz { get; set; }
        }

        /// <summary>
        /// Chargen eines Artikels laden
        /// </summary>
        public async Task<IEnumerable<ArtikelChargeDetail>> GetArtikelChargenAsync(int kArtikel)
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<ArtikelChargeDetail>(@"
                SELECT
                    we.cChargenNr AS CChargenNr,
                    we.dMHD AS DMHD,
                    SUM(we.fAnzahlAktuell) AS FBestand,
                    wl.cName AS CLagerName
                FROM dbo.tWarenLagerEingang we
                JOIN dbo.tWarenLagerPlatz wp ON we.kWarenLagerPlatz = wp.kWarenLagerPlatz
                JOIN dbo.tWarenLager wl ON wp.kWarenLager = wl.kWarenLager
                WHERE we.kArtikel = @KArtikel
                  AND we.cChargenNr IS NOT NULL AND we.cChargenNr <> ''
                  AND we.fAnzahlAktuell > 0
                GROUP BY we.cChargenNr, we.dMHD, wl.cName
                ORDER BY we.dMHD, we.cChargenNr",
                new { KArtikel = kArtikel });
        }

        public class ArtikelChargeDetail
        {
            public string? CChargenNr { get; set; }
            public DateTime? DMHD { get; set; }
            public decimal FBestand { get; set; }
            public string? CLagerName { get; set; }
        }

        /// <summary>
        /// Seriennummern eines Artikels laden
        /// </summary>
        public async Task<IEnumerable<ArtikelSeriennummerDetail>> GetArtikelSeriennummernAsync(int kArtikel)
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<ArtikelSeriennummerDetail>(@"
                SELECT
                    sn.kSerialNumber AS KSerialNumber,
                    sn.cSerialNumber AS CSeriennummer,
                    wl.cName AS CLagerName,
                    l.cFirma AS CLieferant,
                    '' AS CBeschreibung1
                FROM dbo.tSerialNumber sn
                JOIN dbo.tWarenLager wl ON sn.kWarenlager = wl.kWarenLager
                LEFT JOIN dbo.tWarenLagerEingang we ON sn.kWarenlagerEingang = we.kWarenLagerEingang
                LEFT JOIN dbo.tLieferantenBestellungPos lbp ON we.kLieferantenBestellungPos = lbp.kLieferantenBestellungPos
                LEFT JOIN dbo.tLieferantenBestellung lb ON lbp.kLieferantenBestellung = lb.kLieferantenBestellung
                LEFT JOIN dbo.tLieferant l ON lb.kLieferant = l.kLieferant
                WHERE sn.kArtikel = @KArtikel AND sn.nIsActive = 1
                ORDER BY sn.cSerialNumber",
                new { KArtikel = kArtikel });
        }

        public class ArtikelSeriennummerDetail
        {
            public int KSerialNumber { get; set; }
            public string? CSeriennummer { get; set; }
            public string? CLagerName { get; set; }
            public string? CLieferant { get; set; }
            public string? CBeschreibung1 { get; set; }
        }

        /// <summary>
        /// Lagerbestand-Summen eines Artikels laden (aus tlagerbestand)
        /// </summary>
        public async Task<ArtikelLagerbestandSumme> GetArtikelLagerbestandSummeAsync(int kArtikel)
        {
            var conn = await GetConnectionAsync();
            var result = await conn.QueryFirstOrDefaultAsync<ArtikelLagerbestandSumme>(@"
                SELECT
                    ISNULL(fLagerbestand, 0) AS FAufLager,
                    ISNULL(fInAuftraegen, 0) AS FReserviert,
                    ISNULL(fZulauf, 0) AS FImZulauf,
                    ISNULL(fVerfuegbar, 0) AS FVerfuegbar,
                    ISNULL(fAufEinkaufsliste, 0) AS FAufEinkaufsliste,
                    ISNULL(fVerfuegbarGesperrt, 0) AS FGesperrt
                FROM dbo.tlagerbestand
                WHERE kArtikel = @KArtikel",
                new { KArtikel = kArtikel });

            return result ?? new ArtikelLagerbestandSumme();
        }

        public class ArtikelLagerbestandSumme
        {
            public decimal FAufLager { get; set; }
            public decimal FReserviert { get; set; }
            public decimal FImZulauf { get; set; }
            public decimal FVerfuegbar { get; set; }
            public decimal FAufEinkaufsliste { get; set; }
            public decimal FGesperrt { get; set; }
        }

        /// <summary>
        /// Lieferanten eines Artikels laden
        /// </summary>
        public async Task<IEnumerable<ArtikelLieferantDetail>> GetArtikelLieferantenAsync(int kArtikel)
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<ArtikelLieferantDetail>(@"
                SELECT
                    la.kLiefArtikel, la.tArtikel_kArtikel AS KArtikel, la.tLieferant_kLieferant AS KLieferant,
                    l.cFirma AS CLieferantName, l.cLiefNr AS CLieferantNr,
                    la.cLiefArtNr AS CLiefArtNr, la.cName,
                    la.fEKNetto, la.fMwSt, la.cVPEEinheit, la.nVPEMenge,
                    la.nMindestAbnahme, la.nLieferzeit,
                    la.nStandard, la.fLagerbestand, la.cSonstiges
                FROM dbo.tliefartikel la
                JOIN dbo.tLieferant l ON la.tLieferant_kLieferant = l.kLieferant
                WHERE la.tArtikel_kArtikel = @KArtikel
                ORDER BY la.nStandard DESC, l.cFirma",
                new { KArtikel = kArtikel });
        }

        public class ArtikelLieferantDetail
        {
            public int KLiefArtikel { get; set; }
            public int KArtikel { get; set; }
            public int KLieferant { get; set; }
            public string? CLieferantName { get; set; }
            public string? CLieferantNr { get; set; }
            public string? CLiefArtNr { get; set; }
            public string? CName { get; set; }
            public decimal? FEKNetto { get; set; }
            public decimal? FMwSt { get; set; }
            public string? CVPEEinheit { get; set; }
            public int? NVPEMenge { get; set; }
            public int? NMindestAbnahme { get; set; }
            public int? NLieferzeit { get; set; }
            public int NStandard { get; set; }
            public decimal? FLagerbestand { get; set; }
            public string? CSonstiges { get; set; }

            // UI-Eigenschaften
            public string? Kontakt => CLieferantNr;
            public string? Firmenname => CLieferantName;
        }

        /// <summary>
        /// Lagerbewegungen/History eines Artikels laden
        /// </summary>
        public async Task<IEnumerable<ArtikelHistoryEntry>> GetArtikelHistoryAsync(int kArtikel, int limit = 50)
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<ArtikelHistoryEntry>(@"
                SELECT TOP (@Limit)
                    lb.dErstellt AS Datum,
                    lb.fAnzahl AS Anzahl,
                    CASE
                        WHEN lb.kWarenLagerPlatzBuchung > 0 THEN 'Umbuchung'
                        WHEN lb.fAnzahl > 0 THEN 'Wareneingang'
                        ELSE 'Warenausgang'
                    END AS Art,
                    ISNULL(b.cBestellNr, '') AS AuftragNr,
                    lb.cKommentar AS Kommentar,
                    lb.cMHD AS MHD,
                    lb.cChargeNr AS Charge,
                    ISNULL(k.cKundenNr, '') AS KundenNr,
                    ISNULL(a.cFirma, ISNULL(a.cName, '')) AS Firma
                FROM dbo.tLagerBuchung lb
                LEFT JOIN dbo.tBestellung b ON lb.kBestellung = b.kBestellung
                LEFT JOIN dbo.tKunde k ON b.kKunde = k.kKunde
                LEFT JOIN dbo.tAdresse a ON k.kKunde = a.kKunde AND a.nStandard = 1
                WHERE lb.kArtikel = @KArtikel
                ORDER BY lb.dErstellt DESC",
                new { KArtikel = kArtikel, Limit = limit });
        }

        public class ArtikelHistoryEntry
        {
            public DateTime Datum { get; set; }
            public decimal Anzahl { get; set; }
            public string? Art { get; set; }
            public string? AuftragNr { get; set; }
            public string? Kommentar { get; set; }
            public string? MHD { get; set; }
            public string? Charge { get; set; }
            public string? KundenNr { get; set; }
            public string? Firma { get; set; }
        }

        /// <summary>
        /// Lieferant zu einem Artikel hinzufuegen
        /// </summary>
        public async Task AddArtikelLieferantAsync(int kArtikel, int kLieferant)
        {
            var conn = await GetConnectionAsync();

            // Pruefen ob bereits vorhanden
            var exists = await conn.QueryFirstOrDefaultAsync<int?>(
                "SELECT kLiefArtikel FROM dbo.tliefartikel WHERE tArtikel_kArtikel = @kArtikel AND tLieferant_kLieferant = @kLieferant",
                new { kArtikel, kLieferant });

            if (exists.HasValue)
                throw new InvalidOperationException("Lieferant ist bereits diesem Artikel zugeordnet.");

            await conn.ExecuteAsync(@"
                INSERT INTO dbo.tliefartikel (tArtikel_kArtikel, tLieferant_kLieferant, nStandard)
                VALUES (@kArtikel, @kLieferant, 0)",
                new { kArtikel, kLieferant });
        }

        /// <summary>
        /// Standardlieferant fuer einen Artikel setzen
        /// </summary>
        public async Task SetArtikelStandardLieferantAsync(int kArtikel, int kLieferant)
        {
            var conn = await GetConnectionAsync();
            using var tx = conn.BeginTransaction();

            try
            {
                // Alle auf nicht-Standard setzen
                await conn.ExecuteAsync(
                    "UPDATE dbo.tliefartikel SET nStandard = 0 WHERE tArtikel_kArtikel = @kArtikel",
                    new { kArtikel }, tx);

                // Ausgewaehlten auf Standard setzen
                await conn.ExecuteAsync(
                    "UPDATE dbo.tliefartikel SET nStandard = 1 WHERE tArtikel_kArtikel = @kArtikel AND tLieferant_kLieferant = @kLieferant",
                    new { kArtikel, kLieferant }, tx);

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Lieferant von einem Artikel entfernen
        /// </summary>
        public async Task RemoveArtikelLieferantAsync(int kArtikel, int kLieferant)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync(
                "DELETE FROM dbo.tliefartikel WHERE tArtikel_kArtikel = @kArtikel AND tLieferant_kLieferant = @kLieferant",
                new { kArtikel, kLieferant });
        }

        /// <summary>
        /// Bilder eines Artikels laden
        /// </summary>
        public async Task<IEnumerable<ArtikelBildDetail>> GetArtikelBilderAsync(int kArtikel)
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<ArtikelBildDetail>(@"
                SELECT
                    ap.kArtikelbildPlattform AS KArtikelPict, ap.kArtikel, ap.kBild, ap.nNr,
                    b.cQuelle AS CPfad, ap.cBildname AS CDateiname, b.nBreite, b.nHoehe, b.nDateigroesse AS NGroesse,
                    CASE WHEN ap.nNr = 1 THEN 1 ELSE 0 END AS NIstHauptbild
                FROM dbo.tArtikelbildPlattform ap
                JOIN dbo.tBild b ON ap.kBild = b.kBild
                WHERE ap.kArtikel = @KArtikel
                ORDER BY ap.nNr",
                new { KArtikel = kArtikel });
        }

        public class ArtikelBildDetail
        {
            public int KArtikelPict { get; set; }
            public int KArtikel { get; set; }
            public int KBild { get; set; }
            public int NNr { get; set; }
            public string? CPfad { get; set; }
            public string? CDateiname { get; set; }
            public int? NBreite { get; set; }
            public int? NHoehe { get; set; }
            public int? NGroesse { get; set; }
            public int NIstHauptbild { get; set; }
        }

        /// <summary>
        /// Artikelbild loeschen
        /// </summary>
        public async Task DeleteArtikelBildAsync(int kBild)
        {
            var conn = await GetConnectionAsync();
            using var tx = conn.BeginTransaction();

            try
            {
                // Erst die Zuordnung loeschen
                await conn.ExecuteAsync("DELETE FROM dbo.tArtikelbildPlattform WHERE kBild = @kBild", new { kBild }, tx);
                // Dann das Bild selbst
                await conn.ExecuteAsync("DELETE FROM dbo.tBild WHERE kBild = @kBild", new { kBild }, tx);
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Attribute eines Artikels laden (ArtikelAttribut-Werte)
        /// </summary>
        public async Task<IEnumerable<ArtikelAttributWert>> GetArtikelAttributeByArtikelAsync(int kArtikel)
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<ArtikelAttributWert>(@"
                SELECT
                    aa.kArtikelAttribut, aa.kArtikel, aa.kAttribut,
                    asp.cName AS CAttributName,
                    COALESCE(aas.cWertVarchar, CAST(aas.nWertInt AS NVARCHAR), CAST(aas.fWertDecimal AS NVARCHAR), CAST(aas.dWertDateTime AS NVARCHAR)) AS CWert
                FROM dbo.tArtikelAttribut aa
                JOIN dbo.tAttributSprache asp ON aa.kAttribut = asp.kAttribut AND asp.kSprache = 1
                LEFT JOIN dbo.tArtikelAttributSprache aas ON aa.kArtikelAttribut = aas.kArtikelAttribut AND aas.kSprache = 1
                WHERE aa.kArtikel = @KArtikel
                ORDER BY asp.cName",
                new { KArtikel = kArtikel });
        }

        public class ArtikelAttributWert
        {
            public int KArtikelAttribut { get; set; }
            public int KArtikel { get; set; }
            public int KAttribut { get; set; }
            public string? CAttributName { get; set; }
            public string? CWert { get; set; }
        }

        /// <summary>
        /// Merkmale eines Artikels laden
        /// </summary>
        public async Task<IEnumerable<ArtikelMerkmalWert>> GetArtikelMerkmaleAsync(int kArtikel)
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<ArtikelMerkmalWert>(@"
                SELECT
                    am.kMerkmal, am.kMerkmalWert, am.kArtikel,
                    ms.cName AS CMerkmalName, mws.cWert AS CMerkmalWert
                FROM dbo.tArtikelMerkmal am
                JOIN dbo.tMerkmalSprache ms ON am.kMerkmal = ms.kMerkmal AND ms.kSprache = 1
                LEFT JOIN dbo.tMerkmalWertSprache mws ON am.kMerkmalWert = mws.kMerkmalWert AND mws.kSprache = 1
                WHERE am.kArtikel = @KArtikel
                ORDER BY ms.cName",
                new { KArtikel = kArtikel });
        }

        public class ArtikelMerkmalWert
        {
            public int KMerkmal { get; set; }
            public int? KMerkmalWert { get; set; }
            public int KArtikel { get; set; }
            public string? CMerkmalName { get; set; }
            public string? CMerkmalWert { get; set; }
        }

        /// <summary>
        /// Stueckliste eines Artikels laden (kVaterArtikel = Parent, kArtikel = Komponente)
        /// </summary>
        public async Task<IEnumerable<StuecklistePosition>> GetArtikelStuecklisteAsync(int kArtikel)
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<StuecklistePosition>(@"
                SELECT
                    s.kStueckliste, s.kVaterArtikel AS KArtikel, s.kArtikel AS KKomponente, s.fAnzahl AS FMenge,
                    ab.cName AS CKomponenteName, a.cArtNr AS CKomponenteArtNr,
                    ISNULL(pd.fNettoPreis, 0) AS FNettoVK,
                    ISNULL(lb.fLagerbestand, 0) AS FLagerbestand
                FROM dbo.tStueckliste s
                JOIN dbo.tartikel a ON s.kArtikel = a.kArtikel
                LEFT JOIN dbo.tArtikelBeschreibung ab ON a.kArtikel = ab.kArtikel AND ab.kSprache = 1 AND ab.kPlattform = 1
                LEFT JOIN dbo.tPreis p ON a.kArtikel = p.kArtikel AND p.kKundenGruppe = 0
                LEFT JOIN dbo.tPreisDetail pd ON p.kPreis = pd.kPreis AND pd.nAnzahlAb = 0
                LEFT JOIN dbo.tlagerbestand lb ON a.kArtikel = lb.kArtikel
                WHERE s.kVaterArtikel = @KArtikel
                ORDER BY ab.cName",
                new { KArtikel = kArtikel });
        }

        public class StuecklistePosition
        {
            public int KStueckliste { get; set; }
            public int KArtikel { get; set; }
            public int KKomponente { get; set; }
            public decimal FMenge { get; set; }
            public string? CKomponenteName { get; set; }
            public string? CKomponenteArtNr { get; set; }
            public decimal FNettoVK { get; set; }
            public decimal FLagerbestand { get; set; }
        }

        /// <summary>
        /// Artikel zu Stueckliste hinzufuegen
        /// </summary>
        public async Task AddArtikelZuStuecklisteAsync(int kArtikel, int kKomponente, decimal fMenge)
        {
            var conn = await GetConnectionAsync();

            // Pruefen ob bereits vorhanden
            var exists = await conn.QueryFirstOrDefaultAsync<int?>(
                "SELECT kStueckliste FROM dbo.tStueckliste WHERE kVaterArtikel = @kArtikel AND kArtikel = @kKomponente",
                new { kArtikel, kKomponente });

            if (exists.HasValue)
            {
                // Update Menge
                await conn.ExecuteAsync(
                    "UPDATE dbo.tStueckliste SET fAnzahl = fAnzahl + @fMenge WHERE kStueckliste = @kStueckliste",
                    new { kStueckliste = exists.Value, fMenge });
            }
            else
            {
                await conn.ExecuteAsync(@"
                    INSERT INTO dbo.tStueckliste (kVaterArtikel, kArtikel, fAnzahl)
                    VALUES (@kArtikel, @kKomponente, @fMenge)",
                    new { kArtikel, kKomponente, fMenge });
            }
        }

        /// <summary>
        /// Komponente aus Stueckliste entfernen
        /// </summary>
        public async Task RemoveArtikelVonStuecklisteAsync(int kStueckliste)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync(
                "DELETE FROM dbo.tStueckliste WHERE kStueckliste = @kStueckliste",
                new { kStueckliste });
        }

        /// <summary>
        /// Sonderpreise eines Artikels laden
        /// </summary>
        public async Task<IEnumerable<ArtikelSonderpreisInfo>> GetArtikelSonderpreiseAsync(int kArtikel)
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<ArtikelSonderpreisInfo>(@"
                SELECT
                    asp.kArtikelSonderpreis, asp.kArtikel,
                    sp.kKundenGruppe,
                    asp.nAktiv AS NAktiv, asp.nIstDatum AS NIstDatum, asp.nIstAnzahl AS NIstAnzahl,
                    asp.dStart, asp.dEnde, asp.nAnzahl,
                    kg.cName AS CKundengruppe,
                    ISNULL(sp.fNettoPreis, 0) AS FSonderpreisNetto
                FROM dbo.tArtikelSonderpreis asp
                LEFT JOIN dbo.tSonderpreise sp ON asp.kArtikelSonderpreis = sp.kArtikelSonderpreis
                LEFT JOIN dbo.tKundenGruppe kg ON sp.kKundenGruppe = kg.kKundenGruppe
                WHERE asp.kArtikel = @KArtikel
                ORDER BY kg.cName",
                new { KArtikel = kArtikel });
        }

        public class ArtikelSonderpreisInfo
        {
            public int KArtikelSonderpreis { get; set; }
            public int KArtikel { get; set; }
            public int? KKundenGruppe { get; set; }
            public int NAktiv { get; set; }
            public int NIstDatum { get; set; }
            public int NIstAnzahl { get; set; }
            public DateTime? DStart { get; set; }
            public DateTime? DEnde { get; set; }
            public int? NAnzahl { get; set; }
            public string? CKundengruppe { get; set; }
            public decimal FSonderpreisNetto { get; set; }
        }

        #endregion

        public async Task UpdateArtikelAsync(ArtikelDetail artikel)
        {
            var conn = await GetConnectionAsync();

            // Table-Valued Parameter vorbereiten
            var table = new DataTable();
            table.Columns.Add("kArtikel", typeof(int));
            table.Columns.Add("cArtNr", typeof(string));
            table.Columns.Add("cBarcode", typeof(string));
            table.Columns.Add("cHAN", typeof(string));
            table.Columns.Add("cISBN", typeof(string));
            table.Columns.Add("fVKNetto", typeof(decimal));
            table.Columns.Add("fUVP", typeof(decimal));
            table.Columns.Add("fEKNetto", typeof(decimal));
            table.Columns.Add("nMidestbestand", typeof(decimal));
            table.Columns.Add("cLagerArtikel", typeof(string));
            table.Columns.Add("cLagerAktiv", typeof(string));
            table.Columns.Add("cLagerKleinerNull", typeof(string));
            table.Columns.Add("fGewicht", typeof(decimal));
            table.Columns.Add("fBreite", typeof(decimal));
            table.Columns.Add("fHoehe", typeof(decimal));
            table.Columns.Add("fLaenge", typeof(decimal));
            table.Columns.Add("fPackeinheit", typeof(decimal));
            table.Columns.Add("kSteuerklasse", typeof(int));
            table.Columns.Add("kHersteller", typeof(int));
            table.Columns.Add("kWarengruppe", typeof(int));
            table.Columns.Add("kVersandklasse", typeof(int));
            table.Columns.Add("cAktiv", typeof(string));
            table.Columns.Add("cTopArtikel", typeof(string));
            table.Columns.Add("cNeu", typeof(string));
            table.Columns.Add("cTeilbar", typeof(string));
            table.Columns.Add("nMHD", typeof(byte));
            table.Columns.Add("nCharge", typeof(byte));
            table.Columns.Add("nSeriennr", typeof(byte));
            table.Columns.Add("cTaric", typeof(string));
            table.Columns.Add("cHerkunftsland", typeof(string));
            table.Columns.Add("cSuchbegriffe", typeof(string));
            table.Columns.Add("nSort", typeof(int));
            table.Columns.Add("cName", typeof(string));
            table.Columns.Add("cBeschreibung", typeof(string));
            table.Columns.Add("cKurzBeschreibung", typeof(string));
            table.Columns.Add("cUrlPfad", typeof(string));

            var row = table.NewRow();
            row["kArtikel"] = artikel.KArtikel;
            row["cArtNr"] = artikel.CArtNr ?? (object)DBNull.Value;
            row["cBarcode"] = artikel.CBarcode ?? (object)DBNull.Value;
            row["cHAN"] = artikel.CHAN ?? (object)DBNull.Value;
            row["cISBN"] = artikel.CISBN ?? (object)DBNull.Value;
            row["fVKNetto"] = artikel.FVKNetto;
            row["fUVP"] = artikel.FUVP;
            row["fEKNetto"] = artikel.FEKNetto;
            row["nMidestbestand"] = artikel.NMidestbestand;
            row["cLagerArtikel"] = artikel.CLagerArtikel ?? "N";
            row["cLagerAktiv"] = artikel.CLagerAktiv ?? "Y";
            row["cLagerKleinerNull"] = artikel.CLagerKleinerNull ?? "N";
            row["fGewicht"] = artikel.FGewicht;
            row["fBreite"] = artikel.FBreite;
            row["fHoehe"] = artikel.FHoehe;
            row["fLaenge"] = artikel.FLaenge;
            row["fPackeinheit"] = artikel.FPackeinheit > 0 ? artikel.FPackeinheit : 1;
            row["kSteuerklasse"] = artikel.KSteuerklasse ?? (object)DBNull.Value;
            row["kHersteller"] = artikel.KHersteller ?? (object)DBNull.Value;
            row["kWarengruppe"] = artikel.KWarengruppe ?? (object)DBNull.Value;
            row["kVersandklasse"] = artikel.KVersandklasse ?? (object)DBNull.Value;
            row["cAktiv"] = artikel.CAktiv ?? "Y";
            row["cTopArtikel"] = artikel.CTopArtikel ?? "N";
            row["cNeu"] = artikel.CNeu ?? "N";
            row["cTeilbar"] = artikel.CTeilbar ?? "N";
            row["nMHD"] = artikel.NMHD;
            row["nCharge"] = artikel.NCharge;
            row["nSeriennr"] = artikel.NSeriennummernVerfolgung;
            row["cTaric"] = artikel.CTaric ?? (object)DBNull.Value;
            row["cHerkunftsland"] = artikel.CHerkunftsland ?? (object)DBNull.Value;
            row["cSuchbegriffe"] = artikel.CSuchbegriffe ?? (object)DBNull.Value;
            row["nSort"] = artikel.NSort > 0 ? artikel.NSort : (object)DBNull.Value;
            row["cName"] = artikel.Name ?? (object)DBNull.Value;
            row["cBeschreibung"] = artikel.Beschreibung ?? (object)DBNull.Value;
            row["cKurzBeschreibung"] = artikel.KurzBeschreibung ?? (object)DBNull.Value;
            row["cUrlPfad"] = artikel.CSeo ?? (object)DBNull.Value;
            table.Rows.Add(row);

            var param = new DynamicParameters();
            param.Add("@Daten", table.AsTableValuedParameter("NOVVIA.TYPE_ArtikelUpsert"));
            param.Add("@kBenutzer", DBNull.Value);
            param.Add("@cBenutzerName", DBNull.Value);

            await conn.ExecuteAsync("NOVVIA.spArtikelUpdate", param, commandType: CommandType.StoredProcedure);
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

        public async Task<IEnumerable<VersandklasseRef>> GetVersandklassenAsync()
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<VersandklasseRef>("SELECT kVersandklasse, cName FROM tVersandklasse ORDER BY cName");
        }

        public async Task<IEnumerable<WarengruppeRef>> GetWarengruppenAsync()
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<WarengruppeRef>("SELECT kWarengruppe, cName FROM tWarengruppe ORDER BY cName");
        }

        public async Task<int> CreateArtikelAsync(ArtikelDetail artikel)
        {
            var conn = await GetConnectionAsync();

            // Table-Valued Parameter vorbereiten
            var table = new DataTable();
            table.Columns.Add("kArtikel", typeof(int));
            table.Columns.Add("cArtNr", typeof(string));
            table.Columns.Add("cBarcode", typeof(string));
            table.Columns.Add("cHAN", typeof(string));
            table.Columns.Add("cISBN", typeof(string));
            table.Columns.Add("fVKNetto", typeof(decimal));
            table.Columns.Add("fUVP", typeof(decimal));
            table.Columns.Add("fEKNetto", typeof(decimal));
            table.Columns.Add("nMidestbestand", typeof(decimal));
            table.Columns.Add("cLagerArtikel", typeof(string));
            table.Columns.Add("cLagerAktiv", typeof(string));
            table.Columns.Add("cLagerKleinerNull", typeof(string));
            table.Columns.Add("fGewicht", typeof(decimal));
            table.Columns.Add("fBreite", typeof(decimal));
            table.Columns.Add("fHoehe", typeof(decimal));
            table.Columns.Add("fLaenge", typeof(decimal));
            table.Columns.Add("fPackeinheit", typeof(decimal));
            table.Columns.Add("kSteuerklasse", typeof(int));
            table.Columns.Add("kHersteller", typeof(int));
            table.Columns.Add("kWarengruppe", typeof(int));
            table.Columns.Add("kVersandklasse", typeof(int));
            table.Columns.Add("cAktiv", typeof(string));
            table.Columns.Add("cTopArtikel", typeof(string));
            table.Columns.Add("cNeu", typeof(string));
            table.Columns.Add("cTeilbar", typeof(string));
            table.Columns.Add("nMHD", typeof(byte));
            table.Columns.Add("nCharge", typeof(byte));
            table.Columns.Add("nSeriennr", typeof(byte));
            table.Columns.Add("cTaric", typeof(string));
            table.Columns.Add("cHerkunftsland", typeof(string));
            table.Columns.Add("cSuchbegriffe", typeof(string));
            table.Columns.Add("nSort", typeof(int));
            table.Columns.Add("cName", typeof(string));
            table.Columns.Add("cBeschreibung", typeof(string));
            table.Columns.Add("cKurzBeschreibung", typeof(string));
            table.Columns.Add("cUrlPfad", typeof(string));

            var row = table.NewRow();
            row["kArtikel"] = DBNull.Value; // NULL bei Insert
            row["cArtNr"] = string.IsNullOrEmpty(artikel.CArtNr) ? DBNull.Value : artikel.CArtNr;
            row["cBarcode"] = artikel.CBarcode ?? (object)DBNull.Value;
            row["cHAN"] = artikel.CHAN ?? (object)DBNull.Value;
            row["cISBN"] = artikel.CISBN ?? (object)DBNull.Value;
            row["fVKNetto"] = artikel.FVKNetto;
            row["fUVP"] = artikel.FUVP;
            row["fEKNetto"] = artikel.FEKNetto;
            row["nMidestbestand"] = artikel.NMidestbestand;
            row["cLagerArtikel"] = artikel.CLagerArtikel ?? "N";
            row["cLagerAktiv"] = artikel.CLagerAktiv ?? "Y";
            row["cLagerKleinerNull"] = artikel.CLagerKleinerNull ?? "N";
            row["fGewicht"] = artikel.FGewicht;
            row["fBreite"] = artikel.FBreite;
            row["fHoehe"] = artikel.FHoehe;
            row["fLaenge"] = artikel.FLaenge;
            row["fPackeinheit"] = artikel.FPackeinheit > 0 ? artikel.FPackeinheit : 1;
            row["kSteuerklasse"] = artikel.KSteuerklasse ?? (object)DBNull.Value;
            row["kHersteller"] = artikel.KHersteller ?? (object)DBNull.Value;
            row["kWarengruppe"] = artikel.KWarengruppe ?? (object)DBNull.Value;
            row["kVersandklasse"] = artikel.KVersandklasse ?? (object)DBNull.Value;
            row["cAktiv"] = artikel.CAktiv ?? "Y";
            row["cTopArtikel"] = artikel.CTopArtikel ?? "N";
            row["cNeu"] = artikel.CNeu ?? "N";
            row["cTeilbar"] = artikel.CTeilbar ?? "N";
            row["nMHD"] = artikel.NMHD;
            row["nCharge"] = artikel.NCharge;
            row["nSeriennr"] = artikel.NSeriennummernVerfolgung;
            row["cTaric"] = artikel.CTaric ?? (object)DBNull.Value;
            row["cHerkunftsland"] = artikel.CHerkunftsland ?? (object)DBNull.Value;
            row["cSuchbegriffe"] = artikel.CSuchbegriffe ?? (object)DBNull.Value;
            row["nSort"] = artikel.NSort > 0 ? artikel.NSort : (object)DBNull.Value;
            row["cName"] = artikel.Name ?? (object)DBNull.Value;
            row["cBeschreibung"] = artikel.Beschreibung ?? (object)DBNull.Value;
            row["cKurzBeschreibung"] = artikel.KurzBeschreibung ?? (object)DBNull.Value;
            row["cUrlPfad"] = artikel.CSeo ?? (object)DBNull.Value;
            table.Rows.Add(row);

            var param = new DynamicParameters();
            param.Add("@Daten", table.AsTableValuedParameter("NOVVIA.TYPE_ArtikelUpsert"));
            param.Add("@kBenutzer", DBNull.Value);
            param.Add("@cBenutzerName", DBNull.Value);

            var result = await conn.QueryFirstOrDefaultAsync<(int kArtikel, string cArtNr)>(
                "NOVVIA.spArtikelInsert", param, commandType: CommandType.StoredProcedure);

            artikel.KArtikel = result.kArtikel;
            artikel.CArtNr = result.cArtNr;

            return result.kArtikel;
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
                    b.kBestellung, b.cBestellNr, b.cInetBestellNr,
                    CASE WHEN b.dErstellt > '1900-01-02' THEN b.dErstellt ELSE NULL END AS DErstellt,
                    -- JTL Datumslogik: Externes Datum und Wawi-Datum
                    CASE WHEN va.dExternesErstelldatum > '1900-01-02' THEN va.dExternesErstelldatum ELSE NULL END AS DExternesDatum,
                    CASE WHEN va.dErstelltWawi > '1900-01-02' THEN va.dErstelltWawi ELSE NULL END AS DErstelltWawi,
                    -- JTL-native Status berechnet aus nAuftragStatus
                    CASE
                        WHEN b.nStorno = 1 THEN 'Storniert'
                        WHEN va.nAuftragStatus = 255 THEN 'Storniert'
                        WHEN b.dVersandt > '1900-01-02' THEN 'Versendet'
                        WHEN va.nAuftragStatus = 4 THEN 'Abgeschlossen'
                        WHEN va.nAuftragStatus = 3 THEN 'Versendet'
                        WHEN va.nAuftragStatus = 2 THEN 'Versandbereit'
                        WHEN va.nAuftragStatus = 1 THEN 'In Bearbeitung'
                        WHEN va.nAuftragStatus = 0 THEN 'Offen'
                        ELSE 'Offen'
                    END AS CStatus,
                    va.nAuftragStatus AS NAuftragStatus,
                    b.tKunde_kKunde,
                    CASE WHEN b.dVersandt > '1900-01-02' THEN b.dVersandt ELSE NULL END AS DVersandt,
                    CASE WHEN b.dBezahlt > '1900-01-02' THEN b.dBezahlt ELSE NULL END AS DBezahlt,
                    b.cIdentCode, b.nStorno, b.kShop,
                    k.cKundenNr,
                    a.cName AS KundeName, a.cFirma AS KundeFirma,
                    s.cName AS ShopName,
                    b.cAnmerkung AS CAnmerkung,
                    -- Rechnungsadresse
                    re.cFirma AS ReFirma, re.cName AS ReName, re.cVorname AS ReVorname,
                    re.cStrasse AS ReStrasse, re.cPLZ AS RePlz, re.cOrt AS ReOrt, re.cLand AS ReLand,
                    re.cMail AS ReMail, re.cTel AS ReTel,
                    -- Lieferadresse
                    li.cFirma AS LiFirma, li.cName AS LiName, li.cVorname AS LiVorname,
                    li.cStrasse AS LiStrasse, li.cPLZ AS LiPlz, li.cOrt AS LiOrt, li.cLand AS LiLand,
                    ISNULL((SELECT SUM(bp.nAnzahl * bp.fVkNetto * (1 - ISNULL(bp.fRabatt,0)/100)) FROM tbestellpos bp WHERE bp.tBestellung_kBestellung = b.kBestellung), 0) AS GesamtNetto,
                    ISNULL((SELECT SUM(bp.nAnzahl * bp.fVkNetto * (1 - ISNULL(bp.fRabatt,0)/100) * (1 + bp.fMwSt/100)) FROM tbestellpos bp WHERE bp.tBestellung_kBestellung = b.kBestellung), 0) AS GesamtBrutto
                FROM tBestellung b
                LEFT JOIN Verkauf.tAuftrag va ON va.kAuftrag = b.kBestellung
                LEFT JOIN tkunde k ON b.tKunde_kKunde = k.kKunde
                OUTER APPLY (SELECT TOP 1 * FROM tAdresse WHERE kKunde = k.kKunde ORDER BY nStandard DESC, kAdresse) a
                OUTER APPLY (SELECT TOP 1 * FROM Verkauf.tAuftragAdresse WHERE kAuftrag = b.kBestellung AND nTyp = 0) re
                OUTER APPLY (SELECT TOP 1 * FROM Verkauf.tAuftragAdresse WHERE kAuftrag = b.kBestellung AND nTyp = 1) li
                LEFT JOIN tShop s ON b.kShop = s.kShop
                WHERE b.nStorno = 0";

            if (!string.IsNullOrEmpty(status))
            {
                // Status-Filter basierend auf JTL nAuftragStatus
                sql += status switch
                {
                    "Offen" => " AND (va.nAuftragStatus = 0 OR va.nAuftragStatus IS NULL) AND b.nStorno = 0",
                    "In Bearbeitung" => " AND va.nAuftragStatus = 1 AND b.nStorno = 0",
                    "Versandbereit" => " AND va.nAuftragStatus = 2 AND b.nStorno = 0",
                    "Versendet" => " AND (va.nAuftragStatus = 3 OR b.dVersandt > '1900-01-02') AND b.nStorno = 0",
                    "Abgeschlossen" => " AND va.nAuftragStatus = 4 AND b.nStorno = 0",
                    "Storniert" => " AND (b.nStorno = 1 OR va.nAuftragStatus = 255)",
                    _ => ""
                };
            }
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

        // --- Versand-Liste fuer VersandPage ---
        public class VersandItem
        {
            public int KBestellung { get; set; }
            public string? BestellNr { get; set; }
            public DateTime Erstellt { get; set; }
            public string? KundeName { get; set; }
            public string? LieferOrt { get; set; }
            public decimal Gewicht { get; set; }
            public string? VersandartName { get; set; }
            public string? TrackingNr { get; set; }
            public string? VersandDienstleister { get; set; }
            public DateTime? Versandt { get; set; }
            public bool IsSelected { get; set; }
        }

        public async Task<IEnumerable<VersandItem>> GetVersandListeAsync(
            string? suche = null, string? statusFilter = null, DateTime? von = null, DateTime? bis = null, int maxRows = 500)
        {
            var conn = await GetConnectionAsync();

            // Adresse aus Verkauf.tAuftragAdresse (nTyp=1 für Lieferadresse)
            var sql = $@"
                SELECT TOP {maxRows}
                    b.kBestellung AS KBestellung,
                    b.cBestellNr AS BestellNr,
                    b.dErstellt AS Erstellt,
                    ISNULL(NULLIF(la.cFirma, ''), la.cVorname + ' ' + la.cName) AS KundeName,
                    la.cOrt AS LieferOrt,
                    ISNULL(g.Gewicht, 0) AS Gewicht,
                    va.cName AS VersandartName,
                    v.cIdentCode AS TrackingNr,
                    v.cLogistiker AS VersandDienstleister,
                    v.dVersendet AS Versandt
                FROM dbo.tBestellung b
                LEFT JOIN Verkauf.tAuftragAdresse la ON b.kBestellung = la.kAuftrag AND la.nTyp = 1
                LEFT JOIN dbo.tVersandArt va ON b.tVersandArt_kVersandArt = va.kVersandArt
                LEFT JOIN dbo.tLieferschein ls ON ls.kBestellung = b.kBestellung
                LEFT JOIN dbo.tVersand v ON v.kLieferschein = ls.kLieferschein
                LEFT JOIN (
                    SELECT bp.tBestellung_kBestellung, SUM(bp.nAnzahl * ISNULL(art.fGewicht, 0)) AS Gewicht
                    FROM dbo.tbestellpos bp
                    LEFT JOIN tArtikel art ON bp.tArtikel_kArtikel = art.kArtikel
                    GROUP BY bp.tBestellung_kBestellung
                ) g ON g.tBestellung_kBestellung = b.kBestellung
                WHERE b.nStorno = 0";

            // Status-Filter: 0 = zu versenden, 1 = versendet
            if (statusFilter == "0") sql += " AND v.kVersand IS NULL";
            else if (statusFilter == "1") sql += " AND v.kVersand IS NOT NULL";

            if (von.HasValue) sql += " AND b.dErstellt >= @Von";
            if (bis.HasValue) sql += " AND b.dErstellt <= @Bis";

            if (!string.IsNullOrEmpty(suche))
            {
                sql += @" AND (b.cBestellNr LIKE @Suche
                         OR la.cFirma LIKE @Suche
                         OR la.cName LIKE @Suche
                         OR la.cOrt LIKE @Suche)";
            }

            sql += " ORDER BY b.dErstellt DESC";

            return await conn.QueryAsync<VersandItem>(sql, new {
                Suche = $"%{suche}%", Von = von, Bis = bis
            });
        }

        public async Task<BestellungDetail?> GetBestellungByIdAsync(int bestellungId)
        {
            var conn = await GetConnectionAsync();

            var bestellung = await conn.QueryFirstOrDefaultAsync<BestellungDetail>(@"
                SELECT b.kBestellung, b.cBestellNr, b.cInetBestellNr, b.dErstellt, b.dVersandt, b.dBezahlt,
                       b.cIdentCode, b.nStorno, b.kShop, b.tKunde_kKunde, b.cAnmerkung,
                       b.tVersandArt_kVersandArt, b.kZahlungsart,
                       -- JTL-native Status berechnet
                       CASE
                           WHEN b.nStorno = 1 THEN 'Storniert'
                           WHEN va.nAuftragStatus = 255 THEN 'Storniert'
                           WHEN b.dVersandt > '1900-01-02' THEN 'Versendet'
                           WHEN va.nAuftragStatus = 4 THEN 'Abgeschlossen'
                           WHEN va.nAuftragStatus = 3 THEN 'Versendet'
                           WHEN va.nAuftragStatus = 2 THEN 'Versandbereit'
                           WHEN va.nAuftragStatus = 1 THEN 'In Bearbeitung'
                           WHEN va.nAuftragStatus = 0 THEN 'Offen'
                           ELSE 'Offen'
                       END AS CStatus,
                       va.nAuftragStatus AS NAuftragStatus,
                       k.cKundenNr,
                       a.cName AS KundeName, a.cFirma AS KundeFirma, a.cMail AS KundeMail, a.cTel AS KundeTel,
                       v.cName AS VersandartName,
                       z.cName AS ZahlungsartName,
                       s.cName AS ShopName,
                       ISNULL((SELECT SUM(bp.nAnzahl * bp.fVkNetto * (1 - ISNULL(bp.fRabatt,0)/100)) FROM tbestellpos bp WHERE bp.tBestellung_kBestellung = b.kBestellung), 0) AS GesamtNetto,
                       ISNULL((SELECT SUM(bp.nAnzahl * bp.fVkNetto * (1 - ISNULL(bp.fRabatt,0)/100) * (1 + bp.fMwSt/100)) FROM tbestellpos bp WHERE bp.tBestellung_kBestellung = b.kBestellung), 0) AS GesamtBrutto
                FROM tBestellung b
                LEFT JOIN Verkauf.tAuftrag va ON va.kAuftrag = b.kBestellung
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
                       bp.cHinweis AS CHinweis,
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
        /// Prüft ob ein Auftrag gelöscht werden kann (kein Lieferschein und keine Rechnung)
        /// </summary>
        public async Task<(bool CanDelete, string Reason)> CanDeleteAuftragAsync(int kAuftrag)
        {
            var conn = await GetConnectionAsync();

            // Prüfen ob Lieferschein existiert (kBestellung = kAuftrag in JTL)
            var hasLieferschein = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM dbo.tLieferschein WHERE kBestellung = @kAuftrag",
                new { kAuftrag }) > 0;

            if (hasLieferschein)
                return (false, "Auftrag hat bereits einen Lieferschein");

            // Prüfen ob Rechnung existiert
            var hasRechnung = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM Verkauf.tAuftragRechnung WHERE kAuftrag = @kAuftrag",
                new { kAuftrag }) > 0;

            if (hasRechnung)
                return (false, "Auftrag hat bereits eine Rechnung");

            return (true, "");
        }

        /// <summary>
        /// Löscht einen Auftrag komplett (nur wenn kein Lieferschein und keine Rechnung)
        /// </summary>
        public async Task<(bool Success, string Message)> DeleteAuftragAsync(int kAuftrag)
        {
            var (canDelete, reason) = await CanDeleteAuftragAsync(kAuftrag);
            if (!canDelete)
                return (false, reason);

            var conn = await GetConnectionAsync();

            try
            {
                // Auftragsnummer für Meldung holen
                var auftragNr = await conn.ExecuteScalarAsync<string>(
                    "SELECT cAuftragsnr FROM Verkauf.tAuftrag WHERE kAuftrag = @kAuftrag",
                    new { kAuftrag });

                // Lösche in der richtigen Reihenfolge (abhängige Tabellen zuerst)
                await conn.ExecuteAsync("DELETE FROM Verkauf.tAuftragPositionEckdaten WHERE kAuftragPosition IN (SELECT kAuftragPosition FROM Verkauf.tAuftragPosition WHERE kAuftrag = @kAuftrag)", new { kAuftrag });
                await conn.ExecuteAsync("DELETE FROM Verkauf.tAuftragPosition WHERE kAuftrag = @kAuftrag", new { kAuftrag });
                await conn.ExecuteAsync("DELETE FROM Verkauf.tAuftragEckdaten WHERE kAuftrag = @kAuftrag", new { kAuftrag });
                await conn.ExecuteAsync("DELETE FROM Verkauf.tAuftragText WHERE kAuftrag = @kAuftrag", new { kAuftrag });
                await conn.ExecuteAsync("DELETE FROM Verkauf.tAuftragAttributSprache WHERE kAuftragAttribut IN (SELECT kAuftragAttribut FROM Verkauf.tAuftragAttribut WHERE kAuftrag = @kAuftrag)", new { kAuftrag });
                await conn.ExecuteAsync("DELETE FROM Verkauf.tAuftragAttribut WHERE kAuftrag = @kAuftrag", new { kAuftrag });
                await conn.ExecuteAsync("DELETE FROM Verkauf.tAuftragAdresse WHERE kAuftrag = @kAuftrag", new { kAuftrag });
                await conn.ExecuteAsync("DELETE FROM Verkauf.tAuftrag WHERE kAuftrag = @kAuftrag", new { kAuftrag });

                _log.Information("Auftrag {AuftragNr} (ID: {KAuftrag}) erfolgreich gelöscht", auftragNr, kAuftrag);
                return (true, $"Auftrag {auftragNr} wurde erfolgreich gelöscht");
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Fehler beim Löschen von Auftrag {KAuftrag}", kAuftrag);
                return (false, $"Fehler beim Löschen: {ex.Message}");
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

            // Log in NOVVIA.Log
            if (_logService != null)
            {
                await _logService.LogBewegungsdatenAsync(
                    entityTyp: "Lieferschein",
                    kEntity: kLieferschein,
                    entityNr: lieferscheinNr,
                    aktion: "Erstellt",
                    beschreibung: $"Lieferschein erstellt fuer Auftrag {auftragNr}");
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

        #region Versand buchen (komplett)

        /// <summary>
        /// Kompletter Versand-Workflow: Lieferschein erstellen (falls nötig), tVersand anlegen, Tracking setzen
        /// </summary>
        public async Task<VersandResult> VersandBuchenAsync(int kAuftrag, string carrier, string trackingNr, byte[]? labelPdf = null, decimal gewicht = 1, int kBenutzer = 1)
        {
            var conn = await GetConnectionAsync();
            var result = new VersandResult { KAuftrag = kAuftrag, Carrier = carrier, TrackingNr = trackingNr };

            try
            {
                // 1. Prüfen ob Lieferschein existiert, sonst erstellen
                var lieferschein = await conn.QueryFirstOrDefaultAsync<(int KLieferschein, string? CLieferscheinNr)>(
                    "SELECT kLieferschein, cLieferscheinNr FROM tLieferschein WHERE kBestellung = @kAuftrag ORDER BY dErstellt DESC",
                    new { kAuftrag });

                int kLieferschein;
                if (lieferschein.KLieferschein == 0)
                {
                    // Lieferschein erstellen
                    kLieferschein = await CreateLieferscheinAsync(kAuftrag, kBenutzer);
                    result.LieferscheinErstellt = true;
                }
                else
                {
                    kLieferschein = lieferschein.KLieferschein;
                }
                result.KLieferschein = kLieferschein;

                // 2. Prüfen ob bereits ein Versand existiert
                var existingVersand = await conn.QueryFirstOrDefaultAsync<int?>(
                    "SELECT kVersand FROM tVersand WHERE kLieferschein = @kLieferschein",
                    new { kLieferschein });

                if (existingVersand.HasValue)
                {
                    // Versand aktualisieren
                    await conn.ExecuteAsync(@"
                        UPDATE tVersand SET
                            cIdentCode = @trackingNr,
                            cLogistiker = @carrier,
                            fGewicht = @gewicht,
                            dVersendet = GETDATE(),
                            nStatus = 1
                        WHERE kVersand = @kVersand",
                        new { trackingNr, carrier, gewicht, kVersand = existingVersand.Value });
                    result.KVersand = existingVersand.Value;
                }
                else
                {
                    // 3. tVersand Eintrag erstellen
                    var kVersandArt = await conn.QueryFirstOrDefaultAsync<int?>(
                        "SELECT a.kVersandArt FROM Verkauf.tAuftrag a WHERE a.kAuftrag = @kAuftrag",
                        new { kAuftrag });

                    result.KVersand = await conn.QuerySingleAsync<int>(@"
                        INSERT INTO tVersand (kLieferschein, kBenutzer, cIdentCode, dErstellt, fGewicht, kVersandArt, cLogistiker, dVersendet, nStatus)
                        VALUES (@kLieferschein, @kBenutzer, @trackingNr, GETDATE(), @gewicht, @kVersandArt, @carrier, GETDATE(), 1);
                        SELECT SCOPE_IDENTITY();",
                        new { kLieferschein, kBenutzer, trackingNr, gewicht, kVersandArt, carrier });
                }

                // 4. Label in tVersand speichern (falls vorhanden)
                if (labelPdf != null && labelPdf.Length > 0)
                {
                    await conn.ExecuteAsync(
                        "UPDATE tVersand SET bLabel = @label WHERE kVersand = @kVersand",
                        new { label = labelPdf, kVersand = result.KVersand });
                }

                // 5. Lieferschein Tracking aktualisieren
                await conn.ExecuteAsync(@"
                    UPDATE tLieferschein SET
                        cTrackingID = @trackingNr,
                        cVersandDienstleister = @carrier,
                        dVersandt = GETDATE()
                    WHERE kLieferschein = @kLieferschein",
                    new { trackingNr, carrier, kLieferschein });

                // 6. Auftrag Status auf "Versendet" setzen (Status 4 = Versendet)
                await conn.ExecuteAsync(@"
                    UPDATE Verkauf.tAuftrag SET
                        nKomplettAusgeliefert = 1
                    WHERE kAuftrag = @kAuftrag",
                    new { kAuftrag });

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                _log.Error(ex, "Fehler beim Versand buchen für Auftrag {KAuftrag}", kAuftrag);
            }

            return result;
        }

        /// <summary>
        /// Holt oder erstellt einen Lieferschein für einen Auftrag
        /// </summary>
        public async Task<int> GetOrCreateLieferscheinAsync(int kAuftrag, int kBenutzer = 1)
        {
            var conn = await GetConnectionAsync();

            var existing = await conn.QueryFirstOrDefaultAsync<int?>(
                "SELECT kLieferschein FROM tLieferschein WHERE kBestellung = @kAuftrag ORDER BY dErstellt DESC",
                new { kAuftrag });

            if (existing.HasValue && existing.Value > 0)
                return existing.Value;

            return await CreateLieferscheinAsync(kAuftrag, kBenutzer);
        }

        /// <summary>
        /// Lädt die Shipping-Konfiguration aus der Datenbank
        /// </summary>
        public async Task<ShippingConfig> GetShippingConfigAsync()
        {
            var conn = await GetConnectionAsync();
            var config = new ShippingConfig();

            try
            {
                // Firmendaten als Absender laden
                var firma = await conn.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT cFirma, cStrasse, cPLZ, cOrt, cEMail FROM tFirma WHERE kFirma = 1");

                if (firma != null)
                {
                    config.AbsenderName = firma.cFirma ?? "NOVVIA GmbH";
                    config.AbsenderStrasse = firma.cStrasse ?? "";
                    config.AbsenderPLZ = firma.cPLZ ?? "";
                    config.AbsenderOrt = firma.cOrt ?? "";
                    config.AbsenderEmail = firma.cEMail ?? "";
                }

                // Carrier-Zugangsdaten aus NOVVIA.Einstellungen laden
                var einstellungen = await conn.QueryAsync<(string CKey, string? CValue)>(
                    "SELECT cKey, cValue FROM NOVVIA.Einstellungen WHERE cKategorie = 'Versand'");

                foreach (var e in einstellungen)
                {
                    switch (e.CKey)
                    {
                        case "DHL_User": config.DHLUser = e.CValue ?? ""; break;
                        case "DHL_Password": config.DHLPassword = e.CValue ?? ""; break;
                        case "DHL_Profile": config.DHLProfile = e.CValue ?? ""; break;
                        case "DHL_BillingNumber": config.DHLBillingNumber = e.CValue ?? ""; break;
                        case "DPD_User": config.DPDUser = e.CValue ?? ""; break;
                        case "DPD_Password": config.DPDPassword = e.CValue ?? ""; break;
                        case "DPD_Depot": config.DPDDepot = e.CValue ?? ""; break;
                        case "GLS_User": config.GLSUser = e.CValue ?? ""; break;
                        case "GLS_Password": config.GLSPassword = e.CValue ?? ""; break;
                        case "GLS_ShipperId": config.GLSShipperId = e.CValue ?? ""; break;
                        case "UPS_Token": config.UPSToken = e.CValue ?? ""; break;
                        case "UPS_AccountNumber": config.UPSAccountNumber = e.CValue ?? ""; break;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Konnte Shipping-Config nicht aus DB laden, verwende Defaults");
            }

            return config;
        }

        /// <summary>
        /// Speichert die Shipping-Konfiguration in der Datenbank
        /// </summary>
        public async Task SaveShippingConfigAsync(ShippingConfig config)
        {
            var conn = await GetConnectionAsync();

            var settings = new Dictionary<string, string?>
            {
                { "DHL_User", config.DHLUser },
                { "DHL_Password", config.DHLPassword },
                { "DHL_Profile", config.DHLProfile },
                { "DHL_BillingNumber", config.DHLBillingNumber },
                { "DPD_User", config.DPDUser },
                { "DPD_Password", config.DPDPassword },
                { "DPD_Depot", config.DPDDepot },
                { "GLS_User", config.GLSUser },
                { "GLS_Password", config.GLSPassword },
                { "GLS_ShipperId", config.GLSShipperId },
                { "UPS_Token", config.UPSToken },
                { "UPS_AccountNumber", config.UPSAccountNumber }
            };

            foreach (var kvp in settings)
            {
                await conn.ExecuteAsync(@"
                    IF EXISTS (SELECT 1 FROM NOVVIA.Einstellungen WHERE cKategorie = 'Versand' AND cKey = @Key)
                        UPDATE NOVVIA.Einstellungen SET cValue = @Value WHERE cKategorie = 'Versand' AND cKey = @Key
                    ELSE
                        INSERT INTO NOVVIA.Einstellungen (cKategorie, cKey, cValue) VALUES ('Versand', @Key, @Value)",
                    new { Key = kvp.Key, Value = kvp.Value });
            }
        }

        public class VersandResult
        {
            public bool Success { get; set; }
            public int KAuftrag { get; set; }
            public int KLieferschein { get; set; }
            public int KVersand { get; set; }
            public string Carrier { get; set; } = "";
            public string TrackingNr { get; set; } = "";
            public bool LieferscheinErstellt { get; set; }
            public string? Error { get; set; }
        }

        /// <summary>
        /// Lieferadresse eines Auftrags laden (nTyp = 1)
        /// </summary>
        public async Task<AuftragAdresse?> GetAuftragLieferadresseAsync(int kAuftrag)
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryFirstOrDefaultAsync<AuftragAdresse>(@"
                SELECT kAuftrag, kKunde, nTyp, cFirma AS CFirma, cAnrede AS CAnrede, cTitel AS CTitel,
                       cVorname AS CVorname, cName AS CName, cStrasse AS CStrasse, cAdressZusatz AS CAdressZusatz,
                       cPLZ AS CPLZ, cOrt AS COrt, cBundesland AS CBundesland, cLand AS CLand, cISO AS CISO,
                       cTel AS CTel, cMobil AS CMobil, cFax AS CFax, cMail AS CMail
                FROM Verkauf.tAuftragAdresse
                WHERE kAuftrag = @kAuftrag AND nTyp = 1",
                new { kAuftrag });
        }

        /// <summary>
        /// Rechnungsadresse eines Auftrags laden (nTyp = 0)
        /// </summary>
        public async Task<AuftragAdresse?> GetAuftragRechnungsadresseAsync(int kAuftrag)
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryFirstOrDefaultAsync<AuftragAdresse>(@"
                SELECT kAuftrag, kKunde, nTyp, cFirma AS CFirma, cAnrede AS CAnrede, cTitel AS CTitel,
                       cVorname AS CVorname, cName AS CName, cStrasse AS CStrasse, cAdressZusatz AS CAdressZusatz,
                       cPLZ AS CPLZ, cOrt AS COrt, cBundesland AS CBundesland, cLand AS CLand, cISO AS CISO,
                       cTel AS CTel, cMobil AS CMobil, cFax AS CFax, cMail AS CMail
                FROM Verkauf.tAuftragAdresse
                WHERE kAuftrag = @kAuftrag AND nTyp = 0",
                new { kAuftrag });
        }

        public class AuftragAdresse
        {
            public int KAuftrag { get; set; }
            public int? KKunde { get; set; }
            public int NTyp { get; set; }
            public string? CFirma { get; set; }
            public string? CAnrede { get; set; }
            public string? CTitel { get; set; }
            public string? CVorname { get; set; }
            public string? CName { get; set; }
            public string? CStrasse { get; set; }
            public string? CAdressZusatz { get; set; }
            public string? CPLZ { get; set; }
            public string? COrt { get; set; }
            public string? CBundesland { get; set; }
            public string? CLand { get; set; }
            public string? CISO { get; set; }
            public string? CTel { get; set; }
            public string? CMobil { get; set; }
            public string? CFax { get; set; }
            public string? CMail { get; set; }
        }

        /// <summary>
        /// Versand-Label aus DB laden (bLabel aus tVersand)
        /// </summary>
        public async Task<byte[]?> GetVersandLabelAsync(int kAuftrag)
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryFirstOrDefaultAsync<byte[]>(@"
                SELECT v.bLabel
                FROM tVersand v
                INNER JOIN tLieferschein ls ON v.kLieferschein = ls.kLieferschein
                WHERE ls.kBestellung = @kAuftrag
                ORDER BY v.dErstellt DESC",
                new { kAuftrag });
        }

        #endregion

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

            // Log in NOVVIA.Log
            if (_logService != null)
            {
                await _logService.LogBewegungsdatenAsync(
                    entityTyp: "Rechnung",
                    kEntity: kRechnung,
                    entityNr: rechnungsNr,
                    aktion: "Erstellt",
                    beschreibung: $"Rechnung erstellt fuer Auftrag {kAuftrag}, Kunde {auftrag.CKundennr}");
            }

            return kRechnung;
        }

        /// <summary>
        /// Erstellt eine Rechnung OHNE Lieferschein (für Dienstleistungen)
        /// Positionen werden direkt aus dem Auftrag übernommen
        /// </summary>
        public async Task<int> CreateRechnungOhneVersandAsync(int kAuftrag, int kBenutzer = 1)
        {
            var conn = await GetConnectionAsync();

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

            // Positionen DIREKT aus Auftragspositionen erstellen (ohne Lieferschein)
            var bestellPositionen = await conn.QueryAsync<(int KBestellPos, int KArtikel, string? CArtNr, string? CName, string? CEinheit, decimal FAnzahl, decimal FMwSt, decimal FVkNetto, decimal FRabatt, decimal FGewicht, decimal FEkNetto)>(
                @"SELECT kBestellPos, kArtikel, cArtNr, cName, cEinheit,
                         fAnzahl, fMwSt, fVKNetto, ISNULL(fRabatt, 0),
                         ISNULL(fGewicht, 0), ISNULL(fEKNetto, 0)
                  FROM tBestellPos
                  WHERE tBestellung_kBestellung = @kAuftrag",
                new { kAuftrag });

            int nSort = 0;
            foreach (var pos in bestellPositionen)
            {
                nSort++;
                await conn.ExecuteScalarAsync<int>(
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

            // Auftragsstatus aktualisieren
            await conn.ExecuteAsync(
                @"UPDATE tBestellung SET cStatus = 'Abgerechnet', dGeaendert = GETDATE() WHERE kBestellung = @kAuftrag",
                new { kAuftrag });

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

        /// <summary>
        /// Storniert eine Rechnung (nur Status ändern, keine Gutschrift)
        /// Auftrag wird wieder bearbeitbar (Preis, Anschrift), aber KEINE Lagerbewegungen
        /// Lagerbewegungen nur über Retoure änderbar
        /// </summary>
        public async Task StornoRechnungAsync(int kRechnung, int kBenutzer = 1)
        {
            var conn = await GetConnectionAsync();

            // Prüfen ob Rechnung existiert
            var rechnung = await conn.QuerySingleOrDefaultAsync<(int KBestellung, string? CRechnungsnr)>(
                @"SELECT ISNULL((SELECT TOP 1 kAuftrag FROM Rechnung.tRechnungPosition WHERE kRechnung = r.kRechnung), 0),
                         cRechnungsnr
                  FROM Rechnung.tRechnung r WHERE kRechnung = @kRechnung",
                new { kRechnung });

            if (string.IsNullOrEmpty(rechnung.CRechnungsnr))
                throw new InvalidOperationException("Rechnung nicht gefunden.");

            // Rechnung als storniert markieren (Status 5)
            await conn.ExecuteAsync(
                @"UPDATE Rechnung.tRechnung
                  SET nRechnungStatus = 5,
                      cAnmerkung = ISNULL(cAnmerkung, '') + ' [STORNIERT ' + CONVERT(VARCHAR, GETDATE(), 104) + ']'
                  WHERE kRechnung = @kRechnung",
                new { kRechnung });

            // Hinweis: Auftrag wird dadurch wieder bearbeitbar (Preis, Anschrift)
            // Lagerbewegungen bleiben bestehen - nur über Retoure änderbar
            _log.Information("Rechnung {RechnungNr} storniert. Auftrag {KBestellung} wieder bearbeitbar für Preis/Anschrift.",
                rechnung.CRechnungsnr, rechnung.KBestellung);
        }

        /// <summary>
        /// Erstellt eine Rechnungskorrektur (Gutschrift) für einen Teilbetrag
        /// </summary>
        public async Task<int> CreateRechnungskorrekturAsync(int kRechnung, decimal betrag, string grund, int kBenutzer = 1)
        {
            var conn = await GetConnectionAsync();

            // Rechnungsdaten laden
            var rechnung = await conn.QuerySingleOrDefaultAsync<(int KKunde, string? CRechnungsnr, decimal FMwSt, int? KZahlungsart)>(
                @"SELECT kKunde, cRechnungsnr,
                         (SELECT TOP 1 fMwSt FROM Rechnung.tRechnungPosition WHERE kRechnung = r.kRechnung) AS FMwSt,
                         kZahlungsart
                  FROM Rechnung.tRechnung r WHERE kRechnung = @kRechnung",
                new { kRechnung });

            if (rechnung.KKunde == 0)
                throw new InvalidOperationException("Rechnung nicht gefunden.");

            // Gutschriftnummer generieren
            var nextNr = await conn.ExecuteScalarAsync<int>(
                "SELECT ISNULL(MAX(CAST(REPLACE(cRechnungsnr, 'GS-', '') AS INT)), 0) + 1 FROM Rechnung.tRechnung WHERE cRechnungsnr LIKE 'GS-%'");
            var gutschriftNr = $"GS-{nextNr}";

            // Netto/Brutto berechnen
            var mwstSatz = rechnung.FMwSt > 0 ? rechnung.FMwSt : 19m;
            var netto = betrag / (1 + mwstSatz / 100);

            // Kundendaten laden
            var kunde = await conn.QuerySingleOrDefaultAsync<(string? CFirma, int? KKundenGruppe, string? CKundengruppe, string? CKundennr)>(
                @"SELECT a.cFirma, k.kKundenGruppe, kg.cName, k.cKundenNr
                  FROM tkunde k
                  LEFT JOIN tAdresse a ON k.kKunde = a.kKunde AND a.nStandard = 1
                  LEFT JOIN tKundenGruppe kg ON k.kKundenGruppe = kg.kKundenGruppe
                  WHERE k.kKunde = @kKunde",
                new { kKunde = rechnung.KKunde });

            // Gutschrift erstellen
            var kGutschrift = await conn.ExecuteScalarAsync<int>(
                @"INSERT INTO Rechnung.tRechnung
                  (kBenutzer, kKunde, cRechnungsnr, dErstellt, dValutadatum, cKundennr, cKundengruppe, kKundengruppe,
                   cFirma, nMahnstop, nStatus, cWaehrung, fWaehrungsfaktor, kZahlungsart, kSprache,
                   nSteuereinstellung, nRechnungStatus, dLeistungsdatum, fBrutto, fNetto, cAnmerkung)
                  OUTPUT INSERTED.kRechnung
                  VALUES
                  (@kBenutzer, @kKunde, @cRechnungsnr, GETDATE(), GETDATE(), @cKundennr, @cKundengruppe, @kKundengruppe,
                   @cFirma, 1, 0, 'EUR', 1.0, @kZahlungsart, 1, 0, 10, GETDATE(), @fBrutto, @fNetto, @cAnmerkung)",
                new
                {
                    kBenutzer,
                    kKunde = rechnung.KKunde,
                    cRechnungsnr = gutschriftNr,
                    cKundennr = kunde.CKundennr,
                    cKundengruppe = kunde.CKundengruppe,
                    kKundengruppe = kunde.KKundenGruppe,
                    cFirma = kunde.CFirma,
                    kZahlungsart = rechnung.KZahlungsart,
                    fBrutto = -betrag,
                    fNetto = -netto,
                    cAnmerkung = $"Rechnungskorrektur zu {rechnung.CRechnungsnr}: {grund}"
                });

            // Eine Position für die Gutschrift erstellen
            await conn.ExecuteAsync(
                @"INSERT INTO Rechnung.tRechnungPosition
                  (kRechnung, cName, fAnzahl, fMwSt, fVkNetto, nType, nSort)
                  VALUES
                  (@kGutschrift, @cName, -1, @fMwSt, @fVkNetto, 1, 1)",
                new
                {
                    kGutschrift,
                    cName = $"Rechnungskorrektur: {grund}",
                    fMwSt = mwstSatz,
                    fVkNetto = netto
                });

            return kGutschrift;
        }

        /// <summary>
        /// Erfasst eine Zahlung für eine Rechnung
        /// </summary>
        public async Task ErfasseZahlungAsync(int kRechnung, decimal betrag, int kBenutzer = 1)
        {
            var conn = await GetConnectionAsync();

            // Zahlungseingang erfassen
            await conn.ExecuteAsync(
                @"INSERT INTO Rechnung.tZahlungseingang
                  (kRechnung, dDatum, fBetrag, cZahlungsart, kBenutzer)
                  VALUES
                  (@kRechnung, GETDATE(), @fBetrag, 'Manuell', @kBenutzer)",
                new { kRechnung, fBetrag = betrag, kBenutzer });

            // Offenen Betrag aktualisieren
            await conn.ExecuteAsync(
                @"UPDATE Rechnung.tRechnung
                  SET fOffen = fOffen - @fBetrag,
                      nRechnungStatus = CASE WHEN fOffen - @fBetrag <= 0 THEN 3 ELSE nRechnungStatus END,
                      dBezahlt = CASE WHEN fOffen - @fBetrag <= 0 THEN GETDATE() ELSE dBezahlt END
                  WHERE kRechnung = @kRechnung",
                new { kRechnung, fBetrag = betrag });
        }

        /// <summary>
        /// Lädt eine Rechnung mit allen Positionen, Kunde und Zahlungen
        /// </summary>
        public async Task<Rechnung?> GetRechnungMitPositionenAsync(int kRechnung)
        {
            var conn = await GetConnectionAsync();

            // Rechnung laden mit berechneten Werten
            var rechnung = await conn.QuerySingleOrDefaultAsync<Rechnung>(
                @"SELECT
                    r.kRechnung AS Id,
                    r.cRechnungsnr AS RechnungsNr,
                    r.kKunde AS KundeId,
                    r.dErstellt AS Erstellt,
                    DATEADD(DAY, r.nZahlungszielTage, r.dErstellt) AS Faellig,
                    -- Bezahlt = letztes Zahlungsdatum wenn komplett bezahlt
                    CASE WHEN ISNULL(zahlung.Summe, 0) >= ISNULL(pos.Brutto, 0)
                         THEN zahlung.LetztesZahlungsdatum ELSE NULL END AS Bezahlt,
                    ISNULL(pos.Netto, 0) AS Netto,
                    ISNULL(pos.Brutto, 0) AS Brutto,
                    ISNULL(pos.MwSt, 0) AS MwSt,
                    ISNULL(pos.Brutto, 0) - ISNULL(zahlung.Summe, 0) AS OffenerBetrag,
                    r.nRechnungStatus AS Status,
                    r.nStorno AS IstStorniert,
                    r.cWaehrung AS Waehrung
                FROM Rechnung.tRechnung r
                -- Positionen aggregieren
                LEFT JOIN (
                    SELECT kRechnung,
                           SUM(fVkNetto * fAnzahl * (1 - ISNULL(fRabatt,0)/100)) AS Netto,
                           SUM(fVkNetto * fAnzahl * (1 - ISNULL(fRabatt,0)/100) * (1 + fMwSt/100)) AS Brutto,
                           SUM(fVkNetto * fAnzahl * (1 - ISNULL(fRabatt,0)/100) * fMwSt/100) AS MwSt
                    FROM Rechnung.tRechnungPosition
                    GROUP BY kRechnung
                ) pos ON r.kRechnung = pos.kRechnung
                -- Zahlungen aggregieren
                LEFT JOIN (
                    SELECT kRechnung,
                           SUM(fBetrag) AS Summe,
                           MAX(dDatum) AS LetztesZahlungsdatum
                    FROM dbo.tZahlung
                    WHERE kRechnung IS NOT NULL
                    GROUP BY kRechnung
                ) zahlung ON r.kRechnung = zahlung.kRechnung
                WHERE r.kRechnung = @kRechnung",
                new { kRechnung });

            if (rechnung == null) return null;

            // Kunde aus RechnungAdresse laden (nTyp=0 = Rechnungsadresse)
            rechnung.Kunde = await conn.QuerySingleOrDefaultAsync<Kunde>(
                @"SELECT
                    ra.kKunde AS Id,
                    r.cKundennr AS KundenNr,
                    ra.cFirma AS Firma,
                    ra.cVorname AS Vorname,
                    ra.cName AS Nachname,
                    ra.cStrasse AS Strasse,
                    ra.cPLZ AS PLZ,
                    ra.cOrt AS Ort,
                    ra.cLand AS Land,
                    ra.cMail AS Email,
                    ra.cTel AS Telefon
                FROM Rechnung.tRechnungAdresse ra
                JOIN Rechnung.tRechnung r ON ra.kRechnung = r.kRechnung
                WHERE ra.kRechnung = @kRechnung AND ra.nTyp = 0",
                new { kRechnung });

            // Positionen laden
            rechnung.Positionen = (await conn.QueryAsync<RechnungsPosition>(
                @"SELECT
                    kRechnungPosition AS Id,
                    kRechnung AS RechnungId,
                    kArtikel AS ArtikelId,
                    cArtNr AS ArtNr,
                    cName AS Name,
                    fAnzahl AS Menge,
                    fVkNetto AS PreisNetto,
                    fVkNetto * (1 + fMwSt/100) AS PreisBrutto,
                    fMwSt AS MwStSatz,
                    ISNULL(fRabatt, 0) AS Rabatt
                FROM Rechnung.tRechnungPosition
                WHERE kRechnung = @kRechnung
                ORDER BY nSort",
                new { kRechnung })).ToList();

            // Zahlungen laden aus dbo.tZahlung
            rechnung.Zahlungen = (await conn.QueryAsync<Zahlungseingang>(
                @"SELECT
                    kZahlung AS Id,
                    kRechnung AS RechnungId,
                    dDatum AS Datum,
                    fBetrag AS Betrag,
                    ISNULL(za.cName, 'Unbekannt') AS Zahlungsart,
                    cHinweis AS Referenz
                FROM dbo.tZahlung z
                LEFT JOIN dbo.tZahlungsart za ON z.kZahlungsart = za.kZahlungsart
                WHERE z.kRechnung = @kRechnung
                ORDER BY dDatum DESC",
                new { kRechnung })).ToList();

            return rechnung;
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

        #region Eingangsrechnungen (NOVVIA Custom)

        /// <summary>
        /// Lädt Eingangsrechnungen aus JTL-native Tabelle mit Filteroptionen
        /// </summary>
        public async Task<IEnumerable<EingangsrechnungItem>> GetEingangsrechnungenAsync(
            string? suche = null, int? status = null, DateTime? von = null, DateTime? bis = null)
        {
            var conn = await GetConnectionAsync();

            var sql = @"
                SELECT e.kEingangsrechnung AS Id, e.kLieferant AS LieferantId,
                       ISNULL(e.cLieferant, ISNULL(l.cFirma, '')) AS LieferantName,
                       NULL AS LieferantenBestellungId,
                       e.cFremdbelegnummer AS RechnungsNr,
                       ISNULL(e.dBelegdatum, e.dErstellt) AS RechnungsDatum,
                       e.dZahlungsziel AS FaelligAm,
                       ISNULL((SELECT SUM(p.fMenge * p.fEKNetto) FROM dbo.tEingangsrechnungPos p WHERE p.kEingangsrechnung = e.kEingangsrechnung), 0) AS Netto,
                       ISNULL((SELECT SUM(p.fMenge * p.fEKNetto * p.fMwSt / 100) FROM dbo.tEingangsrechnungPos p WHERE p.kEingangsrechnung = e.kEingangsrechnung), 0) AS MwSt,
                       ISNULL((SELECT SUM(p.fMenge * p.fEKNetto * (1 + p.fMwSt / 100)) FROM dbo.tEingangsrechnungPos p WHERE p.kEingangsrechnung = e.kEingangsrechnung), 0) AS Brutto,
                       e.nStatus AS Status,
                       CASE e.nStatus WHEN 0 THEN 'Offen' WHEN 5 THEN 'Freigegeben' WHEN 10 THEN 'Gebucht' WHEN 20 THEN 'Abgeschlossen' ELSE 'Status ' + CAST(e.nStatus AS VARCHAR) END AS StatusText,
                       e.dBezahlt AS BezahltAm, e.cHinweise AS Bemerkung,
                       e.cEigeneRechnungsnummer AS BestellungNr
                FROM dbo.tEingangsrechnung e
                LEFT JOIN dbo.tLieferant l ON e.kLieferant = l.kLieferant
                WHERE e.nDeleted = 0";

            if (!string.IsNullOrWhiteSpace(suche))
                sql += " AND (e.cFremdbelegnummer LIKE @suche OR e.cLieferant LIKE @suche OR l.cFirma LIKE @suche)";
            if (status.HasValue)
                sql += " AND e.nStatus = @status";
            if (von.HasValue)
                sql += " AND ISNULL(e.dBelegdatum, e.dErstellt) >= @von";
            if (bis.HasValue)
                sql += " AND ISNULL(e.dBelegdatum, e.dErstellt) < @bis";

            sql += " ORDER BY e.dErstellt DESC";

            return await conn.QueryAsync<EingangsrechnungItem>(sql, new
            {
                suche = $"%{suche}%",
                status,
                von,
                bis
            });
        }

        public async Task<EingangsrechnungDto?> GetEingangsrechnungByIdAsync(int id)
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryFirstOrDefaultAsync<EingangsrechnungDto>(
                @"SELECT e.kEingangsrechnung AS Id, e.kLieferant AS LieferantId,
                         NULL AS LieferantenBestellungId,
                         e.cFremdbelegnummer AS RechnungsNr,
                         ISNULL(e.dBelegdatum, e.dErstellt) AS RechnungsDatum,
                         e.dZahlungsziel AS FaelligAm,
                         ISNULL((SELECT SUM(p.fMenge * p.fEKNetto) FROM dbo.tEingangsrechnungPos p WHERE p.kEingangsrechnung = e.kEingangsrechnung), 0) AS Netto,
                         ISNULL((SELECT SUM(p.fMenge * p.fEKNetto * p.fMwSt / 100) FROM dbo.tEingangsrechnungPos p WHERE p.kEingangsrechnung = e.kEingangsrechnung), 0) AS MwSt,
                         ISNULL((SELECT SUM(p.fMenge * p.fEKNetto * (1 + p.fMwSt / 100)) FROM dbo.tEingangsrechnungPos p WHERE p.kEingangsrechnung = e.kEingangsrechnung), 0) AS Brutto,
                         e.nStatus AS Status, e.dBezahlt AS BezahltAm,
                         e.cHinweise AS Bemerkung
                  FROM dbo.tEingangsrechnung e
                  WHERE e.kEingangsrechnung = @id AND e.nDeleted = 0",
                new { id });
        }

        /// <summary>
        /// Lädt vollständige Eingangsrechnung-Details für Detailansicht (JTL-native)
        /// </summary>
        public async Task<EingangsrechnungDetail?> GetEingangsrechnungDetailAsync(int id)
        {
            var conn = await GetConnectionAsync();
            var rechnung = await conn.QueryFirstOrDefaultAsync<EingangsrechnungDetail>(
                @"SELECT e.kEingangsrechnung AS Id, e.kLieferant AS LieferantId,
                         ISNULL(e.cLieferant, l.cFirma) AS LieferantName,
                         ISNULL(e.cStrasse, l.cStrasse) AS Strasse,
                         ISNULL(e.cPLZ, l.cPLZ) AS PLZ,
                         ISNULL(e.cOrt, l.cOrt) AS Ort,
                         ISNULL(e.cLandISO, l.cLand) AS Land,
                         ISNULL(e.cTel, l.cTelZentralle) AS Telefon,
                         ISNULL(e.cMail, l.cEMail) AS Email,
                         e.cFremdbelegnummer AS Fremdbelegnummer,
                         e.cEigeneRechnungsnummer AS EigeneNummer,
                         e.dBelegdatum AS Belegdatum,
                         e.dZahlungsziel AS Zahlungsziel,
                         e.cHinweise AS Hinweise,
                         e.nStatus AS Status,
                         e.nZahlungFreigegeben AS ZahlungFreigegeben,
                         e.dBezahlt AS BezahltAm
                  FROM dbo.tEingangsrechnung e
                  LEFT JOIN dbo.tLieferant l ON e.kLieferant = l.kLieferant
                  WHERE e.kEingangsrechnung = @id AND e.nDeleted = 0",
                new { id });

            if (rechnung != null)
            {
                // Enthaltene Bestellungen laden
                rechnung.Bestellungen = (await conn.QueryAsync<string>(
                    @"SELECT DISTINCT ISNULL(lb.cEigeneBestellnummer, 'PO-' + CAST(p.kLieferantenbestellung AS VARCHAR))
                      FROM dbo.tEingangsrechnungPos p
                      INNER JOIN dbo.tLieferantenBestellung lb ON p.kLieferantenbestellung = lb.kLieferantenBestellung
                      WHERE p.kEingangsrechnung = @id AND p.kLieferantenbestellung IS NOT NULL",
                    new { id })).ToList();
            }

            return rechnung;
        }

        /// <summary>
        /// Lädt Eingangsrechnung-Positionen (JTL-native)
        /// </summary>
        public async Task<IEnumerable<EingangsrechnungPosDetail>> GetEingangsrechnungPositionenAsync(int eingangsrechnungId)
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<EingangsrechnungPosDetail>(
                @"SELECT p.kEingangsrechnungPos AS KPosition,
                         p.kArtikel AS KArtikel,
                         ISNULL(p.cArtNr, '') AS CArtNr,
                         ISNULL(p.cLieferantenArtNr, '') AS CLieferantenArtNr,
                         ISNULL(p.cName, '') AS CName,
                         ISNULL(p.cEinheit, 'Stk') AS CEinheit,
                         ISNULL(p.cHinweis, '') AS CHinweis,
                         ISNULL(p.fMenge, 0) AS FMenge,
                         ISNULL(p.fEKNetto, 0) AS FEKNetto,
                         ISNULL(p.fMwSt, 0) AS FMwSt
                  FROM dbo.tEingangsrechnungPos p
                  WHERE p.kEingangsrechnung = @eingangsrechnungId
                  ORDER BY p.kEingangsrechnungPos",
                new { eingangsrechnungId });
        }

        /// <summary>
        /// Aktualisiert Eingangsrechnung-Status (JTL-native)
        /// </summary>
        public async Task UpdateEingangsrechnungJtlAsync(int id, EingangsrechnungUpdateDto dto)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync(
                @"UPDATE dbo.tEingangsrechnung
                  SET nStatus = @Status,
                      nZahlungFreigegeben = @ZahlungFreigegeben,
                      dZahlungsziel = @Zahlungsziel,
                      cHinweise = @Hinweise
                  WHERE kEingangsrechnung = @Id",
                new
                {
                    Id = id,
                    dto.Status,
                    ZahlungFreigegeben = dto.ZahlungFreigegeben ? 1 : 0,
                    dto.Zahlungsziel,
                    dto.Hinweise
                });
        }

        /// <summary>
        /// Erstellt eine Eingangsrechnung aus einer Lieferantenbestellung (JTL-native Tabellen)
        /// </summary>
        public async Task<int> CreateEingangsrechnungFromBestellungAsync(int kLieferantenBestellung, int kLieferant, List<EingangsrechnungPosInput> positionen)
        {
            var conn = await GetConnectionAsync();

            // Lieferant-Name holen
            var lieferantName = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT cFirma FROM dbo.tLieferant WHERE kLieferant = @kLieferant",
                new { kLieferant }) ?? "";

            // Bestellnummer holen
            var bestellNr = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT cEigeneBestellnummer FROM dbo.tLieferantenBestellung WHERE kLieferantenBestellung = @id",
                new { id = kLieferantenBestellung }) ?? "";

            // Eingangsrechnung anlegen
            var kEingangsrechnung = await conn.QuerySingleAsync<int>(
                @"INSERT INTO dbo.tEingangsrechnung
                    (kLieferant, cLieferant, dBelegdatum, dErstellt, nStatus, nDeleted,
                     cEigeneRechnungsnummer, dZahlungsziel, nZahlungFreigegeben)
                  OUTPUT INSERTED.kEingangsrechnung
                  VALUES
                    (@kLieferant, @cLieferant, GETDATE(), GETDATE(), 0, 0,
                     @cEigeneRechnungsnummer, DATEADD(DAY, 30, GETDATE()), 0)",
                new
                {
                    kLieferant,
                    cLieferant = lieferantName,
                    cEigeneRechnungsnummer = $"Bestell. {bestellNr}"
                });

            // Positionen anlegen
            foreach (var pos in positionen)
            {
                await conn.ExecuteAsync(
                    @"INSERT INTO dbo.tEingangsrechnungPos
                        (kEingangsrechnung, kArtikel, cArtNr, cName, fMenge, fEKNetto, fMwSt, cEinheit)
                      VALUES
                        (@kEingangsrechnung, @kArtikel, @cArtNr, @cName, @fMenge, @fEKNetto, @fMwSt, 'Stk')",
                    new
                    {
                        kEingangsrechnung,
                        kArtikel = pos.KArtikel,
                        cArtNr = pos.CArtNr,
                        cName = pos.CName,
                        fMenge = pos.FMenge,
                        fEKNetto = pos.FEKNetto,
                        fMwSt = pos.FMwSt
                    });
            }

            return kEingangsrechnung;
        }

        public class EingangsrechnungPosInput
        {
            public int KArtikel { get; set; }
            public string CArtNr { get; set; } = "";
            public string CLieferantenArtNr { get; set; } = "";
            public string CName { get; set; } = "";
            public decimal FMenge { get; set; }
            public decimal FEKNetto { get; set; }
            public decimal FMwSt { get; set; }
        }

        public class EingangsrechnungDetail
        {
            public int Id { get; set; }
            public int LieferantId { get; set; }
            public string LieferantName { get; set; } = "";
            public string Strasse { get; set; } = "";
            public string PLZ { get; set; } = "";
            public string Ort { get; set; } = "";
            public string Land { get; set; } = "";
            public string Telefon { get; set; } = "";
            public string Email { get; set; } = "";
            public string Fremdbelegnummer { get; set; } = "";
            public string EigeneNummer { get; set; } = "";
            public DateTime? Belegdatum { get; set; }
            public DateTime? Zahlungsziel { get; set; }
            public string Hinweise { get; set; } = "";
            public int Status { get; set; }
            public bool ZahlungFreigegeben { get; set; }
            public DateTime? BezahltAm { get; set; }
            public List<string> Bestellungen { get; set; } = new();
        }

        public class EingangsrechnungPosDetail
        {
            public int KPosition { get; set; }
            public int KArtikel { get; set; }
            public string CArtNr { get; set; } = "";
            public string CLieferantenArtNr { get; set; } = "";
            public string CName { get; set; } = "";
            public string CEinheit { get; set; } = "";
            public string CHinweis { get; set; } = "";
            public decimal FMenge { get; set; }
            public decimal FEKNetto { get; set; }
            public decimal FMwSt { get; set; }
        }

        public class EingangsrechnungUpdateDto
        {
            public int Status { get; set; }
            public bool ZahlungFreigegeben { get; set; }
            public DateTime? Zahlungsziel { get; set; }
            public string? Hinweise { get; set; }
        }

        public async Task<int> CreateEingangsrechnungAsync(EingangsrechnungDto dto)
        {
            var conn = await GetConnectionAsync();

            // Tabelle erstellen falls nicht vorhanden
            await conn.ExecuteAsync(@"
                IF NOT EXISTS (SELECT * FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE s.name = 'NOVVIA' AND t.name = 'tEingangsrechnung')
                BEGIN
                    CREATE TABLE NOVVIA.tEingangsrechnung (
                        kEingangsrechnung INT IDENTITY(1,1) PRIMARY KEY,
                        kLieferant INT NOT NULL,
                        kLieferantenBestellung INT NULL,
                        cRechnungsNr NVARCHAR(50) NOT NULL,
                        dRechnungsDatum DATE NOT NULL,
                        dFaelligAm DATE NULL,
                        fNetto DECIMAL(18,4) NOT NULL DEFAULT 0,
                        fMwSt DECIMAL(18,4) NOT NULL DEFAULT 0,
                        fBrutto DECIMAL(18,4) NOT NULL DEFAULT 0,
                        nStatus INT NOT NULL DEFAULT 0,
                        dBezahltAm DATE NULL,
                        cBemerkung NVARCHAR(MAX) NULL,
                        dErstellt DATETIME NOT NULL DEFAULT GETDATE(),
                        dGeaendert DATETIME NOT NULL DEFAULT GETDATE()
                    )
                END");

            return await conn.ExecuteScalarAsync<int>(
                @"INSERT INTO NOVVIA.tEingangsrechnung
                  (kLieferant, kLieferantenBestellung, cRechnungsNr, dRechnungsDatum, dFaelligAm,
                   fNetto, fMwSt, fBrutto, nStatus, dBezahltAm, cBemerkung)
                  OUTPUT INSERTED.kEingangsrechnung
                  VALUES (@LieferantId, @LieferantenBestellungId, @RechnungsNr, @RechnungsDatum, @FaelligAm,
                          @Netto, @MwSt, @Brutto, @Status, @BezahltAm, @Bemerkung)",
                dto);
        }

        public async Task UpdateEingangsrechnungAsync(EingangsrechnungDto dto)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync(
                @"UPDATE NOVVIA.tEingangsrechnung SET
                    kLieferant = @LieferantId,
                    kLieferantenBestellung = @LieferantenBestellungId,
                    cRechnungsNr = @RechnungsNr,
                    dRechnungsDatum = @RechnungsDatum,
                    dFaelligAm = @FaelligAm,
                    fNetto = @Netto,
                    fMwSt = @MwSt,
                    fBrutto = @Brutto,
                    nStatus = @Status,
                    dBezahltAm = @BezahltAm,
                    cBemerkung = @Bemerkung,
                    dGeaendert = GETDATE()
                  WHERE kEingangsrechnung = @Id",
                dto);
        }

        public async Task UpdateEingangsrechnungStatusAsync(int id, int status, DateTime? bezahltAm)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync(
                @"UPDATE NOVVIA.tEingangsrechnung SET nStatus = @status, dBezahltAm = @bezahltAm, dGeaendert = GETDATE()
                  WHERE kEingangsrechnung = @id",
                new { id, status, bezahltAm });
        }

        public async Task DeleteEingangsrechnungAsync(int id)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync("DELETE FROM NOVVIA.tEingangsrechnung WHERE kEingangsrechnung = @id", new { id });
        }

        public class EingangsrechnungItem
        {
            public int Id { get; set; }
            public int LieferantId { get; set; }
            public string LieferantName { get; set; } = "";
            public int? LieferantenBestellungId { get; set; }
            public string? RechnungsNr { get; set; }
            public DateTime RechnungsDatum { get; set; }
            public DateTime? FaelligAm { get; set; }
            public decimal Netto { get; set; }
            public decimal MwSt { get; set; }
            public decimal Brutto { get; set; }
            public int Status { get; set; }
            public string StatusText { get; set; } = "";
            public DateTime? BezahltAm { get; set; }
            public string? Bemerkung { get; set; }
            public string? BestellungNr { get; set; }
        }

        public class EingangsrechnungDto
        {
            public int Id { get; set; }
            public int LieferantId { get; set; }
            public int? LieferantenBestellungId { get; set; }
            public string RechnungsNr { get; set; } = "";
            public DateTime RechnungsDatum { get; set; } = DateTime.Today;
            public DateTime? FaelligAm { get; set; }
            public decimal Netto { get; set; }
            public decimal MwSt { get; set; }
            public decimal Brutto { get; set; }
            public int Status { get; set; }
            public DateTime? BezahltAm { get; set; }
            public string? Bemerkung { get; set; }
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

        /// <summary>Erweiterte Lieferantensuche (wie Kunden: Name, Firma, Strasse, Ort)</summary>
        public async Task<List<LieferantRef>> SearchLieferantenErweitertAsync(string suchbegriff, int limit = 50)
        {
            var conn = await GetConnectionAsync();
            var sql = @"
                SELECT TOP (@Limit)
                    kLieferant AS KLieferant, cFirma AS CFirma, cStrasse AS CStrasse,
                    cPLZ AS CPLZ, cOrt AS COrt, cLand AS CLand,
                    COALESCE(cTelZentralle, cTelDurchwahl, '') AS CTelefon,
                    cFax AS CFax, cEMail AS CEmail
                FROM dbo.tLieferant
                WHERE cAktiv = 'Y'
                  AND (@Suche = '' OR cFirma LIKE @Suche
                       OR cStrasse LIKE @Suche
                       OR cPLZ LIKE @Suche
                       OR cOrt LIKE @Suche
                       OR CAST(kLieferant AS NVARCHAR) = @SuchExakt)
                ORDER BY cFirma";

            var result = await conn.QueryAsync<LieferantRef>(sql, new {
                Limit = limit,
                Suche = string.IsNullOrEmpty(suchbegriff) ? "" : $"%{suchbegriff}%",
                SuchExakt = suchbegriff ?? ""
            });
            return result.ToList();
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
                WHERE nAktiv = 1
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
        /// Lager-Uebersicht fuer Einstellungen (mit Anzahl Plaetze)
        /// </summary>
        public async Task<List<LagerUebersicht>> GetLagerUebersichtAsync()
        {
            var conn = await GetConnectionAsync();
            return (await conn.QueryAsync<LagerUebersicht>(@"
                SELECT wl.kWarenLager, wl.cName, wl.cKuerzel, wl.cLagerTyp,
                       CAST(wl.nAktiv AS BIT) AS NAktiv,
                       (SELECT COUNT(*) FROM tWarenLagerPlatz WHERE kWarenLager = wl.kWarenLager) AS AnzahlPlaetze,
                       wl.cStrasse, wl.cPLZ, wl.cOrt, wl.cLand,
                       RTRIM(ISNULL(wl.cAnsprechpartnerVorname, '') + ' ' + ISNULL(wl.cAnsprechpartnerName, '')) AS CAnsprechpartner,
                       wl.cAnsprechpartnerTel AS CTelefon,
                       wl.cAnsprechpartnerEMail AS CEmail
                FROM tWarenLager wl
                ORDER BY wl.cName")).ToList();
        }

        /// <summary>
        /// Neues Warenlager anlegen
        /// </summary>
        public async Task<int> CreateWarenlagerAsync(string name, string kuerzel)
        {
            var conn = await GetConnectionAsync();
            return await conn.QuerySingleAsync<int>(@"
                INSERT INTO tWarenLager (cName, cKuerzel, nAktiv, kFirma)
                VALUES (@Name, @Kuerzel, 1, 1);
                SELECT SCOPE_IDENTITY();",
                new { Name = name, Kuerzel = kuerzel });
        }

        /// <summary>
        /// Warenlager aktualisieren (JTL-Style mit Adresse)
        /// </summary>
        public async Task<int> UpdateWarenlagerAsync(int kWarenLager, string name, string kuerzel, bool aktiv,
            string? lagerTyp = null, string? strasse = null, string? plz = null, string? ort = null, string? land = null,
            string? ansprechpartnerName = null, string? telefon = null, string? email = null)
        {
            var conn = await GetConnectionAsync();
            return await conn.ExecuteAsync(@"
                UPDATE tWarenLager
                SET cName = @Name,
                    cKuerzel = @Kuerzel,
                    nAktiv = @Aktiv,
                    cLagerTyp = @LagerTyp,
                    cStrasse = @Strasse,
                    cPLZ = @PLZ,
                    cOrt = @Ort,
                    cLand = @Land,
                    cAnsprechpartnerName = @AnsprechpartnerName,
                    cAnsprechpartnerTel = @Telefon,
                    cAnsprechpartnerEMail = @Email
                WHERE kWarenLager = @Id",
                new
                {
                    Id = kWarenLager,
                    Name = name,
                    Kuerzel = kuerzel,
                    Aktiv = aktiv ? 1 : 0,
                    LagerTyp = lagerTyp ?? "Standard",
                    Strasse = strasse ?? "",
                    PLZ = plz ?? "",
                    Ort = ort ?? "",
                    Land = land ?? "",
                    AnsprechpartnerName = ansprechpartnerName ?? "",
                    Telefon = telefon ?? "",
                    Email = email ?? ""
                });
        }

        /// <summary>
        /// Warenlager loeschen (nur wenn leer)
        /// </summary>
        public async Task DeleteWarenlagerAsync(int kWarenLager)
        {
            var conn = await GetConnectionAsync();

            // Pruefen ob Bestaende vorhanden
            var bestand = await conn.QuerySingleOrDefaultAsync<int>(
                "SELECT COUNT(*) FROM tLagerbestand WHERE kWarenLager = @Id", new { Id = kWarenLager });

            if (bestand > 0)
                throw new InvalidOperationException($"Das Lager kann nicht geloescht werden. Es enthaelt noch {bestand} Bestandspositionen.");

            // Erst Lagerplaetze loeschen
            await conn.ExecuteAsync("DELETE FROM tWarenLagerPlatz WHERE kWarenLager = @Id", new { Id = kWarenLager });

            // Dann Lager loeschen
            await conn.ExecuteAsync("DELETE FROM tWarenLager WHERE kWarenLager = @Id", new { Id = kWarenLager });
        }

        /// <summary>
        /// Neuen Lagerplatz anlegen
        /// </summary>
        public async Task<int> CreateWarenlagerPlatzAsync(int kWarenLager, string name)
        {
            var conn = await GetConnectionAsync();
            return await conn.QuerySingleAsync<int>(@"
                INSERT INTO tWarenLagerPlatz (kWarenLager, cName, nSort)
                VALUES (@KWarenLager, @Name, 0);
                SELECT SCOPE_IDENTITY();",
                new { KWarenLager = kWarenLager, Name = name });
        }

        public class LagerUebersicht
        {
            public int KWarenLager { get; set; }
            public string? CName { get; set; }
            public string? CKuerzel { get; set; }
            public string? CLagerTyp { get; set; }
            public bool NAktiv { get; set; }
            public int AnzahlPlaetze { get; set; }

            // Adresse (JTL-Style)
            public string? CStrasse { get; set; }
            public string? CPLZ { get; set; }
            public string? COrt { get; set; }
            public string? CLand { get; set; }

            // Ansprechpartner
            public string? CAnsprechpartner { get; set; }  // Zusammengesetzt aus Vorname+Name
            public string? CTelefon { get; set; }
            public string? CEmail { get; set; }
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
                       ISNULL(l.cFirma, 'Unbekannt') AS CLieferantName,
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
                       CASE WHEN we.dMHD > '1900-01-02' THEN we.dMHD ELSE NULL END AS DMHD,
                       CASE WHEN we.dErstellt > '1900-01-02' THEN we.dErstellt ELSE NULL END AS DErstellt,
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
                       lb.cInternerKommentar, lb.cDruckAnmerkung,
                       (SELECT SUM(p.fMenge * p.fEKNetto) FROM dbo.tLieferantenBestellungPos p
                        WHERE p.kLieferantenBestellung = lb.kLieferantenBestellung) AS NettoGesamt,
                       (SELECT COUNT(*) FROM dbo.tLieferantenBestellungPos p
                        WHERE p.kLieferantenBestellung = lb.kLieferantenBestellung) AS AnzahlPositionen,
                       -- Rechnungsadresse
                       ra.cFirma AS RaFirma, ra.cNachname AS RaName, ra.cVorname AS RaVorname,
                       ra.cStrasse AS RaStrasse, ra.cPLZ AS RaPlz, ra.cOrt AS RaOrt, ra.cLandISO AS RaLand,
                       ra.cTel AS RaTel, ra.cMail AS RaMail,
                       -- Lieferadresse
                       la.cFirma AS LaFirma, la.cNachname AS LaName, la.cVorname AS LaVorname,
                       la.cStrasse AS LaStrasse, la.cPLZ AS LaPlz, la.cOrt AS LaOrt, la.cLandISO AS LaLand,
                       la.cTel AS LaTel, la.cMail AS LaMail
                FROM dbo.tLieferantenBestellung lb
                LEFT JOIN dbo.tLieferant l ON l.kLieferant = lb.kLieferant
                LEFT JOIN dbo.tLieferantenBestellungRA ra ON ra.kLieferantenBestellungRA = lb.kLieferantenBestellungRA
                LEFT JOIN dbo.tLieferantenBestellungLA la ON la.kLieferantenBestellungLA = lb.kLieferantenBestellungLA
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

            var kLieferantenBestellung = p.Get<int>("@kLieferantenbestellung");

            // Log in NOVVIA.Log
            if (_logService != null)
            {
                await _logService.LogBewegungsdatenAsync(
                    entityTyp: "Lieferantenbestellung",
                    kEntity: kLieferantenBestellung,
                    entityNr: bestellung.CEigeneBestellnummer,
                    aktion: "Erstellt",
                    beschreibung: $"Lieferantenbestellung erstellt fuer Lieferant {bestellung.KLieferant}",
                    betragNetto: bestellung.Positionen?.Sum(pos => pos.FEKNetto * pos.FMenge) ?? 0);
            }

            return kLieferantenBestellung;
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

        /// <summary>
        /// Wareneingang fuer Lieferantenbestellung buchen
        /// Aktualisiert Lagerbestand und fMengeGeliefert
        /// </summary>
        public async Task WareneingangBuchenAsync(int kLieferantenBestellung, DateTime eingangsDatum, string? lieferscheinNr, List<WareneingangPosition> positionen)
        {
            var conn = await GetConnectionAsync();

            foreach (var pos in positionen.Where(p => p.FMenge > 0))
            {
                // 1. fMengeGeliefert aktualisieren
                await conn.ExecuteAsync(
                    @"UPDATE dbo.tLieferantenBestellungPos
                      SET fMengeGeliefert = ISNULL(fMengeGeliefert, 0) + @fMenge
                      WHERE kLieferantenBestellungPos = @kPos",
                    new { fMenge = pos.FMenge, kPos = pos.KLieferantenBestellungPos });

                // 2. Lagerbestand erhoehen (nur wenn kArtikel > 0)
                if (pos.KArtikel > 0)
                {
                    await conn.ExecuteAsync(
                        @"UPDATE dbo.tArtikel
                          SET fLagerbestand = ISNULL(fLagerbestand, 0) + @fMenge
                          WHERE kArtikel = @kArtikel",
                        new { fMenge = pos.FMenge, kArtikel = pos.KArtikel });

                    // Optional: Chargen-Eintrag wenn ChargenNr angegeben
                    if (!string.IsNullOrWhiteSpace(pos.CChargenNr))
                    {
                        try
                        {
                            await conn.ExecuteAsync(
                                @"INSERT INTO dbo.tArtikelCharge (kArtikel, cChargenNr, dMHD, fMenge, dErstellt)
                                  VALUES (@kArtikel, @cChargenNr, @dMHD, @fMenge, GETDATE())",
                                new { kArtikel = pos.KArtikel, cChargenNr = pos.CChargenNr, dMHD = pos.DMHD, fMenge = pos.FMenge });
                        }
                        catch { /* Charge-Tabelle optional */ }
                    }
                }
            }

            // 3. Bestellstatus pruefen und ggf. auf "Abgeschlossen" setzen
            var alleGeliefert = await conn.ExecuteScalarAsync<int>(
                @"SELECT CASE WHEN EXISTS(
                    SELECT 1 FROM dbo.tLieferantenBestellungPos
                    WHERE kLieferantenBestellung = @kBest AND fMenge > ISNULL(fMengeGeliefert, 0)
                  ) THEN 0 ELSE 1 END",
                new { kBest = kLieferantenBestellung });

            if (alleGeliefert == 1)
            {
                await conn.ExecuteAsync(
                    "UPDATE dbo.tLieferantenBestellung SET nStatus = 50 WHERE kLieferantenBestellung = @kBest",
                    new { kBest = kLieferantenBestellung });
            }
            else
            {
                // Teilgeliefert
                await conn.ExecuteAsync(
                    @"UPDATE dbo.tLieferantenBestellung
                      SET nStatus = CASE WHEN nStatus < 30 THEN 30 ELSE nStatus END
                      WHERE kLieferantenBestellung = @kBest",
                    new { kBest = kLieferantenBestellung });
            }

            _log.Information("Wareneingang fuer Bestellung {KBestellung}: {Anzahl} Positionen, LS: {LieferscheinNr}",
                kLieferantenBestellung, positionen.Count(p => p.FMenge > 0), lieferscheinNr);

            // Log in NOVVIA.Log
            if (_logService != null)
            {
                var gesamtMenge = positionen.Sum(p => p.FMenge);
                await _logService.LogBewegungsdatenAsync(
                    entityTyp: "Wareneingang",
                    kEntity: kLieferantenBestellung,
                    entityNr: lieferscheinNr,
                    aktion: alleGeliefert == 1 ? "Vollstaendig" : "Teillieferung",
                    beschreibung: $"Wareneingang: {positionen.Count(p => p.FMenge > 0)} Positionen, {gesamtMenge:N0} Stueck");
            }
        }

        public class WareneingangPosition
        {
            public int KLieferantenBestellungPos { get; set; }
            public int KArtikel { get; set; }
            public decimal FMenge { get; set; }
            public string? CChargenNr { get; set; }
            public DateTime? DMHD { get; set; }
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

            // Texte
            public string? CInternerKommentar { get; set; }
            public string? CDruckAnmerkung { get; set; }

            // Rechnungsadresse
            public string? RaFirma { get; set; }
            public string? RaName { get; set; }
            public string? RaVorname { get; set; }
            public string? RaStrasse { get; set; }
            public string? RaPlz { get; set; }
            public string? RaOrt { get; set; }
            public string? RaLand { get; set; }
            public string? RaTel { get; set; }
            public string? RaMail { get; set; }

            // Lieferadresse
            public string? LaFirma { get; set; }
            public string? LaName { get; set; }
            public string? LaVorname { get; set; }
            public string? LaStrasse { get; set; }
            public string? LaPlz { get; set; }
            public string? LaOrt { get; set; }
            public string? LaLand { get; set; }
            public string? LaTel { get; set; }
            public string? LaMail { get; set; }

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
        /// Artikel nach Artikelnummer suchen (mit TRIM und case-insensitive)
        /// </summary>
        public async Task<ArtikelRef?> GetArtikelByArtNrAsync(string artNr)
        {
            var conn = await GetConnectionAsync();
            var searchTerm = artNr?.Trim() ?? "";
            const string sql = @"
                SELECT TOP 1 a.kArtikel AS KArtikel, a.cArtNr AS CArtNr,
                       ab.cName AS CName, a.fVKNetto AS FVKNetto,
                       ISNULL(s.fSteuersatz, 19) AS FMwSt,
                       ISNULL(a.kSteuerklasse, 1) AS KSteuerklasse
                FROM dbo.tArtikel a
                LEFT JOIN dbo.tArtikelBeschreibung ab ON a.kArtikel = ab.kArtikel AND ab.kSprache = 1
                LEFT JOIN dbo.tSteuersatz s ON a.kSteuerklasse = s.kSteuerklasse AND s.kSteuerzone = 3
                WHERE LTRIM(RTRIM(a.cArtNr)) = @artNr COLLATE Latin1_General_CI_AS
                   OR LTRIM(RTRIM(a.cBarcode)) = @artNr COLLATE Latin1_General_CI_AS
                   OR LTRIM(RTRIM(a.cHAN)) = @artNr COLLATE Latin1_General_CI_AS";
            return await conn.QueryFirstOrDefaultAsync<ArtikelRef>(sql, new { artNr = searchTerm });
        }

        /// <summary>
        /// Auftrag aus Import-Daten erstellen (schreibt in tAuftrag + tAuftragPosition, dann spAuftragEckdatenBerechnen)
        /// </summary>
        public async Task<AuftragImportResult> CreateAuftragFromImportAsync(string kundenNr, List<AuftragImportPosition> positionen, string zusatztext, bool ueberPositionen, DateTime? mindestMHD = null)
        {
            var conn = await GetConnectionAsync();

            // Kunde mit Adresse suchen (inkl. JTL-Pflichtfelder)
            var kunde = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT k.kKunde, k.kSprache, k.kKundenGruppe, k.kZahlungsart, k.cKundenNr, k.nZahlungsziel,
                       a.cFirma, a.cAnrede, a.cTitel, a.cVorname, a.cName, a.cStrasse,
                       a.cAdressZusatz, a.cPLZ, a.cOrt, a.cLand, a.cISO, a.cBundesland,
                       a.cTel, a.cFax, a.cMobil, a.cMail
                FROM dbo.tKunde k
                LEFT JOIN dbo.tAdresse a ON a.kKunde = k.kKunde AND a.nStandard = 1
                WHERE k.cKundenNr = @nr OR CAST(k.kKunde AS VARCHAR) = @nr",
                new { nr = kundenNr });

            if (kunde == null)
            {
                throw new Exception($"Kunde mit Nr. {kundenNr} nicht gefunden");
            }

            // Auftragsnummer aus tLaufendeNummern holen (kLaufendeNummer = 3 für Aufträge)
            var laufendeNummer = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT nNummer, cPrefix, cSuffix
                FROM dbo.tLaufendeNummern
                WHERE kLaufendeNummer = 3");

            int nextNr = (laufendeNummer?.nNummer ?? 10000) + 1;

            // Nummer in tLaufendeNummern hochzählen
            await conn.ExecuteAsync(@"
                UPDATE dbo.tLaufendeNummern
                SET nNummer = @nextNr
                WHERE kLaufendeNummer = 3",
                new { nextNr });

            // Auftragsnummer zusammensetzen (mit optionalem Prefix/Suffix)
            var prefix = (string?)laufendeNummer?.cPrefix ?? "";
            var suffix = (string?)laufendeNummer?.cSuffix ?? "";
            var neueAuftragsNr = $"{prefix}{nextNr}{suffix}";

            // 1. Auftrag anlegen (JTL-kompatibel mit allen Pflichtfeldern)
            var auftragId = await conn.QuerySingleAsync<int>(@"
                INSERT INTO Verkauf.tAuftrag (
                    kBenutzer, kBenutzerErstellt, kKunde, cAuftragsNr, nType, dErstellt,
                    nBeschreibung, cWaehrung, fFaktor, kFirmaHistory, kSprache,
                    nSteuereinstellung, nHatUpload, fZusatzGewicht,
                    cVersandlandISO, cVersandlandWaehrung, fVersandlandWaehrungFaktor,
                    nStorno, nKomplettAusgeliefert, nLieferPrioritaet, nPremiumVersand,
                    nIstExterneRechnung, nIstReadOnly, nArchiv, nReserviert,
                    nAuftragStatus, fFinanzierungskosten, nPending, nSteuersonderbehandlung,
                    kPlattform, kVersandArt, nZahlungszielTage, kZahlungsart, cKundenNr, kKundengruppe
                ) VALUES (
                    1, 1, @KundeId, @AuftragsNr, 1, GETDATE(),
                    0, 'EUR', 1, 1, ISNULL(@Sprache, 1),
                    0, 0, 0,
                    ISNULL(@LandISO, 'DE'), 'EUR', 1,
                    0, 0, 0, 0,
                    0, 0, 0, 0,
                    0, 0, 0, 0,
                    1, 10, @Zahlungsziel, @Zahlungsart, @KundenNr, @Kundengruppe
                );
                SELECT SCOPE_IDENTITY();",
                new {
                    KundeId = (int)kunde.kKunde,
                    AuftragsNr = neueAuftragsNr,
                    Sprache = (int?)kunde.kSprache,
                    LandISO = (string?)kunde.cISO,
                    Zahlungsziel = (int?)kunde.nZahlungsziel ?? 14,
                    Zahlungsart = (int?)kunde.kZahlungsart ?? 2,
                    KundenNr = (string?)kunde.cKundenNr,
                    Kundengruppe = (int?)kunde.kKundenGruppe
                });

            // 2. Adressen anlegen (Rechnungsadresse nTyp=0, Lieferadresse nTyp=1)
            for (int adressTyp = 0; adressTyp <= 1; adressTyp++)
            {
                await conn.ExecuteAsync(@"
                    INSERT INTO Verkauf.tAuftragAdresse (
                        kAuftrag, kKunde, cFirma, cAnrede, cTitel, cVorname, cName,
                        cStrasse, cPLZ, cOrt, cLand, cISO, cBundesland,
                        cTel, cFax, cMobil, cMail, cAdressZusatz, nTyp
                    ) VALUES (
                        @AuftragId, @KundeId, @Firma, @Anrede, @Titel, @Vorname, @Name,
                        @Strasse, @PLZ, @Ort, @Land, @ISO, @Bundesland,
                        @Tel, @Fax, @Mobil, @Mail, @Zusatz, @Typ
                    )",
                    new {
                        AuftragId = auftragId,
                        KundeId = (int)kunde.kKunde,
                        Firma = (string?)kunde.cFirma ?? "",
                        Anrede = (string?)kunde.cAnrede ?? "",
                        Titel = (string?)kunde.cTitel ?? "",
                        Vorname = (string?)kunde.cVorname ?? "",
                        Name = (string?)kunde.cName ?? "",
                        Strasse = (string?)kunde.cStrasse ?? "",
                        PLZ = (string?)kunde.cPLZ ?? "",
                        Ort = (string?)kunde.cOrt ?? "",
                        Land = (string?)kunde.cLand ?? "Deutschland",
                        ISO = (string?)kunde.cISO ?? "DE",
                        Bundesland = (string?)kunde.cBundesland ?? "",
                        Tel = (string?)kunde.cTel ?? "",
                        Fax = (string?)kunde.cFax ?? "",
                        Mobil = (string?)kunde.cMobil ?? "",
                        Mail = (string?)kunde.cMail ?? "",
                        Zusatz = (string?)kunde.cAdressZusatz ?? "",
                        Typ = adressTyp
                    });
            }

            // 3. Positionen anlegen
            int posSort = 0;
            var nichtGefunden = new List<string>();

            foreach (var pos in positionen)
            {
                var artikel = await GetArtikelByArtNrAsync(pos.ArtNr);
                if (artikel == null)
                {
                    nichtGefunden.Add(pos.ArtNr);
                    continue;
                }

                var preis = pos.Preis > 0 ? pos.Preis : artikel.FVKNetto;
                var mwst = artikel.FMwSt;
                // Steuerschlüssel ableiten: 3=19%, 2=7%, 1=steuerfrei
                int steuerschluessel = mwst >= 19 ? 3 : (mwst >= 7 ? 2 : 1);

                await conn.ExecuteAsync(@"
                    INSERT INTO Verkauf.tAuftragPosition (
                        kArtikel, kAuftrag, cArtNr, nReserviert, cName, cHinweis,
                        fAnzahl, fEkNetto, fVkNetto, fRabatt, fMwSt, nSort,
                        cNameStandard, nType, cEinheit, nHatUpload, fFaktor,
                        kSteuerklasse, kSteuerschluessel
                    ) VALUES (
                        @ArtikelId, @AuftragId, @ArtNr, 0, @Name, @Hinweis,
                        @Menge, 0, @Preis, 0, @MwSt, @Sort,
                        @Name, 1, 'Stk', 0, 1,
                        @Steuerklasse, @Steuerschluessel
                    )",
                    new {
                        ArtikelId = artikel.KArtikel,
                        AuftragId = auftragId,
                        ArtNr = artikel.CArtNr,
                        Name = artikel.CName ?? "",
                        Hinweis = "",
                        Menge = pos.Menge,
                        Preis = preis,
                        MwSt = mwst,
                        Sort = posSort++,
                        Steuerklasse = artikel.KSteuerklasse,
                        Steuerschluessel = steuerschluessel
                    });
            }

            // Warnung loggen wenn Artikel nicht gefunden
            if (nichtGefunden.Any())
            {
                _log.Warning("Auftrag {AuftragId}: {Anzahl} Artikel nicht gefunden: {Artikel}",
                    auftragId, nichtGefunden.Count, string.Join(", ", nichtGefunden.Take(10)));
            }

            // 4. Eckdaten berechnen via SP
            var dt = new DataTable();
            dt.Columns.Add("kAuftrag", typeof(int));
            dt.Rows.Add(auftragId);

            var p = new DynamicParameters();
            p.Add("@Auftrag", dt.AsTableValuedParameter("Verkauf.TYPE_spAuftragEckdatenBerechnen"));
            await conn.ExecuteAsync("Verkauf.spAuftragEckdatenBerechnen", p, commandType: CommandType.StoredProcedure);

            return new AuftragImportResult
            {
                AuftragId = auftragId,
                AuftragsNr = neueAuftragsNr,
                PositionenAngelegt = posSort,
                NichtGefundeneArtikel = nichtGefunden
            };
        }

        /// <summary>
        /// Erstellt einen neuen Auftrag mit Kunde-ID, Positionen und optionalen Parametern
        /// </summary>
        public async Task<AuftragImportResult> CreateAuftragAsync(
            int kundeId,
            List<AuftragImportPosition> positionen,
            int versandartId = 10,
            int zahlungsartId = 2,
            int zahlungsziel = 14,
            string? anmerkung = null,
            string? drucktext = null,
            string? hinweis = null)
        {
            var conn = await GetConnectionAsync();

            // Kunde mit Adresse laden
            var kunde = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT k.kKunde, k.kSprache, k.kKundenGruppe, k.kZahlungsart, k.cKundenNr, k.nZahlungsziel,
                       a.cFirma, a.cAnrede, a.cTitel, a.cVorname, a.cName, a.cStrasse,
                       a.cAdressZusatz, a.cPLZ, a.cOrt, a.cLand, a.cISO, a.cBundesland,
                       a.cTel, a.cFax, a.cMobil, a.cMail
                FROM dbo.tKunde k
                LEFT JOIN dbo.tAdresse a ON a.kKunde = k.kKunde AND a.nStandard = 1
                WHERE k.kKunde = @KundeId",
                new { KundeId = kundeId });

            if (kunde == null)
            {
                throw new Exception($"Kunde mit ID {kundeId} nicht gefunden");
            }

            // Auftragsnummer aus tLaufendeNummern holen
            var laufendeNummer = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT nNummer, cPrefix, cSuffix FROM dbo.tLaufendeNummern WHERE kLaufendeNummer = 3");

            int nextNr = (laufendeNummer?.nNummer ?? 10000) + 1;
            await conn.ExecuteAsync(@"UPDATE dbo.tLaufendeNummern SET nNummer = @nextNr WHERE kLaufendeNummer = 3", new { nextNr });

            var prefix = (string?)laufendeNummer?.cPrefix ?? "";
            var suffix = (string?)laufendeNummer?.cSuffix ?? "";
            var neueAuftragsNr = $"{prefix}{nextNr}{suffix}";

            // Auftrag anlegen
            var auftragId = await conn.QuerySingleAsync<int>(@"
                INSERT INTO Verkauf.tAuftrag (
                    kBenutzer, kBenutzerErstellt, kKunde, cAuftragsNr, nType, dErstellt,
                    nBeschreibung, cWaehrung, fFaktor, kFirmaHistory, kSprache,
                    nSteuereinstellung, nHatUpload, fZusatzGewicht,
                    cVersandlandISO, cVersandlandWaehrung, fVersandlandWaehrungFaktor,
                    nStorno, nKomplettAusgeliefert, nLieferPrioritaet, nPremiumVersand,
                    nIstExterneRechnung, nIstReadOnly, nArchiv, nReserviert,
                    nAuftragStatus, fFinanzierungskosten, nPending, nSteuersonderbehandlung,
                    kPlattform, kVersandArt, nZahlungszielTage, kZahlungsart, cKundenNr, kKundengruppe
                ) VALUES (
                    1, 1, @KundeId, @AuftragsNr, 1, GETDATE(),
                    0, 'EUR', 1, 1, ISNULL(@Sprache, 1),
                    0, 0, 0,
                    ISNULL(@LandISO, 'DE'), 'EUR', 1,
                    0, 0, 0, 0,
                    0, 0, 0, 0,
                    0, 0, 0, 0,
                    1, @VersandartId, @Zahlungsziel, @ZahlungsartId, @KundenNr, @Kundengruppe
                );
                SELECT SCOPE_IDENTITY();",
                new {
                    KundeId = kundeId,
                    AuftragsNr = neueAuftragsNr,
                    Sprache = (int?)kunde.kSprache,
                    LandISO = (string?)kunde.cISO,
                    Zahlungsziel = zahlungsziel,
                    ZahlungsartId = zahlungsartId,
                    VersandartId = versandartId,
                    KundenNr = (string?)kunde.cKundenNr,
                    Kundengruppe = (int?)kunde.kKundenGruppe
                });

            // Adressen anlegen
            for (int adressTyp = 0; adressTyp <= 1; adressTyp++)
            {
                await conn.ExecuteAsync(@"
                    INSERT INTO Verkauf.tAuftragAdresse (
                        kAuftrag, kKunde, cFirma, cAnrede, cTitel, cVorname, cName,
                        cStrasse, cPLZ, cOrt, cLand, cISO, cBundesland,
                        cTel, cFax, cMobil, cMail, cAdressZusatz, nTyp
                    ) VALUES (
                        @AuftragId, @KundeId, @Firma, @Anrede, @Titel, @Vorname, @Name,
                        @Strasse, @PLZ, @Ort, @Land, @ISO, @Bundesland,
                        @Tel, @Fax, @Mobil, @Mail, @Zusatz, @Typ
                    )",
                    new {
                        AuftragId = auftragId,
                        KundeId = kundeId,
                        Firma = (string?)kunde.cFirma ?? "",
                        Anrede = (string?)kunde.cAnrede ?? "",
                        Titel = (string?)kunde.cTitel ?? "",
                        Vorname = (string?)kunde.cVorname ?? "",
                        Name = (string?)kunde.cName ?? "",
                        Strasse = (string?)kunde.cStrasse ?? "",
                        PLZ = (string?)kunde.cPLZ ?? "",
                        Ort = (string?)kunde.cOrt ?? "",
                        Land = (string?)kunde.cLand ?? "Deutschland",
                        ISO = (string?)kunde.cISO ?? "DE",
                        Bundesland = (string?)kunde.cBundesland ?? "",
                        Tel = (string?)kunde.cTel ?? "",
                        Fax = (string?)kunde.cFax ?? "",
                        Mobil = (string?)kunde.cMobil ?? "",
                        Mail = (string?)kunde.cMail ?? "",
                        Zusatz = (string?)kunde.cAdressZusatz ?? "",
                        Typ = adressTyp
                    });
            }

            // Texte speichern
            if (!string.IsNullOrEmpty(anmerkung) || !string.IsNullOrEmpty(drucktext) || !string.IsNullOrEmpty(hinweis))
            {
                await conn.ExecuteAsync(@"
                    INSERT INTO Verkauf.tAuftragText (kAuftrag, cAnmerkung, cDrucktext, cHinweis)
                    VALUES (@AuftragId, @Anmerkung, @Drucktext, @Hinweis)",
                    new {
                        AuftragId = auftragId,
                        Anmerkung = anmerkung ?? "",
                        Drucktext = drucktext ?? "",
                        Hinweis = hinweis ?? ""
                    });
            }

            // Positionen anlegen
            int posSort = 0;
            var nichtGefunden = new List<string>();

            foreach (var pos in positionen)
            {
                var artikel = await GetArtikelByArtNrAsync(pos.ArtNr);
                if (artikel == null)
                {
                    nichtGefunden.Add(pos.ArtNr);
                    continue;
                }

                var preis = pos.Preis > 0 ? pos.Preis : artikel.FVKNetto;
                var mwst = artikel.FMwSt;
                int steuerschluessel = mwst >= 19 ? 3 : (mwst >= 7 ? 2 : 1);

                await conn.ExecuteAsync(@"
                    INSERT INTO Verkauf.tAuftragPosition (
                        kArtikel, kAuftrag, cArtNr, nReserviert, cName, cHinweis,
                        fAnzahl, fEkNetto, fVkNetto, fRabatt, fMwSt, nSort,
                        cNameStandard, nType, cEinheit, nHatUpload, fFaktor,
                        kSteuerklasse, kSteuerschluessel
                    ) VALUES (
                        @ArtikelId, @AuftragId, @ArtNr, 0, @Name, @Hinweis,
                        @Menge, 0, @Preis, 0, @MwSt, @Sort,
                        @Name, 1, 'Stk', 0, 1,
                        @Steuerklasse, @Steuerschluessel
                    )",
                    new {
                        ArtikelId = artikel.KArtikel,
                        AuftragId = auftragId,
                        ArtNr = artikel.CArtNr,
                        Name = artikel.CName ?? "",
                        Hinweis = "",
                        Menge = pos.Menge,
                        Preis = preis,
                        MwSt = mwst,
                        Sort = posSort++,
                        Steuerklasse = artikel.KSteuerklasse,
                        Steuerschluessel = steuerschluessel
                    });
            }

            // Eckdaten berechnen
            var dt = new DataTable();
            dt.Columns.Add("kAuftrag", typeof(int));
            dt.Rows.Add(auftragId);
            var p = new DynamicParameters();
            p.Add("@Auftrag", dt.AsTableValuedParameter("Verkauf.TYPE_spAuftragEckdatenBerechnen"));
            await conn.ExecuteAsync("Verkauf.spAuftragEckdatenBerechnen", p, commandType: CommandType.StoredProcedure);

            return new AuftragImportResult
            {
                AuftragId = auftragId,
                AuftragsNr = neueAuftragsNr,
                PositionenAngelegt = posSort,
                NichtGefundeneArtikel = nichtGefunden
            };
        }

        public class AuftragImportResult
        {
            public int AuftragId { get; set; }
            public string AuftragsNr { get; set; } = "";
            public int PositionenAngelegt { get; set; }
            public List<string> NichtGefundeneArtikel { get; set; } = new();
            public bool Success => AuftragId > 0;
        }

        public class ArtikelRef
        {
            public int KArtikel { get; set; }
            public string CArtNr { get; set; } = "";
            public string CName { get; set; } = "";
            public decimal FVKNetto { get; set; }
            public decimal FMwSt { get; set; }
            public int KSteuerklasse { get; set; } = 1;
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

        #region Einstellungen / Stammdaten

        // --- Firmendaten ---
        public class FirmendatenDetail
        {
            public int KFirma { get; set; }
            public string? CFirma { get; set; }
            public string? CZusatz { get; set; }
            public string? CStrasse { get; set; }
            public string? CHausNr { get; set; }
            public string? CPLZ { get; set; }
            public string? COrt { get; set; }
            public string? CLand { get; set; }
            public string? CTel { get; set; }
            public string? CFax { get; set; }
            public string? CMail { get; set; }
            public string? CWWW { get; set; }
            public string? CUSTID { get; set; }
            public string? CSteuerNr { get; set; }
            public string? CHReg { get; set; }
            public string? CGF { get; set; }
            public string? CBank { get; set; }
            public string? CBIC { get; set; }
            public string? CIBAN { get; set; }
        }

        public async Task<FirmendatenDetail?> GetFirmendatenAsync()
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryFirstOrDefaultAsync<FirmendatenDetail>(@"
                SELECT f.kFirma AS KFirma, f.cName AS CFirma, ISNULL(f.cUnternehmer, '') AS CZusatz,
                       f.cStrasse AS CStrasse, '' AS CHausNr, f.cPLZ AS CPLZ, f.cOrt AS COrt, f.cLand AS CLand,
                       f.cTel AS CTel, f.cFax AS CFax, f.cEMail AS CMail, f.cWWW AS CWWW,
                       ISNULL(u.cUStId, '') AS CUSTID, f.cSteuerNr AS CSteuerNr,
                       '' AS CHReg, ISNULL(f.cKontoInhaber, '') AS CGF,
                       ISNULL(f.cBank, '') AS CBank, ISNULL(f.cBIC, '') AS CBIC, ISNULL(f.cIBAN, '') AS CIBAN
                FROM dbo.tFirma f
                LEFT JOIN dbo.tFirmaUStIdNr u ON f.kFirma = u.kFirma
                WHERE f.kFirma = 1");
        }

        public async Task UpdateFirmendatenAsync(FirmendatenDetail firma)
        {
            var conn = await GetConnectionAsync();

            // Firma-Stammdaten
            await conn.ExecuteAsync(@"
                UPDATE dbo.tFirma SET
                    cName = @CFirma, cUnternehmer = @CZusatz,
                    cStrasse = @CStrasse, cPLZ = @CPLZ, cOrt = @COrt, cLand = @CLand,
                    cTel = @CTel, cFax = @CFax, cEMail = @CMail, cWWW = @CWWW,
                    cSteuerNr = @CSteuerNr, cBank = @CBank, cBIC = @CBIC, cIBAN = @CIBAN,
                    cKontoInhaber = @CGF
                WHERE kFirma = 1", firma);

            // USt-ID aktualisieren/einfügen
            var existsUstId = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM tFirmaUStIdNr WHERE kFirma = 1");
            if (existsUstId > 0)
            {
                await conn.ExecuteAsync(
                    "UPDATE tFirmaUStIdNr SET cUStId = @CUSTID WHERE kFirma = 1", firma);
            }
            else if (!string.IsNullOrEmpty(firma.CUSTID))
            {
                await conn.ExecuteAsync(
                    "INSERT INTO tFirmaUStIdNr (kFirma, cLandISO, cUStId, nAuchAlsVersandlandBetrachten) VALUES (1, 'DE', @CUSTID, 1)", firma);
            }
        }

        // --- Firma Eigene Felder ---
        public class FirmaEigenesFeldDetail
        {
            public int KFirmaEigenesFeld { get; set; }
            public int KAttribut { get; set; }
            public string? CName { get; set; }
            public string? CWertVarchar { get; set; }
            public int? NWertInt { get; set; }
            public decimal? FWertDecimal { get; set; }
            public DateTime? DWertDateTime { get; set; }
        }

        public async Task<IEnumerable<FirmaEigenesFeldDetail>> GetFirmaEigeneFelderAsync()
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<FirmaEigenesFeldDetail>(@"
                SELECT ef.kFirmaEigenesFeld AS KFirmaEigenesFeld, ef.kAttribut AS KAttribut,
                       ISNULL(asp.cName, 'Attribut ' + CAST(ef.kAttribut AS NVARCHAR)) AS CName,
                       ef.cWertVarchar AS CWertVarchar, ef.nWertInt AS NWertInt,
                       ef.fWertDecimal AS FWertDecimal, ef.dWertDateTime AS DWertDateTime
                FROM Firma.tFirmaEigenesFeld ef
                LEFT JOIN tAttributSprache asp ON ef.kAttribut = asp.kAttribut AND asp.kSprache = 1
                WHERE ef.kFirma = 1
                ORDER BY asp.cName");
        }

        // --- Kundengruppen ---
        public class KundengruppeDetail
        {
            public int KKundenGruppe { get; set; }
            public string? CName { get; set; }
            public decimal FRabatt { get; set; }
            public int NNettoPreise { get; set; }
        }

        public async Task<IEnumerable<KundengruppeDetail>> GetKundengruppenDetailAsync()
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<KundengruppeDetail>(@"
                SELECT kKundenGruppe AS KKundenGruppe, cName AS CName,
                       ISNULL(fRabatt, 0) AS FRabatt, ISNULL(nNettoPreise, 0) AS NNettoPreise
                FROM dbo.tKundenGruppe ORDER BY cName");
        }

        public async Task CreateKundengruppeAsync(string name, decimal rabatt)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync(@"
                INSERT INTO dbo.tKundenGruppe (cName, fRabatt, nNettoPreise) VALUES (@Name, @Rabatt, 0)",
                new { Name = name, Rabatt = rabatt });

            if (_logService != null)
                await _logService.LogStammdatenAsync(entityTyp: "Kundengruppe", kEntity: 0, entityNr: name, aktion: "Erstellt",
                    beschreibung: $"Kundengruppe '{name}' erstellt (Rabatt: {rabatt}%)");
        }

        public async Task UpdateKundengruppeAsync(int id, string name, decimal rabatt)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync(@"
                UPDATE dbo.tKundenGruppe SET cName = @Name, fRabatt = @Rabatt WHERE kKundenGruppe = @Id",
                new { Id = id, Name = name, Rabatt = rabatt });

            if (_logService != null)
                await _logService.LogStammdatenAsync(entityTyp: "Kundengruppe", kEntity: id, entityNr: name, aktion: "Geaendert",
                    beschreibung: $"Kundengruppe '{name}' geaendert");
        }

        public async Task DeleteKundengruppeAsync(int id)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync("DELETE FROM dbo.tKundenGruppe WHERE kKundenGruppe = @Id", new { Id = id });

            if (_logService != null)
                await _logService.LogStammdatenAsync(entityTyp: "Kundengruppe", kEntity: id, entityNr: null, aktion: "Geloescht",
                    beschreibung: $"Kundengruppe {id} geloescht");
        }

        // --- Kundenkategorien ---
        public async Task CreateKundenkategorieAsync(string name)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync("INSERT INTO dbo.tKundenKategorie (cName) VALUES (@Name)", new { Name = name });

            if (_logService != null)
                await _logService.LogStammdatenAsync(entityTyp: "Kundenkategorie", kEntity: 0, entityNr: name, aktion: "Erstellt",
                    beschreibung: $"Kundenkategorie '{name}' erstellt");
        }

        public async Task UpdateKundenkategorieAsync(int id, string name)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync("UPDATE dbo.tKundenKategorie SET cName = @Name WHERE kKundenKategorie = @Id",
                new { Id = id, Name = name });

            if (_logService != null)
                await _logService.LogStammdatenAsync(entityTyp: "Kundenkategorie", kEntity: id, entityNr: name, aktion: "Geaendert",
                    beschreibung: $"Kundenkategorie '{name}' geaendert");
        }

        public async Task DeleteKundenkategorieAsync(int id)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync("DELETE FROM dbo.tKundenKategorie WHERE kKundenKategorie = @Id", new { Id = id });

            if (_logService != null)
                await _logService.LogStammdatenAsync(entityTyp: "Kundenkategorie", kEntity: id, entityNr: null, aktion: "Geloescht",
                    beschreibung: $"Kundenkategorie {id} geloescht");
        }

        // --- Zahlungsarten ---
        public class ZahlungsartDetail
        {
            public int KZahlungsart { get; set; }
            public string? CName { get; set; }
            public string? CModulId { get; set; }
            public int NAktiv { get; set; }
        }

        public async Task<IEnumerable<ZahlungsartDetail>> GetZahlungsartenDetailAsync()
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<ZahlungsartDetail>(@"
                SELECT kZahlungsart AS KZahlungsart, cName AS CName,
                       ISNULL(cPaymentOption, '') AS CModulId,
                       CAST(ISNULL(nAktiv, 1) AS INT) AS NAktiv
                FROM dbo.tZahlungsArt ORDER BY cName");
        }

        public async Task CreateZahlungsartAsync(string name, string modulId)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync(@"
                INSERT INTO dbo.tZahlungsArt (cName, cPaymentOption, nAktiv) VALUES (@Name, @ModulId, 1)",
                new { Name = name, ModulId = modulId });
        }

        public async Task UpdateZahlungsartAsync(int id, string name, string modulId)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync(@"
                UPDATE dbo.tZahlungsArt SET cName = @Name, cPaymentOption = @ModulId WHERE kZahlungsart = @Id",
                new { Id = id, Name = name, ModulId = modulId });
        }

        public async Task DeleteZahlungsartAsync(int id)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync("DELETE FROM dbo.tZahlungsArt WHERE kZahlungsart = @Id", new { Id = id });
        }

        // --- Versandarten ---
        public class VersandartDetail
        {
            public int KVersandart { get; set; }
            public string? CName { get; set; }
            public string? CLieferzeitText { get; set; }
            public decimal FKosten { get; set; }
        }

        public async Task<IEnumerable<VersandartDetail>> GetVersandartenDetailAsync()
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<VersandartDetail>(@"
                SELECT kVersandArt AS KVersandart, cName AS CName,
                       ISNULL(cDruckText, '') AS CLieferzeitText,
                       ISNULL(fPrice, 0) AS FKosten
                FROM dbo.tVersandArt ORDER BY cName");
        }

        public async Task CreateVersandartAsync(string name, decimal kosten)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync(@"
                INSERT INTO dbo.tVersandArt (cName, fPrice, cAktiv) VALUES (@Name, @Kosten, 'Y')",
                new { Name = name, Kosten = kosten });
        }

        public async Task UpdateVersandartAsync(int id, string name, decimal kosten)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync(@"
                UPDATE dbo.tVersandArt SET cName = @Name, fPrice = @Kosten WHERE kVersandArt = @Id",
                new { Id = id, Name = name, Kosten = kosten });
        }

        public async Task DeleteVersandartAsync(int id)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync("DELETE FROM dbo.tVersandArt WHERE kVersandArt = @Id", new { Id = id });
        }

        // --- Steuern (nur Ansicht) ---
        public class SteuerDetail
        {
            public int KSteuer { get; set; }
            public string? CName { get; set; }
            public decimal FSteuersatz { get; set; }
            public string? CISO { get; set; }
            public int NStandard { get; set; }
        }

        public async Task<IEnumerable<SteuerDetail>> GetSteuernAsync()
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<SteuerDetail>(@"
                SELECT sk.kSteuerklasse AS KSteuer, sk.cName AS CName,
                       ISNULL(ss.fSteuersatz, 0) AS FSteuersatz,
                       'DE' AS CISO, sk.nStandard AS NStandard
                FROM dbo.tSteuerklasse sk
                LEFT JOIN dbo.tSteuersatz ss ON sk.kSteuerklasse = ss.kSteuerklasse AND ss.kSteuerzone = 3
                ORDER BY sk.cName");
        }

        // --- Konten (nur Ansicht - Steuersammelkonten) ---
        public class KontoDetail
        {
            public string? CKontoNr { get; set; }
            public string? CName { get; set; }
            public string? CTyp { get; set; }
        }

        public async Task<IEnumerable<KontoDetail>> GetKontenAsync()
        {
            var conn = await GetConnectionAsync();
            // Zeige Steuersammelkonten wenn vorhanden
            return await conn.QueryAsync<KontoDetail>(@"
                SELECT CAST(kSteuersatz AS NVARCHAR) AS CKontoNr,
                       'Steuersatz ' + CAST(fSteuersatz AS NVARCHAR) + '%' AS CName,
                       'Steuer' AS CTyp
                FROM dbo.tSteuersatz
                WHERE kSteuerzone = 3
                ORDER BY fSteuersatz DESC");
        }

        #endregion

        #region Eigene Felder

        // DTOs für Eigene Felder
        public class EigenesFeldDefinition
        {
            public int KAttribut { get; set; }
            public string CName { get; set; } = "";
            public string? CBeschreibung { get; set; }
            public int NFeldTyp { get; set; }  // 1=Int, 2=Decimal, 3=Text, 4=DateTime (aus tFeldTyp.nDatenTyp)
            public int NSortierung { get; set; }
            public bool NAktiv { get; set; } = true;
            public string FeldTypName => NFeldTyp switch
            {
                1 => "Ganzzahl",   // nDatenTyp 0 = Int
                2 => "Dezimal",    // nDatenTyp 1 = Decimal
                3 => "Text",       // nDatenTyp 2 = Text/Varchar
                4 => "Datum",      // nDatenTyp 3 = DateTime
                _ => "Text"
            };
        }

        public class EigenesFeldWert
        {
            public int KWert { get; set; }
            public int KEntity { get; set; }  // z.B. kKunde, kArtikel, etc.
            public int KAttribut { get; set; }
            public string? CAttributName { get; set; }
            public int NFeldTyp { get; set; }
            public string? CWertVarchar { get; set; }
            public int? NWertInt { get; set; }
            public decimal? FWertDecimal { get; set; }
            public DateTime? DWertDateTime { get; set; }

            public object? Wert => NFeldTyp switch
            {
                1 => NWertInt,         // nDatenTyp 0 = Int
                2 => FWertDecimal,     // nDatenTyp 1 = Decimal
                3 => CWertVarchar,     // nDatenTyp 2 = Text
                4 => DWertDateTime,    // nDatenTyp 3 = DateTime
                _ => CWertVarchar
            };

            // Formatierte Anzeige des Werts (für Checkbox: Ja/Nein)
            public string WertAnzeige
            {
                get
                {
                    return NFeldTyp switch
                    {
                        1 => NWertInt switch  // Ganzzahl - könnte Checkbox sein (0/1)
                        {
                            1 => "Ja",
                            0 => "Nein",
                            _ => NWertInt?.ToString() ?? ""
                        },
                        2 => FWertDecimal?.ToString("N2") ?? "",
                        3 => CWertVarchar ?? "",
                        4 => DWertDateTime?.ToString("dd.MM.yyyy") ?? "",
                        _ => CWertVarchar ?? ""
                    };
                }
            }

            // Checkbox-Wert (true/false) für DataGridCheckBoxColumn
            public bool IstCheckbox => NFeldTyp == 1 && (NWertInt == 0 || NWertInt == 1);
            public bool CheckboxWert => NWertInt == 1;
        }

        // ===== LIEFERANT Eigene Felder (NOVVIA-Tabellen) =====

        public async Task<IEnumerable<EigenesFeldDefinition>> GetLieferantAttributeAsync()
        {
            try
            {
                var conn = await GetConnectionAsync();
                // Alle Lieferant-Attribute laden (ohne nAktiv Filter, da Benutzer evtl. noch keine aktiven hat)
                var result = await conn.QueryAsync<EigenesFeldDefinition>(@"
                    SELECT kLieferantAttribut AS KAttribut, cName AS CName, cBeschreibung AS CBeschreibung,
                           nFeldTyp AS NFeldTyp, nSortierung AS NSortierung, ISNULL(nAktiv, 1) AS NAktiv
                    FROM NOVVIA.LieferantAttribut
                    ORDER BY nSortierung, cName");
                _log.Information("NOVVIA.LieferantAttribut: {Count} Einträge gefunden", result.Count());
                return result;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "NOVVIA.LieferantAttribut Tabelle nicht gefunden - bitte Setup-EigeneFelderLieferant.sql ausführen");
                return Enumerable.Empty<EigenesFeldDefinition>();
            }
        }

        public async Task<IEnumerable<EigenesFeldWert>> GetLieferantEigeneFelderAsync(int kLieferant)
        {
            try
            {
                var conn = await GetConnectionAsync();
                return await conn.QueryAsync<EigenesFeldWert>(@"
                    SELECT ef.kLieferantEigenesFeld AS KWert, ef.kLieferant AS KEntity,
                           ef.kLieferantAttribut AS KAttribut, a.cName AS CAttributName, a.nFeldTyp AS NFeldTyp,
                           ef.cWertVarchar AS CWertVarchar, ef.nWertInt AS NWertInt,
                           ef.fWertDecimal AS FWertDecimal, ef.dWertDateTime AS DWertDateTime
                    FROM NOVVIA.LieferantEigenesFeld ef
                    JOIN NOVVIA.LieferantAttribut a ON ef.kLieferantAttribut = a.kLieferantAttribut
                    WHERE ef.kLieferant = @kLieferant
                    ORDER BY a.nSortierung, a.cName", new { kLieferant });
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "NOVVIA.LieferantEigenesFeld Tabelle nicht gefunden");
                return Enumerable.Empty<EigenesFeldWert>();
            }
        }

        public async Task SaveLieferantEigenesFeldAsync(int kLieferant, int kAttribut, object? wert)
        {
            var conn = await GetConnectionAsync();
            var attr = await conn.QueryFirstOrDefaultAsync<EigenesFeldDefinition>(@"
                SELECT nFeldTyp AS NFeldTyp FROM NOVVIA.LieferantAttribut WHERE kLieferantAttribut = @kAttribut",
                new { kAttribut });

            if (attr == null) return;

            await conn.ExecuteAsync("NOVVIA.spLieferantEigenesFeldSpeichern",
                new
                {
                    kLieferant,
                    kLieferantAttribut = kAttribut,
                    cWertVarchar = attr.NFeldTyp == 1 ? wert?.ToString() : null,
                    nWertInt = attr.NFeldTyp == 2 ? (int?)Convert.ToInt32(wert) : null,
                    fWertDecimal = attr.NFeldTyp == 3 ? (decimal?)Convert.ToDecimal(wert) : null,
                    dWertDateTime = attr.NFeldTyp == 4 ? (DateTime?)wert : null
                },
                commandType: CommandType.StoredProcedure);
        }

        public async Task<int> SaveLieferantAttributAsync(EigenesFeldDefinition attr)
        {
            var conn = await GetConnectionAsync();
            var result = await conn.QueryFirstOrDefaultAsync<int>("NOVVIA.spLieferantAttributSpeichern",
                new
                {
                    kLieferantAttribut = attr.KAttribut > 0 ? (int?)attr.KAttribut : null,
                    cName = attr.CName,
                    cBeschreibung = attr.CBeschreibung,
                    nFeldTyp = attr.NFeldTyp,
                    nSortierung = attr.NSortierung,
                    nAktiv = attr.NAktiv
                },
                commandType: CommandType.StoredProcedure);
            return result;
        }

        public async Task DeleteLieferantAttributAsync(int kAttribut)
        {
            var conn = await GetConnectionAsync();
            // Erst Werte löschen, dann Attribut
            await conn.ExecuteAsync("DELETE FROM NOVVIA.LieferantEigenesFeld WHERE kLieferantAttribut = @kAttribut", new { kAttribut });
            await conn.ExecuteAsync("DELETE FROM NOVVIA.LieferantAttribut WHERE kLieferantAttribut = @kAttribut", new { kAttribut });
        }

        // ===== KUNDE Eigene Felder (JTL SP) =====

        public async Task<IEnumerable<EigenesFeldDefinition>> GetKundeAttributeAsync()
        {
            var conn = await GetConnectionAsync();
            // nBezugstyp = 3 für Kunden-Attribute
            return await conn.QueryAsync<EigenesFeldDefinition>(@"
                SELECT a.kAttribut AS KAttribut, s.cName AS CName, a.cBeschreibung AS CBeschreibung,
                       CASE ft.nDatenTyp WHEN 0 THEN 1 WHEN 1 THEN 2 WHEN 2 THEN 3 WHEN 3 THEN 4 ELSE 3 END AS NFeldTyp,
                       a.nSortierung AS NSortierung, 1 AS NAktiv
                FROM dbo.tAttribut a
                LEFT JOIN dbo.tFeldTyp ft ON a.kFeldTyp = ft.kFeldTyp
                LEFT JOIN dbo.tAttributSprache s ON a.kAttribut = s.kAttribut AND s.kSprache IN (0, 1)
                WHERE a.nIstFreifeld = 1 AND a.nBezugstyp = 3
                ORDER BY a.nSortierung, s.cName");
        }

        public async Task<IEnumerable<EigenesFeldWert>> GetKundeEigenesFeldWerteAsync(int kKunde)
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<EigenesFeldWert>(@"
                SELECT ef.kKundeEigenesFeld AS KWert, ef.kKunde AS KEntity,
                       ef.kAttribut AS KAttribut, s.cName AS CAttributName,
                       CASE ft.nDatenTyp WHEN 0 THEN 1 WHEN 1 THEN 2 WHEN 2 THEN 3 WHEN 3 THEN 4 ELSE 3 END AS NFeldTyp,
                       ef.cWertVarchar AS CWertVarchar, ef.nWertInt AS NWertInt,
                       ef.fWertDecimal AS FWertDecimal, ef.dWertDateTime AS DWertDateTime
                FROM Kunde.tKundeEigenesFeld ef
                JOIN dbo.tAttribut a ON ef.kAttribut = a.kAttribut
                LEFT JOIN dbo.tFeldTyp ft ON a.kFeldTyp = ft.kFeldTyp
                LEFT JOIN dbo.tAttributSprache s ON a.kAttribut = s.kAttribut AND s.kSprache IN (0, 1)
                WHERE ef.kKunde = @kKunde
                ORDER BY a.nSortierung, s.cName", new { kKunde });
        }

        public async Task SaveKundeEigenesFeldAsync(int kKunde, string attributName, object? wert)
        {
            var conn = await GetConnectionAsync();
            // JTL-Wawi verwendet TYPE für Batch-Operationen, wir machen es einfacher mit direktem SQL
            var attr = await conn.QueryFirstOrDefaultAsync<dynamic>(@"
                SELECT a.kAttribut, a.kFeldTyp
                FROM dbo.tAttribut a
                JOIN dbo.tAttributSprache s ON a.kAttribut = s.kAttribut AND s.kSprache IN (0, 1)
                WHERE a.nIstFreifeld = 1 AND s.cName = @attributName",
                new { attributName });

            if (attr == null)
            {
                // Attribut neu anlegen
                var newAttrId = await conn.QueryFirstAsync<int>(@"
                    INSERT INTO dbo.tAttribut (nIstMehrsprachig, nIstFreifeld, nSortierung, nBezugstyp, nAusgabeweg, nIstStandard, kFeldTyp, cGruppeName, nReadOnly)
                    VALUES (0, 1, 1, 3, 2, 0, 1, 'Kundenattribute', 0);
                    SELECT SCOPE_IDENTITY();");

                await conn.ExecuteAsync(@"
                    INSERT INTO dbo.tAttributSprache (kAttribut, kSprache, cName) VALUES (@kAttribut, 0, @cName);
                    INSERT INTO dbo.tAttributSprache (kAttribut, kSprache, cName) VALUES (@kAttribut, 1, @cName);",
                    new { kAttribut = newAttrId, cName = attributName });

                attr = new { kAttribut = newAttrId, kFeldTyp = 1 };
            }

            // Vorhandenen Wert löschen
            await conn.ExecuteAsync("DELETE FROM Kunde.tKundeEigenesFeld WHERE kKunde = @kKunde AND kAttribut = @kAttribut",
                new { kKunde, kAttribut = (int)attr.kAttribut });

            // Neuen Wert einfügen
            await conn.ExecuteAsync(@"
                INSERT INTO Kunde.tKundeEigenesFeld (kKunde, kAttribut, cWertVarchar, nWertInt, fWertDecimal, dWertDateTime)
                VALUES (@kKunde, @kAttribut, @cWertVarchar, @nWertInt, @fWertDecimal, @dWertDateTime)",
                new
                {
                    kKunde,
                    kAttribut = (int)attr.kAttribut,
                    cWertVarchar = attr.kFeldTyp == 1 ? wert?.ToString() : null,
                    nWertInt = attr.kFeldTyp == 2 ? (int?)Convert.ToInt32(wert) : null,
                    fWertDecimal = attr.kFeldTyp == 3 ? (decimal?)Convert.ToDecimal(wert) : null,
                    dWertDateTime = attr.kFeldTyp == 4 ? (DateTime?)wert : null
                });
        }

        // ===== FIRMA Eigene Felder (JTL-Tabellen) =====

        public async Task<IEnumerable<EigenesFeldWert>> GetFirmaEigeneFelderAsync(int kFirma = 1)
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<EigenesFeldWert>(@"
                SELECT ef.kFirmaEigenesFeld AS KWert, ef.kFirma AS KEntity,
                       ef.kAttribut AS KAttribut, s.cName AS CAttributName,
                       CASE ft.nDatenTyp WHEN 0 THEN 1 WHEN 1 THEN 2 WHEN 2 THEN 3 WHEN 3 THEN 4 ELSE 3 END AS NFeldTyp,
                       ef.cWertVarchar AS CWertVarchar, ef.nWertInt AS NWertInt,
                       ef.fWertDecimal AS FWertDecimal, ef.dWertDateTime AS DWertDateTime
                FROM Firma.tFirmaEigenesFeld ef
                JOIN dbo.tAttribut a ON ef.kAttribut = a.kAttribut
                LEFT JOIN dbo.tFeldTyp ft ON a.kFeldTyp = ft.kFeldTyp
                LEFT JOIN dbo.tAttributSprache s ON a.kAttribut = s.kAttribut AND s.kSprache IN (0, 1)
                WHERE ef.kFirma = @kFirma
                ORDER BY a.nSortierung, s.cName", new { kFirma });
        }

        // ===== ARTIKEL Eigene Felder (JTL-Tabellen) =====

        public async Task<IEnumerable<EigenesFeldDefinition>> GetArtikelAttributeAsync()
        {
            var conn = await GetConnectionAsync();
            // nBezugstyp = 0 für Artikel-Attribute (Freifelder)
            return await conn.QueryAsync<EigenesFeldDefinition>(@"
                SELECT a.kAttribut AS KAttribut, s.cName AS CName, a.cBeschreibung AS CBeschreibung,
                       CASE ft.nDatenTyp WHEN 0 THEN 1 WHEN 1 THEN 2 WHEN 2 THEN 3 WHEN 3 THEN 4 ELSE 3 END AS NFeldTyp,
                       a.nSortierung AS NSortierung, 1 AS NAktiv
                FROM dbo.tAttribut a
                LEFT JOIN dbo.tFeldTyp ft ON a.kFeldTyp = ft.kFeldTyp
                LEFT JOIN dbo.tAttributSprache s ON a.kAttribut = s.kAttribut AND s.kSprache IN (0, 1)
                WHERE a.nIstFreifeld = 1 AND a.nBezugstyp = 0
                ORDER BY a.nSortierung, s.cName");
        }

        public async Task<IEnumerable<EigenesFeldWert>> GetArtikelEigenesFeldWerteAsync(int kArtikel)
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<EigenesFeldWert>(@"
                SELECT aas.kArtikelAttribut AS KWert, aa.kArtikel AS KEntity,
                       aa.kAttribut AS KAttribut, s.cName AS CAttributName,
                       CASE ft.nDatenTyp WHEN 0 THEN 1 WHEN 1 THEN 2 WHEN 2 THEN 3 WHEN 3 THEN 4 ELSE 3 END AS NFeldTyp,
                       aas.cWertVarchar AS CWertVarchar, aas.nWertInt AS NWertInt,
                       aas.fWertDecimal AS FWertDecimal, aas.dWertDateTime AS DWertDateTime
                FROM dbo.tArtikelAttribut aa
                JOIN dbo.tArtikelAttributSprache aas ON aa.kArtikelAttribut = aas.kArtikelAttribut AND aas.kSprache IN (0, 1)
                JOIN dbo.tAttribut a ON aa.kAttribut = a.kAttribut
                LEFT JOIN dbo.tFeldTyp ft ON a.kFeldTyp = ft.kFeldTyp
                LEFT JOIN dbo.tAttributSprache s ON a.kAttribut = s.kAttribut AND s.kSprache IN (0, 1)
                WHERE aa.kArtikel = @kArtikel AND a.nIstFreifeld = 1
                ORDER BY a.nSortierung, s.cName", new { kArtikel });
        }

        // ===== AUFTRAG Eigene Felder (JTL-Tabellen) =====

        public async Task<IEnumerable<EigenesFeldDefinition>> GetAuftragAttributeAsync()
        {
            var conn = await GetConnectionAsync();
            // nBezugstyp = 4 für Auftrags-Attribute
            return await conn.QueryAsync<EigenesFeldDefinition>(@"
                SELECT a.kAttribut AS KAttribut, s.cName AS CName, a.cBeschreibung AS CBeschreibung,
                       CASE ft.nDatenTyp WHEN 0 THEN 1 WHEN 1 THEN 2 WHEN 2 THEN 3 WHEN 3 THEN 4 ELSE 3 END AS NFeldTyp,
                       a.nSortierung AS NSortierung, 1 AS NAktiv
                FROM dbo.tAttribut a
                LEFT JOIN dbo.tFeldTyp ft ON a.kFeldTyp = ft.kFeldTyp
                LEFT JOIN dbo.tAttributSprache s ON a.kAttribut = s.kAttribut AND s.kSprache IN (0, 1)
                WHERE a.nIstFreifeld = 1 AND a.nBezugstyp = 4
                ORDER BY a.nSortierung, s.cName");
        }

        public async Task<IEnumerable<EigenesFeldWert>> GetAuftragEigenesFeldWerteAsync(int kAuftrag)
        {
            var conn = await GetConnectionAsync();
            return await conn.QueryAsync<EigenesFeldWert>(@"
                SELECT aa.kAuftragAttribut AS KWert, aa.kAuftrag AS KEntity,
                       aa.kAttribut AS KAttribut, s.cName AS CAttributName,
                       CASE ft.nDatenTyp WHEN 0 THEN 1 WHEN 1 THEN 2 WHEN 2 THEN 3 WHEN 3 THEN 4 ELSE 3 END AS NFeldTyp,
                       NULL AS CWertVarchar, NULL AS NWertInt, NULL AS FWertDecimal, NULL AS DWertDateTime
                FROM Verkauf.tAuftragAttribut aa
                JOIN dbo.tAttribut a ON aa.kAttribut = a.kAttribut
                LEFT JOIN dbo.tFeldTyp ft ON a.kFeldTyp = ft.kFeldTyp
                LEFT JOIN dbo.tAttributSprache s ON a.kAttribut = s.kAttribut AND s.kSprache IN (0, 1)
                WHERE aa.kAuftrag = @kAuftrag
                ORDER BY a.nSortierung, s.cName", new { kAuftrag });
        }

        // ===== FIRMA Eigene Felder (JTL-Tabellen) =====

        public async Task<IEnumerable<EigenesFeldDefinition>> GetFirmaAttributeAsync()
        {
            var conn = await GetConnectionAsync();
            // nBezugstyp = 2 für Firma-Attribute
            return await conn.QueryAsync<EigenesFeldDefinition>(@"
                SELECT a.kAttribut AS KAttribut, s.cName AS CName, a.cBeschreibung AS CBeschreibung,
                       CASE ft.nDatenTyp WHEN 0 THEN 1 WHEN 1 THEN 2 WHEN 2 THEN 3 WHEN 3 THEN 4 ELSE 3 END AS NFeldTyp,
                       a.nSortierung AS NSortierung, 1 AS NAktiv
                FROM dbo.tAttribut a
                LEFT JOIN dbo.tFeldTyp ft ON a.kFeldTyp = ft.kFeldTyp
                LEFT JOIN dbo.tAttributSprache s ON a.kAttribut = s.kAttribut AND s.kSprache IN (0, 1)
                WHERE a.nIstFreifeld = 1 AND a.nBezugstyp = 2
                ORDER BY a.nSortierung, s.cName");
        }

        // ===== Alle verfügbaren Attribute pro Entität =====

        public async Task<IEnumerable<EigenesFeldDefinition>> GetAlleAttributeAsync(string entityTyp)
        {
            return entityTyp.ToLower() switch
            {
                "lieferant" => await GetLieferantAttributeAsync(),
                "kunde" => await GetKundeAttributeAsync(),
                "artikel" => await GetArtikelAttributeAsync(),
                "auftrag" => await GetAuftragAttributeAsync(),
                "firma" => await GetFirmaAttributeAsync(),
                _ => Enumerable.Empty<EigenesFeldDefinition>()
            };
        }

        #endregion

        #region Formular-Auswahl (für Ausgabe-Dialog)

        /// <summary>
        /// Lädt verfügbare Formulare/Vorlagen für einen Dokumenttyp aus JTL tFormularVorlage
        /// </summary>
        public async Task<IEnumerable<FormularVorlageItem>> GetFormulareAsync(DokumentTyp dokumentTyp)
        {
            var conn = await GetConnectionAsync();

            // nTyp in tFormular entspricht DokumentTyp
            var formularTyp = dokumentTyp switch
            {
                DokumentTyp.Angebot => 0,
                DokumentTyp.Bestellung => 1,  // Auftrag
                DokumentTyp.Auftragsbestaetigung => 1,
                DokumentTyp.Rechnung => 2,
                DokumentTyp.Lieferschein => 3,
                DokumentTyp.Gutschrift => 4,  // Rechnungskorrektur
                DokumentTyp.Mahnung => 31,
                DokumentTyp.Versandetikett => 13,
                DokumentTyp.Packliste => 26,
                DokumentTyp.Retourenschein => 32,
                _ => 2  // Default: Rechnung
            };

            var sql = @"
                SELECT fv.kFormularVorlage AS KFormularVorlage,
                       fv.kFormular AS KFormular,
                       ISNULL(fv.cName, f.cName) AS Name,
                       CASE WHEN fv.nTyp = 2 THEN 1 ELSE 0 END AS IsStandard,
                       fv.kFirma AS KFirma,
                       fv.kSprache AS KSprache
                FROM dbo.tFormularVorlage fv
                INNER JOIN dbo.tFormular f ON fv.kFormular = f.kFormular
                WHERE f.nTyp = @formularTyp
                ORDER BY CASE WHEN fv.nTyp = 2 THEN 0 ELSE 1 END, fv.cName";

            var result = await conn.QueryAsync<FormularVorlageItem>(sql, new { formularTyp });

            // Wenn keine Vorlagen gefunden, generische erstellen
            if (!result.Any())
            {
                return new List<FormularVorlageItem>
                {
                    new() { KFormularVorlage = 0, Name = "Standard (QuestPDF)", IsStandard = true }
                };
            }

            return result;
        }

        /// <summary>
        /// Lädt E-Mail-Adresse für ein Dokument
        /// </summary>
        public async Task<string?> GetDokumentEmailAsync(DokumentTyp dokumentTyp, int dokumentId)
        {
            var conn = await GetConnectionAsync();

            return dokumentTyp switch
            {
                DokumentTyp.Rechnung => await conn.QuerySingleOrDefaultAsync<string>(
                    @"SELECT COALESCE(b.cEmail, k.cMail)
                      FROM dbo.tRechnung r
                      LEFT JOIN dbo.tBestellung b ON r.kBestellung = b.kBestellung
                      LEFT JOIN dbo.tKunde k ON b.kKunde = k.kKunde
                      WHERE r.kRechnung = @id", new { id = dokumentId }),

                DokumentTyp.Lieferschein => await conn.QuerySingleOrDefaultAsync<string>(
                    @"SELECT COALESCE(b.cEmail, k.cMail)
                      FROM dbo.tLieferschein l
                      LEFT JOIN dbo.tBestellung b ON l.kBestellung = b.kBestellung
                      LEFT JOIN dbo.tKunde k ON b.kKunde = k.kKunde
                      WHERE l.kLieferschein = @id", new { id = dokumentId }),

                DokumentTyp.Bestellung or DokumentTyp.Auftragsbestaetigung => await conn.QuerySingleOrDefaultAsync<string>(
                    @"SELECT COALESCE(b.cEmail, k.cMail)
                      FROM dbo.tBestellung b
                      LEFT JOIN dbo.tKunde k ON b.kKunde = k.kKunde
                      WHERE b.kBestellung = @id", new { id = dokumentId }),

                DokumentTyp.Angebot => await conn.QuerySingleOrDefaultAsync<string>(
                    @"SELECT COALESCE(a.cMail, k.cMail)
                      FROM dbo.tAngebot a
                      LEFT JOIN dbo.tKunde k ON a.kKunde = k.kKunde
                      WHERE a.kAngebot = @id", new { id = dokumentId }),

                DokumentTyp.Mahnung => await conn.QuerySingleOrDefaultAsync<string>(
                    @"SELECT k.cMail
                      FROM dbo.tMahnung m
                      LEFT JOIN dbo.tKunde k ON m.kKunde = k.kKunde
                      WHERE m.kMahnung = @id", new { id = dokumentId }),

                _ => null
            };
        }

        #endregion

        #region FormularVorlageItem DTO
        /// <summary>
        /// Formular-Vorlage DTO für Ausgabe-Dialog
        /// </summary>
        public class FormularVorlageItem
        {
            public int KFormularVorlage { get; set; }
            public int KFormular { get; set; }
            public string Name { get; set; } = "";
            public bool IsStandard { get; set; }
            public int? KFirma { get; set; }
            public int? KSprache { get; set; }
        }
        #endregion

        #region Artikel-Verwaltung (CRUD)

        /// <summary>
        /// Artikel mit Mapping-Konfiguration aktualisieren
        /// </summary>
        public async Task UpdateArtikelMitMappingAsync(int artikelId, ArtikelMappingConfig mapping)
        {
            var conn = await GetConnectionAsync();

            // Standard-Werte aus Mapping anwenden
            await conn.ExecuteAsync(@"
                UPDATE Artikel.tArtikel SET
                    kWarengruppe = COALESCE(kWarengruppe, @WarengruppeId),
                    kHersteller = COALESCE(kHersteller, @HerstellerId),
                    kLieferant = COALESCE(kLieferant, @LieferantId),
                    kSteuerklasse = COALESCE(kSteuerklasse, @MwStKlasse),
                    fMindestbestellmenge = COALESCE(NULLIF(fMindestbestellmenge, 0), @Mindestbestand),
                    dAendern = GETDATE()
                WHERE kArtikel = @ArtikelId",
                new
                {
                    ArtikelId = artikelId,
                    WarengruppeId = mapping.StandardWarengruppeId > 0 ? mapping.StandardWarengruppeId : (int?)null,
                    HerstellerId = mapping.StandardHerstellerId > 0 ? mapping.StandardHerstellerId : (int?)null,
                    LieferantId = mapping.StandardLieferantId > 0 ? mapping.StandardLieferantId : (int?)null,
                    MwStKlasse = mapping.StandardMwStKlasse,
                    Mindestbestand = mapping.MindestbestandStandard
                });

            _log.Information("Artikel {ArtikelId} mit Mapping aktualisiert", artikelId);
        }

        /// <summary>
        /// Artikel aktiv/inaktiv setzen
        /// </summary>
        public async Task SetArtikelAktivAsync(int artikelId, bool aktiv)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync(@"
                UPDATE Artikel.tArtikel SET
                    nAktiv = @Aktiv,
                    dAendern = GETDATE()
                WHERE kArtikel = @ArtikelId",
                new { ArtikelId = artikelId, Aktiv = aktiv ? 1 : 0 });

            _log.Information("Artikel {ArtikelId} auf aktiv={Aktiv} gesetzt", artikelId, aktiv);
        }

        /// <summary>
        /// Artikel löschen (setzt Löschkennzeichen)
        /// </summary>
        public async Task DeleteArtikelAsync(int artikelId)
        {
            var conn = await GetConnectionAsync();

            // JTL markiert Artikel als gelöscht statt sie wirklich zu löschen
            await conn.ExecuteAsync(@"
                UPDATE Artikel.tArtikel SET
                    nAktiv = 0,
                    cArtNr = 'DEL_' + cArtNr + '_' + CONVERT(VARCHAR(10), GETDATE(), 112),
                    dAendern = GETDATE()
                WHERE kArtikel = @ArtikelId",
                new { ArtikelId = artikelId });

            _log.Warning("Artikel {ArtikelId} gelöscht (deaktiviert)", artikelId);
        }

        /// <summary>
        /// Neuen Artikel erstellen mit Mapping-Defaults
        /// </summary>
        public async Task<int> CreateArtikelAsync(ArtikelNeuInput input, ArtikelMappingConfig? mapping = null)
        {
            var conn = await GetConnectionAsync();
            var m = mapping ?? new ArtikelMappingConfig();

            var artikelId = await conn.QuerySingleAsync<int>(@"
                INSERT INTO Artikel.tArtikel (
                    cArtNr, cName, cBarcode, kWarengruppe, kHersteller, kLieferant,
                    kSteuerklasse, kMassEinheit, fVKNetto, fEKNetto, fGewicht,
                    nAktiv, dErstellt, dAendern
                )
                OUTPUT INSERTED.kArtikel
                VALUES (
                    @ArtNr, @Name, @Barcode, @WarengruppeId, @HerstellerId, @LieferantId,
                    @MwStKlasse, @EinheitId, @VKNetto, @EKNetto, @Gewicht,
                    1, GETDATE(), GETDATE()
                )",
                new
                {
                    input.ArtNr,
                    input.Name,
                    input.Barcode,
                    WarengruppeId = input.WarengruppeId ?? m.StandardWarengruppeId,
                    HerstellerId = input.HerstellerId ?? m.StandardHerstellerId,
                    LieferantId = input.LieferantId ?? m.StandardLieferantId,
                    MwStKlasse = m.StandardMwStKlasse,
                    EinheitId = m.StandardEinheitId,
                    input.VKNetto,
                    input.EKNetto,
                    input.Gewicht
                });

            _log.Information("Artikel erstellt: {ArtikelId} - {ArtNr}", artikelId, input.ArtNr);
            return artikelId;
        }

        public class ArtikelNeuInput
        {
            public string ArtNr { get; set; } = "";
            public string Name { get; set; } = "";
            public string? Barcode { get; set; }
            public int? WarengruppeId { get; set; }
            public int? HerstellerId { get; set; }
            public int? LieferantId { get; set; }
            public decimal VKNetto { get; set; }
            public decimal EKNetto { get; set; }
            public decimal Gewicht { get; set; }
        }

        #endregion

        #region Rechnungen Übersicht

        /// <summary>
        /// Alle Rechnungen laden mit optionalen Filtern
        /// </summary>
        public async Task<IEnumerable<RechnungUebersicht>> GetAllRechnungenAsync(
            string? suche = null,
            int? status = null,
            DateTime? vonDatum = null,
            DateTime? bisDatum = null,
            int limit = 500)
        {
            var conn = await GetConnectionAsync();
            var sql = @"
                SELECT TOP (@Limit)
                    r.kRechnung AS KRechnung,
                    r.cRechnungsnr AS CRechnungsNr,
                    r.dErstellt AS DErstellt,
                    DATEADD(DAY, r.nZahlungszielTage, r.dErstellt) AS DFaellig,
                    CASE WHEN ISNULL(zahlung.Summe, 0) >= ISNULL(pos.Brutto, 0) THEN r.dErstellt ELSE NULL END AS DBezahlt,
                    ISNULL(pos.Netto, 0) AS FNetto,
                    ISNULL(pos.Brutto, 0) AS FBrutto,
                    r.nStorno AS NStorno,
                    0 AS NTyp,
                    ISNULL(ra.cVorname + ' ', '') + ISNULL(ra.cName, '') AS KundeName,
                    ISNULL(ra.cFirma, '') AS KundeFirma,
                    r.kKunde AS KKunde,
                    k.cKundenNr AS CKundenNr,
                    a.cAuftragsNr AS CAuftragNr,
                    ar.kAuftrag AS KAuftrag,
                    ISNULL(zahlung.Summe, 0) AS FBezahlt,
                    0 AS NMahnstufe,
                    rt.cAnmerkung AS CAnmerkung,
                    rt.cRechnungstext AS CRechnungstext,
                    -- Rechnungsadresse
                    ra.cFirma AS ReFirma, ra.cName AS ReName, ra.cVorname AS ReVorname,
                    ra.cStrasse AS ReStrasse, ra.cPLZ AS RePlz, ra.cOrt AS ReOrt, ra.cLand AS ReLand,
                    ra.cMail AS ReMail, ra.cTel AS ReTel,
                    -- Lieferadresse
                    la.cFirma AS LiFirma, la.cName AS LiName, la.cVorname AS LiVorname,
                    la.cStrasse AS LiStrasse, la.cPLZ AS LiPlz, la.cOrt AS LiOrt, la.cLand AS LiLand
                FROM Rechnung.tRechnung r
                LEFT JOIN dbo.tKunde k ON r.kKunde = k.kKunde
                LEFT JOIN Rechnung.tRechnungText rt ON r.kRechnung = rt.kRechnung
                OUTER APPLY (SELECT TOP 1 * FROM Rechnung.tRechnungAdresse WHERE kRechnung = r.kRechnung AND nTyp = 0) ra
                OUTER APPLY (SELECT TOP 1 * FROM Rechnung.tRechnungAdresse WHERE kRechnung = r.kRechnung AND nTyp = 1) la
                OUTER APPLY (SELECT TOP 1 kAuftrag FROM Verkauf.tAuftragRechnung WHERE kRechnung = r.kRechnung) ar
                LEFT JOIN Verkauf.tAuftrag a ON ar.kAuftrag = a.kAuftrag
                LEFT JOIN (
                    SELECT kRechnung,
                           SUM(fAnzahl * fVkNetto * (1 - fRabatt/100)) AS Netto,
                           SUM(fAnzahl * fVkNetto * (1 - fRabatt/100) * (1 + fMwSt/100)) AS Brutto
                    FROM Rechnung.tRechnungPosition
                    GROUP BY kRechnung
                ) pos ON r.kRechnung = pos.kRechnung
                LEFT JOIN (
                    SELECT kRechnung, SUM(fBetrag) AS Summe
                    FROM dbo.tZahlung
                    WHERE kRechnung IS NOT NULL
                    GROUP BY kRechnung
                ) zahlung ON r.kRechnung = zahlung.kRechnung
                WHERE r.nIstProforma = 0";

            if (!string.IsNullOrEmpty(suche))
                sql += @" AND (r.cRechnungsnr LIKE @Suche
                          OR ra.cFirma LIKE @Suche
                          OR ra.cName LIKE @Suche
                          OR a.cAuftragsNr LIKE @Suche)";

            if (status.HasValue)
            {
                if (status == 0) // Offen
                    sql += " AND ISNULL(zahlung.Summe, 0) < ISNULL(pos.Brutto, 0) AND r.nStorno = 0";
                else if (status == 1) // Bezahlt
                    sql += " AND ISNULL(zahlung.Summe, 0) >= ISNULL(pos.Brutto, 0)";
                else if (status == 2) // Überfällig
                    sql += " AND ISNULL(zahlung.Summe, 0) < ISNULL(pos.Brutto, 0) AND r.nStorno = 0 AND DATEADD(DAY, r.nZahlungszielTage, r.dErstellt) < GETDATE()";
                else if (status == 3) // Storniert
                    sql += " AND r.nStorno = 1";
            }

            if (vonDatum.HasValue)
                sql += " AND r.dErstellt >= @VonDatum";
            if (bisDatum.HasValue)
                sql += " AND r.dErstellt <= @BisDatum";

            sql += " ORDER BY r.dErstellt DESC";

            return await conn.QueryAsync<RechnungUebersicht>(sql, new
            {
                Limit = limit,
                Suche = $"%{suche}%",
                VonDatum = vonDatum,
                BisDatum = bisDatum
            });
        }

        public class RechnungUebersicht
        {
            public int KRechnung { get; set; }
            public string CRechnungsNr { get; set; } = "";
            public DateTime DErstellt { get; set; }
            public DateTime? DFaellig { get; set; }
            public DateTime? DBezahlt { get; set; }
            public decimal FNetto { get; set; }
            public decimal FBrutto { get; set; }
            public bool NStorno { get; set; }
            public int NTyp { get; set; }
            public string KundeName { get; set; } = "";
            public string KundeFirma { get; set; } = "";
            public int KKunde { get; set; }
            public string? CKundenNr { get; set; }
            public string? CAuftragNr { get; set; }
            public int? KAuftrag { get; set; }
            public decimal FBezahlt { get; set; }
            public int NMahnstufe { get; set; }
            public string? CAnmerkung { get; set; }
            public string? CRechnungstext { get; set; }

            // Rechnungsadresse
            public string? ReFirma { get; set; }
            public string? ReName { get; set; }
            public string? ReVorname { get; set; }
            public string? ReStrasse { get; set; }
            public string? RePlz { get; set; }
            public string? ReOrt { get; set; }
            public string? ReLand { get; set; }
            public string? ReMail { get; set; }
            public string? ReTel { get; set; }

            // Lieferadresse
            public string? LiFirma { get; set; }
            public string? LiName { get; set; }
            public string? LiVorname { get; set; }
            public string? LiStrasse { get; set; }
            public string? LiPlz { get; set; }
            public string? LiOrt { get; set; }
            public string? LiLand { get; set; }

            public decimal Offen => FBrutto - FBezahlt;
            public string Status => NStorno ? "Storniert" : (DBezahlt.HasValue ? "Bezahlt" : (DFaellig < DateTime.Today ? "Überfällig" : "Offen"));
            public string StatusFarbe => NStorno ? "#dc3545" : (DBezahlt.HasValue ? "#28a745" : (DFaellig < DateTime.Today ? "#dc3545" : "#ffc107"));
            public string TypName => NTyp switch { 0 => "Rechnung", 1 => "Rechnung", 2 => "Gutschrift", _ => "Rechnung" };
        }

        #endregion

        #region Lager Übersicht

        /// <summary>
        /// Lagerbestände laden mit optionalen Filtern
        /// </summary>
        public async Task<IEnumerable<LagerbestandUebersicht>> GetLagerbestaendeAsync(
            int? kWarenlager = null,
            string? suche = null,
            bool nurMitBestand = false,
            int limit = 1000)
        {
            var conn = await GetConnectionAsync();

            // Quarantaene-Lager ausschliessen (ausser explizit angefordert)
            var quarantaeneLager = await GetQuarantaeneLagerIdAsync();

            var sql = @"
                SELECT TOP (@Limit)
                    a.kArtikel AS KArtikel,
                    a.cArtNr AS CArtNr,
                    ISNULL(ab.cName, '') AS CName,
                    ISNULL(a.cBarcode, '') AS CBarcode,
                    wl.kWarenLager AS KWarenLager,
                    wl.cName AS CLagerName,
                    ISNULL(lbp.fBestand, 0) AS FVerfuegbar,
                    ISNULL(lb.fInAuftraegen, 0) AS FReserviert,
                    ISNULL(lbp.fBestand, 0) + ISNULL(lb.fInAuftraegen, 0) AS FGesamt,
                    ISNULL(a.nMidestbestand, 0) AS FMindestbestand,
                    h.cName AS CHersteller
                FROM dbo.tArtikel a
                LEFT JOIN dbo.tArtikelBeschreibung ab ON a.kArtikel = ab.kArtikel AND ab.kSprache = 1 AND ab.kPlattform = 1
                LEFT JOIN dbo.tHersteller h ON a.kHersteller = h.kHersteller
                LEFT JOIN dbo.tlagerbestand lb ON a.kArtikel = lb.kArtikel
                CROSS JOIN dbo.tWarenLager wl
                LEFT JOIN dbo.vLagerbestandProLager lbp ON a.kArtikel = lbp.kArtikel AND wl.kWarenLager = lbp.kWarenlager
                WHERE a.cAktiv = 'Y' AND wl.nAktiv = 1";

            // Quarantaene-Lager ausschliessen, ausser es wird explizit angefordert
            if (kWarenlager.HasValue)
                sql += " AND wl.kWarenLager = @KWarenlager";
            else
                sql += " AND wl.kWarenLager <> @QuarantaeneLager";

            if (!string.IsNullOrEmpty(suche))
                sql += @" AND (a.cArtNr LIKE @Suche
                          OR ab.cName LIKE @Suche
                          OR a.cBarcode LIKE @Suche)";

            if (nurMitBestand)
                sql += " AND ISNULL(lbp.fBestand, 0) > 0";

            sql += " ORDER BY a.cArtNr";

            return await conn.QueryAsync<LagerbestandUebersicht>(sql, new
            {
                Limit = limit,
                KWarenlager = kWarenlager,
                QuarantaeneLager = quarantaeneLager,
                Suche = $"%{suche}%"
            });
        }

        /// <summary>
        /// Lagerbewegungen laden (Eingänge und Ausgänge)
        /// </summary>
        public async Task<IEnumerable<LagerbewegungUebersicht>> GetLagerbewegungenAsync(
            int? kArtikel = null,
            int? kWarenlager = null,
            DateTime? vonDatum = null,
            int limit = 200)
        {
            var conn = await GetConnectionAsync();
            // Kombiniere Wareneingänge und Warenausgänge
            var sql = @"
                SELECT TOP (@Limit) * FROM (
                    -- Wareneingänge (positiv)
                    SELECT
                        we.kWarenLagerEingang AS KLog,
                        we.kArtikel AS KArtikel,
                        a.cArtNr AS CArtNr,
                        ISNULL(ab.cName, '') AS CArtikelName,
                        wl.kWarenLager AS KWarenLager,
                        wl.cName AS CLagerName,
                        we.fAnzahl AS FMenge,
                        'Wareneingang' AS CGrund,
                        'Eingang' AS CTyp,
                        we.dErstellt AS DErstellt,
                        ISNULL(we.cKommentar, we.cLieferscheinNr) AS CHinweis
                    FROM dbo.tWarenLagerEingang we
                    JOIN dbo.tArtikel a ON we.kArtikel = a.kArtikel
                    LEFT JOIN dbo.tArtikelBeschreibung ab ON a.kArtikel = ab.kArtikel AND ab.kSprache = 1 AND ab.kPlattform = 1
                    LEFT JOIN dbo.tWarenLagerPlatz wlp ON we.kWarenLagerPlatz = wlp.kWarenLagerPlatz
                    LEFT JOIN dbo.tWarenLager wl ON wlp.kWarenLager = wl.kWarenLager
                    WHERE we.fAnzahl <> 0

                    UNION ALL

                    -- Warenausgänge (negativ)
                    SELECT
                        wa.kWarenLagerAusgang AS KLog,
                        wa.kArtikel AS KArtikel,
                        a.cArtNr AS CArtNr,
                        ISNULL(ab.cName, '') AS CArtikelName,
                        wl.kWarenLager AS KWarenLager,
                        wl.cName AS CLagerName,
                        -wa.fAnzahl AS FMenge,
                        'Warenausgang' AS CGrund,
                        'Ausgang' AS CTyp,
                        wa.dErstellt AS DErstellt,
                        wa.cKommentar AS CHinweis
                    FROM dbo.tWarenLagerAusgang wa
                    JOIN dbo.tArtikel a ON wa.kArtikel = a.kArtikel
                    LEFT JOIN dbo.tArtikelBeschreibung ab ON a.kArtikel = ab.kArtikel AND ab.kSprache = 1 AND ab.kPlattform = 1
                    LEFT JOIN dbo.tWarenLagerPlatz wlp ON wa.kWarenLagerPlatz = wlp.kWarenLagerPlatz
                    LEFT JOIN dbo.tWarenLager wl ON wlp.kWarenLager = wl.kWarenLager
                    WHERE wa.fAnzahl <> 0
                ) bewegungen
                WHERE 1=1";

            if (kArtikel.HasValue)
                sql += " AND kArtikel = @KArtikel";
            if (kWarenlager.HasValue)
                sql += " AND kWarenLager = @KWarenlager";
            if (vonDatum.HasValue)
                sql += " AND dErstellt >= @VonDatum";

            sql += " ORDER BY dErstellt DESC";

            return await conn.QueryAsync<LagerbewegungUebersicht>(sql, new
            {
                Limit = limit,
                KArtikel = kArtikel,
                KWarenlager = kWarenlager,
                VonDatum = vonDatum
            });
        }

        public class LagerbestandUebersicht
        {
            public int KArtikel { get; set; }
            public string CArtNr { get; set; } = "";
            public string CName { get; set; } = "";
            public string CBarcode { get; set; } = "";
            public int KWarenLager { get; set; }
            public string CLagerName { get; set; } = "";
            public decimal FVerfuegbar { get; set; }
            public decimal FReserviert { get; set; }
            public decimal FGesamt { get; set; }
            public decimal FMindestbestand { get; set; }
            public string? CHersteller { get; set; }

            public bool IstUnterMindestbestand => FMindestbestand > 0 && FVerfuegbar < FMindestbestand;
        }

        public class LagerbewegungUebersicht
        {
            public int KLog { get; set; }
            public int KArtikel { get; set; }
            public string CArtNr { get; set; } = "";
            public string CArtikelName { get; set; } = "";
            public int KWarenLager { get; set; }
            public string CLagerName { get; set; } = "";
            public decimal FMenge { get; set; }
            public string? CGrund { get; set; }
            public string? CTyp { get; set; }
            public DateTime DErstellt { get; set; }
            public string? CHinweis { get; set; }

            public string MengeText => FMenge >= 0 ? $"+{FMenge:N0}" : FMenge.ToString("N0");
            public string MengeFarbe => FMenge >= 0 ? "#28a745" : "#dc3545";
        }

        #endregion

        #region Chargen-Management

        /// <summary>
        /// Chargenbestände laden mit optionalen Filtern
        /// </summary>
        public async Task<IEnumerable<ChargenBestand>> GetChargenBestaendeAsync(
            int? kArtikel = null,
            int? kWarenlager = null,
            string? suche = null,
            bool nurGesperrt = false,
            bool nurQuarantaene = false,
            bool nurAbgelaufen = false,
            bool nurMitBestand = true,
            int limit = 500)
        {
            var conn = await GetConnectionAsync();
            // Quarantaene-Lager aus Einstellungen lesen (Standard: kWarenLager=3)
            var quarantaeneLager = await GetQuarantaeneLagerIdAsync();
            var sql = @"
                SELECT TOP (@Limit)
                    we.kWarenLagerEingang AS KWarenLagerEingang,
                    we.kArtikel AS KArtikel,
                    a.cArtNr AS CArtNr,
                    ISNULL(ab.cName, '') AS CArtikelName,
                    we.cChargenNr AS CChargenNr,
                    CASE WHEN we.dMHD > '1900-01-02' THEN we.dMHD ELSE NULL END AS DMHD,
                    we.fAnzahlAktuell AS FBestand,
                    we.fAnzahl AS FEingang,
                    wlp.kWarenLager AS KWarenLager,
                    ISNULL(wl.cName, 'Unbekannt') AS CLagerName,
                    we.dErstellt AS DEingang,
                    we.cLieferscheinNr AS CLieferscheinNr,
                    -- JTL-konform: Quarantaene = konfiguriertes Quarantaene-Lager
                    CASE WHEN wlp.kWarenLager = @QuarantaeneLager THEN 1 ELSE 0 END AS NGesperrt,
                    CASE WHEN wlp.kWarenLager = @QuarantaeneLager THEN 1 ELSE 0 END AS NQuarantaene,
                    -- Sperrgrund aus Kommentar auslesen (Format: QUARANTAENE|OriginalPlatz|Grund)
                    CASE WHEN we.cKommentar LIKE 'QUARANTAENE|%' THEN
                        SUBSTRING(we.cKommentar, CHARINDEX('|', we.cKommentar, CHARINDEX('|', we.cKommentar) + 1) + 1, LEN(we.cKommentar))
                    ELSE NULL END AS CSperrgrund,
                    NULL AS DGesperrtAm,
                    CASE
                        WHEN we.dMHD IS NULL THEN 'Kein MHD'
                        WHEN we.dMHD < GETDATE() THEN 'Abgelaufen'
                        WHEN we.dMHD < DATEADD(DAY, 30, GETDATE()) THEN 'Bald ablaufend'
                        ELSE 'OK'
                    END AS CMHDStatus,
                    DATEDIFF(DAY, GETDATE(), we.dMHD) AS NTageRestMHD
                FROM dbo.tWarenLagerEingang we
                JOIN dbo.tArtikel a ON we.kArtikel = a.kArtikel
                LEFT JOIN dbo.tArtikelBeschreibung ab ON a.kArtikel = ab.kArtikel AND ab.kSprache = 1 AND ab.kPlattform = 1
                LEFT JOIN dbo.tWarenLagerPlatz wlp ON we.kWarenLagerPlatz = wlp.kWarenLagerPlatz
                LEFT JOIN dbo.tWarenLager wl ON wlp.kWarenLager = wl.kWarenLager
                WHERE we.cChargenNr IS NOT NULL
                  AND we.cChargenNr <> ''";

            if (nurMitBestand)
                sql += " AND we.fAnzahlAktuell > 0";

            if (kArtikel.HasValue)
                sql += " AND we.kArtikel = @KArtikel";
            if (kWarenlager.HasValue)
                sql += " AND wlp.kWarenLager = @KWarenlager";
            if (!string.IsNullOrEmpty(suche))
                sql += @" AND (we.cChargenNr LIKE @Suche OR a.cArtNr LIKE @Suche OR ab.cName LIKE @Suche)";
            if (nurGesperrt)
                sql += " AND wlp.kWarenLager = @QuarantaeneLager";
            if (nurQuarantaene)
                sql += " AND wlp.kWarenLager = @QuarantaeneLager";
            if (nurAbgelaufen)
                sql += " AND we.dMHD < GETDATE()";

            sql += " ORDER BY we.dMHD, we.cChargenNr";

            return await conn.QueryAsync<ChargenBestand>(sql, new
            {
                Limit = limit,
                KArtikel = kArtikel,
                KWarenlager = kWarenlager,
                Suche = $"%{suche}%",
                QuarantaeneLager = quarantaeneLager
            });
        }

        /// <summary>
        /// Chargenverfolgung: Alle Bewegungen einer Charge inkl. Wareneingang
        /// </summary>
        public async Task<IEnumerable<ChargenBewegung>> GetChargenVerfolgungAsync(string chargenNr, int? kArtikel = null)
        {
            var conn = await GetConnectionAsync();
            var sql = @"
                -- Wareneingang als erste Bewegung
                SELECT
                    0 AS KChargenBewegung,
                    we.kWarenLagerEingang AS KWarenLagerEingang,
                    we.kArtikel AS KArtikel,
                    a.cArtNr AS CArtNr,
                    we.cChargenNr AS CChargenNr,
                    'EINGANG' AS CAktion,
                    'Wareneingang' AS CGrund,
                    'Lieferschein: ' + ISNULL(we.cLieferscheinNr, '-') AS CHinweis,
                    NULL AS KVonWarenLager,
                    NULL AS CVonLagerName,
                    wlp.kWarenLager AS KNachWarenLager,
                    wl.cName AS CNachLagerName,
                    we.fAnzahl AS FMenge,
                    we.kBenutzer AS KBenutzer,
                    we.dErstellt AS DErstellt
                FROM dbo.tWarenLagerEingang we
                JOIN dbo.tArtikel a ON we.kArtikel = a.kArtikel
                LEFT JOIN dbo.tWarenLagerPlatz wlp ON we.kWarenLagerPlatz = wlp.kWarenLagerPlatz
                LEFT JOIN dbo.tWarenLager wl ON wlp.kWarenLager = wl.kWarenLager
                WHERE we.cChargenNr = @ChargenNr

                UNION ALL

                -- Folgebewegungen (Sperren, Freigeben, Quarantaene)
                SELECT
                    cb.kChargenBewegung AS KChargenBewegung,
                    cb.kWarenLagerEingang AS KWarenLagerEingang,
                    cb.kArtikel AS KArtikel,
                    a.cArtNr AS CArtNr,
                    cb.cChargenNr AS CChargenNr,
                    cb.cAktion AS CAktion,
                    cb.cGrund AS CGrund,
                    cb.cHinweis AS CHinweis,
                    cb.kVonWarenLager AS KVonWarenLager,
                    wlVon.cName AS CVonLagerName,
                    cb.kNachWarenLager AS KNachWarenLager,
                    wlNach.cName AS CNachLagerName,
                    cb.fMenge AS FMenge,
                    cb.kBenutzer AS KBenutzer,
                    cb.dErstellt AS DErstellt
                FROM NOVVIA.ChargenBewegung cb
                JOIN dbo.tArtikel a ON cb.kArtikel = a.kArtikel
                LEFT JOIN dbo.tWarenLager wlVon ON cb.kVonWarenLager = wlVon.kWarenLager
                LEFT JOIN dbo.tWarenLager wlNach ON cb.kNachWarenLager = wlNach.kWarenLager
                WHERE cb.cChargenNr = @ChargenNr";

            if (kArtikel.HasValue)
                sql = sql.Replace("WHERE we.cChargenNr = @ChargenNr", "WHERE we.cChargenNr = @ChargenNr AND we.kArtikel = @KArtikel")
                         .Replace("WHERE cb.cChargenNr = @ChargenNr", "WHERE cb.cChargenNr = @ChargenNr AND cb.kArtikel = @KArtikel");

            sql += " ORDER BY DErstellt DESC";

            return await conn.QueryAsync<ChargenBewegung>(sql, new { ChargenNr = chargenNr, KArtikel = kArtikel });
        }

        #region Pharma Einstellungen - Quarantaene-Lager

        public const string SETTING_QUARANTAENE_LAGER = "Pharma.QuarantaeneLager";
        public const int DEFAULT_QUARANTAENE_LAGER = 3;

        /// <summary>Liest das konfigurierte Quarantaene-Lager (kWarenLager)</summary>
        public async Task<int> GetQuarantaeneLagerIdAsync()
        {
            var conn = await GetConnectionAsync();
            var wert = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT cWert FROM NOVVIA.FirmaEinstellung WHERE cSchluessel = @Schluessel",
                new { Schluessel = SETTING_QUARANTAENE_LAGER });
            return int.TryParse(wert, out var result) ? result : DEFAULT_QUARANTAENE_LAGER;
        }

        /// <summary>Setzt das Quarantaene-Lager</summary>
        public async Task SetQuarantaeneLagerIdAsync(int kWarenLager)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync(@"
                MERGE INTO NOVVIA.FirmaEinstellung AS target
                USING (SELECT @Schluessel AS cSchluessel) AS source
                ON target.cSchluessel = source.cSchluessel
                WHEN MATCHED THEN UPDATE SET cWert = @Wert, dGeaendert = GETDATE()
                WHEN NOT MATCHED THEN INSERT (cSchluessel, cWert, cBeschreibung, dErstellt)
                    VALUES (@Schluessel, @Wert, 'Quarantaene-Lager (kWarenLager)', GETDATE());",
                new { Schluessel = SETTING_QUARANTAENE_LAGER, Wert = kWarenLager.ToString() });
        }

        /// <summary>Alle NOVVIA Einstellungen laden</summary>
        public async Task<NovviaEinstellungen> GetNovviaEinstellungenAsync()
        {
            var conn = await GetConnectionAsync();
            var settings = await conn.QueryAsync<(string Schluessel, string Wert)>(
                "SELECT cSchluessel, cWert FROM NOVVIA.FirmaEinstellung");

            var dict = settings.ToDictionary(s => s.Schluessel, s => s.Wert);

            return new NovviaEinstellungen
            {
                PharmaModus = dict.TryGetValue("PHARMA", out var p) && p == "1",
                QuarantaeneLagerId = dict.TryGetValue(SETTING_QUARANTAENE_LAGER, out var q) && int.TryParse(q, out var qId) ? qId : DEFAULT_QUARANTAENE_LAGER,
                AutoMatchSchwelle = dict.TryGetValue("Zahlungsabgleich.AutoMatchSchwelle", out var a) && int.TryParse(a, out var aVal) ? aVal : 90,
                Firmenname = dict.TryGetValue("FIRMENNAME", out var f) ? f : "",
                LogoPfad = dict.TryGetValue("LOGO_PFAD", out var l) ? l : null
            };
        }

        /// <summary>Alle NOVVIA Einstellungen speichern</summary>
        public async Task SaveNovviaEinstellungenAsync(NovviaEinstellungen e)
        {
            var conn = await GetConnectionAsync();

            var settings = new[]
            {
                ("PHARMA", e.PharmaModus ? "1" : "0", "Pharma-Modus: 1 = Aktiv"),
                (SETTING_QUARANTAENE_LAGER, e.QuarantaeneLagerId.ToString(), "Quarantaene-Lager (kWarenLager)"),
                ("Zahlungsabgleich.AutoMatchSchwelle", e.AutoMatchSchwelle.ToString(), "Auto-Matching Schwelle (0-100%)"),
                ("FIRMENNAME", e.Firmenname ?? "", "Firmenname fuer Berichte"),
                ("LOGO_PFAD", e.LogoPfad ?? "", "Pfad zum Firmenlogo (PNG/JPG)")
            };

            foreach (var (schluessel, wert, beschreibung) in settings)
            {
                await conn.ExecuteAsync(@"
                    MERGE INTO NOVVIA.FirmaEinstellung AS target
                    USING (SELECT @Schluessel AS cSchluessel) AS source
                    ON target.cSchluessel = source.cSchluessel
                    WHEN MATCHED THEN UPDATE SET cWert = @Wert, cBeschreibung = @Beschreibung, dGeaendert = GETDATE()
                    WHEN NOT MATCHED THEN INSERT (cSchluessel, cWert, cBeschreibung, dGeaendert)
                        VALUES (@Schluessel, @Wert, @Beschreibung, GETDATE());",
                    new { Schluessel = schluessel, Wert = wert, Beschreibung = beschreibung });
            }
        }

        public class NovviaEinstellungen
        {
            public bool PharmaModus { get; set; }
            public int QuarantaeneLagerId { get; set; } = 3;
            public int AutoMatchSchwelle { get; set; } = 90;
            public string? Firmenname { get; set; }
            public string? LogoPfad { get; set; }  // Pfad zum Firmenlogo (PNG/JPG)
        }

        #endregion

        #region Benutzer-Einstellungen (Spalten, UI-State)

        /// <summary>
        /// Benutzer-Einstellung laden (z.B. Spaltenreihenfolge)
        /// </summary>
        public async Task<string?> GetBenutzerEinstellungAsync(int kBenutzer, string schluessel)
        {
            var conn = await GetConnectionAsync();
            return await conn.QuerySingleOrDefaultAsync<string>(
                "SELECT cWert FROM NOVVIA.BenutzerEinstellung WHERE kBenutzer = @KBenutzer AND cSchluessel = @Schluessel",
                new { KBenutzer = kBenutzer, Schluessel = schluessel });
        }

        /// <summary>
        /// Benutzer-Einstellung speichern
        /// </summary>
        public async Task SaveBenutzerEinstellungAsync(int kBenutzer, string schluessel, string wert)
        {
            var conn = await GetConnectionAsync();
            await conn.ExecuteAsync(@"
                MERGE INTO NOVVIA.BenutzerEinstellung AS target
                USING (SELECT @KBenutzer AS kBenutzer, @Schluessel AS cSchluessel) AS source
                ON target.kBenutzer = source.kBenutzer AND target.cSchluessel = source.cSchluessel
                WHEN MATCHED THEN UPDATE SET cWert = @Wert, dGeaendert = GETDATE()
                WHEN NOT MATCHED THEN INSERT (kBenutzer, cSchluessel, cWert)
                    VALUES (@KBenutzer, @Schluessel, @Wert);",
                new { KBenutzer = kBenutzer, Schluessel = schluessel, Wert = wert });
        }

        /// <summary>
        /// Alle Benutzer-Einstellungen fuer einen View laden
        /// </summary>
        public async Task<Dictionary<string, string>> GetBenutzerEinstellungenAsync(int kBenutzer, string viewPrefix)
        {
            var conn = await GetConnectionAsync();
            var results = await conn.QueryAsync<(string Schluessel, string Wert)>(
                "SELECT cSchluessel, cWert FROM NOVVIA.BenutzerEinstellung WHERE kBenutzer = @KBenutzer AND cSchluessel LIKE @Prefix + '%'",
                new { KBenutzer = kBenutzer, Prefix = viewPrefix });

            return results.ToDictionary(r => r.Schluessel, r => r.Wert);
        }

        #endregion

        /// <summary>
        /// Charge sperren (in Quarantaene-Lager verschieben) - JTL-konform
        /// </summary>
        public async Task ChargeSperre(int kWarenLagerEingang, string sperrgrund, string? sperrvermerk, int kBenutzer)
        {
            // In JTL wird Sperrung durch Umlagerung ins Quarantaene-Lager realisiert
            await ChargeInQuarantaene(kWarenLagerEingang, sperrgrund, kBenutzer);
        }

        /// <summary>
        /// Charge freigeben (aus Quarantaene-Lager zurueck ins Hauptlager) - JTL-konform
        /// </summary>
        public async Task ChargeFreigabe(int kWarenLagerEingang, string? hinweis, int kBenutzer)
        {
            // In JTL wird Freigabe durch Umlagerung zurueck ins Hauptlager realisiert
            await ChargeAusQuarantaene(kWarenLagerEingang, hinweis, kBenutzer);
        }

        /// <summary>
        /// Charge in Quarantaene-Lager verschieben - JTL-konform per SP dbo.spPlatzUmbuchen
        /// </summary>
        public async Task ChargeInQuarantaene(int kWarenLagerEingang, string grund, int kBenutzer)
        {
            var conn = await GetConnectionAsync();

            // Charge-Daten laden
            var charge = await conn.QueryFirstOrDefaultAsync<(int KArtikel, decimal FAnzahl, int KWarenLagerPlatz, string? CChargenNr, DateTime? DMHD)>(@"
                SELECT kArtikel, fAnzahlAktuell, kWarenLagerPlatz, cChargenNr, dMHD
                FROM tWarenLagerEingang WHERE kWarenLagerEingang = @Id",
                new { Id = kWarenLagerEingang });

            if (charge.KArtikel == 0)
                throw new InvalidOperationException($"WarenLagerEingang {kWarenLagerEingang} nicht gefunden.");

            // Quarantaene-Lager aus Einstellungen lesen
            var quarantaeneLager = await GetQuarantaeneLagerIdAsync();
            var quarantaenePlatz = await conn.QueryFirstOrDefaultAsync<int?>(
                "SELECT TOP 1 kWarenLagerPlatz FROM tWarenLagerPlatz WHERE kWarenLager = @KWarenLager",
                new { KWarenLager = quarantaeneLager });

            if (quarantaenePlatz == null)
                throw new InvalidOperationException($"Kein Lagerplatz im Quarantaene-Lager (kWarenLager={quarantaeneLager}) gefunden. Bitte zuerst anlegen.");

            // Ursprungslagerplatz in NOVVIA.ChargenBewegung protokollieren
            await conn.ExecuteAsync(@"
                INSERT INTO NOVVIA.ChargenBewegung (kWarenLagerEingang, kArtikel, cChargenNr, cAktion, cGrund, cHinweis, kVonWarenLager, kNachWarenLager, fMenge, kBenutzer, dErstellt)
                SELECT @KWarenLagerEingang, @KArtikel, @CChargenNr, 'Quarantaene', @Grund, NULL,
                       wlp_alt.kWarenLager, @QuarantaeneLager, @FAnzahl, @KBenutzer, GETDATE()
                FROM tWarenLagerPlatz wlp_alt WHERE wlp_alt.kWarenLagerPlatz = @KWarenLagerPlatzAlt",
                new {
                    KWarenLagerEingang = kWarenLagerEingang,
                    KArtikel = charge.KArtikel,
                    CChargenNr = charge.CChargenNr,
                    Grund = grund,
                    QuarantaeneLager = quarantaeneLager,
                    FAnzahl = charge.FAnzahl,
                    KBenutzer = kBenutzer,
                    KWarenLagerPlatzAlt = charge.KWarenLagerPlatz
                });

            // JTL SP aufrufen: dbo.spPlatzUmbuchen
            // kBuchungsart = 60 (Lagerumbuchung)
            var kommentar = $"Quarantaene: {grund}";
            await conn.ExecuteAsync(@"
                EXEC dbo.spPlatzUmbuchen
                    @kArtikel = @KArtikel,
                    @fAnzahl = @FAnzahl,
                    @kWarenlagerPLatzNeu = @KWarenLagerPlatzNeu,
                    @kWarenlagerPlatzAlt = @KWarenLagerPlatzAlt,
                    @cKommentar = @CKommentar,
                    @kBuchungsart = 60,
                    @kBenutzer = @KBenutzer,
                    @dMhd = @DMHD,
                    @cChargenNr = @CChargenNr",
                new {
                    KArtikel = charge.KArtikel,
                    FAnzahl = charge.FAnzahl,
                    KWarenLagerPlatzNeu = quarantaenePlatz,
                    KWarenLagerPlatzAlt = charge.KWarenLagerPlatz,
                    CKommentar = kommentar,
                    KBenutzer = kBenutzer,
                    DMHD = charge.DMHD,
                    CChargenNr = charge.CChargenNr
                });
        }

        /// <summary>
        /// Charge aus Quarantaene zurueck ins Ursprungslager - JTL-konform per SP dbo.spPlatzUmbuchen
        /// </summary>
        public async Task ChargeAusQuarantaene(int kWarenLagerEingang, string? hinweis, int kBenutzer)
        {
            var conn = await GetConnectionAsync();

            // Charge-Daten laden
            var charge = await conn.QueryFirstOrDefaultAsync<(int KArtikel, decimal FAnzahl, int KWarenLagerPlatz, string? CChargenNr, DateTime? DMHD)>(@"
                SELECT kArtikel, fAnzahlAktuell, kWarenLagerPlatz, cChargenNr, dMHD
                FROM tWarenLagerEingang WHERE kWarenLagerEingang = @Id",
                new { Id = kWarenLagerEingang });

            if (charge.KArtikel == 0)
                throw new InvalidOperationException($"WarenLagerEingang {kWarenLagerEingang} nicht gefunden.");

            // Ursprungslagerplatz aus NOVVIA.ChargenBewegung lesen (letzte Quarantaene-Bewegung)
            var ursprung = await conn.QueryFirstOrDefaultAsync<int?>(@"
                SELECT TOP 1 kVonWarenLager
                FROM NOVVIA.ChargenBewegung
                WHERE kWarenLagerEingang = @Id AND cAktion = 'Quarantaene'
                ORDER BY dErstellt DESC",
                new { Id = kWarenLagerEingang });

            // Ersten Lagerplatz im Ursprungslager ermitteln
            int? zielPlatz = null;
            if (ursprung.HasValue)
            {
                zielPlatz = await conn.QueryFirstOrDefaultAsync<int?>(
                    "SELECT TOP 1 kWarenLagerPlatz FROM tWarenLagerPlatz WHERE kWarenLager = @KWarenLager ORDER BY nSort",
                    new { KWarenLager = ursprung.Value });
            }

            // Falls kein Ursprungslager bekannt, Standard-Lager (kWarenLager=1)
            if (zielPlatz == null)
            {
                zielPlatz = await conn.QueryFirstOrDefaultAsync<int?>(
                    "SELECT TOP 1 kWarenLagerPlatz FROM tWarenLagerPlatz WHERE kWarenLager = 1 ORDER BY nSort");
            }

            if (zielPlatz == null)
                throw new InvalidOperationException("Kein Ziellagerplatz fuer Freigabe gefunden.");

            // Freigabe in NOVVIA.ChargenBewegung protokollieren
            var quarantaeneLager = await GetQuarantaeneLagerIdAsync();
            await conn.ExecuteAsync(@"
                INSERT INTO NOVVIA.ChargenBewegung (kWarenLagerEingang, kArtikel, cChargenNr, cAktion, cGrund, cHinweis, kVonWarenLager, kNachWarenLager, fMenge, kBenutzer, dErstellt)
                SELECT @KWarenLagerEingang, @KArtikel, @CChargenNr, 'Freigabe', NULL, @Hinweis,
                       @QuarantaeneLager, wlp_neu.kWarenLager, @FAnzahl, @KBenutzer, GETDATE()
                FROM tWarenLagerPlatz wlp_neu WHERE wlp_neu.kWarenLagerPlatz = @KWarenLagerPlatzNeu",
                new {
                    KWarenLagerEingang = kWarenLagerEingang,
                    KArtikel = charge.KArtikel,
                    CChargenNr = charge.CChargenNr,
                    Hinweis = hinweis,
                    QuarantaeneLager = quarantaeneLager,
                    FAnzahl = charge.FAnzahl,
                    KBenutzer = kBenutzer,
                    KWarenLagerPlatzNeu = zielPlatz
                });

            // JTL SP aufrufen: dbo.spPlatzUmbuchen
            var kommentar = $"Freigabe: {hinweis ?? ""}";
            await conn.ExecuteAsync(@"
                EXEC dbo.spPlatzUmbuchen
                    @kArtikel = @KArtikel,
                    @fAnzahl = @FAnzahl,
                    @kWarenlagerPLatzNeu = @KWarenLagerPlatzNeu,
                    @kWarenlagerPlatzAlt = @KWarenLagerPlatzAlt,
                    @cKommentar = @CKommentar,
                    @kBuchungsart = 60,
                    @kBenutzer = @KBenutzer,
                    @dMhd = @DMHD,
                    @cChargenNr = @CChargenNr",
                new {
                    KArtikel = charge.KArtikel,
                    FAnzahl = charge.FAnzahl,
                    KWarenLagerPlatzNeu = zielPlatz,
                    KWarenLagerPlatzAlt = charge.KWarenLagerPlatz,
                    CKommentar = kommentar,
                    KBenutzer = kBenutzer,
                    DMHD = charge.DMHD,
                    CChargenNr = charge.CChargenNr
                });
        }

        public class ChargenBestand
        {
            public int KWarenLagerEingang { get; set; }
            public int KArtikel { get; set; }
            public string CArtNr { get; set; } = "";
            public string CArtikelName { get; set; } = "";
            public string CChargenNr { get; set; } = "";
            public DateTime? DMHD { get; set; }
            public decimal FBestand { get; set; }
            public decimal FEingang { get; set; }
            public int KWarenLager { get; set; }
            public string CLagerName { get; set; } = "";
            public DateTime DEingang { get; set; }
            public string? CLieferscheinNr { get; set; }
            public bool NGesperrt { get; set; }
            public bool NQuarantaene { get; set; }
            public string? CSperrgrund { get; set; }
            public DateTime? DGesperrtAm { get; set; }
            public string CMHDStatus { get; set; } = "";
            public int? NTageRestMHD { get; set; }

            public string Status => NQuarantaene ? "Quarantäne" : (NGesperrt ? "Gesperrt" : (CMHDStatus == "Abgelaufen" ? "MHD abgelaufen" : "Frei"));
            public string StatusFarbe => NQuarantaene ? "#6c757d" : (NGesperrt ? "#dc3545" : (CMHDStatus == "Abgelaufen" ? "#ffc107" : "#28a745"));
        }

        public class ChargenBewegung
        {
            public int KChargenBewegung { get; set; }
            public int KWarenLagerEingang { get; set; }
            public int KArtikel { get; set; }
            public string CArtNr { get; set; } = "";
            public string CChargenNr { get; set; } = "";
            public string CAktion { get; set; } = "";
            public string? CGrund { get; set; }
            public string? CHinweis { get; set; }
            public int? KVonWarenLager { get; set; }
            public string? CVonLagerName { get; set; }
            public int? KNachWarenLager { get; set; }
            public string? CNachLagerName { get; set; }
            public decimal? FMenge { get; set; }
            public int KBenutzer { get; set; }
            public DateTime DErstellt { get; set; }

            public string AktionText => CAktion switch
            {
                "GESPERRT" => "Gesperrt",
                "FREIGEGEBEN" => "Freigegeben",
                "QUARANTAENE" => "In Quarantäne",
                "RUECKBUCHUNG" => "Aus Quarantäne",
                _ => CAktion
            };
        }

        /// <summary>
        /// Chargen-Ausgänge für Rückrufverfolgung - welche Charge ging an welchen Kunden
        /// </summary>
        public class ChargenAusgang
        {
            public int KWarenLagerAusgang { get; set; }
            public int KWarenLagerEingang { get; set; }
            public string CChargenNr { get; set; } = "";
            public DateTime? DMHD { get; set; }
            public decimal FMenge { get; set; }
            public DateTime DAusgang { get; set; }

            // Lieferschein
            public int? KLieferschein { get; set; }
            public string? CLieferscheinNr { get; set; }
            public DateTime? DLieferschein { get; set; }

            // Auftrag/Bestellung
            public int? KBestellung { get; set; }
            public string? CBestellNr { get; set; }

            // Kunde
            public int? KKunde { get; set; }
            public string? CKundenNr { get; set; }
            public string? KundeName { get; set; }
            public string? KundeOrt { get; set; }
        }

        public async Task<IEnumerable<ChargenAusgang>> GetChargenAusgaengeAsync(int? kArtikel = null, string? chargenNr = null, int limit = 500)
        {
            var conn = await GetConnectionAsync();
            var sql = @"
                SELECT TOP (@Limit)
                    wa.kWarenLagerAusgang AS KWarenLagerAusgang,
                    wa.kWarenLagerEingang AS KWarenLagerEingang,
                    we.cChargenNr AS CChargenNr,
                    CASE WHEN we.dMHD > '1900-01-02' THEN we.dMHD ELSE NULL END AS DMHD,
                    wa.fAnzahl AS FMenge,
                    CASE WHEN wa.dErstellt > '1900-01-02' THEN wa.dErstellt ELSE NULL END AS DAusgang,
                    ls.kLieferschein AS KLieferschein,
                    ls.cLieferscheinNr AS CLieferscheinNr,
                    CASE WHEN ls.dErstellt > '1900-01-02' THEN ls.dErstellt ELSE NULL END AS DLieferschein,
                    b.kBestellung AS KBestellung,
                    b.cBestellNr AS CBestellNr,
                    k.kKunde AS KKunde,
                    k.cKundenNr AS CKundenNr,
                    ISNULL(a.cFirma, a.cVorname + ' ' + a.cName) AS KundeName,
                    a.cOrt AS KundeOrt
                FROM dbo.tWarenLagerAusgang wa
                JOIN dbo.tWarenLagerEingang we ON wa.kWarenLagerEingang = we.kWarenLagerEingang
                LEFT JOIN dbo.tLieferscheinPos lp ON wa.kLieferscheinPos = lp.kLieferscheinPos
                LEFT JOIN dbo.tLieferschein ls ON lp.kLieferschein = ls.kLieferschein
                LEFT JOIN dbo.tBestellung b ON ls.kBestellung = b.kBestellung
                LEFT JOIN dbo.tKunde k ON b.tKunde_kKunde = k.kKunde
                LEFT JOIN dbo.tAdresse a ON b.tAdresse_kAdresse = a.kAdresse
                WHERE 1=1";

            if (kArtikel.HasValue)
                sql += " AND wa.kArtikel = @KArtikel";
            if (!string.IsNullOrEmpty(chargenNr))
                sql += " AND we.cChargenNr = @ChargenNr";

            sql += " ORDER BY wa.dErstellt DESC";

            return await conn.QueryAsync<ChargenAusgang>(sql, new { Limit = limit, KArtikel = kArtikel, ChargenNr = chargenNr });
        }

        #endregion

        #region Benutzerrechte

        /// <summary>
        /// Prüft ob der Pharma-Modus aktiv ist (aus JTL Firma eigene Felder)
        /// </summary>
        public async Task<bool> IstPharmaModusAktivAsync()
        {
            try
            {
                var conn = await GetConnectionAsync();
                var result = await conn.ExecuteScalarAsync<int?>(@"
                    SELECT CASE WHEN f.nWertInt = 1 THEN 1 ELSE 0 END
                    FROM Firma.tFirmaEigenesFeld f
                    JOIN dbo.tAttributSprache a ON f.kAttribut = a.kAttribut AND a.kSprache = 0
                    WHERE a.cName = 'Pharma'
                ");
                return result == 1;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"IstPharmaModusAktivAsync Fehler: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Prüft ob ein Benutzer ein bestimmtes Recht hat
        /// </summary>
        public async Task<bool> HatRechtAsync(int kBenutzer, string cRechtSchluessel)
        {
            try
            {
                var conn = await GetConnectionAsync();
                var result = await conn.QueryFirstOrDefaultAsync<int?>(@"
                    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.ROUTINES WHERE ROUTINE_SCHEMA = 'NOVVIA' AND ROUTINE_NAME = 'spHatRecht')
                    BEGIN
                        DECLARE @nHatRecht BIT;
                        EXEC NOVVIA.spHatRecht @kBenutzer = @kBenutzer, @cRechtSchluessel = @cRechtSchluessel, @nHatRecht = @nHatRecht OUTPUT;
                        SELECT @nHatRecht;
                    END
                    ELSE
                        SELECT 1
                ", new { kBenutzer, cRechtSchluessel });
                return result == 1;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HatRechtAsync Fehler: {ex.Message}");
                return true; // Bei Fehler erlauben, um Sperren zu vermeiden
            }
        }

        /// <summary>
        /// Prüft ob ein Benutzer Validierungsfelder bearbeiten darf (PHARMA-Modus)
        /// </summary>
        public async Task<bool> DarfValidierungBearbeitenAsync(int kBenutzer, string cModul)
        {
            try
            {
                var conn = await GetConnectionAsync();
                var result = await conn.QueryFirstOrDefaultAsync<int?>(@"
                    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.ROUTINES WHERE ROUTINE_SCHEMA = 'NOVVIA' AND ROUTINE_NAME = 'spDarfValidierungBearbeiten')
                    BEGIN
                        DECLARE @nDarf BIT;
                        EXEC NOVVIA.spDarfValidierungBearbeiten @kBenutzer = @kBenutzer, @cModul = @cModul, @nDarf = @nDarf OUTPUT;
                        SELECT @nDarf;
                    END
                    ELSE
                        SELECT 1
                ", new { kBenutzer, cModul });
                return result == 1;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DarfValidierungBearbeitenAsync Fehler: {ex.Message}");
                return true; // Bei Fehler erlauben
            }
        }

        /// <summary>
        /// Lädt die Rollen eines Benutzers
        /// </summary>
        public async Task<List<BenutzerRolle>> GetBenutzerRollenAsync(int kBenutzer)
        {
            try
            {
                var conn = await GetConnectionAsync();
                var result = await conn.QueryAsync<BenutzerRolle>(@"
                    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'NOVVIA' AND TABLE_NAME = 'BenutzerRolle')
                    BEGIN
                        SELECT r.kRolle, r.cName, r.cBeschreibung, r.nAdmin, r.nAktiv
                        FROM NOVVIA.Rolle r
                        JOIN NOVVIA.BenutzerRolle br ON r.kRolle = br.kRolle
                        WHERE br.kBenutzer = @kBenutzer AND r.nAktiv = 1
                    END
                ", new { kBenutzer });
                return result?.ToList() ?? new List<BenutzerRolle>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetBenutzerRollenAsync Fehler: {ex.Message}");
                return new List<BenutzerRolle>();
            }
        }

        /// <summary>
        /// Lädt eine Firmeneinstellung
        /// </summary>
        public async Task<string?> GetFirmaEinstellungAsync(string cSchluessel)
        {
            try
            {
                var conn = await GetConnectionAsync();
                return await conn.ExecuteScalarAsync<string?>(@"
                    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'NOVVIA' AND TABLE_NAME = 'FirmaEinstellung')
                    BEGIN
                        SELECT cWert FROM NOVVIA.FirmaEinstellung WHERE cSchluessel = @cSchluessel
                    END
                ", new { cSchluessel });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetFirmaEinstellungAsync Fehler: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Setzt eine Firmeneinstellung
        /// </summary>
        public async Task<bool> SetFirmaEinstellungAsync(string cSchluessel, string cWert, int? kBenutzer = null)
        {
            try
            {
                var conn = await GetConnectionAsync();
                await conn.ExecuteAsync(@"
                    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'NOVVIA' AND TABLE_NAME = 'FirmaEinstellung')
                    BEGIN
                        IF EXISTS (SELECT 1 FROM NOVVIA.FirmaEinstellung WHERE cSchluessel = @cSchluessel)
                            UPDATE NOVVIA.FirmaEinstellung SET cWert = @cWert, dGeaendert = SYSDATETIME(), kGeaendertVon = @kBenutzer WHERE cSchluessel = @cSchluessel
                        ELSE
                            INSERT INTO NOVVIA.FirmaEinstellung (cSchluessel, cWert, kGeaendertVon) VALUES (@cSchluessel, @cWert, @kBenutzer)
                    END
                ", new { cSchluessel, cWert, kBenutzer });
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetFirmaEinstellungAsync Fehler: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Benutzer & Rollen Verwaltung

        public class NovviaBenutzer
        {
            public int KBenutzer { get; set; }
            public string CBenutzername { get; set; } = "";
            public string? CVorname { get; set; }
            public string? CNachname { get; set; }
            public string? CEmail { get; set; }
            public string? CTelefon { get; set; }
            public bool NAktiv { get; set; }
            public bool NGesperrt { get; set; }
            public string? CSperrgrund { get; set; }
            public DateTime? DLetzteAnmeldung { get; set; }
            public DateTime DErstellt { get; set; }
            public string VollName => $"{CVorname} {CNachname}".Trim();
        }

        public class NovviaRolle
        {
            public int KRolle { get; set; }
            public string CName { get; set; } = "";
            public string CBezeichnung { get; set; } = "";
            public string? CBeschreibung { get; set; }
            public bool NIstSystem { get; set; }
            public bool NIstAdmin { get; set; }
            public bool NAktiv { get; set; }
            public DateTime DErstellt { get; set; }
        }

        public class RolleSelection
        {
            public int KRolle { get; set; }
            public string CBezeichnung { get; set; } = "";
            public bool IsSelected { get; set; }
        }

        /// <summary>
        /// Lädt alle NOVVIA Benutzer
        /// </summary>
        public async Task<List<NovviaBenutzer>> GetNovviaBenutzerAsync()
        {
            try
            {
                var conn = await GetConnectionAsync();
                var result = await conn.QueryAsync<NovviaBenutzer>(@"
                    SELECT kBenutzer, cBenutzername, cVorname, cNachname, cEmail, cTelefon,
                           nAktiv, nGesperrt, cSperrgrund, dLetzteAnmeldung, dErstellt
                    FROM NOVVIA.Benutzer
                    ORDER BY cBenutzername
                ");
                return result.ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetNovviaBenutzerAsync Fehler: {ex.Message}");
                return new List<NovviaBenutzer>();
            }
        }

        /// <summary>
        /// Lädt alle NOVVIA Rollen
        /// </summary>
        public async Task<List<NovviaRolle>> GetNovviaRollenAsync()
        {
            try
            {
                var conn = await GetConnectionAsync();
                var result = await conn.QueryAsync<NovviaRolle>(@"
                    SELECT kRolle, cName, cBezeichnung, cBeschreibung, nIstSystem, nIstAdmin, nAktiv, dErstellt
                    FROM NOVVIA.Rolle
                    ORDER BY cBezeichnung
                ");
                return result.ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetNovviaRollenAsync Fehler: {ex.Message}");
                return new List<NovviaRolle>();
            }
        }

        /// <summary>
        /// Lädt Rollen eines Benutzers (mit Selection-Flag)
        /// </summary>
        public async Task<List<RolleSelection>> GetBenutzerRollenSelectionAsync(int kBenutzer)
        {
            try
            {
                var conn = await GetConnectionAsync();
                var result = await conn.QueryAsync<RolleSelection>(@"
                    SELECT r.kRolle, r.cBezeichnung,
                           CASE WHEN br.kBenutzerRolle IS NOT NULL THEN 1 ELSE 0 END AS IsSelected
                    FROM NOVVIA.Rolle r
                    LEFT JOIN NOVVIA.BenutzerRolle br ON r.kRolle = br.kRolle AND br.kBenutzer = @kBenutzer
                    WHERE r.nAktiv = 1
                    ORDER BY r.cBezeichnung
                ", new { kBenutzer });
                return result.ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetBenutzerRollenSelectionAsync Fehler: {ex.Message}");
                return new List<RolleSelection>();
            }
        }

        /// <summary>
        /// Lädt Benutzer die eine bestimmte Rolle haben
        /// </summary>
        public async Task<List<NovviaBenutzer>> GetBenutzerMitRolleAsync(int kRolle)
        {
            try
            {
                var conn = await GetConnectionAsync();
                var result = await conn.QueryAsync<NovviaBenutzer>(@"
                    SELECT b.kBenutzer, b.cBenutzername, b.cVorname, b.cNachname, b.cEmail,
                           b.nAktiv, b.nGesperrt
                    FROM NOVVIA.Benutzer b
                    INNER JOIN NOVVIA.BenutzerRolle br ON b.kBenutzer = br.kBenutzer
                    WHERE br.kRolle = @kRolle
                    ORDER BY b.cBenutzername
                ", new { kRolle });
                return result.ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetBenutzerMitRolleAsync Fehler: {ex.Message}");
                return new List<NovviaBenutzer>();
            }
        }

        /// <summary>
        /// Erstellt einen neuen Benutzer
        /// </summary>
        public async Task<int> CreateNovviaBenutzerAsync(NovviaBenutzer benutzer, string passwort)
        {
            try
            {
                var conn = await GetConnectionAsync();
                // Passwort hashen (einfacher SHA256 Hash, in Produktion BCrypt empfohlen)
                var passwortHash = ComputePasswordHash(passwort);
                var result = await conn.QuerySingleAsync<int>(@"
                    INSERT INTO NOVVIA.Benutzer (cBenutzername, cPasswortHash, cVorname, cNachname, cEmail, cTelefon,
                                                  nAktiv, nGesperrt, cSprache, cTheme, dErstellt)
                    VALUES (@CBenutzername, @Hash, @CVorname, @CNachname, @CEmail, @CTelefon,
                            @NAktiv, @NGesperrt, 'de', 'light', SYSDATETIME());
                    SELECT SCOPE_IDENTITY();
                ", new
                {
                    benutzer.CBenutzername,
                    Hash = passwortHash,
                    benutzer.CVorname,
                    benutzer.CNachname,
                    benutzer.CEmail,
                    benutzer.CTelefon,
                    benutzer.NAktiv,
                    benutzer.NGesperrt
                });
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateNovviaBenutzerAsync Fehler: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Aktualisiert einen Benutzer
        /// </summary>
        public async Task UpdateNovviaBenutzerAsync(NovviaBenutzer benutzer)
        {
            try
            {
                var conn = await GetConnectionAsync();
                await conn.ExecuteAsync(@"
                    UPDATE NOVVIA.Benutzer SET
                        cBenutzername = @CBenutzername,
                        cVorname = @CVorname,
                        cNachname = @CNachname,
                        cEmail = @CEmail,
                        cTelefon = @CTelefon,
                        nAktiv = @NAktiv,
                        nGesperrt = @NGesperrt,
                        dGeaendert = SYSDATETIME()
                    WHERE kBenutzer = @KBenutzer
                ", benutzer);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateNovviaBenutzerAsync Fehler: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Setzt das Passwort eines Benutzers
        /// </summary>
        public async Task SetBenutzerPasswortAsync(int kBenutzer, string neuesPasswort)
        {
            try
            {
                var conn = await GetConnectionAsync();
                var passwortHash = ComputePasswordHash(neuesPasswort);
                await conn.ExecuteAsync(@"
                    UPDATE NOVVIA.Benutzer SET
                        cPasswortHash = @Hash,
                        nFehlversuche = 0,
                        dGeaendert = SYSDATETIME()
                    WHERE kBenutzer = @kBenutzer
                ", new { kBenutzer, Hash = passwortHash });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetBenutzerPasswortAsync Fehler: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Löscht einen Benutzer
        /// </summary>
        public async Task DeleteNovviaBenutzerAsync(int kBenutzer)
        {
            try
            {
                var conn = await GetConnectionAsync();
                // Zuerst Rollenzuweisungen löschen
                await conn.ExecuteAsync("DELETE FROM NOVVIA.BenutzerRolle WHERE kBenutzer = @kBenutzer", new { kBenutzer });
                // Dann Benutzer löschen
                await conn.ExecuteAsync("DELETE FROM NOVVIA.Benutzer WHERE kBenutzer = @kBenutzer", new { kBenutzer });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteNovviaBenutzerAsync Fehler: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Setzt die Rollen eines Benutzers
        /// </summary>
        public async Task SetBenutzerRollenAsync(int kBenutzer, List<int> rollenIds)
        {
            try
            {
                var conn = await GetConnectionAsync();
                // Alle bestehenden Rollen löschen
                await conn.ExecuteAsync("DELETE FROM NOVVIA.BenutzerRolle WHERE kBenutzer = @kBenutzer", new { kBenutzer });
                // Neue Rollen einfügen
                foreach (var kRolle in rollenIds)
                {
                    await conn.ExecuteAsync(@"
                        INSERT INTO NOVVIA.BenutzerRolle (kBenutzer, kRolle, dErstellt)
                        VALUES (@kBenutzer, @kRolle, SYSDATETIME())
                    ", new { kBenutzer, kRolle });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetBenutzerRollenAsync Fehler: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Erstellt eine neue Rolle
        /// </summary>
        public async Task<int> CreateNovviaRolleAsync(NovviaRolle rolle)
        {
            try
            {
                var conn = await GetConnectionAsync();
                var result = await conn.QuerySingleAsync<int>(@"
                    INSERT INTO NOVVIA.Rolle (cName, cBezeichnung, cBeschreibung, nIstSystem, nIstAdmin, nAktiv, dErstellt)
                    VALUES (@CName, @CBezeichnung, @CBeschreibung, 0, @NIstAdmin, @NAktiv, SYSDATETIME());
                    SELECT SCOPE_IDENTITY();
                ", rolle);
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateNovviaRolleAsync Fehler: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Aktualisiert eine Rolle
        /// </summary>
        public async Task UpdateNovviaRolleAsync(NovviaRolle rolle)
        {
            try
            {
                var conn = await GetConnectionAsync();
                await conn.ExecuteAsync(@"
                    UPDATE NOVVIA.Rolle SET
                        cName = @CName,
                        cBezeichnung = @CBezeichnung,
                        cBeschreibung = @CBeschreibung,
                        nIstAdmin = @NIstAdmin,
                        nAktiv = @NAktiv,
                        dGeaendert = SYSDATETIME()
                    WHERE kRolle = @KRolle
                ", rolle);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateNovviaRolleAsync Fehler: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Löscht eine Rolle (nur wenn nicht System-Rolle)
        /// </summary>
        public async Task DeleteNovviaRolleAsync(int kRolle)
        {
            try
            {
                var conn = await GetConnectionAsync();
                // Prüfen ob System-Rolle
                var isSystem = await conn.ExecuteScalarAsync<bool>(
                    "SELECT nIstSystem FROM NOVVIA.Rolle WHERE kRolle = @kRolle", new { kRolle });
                if (isSystem)
                    throw new InvalidOperationException("System-Rollen können nicht gelöscht werden.");

                // Zuerst Benutzer-Zuweisungen löschen
                await conn.ExecuteAsync("DELETE FROM NOVVIA.BenutzerRolle WHERE kRolle = @kRolle", new { kRolle });
                // Dann Rolle löschen
                await conn.ExecuteAsync("DELETE FROM NOVVIA.Rolle WHERE kRolle = @kRolle", new { kRolle });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteNovviaRolleAsync Fehler: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Einfacher Passwort-Hash (SHA256)
        /// </summary>
        private static string ComputePasswordHash(string password)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        #endregion

        #region Textmeldungen

        public class Textmeldung
        {
            public int KTextmeldung { get; set; }
            public string CTitel { get; set; } = "";
            public string CText { get; set; } = "";
            public int NTyp { get; set; } // 0=Info, 1=Warnung, 2=Wichtig
            public bool NBereichEinkauf { get; set; }
            public bool NBereichVerkauf { get; set; }
            public bool NBereichStammdaten { get; set; }
            public bool NBereichDokumente { get; set; }
            public bool NBereichOnline { get; set; }
            public bool NAktiv { get; set; } = true;
            public DateTime? DGueltigVon { get; set; }
            public DateTime? DGueltigBis { get; set; }
            public bool NPopupAnzeigen { get; set; } = true;
            public DateTime DErstellt { get; set; }

            public string TypBezeichnung => NTyp switch
            {
                0 => "Info",
                1 => "Warnung",
                2 => "Wichtig",
                _ => "Info"
            };

            public string BereicheText
            {
                get
                {
                    var bereiche = new List<string>();
                    if (NBereichEinkauf) bereiche.Add("Einkauf");
                    if (NBereichVerkauf) bereiche.Add("Verkauf");
                    if (NBereichStammdaten) bereiche.Add("Stammdaten");
                    if (NBereichDokumente) bereiche.Add("Dokumente");
                    if (NBereichOnline) bereiche.Add("Online");
                    return bereiche.Count > 0 ? string.Join(", ", bereiche) : "-";
                }
            }

            // UI-Helper (nicht in DB)
            public string ZuweisungenText { get; set; } = "";
        }

        public class EntityTextmeldung
        {
            public int KEntityTextmeldung { get; set; }
            public int KTextmeldung { get; set; }
            public string CEntityTyp { get; set; } = "";
            public int KEntity { get; set; }
            public DateTime DErstellt { get; set; }

            // Joined fields
            public string? CTitel { get; set; }
            public string? CText { get; set; }
            public int NTyp { get; set; }
            public bool NPopupAnzeigen { get; set; }
        }

        /// <summary>
        /// Lädt alle Textmeldungen
        /// </summary>
        public async Task<List<Textmeldung>> GetTextmeldungenAsync()
        {
            try
            {
                var conn = await GetConnectionAsync();
                var result = await conn.QueryAsync<Textmeldung>(@"
                    SELECT kTextmeldung, cTitel, cText, nTyp,
                           nBereichEinkauf, nBereichVerkauf, nBereichStammdaten,
                           nBereichDokumente, nBereichOnline,
                           nAktiv, dGueltigVon, dGueltigBis, nPopupAnzeigen, dErstellt
                    FROM NOVVIA.Textmeldung
                    ORDER BY cTitel
                ");
                return result.ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetTextmeldungenAsync Fehler: {ex.Message}");
                return new List<Textmeldung>();
            }
        }

        /// <summary>
        /// Lädt Textmeldungen für eine Entität (Kunde, Artikel, Lieferant)
        /// </summary>
        public async Task<List<EntityTextmeldung>> GetEntityTextmeldungenAsync(string entityTyp, int kEntity, string? bereich = null)
        {
            try
            {
                var conn = await GetConnectionAsync();
                var sql = @"
                    SELECT et.kEntityTextmeldung, et.kTextmeldung, et.cEntityTyp, et.kEntity, et.dErstellt,
                           t.cTitel, t.cText, t.nTyp, t.nPopupAnzeigen
                    FROM NOVVIA.EntityTextmeldung et
                    INNER JOIN NOVVIA.Textmeldung t ON et.kTextmeldung = t.kTextmeldung
                    WHERE et.cEntityTyp = @entityTyp AND et.kEntity = @kEntity
                      AND t.nAktiv = 1
                      AND (t.dGueltigVon IS NULL OR t.dGueltigVon <= CAST(GETDATE() AS DATE))
                      AND (t.dGueltigBis IS NULL OR t.dGueltigBis >= CAST(GETDATE() AS DATE))
                ";

                // Filter nach Bereich wenn angegeben
                if (!string.IsNullOrEmpty(bereich))
                {
                    sql += bereich switch
                    {
                        "Einkauf" => " AND t.nBereichEinkauf = 1",
                        "Verkauf" => " AND t.nBereichVerkauf = 1",
                        "Stammdaten" => " AND t.nBereichStammdaten = 1",
                        "Dokumente" => " AND t.nBereichDokumente = 1",
                        "Online" => " AND t.nBereichOnline = 1",
                        _ => ""
                    };
                }

                sql += " ORDER BY t.nTyp DESC, t.cTitel";

                var result = await conn.QueryAsync<EntityTextmeldung>(sql, new { entityTyp, kEntity });
                return result.ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetEntityTextmeldungenAsync Fehler: {ex.Message}");
                return new List<EntityTextmeldung>();
            }
        }

        /// <summary>
        /// Lädt Textmeldungen für Popup-Anzeige
        /// </summary>
        public async Task<List<EntityTextmeldung>> GetPopupTextmeldungenAsync(string entityTyp, int kEntity, string bereich)
        {
            try
            {
                var conn = await GetConnectionAsync();
                var bereichFilter = bereich switch
                {
                    "Einkauf" => "t.nBereichEinkauf = 1",
                    "Verkauf" => "t.nBereichVerkauf = 1",
                    "Stammdaten" => "t.nBereichStammdaten = 1",
                    "Dokumente" => "t.nBereichDokumente = 1",
                    "Online" => "t.nBereichOnline = 1",
                    _ => "1=1"
                };

                var result = await conn.QueryAsync<EntityTextmeldung>($@"
                    SELECT et.kEntityTextmeldung, et.kTextmeldung, et.cEntityTyp, et.kEntity,
                           t.cTitel, t.cText, t.nTyp, t.nPopupAnzeigen
                    FROM NOVVIA.EntityTextmeldung et
                    INNER JOIN NOVVIA.Textmeldung t ON et.kTextmeldung = t.kTextmeldung
                    WHERE et.cEntityTyp = @entityTyp AND et.kEntity = @kEntity
                      AND t.nAktiv = 1 AND t.nPopupAnzeigen = 1
                      AND (t.dGueltigVon IS NULL OR t.dGueltigVon <= CAST(GETDATE() AS DATE))
                      AND (t.dGueltigBis IS NULL OR t.dGueltigBis >= CAST(GETDATE() AS DATE))
                      AND {bereichFilter}
                    ORDER BY t.nTyp DESC
                ", new { entityTyp, kEntity });
                return result.ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetPopupTextmeldungenAsync Fehler: {ex.Message}");
                return new List<EntityTextmeldung>();
            }
        }

        /// <summary>
        /// Erstellt eine neue Textmeldung
        /// </summary>
        public async Task<int> CreateTextmeldungAsync(Textmeldung meldung, int? kBenutzer = null)
        {
            try
            {
                var conn = await GetConnectionAsync();
                var result = await conn.QuerySingleAsync<int>(@"
                    INSERT INTO NOVVIA.Textmeldung (cTitel, cText, nTyp,
                        nBereichEinkauf, nBereichVerkauf, nBereichStammdaten, nBereichDokumente, nBereichOnline,
                        nAktiv, dGueltigVon, dGueltigBis, nPopupAnzeigen, kErstelltVon)
                    VALUES (@CTitel, @CText, @NTyp,
                        @NBereichEinkauf, @NBereichVerkauf, @NBereichStammdaten, @NBereichDokumente, @NBereichOnline,
                        @NAktiv, @DGueltigVon, @DGueltigBis, @NPopupAnzeigen, @kBenutzer);
                    SELECT SCOPE_IDENTITY();
                ", new
                {
                    meldung.CTitel,
                    meldung.CText,
                    meldung.NTyp,
                    meldung.NBereichEinkauf,
                    meldung.NBereichVerkauf,
                    meldung.NBereichStammdaten,
                    meldung.NBereichDokumente,
                    meldung.NBereichOnline,
                    meldung.NAktiv,
                    meldung.DGueltigVon,
                    meldung.DGueltigBis,
                    meldung.NPopupAnzeigen,
                    kBenutzer
                });
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CreateTextmeldungAsync Fehler: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Aktualisiert eine Textmeldung
        /// </summary>
        public async Task UpdateTextmeldungAsync(Textmeldung meldung, int? kBenutzer = null)
        {
            try
            {
                var conn = await GetConnectionAsync();
                await conn.ExecuteAsync(@"
                    UPDATE NOVVIA.Textmeldung SET
                        cTitel = @CTitel, cText = @CText, nTyp = @NTyp,
                        nBereichEinkauf = @NBereichEinkauf, nBereichVerkauf = @NBereichVerkauf,
                        nBereichStammdaten = @NBereichStammdaten, nBereichDokumente = @NBereichDokumente,
                        nBereichOnline = @NBereichOnline,
                        nAktiv = @NAktiv, dGueltigVon = @DGueltigVon, dGueltigBis = @DGueltigBis,
                        nPopupAnzeigen = @NPopupAnzeigen,
                        dGeaendert = SYSDATETIME(), kGeaendertVon = @kBenutzer
                    WHERE kTextmeldung = @KTextmeldung
                ", new
                {
                    meldung.CTitel,
                    meldung.CText,
                    meldung.NTyp,
                    meldung.NBereichEinkauf,
                    meldung.NBereichVerkauf,
                    meldung.NBereichStammdaten,
                    meldung.NBereichDokumente,
                    meldung.NBereichOnline,
                    meldung.NAktiv,
                    meldung.DGueltigVon,
                    meldung.DGueltigBis,
                    meldung.NPopupAnzeigen,
                    meldung.KTextmeldung,
                    kBenutzer
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateTextmeldungAsync Fehler: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Löscht eine Textmeldung
        /// </summary>
        public async Task DeleteTextmeldungAsync(int kTextmeldung)
        {
            try
            {
                var conn = await GetConnectionAsync();
                // CASCADE löscht auch EntityTextmeldung Einträge
                await conn.ExecuteAsync("DELETE FROM NOVVIA.Textmeldung WHERE kTextmeldung = @kTextmeldung",
                    new { kTextmeldung });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeleteTextmeldungAsync Fehler: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Weist eine Textmeldung einer Entität zu
        /// </summary>
        public async Task<int> AddEntityTextmeldungAsync(int kTextmeldung, string entityTyp, int kEntity, int? kBenutzer = null)
        {
            try
            {
                var conn = await GetConnectionAsync();
                // Prüfen ob schon vorhanden
                var exists = await conn.ExecuteScalarAsync<int>(@"
                    SELECT COUNT(*) FROM NOVVIA.EntityTextmeldung
                    WHERE kTextmeldung = @kTextmeldung AND cEntityTyp = @entityTyp AND kEntity = @kEntity
                ", new { kTextmeldung, entityTyp, kEntity });

                if (exists > 0) return 0; // Bereits zugewiesen

                var result = await conn.QuerySingleAsync<int>(@"
                    INSERT INTO NOVVIA.EntityTextmeldung (kTextmeldung, cEntityTyp, kEntity, kErstelltVon)
                    VALUES (@kTextmeldung, @entityTyp, @kEntity, @kBenutzer);
                    SELECT SCOPE_IDENTITY();
                ", new { kTextmeldung, entityTyp, kEntity, kBenutzer });
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AddEntityTextmeldungAsync Fehler: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Entfernt eine Textmeldung von einer Entität
        /// </summary>
        public async Task RemoveEntityTextmeldungAsync(int kEntityTextmeldung)
        {
            try
            {
                var conn = await GetConnectionAsync();
                await conn.ExecuteAsync("DELETE FROM NOVVIA.EntityTextmeldung WHERE kEntityTextmeldung = @kEntityTextmeldung",
                    new { kEntityTextmeldung });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RemoveEntityTextmeldungAsync Fehler: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Lädt Entitäten die eine bestimmte Textmeldung haben
        /// </summary>
        public async Task<List<EntityTextmeldung>> GetTextmeldungEntitiesAsync(int kTextmeldung)
        {
            try
            {
                var conn = await GetConnectionAsync();
                var result = await conn.QueryAsync<EntityTextmeldung>(@"
                    SELECT kEntityTextmeldung, kTextmeldung, cEntityTyp, kEntity, dErstellt
                    FROM NOVVIA.EntityTextmeldung
                    WHERE kTextmeldung = @kTextmeldung
                    ORDER BY cEntityTyp, kEntity
                ", new { kTextmeldung });
                return result.ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetTextmeldungEntitiesAsync Fehler: {ex.Message}");
                return new List<EntityTextmeldung>();
            }
        }

        #endregion

        #region JTL Vorlagen

        public class JtlVorlageDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string Typ { get; set; } = "";
            public int VorlagenTyp { get; set; }
            public long DataSize { get; set; }
            public DateTime? LastModified { get; set; }
            public string TypName { get; set; } = "";
            public string SizeText { get; set; } = "";
        }

        public async Task<IEnumerable<JtlVorlageDto>> GetJtlVorlagenAsync()
        {
            var conn = await GetConnectionAsync();
            var vorlagen = await conn.QueryAsync<JtlVorlageDto>(@"
                SELECT kVorlage AS Id, cName AS Name, cTyp AS Typ, nVorlagentyp AS VorlagenTyp,
                       DATALENGTH(bDaten) AS DataSize, dLastModification AS LastModified
                FROM Report.tVorlage
                WHERE cTyp IN ('ll/project/lst', 'll/project/lbl')
                ORDER BY cName");

            return vorlagen.Select(v =>
            {
                v.TypName = GetVorlagenTypName(v.VorlagenTyp);
                v.SizeText = FormatFileSize(v.DataSize);
                return v;
            });
        }

        public async Task<IEnumerable<JtlVorlageDto>> GetJtlBilderAsync()
        {
            var conn = await GetConnectionAsync();
            var bilder = await conn.QueryAsync<JtlVorlageDto>(@"
                SELECT kVorlage AS Id, cName AS Name, cTyp AS Typ, nVorlagentyp AS VorlagenTyp,
                       DATALENGTH(bDaten) AS DataSize, dLastModification AS LastModified
                FROM Report.tVorlage
                WHERE cTyp = 'resource/image'
                ORDER BY cName");

            return bilder.Select(b =>
            {
                b.SizeText = FormatFileSize(b.DataSize);
                return b;
            });
        }

        public async Task<int> GetJtlEmailVorlagenCountAsync()
        {
            var conn = await GetConnectionAsync();
            return await conn.QuerySingleAsync<int>(
                "SELECT COUNT(*) FROM Report.tVorlage WHERE cTyp = 'mail/template/xml'");
        }

        public async Task<byte[]> GetJtlVorlageDatenAsync(int id)
        {
            var conn = await GetConnectionAsync();
            return await conn.QuerySingleAsync<byte[]>(
                "SELECT bDaten FROM Report.tVorlage WHERE kVorlage = @Id",
                new { Id = id });
        }

        public async Task SaveFormularBildAsync(string name, byte[] data)
        {
            var conn = await GetConnectionAsync();

            // Tabelle erstellen falls nicht vorhanden
            await conn.ExecuteAsync(@"
                IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='NOVVIA' AND TABLE_NAME='FormularBild')
                BEGIN
                    CREATE TABLE NOVVIA.FormularBild (
                        kFormularBild INT IDENTITY(1,1) PRIMARY KEY,
                        cName NVARCHAR(200) NOT NULL,
                        bDaten VARBINARY(MAX) NOT NULL,
                        dErstellt DATETIME DEFAULT GETDATE()
                    )
                END");

            // Bild speichern oder aktualisieren
            await conn.ExecuteAsync(@"
                IF EXISTS (SELECT 1 FROM NOVVIA.FormularBild WHERE cName = @Name)
                    UPDATE NOVVIA.FormularBild SET bDaten = @Data WHERE cName = @Name
                ELSE
                    INSERT INTO NOVVIA.FormularBild (cName, bDaten) VALUES (@Name, @Data)",
                new { Name = name, Data = data });
        }

        private static string GetVorlagenTypName(int typ) => typ switch
        {
            1 => "Angebot",
            2 => "Auftragsbestaetigung",
            4 => "Gutschrift",
            8 => "Lieferschein",
            15 => "Rechnung",
            16 => "Export",
            31 => "Standard",
            32 => "Baustein",
            128 => "Etikett",
            256 => "ZUGFeRD",
            _ => $"Typ {typ}"
        };

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }

        #endregion
    }

    #region Benutzerrechte DTOs

    public class BenutzerRolle
    {
        public int KRolle { get; set; }
        public string CName { get; set; } = "";
        public string? CBeschreibung { get; set; }
        public bool NAdmin { get; set; }
        public bool NAktiv { get; set; }
    }

    #endregion
}

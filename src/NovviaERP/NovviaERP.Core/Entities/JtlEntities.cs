using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NovviaERP.Core.Entities
{
    // =========================================================
    // JTL-WAWI 1.11 ENTITIES - KORREKTE SPALTENNAMEN
    // Basierend auf: wawi-db.jtl-software.de
    // =========================================================

    /// <summary>
    /// JTL Tabelle: tKunde
    /// </summary>
    [Table("tKunde")]
    public class JtlKunde
    {
        [Key]
        [Column("kKunde")]
        public int KKunde { get; set; }

        [Column("cKundenNr")]
        public string? CKundenNr { get; set; }

        [Column("cAnrede")]
        public string? CAnrede { get; set; }

        [Column("cTitel")]
        public string? CTitel { get; set; }

        [Column("cVorname")]
        public string? CVorname { get; set; }

        [Column("cName")]
        public string? CName { get; set; }  // Nachname!

        [Column("cFirma")]
        public string? CFirma { get; set; }

        [Column("cZusatz")]
        public string? CZusatz { get; set; }

        [Column("cStrasse")]
        public string? CStrasse { get; set; }

        [Column("cPLZ")]
        public string? CPLZ { get; set; }

        [Column("cOrt")]
        public string? COrt { get; set; }

        [Column("cBundesland")]
        public string? CBundesland { get; set; }

        [Column("cLand")]
        public string? CLand { get; set; }

        [Column("cTel")]
        public string? CTel { get; set; }

        [Column("cFax")]
        public string? CFax { get; set; }

        [Column("cMobil")]
        public string? CMobil { get; set; }

        [Column("cMail")]
        public string? CMail { get; set; }  // ACHTUNG: cMail nicht cEmail!

        [Column("cWWW")]
        public string? CWWW { get; set; }

        [Column("cUSTID")]
        public string? CUSTID { get; set; }

        [Column("cHerkunft")]
        public string? CHerkunft { get; set; }

        [Column("kKundengruppe")]
        public int? KKundengruppe { get; set; }

        [Column("cAnmerkung")]
        public string? CAnmerkung { get; set; }

        [Column("dErstellt")]
        public DateTime? DErstellt { get; set; }

        [Column("nAktiv")]
        public bool NAktiv { get; set; } = true;

        // Berechnete Eigenschaften
        [NotMapped]
        public string Anzeigename => !string.IsNullOrEmpty(CFirma) ? CFirma : $"{CVorname} {CName}".Trim();
    }

    /// <summary>
    /// JTL Tabelle: tArtikel
    /// </summary>
    [Table("tArtikel")]
    public class JtlArtikel
    {
        [Key]
        [Column("kArtikel")]
        public int KArtikel { get; set; }

        [Column("cArtNr")]
        public string? CArtNr { get; set; }

        [Column("cName")]
        public string? CName { get; set; }

        [Column("cBeschreibung")]
        public string? CBeschreibung { get; set; }

        [Column("cKurzBeschreibung")]
        public string? CKurzBeschreibung { get; set; }

        [Column("fVKNetto")]
        public decimal FVKNetto { get; set; }

        [Column("fVKBrutto")]
        public decimal FVKBrutto { get; set; }

        [Column("fLagerbestand")]
        public decimal FLagerbestand { get; set; }

        [Column("fMindestbestand")]
        public decimal? FMindestbestand { get; set; }

        [Column("cBarcode")]
        public string? CBarcode { get; set; }

        [Column("cISBN")]
        public string? CISBN { get; set; }

        [Column("fGewicht")]
        public decimal? FGewicht { get; set; }

        [Column("fBreite")]
        public decimal? FBreite { get; set; }

        [Column("fHoehe")]
        public decimal? FHoehe { get; set; }

        [Column("fLaenge")]
        public decimal? FLaenge { get; set; }

        [Column("kHersteller")]
        public int? KHersteller { get; set; }

        [Column("kSteuerklasse")]
        public int? KSteuerklasse { get; set; }

        [Column("kWarengruppe")]
        public int? KWarengruppe { get; set; }

        [Column("kMassEinheit")]
        public int? KMassEinheit { get; set; }

        [Column("nAktiv")]
        public bool NAktiv { get; set; } = true;

        [Column("dErstellt")]
        public DateTime? DErstellt { get; set; }

        [Column("dGeaendert")]
        public DateTime? DGeaendert { get; set; }
    }

    /// <summary>
    /// JTL Tabelle: tBestellung (Aufträge)
    /// </summary>
    [Table("tBestellung")]
    public class JtlBestellung
    {
        [Key]
        [Column("kBestellung")]
        public int KBestellung { get; set; }

        [Column("cBestellNr")]
        public string? CBestellNr { get; set; }

        [Column("cInetBestellNr")]
        public string? CInetBestellNr { get; set; }  // Externe Bestellnummer (Shop)

        [Column("tKunde_kKunde")]  // ACHTUNG: Nicht kKunde!
        public int TKunde_KKunde { get; set; }

        [Column("kSprache")]
        public int? KSprache { get; set; }

        [Column("cWaehrung")]
        public string CWaehrung { get; set; } = "EUR";

        [Column("tVersandArt_kVersandArt")]
        public int? TVersandArt_KVersandArt { get; set; }

        [Column("kZahlungsart")]
        public int? KZahlungsart { get; set; }

        [Column("nZahlungsziel")]
        public int? NZahlungsziel { get; set; }

        [Column("dErstellt")]
        public DateTime DErstellt { get; set; }

        [Column("dLieferdatum")]
        public DateTime? DLieferdatum { get; set; }

        [Column("dVersandt")]
        public DateTime? DVersandt { get; set; }

        [Column("dBezahlt")]
        public DateTime? DBezahlt { get; set; }

        [Column("nStatus")]
        public int NStatus { get; set; }

        [Column("cAnmerkung")]
        public string? CAnmerkung { get; set; }

        [Column("cBeschreibung")]
        public string? CBeschreibung { get; set; }

        [Column("cHerkunft")]
        public string? CHerkunft { get; set; }

        [Column("cIP")]
        public string? CIP { get; set; }

        [Column("kFirma")]
        public int? KFirma { get; set; }

        // Navigation
        [NotMapped]
        public JtlKunde? Kunde { get; set; }

        [NotMapped]
        public List<JtlBestellPos> Positionen { get; set; } = new();
    }

    /// <summary>
    /// JTL Tabelle: tBestellPos (Bestellpositionen)
    /// </summary>
    [Table("tBestellPos")]
    public class JtlBestellPos
    {
        [Key]
        [Column("kBestellPos")]
        public int KBestellPos { get; set; }

        [Column("tBestellung_kBestellung")]  // ACHTUNG: Nicht kBestellung!
        public int TBestellung_KBestellung { get; set; }

        [Column("tArtikel_kArtikel")]
        public int? TArtikel_KArtikel { get; set; }

        [Column("cArtNr")]
        public string? CArtNr { get; set; }

        [Column("cName")]
        public string? CName { get; set; }

        [Column("fAnzahl")]
        public decimal FAnzahl { get; set; }

        [Column("fVKNetto")]
        public decimal FVKNetto { get; set; }

        [Column("fVKBrutto")]
        public decimal? FVKBrutto { get; set; }

        [Column("fRabatt")]
        public decimal? FRabatt { get; set; }

        [Column("fMwSt")]
        public decimal FMwSt { get; set; }

        [Column("nPosTyp")]
        public int? NPosTyp { get; set; }

        [Column("cEinheit")]
        public string? CEinheit { get; set; }
    }

    /// <summary>
    /// JTL Tabelle: tRechnung
    /// </summary>
    [Table("tRechnung")]
    public class JtlRechnung
    {
        [Key]
        [Column("kRechnung")]
        public int KRechnung { get; set; }

        [Column("cRechnungsNr")]
        public string? CRechnungsNr { get; set; }

        [Column("tBestellung_kBestellung")]
        public int TBestellung_KBestellung { get; set; }

        [Column("dRechnungsDatum")]
        public DateTime? DRechnungsDatum { get; set; }

        [Column("dFaelligkeit")]
        public DateTime? DFaelligkeit { get; set; }

        [Column("fBetragNetto")]
        public decimal? FBetragNetto { get; set; }

        [Column("fBetragBrutto")]
        public decimal? FBetragBrutto { get; set; }

        [Column("nStatus")]
        public int NStatus { get; set; }
    }

    /// <summary>
    /// JTL Tabelle: tLieferschein
    /// </summary>
    [Table("tLieferschein")]
    public class JtlLieferschein
    {
        [Key]
        [Column("kLieferschein")]
        public int KLieferschein { get; set; }

        [Column("cLieferscheinNr")]
        public string? CLieferscheinNr { get; set; }

        [Column("tBestellung_kBestellung")]
        public int TBestellung_KBestellung { get; set; }

        [Column("dErstellt")]
        public DateTime DErstellt { get; set; }
    }

    /// <summary>
    /// JTL Tabelle: tLieferscheinPos
    /// </summary>
    [Table("tLieferscheinPos")]
    public class JtlLieferscheinPos
    {
        [Key]
        [Column("kLieferscheinPos")]
        public int KLieferscheinPos { get; set; }

        [Column("kLieferschein")]
        public int KLieferschein { get; set; }

        [Column("kBestellPos")]
        public int? KBestellPos { get; set; }

        [Column("fAnzahl")]
        public decimal FAnzahl { get; set; }
    }

    /// <summary>
    /// JTL Tabelle: tAngebot
    /// </summary>
    [Table("tAngebot")]
    public class JtlAngebot
    {
        [Key]
        [Column("kAngebot")]
        public int KAngebot { get; set; }

        [Column("cAngebotNr")]
        public string? CAngebotNr { get; set; }

        [Column("kKunde")]
        public int? KKunde { get; set; }

        [Column("dAngebot")]
        public DateTime DAngebot { get; set; }

        [Column("dGueltigBis")]
        public DateTime? DGueltigBis { get; set; }

        [Column("fGesamtNetto")]
        public decimal FGesamtNetto { get; set; }

        [Column("fGesamtBrutto")]
        public decimal FGesamtBrutto { get; set; }

        [Column("cWaehrung")]
        public string CWaehrung { get; set; } = "EUR";

        [Column("nStatus")]
        public int NStatus { get; set; }

        [Column("cAnmerkung")]
        public string? CAnmerkung { get; set; }

        [Column("kBestellung")]
        public int? KBestellung { get; set; }  // Wenn umgewandelt

        [NotMapped]
        public List<JtlAngebotPos> Positionen { get; set; } = new();
    }

    /// <summary>
    /// JTL Tabelle: tAngebotPos
    /// </summary>
    [Table("tAngebotPos")]
    public class JtlAngebotPos
    {
        [Key]
        [Column("kAngebotPos")]
        public int KAngebotPos { get; set; }

        [Column("kAngebot")]
        public int KAngebot { get; set; }

        [Column("kArtikel")]
        public int? KArtikel { get; set; }

        [Column("cArtNr")]
        public string? CArtNr { get; set; }

        [Column("cName")]
        public string? CName { get; set; }

        [Column("fAnzahl")]
        public decimal FAnzahl { get; set; }

        [Column("fVKNetto")]
        public decimal FVKNetto { get; set; }

        [Column("fRabatt")]
        public decimal? FRabatt { get; set; }

        [Column("fMwSt")]
        public decimal FMwSt { get; set; }
    }

    /// <summary>
    /// JTL Tabelle: tFirma
    /// </summary>
    [Table("tFirma")]
    public class JtlFirma
    {
        [Key]
        [Column("kFirma")]
        public int KFirma { get; set; }

        [Column("cName")]
        public string? CName { get; set; }

        [Column("cStrasse")]
        public string? CStrasse { get; set; }

        [Column("cPLZ")]
        public string? CPLZ { get; set; }

        [Column("cOrt")]
        public string? COrt { get; set; }

        [Column("cLand")]
        public string? CLand { get; set; }

        [Column("cTel")]
        public string? CTel { get; set; }

        [Column("cFax")]
        public string? CFax { get; set; }

        [Column("cEMail")]
        public string? CEMail { get; set; }

        [Column("cWWW")]
        public string? CWWW { get; set; }

        [Column("cUSTID")]
        public string? CUSTID { get; set; }

        [Column("cSteuernummer")]
        public string? CSteuernummer { get; set; }

        [Column("cBank")]
        public string? CBank { get; set; }

        [Column("cBLZ")]
        public string? CBLZ { get; set; }

        [Column("cKonto")]
        public string? CKonto { get; set; }

        [Column("cIBAN")]
        public string? CIBAN { get; set; }

        [Column("cBIC")]
        public string? CBIC { get; set; }
    }

    /// <summary>
    /// JTL Tabelle: tBenutzer
    /// </summary>
    [Table("tBenutzer")]
    public class JtlBenutzer
    {
        [Key]
        [Column("kBenutzer")]
        public int KBenutzer { get; set; }

        [Column("cName")]
        public string? CName { get; set; }

        [Column("cLogin")]
        public string? CLogin { get; set; }

        [Column("cPass")]
        public string? CPass { get; set; }

        [Column("cMail")]
        public string? CMail { get; set; }

        [Column("nAktiv")]
        public bool NAktiv { get; set; }
    }

    /// <summary>
    /// JTL Tabelle: tShop (Verkaufskanäle)
    /// </summary>
    [Table("tShop")]
    public class JtlShop
    {
        [Key]
        [Column("kShop")]
        public int KShop { get; set; }

        [Column("cName")]
        public string? CName { get; set; }

        [Column("cURL")]
        public string? CURL { get; set; }

        [Column("nAktiv")]
        public bool NAktiv { get; set; }

        [Column("nTyp")]
        public int? NTyp { get; set; }
    }

    /// <summary>
    /// JTL Tabelle: tWarenLager
    /// </summary>
    [Table("tWarenLager")]
    public class JtlWarenLager
    {
        [Key]
        [Column("kWarenLager")]
        public int KWarenLager { get; set; }

        [Column("cName")]
        public string? CName { get; set; }

        [Column("cKuerzel")]
        public string? CKuerzel { get; set; }

        [Column("nAktiv")]
        public bool NAktiv { get; set; }

        [Column("nStandard")]
        public bool NStandard { get; set; }
    }

    /// <summary>
    /// JTL Tabelle: tVersandArt
    /// </summary>
    [Table("tVersandArt")]
    public class JtlVersandArt
    {
        [Key]
        [Column("kVersandArt")]
        public int KVersandArt { get; set; }

        [Column("cName")]
        public string? CName { get; set; }

        [Column("cAnbieter")]
        public string? CAnbieter { get; set; }

        [Column("nAktiv")]
        public bool NAktiv { get; set; }
    }

    /// <summary>
    /// JTL Tabelle: tZahlungsart
    /// </summary>
    [Table("tZahlungsart")]
    public class JtlZahlungsart
    {
        [Key]
        [Column("kZahlungsart")]
        public int KZahlungsart { get; set; }

        [Column("cName")]
        public string? CName { get; set; }

        [Column("nAktiv")]
        public bool NAktiv { get; set; }

        [Column("nZahlungsziel")]
        public int? NZahlungsziel { get; set; }
    }

    /// <summary>
    /// JTL Tabelle: tKundengruppe
    /// </summary>
    [Table("tKundengruppe")]
    public class JtlKundengruppe
    {
        [Key]
        [Column("kKundengruppe")]
        public int KKundengruppe { get; set; }

        [Column("cName")]
        public string? CName { get; set; }

        [Column("fRabatt")]
        public decimal? FRabatt { get; set; }

        [Column("nStandard")]
        public bool NStandard { get; set; }
    }

    /// <summary>
    /// JTL Tabelle: tHersteller
    /// </summary>
    [Table("tHersteller")]
    public class JtlHersteller
    {
        [Key]
        [Column("kHersteller")]
        public int KHersteller { get; set; }

        [Column("cName")]
        public string? CName { get; set; }

        [Column("cHomepage")]
        public string? CHomepage { get; set; }
    }

    /// <summary>
    /// JTL Tabelle: tSteuerklasse
    /// </summary>
    [Table("tSteuerklasse")]
    public class JtlSteuerklasse
    {
        [Key]
        [Column("kSteuerklasse")]
        public int KSteuerklasse { get; set; }

        [Column("cName")]
        public string? CName { get; set; }

        [Column("fSteuersatz")]
        public decimal FSteuersatz { get; set; }
    }

    /// <summary>
    /// JTL Tabelle: tAdresse (Lieferadressen)
    /// </summary>
    [Table("tAdresse")]
    public class JtlAdresse
    {
        [Key]
        [Column("kAdresse")]
        public int KAdresse { get; set; }

        [Column("kKunde")]
        public int? KKunde { get; set; }

        [Column("cAnrede")]
        public string? CAnrede { get; set; }

        [Column("cVorname")]
        public string? CVorname { get; set; }

        [Column("cName")]
        public string? CName { get; set; }

        [Column("cFirma")]
        public string? CFirma { get; set; }

        [Column("cStrasse")]
        public string? CStrasse { get; set; }

        [Column("cPLZ")]
        public string? CPLZ { get; set; }

        [Column("cOrt")]
        public string? COrt { get; set; }

        [Column("cLand")]
        public string? CLand { get; set; }

        [Column("nStandard")]
        public bool NStandard { get; set; }
    }
}

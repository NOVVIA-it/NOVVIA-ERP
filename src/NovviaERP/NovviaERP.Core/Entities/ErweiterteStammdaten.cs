using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NovviaERP.Core.Entities
{
    #region Eigene Felder (Custom Fields)
    
    /// <summary>
    /// Definition eines eigenen Feldes für Artikel, Kunden, Aufträge etc.
    /// </summary>
    [Table("tEigenesFeld")]
    public class EigenesFeldDefinition
    {
        [Key][Column("kEigenesFeld")] public int Id { get; set; }
        
        /// <summary>Bereich: Artikel, Kunde, Auftrag, Rechnung, Lieferant, Lieferschein, RMA</summary>
        [Column("cBereich")] public string Bereich { get; set; } = "";
        
        [Column("cName")] public string Name { get; set; } = "";
        [Column("cIntName")] public string? InternerName { get; set; }
        
        /// <summary>Typ: TEXT, TEXTAREA, INT, DECIMAL, DATE, DATETIME, BOOL, SELECT, MULTISELECT</summary>
        [Column("cTyp")] public EigenesFeldTyp Typ { get; set; } = EigenesFeldTyp.Text;
        
        /// <summary>Für SELECT/MULTISELECT: Werte getrennt durch |</summary>
        [Column("cWerte")] public string? AuswahlWerte { get; set; }
        
        [Column("cStandardwert")] public string? Standardwert { get; set; }
        [Column("nPflichtfeld")] public bool IstPflichtfeld { get; set; }
        [Column("nSichtbarInListe")] public bool SichtbarInListe { get; set; }
        [Column("nSichtbarImDruck")] public bool SichtbarImDruck { get; set; }
        [Column("nSortierung")] public int Sortierung { get; set; }
        [Column("nAktiv")] public bool Aktiv { get; set; } = true;
        [Column("cValidierung")] public string? Validierung { get; set; } // Regex für Validierung
        [Column("cHinweis")] public string? Hinweis { get; set; }
    }

    public enum EigenesFeldTyp
    {
        Text = 1,
        Textarea = 2,
        Int = 3,
        Decimal = 4,
        Date = 5,
        DateTime = 6,
        Bool = 7,
        Select = 8,
        MultiSelect = 9
    }

    /// <summary>
    /// Wert eines eigenen Feldes für einen konkreten Datensatz
    /// </summary>
    [Table("tEigenesFeldWert")]
    public class EigenesFeldWert
    {
        [Key][Column("kEigenesFeldWert")] public int Id { get; set; }
        [Column("kEigenesFeld")] public int EigenesFeldId { get; set; }
        [Column("kKey")] public int KeyId { get; set; } // ArtikelId, KundeId, etc.
        [Column("cWert")] public string? Wert { get; set; }
        [Column("dGeaendert")] public DateTime Geaendert { get; set; } = DateTime.Now;
    }
    #endregion

    #region Steuer - EU/Nicht-EU/Reverse Charge
    
    [Table("tSteuerzone")]
    public class Steuerzone
    {
        [Key][Column("kSteuerzone")] public int Id { get; set; }
        [Column("cName")] public string Name { get; set; } = "";
        [Column("cTyp")] public SteuerzoneTyp Typ { get; set; }
        [Column("nStandard")] public bool IstStandard { get; set; }
    }

    public enum SteuerzoneTyp
    {
        Inland = 1,           // Deutschland
        EUMitUStID = 2,       // EU mit gültiger USt-ID → 0% (Reverse Charge)
        EUOhneUStID = 3,      // EU ohne USt-ID → deutsche MwSt
        EUPrivat = 4,         // EU Privatperson → deutsche MwSt (oder OSS)
        DrittlandExport = 5,  // Nicht-EU → 0% (steuerfreier Export)
        DrittlandImport = 6   // Import aus Nicht-EU
    }

    [Table("tSteuerKlasse")]
    public class SteuerklasseErweitert
    {
        [Key][Column("kSteuerKlasse")] public int Id { get; set; }
        [Column("cName")] public string Name { get; set; } = "";
        [Column("nStandard")] public bool IstStandard { get; set; }
        [Column("cBeschreibung")] public string? Beschreibung { get; set; }
        public virtual List<SteuersatzErweitert> Steuersaetze { get; set; } = new();
    }

    [Table("tSteuerSatz")]
    public class SteuersatzErweitert
    {
        [Key][Column("kSteuerSatz")] public int Id { get; set; }
        [Column("kSteuerKlasse")] public int SteuerklasseId { get; set; }
        [Column("kSteuerzone")] public int? SteuerzoneId { get; set; }
        [Column("cLandISO")] public string? LandISO { get; set; }
        [Column("fSteuersatz")] public decimal Satz { get; set; }
        [Column("cName")] public string? Name { get; set; }
        [Column("nPrioritaet")] public int Prioritaet { get; set; }
        [Column("dGueltigVon")] public DateTime? GueltigVon { get; set; }
        [Column("dGueltigBis")] public DateTime? GueltigBis { get; set; }
    }

    /// <summary>
    /// Steuerbefreiungsgründe für Reverse Charge, Export etc.
    /// </summary>
    [Table("tSteuerbefreiung")]
    public class Steuerbefreiung
    {
        [Key][Column("kSteuerbefreiung")] public int Id { get; set; }
        [Column("cCode")] public string Code { get; set; } = "";
        [Column("cName")] public string Name { get; set; } = "";
        [Column("cRechnungstext")] public string? Rechnungstext { get; set; }
        [Column("cRechnungstextEN")] public string? RechnungstextEN { get; set; }
        [Column("nTyp")] public SteuerbefreiungTyp Typ { get; set; }
    }

    public enum SteuerbefreiungTyp
    {
        ReverseCharge = 1,        // §13b UStG - Reverse Charge innerhalb EU
        InnergemeinschaftlicheLfg = 2, // §4 Nr. 1b UStG - steuerfreie ig. Lieferung
        Ausfuhrlieferung = 3,     // §4 Nr. 1a UStG - Ausfuhr Drittland
        Kleinunternehmer = 4,     // §19 UStG - Kleinunternehmerregelung
        Steuerfreie_Leistung = 5  // §4 andere - z.B. Heilbehandlung
    }

    /// <summary>
    /// OSS - One Stop Shop für EU-weiten Versand an Privatpersonen
    /// </summary>
    [Table("tOSS")]
    public class OSSRegistrierung
    {
        [Key][Column("kOSS")] public int Id { get; set; }
        [Column("cLandISO")] public string LandISO { get; set; } = "";
        [Column("cLandName")] public string LandName { get; set; } = "";
        [Column("fSteuersatzNormal")] public decimal SteuersatzNormal { get; set; }
        [Column("fSteuersatzErmaessigt")] public decimal SteuersatzErmaessigt { get; set; }
        [Column("fLieferschwelle")] public decimal Lieferschwelle { get; set; } = 10000; // 10.000€ EU-weit seit 1.7.2021
        [Column("nAktiv")] public bool Aktiv { get; set; } = true;
    }

    /// <summary>
    /// USt-ID Validierung (VIES)
    /// </summary>
    [Table("tUStIDPruefung")]
    public class UStIDPruefung
    {
        [Key][Column("kUStIDPruefung")] public int Id { get; set; }
        [Column("kKunde")] public int? KundeId { get; set; }
        [Column("kLieferant")] public int? LieferantId { get; set; }
        [Column("cUStID")] public string UStID { get; set; } = "";
        [Column("cLandISO")] public string LandISO { get; set; } = "";
        [Column("cFirmenname")] public string? Firmenname { get; set; }
        [Column("cAdresse")] public string? Adresse { get; set; }
        [Column("nGueltig")] public bool Gueltig { get; set; }
        [Column("dPruefung")] public DateTime Pruefung { get; set; } = DateTime.Now;
        [Column("cAntwortCode")] public string? AntwortCode { get; set; }
        [Column("cRequestID")] public string? RequestID { get; set; }
    }
    #endregion

    #region Zahlungsanbieter
    
    [Table("tZahlungsanbieter")]
    public class Zahlungsanbieter
    {
        [Key][Column("kZahlungsanbieter")] public int Id { get; set; }
        [Column("cName")] public string Name { get; set; } = "";
        [Column("cAnbieterTyp")] public ZahlungsanbieterTyp Typ { get; set; }
        [Column("cApiUrl")] public string? ApiUrl { get; set; }
        [Column("cApiKey")] public string? ApiKey { get; set; }
        [Column("cApiSecret")] public string? ApiSecret { get; set; }
        [Column("cMerchantId")] public string? MerchantId { get; set; }
        [Column("cWebhookSecret")] public string? WebhookSecret { get; set; }
        [Column("nTestmodus")] public bool Testmodus { get; set; } = true;
        [Column("nAktiv")] public bool Aktiv { get; set; } = true;
        [Column("cKonfiguration")] public string? KonfigurationJson { get; set; } // Zusätzliche Einstellungen als JSON
    }

    public enum ZahlungsanbieterTyp
    {
        PayPal = 1,
        Stripe = 2,
        Mollie = 3,
        Klarna = 4,
        AmazonPay = 5,
        ApplePay = 6,
        GooglePay = 7,
        Sofort = 8,
        Giropay = 9,
        EPS = 10,
        iDEAL = 11,
        Bancontact = 12,
        SEPA = 13,
        Kreditkarte = 14,
        Rechnung = 15,
        Vorkasse = 16,
        Nachnahme = 17,
        Ratenzahlung = 18,
        Sparkasse_HBCI = 19,
        Bank_EBICS = 20
    }

    [Table("tZahlungsanbieterMethode")]
    public class ZahlungsanbieterMethode
    {
        [Key][Column("kZahlungsanbieterMethode")] public int Id { get; set; }
        [Column("kZahlungsanbieter")] public int ZahlungsanbieterId { get; set; }
        [Column("cMethode")] public string Methode { get; set; } = ""; // z.B. "paypal", "credit_card", "sepa_direct_debit"
        [Column("cAnzeigename")] public string Anzeigename { get; set; } = "";
        [Column("cBildUrl")] public string? BildUrl { get; set; }
        [Column("fMindestbetrag")] public decimal? Mindestbetrag { get; set; }
        [Column("fMaximalbetrag")] public decimal? Maximalbetrag { get; set; }
        [Column("cWaehrungen")] public string? Waehrungen { get; set; } // z.B. "EUR,USD,GBP"
        [Column("cLaender")] public string? Laender { get; set; } // z.B. "DE,AT,CH"
        [Column("fGebuehrFix")] public decimal GebuehrFix { get; set; }
        [Column("fGebuehrProzent")] public decimal GebuehrProzent { get; set; }
        [Column("nSortierung")] public int Sortierung { get; set; }
        [Column("nAktiv")] public bool Aktiv { get; set; } = true;
    }

    [Table("tZahlungsanbieterTransaktion")]
    public class ZahlungsanbieterTransaktion
    {
        [Key][Column("kZahlungsanbieterTransaktion")] public int Id { get; set; }
        [Column("kZahlungsanbieter")] public int ZahlungsanbieterId { get; set; }
        [Column("kBestellung")] public int? BestellungId { get; set; }
        [Column("kRechnung")] public int? RechnungId { get; set; }
        [Column("cTransaktionsId")] public string TransaktionsId { get; set; } = "";
        [Column("cReferenz")] public string? Referenz { get; set; }
        [Column("cStatus")] public ZahlungstransaktionStatus Status { get; set; }
        [Column("fBetrag")] public decimal Betrag { get; set; }
        [Column("cWaehrung")] public string Waehrung { get; set; } = "EUR";
        [Column("fGebuehr")] public decimal Gebuehr { get; set; }
        [Column("cMethode")] public string? Methode { get; set; }
        [Column("cPayerEmail")] public string? PayerEmail { get; set; }
        [Column("cPayerId")] public string? PayerId { get; set; }
        [Column("dErstellt")] public DateTime Erstellt { get; set; } = DateTime.Now;
        [Column("dAbgeschlossen")] public DateTime? Abgeschlossen { get; set; }
        [Column("cRawResponse")] public string? RawResponse { get; set; } // JSON der API-Antwort
    }

    public enum ZahlungstransaktionStatus
    {
        Ausstehend = 0,
        Autorisiert = 1,
        Bezahlt = 2,
        Teilbezahlt = 3,
        Storniert = 4,
        Fehlgeschlagen = 5,
        Erstattet = 6,
        TeilErstattet = 7,
        InPruefung = 8
    }

    [Table("tBankverbindung")]
    public class Bankverbindung
    {
        [Key][Column("kBankverbindung")] public int Id { get; set; }
        [Column("kFirma")] public int FirmaId { get; set; }
        [Column("cName")] public string Name { get; set; } = "";
        [Column("cBank")] public string Bank { get; set; } = "";
        [Column("cIBAN")] public string IBAN { get; set; } = "";
        [Column("cBIC")] public string? BIC { get; set; }
        [Column("cKontoinhaber")] public string? Kontoinhaber { get; set; }
        [Column("cBLZ")] public string? BLZ { get; set; }
        [Column("cKontonummer")] public string? Kontonummer { get; set; }
        [Column("nStandard")] public bool IstStandard { get; set; }
        [Column("nAktiv")] public bool Aktiv { get; set; } = true;
        // HBCI/FinTS Zugangsdaten (verschlüsselt speichern!)
        [Column("cHBCIUrl")] public string? HBCIUrl { get; set; }
        [Column("cHBCIBenutzer")] public string? HBCIBenutzer { get; set; }
        [Column("cHBCIPIN")] public string? HBCIPINEncrypted { get; set; }
        [Column("nHBCIVersion")] public int? HBCIVersion { get; set; }
    }
    #endregion

    #region Erweiterte Kundenfelder für Steuern
    
    /// <summary>
    /// Steuereinstellungen pro Kunde
    /// </summary>
    [Table("tKundeSteuer")]
    public class KundeSteuereinstellung
    {
        [Key][Column("kKundeSteuer")] public int Id { get; set; }
        [Column("kKunde")] public int KundeId { get; set; }
        [Column("kSteuerzone")] public int? SteuerzoneId { get; set; }
        [Column("cUStID")] public string? UStID { get; set; }
        [Column("nUStIDGeprueft")] public bool UStIDGeprueft { get; set; }
        [Column("dUStIDPruefung")] public DateTime? UStIDPruefungDatum { get; set; }
        [Column("nReverseCharge")] public bool ReverseCharge { get; set; }
        [Column("nSteuerbefreit")] public bool Steuerbefreit { get; set; }
        [Column("kSteuerbefreiung")] public int? SteuerbefreiungId { get; set; }
        [Column("cSteuerbefreiungGrund")] public string? SteuerbefreiungGrund { get; set; }
        [Column("nKleinunternehmer")] public bool Kleinunternehmer { get; set; }
    }
    #endregion
}

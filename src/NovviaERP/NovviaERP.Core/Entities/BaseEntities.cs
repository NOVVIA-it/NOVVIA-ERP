using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NovviaERP.Core.Entities
{
    #region Artikel
    [Table("tArtikel")]
    public class Artikel
    {
        [Key][Column("kArtikel")] public int Id { get; set; }
        [Column("cArtNr")] public string ArtNr { get; set; } = "";
        [Column("cName")] public string? Name { get; set; }
        [NotMapped] public ArtikelBeschreibung? Beschreibung { get; set; }
        [Column("cBarcode")] public string? Barcode { get; set; }
        [Column("fVKNetto")] public decimal VKNetto { get; set; }
        [Column("fVKBrutto")] public decimal VKBrutto { get; set; }
        [Column("fEKNetto")] public decimal EKNetto { get; set; }
        [Column("fGewicht")] public decimal? Gewicht { get; set; }
        [Column("nLagerbestand")] public decimal Lagerbestand { get; set; }
        [Column("nMindestbestand")] public decimal? Mindestbestand { get; set; }
        [Column("cAktiv")] public string Aktiv { get; set; } = "Y";
        [Column("kHersteller")] public int? HerstellerId { get; set; }
        [Column("kSteuerklasse")] public int? SteuerklasseId { get; set; }
        [Column("kWarengruppe")] public int? WarengruppeId { get; set; }
        [Column("dErstellt")] public DateTime? Erstellt { get; set; }
        [Column("dGeaendert")] public DateTime? Geaendert { get; set; }
        [Column("cHAN")] public string? HerstellerArtNr { get; set; }
        [Column("nMHD")] public bool HatMHD { get; set; }
        [Column("nCharge")] public bool HatCharge { get; set; }
        [Column("cSerie")] public string? Seriennummer { get; set; }

        [NotMapped] public string? HerstellerName { get; set; }
        [NotMapped] public List<ArtikelBeschreibung>? Beschreibungen { get; set; }
        [NotMapped] public List<ArtikelMerkmal>? Merkmale { get; set; }
        [NotMapped] public List<ArtikelAttribut>? Attribute { get; set; }
        [NotMapped] public List<ArtikelPreis>? Preise { get; set; }
        [NotMapped] public List<ArtikelStaffelpreis>? Staffelpreise { get; set; }
        [NotMapped] public List<ArtikelBild>? Bilder { get; set; }
        [NotMapped] public List<ArtikelKategorie>? Kategorien { get; set; }
        [NotMapped] public List<ArtikelLieferant>? Lieferanten { get; set; }
        [NotMapped] public bool IstStueckliste { get; set; }
        [NotMapped] public List<Stueckliste>? Stuecklistenkomponenten { get; set; }
        [NotMapped] public List<Lagerbestand>? Lagerbestaende { get; set; }
        [NotMapped] public List<ArtikelWooCommerce>? WooCommerceLinks { get; set; }
    }

    public class ArtikelBeschreibung
    {
        public int Id { get; set; }
        public int ArtikelId { get; set; }
        public int SpracheId { get; set; } = 1;
        public string Sprache { get; set; } = "DE";
        public string? Name { get; set; }
        public string? Kurzbeschreibung { get; set; }
        public string? Langbeschreibung { get; set; }
        // Aliasse für WooCommerceService
        public string? KurzBeschreibung { get => Kurzbeschreibung; set => Kurzbeschreibung = value; }
        public string? Beschreibung { get => Langbeschreibung; set => Langbeschreibung = value; }
    }

    public class ArtikelMerkmal
    {
        public int Id { get; set; }
        public int ArtikelId { get; set; }
        public int MerkmalId { get; set; }
        public string? MerkmalName { get; set; }
        public string? Wert { get; set; }
        public string? WertName { get => Wert; set => Wert = value; }
        public int Sortierung { get; set; }
    }

    public class ArtikelAttribut
    {
        public int Id { get; set; }
        public int ArtikelId { get; set; }
        public string Name { get; set; } = "";
        public string? Wert { get; set; }
        public int Sortierung { get; set; }
    }

    public class ArtikelPreis
    {
        public int Id { get; set; }
        public int ArtikelId { get; set; }
        public int KundengruppeId { get; set; }
        public decimal PreisNetto { get; set; }
        public decimal PreisBrutto { get; set; }
        public DateTime? GueltigAb { get; set; }
        public DateTime? GueltigBis { get; set; }
    }

    public class ArtikelStaffelpreis
    {
        public int Id { get; set; }
        public int ArtikelId { get; set; }
        public int KundengruppeId { get; set; }
        public decimal AbMenge { get; set; }
        public decimal PreisNetto { get; set; }
        public decimal PreisBrutto { get; set; }
    }

    public class ArtikelBild
    {
        public int Id { get; set; }
        public int ArtikelId { get; set; }
        public string Pfad { get; set; } = "";
        public string? AltText { get; set; }
        public int Sortierung { get; set; }
        public int Nummer { get => Sortierung; set => Sortierung = value; }
        public bool IstHauptbild { get; set; }
    }

    public class ArtikelKategorie
    {
        public int Id { get; set; }
        public int ArtikelId { get; set; }
        public int KategorieId { get; set; }
        public string? KategorieName { get; set; }
        public string? KategoriePfad { get; set; }
    }

    public class ArtikelLieferant
    {
        public int Id { get; set; }
        public int ArtikelId { get; set; }
        public int LieferantId { get; set; }
        public string? LieferantName { get; set; }
        public string? LieferantenArtNr { get; set; }
        public decimal? EKNetto { get; set; }
        public int? Lieferzeit { get; set; }
        public bool IstStandard { get; set; }
    }

    public class ArtikelSonderpreis
    {
        public int Id { get; set; }
        public int ArtikelId { get; set; }
        public int? KundeId { get; set; }
        public int? KundengruppeId { get; set; }
        public decimal PreisNetto { get; set; }
        public DateTime? GueltigAb { get; set; }
        public DateTime? GueltigBis { get; set; }
    }

    public class Stueckliste
    {
        public int Id { get; set; }
        public int ArtikelId { get; set; }
        public int KomponenteArtikelId { get; set; }
        public decimal Menge { get; set; }
        public int Sortierung { get; set; }
        [NotMapped] public string? KomponenteArtNr { get; set; }
        [NotMapped] public string? KomponenteName { get; set; }
    }

    public class ArtikelLagerbestand
    {
        public int Id { get; set; }
        public int ArtikelId { get; set; }
        public int WarenlagerId { get; set; }
        public decimal Bestand { get; set; }
        public decimal Reserviert { get; set; }
        public decimal Verfuegbar => Bestand - Reserviert;
        [NotMapped] public string? WarenlagerName { get; set; }
        [NotMapped] public string? LagerplatzName { get; set; }
    }

    public class ArtikelWooCommerce
    {
        public int Id { get; set; }
        public int ArtikelId { get; set; }
        public int ShopId { get; set; }
        public int WooCommerceProductId { get; set; }
        public DateTime? LetzterSync { get; set; }
        [NotMapped] public string? ShopName { get; set; }
    }
    #endregion

    #region Kunde
    [Table("tkunde")]
    public class Kunde
    {
        [Key][Column("kKunde")] public int Id { get; set; }
        [Column("cKundenNr")] public string? KundenNr { get; set; }
        [Column("cFirma")] public string? Firma { get; set; }
        [Column("cAnrede")] public string? Anrede { get; set; }
        [Column("cVorname")] public string? Vorname { get; set; }
        [Column("cName")] public string? Nachname { get; set; }
        [Column("cStrasse")] public string? Strasse { get; set; }
        [Column("cPLZ")] public string? PLZ { get; set; }
        [Column("cOrt")] public string? Ort { get; set; }
        [Column("cLand")] public string? Land { get; set; }
        [Column("cTel")] public string? Telefon { get; set; }
        [Column("cMobil")] public string? Mobil { get; set; }
        [Column("cMail")] public string? Email { get; set; }
        [Column("cFax")] public string? Fax { get; set; }
        [Column("cUSTID")] public string? UStId { get; set; }
        [Column("kKundenGruppe")] public int? KundengruppeId { get; set; }
        [Column("fRabatt")] public decimal Rabatt { get; set; }
        [Column("nZahlungsziel")] public int? Zahlungsziel { get; set; }
        [Column("kZahlungsart")] public int? ZahlungsartId { get; set; }
        [Column("dErstellt")] public DateTime? Erstellt { get; set; }
        [Column("cSperre")] public string? Sperre { get; set; }
        [Column("cAktiv")] public string Aktiv { get; set; } = "Y";

        [NotMapped] public string? KundengruppeName { get; set; }
        [NotMapped] public string VollerName => $"{Vorname} {Nachname}".Trim();
        [NotMapped] public List<KundeAdresse>? Adressen { get; set; }
        [NotMapped] public List<Bestellung>? Bestellungen { get; set; }
    }

    public class KundeAdresse
    {
        public int Id { get; set; }
        public int KundeId { get; set; }
        public string? Firma { get; set; }
        public string? Anrede { get; set; }
        public string? Vorname { get; set; }
        public string? Nachname { get; set; }
        public string? Strasse { get; set; }
        public string? PLZ { get; set; }
        public string? Ort { get; set; }
        public string? Land { get; set; }
        public int Typ { get; set; } // 1=Rechnung, 2=Lieferung
        public bool IstStandard { get; set; }
    }
    #endregion

    #region Bestellung
    public enum BestellStatus
    {
        Offen = 1,
        InBearbeitung = 2,
        Versendet = 3,
        Abgeschlossen = 4,
        Storniert = 5,
        Bezahlt = 6
    }

    [Table("tBestellung")]
    public class Bestellung
    {
        [Key][Column("kBestellung")] public int Id { get; set; }
        [Column("cBestellNr")] public string BestellNr { get; set; } = "";
        [Column("cExterneBestellNr")] public string? ExterneBestellNr { get; set; }
        [Column("tKunde_kKunde")] public int KundeId { get; set; }
        [Column("dErstellt")] public DateTime Erstellt { get; set; } = DateTime.Now;
        [Column("dGeliefert")] public DateTime? Geliefert { get; set; }
        [Column("cStatus")] public string? StatusText { get; set; }
        [Column("nStatus")] public int Status { get; set; } = 1;
        [Column("fGesamtNetto")] public decimal GesamtNetto { get; set; }
        [Column("fGesamtBrutto")] public decimal GesamtBrutto { get; set; }
        [Column("fVersandkosten")] public decimal Versandkosten { get; set; }
        [Column("fRabatt")] public decimal Rabatt { get; set; }
        [Column("kZahlungsart")] public int? ZahlungsartId { get; set; }
        [Column("kVersandart")] public int? VersandartId { get; set; }
        [Column("kLieferadresse")] public int? LieferadresseId { get; set; }
        [Column("kRechnungsadresse")] public int? RechnungsadresseId { get; set; }
        [Column("cAnmerkung")] public string? Anmerkung { get; set; }
        [Column("cInterneNotiz")] public string? InterneNotiz { get; set; }
        [Column("dBezahlt")] public DateTime? Bezahlt { get; set; }
        [Column("nStorno")] public bool IstStorniert { get; set; }
        [Column("cWaehrung")] public string Waehrung { get; set; } = "EUR";
        [Column("kPlattform")] public int? PlattformId { get; set; }

        [NotMapped] public Kunde? Kunde { get; set; }
        [NotMapped] public List<BestellPosition>? Positionen { get; set; }
        [NotMapped] public BestellAdresse? Lieferadresse { get; set; }
        [NotMapped] public BestellAdresse? Rechnungsadresse { get; set; }
        [NotMapped] public string? ZahlungsartName { get; set; }
        [NotMapped] public string? VersandartName { get; set; }
        [NotMapped] public string? PlattformName { get; set; }
        [NotMapped] public string? VersandDienstleister { get; set; }
        [NotMapped] public string? TrackingNr { get; set; }
        [NotMapped] public List<Rechnung>? Rechnungen { get; set; }
        [NotMapped] public List<Lieferschein>? Lieferscheine { get; set; }
        // Aliasse für WooCommerceService
        [NotMapped] public string? ExterneAuftragsnummer { get => ExterneBestellNr; set => ExterneBestellNr = value; }
        [NotMapped] public int? Platform { get => PlattformId; set => PlattformId = value; }
        [NotMapped] public string? Kommentar { get => Anmerkung; set => Anmerkung = value; }
        [NotMapped] public string? InternerKommentar { get => InterneNotiz; set => InterneNotiz = value; }
    }

    [Table("tBestellpos")]
    public class BestellPosition
    {
        [Key][Column("kBestellpos")] public int Id { get; set; }
        [Column("kBestellung")] public int BestellungId { get; set; }
        [Column("kArtikel")] public int? ArtikelId { get; set; }
        [Column("cArtNr")] public string? ArtNr { get; set; }
        [Column("cName")] public string? Name { get; set; }
        [Column("fAnzahl")] public decimal Menge { get; set; }
        [Column("fVKNetto")] public decimal PreisNetto { get; set; }
        [Column("fVKBrutto")] public decimal PreisBrutto { get; set; }
        [Column("fRabatt")] public decimal Rabatt { get; set; }
        [Column("fMwSt")] public decimal MwStSatz { get; set; }
        [Column("nPosTyp")] public int PosTyp { get; set; }
        [Column("cChargenNr")] public string? ChargenNr { get; set; }
        [Column("dMHD")] public DateTime? MHD { get; set; }

        [NotMapped] public decimal GesamtNetto => Menge * PreisNetto * (1 - Rabatt / 100);
        [NotMapped] public decimal GesamtBrutto => Menge * PreisBrutto * (1 - Rabatt / 100);
        // Aliasse für WooCommerceService
        [NotMapped] public decimal VKNetto { get => PreisNetto; set => PreisNetto = value; }
        [NotMapped] public decimal VKBrutto { get => PreisBrutto; set => PreisBrutto = value; }
        [NotMapped] public decimal MwSt { get => MwStSatz; set => MwStSatz = value; }
        [NotMapped] public decimal Geliefert { get; set; }
    }

    [Table("tBestelladresse")]
    public class BestellAdresse
    {
        [Key][Column("kBestelladresse")] public int Id { get; set; }
        [Column("kBestellung")] public int? BestellungId { get; set; }
        [Column("cFirma")] public string? Firma { get; set; }
        [Column("cAnrede")] public string? Anrede { get; set; }
        [Column("cVorname")] public string? Vorname { get; set; }
        [Column("cName")] public string? Nachname { get; set; }
        [Column("cStrasse")] public string? Strasse { get; set; }
        [Column("cAdressZusatz")] public string? AdressZusatz { get; set; }
        [Column("cPLZ")] public string? PLZ { get; set; }
        [Column("cOrt")] public string? Ort { get; set; }
        [Column("cLand")] public string? Land { get; set; }
        [Column("cISO")] public string? LandISO { get; set; }
        [Column("cTel")] public string? Telefon { get; set; }
        [Column("cMail")] public string? Email { get; set; }
        [Column("nTyp")] public int Typ { get; set; } // 1=Rechnung, 2=Lieferung
    }
    #endregion

    #region Rechnung
    public enum RechnungTyp
    {
        Rechnung = 1,
        Gutschrift = 2,
        Stornorechnung = 3
    }

    [Table("tRechnung")]
    public class Rechnung
    {
        [Key][Column("kRechnung")] public int Id { get; set; }
        [Column("cRechnungsNr")] public string RechnungsNr { get; set; } = "";
        [Column("kBestellung")] public int? BestellungId { get; set; }
        [Column("kKunde")] public int KundeId { get; set; }
        [Column("dErstellt")] public DateTime Erstellt { get; set; } = DateTime.Now;
        [Column("dFaellig")] public DateTime? Faellig { get; set; }
        [Column("dBezahlt")] public DateTime? Bezahlt { get; set; }
        [Column("fNetto")] public decimal Netto { get; set; }
        [Column("fBrutto")] public decimal Brutto { get; set; }
        [Column("fMwSt")] public decimal MwSt { get; set; }
        [Column("nStatus")] public int Status { get; set; }
        [Column("nTyp")] public RechnungTyp Typ { get; set; } = RechnungTyp.Rechnung;
        [Column("nStorno")] public bool IstStorniert { get; set; }
        [Column("cWaehrung")] public string Waehrung { get; set; } = "EUR";

        [NotMapped] public Kunde? Kunde { get; set; }
        [NotMapped] public List<RechnungsPosition>? Positionen { get; set; }
        [NotMapped] public decimal OffenerBetrag => Bezahlt.HasValue ? 0 : Brutto;
        [NotMapped] public decimal Offen { get => OffenerBetrag; }
        [NotMapped] public List<Zahlungseingang>? Zahlungen { get; set; }
    }

    public class Gutschrift
    {
        public int Id { get; set; }
        public string GutschriftNr { get; set; } = "";
        public int? RechnungId { get; set; }
        public int KundeId { get; set; }
        public DateTime Erstellt { get; set; } = DateTime.Now;
        public decimal Netto { get; set; }
        public decimal Brutto { get; set; }
        public string? Grund { get; set; }
        public int Status { get; set; }
    }
    #endregion

    #region Einkauf
    [Table("tLieferantenbestellung")]
    public class EinkaufsBestellung
    {
        [Key][Column("kLieferantenbestellung")] public int Id { get; set; }
        [Column("cBestellNr")] public string? BestellNr { get; set; }
        [Column("kLieferant")] public int LieferantId { get; set; }
        [Column("dErstellt")] public DateTime Erstellt { get; set; } = DateTime.Now;
        [Column("dLiefertermin")] public DateTime? Liefertermin { get; set; }
        [Column("nStatus")] public int Status { get; set; }
        [Column("fSummeNetto")] public decimal SummeNetto { get; set; }
        [Column("cAnmerkung")] public string? Anmerkung { get; set; }

        [NotMapped] public string? LieferantName { get; set; }
        [NotMapped] public List<EinkaufsBestellPosition>? Positionen { get; set; }
    }

    public class EinkaufsBestellPosition
    {
        public int Id { get; set; }
        public int BestellungId { get; set; }
        public int EinkaufsbestellungId { get => BestellungId; set => BestellungId = value; }
        public int ArtikelId { get; set; }
        public string? ArtNr { get; set; }
        public string? Name { get; set; }
        public decimal Menge { get; set; }
        public decimal MengeGeliefert { get; set; }
        public decimal EKNetto { get; set; }
    }

    public class Wareneingang
    {
        public int Id { get; set; }
        public string? WareneingangNr { get; set; }
        public int EinkaufsBestellungId { get; set; }
        public int WarenlagerId { get; set; }
        public DateTime Datum { get; set; } = DateTime.Now;
        public int BearbeiterId { get; set; }
        public string? Bemerkung { get; set; }
        public List<WareneingangPosition>? Positionen { get; set; }
        [NotMapped] public Benutzer? Benutzer { get; set; }
    }

    public class WareneingangPosition
    {
        public int Id { get; set; }
        public int WareneingangId { get; set; }
        public int ArtikelId { get; set; }
        public decimal Menge { get; set; }
        public string? ChargenNr { get; set; }
        public DateTime? MHD { get; set; }
        public int? LagerplatzId { get; set; }
    }
    #endregion

    #region RMA / Retouren
    public enum ArtikelZustand
    {
        Neu = 1,
        WieNeu = 2,
        Neuwertig = 2,
        Gut = 3,
        Akzeptabel = 4,
        Defekt = 5,
        Unbrauchbar = 6
    }

    public enum AdressTyp
    {
        Rechnung = 1,
        Rechnungsadresse = 1,
        Lieferung = 2,
        Lieferadresse = 2,
        Alternativ = 3
    }

    public class RMA
    {
        public int Id { get; set; }
        public string RMANr { get; set; } = "";
        public int BestellungId { get; set; }
        public int KundeId { get; set; }
        public DateTime Erstellt { get; set; } = DateTime.Now;
        public DateTime? Abgeschlossen { get; set; }
        public int Status { get; set; }
        public string? Grund { get; set; }
        public string? Bemerkung { get; set; }
        public List<RMAPosition>? Positionen { get; set; }

        [NotMapped] public string? KundeName { get; set; }
        [NotMapped] public string? BestellNr { get; set; }
    }

    public class RMAPosition
    {
        public int Id { get; set; }
        public int RMAId { get; set; }
        public int ArtikelId { get; set; }
        public decimal Menge { get; set; }
        public string? Grund { get; set; }
        public ArtikelZustand Zustand { get; set; }
        public string? Aktion { get; set; }
    }
    #endregion

    #region WooCommerce
    public class WooCommerceShop
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Url { get; set; } = "";
        public string ConsumerKey { get; set; } = "";
        public string ConsumerSecret { get; set; } = "";
        public bool Aktiv { get; set; } = true;
        public DateTime? LetzterSync { get; set; }
        public int? StandardWarenlagerId { get; set; }
        public int? StandardZahlungsartId { get; set; }
        public int? StandardVersandartId { get; set; }
    }
    #endregion

    // Kategorie, Hersteller, Zahlungsart, Versandart, Plattform are defined in Stammdaten.cs
}

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NovviaERP.Core.Entities
{
    #region Firma / Mandant
    [Table("tFirma")]
    public class Firma
    {
        [Key][Column("kFirma")] public int Id { get; set; }
        [Column("cName")] public string Name { get; set; } = "";
        [Column("cStrasse")] public string? Strasse { get; set; }
        [Column("cPLZ")] public string? PLZ { get; set; }
        [Column("cOrt")] public string? Ort { get; set; }
        [Column("cLand")] public string? Land { get; set; }
        [Column("cTel")] public string? Telefon { get; set; }
        [Column("cFax")] public string? Fax { get; set; }
        [Column("cMail")] public string? Email { get; set; }
        [Column("cWWW")] public string? Website { get; set; }
        [Column("cUStID")] public string? UStID { get; set; }
        [Column("cSteuerNr")] public string? SteuerNr { get; set; }
        [Column("cIBAN")] public string? IBAN { get; set; }
        [Column("cBIC")] public string? BIC { get; set; }
        [Column("cBank")] public string? Bank { get; set; }
        [Column("cGeschaeftsfuehrer")] public string? Geschaeftsfuehrer { get; set; }
        [Column("cAmtsgericht")] public string? Amtsgericht { get; set; }
        [Column("cHandelsregister")] public string? Handelsregister { get; set; }
    }
    #endregion

    #region Warenlager
    [Table("tWarenLager")]
    public class Warenlager
    {
        [Key][Column("kWarenLager")] public int Id { get; set; }
        [Column("cName")] public string Name { get; set; } = "";
        [Column("cKuerzel")] public string? Kuerzel { get; set; }
        [Column("cBeschreibung")] public string? Beschreibung { get; set; }
        [Column("cStrasse")] public string? Strasse { get; set; }
        [Column("cPLZ")] public string? PLZ { get; set; }
        [Column("cOrt")] public string? Ort { get; set; }
        [Column("cLand")] public string? Land { get; set; }
        [Column("nStandard")] public bool IstStandard { get; set; }
        [Column("nAktiv")] public bool Aktiv { get; set; } = true;
        [Column("nFulfillment")] public bool IstFulfillment { get; set; }
        [Column("kLieferant")] public int? LieferantId { get; set; }
    }

    [Table("tWarenLagerPlatz")]
    public class Lagerplatz
    {
        [Key][Column("kWarenLagerPlatz")] public int Id { get; set; }
        [Column("kWarenLager")] public int WarenlagerId { get; set; }
        [Column("cName")] public string Name { get; set; } = "";
        [Column("cRegal")] public string? Regal { get; set; }
        [Column("cFach")] public string? Fach { get; set; }
        [Column("cEbene")] public string? Ebene { get; set; }
        [Column("cBarcode")] public string? Barcode { get; set; }
        [Column("nAktiv")] public bool Aktiv { get; set; } = true;
        [Column("nSortierung")] public int Sortierung { get; set; }
    }
    #endregion

    #region Kategorien
    [Table("tKategorie")]
    public class Kategorie
    {
        [Key][Column("kKategorie")] public int Id { get; set; }
        [Column("kOberKategorie")] public int? OberKategorieId { get; set; }
        [Column("nSort")] public int Sortierung { get; set; }
        [Column("nEbene")] public int Ebene { get; set; }
        [Column("cSeo")] public string? SeoUrl { get; set; }
        [Column("nAktiv")] public bool Aktiv { get; set; } = true;
        public virtual KategorieBeschreibung? Beschreibung { get; set; }
        public virtual List<Kategorie> Unterkategorien { get; set; } = new();
    }

    [Table("tKategorieSprache")]
    public class KategorieBeschreibung
    {
        [Key][Column("kKategorieSprache")] public int Id { get; set; }
        [Column("kKategorie")] public int KategorieId { get; set; }
        [Column("kSprache")] public int SpracheId { get; set; } = 1;
        [Column("cName")] public string Name { get; set; } = "";
        [Column("cBeschreibung")] public string? Beschreibung { get; set; }
        [Column("cMetaTitle")] public string? MetaTitle { get; set; }
        [Column("cMetaDescription")] public string? MetaDescription { get; set; }
        [Column("cMetaKeywords")] public string? MetaKeywords { get; set; }
    }
    #endregion

    #region Kundengruppen
    [Table("tKundenGruppe")]
    public class Kundengruppe
    {
        [Key][Column("kKundenGruppe")] public int Id { get; set; }
        [Column("cName")] public string Name { get; set; } = "";
        [Column("fRabatt")] public decimal Rabatt { get; set; }
        [Column("cStandard")] public string IstStandard { get; set; } = "N";
        [Column("nNettoPreise")] public bool NettoPreise { get; set; }
    }

    [Table("tKundenKategorie")]
    public class Kundenkategorie
    {
        [Key][Column("kKundenKategorie")] public int Id { get; set; }
        [Column("cName")] public string Name { get; set; } = "";
        [Column("cBeschreibung")] public string? Beschreibung { get; set; }
        [Column("cFarbe")] public string? Farbe { get; set; }
    }
    #endregion

    #region Versandarten
    [Table("tVersandArt")]
    public class Versandart
    {
        [Key][Column("kVersandArt")] public int Id { get; set; }
        [Column("cName")] public string Name { get; set; } = "";
        [Column("cAnbieter")] public string? Anbieter { get; set; }
        [Column("cLieferzeit")] public string? Lieferzeit { get; set; }
        [Column("fPreis")] public decimal Preis { get; set; }
        [Column("fVersandkostenfreiAb")] public decimal? VersandkostenfreiAb { get; set; }
        [Column("nSort")] public int Sortierung { get; set; }
        [Column("nAktiv")] public bool Aktiv { get; set; } = true;
        [Column("cTrackingURL")] public string? TrackingUrl { get; set; }
    }

    [Table("tVersandDienstleister")]
    public class Versanddienstleister
    {
        [Key][Column("kVersandDienstleister")] public int Id { get; set; }
        [Column("cName")] public string Name { get; set; } = "";
        [Column("cKuerzel")] public string? Kuerzel { get; set; }
        [Column("cTrackingURL")] public string? TrackingUrl { get; set; }
        [Column("nAktiv")] public bool Aktiv { get; set; } = true;
    }
    #endregion

    #region Zahlungsarten
    [Table("tZahlungsArt")]
    public class Zahlungsart
    {
        [Key][Column("kZahlungsArt")] public int Id { get; set; }
        [Column("cName")] public string Name { get; set; } = "";
        [Column("cModulId")] public string? ModulId { get; set; }
        [Column("cKundenName")] public string? KundenName { get; set; }
        [Column("nSort")] public int Sortierung { get; set; }
        [Column("nAktiv")] public bool Aktiv { get; set; } = true;
        [Column("fAufpreis")] public decimal Aufpreis { get; set; }
        [Column("fProzent")] public decimal AufpreisProzent { get; set; }
        [Column("nNachnahme")] public bool IstNachnahme { get; set; }
        [Column("nZahlungszielTage")] public int? ZahlungszielTage { get; set; }
        [Column("nSkontoTage")] public int? SkontoTage { get; set; }
        [Column("fSkontoProzent")] public decimal? SkontoProzent { get; set; }
    }
    #endregion

    #region Hersteller
    [Table("tHersteller")]
    public class Hersteller
    {
        [Key][Column("kHersteller")] public int Id { get; set; }
        [Column("cName")] public string Name { get; set; } = "";
        [Column("cSeo")] public string? SeoUrl { get; set; }
        [Column("cBildPfad")] public string? LogoPfad { get; set; }
        [Column("cHomepage")] public string? Homepage { get; set; }
        [Column("nSort")] public int Sortierung { get; set; }
        [Column("cBeschreibung")] public string? Beschreibung { get; set; }
    }
    #endregion

    #region Lieferanten
    [Table("tLieferant")]
    public class Lieferant
    {
        [Key][Column("kLieferant")] public int Id { get; set; }
        [Column("cFirma")] public string Firma { get; set; } = "";
        [Column("cAnsprechpartner")] public string? Ansprechpartner { get; set; }
        [Column("cStrasse")] public string? Strasse { get; set; }
        [Column("cPLZ")] public string? PLZ { get; set; }
        [Column("cOrt")] public string? Ort { get; set; }
        [Column("cLand")] public string? Land { get; set; }
        [Column("cTel")] public string? Telefon { get; set; }
        [Column("cFax")] public string? Fax { get; set; }
        [Column("cMail")] public string? Email { get; set; }
        [Column("cWWW")] public string? Website { get; set; }
        [Column("cUStID")] public string? UStID { get; set; }
        [Column("cIBAN")] public string? IBAN { get; set; }
        [Column("cBIC")] public string? BIC { get; set; }
        [Column("cBank")] public string? Bank { get; set; }
        [Column("cKundennummer")] public string? Kundennummer { get; set; }
        [Column("nStandard")] public bool IstStandard { get; set; }
        [Column("nAktiv")] public bool Aktiv { get; set; } = true;
        [Column("fRabatt")] public decimal Rabatt { get; set; }
        [Column("nLieferzeitTage")] public int? LieferzeitTage { get; set; }
        [Column("fMindestbestellwert")] public decimal? Mindestbestellwert { get; set; }
        [Column("nZahlungszielTage")] public int? ZahlungszielTage { get; set; }
    }

    [Table("tLiefArtikel")]
    public class LieferantArtikel
    {
        [Key][Column("kLiefArtikel")] public int Id { get; set; }
        [Column("kLieferant")] public int LieferantId { get; set; }
        [Column("kArtikel")] public int ArtikelId { get; set; }
        [Column("cArtNr")] public string? LieferantArtNr { get; set; }
        [Column("cName")] public string? LieferantName { get; set; }
        [Column("fEKNetto")] public decimal EKNetto { get; set; }
        [Column("fStaffel1")] public decimal? Staffel1 { get; set; }
        [Column("fStaffel2")] public decimal? Staffel2 { get; set; }
        [Column("fStaffel3")] public decimal? Staffel3 { get; set; }
        [Column("nStaffelMenge1")] public int? StaffelMenge1 { get; set; }
        [Column("nStaffelMenge2")] public int? StaffelMenge2 { get; set; }
        [Column("nStaffelMenge3")] public int? StaffelMenge3 { get; set; }
        [Column("nStandard")] public bool IstStandard { get; set; }
        [Column("nLieferzeit")] public int? Lieferzeit { get; set; }
        [Column("fVPE")] public decimal? VPE { get; set; }
    }
    #endregion

    #region Merkmale / Attribute / Eigenschaften
    [Table("tMerkmal")]
    public class Merkmal
    {
        [Key][Column("kMerkmal")] public int Id { get; set; }
        [Column("cName")] public string Name { get; set; } = "";
        [Column("cTyp")] public string Typ { get; set; } = "TEXT";
        [Column("nSort")] public int Sortierung { get; set; }
        [Column("nGlobal")] public bool IstGlobal { get; set; }
        [Column("nMehrfachauswahl")] public bool Mehrfachauswahl { get; set; }
        public virtual List<MerkmalWert> Werte { get; set; } = new();
    }

    [Table("tMerkmalWert")]
    public class MerkmalWert
    {
        [Key][Column("kMerkmalWert")] public int Id { get; set; }
        [Column("kMerkmal")] public int MerkmalId { get; set; }
        [Column("cWert")] public string Wert { get; set; } = "";
        [Column("nSort")] public int Sortierung { get; set; }
        [Column("cSeo")] public string? SeoUrl { get; set; }
        [Column("cBildPfad")] public string? BildPfad { get; set; }
    }

    [Table("tEigenschaft")]
    public class Eigenschaft
    {
        [Key][Column("kEigenschaft")] public int Id { get; set; }
        [Column("kArtikel")] public int ArtikelId { get; set; }
        [Column("cName")] public string Name { get; set; } = "";
        [Column("cTyp")] public string Typ { get; set; } = "SELECTBOX";
        [Column("nSort")] public int Sortierung { get; set; }
        public virtual List<EigenschaftWert> Werte { get; set; } = new();
    }

    [Table("tEigenschaftWert")]
    public class EigenschaftWert
    {
        [Key][Column("kEigenschaftWert")] public int Id { get; set; }
        [Column("kEigenschaft")] public int EigenschaftId { get; set; }
        [Column("cName")] public string Name { get; set; } = "";
        [Column("fAufpreis")] public decimal Aufpreis { get; set; }
        [Column("nSort")] public int Sortierung { get; set; }
        [Column("cArtNr")] public string? ArtNr { get; set; }
    }

    [Table("tAttribut")]
    public class Attribut
    {
        [Key][Column("kAttribut")] public int Id { get; set; }
        [Column("cName")] public string Name { get; set; } = "";
        [Column("nSort")] public int Sortierung { get; set; }
        [Column("cBeschreibung")] public string? Beschreibung { get; set; }
    }
    #endregion

    #region Einheiten
    [Table("tEinheit")]
    public class Einheit
    {
        [Key][Column("kEinheit")] public int Id { get; set; }
        [Column("cName")] public string Name { get; set; } = "";
        [Column("cCode")] public string? Code { get; set; }
    }

    [Table("tMassEinheit")]
    public class Masseinheit
    {
        [Key][Column("kMassEinheit")] public int Id { get; set; }
        [Column("cName")] public string Name { get; set; } = "";
        [Column("cKuerzel")] public string? Kuerzel { get; set; }
    }
    #endregion

    #region Steuern
    [Table("tSteuerKlasse")]
    public class Steuerklasse
    {
        [Key][Column("kSteuerKlasse")] public int Id { get; set; }
        [Column("cName")] public string Name { get; set; } = "";
        [Column("nStandard")] public bool IstStandard { get; set; }
    }

    [Table("tSteuerSatz")]
    public class Steuersatz
    {
        [Key][Column("kSteuerSatz")] public int Id { get; set; }
        [Column("kSteuerKlasse")] public int SteuerklasseId { get; set; }
        [Column("kLand")] public int? LandId { get; set; }
        [Column("fSteuersatz")] public decimal Satz { get; set; }
        [Column("cName")] public string? Name { get; set; }
    }
    #endregion

    #region Mahnwesen
    [Table("tMahnstufe")]
    public class Mahnstufe
    {
        [Key][Column("kMahnstufe")] public int Id { get; set; }
        [Column("nStufe")] public int Stufe { get; set; }
        [Column("cName")] public string Name { get; set; } = "";
        [Column("nTageNachFaelligkeit")] public int TageNachFaelligkeit { get; set; }
        [Column("fGebuehr")] public decimal Gebuehr { get; set; }
        [Column("fZinsProzent")] public decimal ZinsProzent { get; set; }
        [NotMapped] public decimal Zinssatz { get => ZinsProzent; set => ZinsProzent = value; }
        [Column("cText")] public string? Text { get; set; }
        [Column("nAktiv")] public bool Aktiv { get; set; } = true;
    }

    [Table("tMahngruppe")]
    public class Mahngruppe
    {
        [Key][Column("kMahngruppe")] public int Id { get; set; }
        [Column("cName")] public string Name { get; set; } = "";
        [Column("cBeschreibung")] public string? Beschreibung { get; set; }
        [Column("nAktiv")] public bool Aktiv { get; set; } = true;
    }
    #endregion

    #region Sprachen / Währungen / Länder
    [Table("tSprache")]
    public class Sprache
    {
        [Key][Column("kSprache")] public int Id { get; set; }
        [Column("cName")] public string Name { get; set; } = "";
        [Column("cISO")] public string ISO { get; set; } = "DE";
        [Column("nStandard")] public bool IstStandard { get; set; }
    }

    [Table("tWaehrung")]
    public class Waehrung
    {
        [Key][Column("kWaehrung")] public int Id { get; set; }
        [Column("cName")] public string Name { get; set; } = "";
        [Column("cISO")] public string ISO { get; set; } = "EUR";
        [Column("cSymbol")] public string Symbol { get; set; } = "€";
        [Column("fFaktor")] public decimal Faktor { get; set; } = 1;
        [Column("nStandard")] public bool IstStandard { get; set; }
        [Column("nNachkommastellen")] public int Nachkommastellen { get; set; } = 2;
    }

    [Table("tLand")]
    public class Land
    {
        [Key][Column("kLand")] public int Id { get; set; }
        [Column("cName")] public string Name { get; set; } = "";
        [Column("cISO")] public string ISO { get; set; } = "";
        [Column("cKontinent")] public string? Kontinent { get; set; }
        [Column("nEU")] public bool IstEU { get; set; }
        [Column("nAktiv")] public bool Aktiv { get; set; } = true;
    }
    #endregion

    #region Nummernkreise
    [Table("tNummernKreis")]
    public class Nummernkreis
    {
        [Key][Column("kNummernKreis")] public int Id { get; set; }
        [Column("cName")] public string Name { get; set; } = "";
        [Column("cPrefix")] public string? Prefix { get; set; }
        [Column("cSuffix")] public string? Suffix { get; set; }
        [Column("nNummer")] public int AktuelleNummer { get; set; }
        [Column("nStellen")] public int Stellen { get; set; } = 6;
        [Column("nJahresabhaengig")] public bool Jahresabhaengig { get; set; }
    }
    #endregion

    #region Shops / Plattformen
    [Table("tShop")]
    public class Shop
    {
        [Key][Column("kShop")] public int Id { get; set; }
        [Column("cName")] public string Name { get; set; } = "";
        [Column("cURL")] public string? Url { get; set; }
        [Column("cTyp")] public string? Typ { get; set; }
        [Column("nAktiv")] public bool Aktiv { get; set; } = true;
    }

    [Table("tPlattform")]
    public class Plattform
    {
        [Key][Column("kPlattform")] public int Id { get; set; }
        [Column("cName")] public string Name { get; set; } = "";
        [Column("cTyp")] public string? Typ { get; set; }
        [Column("nAktiv")] public bool Aktiv { get; set; } = true;
    }
    #endregion

    #region Druckvorlagen
    [Table("tDruckvorlage")]
    public class Druckvorlage
    {
        [Key][Column("kDruckvorlage")] public int Id { get; set; }
        [Column("cName")] public string Name { get; set; } = "";
        [Column("cTyp")] public string Typ { get; set; } = ""; // Rechnung, Lieferschein, etc.
        [Column("cDatei")] public string? Datei { get; set; }
        [Column("nStandard")] public bool IstStandard { get; set; }
        [Column("nAktiv")] public bool Aktiv { get; set; } = true;
    }
    #endregion

    #region Eigene Felder
    [Table("tEigenesFeld")]
    public class EigenesFeld
    {
        [Key][Column("kEigenesFeld")] public int Id { get; set; }
        [Column("cBereich")] public string Bereich { get; set; } = ""; // Artikel, Kunde, Auftrag
        [Column("cName")] public string Name { get; set; } = "";
        [Column("cTyp")] public string Typ { get; set; } = "TEXT";
        [Column("cWerte")] public string? Werte { get; set; } // Für Dropdown
        [Column("nPflicht")] public bool IstPflicht { get; set; }
        [Column("nSort")] public int Sortierung { get; set; }
    }
    // EigenesFeldWert is defined in ErweiterteStammdaten.cs
    #endregion
}

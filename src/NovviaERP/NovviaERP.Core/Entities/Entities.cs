using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NovviaERP.Core.Entities
{
    #region Lager & Versand
    // Warenlager is defined in Stammdaten.cs with more fields

    [Table("tLagerbestand")]
    public class Lagerbestand
    {
        [Key][Column("kLagerbestand")] public int Id { get; set; }
        [Column("kArtikel")] public int ArtikelId { get; set; }
        [Column("kWarenlager")] public int WarenlagerId { get; set; }
        [Column("fBestand")] public decimal Bestand { get; set; }
        [Column("fReserviert")] public decimal Reserviert { get; set; }
        [Column("cChargenNr")] public string? ChargenNr { get; set; }
        [Column("dMHD")] public DateTime? MHD { get; set; }
        [NotMapped] public string? LagerName { get; set; }
    }

    public enum BewegungTyp { Eingang = 1, Ausgang = 2, Umlagerung = 3, Inventur = 4, Korrektur = 5, Retoure = 6 }

    public class Lagerbewegung
    {
        public int Id { get; set; }
        public int ArtikelId { get; set; }
        public int WarenlagerId { get; set; }
        public BewegungTyp Typ { get; set; }
        public decimal Menge { get; set; }
        public string? Referenz { get; set; }
        public DateTime Datum { get; set; } = DateTime.Now;
        public int? BenutzerId { get; set; }
    }
    #endregion

    #region Versand
    [Table("tVersand")]
    public class Versand
    {
        [Key][Column("kVersand")] public int Id { get; set; }
        [Column("kLieferschein")] public int LieferscheinId { get; set; }
        [Column("cVersandArt")] public string VersandArt { get; set; } = "";
        [Column("cTrackingId")] public string? TrackingId { get; set; }
        [Column("cTrackingUrl")] public string? TrackingUrl { get; set; }
        [Column("dVersandt")] public DateTime? Versandt { get; set; }
        [Column("fGewicht")] public decimal? Gewicht { get; set; }
    }

    public class VersandLabel
    {
        public int Id { get; set; }
        public int VersandId { get; set; }
        public string Carrier { get; set; } = "";
        public string TrackingNr { get; set; } = "";
        public byte[]? LabelPdf { get; set; }
        public DateTime Erstellt { get; set; } = DateTime.Now;
    }
    #endregion

    #region Zahlungen
    public class Zahlung
    {
        public int Id { get; set; }
        public int BestellungId { get; set; }
        public string ZahlungsArt { get; set; } = "";
        public decimal Betrag { get; set; }
        public DateTime Datum { get; set; }
        public string? Referenz { get; set; }
        public string? TransaktionsId { get; set; }
        public int Status { get; set; }
    }

    public class Mahnung
    {
        public int Id { get; set; }
        public int RechnungId { get; set; }
        public int Stufe { get; set; }
        public Mahnstufe? MahnstufeDetails { get; set; }
        public int Mahnstufe { get => Stufe; set => Stufe = value; }
        public DateTime Datum { get; set; }
        public DateTime? Faellig { get; set; }
        public decimal Betrag { get; set; }
        public decimal SummeOffen { get => Betrag; set => Betrag = value; }
        public decimal Gebuehr { get; set; }
        public decimal Zinsen { get; set; }
        public DateTime? Bezahlt { get; set; }
        public Rechnung? Rechnung { get; set; }
    }
    #endregion

    #region Retouren
    public class Retoure
    {
        public int Id { get; set; }
        public string RetoureNr { get; set; } = "";
        public int BestellungId { get; set; }
        public int KundeId { get; set; }
        public DateTime Datum { get; set; } = DateTime.Now;
        public string? Grund { get; set; }
        public int Status { get; set; }
        public List<RetourePosition> Positionen { get; set; } = new();
    }

    public class RetourePosition
    {
        public int Id { get; set; }
        public int RetoureId { get; set; }
        public int ArtikelId { get; set; }
        public decimal Menge { get; set; }
        public string? Grund { get; set; }
        public string? Zustand { get; set; }
    }
    #endregion

    #region Einkauf
    public class Bestellung_Einkauf
    {
        public int Id { get; set; }
        public string BestellNr { get; set; } = "";
        public int LieferantId { get; set; }
        public DateTime Datum { get; set; } = DateTime.Now;
        public DateTime? LieferDatum { get; set; }
        public int Status { get; set; }
        public decimal Summe { get; set; }
        public List<BestellungPos_Einkauf> Positionen { get; set; } = new();
    }

    public class BestellungPos_Einkauf
    {
        public int Id { get; set; }
        public int BestellungId { get; set; }
        public int ArtikelId { get; set; }
        public decimal Menge { get; set; }
        public decimal Preis { get; set; }
    }
    #endregion
}

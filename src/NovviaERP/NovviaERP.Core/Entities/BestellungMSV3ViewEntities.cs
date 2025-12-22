using System;

namespace NovviaERP.Core.Entities
{
    // =========================================================
    // VIEW-ENTITIES FÜR MSV3/MSVE BESTELLUNGEN
    // Diese Klassen repräsentieren die NOVVIA Views
    // =========================================================

    /// <summary>
    /// Detail-View: Lieferantenbestellung mit MSV3-Daten, MSVE-Bestand und MHD
    /// View: NOVVIA.vNOVVIA_BestellungMSV3
    /// </summary>
    public class BestellungMSV3Detail
    {
        // === Bestellkopf ===
        public int KLieferantenBestellung { get; set; }
        public string? CBestellNr { get; set; }
        public DateTime? DBestellDatum { get; set; }
        public DateTime? DLiefertermin { get; set; }
        public int NBestellStatus { get; set; }
        public string? CBestellStatusText { get; set; }
        public decimal FGesamtNetto { get; set; }
        public decimal FGesamtBrutto { get; set; }
        public string? CWaehrung { get; set; }
        public string? CBestellAnmerkung { get; set; }

        // === Lieferant ===
        public int KLieferant { get; set; }
        public string? CLieferantenNr { get; set; }
        public string? CLieferantFirma { get; set; }
        public string? CLieferantStrasse { get; set; }
        public string? CLieferantPLZ { get; set; }
        public string? CLieferantOrt { get; set; }
        public string? CLieferantLand { get; set; }
        public string? CLieferantTel { get; set; }
        public string? CLieferantMail { get; set; }

        // === MSV3 Lieferanten-Config ===
        public int? KMSV3Lieferant { get; set; }
        public string? CMSV3Url { get; set; }
        public string? CMSV3Kundennummer { get; set; }
        public string? CMSV3Filiale { get; set; }
        public int? NMSV3Version { get; set; }
        public bool NHatMSV3 { get; set; }

        // === MSV3 Bestellung ===
        public int? KMSV3Bestellung { get; set; }
        public string? CMSV3AuftragsId { get; set; }
        public string? CMSV3Status { get; set; }
        public DateTime? DMSV3Gesendet { get; set; }
        public DateTime? DMSV3Bestaetigt { get; set; }
        public DateTime? DMSV3Lieferung { get; set; }
        public int? NMSV3AnzahlPos { get; set; }
        public int? NMSV3Verfuegbar { get; set; }
        public int? NMSV3NichtVerfuegbar { get; set; }
        public string? CMSV3Fehler { get; set; }

        // === Position ===
        public int KLieferantenBestellungPos { get; set; }
        public int NPosNr { get; set; }
        public decimal FMengeBestellt { get; set; }
        public decimal FEKNetto { get; set; }
        public decimal FMwSt { get; set; }

        // === Artikel ===
        public int KArtikel { get; set; }
        public string? CArtNr { get; set; }
        public string? CArtikelName { get; set; }
        public string? CEAN { get; set; }
        public string? CHerstellerArtNr { get; set; }
        public decimal FLagerbestand { get; set; }
        public decimal FMindestbestand { get; set; }

        // === ABdata PZN Mapping ===
        public string? CPZN { get; set; }

        // === ABdata Pharma-Stammdaten ===
        public string? CABdataName { get; set; }
        public string? CPharmaHersteller { get; set; }
        public string? CDarreichungsform { get; set; }
        public string? CPackungsgroesse { get; set; }
        public decimal? FABdataAEP { get; set; }
        public decimal? FABdataAVP { get; set; }
        public bool NRezeptpflicht { get; set; }
        public bool NBTM { get; set; }
        public bool NKuehlpflichtig { get; set; }
        public string? CATC { get; set; }
        public string? CWirkstoff { get; set; }

        // === MSV3 Positions-Daten (MSVE Verfügbarkeit + MHD) ===
        public int? KMSV3BestellungPos { get; set; }
        public decimal? FMSVEBestand { get; set; }           // MSVE Bestand vom Großhandel
        public decimal? FMengeGeliefert { get; set; }
        public string? CMSV3PosStatus { get; set; }
        public decimal? FMSV3PreisEK { get; set; }
        public decimal? FMSV3PreisAEP { get; set; }
        public decimal? FMSV3PreisAVP { get; set; }
        public DateTime? DMHD { get; set; }                  // Mindesthaltbarkeitsdatum
        public string? CChargenNr { get; set; }              // Chargennummer
        public string? CMSV3Hinweis { get; set; }

        // === Berechnete Felder ===
        public string? CVerfuegbarkeitsStatus { get; set; }
        public string? CMHDStatus { get; set; }
        public int? NRestlaufzeitTage { get; set; }
    }

    /// <summary>
    /// Kopf-View: Aggregierte Bestelldaten mit MSV3-Summary
    /// View: NOVVIA.vNOVVIA_BestellungMSV3Kopf
    /// </summary>
    public class BestellungMSV3Kopf
    {
        public int KLieferantenBestellung { get; set; }
        public string? CBestellNr { get; set; }
        public DateTime? DBestellDatum { get; set; }
        public DateTime? DLiefertermin { get; set; }
        public int NStatus { get; set; }
        public string? CStatusText { get; set; }
        public decimal FGesamtNetto { get; set; }
        public decimal FGesamtBrutto { get; set; }
        public string? CWaehrung { get; set; }

        // Lieferant
        public int KLieferant { get; set; }
        public string? CLiefNr { get; set; }
        public string? CLieferantFirma { get; set; }

        // MSV3 Status
        public bool NHatMSV3 { get; set; }
        public string? CMSV3Status { get; set; }
        public string? CMSV3AuftragsId { get; set; }
        public DateTime? DMSV3Gesendet { get; set; }
        public DateTime? DMSV3Bestaetigt { get; set; }

        // Aggregierte Counts
        public int NAnzahlPositionen { get; set; }
        public int? NAnzahlVerfuegbar { get; set; }
        public int? NAnzahlNichtVerfuegbar { get; set; }
        public int NAnzahlKurzMHD { get; set; }
        public int NAnzahlMitCharge { get; set; }
    }

    /// <summary>
    /// MSVE Bestand + MHD View
    /// View: NOVVIA.vNOVVIA_MSVEBestandMHD
    /// </summary>
    public class MSVEBestandMHD
    {
        // Artikel-Identifikation
        public int KArtikel { get; set; }
        public string? CArtNr { get; set; }
        public string? CArtikelName { get; set; }
        public string? CEAN { get; set; }
        public string? CPZN { get; set; }

        // JTL Lagerbestand
        public decimal FJTLBestand { get; set; }
        public decimal FMindestbestand { get; set; }

        // MSVE Bestand vom Großhandel
        public decimal? FMSVEBestand { get; set; }
        public string? CMSVEStatus { get; set; }

        // MHD und Charge
        public DateTime? DMHD { get; set; }
        public string? CChargenNr { get; set; }
        public int? NRestlaufzeitTage { get; set; }
        public string? CMHDKategorie { get; set; }

        // Preise vom Großhandel
        public decimal? FPreisEK { get; set; }
        public decimal? FPreisAEP { get; set; }
        public decimal? FPreisAVP { get; set; }

        // Pharma-Stammdaten
        public string? CHersteller { get; set; }
        public string? CDarreichungsform { get; set; }
        public string? CPackungsgroesse { get; set; }
        public bool NRezeptpflicht { get; set; }
        public bool NKuehlpflichtig { get; set; }
        public bool NBTM { get; set; }
        public string? CATC { get; set; }
        public string? CWirkstoff { get; set; }

        // Lieferant
        public int? KLieferant { get; set; }
        public string? CLieferant { get; set; }

        // Zeitstempel
        public DateTime? DLetzteAbfrage { get; set; }
        public string? CMSV3Status { get; set; }
    }

    /// <summary>
    /// MHD-Status Enum für Kategorisierung
    /// </summary>
    public enum MHDKategorie
    {
        KeinMHD,
        Abgelaufen,
        Kurz,       // < 3 Monate
        Mittel,     // 3-6 Monate
        Lang        // > 6 Monate
    }

    /// <summary>
    /// MSV3 Positions-Status
    /// </summary>
    public enum MSV3PosStatus
    {
        Verfuegbar,
        Teilweise,
        NichtVerfuegbar
    }

    /// <summary>
    /// Bestellstatus für Lieferantenbestellungen
    /// </summary>
    /// <summary>
    /// JTL-Wawi tLieferantenBestellung.nStatus Werte
    /// </summary>
    public enum LieferantenBestellStatus
    {
        Offen = 0,
        Freigegeben = 5,
        InBearbeitung = 20,
        Teilgeliefert = 30,
        Abgeschlossen = 500,
        DropshippingAbgeschlossen = 700,
        Storniert = -1
    }
}

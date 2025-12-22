using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NovviaERP.Core.Entities
{
    #region Auth / Benutzer
    [Table("tBenutzer")]
    public class Benutzer
    {
        [Key][Column("kBenutzer")] public int Id { get; set; }
        [Column("cName")] public string Name { get; set; } = "";
        [Column("cVorname")] public string? Vorname { get; set; }
        [Column("cNachname")] public string? Nachname { get; set; }
        [Column("cLogin")] public string Login { get; set; } = "";
        [Column("cPasswort")] public string? Passwort { get; set; }
        [Column("cPasswortHash")] public string? PasswortHash { get; set; }
        [Column("cSalt")] public string? Salt { get; set; }
        [Column("cEmail")] public string? Email { get; set; }
        [Column("nAktiv")] public bool Aktiv { get; set; } = true;
        [Column("dErstellt")] public DateTime Erstellt { get; set; } = DateTime.Now;
        [Column("dLetzterLogin")] public DateTime? LetzterLogin { get; set; }
        [Column("kRolle")] public int? RolleId { get; set; }
        [Column("nFehlversuche")] public int Fehlversuche { get; set; }
        [Column("dGesperrtBis")] public DateTime? GesperrtBis { get; set; }

        [NotMapped] public string? RolleName { get; set; }
        [NotMapped] public List<string>? Berechtigungen { get; set; }
    }

    [Table("tRolle")]
    public class Rolle
    {
        [Key][Column("kRolle")] public int Id { get; set; }
        [Column("cName")] public string Name { get; set; } = "";
        [Column("cBeschreibung")] public string? Beschreibung { get; set; }
        [Column("nAktiv")] public bool Aktiv { get; set; } = true;
        [Column("nIstAdmin")] public bool IstAdmin { get; set; }

        [NotMapped] public List<RolleBerechtigung>? Berechtigungen { get; set; }
    }

    public class RolleBerechtigung
    {
        public int Id { get; set; }
        public int RolleId { get; set; }
        public int BerechtigungId { get; set; }
    }

    [Table("tBerechtigung")]
    public class Berechtigung
    {
        [Key][Column("kBerechtigung")] public int Id { get; set; }
        [Column("cName")] public string Name { get; set; } = "";
        [Column("cModul")] public string? Modul { get; set; }
        [Column("cAktion")] public string? Aktion { get; set; }
        [Column("cBeschreibung")] public string? Beschreibung { get; set; }
    }

    public class BenutzerLog
    {
        public int Id { get; set; }
        public int BenutzerId { get; set; }
        public string Aktion { get; set; } = "";
        public string? Details { get; set; }
        public DateTime Zeitpunkt { get; set; } = DateTime.Now;
        public string? IP { get; set; }
    }

    public class BenutzerSession
    {
        public int Id { get; set; }
        public int BenutzerId { get; set; }
        public string Token { get; set; } = "";
        public DateTime Erstellt { get; set; } = DateTime.Now;
        public DateTime? Ablauf { get; set; }
        public string? IP { get; set; }
    }
    #endregion

    #region Dashboard
    public class DashboardStats
    {
        public int OffeneAuftraege { get; set; }
        public int AuftraegeHeute { get; set; }
        public int BestellungenHeute { get => AuftraegeHeute; set => AuftraegeHeute = value; }
        public int OffeneBestellungen { get => OffeneAuftraege; set => OffeneAuftraege = value; }
        public int ZuVersenden { get; set; }
        public decimal UmsatzHeute { get; set; }
        public decimal UmsatzMonat { get; set; }
        public int NiedrigerBestand { get; set; }
        public int ArtikelUnterMindestbestand { get => NiedrigerBestand; set => NiedrigerBestand = value; }
        public int NeueKunden { get; set; }
        public int OffeneRechnungen { get; set; }
        public decimal OffenerBetrag { get; set; }
        public int RetourenOffen { get; set; }
        public int OffeneRMAs { get => RetourenOffen; set => RetourenOffen = value; }
        public int VersandHeute { get; set; }
    }
    #endregion

    #region Lieferschein
    [Table("tLieferschein")]
    public class Lieferschein
    {
        [Key][Column("kLieferschein")] public int Id { get; set; }
        [Column("cLieferscheinNr")] public string LieferscheinNr { get; set; } = "";
        [Column("kBestellung")] public int BestellungId { get; set; }
        [Column("kKunde")] public int KundeId { get; set; }
        [Column("dErstellt")] public DateTime Erstellt { get; set; } = DateTime.Now;
        [Column("dVersandt")] public DateTime? Versandt { get; set; }
        [Column("nStatus")] public int Status { get; set; }
        [Column("cTrackingNr")] public string? TrackingNr { get; set; }
        [Column("cAnmerkung")] public string? Anmerkung { get; set; }

        [NotMapped] public List<LieferscheinPosition>? Positionen { get; set; }
        [NotMapped] public Kunde? Kunde { get; set; }
    }

    [Table("tLieferscheinPos")]
    public class LieferscheinPosition
    {
        [Key][Column("kLieferscheinPos")] public int Id { get; set; }
        [Column("kLieferschein")] public int LieferscheinId { get; set; }
        [Column("kArtikel")] public int? ArtikelId { get; set; }
        [Column("cArtNr")] public string? ArtNr { get; set; }
        [Column("cName")] public string? Name { get; set; }
        [Column("fMenge")] public decimal Menge { get; set; }
        [Column("cChargenNr")] public string? ChargenNr { get; set; }
        [Column("dMHD")] public DateTime? MHD { get; set; }
    }

    [Table("tRechnungPos")]
    public class RechnungsPosition
    {
        [Key][Column("kRechnungPos")] public int Id { get; set; }
        [Column("kRechnung")] public int RechnungId { get; set; }
        [Column("kArtikel")] public int? ArtikelId { get; set; }
        [Column("cArtNr")] public string? ArtNr { get; set; }
        [Column("cName")] public string? Name { get; set; }
        [Column("fMenge")] public decimal Menge { get; set; }
        [Column("fPreisNetto")] public decimal PreisNetto { get; set; }
        [Column("fPreisBrutto")] public decimal PreisBrutto { get; set; }
        [Column("fMwSt")] public decimal MwStSatz { get; set; }
        [Column("fRabatt")] public decimal Rabatt { get; set; }
        // Aliasse
        [NotMapped] public decimal VKNetto { get => PreisNetto; set => PreisNetto = value; }
        [NotMapped] public decimal VKBrutto { get => PreisBrutto; set => PreisBrutto = value; }
    }
    #endregion

    #region Import DTOs (shared)
    public class ImportVorschau
    {
        public List<string> Header { get; set; } = new();
        public List<Dictionary<string, string>> Zeilen { get; set; } = new();
        public List<string> Warnungen { get; set; } = new();
        public int GesamtZeilen { get; set; }
    }

    public class ImportOptionen
    {
        public bool NeueKundenErstellen { get; set; } = true;
        public bool UnbekannteArtikelErstellen { get; set; } = false;
        public bool FehlerIgnorieren { get; set; } = false;
        public bool UpdateExistierend { get; set; } = true;
        public string Trennzeichen { get; set; } = ";";
        public bool Transaktion { get; set; } = true;
        public int? QuelleId { get; set; }
    }

    public class ImportErgebnis
    {
        public int GesamtZeilen { get; set; }
        public int Erfolgreich { get; set; }
        public int ErfolgreicheZeilen { get; set; }
        public int Fehlgeschlagen { get; set; }
        public int FehlerhafteZeilen { get; set; }
        public int ErstellteAuftraege { get; set; }
        public int ImportiertePositionen { get; set; }
        public List<int> ImportierteIds { get; set; } = new();
        public List<string> Fehler { get; set; } = new();
        public List<string> Warnungen { get; set; } = new();
    }

    public class ImportVorlage
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Typ { get; set; } = "Auftrag";
        public string Trennzeichen { get; set; } = ";";
        public string Dateiendung { get; set; } = "csv";
        public bool HatKopfzeile { get; set; } = true;
        public string? FeldzuordnungJson { get; set; }
        public string? StandardwerteJson { get; set; }
    }
    #endregion

    #region Email
    public class EmailVorlage
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Typ { get; set; } = "";
        public string Betreff { get; set; } = "";
        public string Text { get; set; } = "";
        public string? HtmlText { get; set; }
        public bool IstHtml { get; set; }
        public bool IstStandard { get; set; }
        public bool Aktiv { get; set; } = true;
        public string Sprache { get; set; } = "DE";
    }

    public class EmailLog
    {
        public int Id { get; set; }
        public string Empfaenger { get; set; } = "";
        public string Betreff { get; set; } = "";
        public DateTime Gesendet { get; set; } = DateTime.Now;
        public bool Erfolgreich { get; set; }
        public string? Fehler { get; set; }
        public int? VorlageId { get; set; }
        public string? Referenz { get; set; }
    }
    #endregion

    #region Worker / Jobs
    public class WorkerJob
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Typ { get; set; } = "";
        public string? Parameter { get; set; }
        public string Status { get; set; } = "Wartend";
        public DateTime? LetzterLauf { get; set; }
        public DateTime? NaechsterLauf { get; set; }
        public string? Intervall { get; set; }
        public bool Aktiv { get; set; } = true;
        public string? FehlerMeldung { get; set; }
    }

    public class WorkerLog
    {
        public int Id { get; set; }
        public int JobId { get; set; }
        public DateTime Start { get; set; }
        public DateTime? Ende { get; set; }
        public string Status { get; set; } = "";
        public string? Meldung { get; set; }
        public int? Verarbeitet { get; set; }
        public int? Fehler { get; set; }
    }
    #endregion

    #region Einstellungen
    public class Einstellung
    {
        public int Id { get; set; }
        public string Schluessel { get; set; } = "";
        public string? Wert { get; set; }
        public string? Typ { get; set; }
        public string? Gruppe { get; set; }
        public string? Beschreibung { get; set; }
    }
    // Firma, Sprache, Steuerklasse, Kundengruppe are defined in Stammdaten.cs
    #endregion

    #region Warengruppe
    public class Warengruppe
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int? ParentId { get; set; }
        public bool Aktiv { get; set; } = true;
    }
    #endregion

    #region Workflow
    public class Workflow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Ereignis { get; set; } = "";
        public string? Bedingung { get; set; }
        public string Aktion { get; set; } = "";
        public string? Parameter { get; set; }
        public bool Aktiv { get; set; } = true;
        public int Sortierung { get; set; }
    }
    #endregion

    #region Zahlungen
    public class Zahlungseingang
    {
        public int Id { get; set; }
        public int? RechnungId { get; set; }
        public int? KundeId { get; set; }
        public decimal Betrag { get; set; }
        public DateTime Datum { get; set; } = DateTime.Now;
        public string? Zahlungsart { get; set; }
        public string? Referenz { get; set; }
        public string? Bemerkung { get; set; }
    }
    #endregion

    #region Packliste
    public class Packliste
    {
        public int Id { get; set; }
        public string PacklisteNr { get; set; } = "";
        public int BestellungId { get; set; }
        public DateTime Erstellt { get; set; } = DateTime.Now;
        public int? BearbeiterId { get; set; }
        public int Status { get; set; }
        public List<PacklistePosition>? Positionen { get; set; }
    }

    public class PacklistePosition
    {
        public int Id { get; set; }
        public int PacklisteId { get; set; }
        public int ArtikelId { get; set; }
        public string? ArtNr { get; set; }
        public string? Name { get; set; }
        public decimal MengeSoll { get; set; }
        public decimal MengeIst { get; set; }
        public string? ChargenNr { get; set; }
        public DateTime? MHD { get; set; }
        public int? LagerplatzId { get; set; }
    }
    #endregion
}

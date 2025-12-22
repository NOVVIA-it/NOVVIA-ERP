using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NovviaERP.Core.Entities
{
    // =========================================================
    // NOVVIA-SPEZIFISCHE TABELLEN
    // Nur Tabellen die JTL-Wawi NICHT hat!
    // Präfix: NOVVIA. zur eindeutigen Unterscheidung
    // =========================================================

    /// <summary>
    /// Import-Vorlagen für CSV/Excel Import (JTL hat das nicht)
    /// </summary>
    [Table("NOVVIA.tImportVorlage")]
    public class NovviaImportVorlage
    {
        [Key]
        [Column("kImportVorlage")]
        public int Id { get; set; }

        [Column("cName")]
        public string Name { get; set; } = "";

        [Column("cTyp")]
        public string Typ { get; set; } = "Auftrag";

        [Column("cTrennzeichen")]
        public string Trennzeichen { get; set; } = ";";

        [Column("nKopfzeile")]
        public bool HatKopfzeile { get; set; } = true;

        [Column("cFeldzuordnungJson")]
        public string? FeldzuordnungJson { get; set; }

        [Column("cStandardwerte")]
        public string? Standardwerte { get; set; }

        [Column("dErstellt")]
        public DateTime Erstellt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Import-Log (JTL hat das nicht)
    /// </summary>
    [Table("NOVVIA.tImportLog")]
    public class NovviaImportLog
    {
        [Key]
        [Column("kImportLog")]
        public int Id { get; set; }

        [Column("cDatei")]
        public string? Datei { get; set; }

        [Column("cTyp")]
        public string? Typ { get; set; }

        [Column("nZeilen")]
        public int Zeilen { get; set; }

        [Column("nErfolg")]
        public int Erfolg { get; set; }

        [Column("nFehler")]
        public int Fehler { get; set; }

        [Column("cFehlerDetails")]
        public string? FehlerDetails { get; set; }

        [Column("dImport")]
        public DateTime Import { get; set; } = DateTime.Now;

        [Column("kBenutzer")]
        public int? BenutzerId { get; set; }
    }

    /// <summary>
    /// Worker-Status für Hintergrund-Jobs (JTL hat das nicht)
    /// </summary>
    [Table("NOVVIA.tWorkerStatus")]
    public class NovviaWorkerStatus
    {
        [Key]
        [Column("kWorkerStatus")]
        public int Id { get; set; }

        [Column("cWorker")]
        public string Worker { get; set; } = "";

        [Column("nStatus")]
        public int Status { get; set; }

        [Column("dLetzterLauf")]
        public DateTime? LetzterLauf { get; set; }

        [Column("nLaufzeit_ms")]
        public int? LaufzeitMs { get; set; }

        [Column("cLetzteFehler")]
        public string? LetzteFehler { get; set; }

        [Column("nAktiv")]
        public bool Aktiv { get; set; } = true;

        [Column("cKonfigJson")]
        public string? KonfigJson { get; set; }
    }

    /// <summary>
    /// Worker-Log (JTL hat das nicht)
    /// </summary>
    [Table("NOVVIA.tWorkerLog")]
    public class NovviaWorkerLog
    {
        [Key]
        [Column("kWorkerLog")]
        public int Id { get; set; }

        [Column("cWorker")]
        public string Worker { get; set; } = "";

        [Column("cLevel")]
        public string Level { get; set; } = "INFO";

        [Column("cNachricht")]
        public string? Nachricht { get; set; }

        [Column("dZeitpunkt")]
        public DateTime Zeitpunkt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Ausgabe-Log für Druck/Mail/PDF (JTL hat das nicht in dieser Form)
    /// </summary>
    [Table("NOVVIA.tAusgabeLog")]
    public class NovviaAusgabeLog
    {
        [Key]
        [Column("kAusgabeLog")]
        public int Id { get; set; }

        [Column("cDokumentTyp")]
        public string DokumentTyp { get; set; } = "";

        [Column("kDokument")]
        public int DokumentId { get; set; }

        [Column("cDokumentNr")]
        public string? DokumentNr { get; set; }

        [Column("cAktionen")]
        public string? Aktionen { get; set; }

        [Column("nGedruckt")]
        public bool Gedruckt { get; set; }

        [Column("nGespeichert")]
        public bool Gespeichert { get; set; }

        [Column("nMailGesendet")]
        public bool MailGesendet { get; set; }

        [Column("nVorschau")]
        public bool Vorschau { get; set; }

        [Column("cFehler")]
        public string? Fehler { get; set; }

        [Column("dZeitpunkt")]
        public DateTime Zeitpunkt { get; set; } = DateTime.Now;

        [Column("kBenutzer")]
        public int? BenutzerId { get; set; }
    }

    /// <summary>
    /// Dokument-Archiv für PDFs (JTL hat ähnliches, aber wir brauchen eigenes)
    /// </summary>
    [Table("NOVVIA.tDokumentArchiv")]
    public class NovviaDokumentArchiv
    {
        [Key]
        [Column("kDokumentArchiv")]
        public int Id { get; set; }

        [Column("cDokumentTyp")]
        public string DokumentTyp { get; set; } = "";

        [Column("kDokument")]
        public int DokumentId { get; set; }

        [Column("cDokumentNr")]
        public string? DokumentNr { get; set; }

        [Column("cPfad")]
        public string? Pfad { get; set; }

        [Column("bPdfDaten")]
        public byte[]? PdfDaten { get; set; }

        [Column("nGroesse")]
        public int? Groesse { get; set; }

        [Column("dArchiviert")]
        public DateTime Archiviert { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Plattform-Sync-Log (JTL hat eigenes, wir erweitern)
    /// </summary>
    [Table("NOVVIA.tPlattformSyncLog")]
    public class NovviaPlattformSyncLog
    {
        [Key]
        [Column("kPlattformSyncLog")]
        public int Id { get; set; }

        [Column("kShop")]
        public int ShopId { get; set; }

        [Column("cAktion")]
        public string Aktion { get; set; } = "";

        [Column("nAnzahl")]
        public int? Anzahl { get; set; }

        [Column("cDetails")]
        public string? Details { get; set; }

        [Column("nErfolg")]
        public bool Erfolg { get; set; }

        [Column("cFehler")]
        public string? Fehler { get; set; }

        [Column("dZeitpunkt")]
        public DateTime Zeitpunkt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Artikel-Beschreibung je Shop (falls JTL das nicht hat)
    /// </summary>
    [Table("NOVVIA.tArtikelShopBeschreibung")]
    public class NovviaArtikelShopBeschreibung
    {
        [Key]
        [Column("kArtikelShopBeschreibung")]
        public int Id { get; set; }

        [Column("kArtikel")]
        public int ArtikelId { get; set; }

        [Column("kShop")]
        public int ShopId { get; set; }

        [Column("cName")]
        public string? Name { get; set; }

        [Column("cBeschreibung")]
        public string? Beschreibung { get; set; }

        [Column("cBeschreibungHtml")]
        public string? BeschreibungHtml { get; set; }

        [Column("cKurztext")]
        public string? Kurztext { get; set; }

        [Column("fPreis")]
        public decimal? Preis { get; set; }

        [Column("nAktiv")]
        public bool Aktiv { get; set; } = true;

        [Column("dAktualisiert")]
        public DateTime? Aktualisiert { get; set; }
    }
}

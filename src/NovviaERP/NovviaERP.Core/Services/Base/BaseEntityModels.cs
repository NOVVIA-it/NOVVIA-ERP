using System;

namespace NovviaERP.Core.Services.Base
{
    /// <summary>
    /// Basis-Klasse fuer Entity-Uebersichten in Listen
    /// </summary>
    public abstract class BaseEntityOverview
    {
        public abstract int Id { get; }
        public abstract string? Nummer { get; }
        public abstract string? Bezeichnung { get; }
        public virtual DateTime? ErstelltAm { get; set; }
        public virtual bool IstAktiv { get; set; } = true;
    }

    /// <summary>
    /// Basis-Klasse fuer Entity-Details
    /// </summary>
    public abstract class BaseEntityDetail
    {
        public abstract int Id { get; set; }
        public virtual DateTime? DtErstellt { get; set; }
        public virtual DateTime? DtGeaendert { get; set; }
        public virtual int? KErstelltVon { get; set; }
        public virtual int? KGeaendertVon { get; set; }
    }

    /// <summary>
    /// Basis-Klasse fuer Referenz-Daten (Dropdowns)
    /// </summary>
    public class BaseRefItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";

        public override string ToString() => Name;
    }

    /// <summary>
    /// Suchergebnis fuer EntitySucheDialog
    /// </summary>
    public class EntitySuchErgebnis
    {
        public int Id { get; set; }
        public string Nr { get; set; } = "";
        public string Name { get; set; } = "";
        public string Extra { get; set; } = "";
    }

    /// <summary>
    /// Filter-Parameter fuer Listen
    /// </summary>
    public class ListFilterParameter
    {
        public string? Suchbegriff { get; set; }
        public string Zeitraum { get; set; } = "Alle";
        public int? StatusFilter { get; set; }
        public int? GruppeFilter { get; set; }
        public int? KategorieFilter { get; set; }
        public bool NurAktive { get; set; } = true;
        public int Limit { get; set; } = 500;
        public int Offset { get; set; } = 0;

        /// <summary>
        /// Datumsbereich basierend auf Zeitraum
        /// </summary>
        public (DateTime von, DateTime bis) DatumsBereich => BaseDatabaseService.JtlZeitraumZuDatum(Zeitraum);
    }
}

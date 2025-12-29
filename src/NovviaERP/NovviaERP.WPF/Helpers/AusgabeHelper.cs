using System.Windows;
using NovviaERP.Core.Services;
using NovviaERP.WPF.Views;

namespace NovviaERP.WPF.Helpers
{
    /// <summary>
    /// Zentraler Helper für alle Ausgabe-Operationen (Drucken, E-Mail, PDF)
    /// </summary>
    public static class AusgabeHelper
    {
        /// <summary>
        /// Öffnet den Ausgabe-Dialog für ein Dokument
        /// </summary>
        public static bool? Ausgabe(DokumentTyp typ, int dokumentId, string? dokumentNr = null, Window? owner = null)
        {
            var dialog = new AusgabeDialog(typ, dokumentId, dokumentNr);
            if (owner != null)
                dialog.Owner = owner;
            return dialog.ShowDialog();
        }

        /// <summary>
        /// Öffnet den Ausgabe-Dialog für eine Rechnung
        /// </summary>
        public static bool? AusgabeRechnung(int rechnungId, string? rechnungNr = null, Window? owner = null)
            => Ausgabe(DokumentTyp.Rechnung, rechnungId, rechnungNr, owner);

        /// <summary>
        /// Öffnet den Ausgabe-Dialog für einen Lieferschein
        /// </summary>
        public static bool? AusgabeLieferschein(int lieferscheinId, string? lieferscheinNr = null, Window? owner = null)
            => Ausgabe(DokumentTyp.Lieferschein, lieferscheinId, lieferscheinNr, owner);

        /// <summary>
        /// Öffnet den Ausgabe-Dialog für einen Auftrag/Bestellung
        /// </summary>
        public static bool? AusgabeAuftrag(int auftragId, string? auftragNr = null, Window? owner = null)
            => Ausgabe(DokumentTyp.Bestellung, auftragId, auftragNr, owner);

        /// <summary>
        /// Öffnet den Ausgabe-Dialog für ein Angebot
        /// </summary>
        public static bool? AusgabeAngebot(int angebotId, string? angebotNr = null, Window? owner = null)
            => Ausgabe(DokumentTyp.Angebot, angebotId, angebotNr, owner);

        /// <summary>
        /// Öffnet den Ausgabe-Dialog für eine Mahnung
        /// </summary>
        public static bool? AusgabeMahnung(int mahnungId, string? mahnungNr = null, Window? owner = null)
            => Ausgabe(DokumentTyp.Mahnung, mahnungId, mahnungNr, owner);

        /// <summary>
        /// Öffnet den Ausgabe-Dialog für eine Gutschrift
        /// </summary>
        public static bool? AusgabeGutschrift(int gutschriftId, string? gutschriftNr = null, Window? owner = null)
            => Ausgabe(DokumentTyp.Gutschrift, gutschriftId, gutschriftNr, owner);

        /// <summary>
        /// Öffnet den Ausgabe-Dialog für eine Lieferantenbestellung
        /// </summary>
        public static bool? AusgabeLieferantenBestellung(int bestellungId, string? bestellungNr = null, Window? owner = null)
            => Ausgabe(DokumentTyp.Bestellung, bestellungId, bestellungNr, owner);
    }
}

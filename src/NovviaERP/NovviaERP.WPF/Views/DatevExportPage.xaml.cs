using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using NovviaERP.Core.Services;
using DatevBuchung = NovviaERP.Core.Services.DatevBuchung;

namespace NovviaERP.WPF.Views
{
    public partial class DatevExportPage : UserControl
    {
        private readonly CoreService _core;
        private List<DatevBuchung> _buchungen = new();

        public DatevExportPage()
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();

            // Standardzeitraum: Aktueller Monat
            var heute = DateTime.Today;
            dpVon.SelectedDate = new DateTime(heute.Year, heute.Month, 1);
            dpBis.SelectedDate = dpVon.SelectedDate.Value.AddMonths(1).AddDays(-1);
        }

        private void BtnAktuellerMonat_Click(object sender, RoutedEventArgs e)
        {
            var heute = DateTime.Today;
            dpVon.SelectedDate = new DateTime(heute.Year, heute.Month, 1);
            dpBis.SelectedDate = dpVon.SelectedDate.Value.AddMonths(1).AddDays(-1);
        }

        private void BtnVormonat_Click(object sender, RoutedEventArgs e)
        {
            var heute = DateTime.Today;
            dpVon.SelectedDate = new DateTime(heute.Year, heute.Month, 1).AddMonths(-1);
            dpBis.SelectedDate = dpVon.SelectedDate.Value.AddMonths(1).AddDays(-1);
        }

        private void BtnAktuellesJahr_Click(object sender, RoutedEventArgs e)
        {
            var heute = DateTime.Today;
            dpVon.SelectedDate = new DateTime(heute.Year, 1, 1);
            dpBis.SelectedDate = new DateTime(heute.Year, 12, 31);
        }

        private async void BtnVorschau_Click(object sender, RoutedEventArgs e)
        {
            if (dpVon.SelectedDate == null || dpBis.SelectedDate == null)
            {
                MessageBox.Show("Bitte waehlen Sie einen Zeitraum aus.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var von = dpVon.SelectedDate.Value;
                var bis = dpBis.SelectedDate.Value;

                _buchungen = (await _core.GetDatevBuchungenAsync(von, bis,
                    chkRechnungen.IsChecked ?? false,
                    chkGutschriften.IsChecked ?? false,
                    chkEingangsrechnungen.IsChecked ?? false,
                    chkZahlungen.IsChecked ?? false)).ToList();

                dgVorschau.ItemsSource = _buchungen;

                // Summen
                var summeRechnungen = _buchungen.Where(b => b.Typ == "R").Sum(b => b.Betrag);
                var summeGutschriften = _buchungen.Where(b => b.Typ == "G").Sum(b => b.Betrag);
                var summeZahlungen = _buchungen.Where(b => b.Typ == "Z").Sum(b => b.Betrag);

                txtSummeRechnungen.Text = $"Rechnungen: {summeRechnungen:N2} EUR";
                txtSummeGutschriften.Text = $"Gutschriften: {summeGutschriften:N2} EUR";
                txtSummeZahlungen.Text = $"Zahlungen: {summeZahlungen:N2} EUR";
                txtVorschauInfo.Text = $"{_buchungen.Count} Buchungssaetze geladen";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (!_buchungen.Any())
            {
                MessageBox.Show("Bitte laden Sie zuerst die Vorschau.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "CSV-Datei|*.csv|Alle Dateien|*.*",
                FileName = $"DATEV_Export_{dpVon.SelectedDate:yyyyMMdd}_{dpBis.SelectedDate:yyyyMMdd}.csv",
                DefaultExt = ".csv"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                var sb = new StringBuilder();

                // DATEV-Header
                sb.AppendLine("\"Umsatz (ohne Soll/Haben-Kz)\";\"Soll/Haben-Kennzeichen\";\"WKZ Umsatz\";\"Kurs\";\"Basis-Umsatz\";\"WKZ Basis-Umsatz\";\"Konto\";\"Gegenkonto (ohne BU-Schlüssel)\";\"BU-Schlüssel\";\"Belegdatum\";\"Belegfeld 1\";\"Belegfeld 2\";\"Skonto\";\"Buchungstext\"");

                foreach (var b in _buchungen)
                {
                    var betrag = b.Betrag.ToString("F2").Replace(".", ",");
                    var datum = b.Datum.ToString("ddMM");
                    var sollHaben = b.Betrag >= 0 ? "S" : "H";

                    sb.AppendLine($"\"{betrag}\";\"{sollHaben}\";\"EUR\";\"\";\"\";\"\";\"" +
                        $"{b.SollKonto}\";\"{b.HabenKonto}\";\"{b.UstSchluessel}\";\"{datum}\";\"{b.BelegNr}\";\"\";\"\";\"{b.Buchungstext}\"");
                }

                File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.GetEncoding(1252)); // ANSI
                MessageBox.Show($"Export erfolgreich!\n\n{_buchungen.Count} Buchungssaetze exportiert nach:\n{dialog.FileName}",
                    "DATEV-Export", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Export:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

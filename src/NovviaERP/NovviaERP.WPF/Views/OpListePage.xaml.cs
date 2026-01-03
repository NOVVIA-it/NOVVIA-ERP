using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;
using OffenerPosten = NovviaERP.Core.Services.OffenerPosten;

namespace NovviaERP.WPF.Views
{
    public partial class OpListePage : UserControl
    {
        private readonly CoreService _core;
        private List<OffenerPosten> _alleDaten = new();

        public OpListePage()
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();
            dpStichtag.SelectedDate = DateTime.Today;
            Loaded += async (s, e) => await LadeAsync();
        }

        private async System.Threading.Tasks.Task LadeAsync()
        {
            try
            {
                var stichtag = dpStichtag.SelectedDate ?? DateTime.Today;
                _alleDaten = (await _core.GetOffenePostenAsync(stichtag)).ToList();
                AnwendeFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AnwendeFilter()
        {
            var gefiltert = _alleDaten.AsEnumerable();

            // Art-Filter
            if (cmbArt.SelectedItem is ComboBoxItem item && item.Tag is string art && !string.IsNullOrEmpty(art))
            {
                gefiltert = gefiltert.Where(p => p.Art == art);
            }

            // Suchtext
            var suche = txtSuche.Text?.Trim().ToLower();
            if (!string.IsNullOrEmpty(suche))
            {
                gefiltert = gefiltert.Where(p =>
                    (p.PartnerName?.ToLower().Contains(suche) ?? false) ||
                    (p.PartnerNr?.ToLower().Contains(suche) ?? false) ||
                    (p.BelegNr?.ToLower().Contains(suche) ?? false));
            }

            // Nur ueberfaellige
            if (chkNurUeberfaellig.IsChecked == true)
            {
                gefiltert = gefiltert.Where(p => p.TageOffen > 0);
            }

            var liste = gefiltert.ToList();
            dgOpListe.ItemsSource = liste;

            // Summen berechnen
            var forderungen = liste.Where(p => p.Art == "D").Sum(p => p.Offen);
            var verbindlichkeiten = liste.Where(p => p.Art == "K").Sum(p => p.Offen);

            txtAnzahl.Text = $"{liste.Count} Belege";
            txtForderungen.Text = $"{forderungen:N2} EUR";
            txtVerbindlichkeiten.Text = $"{verbindlichkeiten:N2} EUR";
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (IsLoaded) AnwendeFilter();
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded) AnwendeFilter();
        }

        private void Filter_Changed(object sender, TextChangedEventArgs e)
        {
            if (IsLoaded) AnwendeFilter();
        }

        private async void BtnAktualisieren_Click(object sender, RoutedEventArgs e)
        {
            await LadeAsync();
        }

        private void BtnExcelExport_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Excel-Export wird implementiert.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnPdfExport_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("PDF-Export wird implementiert.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}

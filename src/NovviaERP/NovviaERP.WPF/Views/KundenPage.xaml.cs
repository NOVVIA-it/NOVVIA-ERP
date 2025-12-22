using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class KundenPage : Page
    {
        private readonly CoreService _coreService;
        private List<CoreService.KundeUebersicht> _kunden = new();
        private List<CoreService.KundengruppeRef> _kundengruppen = new();

        public KundenPage()
        {
            InitializeComponent();
            _coreService = App.Services.GetRequiredService<CoreService>();
            Loaded += async (s, e) => await LadeKundenAsync();
        }

        private async System.Threading.Tasks.Task LadeKundenAsync()
        {
            try
            {
                txtStatus.Text = "Lade Kunden...";

                // Kundengruppen laden (einmalig)
                if (_kundengruppen.Count == 0)
                {
                    _kundengruppen = (await _coreService.GetKundengruppenAsync()).ToList();
                    cmbKundengruppe.Items.Clear();
                    cmbKundengruppe.Items.Add(new ComboBoxItem { Content = "Alle Gruppen", IsSelected = true });
                    foreach (var kg in _kundengruppen)
                    {
                        cmbKundengruppe.Items.Add(new ComboBoxItem { Content = kg.CName, Tag = kg.KKundenGruppe });
                    }
                    cmbKundengruppe.SelectedIndex = 0;
                }

                // Kunden laden
                int? kundengruppeId = null;
                if (cmbKundengruppe.SelectedItem is ComboBoxItem item && item.Tag is int kgId)
                    kundengruppeId = kgId;

                bool nurAktive = chkNurAktive.IsChecked == true;

                _kunden = (await _coreService.GetKundenAsync(
                    suche: string.IsNullOrWhiteSpace(txtSuche.Text) ? null : txtSuche.Text,
                    kundengruppeId: kundengruppeId,
                    nurAktive: nurAktive
                )).ToList();

                dgKunden.ItemsSource = _kunden;
                txtAnzahl.Text = $"({_kunden.Count} Kunden)";
                txtStatus.Text = $"{_kunden.Count} Kunden geladen";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
                MessageBox.Show($"Fehler beim Laden der Kunden:\n{ex.Message}", "Fehler",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Suchen_Click(object sender, RoutedEventArgs e)
        {
            await LadeKundenAsync();
        }

        private async void TxtSuche_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                await LadeKundenAsync();
        }

        private async void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (IsLoaded)
                await LadeKundenAsync();
        }

        private async void Aktualisieren_Click(object sender, RoutedEventArgs e)
        {
            await LadeKundenAsync();
        }

        private void Neu_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new KundeDetailPage(null));
        }

        private void Bearbeiten_Click(object sender, RoutedEventArgs e)
        {
            if (dgKunden.SelectedItem is CoreService.KundeUebersicht kunde)
                NavigationService?.Navigate(new KundeDetailPage(kunde.KKunde));
        }

        private void DG_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgKunden.SelectedItem is CoreService.KundeUebersicht kunde)
                NavigationService?.Navigate(new KundeDetailPage(kunde.KKunde));
        }

        private void DG_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool hasSelection = dgKunden.SelectedItem != null;
            btnBearbeiten.IsEnabled = hasSelection;
            btnBestellungen.IsEnabled = hasSelection;
            btnZusammenfuehren.IsEnabled = hasSelection;
        }

        private void Bestellungen_Click(object sender, RoutedEventArgs e)
        {
            if (dgKunden.SelectedItem is CoreService.KundeUebersicht kunde)
            {
                // TODO: Navigate to BestellungenPage with filter for this customer
                MessageBox.Show($"Bestellungen für Kunde {kunde.CKundenNr} - {kunde.Anzeigename}",
                    "Bestellungen", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Zusammenfuehren_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Funktion 'Kunden zusammenführen' wird implementiert...",
                "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}

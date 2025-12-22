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
    public partial class KundenView : UserControl
    {
        private readonly CoreService _coreService;
        private List<CoreService.KundeUebersicht> _kunden = new();

        public KundenView()
        {
            InitializeComponent();
            _coreService = App.Services.GetRequiredService<CoreService>();
            Loaded += async (s, e) => await LadeKundenAsync();
        }

        private void NavigateTo(UserControl view)
        {
            if (Window.GetWindow(this) is MainWindow main)
                main.ShowContent(view);
        }

        private async System.Threading.Tasks.Task LadeKundenAsync()
        {
            try
            {
                txtStatus.Text = "Lade Kunden...";
                _kunden = (await _coreService.GetKundenAsync(
                    suche: string.IsNullOrWhiteSpace(txtSuche.Text) ? null : txtSuche.Text
                )).ToList();

                dgKunden.ItemsSource = _kunden;
                txtAnzahl.Text = $"({_kunden.Count} Kunden)";
                txtStatus.Text = $"{_kunden.Count} Kunden geladen";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Suchen_Click(object sender, RoutedEventArgs e) => await LadeKundenAsync();
        private async void TxtSuche_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) await LadeKundenAsync(); }
        private async void Aktualisieren_Click(object sender, RoutedEventArgs e) => await LadeKundenAsync();

        private void Neu_Click(object sender, RoutedEventArgs e) => NavigateTo(new KundeDetailView(null));
        private void Bearbeiten_Click(object sender, RoutedEventArgs e)
        {
            if (dgKunden.SelectedItem is CoreService.KundeUebersicht kunde)
                NavigateTo(new KundeDetailView(kunde.KKunde));
        }
        private void DG_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgKunden.SelectedItem is CoreService.KundeUebersicht kunde)
                NavigateTo(new KundeDetailView(kunde.KKunde));
        }
        private void DG_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnBearbeiten.IsEnabled = dgKunden.SelectedItem != null;
        }
    }
}

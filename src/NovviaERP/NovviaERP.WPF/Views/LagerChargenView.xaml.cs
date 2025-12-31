using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class LagerChargenView : UserControl
    {
        private readonly CoreService _core;
        private List<CoreService.ChargenBestand> _chargen = new();

        public LagerChargenView()
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();
            Loaded += async (s, e) => await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                txtStatus.Text = "Lade Lager...";

                // Warenlager laden
                var lager = (await _core.GetWarenlagerAsync()).ToList();
                lager.Insert(0, new CoreService.WarenlagerRef { KWarenLager = 0, CName = "Alle Lager" });
                cmbLager.ItemsSource = lager;
                cmbLager.SelectedIndex = 0;

                await LadeChargenAsync();
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
                MessageBox.Show($"Fehler beim Laden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LadeChargenAsync()
        {
            try
            {
                txtStatus.Text = "Lade Chargen...";

                int? kWarenlager = null;
                if (cmbLager.SelectedValue is int selectedLager && selectedLager > 0)
                    kWarenlager = selectedLager;

                var suche = txtSuche.Text.Trim();
                var nurGesperrt = chkNurGesperrt.IsChecked == true;
                var nurQuarantaene = chkNurQuarantaene.IsChecked == true;
                var nurAbgelaufen = chkNurAbgelaufen.IsChecked == true;

                _chargen = (await _core.GetChargenBestaendeAsync(
                    kWarenlager: kWarenlager,
                    suche: string.IsNullOrEmpty(suche) ? null : suche,
                    nurGesperrt: nurGesperrt,
                    nurQuarantaene: nurQuarantaene,
                    nurAbgelaufen: nurAbgelaufen
                )).ToList();

                dgChargen.ItemsSource = _chargen;

                // Statistik
                var gesperrt = _chargen.Count(c => c.NGesperrt);
                var quarantaene = _chargen.Count(c => c.NQuarantaene);
                var abgelaufen = _chargen.Count(c => c.CMHDStatus == "Abgelaufen");

                txtAnzahl.Text = $"({_chargen.Count} Chargen, {gesperrt} gesperrt, {quarantaene} Quarantaene, {abgelaufen} MHD abgelaufen)";
                txtStatus.Text = $"{_chargen.Count} Chargen geladen";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
                MessageBox.Show($"Fehler beim Laden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Event Handlers

        private async void Suchen_Click(object sender, RoutedEventArgs e) => await LadeChargenAsync();
        private async void Aktualisieren_Click(object sender, RoutedEventArgs e) => await LadeChargenAsync();

        private async void Lager_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            await LadeChargenAsync();
        }

        private async void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            await LadeChargenAsync();
        }

        private async void TxtSuche_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) await LadeChargenAsync();
        }

        private async void TxtVerfolgungChargenNr_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) LadeVerfolgung_Click(sender, e);
        }

        private void DG_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = dgChargen.SelectedItem as CoreService.ChargenBestand;
            var hasSelection = selected != null;

            btnVerfolgung.IsEnabled = hasSelection;
            btnSperren.IsEnabled = hasSelection && !selected!.NGesperrt && !selected.NQuarantaene;
            btnFreigeben.IsEnabled = hasSelection && selected!.NGesperrt && !selected.NQuarantaene;
            btnQuarantaene.IsEnabled = hasSelection && !selected!.NQuarantaene;
            btnAusQuarantaene.IsEnabled = hasSelection && selected!.NQuarantaene;
        }

        private void TabChargen_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Nur reagieren wenn Tab gewechselt wird (nicht bei anderen SelectionChanged Events)
            if (e.Source != tabChargen) return;

            // Wenn Verfolgung-Tab ausgewaehlt und eine Charge markiert ist
            if (tabChargen.SelectedIndex == 1 && dgChargen.SelectedItem is CoreService.ChargenBestand charge)
            {
                txtVerfolgungChargenNr.Text = charge.CChargenNr;
                LadeVerfolgung_Click(sender, e);
            }
        }

        private void DG_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgChargen.SelectedItem is CoreService.ChargenBestand charge)
            {
                txtVerfolgungChargenNr.Text = charge.CChargenNr;
                tabChargen.SelectedIndex = 1;
                LadeVerfolgung_Click(sender, e);
            }
        }

        private void Verfolgung_Click(object sender, RoutedEventArgs e)
        {
            if (dgChargen.SelectedItem is CoreService.ChargenBestand charge)
            {
                txtVerfolgungChargenNr.Text = charge.CChargenNr;
                tabChargen.SelectedIndex = 1;
                LadeVerfolgung_Click(sender, e);
            }
        }

        private async void LadeVerfolgung_Click(object sender, RoutedEventArgs e)
        {
            var chargenNr = txtVerfolgungChargenNr.Text.Trim();
            if (string.IsNullOrEmpty(chargenNr))
            {
                MessageBox.Show("Bitte Chargennummer eingeben.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                txtStatus.Text = "Lade Chargenverfolgung...";
                var verfolgung = await _core.GetChargenVerfolgungAsync(chargenNr);
                dgVerfolgung.ItemsSource = verfolgung;
                txtStatus.Text = $"Verfolgung fuer Charge {chargenNr} geladen";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Sperren_Click(object sender, RoutedEventArgs e)
        {
            if (dgChargen.SelectedItem is not CoreService.ChargenBestand charge) return;

            var grund = Microsoft.VisualBasic.Interaction.InputBox(
                $"Sperrgrund fuer Charge {charge.CChargenNr}:",
                "Charge sperren",
                "Qualitaetspruefung");

            if (string.IsNullOrEmpty(grund)) return;

            try
            {
                await _core.ChargeSperre(charge.KWarenLagerEingang, grund, null, App.BenutzerId);
                MessageBox.Show($"Charge {charge.CChargenNr} wurde gesperrt!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                await LadeChargenAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Freigeben_Click(object sender, RoutedEventArgs e)
        {
            if (dgChargen.SelectedItem is not CoreService.ChargenBestand charge) return;

            var result = MessageBox.Show(
                $"Charge {charge.CChargenNr} wirklich freigeben?\n\nSperrgrund war: {charge.CSperrgrund}",
                "Charge freigeben",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _core.ChargeFreigabe(charge.KWarenLagerEingang, "Freigabe nach Pruefung", App.BenutzerId);
                MessageBox.Show($"Charge {charge.CChargenNr} wurde freigegeben!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                await LadeChargenAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Quarantaene_Click(object sender, RoutedEventArgs e)
        {
            if (dgChargen.SelectedItem is not CoreService.ChargenBestand charge) return;

            var grund = Microsoft.VisualBasic.Interaction.InputBox(
                $"Grund fuer Quarantaene der Charge {charge.CChargenNr}:",
                "In Quarantaene verschieben",
                "Verdacht auf Qualitaetsmangel");

            if (string.IsNullOrEmpty(grund)) return;

            try
            {
                await _core.ChargeInQuarantaene(charge.KWarenLagerEingang, grund, App.BenutzerId);
                MessageBox.Show($"Charge {charge.CChargenNr} wurde in Quarantaene verschoben!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                await LadeChargenAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void AusQuarantaene_Click(object sender, RoutedEventArgs e)
        {
            if (dgChargen.SelectedItem is not CoreService.ChargenBestand charge) return;

            var result = MessageBox.Show(
                $"Charge {charge.CChargenNr} aus Quarantaene zurueckholen?\n\nSperrgrund war: {charge.CSperrgrund}",
                "Aus Quarantaene",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _core.ChargeAusQuarantaene(charge.KWarenLagerEingang, "Freigabe nach Pruefung", App.BenutzerId);
                MessageBox.Show($"Charge {charge.CChargenNr} wurde aus Quarantaene zurueckgeholt!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                await LadeChargenAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}

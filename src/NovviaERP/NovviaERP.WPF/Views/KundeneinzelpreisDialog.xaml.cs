using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class KundeneinzelpreisDialog : Window
    {
        private readonly CoreService _coreService;
        private readonly int _kArtikel;
        private readonly decimal _standardVkNetto;
        private int? _kKunde;
        private bool _isUpdating = false;
        private const decimal MwstSatz = 1.19m;
        private ObservableCollection<StaffelpreisItem> _staffelpreise = new();

        public class StaffelpreisItem : INotifyPropertyChanged
        {
            private int _nAnzahlAb;
            private decimal _fNettoPreis;
            private decimal _fBrutto;
            private decimal _fProzent;

            public int NAnzahlAb
            {
                get => _nAnzahlAb;
                set { _nAnzahlAb = value; OnPropertyChanged(nameof(NAnzahlAb)); }
            }
            public decimal FNettoPreis
            {
                get => _fNettoPreis;
                set { _fNettoPreis = value; OnPropertyChanged(nameof(FNettoPreis)); UpdateBrutto(); }
            }
            public decimal FBrutto
            {
                get => _fBrutto;
                set { _fBrutto = value; OnPropertyChanged(nameof(FBrutto)); }
            }
            public decimal FProzent
            {
                get => _fProzent;
                set { _fProzent = value; OnPropertyChanged(nameof(FProzent)); }
            }

            private void UpdateBrutto()
            {
                _fBrutto = _fNettoPreis * 1.19m;
                OnPropertyChanged(nameof(FBrutto));
            }

            public event PropertyChangedEventHandler? PropertyChanged;
            protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public KundeneinzelpreisDialog(CoreService coreService, int kArtikel, decimal standardVkNetto, CoreService.KundeneinzelpreisInfo? existing)
        {
            InitializeComponent();
            _coreService = coreService;
            _kArtikel = kArtikel;
            _standardVkNetto = standardVkNetto;

            if (existing != null)
            {
                // Bearbeiten-Modus
                _kKunde = existing.KKunde;
                txtKundeName.Text = $"{existing.CVorname} {existing.CNachname}".Trim();
                if (!string.IsNullOrEmpty(existing.CFirma))
                    txtKundeName.Text = existing.CFirma + " - " + txtKundeName.Text;
                txtKundenNr.Text = existing.CKundenNr;
                btnKundeAuswaehlen.IsEnabled = false;
                txtHeader.Text = $"Preise fuer Kunde '{existing.CNachname}' bearbeiten";

                if (existing.FProzent != 0)
                {
                    rbProzent.IsChecked = true;
                    txtProzent.Text = existing.FProzent.ToString("N2");
                }
                else if (existing.FNettoPreis > 0)
                {
                    rbFestpreis.IsChecked = true;
                    txtNetto.Text = existing.FNettoPreis.ToString("N4");
                    txtBrutto.Text = (existing.FNettoPreis * MwstSatz).ToString("N2");
                }
                else
                {
                    rbStandardPreis.IsChecked = true;
                }
            }
            else
            {
                // Neu-Modus
                rbStandardPreis.IsChecked = true;
                txtNetto.Text = _standardVkNetto.ToString("N4");
                txtBrutto.Text = (_standardVkNetto * MwstSatz).ToString("N2");
            }

            UpdatePreisFields();

            // Staffelpreise DataGrid initialisieren
            dgStaffelpreise.ItemsSource = _staffelpreise;

            // Bei Bearbeitung: Staffelpreise laden
            if (existing != null && existing.KKunde > 0)
            {
                LoadStaffelpreiseAsync(existing.KKunde);
            }
        }

        private async void LoadStaffelpreiseAsync(int kKunde)
        {
            try
            {
                var staffelpreise = await _coreService.GetKundenStaffelpreiseAsync(_kArtikel, kKunde);
                _staffelpreise.Clear();
                foreach (var sp in staffelpreise.Where(s => s.NAnzahlAb > 0))
                {
                    _staffelpreise.Add(new StaffelpreisItem
                    {
                        NAnzahlAb = sp.NAnzahlAb,
                        FNettoPreis = sp.FNettoPreis,
                        FBrutto = sp.FNettoPreis * MwstSatz,
                        FProzent = sp.FProzent
                    });
                }
            }
            catch { }
        }

        private void BtnKundeAuswaehlen_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Dialogs.EntitySucheDialog(Dialogs.EntitySucheDialog.EntityTyp.Kunde);
            dialog.Owner = this;
            if (dialog.ShowDialog() == true && dialog.SelectedId.HasValue)
            {
                _kKunde = dialog.SelectedId.Value;
                txtKundenNr.Text = dialog.SelectedNr ?? "";
                txtKundeName.Text = dialog.SelectedName ?? "";
            }
        }

        private void PreisTyp_Changed(object sender, RoutedEventArgs e)
        {
            UpdatePreisFields();
        }

        private void UpdatePreisFields()
        {
            if (_isUpdating) return;
            _isUpdating = true;

            try
            {
                bool enableManualPrice = rbFestpreis.IsChecked == true;
                bool enableProzent = rbProzent.IsChecked == true;

                txtNetto.IsReadOnly = !enableManualPrice;
                txtBrutto.IsReadOnly = !enableManualPrice;
                txtProzent.IsEnabled = enableProzent;

                if (rbStandardPreis.IsChecked == true)
                {
                    txtNetto.Text = _standardVkNetto.ToString("N4");
                    txtBrutto.Text = (_standardVkNetto * MwstSatz).ToString("N2");
                    txtProzent.Text = "";
                }
                else if (enableProzent && decimal.TryParse(txtProzent.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var prozent))
                {
                    var nettoPreis = _standardVkNetto * (1 - prozent / 100);
                    txtNetto.Text = nettoPreis.ToString("N4");
                    txtBrutto.Text = (nettoPreis * MwstSatz).ToString("N2");
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void TxtProzent_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (rbProzent.IsChecked == true)
            {
                UpdatePreisFields();
            }
        }

        private void TxtNetto_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_isUpdating || rbFestpreis.IsChecked != true) return;
            _isUpdating = true;
            try
            {
                if (decimal.TryParse(txtNetto.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var netto))
                {
                    txtBrutto.Text = (netto * MwstSatz).ToString("N2");
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void TxtBrutto_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_isUpdating || rbFestpreis.IsChecked != true) return;
            _isUpdating = true;
            try
            {
                if (decimal.TryParse(txtBrutto.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var brutto))
                {
                    txtNetto.Text = (brutto / MwstSatz).ToString("N4");
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private async void BtnSpeichern_Click(object sender, RoutedEventArgs e)
        {
            if (!_kKunde.HasValue)
            {
                MessageBox.Show("Bitte waehlen Sie einen Kunden aus.", "Validierung", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                decimal? nettoPreis = null;
                decimal? prozent = null;

                if (rbFestpreis.IsChecked == true)
                {
                    if (decimal.TryParse(txtNetto.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var netto))
                    {
                        nettoPreis = netto;
                    }
                }
                else if (rbProzent.IsChecked == true)
                {
                    if (decimal.TryParse(txtProzent.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var proz))
                    {
                        prozent = proz;
                    }
                }
                // Bei rbStandardPreis bleibt beides null -> Preis wird geloescht

                // Staffelpreise vorbereiten
                var staffelpreiseListe = _staffelpreise
                    .Where(sp => sp.NAnzahlAb > 0)
                    .Select(sp => new CoreService.StaffelpreisDto
                    {
                        NAnzahlAb = sp.NAnzahlAb,
                        FNettoPreis = sp.FNettoPreis,
                        FProzent = sp.FProzent
                    })
                    .ToList();

                await _coreService.SaveKundeneinzelpreisAsync(_kArtikel, _kKunde.Value, nettoPreis, prozent, staffelpreiseListe);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnAbbrechen_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnStaffelAnlegen_Click(object sender, RoutedEventArgs e)
        {
            // Naechste Mengenstufe ermitteln
            int nextMenge = _staffelpreise.Any() ? _staffelpreise.Max(s => s.NAnzahlAb) + 10 : 5;

            _staffelpreise.Add(new StaffelpreisItem
            {
                NAnzahlAb = nextMenge,
                FNettoPreis = _standardVkNetto,
                FBrutto = _standardVkNetto * MwstSatz,
                FProzent = 0
            });
        }

        private void BtnStaffelLoeschen_Click(object sender, RoutedEventArgs e)
        {
            if (dgStaffelpreise.SelectedItem is StaffelpreisItem item)
            {
                _staffelpreise.Remove(item);
            }
        }
    }
}

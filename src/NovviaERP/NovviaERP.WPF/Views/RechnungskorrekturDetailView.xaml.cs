using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;
using NovviaERP.WPF.Controls;

namespace NovviaERP.WPF.Views
{
    public partial class RechnungskorrekturDetailView : UserControl
    {
        private readonly CoreService _core;
        private CoreService.RechnungskorrekturDetail? _korrektur;
        private readonly int _kGutschrift;

        public RechnungskorrekturDetailView(int kGutschrift)
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();
            _kGutschrift = kGutschrift;
            Loaded += async (s, e) => await LadeKorrekturAsync();
        }

        private async System.Threading.Tasks.Task LadeKorrekturAsync()
        {
            try
            {
                // Spalten-Konfiguration
                DataGridColumnConfig.EnableColumnChooser(dgPositionen, "RechnungskorrekturDetailView.Positionen");

                _korrektur = await _core.GetRechnungskorrekturMitPositionenAsync(_kGutschrift);
                if (_korrektur == null)
                {
                    MessageBox.Show("Rechnungskorrektur nicht gefunden.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Header
                txtHeader.Text = $"Rechnungskorrektur {_korrektur.CGutschriftNr}";
                SetTyp(_korrektur.NStornoTyp);
                SetStatus(_korrektur);

                // Korrekturdaten
                txtKorrekturNr.Text = _korrektur.CGutschriftNr;
                txtDatum.Text = _korrektur.DErstellt.ToString("dd.MM.yyyy");
                txtRechnungNr.Text = _korrektur.KRechnung > 0
                    ? (!string.IsNullOrEmpty(_korrektur.CRechnungsnummer) ? _korrektur.CRechnungsnummer : _korrektur.KRechnung.ToString())
                    : "-";
                txtTypInfo.Text = _korrektur.StornoTypName;
                txtKurztext.Text = string.IsNullOrEmpty(_korrektur.CKurzText) ? "-" : _korrektur.CKurzText;

                // Kunde
                txtKundeName.Text = _korrektur.CKundeName;
                txtKundenNr.Text = _korrektur.CKundenNr;
                if (!string.IsNullOrEmpty(_korrektur.CKundeUstId))
                {
                    spUstId.Visibility = Visibility.Visible;
                    txtUstId.Text = _korrektur.CKundeUstId;
                }

                // Betraege
                var mwStBetrag = _korrektur.FPreisBrutto - _korrektur.FPreisNetto;
                txtNetto.Text = $"{_korrektur.FPreisNetto:N2} {_korrektur.CWaehrung}";
                txtMwSt.Text = $"{mwStBetrag:N2} {_korrektur.CWaehrung}";
                txtBrutto.Text = $"{_korrektur.FPreisBrutto:N2} {_korrektur.CWaehrung}";

                // Positionen
                dgPositionen.ItemsSource = _korrektur.Positionen;

                // Storno-Info
                if (_korrektur.NStorno && _korrektur.DStorniert.HasValue)
                {
                    brdStorno.Visibility = Visibility.Visible;
                    txtStornoDatum.Text = _korrektur.DStorniert.Value.ToString("dd.MM.yyyy HH:mm");
                    txtStornoGrund.Text = string.IsNullOrEmpty(_korrektur.CStornogrund) ? "-" : _korrektur.CStornogrund;
                    if (!string.IsNullOrEmpty(_korrektur.CStornoKommentar))
                    {
                        spStornoKommentar.Visibility = Visibility.Visible;
                        txtStornoKommentar.Text = _korrektur.CStornoKommentar;
                    }
                    btnStorno.IsEnabled = false;
                    btnStorno.Content = "Storniert";
                }
                else
                {
                    btnStorno.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetTyp(int stornoTyp)
        {
            Color bgColor;
            string text;

            switch (stornoTyp)
            {
                case 0:
                    text = "Korrektur";
                    bgColor = Colors.DodgerBlue;
                    break;
                case 1:
                    text = "Stornobeleg";
                    bgColor = Colors.Orange;
                    break;
                case 2:
                    text = "Gegen-Storno";
                    bgColor = Colors.Purple;
                    break;
                default:
                    text = "Unbekannt";
                    bgColor = Colors.Gray;
                    break;
            }

            txtTyp.Text = text;
            brdTyp.Background = new SolidColorBrush(bgColor);
        }

        private void SetStatus(CoreService.RechnungskorrekturDetail k)
        {
            string statusText;
            Color bgColor;

            if (k.NStorno)
            {
                statusText = "Storniert";
                bgColor = Colors.Gray;
            }
            else
            {
                statusText = "Aktiv";
                bgColor = Colors.Green;
            }

            txtStatus.Text = statusText;
            brdStatus.Background = new SolidColorBrush(bgColor);
            txtStatus.Foreground = Brushes.White;
        }

        private void Zurueck_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow main)
            {
                // Versuche Zurueck-Navigation, sonst zur Liste
                if (!main.NavigateBack())
                {
                    main.ShowContent(App.Services.GetRequiredService<RechnungskorrekturenView>(), pushToStack: false);
                }
            }
        }

        private void TxtRechnungNr_Click(object sender, MouseButtonEventArgs e)
        {
            if (_korrektur?.KRechnung > 0)
            {
                NavigateToRechnung(_korrektur.KRechnung);
            }
        }

        private void ZurRechnung_Click(object sender, RoutedEventArgs e)
        {
            if (_korrektur?.KRechnung > 0)
            {
                NavigateToRechnung(_korrektur.KRechnung);
            }
            else
            {
                MessageBox.Show("Keine zugehoerige Rechnung vorhanden.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void NavigateToRechnung(int kRechnung)
        {
            try
            {
                var detailView = new RechnungDetailView(kRechnung);
                if (Window.GetWindow(this) is MainWindow main)
                    main.ShowContent(detailView);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Oeffnen der Rechnung:\n\n{ex.Message}",
                    "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TxtKundenNr_Click(object sender, MouseButtonEventArgs e)
        {
            if (_korrektur?.KKunde > 0)
            {
                try
                {
                    var detailView = new KundeDetailView(_korrektur.KKunde);
                    if (Window.GetWindow(this) is MainWindow main)
                        main.ShowContent(detailView);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Oeffnen des Kunden:\n\n{ex.Message}",
                        "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void PDF_Click(object sender, RoutedEventArgs e)
        {
            if (_korrektur == null) return;
            MessageBox.Show($"PDF-Erstellung fuer Rechnungskorrektur {_korrektur.CGutschriftNr} wird noch implementiert.",
                "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Drucken_Click(object sender, RoutedEventArgs e)
        {
            if (_korrektur == null) return;
            MessageBox.Show($"Drucken von Rechnungskorrektur {_korrektur.CGutschriftNr} wird noch implementiert.",
                "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Email_Click(object sender, RoutedEventArgs e)
        {
            if (_korrektur == null) return;
            MessageBox.Show($"E-Mail-Versand fuer Rechnungskorrektur {_korrektur.CGutschriftNr} wird noch implementiert.",
                "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void Storno_Click(object sender, RoutedEventArgs e)
        {
            if (_korrektur == null || _korrektur.NStorno) return;

            var result = MessageBox.Show(
                $"Soll die Rechnungskorrektur {_korrektur.CGutschriftNr} wirklich storniert werden?\n\n" +
                $"Betrag: {_korrektur.FPreisBrutto:N2} {_korrektur.CWaehrung}",
                "Stornieren bestaetigen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                // Kommentar abfragen
                var kommentar = Microsoft.VisualBasic.Interaction.InputBox(
                    "Optionaler Storno-Kommentar:",
                    "Storno-Kommentar",
                    "");

                // Storno via SP ausfuehren
                await _core.StorniereRechnungskorrekturAsync(_korrektur.KGutschrift, App.BenutzerId, kommentar);

                MessageBox.Show($"Rechnungskorrektur {_korrektur.CGutschriftNr} wurde erfolgreich storniert.",
                    "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);

                // Neu laden
                await LadeKorrekturAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Stornieren:\n\n{ex.Message}",
                    "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

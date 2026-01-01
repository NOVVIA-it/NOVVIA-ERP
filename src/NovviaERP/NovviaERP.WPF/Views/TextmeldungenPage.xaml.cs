using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class TextmeldungenPage : UserControl
    {
        private readonly CoreService _core;
        private List<CoreService.Textmeldung>? _textmeldungen;
        private CoreService.Textmeldung? _selectedMeldung;

        public TextmeldungenPage()
        {
            InitializeComponent();
            _core = new CoreService(App.ConnectionString);
            Loaded += async (s, e) => await LadeTextmeldungenAsync();
        }

        private async System.Threading.Tasks.Task LadeTextmeldungenAsync()
        {
            try
            {
                _textmeldungen = await _core.GetTextmeldungenAsync();
                lstTextmeldungen.ItemsSource = _textmeldungen;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Textmeldungen laden: {ex.Message}");
            }
        }

        private async void LstTextmeldungen_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstTextmeldungen.SelectedItem is CoreService.Textmeldung meldung)
            {
                _selectedMeldung = meldung;
                btnTextmeldungLoeschen.IsEnabled = true;

                txtMeldungTitel.Text = meldung.CTitel;
                txtMeldungText.Text = meldung.CText;
                cmbMeldungTyp.SelectedIndex = meldung.NTyp;

                chkMeldungEinkauf.IsChecked = meldung.NBereichEinkauf;
                chkMeldungVerkauf.IsChecked = meldung.NBereichVerkauf;
                chkMeldungStammdaten.IsChecked = meldung.NBereichStammdaten;
                chkMeldungDokumente.IsChecked = meldung.NBereichDokumente;
                chkMeldungOnline.IsChecked = meldung.NBereichOnline;

                chkMeldungAktiv.IsChecked = meldung.NAktiv;
                chkMeldungPopup.IsChecked = meldung.NPopupAnzeigen;

                dpMeldungVon.SelectedDate = meldung.DGueltigVon;
                dpMeldungBis.SelectedDate = meldung.DGueltigBis;

                // Zugewiesene Entitaeten laden
                var entities = await _core.GetTextmeldungEntitiesAsync(meldung.KTextmeldung);
                lstMeldungEntities.ItemsSource = entities;

                txtMeldungStatus.Text = $"Textmeldung ID: {meldung.KTextmeldung}";
                txtMeldungStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray);
            }
        }

        private void TextmeldungNeu_Click(object sender, RoutedEventArgs e)
        {
            _selectedMeldung = null;
            btnTextmeldungLoeschen.IsEnabled = false;

            txtMeldungTitel.Text = "";
            txtMeldungText.Text = "";
            cmbMeldungTyp.SelectedIndex = 0;

            chkMeldungEinkauf.IsChecked = false;
            chkMeldungVerkauf.IsChecked = false;
            chkMeldungStammdaten.IsChecked = true;
            chkMeldungDokumente.IsChecked = false;
            chkMeldungOnline.IsChecked = false;

            chkMeldungAktiv.IsChecked = true;
            chkMeldungPopup.IsChecked = true;

            dpMeldungVon.SelectedDate = null;
            dpMeldungBis.SelectedDate = null;

            lstMeldungEntities.ItemsSource = null;
            txtMeldungTitel.Focus();
            txtMeldungStatus.Text = "Neue Textmeldung";
            txtMeldungStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Blue);
        }

        private async void TextmeldungLoeschen_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedMeldung == null) return;

            var result = MessageBox.Show($"Textmeldung '{_selectedMeldung.CTitel}' wirklich loeschen?\n\n" +
                "Alle Zuweisungen zu Kunden/Artikeln/Lieferanten werden ebenfalls entfernt.",
                "Bestaetigung", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                await _core.DeleteTextmeldungAsync(_selectedMeldung.KTextmeldung);
                await LadeTextmeldungenAsync();
                TextmeldungNeu_Click(sender, e);
                txtMeldungStatus.Text = "Textmeldung geloescht.";
                txtMeldungStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Loeschen:\n{ex.Message}", "Fehler",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void TextmeldungSpeichern_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtMeldungTitel.Text))
            {
                MessageBox.Show("Bitte einen Titel eingeben.", "Hinweis",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtMeldungText.Text))
            {
                MessageBox.Show("Bitte einen Text eingeben.", "Hinweis",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Mindestens ein Bereich muss ausgewaehlt sein
            if (chkMeldungEinkauf.IsChecked != true && chkMeldungVerkauf.IsChecked != true &&
                chkMeldungStammdaten.IsChecked != true && chkMeldungDokumente.IsChecked != true &&
                chkMeldungOnline.IsChecked != true)
            {
                MessageBox.Show("Bitte mindestens einen Bereich auswaehlen.", "Hinweis",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var meldung = new CoreService.Textmeldung
                {
                    CTitel = txtMeldungTitel.Text.Trim(),
                    CText = txtMeldungText.Text.Trim(),
                    NTyp = cmbMeldungTyp.SelectedIndex,
                    NBereichEinkauf = chkMeldungEinkauf.IsChecked == true,
                    NBereichVerkauf = chkMeldungVerkauf.IsChecked == true,
                    NBereichStammdaten = chkMeldungStammdaten.IsChecked == true,
                    NBereichDokumente = chkMeldungDokumente.IsChecked == true,
                    NBereichOnline = chkMeldungOnline.IsChecked == true,
                    NAktiv = chkMeldungAktiv.IsChecked == true,
                    NPopupAnzeigen = chkMeldungPopup.IsChecked == true,
                    DGueltigVon = dpMeldungVon.SelectedDate,
                    DGueltigBis = dpMeldungBis.SelectedDate
                };

                if (_selectedMeldung == null)
                {
                    // Neue Meldung
                    var newId = await _core.CreateTextmeldungAsync(meldung, App.BenutzerId);
                    txtMeldungStatus.Text = $"Textmeldung '{meldung.CTitel}' angelegt (ID: {newId})!";
                }
                else
                {
                    // Bestehende Meldung
                    meldung.KTextmeldung = _selectedMeldung.KTextmeldung;
                    await _core.UpdateTextmeldungAsync(meldung, App.BenutzerId);
                    txtMeldungStatus.Text = $"Textmeldung '{meldung.CTitel}' gespeichert!";
                }

                txtMeldungStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
                await LadeTextmeldungenAsync();
            }
            catch (Exception ex)
            {
                txtMeldungStatus.Text = $"Fehler: {ex.Message}";
                txtMeldungStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
            }
        }
    }
}

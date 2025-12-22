using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Entities;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class EigeneFelderPage : Page
    {
        private readonly EigeneFelderService? _eigeneFelderService;
        private string _aktuellerBereich = "Kunde";
        private List<EigenesFeldDefinition> _felder = new();

        public EigeneFelderPage()
        {
            InitializeComponent();
            _eigeneFelderService = App.Services.GetService<EigeneFelderService>();
            lstBereiche.SelectedIndex = 0;
        }

        private async void Bereich_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (lstBereiche.SelectedItem is ListBoxItem item && item.Tag != null)
            {
                _aktuellerBereich = item.Tag.ToString()!;
                txtBereichTitel.Text = $"Felder: {_aktuellerBereich}";
                await LadeFelderAsync();
            }
        }

        private async System.Threading.Tasks.Task LadeFelderAsync()
        {
            if (_eigeneFelderService == null)
            {
                txtStatus.Text = "EigeneFelderService nicht verfuegbar";
                return;
            }

            try
            {
                _felder = (await _eigeneFelderService.GetFelderAsync(_aktuellerBereich)).ToList();
                dgFelder.ItemsSource = _felder;
                txtStatus.Text = $"{_felder.Count} Felder geladen";
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        private void Feld_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool hasSelection = dgFelder.SelectedItem != null;
            btnBearbeiten.IsEnabled = hasSelection;
            btnLoeschen.IsEnabled = hasSelection;
        }

        private void Feld_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgFelder.SelectedItem is EigenesFeldDefinition feld)
                ZeigeFeldDialog(feld);
        }

        private void NeuesFeld_Click(object sender, RoutedEventArgs e)
        {
            var neuesFeld = new EigenesFeldDefinition
            {
                Bereich = _aktuellerBereich,
                Typ = EigenesFeldTyp.Text
            };
            ZeigeFeldDialog(neuesFeld);
        }

        private void Bearbeiten_Click(object sender, RoutedEventArgs e)
        {
            if (dgFelder.SelectedItem is EigenesFeldDefinition feld)
                ZeigeFeldDialog(feld);
        }

        private async void Loeschen_Click(object sender, RoutedEventArgs e)
        {
            if (dgFelder.SelectedItem is not EigenesFeldDefinition feld) return;

            if (MessageBox.Show($"Feld '{feld.Name}' wirklich loeschen?\n\nAlle gespeicherten Werte gehen verloren!",
                "Loeschen bestaetigen", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                await _eigeneFelderService!.DeleteFeldAsync(feld.Id);
                txtStatus.Text = $"Feld '{feld.Name}' geloescht";
                await LadeFelderAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ZeigeFeldDialog(EigenesFeldDefinition feld)
        {
            var dialog = new Window
            {
                Title = feld.Id == 0 ? "Neues Feld anlegen" : "Feld bearbeiten",
                Width = 450,
                Height = 480,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            int row = 0;
            void AddRow(string label, FrameworkElement control)
            {
                var lbl = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetRow(lbl, row);
                Grid.SetColumn(lbl, 0);
                grid.Children.Add(lbl);

                Grid.SetRow(control, row);
                Grid.SetColumn(control, 1);
                control.Margin = new Thickness(0, 0, 0, 10);
                grid.Children.Add(control);
                row++;
            }

            var txtName = new TextBox { Text = feld.Name ?? "", Height = 28 };
            AddRow("Name:", txtName);

            var txtInternerName = new TextBox { Text = feld.InternerName ?? "", Height = 28 };
            AddRow("Interner Name:", txtInternerName);

            var cmbTyp = new ComboBox { Height = 28 };
            foreach (var typ in Enum.GetValues<EigenesFeldTyp>())
                cmbTyp.Items.Add(typ);
            cmbTyp.SelectedItem = feld.Typ;
            AddRow("Typ:", cmbTyp);

            var txtStandardwert = new TextBox { Text = feld.Standardwert ?? "", Height = 28 };
            AddRow("Standardwert:", txtStandardwert);

            var txtHinweis = new TextBox { Text = feld.Hinweis ?? "", Height = 28 };
            AddRow("Hinweis:", txtHinweis);

            var txtAuswahlWerte = new TextBox { Text = feld.AuswahlWerte ?? "", Height = 28 };
            AddRow("Auswahlwerte:", txtAuswahlWerte);

            var infoText = new TextBlock { Text = "(Trennzeichen: | z.B. Rot|Gruen|Blau)", FontSize = 10, Foreground = System.Windows.Media.Brushes.Gray };
            Grid.SetRow(infoText, row++);
            Grid.SetColumnSpan(infoText, 2);
            infoText.Margin = new Thickness(120, 0, 0, 10);
            grid.Children.Add(infoText);

            var chkPflicht = new CheckBox { Content = "Pflichtfeld", IsChecked = feld.IstPflichtfeld };
            Grid.SetRow(chkPflicht, row++);
            Grid.SetColumnSpan(chkPflicht, 2);
            chkPflicht.Margin = new Thickness(0, 0, 0, 10);
            grid.Children.Add(chkPflicht);

            // Buttons
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnAbbrechen = new Button { Content = "Abbrechen", Padding = new Thickness(15, 8, 15, 8), Margin = new Thickness(0, 0, 10, 0) };
            var btnSpeichern = new Button { Content = "Speichern", Padding = new Thickness(15, 8, 15, 8), Background = System.Windows.Media.Brushes.DodgerBlue, Foreground = System.Windows.Media.Brushes.White };
            btnPanel.Children.Add(btnAbbrechen);
            btnPanel.Children.Add(btnSpeichern);
            Grid.SetRow(btnPanel, 9);
            Grid.SetColumnSpan(btnPanel, 2);
            grid.Children.Add(btnPanel);

            btnAbbrechen.Click += (s, e) => dialog.DialogResult = false;
            btnSpeichern.Click += async (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    MessageBox.Show("Bitte einen Namen eingeben.", "Validierung", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                feld.Name = txtName.Text.Trim();
                feld.InternerName = string.IsNullOrWhiteSpace(txtInternerName.Text) ? txtName.Text.Trim().Replace(" ", "_") : txtInternerName.Text.Trim();
                feld.Typ = (EigenesFeldTyp)cmbTyp.SelectedItem;
                feld.Standardwert = txtStandardwert.Text;
                feld.Hinweis = txtHinweis.Text;
                feld.AuswahlWerte = txtAuswahlWerte.Text;
                feld.IstPflichtfeld = chkPflicht.IsChecked == true;
                feld.Bereich = _aktuellerBereich;

                try
                {
                    if (feld.Id == 0)
                        await _eigeneFelderService!.CreateFeldAsync(feld);
                    else
                        await _eigeneFelderService!.UpdateFeldAsync(feld);
                    dialog.DialogResult = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            dialog.Content = grid;
            if (dialog.ShowDialog() == true)
                await LadeFelderAsync();
        }
    }
}

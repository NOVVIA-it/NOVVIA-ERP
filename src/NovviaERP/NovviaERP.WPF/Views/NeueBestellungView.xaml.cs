using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Data;
using NovviaERP.Core.Entities;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class NeueBestellungView : Window
    {
        private readonly CoreService _core;
        private readonly JtlDbContext _db;
        private CoreService.KundeUebersicht? _selectedKunde;
        private CoreService.ArtikelDetail? _selectedArtikel;
        private ObservableCollection<PositionViewModel> _positionen = new();

        public int? ErstellteBestellungId { get; private set; }

        public NeueBestellungView()
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();
            _db = App.Services.GetRequiredService<JtlDbContext>();
            dgPositionen.ItemsSource = _positionen;
        }

        #region Kunde

        private async void TxtKundeSuche_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) await SucheKundeAsync();
        }

        private async void KundeSuchen_Click(object sender, RoutedEventArgs e) => await SucheKundeAsync();

        private async Task SucheKundeAsync()
        {
            var suche = txtKundeSuche.Text.Trim();
            if (string.IsNullOrEmpty(suche)) return;

            try
            {
                var kunden = (await _core.GetKundenAsync(suche: suche, limit: 10)).ToList();

                if (kunden.Count == 0)
                {
                    MessageBox.Show("Kein Kunde gefunden.", "Suche", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (kunden.Count == 1)
                {
                    SetKunde(kunden[0]);
                }
                else
                {
                    // Auswahldialog
                    var dialog = new KundeAuswahlDialog(kunden);
                    dialog.Owner = this;
                    if (dialog.ShowDialog() == true && dialog.SelectedKunde != null)
                    {
                        SetKunde(dialog.SelectedKunde);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler bei der Kundensuche:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetKunde(CoreService.KundeUebersicht kunde)
        {
            _selectedKunde = kunde;
            txtKundeInfo.Text = $"{kunde.Anzeigename} (Kd-Nr: {kunde.CKundenNr})";
            txtKundeAdresse.Text = kunde.COrt ?? "";
            UpdateSpeichernButton();
        }

        #endregion

        #region Artikel

        private async void TxtArtikelSuche_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) await SucheArtikelAsync();
        }

        private async void ArtikelSuchen_Click(object sender, RoutedEventArgs e) => await SucheArtikelAsync();

        private async Task SucheArtikelAsync()
        {
            var suche = txtArtikelSuche.Text.Trim();
            if (string.IsNullOrEmpty(suche)) return;

            try
            {
                // Erst per Barcode/PZN versuchen
                var artikel = await _core.GetArtikelByBarcodeAsync(suche);

                if (artikel == null)
                {
                    // Dann normale Suche
                    var treffer = (await _core.GetArtikelAsync(suche: suche, limit: 10)).ToList();
                    if (treffer.Count == 0)
                    {
                        txtArtikelInfo.Text = "Nicht gefunden";
                        _selectedArtikel = null;
                        btnHinzufuegen.IsEnabled = false;
                        return;
                    }

                    if (treffer.Count == 1)
                    {
                        artikel = await _core.GetArtikelByIdAsync(treffer[0].KArtikel);
                    }
                    else
                    {
                        // Auswahldialog
                        var dialog = new ArtikelAuswahlDialog(treffer);
                        dialog.Owner = this;
                        if (dialog.ShowDialog() == true && dialog.SelectedArtikelId.HasValue)
                        {
                            artikel = await _core.GetArtikelByIdAsync(dialog.SelectedArtikelId.Value);
                        }
                    }
                }

                if (artikel != null)
                {
                    _selectedArtikel = artikel;
                    txtArtikelInfo.Text = $"{artikel.CArtNr} - {artikel.Name} ({artikel.FVKNetto:N2} EUR)";
                    btnHinzufuegen.IsEnabled = true;
                    txtMenge.Focus();
                    txtMenge.SelectAll();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler bei der Artikelsuche:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ArtikelHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedArtikel == null) return;

            if (!decimal.TryParse(txtMenge.Text, out var menge) || menge <= 0)
            {
                MessageBox.Show("Bitte gueltige Menge eingeben.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Pruefen ob Artikel schon in Liste
            var existing = _positionen.FirstOrDefault(p => p.ArtikelId == _selectedArtikel.KArtikel);
            if (existing != null)
            {
                existing.Menge += menge;
            }
            else
            {
                _positionen.Add(new PositionViewModel
                {
                    ArtikelId = _selectedArtikel.KArtikel,
                    ArtNr = _selectedArtikel.CArtNr ?? "",
                    Name = _selectedArtikel.Name ?? "",
                    Menge = menge,
                    VKNetto = _selectedArtikel.FVKNetto,
                    MwSt = 19 // Standard
                });
            }

            dgPositionen.Items.Refresh();
            UpdateSummen();
            UpdateSpeichernButton();

            // Reset
            txtArtikelSuche.Text = "";
            txtArtikelInfo.Text = "";
            txtMenge.Text = "1";
            _selectedArtikel = null;
            btnHinzufuegen.IsEnabled = false;
            txtArtikelSuche.Focus();
        }

        private void PositionEntfernen_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is PositionViewModel pos)
            {
                _positionen.Remove(pos);
                UpdateSummen();
                UpdateSpeichernButton();
            }
        }

        #endregion

        #region Summen

        private void UpdateSummen()
        {
            var netto = _positionen.Sum(p => p.Gesamt);
            var mwst = netto * 0.19m;
            var brutto = netto + mwst;

            txtSummeNetto.Text = $"{netto:N2} EUR";
            txtMwSt.Text = $"{mwst:N2} EUR";
            txtSummeBrutto.Text = $"{brutto:N2} EUR";
        }

        private void UpdateSpeichernButton()
        {
            btnSpeichern.IsEnabled = _selectedKunde != null && _positionen.Count > 0;
        }

        #endregion

        #region Speichern

        private async void Speichern_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedKunde == null || _positionen.Count == 0) return;

            try
            {
                btnSpeichern.IsEnabled = false;

                var bestellung = new Bestellung
                {
                    KundeId = _selectedKunde.KKunde,
                    Status = 1,
                    Waehrung = "EUR",
                    GesamtNetto = _positionen.Sum(p => p.Gesamt),
                    GesamtBrutto = _positionen.Sum(p => p.Gesamt * 1.19m),
                    Positionen = _positionen.Select(p => new BestellPosition
                    {
                        ArtikelId = p.ArtikelId,
                        ArtNr = p.ArtNr,
                        Name = p.Name,
                        Menge = p.Menge,
                        VKNetto = p.VKNetto,
                        VKBrutto = p.VKNetto * 1.19m,
                        MwSt = p.MwSt
                    }).ToList()
                };

                var id = await _db.CreateBestellungAsync(bestellung);
                ErstellteBestellungId = id;

                MessageBox.Show($"Bestellung wurde angelegt.\n\nBestellnummer: {bestellung.BestellNr}",
                    "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Anlegen:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                btnSpeichern.IsEnabled = true;
            }
        }

        private void Abbrechen_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        #endregion

        #region ViewModel

        public class PositionViewModel
        {
            public int ArtikelId { get; set; }
            public string ArtNr { get; set; } = "";
            public string Name { get; set; } = "";
            public decimal Menge { get; set; }
            public decimal VKNetto { get; set; }
            public decimal MwSt { get; set; }
            public decimal Gesamt => Menge * VKNetto;
        }

        #endregion
    }

    #region Hilfsdialoge

    public class KundeAuswahlDialog : Window
    {
        public CoreService.KundeUebersicht? SelectedKunde { get; private set; }
        private ListBox _list;

        public KundeAuswahlDialog(List<CoreService.KundeUebersicht> kunden)
        {
            Title = "Kunde auswaehlen";
            Width = 500;
            Height = 350;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var grid = new Grid { Margin = new Thickness(15) };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _list = new ListBox
            {
                ItemsSource = kunden,
                DisplayMemberPath = "Anzeigename"
            };
            _list.MouseDoubleClick += (s, e) => Select();
            Grid.SetRow(_list, 0);
            grid.Children.Add(_list);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            var btnCancel = new Button { Content = "Abbrechen", Padding = new Thickness(15, 5, 15, 5), Margin = new Thickness(0, 0, 10, 0) };
            btnCancel.Click += (s, e) => { DialogResult = false; Close(); };
            var btnOk = new Button { Content = "Auswaehlen", Padding = new Thickness(15, 5, 15, 5) };
            btnOk.Click += (s, e) => Select();
            btnPanel.Children.Add(btnCancel);
            btnPanel.Children.Add(btnOk);
            Grid.SetRow(btnPanel, 1);
            grid.Children.Add(btnPanel);

            Content = grid;
        }

        private void Select()
        {
            SelectedKunde = _list.SelectedItem as CoreService.KundeUebersicht;
            if (SelectedKunde != null)
            {
                DialogResult = true;
                Close();
            }
        }
    }

    public class ArtikelAuswahlDialog : Window
    {
        public int? SelectedArtikelId { get; private set; }
        private ListBox _list;

        public ArtikelAuswahlDialog(List<CoreService.ArtikelUebersicht> artikel)
        {
            Title = "Artikel auswaehlen";
            Width = 600;
            Height = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var grid = new Grid { Margin = new Thickness(15) };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _list = new ListBox { ItemsSource = artikel };
            _list.ItemTemplate = CreateArtikelTemplate();
            _list.MouseDoubleClick += (s, e) => Select();
            Grid.SetRow(_list, 0);
            grid.Children.Add(_list);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            var btnCancel = new Button { Content = "Abbrechen", Padding = new Thickness(15, 5, 15, 5), Margin = new Thickness(0, 0, 10, 0) };
            btnCancel.Click += (s, e) => { DialogResult = false; Close(); };
            var btnOk = new Button { Content = "Auswaehlen", Padding = new Thickness(15, 5, 15, 5) };
            btnOk.Click += (s, e) => Select();
            btnPanel.Children.Add(btnCancel);
            btnPanel.Children.Add(btnOk);
            Grid.SetRow(btnPanel, 1);
            grid.Children.Add(btnPanel);

            Content = grid;
        }

        private DataTemplate CreateArtikelTemplate()
        {
            var template = new DataTemplate();
            var factory = new FrameworkElementFactory(typeof(StackPanel));
            factory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);

            var artNr = new FrameworkElementFactory(typeof(TextBlock));
            artNr.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("CArtNr"));
            artNr.SetValue(TextBlock.WidthProperty, 80.0);
            artNr.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);

            var name = new FrameworkElementFactory(typeof(TextBlock));
            name.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Name"));

            factory.AppendChild(artNr);
            factory.AppendChild(name);
            template.VisualTree = factory;
            return template;
        }

        private void Select()
        {
            if (_list.SelectedItem is CoreService.ArtikelUebersicht art)
            {
                SelectedArtikelId = art.KArtikel;
                DialogResult = true;
                Close();
            }
        }
    }

    #endregion
}

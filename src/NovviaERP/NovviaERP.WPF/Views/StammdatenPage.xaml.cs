using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NovviaERP.Core.Entities;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class StammdatenPage : Page
    {
        private readonly StammdatenService _svc;
        private Warenlager? _selectedLager;

        public StammdatenPage()
        {
            InitializeComponent();
            _svc = new StammdatenService(App.Db);
            Loaded += async (s, e) => await LoadAllAsync();
        }

        private async System.Threading.Tasks.Task LoadAllAsync()
        {
            // Firma
            var firma = await _svc.GetFirmaAsync();
            if (firma != null)
            {
                txtFirmaName.Text = firma.Name; txtFirmaStrasse.Text = firma.Strasse; txtFirmaPLZ.Text = firma.PLZ;
                txtFirmaOrt.Text = firma.Ort; txtFirmaTel.Text = firma.Telefon; txtFirmaMail.Text = firma.Email;
                txtFirmaWWW.Text = firma.Website; txtFirmaUStID.Text = firma.UStID; txtFirmaIBAN.Text = firma.IBAN;
                txtFirmaBIC.Text = firma.BIC; txtFirmaBank.Text = firma.Bank; txtFirmaGF.Text = firma.Geschaeftsfuehrer;
                txtFirmaAG.Text = firma.Amtsgericht; txtFirmaHR.Text = firma.Handelsregister;
            }

            // Listen
            await LadeLagerAsync();
            dgKundengruppen.ItemsSource = await _svc.GetKundengruppenAsync();
            dgVersandarten.ItemsSource = await _svc.GetVersandartenAsync(false);
            dgZahlungsarten.ItemsSource = await _svc.GetZahlungsartenAsync(false);
            dgHersteller.ItemsSource = await _svc.GetHerstellerAsync();
            dgLieferanten.ItemsSource = await _svc.GetLieferantenAsync(false);
            dgMerkmale.ItemsSource = await _svc.GetMerkmaleAsync();
            dgMahnstufen.ItemsSource = await _svc.GetMahngruppenAsync();
            dgNummernkreise.ItemsSource = await _svc.GetNummernkreiseAsync();

            // Kategorien TreeView
            var kategorien = await _svc.GetKategorienAsync();
            tvKategorien.Items.Clear();
            foreach (var k in kategorien)
                tvKategorien.Items.Add(CreateTreeItem(k));
        }

        private async System.Threading.Tasks.Task LadeLagerAsync()
        {
            dgLager.ItemsSource = await _svc.GetWarenlagerAsync(false);
            txtSelectedLager.Text = "";
            dgLagerplaetze.ItemsSource = null;
            _selectedLager = null;
        }

        private TreeViewItem CreateTreeItem(Kategorie k)
        {
            var item = new TreeViewItem { Header = k.Beschreibung?.Name ?? $"Kategorie {k.Id}", Tag = k };
            foreach (var sub in k.Unterkategorien)
                item.Items.Add(CreateTreeItem(sub));
            return item;
        }

        private async void FirmaSpeichern_Click(object s, RoutedEventArgs e)
        {
            var firma = new Firma
            {
                Id = 1, Name = txtFirmaName.Text, Strasse = txtFirmaStrasse.Text, PLZ = txtFirmaPLZ.Text,
                Ort = txtFirmaOrt.Text, Telefon = txtFirmaTel.Text, Email = txtFirmaMail.Text,
                Website = txtFirmaWWW.Text, UStID = txtFirmaUStID.Text, IBAN = txtFirmaIBAN.Text,
                BIC = txtFirmaBIC.Text, Bank = txtFirmaBank.Text, Geschaeftsfuehrer = txtFirmaGF.Text,
                Amtsgericht = txtFirmaAG.Text, Handelsregister = txtFirmaHR.Text
            };
            await _svc.UpdateFirmaAsync(firma);
            MessageBox.Show("Firmendaten gespeichert!");
        }

        #region Warenlager CRUD

        private async void Lager_SelectionChanged(object s, SelectionChangedEventArgs e)
        {
            if (dgLager.SelectedItem is Warenlager lager)
            {
                _selectedLager = lager;
                txtSelectedLager.Text = $"Lagerplätze für: {lager.Name}";
                dgLagerplaetze.ItemsSource = await _svc.GetLagerplaetzeAsync(lager.Id);
            }
        }

        private void Lager_DoubleClick(object s, MouseButtonEventArgs e)
        {
            if (dgLager.SelectedItem is Warenlager)
                LagerBearbeiten_Click(s, e);
        }

        private async void NeuesLager_Click(object s, RoutedEventArgs e)
        {
            var dialog = new LagerDialog();
            if (dialog.ShowDialog() == true && dialog.Lager != null)
            {
                await _svc.CreateWarenlagerAsync(dialog.Lager);
                await LadeLagerAsync();
            }
        }

        private async void LagerBearbeiten_Click(object s, RoutedEventArgs e)
        {
            if (dgLager.SelectedItem is not Warenlager lager)
            {
                MessageBox.Show("Bitte ein Lager auswählen.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new LagerDialog(lager);
            if (dialog.ShowDialog() == true && dialog.Lager != null)
            {
                await _svc.UpdateWarenlagerAsync(dialog.Lager);
                await LadeLagerAsync();
            }
        }

        private async void LagerLoeschen_Click(object s, RoutedEventArgs e)
        {
            if (dgLager.SelectedItem is not Warenlager lager)
            {
                MessageBox.Show("Bitte ein Lager auswählen.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show($"Lager '{lager.Name}' wirklich löschen?", "Löschen bestätigen",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                await _svc.DeleteWarenlagerAsync(lager.Id);
                await LadeLagerAsync();
            }
        }

        #endregion

        #region Lagerplatz CRUD

        private void Lagerplatz_DoubleClick(object s, MouseButtonEventArgs e)
        {
            if (dgLagerplaetze.SelectedItem is Lagerplatz)
                PlatzBearbeiten_Click(s, e);
        }

        private async void NeuerPlatz_Click(object s, RoutedEventArgs e)
        {
            if (_selectedLager == null)
            {
                MessageBox.Show("Bitte zuerst ein Lager auswählen.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new LagerplatzDialog(_selectedLager.Id);
            if (dialog.ShowDialog() == true && dialog.Lagerplatz != null)
            {
                await _svc.CreateLagerplatzAsync(dialog.Lagerplatz);
                dgLagerplaetze.ItemsSource = await _svc.GetLagerplaetzeAsync(_selectedLager.Id);
            }
        }

        private async void PlatzBearbeiten_Click(object s, RoutedEventArgs e)
        {
            if (dgLagerplaetze.SelectedItem is not Lagerplatz platz)
            {
                MessageBox.Show("Bitte einen Lagerplatz auswählen.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new LagerplatzDialog(platz.WarenlagerId, platz);
            if (dialog.ShowDialog() == true && dialog.Lagerplatz != null)
            {
                await _svc.UpdateLagerplatzAsync(dialog.Lagerplatz);
                if (_selectedLager != null)
                    dgLagerplaetze.ItemsSource = await _svc.GetLagerplaetzeAsync(_selectedLager.Id);
            }
        }

        private async void PlatzLoeschen_Click(object s, RoutedEventArgs e)
        {
            if (dgLagerplaetze.SelectedItem is not Lagerplatz platz)
            {
                MessageBox.Show("Bitte einen Lagerplatz auswählen.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show($"Lagerplatz '{platz.Name}' wirklich löschen?", "Löschen bestätigen",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                await _svc.DeleteLagerplatzAsync(platz.Id);
                if (_selectedLager != null)
                    dgLagerplaetze.ItemsSource = await _svc.GetLagerplaetzeAsync(_selectedLager.Id);
            }
        }

        #endregion

        private void Merkmal_SelectionChanged(object s, SelectionChangedEventArgs e)
        {
            if (dgMerkmale.SelectedItem is Merkmal merkmal)
                dgMerkmalWerte.ItemsSource = merkmal.Werte;
        }

        private void NeueKategorie_Click(object s, RoutedEventArgs e) => MessageBox.Show("Dialog für neue Kategorie öffnen");
        private void NeueKundengruppe_Click(object s, RoutedEventArgs e) => MessageBox.Show("Dialog für neue Kundengruppe öffnen");
        private void NeueVersandart_Click(object s, RoutedEventArgs e) => MessageBox.Show("Dialog für neue Versandart öffnen");
        private void NeuerHersteller_Click(object s, RoutedEventArgs e) => MessageBox.Show("Dialog für neuen Hersteller öffnen");
        private void NeuerLieferant_Click(object s, RoutedEventArgs e) => MessageBox.Show("Dialog für neuen Lieferanten öffnen");
    }

    /// <summary>
    /// Dialog für Warenlager anlegen/bearbeiten
    /// </summary>
    public class LagerDialog : Window
    {
        private readonly TextBox txtName = new() { Margin = new Thickness(0, 0, 0, 10) };
        private readonly TextBox txtKuerzel = new() { Margin = new Thickness(0, 0, 0, 10) };
        private readonly TextBox txtOrt = new() { Margin = new Thickness(0, 0, 0, 10) };
        private readonly CheckBox chkStandard = new() { Content = "Standard-Lager", Margin = new Thickness(0, 0, 0, 15) };

        public Warenlager? Lager { get; private set; }

        public LagerDialog(Warenlager? lager = null)
        {
            Title = lager == null ? "Neues Warenlager" : "Warenlager bearbeiten";
            Width = 400; Height = 280;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Owner = Application.Current.MainWindow;
            ResizeMode = ResizeMode.NoResize;

            if (lager != null)
            {
                txtName.Text = lager.Name;
                txtKuerzel.Text = lager.Kuerzel;
                txtOrt.Text = lager.Ort;
                chkStandard.IsChecked = lager.IstStandard;
            }

            var stack = new StackPanel { Margin = new Thickness(20) };
            stack.Children.Add(new TextBlock { Text = "Name:", FontWeight = FontWeights.SemiBold });
            stack.Children.Add(txtName);
            stack.Children.Add(new TextBlock { Text = "Kürzel:", FontWeight = FontWeights.SemiBold });
            stack.Children.Add(txtKuerzel);
            stack.Children.Add(new TextBlock { Text = "Ort:", FontWeight = FontWeights.SemiBold });
            stack.Children.Add(txtOrt);
            stack.Children.Add(chkStandard);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnOk = new Button { Content = "Speichern", Width = 100, IsDefault = true, Background = System.Windows.Media.Brushes.DodgerBlue, Foreground = System.Windows.Media.Brushes.White };
            var btnCancel = new Button { Content = "Abbrechen", Width = 100, Margin = new Thickness(10, 0, 0, 0), IsCancel = true };
            btnOk.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    MessageBox.Show("Bitte Name eingeben.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                Lager = new Warenlager
                {
                    Id = lager?.Id ?? 0,
                    Name = txtName.Text.Trim(),
                    Kuerzel = txtKuerzel.Text.Trim(),
                    Ort = txtOrt.Text.Trim(),
                    IstStandard = chkStandard.IsChecked == true
                };
                DialogResult = true;
            };
            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);
            stack.Children.Add(btnPanel);

            Content = stack;
        }
    }

    /// <summary>
    /// Dialog für Lagerplatz anlegen/bearbeiten
    /// </summary>
    public class LagerplatzDialog : Window
    {
        private readonly TextBox txtName = new() { Margin = new Thickness(0, 0, 0, 10) };
        private readonly TextBox txtRegal = new() { Margin = new Thickness(0, 0, 0, 10) };
        private readonly TextBox txtFach = new() { Margin = new Thickness(0, 0, 0, 10) };
        private readonly TextBox txtBarcode = new() { Margin = new Thickness(0, 0, 0, 15) };
        private readonly int _lagerId;

        public Lagerplatz? Lagerplatz { get; private set; }

        public LagerplatzDialog(int lagerId, Lagerplatz? platz = null)
        {
            _lagerId = lagerId;
            Title = platz == null ? "Neuer Lagerplatz" : "Lagerplatz bearbeiten";
            Width = 400; Height = 320;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Owner = Application.Current.MainWindow;
            ResizeMode = ResizeMode.NoResize;

            if (platz != null)
            {
                txtName.Text = platz.Name;
                txtRegal.Text = platz.Regal;
                txtFach.Text = platz.Fach;
                txtBarcode.Text = platz.Barcode;
            }

            var stack = new StackPanel { Margin = new Thickness(20) };
            stack.Children.Add(new TextBlock { Text = "Name:", FontWeight = FontWeights.SemiBold });
            stack.Children.Add(txtName);
            stack.Children.Add(new TextBlock { Text = "Regal:", FontWeight = FontWeights.SemiBold });
            stack.Children.Add(txtRegal);
            stack.Children.Add(new TextBlock { Text = "Fach:", FontWeight = FontWeights.SemiBold });
            stack.Children.Add(txtFach);
            stack.Children.Add(new TextBlock { Text = "Barcode:", FontWeight = FontWeights.SemiBold });
            stack.Children.Add(txtBarcode);

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var btnOk = new Button { Content = "Speichern", Width = 100, IsDefault = true, Background = System.Windows.Media.Brushes.DodgerBlue, Foreground = System.Windows.Media.Brushes.White };
            var btnCancel = new Button { Content = "Abbrechen", Width = 100, Margin = new Thickness(10, 0, 0, 0), IsCancel = true };
            btnOk.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtName.Text))
                {
                    MessageBox.Show("Bitte Name eingeben.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                Lagerplatz = new Lagerplatz
                {
                    Id = platz?.Id ?? 0,
                    WarenlagerId = _lagerId,
                    Name = txtName.Text.Trim(),
                    Regal = txtRegal.Text.Trim(),
                    Fach = txtFach.Text.Trim(),
                    Barcode = txtBarcode.Text.Trim()
                };
                DialogResult = true;
            };
            btnPanel.Children.Add(btnOk);
            btnPanel.Children.Add(btnCancel);
            stack.Children.Add(btnPanel);

            Content = stack;
        }
    }
}

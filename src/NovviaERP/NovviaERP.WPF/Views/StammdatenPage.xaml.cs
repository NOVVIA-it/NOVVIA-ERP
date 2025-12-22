using System.Windows;
using System.Windows.Controls;
using NovviaERP.Core.Entities;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class StammdatenPage : Page
    {
        private readonly StammdatenService _svc;

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
            dgLager.ItemsSource = await _svc.GetWarenlagerAsync(false);
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

        private async void Lager_SelectionChanged(object s, SelectionChangedEventArgs e)
        {
            if (dgLager.SelectedItem is Warenlager lager)
                dgLagerplaetze.ItemsSource = await _svc.GetLagerplaetzeAsync(lager.Id);
        }

        private async void Merkmal_SelectionChanged(object s, SelectionChangedEventArgs e)
        {
            if (dgMerkmale.SelectedItem is Merkmal merkmal)
                dgMerkmalWerte.ItemsSource = merkmal.Werte;
        }

        private void NeuesLager_Click(object s, RoutedEventArgs e) => MessageBox.Show("Dialog für neues Lager öffnen");
        private void NeuerPlatz_Click(object s, RoutedEventArgs e) => MessageBox.Show("Dialog für neuen Lagerplatz öffnen");
        private void NeueKategorie_Click(object s, RoutedEventArgs e) => MessageBox.Show("Dialog für neue Kategorie öffnen");
        private void NeueKundengruppe_Click(object s, RoutedEventArgs e) => MessageBox.Show("Dialog für neue Kundengruppe öffnen");
        private void NeueVersandart_Click(object s, RoutedEventArgs e) => MessageBox.Show("Dialog für neue Versandart öffnen");
        private void NeuerHersteller_Click(object s, RoutedEventArgs e) => MessageBox.Show("Dialog für neuen Hersteller öffnen");
        private void NeuerLieferant_Click(object s, RoutedEventArgs e) => MessageBox.Show("Dialog für neuen Lieferanten öffnen");
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class ArtikelDetailView : UserControl
    {
        private readonly CoreService _coreService;
        private readonly int? _artikelId;

        public ArtikelDetailView(int? artikelId)
        {
            InitializeComponent();
            _artikelId = artikelId;
            _coreService = App.Services.GetRequiredService<CoreService>();
            txtTitel.Text = artikelId.HasValue ? "Artikel bearbeiten" : "Neuer Artikel";
            Loaded += async (s, e) => await LadeArtikelAsync();
        }

        private async System.Threading.Tasks.Task LadeArtikelAsync()
        {
            if (!_artikelId.HasValue)
            {
                txtStatus.Text = "Neuer Artikel - bitte Daten eingeben";
                return;
            }

            try
            {
                var artikel = await _coreService.GetArtikelByIdAsync(_artikelId.Value);
                if (artikel == null)
                {
                    txtStatus.Text = "Artikel nicht gefunden";
                    return;
                }

                txtStatus.Text = "";
                pnlInhalt.Children.Clear();

                // Einfache Anzeige der Artikeldaten
                AddField("Art-Nr:", artikel.CArtNr);
                AddField("Name:", artikel.Name);
                AddField("Barcode:", artikel.CBarcode ?? "-");
                AddField("VK Netto:", $"{artikel.FVKNetto:N2} EUR");
                AddField("EK Netto:", $"{artikel.FEKNetto:N2} EUR");
                AddField("Bestand:", $"{artikel.NLagerbestand:N0}");
                AddField("Mindestbestand:", $"{artikel.NMidestbestand:N0}");
                AddField("Aktiv:", artikel.CAktiv == "Y" ? "Ja" : "Nein");
            }
            catch (Exception ex)
            {
                txtStatus.Text = $"Fehler: {ex.Message}";
            }
        }

        private void AddField(string label, string value)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };
            sp.Children.Add(new TextBlock { Text = label, Width = 120, FontWeight = FontWeights.SemiBold });
            sp.Children.Add(new TextBlock { Text = value ?? "-" });
            pnlInhalt.Children.Add(sp);
        }

        private void Zurueck_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow main)
            {
                main.ShowContent(App.Services.GetRequiredService<ArtikelView>());
            }
        }

        private void Speichern_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Speichern noch nicht implementiert", "Info");
        }
    }
}

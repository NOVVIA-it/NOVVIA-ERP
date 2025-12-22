using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class KundeDetailView : UserControl
    {
        private readonly CoreService _coreService;
        private readonly int? _kundeId;

        public KundeDetailView(int? kundeId)
        {
            InitializeComponent();
            _kundeId = kundeId;
            _coreService = App.Services.GetRequiredService<CoreService>();
            txtTitel.Text = kundeId.HasValue ? "Kunde bearbeiten" : "Neuer Kunde";
            Loaded += async (s, e) => await LadeKundeAsync();
        }

        private async System.Threading.Tasks.Task LadeKundeAsync()
        {
            if (!_kundeId.HasValue) { txtStatus.Text = "Neuer Kunde"; return; }

            try
            {
                var kunde = await _coreService.GetKundeByIdAsync(_kundeId.Value);
                if (kunde == null) { txtStatus.Text = "Nicht gefunden"; return; }

                txtStatus.Text = "";
                pnlInhalt.Children.Clear();
                AddField("Kd-Nr:", kunde.CKundenNr);
                var adr = kunde.StandardAdresse;
                AddField("Firma:", adr?.CFirma);
                AddField("Anrede:", adr?.CAnrede);
                AddField("Vorname:", adr?.CVorname);
                AddField("Nachname:", adr?.CName);
                AddField("Strasse:", adr?.CStrasse);
                AddField("PLZ/Ort:", $"{adr?.CPLZ} {adr?.COrt}");
                AddField("E-Mail:", adr?.CMail);
                AddField("Telefon:", adr?.CTel);
            }
            catch (Exception ex) { txtStatus.Text = $"Fehler: {ex.Message}"; }
        }

        private void AddField(string label, string? value)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 5) };
            sp.Children.Add(new TextBlock { Text = label, Width = 100, FontWeight = FontWeights.SemiBold });
            sp.Children.Add(new TextBlock { Text = value ?? "-" });
            pnlInhalt.Children.Add(sp);
        }

        private void Zurueck_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow main)
                main.ShowContent(App.Services.GetRequiredService<KundenView>());
        }

        private void Speichern_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Speichern noch nicht implementiert", "Info");
        }
    }
}

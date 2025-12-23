using System;
using System.Collections.Generic;
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
            Loaded += async (s, e) =>
            {
                await LadeKundeAsync();
                await LadeValidierungAsync();
            };
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

        #region Validierung (JTL Eigene Felder)

        private async System.Threading.Tasks.Task LadeValidierungAsync()
        {
            if (!_kundeId.HasValue) return;

            try
            {
                var felder = await _coreService.GetKundeEigeneFelderAsync(_kundeId.Value);

                chkValAmbient.IsChecked = GetBoolWert(felder, "Ambient");
                chkValCool.IsChecked = GetBoolWert(felder, "Cool");
                chkValMedcan.IsChecked = GetBoolWert(felder, "Medcan");
                chkValTierarznei.IsChecked = GetBoolWert(felder, "Tierarznei");

                if (felder.TryGetValue("QualifiziertAm", out var qualDatum) && DateTime.TryParse(qualDatum, out var dt))
                {
                    dpQualifiziertAm.SelectedDate = dt;
                }

                txtQualifiziertVon.Text = felder.TryGetValue("QualifiziertVon", out var qualVon) ? qualVon ?? "" : "";
                txtGDP.Text = felder.TryGetValue("GDP", out var gdp) ? gdp ?? "" : "";
                txtGMP.Text = felder.TryGetValue("GMP", out var gmp) ? gmp ?? "" : "";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Laden der Validierungsfelder: {ex.Message}");
            }
        }

        private bool GetBoolWert(Dictionary<string, string?> felder, string key)
        {
            if (felder.TryGetValue(key, out var wert))
            {
                return wert == "1" || wert?.ToLower() == "true" || wert?.ToLower() == "ja";
            }
            return false;
        }

        private async void ValidierungSpeichern_Click(object sender, RoutedEventArgs e)
        {
            if (!_kundeId.HasValue)
            {
                MessageBox.Show("Bitte zuerst den Kunden speichern!", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var felder = new Dictionary<string, string?>
                {
                    ["Ambient"] = chkValAmbient.IsChecked == true ? "1" : "0",
                    ["Cool"] = chkValCool.IsChecked == true ? "1" : "0",
                    ["Medcan"] = chkValMedcan.IsChecked == true ? "1" : "0",
                    ["Tierarznei"] = chkValTierarznei.IsChecked == true ? "1" : "0",
                    ["QualifiziertAm"] = dpQualifiziertAm.SelectedDate?.ToString("yyyy-MM-dd"),
                    ["QualifiziertVon"] = txtQualifiziertVon.Text.Trim(),
                    ["GDP"] = txtGDP.Text.Trim(),
                    ["GMP"] = txtGMP.Text.Trim()
                };

                await _coreService.SetKundeEigeneFelderAsync(_kundeId.Value, felder);
                MessageBox.Show("Validierungsfelder wurden gespeichert!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        private void Zurueck_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow main)
                main.ShowContent(App.Services.GetRequiredService<KundenView>());
        }
    }
}

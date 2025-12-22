using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class AusgabeDialog : Window
    {
        private readonly AusgabeService _ausgabeService;
        private readonly EmailVorlageService _emailService;
        private readonly DokumentTyp _dokumentTyp;
        private readonly int _dokumentId;

        public AusgabeDialog(AusgabeService ausgabe, EmailVorlageService email, DokumentTyp typ, int id)
        {
            _ausgabeService = ausgabe;
            _emailService = email;
            _dokumentTyp = typ;
            _dokumentId = id;
            
            InitializeComponent();
            Loaded += async (s, e) => await LoadDataAsync();
        }

        private async Task LoadDataAsync()
        {
            txtTitel.Text = $"📄 {_dokumentTyp} ausgeben";
            txtDokumentInfo.Text = $"Dokument-ID: {_dokumentId}";
            
            // Drucker laden
            foreach (string printer in PrinterSettings.InstalledPrinters)
            {
                cbDrucker.Items.Add(printer);
            }
            if (cbDrucker.Items.Count > 0)
                cbDrucker.SelectedIndex = 0;
            
            // E-Mail-Vorlagen laden
            var vorlagen = await _emailService.GetVorlagenAsync(_dokumentTyp.ToString());
            cbEmailVorlage.ItemsSource = vorlagen;
            cbEmailVorlage.DisplayMemberPath = "Name";
            if (vorlagen.Any())
                cbEmailVorlage.SelectedIndex = 0;
            
            // Standard-Speicherpfad
            txtSpeicherPfad.Text = $@"C:\NOVVIA\Dokumente\{_dokumentTyp}\{_dokumentTyp}_{_dokumentId}.pdf";
        }

        private void BtnPfadWaehlen_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "PDF-Dateien|*.pdf",
                FileName = $"{_dokumentTyp}_{_dokumentId}.pdf"
            };

            if (dialog.ShowDialog() == true)
            {
                txtSpeicherPfad.Text = dialog.FileName;
            }
        }

        private async void BtnAusfuehren_Click(object sender, RoutedEventArgs e)
        {
            var aktionen = new List<AusgabeAktion>();
            
            if (chkVorschau.IsChecked == true) aktionen.Add(AusgabeAktion.Vorschau);
            if (chkDrucken.IsChecked == true) aktionen.Add(AusgabeAktion.Drucken);
            if (chkSpeichern.IsChecked == true) aktionen.Add(AusgabeAktion.Speichern);
            if (chkEmail.IsChecked == true) aktionen.Add(AusgabeAktion.EMail);
            
            if (!aktionen.Any())
            {
                MessageBox.Show("Bitte mindestens eine Aktion auswählen.", "Hinweis", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            txtStatus.Text = "Verarbeite...";
            
            try
            {
                var anfrage = new AusgabeAnfrage
                {
                    DokumentTyp = _dokumentTyp,
                    DokumentId = _dokumentId,
                    Aktionen = aktionen,
                    DruckerName = cbDrucker.SelectedItem?.ToString(),
                    Kopien = int.TryParse(txtKopien.Text, out var k) ? k : 1,
                    SpeicherPfad = txtSpeicherPfad.Text,
                    Archivieren = chkArchivieren.IsChecked == true,
                    EmpfaengerEmail = txtEmpfaenger.Text,
                    EmailVorlageId = (cbEmailVorlage.SelectedItem as EmailVorlageErweitert)?.Id,
                    EmailVorschau = chkEmailVorschau.IsChecked == true
                };

                var ergebnis = await _ausgabeService.AusgabeAsync(anfrage);

                var msg = "Ausgabe abgeschlossen:\n";
                if (ergebnis.VorschauAngezeigt) msg += "✓ Vorschau angezeigt\n";
                if (ergebnis.Gedruckt) msg += $"✓ Gedruckt ({anfrage.Kopien} Kopien)\n";
                if (ergebnis.Gespeichert) msg += $"✓ Gespeichert: {anfrage.SpeicherPfad}\n";
                if (ergebnis.EmailGesendet) msg += "✓ E-Mail gesendet\n";

                if (ergebnis.Fehler.Any())
                {
                    msg += "\nFehler:\n" + string.Join("\n", ergebnis.Fehler);
                    MessageBox.Show(msg, "Ausgabe mit Fehlern", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                else
                {
                    MessageBox.Show(msg, "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                txtStatus.Text = "";
            }
        }
    }
}

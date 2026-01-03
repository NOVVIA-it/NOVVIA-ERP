using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class RetoureDetailView : UserControl
    {
        private readonly CoreService _core;
        private CoreService.RetoureDetail? _retoure;
        private List<CoreService.RMStatusItem> _statusListe = new();
        private int _kRMRetoure;

        public RetoureDetailView()
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();
        }

        public void LoadRetoure(int kRMRetoure)
        {
            _kRMRetoure = kRMRetoure;
            Loaded += async (s, e) => await LoadAsync();
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            try
            {
                // Status-Liste laden
                _statusListe = await _core.GetRMStatusListeAsync();
                cmbStatus.ItemsSource = _statusListe;

                // Retoure laden
                _retoure = await _core.GetRetoureByIdAsync(_kRMRetoure);
                if (_retoure == null)
                {
                    MessageBox.Show("Retoure nicht gefunden.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                    BtnZurueck_Click(this, new RoutedEventArgs());
                    return;
                }

                // Daten anzeigen
                txtTitel.Text = $"Retoure {_retoure.CRetoureNr}";
                txtStatus.Text = _retoure.CStatus ?? "";
                txtRetoureNr.Text = _retoure.CRetoureNr;
                cmbStatus.SelectedItem = _statusListe.FirstOrDefault(s => s.KRMStatus == _retoure.KRMStatus);
                txtErstellt.Text = _retoure.DErstellt.ToString("dd.MM.yyyy HH:mm");
                txtBearbeiter.Text = _retoure.CBenutzer ?? "-";
                txtWarenlager.Text = _retoure.CWarenlager ?? "-";
                txtAnsprechpartner.Text = _retoure.CAnsprechpartner ?? "-";

                txtKundenNr.Text = _retoure.CKundenNr ?? "-";
                txtKundeName.Text = _retoure.CKundeName ?? "-";
                txtAuftragNr.Text = _retoure.CAuftragNr ?? "-";
                txtGutschriftNr.Text = _retoure.CGutschriftNr ?? "-";
                txtKorrekturBetrag.Text = _retoure.FKorrekturBetrag.ToString("N2") + " EUR";
                chkVersandkosten.IsChecked = _retoure.NVersandkostenErstatten;

                txtKommentarExtern.Text = _retoure.CKommentarExtern ?? "";
                txtKommentarIntern.Text = _retoure.CKommentarIntern ?? "";

                // Positionen
                dgPositionen.ItemsSource = _retoure.Positionen;

                // Gutschrift-Button deaktivieren wenn bereits Gutschrift vorhanden
                btnGutschrift.IsEnabled = !_retoure.KGutschrift.HasValue || _retoure.KGutschrift == 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnZurueck_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainWindow main)
            {
                if (!main.NavigateBack())
                {
                    var retourenView = App.Services.GetRequiredService<RetourenView>();
                    main.ShowContent(retourenView, false);
                }
            }
        }

        private async void BtnSpeichern_Click(object sender, RoutedEventArgs e)
        {
            if (_retoure == null) return;

            try
            {
                // Status speichern
                if (cmbStatus.SelectedItem is CoreService.RMStatusItem status)
                {
                    await _core.UpdateRetoureStatusAsync(_retoure.KRMRetoure, status.KRMStatus);
                }

                // Kommentare speichern
                await _core.UpdateRetoureKommentareAsync(_retoure.KRMRetoure, txtKommentarExtern.Text, txtKommentarIntern.Text);

                MessageBox.Show("Aenderungen gespeichert.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnGutschrift_Click(object sender, RoutedEventArgs e)
        {
            if (_retoure == null || !_retoure.KBestellung.HasValue)
            {
                MessageBox.Show("Kein Auftrag zugeordnet - Gutschrift kann nicht erstellt werden.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show("Moechten Sie eine Gutschrift fuer diese Retoure erstellen?",
                "Gutschrift erstellen", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                // Hier kann RM.spGutschriftErstellen aufgerufen werden
                // Vorerst nur Hinweis anzeigen
                MessageBox.Show("Gutschrift-Erstellung wird implementiert.\n\nDie JTL-SP 'RM.spGutschriftErstellen' kann verwendet werden.",
                    "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void KundenNr_Click(object sender, MouseButtonEventArgs e)
        {
            if (_retoure == null || _retoure.KKunde == 0) return;

            var detailView = new KundeDetailView(_retoure.KKunde);

            if (Window.GetWindow(this) is MainWindow main)
            {
                main.ShowContent(detailView);
            }
        }

        private void AuftragNr_Click(object sender, MouseButtonEventArgs e)
        {
            if (_retoure == null || !_retoure.KBestellung.HasValue) return;

            var detailView = App.Services.GetRequiredService<BestellungDetailView>();
            detailView.LadeBestellung(_retoure.KBestellung.Value);

            if (Window.GetWindow(this) is MainWindow main)
            {
                main.ShowContent(detailView);
            }
        }

        private void GutschriftNr_Click(object sender, MouseButtonEventArgs e)
        {
            if (_retoure == null || !_retoure.KGutschrift.HasValue || _retoure.KGutschrift == 0) return;

            // TODO: GutschriftDetailView oeffnen wenn vorhanden
            MessageBox.Show($"Gutschrift {_retoure.CGutschriftNr} oeffnen", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}

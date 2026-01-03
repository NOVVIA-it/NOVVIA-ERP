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
    public partial class RetourenView : UserControl
    {
        private readonly CoreService _core;
        private List<CoreService.RetoureUebersicht> _retouren = new();
        private List<CoreService.RMStatusItem> _statusListe = new();
        private int? _statusFilter;

        public RetourenView()
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();
            Loaded += async (s, e) => await LoadAsync();
        }

        /// <summary>
        /// Status-Filter von aussen setzen (z.B. von MainWindow Sidebar)
        /// </summary>
        public void SetStatusFilter(string status)
        {
            if (int.TryParse(status, out int kRMStatus))
                _statusFilter = kRMStatus;
            else
                _statusFilter = null;
        }

        private async System.Threading.Tasks.Task LoadAsync()
        {
            try
            {
                // Status-Liste laden
                _statusListe = await _core.GetRMStatusListeAsync();
                var alleStatus = new List<CoreService.RMStatusItem> { new() { KRMStatus = 0, CName = "Alle" } };
                alleStatus.AddRange(_statusListe);
                cmbStatus.ItemsSource = alleStatus;

                // Status-Filter setzen falls vorhanden
                if (_statusFilter.HasValue)
                {
                    var match = alleStatus.FirstOrDefault(s => s.KRMStatus == _statusFilter.Value);
                    cmbStatus.SelectedItem = match ?? alleStatus[0];
                }
                else
                {
                    cmbStatus.SelectedIndex = 0;
                }

                // Retouren laden
                await LadeRetourenAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async System.Threading.Tasks.Task LadeRetourenAsync()
        {
            try
            {
                int? statusId = null;
                if (cmbStatus.SelectedItem is CoreService.RMStatusItem status && status.KRMStatus > 0)
                    statusId = status.KRMStatus;

                var suche = string.IsNullOrWhiteSpace(txtSuche.Text) ? null : txtSuche.Text.Trim();

                _retouren = await _core.GetRetourenAsync(statusId, suche);
                gridRetouren.ItemsSource = _retouren;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CmbStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            await LadeRetourenAsync();
        }

        private async void TxtSuche_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                await LadeRetourenAsync();
        }

        private async void BtnSuchen_Click(object sender, RoutedEventArgs e)
        {
            await LadeRetourenAsync();
        }

        private async void BtnAktualisieren_Click(object sender, RoutedEventArgs e)
        {
            await LadeRetourenAsync();
        }

        private void GridRetouren_ItemDoubleClick(object? sender, object? item)
        {
            if (item is CoreService.RetoureUebersicht retoure)
            {
                // RetoureDetailView oeffnen
                var detailView = App.Services.GetRequiredService<RetoureDetailView>();
                detailView.LoadRetoure(retoure.KRMRetoure);

                if (Window.GetWindow(this) is MainWindow main)
                {
                    main.ShowContent(detailView);
                }
            }
        }
    }
}

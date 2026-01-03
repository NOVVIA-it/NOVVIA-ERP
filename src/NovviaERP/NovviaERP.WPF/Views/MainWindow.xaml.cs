using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class MainWindow : Window
    {
        private readonly CoreService _core;
        private bool _isLoading = true;
        private string _currentSection = ""; // Aktueller Abschnitt (Auftraege, Rechnungen, etc.)
        private readonly Stack<UserControl> _navigationStack = new();
        private const int MaxStackSize = 20;

        public MainWindow()
        {
            InitializeComponent();
            _core = App.Services.GetRequiredService<CoreService>();
            Title = $"NOVVIA ERP - {App.MandantName}";
            txtMandant.Text = App.MandantName;
            txtBenutzer.Text = App.Benutzername;

            // Dashboard beim Start laden und Einstellungen laden
            Loaded += async (s, e) =>
            {
                await LadeSidebarBreiteAsync();
                contentMain.Content = new DashboardPage();
                _isLoading = false;
            };

            // Sidebar-Breite beim Schliessen synchron speichern
            Closing += (s, e) =>
            {
                SpeichereSidebarBreiteAsync().GetAwaiter().GetResult();
            };
        }

        private async void SidebarSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            await SpeichereSidebarBreiteAsync();
        }

        private void AlleStatusMenusEinklappen()
        {
            // Navigation-Stack leeren bei Sidebar-Navigation
            _navigationStack.Clear();

            // Verkauf
            pnlAuftraegeStatus.Visibility = Visibility.Collapsed;
            pnlRechnungenStatus.Visibility = Visibility.Collapsed;
            pnlRechnungskorrekturenStatus.Visibility = Visibility.Collapsed;
            pnlRetourenStatus.Visibility = Visibility.Collapsed;
            btnAuftraege.Content = "▶ Auftraege";
            btnRechnungen.Content = "▶ Rechnungen";
            btnRechnungskorrekturen.Content = "▶ Rechnungskorrekturen";
            btnRetouren.Content = "▶ Retouren";

            // Einkauf
            pnlLieferantenBestellungenStatus.Visibility = Visibility.Collapsed;
            pnlEingangsrechnungenStatus.Visibility = Visibility.Collapsed;
            btnLieferantenBestellungen.Content = "▶ Bestellungen";
            btnEingangsrechnungen.Content = "▶ Eingangsrechnungen";
        }

        private void StatusMenuAufklappen(string section)
        {
            AlleStatusMenusEinklappen();
            _currentSection = section;

            switch (section)
            {
                // Verkauf
                case "Auftraege":
                    pnlAuftraegeStatus.Visibility = Visibility.Visible;
                    btnAuftraege.Content = "▼ Auftraege";
                    break;
                case "Rechnungen":
                    pnlRechnungenStatus.Visibility = Visibility.Visible;
                    btnRechnungen.Content = "▼ Rechnungen";
                    break;
                case "Rechnungskorrekturen":
                    pnlRechnungskorrekturenStatus.Visibility = Visibility.Visible;
                    btnRechnungskorrekturen.Content = "▼ Rechnungskorrekturen";
                    break;
                case "Retouren":
                    pnlRetourenStatus.Visibility = Visibility.Visible;
                    btnRetouren.Content = "▼ Retouren";
                    break;

                // Einkauf
                case "LieferantenBestellungen":
                    pnlLieferantenBestellungenStatus.Visibility = Visibility.Visible;
                    btnLieferantenBestellungen.Content = "▼ Bestellungen";
                    break;
                case "Eingangsrechnungen":
                    pnlEingangsrechnungenStatus.Visibility = Visibility.Visible;
                    btnEingangsrechnungen.Content = "▼ Eingangsrechnungen";
                    break;
            }
        }

        private async Task LadeSidebarBreiteAsync()
        {
            try
            {
                var breite = await _core.GetBenutzerEinstellungAsync(App.BenutzerId, "MainWindow.SidebarBreite");
                if (!string.IsNullOrEmpty(breite) && double.TryParse(breite, out double width) && width >= 100)
                {
                    sidebarColumn.Width = new GridLength(width);
                }
            }
            catch { /* Ignorieren - Standardwerte verwenden */ }
        }

        private async Task SpeichereSidebarBreiteAsync()
        {
            try
            {
                await _core.SaveBenutzerEinstellungAsync(App.BenutzerId, "MainWindow.SidebarBreite", sidebarColumn.Width.Value.ToString());
            }
            catch { /* Ignorieren */ }
        }

        private void ShowView<T>() where T : UserControl
        {
            try
            {
                var view = App.Services.GetRequiredService<T>();
                contentMain.Content = view;
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden: {ex.Message}", "Fehler");
            }
        }

        private void NavDashboard_Click(object sender, RoutedEventArgs e)
        {
            AlleStatusMenusEinklappen();
            contentMain.Content = new DashboardPage();
        }

        private void NavKunden_Click(object sender, RoutedEventArgs e)
        {
            AlleStatusMenusEinklappen();
            ShowView<KundenView>();
        }

        private void NavArtikel_Click(object sender, RoutedEventArgs e)
        {
            AlleStatusMenusEinklappen();
            ShowView<ArtikelView>();
        }

        private void NavKategorien_Click(object sender, RoutedEventArgs e)
        {
            AlleStatusMenusEinklappen();
            ShowView<KategorieView>();
        }

        // Aufträge - Hauptbutton klappt Menü auf und zeigt "Alle"
        private void NavBestellungen_Click(object sender, RoutedEventArgs e)
        {
            StatusMenuAufklappen("Auftraege");
            var view = App.Services.GetRequiredService<BestellungenView>();
            view.SetStatusFilter(""); // Alle
            contentMain.Content = view;
        }

        // Aufträge - Status-Untermenü
        private void NavAuftragStatus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string status)
            {
                var view = App.Services.GetRequiredService<BestellungenView>();
                view.SetStatusFilter(status);
                contentMain.Content = view;
            }
        }

        // Rechnungen - Hauptbutton klappt Menü auf und zeigt "Alle"
        private void NavRechnungen_Click(object sender, RoutedEventArgs e)
        {
            StatusMenuAufklappen("Rechnungen");
            var view = App.Services.GetRequiredService<RechnungenView>();
            view.SetStatusFilter(""); // Alle
            contentMain.Content = view;
        }

        // Rechnungen - Status-Untermenü
        private void NavRechnungStatus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string status)
            {
                var view = App.Services.GetRequiredService<RechnungenView>();
                view.SetStatusFilter(status);
                contentMain.Content = view;
            }
        }

        // Rechnungskorrekturen - Hauptbutton klappt Menü auf und zeigt "Alle"
        private void NavRechnungskorrekturen_Click(object sender, RoutedEventArgs e)
        {
            StatusMenuAufklappen("Rechnungskorrekturen");
            var view = App.Services.GetRequiredService<RechnungskorrekturenView>();
            view.SetStatusFilter("");
            contentMain.Content = view;
        }

        // Rechnungskorrekturen - Status-Untermenü
        private void NavRechnungskorrekturStatus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string status)
            {
                var view = App.Services.GetRequiredService<RechnungskorrekturenView>();
                view.SetStatusFilter(status);
                contentMain.Content = view;
            }
        }

        // Retouren - Hauptbutton klappt Menü auf und zeigt "Alle"
        private void NavRetouren_Click(object sender, RoutedEventArgs e)
        {
            StatusMenuAufklappen("Retouren");
            var view = App.Services.GetRequiredService<RetourenView>();
            view.SetStatusFilter("");
            contentMain.Content = view;
        }

        // Retouren - Status-Untermenü
        private void NavRetoureStatus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string status)
            {
                var view = App.Services.GetRequiredService<RetourenView>();
                view.SetStatusFilter(status);
                contentMain.Content = view;
            }
        }

        private void NavLieferanten_Click(object sender, RoutedEventArgs e)
        {
            AlleStatusMenusEinklappen();
            ShowView<LieferantenView>();
        }

        // Lieferantenbestellungen - Hauptbutton klappt Menü auf und zeigt "Alle"
        private void NavLieferantenBestellungen_Click(object sender, RoutedEventArgs e)
        {
            StatusMenuAufklappen("LieferantenBestellungen");
            var view = new LieferantenBestellungPage();
            view.SetStatusFilter("");
            contentMain.Content = view;
        }

        // Lieferantenbestellungen - Status-Untermenü
        private void NavLieferantenBestellungStatus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string status)
            {
                var view = new LieferantenBestellungPage();
                view.SetStatusFilter(status);
                contentMain.Content = view;
            }
        }

        // Eingangsrechnungen - Hauptbutton klappt Menü auf und zeigt "Alle"
        private void NavEingangsrechnungen_Click(object sender, RoutedEventArgs e)
        {
            StatusMenuAufklappen("Eingangsrechnungen");
            var view = new EingangsrechnungenPage();
            view.SetStatusFilter("");
            contentMain.Content = view;
        }

        // Eingangsrechnungen - Status-Untermenü
        private void NavEingangsrechnungStatus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string status)
            {
                var view = new EingangsrechnungenPage();
                view.SetStatusFilter(status);
                contentMain.Content = view;
            }
        }

        private void NavEdifact_Click(object sender, RoutedEventArgs e)
        {
            AlleStatusMenusEinklappen();
            ShowView<EdifactPage>();
        }

        private void NavWorkflow_Click(object sender, RoutedEventArgs e)
        {
            AlleStatusMenusEinklappen();
            ShowView<WorkflowPage>();
        }

        private void NavFormularDesigner_Click(object sender, RoutedEventArgs e)
        {
            AlleStatusMenusEinklappen();
            var frame = new System.Windows.Controls.Frame();
            var db = new NovviaERP.Core.Data.JtlDbContext(App.ConnectionString!);
            frame.Navigate(new FormularDesignerPage(db));
            contentMain.Content = frame;
        }

        private void NavLager_Click(object sender, RoutedEventArgs e)
        {
            AlleStatusMenusEinklappen();
            ShowView<LagerView>();
        }

        private void NavChargen_Click(object sender, RoutedEventArgs e)
        {
            AlleStatusMenusEinklappen();
            ShowView<LagerChargenView>();
        }

        private void NavVersand_Click(object sender, RoutedEventArgs e)
        {
            AlleStatusMenusEinklappen();
            contentMain.Content = new VersandPage();
        }

        private void NavZahlungsabgleich_Click(object sender, RoutedEventArgs e)
        {
            AlleStatusMenusEinklappen();
            contentMain.Content = new ZahlungsabgleichView();
        }

        private void NavMahnungslauf_Click(object sender, RoutedEventArgs e)
        {
            AlleStatusMenusEinklappen();
            contentMain.Content = new MahnungslaufPage();
        }

        private void NavOpListe_Click(object sender, RoutedEventArgs e)
        {
            AlleStatusMenusEinklappen();
            contentMain.Content = new OpListePage();
        }

        private void NavDatevExport_Click(object sender, RoutedEventArgs e)
        {
            AlleStatusMenusEinklappen();
            contentMain.Content = new DatevExportPage();
        }

        private void NavImport_Click(object sender, RoutedEventArgs e)
        {
            AlleStatusMenusEinklappen();
            contentMain.Content = new ImportView();
        }

        private void NavEigeneFelder_Click(object sender, RoutedEventArgs e)
        {
            AlleStatusMenusEinklappen();
            contentMain.Content = new EigeneFelderView();
        }

        private void NavTextmeldungen_Click(object sender, RoutedEventArgs e)
        {
            AlleStatusMenusEinklappen();
            contentMain.Content = new TextmeldungenPage();
        }

        private void NavAmeise_Click(object sender, RoutedEventArgs e)
        {
            AlleStatusMenusEinklappen();
            ShowView<AmeiseView>();
        }

        private void NavEinstellungen_Click(object sender, RoutedEventArgs e)
        {
            AlleStatusMenusEinklappen();
            ShowView<EinstellungenView>();
        }

        private void NavTest_Click(object sender, RoutedEventArgs e)
        {
            AlleStatusMenusEinklappen();
            contentMain.Content = new TestView();
        }

        /// <summary>
        /// Zeigt eine View im Hauptbereich an.
        /// Bei pushToStack=true wird die vorherige View auf den Stack gelegt (fuer Zurueck-Navigation).
        /// </summary>
        public void ShowContent(UserControl view, bool pushToStack = true)
        {
            // Aktuelle View auf Stack legen wenn gewuenscht
            if (pushToStack && contentMain.Content is UserControl currentView)
            {
                // Stack-Groesse begrenzen
                while (_navigationStack.Count >= MaxStackSize)
                {
                    // Aeltesten Eintrag entfernen (am Ende des Stacks)
                    var tempStack = new Stack<UserControl>();
                    while (_navigationStack.Count > 1)
                        tempStack.Push(_navigationStack.Pop());
                    _navigationStack.Clear();
                    while (tempStack.Count > 0)
                        _navigationStack.Push(tempStack.Pop());
                }
                _navigationStack.Push(currentView);
            }
            contentMain.Content = view;
        }

        /// <summary>
        /// Navigiert zurueck zur vorherigen View.
        /// Gibt true zurueck wenn erfolgreich, false wenn der Stack leer war.
        /// </summary>
        public bool NavigateBack()
        {
            if (_navigationStack.Count > 0)
            {
                var previousView = _navigationStack.Pop();
                contentMain.Content = previousView;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Leert den Navigation-Stack (z.B. bei Navigation ueber Sidebar)
        /// </summary>
        public void ClearNavigationStack()
        {
            _navigationStack.Clear();
        }

        /// <summary>
        /// Prueft ob eine Zurueck-Navigation moeglich ist
        /// </summary>
        public bool CanNavigateBack => _navigationStack.Count > 0;

        private void BtnAbmelden_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Wirklich abmelden?", "Abmelden",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }
        }

        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("HAUPTTEST BUTTON FUNKTIONIERT!", "Test");
        }
    }
}

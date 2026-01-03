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

            // Sidebar-Breite bei Änderung speichern (wenn Fenster geschlossen wird)
            Closing += async (s, e) =>
            {
                await SpeichereSidebarBreiteAsync();
            };
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

        private void NavDashboard_Click(object sender, RoutedEventArgs e) => contentMain.Content = new DashboardPage();
        private void NavKunden_Click(object sender, RoutedEventArgs e) => ShowView<KundenView>();
        private void NavArtikel_Click(object sender, RoutedEventArgs e) => ShowView<ArtikelView>();
        private void NavBestellungen_Click(object sender, RoutedEventArgs e) => ShowView<BestellungenView>();
        private void NavRechnungen_Click(object sender, RoutedEventArgs e) => ShowView<RechnungenView>();
        private void NavLieferanten_Click(object sender, RoutedEventArgs e) => ShowView<LieferantenView>();
        private void NavLieferantenBestellungen_Click(object sender, RoutedEventArgs e) => contentMain.Content = new LieferantenBestellungPage();
        private void NavEingangsrechnungen_Click(object sender, RoutedEventArgs e) => contentMain.Content = new EingangsrechnungenPage();
        private void NavEdifact_Click(object sender, RoutedEventArgs e) => ShowView<EdifactPage>();
        private void NavWorkflow_Click(object sender, RoutedEventArgs e) => ShowView<WorkflowPage>();
        private void NavFormularDesigner_Click(object sender, RoutedEventArgs e)
        {
            var frame = new System.Windows.Controls.Frame();
            var db = new NovviaERP.Core.Data.JtlDbContext(App.ConnectionString!);
            frame.Navigate(new FormularDesignerPage(db));
            contentMain.Content = frame;
        }
        private void NavLager_Click(object sender, RoutedEventArgs e) => ShowView<LagerView>();
        private void NavChargen_Click(object sender, RoutedEventArgs e) => ShowView<LagerChargenView>();
        private void NavVersand_Click(object sender, RoutedEventArgs e) => contentMain.Content = new VersandPage();
        private void NavZahlungsabgleich_Click(object sender, RoutedEventArgs e) => contentMain.Content = new ZahlungsabgleichView();
        private void NavImport_Click(object sender, RoutedEventArgs e) => contentMain.Content = new ImportView();
        private void NavEigeneFelder_Click(object sender, RoutedEventArgs e) => contentMain.Content = new EigeneFelderView();
        private void NavTextmeldungen_Click(object sender, RoutedEventArgs e) => contentMain.Content = new TextmeldungenPage();
        private void NavAmeise_Click(object sender, RoutedEventArgs e) => ShowView<AmeiseView>();
        private void NavEinstellungen_Click(object sender, RoutedEventArgs e) => ShowView<EinstellungenView>();
        private void NavTest_Click(object sender, RoutedEventArgs e) => contentMain.Content = new TestView();

        public void ShowContent(UserControl view)
        {
            contentMain.Content = view;
        }

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

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.SqlClient;

namespace NovviaERP.WPF.Views
{
    public partial class ProfilManagerWindow : Window
    {
        private static readonly string ProfilPfad = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NovviaERP", "profile.json");

        public ObservableCollection<ServerProfil> Profile { get; } = new();
        public ObservableCollection<MandantInfo> Mandanten { get; } = new();
        
        private ServerProfil? _aktuellesProfil;
        public ServerProfil? AusgewaehltesProfil { get; private set; }

        public ProfilManagerWindow()
        {
            InitializeComponent();
            LoadProfile();
            lstProfile.ItemsSource = Profile;
            dgMandanten.ItemsSource = Mandanten;
        }

        private void LoadProfile()
        {
            Profile.Clear();
            
            if (File.Exists(ProfilPfad))
            {
                try
                {
                    var json = File.ReadAllText(ProfilPfad);
                    var profile = JsonSerializer.Deserialize<List<ServerProfil>>(json);
                    if (profile != null)
                    {
                        foreach (var p in profile)
                            Profile.Add(p);
                    }
                }
                catch { }
            }

            // Standard-Profil falls leer
            if (Profile.Count == 0)
            {
                Profile.Add(new ServerProfil
                {
                    Name = "NOVVIA Produktion",
                    Beschreibung = "Produktivserver",
                    Server = "24.134.81.65,2107\\NOVVIAS05",
                    SqlBenutzer = "NOVVIA_SQL",
                    Mandanten = new List<MandantInfo>
                    {
                        new() { Name = "NOVVIA", Datenbank = "Mandant_1", Aktiv = true },
                        new() { Name = "NOVVIA_PHARM", Datenbank = "Mandant_2", Aktiv = true },
                        new() { Name = "PA", Datenbank = "Mandant_3", Aktiv = true },
                        new() { Name = "NOVVIA_TEST", Datenbank = "Mandant_5", Aktiv = false }
                    }
                });
            }
        }

        private void SaveProfile()
        {
            try
            {
                var dir = Path.GetDirectoryName(ProfilPfad);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);
                
                var json = JsonSerializer.Serialize(Profile.ToList(), new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ProfilPfad, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LstProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _aktuellesProfil = lstProfile.SelectedItem as ServerProfil;
            if (_aktuellesProfil == null) return;

            txtProfilName.Text = _aktuellesProfil.Name;
            txtBeschreibung.Text = _aktuellesProfil.Beschreibung;
            txtServer.Text = _aktuellesProfil.Server;
            txtSqlUser.Text = _aktuellesProfil.SqlBenutzer;
            txtSqlPass.Password = _aktuellesProfil.SqlPasswort;

            Mandanten.Clear();
            foreach (var m in _aktuellesProfil.Mandanten)
                Mandanten.Add(m);
        }

        private void BtnNeuesProfil_Click(object sender, RoutedEventArgs e)
        {
            var neuesProfil = new ServerProfil
            {
                Name = "Neues Profil " + (Profile.Count + 1),
                Beschreibung = "Neues Serverprofil",
                Server = "24.134.81.65,2107\\NOVVIAS05",
                SqlBenutzer = "NOVVIA_SQL",
                SqlPasswort = "",
                Mandanten = new List<MandantInfo>()
            };
            Profile.Add(neuesProfil);
            lstProfile.SelectedItem = neuesProfil;

            // Formular leeren für Eingabe
            txtProfilName.Text = neuesProfil.Name;
            txtBeschreibung.Text = neuesProfil.Beschreibung;
            txtServer.Text = neuesProfil.Server;
            txtSqlUser.Text = neuesProfil.SqlBenutzer;
            txtSqlPass.Password = "";
            Mandanten.Clear();

            txtProfilName.Focus();
            txtProfilName.SelectAll();
        }

        private void BtnProfilLoeschen_Click(object sender, RoutedEventArgs e)
        {
            if (_aktuellesProfil == null) return;
            if (MessageBox.Show($"Profil '{_aktuellesProfil.Name}' wirklich löschen?", "Löschen",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                Profile.Remove(_aktuellesProfil);
                SaveProfile();
            }
        }

        private async void BtnTestVerbindung_Click(object sender, RoutedEventArgs e)
        {
            txtVerbindungsStatus.Text = "Teste Verbindung...";
            txtVerbindungsStatus.Foreground = Brushes.Gray;

            var connStr = $"Server={txtServer.Text};User Id={txtSqlUser.Text};Password={txtSqlPass.Password};TrustServerCertificate=True;Connection Timeout=10";

            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();
                
                txtVerbindungsStatus.Text = "✅ Verbindung erfolgreich!";
                txtVerbindungsStatus.Foreground = Brushes.Green;

                // Mandanten automatisch laden
                await LoadMandantenAsync(conn);
            }
            catch (Exception ex)
            {
                txtVerbindungsStatus.Text = $"❌ Fehler: {ex.Message}";
                txtVerbindungsStatus.Foreground = Brushes.Red;
            }
        }

        private async void BtnMandantenLaden_Click(object sender, RoutedEventArgs e)
        {
            var connStr = $"Server={txtServer.Text};User Id={txtSqlUser.Text};Password={txtSqlPass.Password};TrustServerCertificate=True;Connection Timeout=10";

            try
            {
                using var conn = new SqlConnection(connStr);
                await conn.OpenAsync();
                await LoadMandantenAsync(conn);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task LoadMandantenAsync(SqlConnection conn)
        {
            Mandanten.Clear();

            // JTL speichert Mandanten in eigener Verwaltung - wir suchen alle Mandant_X Datenbanken
            using var cmd = new SqlCommand(@"
                SELECT name FROM sys.databases 
                WHERE name LIKE 'Mandant_%' OR name = 'eazybusiness'
                ORDER BY name", conn);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var dbName = reader.GetString(0);
                var mandantName = dbName == "eazybusiness" ? "eB-Standard" : dbName.Replace("Mandant_", "Mandant ");
                
                Mandanten.Add(new MandantInfo
                {
                    Name = mandantName,
                    Datenbank = dbName,
                    Aktiv = true
                });
            }
        }

        private void BtnSpeichern_Click(object sender, RoutedEventArgs e)
        {
            if (_aktuellesProfil == null)
            {
                _aktuellesProfil = new ServerProfil();
                Profile.Add(_aktuellesProfil);
            }

            _aktuellesProfil.Name = txtProfilName.Text;
            _aktuellesProfil.Beschreibung = txtBeschreibung.Text;
            _aktuellesProfil.Server = txtServer.Text;
            _aktuellesProfil.SqlBenutzer = txtSqlUser.Text;
            _aktuellesProfil.SqlPasswort = txtSqlPass.Password;
            _aktuellesProfil.Mandanten = Mandanten.ToList();

            SaveProfile();
            lstProfile.Items.Refresh();
            
            MessageBox.Show("Profil gespeichert!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnAbbrechen_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class ServerProfil
    {
        public string Name { get; set; } = "";
        public string Beschreibung { get; set; } = "";
        public string Server { get; set; } = "";
        public string SqlBenutzer { get; set; } = "";
        public string SqlPasswort { get; set; } = "";
        public List<MandantInfo> Mandanten { get; set; } = new();

        // Für UI - nicht serialisieren
        [System.Text.Json.Serialization.JsonIgnore]
        public string ServerKurz => Server.Length > 30 ? Server[..30] + "..." : Server;
        [System.Text.Json.Serialization.JsonIgnore]
        public Brush StatusFarbe => string.IsNullOrEmpty(SqlPasswort) ? Brushes.Orange : Brushes.LimeGreen;
    }

    public class MandantInfo
    {
        public string Name { get; set; } = "";
        public string Datenbank { get; set; } = "";
        public bool Aktiv { get; set; } = true;
    }
}

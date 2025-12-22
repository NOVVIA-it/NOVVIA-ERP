using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using Microsoft.Data.SqlClient;
using Dapper;

namespace NovviaERP.WPF.Views
{
    public partial class LoginWindow : Window
    {
        private static readonly string ProfilPfad = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NovviaERP", "profile.json");
        
        private static readonly string LoginPfad = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NovviaERP", "login.json");

        public ObservableCollection<ServerProfil> Profile { get; } = new();
        public ObservableCollection<MandantInfo> Mandanten { get; } = new();

        // Ergebnis nach erfolgreichem Login
        public string? ConnectionString { get; private set; }
        public string? MandantName { get; private set; }
        public string? Benutzername { get; private set; }
        public int BenutzerId { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();
            LoadProfile();
            LoadLetzteAnmeldung();
            
            cbProfil.ItemsSource = Profile;
            cbMandant.ItemsSource = Mandanten;
            
            if (Profile.Count > 0)
                cbProfil.SelectedIndex = 0;

            txtBenutzer.Focus();
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

        private void LoadLetzteAnmeldung()
        {
            if (!File.Exists(LoginPfad)) return;

            try
            {
                var json = File.ReadAllText(LoginPfad);
                var login = JsonSerializer.Deserialize<LetzteAnmeldung>(json);
                if (login != null)
                {
                    txtBenutzer.Text = login.Benutzer;
                    
                    // Profil auswählen
                    for (int i = 0; i < Profile.Count; i++)
                    {
                        if (Profile[i].Name == login.Profil)
                        {
                            cbProfil.SelectedIndex = i;
                            break;
                        }
                    }
                    
                    // Mandant auswählen (nach Profil-Wechsel)
                    Dispatcher.BeginInvoke(() =>
                    {
                        for (int i = 0; i < Mandanten.Count; i++)
                        {
                            if (Mandanten[i].Name == login.Mandant)
                            {
                                cbMandant.SelectedIndex = i;
                                break;
                            }
                        }
                    });
                }
            }
            catch { }
        }

        private void SaveLetzteAnmeldung()
        {
            try
            {
                var dir = Path.GetDirectoryName(LoginPfad);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);

                var login = new LetzteAnmeldung
                {
                    Benutzer = txtBenutzer.Text,
                    Profil = (cbProfil.SelectedItem as ServerProfil)?.Name ?? "",
                    Mandant = (cbMandant.SelectedItem as MandantInfo)?.Name ?? ""
                };

                var json = JsonSerializer.Serialize(login);
                File.WriteAllText(LoginPfad, json);
            }
            catch { }
        }

        private void CbProfil_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var profil = cbProfil.SelectedItem as ServerProfil;
            if (profil == null) return;

            Mandanten.Clear();
            foreach (var m in profil.Mandanten.Where(m => m.Aktiv))
            {
                Mandanten.Add(m);
            }

            if (Mandanten.Count > 0)
                cbMandant.SelectedIndex = 0;
        }

        private void BtnProfileVerwalten_Click(object sender, RoutedEventArgs e)
        {
            var profilManager = new ProfilManagerWindow();
            profilManager.ShowDialog();
            
            // Profile neu laden
            LoadProfile();
            cbProfil.ItemsSource = null;
            cbProfil.ItemsSource = Profile;
            if (Profile.Count > 0) cbProfil.SelectedIndex = 0;
        }

        private void TxtPasswort_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                DoLoginAsync();
        }

        private void BtnLogin_DirectClick(object sender, RoutedEventArgs e)
        {
            DoLoginAsync();
        }

        private bool _isLoggingIn = false;

        private async void DoLoginAsync()
        {
            if (_isLoggingIn) return;

            txtFehler.Visibility = Visibility.Collapsed;

            var profil = cbProfil.SelectedItem as ServerProfil;
            var mandant = cbMandant.SelectedItem as MandantInfo;

            if (profil == null || mandant == null)
            {
                ShowError("Bitte Profil und Mandant auswählen.");
                return;
            }

            if (string.IsNullOrWhiteSpace(txtBenutzer.Text))
            {
                ShowError("Bitte Benutzername eingeben.");
                txtBenutzer.Focus();
                return;
            }

            _isLoggingIn = true;
            btnLogin.IsEnabled = false;
            btnLogin.Content = "Anmelden...";

            try
            {
                // Connection String für eazybusiness (Benutzer-DB)
                var eazyConnStr = $"Server={profil.Server};Database=eazybusiness;User Id={profil.SqlBenutzer};Password={profil.SqlPasswort};TrustServerCertificate=True;MultipleActiveResultSets=True";

                // Connection String für Mandant
                var connStr = $"Server={profil.Server};Database={mandant.Datenbank};User Id={profil.SqlBenutzer};Password={profil.SqlPasswort};TrustServerCertificate=True;MultipleActiveResultSets=True";

                // JTL-Benutzer aus eazybusiness prüfen
                using var eazyConn = new SqlConnection(eazyConnStr);
                await eazyConn.OpenAsync();

                var benutzer = await eazyConn.QueryFirstOrDefaultAsync<JtlBenutzerLogin>(
                    @"SELECT kBenutzer, cName, cLogin, cPasswort, nAktiv
                      FROM tBenutzer
                      WHERE cLogin = @Login AND nAktiv = 1",
                    new { Login = txtBenutzer.Text });

                if (benutzer == null)
                {
                    ShowError("Benutzer nicht gefunden oder inaktiv.");
                    return;
                }

                // JTL Passwort-Prüfung (SHA1 oder MD5 je nach Version)
                // Bypass für Test: "test" als Passwort erlaubt Login
                if (txtPasswort.Password != "test" && !VerifyJtlPassword(txtPasswort.Password, benutzer.CPasswort))
                {
                    ShowError("Falsches Passwort.");
                    return;
                }

                // Erfolg!
                ConnectionString = connStr;
                MandantName = mandant.Name;
                Benutzername = benutzer.CName ?? benutzer.CLogin;
                BenutzerId = benutzer.KBenutzer;

                if (chkMerken.IsChecked == true)
                    SaveLetzteAnmeldung();

                DialogResult = true;
                Close();
            }
            catch (SqlException ex)
            {
                ShowError($"Datenbankfehler: {ex.Message}");
            }
            catch (Exception ex)
            {
                ShowError($"Fehler: {ex.Message}");
            }
            finally
            {
                _isLoggingIn = false;
                btnLogin.IsEnabled = true;
                btnLogin.Content = "ANMELDEN";
            }
        }

        private bool VerifyJtlPassword(string eingabe, string? hash)
        {
            if (string.IsNullOrEmpty(hash)) return false;

            // JTL verwendet verschiedene Hash-Methoden je nach Version
            // Einfache Variante: MD5 oder SHA1
            
            // MD5
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var inputBytes = System.Text.Encoding.UTF8.GetBytes(eingabe);
                var hashBytes = md5.ComputeHash(inputBytes);
                var md5Hash = Convert.ToHexString(hashBytes).ToLower();
                if (md5Hash == hash.ToLower()) return true;
            }

            // SHA1
            using (var sha1 = System.Security.Cryptography.SHA1.Create())
            {
                var inputBytes = System.Text.Encoding.UTF8.GetBytes(eingabe);
                var hashBytes = sha1.ComputeHash(inputBytes);
                var sha1Hash = Convert.ToHexString(hashBytes).ToLower();
                if (sha1Hash == hash.ToLower()) return true;
            }

            // SHA256
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var inputBytes = System.Text.Encoding.UTF8.GetBytes(eingabe);
                var hashBytes = sha256.ComputeHash(inputBytes);
                var sha256Hash = Convert.ToHexString(hashBytes).ToLower();
                if (sha256Hash == hash.ToLower()) return true;
            }

            // Fallback: Direktvergleich (falls Klartext - sollte nicht sein!)
            return eingabe == hash;
        }

        private void ShowError(string message)
        {
            txtFehler.Text = message;
            txtFehler.Visibility = Visibility.Visible;
        }

        private void BtnSchliessen_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }

    public class JtlBenutzerLogin
    {
        public int KBenutzer { get; set; }
        public string? CName { get; set; }
        public string? CLogin { get; set; }
        public string? CPasswort { get; set; }
        public bool NAktiv { get; set; }
    }

    public class LetzteAnmeldung
    {
        public string Benutzer { get; set; } = "";
        public string Profil { get; set; } = "";
        public string Mandant { get; set; } = "";
    }
}

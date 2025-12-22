using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NovviaERP.Core.Entities;
using NovviaERP.Core.Services;

namespace NovviaERP.WPF.Views
{
    public partial class BenutzerPage : Page
    {
        private readonly AuthService _auth;
        private Benutzer? _selectedBenutzer;
        private List<Berechtigung> _alleBerechtigungen = new();

        public BenutzerPage()
        {
            InitializeComponent();
            _auth = new AuthService(App.Db);
            Loaded += async (s, e) =>
            {
                dgBenutzer.ItemsSource = await _auth.GetBenutzerAsync();
                dgRollen.ItemsSource = await _auth.GetRollenAsync();
                cmbRolle.ItemsSource = await _auth.GetRollenAsync();
                _alleBerechtigungen = (await _auth.GetAlleBerechtigungenAsync()).ToList();
                lstBerechtigungen.ItemsSource = _alleBerechtigungen.Select(b => new { Id = b.Id, Display = $"{b.Modul}: {b.Aktion}" });
            };
        }

        private void Benutzer_SelectionChanged(object s, SelectionChangedEventArgs e)
        {
            if (dgBenutzer.SelectedItem is Benutzer b)
            {
                _selectedBenutzer = b;
                txtLogin.Text = b.Login;
                txtVorname.Text = b.Vorname;
                txtNachname.Text = b.Nachname;
                txtEmail.Text = b.Email;
                cmbRolle.SelectedValue = b.RolleId;
                chkAktiv.IsChecked = b.Aktiv;
            }
        }

        private void NeuBenutzer_Click(object s, RoutedEventArgs e)
        {
            _selectedBenutzer = null;
            txtLogin.Text = ""; txtVorname.Text = ""; txtNachname.Text = ""; txtEmail.Text = "";
            cmbRolle.SelectedIndex = 0; chkAktiv.IsChecked = true;
        }

        private async void Speichern_Click(object s, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtLogin.Text) || string.IsNullOrEmpty(txtNachname.Text))
            {
                MessageBox.Show("Login und Nachname sind Pflichtfelder!");
                return;
            }

            var benutzer = _selectedBenutzer ?? new Benutzer();
            benutzer.Login = txtLogin.Text;
            benutzer.Vorname = txtVorname.Text;
            benutzer.Nachname = txtNachname.Text;
            benutzer.Email = txtEmail.Text;
            benutzer.RolleId = (int)(cmbRolle.SelectedValue ?? 1);
            benutzer.Aktiv = chkAktiv.IsChecked ?? true;

            if (_selectedBenutzer == null)
            {
                var passwort = string.IsNullOrEmpty(txtNeuesPasswort.Password) ? "changeme" : txtNeuesPasswort.Password;
                await _auth.CreateBenutzerAsync(benutzer, passwort);
                MessageBox.Show($"Benutzer erstellt. Initiales Passwort: {passwort}");
            }
            else
            {
                var conn = await App.Db.GetConnectionAsync();
                await Dapper.SqlMapper.ExecuteAsync(conn,
                    "UPDATE tBenutzer SET cLogin=@Login, cVorname=@Vorname, cNachname=@Nachname, cMail=@Email, kRolle=@RolleId, nAktiv=@Aktiv WHERE kBenutzer=@Id",
                    benutzer);
                MessageBox.Show("Gespeichert!");
            }
            dgBenutzer.ItemsSource = await _auth.GetBenutzerAsync();
        }

        private async void PasswortReset_Click(object s, RoutedEventArgs e)
        {
            if (_selectedBenutzer == null || string.IsNullOrEmpty(txtNeuesPasswort.Password))
            {
                MessageBox.Show("Benutzer auswählen und neues Passwort eingeben!");
                return;
            }
            await _auth.ResetPasswordAsync(_selectedBenutzer.Id, txtNeuesPasswort.Password);
            txtNeuesPasswort.Password = "";
            MessageBox.Show("Passwort zurückgesetzt!");
        }

        private async void Loeschen_Click(object s, RoutedEventArgs e)
        {
            if (_selectedBenutzer == null) return;
            if (MessageBox.Show($"Benutzer '{_selectedBenutzer.Login}' wirklich löschen?", "Löschen", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                await _auth.SetBenutzerAktivAsync(_selectedBenutzer.Id, false);
                dgBenutzer.ItemsSource = await _auth.GetBenutzerAsync();
            }
        }

        private async void Rolle_SelectionChanged(object s, SelectionChangedEventArgs e)
        {
            if (dgRollen.SelectedItem is Rolle rolle)
            {
                lstBerechtigungen.SelectedItems.Clear();
                var ids = rolle.Berechtigungen.Select(rb => rb.BerechtigungId).ToList();
                foreach (var item in lstBerechtigungen.Items)
                {
                    var id = (int)item.GetType().GetProperty("Id")!.GetValue(item)!;
                    if (ids.Contains(id))
                        lstBerechtigungen.SelectedItems.Add(item);
                }
            }
        }

        private async void BerechtigungenSpeichern_Click(object s, RoutedEventArgs e)
        {
            if (dgRollen.SelectedItem is not Rolle rolle) return;
            var ids = lstBerechtigungen.SelectedItems.Cast<object>()
                .Select(item => (int)item.GetType().GetProperty("Id")!.GetValue(item)!).ToList();
            await _auth.UpdateRolleBerechtigungenAsync(rolle.Id, ids);
            MessageBox.Show("Berechtigungen gespeichert!");
            dgRollen.ItemsSource = await _auth.GetRollenAsync();
        }

        private async void NeueRolle_Click(object s, RoutedEventArgs e)
        {
            var name = Microsoft.VisualBasic.Interaction.InputBox("Name der neuen Rolle:", "Neue Rolle");
            if (string.IsNullOrEmpty(name)) return;
            await _auth.CreateRolleAsync(new Rolle { Name = name }, new List<int>());
            dgRollen.ItemsSource = await _auth.GetRollenAsync();
        }
    }
}

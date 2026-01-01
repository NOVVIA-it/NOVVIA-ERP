using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using NovviaERP.Core.Data;

namespace NovviaERP.WPF.Views
{
    public partial class WorkflowDialog : Window
    {
        private readonly JtlDbContext _db;
        private readonly int? _workflowId;

        public WorkflowDialog(NovviaWorkflow? workflow = null)
        {
            InitializeComponent();
            _db = new JtlDbContext(App.ConnectionString!);
            _workflowId = workflow?.KWorkflow;

            if (workflow != null)
            {
                Title = "Workflow bearbeiten";
                LadeWorkflow(workflow);
            }
            else
            {
                Title = "Neuer Workflow";
                cmbEreignis.SelectedIndex = 0;
                cmbAktionTyp.SelectedIndex = 0;
            }
        }

        private void LadeWorkflow(NovviaWorkflow wf)
        {
            txtName.Text = wf.CName;
            txtBeschreibung.Text = wf.CBeschreibung;

            // Ereignis
            foreach (ComboBoxItem item in cmbEreignis.Items)
            {
                if (item.Tag?.ToString() == wf.CEreignis)
                {
                    cmbEreignis.SelectedItem = item;
                    break;
                }
            }

            txtBedingung.Text = wf.CBedingung;

            // Aktionstyp
            foreach (ComboBoxItem item in cmbAktionTyp.Items)
            {
                if (item.Tag?.ToString() == wf.CAktionTyp)
                {
                    cmbAktionTyp.SelectedItem = item;
                    break;
                }
            }

            // Parameter parsen
            ParseAktionParameter(wf.CAktionTyp, wf.CAktionParameter);

            txtPrioritaet.Text = wf.NPrioritaet.ToString();
            chkAktiv.IsChecked = wf.NAktiv;
        }

        private void ParseAktionParameter(string typ, string? parameter)
        {
            if (string.IsNullOrEmpty(parameter)) return;

            switch (typ)
            {
                case "EMAIL":
                    var emailParts = parameter.Split(':');
                    if (emailParts.Length >= 1) txtEmailTo.Text = emailParts[0];
                    if (emailParts.Length >= 2) txtEmailSubject.Text = emailParts[1];
                    if (emailParts.Length >= 3) txtEmailBody.Text = string.Join(":", emailParts.Skip(2));
                    break;

                case "WEBHOOK":
                    txtWebhookUrl.Text = parameter;
                    break;

                case "STATUS":
                    foreach (ComboBoxItem item in cmbNeuerStatus.Items)
                    {
                        if (item.Tag?.ToString() == parameter)
                        {
                            cmbNeuerStatus.SelectedItem = item;
                            break;
                        }
                    }
                    break;

                case "SQL":
                    txtSqlStatement.Text = parameter;
                    break;
            }
        }

        private void AktionTyp_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (cmbAktionTyp.SelectedItem is not ComboBoxItem item) return;

            var typ = item.Tag?.ToString();

            pnlEmail.Visibility = typ == "EMAIL" ? Visibility.Visible : Visibility.Collapsed;
            pnlWebhook.Visibility = typ == "WEBHOOK" ? Visibility.Visible : Visibility.Collapsed;
            pnlStatus.Visibility = typ == "STATUS" ? Visibility.Visible : Visibility.Collapsed;
            pnlSql.Visibility = typ == "SQL" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BedingungHilfe_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Bedingungen werden im Format 'Feld=Wert' oder 'Feld>Wert' angegeben.\n\n" +
                "Beispiele:\n" +
                "  Status=3 - Workflow wird nur ausgefuehrt wenn Status = 3\n" +
                "  Betrag>100 - Nur bei Betrag groesser 100\n" +
                "  Lagerbestand<10 - Nur bei Lagerbestand unter 10\n\n" +
                "Unterstuetzte Operatoren: = != > < >= <=\n\n" +
                "Leer lassen = Workflow wird immer ausgefuehrt",
                "Hilfe zu Bedingungen",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private async void Speichern_Click(object sender, RoutedEventArgs e)
        {
            // Validierung
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Bitte geben Sie einen Namen ein.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtName.Focus();
                return;
            }

            if (cmbEreignis.SelectedItem is not ComboBoxItem ereignisItem)
            {
                MessageBox.Show("Bitte waehlen Sie ein Ereignis.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (cmbAktionTyp.SelectedItem is not ComboBoxItem aktionItem)
            {
                MessageBox.Show("Bitte waehlen Sie einen Aktionstyp.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var ereignis = ereignisItem.Tag?.ToString() ?? "";
                var aktionTyp = aktionItem.Tag?.ToString() ?? "";
                var aktionParameter = BuildAktionParameter(aktionTyp);

                var conn = await _db.GetConnectionAsync();

                if (_workflowId.HasValue)
                {
                    await conn.ExecuteAsync(@"
                        UPDATE NOVVIA.Workflow SET
                            cName = @Name, cBeschreibung = @Beschreibung,
                            cEreignis = @Ereignis, cBedingung = @Bedingung,
                            cAktionTyp = @AktionTyp, cAktionParameter = @AktionParameter,
                            nAktiv = @Aktiv, nPrioritaet = @Prioritaet,
                            dGeaendert = GETDATE()
                        WHERE kWorkflow = @Id",
                        new
                        {
                            Id = _workflowId,
                            Name = txtName.Text.Trim(),
                            Beschreibung = txtBeschreibung.Text.Trim(),
                            Ereignis = ereignis,
                            Bedingung = string.IsNullOrWhiteSpace(txtBedingung.Text) ? null : txtBedingung.Text.Trim(),
                            AktionTyp = aktionTyp,
                            AktionParameter = aktionParameter,
                            Aktiv = chkAktiv.IsChecked ?? true,
                            Prioritaet = int.TryParse(txtPrioritaet.Text, out var p) ? p : 100
                        });
                }
                else
                {
                    await conn.ExecuteAsync(@"
                        INSERT INTO NOVVIA.Workflow
                        (cName, cBeschreibung, cEreignis, cBedingung, cAktionTyp, cAktionParameter, nAktiv, nPrioritaet)
                        VALUES (@Name, @Beschreibung, @Ereignis, @Bedingung, @AktionTyp, @AktionParameter, @Aktiv, @Prioritaet)",
                        new
                        {
                            Name = txtName.Text.Trim(),
                            Beschreibung = txtBeschreibung.Text.Trim(),
                            Ereignis = ereignis,
                            Bedingung = string.IsNullOrWhiteSpace(txtBedingung.Text) ? null : txtBedingung.Text.Trim(),
                            AktionTyp = aktionTyp,
                            AktionParameter = aktionParameter,
                            Aktiv = chkAktiv.IsChecked ?? true,
                            Prioritaet = int.TryParse(txtPrioritaet.Text, out var p) ? p : 100
                        });
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string BuildAktionParameter(string typ)
        {
            switch (typ)
            {
                case "EMAIL":
                    return $"{txtEmailTo.Text.Trim()}:{txtEmailSubject.Text.Trim()}:{txtEmailBody.Text.Trim()}";

                case "WEBHOOK":
                    return txtWebhookUrl.Text.Trim();

                case "STATUS":
                    return (cmbNeuerStatus.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "1";

                case "SQL":
                    return txtSqlStatement.Text.Trim();

                default:
                    return "";
            }
        }

        private void Abbrechen_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

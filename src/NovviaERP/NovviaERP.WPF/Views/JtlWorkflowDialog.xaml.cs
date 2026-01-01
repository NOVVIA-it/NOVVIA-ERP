using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using NovviaERP.Core.Data;

namespace NovviaERP.WPF.Views
{
    public partial class JtlWorkflowDialog : Window
    {
        private readonly JtlDbContext _db;
        private readonly int? _workflowId;
        private ObservableCollection<JtlAktionEdit> _aktionen = new();
        private ObservableCollection<JtlBedingungEdit> _bedingungen = new();
        private JtlAktionEdit? _selectedAktion;

        public JtlWorkflowDialog(int? workflowId = null)
        {
            InitializeComponent();
            _db = new JtlDbContext(App.ConnectionString!);
            _workflowId = workflowId;

            dgAktionen.ItemsSource = _aktionen;
            dgBedingungen.ItemsSource = _bedingungen;

            Loaded += async (s, e) => await InitAsync();
        }

        private async System.Threading.Tasks.Task InitAsync()
        {
            await LadeEventsAsync();

            if (_workflowId.HasValue)
            {
                Title = "JTL Workflow bearbeiten";
                await LadeWorkflowAsync(_workflowId.Value);
            }
            else
            {
                Title = "Neuer JTL Workflow";
                cmbObjekt.SelectedIndex = 0;
            }
        }

        private async System.Threading.Tasks.Task LadeEventsAsync()
        {
            try
            {
                var conn = await _db.GetConnectionAsync();
                var events = await conn.QueryAsync<JtlEvent>(
                    "SELECT DISTINCT nEvent, nObjekt, cDisplayName FROM dbo.tWorkflowEvent ORDER BY nObjekt, cDisplayName");
                _allEvents = events.ToList();
                UpdateEventComboBox();
            }
            catch { }
        }

        private List<JtlEvent> _allEvents = new();

        private void UpdateEventComboBox()
        {
            if (cmbObjekt.SelectedItem is not ComboBoxItem item) return;
            var objekt = int.Parse(item.Tag?.ToString() ?? "6");
            var filtered = _allEvents.Where(e => e.NObjekt == objekt).ToList();
            cmbEvent.ItemsSource = filtered;
            if (filtered.Any()) cmbEvent.SelectedIndex = 0;
        }

        private void Objekt_Changed(object sender, SelectionChangedEventArgs e)
        {
            UpdateEventComboBox();
        }

        private async System.Threading.Tasks.Task LadeWorkflowAsync(int id)
        {
            try
            {
                var conn = await _db.GetConnectionAsync();

                var wf = await conn.QueryFirstOrDefaultAsync<dynamic>(
                    "SELECT * FROM dbo.tWorkflow WHERE kWorkflow = @Id", new { Id = id });

                if (wf == null) return;

                txtName.Text = wf.cName;

                // Objekt
                foreach (ComboBoxItem item in cmbObjekt.Items)
                {
                    if (item.Tag?.ToString() == wf.nObjekt.ToString())
                    {
                        cmbObjekt.SelectedItem = item;
                        break;
                    }
                }

                // Typ
                foreach (ComboBoxItem item in cmbTyp.Items)
                {
                    if (item.Tag?.ToString() == wf.nTyp.ToString())
                    {
                        cmbTyp.SelectedItem = item;
                        break;
                    }
                }

                // Event
                UpdateEventComboBox();
                cmbEvent.SelectedValue = (int)wf.nEvent;

                // Aktionen laden
                var aktionen = await conn.QueryAsync<JtlAktionEdit>(@"
                    SELECT kWorkflowAktion, kWorkflow, nPos, cName,
                           CAST(xXmlObjekt AS NVARCHAR(MAX)) AS XmlObjekt,
                           CASE
                               WHEN CAST(xXmlObjekt AS NVARCHAR(MAX)) LIKE '%jtlAktionSQL%' THEN 'SQL'
                               WHEN CAST(xXmlObjekt AS NVARCHAR(MAX)) LIKE '%jtlAktionWertSetzen%' THEN 'WertSetzen'
                               WHEN CAST(xXmlObjekt AS NVARCHAR(MAX)) LIKE '%jtlAktionDateiSchreiben%' THEN 'Datei'
                               ELSE 'Andere'
                           END AS AktionTyp
                    FROM dbo.tWorkflowAktion
                    WHERE kWorkflow = @Id
                    ORDER BY nPos", new { Id = id });

                _aktionen.Clear();
                foreach (var a in aktionen)
                {
                    a.ExtractParameter();
                    _aktionen.Add(a);
                }

                // Bedingungen laden
                var bedingungen = await conn.QueryAsync<JtlBedingungEdit>(@"
                    SELECT b.kWorkflowBedingung, b.kWorkflow, b.nPos, b.cVergleichswert, b.nOperator,
                           b.kWorkflowEigenschaft,
                           COALESCE(e.cEigenschaft, 'Eigenschaft') AS Eigenschaft
                    FROM dbo.tWorkflowBedingung b
                    LEFT JOIN dbo.tWorkflowEigenschaft e ON b.kWorkflowEigenschaft = e.kWorkflowEigenschaft
                    WHERE b.kWorkflow = @Id
                    ORDER BY b.nPos", new { Id = id });

                _bedingungen.Clear();
                foreach (var b in bedingungen) _bedingungen.Add(b);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Aktionen

        private void AddSqlAktion_Click(object sender, RoutedEventArgs e)
        {
            var aktion = new JtlAktionEdit
            {
                NPos = _aktionen.Count + 1,
                CName = "SQL Aktion",
                AktionTyp = "SQL",
                ParameterPreview = "SELECT 1"
            };
            _aktionen.Add(aktion);
        }

        private void AddWertSetzenAktion_Click(object sender, RoutedEventArgs e)
        {
            var aktion = new JtlAktionEdit
            {
                NPos = _aktionen.Count + 1,
                CName = "Wert setzen",
                AktionTyp = "WertSetzen",
                ParameterPreview = ""
            };
            _aktionen.Add(aktion);
        }

        private void AddDateiAktion_Click(object sender, RoutedEventArgs e)
        {
            var aktion = new JtlAktionEdit
            {
                NPos = _aktionen.Count + 1,
                CName = "Datei schreiben",
                AktionTyp = "Datei",
                ParameterPreview = "C:\\Export\\datei.txt"
            };
            _aktionen.Add(aktion);
        }

        private void Aktion_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnAktionLoeschen.IsEnabled = dgAktionen.SelectedItem != null;

            if (dgAktionen.SelectedItem is JtlAktionEdit aktion)
            {
                _selectedAktion = aktion;
                txtAktionName.Text = aktion.CName;
                txtAktionParameter.Text = aktion.ParameterPreview;
                grpAktionDetails.Visibility = Visibility.Visible;
            }
            else
            {
                grpAktionDetails.Visibility = Visibility.Collapsed;
            }
        }

        private void AktionLoeschen_Click(object sender, RoutedEventArgs e)
        {
            if (dgAktionen.SelectedItem is JtlAktionEdit aktion)
            {
                _aktionen.Remove(aktion);
                RenumberAktionen();
            }
        }

        private void AktionSpeichern_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAktion == null) return;
            _selectedAktion.CName = txtAktionName.Text;
            _selectedAktion.ParameterPreview = txtAktionParameter.Text;
            dgAktionen.Items.Refresh();
        }

        private void RenumberAktionen()
        {
            for (int i = 0; i < _aktionen.Count; i++)
                _aktionen[i].NPos = i + 1;
        }

        #endregion

        #region Bedingungen

        private void AddBedingung_Click(object sender, RoutedEventArgs e)
        {
            var bedingung = new JtlBedingungEdit
            {
                NPos = _bedingungen.Count + 1,
                Eigenschaft = "Status",
                NOperator = 0,
                CVergleichswert = "1"
            };
            _bedingungen.Add(bedingung);
        }

        private void Bedingung_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            btnBedingungLoeschen.IsEnabled = dgBedingungen.SelectedItem != null;
        }

        private void BedingungLoeschen_Click(object sender, RoutedEventArgs e)
        {
            if (dgBedingungen.SelectedItem is JtlBedingungEdit bed)
            {
                _bedingungen.Remove(bed);
                for (int i = 0; i < _bedingungen.Count; i++)
                    _bedingungen[i].NPos = i + 1;
            }
        }

        #endregion

        private async void Speichern_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Bitte geben Sie einen Namen ein.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (cmbObjekt.SelectedItem is not ComboBoxItem objektItem)
            {
                MessageBox.Show("Bitte waehlen Sie ein Objekt.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var conn = await _db.GetConnectionAsync();
                var objekt = int.Parse(objektItem.Tag?.ToString() ?? "6");
                var typ = int.Parse((cmbTyp.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "0");
                var eventId = cmbEvent.SelectedValue is int ev ? ev : 1;

                int workflowId;

                if (_workflowId.HasValue)
                {
                    // Update
                    await conn.ExecuteAsync(@"
                        UPDATE dbo.tWorkflow SET
                            cName = @Name, nEvent = @Event, nObjekt = @Objekt, nTyp = @Typ
                        WHERE kWorkflow = @Id",
                        new { Id = _workflowId, Name = txtName.Text.Trim(), Event = eventId, Objekt = objekt, Typ = typ });
                    workflowId = _workflowId.Value;

                    // Alte Aktionen loeschen
                    await conn.ExecuteAsync("DELETE FROM dbo.tWorkflowAktion WHERE kWorkflow = @Id", new { Id = workflowId });
                    await conn.ExecuteAsync("DELETE FROM dbo.tWorkflowBedingung WHERE kWorkflow = @Id", new { Id = workflowId });
                }
                else
                {
                    // Insert
                    workflowId = await conn.QuerySingleAsync<int>(@"
                        INSERT INTO dbo.tWorkflow (cName, nEvent, nObjekt, nVerknuepfung, nPos, nTyp, nApplikation)
                        VALUES (@Name, @Event, @Objekt, 0, 0, @Typ, 0);
                        SELECT SCOPE_IDENTITY()",
                        new { Name = txtName.Text.Trim(), Event = eventId, Objekt = objekt, Typ = typ });
                }

                // Aktionen speichern
                foreach (var aktion in _aktionen)
                {
                    var xml = aktion.BuildXml();
                    await conn.ExecuteAsync(@"
                        INSERT INTO dbo.tWorkflowAktion (kWorkflow, nPos, cName, xXmlObjekt)
                        VALUES (@WorkflowId, @Pos, @Name, @Xml)",
                        new { WorkflowId = workflowId, Pos = aktion.NPos, Name = aktion.CName, Xml = xml });
                }

                // Bedingungen speichern (vereinfacht)
                foreach (var bed in _bedingungen)
                {
                    await conn.ExecuteAsync(@"
                        INSERT INTO dbo.tWorkflowBedingung (kWorkflow, nPos, nOperator, cVergleichswert, kWorkflowEigenschaft)
                        VALUES (@WorkflowId, @Pos, @Op, @Wert, 0)",
                        new { WorkflowId = workflowId, Pos = bed.NPos, Op = bed.NOperator, Wert = bed.CVergleichswert });
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Abbrechen_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    #region DTOs

    public class JtlEvent
    {
        public int NEvent { get; set; }
        public int NObjekt { get; set; }
        public string CDisplayName { get; set; } = "";
    }

    public class JtlAktionEdit
    {
        public int KWorkflowAktion { get; set; }
        public int KWorkflow { get; set; }
        public int NPos { get; set; }
        public string? CName { get; set; }
        public string AktionTyp { get; set; } = "SQL";
        public string? XmlObjekt { get; set; }
        public string ParameterPreview { get; set; } = "";

        public void ExtractParameter()
        {
            if (string.IsNullOrEmpty(XmlObjekt)) return;

            // Versuche SQL Statement zu extrahieren
            if (XmlObjekt.Contains("<SqlStatement>"))
            {
                var start = XmlObjekt.IndexOf("<SqlStatement>") + 14;
                var end = XmlObjekt.IndexOf("</SqlStatement>");
                if (end > start)
                    ParameterPreview = XmlObjekt.Substring(start, end - start).Replace("&lt;", "<").Replace("&gt;", ">");
            }
            else if (XmlObjekt.Contains("<Pfad>"))
            {
                var start = XmlObjekt.IndexOf("<Pfad>") + 6;
                var end = XmlObjekt.IndexOf("</Pfad>");
                if (end > start)
                    ParameterPreview = XmlObjekt.Substring(start, end - start);
            }
        }

        public string BuildXml()
        {
            switch (AktionTyp)
            {
                case "SQL":
                    var escapedSql = (ParameterPreview ?? "").Replace("<", "&lt;").Replace(">", "&gt;");
                    return $@"<jtlAktion xmlns=""jtlCore.Workflows.Aktionen"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:a=""http://schemas.datacontract.org/2004/07/jtlCore.Workflows.Aktionen"" i:type=""a:jtlAktionSQL""><CancelOnError>false</CancelOnError><WawiVersion>1</WawiVersion><SqlStatement>{escapedSql}</SqlStatement></jtlAktion>";

                case "Datei":
                    return $@"<jtlAktion xmlns=""jtlCore.Workflows.Aktionen"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:a=""http://schemas.datacontract.org/2004/07/jtlCore.Workflows.Aktionen"" i:type=""a:jtlAktionDateiSchreiben""><CancelOnError>false</CancelOnError><WawiVersion>1</WawiVersion><Pfad>{ParameterPreview}</Pfad><Inhalt></Inhalt></jtlAktion>";

                default:
                    return $@"<jtlAktion xmlns=""jtlCore.Workflows.Aktionen"" xmlns:i=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:a=""http://schemas.datacontract.org/2004/07/jtlCore.Workflows.Aktionen"" i:type=""a:jtlAktionWertSetzen""><CancelOnError>false</CancelOnError><WawiVersion>1</WawiVersion></jtlAktion>";
            }
        }
    }

    public class JtlBedingungEdit
    {
        public int KWorkflowBedingung { get; set; }
        public int KWorkflow { get; set; }
        public int NPos { get; set; }
        public string Eigenschaft { get; set; } = "";
        public int NOperator { get; set; }
        public string? CVergleichswert { get; set; }
        public int KWorkflowEigenschaft { get; set; }
    }

    #endregion
}

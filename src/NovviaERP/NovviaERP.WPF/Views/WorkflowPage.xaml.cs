using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using NovviaERP.Core.Data;

namespace NovviaERP.WPF.Views
{
    public partial class WorkflowPage : UserControl
    {
        private readonly JtlDbContext _db;
        private List<JtlWorkflowView> _jtlWorkflows = new();

        public WorkflowPage()
        {
            InitializeComponent();
            _db = new JtlDbContext(App.ConnectionString!);

            Loaded += async (s, e) => await LadeJtlWorkflowsAsync();
        }

        private async void Aktualisieren_Click(object sender, RoutedEventArgs e)
        {
            await LadeJtlWorkflowsAsync();
        }

        #region JTL Workflows

        private async System.Threading.Tasks.Task LadeJtlWorkflowsAsync()
        {
            try
            {
                var conn = await _db.GetConnectionAsync();
                _jtlWorkflows = (await conn.QueryAsync<JtlWorkflowView>(@"
                    SELECT w.kWorkflow, w.cName, w.nEvent, w.nObjekt, w.nTyp,
                           COALESCE(e.cDisplayName, 'Event ' + CAST(w.nEvent AS VARCHAR)) AS EventName,
                           CASE w.nObjekt
                               WHEN 5 THEN 'Kunde'
                               WHEN 6 THEN 'Auftrag'
                               WHEN 7 THEN 'Lieferschein'
                               WHEN 8 THEN 'Rechnung'
                               WHEN 9 THEN 'Artikel'
                               ELSE 'Objekt ' + CAST(w.nObjekt AS VARCHAR)
                           END AS ObjektName,
                           CASE w.nTyp
                               WHEN 0 THEN 'Standard'
                               WHEN 1 THEN 'Manuell'
                               WHEN 2 THEN 'Zeitgesteuert'
                               ELSE 'Typ ' + CAST(w.nTyp AS VARCHAR)
                           END AS TypName,
                           (SELECT COUNT(*) FROM dbo.tWorkflowAktion WHERE kWorkflow = w.kWorkflow) AS AktionenCount,
                           (SELECT COUNT(*) FROM dbo.tWorkflowBedingung WHERE kWorkflow = w.kWorkflow) AS BedingungenCount
                    FROM dbo.tWorkflow w
                    LEFT JOIN dbo.tWorkflowEvent e ON w.nEvent = e.nEvent AND w.nObjekt = e.nObjekt
                    ORDER BY w.cName")).ToList();

                ApplyJtlFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden der JTL Workflows:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyJtlFilter()
        {
            var filtered = _jtlWorkflows.AsEnumerable();

            if (cmbJtlObjektFilter.SelectedItem is ComboBoxItem item && !string.IsNullOrEmpty(item.Tag?.ToString()))
            {
                var objekt = int.Parse(item.Tag.ToString()!);
                filtered = filtered.Where(w => w.NObjekt == objekt);
            }

            dgJtlWorkflows.ItemsSource = filtered.ToList();
        }

        private void JtlFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            ApplyJtlFilter();
        }

        private async void JtlWorkflow_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = dgJtlWorkflows.SelectedItem is JtlWorkflowView;
            btnJtlBearbeiten.IsEnabled = selected;
            btnJtlLoeschen.IsEnabled = selected;

            if (dgJtlWorkflows.SelectedItem is JtlWorkflowView wf)
            {
                await LadeJtlWorkflowDetailsAsync(wf.KWorkflow);
            }
            else
            {
                dgJtlAktionen.ItemsSource = null;
                dgJtlBedingungen.ItemsSource = null;
            }
        }

        private async System.Threading.Tasks.Task LadeJtlWorkflowDetailsAsync(int workflowId)
        {
            try
            {
                var conn = await _db.GetConnectionAsync();

                // Aktionen laden
                var aktionen = await conn.QueryAsync<JtlWorkflowAktion>(@"
                    SELECT kWorkflowAktion, kWorkflow, nPos, cName,
                           CASE
                               WHEN CAST(xXmlObjekt AS NVARCHAR(MAX)) LIKE '%jtlAktionWertSetzen%' THEN 'Wert setzen'
                               WHEN CAST(xXmlObjekt AS NVARCHAR(MAX)) LIKE '%jtlAktionDateiSchreiben%' THEN 'Datei schreiben'
                               WHEN CAST(xXmlObjekt AS NVARCHAR(MAX)) LIKE '%jtlAktionMail%' THEN 'E-Mail senden'
                               WHEN CAST(xXmlObjekt AS NVARCHAR(MAX)) LIKE '%jtlAktionSQL%' THEN 'SQL ausfuehren'
                               WHEN CAST(xXmlObjekt AS NVARCHAR(MAX)) LIKE '%jtlAktionWebRequest%' THEN 'Web-Request'
                               WHEN CAST(xXmlObjekt AS NVARCHAR(MAX)) LIKE '%jtlAktionDotLiquid%' THEN 'DotLiquid'
                               ELSE 'Unbekannt'
                           END AS AktionTyp
                    FROM dbo.tWorkflowAktion
                    WHERE kWorkflow = @Id
                    ORDER BY nPos", new { Id = workflowId });
                dgJtlAktionen.ItemsSource = aktionen;

                // Bedingungen laden
                var bedingungen = await conn.QueryAsync<JtlWorkflowBedingung>(@"
                    SELECT b.kWorkflowBedingung, b.kWorkflow, b.nPos, b.cVergleichswert, b.nOperator,
                           COALESCE(e.cName, 'Eigenschaft ' + CAST(b.kWorkflowEigenschaft AS VARCHAR)) AS Eigenschaft,
                           CASE b.nOperator
                               WHEN 0 THEN '='
                               WHEN 1 THEN '!='
                               WHEN 2 THEN '>'
                               WHEN 3 THEN '<'
                               WHEN 4 THEN '>='
                               WHEN 5 THEN '<='
                               WHEN 6 THEN 'enthaelt'
                               WHEN 7 THEN 'beginnt'
                               WHEN 8 THEN 'endet'
                               ELSE '?'
                           END AS OperatorText
                    FROM dbo.tWorkflowBedingung b
                    LEFT JOIN dbo.tWorkflowEigenschaft e ON b.kWorkflowEigenschaft = e.kWorkflowEigenschaft
                    WHERE b.kWorkflow = @Id
                    ORDER BY b.nPos", new { Id = workflowId });
                dgJtlBedingungen.ItemsSource = bedingungen;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Laden der Details: {ex.Message}");
            }
        }

        private void JtlWorkflow_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgJtlWorkflows.SelectedItem is JtlWorkflowView wf)
                JtlWorkflowBearbeiten(wf);
        }

        private void NeuerJtlWorkflow_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new JtlWorkflowDialog();
            if (dialog.ShowDialog() == true)
            {
                _ = LadeJtlWorkflowsAsync();
            }
        }

        private void JtlWorkflowBearbeiten_Click(object sender, RoutedEventArgs e)
        {
            if (dgJtlWorkflows.SelectedItem is JtlWorkflowView wf)
                JtlWorkflowBearbeiten(wf);
        }

        private void JtlWorkflowBearbeiten(JtlWorkflowView wf)
        {
            var dialog = new JtlWorkflowDialog(wf.KWorkflow);
            if (dialog.ShowDialog() == true)
            {
                _ = LadeJtlWorkflowsAsync();
            }
        }

        private async void JtlWorkflowLoeschen_Click(object sender, RoutedEventArgs e)
        {
            if (dgJtlWorkflows.SelectedItem is not JtlWorkflowView wf) return;

            var result = MessageBox.Show(
                $"JTL Workflow '{wf.CName}' wirklich loeschen?\n\nAlle Aktionen und Bedingungen werden ebenfalls geloescht!",
                "JTL Workflow loeschen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                var conn = await _db.GetConnectionAsync();
                await conn.ExecuteAsync("DELETE FROM dbo.tWorkflowAktion WHERE kWorkflow = @Id", new { Id = wf.KWorkflow });
                await conn.ExecuteAsync("DELETE FROM dbo.tWorkflowBedingung WHERE kWorkflow = @Id", new { Id = wf.KWorkflow });
                await conn.ExecuteAsync("DELETE FROM dbo.tWorkflow WHERE kWorkflow = @Id", new { Id = wf.KWorkflow });

                await LadeJtlWorkflowsAsync();
                MessageBox.Show("JTL Workflow wurde geloescht.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Loeschen:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }

    #region DTOs

    public class JtlWorkflowView
    {
        public int KWorkflow { get; set; }
        public string CName { get; set; } = "";
        public int NEvent { get; set; }
        public int NObjekt { get; set; }
        public int NTyp { get; set; }
        public string EventName { get; set; } = "";
        public string ObjektName { get; set; } = "";
        public string TypName { get; set; } = "";
        public int AktionenCount { get; set; }
        public int BedingungenCount { get; set; }
    }

    public class JtlWorkflowAktion
    {
        public int KWorkflowAktion { get; set; }
        public int KWorkflow { get; set; }
        public int NPos { get; set; }
        public string? CName { get; set; }
        public string AktionTyp { get; set; } = "";
    }

    public class JtlWorkflowBedingung
    {
        public int KWorkflowBedingung { get; set; }
        public int KWorkflow { get; set; }
        public int NPos { get; set; }
        public string? Eigenschaft { get; set; }
        public int NOperator { get; set; }
        public string OperatorText { get; set; } = "";
        public string? CVergleichswert { get; set; }
    }

    #endregion
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Dapper;
using NovviaERP.Core.Data;

namespace NovviaERP.WPF.Views
{
    public partial class WorkflowPage : UserControl
    {
        private readonly JtlDbContext _db;
        private List<NovviaWorkflow> _workflows = new();

        public WorkflowPage()
        {
            InitializeComponent();
            _db = new JtlDbContext(App.ConnectionString!);

            Loaded += async (s, e) => await LadeAllesAsync();
        }

        private async System.Threading.Tasks.Task LadeAllesAsync()
        {
            await LadeWorkflowsAsync();
            await LadeJtlWorkflowsAsync();
            await LadeLogAsync();
        }

        #region NOVVIA Workflows

        private async System.Threading.Tasks.Task LadeWorkflowsAsync()
        {
            try
            {
                var conn = await _db.GetConnectionAsync();
                _workflows = (await conn.QueryAsync<NovviaWorkflow>(
                    "SELECT * FROM NOVVIA.Workflow ORDER BY nPrioritaet, cName")).ToList();

                ApplyFilter();

                // ComboBox fuer Log-Filter befuellen
                var workflowsMitAlle = new List<NovviaWorkflow> { new() { KWorkflow = 0, CName = "Alle" } };
                workflowsMitAlle.AddRange(_workflows);
                cmbLogWorkflow.ItemsSource = workflowsMitAlle;
                cmbLogWorkflow.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden der Workflows:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilter()
        {
            var filtered = _workflows.AsEnumerable();

            if (cmbEreignisFilter.SelectedItem is ComboBoxItem ereignisItem &&
                !string.IsNullOrEmpty(ereignisItem.Tag?.ToString()))
            {
                filtered = filtered.Where(w => w.CEreignis == ereignisItem.Tag.ToString());
            }

            if (cmbStatusFilter.SelectedItem is ComboBoxItem statusItem &&
                !string.IsNullOrEmpty(statusItem.Tag?.ToString()))
            {
                var aktiv = statusItem.Tag.ToString() == "1";
                filtered = filtered.Where(w => w.NAktiv == aktiv);
            }

            dgWorkflows.ItemsSource = filtered.ToList();
        }

        private void Filter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            ApplyFilter();
        }

        private void Workflow_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Buttons aktivieren/deaktivieren je nach Auswahl
        }

        private void Workflow_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (dgWorkflows.SelectedItem is NovviaWorkflow workflow)
                WorkflowBearbeiten(workflow);
        }

        private void NeuerWorkflow_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WorkflowDialog();
            if (dialog.ShowDialog() == true)
            {
                _ = LadeWorkflowsAsync();
            }
        }

        private void WorkflowBearbeiten_Click(object sender, RoutedEventArgs e)
        {
            if (dgWorkflows.SelectedItem is NovviaWorkflow workflow)
                WorkflowBearbeiten(workflow);
        }

        private void WorkflowBearbeiten(NovviaWorkflow workflow)
        {
            var dialog = new WorkflowDialog(workflow);
            if (dialog.ShowDialog() == true)
            {
                _ = LadeWorkflowsAsync();
            }
        }

        private async void WorkflowDuplizieren_Click(object sender, RoutedEventArgs e)
        {
            if (dgWorkflows.SelectedItem is not NovviaWorkflow workflow) return;

            try
            {
                var conn = await _db.GetConnectionAsync();
                await conn.ExecuteAsync(@"
                    INSERT INTO NOVVIA.Workflow (cName, cBeschreibung, cEreignis, cBedingung, cAktionTyp, cAktionParameter, nAktiv, nPrioritaet)
                    SELECT cName + ' (Kopie)', cBeschreibung, cEreignis, cBedingung, cAktionTyp, cAktionParameter, 0, nPrioritaet
                    FROM NOVVIA.Workflow WHERE kWorkflow = @Id", new { Id = workflow.KWorkflow });

                await LadeWorkflowsAsync();
                MessageBox.Show("Workflow wurde dupliziert.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Duplizieren:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void WorkflowAktivieren_Click(object sender, RoutedEventArgs e)
        {
            await SetWorkflowAktiv(true);
        }

        private async void WorkflowDeaktivieren_Click(object sender, RoutedEventArgs e)
        {
            await SetWorkflowAktiv(false);
        }

        private async System.Threading.Tasks.Task SetWorkflowAktiv(bool aktiv)
        {
            if (dgWorkflows.SelectedItem is not NovviaWorkflow workflow) return;

            try
            {
                var conn = await _db.GetConnectionAsync();
                await conn.ExecuteAsync(
                    "UPDATE NOVVIA.Workflow SET nAktiv = @Aktiv, dGeaendert = GETDATE() WHERE kWorkflow = @Id",
                    new { Aktiv = aktiv, Id = workflow.KWorkflow });

                await LadeWorkflowsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void WorkflowLoeschen_Click(object sender, RoutedEventArgs e)
        {
            if (dgWorkflows.SelectedItem is not NovviaWorkflow workflow) return;

            var result = MessageBox.Show(
                $"Workflow '{workflow.CName}' wirklich loeschen?\n\nDas Ausfuehrungsprotokoll wird ebenfalls geloescht.",
                "Workflow loeschen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                var conn = await _db.GetConnectionAsync();
                await conn.ExecuteAsync("DELETE FROM NOVVIA.WorkflowLog WHERE kWorkflow = @Id", new { Id = workflow.KWorkflow });
                await conn.ExecuteAsync("DELETE FROM NOVVIA.Workflow WHERE kWorkflow = @Id", new { Id = workflow.KWorkflow });

                await LadeWorkflowsAsync();
                MessageBox.Show("Workflow wurde geloescht.", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Loeschen:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Aktualisieren_Click(object sender, RoutedEventArgs e)
        {
            await LadeAllesAsync();
        }

        #endregion

        #region JTL Workflows

        private List<JtlWorkflowView> _jtlWorkflows = new();

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
                System.Diagnostics.Debug.WriteLine($"JTL Workflows konnten nicht geladen werden: {ex.Message}");
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

        #region Log

        private async System.Threading.Tasks.Task LadeLogAsync()
        {
            try
            {
                var conn = await _db.GetConnectionAsync();

                int? workflowId = null;
                if (cmbLogWorkflow.SelectedValue is int wfId && wfId > 0)
                    workflowId = wfId;

                bool? erfolgreich = null;
                if (cmbLogStatus.SelectedItem is ComboBoxItem statusItem && !string.IsNullOrEmpty(statusItem.Tag?.ToString()))
                    erfolgreich = statusItem.Tag.ToString() == "1";

                var sql = @"
                    SELECT l.*, w.cName AS WorkflowName
                    FROM NOVVIA.WorkflowLog l
                    LEFT JOIN NOVVIA.Workflow w ON l.kWorkflow = w.kWorkflow
                    WHERE 1=1";

                if (workflowId.HasValue)
                    sql += " AND l.kWorkflow = @WorkflowId";
                if (erfolgreich.HasValue)
                    sql += " AND l.nErfolgreich = @Erfolgreich";

                sql += " ORDER BY l.dAusgefuehrt DESC";

                var logs = await conn.QueryAsync<WorkflowLogEntry>(sql, new { WorkflowId = workflowId, Erfolgreich = erfolgreich });
                dgLog.ItemsSource = logs;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden des Logs:\n{ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LogFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            await LadeLogAsync();
        }

        private async void LogAktualisieren_Click(object sender, RoutedEventArgs e)
        {
            await LadeLogAsync();
        }

        #endregion
    }

    #region DTOs

    public class NovviaWorkflow
    {
        public int KWorkflow { get; set; }
        public string CName { get; set; } = "";
        public string? CBeschreibung { get; set; }
        public string CEreignis { get; set; } = "";
        public string? CBedingung { get; set; }
        public string CAktionTyp { get; set; } = "";
        public string? CAktionParameter { get; set; }
        public bool NAktiv { get; set; } = true;
        public int NPrioritaet { get; set; } = 100;
        public DateTime DErstellt { get; set; }
        public DateTime DGeaendert { get; set; }
    }

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

    public class WorkflowLogEntry
    {
        public int KLog { get; set; }
        public int KWorkflow { get; set; }
        public string? WorkflowName { get; set; }
        public string? CEreignis { get; set; }
        public string? CReferenz { get; set; }
        public bool NErfolgreich { get; set; }
        public string? CFehler { get; set; }
        public DateTime DAusgefuehrt { get; set; }
    }

    #endregion

    #region Converters

    public class BoolToStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? "OK" : "Fehler";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? Brushes.Green : Brushes.Red;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    #endregion
}

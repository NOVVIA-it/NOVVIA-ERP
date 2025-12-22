using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NovviaERP.Core.Data;
using Dapper;

namespace NovviaERP.WPF.Views
{
    public partial class WorkerControlPage : Page
    {
        private readonly JtlDbContext _db;
        public ObservableCollection<WorkerViewModel> Workers { get; } = new();
        public ObservableCollection<string> LogEintraege { get; } = new();
        private DispatcherTimer _refreshTimer;

        public WorkerControlPage(JtlDbContext db)
        {
            _db = db;
            InitializeComponent();
            
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _refreshTimer.Tick += async (s, e) => await LoadDataAsync();
            
            Loaded += async (s, e) =>
            {
                await LoadDataAsync();
                _refreshTimer.Start();
            };
            Unloaded += (s, e) => _refreshTimer.Stop();
        }

        private async Task LoadDataAsync()
        {
            try
            {
                var conn = await _db.GetConnectionAsync();
                var workers = await conn.QueryAsync<WorkerStatus>(
                    "SELECT * FROM tWorkerStatus ORDER BY cWorker");

                Workers.Clear();
                foreach (var w in workers)
                {
                    Workers.Add(new WorkerViewModel(w));
                }
                dgWorker.ItemsSource = Workers;

                // Letzte Log-Einträge
                var logs = await conn.QueryAsync<string>(
                    "SELECT TOP 50 CONCAT(FORMAT(dZeitpunkt, 'HH:mm:ss'), ' [', cLevel, '] ', cWorker, ': ', cNachricht) FROM tWorkerLog ORDER BY dZeitpunkt DESC");
                
                LogEintraege.Clear();
                foreach (var log in logs)
                    LogEintraege.Add(log);
                lstLog.ItemsSource = LogEintraege;
            }
            catch { /* Ignore refresh errors */ }
        }

        private async void BtnAlleStarten_Click(object sender, RoutedEventArgs e)
        {
            var conn = await _db.GetConnectionAsync();
            await conn.ExecuteAsync("UPDATE tWorkerStatus SET nStatus = 1 WHERE nAktiv = 1");
            await LoadDataAsync();
            AddLog("INFO", "Alle Worker gestartet");
        }

        private async void BtnAlleStoppen_Click(object sender, RoutedEventArgs e)
        {
            var conn = await _db.GetConnectionAsync();
            await conn.ExecuteAsync("UPDATE tWorkerStatus SET nStatus = 0");
            await LoadDataAsync();
            AddLog("INFO", "Alle Worker gestoppt");
        }

        private async void BtnWorkerAusfuehren_Click(object sender, RoutedEventArgs e)
        {
            var worker = GetSelectedWorker();
            if (worker == null) return;

            AddLog("INFO", $"Worker '{worker.Name}' wird manuell ausgeführt...");
            
            // TODO: Tatsächliche Worker-Ausführung
            await Task.Delay(1000); // Simulation
            
            var conn = await _db.GetConnectionAsync();
            await conn.ExecuteAsync(
                "UPDATE tWorkerStatus SET dLetzterLauf = GETDATE(), nLaufzeit_ms = 1000 WHERE cWorker = @Name",
                new { worker.Name });
            
            AddLog("INFO", $"Worker '{worker.Name}' abgeschlossen");
            await LoadDataAsync();
        }

        private async void BtnWorkerPausieren_Click(object sender, RoutedEventArgs e)
        {
            var worker = GetSelectedWorker();
            if (worker == null) return;

            var conn = await _db.GetConnectionAsync();
            await conn.ExecuteAsync(
                "UPDATE tWorkerStatus SET nAktiv = CASE WHEN nAktiv = 1 THEN 0 ELSE 1 END WHERE cWorker = @Name",
                new { worker.Name });
            await LoadDataAsync();
        }

        private void BtnWorkerKonfig_Click(object sender, RoutedEventArgs e)
        {
            var worker = GetSelectedWorker();
            if (worker == null) return;
            MessageBox.Show($"Konfiguration für '{worker.Name}' (TODO)", "Konfiguration");
        }

        private async void BtnWorkerLog_Click(object sender, RoutedEventArgs e)
        {
            var worker = GetSelectedWorker();
            if (worker == null) return;

            var conn = await _db.GetConnectionAsync();
            var logs = await conn.QueryAsync<string>(
                "SELECT TOP 100 CONCAT(FORMAT(dZeitpunkt, 'dd.MM. HH:mm:ss'), ' [', cLevel, '] ', cNachricht) FROM tWorkerLog WHERE cWorker = @Name ORDER BY dZeitpunkt DESC",
                new { worker.Name });

            MessageBox.Show(string.Join("\n", logs), $"Log: {worker.Name}");
        }

        private void BtnLogLoeschen_Click(object sender, RoutedEventArgs e)
        {
            LogEintraege.Clear();
        }

        private void AddLog(string level, string nachricht)
        {
            LogEintraege.Insert(0, $"{DateTime.Now:HH:mm:ss} [{level}] {nachricht}");
        }

        private WorkerViewModel? GetSelectedWorker()
        {
            return dgWorker.SelectedItem as WorkerViewModel;
        }
    }

    public class WorkerStatus
    {
        public string Worker { get; set; } = "";
        public int Status { get; set; }
        public DateTime? LetzterLauf { get; set; }
        public int? Laufzeit_ms { get; set; }
        public string? LetzteFehler { get; set; }
        public bool Aktiv { get; set; }
    }

    public class WorkerViewModel
    {
        private readonly WorkerStatus _status;
        public WorkerViewModel(WorkerStatus s) => _status = s;

        public string Name => _status.Worker;
        public bool Aktiv => _status.Aktiv;
        public int Status => _status.Status;

        public string Beschreibung => Name switch
        {
            "Zahlungsabgleich" => "Prüft HBCI/PayPal auf neue Zahlungseingänge",
            "WooCommerce-Sync" => "Synchronisiert Bestellungen und Lagerbestände",
            "Mahnlauf" => "Erstellt automatische Mahnungen",
            "Lagerbestand-Prüfung" => "Warnt bei niedrigem Bestand",
            "USt-ID-Validierung" => "Prüft USt-IDs bei VIES",
            "Workflow-Queue" => "Verarbeitet ausstehende Workflows",
            "Cleanup" => "Bereinigt alte Logs und temporäre Daten",
            _ => "-"
        };

        public string LetzterLaufText => _status.LetzterLauf?.ToString("dd.MM. HH:mm:ss") ?? "Noch nie";
        public string LaufzeitText => _status.Laufzeit_ms.HasValue ? $"{_status.Laufzeit_ms}ms" : "-";

        public Brush StatusFarbe => Status switch
        {
            0 => Brushes.Gray,      // Gestoppt
            1 => Brushes.LimeGreen, // Läuft
            2 => Brushes.Orange,    // Wartet
            3 => Brushes.Red,       // Fehler
            _ => Brushes.Gray
        };
    }
}

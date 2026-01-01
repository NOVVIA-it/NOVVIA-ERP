using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Data;
using NovviaERP.Core.Services;
using NovviaERP.WPF.Services;
using NovviaERP.WPF.Views;
using Serilog;

namespace NovviaERP.WPF
{
    public partial class App : Application
    {
        public static IServiceProvider Services { get; private set; } = null!;

        // Shortcut für JtlDbContext (für Abwärtskompatibilität)
        public static JtlDbContext Db => Services.GetRequiredService<JtlDbContext>();

        // Aktuelle Session-Infos
        public static string? ConnectionString { get; private set; }
        public static string? MandantName { get; private set; }
        public static string? Benutzername { get; private set; }
        public static int BenutzerId { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Serilog konfigurieren - Logs in C:\NovviaERP\logs
            var logPath = @"C:\NovviaERP\logs";
            Directory.CreateDirectory(logPath);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    Path.Combine(logPath, "novviaerp-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            // Globaler Exception Handler
            DispatcherUnhandledException += (s, args) =>
            {
                var ex = args.Exception;
                var msg = $"Unhandled Exception:\n\n{ex.Message}";
                if (ex.InnerException != null)
                    msg += $"\n\nInner: {ex.InnerException.Message}";
                msg += $"\n\nStackTrace:\n{ex.StackTrace}";
                System.Windows.MessageBox.Show(msg, "Fehler", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                args.Handled = true; // Verhindert Absturz
            };

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                var msg = $"Fatal Exception:\n\n{ex?.Message ?? "Unknown"}";
                if (ex?.InnerException != null)
                    msg += $"\n\nInner: {ex.InnerException.Message}";
                msg += $"\n\nStackTrace:\n{ex?.StackTrace}";
                System.Windows.MessageBox.Show(msg, "Fataler Fehler", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            };

            // Login-Fenster anzeigen
            var loginWindow = new LoginWindow();
            var result = loginWindow.ShowDialog();

            if (result != true || string.IsNullOrEmpty(loginWindow.ConnectionString))
            {
                Shutdown();
                return;
            }

            // Session-Infos speichern
            ConnectionString = loginWindow.ConnectionString;
            MandantName = loginWindow.MandantName;
            Benutzername = loginWindow.Benutzername;
            BenutzerId = loginWindow.BenutzerId;

            try
            {
                // DI Container konfigurieren
                var services = new ServiceCollection();
                ConfigureServices(services);
                Services = services.BuildServiceProvider();

                // Theme-Einstellungen laden (pro Mandant aus NOVVIA.Config)
                _ = ThemeService.LoadSettingsAsync(ConnectionString!);

                // Hauptfenster öffnen
                var mainWindow = new MainWindow();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                var msg = $"Fehler: {ex.Message}";
                if (ex.InnerException != null)
                    msg += $"\n\nInner: {ex.InnerException.Message}";
                if (ex.InnerException?.InnerException != null)
                    msg += $"\n\nInner2: {ex.InnerException.InnerException.Message}";
                msg += $"\n\nStackTrace: {ex.StackTrace?.Substring(0, Math.Min(500, ex.StackTrace?.Length ?? 0))}";
                System.Windows.MessageBox.Show(msg, "Startfehler", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                Shutdown();
            }
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Datenbank-Context mit aktuellem Connection String
            services.AddSingleton<JtlDbContext>(sp => new JtlDbContext(ConnectionString!));

            // Services
            services.AddTransient<AngebotService>();
            services.AddTransient<AusgabeService>();
            services.AddTransient<AuftragsImportService>();
            services.AddTransient<PlattformService>();
            services.AddTransient<EmailVorlageService>();
            services.AddTransient<ReportService>();
            services.AddTransient<DruckerService>();
            services.AddTransient<ShippingService>();
            services.AddTransient<WooCommerceService>();

            // PaymentConfig aus JSON-Datei laden oder Standard verwenden
            var paymentConfigPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NovviaERP", "payment-config.json");
            PaymentConfig paymentConfig;
            if (File.Exists(paymentConfigPath))
            {
                try
                {
                    var json = File.ReadAllText(paymentConfigPath);
                    paymentConfig = System.Text.Json.JsonSerializer.Deserialize<PaymentConfig>(json) ?? new PaymentConfig();
                }
                catch
                {
                    paymentConfig = new PaymentConfig();
                }
            }
            else
            {
                paymentConfig = new PaymentConfig();
            }
            services.AddSingleton(paymentConfig);
            services.AddTransient<PaymentService>();

            services.AddTransient<StammdatenService>();
            services.AddTransient<WorkflowService>();

            // Einkauf / Pharma Services
            services.AddSingleton(sp => new EinkaufService(ConnectionString!));
            services.AddSingleton(sp => new MSV3Service(ConnectionString!));
            services.AddSingleton(sp => new ABdataService(ConnectionString!));

            // Core Service (Kunden, Artikel, Bestellungen)
            services.AddSingleton(sp => new CoreService(ConnectionString!));

            // Zahlungs-Services
            services.AddSingleton(sp => new ZahlungsabgleichService(sp.GetRequiredService<JtlDbContext>()));
            services.AddSingleton(sp => new SepaService(sp.GetRequiredService<JtlDbContext>()));

            // Log-Service (für zentrale Protokollierung)
            services.AddSingleton(sp => new LogService(sp.GetRequiredService<JtlDbContext>()));

            // AppData Service (lokale Einstellungen, Mappings, Cache)
            services.AddSingleton<AppDataService>();

            // Eigene Felder Service
            services.AddTransient<EigeneFelderService>();

            // Pages (legacy)
            services.AddTransient<MainWindow>();
            services.AddTransient<DashboardPage>();
            services.AddTransient<KundenPage>();
            services.AddTransient<ArtikelPage>();
            services.AddTransient<BestellungenPage>();
            services.AddTransient<RechnungenPage>();
            services.AddTransient<LagerPage>();
            services.AddTransient<VersandPage>();
            services.AddTransient<PlattformPage>();
            services.AddTransient<EmailVorlagenPage>();
            services.AddTransient<WorkerControlPage>();
            services.AddTransient<EinstellungenPage>();
            services.AddTransient<LieferantenPage>();

            // UserControl Views (new - no Frame blocking issues)
            services.AddTransient<ArtikelView>();
            services.AddTransient<ArtikelDetailView>();
            services.AddTransient<KundenView>();
            services.AddTransient<KundeDetailView>();
            services.AddTransient<BestellungenView>();
            services.AddTransient<BestellungDetailView>();
            services.AddTransient<RechnungenView>();
            services.AddTransient<LieferantenView>();
            services.AddTransient<LagerView>();
            services.AddTransient<LagerChargenView>();
            services.AddTransient<VersandView>();
            services.AddTransient<ImportView>();
            services.AddTransient<EigeneFelderView>();
            services.AddTransient<EinstellungenView>();
            services.AddTransient<TestView>();
            services.AddTransient<AmeiseView>();
        }
    }
}

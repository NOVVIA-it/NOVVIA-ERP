using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NovviaERP.Core.Data;
using NovviaERP.Worker.Jobs;

// CLI-Argumente Parser
static string? GetArg(string[] args, string key)
{
    var idx = Array.FindIndex(args, a => string.Equals(a, key, StringComparison.OrdinalIgnoreCase));
    if (idx < 0 || idx + 1 >= args.Length) return null;
    return args[idx + 1];
}

// CLI-Modus prüfen
var mode = GetArg(args, "--mode");

if (string.Equals(mode, "msv3-stock", StringComparison.OrdinalIgnoreCase))
{
    // MSV3 Bestandsabfrage für einzelne PZN
    var pzn = GetArg(args, "--pzn");
    if (string.IsNullOrWhiteSpace(pzn))
    {
        Console.Error.WriteLine("Fehler: --pzn Parameter fehlt");
        Console.Error.WriteLine("Verwendung: NovviaERP.Worker.exe --mode msv3-stock --pzn 14036711");
        return 2;
    }

    var config = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true)
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .AddEnvironmentVariables()
        .Build();

    var cs = config.GetConnectionString("JtlWawi");
    if (string.IsNullOrWhiteSpace(cs))
    {
        // Fallback: Standard Mandant_2 Connection String
        cs = "Server=JTL-DB;Database=Mandant_2;Integrated Security=True;TrustServerCertificate=True;";
        Console.WriteLine($"[INFO] Verwende Standard Connection String");
    }

    var job = new Msv3SinglePznJob(cs);
    return await job.RunAsync(pzn.Trim());
}

// Normaler Worker-Modus (Hosted Services)
var builder = Host.CreateApplicationBuilder(args);

// Services registrieren
builder.Services.AddSingleton<JtlDbContext>();

// TODO: Worker-Klassen implementieren
// builder.Services.AddHostedService<ZahlungsabgleichWorker>();
// builder.Services.AddHostedService<WooCommerceSyncWorker>();
// builder.Services.AddHostedService<MahnlaufWorker>();
// builder.Services.AddHostedService<WorkflowQueueWorker>();

Console.WriteLine("NovviaERP Worker gestartet");
Console.WriteLine("Verfügbare Modi:");
Console.WriteLine("  --mode msv3-stock --pzn <PZN>   MSV3 Bestandsabfrage für einzelne PZN");

var host = builder.Build();
host.Run();
return 0;

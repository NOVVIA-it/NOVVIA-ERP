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

if (string.Equals(mode, "output", StringComparison.OrdinalIgnoreCase))
{
    // PDF + EML Ausgabe erzeugen
    var outDir = GetArg(args, "--out");
    var to = GetArg(args, "--to");
    var subject = GetArg(args, "--subject");
    var body = GetArg(args, "--body");
    var pdfName = GetArg(args, "--pdfname");
    var html = GetArg(args, "--html");

    var job = new OutputJob(outDir, to, subject, body, pdfName, html);
    return job.Run();
}

// ============ LAGER-BUCHUNGEN (JTL-konform via SP) ============

static string GetConnectionString()
{
    var config = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true)
        .AddEnvironmentVariables()
        .Build();

    var cs = config.GetConnectionString("JtlWawi");
    if (string.IsNullOrWhiteSpace(cs))
    {
        cs = "Server=JTL-DB;Database=Mandant_2;Integrated Security=True;TrustServerCertificate=True;";
    }
    return cs;
}

if (string.Equals(mode, "stock-we", StringComparison.OrdinalIgnoreCase))
{
    // Wareneingang buchen (JTL SP)
    var artikelStr = GetArg(args, "--artikel");
    var platzStr = GetArg(args, "--platz");
    var mengeStr = GetArg(args, "--menge");

    if (string.IsNullOrWhiteSpace(artikelStr) || string.IsNullOrWhiteSpace(platzStr) || string.IsNullOrWhiteSpace(mengeStr))
    {
        Console.Error.WriteLine("Fehler: --artikel, --platz und --menge sind Pflichtparameter");
        Console.Error.WriteLine("Verwendung: NovviaERP.Worker.exe --mode stock-we --artikel 12345 --platz 1 --menge 5");
        Console.Error.WriteLine("Optional:   --kommentar \"...\" --charge \"...\" --mhd 2025-12-31 --lieferschein \"...\"");
        return 2;
    }

    if (!int.TryParse(artikelStr, out var artikelId) ||
        !int.TryParse(platzStr, out var platzId) ||
        !decimal.TryParse(mengeStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var menge))
    {
        Console.Error.WriteLine("Fehler: Ungueltige Parameter (artikel/platz muessen int sein, menge decimal)");
        return 2;
    }

    var kommentar = GetArg(args, "--kommentar");
    var charge = GetArg(args, "--charge");
    var mhdStr = GetArg(args, "--mhd");
    var lieferschein = GetArg(args, "--lieferschein");

    DateTime? mhd = null;
    if (!string.IsNullOrEmpty(mhdStr) && DateTime.TryParse(mhdStr, out var mhdParsed))
        mhd = mhdParsed;

    var job = new StockBookingJob(GetConnectionString());
    return await job.RunWareneingangAsync(artikelId, platzId, menge, 1, kommentar, charge, mhd, lieferschein);
}

if (string.Equals(mode, "stock-wa", StringComparison.OrdinalIgnoreCase))
{
    // Warenausgang buchen (JTL SP)
    var artikelStr = GetArg(args, "--artikel");
    var platzStr = GetArg(args, "--platz");
    var mengeStr = GetArg(args, "--menge");

    if (string.IsNullOrWhiteSpace(artikelStr) || string.IsNullOrWhiteSpace(platzStr) || string.IsNullOrWhiteSpace(mengeStr))
    {
        Console.Error.WriteLine("Fehler: --artikel, --platz und --menge sind Pflichtparameter");
        Console.Error.WriteLine("Verwendung: NovviaERP.Worker.exe --mode stock-wa --artikel 12345 --platz 1 --menge 2");
        Console.Error.WriteLine("Optional:   --kommentar \"...\" --buchungsart 1");
        Console.Error.WriteLine("Buchungsarten: 1=Verkauf, 2=Korrektur, 3=Inventur, 4=Umlagerung, 5=Verlust, 6=Retoure");
        return 2;
    }

    if (!int.TryParse(artikelStr, out var artikelId) ||
        !int.TryParse(platzStr, out var platzId) ||
        !decimal.TryParse(mengeStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var menge))
    {
        Console.Error.WriteLine("Fehler: Ungueltige Parameter");
        return 2;
    }

    var kommentar = GetArg(args, "--kommentar");
    var buchungsartStr = GetArg(args, "--buchungsart");
    int buchungsart = 1;
    if (!string.IsNullOrEmpty(buchungsartStr) && int.TryParse(buchungsartStr, out var ba))
        buchungsart = ba;

    var job = new StockBookingJob(GetConnectionString());
    return await job.RunWarenausgangAsync(artikelId, platzId, menge, 1, buchungsart, kommentar);
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
Console.WriteLine("Verfuegbare Modi:");
Console.WriteLine("  --mode msv3-stock --pzn <PZN>   MSV3 Bestandsabfrage fuer einzelne PZN");
Console.WriteLine("  --mode output [Optionen]        PDF + EML Ausgabe erzeugen");
Console.WriteLine("     --out <Verzeichnis>          Ausgabeverzeichnis (Standard: ./output)");
Console.WriteLine("     --to <Email>                 Empfaenger E-Mail");
Console.WriteLine("     --subject <Betreff>          E-Mail Betreff");
Console.WriteLine("     --body <Text>                E-Mail/PDF Inhalt");
Console.WriteLine("     --pdfname <Dateiname>        PDF Dateiname (Standard: output.pdf)");
Console.WriteLine("     --html <HTML>                HTML-Inhalt fuer PDF");

var host = builder.Build();
host.Run();
return 0;

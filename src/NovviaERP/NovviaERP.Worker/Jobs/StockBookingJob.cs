using NovviaERP.Core.Infrastructure.Jtl;

namespace NovviaERP.Worker.Jobs;

/// <summary>
/// CLI Job fuer JTL-konforme Lagerbuchungen (Wareneingang/Warenausgang)
///
/// Verwendung:
///   --mode stock-we --artikel 12345 --platz 1 --menge 5 [--kommentar "..."] [--charge "..."] [--mhd 2025-12-31]
///   --mode stock-wa --artikel 12345 --platz 1 --menge 2 [--kommentar "..."] [--buchungsart 1]
/// </summary>
public class StockBookingJob
{
    private readonly JtlStockBookingClient _client;

    public StockBookingJob(string connectionString)
    {
        _client = new JtlStockBookingClient(connectionString);
    }

    public async Task<int> RunWareneingangAsync(
        int artikelId,
        int lagerPlatzId,
        decimal menge,
        int benutzerId = 1,
        string? kommentar = null,
        string? chargenNr = null,
        DateTime? mhd = null,
        string? lieferscheinNr = null)
    {
        Console.WriteLine($"[INFO] Wareneingang wird gebucht...");
        Console.WriteLine($"       Artikel:    {artikelId}");
        Console.WriteLine($"       Lagerplatz: {lagerPlatzId}");
        Console.WriteLine($"       Menge:      {menge:N2}");

        if (!string.IsNullOrEmpty(chargenNr))
            Console.WriteLine($"       Charge:     {chargenNr}");
        if (mhd.HasValue)
            Console.WriteLine($"       MHD:        {mhd.Value:dd.MM.yyyy}");

        var result = await _client.BucheWareneingangAsync(
            new JtlStockBookingClient.StockBookingRequest(
                ArtikelId: artikelId,
                WarenlagerPlatzId: lagerPlatzId,
                Menge: menge,
                BenutzerId: benutzerId,
                Kommentar: kommentar,
                ChargenNr: chargenNr,
                MHD: mhd,
                LieferscheinNr: lieferscheinNr
            ));

        if (result.Success)
        {
            Console.WriteLine($"[OK] {result.Message}");
            return 0;
        }
        else
        {
            Console.Error.WriteLine($"[FEHLER] {result.Message}");
            return 1;
        }
    }

    public async Task<int> RunWarenausgangAsync(
        int artikelId,
        int lagerPlatzId,
        decimal menge,
        int benutzerId = 1,
        int buchungsart = 1,
        string? kommentar = null)
    {
        Console.WriteLine($"[INFO] Warenausgang wird gebucht...");
        Console.WriteLine($"       Artikel:     {artikelId}");
        Console.WriteLine($"       Lagerplatz:  {lagerPlatzId}");
        Console.WriteLine($"       Menge:       {menge:N2}");
        Console.WriteLine($"       Buchungsart: {buchungsart} ({GetBuchungsartText(buchungsart)})");

        var result = await _client.BucheWarenausgangAsync(
            artikelId: artikelId,
            warenlagerPlatzId: lagerPlatzId,
            menge: menge,
            benutzerId: benutzerId,
            buchungsart: buchungsart,
            kommentar: kommentar);

        if (result.Success)
        {
            Console.WriteLine($"[OK] {result.Message}");
            if (result.NewId.HasValue)
                Console.WriteLine($"     kWarenlagerAusgang: {result.NewId}");
            return 0;
        }
        else
        {
            Console.Error.WriteLine($"[FEHLER] {result.Message}");
            return 1;
        }
    }

    private static string GetBuchungsartText(int art) => art switch
    {
        1 => "Verkauf",
        2 => "Korrektur",
        3 => "Inventur",
        4 => "Umlagerung",
        5 => "Verlust",
        6 => "Retoure",
        7 => "Produktion",
        _ => "Sonstige"
    };
}

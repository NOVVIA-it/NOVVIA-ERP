using System.Data;
using System.Globalization;
using System.Text;
using Microsoft.Data.SqlClient;

namespace NovviaERP.Core.Infrastructure.Jtl;

/// <summary>
/// JTL-konforme Lagerbuchungen via Stored Procedures.
/// WICHTIG: Niemals direkt JTL-Tabellen aendern - immer ueber SPs buchen!
/// </summary>
public sealed class JtlStockBookingClient
{
    private readonly string _connectionString;

    public JtlStockBookingClient(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    #region DTOs

    public sealed record StockBookingRequest(
        int ArtikelId,
        int WarenlagerPlatzId,
        decimal Menge,
        int BenutzerId,
        string? Kommentar = null,
        string? LieferscheinNr = null,
        string? ChargenNr = null,
        DateTime? MHD = null,
        DateTime? GeliefertAm = null,
        int? LieferantenBestellungPosId = null,
        decimal? EKEinzel = null
    );

    public sealed record StockBookingResult(
        bool Success,
        string? Message = null,
        int? NewId = null
    );

    #endregion

    #region Wareneingang

    /// <summary>
    /// Wareneingang buchen via JTL SP dbo.spWarenlagerEingangSchreiben
    /// </summary>
    public async Task<StockBookingResult> BucheWareneingangAsync(
        StockBookingRequest req,
        CancellationToken ct = default)
    {
        try
        {
            await using var con = new SqlConnection(_connectionString);
            await con.OpenAsync(ct);

            await using var cmd = new SqlCommand("dbo.spWarenlagerEingangSchreiben", con)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 60
            };

            // XML Parameter (JTL erwartet IMMER XML)
            cmd.Parameters.Add(new SqlParameter("@xWarenlagerEingaenge", SqlDbType.Xml)
            {
                Value = BuildWareneingangXml(req)
            });

            // Pflichtparameter
            cmd.Parameters.Add(new SqlParameter("@kArtikel", SqlDbType.Int) { Value = req.ArtikelId });
            cmd.Parameters.Add(new SqlParameter("@kWarenlagerPlatz", SqlDbType.Int) { Value = req.WarenlagerPlatzId });
            cmd.Parameters.Add(new SqlParameter("@kBenutzer", SqlDbType.Int) { Value = req.BenutzerId });
            cmd.Parameters.Add(new SqlParameter("@fAnzahl", SqlDbType.Decimal)
            {
                Precision = 25, Scale = 13, Value = req.Menge
            });

            // Optionale Parameter
            cmd.Parameters.Add(new SqlParameter("@cKommentar", SqlDbType.NVarChar, 255)
            {
                Value = (object?)req.Kommentar ?? DBNull.Value
            });
            cmd.Parameters.Add(new SqlParameter("@cLieferscheinNr", SqlDbType.NVarChar, 50)
            {
                Value = (object?)req.LieferscheinNr ?? DBNull.Value
            });
            cmd.Parameters.Add(new SqlParameter("@cChargenNr", SqlDbType.NVarChar, 50)
            {
                Value = (object?)req.ChargenNr ?? DBNull.Value
            });
            cmd.Parameters.Add(new SqlParameter("@dMHD", SqlDbType.DateTime)
            {
                Value = (object?)req.MHD ?? DBNull.Value
            });
            cmd.Parameters.Add(new SqlParameter("@dGeliefertAm", SqlDbType.DateTime)
            {
                Value = (object?)req.GeliefertAm ?? DBNull.Value
            });
            cmd.Parameters.Add(new SqlParameter("@kLieferantenBestellungPos", SqlDbType.Int)
            {
                Value = (object?)req.LieferantenBestellungPosId ?? DBNull.Value
            });
            cmd.Parameters.Add(new SqlParameter("@fEKEinzel", SqlDbType.Decimal)
            {
                Precision = 25, Scale = 13, Value = (object?)req.EKEinzel ?? DBNull.Value
            });

            await cmd.ExecuteNonQueryAsync(ct);

            return new StockBookingResult(true,
                $"Wareneingang gebucht: {req.Menge:N2} x Artikel {req.ArtikelId} auf Platz {req.WarenlagerPlatzId}");
        }
        catch (SqlException ex)
        {
            return new StockBookingResult(false, $"SQL Fehler bei Wareneingang: {ex.Message}");
        }
        catch (Exception ex)
        {
            return new StockBookingResult(false, $"Fehler bei Wareneingang: {ex.Message}");
        }
    }

    private static string BuildWareneingangXml(StockBookingRequest req)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<WarenlagerEingaenge>");
        sb.AppendLine("  <Eingang>");
        sb.AppendLine($"    <kArtikel>{req.ArtikelId}</kArtikel>");
        sb.AppendLine($"    <kWarenlagerPlatz>{req.WarenlagerPlatzId}</kWarenlagerPlatz>");
        sb.AppendLine($"    <fAnzahl>{req.Menge.ToString(CultureInfo.InvariantCulture)}</fAnzahl>");

        if (!string.IsNullOrEmpty(req.ChargenNr))
            sb.AppendLine($"    <cChargenNr>{EscapeXml(req.ChargenNr)}</cChargenNr>");
        if (req.MHD.HasValue)
            sb.AppendLine($"    <dMHD>{req.MHD.Value:yyyy-MM-ddTHH:mm:ss}</dMHD>");
        if (req.EKEinzel.HasValue)
            sb.AppendLine($"    <fEKEinzel>{req.EKEinzel.Value.ToString(CultureInfo.InvariantCulture)}</fEKEinzel>");

        sb.AppendLine("  </Eingang>");
        sb.AppendLine("</WarenlagerEingaenge>");
        return sb.ToString();
    }

    #endregion

    #region Warenausgang

    /// <summary>
    /// Warenausgang buchen via JTL SP dbo.spWarenlagerAusgangSchreiben
    /// </summary>
    public async Task<StockBookingResult> BucheWarenausgangAsync(
        int artikelId,
        int warenlagerPlatzId,
        decimal menge,
        int benutzerId,
        int buchungsart = 1,
        string? kommentar = null,
        int? warenlagerEingangId = null,
        int? lieferscheinPosId = null,
        CancellationToken ct = default)
    {
        try
        {
            await using var con = new SqlConnection(_connectionString);
            await con.OpenAsync(ct);

            await using var cmd = new SqlCommand("dbo.spWarenlagerAusgangSchreiben", con)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 60
            };

            // XML Parameter
            cmd.Parameters.Add(new SqlParameter("@xWarenlagerAusgaenge", SqlDbType.Xml)
            {
                Value = BuildWarenausgangXml(artikelId, warenlagerPlatzId, menge)
            });

            // Pflichtparameter
            cmd.Parameters.Add(new SqlParameter("@fAnzahl", SqlDbType.Decimal)
            {
                Precision = 25, Scale = 13, Value = menge
            });
            cmd.Parameters.Add(new SqlParameter("@kBenutzer", SqlDbType.Int) { Value = benutzerId });
            cmd.Parameters.Add(new SqlParameter("@kBuchungsart", SqlDbType.Int) { Value = buchungsart });

            // Optionale Parameter
            cmd.Parameters.Add(new SqlParameter("@cKommentar", SqlDbType.NVarChar, 255)
            {
                Value = (object?)kommentar ?? DBNull.Value
            });
            cmd.Parameters.Add(new SqlParameter("@kWarenlagerEingang", SqlDbType.Int)
            {
                Value = (object?)warenlagerEingangId ?? DBNull.Value
            });
            cmd.Parameters.Add(new SqlParameter("@kLieferscheinPos", SqlDbType.Int)
            {
                Value = (object?)lieferscheinPosId ?? DBNull.Value
            });

            // Output Parameter
            var outId = new SqlParameter("@kWarenlagerAusgang", SqlDbType.Int)
            {
                Direction = ParameterDirection.Output
            };
            cmd.Parameters.Add(outId);

            await cmd.ExecuteNonQueryAsync(ct);

            int? newId = outId.Value == DBNull.Value ? null : Convert.ToInt32(outId.Value);

            return new StockBookingResult(true,
                $"Warenausgang gebucht: {menge:N2} x Artikel {artikelId}", newId);
        }
        catch (SqlException ex)
        {
            return new StockBookingResult(false, $"SQL Fehler bei Warenausgang: {ex.Message}");
        }
        catch (Exception ex)
        {
            return new StockBookingResult(false, $"Fehler bei Warenausgang: {ex.Message}");
        }
    }

    private static string BuildWarenausgangXml(int artikelId, int lagerPlatzId, decimal menge)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<WarenlagerAusgaenge>");
        sb.AppendLine("  <Ausgang>");
        sb.AppendLine($"    <kArtikel>{artikelId}</kArtikel>");
        sb.AppendLine($"    <kWarenlagerPlatz>{lagerPlatzId}</kWarenlagerPlatz>");
        sb.AppendLine($"    <fAnzahl>{menge.ToString(CultureInfo.InvariantCulture)}</fAnzahl>");
        sb.AppendLine("  </Ausgang>");
        sb.AppendLine("</WarenlagerAusgaenge>");
        return sb.ToString();
    }

    #endregion

    #region Helpers

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    #endregion

    #region Buchungsarten (Enum fuer kBuchungsart)

    /// <summary>
    /// JTL Buchungsarten fuer Warenausgang
    /// </summary>
    public enum Buchungsart
    {
        Verkauf = 1,
        Korrektur = 2,
        Inventur = 3,
        Umlagerung = 4,
        Verlust = 5,
        Retoure = 6,
        Produktion = 7,
        Sonstige = 99
    }

    #endregion
}

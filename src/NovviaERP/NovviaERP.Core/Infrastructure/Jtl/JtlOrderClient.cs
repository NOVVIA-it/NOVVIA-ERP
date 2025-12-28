using System.Data;
using Microsoft.Data.SqlClient;

namespace NovviaERP.Core.Infrastructure.Jtl;

/// <summary>
/// JTL-konforme Auftragsoperationen via Stored Procedures.
/// WICHTIG: Nach Auftragsaenderungen IMMER spAuftragEckdatenBerechnen aufrufen!
/// </summary>
public sealed class JtlOrderClient
{
    private readonly string _connectionString;

    public JtlOrderClient(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    #region DTOs

    public sealed record AuftragEckdaten(
        int KAuftrag,
        decimal FWertNetto,
        decimal FWertBrutto,
        decimal FZahlung,
        decimal FGutschrift,
        decimal FOffenerWert,
        int NZahlungStatus,
        int NRechnungStatus,
        int NLieferstatus,
        int NKomplettAusgeliefert,
        DateTime? DBezahlt,
        DateTime? DLetzterVersand,
        int NAnzahlPakete,
        int NAnzahlVersendetePakete,
        string? CRechnungsnummern
    );

    public sealed record OperationResult(
        bool Success,
        string? Message = null,
        int? AffectedRows = null
    );

    #endregion

    #region Eckdaten berechnen

    /// <summary>
    /// Berechnet die Eckdaten (Summen, Status, etc.) fuer einen oder mehrere Auftraege.
    /// MUSS nach jeder Auftragsaenderung aufgerufen werden!
    /// </summary>
    public async Task<OperationResult> BerechneAuftragEckdatenAsync(
        int kAuftrag,
        CancellationToken ct = default)
    {
        return await BerechneAuftragEckdatenAsync(new[] { kAuftrag }, ct);
    }

    /// <summary>
    /// Berechnet die Eckdaten fuer mehrere Auftraege.
    /// </summary>
    public async Task<OperationResult> BerechneAuftragEckdatenAsync(
        IEnumerable<int> auftragIds,
        CancellationToken ct = default)
    {
        try
        {
            await using var con = new SqlConnection(_connectionString);
            await con.OpenAsync(ct);

            // Table-Valued Parameter erstellen
            var table = new DataTable();
            table.Columns.Add("kAuftrag", typeof(int));

            foreach (var id in auftragIds)
            {
                if (id > 0)
                    table.Rows.Add(id);
            }

            if (table.Rows.Count == 0)
                return new OperationResult(true, "Keine Auftraege zum Berechnen");

            await using var cmd = new SqlCommand("[Verkauf].[spAuftragEckdatenBerechnen]", con)
            {
                CommandType = CommandType.StoredProcedure,
                CommandTimeout = 120 // SP kann bei vielen Auftraegen laenger dauern
            };

            var param = new SqlParameter("@Auftrag", SqlDbType.Structured)
            {
                TypeName = "Verkauf.TYPE_spAuftragEckdatenBerechnen",
                Value = table
            };
            cmd.Parameters.Add(param);

            await cmd.ExecuteNonQueryAsync(ct);

            return new OperationResult(true,
                $"Eckdaten fuer {table.Rows.Count} Auftrag/Auftraege berechnet",
                table.Rows.Count);
        }
        catch (SqlException ex)
        {
            return new OperationResult(false, $"SQL Fehler: {ex.Message}");
        }
        catch (Exception ex)
        {
            return new OperationResult(false, $"Fehler: {ex.Message}");
        }
    }

    #endregion

    #region Eckdaten lesen

    /// <summary>
    /// Liest die berechneten Eckdaten eines Auftrags.
    /// </summary>
    public async Task<AuftragEckdaten?> GetAuftragEckdatenAsync(
        int kAuftrag,
        CancellationToken ct = default)
    {
        try
        {
            await using var con = new SqlConnection(_connectionString);
            await con.OpenAsync(ct);

            await using var cmd = new SqlCommand(@"
                SELECT
                    e.kAuftrag,
                    ISNULL(e.fWertNetto, 0) AS fWertNetto,
                    ISNULL(e.fWertBrutto, 0) AS fWertBrutto,
                    ISNULL(e.fZahlung, 0) AS fZahlung,
                    ISNULL(e.fGutschrift, 0) AS fGutschrift,
                    ISNULL(e.fOffenerWert, 0) AS fOffenerWert,
                    ISNULL(e.nZahlungStatus, 0) AS nZahlungStatus,
                    ISNULL(e.nRechnungStatus, 0) AS nRechnungStatus,
                    ISNULL(e.nLieferstatus, 1) AS nLieferstatus,
                    ISNULL(a.nKomplettAusgeliefert, 0) AS nKomplettAusgeliefert,
                    e.dBezahlt,
                    e.dLetzterVersand,
                    ISNULL(e.nAnzahlPakete, 0) AS nAnzahlPakete,
                    ISNULL(e.nAnzahlVersendetePakete, 0) AS nAnzahlVersendetePakete,
                    e.cRechnungsnummern
                FROM Verkauf.tAuftragEckdaten e
                JOIN Verkauf.tAuftrag a ON a.kAuftrag = e.kAuftrag
                WHERE e.kAuftrag = @kAuftrag", con);

            cmd.Parameters.AddWithValue("@kAuftrag", kAuftrag);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                return new AuftragEckdaten(
                    KAuftrag: reader.GetInt32(0),
                    FWertNetto: reader.GetDecimal(1),
                    FWertBrutto: reader.GetDecimal(2),
                    FZahlung: reader.GetDecimal(3),
                    FGutschrift: reader.GetDecimal(4),
                    FOffenerWert: reader.GetDecimal(5),
                    NZahlungStatus: reader.GetInt32(6),
                    NRechnungStatus: reader.GetInt32(7),
                    NLieferstatus: reader.GetInt32(8),
                    NKomplettAusgeliefert: reader.GetInt32(9),
                    DBezahlt: reader.IsDBNull(10) ? null : reader.GetDateTime(10),
                    DLetzterVersand: reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                    NAnzahlPakete: reader.GetInt32(12),
                    NAnzahlVersendetePakete: reader.GetInt32(13),
                    CRechnungsnummern: reader.IsDBNull(14) ? null : reader.GetString(14)
                );
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Hilfsmethoden fuer Status

    /// <summary>
    /// Zahlungsstatus als Text
    /// </summary>
    public static string GetZahlungStatusText(int status) => status switch
    {
        0 => "Unbezahlt",
        1 => "Teilbezahlt",
        2 => "Bezahlt",
        3 => "Nicht ermittelbar",
        _ => "Unbekannt"
    };

    /// <summary>
    /// Rechnungsstatus als Text
    /// </summary>
    public static string GetRechnungStatusText(int status) => status switch
    {
        0 => "Keine Rechnung",
        1 => "Teilweise",
        2 => "Vollstaendig",
        _ => "Unbekannt"
    };

    /// <summary>
    /// Lieferstatus als Text
    /// </summary>
    public static string GetLieferStatusText(int status) => status switch
    {
        0 => "Storniert",
        1 => "Ausstehend",
        2 => "Teilgeliefert",
        3 => "Geliefert",
        4 => "Teilversendet",
        5 => "Versendet",
        6 => "Gutgeschrieben",
        7 => "Ohne Versand abgeschlossen",
        _ => "Unbekannt"
    };

    /// <summary>
    /// Farbe fuer Lieferstatus (fuer UI)
    /// </summary>
    public static string GetLieferStatusColor(int status) => status switch
    {
        0 => "#dc3545", // Rot - Storniert
        1 => "#ffc107", // Gelb - Ausstehend
        2 => "#17a2b8", // Cyan - Teilgeliefert
        3 => "#28a745", // Gruen - Geliefert
        4 => "#007bff", // Blau - Teilversendet
        5 => "#28a745", // Gruen - Versendet
        6 => "#6c757d", // Grau - Gutgeschrieben
        7 => "#6c757d", // Grau - Ohne Versand
        _ => "#6c757d"
    };

    #endregion
}

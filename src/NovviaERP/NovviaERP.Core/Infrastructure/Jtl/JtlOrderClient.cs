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
        int? AffectedRows = null,
        int? NewId = null
    );

    /// <summary>
    /// DTO fuer neue Auftragsposition
    /// </summary>
    public sealed record AuftragPositionInput(
        int KArtikel,
        decimal FAnzahl,
        decimal? FVKNetto = null,
        decimal? FRabatt = null,
        string? CFreifeld1 = null,
        string? CFreifeld2 = null,
        string? CHinweis = null
    );

    /// <summary>
    /// DTO fuer Auftragsposition-Update
    /// </summary>
    public sealed record AuftragPositionUpdate(
        int KAuftragPosition,
        decimal? FAnzahl = null,
        decimal? FVKNetto = null,
        decimal? FRabatt = null,
        string? CFreifeld1 = null,
        string? CFreifeld2 = null,
        string? CHinweis = null
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

    #region Positionen schreiben

    /// <summary>
    /// Fuegt eine neue Position zum Auftrag hinzu.
    /// Ruft automatisch spAuftragEckdatenBerechnen auf!
    /// </summary>
    public async Task<OperationResult> AddAuftragPositionAsync(
        int kAuftrag,
        AuftragPositionInput position,
        CancellationToken ct = default)
    {
        try
        {
            await using var con = new SqlConnection(_connectionString);
            await con.OpenAsync(ct);

            // Artikeldaten laden
            await using var artCmd = new SqlCommand(@"
                SELECT a.cArtNr, ab.cName, a.fVKNetto, a.fMwSt
                FROM dbo.tArtikel a
                LEFT JOIN dbo.tArtikelBeschreibung ab ON a.kArtikel = ab.kArtikel AND ab.kSprache = 1
                WHERE a.kArtikel = @kArtikel", con);
            artCmd.Parameters.AddWithValue("@kArtikel", position.KArtikel);

            string? cArtNr = null, cName = null;
            decimal fVKNetto = 0, fMwSt = 19;

            await using (var reader = await artCmd.ExecuteReaderAsync(ct))
            {
                if (await reader.ReadAsync(ct))
                {
                    cArtNr = reader.IsDBNull(0) ? null : reader.GetString(0);
                    cName = reader.IsDBNull(1) ? null : reader.GetString(1);
                    fVKNetto = reader.IsDBNull(2) ? 0 : reader.GetDecimal(2);
                    fMwSt = reader.IsDBNull(3) ? 19 : reader.GetDecimal(3);
                }
                else
                {
                    return new OperationResult(false, $"Artikel {position.KArtikel} nicht gefunden");
                }
            }

            // Naechste Sortierung ermitteln
            await using var sortCmd = new SqlCommand(@"
                SELECT ISNULL(MAX(nSort), 0) + 1 FROM Verkauf.tAuftragPosition WHERE kAuftrag = @kAuftrag", con);
            sortCmd.Parameters.AddWithValue("@kAuftrag", kAuftrag);
            var nSort = (int)(await sortCmd.ExecuteScalarAsync(ct) ?? 1);

            // Position einfuegen
            var vkNetto = position.FVKNetto ?? fVKNetto;
            var rabatt = position.FRabatt ?? 0;

            await using var insertCmd = new SqlCommand(@"
                INSERT INTO Verkauf.tAuftragPosition (
                    kAuftrag, kArtikel, cArtNr, cName, fAnzahl, fVKNetto, fRabatt, fMwSt,
                    nSort, nPosTyp, cFreifeld1, cFreifeld2, cHinweis, dErstellt
                )
                OUTPUT INSERTED.kAuftragPosition
                VALUES (
                    @kAuftrag, @kArtikel, @cArtNr, @cName, @fAnzahl, @fVKNetto, @fRabatt, @fMwSt,
                    @nSort, 0, @cFreifeld1, @cFreifeld2, @cHinweis, GETDATE()
                )", con);

            insertCmd.Parameters.AddWithValue("@kAuftrag", kAuftrag);
            insertCmd.Parameters.AddWithValue("@kArtikel", position.KArtikel);
            insertCmd.Parameters.AddWithValue("@cArtNr", (object?)cArtNr ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@cName", (object?)cName ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@fAnzahl", position.FAnzahl);
            insertCmd.Parameters.AddWithValue("@fVKNetto", vkNetto);
            insertCmd.Parameters.AddWithValue("@fRabatt", rabatt);
            insertCmd.Parameters.AddWithValue("@fMwSt", fMwSt);
            insertCmd.Parameters.AddWithValue("@nSort", nSort);
            insertCmd.Parameters.AddWithValue("@cFreifeld1", (object?)position.CFreifeld1 ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@cFreifeld2", (object?)position.CFreifeld2 ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("@cHinweis", (object?)position.CHinweis ?? DBNull.Value);

            var newId = (int)(await insertCmd.ExecuteScalarAsync(ct) ?? 0);

            // Eckdaten neu berechnen
            await BerechneAuftragEckdatenAsync(kAuftrag, ct);

            return new OperationResult(true, "Position hinzugefuegt", 1, newId);
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

    /// <summary>
    /// Aktualisiert eine bestehende Auftragsposition.
    /// Ruft automatisch spAuftragEckdatenBerechnen auf!
    /// </summary>
    public async Task<OperationResult> UpdateAuftragPositionAsync(
        AuftragPositionUpdate update,
        CancellationToken ct = default)
    {
        try
        {
            await using var con = new SqlConnection(_connectionString);
            await con.OpenAsync(ct);

            // kAuftrag ermitteln fuer Eckdaten-Berechnung
            await using var auftragCmd = new SqlCommand(
                "SELECT kAuftrag FROM Verkauf.tAuftragPosition WHERE kAuftragPosition = @kPos", con);
            auftragCmd.Parameters.AddWithValue("@kPos", update.KAuftragPosition);
            var kAuftrag = (int?)await auftragCmd.ExecuteScalarAsync(ct);

            if (kAuftrag == null)
                return new OperationResult(false, "Position nicht gefunden");

            // Dynamisches Update bauen
            var setClauses = new List<string>();
            var cmd = new SqlCommand { Connection = con };

            if (update.FAnzahl.HasValue)
            {
                setClauses.Add("fAnzahl = @fAnzahl");
                cmd.Parameters.AddWithValue("@fAnzahl", update.FAnzahl.Value);
            }
            if (update.FVKNetto.HasValue)
            {
                setClauses.Add("fVKNetto = @fVKNetto");
                cmd.Parameters.AddWithValue("@fVKNetto", update.FVKNetto.Value);
            }
            if (update.FRabatt.HasValue)
            {
                setClauses.Add("fRabatt = @fRabatt");
                cmd.Parameters.AddWithValue("@fRabatt", update.FRabatt.Value);
            }
            if (update.CFreifeld1 != null)
            {
                setClauses.Add("cFreifeld1 = @cFreifeld1");
                cmd.Parameters.AddWithValue("@cFreifeld1", update.CFreifeld1);
            }
            if (update.CFreifeld2 != null)
            {
                setClauses.Add("cFreifeld2 = @cFreifeld2");
                cmd.Parameters.AddWithValue("@cFreifeld2", update.CFreifeld2);
            }
            if (update.CHinweis != null)
            {
                setClauses.Add("cHinweis = @cHinweis");
                cmd.Parameters.AddWithValue("@cHinweis", update.CHinweis);
            }

            if (setClauses.Count == 0)
                return new OperationResult(true, "Keine Aenderungen");

            cmd.CommandText = $@"
                UPDATE Verkauf.tAuftragPosition
                SET {string.Join(", ", setClauses)}, dGeaendert = GETDATE()
                WHERE kAuftragPosition = @kPos";
            cmd.Parameters.AddWithValue("@kPos", update.KAuftragPosition);

            var rows = await cmd.ExecuteNonQueryAsync(ct);

            // Eckdaten neu berechnen
            await BerechneAuftragEckdatenAsync(kAuftrag.Value, ct);

            return new OperationResult(true, "Position aktualisiert", rows);
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

    /// <summary>
    /// Loescht eine Auftragsposition.
    /// Ruft automatisch spAuftragEckdatenBerechnen auf!
    /// </summary>
    public async Task<OperationResult> DeleteAuftragPositionAsync(
        int kAuftragPosition,
        CancellationToken ct = default)
    {
        try
        {
            await using var con = new SqlConnection(_connectionString);
            await con.OpenAsync(ct);

            // kAuftrag ermitteln fuer Eckdaten-Berechnung
            await using var auftragCmd = new SqlCommand(
                "SELECT kAuftrag FROM Verkauf.tAuftragPosition WHERE kAuftragPosition = @kPos", con);
            auftragCmd.Parameters.AddWithValue("@kPos", kAuftragPosition);
            var kAuftrag = (int?)await auftragCmd.ExecuteScalarAsync(ct);

            if (kAuftrag == null)
                return new OperationResult(false, "Position nicht gefunden");

            // Position loeschen
            await using var deleteCmd = new SqlCommand(
                "DELETE FROM Verkauf.tAuftragPosition WHERE kAuftragPosition = @kPos", con);
            deleteCmd.Parameters.AddWithValue("@kPos", kAuftragPosition);
            var rows = await deleteCmd.ExecuteNonQueryAsync(ct);

            // Eckdaten neu berechnen
            await BerechneAuftragEckdatenAsync(kAuftrag.Value, ct);

            return new OperationResult(true, "Position geloescht", rows);
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

    #region Auftrag Status

    /// <summary>
    /// Aktualisiert den Auftragsstatus.
    /// Ruft automatisch spAuftragEckdatenBerechnen auf!
    /// </summary>
    public async Task<OperationResult> UpdateAuftragStatusAsync(
        int kAuftrag,
        int nStatus,
        CancellationToken ct = default)
    {
        try
        {
            await using var con = new SqlConnection(_connectionString);
            await con.OpenAsync(ct);

            await using var cmd = new SqlCommand(@"
                UPDATE Verkauf.tAuftrag
                SET nStatus = @nStatus, dGeaendert = GETDATE()
                WHERE kAuftrag = @kAuftrag", con);
            cmd.Parameters.AddWithValue("@kAuftrag", kAuftrag);
            cmd.Parameters.AddWithValue("@nStatus", nStatus);

            var rows = await cmd.ExecuteNonQueryAsync(ct);

            if (rows == 0)
                return new OperationResult(false, "Auftrag nicht gefunden");

            // Eckdaten neu berechnen
            await BerechneAuftragEckdatenAsync(kAuftrag, ct);

            return new OperationResult(true, $"Status auf {nStatus} gesetzt", rows);
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

    /// <summary>
    /// Aktualisiert die Lieferadresse des Auftrags.
    /// Ruft automatisch spAuftragEckdatenBerechnen auf!
    /// </summary>
    public async Task<OperationResult> UpdateAuftragLieferadresseAsync(
        int kAuftrag,
        int kLieferadresse,
        CancellationToken ct = default)
    {
        try
        {
            await using var con = new SqlConnection(_connectionString);
            await con.OpenAsync(ct);

            await using var cmd = new SqlCommand(@"
                UPDATE Verkauf.tAuftrag
                SET kLieferadresse = @kLieferadresse, dGeaendert = GETDATE()
                WHERE kAuftrag = @kAuftrag", con);
            cmd.Parameters.AddWithValue("@kAuftrag", kAuftrag);
            cmd.Parameters.AddWithValue("@kLieferadresse", kLieferadresse);

            var rows = await cmd.ExecuteNonQueryAsync(ct);

            if (rows == 0)
                return new OperationResult(false, "Auftrag nicht gefunden");

            // Eckdaten neu berechnen
            await BerechneAuftragEckdatenAsync(kAuftrag, ct);

            return new OperationResult(true, "Lieferadresse aktualisiert", rows);
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

    /// <summary>
    /// Setzt die interne Anmerkung des Auftrags.
    /// </summary>
    public async Task<OperationResult> UpdateAuftragAnmerkungAsync(
        int kAuftrag,
        string? cInternerKommentar,
        CancellationToken ct = default)
    {
        try
        {
            await using var con = new SqlConnection(_connectionString);
            await con.OpenAsync(ct);

            await using var cmd = new SqlCommand(@"
                UPDATE Verkauf.tAuftrag
                SET cInternerKommentar = @cInternerKommentar, dGeaendert = GETDATE()
                WHERE kAuftrag = @kAuftrag", con);
            cmd.Parameters.AddWithValue("@kAuftrag", kAuftrag);
            cmd.Parameters.AddWithValue("@cInternerKommentar", (object?)cInternerKommentar ?? DBNull.Value);

            var rows = await cmd.ExecuteNonQueryAsync(ct);

            if (rows == 0)
                return new OperationResult(false, "Auftrag nicht gefunden");

            return new OperationResult(true, "Anmerkung aktualisiert", rows);
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Dapper;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Extensions.DependencyInjection;
using NovviaERP.Core.Data;
using SkiaSharp;

namespace NovviaERP.WPF.Views
{
    public partial class DashboardPage : UserControl
    {
        private readonly JtlDbContext _db;

        public DashboardPage()
        {
            InitializeComponent();
            _db = App.Services.GetRequiredService<JtlDbContext>();

            txtDatum.Text = DateTime.Now.ToString("dddd, dd. MMMM yyyy");
            Loaded += async (s, e) => await LadeDashboardAsync();
        }

        private async void BtnAktualisieren_Click(object sender, RoutedEventArgs e)
        {
            await LadeDashboardAsync();
        }

        private async Task LadeDashboardAsync()
        {
            try
            {
                pnlLaden.Visibility = Visibility.Visible;

                await Task.WhenAll(
                    LadeKPIsAsync(),
                    LadeUmsatzVerlaufAsync(),
                    LadeStatusChartAsync(),
                    LadeTopKundenAsync(),
                    LadeTopArtikelAsync(),
                    LadeZahlungenAsync()
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Fehler beim Laden: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                pnlLaden.Visibility = Visibility.Collapsed;
            }
        }

        #region KPIs

        private async Task LadeKPIsAsync()
        {
            try
            {
                var conn = await _db.GetConnectionAsync();

                // JTL-Schema: Verkauf.tAuftrag, Rechnung.tRechnung, Lieferschein.tLieferschein
                var kpis = await conn.QueryFirstOrDefaultAsync<DashboardKpis>(@"
                    DECLARE @Heute DATE = CAST(GETDATE() AS DATE);
                    DECLARE @MonatStart DATE = DATEFROMPARTS(YEAR(@Heute), MONTH(@Heute), 1);
                    DECLARE @VormonatStart DATE = DATEADD(MONTH, -1, @MonatStart);
                    DECLARE @VormonatEnde DATE = DATEADD(DAY, -1, @MonatStart);

                    SELECT
                        -- Umsatz aus Rechnung.tRechnungEckdaten
                        ISNULL((SELECT SUM(re.fVKBruttoGesamt) FROM Rechnung.tRechnung r JOIN Rechnung.tRechnungEckdaten re ON r.kRechnung = re.kRechnung WHERE r.dErstellt >= @MonatStart AND r.nStorno = 0), 0) AS UmsatzMonat,
                        ISNULL((SELECT SUM(re.fVKBruttoGesamt) FROM Rechnung.tRechnung r JOIN Rechnung.tRechnungEckdaten re ON r.kRechnung = re.kRechnung WHERE r.dErstellt >= @VormonatStart AND r.dErstellt <= @VormonatEnde AND r.nStorno = 0), 0) AS UmsatzVormonat,
                        -- Auftraege aus Verkauf.tAuftrag
                        (SELECT COUNT(*) FROM Verkauf.tAuftrag WHERE CAST(dErstellt AS DATE) = @Heute AND nStorno = 0) AS AuftraegeHeute,
                        (SELECT COUNT(*) FROM Verkauf.tAuftrag WHERE nAuftragStatus IN (1,2) AND nStorno = 0) AS AuftraegeOffen,
                        -- Rechnungen
                        (SELECT COUNT(*) FROM Rechnung.tRechnung r JOIN Rechnung.tRechnungEckdaten re ON r.kRechnung = re.kRechnung WHERE re.nZahlungStatus = 1 AND r.nStorno = 0) AS RechnungenOffen,
                        ISNULL((SELECT SUM(re.fOffenerWert) FROM Rechnung.tRechnung r JOIN Rechnung.tRechnungEckdaten re ON r.kRechnung = re.kRechnung WHERE re.nZahlungStatus = 1 AND r.nStorno = 0), 0) AS RechnungenOffenWert,
                        (SELECT COUNT(*) FROM Rechnung.tRechnung r JOIN Rechnung.tRechnungEckdaten re ON r.kRechnung = re.kRechnung WHERE re.nZahlungStatus = 1 AND re.dZahlungsziel < @Heute AND r.nStorno = 0) AS RechnungenUeberfaellig,
                        ISNULL((SELECT SUM(re.fOffenerWert) FROM Rechnung.tRechnung r JOIN Rechnung.tRechnungEckdaten re ON r.kRechnung = re.kRechnung WHERE re.nZahlungStatus = 1 AND re.dZahlungsziel < @Heute AND r.nStorno = 0), 0) AS RechnungenUeberfaelligWert,
                        -- Versand aus Lieferschein
                        (SELECT COUNT(*) FROM Lieferschein.tLieferschein WHERE CAST(dErstellt AS DATE) = @Heute) AS VersandHeute,
                        (SELECT COUNT(*) FROM Verkauf.tAuftrag WHERE nAuftragStatus = 2 AND nStorno = 0) AS VersandOffen,
                        -- Neue Kunden
                        (SELECT COUNT(*) FROM dbo.tKunde WHERE CAST(dErstellt AS DATE) >= @MonatStart) AS NeueKundenMonat
                ");

                if (kpis != null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        txtUmsatzMonat.Text = $"{kpis.UmsatzMonat:N0} EUR";

                        // Trend berechnen
                        if (kpis.UmsatzVormonat > 0)
                        {
                            var trend = ((kpis.UmsatzMonat - kpis.UmsatzVormonat) / kpis.UmsatzVormonat) * 100;
                            txtUmsatzTrend.Text = $"{(trend >= 0 ? "+" : "")}{trend:N0}%";
                            txtUmsatzTrend.Foreground = new SolidColorBrush(trend >= 0 ? Colors.Green : Colors.Red);
                        }

                        txtAuftraegeHeute.Text = kpis.AuftraegeHeute.ToString();
                        txtAuftraegeOffen.Text = $"{kpis.AuftraegeOffen} offen";

                        txtRechnungenOffen.Text = kpis.RechnungenOffen.ToString();
                        txtRechnungenOffenWert.Text = $"{kpis.RechnungenOffenWert:N0} EUR";

                        txtUeberfaellig.Text = kpis.RechnungenUeberfaellig.ToString();
                        txtUeberfaelligWert.Text = $"{kpis.RechnungenUeberfaelligWert:N0} EUR";

                        txtVersandHeute.Text = kpis.VersandHeute.ToString();
                        txtVersandOffen.Text = $"{kpis.VersandOffen} offen";

                        txtNeueKunden.Text = kpis.NeueKundenMonat.ToString();
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"KPI Fehler: {ex.Message}");
            }
        }

        #endregion

        #region Umsatzverlauf Chart

        private async Task LadeUmsatzVerlaufAsync()
        {
            try
            {
                var conn = await _db.GetConnectionAsync();

                var daten = await conn.QueryAsync<UmsatzMonat>(@"
                    ;WITH Monate AS (
                        SELECT
                            DATEFROMPARTS(YEAR(DATEADD(MONTH, -n, GETDATE())), MONTH(DATEADD(MONTH, -n, GETDATE())), 1) AS MonatStart,
                            EOMONTH(DATEADD(MONTH, -n, GETDATE())) AS MonatEnde,
                            FORMAT(DATEADD(MONTH, -n, GETDATE()), 'MMM', 'de-DE') AS MonatLabel,
                            DATEADD(MONTH, -n, GETDATE()) AS SortDatum
                        FROM (SELECT TOP 12 ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS n FROM sys.objects) Numbers
                    )
                    SELECT
                        m.MonatLabel AS Monat,
                        ISNULL(SUM(r.fBrutto), 0) AS Umsatz
                    FROM Monate m
                    LEFT JOIN tRechnung r ON r.dErstellt >= m.MonatStart AND r.dErstellt <= m.MonatEnde AND r.nStorno = 0
                    GROUP BY m.MonatLabel, m.SortDatum
                    ORDER BY m.SortDatum
                ");

                var liste = daten.ToList();

                Dispatcher.Invoke(() =>
                {
                    chartUmsatz.Series = new ISeries[]
                    {
                        new LineSeries<double>
                        {
                            Values = liste.Select(x => (double)x.Umsatz).ToArray(),
                            Fill = new SolidColorPaint(SKColor.Parse("#2196F3").WithAlpha(50)),
                            Stroke = new SolidColorPaint(SKColor.Parse("#2196F3")) { StrokeThickness = 3 },
                            GeometrySize = 8,
                            GeometryStroke = new SolidColorPaint(SKColor.Parse("#2196F3")) { StrokeThickness = 2 },
                            GeometryFill = new SolidColorPaint(SKColors.White)
                        }
                    };

                    chartUmsatz.XAxes = new Axis[]
                    {
                        new Axis
                        {
                            Labels = liste.Select(x => x.Monat).ToArray(),
                            LabelsRotation = 0,
                            TextSize = 11
                        }
                    };

                    chartUmsatz.YAxes = new Axis[]
                    {
                        new Axis
                        {
                            Labeler = value => $"{value/1000:N0}k",
                            TextSize = 11
                        }
                    };
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Umsatz Chart Fehler: {ex.Message}");
            }
        }

        #endregion

        #region Status Pie Chart

        private async Task LadeStatusChartAsync()
        {
            try
            {
                var conn = await _db.GetConnectionAsync();

                var daten = await conn.QueryAsync<StatusDaten>(@"
                    SELECT
                        CASE nStatus
                            WHEN 1 THEN 'Offen'
                            WHEN 2 THEN 'In Bearbeitung'
                            WHEN 3 THEN 'Versendet'
                            WHEN 4 THEN 'Bezahlt'
                            ELSE 'Sonstige'
                        END AS Status,
                        COUNT(*) AS Anzahl
                    FROM tBestellung
                    WHERE dErstellt >= DATEADD(DAY, -30, GETDATE()) AND nStorno = 0
                    GROUP BY nStatus
                ");

                var liste = daten.ToList();
                var farben = new[] { "#FF9800", "#2196F3", "#00BCD4", "#4CAF50", "#9E9E9E" };

                Dispatcher.Invoke(() =>
                {
                    var series = new List<ISeries>();
                    for (int i = 0; i < liste.Count; i++)
                    {
                        series.Add(new PieSeries<double>
                        {
                            Values = new double[] { liste[i].Anzahl },
                            Name = liste[i].Status,
                            Fill = new SolidColorPaint(SKColor.Parse(farben[i % farben.Length])),
                            DataLabelsSize = 12,
                            DataLabelsPaint = new SolidColorPaint(SKColors.White),
                            DataLabelsFormatter = point => $"{liste[i].Anzahl}"
                        });
                    }
                    chartStatus.Series = series;
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Status Chart Fehler: {ex.Message}");
            }
        }

        #endregion

        #region Top Kunden Chart

        private async Task LadeTopKundenAsync()
        {
            try
            {
                var conn = await _db.GetConnectionAsync();

                var daten = await conn.QueryAsync<TopKunde>(@"
                    SELECT TOP 5
                        COALESCE(k.cFirma, k.cVorname + ' ' + k.cName, k.cName) AS KundeName,
                        SUM(r.fBrutto) AS Umsatz
                    FROM dbo.tKunde k
                    JOIN tRechnung r ON k.kKunde = r.kKunde
                    WHERE r.dErstellt >= DATEADD(DAY, -365, GETDATE()) AND r.nStorno = 0
                    GROUP BY k.kKunde, k.cFirma, k.cVorname, k.cName
                    ORDER BY Umsatz DESC
                ");

                var liste = daten.ToList();

                Dispatcher.Invoke(() =>
                {
                    chartTopKunden.Series = new ISeries[]
                    {
                        new RowSeries<double>
                        {
                            Values = liste.Select(x => (double)x.Umsatz).ToArray(),
                            Fill = new SolidColorPaint(SKColor.Parse("#4CAF50")),
                            DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                            DataLabelsSize = 11,
                            DataLabelsFormatter = point => $"{point.Model/1000:N0}k"
                        }
                    };

                    chartTopKunden.YAxes = new Axis[]
                    {
                        new Axis
                        {
                            Labels = liste.Select(x => x.KundeName?.Length > 15 ? x.KundeName.Substring(0, 15) + "..." : x.KundeName ?? "").ToArray(),
                            TextSize = 10
                        }
                    };

                    chartTopKunden.XAxes = new Axis[]
                    {
                        new Axis
                        {
                            Labeler = value => $"{value/1000:N0}k",
                            TextSize = 10
                        }
                    };
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Top Kunden Chart Fehler: {ex.Message}");
            }
        }

        #endregion

        #region Top Artikel Chart

        private async Task LadeTopArtikelAsync()
        {
            try
            {
                var conn = await _db.GetConnectionAsync();

                var daten = await conn.QueryAsync<TopArtikel>(@"
                    SELECT TOP 5
                        COALESCE(ab.cName, a.cArtNr) AS ArtikelName,
                        SUM(ap.nAnzahl) AS Menge
                    FROM dbo.tArtikel a
                    LEFT JOIN dbo.tArtikelBeschreibung ab ON a.kArtikel = ab.kArtikel AND ab.kSprache = 1
                    JOIN tBestellPos ap ON a.kArtikel = ap.kArtikel
                    JOIN tBestellung au ON ap.tBestellung_kBestellung = au.kBestellung
                    WHERE au.dErstellt >= DATEADD(DAY, -30, GETDATE()) AND au.nStorno = 0
                    GROUP BY a.kArtikel, a.cArtNr, ab.cName
                    ORDER BY Menge DESC
                ");

                var liste = daten.ToList();

                Dispatcher.Invoke(() =>
                {
                    chartTopArtikel.Series = new ISeries[]
                    {
                        new RowSeries<double>
                        {
                            Values = liste.Select(x => (double)x.Menge).ToArray(),
                            Fill = new SolidColorPaint(SKColor.Parse("#9C27B0")),
                            DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                            DataLabelsSize = 11,
                            DataLabelsFormatter = point => $"{point.Model:N0}"
                        }
                    };

                    chartTopArtikel.YAxes = new Axis[]
                    {
                        new Axis
                        {
                            Labels = liste.Select(x => x.ArtikelName?.Length > 15 ? x.ArtikelName.Substring(0, 15) + "..." : x.ArtikelName ?? "").ToArray(),
                            TextSize = 10
                        }
                    };

                    chartTopArtikel.XAxes = new Axis[]
                    {
                        new Axis { TextSize = 10 }
                    };
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Top Artikel Chart Fehler: {ex.Message}");
            }
        }

        #endregion

        #region Zahlungen Chart

        private async Task LadeZahlungenAsync()
        {
            try
            {
                var conn = await _db.GetConnectionAsync();

                var daten = await conn.QueryAsync<ZahlungWoche>(@"
                    ;WITH Wochen AS (
                        SELECT
                            DATEADD(WEEK, -n, CAST(GETDATE() AS DATE)) AS WocheStart,
                            DATEADD(DAY, 6, DATEADD(WEEK, -n, CAST(GETDATE() AS DATE))) AS WocheEnde,
                            'KW' + CAST(DATEPART(WEEK, DATEADD(WEEK, -n, GETDATE())) AS NVARCHAR(2)) AS WocheLabel,
                            n AS SortNr
                        FROM (SELECT TOP 8 ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS n FROM sys.objects) Numbers
                    )
                    SELECT
                        w.WocheLabel AS Woche,
                        ISNULL(SUM(z.fBetrag), 0) AS Betrag
                    FROM Wochen w
                    LEFT JOIN dbo.tZahlung z ON z.dDatum >= w.WocheStart AND z.dDatum <= w.WocheEnde AND z.fBetrag > 0
                    GROUP BY w.WocheLabel, w.SortNr
                    ORDER BY w.SortNr DESC
                ");

                var liste = daten.ToList();

                Dispatcher.Invoke(() =>
                {
                    chartZahlungen.Series = new ISeries[]
                    {
                        new ColumnSeries<double>
                        {
                            Values = liste.Select(x => (double)x.Betrag).ToArray(),
                            Fill = new SolidColorPaint(SKColor.Parse("#00BCD4")),
                            DataLabelsPaint = new SolidColorPaint(SKColors.Black),
                            DataLabelsSize = 10,
                            DataLabelsFormatter = point => point.Model > 0 ? $"{point.Model/1000:N0}k" : ""
                        }
                    };

                    chartZahlungen.XAxes = new Axis[]
                    {
                        new Axis
                        {
                            Labels = liste.Select(x => x.Woche).ToArray(),
                            TextSize = 10
                        }
                    };

                    chartZahlungen.YAxes = new Axis[]
                    {
                        new Axis
                        {
                            Labeler = value => $"{value/1000:N0}k",
                            TextSize = 10
                        }
                    };
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Zahlungen Chart Fehler: {ex.Message}");
            }
        }

        #endregion
    }

    #region DTOs

    internal class DashboardKpis
    {
        public decimal UmsatzMonat { get; set; }
        public decimal UmsatzVormonat { get; set; }
        public int AuftraegeHeute { get; set; }
        public int AuftraegeOffen { get; set; }
        public int RechnungenOffen { get; set; }
        public decimal RechnungenOffenWert { get; set; }
        public int RechnungenUeberfaellig { get; set; }
        public decimal RechnungenUeberfaelligWert { get; set; }
        public int VersandHeute { get; set; }
        public int VersandOffen { get; set; }
        public int NeueKundenMonat { get; set; }
    }

    internal class UmsatzMonat
    {
        public string Monat { get; set; } = "";
        public decimal Umsatz { get; set; }
    }

    internal class StatusDaten
    {
        public string Status { get; set; } = "";
        public int Anzahl { get; set; }
    }

    internal class TopKunde
    {
        public string? KundeName { get; set; }
        public decimal Umsatz { get; set; }
    }

    internal class TopArtikel
    {
        public string? ArtikelName { get; set; }
        public decimal Menge { get; set; }
    }

    internal class ZahlungWoche
    {
        public string Woche { get; set; } = "";
        public decimal Betrag { get; set; }
    }

    #endregion
}

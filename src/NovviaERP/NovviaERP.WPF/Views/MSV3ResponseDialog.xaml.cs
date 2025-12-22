using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace NovviaERP.WPF.Views;

public partial class MSV3ResponseDialog : Window
{
    public MSV3ResponseDialog()
    {
        InitializeComponent();
    }

    public void SetErgebnis(string titel, string subtitel, List<MSV3ResponsePosition> positionen, string? responseXml = null)
    {
        txtTitel.Text = titel;
        txtSubtitel.Text = subtitel;
        dgPositionen.ItemsSource = positionen;
        txtResponseXml.Text = responseXml ?? "(keine Response verfuegbar)";

        int verfuegbar = positionen.Count(p => p.VerfuegbareMenge >= p.Menge);
        int teilweise = positionen.Count(p => p.VerfuegbareMenge > 0 && p.VerfuegbareMenge < p.Menge);
        int nichtVerfuegbar = positionen.Count(p => p.VerfuegbareMenge == 0);

        txtStatus.Text = $"Verfuegbar: {verfuegbar} | Teilweise: {teilweise} | Nicht verfuegbar: {nichtVerfuegbar}";
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"MSV3 Ergebnis: {txtTitel.Text}");
        sb.AppendLine($"{txtSubtitel.Text}");
        sb.AppendLine();
        sb.AppendLine("PZN\tArtikel\tMenge\tVerfuegbar\tStatus\tMHD\tCharge\tLieferant");

        if (dgPositionen.ItemsSource is IEnumerable<MSV3ResponsePosition> positionen)
        {
            foreach (var pos in positionen)
            {
                sb.AppendLine($"{pos.PZN}\t{pos.ArtikelName}\t{pos.Menge}\t{pos.VerfuegbareMenge}\t{pos.StatusCode}\t{pos.MHDText}\t{pos.ChargenNr}\t{pos.LieferantName}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("--- Response XML ---");
        sb.AppendLine(txtResponseXml.Text);

        Clipboard.SetText(sb.ToString());
        MessageBox.Show("In Zwischenablage kopiert!", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

public class MSV3ResponsePosition
{
    public string? PZN { get; set; }
    public string? ArtikelName { get; set; }
    public int Menge { get; set; }
    public int VerfuegbareMenge { get; set; }
    public string? StatusCode { get; set; }
    public string? MHDText { get; set; }
    public string? ChargenNr { get; set; }
    public string? LieferantName { get; set; }

    public string VerfuegbarFarbe => VerfuegbareMenge >= Menge ? "Green" :
        (VerfuegbareMenge > 0 ? "Orange" : "Red");
}

using System.Net.Mail;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NovviaERP.Worker.Jobs;

/// <summary>
/// Job zum Erzeugen von PDF und EML (E-Mail) Dateien
/// Verwendung:
///   --mode output --out "C:\temp" --to "kunde@example.com" --subject "Bestandsinfo" --body "Text..." --pdfname "Bestand.pdf"
/// </summary>
public class OutputJob
{
    private readonly string _outDir;
    private readonly string _to;
    private readonly string _subject;
    private readonly string _body;
    private readonly string _pdfName;
    private readonly string? _htmlContent;

    public OutputJob(string? outDir, string? to, string? subject, string? body, string? pdfName, string? htmlContent = null)
    {
        _outDir = outDir ?? Path.Combine(Environment.CurrentDirectory, "output");
        _to = to ?? "test@example.com";
        _subject = subject ?? "NOVVIA Ausgabe";
        _body = body ?? "Hallo,\r\nanbei die Ausgabe.\r\nMit freundlichen Gruessen\r\nNOVVIA GmbH";
        _pdfName = pdfName ?? "output.pdf";
        _htmlContent = htmlContent;
    }

    public int Run()
    {
        try
        {
            // QuestPDF Lizenz (Community)
            QuestPDF.Settings.License = LicenseType.Community;

            Directory.CreateDirectory(_outDir);

            // 1) PDF erzeugen
            string pdfPath = Path.Combine(_outDir, _pdfName);
            CreatePdf(pdfPath);
            Console.WriteLine($"[OK] PDF erzeugt: {pdfPath}");

            // 2) EML erzeugen (Mail-Datei die in Outlook geoeffnet werden kann)
            string emlPath = Path.Combine(_outDir, Path.GetFileNameWithoutExtension(_pdfName) + ".eml");
            CreateEml(emlPath, pdfPath);
            Console.WriteLine($"[OK] EML erzeugt: {emlPath}");

            Console.WriteLine();
            Console.WriteLine("Ausgabe erfolgreich:");
            Console.WriteLine($"  PDF: {pdfPath}");
            Console.WriteLine($"  EML: {emlPath}");

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[FEHLER] {ex.Message}");
            return 1;
        }
    }

    private void CreatePdf(string pdfPath)
    {
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                // Header
                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("NOVVIA GmbH")
                            .FontSize(18).Bold().FontColor(Colors.Blue.Darken2);
                        row.ConstantItem(100).AlignRight().Text(DateTime.Now.ToString("dd.MM.yyyy"))
                            .FontSize(10).FontColor(Colors.Grey.Darken1);
                    });
                    col.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                // Content
                page.Content().PaddingVertical(20).Column(col =>
                {
                    col.Spacing(10);

                    // Betreff
                    col.Item().Text(_subject).FontSize(14).Bold();
                    col.Item().PaddingTop(10);

                    // Body
                    if (!string.IsNullOrWhiteSpace(_htmlContent))
                    {
                        // HTML-Content (vereinfacht als Text)
                        col.Item().Text(StripHtml(_htmlContent));
                    }
                    else
                    {
                        foreach (var line in _body.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
                        {
                            col.Item().Text(line);
                        }
                    }
                });

                // Footer
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("NOVVIA GmbH - ").FontSize(9).FontColor(Colors.Grey.Medium);
                    text.Span("Seite ").FontSize(9).FontColor(Colors.Grey.Medium);
                    text.CurrentPageNumber().FontSize(9).FontColor(Colors.Grey.Medium);
                    text.Span(" von ").FontSize(9).FontColor(Colors.Grey.Medium);
                    text.TotalPages().FontSize(9).FontColor(Colors.Grey.Medium);
                });
            });
        }).GeneratePdf(pdfPath);
    }

    private void CreateEml(string emlPath, string attachmentPath)
    {
        using var message = new MailMessage("no-reply@novvia.de", _to, _subject, _body);
        message.IsBodyHtml = false;
        message.BodyEncoding = Encoding.UTF8;
        message.SubjectEncoding = Encoding.UTF8;

        if (!string.IsNullOrWhiteSpace(attachmentPath) && File.Exists(attachmentPath))
        {
            message.Attachments.Add(new Attachment(attachmentPath));
        }

        // System.Net.Mail kann nicht direkt als .eml speichern,
        // daher nutzen wir PickupDirectory
        using var client = new SmtpClient("localhost");
        client.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
        client.PickupDirectoryLocation = _outDir;

        client.Send(message);

        // PickupDirectory erzeugt eine GUID.eml Datei. Wir benennen sie um.
        var newestFile = GetNewestFile(_outDir);
        if (newestFile != null)
        {
            if (File.Exists(emlPath)) File.Delete(emlPath);
            File.Move(newestFile, emlPath);
        }
    }

    private static string? GetNewestFile(string dir)
    {
        // Pickup-Dateien haben .eml Endung und GUID-Namen
        string? newest = null;
        DateTime newestTime = DateTime.MinValue;

        foreach (var f in Directory.GetFiles(dir, "*.eml"))
        {
            var info = new FileInfo(f);
            // Nur GUID-Dateien (36 Zeichen Laenge im Namen)
            var nameWithoutExt = Path.GetFileNameWithoutExtension(f);
            if (nameWithoutExt.Length == 36 && Guid.TryParse(nameWithoutExt, out _))
            {
                if (info.LastWriteTime > newestTime)
                {
                    newestTime = info.LastWriteTime;
                    newest = f;
                }
            }
        }
        return newest;
    }

    private static string StripHtml(string html)
    {
        // Einfacher HTML-Stripper
        var result = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]+>", "");
        result = System.Net.WebUtility.HtmlDecode(result);
        return result.Trim();
    }
}

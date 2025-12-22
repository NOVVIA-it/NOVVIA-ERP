using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Dapper;
using NovviaERP.Core.Data;
using Serilog;

namespace NovviaERP.Core.Services
{
    /// <summary>
    /// Service für Drucker-Verwaltung und direkten Etikettendruck
    /// </summary>
    public class DruckerService
    {
        private readonly JtlDbContext _db;
        private static readonly ILogger _log = Log.ForContext<DruckerService>();

        public DruckerService(JtlDbContext db) => _db = db;

        #region Drucker-Verwaltung
        /// <summary>
        /// Holt alle im System installierten Drucker
        /// </summary>
        public List<DruckerInfo> GetInstallierteDrucker()
        {
            var drucker = new List<DruckerInfo>();
            foreach (string name in PrinterSettings.InstalledPrinters)
            {
                try
                {
                    var ps = new PrinterSettings { PrinterName = name };
                    drucker.Add(new DruckerInfo
                    {
                        Name = name,
                        IstStandard = ps.IsDefaultPrinter,
                        IstVerfuegbar = ps.IsValid,
                        Papierfaecher = ps.PaperSources.Cast<PaperSource>().Select(p => p.SourceName).ToList(),
                        Papierformate = ps.PaperSizes.Cast<PaperSize>().Select(p => $"{p.PaperName} ({p.Width/100.0}x{p.Height/100.0} mm)").ToList()
                    });
                }
                catch { }
            }
            return drucker;
        }

        /// <summary>
        /// Holt die Drucker-Konfiguration aus der Datenbank
        /// </summary>
        public async Task<IEnumerable<DruckerKonfiguration>> GetDruckerKonfigurationAsync()
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QueryAsync<DruckerKonfiguration>("SELECT * FROM tDrucker WHERE nAktiv = 1 ORDER BY cTyp, cName");
        }

        /// <summary>
        /// Speichert eine Drucker-Konfiguration
        /// </summary>
        public async Task<int> SaveDruckerKonfigurationAsync(DruckerKonfiguration config)
        {
            var conn = await _db.GetConnectionAsync();
            if (config.Id == 0)
            {
                return await conn.QuerySingleAsync<int>(@"
                    INSERT INTO tDrucker (cName, cDruckerName, cTyp, nBreiteMM, nHoeheMM, nDPI, nKopien, nAktiv, cPapierfach, cKonfiguration)
                    VALUES (@Name, @DruckerName, @Typ, @BreiteMM, @HoeheMM, @DPI, @Kopien, @Aktiv, @Papierfach, @KonfigurationJson);
                    SELECT SCOPE_IDENTITY();", config);
            }
            await conn.ExecuteAsync(@"
                UPDATE tDrucker SET cName=@Name, cDruckerName=@DruckerName, cTyp=@Typ, nBreiteMM=@BreiteMM, 
                    nHoeheMM=@HoeheMM, nDPI=@DPI, nKopien=@Kopien, nAktiv=@Aktiv, cPapierfach=@Papierfach, cKonfiguration=@KonfigurationJson
                WHERE kDrucker=@Id", config);
            return config.Id;
        }

        /// <summary>
        /// Holt den Standard-Drucker für einen Typ
        /// </summary>
        public async Task<DruckerKonfiguration?> GetStandardDruckerAsync(DruckerTyp typ)
        {
            var conn = await _db.GetConnectionAsync();
            return await conn.QuerySingleOrDefaultAsync<DruckerKonfiguration>(
                "SELECT TOP 1 * FROM tDrucker WHERE cTyp = @Typ AND nAktiv = 1 ORDER BY nStandard DESC", new { Typ = typ.ToString() });
        }
        #endregion

        #region Versandetiketten drucken
        /// <summary>
        /// Druckt ein Versandetikett (PDF) auf dem konfigurierten Drucker
        /// </summary>
        public async Task<bool> DruckeVersandetikettAsync(byte[] labelPdf, string? druckerName = null, int kopien = 1)
        {
            try
            {
                var drucker = druckerName;
                if (string.IsNullOrEmpty(drucker))
                {
                    var config = await GetStandardDruckerAsync(DruckerTyp.Versandetikett);
                    drucker = config?.DruckerName;
                }

                if (string.IsNullOrEmpty(drucker))
                {
                    _log.Warning("Kein Versandetiketten-Drucker konfiguriert");
                    return false;
                }

                // PDF in Temp speichern und mit Standardprogramm drucken
                var tempFile = Path.Combine(Path.GetTempPath(), $"label_{Guid.NewGuid()}.pdf");
                await File.WriteAllBytesAsync(tempFile, labelPdf);

                for (int i = 0; i < kopien; i++)
                {
                    // Windows: Drucken über ShellExecute
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = tempFile,
                            Verb = "print",
                            UseShellExecute = true,
                            CreateNoWindow = true,
                            Arguments = $"\"{drucker}\""
                        };
                        System.Diagnostics.Process.Start(psi);
                    }
                }

                _log.Information("Versandetikett gedruckt auf {Drucker} ({Kopien}x)", drucker, kopien);
                
                // Temp-Datei nach kurzer Verzögerung löschen
                _ = Task.Delay(5000).ContinueWith(_ => { try { File.Delete(tempFile); } catch { } });
                
                return true;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Fehler beim Drucken des Versandetiketts");
                return false;
            }
        }

        /// <summary>
        /// Druckt ein Versandetikett mit ZPL direkt auf Zebra-Drucker
        /// </summary>
        public async Task<bool> DruckeZPLAsync(string zplCode, string druckerName)
        {
            try
            {
                // Raw-Print an Windows-Drucker
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return RawPrinterHelper.SendStringToPrinter(druckerName, zplCode);
                }
                return false;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Fehler beim ZPL-Druck");
                return false;
            }
        }

        /// <summary>
        /// Generiert ZPL-Code für ein Versandetikett (100x150mm)
        /// </summary>
        public string GenerateVersandetikettZPL(VersandetikettDaten daten)
        {
            var zpl = new System.Text.StringBuilder();
            zpl.AppendLine("^XA"); // Start
            zpl.AppendLine("^PW800"); // Breite 100mm bei 203dpi
            zpl.AppendLine("^LL1200"); // Höhe 150mm
            
            // Absender (klein oben)
            zpl.AppendLine("^FO30,30^A0N,20,20^FD" + EscapeZPL(daten.AbsenderName) + "^FS");
            zpl.AppendLine("^FO30,50^A0N,18,18^FD" + EscapeZPL($"{daten.AbsenderStrasse}, {daten.AbsenderPLZ} {daten.AbsenderOrt}") + "^FS");
            
            // Trennlinie
            zpl.AppendLine("^FO20,80^GB760,2,2^FS");
            
            // Empfänger (groß)
            zpl.AppendLine("^FO40,120^A0N,40,40^FD" + EscapeZPL(daten.EmpfaengerName) + "^FS");
            if (!string.IsNullOrEmpty(daten.EmpfaengerFirma))
                zpl.AppendLine("^FO40,170^A0N,35,35^FD" + EscapeZPL(daten.EmpfaengerFirma) + "^FS");
            zpl.AppendLine("^FO40,220^A0N,35,35^FD" + EscapeZPL(daten.EmpfaengerStrasse) + "^FS");
            zpl.AppendLine("^FO40,280^A0N,50,50^FD" + EscapeZPL($"{daten.EmpfaengerPLZ} {daten.EmpfaengerOrt}") + "^FS");
            zpl.AppendLine("^FO40,350^A0N,35,35^FD" + EscapeZPL(daten.EmpfaengerLand) + "^FS");
            
            // Barcode (Tracking-Nummer)
            if (!string.IsNullOrEmpty(daten.TrackingNummer))
            {
                zpl.AppendLine("^FO100,450^BY3");
                zpl.AppendLine("^BCN,100,Y,N,N");
                zpl.AppendLine("^FD" + daten.TrackingNummer + "^FS");
            }
            
            // Referenz
            if (!string.IsNullOrEmpty(daten.Referenz))
                zpl.AppendLine("^FO40,600^A0N,25,25^FDRef: " + EscapeZPL(daten.Referenz) + "^FS");
            
            // Gewicht
            if (daten.GewichtKG > 0)
                zpl.AppendLine($"^FO600,600^A0N,25,25^FD{daten.GewichtKG:N2} kg^FS");
            
            // Carrier-Logo Position
            zpl.AppendLine("^FO600,30^A0N,30,30^FD" + EscapeZPL(daten.Carrier) + "^FS");
            
            zpl.AppendLine("^XZ"); // Ende
            return zpl.ToString();
        }

        private string EscapeZPL(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("^", "").Replace("~", "").Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue").Replace("ß", "ss")
                       .Replace("Ä", "Ae").Replace("Ö", "Oe").Replace("Ü", "Ue");
        }
        #endregion

        #region Artikeletiketten drucken
        /// <summary>
        /// Druckt ein Artikeletikett
        /// </summary>
        public async Task<bool> DruckeArtikeletikettAsync(ArtikeletikettDaten daten, string? druckerName = null, int kopien = 1)
        {
            var drucker = druckerName;
            if (string.IsNullOrEmpty(drucker))
            {
                var config = await GetStandardDruckerAsync(DruckerTyp.Artikeletikett);
                drucker = config?.DruckerName;
            }

            var zpl = GenerateArtikeletikettZPL(daten);
            for (int i = 0; i < kopien; i++)
            {
                await DruckeZPLAsync(zpl, drucker ?? "");
            }
            return true;
        }

        /// <summary>
        /// Generiert ZPL für Artikeletikett (verschiedene Größen)
        /// </summary>
        public string GenerateArtikeletikettZPL(ArtikeletikettDaten daten, string format = "62x29")
        {
            var zpl = new System.Text.StringBuilder();
            zpl.AppendLine("^XA");
            
            switch (format)
            {
                case "62x29": // Standard Dymo-Format
                    zpl.AppendLine("^PW500^LL230");
                    zpl.AppendLine("^FO10,20^A0N,35,35^FD" + EscapeZPL(daten.Name.Length > 25 ? daten.Name[..25] : daten.Name) + "^FS");
                    zpl.AppendLine("^FO10,60^A0N,25,25^FDArt: " + EscapeZPL(daten.ArtNr) + "^FS");
                    zpl.AppendLine($"^FO350,60^A0N,30,30^FD{daten.PreisBrutto:N2} EUR^FS");
                    // Barcode
                    zpl.AppendLine("^FO50,100^BY2^BCN,60,N,N,N^FD" + daten.Barcode + "^FS");
                    zpl.AppendLine("^FO100,170^A0N,20,20^FD" + daten.Barcode + "^FS");
                    break;
                    
                case "62x100": // Größeres Format
                    zpl.AppendLine("^PW500^LL800");
                    zpl.AppendLine("^FO10,20^A0N,40,40^FD" + EscapeZPL(daten.Name) + "^FS");
                    zpl.AppendLine("^FO10,70^A0N,30,30^FDArt.Nr: " + EscapeZPL(daten.ArtNr) + "^FS");
                    zpl.AppendLine($"^FO10,110^A0N,50,50^FD{daten.PreisBrutto:N2} EUR^FS");
                    if (!string.IsNullOrEmpty(daten.Beschreibung))
                        zpl.AppendLine("^FO10,180^A0N,22,22^FB480,3,,^FD" + EscapeZPL(daten.Beschreibung) + "^FS");
                    // Barcode
                    zpl.AppendLine("^FO80,280^BY3^BCN,100,Y,N,N^FD" + daten.Barcode + "^FS");
                    break;
            }
            
            zpl.AppendLine("^XZ");
            return zpl.ToString();
        }
        #endregion

        #region Dokumente drucken
        /// <summary>
        /// Druckt ein Dokument (Rechnung, Lieferschein etc.)
        /// </summary>
        public async Task<bool> DruckeDokumentAsync(byte[] pdf, DruckerTyp typ, int kopien = 1)
        {
            var config = await GetStandardDruckerAsync(typ);
            if (config == null)
            {
                _log.Warning("Kein Drucker für {Typ} konfiguriert", typ);
                return false;
            }

            var tempFile = Path.Combine(Path.GetTempPath(), $"doc_{Guid.NewGuid()}.pdf");
            await File.WriteAllBytesAsync(tempFile, pdf);

            for (int i = 0; i < (config.Kopien > 0 ? config.Kopien : kopien); i++)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "AcroRd32.exe", // oder SumatraPDF
                        Arguments = $"/t \"{tempFile}\" \"{config.DruckerName}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    try { System.Diagnostics.Process.Start(psi); }
                    catch
                    {
                        // Fallback: Shell print
                        psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = tempFile,
                            Verb = "print",
                            UseShellExecute = true
                        };
                        System.Diagnostics.Process.Start(psi);
                    }
                }
            }

            _ = Task.Delay(10000).ContinueWith(_ => { try { File.Delete(tempFile); } catch { } });
            return true;
        }
        #endregion
    }

    #region DTOs und Enums
    public class DruckerInfo
    {
        public string Name { get; set; } = "";
        public bool IstStandard { get; set; }
        public bool IstVerfuegbar { get; set; }
        public List<string> Papierfaecher { get; set; } = new();
        public List<string> Papierformate { get; set; } = new();
    }

    public class DruckerKonfiguration
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string DruckerName { get; set; } = "";
        public string Typ { get; set; } = "";
        public int BreiteMM { get; set; }
        public int HoeheMM { get; set; }
        public int DPI { get; set; } = 203;
        public int Kopien { get; set; } = 1;
        public bool Aktiv { get; set; } = true;
        public bool IstStandard { get; set; }
        public string? Papierfach { get; set; }
        public string? KonfigurationJson { get; set; }
    }

    public enum DruckerTyp
    {
        Versandetikett,
        Artikeletikett,
        Rechnung,
        Lieferschein,
        Pickliste,
        Mahnung,
        Sonstige
    }

    public class VersandetikettDaten
    {
        public string AbsenderName { get; set; } = "";
        public string AbsenderStrasse { get; set; } = "";
        public string AbsenderPLZ { get; set; } = "";
        public string AbsenderOrt { get; set; } = "";
        public string EmpfaengerName { get; set; } = "";
        public string? EmpfaengerFirma { get; set; }
        public string EmpfaengerStrasse { get; set; } = "";
        public string EmpfaengerPLZ { get; set; } = "";
        public string EmpfaengerOrt { get; set; } = "";
        public string EmpfaengerLand { get; set; } = "Deutschland";
        public string? TrackingNummer { get; set; }
        public string? Referenz { get; set; }
        public decimal GewichtKG { get; set; }
        public string Carrier { get; set; } = "";
    }

    public class ArtikeletikettDaten
    {
        public string ArtNr { get; set; } = "";
        public string Name { get; set; } = "";
        public string? Beschreibung { get; set; }
        public string Barcode { get; set; } = "";
        public decimal PreisBrutto { get; set; }
    }
    #endregion

    #region Raw Printer Helper (Windows)
    public static class RawPrinterHelper
    {
        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

        [DllImport("winspool.drv", SetLastError = true)]
        public static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        public static extern bool StartDocPrinter(IntPtr hPrinter, int level, ref DOCINFO pDocInfo);

        [DllImport("winspool.drv", SetLastError = true)]
        public static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        public static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        public static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.drv", SetLastError = true)]
        public static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DOCINFO
        {
            public string pDocName;
            public string? pOutputFile;
            public string pDataType;
        }

        public static bool SendStringToPrinter(string printerName, string data)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;
            
            IntPtr pBytes = Marshal.StringToCoTaskMemAnsi(data);
            int dwCount = data.Length;
            bool success = SendBytesToPrinter(printerName, pBytes, dwCount);
            Marshal.FreeCoTaskMem(pBytes);
            return success;
        }

        public static bool SendBytesToPrinter(string printerName, IntPtr pBytes, int dwCount)
        {
            IntPtr hPrinter = IntPtr.Zero;
            DOCINFO di = new() { pDocName = "RAW Document", pDataType = "RAW" };
            bool success = false;

            if (OpenPrinter(printerName, out hPrinter, IntPtr.Zero))
            {
                if (StartDocPrinter(hPrinter, 1, ref di))
                {
                    if (StartPagePrinter(hPrinter))
                    {
                        success = WritePrinter(hPrinter, pBytes, dwCount, out _);
                        EndPagePrinter(hPrinter);
                    }
                    EndDocPrinter(hPrinter);
                }
                ClosePrinter(hPrinter);
            }
            return success;
        }
    }
    #endregion
}

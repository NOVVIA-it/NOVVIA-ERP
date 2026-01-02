using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NovviaERP.Core.Services
{
    /// <summary>
    /// Zentraler Service für AppData-Speicherung (Einstellungen, Profile, Benutzereinstellungen)
    /// Speicherort: %APPDATA%\NovviaERP\
    /// </summary>
    public class AppDataService
    {
        private static readonly string AppDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NovviaERP");

        private static readonly byte[] EntropyKey = Encoding.UTF8.GetBytes("NovviaERP2024!");

        private readonly Dictionary<string, object> _cache = new();
        private readonly object _lock = new();

        public AppDataService()
        {
            EnsureDirectoryExists();
        }

        #region Pfade

        /// <summary>
        /// Basis-Pfad für AppData
        /// </summary>
        public static string BasePath => AppDataPath;

        /// <summary>
        /// Pfad zur Profil-Datei
        /// </summary>
        public static string ProfilePath => Path.Combine(AppDataPath, "profile.json");

        /// <summary>
        /// Pfad zur Login-History
        /// </summary>
        public static string LoginPath => Path.Combine(AppDataPath, "login.json");

        /// <summary>
        /// Pfad zu Benutzereinstellungen
        /// </summary>
        public static string SettingsPath => Path.Combine(AppDataPath, "settings.json");

        /// <summary>
        /// Pfad zum Cache-Ordner
        /// </summary>
        public static string CachePath => Path.Combine(AppDataPath, "cache");

        /// <summary>
        /// Pfad zum Log-Ordner
        /// </summary>
        public static string LogPath => Path.Combine(AppDataPath, "logs");

        #endregion

        #region Verzeichnis-Management

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(AppDataPath))
                Directory.CreateDirectory(AppDataPath);

            if (!Directory.Exists(CachePath))
                Directory.CreateDirectory(CachePath);

            if (!Directory.Exists(LogPath))
                Directory.CreateDirectory(LogPath);
        }

        #endregion

        #region Generische Speicher-Methoden

        /// <summary>
        /// Objekt als JSON speichern
        /// </summary>
        public void Save<T>(string key, T data)
        {
            lock (_lock)
            {
                var filePath = GetFilePath(key);
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(filePath, json);
                _cache[key] = data!;
            }
        }

        /// <summary>
        /// Objekt aus JSON laden
        /// </summary>
        public T? Load<T>(string key, T? defaultValue = default)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var cached))
                    return (T)cached;

                var filePath = GetFilePath(key);
                if (!File.Exists(filePath))
                    return defaultValue;

                try
                {
                    var json = File.ReadAllText(filePath);
                    var data = JsonSerializer.Deserialize<T>(json);
                    if (data != null)
                        _cache[key] = data;
                    return data;
                }
                catch
                {
                    return defaultValue;
                }
            }
        }

        /// <summary>
        /// Prüfen ob Schlüssel existiert
        /// </summary>
        public bool Exists(string key)
        {
            return File.Exists(GetFilePath(key));
        }

        /// <summary>
        /// Schlüssel löschen
        /// </summary>
        public void Delete(string key)
        {
            lock (_lock)
            {
                var filePath = GetFilePath(key);
                if (File.Exists(filePath))
                    File.Delete(filePath);
                _cache.Remove(key);
            }
        }

        private string GetFilePath(string key)
        {
            var safeName = string.Join("_", key.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(AppDataPath, $"{safeName}.json");
        }

        #endregion

        #region Verschlüsselung (DPAPI)

        /// <summary>
        /// Sensible Daten verschlüsselt speichern (verwendet Windows DPAPI)
        /// </summary>
        public void SaveEncrypted(string key, string value)
        {
            lock (_lock)
            {
                var encrypted = ProtectData(value);
                var filePath = GetFilePath($"{key}.enc");
                File.WriteAllText(filePath, encrypted);
            }
        }

        /// <summary>
        /// Verschlüsselte Daten laden
        /// </summary>
        public string? LoadEncrypted(string key)
        {
            lock (_lock)
            {
                var filePath = GetFilePath($"{key}.enc");
                if (!File.Exists(filePath))
                    return null;

                try
                {
                    var encrypted = File.ReadAllText(filePath);
                    return UnprotectData(encrypted);
                }
                catch
                {
                    return null;
                }
            }
        }

        private string ProtectData(string data)
        {
            var dataBytes = Encoding.UTF8.GetBytes(data);
            var protectedBytes = ProtectedData.Protect(dataBytes, EntropyKey, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }

        private string UnprotectData(string protectedData)
        {
            var protectedBytes = Convert.FromBase64String(protectedData);
            var dataBytes = ProtectedData.Unprotect(protectedBytes, EntropyKey, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(dataBytes);
        }

        #endregion

        #region Benutzereinstellungen

        /// <summary>
        /// Alle Benutzereinstellungen
        /// </summary>
        public UserSettings GetUserSettings()
        {
            return Load<UserSettings>("settings", new UserSettings()) ?? new UserSettings();
        }

        /// <summary>
        /// Benutzereinstellungen speichern
        /// </summary>
        public void SaveUserSettings(UserSettings settings)
        {
            Save("settings", settings);
        }

        /// <summary>
        /// Einzelne Einstellung lesen
        /// </summary>
        public T GetSetting<T>(string key, T defaultValue)
        {
            var settings = GetUserSettings();
            if (settings.Values.TryGetValue(key, out var value))
            {
                try
                {
                    if (value is JsonElement element)
                        return JsonSerializer.Deserialize<T>(element.GetRawText()) ?? defaultValue;
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        /// <summary>
        /// Einzelne Einstellung speichern
        /// </summary>
        public void SetSetting<T>(string key, T value)
        {
            var settings = GetUserSettings();
            settings.Values[key] = value!;
            SaveUserSettings(settings);
        }

        #endregion

        #region Server-Profile

        /// <summary>
        /// Alle Server-Profile laden
        /// </summary>
        public List<ServerProfil> GetServerProfile()
        {
            return Load<List<ServerProfil>>("profile", new List<ServerProfil>()) ?? new List<ServerProfil>();
        }

        /// <summary>
        /// Server-Profile speichern
        /// </summary>
        public void SaveServerProfile(List<ServerProfil> profile)
        {
            Save("profile", profile);
        }

        /// <summary>
        /// Letztes aktives Profil abrufen
        /// </summary>
        public ServerProfil? GetAktivesProfil()
        {
            var profile = GetServerProfile();
            var aktivesProfilName = GetSetting<string>("AktivesProfil", "");

            if (!string.IsNullOrEmpty(aktivesProfilName))
                return profile.Find(p => p.Name == aktivesProfilName);

            return profile.Count > 0 ? profile[0] : null;
        }

        /// <summary>
        /// Aktives Profil setzen
        /// </summary>
        public void SetAktivesProfil(string profilName)
        {
            SetSetting("AktivesProfil", profilName);
        }

        #endregion

        #region Login-History

        /// <summary>
        /// Letzte Anmeldung speichern
        /// </summary>
        public void SaveLetzteAnmeldung(LetzteAnmeldung anmeldung)
        {
            Save("login", anmeldung);
        }

        /// <summary>
        /// Letzte Anmeldung laden
        /// </summary>
        public LetzteAnmeldung? GetLetzteAnmeldung()
        {
            return Load<LetzteAnmeldung>("login");
        }

        #endregion

        #region Fenster-Positionen

        /// <summary>
        /// Fensterposition speichern
        /// </summary>
        public void SaveWindowPosition(string windowName, WindowPosition position)
        {
            var positions = Load<Dictionary<string, WindowPosition>>("window_positions",
                new Dictionary<string, WindowPosition>()) ?? new Dictionary<string, WindowPosition>();
            positions[windowName] = position;
            Save("window_positions", positions);
        }

        /// <summary>
        /// Fensterposition laden
        /// </summary>
        public WindowPosition? GetWindowPosition(string windowName)
        {
            var positions = Load<Dictionary<string, WindowPosition>>("window_positions");
            if (positions != null && positions.TryGetValue(windowName, out var pos))
                return pos;
            return null;
        }

        #endregion

        #region Zuletzt verwendet

        /// <summary>
        /// Zuletzt verwendete Werte speichern (z.B. letzte Suchbegriffe)
        /// </summary>
        public void AddRecentItem(string category, string item, int maxItems = 10)
        {
            var recent = Load<Dictionary<string, List<string>>>("recent_items",
                new Dictionary<string, List<string>>()) ?? new Dictionary<string, List<string>>();

            if (!recent.ContainsKey(category))
                recent[category] = new List<string>();

            recent[category].Remove(item);
            recent[category].Insert(0, item);

            if (recent[category].Count > maxItems)
                recent[category].RemoveRange(maxItems, recent[category].Count - maxItems);

            Save("recent_items", recent);
        }

        /// <summary>
        /// Zuletzt verwendete Werte abrufen
        /// </summary>
        public List<string> GetRecentItems(string category)
        {
            var recent = Load<Dictionary<string, List<string>>>("recent_items");
            if (recent != null && recent.TryGetValue(category, out var items))
                return items;
            return new List<string>();
        }

        #endregion

        #region Cache-Verwaltung

        /// <summary>
        /// Daten im Cache speichern (mit TTL)
        /// </summary>
        public void CacheSet<T>(string key, T data, TimeSpan? ttl = null)
        {
            var cacheFile = Path.Combine(CachePath, $"{key}.cache");
            var cacheData = new CacheEntry<T>
            {
                Data = data,
                CreatedAt = DateTime.Now,
                ExpiresAt = ttl.HasValue ? DateTime.Now.Add(ttl.Value) : DateTime.MaxValue
            };
            var json = JsonSerializer.Serialize(cacheData);
            File.WriteAllText(cacheFile, json);
        }

        /// <summary>
        /// Daten aus Cache laden
        /// </summary>
        public T? CacheGet<T>(string key)
        {
            var cacheFile = Path.Combine(CachePath, $"{key}.cache");
            if (!File.Exists(cacheFile))
                return default;

            try
            {
                var json = File.ReadAllText(cacheFile);
                var entry = JsonSerializer.Deserialize<CacheEntry<T>>(json);
                if (entry == null || entry.ExpiresAt < DateTime.Now)
                {
                    File.Delete(cacheFile);
                    return default;
                }
                return entry.Data;
            }
            catch
            {
                return default;
            }
        }

        /// <summary>
        /// Cache leeren
        /// </summary>
        public void ClearCache()
        {
            if (Directory.Exists(CachePath))
            {
                foreach (var file in Directory.GetFiles(CachePath, "*.cache"))
                {
                    try { File.Delete(file); } catch { }
                }
            }
        }

        #endregion

        #region Anwendungs-Einstellungen (zentral)

        /// <summary>
        /// Zentrale App-Einstellungen laden
        /// </summary>
        public AppEinstellungen GetAppEinstellungen()
        {
            var settings = Load<AppEinstellungen>("app_einstellungen");
            if (settings == null)
            {
                settings = AppEinstellungen.GetDefaults();
                SaveAppEinstellungen(settings);
            }
            return settings;
        }

        /// <summary>
        /// Zentrale App-Einstellungen speichern
        /// </summary>
        public void SaveAppEinstellungen(AppEinstellungen settings)
        {
            settings.GeaendertAm = DateTime.Now;
            Save("app_einstellungen", settings);
        }

        #endregion

        #region Artikel-Mapping (JTL)

        /// <summary>
        /// Artikel-Mapping-Konfiguration laden
        /// </summary>
        public ArtikelMappingConfig GetArtikelMapping()
        {
            var mapping = Load<ArtikelMappingConfig>("artikel_mapping");
            if (mapping == null)
            {
                mapping = ArtikelMappingConfig.GetDefaults();
                SaveArtikelMapping(mapping);
            }
            return mapping;
        }

        /// <summary>
        /// Artikel-Mapping-Konfiguration speichern
        /// </summary>
        public void SaveArtikelMapping(ArtikelMappingConfig mapping)
        {
            mapping.GeaendertAm = DateTime.Now;
            Save("artikel_mapping", mapping);
        }

        #endregion

        #region Auftrags-Einstellungen

        /// <summary>
        /// Auftrags-Standard-Einstellungen laden
        /// </summary>
        public AuftragEinstellungen GetAuftragEinstellungen()
        {
            var settings = Load<AuftragEinstellungen>("auftrag_einstellungen");
            if (settings == null)
            {
                settings = AuftragEinstellungen.GetDefaults();
                SaveAuftragEinstellungen(settings);
            }
            return settings;
        }

        /// <summary>
        /// Auftrags-Standard-Einstellungen speichern
        /// </summary>
        public void SaveAuftragEinstellungen(AuftragEinstellungen settings)
        {
            settings.GeaendertAm = DateTime.Now;
            Save("auftrag_einstellungen", settings);
        }

        #endregion
    }

    #region Datenklassen

    public class UserSettings
    {
        public Dictionary<string, object> Values { get; set; } = new();
        public DateTime LastModified { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Zentrale Anwendungseinstellungen
    /// </summary>
    public class AppEinstellungen
    {
        // Allgemein
        public string Sprache { get; set; } = "de-DE";
        public string Waehrung { get; set; } = "EUR";
        public string Datumsformat { get; set; } = "dd.MM.yyyy";
        public int StandardMwStSatz { get; set; } = 19;
        public int ReduzierterMwStSatz { get; set; } = 7;

        // Firma
        public int StandardFirmaId { get; set; } = 1;
        public int StandardMandantId { get; set; } = 1;

        // Nummernkreise
        public string AuftragNrPrefix { get; set; } = "A";
        public string RechnungNrPrefix { get; set; } = "R";
        public string LieferscheinNrPrefix { get; set; } = "L";

        // Druck
        public bool AutoDruckLieferschein { get; set; } = false;
        public bool AutoDruckRechnung { get; set; } = false;
        public string StandardDrucker { get; set; } = "";

        // E-Mail
        public bool AutoEmailRechnung { get; set; } = false;
        public string EmailAbsender { get; set; } = "";
        public string SmtpServer { get; set; } = "";
        public int SmtpPort { get; set; } = 587;

        // Performance
        public int MaxSuchergebnisse { get; set; } = 200;
        public int CacheZeitMinuten { get; set; } = 30;
        public bool LadeArtikelbilderAsync { get; set; } = true;

        // UI
        public bool ZeigeArtikelbilder { get; set; } = true;
        public bool ZeigePreiseInListe { get; set; } = true;
        public bool KompakteAnsicht { get; set; } = false;
        public string Theme { get; set; } = "Standard";

        public DateTime GeaendertAm { get; set; } = DateTime.Now;

        public static AppEinstellungen GetDefaults() => new AppEinstellungen();
    }

    /// <summary>
    /// Artikel-Mapping-Konfiguration für Import/Export
    /// </summary>
    public class ArtikelMappingConfig
    {
        // Standard-Werte für neue Artikel
        public int StandardLagerId { get; set; } = 1;
        public int StandardEinheitId { get; set; } = 1;
        public string StandardEinheit { get; set; } = "Stk";
        public int StandardMwStKlasse { get; set; } = 1;
        public decimal StandardMwStSatz { get; set; } = 19m;
        public int StandardWarengruppeId { get; set; } = 0;
        public int StandardHerstellerId { get; set; } = 0;
        public int StandardLieferantId { get; set; } = 0;

        // Artikel-Typ-Mapping
        public int ArtikelTypStandard { get; set; } = 1;  // 1 = Artikel, 2 = Set, 3 = Variante
        public int ArtikelTypDigital { get; set; } = 4;   // Download/Digital
        public int ArtikelTypDienstleistung { get; set; } = 5;

        // Preis-Mapping
        public string StandardWaehrung { get; set; } = "EUR";
        public int StandardPreisgruppe { get; set; } = 1;
        public bool PreiseNetto { get; set; } = true;
        public decimal StandardAufschlag { get; set; } = 0m;
        public decimal StandardRabatt { get; set; } = 0m;

        // Lagerbestand
        public decimal MindestbestandStandard { get; set; } = 0m;
        public decimal BestellpunktStandard { get; set; } = 0m;
        public int WiederbestellzeitTage { get; set; } = 7;

        // Felder-Mapping (AppData-Feldname -> JTL-Feldname)
        public Dictionary<string, string> FeldMapping { get; set; } = new()
        {
            { "Artikelnummer", "cArtNr" },
            { "Name", "cName" },
            { "Beschreibung", "cBeschreibung" },
            { "Kurztext", "cKurzBeschreibung" },
            { "EAN", "cBarcode" },
            { "Gewicht", "fGewicht" },
            { "Preis", "fVKNetto" },
            { "EK", "fEKNetto" },
            { "Bestand", "fLagerbestand" }
        };

        // Eigene Felder Mapping
        public Dictionary<string, int> EigeneFelderMapping { get; set; } = new();

        public DateTime GeaendertAm { get; set; } = DateTime.Now;

        public static ArtikelMappingConfig GetDefaults() => new ArtikelMappingConfig();
    }

    /// <summary>
    /// Auftrags-Standard-Einstellungen
    /// </summary>
    public class AuftragEinstellungen
    {
        // Standard-Werte für neue Aufträge
        public int StandardVersandartId { get; set; } = 0;
        public string StandardVersandart { get; set; } = "";
        public int StandardZahlungsartId { get; set; } = 0;
        public string StandardZahlungsart { get; set; } = "";
        public int StandardZahlungszielTage { get; set; } = 14;
        public decimal StandardSkontoProzent { get; set; } = 0m;
        public int StandardSkontoTage { get; set; } = 0;

        // Lieferung
        public int StandardLieferzeitTage { get; set; } = 3;
        public bool TeillieferungErlaubt { get; set; } = true;
        public bool NachlieferungErlaubt { get; set; } = true;

        // Steuern
        public string StandardSteuerzone { get; set; } = "DE"; // DE, EU, NICHT-EU
        public bool PruefeUstIdNr { get; set; } = true;
        public bool AutoMwStBerechnung { get; set; } = true;

        // Rabatte
        public decimal MaxRabattProzent { get; set; } = 100m;
        public bool RabattErlaubt { get; set; } = true;

        // Dokumente
        public bool AutoLieferschein { get; set; } = false;
        public bool AutoRechnung { get; set; } = false;
        public bool ZeigeHinweistexte { get; set; } = true;

        // Workflow
        public string StandardStatus { get; set; } = "Offen";
        public int StandardVorgangsfarbe { get; set; } = 0;
        public bool AutoStatusWechsel { get; set; } = true;

        public DateTime GeaendertAm { get; set; } = DateTime.Now;

        public static AuftragEinstellungen GetDefaults() => new AuftragEinstellungen();
    }

    public class ServerProfil
    {
        public string Name { get; set; } = "";
        public string Beschreibung { get; set; } = "";
        public string Server { get; set; } = "";
        public string SqlBenutzer { get; set; } = "";
        public string SqlPasswort { get; set; } = "";
        public List<MandantInfo> Mandanten { get; set; } = new();
        public bool IstAktiv { get; set; }
    }

    public class MandantInfo
    {
        public string Name { get; set; } = "";
        public string Datenbank { get; set; } = "";
        public bool Aktiv { get; set; } = true;
    }

    public class LetzteAnmeldung
    {
        public string ProfilName { get; set; } = "";
        public string MandantName { get; set; } = "";
        public string Benutzer { get; set; } = "";
        public DateTime Zeitpunkt { get; set; }
    }

    public class WindowPosition
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool IsMaximized { get; set; }
    }

    public class CacheEntry<T>
    {
        public T? Data { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    /// <summary>
    /// Spalten-Konfiguration für eine View
    /// </summary>
    public class ViewColumnSettings
    {
        public string ViewName { get; set; } = "";
        public Dictionary<string, ColumnSetting> Columns { get; set; } = new();
    }

    /// <summary>
    /// Einstellungen für eine einzelne Spalte
    /// </summary>
    public class ColumnSetting
    {
        public bool IsVisible { get; set; } = true;
        public double Width { get; set; } = 100;
        public int DisplayIndex { get; set; }
        public string? SortDirection { get; set; } // "Ascending", "Descending", or null
        public int SortOrder { get; set; } = -1; // -1 = nicht sortiert, 0+ = Reihenfolge bei Multi-Sort
    }

    /// <summary>
    /// Alle Benutzer-View-Einstellungen
    /// </summary>
    public class UserViewSettings
    {
        public Dictionary<string, ViewColumnSettings> Views { get; set; } = new();

        public ViewColumnSettings GetViewSettings(string viewName)
        {
            if (!Views.ContainsKey(viewName))
                Views[viewName] = new ViewColumnSettings { ViewName = viewName };
            return Views[viewName];
        }

        public void SetColumnVisibility(string viewName, string columnName, bool isVisible)
        {
            var view = GetViewSettings(viewName);
            if (!view.Columns.ContainsKey(columnName))
                view.Columns[columnName] = new ColumnSetting();
            view.Columns[columnName].IsVisible = isVisible;
        }

        public bool GetColumnVisibility(string viewName, string columnName, bool defaultVisible = true)
        {
            if (!Views.ContainsKey(viewName)) return defaultVisible;
            if (!Views[viewName].Columns.ContainsKey(columnName)) return defaultVisible;
            return Views[viewName].Columns[columnName].IsVisible;
        }
    }

    #endregion
}

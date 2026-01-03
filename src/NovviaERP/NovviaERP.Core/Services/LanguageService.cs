using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Dapper;

namespace NovviaERP.Core.Services
{
    /// <summary>
    /// Sprachdienst - laedt Texte aus Datenbank (primaer) oder JSON-Fallback
    /// Verwendung: Lang.Get("Buttons.Speichern") -> "Speichern"
    /// </summary>
    public static class Lang
    {
        private static Dictionary<string, string> _strings = new();
        private static string _currentLanguage = "de";
        private static string? _connectionString;
        private static bool _isLoaded = false;

        /// <summary>Aktuelle Sprache (z.B. "de", "en")</summary>
        public static string CurrentLanguage => _currentLanguage;

        /// <summary>Ist geladen?</summary>
        public static bool IsLoaded => _isLoaded;

        /// <summary>
        /// Initialisieren mit DB-Verbindung
        /// </summary>
        public static async Task InitAsync(string connectionString, string language = "de")
        {
            _connectionString = connectionString;
            _currentLanguage = language;
            await LoadFromDbAsync();
        }

        /// <summary>
        /// Aus Datenbank laden
        /// </summary>
        public static async Task LoadFromDbAsync()
        {
            if (string.IsNullOrEmpty(_connectionString))
            {
                LoadFromFile();
                return;
            }

            try
            {
                using var conn = new SqlConnection(_connectionString);
                var rows = await conn.QueryAsync<(string Key, string Value)>(@"
                    IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'NOVVIA' AND TABLE_NAME = 'Sprache')
                        SELECT cSchluessel AS [Key], cWert AS Value
                        FROM NOVVIA.Sprache
                        WHERE cSprache = @Sprache
                    ELSE
                        SELECT NULL AS [Key], NULL AS Value WHERE 1=0
                ", new { Sprache = _currentLanguage });

                _strings.Clear();
                foreach (var row in rows)
                {
                    if (!string.IsNullOrEmpty(row.Key))
                        _strings[row.Key] = row.Value ?? row.Key;
                }

                // Wenn DB leer, JSON als Fallback
                if (_strings.Count == 0)
                    LoadFromFile();
                else
                    _isLoaded = true;
            }
            catch
            {
                LoadFromFile();
            }
        }

        /// <summary>
        /// Aus JSON-Datei laden (Fallback)
        /// </summary>
        public static void LoadFromFile(string? basePath = null)
        {
            basePath ??= AppDomain.CurrentDomain.BaseDirectory;
            var langFile = Path.Combine(basePath, "Resources", "Lang", $"{_currentLanguage}.json");

            if (!File.Exists(langFile))
            {
                _isLoaded = true;
                return;
            }

            try
            {
                var json = File.ReadAllText(langFile);
                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                if (dict != null)
                    FlattenJson(dict, "");
                _isLoaded = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Laden: {ex.Message}");
                _isLoaded = true;
            }
        }

        private static void FlattenJson(Dictionary<string, JsonElement> dict, string prefix)
        {
            foreach (var kvp in dict)
            {
                var key = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}.{kvp.Key}";

                if (kvp.Value.ValueKind == JsonValueKind.Object)
                {
                    var nested = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(kvp.Value.GetRawText());
                    if (nested != null)
                        FlattenJson(nested, key);
                }
                else if (kvp.Value.ValueKind == JsonValueKind.String)
                {
                    _strings[key] = kvp.Value.GetString() ?? key;
                }
            }
        }

        /// <summary>
        /// Text abrufen
        /// Beispiel: Lang.Get("Buttons.Speichern") -> "Speichern"
        /// </summary>
        public static string Get(string key, string? fallback = null)
        {
            if (!_isLoaded) LoadFromFile();

            return _strings.TryGetValue(key, out var value)
                ? value
                : fallback ?? key;
        }

        /// <summary>Kurzform fuer Get()</summary>
        public static string T(string key) => Get(key);

        /// <summary>Text mit Platzhaltern: Lang.Format("Hallo {0}", name)</summary>
        public static string Format(string key, params object[] args)
        {
            var template = Get(key);
            try { return string.Format(template, args); }
            catch { return template; }
        }

        /// <summary>Text in DB speichern/aktualisieren</summary>
        public static async Task SetAsync(string key, string value)
        {
            if (string.IsNullOrEmpty(_connectionString)) return;

            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.ExecuteAsync(@"
                    MERGE INTO NOVVIA.Sprache AS target
                    USING (SELECT @Schluessel AS cSchluessel, @Sprache AS cSprache) AS source
                    ON target.cSchluessel = source.cSchluessel AND target.cSprache = source.cSprache
                    WHEN MATCHED THEN UPDATE SET cWert = @Wert, dGeaendert = GETDATE()
                    WHEN NOT MATCHED THEN INSERT (cSchluessel, cSprache, cWert) VALUES (@Schluessel, @Sprache, @Wert);
                ", new { Schluessel = key, Sprache = _currentLanguage, Wert = value });

                _strings[key] = value;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lang.SetAsync Fehler: {ex.Message}");
            }
        }

        /// <summary>Alle Texte als Dictionary</summary>
        public static Dictionary<string, string> GetAll() => new(_strings);

        /// <summary>JSON aus DB in Datei exportieren</summary>
        public static async Task ExportToFileAsync(string filePath)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_strings, options);
            await File.WriteAllTextAsync(filePath, json);
        }

        /// <summary>JSON aus Datei in DB importieren</summary>
        public static async Task ImportFromFileAsync(string filePath)
        {
            if (!File.Exists(filePath) || string.IsNullOrEmpty(_connectionString)) return;

            var json = await File.ReadAllTextAsync(filePath);
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (dict == null) return;

            var flat = new Dictionary<string, string>();
            FlattenJsonToDict(dict, "", flat);

            using var conn = new SqlConnection(_connectionString);
            foreach (var kvp in flat)
            {
                await conn.ExecuteAsync(@"
                    MERGE INTO NOVVIA.Sprache AS target
                    USING (SELECT @Schluessel AS cSchluessel, @Sprache AS cSprache) AS source
                    ON target.cSchluessel = source.cSchluessel AND target.cSprache = source.cSprache
                    WHEN MATCHED THEN UPDATE SET cWert = @Wert, dGeaendert = GETDATE()
                    WHEN NOT MATCHED THEN INSERT (cSchluessel, cSprache, cWert) VALUES (@Schluessel, @Sprache, @Wert);
                ", new { Schluessel = kvp.Key, Sprache = _currentLanguage, Wert = kvp.Value });
            }

            await LoadFromDbAsync();
        }

        private static void FlattenJsonToDict(Dictionary<string, JsonElement> dict, string prefix, Dictionary<string, string> result)
        {
            foreach (var kvp in dict)
            {
                var key = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}.{kvp.Key}";

                if (kvp.Value.ValueKind == JsonValueKind.Object)
                {
                    var nested = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(kvp.Value.GetRawText());
                    if (nested != null)
                        FlattenJsonToDict(nested, key, result);
                }
                else if (kvp.Value.ValueKind == JsonValueKind.String)
                {
                    result[key] = kvp.Value.GetString() ?? key;
                }
            }
        }

        /// <summary>Alle verfuegbaren Sprachen auflisten</summary>
        public static IEnumerable<string> GetAvailableLanguages(string? basePath = null)
        {
            basePath ??= AppDomain.CurrentDomain.BaseDirectory;
            var langDir = Path.Combine(basePath, "Resources", "Lang");

            if (!Directory.Exists(langDir))
                yield break;

            foreach (var file in Directory.GetFiles(langDir, "*.json"))
            {
                yield return Path.GetFileNameWithoutExtension(file);
            }
        }
    }
}

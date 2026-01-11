using System;
using System.IO;
using System.Text.Json;

namespace BestFlex.Shell.Infrastructure
{
    /// <summary>
    /// Tiny JSON-backed preferences store (per-machine user).
    /// Safe: IO failures never throw outward.
    /// </summary>
    public sealed class UserPrefs
    {
        public static UserPrefs Current { get; private set; } = Load();

        // --- Preferences (add more as needed) ---
        public int InvoicePageSize { get; set; } = 25;
        public string Theme { get; set; } = "Light"; // "Light" or "Dark"

        // --------- persistence ----------
        private static readonly string _dir =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BestFlex");

        private static readonly string _file = Path.Combine(_dir, "prefs.json");

        private static UserPrefs Load()
        {
            try
            {
                if (File.Exists(_file))
                {
                    var json = File.ReadAllText(_file);
                    var obj = JsonSerializer.Deserialize<UserPrefs>(json);
                    if (obj != null) return obj;
                }
            }
            catch { /* ignore */ }

            return new UserPrefs();
        }

        public static void Save()
        {
            try
            {
                if (!Directory.Exists(_dir)) Directory.CreateDirectory(_dir);
                var json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_file, json);
            }
            catch { /* ignore */ }
        }
    }
}

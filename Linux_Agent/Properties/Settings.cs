using System;
using System.IO;
using System.Text.Json;

namespace SystemMonitor.Properties
{
    internal sealed class Settings
    {
        private const string FileName = "settings.json";
        
        
        private static readonly string FilePath = Path.Combine(
            Path.GetDirectoryName(Environment.ProcessPath),
            FileName);

        public static Settings Default { get; set; } = Load();

        public string ServerUrl { get; set; } = "http://185.180.231.185:80";
        public string ConnectionCode { get; set; } = ""; 

        public Settings() { }

        private static Settings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    var s = JsonSerializer.Deserialize<Settings>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (s != null) return s;
                }
            }
            catch { }
            return new Settings();
        }

        public void Save()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
                var json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(FilePath, json);
            }
            catch { }
        }
    }
}
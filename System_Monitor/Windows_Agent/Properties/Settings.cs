using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization; 

namespace SystemMonitor.Properties
{
    internal sealed class Settings
    {
        private const string FileName = "settings.json";
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SystemMonitor",
            FileName);

        public static Settings Default { get; set; } = Load();

        
        public string ServerUrl { get; set; } = "http://185.180.231.185:80";
        public string ConnectionCode { get; set; } = ""; 

        
        public Settings() { }

        private static Settings Load()
        {
            try
            {
                var dir = Path.GetDirectoryName(FilePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                if (File.Exists(FilePath))
                {
                    var json = File.ReadAllText(FilePath);
                    
                    var s = JsonSerializer.Deserialize<Settings>(json, new JsonSerializerOptions
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                    
                    if (s != null) return s;
                }
            }
            catch (Exception) 
            {
                
            }

            return new Settings();
        }

        public void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(FilePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    IncludeFields = true 
                };
                
                var json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения настроек: {ex.Message}");
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace CursorBackup.Services
{
    /// <summary>
    /// Service for storing and retrieving application settings (like last used paths)
    /// </summary>
    public class SettingsStorage
    {
        private readonly string _settingsFilePath;
        private AppSettings _settings;

        public SettingsStorage()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CursorBackup"
            );
            
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }

            _settingsFilePath = Path.Combine(appDataPath, "settings.json");
            _settings = LoadSettings();
        }

        public string GetLastPath(string key, string defaultValue = "")
        {
            if (_settings.LastPaths.TryGetValue(key, out var path))
            {
                return path;
            }
            return defaultValue;
        }

        public void SetLastPath(string key, string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                // Check if it's a directory or file
                if (Directory.Exists(path) || File.Exists(path))
                {
                    _settings.LastPaths[key] = path;
                    SaveSettings();
                }
                else
                {
                    // If path doesn't exist yet, still save it (might be a new file to create)
                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        _settings.LastPaths[key] = dir; // Save parent directory
                        SaveSettings();
                    }
                }
            }
        }

        private AppSettings LoadSettings()
        {
            if (File.Exists(_settingsFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_settingsFilePath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                catch
                {
                    return new AppSettings();
                }
            }
            return new AppSettings();
        }

        private void SaveSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch
            {
                // Ignore save errors
            }
        }
    }

    public class AppSettings
    {
        public Dictionary<string, string> LastPaths { get; set; } = new();
    }
}


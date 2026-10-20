using System.Text.Json;

namespace MikuScreenRecorder
{
    public class AppSettings
    {
        public string SavePath { get; set; } = @"C:\ScreenRecorder";
        public bool CopyToClipboard { get; set; } = true;
        public bool SaveToDisk { get; set; } = true;
        public int GifFps { get; set; } = 15;
        public int GifMaxSeconds { get; set; } = 60;
        public bool CaptureCursor { get; set; } = true;
        public bool GifDithering { get; set; } = false;
        public bool ShowNotifications { get; set; } = true;
        public string? Language { get; set; }

        private static readonly string SettingsFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ScreenRecorder", "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                    return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsFile)) ?? new AppSettings();
            }
            catch { }
            return new AppSettings();
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}

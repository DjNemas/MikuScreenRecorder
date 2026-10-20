namespace MikuScreenRecorder
{
    internal class Program
    {
        [STAThread]
        static void Main()
        {
            using var mutex = new Mutex(true, "ScreenRecorder_SingleInstance", out bool isNew);
            if (!isNew) return;

            var settings = AppSettings.Load();
            string language = Localization.Resolve(settings.Language);
            Localization.SetLanguage(language);
            if (settings.Language != language)
            {
                settings.Language = language;
                settings.Save();
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApp());
        }
    }
}

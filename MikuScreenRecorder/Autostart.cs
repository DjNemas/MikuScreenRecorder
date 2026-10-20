using Microsoft.Win32;

namespace MikuScreenRecorder
{
    internal static class Autostart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "ScreenRecorder";

        private static string ExePath => Environment.ProcessPath ?? Application.ExecutablePath;

        public static bool IsEnabled
        {
            get
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey);
                return key?.GetValue(ValueName) is string value &&
                       value.Trim('"').Equals(ExePath, StringComparison.OrdinalIgnoreCase);
            }
        }

        public static void Set(bool enabled)
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
            if (enabled)
                key.SetValue(ValueName, $"\"{ExePath}\"");
            else if (key.GetValue(ValueName) != null)
                key.DeleteValue(ValueName, false);
        }
    }
}

namespace MikuScreenRecorder
{
    internal static class AppIcon
    {
        private static Icon? _icon;

        /// <summary>
        /// Liefert das beim Kompilieren eingebettete Anwendungssymbol in passender Größe.
        /// </summary>
        public static Icon Get(int size = 0)
        {
            if (size <= 0) size = SystemInformation.SmallIconSize.Width;
            using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream("MikuScreenRecorder.icon.ico");
            if (stream != null)
                return new Icon(stream, size, size);

            return _icon ??= (Environment.ProcessPath is string exe ? Icon.ExtractAssociatedIcon(exe) : null)
                             ?? SystemIcons.Application;
        }
    }
}

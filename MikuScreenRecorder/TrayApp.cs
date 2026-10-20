using System.Collections.Specialized;
using System.Diagnostics;
using System.Drawing.Imaging;
using MikuScreenRecorder.Resources;

namespace MikuScreenRecorder
{
    internal sealed class TrayApp : ApplicationContext
    {
        private const int HotkeyScreenshot = 1;
        private const int HotkeyGif = 2;

        private readonly AppSettings _settings = AppSettings.Load();
        private readonly NotifyIcon _tray;
        private readonly HotkeyWindow _hotkeys;
        private readonly ToolStripMenuItem _clipboardItem;
        private readonly ToolStripMenuItem _saveItem;
        private readonly ToolStripMenuItem _gifItem;
        private readonly ToolStripMenuItem _autostartItem;
        private readonly ToolStripMenuItem _screenshotItem;
        private readonly ToolStripMenuItem _openFolderItem;
        private readonly ToolStripMenuItem _settingsItem;
        private readonly ToolStripMenuItem _exitItem;

        private GifRecorder? _recorder;
        private RecordingFrame? _frame;
        private StopButtonForm? _stopButton;
        private string? _gifPath;
        private string? _notifiedFile;
        private bool _busy;

        public TrayApp()
        {
            _clipboardItem = new ToolStripMenuItem(string.Empty, null, (_, _) => Toggle(s => s.CopyToClipboard = !s.CopyToClipboard));
            _saveItem = new ToolStripMenuItem(string.Empty, null, (_, _) => Toggle(s => s.SaveToDisk = !s.SaveToDisk));
            _gifItem = new ToolStripMenuItem(string.Empty, null, async (_, _) => await ToggleGifAsync());
            _autostartItem = new ToolStripMenuItem(string.Empty, null, (_, _) => ToggleAutostart());
            _screenshotItem = new ToolStripMenuItem(string.Empty, null, (_, _) => TakeScreenshot());
            _openFolderItem = new ToolStripMenuItem(string.Empty, null, (_, _) => OpenFolder());
            _settingsItem = new ToolStripMenuItem(string.Empty, null, (_, _) => ShowSettings());
            _exitItem = new ToolStripMenuItem(string.Empty, null, async (_, _) => await ExitAsync());

            var menu = new ContextMenuStrip();
            menu.Items.Add(_screenshotItem);
            menu.Items.Add(_gifItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_clipboardItem);
            menu.Items.Add(_saveItem);
            menu.Items.Add(_autostartItem);
            menu.Items.Add(_openFolderItem);
            menu.Items.Add(_settingsItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(_exitItem);
            menu.Opening += (_, _) => UpdateMenu();

            _tray = new NotifyIcon
            {
                Icon = AppIcon.Get(),
                Text = "ScreenRecorder",
                ContextMenuStrip = menu,
                Visible = true
            };
            _tray.DoubleClick += (_, _) => ShowSettings();
            _tray.BalloonTipClicked += (_, _) => OpenNotifiedFile();

            ApplyTexts();
            Localization.LanguageChanged += ApplyTexts;

            _hotkeys = new HotkeyWindow();
            _hotkeys.HotkeyPressed += async id =>
            {
                if (id == HotkeyScreenshot) TakeScreenshot();
                else if (id == HotkeyGif) await ToggleGifAsync();
            };

            bool ok1 = _hotkeys.Register(HotkeyScreenshot, NativeMethods.MOD_NOREPEAT, NativeMethods.VK_SNAPSHOT);
            bool ok2 = _hotkeys.Register(HotkeyGif, NativeMethods.MOD_CONTROL | NativeMethods.MOD_NOREPEAT, NativeMethods.VK_SNAPSHOT);
            if (!ok1 || !ok2)
            {
                _tray.ShowBalloonTip(5000, "ScreenRecorder", Strings.HotkeyFailed, ToolTipIcon.Warning);
            }
            else
            {
                Notify(Strings.Running);
            }
        }

        private void ApplyTexts()
        {
            _screenshotItem.Text = Strings.MenuScreenshot;
            _clipboardItem.Text = Strings.MenuCopyToClipboard;
            _saveItem.Text = Strings.MenuSaveToFile;
            _autostartItem.Text = Strings.MenuStartWithWindows;
            _openFolderItem.Text = Strings.MenuOpenFolder;
            _settingsItem.Text = Strings.MenuSettings;
            _exitItem.Text = Strings.MenuExit;
            _gifItem.Text = _recorder == null ? Strings.MenuGifStart : Strings.MenuGifStop;
            if (_recorder != null) _tray.Text = Strings.TrayRecording;
        }

        private void Toggle(Action<AppSettings> change)
        {
            change(_settings);
            _settings.Save();
        }

        private void ToggleAutostart()
        {
            try { Autostart.Set(!Autostart.IsEnabled); }
            catch (Exception ex) { Error(ex); }
        }

        private void UpdateMenu()
        {
            _clipboardItem.Checked = _settings.CopyToClipboard;
            _saveItem.Checked = _settings.SaveToDisk;
            _autostartItem.Checked = Autostart.IsEnabled;
            _gifItem.Text = _recorder == null ? Strings.MenuGifStart : Strings.MenuGifStop;
        }

        private void TakeScreenshot()
        {
            if (_busy || _recorder != null) return;
            _busy = true;
            try
            {
                var region = RegionSelector.Select(Strings.SelectScreenshotRegion);
                if (region == null) return;

                using var bmp = ScreenCapture.Capture(region.Value, false);
                string? file = null;
                if (_settings.SaveToDisk)
                {
                    file = NewFilePath("png");
                    bmp.Save(file, ImageFormat.Png);
                }
                if (_settings.CopyToClipboard)
                    Clipboard.SetImage(bmp);

                Notify(file != null ? string.Format(Strings.ScreenshotSaved, file) : Strings.ScreenshotCopied, file);
            }
            catch (Exception ex)
            {
                Error(ex);
            }
            finally
            {
                _busy = false;
            }
        }

        private async Task ToggleGifAsync()
        {
            if (_recorder != null)
            {
                await StopGifAsync();
                return;
            }
            if (_busy) return;

            _busy = true;
            try
            {
                var region = RegionSelector.Select(Strings.SelectGifRegion);
                if (region == null) return;

                _gifPath = _settings.SaveToDisk
                    ? NewFilePath("gif")
                    : Path.Combine(Path.GetTempPath(), $"ScreenRecorder_{DateTime.Now:yyyyMMdd_HHmmss}.gif");

                _frame = new RecordingFrame(region.Value);
                _frame.Show();

                _stopButton = new StopButtonForm(region.Value);
                _stopButton.StopClicked += async () => await StopGifAsync();
                _stopButton.Show();

                _recorder = new GifRecorder(region.Value, _settings, _gifPath);
                _recorder.MaxDurationReached += () => _hotkeys.BeginInvokeSafe(async () => await StopGifAsync());
                _recorder.Start();
                _tray.Text = Strings.TrayRecording;
                Notify(Strings.GifStarted);
            }
            catch (Exception ex)
            {
                Error(ex);
            }
            finally
            {
                _busy = false;
            }
        }

        private async Task StopGifAsync()
        {
            var recorder = _recorder;
            if (recorder == null) return;
            _recorder = null;

            try
            {
                _stopButton?.Close();
                _stopButton?.Dispose();
                _stopButton = null;
                await recorder.StopAsync();
                _frame?.Close();
                _frame?.Dispose();
                _frame = null;
                _tray.Text = "ScreenRecorder";

                if (_gifPath == null) return;
                if (_settings.CopyToClipboard)
                    CopyGifToClipboard(_gifPath);

                Notify(_settings.SaveToDisk ? string.Format(Strings.GifSaved, _gifPath) : Strings.GifCopied,
                    _settings.SaveToDisk ? _gifPath : null);
            }
            catch (Exception ex)
            {
                Error(ex);
            }
        }

        private static void CopyGifToClipboard(string path)
        {
            var data = new DataObject();
            var bytes = File.ReadAllBytes(path);
            data.SetData("GIF", false, new MemoryStream(bytes));
            data.SetFileDropList(new StringCollection { path });
            using (var img = Image.FromStream(new MemoryStream(bytes)))
                data.SetImage(new Bitmap(img));
            Clipboard.SetDataObject(data, true);
        }

        private string NewFilePath(string ext)
        {
            Directory.CreateDirectory(_settings.SavePath);
            string baseName = $"{(ext == "gif" ? "GIF" : "Screenshot")}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
            string path = Path.Combine(_settings.SavePath, $"{baseName}.{ext}");
            for (int i = 1; File.Exists(path); i++)
                path = Path.Combine(_settings.SavePath, $"{baseName}_{i}.{ext}");
            return path;
        }

        private void OpenFolder()
        {
            Directory.CreateDirectory(_settings.SavePath);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_settings.SavePath}\"") { UseShellExecute = true });
        }

        private void ShowSettings()
        {
            using var f = new SettingsForm(_settings);
            f.ShowDialog();
        }

        private void Notify(string text, string? file = null)
        {
            _notifiedFile = file;
            if (_settings.ShowNotifications)
                _tray.ShowBalloonTip(2500, "ScreenRecorder", text, ToolTipIcon.Info);
        }

        private void OpenNotifiedFile()
        {
            var file = _notifiedFile;
            if (file == null) return;
            try
            {
                if (File.Exists(file))
                    Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{file}\"") { UseShellExecute = true });
                else
                    OpenFolder();
            }
            catch (Exception ex)
            {
                Error(ex);
            }
        }

        private void Error(Exception ex)
        {
            _notifiedFile = null;
            _tray.ShowBalloonTip(5000, Strings.ErrorTitle, ex.Message, ToolTipIcon.Error);
        }

        private async Task ExitAsync()
        {
            await StopGifAsync();
            _tray.Visible = false;
            _tray.Dispose();
            _hotkeys.Dispose();
            Localization.LanguageChanged -= ApplyTexts;
            ExitThread();
        }

        private sealed class HotkeyWindow : NativeWindow, IDisposable
        {
            private readonly List<int> _ids = new();
            private readonly SynchronizationContext _sync;
            public event Action<int>? HotkeyPressed;

            public HotkeyWindow()
            {
                CreateHandle(new CreateParams());
                _sync = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
            }

            public bool Register(int id, uint modifiers, uint vk)
            {
                bool ok = NativeMethods.RegisterHotKey(Handle, id, modifiers, vk);
                if (ok) _ids.Add(id);
                return ok;
            }

            public void BeginInvokeSafe(Action action) => _sync.Post(_ => action(), null);

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == NativeMethods.WM_HOTKEY)
                    HotkeyPressed?.Invoke((int)m.WParam);
                base.WndProc(ref m);
            }

            public void Dispose()
            {
                foreach (var id in _ids) NativeMethods.UnregisterHotKey(Handle, id);
                DestroyHandle();
            }
        }
    }
}

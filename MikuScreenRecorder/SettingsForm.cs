using System.Globalization;
using MikuScreenRecorder.Resources;

namespace MikuScreenRecorder
{
    internal sealed class SettingsForm : Form
    {
        private readonly AppSettings _settings;
        private readonly string _originalLanguage;
        private readonly TextBox _path = new() { Width = 300 };
        private readonly CheckBox _clipboard = new() { AutoSize = true };
        private readonly CheckBox _save = new() { AutoSize = true };
        private readonly CheckBox _cursor = new() { AutoSize = true };
        private readonly CheckBox _notify = new() { AutoSize = true };
        private readonly CheckBox _autostart = new() { AutoSize = true };
        private readonly CheckBox _dither = new() { AutoSize = true };
        private readonly ComboBox _language = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
        private readonly NumericUpDown _fps = new() { Minimum = 1, Maximum = 50, Width = 80 };
        private readonly NumericUpDown _max = new() { Minimum = 1, Maximum = 3600, Width = 80 };
        private readonly Label _pathLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
        private readonly Label _fpsLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
        private readonly Label _maxLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
        private readonly Label _languageLabel = new() { AutoSize = true, Anchor = AnchorStyles.Left };
        private readonly Button _cancel = new() { DialogResult = DialogResult.Cancel };

        public SettingsForm(AppSettings settings)
        {
            _settings = settings;
            _originalLanguage = Localization.CurrentLanguage;
            Icon = AppIcon.Get();
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(10);

            var browse = new Button { Text = "...", Width = 30 };
            browse.Click += (_, _) =>
            {
                using var dlg = new FolderBrowserDialog { SelectedPath = _path.Text };
                if (dlg.ShowDialog(this) == DialogResult.OK) _path.Text = dlg.SelectedPath;
            };

            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK };
            ok.Click += (_, _) => Apply();
            AcceptButton = ok;
            CancelButton = _cancel;

            var layout = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
            layout.Controls.Add(_pathLabel);
            var pathRow = new FlowLayoutPanel { AutoSize = true, Margin = Padding.Empty };
            pathRow.Controls.Add(_path);
            pathRow.Controls.Add(browse);
            layout.Controls.Add(pathRow);

            layout.Controls.Add(_fpsLabel);
            layout.Controls.Add(_fps);
            layout.Controls.Add(_maxLabel);
            layout.Controls.Add(_max);
            layout.Controls.Add(_languageLabel);
            layout.Controls.Add(_language);

            foreach (var cb in new[] { _clipboard, _save, _cursor, _dither, _notify, _autostart })
            {
                layout.Controls.Add(cb);
                layout.SetColumnSpan(cb, 2);
            }

            var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill };
            buttons.Controls.Add(_cancel);
            buttons.Controls.Add(ok);
            layout.Controls.Add(buttons);
            layout.SetColumnSpan(buttons, 2);
            Controls.Add(layout);

            _path.Text = settings.SavePath;
            _clipboard.Checked = settings.CopyToClipboard;
            _save.Checked = settings.SaveToDisk;
            _cursor.Checked = settings.CaptureCursor;
            _notify.Checked = settings.ShowNotifications;
            _autostart.Checked = Autostart.IsEnabled;
            _dither.Checked = settings.GifDithering;
            _fps.Value = Math.Clamp(settings.GifFps, 1, 50);
            _max.Value = Math.Clamp(settings.GifMaxSeconds, 1, 3600);

            _language.DisplayMember = nameof(CultureInfo.NativeName);
            foreach (var culture in Localization.AvailableLanguages)
                _language.Items.Add(culture);
            _language.SelectedItem = Localization.AvailableLanguages.FirstOrDefault(c => c.Name == Localization.CurrentLanguage);
            _language.SelectedIndexChanged += (_, _) =>
            {
                if (_language.SelectedItem is CultureInfo c) Localization.SetLanguage(c.Name);
            };

            ApplyTexts();
            Localization.LanguageChanged += ApplyTexts;
        }

        private void ApplyTexts()
        {
            Text = Strings.SettingsTitle;
            _pathLabel.Text = Strings.SettingsSavePath;
            _fpsLabel.Text = Strings.SettingsGifFps;
            _maxLabel.Text = Strings.SettingsGifMax;
            _languageLabel.Text = Strings.SettingsLanguage;
            _clipboard.Text = Strings.SettingsAutoClipboard;
            _save.Text = Strings.SettingsAutoSave;
            _cursor.Text = Strings.SettingsCursor;
            _notify.Text = Strings.SettingsNotifications;
            _autostart.Text = Strings.MenuStartWithWindows;
            _dither.Text = Strings.SettingsDither;
            _cancel.Text = Strings.Cancel;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Localization.LanguageChanged -= ApplyTexts;
            if (DialogResult != DialogResult.OK)
                Localization.SetLanguage(_originalLanguage);
            base.OnFormClosed(e);
        }

        private void Apply()
        {
            _settings.SavePath = string.IsNullOrWhiteSpace(_path.Text) ? @"C:\ScreenRecorder" : _path.Text.Trim();
            _settings.CopyToClipboard = _clipboard.Checked;
            _settings.SaveToDisk = _save.Checked;
            _settings.CaptureCursor = _cursor.Checked;
            _settings.ShowNotifications = _notify.Checked;
            _settings.GifDithering = _dither.Checked;
            _settings.GifFps = (int)_fps.Value;
            _settings.GifMaxSeconds = (int)_max.Value;
            _settings.Language = Localization.CurrentLanguage;
            _settings.Save();
            try { Autostart.Set(_autostart.Checked); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Autostart", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }
}

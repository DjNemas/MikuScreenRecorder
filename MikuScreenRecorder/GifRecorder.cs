using System.Diagnostics;

namespace MikuScreenRecorder
{
    /// <summary>
    /// Zeichnet einen roten Rahmen um den Aufnahmebereich (außerhalb, damit er nicht mit aufgenommen wird).
    /// Das Fenster ist klick-durchlässig.
    /// </summary>
    internal sealed class RecordingFrame : Form
    {
        private const int Border = 3;

        public RecordingFrame(Rectangle region)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.Magenta;
            TransparencyKey = Color.Magenta;
            Bounds = Rectangle.Inflate(region, Border, Border);
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80000 | 0x20 | 0x80; // WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW
                return cp;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using var pen = new Pen(Color.Red, Border);
            e.Graphics.DrawRectangle(pen, Border / 2f, Border / 2f, Width - Border, Height - Border);
        }
    }

    /// <summary>
    /// Kleines Fenster mit Stopp-Button unten links unter dem Aufnahmebereich.
    /// </summary>
    internal sealed class StopButtonForm : Form
    {
        private readonly System.Windows.Forms.Timer _timer = new() { Interval = 500 };
        private readonly Stopwatch _sw = Stopwatch.StartNew();
        private readonly Button _button;

        public event Action? StopClicked;

        public StopButtonForm(Rectangle region)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(32, 32, 32);
            Padding = new Padding(2);
            Size = new Size(120, 32);

            _button = new Button
            {
                Text = $"{Resources.Strings.Stop}  00:00",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Cyan,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            _button.FlatAppearance.BorderSize = 0;
            _button.Click += (_, _) =>
            {
                _button.Enabled = false;
                StopClicked?.Invoke();
            };
            Controls.Add(_button);

            const int gap = 6;
            var screen = Screen.FromRectangle(region).Bounds;
            int x = Math.Clamp(region.Left - 3, screen.Left, screen.Right - Width);
            int y = region.Bottom + 3 + gap;
            if (y + Height > screen.Bottom)
                y = region.Top - 3 - gap - Height;
            if (y < screen.Top)
                y = region.Bottom - Height - gap; // Notfall: innerhalb des Bereichs
            Location = new Point(x, y);

            _timer.Tick += (_, _) =>
            {
                var t = _sw.Elapsed;
                _button.Text = $"{Resources.Strings.Stop}  {(int)t.TotalMinutes:00}:{t.Seconds:00}";
            };
            _timer.Start();
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80 | 0x08000000; // WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE
                return cp;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer.Dispose();
            base.Dispose(disposing);
        }
    }

    internal sealed class GifRecorder
    {
        private readonly Rectangle _region;
        private readonly AppSettings _settings;
        private readonly string _filePath;
        private CancellationTokenSource? _cts;
        private Task? _task;

        public GifRecorder(Rectangle region, AppSettings settings, string filePath)
        {
            _region = region;
            _settings = settings;
            _filePath = filePath;
        }

        public event Action? MaxDurationReached;

        public void Start()
        {
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _task = Task.Run(() => Record(token), token);
        }

        public async Task StopAsync()
        {
            _cts?.Cancel();
            if (_task != null)
            {
                try { await _task; } catch (OperationCanceledException) { }
            }
        }

        private void Record(CancellationToken token)
        {
            int fps = Math.Clamp(_settings.GifFps, 1, 50);
            int interval = 1000 / fps;
            long maxMs = Math.Max(1, _settings.GifMaxSeconds) * 1000L;

            using var writer = new GifWriter(File.Create(_filePath), _settings.GifDithering);
            var sw = Stopwatch.StartNew();
            Bitmap? pending = null;
            long pendingTime = 0;

            try
            {
                while (true)
                {
                    long now = sw.ElapsedMilliseconds;
                    var frame = ScreenCapture.Capture(_region, _settings.CaptureCursor);

                    if (pending != null)
                    {
                        writer.AddFrame(pending, (int)(now - pendingTime));
                        pending.Dispose();
                    }
                    pending = frame;
                    pendingTime = now;

                    if (token.IsCancellationRequested) break;
                    if (now >= maxMs)
                    {
                        MaxDurationReached?.Invoke();
                        break;
                    }

                    long wait = interval - (sw.ElapsedMilliseconds - now);
                    if (wait > 0 && token.WaitHandle.WaitOne((int)wait)) { }
                }
            }
            finally
            {
                if (pending != null)
                {
                    writer.AddFrame(pending, interval);
                    pending.Dispose();
                }
            }
        }
    }
}

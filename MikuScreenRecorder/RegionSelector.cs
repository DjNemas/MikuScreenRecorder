namespace MikuScreenRecorder
{
    /// <summary>
    /// Vollbild-Overlay über alle Monitore, in dem per Maus ein Bereich aufgezogen wird.
    /// </summary>
    internal sealed class RegionSelector : Form
    {
        private readonly Bitmap _background;
        private readonly Rectangle _virtual;
        private Point _start;
        private Rectangle _selection;
        private bool _dragging;
        private readonly string _hint;

        public Rectangle SelectedRegion { get; private set; }

        private RegionSelector(string hint)
        {
            _hint = hint;
            _virtual = SystemInformation.VirtualScreen;
            _background = ScreenCapture.Capture(_virtual, false);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = _virtual;
            TopMost = true;
            ShowInTaskbar = false;
            Cursor = Cursors.Cross;
            DoubleBuffered = true;
            KeyPreview = true;
        }

        public static Rectangle? Select(string hint)
        {
            using var f = new RegionSelector(hint);
            return f.ShowDialog() == DialogResult.OK ? f.SelectedRegion : null;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
            base.OnKeyDown(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }
            _dragging = true;
            _start = e.Location;
            _selection = new Rectangle(e.Location, Size.Empty);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!_dragging) return;
            _selection = Rectangle.FromLTRB(
                Math.Min(_start.X, e.X), Math.Min(_start.Y, e.Y),
                Math.Max(_start.X, e.X), Math.Max(_start.Y, e.Y));
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (!_dragging) return;
            _dragging = false;
            if (_selection.Width < 3 || _selection.Height < 3)
            {
                _selection = Rectangle.Empty;
                Invalidate();
                return;
            }
            SelectedRegion = new Rectangle(_selection.X + _virtual.X, _selection.Y + _virtual.Y, _selection.Width, _selection.Height);
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.DrawImageUnscaled(_background, 0, 0);
            using (var dim = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
            {
                using var region = new Region(ClientRectangle);
                if (!_selection.IsEmpty) region.Exclude(_selection);
                g.FillRegion(dim, region);
            }

            if (!_selection.IsEmpty)
            {
                using var pen = new Pen(Color.DeepSkyBlue, 2);
                g.DrawRectangle(pen, _selection);
                var txt = $"{_selection.Width} x {_selection.Height}";
                g.DrawString(txt, Font, Brushes.White, _selection.X, Math.Max(0, _selection.Y - 18));
            }

            using var font = new Font("Segoe UI", 12, FontStyle.Bold);
            g.DrawString(_hint + "   " + Resources.Strings.SelectionCancelHint, font, Brushes.White, 20, 20);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _background.Dispose();
            base.Dispose(disposing);
        }
    }
}

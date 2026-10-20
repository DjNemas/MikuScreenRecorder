using System.Drawing.Imaging;

namespace MikuScreenRecorder
{
    internal static class ScreenCapture
    {
        public static Bitmap Capture(Rectangle area, bool includeCursor)
        {
            var bmp = new Bitmap(area.Width, area.Height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.CopyFromScreen(area.Location, Point.Empty, area.Size, CopyPixelOperation.SourceCopy);
            if (includeCursor)
                DrawCursor(g, area);
            return bmp;
        }

        private static void DrawCursor(Graphics g, Rectangle area)
        {
            var ci = new NativeMethods.CURSORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.CURSORINFO>() };
            if (!NativeMethods.GetCursorInfo(ref ci) || ci.flags != NativeMethods.CURSOR_SHOWING)
                return;

            int hx = 0, hy = 0;
            if (NativeMethods.GetIconInfo(ci.hCursor, out var info))
            {
                hx = info.xHotspot;
                hy = info.yHotspot;
                if (info.hbmMask != IntPtr.Zero) NativeMethods.DeleteObject(info.hbmMask);
                if (info.hbmColor != IntPtr.Zero) NativeMethods.DeleteObject(info.hbmColor);
            }

            int x = ci.ptScreenPos.X - area.X - hx;
            int y = ci.ptScreenPos.Y - area.Y - hy;
            var hdc = g.GetHdc();
            try { NativeMethods.DrawIcon(hdc, x, y, ci.hCursor); }
            finally { g.ReleaseHdc(hdc); }
        }
    }
}

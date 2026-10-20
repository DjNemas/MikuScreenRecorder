using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace MikuScreenRecorder
{
    /// <summary>
    /// Eigener GIF-Encoder: erzeugt pro Frame eine optimale 256-Farben-Palette (Median Cut),
    /// optional mit Floyd-Steinberg-Dithering, und kodiert per LZW.
    /// </summary>
    internal sealed class GifWriter : IDisposable
    {
        private readonly Stream _out;
        private readonly bool _dither;
        private bool _headerWritten;

        public GifWriter(Stream output, bool dither = false)
        {
            _out = output;
            _dither = dither;
        }

        public void AddFrame(Bitmap frame, int delayMs)
        {
            int w = frame.Width, h = frame.Height;
            if (!_headerWritten)
            {
                WriteHeader(w, h);
                _headerWritten = true;
            }

            int[] pixels = GetPixels(frame);
            var (palette, indices) = Quantize(pixels, w, h);

            WriteGraphicControl(delayMs);

            // Image Descriptor mit lokaler Farbtabelle (256 Einträge)
            _out.WriteByte(0x2C);
            WriteShort(0); WriteShort(0);
            WriteShort(w); WriteShort(h);
            _out.WriteByte(0x87);
            _out.Write(palette);

            WriteLzw(indices);
        }

        private static int[] GetPixels(Bitmap bmp)
        {
            var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var pixels = new int[bmp.Width * bmp.Height];
                for (int y = 0; y < bmp.Height; y++)
                    Marshal.Copy(data.Scan0 + y * data.Stride, pixels, y * bmp.Width, bmp.Width);
                return pixels;
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }

        #region Quantisierung (Median Cut)

        private struct Bin
        {
            public int Key;
            public int Count;
            public long R, G, B;
        }

        private (byte[] palette, byte[] indices) Quantize(int[] pixels, int w, int h)
        {
            // Histogramm mit 5 Bit pro Kanal, Summen für exakte Durchschnittsfarben
            var counts = new int[32768];
            var sums = new long[32768 * 3];
            foreach (int p in pixels)
            {
                int r = (p >> 16) & 0xFF, g = (p >> 8) & 0xFF, b = p & 0xFF;
                int key = ((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3);
                counts[key]++;
                sums[key * 3] += r;
                sums[key * 3 + 1] += g;
                sums[key * 3 + 2] += b;
            }

            var bins = new List<Bin>();
            for (int k = 0; k < counts.Length; k++)
                if (counts[k] > 0)
                    bins.Add(new Bin { Key = k, Count = counts[k], R = sums[k * 3], G = sums[k * 3 + 1], B = sums[k * 3 + 2] });

            var binArr = bins.ToArray();
            var boxes = new List<(int start, int end)> { (0, binArr.Length) };

            while (boxes.Count < 256)
            {
                int best = -1;
                long bestScore = 0;
                int bestAxis = 0;
                for (int i = 0; i < boxes.Count; i++)
                {
                    var (s, e) = boxes[i];
                    if (e - s < 2) continue;
                    GetRange(binArr, s, e, out int axis, out int range, out long pixelCount);
                    long score = range * (long)Math.Sqrt(pixelCount);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = i;
                        bestAxis = axis;
                    }
                }
                if (best < 0) break;

                var (bs, be) = boxes[best];
                int shift = bestAxis == 0 ? 10 : bestAxis == 1 ? 5 : 0;
                Array.Sort(binArr, bs, be - bs, Comparer<Bin>.Create((a, b) => ((a.Key >> shift) & 31).CompareTo((b.Key >> shift) & 31)));

                long total = 0;
                for (int i = bs; i < be; i++) total += binArr[i].Count;
                long acc = 0;
                int split = bs + 1;
                for (int i = bs; i < be - 1; i++)
                {
                    acc += binArr[i].Count;
                    split = i + 1;
                    if (acc * 2 >= total) break;
                }

                boxes[best] = (bs, split);
                boxes.Add((split, be));
            }

            var palette = new byte[256 * 3];
            var paletteInts = new int[boxes.Count * 3];
            var map = new byte[32768];
            for (int i = 0; i < boxes.Count; i++)
            {
                var (s, e) = boxes[i];
                long cnt = 0, r = 0, g = 0, b = 0;
                for (int j = s; j < e; j++)
                {
                    cnt += binArr[j].Count;
                    r += binArr[j].R; g += binArr[j].G; b += binArr[j].B;
                    map[binArr[j].Key] = (byte)i;
                }
                if (cnt == 0) cnt = 1;
                paletteInts[i * 3] = palette[i * 3] = (byte)(r / cnt);
                paletteInts[i * 3 + 1] = palette[i * 3 + 1] = (byte)(g / cnt);
                paletteInts[i * 3 + 2] = palette[i * 3 + 2] = (byte)(b / cnt);
            }

            var indices = new byte[pixels.Length];
            if (!_dither)
            {
                for (int i = 0; i < pixels.Length; i++)
                {
                    int p = pixels[i];
                    int key = ((((p >> 16) & 0xFF) >> 3) << 10) | ((((p >> 8) & 0xFF) >> 3) << 5) | ((p & 0xFF) >> 3);
                    indices[i] = map[key];
                }
                return (palette, indices);
            }

            // Floyd-Steinberg mit gecachter Nächster-Farbe-Suche
            var cache = new short[32768];
            Array.Fill(cache, (short)-1);
            int colorCount = boxes.Count;
            var errCur = new int[(w + 2) * 3];
            var errNext = new int[(w + 2) * 3];

            for (int y = 0; y < h; y++)
            {
                Array.Clear(errNext);
                for (int x = 0; x < w; x++)
                {
                    int p = pixels[y * w + x];
                    int ei = (x + 1) * 3;
                    int r = Math.Clamp(((p >> 16) & 0xFF) + errCur[ei] / 16, 0, 255);
                    int g = Math.Clamp(((p >> 8) & 0xFF) + errCur[ei + 1] / 16, 0, 255);
                    int b = Math.Clamp((p & 0xFF) + errCur[ei + 2] / 16, 0, 255);

                    int key = ((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3);
                    int idx = cache[key];
                    if (idx < 0)
                    {
                        idx = Nearest(paletteInts, colorCount, r, g, b);
                        cache[key] = (short)idx;
                    }
                    indices[y * w + x] = (byte)idx;

                    int dr = r - paletteInts[idx * 3];
                    int dg = g - paletteInts[idx * 3 + 1];
                    int db = b - paletteInts[idx * 3 + 2];

                    errCur[ei + 3] += dr * 7; errCur[ei + 4] += dg * 7; errCur[ei + 5] += db * 7;
                    errNext[ei - 3] += dr * 3; errNext[ei - 2] += dg * 3; errNext[ei - 1] += db * 3;
                    errNext[ei] += dr * 5; errNext[ei + 1] += dg * 5; errNext[ei + 2] += db * 5;
                    errNext[ei + 3] += dr; errNext[ei + 4] += dg; errNext[ei + 5] += db;
                }
                (errCur, errNext) = (errNext, errCur);
            }
            return (palette, indices);
        }

        private static void GetRange(Bin[] bins, int s, int e, out int axis, out int range, out long pixelCount)
        {
            int rMin = 31, rMax = 0, gMin = 31, gMax = 0, bMin = 31, bMax = 0;
            pixelCount = 0;
            for (int i = s; i < e; i++)
            {
                int k = bins[i].Key;
                int r = (k >> 10) & 31, g = (k >> 5) & 31, b = k & 31;
                if (r < rMin) rMin = r; if (r > rMax) rMax = r;
                if (g < gMin) gMin = g; if (g > gMax) gMax = g;
                if (b < bMin) bMin = b; if (b > bMax) bMax = b;
                pixelCount += bins[i].Count;
            }
            int rr = rMax - rMin, gr = gMax - gMin, br = bMax - bMin;
            if (gr >= rr && gr >= br) { axis = 1; range = gr; }
            else if (rr >= br) { axis = 0; range = rr; }
            else { axis = 2; range = br; }
        }

        private static int Nearest(int[] pal, int count, int r, int g, int b)
        {
            int best = 0, bestDist = int.MaxValue;
            for (int i = 0; i < count; i++)
            {
                int dr = r - pal[i * 3], dg = g - pal[i * 3 + 1], db = b - pal[i * 3 + 2];
                int d = dr * dr * 3 + dg * dg * 4 + db * db * 2;
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best;
        }

        #endregion

        #region LZW

        private void WriteLzw(byte[] indices)
        {
            const int minCodeSize = 8;
            const int clearCode = 1 << minCodeSize;
            const int eoiCode = clearCode + 1;

            _out.WriteByte(minCodeSize);

            var block = new byte[256];
            int blockLen = 0;
            int bitBuffer = 0, bitCount = 0;
            int codeSize = minCodeSize + 1;
            int next = eoiCode + 1;
            var dict = new Dictionary<int, int>(4096);

            void FlushBlock()
            {
                if (blockLen == 0) return;
                _out.WriteByte((byte)blockLen);
                _out.Write(block, 0, blockLen);
                blockLen = 0;
            }

            void Emit(int code)
            {
                bitBuffer |= code << bitCount;
                bitCount += codeSize;
                while (bitCount >= 8)
                {
                    block[blockLen++] = (byte)(bitBuffer & 0xFF);
                    if (blockLen == 255) FlushBlock();
                    bitBuffer >>= 8;
                    bitCount -= 8;
                }
                if (next > (1 << codeSize) - 1 && codeSize < 12)
                    codeSize++;
            }

            Emit(clearCode);
            if (indices.Length == 0)
            {
                Emit(eoiCode);
            }
            else
            {
                int prefix = indices[0];
                for (int i = 1; i < indices.Length; i++)
                {
                    int k = indices[i];
                    int key = (prefix << 8) | k;
                    if (dict.TryGetValue(key, out int code))
                    {
                        prefix = code;
                        continue;
                    }

                    Emit(prefix);
                    if (next < 4096)
                    {
                        dict[key] = next++;
                    }
                    else
                    {
                        Emit(clearCode);
                        dict.Clear();
                        next = eoiCode + 1;
                        codeSize = minCodeSize + 1;
                    }
                    prefix = k;
                }
                Emit(prefix);
                Emit(eoiCode);
            }

            if (bitCount > 0)
            {
                block[blockLen++] = (byte)(bitBuffer & 0xFF);
                if (blockLen == 255) FlushBlock();
            }
            FlushBlock();
            _out.WriteByte(0); // Block-Terminator
        }

        #endregion

        private void WriteShort(int v)
        {
            _out.WriteByte((byte)(v & 0xFF));
            _out.WriteByte((byte)((v >> 8) & 0xFF));
        }

        private void WriteHeader(int w, int h)
        {
            _out.Write("GIF89a"u8);
            WriteShort(w);
            WriteShort(h);
            _out.WriteByte(0x70); // keine globale Palette, 8 bit Farbauflösung
            _out.WriteByte(0);
            _out.WriteByte(0);

            // NETSCAPE2.0 Erweiterung -> Endlosschleife
            _out.Write(new byte[] { 0x21, 0xFF, 0x0B });
            _out.Write("NETSCAPE2.0"u8);
            _out.Write(new byte[] { 0x03, 0x01, 0x00, 0x00, 0x00 });
        }

        private void WriteGraphicControl(int delayMs)
        {
            int delay = Math.Max(2, (int)Math.Round(delayMs / 10.0));
            _out.Write(new byte[]
            {
                0x21, 0xF9, 0x04, 0x04,
                (byte)(delay & 0xFF), (byte)(delay >> 8),
                0x00, 0x00
            });
        }

        public void Dispose()
        {
            _out.WriteByte(0x3B);
            _out.Flush();
            _out.Dispose();
        }
    }
}

using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections.Generic;

// Original vector-style artwork; uses only Windows System.Drawing.
// Rebuild: csc /r:System.Drawing.dll /out:build/GenerateIcon.exe tools/GenerateIcon.cs
//          build/GenerateIcon.exe assets build
internal static class GenerateIcon
{
    private static GraphicsPath Rounded(float x, float y, float w, float h, float r)
    {
        GraphicsPath path = new GraphicsPath(); float d = r * 2;
        path.AddArc(x, y, d, d, 180, 90); path.AddArc(x + w - d, y, d, d, 270, 90);
        path.AddArc(x + w - d, y + h - d, d, d, 0, 90); path.AddArc(x, y + h - d, d, d, 90, 90);
        path.CloseFigure(); return path;
    }
    private static Bitmap Render(int size)
    {
        using (Bitmap large = new Bitmap(size * 4, size * 4, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(large))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.Transparent);
                g.ScaleTransform(large.Width / 128F, large.Height / 128F);
                using (GraphicsPath plate = Rounded(5, 5, 118, 118, 29))
                using (Brush fill = new LinearGradientBrush(new Point(5, 5), new Point(123, 123), Color.FromArgb(33, 59, 83), Color.FromArgb(16, 29, 44)))
                using (Pen edge = new Pen(Color.FromArgb(52, 81, 104), 2)) { g.FillPath(fill, plate); g.DrawPath(edge, plate); }
                using (GraphicsPath stem = Rounded(58, 25, 12, 13, 6))
                using (Brush fill = new SolidBrush(Color.FromArgb(143, 228, 228))) g.FillPath(fill, stem);
                using (GraphicsPath bell = new GraphicsPath())
                using (Brush fill = new LinearGradientBrush(new Point(41, 32), new Point(85, 94), Color.FromArgb(186, 247, 240), Color.FromArgb(71, 201, 215)))
                {
                    bell.AddBezier(64, 32, 49, 32, 40, 43, 40, 58); bell.AddLine(40, 58, 40, 70);
                    bell.AddBezier(40, 70, 40, 77, 37, 81, 33, 86); bell.AddBezier(33, 86, 32, 89, 33, 89, 36, 89);
                    bell.AddLine(36, 89, 92, 89); bell.AddBezier(92, 89, 95, 89, 96, 89, 95, 86);
                    bell.AddBezier(95, 86, 91, 81, 88, 77, 88, 70); bell.AddLine(88, 70, 88, 58);
                    bell.AddBezier(88, 58, 88, 43, 79, 32, 64, 32); bell.CloseFigure(); g.FillPath(fill, bell);
                }
                using (GraphicsPath clapper = new GraphicsPath())
                using (Brush fill = new SolidBrush(Color.FromArgb(143, 228, 228)))
                {
                    clapper.AddBezier(56, 96, 57, 102, 61, 105, 64, 105);
                    clapper.AddBezier(64, 105, 67, 105, 71, 102, 72, 96); clapper.CloseFigure(); g.FillPath(fill, clapper);
                }
                using (Brush outline = new SolidBrush(Color.FromArgb(21, 40, 58))) g.FillEllipse(outline, 82, 19, 26, 26);
                using (Brush fill = new SolidBrush(Color.FromArgb(246, 202, 114))) g.FillEllipse(fill, 86, 23, 18, 18);
                if (size >= 24) using (Brush shine = new SolidBrush(Color.FromArgb(255, 233, 188))) g.FillEllipse(shine, 90.1F, 27.1F, 4.8F, 4.8F);
            }
            Bitmap result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.CompositingMode = CompositingMode.SourceCopy; g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.DrawImage(large, new Rectangle(0, 0, size, size));
            }
            return result;
        }
    }
    private static byte[] Dib(Bitmap bitmap)
    {
        int n = bitmap.Width, maskStride = ((n + 31) / 32) * 4;
        using (MemoryStream stream = new MemoryStream()) using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write(40); writer.Write(n); writer.Write(n * 2); writer.Write((short)1); writer.Write((short)32);
            writer.Write(0); writer.Write(n * n * 4 + maskStride * n); writer.Write(0); writer.Write(0); writer.Write(0); writer.Write(0);
            for (int y = n - 1; y >= 0; y--) for (int x = 0; x < n; x++)
            { Color c = bitmap.GetPixel(x, y); writer.Write(c.B); writer.Write(c.G); writer.Write(c.R); writer.Write(c.A); }
            for (int y = n - 1; y >= 0; y--)
            {
                byte[] mask = new byte[maskStride];
                for (int x = 0; x < n; x++) if (bitmap.GetPixel(x, y).A < 128) mask[x / 8] |= (byte)(128 >> (x % 8));
                writer.Write(mask);
            }
            return stream.ToArray();
        }
    }
    private static void Main(string[] args)
    {
        string assets = Path.GetFullPath(args[0]), build = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(assets); Directory.CreateDirectory(build);
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 }; List<byte[]> frames = new List<byte[]>();
        foreach (int size in sizes) using (Bitmap frame = Render(size)) frames.Add(Dib(frame));
        using (BinaryWriter writer = new BinaryWriter(File.Create(Path.Combine(assets, "watchdog.ico"))))
        {
            writer.Write((short)0); writer.Write((short)1); writer.Write((short)sizes.Length); int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                writer.Write((byte)0); writer.Write((byte)0); writer.Write((short)1); writer.Write((short)32);
                writer.Write(frames[i].Length); writer.Write(offset); offset += frames[i].Length;
            }
            foreach (byte[] frame in frames) writer.Write(frame);
        }
        using (Bitmap preview = Render(96)) preview.Save(Path.Combine(assets, "watchdog-preview.png"), ImageFormat.Png);
        using (Bitmap sheet = new Bitmap(240, 120))
        using (Graphics g = Graphics.FromImage(sheet))
        {
            g.Clear(Color.FromArgb(242, 245, 249)); using (Brush dark = new SolidBrush(Color.FromArgb(29, 36, 46))) g.FillRectangle(dark, 0, 60, 240, 60);
            int x = 8; foreach (int size in new[] { 16, 24, 32, 48 })
            { using (Bitmap icon = Render(size)) { g.DrawImageUnscaled(icon, x, (60 - size) / 2); g.DrawImageUnscaled(icon, x, 60 + (60 - size) / 2); } x += size + 16; }
            sheet.Save(Path.Combine(build, "icon-size-check.png"), ImageFormat.Png);
        }
        Console.WriteLine("Created original 9-size 32-bit transparent ICO and previews.");
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// An optional animated image the user adds as mascot.gif in the app's data folder. None is
// included with the app. Gray and black pixels are treated as background and removed.
class Mascot : IDisposable
{
    const long MaxFileBytes = 5 * 1024 * 1024;
    const int MinimumColorSpread = 40;
    const int DefaultDelayMs = 100;
    const int MinimumDelayMs = 40;
    const int FrameDelayProperty = 0x5100;

    readonly List<Bitmap> frames = new List<Bitmap>();
    readonly List<int> delays = new List<int>();
    int index;

    public int CurrentDelayMs { get { return delays[index]; } }
    public Bitmap CurrentFrame { get { return frames[index]; } }

    public void Advance()
    {
        index = (index + 1) % frames.Count;
    }

    public static Mascot Load(int height)
    {
        string path = Path.Combine(App.DataDir, "mascot.gif");
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > MaxFileBytes) return null;
            using (Image image = Image.FromFile(path))
            {
                if (!image.RawFormat.Equals(ImageFormat.Gif)) return null;
                Mascot mascot = new Mascot();
                mascot.ReadFrames(image, height);
                return mascot.frames.Count > 0 ? mascot : null;
            }
        }
        catch (Exception)
        {
            return null; // A bad file only means no mascot.
        }
    }

    void ReadFrames(Image image, int height)
    {
        FrameDimension dimension = new FrameDimension(image.FrameDimensionsList[0]);
        int count = image.GetFrameCount(dimension);
        byte[] delayData = image.GetPropertyItem(FrameDelayProperty).Value;

        List<Bitmap> keyed = new List<Bitmap>();
        Rectangle bounds = Rectangle.Empty;
        for (int i = 0; i < count; i++)
        {
            image.SelectActiveFrame(dimension, i);
            Bitmap frame = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(frame)) g.DrawImage(image, 0, 0, image.Width, image.Height);
            Rectangle visible = RemoveBackground(frame);
            bounds = bounds.IsEmpty ? visible : Rectangle.Union(bounds, visible);
            keyed.Add(frame);

            int delay = BitConverter.ToInt32(delayData, i * 4) * 10;
            delays.Add(delay <= 0 ? DefaultDelayMs : Math.Max(MinimumDelayMs, delay));
        }

        if (bounds.IsEmpty) return;
        int width = Math.Max(1, bounds.Width * height / bounds.Height);
        foreach (Bitmap frame in keyed)
        {
            frames.Add(Scale(frame, bounds, width, height));
            frame.Dispose();
        }
    }

    static Rectangle RemoveBackground(Bitmap frame)
    {
        int left = frame.Width, top = frame.Height, right = -1, bottom = -1;
        for (int y = 0; y < frame.Height; y++)
        {
            for (int x = 0; x < frame.Width; x++)
            {
                Color pixel = frame.GetPixel(x, y);
                int spread = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B)) - Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
                if (spread < MinimumColorSpread)
                {
                    frame.SetPixel(x, y, Color.Transparent);
                    continue;
                }
                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }
        return right < 0 ? Rectangle.Empty : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }

    static Bitmap Scale(Bitmap source, Rectangle area, int width, int height)
    {
        Bitmap result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(result))
        {
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.DrawImage(source, new Rectangle(0, 0, width, height), area, GraphicsUnit.Pixel);
        }
        return result;
    }

    public void Dispose()
    {
        foreach (Bitmap frame in frames) frame.Dispose();
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

// The Classic character, built into the app as a GIF with a transparent background.
class Mascot : IMascot
{
    const string ResourceName = "mascot.default.gif";
    const int DefaultDelayMs = 100;
    const int MinimumDelayMs = 40;
    const int FrameDelayProperty = 0x5100;
    const int EdgePasses = 2; // The dark outline is up to two pixels wide.
    const int BodyRed = 150; // Darker than this is outline, not the character's orange.
    const int LaptopGray = 80;
    const int NeutralRange = 16;

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
        try
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName))
            using (Image image = Image.FromStream(stream))
            {
                Mascot mascot = new Mascot();
                mascot.ReadFrames(image, height);
                return mascot;
            }
        }
        catch (Exception)
        {
            return null; // The panel works the same without it.
        }
    }

    void ReadFrames(Image image, int height)
    {
        FrameDimension dimension = new FrameDimension(image.FrameDimensionsList[0]);
        int count = image.GetFrameCount(dimension);
        byte[] delayData = image.GetPropertyItem(FrameDelayProperty).Value;
        int width = Math.Max(1, image.Width * height / image.Height);
        Rectangle area = new Rectangle(0, 0, image.Width, image.Height);

        for (int i = 0; i < count; i++)
        {
            image.SelectActiveFrame(dimension, i);
            using (Bitmap frame = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(frame)) g.DrawImage(image, area);
                RemoveDarkEdge(frame);
                frames.Add(Scale(frame, area, width, height));
            }
            int delay = BitConverter.ToInt32(delayData, i * 4) * 10;
            delays.Add(delay <= 0 ? DefaultDelayMs : Math.Max(MinimumDelayMs, delay));
        }
    }

    // The GIF was cut from a dark background, which leaves an outline that shows on the light panel.
    static void RemoveDarkEdge(Bitmap frame)
    {
        int width = frame.Width;
        int height = frame.Height;
        Rectangle area = new Rectangle(0, 0, width, height);
        BitmapData data = frame.LockBits(area, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        int[] pixels = new int[width * height];
        Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);

        for (int pass = 0; pass < EdgePasses; pass++)
        {
            int[] next = (int[])pixels.Clone();
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (IsOutline(pixels[y * width + x]) && TouchesTransparent(pixels, x, y, width, height)) next[y * width + x] = 0;
            pixels = next;
        }

        Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
        frame.UnlockBits(data);
    }

    static bool IsOutline(int pixel)
    {
        Color color = Color.FromArgb(pixel);
        bool laptop = Math.Abs(color.R - color.B) < NeutralRange && color.R >= LaptopGray;
        return color.A > 0 && color.R < BodyRed && !laptop;
    }

    static bool TouchesTransparent(int[] pixels, int x, int y, int width, int height)
    {
        return IsTransparent(pixels, x - 1, y, width, height) || IsTransparent(pixels, x + 1, y, width, height)
            || IsTransparent(pixels, x, y - 1, width, height) || IsTransparent(pixels, x, y + 1, width, height);
    }

    static bool IsTransparent(int[] pixels, int x, int y, int width, int height)
    {
        return x < 0 || y < 0 || x >= width || y >= height || (pixels[y * width + x] >> 24 & 0xFF) == 0;
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

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;

// The Classic character, built into the app as a GIF with a transparent background.
class Mascot : IMascot
{
    const string ResourceName = "mascot.default.gif";
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
                frames.Add(Scale(frame, area, width, height));
            }
            int delay = BitConverter.ToInt32(delayData, i * 4) * 10;
            delays.Add(delay <= 0 ? DefaultDelayMs : Math.Max(MinimumDelayMs, delay));
        }
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

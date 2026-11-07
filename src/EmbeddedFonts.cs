using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

// Inter is built into the app, so the panel looks the same on every PC.
static class EmbeddedFonts
{
    const string RegularFamily = "Usage Inter";
    const string SemiBoldFamily = "Usage Inter SemiBold";
    const string Fallback = "Segoe UI";

    static readonly PrivateFontCollection fonts = new PrivateFontCollection();
    static readonly bool loaded = Load("font.regular.ttf") & Load("font.semibold.ttf");

    public static Font New(float pixels, bool strong)
    {
        if (!loaded) return new Font(Fallback, pixels, strong ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        string name = strong ? SemiBoldFamily : RegularFamily;
        FontFamily family = Array.Find(fonts.Families, delegate(FontFamily f) { return f.Name == name; });
        return new Font(family, pixels, FontStyle.Regular, GraphicsUnit.Pixel);
    }

    // GDI+ and GDI keep separate font lists, and the panel's text is drawn through GDI.
    static bool Load(string resource)
    {
        try
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            {
                byte[] data = new byte[stream.Length];
                stream.Read(data, 0, data.Length);

                // Stays allocated: both font lists read from it for as long as the app runs.
                IntPtr memory = Marshal.AllocCoTaskMem(data.Length);
                Marshal.Copy(data, 0, memory, data.Length);
                fonts.AddMemoryFont(memory, data.Length);
                uint installed;
                return Native.AddFontMemResourceEx(memory, (uint)data.Length, IntPtr.Zero, out installed) != IntPtr.Zero;
            }
        }
        catch (Exception)
        {
            return false;
        }
    }
}

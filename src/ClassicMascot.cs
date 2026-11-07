using System.Drawing;

// The original animation while working, and the pixel character for every other mood.
class ClassicMascot : IReactiveMascot
{
    readonly Mascot working;
    readonly PixelMascot other;

    public ClassicMascot(Mascot working, PixelMascot other)
    {
        this.working = working;
        this.other = other;
    }

    IMascot Active { get { return other.Mood == MascotMood.Work ? (IMascot)working : other; } }

    public MascotMood Mood
    {
        get { return other.Mood; }
        set { other.Mood = value; }
    }

    public Bitmap CurrentFrame { get { return Active.CurrentFrame; } }
    public int CurrentDelayMs { get { return Active.CurrentDelayMs; } }

    public void Advance()
    {
        Active.Advance();
    }

    public void Dispose()
    {
        working.Dispose();
        other.Dispose();
    }
}

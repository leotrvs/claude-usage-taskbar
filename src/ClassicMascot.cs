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

    public PixelMascot Pixel { get { return other; } }

    IMascot Active { get { return other.Mood == MascotMood.Work && !other.IsPlaying ? (IMascot)working : other; } }

    public MascotMood Mood
    {
        get { return other.Mood; }
        set { other.Mood = value; }
    }

    public double WalkOffset { get { return other.WalkOffset; } }

    public int UsagePercent
    {
        set { other.UsagePercent = value; }
    }

    public int? WalkGoal
    {
        set { other.WalkGoal = value; }
    }

    public int WalkRoom
    {
        set { other.WalkRoom = value; }
    }

    public void Play(MascotAction action)
    {
        other.Play(action);
    }

    public Bitmap CurrentFrame { get { return Active.CurrentFrame; } }
    public int CurrentDelayMs { get { return Active.CurrentDelayMs; } }

    public void Advance(int ms)
    {
        Active.Advance(ms);
        if (Active != other && other.IsPlaying) other.Advance(ms);
    }

    public void Advance()
    {
        Active.Advance();
        if (Active != other && other.IsPlaying) other.Advance();
    }

    public void Dispose()
    {
        working.Dispose();
        other.Dispose();
    }
}

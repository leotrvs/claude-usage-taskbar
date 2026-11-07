using System;
using System.Drawing;
using System.Drawing.Imaging;

enum MascotStyle { Pixel, Santa, Classic }
enum MascotMood { Sleep, Idle, Work, Tired, Down }

interface IMascot : IDisposable
{
    Bitmap CurrentFrame { get; }
    int CurrentDelayMs { get; }
    void Advance();
}

interface IReactiveMascot : IMascot
{
    MascotMood Mood { get; set; }
}

// The panel character drawn from code, so it can react to usage and wear an outfit.
class PixelMascot : IReactiveMascot
{
    const int Columns = 32;
    const int Rows = 22;
    const int BodyLeft = 4;
    const int BodyTop = 6;
    const int WorkingMinutes = 10;
    const int RestingMinutes = 45;
    const int TiredPercent = 90;
    const int FullPercent = 100;

    static readonly Color Body = Color.FromArgb(208, 106, 75);
    static readonly Color Eye = Color.FromArgb(20, 20, 22);
    static readonly Color Lid = Color.FromArgb(126, 126, 126);
    static readonly Color LidMark = Color.FromArgb(190, 190, 190);
    static readonly Color Base = Color.FromArgb(95, 95, 95);
    static readonly Color Snore = Color.FromArgb(160, 160, 160);
    static readonly Color Sweat = Color.FromArgb(96, 170, 245);
    static readonly Color HatRed = Color.FromArgb(214, 48, 49);
    static readonly Color HatWhite = Color.FromArgb(240, 240, 240);

    // Two base pixels per character, the same block size as the body.
    static readonly string[] SantaHat =
    {
        ".......ww.",
        "...rrrrww.",
        "..rrrrrr..",
        "wwwwwwwwww"
    };
    static readonly string[] SnoreLetter = { "####", "..#.", ".#..", "####" };

    readonly MascotStyle style;
    readonly int pixel;
    Bitmap frame;
    MascotMood mood = MascotMood.Idle;
    int tick;

    public PixelMascot(MascotStyle style, int pixel)
    {
        this.style = style;
        this.pixel = pixel;
        Redraw();
    }

    public Bitmap CurrentFrame { get { return frame; } }

    public int CurrentDelayMs
    {
        get
        {
            switch (mood)
            {
                case MascotMood.Sleep: return 420;
                case MascotMood.Tired: return 110;
                case MascotMood.Work: return 170;
                default: return 200;
            }
        }
    }

    public MascotMood Mood
    {
        get { return mood; }
        set
        {
            if (value == mood) return;
            mood = value;
            tick = 0;
            Redraw();
        }
    }

    public void Advance()
    {
        tick++;
        Redraw();
    }

    void Redraw()
    {
        Bitmap next = Render(style, mood, tick, pixel);
        if (frame != null) frame.Dispose();
        frame = next;
    }

    // Open sessions say what is happening on this PC. A rising percentage also catches use elsewhere, like claude.ai.
    public static MascotMood MoodFor(PanelState state, DateTime now)
    {
        if (state.Stale) return MascotMood.Sleep;
        int highest = Math.Max(state.Session.Percent ?? 0, state.Weekly.Percent ?? 0);
        if (highest >= FullPercent) return MascotMood.Down;

        double minutes = double.MaxValue;
        for (int i = state.History.Count - 1; i > 0; i--)
        {
            if (state.History[i].Session <= state.History[i - 1].Session) continue;
            minutes = (now - state.History[i].Time).TotalMinutes;
            break;
        }

        if (state.Sessions.Busy > 0 || minutes <= WorkingMinutes) return highest >= TiredPercent ? MascotMood.Tired : MascotMood.Work;
        return state.Sessions.Running > 0 || minutes <= RestingMinutes ? MascotMood.Idle : MascotMood.Sleep;
    }

    public static Bitmap Render(MascotStyle style, MascotMood mood, int tick, int pixel)
    {
        Bitmap bitmap = new Bitmap(Columns * pixel, Rows * pixel, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            Canvas canvas = new Canvas(g, pixel);
            switch (mood)
            {
                case MascotMood.Sleep: DrawSleep(canvas, style, tick); break;
                case MascotMood.Work: DrawWork(canvas, style, tick, false); break;
                case MascotMood.Tired: DrawWork(canvas, style, tick, true); break;
                case MascotMood.Down: DrawDown(canvas, tick); break;
                default: DrawIdle(canvas, style, tick); break;
            }
        }
        return bitmap;
    }

    static void DrawIdle(Canvas canvas, MascotStyle style, int tick)
    {
        int crouch = tick % 64 == 40 ? 2 : 0;
        int wave = tick % 96 >= 60 && tick % 96 < 68 && tick % 2 == 0 ? -2 : 0;
        int look = tick % 48 >= 8 && tick % 48 < 13 ? -1 : tick % 48 >= 30 && tick % 48 < 35 ? 1 : 0;
        DrawFigure(canvas, BodyTop + crouch, 0, wave);
        if (tick % 24 == 0) DrawClosedEyes(canvas, BodyTop + crouch);
        else DrawOpenEyes(canvas, BodyTop + crouch, look, 0, 4);
        DrawHat(canvas, style, BodyTop + crouch);
    }

    static void DrawWork(Canvas canvas, MascotStyle style, int tick, bool tired)
    {
        bool leftDown = tick % 2 == 0;
        DrawFigure(canvas, BodyTop, leftDown ? 0 : -1, leftDown ? -1 : 0);
        if (tick % 23 == 0) DrawClosedEyes(canvas, BodyTop);
        else if (tired) DrawOpenEyes(canvas, BodyTop, 0, 2, 2);
        else DrawOpenEyes(canvas, BodyTop, 0, 1, 4);

        canvas.Fill(BodyLeft + 6, BodyTop + 8, 12, 7, Lid);
        canvas.Fill(BodyLeft + 11, BodyTop + 10, 2, 2, LidMark);
        canvas.Fill(BodyLeft + 3, BodyTop + 15, 18, 1, Base);
        DrawHat(canvas, style, BodyTop);
        if (tired) canvas.Fill(BodyLeft + 23, BodyTop + tick % 8, 2, 3, Sweat);
    }

    static void DrawSleep(Canvas canvas, MascotStyle style, int tick)
    {
        int top = BodyTop + 4;
        DrawFigure(canvas, top, 0, 0);
        DrawClosedEyes(canvas, top);
        DrawHat(canvas, style, top);
        for (int letter = 0; letter < 2; letter++)
        {
            int phase = (tick + letter * 3) % 6;
            canvas.Art(SnoreLetter, 1, 25 + phase, 9 - phase * 2 + letter * 2, Snore);
        }
    }

    // On its back with its legs in the air; the outfit stays off because a hat would float below it.
    static void DrawDown(Canvas canvas, int tick)
    {
        canvas.UpsideDownAround = BodyTop * 2 + 16;
        DrawFigure(canvas, BodyTop, 0, 0, tick % 8 < 2 ? 1 : 0);
        DrawCrossEye(canvas, BodyLeft + 6, BodyTop + 2);
        DrawCrossEye(canvas, BodyLeft + 15, BodyTop + 2);
    }

    static void DrawFigure(Canvas canvas, int top, int leftArm, int rightArm)
    {
        DrawFigure(canvas, top, leftArm, rightArm, 0);
    }

    static void DrawFigure(Canvas canvas, int top, int leftArm, int rightArm, int legShift)
    {
        int left = BodyLeft;
        canvas.Fill(left + 4, top, 16, 12, Body);
        canvas.Fill(left, top + 6 + leftArm, 4, 4, Body);
        canvas.Fill(left + 20, top + 6 + rightArm, 4, 4, Body);
        foreach (int leg in new[] { 4, 8, 14, 18 })
            canvas.Fill(left + leg, top + 12, 2, 4 - legShift, Body);
    }

    static void DrawOpenEyes(Canvas canvas, int top, int look, int offset, int height)
    {
        canvas.Fill(BodyLeft + 6 + look, top + 2 + offset, 2, height, Eye);
        canvas.Fill(BodyLeft + 16 + look, top + 2 + offset, 2, height, Eye);
    }

    static void DrawClosedEyes(Canvas canvas, int top)
    {
        canvas.Fill(BodyLeft + 5, top + 5, 4, 1, Eye);
        canvas.Fill(BodyLeft + 15, top + 5, 4, 1, Eye);
    }

    static void DrawCrossEye(Canvas canvas, int x, int y)
    {
        canvas.Art(new[] { "#.#", ".#.", "#.#" }, 1, x, y, Eye);
    }

    static void DrawHat(Canvas canvas, MascotStyle style, int top)
    {
        if (style != MascotStyle.Santa) return;
        for (int row = 0; row < SantaHat.Length; row++)
        {
            string line = SantaHat[row];
            for (int column = 0; column < line.Length; column++)
            {
                if (line[column] == '.') continue;
                canvas.Fill(BodyLeft + 2 + column * 2, top - 6 + row * 2, 2, 2, line[column] == 'r' ? HatRed : HatWhite);
            }
        }
    }

    class Canvas
    {
        readonly Graphics graphics;
        readonly int pixel;
        public int? UpsideDownAround;

        public Canvas(Graphics graphics, int pixel)
        {
            this.graphics = graphics;
            this.pixel = pixel;
        }

        public void Fill(int x, int y, int width, int height, Color color)
        {
            if (UpsideDownAround.HasValue) y = UpsideDownAround.Value - y - height;
            using (Brush brush = new SolidBrush(color))
                graphics.FillRectangle(brush, x * pixel, y * pixel, width * pixel, height * pixel);
        }

        public void Art(string[] rows, int size, int x, int y, Color color)
        {
            for (int row = 0; row < rows.Length; row++)
                for (int column = 0; column < rows[row].Length; column++)
                    if (rows[row][column] != '.') Fill(x + column * size, y + row * size, size, size, color);
        }
    }

    public void Dispose()
    {
        frame.Dispose();
    }
}

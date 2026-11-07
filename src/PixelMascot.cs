using System;
using System.Drawing;
using System.Drawing.Imaging;

enum MascotStyle { Pixel, Santa, Classic, Headphones, Chef }
enum MascotMood { Sleep, Idle, Work, Tired, Down, Waiting, Panic, Coffee, Juggle, Night }
enum MascotAction { Celebrate, Wave, Pet }

interface IMascot : IDisposable
{
    Bitmap CurrentFrame { get; }
    int CurrentDelayMs { get; }
    void Advance();
}

interface IReactiveMascot : IMascot
{
    MascotMood Mood { get; set; }
    void Play(MascotAction action);
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
    const int PanicMinutes = 30;
    const int CoffeeMinutes = 120;
    const int JuggleSessions = 3;
    const int LastNightHour = 5;
    const int CelebrateFrames = 26;
    const int WaveFrames = 14;
    const int PetFrames = 14;
    const int ActionDelayMs = 110;
    const int OpenLidHeight = 7;
    const int ClosedLidHeight = 1;
    const int LaptopCycle = 70; // Frames of typing and of a closed lid, so the laptop does not hide the face all the time.
    const int LidClosesAt = 46;
    const int LidClosedAt = 52;
    const int LidOpensAt = 64;

    static readonly Color Body = Color.FromArgb(208, 106, 75);
    static readonly Color Eye = Color.FromArgb(20, 20, 22);
    static readonly Color Lid = Color.FromArgb(126, 126, 126);
    static readonly Color LidMark = Color.FromArgb(190, 190, 190);
    static readonly Color Base = Color.FromArgb(95, 95, 95);
    static readonly Color Snore = Color.FromArgb(160, 160, 160);
    static readonly Color Sweat = Color.FromArgb(96, 170, 245);
    static readonly Color Question = Color.FromArgb(250, 204, 21);
    static readonly Color Heart = Color.FromArgb(244, 94, 124);
    static readonly Color Mug = Color.FromArgb(230, 230, 230);
    static readonly Color[] Bright =
    {
        Color.FromArgb(72, 150, 245), Color.FromArgb(250, 204, 21),
        Color.FromArgb(52, 208, 106), Color.FromArgb(244, 94, 124)
    };

    class Outfit
    {
        public int Left; // In base pixels from the character's left edge.
        public string[] Art;
    }

    // Two base pixels per block, the same block size as the body. Row 3 lands on the top of the head.
    static readonly Outfit SantaHat = new Outfit
    {
        Left = 2,
        Art = new[]
        {
            ".......ww.",
            "...rrrrww.",
            "..rrrrrr..",
            "wwwwwwwwww"
        }
    };
    static readonly Outfit HeadphoneSet = new Outfit
    {
        Left = 0,
        Art = new[]
        {
            "............",
            "...gggggg...",
            "..g......g..",
            "..b......b..",
            "..b......b..",
            "..b......b.."
        }
    };
    static readonly Outfit ChefHat = new Outfit
    {
        Left = 0,
        Art = new[]
        {
            "...wwwwww...",
            "..wwwwwwww..",
            "...wwwwww...",
            "...gggggg..."
        }
    };
    static readonly Outfit NightCap = new Outfit
    {
        Left = 2,
        Art = new[]
        {
            "..........",
            "...iiiiw..",
            "..iiiiii..",
            "iiiiiiiiii"
        }
    };

    static readonly string[] SnoreLetter = { "####", "..#.", ".#..", "####" };
    static readonly string[] QuestionMark = { "###", "..#", ".##", "...", ".#." };
    static readonly string[] HeartShape = { "#.#", "###", ".#." };
    static readonly string[] HappyEye = { ".#.", "#.#" };

    readonly MascotStyle style;
    readonly int pixel;
    Bitmap frame;
    MascotMood mood = MascotMood.Idle;
    MascotAction? action;
    int actionTick;
    int tick;

    public PixelMascot(MascotStyle style, int pixel)
    {
        this.style = style;
        this.pixel = pixel;
        Redraw();
    }

    public Bitmap CurrentFrame { get { return frame; } }
    public bool IsPlaying { get { return action.HasValue; } }

    public int CurrentDelayMs
    {
        get
        {
            if (action.HasValue) return ActionDelayMs;
            switch (mood)
            {
                case MascotMood.Sleep:
                case MascotMood.Night: return 420;
                case MascotMood.Tired: return 110;
                case MascotMood.Panic: return 90;
                case MascotMood.Work:
                case MascotMood.Coffee:
                case MascotMood.Juggle: return 170;
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

    public void Play(MascotAction value)
    {
        action = value;
        actionTick = 0;
        Redraw();
    }

    public void Advance()
    {
        tick++;
        if (action.HasValue && ++actionTick >= FramesOf(action.Value)) action = null;
        Redraw();
    }

    static int FramesOf(MascotAction value)
    {
        return value == MascotAction.Celebrate ? CelebrateFrames : value == MascotAction.Wave ? WaveFrames : PetFrames;
    }

    void Redraw()
    {
        Bitmap next = Render(style, mood, tick, pixel, action, actionTick);
        if (frame != null) frame.Dispose();
        frame = next;
    }

    // Open sessions say what is happening on this PC. A rising percentage also catches use elsewhere, like claude.ai.
    public static MascotMood MoodFor(PanelState state, DateTime now)
    {
        if (state.Stale) return RestingMood(now);
        int highest = Math.Max(state.Session.Percent ?? 0, state.Weekly.Percent ?? 0);
        if (highest >= FullPercent) return MascotMood.Down;
        if (state.Sessions.Waiting > 0) return MascotMood.Waiting;
        if (state.MinutesToLimit.HasValue && state.MinutesToLimit.Value < PanicMinutes) return MascotMood.Panic;

        double minutes = double.MaxValue;
        for (int i = state.History.Count - 1; i > 0; i--)
        {
            if (state.History[i].Session <= state.History[i - 1].Session) continue;
            minutes = (now - state.History[i].Time).TotalMinutes;
            break;
        }

        if (state.Sessions.Busy >= JuggleSessions) return MascotMood.Juggle;
        if (state.Sessions.Busy > 0 || minutes <= WorkingMinutes)
        {
            if (highest >= TiredPercent) return MascotMood.Tired;
            return state.BusyMinutes >= CoffeeMinutes ? MascotMood.Coffee : MascotMood.Work;
        }
        return state.Sessions.Running > 0 || minutes <= RestingMinutes ? MascotMood.Idle : RestingMood(now);
    }

    static MascotMood RestingMood(DateTime now)
    {
        return now.Hour <= LastNightHour ? MascotMood.Night : MascotMood.Sleep;
    }

    public static Bitmap Render(MascotStyle style, MascotMood mood, int tick, int pixel)
    {
        return Render(style, mood, tick, pixel, null, 0);
    }

    public static Bitmap Render(MascotStyle style, MascotMood mood, int tick, int pixel, MascotAction? action, int actionTick)
    {
        Bitmap bitmap = new Bitmap(Columns * pixel, Rows * pixel, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            Canvas canvas = new Canvas(g, pixel);
            if (action.HasValue) DrawAction(canvas, style, action.Value, actionTick);
            else DrawMood(canvas, style, mood, tick);
        }
        return bitmap;
    }

    static void DrawMood(Canvas canvas, MascotStyle style, MascotMood mood, int tick)
    {
        switch (mood)
        {
            case MascotMood.Sleep: DrawSleep(canvas, style, tick, false); break;
            case MascotMood.Night: DrawSleep(canvas, style, tick, true); break;
            case MascotMood.Work: DrawWork(canvas, style, tick, false); break;
            case MascotMood.Tired: DrawWork(canvas, style, tick, true); break;
            case MascotMood.Down: DrawDown(canvas, tick); break;
            case MascotMood.Waiting: DrawWaiting(canvas, style, tick); break;
            case MascotMood.Panic: DrawPanic(canvas, style, tick); break;
            case MascotMood.Coffee: DrawCoffee(canvas, style, tick); break;
            case MascotMood.Juggle: DrawJuggle(canvas, style, tick); break;
            default: DrawIdle(canvas, style, tick); break;
        }
    }

    static void DrawAction(Canvas canvas, MascotStyle style, MascotAction action, int tick)
    {
        switch (action)
        {
            case MascotAction.Celebrate: DrawCelebrate(canvas, style, tick); break;
            case MascotAction.Wave: DrawWave(canvas, style, tick); break;
            default: DrawPet(canvas, style, tick); break;
        }
    }

    static void DrawIdle(Canvas canvas, MascotStyle style, int tick)
    {
        int crouch = tick % 64 == 40 ? 2 : 0;
        int wave = tick % 96 >= 60 && tick % 96 < 68 && tick % 2 == 0 ? -2 : 0;
        int look = tick % 48 >= 8 && tick % 48 < 13 ? -1 : tick % 48 >= 30 && tick % 48 < 35 ? 1 : 0;
        DrawFigure(canvas, BodyTop + crouch, 0, wave);
        if (tick % 24 == 0) DrawClosedEyes(canvas, BodyTop + crouch);
        else DrawOpenEyes(canvas, BodyTop + crouch, look, 0, 4);
        DrawOutfit(canvas, style, BodyTop + crouch);
    }

    static void DrawWork(Canvas canvas, MascotStyle style, int tick, bool tired)
    {
        DrawTyping(canvas, tick, tired);
        DrawLaptop(canvas, LidHeight(tick));
        DrawOutfit(canvas, style, BodyTop);
        if (tired) canvas.Fill(BodyLeft + 23, BodyTop + tick % 8, 2, 3, Sweat);
    }

    static int LidHeight(int tick)
    {
        int step = tick % LaptopCycle;
        if (step < LidClosesAt) return OpenLidHeight;
        if (step < LidClosedAt) return OpenLidHeight - (step - LidClosesAt);
        return step < LidOpensAt ? ClosedLidHeight : ClosedLidHeight + (step - LidOpensAt);
    }

    static void DrawTyping(Canvas canvas, int tick, bool tired)
    {
        bool typing = LidHeight(tick) == OpenLidHeight;
        bool leftDown = tick % 2 == 0;
        DrawFigure(canvas, BodyTop, typing ? (leftDown ? 0 : -1) : 0, typing ? (leftDown ? -1 : 0) : 0);
        if (!typing && LidHeight(tick) == ClosedLidHeight) DrawOpenEyes(canvas, BodyTop, tick % 12 < 6 ? 1 : -1, 0, tired ? 3 : 4);
        else if (tick % 23 == 0) DrawClosedEyes(canvas, BodyTop);
        else if (tired) DrawOpenEyes(canvas, BodyTop, 0, 2, 2);
        else DrawOpenEyes(canvas, BodyTop, 0, 1, 4);
    }

    static void DrawLaptop(Canvas canvas, int lidHeight)
    {
        canvas.Fill(BodyLeft + 6, BodyTop + 15 - lidHeight, 12, lidHeight, Lid);
        if (lidHeight == OpenLidHeight) canvas.Fill(BodyLeft + 11, BodyTop + 10, 2, 2, LidMark);
        canvas.Fill(BodyLeft + 3, BodyTop + 15, 18, 1, Base);
    }

    static void DrawSleep(Canvas canvas, MascotStyle style, int tick, bool night)
    {
        int top = BodyTop + 4;
        DrawFigure(canvas, top, 0, 0);
        DrawClosedEyes(canvas, top);
        if (night && OutfitOf(style) == null) DrawArt(canvas, NightCap, top);
        else DrawOutfit(canvas, style, top);
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

    static void DrawWaiting(Canvas canvas, MascotStyle style, int tick)
    {
        int crouch = tick % 8 == 0 ? 2 : 0;
        DrawFigure(canvas, BodyTop + crouch, 0, -2);
        DrawOpenEyes(canvas, BodyTop + crouch, 0, 0, 4);
        DrawOutfit(canvas, style, BodyTop + crouch);
        canvas.Art(QuestionMark, 1, 27, 1 + (tick % 4 < 2 ? 0 : 1), Question);
    }

    static void DrawPanic(Canvas canvas, MascotStyle style, int tick)
    {
        canvas.ShiftX = tick % 2 == 0 ? -1 : 1;
        bool up = tick % 4 < 2;
        DrawFigure(canvas, BodyTop, up ? -4 : -2, up ? -2 : -4);
        DrawOpenEyes(canvas, BodyTop, 0, 0, 5);
        DrawOutfit(canvas, style, BodyTop);
        canvas.ShiftX = 0;
        canvas.Fill(1, BodyTop + (tick * 2) % 10, 2, 3, Sweat);
        canvas.Fill(29, BodyTop + (tick * 2 + 5) % 10, 2, 3, Sweat);
    }

    static void DrawCoffee(Canvas canvas, MascotStyle style, int tick)
    {
        bool sipping = tick % 26 >= 18;
        if (sipping)
        {
            DrawFigure(canvas, BodyTop, 0, -3);
            DrawClosedEyes(canvas, BodyTop);
        }
        else
        {
            DrawTyping(canvas, tick, false);
        }
        DrawLaptop(canvas, sipping ? ClosedLidHeight : LidHeight(tick));
        DrawOutfit(canvas, style, BodyTop);

        int mugX = BodyLeft + (sipping ? 21 : 24);
        int mugY = BodyTop + (sipping ? 2 : 11);
        canvas.Fill(mugX, mugY, 3, 4, Mug);
        canvas.Fill(mugX + 3, mugY + 1, 1, 2, Mug);
        if (tick % 4 < 2) canvas.Fill(mugX + 1, mugY - 2, 1, 1, Snore);
    }

    static void DrawJuggle(Canvas canvas, MascotStyle style, int tick)
    {
        const int Path = 10;
        bool leftUp = tick % 2 == 0;
        DrawFigure(canvas, BodyTop, leftUp ? -2 : 0, leftUp ? 0 : -2);
        DrawOpenEyes(canvas, BodyTop, 0, 0, 3);
        DrawOutfit(canvas, style, BodyTop);
        for (int ball = 0; ball < 3; ball++)
        {
            int step = (tick + ball * 3) % Path;
            canvas.Fill(BodyLeft + 1 + step * 2, Math.Abs(step - Path / 2) - 1, 2, 2, Bright[ball]);
        }
    }

    static void DrawCelebrate(Canvas canvas, MascotStyle style, int tick)
    {
        int top = BodyTop + (tick % 6 < 3 ? 0 : 2);
        DrawFigure(canvas, top, -3, -3);
        DrawHappyEyes(canvas, top);
        DrawOutfit(canvas, style, top);
        for (int piece = 0; piece < 12; piece++)
            canvas.Fill((piece * 11 + 3) % Columns, (tick + piece * 5) % Rows, 2, 1, Bright[piece % Bright.Length]);
    }

    static void DrawWave(Canvas canvas, MascotStyle style, int tick)
    {
        DrawFigure(canvas, BodyTop, 0, tick % 4 < 2 ? -4 : -2);
        DrawOpenEyes(canvas, BodyTop, 1, 0, 4);
        DrawOutfit(canvas, style, BodyTop);
    }

    static void DrawPet(Canvas canvas, MascotStyle style, int tick)
    {
        int top = BodyTop + (tick % 4 < 2 ? 0 : 2);
        DrawFigure(canvas, top, -1, -1);
        DrawHappyEyes(canvas, top);
        DrawOutfit(canvas, style, top);
        canvas.Art(HeartShape, 1, 26, 7 - tick / 3, Heart);
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

    static void DrawHappyEyes(Canvas canvas, int top)
    {
        canvas.Art(HappyEye, 1, BodyLeft + 5, top + 3, Eye);
        canvas.Art(HappyEye, 1, BodyLeft + 15, top + 3, Eye);
    }

    static void DrawCrossEye(Canvas canvas, int x, int y)
    {
        canvas.Art(new[] { "#.#", ".#.", "#.#" }, 1, x, y, Eye);
    }

    static Outfit OutfitOf(MascotStyle style)
    {
        switch (style)
        {
            case MascotStyle.Santa: return SantaHat;
            case MascotStyle.Headphones: return HeadphoneSet;
            case MascotStyle.Chef: return ChefHat;
            default: return null;
        }
    }

    static void DrawOutfit(Canvas canvas, MascotStyle style, int top)
    {
        Outfit outfit = OutfitOf(style);
        if (outfit != null) DrawArt(canvas, outfit, top);
    }

    static void DrawArt(Canvas canvas, Outfit outfit, int top)
    {
        for (int row = 0; row < outfit.Art.Length; row++)
        {
            string line = outfit.Art[row];
            for (int column = 0; column < line.Length; column++)
            {
                if (line[column] == '.') continue;
                canvas.Fill(BodyLeft + outfit.Left + column * 2, top - 6 + row * 2, 2, 2, OutfitColor(line[column]));
            }
        }
    }

    static Color OutfitColor(char letter)
    {
        switch (letter)
        {
            case 'r': return Color.FromArgb(214, 48, 49);
            case 'g': return Color.FromArgb(150, 150, 156);
            case 'b': return Color.FromArgb(72, 150, 245);
            case 'i': return Color.FromArgb(82, 104, 190);
            default: return Color.FromArgb(240, 240, 240);
        }
    }

    class Canvas
    {
        readonly Graphics graphics;
        readonly int pixel;
        public int? UpsideDownAround;
        public int ShiftX;

        public Canvas(Graphics graphics, int pixel)
        {
            this.graphics = graphics;
            this.pixel = pixel;
        }

        public void Fill(int x, int y, int width, int height, Color color)
        {
            if (UpsideDownAround.HasValue) y = UpsideDownAround.Value - y - height;
            using (Brush brush = new SolidBrush(color))
                graphics.FillRectangle(brush, (x + ShiftX) * pixel, y * pixel, width * pixel, height * pixel);
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

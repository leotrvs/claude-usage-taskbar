using System;
using System.Drawing;
using System.Drawing.Imaging;

enum MascotStyle { Pixel, Santa, Classic, Headphones, Chef, Scientist, Detective, Winter, Racer, Halloween, Carnival }
enum MascotMood { Sleep, Idle, Work, Tired, Down, Waiting, Panic, Coffee, Juggle, Night }
enum MascotAction { Celebrate, Wave, Pet, Love }

interface IMascot : IDisposable
{
    Bitmap CurrentFrame { get; }
    int CurrentDelayMs { get; }
    void Advance();
}

interface IReactiveMascot : IMascot
{
    MascotMood Mood { get; set; }
    int WalkOffset { get; } // Columns the character has moved left of its resting spot.
    int WalkRoom { set; } // Columns it may move left without covering the panel's text.
    int UsagePercent { set; } // The highest limit, so it can glance at the bars when close to it.
    int? WalkGoal { set; } // Where the cursor last was in the header, in columns left of the resting spot.
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
    const int LoveFrames = 44;
    const int GlanceFromPercent = 80;
    const int SweatPercent = 90;
    const int ActionDelayMs = 110;
    const int SnowFlakes = 10;
    const int SteamPuffs = 3;
    const int SteamHeight = 9;
    const int OpenLidHeight = 7;
    const int ClosedLidHeight = 1;
    const int LaptopCycle = 70; // Frames of typing and of a closed lid, so the laptop does not hide the face all the time.
    const int LidClosesAt = 46;
    const int LidClosedAt = 52;
    const int LidOpensAt = 64;
    const int MinWalkRoom = 12;
    const int MaxWalkColumns = 36;
    const int WalkTickMs = 100;
    const int WalkLookTicks = 18;
    const int PatrolStride = 9;
    const int PatrolStopTicks = 12;
    const int MinWalkWaitMs = 15000;
    const int MaxWalkWaitMs = 30000;
    const int MinPanicWaitMs = 25000;
    const int MaxPanicWaitMs = 55000;
    const int PanicSpeed = 2;
    const int PanicTickMs = 70;
    const int RaceSpeed = 3;
    const int FlagTicks = 14; // The checkered flag waves, then the car arrives and the driver gets in.
    const int BoardTicks = 5;
    const int ParkTicks = 6;
    const int BoomAt = 6; // Ticks into the experiment's pause when the flask goes off.

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
    static readonly Color Steam = Color.FromArgb(225, 225, 230);
    static readonly Color Snow = Color.FromArgb(235, 240, 248);
    static readonly Color[] Bright =
    {
        Color.FromArgb(72, 150, 245), Color.FromArgb(250, 204, 21),
        Color.FromArgb(52, 208, 106), Color.FromArgb(244, 94, 124)
    };

    class Outfit
    {
        public int Left; // In base pixels from the character's left edge.
        public int Block = 2; // Base pixels per letter. 2 matches the body; 1 allows finer detail.
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
    // Wired earbuds: the wires run from each ear and join on the chest.
    static readonly Outfit Earbuds = new Outfit
    {
        Left = 3,
        Block = 1,
        Art = new[]
        {
            "", "", "", "", "", "", "", "",
            "ww..............ww",
            "ww..............ww",
            "ww..............ww",
            ".w..............w.",
            "..w............w..",
            "...w..........w...",
            "....w........w....",
            ".....ww....ww.....",
            ".......w..w.......",
            "........ww........",
            "........w.........",
            "........w........."
        }
    };
    // A puffed top leaning left over a narrow band, drawn at finer detail for the pleats.
    static readonly Outfit ChefHat = new Outfit
    {
        Left = 2,
        Block = 1,
        Art = new[]
        {
            ".....wwwwwwwwww.....",
            "..wwwwswwwswwwwwww..",
            ".wwwwwswwwswwwwwwww.",
            "wwwwwwswwwswwwwwwww.",
            ".wwwwsswwwsswwwwww..",
            "...dddddddddddddd...",
            "...wwwwwwwwwwwwww...",
            "...ssssssssssssss..."
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

    enum WalkStep { None, Out, Look, Search, Back, Flag, Board, Park, Alight }
    enum WalkAct { Walk, Dance, Tray, Skate, Broom, Experiment }

    // What the idle character is doing while it walks around the header.
    class WalkPose
    {
        public bool Moving;
        public int Step;
        public int Look;
        public bool Searching;
        public bool Down; // Looking down at the usage bars.
        public bool Sweat;
        public WalkAct Act;
        public bool AtEnd; // At the far end of the walk, doing the act's finale.
        public bool Returning;
        public bool Sooty;
        public int Boom = -1; // Ticks since the experiment went off, or -1.
        public bool Racing; // The Racer drives instead of walking.
        public bool Flag;
        public bool Car;
        public bool Seated;
        public bool Mirror;
        public int Slide; // Columns the car is still to the right of its place.
    }

    static readonly Random Chance = new Random();

    readonly MascotStyle style;
    readonly int pixel;
    WalkStep walkStep;
    int walkTarget;
    int walkWait;
    int walkTick;
    int walkStops;
    bool walkPatrol;
    bool walkPanic;
    bool walkRace;
    WalkAct walkAct;
    bool walkGlance;
    bool walkCursor;
    int walkLegs;
    int nextWalk;
    Bitmap frame;
    MascotMood mood = MascotMood.Idle;
    MascotAction? action;
    int actionTick;
    int tick;

    public PixelMascot(MascotStyle style, int pixel)
    {
        this.style = style;
        this.pixel = pixel;
        ScheduleWalk();
        Redraw();
    }

    public int WalkOffset { get; private set; }
    public int WalkRoom { get; set; }
    public bool AlwaysSpecial { get; set; } // For the documentation images: every walk is the outfit's special one.
    public int UsagePercent { get; set; }
    public int? WalkGoal { get; set; }

    public Bitmap CurrentFrame { get { return frame; } }
    public bool IsPlaying { get { return action.HasValue; } }

    public int CurrentDelayMs
    {
        get
        {
            if (action.HasValue) return ActionDelayMs;
            if (walkStep != WalkStep.None) return walkPanic || walkRace ? PanicTickMs : WalkTickMs;
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
            EndWalk();
            Redraw();
        }
    }

    public void Play(MascotAction value)
    {
        if (action.HasValue && action.Value == MascotAction.Love && value != MascotAction.Love) return; // Let it finish melting.
        action = value;
        actionTick = 0;
        Redraw();
    }

    public void Advance()
    {
        tick++;
        if (action.HasValue && ++actionTick >= FramesOf(action.Value)) action = null;
        if (!action.HasValue) AdvanceWalk();
        Redraw();
    }

    // Walks only while idle and only now and then, so it stays a surprise.
    void AdvanceWalk()
    {
        if (walkStep == WalkStep.None)
        {
            if (!CanWalk || WalkRoom < MinWalkRoom || Environment.TickCount - nextWalk < 0) return;
            BeginWalk();
        }
        walkTick++;
        int speed = walkRace ? RaceSpeed : walkPanic ? PanicSpeed : (walkAct == WalkAct.Skate || walkAct == WalkAct.Broom ? 2 : 1);
        switch (walkStep)
        {
            case WalkStep.Flag:
                if (--walkWait <= 0) { walkStep = WalkStep.Board; walkWait = BoardTicks; }
                break;
            case WalkStep.Board:
                if (--walkWait <= 0) walkStep = WalkStep.Out;
                break;
            case WalkStep.Park:
                if (--walkWait <= 0) walkStep = WalkStep.Back;
                break;
            case WalkStep.Alight:
                if (--walkWait <= 0) EndWalk();
                break;
            case WalkStep.Out:
                WalkOffset = Math.Min(walkTarget, WalkOffset + speed);
                if (WalkOffset >= Math.Min(walkTarget, WalkRoom))
                {
                    if (walkRace) { walkStep = WalkStep.Park; walkWait = ParkTicks; }
                    else if (walkPanic) walkStep = WalkStep.Back;
                    else { walkStep = WalkStep.Look; walkWait = WalkLookTicks; }
                }
                else if (walkPatrol && WalkOffset % PatrolStride == 0 && WalkOffset / PatrolStride > walkStops)
                {
                    walkStops = WalkOffset / PatrolStride;
                    walkStep = WalkStep.Search;
                    walkWait = PatrolStopTicks;
                }
                break;
            case WalkStep.Search:
                if (--walkWait <= 0) walkStep = WalkStep.Out;
                break;
            case WalkStep.Look:
                if (--walkWait <= 0) walkStep = WalkStep.Back;
                break;
            case WalkStep.Back:
                WalkOffset = Math.Max(0, WalkOffset - speed);
                if (WalkOffset > 0) break;
                if (walkRace) { walkStep = WalkStep.Alight; walkWait = BoardTicks; }
                else if (walkPanic && --walkLegs > 0) walkStep = WalkStep.Out;
                else EndWalk();
                break;
        }
    }

    bool CanWalk { get { return mood == MascotMood.Idle || mood == MascotMood.Panic; } }

    // Starts a walk now. The app waits a few minutes between walks; the documentation images call this directly.
    public void BeginWalk()
    {
        int room = Math.Min(WalkRoom, MaxWalkColumns);
        if (room < MinWalkRoom) return;
        walkPanic = mood == MascotMood.Panic;
        walkLegs = Chance.Next(2, 4); // Panicking, it runs there and back a few times.
        walkPatrol = !walkPanic && style == MascotStyle.Detective && Chance.Next(2) == 0;
        bool peek = !walkPanic && !walkPatrol && Chance.Next(3) == 0;
        walkTarget = peek ? room / 2 : room;
        walkStops = 0;
        walkTick = 0;
        walkGlance = false;
        walkCursor = false;
        if (!walkPanic && !walkPatrol)
        {
            if (WalkGoal.HasValue && Chance.Next(2) == 0)
            {
                walkTarget = Math.Max(MinWalkRoom / 2, Math.Min(WalkGoal.Value, room));
                walkCursor = true;
            }
            else if (UsagePercent >= GlanceFromPercent && Chance.Next(2) == 0)
            {
                walkTarget = Math.Max(MinWalkRoom / 2, room / 2);
                walkGlance = true;
            }
            WalkGoal = null;
        }
        walkAct = WalkAct.Walk;
        if (!walkPanic && !walkPatrol && !walkGlance && !walkCursor && (AlwaysSpecial || Chance.Next(5) < 3))
        {
            switch (style)
            {
                case MascotStyle.Headphones:
                case MascotStyle.Carnival: walkAct = WalkAct.Dance; break;
                case MascotStyle.Chef: walkAct = WalkAct.Tray; break;
                case MascotStyle.Scientist: walkAct = WalkAct.Experiment; break;
                case MascotStyle.Winter: walkAct = WalkAct.Skate; break;
                case MascotStyle.Halloween: walkAct = WalkAct.Broom; break;
            }
        }
        // The Racer drives its car; Santa sometimes takes the sleigh.
        walkRace = !walkPanic && (style == MascotStyle.Racer || (style == MascotStyle.Santa && (AlwaysSpecial || Chance.Next(2) == 0)));
        if (walkRace)
        {
            walkGlance = false;
            walkCursor = false;
            walkAct = WalkAct.Walk;
            walkTarget = room;
            walkStep = style == MascotStyle.Racer ? WalkStep.Flag : WalkStep.Board;
            walkWait = style == MascotStyle.Racer ? FlagTicks : BoardTicks;
            return;
        }
        walkStep = WalkStep.Out;
    }

    void EndWalk()
    {
        walkStep = WalkStep.None;
        WalkOffset = 0;
        ScheduleWalk();
    }

    void ScheduleWalk()
    {
        bool panic = mood == MascotMood.Panic;
        nextWalk = Environment.TickCount + (panic ? Chance.Next(MinPanicWaitMs, MaxPanicWaitMs) : Chance.Next(MinWalkWaitMs, MaxWalkWaitMs));
    }

    WalkPose CurrentWalkPose()
    {
        if (walkStep == WalkStep.None || !CanWalk) return null;
        WalkPose pose = new WalkPose();
        pose.Moving = walkStep == WalkStep.Out || walkStep == WalkStep.Back;
        pose.Step = walkTick;
        pose.Searching = walkStep == WalkStep.Search;
        if (walkStep == WalkStep.Back) pose.Look = 1;
        else if (walkStep == WalkStep.Out && walkPanic) pose.Look = -1;
        else if (walkStep == WalkStep.Look) pose.Look = walkTick % WalkLookTicks < WalkLookTicks / 2 ? 1 : -1;
        else if (walkStep == WalkStep.Search) pose.Look = walkTick / 3 % 2 == 0 ? -1 : 0;
        else pose.Look = -1;
        pose.Down = walkStep == WalkStep.Look && walkGlance;
        pose.Sweat = pose.Down && UsagePercent >= SweatPercent;
        if (pose.Down) pose.Look = 0;
        else if (walkStep == WalkStep.Look && walkCursor) pose.Look = walkTick % 8 < 4 ? -1 : 1;
        pose.Act = walkAct;
        pose.AtEnd = walkStep == WalkStep.Look;
        pose.Returning = walkStep == WalkStep.Back;
        if (walkAct == WalkAct.Experiment)
        {
            int age = WalkLookTicks - walkWait;
            bool boomed = walkStep == WalkStep.Look && age >= BoomAt;
            pose.Boom = boomed ? age - BoomAt : -1;
            pose.Sooty = boomed || walkStep == WalkStep.Back;
        }
        if (walkRace)
        {
            pose.Racing = true;
            pose.Look = -1;
            pose.Flag = walkStep == WalkStep.Flag;
            pose.Car = walkStep != WalkStep.Flag;
            pose.Seated = walkStep == WalkStep.Out || walkStep == WalkStep.Park || walkStep == WalkStep.Back || (walkStep == WalkStep.Board && walkWait <= 2);
            pose.Mirror = walkStep == WalkStep.Back || walkStep == WalkStep.Alight;
            if (walkStep == WalkStep.Board) pose.Slide = walkWait * 4;
            else if (walkStep == WalkStep.Alight) pose.Slide = (BoardTicks - walkWait) * 4;
        }
        return pose;
    }

    static int FramesOf(MascotAction value)
    {
        return value == MascotAction.Celebrate ? CelebrateFrames : value == MascotAction.Wave ? WaveFrames
            : value == MascotAction.Love ? LoveFrames : PetFrames;
    }

    void Redraw()
    {
        Bitmap next = Render(style, mood, tick, pixel, action, actionTick, CurrentWalkPose());
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
        return Render(style, mood, tick, pixel, null, 0, null);
    }

    public static Bitmap Render(MascotStyle style, MascotMood mood, int tick, int pixel, MascotAction? action, int actionTick)
    {
        return Render(style, mood, tick, pixel, action, actionTick, null);
    }

    static Bitmap Render(MascotStyle style, MascotMood mood, int tick, int pixel, MascotAction? action, int actionTick, WalkPose walk)
    {
        Bitmap bitmap = new Bitmap(Columns * pixel, Rows * pixel, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            Canvas canvas = new Canvas(g, pixel);
            canvas.Style = style;
            canvas.Tick = tick;
            canvas.Mood = action.HasValue ? (MascotMood?)null : mood;
            canvas.Action = action;
            canvas.Glint = (walk != null && walk.Searching) || (!action.HasValue && mood == MascotMood.Waiting && tick % 4 < 2);
            if (style == MascotStyle.Santa || style == MascotStyle.Winter) DrawSnow(canvas, tick);
            if (style == MascotStyle.Halloween) DrawBats(canvas, tick);
            if (style == MascotStyle.Carnival) DrawConfetti(canvas, tick);
            if (action.HasValue) DrawAction(canvas, style, action.Value, actionTick);
            else DrawMood(canvas, style, mood, tick, walk);
        }
        return bitmap;
    }

    static void DrawSnow(Canvas canvas, int tick)
    {
        for (int flake = 0; flake < SnowFlakes; flake++)
        {
            int sway = (tick + flake * 3) / 6 % 2;
            canvas.Fill((flake * 7 + 2 + sway) % Columns, (tick + flake * 5) % Rows, 1, 1, Snow);
        }
    }

    static void DrawMood(Canvas canvas, MascotStyle style, MascotMood mood, int tick, WalkPose walk)
    {
        switch (mood)
        {
            case MascotMood.Sleep: DrawSleep(canvas, style, tick, false); break;
            case MascotMood.Night: DrawSleep(canvas, style, tick, true); break;
            case MascotMood.Work: DrawWork(canvas, style, tick, false); break;
            case MascotMood.Tired: DrawWork(canvas, style, tick, true); break;
            case MascotMood.Down: DrawDown(canvas, tick); break;
            case MascotMood.Waiting: DrawWaiting(canvas, style, tick); break;
            case MascotMood.Panic: DrawPanic(canvas, style, tick, walk); break;
            case MascotMood.Coffee: DrawCoffee(canvas, style, tick); break;
            case MascotMood.Juggle: DrawJuggle(canvas, style, tick); break;
            default: DrawIdle(canvas, style, tick, walk); break;
        }
    }

    static void DrawAction(Canvas canvas, MascotStyle style, MascotAction action, int tick)
    {
        switch (action)
        {
            case MascotAction.Celebrate: DrawCelebrate(canvas, style, tick); break;
            case MascotAction.Wave: DrawWave(canvas, style, tick); break;
            case MascotAction.Love: DrawLove(canvas, style, tick); break;
            default: DrawPet(canvas, style, tick); break;
        }
    }

    static void DrawIdle(Canvas canvas, MascotStyle style, int tick, WalkPose walk)
    {
        if (walk != null && walk.Racing)
        {
            DrawRacing(canvas, style, tick, walk);
            return;
        }
        if (walk != null)
        {
            DrawWalking(canvas, style, tick, walk);
            return;
        }
        int crouch = tick % 64 == 40 ? 2 : 0;
        int wave = tick % 96 >= 60 && tick % 96 < 68 && tick % 2 == 0 ? -2 : 0;
        int look = tick % 48 >= 8 && tick % 48 < 13 ? -1 : tick % 48 >= 30 && tick % 48 < 35 ? 1 : 0;
        DrawFigure(canvas, BodyTop + crouch, 0, wave);
        if (tick % 24 == 0) DrawClosedEyes(canvas, BodyTop + crouch);
        else DrawOpenEyes(canvas, BodyTop + crouch, look, 0, 4);
        DrawOutfit(canvas, style, BodyTop + crouch);
    }

    // The idle walk, with the little acts some outfits put on: dancing, a tray, skating, a broom, an experiment.
    static void DrawWalking(Canvas canvas, MascotStyle style, int tick, WalkPose walk)
    {
        int stride = walk.Moving ? walk.Step / 2 % 2 : 0;
        int top = BodyTop;
        int leftArm = walk.Moving ? (stride == 0 ? -1 : 0) : 0;
        int rightArm = walk.Moving ? (stride == 0 ? 0 : -1) : (walk.Searching ? -2 : 0);
        int legShift = stride;
        bool happy = false;
        switch (walk.Act)
        {
            case WalkAct.Dance:
                bool beat = walk.Step % 2 == 0;
                top = BodyTop + (beat ? 0 : 1);
                leftArm = beat ? -3 : 0;
                rightArm = beat ? 0 : -3;
                legShift = beat ? 0 : 1;
                happy = walk.AtEnd;
                break;
            case WalkAct.Tray:
                rightArm = -4;
                break;
            case WalkAct.Skate:
                legShift = 0;
                leftArm = stride == 0 ? -2 : 0;
                rightArm = stride == 0 ? 0 : -2;
                if (walk.AtEnd) top = BodyTop + walk.Step / 2 % 2; // A little pirouette hop at the end.
                break;
            case WalkAct.Broom:
                legShift = walk.Step / 3 % 2;
                leftArm = -1;
                rightArm = -1;
                break;
            case WalkAct.Experiment:
                rightArm = -2;
                break;
        }
        canvas.Mirror = walk.Returning && (walk.Act == WalkAct.Skate || walk.Act == WalkAct.Broom);
        DrawFigure(canvas, top, leftArm, rightArm, legShift);
        bool sooty = walk.Act == WalkAct.Experiment && walk.Sooty;
        if (sooty) canvas.Fill(BodyLeft + 4, top, 16, 12, Color.FromArgb(110, 30, 30, 34));
        if (happy) DrawHappyEyes(canvas, top);
        else if (tick % 24 == 0) DrawClosedEyes(canvas, top);
        else DrawOpenEyes(canvas, top, walk.Look, walk.Down ? 2 : 0, walk.Down ? 3 : (sooty ? 5 : 4));
        DrawOutfit(canvas, style, top);
        switch (walk.Act)
        {
            case WalkAct.Dance:
                if (style == MascotStyle.Carnival) DrawConfetti(canvas, tick, walk.AtEnd ? 30 : 16);
                else DrawNotes(canvas, tick);
                break;
            case WalkAct.Tray: DrawTray(canvas, top, walk.AtEnd, tick); break;
            case WalkAct.Skate: DrawSkates(canvas, top, walk.Step, walk.Moving); break;
            case WalkAct.Broom: DrawBroom(canvas, top, walk.Step); break;
            case WalkAct.Experiment:
                DrawFlask(canvas, tick, false);
                if (walk.Boom >= 0) DrawBoom(canvas, walk.Boom);
                break;
        }
        canvas.Mirror = false;
        if (walk.Sweat) canvas.Fill(BodyLeft + 23, BodyTop + walk.Step % 8, 2, 3, Sweat);
    }

    static readonly string[] Note = { "..#", "..#", "##.", "##." };

    // Music notes drifting up on both sides of the head while the headphones dance.
    static void DrawNotes(Canvas canvas, int tick)
    {
        Color[] colors = { Color.FromArgb(120, 200, 250), Color.FromArgb(250, 200, 60), Color.FromArgb(244, 94, 124) };
        for (int i = 0; i < 3; i++)
        {
            int rise = (tick + i * 5) % 14;
            int x = (i % 2 == 0 ? 0 : 28) + (tick / 4 + i) % 2;
            canvas.Art(Note, 1, x, 16 - rise, colors[i]);
        }
    }

    // A serving tray with a dome in the raised hand; at the far end the dome lifts and steam rises.
    static void DrawTray(Canvas canvas, int top, bool lifted, int tick)
    {
        Color steel = Color.FromArgb(205, 205, 212);
        int y = top + 1;
        int lift = lifted ? 3 : 0;
        canvas.Fill(23, y, 8, 1, steel);
        canvas.Fill(25, y - 3 - lift, 4, 3, steel);
        canvas.Fill(26, y - 4 - lift, 2, 1, steel);
        if (!lifted) return;
        for (int puff = 0; puff < 3; puff++)
            canvas.Fill(25 + puff * 2, y - 1 - (tick + puff * 2) % 5, 1, 1, Color.FromArgb(200, 240, 240, 245));
    }

    static void DrawSkates(Canvas canvas, int top, int step, bool moving)
    {
        Color blade = Color.FromArgb(200, 210, 225);
        foreach (int leg in new[] { 4, 8, 14, 18 })
            canvas.Fill(BodyLeft + leg - 1, top + 15, 4, 1, blade);
        if (!moving) return;
        Color snow = Color.FromArgb(190, 235, 240, 248);
        canvas.Fill(BodyLeft + 25 + step % 3, top + 13 - step % 2, 2, 1, snow);
        canvas.Fill(BodyLeft + 27 + (step + 1) % 3, top + 14, 1, 1, snow);
        canvas.Fill(BodyLeft + 26 + (step + 2) % 3, top + 15, 2, 1, snow);
    }

    static void DrawBroom(Canvas canvas, int top, int step)
    {
        int bob = step / 3 % 2;
        Color wood = Color.FromArgb(139, 94, 52);
        Color straw = Color.FromArgb(214, 170, 70);
        canvas.Fill(BodyLeft - 2, top + 12 + bob, 26, 2, wood);
        canvas.Fill(28, top + 10 + bob, 4, 6, straw);
        canvas.Fill(BodyLeft + 23, top + 12 + bob, 1, 2, Boot);
    }

    // A puff of fire and smoke around the flask, with a bright flash at first.
    static void DrawBoom(Canvas canvas, int age)
    {
        int radius = Math.Min(age + 1, 5);
        Color[] flame = { Color.FromArgb(255, 220, 80), Color.FromArgb(250, 140, 40), Color.FromArgb(170, 170, 176) };
        for (int i = 0; i < 8; i++)
        {
            int dx = (int)Math.Round(Math.Cos(i * Math.PI / 4) * radius * 1.6);
            int dy = (int)Math.Round(Math.Sin(i * Math.PI / 4) * radius * 1.2);
            canvas.Fill(28 + dx, 13 + dy, 2, 2, flame[(i + age) % flame.Length]);
        }
        if (age < 3) canvas.Fill(26, 11, 5, 5, Color.FromArgb(240, 255, 245, 160));
    }

    static readonly Color CarRed = Color.FromArgb(214, 48, 49);
    static readonly Color Tire = Color.FromArgb(62, 62, 70);
    static readonly Color Hub = Color.FromArgb(176, 176, 184);

    // The Racer's outing: a checkered flag waves, a Formula 1 car arrives, the driver climbs in and drives off.
    static void DrawRacing(Canvas canvas, MascotStyle style, int tick, WalkPose walk)
    {
        int top = walk.Seated ? BodyTop + 1 : BodyTop;
        canvas.Mirror = walk.Mirror && walk.Seated;
        if (walk.Seated)
        {
            // Only the head shows above the cockpit, so the body, arms and legs are left out.
            canvas.Fill(BodyLeft + 4, top, 16, 7, Body);
            canvas.Fill(BodyLeft + 4, top + 7, 16, 1, style == MascotStyle.Santa ? CarRed : Suit);
        }
        else DrawFigure(canvas, top, 0, walk.Flag ? -3 : 0, 0);
        if (tick % 24 == 0) DrawClosedEyes(canvas, top);
        else DrawOpenEyes(canvas, top, walk.Look, 0, 4);
        DrawOutfit(canvas, style, top);
        canvas.Mirror = walk.Mirror;
        if (walk.Car)
        {
            canvas.ShiftX = walk.Slide;
            if (style == MascotStyle.Santa) DrawSleigh(canvas, walk.Step, walk.Moving);
            else DrawCar(canvas, walk.Step, walk.Moving);
            canvas.ShiftX = 0;
        }
        canvas.Mirror = false;
        if (walk.Flag) DrawFlag(canvas, walk.Step);
    }

    // A Formula 1 car seen from the side, nose to the left. R red, W white, N navy, Y yellow.
    static readonly string[] CarArt =
    {
        "..........................WWWWWW", // Rear wing, from y12.
        "..........................NNNNNN",
        "......................RRR.R.NN.R..",
        ".........NNNNNNNNNNNRRRRR.R.NN.R",
        "...........RRRRRRRRRRRRRRRRRNN.R",
        "........RRRRRYYYRRRRRRRRRRRRNN..",
        "....RRRRRRRWWWWWWWWWWWWWWRRR....",
        "NRRRRRRRRRRRRRRRRRRRRRRRRRR.....",
        "NWWNNNNNNNNNNNNNNNNNNNNNNNN.....",
        "NWWWWWWWW......................."
    };
    const int CarTop = 12;

    // Santa's sleigh, nose to the left, with two presents on the back.
    static readonly string[] SleighArt =
    {
        "...........................GGG..",
        "...........................GWG..",
        "......................UUUUUGGG..",
        "...........DDDDDDDDDDDUWWWU.....",
        "....RR.....RRRRRRRRRRRRRRRRRRR..",
        "...RRR.....RRRRRRRRRRRRRRRRRRRR.",
        "..RRRRRYYYYYYYYYYYYYYYYYYYRRRR..",
        "..RRRRRRRRRRRRRRRRRRRRRRRRRRR...",
        ".KKKKKKKKKKKKKKKKKKKKKKKKKKKK...",
        "KK.........................KK..."
    };
    const int SleighTop = 12;

    static Color SleighColor(char letter)
    {
        switch (letter)
        {
            case 'R': return Color.FromArgb(200, 40, 45);
            case 'D': return Color.FromArgb(120, 30, 30);
            case 'Y': return Gold;
            case 'G': return Color.FromArgb(40, 160, 80);
            case 'W': return Coat;
            case 'U': return Wool;
            case 'K': return Color.FromArgb(190, 195, 205);
            default: return Color.Empty;
        }
    }

    static void DrawSleigh(Canvas canvas, int step, bool moving)
    {
        for (int row = 0; row < SleighArt.Length; row++)
            for (int column = 0; column < SleighArt[row].Length && column < Columns; column++)
            {
                Color color = SleighColor(SleighArt[row][column]);
                if (!color.IsEmpty) canvas.Fill(column, SleighTop + row, 1, 1, color);
            }
        if (!moving) return;
        Color snow = Color.FromArgb(190, 235, 240, 248);
        canvas.Fill(29 + step % 3, 21 - step % 2, 2, 1, snow);
        canvas.Fill(30 + (step + 1) % 2, 19 + step % 3, 1, 1, snow);
    }

    static void DrawCar(Canvas canvas, int step, bool moving)
    {
        for (int row = 0; row < CarArt.Length; row++)
            for (int column = 0; column < CarArt[row].Length; column++)
            {
                Color color;
                switch (CarArt[row][column])
                {
                    case 'R': color = CarRed; break;
                    case 'W': color = Coat; break;
                    case 'N': color = Suit; break;
                    case 'Y': color = Gold; break;
                    default: continue;
                }
                canvas.Fill(column, CarTop + row, 1, 1, color);
            }
        bool spin = step % 2 == 0;
        canvas.Fill(8, 17, 5, 5, Tire);
        canvas.Fill(21, 15, 8, 7, Tire);
        canvas.Fill(10, 19, 1, 1, Hub);
        canvas.Fill(spin ? 9 : 11, 18, 1, 1, Hub);
        canvas.Fill(23, 17, 3, 3, Hub);
        canvas.Fill(spin ? 24 : 23, spin ? 18 : 19, 1, 1, Tire);
        if (!moving) return;
        Color puff = Color.FromArgb(150, 175, 175, 180);
        canvas.Fill(29 + step % 3, 17 + step / 2 % 3, 1, 1, puff);
        canvas.Fill(30 + step % 2, 20 - step % 2, 1, 1, puff);
    }

    // A pole at the left edge with a checkered cloth that flutters.
    static void DrawFlag(Canvas canvas, int step)
    {
        canvas.Fill(1, 3, 1, 17, Color.FromArgb(150, 150, 156));
        for (int column = 0; column < 3; column++)
        {
            int dip = (step / 2 + column) % 2;
            for (int row = 0; row < 2; row++)
                canvas.Fill(2 + column * 2, 3 + row * 2 + dip, 2, 2, (column + row) % 2 == 0 ? Boot : Coat);
        }
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
        if (night && !Dressed(style)) DrawArt(canvas, NightCap, top);
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
        canvas.Art(QuestionMark, 1, style == MascotStyle.Detective ? 1 : 27, 1 + (tick % 4 < 2 ? 0 : 1), Question);
    }

    static void DrawPanic(Canvas canvas, MascotStyle style, int tick, WalkPose walk)
    {
        canvas.ShiftX = tick % 2 == 0 ? -1 : 1;
        bool up = tick % 4 < 2;
        DrawFigure(canvas, BodyTop, up ? -4 : -2, up ? -2 : -4, walk != null && walk.Moving ? tick % 2 : 0);
        DrawOpenEyes(canvas, BodyTop, walk != null ? walk.Look : 0, 0, 5);
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
        DrawSteam(canvas, mugX, mugY, tick);
    }

    // Three puffs that rise, sway and fade.
    static void DrawSteam(Canvas canvas, int mugX, int mugY, int tick)
    {
        for (int puff = 0; puff < SteamPuffs; puff++)
        {
            int rise = (tick + puff * SteamHeight / SteamPuffs) % SteamHeight;
            int sway = rise % 4 < 2 ? 0 : 1;
            int alpha = 230 - rise * 170 / SteamHeight;
            canvas.Fill(mugX + sway, mugY - 1 - rise, 1, 1, Color.FromArgb(alpha, Steam));
            canvas.Fill(mugX + 2 - sway, mugY - 1 - rise, 1, 1, Color.FromArgb(alpha, Steam));
        }
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

    // Click the mascot again and again and it melts: hearts everywhere, a happy bounce and blushing cheeks.
    static void DrawLove(Canvas canvas, MascotStyle style, int tick)
    {
        int top = BodyTop + (tick % 4 < 2 ? 0 : 2);
        DrawFigure(canvas, top, -3, -3);
        DrawHappyEyes(canvas, top);
        canvas.Fill(BodyLeft + 5, top + 6, 3, 1, Heart);
        canvas.Fill(BodyLeft + 16, top + 6, 3, 1, Heart);
        DrawOutfit(canvas, style, top);
        for (int heart = 0; heart < 5; heart++)
        {
            int rise = (tick * 2 + heart * 7) % 22;
            canvas.Art(HeartShape, 1, 2 + heart * 6 + (rise / 3 % 2), Rows - 3 - rise, Heart);
        }
    }

    static void DrawBats(Canvas canvas, int tick)
    {
        Color bat = Color.FromArgb(150, 110, 200);
        for (int i = 0; i < 2; i++)
        {
            int x = (tick + i * 19) % 46 - 8;
            int y = 2 + i * 5 + (tick / 3 + i) % 2;
            canvas.Art(((tick / 2 + i) % 2 == 0) ? new[] { "#.#.#", ".###." } : new[] { ".###.", "#.#.#" }, 1, x, y, bat);
        }
    }

    static void DrawConfetti(Canvas canvas, int tick)
    {
        DrawConfetti(canvas, tick, 14);
    }

    static void DrawConfetti(Canvas canvas, int tick, int count)
    {
        for (int piece = 0; piece < count; piece++)
        {
            int sway = (tick + piece * 3) / 5 % 2;
            canvas.Fill((piece * 7 + 1 + sway) % Columns, (tick + piece * 4) % Rows, 1, 2, Bright[piece % Bright.Length]);
        }
    }

    static void DrawCelebrate(Canvas canvas, MascotStyle style, int tick)
    {
        if (style == MascotStyle.Racer) DrawFlag(canvas, tick);
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
        canvas.RightArm = rightArm;
        DrawClothes(canvas, top, leftArm, rightArm, legShift);
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
            case MascotStyle.Headphones: return Earbuds;
            case MascotStyle.Chef: return ChefHat;
            default: return null;
        }
    }

    static bool Dressed(MascotStyle style)
    {
        return OutfitOf(style) != null || style == MascotStyle.Scientist || style == MascotStyle.Detective
            || style == MascotStyle.Winter || style == MascotStyle.Racer || style == MascotStyle.Halloween
            || style == MascotStyle.Carnival;
    }

    static void DrawOutfit(Canvas canvas, MascotStyle style, int top)
    {
        Outfit outfit = OutfitOf(style);
        if (outfit != null) DrawArt(canvas, outfit, top);
        else DrawAccessories(canvas, style, top);
    }

    static readonly Color Coat = Color.FromArgb(244, 244, 246);
    static readonly Color CoatShade = Color.FromArgb(211, 211, 218);
    static readonly Color Frame = Color.FromArgb(68, 68, 68);
    static readonly Color Trench = Color.FromArgb(176, 141, 87);
    static readonly Color TrenchDark = Color.FromArgb(122, 90, 48);
    static readonly Color Brown = Color.FromArgb(122, 82, 48);
    static readonly Color Gold = Color.FromArgb(250, 204, 21);
    static readonly Color Wool = Color.FromArgb(59, 127, 212);
    static readonly Color Ice = Color.FromArgb(232, 238, 252);
    static readonly Color Scarf = Color.FromArgb(214, 48, 49);
    static readonly Color Suit = Color.FromArgb(39, 64, 139);
    static readonly Color Boot = Color.FromArgb(34, 34, 34);
    static readonly Color Violet = Color.FromArgb(72, 40, 110);
    static readonly Color Pumpkin = Color.FromArgb(245, 130, 32);
    static readonly Color Pink = Color.FromArgb(240, 90, 160);
    static readonly Color Teal = Color.FromArgb(40, 200, 190);

    // Clothes on the body sit under the laptop and the mug, so they are drawn right after the figure.
    static void DrawClothes(Canvas canvas, int top, int leftArm, int rightArm, int legShift)
    {
        int left = BodyLeft;
        switch (canvas.Style)
        {
            case MascotStyle.Scientist:
                canvas.Fill(left, top + 6 + leftArm, 4, 3, Coat);
                canvas.Fill(left + 20, top + 6 + rightArm, 4, 3, Coat);
                canvas.Fill(left + 4, top + 7, 16, 5, Coat);
                canvas.Fill(left + 11, top + 7, 2, 5, CoatShade);
                canvas.Fill(left + 7, top + 9, 2, 2, CoatShade);
                break;
            case MascotStyle.Detective:
                canvas.Fill(left, top + 6 + leftArm, 4, 3, Trench);
                canvas.Fill(left + 20, top + 6 + rightArm, 4, 3, Trench);
                canvas.Fill(left + 4, top + 6, 16, 6, Trench);
                canvas.Fill(left + 4, top + 12, 7, 3, Trench);
                canvas.Fill(left + 13, top + 12, 7, 3, Trench);
                canvas.Fill(left + 11, top + 6, 2, 9, TrenchDark);
                canvas.Fill(left + 4, top + 6, 3, 3, TrenchDark);
                canvas.Fill(left + 17, top + 6, 3, 3, TrenchDark);
                canvas.Fill(left + 4, top + 9, 16, 1, TrenchDark);
                canvas.Fill(left + 10, top + 9, 4, 1, Gold);
                break;
            case MascotStyle.Winter:
                canvas.Fill(left + 4, top + 8, 16, 2, Scarf);
                canvas.Fill(left + 8, top + 8, 2, 2, Coat);
                canvas.Fill(left + 14, top + 8, 2, 2, Coat);
                int flutter = canvas.Tick / 3 % 2;
                canvas.Fill(left + 15 + flutter, top + 10, 3, 5, Scarf);
                canvas.Fill(left + 15 + flutter, top + 12, 3, 1, Coat);
                break;
            case MascotStyle.Halloween:
                canvas.Fill(left, top + 6 + leftArm, 4, 3, Violet);
                canvas.Fill(left + 20, top + 6 + rightArm, 4, 3, Violet);
                canvas.Fill(left + 4, top + 7, 16, 5, Violet);
                canvas.Fill(left + 4, top + 7, 16, 1, Pumpkin);
                canvas.Fill(left + 4, top + 11, 16, 1, Pumpkin);
                canvas.Fill(left + 10, top + 9, 4, 1, Gold);
                break;
            case MascotStyle.Carnival:
                canvas.Fill(left + 4, top + 8, 16, 2, Pink);
                canvas.Fill(left + 4, top + 8, 4, 2, Gold);
                canvas.Fill(left + 16, top + 8, 4, 2, Teal);
                break;
            case MascotStyle.Racer:
                foreach (int leg in new[] { 4, 8, 14, 18 })
                {
                    int height = 4 - legShift;
                    canvas.Fill(left + leg, top + 12, 2, height - 1, Suit);
                    canvas.Fill(left + leg, top + 11 + height, 2, 1, Boot);
                }
                canvas.Fill(left, top + 6 + leftArm, 4, 3, Suit);
                canvas.Fill(left + 20, top + 6 + rightArm, 4, 3, Suit);
                canvas.Fill(left, top + 9 + leftArm, 4, 1, Coat);
                canvas.Fill(left + 20, top + 9 + rightArm, 4, 1, Coat);
                canvas.Fill(left + 4, top + 7, 16, 5, Suit);
                canvas.Fill(left + 4, top + 8, 16, 1, Coat);
                canvas.Fill(left + 6, top + 6, 12, 1, Boot);
                canvas.Fill(left + 10, top + 7, 4, 1, Coat);
                canvas.Fill(left + 15, top + 9, 4, 2, Gold);
                canvas.Fill(left + 11, top + 7, 2, 5, Color.FromArgb(46, 255, 255, 255));
                canvas.Fill(left + 4, top + 10, 16, 1, Boot);
                canvas.Fill(left + 11, top + 10, 2, 1, Gold);
                break;
        }
    }

    static bool Asleep(Canvas canvas)
    {
        return canvas.Mood == MascotMood.Sleep || canvas.Mood == MascotMood.Night;
    }

    static bool Celebrating(Canvas canvas)
    {
        return canvas.Action == MascotAction.Celebrate;
    }

    // Hats ride the head: pulled down over the eyes asleep, bouncing in a panic, tossed in the air to celebrate.
    static int HatShift(Canvas canvas)
    {
        if (Asleep(canvas)) return 3;
        if (canvas.Mood == MascotMood.Panic) return -(canvas.Tick % 2);
        return Celebrating(canvas) ? -(canvas.Tick * 2 % 14) : 0;
    }

    // Hats, goggles and the magnifying glass sit over the face.
    static void DrawAccessories(Canvas canvas, MascotStyle style, int top)
    {
        int left = BodyLeft;
        int hat = HatShift(canvas);
        switch (style)
        {
            case MascotStyle.Scientist:
                int goggles = Asleep(canvas) ? -4 : 0; // Pushed up onto the forehead to sleep.
                Goggle(canvas, left + 5, top + goggles);
                Goggle(canvas, left + 15, top + goggles);
                if (goggles == 0) canvas.Fill(left + 10, top + 3, 5, 1, Frame);
                if (canvas.Mood == MascotMood.Panic && canvas.Tick % 2 == 0)
                {
                    canvas.Fill(left + 6, top + 2, 3, 4, Color.FromArgb(170, 255, 60, 60));
                    canvas.Fill(left + 16, top + 2, 3, 4, Color.FromArgb(170, 255, 60, 60));
                }
                bool stirring = canvas.Mood == MascotMood.Work || canvas.Mood == MascotMood.Tired
                    || canvas.Mood == MascotMood.Juggle || canvas.Mood == MascotMood.Waiting;
                if (stirring || Celebrating(canvas)) DrawFlask(canvas, canvas.Tick, Celebrating(canvas));
                break;
            case MascotStyle.Detective:
                canvas.Fill(left + 2, top - 1 + hat, 20, 2, Brown);
                canvas.Fill(left + 5, top - 5 + hat, 14, 4, Color.FromArgb(141, 98, 56));
                canvas.Fill(left + 5, top - 2 + hat, 14, 1, Color.FromArgb(59, 42, 26));
                canvas.Fill(left + 11, top - 6 + hat, 2, 1, Color.FromArgb(59, 42, 26));
                DrawLens(canvas, top + 6 + canvas.RightArm);
                break;
            case MascotStyle.Winter:
                if (Asleep(canvas)) canvas.Fill(left + 3, top - 2, 3, 3, Coat); // The pompom droops asleep.
                else canvas.Fill(left + 10, top - 6, 4, 3, Coat);
                canvas.Fill(left + 6, top - 3, 12, 3, Wool);
                canvas.Fill(left + 8, top - 3, 2, 2, Ice);
                canvas.Fill(left + 14, top - 3, 2, 2, Ice);
                canvas.Fill(left + 4, top - 1, 16, 2, Ice);
                foreach (int rib in new[] { 6, 10, 14, 18 })
                    canvas.Fill(left + rib, top - 1, 1, 2, Color.FromArgb(195, 205, 230));
                if (canvas.Mood == MascotMood.Panic)
                {
                    Color breath = Color.FromArgb(200, 235, 240, 248);
                    canvas.Fill(left + 21 + canvas.Tick % 3, top + 5, 2, 1, breath);
                    canvas.Fill(left + 23 + canvas.Tick % 2, top + 3 - canvas.Tick % 2, 1, 1, breath);
                }
                break;
            case MascotStyle.Halloween:
                canvas.Fill(left + 2, top - 1 + hat, 20, 2, Violet);
                for (int row = 0; row < 5; row++)
                {
                    int width = 12 - row * 2;
                    canvas.Fill(left + 12 - width / 2, top - 2 - row + hat, width, 1, Violet);
                }
                canvas.Fill(left + 6, top - 2 + hat, 12, 1, Pumpkin);
                canvas.Fill(left + 11, top - 2 + hat, 2, 1, Gold);
                break;
            case MascotStyle.Carnival:
                int mask = Asleep(canvas) ? -5 : 0; // Pushed up onto the forehead to sleep.
                canvas.Fill(left + 4, top, 16, 1, Gold);
                canvas.Fill(left + 2, top - 3, 2, 3, Pink);
                canvas.Fill(left + 1, top - 5, 2, 2, Pink);
                canvas.Fill(left + 6, top - 5, 2, 5, Color.FromArgb(250, 204, 21));
                canvas.Fill(left + 10, top - 4, 2, 4, Teal);
                canvas.Fill(left + 11, top - 6, 2, 2, Teal);
                canvas.Fill(left + 4, top + 1 + mask, 16, 1, Gold);
                canvas.Fill(left + 4, top + 6 + mask, 16, 1, Violet);
                canvas.Fill(left + 4, top + 2 + mask, 2, 4, Violet);
                canvas.Fill(left + 10, top + 2 + mask, 4, 4, Violet);
                canvas.Fill(left + 18, top + 2 + mask, 2, 4, Violet);
                break;
            case MascotStyle.Racer:
                if (canvas.Mood == MascotMood.Panic && canvas.Tick % 2 == 0)
                    canvas.Fill(left + 5, top + 1, 14, 5, Color.FromArgb(70, 255, 40, 40));
                canvas.Fill(left + 6, top - 4, 12, 1, Scarf);
                canvas.Fill(left + 5, top - 3, 14, 1, Scarf);
                canvas.Fill(left + 4, top - 2, 16, 3, Scarf);
                canvas.Fill(left + 11, top - 4, 2, 5, Coat);
                canvas.Fill(left + 4, top + 1, 1, 5, Scarf);
                canvas.Fill(left + 19, top + 1, 1, 5, Scarf);
                canvas.Fill(left + 4, top + 6, 16, 1, Scarf);
                canvas.Fill(left + 5, top + 1, 3, 1, Color.FromArgb(89, 255, 255, 255));
                if (canvas.Mood == MascotMood.Waiting) DrawStopwatch(canvas, canvas.Tick);
                break;
        }
    }

    // The Racer checks a stopwatch while it waits for you.
    static void DrawStopwatch(Canvas canvas, int tick)
    {
        Color ring = Color.FromArgb(205, 205, 212);
        canvas.Fill(3, 7, 2, 1, ring);
        canvas.Fill(2, 8, 4, 1, ring);
        canvas.Fill(1, 9, 1, 4, ring);
        canvas.Fill(6, 9, 1, 4, ring);
        canvas.Fill(2, 13, 4, 1, ring);
        canvas.Fill(2, 9, 4, 4, Color.FromArgb(40, 40, 46));
        int[,] hand = { { 3, 9 }, { 5, 10 }, { 3, 12 }, { 2, 10 } };
        int position = tick / 2 % 4;
        canvas.Fill(hand[position, 0], hand[position, 1], 1, 2, Color.FromArgb(250, 204, 21));
    }

    // A bubbling flask the Scientist holds while working; it overflows to celebrate.
    static void DrawFlask(Canvas canvas, int tick, bool burst)
    {
        Color glass = Color.FromArgb(190, 200, 230, 245);
        Color liquid = Color.FromArgb(70, 210, 120);
        canvas.Fill(27, 13, 2, 3, glass);
        canvas.Fill(26, 16, 4, 1, glass);
        canvas.Fill(25, 17, 6, 4, glass);
        canvas.Fill(26, 18, 4, 3, liquid);
        for (int bubble = 0; bubble < (burst ? 6 : 2); bubble++)
            canvas.Fill(26 + (tick / 3 + bubble) % 4, 12 - (tick * (burst ? 2 : 1) + bubble * 3) % (burst ? 11 : 5), 1, 1, bubble % 2 == 0 ? liquid : Color.White);
    }

    static void Goggle(Canvas canvas, int x, int top)
    {
        canvas.Fill(x, top + 1, 5, 1, Frame);
        canvas.Fill(x, top + 6, 5, 1, Frame);
        canvas.Fill(x, top + 1, 1, 6, Frame);
        canvas.Fill(x + 4, top + 1, 1, 6, Frame);
        canvas.Fill(x + 1, top + 2, 3, 4, Color.FromArgb(140, 159, 216, 255));
    }

    // The magnifying glass rises from the right hand, so it follows the arm.
    static void DrawLens(Canvas canvas, int armTop)
    {
        int x = BodyLeft + 22;
        Color rim = Color.FromArgb(154, 154, 162);
        canvas.Fill(x, armTop - 9, 6, 1, rim);
        canvas.Fill(x, armTop - 4, 6, 1, rim);
        canvas.Fill(x, armTop - 9, 1, 6, rim);
        canvas.Fill(x + 5, armTop - 9, 1, 6, rim);
        canvas.Fill(x + 1, armTop - 8, 4, 4, canvas.Glint ? Color.White : Color.FromArgb(207, 234, 255));
        canvas.Fill(x + 1, armTop - 3, 1, 3, Color.FromArgb(107, 74, 43));
    }

    static void DrawArt(Canvas canvas, Outfit outfit, int top)
    {
        for (int row = 0; row < outfit.Art.Length; row++)
        {
            string line = outfit.Art[row];
            for (int column = 0; column < line.Length; column++)
            {
                if (line[column] == '.') continue;
                canvas.Fill(BodyLeft + outfit.Left + column * outfit.Block, top - 6 + row * outfit.Block, outfit.Block, outfit.Block, OutfitColor(line[column]));
            }
        }
    }

    static Color OutfitColor(char letter)
    {
        switch (letter)
        {
            case 'r': return Color.FromArgb(214, 48, 49);
            case 'g': return Color.FromArgb(150, 150, 156);
            case 's': return Color.FromArgb(198, 198, 206);
            case 'd': return Color.FromArgb(84, 84, 92);
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
        public MascotStyle Style;
        public int RightArm;
        public bool Glint;
        public bool Mirror;
        public int Tick;
        public MascotMood? Mood;
        public MascotAction? Action;

        public Canvas(Graphics graphics, int pixel)
        {
            this.graphics = graphics;
            this.pixel = pixel;
        }

        public void Fill(int x, int y, int width, int height, Color color)
        {
            if (UpsideDownAround.HasValue) y = UpsideDownAround.Value - y - height;
            if (Mirror) x = Columns - x - width;
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

using Godot;
using MathRobot.Core;
using MathRobot.Game.Snes;

namespace MathRobot.Game.Screens;

/// <summary>
/// The main game: one Stage of subtraction problems on the number line. For each problem the player slides the
/// green Start flag to the first number and locks it, then hops Beep back (1 or 10 at a time) until the hops add up
/// to the number taken away, and presses GO. Beep hops the chain, counting as he lands, and the orange Land flag is
/// the answer. Underneath, a Mode 7 circuit floor scrolls along with the line.
/// </summary>
public partial class PlayScreen : Screen
{
    private enum State { Intro, Solving, Hopping, Feedback, Paused, Outro }

    private const float LineY = 160;

    private readonly Stage _stage;
    private readonly string _demo;
    private StageRun _run = null!;
    private HopLine _line = null!;
    private State _state = State.Intro;
    private State _beforePause;
    private double _stateTime;
    private double _problemTime;
    private float _unit;
    private float _baseUnit;
    private float _view;
    private Outfit _outfit = new();
    private string _message = "";
    private Color _messageColor = Gfx.White;
    private double _messageTime = -10;
    private float _shake;

    // Beep on the line: where he stands (in numbers), how high he is and how squashed.
    private float _beepAt;
    private float _beepLift;
    private Vector2 _squash = Vector2.One;
    private string? _mood;

    // The GO animation.
    private readonly List<(float From, float To, int Size)> _hopPath = new();
    private int _hopIndex;
    private double _hopT;

    // Feedback after an answer.
    private bool _lastRight;
    private StageRun.Outcome _lastOutcome;
    private IReadOnlyList<int>? _ghostHops;
    private int _ghostStart;
    private double _hintUntil = -1;
    private double _answerPop = -1;
    private string _cheer = "";
    private readonly List<int> _pips = new(); // 1 right, 2 retried later, 3 shown

    // The boss.
    private int _bossHp;
    private double _bossHitAt = -10;
    private double _zapAt = -10;
    private bool _bossDown;

    public PlayScreen(Stage stage, string demo = "")
    {
        _stage = stage;
        _demo = demo;
    }

    public override void Enter()
    {
        _outfit = Outfit.Of(P);
        var plane = AddPlane();
        plane.SetMap(WorldArt.Floor, tiled: true);
        plane.SetSea(WorldArt.Sea);
        plane.SetSky(WorldArt.SkyTech);
        plane.SetColors(new Color("0c0a2a"), new Color("6a3a9a"), new Color("6a3a9a"), 520);
        plane.CamHeight = 26;
        plane.Focal = 110;
        plane.Horizon = 100;

        _unit = _baseUnit = _stage.LineMax <= 10 ? 20 : _stage.LineMax <= 20 ? 11 : 12;
        var problems = ProblemDealer.Deal(_stage, M.Rng);
        _run = new StageRun(_stage, problems, P.Perks);
        _bossHp = _run.Total;
        Pad.RepeatEvery = P.RepeatSeconds;
        NewLine();
        _view = TargetView();
        M.Sound.PlayMusic(_stage.Boss ? "boss" : "play");

        if (_demo is "hop" or "bighop" or "boss" or "go" or "gowrong" or "bossgo") SetUpDemo();
    }

    public override void Leave() => Pad.RepeatEvery = 0.15;

    private void SetUpDemo()
    {
        _state = State.Solving;
        var problem = _demo == "bighop" ? new Problem(63, 27) : _demo.StartsWith("boss") ? new Problem(52, 17) : new Problem(13, 5);
        _line = new HopLine(problem, _stage.LineMax, 0);
        while (_line.Start < problem.Minuend && _line.MoveStart(problem.Minuend - _line.Start >= 10 ? 10 : 1)) { }
        _line.LockStart();
        int[] hops = _demo switch
        {
            "bighop" => new[] { 10, 10, 1, 1, 1, 1 },
            "boss" => new[] { 10, 1, 1, 1 },
            "bossgo" => new[] { 10, 1, 1, 1, 1, 1, 1, 1 },
            "go" => new[] { 1, 1, 1, 1, 1 },
            "gowrong" => new[] { 1, 1, 1, 1 },
            _ => new[] { 1, 1, 1 },
        };
        foreach (var h in hops) _line.HopBack(h);
        _beepAt = _line.Start;
        _view = TargetView();
        _pips.AddRange(new[] { 1, 1, 2 });
        if (_demo.StartsWith("boss")) _bossHp -= 3;
        if (_demo.Contains("go"))
        {
            _line.Go();
            StartHopping();
        }
    }

    private void NewLine()
    {
        _line = new HopLine(_run.Current, _stage.LineMax, 0);
        _beepAt = 0;
        _beepLift = 0;
        _squash = Vector2.One;
        _mood = null;
        _ghostHops = null;
        _problemTime = 0;
        _answerPop = -1;
    }

    private void SetState(State s)
    {
        _state = s;
        _stateTime = 0;
    }

    private void Say(string text, Color color)
    {
        _message = text;
        _messageColor = color;
        _messageTime = Time;
    }

    private float X(float n) => Mathf.Floor(Gfx.W / 2f + (n - _view) * _unit);

    private float TargetView()
    {
        float half = (Gfx.W / 2f - 14) / _unit;
        if (_stage.LineMax * _unit <= Gfx.W - 28) return _stage.LineMax / 2f;
        float target = _state == State.Hopping || _state == State.Feedback ? _beepAt
            : _line.Phase == HopPhase.PlaceStart ? _line.Start
            : (_line.Start - _line.Land) <= 2 * half - 4 ? (_line.Start + _line.Land) / 2f
            : _line.Land + half - 3;
        return Math.Clamp(target, half - 1.5f, _stage.LineMax - half + 1.5f);
    }

    // ---------------------------------------------------------------- update

    protected override void Update(double dt)
    {
        _stateTime += dt;
        _shake = Math.Max(0, _shake - (float)dt * 30);
        // Long hop chains on the two-digit lines zoom out so the whole chain stays on screen.
        if (_stage.TwoDigit)
        {
            int span = _line.Phase == HopPhase.PlaceStart && _state == State.Solving ? 0 : _line.Start - _line.Land;
            float want = Math.Clamp((Gfx.W - 24) / (span + 6f), 5.5f, _baseUnit);
            _unit += (want - _unit) * Math.Min(1, (float)dt * 5);
        }
        _view += (TargetView() - _view) * Math.Min(1, (float)dt * 7);
        _squash = _squash.Lerp(Vector2.One, Math.Min(1, (float)dt * 12));
        if (Plane is { } plane)
        {
            plane.Cam = new Vector2(_view * 16, plane.Cam.Y - (float)dt * 14);
            plane.Angle = Mathf.Sin((float)Time * 0.3f) * 0.04f;
        }

        switch (_state)
        {
            case State.Intro:
                if (_demo == "auto" && _stateTime > 0.5) Pad.Inject(Btn.A);
                if (_stateTime > (_stage.Boss ? 2.6 : 2.0) || (_stateTime > 0.4 && Pad.Pressed(Btn.A)))
                {
                    SetState(State.Solving);
                    M.Sound.Play("confirm");
                }
                break;
            case State.Solving:
                _run.Tick(dt);
                _problemTime += dt;
                if (_demo == "auto") Bot();
                Solve();
                break;
            case State.Hopping:
                _run.Tick(dt);
                Hop(dt);
                break;
            case State.Feedback:
                bool wait = !_lastRight;
                if (_demo == "auto" && wait && _stateTime > 0.8) Pad.Inject(Btn.A);
                if ((!wait && _stateTime > 1.15) || (wait && _stateTime > 0.6 && (Pad.Pressed(Btn.A) || Pad.Pressed(Btn.B))))
                    Next();
                break;
            case State.Paused:
                if (Pad.Pressed(Btn.A) || Pad.Pressed(Btn.Start))
                {
                    _state = _beforePause;
                    M.Sound.Play("confirm");
                }
                else if (Pad.Pressed(Btn.B) || Pad.Pressed(Btn.Select))
                {
                    M.Sound.Play("cancel");
                    M.Go(new MapScreen());
                }
                break;
            case State.Outro:
                if (_stateTime > 2.2 || (_stateTime > 0.8 && Pad.Pressed(Btn.A))) Finish();
                break;
        }

        if (_state is State.Solving or State.Hopping or State.Intro && (Pad.Pressed(Btn.Start) || Pad.Pressed(Btn.Select)))
        {
            _beforePause = _state;
            _state = State.Paused;
            M.Sound.Play("cancel");
        }

        if (_state == State.Hopping && _beepLift > 2) Fx.Trail(_outfit.Trail, new Vector2(X(_beepAt), LineY - _beepLift), Time);
    }

    private void Solve()
    {
        if (_line.Phase == HopPhase.PlaceStart)
        {
            int move = 0;
            if (Pad.Repeat(Btn.Right)) move = 1;
            else if (Pad.Repeat(Btn.Left)) move = -1;
            else if (Pad.Repeat(Btn.RB)) move = 10;
            else if (Pad.Repeat(Btn.LB)) move = -10;
            if (move != 0)
            {
                if (_line.MoveStart(move))
                {
                    _beepAt = _line.Start;
                    M.Sound.Play(Math.Abs(move) == 10 ? "tens" : "tick", 1f + Math.Min(_line.Start, 100) * 0.004f);
                }
                else Bump();
            }
            if (Pad.Pressed(Btn.A))
            {
                if (_line.LockStart() == HopRefusal.WrongStart)
                {
                    M.Sound.Play("error");
                    _shake = 6;
                    string dir = _line.Start < _line.Problem.Minuend ? "more to the right" : "more to the left";
                    Say($"That's {_line.Start}. Find {_line.Problem.Minuend}, {dir}!", Gfx.Orange);
                }
                else
                {
                    M.Sound.Play("lock");
                    _squash = new Vector2(1.3f, 0.7f);
                    Fx.Burst(Particles.Kind.Sparkle, new Vector2(X(_line.Start), LineY), 8, Gfx.Green, 50, 60, 0.5);
                    Say($"Now hop back {_line.Problem.Subtrahend}!", Gfx.Cyan);
                }
            }
        }
        else if (_line.Phase == HopPhase.Hop)
        {
            if (Pad.Repeat(Btn.Left)) AddHop(1);
            else if (Pad.Repeat(Btn.LB)) AddHop(10);
            else if (Pad.Repeat(Btn.Right))
            {
                if (_line.Shrink(1)) M.Sound.Play("cancel", 1.2f);
                else Bump();
            }
            else if (Pad.Repeat(Btn.RB))
            {
                if (_line.Shrink(10)) M.Sound.Play("cancel", 0.9f);
                else Bump();
            }
            if (Pad.Pressed(Btn.B))
            {
                _line.Undo();
                M.Sound.Play("cancel");
                if (_line.Phase == HopPhase.PlaceStart) Say($"Start at {_line.Problem.Minuend}!", Gfx.Green);
            }
            if (Pad.Pressed(Btn.A))
            {
                if (_line.Go() == HopRefusal.NoHops)
                {
                    M.Sound.Play("error");
                    Say($"Hop back {_line.Problem.Subtrahend} first! Press {(Pad.ControllerConnected ? "<" : "<")}.", Gfx.Orange);
                }
                else StartHopping();
            }
        }

        if (Pad.Pressed(Btn.Y))
        {
            if (_run.UseHint())
            {
                M.Sound.Play("star");
                _hintUntil = Time + 3;
                if (_line.Phase == HopPhase.PlaceStart) Say($"Look for {_line.Problem.Minuend}: it glows!", Gfx.Gold);
                else Say(HintText(_line.Problem), Gfx.Gold);
            }
            else if (P.Owns("lamp"))
            {
                M.Sound.Play("error");
                Say("The Hint Lamp is out for this Stage.", Gfx.Grey);
            }
        }
    }

    private int _botFrame;
    private bool _botSlipped;

    /// <summary>
    /// For checking the game end to end (--screen play:auto): presses the pad's buttons like a player would,
    /// getting the first problem wrong on purpose so the retry path runs too.
    /// </summary>
    private void Bot()
    {
        if (++_botFrame % 3 != 0) return;
        var p = _line.Problem;
        if (_line.Phase == HopPhase.PlaceStart)
        {
            int gap = p.Minuend - _line.Start;
            Pad.Inject(gap >= 10 ? Btn.RB : gap > 0 ? Btn.Right : gap <= -10 ? Btn.LB : gap < 0 ? Btn.Left : Btn.A);
            return;
        }
        int want = _botSlipped ? p.Subtrahend : Math.Max(1, p.Subtrahend - 1);
        int left = want - _line.Taken;
        if (left == 0)
        {
            _botSlipped = true;
            Pad.Inject(Btn.A);
        }
        else Pad.Inject(left >= 10 ? Btn.LB : left > 0 ? Btn.Left : Btn.Right);
    }

    private static string HintText(Problem p)
    {
        int tens = p.Subtrahend / 10, ones = p.Subtrahend % 10;
        if (tens == 0) return $"Hop back 1, {p.Subtrahend} times!";
        if (ones == 0) return $"Hop back 10, {tens} time{(tens > 1 ? "s" : "")}!";
        return $"{tens} hop{(tens > 1 ? "s" : "")} of 10, then {ones} of 1!";
    }

    private void AddHop(int size)
    {
        if (_line.HopBack(size))
        {
            M.Sound.Play(size == 10 ? "tens" : "tick", 1.3f - Math.Min(_line.Hops.Count, 20) * 0.015f);
            var at = new Vector2(X(_line.Land), LineY - 4);
            Fx.Burst(Particles.Kind.Dot, at, 3, size == 10 ? Gfx.Gold : Gfx.Cyan, 30, 60, 0.3);
        }
        else
        {
            Bump();
            if (_line.Land - size < 0) Say("Can't hop past 0!", Gfx.Orange);
        }
    }

    private void Bump()
    {
        M.Sound.Play("error", 1.3f, -6);
        _shake = 3;
    }

    private void StartHopping()
    {
        M.Sound.Play("confirm");
        _hopPath.Clear();
        float at = _line.Start;
        foreach (int h in _line.Hops)
        {
            _hopPath.Add((at, at - h, h));
            at -= h;
        }
        _hopIndex = 0;
        _hopT = 0;
        _squash = new Vector2(1.3f, 0.7f);
        SetState(State.Hopping);
    }

    private void Hop(double dt)
    {
        float speed = 1 + 0.18f * P.LevelOf("treads");
        // Long chains of ones hop faster so the wait stays short.
        if (_line.Hops.Count > 12) speed *= 1.5f;
        var (from, to, size) = _hopPath[_hopIndex];
        double length = (size == 10 ? 0.42 : 0.2) / speed;
        _hopT += dt;
        float t = (float)Math.Min(1, _hopT / length);
        _beepAt = Mathf.Lerp(from, to, t);
        _beepLift = (size == 10 ? 34 : 11) * 4 * t * (1 - t);
        _squash = t < 0.3f ? new Vector2(0.85f, 1.2f) : _squash;
        if (t < 1) return;

        // Landed on the next number: count it out loud.
        _hopIndex++;
        _hopT = 0;
        _beepLift = 0;
        _squash = new Vector2(1.25f, 0.8f);
        M.Sound.Play(size == 10 ? "bighop" : "hop", 1f + Math.Min(_hopIndex, 15) * 0.05f);
        Fx.Pop(((int)to).ToString(), new Vector2(X(to), LineY - 34), size == 10 ? Gfx.Gold : Gfx.White);
        if (_hopIndex >= _hopPath.Count) Land();
    }

    private void Land()
    {
        _lastRight = _line.Correct;
        double took = _problemTime;
        _lastOutcome = _run.Answer(_lastRight, took);
        GD.Print($"ANSWER {_line.Problem} hopped {_line.Taken} -> {(_lastRight ? "right" : _lastOutcome.ToString())}");
        _pips.Add(_lastRight ? 1 : _lastOutcome == StageRun.Outcome.TryAgainLater ? 2 : 3);
        SetState(State.Feedback);
        var spot = new Vector2(X(_line.Land), LineY - 12);
        if (_lastRight)
        {
            M.Sound.Play("land");
            M.Sound.Play("correct");
            _mood = "happy";
            _answerPop = Time;
            string[] cheers = { "GREAT!", "SUPER!", "AWESOME!", "NICE HOP!", "YES!", "BRILLIANT!" };
            _cheer = took <= StageRun.SpeedySeconds ? "SPEEDY!" : cheers[M.Rng.Next(cheers.Length)];
            if (_run.Streak >= 3) _cheer = $"COMBO x{_run.Streak}!";
            Fx.Confetti(spot, 24);
            Fx.Pop("+2", spot + new Vector2(0, -26), Gfx.Gold, 1.1);
            if (_stage.Boss)
            {
                _bossHp--;
                _zapAt = Time;
                _bossHitAt = Time + 0.25;
                M.Sound.Play("boss_hit");
                if (_bossHp <= 0)
                {
                    _bossDown = true;
                    M.Sound.Play("boss_down");
                }
            }
        }
        else
        {
            M.Sound.Play("wrong");
            _mood = "sad";
            _shake = 5;
            _ghostHops = HopLine.NeatHops(_line.Problem.Subtrahend);
            _ghostStart = _line.Problem.Minuend;
            var p = _line.Problem;
            Say(_lastOutcome == StageRun.Outcome.TryAgainLater
                ? $"Oops! You hopped back {_line.Taken}, not {p.Subtrahend}.\n{p.Minuend} - {p.Subtrahend} = {p.Answer}. You'll get it again later!"
                : $"{p.Minuend} - {p.Subtrahend} = {p.Answer}. Hop back {p.Subtrahend} from {p.Minuend}!", Gfx.Orange);
        }
    }

    private void Next()
    {
        if (_run.Over)
        {
            SetState(State.Outro);
            M.Sound.Play("victory");
            M.Sound.StopMusic();
            Fx.Confetti(new Vector2(128, 90), 60);
            return;
        }
        NewLine();
        _message = "";
        SetState(State.Solving);
        M.Sound.Play("whoosh", 1.5f, -8);
        if (_run.CurrentIsRetry) Say("Let's try this one again!", Gfx.Cyan);
    }

    private bool _finished;

    private void Finish()
    {
        if (_finished) return;
        _finished = true;
        var result = _run.Result();
        int levels = P.Record(result);
        P.LastStop = WorldArt.StopOf(_stage.Number);
        M.Save();
        M.Go(new ResultsScreen(_stage, result, levels));
    }

    // ---------------------------------------------------------------- drawing

    protected override void Paint(CanvasItem ci)
    {
        var shake = new Vector2(_shake > 0 ? (float)Math.Round(Math.Sin(Time * 60) * _shake * 0.5) : 0, 0);
        DrawHud(ci);
        DrawProblem(ci);
        if (_stage.TwoDigit) DrawMiniMap(ci);
        if (_stage.Boss) DrawBoss(ci);

        // A dark track band behind the line so the numbers stay readable over the floor.
        Gfx.Rect(ci, 0, 118, Gfx.W, 72, new Color(0.02f, 0.02f, 0.08f, 0.55f));
        Gfx.Rect(ci, 0, 118, Gfx.W, 1, new Color(0.4f, 0.45f, 0.9f, 0.5f));
        Gfx.Rect(ci, 0, 189, Gfx.W, 1, new Color(0.4f, 0.45f, 0.9f, 0.5f));

        DrawLine(ci, shake);
        DrawArcs(ci, shake);
        DrawFlags(ci, shake);
        DrawBeep(ci, shake);
    }

    protected override void PaintTop(CanvasItem ci)
    {
        DrawHelp(ci);
        switch (_state)
        {
            case State.Intro:
                DrawIntro(ci);
                break;
            case State.Feedback when _lastRight:
            {
                float t = (float)_stateTime;
                int scale = 2;
                var color = _cheer.StartsWith("COMBO") ? Gfx.Rainbow(Time) : Gfx.Gold;
                if (t < 1.1f) WavyText(ci, _cheer, 128, 84 - 8 * EaseOut(t * 3), color, scale, Time, 2, Gfx.Ink);
                break;
            }
            case State.Paused:
                Gfx.Rect(ci, 0, 0, Gfx.W, Gfx.H, new Color(0, 0, 0, 0.5f));
                Gfx.Window(ci, new Rect2(64, 76, 128, 64));
                Gfx.TextCentered(ci, "PAUSED", 128, 84, Gfx.Gold, 2);
                Gfx.Help(ci, "{A} Keep going", 82, 108, Gfx.White);
                Gfx.Help(ci, "{B} Back to map", 82, 122, Gfx.White);
                break;
            case State.Outro:
            {
                float t = (float)_stateTime;
                Gfx.Rect(ci, 0, 64, Gfx.W, 48 * EaseOut(t * 3), new Color(0, 0, 0.1f, 0.6f));
                if (t > 0.1f) WavyText(ci, "STAGE CLEAR!", 128, 74, Gfx.Rainbow(Time), 2, Time, 3, Gfx.Ink);
                if (t > 0.5f) Gfx.TextCentered(ci, $"Time {StageRun.Format(_run.Seconds)}", 128, 98, Gfx.White);
                break;
            }
        }
    }

    private void DrawHud(CanvasItem ci)
    {
        Gfx.Rect(ci, 0, 0, Gfx.W, 12, new Color(0.02f, 0.02f, 0.1f, 0.7f));
        Gfx.Text(ci, $"{_stage.Number} {_stage.Name.ToUpperInvariant()}", new Vector2(3, 1), Gfx.Cyan);
        bool late = _run.Seconds > _run.ParSeconds;
        string clock = StageRun.Format(_run.Seconds);
        Gfx.TextCentered(ci, clock, 150, 1, late ? Gfx.Grey : Gfx.White);
        // A little clock bar: green until the speed-star time runs out.
        float share = (float)Math.Min(1, _run.Seconds / _run.ParSeconds);
        Gfx.Rect(ci, 164, 4, 30, 4, new Color(0, 0, 0, 0.6f));
        Gfx.Rect(ci, 164, 4, 30 * (1 - share), 4, late ? Gfx.Red : share > 0.75f ? Gfx.Gold : Gfx.Green);
        Sprites.Draw(ci, Sprites.Get("bolt"), new Vector2(214, 2));
        Gfx.TextRight(ci, P.Bolts.ToString(), 253, 1, Gfx.Gold);
    }

    private void DrawProblem(CanvasItem ci)
    {
        var box = new Rect2(40, 16, 176, 36);
        Gfx.Window(ci, box);
        var p = _line.Problem;
        bool showAnswer = _state == State.Feedback || (_state == State.Outro);
        string a = p.Minuend.ToString(), b = p.Subtrahend.ToString();
        string c = showAnswer ? p.Answer.ToString() : "?";
        const int s = 2;
        string mid1 = " - ", mid2 = " = ";
        float w = Gfx.Measure(a + mid1 + b + mid2 + c, s);
        float x = 128 - w / 2, y = 24;
        var green = _line.Phase != HopPhase.PlaceStart || _state != State.Solving ? Gfx.Green : Gfx.White;
        Gfx.Text(ci, a, new Vector2(x, y), green, s);
        x += Gfx.Measure(a + " ", s) + 2;
        Gfx.Text(ci, "-", new Vector2(x, y), Gfx.White, s);
        x += Gfx.Measure("- ", s) + 2;
        Gfx.Text(ci, b, new Vector2(x, y), Gfx.Cyan, s);
        x += Gfx.Measure(b + " ", s) + 2;
        Gfx.Text(ci, "=", new Vector2(x, y), Gfx.White, s);
        x += Gfx.Measure("= ", s) + 2;
        if (showAnswer && _lastRight)
        {
            float pop = Pop((float)(Time - _answerPop) * 3);
            int sc = pop > 1.15f ? 3 : 2;
            Gfx.Text(ci, c, new Vector2(x, y - (sc - 2) * 5), Gfx.Orange, sc);
        }
        else if (showAnswer)
        {
            Gfx.Text(ci, c, new Vector2(x, y), Gfx.Orange, s);
        }
        else
        {
            bool blink = (Time % 1.0) < 0.6;
            Gfx.Text(ci, c, new Vector2(x, y), blink ? Gfx.Gold : Gfx.Dim, s);
        }

        // Pips: one per problem, filling in as they are answered.
        int n = _run.Total;
        float px = 128 - (n * 8 - 2) / 2f;
        for (int i = 0; i < n; i++)
        {
            var col = i < _pips.Count
                ? _pips[i] switch { 1 => Gfx.Green, 2 => Gfx.Orange, _ => Gfx.Red }
                : i == _pips.Count && (Time % 0.8) < 0.5 ? Gfx.White : Gfx.Dim;
            Gfx.Rect(ci, px + i * 8, 55, 6, 4, new Color(0, 0, 0, 0.6f));
            Gfx.Rect(ci, px + i * 8, 54, 6, 4, col);
        }
        if (_run.Streak >= 2 && _state != State.Intro)
            Gfx.TextCentered(ci, $"{_run.Streak} in a row!", 128, 60, Gfx.Rainbow(Time));
    }

    private void DrawMiniMap(CanvasItem ci)
    {
        // The whole 0-100 line at a glance, with the part on screen boxed.
        float x0 = 28, w = 200, y = 76;
        float Mx(float n) => Mathf.Floor(x0 + n / _stage.LineMax * w);
        Gfx.Rect(ci, x0 - 2, y - 3, w + 5, 9, new Color(0, 0, 0.1f, 0.6f));
        Gfx.Rect(ci, x0, y, w + 1, 1, Gfx.Grey);
        for (int n = 0; n <= _stage.LineMax; n += 10)
        {
            Gfx.Rect(ci, Mx(n), y - 2, 1, 5, n % 50 == 0 ? Gfx.Gold : Gfx.Grey);
        }
        float half = (Gfx.W / 2f) / _unit;
        float a = Mx(Math.Max(0, _view - half)), b = Mx(Math.Min(_stage.LineMax, _view + half));
        Gfx.Rect(ci, a, y - 3, b - a, 1, Gfx.White);
        Gfx.Rect(ci, a, y + 3, b - a, 1, Gfx.White);
        if (_line.Phase != HopPhase.PlaceStart || _state != State.Solving)
        {
            Gfx.Rect(ci, Mx(_line.Land), y, Math.Max(1, Mx(_line.Start) - Mx(_line.Land)), 1, Gfx.Cyan);
            Gfx.Rect(ci, Mx(_line.Land) - 1, y - 1, 3, 3, Gfx.Orange);
        }
        Gfx.Rect(ci, Mx(_line.Start) - 1, y - 1, 3, 3, Gfx.Green);
        Gfx.Tiny(ci, "0", x0, y + 4, Gfx.Grey);
        Gfx.Tiny(ci, _stage.LineMax.ToString(), x0 + w + 8, y + 4, Gfx.Grey);
    }

    private (int Lo, int Hi) VisibleRange()
    {
        float half = Gfx.W / 2f / _unit + 1;
        return (Math.Max(0, (int)Mathf.Floor(_view - half)), Math.Min(_stage.LineMax, (int)Mathf.Ceil(_view + half)));
    }

    private void DrawLine(CanvasItem ci, Vector2 shake)
    {
        var (lo, hi) = VisibleRange();
        float left = Math.Max(-2, X(0) - 6) + shake.X, right = Math.Min(Gfx.W + 2, X(_stage.LineMax) + 7) + shake.X;
        // The bar: white with a dark outline.
        Gfx.Rect(ci, left - 1, LineY - 2, right - left + 2, 4, Gfx.Ink);
        Gfx.Rect(ci, left, LineY - 1, right - left, 2, Gfx.White);
        // Arrowheads at the ends: the line goes on.
        foreach (var (ex, dir) in new[] { (X(0) - 6, -1), (X(_stage.LineMax) + 6, 1) })
        {
            float ax = ex + shake.X;
            if (ax < -4 || ax > Gfx.W + 4) continue;
            for (int k = 0; k < 4; k++) Gfx.Rect(ci, ax - dir * k, LineY - k, 1, 2 * k + 1, Gfx.White);
        }

        bool every = !_stage.TwoDigit || P.Owns("specs");
        bool hint = Time < _hintUntil && _line.Phase == HopPhase.PlaceStart;
        for (int n = lo; n <= hi; n++)
        {
            float x = X(n) + shake.X;
            int len = n % 10 == 0 ? 7 : n % 5 == 0 ? 5 : 3;
            var tick = n % 10 == 0 ? Gfx.Gold : Gfx.White;
            Gfx.Rect(ci, x, LineY - len + 1, 1, len * 2 - 1, Gfx.Ink);
            Gfx.Rect(ci, x, LineY - len + 2, 1, len * 2 - 3, tick);

            bool near = Math.Abs(n - _line.Active) <= (_unit < 9 ? 0 : 2);
            bool flagged = n == _line.Start || (n == _line.Land && _line.Phase != HopPhase.PlaceStart);
            bool label = _unit < 7 ? flagged || (n % 10 == 0 && Math.Abs(n - _line.Start) > 2 && Math.Abs(n - _line.Land) > 2)
                : _unit < 10 ? flagged || (n % 5 == 0 && Math.Abs(n - _line.Start) > 1 && Math.Abs(n - _line.Land) > 1)
                : every || n % 5 == 0 || near;
            if (hint && n == _line.Problem.Minuend)
            {
                float glow = (float)(0.5 + 0.5 * Math.Sin(Time * 12));
                Gfx.Rect(ci, x - 6, LineY - 14, 13, 28, new Color(1, 0.9f, 0.3f, 0.25f + 0.25f * glow));
            }
            if (!label) continue;
            var col = n % 10 == 0 ? Gfx.Gold : near || every ? Gfx.White : Gfx.Grey;
            if (n == _line.Start && _line.Phase != HopPhase.PlaceStart) col = Gfx.Green;
            if (n == _line.Land && _line.Phase != HopPhase.PlaceStart && _line.Hops.Count > 0) col = Gfx.Orange;
            Gfx.Tiny(ci, n.ToString(), x + 0.5f, LineY + 8, col);
        }
    }

    private void DrawArcs(CanvasItem ci, Vector2 shake)
    {
        bool placing = _line.Phase == HopPhase.PlaceStart && _state == State.Solving;
        if (!placing)
        {
            // The planned chain: solid once it's been hopped, bright while it's being built.
            DrawChain(ci, _line.Start, _line.Hops, 1f, shake, labels: true);
        }
        // The right way, shown after a wrong answer or as a hint.
        bool hintHops = Time < _hintUntil && _line.Phase == HopPhase.Hop;
        if (_ghostHops is not null || hintHops)
        {
            var hops = _ghostHops ?? HopLine.NeatHops(_line.Problem.Subtrahend);
            int start = _ghostHops is not null ? _ghostStart : _line.Start;
            DrawChain(ci, start, hops, 0.95f, shake, labels: _ghostHops is not null, ghost: true);
            if (_ghostHops is not null)
            {
                // A green bubble over the right answer, pointing down at it.
                int answer = start - hops.Sum();
                float gx = X(answer) + shake.X;
                float bob = Mathf.Round((float)Math.Sin(Time * 6) * 2);
                string text = answer.ToString();
                float w = Gfx.Measure(text) + 8;
                float by = LineY - 78 + bob;
                Gfx.Rect(ci, gx, by + 12, 1, LineY - 4 - (by + 12), new Color(0.4f, 1, 0.5f, 0.7f));
                Gfx.Rect(ci, gx - w / 2, by, w, 12, Gfx.Ink);
                Gfx.Rect(ci, gx - w / 2 + 1, by + 1, w - 2, 10, new Color("2a9a3a"));
                Gfx.Text(ci, text, new Vector2(gx - w / 2 + 4, by + 1), Gfx.White);
            }
        }
    }

    private void DrawChain(CanvasItem ci, int start, IReadOnlyList<int> hops, float alpha, Vector2 shake, bool labels, bool ghost = false)
    {
        float at = start;
        for (int i = 0; i < hops.Count; i++)
        {
            int size = hops[i];
            bool done = _state == State.Hopping && !ghost ? i < _hopIndex : true;
            var col = ghost ? Gfx.Green : size == 10 ? Gfx.Gold : Gfx.Cyan;
            if (!done) col = col.Darkened(0.35f);
            col.A = alpha;
            float h = size == 10 ? 30 : _unit >= 20 ? 12 : 9;
            Arc(ci, X(at) + shake.X, X(at - size) + shake.X, ghost ? h + 12 : h, col, dotted: ghost);
            at -= size;
        }
        if (!labels) return;
        foreach (var run in HopLine.RunsOf(start, hops))
        {
            var col = ghost ? Gfx.Green : run.Size == 10 ? Gfx.Gold : Gfx.Cyan;
            col.A = alpha;
            if (run.Size == 10)
            {
                for (int k = 0; k < run.Count; k++)
                {
                    float mid = (X(run.From - 10 * k) + X(run.From - 10 * (k + 1))) / 2 + shake.X;
                    Gfx.Tiny(ci, "-10", mid, LineY - (ghost ? 52 : 40), col);
                }
            }
            else
            {
                float mid = (X(run.From) + X(run.To)) / 2 + shake.X;
                float y = LineY - (_unit >= 20 ? 21 : 18) - (ghost ? 12 : 0);
                Gfx.Tiny(ci, "-" + run.Count, mid, y, col);
            }
        }
    }

    /// <summary>A hop: a parabola from a down to b, two pixels thick, with an arrowhead where it lands.</summary>
    private static void Arc(CanvasItem ci, float a, float b, float height, Color color, bool dotted)
    {
        float span = Math.Abs(a - b);
        if (span < 1) return;
        float baseY = LineY - 3;
        var shadow = new Color(0, 0, 0, color.A * 0.6f);
        int steps = (int)span;
        for (int i = 0; i <= steps; i++)
        {
            if (dotted && i % 4 == 3) continue;
            float t = i / (float)steps;
            float x = Mathf.Lerp(a, b, t);
            float y = baseY - height * 4 * t * (1 - t);
            Gfx.Rect(ci, x, y + 1, 1, 2, shadow);
            Gfx.Rect(ci, x, y - 1, 1, 2, color);
        }
        // Arrowhead pointing down at the landing end.
        float dir = Math.Sign(a - b);
        Gfx.Rect(ci, b - 1, baseY - 1, 3, 1, color);
        Gfx.Rect(ci, b, baseY, 1, 1, color);
        Gfx.Rect(ci, b + dir * 1, baseY - 3, 1, 2, color);
    }

    private void DrawFlags(CanvasItem ci, Vector2 shake)
    {
        bool placing = _line.Phase == HopPhase.PlaceStart && _state == State.Solving;
        float pulse = (Time % 0.6) < 0.35 ? 1 : 0;
        // Tags under the labels: green Start, orange Land.
        float sx = X(_line.Start) + shake.X, lx = X(_line.Land) + shake.X;
        string st = _line.Start.ToString(), lt = _line.Land.ToString();
        float sw = Gfx.Measure(st) + 8, lw = Gfx.Measure(lt) + 8;
        float sTag = sx, lTag = lx;
        bool both = !placing && _line.Hops.Count > 0;
        if (both && sTag - lTag < (sw + lw) / 2 + 2)
        {
            float mid = (sTag + lTag) / 2;
            sTag = mid + (sw + lw) / 4 + 1;
            lTag = mid - (sw + lw) / 4 - 1;
        }
        Tag(ci, sTag, sx, st, new Color("2a9a3a"), placing && pulse > 0);
        if (both) Tag(ci, lTag, lx, lt, new Color("c86a18"), !placing && _state == State.Solving && pulse > 0);

        if (both) Sprites.DrawFoot(ci, Sprites.Get("flag_land"), new Vector2(lx + 3, LineY - 1));
        if (!placing) Sprites.DrawFoot(ci, Sprites.Get("flag_start"), new Vector2(sx + 3 + 10, LineY - 1), tint: new Color(1, 1, 1, 0.9f));
    }

    private static void Tag(CanvasItem ci, float centerX, float pointX, string text, Color color, bool bright)
    {
        float w = Gfx.Measure(text) + 8;
        float x = Mathf.Floor(centerX - w / 2), y = 176;
        var fill = bright ? color.Lightened(0.3f) : color;
        // Pointer up to the line.
        Gfx.Rect(ci, pointX, LineY + 14, 1, y - LineY - 14, fill);
        Gfx.Rect(ci, x, y, w, 12, Gfx.Ink);
        Gfx.Rect(ci, x + 1, y + 1, w - 2, 10, fill);
        Gfx.Rect(ci, x + 1, y + 1, w - 2, 1, fill.Lightened(0.4f));
        Gfx.Text(ci, text, new Vector2(x + 4, y + 1), Gfx.White);
    }

    private void DrawBeep(CanvasItem ci, Vector2 shake)
    {
        var foot = new Vector2(X(_beepAt) + shake.X, LineY - 2 - _beepLift);
        if (_state == State.Outro)
        {
            float hop = Math.Abs(Mathf.Sin((float)Time * 7)) * 10;
            foot.Y -= hop;
        }
        BeepArt.Draw(ci, foot, _outfit, Time, 1, _mood ?? (_state == State.Outro ? "happy" : null), _squash,
            flip: _state == State.Hopping || (_line.Phase == HopPhase.Hop && _state == State.Solving));
    }

    private void DrawBoss(CanvasItem ci)
    {
        if (_bossDown && Time - _bossHitAt > 1.2) return;
        float bob = Mathf.Sin((float)Time * 2.5f) * 3;
        var at = new Vector2(232, 104 + bob);
        bool hit = Time - _bossHitAt is > 0 and < 0.35;
        float enter = _state == State.Intro ? EaseOut((float)_stateTime / 1.5f) : 1;
        at.Y -= (1 - enter) * 120;
        var tint = hit ? ((Time * 30) % 2 < 1 ? new Color(3, 3, 3) : new Color(1, 0.4f, 0.4f)) : Colors.White;
        if (_bossDown)
        {
            float t = (float)(Time - _bossHitAt);
            tint = new Color(1, 1, 1, 1 - t / 1.2f);
            at.Y += t * t * 60;
            if ((int)(t * 20) % 3 == 0) Fx.Burst(Particles.Kind.Dot, at + new Vector2(M.Rng.Next(-10, 10), -10), 3, Gfx.Orange, 40, 40, 0.4);
        }
        Sprites.DrawFoot(ci, Sprites.Get("glitch"), at + new Vector2(hit ? M.Rng.Next(-2, 3) : 0, 0), 1, tint: tint);
        // Glitchy scanline slices across it now and then.
        if ((Time % 1.7) < 0.08)
            Gfx.Rect(ci, at.X - 14, at.Y - 12, 28, 2, new Color(0.7f, 0.3f, 1, 0.8f));
        // Health bar.
        float share = _bossHp / (float)_run.Total;
        Gfx.Rect(ci, 216, 108, 32, 4, Gfx.Ink);
        Gfx.Rect(ci, 217, 109, 30 * share, 2, share > 0.3f ? Gfx.Pink : Gfx.Red);

        // Beep's zap: a bolt of light from Beep up to the Glitch.
        double z = Time - _zapAt;
        if (z is >= 0 and < 0.25)
        {
            var from = new Vector2(X(_beepAt), LineY - 16);
            var to = at + new Vector2(0, -10);
            for (int i = 0; i < 12; i++)
            {
                float t = i / 12f;
                var p = from.Lerp(to, t) + new Vector2(M.Rng.Next(-3, 4), M.Rng.Next(-3, 4));
                Gfx.Rect(ci, p.X, p.Y, 2, 2, i % 2 == 0 ? Gfx.Cyan : Colors.White);
            }
        }
    }

    private void DrawHelp(CanvasItem ci)
    {
        var box = new Rect2(2, 192, 252, 31);
        Gfx.Window(ci, box, Gfx.Look.Blue, 0.92f);
        string line1, line2;
        var p = _line.Problem;
        bool hints = _run.HintsLeft > 0;
        string hint = hints ? $"  {{Y}}Hint" : "";
        switch (_state)
        {
            case State.Intro:
                line1 = _stage.Boss ? "Beat the Glitch with right answers!" : "Get ready to hop!";
                line2 = "{A} Start";
                break;
            case State.Solving when _line.Phase == HopPhase.PlaceStart:
                line1 = $"Put the green flag on {p.Minuend}!";
                line2 = (_stage.TwoDigit ? "{Left}{Right}1  {LB}{RB}10  {A}Lock" : "{Left}{Right}Move  {A}Lock it") + hint;
                break;
            case State.Solving:
                line1 = $"Hop back {p.Subtrahend}!   Hopped: {_line.Taken}";
                line2 = (_stage.TwoDigit ? "{Left}-1 {LB}-10 {Right}{RB}Undo {A}GO!" : "{Left}Hop  {Right}Undo  {A}GO!") + hint;
                break;
            case State.Hopping:
                line1 = "Count the hops!";
                line2 = $"{p.Minuend} back {_hopIndex} ...";
                if (_hopIndex > 0) line2 = $"{p.Minuend} back {_line.Hops.Take(_hopIndex).Sum()} is {(int)_hopPath[_hopIndex - 1].To}";
                break;
            case State.Feedback when _lastRight:
                line1 = $"{p.Minuend} - {p.Subtrahend} = {p.Answer}";
                line2 = "Hop " + (p.Subtrahend >= 10 ? $"back {p.Subtrahend / 10} ten{(p.Subtrahend >= 20 ? "s" : "")}" + (p.Subtrahend % 10 > 0 ? $" and {p.Subtrahend % 10}" : "") : $"back {p.Subtrahend}") + $", land on {p.Answer}!";
                break;
            case State.Feedback:
                line1 = "";
                line2 = "";
                break;
            default:
                line1 = "";
                line2 = "";
                break;
        }
        // Fresh messages (wrong start, hints, oops) take over for a few seconds.
        bool fresh = Time - _messageTime < 3.2 || (_state == State.Feedback && !_lastRight);
        if (fresh && _message.Length > 0)
        {
            var lines = _message.Split('\n');
            if (lines.Length > 1)
            {
                Gfx.Text(ci, lines[0], new Vector2(8, 196), _messageColor);
                Gfx.Text(ci, lines[1], new Vector2(8, 208), Gfx.White);
                if (_state == State.Feedback) Gfx.Help(ci, "{A}", 250 - Gfx.HelpWidth("{A}"), 209, Gfx.White);
                return;
            }
            line1 = _message;
            if (_state == State.Feedback) line2 = "{A} OK";
            Gfx.Text(ci, line1, new Vector2(8, 196), _messageColor);
        }
        else Gfx.Text(ci, line1, new Vector2(8, 196), Gfx.White);
        Gfx.Help(ci, line2, 8, 209, Gfx.Grey);
    }

    private void DrawIntro(CanvasItem ci)
    {
        float t = (float)_stateTime;
        float slide = EaseOut(t * 2.5f);
        Gfx.Rect(ci, 0, 80, Gfx.W, 50, new Color(0, 0, 0.12f, 0.75f * slide));
        float x = 128 + (1 - slide) * 260;
        WavyText(ci, _stage.Boss ? "BOSS STAGE!" : $"STAGE {_stage.Number}", x, 84, _stage.Boss ? Gfx.Red : Gfx.Gold, 2, Time, 2, Gfx.Ink);
        Gfx.TextCentered(ci, _stage.Name, 128 - (1 - slide) * 260, 106, Gfx.White);
        Gfx.TextCentered(ci, _stage.Blurb, 128 + (1 - slide) * 260, 117, Gfx.Cyan);
        if (t > 1.4f && !_stage.Boss || t > 2.0f) Gfx.TextCentered(ci, "GO!", 128, 64, Gfx.Green, 2);
    }
}

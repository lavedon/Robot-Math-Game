using Godot;

namespace MathRobot.Game.Snes;

/// <summary>The buttons the game reads, laid out like an Xbox controller.</summary>
public enum Btn { Left, Right, Up, Down, A, B, X, Y, LB, RB, Start, Select }

/// <summary>
/// Polled input: every Xbox controller plugged in, plus the keyboard, merged into one pad. <see cref="Repeat"/> fires
/// on the press and then again and again while held (faster the longer it's held), which is how the number line's
/// flags slide.
/// </summary>
public sealed class Pad
{
    private const int Count = 12;
    private const float StickDeadzone = 0.5f;

    private readonly bool[] _held = new bool[Count];
    private readonly bool[] _was = new bool[Count];
    private readonly double[] _heldFor = new double[Count];
    private readonly double[] _nextRepeat = new double[Count];
    private readonly bool[] _repeatNow = new bool[Count];
    // A button held when the pad was cleared stays dead until it is let go.
    private readonly bool[] _dead = new bool[Count];

    /// <summary>Seconds before a held button starts repeating.</summary>
    public double RepeatDelay { get; set; } = 0.3;

    /// <summary>Seconds between repeats (it halves after a second of holding).</summary>
    public double RepeatEvery { get; set; } = 0.15;

    /// <summary>While true (screen transitions), nothing reads as pressed.</summary>
    public bool Locked { get; set; }

    /// <summary>A button press that came from a test script instead of the hardware, read on the next poll.</summary>
    private readonly HashSet<Btn> _injected = new();

    public void Inject(Btn b) => _injected.Add(b);

    public void Poll(double dt)
    {
        var now = new bool[Count];
        Read(now);
        foreach (var b in _injected) now[(int)b] = true;
        _injected.Clear();
        for (int i = 0; i < Count; i++)
        {
            _was[i] = _held[i];
            if (_dead[i] && !now[i]) _dead[i] = false;
            _held[i] = now[i] && !_dead[i];
            _repeatNow[i] = false;
            if (!_held[i])
            {
                _heldFor[i] = 0;
                continue;
            }
            if (!_was[i])
            {
                _heldFor[i] = 0;
                _nextRepeat[i] = RepeatDelay;
                _repeatNow[i] = true;
                continue;
            }
            _heldFor[i] += dt;
            if (_heldFor[i] >= _nextRepeat[i])
            {
                _repeatNow[i] = true;
                double every = _heldFor[i] > RepeatDelay + 1.0 ? RepeatEvery * 0.5 : RepeatEvery;
                _nextRepeat[i] += every;
            }
        }
    }

    /// <summary>Forgets every held button: they must be let go before they count again.</summary>
    public void Clear()
    {
        for (int i = 0; i < Count; i++)
        {
            if (_held[i]) _dead[i] = true;
            _held[i] = _was[i] = _repeatNow[i] = false;
        }
    }

    public bool Pressed(Btn b) => !Locked && _held[(int)b] && !_was[(int)b];
    public bool Held(Btn b) => !Locked && _held[(int)b];
    public bool Repeat(Btn b) => !Locked && _repeatNow[(int)b];

    /// <summary>True when the newest input came from a controller (to show Xbox button names).</summary>
    public static bool ControllerConnected => Input.GetConnectedJoypads().Count > 0;

    private static void Read(bool[] b)
    {
        void Key(Btn btn, params Key[] keys)
        {
            foreach (var k in keys)
                if (Input.IsPhysicalKeyPressed(k)) b[(int)btn] = true;
        }

        Key(Btn.Left, Godot.Key.Left, Godot.Key.A);
        Key(Btn.Right, Godot.Key.Right, Godot.Key.D);
        Key(Btn.Up, Godot.Key.Up, Godot.Key.W);
        Key(Btn.Down, Godot.Key.Down, Godot.Key.S);
        Key(Btn.A, Godot.Key.Space, Godot.Key.Enter, Godot.Key.Z, Godot.Key.KpEnter);
        Key(Btn.B, Godot.Key.Backspace, Godot.Key.X, Godot.Key.Escape);
        Key(Btn.X, Godot.Key.C);
        Key(Btn.Y, Godot.Key.H, Godot.Key.V);
        Key(Btn.LB, Godot.Key.Q, Godot.Key.Pagedown);
        Key(Btn.RB, Godot.Key.E, Godot.Key.Pageup);
        Key(Btn.Start, Godot.Key.Enter);
        Key(Btn.Select, Godot.Key.Tab);

        foreach (int pad in Input.GetConnectedJoypads())
        {
            void Joy(Btn btn, JoyButton jb)
            {
                if (Input.IsJoyButtonPressed(pad, jb)) b[(int)btn] = true;
            }

            Joy(Btn.Left, JoyButton.DpadLeft);
            Joy(Btn.Right, JoyButton.DpadRight);
            Joy(Btn.Up, JoyButton.DpadUp);
            Joy(Btn.Down, JoyButton.DpadDown);
            Joy(Btn.A, JoyButton.A);
            Joy(Btn.B, JoyButton.B);
            Joy(Btn.X, JoyButton.X);
            Joy(Btn.Y, JoyButton.Y);
            Joy(Btn.LB, JoyButton.LeftShoulder);
            Joy(Btn.RB, JoyButton.RightShoulder);
            Joy(Btn.Start, JoyButton.Start);
            Joy(Btn.Select, JoyButton.Back);

            float x = Input.GetJoyAxis(pad, JoyAxis.LeftX), y = Input.GetJoyAxis(pad, JoyAxis.LeftY);
            if (x < -StickDeadzone) b[(int)Btn.Left] = true;
            if (x > StickDeadzone) b[(int)Btn.Right] = true;
            if (y < -StickDeadzone) b[(int)Btn.Up] = true;
            if (y > StickDeadzone) b[(int)Btn.Down] = true;
            // The triggers work as the bumpers: big hops of ten.
            if (Input.GetJoyAxis(pad, JoyAxis.TriggerLeft) > 0.5f) b[(int)Btn.LB] = true;
            if (Input.GetJoyAxis(pad, JoyAxis.TriggerRight) > 0.5f) b[(int)Btn.RB] = true;
        }
    }
}

namespace MathRobot.Core;

/// <summary>Where the player is in solving one problem on the number line.</summary>
public enum HopPhase
{
    /// <summary>Slide the green Start flag to the first number, then lock it in.</summary>
    PlaceStart,
    /// <summary>Stretch the hops back from the Start; the orange Land flag follows.</summary>
    Hop,
    /// <summary>GO was pressed: the answer is in.</summary>
    Done,
}

/// <summary>Why a press did nothing, so the game can say something helpful.</summary>
public enum HopRefusal { None, OffTheLine, WrongStart, NoHops }

/// <summary>
/// One problem on the number line. The player first slides the Start flag to the first number (±1 or ±10 a press)
/// and locks it; then each press hops Beep back 1 or 10, building a chain of Hops whose far end is the Land flag.
/// The chain's total is the number taken away, so when it matches the problem, Land sits on the answer.
/// </summary>
public sealed class HopLine
{
    private readonly List<int> _hops = new();

    public HopLine(Problem problem, int lineMax, int startAt = 0)
    {
        Problem = problem;
        LineMax = lineMax;
        Start = Math.Clamp(startAt, 0, lineMax);
    }

    public Problem Problem { get; }
    public int LineMax { get; }
    public HopPhase Phase { get; private set; } = HopPhase.PlaceStart;

    /// <summary>Where the Start flag stands.</summary>
    public int Start { get; private set; }

    /// <summary>The hops back from Start, in order: each is 1 or 10.</summary>
    public IReadOnlyList<int> Hops => _hops;

    /// <summary>How far back the hops reach in all.</summary>
    public int Taken => _hops.Sum();

    /// <summary>Where the Land flag stands: the end of the last hop.</summary>
    public int Land => Start - Taken;

    /// <summary>The flag the player is moving right now.</summary>
    public int Active => Phase == HopPhase.PlaceStart ? Start : Land;

    /// <summary>Slides the Start flag (only before it is locked). False if it would leave the line.</summary>
    public bool MoveStart(int delta)
    {
        if (Phase != HopPhase.PlaceStart) return false;
        int to = Start + delta;
        if (to < 0 || to > LineMax) return false;
        Start = to;
        return true;
    }

    /// <summary>Locks the Start flag if it stands on the first number of the problem.</summary>
    public HopRefusal LockStart()
    {
        if (Phase != HopPhase.PlaceStart) return HopRefusal.None;
        if (Start != Problem.Minuend) return HopRefusal.WrongStart;
        Phase = HopPhase.Hop;
        return HopRefusal.None;
    }

    /// <summary>Adds a hop back of 1 or 10. False if it would hop off the left end of the line.</summary>
    public bool HopBack(int size)
    {
        if (Phase != HopPhase.Hop || size <= 0 || Land - size < 0) return false;
        _hops.Add(size);
        return true;
    }

    /// <summary>
    /// Shrinks the chain by <paramref name="size"/>: removes the last hop of that size, or for a 10 with only
    /// small hops at the end, up to ten 1-hops. False when there are no hops to take back.
    /// </summary>
    public bool Shrink(int size)
    {
        if (Phase != HopPhase.Hop || _hops.Count == 0) return false;
        int last = _hops.FindLastIndex(h => h == size);
        if (last >= 0)
        {
            _hops.RemoveAt(last);
            return true;
        }
        // A 10 over a tail of 1-hops: take back up to ten of them.
        for (int i = 0; i < size && _hops.Count > 0; i++) _hops.RemoveAt(_hops.Count - 1);
        return true;
    }

    /// <summary>Takes back the most recent hop (or unlocks the Start flag when there are none).</summary>
    public bool Undo()
    {
        if (Phase != HopPhase.Hop) return false;
        if (_hops.Count == 0) Phase = HopPhase.PlaceStart;
        else _hops.RemoveAt(_hops.Count - 1);
        return true;
    }

    /// <summary>Sends Beep hopping. Returns NoHops when there is nothing to hop yet.</summary>
    public HopRefusal Go()
    {
        if (Phase != HopPhase.Hop) return HopRefusal.None;
        if (_hops.Count == 0) return HopRefusal.NoHops;
        Phase = HopPhase.Done;
        return HopRefusal.None;
    }

    public bool Correct => Phase == HopPhase.Done && Taken == Problem.Subtrahend;

    /// <summary>
    /// The hops as runs of equal size for labelling: 10, 10, 1, 1, 1 → (10, 2 hops), (1, 3 hops). Each run reports
    /// where it starts and how much it takes away.
    /// </summary>
    public IReadOnlyList<HopRun> Runs() => RunsOf(Start, _hops);

    public static IReadOnlyList<HopRun> RunsOf(int start, IReadOnlyList<int> hops)
    {
        var runs = new List<HopRun>();
        int at = start;
        int i = 0;
        while (i < hops.Count)
        {
            int size = hops[i], count = 0;
            int from = at;
            while (i < hops.Count && hops[i] == size)
            {
                at -= size;
                count++;
                i++;
            }
            runs.Add(new HopRun(from, size, count));
        }
        return runs;
    }

    /// <summary>The neat way to make the problem's hops: whole tens first, then ones (used by the hint).</summary>
    public static IReadOnlyList<int> NeatHops(int subtrahend)
    {
        var hops = new List<int>();
        for (int i = 0; i < subtrahend / 10; i++) hops.Add(10);
        for (int i = 0; i < subtrahend % 10; i++) hops.Add(1);
        return hops;
    }
}

/// <summary>A run of equal hops: from <see cref="From"/>, <see cref="Count"/> hops of <see cref="Size"/>.</summary>
public readonly record struct HopRun(int From, int Size, int Count)
{
    public int Total => Size * Count;
    public int To => From - Total;
}

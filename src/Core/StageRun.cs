namespace MathRobot.Core;

/// <summary>The perks a player brings into a Stage, from the Upgrades they own.</summary>
public sealed record Perks(int ExtraParSeconds = 0, double BoltBonus = 0, int ComboLevel = 0, int Hints = 0, bool Shield = false)
{
    public static readonly Perks None = new();
}

/// <summary>
/// One play of a Stage: its problems in order, the clock, mistakes and the streak. A problem answered wrong goes to
/// the back of the queue once; wrong a second time, Beep shows the answer and moves on.
/// </summary>
public sealed class StageRun
{
    /// <summary>Solving a problem within this many seconds earns a Speedy bonus Bolt.</summary>
    public const double SpeedySeconds = 8;

    private readonly Queue<(Problem Problem, bool Retry)> _queue;
    private readonly Perks _perks;

    public StageRun(Stage stage, IReadOnlyList<Problem> problems, Perks? perks = null)
    {
        Stage = stage;
        _perks = perks ?? Perks.None;
        _queue = new Queue<(Problem, bool)>(problems.Select(p => (p, false)));
        Total = problems.Count;
        HintsLeft = _perks.Hints;
    }

    public Stage Stage { get; }
    public int Total { get; }

    /// <summary>Problems solved (right first time or on the retry) or given up on.</summary>
    public int Finished { get; private set; }
    public int FirstTry { get; private set; }
    public int Mistakes { get; private set; }
    public int Speedy { get; private set; }
    public int Streak { get; private set; }
    public int BestStreak { get; private set; }
    public int HintsLeft { get; private set; }
    public double Seconds { get; private set; }
    public bool ShieldUsed { get; private set; }

    /// <summary>Bolts from the combo: from a streak of 3, each right answer pays the combo level extra.</summary>
    public int ComboBolts { get; private set; }

    public bool Over => _queue.Count == 0;
    public Problem Current => _queue.Peek().Problem;
    public bool CurrentIsRetry => _queue.Peek().Retry;

    /// <summary>The clock only runs while a problem is being solved.</summary>
    public void Tick(double seconds)
    {
        if (!Over) Seconds += seconds;
    }

    public bool UseHint()
    {
        if (HintsLeft <= 0) return false;
        HintsLeft--;
        return true;
    }

    /// <summary>What happened to an answer.</summary>
    public enum Outcome { Right, TryAgainLater, ShowAnswer }

    /// <summary>Records the answer to the current problem and moves the queue on.</summary>
    public Outcome Answer(bool correct, double secondsTaken)
    {
        var (problem, retry) = _queue.Dequeue();
        if (correct)
        {
            Finished++;
            if (!retry) FirstTry++;
            if (secondsTaken <= SpeedySeconds) Speedy++;
            Streak++;
            BestStreak = Math.Max(BestStreak, Streak);
            if (Streak >= 3) ComboBolts += _perks.ComboLevel;
            return Outcome.Right;
        }
        if (_perks.Shield && !ShieldUsed) ShieldUsed = true; // the Oops Shield takes the first slip
        else Mistakes++;
        Streak = 0;
        if (!retry)
        {
            _queue.Enqueue((problem, true));
            return Outcome.TryAgainLater;
        }
        Finished++;
        return Outcome.ShowAnswer;
    }

    public int ParSeconds => Stage.ParSeconds + _perks.ExtraParSeconds;

    public StageResult Result()
    {
        bool accurate = Mistakes == 0;
        bool fast = Seconds <= ParSeconds;
        int stars = 1 + (accurate ? 1 : 0) + (fast ? 1 : 0);
        var lines = new List<RewardLine>
        {
            new("Problems solved", $"{FirstTry} x 2", FirstTry * 2),
            new("Speedy answers", $"{Speedy} x 1", Speedy),
        };
        if (ComboBolts > 0) lines.Add(new("Combo bonus", $"best x{BestStreak}", ComboBolts));
        lines.Add(new("Stage clear", "", 5));
        lines.Add(new(accurate ? "No mistakes!" : $"Mistakes: {Mistakes}", "", accurate ? 5 : 0));
        lines.Add(new(fast ? "Beat the clock!" : "Clock", $"{Format(Seconds)} / {Format(ParSeconds)}", fast ? 5 : 0));
        if (Stage.Boss) lines.Add(new("Glitch defeated!", "", 20));
        int sum = lines.Sum(l => l.Bolts);
        int bonus = (int)Math.Round(sum * _perks.BoltBonus);
        if (bonus > 0) lines.Add(new("Bolt Magnet", $"+{(int)Math.Round(_perks.BoltBonus * 100)}%", bonus));
        int bolts = sum + bonus;
        int xp = FirstTry * 10 + (Finished - FirstTry) * 4 + stars * 15 + (Stage.Boss ? 50 : 0);
        return new StageResult(Stage.Number, stars, accurate, fast, Seconds, bolts, xp, lines);
    }

    public static string Format(double seconds)
    {
        int s = (int)Math.Floor(seconds);
        return $"{s / 60}:{s % 60:00}";
    }
}

public sealed record RewardLine(string Label, string Detail, int Bolts);

public sealed record StageResult(int Stage, int Stars, bool Accurate, bool Fast, double Seconds, int Bolts, int Xp,
    IReadOnlyList<RewardLine> Lines);

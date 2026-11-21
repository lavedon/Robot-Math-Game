namespace MathRobot.Core;

/// <summary>A subtraction problem: <see cref="Minuend"/> − <see cref="Subtrahend"/>.</summary>
public readonly record struct Problem(int Minuend, int Subtrahend)
{
    public int Answer => Minuend - Subtrahend;

    /// <summary>True when the ones digit of the start is smaller than the ones being taken away (e.g. 42 − 7).</summary>
    public bool CrossesTen => Minuend % 10 < Subtrahend % 10;

    public override string ToString() => $"{Minuend} - {Subtrahend}";
}

/// <summary>The kind of subtraction a Stage practises, easiest first.</summary>
public enum Skill
{
    /// <summary>Starts up to 10, take away 1 or 2.</summary>
    TakeOneOrTwo,
    /// <summary>Starts up to 10, take away up to the start.</summary>
    WithinTen,
    /// <summary>Starts 11-19, ones only, answer stays 10 or more (17 − 4).</summary>
    TeensNoCross,
    /// <summary>Starts 11-19, ones only, hopping back past 10 (13 − 5).</summary>
    TeensCrossTen,
    /// <summary>Two-digit start, take away whole tens (64 − 30).</summary>
    WholeTens,
    /// <summary>Two-digit minus one-digit, staying inside the same ten (47 − 3).</summary>
    TwoMinusOneEasy,
    /// <summary>Two-digit minus one-digit, crossing a ten (42 − 7).</summary>
    TwoMinusOneCross,
    /// <summary>Two-digit minus two-digit without crossing a ten (58 − 23).</summary>
    TwoMinusTwoEasy,
    /// <summary>Two-digit minus two-digit crossing a ten (52 − 17).</summary>
    TwoMinusTwoCross,
    /// <summary>A mix of everything: the boss stage.</summary>
    Mixed,
}

/// <summary>One stop on the World Map where Beep practises a <see cref="Skill"/>.</summary>
/// <param name="ParSeconds">Finish all problems within this many seconds for the speed Star.</param>
/// <param name="LineMax">The number line runs from 0 to this.</param>
public sealed record Stage(int Number, string Name, string Blurb, Skill Skill, int Problems, int ParSeconds, int LineMax, bool Boss = false)
{
    /// <summary>Two-digit stages: the line is too long to see all at once and every 10 matters.</summary>
    public bool TwoDigit => LineMax > 20;
}

public static class Stages
{
    public static readonly IReadOnlyList<Stage> All = new Stage[]
    {
        new(1, "Hop Hop", "Take away 1 or 2", Skill.TakeOneOrTwo, 6, 60, 10),
        new(2, "Ten Town", "Take away inside 10", Skill.WithinTen, 8, 90, 10),
        new(3, "Teen Trail", "Teens: stay above 10", Skill.TeensNoCross, 8, 100, 20),
        new(4, "Ten Bridge", "Teens: hop past 10", Skill.TeensCrossTen, 8, 110, 20),
        new(5, "Big Leaps", "Jump back whole tens", Skill.WholeTens, 8, 110, 100),
        new(6, "Gear Grove", "Big number, small hop", Skill.TwoMinusOneEasy, 8, 120, 100),
        new(7, "Spring Falls", "Small hops past a ten", Skill.TwoMinusOneCross, 8, 130, 100),
        new(8, "Circuit Cove", "Tens, then ones", Skill.TwoMinusTwoEasy, 8, 150, 100),
        new(9, "Volt Peak", "Tens, ones, past a ten", Skill.TwoMinusTwoCross, 8, 170, 100),
        new(10, "Glitch Core", "BOSS: the Glitch!", Skill.Mixed, 10, 210, 100, Boss: true),
    };

    public static Stage Get(int number) => All[number - 1];
}

/// <summary>Deals the problems of a Stage: every one fits its Skill, and none repeats within a Stage.</summary>
public static class ProblemDealer
{
    public static IReadOnlyList<Problem> Deal(Stage stage, Random random)
    {
        var dealt = new List<Problem>();
        var seen = new HashSet<Problem>();
        int guard = 0;
        while (dealt.Count < stage.Problems)
        {
            var skill = stage.Skill == Skill.Mixed ? MixedSkill(dealt.Count, random) : stage.Skill;
            var p = Make(skill, random);
            // Allow a repeat only if the skill has run out of fresh problems (TakeOneOrTwo has few).
            if (seen.Add(p) || ++guard > 500) dealt.Add(p);
        }
        return dealt;
    }

    // The boss mixes in the harder skills more as it goes.
    private static Skill MixedSkill(int index, Random random)
    {
        Skill[] pool = index < 3
            ? new[] { Skill.TeensCrossTen, Skill.WholeTens, Skill.TwoMinusOneEasy }
            : new[] { Skill.TwoMinusOneCross, Skill.TwoMinusTwoEasy, Skill.TwoMinusTwoCross, Skill.TeensCrossTen };
        return pool[random.Next(pool.Length)];
    }

    public static Problem Make(Skill skill, Random r)
    {
        switch (skill)
        {
            case Skill.TakeOneOrTwo:
            {
                int b = r.Next(1, 3);
                return new(r.Next(Math.Max(b, 2), 11), b);
            }
            case Skill.WithinTen:
            {
                int a = r.Next(3, 11);
                return new(a, r.Next(1, a + 1));
            }
            case Skill.TeensNoCross:
            {
                int a = r.Next(12, 20);
                return new(a, r.Next(1, a % 10 + 1));
            }
            case Skill.TeensCrossTen:
            {
                int a = r.Next(11, 19);
                return new(a, r.Next(a % 10 + 1, 10));
            }
            case Skill.WholeTens:
            {
                int a = r.Next(20, 100);
                return new(a, 10 * r.Next(1, a / 10 + 1));
            }
            case Skill.TwoMinusOneEasy:
            {
                int a;
                do a = r.Next(21, 100); while (a % 10 == 0);
                return new(a, r.Next(1, a % 10 + 1));
            }
            case Skill.TwoMinusOneCross:
            {
                int a;
                do a = r.Next(21, 100); while (a % 10 == 9);
                return new(a, r.Next(a % 10 + 1, 10));
            }
            case Skill.TwoMinusTwoEasy:
            {
                int a;
                do a = r.Next(31, 100); while (a % 10 == 0);
                int tens = r.Next(1, a / 10);
                int ones = r.Next(0, a % 10 + 1);
                return new(a, tens * 10 + ones);
            }
            case Skill.TwoMinusTwoCross:
            {
                int a;
                do a = r.Next(31, 100); while (a % 10 == 9);
                int tens = r.Next(1, a / 10);
                int ones = r.Next(a % 10 + 1, 10);
                return new(a, tens * 10 + ones);
            }
            default:
                return Make(Skill.TwoMinusOneCross, r);
        }
    }
}

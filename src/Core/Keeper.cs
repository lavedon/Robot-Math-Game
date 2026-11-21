namespace MathRobot.Core;

/// <summary>What Gizmo, the old repair robot who runs the Garage, says. Picks vary so he doesn't repeat himself.</summary>
public static class Keeper
{
    public static readonly string[] FirstHello =
    {
        "Bolt my bearings, a new bot!\nI'm Gizmo. Spend Bolts here!",
    };

    public static readonly string[] Hello =
    {
        "Back again, Beep? Let's see\nwhat those Bolts can buy!",
        "Clank-clank! Welcome to\nGizmo's Garage!",
        "Fresh parts, fair prices.\nMostly fair. Ha!",
        "Hopped any good number lines\nlately?",
    };

    public static readonly string[] Bought =
    {
        "Ka-ching! Pleasure doing\nbusiness!",
        "Bolted on tight. Looking\nsharp, Beep!",
        "Ooh, that suits you!",
        "Another happy customer!",
    };

    public static readonly string[] Broke =
    {
        "Not enough Bolts, kiddo.\nSolve a few more problems!",
        "Your bolt bag is a bit\nlight for that one.",
    };

    public static readonly string[] TooLow =
    {
        "That part's for higher-level\nbots. Level up first!",
    };

    public static readonly string[] Maxed =
    {
        "That's maxed out! Can't\nturbo a turbo.",
    };

    public static readonly string[] Bye =
    {
        "Off you hop! Mind the\nnumber line!",
        "Come back with more Bolts!",
    };

    public static string Pick(string[] lines, Random random) => lines[random.Next(lines.Length)];
}

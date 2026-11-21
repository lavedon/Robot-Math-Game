using MathRobot.Core;
using Xunit;

namespace MathRobot.Core.Tests;

public class ProblemTests
{
    [Fact]
    public void Every_stage_deals_problems_that_fit_its_skill()
    {
        var random = new Random(42);
        foreach (var stage in Stages.All)
        {
            for (int round = 0; round < 50; round++)
            {
                var problems = ProblemDealer.Deal(stage, random);
                Assert.Equal(stage.Problems, problems.Count);
                foreach (var p in problems)
                {
                    Assert.InRange(p.Subtrahend, 1, p.Minuend);
                    Assert.InRange(p.Minuend, 1, stage.LineMax);
                    Assert.True(p.Answer >= 0, $"{p} on stage {stage.Number}");
                    Check(stage.Skill, p);
                }
            }
        }
    }

    private static void Check(Skill skill, Problem p)
    {
        switch (skill)
        {
            case Skill.TakeOneOrTwo:
                Assert.InRange(p.Subtrahend, 1, 2);
                Assert.InRange(p.Minuend, 2, 10);
                break;
            case Skill.WithinTen:
                Assert.InRange(p.Minuend, 3, 10);
                break;
            case Skill.TeensNoCross:
                Assert.InRange(p.Minuend, 11, 19);
                Assert.True(p.Answer >= 10);
                Assert.True(p.Subtrahend < 10);
                break;
            case Skill.TeensCrossTen:
                Assert.InRange(p.Minuend, 11, 19);
                Assert.True(p.Answer < 10, p.ToString());
                Assert.True(p.Subtrahend < 10);
                break;
            case Skill.WholeTens:
                Assert.Equal(0, p.Subtrahend % 10);
                break;
            case Skill.TwoMinusOneEasy:
                Assert.True(p.Subtrahend < 10);
                Assert.False(p.CrossesTen);
                Assert.True(p.Minuend >= 21);
                break;
            case Skill.TwoMinusOneCross:
                Assert.True(p.Subtrahend < 10);
                Assert.True(p.CrossesTen, p.ToString());
                break;
            case Skill.TwoMinusTwoEasy:
                Assert.True(p.Subtrahend >= 10);
                Assert.False(p.CrossesTen, p.ToString());
                break;
            case Skill.TwoMinusTwoCross:
                Assert.True(p.Subtrahend >= 10);
                Assert.True(p.CrossesTen, p.ToString());
                break;
        }
    }

    [Fact]
    public void Problems_do_not_repeat_within_a_stage_when_there_are_enough()
    {
        var problems = ProblemDealer.Deal(Stages.Get(9), new Random(1));
        Assert.Equal(problems.Count, problems.Distinct().Count());
    }
}

public class HopLineTests
{
    [Fact]
    public void Start_must_be_on_the_first_number_to_lock()
    {
        var line = new HopLine(new Problem(13, 5), 20);
        Assert.Equal(HopRefusal.WrongStart, line.LockStart());
        for (int i = 0; i < 12; i++) line.MoveStart(1);
        Assert.Equal(HopRefusal.WrongStart, line.LockStart());
        line.MoveStart(1);
        Assert.Equal(HopRefusal.None, line.LockStart());
        Assert.Equal(HopPhase.Hop, line.Phase);
    }

    [Fact]
    public void The_start_flag_stays_on_the_line()
    {
        var line = new HopLine(new Problem(5, 2), 10);
        Assert.False(line.MoveStart(-1));
        Assert.False(line.MoveStart(11));
        Assert.True(line.MoveStart(10));
        Assert.False(line.MoveStart(1));
    }

    [Fact]
    public void Hops_add_up_and_land_on_the_answer()
    {
        var line = new HopLine(new Problem(52, 17), 100, 52);
        line.LockStart();
        line.HopBack(10);
        for (int i = 0; i < 7; i++) line.HopBack(1);
        Assert.Equal(17, line.Taken);
        Assert.Equal(35, line.Land);
        Assert.Equal(HopRefusal.None, line.Go());
        Assert.True(line.Correct);
    }

    [Fact]
    public void Wrong_hops_are_wrong()
    {
        var line = new HopLine(new Problem(9, 4), 10, 9);
        line.LockStart();
        for (int i = 0; i < 3; i++) line.HopBack(1);
        line.Go();
        Assert.False(line.Correct);
        Assert.Equal(HopPhase.Done, line.Phase);
    }

    [Fact]
    public void Cannot_hop_past_zero_or_go_without_hops()
    {
        var line = new HopLine(new Problem(3, 2), 10, 3);
        line.LockStart();
        Assert.Equal(HopRefusal.NoHops, line.Go());
        Assert.False(line.HopBack(10));
        Assert.True(line.HopBack(1));
        Assert.True(line.HopBack(1));
        Assert.True(line.HopBack(1));
        Assert.False(line.HopBack(1));
        Assert.Equal(0, line.Land);
    }

    [Fact]
    public void Shrinking_takes_back_hops_and_undo_can_unlock_the_start()
    {
        var line = new HopLine(new Problem(45, 13), 100, 45);
        line.LockStart();
        line.HopBack(10);
        line.HopBack(1);
        line.HopBack(1);
        Assert.True(line.Shrink(10));
        Assert.Equal(new[] { 1, 1 }, line.Hops);
        Assert.True(line.Shrink(10)); // a 10 over a tail of ones takes them all back
        Assert.Empty(line.Hops);
        Assert.False(line.Shrink(1));
        Assert.Equal(HopPhase.Hop, line.Phase);
        Assert.True(line.Undo());
        Assert.Equal(HopPhase.PlaceStart, line.Phase);
    }

    [Fact]
    public void Runs_group_equal_hops_for_labels()
    {
        var runs = HopLine.RunsOf(63, new[] { 10, 10, 1, 1, 1, 1, 1, 1, 1 });
        Assert.Equal(2, runs.Count);
        Assert.Equal(new HopRun(63, 10, 2), runs[0]);
        Assert.Equal(43, runs[0].To);
        Assert.Equal(new HopRun(43, 1, 7), runs[1]);
        Assert.Equal(36, runs[1].To);
    }

    [Fact]
    public void Neat_hops_are_tens_then_ones()
    {
        Assert.Equal(new[] { 10, 10, 1, 1, 1 }, HopLine.NeatHops(23));
        Assert.Equal(new[] { 1, 1 }, HopLine.NeatHops(2));
    }
}

public class StageRunTests
{
    private static Stage S => Stages.Get(3);

    [Fact]
    public void A_wrong_answer_comes_back_once_then_is_shown()
    {
        var problems = new[] { new Problem(15, 3), new Problem(17, 2) };
        var run = new StageRun(S, problems);
        Assert.Equal(StageRun.Outcome.TryAgainLater, run.Answer(false, 5));
        Assert.Equal(new Problem(17, 2), run.Current);
        Assert.Equal(StageRun.Outcome.Right, run.Answer(true, 5));
        Assert.Equal(new Problem(15, 3), run.Current);
        Assert.True(run.CurrentIsRetry);
        Assert.Equal(StageRun.Outcome.ShowAnswer, run.Answer(false, 5));
        Assert.True(run.Over);
        Assert.Equal(2, run.Mistakes);
        Assert.Equal(1, run.FirstTry);
    }

    [Fact]
    public void A_perfect_fast_run_earns_three_stars()
    {
        var problems = ProblemDealer.Deal(S, new Random(3));
        var run = new StageRun(S, problems);
        while (!run.Over)
        {
            run.Tick(4);
            run.Answer(true, 4);
        }
        var result = run.Result();
        Assert.Equal(3, result.Stars);
        Assert.Equal(result.Lines.Sum(l => l.Bolts), result.Bolts);
        Assert.Equal(S.Problems * 2 + S.Problems + 15, result.Bolts);
    }

    [Fact]
    public void Slow_and_sloppy_still_earns_one_star()
    {
        var run = new StageRun(S, new[] { new Problem(15, 3) });
        run.Tick(1000);
        run.Answer(false, 1000);
        run.Answer(true, 10);
        var result = run.Result();
        Assert.Equal(1, result.Stars);
        Assert.False(result.Accurate);
        Assert.False(result.Fast);
    }

    [Fact]
    public void The_shield_forgives_the_first_slip_and_the_clock_core_adds_time()
    {
        var run = new StageRun(S, new[] { new Problem(15, 3), new Problem(16, 4) }, new Perks(ExtraParSeconds: 10, Shield: true));
        run.Answer(false, 3);
        run.Answer(true, 3);
        run.Answer(true, 3);
        Assert.Equal(0, run.Mistakes);
        Assert.True(run.ShieldUsed);
        Assert.Equal(S.ParSeconds + 10, run.ParSeconds);
    }

    [Fact]
    public void Combo_and_magnet_pay_extra()
    {
        var problems = Enumerable.Range(0, 5).Select(i => new Problem(15 + i % 4, 2)).ToArray();
        var run = new StageRun(S, problems, new Perks(BoltBonus: 0.5, ComboLevel: 2));
        foreach (var _ in problems) run.Answer(true, 20);
        Assert.Equal(6, run.ComboBolts); // answers 3, 4 and 5 pay 2 each
        var result = run.Result();
        Assert.Contains(result.Lines, l => l.Label == "Bolt Magnet" && l.Bolts > 0);
    }

    [Fact]
    public void Hints_run_out()
    {
        var run = new StageRun(S, new[] { new Problem(15, 3) }, new Perks(Hints: 1));
        Assert.True(run.UseHint());
        Assert.False(run.UseHint());
    }
}

public class ProgressTests
{
    [Fact]
    public void Stages_unlock_in_order()
    {
        var p = new Progress();
        Assert.True(p.Unlocked(1));
        Assert.False(p.Unlocked(2));
        p.Record(new StageResult(1, 2, true, false, 30, 10, 50, Array.Empty<RewardLine>()));
        Assert.True(p.Unlocked(2));
        Assert.Equal(10, p.Bolts);
    }

    [Fact]
    public void Buying_levels_up_parts_and_costs_the_next_price()
    {
        var p = new Progress();
        var treads = Progress.Item("treads");
        Assert.Equal(Progress.BuyCheck.NeedBolts, p.CanBuy(treads));
        p.Grant(1000, 10000);
        for (int i = 0; i < treads.MaxLevel; i++) p.Buy(treads);
        Assert.Equal(1000 - treads.Prices.Sum(), p.Bolts);
        Assert.Equal(Progress.BuyCheck.MaxedOut, p.CanBuy(treads));
        Assert.Equal(1 + treads.MaxLevel, p.Stats.Speed);
        Assert.True(p.RepeatSeconds > 0.03);
    }

    [Fact]
    public void Some_items_need_a_higher_level()
    {
        var p = new Progress();
        p.Grant(1000);
        Assert.Equal(Progress.BuyCheck.NeedLevel, p.CanBuy(Progress.Item("hat_crown")));
        p.Grant(0, Progress.XpFor(5));
        Assert.Equal(5, p.Level);
        Assert.Equal(Progress.BuyCheck.Ok, p.CanBuy(Progress.Item("hat_crown")));
    }

    [Fact]
    public void Style_is_worn_when_bought_and_one_per_slot()
    {
        var p = new Progress();
        p.Grant(500, 5000);
        p.Buy(Progress.Item("hat_party"));
        Assert.Equal("hat_party", p.Worn(Slot.Hat));
        p.Buy(Progress.Item("hat_propeller"));
        Assert.Equal("hat_propeller", p.Worn(Slot.Hat));
        p.Wear(Progress.Item("hat_party"));
        Assert.Equal("hat_party", p.Worn(Slot.Hat));
        p.TakeOff(Slot.Hat);
        Assert.Null(p.Worn(Slot.Hat));
    }

    [Fact]
    public void Saves_round_trip_and_bad_saves_start_fresh()
    {
        var p = new Progress();
        p.Grant(300, 400);
        p.Buy(Progress.Item("magnet"));
        p.Buy(Progress.Item("phones_lime"));
        p.Record(new StageResult(1, 3, true, true, 30, 20, 50, Array.Empty<RewardLine>()));
        p.LastStop = 4;
        var copy = Progress.FromJson(p.ToJson());
        Assert.Equal(p.Bolts, copy.Bolts);
        Assert.Equal(p.Xp, copy.Xp);
        Assert.Equal(1, copy.LevelOf("magnet"));
        Assert.Equal("phones_lime", copy.Worn(Slot.Phones));
        Assert.Equal(3, copy.BestStars(1));
        Assert.Equal(4, copy.LastStop);

        Assert.Equal(0, Progress.FromJson("not json {").Bolts);
        Assert.Equal(0, Progress.FromJson(null).Bolts);
    }

    [Fact]
    public void Level_curve_and_titles()
    {
        Assert.Equal(0, Progress.XpFor(1));
        Assert.Equal(60, Progress.XpFor(2));
        var p = new Progress();
        Assert.Equal("Rookie Bot", p.Title);
        p.Grant(0, Progress.XpFor(3));
        Assert.Equal("Hop Scout", p.Title);
    }

    [Fact]
    public void Every_shop_item_has_prices()
    {
        Assert.All(Progress.Catalog, i => Assert.True(i.Prices.Length > 0 && i.Prices.All(x => x > 0)));
        Assert.Equal(Progress.Catalog.Count, Progress.Catalog.Select(i => i.Id).Distinct().Count());
    }
}

public class FontTests
{
    [Fact]
    public void The_embedded_font_has_every_printable_character()
    {
        var font = PixelFont.Default;
        for (char c = ' '; c <= '~'; c++) Assert.NotNull(font.GlyphFor(c));
        Assert.True(font.Measure("13 - 5 = 8") > 20);
    }
}

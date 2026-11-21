using System.Text.Json;

namespace MathRobot.Core;

/// <summary>The shelves of Gizmo's Garage.</summary>
public enum Shelf { Parts, Gadgets, Style }

/// <summary>Which part of Beep a Style item changes.</summary>
public enum Slot { None, Phones, Eyes, Hat, Trail }

/// <summary>
/// Something Gizmo sells. Parts and some Gadgets have levels (each level costs the next price); Style items are
/// owned once and worn in their <see cref="Slot"/>.
/// </summary>
public sealed record ShopItem(string Id, Shelf Shelf, string Name, string Blurb, int[] Prices, int MinLevel = 1,
    Slot Slot = Slot.None, string Stat = "")
{
    public int MaxLevel => Prices.Length;
    public bool Leveled => Prices.Length > 1;
}

/// <summary>Beep's stats as the shop shows them, from the Parts owned.</summary>
public readonly record struct Stats(int Speed, int Luck, int Time, int Combo);

/// <summary>Everything the player has earned: Bolts, XP and level, Stars per Stage, Upgrades and Style.</summary>
public sealed class Progress
{
    public static readonly IReadOnlyList<ShopItem> Catalog = new ShopItem[]
    {
        // Parts: stats, five levels each.
        new("treads", Shelf.Parts, "Turbo Treads", "The flags slide faster when you hold a button, and Beep hops quicker.", new[] { 20, 40, 70, 110, 160 }, Stat: "SPD"),
        new("magnet", Shelf.Parts, "Bolt Magnet", "+10% Bolts from every Stage, per level.", new[] { 25, 50, 80, 120, 180 }, Stat: "LUCK"),
        new("clock", Shelf.Parts, "Clock Core", "+5 seconds to beat the clock in every Stage, per level.", new[] { 30, 55, 90 }, Stat: "TIME"),
        new("combo", Shelf.Parts, "Combo Coil", "After 3 right in a row, each answer pays +1 Bolt per level.", new[] { 35, 70, 120 }, 2, Stat: "COMBO"),
        // Gadgets: helpers.
        new("specs", Shelf.Gadgets, "Number Specs", "See every number on the big two-digit lines.", new[] { 40 }),
        new("lamp", Shelf.Gadgets, "Hint Lamp", "Press Y for a hint, once per Stage per level.", new[] { 30, 80 }),
        new("shield", Shelf.Gadgets, "Oops Shield", "Your first slip in a Stage doesn't count.", new[] { 60 }, 3),
        // Style: what Beep wears.
        new("phones_cherry", Shelf.Style, "Cherry Phones", "Bright red headphones.", new[] { 15 }, Slot: Slot.Phones),
        new("phones_lime", Shelf.Style, "Lime Phones", "Zesty green headphones.", new[] { 15 }, Slot: Slot.Phones),
        new("phones_gold", Shelf.Style, "Gold Phones", "Shiny gold, for champions.", new[] { 40 }, 3, Slot.Phones),
        new("phones_rainbow", Shelf.Style, "Rainbow Phones", "Every colour at once!", new[] { 90 }, 5, Slot.Phones),
        new("eyes_round", Shelf.Style, "Round Eyes", "Big round friendly eyes.", new[] { 15 }, Slot: Slot.Eyes),
        new("eyes_heart", Shelf.Style, "Heart Eyes", "Beep loves maths.", new[] { 30 }, Slot: Slot.Eyes),
        new("eyes_star", Shelf.Style, "Star Eyes", "Sparkly star eyes.", new[] { 30 }, 2, Slot.Eyes),
        new("eyes_happy", Shelf.Style, "Happy Eyes", "Always smiling.", new[] { 20 }, Slot: Slot.Eyes),
        new("hat_propeller", Shelf.Style, "Propeller Cap", "It really spins!", new[] { 25 }, Slot: Slot.Hat),
        new("hat_party", Shelf.Style, "Party Hat", "Every Stage is a party.", new[] { 15 }, Slot: Slot.Hat),
        new("hat_wizard", Shelf.Style, "Wizard Hat", "For number magic.", new[] { 50 }, 3, Slot.Hat),
        new("hat_crown", Shelf.Style, "Crown", "The Number Line King.", new[] { 100 }, 5, Slot.Hat),
        new("trail_sparkle", Shelf.Style, "Sparkle Trail", "Sparkles follow every hop.", new[] { 25 }, Slot: Slot.Trail),
        new("trail_bubble", Shelf.Style, "Bubble Trail", "Bubbles follow every hop.", new[] { 25 }, Slot: Slot.Trail),
        new("trail_rainbow", Shelf.Style, "Rainbow Trail", "A rainbow follows every hop.", new[] { 60 }, 4, Slot.Trail),
    };

    public static ShopItem Item(string id) => Catalog.First(i => i.Id == id);

    public static readonly string[] Titles =
        { "Rookie Bot", "Hop Scout", "Line Ranger", "Minus Knight", "Number Ninja", "Math Mech", "Mega Bot", "Legend Bot" };

    private readonly Dictionary<string, int> _levels = new();
    private readonly Dictionary<int, int> _stars = new();
    private readonly Dictionary<Slot, string> _worn = new();

    public int Bolts { get; private set; }
    public int Xp { get; private set; }
    public int ShopVisits { get; set; }
    public int LastStop { get; set; }
    public bool Muted { get; set; }

    /// <summary>Total XP needed to reach <paramref name="level"/> (level 1 needs none): 60, 150, 270, ...</summary>
    public static int XpFor(int level) => 30 * (level - 1) * level;

    public int Level
    {
        get
        {
            int level = 1;
            while (Xp >= XpFor(level + 1)) level++;
            return level;
        }
    }

    /// <summary>A new title every two levels.</summary>
    public string Title => Titles[Math.Min((Level - 1) / 2, Titles.Length - 1)];

    /// <summary>The level an item is at (0 = not owned).</summary>
    public int LevelOf(string id) => _levels.GetValueOrDefault(id);
    public bool Owns(string id) => LevelOf(id) > 0;

    public int BestStars(int stage) => _stars.GetValueOrDefault(stage);
    public int TotalStars => _stars.Values.Sum();
    public bool Cleared(int stage) => BestStars(stage) > 0;

    /// <summary>A Stage is open when it is the first, or the one before it is Cleared.</summary>
    public bool Unlocked(int stage) => stage <= 1 || Cleared(stage - 1);

    /// <summary>The price of the next level of an item, or null when it's maxed out.</summary>
    public int? NextPrice(ShopItem item) => LevelOf(item.Id) < item.MaxLevel ? item.Prices[LevelOf(item.Id)] : null;

    public enum BuyCheck { Ok, MaxedOut, NeedLevel, NeedBolts }

    public BuyCheck CanBuy(ShopItem item)
    {
        if (NextPrice(item) is not { } price) return BuyCheck.MaxedOut;
        if (Level < item.MinLevel) return BuyCheck.NeedLevel;
        return Bolts < price ? BuyCheck.NeedBolts : BuyCheck.Ok;
    }

    public void Buy(ShopItem item)
    {
        if (CanBuy(item) != BuyCheck.Ok) throw new InvalidOperationException($"Can't buy {item.Name}");
        Bolts -= NextPrice(item)!.Value;
        _levels[item.Id] = LevelOf(item.Id) + 1;
        if (item.Slot != Slot.None) Wear(item);
    }

    /// <summary>The Style item worn in a slot, or null for the default look.</summary>
    public string? Worn(Slot slot) => _worn.GetValueOrDefault(slot);

    public void Wear(ShopItem item)
    {
        if (item.Slot == Slot.None || !Owns(item.Id)) return;
        _worn[item.Slot] = item.Id;
    }

    public void TakeOff(Slot slot) => _worn.Remove(slot);

    public Stats Stats => new(1 + LevelOf("treads"), 1 + LevelOf("magnet"), 1 + LevelOf("clock"), LevelOf("combo"));

    public Perks Perks => new(
        ExtraParSeconds: 5 * LevelOf("clock"),
        BoltBonus: 0.1 * LevelOf("magnet"),
        ComboLevel: LevelOf("combo"),
        Hints: LevelOf("lamp"),
        Shield: Owns("shield"));

    /// <summary>Seconds between repeats when a flag button is held: Turbo Treads make it quicker.</summary>
    public double RepeatSeconds => 0.16 - 0.022 * LevelOf("treads");

    /// <summary>Adds a finished Stage's rewards. Returns how many levels Beep went up.</summary>
    public int Record(StageResult result)
    {
        int before = Level;
        Bolts += result.Bolts;
        Xp += result.Xp;
        _stars[result.Stage] = Math.Max(BestStars(result.Stage), result.Stars);
        return Level - before;
    }

    /// <summary>For tests and the screenshot tool.</summary>
    internal void Grant(int bolts, int xp = 0)
    {
        Bolts += bolts;
        Xp += xp;
    }

    // ---- Saving ----

    private sealed class SaveData
    {
        public int Version { get; set; } = 1;
        public int Bolts { get; set; }
        public int Xp { get; set; }
        public int ShopVisits { get; set; }
        public int LastStop { get; set; }
        public bool Muted { get; set; }
        public Dictionary<string, int> Levels { get; set; } = new();
        public Dictionary<string, int> Stars { get; set; } = new();
        public Dictionary<string, string> Worn { get; set; } = new();
    }

    public string ToJson() => JsonSerializer.Serialize(new SaveData
    {
        Bolts = Bolts,
        Xp = Xp,
        ShopVisits = ShopVisits,
        LastStop = LastStop,
        Muted = Muted,
        Levels = new(_levels),
        Stars = _stars.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
        Worn = _worn.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value),
    }, new JsonSerializerOptions { WriteIndented = true });

    /// <summary>Reads a save; anything unreadable gives a fresh game rather than a crash.</summary>
    public static Progress FromJson(string? json)
    {
        var p = new Progress();
        if (string.IsNullOrWhiteSpace(json)) return p;
        try
        {
            var d = JsonSerializer.Deserialize<SaveData>(json);
            if (d is null) return p;
            p.Bolts = Math.Max(0, d.Bolts);
            p.Xp = Math.Max(0, d.Xp);
            p.ShopVisits = d.ShopVisits;
            p.LastStop = d.LastStop;
            p.Muted = d.Muted;
            foreach (var (id, level) in d.Levels)
                if (Catalog.FirstOrDefault(i => i.Id == id) is { } item) p._levels[id] = Math.Clamp(level, 0, item.MaxLevel);
            foreach (var (stage, stars) in d.Stars)
                if (int.TryParse(stage, out int s) && s >= 1 && s <= Stages.All.Count) p._stars[s] = Math.Clamp(stars, 0, 3);
            foreach (var (slot, id) in d.Worn)
                if (Enum.TryParse<Slot>(slot, out var sl) && p.Owns(id)) p._worn[sl] = id;
        }
        catch (JsonException)
        {
            return new Progress();
        }
        return p;
    }
}

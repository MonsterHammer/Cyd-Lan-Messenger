using System.Windows.Media;
using CydLan.Core;
using Brushes = System.Windows.Media.Brushes;

namespace CydLan.Desktop;

/// <summary>Full visual and behavioral configuration for the floating desktop companion.</summary>
public sealed record PetAppearance(
    string Body,
    string Ears,
    string Eyes,
    string Mouth,
    string Cheeks,
    string Glasses,
    string Hat,
    string Outfit,
    string Back,
    string Prop,
    string Feet,
    string Pattern,
    string Vibe,
    string BodyColor,
    string AccentColor,
    string DetailColor,
    double Size,
    string Brows = "none",
    string Aura = "none")
{
    public static PetAppearance Default { get; } = new(
        "squircle", "auto", "dot", "smile", "blush",
        "none", "auto", "auto", "none", "none",
        "paws", "solid", "bouncy", "", "", "", 1.0, "none", "none");

    public static PetAppearance From(AppSettings s) => new(
        FloatingPetStyle.NormalizeBody(s.PetBody),
        FloatingPetStyle.NormalizeEars(s.PetEars),
        FloatingPetStyle.NormalizeEyes(s.PetEyes),
        FloatingPetStyle.NormalizeMouth(s.PetMouth),
        FloatingPetStyle.NormalizeCheeks(s.PetCheeks),
        FloatingPetStyle.NormalizeGlasses(s.PetGlasses),
        FloatingPetStyle.NormalizeHat(s.PetHat),
        FloatingPetStyle.NormalizeOutfit(s.PetOutfit),
        FloatingPetStyle.NormalizeBack(s.PetBack),
        FloatingPetStyle.NormalizeProp(s.PetProp),
        FloatingPetStyle.NormalizeFeet(s.PetFeet),
        FloatingPetStyle.NormalizePattern(s.PetPattern),
        FloatingPetStyle.NormalizeVibe(s.PetVibe),
        AppearanceOptions.NormalizeHex(s.PetBodyColor),
        AppearanceOptions.NormalizeHex(s.PetAccentColor),
        AppearanceOptions.NormalizeHex(s.PetDetailColor),
        FloatingPetStyle.ClampSize(s.PetSize),
        FloatingPetStyle.NormalizeBrows(s.PetBrows),
        FloatingPetStyle.NormalizeAura(s.PetAura));

    public static PetAppearance? FromWireCode(string? petCode)
    {
        if (string.IsNullOrWhiteSpace(petCode)) return null;
        var temp = new AppSettings();
        return CompanionProfileCodec.TryDecodeInto(petCode, temp) ? From(temp) : null;
    }

    public string ToWireCode()
    {
        var temp = new AppSettings();
        FloatingPetStyle.Apply(this, temp);
        return CompanionProfileCodec.Encode(temp);
    }
}

public sealed record PetPreset(string Name, PetAppearance Appearance)
{
    public override string ToString() => Name;
}

/// <summary>Option catalogs, presets, normalization, and randomizer for companion customization.</summary>
public static class FloatingPetStyle
{
    public const string Auto = "auto";
    public const string None = "none";
    public const double MinSize = .75;
    public const double MaxSize = 5.0;
    /// <summary>Largest size drawn inside a fixed-size preview.</summary>
    public const double MaxInlineSize = 1.35;

    public static IReadOnlyList<StyleOption> Bodies { get; } =
    [
        new("squircle", "Squircle"),
        new("round", "Mochi"),
        new("bean", "Bean"),
        new("pear", "Pear"),
        new("star", "Star"),
        new("mecha", "Mecha"),
        new("cloud", "Cloud"),
        new("ghost", "Spirit"),
        new("heart", "Heart"),
        new("cube", "Voxel"),
        new("slime", "Slime"),
        new("egg", "Egg"),
        new("mushroom", "Mushroom"),
        new("octo", "Octo"),
        new("drop", "Droplet"),
        new("chonk", "Chonk")
    ];

    public static IReadOnlyList<StyleOption> Ears { get; } =
    [
        new(Auto, "Classic"),
        new("cat", "Cat"),
        new("bunny", "Bunny"),
        new("bear", "Bear"),
        new("fox", "Fox"),
        new("floppy", "Puppy"),
        new("elf", "Elf"),
        new("devil", "Horns"),
        new("antler", "Antlers"),
        new("mecha", "V-fin"),
        new("fin", "Axolotl"),
        new("mouse", "Mouse"),
        new("panda", "Panda"),
        new("unicorn", "Unicorn"),
        new("dragon", "Dragon"),
        new("sheep", "Sheep"),
        new("leaf", "Leaves"),
        new(None, "None")
    ];

    public static IReadOnlyList<StyleOption> Eyes { get; } =
    [
        new("dot", "Glossy"),
        new("big", "Kawaii"),
        new("cat", "Cat"),
        new("star", "Starry"),
        new("heart", "Heart"),
        new("sparkle", "Sparkle"),
        new("visor", "Visor"),
        new("chill", "Chill"),
        new("wink", "Wink"),
        new("sleepy", "Comfy"),
        new("spiral", "Dizzy"),
        new("button", "Plush"),
        new("anime", "Anime"),
        new("uwu", "UwU"),
        new("robot", "LED"),
        new("tiny", "Beady"),
        new("glow", "Glow")
    ];

    public static IReadOnlyList<StyleOption> Brows { get; } =
    [
        new(None, "Clean"),
        new("soft", "Arched"),
        new("happy", "Upbeat"),
        new("focused", "Fierce"),
        new("worried", "Puppy"),
        new("unibrow", "Unibrow"),
        new("thick", "Bold")
    ];

    public static IReadOnlyList<StyleOption> Mouths { get; } =
    [
        new("smile", "Smile"),
        new("cat", "OwO"),
        new("grin", "Grin"),
        new("fang", "Fang"),
        new("blep", "Blep"),
        new("smirk", "Smirk"),
        new("kiss", "Cute :3"),
        new("beak", "Beak"),
        new("whistle", "Pout"),
        new("mustache", "Dapper"),
        new("buck", "Buck teeth"),
        new("tongue", "Tongue out"),
        new("wide", "Big laugh"),
        new("vampire", "Vampire"),
        new(None, "Quiet")
    ];

    public static IReadOnlyList<StyleOption> Cheeks { get; } =
    [
        new("blush", "Rosy"),
        new("freckles", "Freckles"),
        new("whiskers", "Whiskers"),
        new("stripes", "Anime"),
        new("hearts", "Hearts"),
        new("stars", "Stars"),
        new(None, "Clean")
    ];

    public static IReadOnlyList<StyleOption> Glasses { get; } =
    [
        new(None, "None"),
        new("round", "Round"),
        new("shades", "Shades"),
        new("star", "Star shades"),
        new("monocle", "Monocle"),
        new("goggles", "Goggles"),
        new("patch", "Eyepatch")
    ];

    public static IReadOnlyList<StyleOption> Hats { get; } =
    [
        new(Auto, "Classic"),
        new(None, "None"),
        new("antenna", "Antenna"),
        new("sprout", "Sprout"),
        new("halo", "Halo"),
        new("crown", "Crown"),
        new("cap", "Cap"),
        new("beanie", "Beanie"),
        new("wizard", "Wizard"),
        new("headphones", "Headset"),
        new("bow", "Bow"),
        new("beret", "Beret"),
        new("tophat", "Top hat"),
        new("chef", "Chef"),
        new("flower", "Flower"),
        new("pirate", "Pirate"),
        new("witch", "Witch"),
        new("party", "Party"),
        new("headband", "Headband"),
        new("cowboy", "Cowboy"),
        new("viking", "Viking")
    ];

    public static IReadOnlyList<StyleOption> Outfits { get; } =
    [
        new(Auto, "Classic"),
        new("bowtie", "Bow tie"),
        new("scarf", "Scarf"),
        new("hoodie", "Hoodie"),
        new("tie", "Necktie"),
        new("bib", "Apron"),
        new("armor", "Armor"),
        new("star", "Star badge"),
        new("heart", "Heart gem"),
        new("arc", "Arc core"),
        new(None, "Plain")
    ];

    public static IReadOnlyList<StyleOption> Backs { get; } =
    [
        new(None, "None"),
        new("angel", "Angel wings"),
        new("bat", "Bat wings"),
        new("fairy", "Fairy wings"),
        new("mecha", "Thrusters"),
        new("jetpack", "Jetpack"),
        new("cape", "Cape"),
        new("tail", "Fluffy tail"),
        new("shell", "Shell"),
        new("shark", "Shark fin"),
        new("dragon", "Dragon wings"),
        new("butterfly", "Butterfly"),
        new("backpack", "Backpack")
    ];

    public static IReadOnlyList<StyleOption> Props { get; } =
    [
        new(None, "None"),
        new("sparkles", "Sparkles"),
        new("hearts", "Hearts"),
        new("music", "Notes"),
        new("fire", "Flames"),
        new("orbit", "Orbit ring"),
        new("coffee", "Coffee"),
        new("wand", "Star wand"),
        new("balloon", "Balloon"),
        new("gamepad", "Gamepad"),
        new("lantern", "Lantern"),
        new("sword", "Sword"),
        new("shield", "Shield"),
        new("book", "Book"),
        new("bubbletea", "Bubble tea")
    ];

    public static IReadOnlyList<StyleOption> Auras { get; } =
    [
        new(None, "None"),
        new("ring", "Halo ring"),
        new("sunburst", "Rays"),
        new("neon", "Neon glow"),
        new("sakura", "Petals"),
        new("bubbles", "Bubbles"),
        new("stars", "Star ring")
    ];

    public static IReadOnlyList<StyleOption> Feet { get; } =
    [
        new("paws", "Paws"),
        new("boots", "Boots"),
        new("sneakers", "Sneakers"),
        new("hover", "Hover"),
        new("wheels", "Rollers"),
        new(None, "None")
    ];

    public static IReadOnlyList<StyleOption> Patterns { get; } =
    [
        new("solid", "Clean"),
        new("belly", "Belly"),
        new("gradient", "Ombre"),
        new("spots", "Spots"),
        new("stripes", "Tabby"),
        new("cosmic", "Cosmic"),
        new("tuxedo", "Tuxedo"),
        new("grid", "Cyber"),
        new("hearts", "Hearts"),
        new("cow", "Cow"),
        new("zigzag", "Zigzag")
    ];

    public static IReadOnlyList<StyleOption> Vibes { get; } =
    [
        new("bouncy", "Bouncy"),
        new("calm", "Chill"),
        new("zoomies", "Zoomies"),
        new("sleepy", "Sleepy")
    ];

    public static IReadOnlyList<ColorOption> BodySwatches { get; } = BuildSwatches("Classic",
    [
        ("#FFFDF9", "Cream"),
        ("#FCE7F3", "Sakura"),
        ("#E0F2FE", "Sky"),
        ("#DCFCE7", "Mint"),
        ("#FEF3C7", "Butter"),
        ("#EDE9FE", "Lavender"),
        ("#FFE4E6", "Peach"),
        ("#E2E8F0", "Silver"),
        ("#334155", "Slate"),
        ("#1E1B4B", "Indigo"),
        ("#18181B", "Onyx"),
        ("#DC2626", "Ruby")
    ]);

    public static IReadOnlyList<ColorOption> AccentSwatches { get; } = BuildSwatches("Classic",
    [
        ("#3B82F6", "Cobalt"),
        ("#06B6D4", "Cyan"),
        ("#10B981", "Emerald"),
        ("#F59E0B", "Gold"),
        ("#F97316", "Coral"),
        ("#EF4444", "Crimson"),
        ("#EC4899", "Rose"),
        ("#A855F7", "Violet"),
        ("#6366F1", "Indigo"),
        ("#84CC16", "Lime"),
        ("#1E293B", "Obsidian"),
        ("#F8FAFC", "Pearl")
    ]);

    public static IReadOnlyList<ColorOption> DetailSwatches { get; } = BuildSwatches("Classic",
    [
        ("#F472B6", "Pink"),
        ("#38BDF8", "Sky"),
        ("#FACC15", "Sun"),
        ("#34D399", "Mint"),
        ("#FB923C", "Tangerine"),
        ("#F43F5E", "Berry"),
        ("#C084FC", "Orchid"),
        ("#FBBF24", "Honey"),
        ("#94A3B8", "Frost"),
        ("#FFFFFF", "White"),
        ("#1F2937", "Charcoal"),
        ("#4ADE80", "Neon")
    ]);

    public static IReadOnlyList<PetPreset> Presets { get; } =
    [
        new("Default", PetAppearance.Default),
        new("Mecha Ace", new("mecha", "mecha", "visor", "smirk", "stripes", "none", "none", "arc", "mecha", "orbit", "boots", "grid", "zoomies", "#E2E8F0", "#2563EB", "#38BDF8", 1.05, "focused", "ring")),
        new("Barista", new("round", "bear", "chill", "smile", "freckles", "round", "beret", "scarf", "none", "coffee", "boots", "belly", "calm", "#FDE6C4", "#92400E", "#FB7185", 1.0, "soft", "none")),
        new("Wizard", new("pear", "fox", "star", "cat", "stars", "none", "wizard", "bowtie", "cape", "wand", "paws", "cosmic", "bouncy", "#312E81", "#F59E0B", "#38BDF8", 1.0, "happy", "stars")),
        new("Angel", new("cloud", "none", "big", "smile", "blush", "none", "halo", "heart", "angel", "sparkles", "hover", "gradient", "calm", "#FFFDF9", "#38BDF8", "#FACC15", 1.0, "soft", "sunburst")),
        new("Imp", new("bean", "devil", "cat", "fang", "stripes", "none", "headphones", "hoodie", "bat", "fire", "sneakers", "tuxedo", "zoomies", "#DC2626", "#18181B", "#FACC15", 1.0, "focused", "neon")),
        new("Cyber DJ", new("squircle", "cat", "visor", "grin", "stripes", "shades", "headphones", "hoodie", "jetpack", "music", "sneakers", "gradient", "zoomies", "#111827", "#06B6D4", "#EC4899", 1.05, "none", "neon")),
        new("Sakura", new("round", "bunny", "sparkle", "blep", "hearts", "none", "flower", "bowtie", "tail", "hearts", "paws", "belly", "bouncy", "#FCE7F3", "#EC4899", "#34D399", .95, "soft", "sakura")),
        new("Monarch", new("pear", "none", "dot", "mustache", "blush", "monocle", "crown", "tie", "cape", "sparkles", "none", "gradient", "calm", "#38BDF8", "#F59E0B", "#E11D48", 1.1, "thick", "sunburst")),
        new("Spirit", new("ghost", "fin", "spiral", "blep", "stars", "patch", "beanie", "scarf", "tail", "lantern", "hover", "cosmic", "sleepy", "#F1F5F9", "#8B5CF6", "#38BDF8", 1.0, "worried", "ring")),
        new("Chef", new("round", "bear", "sparkle", "kiss", "blush", "none", "chef", "bib", "none", "coffee", "paws", "tuxedo", "bouncy", "#FFFDF9", "#EF4444", "#FBBF24", 1.0, "happy", "none")),
        new("Knight", new("cube", "elf", "dot", "smirk", "none", "none", "crown", "armor", "cape", "sparkles", "boots", "solid", "bouncy", "#E2E8F0", "#3B82F6", "#FACC15", 1.05, "focused", "sunburst")),
        new("Shark", new("bean", "fin", "cat", "grin", "stripes", "none", "cap", "hoodie", "shark", "none", "sneakers", "belly", "zoomies", "#E0F2FE", "#0284C7", "#FFFFFF", 1.05, "focused", "bubbles")),
        new("Puppy", new("squircle", "floppy", "big", "blep", "freckles", "none", "sprout", "scarf", "tail", "balloon", "paws", "spots", "bouncy", "#FEF3C7", "#D97706", "#F472B6", .95, "worried", "none")),
        new("Gamer", new("cube", "cat", "sparkle", "fang", "stripes", "goggles", "tophat", "hoodie", "jetpack", "gamepad", "wheels", "grid", "zoomies", "#1E1B4B", "#A855F7", "#4ADE80", 1.0, "happy", "neon")),
        new("Astronaut", new("round", "none", "visor", "smile", "none", "none", "antenna", "star", "jetpack", "orbit", "boots", "solid", "calm", "#F8FAFC", "#1E293B", "#F97316", 1.0, "soft", "stars")),
        new("Reindeer", new("bean", "antler", "big", "smile", "blush", "none", "none", "scarf", "tail", "lantern", "paws", "belly", "bouncy", "#B45309", "#DC2626", "#FFFDF9", 1.0, "happy", "none")),
        new("Frog", new("round", "none", "big", "grin", "blush", "none", "crown", "none", "none", "none", "paws", "belly", "calm", "#84CC16", "#166534", "#FACC15", .95, "none", "none")),
        new("Pirate", new("pear", "bear", "dot", "smirk", "stripes", "patch", "tophat", "tie", "cape", "lantern", "boots", "stripes", "bouncy", "#FDE6C4", "#1F2937", "#DC2626", 1.05, "focused", "none")),
        new("Penguin", new("bean", "none", "dot", "beak", "blush", "none", "beanie", "scarf", "none", "none", "paws", "tuxedo", "calm", "#18181B", "#F8FAFC", "#F59E0B", .95, "none", "none")),
        new("Fairy", new("star", "elf", "sparkle", "kiss", "hearts", "none", "flower", "heart", "fairy", "wand", "hover", "gradient", "bouncy", "#EDE9FE", "#C084FC", "#F472B6", .9, "happy", "sakura")),
        new("Robot", new("cube", "mecha", "visor", "whistle", "none", "goggles", "antenna", "armor", "mecha", "gamepad", "wheels", "grid", "zoomies", "#94A3B8", "#0EA5E9", "#FACC15", 1.0, "unibrow", "ring")),
        new("Vampire", new("pear", "devil", "cat", "fang", "none", "monocle", "tophat", "bowtie", "bat", "lantern", "boots", "tuxedo", "calm", "#E2E8F0", "#7F1D1D", "#18181B", 1.0, "thick", "none")),
        new("Turtle", new("round", "none", "chill", "smile", "freckles", "round", "cap", "none", "shell", "coffee", "paws", "spots", "sleepy", "#86EFAC", "#15803D", "#FBBF24", 1.0, "soft", "bubbles")),
        new("Rock star", new("squircle", "fox", "star", "grin", "stars", "shades", "headphones", "tie", "cape", "music", "sneakers", "stripes", "zoomies", "#18181B", "#EF4444", "#FACC15", 1.1, "focused", "sunburst")),
        new("Lovebug", new("heart", "bunny", "heart", "kiss", "hearts", "none", "bow", "heart", "fairy", "hearts", "hover", "solid", "bouncy", "#FFE4E6", "#E11D48", "#F472B6", .95, "happy", "sakura")),
        new("Kaiju", new("bean", "fin", "spiral", "fang", "stripes", "none", "crown", "none", "tail", "fire", "boots", "spots", "zoomies", "#10B981", "#064E3B", "#F97316", 1.15, "thick", "neon"))
    ];

    public static long CombinationCount { get; } =
        (long)Bodies.Count * Ears.Count * Eyes.Count * Brows.Count * Mouths.Count * Cheeks.Count * Glasses.Count *
        Hats.Count * Outfits.Count * Backs.Count * Props.Count * Auras.Count * Feet.Count * Patterns.Count * Vibes.Count;

    public static string NormalizeBody(string? value) => NormalizeFrom(value, Bodies, "squircle");
    public static string NormalizeEars(string? value) => NormalizeFrom(value, Ears, Auto);
    public static string NormalizeEyes(string? value) => NormalizeFrom(value, Eyes, "dot");
    public static string NormalizeBrows(string? value) => NormalizeFrom(value, Brows, None);
    public static string NormalizeMouth(string? value) => NormalizeFrom(value, Mouths, "smile");
    public static string NormalizeCheeks(string? value) => NormalizeFrom(value, Cheeks, "blush");
    public static string NormalizeGlasses(string? value) => NormalizeFrom(value, Glasses, None);
    public static string NormalizeHat(string? value) => NormalizeFrom(value, Hats, Auto);
    public static string NormalizeOutfit(string? value) => NormalizeFrom(value, Outfits, Auto);
    public static string NormalizeBack(string? value) => NormalizeFrom(value, Backs, None);
    public static string NormalizeProp(string? value) => NormalizeFrom(value, Props, None);
    public static string NormalizeAura(string? value) => NormalizeFrom(value, Auras, None);
    public static string NormalizeFeet(string? value) => NormalizeFrom(value, Feet, "paws");
    public static string NormalizePattern(string? value) => NormalizeFrom(value, Patterns, "solid");
    public static string NormalizeVibe(string? value) => NormalizeFrom(value, Vibes, "bouncy");
    public static double ClampSize(double size) => double.IsFinite(size) ? Math.Round(Math.Clamp(size, MinSize, MaxSize), 2) : 1.0;

    public static void Apply(PetAppearance source, AppSettings target)
    {
        target.PetBody = NormalizeBody(source.Body);
        target.PetEars = NormalizeEars(source.Ears);
        target.PetEyes = NormalizeEyes(source.Eyes);
        target.PetBrows = NormalizeBrows(source.Brows);
        target.PetMouth = NormalizeMouth(source.Mouth);
        target.PetCheeks = NormalizeCheeks(source.Cheeks);
        target.PetGlasses = NormalizeGlasses(source.Glasses);
        target.PetHat = NormalizeHat(source.Hat);
        target.PetOutfit = NormalizeOutfit(source.Outfit);
        target.PetBack = NormalizeBack(source.Back);
        target.PetProp = NormalizeProp(source.Prop);
        target.PetAura = NormalizeAura(source.Aura);
        target.PetFeet = NormalizeFeet(source.Feet);
        target.PetPattern = NormalizePattern(source.Pattern);
        target.PetVibe = NormalizeVibe(source.Vibe);
        target.PetBodyColor = AppearanceOptions.NormalizeHex(source.BodyColor);
        target.PetAccentColor = AppearanceOptions.NormalizeHex(source.AccentColor);
        target.PetDetailColor = AppearanceOptions.NormalizeHex(source.DetailColor);
        target.PetSize = ClampSize(source.Size);
    }

    public static PetAppearance Randomize(Random? random = null)
    {
        var rng = random ?? Random.Shared;
        string Pick(IReadOnlyList<StyleOption> items, bool skipFirstIfRare = false)
        {
            int start = skipFirstIfRare && items.Count > 1 && rng.Next(4) > 0 ? 1 : 0;
            return items[rng.Next(start, items.Count)].Value;
        }
        string PickColor(IReadOnlyList<ColorOption> swatches) =>
            rng.Next(5) == 0 ? "" : swatches[rng.Next(1, swatches.Count)].Value;

        string[] nonAutoHats = Hats.Where(h => h.Value != Auto).Select(h => h.Value).ToArray();
        string[] nonAutoEars = Ears.Where(e => e.Value != Auto).Select(e => e.Value).ToArray();
        string[] nonAutoOutfits = Outfits.Where(o => o.Value != Auto).Select(o => o.Value).ToArray();

        return new PetAppearance(
            Pick(Bodies),
            rng.Next(6) == 0 ? None : nonAutoEars[rng.Next(nonAutoEars.Length)],
            Pick(Eyes),
            Pick(Mouths),
            Pick(Cheeks),
            rng.Next(3) == 0 ? Pick(Glasses, skipFirstIfRare: true) : None,
            nonAutoHats[rng.Next(nonAutoHats.Length)],
            rng.Next(5) == 0 ? None : nonAutoOutfits[rng.Next(nonAutoOutfits.Length)],
            rng.Next(4) == 0 ? None : Backs.Where(b => b.Value != None).ElementAt(rng.Next(Backs.Count - 1)).Value,
            rng.Next(4) == 0 ? None : Props.Where(p => p.Value != None).ElementAt(rng.Next(Props.Count - 1)).Value,
            Pick(Feet),
            Pick(Patterns),
            Pick(Vibes),
            PickColor(BodySwatches),
            PickColor(AccentSwatches),
            PickColor(DetailSwatches),
            Math.Round(.85 + rng.Next(8) * .05, 2),
            rng.Next(2) == 0 ? Pick(Brows) : None,
            rng.Next(2) == 0 ? Pick(Auras, skipFirstIfRare: true) : None);
    }

    private static string NormalizeFrom(string? value, IReadOnlyList<StyleOption> catalog, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        string normalized = value.Trim().ToLowerInvariant();
        if (normalized == "bow" && catalog == Outfits) return "bowtie";
        return catalog.Any(o => o.Value == normalized) ? normalized : fallback;
    }

    private static IReadOnlyList<ColorOption> BuildSwatches(string defaultLabel, (string Hex, string Name)[] palette)
    {
        var list = new List<ColorOption>(palette.Length + 1)
        {
            new("", defaultLabel, Brushes.Transparent, IsDefault: true)
        };
        foreach (var (hex, name) in palette)
        {
            AppearanceOptions.TryParseHex(hex, out var color);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            list.Add(new ColorOption(hex, name, brush));
        }
        return list;
    }
}

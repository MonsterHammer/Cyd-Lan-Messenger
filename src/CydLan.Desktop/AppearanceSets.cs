namespace CydLan.Desktop;

public sealed record AppearanceSet(string Name, AppearanceCodec.LookData Look)
{
    public override string ToString() => Name;
}

/// <summary>Curated one-click looks that set theme, texture, ambient, headers, panels, aura, bubbles and motion together.</summary>
public static class AppearanceSets
{
    private static AppearanceSet Set(
        string name, string theme, string texture, string finish, string bubble, string shape, string canvas, string aura,
        string anim, Action<AppearanceCodec.LookData> fx, bool details = true)
    {
        var d = new AppearanceCodec.LookData
        {
            Theme = theme,
            ThemeDetails = details ? true : null,
            ColorFinish = finish == ColorFinish.Off ? null : finish,
            ColorFinishIntensity = .65,
            BubbleStyle = bubble,
            BubbleShape = shape,
            CanvasGradient = canvas,
            CanvasGradientIntensity = .55,
            ChromeTexture = texture,
            ChromeTextureIntensity = .65,
            TargetAura = aura,
            TargetAuraIntensity = .7,
            AuraModule = aura != TargetAura.Off ? true : null,
            AuraPerson = aura != TargetAura.Off ? true : null,
            AuraButtons = aura != TargetAura.Off ? true : null,
            MsgAnim = anim,
            EffectSpeed = 1.0
        };
        fx(d);
        return new AppearanceSet(name, d);
    }

    public static IReadOnlyList<AppearanceSet> All { get; } =
    [
        Set("Cherry blossom", ThemeManager.Sakura, ChromeTexture.Watercolor, ColorFinish.Pastel, AppearanceOptions.BubbleSoft, AppearanceOptions.ShapeTail, ChatCanvasOverlay.Spotlight, TargetAura.Sakura, MessageMotion.Pop,
            d => { d.Petals = true; d.TitleSparkles = true; d.HeaderSilk = true; d.SidebarMotes = true; d.ButtonBounce = true; }),
        Set("Neon city", ThemeManager.Cyberpunk, ChromeTexture.Circuit, ColorFinish.Vivid, AppearanceOptions.BubbleGlass, AppearanceOptions.ShapeSubtle, ChatCanvasOverlay.Mesh, TargetAura.Neon, MessageMotion.Slide,
            d => { d.Matrix = true; d.TitleLaser = true; d.HeaderCircuit = true; d.RailScan = true; d.SidebarCircuit = true; d.ButtonGlow = true; d.ButtonSpark = true; }),
        Set("Deep space", ThemeManager.Midnight, ChromeTexture.Galaxy, ColorFinish.Cinema, AppearanceOptions.BubbleGlass, AppearanceOptions.ShapeRounded, ChatCanvasOverlay.Vignette, TargetAura.Arcane, MessageMotion.Fade,
            d => { d.Starfield = true; d.TitleComet = true; d.HeaderNebula = true; d.RailStardust = true; d.SidebarNebula = true; d.ButtonRing = true; }),
        Set("Campfire", ThemeManager.Ember, ChromeTexture.Grain, ColorFinish.Vintage, AppearanceOptions.BubbleSolid, AppearanceOptions.ShapeRounded, ChatCanvasOverlay.Vignette, TargetAura.Fire, MessageMotion.Fade,
            d => { d.Embers = true; d.TitleShimmer = true; d.HeaderPulse = true; d.RailBeacon = true; d.SidebarGlow = true; d.ButtonGlow = true; }),
        Set("Rainy window", ThemeManager.Nord, ChromeTexture.Topo, ColorFinish.Muted, AppearanceOptions.BubbleMinimal, AppearanceOptions.ShapeSubtle, ChatCanvasOverlay.Diagonal, TargetAura.Smoke, MessageMotion.Fade,
            d => { d.Rain = true; d.HeaderRipples = true; d.RailStream = true; d.SidebarWaves = true; }),
        Set("Snow day", ThemeManager.Frost, ChromeTexture.Grain, ColorFinish.Frosted, AppearanceOptions.BubbleSoft, AppearanceOptions.ShapePill, ChatCanvasOverlay.Spotlight, TargetAura.Frost, MessageMotion.Pop,
            d => { d.Snow = true; d.TitleSparkles = true; d.HeaderStarlight = true; d.RailStardust = true; d.SidebarMotes = true; d.ButtonBounce = true; }),
        Set("Firefly forest", ThemeManager.Forest, ChromeTexture.Leaves, ColorFinish.SoftMatte, AppearanceOptions.BubbleSoft, AppearanceOptions.ShapeRounded, ChatCanvasOverlay.Vignette, TargetAura.Aura, MessageMotion.Fade,
            d => { d.Fireflies = true; d.HeaderSilk = true; d.RailAurora = true; d.SidebarMotes = true; d.ButtonLift = true; }),
        Set("Retro wave", ThemeManager.Synthwave, ChromeTexture.Grid, ColorFinish.Vivid, AppearanceOptions.BubbleGradient, AppearanceOptions.ShapePill, ChatCanvasOverlay.FullBurst, TargetAura.Neon, MessageMotion.Slide,
            d => { d.Bokeh = true; d.TitleEqualizer = true; d.HeaderPlasma = true; d.RailPlasma = true; d.SidebarPrism = true; d.ButtonGlow = true; d.ButtonBounce = true; }),
        Set("Under the sea", ThemeManager.Ocean, ChromeTexture.Bubbles, ColorFinish.Watercolor, AppearanceOptions.BubbleGlass, AppearanceOptions.ShapeTail, ChatCanvasOverlay.Aurora, TargetAura.Aura, MessageMotion.Pop,
            d => { d.Bubbles = true; d.TitleWave = true; d.HeaderRipples = true; d.RailCascade = true; d.SidebarWaves = true; d.ButtonBounce = true; }),
        Set("Northern lights", ThemeManager.Aurora, ChromeTexture.StarryNight, ColorFinish.Dusk, AppearanceOptions.BubbleGlass, AppearanceOptions.ShapeRounded, ChatCanvasOverlay.Aurora, TargetAura.Prism, MessageMotion.Fade,
            d => { d.Constellation = true; d.TitleAurora = true; d.HeaderAurora = true; d.RailAurora = true; d.SidebarAurora = true; }),
        Set("Hacker den", ThemeManager.Cipher, ChromeTexture.Cipher, ColorFinish.Off, AppearanceOptions.BubbleOutlined, AppearanceOptions.ShapeSharp, ChatCanvasOverlay.Vignette, TargetAura.Electric, MessageMotion.Slide,
            d => { d.Matrix = true; d.TitleLaser = true; d.HeaderCircuit = true; d.RailScan = true; d.SidebarHud = true; d.ButtonSpark = true; }),
        Set("Iron lab", ThemeManager.ArcReactor, ChromeTexture.ArcReactor, ColorFinish.Cinema, AppearanceOptions.BubbleReactor, AppearanceOptions.ShapeSubtle, ChatCanvasOverlay.ArcCore, TargetAura.ArcReactor, MessageMotion.Pop,
            d => { d.Repulsor = true; d.TitleReactor = true; d.HeaderArc = true; d.RailArc = true; d.SidebarHud = true; d.ButtonRing = true; d.ButtonGlow = true; }),
        Set("Sky scouts", ThemeManager.FreedomWings, ChromeTexture.FreedomWings, ColorFinish.Stylized, AppearanceOptions.BubbleMecha, AppearanceOptions.ShapeSubtle, ChatCanvasOverlay.Diagonal, TargetAura.WingFlare, MessageMotion.Slide,
            d => { d.WingPlumes = true; d.TitleWings = true; d.HeaderBurst = true; d.RailWings = true; d.SidebarWings = true; d.ButtonLift = true; }),
        Set("Golden hour", ThemeManager.Sunset, ChromeTexture.Sun, ColorFinish.Cream, AppearanceOptions.BubbleGradient, AppearanceOptions.ShapeRounded, ChatCanvasOverlay.Spotlight, TargetAura.Embers, MessageMotion.Fade,
            d => { d.Bokeh = true; d.TitleShimmer = true; d.HeaderPrism = true; d.RailBeacon = true; d.SidebarGlow = true; d.ButtonLift = true; }),
        Set("Moonlit", ThemeManager.Obsidian, ChromeTexture.Moon, ColorFinish.Velvet, AppearanceOptions.BubbleSoft, AppearanceOptions.ShapeRounded, ChatCanvasOverlay.Vignette, TargetAura.Arcane, MessageMotion.Fade,
            d => { d.Starfield = true; d.HeaderStarlight = true; d.RailStardust = true; d.SidebarNebula = true; }),
        Set("Coffee shop", ThemeManager.Espresso, ChromeTexture.Woven, ColorFinish.Cream, AppearanceOptions.BubbleSolid, AppearanceOptions.ShapeRounded, ChatCanvasOverlay.Vignette, TargetAura.Smoke, MessageMotion.Fade,
            d => { d.Bokeh = true; d.HeaderSilk = true; d.SidebarGlow = true; d.ButtonLift = true; }),
        Set("Tea garden", ThemeManager.Matcha, ChromeTexture.Botanical, ColorFinish.SoftMatte, AppearanceOptions.BubbleSoft, AppearanceOptions.ShapePill, ChatCanvasOverlay.Mesh, TargetAura.Aura, MessageMotion.Pop,
            d => { d.Petals = true; d.HeaderSilk = true; d.RailAurora = true; d.SidebarMotes = true; d.ButtonBounce = true; }),
        Set("Arcade night", ThemeManager.Arcade, ChromeTexture.Hex, ColorFinish.Vivid, AppearanceOptions.BubbleSolid, AppearanceOptions.ShapeSharp, ChatCanvasOverlay.FullBurst, TargetAura.Electric, MessageMotion.Pop,
            d => { d.Starfield = true; d.TitleEqualizer = true; d.HeaderBurst = true; d.RailPulse = true; d.SidebarPrism = true; d.ButtonBounce = true; d.ButtonSpark = true; }),
        Set("Clockwork", ThemeManager.Steampunk, ChromeTexture.Brushed, ColorFinish.Vintage, AppearanceOptions.BubbleSolid, AppearanceOptions.ShapeSubtle, ChatCanvasOverlay.Vignette, TargetAura.Embers, MessageMotion.Fade,
            d => { d.Embers = true; d.TitleShimmer = true; d.HeaderPulse = true; d.RailBeacon = true; d.SidebarSweep = true; }),
        Set("Bubblegum pop", ThemeManager.Bubblegum, ChromeTexture.Terrazzo, ColorFinish.Pastel, AppearanceOptions.BubbleGradient, AppearanceOptions.ShapePill, ChatCanvasOverlay.Mesh, TargetAura.Prism, MessageMotion.Pop,
            d => { d.Bubbles = true; d.TitleSparkles = true; d.HeaderPrism = true; d.RailPulse = true; d.SidebarPrism = true; d.ButtonBounce = true; d.ButtonGlow = true; }),
        Set("Paper notebook", ThemeManager.Paper, ChromeTexture.Grid, ColorFinish.Off, AppearanceOptions.BubbleOutlined, AppearanceOptions.ShapeSubtle, ChatCanvasOverlay.Off, TargetAura.Off, MessageMotion.Fade,
            d => { }, details: false),
        Set("Gothic castle", ThemeManager.Vampire, ChromeTexture.Gothic, ColorFinish.Velvet, AppearanceOptions.BubbleSolid, AppearanceOptions.ShapeSharp, ChatCanvasOverlay.Vignette, TargetAura.Smoke, MessageMotion.Fade,
            d => { d.Embers = true; d.HeaderNebula = true; d.RailPulse = true; d.SidebarSweep = true; }),
        Set("Storm chaser", ThemeManager.Cobalt, ChromeTexture.Lightning, ColorFinish.Cinema, AppearanceOptions.BubbleGlass, AppearanceOptions.ShapeSubtle, ChatCanvasOverlay.Diagonal, TargetAura.Electric, MessageMotion.Slide,
            d => { d.Rain = true; d.TitleLaser = true; d.HeaderPlasma = true; d.RailPlasma = true; d.SidebarRipples = true; d.ButtonSpark = true; }),
        Set("Black & gold", ThemeManager.NoirGold, ChromeTexture.Marble, ColorFinish.Velvet, AppearanceOptions.BubbleSolid, AppearanceOptions.ShapeRounded, ChatCanvasOverlay.Spotlight, TargetAura.Aura, MessageMotion.Fade,
            d => { d.Bokeh = true; d.TitleShimmer = true; d.HeaderSilk = true; d.SidebarGlow = true; d.ButtonRing = true; }),
        Set("Meadow", ThemeManager.Sage, ChromeTexture.GrassyFields, ColorFinish.SoftMatte, AppearanceOptions.BubbleSoft, AppearanceOptions.ShapeRounded, ChatCanvasOverlay.Spotlight, TargetAura.Off, MessageMotion.Fade,
            d => { d.Fireflies = true; d.HeaderSilk = true; d.SidebarMotes = true; }),
        Set("Spider web", ThemeManager.WebCrimson, ChromeTexture.Web, ColorFinish.Stylized, AppearanceOptions.BubbleMecha, AppearanceOptions.ShapeSubtle, ChatCanvasOverlay.Diagonal, TargetAura.Neon, MessageMotion.Slide,
            d => { d.Constellation = true; d.TitleLaser = true; d.HeaderBurst = true; d.RailScan = true; d.SidebarRipples = true; d.ButtonSpark = true; }),
        Set("Minimal light", ThemeManager.Light, ChromeTexture.Off, ColorFinish.Off, AppearanceOptions.BubbleMinimal, AppearanceOptions.ShapeSubtle, ChatCanvasOverlay.Off, TargetAura.Off, MessageMotion.Off,
            d => { }, details: false),
        Set("Minimal dark", ThemeManager.Dark, ChromeTexture.Off, ColorFinish.Off, AppearanceOptions.BubbleMinimal, AppearanceOptions.ShapeSubtle, ChatCanvasOverlay.Off, TargetAura.Off, MessageMotion.Off,
            d => { }, details: false)
    ];
}

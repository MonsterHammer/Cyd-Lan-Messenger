using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

public enum ChromeTextureKind
{
    Off,
    Grain, Woven, Topo, Grid, Silk, Hex,
    Brushed, Marble, Carbon, Circuit, Waves, ArtDeco, Terrazzo, Mesh,
    Bubbles, Gothic, Lightning, Web, Starburst, Botanical, Cipher, Watercolor,
    FreedomWings, ArcReactor,
    Leaves, LonelyGrass, GrassyFields, Galaxy, StarryNight, Sun, Moon,
    Aurora, Clouds, Mountains, Ocean, Dunes, Sakura, Bamboo, Pines, Skyline, Snowfall, Lanterns, Rain, Plaid, Chevron
}

public sealed record ChromeTextureSwatch(string Value, string Name, ChromeTextureKind Kind);

/// <summary>Renders a single continuous surface texture behind unified chrome regions (title bar, side nav, side panel, and header).</summary>
public sealed partial class ChromeTexture : FrameworkElement
{
    public const string Off = "off";
    public const string Grain = "grain";
    public const string Woven = "woven";
    public const string Topo = "topo";
    public const string Grid = "grid";
    public const string Silk = "silk";
    public const string Hex = "hex";
    public const string Brushed = "brushed";
    public const string Marble = "marble";
    public const string Carbon = "carbon";
    public const string Circuit = "circuit";
    public const string Waves = "waves";
    public const string ArtDeco = "artdeco";
    public const string Terrazzo = "terrazzo";
    public const string Mesh = "mesh";
    public const string Bubbles = "bubbles";
    public const string Gothic = "gothic";
    public const string Lightning = "lightning";
    public const string Web = "web";
    public const string Starburst = "starburst";
    public const string Botanical = "botanical";
    public const string Leaves = "leaves";
    public const string LonelyGrass = "lonelygrass";
    public const string GrassyFields = "grassyfields";
    public const string Galaxy = "galaxy";
    public const string StarryNight = "starrynight";
    public const string Sun = "sun";
    public const string Moon = "moon";
    public const string Cipher = "cipher";
    public const string Watercolor = "watercolor";
    public const string FreedomWings = "freedomwings";
    public const string ArcReactor = "arcreactor";
    public const string Aurora = "aurora";
    public const string Clouds = "clouds";
    public const string Mountains = "mountains";
    public const string Ocean = "ocean";
    public const string Dunes = "dunes";
    public const string Sakura = "sakura";
    public const string Bamboo = "bamboo";
    public const string Pines = "pines";
    public const string Skyline = "skyline";
    public const string Snowfall = "snowfall";
    public const string Lanterns = "lanterns";
    public const string Rain = "rain";
    public const string Plaid = "plaid";
    public const string Chevron = "chevron";

    public static IReadOnlyList<ChromeTextureSwatch> Catalog { get; } =
    [
        new(Off, "None", ChromeTextureKind.Off),
        new(Grain, "Frosted grain", ChromeTextureKind.Grain),
        new(Brushed, "Brushed metal", ChromeTextureKind.Brushed),
        new(Marble, "Marble veins", ChromeTextureKind.Marble),
        new(Silk, "Flowing silk", ChromeTextureKind.Silk),
        new(Topo, "Topographic", ChromeTextureKind.Topo),
        new(Watercolor, "Watercolor", ChromeTextureKind.Watercolor),
        new(Terrazzo, "Terrazzo", ChromeTextureKind.Terrazzo),
        new(Circuit, "Circuit traces", ChromeTextureKind.Circuit),
        new(Bubbles, "Reef bubbles", ChromeTextureKind.Bubbles),
        new(Gothic, "Gothic filigree", ChromeTextureKind.Gothic),
        new(Lightning, "Lightning veins", ChromeTextureKind.Lightning),
        new(Web, "Cobweb silk", ChromeTextureKind.Web),
        new(Starburst, "Starburst", ChromeTextureKind.Starburst),
        new(Galaxy, "Galaxy", ChromeTextureKind.Galaxy),
        new(StarryNight, "Starry night", ChromeTextureKind.StarryNight),
        new(Sun, "Sun", ChromeTextureKind.Sun),
        new(Moon, "Moon", ChromeTextureKind.Moon),
        new(Botanical, "Botanical", ChromeTextureKind.Botanical),
        new(Leaves, "Leaves", ChromeTextureKind.Leaves),
        new(LonelyGrass, "Lonely grass", ChromeTextureKind.LonelyGrass),
        new(GrassyFields, "Grassy fields", ChromeTextureKind.GrassyFields),
        new(Sakura, "Sakura branch", ChromeTextureKind.Sakura),
        new(Bamboo, "Bamboo grove", ChromeTextureKind.Bamboo),
        new(Pines, "Pine ridge", ChromeTextureKind.Pines),
        new(Mountains, "Mountains", ChromeTextureKind.Mountains),
        new(Dunes, "Desert dunes", ChromeTextureKind.Dunes),
        new(Ocean, "Ocean swell", ChromeTextureKind.Ocean),
        new(Clouds, "Drifting clouds", ChromeTextureKind.Clouds),
        new(Aurora, "Aurora curtains", ChromeTextureKind.Aurora),
        new(Snowfall, "Snowfall", ChromeTextureKind.Snowfall),
        new(Rain, "Rain streaks", ChromeTextureKind.Rain),
        new(Lanterns, "Lanterns", ChromeTextureKind.Lanterns),
        new(Skyline, "City night", ChromeTextureKind.Skyline),
        new(Cipher, "Cipher streams", ChromeTextureKind.Cipher),
        new(FreedomWings, "Freedom wings", ChromeTextureKind.FreedomWings),
        new(ArcReactor, "Arc reactor", ChromeTextureKind.ArcReactor),
        new(Woven, "Woven linen", ChromeTextureKind.Woven),
        new(Grid, "Drafting grid", ChromeTextureKind.Grid),
        new(Hex, "Honeycomb", ChromeTextureKind.Hex),
        new(Carbon, "Carbon fiber", ChromeTextureKind.Carbon),
        new(Waves, "Seigaiha waves", ChromeTextureKind.Waves),
        new(ArtDeco, "Art Deco", ChromeTextureKind.ArtDeco),
        new(Mesh, "Starlight mesh", ChromeTextureKind.Mesh),
        new(Plaid, "Tartan plaid", ChromeTextureKind.Plaid),
        new(Chevron, "Chevron", ChromeTextureKind.Chevron)
    ];

    private static readonly Dictionary<string, ChromeTextureKind> ByValue =
        Catalog.Where(t => t.Kind != ChromeTextureKind.Off).ToDictionary(t => t.Value, t => t.Kind);

    public static readonly DependencyProperty MotionProperty = DependencyProperty.Register(
        nameof(Motion), typeof(bool), typeof(ChromeTexture),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, InvalidateTexture));

    /// <summary>Animates the texture (swaying grass, drifting or scrolling layers, twinkles).</summary>
    public bool Motion { get => (bool)GetValue(MotionProperty); set => SetValue(MotionProperty, value); }

    public static readonly DependencyProperty TextureProperty = DependencyProperty.Register(
        nameof(Texture), typeof(string), typeof(ChromeTexture),
        new FrameworkPropertyMetadata(Off, FrameworkPropertyMetadataOptions.AffectsRender, OnTextureStringChanged));

    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(ChromeTextureKind), typeof(ChromeTexture),
        new FrameworkPropertyMetadata(ChromeTextureKind.Off, FrameworkPropertyMetadataOptions.AffectsRender, InvalidateTexture));

    public static readonly DependencyProperty IntensityProperty = DependencyProperty.Register(
        nameof(Intensity), typeof(double), typeof(ChromeTexture),
        new FrameworkPropertyMetadata(.65, FrameworkPropertyMetadataOptions.AffectsRender, InvalidateTexture));

    public static readonly DependencyProperty TintProperty = DependencyProperty.Register(
        nameof(Tint), typeof(Color), typeof(ChromeTexture),
        new FrameworkPropertyMetadata(Color.FromRgb(59, 122, 140), FrameworkPropertyMetadataOptions.AffectsRender, InvalidateTexture));

    public static readonly DependencyProperty SurfaceColorProperty = DependencyProperty.Register(
        nameof(SurfaceColor), typeof(Color), typeof(ChromeTexture),
        new FrameworkPropertyMetadata(Color.FromRgb(247, 248, 248), FrameworkPropertyMetadataOptions.AffectsRender, InvalidateTexture));

    public static readonly DependencyProperty LineColorProperty = DependencyProperty.Register(
        nameof(LineColor), typeof(Color), typeof(ChromeTexture),
        new FrameworkPropertyMetadata(Color.FromRgb(225, 228, 231), FrameworkPropertyMetadataOptions.AffectsRender, InvalidateTexture));

    private IReadOnlyList<ChromeTextureKind> stack = [];
    private readonly Dictionary<ChromeTextureKind, TextureLayer> layers = [];
    private DrawingGroup? cachedVector;
    private Brush? cachedTile;
    private Brush? cachedWash;
    private ChromeTextureKind cachedKind;
    private double cachedWidth;
    private double cachedHeight;
    private double cachedIntensity = -1;
    private Color cachedTint;
    private Color cachedSurface;

    public string Texture { get => (string)GetValue(TextureProperty); set => SetValue(TextureProperty, value); }
    public ChromeTextureKind Kind { get => (ChromeTextureKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public double Intensity { get => (double)GetValue(IntensityProperty); set => SetValue(IntensityProperty, value); }
    public Color Tint { get => (Color)GetValue(TintProperty); set => SetValue(TintProperty, value); }
    public Color SurfaceColor { get => (Color)GetValue(SurfaceColorProperty); set => SetValue(SurfaceColorProperty, value); }
    public Color LineColor { get => (Color)GetValue(LineColorProperty); set => SetValue(LineColorProperty, value); }

    public ChromeTexture()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        SetResourceReference(TintProperty, "EffectColor");
        SetResourceReference(SurfaceColorProperty, "ChromeSurfaceColor");
        SetResourceReference(LineColorProperty, "ChromeLineColor");
    }

    /// <summary>Normalizes one texture or a comma-separated stack of textures; returns "off" when none are valid.</summary>
    public static string Normalize(string? value)
    {
        var list = NormalizeList(value);
        return list.Count == 0 ? Off : string.Join(",", list);
    }

    public static List<string> NormalizeList(string? value)
    {
        var result = new List<string>();
        foreach (var raw in (value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string key = raw.ToLowerInvariant().Replace(" ", "").Replace("-", "");
            if (ByValue.ContainsKey(key) && !result.Contains(key)) result.Add(key);
        }
        return result;
    }

    public static ChromeTextureKind KindFor(string? value) =>
        NormalizeList(value) is { Count: > 0 } list ? ByValue[list[0]] : ChromeTextureKind.Off;

    public static IReadOnlyList<ChromeTextureKind> KindsFor(string? value) =>
        NormalizeList(value).Select(v => ByValue[v]).ToList();

    /// <summary>Only intentionally geometric weaves/grids repeat as tiles; organic and thematic textures render as full-canvas non-repeating compositions.</summary>
    private static bool IsRepeatingPattern(ChromeTextureKind kind) => kind is
        ChromeTextureKind.Woven or ChromeTextureKind.Grid or ChromeTextureKind.Hex or
        ChromeTextureKind.Carbon or ChromeTextureKind.Waves or ChromeTextureKind.ArtDeco or ChromeTextureKind.Mesh or
        ChromeTextureKind.Plaid or ChromeTextureKind.Chevron;

    private static void OnTextureStringChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var texture = (ChromeTexture)d;
        texture.stack = KindsFor(e.NewValue as string);
        texture.Kind = texture.stack.Count > 0 ? texture.stack[0] : ChromeTextureKind.Off;
        texture.InvalidateCache();
    }

    private static void InvalidateTexture(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((ChromeTexture)d).InvalidateCache();
    }

    private void InvalidateCache()
    {
        layers.Clear();
        ResetScratch();
    }

    private void ResetScratch()
    {
        cachedVector = null;
        cachedTile = null;
        cachedWash = null;
        cachedIntensity = -1;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double width = ActualWidth, height = ActualHeight;
        if (width < 8 || height < 8) return;
        IReadOnlyList<ChromeTextureKind> kinds = stack.Count > 0 ? stack : Kind != ChromeTextureKind.Off ? [Kind] : [];
        if (kinds.Count == 0) return;

        double intensity = Math.Clamp(Intensity, .1, 1);
        // Stacked layers share the surface, so each one is drawn a little quieter.
        if (kinds.Count > 1) intensity = Math.Max(.1, intensity * (kinds.Count == 2 ? .85 : .72));
        bool motion = Motion && SystemParameters.ClientAreaAnimation;

        var rect = new Rect(0, 0, width, height);
        Brush? wash = null;
        foreach (var kind in kinds)
        {
            var layer = GetLayer(kind, width, height, intensity, motion);
            wash ??= layer.Wash;
            DrawLayer(dc, layer, rect);
        }
        if (wash is not null) dc.DrawRectangle(wash, null, rect);
    }

    private void EnsureCache(ChromeTextureKind kind, double width, double height, double intensity)
    {
        bool sizeMatters = !IsRepeatingPattern(kind);
        bool sameSize = !sizeMatters || (Math.Abs(width - cachedWidth) < .5 && Math.Abs(height - cachedHeight) < .5);
        bool sameKey = sameSize && cachedKind == kind && Math.Abs(cachedIntensity - intensity) < .001 &&
            cachedTint == Tint && cachedSurface == SurfaceColor && (cachedTile is not null || cachedVector is not null);
        if (sameKey) return;

        ResetScratch();
        cachedKind = kind;
        cachedWidth = width;
        cachedHeight = height;
        cachedIntensity = intensity;
        cachedTint = Tint;
        cachedSurface = SurfaceColor;

        double luma = (0.2126 * SurfaceColor.R + 0.7152 * SurfaceColor.G + 0.0722 * SurfaceColor.B) / 255.0;
        bool isLight = luma >= .50;
        Color hi = isLight ? Colors.White : EffectPalette.Blend(Tint, Colors.White, .72);
        Color lo = isLight ? EffectPalette.Blend(SurfaceColor, Colors.Black, .58) : Colors.Black;
        Color accent = EffectPalette.Blend(Tint, isLight ? lo : Colors.White, isLight ? .18 : .22);
        bool compact = width < 180 || height < 100;

        cachedWash = BuildAmbientWash(hi, lo, accent, isLight, intensity, kind);

        switch (kind)
        {
            // Organic / randomized full-canvas non-repeating textures
            case ChromeTextureKind.Grain:
                cachedTile = BuildGrainBrush(hi, lo, accent, isLight, intensity);
                cachedVector = BuildGrainCloudsDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Brushed:
                cachedVector = BuildBrushedDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Topo:
                cachedVector = BuildTopoDrawing(width, height, hi, lo, accent, isLight, intensity);
                break;
            case ChromeTextureKind.Silk:
                cachedVector = BuildSilkDrawing(width, height, hi, lo, accent, isLight, intensity);
                break;
            case ChromeTextureKind.Marble:
                cachedVector = BuildMarbleDrawing(width, height, hi, lo, accent, isLight, intensity);
                break;
            case ChromeTextureKind.Circuit:
                cachedVector = BuildCircuitDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Terrazzo:
                cachedVector = BuildTerrazzoDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Bubbles:
                cachedVector = BuildBubblesDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Gothic:
                cachedVector = BuildGothicDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Lightning:
                cachedVector = BuildLightningDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Web:
                cachedVector = BuildWebDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Starburst:
                cachedVector = BuildStarburstDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Botanical:
                cachedVector = BuildBotanicalDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Leaves:
                cachedVector = BuildLeavesDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.LonelyGrass:
                cachedVector = BuildLonelyGrassDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.GrassyFields:
                cachedVector = BuildGrassyFieldsDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Galaxy:
                cachedVector = BuildGalaxyDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.StarryNight:
                cachedVector = BuildStarryNightDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Sun:
                cachedVector = BuildSunDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Moon:
                cachedVector = BuildMoonDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Cipher:
                cachedVector = BuildCipherDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Watercolor:
                cachedVector = BuildWatercolorDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.FreedomWings:
                cachedVector = BuildFreedomWingsDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.ArcReactor:
                cachedVector = BuildArcReactorDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Aurora:
                cachedVector = BuildAuroraDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Clouds:
                cachedVector = BuildCloudsDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Mountains:
                cachedVector = BuildMountainsDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Ocean:
                cachedVector = BuildOceanDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Dunes:
                cachedVector = BuildDunesDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Sakura:
                cachedVector = BuildSakuraDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Bamboo:
                cachedVector = BuildBambooDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Pines:
                cachedVector = BuildPinesDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Skyline:
                cachedVector = BuildSkylineDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Snowfall:
                cachedVector = BuildSnowfallDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Lanterns:
                cachedVector = BuildLanternsDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Rain:
                cachedVector = BuildRainDrawing(width, height, hi, lo, accent, isLight, intensity, compact);
                break;

            // Intentionally geometric repeating textures
            case ChromeTextureKind.Woven:
                cachedTile = BuildWovenBrush(hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Grid:
                cachedTile = BuildGridBrush(hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Hex:
                cachedTile = BuildHexBrush(hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Carbon:
                cachedTile = BuildCarbonBrush(hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Waves:
                cachedTile = BuildWavesBrush(hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.ArtDeco:
                cachedTile = BuildArtDecoBrush(hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Mesh:
                cachedTile = BuildMeshBrush(hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Plaid:
                cachedTile = BuildPlaidBrush(hi, lo, accent, isLight, intensity, compact);
                break;
            case ChromeTextureKind.Chevron:
                cachedTile = BuildChevronBrush(hi, lo, accent, isLight, intensity, compact);
                break;
        }
        if (cachedVector is { CanFreeze: true }) cachedVector.Freeze();
    }

    private static Brush BuildAmbientWash(Color hi, Color lo, Color accent, bool isLight, double intensity, ChromeTextureKind kind)
    {
        byte topAlpha = (byte)Math.Round((isLight ? 40 : 16) * intensity);
        byte midAlpha = (byte)Math.Round((isLight ? 12 : 14) * intensity);
        byte edgeAlpha = (byte)Math.Round((isLight ? 16 : 32) * intensity);
        if (kind is ChromeTextureKind.Silk or ChromeTextureKind.Watercolor
            or ChromeTextureKind.Galaxy or ChromeTextureKind.StarryNight
            or ChromeTextureKind.Sun or ChromeTextureKind.Moon
            or ChromeTextureKind.LonelyGrass or ChromeTextureKind.GrassyFields)
            topAlpha = (byte)(topAlpha / 2);

        var wash = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        wash.GradientStops.Add(new GradientStop(WithAlpha(hi, topAlpha), 0));
        wash.GradientStops.Add(new GradientStop(WithAlpha(accent, midAlpha), .42));
        wash.GradientStops.Add(new GradientStop(WithAlpha(lo, edgeAlpha), 1));
        wash.Freeze();
        return wash;
    }

    // ---------------- Randomized, Non-Repeating Full-Canvas Textures ----------------

    private static Brush BuildGrainBrush(Color hi, Color lo, Color accent, bool isLight, double intensity)
    {
        const int size = 192;
        byte[] pixels = new byte[size * size * 4];
        double strength = .40 + .56 * intensity;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                double coarse = SmoothTileNoise(x * 6.0 / size, y * 6.0 / size, 6);
                double medium = SmoothTileNoise(x * 24.0 / size, y * 24.0 / size, 24);
                double fine = Hash2D(x, y) * 2.0 - 1.0;
                double micro = (Hash2D((x + 29) % size, (y + 47) % size)
                    + Hash2D((x + size - 13) % size, (y + 19) % size) - 1.0) * .5;
                double value = (fine * .58 + micro * .22 + (medium - .5) * .32 + (coarse - .5) * .22) * strength;

                int idx = (y * size + x) * 4;
                if (value >= 0)
                {
                    Color c = (x + y) % 3 == 0 ? EffectPalette.Blend(hi, accent, .26) : hi;
                    byte a = (byte)Math.Clamp(value * (isLight ? 44 : 28), 0, 80);
                    WritePremul(pixels, idx, c, a);
                }
                else
                {
                    byte a = (byte)Math.Clamp(-value * (isLight ? 20 : 40), 0, 80);
                    WritePremul(pixels, idx, lo, a);
                }
            }
        }

        var bmp = BitmapSource.Create(size, size, 96, 96, PixelFormats.Pbgra32, null, pixels, size * 4);
        bmp.Freeze();
        var brush = new ImageBrush(bmp)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, size, size),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None
        };
        brush.Freeze();
        return brush;
    }

    private static DrawingGroup BuildGrainCloudsDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();
        var rng = new Random(1907);
        int blooms = compact ? 3 : 6;
        for (int i = 0; i < blooms; i++)
        {
            double cx = rng.NextDouble();
            double cy = rng.NextDouble();
            var radial = new RadialGradientBrush
            {
                Center = new Point(cx, cy),
                GradientOrigin = new Point(cx, cy),
                RadiusX = .26 + rng.NextDouble() * .34,
                RadiusY = .24 + rng.NextDouble() * .34
            };
            Color col = i % 3 == 0 ? accent : i % 2 == 0 ? hi : lo;
            byte a = (byte)Math.Round((i % 3 == 0 ? (isLight ? 20 : 18) : (isLight ? 28 : 20)) * intensity);
            radial.GradientStops.Add(new GradientStop(WithAlpha(col, a), 0));
            radial.GradientStops.Add(new GradientStop(WithAlpha(col, 0), 1));
            radial.Freeze();
            dc.DrawRectangle(radial, null, new Rect(0, 0, width, height));
        }
        return group;
    }

    private static DrawingGroup BuildBrushedDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();

        // Asymmetric anodized metallic sheen bands across full canvas
        var sheen = new LinearGradientBrush { StartPoint = new Point(0, .15), EndPoint = new Point(1, .85) };
        sheen.GradientStops.Add(new GradientStop(WithAlpha(lo, (byte)Math.Round((isLight ? 14 : 32) * intensity)), 0));
        sheen.GradientStops.Add(new GradientStop(WithAlpha(hi, (byte)Math.Round((isLight ? 64 : 32) * intensity)), .28));
        sheen.GradientStops.Add(new GradientStop(WithAlpha(accent, (byte)Math.Round((isLight ? 20 : 22) * intensity)), .56));
        sheen.GradientStops.Add(new GradientStop(WithAlpha(hi, (byte)Math.Round((isLight ? 46 : 24) * intensity)), .78));
        sheen.GradientStops.Add(new GradientStop(WithAlpha(lo, (byte)Math.Round((isLight ? 18 : 38) * intensity)), 1));
        sheen.Freeze();
        dc.DrawRectangle(sheen, null, new Rect(0, 0, width, height));

        // Non-repeating randomized micro-brushed horizontal filaments of varying length & tone
        var rng = new Random(3319);
        var hiPen = FrozenPen(WithAlpha(hi, (byte)Math.Round((isLight ? 62 : 28) * intensity)), .75);
        var loPen = FrozenPen(WithAlpha(lo, (byte)Math.Round((isLight ? 16 : 36) * intensity)), .75);
        var accPen = FrozenPen(WithAlpha(accent, (byte)Math.Round((isLight ? 24 : 26) * intensity)), .7);
        int lines = compact ? 36 : Math.Clamp((int)(height * .48), 90, 260);
        for (int i = 0; i < lines; i++)
        {
            double y = rng.NextDouble() * height;
            double x0 = (rng.NextDouble() - .15) * width;
            double len = width * (.18 + rng.NextDouble() * .68);
            var pen = i % 7 == 0 ? accPen : (i % 2 == 0 ? hiPen : loPen);
            dc.DrawLine(pen, new Point(Math.Max(0, x0), y), new Point(Math.Min(width, x0 + len), y));
        }
        return group;
    }

    private static DrawingGroup BuildCircuitDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();
        var rng = new Random(5827);

        var tracePen = FrozenPen(WithAlpha(accent, (byte)Math.Round((isLight ? 42 : 48) * intensity)), compact ? .9 : 1.1);
        var subPen = FrozenPen(WithAlpha(isLight ? lo : hi, (byte)Math.Round((isLight ? 20 : 24) * intensity)), .85);
        var padFill = FrozenBrush(WithAlpha(hi, (byte)Math.Round((isLight ? 84 : 48) * intensity)));
        var padStroke = FrozenPen(WithAlpha(accent, (byte)Math.Round((isLight ? 60 : 66) * intensity)), 1.05);
        var chipFill = FrozenBrush(WithAlpha(accent, (byte)Math.Round((isLight ? 16 : 18) * intensity)));

        int traces = compact ? 7 : Math.Clamp((int)((width + height) / 48), 18, 42);
        double step = compact ? 10 : 18;
        for (int i = 0; i < traces; i++)
        {
            bool horizontal = rng.Next(3) != 0;
            double x = Math.Round(rng.NextDouble() * width / step) * step;
            double y = Math.Round(rng.NextDouble() * height / step) * step;
            var geom = new StreamGeometry();
            Point endPt;
            using (var ctx = geom.Open())
            {
                ctx.BeginFigure(new Point(x, y), isFilled: false, isClosed: false);
                int segs = 2 + rng.Next(4);
                for (int s = 0; s < segs; s++)
                {
                    double len = (1 + rng.Next(compact ? 3 : 6)) * step;
                    int dir = rng.Next(2) == 0 ? 1 : -1;
                    if (s % 2 == 1)
                    {
                        double diag = (1 + rng.Next(2)) * step;
                        x += dir * diag;
                        y += (rng.Next(2) == 0 ? 1 : -1) * diag;
                    }
                    else if (horizontal) x += dir * len;
                    else y += dir * len;
                    x = Math.Clamp(x, 4, width - 4);
                    y = Math.Clamp(y, 4, height - 4);
                    ctx.LineTo(new Point(x, y), isStroked: true, isSmoothJoin: false);
                }
                endPt = new Point(x, y);
            }
            geom.Freeze();
            dc.DrawGeometry(null, i % 3 == 0 ? tracePen : subPen, geom);
            double r = compact ? 1.8 : 2.5;
            dc.DrawEllipse(padFill, padStroke, endPt, r, r);
        }

        // Occasional non-repeating IC chip outlines
        int chips = compact ? 1 : 4;
        for (int c = 0; c < chips; c++)
        {
            double cw = compact ? 14 : 22 + rng.Next(18);
            double ch = compact ? 10 : 14 + rng.Next(12);
            double cx = 12 + rng.NextDouble() * Math.Max(8, width - cw - 24);
            double cy = 12 + rng.NextDouble() * Math.Max(8, height - ch - 24);
            dc.DrawRoundedRectangle(chipFill, subPen, new Rect(cx, cy, cw, ch), 2, 2);
        }
        return group;
    }

    private static DrawingGroup BuildTerrazzoDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();
        var rng = new Random(4019);
        var fills = new[]
        {
            FrozenBrush(WithAlpha(accent, (byte)Math.Round((isLight ? 48 : 44) * intensity))),
            FrozenBrush(WithAlpha(hi, (byte)Math.Round((isLight ? 88 : 38) * intensity))),
            FrozenBrush(WithAlpha(lo, (byte)Math.Round((isLight ? 24 : 54) * intensity))),
            FrozenBrush(WithAlpha(EffectPalette.Blend(accent, hi, .5), (byte)Math.Round((isLight ? 44 : 36) * intensity))),
            FrozenBrush(WithAlpha(ShiftColor(accent), (byte)Math.Round((isLight ? 36 : 34) * intensity)))
        };

        int count = compact ? 22 : Math.Clamp((int)(width * height / 3400), 65, 220);
        for (int i = 0; i < count; i++)
        {
            double cx = rng.NextDouble() * width;
            double cy = rng.NextDouble() * height;
            double rx = (compact ? 2.2 : 3.2) + rng.NextDouble() * (compact ? 4.2 : 9.5);
            double ry = rx * (.42 + rng.NextDouble() * .48);
            double rot = rng.NextDouble() * Math.Tau;
            var chip = new StreamGeometry();
            using (var ctx = chip.Open())
            {
                int sides = 4 + rng.Next(4);
                for (int s = 0; s < sides; s++)
                {
                    double a = rot + s * Math.Tau / sides + (rng.NextDouble() - .5) * .35;
                    var pt = new Point(cx + Math.Cos(a) * rx, cy + Math.Sin(a) * ry);
                    if (s == 0) ctx.BeginFigure(pt, isFilled: true, isClosed: true);
                    else ctx.LineTo(pt, isStroked: false, isSmoothJoin: false);
                }
            }
            chip.Freeze();
            dc.DrawGeometry(fills[rng.Next(fills.Length)], null, chip);
        }
        return group;
    }

    private static DrawingGroup BuildBubblesDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();
        var rng = new Random(7711);

        var rimPen = FrozenPen(WithAlpha(accent, (byte)Math.Round((isLight ? 52 : 54) * intensity)), compact ? .9 : 1.15);
        var hiPen = FrozenPen(WithAlpha(hi, (byte)Math.Round((isLight ? 92 : 48) * intensity)), .95);
        var fillSoft = FrozenBrush(WithAlpha(accent, (byte)Math.Round((isLight ? 18 : 16) * intensity)));
        var glint = FrozenBrush(WithAlpha(hi, (byte)Math.Round((isLight ? 120 : 72) * intensity)));

        int count = compact ? 14 : Math.Clamp((int)(width * height / 6200), 34, 110);
        for (int i = 0; i < count; i++)
        {
            double cx = rng.NextDouble() * width;
            double cy = rng.NextDouble() * height;
            double r = (compact ? 2.4 : 4.0) + Math.Pow(rng.NextDouble(), 1.8) * (compact ? 7.5 : 22.0);
            dc.DrawEllipse(i % 3 == 0 ? fillSoft : null, i % 2 == 0 ? rimPen : hiPen, new Point(cx, cy), r, r);
            if (r >= 4.5)
            {
                dc.DrawEllipse(glint, null, new Point(cx - r * .32, cy - r * .32), Math.Max(1.0, r * .20), Math.Max(.8, r * .14));
                if (i % 4 == 0)
                    dc.DrawEllipse(null, hiPen, new Point(cx, cy), r * .72, r * .72);
            }
        }
        return group;
    }

    private static DrawingGroup BuildGothicDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();
        var rng = new Random(6613);

        var archPen = FrozenPen(WithAlpha(accent, (byte)Math.Round((isLight ? 46 : 56) * intensity)), 1.2);
        var tracePen = FrozenPen(WithAlpha(isLight ? lo : hi, (byte)Math.Round((isLight ? 24 : 30) * intensity)), .85);
        var hiPen = FrozenPen(WithAlpha(hi, (byte)Math.Round((isLight ? 72 : 34) * intensity)), .75);
        var accentFill = FrozenBrush(WithAlpha(accent, (byte)Math.Round((isLight ? 18 : 22) * intensity)));

        // Non-repeating pointed gothic lancet arches at varied heights & positions
        int arches = compact ? 3 : 8;
        for (int i = 0; i < arches; i++)
        {
            double aw = compact ? 22 + rng.Next(14) : 64 + rng.Next(96);
            double ah = compact ? 28 + rng.Next(16) : 110 + rng.Next(180);
            double x = rng.NextDouble() * width;
            double baseY = height * (.45 + rng.NextDouble() * .65);
            var lancet = new StreamGeometry();
            using (var ctx = lancet.Open())
            {
                ctx.BeginFigure(new Point(x - aw * .5, baseY), isFilled: false, isClosed: false);
                ctx.LineTo(new Point(x - aw * .5, baseY - ah * .55), isStroked: true, isSmoothJoin: true);
                ctx.QuadraticBezierTo(new Point(x - aw * .45, baseY - ah * .88), new Point(x, baseY - ah), isStroked: true, isSmoothJoin: true);
                ctx.QuadraticBezierTo(new Point(x + aw * .45, baseY - ah * .88), new Point(x + aw * .5, baseY - ah * .55), isStroked: true, isSmoothJoin: true);
                ctx.LineTo(new Point(x + aw * .5, baseY), isStroked: true, isSmoothJoin: true);
            }
            lancet.Freeze();
            dc.DrawGeometry(null, i % 2 == 0 ? archPen : tracePen, lancet);

            // Rose-window medallion inside the arch apex
            double mr = aw * .20;
            var mc = new Point(x, baseY - ah * .68);
            dc.DrawEllipse(accentFill, hiPen, mc, mr, mr);
            for (int spoke = 0; spoke < 6; spoke++)
            {
                double a = spoke * Math.PI / 3.0;
                dc.DrawLine(tracePen, mc, new Point(mc.X + Math.Cos(a) * mr, mc.Y + Math.Sin(a) * mr));
            }
        }
        return group;
    }

    private static DrawingGroup BuildLightningDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();
        var rng = new Random(9181);

        var glowPen = FrozenPen(WithAlpha(accent, (byte)Math.Round((isLight ? 28 : 34) * intensity)), compact ? 2.6 : 4.2);
        var boltPen = FrozenPen(WithAlpha(accent, (byte)Math.Round((isLight ? 58 : 70) * intensity)), compact ? 1.1 : 1.45);
        var corePen = FrozenPen(WithAlpha(hi, (byte)Math.Round((isLight ? 92 : 64) * intensity)), .75);

        void DrawBranch(Point start, double angle, double length, int depth)
        {
            var geom = new StreamGeometry();
            Point cur = start;
            var forks = new List<(Point P, double Ang, double Len)>();
            int segs = compact ? 5 : 9;
            using (var ctx = geom.Open())
            {
                ctx.BeginFigure(cur, isFilled: false, isClosed: false);
                for (int s = 1; s <= segs; s++)
                {
                    double stepLen = length / segs;
                    double jitter = (rng.NextDouble() - .5) * .95;
                    angle += jitter * .55;
                    cur = new Point(cur.X + Math.Cos(angle) * stepLen, cur.Y + Math.Sin(angle) * stepLen);
                    ctx.LineTo(cur, isStroked: true, isSmoothJoin: false);
                    if (depth > 0 && rng.NextDouble() < .32)
                        forks.Add((cur, angle + (rng.Next(2) == 0 ? 1 : -1) * (.42 + rng.NextDouble() * .48), length * (.38 + rng.NextDouble() * .25)));
                }
            }
            geom.Freeze();
            if (depth > 0) dc.DrawGeometry(null, glowPen, geom);
            dc.DrawGeometry(null, depth > 0 ? boltPen : corePen, geom);
            if (depth > 0) dc.DrawGeometry(null, corePen, geom);

            foreach (var f in forks)
                DrawBranch(f.P, f.Ang, f.Len, depth - 1);
        }

        int bolts = compact ? 2 : 5;
        for (int b = 0; b < bolts; b++)
        {
            bool fromTop = b % 2 == 0;
            var origin = fromTop
                ? new Point(width * (.12 + rng.NextDouble() * .76), 0)
                : new Point(0, height * (.12 + rng.NextDouble() * .76));
            double ang = fromTop ? Math.PI * .35 + rng.NextDouble() * .35 : -.25 + rng.NextDouble() * .55;
            double len = Math.Max(width, height) * (compact ? .65 : .42 + rng.NextDouble() * .32);
            DrawBranch(origin, ang, len, depth: compact ? 1 : 2);
        }
        return group;
    }

    private static DrawingGroup BuildWebDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();
        var rng = new Random(8423);

        var spokePen = FrozenPen(WithAlpha(accent, (byte)Math.Round((isLight ? 44 : 52) * intensity)), .95);
        var silkPen = FrozenPen(WithAlpha(hi, (byte)Math.Round((isLight ? 74 : 38) * intensity)), .8);
        var dewFill = FrozenBrush(WithAlpha(hi, (byte)Math.Round((isLight ? 95 : 68) * intensity)));

        void DrawCornerWeb(Point anchor, double startAngle, double sweepAngle, double maxRadius, int spokes, int rings)
        {
            var angles = new double[spokes];
            for (int s = 0; s < spokes; s++)
            {
                double frac = s / (double)Math.Max(1, spokes - 1);
                angles[s] = startAngle + frac * sweepAngle + (rng.NextDouble() - .5) * .06;
                double len = maxRadius * (.86 + rng.NextDouble() * .24);
                dc.DrawLine(spokePen, anchor, new Point(anchor.X + Math.Cos(angles[s]) * len, anchor.Y + Math.Sin(angles[s]) * len));
            }

            for (int r = 1; r <= rings; r++)
            {
                double rad = maxRadius * Math.Pow(r / (double)rings, 1.12);
                var strand = new StreamGeometry();
                using (var ctx = strand.Open())
                {
                    for (int s = 0; s < spokes - 1; s++)
                    {
                        var p0 = new Point(anchor.X + Math.Cos(angles[s]) * rad, anchor.Y + Math.Sin(angles[s]) * rad);
                        var p1 = new Point(anchor.X + Math.Cos(angles[s + 1]) * rad, anchor.Y + Math.Sin(angles[s + 1]) * rad);
                        double midA = (angles[s] + angles[s + 1]) * .5;
                        double sag = rad * (.84 + rng.NextDouble() * .06);
                        var ctrl = new Point(anchor.X + Math.Cos(midA) * sag, anchor.Y + Math.Sin(midA) * sag);
                        if (s == 0) ctx.BeginFigure(p0, isFilled: false, isClosed: false);
                        ctx.QuadraticBezierTo(ctrl, p1, isStroked: true, isSmoothJoin: true);
                        if (!compact && rng.NextDouble() < .18)
                            dc.DrawEllipse(dewFill, null, ctrl, 1.2, 1.2);
                    }
                }
                strand.Freeze();
                dc.DrawGeometry(null, r % 2 == 0 ? silkPen : spokePen, strand);
            }
        }

        double r1 = Math.Min(width, height) * (compact ? .88 : .44);
        DrawCornerWeb(new Point(0, 0), .02, Math.PI * .48, r1, compact ? 6 : 9, compact ? 4 : 8);
        DrawCornerWeb(new Point(width, height), Math.PI * 1.02, Math.PI * .48, r1 * .82, compact ? 5 : 8, compact ? 3 : 7);
        if (!compact)
            DrawCornerWeb(new Point(width, 0), Math.PI * .52, Math.PI * .46, r1 * .58, 7, 5);
        return group;
    }

    private static DrawingGroup BuildStarburstDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();
        var rng = new Random(5521);

        var starFill = FrozenBrush(WithAlpha(accent, (byte)Math.Round((isLight ? 52 : 56) * intensity)));
        var hiFill = FrozenBrush(WithAlpha(hi, (byte)Math.Round((isLight ? 105 : 64) * intensity)));
        var altFill = FrozenBrush(WithAlpha(ShiftColor(accent), (byte)Math.Round((isLight ? 44 : 48) * intensity)));
        var strokePen = FrozenPen(WithAlpha(accent, (byte)Math.Round((isLight ? 38 : 42) * intensity)), .85);

        var starGeom = Geometry.Parse("M0,-6 Q0,0 6,0 Q0,0 0,6 Q0,0 -6,0 Q0,0 0,-6 Z");
        starGeom.Freeze();
        var diamondGeom = Geometry.Parse("M0,-4.5 L3,0 L0,4.5 L-3,0 Z");
        diamondGeom.Freeze();

        int count = compact ? 16 : Math.Clamp((int)(width * height / 5200), 42, 140);
        for (int i = 0; i < count; i++)
        {
            double x = rng.NextDouble() * width;
            double y = rng.NextDouble() * height;
            double sc = (compact ? .45 : .60) + Math.Pow(rng.NextDouble(), 1.6) * (compact ? 1.1 : 1.9);
            var brush = i % 3 == 0 ? starFill : i % 3 == 1 ? hiFill : altFill;

            if (i % 4 == 3)
            {
                double r = 1.1 + sc * 1.1;
                dc.DrawEllipse(brush, null, new Point(x, y), r, r);
            }
            else
            {
                var m = Matrix.Identity;
                m.Scale(sc, sc);
                m.Rotate(rng.NextDouble() * 45);
                m.Translate(x, y);
                dc.PushTransform(new MatrixTransform(m));
                dc.DrawGeometry(brush, i % 5 == 0 ? strokePen : null, i % 2 == 0 ? starGeom : diamondGeom);
                dc.Pop();
            }
        }
        return group;
    }

    // BuildBotanicalDrawing & scenic styles live in ChromeTexture.Scenes.cs

    private static DrawingGroup BuildCipherDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();
        var rng = new Random(1337);

        double colStep = compact ? 10 : 18;
        int cols = Math.Max(4, (int)(width / colStep));
        var headBrush = FrozenBrush(WithAlpha(hi, (byte)Math.Round((isLight ? 95 : 88) * intensity)));
        var bodyBrush = FrozenBrush(WithAlpha(accent, (byte)Math.Round((isLight ? 44 : 52) * intensity)));
        var tailBrush = FrozenBrush(WithAlpha(accent, (byte)Math.Round((isLight ? 20 : 24) * intensity)));

        for (int c = 0; c < cols; c++)
        {
            if (rng.NextDouble() < .26) continue;
            double x = c * colStep + (rng.NextDouble() - .5) * 3;
            double startY = rng.NextDouble() * height * .8;
            int rows = (compact ? 4 : 6) + rng.Next(compact ? 5 : 16);
            double cellH = compact ? 5.5 : 9.0;
            for (int r = 0; r < rows; r++)
            {
                double y = startY + r * cellH;
                if (y > height) break;
                double frac = r / (double)Math.Max(1, rows - 1);
                var brush = r == rows - 1 ? headBrush : frac > .55 ? bodyBrush : tailBrush;
                double w = (compact ? 2.4 : 4.2) + (rng.Next(3) * (compact ? .8 : 1.4));
                double h = compact ? 2.2 : 3.4;
                dc.DrawRoundedRectangle(brush, null, new Rect(x, y, w, h), 1, 1);
                if (rng.NextDouble() < .35)
                    dc.DrawRectangle(tailBrush, null, new Rect(x + w + 1.5, y + .6, compact ? 1.4 : 2.0, h * .65));
            }
        }
        return group;
    }

    private static DrawingGroup BuildWatercolorDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();
        var rng = new Random(7331);
        Color alt = ShiftColor(accent);

        int washes = compact ? 5 : 11;
        for (int i = 0; i < washes; i++)
        {
            double cx = rng.NextDouble();
            double cy = rng.NextDouble();
            double rx = .18 + rng.NextDouble() * .34;
            double ry = .18 + rng.NextDouble() * .34;
            Color col = i % 3 == 0 ? accent : i % 3 == 1 ? alt : hi;
            byte innerA = (byte)Math.Round((isLight ? 36 : 24) * intensity);
            byte edgeA = (byte)Math.Round((isLight ? 48 : 30) * intensity);

            // Wet-edge watercolor bloom (slightly darker pigment pooling near the outer ring)
            var bloom = new RadialGradientBrush
            {
                Center = new Point(cx, cy),
                GradientOrigin = new Point(cx + (rng.NextDouble() - .5) * .08, cy + (rng.NextDouble() - .5) * .08),
                RadiusX = rx,
                RadiusY = ry
            };
            bloom.GradientStops.Add(new GradientStop(WithAlpha(col, innerA), 0));
            bloom.GradientStops.Add(new GradientStop(WithAlpha(col, (byte)(innerA * .75)), .58));
            bloom.GradientStops.Add(new GradientStop(WithAlpha(col, edgeA), .84));
            bloom.GradientStops.Add(new GradientStop(WithAlpha(col, 0), 1));
            bloom.Freeze();
            dc.DrawRectangle(bloom, null, new Rect(0, 0, width, height));
        }

        // Delicate pigment splatters
        var speck = FrozenBrush(WithAlpha(accent, (byte)Math.Round((isLight ? 42 : 40) * intensity)));
        int dots = compact ? 10 : 38;
        for (int d = 0; d < dots; d++)
        {
            double r = .7 + rng.NextDouble() * (compact ? 1.5 : 2.6);
            dc.DrawEllipse(speck, null, new Point(rng.NextDouble() * width, rng.NextDouble() * height), r, r * (.75 + rng.NextDouble() * .5));
        }
        return group;
    }

    private static DrawingGroup BuildFreedomWingsDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();
        Color beamBlue = Color.FromRgb(0x38, 0xBD, 0xF8);
        Color cobalt = Color.FromRgb(0x1D, 0x4E, 0xD8);
        Color crimson = Color.FromRgb(0xE1, 0x1D, 0x48);
        Color gold = Color.FromRgb(0xEA, 0xB3, 0x08);

        var topGlow = new RadialGradientBrush
        {
            Center = new Point(.86, .14),
            GradientOrigin = new Point(.86, .14),
            RadiusX = .65,
            RadiusY = .58
        };
        topGlow.GradientStops.Add(new GradientStop(WithAlpha(beamBlue, (byte)Math.Round((isLight ? 42 : 48) * intensity)), 0));
        topGlow.GradientStops.Add(new GradientStop(WithAlpha(cobalt, (byte)Math.Round((isLight ? 16 : 22) * intensity)), .55));
        topGlow.GradientStops.Add(new GradientStop(WithAlpha(cobalt, 0), 1));
        topGlow.Freeze();
        dc.DrawRectangle(topGlow, null, new Rect(0, 0, width, height));

        // Swept HiMAT wing blades fanning from top-right
        int wingCount = compact ? 4 : 7;
        double pivotX = width * 1.04;
        double pivotY = height * -.06;
        double span = Math.Max(width, height) * .95;
        var edgePen = FrozenPen(WithAlpha(cobalt, (byte)Math.Round((isLight ? 48 : 74) * intensity)), compact ? 1.0 : 1.35);
        var beamPen = FrozenPen(WithAlpha(beamBlue, (byte)Math.Round((isLight ? 52 : 76) * intensity)), compact ? .85 : 1.15);
        var trimPen = FrozenPen(WithAlpha(crimson, (byte)Math.Round((isLight ? 36 : 56) * intensity)), compact ? .75 : 1.0);
        for (int i = 0; i < wingCount; i++)
        {
            double a0 = 1.92 + i * .16;
            double a1 = a0 + .09;
            double len = span * (.94 - i * .07);
            var p0 = new Point(pivotX, pivotY);
            var p1 = new Point(pivotX + Math.Cos(a0) * len, pivotY + Math.Sin(a0) * len);
            var tip = new Point(pivotX + Math.Cos((a0 + a1) * .5) * (len * 1.08), pivotY + Math.Sin((a0 + a1) * .5) * (len * 1.08));
            var p2 = new Point(pivotX + Math.Cos(a1) * (len * .76), pivotY + Math.Sin(a1) * (len * .76));
            var blade = new StreamGeometry();
            using (var ctx = blade.Open())
            {
                ctx.BeginFigure(p0, isFilled: true, isClosed: true);
                ctx.LineTo(p1, isStroked: true, isSmoothJoin: false);
                ctx.LineTo(tip, isStroked: true, isSmoothJoin: false);
                ctx.LineTo(p2, isStroked: true, isSmoothJoin: false);
            }
            blade.Freeze();
            var fill = FrozenBrush(WithAlpha(i % 2 == 0 ? cobalt : beamBlue, (byte)Math.Round((isLight ? 16 : 24) * intensity)));
            dc.DrawGeometry(fill, i % 3 == 0 ? trimPen : i % 2 == 0 ? edgePen : beamPen, blade);
        }

        // Mirrored lower-left thruster vanes + subtle V-fin accent
        var panelPen = FrozenPen(WithAlpha(hi, (byte)Math.Round((isLight ? 90 : 34) * intensity)), .9);
        int lowerCount = compact ? 2 : 4;
        for (int i = 0; i < lowerCount; i++)
        {
            double yBase = height * (.66 + i * .085);
            dc.DrawLine(i == 0 ? trimPen : edgePen, new Point(0, yBase), new Point(width * (.36 - i * .06), yBase + height * .16));
            dc.DrawLine(panelPen, new Point(width * .04, yBase - 3), new Point(width * (.30 - i * .05), yBase + height * .12));
        }

        var vPen = FrozenPen(WithAlpha(gold, (byte)Math.Round((isLight ? 52 : 68) * intensity)), compact ? 1.1 : 1.5);
        double vx = width * (compact ? .24 : .16);
        double vy = height * (compact ? .28 : .20);
        double vs = compact ? 16 : 28;
        var vfin = new StreamGeometry();
        using (var ctx = vfin.Open())
        {
            ctx.BeginFigure(new Point(vx - vs * .95, vy - vs * .42), isFilled: false, isClosed: false);
            ctx.LineTo(new Point(vx, vy + vs * .22), isStroked: true, isSmoothJoin: false);
            ctx.LineTo(new Point(vx + vs * .95, vy - vs * .42), isStroked: true, isSmoothJoin: false);
        }
        vfin.Freeze();
        dc.DrawGeometry(null, vPen, vfin);
        dc.DrawEllipse(FrozenBrush(WithAlpha(crimson, (byte)Math.Round(65 * intensity))), null, new Point(vx, vy + vs * .22), compact ? 2 : 3, compact ? 2 : 3);
        return group;
    }

    private static DrawingGroup BuildArcReactorDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();
        Color arcCyan = Color.FromRgb(0x38, 0xBD, 0xF8);
        Color arcGold = Color.FromRgb(0xF5, 0xB7, 0x00);
        Color armorRed = Color.FromRgb(0xDC, 0x26, 0x26);

        var center = new Point(width * (compact ? .64 : .72), height * (compact ? .52 : .36));
        double minDim = Math.Min(width, height);
        double coreRadius = Math.Clamp(minDim * (compact ? .36 : .28), 22, 140);

        var glow = new RadialGradientBrush
        {
            Center = new Point(center.X / Math.Max(1, width), center.Y / Math.Max(1, height)),
            GradientOrigin = new Point(center.X / Math.Max(1, width), center.Y / Math.Max(1, height)),
            RadiusX = compact ? .55 : .42,
            RadiusY = compact ? .55 : .42
        };
        glow.GradientStops.Add(new GradientStop(WithAlpha(arcCyan, (byte)Math.Round((isLight ? 44 : 68) * intensity)), 0));
        glow.GradientStops.Add(new GradientStop(WithAlpha(arcGold, (byte)Math.Round((isLight ? 18 : 26) * intensity)), .52));
        glow.GradientStops.Add(new GradientStop(WithAlpha(armorRed, 0), 1));
        glow.Freeze();
        dc.DrawRectangle(glow, null, new Rect(0, 0, width, height));

        var cyanPen = FrozenPen(WithAlpha(arcCyan, (byte)Math.Round((isLight ? 54 : 88) * intensity)), compact ? 1.1 : 1.5);
        var goldPen = FrozenPen(WithAlpha(arcGold, (byte)Math.Round((isLight ? 42 : 68) * intensity)), compact ? 1.0 : 1.35);
        var redPen = FrozenPen(WithAlpha(armorRed, (byte)Math.Round((isLight ? 34 : 58) * intensity)), .95);

        dc.DrawEllipse(null, cyanPen, center, coreRadius, coreRadius);
        dc.DrawEllipse(null, goldPen, center, coreRadius * .72, coreRadius * .72);
        dc.DrawEllipse(null, cyanPen, center, coreRadius * .42, coreRadius * .42);

        // Solenoid coils around the reactor ring
        int coils = compact ? 8 : 10;
        for (int i = 0; i < coils; i++)
        {
            double a = i * Math.Tau / coils - Math.PI * .5;
            var p0 = new Point(center.X + Math.Cos(a) * coreRadius * .45, center.Y + Math.Sin(a) * coreRadius * .45);
            var p1 = new Point(center.X + Math.Cos(a) * coreRadius * .96, center.Y + Math.Sin(a) * coreRadius * .96);
            dc.DrawLine(i % 2 == 0 ? goldPen : cyanPen, p0, p1);
        }

        // Triangular / beveled armor seam lines across the panel
        if (!compact)
        {
            dc.DrawLine(redPen, new Point(0, height * .18), new Point(width * .28, height * .18));
            dc.DrawLine(goldPen, new Point(width * .28, height * .18), new Point(width * .38, height * .30));
            dc.DrawLine(redPen, new Point(0, height * .82), new Point(width * .34, height * .82));
            dc.DrawLine(goldPen, new Point(width * .34, height * .82), new Point(width * .44, height * .68));
            dc.DrawLine(redPen, new Point(center.X - coreRadius * 1.3, center.Y), new Point(0, center.Y));
        }

        // Subtle hexagonal armor mesh cell
        double hexR = compact ? 16 : 26;
        var hex = new StreamGeometry();
        using (var ctx = hex.Open())
        {
            for (int i = 0; i < 6; i++)
            {
                double a = i * Math.Tau / 6;
                var pt = new Point(center.X + Math.Cos(a) * hexR * .65, center.Y + Math.Sin(a) * hexR * .65);
                if (i == 0) ctx.BeginFigure(pt, isFilled: false, isClosed: true);
                else ctx.LineTo(pt, isStroked: true, isSmoothJoin: false);
            }
        }
        hex.Freeze();
        dc.DrawGeometry(null, cyanPen, hex);
        return group;
    }

    private static DrawingGroup BuildMarbleDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();

        for (int c = 0; c < 4; c++)
        {
            var cloud = new RadialGradientBrush
            {
                Center = new Point(.18 + c * .22, .22 + (c % 2) * .54),
                GradientOrigin = new Point(.18 + c * .22, .22 + (c % 2) * .54),
                RadiusX = .55,
                RadiusY = .45
            };
            Color col = c % 2 == 0 ? hi : accent;
            cloud.GradientStops.Add(new GradientStop(WithAlpha(col, (byte)Math.Round((isLight ? 40 : 22) * intensity)), 0));
            cloud.GradientStops.Add(new GradientStop(WithAlpha(col, 0), 1));
            cloud.Freeze();
            dc.DrawRectangle(cloud, null, new Rect(0, 0, width, height));
        }

        var mainVein = FrozenPen(WithAlpha(accent, (byte)Math.Round((isLight ? 44 : 52) * intensity)), 1.45);
        var shadowVein = FrozenPen(WithAlpha(lo, (byte)Math.Round((isLight ? 24 : 42) * intensity)), 2.2);
        var fineVein = FrozenPen(WithAlpha(hi, (byte)Math.Round((isLight ? 86 : 42) * intensity)), .85);

        int veins = height < 90 ? 4 : 7;
        for (int i = 0; i < veins; i++)
        {
            double startY = height * ((i + .3) / veins) - height * .18;
            double slope = .32 + (i % 3) * .12;
            var path = new StreamGeometry();
            using (var ctx = path.Open())
            {
                const int segs = 42;
                for (int s = 0; s <= segs; s++)
                {
                    double t = s / (double)segs;
                    double x = t * width;
                    double warp = Math.Sin(t * Math.Tau * 1.7 + i * 1.4) * height * .09
                        + Math.Cos(t * Math.Tau * 3.9 - i * .9) * height * .035
                        + Math.Sin(t * Math.Tau * 7.3 + i * 2.1) * height * .014;
                    var p = new Point(x, startY + t * height * slope + warp);
                    if (s == 0) ctx.BeginFigure(p, isFilled: false, isClosed: false);
                    else ctx.LineTo(p, isStroked: true, isSmoothJoin: true);
                }
            }
            path.Freeze();
            dc.DrawGeometry(null, shadowVein, path);
            dc.DrawGeometry(null, i % 2 == 0 ? mainVein : fineVein, path);
        }
        return group;
    }

    private static DrawingGroup BuildTopoDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity)
    {
        var group = new DrawingGroup();
        var majorPen = FrozenPen(WithAlpha(accent, (byte)Math.Round((isLight ? 44 : 52) * intensity)), 1.25);
        var minorPen = FrozenPen(WithAlpha(isLight ? lo : hi, (byte)Math.Round((isLight ? 22 : 24) * intensity)), .85);
        var ridgeHighlight = FrozenPen(WithAlpha(hi, (byte)Math.Round((isLight ? 68 : 30) * intensity)), .85);

        using var dc = group.Open();
        double scale = Math.Max(width, height);
        (double cx, double cy, double baseR, double phase, int rings, double warp)[] peaks =
        [
            (width * .20, height * .16, scale * .055, .4, 10, .18),
            (width * .66, height * .08, scale * .050, 1.9, 9, .16),
            (width * .11, height * .80, scale * .045, 3.1, 8, .15)
        ];

        foreach (var peak in peaks)
        {
            for (int ring = 1; ring <= peak.rings; ring++)
            {
                double radius = peak.baseR * Math.Pow(ring, 1.18);
                var ringGeom = CreateContourRing(peak.cx, peak.cy, radius, peak.phase + ring * .22, peak.warp);
                bool major = ring % 3 == 0;
                dc.DrawGeometry(null, major ? majorPen : minorPen, ringGeom);
                if (major)
                {
                    var highlightGeom = CreateContourRing(peak.cx - .8, peak.cy - .8, radius, peak.phase + ring * .22, peak.warp);
                    dc.DrawGeometry(null, ridgeHighlight, highlightGeom);
                }
            }
        }

        int waves = height < 90 ? 4 : 6;
        for (int i = 0; i < waves; i++)
        {
            double frac = (i + .6) / (waves + .4);
            var lineGeom = new StreamGeometry();
            using (var ctx = lineGeom.Open())
            {
                const int segs = 36;
                for (int s = 0; s <= segs; s++)
                {
                    double u = s / (double)segs;
                    double x = u * width;
                    double y = height * frac
                        + Math.Sin(u * Math.Tau * 1.4 + i * .85) * height * .065
                        + Math.Cos(u * Math.Tau * 2.7 - i * .55) * height * .028;
                    var p = new Point(x, y);
                    if (s == 0) ctx.BeginFigure(p, isFilled: false, isClosed: false);
                    else ctx.LineTo(p, isStroked: true, isSmoothJoin: true);
                }
            }
            lineGeom.Freeze();
            dc.DrawGeometry(null, i % 2 == 0 ? minorPen : majorPen, lineGeom);
        }

        return group;
    }

    private static DrawingGroup BuildSilkDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();

        (double yStart, double yEnd, double amp1, double amp2, double thickness, double phase, bool warm)[] ribbons =
        [
            (.06, .34, .09, .05, .26, 0.2, false),
            (.14, .48, -.08, .06, .22, 1.3, true),
            (.30, .62, .10, -.04, .28, 2.4, false),
            (.46, .78, -.09, .05, .24, 3.6, true),
            (.66, .94, .08, -.05, .26, 4.7, false)
        ];

        byte hiRim = (byte)Math.Round((isLight ? 92 : 44) * intensity);
        var rimPen = FrozenPen(WithAlpha(hi, hiRim), 1.05);
        var accentRimPen = FrozenPen(WithAlpha(accent, (byte)Math.Round((isLight ? 40 : 48) * intensity)), .9);

        foreach (var r in ribbons)
        {
            var fill = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(.25, 1) };
            Color foldColor = r.warm ? accent : hi;
            fill.GradientStops.Add(new GradientStop(WithAlpha(foldColor, (byte)Math.Round((isLight ? 66 : 30) * intensity)), 0));
            fill.GradientStops.Add(new GradientStop(WithAlpha(accent, (byte)Math.Round((isLight ? 24 : 22) * intensity)), .48));
            fill.GradientStops.Add(new GradientStop(WithAlpha(lo, (byte)Math.Round((isLight ? 16 : 36) * intensity)), 1));
            fill.Freeze();

            var crest = new StreamGeometry();
            var band = new StreamGeometry();
            const int steps = 40;
            var topPts = new Point[steps + 1];
            var botPts = new Point[steps + 1];

            for (int s = 0; s <= steps; s++)
            {
                double t = s / (double)steps;
                double x = t * width;
                double cy = (r.yStart + (r.yEnd - r.yStart) * t) * height
                    + Math.Sin(t * Math.Tau * 1.2 + r.phase) * height * r.amp1
                    + Math.Cos(t * Math.Tau * 2.1 - r.phase * .7) * height * r.amp2;
                double half = height * r.thickness * (.65 + .35 * Math.Sin(t * Math.PI + .4)) * .5;
                topPts[s] = new Point(x, cy - half);
                botPts[s] = new Point(x, cy + half);
            }

            using (var ctx = band.Open())
            {
                ctx.BeginFigure(topPts[0], isFilled: true, isClosed: true);
                for (int s = 1; s <= steps; s++) ctx.LineTo(topPts[s], isStroked: false, isSmoothJoin: true);
                for (int s = steps; s >= 0; s--) ctx.LineTo(botPts[s], isStroked: false, isSmoothJoin: true);
            }
            band.Freeze();
            dc.DrawGeometry(fill, null, band);

            using (var ctx = crest.Open())
            {
                ctx.BeginFigure(topPts[0], isFilled: false, isClosed: false);
                for (int s = 1; s <= steps; s++) ctx.LineTo(topPts[s], isStroked: true, isSmoothJoin: true);
            }
            crest.Freeze();
            dc.DrawGeometry(null, r.warm ? accentRimPen : rimPen, crest);
        }

        return group;
    }

    // ---------------- Intentionally Geometric Repeating Textures ----------------

    private static Brush BuildWovenBrush(Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        double cell = compact ? 10 : 12;
        double half = cell * .5;
        double thread = compact ? 1.5 : 1.9;

        var group = new DrawingGroup();
        var warpPen = FrozenPen(WithAlpha(hi, (byte)Math.Round((isLight ? 58 : 24) * intensity)), thread);
        var weftPen = FrozenPen(WithAlpha(accent, (byte)Math.Round((isLight ? 24 : 22) * intensity)), thread * .85);
        var shadowPen = FrozenPen(WithAlpha(lo, (byte)Math.Round((isLight ? 16 : 30) * intensity)), thread * .8);
        var groovePen = FrozenPen(WithAlpha(lo, (byte)Math.Round((isLight ? 11 : 20) * intensity)), .65);

        using (var dc = group.Open())
        {
            dc.DrawLine(shadowPen, new Point(0, half * .5), new Point(half, half * .5));
            dc.DrawLine(warpPen, new Point(half, half * .5), new Point(cell, half * .5));
            dc.DrawLine(warpPen, new Point(0, half * 1.5), new Point(half, half * 1.5));
            dc.DrawLine(weftPen, new Point(half, half * 1.5), new Point(cell, half * 1.5));

            dc.DrawLine(warpPen, new Point(half * .5, 0), new Point(half * .5, half));
            dc.DrawLine(weftPen, new Point(half * 1.5, half), new Point(half * 1.5, cell));
            dc.DrawLine(shadowPen, new Point(half * 1.5, 0), new Point(half * 1.5, half));
            dc.DrawLine(shadowPen, new Point(half * .5, half), new Point(half * .5, cell));

            dc.DrawLine(groovePen, new Point(0, half), new Point(cell, half));
            dc.DrawLine(groovePen, new Point(half, 0), new Point(half, cell));
        }

        return TiledDrawingBrush(group, cell, cell);
    }

    private static Brush BuildGridBrush(Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        double sub = compact ? 14 : 20;
        double major = sub * 4;

        var group = new DrawingGroup();
        var subPen = FrozenPen(WithAlpha(isLight ? lo : hi, (byte)Math.Round((isLight ? 18 : 16) * intensity)), .65);
        var majorPen = FrozenPen(WithAlpha(accent, (byte)Math.Round((isLight ? 36 : 38) * intensity)), .95);
        var crossPen = FrozenPen(WithAlpha(hi, (byte)Math.Round((isLight ? 84 : 58) * intensity)), 1.2);
        var dotBrush = FrozenBrush(WithAlpha(accent, (byte)Math.Round((isLight ? 54 : 52) * intensity)));

        using (var dc = group.Open())
        {
            for (int i = 1; i < 4; i++)
            {
                double p = sub * i + .5;
                dc.DrawLine(subPen, new Point(p, 0), new Point(p, major));
                dc.DrawLine(subPen, new Point(0, p), new Point(major, p));
            }
            dc.DrawLine(majorPen, new Point(.5, 0), new Point(.5, major));
            dc.DrawLine(majorPen, new Point(0, .5), new Point(major, .5));

            double arm = compact ? 3.5 : 5;
            dc.DrawLine(crossPen, new Point(.5, 0), new Point(.5, arm));
            dc.DrawLine(crossPen, new Point(.5, major - arm), new Point(.5, major));
            dc.DrawLine(crossPen, new Point(0, .5), new Point(arm, .5));
            dc.DrawLine(crossPen, new Point(major - arm, .5), new Point(major, .5));
            dc.DrawEllipse(dotBrush, null, new Point(major * .5 + .5, major * .5 + .5), 1.0, 1.0);
        }

        return TiledDrawingBrush(group, major, major);
    }

    private static Brush BuildHexBrush(Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        double r = compact ? 10 : 15;
        double w = Math.Sqrt(3) * r;
        double stepY = 3 * r;

        var group = new DrawingGroup();
        var bevelLight = FrozenPen(WithAlpha(hi, (byte)Math.Round((isLight ? 78 : 34) * intensity)), 1.05);
        var bevelShadow = FrozenPen(WithAlpha(lo, (byte)Math.Round((isLight ? 28 : 54) * intensity)), 1.15);
        var ridge = FrozenPen(WithAlpha(accent, (byte)Math.Round((isLight ? 30 : 32) * intensity)), .85);
        var cellFill = FrozenBrush(WithAlpha(hi, (byte)Math.Round((isLight ? 20 : 12) * intensity)));
        var altFill = FrozenBrush(WithAlpha(lo, (byte)Math.Round((isLight ? 10 : 18) * intensity)));

        using (var dc = group.Open())
        {
            Point[] centers =
            [
                new(0, 0), new(w, 0),
                new(w * .5, 1.5 * r), new(-w * .5, 1.5 * r), new(w * 1.5, 1.5 * r),
                new(0, 3 * r), new(w, 3 * r)
            ];
            for (int cIdx = 0; cIdx < centers.Length; cIdx++)
            {
                var c = centers[cIdx];
                Point[] pts = HexPoints(c.X, c.Y, r - .7);
                var poly = BuildHexPolygon(pts);
                dc.DrawGeometry(cIdx % 2 == 0 ? cellFill : altFill, ridge, poly);
                dc.DrawLine(bevelLight, pts[4], pts[5]);
                dc.DrawLine(bevelLight, pts[5], pts[0]);
                dc.DrawLine(bevelShadow, pts[1], pts[2]);
                dc.DrawLine(bevelShadow, pts[2], pts[3]);
            }
        }

        return TiledDrawingBrush(group, w, stepY);
    }

    private static Brush BuildCarbonBrush(Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        double cell = compact ? 10 : 14;
        double half = cell * .5;
        var group = new DrawingGroup();

        var darkPatch = new LinearGradientBrush(
            WithAlpha(lo, (byte)Math.Round((isLight ? 36 : 74) * intensity)),
            WithAlpha(hi, (byte)Math.Round((isLight ? 22 : 16) * intensity)), 90);
        darkPatch.Freeze();
        var lightPatch = new LinearGradientBrush(
            WithAlpha(hi, (byte)Math.Round((isLight ? 72 : 38) * intensity)),
            WithAlpha(accent, (byte)Math.Round((isLight ? 26 : 28) * intensity)), 0);
        lightPatch.Freeze();
        var seamPen = FrozenPen(WithAlpha(lo, (byte)Math.Round((isLight ? 24 : 58) * intensity)), .75);

        using (var dc = group.Open())
        {
            dc.DrawRectangle(darkPatch, seamPen, new Rect(0, 0, half, half));
            dc.DrawRectangle(lightPatch, seamPen, new Rect(half, 0, half, half));
            dc.DrawRectangle(lightPatch, seamPen, new Rect(0, half, half, half));
            dc.DrawRectangle(darkPatch, seamPen, new Rect(half, half, half, half));
        }
        return TiledDrawingBrush(group, cell, cell);
    }

    private static Brush BuildWavesBrush(Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        double w = compact ? 26 : 38;
        double h = w * .5;
        var group = new DrawingGroup();
        var outerPen = FrozenPen(WithAlpha(accent, (byte)Math.Round((isLight ? 44 : 48) * intensity)), 1.05);
        var midPen = FrozenPen(WithAlpha(isLight ? lo : hi, (byte)Math.Round((isLight ? 24 : 26) * intensity)), .85);
        var hiPen = FrozenPen(WithAlpha(hi, (byte)Math.Round((isLight ? 72 : 34) * intensity)), .8);

        using (var dc = group.Open())
        {
            Point[] centers =
            [
                new(0, h), new(w, h),
                new(w * .5, h * 2),
                new(0, 0), new(w, 0),
                new(w * .5, h)
            ];
            foreach (var c in centers)
            {
                dc.DrawEllipse(null, outerPen, c, w * .5, w * .5);
                dc.DrawEllipse(null, hiPen, c, w * .36, w * .36);
                dc.DrawEllipse(null, midPen, c, w * .22, w * .22);
            }
        }
        return TiledDrawingBrush(group, w, h);
    }

    private static Brush BuildArtDecoBrush(Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        double w = compact ? 28 : 42;
        double h = w * .72;
        var group = new DrawingGroup();
        var archPen = FrozenPen(WithAlpha(accent, (byte)Math.Round((isLight ? 48 : 54) * intensity)), 1.05);
        var ribPen = FrozenPen(WithAlpha(hi, (byte)Math.Round((isLight ? 68 : 34) * intensity)), .8);
        var shadePen = FrozenPen(WithAlpha(lo, (byte)Math.Round((isLight ? 18 : 32) * intensity)), .85);

        using (var dc = group.Open())
        {
            void DrawFan(double cx, double cy)
            {
                for (int r = 1; r <= 3; r++)
                {
                    double rad = w * .17 * r;
                    dc.DrawEllipse(null, r == 3 ? archPen : ribPen, new Point(cx, cy), rad, rad);
                }
                for (int k = -2; k <= 2; k++)
                {
                    double a = -Math.PI * .5 + k * .36;
                    dc.DrawLine(shadePen, new Point(cx, cy), new Point(cx + Math.Cos(a) * w * .5, cy + Math.Sin(a) * w * .5));
                }
            }
            DrawFan(w * .5, h);
            DrawFan(0, h * .5);
            DrawFan(w, h * .5);
            DrawFan(w * .5, 0);
        }
        return TiledDrawingBrush(group, w, h);
    }

    private static Brush BuildMeshBrush(Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        double size = compact ? 22 : 32;
        double half = size * .5;
        var group = new DrawingGroup();
        var linePen = FrozenPen(WithAlpha(isLight ? lo : hi, (byte)Math.Round((isLight ? 20 : 22) * intensity)), .8);
        var accentPen = FrozenPen(WithAlpha(accent, (byte)Math.Round((isLight ? 38 : 42) * intensity)), .95);
        var jewel = FrozenBrush(WithAlpha(hi, (byte)Math.Round((isLight ? 92 : 64) * intensity)));
        var facet = FrozenBrush(WithAlpha(accent, (byte)Math.Round((isLight ? 16 : 18) * intensity)));

        using (var dc = group.Open())
        {
            var diamond = new StreamGeometry();
            using (var ctx = diamond.Open())
            {
                ctx.BeginFigure(new Point(half, 0), isFilled: true, isClosed: true);
                ctx.LineTo(new Point(size, half), isStroked: true, isSmoothJoin: false);
                ctx.LineTo(new Point(half, size), isStroked: true, isSmoothJoin: false);
                ctx.LineTo(new Point(0, half), isStroked: true, isSmoothJoin: false);
            }
            diamond.Freeze();
            dc.DrawGeometry(facet, accentPen, diamond);
            dc.DrawLine(linePen, new Point(0, 0), new Point(size, size));
            dc.DrawLine(linePen, new Point(size, 0), new Point(0, size));
            dc.DrawEllipse(jewel, null, new Point(half, half), 1.4, 1.4);
            dc.DrawEllipse(jewel, null, new Point(half, 0), 1.2, 1.2);
            dc.DrawEllipse(jewel, null, new Point(0, half), 1.2, 1.2);
        }
        return TiledDrawingBrush(group, size, size);
    }

    // ---------------- Helpers ----------------

    private static Color ShiftColor(Color c) => Color.FromRgb(
        (byte)((c.G * .55 + 130) % 255),
        (byte)((c.B * .55 + 115) % 255),
        (byte)Math.Min(255, c.R * .45 + 190));

    private static StreamGeometry CreateContourRing(double cx, double cy, double radius, double phase, double warp)
    {
        const int steps = 48;
        var geom = new StreamGeometry();
        using var ctx = geom.Open();
        for (int s = 0; s <= steps; s++)
        {
            double angle = s / (double)steps * Math.Tau;
            double mod = 1.0
                + warp * Math.Sin(angle * 3 + phase)
                + warp * .55 * Math.Cos(angle * 5 - phase * 1.4)
                + warp * .25 * Math.Sin(angle * 7 + phase * .7);
            double r = radius * mod;
            var p = new Point(cx + Math.Cos(angle) * r, cy + Math.Sin(angle) * r * .86);
            if (s == 0) ctx.BeginFigure(p, isFilled: false, isClosed: true);
            else ctx.LineTo(p, isStroked: true, isSmoothJoin: true);
        }
        geom.Freeze();
        return geom;
    }

    private static Point[] HexPoints(double cx, double cy, double r)
    {
        var pts = new Point[6];
        for (int i = 0; i < 6; i++)
        {
            double a = Math.PI / 6.0 + i * Math.PI / 3.0;
            pts[i] = new Point(cx + r * Math.Cos(a), cy + r * Math.Sin(a));
        }
        return pts;
    }

    private static StreamGeometry BuildHexPolygon(Point[] pts)
    {
        var geom = new StreamGeometry();
        using var ctx = geom.Open();
        ctx.BeginFigure(pts[0], isFilled: true, isClosed: true);
        for (int i = 1; i < pts.Length; i++) ctx.LineTo(pts[i], isStroked: true, isSmoothJoin: false);
        geom.Freeze();
        return geom;
    }

    private static Brush TiledDrawingBrush(DrawingGroup drawing, double width, double height)
    {
        drawing.Freeze();
        var brush = new DrawingBrush(drawing)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, width, height),
            ViewportUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(0, 0, width, height),
            ViewboxUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None
        };
        brush.Freeze();
        return brush;
    }

    private static Pen FrozenPen(Color color, double thickness)
    {
        var brush = FrozenBrush(color);
        var pen = new Pen(brush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen.Freeze();
        return pen;
    }

    private static Brush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Color WithAlpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

    private static double Hash2D(int x, int y)
    {
        uint h = (uint)(x * 374761393 + y * 668265263);
        h = (h ^ (h >> 13)) * 1274126177u;
        return ((h ^ (h >> 16)) & 0xFFFF) / 65535.0;
    }

    private static double SmoothTileNoise(double x, double y, int period)
    {
        int ix = (int)Math.Floor(x);
        int iy = (int)Math.Floor(y);
        double fx = x - ix;
        double fy = y - iy;
        fx = fx * fx * (3 - 2 * fx);
        fy = fy * fy * (3 - 2 * fy);
        int x0 = ((ix % period) + period) % period;
        int x1 = (x0 + 1) % period;
        int y0 = ((iy % period) + period) % period;
        int y1 = (y0 + 1) % period;
        double v00 = Hash2D(x0, y0);
        double v10 = Hash2D(x1, y0);
        double v01 = Hash2D(x0, y1);
        double v11 = Hash2D(x1, y1);
        return (v00 * (1 - fx) + v10 * fx) * (1 - fy) + (v01 * (1 - fx) + v11 * fx) * fy;
    }

    private static void WritePremul(byte[] buffer, int idx, Color c, byte a)
    {
        buffer[idx] = (byte)(c.B * a / 255);
        buffer[idx + 1] = (byte)(c.G * a / 255);
        buffer[idx + 2] = (byte)(c.R * a / 255);
        buffer[idx + 3] = a;
    }
}

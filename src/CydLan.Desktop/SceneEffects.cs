using System.Windows;
using System.Windows.Media;
using CydLan.Core;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

public sealed record SceneEffectDescriptor(string Kind, string Name, double DefaultX, double DefaultY);
public sealed record SceneZoneDescriptor(string Zone, string Name);

/// <summary>Catalog, normalization, and procedural animated rendering for drag-and-drop scene effects.</summary>
public static class SceneEffectCatalog
{
    public const int MaxItems = 120;

    public const string Grass = "grass";
    public const string Vine = "vine";
    public const string Sakura = "sakura";
    public const string Lantern = "lantern";
    public const string Fireflies = "fireflies";
    public const string Sparkle = "sparkle";
    public const string Butterfly = "butterfly";
    public const string Gear = "gear";
    public const string Ripple = "ripple";

    public const string ZoneContent = "content";
    public const string ZoneSidebar = "sidebar";
    public const string ZoneHeader = "header";
    public const string ZoneRail = "rail";
    public const string ZoneTitle = "title";

    public static IReadOnlyList<SceneEffectDescriptor> Effects { get; } =
    [
        new(Grass, "Swaying grass", .50, .99),
        new(Vine, "Hanging vine", .50, .03),
        new(Sakura, "Sakura", .86, .18),
        new(Lantern, "Lantern", .16, .16),
        new(Fireflies, "Fireflies", .50, .55),
        new(Sparkle, "Sparkles", .50, .45),
        new(Butterfly, "Butterfly", .45, .40),
        new(Gear, "Gear", .50, .50),
        new(Ripple, "Ripple", .50, .50)
    ];

    public static IReadOnlyList<SceneZoneDescriptor> Zones { get; } =
    [
        new(ZoneContent, "Content"),
        new(ZoneSidebar, "Side content"),
        new(ZoneHeader, "Header"),
        new(ZoneRail, "Side nav"),
        new(ZoneTitle, "Title bar")
    ];

    public static string NormalizeKind(string? value) => (value ?? "").Trim().ToLowerInvariant() switch
    {
        Vine => Vine,
        Sakura => Sakura,
        Lantern => Lantern,
        Fireflies => Fireflies,
        Sparkle => Sparkle,
        Butterfly => Butterfly,
        Gear => Gear,
        Ripple => Ripple,
        _ => Grass
    };

    public static string NormalizeZone(string? value) => (value ?? "").Trim().ToLowerInvariant() switch
    {
        ZoneSidebar => ZoneSidebar,
        ZoneHeader => ZoneHeader,
        ZoneRail => ZoneRail,
        ZoneTitle => ZoneTitle,
        _ => ZoneContent
    };

    public static SceneEffectDescriptor DescriptorFor(string? kind)
    {
        string normalized = NormalizeKind(kind);
        return Effects.FirstOrDefault(e => e.Kind == normalized) ?? Effects[0];
    }

    public static SceneEffectItem NormalizeItem(SceneEffectItem item) => new()
    {
        Id = string.IsNullOrWhiteSpace(item.Id) ? Guid.NewGuid().ToString("N")[..8] : item.Id.Trim(),
        Kind = NormalizeKind(item.Kind),
        Zone = NormalizeZone(item.Zone),
        X = ClampFinite(item.X, .5, 0, 1),
        Y = ClampFinite(item.Y, .5, 0, 1),
        Scale = ClampFinite(item.Scale, 1.0, .4, 2.4),
        Opacity = ClampFinite(item.Opacity, .90, .15, 1.0),
        Speed = ClampFinite(item.Speed, 1.0, .2, 2.5),
        Rotation = ClampFinite(item.Rotation, 0, -180, 180),
        FlipX = item.FlipX,
        ColorHex = AppearanceOptions.NormalizeHex(item.ColorHex),
        Seed = ClampFinite(item.Seed, 0, 0, 1000)
    };

    public static List<SceneEffectItem> NormalizeList(IEnumerable<SceneEffectItem>? items)
    {
        if (items is null) return [];
        var list = new List<SceneEffectItem>();
        foreach (var item in items)
        {
            if (item is null) continue;
            list.Add(NormalizeItem(item));
            if (list.Count >= MaxItems) break;
        }
        return list;
    }

    private static double ClampFinite(double v, double fallback, double min, double max) =>
        double.IsFinite(v) ? Math.Clamp(v, min, max) : fallback;
}

internal static class SceneEffectRenderer
{
    private static readonly Geometry StarGlyph = Geometry.Parse("M0,-7 Q0,0 7,0 Q0,0 0,7 Q0,0 -7,0 Q0,0 0,-7 Z");
    private static readonly Geometry BlossomPetal = Geometry.Parse("M0,0 C-3.2,-4.8 -3.0,-9.5 0,-9.2 L1.0,-7.8 L2.0,-9.2 C5.0,-8.8 3.4,-3.4 0,0 Z");

    private readonly record struct ScenePalette(
        Color Surface,
        bool IsLight,
        Color Hi,
        Color Lo,
        Color Accent);

    public static Color ResolveZoneSurfaceColor(FrameworkElement element, string? zone)
    {
        string normalized = SceneEffectCatalog.NormalizeZone(zone);
        string[] keys = normalized switch
        {
            SceneEffectCatalog.ZoneSidebar => ["SidebarPreviewBackground", "Sidebar", "ChromeSurfaceColor"],
            SceneEffectCatalog.ZoneHeader => ["HeaderPreviewBackground", "HeaderBackground", "Sidebar", "ChromeSurfaceColor"],
            SceneEffectCatalog.ZoneRail => ["RailPreviewBackground", "Rail", "Sidebar", "ChromeSurfaceColor"],
            SceneEffectCatalog.ZoneTitle => ["TitleBarPreviewBackground", "TitleBarBackground", "Sidebar", "ChromeSurfaceColor"],
            _ => ["ChatPreviewBackground", "ChatSurfaceBackground", "Canvas", "ChromeSurfaceColor"]
        };
        return ResolveFirstBrushColor(element, keys, Color.FromRgb(0x14, 0x18, 0x24));
    }

    public static Color ResolveFirstBrushColor(FrameworkElement element, IReadOnlyList<string> keys, Color fallback)
    {
        foreach (string key in keys)
        {
            object? res = element.TryFindResource(key) ?? System.Windows.Application.Current?.TryFindResource(key);
            if (TryExtractColor(res, out Color c) && c.A > 12) return c;
        }
        return fallback;
    }

    private static bool TryExtractColor(object? res, out Color color)
    {
        switch (res)
        {
            case SolidColorBrush solid:
                color = solid.Color;
                return true;
            case GradientBrush grad when grad.GradientStops.Count > 0:
                color = grad.GradientStops[grad.GradientStops.Count / 2].Color;
                return true;
            case Color c:
                color = c;
                return true;
            default:
                color = default;
                return false;
        }
    }

    private static ScenePalette BuildPalette(Color surface, Color themeTint)
    {
        double luma = (0.2126 * surface.R + 0.7152 * surface.G + 0.0722 * surface.B) / 255.0;
        bool isLight = luma >= 0.50;
        Color hi = isLight ? Colors.White : EffectPalette.Blend(themeTint, Colors.White, 0.72);
        Color lo = isLight ? EffectPalette.Blend(surface, Colors.Black, 0.58) : Colors.Black;
        Color accent = EffectPalette.Blend(themeTint, isLight ? lo : Colors.White, isLight ? 0.18 : 0.22);
        return new(surface, isLight, hi, lo, accent);
    }

    private static Color BlendTheme(ScenePalette p, Color natural, Color? customColor, double naturalWeight)
    {
        Color themeAnchor = p.IsLight ? p.Lo : p.Accent;
        if (customColor is Color custom)
            return EffectPalette.Blend(p.Surface, custom, 0.68);
        Color themed = EffectPalette.Blend(themeAnchor, natural, naturalWeight);
        return EffectPalette.Blend(p.Surface, themed, p.IsLight ? 0.68 : 0.74);
    }

    private static byte SoftAlpha(double baseAlpha, ScenePalette p, double scale = 1.0) =>
        (byte)Math.Clamp(Math.Round(baseAlpha * (p.IsLight ? 0.94 : 1.0) * scale), 0, 255);

    private static Brush CreateGlowBrush(Color c, byte centerAlpha, byte midAlpha = 0)
    {
        var brush = new RadialGradientBrush();
        brush.GradientStops.Add(new GradientStop(WithAlpha(c, centerAlpha), 0));
        if (midAlpha > 0) brush.GradientStops.Add(new GradientStop(WithAlpha(c, midAlpha), 0.52));
        brush.GradientStops.Add(new GradientStop(WithAlpha(c, 0), 1));
        brush.Freeze();
        return brush;
    }

    private static Brush CreateSweepBrush(Point start, Point end, params (Color Color, double Offset)[] stops)
    {
        var brush = new LinearGradientBrush { StartPoint = start, EndPoint = end };
        foreach (var (color, offset) in stops)
            brush.GradientStops.Add(new GradientStop(color, offset));
        brush.Freeze();
        return brush;
    }

    private static Pen FrozenPen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        pen.Freeze();
        return pen;
    }

    public static void DrawItem(DrawingContext dc, SceneEffectItem item, Point center, double baseScale, double time, Color themeTint, Color surfaceColor)
    {
        bool customSet = AppearanceOptions.TryParseHex(item.ColorHex, out Color custom);
        Color? customColor = customSet ? custom : null;
        var palette = BuildPalette(surfaceColor, themeTint);

        double scale = Math.Clamp(item.Scale, .4, 2.4) * baseScale;
        double opacity = Math.Clamp(item.Opacity, .15, 1.0);
        double t = time * Math.Clamp(item.Speed, .2, 2.5) + item.Seed * 7.13;

        var matrix = Matrix.Identity;
        matrix.Scale(item.FlipX ? -scale : scale, scale);
        if (Math.Abs(item.Rotation) > .01) matrix.Rotate(item.Rotation);
        matrix.Translate(center.X, center.Y);

        dc.PushOpacity(opacity);
        dc.PushTransform(new MatrixTransform(matrix));

        switch (SceneEffectCatalog.NormalizeKind(item.Kind))
        {
            case SceneEffectCatalog.Grass:
                DrawGrass(dc, t, palette, item.Seed, customColor);
                break;
            case SceneEffectCatalog.Vine:
                DrawVine(dc, t, palette, item.Seed, customColor);
                break;
            case SceneEffectCatalog.Sakura:
                DrawSakura(dc, t, palette, customColor);
                break;
            case SceneEffectCatalog.Lantern:
                DrawLantern(dc, t, palette, customColor);
                break;
            case SceneEffectCatalog.Fireflies:
                DrawFireflyCluster(dc, t, palette, item.Seed, customColor);
                break;
            case SceneEffectCatalog.Sparkle:
                DrawSparkleBurst(dc, t, palette, item.Seed, customColor);
                break;
            case SceneEffectCatalog.Butterfly:
                DrawButterfly(dc, t, palette, customColor);
                break;
            case SceneEffectCatalog.Gear:
                DrawGear(dc, t, palette, customColor);
                break;
            case SceneEffectCatalog.Ripple:
                DrawRipple(dc, t, palette, customColor);
                break;
        }

        dc.Pop();
        dc.Pop();
    }

    private static void DrawGrass(DrawingContext dc, double t, ScenePalette p, double seed, Color? customColor)
    {
        Color meadow = BlendTheme(p, Color.FromRgb(0x47, 0x88, 0x5D), customColor, p.IsLight ? 0.56 : 0.48);
        Color rootTint = EffectPalette.Blend(p.Surface, meadow, 0.35);
        Color tipTint = EffectPalette.Blend(meadow, p.Hi, p.IsLight ? 0.25 : 0.46);
        Color reedTone = BlendTheme(p, Color.FromRgb(0xBA, 0x9B, 0x58), customColor, p.IsLight ? 0.48 : 0.40);

        // Soft horizon mist anchoring the tuft smoothly into the surface
        dc.DrawEllipse(CreateGlowBrush(meadow, SoftAlpha(34, p), SoftAlpha(14, p)), null, new Point(0, -4), 34, 15);

        double wind = Math.Sin(t * 2.1 + seed * 3.1) * 5.6 + Math.Sin(t * 4.5 + seed * 5.3) * 1.3;
        (double rx, double h, double w, double lean, bool back)[] blades =
        [
            (-23, 25, 3.6, -6.5, true),
            (-16, 35, 4.0, -3.2, false),
            (-10, 42, 4.3, -1.0, true),
            (-3, 46, 4.6, 1.8, false),
            (4, 41, 4.1, 3.4, true),
            (11, 35, 3.9, 5.2, false),
            (18, 26, 3.4, 7.0, true),
            (24, 19, 3.0, 8.2, false)
        ];

        var rimPen = FrozenPen(FrozenBrush(WithAlpha(p.IsLight ? p.Lo : p.Hi, SoftAlpha(38, p))), 0.65);

        for (int i = 0; i < blades.Length; i++)
        {
            var (rx, h, w, lean, back) = blades[i];
            double localSway = wind * (.72 + (h / 46.0) * .55) + Math.Sin(t * 3.2 + i * .75 + seed) * 2.1;
            double tipX = rx + lean + localSway;
            double tipY = -h;
            double midX = rx + (lean + localSway) * .42;
            double midY = -h * .55;

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(rx - w * .52, 3), isFilled: true, isClosed: true);
                ctx.QuadraticBezierTo(new Point(midX - w * .28, midY), new Point(tipX, tipY), isStroked: false, isSmoothJoin: true);
                ctx.QuadraticBezierTo(new Point(midX + w * .34, midY + 1.5), new Point(rx + w * .52, 3), isStroked: false, isSmoothJoin: true);
            }
            geo.Freeze();

            byte aRoot = SoftAlpha(back ? 10 : 18, p);
            byte aMid = SoftAlpha(back ? 42 : 76, p);
            byte aTip = SoftAlpha(back ? 24 : 52, p);
            var brush = CreateSweepBrush(new Point(0, 0), new Point(0, 1),
                (WithAlpha(tipTint, aTip), 0.0),
                (WithAlpha(meadow, aMid), 0.54),
                (WithAlpha(rootTint, aRoot), 1.0));
            dc.DrawGeometry(brush, null, geo);

            if (!back && i % 3 == 1)
            {
                var ridge = new StreamGeometry();
                using (var rctx = ridge.Open())
                {
                    rctx.BeginFigure(new Point(rx - w * .34, 1), false, false);
                    rctx.QuadraticBezierTo(new Point(midX - w * .20, midY), new Point(tipX, tipY), true, true);
                }
                ridge.Freeze();
                dc.DrawGeometry(null, rimPen, ridge);
            }
        }

        // Delicate seed stalks rising lightly through the blades (matching LonelyGrass)
        var reedPen = FrozenPen(CreateSweepBrush(new Point(0, 0), new Point(0, 1),
            (WithAlpha(reedTone, SoftAlpha(78, p)), 0.0),
            (WithAlpha(rootTint, SoftAlpha(16, p)), 1.0)), 0.9);
        var grainBrush = FrozenBrush(WithAlpha(reedTone, SoftAlpha(86, p)));
        for (int s = 0; s < 2; s++)
        {
            double sx = s == 0 ? -5.5 : 8.5;
            double sh = s == 0 ? 52 : 44;
            double sw = wind * 1.15 + s * 2.8;
            var stalk = new StreamGeometry();
            using (var ctx = stalk.Open())
            {
                ctx.BeginFigure(new Point(sx, 2), false, false);
                ctx.QuadraticBezierTo(new Point(sx + sw * 0.45, -sh * 0.56), new Point(sx + sw + (s == 0 ? 2 : 5), -sh), true, true);
            }
            stalk.Freeze();
            dc.DrawGeometry(null, reedPen, stalk);

            for (int k = 0; k < 3; k++)
            {
                double u = 0.72 + k * 0.12;
                double gx = sx + (sw + (s == 0 ? 2 : 5)) * u;
                double gy = -sh * u + 2 * (1 - u);
                dc.DrawEllipse(grainBrush, null, new Point(gx, gy), 1.9, 0.95);
            }
        }
    }

    private static void DrawVine(DrawingContext dc, double t, ScenePalette p, double seed, Color? customColor)
    {
        Color leafColor = BlendTheme(p, Color.FromRgb(0x46, 0x8F, 0x5B), customColor, p.IsLight ? 0.56 : 0.48);
        Color oliveLeaf = BlendTheme(p, Color.FromRgb(0x7B, 0xA3, 0x4E), customColor, p.IsLight ? 0.50 : 0.44);
        Color stemTone = EffectPalette.Blend(p.Surface, leafColor, 0.56);
        Color tipHi = EffectPalette.Blend(leafColor, p.Hi, p.IsLight ? 0.24 : 0.42);
        double sway = Math.Sin(t * 1.9 + seed * 2.7) * 6.5;

        // Soft canopy atmosphere anchoring the top edge
        dc.DrawEllipse(CreateGlowBrush(leafColor, SoftAlpha(30, p)), null, new Point(0, 6), 24, 14);

        var stem = new StreamGeometry();
        using (var ctx = stem.Open())
        {
            ctx.BeginFigure(new Point(0, -3), false, false);
            ctx.BezierTo(new Point(sway * .25 + 4, 20), new Point(sway * .65 - 4, 42), new Point(sway, 62), true, true);
        }
        stem.Freeze();
        var stemBrush = CreateSweepBrush(new Point(0, 0), new Point(0, 1),
            (WithAlpha(p.Surface, SoftAlpha(20, p)), 0.0),
            (WithAlpha(stemTone, SoftAlpha(74, p)), 0.35),
            (WithAlpha(leafColor, SoftAlpha(48, p)), 1.0));
        dc.DrawGeometry(null, FrozenPen(stemBrush, 1.45), stem);

        var veinPen = FrozenPen(FrozenBrush(WithAlpha(p.Hi, SoftAlpha(44, p))), 0.65);
        var outlinePen = FrozenPen(FrozenBrush(WithAlpha(p.IsLight ? p.Lo : p.Hi, SoftAlpha(30, p))), 0.65);

        for (int i = 0; i < 6; i++)
        {
            bool shadow = i == 1 || i == 4;
            double frac = (i + 0.7) / 6.2;
            double lx = sway * frac * frac;
            double ly = frac * 59;
            double side = i % 2 == 0 ? 1 : -1;
            double flutter = Math.Sin(t * 3.1 + i * .9) * 7.5;
            double lScale = shadow ? 0.85 : 1.0;

            var m = Matrix.Identity;
            m.Scale(side * lScale, lScale);
            m.Rotate(side * (26 + flutter));
            m.Translate(lx, ly);
            dc.PushTransform(new MatrixTransform(m));

            var leafGeo = new StreamGeometry();
            using (var ctx = leafGeo.Open())
            {
                ctx.BeginFigure(new Point(0, 0), true, true);
                ctx.QuadraticBezierTo(new Point(7.5, -5.2), new Point(13.5, 1), false, true);
                ctx.QuadraticBezierTo(new Point(6.5, 5.8), new Point(0, 0), false, true);
            }
            leafGeo.Freeze();

            Color fillTone = i % 2 == 0 ? leafColor : oliveLeaf;
            var leafBrush = CreateSweepBrush(new Point(0, 0), new Point(1, 0.4),
                (WithAlpha(fillTone, SoftAlpha(shadow ? 24 : 58, p)), 0.0),
                (WithAlpha(tipHi, SoftAlpha(shadow ? 16 : 42, p)), 1.0));
            dc.DrawGeometry(leafBrush, shadow ? null : outlinePen, leafGeo);

            if (!shadow)
                dc.DrawLine(veinPen, new Point(1.2, 0.1), new Point(10.5, 0.7));
            dc.Pop();
        }
    }

    private static void DrawSakura(DrawingContext dc, double t, ScenePalette p, Color? customColor)
    {
        Color petalColor = BlendTheme(p, Color.FromRgb(0xF4, 0x9C, 0xC6), customColor, p.IsLight ? 0.56 : 0.48);
        Color petalHi = EffectPalette.Blend(petalColor, p.Hi, 0.52);
        Color branchColor = BlendTheme(p, Color.FromRgb(0x6D, 0x45, 0x56), customColor, 0.52);
        double sway = Math.Sin(t * 1.8) * 3.0;

        dc.DrawEllipse(CreateGlowBrush(petalColor, SoftAlpha(30, p)), null, new Point(0, 4), 34, 18);

        var branch = new StreamGeometry();
        using (var ctx = branch.Open())
        {
            ctx.BeginFigure(new Point(32, -8), false, false);
            ctx.QuadraticBezierTo(new Point(8, 2 + sway * .4), new Point(-26, 12 + sway), true, true);
        }
        branch.Freeze();
        var branchBrush = CreateSweepBrush(new Point(1, 0), new Point(0, 1),
            (WithAlpha(p.Surface, SoftAlpha(20, p)), 0.0),
            (WithAlpha(branchColor, SoftAlpha(76, p)), 0.45),
            (WithAlpha(branchColor, SoftAlpha(42, p)), 1.0));
        dc.DrawGeometry(null, FrozenPen(branchBrush, 1.8), branch);

        var fill = CreateSweepBrush(new Point(0, 1), new Point(0, 0),
            (WithAlpha(petalColor, SoftAlpha(66, p)), 0.0),
            (WithAlpha(petalHi, SoftAlpha(48, p)), 1.0));
        var petalEdge = FrozenPen(FrozenBrush(WithAlpha(p.Hi, SoftAlpha(38, p))), 0.6);
        var coreBrush = FrozenBrush(WithAlpha(p.Hi, SoftAlpha(92, p)));

        Point[] blooms = [new(-21, 11 + sway), new(-3, 4 + sway * .5), new(14, -2 + sway * .2)];
        for (int b = 0; b < blooms.Length; b++)
        {
            double s = b == 1 ? 0.96 : .76;
            for (int k = 0; k < 5; k++)
            {
                var pm = Matrix.Identity;
                pm.Scale(s, s);
                pm.Rotate(k * 72 + Math.Sin(t * 2 + b) * 4.5);
                pm.Translate(blooms[b].X, blooms[b].Y);
                dc.PushTransform(new MatrixTransform(pm));
                dc.DrawGeometry(fill, petalEdge, BlossomPetal);
                dc.Pop();
            }
            dc.DrawEllipse(coreBrush, null, blooms[b], 1.7 * s, 1.7 * s);
        }

        // Drifting petals fading softly below the bough
        for (int d = 0; d < 2; d++)
        {
            double fall = (t * 0.28 + d * 0.5) % 1.0;
            double px = -12 + d * 18 + Math.Sin(t * 2.2 + d * 2.1) * 5.0;
            double py = 12 + fall * 20;
            byte da = SoftAlpha((1.0 - fall) * 58, p);
            dc.DrawEllipse(FrozenBrush(WithAlpha(petalHi, da)), null, new Point(px, py), 2.5, 1.5);
        }
    }

    private static void DrawLantern(DrawingContext dc, double t, ScenePalette p, Color? customColor)
    {
        Color glowColor = BlendTheme(p, Color.FromRgb(0xF5, 0x9E, 0x0B), customColor, p.IsLight ? 0.56 : 0.46);
        Color warmCore = EffectPalette.Blend(glowColor, p.Hi, 0.58);
        Color capTone = EffectPalette.Blend(p.Surface, p.IsLight ? p.Lo : glowColor, 0.48);
        double swing = Math.Sin(t * 1.7) * 4.5;
        double pulse = .84 + .16 * Math.Sin(t * 3.6);

        // Wide diffused ambient glow so light bleeds naturally into the panel
        dc.DrawEllipse(CreateGlowBrush(glowColor, SoftAlpha(58 * pulse, p), SoftAlpha(22 * pulse, p)), null, new Point(swing * .35, 3), 38, 40);

        var cordBrush = CreateSweepBrush(new Point(0, 0), new Point(0, 1),
            (WithAlpha(p.Surface, SoftAlpha(12, p)), 0.0),
            (WithAlpha(capTone, SoftAlpha(72, p)), 1.0));
        dc.DrawLine(FrozenPen(cordBrush, 1.2), new Point(0, -27), new Point(swing * .35, -12));

        var m = Matrix.Identity;
        m.Rotate(swing * .85);
        m.Translate(swing * .35, 0);
        dc.PushTransform(new MatrixTransform(m));

        var bodyBrush = new RadialGradientBrush
        {
            GradientOrigin = new Point(0.5, 0.46),
            Center = new Point(0.5, 0.48),
            RadiusX = 0.65,
            RadiusY = 0.65
        };
        bodyBrush.GradientStops.Add(new GradientStop(WithAlpha(warmCore, SoftAlpha(96 * pulse, p)), 0));
        bodyBrush.GradientStops.Add(new GradientStop(WithAlpha(glowColor, SoftAlpha(54, p)), 0.72));
        bodyBrush.GradientStops.Add(new GradientStop(WithAlpha(glowColor, SoftAlpha(28, p)), 1.0));
        bodyBrush.Freeze();

        var rimPen = FrozenPen(FrozenBrush(WithAlpha(warmCore, SoftAlpha(66, p))), 0.85);
        dc.DrawRoundedRectangle(bodyBrush, rimPen, new Rect(-10, -10, 20, 24), 7.5, 7.5);

        // Subtle horizontal lantern ribs
        var ribPen = FrozenPen(FrozenBrush(WithAlpha(p.IsLight ? p.Lo : p.Hi, SoftAlpha(34, p))), 0.65);
        dc.DrawLine(ribPen, new Point(-9.2, -2), new Point(9.2, -2));
        dc.DrawLine(ribPen, new Point(-9.2, 6), new Point(9.2, 6));

        var capBrush = FrozenBrush(WithAlpha(capTone, SoftAlpha(78, p)));
        dc.DrawRoundedRectangle(capBrush, null, new Rect(-6.5, -12.5, 13, 3.2), 1.4, 1.4);
        dc.DrawRoundedRectangle(capBrush, null, new Rect(-5.5, 13.3, 11, 2.5), 1.2, 1.2);
        dc.DrawEllipse(CreateGlowBrush(p.Hi, SoftAlpha(95 * pulse, p)), null, new Point(0, 2), 6, 8);
        dc.Pop();
    }

    private static void DrawFireflyCluster(DrawingContext dc, double t, ScenePalette p, double seed, Color? customColor)
    {
        Color glowColor = BlendTheme(p, p.Accent, customColor, 0.42);
        Color coreColor = EffectPalette.Blend(glowColor, p.Hi, 0.65);
        for (int i = 0; i < 6; i++)
        {
            double a = seed * 3.7 + i * 1.05;
            double x = Math.Sin(t * (1.2 + i * .17) + a) * (14 + i * 2.5);
            double y = Math.Cos(t * (1.5 + i * .13) + a * 1.4) * (11 + i * 2.0);
            double glow = .22 + .78 * Math.Pow(.5 + .5 * Math.Sin(t * 3.4 + i * 1.3), 2);
            double r = 9.5 + (i % 3) * 2.8;
            dc.DrawEllipse(CreateGlowBrush(glowColor, SoftAlpha(74 * glow, p), SoftAlpha(24 * glow, p)), null, new Point(x, y), r, r);
            dc.DrawEllipse(FrozenBrush(WithAlpha(coreColor, SoftAlpha(128 * glow, p))), null, new Point(x, y), 1.35, 1.35);
        }
    }

    private static void DrawSparkleBurst(DrawingContext dc, double t, ScenePalette p, double seed, Color? customColor)
    {
        Color tone = BlendTheme(p, p.Accent, customColor, 0.42);
        Color bright = EffectPalette.Blend(tone, p.Hi, 0.58);
        dc.DrawEllipse(CreateGlowBrush(tone, SoftAlpha(28, p)), null, new Point(0, 0), 26, 26);

        for (int i = 0; i < 5; i++)
        {
            double angle = i * (Math.Tau / 5) + seed;
            double dist = i == 0 ? 0 : 12 + (i % 2) * 6;
            double pulse = .30 + .70 * Math.Pow(.5 + .5 * Math.Sin(t * 3.8 + i * 1.15), 2);
            double s = (i == 0 ? 1.12 : .66) * (.75 + .35 * pulse);

            var m = Matrix.Identity;
            m.Scale(s, s);
            m.Rotate(t * 18 + i * 20);
            m.Translate(Math.Cos(angle) * dist, Math.Sin(angle) * dist);
            dc.PushTransform(new MatrixTransform(m));
            dc.DrawGeometry(FrozenBrush(WithAlpha(i == 0 ? bright : tone, SoftAlpha(108 * pulse, p))), null, StarGlyph);
            dc.Pop();
        }
    }

    private static void DrawButterfly(DrawingContext dc, double t, ScenePalette p, Color? customColor)
    {
        Color wingTone = BlendTheme(p, Color.FromRgb(0x6C, 0xD4, 0xC8), customColor, p.IsLight ? 0.50 : 0.42);
        Color wingHi = EffectPalette.Blend(wingTone, p.Hi, 0.48);
        double flap = .30 + .70 * Math.Abs(Math.Sin(t * 7.5));
        double hoverX = Math.Sin(t * 2.0) * 8;
        double hoverY = Math.Sin(t * 4.0) * 4;

        var m = Matrix.Identity;
        m.Translate(hoverX, hoverY);
        dc.PushTransform(new MatrixTransform(m));

        dc.DrawEllipse(CreateGlowBrush(wingTone, SoftAlpha(30, p)), null, new Point(0, 0), 24, 18);

        var wingBrush = CreateSweepBrush(new Point(0, 0.5), new Point(1, 0.2),
            (WithAlpha(wingHi, SoftAlpha(72, p)), 0.0),
            (WithAlpha(wingTone, SoftAlpha(34, p)), 1.0));
        var wingPen = FrozenPen(FrozenBrush(WithAlpha(p.Hi, SoftAlpha(48, p))), 0.65);

        for (int side = -1; side <= 1; side += 2)
        {
            var wm = Matrix.Identity;
            wm.Scale(side * flap, 1);
            dc.PushTransform(new MatrixTransform(wm));

            var upper = new StreamGeometry();
            using (var ctx = upper.Open())
            {
                ctx.BeginFigure(new Point(0, -1), true, true);
                ctx.BezierTo(new Point(9, -15), new Point(21, -11), new Point(14, 1), false, true);
                ctx.QuadraticBezierTo(new Point(6, 3), new Point(0, -1), false, true);
            }
            upper.Freeze();
            var lower = new StreamGeometry();
            using (var ctx = lower.Open())
            {
                ctx.BeginFigure(new Point(0, 1), true, true);
                ctx.BezierTo(new Point(11, 2), new Point(12, 13), new Point(3, 11), false, true);
                ctx.QuadraticBezierTo(new Point(1, 7), new Point(0, 1), false, true);
            }
            lower.Freeze();

            dc.DrawGeometry(wingBrush, wingPen, upper);
            dc.DrawGeometry(wingBrush, wingPen, lower);
            dc.Pop();
        }

        var bodyPen = FrozenPen(FrozenBrush(WithAlpha(EffectPalette.Blend(wingTone, p.Lo, 0.55), SoftAlpha(88, p))), 1.4);
        dc.DrawLine(bodyPen, new Point(0, -5.5), new Point(0, 6.5));
        dc.Pop();
    }

    private static void DrawGear(DrawingContext dc, double t, ScenePalette p, Color? customColor)
    {
        Color metal = BlendTheme(p, Color.FromRgb(0xC8, 0x9B, 0x54), customColor, p.IsLight ? 0.44 : 0.36);
        var stroke = FrozenPen(FrozenBrush(WithAlpha(metal, SoftAlpha(76, p))), 1.5);
        var innerPen = FrozenPen(FrozenBrush(WithAlpha(p.Hi, SoftAlpha(42, p))), 0.8);
        var fill = FrozenBrush(WithAlpha(metal, SoftAlpha(18, p)));

        dc.DrawEllipse(CreateGlowBrush(metal, SoftAlpha(24, p)), null, new Point(0, 0), 24, 24);

        var m = Matrix.Identity;
        m.Rotate(t * 28);
        dc.PushTransform(new MatrixTransform(m));
        dc.DrawEllipse(fill, stroke, new Point(0, 0), 13, 13);
        dc.DrawEllipse(null, innerPen, new Point(0, 0), 9.5, 9.5);
        dc.DrawEllipse(null, stroke, new Point(0, 0), 5, 5);
        for (int i = 0; i < 8; i++)
        {
            double a = i * (Math.Tau / 8);
            dc.DrawLine(stroke, new Point(Math.Cos(a) * 13, Math.Sin(a) * 13), new Point(Math.Cos(a) * 17.5, Math.Sin(a) * 17.5));
            if (i % 2 == 0)
                dc.DrawLine(stroke, new Point(Math.Cos(a) * 5, Math.Sin(a) * 5), new Point(Math.Cos(a) * 12, Math.Sin(a) * 12));
        }
        dc.Pop();
    }

    private static void DrawRipple(DrawingContext dc, double t, ScenePalette p, Color? customColor)
    {
        Color tone = BlendTheme(p, p.Accent, customColor, 0.42);
        Color glint = EffectPalette.Blend(tone, p.Hi, 0.45);
        dc.DrawEllipse(CreateGlowBrush(tone, SoftAlpha(22, p)), null, new Point(0, 0), 26, 26);

        for (int i = 0; i < 3; i++)
        {
            double frac = (t * .55 + i / 3.0) % 1.0;
            double r = 4 + frac * 26;
            byte a = SoftAlpha((1.0 - frac) * 86, p);
            var pen = FrozenPen(FrozenBrush(WithAlpha(i == 0 ? glint : tone, a)), 1.4 - frac * .65);
            dc.DrawEllipse(null, pen, new Point(0, 0), r, r);
        }
    }

    private static Color WithAlpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

    private static SolidColorBrush FrozenBrush(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}

/// <summary>Renders placed animated SceneEffectItems scoped to a single layout zone.</summary>
public sealed class SceneEffectsLayer : AmbientEffect
{
    public static readonly DependencyProperty ZoneProperty = DependencyProperty.Register(
        nameof(Zone), typeof(string), typeof(SceneEffectsLayer),
        new FrameworkPropertyMetadata(SceneEffectCatalog.ZoneContent, FrameworkPropertyMetadataOptions.AffectsRender, OnSourceChanged));

    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(
        nameof(Items), typeof(IEnumerable<SceneEffectItem>), typeof(SceneEffectsLayer),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnSourceChanged));

    private List<SceneEffectItem> scopedItems = [];

    public string Zone { get => (string)GetValue(ZoneProperty); set => SetValue(ZoneProperty, value); }
    public IEnumerable<SceneEffectItem>? Items { get => (IEnumerable<SceneEffectItem>?)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

    protected override int FrameRate => 24;
    protected override bool HasActiveContent => scopedItems.Count > 0;

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SceneEffectsLayer layer) layer.RebuildScopedItems();
    }

    public void RefreshItems() => RebuildScopedItems();

    private void RebuildScopedItems()
    {
        string zone = SceneEffectCatalog.NormalizeZone(Zone);
        scopedItems = Items is null
            ? []
            : Items.Where(i => i is not null && SceneEffectCatalog.NormalizeZone(i.Zone) == zone).ToList();
        UpdateAnimation();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (scopedItems.Count == 0 || ActualWidth < 8 || ActualHeight < 8) return;
        Color surface = SceneEffectRenderer.ResolveZoneSurfaceColor(this, Zone);
        double t = Time * 2.4;
        foreach (var item in scopedItems)
        {
            var pt = new Point(Math.Clamp(item.X, 0, 1) * ActualWidth, Math.Clamp(item.Y, 0, 1) * ActualHeight);
            SceneEffectRenderer.DrawItem(dc, item, pt, 1.0, t, Tint, surface);
        }
    }
}

/// <summary>Compact animated thumbnail preview for a single SceneEffect kind in the drag palette.</summary>
public sealed class SceneEffectPreview : AmbientEffect
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(string), typeof(SceneEffectPreview),
        new FrameworkPropertyMetadata(SceneEffectCatalog.Grass, FrameworkPropertyMetadataOptions.AffectsRender));

    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth < 12 || ActualHeight < 12) return;
        var desc = SceneEffectCatalog.DescriptorFor(Kind);
        double anchorY = desc.Kind == SceneEffectCatalog.Grass ? ActualHeight * .86
            : desc.Kind == SceneEffectCatalog.Vine ? ActualHeight * .18
            : ActualHeight * .52;
        Color cardSurface = SceneEffectRenderer.ResolveFirstBrushColor(this, ["Sidebar", "Canvas"], Color.FromRgb(0xF7, 0xF8, 0xF8));
        var sample = new SceneEffectItem { Kind = desc.Kind, Scale = .78, Opacity = 1.0, Seed = .25 };
        SceneEffectRenderer.DrawItem(dc, sample, new Point(ActualWidth * .5, anchorY), .75, Time * 2.4, Tint, cardSurface);
    }
}

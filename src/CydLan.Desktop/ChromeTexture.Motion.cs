using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Brush = System.Windows.Media.Brush;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

/// <summary>Texture motion: stacked layers, swaying plants and animated transforms over the cached drawings.</summary>
public sealed partial class ChromeTexture
{
    private sealed record TextureLayer(
        ChromeTextureKind Kind, Brush? Tile, DrawingGroup? Vector, Brush? Wash,
        double Width, double Height, double Intensity, System.Windows.Media.Color Tint, System.Windows.Media.Color Surface,
        bool Motion, LayerMotion? Animation);

    /// <summary>How a layer moves: a transform, an opacity clock, and whether the drawing repeats to scroll seamlessly.</summary>
    private sealed record LayerMotion(Transform? Transform, AnimationClock? Opacity, Vector Repeat);

    private enum MotionStyle { None, Sway, Drift, Twinkle, Flash, Fall, Rise, Float, TileScroll }

    // Plants built while this is set get animated sway transforms instead of static ones.
    [ThreadStatic] private static bool swayBuilding;
    private static SkewTransform[]? bladeSway;
    private static RotateTransform[]? leafSway;
    private static double swaySpeed;

    private TextureLayer GetLayer(ChromeTextureKind kind, double width, double height, double intensity, bool motion)
    {
        bool sizeMatters = !IsRepeatingPattern(kind);
        if (layers.TryGetValue(kind, out var cached) &&
            Math.Abs(cached.Intensity - intensity) < .001 && cached.Tint == Tint && cached.Surface == SurfaceColor && cached.Motion == motion &&
            (!sizeMatters || (Math.Abs(cached.Width - width) < .5 && Math.Abs(cached.Height - height) < .5)))
            return cached;

        ResetScratch();
        swayBuilding = motion && StyleFor(kind) == MotionStyle.Sway;
        try { EnsureCache(kind, width, height, intensity); }
        finally { swayBuilding = false; }

        var layer = new TextureLayer(kind, cachedTile, cachedVector, cachedWash, width, height, intensity, Tint, SurfaceColor, motion,
            motion ? BuildMotion(kind, width, height, cachedTile) : null);
        layers[kind] = layer;
        return layer;
    }

    private static void DrawLayer(DrawingContext dc, TextureLayer layer, Rect rect)
    {
        var motion = layer.Animation;
        int pushes = 0;
        if (motion?.Opacity is { } clock) { dc.PushOpacity(1, clock); pushes++; }
        if (motion?.Transform is { } transform) { dc.PushTransform(transform); pushes++; }

        var repeat = motion?.Repeat ?? default;
        if (layer.Tile is not null)
        {
            // Tiles scroll by exactly one tile, so the extra margin keeps the edges covered.
            var area = repeat == default ? rect : new Rect(-repeat.X - 1, -repeat.Y - 1, rect.Width + Math.Abs(repeat.X) * 2 + 2, rect.Height + Math.Abs(repeat.Y) * 2 + 2);
            dc.DrawRectangle(layer.Tile, null, area);
        }
        if (layer.Vector is not null)
        {
            dc.DrawDrawing(layer.Vector);
            if (repeat != default && layer.Tile is null)
            {
                dc.PushTransform(new TranslateTransform(-repeat.X, -repeat.Y));
                dc.DrawDrawing(layer.Vector);
                dc.Pop();
            }
        }
        for (int i = 0; i < pushes; i++) dc.Pop();
    }

    private static MotionStyle StyleFor(ChromeTextureKind kind) => kind switch
    {
        ChromeTextureKind.GrassyFields or ChromeTextureKind.LonelyGrass or ChromeTextureKind.Botanical or
            ChromeTextureKind.Leaves or ChromeTextureKind.Sakura or ChromeTextureKind.Bamboo => MotionStyle.Sway,
        ChromeTextureKind.Woven or ChromeTextureKind.Grid or ChromeTextureKind.Hex or ChromeTextureKind.Carbon or
            ChromeTextureKind.Waves or ChromeTextureKind.ArtDeco or ChromeTextureKind.Mesh or ChromeTextureKind.Plaid or
            ChromeTextureKind.Chevron or ChromeTextureKind.Grain => MotionStyle.TileScroll,
        ChromeTextureKind.StarryNight or ChromeTextureKind.Starburst or ChromeTextureKind.Circuit or
            ChromeTextureKind.ArcReactor or ChromeTextureKind.Skyline or ChromeTextureKind.Galaxy => MotionStyle.Twinkle,
        ChromeTextureKind.Lightning => MotionStyle.Flash,
        ChromeTextureKind.Snowfall or ChromeTextureKind.Rain or ChromeTextureKind.Cipher => MotionStyle.Fall,
        ChromeTextureKind.Bubbles or ChromeTextureKind.Lanterns => MotionStyle.Rise,
        ChromeTextureKind.Sun or ChromeTextureKind.Moon or ChromeTextureKind.Ocean => MotionStyle.Float,
        ChromeTextureKind.Off => MotionStyle.None,
        _ => MotionStyle.Drift
    };

    private static double Seconds(double baseSeconds) => baseSeconds / Math.Max(.4, AmbientEffect.SpeedMultiplier);

    private static DoubleAnimation Swing(double from, double to, double seconds, double delay = 0) =>
        new(from, to, TimeSpan.FromSeconds(Seconds(seconds)))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            BeginTime = TimeSpan.FromSeconds(delay),
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };

    private static DoubleAnimation Loop(double from, double to, double seconds) =>
        new(from, to, TimeSpan.FromSeconds(Seconds(seconds))) { RepeatBehavior = RepeatBehavior.Forever };

    private static LayerMotion? BuildMotion(ChromeTextureKind kind, double width, double height, Brush? tile)
    {
        switch (StyleFor(kind))
        {
            case MotionStyle.TileScroll when tile is TileBrush brush:
            {
                var size = brush.Viewport.Size;
                var shift = new TranslateTransform();
                bool diagonal = kind is ChromeTextureKind.Carbon or ChromeTextureKind.Mesh or ChromeTextureKind.Chevron or ChromeTextureKind.Grain;
                double seconds = Math.Clamp(size.Width / 7, 6, 30);
                shift.BeginAnimation(TranslateTransform.XProperty, Loop(0, size.Width, seconds));
                if (diagonal) shift.BeginAnimation(TranslateTransform.YProperty, Loop(0, size.Height, seconds * size.Height / Math.Max(1, size.Width)));
                return new LayerMotion(shift, null, new Vector(size.Width, diagonal ? size.Height : 0));
            }
            case MotionStyle.Drift:
            {
                // Slightly enlarged so the drifting layer never shows its edge.
                var group = new TransformGroup();
                group.Children.Add(new ScaleTransform(1.08, 1.08, width / 2, height / 2));
                var shift = new TranslateTransform();
                shift.BeginAnimation(TranslateTransform.XProperty, Swing(-width * .03, width * .03, kind == ChromeTextureKind.Clouds ? 18 : 11));
                shift.BeginAnimation(TranslateTransform.YProperty, Swing(-height * .015, height * .015, 7.5));
                group.Children.Add(shift);
                return new LayerMotion(group, kind is ChromeTextureKind.Aurora ? Swing(.55, 1, 4.2).CreateClock() : null, default);
            }
            case MotionStyle.Twinkle:
            {
                var breathe = new ScaleTransform(1, 1, width / 2, height / 2);
                var swell = Swing(1, 1.025, 9);
                breathe.BeginAnimation(ScaleTransform.ScaleXProperty, swell);
                breathe.BeginAnimation(ScaleTransform.ScaleYProperty, swell);
                return new LayerMotion(breathe, Swing(.5, 1, kind == ChromeTextureKind.Skyline ? 2.4 : 1.8).CreateClock(), default);
            }
            case MotionStyle.Flash:
            {
                var flash = new DoubleAnimationUsingKeyFrames { RepeatBehavior = RepeatBehavior.Forever, Duration = TimeSpan.FromSeconds(Seconds(4.2)) };
                foreach (var (t, v) in new[] { (0.0, .35), (2.6, .35), (2.7, 1.0), (2.82, .45), (2.95, 1.0), (3.5, .35), (4.2, .35) })
                    flash.KeyFrames.Add(new LinearDoubleKeyFrame(v, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(Seconds(t)))));
                return new LayerMotion(null, flash.CreateClock(), default);
            }
            case MotionStyle.Fall:
            case MotionStyle.Rise:
            {
                bool fall = StyleFor(kind) == MotionStyle.Fall;
                var shift = new TranslateTransform();
                double seconds = kind == ChromeTextureKind.Rain ? 2.2 : kind == ChromeTextureKind.Cipher ? 9 : fall ? 14 : 18;
                shift.BeginAnimation(TranslateTransform.YProperty, fall ? Loop(0, height, seconds) : Loop(0, -height, seconds));
                if (kind == ChromeTextureKind.Snowfall) shift.BeginAnimation(TranslateTransform.XProperty, Swing(-6, 6, 3.4));
                return new LayerMotion(shift, null, new Vector(0, fall ? height : -height));
            }
            case MotionStyle.Float:
            {
                var bob = new TranslateTransform();
                bob.BeginAnimation(TranslateTransform.YProperty, Swing(-Math.Max(2, height * .02), Math.Max(2, height * .02), kind == ChromeTextureKind.Ocean ? 2.6 : 5));
                if (kind == ChromeTextureKind.Ocean) bob.BeginAnimation(TranslateTransform.XProperty, Swing(-width * .012, width * .012, 4.4));
                var group = new TransformGroup();
                group.Children.Add(new ScaleTransform(1.04, 1.04, width / 2, height / 2));
                group.Children.Add(bob);
                return new LayerMotion(group, kind == ChromeTextureKind.Ocean ? null : Swing(.78, 1, 3.6).CreateClock(), default);
            }
            default:
                return null;
        }
    }

    private static void EnsureSwayPools()
    {
        double speed = AmbientEffect.SpeedMultiplier;
        if (bladeSway is not null && Math.Abs(swaySpeed - speed) < .001) return;
        swaySpeed = speed;
        bladeSway = new SkewTransform[6];
        leafSway = new RotateTransform[6];
        for (int i = 0; i < bladeSway.Length; i++)
        {
            double reach = 5 + i * 1.6;
            var skew = new SkewTransform();
            skew.BeginAnimation(SkewTransform.AngleXProperty, Swing(-reach * .35, reach, 2.2 + i * .37, i * .23));
            bladeSway[i] = skew;
            var turn = new RotateTransform();
            turn.BeginAnimation(RotateTransform.AngleProperty, Swing(-2.5 - i * .5, 2.5 + i * .5, 2.8 + i * .41, i * .31));
            leafSway[i] = turn;
        }
    }

    private static int SwaySlot(Point origin) => (int)(Math.Abs(origin.X * .37 + origin.Y * .11)) % 6;

    /// <summary>Pivots the next drawing around its root with a shared animated transform.</summary>
    private static bool PushSway(DrawingContext dc, Point origin, bool blade)
    {
        if (!swayBuilding) return false;
        EnsureSwayPools();
        var group = new TransformGroup();
        group.Children.Add(new TranslateTransform(-origin.X, -origin.Y));
        group.Children.Add(blade ? bladeSway![SwaySlot(origin)] : leafSway![SwaySlot(origin)]);
        group.Children.Add(new TranslateTransform(origin.X, origin.Y));
        dc.PushTransform(group);
        return true;
    }

    private static void DrawTaperedBlade(DrawingContext dc, Brush? fill, Pen? edgePen, Point origin, double length, double angleRad, double curveRad, double baseHalfW)
    {
        bool sway = PushSway(dc, origin, blade: true);
        DrawTaperedBladeCore(dc, fill, edgePen, origin, length, angleRad, curveRad, baseHalfW);
        if (sway) dc.Pop();
    }

    private static void DrawOrganicLeaf(DrawingContext dc, Brush fill, Pen? outlinePen, Pen? veinPen, Point origin, double scale, double angleDeg, LeafSpecies species)
    {
        bool sway = PushSway(dc, origin, blade: false);
        DrawOrganicLeafCore(dc, fill, outlinePen, veinPen, origin, scale, angleDeg, species);
        if (sway) dc.Pop();
    }

    private static void DrawSplitMonstera(DrawingContext dc, Brush fill, Pen? outlinePen, Pen? veinPen, Point center, double scale, double angleDeg)
    {
        bool sway = PushSway(dc, center, blade: false);
        DrawSplitMonsteraCore(dc, fill, outlinePen, veinPen, center, scale, angleDeg);
        if (sway) dc.Pop();
    }

    private static void DrawFernFrond(DrawingContext dc, Pen stemPen, Brush pinnaFill, Pen? pinnaPen, Point origin, double length, double angle, double bend, int pinnaPairs, double baseScale)
    {
        bool sway = PushSway(dc, origin, blade: false);
        DrawFernFrondCore(dc, stemPen, pinnaFill, pinnaPen, origin, length, angle, bend, pinnaPairs, baseScale);
        if (sway) dc.Pop();
    }

    private static void DrawVineTrail(DrawingContext dc, Pen stemPen, Brush fill, Pen? veinPen, Point origin, double length, double angle, double bend, int pairs, double baseScale)
    {
        bool sway = PushSway(dc, origin, blade: false);
        DrawVineTrailCore(dc, stemPen, fill, veinPen, origin, length, angle, bend, pairs, baseScale);
        if (sway) dc.Pop();
    }

    private static void DrawFlowerBud(DrawingContext dc, Brush fill, Pen? pen, Point origin, double angleRad, double scale)
    {
        bool sway = PushSway(dc, origin, blade: false);
        DrawFlowerBudCore(dc, fill, pen, origin, angleRad, scale);
        if (sway) dc.Pop();
    }
}

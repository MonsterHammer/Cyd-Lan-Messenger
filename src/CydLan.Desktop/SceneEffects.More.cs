using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

/// <summary>Additional procedural scene effects: weather, sky, creatures, and props.</summary>
internal static partial class SceneEffectRenderer
{
    private static readonly Geometry LeafShape = Frozen(Geometry.Parse("M0,-5.5 C3.8,-3 3.8,3 0,5.5 C-3.8,3 -3.8,-3 0,-5.5 Z"));
    private static readonly Geometry HeartShape = Frozen(Geometry.Parse("M0,3.6 C-6.4,-1.6 -4.4,-7.4 0,-4.2 C4.4,-7.4 6.4,-1.6 0,3.6 Z"));
    private static readonly Geometry FlameShape = Frozen(Geometry.Parse("M0,-9 C3.4,-4.2 4.0,-0.6 0,2.4 C-4.0,-0.6 -3.4,-4.2 0,-9 Z"));
    private static readonly Geometry BoltShape = Frozen(Geometry.Parse("M3,-24 L-6,-2 L0,-2 L-4,22 L8,-6 L1,-6 L6,-24 Z"));
    private static readonly Geometry CrystalShape = Frozen(Geometry.Parse("M0,-22 L11,-6 L7,18 L-7,18 L-11,-6 Z"));
    private static readonly Geometry CrescentShape = Frozen(new CombinedGeometry(GeometryCombineMode.Exclude,
        new EllipseGeometry(new Point(0, 0), 14, 14), new EllipseGeometry(new Point(6.5, -4.5), 12, 12)));
    private static readonly Geometry CloudShape = Frozen(new CombinedGeometry(GeometryCombineMode.Union,
        new CombinedGeometry(GeometryCombineMode.Union, new EllipseGeometry(new Point(-11, 2), 11, 8), new EllipseGeometry(new Point(2, -4), 13, 11)),
        new CombinedGeometry(GeometryCombineMode.Union, new EllipseGeometry(new Point(14, 3), 10, 7), new RectangleGeometry(new Rect(-15, 1, 30, 9), 4, 4))));
    private static readonly Geometry CloudShadow = Shifted(CloudShape.Clone(), 1.5, 2.5);
    private static readonly Geometry MushroomCap = Frozen(Geometry.Parse("M-16,0 C-16,-14 16,-14 16,0 Z"));
    private static readonly Geometry JellyDome = Frozen(Geometry.Parse("M-13,0 C-13,-17 13,-17 13,0 C8,2.5 4,-1 0,1.5 C-4,-1 -8,2.5 -13,0 Z"));

    private static Geometry Frozen(Geometry g)
    {
        g.Freeze();
        return g;
    }

    /// <summary>Stable pseudo-random value in [0, 1) for particle placement.</summary>
    private static double Hash(int i, double seed, double salt) =>
        Frac(Math.Sin((i + 1) * 12.9898 + seed * 78.233 + salt * 37.719) * 43758.5453);

    private static double Frac(double v) => v - Math.Floor(v);

    /// <summary>Fades particles in and out across a looping life fraction.</summary>
    private static double LifeFade(double frac) => Math.Clamp(Math.Min(frac / .18, (1 - frac) / .22), 0, 1);

    private static void DrawMore(DrawingContext dc, string kind, double t, ScenePalette p, double seed, Color? customColor)
    {
        switch (kind)
        {
            case SceneEffectCatalog.Snow: DrawSnow(dc, t, p, seed, customColor); break;
            case SceneEffectCatalog.Rain: DrawRain(dc, t, p, seed, customColor); break;
            case SceneEffectCatalog.Bubbles: DrawBubbles(dc, t, p, seed, customColor); break;
            case SceneEffectCatalog.Embers: DrawEmbers(dc, t, p, seed, customColor); break;
            case SceneEffectCatalog.Leaves: DrawLeaves(dc, t, p, seed, customColor); break;
            case SceneEffectCatalog.Stars: DrawStars(dc, t, p, seed, customColor); break;
            case SceneEffectCatalog.Moon: DrawMoon(dc, t, p, customColor); break;
            case SceneEffectCatalog.Sun: DrawSun(dc, t, p, customColor); break;
            case SceneEffectCatalog.Cloud: DrawCloud(dc, t, p, customColor); break;
            case SceneEffectCatalog.Hearts: DrawHearts(dc, t, p, seed, customColor); break;
            case SceneEffectCatalog.Notes: DrawNotes(dc, t, p, seed, customColor); break;
            case SceneEffectCatalog.Bird: DrawBird(dc, t, p, customColor); break;
            case SceneEffectCatalog.Fish: DrawFish(dc, t, p, customColor); break;
            case SceneEffectCatalog.Flower: DrawFlower(dc, t, p, customColor); break;
            case SceneEffectCatalog.Mushroom: DrawMushroom(dc, t, p, customColor); break;
            case SceneEffectCatalog.Candle: DrawCandle(dc, t, p, customColor); break;
            case SceneEffectCatalog.Lightning: DrawLightning(dc, t, p, seed, customColor); break;
            case SceneEffectCatalog.Comet: DrawComet(dc, t, p, customColor); break;
            case SceneEffectCatalog.Crystal: DrawCrystal(dc, t, p, customColor); break;
            case SceneEffectCatalog.Jellyfish: DrawJellyfish(dc, t, p, customColor); break;
        }
    }

    private static void DrawSnow(DrawingContext dc, double t, ScenePalette p, double seed, Color? customColor)
    {
        Color flake = BlendTheme(p, Color.FromRgb(0xF4, 0xF8, 0xFF), customColor, p.IsLight ? .45 : .85);
        for (int i = 0; i < 14; i++)
        {
            double life = Frac(t * (.10 + Hash(i, seed, 1) * .08) + Hash(i, seed, 2));
            double x = -30 + Hash(i, seed, 3) * 60 + Math.Sin(t * 1.3 + i) * 4;
            double y = -32 + life * 64;
            double r = 1.1 + Hash(i, seed, 4) * 1.5;
            dc.DrawEllipse(FrozenBrush(WithAlpha(flake, SoftAlpha(200 * LifeFade(life), p))), null, new Point(x, y), r, r);
        }
    }

    private static void DrawRain(DrawingContext dc, double t, ScenePalette p, double seed, Color? customColor)
    {
        Color drop = BlendTheme(p, Color.FromRgb(0x7D, 0xB4, 0xE8), customColor, .62);
        for (int i = 0; i < 16; i++)
        {
            double life = Frac(t * (.55 + Hash(i, seed, 1) * .3) + Hash(i, seed, 2));
            double x = -30 + Hash(i, seed, 3) * 60 - life * 5;
            double y = -32 + life * 64;
            var pen = FrozenPen(FrozenBrush(WithAlpha(drop, SoftAlpha(170 * LifeFade(life), p))), .9);
            dc.DrawLine(pen, new Point(x, y), new Point(x - 1.6, y + 7));
        }
    }

    private static void DrawBubbles(DrawingContext dc, double t, ScenePalette p, double seed, Color? customColor)
    {
        Color tone = BlendTheme(p, Color.FromRgb(0x8E, 0xD8, 0xF2), customColor, .55);
        Color shine = EffectPalette.Blend(tone, Colors.White, .6);
        for (int i = 0; i < 8; i++)
        {
            double life = Frac(t * (.12 + Hash(i, seed, 1) * .1) + Hash(i, seed, 2));
            double r = 2.4 + Hash(i, seed, 4) * 3.6;
            double x = -22 + Hash(i, seed, 3) * 44 + Math.Sin(t * 2 + i * 1.7) * 3;
            double y = 30 - life * 62;
            double fade = LifeFade(life);
            dc.DrawEllipse(FrozenBrush(WithAlpha(tone, SoftAlpha(30 * fade, p))),
                FrozenPen(FrozenBrush(WithAlpha(tone, SoftAlpha(150 * fade, p))), .8), new Point(x, y), r, r);
            dc.DrawEllipse(FrozenBrush(WithAlpha(shine, SoftAlpha(190 * fade, p))), null, new Point(x - r * .35, y - r * .4), r * .25, r * .25);
        }
    }

    private static void DrawEmbers(DrawingContext dc, double t, ScenePalette p, double seed, Color? customColor)
    {
        Color hot = BlendTheme(p, Color.FromRgb(0xFF, 0x8A, 0x3D), customColor, .82);
        Color core = EffectPalette.Blend(hot, Color.FromRgb(0xFF, 0xE6, 0x9A), .5);
        for (int i = 0; i < 12; i++)
        {
            double life = Frac(t * (.2 + Hash(i, seed, 1) * .15) + Hash(i, seed, 2));
            double x = -16 + Hash(i, seed, 3) * 32 + Math.Sin(t * 2.6 + i * 2.1) * (3 + life * 6);
            double y = 26 - life * 58;
            double fade = LifeFade(life) * (1 - life * .5);
            double r = 1.0 + Hash(i, seed, 4) * 1.2;
            dc.DrawEllipse(CreateGlowBrush(hot, SoftAlpha(120 * fade, p)), null, new Point(x, y), r * 3.6, r * 3.6);
            dc.DrawEllipse(FrozenBrush(WithAlpha(core, SoftAlpha(235 * fade, p))), null, new Point(x, y), r, r);
        }
    }

    private static void DrawLeaves(DrawingContext dc, double t, ScenePalette p, double seed, Color? customColor)
    {
        Color[] natural = [Color.FromRgb(0xE0, 0x7B, 0x39), Color.FromRgb(0xD9, 0xA4, 0x41), Color.FromRgb(0xB8, 0x4A, 0x32)];
        for (int i = 0; i < 6; i++)
        {
            double life = Frac(t * (.09 + Hash(i, seed, 1) * .06) + Hash(i, seed, 2));
            double x = -26 + Hash(i, seed, 3) * 52 + Math.Sin(t * 1.4 + i * 2.3) * 9;
            double y = -32 + life * 64;
            Color leaf = BlendTheme(p, natural[i % natural.Length], customColor, .72);
            dc.PushTransform(new TranslateTransform(x, y));
            dc.PushTransform(new RotateTransform(Math.Sin(t * 2.2 + i) * 70 + i * 40));
            dc.PushTransform(new ScaleTransform(Math.Cos(t * 3 + i) * .5 + .7, 1));
            dc.DrawGeometry(FrozenBrush(WithAlpha(leaf, SoftAlpha(220 * LifeFade(life), p))),
                FrozenPen(FrozenBrush(WithAlpha(EffectPalette.Blend(leaf, p.Lo, .3), SoftAlpha(120 * LifeFade(life), p))), .5), LeafShape);
            dc.Pop(); dc.Pop(); dc.Pop();
        }
    }

    private static void DrawStars(DrawingContext dc, double t, ScenePalette p, double seed, Color? customColor)
    {
        Color star = BlendTheme(p, Color.FromRgb(0xFF, 0xF1, 0xB8), customColor, p.IsLight ? .55 : .85);
        for (int i = 0; i < 9; i++)
        {
            double x = -30 + Hash(i, seed, 3) * 60;
            double y = -24 + Hash(i, seed, 5) * 48;
            double tw = (Math.Sin(t * (1.6 + Hash(i, seed, 1) * 2.2) + i * 2.4) + 1) * .5;
            double s = .35 + tw * .55 * (.6 + Hash(i, seed, 4) * .6);
            dc.DrawEllipse(CreateGlowBrush(star, SoftAlpha(70 * tw, p)), null, new Point(x, y), 7 * s, 7 * s);
            dc.PushTransform(new TranslateTransform(x, y));
            dc.PushTransform(new ScaleTransform(s, s));
            dc.DrawGeometry(FrozenBrush(WithAlpha(star, SoftAlpha(120 + 120 * tw, p))), null, StarGlyph);
            dc.Pop(); dc.Pop();
        }
    }

    private static void DrawMoon(DrawingContext dc, double t, ScenePalette p, Color? customColor)
    {
        Color moon = BlendTheme(p, Color.FromRgb(0xFA, 0xEE, 0xC6), customColor, p.IsLight ? .62 : .88);
        double bob = Math.Sin(t * .8) * 1.6;
        dc.DrawEllipse(CreateGlowBrush(moon, SoftAlpha(60 + Math.Sin(t * 1.3) * 14, p), SoftAlpha(18, p)), null, new Point(0, bob), 30, 30);
        dc.PushTransform(new TranslateTransform(0, bob));
        dc.PushTransform(new RotateTransform(-18 + Math.Sin(t * .6) * 4));
        dc.DrawGeometry(FrozenBrush(WithAlpha(moon, SoftAlpha(235, p))), null, CrescentShape);
        dc.Pop(); dc.Pop();
    }

    private static void DrawSun(DrawingContext dc, double t, ScenePalette p, Color? customColor)
    {
        Color sun = BlendTheme(p, Color.FromRgb(0xFF, 0xC8, 0x3D), customColor, .84);
        Color core = EffectPalette.Blend(sun, Color.FromRgb(0xFF, 0xF3, 0xC4), .45);
        dc.DrawEllipse(CreateGlowBrush(sun, SoftAlpha(80, p), SoftAlpha(26, p)), null, new Point(0, 0), 34, 34);
        dc.PushTransform(new RotateTransform(t * 14 % 360));
        for (int i = 0; i < 12; i++)
        {
            double a = i * Math.PI / 6;
            double len = 16 + (i % 2 == 0 ? 6 : 3) + Math.Sin(t * 2.4 + i) * 1.6;
            var pen = FrozenPen(FrozenBrush(WithAlpha(sun, SoftAlpha(i % 2 == 0 ? 210 : 150, p))), i % 2 == 0 ? 2.0 : 1.4);
            dc.DrawLine(pen, new Point(Math.Cos(a) * 13, Math.Sin(a) * 13), new Point(Math.Cos(a) * len, Math.Sin(a) * len));
        }
        dc.Pop();
        dc.DrawEllipse(FrozenBrush(WithAlpha(core, SoftAlpha(245, p))), FrozenPen(FrozenBrush(WithAlpha(sun, SoftAlpha(220, p))), 1.4), new Point(0, 0), 10.5, 10.5);
    }

    private static void DrawCloud(DrawingContext dc, double t, ScenePalette p, Color? customColor)
    {
        Color cloud = BlendTheme(p, p.IsLight ? Color.FromRgb(0xE6, 0xEC, 0xF3) : Color.FromRgb(0xEE, 0xF2, 0xF8), customColor, p.IsLight ? .72 : .62);
        double drift = Math.Sin(t * .45) * 7;
        dc.PushTransform(new TranslateTransform(drift, Math.Sin(t * .9) * 1.2));
        dc.DrawGeometry(FrozenBrush(WithAlpha(EffectPalette.Blend(cloud, p.Lo, .25), SoftAlpha(60, p))), null, CloudShadow);
        dc.DrawGeometry(FrozenBrush(WithAlpha(cloud, SoftAlpha(215, p))),
            FrozenPen(FrozenBrush(WithAlpha(EffectPalette.Blend(cloud, p.Hi, .4), SoftAlpha(120, p))), .8), CloudShape);
        dc.Pop();
    }

    private static Geometry Shifted(Geometry g, double dx, double dy)
    {
        g.Transform = new TranslateTransform(dx, dy);
        g.Freeze();
        return g;
    }

    private static void DrawHearts(DrawingContext dc, double t, ScenePalette p, double seed, Color? customColor)
    {
        Color heart = BlendTheme(p, Color.FromRgb(0xF2, 0x5C, 0x8A), customColor, .82);
        for (int i = 0; i < 5; i++)
        {
            double life = Frac(t * (.16 + Hash(i, seed, 1) * .08) + i / 5.0);
            double x = -18 + Hash(i, seed, 3) * 36 + Math.Sin(t * 1.8 + i * 1.9) * 5;
            double y = 26 - life * 56;
            double s = .8 + Hash(i, seed, 4) * .7 + Math.Sin(t * 5 + i) * .08;
            dc.PushTransform(new TranslateTransform(x, y));
            dc.PushTransform(new ScaleTransform(s, s));
            dc.DrawGeometry(FrozenBrush(WithAlpha(heart, SoftAlpha(225 * LifeFade(life), p))), null, HeartShape);
            dc.Pop(); dc.Pop();
        }
    }

    private static void DrawNotes(DrawingContext dc, double t, ScenePalette p, double seed, Color? customColor)
    {
        Color ink = BlendTheme(p, p.Accent, customColor, .45);
        for (int i = 0; i < 5; i++)
        {
            double life = Frac(t * (.14 + Hash(i, seed, 1) * .07) + i / 5.0);
            double x = -20 + Hash(i, seed, 3) * 40 + Math.Sin(t * 1.6 + i * 2.2) * 7;
            double y = 26 - life * 56;
            byte a = SoftAlpha(230 * LifeFade(life), p);
            var brush = FrozenBrush(WithAlpha(ink, a));
            var pen = FrozenPen(brush, 1.3);
            dc.PushTransform(new TranslateTransform(x, y));
            dc.PushTransform(new RotateTransform(Math.Sin(t * 2.4 + i) * 14 - 12));
            dc.DrawEllipse(brush, null, new Point(0, 0), 3.0, 2.2);
            dc.DrawLine(pen, new Point(2.6, -.4), new Point(2.6, -11));
            if (i % 2 == 0)
            {
                dc.DrawEllipse(brush, null, new Point(9, 1.5), 3.0, 2.2);
                dc.DrawLine(pen, new Point(11.6, 1.1), new Point(11.6, -9.5));
                dc.DrawLine(FrozenPen(brush, 2.2), new Point(2.6, -11), new Point(11.6, -9.5));
            }
            else
            {
                dc.DrawLine(pen, new Point(2.6, -11), new Point(6.5, -7.5));
            }
            dc.Pop(); dc.Pop();
        }
    }

    private static void DrawBird(DrawingContext dc, double t, ScenePalette p, Color? customColor)
    {
        Color ink = BlendTheme(p, p.IsLight ? Color.FromRgb(0x3A, 0x44, 0x55) : Color.FromRgb(0xD8, 0xE2, 0xEE), customColor, .7);
        var pen = FrozenPen(FrozenBrush(WithAlpha(ink, SoftAlpha(225, p))), 2.0);
        double glide = Math.Sin(t * .7) * 3;
        double flap = Math.Sin(t * 7.5);
        double wingY = -4 - flap * 6;
        dc.PushTransform(new TranslateTransform(Math.Sin(t * .5) * 6, glide));
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(new Point(-15, wingY), false, false);
            ctx.QuadraticBezierTo(new Point(-7, wingY - 3 + flap * 2), new Point(0, 1), true, true);
            ctx.QuadraticBezierTo(new Point(7, wingY - 3 + flap * 2), new Point(15, wingY), true, true);
        }
        g.Freeze();
        dc.DrawGeometry(null, pen, g);
        dc.Pop();
    }

    private static void DrawFish(DrawingContext dc, double t, ScenePalette p, Color? customColor)
    {
        Color body = BlendTheme(p, Color.FromRgb(0xFF, 0x9F, 0x43), customColor, .8);
        Color fin = EffectPalette.Blend(body, Color.FromRgb(0xFF, 0xD6, 0x8A), .4);
        double swimPhase = t * .45;
        double x = Math.Sin(swimPhase) * 20;
        double dir = Math.Cos(swimPhase) >= 0 ? 1 : -1;
        double wag = Math.Sin(t * 8) * 4;
        dc.PushTransform(new TranslateTransform(x, Math.Sin(t * 1.6) * 2));
        dc.PushTransform(new ScaleTransform(dir, 1));
        var tail = new StreamGeometry();
        using (var ctx = tail.Open())
        {
            ctx.BeginFigure(new Point(-8, 0), true, true);
            ctx.LineTo(new Point(-17, -6 + wag), true, true);
            ctx.LineTo(new Point(-17, 6 + wag), true, true);
        }
        tail.Freeze();
        dc.DrawGeometry(FrozenBrush(WithAlpha(fin, SoftAlpha(220, p))), null, tail);
        dc.DrawEllipse(FrozenBrush(WithAlpha(body, SoftAlpha(235, p))), null, new Point(0, 0), 11, 6.5);
        dc.DrawEllipse(FrozenBrush(WithAlpha(p.IsLight ? p.Lo : Color.FromRgb(0x14, 0x18, 0x22), SoftAlpha(230, p))), null, new Point(6, -1.5), 1.3, 1.3);
        for (int i = 0; i < 2; i++)
        {
            double life = Frac(t * .5 + i * .5);
            dc.DrawEllipse(null, FrozenPen(FrozenBrush(WithAlpha(fin, SoftAlpha(140 * LifeFade(life), p))), .7),
                new Point(14 + life * 3, -4 - life * 18), 1.4 + life * 1.2, 1.4 + life * 1.2);
        }
        dc.Pop(); dc.Pop();
    }

    private static void DrawFlower(DrawingContext dc, double t, ScenePalette p, Color? customColor)
    {
        Color petal = BlendTheme(p, Color.FromRgb(0xF4, 0x8F, 0xB1), customColor, .82);
        Color center = BlendTheme(p, Color.FromRgb(0xFF, 0xD1, 0x4D), null, .86);
        Color stem = BlendTheme(p, Color.FromRgb(0x4C, 0x9A, 0x62), null, .66);
        double sway = Math.Sin(t * 1.4) * 4;
        var stemPen = FrozenPen(FrozenBrush(WithAlpha(stem, SoftAlpha(225, p))), 2.2);
        dc.DrawLine(stemPen, new Point(0, 24), new Point(sway, 0));
        dc.DrawEllipse(FrozenBrush(WithAlpha(stem, SoftAlpha(200, p))), null, new Point(4 + sway * .4, 14), 4.5, 2.2);
        double breathe = 1 + Math.Sin(t * 2.2) * .06;
        dc.PushTransform(new TranslateTransform(sway, 0));
        dc.PushTransform(new RotateTransform(t * 12 % 360));
        dc.PushTransform(new ScaleTransform(breathe, breathe));
        var petalBrush = FrozenBrush(WithAlpha(petal, SoftAlpha(230, p)));
        for (int i = 0; i < 6; i++)
        {
            dc.PushTransform(new RotateTransform(i * 60));
            dc.DrawEllipse(petalBrush, null, new Point(0, -7.5), 4.2, 6.6);
            dc.Pop();
        }
        dc.DrawEllipse(FrozenBrush(WithAlpha(center, SoftAlpha(245, p))), null, new Point(0, 0), 4.6, 4.6);
        dc.Pop(); dc.Pop(); dc.Pop();
    }

    private static void DrawMushroom(DrawingContext dc, double t, ScenePalette p, Color? customColor)
    {
        Color cap = BlendTheme(p, Color.FromRgb(0xE2, 0x4B, 0x4B), customColor, .8);
        Color stalk = BlendTheme(p, Color.FromRgb(0xF3, 0xE6, 0xCF), null, .78);
        double glow = (Math.Sin(t * 1.8) + 1) * .5;
        dc.DrawEllipse(CreateGlowBrush(cap, SoftAlpha(30 + 40 * glow, p)), null, new Point(0, -6), 28, 22);
        dc.DrawRoundedRectangle(FrozenBrush(WithAlpha(stalk, SoftAlpha(240, p))), null, new Rect(-5, -2, 10, 16), 4, 4);
        double squish = 1 + Math.Sin(t * 2.4) * .03;
        dc.PushTransform(new ScaleTransform(squish, 2 - squish, 0, 0));
        dc.DrawGeometry(FrozenBrush(WithAlpha(cap, SoftAlpha(240, p))), null, MushroomCap);
        var dot = FrozenBrush(WithAlpha(Color.FromRgb(0xFF, 0xF7, 0xEC), SoftAlpha(230, p)));
        dc.DrawEllipse(dot, null, new Point(-7, -6), 2.4, 2.0);
        dc.DrawEllipse(dot, null, new Point(3, -9.5), 2.0, 1.7);
        dc.DrawEllipse(dot, null, new Point(9, -4), 1.7, 1.5);
        dc.Pop();
    }

    private static void DrawCandle(DrawingContext dc, double t, ScenePalette p, Color? customColor)
    {
        Color wax = BlendTheme(p, Color.FromRgb(0xF5, 0xEB, 0xDA), null, .78);
        Color flame = BlendTheme(p, Color.FromRgb(0xFF, 0xA8, 0x3A), customColor, .86);
        Color core = EffectPalette.Blend(flame, Color.FromRgb(0xFF, 0xF4, 0xC8), .6);
        double flick = Math.Sin(t * 11) * .5 + Math.Sin(t * 17.3) * .35;
        dc.DrawEllipse(CreateGlowBrush(flame, SoftAlpha(70 + flick * 18, p), SoftAlpha(22, p)), null, new Point(0, -12), 26, 26);
        dc.DrawRoundedRectangle(FrozenBrush(WithAlpha(wax, SoftAlpha(240, p))), null, new Rect(-6, -4, 12, 26), 2.5, 2.5);
        dc.DrawLine(FrozenPen(FrozenBrush(WithAlpha(p.Lo, SoftAlpha(200, p))), 1), new Point(0, -4), new Point(0, -7));
        dc.PushTransform(new TranslateTransform(0, -6));
        dc.PushTransform(new SkewTransform(flick * 8, 0));
        dc.PushTransform(new ScaleTransform(1, 1 + flick * .12));
        dc.DrawGeometry(FrozenBrush(WithAlpha(flame, SoftAlpha(235, p))), null, FlameShape);
        dc.PushTransform(new ScaleTransform(.5, .55, 0, 1));
        dc.DrawGeometry(FrozenBrush(WithAlpha(core, SoftAlpha(245, p))), null, FlameShape);
        dc.Pop(); dc.Pop(); dc.Pop(); dc.Pop();
    }

    private static void DrawLightning(DrawingContext dc, double t, ScenePalette p, double seed, Color? customColor)
    {
        Color bolt = BlendTheme(p, Color.FromRgb(0xB8, 0xD8, 0xFF), customColor, .86);
        double cycle = Frac(t * .35 + seed);
        double flash = cycle < .08 ? 1 - cycle / .08 : cycle is > .12 and < .17 ? .7 : 0;
        dc.DrawEllipse(CreateGlowBrush(bolt, SoftAlpha(26 + 110 * flash, p)), null, new Point(0, 0), 32, 32);
        dc.DrawGeometry(FrozenBrush(WithAlpha(bolt, SoftAlpha(60 + 190 * flash, p))),
            FrozenPen(FrozenBrush(WithAlpha(Colors.White, SoftAlpha(40 + 180 * flash, p))), .8), BoltShape);
    }

    private static void DrawComet(DrawingContext dc, double t, ScenePalette p, Color? customColor)
    {
        Color head = BlendTheme(p, Color.FromRgb(0xE8, 0xF2, 0xFF), customColor, p.IsLight ? .55 : .88);
        double life = Frac(t * .28);
        double fade = LifeFade(life);
        var pos = new Point(-30 + life * 60, -18 + life * 36);
        var tailEnd = new Point(pos.X - 20, pos.Y - 12);
        var tail = new LinearGradientBrush(WithAlpha(head, 0), WithAlpha(head, SoftAlpha(200 * fade, p)), new Point(0, 0), new Point(1, 1))
        {
            MappingMode = BrushMappingMode.Absolute,
            StartPoint = tailEnd,
            EndPoint = pos
        };
        tail.Freeze();
        dc.DrawLine(new Pen(tail, 2.6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, tailEnd, pos);
        dc.DrawEllipse(CreateGlowBrush(head, SoftAlpha(130 * fade, p)), null, pos, 7, 7);
        dc.DrawEllipse(FrozenBrush(WithAlpha(head, SoftAlpha(250 * fade, p))), null, pos, 1.9, 1.9);
    }

    private static void DrawCrystal(DrawingContext dc, double t, ScenePalette p, Color? customColor)
    {
        Color gem = BlendTheme(p, Color.FromRgb(0x9C, 0x7B, 0xF0), customColor, .78);
        Color light = EffectPalette.Blend(gem, Colors.White, .55);
        double pulse = (Math.Sin(t * 1.7) + 1) * .5;
        double bob = Math.Sin(t * 1.1) * 2.4;
        dc.DrawEllipse(CreateGlowBrush(gem, SoftAlpha(40 + 50 * pulse, p)), null, new Point(0, bob), 30, 30);
        dc.PushTransform(new TranslateTransform(0, bob));
        dc.PushTransform(new ScaleTransform(Math.Cos(t * .9) * .25 + .85, 1));
        var fill = new LinearGradientBrush(WithAlpha(light, SoftAlpha(235, p)), WithAlpha(gem, SoftAlpha(235, p)), 35);
        fill.Freeze();
        dc.DrawGeometry(fill, FrozenPen(FrozenBrush(WithAlpha(light, SoftAlpha(200, p))), .8), CrystalShape);
        var facet = FrozenPen(FrozenBrush(WithAlpha(light, SoftAlpha(120 + 80 * pulse, p))), .6);
        dc.DrawLine(facet, new Point(0, -22), new Point(0, 18));
        dc.DrawLine(facet, new Point(-11, -6), new Point(11, -6));
        dc.Pop(); dc.Pop();
        double glint = Frac(t * .4);
        if (glint < .2)
        {
            double s = Math.Sin(glint / .2 * Math.PI) * .9;
            dc.PushTransform(new TranslateTransform(5, -12 + bob));
            dc.PushTransform(new ScaleTransform(s, s));
            dc.DrawGeometry(FrozenBrush(WithAlpha(Colors.White, SoftAlpha(240, p))), null, StarGlyph);
            dc.Pop(); dc.Pop();
        }
    }

    private static void DrawJellyfish(DrawingContext dc, double t, ScenePalette p, Color? customColor)
    {
        Color jelly = BlendTheme(p, Color.FromRgb(0xC0, 0x8B, 0xF5), customColor, .78);
        double pulse = Math.Sin(t * 2.2);
        double y = Math.Sin(t * 1.1) * 5 - 6;
        dc.DrawEllipse(CreateGlowBrush(jelly, SoftAlpha(36, p)), null, new Point(0, y), 26, 26);
        dc.PushTransform(new TranslateTransform(0, y));
        var tentacle = FrozenPen(FrozenBrush(WithAlpha(jelly, SoftAlpha(170, p))), 1.1);
        for (int i = 0; i < 5; i++)
        {
            double x0 = -8 + i * 4;
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                ctx.BeginFigure(new Point(x0, 1), false, false);
                ctx.BezierTo(new Point(x0 + Math.Sin(t * 2.6 + i) * 4, 9),
                    new Point(x0 - Math.Sin(t * 2.6 + i + 1) * 4, 16),
                    new Point(x0 + Math.Sin(t * 2.6 + i + 2) * 3, 23 + pulse * 1.5), true, true);
            }
            g.Freeze();
            dc.DrawGeometry(null, tentacle, g);
        }
        dc.PushTransform(new ScaleTransform(1 + pulse * .07, 1 - pulse * .07, 0, 0));
        dc.DrawGeometry(FrozenBrush(WithAlpha(jelly, SoftAlpha(190, p))),
            FrozenPen(FrozenBrush(WithAlpha(EffectPalette.Blend(jelly, Colors.White, .5), SoftAlpha(200, p))), .8), JellyDome);
        dc.Pop();
        dc.Pop();
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using CydLan.Core;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace CydLan.Desktop;

/// <summary>
/// Procedural vector battle terrain that adapts to the stage's biome element, place name, stage level tier, and boss status.
/// Drawn inside <see cref="World"/> behind the pets so it scales cleanly at any window size.
/// </summary>
public partial class PetArenaView
{
    private (string Element, string Place, int StageNumber, bool Boss)? terrainSpec;

    private static Color Hex(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    private static Color Mix(Color a, Color b, double t) => Color.FromRgb(
        (byte)Math.Clamp(a.R + (b.R - a.R) * t, 0, 255),
        (byte)Math.Clamp(a.G + (b.G - a.G) * t, 0, 255),
        (byte)Math.Clamp(a.B + (b.B - a.B) * t, 0, 255));
    private static Color Alpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

    /// <summary>Paints sky/ground gradients and draws layered vector landmarks for the current world stage and enemy tier.</summary>
    private void PaintTerrain(string element, string place = "", int stageNumber = 1, bool boss = false)
    {
        terrainSpec = (element, place, stageNumber, boss);
        if (!Biomes.TryGetValue(element, out var b)) b = Biomes["leaf"];
        // Stage tier shifts atmosphere: 0 = Stage 1-4 (day), 1 = Stage 5-14 (dusk), 2 = Stage 15-29 (storm/twilight), 3 = Stage 30+ (mythic).
        int tier = stageNumber >= 30 ? 3 : stageNumber >= 15 ? 2 : stageNumber >= 5 ? 1 : 0;
        var skyTop = Hex(b.SkyTop);
        var skyBot = Hex(b.SkyBottom);
        var gTop = Hex(b.GroundTop);
        var gBot = Hex(b.GroundBottom);
        Color moodTint = element switch
        {
            "blaze" => Hex("#7C2D12"),
            "tide" => Hex("#0C4A6E"),
            "frost" => Hex("#1E3A8A"),
            "leaf" => Hex("#14532D"),
            "light" => Hex("#92400E"),
            "shadow" => Hex("#1E1B4B"),
            _ => Hex("#312E81")
        };
        double depth = tier * .16 + (boss ? .16 : 0);
        SkyTop.Color = Mix(skyTop, moodTint, Math.Min(.62, depth));
        SkyBottom.Color = Mix(skyBot, moodTint, Math.Min(.35, depth * .6));
        GroundTop.Color = Mix(gTop, moodTint, Math.Min(.32, depth * .5));
        GroundBottom.Color = Mix(gBot, moodTint, Math.Min(.42, depth * .6));
        RedrawTerrain();
    }

    private void RedrawTerrain()
    {
        if (TerrainLayer is null || terrainSpec is not var (element, place, stageNumber, boss)) return;
        TerrainLayer.Children.Clear();
        double w = StageW, h = StageH;
        if (w <= 10 || h <= 10) return;

        int tier = stageNumber >= 30 ? 3 : stageNumber >= 15 ? 2 : stageNumber >= 5 ? 1 : 0;
        double horizon = h * .68;
        int seed = (stageNumber * 997) ^ StringComparer.Ordinal.GetHashCode(place + ":" + element);
        var rng = new Random(seed);

        void Place(UIElement el, double x, double y) { Canvas.SetLeft(el, x); Canvas.SetTop(el, y); TerrainLayer.Children.Add(el); }

        // Horizon atmospheric glow (stronger on higher tiers and bosses)
        if (tier >= 1 || boss)
        {
            var glowColor = Hex(PetGame.ElementOf(element).Color);
            Place(new Rectangle
            {
                Width = w + 20, Height = h * .34,
                Fill = new LinearGradientBrush(Alpha(glowColor, 0), Alpha(glowColor, (byte)(boss ? 72 : 28 + tier * 14)), 90)
            }, -10, horizon - h * .34);
        }

        // High-tier sky ribbons (aurora / starfield for Stage 15+ or bosses)
        if (tier >= 2 || boss)
        {
            var starColor = element == "light" ? Hex("#FEF08A") : Colors.White;
            for (int i = 0; i < (boss ? 18 : 10 + tier * 3); i++)
            {
                double sz = 1.8 + rng.NextDouble() * 2.4;
                Place(new Ellipse { Width = sz, Height = sz, Fill = new SolidColorBrush(Alpha(starColor, 175)) },
                    rng.NextDouble() * w, rng.NextDouble() * horizon * .72);
            }
        }
        if (tier >= 3)
        {
            var ribbonColor = Hex(PetGame.ElementOf(element).Color);
            var aurora = new Path
            {
                Data = Geometry.Parse($"M -20,{horizon * .35:0} Q {w * .28:0},{horizon * .1:0} {w * .55:0},{horizon * .3:0} T {w + 20:0},{horizon * .2:0}"),
                Stroke = new SolidColorBrush(Alpha(ribbonColor, 85)), StrokeThickness = 18,
                Effect = new BlurEffect { Radius = 14 }
            };
            Place(aurora, 0, 0);
        }

        // Biome-specific layered terrain & landmarks
        switch (element)
        {
            case "blaze": DrawBlazeTerrain(w, h, horizon, tier, boss, place, rng, Place); break;
            case "tide": DrawTideTerrain(w, h, horizon, tier, boss, place, rng, Place); break;
            case "frost": DrawFrostTerrain(w, h, horizon, tier, boss, place, rng, Place); break;
            case "leaf": DrawLeafTerrain(w, h, horizon, tier, boss, place, rng, Place); break;
            case "light": DrawLightTerrain(w, h, horizon, tier, boss, place, rng, Place); break;
            case "shadow": DrawShadowTerrain(w, h, horizon, tier, boss, place, rng, Place); break;
            case "volt": DrawVoltTerrain(w, h, horizon, tier, boss, place, rng, Place); break;
            default: DrawLeafTerrain(w, h, horizon, tier, boss, place, rng, Place); break;
        }

        // Boss arena emblem on the ground under the boss pet
        if (boss)
        {
            var bossColor = Hex(PetGame.ElementOf(element).Color);
            var ring = new Ellipse
            {
                Width = 156, Height = 36,
                Stroke = new SolidColorBrush(Alpha(bossColor, 185)), StrokeThickness = 2.6,
                StrokeDashArray = [5, 3],
                Fill = new RadialGradientBrush(Alpha(bossColor, 65), Alpha(bossColor, 0)),
                Effect = new DropShadowEffect { Color = bossColor, BlurRadius = 14, ShadowDepth = 0, Opacity = .75 }
            };
            Place(ring, w * .75 - 78, horizon + 20);
        }
    }

    private static void DrawBlazeTerrain(double w, double h, double horizon, int tier, bool boss, string place, Random rng, Action<UIElement, double, double> placeEl)
    {
        // Distant volcanic mountains
        var far = new PointCollection { new(0, horizon) };
        int peaks = 4 + tier;
        for (int i = 0; i <= peaks; i++)
        {
            double x = w * i / peaks;
            double peakH = (i % 2 == 1 ? 58 : 26) + rng.Next(26) + tier * 8;
            far.Add(new Point(x, horizon - peakH));
        }
        far.Add(new Point(w, horizon));
        placeEl(new Polygon { Points = far, Fill = new SolidColorBrush(Alpha(Hex("#7C2D12"), 120)) }, 0, 0);

        // Volcano cone with glowing caldera
        double vx = w * (.42 + rng.NextDouble() * .16);
        double vh = 78 + tier * 14 + (boss ? 14 : 0);
        placeEl(new Polygon
        {
            Points = [new(vx - 74, horizon), new(vx - 14, horizon - vh), new(vx + 14, horizon - vh), new(vx + 74, horizon)],
            Fill = new LinearGradientBrush(Hex("#572419"), Hex("#8C3B24"), 90)
        }, 0, 0);
        placeEl(new Ellipse
        {
            Width = 30, Height = 10, Fill = new SolidColorBrush(Hex("#FB923C")),
            Effect = new DropShadowEffect { Color = Hex("#F97316"), BlurRadius = 16, ShadowDepth = 0 }
        }, vx - 15, horizon - vh - 4);

        // Glowing lava veins & charred rocks on the ground
        int veins = 2 + tier + (boss ? 1 : 0);
        for (int i = 0; i < veins; i++)
        {
            double y = horizon + 12 + i * ((h - horizon - 20) / Math.Max(1, veins));
            var vein = new Polyline
            {
                Points = [new(w * (.06 + i * .12), y), new(w * (.32 + i * .1), y + 5), new(w * (.58 + i * .08), y - 3), new(w * (.88 - i * .05), y + 4)],
                Stroke = new SolidColorBrush(Alpha(Hex("#F97316"), (byte)(120 + tier * 30))),
                StrokeThickness = 2.5 + tier * .5,
                Effect = new DropShadowEffect { Color = Hex("#FACC15"), BlurRadius = 8, ShadowDepth = 0 }
            };
            placeEl(vein, 0, 0);
        }
        for (int i = 0; i < 4 + tier; i++)
        {
            double rw = 16 + rng.Next(18), rh = 9 + rng.Next(10);
            placeEl(new Polygon
            {
                Points = [new(0, rh), new(rw * .3, 0), new(rw * .75, rh * .2), new(rw, rh)],
                Fill = new SolidColorBrush(Alpha(Hex("#431407"), 155))
            }, rng.NextDouble() * (w - rw), horizon - rh + 4);
        }
    }

    private static void DrawTideTerrain(double w, double h, double horizon, int tier, bool boss, string place, Random rng, Action<UIElement, double, double> placeEl)
    {
        // Clouds
        for (int i = 0; i < 3; i++)
        {
            double cx = w * (.12 + i * .32 + rng.NextDouble() * .08), cy = 18 + rng.Next(36);
            placeEl(new Ellipse { Width = 78 + rng.Next(30), Height = 22, Fill = new SolidColorBrush(Alpha(Colors.White, (byte)(boss ? 95 : 155))) }, cx, cy);
        }
        // Distant coastal headlands / island
        placeEl(new Polygon
        {
            Points = [new(0, horizon), new(w * .14, horizon - 38 - tier * 6), new(w * .32, horizon - 22), new(w * .44, horizon)],
            Fill = new SolidColorBrush(Alpha(Hex("#0284C7"), 90))
        }, 0, 0);
        placeEl(new Polygon
        {
            Points = [new(w * .62, horizon), new(w * .78, horizon - 46 - tier * 8), new(w * .94, horizon - 26), new(w, horizon)],
            Fill = new SolidColorBrush(Alpha(Hex("#0369A1"), 105))
        }, 0, 0);

        // Pier pilings on Sunken Pier or higher stages
        if (place.Contains("Pier", StringComparison.OrdinalIgnoreCase) || tier >= 2)
        {
            for (int i = 0; i < 4; i++)
                placeEl(new Rectangle { Width = 9, Height = 30 + (i % 2) * 8, RadiusX = 2, RadiusY = 2, Fill = new SolidColorBrush(Alpha(Hex("#78350F"), 165)) },
                    w * (.38 + i * .07), horizon - 24);
        }

        // Shimmering ocean wave bands across the water floor
        for (int i = 0; i < 4 + tier; i++)
        {
            double y = horizon + 10 + i * ((h - horizon - 18) / (4 + tier));
            double x = rng.NextDouble() * (w * .35);
            placeEl(new Rectangle
            {
                Width = 90 + rng.Next(120), Height = 3, RadiusX = 1.5, RadiusY = 1.5,
                Fill = new SolidColorBrush(Alpha(Colors.White, (byte)(85 + (i % 2) * 45)))
            }, x + (i % 2) * (w * .35), y);
        }
        // Coral formations along horizon
        Color[] corals = [Hex("#F472B6"), Hex("#FB7185"), Hex("#34D399")];
        for (int i = 0; i < 3 + tier; i++)
        {
            double x = w * (.08 + i * (.84 / (3 + tier)));
            placeEl(new Ellipse { Width = 14 + rng.Next(12), Height = 18 + rng.Next(14), Fill = new SolidColorBrush(Alpha(corals[i % corals.Length], 145)) },
                x, horizon - 10);
        }
    }

    private static void DrawFrostTerrain(double w, double h, double horizon, int tier, bool boss, string place, Random rng, Action<UIElement, double, double> placeEl)
    {
        // Jagged glacier peaks
        var peaks = new PointCollection { new(0, horizon) };
        int n = 5 + tier;
        for (int i = 0; i <= n; i++)
        {
            double x = w * i / n;
            double ph = (i % 2 == 1 ? 66 : 30) + rng.Next(26) + tier * 8;
            peaks.Add(new Point(x, horizon - ph));
        }
        peaks.Add(new Point(w, horizon));
        placeEl(new Polygon { Points = peaks, Fill = new LinearGradientBrush(Alpha(Colors.White, 210), Alpha(Hex("#93C5FD"), 155), 90) }, 0, 0);

        // Translucent crystal ice pillars
        int crystals = 3 + tier + (boss ? 2 : 0);
        for (int i = 0; i < crystals; i++)
        {
            double cx = w * (.08 + i * (.84 / crystals)) + rng.Next(-14, 14);
            double ch = 26 + rng.Next(24) + tier * 5, cw = 12 + rng.Next(8);
            placeEl(new Polygon
            {
                Points = [new(cw / 2, 0), new(cw, ch * .3), new(cw * .85, ch), new(cw * .15, ch), new(0, ch * .3)],
                Fill = new LinearGradientBrush(Alpha(Colors.White, 215), Alpha(Hex("#38BDF8"), 145), 90),
                Stroke = new SolidColorBrush(Alpha(Colors.White, 180)), StrokeThickness = 1
            }, cx, horizon - ch + 5);
        }

        // Ice sheen diagonal streaks on frozen ground
        for (int i = 0; i < 4; i++)
            placeEl(new Polygon
            {
                Points = [new(0, 0), new(46, 0), new(18, h - horizon), new(-28, h - horizon)],
                Fill = new SolidColorBrush(Alpha(Colors.White, 55))
            }, w * (.12 + i * .24), horizon);
    }

    private static void DrawLeafTerrain(double w, double h, double horizon, int tier, bool boss, string place, Random rng, Action<UIElement, double, double> placeEl)
    {
        // Rolling emerald hills
        var hill = new Path
        {
            Data = Geometry.Parse($"M 0,{horizon:0} Q {w * .25:0},{horizon - 52 - tier * 8:0} {w * .52:0},{horizon - 18:0} T {w:0},{horizon - 36 - tier * 6:0} L {w:0},{horizon:0} Z"),
            Fill = new SolidColorBrush(Alpha(Hex("#4ADE80"), 115))
        };
        placeEl(hill, 0, 0);

        // Layered trees & shrubs along the treeline
        int trees = 4 + tier;
        for (int i = 0; i < trees; i++)
        {
            double tx = w * (.07 + i * (.86 / trees)) + rng.Next(-12, 12);
            if (Math.Abs(tx - w * .5) < 42 && i % 2 == 0) tx += 52;
            double th = 34 + rng.Next(24) + tier * 5;
            placeEl(new Rectangle { Width = 8, Height = th * .55, RadiusX = 2, RadiusY = 2, Fill = new SolidColorBrush(Alpha(Hex("#78350F"), 175)) }, tx - 4, horizon - th * .5);
            placeEl(new Ellipse { Width = th * .95, Height = th * .78, Fill = new SolidColorBrush(Alpha(Hex(i % 2 == 0 ? "#16A34A" : "#22C55E"), 175)) }, tx - th * .47, horizon - th);
        }

        // Ancient stone ruin pillar on Vine Ruins or higher stages
        if (place.Contains("Ruin", StringComparison.OrdinalIgnoreCase) || tier >= 1)
        {
            double rx = w * .49;
            placeEl(new Rectangle { Width = 18, Height = 52 + tier * 8, RadiusX = 2, RadiusY = 2, Fill = new SolidColorBrush(Alpha(Hex("#94A3B8"), 165)) }, rx - 9, horizon - 50 - tier * 8);
            placeEl(new Rectangle { Width = 26, Height = 7, RadiusX = 2, RadiusY = 2, Fill = new SolidColorBrush(Alpha(Hex("#CBD5E1"), 185)) }, rx - 13, horizon - 54 - tier * 8);
        }

        // Wildflowers / grass tufts on ground
        Color[] petals = [Hex("#FACC15"), Hex("#F472B6"), Colors.White];
        for (int i = 0; i < 8; i++)
            placeEl(new Ellipse { Width = 5, Height = 5, Fill = new SolidColorBrush(Alpha(petals[i % petals.Length], 185)) },
                rng.NextDouble() * (w - 16) + 8, horizon + 10 + rng.NextDouble() * Math.Max(12, h - horizon - 20));
    }

    private static void DrawLightTerrain(double w, double h, double horizon, int tier, bool boss, string place, Random rng, Action<UIElement, double, double> placeEl)
    {
        // Radiant sun & diagonal shafts
        double sunX = w * .5, sunY = horizon * .34;
        placeEl(new Ellipse
        {
            Width = 64 + tier * 10, Height = 64 + tier * 10,
            Fill = new RadialGradientBrush(Colors.White, Alpha(Hex("#FDE047"), 140)),
            Effect = new DropShadowEffect { Color = Hex("#FACC15"), BlurRadius = 26, ShadowDepth = 0 }
        }, sunX - (32 + tier * 5), sunY - (32 + tier * 5));
        for (int i = -1; i <= 1; i++)
            placeEl(new Polygon
            {
                Points = [new(sunX + i * 24, 0), new(sunX + i * 24 + 34, 0), new(sunX + i * 110 + 48, horizon), new(sunX + i * 110 - 16, horizon)],
                Fill = new SolidColorBrush(Alpha(Hex("#FEF9C3"), 48))
            }, 0, 0);

        // Temple columns / crystal spires on horizon
        int pillars = 3 + tier;
        for (int i = 0; i < pillars; i++)
        {
            double px = w * (.12 + i * (.76 / Math.Max(1, pillars - 1)));
            double ph = 42 + (i % 2) * 20 + tier * 8;
            placeEl(new Rectangle { Width = 14, Height = ph, RadiusX = 2, RadiusY = 2, Fill = new LinearGradientBrush(Alpha(Colors.White, 200), Alpha(Hex("#FDE68A"), 165), 90) }, px - 7, horizon - ph);
            placeEl(new Polygon { Points = [new(7, 0), new(16, 12), new(-2, 12)], Fill = new SolidColorBrush(Alpha(Hex("#FACC15"), 175)) }, px - 7, horizon - ph - 12);
        }

        // Golden floor tiles / light motes
        for (int i = 0; i < 6 + tier * 2; i++)
            placeEl(new Ellipse { Width = 6, Height = 6, Fill = new SolidColorBrush(Alpha(Hex("#FEF08A"), 185)), Effect = new DropShadowEffect { Color = Hex("#FACC15"), BlurRadius = 8, ShadowDepth = 0 } },
                rng.NextDouble() * w, rng.NextDouble() * (horizon - 14));
    }

    private static void DrawShadowTerrain(double w, double h, double horizon, int tier, bool boss, string place, Random rng, Action<UIElement, double, double> placeEl)
    {
        // Glowing moon (blood-violet on high stages/bosses)
        var moonColor = boss || tier >= 2 ? Hex("#E879F9") : Hex("#E2E8F0");
        placeEl(new Ellipse
        {
            Width = 52 + tier * 6, Height = 52 + tier * 6,
            Fill = new SolidColorBrush(Alpha(moonColor, 215)),
            Effect = new DropShadowEffect { Color = Hex("#A855F7"), BlurRadius = 24, ShadowDepth = 0 }
        }, w * .48, 22);

        // Gothic crypt arches / spires along horizon
        int spires = 4 + tier;
        for (int i = 0; i < spires; i++)
        {
            double sx = w * (.09 + i * (.82 / Math.Max(1, spires - 1)));
            double sh = 46 + rng.Next(32) + tier * 8;
            placeEl(new Polygon
            {
                Points = [new(sx - 16, horizon), new(sx - 12, horizon - sh * .65), new(sx, horizon - sh), new(sx + 12, horizon - sh * .65), new(sx + 16, horizon)],
                Fill = new SolidColorBrush(Alpha(Hex("#1E1B4B"), 185))
            }, 0, 0);
        }

        // Low purple mist banks hugging the ground
        for (int i = 0; i < 5; i++)
            placeEl(new Ellipse
            {
                Width = 140 + rng.Next(90), Height = 26,
                Fill = new RadialGradientBrush(Alpha(Hex("#A855F7"), (byte)(70 + tier * 15)), Alpha(Hex("#A855F7"), 0))
            }, w * (.02 + i * .2), horizon - 10 + (i % 2) * 14);
    }

    private static void DrawVoltTerrain(double w, double h, double horizon, int tier, bool boss, string place, Random rng, Action<UIElement, double, double> placeEl)
    {
        // Storm clouds at top
        for (int i = 0; i < 4; i++)
            placeEl(new Ellipse
            {
                Width = 120 + rng.Next(50), Height = 32,
                Fill = new SolidColorBrush(Alpha(Hex("#6366F1"), (byte)(65 + tier * 18)))
            }, w * (i * .26 - .03), 6 + (i % 2) * 12);

        // Distant thunder pylons / neon towers
        int towers = 3 + tier;
        for (int i = 0; i < towers; i++)
        {
            double tx = w * (.12 + i * (.76 / Math.Max(1, towers - 1)));
            double th = 48 + (i % 2) * 22 + tier * 10;
            placeEl(new Polygon
            {
                Points = [new(tx - 12, horizon), new(tx - 4, horizon - th), new(tx + 4, horizon - th), new(tx + 12, horizon)],
                Fill = new SolidColorBrush(Alpha(Hex("#4338CA"), 145))
            }, 0, 0);
            placeEl(new Ellipse
            {
                Width = 12, Height = 12, Fill = new SolidColorBrush(Hex("#FDE047")),
                Effect = new DropShadowEffect { Color = Hex("#FACC15"), BlurRadius = 14, ShadowDepth = 0 }
            }, tx - 6, horizon - th - 6);
        }

        // Glowing circuit traces on the ground
        for (int i = 0; i < 3 + tier; i++)
        {
            double y = horizon + 14 + i * ((h - horizon - 22) / (3 + tier));
            placeEl(new Polyline
            {
                Points = [new(w * .08, y), new(w * .36, y), new(w * .42, y - 6), new(w * .72, y - 6), new(w * .82, y)],
                Stroke = new SolidColorBrush(Alpha(Hex("#FACC15"), (byte)(85 + tier * 25))), StrokeThickness = 2
            }, 0, 0);
        }
    }
}

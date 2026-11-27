using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

public sealed partial class ChromeTexture
{
    private enum LeafSpecies { Ovate, Cordate, Willow, Ginkgo, Maple }

    private static readonly Lazy<Geometry> UnitSparkGeom = new(() =>
    {
        var g = Geometry.Parse("M0,-5.5 Q0,0 5.5,0 Q0,0 0,5.5 Q0,0 -5.5,0 Q0,0 0,-5.5 Z");
        g.Freeze();
        return g;
    });

    private static readonly Lazy<Geometry> MonsteraGeom = new(() =>
    {
        var g = Geometry.Parse(
            "M0,18 C-14,17 -22,6 -20,-4 L-7,-1 L-18,-9 C-16,-15 -11,-19 -6,-21 L-2,-11 L-3,-23 C-1,-24 1,-24 3,-23 " +
            "L2,-11 L6,-21 C11,-19 16,-15 18,-9 L7,-1 L20,-4 C22,6 14,17 0,18 Z");
        g.Freeze();
        return g;
    });

    // ---------------- Botanical & Nature Textures ----------------

    private static DrawingGroup BuildBotanicalDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();
        var rng = new Random(2917);

        Color sage = BlendTheme(Color.FromRgb(0x4F, 0x94, 0x67), accent, .58);
        Color fern = BlendTheme(Color.FromRgb(0x73, 0xB3, 0x7A), accent, .55);
        Color olive = BlendTheme(Color.FromRgb(0x3D, 0x72, 0x56), isLight ? lo : accent, .60);

        // Soft organic moss & sunlight wash anchors in corners
        dc.DrawRectangle(CreateGlowBrush(.16, .86, .48, .42,
            (WithAlpha(sage, ScaleByte(isLight ? 24 : 30, intensity)), 0),
            (WithAlpha(sage, 0), 1)), null, new Rect(0, 0, width, height));
        dc.DrawRectangle(CreateGlowBrush(.88, .18, .44, .44,
            (WithAlpha(fern, ScaleByte(isLight ? 20 : 26, intensity)), 0),
            (WithAlpha(fern, 0), 1)), null, new Rect(0, 0, width, height));

        var shadowFill = FrozenBrush(WithAlpha(isLight ? olive : sage, ScaleByte(isLight ? 22 : 24, intensity)));
        var midFill = FrozenBrush(WithAlpha(sage, ScaleByte(isLight ? 36 : 40, intensity)));
        var brightFill = FrozenBrush(WithAlpha(fern, ScaleByte(isLight ? 42 : 44, intensity)));
        var stemPen = FrozenPen(WithAlpha(olive, ScaleByte(isLight ? 62 : 66, intensity)), compact ? 1.05 : 1.4);
        var veinPen = FrozenPen(WithAlpha(hi, ScaleByte(isLight ? 82 : 52, intensity)), compact ? .55 : .85);
        var outlinePen = FrozenPen(WithAlpha(isLight ? olive : hi, ScaleByte(isLight ? 42 : 36, intensity)), compact ? .65 : .92);
        var budFill = FrozenBrush(WithAlpha(EffectPalette.Blend(hi, fern, .45), ScaleByte(isLight ? 86 : 74, intensity)));

        // Background shadow fern silhouettes for atmospheric depth
        if (!compact)
        {
            DrawFernFrond(dc, FrozenPen(WithAlpha(olive, ScaleByte(22, intensity)), 1.0), shadowFill, null,
                new Point(width * .06, height * 1.02), height * .56, -Math.PI * .38, .42, 11, 1.05);
            DrawFernFrond(dc, FrozenPen(WithAlpha(olive, ScaleByte(22, intensity)), 1.0), shadowFill, null,
                new Point(width * .92, height * 1.02), height * .52, -Math.PI * .64, -.38, 10, .98);
        }

        // Sculptural split Monstera / tropical leaves
        DrawSplitMonstera(dc, midFill, outlinePen, veinPen,
            new Point(width * (compact ? .24 : .89), height * (compact ? .66 : .72)),
            compact ? .36 : 1.15, compact ? -28 : -34);
        if (!compact)
        {
            DrawSplitMonstera(dc, midFill, outlinePen, veinPen,
                new Point(width * .18, height * .82), .86, 22);
            DrawFernFrond(dc, stemPen, brightFill, outlinePen,
                new Point(width * 1.01, height * .28), height * .34, Math.PI * .78, .28, 8, .76);
        }

        // Graceful arching fern fronds
        DrawFernFrond(dc, stemPen, brightFill, outlinePen,
            new Point(width * (compact ? .74 : .03), height * (compact ? 1.05 : .96)),
            compact ? height * .92 : height * .46, compact ? -Math.PI * .66 : -Math.PI * .32,
            compact ? -.36 : .34, compact ? 7 : 10, compact ? .42 : .88);
        if (!compact)
        {
            DrawFernFrond(dc, stemPen, midFill, outlinePen,
                new Point(width * .97, height * .86), height * .42, -Math.PI * .72, -.30, 9, .80);
        }

        // Draping upper eucalyptus / ivy vine trails
        int vines = compact ? 1 : 3;
        for (int v = 0; v < vines; v++)
        {
            double vx = compact ? width * .16 : width * (v == 0 ? .12 : v == 1 ? .48 : .84);
            double vLen = compact ? height * .76 : height * (.26 + (v % 2) * .10);
            double bend = v == 1 ? .22 : (v == 0 ? .32 : -.30);
            DrawVineTrail(dc, stemPen, v % 2 == 0 ? midFill : brightFill, veinPen,
                new Point(vx, -2), vLen, Math.PI * .5 + (rng.NextDouble() - .5) * .25, bend,
                compact ? 4 : 7, compact ? .44 : .78);
        }

        // Foreground branching stems with veined ovate/cordate leaves and small flora buds
        int stems = compact ? 2 : 5;
        for (int s = 0; s < stems; s++)
        {
            double x0 = compact ? width * (s == 0 ? .48 : .86) : width * (.08 + s * .21 + (rng.NextDouble() - .5) * .06);
            double y0 = height + 4;
            double len = (compact ? height * .72 : height * (.24 + rng.NextDouble() * .20));
            double angle = -Math.PI * (.42 + (rng.NextDouble() - .5) * .24);
            double bend = (rng.NextDouble() - .5) * .48;

            var anchors = DrawCurvedStem(dc, stemPen, new Point(x0, y0), len, angle, bend, compact ? 7 : 10);
            for (int k = 1; k < anchors.Count; k += 2)
            {
                var (pt, tan) = anchors[k];
                double sc = (compact ? .44 : .86) * (1.12 - k * .08);
                foreach (int side in new[] { -1, 1 })
                {
                    var sp = (k + s + side) % 3 == 0 ? LeafSpecies.Cordate : LeafSpecies.Ovate;
                    var fill = (k + s) % 2 == 0 ? midFill : brightFill;
                    DrawOrganicLeaf(dc, fill, outlinePen, veinPen, pt, sc, (tan + side * .64) * 180 / Math.PI, sp);
                }
            }

            if (anchors.Count > 0)
            {
                var (tip, tan) = anchors[^1];
                DrawFlowerBud(dc, budFill, outlinePen, tip, tan, compact ? .55 : .95);
            }
        }

        return group;
    }

    private static DrawingGroup BuildLeavesDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();
        var rng = new Random(4391);

        Color leafGreen = BlendTheme(Color.FromRgb(0x46, 0x8F, 0x5B), isLight ? lo : accent, isLight ? .72 : .60);
        Color oliveLeaf = BlendTheme(Color.FromRgb(0x7B, 0xA3, 0x4E), accent, .64);
        Color warmAmber = BlendTheme(Color.FromRgb(0xC9, 0x82, 0x36), accent, .64);

        // Soft breeze glow along diagonal canopy drift
        dc.DrawRectangle(CreateGlowBrush(.24, .22, .52, .48,
            (WithAlpha(leafGreen, ScaleByte(isLight ? 20 : 26, intensity)), 0),
            (WithAlpha(leafGreen, 0), 1)), null, new Rect(0, 0, width, height));
        dc.DrawRectangle(CreateGlowBrush(.82, .76, .48, .48,
            (WithAlpha(warmAmber, ScaleByte(isLight ? 18 : 24, intensity)), 0),
            (WithAlpha(warmAmber, 0), 1)), null, new Rect(0, 0, width, height));

        var backFills = new[]
        {
            FrozenBrush(WithAlpha(leafGreen, ScaleByte(isLight ? 22 : 20, intensity))),
            FrozenBrush(WithAlpha(oliveLeaf, ScaleByte(isLight ? 20 : 18, intensity)))
        };
        var foreFills = new[]
        {
            FrozenBrush(WithAlpha(leafGreen, ScaleByte(isLight ? 46 : 42, intensity))),
            FrozenBrush(WithAlpha(oliveLeaf, ScaleByte(isLight ? 42 : 38, intensity))),
            FrozenBrush(WithAlpha(warmAmber, ScaleByte(isLight ? 38 : 34, intensity)))
        };
        var veinPen = FrozenPen(WithAlpha(hi, ScaleByte(isLight ? 92 : 58, intensity)), compact ? .6 : .88);
        var outlinePen = FrozenPen(WithAlpha(isLight ? lo : hi, ScaleByte(isLight ? 42 : 34, intensity)), compact ? .65 : .92);
        var speckFill = FrozenBrush(WithAlpha(isLight ? lo : hi, ScaleByte(isLight ? 58 : 48, intensity)));

        LeafSpecies[] species = [LeafSpecies.Maple, LeafSpecies.Ovate, LeafSpecies.Ginkgo, LeafSpecies.Willow, LeafSpecies.Cordate];

        // Layer 1: faint backlit shadow silhouettes
        int shadows = compact ? 5 : 16;
        for (int i = 0; i < shadows; i++)
        {
            double x = rng.NextDouble() * width;
            double y = rng.NextDouble() * height;
            double sc = (compact ? .55 : 1.05) + rng.NextDouble() * (compact ? .35 : .85);
            double ang = -55 + rng.NextDouble() * 140;
            DrawOrganicLeaf(dc, backFills[i % backFills.Length], null, null, new Point(x, y), sc, ang, species[(i + 1) % species.Length]);
        }

        // Layer 2: detailed crisp multi-species veined leaves floating across the wind path
        int leaves = compact ? 10 : 28;
        for (int i = 0; i < leaves; i++)
        {
            double t = (i + .5) / leaves;
            double x = compact ? width * (.08 + t * .84) : width * (t * .92 + (rng.NextDouble() - .5) * .16);
            double bandY = height * (.18 + .64 * Math.Sin(t * Math.PI * 1.6 + i * .5) * .5 + rng.NextDouble() * .65);
            double y = Math.Clamp(bandY, 4, height - 4);
            double sc = (compact ? .44 : .78) + rng.NextDouble() * (compact ? .28 : .58);
            double ang = -35 + (rng.NextDouble() - .5) * 130;
            var sp = species[i % species.Length];
            var fill = foreFills[i % foreFills.Length];
            DrawOrganicLeaf(dc, fill, outlinePen, veinPen, new Point(x, y), sc, ang, sp);
        }

        // Tiny pollen / botanical specks
        int specks = compact ? 6 : 20;
        for (int i = 0; i < specks; i++)
        {
            double r = compact ? .9 : 1.3;
            dc.DrawEllipse(speckFill, null, new Point(rng.NextDouble() * width, rng.NextDouble() * height), r, r);
        }

        return group;
    }

    private static DrawingGroup BuildLonelyGrassDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();
        var rng = new Random(6109);

        Color meadow = BlendTheme(Color.FromRgb(0x47, 0x88, 0x5D), isLight ? lo : accent, isLight ? .66 : .58);
        Color reed = BlendTheme(Color.FromRgb(0xBA, 0x9B, 0x58), isLight ? lo : accent, isLight ? .58 : .46);

        // Tranquil low horizon mist leaving plenty of quiet open sky
        var mist = CreateSweepBrush(new Point(0, .54), new Point(0, 1),
            (WithAlpha(meadow, 0), 0),
            (WithAlpha(meadow, ScaleByte(isLight ? 20 : 22, intensity)), .55),
            (WithAlpha(isLight ? lo : meadow, ScaleByte(isLight ? 28 : 34, intensity)), 1));
        dc.DrawRectangle(mist, null, new Rect(0, 0, width, height));

        // Soft lonely earth knoll rising gently on the right side
        var knollBrush = FrozenBrush(WithAlpha(meadow, ScaleByte(isLight ? 30 : 28, intensity)));
        var knollPen = FrozenPen(WithAlpha(meadow, ScaleByte(isLight ? 56 : 52, intensity)), compact ? .9 : 1.25);
        DrawSoftHillRidge(dc, knollBrush, knollPen, width, height,
            baseY: compact ? height * .76 : height * .84,
            ampA: compact ? height * .09 : height * .055, freqA: 1.0, phaseA: 4.1,
            ampB: compact ? height * .03 : height * .02, out double[] crestY);

        var bladeBack = FrozenBrush(WithAlpha(meadow, ScaleByte(isLight ? 38 : 38, intensity)));
        var bladeMain = FrozenBrush(WithAlpha(meadow, ScaleByte(isLight ? 60 : 60, intensity)));
        var bladeHi = FrozenPen(WithAlpha(isLight ? lo : hi, ScaleByte(isLight ? 42 : 44, intensity)), .7);
        var reedPen = FrozenPen(WithAlpha(reed, ScaleByte(isLight ? 72 : 70, intensity)), compact ? .85 : 1.2);
        var plumeFill = FrozenBrush(WithAlpha(reed, ScaleByte(isLight ? 68 : 68, intensity)));

        void DrawSolitaryTuft(double centerFrac, double scale, int bladeCount, int stalkCount)
        {
            double cx = width * centerFrac;
            int sampleIdx = Math.Clamp((int)Math.Round(centerFrac * (crestY.Length - 1)), 0, crestY.Length - 1);
            double rootY = Math.Min(height + 1, crestY[sampleIdx] + 2);

            for (int b = 0; b < bladeCount; b++)
            {
                double spread = (b - (bladeCount - 1) * .5) / Math.Max(1, bladeCount);
                double bx = cx + spread * (compact ? 14 : 42) * scale;
                double len = (compact ? height * .48 : height * (.20 + rng.NextDouble() * .24)) * scale;
                double lean = .22 + spread * .42 + (rng.NextDouble() - .5) * .14;
                double ang = -Math.PI * .5 + lean;
                double curve = .26 + rng.NextDouble() * .28;
                double baseW = (compact ? 1.2 : 2.5) * scale * (.75 + .4 * (1 - Math.Abs(spread)));
                bool front = b % 2 == 0;
                DrawTaperedBlade(dc, front ? bladeMain : bladeBack, front ? bladeHi : null,
                    new Point(bx, rootY), len, ang, curve, baseW);
            }

            // Tall windswept seed stalks bending delicately into the quiet air
            for (int s = 0; s < stalkCount; s++)
            {
                double sx = cx + (s - (stalkCount - 1) * .5) * (compact ? 5 : 14) * scale;
                double stalkLen = (compact ? height * .68 : height * (.32 + s * .08)) * scale;
                var anchors = DrawCurvedStem(dc, reedPen, new Point(sx, rootY), stalkLen,
                    -Math.PI * .5 + .18 + s * .10, .48 + s * .08, 8);
                if (anchors.Count >= 3)
                {
                    for (int k = anchors.Count - 3; k < anchors.Count; k++)
                    {
                        var (pt, tan) = anchors[k];
                        DrawSeedGrain(dc, plumeFill, pt, tan, (compact ? .55 : 1.0) * scale);
                    }
                }
            }
        }

        // Primary solitary windswept grass clump
        DrawSolitaryTuft(compact ? .62 : .74, scale: 1.0, bladeCount: compact ? 9 : 16, stalkCount: compact ? 2 : 3);

        // Distant secondary wisp on the sidebar side (smaller & sparser so openness stays intact)
        if (!compact)
        {
            DrawSolitaryTuft(.17, scale: .62, bladeCount: 7, stalkCount: 1);
        }

        // Few drifting wind seeds floating right into empty sky
        var seedPen = FrozenPen(WithAlpha(hi, ScaleByte(isLight ? 44 : 46, intensity)), .65);
        int seeds = compact ? 3 : 6;
        for (int i = 0; i < seeds; i++)
        {
            double sx = width * (compact ? .74 + i * .07 : .78 + rng.NextDouble() * .18);
            double sy = height * (compact ? .24 + i * .12 : .34 + rng.NextDouble() * .34);
            dc.DrawEllipse(plumeFill, null, new Point(sx, sy), compact ? 1.0 : 1.4, compact ? .6 : .9);
            dc.DrawLine(seedPen, new Point(sx, sy), new Point(sx - (compact ? 3.5 : 6), sy - (compact ? 2.0 : 3.5)));
        }

        return group;
    }

    private static DrawingGroup BuildGrassyFieldsDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();
        var rng = new Random(7213);

        Color lush = BlendTheme(Color.FromRgb(0x4C, 0x9C, 0x61), accent, .58);
        Color deepMeadow = BlendTheme(Color.FromRgb(0x2F, 0x6E, 0x45), isLight ? lo : accent, .62);
        Color sunlitBlade = BlendTheme(Color.FromRgb(0x88, 0xC9, 0x74), hi, .55);
        Color wildGold = BlendTheme(Color.FromRgb(0xF6, 0xD3, 0x65), hi, .60);

        // Open horizon atmosphere wash
        dc.DrawRectangle(CreateGlowBrush(.58, .54, .75, .48,
            (WithAlpha(sunlitBlade, ScaleByte(isLight ? 20 : 22, intensity)), 0),
            (WithAlpha(lush, 0), 1)), null, new Rect(0, 0, width, height));

        // Tier 1: far misty hill
        var farBrush = CreateSweepBrush(new Point(0, 0), new Point(0, 1),
            (WithAlpha(lush, ScaleByte(isLight ? 16 : 20, intensity)), 0),
            (WithAlpha(deepMeadow, ScaleByte(isLight ? 24 : 30, intensity)), 1));
        DrawSoftHillRidge(dc, farBrush, null, width, height,
            baseY: compact ? height * .52 : height * .68,
            ampA: compact ? height * .08 : height * .05, freqA: 1.3, phaseA: .6,
            ampB: compact ? height * .03 : height * .018, out double[] farY);

        // Tier 2: mid rolling meadow hill + scattered distant blades
        var midBrush = CreateSweepBrush(new Point(0, 0), new Point(0, 1),
            (WithAlpha(lush, ScaleByte(isLight ? 24 : 28, intensity)), 0),
            (WithAlpha(deepMeadow, ScaleByte(isLight ? 34 : 42, intensity)), 1));
        var midCrestPen = FrozenPen(WithAlpha(sunlitBlade, ScaleByte(isLight ? 36 : 42, intensity)), .9);
        DrawSoftHillRidge(dc, midBrush, midCrestPen, width, height,
            baseY: compact ? height * .65 : height * .73,
            ampA: compact ? height * .09 : height * .055, freqA: 1.6, phaseA: 2.3,
            ampB: compact ? height * .03 : height * .02, out double[] midY);

        var midBladeFill = FrozenBrush(WithAlpha(lush, ScaleByte(isLight ? 32 : 38, intensity)));
        int midBlades = compact ? 14 : 44;
        for (int i = 0; i < midBlades; i++)
        {
            double frac = (i + rng.NextDouble() * .7) / midBlades;
            double x = Math.Clamp(frac * width, 2, width - 2);
            int idx = Math.Clamp((int)(frac * (midY.Length - 1)), 0, midY.Length - 1);
            double rootY = midY[idx] + 2;
            double len = (compact ? height * .26 : height * (.08 + rng.NextDouble() * .09));
            double ang = -Math.PI * .5 + .20 + (rng.NextDouble() - .5) * .28;
            DrawTaperedBlade(dc, midBladeFill, null, new Point(x, rootY), len, ang, .22, compact ? .9 : 1.7);
        }

        // Tier 3: foreground lush grassy ridge
        var frontBrush = CreateSweepBrush(new Point(0, 0), new Point(0, 1),
            (WithAlpha(sunlitBlade, ScaleByte(isLight ? 32 : 34, intensity)), 0),
            (WithAlpha(deepMeadow, ScaleByte(isLight ? 46 : 56, intensity)), 1));
        var frontCrestPen = FrozenPen(WithAlpha(sunlitBlade, ScaleByte(isLight ? 52 : 56, intensity)), 1.15);
        DrawSoftHillRidge(dc, frontBrush, frontCrestPen, width, height,
            baseY: compact ? height * .79 : height * .87,
            ampA: compact ? height * .08 : height * .045, freqA: 1.1, phaseA: 4.4,
            ampB: compact ? height * .025 : height * .018, out double[] frontY);

        var bladeMain = FrozenBrush(WithAlpha(lush, ScaleByte(isLight ? 48 : 54, intensity)));
        var bladeSun = FrozenBrush(WithAlpha(sunlitBlade, ScaleByte(isLight ? 44 : 50, intensity)));
        var rimPen = FrozenPen(WithAlpha(hi, ScaleByte(isLight ? 54 : 34, intensity)), .6);

        int foreBlades = compact ? 24 : 78;
        for (int i = 0; i < foreBlades; i++)
        {
            double frac = (i + rng.NextDouble() * .8) / foreBlades;
            double x = Math.Clamp(frac * width, 1, width - 1);
            int idx = Math.Clamp((int)(frac * (frontY.Length - 1)), 0, frontY.Length - 1);
            double rootY = frontY[idx] + 3 + rng.NextDouble() * (compact ? 4 : 18);
            double len = (compact ? height * (.30 + rng.NextDouble() * .20) : height * (.09 + rng.NextDouble() * .13));
            double windSway = .24 + Math.Sin(frac * Math.Tau) * .12 + (rng.NextDouble() - .5) * .20;
            double ang = -Math.PI * .5 + windSway;
            double curve = .18 + rng.NextDouble() * .24;
            DrawTaperedBlade(dc, i % 3 == 0 ? bladeSun : bladeMain, i % 4 == 0 ? rimPen : null,
                new Point(x, rootY), len, ang, curve, compact ? 1.1 : 2.1);
        }

        // Tall feathery meadow plumes & tiny wildflowers scattered along the grass
        var stalkPen = FrozenPen(WithAlpha(wildGold, ScaleByte(isLight ? 46 : 52, intensity)), compact ? .75 : 1.0);
        var plumeFill = FrozenBrush(WithAlpha(wildGold, ScaleByte(isLight ? 56 : 64, intensity)));
        var flowerWhite = FrozenBrush(WithAlpha(hi, ScaleByte(isLight ? 92 : 76, intensity)));
        int blooms = compact ? 4 : 14;
        for (int b = 0; b < blooms; b++)
        {
            double frac = (.08 + b / (double)blooms * .84);
            double x = frac * width;
            int idx = Math.Clamp((int)(frac * (frontY.Length - 1)), 0, frontY.Length - 1);
            double rootY = frontY[idx] + 2;
            if (b % 2 == 0)
            {
                double len = compact ? height * .42 : height * (.16 + rng.NextDouble() * .10);
                var anchors = DrawCurvedStem(dc, stalkPen, new Point(x, rootY), len, -Math.PI * .5 + .22, .35, 6);
                if (anchors.Count > 2)
                {
                    DrawSeedGrain(dc, plumeFill, anchors[^1].P, anchors[^1].Tan, compact ? .55 : .95);
                    DrawSeedGrain(dc, plumeFill, anchors[^2].P, anchors[^2].Tan, compact ? .48 : .82);
                }
            }
            else
            {
                double fy = rootY - (compact ? 4 : 10 + rng.NextDouble() * 14);
                double r = compact ? 1.2 : 2.0;
                dc.DrawEllipse(flowerWhite, null, new Point(x, fy), r, r);
                dc.DrawEllipse(plumeFill, null, new Point(x, fy), r * .48, r * .48);
            }
        }

        return group;
    }

    // ---------------- Cosmic & Sky Textures ----------------

    private static DrawingGroup BuildGalaxyDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();
        var rng = new Random(9041);

        Color deepIndigo = BlendTheme(Color.FromRgb(0x3B, 0x26, 0x7A), accent, .64);
        Color nebulaViolet = BlendTheme(Color.FromRgb(0x9D, 0x4E, 0xDD), accent, .55);
        Color nebulaCyan = BlendTheme(Color.FromRgb(0x38, 0xBD, 0xF8), accent, .58);
        Color starWarm = BlendTheme(Color.FromRgb(0xFD, 0xE6, 0x8A), hi, .50);

        var center = new Point(width * (compact ? .52 : .64), height * (compact ? .48 : .45));
        double normCx = center.X / Math.Max(1, width);
        double normCy = center.Y / Math.Max(1, height);

        // Cosmic background nebula clouds
        dc.DrawRectangle(CreateGlowBrush(normCx, normCy, .64, .58,
            (WithAlpha(nebulaCyan, ScaleByte(isLight ? 24 : 38, intensity)), 0),
            (WithAlpha(nebulaViolet, ScaleByte(isLight ? 22 : 34, intensity)), .38),
            (WithAlpha(deepIndigo, ScaleByte(isLight ? 16 : 24, intensity)), .72),
            (WithAlpha(deepIndigo, 0), 1)), null, new Rect(0, 0, width, height));

        if (!compact)
        {
            // Companion left/sidebar nebula glow so UI chrome looks rich even without Show on chat
            dc.DrawRectangle(CreateGlowBrush(.18, .28, .36, .34,
                (WithAlpha(nebulaViolet, ScaleByte(isLight ? 16 : 26, intensity)), 0),
                (WithAlpha(deepIndigo, 0), 1)), null, new Rect(0, 0, width, height));
        }

        // Elliptical tilted spiral galaxy arms
        double rxMax = compact ? width * .46 : Math.Min(width * .42, height * .64);
        double tilt = -.42;
        double cosT = Math.Cos(tilt), sinT = Math.Sin(tilt);

        Point SpiralPoint(double theta, double armOffset, double scaleR, double jitter)
        {
            double u = theta / (Math.PI * 2.15);
            double r = rxMax * Math.Pow(u, 1.08) * scaleR;
            double a = theta + armOffset;
            double lx = Math.Cos(a) * r + (rng.NextDouble() - .5) * jitter;
            double ly = Math.Sin(a) * r * .48 + (rng.NextDouble() - .5) * jitter * .6;
            return new Point(center.X + lx * cosT - ly * sinT, center.Y + lx * sinT + ly * cosT);
        }

        // Soft glowing dust arm blobs (no hard wireframe spline)
        int armSteps = compact ? 16 : 30;
        for (int arm = 0; arm < 2; arm++)
        {
            double armOffset = arm * Math.PI;
            for (int s = 1; s <= armSteps; s++)
            {
                double frac = s / (double)armSteps;
                double theta = .25 + frac * Math.PI * 2.05;
                var pt = SpiralPoint(theta, armOffset, 1.0, compact ? 2.5 : 10.0);
                double nx = pt.X / Math.Max(1, width);
                double ny = pt.Y / Math.Max(1, height);
                double radN = (compact ? .16 : .14) * (1.22 - frac * .50);
                Color dustCol = (s + arm) % 2 == 0 ? nebulaCyan : nebulaViolet;
                byte a = ScaleByte((1.0 - frac * .52) * (isLight ? 22 : 34), intensity);
                dc.DrawRectangle(CreateGlowBrush(nx, ny, radN, radN * .85,
                    (WithAlpha(dustCol, a), 0),
                    (WithAlpha(dustCol, 0), 1)), null, new Rect(0, 0, width, height));
            }
        }

        // Micro-star dust tightly clustered along spiral arms
        var armDotLight = FrozenBrush(WithAlpha(isLight ? deepIndigo : starWarm, ScaleByte(isLight ? 68 : 112, intensity)));
        var armDotCyan = FrozenBrush(WithAlpha(nebulaCyan, ScaleByte(isLight ? 64 : 118, intensity)));
        int armDust = compact ? 55 : 210;
        for (int d = 0; d < armDust; d++)
        {
            double armOffset = (d % 2) * Math.PI + (d % 4 == 3 ? Math.PI * .35 : 0);
            double theta = .15 + Math.Pow(rng.NextDouble(), .85) * Math.PI * 2.15;
            double spread = compact ? 4.5 : 16.0;
            var pt = SpiralPoint(theta, armOffset, .68 + rng.NextDouble() * .42, spread);
            double r = (compact ? .45 : .65) + rng.NextDouble() * (compact ? .8 : 1.35);
            dc.DrawEllipse(d % 3 == 0 ? armDotCyan : armDotLight, null, pt, r, r);
        }

        // Bright galactic core bloom
        dc.DrawRectangle(CreateGlowBrush(normCx, normCy, compact ? .16 : .14, compact ? .14 : .12,
            (WithAlpha(isLight ? nebulaViolet : Colors.White, ScaleByte(isLight ? 52 : 98, intensity)), 0),
            (WithAlpha(starWarm, ScaleByte(isLight ? 28 : 54, intensity)), .42),
            (WithAlpha(nebulaCyan, 0), 1)), null, new Rect(0, 0, width, height));
        DrawFourPointStar(dc, FrozenBrush(WithAlpha(isLight ? deepIndigo : Colors.White, ScaleByte(isLight ? 86 : 145, intensity))),
            null, center, compact ? 1.05 : 1.9, 12);

        // Outer ambient star sparkles
        int stars = compact ? 14 : 48;
        var starBrush = FrozenBrush(WithAlpha(isLight ? deepIndigo : hi, ScaleByte(isLight ? 74 : 126, intensity)));
        for (int i = 0; i < stars; i++)
        {
            var sp = new Point(rng.NextDouble() * width, rng.NextDouble() * height);
            if (i % 4 == 0)
                DrawFourPointStar(dc, i % 8 == 0 ? armDotCyan : starBrush, null, sp, (compact ? .36 : .52) + rng.NextDouble() * .55, i * 17);
            else
            {
                double sr = (compact ? .5 : .7) + rng.NextDouble() * (compact ? .7 : 1.1);
                dc.DrawEllipse(starBrush, null, sp, sr, sr);
            }
        }

        return group;
    }

    private static DrawingGroup BuildStarryNightDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();
        var rng = new Random(1889);

        Color cobalt = BlendTheme(Color.FromRgb(0x1D, 0x4E, 0xD8), accent, .64);
        Color skyBlue = BlendTheme(Color.FromRgb(0x60, 0xA5, 0xFA), accent, .55);
        Color starGold = BlendTheme(Color.FromRgb(0xFB, 0xBF, 0x24), hi, .56);
        Color hillDeep = BlendTheme(Color.FromRgb(0x1E, 0x29, 0x3B), isLight ? lo : accent, .68);

        // Twilight sky glow
        dc.DrawRectangle(CreateGlowBrush(.50, .35, .72, .56,
            (WithAlpha(cobalt, ScaleByte(isLight ? 24 : 36, intensity)), 0),
            (WithAlpha(skyBlue, ScaleByte(isLight ? 14 : 20, intensity)), .55),
            (WithAlpha(cobalt, 0), 1)), null, new Rect(0, 0, width, height));

        // Impasto dashed swirl brushes
        var dashA = new DashStyle([3.2, 2.8], 0); dashA.Freeze();
        var dashB = new DashStyle([5.0, 3.4], 2.0); dashB.Freeze();
        var swirlBlue = new Pen(FrozenBrush(WithAlpha(skyBlue, ScaleByte(isLight ? 52 : 66, intensity))), compact ? 1.15 : 1.85)
        {
            StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, DashStyle = dashA
        };
        swirlBlue.Freeze();
        var swirlCobalt = new Pen(FrozenBrush(WithAlpha(cobalt, ScaleByte(isLight ? 44 : 58, intensity))), compact ? 1.4 : 2.3)
        {
            StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, DashStyle = dashB
        };
        swirlCobalt.Freeze();
        var swirlGold = new Pen(FrozenBrush(WithAlpha(starGold, ScaleByte(isLight ? 38 : 48, intensity))), compact ? .9 : 1.45)
        {
            StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, DashStyle = dashA
        };
        swirlGold.Freeze();

        // Van Gogh double-vortex starry wind swirls
        double mainR = compact ? 11 : Math.Min(width, height) * .17;
        void DrawWindVortex(Point center, double rMax, int rings, double spinDir)
        {
            for (int r = 1; r <= rings; r++)
            {
                var spiral = new StreamGeometry();
                using (var ctx = spiral.Open())
                {
                    int steps = 28;
                    for (int s = 0; s <= steps; s++)
                    {
                        double t = s / (double)steps;
                        double ang = t * Math.Tau * 1.45 * spinDir + r * .55;
                        double rad = rMax * (.18 + .82 * t) * (r / (double)rings * .55 + .45);
                        var p = new Point(center.X + Math.Cos(ang) * rad * 1.25, center.Y + Math.Sin(ang) * rad * .88);
                        if (s == 0) ctx.BeginFigure(p, isFilled: false, isClosed: false);
                        else ctx.LineTo(p, isStroked: true, isSmoothJoin: true);
                    }
                }
                spiral.Freeze();
                dc.DrawGeometry(null, r % 3 == 0 ? swirlGold : (r % 2 == 0 ? swirlBlue : swirlCobalt), spiral);
            }
        }

        DrawWindVortex(new Point(width * (compact ? .38 : .44), height * (compact ? .44 : .36)), mainR, compact ? 3 : 5, 1.0);
        DrawWindVortex(new Point(width * (compact ? .62 : .64), height * (compact ? .52 : .42)), mainR * .78, compact ? 2 : 4, -1.0);

        // Sweeping wind streamlines curving across the sky
        int streams = compact ? 3 : 6;
        for (int i = 0; i < streams; i++)
        {
            double baseY = height * (.16 + i * (compact ? .18 : .10));
            var stream = new StreamGeometry();
            using (var ctx = stream.Open())
            {
                const int segs = 28;
                for (int s = 0; s <= segs; s++)
                {
                    double t = s / (double)segs;
                    double y = baseY + Math.Sin(t * Math.Tau * 1.3 + i * .8) * (compact ? 4.5 : height * .055);
                    var pt = new Point(t * width, y);
                    if (s == 0) ctx.BeginFigure(pt, isFilled: false, isClosed: false);
                    else ctx.LineTo(pt, isStroked: true, isSmoothJoin: true);
                }
            }
            stream.Freeze();
            dc.DrawGeometry(null, i % 2 == 0 ? swirlBlue : swirlCobalt, stream);
        }

        // Luminous halo stars with painterly dashed rings
        Point[] starCenters = compact
            ? [new(width * .18, height * .28), new(width * .48, height * .24), new(width * .28, height * .64)]
            : [
                new(width * .12, height * .22), new(width * .28, height * .46),
                new(width * .42, height * .16), new(width * .56, height * .52),
                new(width * .68, height * .20), new(width * .90, height * .36),
                new(width * .48, height * .68), new(width * .78, height * .58)
            ];

        var coreGold = FrozenBrush(WithAlpha(starGold, ScaleByte(isLight ? 92 : 145, intensity)));
        var coreHi = FrozenBrush(WithAlpha(isLight ? starGold : Colors.White, ScaleByte(isLight ? 95 : 155, intensity)));
        for (int i = 0; i < starCenters.Length; i++)
        {
            var c = starCenters[i];
            double r = (compact ? 2.0 : 3.2) + (i % 3) * (compact ? .6 : 1.4);
            dc.DrawRectangle(CreateGlowBrush(c.X / Math.Max(1, width), c.Y / Math.Max(1, height),
                compact ? .10 : .08, compact ? .16 : .10,
                (WithAlpha(starGold, ScaleByte(isLight ? 44 : 62, intensity)), 0),
                (WithAlpha(starGold, 0), 1)), null, new Rect(0, 0, width, height));
            dc.DrawEllipse(null, swirlGold, c, r * 2.4, r * 2.4);
            dc.DrawEllipse(null, swirlBlue, c, r * 3.8, r * 3.8);
            dc.DrawEllipse(i % 2 == 0 ? coreHi : coreGold, null, c, r, r);
        }

        // Glowing warm crescent moon below the conversation header line
        var moonPos = new Point(width * (compact ? .82 : .86), height * (compact ? .32 : .31));
        double moonR = compact ? 5.2 : 16.0;
        dc.DrawRectangle(CreateGlowBrush(moonPos.X / Math.Max(1, width), moonPos.Y / Math.Max(1, height),
            compact ? .14 : .11, compact ? .22 : .13,
            (WithAlpha(starGold, ScaleByte(isLight ? 52 : 74, intensity)), 0),
            (WithAlpha(starGold, 0), 1)), null, new Rect(0, 0, width, height));
        var outerDisc = new EllipseGeometry(moonPos, moonR, moonR);
        var shadowDisc = new EllipseGeometry(new Point(moonPos.X - moonR * .44, moonPos.Y - moonR * .20), moonR * .86, moonR * .86);
        var crescent = new CombinedGeometry(GeometryCombineMode.Exclude, outerDisc, shadowDisc);
        crescent.Freeze();
        dc.DrawGeometry(coreGold, swirlGold, crescent);

        // Rolling night horizon ridge + tall cypress silhouette anchor
        var ridgeBrush = FrozenBrush(WithAlpha(hillDeep, ScaleByte(isLight ? 36 : 52, intensity)));
        DrawSoftHillRidge(dc, ridgeBrush, swirlCobalt, width, height,
            baseY: compact ? height * .82 : height * .89,
            ampA: compact ? height * .08 : height * .045, freqA: 1.8, phaseA: 1.2,
            ampB: compact ? height * .025 : height * .018, out _);

        if (!compact)
        {
            double cyX = width * .13;
            double cyBase = height;
            double cyH = height * .46;
            var cypress = new StreamGeometry();
            using (var ctx = cypress.Open())
            {
                ctx.BeginFigure(new Point(cyX - 18, cyBase), isFilled: true, isClosed: true);
                ctx.BezierTo(new Point(cyX - 24, cyBase - cyH * .45), new Point(cyX - 10, cyBase - cyH * .82), new Point(cyX + 3, cyBase - cyH), isStroked: false, isSmoothJoin: true);
                ctx.BezierTo(new Point(cyX + 14, cyBase - cyH * .76), new Point(cyX + 26, cyBase - cyH * .40), new Point(cyX + 20, cyBase), isStroked: false, isSmoothJoin: true);
            }
            cypress.Freeze();
            dc.DrawGeometry(ridgeBrush, null, cypress);
        }

        // Tiny scattered night specks
        var tinyDot = FrozenBrush(WithAlpha(hi, ScaleByte(isLight ? 50 : 88, intensity)));
        int specks = compact ? 8 : 28;
        for (int i = 0; i < specks; i++)
            dc.DrawEllipse(tinyDot, null, new Point(rng.NextDouble() * width, rng.NextDouble() * height * .82), .85, .85);

        return group;
    }

    private static DrawingGroup BuildSunDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();

        Color amber = BlendTheme(Color.FromRgb(0xF5, 0x9E, 0x0B), isLight ? lo : accent, isLight ? .78 : .66);
        Color warmGold = BlendTheme(Color.FromRgb(0xFB, 0xBF, 0x24), hi, .64);
        Color coralDusk = BlendTheme(Color.FromRgb(0xFB, 0x71, 0x85), accent, .52);

        var sunPos = new Point(width * (compact ? .68 : .82), height * (compact ? .42 : .32));
        double nx = sunPos.X / Math.Max(1, width);
        double ny = sunPos.Y / Math.Max(1, height);

        // Broad golden solar atmosphere bloom
        dc.DrawRectangle(CreateGlowBrush(nx, ny, compact ? .62 : .54, compact ? .72 : .58,
            (WithAlpha(warmGold, ScaleByte(isLight ? 62 : 76, intensity)), 0),
            (WithAlpha(amber, ScaleByte(isLight ? 36 : 44, intensity)), .36),
            (WithAlpha(coralDusk, ScaleByte(isLight ? 14 : 18, intensity)), .72),
            (WithAlpha(amber, 0), 1)), null, new Rect(0, 0, width, height));

        // Warm horizon shimmer wash
        dc.DrawRectangle(CreateSweepBrush(new Point(0, .70), new Point(0, 1),
            (WithAlpha(amber, 0), 0),
            (WithAlpha(amber, ScaleByte(isLight ? 16 : 20, intensity)), 1)), null, new Rect(0, 0, width, height));

        // Translucent volumetric sunbeam shafts fanning outward across canvas
        double rayLen = Math.Max(width, height) * 1.15;
        int rays = compact ? 7 : 12;
        for (int i = 0; i < rays; i++)
        {
            double baseAngle = Math.PI * .52 + (i - rays * .45) * .20;
            double spread = compact ? .06 : .045 + (i % 3) * .018;
            var tipA = new Point(sunPos.X + Math.Cos(baseAngle - spread) * rayLen, sunPos.Y + Math.Sin(baseAngle - spread) * rayLen);
            var tipB = new Point(sunPos.X + Math.Cos(baseAngle + spread) * rayLen, sunPos.Y + Math.Sin(baseAngle + spread) * rayLen);

            var rayGeom = new StreamGeometry();
            using (var ctx = rayGeom.Open())
            {
                ctx.BeginFigure(sunPos, isFilled: true, isClosed: true);
                ctx.LineTo(tipA, isStroked: false, isSmoothJoin: false);
                ctx.LineTo(tipB, isStroked: false, isSmoothJoin: false);
            }
            rayGeom.Freeze();

            byte rayAlpha = ScaleByte((i % 2 == 0 ? (isLight ? 30 : 32) : (isLight ? 18 : 20)), intensity);
            double gradR = Math.Max(width, height) * (compact ? 1.15 : .85);
            var rayBrush = new RadialGradientBrush
            {
                MappingMode = BrushMappingMode.Absolute,
                Center = sunPos,
                GradientOrigin = sunPos,
                RadiusX = gradR,
                RadiusY = gradR
            };
            rayBrush.GradientStops.Add(new GradientStop(WithAlpha(warmGold, rayAlpha), 0));
            rayBrush.GradientStops.Add(new GradientStop(WithAlpha(amber, (byte)(rayAlpha * .52)), .45));
            rayBrush.GradientStops.Add(new GradientStop(WithAlpha(amber, 0), 1));
            rayBrush.Freeze();
            dc.DrawGeometry(rayBrush, null, rayGeom);
        }

        // Solar disc, crisp coronal rings, and subtle diagonal bokeh flares
        double sunR = compact ? 7.5 : Math.Clamp(Math.Min(width, height) * .072, 24, 42);
        var coreFill = FrozenBrush(WithAlpha(isLight ? warmGold : Colors.White, ScaleByte(isLight ? 92 : 128, intensity)));
        var rimPen = FrozenPen(WithAlpha(amber, ScaleByte(isLight ? 68 : 88, intensity)), compact ? 1.0 : 1.5);
        var outerCorona = FrozenPen(WithAlpha(warmGold, ScaleByte(isLight ? 44 : 56, intensity)), compact ? .75 : 1.05);

        dc.DrawEllipse(coreFill, rimPen, sunPos, sunR, sunR);
        dc.DrawEllipse(null, outerCorona, sunPos, sunR * 1.48, sunR * 1.48);
        dc.DrawEllipse(null, FrozenPen(WithAlpha(amber, ScaleByte(isLight ? 26 : 34, intensity)), .85), sunPos, sunR * 2.15, sunR * 2.15);

        if (!compact)
        {
            // Delicate bokeh flare circles drifting along the sunbeam axis toward lower-left
            var flarePen = FrozenPen(WithAlpha(warmGold, ScaleByte(isLight ? 36 : 44, intensity)), .95);
            var flareFill = FrozenBrush(WithAlpha(amber, ScaleByte(isLight ? 12 : 16, intensity)));
            (double t, double r)[] flares = [(.24, 9), (.38, 16), (.56, 7), (.72, 22)];
            foreach (var (t, r) in flares)
            {
                var p = new Point(sunPos.X - width * .52 * t, sunPos.Y + height * .56 * t);
                dc.DrawEllipse(flareFill, flarePen, p, r, r);
            }
        }

        return group;
    }

    private static DrawingGroup BuildMoonDrawing(double width, double height, Color hi, Color lo, Color accent, bool isLight, double intensity, bool compact)
    {
        var group = new DrawingGroup();
        using var dc = group.Open();
        var rng = new Random(3793);

        Color moonPearl = BlendTheme(Color.FromRgb(0xF1, 0xF5, 0xF9), hi, .56);
        Color moonGlow = BlendTheme(Color.FromRgb(0x60, 0xA5, 0xFA), accent, .62);
        Color slateNight = BlendTheme(Color.FromRgb(0x33, 0x41, 0x55), lo, .62);

        var moonPos = new Point(width * (compact ? .66 : .82), height * (compact ? .44 : .33));
        double nx = moonPos.X / Math.Max(1, width);
        double ny = moonPos.Y / Math.Max(1, height);

        // Soft silvery moonlight aura
        dc.DrawRectangle(CreateGlowBrush(nx, ny, compact ? .44 : .40, compact ? .66 : .48,
            (WithAlpha(moonGlow, ScaleByte(isLight ? 32 : 52, intensity)), 0),
            (WithAlpha(moonPearl, ScaleByte(isLight ? 20 : 28, intensity)), .42),
            (WithAlpha(moonGlow, 0), 1)), null, new Rect(0, 0, width, height));

        double r = compact ? 9.5 : Math.Clamp(Math.Min(width, height) * .092, 32, 54);
        var fullDisc = new EllipseGeometry(moonPos, r, r);
        var shadowOffset = new Point(moonPos.X - r * .36, moonPos.Y - r * .22);
        var shadowDisc = new EllipseGeometry(shadowOffset, r * .88, r * .88);
        var crescent = new CombinedGeometry(GeometryCombineMode.Exclude, fullDisc, shadowDisc);
        crescent.Freeze();

        // Subtle soft earthshine body (no wireframe outer stroke) + textured craters
        var earthshineFill = FrozenBrush(WithAlpha(isLight ? slateNight : moonGlow, ScaleByte(isLight ? 14 : 18, intensity)));
        dc.DrawEllipse(earthshineFill, null, moonPos, r, r);

        var craterFill = FrozenBrush(WithAlpha(isLight ? slateNight : moonPearl, ScaleByte(isLight ? 16 : 18, intensity)));
        var craterRim = FrozenPen(WithAlpha(isLight ? lo : hi, ScaleByte(isLight ? 20 : 24, intensity)), .65);
        (double dx, double dy, double cr)[] craters = [(-.24, -.12, .16), (.02, .22, .12), (-.16, .28, .08)];
        foreach (var (dx, dy, cr) in craters)
            dc.DrawEllipse(craterFill, craterRim, new Point(moonPos.X + r * dx, moonPos.Y + r * dy), r * cr, r * cr);

        // Luminous razor-crisp lunar crescent
        var crescentBrush = FrozenBrush(WithAlpha(isLight ? moonGlow : moonPearl, ScaleByte(isLight ? 86 : 142, intensity)));
        var crescentRim = FrozenPen(WithAlpha(isLight ? slateNight : Colors.White, ScaleByte(isLight ? 56 : 98, intensity)), compact ? .85 : 1.25);
        dc.DrawGeometry(crescentBrush, crescentRim, crescent);

        // Soft elongated drifting moonlight cloud wisps passing horizontally
        int wisps = compact ? 2 : 5;
        for (int w = 0; w < wisps; w++)
        {
            double wx = compact ? (w == 0 ? .24 : .64) : (.16 + w * .17);
            double wy = compact ? (w == 0 ? .58 : .68) : (.29 + (w % 3) * .18);
            double rx = compact ? .30 : (.20 + (w % 2) * .12);
            double ry = compact ? .10 : (.042 + (w % 2) * .022);
            Color cloudCol = isLight ? slateNight : moonPearl;
            dc.DrawRectangle(CreateGlowBrush(wx, wy, rx, ry,
                (WithAlpha(cloudCol, ScaleByte(isLight ? 16 : 24, intensity)), 0),
                (WithAlpha(cloudCol, 0), 1)), null, new Rect(0, 0, width, height));
        }

        // Calm night stars
        var starBrush = FrozenBrush(WithAlpha(isLight ? slateNight : moonPearl, ScaleByte(isLight ? 68 : 118, intensity)));
        int stars = compact ? 8 : 34;
        for (int i = 0; i < stars; i++)
        {
            var sp = new Point(rng.NextDouble() * width, rng.NextDouble() * height);
            double dist = (sp - moonPos).Length;
            if (dist < r * 1.45) continue;
            if (i % 4 == 0)
                DrawFourPointStar(dc, starBrush, null, sp, (compact ? .36 : .55) + rng.NextDouble() * .42, i * 19);
            else
                dc.DrawEllipse(starBrush, null, sp, compact ? .75 : 1.1, compact ? .75 : 1.1);
        }

        return group;
    }

    // ---------------- Shared Procedural Foliage & Landscape Primitives ----------------

    private static List<(Point P, double Tan)> DrawCurvedStem(DrawingContext dc, Pen stemPen, Point start, double length, double angle, double bend, int segments)
    {
        var stem = new StreamGeometry();
        var anchors = new List<(Point P, double Tan)>(segments);
        using (var ctx = stem.Open())
        {
            double cx = start.X, cy = start.Y, a = angle;
            ctx.BeginFigure(start, isFilled: false, isClosed: false);
            for (int s = 1; s <= segments; s++)
            {
                a += bend / segments;
                cx += Math.Cos(a) * (length / segments);
                cy += Math.Sin(a) * (length / segments);
                var pt = new Point(cx, cy);
                ctx.LineTo(pt, isStroked: true, isSmoothJoin: true);
                anchors.Add((pt, a));
            }
        }
        stem.Freeze();
        dc.DrawGeometry(null, stemPen, stem);
        return anchors;
    }

    private static void DrawOrganicLeaf(DrawingContext dc, Brush fill, Pen? outlinePen, Pen? veinPen, Point origin, double scale, double angleDeg, LeafSpecies species)
    {
        var m = Matrix.Identity;
        m.Scale(scale, scale);
        m.Rotate(angleDeg);
        m.Translate(origin.X, origin.Y);
        dc.PushTransform(new MatrixTransform(m));

        var blade = new StreamGeometry();
        using (var ctx = blade.Open())
        {
            switch (species)
            {
                case LeafSpecies.Cordate:
                    ctx.BeginFigure(new Point(0, 0), isFilled: true, isClosed: true);
                    ctx.BezierTo(new Point(-3, -8), new Point(7, -12), new Point(18, -3), isStroked: true, isSmoothJoin: true);
                    ctx.BezierTo(new Point(22, -1), new Point(23, 0), new Point(24, 0), isStroked: true, isSmoothJoin: true);
                    ctx.BezierTo(new Point(23, 0), new Point(20, 4), new Point(16, 7), isStroked: true, isSmoothJoin: true);
                    ctx.BezierTo(new Point(6, 12), new Point(-2, 7), new Point(0, 0), isStroked: true, isSmoothJoin: true);
                    break;
                case LeafSpecies.Willow:
                    ctx.BeginFigure(new Point(0, 0), isFilled: true, isClosed: true);
                    ctx.BezierTo(new Point(6, -3.5), new Point(18, -3.2), new Point(28, -0.5), isStroked: true, isSmoothJoin: true);
                    ctx.BezierTo(new Point(18, 3.4), new Point(6, 3.2), new Point(0, 0), isStroked: true, isSmoothJoin: true);
                    break;
                case LeafSpecies.Ginkgo:
                    ctx.BeginFigure(new Point(0, 0), isFilled: true, isClosed: true);
                    ctx.BezierTo(new Point(4, -5), new Point(10, -11), new Point(15, -9), isStroked: true, isSmoothJoin: true);
                    ctx.BezierTo(new Point(17, -6), new Point(15, -2), new Point(14, 0), isStroked: true, isSmoothJoin: true);
                    ctx.BezierTo(new Point(15, 2), new Point(17, 6), new Point(15, 9), isStroked: true, isSmoothJoin: true);
                    ctx.BezierTo(new Point(10, 11), new Point(4, 5), new Point(0, 0), isStroked: true, isSmoothJoin: true);
                    break;
                case LeafSpecies.Maple:
                    ctx.BeginFigure(new Point(0, 0), isFilled: true, isClosed: true);
                    ctx.LineTo(new Point(4, -3), isStroked: true, isSmoothJoin: true);
                    ctx.LineTo(new Point(5, -10), isStroked: true, isSmoothJoin: true);
                    ctx.LineTo(new Point(9, -6), isStroked: true, isSmoothJoin: true);
                    ctx.LineTo(new Point(14, -12), isStroked: true, isSmoothJoin: true);
                    ctx.LineTo(new Point(14, -4), isStroked: true, isSmoothJoin: true);
                    ctx.LineTo(new Point(22, 0), isStroked: true, isSmoothJoin: true);
                    ctx.LineTo(new Point(14, 4), isStroked: true, isSmoothJoin: true);
                    ctx.LineTo(new Point(14, 12), isStroked: true, isSmoothJoin: true);
                    ctx.LineTo(new Point(9, 6), isStroked: true, isSmoothJoin: true);
                    ctx.LineTo(new Point(5, 10), isStroked: true, isSmoothJoin: true);
                    ctx.LineTo(new Point(4, 3), isStroked: true, isSmoothJoin: true);
                    break;
                default: // Ovate with gentle organic asymmetry
                    ctx.BeginFigure(new Point(0, 0), isFilled: true, isClosed: true);
                    ctx.BezierTo(new Point(5, -7.2), new Point(14, -6.4), new Point(21, -0.4), isStroked: true, isSmoothJoin: true);
                    ctx.BezierTo(new Point(14, 6.2), new Point(5, 6.5), new Point(0, 0), isStroked: true, isSmoothJoin: true);
                    break;
            }
        }
        blade.Freeze();
        dc.DrawGeometry(fill, outlinePen, blade);

        if (veinPen is not null)
        {
            double midLen = species switch
            {
                LeafSpecies.Willow => 25,
                LeafSpecies.Ginkgo => 12,
                LeafSpecies.Maple => 19,
                LeafSpecies.Cordate => 20,
                _ => 18
            };
            dc.DrawLine(veinPen, new Point(-2.5, 0), new Point(midLen, -.2));
            if (species == LeafSpecies.Maple)
            {
                dc.DrawLine(veinPen, new Point(5, 0), new Point(12.5, -9.5));
                dc.DrawLine(veinPen, new Point(5, 0), new Point(12.5, 9.5));
            }
            else if (species == LeafSpecies.Ginkgo)
            {
                dc.DrawLine(veinPen, new Point(2, 0), new Point(12, -5.5));
                dc.DrawLine(veinPen, new Point(2, 0), new Point(12, 5.5));
            }
            else if (species != LeafSpecies.Willow)
            {
                dc.DrawLine(veinPen, new Point(5, 0), new Point(10, -3.5));
                dc.DrawLine(veinPen, new Point(6, 0), new Point(11, 3.2));
                dc.DrawLine(veinPen, new Point(10.5, 0), new Point(14.5, -2.5));
            }
        }
        dc.Pop();
    }

    private static void DrawSplitMonstera(DrawingContext dc, Brush fill, Pen? outlinePen, Pen? veinPen, Point center, double scale, double angleDeg)
    {
        var m = Matrix.Identity;
        m.Scale(scale, scale);
        m.Rotate(angleDeg);
        m.Translate(center.X, center.Y);
        dc.PushTransform(new MatrixTransform(m));

        // Sculptural tropical split-leaf silhouette with recessed fenestration bays
        dc.DrawGeometry(fill, outlinePen, MonsteraGeom.Value);

        if (veinPen is not null)
        {
            dc.DrawLine(veinPen, new Point(0, 23), new Point(0, -18));
            dc.DrawLine(veinPen, new Point(0, 8), new Point(-14, 1));
            dc.DrawLine(veinPen, new Point(0, 8), new Point(14, 1));
            dc.DrawLine(veinPen, new Point(0, -1), new Point(-12, -11));
            dc.DrawLine(veinPen, new Point(0, -1), new Point(12, -11));
        }
        dc.Pop();
    }

    private static void DrawFernFrond(DrawingContext dc, Pen stemPen, Brush pinnaFill, Pen? pinnaPen, Point origin, double length, double angle, double bend, int pinnaPairs, double baseScale)
    {
        var anchors = DrawCurvedStem(dc, stemPen, origin, length, angle, bend, pinnaPairs + 2);
        for (int i = 1; i < anchors.Count; i++)
        {
            var (pt, tan) = anchors[i];
            double frac = i / (double)anchors.Count;
            double envelope = Math.Sin(Math.Pow(frac, .72) * Math.PI * .92) * baseScale;
            double sc = Math.Max(.18, envelope * .62);
            foreach (int side in new[] { -1, 1 })
                DrawOrganicLeaf(dc, pinnaFill, pinnaPen, null, pt, sc, (tan + side * .95) * 180 / Math.PI, LeafSpecies.Willow);
        }
    }

    private static void DrawVineTrail(DrawingContext dc, Pen stemPen, Brush fill, Pen? veinPen, Point origin, double length, double angle, double bend, int pairs, double baseScale)
    {
        var anchors = DrawCurvedStem(dc, stemPen, origin, length, angle, bend, pairs + 1);
        for (int i = 0; i < anchors.Count; i++)
        {
            var (pt, tan) = anchors[i];
            int side = i % 2 == 0 ? 1 : -1;
            double leafAngle = (tan + side * .72) * 180 / Math.PI;
            double sc = baseScale * (.72 - i * .04);
            DrawOrganicLeaf(dc, fill, null, veinPen, pt, Math.Max(.25, sc), leafAngle, LeafSpecies.Cordate);
        }
    }

    private static void DrawFlowerBud(DrawingContext dc, Brush fill, Pen? pen, Point origin, double angleRad, double scale)
    {
        var m = Matrix.Identity;
        m.Scale(scale, scale);
        m.Rotate(angleRad * 180 / Math.PI);
        m.Translate(origin.X, origin.Y);
        dc.PushTransform(new MatrixTransform(m));
        dc.DrawEllipse(fill, pen, new Point(2.5, -1.6), 2.4, 1.4);
        dc.DrawEllipse(fill, pen, new Point(2.5, 1.6), 2.4, 1.4);
        dc.DrawEllipse(fill, pen, new Point(4.2, 0), 2.6, 1.5);
        dc.Pop();
    }

    private static void DrawTaperedBlade(DrawingContext dc, Brush? fill, Pen? edgePen, Point origin, double length, double angleRad, double curveRad, double baseHalfW)
    {
        double nx = -Math.Sin(angleRad), ny = Math.Cos(angleRad);
        double tipAngle = angleRad + curveRad;
        var tip = new Point(
            origin.X + Math.Cos((angleRad + tipAngle) * .5) * length,
            origin.Y + Math.Sin((angleRad + tipAngle) * .5) * length);
        var midCtrl = new Point(
            origin.X + Math.Cos(angleRad) * length * .54,
            origin.Y + Math.Sin(angleRad) * length * .54);

        var pL = new Point(origin.X + nx * baseHalfW, origin.Y + ny * baseHalfW);
        var pR = new Point(origin.X - nx * baseHalfW, origin.Y - ny * baseHalfW);

        var blade = new StreamGeometry();
        using (var ctx = blade.Open())
        {
            ctx.BeginFigure(pL, isFilled: true, isClosed: true);
            ctx.QuadraticBezierTo(new Point(midCtrl.X + nx * baseHalfW * .42, midCtrl.Y + ny * baseHalfW * .42), tip, isStroked: true, isSmoothJoin: true);
            ctx.QuadraticBezierTo(new Point(midCtrl.X - nx * baseHalfW * .25, midCtrl.Y - ny * baseHalfW * .25), pR, isStroked: false, isSmoothJoin: true);
        }
        blade.Freeze();
        dc.DrawGeometry(fill, null, blade);

        if (edgePen is not null)
        {
            var highlight = new StreamGeometry();
            using (var ctx = highlight.Open())
            {
                ctx.BeginFigure(pL, isFilled: false, isClosed: false);
                ctx.QuadraticBezierTo(new Point(midCtrl.X + nx * baseHalfW * .42, midCtrl.Y + ny * baseHalfW * .42), tip, isStroked: true, isSmoothJoin: true);
            }
            highlight.Freeze();
            dc.DrawGeometry(null, edgePen, highlight);
        }
    }

    private static void DrawSeedGrain(DrawingContext dc, Brush fill, Point pt, double angleRad, double scale)
    {
        var m = Matrix.Identity;
        m.Scale(scale, scale);
        m.Rotate(angleRad * 180 / Math.PI + 18);
        m.Translate(pt.X, pt.Y);
        dc.PushTransform(new MatrixTransform(m));
        dc.DrawEllipse(fill, null, new Point(2.2, 0), 2.4, 1.1);
        dc.Pop();
    }

    private static void DrawSoftHillRidge(
        DrawingContext dc, Brush fill, Pen? crestPen,
        double width, double height, double baseY,
        double ampA, double freqA, double phaseA, double ampB,
        out double[] samples)
    {
        const int steps = 40;
        samples = new double[steps + 1];
        var area = new StreamGeometry();
        var crest = new StreamGeometry();

        using (var ctx = area.Open())
        using (var cCtx = crest.Open())
        {
            for (int s = 0; s <= steps; s++)
            {
                double t = s / (double)steps;
                double x = t * width;
                double y = baseY
                    + Math.Sin(t * Math.Tau * freqA + phaseA) * ampA
                    + Math.Cos(t * Math.Tau * (freqA * 2.1) - phaseA * .6) * ampB;
                y = Math.Clamp(y, 2, height + 2);
                samples[s] = y;
                var pt = new Point(x, y);
                if (s == 0)
                {
                    ctx.BeginFigure(pt, isFilled: true, isClosed: true);
                    cCtx.BeginFigure(pt, isFilled: false, isClosed: false);
                }
                else
                {
                    ctx.LineTo(pt, isStroked: false, isSmoothJoin: true);
                    cCtx.LineTo(pt, isStroked: true, isSmoothJoin: true);
                }
            }
            ctx.LineTo(new Point(width, height + 2), isStroked: false, isSmoothJoin: false);
            ctx.LineTo(new Point(0, height + 2), isStroked: false, isSmoothJoin: false);
        }
        area.Freeze();
        crest.Freeze();
        dc.DrawGeometry(fill, null, area);
        if (crestPen is not null) dc.DrawGeometry(null, crestPen, crest);
    }

    private static void DrawFourPointStar(DrawingContext dc, Brush fill, Pen? pen, Point center, double scale, double rotDeg)
    {
        var m = Matrix.Identity;
        m.Scale(scale, scale);
        m.Rotate(rotDeg);
        m.Translate(center.X, center.Y);
        dc.PushTransform(new MatrixTransform(m));
        dc.DrawGeometry(fill, pen, UnitSparkGeom.Value);
        dc.Pop();
    }

    private static Brush CreateGlowBrush(double cxNorm, double cyNorm, double rx, double ry, params (Color Color, double Offset)[] stops)
    {
        var brush = new RadialGradientBrush
        {
            Center = new Point(cxNorm, cyNorm),
            GradientOrigin = new Point(cxNorm, cyNorm),
            RadiusX = rx,
            RadiusY = ry
        };
        foreach (var (color, offset) in stops)
            brush.GradientStops.Add(new GradientStop(color, offset));
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

    private static Color BlendTheme(Color natural, Color theme, double naturalWeight) =>
        EffectPalette.Blend(theme, natural, naturalWeight);

    private static byte ScaleByte(double factor, double intensity) =>
        (byte)Math.Clamp(Math.Round(factor * intensity), 0, 255);
}

using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

/// <summary>Extra aura styles, including anime-inspired ones, plus the option to let auras spill past their element.</summary>
public sealed partial class TargetAura
{
    private static readonly Thickness ButtonInset = new(6, 9, 6, 4);
    private static readonly Thickness ButtonBleed = new(18, 24, 18, 14);
    private static readonly Thickness RowBleed = new(14, 16, 14, 12);
    private static readonly Dictionary<(Color, double), Pen[]> PenCache = [];
    private static readonly Dictionary<Color, Brush[]> FillCache = [];

    private static readonly Geometry HeartShape = Frozen("M0,3 C-4,0 -5,-3 -3,-4.5 C-1.5,-5.5 0,-4 0,-3 C0,-4 1.5,-5.5 3,-4.5 C5,-3 4,0 0,3 Z");
    private static readonly Geometry NoteShape = Frozen("M1.6,-5 V2.2 A1.9,1.5 0 1 1 0.4,1 V-5 Z M1.6,-5 C3.5,-4.2 4.4,-3.2 3.8,-1.4 C3.6,-2.6 2.8,-3.2 1.6,-3.2 Z");
    private static readonly Geometry LeafShape = Frozen("M-4,0 C-2,-2.6 2,-2.6 4,0 C2,2.6 -2,2.6 -4,0 Z");
    private static readonly Geometry WingShape = Frozen("M0,0 C-1,-5 -6,-6 -6,-2 C-6,0 -3,1 0,0 Z M0,0 C-1,2 -4,4.5 -4.5,2.5 C-4.8,1 -2,0.5 0,0 Z");
    private static readonly Geometry SparkShape = Frozen("M0,-4 Q0,0 4,0 Q0,0 0,4 Q0,0 -4,0 Q0,0 0,-4 Z");

    /// <summary>When on, auras draw past their element's edges instead of being cut off at them.</summary>
    public static bool Overflow { get; private set; }

    /// <summary>Set on conversation-row auras so row-shaped effects lay out across the full row.</summary>
    public bool IsRow { get; set; }

    private static Geometry Frozen(string data)
    {
        var g = Geometry.Parse(data);
        g.Freeze();
        return g;
    }

    /// <summary>Sizes and unclips an aura so its effect has room to spill out when overflow is on.</summary>
    internal static void ApplyBleed(TargetAura aura, bool row)
    {
        var inset = row ? (Overflow ? RowBleed : new Thickness(0)) : Overflow ? ButtonBleed : ButtonInset;
        aura.IsRow = row;
        aura.EdgeInset = inset;
        aura.Margin = new Thickness(-inset.Left, -inset.Top, -inset.Right, -inset.Bottom);
        aura.ClipToBounds = !Overflow;
        if (row)
        {
            // The row border clips its content; lift that while overflow is on.
            for (DependencyObject? node = VisualTreeHelper.GetParent(aura); node is not null and not System.Windows.Controls.ListBoxItem; node = VisualTreeHelper.GetParent(node))
                if (node is FrameworkElement { Name: "Row" } rowBorder) { rowBorder.ClipToBounds = !Overflow; break; }
        }
    }

    private static Pen[] PenSet(Color color, double thickness)
    {
        if (PenCache.TryGetValue((color, thickness), out var pens)) return pens;
        if (PenCache.Count > 64) PenCache.Clear();
        return PenCache[(color, thickness)] = BuildPens(color, thickness);
    }

    private static Brush[] FillSet(Color color)
    {
        if (FillCache.TryGetValue(color, out var fills)) return fills;
        if (FillCache.Count > 64) FillCache.Clear();
        return FillCache[color] = BuildSolidBrushes(color);
    }

    private static int O(double alpha) => EffectPalette.OpacityIndex(alpha);

    private static Color Hue(double h)
    {
        h = ((h % 1) + 1) % 1 * 6;
        int i = (int)h;
        double f = h - i;
        byte v = 255, p = 70, q = (byte)(255 - 185 * f), t = (byte)(70 + 185 * f);
        return i switch
        {
            0 => Color.FromRgb(v, t, p),
            1 => Color.FromRgb(q, v, p),
            2 => Color.FromRgb(p, v, t),
            3 => Color.FromRgb(p, q, v),
            4 => Color.FromRgb(t, p, v),
            _ => Color.FromRgb(v, p, q)
        };
    }

    private void DrawShape(DrawingContext dc, Geometry shape, Brush fill, Point at, double scale, double degrees)
    {
        var m = Matrix.Identity;
        m.Scale(scale, scale);
        m.Rotate(degrees);
        m.Translate(at.X, at.Y);
        dc.PushTransform(new MatrixTransform(m));
        dc.DrawGeometry(fill, null, shape);
        dc.Pop();
    }

    private void DrawMore(DrawingContext dc, TargetAuraKind kind, double width, double height, double k)
    {
        var target = TargetBounds(width, height);
        double sec = Time * 26;
        double corner = Math.Min(target.Height * .32, 10);
        var frame = target;
        frame.Inflate(EdgeInset.Left > 2 ? 2 : -1, EdgeInset.Top > 2 ? 2 : -1);
        bool wide = target.Width > 140;
        // How far particles may travel beyond the element (more when overflow gives them room).
        double reach = Math.Max(6, Math.Min(EdgeInset.Top, 26));

        switch (kind)
        {
            case TargetAuraKind.PowerSurge:
            {
                Color gold = EffectPalette.Blend(Color.FromRgb(255, 214, 64), Tint, .1);
                Color white = Color.FromRgb(255, 250, 225);
                var glow = BuildOrbSprites(gold);
                var gold2 = FillSet(gold);
                var core = FillSet(white);
                dc.DrawRoundedRectangle(null, PenSet(gold, 1.6)[O(.55 * k + .2 * Math.Sin(sec * 18))], frame, corner, corner);
                int spikes = wide ? 22 : 12;
                for (int i = 0; i < spikes; i++)
                {
                    var seed = Seeds[i % Seeds.Length];
                    double u = (i + .5) / spikes;
                    double flicker = .55 + .45 * Math.Abs(Math.Sin(sec * (7 + seed.Speed * 6) + seed.Phase));
                    double x = target.Left + target.Width * u;
                    double baseY = target.Bottom - 1;
                    double h = (target.Height * .7 + reach) * flicker * (.6 + .4 * k);
                    double w = target.Width / spikes * 1.2;
                    var spike = new StreamGeometry();
                    using (var c = spike.Open())
                    {
                        c.BeginFigure(new Point(x - w * .5, baseY), true, true);
                        c.QuadraticBezierTo(new Point(x - w * .1, baseY - h * .55), new Point(x + Math.Sin(sec * 9 + i) * 2, baseY - h), true, true);
                        c.QuadraticBezierTo(new Point(x + w * .1, baseY - h * .55), new Point(x + w * .5, baseY), true, true);
                    }
                    spike.Freeze();
                    dc.DrawGeometry(gold2[O((.25 + .35 * k) * flicker)], null, spike);
                }
                for (int i = 0; i < (wide ? 6 : 4); i++)
                {
                    var seed = Seeds[(i + 7) % Seeds.Length];
                    double rise = (seed.Start + sec * (.9 + seed.Speed)) % 1;
                    var p = new Point(target.Left + target.Width * seed.Lane, target.Bottom - rise * (target.Height + reach));
                    dc.DrawImage(glow[O(Math.Sin(rise * Math.PI) * .7 * k)], new Rect(p.X - 6, p.Y - 6, 12, 12));
                    dc.DrawEllipse(core[O(Math.Sin(rise * Math.PI) * k)], null, p, 1.3, 1.3);
                }
                break;
            }
            case TargetAuraKind.SpiritFlame:
            {
                Color blue = EffectPalette.Blend(Color.FromRgb(96, 165, 250), Tint, .15);
                var orbs = BuildOrbSprites(blue);
                var white = BuildOrbSprites(Color.FromRgb(224, 242, 254));
                int wisps = wide ? 5 : 3;
                for (int i = 0; i < wisps; i++)
                {
                    double t = (sec * .09 + i / (double)wisps) % 1;
                    var head = PerimeterPoint(Inflated(frame, reach * .35), t);
                    for (int tail = 6; tail >= 0; tail--)
                    {
                        var p = PerimeterPoint(Inflated(frame, reach * .35), (t - tail * .012 + 1) % 1);
                        p.Y -= tail * 1.6 + Math.Sin(sec * 6 + tail + i) * 1.2;
                        double r = (7 - tail) * 1.3 * (.7 + .4 * k);
                        dc.DrawImage(orbs[O((1 - tail / 7.0) * .55 * k)], new Rect(p.X - r, p.Y - r, r * 2, r * 2));
                    }
                    dc.DrawImage(white[O(.85 * k)], new Rect(head.X - 4, head.Y - 4, 8, 8));
                }
                break;
            }
            case TargetAuraKind.SpeedLines:
            {
                Color ink = EffectPalette.Blend(Tint, Colors.White, .45);
                var pens = PenSet(ink, 1.1);
                var center = new Point(target.Left + target.Width / 2, target.Top + target.Height / 2);
                int lines = wide ? 34 : 22;
                int frameStep = (int)(sec * 12);
                for (int i = 0; i < lines; i++)
                {
                    double a = (i + R(i * 31 + frameStep) * .8) / lines * Math.Tau;
                    var edge = PerimeterPoint(frame, a / Math.Tau);
                    double dx = edge.X - center.X, dy = edge.Y - center.Y, len = Math.Sqrt(dx * dx + dy * dy);
                    if (len < 1) continue;
                    double l = (4 + reach * (.4 + R(i * 17 + frameStep) * .9));
                    var start = new Point(edge.X + dx / len * 2, edge.Y + dy / len * 2);
                    dc.DrawLine(pens[O((.35 + .45 * k) * R(i * 7 + frameStep))], start, new Point(start.X + dx / len * l, start.Y + dy / len * l));
                }
                dc.DrawRoundedRectangle(null, PenSet(ink, 1.4)[O(.5 * k)], frame, corner, corner);
                break;
            }
            case TargetAuraKind.Shoujo:
            {
                Color pink = EffectPalette.Blend(Color.FromRgb(249, 168, 212), Tint, .15);
                Color lilac = Color.FromRgb(216, 180, 254);
                var pinkFill = FillSet(pink);
                var lilacFill = FillSet(lilac);
                var whiteFill = FillSet(Colors.White);
                var bubblePens = PenSet(Color.FromRgb(255, 240, 250), .9);
                dc.DrawRoundedRectangle(null, PenSet(pink, 1.2)[O(.45 * k)], frame, corner, corner);
                int count = wide ? 14 : 9;
                for (int i = 0; i < count; i++)
                {
                    var seed = Seeds[i];
                    double life = (seed.Start + sec * (.18 + seed.Speed * .12)) % 1;
                    double fade = Math.Sin(life * Math.PI);
                    var p = PerimeterPoint(Inflated(frame, reach * .5 * seed.Size), (seed.Lane + life * .15) % 1);
                    p.Y -= life * reach * .6;
                    double twinkle = .5 + .5 * Math.Sin(sec * 8 + seed.Phase);
                    switch (i % 3)
                    {
                        case 0: DrawShape(dc, SparkShape, whiteFill[O(fade * twinkle * k)], p, .7 + .6 * seed.Size, sec * 40 + i * 20); break;
                        case 1: dc.DrawEllipse(null, bubblePens[O(fade * .7 * k)], p, 2.5 + 2 * seed.Size, 2.5 + 2 * seed.Size); break;
                        default:
                            for (int petal = 0; petal < 5; petal++)
                            {
                                double a = petal * Math.Tau / 5 + sec;
                                dc.DrawEllipse((i % 2 == 0 ? pinkFill : lilacFill)[O(fade * .8 * k)], null, new Point(p.X + Math.Cos(a) * 2.2, p.Y + Math.Sin(a) * 2.2), 1.7, 1.7);
                            }
                            dc.DrawEllipse(whiteFill[O(fade * k)], null, p, 1, 1);
                            break;
                    }
                }
                break;
            }
            case TargetAuraKind.KiCharge:
            {
                Color ki = EffectPalette.Blend(Color.FromRgb(125, 211, 252), Tint, .3);
                var pens = PenSet(ki, 1.5);
                var dashes = new Pen(new SolidColorBrush(Color.FromArgb((byte)(200 * k), ki.R, ki.G, ki.B)), 1.6) { DashStyle = new DashStyle([3, 4], sec * 6) };
                for (int ring = 0; ring < 3; ring++)
                {
                    double t = (sec * .8 + ring / 3.0) % 1;
                    var r = Inflated(frame, t * reach);
                    dc.DrawRoundedRectangle(null, pens[O((1 - t) * .7 * k)], r, corner + t * 6, corner + t * 6);
                }
                dc.DrawRoundedRectangle(null, dashes, Inflated(frame, 2), corner + 2, corner + 2);
                var glow = BuildOrbSprites(ki);
                for (int i = 0; i < (wide ? 10 : 6); i++)
                {
                    var seed = Seeds[i];
                    double t = (seed.Start + sec * (.7 + seed.Speed * .5)) % 1;
                    // Energy motes gathering inward toward the element.
                    var outer = PerimeterPoint(Inflated(frame, reach), seed.Lane);
                    var inner = PerimeterPoint(frame, seed.Lane);
                    var p = new Point(outer.X + (inner.X - outer.X) * t, outer.Y + (inner.Y - outer.Y) * t);
                    dc.DrawImage(glow[O(Math.Sin(t * Math.PI) * .8 * k)], new Rect(p.X - 4, p.Y - 4, 8, 8));
                }
                break;
            }
            case TargetAuraKind.Halo:
            {
                Color light = EffectPalette.Blend(Color.FromRgb(254, 240, 138), Tint, .15);
                var glow = BuildOrbSprites(light);
                double bob = Math.Sin(sec * 2.2) * 1.5;
                double rx = Math.Min(target.Width * .3, 26), ry = Math.Max(2.5, rx * .22);
                var c = new Point(target.Left + target.Width / 2, target.Top - Math.Min(reach, 10) * .5 + bob);
                dc.DrawImage(glow[O(.45 * k)], new Rect(c.X - rx * 1.3, c.Y - ry * 3, rx * 2.6, ry * 6));
                dc.DrawEllipse(null, PenSet(light, 2.2)[O(.85 * k)], c, rx, ry);
                dc.DrawEllipse(null, PenSet(Colors.White, .8)[O(.7 * k)], c, rx, ry);
                var rays = PenSet(light, 1);
                for (int i = 0; i < 7; i++)
                {
                    double x = target.Left + target.Width * (i + .5) / 7;
                    double shimmer = .5 + .5 * Math.Sin(sec * 3 + i * 1.3);
                    dc.DrawLine(rays[O(.25 * shimmer * k)], new Point(x, target.Top), new Point(x + (x - c.X) * .15, target.Bottom));
                }
                break;
            }
            case TargetAuraKind.Hearts:
            case TargetAuraKind.Notes:
            case TargetAuraKind.Bubbles:
            case TargetAuraKind.Pixel:
            {
                bool hearts = kind == TargetAuraKind.Hearts, notes = kind == TargetAuraKind.Notes, pixel = kind == TargetAuraKind.Pixel;
                Color color = hearts ? EffectPalette.Blend(Color.FromRgb(244, 63, 94), Tint, .15)
                    : notes ? EffectPalette.Blend(Tint, Colors.White, .35)
                    : pixel ? Color.FromRgb(74, 222, 128) : EffectPalette.Blend(Color.FromRgb(186, 230, 253), Tint, .2);
                var fills = FillSet(color);
                var pens = PenSet(EffectPalette.Blend(color, Colors.White, .4), 1);
                int count = wide ? 12 : 7;
                for (int i = 0; i < count; i++)
                {
                    var seed = Seeds[i];
                    double rise = (seed.Start + sec * (.12 + seed.Speed * .1)) % 1;
                    double fade = Math.Sin(rise * Math.PI);
                    double x = target.Left + target.Width * (.05 + .9 * seed.Lane) + Math.Sin(rise * Math.Tau * 1.5 + seed.Phase) * 5;
                    double y = target.Bottom - rise * (target.Height + reach);
                    double size = .9 + .7 * seed.Size;
                    if (hearts) DrawShape(dc, HeartShape, fills[O(fade * .85 * k)], new Point(x, y), size, Math.Sin(sec * 2 + i) * 12);
                    else if (notes) DrawShape(dc, NoteShape, fills[O(fade * .9 * k)], new Point(x, y), size * 1.2, Math.Sin(sec * 3 + i) * 10);
                    else if (pixel)
                    {
                        var pc = FillSet(Hue(seed.Lane + sec * .05));
                        double s = 2 + Math.Round(seed.Size * 2);
                        dc.DrawRectangle(pc[O(fade * .9 * k)], null, new Rect(Math.Round(x), Math.Round(y), s, s));
                    }
                    else
                    {
                        double r = 2.2 + 3 * seed.Size;
                        dc.DrawEllipse(fills[O(fade * .18 * k)], pens[O(fade * .75 * k)], new Point(x, y), r, r);
                        dc.DrawEllipse(FillSet(Colors.White)[O(fade * .8 * k)], null, new Point(x - r * .35, y - r * .35), r * .25, r * .25);
                    }
                }
                if (pixel) dc.DrawRectangle(null, PenSet(color, 2)[O(.55 * k)], frame);
                break;
            }
            case TargetAuraKind.Stardust:
            {
                Color star = EffectPalette.Blend(Tint, Colors.White, .55);
                var fills = FillSet(star);
                int count = wide ? 26 : 16;
                for (int i = 0; i < count; i++)
                {
                    var seed = Seeds[i % Seeds.Length];
                    double t = (seed.Lane + sec * (.04 + seed.Speed * .03) * (i % 2 == 0 ? 1 : -1) + 1) % 1;
                    var p = PerimeterPoint(Inflated(frame, (seed.Size - .5) * reach * .6), t);
                    double twinkle = Math.Pow(.5 + .5 * Math.Sin(sec * (4 + seed.Speed * 4) + seed.Phase), 2);
                    if (i % 4 == 0) DrawShape(dc, SparkShape, fills[O(twinkle * k)], p, .5 + .4 * seed.Size, 0);
                    else dc.DrawEllipse(fills[O(twinkle * .9 * k)], null, p, .8 + .6 * seed.Size, .8 + .6 * seed.Size);
                }
                break;
            }
            case TargetAuraKind.Shadow:
            {
                Color dark = EffectPalette.Blend(Color.FromRgb(46, 16, 101), Tint, .2);
                var smoke = BuildSmokeSprites(dark);
                var eyes = FillSet(Color.FromRgb(244, 63, 94));
                int puffs = wide ? 14 : 9;
                for (int i = 0; i < puffs; i++)
                {
                    var seed = Seeds[i];
                    double rise = (seed.Start + sec * (.1 + seed.Speed * .08)) % 1;
                    var p = PerimeterPoint(frame, (seed.Lane + rise * .1) % 1);
                    p.Y -= rise * reach;
                    double r = (6 + 8 * rise) * (.7 + .4 * seed.Size);
                    dc.DrawImage(smoke[O(Math.Sin(rise * Math.PI) * .55 * k)], new Rect(p.X - r, p.Y - r, r * 2, r * 2));
                }
                dc.DrawRoundedRectangle(null, PenSet(dark, 1.5)[O(.7 * k)], frame, corner, corner);
                double blink = Math.Sin(sec * .9) > .2 ? 1 : 0;
                var eye = new Point(target.Right - 8, target.Top + 4);
                dc.DrawEllipse(eyes[O(blink * .7 * k)], null, eye, 1.2, 1.2);
                dc.DrawEllipse(eyes[O(blink * .7 * k)], null, new Point(eye.X - 4, eye.Y), 1.2, 1.2);
                break;
            }
            case TargetAuraKind.Butterflies:
            case TargetAuraKind.LeafWhirl:
            {
                bool butterfly = kind == TargetAuraKind.Butterflies;
                int count = wide ? 5 : 3;
                for (int i = 0; i < count; i++)
                {
                    var seed = Seeds[i + 3];
                    double t = (seed.Start + sec * (butterfly ? .05 : .09)) % 1;
                    var p = PerimeterPoint(Inflated(frame, reach * (.3 + .4 * seed.Size)), t);
                    p.Y += Math.Sin(sec * 3 + seed.Phase) * 3;
                    Color c = butterfly ? Hue(seed.Lane * .7 + .55) : EffectPalette.Blend(Color.FromRgb(132, 204, 22), Tint, .2);
                    var fill = FillSet(c)[O(.85 * k)];
                    if (butterfly)
                    {
                        double flap = .35 + .65 * Math.Abs(Math.Sin(sec * 14 + i));
                        var m = new Matrix(flap, 0, 0, 1, p.X, p.Y);
                        dc.PushTransform(new MatrixTransform(m));
                        dc.DrawGeometry(fill, null, WingShape);
                        dc.PushTransform(new ScaleTransform(-1, 1));
                        dc.DrawGeometry(fill, null, WingShape);
                        dc.Pop();
                        dc.Pop();
                    }
                    else DrawShape(dc, LeafShape, fill, p, 1 + seed.Size * .6, sec * 120 + i * 60);
                }
                break;
            }
            case TargetAuraKind.Glitch:
            {
                int step = (int)(sec * 10);
                bool burst = R(step) > .55;
                double jx = burst ? (R(step * 3) - .5) * 5 : 0, jy = burst ? (R(step * 5) - .5) * 2 : 0;
                dc.DrawRoundedRectangle(null, PenSet(Color.FromRgb(255, 0, 90), 1.4)[O(.6 * k)], Offset(frame, jx - 1.5, jy), corner, corner);
                dc.DrawRoundedRectangle(null, PenSet(Color.FromRgb(0, 229, 255), 1.4)[O(.6 * k)], Offset(frame, -jx + 1.5, -jy), corner, corner);
                if (burst)
                {
                    var bars = FillSet(EffectPalette.Blend(Tint, Colors.White, .4));
                    for (int b = 0; b < 3; b++)
                    {
                        double y = target.Top + target.Height * R(step * 7 + b);
                        dc.DrawRectangle(bars[O(.35 * k)], null, new Rect(target.Left + target.Width * R(step + b * 11) * .5 - reach * .3, y, target.Width * (.2 + .5 * R(step * 13 + b)), 1.5 + R(b + step) * 2));
                    }
                }
                break;
            }
            case TargetAuraKind.Ripple:
            {
                Color water = EffectPalette.Blend(Color.FromRgb(56, 189, 248), Tint, .35);
                var pens = PenSet(water, 1.3);
                for (int ring = 0; ring < 3; ring++)
                {
                    double t = (sec * .45 + ring / 3.0) % 1;
                    double grow = t * reach;
                    dc.DrawRoundedRectangle(null, pens[O((1 - t) * (1 - t) * .8 * k)], Inflated(frame, grow), corner + grow, corner + grow);
                }
                break;
            }
            case TargetAuraKind.Comet:
            {
                Color head = EffectPalette.Blend(Tint, Colors.White, .5);
                var glow = BuildOrbSprites(head);
                var path = Inflated(frame, 1);
                for (int c = 0; c < 2; c++)
                {
                    double t = (sec * .22 + c * .5) % 1;
                    for (int tail = 14; tail >= 0; tail--)
                    {
                        var p = PerimeterPoint(path, (t - tail * .008 + 1) % 1);
                        double r = (1 - tail / 15.0) * 5 + 1;
                        dc.DrawImage(glow[O((1 - tail / 15.0) * .75 * k)], new Rect(p.X - r, p.Y - r, r * 2, r * 2));
                    }
                }
                break;
            }
            case TargetAuraKind.Rays:
            {
                Color light = EffectPalette.Blend(Tint, Colors.White, .5);
                var fills = FillSet(light);
                var c = new Point(target.Left + target.Width / 2, target.Top + target.Height / 2);
                double len = Math.Max(target.Width, target.Height) * .5 + reach;
                int rays = 12;
                for (int i = 0; i < rays; i++)
                {
                    double a = i * Math.Tau / rays + sec * .25;
                    double spread = .09;
                    var g = new StreamGeometry();
                    using (var ctx = g.Open())
                    {
                        ctx.BeginFigure(c, true, true);
                        ctx.LineTo(new Point(c.X + Math.Cos(a - spread) * len, c.Y + Math.Sin(a - spread) * len * .6), false, false);
                        ctx.LineTo(new Point(c.X + Math.Cos(a + spread) * len, c.Y + Math.Sin(a + spread) * len * .6), false, false);
                    }
                    g.Freeze();
                    dc.DrawGeometry(fills[O((.12 + .1 * Math.Sin(sec * 2 + i)) * k)], null, g);
                }
                break;
            }
            case TargetAuraKind.Snow:
            {
                Color frost = Color.FromRgb(224, 242, 254);
                var fills = FillSet(frost);
                dc.DrawRoundedRectangle(null, PenSet(EffectPalette.Blend(frost, Tint, .2), 1.3)[O(.55 * k)], frame, corner, corner);
                int count = wide ? 16 : 10;
                for (int i = 0; i < count; i++)
                {
                    var seed = Seeds[i];
                    double fall = (seed.Start + sec * (.1 + seed.Speed * .07)) % 1;
                    double x = target.Left + target.Width * seed.Lane + Math.Sin(fall * Math.Tau + seed.Phase) * 4;
                    double y = target.Top - reach + fall * (target.Height + reach * 1.5);
                    dc.DrawEllipse(fills[O(Math.Sin(fall * Math.PI) * .9 * k)], null, new Point(x, y), .9 + seed.Size, .9 + seed.Size);
                }
                break;
            }
            case TargetAuraKind.Rainbow:
            {
                var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1), MappingMode = BrushMappingMode.RelativeToBoundingBox };
                for (int i = 0; i <= 6; i++)
                {
                    var c = Hue(i / 6.0 + sec * .15);
                    brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(230 * k), c.R, c.G, c.B), i / 6.0));
                }
                brush.Freeze();
                var pen = new Pen(brush, 2.2);
                pen.Freeze();
                dc.DrawRoundedRectangle(null, pen, frame, corner, corner);
                var glow = BuildOrbSprites(Hue(sec * .15));
                var p = PerimeterPoint(frame, sec * .2 % 1);
                dc.DrawImage(glow[O(.7 * k)], new Rect(p.X - 7, p.Y - 7, 14, 14));
                break;
            }
        }
    }

    private static Rect Inflated(Rect r, double by)
    {
        r.Inflate(by, by);
        return r;
    }

    private static Rect Offset(Rect r, double x, double y)
    {
        r.Offset(x, y);
        return r;
    }

    private static double R(int seed)
    {
        unchecked
        {
            uint x = (uint)seed * 2654435761u;
            x ^= x >> 15; x *= 2246822519u; x ^= x >> 13;
            return (x & 0xFFFFFF) / (double)0x1000000;
        }
    }
}

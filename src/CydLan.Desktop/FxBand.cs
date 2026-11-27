using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

/// <summary>Extra animated effects for the title bar and chat header; several can play at once.</summary>
public static class BandEffects
{
    public static IReadOnlyList<StyleOption> All { get; } =
    [
        new("fireworks", "Fireworks"),
        new("snow", "Snowfall"),
        new("rainbow", "Rainbow"),
        new("hearts", "Hearts"),
        new("lightning", "Lightning"),
        new("coderain", "Code rain"),
        new("confetti", "Confetti"),
        new("heartbeat", "Heartbeat"),
        new("meteors", "Meteors"),
        new("bubbles", "Bubbles"),
        new("scanline", "Scanline"),
        new("neonedge", "Neon edge"),
        new("petals", "Petals"),
        new("glitch", "Glitch"),
        new("helix", "Helix"),
        new("fireflies", "Fireflies")
    ];

    public static List<string> Normalize(IEnumerable<string>? values) =>
        (values ?? []).Where(v => All.Any(o => o.Value == v)).Distinct().ToList();
}

public sealed class FxBand : AmbientEffect
{
    public static readonly DependencyProperty KindsProperty = DependencyProperty.Register(
        nameof(Kinds), typeof(IReadOnlyList<string>), typeof(FxBand),
        new FrameworkPropertyMetadata(Array.Empty<string>(), FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((FxBand)d).SyncVisibility()));
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(string), typeof(FxBand),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((FxBand)d).SyncVisibility()));

    /// <summary>Effects to play together.</summary>
    public IReadOnlyList<string> Kinds { get => (IReadOnlyList<string>)GetValue(KindsProperty); set => SetValue(KindsProperty, value); }
    /// <summary>A single effect, used by previews.</summary>
    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }

    private static readonly Color[] Party =
    [
        Color.FromRgb(0xEF, 0x44, 0x44), Color.FromRgb(0xF5, 0x9E, 0x0B), Color.FromRgb(0xFA, 0xCC, 0x15), Color.FromRgb(0x22, 0xC5, 0x5E),
        Color.FromRgb(0x06, 0xB6, 0xD4), Color.FromRgb(0x3B, 0x82, 0xF6), Color.FromRgb(0x8B, 0x5C, 0xF6), Color.FromRgb(0xEC, 0x48, 0x99)
    ];

    private static readonly Geometry HeartShape = Frozen(Geometry.Parse("M0,3 C-4,0 -5,-3 -3,-4.5 C-1.5,-5.5 0,-4 0,-3 C0,-4 1.5,-5.5 3,-4.5 C5,-3 4,0 0,3 Z"));
    private static readonly Geometry PetalShape = Frozen(new EllipseGeometry(new Point(0, 0), 3.4, 1.8));

    private static Geometry Frozen(Geometry g) { g.Freeze(); return g; }

    public FxBand() => SyncVisibility();

    private void SyncVisibility() => Visibility = Active().Any() ? Visibility.Visible : Visibility.Collapsed;

    private IEnumerable<string> Active() => Kind is { Length: > 0 } k ? [k] : Kinds ?? [];

    // Stable pseudo-random value in [0,1) for a seed.
    private static double R(int seed)
    {
        unchecked
        {
            uint x = (uint)seed * 2654435761u;
            x ^= x >> 15; x *= 2246822519u; x ^= x >> 13;
            return (x & 0xFFFFFF) / (double)0x1000000;
        }
    }

    private static Brush B(Color c, double alpha)
    {
        var b = new SolidColorBrush(Color.FromArgb((byte)Math.Clamp(alpha * 255, 0, 255), c.R, c.G, c.B));
        b.Freeze();
        return b;
    }

    private static Pen P(Color c, double alpha, double thickness)
    {
        var p = new Pen(B(c, alpha), thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        p.Freeze();
        return p;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double w = ActualWidth, h = ActualHeight;
        if (w < 24 || h < 10) return;
        double k = Math.Clamp(Intensity, .1, 1);
        double t = Time;
        Color tint = Tint;
        foreach (string kind in Active())
        {
            switch (kind)
            {
                case "fireworks": Fireworks(dc, w, h, k, t); break;
                case "snow": Snow(dc, w, h, k, t, tint); break;
                case "rainbow": Rainbow(dc, w, h, k, t); break;
                case "hearts": Hearts(dc, w, h, k, t); break;
                case "lightning": Lightning(dc, w, h, k, t, tint); break;
                case "coderain": CodeRain(dc, w, h, k, t, tint); break;
                case "confetti": Confetti(dc, w, h, k, t); break;
                case "heartbeat": Heartbeat(dc, w, h, k, t, tint); break;
                case "meteors": Meteors(dc, w, h, k, t, tint); break;
                case "bubbles": Bubbles(dc, w, h, k, t, tint); break;
                case "scanline": Scanline(dc, w, h, k, t, tint); break;
                case "neonedge": NeonEdge(dc, w, h, k, t, tint); break;
                case "petals": Petals(dc, w, h, k, t); break;
                case "glitch": Glitch(dc, w, h, k, t, tint); break;
                case "helix": Helix(dc, w, h, k, t, tint); break;
                case "fireflies": Fireflies(dc, w, h, k, t); break;
            }
        }
    }

    private static int Count(double w, double per, int min, int max) => Math.Clamp((int)(w / per), min, max);

    private static void Fireworks(DrawingContext dc, double w, double h, double k, double t)
    {
        int bursts = Count(w, 160, 2, 8);
        for (int i = 0; i < bursts; i++)
        {
            double period = 1.6 + R(i * 7) * 1.2;
            double local = (t * 1.4 + R(i * 3)) / period;
            int cycle = (int)Math.Floor(local);
            double u = local - cycle;
            var c = Party[(int)(R(i * 11 + cycle) * Party.Length)];
            var center = new Point(R(i * 13 + cycle * 5) * w, h * (.25 + R(i * 17 + cycle) * .5));
            double radius = Math.Min(h * .7, 26) * Math.Sqrt(u);
            double fade = (1 - u) * (.4 + .6 * k);
            for (int s = 0; s < 10; s++)
            {
                double a = s / 10.0 * Math.Tau + cycle;
                var p = new Point(center.X + Math.Cos(a) * radius, center.Y + Math.Sin(a) * radius + u * u * 6);
                dc.DrawEllipse(B(c, fade), null, p, 1.4, 1.4);
            }
        }
    }

    private static void Snow(DrawingContext dc, double w, double h, double k, double t, Color tint)
    {
        var flake = EffectPalette.Blend(tint, Colors.White, .45);
        int n = Count(w, 14, 10, 120);
        for (int i = 0; i < n; i++)
        {
            double speed = .25 + R(i) * .35;
            double y = ((t * speed + R(i * 5)) % 1) * (h + 6) - 3;
            double x = (R(i * 9) * w + Math.Sin(t * 2 + i) * 5 + w) % w;
            dc.DrawEllipse(B(flake, (.45 + .45 * R(i * 3)) * (.5 + .5 * k)), null, new Point(x, y), .9 + R(i * 7) * 1.3, .9 + R(i * 7) * 1.3);
        }
    }

    private static void Rainbow(DrawingContext dc, double w, double h, double k, double t)
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0), MappingMode = BrushMappingMode.RelativeToBoundingBox, SpreadMethod = GradientSpreadMethod.Repeat };
        double shift = t * .25 % 1;
        for (int i = 0; i <= Party.Length; i++)
            brush.GradientStops.Add(new GradientStop(Party[i % Party.Length], (i / (double)Party.Length + shift) % 1.0001));
        brush.Transform = new TranslateTransform(-shift * w, 0);
        brush.Opacity = .45 + .45 * k;
        dc.DrawRectangle(brush, null, new Rect(0, h - 3, w, 3));
        var glow = brush.Clone(); glow.Opacity = .08 + .1 * k;
        dc.DrawRectangle(glow, null, new Rect(0, 0, w, h));
    }

    private static void Hearts(DrawingContext dc, double w, double h, double k, double t)
    {
        int n = Count(w, 60, 4, 22);
        for (int i = 0; i < n; i++)
        {
            double u = (t * (.35 + R(i) * .3) + R(i * 3)) % 1;
            double x = R(i * 5) * w + Math.Sin(t * 3 + i) * 4;
            double y = h + 6 - u * (h + 12);
            double s = .8 + R(i * 7) * .8;
            var c = R(i * 11) < .5 ? Color.FromRgb(0xF4, 0x3F, 0x5E) : Color.FromRgb(0xF4, 0x72, 0xB6);
            dc.PushTransform(new MatrixTransform(s, 0, 0, s, x, y));
            dc.DrawGeometry(B(c, Math.Sin(u * Math.PI) * (.5 + .45 * k)), null, HeartShape);
            dc.Pop();
        }
    }

    private static void Lightning(DrawingContext dc, double w, double h, double k, double t, Color tint)
    {
        double cycle = t * .7;
        int strike = (int)Math.Floor(cycle);
        double u = cycle - strike;
        if (u > .22) return;
        double flash = 1 - u / .22;
        var bolt = Color.FromRgb(0xE0, 0xF2, 0xFE);
        dc.DrawRectangle(B(EffectPalette.Blend(tint, Colors.White, .6), .12 * flash * k), null, new Rect(0, 0, w, h));
        double x = R(strike * 7) * w;
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(new Point(x, 0), false, false);
            double cx = x;
            for (int s = 1; s <= 6; s++) { cx += (R(strike * 31 + s) - .5) * 22; g.LineTo(new Point(cx, h * s / 6), true, true); }
        }
        geo.Freeze();
        dc.DrawGeometry(null, P(tint, .5 * flash, 4), geo);
        dc.DrawGeometry(null, P(bolt, flash * (.6 + .4 * k), 1.4), geo);
    }

    private static void CodeRain(DrawingContext dc, double w, double h, double k, double t, Color tint)
    {
        var green = EffectPalette.Blend(tint, Color.FromRgb(0x22, 0xC5, 0x5E), .6);
        int cols = Count(w, 12, 8, 160);
        for (int i = 0; i < cols; i++)
        {
            double speed = .6 + R(i) * .9;
            double head = ((t * speed + R(i * 3)) % 1) * (h + 30) - 10;
            double x = (i + .5) * w / cols;
            for (int s = 0; s < 6; s++)
            {
                double y = head - s * 5;
                if (y < -3 || y > h + 3) continue;
                double a = (s == 0 ? .95 : .55 * (1 - s / 6.0)) * (.4 + .6 * k);
                dc.DrawRectangle(B(s == 0 ? Color.FromRgb(0xD9, 0xF9, 0x9D) : green, a), null, new Rect(x - 1.2, y - 1.6, 2.4, 3.2));
            }
        }
    }

    private static void Confetti(DrawingContext dc, double w, double h, double k, double t)
    {
        int n = Count(w, 18, 10, 90);
        for (int i = 0; i < n; i++)
        {
            double u = (t * (.3 + R(i) * .4) + R(i * 3)) % 1;
            double x = (R(i * 5) * w + Math.Sin(t * 2.5 + i) * 8 + w) % w;
            double y = u * (h + 10) - 5;
            double spin = t * (4 + R(i * 9) * 6) + i;
            var c = Party[i % Party.Length];
            dc.PushTransform(new TranslateTransform(x, y));
            dc.PushTransform(new RotateTransform(spin * 57));
            dc.DrawRectangle(B(c, .55 + .4 * k), null, new Rect(-2, -1, 4 * Math.Abs(Math.Cos(spin)) + .6, 2));
            dc.Pop(); dc.Pop();
        }
    }

    private static void Heartbeat(DrawingContext dc, double w, double h, double k, double t, Color tint)
    {
        var geo = new StreamGeometry();
        double mid = h * .55, period = 120, offset = t * 90 % period;
        using (var g = geo.Open())
        {
            g.BeginFigure(new Point(0, mid), false, false);
            for (double x = -offset; x < w + period; x += period)
            {
                g.LineTo(new Point(x + 40, mid), true, true);
                g.LineTo(new Point(x + 46, mid - h * .18), true, true);
                g.LineTo(new Point(x + 52, mid + h * .32), true, true);
                g.LineTo(new Point(x + 58, mid - h * .42), true, true);
                g.LineTo(new Point(x + 64, mid + h * .12), true, true);
                g.LineTo(new Point(x + 70, mid), true, true);
            }
        }
        geo.Freeze();
        var c = EffectPalette.Blend(tint, Color.FromRgb(0x22, 0xC5, 0x5E), .4);
        dc.DrawGeometry(null, P(c, .16 * k, 4), geo);
        dc.DrawGeometry(null, P(c, .45 + .45 * k, 1.3), geo);
    }

    private static void Meteors(DrawingContext dc, double w, double h, double k, double t, Color tint)
    {
        int n = Count(w, 220, 2, 7);
        var head = EffectPalette.Blend(tint, Colors.White, .6);
        for (int i = 0; i < n; i++)
        {
            double u = (t * (.45 + R(i) * .3) + R(i * 3)) % 1;
            double sx = R(i * 5) * w * 1.2;
            var p = new Point(sx - u * w * .5, -6 + u * (h + 12));
            var tail = new Point(p.X + 28, p.Y - 10);
            double fade = Math.Sin(u * Math.PI) * (.5 + .5 * k);
            var brush = new LinearGradientBrush(Color.FromArgb((byte)(fade * 230), head.R, head.G, head.B), Color.FromArgb(0, tint.R, tint.G, tint.B), new Point(0, 0), new Point(1, 0))
            { MappingMode = BrushMappingMode.Absolute, StartPoint = p, EndPoint = tail };
            dc.DrawLine(new Pen(brush, 1.6) { StartLineCap = PenLineCap.Round }, p, tail);
            dc.DrawEllipse(B(Colors.White, fade), null, p, 1.4, 1.4);
        }
    }

    private static void Bubbles(DrawingContext dc, double w, double h, double k, double t, Color tint)
    {
        int n = Count(w, 30, 6, 50);
        var c = EffectPalette.Blend(tint, Colors.White, .4);
        for (int i = 0; i < n; i++)
        {
            double u = (t * (.25 + R(i) * .3) + R(i * 3)) % 1;
            double x = R(i * 5) * w + Math.Sin(t * 2 + i * 1.7) * 4;
            double y = h + 5 - u * (h + 10);
            double r = 1.5 + R(i * 7) * 3;
            dc.DrawEllipse(B(c, .08 * k), P(c, (.3 + .4 * k) * Math.Sin(u * Math.PI), .8), new Point(x, y), r, r);
        }
    }

    private static void Scanline(DrawingContext dc, double w, double h, double k, double t, Color tint)
    {
        for (double y = 1; y < h; y += 3) dc.DrawRectangle(B(Colors.Black, .035 * k), null, new Rect(0, y, w, 1));
        double u = t * .5 % 1;
        double sy = u * (h + 16) - 8;
        var glow = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        glow.GradientStops.Add(new GradientStop(Color.FromArgb(0, tint.R, tint.G, tint.B), 0));
        glow.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(110 * k), tint.R, tint.G, tint.B), .5));
        glow.GradientStops.Add(new GradientStop(Color.FromArgb(0, tint.R, tint.G, tint.B), 1));
        dc.DrawRectangle(glow, null, new Rect(0, sy - 6, w, 12));
    }

    private static void NeonEdge(DrawingContext dc, double w, double h, double k, double t, Color tint)
    {
        double pulse = .55 + .45 * Math.Sin(t * Math.Tau * .8);
        dc.DrawRectangle(B(tint, (.25 + .25 * k) * pulse), null, new Rect(0, h - 4, w, 4));
        dc.DrawRectangle(B(EffectPalette.Blend(tint, Colors.White, .6), (.6 + .4 * k) * pulse), null, new Rect(0, h - 1.5, w, 1.5));
        double x = t * .35 % 1 * (w + 120) - 60;
        var spot = new RadialGradientBrush(Color.FromArgb((byte)(160 * k), 255, 255, 255), Color.FromArgb(0, 255, 255, 255));
        dc.DrawEllipse(spot, null, new Point(x, h - 1), 50, 6);
    }

    private static void Petals(DrawingContext dc, double w, double h, double k, double t)
    {
        int n = Count(w, 40, 6, 40);
        for (int i = 0; i < n; i++)
        {
            double u = (t * (.18 + R(i) * .2) + R(i * 3)) % 1;
            double x = (R(i * 5) * w + u * 60 + Math.Sin(t * 2 + i) * 6) % w;
            double y = u * (h + 10) - 5;
            var c = R(i * 11) < .5 ? Color.FromRgb(0xF9, 0xA8, 0xD4) : Color.FromRgb(0xFB, 0xCF, 0xE8);
            dc.PushTransform(new MatrixTransform(Math.Cos(t * 3 + i), Math.Sin(t * 3 + i), -Math.Sin(t * 3 + i), Math.Cos(t * 3 + i), x, y));
            dc.DrawGeometry(B(c, .6 + .35 * k), null, PetalShape);
            dc.Pop();
        }
    }

    private static void Glitch(DrawingContext dc, double w, double h, double k, double t, Color tint)
    {
        int frame = (int)(t * 12);
        if (R(frame) > .35 + .3 * k) return;
        for (int i = 0; i < 4; i++)
        {
            double y = R(frame * 7 + i) * h, bh = 1 + R(frame * 11 + i) * 4;
            double x = R(frame * 13 + i) * w * .7, bw = 20 + R(frame * 17 + i) * w * .3;
            var c = i % 2 == 0 ? Color.FromRgb(0xFF, 0x00, 0x6E) : Color.FromRgb(0x00, 0xF5, 0xFF);
            dc.DrawRectangle(B(c, .25 + .3 * k), null, new Rect(x, y, bw, bh));
        }
        dc.DrawRectangle(B(tint, .05 * k), null, new Rect(0, 0, w, h));
    }

    private static void Helix(DrawingContext dc, double w, double h, double k, double t, Color tint)
    {
        var alt = EffectPalette.Blend(tint, Color.FromRgb(0xEC, 0x48, 0x99), .6);
        int n = Count(w, 9, 12, 240);
        for (int i = 0; i < n; i++)
        {
            double x = (i + .5) * w / n;
            double phase = x / 34 + t * 2.4;
            double y1 = h / 2 + Math.Sin(phase) * h * .3, y2 = h / 2 - Math.Sin(phase) * h * .3;
            double depth = (Math.Cos(phase) + 1) / 2;
            if (i % 3 == 0) dc.DrawLine(P(tint, .12 * k, .8), new Point(x, y1), new Point(x, y2));
            dc.DrawEllipse(B(tint, (.25 + .55 * depth) * k), null, new Point(x, y1), 1.2 + depth, 1.2 + depth);
            dc.DrawEllipse(B(alt, (.25 + .55 * (1 - depth)) * k), null, new Point(x, y2), 2.2 - depth, 2.2 - depth);
        }
    }

    private static void Fireflies(DrawingContext dc, double w, double h, double k, double t)
    {
        int n = Count(w, 45, 5, 30);
        var c = Color.FromRgb(0xFD, 0xE0, 0x47);
        for (int i = 0; i < n; i++)
        {
            double x = R(i) * w + Math.Sin(t * .9 + i * 2.1) * 18;
            double y = h * (.2 + .6 * R(i * 3)) + Math.Cos(t * 1.1 + i) * h * .2;
            double glow = Math.Max(0, Math.Sin(t * 2.2 + i * 1.3)) * (.5 + .5 * k);
            dc.DrawEllipse(B(c, glow * .25), null, new Point(x, y), 5, 5);
            dc.DrawEllipse(B(c, glow), null, new Point(x, y), 1.4, 1.4);
        }
    }
}

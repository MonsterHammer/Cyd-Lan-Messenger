using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

/// <summary>Cached brushes, pens, glow sprites and shapes shared by the multi-select effect layers.</summary>
internal static class FxDraw
{
    private static readonly Dictionary<Color, Brush[]> Fills = [];
    private static readonly Dictionary<(Color, double), Pen[]> Pens = [];
    private static readonly Dictionary<Color, BitmapSource[]> Glows = [];

    public static readonly Color[] Party =
    [
        Color.FromRgb(0xEF, 0x44, 0x44), Color.FromRgb(0xF5, 0x9E, 0x0B), Color.FromRgb(0xFA, 0xCC, 0x15), Color.FromRgb(0x22, 0xC5, 0x5E),
        Color.FromRgb(0x06, 0xB6, 0xD4), Color.FromRgb(0x3B, 0x82, 0xF6), Color.FromRgb(0x8B, 0x5C, 0xF6), Color.FromRgb(0xEC, 0x48, 0x99)
    ];

    public static readonly Geometry Heart = Frozen("M0,3 C-4,0 -5,-3 -3,-4.5 C-1.5,-5.5 0,-4 0,-3 C0,-4 1.5,-5.5 3,-4.5 C5,-3 4,0 0,3 Z");
    public static readonly Geometry Leaf = Frozen("M-5,0 C-3,-3.2 2.5,-3.2 5,0 C2.5,3.2 -3,3.2 -5,0 Z M-5,0 L6,0");
    public static readonly Geometry Note = Frozen("M1.6,-5 V2.2 A1.9,1.5 0 1 1 0.4,1 V-5 Z M1.6,-5 C3.5,-4.2 4.4,-3.2 3.8,-1.4 C3.6,-2.6 2.8,-3.2 1.6,-3.2 Z");
    public static readonly Geometry Wing = Frozen("M0,0 C-1,-5 -6,-6 -6,-2 C-6,0 -3,1 0,0 Z M0,0 C-1,2 -4,4.5 -4.5,2.5 C-4.8,1 -2,0.5 0,0 Z");
    public static readonly Geometry Spark = Frozen("M0,-4 Q0,0 4,0 Q0,0 0,4 Q0,0 -4,0 Q0,0 0,-4 Z");
    public static readonly Geometry Feather = Frozen("M-6,0 C-3,-2.2 3,-2.4 6,0 C3,1.6 -3,1.8 -6,0 Z");
    public static readonly Geometry Plane = Frozen("M6,0 L-5,-4 L-2.5,0 L-5,4 Z M6,0 L-2.5,0");

    private static Geometry Frozen(string data)
    {
        var g = Geometry.Parse(data);
        g.Freeze();
        return g;
    }

    public static int Level(double alpha) => EffectPalette.OpacityIndex(alpha);

    public static Brush Fill(Color color, double alpha)
    {
        if (!Fills.TryGetValue(color, out var set))
        {
            if (Fills.Count > 256) Fills.Clear();
            set = new Brush[EffectPalette.OpacityLevels];
            for (int i = 0; i < set.Length; i++)
            {
                var b = new SolidColorBrush(EffectPalette.WithOpacity(color, i));
                b.Freeze();
                set[i] = b;
            }
            Fills[color] = set;
        }
        return set[Level(alpha)];
    }

    public static Pen Stroke(Color color, double alpha, double thickness)
    {
        thickness = Math.Round(thickness * 4) / 4;
        if (!Pens.TryGetValue((color, thickness), out var set))
        {
            if (Pens.Count > 256) Pens.Clear();
            set = new Pen[EffectPalette.OpacityLevels];
            for (int i = 0; i < set.Length; i++)
            {
                var b = new SolidColorBrush(EffectPalette.WithOpacity(color, i));
                b.Freeze();
                var p = new Pen(b, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
                p.Freeze();
                set[i] = p;
            }
            Pens[(color, thickness)] = set;
        }
        return set[Level(alpha)];
    }

    /// <summary>A soft round glow sprite at the given opacity.</summary>
    public static BitmapSource Glow(Color color, double alpha)
    {
        if (!Glows.TryGetValue(color, out var set))
        {
            if (Glows.Count > 64) Glows.Clear();
            const int size = 32, stride = size * 4;
            Color bright = EffectPalette.Blend(color, Colors.White, .45);
            set = new BitmapSource[EffectPalette.OpacityLevels];
            for (int i = 0; i < set.Length; i++)
            {
                double scale = i / (double)(EffectPalette.OpacityLevels - 1);
                var pixels = new byte[size * stride];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        double dx = (x + .5) / size * 2 - 1, dy = (y + .5) / size * 2 - 1;
                        double d = Math.Sqrt(dx * dx + dy * dy);
                        if (d >= 1) continue;
                        double falloff = Math.Pow(1 - d, 1.8);
                        var c = EffectPalette.Blend(color, bright, Math.Max(0, 1 - d * 2.2));
                        byte a = (byte)Math.Clamp(255 * scale * falloff, 0, 255);
                        int o = y * stride + x * 4;
                        pixels[o] = (byte)(c.B * a / 255);
                        pixels[o + 1] = (byte)(c.G * a / 255);
                        pixels[o + 2] = (byte)(c.R * a / 255);
                        pixels[o + 3] = a;
                    }
                var bmp = BitmapSource.Create(size, size, 96, 96, PixelFormats.Pbgra32, null, pixels, stride);
                bmp.Freeze();
                set[i] = bmp;
            }
            Glows[color] = set;
        }
        return set[Level(alpha)];
    }

    public static void GlowAt(DrawingContext dc, Color color, double alpha, Point at, double radius) =>
        dc.DrawImage(Glow(color, alpha), new Rect(at.X - radius, at.Y - radius, radius * 2, radius * 2));

    public static void Shape(DrawingContext dc, Geometry shape, Brush? fill, Pen? pen, Point at, double scale, double degrees, double scaleX = 1)
    {
        var m = Matrix.Identity;
        m.Scale(scale * scaleX, scale);
        m.Rotate(degrees);
        m.Translate(at.X, at.Y);
        dc.PushTransform(new MatrixTransform(m));
        dc.DrawGeometry(fill, pen, shape);
        dc.Pop();
    }

    /// <summary>Stable pseudo-random value in [0,1) for a seed.</summary>
    public static double R(int seed)
    {
        unchecked
        {
            uint x = (uint)seed * 2654435761u;
            x ^= x >> 15; x *= 2246822519u; x ^= x >> 13;
            return (x & 0xFFFFFF) / (double)0x1000000;
        }
    }

    public static double Frac(double v) => v - Math.Floor(v);

    public static Color Hue(double h)
    {
        h = Frac(h) * 6;
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

    public static int Count(double w, double h, double perArea, int min, int max) => Math.Clamp((int)(w * h / perArea), min, max);
}

/// <summary>Base for effect layers that play a list of effects together, or one effect in a preview.</summary>
public abstract class MultiFxLayer : AmbientEffect
{
    public static readonly DependencyProperty KindsProperty = DependencyProperty.Register(
        nameof(Kinds), typeof(IReadOnlyList<string>), typeof(MultiFxLayer),
        new FrameworkPropertyMetadata(Array.Empty<string>(), FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((MultiFxLayer)d).SyncVisibility()));
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(string), typeof(MultiFxLayer),
        new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender, (d, _) => ((MultiFxLayer)d).SyncVisibility()));

    public IReadOnlyList<string> Kinds { get => (IReadOnlyList<string>)GetValue(KindsProperty); set => SetValue(KindsProperty, value); }
    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }

    protected MultiFxLayer() => SyncVisibility();
    protected override int FrameRate => 30;
    protected override bool HasActiveContent => Active().Any();

    private void SyncVisibility()
    {
        Visibility = Active().Any() ? Visibility.Visible : Visibility.Collapsed;
        UpdateAnimation();
    }

    protected IEnumerable<string> Active() => Kind is { Length: > 0 } k ? [k] : Kinds ?? [];

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double w = ActualWidth, h = ActualHeight;
        if (w < 12 || h < 12) return;
        double k = Math.Clamp(Intensity, .1, 1);
        double sec = Time * 26;
        foreach (string kind in Active()) Draw(dc, kind, w, h, k, sec, Tint);
    }

    protected abstract void Draw(DrawingContext dc, string kind, double w, double h, double k, double sec, Color tint);
}

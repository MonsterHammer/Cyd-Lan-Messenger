using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

public sealed class SakuraPetals : AmbientEffect
{
    private static readonly Geometry PetalShape = FrozenGeometry(
        "M0,6 C-3,3 -6,-1 -4,-5 Q-2,-7 0,-4 Q2,-7 4,-5 C6,-1 3,3 0,6 Z");
    private Brush[]? fills;
    private static readonly Petal[] Petals = CreatePetals();
    protected override void OnTintChanged() => fills = null;

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double width = ActualWidth;
        double height = ActualHeight;
        if (width < 24 || height < 24) return;

        double intensity = Math.Clamp(Intensity, .1, 1);
        int count = (int)Math.Clamp(width * height / 8500 * intensity, 4, Petals.Length);
        fills ??= Enumerable.Range(0, EffectPalette.OpacityLevels).Select(index => PetalBrush(Tint, index)).ToArray();
        double time = Progress;
        for (int index = 0; index < count; index++)
        {
            var petal = Petals[index];
            double fall = (petal.Start + time * petal.Falls) % 1;
            double flutter = (fall * 2 + petal.Start) * Math.Tau;
            double x = 12 + (width - 24) * petal.Lane + Math.Sin(flutter) * 17 + (fall - .5) * 35;
            double y = fall * (height + 16) - 8;
            double scale = petal.Size;
            var transform = Matrix.Identity;
            transform.Scale(scale * (.67 + Math.Abs(Math.Cos(flutter)) * .33), scale);
            transform.Rotate(petal.Tilt + fall * 360 + Math.Sin(flutter) * 28);
            transform.Translate(x, y);
            double fade = Math.Min(1, Math.Min(fall, 1 - fall) * 9);
            dc.PushTransform(new MatrixTransform(transform));
            int opacity = EffectPalette.OpacityIndex(fade * petal.Opacity * (.45 + .55 * intensity));
            dc.DrawGeometry(fills[opacity], null, PetalShape);
            dc.Pop();
        }
    }

    private static Petal[] CreatePetals()
    {
        // Stable, staggered positions keep petals distributed from the first frame.
        var random = new Random(329);
        var petals = new Petal[26];
        for (int index = 0; index < petals.Length; index++)
        {
            double lane = (index * .61803398875) % 1;
            petals[index] = new Petal(lane, random.NextDouble(), .55 + random.NextDouble() * .72,
                random.Next(0, 360), random.Next(1, 3), .48 + random.NextDouble() * .34);
        }
        return petals;
    }

    private static Geometry FrozenGeometry(string path)
    {
        var geometry = Geometry.Parse(path);
        geometry.Freeze();
        return geometry;
    }

    private static Brush PetalBrush(Color tint, int opacity)
    {
        var brush = new LinearGradientBrush(EffectPalette.WithOpacity(EffectPalette.Blend(tint, Colors.Black, .32), opacity),
            EffectPalette.WithOpacity(EffectPalette.Blend(tint, Colors.White, .52), opacity), 90);
        brush.Freeze();
        return brush;
    }

    private sealed record Petal(double Lane, double Start, double Size, int Tilt, int Falls, double Opacity);
}

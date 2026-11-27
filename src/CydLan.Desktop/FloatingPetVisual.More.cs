using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace CydLan.Desktop;

/// <summary>Extra companion parts: bodies, ears, eyes, mouths, hats, backs, props and patterns.</summary>
public sealed partial class FloatingPetVisual
{
    private static (Geometry Geo, double Width, double Height, double Top, double FaceY)? MoreBody(string body, double bodyBottom, double breath)
    {
        switch (body)
        {
            case "slime":
            {
                double w = 50 + breath * .9, h = 36 - breath * .8, top = bodyBottom - h, l = -w * .5, r = w * .5;
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    c.BeginFigure(new Point(l, bodyBottom), true, true);
                    c.BezierTo(new Point(l - 2, bodyBottom - h * .5), new Point(l + w * .12, top), new Point(0, top), true, false);
                    c.BezierTo(new Point(r - w * .12, top), new Point(r + 2, bodyBottom - h * .5), new Point(r, bodyBottom), true, false);
                    c.QuadraticBezierTo(new Point(r - 4, bodyBottom + 2), new Point(r - 8, bodyBottom - 1), true, false);
                    c.QuadraticBezierTo(new Point(r - 11, bodyBottom + 5), new Point(r - 15, bodyBottom - 1), true, false);
                    c.LineTo(new Point(l + 6, bodyBottom - 1), true, false);
                    c.QuadraticBezierTo(new Point(l + 3, bodyBottom + 3), new Point(l, bodyBottom), true, false);
                }
                g.Freeze();
                return (g, w, h, top, top + h * .52);
            }
            case "egg":
            {
                double w = 40 + breath * .5, h = 47 - breath * .6, top = bodyBottom - h;
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    c.BeginFigure(new Point(0, top), true, true);
                    c.BezierTo(new Point(w * .36, top), new Point(w * .52, bodyBottom - h * .42), new Point(w * .5, bodyBottom - h * .26), true, false);
                    c.BezierTo(new Point(w * .48, bodyBottom), new Point(w * .2, bodyBottom), new Point(0, bodyBottom), true, false);
                    c.BezierTo(new Point(-w * .2, bodyBottom), new Point(-w * .48, bodyBottom), new Point(-w * .5, bodyBottom - h * .26), true, false);
                    c.BezierTo(new Point(-w * .52, bodyBottom - h * .42), new Point(-w * .36, top), new Point(0, top), true, false);
                }
                g.Freeze();
                return (g, w, h, top, top + h * .55);
            }
            case "mushroom":
            {
                double w = 52 + breath * .6, h = 44 - breath * .6, top = bodyBottom - h, capH = h * .46, stemW = w * .62;
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    c.BeginFigure(new Point(-w * .5, top + capH), true, true);
                    c.BezierTo(new Point(-w * .5, top - 2), new Point(w * .5, top - 2), new Point(w * .5, top + capH), true, false);
                    c.QuadraticBezierTo(new Point(w * .42, top + capH + 4), new Point(stemW * .5, top + capH + 2), true, false);
                    c.BezierTo(new Point(stemW * .56, bodyBottom - 6), new Point(stemW * .5, bodyBottom), new Point(0, bodyBottom), true, false);
                    c.BezierTo(new Point(-stemW * .5, bodyBottom), new Point(-stemW * .56, bodyBottom - 6), new Point(-stemW * .5, top + capH + 2), true, false);
                    c.QuadraticBezierTo(new Point(-w * .42, top + capH + 4), new Point(-w * .5, top + capH), true, false);
                }
                g.Freeze();
                return (g, w * .86, h, top, top + h * .7);
            }
            case "octo":
            {
                double w = 46 + breath * .6, h = 42 - breath * .6, top = bodyBottom - h, l = -w * .5, r = w * .5;
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    c.BeginFigure(new Point(l, bodyBottom - 8), true, true);
                    c.BezierTo(new Point(l, top - 2), new Point(r, top - 2), new Point(r, bodyBottom - 8), true, false);
                    for (int i = 0; i < 4; i++)
                    {
                        double x0 = r - i * w / 4, x1 = r - (i + 1) * w / 4;
                        c.QuadraticBezierTo(new Point(x0 + 1, bodyBottom + 1), new Point((x0 + x1) * .5, bodyBottom), true, false);
                        c.QuadraticBezierTo(new Point(x1 + 2, bodyBottom - 1), new Point(x1, bodyBottom - 7), true, false);
                    }
                }
                g.Freeze();
                return (g, w, h, top, top + h * .46);
            }
            case "drop":
            {
                double w = 44 + breath * .6, h = 46 - breath * .6, top = bodyBottom - h;
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    c.BeginFigure(new Point(0, top - 2), true, true);
                    c.BezierTo(new Point(w * .18, top + h * .22), new Point(w * .52, bodyBottom - h * .42), new Point(w * .5, bodyBottom - h * .24), true, false);
                    c.BezierTo(new Point(w * .48, bodyBottom + 1), new Point(-w * .48, bodyBottom + 1), new Point(-w * .5, bodyBottom - h * .24), true, false);
                    c.BezierTo(new Point(-w * .52, bodyBottom - h * .42), new Point(-w * .18, top + h * .22), new Point(0, top - 2), true, false);
                }
                g.Freeze();
                return (g, w, h, top + 8, top + h * .6);
            }
            case "chonk":
            {
                double w = 58 + breath * .9, h = 34 - breath * .7, top = bodyBottom - h;
                var geo = new RectangleGeometry(new Rect(-w * .5, top, w, h), 22, 17);
                geo.Freeze();
                return (geo, w, h, top, top + h * .48);
            }
            default:
                return null;
        }
    }

    private static bool DrawMoreEars(DrawingContext dc, string ears, double bodyTop, double bodyW, Color surface, Color accent, Color accentSoft, Color detail,
        Pen outline, double breathPhase, bool hovered, bool excited)
    {
        Brush outer = FrozenBrush(EffectPalette.Blend(surface, accentSoft, .42));
        Brush inner = FrozenBrush(EffectPalette.Blend(detail, accent, .32));
        double perk = hovered || excited ? 1.6 : Math.Sin(breathPhase) * .6;
        switch (ears)
        {
            case "mouse":
                foreach (int s in new[] { -1, 1 })
                {
                    dc.DrawEllipse(outer, outline, new Point(s * bodyW * .4, bodyTop - 2 - perk), 10, 10);
                    dc.DrawEllipse(inner, null, new Point(s * bodyW * .4, bodyTop - 1 - perk), 6, 6);
                }
                return true;
            case "panda":
            {
                Brush dark = FrozenBrush(Color.FromRgb(0x23, 0x23, 0x2B));
                foreach (int s in new[] { -1, 1 }) dc.DrawEllipse(dark, outline, new Point(s * bodyW * .36, bodyTop + 1 - perk * .5), 7, 6.6);
                return true;
            }
            case "unicorn":
            {
                Brush horn = new LinearGradientBrush(Color.FromRgb(0xFD, 0xE6, 0x8A), Color.FromRgb(0xF9, 0xA8, 0xD4), 90);
                horn.Freeze();
                double tipY = bodyTop - 18 - perk;
                dc.DrawPolygon(horn, outline, new Point(-4, bodyTop + 2), new Point(1, tipY), new Point(4.5, bodyTop + 2));
                Pen swirl = FrozenPen(Color.FromArgb(170, 0xC0, 0x84, 0xFC), 1);
                for (int i = 1; i <= 3; i++) dc.DrawLine(swirl, new Point(-3.4 + i * .9, bodyTop + 1 - i * 4.6), new Point(3.6 - i * .5, bodyTop - 1 - i * 4.6));
                foreach (int s in new[] { -1, 1 })
                    DrawTriangleEar(dc, new Point(s * 17, bodyTop + 6), new Point(s * 15, bodyTop - 6 - perk), new Point(s * 8, bodyTop + 2), outer, inner, outline);
                return true;
            }
            case "dragon":
            {
                Brush hornFill = FrozenBrush(EffectPalette.Blend(detail, Colors.White, .2));
                foreach (int s in new[] { -1, 1 })
                {
                    var g = new StreamGeometry();
                    using (var c = g.Open())
                    {
                        c.BeginFigure(new Point(s * 8, bodyTop + 3), true, true);
                        c.BezierTo(new Point(s * 12, bodyTop - 6), new Point(s * 22, bodyTop - 10 - perk), new Point(s * 26, bodyTop - 6 - perk), true, false);
                        c.BezierTo(new Point(s * 20, bodyTop - 4), new Point(s * 16, bodyTop), new Point(s * 15, bodyTop + 5), true, false);
                    }
                    g.Freeze();
                    dc.DrawGeometry(hornFill, outline, g);
                }
                // Spiky crest.
                dc.DrawPolygon(FrozenBrush(accent), outline, new Point(-4, bodyTop + 2), new Point(-1.5, bodyTop - 5), new Point(1, bodyTop + 1), new Point(3, bodyTop - 4), new Point(5, bodyTop + 2));
                return true;
            }
            case "sheep":
            {
                Brush wool = FrozenBrush(Color.FromRgb(0xF5, 0xF0, 0xE6));
                Pen woolPen = FrozenPen(Color.FromArgb(150, 0x8A, 0x7E, 0x6A), .8);
                for (int i = 0; i < 6; i++)
                    dc.DrawEllipse(wool, woolPen, new Point(-12.5 + i * 5, bodyTop + 1 - (i is 2 or 3 ? 3 : 1)), 4.4, 4);
                foreach (int s in new[] { -1, 1 })
                {
                    dc.DrawEllipse(FrozenBrush(EffectPalette.Blend(surface, detail, .3)), outline, new Point(s * bodyW * .52, bodyTop + 6 + perk), 5.8, 3.2);
                    // Curled horns.
                    dc.DrawEllipse(null, FrozenPen(Color.FromRgb(0xB4, 0x8A, 0x5A), 2.4), new Point(s * bodyW * .42, bodyTop + 2), 4.6, 4.6);
                }
                return true;
            }
            case "leaf":
                foreach (int s in new[] { -1, 1 })
                {
                    var g = new StreamGeometry();
                    using (var c = g.Open())
                    {
                        c.BeginFigure(new Point(s * 6, bodyTop + 2), true, true);
                        c.QuadraticBezierTo(new Point(s * 12, bodyTop - 16 - perk), new Point(s * 22, bodyTop - 12 - perk), true, false);
                        c.QuadraticBezierTo(new Point(s * 20, bodyTop - 2), new Point(s * 6, bodyTop + 2), true, false);
                    }
                    g.Freeze();
                    dc.DrawGeometry(FrozenBrush(Color.FromRgb(0x4A, 0xDE, 0x80)), FrozenPen(Color.FromRgb(0x16, 0xA3, 0x4A), 1.1), g);
                    dc.DrawLine(FrozenPen(Color.FromRgb(0x16, 0xA3, 0x4A), .8), new Point(s * 7, bodyTop + 1), new Point(s * 19, bodyTop - 11 - perk));
                }
                return true;
            default:
                return false;
        }
    }

    private static bool DrawMoreEyes(DrawingContext dc, string eyes, double lookX, double eyeY, double eyeSep, Color accent, Color detail, Color ink,
        double blink, bool happy)
    {
        if (eyes is not ("anime" or "uwu" or "robot" or "tiny" or "glow")) return false;
        double lx = lookX - eyeSep, rx = lookX + eyeSep;
        Pen pen = FrozenPen(ink, 1.85);
        if (blink > .45 && eyes != "uwu")
        {
            DrawClosedEye(dc, lx, eyeY, pen);
            DrawClosedEye(dc, rx, eyeY, pen);
            return true;
        }
        if (happy && eyes != "robot")
        {
            DrawHappyEye(dc, lx, eyeY, pen);
            DrawHappyEye(dc, rx, eyeY, pen);
            return true;
        }
        switch (eyes)
        {
            case "anime":
                foreach (double x in new[] { lx, rx })
                {
                    var iris = new LinearGradientBrush(EffectPalette.Blend(accent, ink, .55), EffectPalette.Blend(accent, Colors.White, .15), 90);
                    iris.Freeze();
                    dc.DrawEllipse(FrozenBrush(ink), null, new Point(x, eyeY), 3.6, 5.2);
                    dc.DrawEllipse(iris, null, new Point(x, eyeY + .6), 2.8, 4.2);
                    dc.DrawEllipse(FrozenBrush(Colors.White), null, new Point(x - 1.1, eyeY - 1.8), 1.4, 1.6);
                    dc.DrawEllipse(FrozenBrush(Color.FromArgb(220, 255, 255, 255)), null, new Point(x + 1.2, eyeY + 2), .7, .7);
                    dc.DrawLine(FrozenPen(ink, 1.4), new Point(x - 4.2, eyeY - 4.8), new Point(x + 3.8, eyeY - 5.6));
                }
                return true;
            case "uwu":
                foreach (double x in new[] { lx, rx })
                {
                    var g = new StreamGeometry();
                    using (var c = g.Open())
                    {
                        c.BeginFigure(new Point(x - 3.2, eyeY - 1), false, false);
                        c.QuadraticBezierTo(new Point(x, eyeY + 3.2), new Point(x + 3.2, eyeY - 1), true, false);
                    }
                    g.Freeze();
                    dc.DrawGeometry(null, pen, g);
                }
                return true;
            case "robot":
            {
                Brush led = FrozenBrush(detail);
                Brush glow = FrozenBrush(Color.FromArgb(80, detail.R, detail.G, detail.B));
                foreach (double x in new[] { lx, rx })
                {
                    dc.DrawRoundedRectangle(glow, null, new Rect(x - 4.4, eyeY - 4.4, 8.8, 8.8), 2, 2);
                    dc.DrawRoundedRectangle(led, FrozenPen(ink, .9), new Rect(x - 3, eyeY - 3, 6, 6), 1.2, 1.2);
                    if (happy) dc.DrawLine(FrozenPen(ink, 1), new Point(x - 2, eyeY + 1), new Point(x + 2, eyeY + 1));
                }
                return true;
            }
            case "tiny":
                foreach (double x in new[] { lx, rx })
                    dc.DrawEllipse(FrozenBrush(ink), null, new Point(x, eyeY + .4), 1.5, 2.2);
                return true;
            default: // glow
                foreach (double x in new[] { lx, rx })
                {
                    var halo = new RadialGradientBrush(Color.FromArgb(160, accent.R, accent.G, accent.B), Color.FromArgb(0, accent.R, accent.G, accent.B));
                    halo.Freeze();
                    dc.DrawEllipse(halo, null, new Point(x, eyeY), 7, 7);
                    dc.DrawEllipse(FrozenBrush(EffectPalette.Blend(accent, Colors.White, .55)), null, new Point(x, eyeY), 2.8, 3.4);
                    dc.DrawEllipse(FrozenBrush(Colors.White), null, new Point(x, eyeY - .4), 1.2, 1.5);
                }
                return true;
        }
    }

    private static bool DrawMoreMouth(DrawingContext dc, string mouth, double cx, double cy, Color ink, Color detail, bool happy)
    {
        Pen pen = FrozenPen(ink, 1.6);
        switch (mouth)
        {
            case "buck":
                DrawSmile(dc, cx, cy, 3.8, 1.8, pen);
                dc.DrawRectangle(FrozenBrush(Colors.White), FrozenPen(ink, .8), new Rect(cx - 2.2, cy + 1.2, 2.2, 2.8));
                dc.DrawRectangle(FrozenBrush(Colors.White), FrozenPen(ink, .8), new Rect(cx, cy + 1.2, 2.2, 2.8));
                return true;
            case "tongue":
                DrawSmile(dc, cx, cy, 4.4, 2.2, pen);
                dc.DrawEllipse(FrozenBrush(Color.FromRgb(0xF4, 0x72, 0xB6)), FrozenPen(ink, .9), new Point(cx + 1.6, cy + 4.2), 2.4, 3);
                dc.DrawLine(FrozenPen(Color.FromArgb(160, ink.R, ink.G, ink.B), .7), new Point(cx + 1.6, cy + 2.6), new Point(cx + 1.6, cy + 5.2));
                return true;
            case "wide":
            {
                double open = happy ? 4.4 : 3.4;
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    c.BeginFigure(new Point(cx - 5.4, cy), true, true);
                    c.LineTo(new Point(cx + 5.4, cy), true, false);
                    c.ArcTo(new Point(cx - 5.4, cy), new Size(5.4, open), 0, false, SweepDirection.Clockwise, true, false);
                }
                g.Freeze();
                dc.DrawGeometry(FrozenBrush(EffectPalette.Blend(ink, Color.FromRgb(0xF4, 0x72, 0xB6), .2)), FrozenPen(ink, 1.2), g);
                dc.DrawEllipse(FrozenBrush(Color.FromRgb(0xF4, 0x72, 0xB6)), null, new Point(cx, cy + open * .6), 2.4, 1.2);
                return true;
            }
            case "vampire":
                DrawSmile(dc, cx, cy, 4.4, 1.6, pen);
                dc.DrawPolygon(FrozenBrush(Colors.White), FrozenPen(ink, .7), new Point(cx - 3.4, cy + .8), new Point(cx - 2.2, cy + 4.4), new Point(cx - 1.2, cy + 1.3));
                dc.DrawPolygon(FrozenBrush(Colors.White), FrozenPen(ink, .7), new Point(cx + 3.4, cy + .8), new Point(cx + 2.2, cy + 4.4), new Point(cx + 1.2, cy + 1.3));
                return true;
            default:
                return false;
        }
    }

    private static bool DrawMoreHeadwear(DrawingContext dc, string hat, double bodyTop, double bodyW, Color accent, Color accentSoft, Color surface, Color detail,
        Pen outline, double breathPhase, double facing)
    {
        Brush main = FrozenBrush(accent);
        Brush soft = FrozenBrush(EffectPalette.Blend(accent, Colors.White, .35));
        switch (hat)
        {
            case "pirate":
            {
                Brush black = FrozenBrush(Color.FromRgb(0x1F, 0x21, 0x2A));
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    c.BeginFigure(new Point(-20, bodyTop + 3), true, true);
                    c.QuadraticBezierTo(new Point(-14, bodyTop - 14), new Point(0, bodyTop - 12), true, false);
                    c.QuadraticBezierTo(new Point(14, bodyTop - 14), new Point(20, bodyTop + 3), true, false);
                    c.QuadraticBezierTo(new Point(0, bodyTop - 3), new Point(-20, bodyTop + 3), true, false);
                }
                g.Freeze();
                dc.DrawGeometry(black, outline, g);
                Pen bone = FrozenPen(Colors.White, 1.2);
                dc.DrawEllipse(FrozenBrush(Colors.White), null, new Point(0, bodyTop - 7), 2.4, 2.2);
                dc.DrawLine(bone, new Point(-3.4, bodyTop - 2.6), new Point(3.4, bodyTop - 5.4));
                dc.DrawLine(bone, new Point(3.4, bodyTop - 2.6), new Point(-3.4, bodyTop - 5.4));
                dc.DrawLine(FrozenPen(Color.FromRgb(0xFA, 0xCC, 0x15), 1.1), new Point(-16, bodyTop + .6), new Point(16, bodyTop + .6));
                return true;
            }
            case "witch":
            {
                Brush hatFill = FrozenBrush(Color.FromRgb(0x4C, 0x1D, 0x95));
                dc.DrawEllipse(hatFill, outline, new Point(0, bodyTop + 1), 22, 3.6);
                double tipX = facing * 9 + Math.Sin(breathPhase) * 1.2;
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    c.BeginFigure(new Point(-11, bodyTop), true, true);
                    c.QuadraticBezierTo(new Point(-4, bodyTop - 14), new Point(tipX, bodyTop - 24), true, false);
                    c.QuadraticBezierTo(new Point(4, bodyTop - 12), new Point(11, bodyTop), true, false);
                }
                g.Freeze();
                dc.DrawGeometry(hatFill, outline, g);
                dc.DrawRectangle(main, null, new Rect(-10, bodyTop - 4, 20, 3));
                DrawStarShape(dc, -4, bodyTop - 10, 2.2, FrozenBrush(Color.FromRgb(0xFA, 0xCC, 0x15)), null);
                return true;
            }
            case "party":
            {
                var stripes = new LinearGradientBrush(new GradientStopCollection
                {
                    new(accent, 0), new(accent, .33), new(detail, .33), new(detail, .66), new(EffectPalette.Blend(accent, Colors.White, .5), .66), new(EffectPalette.Blend(accent, Colors.White, .5), 1)
                }, 90);
                stripes.Freeze();
                double tilt = facing * 4;
                dc.PushTransform(new RotateTransform(tilt, 0, bodyTop));
                dc.DrawPolygon(stripes, outline, new Point(-8, bodyTop + 1), new Point(0, bodyTop - 20), new Point(8, bodyTop + 1));
                dc.DrawEllipse(FrozenBrush(Color.FromRgb(0xFA, 0xCC, 0x15)), outline, new Point(0, bodyTop - 21), 2.8, 2.8);
                dc.Pop();
                return true;
            }
            case "headband":
            {
                double flutter = Math.Sin(breathPhase * 3) * 2.4;
                dc.DrawRectangle(main, outline, new Rect(-bodyW * .5, bodyTop + 6, bodyW, 4.4));
                dc.DrawRoundedRectangle(FrozenBrush(Color.FromRgb(0xD1, 0xD5, 0xDB)), outline, new Rect(-5, bodyTop + 5.6, 10, 5.2), 1, 1);
                double tx = -facing * (bodyW * .5);
                dc.DrawPolygon(main, outline, new Point(tx, bodyTop + 7), new Point(tx - facing * 11, bodyTop + 4 + flutter), new Point(tx - facing * 9, bodyTop + 9 + flutter));
                dc.DrawPolygon(main, outline, new Point(tx, bodyTop + 9), new Point(tx - facing * 10, bodyTop + 12 - flutter), new Point(tx - facing * 6, bodyTop + 14 - flutter));
                return true;
            }
            case "cowboy":
            {
                Brush brown = FrozenBrush(Color.FromRgb(0xA1, 0x62, 0x07));
                var brim = new StreamGeometry();
                using (var c = brim.Open())
                {
                    c.BeginFigure(new Point(-25, bodyTop - 3), true, true);
                    c.QuadraticBezierTo(new Point(0, bodyTop + 5), new Point(25, bodyTop - 3), true, false);
                    c.QuadraticBezierTo(new Point(0, bodyTop + 1), new Point(-25, bodyTop - 3), true, false);
                }
                brim.Freeze();
                dc.DrawRoundedRectangle(brown, outline, new Rect(-11, bodyTop - 14, 22, 13), 5, 5);
                dc.DrawGeometry(brown, outline, brim);
                dc.DrawRectangle(FrozenBrush(Color.FromRgb(0x45, 0x1A, 0x03)), null, new Rect(-11, bodyTop - 5, 22, 2.4));
                return true;
            }
            case "viking":
            {
                Brush metal = FrozenBrush(Color.FromRgb(0x9C, 0xA3, 0xAF));
                Brush horn = FrozenBrush(Color.FromRgb(0xFE, 0xF3, 0xC7));
                foreach (int s in new[] { -1, 1 })
                {
                    var g = new StreamGeometry();
                    using (var c = g.Open())
                    {
                        c.BeginFigure(new Point(s * 13, bodyTop - 1), true, true);
                        c.QuadraticBezierTo(new Point(s * 24, bodyTop - 4), new Point(s * 24, bodyTop - 17), true, false);
                        c.QuadraticBezierTo(new Point(s * 19, bodyTop - 7), new Point(s * 11, bodyTop - 5), true, false);
                    }
                    g.Freeze();
                    dc.DrawGeometry(horn, outline, g);
                }
                var dome = new StreamGeometry();
                using (var c = dome.Open())
                {
                    c.BeginFigure(new Point(-16, bodyTop + 2), true, true);
                    c.BezierTo(new Point(-16, bodyTop - 13), new Point(16, bodyTop - 13), new Point(16, bodyTop + 2), true, false);
                }
                dome.Freeze();
                dc.DrawGeometry(metal, outline, dome);
                dc.DrawRectangle(FrozenBrush(Color.FromRgb(0xB4, 0x53, 0x09)), null, new Rect(-16, bodyTop - 1, 32, 3));
                return true;
            }
            default:
                return false;
        }
    }

    private static bool DrawMoreBack(DrawingContext dc, string back, double bodyBottom, double bodyTop, double bodyW, double bodyH,
        Color accent, Color detail, Pen outline, double breathPhase, double hopArc, bool roaming)
    {
        double flap = Math.Sin(breathPhase * (hopArc > 1 || roaming ? 4.4 : 2.1)) * (hopArc > 1 ? 14 : 7);
        double midY = bodyTop + bodyH * .5;
        switch (back)
        {
            case "dragon":
            {
                Brush membrane = FrozenBrush(Color.FromArgb(225, detail.R, detail.G, detail.B));
                Pen bone = FrozenPen(EffectPalette.Blend(detail, Colors.Black, .35), 1.3);
                foreach (int s in new[] { -1, 1 })
                {
                    dc.PushTransform(new RotateTransform(-s * flap, s * bodyW * .32, midY));
                    double x0 = s * bodyW * .3;
                    var g = new StreamGeometry();
                    using (var c = g.Open())
                    {
                        c.BeginFigure(new Point(x0, midY - 4), true, true);
                        c.LineTo(new Point(s * (bodyW * .5 + 22), midY - 22), true, false);
                        c.QuadraticBezierTo(new Point(s * (bodyW * .5 + 20), midY - 8), new Point(s * (bodyW * .5 + 24), midY + 2), true, false);
                        c.QuadraticBezierTo(new Point(s * (bodyW * .5 + 14), midY - 2), new Point(s * (bodyW * .5 + 14), midY + 8), true, false);
                        c.QuadraticBezierTo(new Point(s * (bodyW * .5 + 6), midY + 2), new Point(x0, midY + 6), true, false);
                    }
                    g.Freeze();
                    dc.DrawGeometry(membrane, outline, g);
                    dc.DrawLine(bone, new Point(x0, midY - 4), new Point(s * (bodyW * .5 + 22), midY - 22));
                    dc.Pop();
                }
                return true;
            }
            case "butterfly":
            {
                foreach (int s in new[] { -1, 1 })
                {
                    dc.PushTransform(new ScaleTransform(1 - Math.Abs(Math.Sin(breathPhase * 3)) * .35, 1, s * bodyW * .3, midY));
                    var top = new RadialGradientBrush(EffectPalette.Blend(accent, Colors.White, .4), detail);
                    top.Freeze();
                    dc.DrawEllipse(top, outline, new Point(s * (bodyW * .5 + 8), midY - 8), 13, 11);
                    dc.DrawEllipse(FrozenBrush(EffectPalette.Blend(detail, accent, .4)), outline, new Point(s * (bodyW * .5 + 5), midY + 7), 9, 8);
                    dc.DrawEllipse(FrozenBrush(Color.FromArgb(200, 255, 255, 255)), null, new Point(s * (bodyW * .5 + 11), midY - 10), 3, 3);
                    dc.Pop();
                }
                return true;
            }
            case "backpack":
            {
                Brush bag = FrozenBrush(accent);
                dc.DrawRoundedRectangle(bag, outline, new Rect(-bodyW * .5 - 7, midY - 11, 14, 20), 4, 4);
                dc.DrawRoundedRectangle(FrozenBrush(EffectPalette.Blend(accent, Colors.White, .3)), outline, new Rect(-bodyW * .5 - 6, midY - 1, 12, 8), 2.5, 2.5);
                dc.DrawLine(FrozenPen(EffectPalette.Blend(accent, Colors.Black, .3), 2), new Point(-bodyW * .5 + 2, midY - 9), new Point(-bodyW * .5 + 6, midY + 6));
                return true;
            }
            default:
                return false;
        }
    }

    private static bool DrawMoreProp(DrawingContext dc, string prop, double bodyTop, double bodyBottom, double bodyW, Color accent, Color detail, Color ink,
        double breathPhase, double facing)
    {
        double side = facing >= 0 ? 1 : -1;
        double hx = side * (bodyW * .5 + 4), hy = bodyBottom - 12 + Math.Sin(breathPhase * 1.4) * 1.2;
        Pen dark = FrozenPen(Color.FromArgb(200, ink.R, ink.G, ink.B), .9);
        switch (prop)
        {
            case "sword":
            {
                dc.PushTransform(new RotateTransform(side * 28, hx, hy));
                var blade = new LinearGradientBrush(Color.FromRgb(0xF1, 0xF5, 0xF9), Color.FromRgb(0x94, 0xA3, 0xB8), 0);
                blade.Freeze();
                dc.DrawPolygon(blade, dark, new Point(hx - 2, hy - 2), new Point(hx - 2, hy - 22), new Point(hx, hy - 26), new Point(hx + 2, hy - 22), new Point(hx + 2, hy - 2));
                dc.DrawRoundedRectangle(FrozenBrush(Color.FromRgb(0xFA, 0xCC, 0x15)), dark, new Rect(hx - 6, hy - 2.5, 12, 3), 1.2, 1.2);
                dc.DrawRectangle(FrozenBrush(Color.FromRgb(0x78, 0x35, 0x0F)), dark, new Rect(hx - 1.4, hy, 2.8, 6));
                dc.Pop();
                return true;
            }
            case "shield":
            {
                double sx = -side * (bodyW * .5 + 3);
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    c.BeginFigure(new Point(sx - 8, hy - 10), true, true);
                    c.LineTo(new Point(sx + 8, hy - 10), true, false);
                    c.BezierTo(new Point(sx + 8, hy), new Point(sx + 4, hy + 6), new Point(sx, hy + 9), true, false);
                    c.BezierTo(new Point(sx - 4, hy + 6), new Point(sx - 8, hy), new Point(sx - 8, hy - 10), true, false);
                }
                g.Freeze();
                dc.DrawGeometry(FrozenBrush(accent), FrozenPen(EffectPalette.Blend(accent, Colors.Black, .35), 1.3), g);
                DrawStarShape(dc, sx, hy - 3, 4, FrozenBrush(Color.FromRgb(0xFA, 0xCC, 0x15)), null);
                return true;
            }
            case "book":
            {
                dc.PushTransform(new RotateTransform(side * -8, hx, hy));
                dc.DrawRectangle(FrozenBrush(detail), dark, new Rect(hx - 7, hy - 6, 14, 10));
                dc.DrawRectangle(FrozenBrush(Colors.White), null, new Rect(hx - 6, hy - 5, 5.6, 8));
                dc.DrawRectangle(FrozenBrush(Colors.White), null, new Rect(hx + .4, hy - 5, 5.6, 8));
                Pen line = FrozenPen(Color.FromArgb(120, ink.R, ink.G, ink.B), .5);
                for (int i = 0; i < 3; i++) { dc.DrawLine(line, new Point(hx - 5, hy - 3 + i * 2), new Point(hx - 1.6, hy - 3 + i * 2)); dc.DrawLine(line, new Point(hx + 1.4, hy - 3 + i * 2), new Point(hx + 5, hy - 3 + i * 2)); }
                dc.Pop();
                return true;
            }
            case "bubbletea":
            {
                dc.DrawPolygon(FrozenBrush(Color.FromArgb(230, 0xE7, 0xC9, 0xA9)), dark, new Point(hx - 5, hy - 7), new Point(hx + 5, hy - 7), new Point(hx + 4, hy + 6), new Point(hx - 4, hy + 6));
                dc.DrawEllipse(FrozenBrush(Color.FromRgb(0xF9, 0xA8, 0xD4)), dark, new Point(hx, hy - 7), 5.4, 1.8);
                Brush pearl = FrozenBrush(Color.FromRgb(0x3F, 0x2A, 0x1C));
                for (int i = 0; i < 4; i++) dc.DrawEllipse(pearl, null, new Point(hx - 2.4 + i * 1.6, hy + 4 - (i % 2) * 1.2), .9, .9);
                dc.DrawLine(FrozenPen(accent, 1.6), new Point(hx + 1, hy - 8), new Point(hx + 3, hy - 15));
                return true;
            }
            default:
                return false;
        }
    }

    private static bool DrawMorePattern(DrawingContext dc, string pattern, double top, double bottom, double w, double h, Color surface, Color accent, Color detail, bool darkBody)
    {
        switch (pattern)
        {
            case "hearts":
            {
                Brush b = FrozenBrush(Color.FromArgb(darkBody ? (byte)190 : (byte)175, detail.R, detail.G, detail.B));
                for (int i = 0; i < 7; i++)
                {
                    double x = -w * .42 + (i * 13.7 % w * .84), y = top + 6 + (i * 9.3 % (h - 8));
                    DrawHeartShape(dc, x, y, 3.2 + (i % 3) * .7, b, null);
                }
                return true;
            }
            case "cow":
            {
                Brush b = FrozenBrush(darkBody ? Color.FromArgb(150, 255, 255, 255) : Color.FromArgb(200, 0x2A, 0x2A, 0x33));
                dc.DrawEllipse(b, null, new Point(-w * .3, top + h * .3), 7, 5);
                dc.DrawEllipse(b, null, new Point(w * .32, top + h * .62), 8, 6);
                dc.DrawEllipse(b, null, new Point(-w * .12, bottom - 4), 5, 3.4);
                dc.DrawEllipse(b, null, new Point(w * .1, top + 3), 4, 3);
                return true;
            }
            case "zigzag":
            {
                Pen p = FrozenPen(Color.FromArgb(darkBody ? (byte)120 : (byte)110, accent.R, accent.G, accent.B), 2);
                for (int row = 0; row < 3; row++)
                {
                    double y = top + h * (.34 + row * .22);
                    var g = new StreamGeometry();
                    using (var c = g.Open())
                    {
                        c.BeginFigure(new Point(-w * .6, y), false, false);
                        for (int i = 0; i < 10; i++) c.LineTo(new Point(-w * .6 + (i + 1) * w * .12, y + (i % 2 == 0 ? -3 : 0)), true, false);
                    }
                    g.Freeze();
                    dc.DrawGeometry(null, p, g);
                }
                return true;
            }
            default:
                return false;
        }
    }
}

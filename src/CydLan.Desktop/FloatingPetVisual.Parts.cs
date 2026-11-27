using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

public sealed partial class FloatingPetVisual
{
    private static void DrawPatternOverlay(
        DrawingContext dc, string pattern, double top, double bottom, double w, double h,
        Color surface, Color accent, Color accentSoft, Color detail, Color topFill, bool darkBody)
    {
        switch (pattern)
        {
            case "belly":
            {
                Color bellyCol = darkBody ? EffectPalette.Blend(surface, Colors.White, .72) : Color.FromRgb(0xFF, 0xFC, 0xF5);
                dc.DrawEllipse(FrozenBrush(Color.FromArgb(205, bellyCol.R, bellyCol.G, bellyCol.B)), null,
                    new Point(0, bottom - h * .18), w * .31, h * .28);
                break;
            }
            case "spots":
            {
                Brush spot = FrozenBrush(EffectPalette.Blend(accent, detail, .45));
                dc.DrawEllipse(spot, null, new Point(-w * .34, top + h * .22), 5.5, 4.2);
                dc.DrawEllipse(spot, null, new Point(w * .36, top + h * .72), 6.5, 4.8);
                dc.DrawEllipse(spot, null, new Point(-w * .14, bottom - 3), 4.0, 3.0);
                break;
            }
            case "stripes":
            {
                Pen stripePen = FrozenPen(EffectPalette.Blend(accent, detail, .35), 2.4);
                dc.DrawLine(stripePen, new Point(0, top), new Point(0, top + 6));
                dc.DrawLine(stripePen, new Point(-5, top + 1), new Point(-4, top + 5));
                dc.DrawLine(stripePen, new Point(5, top + 1), new Point(4, top + 5));
                dc.DrawLine(stripePen, new Point(-w * .5, top + h * .55), new Point(-w * .28, top + h * .57));
                dc.DrawLine(stripePen, new Point(w * .5, top + h * .55), new Point(w * .28, top + h * .57));
                break;
            }
            case "cosmic":
            {
                var cosmos = new LinearGradientBrush(
                    EffectPalette.Blend(surface, Color.FromRgb(0x1E, 0x1B, 0x4B), .55),
                    EffectPalette.Blend(accent, Color.FromRgb(0x7C, 0x3A, 0xED), .55), 60) { Opacity = .55 };
                cosmos.Freeze();
                dc.DrawRectangle(cosmos, null, new Rect(-w * .5, top, w, h));
                Brush star = FrozenBrush(Color.FromArgb(220, 255, 255, 255));
                foreach (var (sx, sy, sr) in new[] { (-w * .32, top + 7, 1.3), (w * .28, top + 10, 1.1), (-w * .22, bottom - 8, 1.0), (w * .32, bottom - 10, 1.4), (4, top + 6, .9) })
                    dc.DrawEllipse(star, null, new Point(sx, sy), sr, sr);
                break;
            }
            case "tuxedo":
            {
                Color shirt = darkBody ? Colors.White : Color.FromRgb(0xFF, 0xFD, 0xFA);
                Brush suit = FrozenBrush(darkBody ? Color.FromRgb(0x0F, 0x17, 0x2A) : Color.FromRgb(0x1F, 0x29, 0x37));
                dc.DrawPolygon(FrozenBrush(shirt), null,
                    new Point(-11, top + h * .56), new Point(11, top + h * .56), new Point(0, bottom));
                dc.DrawPolygon(suit, null,
                    new Point(-w * .5, top + h * .54), new Point(-10, top + h * .56), new Point(-3, bottom), new Point(-w * .5, bottom));
                dc.DrawPolygon(suit, null,
                    new Point(w * .5, top + h * .54), new Point(10, top + h * .56), new Point(3, bottom), new Point(w * .5, bottom));
                break;
            }
            case "grid":
            {
                Pen linePen = FrozenPen(Color.FromArgb(75, accent.R, accent.G, accent.B), 1.0);
                dc.DrawLine(linePen, new Point(-w * .5, top + h * .30), new Point(-w * .26, top + h * .30));
                dc.DrawLine(linePen, new Point(w * .26, top + h * .30), new Point(w * .5, top + h * .30));
                dc.DrawLine(linePen, new Point(-w * .28, bottom - 9), new Point(w * .28, bottom - 9));
                dc.DrawLine(linePen, new Point(-w * .28, bottom - 9), new Point(-w * .20, bottom));
                dc.DrawLine(linePen, new Point(w * .28, bottom - 9), new Point(w * .20, bottom));
                break;
            }
            case "gradient":
                dc.DrawRectangle(
                    FrozenBrush(Color.FromArgb(24, topFill.R, topFill.G, topFill.B)), null,
                    new Rect(-w * .5, top, w, h * .3));
                break;
        }
    }

    private static void DrawEars(
        DrawingContext dc, string ears, double bodyTop, double bodyW,
        Color surface, Color accent, Color accentSoft, Color detail, Pen outline,
        double breathPhase, bool hovered, bool excited)
    {
        Brush outer = FrozenBrush(EffectPalette.Blend(surface, accentSoft, .42));
        Brush inner = FrozenBrush(EffectPalette.Blend(detail, accent, .32));
        double perk = hovered || excited ? 1.8 : Math.Sin(breathPhase) * .7;

        switch (ears)
        {
            case "cat":
                DrawTriangleEar(dc, new Point(-17, bodyTop + 6), new Point(-19, bodyTop - 10 - perk), new Point(-5, bodyTop + 1), outer, inner, outline);
                DrawTriangleEar(dc, new Point(5, bodyTop + 1), new Point(19, bodyTop - 10 - perk), new Point(17, bodyTop + 6), outer, inner, outline);
                break;
            case "fox":
                DrawTriangleEar(dc, new Point(-18, bodyTop + 7), new Point(-23, bodyTop - 13 - perk), new Point(-4, bodyTop + 1), outer, inner, outline);
                DrawTriangleEar(dc, new Point(4, bodyTop + 1), new Point(23, bodyTop - 13 - perk), new Point(18, bodyTop + 7), outer, inner, outline);
                break;
            case "bunny":
            {
                dc.PushTransform(new RotateTransform(-12 - perk, -11, bodyTop + 2));
                dc.DrawRoundedRectangle(outer, outline, new Rect(-16, bodyTop - 21, 9.5, 24), 4.8, 10);
                dc.DrawRoundedRectangle(inner, null, new Rect(-13.8, bodyTop - 17, 5.2, 16), 2.6, 7);
                dc.Pop();

                dc.PushTransform(new RotateTransform(14 + perk, 11, bodyTop + 2));
                dc.DrawRoundedRectangle(outer, outline, new Rect(6.5, bodyTop - 21, 9.5, 24), 4.8, 10);
                dc.DrawRoundedRectangle(inner, null, new Rect(8.6, bodyTop - 17, 5.2, 16), 2.6, 7);
                dc.Pop();
                break;
            }
            case "bear":
                dc.DrawEllipse(outer, outline, new Point(-16, bodyTop + 2), 7.2, 7.2);
                dc.DrawEllipse(inner, null, new Point(-16, bodyTop + 2), 3.9, 3.9);
                dc.DrawEllipse(outer, outline, new Point(16, bodyTop + 2), 7.2, 7.2);
                dc.DrawEllipse(inner, null, new Point(16, bodyTop + 2), 3.9, 3.9);
                break;
            case "devil":
            {
                Brush hornBrush = FrozenBrush(EffectPalette.Blend(accent, Color.FromRgb(0x99, 0x1B, 0x1B), .45));
                Pen hornPen = FrozenPen(EffectPalette.Blend(accent, Colors.Black, .35), 1.3);
                foreach (int s in new[] { -1, 1 })
                {
                    var g = new StreamGeometry();
                    using (var c = g.Open())
                    {
                        c.BeginFigure(new Point(s * 9, bodyTop + 2), true, true);
                        c.QuadraticBezierTo(new Point(s * 20, bodyTop - 3), new Point(s * 18, bodyTop - 13 - perk * .5), true, false);
                        c.QuadraticBezierTo(new Point(s * 13, bodyTop - 6), new Point(s * 14, bodyTop + 4), true, false);
                    }
                    g.Freeze();
                    dc.DrawGeometry(hornBrush, hornPen, g);
                }
                break;
            }
            case "antler":
            {
                Pen antPen = FrozenPen(Color.FromRgb(0x92, 0x5E, 0x34), 2.2);
                foreach (int s in new[] { -1, 1 })
                {
                    dc.DrawLine(antPen, new Point(s * 11, bodyTop + 1), new Point(s * 19, bodyTop - 14));
                    dc.DrawLine(antPen, new Point(s * 15, bodyTop - 6), new Point(s * 23, bodyTop - 9));
                    dc.DrawLine(antPen, new Point(s * 16, bodyTop - 9), new Point(s * 13, bodyTop - 15));
                }
                break;
            }
            case "fin":
            {
                Brush frill = FrozenBrush(EffectPalette.Blend(detail, accent, .3));
                foreach (int s in new[] { -1, 1 })
                {
                    double x0 = s * (bodyW * .46);
                    for (int i = 0; i < 3; i++)
                    {
                        double y = bodyTop + 10 + i * 5.2;
                        dc.PushTransform(new RotateTransform(s * (-18 + i * 16), x0, y));
                        dc.DrawEllipse(frill, outline, new Point(x0 + s * 6, y), 6.2, 2.7);
                        dc.Pop();
                    }
                }
                break;
            }
            case "floppy":
            {
                Brush flop = FrozenBrush(EffectPalette.Blend(surface, accent, .34));
                foreach (int s in new[] { -1, 1 })
                {
                    dc.PushTransform(new RotateTransform(s * (14 + perk * 1.4), s * (bodyW * .42), bodyTop + 6));
                    dc.DrawRoundedRectangle(flop, outline, new Rect(s < 0 ? -bodyW * .5 - 6 : bodyW * .5 - 3, bodyTop + 5, 9, 20), 4.5, 9);
                    dc.Pop();
                }
                break;
            }
            case "elf":
            {
                foreach (int s in new[] { -1, 1 })
                {
                    double x0 = s * (bodyW * .44);
                    dc.DrawPolygon(outer, outline,
                        new Point(x0, bodyTop + 9),
                        new Point(s * (bodyW * .5 + 14), bodyTop + 3 - perk * .5),
                        new Point(x0, bodyTop + 16));
                }
                break;
            }
        }
    }

    private static void DrawTriangleEar(DrawingContext dc, Point a, Point apex, Point b, Brush outer, Brush inner, Pen pen)
    {
        dc.DrawPolygon(outer, pen, a, apex, b);
        var center = new Point((a.X + apex.X + b.X) / 3.0, (a.Y + apex.Y + b.Y) / 3.0);
        Point Inset(Point p) => new(center.X + (p.X - center.X) * .52, center.Y + (p.Y - center.Y) * .52);
        dc.DrawPolygon(inner, null, Inset(a), Inset(apex), Inset(b));
    }

    private static void DrawMechaFin(DrawingContext dc, double bodyTop, Color accent, Pen outline)
    {
        Pen finPen = FrozenPen(Color.FromRgb(0xF5, 0x9E, 0x0B), 2.2);
        dc.DrawLine(finPen, new Point(0, bodyTop + 1), new Point(-17, bodyTop - 10));
        dc.DrawLine(finPen, new Point(0, bodyTop + 1), new Point(17, bodyTop - 10));
        dc.DrawLine(finPen, new Point(0, bodyTop + 2), new Point(-11, bodyTop - 4));
        dc.DrawLine(finPen, new Point(0, bodyTop + 2), new Point(11, bodyTop - 4));
        dc.DrawPolygon(FrozenBrush(EffectPalette.Blend(accent, Color.FromRgb(0x38, 0xBD, 0xF8), .5)), outline,
            new Point(0, bodyTop - 5), new Point(4, bodyTop), new Point(0, bodyTop + 5), new Point(-4, bodyTop));
    }

    private static void DrawFeet(
        DrawingContext dc, string feet, double bodyBottom, Color surface, Color accent, Color detail,
        Pen outline, double stepPhase, bool isRoaming, double breathPhase)
    {
        if (feet == FloatingPetStyle.None) return;
        double leftStep = isRoaming ? Math.Max(0, Math.Sin(stepPhase)) * 3.2 : 0;
        double rightStep = isRoaming ? Math.Max(0, -Math.Sin(stepPhase)) * 3.2 : 0;

        switch (feet)
        {
            case "hover":
            {
                double pulse = 1.0 + Math.Sin(breathPhase * 2.2) * .12;
                Brush glow = FrozenBrush(Color.FromArgb(95, accent.R, accent.G, accent.B));
                dc.DrawEllipse(glow, FrozenPen(Color.FromArgb(170, detail.R, detail.G, detail.B), 1.2),
                    new Point(0, bodyBottom + 3.2), 14 * pulse, 3.0);
                break;
            }
            case "boots":
            {
                Brush boot = FrozenBrush(EffectPalette.Blend(Color.FromRgb(0x3B, 0x28, 0x1E), accent, .2));
                Brush cuff = FrozenBrush(detail);
                dc.DrawRoundedRectangle(boot, outline, new Rect(-14, bodyBottom - 3.5 - leftStep, 10, 7.5), 3, 3);
                dc.DrawRoundedRectangle(boot, outline, new Rect(4, bodyBottom - 3.5 - rightStep, 10, 7.5), 3, 3);
                dc.DrawRectangle(cuff, null, new Rect(-13.5, bodyBottom - 3.5 - leftStep, 9, 2));
                dc.DrawRectangle(cuff, null, new Rect(4.5, bodyBottom - 3.5 - rightStep, 9, 2));
                break;
            }
            case "sneakers":
            {
                Brush shoe = FrozenBrush(accent);
                Brush sole = FrozenBrush(Colors.White);
                dc.DrawRoundedRectangle(shoe, outline, new Rect(-14.5, bodyBottom - 3 - leftStep, 11, 7), 3.5, 3.5);
                dc.DrawRoundedRectangle(shoe, outline, new Rect(3.5, bodyBottom - 3 - rightStep, 11, 7), 3.5, 3.5);
                dc.DrawRoundedRectangle(sole, null, new Rect(-14, bodyBottom + 1.5 - leftStep, 10, 2.2), 1, 1);
                dc.DrawRoundedRectangle(sole, null, new Rect(4, bodyBottom + 1.5 - rightStep, 10, 2.2), 1, 1);
                break;
            }
            case "wheels":
            {
                Brush tire = FrozenBrush(Color.FromRgb(0x1F, 0x29, 0x37));
                Brush hub = FrozenBrush(detail);
                foreach (double wx in new[] { -9.5, 9.5 })
                {
                    double wy = bodyBottom + 1.2 - (wx < 0 ? leftStep : rightStep) * .4;
                    dc.DrawEllipse(tire, outline, new Point(wx, wy), 5.2, 5.2);
                    dc.DrawEllipse(hub, null, new Point(wx, wy), 2.3, 2.3);
                }
                break;
            }
            default: // "paws"
            {
                Brush footBrush = FrozenBrush(EffectPalette.Blend(surface, accent, .28));
                dc.DrawRoundedRectangle(footBrush, outline, new Rect(-13, bodyBottom - 3 - leftStep, 9, 6.5), 3.2, 3.2);
                dc.DrawRoundedRectangle(footBrush, outline, new Rect(4, bodyBottom - 3 - rightStep, 9, 6.5), 3.2, 3.2);
                break;
            }
        }
    }

    private static void DrawBackAccessory(
        DrawingContext dc, string back, double cx, double bodyBottom, double bodyTop, double bodyW, double bodyH,
        Color accent, Color accentSoft, Color surface, Color detail, Pen outline,
        double breathPhase, double hopArc, bool isRoaming, double facing)
    {
        if (back == FloatingPetStyle.None) return;
        double flap = Math.Sin(breathPhase * (hopArc > 1 || isRoaming ? 4.4 : 2.1)) * (hopArc > 1 ? 15 : 8);
        double midY = bodyTop + bodyH * .54;

        switch (back)
        {
            case "angel":
            {
                Brush wing = FrozenBrush(Color.FromArgb(240, 255, 255, 255));
                Pen wPen = FrozenPen(EffectPalette.Blend(accent, Color.FromRgb(0x94, 0xA3, 0xB8), .45), 1.3);
                foreach (int s in new[] { -1, 1 })
                {
                    dc.PushTransform(new RotateTransform(s * (-8 - flap), s * bodyW * .38, midY));
                    var g = new StreamGeometry();
                    using (var c = g.Open())
                    {
                        c.BeginFigure(new Point(s * bodyW * .35, midY), true, true);
                        c.BezierTo(new Point(s * (bodyW * .5 + 16), midY - 16), new Point(s * (bodyW * .5 + 18), midY - 2), new Point(s * (bodyW * .5 + 9), midY + 4), true, false);
                        c.QuadraticBezierTo(new Point(s * (bodyW * .5 + 11), midY + 10), new Point(s * bodyW * .35, midY + 7), true, false);
                    }
                    g.Freeze();
                    dc.DrawGeometry(wing, wPen, g);
                    dc.Pop();
                }
                break;
            }
            case "bat":
            {
                Brush wing = FrozenBrush(Color.FromRgb(0x27, 0x20, 0x30));
                foreach (int s in new[] { -1, 1 })
                {
                    dc.PushTransform(new RotateTransform(s * (-flap), s * bodyW * .38, midY));
                    dc.DrawPolygon(wing, outline,
                        new Point(s * bodyW * .36, midY),
                        new Point(s * (bodyW * .5 + 16), midY - 13),
                        new Point(s * (bodyW * .5 + 13), midY + 5),
                        new Point(s * (bodyW * .5 + 6), midY + 2),
                        new Point(s * bodyW * .36, midY + 7));
                    dc.Pop();
                }
                break;
            }
            case "fairy":
            {
                Brush fTop = FrozenBrush(Color.FromArgb(165, detail.R, detail.G, detail.B));
                Brush fBot = FrozenBrush(Color.FromArgb(145, accentSoft.R, accentSoft.G, accentSoft.B));
                Pen fPen = FrozenPen(EffectPalette.Blend(detail, Colors.White, .4), 1.1);
                foreach (int s in new[] { -1, 1 })
                {
                    dc.PushTransform(new RotateTransform(s * (-flap * 1.1), s * bodyW * .36, midY));
                    dc.DrawEllipse(fTop, fPen, new Point(s * (bodyW * .5 + 9), midY - 7), 11, 7.5);
                    dc.DrawEllipse(fBot, fPen, new Point(s * (bodyW * .5 + 6), midY + 4), 7.5, 5.2);
                    dc.Pop();
                }
                break;
            }
            case "mecha":
            {
                Brush b1 = FrozenBrush(Color.FromRgb(0x1E, 0x3A, 0x8A));
                Brush b2 = FrozenBrush(Color.FromRgb(0xE1, 0x1D, 0x48));
                foreach (int s in new[] { -1, 1 })
                {
                    dc.PushTransform(new RotateTransform(s * (-flap * .6), s * bodyW * .36, midY));
                    dc.DrawPolygon(b1, outline,
                        new Point(s * bodyW * .36, midY - 2),
                        new Point(s * (bodyW * .5 + 19), midY - 14),
                        new Point(s * (bodyW * .5 + 14), midY + 7),
                        new Point(s * bodyW * .36, midY + 5));
                    dc.DrawPolygon(b2, null,
                        new Point(s * bodyW * .42, midY + 1),
                        new Point(s * (bodyW * .5 + 11), midY - 4),
                        new Point(s * (bodyW * .5 + 8), midY + 5));
                    dc.Pop();
                }
                break;
            }
            case "jetpack":
            {
                Brush metal = FrozenBrush(Color.FromRgb(0x64, 0x74, 0x8B));
                Brush trim = FrozenBrush(accent);
                foreach (int s in new[] { -1, 1 })
                {
                    double jx = s * (bodyW * .5 + 2) - 4;
                    dc.DrawRoundedRectangle(metal, outline, new Rect(jx, midY - 7, 8, 16), 3, 3);
                    dc.DrawRectangle(trim, null, new Rect(jx + 1.5, midY - 2, 5, 3));
                    if (hopArc > 1 || isRoaming)
                    {
                        double fl = 6 + Math.Abs(Math.Sin(breathPhase * 6)) * 5;
                        dc.DrawPolygon(FrozenBrush(Color.FromRgb(0xFB, 0x92, 0x3C)), null,
                            new Point(jx + 1.5, midY + 9), new Point(jx + 4, midY + 9 + fl), new Point(jx + 6.5, midY + 9));
                    }
                }
                break;
            }
            case "cape":
            {
                Brush cape = FrozenBrush(EffectPalette.Blend(accent, detail, .25));
                double billow = Math.Sin(breathPhase * 1.9) * 2.5 + (isRoaming ? -facing * 4.5 : 0);
                dc.DrawPolygon(cape, outline,
                    new Point(-bodyW * .34, bodyTop + 12),
                    new Point(bodyW * .34, bodyTop + 12),
                    new Point(bodyW * .52 + billow, bodyBottom + 2),
                    new Point(-bodyW * .52 + billow, bodyBottom + 2));
                break;
            }
            case "tail":
            {
                double s = -facing;
                double wag = Math.Sin(breathPhase * 2.6) * 10;
                Brush tailBrush = FrozenBrush(EffectPalette.Blend(surface, accent, .28));
                Brush tipBrush = FrozenBrush(EffectPalette.Blend(detail, Colors.White, .35));
                dc.PushTransform(new RotateTransform(wag, s * bodyW * .42, bodyBottom - 8));
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    c.BeginFigure(new Point(s * bodyW * .38, bodyBottom - 6), true, true);
                    c.BezierTo(new Point(s * (bodyW * .5 + 16), bodyBottom - 6), new Point(s * (bodyW * .5 + 17), bodyBottom - 23), new Point(s * (bodyW * .5 + 7), bodyBottom - 20), true, false);
                    c.QuadraticBezierTo(new Point(s * (bodyW * .5 + 4), bodyBottom - 11), new Point(s * bodyW * .38, bodyBottom - 11), true, false);
                }
                g.Freeze();
                dc.DrawGeometry(tailBrush, outline, g);
                dc.DrawEllipse(tipBrush, null, new Point(s * (bodyW * .5 + 11), bodyBottom - 18), 3.4, 3.0);
                dc.Pop();
                break;
            }
            case "shell":
            {
                Brush shell = FrozenBrush(EffectPalette.Blend(accent, Color.FromRgb(0x16, 0xA3, 0x4A), .45));
                Brush rim = FrozenBrush(EffectPalette.Blend(detail, Color.FromRgb(0xFE, 0xF0, 0x8A), .55));
                dc.DrawEllipse(shell, outline, new Point(-facing * 3, bodyTop + bodyH * .56), bodyW * .54, bodyH * .44);
                dc.DrawEllipse(null, FrozenPen(EffectPalette.Blend(accent, Colors.Black, .2), 1.3), new Point(-facing * 3, bodyTop + bodyH * .56), bodyW * .38, bodyH * .28);
                dc.DrawRoundedRectangle(rim, outline, new Rect(-bodyW * .52, bodyBottom - 7, bodyW * 1.04, 5), 2.5, 2.5);
                break;
            }
            case "shark":
            {
                double s = -facing;
                Brush fin = FrozenBrush(EffectPalette.Blend(accent, Color.FromRgb(0x47, 0x55, 0x69), .35));
                dc.DrawPolygon(fin, outline,
                    new Point(s * bodyW * .25, bodyTop + 16),
                    new Point(s * (bodyW * .5 + 13), bodyTop + 2),
                    new Point(s * (bodyW * .5 + 6), bodyTop + 24),
                    new Point(s * bodyW * .25, bodyTop + 28));
                break;
            }
        }
    }

    private static void DrawOutfit(
        DrawingContext dc, string outfit, double bodyBottom, double bodyW, double bodyH,
        Color surface, Color accent, Color accentSoft, Color detail, Color ink, Pen outline, double facing)
    {
        double neckY = bodyBottom - bodyH * .23;
        switch (outfit)
        {
            case "bowtie":
            {
                Brush bow = FrozenBrush(detail);
                dc.DrawPolygon(bow, outline, new Point(-7, neckY - 3.5), new Point(0, neckY), new Point(-7, neckY + 3.5));
                dc.DrawPolygon(bow, outline, new Point(7, neckY - 3.5), new Point(0, neckY), new Point(7, neckY + 3.5));
                dc.DrawEllipse(FrozenBrush(accent), null, new Point(0, neckY), 2.3, 2.3);
                break;
            }
            case "scarf":
            {
                Brush s1 = FrozenBrush(detail);
                Brush s2 = FrozenBrush(accent);
                dc.DrawRoundedRectangle(s1, outline, new Rect(-bodyW * .38, neckY - 3.5, bodyW * .76, 6.2), 3, 3);
                dc.DrawRoundedRectangle(s2, outline, new Rect(facing * 5 - 3.5, neckY + 1, 7, 10), 2.5, 2.5);
                break;
            }
            case "hoodie":
            {
                Pen stringPen = FrozenPen(ink, 1.4);
                dc.DrawLine(stringPen, new Point(-4.5, neckY - 2), new Point(-4.5, bodyBottom - 3.5));
                dc.DrawLine(stringPen, new Point(4.5, neckY - 2), new Point(4.5, bodyBottom - 3.5));
                dc.DrawRoundedRectangle(FrozenBrush(Color.FromArgb(75, accent.R, accent.G, accent.B)), outline,
                    new Rect(-11, bodyBottom - 9.5, 22, 8), 3.5, 3.5);
                break;
            }
            case "tie":
            {
                Brush tie = FrozenBrush(detail);
                dc.DrawPolygon(tie, outline,
                    new Point(-2.4, neckY - 2), new Point(2.4, neckY - 2), new Point(3.4, bodyBottom - 4),
                    new Point(0, bodyBottom - 1), new Point(-3.4, bodyBottom - 4));
                break;
            }
            case "star":
                DrawStarShape(dc, facing * 2, neckY + 2, 5.2, FrozenBrush(Color.FromRgb(0xFA, 0xCC, 0x15)), outline);
                break;
            case "heart":
                DrawHeartShape(dc, 0, neckY + 2, 5.2, FrozenBrush(detail), outline);
                break;
            case "arc":
                dc.DrawEllipse(FrozenBrush(Color.FromArgb(75, 0x38, 0xBD, 0xF8)), null, new Point(facing * 1.2, neckY + 1), 6, 6);
                dc.DrawEllipse(FrozenBrush(Color.FromRgb(0x7D, 0xD3, 0xFC)), outline, new Point(facing * 1.2, neckY + 1), 3.1, 3.1);
                break;
            case "bib":
            {
                Brush bib = FrozenBrush(EffectPalette.Blend(accent, Colors.White, .25));
                dc.DrawRoundedRectangle(bib, outline, new Rect(-10, neckY - 2, 20, bodyBottom - neckY + 1), 3, 3);
                dc.DrawLine(FrozenPen(accent, 2.2), new Point(-8, neckY - 6), new Point(-7, neckY - 1));
                dc.DrawLine(FrozenPen(accent, 2.2), new Point(8, neckY - 6), new Point(7, neckY - 1));
                dc.DrawEllipse(FrozenBrush(detail), null, new Point(-7, neckY - .5), 1.4, 1.4);
                dc.DrawEllipse(FrozenBrush(detail), null, new Point(7, neckY - .5), 1.4, 1.4);
                break;
            }
            case "armor":
            {
                Brush steel = FrozenBrush(Color.FromRgb(0xCB, 0xD5, 0xE1));
                Pen steelPen = FrozenPen(Color.FromRgb(0x47, 0x55, 0x69), 1.3);
                dc.DrawPolygon(steel, steelPen,
                    new Point(-14, neckY - 2), new Point(14, neckY - 2),
                    new Point(10, bodyBottom - 2), new Point(0, bodyBottom + 1), new Point(-10, bodyBottom - 2));
                dc.DrawLine(FrozenPen(detail, 2.0), new Point(-8, neckY + 2), new Point(8, neckY + 2));
                break;
            }
        }
    }

    private static void DrawCheeks(
        DrawingContext dc, string cheeks, double lookX, double eyeY, double eyeSep,
        Color accent, Color detail, Color featureInk, bool hovered, bool excited)
    {
        if (cheeks == FloatingPetStyle.None) return;
        byte alpha = (byte)(hovered || excited ? 140 : 85);
        Color c = EffectPalette.Blend(accent, Color.FromRgb(0xF4, 0x72, 0xB6), .55);
        Brush b = FrozenBrush(Color.FromArgb(alpha, c.R, c.G, c.B));
        double lx = lookX - eyeSep - 3.6, rx = lookX + eyeSep + 3.6, cy = eyeY + 4.8;

        switch (cheeks)
        {
            case "freckles":
            {
                Brush f = FrozenBrush(Color.FromArgb(165, 0xB4, 0x53, 0x09));
                foreach (double sx in new[] { lx, rx })
                {
                    dc.DrawEllipse(f, null, new Point(sx - 1.4, cy), 1, 1);
                    dc.DrawEllipse(f, null, new Point(sx + 1.5, cy - .8), 1, 1);
                    dc.DrawEllipse(f, null, new Point(sx + .4, cy + 1.4), 1, 1);
                }
                break;
            }
            case "whiskers":
            {
                Pen wPen = FrozenPen(Color.FromArgb(170, featureInk.R, featureInk.G, featureInk.B), 1.15);
                foreach (int s in new[] { -1, 1 })
                {
                    double wx = lookX + s * (eyeSep + 3.5);
                    dc.DrawLine(wPen, new Point(wx, cy - 1), new Point(wx + s * 6.5, cy - 2.2));
                    dc.DrawLine(wPen, new Point(wx, cy + 1.5), new Point(wx + s * 7.0, cy + 2.2));
                }
                break;
            }
            case "stripes":
            {
                Pen sp = FrozenPen(Color.FromArgb(160, c.R, c.G, c.B), 1.25);
                foreach (double sx in new[] { lx, rx })
                    for (int i = -1; i <= 1; i++)
                        dc.DrawLine(sp, new Point(sx + i * 2.2 - 1, cy + 1.6), new Point(sx + i * 2.2 + 1, cy - 1.6));
                break;
            }
            case "hearts":
                DrawHeartShape(dc, lx, cy, 2.7, b, null);
                DrawHeartShape(dc, rx, cy, 2.7, b, null);
                break;
            case "stars":
                DrawStarShape(dc, lx, cy, 2.9, FrozenBrush(detail), null);
                DrawStarShape(dc, rx, cy, 2.9, FrozenBrush(detail), null);
                break;
            default: // "blush"
                dc.DrawEllipse(b, null, new Point(lx, cy), 3.8, 2.2);
                dc.DrawEllipse(b, null, new Point(rx, cy), 3.8, 2.2);
                break;
        }
    }

    private static void DrawEyes(
        DrawingContext dc, string eyes, double lookX, double eyeY, double eyeSep,
        Color accent, Color detail, Color featureInk, Color surface,
        double blinkProgress, bool hovered, bool excited, bool sleepy)
    {
        Pen featurePen = FrozenPen(featureInk, 1.85);
        Brush inkBrush = FrozenBrush(featureInk);
        bool happyState = excited || (hovered && !sleepy);

        if (eyes == "visor")
        {
            dc.DrawRoundedRectangle(FrozenBrush(Color.FromRgb(0x0F, 0x17, 0x2A)), FrozenPen(accent, 1.2),
                new Rect(lookX - 14.5, eyeY - 4.2, 29, 8.4), 4.2, 4.2);
            Brush glow = FrozenBrush(detail);
            if (happyState)
            {
                Pen gp = FrozenPen(detail, 1.7);
                DrawHappyEye(dc, lookX - eyeSep * .8, eyeY, gp);
                DrawHappyEye(dc, lookX + eyeSep * .8, eyeY, gp);
            }
            else if (sleepy || blinkProgress > .45)
                dc.DrawLine(FrozenPen(detail, 1.6), new Point(lookX - 10, eyeY), new Point(lookX + 10, eyeY));
            else
                dc.DrawRoundedRectangle(glow, null, new Rect(lookX - 10.5, eyeY - 1.5, 21, 3), 1.5, 1.5);
            return;
        }

        if (happyState)
        {
            DrawHappyEye(dc, lookX - eyeSep, eyeY, featurePen);
            DrawHappyEye(dc, lookX + eyeSep, eyeY, featurePen);
            return;
        }
        if (sleepy || blinkProgress > .45)
        {
            DrawClosedEye(dc, lookX - eyeSep, eyeY + .5, featurePen);
            DrawClosedEye(dc, lookX + eyeSep, eyeY + .5, featurePen);
            return;
        }

        double openY = Math.Max(.2, 1.0 - blinkProgress);
        Brush shine = FrozenBrush(Color.FromArgb(230, 255, 255, 255));

        foreach (int side in new[] { -1, 1 })
        {
            double ex = lookX + side * eyeSep;
            if (eyes == "wink" && side == 1)
            {
                DrawHappyEye(dc, ex, eyeY, featurePen);
                continue;
            }
            switch (eyes)
            {
                case "sparkle":
                {
                    dc.DrawEllipse(inkBrush, null, new Point(ex, eyeY), 3.5, 4.3 * openY);
                    dc.DrawEllipse(FrozenBrush(accent), null, new Point(ex, eyeY + 1.0), 2.5, 2.3 * openY);
                    dc.DrawEllipse(FrozenBrush(detail), null, new Point(ex, eyeY + 1.4), 1.3, 1.2 * openY);
                    if (openY > .5)
                    {
                        dc.DrawEllipse(shine, null, new Point(ex - 1.1, eyeY - 1.4), 1.3, 1.3);
                        dc.DrawLine(featurePen, new Point(ex + side * 2.5, eyeY - 3.2), new Point(ex + side * 4.5, eyeY - 4.3));
                    }
                    break;
                }
                case "sleepy":
                    DrawClosedEye(dc, ex, eyeY + .5, featurePen);
                    break;
                case "big":
                    dc.DrawEllipse(inkBrush, null, new Point(ex, eyeY), 3.6, 4.4 * openY);
                    dc.DrawEllipse(FrozenBrush(detail), null, new Point(ex, eyeY + 1.0), 2.3, 2.2 * openY);
                    if (openY > .5)
                    {
                        dc.DrawEllipse(shine, null, new Point(ex - 1.1, eyeY - 1.5), 1.3, 1.3);
                        dc.DrawEllipse(shine, null, new Point(ex + 1.0, eyeY + .9), .75, .75);
                    }
                    break;
                case "cat":
                    dc.DrawEllipse(FrozenBrush(detail), FrozenPen(featureInk, 1.1), new Point(ex, eyeY), 3.2, 3.6 * openY);
                    dc.DrawEllipse(inkBrush, null, new Point(ex, eyeY), 1.1, 2.9 * openY);
                    break;
                case "star":
                    DrawStarShape(dc, ex, eyeY, 4.3 * openY, FrozenBrush(detail), FrozenPen(featureInk, .9));
                    if (openY > .5) dc.DrawEllipse(shine, null, new Point(ex - .8, eyeY - .9), .9, .9);
                    break;
                case "heart":
                    DrawHeartShape(dc, ex, eyeY, 3.8 * openY, FrozenBrush(detail), FrozenPen(featureInk, .85));
                    break;
                case "chill":
                    dc.DrawEllipse(FrozenBrush(surface), null, new Point(ex, eyeY), 3.0, 2.8);
                    dc.DrawEllipse(inkBrush, null, new Point(ex, eyeY + .6), 2.4, 2.1 * openY);
                    dc.DrawLine(featurePen, new Point(ex - 3.4, eyeY - .8), new Point(ex + 3.4, eyeY - .8));
                    break;
                case "spiral":
                {
                    Pen sp = FrozenPen(featureInk, 1.3);
                    dc.DrawEllipse(null, sp, new Point(ex, eyeY), 3.4, 3.4 * openY);
                    dc.DrawEllipse(null, sp, new Point(ex + .4, eyeY), 1.6, 1.6 * openY);
                    break;
                }
                case "button":
                    dc.DrawEllipse(null, FrozenPen(featureInk, 1.3), new Point(ex, eyeY), 3.4, 3.4 * openY);
                    dc.DrawLine(featurePen, new Point(ex - 1.6, eyeY - 1.6), new Point(ex + 1.6, eyeY + 1.6));
                    dc.DrawLine(featurePen, new Point(ex + 1.6, eyeY - 1.6), new Point(ex - 1.6, eyeY + 1.6));
                    break;
                default: // "dot"
                    dc.DrawEllipse(inkBrush, null, new Point(ex, eyeY), 2.7, 3.5 * openY);
                    if (openY > .55) dc.DrawEllipse(shine, null, new Point(ex - .9, eyeY - 1.2), 1.0, 1.0);
                    break;
            }
        }
    }

    private static void DrawBrows(DrawingContext dc, string brows, double lookX, double browY, double eyeSep, Color featureInk)
    {
        Pen p = FrozenPen(featureInk, brows == "thick" ? 2.5 : 1.5);
        double lx = lookX - eyeSep, rx = lookX + eyeSep;
        switch (brows)
        {
            case "soft":
                DrawHappyEye(dc, lx, browY + 1, FrozenPen(featureInk, 1.35));
                DrawHappyEye(dc, rx, browY + 1, FrozenPen(featureInk, 1.35));
                break;
            case "happy":
                DrawHappyEye(dc, lx, browY - .4, FrozenPen(featureInk, 1.6));
                DrawHappyEye(dc, rx, browY - .4, FrozenPen(featureInk, 1.6));
                break;
            case "focused":
                dc.DrawLine(p, new Point(lx - 3.2, browY - 1.5), new Point(lx + 3.2, browY + 1.4));
                dc.DrawLine(p, new Point(rx - 3.2, browY + 1.4), new Point(rx + 3.2, browY - 1.5));
                break;
            case "worried":
                dc.DrawLine(p, new Point(lx - 3.2, browY + 1.5), new Point(lx + 3.2, browY - 1.4));
                dc.DrawLine(p, new Point(rx - 3.2, browY - 1.4), new Point(rx + 3.2, browY + 1.5));
                break;
            case "unibrow":
                dc.DrawLine(FrozenPen(featureInk, 2.2), new Point(lx - 4, browY), new Point(lookX, browY + 1.3));
                dc.DrawLine(FrozenPen(featureInk, 2.2), new Point(lookX, browY + 1.3), new Point(rx + 4, browY));
                break;
            case "thick":
                dc.DrawLine(p, new Point(lx - 3.6, browY), new Point(lx + 3.6, browY));
                dc.DrawLine(p, new Point(rx - 3.6, browY), new Point(rx + 3.6, browY));
                break;
        }
    }

    private static void DrawMouth(
        DrawingContext dc, string mouth, double cx, double cy, Color ink, Color detail,
        bool hovered, bool excited, bool sleepy, double blinkProgress)
    {
        if (mouth == FloatingPetStyle.None) return;
        Pen pen = FrozenPen(ink, 1.7);
        if (sleepy && blinkProgress <= .45)
        {
            dc.DrawEllipse(null, FrozenPen(ink, 1.35), new Point(cx, cy + 1.1), 1.9, 1.6);
            return;
        }
        bool openHappy = hovered || excited;

        switch (mouth)
        {
            case "cat":
                DrawSmile(dc, cx - 2.3, cy - .3, 2.3, 2.2, pen);
                DrawSmile(dc, cx + 2.3, cy - .3, 2.3, 2.2, pen);
                break;
            case "grin":
            {
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    c.BeginFigure(new Point(cx - 4.8, cy - .6), true, true);
                    c.LineTo(new Point(cx + 4.8, cy - .6), true, false);
                    c.QuadraticBezierTo(new Point(cx, cy + 6.2), new Point(cx - 4.8, cy - .6), true, false);
                }
                g.Freeze();
                dc.DrawGeometry(FrozenBrush(Color.FromRgb(0xF4, 0x3F, 0x5E)), pen, g);
                break;
            }
            case "fang":
                DrawSmile(dc, cx, cy, 4.4, 2.1, pen);
                dc.DrawPolygon(FrozenBrush(Colors.White), FrozenPen(ink, .85),
                    new Point(cx + 1.6, cy + .8), new Point(cx + 3.2, cy + 3.2), new Point(cx + 3.8, cy + .4));
                break;
            case "blep":
                DrawSmile(dc, cx, cy - .4, 4.1, 1.8, pen);
                dc.DrawRoundedRectangle(FrozenBrush(Color.FromRgb(0xFB, 0x71, 0x85)), FrozenPen(ink, 1.0),
                    new Rect(cx - 2.1, cy + .5, 4.2, 4.2), 2.1, 2.1);
                break;
            case "smirk":
            {
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    c.BeginFigure(new Point(cx - 4.0, cy + .8), false, false);
                    c.QuadraticBezierTo(new Point(cx + 1.2, cy + 2.4), new Point(cx + 4.6, cy - 1.2), true, false);
                }
                g.Freeze();
                dc.DrawGeometry(null, pen, g);
                break;
            }
            case "whistle":
                dc.DrawEllipse(FrozenBrush(detail), pen, new Point(cx, cy + 1.0), 1.9, 2.2);
                break;
            case "kiss":
            {
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    c.BeginFigure(new Point(cx - 2.0, cy - .6), false, false);
                    c.QuadraticBezierTo(new Point(cx + 2.4, cy), new Point(cx - 1.2, cy + 1.2), true, false);
                    c.QuadraticBezierTo(new Point(cx + 2.4, cy + 2.2), new Point(cx - 2.0, cy + 3.0), true, false);
                }
                g.Freeze();
                dc.DrawGeometry(null, pen, g);
                break;
            }
            case "beak":
                dc.DrawPolygon(FrozenBrush(Color.FromRgb(0xFB, 0x92, 0x3C)), FrozenPen(Color.FromRgb(0xC2, 0x41, 0x0C), 1.1),
                    new Point(cx - 3.6, cy - .4), new Point(cx + 3.6, cy - .4), new Point(cx, cy + 4.2));
                break;
            case "mustache":
            {
                Brush stache = FrozenBrush(ink);
                foreach (int s in new[] { -1, 1 })
                {
                    var g = new StreamGeometry();
                    using (var c = g.Open())
                    {
                        c.BeginFigure(new Point(cx, cy - .2), true, true);
                        c.BezierTo(new Point(cx + s * 4, cy - 3), new Point(cx + s * 8, cy + 1), new Point(cx + s * 6.5, cy + 2.8), true, false);
                        c.QuadraticBezierTo(new Point(cx + s * 3, cy + 1.5), new Point(cx, cy + 1.2), true, false);
                    }
                    g.Freeze();
                    dc.DrawGeometry(stache, null, g);
                }
                break;
            }
            default: // "smile"
                DrawSmile(dc, cx, cy, openHappy ? 5.2 : 4.2, openHappy ? 3.1 : 2.1, pen);
                break;
        }
    }

    private static void DrawGlasses(
        DrawingContext dc, string glasses, double lookX, double eyeY, double eyeSep,
        Color accent, Color detail, Color featureInk, double facing)
    {
        double lx = lookX - eyeSep, rx = lookX + eyeSep;
        switch (glasses)
        {
            case "round":
            {
                Pen gp = FrozenPen(featureInk, 1.45);
                dc.DrawEllipse(FrozenBrush(Color.FromArgb(26, 255, 255, 255)), gp, new Point(lx, eyeY), 5.6, 5.6);
                dc.DrawEllipse(FrozenBrush(Color.FromArgb(26, 255, 255, 255)), gp, new Point(rx, eyeY), 5.6, 5.6);
                dc.DrawLine(gp, new Point(lx + 5.6, eyeY - .5), new Point(rx - 5.6, eyeY - .5));
                break;
            }
            case "shades":
            {
                Brush lens = FrozenBrush(Color.FromArgb(235, 0x11, 0x18, 0x27));
                Pen rim = FrozenPen(accent, 1.2);
                dc.DrawRoundedRectangle(lens, rim, new Rect(lx - 6.6, eyeY - 4.2, 13.2, 8.6), 2.5, 4);
                dc.DrawRoundedRectangle(lens, rim, new Rect(rx - 6.6, eyeY - 4.2, 13.2, 8.6), 2.5, 4);
                dc.DrawLine(rim, new Point(lx + 6.4, eyeY - 1.5), new Point(rx - 6.4, eyeY - 1.5));
                Pen glint = FrozenPen(Color.FromArgb(145, 255, 255, 255), 1.3);
                dc.DrawLine(glint, new Point(lx - 3.5, eyeY - 2), new Point(lx - .5, eyeY + 2));
                dc.DrawLine(glint, new Point(rx - 3.5, eyeY - 2), new Point(rx - .5, eyeY + 2));
                break;
            }
            case "star":
            {
                Brush frame = FrozenBrush(detail);
                Pen rim = FrozenPen(featureInk, 1.0);
                DrawStarShape(dc, lx, eyeY, 6.2, frame, rim);
                DrawStarShape(dc, rx, eyeY, 6.2, frame, rim);
                dc.DrawLine(rim, new Point(lx + 4.5, eyeY - .8), new Point(rx - 4.5, eyeY - .8));
                break;
            }
            case "monocle":
            {
                Pen gold = FrozenPen(Color.FromRgb(0xF5, 0x9E, 0x0B), 1.6);
                dc.DrawEllipse(FrozenBrush(Color.FromArgb(30, 255, 255, 255)), gold, new Point(rx, eyeY), 5.8, 5.8);
                dc.DrawLine(FrozenPen(Color.FromRgb(0xF5, 0x9E, 0x0B), 1.1), new Point(rx + 5.2, eyeY + 2.5), new Point(rx + 6.5, eyeY + 12));
                break;
            }
            case "goggles":
            {
                Pen strap = FrozenPen(Color.FromRgb(0x47, 0x55, 0x69), 2.2);
                Pen brass = FrozenPen(Color.FromRgb(0xD9, 0x77, 0x06), 1.8);
                Brush tint = FrozenBrush(Color.FromArgb(115, 0x38, 0xBD, 0xF8));
                dc.DrawLine(strap, new Point(-18, eyeY - 1), new Point(18, eyeY - 1));
                dc.DrawEllipse(tint, brass, new Point(lx, eyeY), 6.2, 5.4);
                dc.DrawEllipse(tint, brass, new Point(rx, eyeY), 6.2, 5.4);
                break;
            }
            case "patch":
            {
                double px = facing >= 0 ? lx : rx;
                dc.DrawLine(FrozenPen(featureInk, 1.5), new Point(-17, eyeY - 5), new Point(17, eyeY + 1));
                dc.DrawEllipse(FrozenBrush(Color.FromRgb(0x18, 0x18, 0x1B)), FrozenPen(accent, 1.1), new Point(px, eyeY), 5.4, 5.0);
                break;
            }
        }
    }

    private static void DrawHeadwear(
        DrawingContext dc, string hat, double bodyTop, double bodyW,
        Color accent, Color accentSoft, Color surface, Color detail, Color signalColor, Pen outline,
        double breathPhase, double facing, bool isPineappleTheme, bool isOnline)
    {
        if (hat == FloatingPetStyle.None) return;
        string effectiveHat = hat == FloatingPetStyle.Auto ? "antenna" : hat;

        switch (effectiveHat)
        {
            case "antenna":
            {
                double tipX = facing * 2.2;
                double tipY = bodyTop - 12 + Math.Sin(breathPhase * 1.1) * 1.0;
                dc.DrawLine(outline, new Point(0, bodyTop + 1), new Point(tipX, tipY));
                if (hat == FloatingPetStyle.Auto && isPineappleTheme)
                    dc.DrawPolygon(FrozenBrush(Color.FromRgb(0x1F, 0x8A, 0x5B)), null, new Point(tipX - 5, tipY + 2), new Point(tipX, tipY - 5), new Point(tipX + 5, tipY + 2));
                double pulse = isOnline ? (.42 + .22 * Math.Sin(breathPhase * 1.8)) : .18;
                dc.DrawEllipse(FrozenBrush(Color.FromArgb((byte)Math.Round(120 * pulse), signalColor.R, signalColor.G, signalColor.B)), null, new Point(tipX, tipY), 6.5, 6.5);
                dc.DrawEllipse(FrozenBrush(signalColor), FrozenPen(Colors.White, .9), new Point(tipX, tipY), 3.3, 3.3);
                break;
            }
            case "sprout":
            {
                double sway = Math.Sin(breathPhase * 1.4) * 2.0;
                Pen stem = FrozenPen(Color.FromRgb(0x16, 0xA3, 0x4A), 1.9);
                dc.DrawLine(stem, new Point(0, bodyTop + 1), new Point(sway, bodyTop - 10));
                Brush leaf = FrozenBrush(Color.FromRgb(0x22, 0xC5, 0x5E));
                dc.PushTransform(new RotateTransform(-26, sway, bodyTop - 10));
                dc.DrawEllipse(leaf, FrozenPen(Color.FromRgb(0x15, 0x80, 0x3D), 1), new Point(sway - 5, bodyTop - 10), 5.2, 2.7);
                dc.Pop();
                dc.PushTransform(new RotateTransform(26, sway, bodyTop - 10));
                dc.DrawEllipse(leaf, FrozenPen(Color.FromRgb(0x15, 0x80, 0x3D), 1), new Point(sway + 5, bodyTop - 10), 5.2, 2.7);
                dc.Pop();
                break;
            }
            case "halo":
            {
                double fy = bodyTop - 9 + Math.Sin(breathPhase * 1.5) * 1.6;
                dc.DrawEllipse(null, FrozenPen(Color.FromArgb(90, 0xFA, 0xCC, 0x15), 4.2), new Point(0, fy), 12.5, 3.8);
                dc.DrawEllipse(null, FrozenPen(Color.FromRgb(0xFD, 0xE0, 0x47), 2.1), new Point(0, fy), 12.5, 3.8);
                break;
            }
            case "crown":
            {
                Brush gold = FrozenBrush(Color.FromRgb(0xFB, 0xBF, 0x24));
                Pen goldPen = FrozenPen(Color.FromRgb(0xB4, 0x53, 0x09), 1.3);
                dc.DrawPolygon(gold, goldPen,
                    new Point(-11, bodyTop + 2), new Point(-13, bodyTop - 10), new Point(-5, bodyTop - 4),
                    new Point(0, bodyTop - 13), new Point(5, bodyTop - 4), new Point(13, bodyTop - 10), new Point(11, bodyTop + 2));
                dc.DrawEllipse(FrozenBrush(detail), null, new Point(0, bodyTop - 3), 2.2, 2.2);
                break;
            }
            case "cap":
            {
                Brush cap = FrozenBrush(accent);
                Brush bill = FrozenBrush(EffectPalette.Blend(accent, Colors.Black, .28));
                dc.DrawRoundedRectangle(cap, outline, new Rect(-15, bodyTop - 9, 30, 12), 12, 9);
                dc.DrawRoundedRectangle(bill, outline, new Rect(facing >= 0 ? -4 : -20, bodyTop - 1, 24, 4.5), 2.2, 2.2);
                dc.DrawEllipse(FrozenBrush(detail), outline, new Point(0, bodyTop - 9.5), 2.0, 1.5);
                break;
            }
            case "beanie":
            {
                Brush knit = FrozenBrush(accent);
                Brush cuff = FrozenBrush(accentSoft);
                dc.DrawRoundedRectangle(knit, outline, new Rect(-14, bodyTop - 12, 28, 15), 11, 11);
                dc.DrawRoundedRectangle(cuff, outline, new Rect(-15, bodyTop - 3, 30, 6), 3, 3);
                dc.DrawEllipse(FrozenBrush(detail), outline, new Point(0, bodyTop - 14), 4.2, 4.2);
                break;
            }
            case "wizard":
            {
                Brush hatBrush = FrozenBrush(EffectPalette.Blend(accent, Color.FromRgb(0x31, 0x2E, 0x81), .45));
                dc.DrawEllipse(hatBrush, outline, new Point(0, bodyTop + 1), 21, 5.2);
                dc.DrawPolygon(hatBrush, outline,
                    new Point(-13, bodyTop + 1), new Point(-3, bodyTop - 21), new Point(8, bodyTop - 17), new Point(13, bodyTop + 1));
                dc.DrawLine(FrozenPen(detail, 2.4), new Point(-11, bodyTop - 1.5), new Point(11, bodyTop - 1.5));
                DrawStarShape(dc, 0, bodyTop - 9, 2.7, FrozenBrush(Color.FromRgb(0xFD, 0xE0, 0x47)), null);
                break;
            }
            case "headphones":
            {
                double r = bodyW * .49;
                var arc = new StreamGeometry();
                using (var c = arc.Open())
                {
                    c.BeginFigure(new Point(-r, bodyTop + 15), false, false);
                    c.BezierTo(new Point(-r - 2, bodyTop - 12), new Point(r + 2, bodyTop - 12), new Point(r, bodyTop + 15), true, false);
                }
                arc.Freeze();
                dc.DrawGeometry(null, FrozenPen(EffectPalette.Blend(accent, Colors.Black, .2), 3.6), arc);
                dc.DrawGeometry(null, FrozenPen(accent, 1.8), arc);
                Brush pad = FrozenBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
                Brush ring = FrozenBrush(detail);
                dc.DrawRoundedRectangle(pad, outline, new Rect(-r - 4.5, bodyTop + 10, 7.5, 13), 3.5, 5);
                dc.DrawRoundedRectangle(pad, outline, new Rect(r - 3.0, bodyTop + 10, 7.5, 13), 3.5, 5);
                dc.DrawEllipse(ring, null, new Point(-r - .8, bodyTop + 16.5), 1.8, 3.2);
                dc.DrawEllipse(ring, null, new Point(r + .8, bodyTop + 16.5), 1.8, 3.2);
                break;
            }
            case "bow":
            {
                Brush bow = FrozenBrush(detail);
                dc.DrawPolygon(bow, outline, new Point(-11, bodyTop - 7), new Point(0, bodyTop), new Point(-11, bodyTop + 4));
                dc.DrawPolygon(bow, outline, new Point(11, bodyTop - 7), new Point(0, bodyTop), new Point(11, bodyTop + 4));
                dc.DrawEllipse(FrozenBrush(accent), outline, new Point(0, bodyTop - 1), 3.0, 3.0);
                break;
            }
            case "beret":
            {
                dc.PushTransform(new RotateTransform(-11, 0, bodyTop - 2));
                dc.DrawEllipse(FrozenBrush(accent), outline, new Point(-1, bodyTop - 3), 16, 6.2);
                dc.DrawLine(outline, new Point(-2, bodyTop - 9), new Point(1, bodyTop - 12));
                dc.Pop();
                break;
            }
            case "tophat":
            {
                Brush dark = FrozenBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
                dc.DrawRoundedRectangle(dark, outline, new Rect(-18, bodyTop - 2.5, 36, 4.5), 2, 2);
                dc.DrawRoundedRectangle(dark, outline, new Rect(-11, bodyTop - 19, 22, 17), 3, 3);
                dc.DrawRectangle(FrozenBrush(detail), null, new Rect(-10.5, bodyTop - 6.5, 21, 3.2));
                break;
            }
            case "chef":
            {
                Brush white = FrozenBrush(Colors.White);
                dc.DrawEllipse(white, outline, new Point(-6, bodyTop - 11), 7.5, 6.5);
                dc.DrawEllipse(white, outline, new Point(6, bodyTop - 11), 7.5, 6.5);
                dc.DrawEllipse(white, outline, new Point(0, bodyTop - 14), 8.2, 7.2);
                dc.DrawRoundedRectangle(white, outline, new Rect(-12, bodyTop - 6, 24, 7.5), 2.5, 2.5);
                break;
            }
            case "flower":
            {
                double fx = facing >= 0 ? -12 : 12, fy = bodyTop + 1;
                Brush petal = FrozenBrush(detail);
                for (int i = 0; i < 5; i++)
                {
                    double a = i * Math.PI * 2 / 5.0;
                    dc.DrawEllipse(petal, outline, new Point(fx + Math.Cos(a) * 4.5, fy + Math.Sin(a) * 4.5), 3.3, 3.3);
                }
                dc.DrawEllipse(FrozenBrush(Color.FromRgb(0xFA, 0xCC, 0x15)), null, new Point(fx, fy), 2.5, 2.5);
                break;
            }
        }
    }

    private static void DrawAura(DrawingContext dc, string aura, double cy, double bodyW, Color accent, Color detail, double breathPhase)
    {
        switch (aura)
        {
            case "ring":
            {
                double pulse = 1.0 + Math.Sin(breathPhase * 1.4) * .05;
                dc.DrawEllipse(null, FrozenPen(Color.FromArgb(120, accent.R, accent.G, accent.B), 2.0), new Point(0, cy), 33 * pulse, 28 * pulse);
                dc.DrawEllipse(null, FrozenPen(Color.FromArgb(75, detail.R, detail.G, detail.B), 1.2), new Point(0, cy), 37 * pulse, 32 * pulse);
                break;
            }
            case "sunburst":
            {
                double rot = (breathPhase * 16) % 360;
                dc.PushTransform(new RotateTransform(rot, 0, cy));
                Pen rayPen = FrozenPen(Color.FromArgb(125, detail.R, detail.G, detail.B), 2.2);
                for (int i = 0; i < 8; i++)
                {
                    double a = i * Math.PI / 4.0;
                    dc.DrawLine(rayPen, new Point(Math.Cos(a) * 27, cy + Math.Sin(a) * 24), new Point(Math.Cos(a) * 37, cy + Math.Sin(a) * 33));
                }
                dc.Pop();
                break;
            }
            case "neon":
            {
                var rad = new RadialGradientBrush(Color.FromArgb(140, accent.R, accent.G, accent.B), Color.FromArgb(0, detail.R, detail.G, detail.B));
                rad.Freeze();
                dc.DrawEllipse(rad, null, new Point(0, cy), 40, 36);
                break;
            }
            case "sakura":
            {
                Brush pb = FrozenBrush(Color.FromArgb(175, 0xF9, 0xA8, 0xD4));
                for (int i = 0; i < 5; i++)
                {
                    double a = breathPhase * .6 + i * 1.25;
                    dc.DrawEllipse(pb, null, new Point(Math.Cos(a) * (bodyW * .68), cy + Math.Sin(a * 1.2) * 20), 3.1, 1.8);
                }
                break;
            }
            case "bubbles":
            {
                Pen bp = FrozenPen(Color.FromArgb(145, detail.R, detail.G, detail.B), 1.1);
                Brush bf = FrozenBrush(Color.FromArgb(45, accent.R, accent.G, accent.B));
                for (int i = 0; i < 4; i++)
                {
                    double a = breathPhase * .8 + i * 1.57;
                    dc.DrawEllipse(bf, bp, new Point(Math.Cos(a) * 33, cy + Math.Sin(a) * 24), 3.5 + (i % 2) * 1.4, 3.5 + (i % 2) * 1.4);
                }
                break;
            }
            case "stars":
            {
                Brush sb = FrozenBrush(Color.FromArgb(200, detail.R, detail.G, detail.B));
                for (int i = 0; i < 5; i++)
                {
                    double a = breathPhase * .7 + i * (Math.PI * 2 / 5.0);
                    DrawStarShape(dc, Math.Cos(a) * 34, cy + Math.Sin(a) * 26, 2.8, sb, null);
                }
                break;
            }
        }
    }

    private static void DrawPropAndCues(
        DrawingContext dc, string prop, double bodyTop, double bodyBottom, double bodyW,
        Color accent, Color detail, Color ink, double breathPhase, double hopArc, double facing,
        bool isExcited, bool sleepy, bool isDancing = false)
    {
        double floatOrb = Math.Sin(breathPhase * 2.0) * 3.0;
        switch (prop)
        {
            case "sparkles":
                DrawStarShape(dc, -bodyW * .5 - 8, bodyTop + 8 + floatOrb, 3.4, FrozenBrush(detail), null);
                DrawStarShape(dc, bodyW * .5 + 9, bodyTop + 2 - floatOrb, 4.2, FrozenBrush(accent), null);
                DrawStarShape(dc, bodyW * .5 + 6, bodyBottom - 8 + floatOrb * .7, 2.5, FrozenBrush(detail), null);
                break;
            case "hearts":
                DrawHeartShape(dc, -bodyW * .5 - 8, bodyTop + 6 - floatOrb, 3.8, FrozenBrush(Color.FromRgb(0xF4, 0x72, 0xB6)), null);
                DrawHeartShape(dc, bodyW * .5 + 8, bodyTop + 10 + floatOrb, 3.1, FrozenBrush(detail), null);
                break;
            case "music":
                DrawMusicNote(dc, bodyW * .5 + 9, bodyTop + 6 + floatOrb, accent);
                DrawMusicNote(dc, -bodyW * .5 - 10, bodyTop + 12 - floatOrb * .8, detail);
                break;
            case "fire":
                for (int i = -1; i <= 1; i += 2)
                {
                    double fx = i * (bodyW * .5 + 8);
                    double fy = bodyBottom - 12 + (i < 0 ? floatOrb : -floatOrb);
                    dc.DrawEllipse(FrozenBrush(Color.FromArgb(180, 0xFB, 0x92, 0x3C)), null, new Point(fx, fy), 4.2, 6.0);
                    dc.DrawEllipse(FrozenBrush(Color.FromRgb(0xFD, 0xE0, 0x47)), null, new Point(fx, fy + 1.5), 2.2, 3.4);
                }
                break;
            case "orbit":
            {
                dc.PushTransform(new RotateTransform(-16, 0, bodyTop + 22));
                dc.DrawEllipse(null, FrozenPen(Color.FromArgb(145, detail.R, detail.G, detail.B), 1.4),
                    new Point(0, bodyTop + 22), bodyW * .66, 9.5);
                double ang = breathPhase * 1.9;
                dc.DrawEllipse(FrozenBrush(accent), FrozenPen(Colors.White, .8),
                    new Point(Math.Cos(ang) * bodyW * .66, bodyTop + 22 + Math.Sin(ang) * 9.5), 3.1, 3.1);
                dc.Pop();
                break;
            }
            case "coffee":
            {
                double mx = facing * (bodyW * .5 + 2) - 4;
                double my = bodyBottom - 16 + floatOrb * .25;
                Pen mugPen = FrozenPen(ink, 1.2);
                dc.DrawEllipse(null, mugPen, new Point(mx + 8.5, my + 5), 2.5, 3.0);
                dc.DrawRoundedRectangle(FrozenBrush(Colors.White), mugPen, new Rect(mx, my, 9, 10), 2, 2);
                dc.DrawRectangle(FrozenBrush(Color.FromRgb(0x78, 0x35, 0x0F)), null, new Rect(mx + 1.5, my + 1.5, 6, 2));
                Pen steam = FrozenPen(Color.FromArgb(165, 255, 255, 255), 1.1);
                dc.DrawLine(steam, new Point(mx + 3, my - 2), new Point(mx + 2 + Math.Sin(breathPhase * 2), my - 6));
                dc.DrawLine(steam, new Point(mx + 6, my - 2), new Point(mx + 7 - Math.Sin(breathPhase * 2), my - 7));
                break;
            }
            case "wand":
            {
                double wx = facing * (bodyW * .5 + 5);
                double wy = bodyBottom - 10;
                double tx = wx + facing * 7, ty = wy - 17;
                dc.DrawLine(FrozenPen(Color.FromRgb(0x92, 0x40, 0x0E), 2.0), new Point(wx, wy), new Point(tx, ty));
                DrawStarShape(dc, tx, ty, 5.2, FrozenBrush(Color.FromRgb(0xFD, 0xE0, 0x47)), FrozenPen(Color.FromRgb(0xB4, 0x53, 0x09), .9));
                break;
            }
            case "balloon":
            {
                double bx = facing * (bodyW * .5 + 11), by = bodyTop - 10 + floatOrb * .8;
                dc.DrawLine(FrozenPen(ink, 1.1), new Point(facing * (bodyW * .5 + 3), bodyBottom - 10), new Point(bx, by + 7));
                dc.DrawEllipse(FrozenBrush(detail), FrozenPen(ink, 1.1), new Point(bx, by), 6.2, 7.6);
                dc.DrawEllipse(FrozenBrush(Color.FromArgb(145, 255, 255, 255)), null, new Point(bx - 2, by - 2.5), 1.5, 2.2);
                break;
            }
            case "gamepad":
            {
                double gx = facing * 4 - 9, gy = bodyBottom - 11;
                dc.DrawRoundedRectangle(FrozenBrush(Color.FromRgb(0x1E, 0x29, 0x3B)), FrozenPen(accent, 1.1), new Rect(gx, gy, 18, 9), 4, 4);
                dc.DrawRectangle(FrozenBrush(Colors.White), null, new Rect(gx + 3.5, gy + 3, 4, 2));
                dc.DrawEllipse(FrozenBrush(detail), null, new Point(gx + 13.5, gy + 3.5), 1.3, 1.3);
                dc.DrawEllipse(FrozenBrush(accent), null, new Point(gx + 11.5, gy + 5.5), 1.3, 1.3);
                break;
            }
            case "lantern":
            {
                double lx = facing * (bodyW * .5 + 8), ly = bodyTop + 14 + floatOrb * .5;
                dc.DrawLine(FrozenPen(Color.FromRgb(0x92, 0x40, 0x0E), 1.6), new Point(facing * (bodyW * .5 + 2), bodyBottom - 12), new Point(lx, bodyTop + 4));
                dc.DrawLine(FrozenPen(ink, 1.0), new Point(lx, bodyTop + 4), new Point(lx, ly - 5));
                dc.DrawRoundedRectangle(FrozenBrush(Color.FromRgb(0xFB, 0x92, 0x3C)), FrozenPen(Color.FromRgb(0xB4, 0x53, 0x09), 1.1), new Rect(lx - 4.5, ly - 5, 9, 11), 3, 3);
                dc.DrawEllipse(FrozenBrush(Color.FromArgb(110, 0xFD, 0xE0, 0x47)), null, new Point(lx, ly + .5), 8, 8);
                break;
            }
        }

        if (isDancing && prop != "music")
        {
            DrawMusicNote(dc, bodyW * .5 + 9, bodyTop + 5 + floatOrb, accent);
            DrawMusicNote(dc, -bodyW * .5 - 10, bodyTop + 11 - floatOrb * .8, detail);
        }
        if (isExcited || hopArc > 3)
        {
            Brush spark = FrozenBrush(Color.FromArgb(205, accent.R, accent.G, accent.B));
            dc.DrawEllipse(spark, null, new Point(-bodyW * .48, bodyTop - 6), 2.3, 2.3);
            dc.DrawEllipse(spark, null, new Point(bodyW * .48, bodyTop - 9), 1.8, 1.8);
        }
        else if (sleepy)
        {
            double drift = Math.Sin(breathPhase * .6) * 2.5;
            Pen zPen = FrozenPen(Color.FromArgb(150, ink.R, ink.G, ink.B), 1.3);
            var zGeo = new StreamGeometry();
            using (var g = zGeo.Open())
            {
                double zx = bodyW * .44, zy = bodyTop - 5 - Math.Abs(drift);
                g.BeginFigure(new Point(zx, zy), false, false);
                g.LineTo(new Point(zx + 5, zy), true, false);
                g.LineTo(new Point(zx, zy + 5), true, false);
                g.LineTo(new Point(zx + 5, zy + 5), true, false);
            }
            zGeo.Freeze();
            dc.DrawGeometry(null, zPen, zGeo);
        }
    }

    private static void DrawMusicNote(DrawingContext dc, double x, double y, Color col)
    {
        Brush b = FrozenBrush(col);
        Pen p = FrozenPen(col, 1.5);
        dc.DrawEllipse(b, null, new Point(x, y + 3), 2.5, 1.9);
        dc.DrawLine(p, new Point(x + 2, y + 2.5), new Point(x + 2, y - 5));
        dc.DrawLine(p, new Point(x + 2, y - 5), new Point(x + 6, y - 3.5));
    }

    private static void DrawStarShape(DrawingContext dc, double cx, double cy, double outerR, Brush fill, Pen? pen)
    {
        var pts = new Point[10];
        double innerR = outerR * .45;
        for (int i = 0; i < 10; i++)
        {
            double ang = -Math.PI * .5 + i * Math.PI / 5.0;
            double r = i % 2 == 0 ? outerR : innerR;
            pts[i] = new Point(cx + Math.Cos(ang) * r, cy + Math.Sin(ang) * r);
        }
        dc.DrawPolygon(fill, pen, pts);
    }

    private static void DrawHeartShape(DrawingContext dc, double cx, double cy, double r, Brush fill, Pen? pen)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(cx, cy + r * .85), true, true);
            c.BezierTo(new Point(cx - r * 1.4, cy - r * .1), new Point(cx - r * .8, cy - r * 1.1), new Point(cx, cy - r * .35), true, false);
            c.BezierTo(new Point(cx + r * .8, cy - r * 1.1), new Point(cx + r * 1.4, cy - r * .1), new Point(cx, cy + r * .85), true, false);
        }
        g.Freeze();
        dc.DrawGeometry(fill, pen, g);
    }

    private static void DrawHappyEye(DrawingContext dc, double cx, double cy, Pen pen)
    {
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(new Point(cx - 3.6, cy + 1.2), false, false);
            g.QuadraticBezierTo(new Point(cx, cy - 3.6), new Point(cx + 3.6, cy + 1.2), true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(null, pen, geo);
    }

    private static void DrawClosedEye(DrawingContext dc, double cx, double cy, Pen pen)
    {
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(new Point(cx - 3.4, cy - .6), false, false);
            g.QuadraticBezierTo(new Point(cx, cy + 2.8), new Point(cx + 3.4, cy - .6), true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(null, pen, geo);
    }

    private static void DrawSmile(DrawingContext dc, double cx, double cy, double halfW, double dip, Pen pen)
    {
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(new Point(cx - halfW, cy), false, false);
            g.QuadraticBezierTo(new Point(cx, cy + dip), new Point(cx + halfW, cy), true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(null, pen, geo);
    }
}

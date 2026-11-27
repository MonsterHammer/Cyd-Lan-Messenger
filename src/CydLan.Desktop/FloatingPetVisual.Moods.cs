using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using FontFamily = System.Windows.Media.FontFamily;

namespace CydLan.Desktop;

/// <summary>Mood expressions layered over the companion: faces, brows, mouths, and floating cues.</summary>
public sealed partial class FloatingPetVisual
{
    private string mood = PetMood.Neutral;

    /// <summary>Sets the personality mood to draw (see <see cref="PetMood"/>).</summary>
    public void SetMood(string value)
    {
        value ??= PetMood.Neutral;
        if (mood == value) return;
        mood = value;
        InvalidateVisual();
    }

    public string Mood => mood;

    private static readonly Color AngryRed = Color.FromRgb(0xEF, 0x44, 0x44);
    private static readonly Color TearBlue = Color.FromRgb(0x60, 0xA5, 0xFA);
    private static readonly Color LovePink = Color.FromRgb(0xF4, 0x72, 0xB6);

    private bool MoodIsHappy => mood is PetMood.Love or PetMood.Celebrate or PetMood.Happy or PetMood.Laughing;

    /// <summary>Extra tilt and sideways jitter from the mood.</summary>
    private (double Tilt, double ShakeX, double Squash) MoodMotion(double phase, double facingSign) => mood switch
    {
        PetMood.Fainted => (facingSign * 78, 0, 1),
        PetMood.Angry => (Math.Sin(phase * 9) * 2.2, Math.Sin(phase * 31) * 1.4, 1),
        PetMood.Dizzy => (Math.Sin(phase * 2.6) * 9, 0, 1),
        PetMood.Sad => (0, 0, .955),
        PetMood.Shy => (-facingSign * 6, 0, .975),
        PetMood.Celebrate => (Math.Sin(phase * 5.2) * 7, 0, 1),
        PetMood.Curious => (facingSign * 9, 0, 1),
        PetMood.Crying => (Math.Sin(phase * 7) * 2.4, Math.Sin(phase * 23) * .7, .965 + Math.Abs(Math.Sin(phase * 3.5)) * .03),
        PetMood.Laughing => (Math.Sin(phase * 4.2) * 7, 0, .93 + Math.Abs(Math.Sin(phase * 8.5)) * .1),
        PetMood.Surprised => (0, 0, 1.06),
        PetMood.Nervous => (0, Math.Sin(phase * 37) * .9, .985),
        PetMood.Fired => (facingSign * 5, 0, 1.02),
        PetMood.Hurt => (-facingSign * 12, Math.Sin(phase * 41) * 1.8, .9),
        PetMood.Eating => (0, 0, .97 + Math.Abs(Math.Sin(phase * 6)) * .04),
        PetMood.Yawning => (-facingSign * 4, 0, 1.05),
        _ => (0, 0, 1)
    };

    private void DrawMoodBodyTint(DrawingContext dc, Geometry body, double phase)
    {
        if (mood == PetMood.Angry)
        {
            byte a = (byte)(46 + (Math.Sin(phase * 4) + 1) * 18);
            dc.DrawGeometry(FrozenBrush(Color.FromArgb(a, AngryRed.R, AngryRed.G, AngryRed.B)), null, body);
        }
        else if (mood == PetMood.Sad)
            dc.DrawGeometry(FrozenBrush(Color.FromArgb(34, TearBlue.R, TearBlue.G, TearBlue.B)), null, body);
        else if (mood is PetMood.Shy or PetMood.Love)
            dc.DrawGeometry(FrozenBrush(Color.FromArgb(22, LovePink.R, LovePink.G, LovePink.B)), null, body);
    }

    /// <summary>Draws mood-specific eyes. Returns false to fall back to the style's own eyes.</summary>
    private bool DrawMoodEyes(DrawingContext dc, double lookX, double eyeY, double eyeSep, Color ink, Color accent, double phase, double blink)
    {
        double lx = lookX - eyeSep, rx = lookX + eyeSep;
        Pen pen = FrozenPen(ink, 1.85);
        if (DrawActionEyes(dc, lx, rx, eyeY, ink, accent, phase, blink)) return true;
        switch (mood)
        {
            case PetMood.Fainted:
                foreach (double x in new[] { lx, rx })
                {
                    dc.DrawLine(pen, new Point(x - 2.8, eyeY - 2.8), new Point(x + 2.8, eyeY + 2.8));
                    dc.DrawLine(pen, new Point(x - 2.8, eyeY + 2.8), new Point(x + 2.8, eyeY - 2.8));
                }
                return true;
            case PetMood.Dizzy:
                foreach (double x in new[] { lx, rx }) DrawSpiral(dc, x, eyeY, 3.6, phase * 3, FrozenPen(ink, 1.3));
                return true;
            case PetMood.Love:
                DrawHeartShape(dc, lx, eyeY + .4, 3.6, FrozenBrush(LovePink), FrozenPen(EffectPalette.Blend(LovePink, ink, .3), .8));
                DrawHeartShape(dc, rx, eyeY + .4, 3.6, FrozenBrush(LovePink), FrozenPen(EffectPalette.Blend(LovePink, ink, .3), .8));
                return true;
            case PetMood.Celebrate:
            case PetMood.Happy when blink < .45:
                DrawHappyEye(dc, lx, eyeY, pen);
                DrawHappyEye(dc, rx, eyeY, pen);
                return true;
            case PetMood.Shy:
                // > <
                dc.DrawLine(pen, new Point(lx - 2.8, eyeY - 2.2), new Point(lx + 2, eyeY));
                dc.DrawLine(pen, new Point(lx + 2, eyeY), new Point(lx - 2.8, eyeY + 2.2));
                dc.DrawLine(pen, new Point(rx + 2.8, eyeY - 2.2), new Point(rx - 2, eyeY));
                dc.DrawLine(pen, new Point(rx - 2, eyeY), new Point(rx + 2.8, eyeY + 2.2));
                return true;
            case PetMood.Bored:
            case PetMood.Smug:
            case PetMood.Drowsy:
            {
                // Half-lidded: lid line over a small pupil.
                Brush pupil = FrozenBrush(ink);
                foreach (double x in new[] { lx, rx })
                {
                    if (blink < .45) dc.DrawEllipse(pupil, null, new Point(x, eyeY + 1.2), 2.1, 1.6);
                    dc.DrawLine(FrozenPen(ink, 1.6), new Point(x - 3.4, eyeY - .2), new Point(x + 3.4, eyeY - .2));
                }
                return true;
            }
            case PetMood.Alert:
            {
                if (blink > .45) return false;
                Brush white = FrozenBrush(Colors.White);
                Brush pupil = FrozenBrush(ink);
                foreach (double x in new[] { lx, rx })
                {
                    dc.DrawEllipse(white, FrozenPen(ink, 1.1), new Point(x, eyeY), 3.6, 3.9);
                    dc.DrawEllipse(pupil, null, new Point(x + (lookX * .25), eyeY), 1.7, 1.9);
                }
                return true;
            }
            case PetMood.Angry:
            {
                if (blink > .45) return false;
                Brush pupil = FrozenBrush(ink);
                foreach (int s in new[] { -1, 1 })
                {
                    double x = lookX + s * eyeSep;
                    // Narrowed: a half-disc under a hard lid.
                    var g = new StreamGeometry();
                    using (var c = g.Open())
                    {
                        c.BeginFigure(new Point(x - 3.2, eyeY - (s < 0 ? 1.4 : -.2)), true, true);
                        c.LineTo(new Point(x + 3.2, eyeY - (s < 0 ? -.2 : 1.4)), true, false);
                        c.ArcTo(new Point(x - 3.2, eyeY - (s < 0 ? 1.4 : -.2)), new Size(3.2, 3.0), 0, false, SweepDirection.Clockwise, true, false);
                    }
                    g.Freeze();
                    dc.DrawGeometry(pupil, null, g);
                }
                return true;
            }
            default:
                return false;
        }
    }

    /// <summary>Returns true when the mood replaces the style's brows.</summary>
    private bool DrawMoodBrows(DrawingContext dc, double lookX, double browY, double eyeSep, Color ink)
    {
        Pen p = FrozenPen(ink, 1.9);
        double lx = lookX - eyeSep, rx = lookX + eyeSep;
        if (DrawActionBrows(dc, lx, rx, browY, ink)) return true;
        switch (mood)
        {
            case PetMood.Angry:
                dc.DrawLine(FrozenPen(ink, 2.3), new Point(lx - 3.6, browY - 2), new Point(lx + 3.4, browY + 1.6));
                dc.DrawLine(FrozenPen(ink, 2.3), new Point(rx - 3.4, browY + 1.6), new Point(rx + 3.6, browY - 2));
                return true;
            case PetMood.Sad:
            case PetMood.Hungry:
                dc.DrawLine(p, new Point(lx - 3.2, browY + 1.4), new Point(lx + 3.2, browY - 1.4));
                dc.DrawLine(p, new Point(rx - 3.2, browY - 1.4), new Point(rx + 3.2, browY + 1.4));
                return true;
            case PetMood.Alert:
                DrawHappyEye(dc, lx, browY - 1.6, FrozenPen(ink, 1.5));
                DrawHappyEye(dc, rx, browY - 1.6, FrozenPen(ink, 1.5));
                return true;
            case PetMood.Smug:
                dc.DrawLine(p, new Point(lx - 3.2, browY + .6), new Point(lx + 3.2, browY + .6));
                DrawHappyEye(dc, rx, browY - 1.4, FrozenPen(ink, 1.5));
                return true;
            case PetMood.Curious:
                DrawHappyEye(dc, rx, browY - 1.8, FrozenPen(ink, 1.5));
                return false;
            default:
                return false;
        }
    }

    /// <summary>Draws a mood mouth. Returns false to fall back to the style's mouth.</summary>
    private bool DrawMoodMouth(DrawingContext dc, double cx, double cy, Color ink, Color detail, double phase)
    {
        Pen pen = FrozenPen(ink, 1.7);
        if (DrawActionMouth(dc, cx, cy, ink, detail, phase)) return true;
        switch (mood)
        {
            case PetMood.Angry:
            {
                // Gritted teeth.
                var r = new Rect(cx - 4.6, cy - 1.2, 9.2, 4.2);
                dc.DrawRoundedRectangle(FrozenBrush(Colors.White), FrozenPen(ink, 1.3), r, 1.4, 1.4);
                dc.DrawLine(FrozenPen(ink, .9), new Point(cx - 4.4, cy + .9), new Point(cx + 4.4, cy + .9));
                for (int i = -1; i <= 1; i++) dc.DrawLine(FrozenPen(ink, .8), new Point(cx + i * 2.3, cy - 1.1), new Point(cx + i * 2.3, cy + 2.9));
                return true;
            }
            case PetMood.Sad:
                DrawSmile(dc, cx, cy + 2.4, 3.6, -2.6, pen);
                return true;
            case PetMood.Bored:
            case PetMood.Fainted:
                dc.DrawLine(pen, new Point(cx - 3, cy + 1), new Point(cx + 3, cy + 1));
                return true;
            case PetMood.Dizzy:
            case PetMood.Shy:
                DrawWave(dc, cx, cy + 1, 3.6, FrozenPen(ink, 1.35));
                return true;
            case PetMood.Hungry:
            {
                dc.DrawEllipse(FrozenBrush(EffectPalette.Blend(ink, detail, .2)), null, new Point(cx, cy + 1.4), 2.6, 2.4);
                double drip = (phase * .8) % 1.0;
                dc.DrawEllipse(FrozenBrush(Color.FromArgb((byte)(220 * (1 - drip)), TearBlue.R, TearBlue.G, TearBlue.B)), null, new Point(cx + 3.2, cy + 4 + drip * 5), 1.1, 1.5);
                return true;
            }
            case PetMood.Alert:
            case PetMood.Drowsy:
                dc.DrawEllipse(FrozenBrush(EffectPalette.Blend(ink, detail, .25)), null, new Point(cx, cy + 1.6), mood == PetMood.Drowsy ? 2.2 + Math.Sin(phase) * .5 : 2.0, mood == PetMood.Drowsy ? 2.6 + Math.Sin(phase) * .7 : 2.4);
                return true;
            case PetMood.Love:
            case PetMood.Celebrate:
            {
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    c.BeginFigure(new Point(cx - 4.4, cy), true, true);
                    c.LineTo(new Point(cx + 4.4, cy), true, false);
                    c.ArcTo(new Point(cx - 4.4, cy), new Size(4.4, 4.0), 0, false, SweepDirection.Clockwise, true, false);
                }
                g.Freeze();
                dc.DrawGeometry(FrozenBrush(EffectPalette.Blend(ink, LovePink, .25)), FrozenPen(ink, 1.2), g);
                dc.DrawEllipse(FrozenBrush(LovePink), null, new Point(cx, cy + 2.6), 2.0, 1.1);
                return true;
            }
            case PetMood.Smug:
            {
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    c.BeginFigure(new Point(cx - 3.4, cy + 1.4), false, false);
                    c.QuadraticBezierTo(new Point(cx + 1, cy + 2.6), new Point(cx + 4, cy - .6), true, false);
                }
                g.Freeze();
                dc.DrawGeometry(null, pen, g);
                return true;
            }
            default:
                return false;
        }
    }

    private void DrawMoodCheeks(DrawingContext dc, double lookX, double eyeY, double eyeSep)
    {
        if (mood is not (PetMood.Shy or PetMood.Love or PetMood.Angry)) return;
        Color c = mood == PetMood.Angry ? AngryRed : LovePink;
        Brush b = FrozenBrush(Color.FromArgb(mood == PetMood.Angry ? (byte)110 : (byte)150, c.R, c.G, c.B));
        double cy = eyeY + 4.8;
        dc.DrawEllipse(b, null, new Point(lookX - eyeSep - 3.6, cy), 4.4, 2.6);
        dc.DrawEllipse(b, null, new Point(lookX + eyeSep + 3.6, cy), 4.4, 2.6);
        if (mood == PetMood.Shy)
        {
            Pen line = FrozenPen(Color.FromArgb(150, c.R, c.G, c.B), .9);
            foreach (double x in new[] { lookX - eyeSep - 3.6, lookX + eyeSep + 3.6 })
                for (int i = -1; i <= 1; i++) dc.DrawLine(line, new Point(x + i * 1.8 - .6, cy + 1.2), new Point(x + i * 1.8 + .6, cy - 1.2));
        }
    }

    /// <summary>Floating mood cues around the companion (above the body, inside the tilt).</summary>
    private void DrawMoodCues(DrawingContext dc, double bodyTop, double bodyBottom, double bodyW, double eyeY, double eyeSep, double lookX,
        Color accent, Color detail, Color ink, double phase)
    {
        DrawActionCues(dc, bodyTop, bodyBottom, bodyW, eyeY, eyeSep, lookX, accent, detail, ink, phase);
        switch (mood)
        {
            case PetMood.Angry:
            {
                // Anger mark: four bent strokes.
                double ax = bodyW * .42, ay = bodyTop + 2;
                double pulse = 1 + Math.Sin(phase * 6) * .14;
                Pen red = FrozenPen(AngryRed, 1.7);
                foreach (var (sx, sy) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
                {
                    var g = new StreamGeometry();
                    using (var c = g.Open())
                    {
                        c.BeginFigure(new Point(ax + sx * 1.1 * pulse, ay + sy * 3.8 * pulse), false, false);
                        c.QuadraticBezierTo(new Point(ax + sx * 1.1 * pulse, ay + sy * 1.1 * pulse), new Point(ax + sx * 3.8 * pulse, ay + sy * 1.1 * pulse), true, false);
                    }
                    g.Freeze();
                    dc.DrawGeometry(null, red, g);
                }
                // Steam puffs.
                for (int i = 0; i < 2; i++)
                {
                    double t = (phase * .9 + i * .5) % 1.0;
                    double sx = (i == 0 ? -1 : 1) * (bodyW * .3 + t * 6);
                    double sy = bodyTop - 2 - t * 12;
                    byte a = (byte)(150 * (1 - t));
                    dc.DrawEllipse(FrozenBrush(Color.FromArgb(a, 0xE5, 0xE7, 0xEB)), null, new Point(sx, sy), 2.4 + t * 3, 2.0 + t * 2.4);
                }
                break;
            }
            case PetMood.Sad:
            {
                double t = (phase * .55) % 1.0;
                double tx = lookX - eyeSep - 1.4, ty = eyeY + 3 + t * 9;
                byte a = (byte)(230 * (1 - t * .7));
                DrawDrop(dc, tx, ty, 1.7, Color.FromArgb(a, TearBlue.R, TearBlue.G, TearBlue.B));
                break;
            }
            case PetMood.Love:
                for (int i = 0; i < 3; i++)
                {
                    double t = (phase * .35 + i / 3.0) % 1.0;
                    double hx = (i - 1) * bodyW * .38 + Math.Sin(phase * 2 + i) * 3;
                    double hy = bodyTop - 2 - t * 22;
                    byte a = (byte)(235 * Math.Sin(t * Math.PI));
                    DrawHeartShape(dc, hx, hy, 3.2 + i * .4, FrozenBrush(Color.FromArgb(a, LovePink.R, LovePink.G, LovePink.B)), null);
                }
                break;
            case PetMood.Celebrate:
            {
                Color[] colors = [accent, detail, LovePink, Color.FromRgb(0xFA, 0xCC, 0x15), Color.FromRgb(0x34, 0xD3, 0x99)];
                for (int i = 0; i < 12; i++)
                {
                    double t = (phase * .45 + i / 12.0) % 1.0;
                    double ang = i * 2.399;
                    double r = 10 + t * 26;
                    double px = Math.Cos(ang) * r, py = bodyTop + 6 - Math.Sin(ang) * r * .7 + t * t * 14;
                    byte a = (byte)(255 * (1 - t));
                    Color c = colors[i % colors.Length];
                    dc.PushTransform(new RotateTransform(phase * 120 + i * 40, px, py));
                    dc.DrawRectangle(FrozenBrush(Color.FromArgb(a, c.R, c.G, c.B)), null, new Rect(px - 1.6, py - .9, 3.2, 1.8));
                    dc.Pop();
                }
                break;
            }
            case PetMood.Alert:
            {
                double bob = Math.Abs(Math.Sin(phase * 3.4)) * 3;
                double x = bodyW * .44, y = bodyTop - 10 - bob;
                dc.DrawEllipse(FrozenBrush(Color.FromArgb(235, 0xFA, 0xCC, 0x15)), FrozenPen(EffectPalette.Blend(Color.FromRgb(0xFA, 0xCC, 0x15), ink, .4), 1), new Point(x, y), 5.4, 5.4);
                dc.DrawLine(FrozenPen(ink, 1.8), new Point(x, y - 2.8), new Point(x, y + .9));
                dc.DrawEllipse(FrozenBrush(ink), null, new Point(x, y + 2.7), .95, .95);
                break;
            }
            case PetMood.Bored:
            {
                Brush b = FrozenBrush(Color.FromArgb(170, ink.R, ink.G, ink.B));
                int shown = (int)(phase * 1.2) % 4;
                for (int i = 0; i < shown; i++) dc.DrawEllipse(b, null, new Point(bodyW * .36 + i * 4.2, bodyTop - 4), 1.2, 1.2);
                break;
            }
            case PetMood.Hungry:
            {
                // Thought bubble with a donut.
                double x = bodyW * .5 + 6, y = bodyTop - 12 + Math.Sin(phase) * 1.4;
                Brush cloud = FrozenBrush(Color.FromArgb(235, 255, 255, 255));
                Pen outline = FrozenPen(Color.FromArgb(120, ink.R, ink.G, ink.B), .8);
                dc.DrawEllipse(cloud, outline, new Point(bodyW * .34, bodyTop - 2), 1.6, 1.6);
                dc.DrawEllipse(cloud, outline, new Point(bodyW * .42, bodyTop - 6), 2.3, 2.3);
                dc.DrawEllipse(cloud, outline, new Point(x, y), 8.4, 7.2);
                dc.DrawEllipse(FrozenBrush(Color.FromRgb(0xF4, 0x72, 0xB6)), FrozenPen(Color.FromRgb(0xB4, 0x53, 0x09), 1.5), new Point(x, y), 4.6, 4.0);
                dc.DrawEllipse(cloud, null, new Point(x, y), 1.5, 1.3);
                break;
            }
            case PetMood.Dizzy:
            case PetMood.Fainted:
            {
                Brush star = FrozenBrush(Color.FromRgb(0xFA, 0xCC, 0x15));
                for (int i = 0; i < 3; i++)
                {
                    double ang = phase * 2.4 + i * Math.PI * 2 / 3;
                    DrawStarShape(dc, Math.Cos(ang) * bodyW * .42, bodyTop - 3 + Math.Sin(ang) * 3.2, 2.6, star, null);
                }
                break;
            }
            case PetMood.Shy:
                DrawDrop(dc, bodyW * .36, bodyTop + 5 + Math.Sin(phase) * .8, 2.0, Color.FromArgb(210, TearBlue.R, TearBlue.G, TearBlue.B));
                break;
            case PetMood.Curious:
            {
                double bob = Math.Sin(phase * 2) * 1.6;
                var text = new FormattedText("?", System.Globalization.CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), 13, FrozenBrush(accent), 1.0);
                dc.DrawText(text, new Point(bodyW * .4, bodyTop - 16 + bob));
                break;
            }
            case PetMood.Smug:
                DrawStarShape(dc, -bodyW * .44, bodyTop + 2 + Math.Sin(phase * 2) * 1.5, 2.8, FrozenBrush(detail), null);
                break;
        }
    }

    private static void DrawSpiral(DrawingContext dc, double cx, double cy, double r, double rotation, Pen pen)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            const int steps = 26;
            for (int i = 0; i <= steps; i++)
            {
                double t = i / (double)steps;
                double ang = rotation + t * Math.PI * 4;
                var pt = new Point(cx + Math.Cos(ang) * r * t, cy + Math.Sin(ang) * r * t);
                if (i == 0) c.BeginFigure(pt, false, false); else c.LineTo(pt, true, false);
            }
        }
        g.Freeze();
        dc.DrawGeometry(null, pen, g);
    }

    private static void DrawWave(DrawingContext dc, double cx, double cy, double halfW, Pen pen)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(cx - halfW, cy), false, false);
            c.BezierTo(new Point(cx - halfW * .5, cy - 2), new Point(cx - halfW * .2, cy + 2), new Point(cx, cy), true, false);
            c.BezierTo(new Point(cx + halfW * .2, cy - 2), new Point(cx + halfW * .5, cy + 2), new Point(cx + halfW, cy), true, false);
        }
        g.Freeze();
        dc.DrawGeometry(null, pen, g);
    }

    private static void DrawDrop(DrawingContext dc, double cx, double cy, double r, Color color)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(cx, cy - r * 2.2), true, true);
            c.BezierTo(new Point(cx + r * .4, cy - r * 1.2), new Point(cx + r, cy - r * .4), new Point(cx + r, cy), true, false);
            c.ArcTo(new Point(cx - r, cy), new Size(r, r), 0, false, SweepDirection.Clockwise, true, false);
            c.BezierTo(new Point(cx - r, cy - r * .4), new Point(cx - r * .4, cy - r * 1.2), new Point(cx, cy - r * 2.2), true, false);
        }
        g.Freeze();
        dc.DrawGeometry(FrozenBrush(color), null, g);
    }
}

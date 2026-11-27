using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;
using Size = System.Windows.Size;

namespace CydLan.Desktop;

/// <summary>Expressive actions: crying, laughing, surprise, eating, yawning, nerves, fired up and hurt, plus spin/flip poses.</summary>
public sealed partial class FloatingPetVisual
{
    private double poseSpin, poseLift, poseStretchX = 1, poseStretchY = 1;
    private string heldFood = "";

    /// <summary>Extra whole-body pose on top of the idle motion: rotation around the body centre, lift and stretch.</summary>
    public void SetPose(double spinDegrees, double lift, double stretchX = 1, double stretchY = 1)
    {
        poseSpin = spinDegrees; poseLift = lift; poseStretchX = stretchX; poseStretchY = stretchY;
        InvalidateVisual();
    }

    /// <summary>The food drawn while the companion is eating (a <see cref="CydLan.Core.PetGame"/> food id).</summary>
    public void SetHeldFood(string? food) { heldFood = food ?? ""; InvalidateVisual(); }

    private static readonly Color JoyYellow = Color.FromRgb(0xFA, 0xCC, 0x15);
    private static readonly Color FireOrange = Color.FromRgb(0xF9, 0x73, 0x16);

    private bool DrawActionEyes(DrawingContext dc, double lx, double rx, double eyeY, Color ink, Color accent, double phase, double blink)
    {
        Pen pen = FrozenPen(ink, 1.9);
        switch (mood)
        {
            case PetMood.Crying:
                // Squeezed shut, bowed down: ∩ ∩ with wobbly lids.
                foreach (double x in new[] { lx, rx })
                {
                    var g = new StreamGeometry();
                    using (var c = g.Open())
                    {
                        c.BeginFigure(new Point(x - 3.4, eyeY + 1.2), false, false);
                        c.QuadraticBezierTo(new Point(x, eyeY - 2.6 + Math.Sin(phase * 9) * .4), new Point(x + 3.4, eyeY + 1.2), true, false);
                    }
                    g.Freeze();
                    dc.DrawGeometry(null, pen, g);
                }
                return true;
            case PetMood.Laughing:
                // > < squint.
                dc.DrawLine(pen, new Point(lx - 3, eyeY - 2.6), new Point(lx + 2.4, eyeY));
                dc.DrawLine(pen, new Point(lx + 2.4, eyeY), new Point(lx - 3, eyeY + 2.6));
                dc.DrawLine(pen, new Point(rx + 3, eyeY - 2.6), new Point(rx - 2.4, eyeY));
                dc.DrawLine(pen, new Point(rx - 2.4, eyeY), new Point(rx + 3, eyeY + 2.6));
                return true;
            case PetMood.Surprised:
            {
                Brush white = FrozenBrush(Colors.White);
                Brush pupil = FrozenBrush(ink);
                foreach (double x in new[] { lx, rx })
                {
                    dc.DrawEllipse(white, FrozenPen(ink, 1.2), new Point(x, eyeY - .6), 4.2, 4.8);
                    dc.DrawEllipse(pupil, null, new Point(x, eyeY - .4), 1.3, 1.5);
                }
                return true;
            }
            case PetMood.Eating:
                DrawHappyEye(dc, lx, eyeY, pen);
                DrawHappyEye(dc, rx, eyeY, pen);
                return true;
            case PetMood.Yawning:
                DrawClosedEye(dc, lx, eyeY, pen);
                DrawClosedEye(dc, rx, eyeY, pen);
                return true;
            case PetMood.Nervous:
            {
                if (blink > .45) return false;
                Brush white = FrozenBrush(Colors.White);
                Brush pupil = FrozenBrush(ink);
                double dart = Math.Sin(phase * 5.3) * 1.3;
                foreach (double x in new[] { lx, rx })
                {
                    dc.DrawEllipse(white, FrozenPen(ink, 1.1), new Point(x, eyeY), 3.5, 3.8);
                    dc.DrawEllipse(pupil, null, new Point(x + dart, eyeY + .4), 1.2, 1.3);
                }
                return true;
            }
            case PetMood.Fired:
            {
                if (blink > .45) return false;
                Brush pupil = FrozenBrush(ink);
                foreach (int s in new[] { -1, 1 })
                {
                    double x = s < 0 ? lx : rx;
                    var g = new StreamGeometry();
                    using (var c = g.Open())
                    {
                        c.BeginFigure(new Point(x - 3.3, eyeY - (s < 0 ? .9 : -.4)), true, true);
                        c.LineTo(new Point(x + 3.3, eyeY - (s < 0 ? -.4 : .9)), true, false);
                        c.ArcTo(new Point(x - 3.3, eyeY - (s < 0 ? .9 : -.4)), new Size(3.3, 3.2), 0, false, SweepDirection.Clockwise, true, false);
                    }
                    g.Freeze();
                    dc.DrawGeometry(pupil, null, g);
                    // Fiery glint.
                    dc.DrawEllipse(FrozenBrush(Color.FromArgb(230, FireOrange.R, FireOrange.G, FireOrange.B)), null, new Point(x + .6, eyeY + 1.1), 1.1, 1.1);
                }
                return true;
            }
            case PetMood.Hurt:
                // >_<
                dc.DrawLine(pen, new Point(lx - 3, eyeY - 2.4), new Point(lx + 2.6, eyeY));
                dc.DrawLine(pen, new Point(lx + 2.6, eyeY), new Point(lx - 3, eyeY + 2.4));
                dc.DrawLine(pen, new Point(rx + 3, eyeY - 2.4), new Point(rx - 2.6, eyeY));
                dc.DrawLine(pen, new Point(rx - 2.6, eyeY), new Point(rx + 3, eyeY + 2.4));
                return true;
            default:
                return false;
        }
    }

    private bool DrawActionBrows(DrawingContext dc, double lx, double rx, double browY, Color ink)
    {
        Pen p = FrozenPen(ink, 1.9);
        switch (mood)
        {
            case PetMood.Crying:
            case PetMood.Nervous:
                dc.DrawLine(p, new Point(lx - 3.4, browY + 1.8), new Point(lx + 3, browY - 1.6));
                dc.DrawLine(p, new Point(rx - 3, browY - 1.6), new Point(rx + 3.4, browY + 1.8));
                return true;
            case PetMood.Surprised:
                DrawHappyEye(dc, lx, browY - 2.4, FrozenPen(ink, 1.5));
                DrawHappyEye(dc, rx, browY - 2.4, FrozenPen(ink, 1.5));
                return true;
            case PetMood.Fired:
                dc.DrawLine(FrozenPen(ink, 2.4), new Point(lx - 3.6, browY - 1.6), new Point(lx + 3.4, browY + 1.2));
                dc.DrawLine(FrozenPen(ink, 2.4), new Point(rx - 3.4, browY + 1.2), new Point(rx + 3.6, browY - 1.6));
                return true;
            default:
                return false;
        }
    }

    private bool DrawActionMouth(DrawingContext dc, double cx, double cy, Color ink, Color detail, double phase)
    {
        Pen pen = FrozenPen(ink, 1.6);
        switch (mood)
        {
            case PetMood.Crying:
            {
                // Wailing open mouth that wobbles.
                double open = 2.6 + Math.Abs(Math.Sin(phase * 6.5)) * 1.6;
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    c.BeginFigure(new Point(cx - 4.2, cy + 3.4), true, true);
                    c.QuadraticBezierTo(new Point(cx, cy - open), new Point(cx + 4.2, cy + 3.4), true, false);
                    c.QuadraticBezierTo(new Point(cx, cy + 2.4), new Point(cx - 4.2, cy + 3.4), true, false);
                }
                g.Freeze();
                dc.DrawGeometry(FrozenBrush(EffectPalette.Blend(ink, LovePink, .2)), FrozenPen(ink, 1.2), g);
                return true;
            }
            case PetMood.Laughing:
            {
                // Big D-shaped laugh with a tongue.
                double open = 4.2 + Math.Abs(Math.Sin(phase * 8.5)) * 1.4;
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    c.BeginFigure(new Point(cx - 5.2, cy - .4), true, true);
                    c.LineTo(new Point(cx + 5.2, cy - .4), true, false);
                    c.ArcTo(new Point(cx - 5.2, cy - .4), new Size(5.2, open), 0, false, SweepDirection.Clockwise, true, false);
                }
                g.Freeze();
                dc.DrawGeometry(FrozenBrush(EffectPalette.Blend(ink, LovePink, .2)), FrozenPen(ink, 1.2), g);
                dc.DrawEllipse(FrozenBrush(LovePink), null, new Point(cx, cy + open * .62), 2.6, 1.3);
                dc.DrawRectangle(FrozenBrush(Colors.White), null, new Rect(cx - 4.2, cy - .2, 8.4, 1.3));
                return true;
            }
            case PetMood.Surprised:
                dc.DrawEllipse(FrozenBrush(EffectPalette.Blend(ink, detail, .2)), FrozenPen(ink, 1.1), new Point(cx, cy + 2), 2.4, 3.2);
                return true;
            case PetMood.Eating:
            {
                // Chomp: alternates open and closed.
                bool open = Math.Sin(phase * 7) > 0;
                if (open) dc.DrawEllipse(FrozenBrush(EffectPalette.Blend(ink, LovePink, .2)), FrozenPen(ink, 1.1), new Point(cx, cy + 1.4), 3.2, 2.6);
                else DrawWave(dc, cx, cy + 1.2, 3.4, FrozenPen(ink, 1.5));
                return true;
            }
            case PetMood.Yawning:
                dc.DrawEllipse(FrozenBrush(EffectPalette.Blend(ink, LovePink, .2)), FrozenPen(ink, 1.1), new Point(cx, cy + 2.4), 3.4, 4.2 + Math.Sin(phase * 1.4) * .6);
                return true;
            case PetMood.Nervous:
                DrawWave(dc, cx, cy + 1.4, 4.2, FrozenPen(ink, 1.4));
                return true;
            case PetMood.Fired:
            {
                // Determined grin with teeth.
                var r = new Rect(cx - 4.8, cy - .2, 9.6, 3.6);
                dc.DrawRoundedRectangle(FrozenBrush(Colors.White), FrozenPen(ink, 1.2), r, 1.8, 1.8);
                dc.DrawLine(FrozenPen(ink, .8), new Point(cx, cy), new Point(cx, cy + 3.2));
                return true;
            }
            case PetMood.Hurt:
            {
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    c.BeginFigure(new Point(cx - 4, cy + 2), false, false);
                    c.LineTo(new Point(cx - 2, cy), true, false);
                    c.LineTo(new Point(cx, cy + 2), true, false);
                    c.LineTo(new Point(cx + 2, cy), true, false);
                    c.LineTo(new Point(cx + 4, cy + 2), true, false);
                }
                g.Freeze();
                dc.DrawGeometry(null, pen, g);
                return true;
            }
            default:
                return false;
        }
    }

    private void DrawActionCues(DrawingContext dc, double bodyTop, double bodyBottom, double bodyW, double eyeY, double eyeSep, double lookX,
        Color accent, Color detail, Color ink, double phase)
    {
        double lx = lookX - eyeSep, rx = lookX + eyeSep;
        switch (mood)
        {
            case PetMood.Crying:
            {
                // Waterfall tears from both eyes, a puddle, and sob drops flying off.
                Color tear = TearBlue;
                foreach (int s in new[] { -1, 1 })
                {
                    double x0 = (s < 0 ? lx : rx) + s * 1.2;
                    var stream = new StreamGeometry();
                    using (var c = stream.Open())
                    {
                        double sway = Math.Sin(phase * 5 + s) * 1.2;
                        c.BeginFigure(new Point(x0 - 1.4, eyeY + 1.5), true, true);
                        c.BezierTo(new Point(x0 - 1.6 + sway, eyeY + 8), new Point(x0 - 2.4 + sway, bodyBottom - 6), new Point(x0 - 2.8 + sway, bodyBottom + 1), true, false);
                        c.LineTo(new Point(x0 + 2.8 + sway, bodyBottom + 1), true, false);
                        c.BezierTo(new Point(x0 + 2.4 + sway, bodyBottom - 6), new Point(x0 + 1.6 + sway, eyeY + 8), new Point(x0 + 1.4, eyeY + 1.5), true, false);
                    }
                    stream.Freeze();
                    var fill = new LinearGradientBrush(Color.FromArgb(235, tear.R, tear.G, tear.B), Color.FromArgb(150, tear.R, tear.G, tear.B), 90);
                    fill.Freeze();
                    dc.DrawGeometry(fill, null, stream);
                    // Shine streaks running down.
                    double t = (phase * 1.4 + (s + 1) * .3) % 1.0;
                    double y = eyeY + 4 + t * (bodyBottom - eyeY - 6);
                    dc.DrawEllipse(FrozenBrush(Color.FromArgb(200, 255, 255, 255)), null, new Point(x0 + Math.Sin(phase * 5 + s) * 1.2 * t, y), .7, 1.6);
                    for (int i = 0; i < 2; i++)
                    {
                        double ft = (phase * .9 + i * .5 + (s + 1) * .25) % 1.0;
                        double fx = x0 + s * (3 + ft * 14), fy = eyeY - ft * 8 + ft * ft * 22;
                        DrawDrop(dc, fx, fy, 1.4, Color.FromArgb((byte)(220 * (1 - ft)), tear.R, tear.G, tear.B));
                    }
                }
                double puddle = 1 + Math.Sin(phase * 2) * .06;
                dc.DrawEllipse(FrozenBrush(Color.FromArgb(120, tear.R, tear.G, tear.B)), null, new Point(0, bodyBottom + 3.2), bodyW * .62 * puddle, 3.2 * puddle);
                break;
            }
            case PetMood.Laughing:
            {
                // Tears of joy and bouncing HA text.
                DrawDrop(dc, lx - 4.4, eyeY + 1 + (phase * 1.2 % 1.0) * 4, 1.4, Color.FromArgb(210, TearBlue.R, TearBlue.G, TearBlue.B));
                DrawDrop(dc, rx + 4.4, eyeY + 1 + ((phase * 1.2 + .5) % 1.0) * 4, 1.4, Color.FromArgb(210, TearBlue.R, TearBlue.G, TearBlue.B));
                var face = new Typeface(new FontFamily("Segoe UI Black"), FontStyles.Normal, FontWeights.Black, FontStretches.Normal);
                for (int i = 0; i < 3; i++)
                {
                    double t = (phase * .5 + i / 3.0) % 1.0;
                    double x = (i - 1) * bodyW * .5 + Math.Sin(phase * 3 + i) * 2;
                    double y = bodyTop - 6 - t * 18;
                    byte a = (byte)(255 * Math.Sin(t * Math.PI));
                    var text = new FormattedText("HA", CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight, face, 8.5 + i,
                        FrozenBrush(Color.FromArgb(a, JoyYellow.R, JoyYellow.G, JoyYellow.B)), 1.0);
                    dc.PushTransform(new RotateTransform((i - 1) * 14, x, y));
                    var outline = text.BuildGeometry(new Point(x - text.Width / 2, y - text.Height / 2));
                    dc.DrawGeometry(FrozenBrush(Color.FromArgb(a, JoyYellow.R, JoyYellow.G, JoyYellow.B)), FrozenPen(Color.FromArgb(a, ink.R, ink.G, ink.B), .6), outline);
                    dc.Pop();
                }
                break;
            }
            case PetMood.Surprised:
            {
                double bob = Math.Abs(Math.Sin(phase * 4)) * 2.5;
                var face = new Typeface(new FontFamily("Segoe UI Black"), FontStyles.Normal, FontWeights.Black, FontStretches.Normal);
                var text = new FormattedText("!", CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight, face, 16, FrozenBrush(DangerOrAccent(accent)), 1.0);
                dc.DrawText(text, new Point(bodyW * .36, bodyTop - 20 - bob));
                // Shock lines.
                Pen shock = FrozenPen(Color.FromArgb(200, ink.R, ink.G, ink.B), 1.1);
                for (int i = -1; i <= 1; i++)
                {
                    double ang = -Math.PI / 2 + i * .55;
                    dc.DrawLine(shock, new Point(Math.Cos(ang) * (bodyW * .55), bodyTop + 6 + Math.Sin(ang) * 10),
                        new Point(Math.Cos(ang) * (bodyW * .55 + 6), bodyTop + 6 + Math.Sin(ang) * 16));
                }
                break;
            }
            case PetMood.Eating:
            {
                DrawFood(dc, heldFood, lookX + (lookX >= 0 ? 6.5 : -6.5), eyeY + 9, 1.0, phase);
                // Crumbs.
                Brush crumb = FrozenBrush(Color.FromArgb(210, 0xD9, 0x9A, 0x4E));
                for (int i = 0; i < 4; i++)
                {
                    double t = (phase * .8 + i * .25) % 1.0;
                    dc.DrawEllipse(crumb, null, new Point(lookX + (i - 1.5) * 3.6, eyeY + 8 + t * (bodyBottom - eyeY - 6)), .9, .9);
                }
                // Little "nom" hearts.
                double ht = (phase * .4) % 1.0;
                DrawHeartShape(dc, bodyW * .4, bodyTop - 4 - ht * 12, 2.6, FrozenBrush(Color.FromArgb((byte)(230 * (1 - ht)), LovePink.R, LovePink.G, LovePink.B)), null);
                break;
            }
            case PetMood.Yawning:
            {
                var face = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                for (int i = 0; i < 2; i++)
                {
                    double t = (phase * .35 + i * .5) % 1.0;
                    var z = new FormattedText("z", CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight, face, 7 + i * 3,
                        FrozenBrush(Color.FromArgb((byte)(220 * (1 - t)), accent.R, accent.G, accent.B)), 1.0);
                    dc.DrawText(z, new Point(bodyW * .38 + t * 8, bodyTop - 6 - t * 14 - i * 4));
                }
                DrawDrop(dc, rx + 4.4, eyeY + 1.6, 1.2, Color.FromArgb(180, TearBlue.R, TearBlue.G, TearBlue.B));
                break;
            }
            case PetMood.Nervous:
            {
                // Sweat drops sliding down.
                for (int i = 0; i < 2; i++)
                {
                    double t = (phase * .6 + i * .5) % 1.0;
                    double x = (i == 0 ? -1 : 1) * bodyW * .4;
                    DrawDrop(dc, x, bodyTop + 3 + t * 10, 1.9, Color.FromArgb((byte)(230 * (1 - t * .6)), TearBlue.R, TearBlue.G, TearBlue.B));
                }
                Pen wob = FrozenPen(Color.FromArgb(150, ink.R, ink.G, ink.B), .9);
                for (int i = 0; i < 3; i++) DrawWave(dc, 0, bodyTop - 5 - i * 3.2, 4 + i * 1.5, wob);
                break;
            }
            case PetMood.Fired:
            {
                // Flickering flame aura behind the head.
                for (int i = 0; i < 7; i++)
                {
                    double t = (phase * 1.3 + i / 7.0) % 1.0;
                    double x = (i - 3) * bodyW * .14 + Math.Sin(phase * 6 + i) * 1.6;
                    double y = bodyTop + 4 - t * 16;
                    double r = (1 - t) * 4 + 1;
                    Color c = EffectPalette.Blend(FireOrange, JoyYellow, t);
                    dc.DrawEllipse(FrozenBrush(Color.FromArgb((byte)(200 * (1 - t)), c.R, c.G, c.B)), null, new Point(x, y), r * .8, r * 1.3);
                }
                break;
            }
            case PetMood.Hurt:
            {
                // Impact star and swirl.
                double x = -bodyW * .5, y = bodyTop + 6;
                DrawStarShape(dc, x, y, 6 + Math.Sin(phase * 12), FrozenBrush(Color.FromArgb(235, JoyYellow.R, JoyYellow.G, JoyYellow.B)), FrozenPen(ink, .8));
                Pen streak = FrozenPen(Color.FromArgb(180, ink.R, ink.G, ink.B), 1);
                for (int i = 0; i < 3; i++) dc.DrawLine(streak, new Point(x - 6, y - 5 + i * 5), new Point(x - 12, y - 7 + i * 6));
                break;
            }
        }
    }

    private static Color DangerOrAccent(Color accent) => AppearanceOptions.RelativeLuminance(accent) > .6 ? Color.FromRgb(0xEF, 0x44, 0x44) : accent;

    /// <summary>Draws one of the mini-game foods centred on a point. Also used by the arena's food buttons.</summary>
    internal static void DrawFood(DrawingContext dc, string food, double x, double y, double scale, double phase = 0)
    {
        dc.PushTransform(new ScaleTransform(scale, scale, x, y));
        Pen dark = FrozenPen(Color.FromArgb(160, 0x3F, 0x2A, 0x1C), .7);
        switch (food)
        {
            case "fish":
                dc.DrawEllipse(FrozenBrush(Color.FromRgb(0x60, 0xA5, 0xFA)), dark, new Point(x - 1, y), 4.6, 2.6);
                dc.DrawPolygon(FrozenBrush(Color.FromRgb(0x3B, 0x82, 0xF6)), dark, new Point(x + 3, y), new Point(x + 6.5, y - 2.6), new Point(x + 6.5, y + 2.6));
                dc.DrawEllipse(FrozenBrush(Colors.White), null, new Point(x - 3.4, y - .6), .9, .9);
                break;
            case "cake":
                dc.DrawPolygon(FrozenBrush(Color.FromRgb(0xFD, 0xE6, 0x8A)), dark, new Point(x - 4.6, y + 3), new Point(x + 4.6, y + 3), new Point(x + 4.6, y - 1), new Point(x - 4.6, y - 3));
                dc.DrawPolygon(FrozenBrush(Color.FromRgb(0xF9, 0xA8, 0xD4)), null, new Point(x - 4.6, y - 3), new Point(x + 4.6, y - 1), new Point(x + 4.6, y + .4), new Point(x - 4.6, y - 1.4));
                dc.DrawEllipse(FrozenBrush(Color.FromRgb(0xEF, 0x44, 0x44)), null, new Point(x, y - 3.6), 1.3, 1.3);
                break;
            case "pepper":
                dc.DrawEllipse(FrozenBrush(Color.FromRgb(0xEF, 0x44, 0x44)), dark, new Point(x, y + .6), 2.6, 4.2);
                dc.DrawLine(FrozenPen(Color.FromRgb(0x16, 0xA3, 0x4A), 1.2), new Point(x, y - 3.4), new Point(x + 1.4, y - 5.4));
                break;
            case "tea":
                dc.DrawRoundedRectangle(FrozenBrush(Color.FromRgb(0xA7, 0xF3, 0xD0)), dark, new Rect(x - 3.6, y - 3, 7.2, 6.4), 1.6, 1.6);
                dc.DrawEllipse(null, FrozenPen(Color.FromRgb(0x10, 0xB9, 0x81), 1), new Point(x + 4.4, y), 1.6, 1.8);
                dc.DrawLine(FrozenPen(Color.FromArgb(150, 255, 255, 255), .8), new Point(x - 1, y - 4.4 - Math.Sin(phase * 3) * .6), new Point(x, y - 6.4));
                break;
            case "soda":
                dc.DrawRoundedRectangle(FrozenBrush(Color.FromRgb(0xFA, 0xCC, 0x15)), dark, new Rect(x - 2.8, y - 4.6, 5.6, 9.2), 1.4, 1.4);
                dc.DrawPolygon(FrozenBrush(Color.FromRgb(0x1D, 0x23, 0x26)), null, new Point(x + .6, y - 2.8), new Point(x - 1.2, y + .4), new Point(x + .2, y + .4), new Point(x - .6, y + 3), new Point(x + 1.4, y - .6), new Point(x, y - .6));
                break;
            case "golden":
                dc.DrawEllipse(FrozenBrush(Color.FromRgb(0xFA, 0xCC, 0x15)), FrozenPen(Color.FromRgb(0xB4, 0x53, 0x09), .8), new Point(x, y + .6), 4.4, 4.0);
                dc.DrawLine(FrozenPen(Color.FromRgb(0x78, 0x35, 0x0F), 1), new Point(x, y - 3.2), new Point(x + .6, y - 5.2));
                dc.DrawEllipse(FrozenBrush(Color.FromRgb(0x22, 0xC5, 0x5E)), null, new Point(x + 2.2, y - 4.6), 1.8, .9);
                dc.DrawEllipse(FrozenBrush(Color.FromArgb(200, 255, 255, 255)), null, new Point(x - 1.6, y - .8), 1, 1.4);
                break;
            case "candy":
                DrawStarShape(dc, x, y, 5, FrozenBrush(Color.FromRgb(0xC0, 0x84, 0xFC)), FrozenPen(Color.FromRgb(0x7C, 0x3A, 0xED), .8));
                dc.DrawEllipse(FrozenBrush(Color.FromArgb(220, 255, 255, 255)), null, new Point(x - 1.2, y - 1.4), .9, .9);
                break;
            default: // berry
                dc.DrawEllipse(FrozenBrush(Color.FromRgb(0xDB, 0x27, 0x77)), dark, new Point(x - 1.6, y + .8), 2.6, 2.6);
                dc.DrawEllipse(FrozenBrush(Color.FromRgb(0xBE, 0x18, 0x5D)), dark, new Point(x + 1.8, y + 1.2), 2.6, 2.6);
                dc.DrawEllipse(FrozenBrush(Color.FromArgb(200, 255, 255, 255)), null, new Point(x - 2.2, y), .8, .8);
                dc.DrawLine(FrozenPen(Color.FromRgb(0x16, 0xA3, 0x4A), 1.1), new Point(x, y - 1.4), new Point(x + 1, y - 4.2));
                break;
        }
        dc.Pop();
    }
}

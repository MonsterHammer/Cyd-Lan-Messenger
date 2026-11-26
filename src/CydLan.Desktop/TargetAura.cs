using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Application = System.Windows.Application;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using ListBoxItem = System.Windows.Controls.ListBoxItem;
using Pen = System.Windows.Media.Pen;
using Point = System.Windows.Point;

namespace CydLan.Desktop;

public enum TargetAuraKind { Off, Fire, Smoke, Aura, Embers, Arcane, Frost, Electric, Neon, Sakura, Prism, Pulse }

public enum TargetAuraRole { None, Preview, Module, Person, Button }

public sealed record TargetAuraSwatch(string Value, string Name, TargetAuraKind Kind);

/// <summary>Renders animated selection and button highlight auras around the selected module, selected conversation row, and highlight buttons.</summary>
public sealed class TargetAura : AmbientEffect
{
    public const string Off = "off";
    public const string Fire = "fire";
    public const string Smoke = "smoke";
    public const string Aura = "aura";
    public const string Embers = "embers";
    public const string Arcane = "arcane";
    public const string Frost = "frost";
    public const string Electric = "electric";
    public const string Neon = "neon";
    public const string Sakura = "sakura";
    public const string Prism = "prism";
    public const string Pulse = "pulse";

    public static IReadOnlyList<TargetAuraSwatch> Catalog { get; } =
    [
        new TargetAuraSwatch(Off, "None", TargetAuraKind.Off),
        new TargetAuraSwatch(Fire, "Fire", TargetAuraKind.Fire),
        new TargetAuraSwatch(Smoke, "Smoke", TargetAuraKind.Smoke),
        new TargetAuraSwatch(Aura, "Aura", TargetAuraKind.Aura),
        new TargetAuraSwatch(Embers, "Embers", TargetAuraKind.Embers),
        new TargetAuraSwatch(Arcane, "Arcane", TargetAuraKind.Arcane),
        new TargetAuraSwatch(Frost, "Frost", TargetAuraKind.Frost),
        new TargetAuraSwatch(Electric, "Electric", TargetAuraKind.Electric),
        new TargetAuraSwatch(Neon, "Neon", TargetAuraKind.Neon),
        new TargetAuraSwatch(Sakura, "Sakura", TargetAuraKind.Sakura),
        new TargetAuraSwatch(Prism, "Prism", TargetAuraKind.Prism),
        new TargetAuraSwatch(Pulse, "Pulse", TargetAuraKind.Pulse)
    ];

    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(TargetAuraKind), typeof(TargetAura),
        new FrameworkPropertyMetadata(TargetAuraKind.Off, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty EdgeInsetProperty = DependencyProperty.Register(
        nameof(EdgeInset), typeof(Thickness), typeof(TargetAura),
        new FrameworkPropertyMetadata(new Thickness(0), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SurfaceColorProperty = DependencyProperty.Register(
        nameof(SurfaceColor), typeof(Color), typeof(TargetAura),
        new FrameworkPropertyMetadata(Color.FromRgb(24, 28, 34), FrameworkPropertyMetadataOptions.AffectsRender,
            (d, _) => ((TargetAura)d).OnTintChanged()));

    public static readonly DependencyProperty RoleProperty = DependencyProperty.RegisterAttached(
        "Role", typeof(TargetAuraRole), typeof(TargetAura), new PropertyMetadata(TargetAuraRole.None));

    public static readonly DependencyProperty TrackSelectionProperty = DependencyProperty.RegisterAttached(
        "TrackSelection", typeof(bool), typeof(TargetAura), new PropertyMetadata(false, OnTrackSelectionChanged));

    private static readonly ParticleSeed[] Seeds = CreateSeeds();
    private static readonly Dictionary<ListBoxItem, Action> RowSubscriptions = [];
    private static readonly Dictionary<(byte Kind, Color Color), BitmapSource[]> SpriteCache = [];

    public static TargetAuraKind ActiveKind { get; private set; }
    public static double ActiveIntensity { get; private set; } = .70;
    public static bool OnModule { get; private set; } = true;
    public static bool OnPerson { get; private set; } = true;
    public static bool OnButtons { get; private set; } = true;
    internal static event Action? StateChanged;

    private BitmapSource[]? flameOuterSprites;
    private BitmapSource[]? flameCoreSprites;
    private BitmapSource[]? smokeSprites;
    private BitmapSource[]? auraSprites;
    private BitmapSource[]? arcaneAltSprites;
    private Pen[]? accentPens;
    private Pen[]? altPens;
    private Brush[]? coreFills;

    public TargetAuraKind Kind { get => (TargetAuraKind)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public Thickness EdgeInset { get => (Thickness)GetValue(EdgeInsetProperty); set => SetValue(EdgeInsetProperty, value); }
    public Color SurfaceColor { get => (Color)GetValue(SurfaceColorProperty); set => SetValue(SurfaceColorProperty, value); }

    protected override int FrameRate => Kind == TargetAuraKind.Smoke ? 24 : 30;

    public TargetAura()
    {
        SetResourceReference(SurfaceColorProperty, "ChromeSurfaceColor");
    }

    public static TargetAuraRole GetRole(DependencyObject obj) => (TargetAuraRole)obj.GetValue(RoleProperty);
    public static void SetRole(DependencyObject obj, TargetAuraRole value) => obj.SetValue(RoleProperty, value);
    public static bool GetTrackSelection(DependencyObject obj) => (bool)obj.GetValue(TrackSelectionProperty);
    public static void SetTrackSelection(DependencyObject obj, bool value) => obj.SetValue(TrackSelectionProperty, value);

    public static string Normalize(string? value) => value is Fire or Smoke or Aura or Embers or Arcane
        or Frost or Electric or Neon or Sakura or Prism or Pulse ? value : Off;

    public static TargetAuraKind KindFor(string? value) => Normalize(value) switch
    {
        Fire => TargetAuraKind.Fire,
        Smoke => TargetAuraKind.Smoke,
        Aura => TargetAuraKind.Aura,
        Embers => TargetAuraKind.Embers,
        Arcane => TargetAuraKind.Arcane,
        Frost => TargetAuraKind.Frost,
        Electric => TargetAuraKind.Electric,
        Neon => TargetAuraKind.Neon,
        Sakura => TargetAuraKind.Sakura,
        Prism => TargetAuraKind.Prism,
        Pulse => TargetAuraKind.Pulse,
        _ => TargetAuraKind.Off
    };

    public static void Configure(string? style, double intensity, bool onModule, bool onPerson, bool onButtons)
    {
        ActiveKind = KindFor(style);
        ActiveIntensity = Math.Clamp(intensity, .1, 1);
        OnModule = onModule;
        OnPerson = onPerson;
        OnButtons = onButtons;
        StateChanged?.Invoke();
    }

    public static void RefreshTheme() => StateChanged?.Invoke();

    internal static void ApplyToRow(ListBoxItem item)
    {
        if (item.Template?.FindName("RowAura", item) is not TargetAura rowAura) return;
        bool active = ActiveKind != TargetAuraKind.Off && OnPerson && item.IsSelected;
        rowAura.Kind = ActiveKind;
        rowAura.Intensity = ActiveIntensity;
        rowAura.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
    }

    protected override void OnTintChanged()
    {
        flameOuterSprites = null;
        flameCoreSprites = null;
        smokeSprites = null;
        auraSprites = null;
        arcaneAltSprites = null;
        accentPens = null;
        altPens = null;
        coreFills = null;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double width = ActualWidth, height = ActualHeight;
        var kind = Kind;
        if (kind == TargetAuraKind.Off || width < 16 || height < 16) return;
        double intensity = Math.Clamp(Intensity, .1, 1);

        switch (kind)
        {
            case TargetAuraKind.Fire: DrawFire(dc, width, height, intensity); break;
            case TargetAuraKind.Smoke: DrawSmoke(dc, width, height, intensity); break;
            case TargetAuraKind.Aura: DrawAura(dc, width, height, intensity); break;
            case TargetAuraKind.Embers: DrawEmbers(dc, width, height, intensity); break;
            case TargetAuraKind.Arcane: DrawArcane(dc, width, height, intensity); break;
            case TargetAuraKind.Frost: DrawFrost(dc, width, height, intensity); break;
            case TargetAuraKind.Electric: DrawElectric(dc, width, height, intensity); break;
            case TargetAuraKind.Neon: DrawNeon(dc, width, height, intensity); break;
            case TargetAuraKind.Sakura: DrawSakura(dc, width, height, intensity); break;
            case TargetAuraKind.Prism: DrawPrism(dc, width, height, intensity); break;
            case TargetAuraKind.Pulse: DrawPulse(dc, width, height, intensity); break;
        }
    }

    private Rect TargetBounds(double width, double height)
    {
        var pad = EdgeInset;
        double x = Math.Clamp(pad.Left, 0, width * .3);
        double y = Math.Clamp(pad.Top, 0, height * .3);
        double right = Math.Clamp(pad.Right, 0, width * .3);
        double bottom = Math.Clamp(pad.Bottom, 0, height * .3);
        return new Rect(x, y, Math.Max(10, width - x - right), Math.Max(10, height - y - bottom));
    }

    private void DrawFire(DrawingContext dc, double width, double height, double intensity)
    {
        Color fireOuter = EffectPalette.Blend(Color.FromRgb(255, 78, 22), Tint, .14);
        Color fireCore = EffectPalette.Blend(Color.FromRgb(255, 212, 88), Tint, .08);
        flameOuterSprites ??= BuildOrbSprites(fireOuter);
        flameCoreSprites ??= BuildOrbSprites(fireCore);
        coreFills ??= BuildSolidBrushes(fireCore);

        var target = TargetBounds(width, height);
        bool rowMode = EdgeInset.Top < 1 && target.Width > 140;
        double time = Time * Math.Tau;

        // Warm glowing ember bed along the base and edges
        int bedCount = rowMode ? 8 : 5;
        for (int b = 0; b < bedCount; b++)
        {
            double u = (b + .5) / bedCount;
            double flicker = .72 + .28 * Math.Sin(time * 9 + b * 2.1);
            double bx = target.Left + target.Width * (.08 + .84 * u);
            double by = target.Bottom - 2;
            double rad = (rowMode ? 22 : 16) * (.8 + .35 * intensity) * flicker;
            dc.DrawImage(flameOuterSprites[EffectPalette.OpacityIndex(.28 * intensity * flicker)],
                new Rect(bx - rad, by - rad * .9, rad * 2, rad * 1.4));
        }

        // Lick flames rising smoothly from lower base and sides without hard top clipping
        int count = rowMode ? 18 : 13;
        for (int i = 0; i < count; i++)
        {
            var seed = Seeds[i];
            double rise = (seed.Start + Time * (4.2 + seed.Speed * 2.6)) % 1;
            double envelope = Math.Sin(rise * Math.PI);
            double wave = Math.Sin((rise * 3 + seed.Phase) * Math.Tau + time * 4) * (rowMode ? 5 : 3.5);
            double x = target.Left + target.Width * (.06 + .88 * seed.Lane) + wave;
            double travel = EdgeInset.Top > 2 ? target.Height + EdgeInset.Top * .7 : target.Height * .78;
            double y = target.Bottom - 2 - rise * travel;

            double flameW = (6 + 5 * seed.Size) * (1 - rise * .45) * (.75 + .35 * intensity);
            double flameH = flameW * (1.55 + .45 * Math.Sin(time * 10 + i));
            double alpha = envelope * (.45 + .50 * intensity);

            dc.DrawImage(flameOuterSprites[EffectPalette.OpacityIndex(alpha)],
                new Rect(x - flameW, y - flameH * .7, flameW * 2, flameH * 1.4));
            if (rise < .68)
            {
                double coreR = flameW * .56;
                dc.DrawImage(flameCoreSprites[EffectPalette.OpacityIndex(alpha * .88)],
                    new Rect(x - coreR, y - coreR * 1.2, coreR * 2, coreR * 1.8));
            }
        }

        // Crisp flying ember sparks
        int sparks = rowMode ? 8 : 6;
        for (int i = 0; i < sparks; i++)
        {
            var seed = Seeds[(i + 19) % Seeds.Length];
            double rise = (seed.Start * 1.3 + Time * (5.5 + seed.Speed * 3.0)) % 1;
            double x = target.Left + target.Width * (.08 + .84 * seed.Lane) + Math.Sin(rise * 12 + seed.Phase) * 6;
            double y = target.Bottom - 4 - rise * (height - 6);
            double r = .9 + .7 * seed.Size;
            double alpha = Math.Sin(rise * Math.PI) * (.55 + .45 * intensity);
            dc.DrawEllipse(coreFills[EffectPalette.OpacityIndex(alpha)], null, new Point(x, y), r, r);
        }
    }

    private void DrawSmoke(DrawingContext dc, double width, double height, double intensity)
    {
        double luma = (0.2126 * SurfaceColor.R + 0.7152 * SurfaceColor.G + 0.0722 * SurfaceColor.B) / 255.0;
        bool isLight = luma >= .50;
        Color vaporBase = isLight
            ? EffectPalette.Blend(Color.FromRgb(74, 82, 94), Tint, .22)
            : EffectPalette.Blend(Color.FromRgb(220, 228, 238), Tint, .24);
        smokeSprites ??= BuildSmokeSprites(vaporBase);
        accentPens ??= BuildPens(vaporBase, 1.3);

        var target = TargetBounds(width, height);
        bool rowMode = EdgeInset.Top < 1 && target.Width > 140;
        int puffs = rowMode ? 16 : 12;

        for (int i = 0; i < puffs; i++)
        {
            var seed = Seeds[i];
            double rise = (seed.Start + Time * (1.8 + seed.Speed * 1.1)) % 1;
            double envelope = Math.Sin(rise * Math.PI);
            double driftX = Math.Sin((rise * 1.5 + seed.Phase) * Math.Tau) * (rowMode ? 14 : 7) + (rise - .3) * (rowMode ? 16 : 6);
            double x = target.Left + target.Width * (.08 + .84 * seed.Lane) + driftX;
            double travel = EdgeInset.Top > 2 ? target.Height + EdgeInset.Top * .75 : target.Height * .76;
            double y = target.Bottom - 3 - rise * travel;

            double radius = (rowMode ? 14 : 11) * (.65 + .95 * rise) * (.7 + .4 * seed.Size);
            double alpha = envelope * (isLight ? .30 : .34) * (.45 + .55 * intensity);
            dc.DrawImage(smokeSprites[EffectPalette.OpacityIndex(alpha)],
                new Rect(x - radius, y - radius * .85, radius * 2, radius * 1.7));
        }

        // Slow undulating mist ribbon along the bottom/left contour
        double wavePhase = Time * Math.Tau * 2;
        var wisp = new StreamGeometry();
        using (var ctx = wisp.Open())
        {
            const int segs = 22;
            for (int s = 0; s <= segs; s++)
            {
                double u = s / (double)segs;
                double wx = target.Left + 6 + (target.Width - 12) * u;
                double wy = target.Bottom - 8 - Math.Sin(u * Math.Tau * 1.6 - wavePhase) * 4.5 - Math.Sin(u * Math.PI) * 5;
                var pt = new Point(wx, Math.Max(4, wy));
                if (s == 0) ctx.BeginFigure(pt, isFilled: false, isClosed: false);
                else ctx.LineTo(pt, isStroked: true, isSmoothJoin: true);
            }
        }
        wisp.Freeze();
        dc.DrawGeometry(null, accentPens[EffectPalette.OpacityIndex(.26 * intensity)], wisp);
    }

    private void DrawAura(DrawingContext dc, double width, double height, double intensity)
    {
        auraSprites ??= BuildOrbSprites(EffectPalette.Blend(Tint, Colors.White, .28));
        accentPens ??= BuildPens(Tint, 1.5);
        altPens ??= BuildPens(EffectPalette.Blend(Tint, Colors.White, .62), 1.0);
        coreFills ??= BuildSolidBrushes(EffectPalette.Blend(Tint, Colors.White, .72));

        var target = TargetBounds(width, height);
        double breathe = .62 + .38 * Math.Sin(Time * Math.Tau * 4);
        double corner = Math.Min(target.Height * .32, 10);

        // Breathing luminous energy rings hugging the element frame
        var glowRect = target;
        glowRect.Inflate(EdgeInset.Left > 2 ? 2.5 : -1, EdgeInset.Top > 2 ? 2.5 : -1);
        dc.DrawRoundedRectangle(null, accentPens[EffectPalette.OpacityIndex(.48 * intensity * breathe)], glowRect, corner, corner);
        var innerRect = target;
        innerRect.Inflate(-1.5, -1.5);
        if (innerRect.Width > 8 && innerRect.Height > 8)
            dc.DrawRoundedRectangle(null, altPens[EffectPalette.OpacityIndex(.32 * intensity * (1.2 - breathe * .4))], innerRect, corner, corner);

        // Soft volumetric aura nodes breathing around corners & sides
        int halos = target.Width > 140 ? 6 : 4;
        for (int i = 0; i < halos; i++)
        {
            double angle = (Time * 2 + i / (double)halos) * Math.Tau;
            double rx = target.Left + target.Width * (.5 + .44 * Math.Cos(angle));
            double ry = target.Top + target.Height * (.5 + .38 * Math.Sin(angle));
            double r = (target.Height * .45 + 6) * (.75 + .25 * intensity);
            dc.DrawImage(auraSprites[EffectPalette.OpacityIndex(.28 * intensity * breathe)],
                new Rect(rx - r, ry - r, r * 2, r * 2));
        }

        // Two bright orbital wisps gliding smoothly along the perimeter
        for (int o = 0; o < 2; o++)
        {
            double t = (Time * 4 + o * .5) % 1;
            Point pt = PerimeterPoint(glowRect, t);
            double r = 9 * (.7 + .4 * intensity);
            dc.DrawImage(auraSprites[EffectPalette.OpacityIndex(.65 * intensity)], new Rect(pt.X - r, pt.Y - r, r * 2, r * 2));
            dc.DrawEllipse(coreFills[EffectPalette.OpacityIndex(.80 * intensity)], null, pt, 1.7, 1.7);
        }
    }

    private void DrawEmbers(DrawingContext dc, double width, double height, double intensity)
    {
        Color magma = EffectPalette.Blend(Color.FromRgb(235, 62, 28), Tint, .16);
        Color cinder = EffectPalette.Blend(Color.FromRgb(255, 194, 72), Tint, .12);
        flameOuterSprites ??= BuildOrbSprites(magma);
        flameCoreSprites ??= BuildOrbSprites(cinder);
        accentPens ??= BuildPens(magma, 1.4);
        coreFills ??= BuildSolidBrushes(cinder);

        var target = TargetBounds(width, height);
        double pulse = .6 + .4 * Math.Sin(Time * Math.Tau * 3);
        var rim = target;
        rim.Inflate(EdgeInset.Left > 2 ? 1.5 : -1, EdgeInset.Top > 2 ? 1.5 : -1);
        dc.DrawRoundedRectangle(null, accentPens[EffectPalette.OpacityIndex(.40 * intensity * pulse)], rim, 8, 8);

        int embers = target.Width > 140 ? 18 : 13;
        for (int i = 0; i < embers; i++)
        {
            var seed = Seeds[i];
            double rise = (seed.Start + Time * (2.6 + seed.Speed * 1.9)) % 1;
            double x = target.Left + 4 + (target.Width - 8) * seed.Lane + Math.Sin((rise * 2.5 + seed.Phase) * Math.Tau) * 5;
            double travel = EdgeInset.Top > 2 ? target.Height + EdgeInset.Top * .75 : target.Height * .82;
            double y = target.Bottom - 2 - rise * travel;
            double glow = .45 + .55 * Math.Pow(.5 + .5 * Math.Cos((Time * 8 + seed.Start) * Math.Tau), 2);
            double alpha = Math.Sin(rise * Math.PI) * glow * (.45 + .55 * intensity);
            double haloR = 5.5 + 4 * seed.Size;

            dc.DrawImage(flameOuterSprites[EffectPalette.OpacityIndex(alpha * .55)], new Rect(x - haloR, y - haloR, haloR * 2, haloR * 2));
            dc.DrawEllipse(coreFills[EffectPalette.OpacityIndex(alpha)], null, new Point(x, y), 1.1 + .7 * seed.Size, 1.1 + .7 * seed.Size);
        }
    }

    private void DrawArcane(DrawingContext dc, double width, double height, double intensity)
    {
        Color shift = Color.FromRgb(
            (byte)((Tint.G * .6 + 145) % 255),
            (byte)((Tint.B * .5 + 95) % 255),
            (byte)Math.Min(255, Tint.R * .4 + 210));
        auraSprites ??= BuildOrbSprites(Tint);
        arcaneAltSprites ??= BuildOrbSprites(shift);
        accentPens ??= BuildPens(Tint, 1.3);
        altPens ??= BuildPens(shift, 1.2);
        coreFills ??= BuildSolidBrushes(Colors.White);

        var target = TargetBounds(width, height);
        bool rowMode = EdgeInset.Top < 1 && target.Width > 140;
        double corner = Math.Min(target.Height * .32, 10);
        double shimmer = .55 + .45 * Math.Sin(Time * Math.Tau * 5);

        var arcRect = target;
        arcRect.Inflate(EdgeInset.Left > 2 ? 2.0 : -1, EdgeInset.Top > 2 ? 2.0 : -1);
        dc.DrawRoundedRectangle(null, altPens[EffectPalette.OpacityIndex(.38 * intensity * shimmer)], arcRect, corner, corner);

        // Counter-rotating orbital comet tails & cross-sweeps
        for (int k = 0; k < 3; k++)
        {
            double head = (Time * (3.5 + k * .6) + k / 3.0) % 1;
            var trail = new StreamGeometry();
            using (var ctx = trail.Open())
            {
                const int steps = 10;
                for (int s = 0; s <= steps; s++)
                {
                    double t = (head - (s / (double)steps) * .18 + 1) % 1;
                    Point p = PerimeterPoint(arcRect, t);
                    if (s == 0) ctx.BeginFigure(p, isFilled: false, isClosed: false);
                    else ctx.LineTo(p, isStroked: true, isSmoothJoin: true);
                }
            }
            trail.Freeze();
            dc.DrawGeometry(null, (k % 2 == 0 ? accentPens : altPens)[EffectPalette.OpacityIndex(.58 * intensity)], trail);

            Point headPt = PerimeterPoint(arcRect, head);
            double orbR = (rowMode ? 11 : 9) * (.75 + .35 * intensity);
            var sprites = k % 2 == 0 ? auraSprites : arcaneAltSprites;
            dc.DrawImage(sprites[EffectPalette.OpacityIndex(.68 * intensity)], new Rect(headPt.X - orbR, headPt.Y - orbR, orbR * 2, orbR * 2));
            dc.DrawEllipse(coreFills[EffectPalette.OpacityIndex(.82 * intensity)], null, headPt, 1.6, 1.6);
        }

        // Floating arcane motes inside frame
        int count = rowMode ? 10 : 7;
        for (int i = 0; i < count; i++)
        {
            var seed = Seeds[i];
            double rise = (seed.Start + Time * (3.2 + seed.Speed)) % 1;
            double x = target.Left + 6 + (target.Width - 12) * seed.Lane + Math.Sin(rise * 8 + seed.Phase) * 5;
            double y = target.Top + 4 + (target.Height - 8) * (1 - rise);
            double twinkle = .35 + .65 * Math.Pow(.5 + .5 * Math.Sin((Time * 9 + seed.Start) * Math.Tau), 2);
            double alpha = Math.Sin(rise * Math.PI) * twinkle * (.45 + .5 * intensity);
            double r = 5.5 + 3 * seed.Size;
            dc.DrawImage((i % 2 == 0 ? auraSprites : arcaneAltSprites)[EffectPalette.OpacityIndex(alpha * .65)],
                new Rect(x - r, y - r, r * 2, r * 2));
        }
    }

    private void DrawFrost(DrawingContext dc, double width, double height, double intensity)
    {
        Color ice = EffectPalette.Blend(Color.FromRgb(120, 218, 255), Tint, .22);
        Color crystal = Color.FromRgb(235, 248, 255);
        auraSprites ??= BuildOrbSprites(ice);
        accentPens ??= BuildPens(ice, 1.45);
        altPens ??= BuildPens(crystal, 1.0);
        coreFills ??= BuildSolidBrushes(crystal);

        var target = TargetBounds(width, height);
        double corner = Math.Min(target.Height * .32, 10);
        double shimmer = .65 + .35 * Math.Sin(Time * Math.Tau * 3);
        var rime = target;
        rime.Inflate(EdgeInset.Left > 2 ? 2 : -1, EdgeInset.Top > 2 ? 2 : -1);
        dc.DrawRoundedRectangle(null, accentPens[EffectPalette.OpacityIndex(.52 * intensity * shimmer)], rime, corner, corner);
        dc.DrawRoundedRectangle(null, altPens[EffectPalette.OpacityIndex(.36 * intensity)], target, corner, corner);

        int shards = target.Width > 140 ? 12 : 8;
        for (int i = 0; i < shards; i++)
        {
            var seed = Seeds[i];
            double t = (seed.Start + Time * (.85 + seed.Speed * .4)) % 1;
            Point p = PerimeterPoint(rime, t);
            double twinkle = .35 + .65 * Math.Pow(.5 + .5 * Math.Sin((Time * 7 + seed.Start) * Math.Tau), 2);
            double r = 5.5 + 3.5 * seed.Size;
            dc.DrawImage(auraSprites[EffectPalette.OpacityIndex(twinkle * .48 * intensity)], new Rect(p.X - r, p.Y - r, r * 2, r * 2));
            double arm = 2.6 + 1.8 * seed.Size * twinkle;
            var pen = altPens[EffectPalette.OpacityIndex(twinkle * (.55 + .40 * intensity))];
            dc.DrawLine(pen, new Point(p.X - arm, p.Y), new Point(p.X + arm, p.Y));
            dc.DrawLine(pen, new Point(p.X, p.Y - arm), new Point(p.X, p.Y + arm));
            dc.DrawEllipse(coreFills[EffectPalette.OpacityIndex(twinkle * .8 * intensity)], null, p, 1.1, 1.1);
        }
    }

    private void DrawElectric(DrawingContext dc, double width, double height, double intensity)
    {
        Color bolt = EffectPalette.Blend(Color.FromRgb(96, 224, 255), Tint, .30);
        auraSprites ??= BuildOrbSprites(bolt);
        accentPens ??= BuildPens(bolt, 1.65);
        altPens ??= BuildPens(Colors.White, .95);
        coreFills ??= BuildSolidBrushes(Colors.White);

        var target = TargetBounds(width, height);
        var frame = target;
        frame.Inflate(EdgeInset.Left > 2 ? 2.2 : -1, EdgeInset.Top > 2 ? 2.2 : -1);
        double corner = Math.Min(target.Height * .32, 10);
        double time = Time * Math.Tau;

        // Subtle charged frame glow underneath the flowing arcs
        double charge = .62 + .38 * Math.Sin(time * 6);
        dc.DrawRoundedRectangle(null, accentPens[EffectPalette.OpacityIndex(.20 * intensity * charge)], frame, corner, corner);

        for (int arc = 0; arc < 3; arc++)
        {
            double startT = (Time * (3.2 + arc * .65) + arc / 3.0) % 1;
            double span = .24 + .04 * Math.Sin(time * 4 + arc * 2.1);
            var geom = new StreamGeometry();
            using (var ctx = geom.Open())
            {
                const int segs = 12;
                for (int s = 0; s <= segs; s++)
                {
                    double frac = s / (double)segs;
                    double t = (startT + frac * span) % 1;
                    Point p = PerimeterPoint(frame, t);
                    if (s is > 0 and < segs)
                    {
                        double env = Math.Sin(frac * Math.PI);
                        double jitter = (Math.Sin(time * 18 + s * 3.1 + arc * 2.3) * .62
                            + Math.Cos(time * 29 - s * 4.7 + arc * 1.4) * .38)
                            * (3.4 * (.5 + .5 * intensity)) * env;
                        double jx = jitter * Math.Cos(s * 1.3 + arc);
                        double jy = jitter * Math.Sin(s * 1.7 - arc);
                        p = new Point(Math.Clamp(p.X + jx, 1, width - 1), Math.Clamp(p.Y + jy, 1, height - 1));
                    }
                    if (s == 0) ctx.BeginFigure(p, isFilled: false, isClosed: false);
                    else ctx.LineTo(p, isStroked: true, isSmoothJoin: false);
                }
            }
            geom.Freeze();
            double shimmer = .74 + .26 * Math.Sin(time * 11 + arc * 2.2);
            dc.DrawGeometry(null, accentPens[EffectPalette.OpacityIndex(.72 * intensity * shimmer)], geom);
            dc.DrawGeometry(null, altPens[EffectPalette.OpacityIndex(.60 * intensity * shimmer)], geom);

            Point head = PerimeterPoint(frame, (startT + span) % 1);
            Point tail = PerimeterPoint(frame, startT);
            double orb = 7.5 * (.7 + .35 * intensity);
            dc.DrawImage(auraSprites[EffectPalette.OpacityIndex(.65 * intensity * shimmer)], new Rect(head.X - orb, head.Y - orb, orb * 2, orb * 2));
            dc.DrawEllipse(coreFills[EffectPalette.OpacityIndex(.88 * intensity)], null, head, 1.5, 1.5);
            dc.DrawImage(auraSprites[EffectPalette.OpacityIndex(.38 * intensity)], new Rect(tail.X - orb * .7, tail.Y - orb * .7, orb * 1.4, orb * 1.4));
        }
    }

    private void DrawNeon(DrawingContext dc, double width, double height, double intensity)
    {
        Color vivid = EffectPalette.Blend(Tint, Colors.White, .14);
        Color core = EffectPalette.Blend(Tint, Colors.White, .72);
        auraSprites ??= BuildOrbSprites(vivid);
        accentPens ??= BuildPens(vivid, 2.2);
        altPens ??= BuildPens(core, 1.0);
        coreFills ??= BuildSolidBrushes(core);

        var target = TargetBounds(width, height);
        double corner = Math.Min(target.Height * .32, 10);
        double flicker = .82 + .18 * Math.Sin(Time * Math.Tau * 7) * Math.Cos(Time * Math.Tau * 3);
        var outer = target;
        outer.Inflate(EdgeInset.Left > 2 ? 2.5 : -1, EdgeInset.Top > 2 ? 2.5 : -1);

        dc.DrawRoundedRectangle(null, accentPens[EffectPalette.OpacityIndex(.64 * intensity * flicker)], outer, corner, corner);
        dc.DrawRoundedRectangle(null, altPens[EffectPalette.OpacityIndex(.78 * intensity * flicker)], outer, corner, corner);

        for (int k = 0; k < 2; k++)
        {
            double t = (Time * 5 + k * .5) % 1;
            Point pt = PerimeterPoint(outer, t);
            double r = 10 * (.75 + .35 * intensity);
            dc.DrawImage(auraSprites[EffectPalette.OpacityIndex(.70 * intensity)], new Rect(pt.X - r, pt.Y - r, r * 2, r * 2));
            dc.DrawEllipse(coreFills[EffectPalette.OpacityIndex(.90 * intensity)], null, pt, 1.8, 1.8);
        }
    }

    private void DrawSakura(DrawingContext dc, double width, double height, double intensity)
    {
        Color petal = EffectPalette.Blend(Color.FromRgb(248, 152, 194), Tint, .22);
        Color blush = EffectPalette.Blend(Color.FromRgb(255, 224, 238), Tint, .12);
        auraSprites ??= BuildOrbSprites(petal);
        accentPens ??= BuildPens(petal, 1.2);
        coreFills ??= BuildSolidBrushes(blush);

        var target = TargetBounds(width, height);
        double corner = Math.Min(target.Height * .32, 10);
        var rim = target;
        rim.Inflate(EdgeInset.Left > 2 ? 1.5 : -1, EdgeInset.Top > 2 ? 1.5 : -1);
        dc.DrawRoundedRectangle(null, accentPens[EffectPalette.OpacityIndex(.38 * intensity)], rim, corner, corner);

        int count = target.Width > 140 ? 12 : 8;
        for (int i = 0; i < count; i++)
        {
            var seed = Seeds[i];
            double drift = (seed.Start + Time * (1.8 + seed.Speed * 1.1)) % 1;
            double x = target.Left + 4 + (target.Width - 8) * ((seed.Lane + drift * .35) % 1) + Math.Sin(drift * Math.Tau * 2 + seed.Phase) * 5;
            double y = target.Top + 2 + drift * Math.Max(10, target.Height - 4);
            double alpha = Math.Sin(drift * Math.PI) * (.48 + .48 * intensity);
            double rx = 3.2 + 1.8 * seed.Size;
            double ry = rx * .55;

            dc.DrawImage(auraSprites[EffectPalette.OpacityIndex(alpha * .45)], new Rect(x - rx * 1.8, y - rx * 1.8, rx * 3.6, rx * 3.6));
            dc.PushTransform(new RotateTransform((drift * 180 + seed.Phase * 40) % 360, x, y));
            dc.DrawEllipse(coreFills[EffectPalette.OpacityIndex(alpha)], null, new Point(x, y), rx, ry);
            dc.Pop();
        }
    }

    private void DrawPrism(DrawingContext dc, double width, double height, double intensity)
    {
        Color c1 = Color.FromRgb(255, 102, 184);
        Color c2 = Color.FromRgb(76, 222, 255);
        auraSprites ??= BuildOrbSprites(c1);
        arcaneAltSprites ??= BuildOrbSprites(c2);
        accentPens ??= BuildPens(c1, 1.4);
        altPens ??= BuildPens(c2, 1.4);
        coreFills ??= BuildSolidBrushes(Colors.White);

        var target = TargetBounds(width, height);
        double corner = Math.Min(target.Height * .32, 10);
        var frame = target;
        frame.Inflate(EdgeInset.Left > 2 ? 2.2 : -1, EdgeInset.Top > 2 ? 2.2 : -1);

        double mix = .5 + .5 * Math.Sin(Time * Math.Tau * 3);
        dc.DrawRoundedRectangle(null, accentPens[EffectPalette.OpacityIndex((.25 + .40 * mix) * intensity)], frame, corner, corner);
        dc.DrawRoundedRectangle(null, altPens[EffectPalette.OpacityIndex((.25 + .40 * (1 - mix)) * intensity)], target, corner, corner);

        for (int i = 0; i < 4; i++)
        {
            double t = (Time * 3.2 + i * .25) % 1;
            Point p = PerimeterPoint(frame, t);
            double r = 9 * (.7 + .35 * intensity);
            var sprites = i % 2 == 0 ? auraSprites : arcaneAltSprites;
            dc.DrawImage(sprites[EffectPalette.OpacityIndex(.68 * intensity)], new Rect(p.X - r, p.Y - r, r * 2, r * 2));
            dc.DrawEllipse(coreFills[EffectPalette.OpacityIndex(.85 * intensity)], null, p, 1.5, 1.5);
        }
    }

    private void DrawPulse(DrawingContext dc, double width, double height, double intensity)
    {
        accentPens ??= BuildPens(Tint, 1.5);
        altPens ??= BuildPens(EffectPalette.Blend(Tint, Colors.White, .48), 1.0);
        auraSprites ??= BuildOrbSprites(Tint);

        var target = TargetBounds(width, height);
        double corner = Math.Min(target.Height * .32, 10);

        for (int ring = 0; ring < 3; ring++)
        {
            double phase = (Time * 2.4 + ring / 3.0) % 1;
            double fade = Math.Sin(phase * Math.PI) * (1 - phase * .45);
            var r = target;
            double pad = (phase - .18) * (EdgeInset.Left > 2 ? 6.5 : 4.0);
            r.Inflate(pad, pad);
            if (r.Width < 8 || r.Height < 8) continue;
            var pen = (ring % 2 == 0 ? accentPens : altPens)[EffectPalette.OpacityIndex(fade * (.38 + .55 * intensity))];
            dc.DrawRoundedRectangle(null, pen, r, corner + Math.Max(0, pad * .5), corner + Math.Max(0, pad * .5));
        }

        double breathe = .55 + .45 * Math.Sin(Time * Math.Tau * 3);
        double glowR = Math.Min(target.Width, target.Height) * .65;
        var center = new Point(target.Left + target.Width * .5, target.Top + target.Height * .5);
        dc.DrawImage(auraSprites[EffectPalette.OpacityIndex(.24 * intensity * breathe)],
            new Rect(center.X - glowR, center.Y - glowR * .75, glowR * 2, glowR * 1.5));
    }

    private static Point PerimeterPoint(Rect r, double t)
    {
        double perim = 2 * (r.Width + r.Height);
        if (perim <= 0) return new Point(r.Left, r.Top);
        double d = (t % 1) * perim;
        if (d < r.Width) return new Point(r.Left + d, r.Top);
        d -= r.Width;
        if (d < r.Height) return new Point(r.Right, r.Top + d);
        d -= r.Height;
        if (d < r.Width) return new Point(r.Right - d, r.Bottom);
        d -= r.Width;
        return new Point(r.Left, r.Bottom - d);
    }

    private static void OnTrackSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListBoxItem item) return;
        if ((bool)e.NewValue)
        {
            item.Loaded += Item_Loaded;
            item.Unloaded += Item_Unloaded;
            item.Selected += Item_SelectionChanged;
            item.Unselected += Item_SelectionChanged;
            if (item.IsLoaded) SubscribeRow(item);
        }
        else
        {
            item.Loaded -= Item_Loaded;
            item.Unloaded -= Item_Unloaded;
            item.Selected -= Item_SelectionChanged;
            item.Unselected -= Item_SelectionChanged;
            UnsubscribeRow(item);
        }
    }

    private static void Item_Loaded(object sender, RoutedEventArgs e) { if (sender is ListBoxItem item) SubscribeRow(item); }
    private static void Item_Unloaded(object sender, RoutedEventArgs e) { if (sender is ListBoxItem item) UnsubscribeRow(item); }
    private static void Item_SelectionChanged(object sender, RoutedEventArgs e) { if (sender is ListBoxItem item) ApplyToRow(item); }

    private static void SubscribeRow(ListBoxItem item)
    {
        if (!RowSubscriptions.ContainsKey(item))
        {
            void Refresh() => ApplyToRow(item);
            RowSubscriptions[item] = Refresh;
            StateChanged += Refresh;
        }
        ApplyToRow(item);
    }

    private static void UnsubscribeRow(ListBoxItem item)
    {
        if (RowSubscriptions.Remove(item, out var handler))
            StateChanged -= handler;
        if (item.Template?.FindName("RowAura", item) is TargetAura rowAura)
            rowAura.Visibility = Visibility.Collapsed;
    }

    private static BitmapSource[] BuildOrbSprites(Color color)
    {
        var key = ((byte)0, color);
        if (SpriteCache.TryGetValue(key, out var cached)) return cached;
        if (SpriteCache.Count >= 24) SpriteCache.Clear();

        const int size = 32, stride = size * 4;
        Color bright = EffectPalette.Blend(color, Colors.White, .42);
        var sprites = new BitmapSource[EffectPalette.OpacityLevels];
        for (int i = 0; i < sprites.Length; i++)
        {
            double alphaScale = i / (double)(EffectPalette.OpacityLevels - 1);
            var pixels = new byte[size * stride];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    double dx = (x + .5) / size * 2 - 1, dy = (y + .5) / size * 2 - 1;
                    double d = Math.Sqrt(dx * dx + dy * dy);
                    if (d >= 1) continue;
                    double alphaNorm;
                    Color c;
                    if (d <= .42)
                    {
                        double t = d / .42;
                        c = EffectPalette.Blend(bright, color, t);
                        alphaNorm = 1.0 - t * (1.0 - 175.0 / 255.0);
                    }
                    else
                    {
                        double t = (d - .42) / .58;
                        c = color;
                        alphaNorm = (175.0 / 255.0) * (1.0 - t);
                    }
                    byte alpha = (byte)Math.Clamp(Math.Round(255 * alphaScale * alphaNorm), 0, 255);
                    int offset = y * stride + x * 4;
                    pixels[offset] = (byte)(c.B * alpha / 255);
                    pixels[offset + 1] = (byte)(c.G * alpha / 255);
                    pixels[offset + 2] = (byte)(c.R * alpha / 255);
                    pixels[offset + 3] = alpha;
                }
            }
            var bmp = BitmapSource.Create(size, size, 96, 96, PixelFormats.Pbgra32, null, pixels, stride);
            bmp.Freeze();
            sprites[i] = bmp;
        }
        return SpriteCache[key] = sprites;
    }

    private static BitmapSource[] BuildSmokeSprites(Color color)
    {
        var key = ((byte)1, color);
        if (SpriteCache.TryGetValue(key, out var cached)) return cached;
        if (SpriteCache.Count >= 24) SpriteCache.Clear();

        const int size = 40, stride = size * 4;
        var sprites = new BitmapSource[EffectPalette.OpacityLevels];
        for (int i = 0; i < sprites.Length; i++)
        {
            double alphaScale = i / (double)(EffectPalette.OpacityLevels - 1);
            var pixels = new byte[size * stride];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    double dx = (x + .5) / size * 2 - 1, dy = (y + .5) / size * 2 - 1;
                    double d = Math.Sqrt(dx * dx + dy * dy);
                    if (d >= 1) continue;
                    double baseAlpha = d <= .5
                        ? 215.0 - (d / .5) * (215.0 - 120.0)
                        : 120.0 * (1.0 - (d - .5) / .5);
                    byte alpha = (byte)Math.Clamp(Math.Round(baseAlpha * alphaScale), 0, 255);
                    int offset = y * stride + x * 4;
                    pixels[offset] = (byte)(color.B * alpha / 255);
                    pixels[offset + 1] = (byte)(color.G * alpha / 255);
                    pixels[offset + 2] = (byte)(color.R * alpha / 255);
                    pixels[offset + 3] = alpha;
                }
            }
            var bmp = BitmapSource.Create(size, size, 96, 96, PixelFormats.Pbgra32, null, pixels, stride);
            bmp.Freeze();
            sprites[i] = bmp;
        }
        return SpriteCache[key] = sprites;
    }

    private static Pen[] BuildPens(Color color, double thickness)
    {
        Color bright = EffectPalette.Blend(color, Colors.White, .25);
        var pens = new Pen[EffectPalette.OpacityLevels];
        for (int i = 0; i < pens.Length; i++)
        {
            var b = new SolidColorBrush(EffectPalette.WithOpacity(bright, i));
            b.Freeze();
            var p = new Pen(b, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            p.Freeze();
            pens[i] = p;
        }
        return pens;
    }

    private static Brush[] BuildSolidBrushes(Color color)
    {
        var brushes = new Brush[EffectPalette.OpacityLevels];
        for (int i = 0; i < brushes.Length; i++)
        {
            var b = new SolidColorBrush(EffectPalette.WithOpacity(color, i));
            b.Freeze();
            brushes[i] = b;
        }
        return brushes;
    }

    private static ParticleSeed[] CreateSeeds()
    {
        var rng = new Random(90731);
        var list = new ParticleSeed[28];
        for (int i = 0; i < list.Length; i++)
            list[i] = new(rng.NextDouble(), rng.NextDouble(), rng.NextDouble(), rng.NextDouble() * Math.Tau, .55 + rng.NextDouble() * .75);
        return list;
    }

    private readonly record struct ParticleSeed(double Lane, double Start, double Speed, double Phase, double Size);
}

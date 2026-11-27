using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using CydLan.Core;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace CydLan.Desktop;

/// <summary>
/// Card battle effects: bespoke icon-matched attacks, support/shield/skill effects, status hits, and persistent auras.
/// Colors are sampled directly from each card's 32×32 pixel icon via <see cref="CardArt.Palette"/> so every effect matches its card.
/// </summary>
public partial class PetArenaView
{
    private static readonly Color Ember = Color.FromRgb(0xF9, 0x73, 0x16), Flame = Color.FromRgb(0xFA, 0xCC, 0x15),
        Water = Color.FromRgb(0x38, 0xBD, 0xF8), Ice = Color.FromRgb(0xBA, 0xE6, 0xFD), LeafGreen = Color.FromRgb(0x22, 0xC5, 0x5E),
        Bolt = Color.FromRgb(0xFD, 0xE0, 0x47), Dusk = Color.FromRgb(0x7C, 0x3A, 0xED), Holy = Color.FromRgb(0xFE, 0xF0, 0x8A),
        Toxic = Color.FromRgb(0x84, 0xCC, 0x16), Dream = Color.FromRgb(0x81, 0x8C, 0xF8), Dread = Color.FromRgb(0xA8, 0x55, 0xF7),
        Chill = Color.FromRgb(0x7D, 0xD3, 0xFC), Fury = Color.FromRgb(0xEF, 0x44, 0x44), Steel = Color.FromRgb(0xE2, 0xE8, 0xF0);

    private int auraTick;

    // ── Shared primitives ────────────────────────────────────────────────

    private T Add<T>(T element, double x, double y) where T : UIElement
    {
        Canvas.SetLeft(element, x); Canvas.SetTop(element, y);
        FxLayer.Children.Add(element);
        return element;
    }

    private void FadeOut(UIElement element, int ms, int delay = 0)
    {
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(ms)) { BeginTime = TimeSpan.FromMilliseconds(delay) };
        fade.Completed += (_, _) => FxLayer.Children.Remove(element);
        element.BeginAnimation(OpacityProperty, fade);
    }

    private void FadeInOut(UIElement element, int fadeInMs, int holdMs, int fadeOutMs, int delay = 0, double peak = 1)
    {
        int total = fadeInMs + holdMs + fadeOutMs;
        element.Opacity = 0;
        var anim = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(total), BeginTime = TimeSpan.FromMilliseconds(delay) };
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(peak, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(fadeInMs))));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(peak, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(fadeInMs + holdMs))));
        anim.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(total))));
        anim.Completed += (_, _) => FxLayer.Children.Remove(element);
        element.BeginAnimation(OpacityProperty, anim);
    }

    private static void Move(UIElement element, DependencyProperty property, double to, int ms, IEasingFunction? ease = null, int delay = 0) =>
        element.BeginAnimation(property, new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease, BeginTime = TimeSpan.FromMilliseconds(delay) });

    private static readonly IEasingFunction Out = new QuadraticEase { EasingMode = EasingMode.EaseOut };
    private static readonly IEasingFunction In = new QuadraticEase { EasingMode = EasingMode.EaseIn };

    private static Effect Glow(Color color, double blur = 12) => new DropShadowEffect { Color = color, BlurRadius = blur, ShadowDepth = 0, Opacity = .9 };

    private static Color Darken(Color c, double factor = .55) => Color.FromRgb((byte)(c.R * factor), (byte)(c.G * factor), (byte)(c.B * factor));

    /// <summary>Particles thrown out from a point, falling or rising under gravity.</summary>
    private void Spray(Point at, Color color, int count, double spread, double size, int ms, double gravity = 30, bool up = false)
    {
        var rnd = Random.Shared;
        for (int i = 0; i < count; i++)
        {
            double r = size * (.6 + rnd.NextDouble() * .7);
            var dot = Add(new Ellipse { Width = r * 2, Height = r * 2, Fill = new SolidColorBrush(i % 4 == 0 ? Colors.White : color), Effect = Glow(color, 6) }, at.X - r, at.Y - r);
            double ang = up ? -Math.PI / 2 + (rnd.NextDouble() - .5) * 1.6 : rnd.NextDouble() * Math.PI * 2, dist = spread * (.4 + rnd.NextDouble() * .6);
            int dur = ms + rnd.Next(ms / 2);
            Move(dot, Canvas.LeftProperty, at.X - r + Math.Cos(ang) * dist, dur, Out);
            var fall = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(dur) };
            fall.KeyFrames.Add(new EasingDoubleKeyFrame(at.Y - r + Math.Sin(ang) * dist, KeyTime.FromPercent(.6), Out));
            fall.KeyFrames.Add(new EasingDoubleKeyFrame(at.Y - r + Math.Sin(ang) * dist + gravity, KeyTime.FromPercent(1), In));
            dot.BeginAnimation(Canvas.TopProperty, fall);
            FadeOut(dot, dur);
        }
    }

    private void Flash(Color color, double strength = .45)
    {
        var flash = Add(new Rectangle { Width = StageW + 40, Height = StageH + 40, Fill = new SolidColorBrush(color), Opacity = strength, IsHitTestVisible = false }, -20, -20);
        FadeOut(flash, 260);
    }

    private Task Fly(UIElement shape, double w, double h, Point from, Point to, int ms, double arc = 40, double spin = 0)
    {
        var tcs = new TaskCompletionSource();
        Add(shape, from.X - w / 2, from.Y - h / 2);
        if (spin != 0)
        {
            var rot = new RotateTransform();
            shape.RenderTransformOrigin = new Point(.5, .5);
            shape.RenderTransform = rot;
            rot.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, spin, TimeSpan.FromMilliseconds(ms)));
        }
        var x = new DoubleAnimation(to.X - w / 2, TimeSpan.FromMilliseconds(ms)) { EasingFunction = In };
        x.Completed += (_, _) => { FxLayer.Children.Remove(shape); tcs.TrySetResult(); };
        shape.BeginAnimation(Canvas.LeftProperty, x);
        if (arc == 0)
            shape.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(to.Y - h / 2, TimeSpan.FromMilliseconds(ms)) { EasingFunction = In });
        else
        {
            var y = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(ms) };
            y.KeyFrames.Add(new SplineDoubleKeyFrame(Math.Min(from.Y, to.Y) - arc - h / 2, KeyTime.FromPercent(.5), new KeySpline(.3, 0, .7, 1)));
            y.KeyFrames.Add(new SplineDoubleKeyFrame(to.Y - h / 2, KeyTime.FromPercent(1), new KeySpline(.3, 0, .7, 1)));
            shape.BeginAnimation(Canvas.TopProperty, y);
        }
        return tcs.Task;
    }

    private async void Trail(Point from, Point to, int ms, double arc, Color color, double size, int every = 30)
    {
        var rnd = Random.Shared;
        for (int t = 0; t < ms; t += every)
        {
            if (cardSession is null || BattlePanel.Visibility != Visibility.Visible) return;
            double k = t / (double)ms;
            double ex = k * k;
            var p = new Point(from.X + (to.X - from.X) * ex, from.Y + (to.Y - from.Y) * k - Math.Sin(k * Math.PI) * arc);
            double r = size * (.5 + rnd.NextDouble() * .6);
            var dot = Add(new Ellipse { Width = r * 2, Height = r * 2, Fill = new SolidColorBrush(color) }, p.X - r + rnd.Next(-4, 5), p.Y - r + rnd.Next(-4, 5));
            Move(dot, Canvas.TopProperty, p.Y - r - 10 - rnd.Next(12), 380, Out);
            FadeOut(dot, 380);
            await Task.Delay(every);
        }
    }

    private static Ellipse Orb(double r, Color color, Color core) => new()
    {
        Width = r * 2, Height = r * 2,
        Fill = new RadialGradientBrush(core, color) { GradientOrigin = new Point(.35, .35) },
        Effect = Glow(color, 20)
    };

    // ── Bespoke Card Attack VFX ──────────────────────────────────────────

    /// <summary>Plays a card-accurate attack effect matching the card's weapon/theme and sampled icon colors.</summary>
    private async Task ElementAttack(StagePet attacker, StagePet target, PetCard card, bool multi)
    {
        var from = CenterOf(attacker);
        var to = CenterOf(target);
        from.Y -= 10;
        double dir = attacker == left ? 1 : -1;
        var (main, core) = CardArt.Palette(card);
        int ms = multi ? 200 : 290;

        switch (card.Id)
        {
            // ── Weapons: swords, daggers, lances, scythes ──
            case "strike" or "lightblade" or "dawn":
                await SwordAttack(to, dir, card.Id == "strike" ? Steel : main, core, twin: card.Id == "dawn", heavy: card.Id == "dawn");
                break;
            case "sneak" or "feint" or "ambush":
                await DaggerStrike(from, to, dir, main, core, twin: card.Id == "ambush");
                break;
            case "iceaxe" or "branding" or "sleepdart":
                await LanceThrow(from, to, dir, main, core, card.Id);
                break;
            case "reaper":
                await ScytheCleave(to, from, dir, main, core);
                break;

            // ── Martial / Heavy impacts ──
            case "jab" or "combo" or "searingfist" or "headbutt":
                await FistStrike(from, to, dir, card.Id == "searingfist" ? Ember : main, core, fiery: card.Id == "searingfist", multi);
                break;
            case "shieldbash" or "glacialram":
                await ShieldRam(from, to, dir, card.Id == "glacialram" ? Ice : main, core, icy: card.Id == "glacialram");
                break;
            case "mauler" or "treant":
                await HeavySlam(to, main, core);
                break;
            case "whirlwind" or "whirlpool":
                await VortexSpin(to, card.Id == "whirlpool" ? Water : main, core, water: card.Id == "whirlpool");
                break;

            // ── Whips, vines, claws, jaws ──
            case "flamewhip" or "vinelash" or "leaf" or "thornlunge":
                await WhipLash(from, to, main, core, fiery: card.Id == "flamewhip", thorny: card.Id is "leaf" or "thornlunge");
                break;
            case "flareclaw" or "shadow" or "frostwolf" or "primal":
                await ClawRend(to, main, core, count: multi ? 2 : 4, roar: card.Id == "primal", icy: card.Id == "frostwolf");
                break;
            case "nightbite" or "vampire" or "venomfang" or "devour" or "frost":
                await FangBite(to, from, main, core, drain: card.Lifesteal > 0, venom: card.Id == "venomfang", icy: card.Id == "frost");
                break;

            // ── Fire cataclysms & projectiles ──
            case "meteor":
                await MeteorDrop(to, main, core);
                break;
            case "solarflare":
                await SolarFlareBlast(to, main, core);
                break;
            case "hellfire" or "inferno":
                await FlameColumns(to, main, core, count: card.Id == "hellfire" ? 4 : 3);
                break;
            case "fireball" or "blaze" or "ember" or "cinder" or "sparkspit":
                if (card.Id == "sparkspit") { await SparkVolley(from, to, main, core); break; }
                Trail(from, to, ms, card.Id == "cinder" ? 72 : 44, main, card.Id == "fireball" ? 7 : 5, 22);
                await Fly(Orb(card.Id == "fireball" ? 17 : multi ? 9 : 13, main, core), card.Id == "fireball" ? 34 : 26, card.Id == "fireball" ? 34 : 26, from, to, ms, card.Id == "cinder" ? 72 : 44);
                FlameBurst(to, card.Id == "fireball" ? 13 : multi ? 5 : 9, main, core);
                break;

            // ── Ocean waves, jets, sea dragon ──
            case "tide" or "tsunami" or "breaker" or "riptide":
                await TidalWave(from, to, dir, main, core, huge: card.Id == "tsunami");
                break;
            case "hydrojet" or "current" or "splash":
                if (card.Id == "splash")
                {
                    Trail(from, to, ms, 64, main, 5, 24);
                    await Fly(Orb(12, main, core), 24, 24, from, to, ms, 64);
                    Splash(to, 16, main);
                }
                else await HydroJetBeam(from, to, main, core);
                break;
            case "leviathan":
                await LeviathanSurge(to, main, core);
                break;

            // ── Ice, snow, avalanches, blizzards ──
            case "glacier":
                await GlacierEruption(to, main, core);
                break;
            case "icicle" or "iceshard":
                await IceShardVolley(from, to, main, core, multi ? 1 : 3, ms);
                break;
            case "snowball" or "hailstones" or "coldsnap":
                if (card.Id == "coldsnap") { IceBurst(to, main); await Task.Delay(170); }
                else await SnowPelletDrop(from, to, main, core, fromSky: card.Id == "hailstones");
                break;
            case "avalanche":
                await AvalancheCrash(to, main, core);
                break;
            case "blizzard":
                await BlizzardSweep(to, dir, main, core);
                break;

            // ── Nature projectiles ──
            case "seedshot":
                await SeedVolley(from, to, main, core);
                break;
            case "petalstorm":
                await PetalStormSweep(from, to, main, core);
                break;

            // ── Electric / cyber / sky thunder ──
            case "railgun":
                await RailgunShot(from, to, dir, main, core);
                break;
            case "glitch":
                await GlitchStrike(to, main, core);
                break;
            case "skyfall" or "thunderclap":
                await SkyThunder(to, main, core, bolts: card.Id == "skyfall" ? 3 : 2);
                break;
            case "chainbolt" or "blitz" or "overcharge":
                Lightning(from, to, multi ? 2 : 4, main, core);
                if (!multi) Flash(core, .32);
                await Task.Delay(multi ? 110 : 170);
                Spray(to, main, multi ? 7 : 14, 76, 2.8, 320, 10);
                break;
            case "volt" or "zap" or "shock" or "static" or "arc" or "paralyze" or "stungun":
                if (card.Id == "stungun") await TaserProngs(from, to, main, core);
                else
                {
                    Lightning(from, to, multi ? 1 : 2, main, core);
                    if (!multi) Flash(core, .24);
                    await Task.Delay(multi ? 100 : 150);
                    Spray(to, main, multi ? 6 : 11, 64, 2.5, 280, 10);
                }
                break;

            // ── Shadow swarms, tentacles, rifts, eclipse ──
            case "batswarm" or "ravens":
                await WingedSwarm(from, to, dir, main, core, ravens: card.Id == "ravens");
                break;
            case "tentacle":
                await TentacleErupt(to, main, core);
                break;
            case "voidrift" or "abyss":
                await VoidRiftTear(to, main, core, grand: card.Id == "abyss");
                break;
            case "eclipse":
                await EclipseBeam(to, main, core);
                break;

            // ── Light beams, stars, heavenly judgment ──
            case "light" or "sunbeam":
                await RadiantBeam(from, to, main, core);
                break;
            case "sparkleshot" or "glint":
                await StarShot(from, to, main, core, multi);
                break;
            case "smite" or "judgment" or "flashstrike":
                if (card.Id == "flashstrike") { Flash(core, .4); await SwordAttack(to, dir, main, core, twin: false, heavy: false); }
                else await Pillar(to, multi, main, core, cross: card.Id == "judgment");
                break;
            case "starfall":
                await StarfallRain(to, main, core);
                break;

            // ── Fallback (parry counter-hits & any future cards) ──
            default:
                if (card.Parry > 0) await SwordAttack(to, dir, main, core, twin: false, heavy: false);
                else Slash(to, main, multi ? 1 : 2);
                break;
        }
        if (card.Pierce) Streak(to, dir);
    }

    // ── Bespoke Non-Attack Card VFX (defends, heals, skills, counters) ───

    /// <summary>Plays a card-accurate effect when a non-attack card activates.</summary>
    private void SupportCardVfx(StagePet caster, StagePet foe, PetCard card)
    {
        var me = CenterOf(caster);
        var them = CenterOf(foe);
        var (main, core) = CardArt.Palette(card);
        switch (card.Id)
        {
            case "phoenix":
                Wings(me, Ember, Flame);
                FlameBurst(me, 8, Ember, Flame);
                break;
            case "archangel":
                Wings(me, Holy, Colors.White);
                HaloRing(me, Holy);
                break;
            case "worldtree":
                VineBarrier(me, LeafGreen, Holy);
                RisingLeaves(me, LeafGreen, 10);
                break;
            case "lullaby" or "sirensong":
                MusicNotes(me, them, main);
                break;
            case "raindance" or "deluge" or "monsoon":
                RainCloud(card.Id == "monsoon" ? them : me, main);
                break;
            case "lavapool" or "smolder" or "kindle" or "stoke":
                LavaPool(them, main, core);
                break;
            case "toxicspores" or "sporecloud" or "pollen":
                SporeCloud(them, main);
                break;
            case "rootbind":
                VineBarrier(them, main, core);
                break;
            case "iceprison" or "absolutezero" or "frostbite" or "frostbind" or "chill" or "freezeframe":
                IceCage(them, main, core);
                break;
            case "nightmare" or "curse" or "hex" or "lich" or "soulflame" or "gloom" or "darkpact":
                HexSigil(them, main, core);
                break;
            case "blinding":
                SunburstFlash(them, core);
                break;
            case "heatshield" or "firewall" or "pyreward":
                FlameWall(me, main, core);
                break;
            case "icewall" or "frostguard" or "frostcoat" or "snowfort" or "frozenheart" or "permafrost" or "icemirror":
                CrystalWall(me, main, core);
                break;
            case "bubblewrap" or "pearlshell" or "coralguard" or "seamist":
                BubbleDome(me, main, core);
                break;
            case "barkskin" or "bramble" or "leafshield" or "thornhide":
                VineBarrier(me, main, core);
                break;
            case "sparkguard" or "stormcell" or "magnetfield" or "staticfield":
                ElectricShield(me, main, core);
                break;
            case "shade" or "cloak" or "phantom" or "shadowstep":
                Smoke(me, main, 8);
                Ring(me, main, 100);
                break;
            case "guard" or "brace" or "ironwall" or "bulwark" or "holyshield" or "aegis" or "halo":
                ShieldCrest(me, main, core, halo: card.Id is "halo" or "aegis" or "holyshield");
                break;
            case "tesla" or "supercharge" or "dynamo" or "charge" or "overclock" or "conduit":
                EnergySurge(me, main, core);
                break;
            case "battlecry" or "ragepill" or "ragefire" or "boost" or "focus":
                Ring(me, main, 95);
                Ring(me, core, 130);
                Spray(me, main, 10, 60, 3, 380, -30, up: true);
                break;
            case "lantern" or "wish" or "prayer" or "blessing" or "revive" or "purify" or "sanctuary":
                HaloRing(me, main);
                RisingPlus(caster, core, 0);
                break;
            case "smokescreen" or "fogbank":
                Smoke(them, main, 9);
                break;
            case "shortcircuit" or "mindspike" or "silence" or "disrupt":
                Lightning(me, them, 1, main, core);
                Ring(them, main, 105);
                break;
            case "parry" or "backdraft" or "undercurrent" or "snapvine" or "reflect":
                CrossedBlades(me, main, core);
                break;
        }
    }

    // ── Weapon & Martial Animations ──────────────────────────────────────

    private static Canvas BuildSword(Color blade, Color edge, double length = 76)
    {
        var c = new Canvas { Width = length, Height = 26 };
        // Blade
        c.Children.Add(new Polygon
        {
            Points = [new(18, 9), new(length - 12, 8), new(length, 13), new(length - 12, 18), new(18, 17)],
            Fill = new LinearGradientBrush(Colors.White, blade, 90),
            Stroke = new SolidColorBrush(edge), StrokeThickness = 1.2, Effect = Glow(blade, 12)
        });
        // Fuller highlight
        c.Children.Add(new Line { X1 = 22, Y1 = 13, X2 = length - 14, Y2 = 13, Stroke = new SolidColorBrush(Colors.White), StrokeThickness = 1.6, Opacity = .9 });
        // Crossguard & hilt
        c.Children.Add(new Rectangle { Width = 5, Height = 22, RadiusX = 2, RadiusY = 2, Fill = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)) });
        Canvas.SetLeft(c.Children[^1], 15); Canvas.SetTop(c.Children[^1], 2);
        c.Children.Add(new Rectangle { Width = 14, Height = 6, RadiusX = 2, RadiusY = 2, Fill = new SolidColorBrush(Color.FromRgb(0x78, 0x35, 0x0F)) });
        Canvas.SetLeft(c.Children[^1], 2); Canvas.SetTop(c.Children[^1], 10);
        c.Children.Add(new Ellipse { Width = 7, Height = 7, Fill = new SolidColorBrush(Color.FromRgb(0xFA, 0xCC, 0x15)) });
        Canvas.SetLeft(c.Children[^1], 0); Canvas.SetTop(c.Children[^1], 9.5);
        return c;
    }

    private async Task SwordAttack(Point at, double dir, Color main, Color core, bool twin, bool heavy)
    {
        int swings = twin ? 2 : 1;
        for (int i = 0; i < swings; i++)
        {
            double len = heavy ? 94 : 76;
            var sword = BuildSword(main, core, len);
            var rot = new RotateTransform(i == 0 ? (dir > 0 ? -115 : -65) : (dir > 0 ? 55 : 125));
            sword.RenderTransformOrigin = new Point(.18, .5);
            sword.RenderTransform = rot;
            Add(sword, at.X - (dir > 0 ? 38 : len - 38), at.Y - 22 + i * 8);
            double toAng = i == 0 ? (dir > 0 ? 45 : -225) : (dir > 0 ? -110 : 290);
            rot.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(rot.Angle, toAng, TimeSpan.FromMilliseconds(180)) { EasingFunction = Out });
            Slash(at, i == 0 ? core : main, 1, flip: i == 1);
            FadeOut(sword, 160, 140);
            Spray(at, core, heavy ? 12 : 7, 62, 2.8, 300, 15);
            if (heavy) Ring(at, main, 95);
            await Task.Delay(twin ? 120 : 170);
        }
    }

    private async Task DaggerStrike(Point from, Point to, double dir, Color main, Color core, bool twin)
    {
        int count = twin ? 2 : 1;
        var flights = new List<Task>();
        for (int i = 0; i < count; i++)
        {
            var dagger = new Polygon
            {
                Points = dir > 0 ? [new(0, 6), new(14, 2), new(36, 6), new(14, 10)] : [new(36, 6), new(22, 2), new(0, 6), new(22, 10)],
                Fill = new LinearGradientBrush(Colors.White, main, 0),
                Stroke = new SolidColorBrush(core), StrokeThickness = 1.2, Effect = Glow(main, 12)
            };
            var start = new Point(from.X + dir * 24, from.Y + (i == 0 ? -14 : 14));
            var end = new Point(to.X + dir * 24, to.Y + (i == 0 ? -8 : 8));
            flights.Add(Fly(dagger, 36, 12, start, end, 150, 4));
            if (twin) await Task.Delay(55);
        }
        await Task.WhenAll(flights);
        Streak(to, dir);
        Spray(to, main, 10, 58, 2.6, 300, 10);
    }

    private async Task LanceThrow(Point from, Point to, double dir, Color main, Color core, string id)
    {
        var lance = new Canvas { Width = 68, Height = 14 };
        lance.Children.Add(new Rectangle { Width = 46, Height = 4, RadiusX = 2, RadiusY = 2, Fill = new SolidColorBrush(id == "sleepdart" ? Color.FromRgb(0x94, 0xA3, 0xB8) : main), Effect = Glow(main, 10) });
        Canvas.SetLeft(lance.Children[^1], dir > 0 ? 4 : 18); Canvas.SetTop(lance.Children[^1], 5);
        lance.Children.Add(new Polygon
        {
            Points = dir > 0 ? [new(48, 1), new(68, 7), new(48, 13)] : [new(20, 1), new(0, 7), new(20, 13)],
            Fill = new LinearGradientBrush(Colors.White, core, 0), Effect = Glow(core, 14)
        });
        if (id == "sleepdart")
        {
            // Feathered tail
            lance.Children.Add(new Polygon
            {
                Points = dir > 0 ? [new(0, 1), new(12, 7), new(0, 13), new(4, 7)] : [new(68, 1), new(56, 7), new(68, 13), new(64, 7)],
                Fill = new SolidColorBrush(Dream)
            });
        }
        Trail(from, to, 210, 10, main, 4, 24);
        await Fly(lance, 68, 14, from, to, 210, 10);
        if (id == "iceaxe") IceBurst(to, main);
        else if (id == "branding") { FlameBurst(to, 9, Ember, Flame); Smoke(to, Color.FromRgb(0x64, 0x74, 0x8B), 4); }
        else { Ring(to, Dream, 85); Smoke(to, Dream, 4); }
    }

    private async Task ScytheCleave(Point at, Point from, double dir, Color main, Color core)
    {
        var scythe = new Canvas { Width = 90, Height = 80 };
        scythe.Children.Add(new Line { X1 = 10, Y1 = 76, X2 = 58, Y2 = 12, Stroke = new SolidColorBrush(Darken(main, .45)), StrokeThickness = 5, StrokeEndLineCap = PenLineCap.Round });
        scythe.Children.Add(new Path
        {
            Data = Geometry.Parse("M 54,14 C 82,4 94,30 74,56 C 76,34 66,24 46,24 Z"),
            Fill = new LinearGradientBrush(core, main, 45), Stroke = new SolidColorBrush(Colors.White), StrokeThickness = 1.2, Effect = Glow(main, 16)
        });
        var rot = new RotateTransform(dir > 0 ? -90 : 90);
        scythe.RenderTransformOrigin = new Point(.25, .85);
        scythe.RenderTransform = rot;
        Add(scythe, at.X - 42, at.Y - 56);
        rot.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(rot.Angle, dir > 0 ? 50 : -50, TimeSpan.FromMilliseconds(220)) { EasingFunction = Out });
        Slash(at, main, 2);
        Smoke(at, Darken(main, .5), 5);
        FadeOut(scythe, 220, 160);
        await Task.Delay(170);
        _ = Fly(Orb(7, core, Colors.White), 14, 14, at, from, 260, 42);
    }

    private async Task FistStrike(Point from, Point to, double dir, Color main, Color core, bool fiery, bool multi)
    {
        var fist = new Canvas { Width = 36, Height = 30 };
        fist.Children.Add(new Rectangle { Width = 30, Height = 24, RadiusX = 9, RadiusY = 9, Fill = new RadialGradientBrush(core, main), Stroke = new SolidColorBrush(Colors.White), StrokeThickness = 1.5, Effect = Glow(main, 16) });
        Canvas.SetLeft(fist.Children[^1], 3); Canvas.SetTop(fist.Children[^1], 3);
        for (int k = 0; k < 3; k++)
        {
            var knuckle = new Line { X1 = dir > 0 ? 22 : 14, Y1 = 6 + k * 6, X2 = dir > 0 ? 31 : 5, Y2 = 6 + k * 6, Stroke = new SolidColorBrush(Colors.White), StrokeThickness = 1.6, Opacity = .8 };
            fist.Children.Add(knuckle);
        }
        if (fiery) Trail(from, to, 170, 12, Ember, 6, 20);
        await Fly(fist, 36, 30, new Point(from.X + dir * 20, from.Y + (multi ? Random.Shared.Next(-14, 14) : 0)), to, multi ? 130 : 180, 8);
        Ring(to, core, multi ? 54 : 84);
        if (fiery) FlameBurst(to, 9, Ember, Flame);
        else Spray(to, main, multi ? 6 : 12, 64, 3, 280, 12);
    }

    private async Task ShieldRam(Point from, Point to, double dir, Color main, Color core, bool icy)
    {
        var shield = new Polygon
        {
            Points = [new(18, 0), new(36, 8), new(33, 28), new(18, 42), new(3, 28), new(0, 8)],
            Fill = new LinearGradientBrush(core, main, 90), Stroke = new SolidColorBrush(Colors.White), StrokeThickness = 2, Effect = Glow(main, 16)
        };
        Trail(from, to, 210, 6, core, 5, 24);
        await Fly(shield, 36, 42, from, to, 210, 6);
        Ring(to, core, 105);
        Ring(to, main, 70);
        if (icy) IceBurst(to, main);
        else Spray(to, core, 14, 72, 3.2, 340, 20);
        ShakeStage(6);
    }

    private async Task HeavySlam(Point at, Color main, Color core)
    {
        var weight = new Ellipse
        {
            Width = 54, Height = 44, Fill = new RadialGradientBrush(core, main), Stroke = new SolidColorBrush(Darken(main, .4)), StrokeThickness = 2.5, Effect = Glow(main, 16)
        };
        await Fly(weight, 54, 44, new Point(at.X, -24), new Point(at.X, at.Y + 12), 200, 0);
        // Ground crack lines + dust cloud
        for (int i = -2; i <= 2; i++)
        {
            var crack = Add(new Line { X1 = at.X, Y1 = at.Y + 26, X2 = at.X + i * 26, Y2 = at.Y + 38 + Math.Abs(i) * 4, Stroke = new SolidColorBrush(Darken(main, .35)), StrokeThickness = 3 }, 0, 0);
            FadeOut(crack, 420, 120);
        }
        Ring(new Point(at.X, at.Y + 20), main, 115);
        Smoke(new Point(at.X, at.Y + 24), Color.FromRgb(0x94, 0xA3, 0xB8), 6);
        Spray(at, main, 12, 74, 3.5, 360, 45);
        ShakeStage(8);
    }

    private async Task VortexSpin(Point at, Color main, Color core, bool water)
    {
        for (int i = 0; i < 6; i++)
        {
            double w = 36 + i * 14, h = 14 + i * 3;
            var band = Add(new Ellipse
            {
                Width = w, Height = h, Stroke = new SolidColorBrush(i % 2 == 0 ? core : main), StrokeThickness = 3.5,
                StrokeDashArray = [4, 2], Effect = Glow(main, 10),
                RenderTransformOrigin = new Point(.5, .5), RenderTransform = new RotateTransform(i * 25)
            }, at.X - w / 2, at.Y + 28 - i * 14);
            ((RotateTransform)band.RenderTransform).BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(i * 25, i * 25 + 540, TimeSpan.FromMilliseconds(460)));
            FadeInOut(band, 60, 220, 180, i * 25);
        }
        Spray(at, water ? Water : core, 14, 70, 3.2, 400, -30, up: true);
        await Task.Delay(260);
    }

    private async Task WhipLash(Point from, Point to, Color main, Color core, bool fiery, bool thorny)
    {
        double midX = (from.X + to.X) / 2, midY = Math.Min(from.Y, to.Y) - 34;
        var geo = new PathGeometry();
        var fig = new PathFigure { StartPoint = from };
        fig.Segments.Add(new BezierSegment(new Point(from.X + (to.X - from.X) * .3, midY - 26), new Point(from.X + (to.X - from.X) * .7, midY + 46), to, true));
        geo.Figures.Add(fig);
        var whip = Add(new Path { Data = geo, Stroke = new SolidColorBrush(main), StrokeThickness = 5.5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Triangle, Effect = Glow(core, 14) }, 0, 0);
        var inner = Add(new Path { Data = geo, Stroke = new SolidColorBrush(core), StrokeThickness = 2 }, 0, 0);
        FadeInOut(whip, 50, 150, 180);
        FadeInOut(inner, 50, 150, 160);
        if (thorny)
        {
            for (int k = 1; k <= 5; k++)
            {
                double t = k / 6.0;
                var p = new Point(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t + Math.Sin(t * Math.PI * 2) * 14);
                var thorn = Add(new Polygon { Points = [new(0, 8), new(5, 0), new(10, 8)], Fill = new SolidColorBrush(core), Effect = Glow(main, 6) }, p.X - 5, p.Y - 4);
                FadeInOut(thorn, 40, 140, 160);
            }
        }
        await Task.Delay(150);
        if (fiery) FlameBurst(to, 7, main, core);
        else { Ring(to, main, 74); Spray(to, core, 10, 58, 3, 300, 20); }
    }

    private async Task ClawRend(Point at, Color main, Color core, int count, bool roar, bool icy)
    {
        Claws(at, main, count, core);
        if (roar) { Ring(at, core, 90); Ring(at, main, 140); }
        if (icy) IceBurst(at, main);
        else Spray(at, main, 10, 62, 3, 320, 20);
        await Task.Delay(210);
    }

    private async Task FangBite(Point at, Point from, Color main, Color core, bool drain, bool venom, bool icy)
    {
        var upper = new Polygon { Points = [new(0, 0), new(10, 24), new(20, 4), new(30, 28), new(40, 4), new(50, 24), new(60, 0)], Fill = new SolidColorBrush(Colors.White), Stroke = new SolidColorBrush(main), StrokeThickness = 2, Effect = Glow(main, 12) };
        var lower = new Polygon { Points = [new(0, 28), new(10, 4), new(20, 24), new(30, 0), new(40, 24), new(50, 4), new(60, 28)], Fill = new SolidColorBrush(Colors.White), Stroke = new SolidColorBrush(main), StrokeThickness = 2, Effect = Glow(main, 12) };
        Add(upper, at.X - 30, at.Y - 56);
        Add(lower, at.X - 30, at.Y + 26);
        Move(upper, Canvas.TopProperty, at.Y - 22, 130, In);
        Move(lower, Canvas.TopProperty, at.Y - 4, 130, In);
        FadeInOut(upper, 40, 180, 180);
        FadeInOut(lower, 40, 180, 180);
        await Task.Delay(150);
        Ring(at, main, 80);
        if (venom) { for (int i = 0; i < 6; i++) Bubble(at.X < StageW / 2 ? left : right, Toxic, i * 35); }
        if (icy) IceBurst(at, main);
        if (drain) { for (int i = 0; i < 3; i++) _ = Fly(Orb(6, core, Colors.White), 12, 12, at, from, 280 + i * 50, 35 + i * 12); }
    }

    // ── Elemental Cataclysms & Projectiles ───────────────────────────────

    private async Task MeteorDrop(Point at, Color main, Color core)
    {
        var start = new Point(at.X - 150, -40);
        var rock = new Polygon
        {
            Points = [new(18, 0), new(40, 8), new(46, 26), new(34, 44), new(12, 42), new(0, 24), new(6, 8)],
            Fill = new RadialGradientBrush(core, Darken(main, .45)), Stroke = new SolidColorBrush(Ember), StrokeThickness = 3, Effect = Glow(Ember, 22)
        };
        Trail(start, at, 300, 0, Ember, 9, 18);
        await Fly(rock, 46, 44, start, at, 300, 0, 240);
        FlameBurst(at, 16, main, core);
        Flash(Ember, .42);
        ShakeStage(10);
    }

    private async Task SolarFlareBlast(Point at, Color main, Color core)
    {
        var sunPos = new Point(StageW / 2, 44);
        var sun = Add(Orb(26, main, Colors.White), sunPos.X - 26, sunPos.Y - 26);
        var scale = new ScaleTransform(.3, .3);
        sun.RenderTransformOrigin = new Point(.5, .5);
        sun.RenderTransform = scale;
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(.3, 1.4, TimeSpan.FromMilliseconds(220)) { EasingFunction = Out });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(.3, 1.4, TimeSpan.FromMilliseconds(220)) { EasingFunction = Out });
        FadeInOut(sun, 80, 220, 220);
        await Task.Delay(140);
        await RadiantBeam(sunPos, at, main, core);
        FlameBurst(at, 12, main, core);
        Flash(Flame, .38);
    }

    private async Task FlameColumns(Point at, Color main, Color core, int count)
    {
        for (int i = 0; i < count; i++)
        {
            double w = 26 + i * 4, x = at.X - (count - 1) * 18 + i * 36;
            var col = Add(new Rectangle
            {
                Width = w, Height = 150, RadiusX = w / 2, RadiusY = 12,
                Fill = new LinearGradientBrush(new GradientStopCollection { new(Color.FromArgb(0, main.R, main.G, main.B), 0), new(main, .45), new(core, 1) }, 90),
                Effect = Glow(main, 18), RenderTransformOrigin = new Point(.5, 1), RenderTransform = new ScaleTransform(1, .1)
            }, x - w / 2, at.Y - 110);
            ((ScaleTransform)col.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(.1, 1.1, TimeSpan.FromMilliseconds(180)) { BeginTime = TimeSpan.FromMilliseconds(i * 45), EasingFunction = Out });
            FadeInOut(col, 60, 200, 220, i * 45);
        }
        await Task.Delay(220);
        FlameBurst(at, 11, main, core);
        Flash(main, .28);
    }

    private async Task SparkVolley(Point from, Point to, Color main, Color core)
    {
        var flights = new List<Task>();
        for (int i = 0; i < 4; i++)
        {
            var spark = new Polygon { Points = [new(6, 0), new(8, 5), new(12, 6), new(8, 7), new(6, 12), new(4, 7), new(0, 6), new(4, 5)], Fill = new SolidColorBrush(i % 2 == 0 ? core : main), Effect = Glow(main, 8) };
            flights.Add(Fly(spark, 12, 12, new Point(from.X, from.Y + Random.Shared.Next(-12, 12)), new Point(to.X, to.Y + Random.Shared.Next(-16, 16)), 180 + i * 25, 22 + i * 8, 360));
            await Task.Delay(35);
        }
        await Task.WhenAll(flights);
        Spray(to, main, 9, 55, 2.6, 300, 15);
    }

    private async Task TidalWave(Point from, Point to, double dir, Color main, Color core, bool huge)
    {
        double w = huge ? 92 : 66, h = huge ? 110 : 72;
        var wave = new Path
        {
            Data = Geometry.Parse(dir > 0 ? "M 0,72 C 12,18 46,0 64,18 C 46,20 42,38 56,52 L 64,72 Z" : "M 64,72 C 52,18 18,0 0,18 C 18,20 22,38 8,52 L 0,72 Z"),
            Stretch = Stretch.Fill, Width = w, Height = h,
            Fill = new LinearGradientBrush(core, main, 90), Stroke = new SolidColorBrush(Colors.White), StrokeThickness = 2.5, Effect = Glow(main, 16)
        };
        Trail(from, to, 260, 10, main, 5, 24);
        await Fly(wave, w, h, new Point(from.X + dir * 20, to.Y + 4), new Point(to.X, to.Y + 2), 260, 6);
        Splash(to, huge ? 22 : 14, main);
        if (huge) { Flash(Water, .3); ShakeStage(8); }
    }

    private async Task HydroJetBeam(Point from, Point to, Color main, Color core)
    {
        double len = Math.Max(40, Math.Abs(to.X - from.X));
        var jet = Add(new Rectangle
        {
            Width = len, Height = 18, RadiusX = 9, RadiusY = 9,
            Fill = new LinearGradientBrush(new GradientStopCollection { new(main, 0), new(Colors.White, .5), new(core, 1) }, 90),
            Effect = Glow(main, 16), RenderTransformOrigin = new Point(from.X < to.X ? 0 : 1, .5), RenderTransform = new ScaleTransform(0, 1)
        }, Math.Min(from.X, to.X), (from.Y + to.Y) / 2 - 9);
        ((ScaleTransform)jet.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)) { EasingFunction = Out });
        await Task.Delay(170);
        Splash(to, 15, main);
        FadeOut(jet, 220);
    }

    private async Task LeviathanSurge(Point at, Color main, Color core)
    {
        var geo = new PathGeometry();
        var fig = new PathFigure { StartPoint = new Point(at.X - 70, at.Y + 40) };
        fig.Segments.Add(new BezierSegment(new Point(at.X - 40, at.Y - 90), new Point(at.X + 40, at.Y - 90), new Point(at.X + 60, at.Y + 35), true));
        geo.Figures.Add(fig);
        var serpent = Add(new Path { Data = geo, Stroke = new LinearGradientBrush(core, main, 90), StrokeThickness = 18, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Triangle, Effect = Glow(main, 20) }, 0, 0);
        FadeInOut(serpent, 80, 200, 240);
        await Task.Delay(180);
        Splash(at, 22, main);
        Flash(Water, .3);
        ShakeStage(8);
    }

    private async Task GlacierEruption(Point at, Color main, Color core)
    {
        for (int i = -1; i <= 1; i++)
        {
            double h = i == 0 ? 96 : 68, w = i == 0 ? 34 : 24;
            var spike = Add(new Polygon
            {
                Points = [new(w / 2, 0), new(w, h), new(0, h)],
                Fill = new LinearGradientBrush(Colors.White, main, 90), Stroke = new SolidColorBrush(core), StrokeThickness = 1.6, Effect = Glow(main, 14),
                RenderTransformOrigin = new Point(.5, 1), RenderTransform = new ScaleTransform(1, .1)
            }, at.X + i * 24 - w / 2, at.Y + 32 - h);
            ((ScaleTransform)spike.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(.1, 1, TimeSpan.FromMilliseconds(160)) { BeginTime = TimeSpan.FromMilliseconds(Math.Abs(i) * 40), EasingFunction = Out });
            FadeInOut(spike, 40, 240, 220, Math.Abs(i) * 40);
        }
        await Task.Delay(190);
        IceBurst(at, main);
        ShakeStage(6);
    }

    private async Task IceShardVolley(Point from, Point to, Color main, Color core, int shards, int ms)
    {
        var flights = new List<Task>();
        for (int i = 0; i < shards; i++)
        {
            var shard = new Polygon
            {
                Points = to.X >= from.X ? [new(0, 5), new(22, 0), new(30, 5), new(22, 10)] : [new(30, 5), new(8, 0), new(0, 5), new(8, 10)],
                Fill = new LinearGradientBrush(Colors.White, core, 0), Stroke = new SolidColorBrush(main), StrokeThickness = 1.2, Effect = Glow(core, 10)
            };
            var off = new Point(from.X, from.Y + (i - 1) * 14);
            if (i > 0) await Task.Delay(45);
            flights.Add(Fly(shard, 30, 10, off, new Point(to.X, to.Y + (i - 1) * 8), ms - 50, 10));
        }
        await Task.WhenAll(flights);
        IceBurst(to, main);
    }

    private async Task SnowPelletDrop(Point from, Point to, Color main, Color core, bool fromSky)
    {
        int n = fromSky ? 5 : 1;
        var flights = new List<Task>();
        for (int i = 0; i < n; i++)
        {
            double sz = fromSky ? 14 : 24;
            var ball = new Ellipse { Width = sz, Height = sz, Fill = new RadialGradientBrush(Colors.White, core), Stroke = new SolidColorBrush(main), StrokeThickness = 1.2, Effect = Glow(core, 10) };
            var start = fromSky ? new Point(to.X - 40 + i * 20, -20) : from;
            flights.Add(Fly(ball, sz, sz, start, new Point(to.X + Random.Shared.Next(-22, 22), to.Y + Random.Shared.Next(-14, 14)), fromSky ? 210 : 250, fromSky ? 0 : 54));
            if (fromSky) await Task.Delay(40);
        }
        await Task.WhenAll(flights);
        Spray(to, core, 12, 60, 3.2, 340, 30);
        Ring(to, Colors.White, 70);
    }

    private async Task AvalancheCrash(Point at, Color main, Color core)
    {
        var flights = new List<Task>();
        for (int i = 0; i < 5; i++)
        {
            double sz = 24 + i * 4;
            var boulder = new Ellipse { Width = sz, Height = sz * .85, Fill = new RadialGradientBrush(Colors.White, core), Stroke = new SolidColorBrush(main), StrokeThickness = 2, Effect = Glow(core, 12) };
            flights.Add(Fly(boulder, sz, sz, new Point(at.X - 80 + i * 22, -30), new Point(at.X - 30 + i * 16, at.Y + 8), 240, 0, 280));
            await Task.Delay(35);
        }
        await Task.WhenAll(flights);
        IceBurst(at, main);
        ShakeStage(9);
    }

    private async Task BlizzardSweep(Point at, double dir, Color main, Color core)
    {
        var rnd = Random.Shared;
        for (int i = 0; i < 12; i++)
        {
            double y = at.Y - 70 + rnd.Next(120);
            var gust = Add(new Rectangle { Width = 70 + rnd.Next(50), Height = 3, RadiusX = 2, RadiusY = 2, Fill = new LinearGradientBrush(Color.FromArgb(0, 255, 255, 255), core, dir > 0 ? 0 : 180) }, at.X - dir * 130, y);
            Move(gust, Canvas.LeftProperty, at.X + dir * 70, 280, Out, i * 18);
            FadeInOut(gust, 50, 140, 120, i * 18);
            var flake = Add(new TextBlock { Text = "❄", FontSize = 12 + rnd.Next(7), Foreground = new SolidColorBrush(i % 2 == 0 ? Colors.White : main), Effect = Glow(core, 4) }, at.X - dir * 100 + rnd.Next(-20, 20), y);
            Move(flake, Canvas.LeftProperty, at.X + dir * 60, 340, Out, i * 18);
            FadeInOut(flake, 50, 180, 140, i * 18);
        }
        Flash(core, .24);
        await Task.Delay(260);
    }

    private async Task SeedVolley(Point from, Point to, Color main, Color core)
    {
        var flights = new List<Task>();
        for (int i = 0; i < 4; i++)
        {
            var seed = new Ellipse { Width = 14, Height = 9, Fill = new LinearGradientBrush(core, main, 0), Stroke = new SolidColorBrush(Darken(main, .4)), StrokeThickness = 1, Effect = Glow(main, 6) };
            flights.Add(Fly(seed, 14, 9, new Point(from.X, from.Y + (i - 1.5) * 10), new Point(to.X, to.Y + (i - 1.5) * 8), 180, 16, 480));
            await Task.Delay(35);
        }
        await Task.WhenAll(flights);
        Spray(to, main, 10, 56, 3.2, 340, 30);
    }

    private async Task PetalStormSweep(Point from, Point to, Color main, Color core)
    {
        var flights = new List<Task>();
        Color pink = Color.FromRgb(0xF4, 0x72, 0xB6);
        for (int i = 0; i < 9; i++)
        {
            var petal = new Ellipse { Width = 15, Height = 8, Fill = new LinearGradientBrush(i % 2 == 0 ? pink : core, main, 45), Effect = Glow(i % 2 == 0 ? pink : main, 6) };
            flights.Add(Fly(petal, 15, 8, new Point(from.X, from.Y + Random.Shared.Next(-24, 24)), new Point(to.X + Random.Shared.Next(-24, 24), to.Y + Random.Shared.Next(-24, 24)), 240 + i * 18, 36 + i * 6, 720));
            await Task.Delay(25);
        }
        await Task.WhenAll(flights);
        Spray(to, pink, 10, 66, 3.2, 360, 22);
    }

    private async Task RailgunShot(Point from, Point to, double dir, Color main, Color core)
    {
        // Concentric accelerator rings along barrel
        for (int i = 0; i < 3; i++)
        {
            double x = from.X + dir * (22 + i * 24);
            var coil = Add(new Ellipse { Width = 14, Height = 34, Stroke = new SolidColorBrush(core), StrokeThickness = 2.5, Effect = Glow(main, 10) }, x - 7, from.Y - 17);
            FadeInOut(coil, 40, 140, 160, i * 25);
        }
        await Task.Delay(90);
        double len = Math.Max(60, Math.Abs(to.X - from.X) + 30);
        var beam = Add(new Rectangle
        {
            Width = len, Height = 12, RadiusX = 6, RadiusY = 6,
            Fill = new LinearGradientBrush(new GradientStopCollection { new(main, 0), new(Colors.White, .5), new(main, 1) }, 90),
            Effect = Glow(core, 22)
        }, Math.Min(from.X, to.X), from.Y - 6);
        FadeInOut(beam, 30, 130, 180);
        Flash(core, .32);
        Spray(to, core, 14, 80, 3, 320, 10);
        await Task.Delay(160);
    }

    private async Task GlitchStrike(Point at, Color main, Color core)
    {
        Color[] glitchColors = [Color.FromRgb(0x22, 0xD3, 0xEE), Color.FromRgb(0xF4, 0x3F, 0x5E), main, core];
        var rnd = Random.Shared;
        for (int i = 0; i < 12; i++)
        {
            double w = 24 + rnd.Next(60), h = 4 + rnd.Next(7);
            var bar = Add(new Rectangle { Width = w, Height = h, Fill = new SolidColorBrush(glitchColors[i % glitchColors.Length]), Opacity = .9, Effect = Glow(main, 6) },
                at.X - 55 + rnd.Next(65), at.Y - 50 + rnd.Next(90));
            FadeInOut(bar, 25, 110, 90, i * 16);
        }
        await Task.Delay(210);
    }

    private async Task SkyThunder(Point at, Color main, Color core, int bolts)
    {
        for (int i = 0; i < bolts; i++)
            Lightning(new Point(at.X + (i - (bolts - 1) / 2.0) * 26, -12), new Point(at.X + (i - (bolts - 1) / 2.0) * 14, at.Y + 14), 2, main, core);
        Flash(core, .38);
        Ring(at, main, 105);
        Spray(at, core, 14, 76, 3, 320, 14);
        ShakeStage(7);
        await Task.Delay(200);
    }

    private async Task TaserProngs(Point from, Point to, Color main, Color core)
    {
        var p1 = Fly(new Rectangle { Width = 14, Height = 4, Fill = new SolidColorBrush(core), Effect = Glow(main, 8) }, 14, 4, new Point(from.X, from.Y - 8), new Point(to.X, to.Y - 12), 160, 6);
        var p2 = Fly(new Rectangle { Width = 14, Height = 4, Fill = new SolidColorBrush(core), Effect = Glow(main, 8) }, 14, 4, new Point(from.X, from.Y + 8), new Point(to.X, to.Y + 12), 160, 6);
        await Task.WhenAll(p1, p2);
        Lightning(new Point(to.X, to.Y - 18), new Point(to.X, to.Y + 18), 3, main, core);
        Spray(to, core, 10, 58, 2.5, 280, 10);
    }

    private async Task WingedSwarm(Point from, Point to, double dir, Color main, Color core, bool ravens)
    {
        var flights = new List<Task>();
        for (int i = 0; i < 6; i++)
        {
            var wing = new Path
            {
                Data = Geometry.Parse(ravens ? "M 0,10 Q 8,0 14,9 Q 20,0 28,10 Q 18,12 14,15 Q 10,12 0,10 Z" : "M 0,8 Q 6,0 12,7 Q 18,0 24,8 L 19,12 L 12,9 L 5,12 Z"),
                Fill = new SolidColorBrush(ravens ? Color.FromRgb(0x1E, 0x1B, 0x4B) : Darken(main, .45)),
                Stroke = new SolidColorBrush(core), StrokeThickness = 1, Effect = Glow(main, 8)
            };
            flights.Add(Fly(wing, 28, 16, new Point(from.X, from.Y + Random.Shared.Next(-26, 26)), new Point(to.X + Random.Shared.Next(-20, 20), to.Y + Random.Shared.Next(-24, 24)), 230 + i * 25, 32 + i * 8));
            await Task.Delay(30);
        }
        await Task.WhenAll(flights);
        Smoke(to, main, 5);
    }

    private async Task TentacleErupt(Point at, Color main, Color core)
    {
        for (int i = -1; i <= 1; i += 2)
        {
            var geo = new PathGeometry();
            var fig = new PathFigure { StartPoint = new Point(at.X + i * 44, at.Y + 42) };
            fig.Segments.Add(new BezierSegment(new Point(at.X + i * 54, at.Y - 20), new Point(at.X + i * 10, at.Y - 36), new Point(at.X - i * 8, at.Y - 6), true));
            geo.Figures.Add(fig);
            var tent = Add(new Path { Data = geo, Stroke = new SolidColorBrush(main), StrokeThickness = 9, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Triangle, Effect = Glow(core, 12) }, 0, 0);
            FadeInOut(tent, 70, 220, 200);
        }
        Smoke(at, Darken(main, .5), 5);
        await Task.Delay(220);
    }

    private async Task VoidRiftTear(Point at, Color main, Color core, bool grand)
    {
        double h = grand ? 120 : 88;
        var rift = Add(new Polygon
        {
            Points = [new(18, 0), new(32, h * .35), new(22, h * .5), new(34, h * .7), new(16, h), new(4, h * .65), new(12, h * .45), new(2, h * .25)],
            Fill = new SolidColorBrush(Color.FromRgb(0x0F, 0x0A, 0x24)), Stroke = new SolidColorBrush(core), StrokeThickness = 3, Effect = Glow(main, 22),
            RenderTransformOrigin = new Point(.5, .5), RenderTransform = new ScaleTransform(.1, 1)
        }, at.X - 18, at.Y - h / 2);
        ((ScaleTransform)rift.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(.1, 1.25, TimeSpan.FromMilliseconds(180)) { EasingFunction = Out });
        FadeInOut(rift, 50, 240, 200);
        await Task.Delay(180);
        Ring(at, main, grand ? 125 : 90);
        Smoke(at, Darken(main, .45), 6);
        if (grand) Flash(main, .3);
    }

    private async Task EclipseBeam(Point at, Color main, Color core)
    {
        var sunPos = new Point(at.X, 46);
        var corona = Add(new Ellipse { Width = 58, Height = 58, Fill = new SolidColorBrush(Color.FromRgb(0x0F, 0x0A, 0x24)), Stroke = new SolidColorBrush(core), StrokeThickness = 4, Effect = Glow(main, 24) }, sunPos.X - 29, sunPos.Y - 29);
        FadeInOut(corona, 80, 280, 220);
        await Task.Delay(120);
        await Pillar(at, false, main, core, cross: false);
    }

    private async Task RadiantBeam(Point from, Point to, Color main, Color core)
    {
        double dx = to.X - from.X, dy = to.Y - from.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);
        double ang = Math.Atan2(dy, dx) * 180 / Math.PI;
        var beam = Add(new Rectangle
        {
            Width = len, Height = 22, RadiusX = 11, RadiusY = 11,
            Fill = new LinearGradientBrush(new GradientStopCollection { new(Color.FromArgb(0, main.R, main.G, main.B), 0), new(main, .25), new(Colors.White, .5), new(core, .75), new(Color.FromArgb(0, main.R, main.G, main.B), 1) }, 90),
            Effect = Glow(core, 22), RenderTransformOrigin = new Point(0, .5), RenderTransform = new RotateTransform(ang)
        }, from.X, from.Y - 11);
        FadeInOut(beam, 60, 170, 200);
        Ring(to, core, 90);
        Spray(to, main, 12, 68, 3, 340, -10);
        await Task.Delay(180);
    }

    private async Task StarShot(Point from, Point to, Color main, Color core, bool multi)
    {
        var star = new Polygon
        {
            Points = [new(13, 0), new(16, 10), new(26, 13), new(16, 16), new(13, 26), new(10, 16), new(0, 13), new(10, 10)],
            Fill = new SolidColorBrush(Colors.White), Stroke = new SolidColorBrush(main), StrokeThickness = 1.5, Effect = Glow(core, 16)
        };
        Trail(from, to, multi ? 180 : 230, 24, core, 4, 24);
        await Fly(star, 26, 26, from, to, multi ? 180 : 230, 24, 360);
        Ring(to, core, 72);
        Spray(to, main, 10, 56, 2.8, 300, 10);
    }

    private async Task StarfallRain(Point at, Color main, Color core)
    {
        var flights = new List<Task>();
        for (int i = 0; i < 4; i++)
        {
            var star = new Polygon { Points = [new(11, 0), new(14, 8), new(22, 11), new(14, 14), new(11, 22), new(8, 14), new(0, 11), new(8, 8)], Fill = new SolidColorBrush(Colors.White), Stroke = new SolidColorBrush(main), StrokeThickness = 1.4, Effect = Glow(core, 14) };
            var start = new Point(at.X - 110 + i * 34, -24);
            var end = new Point(at.X - 28 + i * 18, at.Y + Random.Shared.Next(-14, 14));
            Trail(start, end, 220, 0, core, 3.5, 28);
            flights.Add(Fly(star, 22, 22, start, end, 220, 0, 420));
            await Task.Delay(50);
        }
        await Task.WhenAll(flights);
        Ring(at, core, 95);
        Spray(at, main, 12, 66, 3, 340, 12);
    }

    // ── Core Elemental Sub-effects ───────────────────────────────────────

    private void FlameBurst(Point at, int count, Color? edge = null, Color? inner = null)
    {
        Color cEdge = edge ?? Ember, cCore = inner ?? Flame;
        var rnd = Random.Shared;
        for (int i = 0; i < count; i++)
        {
            double w = 10 + rnd.Next(10), h = w * 1.6;
            var tongue = Add(new Ellipse
            {
                Width = w, Height = h, Effect = Glow(cEdge, 10),
                Fill = new RadialGradientBrush(cCore, cEdge) { GradientOrigin = new Point(.5, .8), Center = new Point(.5, .7) }
            }, at.X - w / 2 + rnd.Next(-30, 30), at.Y - h / 2 + rnd.Next(-6, 16));
            Move(tongue, Canvas.TopProperty, Canvas.GetTop(tongue) - 40 - rnd.Next(30), 520, Out);
            FadeOut(tongue, 520);
        }
        Spray(at, cEdge, count, 50, 2.5, 420, -20);
        Ring(at, cEdge, 70);
    }

    private void Splash(Point at, int count, Color? tone = null)
    {
        Color c = tone ?? Water;
        Spray(at, c, count, 70, 3.5, 380, 60);
        var wave = Add(new Ellipse { Width = 40, Height = 12, Stroke = new SolidColorBrush(c), StrokeThickness = 3, RenderTransformOrigin = new Point(.5, .5), RenderTransform = new ScaleTransform(.4, .4) }, at.X - 20, at.Y + 30);
        var grow = new DoubleAnimation(.4, 3, TimeSpan.FromMilliseconds(420)) { EasingFunction = Out };
        ((ScaleTransform)wave.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        ((ScaleTransform)wave.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        FadeOut(wave, 420);
    }

    private void IceBurst(Point at, Color? tone = null)
    {
        Color c = tone ?? Ice;
        var rnd = Random.Shared;
        for (int i = 0; i < 8; i++)
        {
            double ang = i * Math.PI / 4 + rnd.NextDouble() * .3;
            var shard = Add(new Polygon { Points = [new(0, 3), new(14, 0), new(18, 3), new(14, 6)], Fill = new SolidColorBrush(c), Effect = Glow(Colors.White, 6), RenderTransform = new RotateTransform(ang * 180 / Math.PI) }, at.X, at.Y);
            Move(shard, Canvas.LeftProperty, at.X + Math.Cos(ang) * 50, 380, Out);
            Move(shard, Canvas.TopProperty, at.Y + Math.Sin(ang) * 50, 380, Out);
            FadeOut(shard, 380, 80);
        }
        Ring(at, Colors.White, 80);
        Flash(Ice, .22);
    }

    private void Lightning(Point from, Point to, int bolts, Color? edge = null, Color? inner = null)
    {
        Color cEdge = edge ?? Bolt, cInner = inner ?? Colors.White;
        var rnd = Random.Shared;
        for (int b = 0; b < bolts; b++)
        {
            var points = new PointCollection { from };
            int steps = 9;
            for (int i = 1; i < steps; i++)
            {
                double k = i / (double)steps;
                points.Add(new Point(from.X + (to.X - from.X) * k + rnd.Next(-10, 10), from.Y + (to.Y - from.Y) * k + rnd.Next(-22, 22)));
            }
            points.Add(to);
            var bolt = Add(new Polyline { Points = points, Stroke = new SolidColorBrush(b == 0 ? cInner : cEdge), StrokeThickness = b == 0 ? 4 : 2.2, StrokeLineJoin = PenLineJoin.Round, Effect = Glow(cEdge, 16) }, 0, 0);
            var flicker = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(320) };
            foreach (var (v, t) in new[] { (1.0, 0.0), (.2, .25), (1.0, .4), (.5, .7), (0.0, 1.0) })
                flicker.KeyFrames.Add(new DiscreteDoubleKeyFrame(v, KeyTime.FromPercent(t)));
            flicker.Completed += (_, _) => FxLayer.Children.Remove(bolt);
            bolt.BeginAnimation(OpacityProperty, flicker);
        }
    }

    private void Claws(Point at, Color color, int count, Color? glow = null)
    {
        for (int i = 0; i < count; i++)
        {
            double x = at.X - 28 + i * 15;
            var claw = Add(new Line { X1 = 0, Y1 = 0, X2 = 30, Y2 = 54, Stroke = new SolidColorBrush(color), StrokeThickness = 5, StrokeStartLineCap = PenLineCap.Triangle, StrokeEndLineCap = PenLineCap.Triangle, Effect = Glow(glow ?? Colors.White, 10), RenderTransform = new ScaleTransform(1, 0) }, x, at.Y - 40);
            ((ScaleTransform)claw.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(110)) { BeginTime = TimeSpan.FromMilliseconds(i * 45) });
            FadeOut(claw, 280, 190 + i * 45);
        }
    }

    private void Smoke(Point at, Color color, int count)
    {
        var rnd = Random.Shared;
        for (int i = 0; i < count; i++)
        {
            double r = 12 + rnd.Next(12);
            var puff = Add(new Ellipse { Width = r * 2, Height = r * 2, Fill = new RadialGradientBrush(Color.FromArgb(0xAA, color.R, color.G, color.B), Color.FromArgb(0, color.R, color.G, color.B)), RenderTransformOrigin = new Point(.5, .5), RenderTransform = new ScaleTransform(.5, .5) }, at.X - r + rnd.Next(-30, 30), at.Y - r + rnd.Next(-20, 20));
            var grow = new DoubleAnimation(.5, 1.6, TimeSpan.FromMilliseconds(700)) { EasingFunction = Out };
            ((ScaleTransform)puff.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            ((ScaleTransform)puff.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, grow);
            Move(puff, Canvas.TopProperty, Canvas.GetTop(puff) - 24, 700, Out);
            FadeOut(puff, 700);
        }
    }

    private async Task Pillar(Point at, bool thin, Color? edge = null, Color? inner = null, bool cross = false)
    {
        Color cEdge = edge ?? Holy, cInner = inner ?? Colors.White;
        double w = thin ? 30 : 56;
        var beam = Add(new Rectangle
        {
            Width = w, Height = at.Y + 40, RadiusX = w / 2, RadiusY = w / 2, Opacity = 0,
            Fill = new LinearGradientBrush(new GradientStopCollection { new(Color.FromArgb(0, 255, 255, 255), 0), new(cEdge, .5), new(cInner, 1) }, 90),
            Effect = Glow(cEdge, 24), RenderTransformOrigin = new Point(.5, 1), RenderTransform = new ScaleTransform(.2, 1)
        }, at.X - w / 2, -10);
        beam.BeginAnimation(OpacityProperty, new DoubleAnimation(1, TimeSpan.FromMilliseconds(120)));
        ((ScaleTransform)beam.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(.2, 1, TimeSpan.FromMilliseconds(180)) { EasingFunction = Out });
        if (cross)
        {
            var bar = Add(new Rectangle { Width = 96, Height = 14, RadiusX = 7, RadiusY = 7, Fill = new SolidColorBrush(cInner), Effect = Glow(cEdge, 20) }, at.X - 48, at.Y - 58);
            FadeInOut(bar, 80, 180, 240);
        }
        await Task.Delay(thin ? 120 : 180);
        Spray(at, cEdge, thin ? 5 : 10, 60, 3, 420, -10);
        Ring(at, cEdge, thin ? 50 : 90);
        if (!thin) Flash(cEdge, .28);
        FadeOut(beam, 320, 60);
    }

    private void Slash(Point at, Color color, int count, bool flip = false)
    {
        for (int i = 0; i < count; i++)
        {
            var arc = Add(new Path
            {
                Data = Geometry.Parse("M 0,40 Q 30,-10 70,10"), Stroke = new SolidColorBrush(color), StrokeThickness = 5.5,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Triangle, Effect = Glow(color, 10),
                RenderTransformOrigin = new Point(.5, .5), RenderTransform = new RotateTransform((i == 0 && !flip) ? 0 : 160)
            }, at.X - 35, at.Y - 30 + i * 8);
            FadeInOut(arc, 50, 80, 210, i * 65);
        }
    }

    private void Streak(Point at, double dir)
    {
        var line = Add(new Rectangle { Width = 120, Height = 4, Fill = new LinearGradientBrush(Color.FromArgb(0, 255, 255, 255), Colors.White, dir > 0 ? 0 : 180), Effect = Glow(Colors.White, 8) }, at.X - 60, at.Y - 2);
        Move(line, Canvas.LeftProperty, at.X - 60 + dir * 60, 200, Out);
        FadeOut(line, 200);
    }

    // ── Bespoke Support / Shield / Skill Sub-effects ─────────────────────

    private void ShieldCrest(Point at, Color main, Color core, bool halo)
    {
        var crest = Add(new Polygon
        {
            Points = [new(28, 0), new(56, 12), new(50, 44), new(28, 66), new(6, 44), new(0, 12)],
            Fill = new RadialGradientBrush(Color.FromArgb(0x33, core.R, core.G, core.B), Color.FromArgb(0xAA, main.R, main.G, main.B)),
            Stroke = new SolidColorBrush(core), StrokeThickness = 2.6, Effect = Glow(main, 16)
        }, at.X - 28, at.Y - 38);
        FadeInOut(crest, 90, 460, 260);
        if (halo) HaloRing(at, core);
    }

    private void HaloRing(Point at, Color color)
    {
        var halo = Add(new Ellipse { Width = 78, Height = 20, Stroke = new SolidColorBrush(color), StrokeThickness = 4, Effect = Glow(color, 16) }, at.X - 39, at.Y - 76);
        Move(halo, Canvas.TopProperty, at.Y - 84, 700, Out);
        FadeInOut(halo, 90, 420, 240);
    }

    private void FlameWall(Point at, Color main, Color core)
    {
        for (int i = -2; i <= 2; i++)
        {
            var tongue = Add(new Ellipse
            {
                Width = 18, Height = 42, Fill = new RadialGradientBrush(core, main) { GradientOrigin = new Point(.5, .8) }, Effect = Glow(main, 12)
            }, at.X + i * 22 - 9, at.Y - 12);
            Move(tongue, Canvas.TopProperty, at.Y - 34, 620, Out, Math.Abs(i) * 40);
            FadeInOut(tongue, 80, 320, 220, Math.Abs(i) * 40);
        }
    }

    private void CrystalWall(Point at, Color main, Color core)
    {
        for (int i = -2; i <= 2; i++)
        {
            double h = 50 - Math.Abs(i) * 7;
            var hex = Add(new Polygon
            {
                Points = [new(11, 0), new(22, 12), new(22, h - 12), new(11, h), new(0, h - 12), new(0, 12)],
                Fill = new LinearGradientBrush(Color.FromArgb(0xAA, core.R, core.G, core.B), Color.FromArgb(0x66, main.R, main.G, main.B), 90),
                Stroke = new SolidColorBrush(Colors.White), StrokeThickness = 1.6, Effect = Glow(core, 10)
            }, at.X + i * 20 - 11, at.Y - h / 2);
            FadeInOut(hex, 80, 420, 240, Math.Abs(i) * 35);
        }
    }

    private void BubbleDome(Point at, Color main, Color core)
    {
        var dome = Add(new Ellipse
        {
            Width = 132, Height = 120, Stroke = new SolidColorBrush(core), StrokeThickness = 2.6,
            Fill = new RadialGradientBrush(Color.FromArgb(0x18, 255, 255, 255), Color.FromArgb(0x66, main.R, main.G, main.B)),
            Effect = Glow(main, 14)
        }, at.X - 66, at.Y - 64);
        FadeInOut(dome, 90, 480, 260);
        for (int i = 0; i < 6; i++) Bubble(at.X < StageW / 2 ? left : right, core, i * 45);
    }

    private void VineBarrier(Point at, Color main, Color core)
    {
        for (int i = 0; i < 3; i++)
        {
            var ring = Add(new Ellipse
            {
                Width = 112 - i * 12, Height = 34 + i * 8, Stroke = new SolidColorBrush(i == 1 ? core : main), StrokeThickness = 4.5,
                StrokeDashArray = [5, 2], Effect = Glow(main, 8)
            }, at.X - (112 - i * 12) / 2, at.Y - 26 + i * 16);
            FadeInOut(ring, 80, 420, 220, i * 45);
        }
    }

    private void ElectricShield(Point at, Color main, Color core)
    {
        var hex = Add(new Polygon
        {
            Points = [new(60, 0), new(115, 30), new(115, 90), new(60, 120), new(5, 90), new(5, 30)],
            Stroke = new SolidColorBrush(core), StrokeThickness = 3, Fill = new SolidColorBrush(Color.FromArgb(0x22, main.R, main.G, main.B)), Effect = Glow(main, 16)
        }, at.X - 60, at.Y - 64);
        FadeInOut(hex, 60, 440, 220);
        Spray(at, core, 10, 68, 2.4, 340, 0);
    }

    private void Wings(Point at, Color main, Color core)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            var wing = Add(new Path
            {
                Data = Geometry.Parse(side < 0 ? "M 70,56 C 42,6 6,2 0,28 C 12,34 6,52 24,52 C 18,62 34,68 58,62 Z" : "M 0,56 C 28,6 64,2 70,28 C 58,34 64,52 46,52 C 52,62 36,68 12,62 Z"),
                Fill = new LinearGradientBrush(core, main, 90), Stroke = new SolidColorBrush(Colors.White), StrokeThickness = 1.6, Effect = Glow(main, 18)
            }, at.X + (side < 0 ? -94 : 24), at.Y - 62);
            FadeInOut(wing, 100, 460, 280);
        }
    }

    private void RisingLeaves(Point at, Color color, int count)
    {
        var rnd = Random.Shared;
        for (int i = 0; i < count; i++)
        {
            var leaf = Add(new Ellipse { Width = 12, Height = 6, Fill = new SolidColorBrush(color), Effect = Glow(color, 6), RenderTransform = new RotateTransform(rnd.Next(360)) },
                at.X - 48 + rnd.Next(96), at.Y + 20 - rnd.Next(20));
            Move(leaf, Canvas.TopProperty, Canvas.GetTop(leaf) - 65 - rnd.Next(25), 820, Out, i * 40);
            FadeInOut(leaf, 80, 440, 260, i * 40);
        }
    }

    private void MusicNotes(Point from, Point to, Color color)
    {
        string[] notes = ["♪", "♫", "♪", "♫"];
        for (int i = 0; i < notes.Length; i++)
        {
            var n = Add(new TextBlock { Text = notes[i], FontSize = 18 + (i % 2) * 4, FontWeight = FontWeights.Black, Foreground = new SolidColorBrush(color), Effect = Glow(Colors.White, 5) }, from.X, from.Y - 26 + i * 8);
            Move(n, Canvas.LeftProperty, to.X - 20 + i * 14, 520, Out, i * 70);
            Move(n, Canvas.TopProperty, to.Y - 46 - (i % 2) * 16, 520, Out, i * 70);
            FadeInOut(n, 70, 320, 220, i * 70);
        }
    }

    private void RainCloud(Point at, Color color)
    {
        var cloud = Add(new Ellipse { Width = 92, Height = 30, Fill = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)), Effect = Glow(color, 10) }, at.X - 46, at.Y - 94);
        FadeInOut(cloud, 100, 460, 240);
        for (int i = 0; i < 8; i++)
        {
            var drop = Add(new Ellipse { Width = 4, Height = 10, Fill = new SolidColorBrush(color) }, at.X - 36 + i * 10, at.Y - 68);
            Move(drop, Canvas.TopProperty, at.Y + 24, 520, In, i * 45);
            FadeInOut(drop, 50, 300, 180, i * 45);
        }
    }

    private void LavaPool(Point at, Color main, Color core)
    {
        var pool = Add(new Ellipse { Width = 120, Height = 26, Fill = new RadialGradientBrush(core, main), Stroke = new SolidColorBrush(Ember), StrokeThickness = 2, Effect = Glow(Ember, 14) }, at.X - 60, at.Y + 22);
        FadeInOut(pool, 100, 460, 260);
        FlameBurst(at, 6, main, core);
    }

    private void SporeCloud(Point at, Color color)
    {
        Smoke(at, color, 7);
        Spray(at, color, 12, 66, 3.2, 460, -14, up: true);
    }

    private void IceCage(Point at, Color main, Color core)
    {
        for (int i = -2; i <= 2; i++)
        {
            var bar = Add(new Rectangle { Width = 7, Height = 96, RadiusX = 3, RadiusY = 3, Fill = new LinearGradientBrush(Colors.White, main, 90), Effect = Glow(core, 10) }, at.X + i * 20 - 3.5, at.Y - 56);
            FadeInOut(bar, 70, 420, 220, Math.Abs(i) * 35);
        }
        Ring(at, core, 105);
    }

    private void HexSigil(Point at, Color main, Color core)
    {
        var outer = Add(new Ellipse { Width = 108, Height = 108, Stroke = new SolidColorBrush(main), StrokeThickness = 3.5, StrokeDashArray = [6, 3], Effect = Glow(core, 14), RenderTransformOrigin = new Point(.5, .5), RenderTransform = new RotateTransform() }, at.X - 54, at.Y - 54);
        ((RotateTransform)outer.RenderTransform).BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 180, TimeSpan.FromMilliseconds(720)));
        FadeInOut(outer, 80, 400, 240);
        Smoke(at, Darken(main, .5), 5);
    }

    private void SunburstFlash(Point at, Color core)
    {
        Flash(core, .48);
        Ring(at, Colors.White, 120);
        Spray(at, core, 16, 84, 3.5, 360, 0);
    }

    private void EnergySurge(Point at, Color main, Color core)
    {
        Ring(at, main, 88);
        Ring(at, core, 124);
        Spray(at, core, 14, 70, 3.2, 400, -35, up: true);
    }

    private void CrossedBlades(Point at, Color main, Color core)
    {
        for (int i = 0; i < 2; i++)
        {
            var sword = BuildSword(main, core, 64);
            sword.RenderTransformOrigin = new Point(.5, .5);
            sword.RenderTransform = new RotateTransform(i == 0 ? -42 : -138);
            Add(sword, at.X - 32, at.Y - 22);
            FadeInOut(sword, 70, 380, 220);
        }
    }

    private void EnergyOrbFlight(StagePet fromPet, StagePet toPet, Color color)
    {
        var from = CenterOf(fromPet);
        var to = CenterOf(toPet);
        Trail(from, to, 340, 44, color, 4.5, 28);
        _ = Fly(Orb(10, color, Colors.White), 20, 20, from, to, 340, 44);
    }

    // ── Statuses & Persistent Auras ──────────────────────────────────────

    /// <summary>The moment a status lands on a pet.</summary>
    private void StatusHit(StagePet pet, string kind)
    {
        var at = CenterOf(pet);
        switch (kind)
        {
            case "poisoned":
                for (int i = 0; i < 8; i++) Bubble(pet, Toxic, i * 40);
                Smoke(at, Toxic, 4);
                break;
            case "sleep":
                for (int i = 0; i < 3; i++) Zee(pet, i * 220);
                Ring(at, Dream, 100);
                break;
            case "fear":
                Smoke(at, Dread, 5);
                Ring(at, Dread, 110);
                SweatDrop(pet);
                break;
            case "slow":
                for (int i = 0; i < 6; i++) Snowflake(pet, i * 60);
                Ring(at, Chill, 100);
                break;
            case "regen":
                for (int i = 0; i < 6; i++) RisingPlus(pet, LeafGreen, i * 60);
                break;
            case "rage":
                Ring(at, Fury, 80);
                Ring(at, Ember, 120);
                Spray(at, Fury, 12, 60, 3, 420, -40, up: true);
                break;
            case "weak":
                for (int i = 0; i < 5; i++) FallingArrow(pet);
                Ring(at, Dread, 90);
                break;
        }
    }

    private void Bubble(StagePet pet, Color color, int delay)
    {
        var at = CenterOf(pet);
        var rnd = Random.Shared;
        double r = 3 + rnd.Next(5);
        var b = new Ellipse { Width = r * 2, Height = r * 2, Stroke = new SolidColorBrush(color), StrokeThickness = 1.5, Fill = new SolidColorBrush(Color.FromArgb(0x55, color.R, color.G, color.B)) };
        Add(b, at.X - r + rnd.Next(-40, 40), at.Y + 20 - rnd.Next(20));
        Move(b, Canvas.TopProperty, Canvas.GetTop(b) - 50 - rnd.Next(30), 900, Out, delay);
        FadeInOut(b, 100, 600, 200, delay);
    }

    private void Zee(StagePet pet, int delay)
    {
        var at = CenterOf(pet);
        var z = new TextBlock { Text = "z", FontSize = 14, FontWeight = FontWeights.Black, Foreground = new SolidColorBrush(Dream), Effect = new DropShadowEffect { Color = Colors.White, BlurRadius = 3, ShadowDepth = 0 }, RenderTransformOrigin = new Point(.5, .5), RenderTransform = new ScaleTransform(.6, .6) };
        double x = at.X + pet.Visual.Width * .14 * pet.Facing, y = at.Y - pet.Visual.Height * .42;
        Add(z, x, y);
        Move(z, Canvas.LeftProperty, x + 26 * pet.Facing, 1300, Out, delay);
        Move(z, Canvas.TopProperty, y - 46, 1300, Out, delay);
        var grow = new DoubleAnimation(.6, 1.5, TimeSpan.FromMilliseconds(1300)) { BeginTime = TimeSpan.FromMilliseconds(delay) };
        ((ScaleTransform)z.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        ((ScaleTransform)z.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        FadeInOut(z, 150, 850, 300, delay);
    }

    private void SweatDrop(StagePet pet)
    {
        var at = CenterOf(pet);
        var drop = new Ellipse { Width = 7, Height = 11, Fill = new LinearGradientBrush(Colors.White, Color.FromRgb(0x93, 0xC5, 0xFD), 90), Stroke = new SolidColorBrush(Color.FromRgb(0x60, 0xA5, 0xFA)), StrokeThickness = 1 };
        double x = at.X - pet.Visual.Width * .18 * pet.Facing, y = at.Y - pet.Visual.Height * .36;
        Add(drop, x, y);
        Move(drop, Canvas.TopProperty, y + 22, 700, In);
        FadeOut(drop, 250, 500);
    }

    private void Snowflake(StagePet pet, int delay)
    {
        var at = CenterOf(pet);
        var rnd = Random.Shared;
        var flake = new TextBlock { Text = "❄", FontSize = 11 + rnd.Next(7), Foreground = new SolidColorBrush(Chill), Effect = Glow(Colors.White, 4) };
        double x = at.X - 50 + rnd.Next(100), y = at.Y - pet.Visual.Height * .45;
        Add(flake, x, y);
        Move(flake, Canvas.TopProperty, y + 70, 1400, null, delay);
        Move(flake, Canvas.LeftProperty, x + rnd.Next(-16, 16), 1400, null, delay);
        FadeInOut(flake, 150, 950, 300, delay);
    }

    private void RisingPlus(StagePet pet, Color color, int delay)
    {
        var at = CenterOf(pet);
        var rnd = Random.Shared;
        var plus = new TextBlock { Text = "+", FontSize = 13 + rnd.Next(8), FontWeight = FontWeights.Black, Foreground = new SolidColorBrush(color), Effect = Glow(Colors.White, 3) };
        double x = at.X - 40 + rnd.Next(80), y = at.Y + 10;
        Add(plus, x, y);
        Move(plus, Canvas.TopProperty, y - 60, 900, Out, delay);
        FadeInOut(plus, 120, 530, 250, delay);
    }

    private void Flicker(StagePet pet, Color core, Color edge)
    {
        var at = CenterOf(pet);
        var rnd = Random.Shared;
        double w = 6 + rnd.Next(6), h = w * 1.7;
        var flame = Add(new Ellipse { Width = w, Height = h, Fill = new RadialGradientBrush(core, edge) { GradientOrigin = new Point(.5, .8) }, Effect = Glow(edge, 6) },
            at.X - 44 + rnd.Next(88), at.Y + 10 - rnd.Next(30));
        Move(flame, Canvas.TopProperty, Canvas.GetTop(flame) - 26, 520, Out);
        FadeOut(flame, 520);
    }

    private void FallingArrow(StagePet pet)
    {
        var at = CenterOf(pet);
        var arrow = Add(new TextBlock { Text = "▼", FontSize = 10, Foreground = new SolidColorBrush(Dread), Opacity = .8 }, at.X - 50 + Random.Shared.Next(100), at.Y - 50);
        Move(arrow, Canvas.TopProperty, at.Y - 10, 700, In);
        FadeOut(arrow, 700);
    }

    /// <summary>Called every animation frame: keeps a light trickle of particles on pets with active statuses.</summary>
    private void StatusAuras()
    {
        if (cardSession is not { } s || BattlePanel.Visibility != Visibility.Visible || !CardBar.IsVisible) return;
        if (++auraTick % 9 != 0) return;
        bool even = auraTick % 18 == 0;
        foreach (var (pet, side) in new[] { (left, s.Me), (right, s.Them) })
        {
            if (pet.Fainted) continue;
            if (side.Burn > 0) Flicker(pet, Flame, Ember);
            if (side.Poison > 0) Bubble(pet, Toxic, 0);
            if (side.Sleep > 0 && even) Zee(pet, 0);
            if (side.Fear > 0 && even) SweatDrop(pet);
            if (side.Slow > 0 && even) Snowflake(pet, 0);
            if (side.Regen > 0 && even) RisingPlus(pet, LeafGreen, 0);
            if (side.Rage > 0) Flicker(pet, Flame, Fury);
            if (side.Weak > 0 && even) FallingArrow(pet);
        }
    }

    /// <summary>A lasting status shows on the pet's face when nothing else is.</summary>
    private string StatusMood(StagePet pet, string mood)
    {
        if (mood.Length > 0 || pet.Fainted || cardSession is not { } s || BattlePanel.Visibility != Visibility.Visible) return mood;
        var side = pet == left ? s.Me : s.Them;
        return side.Sleep > 0 ? PetMood.Sleeping : side.Fear > 0 ? PetMood.Nervous : side.Rage > 0 ? PetMood.Angry : side.Poison > 0 ? PetMood.Sad : mood;
    }

    /// <summary>Status chips under the HP bar, each in its own colour.</summary>
    private static void StatusChips(TextBlock target, CardBattle.Side side)
    {
        target.Inlines.Clear();
        void Chip(string text, Color color)
        {
            if (target.Inlines.Count > 0) target.Inlines.Add(new System.Windows.Documents.Run("  "));
            target.Inlines.Add(new System.Windows.Documents.Run(text) { Foreground = new SolidColorBrush(color), FontWeight = FontWeights.SemiBold });
        }
        if (side.Block > 0) Chip($"Block {side.Block}", Color.FromRgb(0x25, 0x63, 0xEB));
        if (side.Thorns > 0) Chip($"Thorns {side.Thorns}", Color.FromRgb(0x16, 0xA3, 0x4A));
        if (side.NextEnergy > 0) Chip($"Next +{side.NextEnergy}⚡", Color.FromRgb(0xD9, 0x77, 0x06));
        if (side.Rage > 0) Chip($"Rage {side.Rage}", Fury);
        if (side.Regen > 0) Chip($"Regen {side.Regen}", Color.FromRgb(0x16, 0xA3, 0x4A));
        if (side.Burn > 0) Chip($"Burn {side.Burn}", Color.FromRgb(0xEA, 0x58, 0x0C));
        if (side.Poison > 0) Chip($"Poison {side.Poison}", Color.FromRgb(0x4D, 0x7C, 0x0F));
        if (side.Sleep > 0) Chip("Asleep", Color.FromRgb(0x63, 0x66, 0xF1));
        if (side.Fear > 0) Chip($"Fear {side.Fear}", Color.FromRgb(0x93, 0x33, 0xEA));
        if (side.Slow > 0) Chip("Slow", Color.FromRgb(0x02, 0x84, 0xC7));
        if (side.Weak > 0) Chip("Weak", Color.FromRgb(0x7C, 0x3A, 0xED));
    }
}

using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CydLan.Core;

namespace CydLan.Desktop;

/// <summary>Encodes, decodes, randomizes, and resets full CydLan appearance configurations.</summary>
public static class AppearanceCodec
{
    public const string Prefix = "CYDLAN1:";
    private const int MaxCodeLength = 8192;
    private const int MaxDecompressedBytes = 16384;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public sealed class LookData
    {
        public string? Theme { get; set; }
        public bool? ThemeDetails { get; set; }
        public string? ColorFinish { get; set; }
        public double? ColorFinishIntensity { get; set; }
        public string? Accent { get; set; }
        public string? EffectColor { get; set; }
        public string? OutBubble { get; set; }
        public string? InBubble { get; set; }
        public string? ChatSurface { get; set; }
        public string? ChatBg { get; set; }
        public string? BubbleStyle { get; set; }
        public string? BubbleShape { get; set; }
        public string? ChatDensity { get; set; }
        public string? CanvasGradient { get; set; }
        public double? CanvasGradientIntensity { get; set; }
        public bool? SeamlessComposer { get; set; }
        public double? BgBlur { get; set; }
        public string? CornerStyle { get; set; }
        public string? AvatarShape { get; set; }
        public bool? OnlineRing { get; set; }
        public string? UiFont { get; set; }
        public string? MsgFont { get; set; }
        public string? MsgSize { get; set; }
        public double? MsgSizePx { get; set; }
        public double? WindowOpacity { get; set; }
        public double? EffectSpeed { get; set; }
        public string? MsgAnim { get; set; }
        public string? ChromeTexture { get; set; }
        public double? ChromeTextureIntensity { get; set; }
        public string? TargetAura { get; set; }
        public double? TargetAuraIntensity { get; set; }
        public bool? AuraModule { get; set; }
        public bool? AuraPerson { get; set; }
        public bool? AuraButtons { get; set; }
        public string? TypingEffect { get; set; }
        public double? TypingIntensity { get; set; }
        public double? InkBloomIntensity { get; set; }
        public string? ClickEffect { get; set; }
        public double? ClickIntensity { get; set; }
        public bool? SendPulse { get; set; }
        public double? SendPulseIntensity { get; set; }
        public bool? Engine { get; set; }
        public double? EngineIntensity { get; set; }
        public bool? Petals { get; set; }
        public double? PetalIntensity { get; set; }
        public bool? Fireflies { get; set; }
        public double? FireflyIntensity { get; set; }
        public bool? Rain { get; set; }
        public double? RainIntensity { get; set; }
        public bool? Snow { get; set; }
        public double? SnowIntensity { get; set; }
        public bool? Starfield { get; set; }
        public double? StarfieldIntensity { get; set; }
        public bool? Bubbles { get; set; }
        public double? BubblesIntensity { get; set; }
        public bool? Embers { get; set; }
        public double? EmbersIntensity { get; set; }
        public bool? Matrix { get; set; }
        public double? MatrixIntensity { get; set; }
        public bool? Constellation { get; set; }
        public double? ConstellationIntensity { get; set; }
        public bool? Bokeh { get; set; }
        public double? BokehIntensity { get; set; }
        public bool? WingPlumes { get; set; }
        public double? WingPlumesIntensity { get; set; }
        public bool? Repulsor { get; set; }
        public double? RepulsorIntensity { get; set; }
        public bool? AmbientSidebar { get; set; }
        public bool? HeaderAurora { get; set; }
        public double? HeaderAuroraIntensity { get; set; }
        public bool? TitleSparkles { get; set; }
        public double? TitleSparklesIntensity { get; set; }
        public bool? HeaderPulse { get; set; }
        public double? HeaderPulseIntensity { get; set; }
        public bool? TitleShimmer { get; set; }
        public double? TitleShimmerIntensity { get; set; }
        public bool? TitleComet { get; set; }
        public double? TitleCometIntensity { get; set; }
        public bool? TitleEqualizer { get; set; }
        public double? TitleEqualizerIntensity { get; set; }
        public bool? TitleLaser { get; set; }
        public double? TitleLaserIntensity { get; set; }
        public bool? TitleAurora { get; set; }
        public double? TitleAuroraIntensity { get; set; }
        public bool? TitleWave { get; set; }
        public double? TitleWaveIntensity { get; set; }
        public bool? TitleWings { get; set; }
        public double? TitleWingsIntensity { get; set; }
        public bool? TitleReactor { get; set; }
        public double? TitleReactorIntensity { get; set; }
        public bool? HeaderPrism { get; set; }
        public double? HeaderPrismIntensity { get; set; }
        public bool? HeaderRipples { get; set; }
        public double? HeaderRipplesIntensity { get; set; }
        public bool? HeaderCircuit { get; set; }
        public double? HeaderCircuitIntensity { get; set; }
        public bool? HeaderStarlight { get; set; }
        public double? HeaderStarlightIntensity { get; set; }
        public bool? HeaderNebula { get; set; }
        public double? HeaderNebulaIntensity { get; set; }
        public bool? HeaderSilk { get; set; }
        public double? HeaderSilkIntensity { get; set; }
        public bool? HeaderPlasma { get; set; }
        public double? HeaderPlasmaIntensity { get; set; }
        public bool? HeaderBurst { get; set; }
        public double? HeaderBurstIntensity { get; set; }
        public bool? HeaderArc { get; set; }
        public double? HeaderArcIntensity { get; set; }
        public bool? RailBeacon { get; set; }
        public double? RailBeaconIntensity { get; set; }
        public bool? RailStream { get; set; }
        public double? RailStreamIntensity { get; set; }
        public bool? RailStardust { get; set; }
        public double? RailStardustIntensity { get; set; }
        public bool? RailScan { get; set; }
        public double? RailScanIntensity { get; set; }
        public bool? RailAurora { get; set; }
        public double? RailAuroraIntensity { get; set; }
        public bool? RailPulse { get; set; }
        public double? RailPulseIntensity { get; set; }
        public bool? RailPlasma { get; set; }
        public double? RailPlasmaIntensity { get; set; }
        public bool? RailCascade { get; set; }
        public double? RailCascadeIntensity { get; set; }
        public bool? RailWings { get; set; }
        public double? RailWingsIntensity { get; set; }
        public bool? RailArc { get; set; }
        public double? RailArcIntensity { get; set; }
        public bool? SidebarMotes { get; set; }
        public double? SidebarMotesIntensity { get; set; }
        public bool? SidebarGlow { get; set; }
        public double? SidebarGlowIntensity { get; set; }
        public bool? SidebarPrism { get; set; }
        public double? SidebarPrismIntensity { get; set; }
        public bool? SidebarSweep { get; set; }
        public double? SidebarSweepIntensity { get; set; }
        public bool? SidebarAurora { get; set; }
        public double? SidebarAuroraIntensity { get; set; }
        public bool? SidebarNebula { get; set; }
        public double? SidebarNebulaIntensity { get; set; }
        public bool? SidebarWaves { get; set; }
        public double? SidebarWavesIntensity { get; set; }
        public bool? SidebarRipples { get; set; }
        public double? SidebarRipplesIntensity { get; set; }
        public bool? SidebarCircuit { get; set; }
        public double? SidebarCircuitIntensity { get; set; }
        public bool? SidebarWings { get; set; }
        public double? SidebarWingsIntensity { get; set; }
        public bool? SidebarHud { get; set; }
        public double? SidebarHudIntensity { get; set; }
        public bool? ButtonGlow { get; set; }
        public double? ButtonGlowIntensity { get; set; }
        public bool? ButtonSheen { get; set; }
        public double? ButtonSheenIntensity { get; set; }
        public bool? ButtonBounce { get; set; }
        public double? ButtonBounceIntensity { get; set; }
        public bool? ButtonRing { get; set; }
        public double? ButtonRingIntensity { get; set; }
        public bool? ButtonPulse { get; set; }
        public double? ButtonPulseIntensity { get; set; }
        public bool? ButtonLift { get; set; }
        public double? ButtonLiftIntensity { get; set; }
        public bool? ButtonSpark { get; set; }
        public double? ButtonSparkIntensity { get; set; }
    }

    public static LookData Capture(AppSettings s) => new()
    {
        Theme = ThemeManager.Normalize(s.Theme),
        ThemeDetails = s.ThemeDetailsEnabled ? true : null,
        ColorFinish = NonDefault(Desktop.ColorFinish.Normalize(s.ColorFinish), Desktop.ColorFinish.Off),
        ColorFinishIntensity = Desktop.ColorFinish.Normalize(s.ColorFinish) != Desktop.ColorFinish.Off ? Round2(s.ColorFinishIntensity) : null,
        Accent = NormalizeHexOrNull(s.CustomAccentColor),
        EffectColor = NormalizeHexOrNull(s.CustomEffectColor),
        OutBubble = NormalizeHexOrNull(s.CustomOutgoingBubbleColor),
        InBubble = NormalizeHexOrNull(s.CustomIncomingBubbleColor),
        ChatSurface = NonDefault(AppearanceOptions.NormalizeChatSurfaceMode(s.ChatSurfaceMode), AppearanceOptions.ChatSurfaceSeparate),
        ChatBg = NormalizeHexOrNull(s.CustomChatBackgroundColor),
        BubbleStyle = NonDefault(AppearanceOptions.NormalizeBubbleStyle(s.BubbleStyle), AppearanceOptions.BubbleSoft),
        BubbleShape = NonDefault(AppearanceOptions.NormalizeBubbleShape(s.BubbleShape), AppearanceOptions.Default),
        ChatDensity = NonDefault(AppearanceOptions.NormalizeChatDensity(s.ChatDensity), AppearanceOptions.DensityCozy),
        CanvasGradient = NonDefault(ChatCanvasOverlay.Normalize(s.ChatCanvasGradient), ChatCanvasOverlay.Off),
        CanvasGradientIntensity = ChatCanvasOverlay.Normalize(s.ChatCanvasGradient) != ChatCanvasOverlay.Off ? Round2(s.ChatCanvasGradientIntensity) : null,
        SeamlessComposer = s.SeamlessComposerEnabled ? true : null,
        BgBlur = s.ChatBackgroundBlur > .1 ? Math.Round(Math.Clamp(s.ChatBackgroundBlur, 0, 24), 1) : null,
        CornerStyle = NonDefault(AppearanceOptions.NormalizeCornerStyle(s.CornerStyle), AppearanceOptions.Default),
        AvatarShape = NonDefault(AppearanceOptions.NormalizeAvatarShape(s.AvatarShape), AppearanceOptions.Default),
        OnlineRing = s.OnlineRingEnabled ? true : null,
        UiFont = string.IsNullOrWhiteSpace(s.UiFont) ? null : s.UiFont.Trim(),
        MsgFont = s.MessageFont == "Segoe UI Variable Text" ? null : s.MessageFont,
        MsgSize = s.MessageFontSize == "medium" ? null : s.MessageFontSize,
        MsgSizePx = s.MessageFontSizePx is { } px ? Math.Clamp(Math.Round(px), 11, 22) : null,
        WindowOpacity = Math.Abs(s.WindowOpacity - 1.0) > .005 ? Math.Round(Math.Clamp(s.WindowOpacity, .80, 1.0), 2) : null,
        EffectSpeed = Math.Abs(s.EffectSpeed - 1.0) > .01 ? Math.Round(Math.Clamp(s.EffectSpeed, .4, 2.0), 2) : null,
        MsgAnim = NonDefault(MessageMotion.Normalize(s.MessageAnimation), MessageMotion.Off),
        ChromeTexture = NonDefault(ChromeTexture.Normalize(s.ChromeTexture), ChromeTexture.Off),
        ChromeTextureIntensity = ChromeTexture.Normalize(s.ChromeTexture) != ChromeTexture.Off ? Round2(s.ChromeTextureIntensity) : null,
        TargetAura = NonDefault(TargetAura.Normalize(s.TargetAura), TargetAura.Off),
        TargetAuraIntensity = TargetAura.Normalize(s.TargetAura) != TargetAura.Off ? Round2(s.TargetAuraIntensity) : null,
        AuraModule = TargetAura.Normalize(s.TargetAura) != TargetAura.Off ? s.AuraSelectedModuleEnabled : null,
        AuraPerson = TargetAura.Normalize(s.TargetAura) != TargetAura.Off ? s.AuraSelectedPersonEnabled : null,
        AuraButtons = TargetAura.Normalize(s.TargetAura) != TargetAura.Off ? s.AuraHighlightButtonsEnabled : null,
        TypingEffect = NonDefault(ChatEffects.Normalize(s.TypingEffect, typing: true), "off"),
        TypingIntensity = s.TypingEffect != "off" && s.TypingIntensity is { } ti ? Round2(ti) : null,
        InkBloomIntensity = s.TypingEffect == "ink" ? Round2(s.InkBloomIntensity) : null,
        ClickEffect = NonDefault(ChatEffects.Normalize(s.ClickEffect), "off"),
        ClickIntensity = s.ClickEffect != "off" && s.ClickIntensity is { } ci ? Round2(ci) : null,
        SendPulse = s.SendPulseEnabled ? true : null,
        SendPulseIntensity = s.SendPulseEnabled ? Round2(s.SendPulseIntensity) : null,
        Engine = s.SteampunkEngineEnabled ? true : null,
        EngineIntensity = s.SteampunkEngineEnabled ? Round2(s.EngineIntensity) : null,
        Petals = s.SakuraPetalsEnabled ? true : null,
        PetalIntensity = s.SakuraPetalsEnabled ? Round2(s.PetalIntensity) : null,
        Fireflies = s.FirefliesEnabled ? true : null,
        FireflyIntensity = s.FirefliesEnabled ? Round2(s.FireflyIntensity) : null,
        Rain = s.RainEnabled ? true : null,
        RainIntensity = s.RainEnabled ? Round2(s.RainIntensity) : null,
        Snow = s.SnowEnabled ? true : null,
        SnowIntensity = s.SnowEnabled ? Round2(s.SnowIntensity) : null,
        Starfield = s.StarfieldEnabled ? true : null,
        StarfieldIntensity = s.StarfieldEnabled ? Round2(s.StarfieldIntensity) : null,
        Bubbles = s.BubblesEnabled ? true : null,
        BubblesIntensity = s.BubblesEnabled ? Round2(s.BubblesIntensity) : null,
        Embers = s.EmbersEnabled ? true : null,
        EmbersIntensity = s.EmbersEnabled ? Round2(s.EmbersIntensity) : null,
        Matrix = s.MatrixEnabled ? true : null,
        MatrixIntensity = s.MatrixEnabled ? Round2(s.MatrixIntensity) : null,
        Constellation = s.ConstellationEnabled ? true : null,
        ConstellationIntensity = s.ConstellationEnabled ? Round2(s.ConstellationIntensity) : null,
        Bokeh = s.BokehEnabled ? true : null,
        BokehIntensity = s.BokehEnabled ? Round2(s.BokehIntensity) : null,
        WingPlumes = s.WingPlumesEnabled ? true : null,
        WingPlumesIntensity = s.WingPlumesEnabled ? Round2(s.WingPlumesIntensity) : null,
        Repulsor = s.RepulsorEnabled ? true : null,
        RepulsorIntensity = s.RepulsorEnabled ? Round2(s.RepulsorIntensity) : null,
        AmbientSidebar = s.AmbientOnSidebarEnabled ? true : null,
        HeaderAurora = s.HeaderAuroraEnabled ? true : null,
        HeaderAuroraIntensity = s.HeaderAuroraEnabled ? Round2(s.HeaderAuroraIntensity) : null,
        TitleSparkles = s.TitleSparklesEnabled ? true : null,
        TitleSparklesIntensity = s.TitleSparklesEnabled ? Round2(s.TitleSparklesIntensity) : null,
        HeaderPulse = s.HeaderPulseEnabled ? true : null,
        HeaderPulseIntensity = s.HeaderPulseEnabled ? Round2(s.HeaderPulseIntensity) : null,
        TitleShimmer = s.TitleShimmerEnabled ? true : null,
        TitleShimmerIntensity = s.TitleShimmerEnabled ? Round2(s.TitleShimmerIntensity) : null,
        TitleComet = s.TitleCometEnabled ? true : null,
        TitleCometIntensity = s.TitleCometEnabled ? Round2(s.TitleCometIntensity) : null,
        TitleEqualizer = s.TitleEqualizerEnabled ? true : null,
        TitleEqualizerIntensity = s.TitleEqualizerEnabled ? Round2(s.TitleEqualizerIntensity) : null,
        TitleLaser = s.TitleLaserEnabled ? true : null,
        TitleLaserIntensity = s.TitleLaserEnabled ? Round2(s.TitleLaserIntensity) : null,
        TitleAurora = s.TitleAuroraEnabled ? true : null,
        TitleAuroraIntensity = s.TitleAuroraEnabled ? Round2(s.TitleAuroraIntensity) : null,
        TitleWave = s.TitleWaveEnabled ? true : null,
        TitleWaveIntensity = s.TitleWaveEnabled ? Round2(s.TitleWaveIntensity) : null,
        TitleWings = s.TitleWingsEnabled ? true : null,
        TitleWingsIntensity = s.TitleWingsEnabled ? Round2(s.TitleWingsIntensity) : null,
        TitleReactor = s.TitleReactorEnabled ? true : null,
        TitleReactorIntensity = s.TitleReactorEnabled ? Round2(s.TitleReactorIntensity) : null,
        HeaderPrism = s.HeaderPrismEnabled ? true : null,
        HeaderPrismIntensity = s.HeaderPrismEnabled ? Round2(s.HeaderPrismIntensity) : null,
        HeaderRipples = s.HeaderRipplesEnabled ? true : null,
        HeaderRipplesIntensity = s.HeaderRipplesEnabled ? Round2(s.HeaderRipplesIntensity) : null,
        HeaderCircuit = s.HeaderCircuitEnabled ? true : null,
        HeaderCircuitIntensity = s.HeaderCircuitEnabled ? Round2(s.HeaderCircuitIntensity) : null,
        HeaderStarlight = s.HeaderStarlightEnabled ? true : null,
        HeaderStarlightIntensity = s.HeaderStarlightEnabled ? Round2(s.HeaderStarlightIntensity) : null,
        HeaderNebula = s.HeaderNebulaEnabled ? true : null,
        HeaderNebulaIntensity = s.HeaderNebulaEnabled ? Round2(s.HeaderNebulaIntensity) : null,
        HeaderSilk = s.HeaderSilkEnabled ? true : null,
        HeaderSilkIntensity = s.HeaderSilkEnabled ? Round2(s.HeaderSilkIntensity) : null,
        HeaderPlasma = s.HeaderPlasmaEnabled ? true : null,
        HeaderPlasmaIntensity = s.HeaderPlasmaEnabled ? Round2(s.HeaderPlasmaIntensity) : null,
        HeaderBurst = s.HeaderBurstEnabled ? true : null,
        HeaderBurstIntensity = s.HeaderBurstEnabled ? Round2(s.HeaderBurstIntensity) : null,
        HeaderArc = s.HeaderArcEnabled ? true : null,
        HeaderArcIntensity = s.HeaderArcEnabled ? Round2(s.HeaderArcIntensity) : null,
        RailBeacon = s.RailBeaconEnabled ? true : null,
        RailBeaconIntensity = s.RailBeaconEnabled ? Round2(s.RailBeaconIntensity) : null,
        RailStream = s.RailStreamEnabled ? true : null,
        RailStreamIntensity = s.RailStreamEnabled ? Round2(s.RailStreamIntensity) : null,
        RailStardust = s.RailStardustEnabled ? true : null,
        RailStardustIntensity = s.RailStardustEnabled ? Round2(s.RailStardustIntensity) : null,
        RailScan = s.RailScanEnabled ? true : null,
        RailScanIntensity = s.RailScanEnabled ? Round2(s.RailScanIntensity) : null,
        RailAurora = s.RailAuroraEnabled ? true : null,
        RailAuroraIntensity = s.RailAuroraEnabled ? Round2(s.RailAuroraIntensity) : null,
        RailPulse = s.RailPulseEnabled ? true : null,
        RailPulseIntensity = s.RailPulseEnabled ? Round2(s.RailPulseIntensity) : null,
        RailPlasma = s.RailPlasmaEnabled ? true : null,
        RailPlasmaIntensity = s.RailPlasmaEnabled ? Round2(s.RailPlasmaIntensity) : null,
        RailCascade = s.RailCascadeEnabled ? true : null,
        RailCascadeIntensity = s.RailCascadeEnabled ? Round2(s.RailCascadeIntensity) : null,
        RailWings = s.RailWingsEnabled ? true : null,
        RailWingsIntensity = s.RailWingsEnabled ? Round2(s.RailWingsIntensity) : null,
        RailArc = s.RailArcEnabled ? true : null,
        RailArcIntensity = s.RailArcEnabled ? Round2(s.RailArcIntensity) : null,
        SidebarMotes = s.SidebarMotesEnabled ? true : null,
        SidebarMotesIntensity = s.SidebarMotesEnabled ? Round2(s.SidebarMotesIntensity) : null,
        SidebarGlow = s.SidebarGlowEnabled ? true : null,
        SidebarGlowIntensity = s.SidebarGlowEnabled ? Round2(s.SidebarGlowIntensity) : null,
        SidebarPrism = s.SidebarPrismEnabled ? true : null,
        SidebarPrismIntensity = s.SidebarPrismEnabled ? Round2(s.SidebarPrismIntensity) : null,
        SidebarSweep = s.SidebarSweepEnabled ? true : null,
        SidebarSweepIntensity = s.SidebarSweepEnabled ? Round2(s.SidebarSweepIntensity) : null,
        SidebarAurora = s.SidebarAuroraEnabled ? true : null,
        SidebarAuroraIntensity = s.SidebarAuroraEnabled ? Round2(s.SidebarAuroraIntensity) : null,
        SidebarNebula = s.SidebarNebulaEnabled ? true : null,
        SidebarNebulaIntensity = s.SidebarNebulaEnabled ? Round2(s.SidebarNebulaIntensity) : null,
        SidebarWaves = s.SidebarWavesEnabled ? true : null,
        SidebarWavesIntensity = s.SidebarWavesEnabled ? Round2(s.SidebarWavesIntensity) : null,
        SidebarRipples = s.SidebarRipplesEnabled ? true : null,
        SidebarRipplesIntensity = s.SidebarRipplesEnabled ? Round2(s.SidebarRipplesIntensity) : null,
        SidebarCircuit = s.SidebarCircuitEnabled ? true : null,
        SidebarCircuitIntensity = s.SidebarCircuitEnabled ? Round2(s.SidebarCircuitIntensity) : null,
        SidebarWings = s.SidebarWingsEnabled ? true : null,
        SidebarWingsIntensity = s.SidebarWingsEnabled ? Round2(s.SidebarWingsIntensity) : null,
        SidebarHud = s.SidebarHudEnabled ? true : null,
        SidebarHudIntensity = s.SidebarHudEnabled ? Round2(s.SidebarHudIntensity) : null,
        ButtonGlow = s.ButtonGlowEnabled ? true : null,
        ButtonGlowIntensity = s.ButtonGlowEnabled ? Round2(s.ButtonGlowIntensity) : null,
        ButtonSheen = s.ButtonSheenEnabled ? true : null,
        ButtonSheenIntensity = s.ButtonSheenEnabled ? Round2(s.ButtonSheenIntensity) : null,
        ButtonBounce = s.ButtonBounceEnabled ? true : null,
        ButtonBounceIntensity = s.ButtonBounceEnabled ? Round2(s.ButtonBounceIntensity) : null,
        ButtonRing = s.ButtonRingEnabled ? true : null,
        ButtonRingIntensity = s.ButtonRingEnabled ? Round2(s.ButtonRingIntensity) : null,
        ButtonPulse = s.ButtonPulseEnabled ? true : null,
        ButtonPulseIntensity = s.ButtonPulseEnabled ? Round2(s.ButtonPulseIntensity) : null,
        ButtonLift = s.ButtonLiftEnabled ? true : null,
        ButtonLiftIntensity = s.ButtonLiftEnabled ? Round2(s.ButtonLiftIntensity) : null,
        ButtonSpark = s.ButtonSparkEnabled ? true : null,
        ButtonSparkIntensity = s.ButtonSparkEnabled ? Round2(s.ButtonSparkIntensity) : null
    };

    public static string Encode(LookData data)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(data, JsonOptions);
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
            deflate.Write(json, 0, json.Length);
        string b64 = Convert.ToBase64String(output.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return Prefix + b64;
    }

    public static string Encode(AppSettings settings) => Encode(Capture(settings));

    public static bool TryDecode(string? code, out LookData data)
    {
        data = new LookData();
        if (string.IsNullOrWhiteSpace(code)) return false;
        string trimmed = code.Trim();
        if (!trimmed.StartsWith(Prefix, StringComparison.Ordinal) || trimmed.Length > MaxCodeLength) return false;
        string payload = trimmed[Prefix.Length..].Replace('-', '+').Replace('_', '/');
        int mod = payload.Length % 4;
        if (mod == 1) return false;
        if (mod > 0) payload += new string('=', 4 - mod);
        try
        {
            byte[] compressed = Convert.FromBase64String(payload);
            using var input = new MemoryStream(compressed);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            byte[] buffer = new byte[1024];
            int read;
            while ((read = deflate.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (output.Length + read > MaxDecompressedBytes) return false;
                output.Write(buffer, 0, read);
            }
            var parsed = JsonSerializer.Deserialize<LookData>(output.ToArray(), JsonOptions);
            if (parsed is null) return false;
            data = parsed;
            return true;
        }
        catch (Exception e) when (e is FormatException or InvalidDataException or JsonException)
        {
            return false;
        }
    }

    public static void Apply(LookData d, AppSettings s)
    {
        ResetDefaults(s);
        s.Theme = ThemeManager.Normalize(d.Theme);
        s.ThemeDetailsEnabled = d.ThemeDetails == true;
        s.ColorFinish = Desktop.ColorFinish.Normalize(d.ColorFinish);
        s.ColorFinishIntensity = ClampUnit(d.ColorFinishIntensity, .65);
        s.CustomAccentColor = AppearanceOptions.NormalizeHex(d.Accent);
        s.CustomEffectColor = AppearanceOptions.NormalizeHex(d.EffectColor);
        s.CustomOutgoingBubbleColor = AppearanceOptions.NormalizeHex(d.OutBubble);
        s.CustomIncomingBubbleColor = AppearanceOptions.NormalizeHex(d.InBubble);
        s.ChatSurfaceMode = AppearanceOptions.NormalizeChatSurfaceMode(d.ChatSurface);
        s.CustomChatBackgroundColor = AppearanceOptions.NormalizeHex(d.ChatBg);
        s.BubbleStyle = AppearanceOptions.NormalizeBubbleStyle(d.BubbleStyle);
        s.BubbleShape = AppearanceOptions.NormalizeBubbleShape(d.BubbleShape);
        s.ChatDensity = AppearanceOptions.NormalizeChatDensity(d.ChatDensity);
        s.ChatCanvasGradient = ChatCanvasOverlay.Normalize(d.CanvasGradient);
        s.ChatCanvasGradientIntensity = ClampUnit(d.CanvasGradientIntensity, .55);
        s.SeamlessComposerEnabled = d.SeamlessComposer == true;
        s.ChatBackgroundBlur = Math.Clamp(d.BgBlur ?? 0, 0, 24);
        s.CornerStyle = AppearanceOptions.NormalizeCornerStyle(d.CornerStyle);
        s.AvatarShape = AppearanceOptions.NormalizeAvatarShape(d.AvatarShape);
        s.OnlineRingEnabled = d.OnlineRing == true;
        s.UiFont = (d.UiFont ?? "").Trim();
        s.MessageFont = string.IsNullOrWhiteSpace(d.MsgFont) ? "Segoe UI Variable Text" : d.MsgFont.Trim();
        s.MessageFontSize = d.MsgSize is "small" or "large" ? d.MsgSize : "medium";
        s.MessageFontSizePx = d.MsgSizePx is { } px && double.IsFinite(px) ? Math.Clamp(Math.Round(px), 11, 22) : null;
        s.WindowOpacity = double.IsFinite(d.WindowOpacity ?? 1) ? Math.Clamp(d.WindowOpacity ?? 1.0, .80, 1.0) : 1.0;
        s.EffectSpeed = double.IsFinite(d.EffectSpeed ?? 1) ? Math.Clamp(d.EffectSpeed ?? 1.0, .4, 2.0) : 1.0;
        s.MessageAnimation = MessageMotion.Normalize(d.MsgAnim);
        s.ChromeTexture = ChromeTexture.Normalize(d.ChromeTexture);
        s.ChromeTextureIntensity = ClampUnit(d.ChromeTextureIntensity, .65);
        s.TargetAura = TargetAura.Normalize(d.TargetAura);
        s.TargetAuraIntensity = ClampUnit(d.TargetAuraIntensity, .70);
        s.AuraSelectedModuleEnabled = d.AuraModule ?? true;
        s.AuraSelectedPersonEnabled = d.AuraPerson ?? true;
        s.AuraHighlightButtonsEnabled = d.AuraButtons ?? true;
        s.TypingEffect = ChatEffects.Normalize(d.TypingEffect, typing: true);
        s.TypingIntensity = d.TypingIntensity is { } ti ? ClampUnit(ti, .75) : null;
        s.InkBloomIntensity = ClampUnit(d.InkBloomIntensity, .65);
        s.ClickEffect = ChatEffects.Normalize(d.ClickEffect);
        s.ClickIntensity = d.ClickIntensity is { } ci ? ClampUnit(ci, .75) : null;
        s.SendPulseEnabled = d.SendPulse == true;
        s.SendPulseIntensity = ClampUnit(d.SendPulseIntensity, .65);
        s.SteampunkEngineEnabled = d.Engine == true;
        s.EngineIntensity = ClampUnit(d.EngineIntensity, .85);
        s.SakuraPetalsEnabled = d.Petals == true;
        s.PetalIntensity = ClampUnit(d.PetalIntensity, .75);
        s.FirefliesEnabled = d.Fireflies == true;
        s.FireflyIntensity = ClampUnit(d.FireflyIntensity, .65);
        s.RainEnabled = d.Rain == true;
        s.RainIntensity = ClampUnit(d.RainIntensity, .65);
        s.SnowEnabled = d.Snow == true;
        s.SnowIntensity = ClampUnit(d.SnowIntensity, .65);
        s.StarfieldEnabled = d.Starfield == true;
        s.StarfieldIntensity = ClampUnit(d.StarfieldIntensity, .65);
        s.BubblesEnabled = d.Bubbles == true;
        s.BubblesIntensity = ClampUnit(d.BubblesIntensity, .65);
        s.EmbersEnabled = d.Embers == true;
        s.EmbersIntensity = ClampUnit(d.EmbersIntensity, .65);
        s.MatrixEnabled = d.Matrix == true;
        s.MatrixIntensity = ClampUnit(d.MatrixIntensity, .65);
        s.ConstellationEnabled = d.Constellation == true;
        s.ConstellationIntensity = ClampUnit(d.ConstellationIntensity, .65);
        s.BokehEnabled = d.Bokeh == true;
        s.BokehIntensity = ClampUnit(d.BokehIntensity, .65);
        s.WingPlumesEnabled = d.WingPlumes == true;
        s.WingPlumesIntensity = ClampUnit(d.WingPlumesIntensity, .65);
        s.RepulsorEnabled = d.Repulsor == true;
        s.RepulsorIntensity = ClampUnit(d.RepulsorIntensity, .65);
        s.AmbientOnSidebarEnabled = d.AmbientSidebar == true;
        s.HeaderAuroraEnabled = d.HeaderAurora == true;
        s.HeaderAuroraIntensity = ClampUnit(d.HeaderAuroraIntensity, .65);
        s.TitleSparklesEnabled = d.TitleSparkles == true;
        s.TitleSparklesIntensity = ClampUnit(d.TitleSparklesIntensity, .65);
        s.HeaderPulseEnabled = d.HeaderPulse == true;
        s.HeaderPulseIntensity = ClampUnit(d.HeaderPulseIntensity, .65);
        s.TitleShimmerEnabled = d.TitleShimmer == true;
        s.TitleShimmerIntensity = ClampUnit(d.TitleShimmerIntensity, .65);
        s.TitleCometEnabled = d.TitleComet == true;
        s.TitleCometIntensity = ClampUnit(d.TitleCometIntensity, .65);
        s.TitleEqualizerEnabled = d.TitleEqualizer == true;
        s.TitleEqualizerIntensity = ClampUnit(d.TitleEqualizerIntensity, .65);
        s.TitleLaserEnabled = d.TitleLaser == true;
        s.TitleLaserIntensity = ClampUnit(d.TitleLaserIntensity, .65);
        s.TitleAuroraEnabled = d.TitleAurora == true;
        s.TitleAuroraIntensity = ClampUnit(d.TitleAuroraIntensity, .65);
        s.TitleWaveEnabled = d.TitleWave == true;
        s.TitleWaveIntensity = ClampUnit(d.TitleWaveIntensity, .65);
        s.TitleWingsEnabled = d.TitleWings == true;
        s.TitleWingsIntensity = ClampUnit(d.TitleWingsIntensity, .65);
        s.TitleReactorEnabled = d.TitleReactor == true;
        s.TitleReactorIntensity = ClampUnit(d.TitleReactorIntensity, .65);
        s.HeaderPrismEnabled = d.HeaderPrism == true;
        s.HeaderPrismIntensity = ClampUnit(d.HeaderPrismIntensity, .65);
        s.HeaderRipplesEnabled = d.HeaderRipples == true;
        s.HeaderRipplesIntensity = ClampUnit(d.HeaderRipplesIntensity, .65);
        s.HeaderCircuitEnabled = d.HeaderCircuit == true;
        s.HeaderCircuitIntensity = ClampUnit(d.HeaderCircuitIntensity, .65);
        s.HeaderStarlightEnabled = d.HeaderStarlight == true;
        s.HeaderStarlightIntensity = ClampUnit(d.HeaderStarlightIntensity, .65);
        s.HeaderNebulaEnabled = d.HeaderNebula == true;
        s.HeaderNebulaIntensity = ClampUnit(d.HeaderNebulaIntensity, .65);
        s.HeaderSilkEnabled = d.HeaderSilk == true;
        s.HeaderSilkIntensity = ClampUnit(d.HeaderSilkIntensity, .65);
        s.HeaderPlasmaEnabled = d.HeaderPlasma == true;
        s.HeaderPlasmaIntensity = ClampUnit(d.HeaderPlasmaIntensity, .65);
        s.HeaderBurstEnabled = d.HeaderBurst == true;
        s.HeaderBurstIntensity = ClampUnit(d.HeaderBurstIntensity, .65);
        s.HeaderArcEnabled = d.HeaderArc == true;
        s.HeaderArcIntensity = ClampUnit(d.HeaderArcIntensity, .65);
        s.RailBeaconEnabled = d.RailBeacon == true;
        s.RailBeaconIntensity = ClampUnit(d.RailBeaconIntensity, .65);
        s.RailStreamEnabled = d.RailStream == true;
        s.RailStreamIntensity = ClampUnit(d.RailStreamIntensity, .65);
        s.RailStardustEnabled = d.RailStardust == true;
        s.RailStardustIntensity = ClampUnit(d.RailStardustIntensity, .65);
        s.RailScanEnabled = d.RailScan == true;
        s.RailScanIntensity = ClampUnit(d.RailScanIntensity, .65);
        s.RailAuroraEnabled = d.RailAurora == true;
        s.RailAuroraIntensity = ClampUnit(d.RailAuroraIntensity, .65);
        s.RailPulseEnabled = d.RailPulse == true;
        s.RailPulseIntensity = ClampUnit(d.RailPulseIntensity, .65);
        s.RailPlasmaEnabled = d.RailPlasma == true;
        s.RailPlasmaIntensity = ClampUnit(d.RailPlasmaIntensity, .65);
        s.RailCascadeEnabled = d.RailCascade == true;
        s.RailCascadeIntensity = ClampUnit(d.RailCascadeIntensity, .65);
        s.RailWingsEnabled = d.RailWings == true;
        s.RailWingsIntensity = ClampUnit(d.RailWingsIntensity, .65);
        s.RailArcEnabled = d.RailArc == true;
        s.RailArcIntensity = ClampUnit(d.RailArcIntensity, .65);
        s.SidebarMotesEnabled = d.SidebarMotes == true;
        s.SidebarMotesIntensity = ClampUnit(d.SidebarMotesIntensity, .65);
        s.SidebarGlowEnabled = d.SidebarGlow == true;
        s.SidebarGlowIntensity = ClampUnit(d.SidebarGlowIntensity, .65);
        s.SidebarPrismEnabled = d.SidebarPrism == true;
        s.SidebarPrismIntensity = ClampUnit(d.SidebarPrismIntensity, .65);
        s.SidebarSweepEnabled = d.SidebarSweep == true;
        s.SidebarSweepIntensity = ClampUnit(d.SidebarSweepIntensity, .65);
        s.SidebarAuroraEnabled = d.SidebarAurora == true;
        s.SidebarAuroraIntensity = ClampUnit(d.SidebarAuroraIntensity, .65);
        s.SidebarNebulaEnabled = d.SidebarNebula == true;
        s.SidebarNebulaIntensity = ClampUnit(d.SidebarNebulaIntensity, .65);
        s.SidebarWavesEnabled = d.SidebarWaves == true;
        s.SidebarWavesIntensity = ClampUnit(d.SidebarWavesIntensity, .65);
        s.SidebarRipplesEnabled = d.SidebarRipples == true;
        s.SidebarRipplesIntensity = ClampUnit(d.SidebarRipplesIntensity, .65);
        s.SidebarCircuitEnabled = d.SidebarCircuit == true;
        s.SidebarCircuitIntensity = ClampUnit(d.SidebarCircuitIntensity, .65);
        s.SidebarWingsEnabled = d.SidebarWings == true;
        s.SidebarWingsIntensity = ClampUnit(d.SidebarWingsIntensity, .65);
        s.SidebarHudEnabled = d.SidebarHud == true;
        s.SidebarHudIntensity = ClampUnit(d.SidebarHudIntensity, .65);
        s.ButtonGlowEnabled = d.ButtonGlow == true;
        s.ButtonGlowIntensity = ClampUnit(d.ButtonGlowIntensity, .65);
        s.ButtonSheenEnabled = d.ButtonSheen == true;
        s.ButtonSheenIntensity = ClampUnit(d.ButtonSheenIntensity, .65);
        s.ButtonBounceEnabled = d.ButtonBounce == true;
        s.ButtonBounceIntensity = ClampUnit(d.ButtonBounceIntensity, .65);
        s.ButtonRingEnabled = d.ButtonRing == true;
        s.ButtonRingIntensity = ClampUnit(d.ButtonRingIntensity, .65);
        s.ButtonPulseEnabled = d.ButtonPulse == true;
        s.ButtonPulseIntensity = ClampUnit(d.ButtonPulseIntensity, .65);
        s.ButtonLiftEnabled = d.ButtonLift == true;
        s.ButtonLiftIntensity = ClampUnit(d.ButtonLiftIntensity, .65);
        s.ButtonSparkEnabled = d.ButtonSpark == true;
        s.ButtonSparkIntensity = ClampUnit(d.ButtonSparkIntensity, .65);
    }

    public static void ResetDefaults(AppSettings s)
    {
        s.ThemeDetailsEnabled = false;
        s.ColorFinish = Desktop.ColorFinish.Off;
        s.ColorFinishIntensity = .65;
        s.CustomAccentColor = "";
        s.CustomEffectColor = "";
        s.CustomOutgoingBubbleColor = "";
        s.CustomIncomingBubbleColor = "";
        s.ChatSurfaceMode = AppearanceOptions.ChatSurfaceSeparate;
        s.CustomChatBackgroundColor = "";
        s.BubbleStyle = AppearanceOptions.BubbleSoft;
        s.BubbleShape = AppearanceOptions.Default;
        s.ChatDensity = AppearanceOptions.DensityCozy;
        s.MessageFont = "Segoe UI Variable Text";
        s.MessageFontSize = "medium";
        s.MessageFontSizePx = null;
        s.ChatCanvasGradient = ChatCanvasOverlay.Off;
        s.ChatCanvasGradientIntensity = .55;
        s.SeamlessComposerEnabled = false;
        s.ChatBackgroundBlur = 0;
        s.CornerStyle = AppearanceOptions.Default;
        s.AvatarShape = AppearanceOptions.Default;
        s.OnlineRingEnabled = false;
        s.UiFont = "";
        s.WindowOpacity = 1.0;
        s.EffectSpeed = 1.0;
        s.MessageAnimation = MessageMotion.Off;
        s.ChromeTexture = ChromeTexture.Off;
        s.ChromeTextureIntensity = .65;
        s.TargetAura = TargetAura.Off;
        s.TargetAuraIntensity = .70;
        s.AuraSelectedModuleEnabled = true;
        s.AuraSelectedPersonEnabled = true;
        s.AuraHighlightButtonsEnabled = true;
        s.TypingEffect = "off";
        s.TypingIntensity = null;
        s.ClickEffect = "off";
        s.ClickIntensity = null;
        s.InkBloomIntensity = .65;
        s.SendPulseEnabled = false;
        s.SendPulseIntensity = .65;
        s.SteampunkEngineEnabled = false;
        s.EngineIntensity = .85;
        s.SakuraPetalsEnabled = false;
        s.PetalIntensity = .75;
        s.FirefliesEnabled = false;
        s.FireflyIntensity = .65;
        s.RainEnabled = false;
        s.RainIntensity = .65;
        s.SnowEnabled = false;
        s.SnowIntensity = .65;
        s.StarfieldEnabled = false;
        s.StarfieldIntensity = .65;
        s.BubblesEnabled = false;
        s.BubblesIntensity = .65;
        s.EmbersEnabled = false;
        s.EmbersIntensity = .65;
        s.MatrixEnabled = false;
        s.MatrixIntensity = .65;
        s.ConstellationEnabled = false;
        s.ConstellationIntensity = .65;
        s.BokehEnabled = false;
        s.BokehIntensity = .65;
        s.WingPlumesEnabled = false;
        s.WingPlumesIntensity = .65;
        s.RepulsorEnabled = false;
        s.RepulsorIntensity = .65;
        s.AmbientOnSidebarEnabled = false;
        s.HeaderAuroraEnabled = false;
        s.HeaderAuroraIntensity = .65;
        s.TitleSparklesEnabled = false;
        s.TitleSparklesIntensity = .65;
        s.HeaderPulseEnabled = false;
        s.HeaderPulseIntensity = .65;
        s.TitleShimmerEnabled = false;
        s.TitleShimmerIntensity = .65;
        s.TitleCometEnabled = false;
        s.TitleCometIntensity = .65;
        s.TitleEqualizerEnabled = false;
        s.TitleEqualizerIntensity = .65;
        s.TitleLaserEnabled = false;
        s.TitleLaserIntensity = .65;
        s.TitleAuroraEnabled = false;
        s.TitleAuroraIntensity = .65;
        s.TitleWaveEnabled = false;
        s.TitleWaveIntensity = .65;
        s.TitleWingsEnabled = false;
        s.TitleWingsIntensity = .65;
        s.TitleReactorEnabled = false;
        s.TitleReactorIntensity = .65;
        s.HeaderPrismEnabled = false;
        s.HeaderPrismIntensity = .65;
        s.HeaderRipplesEnabled = false;
        s.HeaderRipplesIntensity = .65;
        s.HeaderCircuitEnabled = false;
        s.HeaderCircuitIntensity = .65;
        s.HeaderStarlightEnabled = false;
        s.HeaderStarlightIntensity = .65;
        s.HeaderNebulaEnabled = false;
        s.HeaderNebulaIntensity = .65;
        s.HeaderSilkEnabled = false;
        s.HeaderSilkIntensity = .65;
        s.HeaderPlasmaEnabled = false;
        s.HeaderPlasmaIntensity = .65;
        s.HeaderBurstEnabled = false;
        s.HeaderBurstIntensity = .65;
        s.HeaderArcEnabled = false;
        s.HeaderArcIntensity = .65;
        s.RailBeaconEnabled = false;
        s.RailBeaconIntensity = .65;
        s.RailStreamEnabled = false;
        s.RailStreamIntensity = .65;
        s.RailStardustEnabled = false;
        s.RailStardustIntensity = .65;
        s.RailScanEnabled = false;
        s.RailScanIntensity = .65;
        s.RailAuroraEnabled = false;
        s.RailAuroraIntensity = .65;
        s.RailPulseEnabled = false;
        s.RailPulseIntensity = .65;
        s.RailPlasmaEnabled = false;
        s.RailPlasmaIntensity = .65;
        s.RailCascadeEnabled = false;
        s.RailCascadeIntensity = .65;
        s.RailWingsEnabled = false;
        s.RailWingsIntensity = .65;
        s.RailArcEnabled = false;
        s.RailArcIntensity = .65;
        s.SidebarMotesEnabled = false;
        s.SidebarMotesIntensity = .65;
        s.SidebarGlowEnabled = false;
        s.SidebarGlowIntensity = .65;
        s.SidebarPrismEnabled = false;
        s.SidebarPrismIntensity = .65;
        s.SidebarSweepEnabled = false;
        s.SidebarSweepIntensity = .65;
        s.SidebarAuroraEnabled = false;
        s.SidebarAuroraIntensity = .65;
        s.SidebarNebulaEnabled = false;
        s.SidebarNebulaIntensity = .65;
        s.SidebarWavesEnabled = false;
        s.SidebarWavesIntensity = .65;
        s.SidebarRipplesEnabled = false;
        s.SidebarRipplesIntensity = .65;
        s.SidebarCircuitEnabled = false;
        s.SidebarCircuitIntensity = .65;
        s.SidebarWingsEnabled = false;
        s.SidebarWingsIntensity = .65;
        s.SidebarHudEnabled = false;
        s.SidebarHudIntensity = .65;
        s.ButtonGlowEnabled = false;
        s.ButtonGlowIntensity = .65;
        s.ButtonSheenEnabled = false;
        s.ButtonSheenIntensity = .65;
        s.ButtonBounceEnabled = false;
        s.ButtonBounceIntensity = .65;
        s.ButtonRingEnabled = false;
        s.ButtonRingIntensity = .65;
        s.ButtonPulseEnabled = false;
        s.ButtonPulseIntensity = .65;
        s.ButtonLiftEnabled = false;
        s.ButtonLiftIntensity = .65;
        s.ButtonSparkEnabled = false;
        s.ButtonSparkIntensity = .65;
    }

    public static LookData Randomize(Random? random = null)
    {
        var rng = random ?? Random.Shared;
        var themes = ThemeManager.Catalog.Where(t => !t.IsSystem).Select(t => t.Tag).ToArray();
        string theme = themes[rng.Next(themes.Length)];
        string[] textures = ChromeTexture.Catalog.Select(t => t.Value).ToArray();
        string[] finishes = Desktop.ColorFinish.Catalog.Select(f => f.Value).ToArray();
        string[] bubbleStyles = [AppearanceOptions.BubbleSoft, AppearanceOptions.BubbleSolid, AppearanceOptions.BubbleOutlined, AppearanceOptions.BubbleGlass, AppearanceOptions.BubbleGradient, AppearanceOptions.BubbleMinimal, AppearanceOptions.BubbleMecha, AppearanceOptions.BubbleReactor];
        string[] bubbleShapes = [AppearanceOptions.Default, AppearanceOptions.ShapeSubtle, AppearanceOptions.ShapeRounded, AppearanceOptions.ShapePill, AppearanceOptions.ShapeTail];
        string[] gradients = [ChatCanvasOverlay.Off, ChatCanvasOverlay.Vignette, ChatCanvasOverlay.Spotlight, ChatCanvasOverlay.Mesh, ChatCanvasOverlay.Diagonal, ChatCanvasOverlay.Aurora, ChatCanvasOverlay.FullBurst, ChatCanvasOverlay.ArcCore];
        string[] auras = TargetAura.Catalog.Select(a => a.Value).ToArray();
        string[] anims = [MessageMotion.Off, MessageMotion.Fade, MessageMotion.Slide, MessageMotion.Pop];
        string[] avatars = [AppearanceOptions.Default, AppearanceOptions.AvatarCircle, AppearanceOptions.AvatarSquircle, AppearanceOptions.AvatarRounded];

        var d = new LookData
        {
            Theme = theme,
            ThemeDetails = rng.Next(2) == 0 ? true : null,
            ColorFinish = rng.Next(2) == 0 ? NonDefault(finishes[rng.Next(finishes.Length)], Desktop.ColorFinish.Off) : null,
            ColorFinishIntensity = .65,
            BubbleStyle = bubbleStyles[rng.Next(bubbleStyles.Length)],
            BubbleShape = bubbleShapes[rng.Next(bubbleShapes.Length)],
            CanvasGradient = gradients[rng.Next(gradients.Length)],
            CanvasGradientIntensity = .55,
            SeamlessComposer = rng.Next(2) == 0 ? true : null,
            ChromeTexture = textures[rng.Next(textures.Length)],
            ChromeTextureIntensity = .65,
            TargetAura = auras[rng.Next(auras.Length)],
            TargetAuraIntensity = .70,
            AuraModule = true,
            AuraPerson = true,
            AuraButtons = true,
            AvatarShape = avatars[rng.Next(avatars.Length)],
            OnlineRing = rng.Next(3) == 0 ? true : null,
            MsgAnim = anims[rng.Next(anims.Length)],
            EffectSpeed = Math.Round(.7 + rng.NextDouble() * .6, 2),
            ButtonGlow = rng.Next(2) == 0 ? true : null,
            ButtonBounce = rng.Next(2) == 0 ? true : null,
            ButtonLift = rng.Next(3) == 0 ? true : null,
            ButtonSpark = rng.Next(3) == 0 ? true : null,
            ButtonRing = rng.Next(3) == 0 ? true : null
        };

        // Pick one ambient effect cleanly so random looks stay tasteful and legible.
        switch (rng.Next(12))
        {
            case 0: d.Petals = true; break;
            case 1: d.Fireflies = true; break;
            case 2: d.Rain = true; break;
            case 3: d.Snow = true; break;
            case 4: d.Starfield = true; break;
            case 5: d.Bubbles = true; break;
            case 6: d.Embers = true; break;
            case 7: d.Matrix = true; break;
            case 8: d.Constellation = true; break;
            case 9: d.Bokeh = true; break;
            case 10: d.WingPlumes = true; break;
            case 11: d.Repulsor = true; break;
        }
        switch (rng.Next(10))
        {
            case 0: d.TitleSparkles = true; break;
            case 1: d.TitleShimmer = true; break;
            case 2: d.TitleComet = true; break;
            case 3: d.TitleEqualizer = true; break;
            case 4: d.TitleLaser = true; break;
            case 5: d.TitleAurora = true; break;
            case 6: d.TitleWave = true; break;
            case 7: d.TitleWings = true; break;
            case 8: d.TitleReactor = true; break;
        }
        switch (rng.Next(12))
        {
            case 0: d.HeaderAurora = true; break;
            case 1: d.HeaderPulse = true; break;
            case 2: d.HeaderPrism = true; break;
            case 3: d.HeaderRipples = true; break;
            case 4: d.HeaderCircuit = true; break;
            case 5: d.HeaderStarlight = true; break;
            case 6: d.HeaderNebula = true; break;
            case 7: d.HeaderSilk = true; break;
            case 8: d.HeaderPlasma = true; break;
            case 9: d.HeaderBurst = true; break;
            case 10: d.HeaderArc = true; break;
        }
        switch (rng.Next(11))
        {
            case 0: d.RailBeacon = true; break;
            case 1: d.RailStream = true; break;
            case 2: d.RailStardust = true; break;
            case 3: d.RailScan = true; break;
            case 4: d.RailAurora = true; break;
            case 5: d.RailPulse = true; break;
            case 6: d.RailPlasma = true; break;
            case 7: d.RailCascade = true; break;
            case 8: d.RailWings = true; break;
            case 9: d.RailArc = true; break;
        }
        switch (rng.Next(12))
        {
            case 0: d.SidebarMotes = true; break;
            case 1: d.SidebarGlow = true; break;
            case 2: d.SidebarPrism = true; break;
            case 3: d.SidebarSweep = true; break;
            case 4: d.SidebarAurora = true; break;
            case 5: d.SidebarNebula = true; break;
            case 6: d.SidebarWaves = true; break;
            case 7: d.SidebarRipples = true; break;
            case 8: d.SidebarCircuit = true; break;
            case 9: d.SidebarWings = true; break;
            case 10: d.SidebarHud = true; break;
        }
        if (rng.Next(2) == 0) d.ChatSurface = AppearanceOptions.ChatSurfaceTheme;
        return d;
    }

    private static string? NormalizeHexOrNull(string? value)
    {
        string normalized = AppearanceOptions.NormalizeHex(value);
        return normalized.Length == 0 ? null : normalized;
    }

    private static string? NonDefault(string value, string fallback) => value == fallback ? null : value;
    private static double Round2(double value) => Math.Round(Math.Clamp(value, .1, 1), 2);
    private static double ClampUnit(double? value, double fallback) => value is { } v && double.IsFinite(v) ? Math.Clamp(v, .1, 1) : fallback;
}

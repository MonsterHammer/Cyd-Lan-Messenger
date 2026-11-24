using Color = System.Windows.Media.Color;

namespace CydLan.Desktop;

internal static class EffectPalette
{
    internal const int OpacityLevels = 16;
    internal static int OpacityIndex(double opacity) =>
        (int)Math.Round(Math.Clamp(opacity, 0, 1) * (OpacityLevels - 1));

    internal static Color WithOpacity(Color color, int index) =>
        Color.FromArgb((byte)(255 * index / (OpacityLevels - 1)), color.R, color.G, color.B);

    internal static Color Blend(Color source, Color target, double amount)
    {
        byte Channel(byte from, byte to) => (byte)Math.Round(from + (to - from) * amount);
        return Color.FromRgb(Channel(source.R, target.R), Channel(source.G, target.G), Channel(source.B, target.B));
    }
}

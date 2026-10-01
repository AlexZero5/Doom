namespace Doom.Rendering;

/// <summary>Утилиты для цвета в формате 0xRRGGBB.</summary>
internal static class ColorRgb
{
    public static int Red(int color) => (color >> 16) & 0xFF;

    public static int Green(int color) => (color >> 8) & 0xFF;

    public static int Blue(int color) => color & 0xFF;

    public static int Pack(int r, int g, int b) => (Clamp(r) << 16) | (Clamp(g) << 8) | Clamp(b);

    public static int Clamp(int value) => value < 0 ? 0 : value > 255 ? 255 : value;

    /// <summary>Смешивает два цвета: <paramref name="alpha" /> = 0 — фон, 1 — передний план.</summary>
    public static int Blend(int background, int foreground, double alpha)
    {
        int r = (int)(Red(background) + (Red(foreground) - Red(background)) * alpha);
        int g = (int)(Green(background) + (Green(foreground) - Green(background)) * alpha);
        int b = (int)(Blue(background) + (Blue(foreground) - Blue(background)) * alpha);

        return Pack(r, g, b);
    }
}

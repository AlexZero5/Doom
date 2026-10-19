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
        // Целочисленная арифметика с весом 0..256: смешивание вызывается на каждую ячейку
        // при засветке и затемнении меню, а double-версия была заметно дороже.
        int weight = alpha <= 0 ? 0 : alpha >= 1 ? 256 : (int)(alpha * 256 + 0.5);
        int inverse = 256 - weight;

        int r = (Red(background) * inverse + Red(foreground) * weight) >> 8;
        int g = (Green(background) * inverse + Green(foreground) * weight) >> 8;
        int b = (Blue(background) * inverse + Blue(foreground) * weight) >> 8;

        return (r << 16) | (g << 8) | b;
    }
}

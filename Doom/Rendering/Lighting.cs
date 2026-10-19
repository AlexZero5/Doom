namespace Doom.Rendering;

/// <summary>
///     Освещение по дистанции — одно для стен, пола, объектов и спрайтов. Яркость меняется
///     ступенями (<see cref="Levels" /> уровней, как таблица COLORMAP в Doom): соседние пиксели
///     одной поверхности получают одинаковый цвет — это и привычный «думовский» вид, и меньше
///     уникальных цветов в кадре, а значит, меньше байтов в терминал.
/// </summary>
internal static class Lighting
{
    private const double Range = 20.0;
    private const double Offset = 5.0;
    private const double Gamma = 1.0 / 1.6;

    /// <summary>Число ступеней яркости.</summary>
    public const int Levels = 32;

    /// <summary>Затенение граней, обращённых «вбок» (горизонтальные стороны клеток, бока объектов).</summary>
    public const double SideShadow = 0.78;

    /// <summary>Яркость поверхности на перпендикулярной дистанции (0..1, ступенями).</summary>
    public static double Brightness(double distance, double minimum = 0.03)
    {
        double brightness = Range / (distance + Offset);
        if (brightness > 1) brightness = 1;
        if (brightness < minimum) brightness = minimum;
        return Quantize(Math.Pow(brightness, Gamma));
    }

    /// <summary>Округление яркости до ступени.</summary>
    public static double Quantize(double brightness) =>
        Math.Max(1.0 / Levels, Math.Round(brightness * Levels) / Levels);

    /// <summary>Цвет текселя с яркостью (0xRRGGBB).</summary>
    public static int Shade(int argb, double brightness)
    {
        int r = (int)(((argb >> 16) & 0xFF) * brightness);
        int g = (int)(((argb >> 8) & 0xFF) * brightness);
        int b = (int)((argb & 0xFF) * brightness);
        if (r > 255) r = 255;
        if (g > 255) g = 255;
        if (b > 255) b = 255;
        return (r << 16) | (g << 8) | b;
    }
}

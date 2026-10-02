namespace Doom.Assets;

/// <summary>
///     Текстура: неизменяемая сетка пикселей в формате ARGB (0xAARRGGBB, как в
///     System.Drawing). Создаётся из PNG при загрузке ассетов; сэмплируется лучами
///     стен и отрисовкой спрайтов с ближайшей соседней интерполяцией.
/// </summary>
internal sealed class Texture
{
    public Texture(string name, int width, int height, int[] argb)
    {
        Name = name;
        Width = width;
        Height = height;
        Pixels = argb;
    }

    public string Name { get; internal set; }

    public int Width { get; }

    public int Height { get; }

    public int[] Pixels { get; }

    /// <summary>Прозрачный (по альфе) пиксель — рисунок «дырявый» в этом месте.</summary>
    public static bool IsTransparent(int argb) => (argb >>> 24) < 128;

    /// <summary>Цвет без альфы: формат кадра — 0xRRGGBB.</summary>
    public static int RgbOf(int argb) => argb & 0xFFFFFF;

    /// <summary>Сэмпл по UV (0..1), ближайший пиксель, края зажимаются.</summary>
    public int Sample(double u, double v)
    {
        int x = (int)(u * Width);
        int y = (int)(v * Height);
        return Pixels[Math.Clamp(y, 0, Height - 1) * Width + Math.Clamp(x, 0, Width - 1)];
    }

    /// <summary>Сэмпл по индексу пикселя (без проверок границ — для внутренних циклов).</summary>
    public int SamplePixel(int x, int y) => Pixels[y * Width + x];
}

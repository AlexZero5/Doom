namespace Doom.Assets;

/// <summary>
///     Текстура: неизменяемая сетка пикселей ARGB (0xAARRGGBB). Создаётся из PNG при загрузке
///     ассетов; сэмплируется ближайшим пикселем. Для дальних поверхностей есть мип-уровни —
///     уменьшенные копии (каждая вдвое меньше): вдали пиксель кадра накрывает много текселей,
///     и без них поверхность рябит при движении.
/// </summary>
internal sealed class Texture
{
    private Texture[]? _mips;
    private int _averageColor = -1;

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

    /// <summary>Средний цвет непрозрачных пикселей (0xRRGGBB) — запасной цвет поверхности.</summary>
    public int AverageColor
    {
        get
        {
            if (_averageColor >= 0)
                return _averageColor;

            long r = 0, g = 0, b = 0, count = 0;
            foreach (int argb in Pixels)
            {
                if (IsTransparent(argb)) continue;
                r += (argb >> 16) & 0xFF;
                g += (argb >> 8) & 0xFF;
                b += argb & 0xFF;
                count++;
            }

            _averageColor = count == 0 ? 0x808080 : (int)((r / count << 16) | (g / count << 8) | (b / count));
            return _averageColor;
        }
    }

    /// <summary>Сэмпл по UV с повтором (тайлинг): u = 1.3 — то же, что 0.3.</summary>
    public int Sample(double u, double v)
    {
        int x = (int)Math.Floor(u * Width) % Width;
        int y = (int)Math.Floor(v * Height) % Height;
        if (x < 0) x += Width;
        if (y < 0) y += Height;
        return Pixels[y * Width + x];
    }

    /// <summary>Сэмпл по индексу пикселя (без проверок границ — для внутренних циклов).</summary>
    public int SamplePixel(int x, int y) => Pixels[y * Width + x];

    /// <summary>
    ///     Мип-уровень: 0 — сама текстура, каждый следующий вдвое меньше (усреднение 2×2).
    ///     Строятся лениво и один раз.
    /// </summary>
    public Texture Mip(int level)
    {
        if (level <= 0)
            return this;

        Texture[] mips = _mips ??= BuildMips();
        return mips[Math.Min(level, mips.Length) - 1];
    }

    private Texture[] BuildMips()
    {
        var levels = new List<Texture>();
        Texture current = this;

        while (current.Width > 4 && current.Height > 4 && levels.Count < 5)
        {
            int width = current.Width / 2, height = current.Height / 2;
            var pixels = new int[width * height];

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int r = 0, g = 0, b = 0, a = 0;
                for (int dy = 0; dy < 2; dy++)
                for (int dx = 0; dx < 2; dx++)
                {
                    int argb = current.Pixels[(y * 2 + dy) * current.Width + x * 2 + dx];
                    r += (argb >> 16) & 0xFF;
                    g += (argb >> 8) & 0xFF;
                    b += argb & 0xFF;
                    a += (argb >>> 24) & 0xFF;
                }

                pixels[y * width + x] = ((a / 4) << 24) | ((r / 4) << 16) | ((g / 4) << 8) | (b / 4);
            }

            current = new Texture(Name, width, height, pixels);
            levels.Add(current);
        }

        return levels.Count > 0 ? levels.ToArray() : new[] { this };
    }
}

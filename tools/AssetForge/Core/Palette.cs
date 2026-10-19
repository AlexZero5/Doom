namespace AssetForge.Core;

/// <summary>
///     Общая палитра игры: 16 «рамп» по 16 оттенков (как PLAYPAL в Doom). Все картинки
///     квантуются в неё с лёгким упорядоченным дизерингом — это и даёт единый «рукотворный»
///     стиль: ограниченные цвета, заметные ступени, никаких мягких нейро-градиентов.
/// </summary>
internal static class Palette
{
    private static readonly int[,] Bayer =
    {
        { 0, 8, 2, 10 },
        { 12, 4, 14, 6 },
        { 3, 11, 1, 9 },
        { 15, 7, 13, 5 }
    };

    private static readonly Rgb[][] RampStops =
    {
        new[] { Rgb.Hex(0x060606), Rgb.Hex(0x5a5a5a), Rgb.Hex(0xb4b4b4), Rgb.Hex(0xffffff) }, // серый
        new[] { Rgb.Hex(0x0e0c0a), Rgb.Hex(0x4c4640), Rgb.Hex(0x928a7e), Rgb.Hex(0xdcd4c4) }, // камень
        new[] { Rgb.Hex(0x120a05), Rgb.Hex(0x4a2c16), Rgb.Hex(0x8c5c30), Rgb.Hex(0xdcae70) }, // дерево
        new[] { Rgb.Hex(0x1a1408), Rgb.Hex(0x6b5530), Rgb.Hex(0xb89a62), Rgb.Hex(0xf4e6b8) }, // песок
        new[] { Rgb.Hex(0x160404), Rgb.Hex(0x601210), Rgb.Hex(0xac2c20), Rgb.Hex(0xf48466) }, // кирпич/кровь
        new[] { Rgb.Hex(0x1c0800), Rgb.Hex(0x7c2c00), Rgb.Hex(0xe46c10), Rgb.Hex(0xffd878) }, // огонь
        new[] { Rgb.Hex(0x161200), Rgb.Hex(0x6c5400), Rgb.Hex(0xd6b222), Rgb.Hex(0xfff6a8) }, // золото
        new[] { Rgb.Hex(0x0c0e04), Rgb.Hex(0x40481a), Rgb.Hex(0x808c3c), Rgb.Hex(0xcad28e) }, // олива
        new[] { Rgb.Hex(0x041206), Rgb.Hex(0x1a4c1c), Rgb.Hex(0x40903a), Rgb.Hex(0xb0ec70) }, // зелень
        new[] { Rgb.Hex(0x021212), Rgb.Hex(0x0f4c4a), Rgb.Hex(0x2c8c86), Rgb.Hex(0x98e6de) }, // бирюза
        new[] { Rgb.Hex(0x060c1c), Rgb.Hex(0x24467e), Rgb.Hex(0x5c90d0), Rgb.Hex(0xdcf0ff) }, // небо
        new[] { Rgb.Hex(0x07090e), Rgb.Hex(0x2c3644), Rgb.Hex(0x5e6e82), Rgb.Hex(0xc8d4e0) }, // сталь
        new[] { Rgb.Hex(0x0e0516), Rgb.Hex(0x3e1c5a), Rgb.Hex(0x7c46a4), Rgb.Hex(0xdcb4f4) }, // фиолет
        new[] { Rgb.Hex(0x1a0d08), Rgb.Hex(0x6c3c28), Rgb.Hex(0xc27c58), Rgb.Hex(0xffd8b8) }, // кожа
        new[] { Rgb.Hex(0x16060c), Rgb.Hex(0x601c32), Rgb.Hex(0xb24c6c), Rgb.Hex(0xf6b4c8) }, // ткань
        new[] { Rgb.Hex(0x001420), Rgb.Hex(0x00708e), Rgb.Hex(0x34c4f4), Rgb.Hex(0xe8ffff) }, // плазма
    };

    public static readonly Rgb[] Colors = Build();

    private static readonly Dictionary<int, Rgb> NearestCache = new();

    private static Rgb[] Build()
    {
        var colors = new List<Rgb>(256);
        foreach (Rgb[] stops in RampStops)
        {
            for (int step = 0; step < 16; step++)
                colors.Add(Rgb.Ramp(step / 15.0, stops));
        }

        return colors.ToArray();
    }

    /// <summary>Ближайший цвет палитры (взвешенное расстояние, ближе к восприятию).</summary>
    public static Rgb Nearest(Rgb color)
    {
        int key = color.Argb(0);
        lock (NearestCache)
        {
            if (NearestCache.TryGetValue(key, out Rgb cached))
                return cached;
        }

        Rgb best = Colors[0];
        double bestDistance = double.MaxValue;
        int r = Math.Clamp(color.R, 0, 255), g = Math.Clamp(color.G, 0, 255), b = Math.Clamp(color.B, 0, 255);

        foreach (Rgb candidate in Colors)
        {
            double meanR = (r + candidate.R) * 0.5;
            double dr = r - candidate.R, dg = g - candidate.G, db = b - candidate.B;
            double distance = (2 + meanR / 256) * dr * dr + 4 * dg * dg + (2 + (255 - meanR) / 256) * db * db;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }

        lock (NearestCache)
            NearestCache[key] = best;

        return best;
    }

    /// <summary>
    ///     Квантует холст в палитру. Альфа становится двоичной (0 или 255): у спрайтов чёткий
    ///     край без полупрозрачной «каймы» (та самая зелёная обводка у старых снарядов).
    /// </summary>
    public static void Apply(Canvas canvas, double dither = 10)
    {
        for (int y = 0; y < canvas.Height; y++)
        for (int x = 0; x < canvas.Width; x++)
        {
            if (!canvas.Opaque(x, y))
            {
                canvas[x, y] = 0;
                continue;
            }

            Rgb color = canvas.ColorAt(x, y);
            int offset = (int)Math.Round((Bayer[y & 3, x & 3] / 15.0 - 0.5) * dither);
            canvas[x, y] = Nearest(color.Add(offset)).Argb();
        }
    }

    /// <summary>Картинка палитры (для проверки глазами).</summary>
    public static Canvas Swatch()
    {
        var canvas = new Canvas(16 * 8, 16 * 8);
        for (int index = 0; index < 256; index++)
            canvas.FillRect(index % 16 * 8, index / 16 * 8, 8, 8, Colors[index]);
        return canvas;
    }
}

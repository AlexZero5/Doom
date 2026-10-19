using AssetForge.Core;

namespace AssetForge.Art;

/// <summary>
///     Небо: панорама 360° шириной 512 (тайлится по горизонтали), по вертикали — от зенита
///     (верхняя строка) до горизонта (нижняя). У горизонта — дальние холмы и лес силуэтами.
/// </summary>
internal static class Skies
{
    private const int Width = 512;
    private const int Height = 128;

    public static IEnumerable<(string Name, Canvas Sky)> All()
    {
        yield return ("day", Build(
            gradient: new[] { Rgb.Hex(0x24467e), Rgb.Hex(0x5c90d0), Rgb.Hex(0xc4dcf0) }, cloudThreshold: 0.62,
            cloudLight: Rgb.Hex(0xf4f4ee), cloudShade: Rgb.Hex(0x9aa8bc),
            farHills: Rgb.Hex(0x6c84a0), nearHills: Rgb.Hex(0x2e4a34), seed: 401, sun: null));

        yield return ("dusk", Build(
            gradient: new[] { Rgb.Hex(0x1c1238), Rgb.Hex(0x7c46a4), Rgb.Hex(0xe0607a), Rgb.Hex(0xffb060) }, cloudThreshold: 0.68,
            cloudLight: Rgb.Hex(0xf8b47a), cloudShade: Rgb.Hex(0x6a3a5a),
            farHills: Rgb.Hex(0x5a3a5a), nearHills: Rgb.Hex(0x1c1220), seed: 402, sun: (360, 104)));
    }

    private static Canvas Build(Rgb[] gradient, double cloudThreshold, Rgb cloudLight, Rgb cloudShade,
        Rgb farHills, Rgb nearHills, int seed, (int X, int Y)? sun)
    {
        var canvas = new Canvas(Width, Height);

        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            double t = Math.Pow(y / (double)(Height - 1), 1.6);
            Rgb sky = Rgb.Ramp(t, gradient);

            if (sun is { } s)
            {
                double distance = Math.Sqrt(Math.Pow(Wrap(x - s.X), 2) + Math.Pow((y - s.Y) * 1.4, 2));
                if (distance < 9) sky = Rgb.Hex(0xfff0b0);
                else if (distance < 40) sky = sky.Lerp(Rgb.Hex(0xffc070), (40 - distance) / 31 * 0.55);
            }

            // Облака: порог по фрактальному шуму, сплющенному по вертикали; гуще у горизонта.
            double band = Math.Clamp(1.0 - Math.Abs(y - 70) / 60.0, 0, 1);
            double n = Noise.Streaks(x, y * 3.0, Width, seed, 8, 3, 4);
            double cover = n + band * 0.18 - cloudThreshold;
            if (cover > 0)
            {
                double above = Noise.Streaks(x, (y - 3) * 3.0, Width, seed, 8, 3, 4) + band * 0.18 - cloudThreshold;
                Rgb cloud = above > cover ? cloudShade.Lerp(cloudLight, 0.45) : cloudLight;
                if (cover < 0.04) cloud = cloud.Lerp(sky, 0.5);
                if (above <= 0) cloud = cloudLight.Add(10);
                sky = cloud.Lerp(cloudShade, Math.Clamp((y - 40) / 120.0, 0, 0.6));
            }

            canvas[x, y] = sky.Argb();
        }

        Hills(canvas, farHills, seed + 10, baseLine: Height - 14, amplitude: 12, cells: 6, treeline: false);
        Hills(canvas, nearHills, seed + 20, baseLine: Height - 4, amplitude: 9, cells: 10, treeline: true);
        return canvas;
    }

    private static double Wrap(double dx) => dx > Width / 2.0 ? dx - Width : dx < -Width / 2.0 ? dx + Width : dx;

    /// <summary>Силуэт холмов (и зубчатой кромки леса) у нижнего края панорамы.</summary>
    private static void Hills(Canvas canvas, Rgb color, int seed, int baseLine, int amplitude, int cells, bool treeline)
    {
        for (int x = 0; x < Width; x++)
        {
            double h = Noise.Streaks(x, 0, Width, seed, cells, 1, 3) * amplitude;
            if (treeline)
                h += (Noise.Unit(x / 3, 0, seed + 1) * 4) + (x % 3 == 1 ? 1 : 0);

            int top = (int)(baseLine - h);
            for (int y = Math.Max(0, top); y < Height; y++)
            {
                double shade = 1.0 - (y - top) * 0.012 + (Noise.Unit(x, y, seed + 2) - 0.5) * 0.08;
                canvas[x, y] = color.Scale(shade).Argb();
            }
        }
    }
}

/// <summary>Листы предпросмотра для проверки глазами.</summary>
internal static class Sheets
{
    /// <summary>Каждая текстура плиткой 2×2 (видно швы), 8 штук в ряд.</summary>
    public static Canvas Tiled(IReadOnlyList<(string Name, Canvas Texture)> items)
    {
        const int cell = 132;
        int columns = 8;
        int rows = (items.Count + columns - 1) / columns;
        var sheet = new Canvas(columns * cell, rows * cell);
        sheet.FillRect(0, 0, sheet.Width, sheet.Height, Rgb.Hex(0x202020));

        for (int i = 0; i < items.Count; i++)
        {
            Canvas texture = items[i].Texture;
            int ox = i % columns * cell + 2, oy = i / columns * cell + 2;
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 128; x++)
                sheet[ox + x, oy + y] = texture[x % texture.Width, y % texture.Height];
        }

        return sheet;
    }

    /// <summary>Увеличение без сглаживания; прозрачное — на шахматке.</summary>
    public static Canvas Scale(Canvas source, int factor)
    {
        var result = new Canvas(source.Width * factor, source.Height * factor);
        for (int y = 0; y < result.Height; y++)
        for (int x = 0; x < result.Width; x++)
        {
            int argb = source[x / factor, y / factor];
            bool checker = (x / 8 + y / 8) % 2 == 0;
            result[x, y] = (uint)argb >> 24 >= 128 ? argb : (checker ? Rgb.Hex(0x303038) : Rgb.Hex(0x404048)).Argb();
        }

        return result;
    }

    /// <summary>Ряд картинок на одном листе (кадры анимации), с увеличением.</summary>
    public static Canvas Strip(IReadOnlyList<Canvas> frames, int factor, int gap = 4)
    {
        int width = frames.Sum(frame => frame.Width) + gap * (frames.Count + 1);
        int height = frames.Max(frame => frame.Height) + gap * 2;
        var strip = new Canvas(width, height);
        int x = gap;
        foreach (Canvas frame in frames)
        {
            strip.Blit(frame, x, gap);
            x += frame.Width + gap;
        }

        return Scale(strip, factor);
    }
}

using AssetForge.Core;

namespace AssetForge.Art;

/// <summary>
///     Холст для «сборки» спрайта из частей: каждая часть — маска фигуры, закрашенная цветом
///     с рельефным освещением (свет сверху-слева). Части накладываются по порядку (дальние раньше).
/// </summary>
internal sealed class Sprite
{
    public Sprite(int width, int height)
    {
        Width = width;
        Height = height;
        Canvas = new Canvas(width, height);
    }

    public int Width { get; }

    public int Height { get; }

    public Canvas Canvas { get; }

    public Mask Poly(params (double X, double Y)[] points) => Mask.Polygon(Width, Height, points);

    public Mask Ellipse(double cx, double cy, double rx, double ry) => Mask.Ellipse(Width, Height, cx, cy, rx, ry);

    public Mask Rect(double x, double y, double w, double h) => Mask.Rect(Width, Height, x, y, w, h);

    public Mask Round(double x, double y, double w, double h, double r) => Mask.RoundRect(Width, Height, x, y, w, h, r);

    public Mask Line(double ax, double ay, double bx, double by, double thickness) =>
        Mask.Line(Width, Height, ax, ay, bx, by, thickness);

    /// <summary>
    ///     Часть с объёмом: цвет × освещение рельефа. <paramref name="gloss" /> добавляет блик
    ///     на самых освещённых местах (металл).
    /// </summary>
    public void Part(Mask mask, Rgb color, double depth = 2.5, double strength = 1.0, double gloss = 0.0,
        double grain = 0.06, int seed = 0)
    {
        double[] light = Relief.Light(mask, depth, strength);
        Canvas.Paint(mask, (x, y) =>
        {
            double l = light[y * Width + x];
            double g = (Noise.Unit(x, y, 900 + seed) - 0.5) * grain * 2;
            Rgb shaded = color.Scale(l * (1 + g));
            if (gloss > 0 && l > 1.2)
                shaded = shaded.Lerp(Rgb.Hex(0xffffff), Math.Min(1, (l - 1.2) * 2) * gloss);
            return shaded;
        });
    }

    /// <summary>Часть с вертикальным градиентом цвета (верх → низ) и рельефом.</summary>
    public void Gradient(Mask mask, Rgb top, Rgb bottom, double depth = 2.5, double strength = 1.0)
    {
        double[] light = Relief.Light(mask, depth, strength);
        int minY = Height, maxY = 0;
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            if (!mask[x, y]) continue;
            minY = Math.Min(minY, y);
            maxY = Math.Max(maxY, y);
        }

        Canvas.Paint(mask, (x, y) =>
        {
            double t = maxY > minY ? (y - minY) / (double)(maxY - minY) : 0;
            return top.Lerp(bottom, t).Scale(light[y * Width + x]);
        });
    }

    /// <summary>Плоская заливка без рельефа (тени, отверстия, свечение).</summary>
    public void Flat(Mask mask, Rgb color) => Canvas.Paint(mask, color);

    /// <summary>
    ///     Вспышка выстрела: звезда из лучей и яркое ядро. Цвета — от ядра к краю.
    /// </summary>
    public void Flash(double cx, double cy, double radius, int rays, int seed, params Rgb[] colors)
    {
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            double dx = x + 0.5 - cx, dy = (y + 0.5 - cy) * 1.15;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            double angle = Math.Atan2(dy, dx);
            double rayShape = 0.55 + 0.45 * Math.Pow(Math.Abs(Math.Cos(angle * rays / 2.0 + seed)), 6);
            double jitter = 0.85 + Noise.Unit((int)(angle * 10), seed, 931) * 0.3;
            double reach = radius * rayShape * jitter;
            if (distance > reach) continue;

            double t = distance / reach;
            Canvas.Plot(x, y, Rgb.Ramp(t, colors));
        }
    }

    /// <summary>Обводка, квантование в палитру, готовый холст.</summary>
    public Canvas Finish(Rgb? outline = null, double dither = 6)
    {
        Canvas.Outline(outline ?? Rgb.Hex(0x0e0a08));
        Palette.Apply(Canvas, dither);
        return Canvas;
    }

    // Общие цвета (подобраны под палитру).
    public static readonly Rgb Skin = Rgb.Hex(0xc27c58);
    public static readonly Rgb Steel = Rgb.Hex(0x5e6e82);
    public static readonly Rgb DarkSteel = Rgb.Hex(0x2c3644);
    public static readonly Rgb Gunmetal = Rgb.Hex(0x3c4048);
    public static readonly Rgb Wood = Rgb.Hex(0x8c5c30);
    public static readonly Rgb Hole = Rgb.Hex(0x0a0a0c);

    public static readonly Rgb[] FireFlash =
        { Rgb.Hex(0xffffff), Rgb.Hex(0xfff6a8), Rgb.Hex(0xffc040), Rgb.Hex(0xe46c10), Rgb.Hex(0x9c2c08) };

    public static readonly Rgb[] PlasmaFlash =
        { Rgb.Hex(0xffffff), Rgb.Hex(0xe8ffff), Rgb.Hex(0x34c4f4), Rgb.Hex(0x00708e) };

    public static readonly Rgb[] BfgFlash =
        { Rgb.Hex(0xffffff), Rgb.Hex(0xe8ffc0), Rgb.Hex(0xb0ec70), Rgb.Hex(0x40903a), Rgb.Hex(0x1a4c1c) };
}

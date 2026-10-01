using Doom.World;

namespace Doom.Rendering;

/// <summary>
///     Полоса спрайта: часть силуэта бонуса со своим цветом и долей ширины.
///     Полосы рисуются снизу вверх, поэтому верхние перекрывают нижние.
/// </summary>
internal readonly struct SpriteBand
{
    public SpriteBand(double bottomZ, double topZ, double widthFactor, int color)
    {
        BottomZ = bottomZ;
        TopZ = topZ;
        WidthFactor = widthFactor;
        Color = color;
    }

    public double BottomZ { get; }

    public double TopZ { get; }

    public double WidthFactor { get; }

    public int Color { get; }
}

/// <summary>Внешний вид бонуса: общая ширина в клетках и полосы силуэта.</summary>
internal readonly struct PickupVisual
{
    public PickupVisual(double width, params SpriteBand[] bands)
    {
        Width = width;
        Bands = bands;
    }

    public double Width { get; }

    public SpriteBand[] Bands { get; }
}

/// <summary>
///     Цвета и силуэты бонусов. Боеприпасы — одна узкая полоса, оружие — несколько полос,
///     чтобы его силуэт читался издалека.
/// </summary>
internal static class PickupPalette
{
    private static readonly PickupVisual[] Visuals =
    {
        // Clip — жёлтая обойма (цвет сохранён как в исходной версии).
        new(0.35, new SpriteBand(0.0, 0.30, 1.0, 0xFFD24A)),

        // BulletBox — крупный тёмно-жёлтый ящик.
        new(0.44, new SpriteBand(0.0, 0.34, 1.0, 0xC8A02A), new SpriteBand(0.34, 0.40, 0.7, 0x8A6E1E)),

        // Shells — четыре красных патрона.
        new(0.30, new SpriteBand(0.0, 0.26, 1.0, 0xE04030)),

        // ShellBox — ящик дроби.
        new(0.46, new SpriteBand(0.0, 0.30, 1.0, 0xA8332A), new SpriteBand(0.30, 0.36, 0.75, 0xD85848)),

        // Rocket — одна ракета.
        new(0.28, new SpriteBand(0.0, 0.24, 1.0, 0x8A8A96), new SpriteBand(0.24, 0.34, 0.5, 0xC03028)),

        // RocketBox — ящик ракет.
        new(0.48, new SpriteBand(0.0, 0.34, 1.0, 0x6E6E78), new SpriteBand(0.34, 0.42, 0.6, 0xC03028)),

        // Cell — светящаяся батарея.
        new(0.30, new SpriteBand(0.0, 0.26, 1.0, 0x3AD2FF)),

        // CellPack — упаковка энергоячеек.
        new(0.46, new SpriteBand(0.0, 0.30, 1.0, 0x2A9AC8), new SpriteBand(0.30, 0.38, 0.7, 0x3AD2FF)),

        // Chainsaw — оранжевый корпус с серым полотном.
        new(0.55,
            new SpriteBand(0.0, 0.22, 1.0, 0xC06020),
            new SpriteBand(0.22, 0.38, 0.55, 0xA8AEB8),
            new SpriteBand(0.38, 0.48, 0.28, 0x2A2E34)),

        // Shotgun — деревянный приклад и два ствола.
        new(0.58,
            new SpriteBand(0.0, 0.16, 1.0, 0x6A4526),
            new SpriteBand(0.16, 0.32, 0.9, 0x2A2E34),
            new SpriteBand(0.32, 0.38, 0.45, 0x9AA0A8)),

        // Chaingun — блок стволов с латунным кольцом.
        new(0.60,
            new SpriteBand(0.0, 0.14, 1.0, 0x2E3238),
            new SpriteBand(0.14, 0.34, 0.7, 0x9AA0A8),
            new SpriteBand(0.34, 0.42, 0.35, 0xB08A30)),

        // Rocket launcher — труба с прицелом и красным маркером.
        new(0.62,
            new SpriteBand(0.0, 0.30, 1.0, 0x5E6A44),
            new SpriteBand(0.30, 0.46, 0.45, 0x3A4030),
            new SpriteBand(0.46, 0.52, 0.20, 0xC03028)),

        // Plasma rifle — синий корпус со светящимся ядром.
        new(0.58,
            new SpriteBand(0.0, 0.24, 1.0, 0x33485E),
            new SpriteBand(0.24, 0.42, 0.55, 0x3AD2FF),
            new SpriteBand(0.42, 0.50, 0.25, 0xE8FFD0)),

        // BFG9000 — тёмный корпус с огромным зелёным раструбом.
        new(0.72,
            new SpriteBand(0.0, 0.26, 1.0, 0x2A5A22),
            new SpriteBand(0.26, 0.50, 0.8, 0x8CFF3A),
            new SpriteBand(0.50, 0.62, 0.4, 0xE8FFD0))
    };

    public static PickupVisual Get(PickupKind kind) => Visuals[(int)kind];
}

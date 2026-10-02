using Doom.Assets;

namespace Doom.Rendering;

/// <summary>Готовый к отрисовке спрайт: экранные границы, глубина, цвет и прозрачность.</summary>
internal readonly struct SpriteQuad
{
    public SpriteQuad(
        double minX,
        double maxX,
        double topY,
        double bottomY,
        double perpendicularDistance,
        int color,
        double alpha = 1.0,
        Texture? texture = null,
        double brightness = 1.0)
    {
        MinX = minX;
        MaxX = maxX;
        TopY = topY;
        BottomY = bottomY;
        PerpendicularDistance = perpendicularDistance;
        Color = color;
        Alpha = alpha;
        Texture = texture;
        Brightness = brightness;
    }

    public double MinX { get; }

    public double MaxX { get; }

    public double TopY { get; }

    public double BottomY { get; }

    /// <summary>Расстояние до камеры по оси взгляда (для сортировки и перекрытия стенами).</summary>
    public double PerpendicularDistance { get; }

    public int Color { get; }

    /// <summary>1 — сплошной спрайт, меньше 1 — тающий дым и искры.</summary>
    public double Alpha { get; }

    /// <summary>Текстура спрайта; null — рисовать сплошным цветом.</summary>
    public Texture? Texture { get; }

    /// <summary>Множитель яркости, рассчитанный по дистанции (как у стен).</summary>
    public double Brightness { get; }

    /// <summary>Сортировка по глубине: ближние спрайты рисуются последними.</summary>
    public static int CompareByDistance(SpriteQuad left, SpriteQuad right) =>
        left.PerpendicularDistance.CompareTo(right.PerpendicularDistance);
}

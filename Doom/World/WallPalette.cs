namespace Doom.World;

/// <summary>Базовые цвета стен по типу клетки карты.</summary>
internal static class WallPalette
{
    public static (int R, int G, int B) GetBaseColor(int tileType) => tileType switch
    {
        1 => (235, 70, 70),
        2 => (80, 200, 90),
        3 => (80, 120, 230),
        4 => (230, 200, 80),
        5 => (200, 80, 230),
        _ => (180, 180, 180),
    };
}

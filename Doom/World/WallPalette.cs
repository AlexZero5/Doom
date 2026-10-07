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
        6 => (95, 150, 70),    // трава (пол)
        7 => (135, 138, 148),  // металл (пол)
        8 => (120, 92, 62),    // земля (пол)
        _ => (180, 180, 180),
    };
}

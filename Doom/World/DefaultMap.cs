namespace Doom.World;

/// <summary>
///     Стандартная карта уровня. Строится кодом из описаний зданий: каждый дом —
///     прямоугольник стен с проёмом-дверью, у своего пола и потолка. Снаружи — трава
///     и земляные тропы под открытым небом, внутри — металл/земля и потолок в стиле стен.
///     Коды стен: 1..5 — полная стена, 11..15 — половинная (укрытие), 21..25 — ограждение;
///     вторая цифра — тип текстуры.
/// </summary>
internal static class DefaultMap
{
    private const int Columns = 32;
    private const int Rows = 26;

    private readonly record struct Building(
        int Left,
        int Top,
        int Right,
        int Bottom,
        int WallType,
        int FloorType,
        int CeilingType,
        int DoorX,
        int DoorY);

    // Двери — клетка ПРОЁМА в стене здания (запирается нулем поверх стены).
    private static readonly Building[] Buildings =
    {
        // Улей (зелёный): вход с юга, металлический пол, органический потолок.
        new(2, 3, 7, 7, WallType: 2, FloorType: 7, CeilingType: 2, DoorX: 4, DoorY: 7),

        // Крио-тех (синий): вход с севера, металлический пол.
        new(16, 3, 21, 7, WallType: 3, FloorType: 7, CeilingType: 3, DoorX: 18, DoorY: 3),

        // Песчаник: вход с юга, земляной пол.
        new(9, 11, 15, 15, WallType: 4, FloorType: 8, CeilingType: 4, DoorX: 12, DoorY: 15),

        // Кристалл (фиолетовый): вход с севера, металлический пол.
        new(21, 18, 26, 22, WallType: 5, FloorType: 7, CeilingType: 5, DoorX: 23, DoorY: 18),
    };

    public static Level Create()
    {
        var tiles = new int[Rows, Columns];
        var floors = new int[Rows, Columns];
        var ceilings = new int[Rows, Columns];

        // Периметр уровня — полная стена.
        for (int x = 0; x < Columns; x++)
        {
            tiles[0, x] = 1;
            tiles[Rows - 1, x] = 1;
        }

        for (int y = 0; y < Rows; y++)
        {
            tiles[y, 0] = 1;
            tiles[y, Columns - 1] = 1;
        }

        // Под открытым небом — трава; с севера на юг идёт земляная тропа.
        for (int y = 1; y < Rows - 1; y++)
        {
            for (int x = 1; x < Columns - 1; x++)
                floors[y, x] = 6;

            for (int x = 8; x <= 10; x++)
                floors[y, x] = 8;
        }

        foreach (Building building in Buildings)
        {
            for (int x = building.Left; x <= building.Right; x++)
            {
                tiles[building.Top, x] = building.WallType;
                tiles[building.Bottom, x] = building.WallType;
            }

            for (int y = building.Top; y <= building.Bottom; y++)
            {
                tiles[y, building.Left] = building.WallType;
                tiles[y, building.Right] = building.WallType;
            }

            for (int y = building.Top + 1; y < building.Bottom; y++)
            {
                for (int x = building.Left + 1; x < building.Right; x++)
                {
                    floors[y, x] = building.FloorType;
                    ceilings[y, x] = building.CeilingType;
                }
            }

            // Проём: пол как внутри (порог), потолка над проёмом нет — видно небо.
            tiles[building.DoorY, building.DoorX] = 0;
            floors[building.DoorY, building.DoorX] = building.FloorType;
        }

        // Отдельно стоящие колонны-укрытия во дворе (полная высота).
        tiles[10, 4] = 1;
        tiles[11, 4] = 1;
        tiles[9, 26] = 2;
        tiles[10, 26] = 2;
        tiles[20, 12] = 4;
        tiles[21, 12] = 4;

        return new Level(tiles, floors, ceilings);
    }
}

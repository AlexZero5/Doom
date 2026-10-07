namespace Doom.World;

/// <summary>
///     Уровень: три слоя сетки — стены, полы и потолки — и правила проходимости.
///     Один символ по вертикали игровой клетки равен единице высоты стены.
/// </summary>
internal sealed class Level
{
    /// <summary>Текстура пола «по умолчанию» (трава под открытым небом).</summary>
    private const int DefaultOutdoorFloor = 6;

    private readonly int[,] _tiles;
    private readonly int[,] _floorTiles;
    private readonly int[,] _ceilingTiles;

    public Level(int[,] tiles, int[,]? floorTiles = null, int[,]? ceilingTiles = null)
    {
        _tiles = tiles;
        Rows = tiles.GetLength(0);
        Columns = tiles.GetLength(1);

        _floorTiles = floorTiles ?? new int[Rows, Columns];
        _ceilingTiles = ceilingTiles ?? new int[Rows, Columns];
    }

    public static Level CreateDefault() => DefaultMap.Create();

    public int Rows { get; }

    public int Columns { get; }

    /// <summary>Можно ли встать в точку с мировыми координатами (x, y).</summary>
    public bool IsWalkable(double x, double y) =>
        TryGetTile((int)x, (int)y, out int tileType) && tileType == 0;

    /// <summary>Возвращает тип клетки. <c>false</c> — координаты за пределами карты.</summary>
    public bool TryGetTile(int column, int row, out int tileType)
    {
        if (column < 0 || row < 0 || row >= Rows || column >= Columns)
        {
            tileType = 0;
            return false;
        }

        tileType = _tiles[row, column];
        return true;
    }

    /// <summary>
    ///     Текстура пола в клетке: номер файла из assets/walls/ (6 — трава, 7 — металл,
    ///     8 — земля). Ноль означает «пол по умолчанию» — трава под открытым небом.
    /// </summary>
    public int FloorAt(int column, int row)
    {
        bool inside = column >= 0 && row >= 0 && row < Rows && column < Columns;
        int floor = inside ? _floorTiles[row, column] : 0;
        return floor == 0 ? DefaultOutdoorFloor : floor;
    }

    /// <summary>
    ///     Текстура потолка в клетке (в стиле стен здания) или ноль — потолка нет,
    ///     над клеткой открытое небо.
    /// </summary>
    public int CeilingAt(int column, int row)
    {
        bool inside = column >= 0 && row >= 0 && row < Rows && column < Columns;
        return inside ? _ceilingTiles[row, column] : 0;
    }

    /// <summary>Тип стены клетки (номер текстуры): низкие стены кодируются десятками.</summary>
    public static int WallTypeOf(int tile) => tile % 10;

    /// <summary>
    ///     Высота стены клетки: 1..5 — полная стена, 11..15 — половина (укрытие),
    ///     21..25 — четверть (ограждение). Луч проходит над низкой стеной.
    /// </summary>
    public static double WallHeightOf(int tile) => tile switch
    {
        <= 5 => 1.0,
        <= 15 => 0.5,
        _ => 0.25
    };
}

namespace Doom.World;

/// <summary>Уровень: сетка клеток и правила проходимости.</summary>
internal sealed class Level
{
    private readonly int[,] _tiles;

    public Level(int[,] tiles)
    {
        _tiles = tiles;
        Rows = tiles.GetLength(0);
        Columns = tiles.GetLength(1);
    }

    public static Level CreateDefault() => new(DefaultMap.Tiles);

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
}

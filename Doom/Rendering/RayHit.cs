namespace Doom.Rendering;

/// <summary>Результат трассировки луча: расстояние до стены и точка попадания.</summary>
internal readonly struct RayHit
{
    /// <summary>Луч не упёрся в стену за отведённое число шагов.</summary>
    public static readonly RayHit Miss = new(1e30, 0, 0, 0, 0, 0);

    public RayHit(double distance, int side, int tileType, double wallX, double hitX, double hitY)
    {
        Distance = distance;
        Side = side;
        TileType = tileType;
        WallX = wallX;
        HitX = hitX;
        HitY = hitY;
    }

    public double Distance { get; }

    /// <summary>0 — вертикальная грань стены, 1 — горизонтальная.</summary>
    public int Side { get; }

    public int TileType { get; }

    /// <summary>Дробная координата попадания вдоль стены (0..1).</summary>
    public double WallX { get; }

    /// <summary>Мировая координата X точки попадания (нужна искрам и следам от выстрелов).</summary>
    public double HitX { get; }

    public double HitY { get; }
}

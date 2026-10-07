using Doom.Configuration;
using Doom.World;

namespace Doom.Rendering;

/// <summary>Сегмент стены на пути луча: попадание в один стеновой блок.</summary>
internal readonly struct WallHit
{
    public WallHit(double distance, int side, int tileType, double wallX, double height)
    {
        Distance = distance;
        Side = side;
        TileType = tileType;
        WallX = wallX;
        Height = height;
    }

    /// <summary>Перпендикулярное расстояние до грани (без «рыбьего глаза»).</summary>
    public double Distance { get; }

    /// <summary>0 — вертикальная грань стены, 1 — горизонтальная.</summary>
    public int Side { get; }

    public int TileType { get; }

    /// <summary>Дробная координата попадания вдоль стены (0..1).</summary>
    public double WallX { get; }

    /// <summary>Высота стены в долях клетки: лучи выше неё летят дальше.</summary>
    public double Height { get; }
}

/// <summary>Трассировка луча по сетке уровня (алгоритм DDA).</summary>
internal static class Raycaster
{
    /// <summary>
    ///     Трассирует луч, собирая ДО MaxHits стеновых сегментов от ближнего к дальнему.
    ///     Низкая стена (высота меньше единицы) записывается как попадание, но луч
    ///     продолжает лететь над ней — так видны стены за укрытиями и ограждениями.
    ///     Возвращает число записанных попаданий.
    /// </summary>
    public static int CastAll(
        Level level, double originX, double originY, double rayAngle, Span<WallHit> hits)
    {
        double directionX = Math.Cos(rayAngle);
        double directionY = Math.Sin(rayAngle);

        int mapX = (int)originX;
        int mapY = (int)originY;

        double deltaX = directionX == 0 ? 1e30 : Math.Abs(1.0 / directionX);
        double deltaY = directionY == 0 ? 1e30 : Math.Abs(1.0 / directionY);

        int stepX, stepY;
        double sideDistanceX, sideDistanceY;

        if (directionX < 0)
        {
            stepX = -1;
            sideDistanceX = (originX - mapX) * deltaX;
        }
        else
        {
            stepX = 1;
            sideDistanceX = (mapX + 1.0 - originX) * deltaX;
        }

        if (directionY < 0)
        {
            stepY = -1;
            sideDistanceY = (originY - mapY) * deltaY;
        }
        else
        {
            stepY = 1;
            sideDistanceY = (mapY + 1.0 - originY) * deltaY;
        }

        int count = 0;

        for (int step = 0; step < GameConfig.MaxRaySteps; step++)
        {
            int side;
            if (sideDistanceX < sideDistanceY)
            {
                sideDistanceX += deltaX;
                mapX += stepX;
                side = 0;
            }
            else
            {
                sideDistanceY += deltaY;
                mapY += stepY;
                side = 1;
            }

            if (!level.TryGetTile(mapX, mapY, out int tile))
                break;

            if (tile == 0)
                continue;

            double perpendicularDistance = side == 0
                ? (mapX - originX + (1 - stepX) / 2.0) / directionX
                : (mapY - originY + (1 - stepY) / 2.0) / directionY;

            if (perpendicularDistance < 0.01)
                perpendicularDistance = 0.01;

            double wallX = side == 0
                ? originY + perpendicularDistance * directionY
                : originX + perpendicularDistance * directionX;
            wallX -= Math.Floor(wallX);

            double height = Level.WallHeightOf(tile);
            hits[count++] = new WallHit(perpendicularDistance, side, tile, wallX, height);

            if (height >= 1.0 || count == hits.Length)
                break;
        }

        return count;
    }

    /// <summary>
    ///     Первая стена на пути луча любой высоты (для мгновенных выстрелов и рукопашной:
    ///     пуля упирается и в низкое укрытие).
    /// </summary>
    public static RayHit Cast(Level level, double originX, double originY, double rayAngle)
    {
        Span<WallHit> hits = stackalloc WallHit[1];
        int count = CastAll(level, originX, originY, rayAngle, hits);

        if (count == 0)
            return RayHit.Miss;

        WallHit hit = hits[0];
        double directionX = Math.Cos(rayAngle);
        double directionY = Math.Sin(rayAngle);

        // Точка попадания лежит на направлении взгляда (как и раньше — по дистанции луча).
        return new RayHit(
            hit.Distance, hit.Side, hit.TileType, hit.WallX,
            originX + hit.Distance * directionX,
            originY + hit.Distance * directionY);
    }
}

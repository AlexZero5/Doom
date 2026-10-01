using Doom.Configuration;
using Doom.World;

namespace Doom.Rendering;

/// <summary>Трассировка одного луча по сетке уровня (алгоритм DDA).</summary>
internal static class Raycaster
{
    public static RayHit Cast(Level level, double originX, double originY, double rayAngle)
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

        int side = 0;
        int tileType = 0;

        for (int step = 0; step < GameConfig.MaxRaySteps; step++)
        {
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

            if (tile > 0)
            {
                tileType = tile;
                break;
            }
        }

        if (tileType == 0)
            return RayHit.Miss;

        double perpendicularDistance = side == 0
            ? (mapX - originX + (1 - stepX) / 2.0) / directionX
            : (mapY - originY + (1 - stepY) / 2.0) / directionY;

        if (perpendicularDistance < 0.01)
            perpendicularDistance = 0.01;

        double wallX = side == 0
            ? originY + perpendicularDistance * directionY
            : originX + perpendicularDistance * directionX;

        // Distance — параметр луча, поэтому точка попадания лежит на направлении взгляда.
        double hitX = originX + perpendicularDistance * directionX;
        double hitY = originY + perpendicularDistance * directionY;

        wallX -= Math.Floor(wallX);

        return new RayHit(perpendicularDistance, side, tileType, wallX, hitX, hitY);
    }
}

using Doom.Configuration;
using Doom.Gameplay;
using Doom.World;

namespace Doom.Rendering;

/// <summary>Отрисовка кадра: фон, стены, спрайты, оружие и строка статуса.</summary>
internal sealed class SceneRenderer
{
    /// <summary>Минимальная ширина кадра для параллельного рендера колонок.</summary>
    private const int ParallelismThreshold = 80;

    private readonly List<SpriteQuad> _visibleSprites = new();
    private SpriteQuad[] _spriteCache = Array.Empty<SpriteQuad>();

    /// <summary>Рисует кадр: фон, стены, спрайты, засветку от выстрелов и взрывов, оружие и строку состояния.</summary>
    public void Render(
        Framebuffer framebuffer,
        Viewport viewport,
        Level level,
        Player player,
        IReadOnlyList<Pickup> pickups,
        ProjectileSystem projectiles,
        double nowSeconds,
        double flashIntensity = 0.0,
        int flashColor = 0)
    {
        FillBackground(framebuffer, viewport);

        _visibleSprites.Clear();
        SpriteProjector.ProjectPickups(viewport, player, pickups, _visibleSprites);
        SpriteProjector.ProjectDynamics(viewport, player, projectiles.Projectiles, projectiles.Impacts,
            _visibleSprites);
        _visibleSprites.Sort(SpriteQuad.CompareByDistance);

        if (_spriteCache.Length < _visibleSprites.Count)
            _spriteCache = new SpriteQuad[_visibleSprites.Count];

        _visibleSprites.CopyTo(_spriteCache);

        int spriteCount = _visibleSprites.Count;
        int raysPerColumn = viewport.RaysPerColumn;

        if (viewport.Columns < ParallelismThreshold)
        {
            for (int x = 0; x < viewport.Columns; x++)
                RenderColumn(x, framebuffer, viewport, level, player, raysPerColumn, spriteCount);
        }
        else
        {
            Parallel.For(0, viewport.Columns, x =>
                RenderColumn(x, framebuffer, viewport, level, player, raysPerColumn, spriteCount));
        }

        ApplyFlash(framebuffer, flashIntensity, flashColor);
        WeaponRenderer.Draw(framebuffer, viewport, player, nowSeconds);
        HudRenderer.Draw(framebuffer, player);
    }

    /// <summary>Засветка игрового поля от выстрела или взрыва; строка состояния не затрагивается.</summary>
    private static void ApplyFlash(Framebuffer framebuffer, double intensity, int color)
    {
        if (intensity <= 0.001)
            return;

        int cells = (framebuffer.Rows - 1) * framebuffer.Columns;

        for (int index = 0; index < cells; index++)
        {
            framebuffer.Foreground[index] = ColorRgb.Blend(framebuffer.Foreground[index], color, intensity);
            framebuffer.Background[index] = ColorRgb.Blend(framebuffer.Background[index], color, intensity);
        }
    }

    /// <summary>Заливает фон небом и полом, интерполируя градиенты по «пикселям» кадра.</summary>
    private static void FillBackground(Framebuffer framebuffer, Viewport viewport)
    {
        int horizon = viewport.PixelRows / 2;

        char[] chars = framebuffer.Chars;
        int[] foreground = framebuffer.Foreground;
        int[] background = framebuffer.Background;

        for (int y = 0; y < viewport.Rows; y++)
        {
            int topPixel = y * 2;
            int bottomPixel = y * 2 + 1;

            int topColor = topPixel < horizon
                ? EnvironmentPalette.SkyColor(topPixel, viewport.PixelRows)
                : EnvironmentPalette.FloorColor(topPixel, viewport.PixelRows);

            int bottomColor = bottomPixel < horizon
                ? EnvironmentPalette.SkyColor(bottomPixel, viewport.PixelRows)
                : EnvironmentPalette.FloorColor(bottomPixel, viewport.PixelRows);

            int rowBase = y * framebuffer.Columns;

            for (int x = 0; x < framebuffer.Columns; x++)
            {
                int index = rowBase + x;
                chars[index] = Framebuffer.UpperHalfBlock;
                foreground[index] = topColor;
                background[index] = bottomColor;
            }
        }
    }

    /// <summary>Трассирует колонку, рисует стену, затем — видимые перед ней спрайты.</summary>
    private void RenderColumn(
        int x,
        Framebuffer framebuffer,
        Viewport viewport,
        Level level,
        Player player,
        int raysPerColumn,
        int spriteCount)
    {
        double distanceSum = 0;
        double wallTextureSum = 0;
        int hits = 0;
        int hitTileType = 0;
        int side = 0;

        for (int sample = 0; sample < raysPerColumn; sample++)
        {
            double t = (x + (sample + 0.5) / raysPerColumn) / viewport.Columns;
            double rayAngle = player.Angle - GameConfig.HalfFieldOfView + GameConfig.FieldOfView * t;

            RayHit hit = Raycaster.Cast(level, player.X, player.Y, rayAngle);
            if (hit.TileType == 0)
                continue;

            distanceSum += hit.Distance;
            wallTextureSum += hit.WallX;
            side = hit.Side;
            hitTileType = hit.TileType;
            hits++;
        }

        char[] chars = framebuffer.Chars;
        int[] foreground = framebuffer.Foreground;
        int[] background = framebuffer.Background;

        double wallDistance = 1e30;

        if (hits > 0)
        {
            double distance = distanceSum / hits;
            double wallTextureX = wallTextureSum / hits;
            wallDistance = distance;

            (int baseR, int baseG, int baseB) = WallPalette.GetBaseColor(hitTileType);

            double wallHeight = viewport.PixelRows / distance;
            double wallTop = viewport.PixelRows / 2.0 - wallHeight / 2.0;
            double wallBottom = viewport.PixelRows / 2.0 + wallHeight / 2.0;

            for (int y = 0; y < viewport.Rows; y++)
            {
                int topPixel = y * 2;
                int bottomPixel = y * 2 + 1;

                double topCoverage = PixelCoverage(topPixel, wallTop, wallBottom);
                double bottomCoverage = PixelCoverage(bottomPixel, wallTop, wallBottom);
                if (topCoverage <= 0 && bottomCoverage <= 0)
                    continue;

                int index = y * viewport.Columns + x;
                chars[index] = Framebuffer.UpperHalfBlock;

                if (topCoverage > 0)
                {
                    int color = WallShader.Shade(
                        baseR, baseG, baseB, distance, side, wallTextureX, topPixel, wallTop, wallHeight);

                    foreground[index] = topCoverage >= 1
                        ? color
                        : ColorRgb.Blend(foreground[index], color, topCoverage);
                }

                if (bottomCoverage > 0)
                {
                    int color = WallShader.Shade(
                        baseR, baseG, baseB, distance, side, wallTextureX, bottomPixel, wallTop, wallHeight);

                    background[index] = bottomCoverage >= 1
                        ? color
                        : ColorRgb.Blend(background[index], color, bottomCoverage);
                }
            }
        }

        if (spriteCount == 0)
            return;

        for (int spriteIndex = 0; spriteIndex < spriteCount; spriteIndex++)
        {
            SpriteQuad sprite = _spriteCache[spriteIndex];

            if (sprite.PerpendicularDistance >= wallDistance)
                continue;

            if (x + 0.5 < sprite.MinX || x + 0.5 > sprite.MaxX)
                continue;

            for (int y = 0; y < viewport.Rows; y++)
            {
                int topPixel = y * 2;
                int bottomPixel = y * 2 + 1;

                double topCoverage = PixelCoverage(topPixel, sprite.TopY, sprite.BottomY);
                double bottomCoverage = PixelCoverage(bottomPixel, sprite.TopY, sprite.BottomY);
                if (topCoverage <= 0 && bottomCoverage <= 0)
                    continue;

                int index = y * viewport.Columns + x;
                chars[index] = Framebuffer.UpperHalfBlock;

                if (topCoverage > 0)
                {
                    double alpha = topCoverage * sprite.Alpha;
                    foreground[index] = alpha >= 1
                        ? sprite.Color
                        : ColorRgb.Blend(foreground[index], sprite.Color, alpha);
                }

                if (bottomCoverage > 0)
                {
                    double alpha = bottomCoverage * sprite.Alpha;
                    background[index] = alpha >= 1
                        ? sprite.Color
                        : ColorRgb.Blend(background[index], sprite.Color, alpha);
                }
            }
        }
    }

    /// <summary>Доля «пикселя», попавшая в вертикальный отрезок (сглаживание краёв).</summary>
    private static double PixelCoverage(int pixel, double top, double bottom)
    {
        double overlap = Math.Min(bottom, pixel + 1.0) - Math.Max(top, (double)pixel);
        return overlap <= 0 ? 0 : Math.Min(overlap, 1.0);
    }
}

using Doom.Assets;
using Doom.Configuration;
using Doom.Gameplay;
using Doom.World;

namespace Doom.Rendering;

/// <summary>Отрисовка кадра: небо, полы и потолки, стены, спрайты, оружие и строка статуса.</summary>
internal sealed class SceneRenderer
{
    /// <summary>Минимальная ширина кадра для параллельного рендера колонок.</summary>
    private const int ParallelismThreshold = 80;

    // Освещение полов и потолков — те же законы, что у стен и спрайтов.
    private const double LightRange = 7.0;
    private const double LightOffset = 2.0;
    private const double MinBrightness = 0.02;
    private const double Gamma = 1.0 / 1.6;

    /// <summary>Дальние половины кадров за пределами трассировки рисуются почти чёрными.</summary>
    private const double MaxSurfaceDistance = 64.0;

    private readonly List<SpriteQuad> _visibleSprites = new();
    private SpriteQuad[] _spriteCache = Array.Empty<SpriteQuad>();

    // Контекст кадра для кэшированного делегата Parallel.For (без аллокаций на кадр).
    private Framebuffer _frameBuffer = null!;
    private Viewport _viewport;
    private Level _level = null!;
    private Player _player = null!;
    private int _spriteCount;
    private readonly Action<int> _renderColumnAction;

    // Предвычисленное на кадр: одинаково для всех колонок.
    private double _horizon;
    private int _pixelRows;
    private double[] _floorRowDistance = Array.Empty<double>();
    private double[] _ceilingRowDistance = Array.Empty<double>();
    private double[] _floorBrightness = Array.Empty<double>();
    private double[] _ceilingBrightness = Array.Empty<double>();
    private double[] _skyTexV = Array.Empty<double>();

    // Кэш текстур полов/потолков на кадр: словарь ассетов слишком дорог на каждый пиксель.
    private readonly Texture?[] _floorTextureCache = new Texture?[16];
    private readonly Texture?[] _ceilingTextureCache = new Texture?[16];
    private readonly Texture?[] _wallTextureCache = new Texture?[16];
    private Texture? _skyboxTexture;

    public SceneRenderer(AssetStore? assets = null)
    {
        Assets = assets;
        _renderColumnAction = RenderColumnFromContext;
    }

    /// <summary>Хранилище текстур (может отсутствовать — тогда всё рисуется процедурно).</summary>
    public AssetStore? Assets { get; }

    /// <summary>Рисует кадр: небо, полы и потолки, стены, спрайты, засветку, оружие и статус.</summary>
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
        _frameBuffer = framebuffer;
        _viewport = viewport;
        _level = level;
        _player = player;
        _pixelRows = viewport.PixelRows;
        _horizon = viewport.HorizonPixels(player.Pitch);

        PrepareRowCaches();
        Array.Clear(_floorTextureCache);
        Array.Clear(_ceilingTextureCache);
        Array.Clear(_wallTextureCache);
        _skyboxTexture = Assets?.GetSkybox();

        _visibleSprites.Clear();
        SpriteProjector.ProjectPickups(viewport, player, pickups, _visibleSprites);
        SpriteProjector.ProjectDynamics(viewport, player, projectiles.Projectiles, projectiles.Impacts,
            _visibleSprites, Assets);
        _visibleSprites.Sort(SpriteQuad.CompareByDistance);

        if (_spriteCache.Length < _visibleSprites.Count)
            _spriteCache = new SpriteQuad[_visibleSprites.Count];

        _visibleSprites.CopyTo(_spriteCache);
        _spriteCount = _visibleSprites.Count;

        if (viewport.Columns < ParallelismThreshold)
        {
            for (int x = 0; x < viewport.Columns; x++)
                RenderColumn(x);
        }
        else
        {
            Parallel.For(0, viewport.Columns, _renderColumnAction);
        }

        ApplyFlash(framebuffer, flashIntensity, flashColor);
        WeaponRenderer.Draw(framebuffer, viewport, player, nowSeconds, Assets);
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

    /// <summary>
    ///     На кадр: расстояния и яркость строк пола/потолка (зависят только от вертикали),
    ///     V-координаты неба. Экономит тригонометрию в каждой колонке.
    /// </summary>
    private void PrepareRowCaches()
    {
        if (_floorRowDistance.Length < _pixelRows)
        {
            _floorRowDistance = new double[_pixelRows];
            _ceilingRowDistance = new double[_pixelRows];
            _floorBrightness = new double[_pixelRows];
            _ceilingBrightness = new double[_pixelRows];
            _skyTexV = new double[_pixelRows];
        }

        double focal = _viewport.FocalPixels;

        for (int pixel = 0; pixel < _pixelRows; pixel++)
        {
            double belowHorizon = pixel + 0.5 - _horizon;
            double aboveHorizon = _horizon - (pixel + 0.5);

            // Пол/потолок на высоте камеры (0.5): строка экрана <-> перпендикулярная дистанция.
            double floorDistance = belowHorizon > 0.5 ? 0.5 * _pixelRows / belowHorizon : 1e30;
            double ceilingDistance = aboveHorizon > 0.5 ? 0.5 * _pixelRows / aboveHorizon : 1e30;

            _floorRowDistance[pixel] = Math.Min(floorDistance, MaxSurfaceDistance);
            _ceilingRowDistance[pixel] = Math.Min(ceilingDistance, MaxSurfaceDistance);
            _floorBrightness[pixel] = BrightnessAt(_floorRowDistance[pixel]);
            _ceilingBrightness[pixel] = BrightnessAt(_ceilingRowDistance[pixel]);

            // Небо: вертикальный угол над горизонтом -> V экваторасибо текстуры.
            // Панорама занимает ВЕРХНЮЮ половину картинки: зенит сверху, горизонт в середине.
            double verticalAngle = Math.Atan(aboveHorizon / focal);
            double v = 0.5 * (1.0 - verticalAngle / (Math.PI / 2.0));
            _skyTexV[pixel] = Math.Clamp(v, 0.0, 0.5);
        }
    }

    private static double BrightnessAt(double distance)
    {
        double brightness = LightRange / (distance + LightOffset);
        if (brightness > 1) brightness = 1;
        if (brightness < MinBrightness) brightness = MinBrightness;
        return Math.Pow(brightness, Gamma);
    }

    /// <summary>Точка входа для Parallel.For: читает контекст кадра из полей.</summary>
    private void RenderColumnFromContext(int x) => RenderColumn(x);

    /// <summary>Трассирует колонку: небо/потолок, пол, стеновые сегменты, затем спрайты.</summary>
    private void RenderColumn(int x)
    {
        Framebuffer framebuffer = _frameBuffer;
        Viewport viewport = _viewport;
        Player player = _player;

        char[] chars = framebuffer.Chars;
        int[] foreground = framebuffer.Foreground;
        int[] background = framebuffer.Background;

        double rayAngle = player.Angle - viewport.HalfFieldOfViewRadians
                          + viewport.FieldOfViewRadians * ((x + 0.5) / viewport.Columns);
        double rayDirX = Math.Cos(rayAngle);
        double rayDirY = Math.Sin(rayAngle);

        Span<WallHit> hits = stackalloc WallHit[GameConfig.MaxWallHitsPerRay];
        int hitCount = Raycaster.CastAll(_level, player.X, player.Y, rayAngle, hits);

        // 1. Пол и потолок/небо на всю колонку — стены лягут поверх.
        // ВАЖНО: дистанцией пола/потолка считаем ПЕРПЕНДИКУЛЯРНУЮ (как у стен), без поправки
        // на «рыбий глаз». Иначе у краёв экрана потолок/пол перестают сходиться со стенами:
        // между крышей и стеной пролезает небо, пол «отрывается» от стен эллипсом.
        for (int y = 0; y < viewport.Rows; y++)
        {
            int index = y * viewport.Columns + x;
            chars[index] = Framebuffer.UpperHalfBlock;

            int topPixel = y * 2;
            int bottomPixel = y * 2 + 1;

            foreground[index] = topPixel + 0.5 < _horizon
                ? CeilingOrSky(topPixel, rayAngle, rayDirX, rayDirY)
                : FloorAtPixel(topPixel, rayDirX, rayDirY);

            background[index] = bottomPixel + 0.5 < _horizon
                ? CeilingOrSky(bottomPixel, rayAngle, rayDirX, rayDirY)
                : FloorAtPixel(bottomPixel, rayDirX, rayDirY);
        }

        // 2. Стены от дальних к ближним: низкая стена не закрывает то, что за ней.
        // Границы сегментов — локальные для колонки (рендер параллельный!).
        Span<double> slabDistance = stackalloc double[GameConfig.MaxWallHitsPerRay];
        Span<double> slabTop = stackalloc double[GameConfig.MaxWallHitsPerRay];
        Span<double> slabBottom = stackalloc double[GameConfig.MaxWallHitsPerRay];
        int slabCount = 0;

        for (int hitIndex = hitCount - 1; hitIndex >= 0; hitIndex--)
        {
            WallHit hit = hits[hitIndex];
            double screenTop = _horizon + _pixelRows * (0.5 - hit.Height) / hit.Distance;
            double screenBottom = _horizon + _pixelRows * 0.5 / hit.Distance;

            PaintWallSlab(x, hit, screenTop, screenBottom);

            if (slabCount < GameConfig.MaxWallHitsPerRay)
            {
                slabDistance[slabCount] = hit.Distance;
                slabTop[slabCount] = screenTop;
                slabBottom[slabCount] = screenBottom;
                slabCount++;
            }
        }

        // 3. Спрайты перед стенами этой колонки.
        if (_spriteCount == 0)
            return;

        for (int spriteIndex = 0; spriteIndex < _spriteCount; spriteIndex++)
        {
            SpriteQuad sprite = _spriteCache[spriteIndex];

            if (x + 0.5 < sprite.MinX || x + 0.5 > sprite.MaxX)
                continue;

            if (sprite.Texture is not null)
                RenderTexturedSpriteColumn(x, sprite, slabDistance, slabTop, slabBottom, slabCount);
            else
                RenderColoredSpriteColumn(x, sprite, slabDistance, slabTop, slabBottom, slabCount);
        }
    }

    // ============================================================
    //   Пол, потолок и небо
    // ============================================================

    /// <summary>Цвет пола в «пикселе»: текстура по мировым координатам с затенением по дистанции.</summary>
    private int FloorAtPixel(int pixel, double rayDirX, double rayDirY)
    {
        double rowDistance = _floorRowDistance[pixel];
        double brightness = _floorBrightness[pixel];

        double worldX = _player.X + rayDirX * rowDistance;
        double worldY = _player.Y + rayDirY * rowDistance;

        int floorType = _level.FloorAt((int)worldX, (int)worldY);
        (int baseR, int baseG, int baseB) = WallPalette.GetBaseColor(floorType);

        Texture? texture = FloorTexture(floorType);
        if (texture is null)
            return ColorRgb.Pack((int)(baseR * brightness), (int)(baseG * brightness), (int)(baseB * brightness));

        int texX = TextureX(worldX, texture);
        int texY = TextureX(worldY, texture);
        int argb = texture.SamplePixel(texX, texY);

        if (Texture.IsTransparent(argb))
            return ColorRgb.Pack((int)(baseR * brightness), (int)(baseG * brightness), (int)(baseB * brightness));

        return ColorRgb.Pack(
            (int)(((argb >>> 16) & 0xFF) * brightness),
            (int)(((argb >>> 8) & 0xFF) * brightness),
            (int)((argb & 0xFF) * brightness));
    }

    private Texture? FloorTexture(int floorType) =>
        floorType < _floorTextureCache.Length
            ? _floorTextureCache[floorType] ??= Assets?.GetFloor(floorType)
            : Assets?.GetFloor(floorType);

    /// <summary>
    ///     Цвет над горизонтом: если над точкой потолок — его текстура, если нет — небо.
    ///     Так из окна здания видно небо, а изнутри — потолок в стиле стен.
    /// </summary>
    private int CeilingOrSky(int pixel, double rayAngle, double rayDirX, double rayDirY)
    {
        double rowDistance = _ceilingRowDistance[pixel];
        double brightness = _ceilingBrightness[pixel];

        double worldX = _player.X + rayDirX * rowDistance;
        double worldY = _player.Y + rayDirY * rowDistance;

        int ceilingType = _level.CeilingAt((int)worldX, (int)worldY);
        if (ceilingType == 0)
            return SkyAtPixel(pixel, rayAngle);

        (int baseR, int baseG, int baseB) = WallPalette.GetBaseColor(ceilingType);

        // Потолок — текстурой стены здания.
        Texture? texture = CeilingTexture(ceilingType);
        if (texture is null)
            return ColorRgb.Pack((int)(baseR * brightness), (int)(baseG * brightness), (int)(baseB * brightness));

        int texX = TextureX(worldX, texture);
        int texY = TextureX(worldY, texture);
        int argb = texture.SamplePixel(texX, texY);

        if (Texture.IsTransparent(argb))
            return ColorRgb.Pack((int)(baseR * brightness), (int)(baseG * brightness), (int)(baseB * brightness));

        return ColorRgb.Pack(
            (int)(((argb >>> 16) & 0xFF) * brightness),
            (int)(((argb >>> 8) & 0xFF) * brightness),
            (int)((argb & 0xFF) * brightness));
    }

    private Texture? CeilingTexture(int ceilingType) =>
        ceilingType < _ceilingTextureCache.Length
            ? _ceilingTextureCache[ceilingType] ??= Assets?.GetWall(ceilingType)
            : Assets?.GetWall(ceilingType);

    private Texture? WallTexture(int wallType) =>
        wallType < _wallTextureCache.Length
            ? _wallTextureCache[wallType] ??= Assets?.GetWall(wallType)
            : Assets?.GetWall(wallType);

    /// <summary>Цвет неба: 360° панорама по углу луча, по вертикали — наклон камеры.</summary>
    private int SkyAtPixel(int pixel, double rayAngle)
    {
        Texture? skybox = _skyboxTexture;

        if (skybox is null)
            return EnvironmentPalette.SkyColor(pixel, _pixelRows);

        double u = rayAngle / Math.Tau;
        u -= Math.Floor(u);

        int texX = Math.Clamp((int)(u * skybox.Width), 0, skybox.Width - 1);
        int texY = Math.Clamp((int)(_skyTexV[pixel] * skybox.Height), 0, skybox.Height - 1);

        int argb = skybox.SamplePixel(texX, texY);

        // Чёрная половина панорамы не должна попасть в кадр.
        return Texture.IsTransparent(argb) || (argb & 0xFFFFFF) == 0
            ? EnvironmentPalette.SkyColor(pixel, _pixelRows)
            : argb | unchecked((int)0xFF000000);
    }

    private static int TextureX(double worldCoordinate, Texture texture) =>
        Math.Clamp((int)((worldCoordinate - Math.Floor(worldCoordinate)) * texture.Width), 0, texture.Width - 1);

    // ============================================================
    //   Стены (сегменты произвольной высоты, от дальних к ближним)
    // ============================================================

    private void PaintWallSlab(int x, WallHit hit, double screenTop, double screenBottom)
    {
        Framebuffer framebuffer = _frameBuffer;
        Viewport viewport = _viewport;

        char[] chars = framebuffer.Chars;
        int[] foreground = framebuffer.Foreground;
        int[] background = framebuffer.Background;

        double distance = hit.Distance;

        (int baseR, int baseG, int baseB) = WallPalette.GetBaseColor(Level.WallTypeOf(hit.TileType));
        Texture? wallTexture = WallTexture(Level.WallTypeOf(hit.TileType));

        var shader = new WallShader.ColumnShader(baseR, baseG, baseB, wallTexture, distance, hit.Side, hit.WallX);
        double scale = distance / _pixelRows;

        for (int y = 0; y < viewport.Rows; y++)
        {
            int topPixel = y * 2;
            int bottomPixel = y * 2 + 1;

            double topCoverage = PixelCoverage(topPixel, screenTop, screenBottom);
            double bottomCoverage = PixelCoverage(bottomPixel, screenTop, screenBottom);
            if (topCoverage <= 0 && bottomCoverage <= 0)
                continue;

            int index = y * viewport.Columns + x;
            chars[index] = Framebuffer.UpperHalfBlock;

            // Мировая высота краёв пикселя: кирпичи «продолжаются» по стенам любой высоты.
            if (topCoverage > 0)
            {
                int color = shader.ShadeSpan(
                    1.0 - WorldZAt(topPixel, scale),
                    1.0 - WorldZAt(topPixel + 1, scale));

                foreground[index] = topCoverage >= 1
                    ? color
                    : ColorRgb.Blend(foreground[index], color, topCoverage);
            }

            if (bottomCoverage > 0)
            {
                int color = shader.ShadeSpan(
                    1.0 - WorldZAt(bottomPixel, scale),
                    1.0 - WorldZAt(bottomPixel + 1, scale));

                background[index] = bottomCoverage >= 1
                    ? color
                    : ColorRgb.Blend(background[index], color, bottomCoverage);
            }
        }
    }

    /// <summary>Мировая высота точки экрана на стене с данной дистанцией (0 — пол, 1 — потолок уровня).</summary>
    private double WorldZAt(int pixel, double scale) => 0.5 - (pixel - _horizon) * scale;

    // ============================================================
    //   Спрайты
    // ============================================================

    /// <summary>Пиксель спрайта закрыт стеной этой колонки, стоящей ближе?</summary>
    private static bool HiddenByWall(
        ReadOnlySpan<double> slabDistance,
        ReadOnlySpan<double> slabTop,
        ReadOnlySpan<double> slabBottom,
        int slabCount,
        int pixel,
        double spriteDistance)
    {
        for (int slab = 0; slab < slabCount; slab++)
        {
            if (slabDistance[slab] < spriteDistance - 0.01
                && pixel + 0.5 >= slabTop[slab]
                && pixel + 0.5 <= slabBottom[slab])
                return true;
        }

        return false;
    }

    private void RenderColoredSpriteColumn(
        int x,
        SpriteQuad sprite,
        ReadOnlySpan<double> slabDistance,
        ReadOnlySpan<double> slabTop,
        ReadOnlySpan<double> slabBottom,
        int slabCount)
    {
        Framebuffer framebuffer = _frameBuffer;
        Viewport viewport = _viewport;

        char[] chars = framebuffer.Chars;
        int[] foreground = framebuffer.Foreground;
        int[] background = framebuffer.Background;

        for (int y = 0; y < viewport.Rows; y++)
        {
            int topPixel = y * 2;
            int bottomPixel = y * 2 + 1;

            bool topHidden = HiddenByWall(slabDistance, slabTop, slabBottom, slabCount, topPixel,
                sprite.PerpendicularDistance);
            bool bottomHidden = HiddenByWall(slabDistance, slabTop, slabBottom, slabCount, bottomPixel,
                sprite.PerpendicularDistance);

            if (topHidden && bottomHidden)
                continue;

            double topCoverage = PixelCoverage(topPixel, sprite.TopY, sprite.BottomY);
            double bottomCoverage = PixelCoverage(bottomPixel, sprite.TopY, sprite.BottomY);
            if (topCoverage <= 0 && bottomCoverage <= 0)
                continue;

            int index = y * viewport.Columns + x;
            chars[index] = Framebuffer.UpperHalfBlock;

            if (topCoverage > 0 && !topHidden)
            {
                double alpha = topCoverage * sprite.Alpha;
                foreground[index] = alpha >= 1
                    ? sprite.Color
                    : ColorRgb.Blend(foreground[index], sprite.Color, alpha);
            }

            if (bottomCoverage > 0 && !bottomHidden)
            {
                double alpha = bottomCoverage * sprite.Alpha;
                background[index] = alpha >= 1
                    ? sprite.Color
                    : ColorRgb.Blend(background[index], sprite.Color, alpha);
            }
        }
    }

    /// <summary>Колонка текстурированного спрайта: сэмплы по UV с затенением и альфой.</summary>
    private void RenderTexturedSpriteColumn(
        int x,
        SpriteQuad sprite,
        ReadOnlySpan<double> slabDistance,
        ReadOnlySpan<double> slabTop,
        ReadOnlySpan<double> slabBottom,
        int slabCount)
    {
        Framebuffer framebuffer = _frameBuffer;
        Viewport viewport = _viewport;
        Texture texture = sprite.Texture!;

        double quadWidth = sprite.MaxX - sprite.MinX;
        double quadHeight = sprite.BottomY - sprite.TopY;

        if (quadWidth <= 0.001 || quadHeight <= 0.001)
            return;

        double u = (x + 0.5 - sprite.MinX) / quadWidth;
        int texX = Math.Clamp((int)(u * texture.Width), 0, texture.Width - 1);

        char[] chars = framebuffer.Chars;
        int[] foreground = framebuffer.Foreground;
        int[] background = framebuffer.Background;

        for (int y = 0; y < viewport.Rows; y++)
        {
            int topPixel = y * 2;
            int bottomPixel = y * 2 + 1;

            bool topHidden = HiddenByWall(slabDistance, slabTop, slabBottom, slabCount, topPixel,
                sprite.PerpendicularDistance);
            bool bottomHidden = HiddenByWall(slabDistance, slabTop, slabBottom, slabCount, bottomPixel,
                sprite.PerpendicularDistance);

            if (topHidden && bottomHidden)
                continue;

            double topCoverage = PixelCoverage(topPixel, sprite.TopY, sprite.BottomY);
            double bottomCoverage = PixelCoverage(bottomPixel, sprite.TopY, sprite.BottomY);
            if (topCoverage <= 0 && bottomCoverage <= 0)
                continue;

            int index = y * viewport.Columns + x;
            chars[index] = Framebuffer.UpperHalfBlock;

            if (topCoverage > 0 && !topHidden)
            {
                int argb = SampleSprite(texture, texX, topPixel, sprite.TopY, quadHeight);
                if (!Texture.IsTransparent(argb))
                {
                    double alpha = topCoverage * sprite.Alpha * ((argb >>> 24) / 255.0);
                    int color = ShadeSpritePixel(argb, sprite.Brightness);
                    foreground[index] = alpha >= 1
                        ? color
                        : ColorRgb.Blend(foreground[index], color, alpha);
                }
            }

            if (bottomCoverage > 0 && !bottomHidden)
            {
                int argb = SampleSprite(texture, texX, bottomPixel, sprite.TopY, quadHeight);
                if (!Texture.IsTransparent(argb))
                {
                    double alpha = bottomCoverage * sprite.Alpha * ((argb >>> 24) / 255.0);
                    int color = ShadeSpritePixel(argb, sprite.Brightness);
                    background[index] = alpha >= 1
                        ? color
                        : ColorRgb.Blend(background[index], color, alpha);
                }
            }
        }
    }

    private static int SampleSprite(Texture texture, int texX, int pixelY, double quadTop, double quadHeight)
    {
        double v = (pixelY + 0.5 - quadTop) / quadHeight;
        int texY = Math.Clamp((int)(v * texture.Height), 0, texture.Height - 1);
        return texture.SamplePixel(texX, texY);
    }

    private static int ShadeSpritePixel(int argb, double brightness)
    {
        int r = Math.Clamp((int)(((argb >>> 16) & 0xFF) * brightness), 0, 255);
        int g = Math.Clamp((int)(((argb >>> 8) & 0xFF) * brightness), 0, 255);
        int b = Math.Clamp((int)((argb & 0xFF) * brightness), 0, 255);
        return ColorRgb.Pack(r, g, b);
    }

    /// <summary>Доля «пикселя», попавшая в вертикальный отрезок (сглаживание краёв).</summary>
    private static double PixelCoverage(int pixel, double top, double bottom)
    {
        double overlap = Math.Min(bottom, pixel + 1.0) - Math.Max(top, (double)pixel);
        return overlap <= 0 ? 0 : Math.Min(overlap, 1.0);
    }
}

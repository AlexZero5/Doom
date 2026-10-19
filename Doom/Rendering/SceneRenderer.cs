using Doom.Assets;
using Doom.Configuration;
using Doom.Gameplay;
using Doom.World;

namespace Doom.Rendering;

/// <summary>
///     Кадр целиком: трёхмерный вид рисуется во внутреннем кадре пониженного разрешения
///     (настройка «Разрешение»), растягивается на верх экрана, а поверх — засветка, оружие,
///     прицел и строка состояния уже в полном разрешении терминала: HUD и меню всегда чёткие.
/// </summary>
internal sealed class SceneRenderer
{
    /// <summary>Старые единицы эффектов и снарядов (1 = 2.6 м) → метры.</summary>
    private const double OldUnit = 2.6;

    private const int FallbackSkyTop = 0x3a5a8c;
    private const int FallbackSkyHorizon = 0xa8b8c8;

    private readonly Rasterizer _rasterizer = new();
    private readonly WeaponRenderer _weapon;
    private readonly HudRenderer _hud;
    private readonly List<Face> _scratch = new();
    private readonly List<Texture?> _frameTextures = new();
    private readonly Dictionary<Texture, int> _spriteIndex = new(ReferenceEqualityComparer.Instance);
    private readonly Func<int, int, int> _skyFunction;

    private Level? _texturesFor;
    private int _texturesVersion = -1;
    private int _levelTextureCount;
    private Texture? _sky;
    private Camera _camera;
    private int[] _columnMap = Array.Empty<int>();
    private int[] _rowMap = Array.Empty<int>();
    private int _colorMask = 0xFFFFFF;

    private static readonly string[] PickupSprites =
        Enum.GetValues<PickupKind>().Select(kind => "pickup_" + kind.ToString().ToLowerInvariant()).ToArray();

    public SceneRenderer(AssetStore? assets = null)
    {
        Assets = assets;
        _weapon = new WeaponRenderer(assets);
        _hud = new HudRenderer(assets);
        _skyFunction = SkyAt;
    }

    public AssetStore? Assets { get; }

    public HudRenderer Hud => _hud;

    /// <summary>Сколько треугольников ушло в растеризацию в последнем кадре (для замеров).</summary>
    public int LastTriangleCount => _rasterizer.TriangleCount;

    /// <summary>Размер внутреннего кадра трёхмерного вида для окна (для подписи в настройках).</summary>
    public static (int Width, int Height) InternalSize(int columns, int pixelRows, double scale) =>
        (Math.Max(16, (int)Math.Round(columns / scale)), Math.Max(8, (int)Math.Round(pixelRows / scale)));

    /// <summary>Рисует кадр целиком.</summary>
    public void Render(
        Framebuffer framebuffer,
        Viewport viewport,
        Level level,
        Player player,
        IReadOnlyList<Pickup> pickups,
        ProjectileSystem projectiles,
        double nowSeconds,
        double flashIntensity = 0.0,
        int flashColor = 0,
        MotionDetail motion = MotionDetail.Full)
    {
        PrepareTextures(level);

        // Динамическое разрешение: в движении внутренний кадр ещё меньше (деталей всё равно не
        // разглядеть), а цвет грубее — терминалу уходит заметно меньше байтов. Встали — полный кадр.
        double scale = GameSettings.Current.RenderScale * motion switch
        {
            MotionDetail.Soft => 1.35,
            MotionDetail.Strong => 2.0,
            _ => 1.0
        };
        _colorMask = motion switch { MotionDetail.Soft => 0xF8F8F8, MotionDetail.Strong => 0xF0F0F0, _ => 0xFFFFFF };

        (int width, int height) = InternalSize(viewport.Columns, viewport.PixelRows, Math.Min(scale, 6.0));
        _camera = new Camera(new Vec3(player.X, player.Y, player.EyeZ), player.Angle, player.Pitch,
            viewport.FieldOfViewRadians, width, height);

        // Сначала подвижное (двери, спрайты): их текстуры дописываются в таблицу кадра.
        _scratch.Clear();
        foreach (Door door in level.Doors)
            door.AppendFaces(_scratch);
        AddPickups(pickups);
        AddDecorations(level);
        AddDynamics(projectiles);

        _rasterizer.Begin(_camera, _frameTextures);
        AddLevel(level);
        foreach (Face face in _scratch)
            _rasterizer.AddFace(face);

        _rasterizer.Rasterize(_skyFunction);
        Present(framebuffer, viewport);

        ApplyFlash(framebuffer, viewport, flashIntensity, flashColor);
        _weapon.Draw(framebuffer, viewport, player, nowSeconds);
        if (GameSettings.Current.Crosshair)
            DrawCrosshair(framebuffer, viewport);
        _hud.Draw(framebuffer, viewport.Rows, player, nowSeconds);
    }

    // ============================================================
    //   Текстуры кадра: таблица уровня + спрайты этого кадра
    // ============================================================

    private void PrepareTextures(Level level)
    {
        int version = Assets?.Version ?? 0;
        if (!ReferenceEquals(level, _texturesFor) || version != _texturesVersion || _levelTextureCount != level.TextureNames.Count)
        {
            _frameTextures.Clear();
            _frameTextures.Add(null);
            for (int i = 1; i < level.TextureNames.Count; i++)
                _frameTextures.Add(Assets?.GetTexture(level.TextureNames[i]) ?? FallbackTexture(level.TextureNames[i]));

            _levelTextureCount = level.TextureNames.Count;
            _sky = Assets?.GetSky(level.Sky);
            _texturesFor = level;
            _texturesVersion = version;
        }

        // Спрайты прошлого кадра выбрасываются: таблица остаётся только уровня.
        _frameTextures.RemoveRange(_levelTextureCount, _frameTextures.Count - _levelTextureCount);
        _spriteIndex.Clear();
    }

    /// <summary>Нет картинки — серая «клетка», чтобы грань всё равно была видна.</summary>
    private static Texture FallbackTexture(string name)
    {
        var pixels = new int[8 * 8];
        int seed = name.Aggregate(17, (hash, c) => hash * 31 + c);
        int baseColor = 0x606060 + (seed & 0x1f) * 0x010101;
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = unchecked((int)0xFF000000) | (((i / 8 + i % 8) & 1) == 0 ? baseColor : baseColor - 0x101010);
        return new Texture(name, 8, 8, pixels);
    }

    private int SpriteTexture(Texture texture)
    {
        if (_spriteIndex.TryGetValue(texture, out int index))
            return index;

        index = _frameTextures.Count;
        _frameTextures.Add(texture);
        _spriteIndex[texture] = index;
        return index;
    }

    // ============================================================
    //   Уровень: куски, видимые камерой
    // ============================================================

    private void AddLevel(Level level)
    {
        double kx = (_camera.Width * 0.5 + 2) / _camera.Focal;
        double ky = (_camera.Height * 0.5 + 2) / _camera.Focal;
        List<Face> faces = level.Faces;

        foreach (FaceChunk chunk in level.Chunks)
        {
            if (!ChunkVisible(chunk, kx, ky))
                continue;

            foreach (int index in chunk.Faces)
                _rasterizer.AddFace(faces[index]);
        }
    }

    /// <summary>Отсечение по пирамиде видимости: все 8 углов габаритов за одной гранью — не видно.</summary>
    private bool ChunkVisible(FaceChunk chunk, double kx, double ky)
    {
        int outside = 0x1F;
        for (int corner = 0; corner < 8; corner++)
        {
            var point = new Vec3(
                (corner & 1) == 0 ? chunk.Min.X : chunk.Max.X,
                (corner & 2) == 0 ? chunk.Min.Y : chunk.Max.Y,
                (corner & 4) == 0 ? chunk.Min.Z : chunk.Max.Z);
            (double x, double y, double z) = _camera.ToView(point);

            int code = 0;
            if (z < 0.05) code |= 1;
            if (x < -z * kx) code |= 2;
            if (x > z * kx) code |= 4;
            if (y < -z * ky) code |= 8;
            if (y > z * ky) code |= 16;
            outside &= code;
            if (outside == 0)
                return true;
        }

        return outside == 0;
    }

    // ============================================================
    //   Спрайты: бонусы, декорации, снаряды, эффекты
    // ============================================================

    private void AddPickups(IReadOnlyList<Pickup> pickups)
    {
        foreach (Pickup pickup in pickups)
        {
            if (pickup.Taken)
                continue;

            string name = PickupSprites[(int)pickup.Kind];
            Texture? texture = Assets?.GetSprite(name);
            if (texture is null)
                continue;

            double ppu = Assets!.SpritePixelsPerUnit(name);
            AddUpright(texture, pickup.X, pickup.Y, pickup.Z, texture.Width / ppu, texture.Height / ppu);
        }
    }

    private void AddDecorations(Level level)
    {
        foreach (Decoration decoration in level.Decorations)
        {
            Texture? texture = Assets?.GetSprite(decoration.Sprite);
            if (texture is null)
                continue;

            double ppu = Assets!.SpritePixelsPerUnit(decoration.Sprite);
            AddUpright(texture, decoration.X, decoration.Y, decoration.Z, texture.Width / ppu, texture.Height / ppu);
        }
    }

    private void AddDynamics(ProjectileSystem projectiles)
    {
        foreach (Projectile projectile in projectiles.Projectiles)
        {
            if (!projectile.Alive)
                continue;

            Texture? frame = Assets?.GetAnimation(ProjectileAnimation(projectile.Kind))?.Sample(projectile.Age);
            if (frame is null)
                continue;

            double width = ProjectilePalette.Get(projectile.Kind).Width * 1.6 * OldUnit;
            AddFacing(frame, new Vec3(projectile.X, projectile.Y, projectile.Z), width, width * frame.Height / frame.Width,
                fullBright: true, mirrored: false);
        }

        foreach (Impact impact in projectiles.Impacts)
        {
            if (impact.IsExpired)
                continue;

            (string clip, bool glow) = ImpactAnimation(impact.Kind);
            Texture? frame = Assets?.GetAnimation(clip)?.SampleProgress(impact.Progress);
            if (frame is null)
                continue;

            ImpactVisual visual = ImpactPalette.Get(impact.Kind);
            double width = visual.Width * OldUnit * impact.Scale * (1.0 + (visual.Growth - 1.0) * impact.Progress);
            var center = new Vec3(impact.X, impact.Y, impact.Z + visual.Rise * OldUnit * impact.Age);
            AddFacing(frame, center, width, width * frame.Height / frame.Width, glow, impact.Scale > 1.0);
        }
    }

    private static string ProjectileAnimation(ProjectileKind kind) => kind switch
    {
        ProjectileKind.Rocket => "rocket",
        ProjectileKind.PlasmaBolt => "plasma_ball",
        _ => "bfg_ball"
    };

    /// <summary>Анимация эффекта и светится ли она сама.</summary>
    private static (string Clip, bool Glow) ImpactAnimation(ImpactKind kind) => kind switch
    {
        ImpactKind.BulletSpark or ImpactKind.MeleeSpark => ("puff", false),
        ImpactKind.RocketTrail or ImpactKind.ExplosionPuff => ("smoke", false),
        ImpactKind.PlasmaTrail or ImpactKind.PlasmaImpact => ("plasma_impact", true),
        ImpactKind.BfgTrail => ("bfg_ball", true),
        ImpactKind.BfgImpact => ("bfg_impact", true),
        _ => ("explosion", true)
    };

    /// <summary>Стоячий спрайт (дерево, бонус): вертикален, поворачивается к камере вокруг оси.</summary>
    private void AddUpright(Texture texture, double x, double y, double z, double width, double height)
    {
        Vec3 right = _camera.Right * (width * 0.5);
        var bottom = new Vec3(x, y, z);
        var top = new Vec3(x, y, z + height);
        AddSprite(texture, top - right, top + right, bottom + right, bottom - right, fullBright: false, mirrored: false);
    }

    /// <summary>Спрайт «лицом к камере» (снаряд, вспышка, дым).</summary>
    private void AddFacing(Texture texture, Vec3 center, double width, double height, bool fullBright, bool mirrored)
    {
        Vec3 right = _camera.Right * (width * 0.5), up = _camera.Up * (height * 0.5);
        AddSprite(texture, center - right + up, center + right + up, center + right - up, center - right - up, fullBright, mirrored);
    }

    private void AddSprite(Texture texture, Vec3 topLeft, Vec3 topRight, Vec3 bottomRight, Vec3 bottomLeft, bool fullBright,
        bool mirrored)
    {
        double u0 = mirrored ? 1 : 0, u1 = mirrored ? 0 : 1;
        FaceFlags flags = FaceFlags.AlphaTest | FaceFlags.Clamp | FaceFlags.DoubleSided | (fullBright ? FaceFlags.FullBright : 0);
        _scratch.Add(Face.Create(topLeft, topRight, bottomRight, bottomLeft,
            (u0, 0), (u1, 0), (u1, 0.999), (u0, 0.999), SpriteTexture(texture), -_camera.Forward, flags));
    }

    // ============================================================
    //   Небо
    // ============================================================

    /// <summary>Небо по направлению луча пикселя: панорама по курсу и высоте над горизонтом.</summary>
    private int SkyAt(int x, int y)
    {
        Vec3 ray = _camera.RayThrough(x + 0.5, y + 0.5);
        double horizontal = Math.Sqrt(ray.X * ray.X + ray.Y * ray.Y);
        double elevation = Math.Atan2(ray.Z, horizontal);
        Texture? sky = _sky;

        if (sky is null)
        {
            double t = Math.Clamp(elevation / (Math.PI / 2), 0, 1);
            return ColorRgb.Blend(FallbackSkyHorizon, FallbackSkyTop, t);
        }

        double u = Math.Atan2(ray.Y, ray.X) / Math.Tau;
        u -= Math.Floor(u);
        double v = Math.Clamp(1.0 - elevation / (Math.PI / 2), 0.0, 0.999);
        int argb = sky.Pixels[(int)(v * sky.Height) * sky.Width + Math.Min(sky.Width - 1, (int)(u * sky.Width))];

        // Ниже горизонта (видно только за краем мира) — затемнённая дымка горизонта.
        return elevation < 0 ? ColorRgb.Blend(argb & 0xFFFFFF, 0x202428, Math.Min(1, -elevation * 3)) : argb & 0xFFFFFF;
    }

    // ============================================================
    //   Вывод внутреннего кадра на экран
    // ============================================================

    /// <summary>Растягивает внутренний кадр на строки вида (ближайший пиксель).</summary>
    private void Present(Framebuffer framebuffer, Viewport viewport)
    {
        int columns = viewport.Columns, pixelRows = viewport.PixelRows;
        int width = _rasterizer.Width, height = _rasterizer.Height;

        if (_columnMap.Length != columns || _columnMap.Length > 0 && _columnMap[^1] >= width)
            _columnMap = new int[columns];
        for (int x = 0; x < columns; x++)
            _columnMap[x] = Math.Min(width - 1, x * width / columns);

        // Если внутренний кадр не выше числа строк терминала, обе половинки ячейки берут одну
        // строку: ячейка одноцветная, и терминалу хватает одной смены цвета (меньше байтов).
        if (_rowMap.Length != pixelRows)
            _rowMap = new int[pixelRows];
        bool wholeCells = height <= viewport.Rows;
        for (int y = 0; y < pixelRows; y++)
            _rowMap[y] = wholeCells ? Math.Min(height - 1, (y >> 1) * height / viewport.Rows) : Math.Min(height - 1, y * height / pixelRows);

        int[] source = _rasterizer.Color;
        int mask = _colorMask;
        for (int pixel = 0; pixel < pixelRows; pixel++)
        {
            int rowBase = (pixel >> 1) * framebuffer.Columns;
            int[] target = (pixel & 1) == 0 ? framebuffer.Foreground : framebuffer.Background;
            int sourceRow = _rowMap[pixel] * width;
            for (int x = 0; x < columns; x++)
                target[rowBase + x] = source[sourceRow + _columnMap[x]] & mask;
        }

        Array.Fill(framebuffer.Chars, Framebuffer.UpperHalfBlock, 0, viewport.Rows * framebuffer.Columns);
    }

    /// <summary>Засветка трёхмерного вида от выстрела или взрыва.</summary>
    private static void ApplyFlash(Framebuffer framebuffer, Viewport viewport, double intensity, int color)
    {
        if (intensity <= 0.001)
            return;

        int cells = viewport.Rows * framebuffer.Columns;
        int[] foreground = framebuffer.Foreground;
        int[] background = framebuffer.Background;

        for (int index = 0; index < cells; index++)
        {
            foreground[index] = ColorRgb.Blend(foreground[index], color, intensity);
            background[index] = ColorRgb.Blend(background[index], color, intensity);
        }
    }

    /// <summary>Маленький прицел в центре вида: светлая точка с тёмной обводкой.</summary>
    private static void DrawCrosshair(Framebuffer framebuffer, Viewport viewport)
    {
        int cx = viewport.Columns / 2, cy = viewport.PixelRows / 2;
        int size = Math.Max(1, viewport.PixelRows / 120);

        for (int dy = -size - 1; dy <= size; dy++)
        for (int dx = -size - 1; dx <= size; dx++)
        {
            bool core = dx >= -size && dx < size && dy >= -size && dy < size;
            bool cross = (dx >= -size && dx < size) || (dy >= -size && dy < size);
            if (!cross)
                continue;

            Plot(framebuffer, cx + dx, cy + dy, core ? 0xF0F0E0 : 0x101010);
        }
    }

    private static void Plot(Framebuffer framebuffer, int x, int pixel, int color)
    {
        if (x < 0 || x >= framebuffer.Columns || pixel < 0 || pixel >= framebuffer.Rows * 2)
            return;

        int index = (pixel >> 1) * framebuffer.Columns + x;
        ((pixel & 1) == 0 ? framebuffer.Foreground : framebuffer.Background)[index] = color;
        framebuffer.Chars[index] = Framebuffer.UpperHalfBlock;
    }
}

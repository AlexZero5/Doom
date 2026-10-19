using System.Globalization;
using System.Text.Json;
using Doom.Configuration;

namespace Doom.Assets;

/// <summary>
///     Хранилище ассетов. Вся папка assets/ сканируется при старте: каждый PNG становится
///     текстурой с ключом «путь без расширения» (<c>textures/brick_red</c>, <c>weapons/pistol/fire1</c>),
///     папки anims/&lt;клип&gt;/ — анимациями. Декодированные PNG сбрасываются в бинарный кэш
///     assets/.cache/ — при следующем запуске он читается напрямую. Пока игра запущена,
///     FileSystemWatcher следит за папкой: перерисованный PNG подхватывается на лету.
/// </summary>
/// <remarks>Всю графику генерирует tools/AssetForge.</remarks>
internal sealed class AssetStore : IDisposable
{
    private const uint CacheMagic = 0x4658_5444; // "FDTX"

    private const int CacheHeaderSize = 4 + 1 + 4 + 4 + 8 + 8;

    private readonly Dictionary<string, Texture> _textures = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, AnimationClip> _animations = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, double> _spriteScale = new(StringComparer.OrdinalIgnoreCase);

    private readonly object _pendingGate = new();
    private readonly Queue<string> _pendingChanges = new();

    private FileSystemWatcher? _watcher;

    /// <summary>Абсолютный путь к корню ассетов.</summary>
    public string RootPath { get; private set; } = string.Empty;

    /// <summary>Растёт при каждой перезагрузке: по нему кэши рендера понимают, что пора обновиться.</summary>
    public int Version { get; private set; }

    // ============================================================
    //   Запросы
    // ============================================================

    /// <summary>Текстура по ключу «путь без расширения» (например <c>textures/brick_red</c>).</summary>
    public Texture? Get(string key) => _textures.GetValueOrDefault(key);

    /// <summary>Материал стены, пола, потолка или 3D-объекта: assets/textures/&lt;имя&gt;.png.</summary>
    public Texture? GetTexture(string name) => Get("textures/" + name);

    /// <summary>Небо: панорама assets/sky/&lt;имя&gt;.png (зенит сверху, горизонт снизу).</summary>
    public Texture? GetSky(string name) => Get("sky/" + name);

    /// <summary>Спрайт бонуса или декорации: assets/sprites/&lt;имя&gt;.png.</summary>
    public Texture? GetSprite(string name) => Get("sprites/" + name);

    /// <summary>Кадр оружия: assets/weapons/&lt;оружие&gt;/&lt;кадр&gt;.png.</summary>
    public Texture? GetWeaponFrame(string weapon, string frame) => Get($"weapons/{weapon}/{frame}");

    /// <summary>Элемент интерфейса: assets/ui/&lt;имя&gt;.png.</summary>
    public Texture? GetUi(string name) => Get("ui/" + name);

    /// <summary>Элемент строки состояния: assets/hud/&lt;имя&gt;.png.</summary>
    public Texture? GetHud(string name) => Get("hud/" + name);

    /// <summary>Анимационный клип: assets/anims/&lt;имя&gt;/.</summary>
    public AnimationClip? GetAnimation(string name) => _animations.GetValueOrDefault(name);

    /// <summary>Сколько пикселей спрайта приходится на единицу мира (из sprites/sprites.txt).</summary>
    public double SpritePixelsPerUnit(string name) => _spriteScale.GetValueOrDefault(name, 32);

    /// <summary>Текстовый файл из assets/ (описание шрифта, карты) или null.</summary>
    public string? ReadText(string relativePath)
    {
        try
        {
            string path = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    // ============================================================
    //   Жизненный цикл
    // ============================================================

    /// <summary>Находит папку ассетов, загружает всё и включает слежение.</summary>
    public void Load()
    {
        ResolveRoot();
        Scan();
        StartWatcher();
        WriteDiagnostics();
    }

    private void WriteDiagnostics()
    {
        Console.WriteLine($"[assets] root={RootPath} textures={_textures.Count} anims={_animations.Count}");

        try
        {
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "assets_last_run.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | cwd={Environment.CurrentDirectory} | root={RootPath} | " +
                $"textures={_textures.Count} | anims={_animations.Count}{Environment.NewLine}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Лог не критичен.
        }
    }

    /// <summary>
    ///     Применяет накопившиеся изменения файлов (вызывать из главного цикла, не во время
    ///     рендера). Возвращает <c>true</c>, если что-то перезагрузилось и кадр надо перерисовать.
    /// </summary>
    public bool Poll()
    {
        string[] changes;

        lock (_pendingGate)
        {
            if (_pendingChanges.Count == 0)
                return false;

            changes = _pendingChanges.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            _pendingChanges.Clear();
        }

        foreach (string relativePath in changes)
            ReloadPath(relativePath);

        Version++;
        return true;
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _watcher = null;
    }

    /// <summary>Ищет assets/ вверх по дереву от рабочего каталога и папки с exe.</summary>
    private void ResolveRoot()
    {
        RootPath = FindUpwards(Directory.GetCurrentDirectory())
                   ?? FindUpwards(AppContext.BaseDirectory)
                   ?? Path.Combine(Directory.GetCurrentDirectory(), GameConfig.AssetsFolderName);

        try
        {
            Directory.CreateDirectory(CacheDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Без кэша тоже работаем — просто декодируем PNG каждый раз.
        }
    }

    private string CacheDirectory => Path.Combine(RootPath, GameConfig.AssetCacheFolderName);

    private static string? FindUpwards(string? start)
    {
        for (int depth = 0; depth < 6 && start is not null; depth++)
        {
            string candidate = Path.Combine(start, GameConfig.AssetsFolderName);

            // Папка должна быть похожа на ассеты: одноимённый каталог с кодом (Doom/Assets —
            // Windows не различает регистр) не должен засчитаться.
            if (Directory.Exists(candidate) && Directory.Exists(Path.Combine(candidate, "textures")))
                return candidate;

            start = Path.GetDirectoryName(start.TrimEnd(Path.DirectorySeparatorChar));
        }

        return null;
    }

    // ============================================================
    //   Загрузка
    // ============================================================

    private void Scan()
    {
        foreach (string file in SafeFiles(RootPath, "*.png", SearchOption.AllDirectories))
        {
            string relative = ToRelative(file);
            if (!relative.StartsWith(GameConfig.AssetCacheFolderName + "/", StringComparison.OrdinalIgnoreCase))
                LoadTexture(relative);
        }

        foreach (string directory in SafeDirectories(Path.Combine(RootPath, "anims")))
            BuildAnimation(Path.GetFileName(directory));

        LoadSpriteScale();
    }

    /// <summary>Ключ текстуры: путь без расширения через «/».</summary>
    private static string KeyOf(string relativePath) =>
        Path.ChangeExtension(relativePath, null)!.Replace('\\', '/');

    /// <summary>Декодирует (или берёт из кэша) один PNG. false — файл битый или пропал.</summary>
    private bool LoadTexture(string relativePath)
    {
        string fullPath = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        string key = KeyOf(relativePath);

        if (!File.Exists(fullPath))
        {
            _textures.Remove(key);
            return false;
        }

        byte[] source;
        long ticks;

        try
        {
            source = File.ReadAllBytes(fullPath);
            ticks = File.GetLastWriteTimeUtc(fullPath).Ticks;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false; // файл занят редактором — дождёмся следующего события
        }

        string cachePath = CachePathFor(relativePath);
        Texture? texture = TryReadCache(cachePath, ticks, source.Length);

        if (texture is null)
        {
            if (!PngDecoder.TryDecode(source, out Texture? decoded, out string error))
            {
                Console.WriteLine($"[assets] {relativePath}: пропущен — {error}");
                return false;
            }

            texture = decoded!;
            WriteCache(cachePath, texture, ticks, source.Length);
        }

        texture.Name = key;
        _textures[key] = texture;
        return true;
    }

    /// <summary>Собирает клип анимации из папки assets/anims/&lt;имя&gt;/ (кадры 0.png, 1.png…).</summary>
    private void BuildAnimation(string clipName)
    {
        string directory = Path.Combine(RootPath, "anims", clipName);

        if (!Directory.Exists(directory))
        {
            _animations.Remove(clipName);
            return;
        }

        var frames = new List<Texture>();
        foreach (string file in SafeFiles(directory, "*.png", SearchOption.TopDirectoryOnly).OrderBy(NaturalOrderKey))
        {
            string relative = ToRelative(file);
            if (_textures.TryGetValue(KeyOf(relative), out Texture? texture) || LoadTexture(relative) &&
                _textures.TryGetValue(KeyOf(relative), out texture))
                frames.Add(texture);
        }

        if (frames.Count == 0)
        {
            _animations.Remove(clipName);
            return;
        }

        (double frameTime, bool loop) = ReadManifest(directory);
        _animations[clipName] = new AnimationClip(clipName, frames.ToArray(), frameTime, loop);
    }

    /// <summary>manifest.json рядом с кадрами: {"frameTime": 0.1, "loop": false}.</summary>
    private static (double FrameTime, bool Loop) ReadManifest(string directory)
    {
        double frameTime = GameConfig.DefaultAnimationFrameTime;
        bool loop = true;

        try
        {
            string path = Path.Combine(directory, "manifest.json");
            if (!File.Exists(path))
                return (frameTime, loop);

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = document.RootElement;

            if (root.TryGetProperty("frameTime", out JsonElement time) && time.TryGetDouble(out double value))
                frameTime = value;

            if (root.TryGetProperty("loop", out JsonElement loopElement))
                loop = loopElement.ValueKind == JsonValueKind.True;
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            // Битый манифест не валит загрузку — берутся значения по умолчанию.
        }

        return (frameTime, loop);
    }

    /// <summary>sprites/sprites.txt: «имя пикселей_на_метр» — масштаб спрайтов в мире.</summary>
    private void LoadSpriteScale()
    {
        _spriteScale.Clear();
        string? text = ReadText("sprites/sprites.txt");
        if (text is null)
            return;

        foreach (string line in text.Split('\n'))
        {
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 2 && !parts[0].StartsWith('#')
                                  && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double ppu) && ppu > 0)
                _spriteScale[parts[0]] = ppu;
        }
    }

    private static string NaturalOrderKey(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        return int.TryParse(name, out int number) ? number.ToString("D8") : name;
    }

    // ============================================================
    //   Бинарный кэш
    // ============================================================

    private string CachePathFor(string relativePath) =>
        Path.Combine(CacheDirectory, Path.ChangeExtension(relativePath.Replace('/', '_').Replace('\\', '_'), ".tex"));

    private static Texture? TryReadCache(string cachePath, long ticks, long length)
    {
        try
        {
            if (!File.Exists(cachePath))
                return null;

            byte[] data = File.ReadAllBytes(cachePath);
            if (data.Length < CacheHeaderSize || BitConverter.ToUInt32(data, 0) != CacheMagic || data[4] != 1)
                return null;

            int width = BitConverter.ToInt32(data, 5);
            int height = BitConverter.ToInt32(data, 9);
            long cacheTicks = BitConverter.ToInt64(data, 13);
            long cacheLength = BitConverter.ToInt64(data, 21);

            if (cacheTicks != ticks || cacheLength != length || width < 1 || height < 1
                || width > 8192 || height > 8192 || data.Length < CacheHeaderSize + (long)width * height * 4)
                return null;

            var pixels = new int[width * height];
            Buffer.BlockCopy(data, CacheHeaderSize, pixels, 0, pixels.Length * 4);
            return new Texture(string.Empty, width, height, pixels);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void WriteCache(string cachePath, Texture texture, long ticks, long length)
    {
        try
        {
            using var stream = new FileStream(cachePath, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(stream);

            writer.Write(CacheMagic);
            writer.Write((byte)1);
            writer.Write(texture.Width);
            writer.Write(texture.Height);
            writer.Write(ticks);
            writer.Write(length);

            byte[] pixelBytes = new byte[texture.Pixels.Length * 4];
            Buffer.BlockCopy(texture.Pixels, 0, pixelBytes, 0, pixelBytes.Length);
            writer.Write(pixelBytes);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Кэш не критичен: не записался — декодируем PNG в следующий раз.
        }
    }

    // ============================================================
    //   Горячая перезагрузка
    // ============================================================

    private void StartWatcher()
    {
        try
        {
            _watcher = new FileSystemWatcher(RootPath)
            {
                IncludeSubdirectories = true,
                InternalBufferSize = 64 * 1024,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName
            };

            _watcher.Changed += (_, args) => EnqueueRelative(args.FullPath);
            _watcher.Created += (_, args) => EnqueueRelative(args.FullPath);
            _watcher.Deleted += (_, args) => EnqueueRelative(args.FullPath);
            _watcher.Renamed += (_, args) =>
            {
                EnqueueRelative(args.OldFullPath);
                EnqueueRelative(args.FullPath);
            };

            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception exception) when (exception is IOException or ArgumentException or UnauthorizedAccessException)
        {
            // Слежение не завелось (сетевой диск и т.п.) — игра работает без горячей перезагрузки.
        }
    }

    private void EnqueueRelative(string fullPath)
    {
        string relativePath = ToRelative(fullPath);
        if (relativePath.StartsWith(GameConfig.AssetCacheFolderName, StringComparison.OrdinalIgnoreCase))
            return;

        lock (_pendingGate)
            _pendingChanges.Enqueue(relativePath);
    }

    private void ReloadPath(string relativePath)
    {
        string extension = Path.GetExtension(relativePath);
        bool isPng = extension.Equals(".png", StringComparison.OrdinalIgnoreCase);

        if (isPng)
            LoadTexture(relativePath);

        if (relativePath.StartsWith("anims/", StringComparison.OrdinalIgnoreCase))
        {
            string[] parts = relativePath.Split('/');
            if (parts.Length > 1 && parts[1].Length > 0)
                BuildAnimation(parts[1]);
        }

        if (relativePath.Equals("sprites/sprites.txt", StringComparison.OrdinalIgnoreCase))
            LoadSpriteScale();
    }

    // ============================================================
    //   Мелочи
    // ============================================================

    private string ToRelative(string fullPath) => Path.GetRelativePath(RootPath, fullPath).Replace('\\', '/');

    private static IEnumerable<string> SafeFiles(string directory, string pattern, SearchOption option)
    {
        if (!Directory.Exists(directory))
            return Array.Empty<string>();

        try
        {
            return Directory.GetFiles(directory, pattern, option);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    private static IEnumerable<string> SafeDirectories(string directory)
    {
        if (!Directory.Exists(directory))
            return Array.Empty<string>();

        try
        {
            return Directory.GetDirectories(directory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }
}

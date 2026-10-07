using System.Text.Json;
using Doom.Configuration;

namespace Doom.Assets;

/// <summary>
///     Хранилище ассетов. Папка assets/ (walls/, weapons/, sprites/, anims/) сканируется
///     при старте; каждый PNG декодируется в текстуру и сбрасывается в бинарный кэш
///     assets/.cache/*.tex — при следующем запуске кэш читается напрямую, без декодирования
///     (инвалидация по времени и размеру исходника). Пока игра запущена, FileSystemWatcher
///     следит за папкой: перекрасил PNG в редакторе — текстура в игре обновилась сама.
/// </summary>
internal sealed class AssetStore : IDisposable
{
    private const uint CacheMagic = 0x4658_5444; // "FDTX"

    private const int CacheHeaderSize = 4 + 1 + 4 + 4 + 8 + 8;

    // Категории с плоским списком PNG (кроме anims — там папка = клип).
    private static readonly string[] FlatCategories = { "walls", "weapons", "sprites", "fonts" };

    private readonly Dictionary<string, Texture> _textures = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, AnimationClip> _animations = new(StringComparer.OrdinalIgnoreCase);

    private readonly object _pendingGate = new();
    private readonly Queue<string> _pendingChanges = new();

    private FileSystemWatcher? _watcher;

    /// <summary>Абсолютный путь к корню ассетов (папка создаётся при отсутствии).</summary>
    public string RootPath { get; private set; } = string.Empty;

    /// <summary>Сколько текстур загружено (для диагностики).</summary>
    public int TextureCount => _textures.Count;

    /// <summary>Сколько анимационных клипов загружено.</summary>
    public int AnimationCount => _animations.Count;

    // ============================================================
    //   Запросы из рендера
    // ============================================================

    /// <summary>Текстура стены по типу клетки: assets/walls/&lt;тип&gt;.png.</summary>
    public Texture? GetWall(int tileType) => Get($"walls/{tileType}.png");

    /// <summary>Текстура пола: тоже стены (6 — трава, 7 — металл, 8 — земля).</summary>
    public Texture? GetFloor(int floorType) => Get($"walls/{floorType}.png");

    /// <summary>Скайбокс: 360° панорама в assets/walls/9.png (рисунок в верхней половине).</summary>
    public Texture? GetSkybox() => Get("walls/9.png");

    /// <summary>Текстура оружия: assets/weapons/&lt;имя&gt;.png.</summary>
    public Texture? GetWeapon(string name) => Get($"weapons/{name}.png");

    /// <summary>Текстура спрайта: assets/sprites/&lt;имя&gt;.png.</summary>
    public Texture? GetSprite(string name) => Get($"sprites/{name}.png");

    /// <summary>Текстурная заливка шрифта: assets/fonts/&lt;имя&gt;.png.</summary>
    public Texture? GetFont(string name) => Get($"fonts/{name}.png");

    /// <summary>Анимационный клип: assets/anims/&lt;имя&gt;/.</summary>
    public AnimationClip? GetAnimation(string name) =>
        _animations.GetValueOrDefault(name.ToLowerInvariant());

    private Texture? Get(string relativePath) =>
        _textures.GetValueOrDefault(relativePath.Replace('\\', '/'));

    // ============================================================
    //   Жизненный цикл
    // ============================================================

    /// <summary>Находит (или создаёт) папку ассетов, загружает всё и включает слежение.</summary>
    public void Load()
    {
        ResolveRoot();
        Scan();
        StartWatcher();
        WriteDiagnostics();
    }

    /// <summary>
    ///     Диагностика запуска: куда разрешился корень ассетов и что загрузилось.
    ///     Пишется в консоль и в файл рядом с exe — по нему видно, почему игра
    ///     вдруг рисует процедурщину вместо текстур.
    /// </summary>
    private void WriteDiagnostics()
    {
        Console.WriteLine(
            $"[assets] root={RootPath} textures={_textures.Count} anims={_animations.Count}");

        try
        {
            string line =
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | cwd={Environment.CurrentDirectory} | " +
                $"root={RootPath} | textures={_textures.Count} | anims={_animations.Count}{Environment.NewLine}";

            File.AppendAllText(
                Path.Combine(AppContext.BaseDirectory, "assets_last_run.log"), line);
        }
        catch (IOException)
        {
            // Лог не критичен.
        }
    }

    /// <summary>Применяет накопившиеся изменения файлов (вызывать из главного цикла).</summary>
    public void Poll()
    {
        lock (_pendingGate)
        {
            if (_pendingChanges.Count == 0)
                return;

            while (_pendingChanges.Count > 0)
            {
                string relativePath = _pendingChanges.Dequeue();
                ReloadPath(relativePath);
            }
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _watcher = null;
    }

    /// <summary>
    ///     Ищет assets/ вверх по дереву от рабочего каталога и папки с exe.
    ///     Найденный корень не мутируется: структура папок создаётся только когда
    ///     настоящего корня нет и мы заводим новый рядом с рабочим каталогом
    ///     (иначе однажды созданные пустые категории в чужой папке заставляют
    ///     последующие запуски считать её корнем).
    /// </summary>
    private void ResolveRoot()
    {
        string? root = FindUpwards(Directory.GetCurrentDirectory())
                       ?? FindUpwards(AppContext.BaseDirectory);

        if (root is not null)
        {
            RootPath = root;
            Directory.CreateDirectory(CacheDirectory);
            return;
        }

        RootPath = Path.Combine(Directory.GetCurrentDirectory(), GameConfig.AssetsFolderName);
        Directory.CreateDirectory(RootPath);

        foreach (string category in FlatCategories)
            Directory.CreateDirectory(Path.Combine(RootPath, category));

        Directory.CreateDirectory(Path.Combine(RootPath, "anims"));
        Directory.CreateDirectory(CacheDirectory);
    }

    private string CacheDirectory => Path.Combine(RootPath, GameConfig.AssetCacheFolderName);

    private static string? FindUpwards(string? start)
    {
        for (int depth = 0; depth < 6 && start is not null; depth++)
        {
            string candidate = Path.Combine(start, GameConfig.AssetsFolderName);

            // Папка должна быть ПОХОЖА на ассеты. Иначе мимо неё путь продолжается:
            // случайный одноимённый каталог (например, Doom/Assets/ с кодом — Windows
            // не различает регистр) не должен засчитаться и обнулить загрузку.
            if (Directory.Exists(candidate) && LooksLikeAssetsRoot(candidate))
                return candidate;

            start = Path.GetDirectoryName(start.TrimEnd(Path.DirectorySeparatorChar));
        }

        return null;
    }

    /// <summary>Правда ли в папке есть категории ассетов или хотя бы один PNG.</summary>
    private static bool LooksLikeAssetsRoot(string path)
    {
        foreach (string category in new[] { "walls", "weapons", "sprites", "anims", "fonts" })
        {
            if (Directory.Exists(Path.Combine(path, category)))
                return true;
        }

        try
        {
            return Directory.EnumerateFiles(path, "*.png", SearchOption.TopDirectoryOnly).Any();
        }
        catch (IOException)
        {
            return false;
        }
    }

    // ============================================================
    //   Загрузка
    // ============================================================

    private void Scan()
    {
        foreach (string category in FlatCategories)
        {
            string directory = Path.Combine(RootPath, category);
            foreach (string file in SafeFiles(directory, "*.png"))
                LoadTexture(ToRelative(file));
        }

        string animsDirectory = Path.Combine(RootPath, "anims");
        foreach (string directory in SafeDirectories(animsDirectory))
            BuildAnimation(Path.GetFileName(directory));
    }

    /// <summary>Декодирует (или берёт из кэша) один PNG и кладёт в словарь. false — файл битый.</summary>
    private bool LoadTexture(string relativePath)
    {
        string fullPath = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(fullPath))
        {
            _textures.Remove(relativePath);
            return false;
        }

        byte[] source;
        long ticks, length;

        try
        {
            source = File.ReadAllBytes(fullPath);
            ticks = File.GetLastWriteTimeUtc(fullPath).Ticks;
            length = source.Length;
        }
        catch (IOException)
        {
            return false; // файл занят редактором — дождёмся следующего события
        }

        // Кэш валиден, если совпадают magic, версия, размеры, время и длина исходника.
        string cachePath = CachePathFor(relativePath);
        Texture? texture = TryReadCache(cachePath, ticks, length);

        if (texture is null)
        {
            if (!PngDecoder.TryDecode(source, out Texture? decoded, out string error))
            {
                Console.WriteLine($"[assets] {relativePath}: пропущен — {error}");
                return false;
            }

            texture = decoded!;
            WriteCache(cachePath, texture, ticks, length);
        }

        texture.Name = relativePath;
        _textures[relativePath] = texture;
        return true;
    }

    /// <summary>Собирает клип анимации из папки assets/anims/&lt;имя&gt;/.</summary>
    private void BuildAnimation(string clipName)
    {
        string directory = Path.Combine(RootPath, "anims", clipName);

        if (!Directory.Exists(directory))
        {
            _animations.Remove(clipName);
            return;
        }

        var frameFiles = SafeFiles(directory, "*.png")
            .OrderBy(NaturalOrderKey)
            .ToList();

        var frames = new List<Texture>();

        foreach (string file in frameFiles)
        {
            string relativePath = ToRelative(file);
            if (LoadTexture(relativePath) && _textures.TryGetValue(relativePath, out Texture? texture))
                frames.Add(texture);
        }

        if (frames.Count == 0)
        {
            _animations.Remove(clipName);
            return;
        }

        (double frameTime, bool loop) = ReadManifest(directory);
        _animations[clipName.ToLowerInvariant()] =
            new AnimationClip(clipName, frames.ToArray(), frameTime, loop);
    }

    /// <summary>manifest.json рядом с кадрами: {"frameTime": 0.1, "loop": false}.</summary>
    private static (double frameTime, bool loop) ReadManifest(string directory)
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
        catch (JsonException)
        {
            // Битый манифест не должен валить загрузку — берутся значения по умолчанию.
        }

        return (frameTime, loop);
    }

    // Ключ сортировки кадров: «2» < «10», а не «10» < «2» как при обычной сортировке строк.
    private static string NaturalOrderKey(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        return int.TryParse(name, out int number) ? number.ToString("D8") : name;
    }

    // ============================================================
    //   Бинарный кэш
    // ============================================================

    private string CachePathFor(string relativePath)
    {
        string flat = relativePath
            .Replace('/', '_')
            .Replace('\\', '_');

        return Path.Combine(CacheDirectory, Path.ChangeExtension(flat, ".tex"));
    }

    private Texture? TryReadCache(string cachePath, long ticks, long length)
    {
        try
        {
            if (!File.Exists(cachePath))
                return null;

            byte[] data = File.ReadAllBytes(cachePath);
            if (data.Length < CacheHeaderSize)
                return null;

            int offset = 0;
            uint magic = BitConverter.ToUInt32(data, offset);
            offset += 4;

            if (magic != CacheMagic || data[offset++] != 1)
                return null;

            int width = BitConverter.ToInt32(data, offset);
            offset += 4;
            int height = BitConverter.ToInt32(data, offset);
            offset += 4;
            long cacheTicks = BitConverter.ToInt64(data, offset);
            offset += 8;
            long cacheLength = BitConverter.ToInt64(data, offset);
            offset += 8;

            if (cacheTicks != ticks || cacheLength != length)
                return null;

            int pixelCount = width * height;
            if (width < 1 || height < 1 || data.Length < offset + pixelCount * 4)
                return null;

            var pixels = new int[pixelCount];
            Buffer.BlockCopy(data, offset, pixels, 0, pixelCount * 4);

            return new Texture(string.Empty, width, height, pixels);
        }
        catch (IOException)
        {
            return null;
        }
    }

    private void WriteCache(string cachePath, Texture texture, long ticks, long length)
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
        catch (IOException)
        {
            // Кэш не критичен: не записался — просто декодируем PNG при следующем запуске.
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

            _watcher.Changed += OnWatcherEvent;
            _watcher.Created += OnWatcherEvent;
            _watcher.Deleted += OnWatcherEvent;
            _watcher.Renamed += OnWatcherRenamed;

            _watcher.EnableRaisingEvents = true;
        }
        catch (IOException)
        {
            // Слежение не заведётся (например, папка на сетевом диске) — игра работает без hot-reload.
        }
    }

    private void OnWatcherEvent(object sender, FileSystemEventArgs args)
    {
        EnqueueRelative(args.FullPath);
    }

    private void OnWatcherRenamed(object sender, RenamedEventArgs args)
    {
        EnqueueRelative(args.OldFullPath);
        EnqueueRelative(args.FullPath);
    }

    private void EnqueueRelative(string fullPath)
    {
        try
        {
            string relativePath = Path.GetRelativePath(RootPath, fullPath).Replace('\\', '/');
            lock (_pendingGate)
                _pendingChanges.Enqueue(relativePath);
        }
        catch (ArgumentException)
        {
            // Путь вне корня ассетов (или временный файл редактора) — не интересно.
        }
    }

    private void ReloadPath(string relativePath)
    {
        string extension = Path.GetExtension(relativePath);
        bool isPng = extension.Equals(".png", StringComparison.OrdinalIgnoreCase);
        bool isManifest = extension.Equals(".json", StringComparison.OrdinalIgnoreCase);

        if (!isPng && !isManifest)
            return;

        // Изменение кадра или манифеста — пересобираем весь клип.
        if (relativePath.StartsWith("anims/", StringComparison.OrdinalIgnoreCase))
        {
            string clip = relativePath.Split('/')[1];
            if (!string.IsNullOrEmpty(clip) && !clip.Equals(GameConfig.AssetCacheFolderName, StringComparison.OrdinalIgnoreCase))
                BuildAnimation(clip);
            return;
        }

        if (isPng)
            LoadTexture(relativePath);
    }

    // ============================================================
    //   Мелочи
    // ============================================================

    private string ToRelative(string fullPath) =>
        Path.GetRelativePath(RootPath, fullPath).Replace('\\', '/');

    private static IEnumerable<string> SafeFiles(string directory, string pattern)
    {
        if (!Directory.Exists(directory))
            return Array.Empty<string>();

        try
        {
            return Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly);
        }
        catch (IOException)
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
            return Directory.EnumerateDirectories(directory);
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
    }
}

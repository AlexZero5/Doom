using System.Text.Json;
using System.Text.Json.Serialization;

namespace Doom.Configuration;

/// <summary>
///     Масштаб терминала («отдаление» шрифта). Игра всегда отдаляет до максимума: качество
///     трёхмерного вида задаёт <see cref="GameSettings.RenderScale" />, а HUD и меню рисуются в
///     полном разрешении терминала.
/// </summary>
internal enum GraphicsQuality
{
    Low,
    Medium,
    High,
    VeryHigh,
    Ultra,
    Extreme,
    Maximum
}

/// <summary>Допустимые диапазоны настроек: общие и для меню, и для проверки загруженного файла.</summary>
internal static class SettingsRanges
{
    public const double MouseSensitivityMin = 0.0005;
    public const double MouseSensitivityMax = 0.0100;
    public const double MoveSpeedMin = 2.0;
    public const double MoveSpeedMax = 12.0;
    public const double RotationSpeedMin = 0.5;
    public const double RotationSpeedMax = 5.0;
    public const double FieldOfViewMin = 50.0;
    public const double FieldOfViewMax = 120.0;

    /// <summary>
    ///     Варианты разрешения трёхмерного вида: во сколько раз внутренний кадр меньше терминала
    ///     (1 — пиксель в пиксель, 4 — один «пиксель» на 4×4).
    /// </summary>
    public static readonly double[] RenderScales = { 1.0, 1.25, 1.5, 2.0, 2.5, 3.0, 4.0 };
}

/// <summary>
///     Настройки игры. Значения по умолчанию берутся из <see cref="GameConfig" />, а изменения
///     сохраняются в <c>settings.json</c> рядом с игрой. Игровой код читает настройки напрямую
///     (<see cref="Current" />), поэтому правки в меню применяются сразу.
/// </summary>
internal sealed class GameSettings
{
    private const string FileName = "settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    ///     Версия формата настроек. 2 — камера стала честной (сброс угла обзора). 3 — мир в метрах:
    ///     скорость ходьбы — в м/с (сбрасывается), качество графики стало разрешением вида.
    /// </summary>
    private const int CurrentVersion = 3;

    /// <summary>Текущие настройки (один экземпляр на процесс).</summary>
    public static GameSettings Current { get; private set; } = new() { Version = CurrentVersion };

    /// <summary>Версия формата файла (для миграции; в старых файлах её нет — читается как 0).</summary>
    public int Version { get; set; }

    /// <summary>Масштаб терминала: всегда максимальный (самые мелкие «пиксели»).</summary>
    [JsonIgnore]
    public GraphicsQuality Quality => GraphicsQuality.Maximum;

    /// <summary>
    ///     Разрешение трёхмерного вида: во сколько раз внутренний кадр меньше окна. Больше —
    ///     грубее картинка, меньше работы и меньше байтов терминалу.
    /// </summary>
    public double RenderScale { get; set; } = 2.0;

    /// <summary>Чувствительность мыши: радианы поворота на пиксель сдвига.</summary>
    public double MouseSensitivity { get; set; } = GameConfig.MouseSensitivity;

    /// <summary>Скорость ходьбы игрока, м/с.</summary>
    public double MoveSpeed { get; set; } = GameConfig.MoveSpeed;

    /// <summary>Скорость поворота стрелками.</summary>
    public double RotationSpeed { get; set; } = GameConfig.RotationSpeed;

    /// <summary>Угол обзора по горизонтали в градусах.</summary>
    public double FieldOfViewDegrees { get; set; } = GameConfig.FieldOfViewDegrees;

    /// <summary>Захватывать курсор во время игры.</summary>
    public bool CaptureMouse { get; set; } = GameConfig.CaptureMouse;

    /// <summary>
    ///     Прятать курсор в игре и не давать мыши выделять текст терминала: под курсором
    ///     держится невидимое окно-«шторка», клики попадают в него, а не в терминал.
    /// </summary>
    public bool HideCursor { get; set; } = true;

    /// <summary>Покачивание оружия при ходьбе.</summary>
    public bool WeaponBob { get; set; } = true;

    /// <summary>Прицел в центре экрана.</summary>
    public bool Crosshair { get; set; } = true;

    /// <summary>
    ///     Сильнее упрощать кадр в движении. Само упрощение включено всегда (это оптимизация:
    ///     пока камера движется, мелких деталей всё равно не разглядеть); галка делает его грубее.
    /// </summary>
    public bool StrongMotionSimplify { get; set; }

    /// <summary>Показывать счётчик FPS в строке состояния.</summary>
    public bool ShowFps { get; set; }

    /// <summary>Автоматически «отдалять» картинку при старте (уменьшать шрифт терминала).</summary>
    public bool AutoZoom { get; set; } = GameConfig.AutoZoomOut;

    /// <summary>Разворачивать окно консоли на весь экран при старте.</summary>
    public bool AutoMaximizeWindow { get; set; } = true;

    /// <summary>
    ///     Во сколько раз кадр должен быть шире ЗАВОДСКОГО (масштаб терминала 100%): чем больше,
    ///     тем мельче «пиксели». Отсчёт идёт от заводского масштаба, а не от предела терминала.
    /// </summary>
    [JsonIgnore]
    public double ZoomColumnsOfBase => Quality switch
    {
        GraphicsQuality.Low => 1.00,
        GraphicsQuality.Medium => 1.20,
        GraphicsQuality.High => 1.45,
        GraphicsQuality.VeryHigh => 1.70,
        GraphicsQuality.Ultra => 2.00,
        GraphicsQuality.Extreme => 2.30,
        _ => 2.65 // Maximum
    };

    [JsonIgnore]
    public double FieldOfViewRadians => FieldOfViewDegrees * Math.PI / 180.0;

    [JsonIgnore]
    public double HalfFieldOfViewRadians => FieldOfViewDegrees * Math.PI / 360.0;

    /// <summary>Сохраняет настройки рядом с игрой. Ошибки ввода-вывода игнорируются.</summary>
    public void Save()
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, FileName);
            File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch
        {
            // Нет прав на запись — просто не сохраняем, игра продолжает работать.
        }
    }

    /// <summary>Загружает настройки из <c>settings.json</c>. Если файла нет или он битый — значения по умолчанию.</summary>
    public static void Load()
    {
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, FileName);
            if (!File.Exists(path))
                return;

            GameSettings? loaded = JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(path), JsonOptions);
            if (loaded is null)
                return;

            if (loaded.Version < 2)
                loaded.FieldOfViewDegrees = GameConfig.FieldOfViewDegrees;

            if (loaded.Version < 3)
            {
                loaded.MoveSpeed = GameConfig.MoveSpeed;
                loaded.RenderScale = 2.0;
            }

            loaded.Version = CurrentVersion;
            loaded.ClampToValidRanges();
            Current = loaded;
        }
        catch
        {
            // Повреждённый файл не должен мешать запуску: остаются значения по умолчанию.
        }
    }

    /// <summary>Проверяет, что значения из файла лежат в допустимых диапазонах.</summary>
    private void ClampToValidRanges()
    {
        MouseSensitivity = Math.Clamp(MouseSensitivity, SettingsRanges.MouseSensitivityMin, SettingsRanges.MouseSensitivityMax);
        MoveSpeed = Math.Clamp(MoveSpeed, SettingsRanges.MoveSpeedMin, SettingsRanges.MoveSpeedMax);
        RotationSpeed = Math.Clamp(RotationSpeed, SettingsRanges.RotationSpeedMin, SettingsRanges.RotationSpeedMax);
        FieldOfViewDegrees = Math.Clamp(FieldOfViewDegrees, SettingsRanges.FieldOfViewMin, SettingsRanges.FieldOfViewMax);
        double requested = RenderScale;
        RenderScale = SettingsRanges.RenderScales.MinBy(scale => Math.Abs(scale - requested));
    }
}

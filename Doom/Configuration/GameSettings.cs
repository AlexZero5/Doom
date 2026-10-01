using System.Text.Json;
using System.Text.Json.Serialization;

namespace Doom.Configuration;

/// <summary>
///     Уровень «качества графики»: задаёт, насколько сильно игра «отдаляет» картинку при
///     старте (сколько раз нажать «Ctrl + −»). Чем выше качество, тем мельче «пиксели».
/// </summary>
internal enum GraphicsQuality
{
    Low,
    Medium,
    High,
    Ultra
}

/// <summary>Допустимые диапазоны настроек: общие и для меню, и для проверки загруженного файла.</summary>
internal static class SettingsRanges
{
    public const double MouseSensitivityMin = 0.0005;
    public const double MouseSensitivityMax = 0.0100;
    public const double MoveSpeedMin = 1.0;
    public const double MoveSpeedMax = 8.0;
    public const double RotationSpeedMin = 0.5;
    public const double RotationSpeedMax = 5.0;
    public const double FieldOfViewMin = 40.0;
    public const double FieldOfViewMax = 110.0;
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

    /// <summary>Текущие настройки (один экземпляр на процесс).</summary>
    public static GameSettings Current { get; private set; } = new();

    /// <summary>Качество графики: влияет на «отдаление» картинки при старте.</summary>
    public GraphicsQuality Quality { get; set; } = GraphicsQuality.High;

    /// <summary>Чувствительность мыши: радианы поворота на пиксель сдвига.</summary>
    public double MouseSensitivity { get; set; } = GameConfig.MouseSensitivity;

    /// <summary>Скорость ходьбы игрока.</summary>
    public double MoveSpeed { get; set; } = GameConfig.MoveSpeed;

    /// <summary>Скорость поворота стрелками.</summary>
    public double RotationSpeed { get; set; } = GameConfig.RotationSpeed;

    /// <summary>Угол обзора по горизонтали в градусах.</summary>
    public double FieldOfViewDegrees { get; set; } = GameConfig.FieldOfViewDegrees;

    /// <summary>Захватывать курсор во время игры.</summary>
    public bool CaptureMouse { get; set; } = GameConfig.CaptureMouse;

    /// <summary>Сколько раз нажать «Ctrl + −» при старте: чем выше качество, тем мельче «пиксели».</summary>
    [JsonIgnore]
    public int ZoomOutSteps => Quality switch
    {
        GraphicsQuality.Low => 3,
        GraphicsQuality.Medium => 6,
        GraphicsQuality.Ultra => 12,
        _ => 9
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
    }
}
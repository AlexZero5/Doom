namespace Doom.Configuration;

/// <summary>
///     Единая точка настройки игры: геометрия обзора, параметры консоли,
///     скорости игрока, тайминги анимаций и лимиты боезапаса.
/// </summary>
internal static class GameConfig
{
    // ============================================================
    //   Обзор и рендер
    // ============================================================
    public const double FieldOfView = Math.PI / 3;

    public const double HalfFieldOfView = FieldOfView * 0.5;

    /// <summary>Количество вертикальных сэмплов текстуры стены.</summary>
    public const int TextureSamples = 2;

    /// <summary>Максимум шагов DDA при трассировке луча.</summary>
    public const int MaxRaySteps = 512;

    private const int WideScreenColumns = 200;
    private const int MediumScreenColumns = 100;

    /// <summary>Количество лучей на колонку: чем уже кадр, тем выше качество сглаживания.</summary>
    public static int RaysPerColumn(int columns) =>
        columns > WideScreenColumns ? 2
        : columns > MediumScreenColumns ? 4
        : 6;

    // ============================================================
    //   Консоль
    // ============================================================
    public const int DesiredColumns = 320;
    public const int DesiredRows = 100;
    public const short DesiredFontSize = 8;
    public const short MinimumFontSize = 4;

    public const int MinimumColumns = 40;
    public const int MinimumRows = 15;

    /// <summary>Размер кадра, который используется, если консоль не сообщает свои размеры.</summary>
    public const int FallbackColumns = 160;

    public const int FallbackRows = 40;

    public const int StartupDelayMs = 300;
    public const int ResizeDelayMs = 200;
    public const int ZoomHintMilliseconds = 4000;
    public const int ZoomHintPollMs = 50;

    // ============================================================
    //   Игрок
    // ============================================================
    public const double MoveSpeed = 3.0;
    public const double RotationSpeed = 2.0;

    public const double PickupRadius = 0.45;

    /// <summary>Стартовая позиция игрока и направление взгляда.</summary>
    public const double StartX = 2.5;

    public const double StartY = 2.5;

    public const double StartAngle = 0.4;

    public const int InitialBullets = 50;

    // Предельный запас боеприпасов каждого типа (как в Doom).
    public const int MaxBullets = 200;
    public const int MaxShells = 50;
    public const int MaxRockets = 50;
    public const int MaxCells = 300;

    // ============================================================
    //   Оружие, снаряды и эффекты
    // ============================================================

    /// <summary>Сколько секунд ствол поднимается после смены оружия.</summary>
    public const double WeaponSwitchSeconds = 0.30;

    /// <summary>Насколько ствол «утоплен» вниз в начале подъёма (в базовых строках экрана).</summary>
    public const double WeaponRaiseOffset = 11.0;

    /// <summary>Дальность рукопашной атаки в клетках.</summary>
    public const double MeleeRange = 1.25;

    /// <summary>Насколько искра отодвигается от стены, чтобы её не перекрывала грань.</summary>
    public const double SparkOffsetFromWall = 0.07;

    /// <summary>На каком расстоянии от игрока рождается снаряд.</summary>
    public const double ProjectileSpawnOffset = 0.34;

    /// <summary>Максимальный шаг проверки столкновения снаряда со стеной.</summary>
    public const double MaxProjectileStep = 0.08;

    /// <summary>Ограничения на количество снарядов и эффектов, чтобы кадр не «плыл».</summary>
    public const int MaxProjectiles = 64;

    public const int MaxImpacts = 192;

    /// <summary>Оборот стволов пулемёта и полотна пилы за выстрел (радианы) и предел фазы.</summary>
    public const double ChaingunSpinPerShot = 1.10;

    public const double ChainsawSpinPerShot = 1.60;

    public const double SpinPhaseWrap = 4 * Math.Tau;


    // ============================================================
    //   Тайминги
    // ============================================================
    public const int HoldTimeoutMs = 150;
    public const double TargetFrameMs = 16.0;
    public const double MaxFrameSeconds = 0.1;

    public const double MuzzleFlashSeconds = 0.06;
    public const double RecoilSeconds = 0.18;
    public const double WeaponAnimationSeconds = 0.25;

    public const double BobRisePerSecond = 8.0;
    public const double BobFallPerSecond = 5.0;
    public const double BobTimeWrap = 1000.0;
}

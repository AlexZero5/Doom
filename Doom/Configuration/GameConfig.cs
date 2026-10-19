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
    /// <summary>
    ///     Горизонтальный угол обзора по умолчанию. Камера честная (квадратные пиксели), поэтому
    ///     на широком терминале 90° по горизонтали дают ~55° по вертикали.
    /// </summary>
    public const double FieldOfView = Math.PI / 2;

    /// <summary>Угол обзора в градусах — значение по умолчанию для настроек.</summary>
    public const double FieldOfViewDegrees = FieldOfView * 180.0 / Math.PI;

    /// <summary>Предел взгляда вверх/вниз: почти 90° — можно смотреть прямо в пол и в небо.</summary>
    public const double MaxPitchRadians = 89.0 * Math.PI / 180.0;

    // ============================================================
    //   Консоль
    // ============================================================
    // Размер окна игрой НЕ задаётся: терминал физически не может растянуть буфер на
    // заведомо большее число ячеек, чем влезает на экран, — от такого запроса кадр
    // рисовался «мимо» рамки. Игра подстраивается под фактическое окно.

    public const short MinimumFontSize = 4;

    /// <summary>Верхняя граница подбора шрифта, если точный размер для качества не поддерживается.</summary>
    public const short MaximumFontSize = 24;

    public const int MinimumColumns = 40;
    public const int MinimumRows = 15;

    /// <summary>Размер кадра, который используется, если консоль не сообщает свои размеры.</summary>
    public const int FallbackColumns = 160;

    public const int FallbackRows = 40;

    public const int ResizeDelayMs = 200;
    public const int ZoomHintMilliseconds = 4000;
    public const int ZoomHintPollMs = 50;

    /// <summary>
    ///     Автоматически «отдалять» картинку при старте: уменьшать шрифт в классической консоли
    ///     и многократно нажимать «Ctrl + −» в Windows Terminal (там размер шрифта консольным
    ///     API не меняется). Если сделать это не удалось, показывается подсказка.
    /// </summary>
    public const bool AutoZoomOut = true;

    /// <summary>Пауза после эмуляции зума, чтобы терминал успел применить новый шрифт.</summary>
    public const int AutoZoomOutSettleMs = 150;

    /// <summary>
    ///     Предел нажатий «Ctrl + −» при подборе масштаба: страховка от зацикливания, если
    ///     терминал перестал реагировать на клавиши.
    /// </summary>
    public const int MaxZoomDownPresses = 24;

    /// <summary>Сколько раз перечитать размер кадра после нажатия, прежде чем считать, что оно не сработало.</summary>
    public const int ZoomSettlePolls = 4;

    /// <summary>Опрос стабильности размера окна после разворота/смены масштаба.</summary>
    public const int StablePollMs = 150;
    public const int StablePollAttempts = 20;

    /// <summary>Сколько держать экран загрузки, пока настраивается масштаб терминала.</summary>
    public const int LoadingScreenMs = 650;

    // ============================================================
    //   Игрок
    // ============================================================
    /// <summary>Скорость ходьбы, м/с.</summary>
    public const double MoveSpeed = 5.5;
    public const double RotationSpeed = 2.0;

    public const double PickupRadius = 0.7;

    /// <summary>Радиус игрока (метры): дверной проём в метр проходим с запасом.</summary>
    public const double PlayerRadius = 0.28;

    /// <summary>Рост (метры): столько места нужно над ногами.</summary>
    public const double PlayerHeight = 1.75;

    /// <summary>Высота глаз над ногами.</summary>
    public const double EyeHeight = 1.6;

    /// <summary>Уступ, на который игрок поднимается без прыжка (ступени, порог, бордюр).</summary>
    public const double StepHeight = 0.42;

    /// <summary>Ускорение свободного падения (м/с², «игровое» — чуть больше настоящего).</summary>
    public const double Gravity = 19.0;

    /// <summary>Скорость отрыва при прыжке: высота прыжка ≈ v² / 2g ≈ 1.1 м.</summary>
    public const double JumpSpeed = 6.5;

    /// <summary>Скорость подъёма по вертикальной лестнице.</summary>
    public const double ClimbSpeed = 2.4;

    /// <summary>Дальность «использовать» (двери).</summary>
    public const double UseRange = 2.0;

    /// <summary>Стартовая позиция игрока и направление взгляда (юг площади, лицом к башне).</summary>
    public const double StartX = 16.5;

    public const double StartY = 20.5;

    public const double StartAngle = -Math.PI / 2;

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
    public const double MeleeRange = 1.6;

    /// <summary>Насколько искра отодвигается от стены, чтобы её не перекрывала грань.</summary>
    public const double SparkOffsetFromWall = 0.07;

    /// <summary>На каком расстоянии от игрока рождается снаряд.</summary>
    public const double ProjectileSpawnOffset = 0.5;

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

    // ============================================================
    //   Ассеты (assets/ рядом с репозиторием)
    // ============================================================

    /// <summary>Имя папки с текстурами: walls/, weapons/, sprites/, anims/.</summary>
    public const string AssetsFolderName = "assets";

    /// <summary>Подпапка бинарного кэша декодированных текстур.</summary>
    public const string AssetCacheFolderName = ".cache";

    /// <summary>Длительность кадра анимации, если в manifest.json не задано иное.</summary>
    public const double DefaultAnimationFrameTime = 0.12;

    /// <summary>Пауза после разворота окна, чтобы консоль успела пересчитать размер.</summary>
    public const int MaximizeSettleMs = 200;

    /// <summary>Высота текстурного оружия как доля высоты кадра (в «пикселях»).</summary>
    public const double WeaponTextureHeightFraction = 0.45;

    /// <summary>Высота анимации вспышки у дула как доля высоты кадра.</summary>
    public const double MuzzleAnimationHeightFraction = 0.16;

    // ============================================================
    //   Мышь
    // ============================================================

    /// <summary>Поворот мышью: сколько радиан на каждый пиксель горизонтального сдвига.</summary>
    public const double MouseSensitivity = 0.0025;

    /// <summary>
    ///     Захватывать курсор во время игры: пока окно терминала на переднем плане, курсор
    ///     скрывается и удерживается в исходной точке, поэтому поворачиваться можно бесконечно
    ///     (как обзор мышью в шутере). При потере фокуса курсор снова показывается.
    /// </summary>
    public const bool CaptureMouse = true;
}

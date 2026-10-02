using System.Diagnostics;
using Doom.Assets;
using Doom.Configuration;
using Doom.Gameplay;
using Doom.Input;
using Doom.Platform;
using Doom.Rendering;
using Doom.UI;
using Doom.World;

namespace Doom.Core;

/// <summary>
///     Главный цикл игры: ввод, симуляция игрока, рендер кадра и отправка его в терминал.
/// </summary>
internal sealed class DoomGame
{
    private readonly Terminal _terminal = new();
    private readonly KeyboardState _keyboard = new();
    private readonly MouseState _mouse = new();
    private readonly ConsoleInputSource _input = new();
    private readonly MenuController _menu = new();
    private readonly Level _level = Level.CreateDefault();
    private readonly Player _player = new();
    private readonly List<Pickup> _pickups = new();
    private readonly ProjectileSystem _projectiles = new();
    private readonly ConsolePresenter _presenter;
    private readonly Stopwatch _totalTimer = new();
    private readonly Stopwatch _frameTimer = new();

    private readonly AssetStore _assets;
    private readonly SceneRenderer _renderer;

    private Framebuffer _framebuffer = new(1, 1);
    private Viewport _viewport;
    private double _previousFrameTime;

    /// <summary>Текущий экран: геймплей или один из экранов меню.</summary>
    private GameScreen _screen = GameScreen.Playing;

    /// <summary>Запрошен выход из игры (пункт меню «Выход»).</summary>
    private bool _quitRequested;

    /// <summary>Счётчик выстрелов: по нему считается детерминированный разброс дробинок.</summary>
    private int _shotCounter;

    /// <summary>Меню ждёт перерисовки (открытие, движение мыши, изменение настроек).</summary>
    private bool _menuDirty;

    /// <summary>Сглаженное значение FPS для счётчика в строке состояния.</summary>
    private double _fps;

    public DoomGame()
    {
        _assets = new AssetStore();
        _renderer = new SceneRenderer(_assets);
        _presenter = new ConsolePresenter(_terminal);
        _pickups.AddRange(PickupLayout.CreateDefault());
    }

    public void Run()
    {
        GameSettings.Load();
        _assets.Load();

        if (!PrepareTerminal())
            return;

        _terminal.HideCursorAndClearScreen();

        // Ввод в сыром режиме: без QuickEdit и построчного редактирования, с VT-мышью в меню.
        _terminal.ConfigureInput();

        // Левой кнопкой стреляем, движением — обзор; при захвате курсор удерживается на месте.
        if (GameSettings.Current.CaptureMouse)
            _mouse.BeginCapture();

        Console.CancelKeyPress += OnCancelKeyPress;

        _totalTimer.Restart();
        _frameTimer.Restart();
        _previousFrameTime = _totalTimer.Elapsed.TotalSeconds;

        try
        {
            Loop();
        }
        finally
        {
            GameSettings.Current.Save();
            _assets.Dispose();
            _mouse.EndCapture();
            _terminal.DisableMouseReporting();
            _terminal.RestoreInputMode();
            _terminal.Restore();
        }
    }

    /// <summary>
    ///     Настраивает терминал: показывает экран загрузки, «отдаляет» картинку и подбирает размер.
    ///     Возвращает <c>false</c>, если окно слишком маленькое.
    /// </summary>
    private bool PrepareTerminal()
    {
        // Загрузка скрывает «магию» с масштабом: пока она на экране, игра жмёт Ctrl+− за пользователя.
        _terminal.ShowLoadingScreen();
        Thread.Sleep(GameConfig.LoadingScreenMs);

        bool zoomedOut = GameConfig.AutoZoomOut && GameSettings.Current.AutoZoom && _terminal.TryAutoZoomOut();
        if (!zoomedOut)
            _terminal.TrySetFontSize(GameConfig.DesiredFontSize);

        // Окно увеличиваем, но НЕ уменьшаем: после «отдаления» шрифта то же окно вмещает больше
        // символов, и запрос 320×100 наоборот сжал бы картинку.
        GrowWindowToDesiredSize();
        Thread.Sleep(GameConfig.StartupDelayMs);

        Terminal.ReadWindowSize(out int columns, out int rows);
        Resize(columns, rows);

        if (columns < GameConfig.MinimumColumns || rows < GameConfig.MinimumRows)
        {
            Console.WriteLine("Окно консоли слишком маленькое. Нужно минимум 40x15.");
            return false;
        }

        // Подсказку показываем только если «отдалить» автоматически не получилось.
        if (!zoomedOut)
            _terminal.ShowZoomHint(PollAnyKey);

        // После подсказки и ресайза окно могло измениться — читаем размер ещё раз.
        Terminal.ReadWindowSize(out columns, out rows);
        Resize(columns, rows);

        return true;
    }

    /// <summary>
    ///     Увеличивает окно до желаемого размера, но никогда не уменьшает его. Если текущее окно
    ///     уже больше 320×100 (частый случай после «отдаления» шрифта), размер не трогается, поэтому
    ///     картинка не сжимается.
    /// </summary>
    private void GrowWindowToDesiredSize()
    {
        Terminal.ReadWindowSize(out int columns, out int rows);

        int targetColumns = Math.Max(columns, GameConfig.DesiredColumns);
        int targetRows = Math.Max(rows, GameConfig.DesiredRows);

        if (targetColumns == columns && targetRows == rows)
            return;

        _terminal.TryResize(targetColumns, targetRows);
    }

    /// <summary>Пересоздаёт буферы кадра под новый размер окна.</summary>
    private void Resize(int columns, int rows)
    {
        _framebuffer = new Framebuffer(columns, rows);
        _viewport = new Viewport(columns, rows);
        _presenter.EnsureCapacity(columns * rows);
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs args)
    {
        args.Cancel = false;
        _mouse.EndCapture();
        _terminal.DisableMouseReporting();
        _terminal.RestoreInputMode();
        _terminal.Restore();
    }

    /// <summary>Читает одно нажатие клавиши: из событий консоли или напрямую (не-Windows).</summary>
    private bool TryReadKey(out ConsoleKey key)
    {
        if (OperatingSystem.IsWindows())
        {
            if (_input.TryReadKey(out VtKeyEvent vtKey))
            {
                key = vtKey.Key;
                return true;
            }

            key = 0;
            return false;
        }

        if (Console.KeyAvailable)
        {
            key = Console.ReadKey(true).Key;
            return true;
        }

        key = 0;
        return false;
    }

    /// <summary>Опрос «нажата ли какая-нибудь клавиша» для экрана подсказки.</summary>
    private bool PollAnyKey()
    {
        if (OperatingSystem.IsWindows())
        {
            _input.Poll();
            return _input.TryReadKey(out _);
        }

        if (Console.KeyAvailable)
        {
            Console.ReadKey(true);
            return true;
        }

        return false;
    }

    /// <summary>Обрабатывает нажатия клавиш во время игры.</summary>
    private void HandleGameKeys(long nowMs)
    {
        double nowSeconds = nowMs / 1000.0;

        while (TryReadKey(out ConsoleKey key))
        {
            switch (key)
            {
                case ConsoleKey.Escape:
                    OpenMenu();
                    return;
                case ConsoleKey.OemMinus:
                case ConsoleKey.Subtract:
                    _terminal.TrySetFontSize((short)Math.Max(
                        (int)GameConfig.MinimumFontSize, GameConfig.DesiredFontSize - 1));
                    break;

                // Слоты оружия 1..7: у кулака и бензопилы общий слот 1.
                case ConsoleKey.D1 or ConsoleKey.NumPad1:
                    _player.SelectSlot(1, nowSeconds);
                    break;
                case ConsoleKey.D2 or ConsoleKey.NumPad2:
                    _player.SelectSlot(2, nowSeconds);
                    break;
                case ConsoleKey.D3 or ConsoleKey.NumPad3:
                    _player.SelectSlot(3, nowSeconds);
                    break;
                case ConsoleKey.D4 or ConsoleKey.NumPad4:
                    _player.SelectSlot(4, nowSeconds);
                    break;
                case ConsoleKey.D5 or ConsoleKey.NumPad5:
                    _player.SelectSlot(5, nowSeconds);
                    break;
                case ConsoleKey.D6 or ConsoleKey.NumPad6:
                    _player.SelectSlot(6, nowSeconds);
                    break;
                case ConsoleKey.D7 or ConsoleKey.NumPad7:
                    _player.SelectSlot(7, nowSeconds);
                    break;

                case ConsoleKey.Q:
                    _player.CycleWeapon(-1, nowSeconds);
                    break;
                case ConsoleKey.E:
                    _player.CycleWeapon(1, nowSeconds);
                    break;
                case ConsoleKey.R:
                    RestartLevel();
                    break;
            }

            _keyboard.MarkPressed(key, nowMs);
        }
    }

    /// <summary>Возвращает игрока в начало уровня и раскладывает бонусы заново.</summary>
    private void RestartLevel()
    {
        _player.Reset();
        _projectiles.Reset();
        _pickups.Clear();
        _pickups.AddRange(PickupLayout.CreateDefault());
        _shotCounter = 0;
    }

    /// <summary>Читает размер окна и при изменении пересоздаёт буферы. Возвращает <c>true</c>, если размер изменился.</summary>
    private bool TryApplyWindowSize()
    {
        Terminal.ReadWindowSize(out int columns, out int rows);
        if (columns == _viewport.Columns && rows == _viewport.Rows)
            return false;

        Resize(columns, rows);
        _terminal.ClearScreen();
        return true;
    }

    private void Loop()
    {
        double lastX = double.NaN;
        double lastY = double.NaN;
        double lastAngle = double.NaN;
        bool hasRendered = false;

        while (!_quitRequested)
        {
            _frameTimer.Restart();

            if (TryApplyWindowSize())
                hasRendered = false;

            long nowMs = Environment.TickCount64;
            double nowSeconds = nowMs / 1000.0;

            _mouse.Update();

            // Забираем клавиши и мышь из очереди событий консоли (клавиатура меню,
            // мышь меню, Esc во время игры — всё приходит отсюда) и применяем
            // изменения ассетов: перекрашенный в редакторе PNG подхватывается на лету.
            _input.Poll();
            _assets.Poll();

            if (_screen == GameScreen.Playing)
                HandleGameKeys(nowMs);

            bool redraw;

            if (_screen == GameScreen.Playing)
            {
                double elapsedSeconds = _totalTimer.Elapsed.TotalSeconds;
                double deltaSeconds = elapsedSeconds - _previousFrameTime;
                _previousFrameTime = elapsedSeconds;

                if (deltaSeconds > GameConfig.MaxFrameSeconds)
                    deltaSeconds = GameConfig.MaxFrameSeconds;
                if (deltaSeconds < 0)
                    deltaSeconds = 0;

                bool anyInput = UpdateWorld(deltaSeconds, nowSeconds, nowMs);

                bool positionChanged = _player.X != lastX || _player.Y != lastY || _player.Angle != lastAngle;
                bool animationRunning =
                    nowSeconds - _player.LastFireTime < GameConfig.WeaponAnimationSeconds ||
                    nowSeconds < _player.MuzzleFlashUntil ||
                    _player.IsRaisingWeapon(nowSeconds) ||
                    _player.BobIntensity > 0.001 ||
                    _projectiles.IsActive;

                redraw = !hasRendered || anyInput || positionChanged || animationRunning;

                if (redraw)
                {
                    lastX = _player.X;
                    lastY = _player.Y;
                    lastAngle = _player.Angle;
                }
            }
            else
            {
                // Меню перерисовывается по событиям: открытие, клавиши, движение мыши.
                bool menuChanged = ProcessMenuInput();
                redraw = !hasRendered || menuChanged || _menuDirty;
                _menuDirty = false;
            }

            if (redraw)
            {
                RenderFrame(nowSeconds);
                _presenter.Present(_framebuffer);
                hasRendered = true;
            }

            double frameSeconds = _frameTimer.Elapsed.TotalSeconds;
            if (frameSeconds > 0.0001)
            {
                double instantFps = 1.0 / frameSeconds;
                _fps = _fps <= 0 ? instantFps : _fps * 0.9 + instantFps * 0.1;
            }

            SleepToTargetFrameRate();
        }
    }

    /// <summary>Рисует кадр: сцену и, если открыто меню, наложение поверх неё.</summary>
    private void RenderFrame(double nowSeconds)
    {
        _renderer.Render(
            _framebuffer,
            _viewport,
            _level,
            _player,
            _pickups,
            _projectiles,
            nowSeconds,
            _projectiles.FlashIntensity,
            _projectiles.FlashColor);

        if (GameSettings.Current.ShowFps)
            HudRenderer.DrawFps(_framebuffer, (int)Math.Round(_fps));

        if (_screen != GameScreen.Playing)
            MenuRenderer.Draw(_framebuffer, _menu, _player, _assets);
    }

    /// <summary>Открывает меню-паузу и освобождает курсор, чтобы мышью можно было выбирать пункты.</summary>
    private void OpenMenu()
    {
        _menu.Open();
        _screen = GameScreen.MainMenu;
        _mouse.EndCapture();
        _input.Clear();

        // Терминал начинает присылать события мыши: наведение, клики, колесо.
        _terminal.EnableMouseReporting();

        // Иначе меню появится только после следующего события (баг «Esc не сразу»).
        _menuDirty = true;
    }

    /// <summary>Обрабатывает клавиши и мышь в меню. Возвращает <c>true</c>, если нужна перерисовка.</summary>
    private bool ProcessMenuInput()
    {
        bool redraw = false;

        while (TryReadKey(out ConsoleKey key))
        {
            ApplyMenuCommand(_menu.HandleKey(key, _player), ref redraw);
        }

        if (OperatingSystem.IsWindows())
        {
            while (_input.TryReadMouse(out VtMouseEvent mouse))
            {
                ApplyMenuCommand(_menu.HandleMouse(mouse, _player), ref redraw);
            }
        }

        return redraw;
    }

    private void ApplyMenuCommand(MenuCommand command, ref bool redraw)
    {
        switch (command)
        {
            case MenuCommand.Redraw:
                redraw = true;
                break;
            case MenuCommand.Resume:
                ResumeGame();
                redraw = true;
                break;
            case MenuCommand.Quit:
                _quitRequested = true;
                redraw = true;
                break;
        }
    }

    /// <summary>Возвращает управление игре и применяет новые настройки (например, угол обзора).</summary>
    private void ResumeGame()
    {
        _screen = GameScreen.Playing;
        _viewport = new Viewport(_framebuffer.Columns, _framebuffer.Rows);
        _previousFrameTime = _totalTimer.Elapsed.TotalSeconds;

        GameSettings.Current.Save();

        // Мышь снова для обзора: отчёт о событиях больше не нужен.
        _terminal.DisableMouseReporting();
        _input.Clear();

        if (GameSettings.Current.CaptureMouse)
            _mouse.BeginCapture();
    }

    /// <summary>Обновляет состояние игрока. Возвращает <c>true</c>, если игрок что-то сделал.</summary>
    private bool UpdateWorld(double deltaSeconds, double nowSeconds, long nowMs)
    {
        double move = GameConfig.MoveSpeed * deltaSeconds;
        double rotate = GameConfig.RotationSpeed * deltaSeconds;

        bool anyInput = false;
        bool isMoving = false;

        if (IsHeld(ConsoleKey.W, nowMs) || IsHeld(ConsoleKey.UpArrow, nowMs))
        {
            _player.Move(_level, _player.Angle, move);
            anyInput = true;
            isMoving = true;
        }

        if (IsHeld(ConsoleKey.S, nowMs) || IsHeld(ConsoleKey.DownArrow, nowMs))
        {
            _player.Move(_level, _player.Angle, -move);
            anyInput = true;
            isMoving = true;
        }

        if (IsHeld(ConsoleKey.A, nowMs))
        {
            _player.Move(_level, _player.Angle - Math.PI / 2, move);
            anyInput = true;
            isMoving = true;
        }

        if (IsHeld(ConsoleKey.D, nowMs))
        {
            _player.Move(_level, _player.Angle + Math.PI / 2, move);
            anyInput = true;
            isMoving = true;
        }

        if (IsHeld(ConsoleKey.LeftArrow, nowMs))
        {
            _player.Turn(-rotate);
            anyInput = true;
        }

        if (IsHeld(ConsoleKey.RightArrow, nowMs))
        {
            _player.Turn(rotate);
            anyInput = true;
        }

        // Горизонтальный сдвиг мыши поворачивает камеру; левая кнопка стреляет (как Space).
        if (_mouse.DeltaX != 0)
        {
            _player.Turn(_mouse.DeltaX * GameConfig.MouseSensitivity);
            anyInput = true;
        }

        bool firePressed = IsHeld(ConsoleKey.Spacebar, nowMs) || _mouse.LeftButton;
        if (firePressed && _player.TryFire(nowSeconds, out WeaponInfo info))
        {
            FireWeapon(info);
            anyInput = true;
        }

        // Если патроны кончились, игрок автоматически берёт следующий готовый ствол.
        if (_player.EnsureUsableWeapon(nowSeconds))
            anyInput = true;

        if (_player.CollectPickups(_pickups, nowSeconds))
            anyInput = true;

        _projectiles.Update(deltaSeconds, _level);

        // Покачивание оружия можно отключить в настройках: амплитуда плавно гаснет.
        if (GameSettings.Current.WeaponBob)
            _player.UpdateWeaponBob(deltaSeconds, isMoving);
        else if (_player.BobIntensity > 0.0001)
            _player.UpdateWeaponBob(deltaSeconds, false);

        return anyInput;
    }

    /// <summary>Обрабатывает состоявшийся выстрел: рукопашная, мгновенные лучи или снаряд.</summary>
    private void FireWeapon(WeaponInfo info)
    {
        if (info.IsMelee)
        {
            FireMelee();
        }
        else if (info.Projectile != ProjectileKind.None)
        {
            _projectiles.Launch(info.Projectile, _player.X, _player.Y, _player.Angle);
        }
        else
        {
            FireHitscan(info);
        }

        // Вспышка выстрела подсвечивает кадр: у ракетницы и BFG заметно, у пистолета едва.
        _projectiles.AddFlash(info.MuzzleFlash, info.MuzzleFlashSeconds + 0.06, info.MuzzleFlashColor);
    }

    /// <summary>Мгновенный выстрел: каждая дробинка оставляет искру на стене.</summary>
    private void FireHitscan(WeaponInfo info)
    {
        for (int pellet = 0; pellet < info.Pellets; pellet++)
        {
            double spreadOffset = info.Pellets > 1
                ? info.Spread * (pellet / (double)(info.Pellets - 1) - 0.5)
                : 0.0;

            // Разброс детерминированный: одно и то же число выстрелов даёт ту же картинку.
            double jitter = (SpreadNoise.Unit(pellet, _shotCounter) - 0.5) * info.Spread * 0.6;
            double angle = _player.Angle + spreadOffset + jitter;

            RayHit hit = Raycaster.Cast(_level, _player.X, _player.Y, angle);
            if (hit.TileType == 0)
                continue;

            _projectiles.SpawnHitSpark(ImpactKind.BulletSpark, _player.X, _player.Y, angle, hit.Distance);
        }

        _shotCounter++;
    }

    /// <summary>Рукопашная атака: искра появляется, если стена ближе вытянутой руки.</summary>
    private void FireMelee()
    {
        RayHit hit = Raycaster.Cast(_level, _player.X, _player.Y, _player.Angle);
        if (hit.TileType == 0 || hit.Distance > GameConfig.MeleeRange)
            return;

        _projectiles.SpawnHitSpark(ImpactKind.MeleeSpark, _player.X, _player.Y, _player.Angle, hit.Distance);
    }

    private bool IsHeld(ConsoleKey key, long nowMs) => _keyboard.IsHeld(key, nowMs);

    /// <summary>Ограничивает частоту кадров ~60 FPS.</summary>
    private void SleepToTargetFrameRate()
    {
        double elapsedMs = _frameTimer.Elapsed.TotalMilliseconds;
        if (elapsedMs >= GameConfig.TargetFrameMs)
            return;

        int sleepMs = (int)(GameConfig.TargetFrameMs - elapsedMs);
        if (sleepMs > 0)
            Thread.Sleep(sleepMs);
    }
}

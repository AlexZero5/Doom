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
    private readonly Player _player = new();
    private readonly List<Pickup> _pickups = new();
    private readonly ProjectileSystem _projectiles = new();
    private readonly ConsolePresenter _presenter;
    private readonly FramePipeline _pipeline;
    private readonly Stopwatch _totalTimer = new();
    private readonly Stopwatch _frameTimer = new();

    private readonly AssetStore _assets;
    private readonly SceneRenderer _renderer;

    /// <summary>Файл диагностики уже создан за этот запуск (первая строка перезаписывает старый).</summary>
    private static bool _diagnosticsStarted;

    /// <summary>Текущая карта (assets/maps/house.map; без файла — запасная коробка).</summary>
    private Level _level = FallbackLevel();

    /// <summary>Последний кадр нарисован упрощённым (в движении): в покое его надо перерисовать начисто.</summary>
    private bool _lastFrameSimplified;

    /// <summary>Фаза мигания черепа-указателя в меню: меню перерисовывается при её смене.</summary>
    private int _menuBlinkPhase;

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

    /// <summary>
    ///     FPS для счётчика в строке состояния: сколько кадров реально дошло до терминала
    ///     за последние полсекунды (а не сколько раз провернулся цикл).
    /// </summary>
    private double _fps;

    private long _fpsWindowStartTick;
    private long _fpsWindowStartFrames;

    /// <summary>Окно игры на переднем плане (опрашивается раз в кадр).</summary>
    private bool _hasFocus = true;

    /// <summary>Невидимая «шторка» под курсором: прячет его и не даёт мыши выделять текст терминала.</summary>
    private CursorShield? _shield;

    public DoomGame()
    {
        _assets = new AssetStore();
        _renderer = new SceneRenderer(_assets);
        _presenter = new ConsolePresenter(_terminal, Terminal.IsRunningInWindowsTerminal);
        _pipeline = new FramePipeline(_presenter);
    }

    public void Run()
    {
        GameSettings.Load();
        _assets.Load();
        _level = LoadLevel(_assets, "maps/town.map");
        _shield = OperatingSystem.IsWindows() ? new CursorShield() : null;
        _mouse.Shield = _shield;
        RestartLevel();

        if (!PrepareTerminal())
            return;

        _terminal.HideCursorAndClearScreen();

        // Автоперенос выключаем: запись в последнюю ячейку строки не должна
        // переносить курсор и скроллить экран — это рассинхронизирует кадр.
        _terminal.DisableAutoWrap();

        // Ввод в сыром режиме: без QuickEdit и построчного редактирования, с VT-мышью в меню.
        _terminal.ConfigureInput();

        // Левой кнопкой стреляем, движением — обзор; при захвате курсор удерживается на месте.
        if (GameSettings.Current.CaptureMouse)
            _mouse.BeginCapture();

        Console.CancelKeyPress += OnCancelKeyPress;

        // Диагностика окружения: видно сразу в окне, какой терминал и какой режим вывода.
        LogDiagnostics(
            $"[doom] terminal={(Terminal.IsRunningInWindowsTerminal ? "WindowsTerminal" : "conhost")}, " +
            $"output={(Terminal.IsRunningInWindowsTerminal || _terminal.TerminalIsWindowsTerminal ? "diff+sync" : "full-frame")}, " +
            $"console={Terminal.DescribeConsole()}, окно терминала={_terminal.TerminalWindowReport}");

        _totalTimer.Restart();
        _frameTimer.Restart();
        _previousFrameTime = _totalTimer.Elapsed.TotalSeconds;

        // Точный системный таймер: без него Thread.Sleep(5) спит ~15.6 мс, и цикл не мог
        // держать 60 кадров — картинка и ввод шли рывками.
        bool fineTimer = OperatingSystem.IsWindows() && NativeMethods.timeBeginPeriod(1) == 0;

        try
        {
            Loop();
        }
        finally
        {
            // Сначала дожидаемся, пока уйдёт последний кадр: иначе он мог бы лечь
            // поверх восстановленного экрана терминала.
            _pipeline.Dispose();

            if (fineTimer)
                NativeMethods.timeEndPeriod(1);

            GameSettings.Current.Save();
            _assets.Dispose();
            _shield?.Dispose();
            RestoreTerminal();
        }
    }

    /// <summary>Возвращает терминал и курсор в обычное состояние (выход или Ctrl+C).</summary>
    private void RestoreTerminal()
    {
        _mouse.EndCapture();
        _terminal.DisableMouseReporting();
        _terminal.EnableAutoWrap();
        _terminal.RestoreInputMode();
        _terminal.Restore();
    }

    /// <summary>
    ///     Печатает диагностическую строку и дублирует её в <c>doom_last_run.log</c> рядом с игрой.
    ///     В окне эти строки жить не могут: их затирает очистка экрана и первый кадр, поэтому
    ///     без файла по ним невозможно понять, что произошло при старте.
    /// </summary>
    private static void LogDiagnostics(string line)
    {
        Console.WriteLine(line);

        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "doom_last_run.log");

            // Каждый запуск — свой лог: старый не нужен, а копить его нельзя.
            if (!_diagnosticsStarted)
            {
                _diagnosticsStarted = true;
                File.WriteAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} запуск{Environment.NewLine}");
            }

            File.AppendAllText(path, $"    {line}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // Лог не критичен: игра продолжает работать.
        }
    }

    /// <summary>
    ///     Настраивает терминал: показывает экран загрузки, «отдаляет» картинку и подбирает размер.
    ///     Возвращает <c>false</c>, если окно слишком маленькое.
    /// </summary>
    private bool PrepareTerminal()
    {
        // Сначала разворачиваем окно (просьба игрока: «сначала развернуть, затем отдалять»),
        // затем загрузочный экран прячет «магию» с масштабом.
        if (GameSettings.Current.AutoMaximizeWindow)
        {
            _terminal.MaximizeWindow();
            Thread.Sleep(GameConfig.MaximizeSettleMs);
        }

        // Кто нас показывает — выясняем ДО первого кадра: по этому признаку выбирается и способ
        // смены масштаба, и режим вывода. Переменной WT_SESSION верить нельзя: когда консоль
        // делегирована терминалу, она не выставляется, и Windows Terminal принимался за conhost.
        _terminal.TryDetectTerminalWindow();
        _presenter.UseSynchronizedOutput(
            Terminal.IsRunningInWindowsTerminal || _terminal.TerminalIsWindowsTerminal);

        _terminal.ShowLoadingScreen();
        Thread.Sleep(GameConfig.LoadingScreenMs);

        bool zoomedOut = GameConfig.AutoZoomOut && GameSettings.Current.AutoZoom && _terminal.TryAutoZoomOut();

        // Окно терминала могло найтись только в ходе смены масштаба (первый поиск идёт по
        // заголовку и не всегда успевает) — тогда самое время переключить режим вывода.
        _presenter.UseSynchronizedOutput(
            Terminal.IsRunningInWindowsTerminal || _terminal.TerminalIsWindowsTerminal);

        // Никаких «окно должно быть 1280×720 ячеек»: этот запрос терминал не может выполнить
        // (столько ячеек не помещается на экран), буфер оставался огромным, а видно было лишь
        // его часть — кадр «ехал» сверху вниз и уходил за рамку. Игра просто подстраивается
        // под то окно, которое есть.

        // Ждём, пока разворот окна и смена масштаба устаканятся: замер «в моменте» давал
        // размер, по которому нельзя было рисовать кадр.
        Terminal.WaitForSizeStable();
        _terminal.SyncBufferToWindow();

        Terminal.ReadWindowSize(out int columns, out int rows);
        Resize(columns, rows);

        if (columns < GameConfig.MinimumColumns || rows < GameConfig.MinimumRows)
        {
            Console.WriteLine("Окно консоли слишком маленькое. Нужно минимум 40x15.");
            return false;
        }

        // Диагностика: по ней сразу видно, что именно применилось при старте. Пишем и в файл:
        // на экране строки затираются первым же кадром (а часть — очисткой экрана).
        LogDiagnostics(
            $"[doom] quality={GameSettings.Current.Quality} " +
            $"кадр_×{GameSettings.Current.ZoomColumnsOfBase:0.00} " +
            $"zoom={(zoomedOut ? "ok" : "failed")} size={columns}x{rows} :: {_terminal.LastZoomReport}");

        // Подсказку показываем только если «отдалить» автоматически не получилось.
        if (!zoomedOut)
            _terminal.ShowZoomHint(PollAnyKey);

        // После подсказки и ресайза окно могло измениться — читаем размер ещё раз.
        Terminal.ReadWindowSize(out columns, out rows);
        Resize(columns, rows);

        return true;
    }

    /// <summary>
    ///     Пересоздаёт буферы кадра под новый размер окна. Делается, пока поток вывода
    ///     простаивает: недоотправленный кадр старого размера выбрасывается.
    /// </summary>
    private void Resize(int columns, int rows, bool clearScreen = false)
    {
        _framebuffer = new Framebuffer(columns, rows);
        _viewport = CreateViewport();
        MenuController.ViewSize = (_viewport.Columns, _viewport.PixelRows);

        _pipeline.Exclusive(presenter =>
        {
            presenter.EnsureCapacity(columns * rows);

            if (clearScreen)
                _terminal.ClearScreen();

            // Буфер консоли выравниваем по рамке: вывод в нижнюю строку окна не должен
            // прокручивать буфер — иначе кадр «размазывается» и уходит за рамку.
            _terminal.SyncBufferToWindow();
        });
    }

    /// <summary>Вид — всё, что над строкой состояния (её высота зависит от размера окна).</summary>
    private Viewport CreateViewport()
    {
        int hudRows = _renderer.Hud.RowsFor(_framebuffer.Columns, _framebuffer.Rows);
        return new Viewport(_framebuffer.Columns, Math.Max(1, _framebuffer.Rows - hudRows));
    }

    /// <summary>Загружает карту из ассетов; при ошибке — запасной уровень и строка в лог.</summary>
    private static Level LoadLevel(AssetStore assets, string path)
    {
        string? text = assets.ReadText(path);
        if (text is null)
        {
            LogDiagnostics($"[doom] карта {path} не найдена — запасной уровень");
            return FallbackLevel();
        }

        try
        {
            return MapLoader.Parse(text, TextureMetrics.Parse(assets.ReadText("textures/textures.txt")));
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or InvalidOperationException
                                              or IndexOutOfRangeException or KeyNotFoundException)
        {
            LogDiagnostics($"[doom] карта {path} не читается: {exception.Message} — запасной уровень");
            return FallbackLevel();
        }
    }

    /// <summary>Запасной уровень: пустая комната, чтобы игра запускалась даже без assets/.</summary>
    private static Level FallbackLevel() => MapLoader.Parse("""
        [map]
        name = Запасная комната
        start = 4.5 4.5 -90
        [walls]
        B = brick_red
        [floors]
        f = concrete
        [ceilings]
        c = ceiling_plaster
        [grid walls]
        BBBBBBBBB
        B       B
        B       B
        B       B
        B       B
        B       B
        BBBBBBBBB
        [grid floors]
        fffffffff
        fffffffff
        fffffffff
        fffffffff
        fffffffff
        fffffffff
        fffffffff
        [grid ceilings]
        ccccccccc
        ccccccccc
        ccccccccc
        ccccccccc
        ccccccccc
        ccccccccc
        ccccccccc
        """);

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs args)
    {
        args.Cancel = false;
        _pipeline.Exclusive(_ => RestoreTerminal());
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
                    _player.CycleWeapon(1, nowSeconds);
                    break;
                case ConsoleKey.E:
                    UseDoor();
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
        _player.Reset(_level);
        _projectiles.Reset();
        _pickups.Clear();
        _pickups.AddRange(_level.CreatePickups());
        _shotCounter = 0;
    }

    /// <summary>Читает размер окна и при изменении пересоздаёт буферы. Возвращает <c>true</c>, если размер изменился.</summary>
    private bool TryApplyWindowSize()
    {
        Terminal.ReadWindowSize(out int columns, out int rows);
        if (columns == _framebuffer.Columns && rows == _framebuffer.Rows)
            return false;

        Resize(columns, rows, clearScreen: true);
        return true;
    }

    private void Loop()
    {
        double lastX = double.NaN;
        double lastY = double.NaN;
        double lastAngle = double.NaN;
        double lastPitch = double.NaN;
        double lastEye = double.NaN;
        bool hasRendered = false;

        while (!_quitRequested)
        {
            _frameTimer.Restart();

            // Игрок мог зумить терминал вручную (Ctrl + колесо, Ctrl + −): калибровка
            // восстанавливается раньше, чем кадр подстроится под чужой масштаб.
            if (_terminal.TryRestoreUserZoom())
                hasRendered = false;

            if (TryApplyWindowSize())
                hasRendered = false;

            long nowMs = Environment.TickCount64;
            double nowSeconds = nowMs / 1000.0;

            // Клавиши и кнопки опрашиваются глобально, поэтому без фокуса игра их не слушает.
            bool hadFocus = _hasFocus;
            _hasFocus = _terminal.IsForeground();
            if (_hasFocus != hadFocus)
                hasRendered = false;

            _mouse.HideCursor = GameSettings.Current.HideCursor;
            _mouse.Update(_hasFocus && _screen == GameScreen.Playing);

            // Забираем клавиши и мышь из очереди событий консоли (клавиатура меню,
            // мышь меню, Esc во время игры — всё приходит отсюда) и применяем
            // изменения ассетов: перекрашенный в редакторе PNG подхватывается на лету.
            _input.Poll();
            if (_assets.Poll())
                hasRendered = false;

            if (_screen == GameScreen.Playing)
                HandleGameKeys(nowMs);

            bool redraw;
            MotionDetail motion = MotionDetail.Full;

            if (_screen == GameScreen.Playing)
            {
                double elapsedSeconds = _totalTimer.Elapsed.TotalSeconds;
                double deltaSeconds = Math.Clamp(elapsedSeconds - _previousFrameTime, 0, GameConfig.MaxFrameSeconds);
                _previousFrameTime = elapsedSeconds;

                bool anyInput = UpdateWorld(deltaSeconds, nowSeconds, nowMs);

                bool positionChanged = _player.X != lastX || _player.Y != lastY
                                       || _player.Angle != lastAngle || _player.Pitch != lastPitch
                                       || _player.EyeZ != lastEye;
                bool animationRunning =
                    nowSeconds - _player.LastFireTime < GameConfig.WeaponAnimationSeconds ||
                    nowSeconds < _player.MuzzleFlashUntil ||
                    _player.IsRaisingWeapon(nowSeconds) ||
                    _player.BobIntensity > 0.001 ||
                    _projectiles.IsActive ||
                    !_player.OnGround ||
                    _level.Doors.Any(door => door.IsMoving);

                // Остановились после движения — кадр перерисовывается в полном качестве.
                bool settled = _lastFrameSimplified && !positionChanged;
                redraw = !hasRendered || anyInput || positionChanged || animationRunning || settled;
                motion = positionChanged && hasRendered ? MotionDetailNow() : MotionDetail.Full;

                if (redraw)
                {
                    lastX = _player.X;
                    lastY = _player.Y;
                    lastAngle = _player.Angle;
                    lastPitch = _player.Pitch;
                    lastEye = _player.EyeZ;
                }
            }
            else
            {
                // Меню перерисовывается по событиям (открытие, клавиши, мышь) и когда мигает череп.
                bool menuChanged = ProcessMenuInput();
                int blinkPhase = (int)(nowSeconds / 0.3);
                bool blink = blinkPhase != _menuBlinkPhase;
                _menuBlinkPhase = blinkPhase;
                redraw = !hasRendered || menuChanged || _menuDirty || blink;
                _menuDirty = false;
            }

            UpdateFpsCounter();

            if (redraw)
            {
                RenderFrame(nowSeconds, motion);
                _lastFrameSimplified = motion != MotionDetail.Full;

                // Кадр уходит в терминал отдельным потоком: цикл не ждёт медленный терминал.
                _pipeline.Submit(_framebuffer);
                hasRendered = true;
            }

            SleepToTargetFrameRate();
        }
    }

    /// <summary>Пересчитывает FPS по числу кадров, реально отправленных в терминал.</summary>
    private void UpdateFpsCounter()
    {
        long nowTick = Stopwatch.GetTimestamp();
        long frames = _pipeline.PresentedFrames;

        if (_fpsWindowStartTick == 0)
        {
            _fpsWindowStartTick = nowTick;
            _fpsWindowStartFrames = frames;
            return;
        }

        double seconds = (nowTick - _fpsWindowStartTick) / (double)Stopwatch.Frequency;
        if (seconds < 0.5)
            return;

        _fps = (frames - _fpsWindowStartFrames) / seconds;
        _fpsWindowStartTick = nowTick;
        _fpsWindowStartFrames = frames;
    }

    /// <summary>
    ///     Насколько упростить кадр в движении. Упрощение включено всегда — это оптимизация
    ///     (в движении мелочи не разглядеть, а терминалу уходит заметно меньше данных);
    ///     галка «Грубее в движении» делает его сильнее.
    /// </summary>
    private static MotionDetail MotionDetailNow() =>
        GameSettings.Current.StrongMotionSimplify ? MotionDetail.Strong : MotionDetail.Soft;

    /// <summary>Рисует кадр: сцену и, если открыто меню, наложение поверх неё.</summary>
    private void RenderFrame(double nowSeconds, MotionDetail motion = MotionDetail.Full)
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
            _projectiles.FlashColor,
            motion);

        if (GameSettings.Current.ShowFps)
            _renderer.Hud.DrawFps(_framebuffer, (int)Math.Round(_fps));

        if (_screen != GameScreen.Playing)
            MenuRenderer.Draw(_framebuffer, _menu, _player, _assets, nowSeconds);
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
            case MenuCommand.Apply:
                ApplySettingsNow();
                redraw = true;
                break;
            case MenuCommand.Quit:
                _quitRequested = true;
                redraw = true;
                break;
        }
    }

    /// <summary>
    ///     Применяет настройки немедленно, не дожидаясь перезапуска: сохраняет в файл и
    ///     перевызывает ВСЁ, что от них зависит — масштаб терминала (качество), угол обзора
    ///     (вьюпорт пересоздаётся), захват мыши, а также сбрасывает снимок вывода, чтобы
    ///     кадр перерисовался целиком. Скорость, поворот и чувствительность читаются из
    ///     настроек каждый кадр и подхватываются сами.
    /// </summary>
    private void ApplySettingsNow()
    {
        GameSettings.Current.Save();

        // Качество графики: масштаб терминала / шрифт + пересчёт окна. Пока терминал
        // перестраивается, кадры не шлём; снимок отправленного больше не соответствует
        // экрану — следующий кадр уйдёт полным.
        _pipeline.Exclusive(presenter =>
        {
            _terminal.ApplyQualityOnFly();
            presenter.Reset();
        });

        // Угол обзора зашит в вьюпорт — пересоздаём его под новые настройки.
        _viewport = CreateViewport();

        // Захват мыши применится при возврате в игру: сейчас открыто меню,
        // и курсор должен оставаться свободным для навигации по пунктам.
    }

    /// <summary>Возвращает управление игре и применяет новые настройки (например, угол обзора).</summary>
    private void ResumeGame()
    {
        _screen = GameScreen.Playing;
        _viewport = CreateViewport();
        _previousFrameTime = _totalTimer.Elapsed.TotalSeconds;

        GameSettings.Current.Save();

        // Мышь снова для обзора: отчёт о событиях больше не нужен.
        _terminal.DisableMouseReporting();
        _input.Clear();

        // Захват — по актуальной настройке (галочку могли снять в меню).
        if (GameSettings.Current.CaptureMouse)
            _mouse.BeginCapture();
        else
            _mouse.EndCapture();
    }

    /// <summary>Обновляет игрока, двери и снаряды. Возвращает <c>true</c>, если игрок что-то сделал.</summary>
    private bool UpdateWorld(double deltaSeconds, double nowSeconds, long nowMs)
    {
        // Скорости берём из НАСТРОЕК (а не из констант GameConfig): иначе ползунки
        // в меню ни на что не влияли — игра всегда ходила с заводскими параметрами.
        double rotate = GameSettings.Current.RotationSpeed * deltaSeconds;
        bool anyInput = false;

        double forward = (IsHeld(ConsoleKey.W, nowMs) || IsHeld(ConsoleKey.UpArrow, nowMs) ? 1 : 0)
                         - (IsHeld(ConsoleKey.S, nowMs) || IsHeld(ConsoleKey.DownArrow, nowMs) ? 1 : 0);
        double strafe = (IsHeld(ConsoleKey.D, nowMs) ? 1 : 0) - (IsHeld(ConsoleKey.A, nowMs) ? 1 : 0);
        bool jump = IsHeld(ConsoleKey.Spacebar, nowMs);
        bool isMoving = forward != 0 || strafe != 0;
        anyInput |= isMoving || jump;

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

        // Горизонтальный сдвиг мыши поворачивает камеру, вертикальный — наклоняет взгляд.
        if (_mouse.DeltaX != 0 || _mouse.DeltaY != 0)
        {
            _player.Turn(_mouse.DeltaX * GameSettings.Current.MouseSensitivity);

            // Мышь вверх (DeltaY < 0) — взгляд вверх, без инверсии.
            _player.TurnPitch(-_mouse.DeltaY * GameSettings.Current.MouseSensitivity);
            anyInput = true;
        }

        foreach (Door door in _level.Doors)
            door.Update(deltaSeconds);

        _player.Simulate(_level, forward, strafe, jump, GameSettings.Current.MoveSpeed, deltaSeconds);

        // Упал за край мира — обратно на старт.
        if (_player.Z < -30)
            _player.PlaceAt(_level.StartX, _level.StartY, _level.StartZ, _level.StartAngle);

        // Стрельба — левая кнопка мыши или F (пробел теперь — прыжок).
        bool firePressed = _mouse.LeftButton || IsHeld(ConsoleKey.F, nowMs);
        _player.SetTrigger(firePressed, nowSeconds);
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
        bool walking = isMoving && _player.OnGround;
        if (GameSettings.Current.WeaponBob)
            _player.UpdateWeaponBob(deltaSeconds, walking);
        else if (_player.BobIntensity > 0.0001)
            _player.UpdateWeaponBob(deltaSeconds, false);

        return anyInput;
    }

    /// <summary>Глаза игрока и направление взгляда (курс + наклон).</summary>
    private (Vec3 Eye, Vec3 Direction) ViewRay(double yawOffset = 0, double pitchOffset = 0) =>
        (new Vec3(_player.X, _player.Y, _player.EyeZ),
            Vec3.FromAngles(_player.Angle + yawOffset, _player.Pitch + pitchOffset));

    /// <summary>
    ///     «Использовать» (E): дверь, на которую смотрит игрок, — по лучу взгляда или ближайшая
    ///     перед ним в пределах досягаемости. Створки одной группы открываются вместе.
    /// </summary>
    private void UseDoor()
    {
        (Vec3 eye, Vec3 direction) = ViewRay();
        Door? best = null;
        double bestScore = double.MaxValue;

        foreach (Door door in _level.Doors)
        {
            Vec3 toDoor = door.Center - eye;
            double distance = toDoor.Length;
            if (distance > GameConfig.UseRange + door.Width)
                continue;

            double facing = toDoor.Normalized.Dot(direction);
            if (facing < 0.3)
                continue;

            double score = distance * (2 - facing);
            if (score < bestScore)
            {
                bestScore = score;
                best = door;
            }
        }

        if (best is null)
            return;

        foreach (Door door in _level.Doors)
        {
            if (door.Group == best.Group)
                door.Toggle(_player.X, _player.Y);
        }
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
            // Снаряд вылетает из глаз строго в прицел: туда, куда смотрит игрок (с наклоном).
            (Vec3 eye, Vec3 direction) = ViewRay();
            _projectiles.Launch(info.Projectile, eye, direction);
        }
        else
        {
            FireHitscan(info);
        }

        // Вспышка выстрела подсвечивает кадр: у ракетницы и BFG заметно, у пистолета едва.
        _projectiles.AddFlash(info.MuzzleFlash, info.MuzzleFlashSeconds + 0.06, info.MuzzleFlashColor);
    }

    /// <summary>Мгновенный выстрел: каждая дробинка — луч из глаз; искра там, где он упёрся в мир.</summary>
    private void FireHitscan(WeaponInfo info)
    {
        for (int pellet = 0; pellet < info.Pellets; pellet++)
        {
            double spreadOffset = info.Pellets > 1
                ? info.Spread * (pellet / (double)(info.Pellets - 1) - 0.5)
                : 0.0;

            // Разброс детерминированный: одно и то же число выстрелов даёт ту же картинку.
            double jitter = (SpreadNoise.Unit(pellet, _shotCounter) - 0.5) * info.Spread * 0.6;
            double vertical = info.Pellets > 1 ? (SpreadNoise.Unit(_shotCounter, pellet) - 0.5) * info.Spread * 0.5 : 0;

            (Vec3 eye, Vec3 direction) = ViewRay(spreadOffset + jitter, vertical);
            if (_level.RayCast(eye, direction, 200, out RayHit hit))
                _projectiles.SpawnHitSpark(ImpactKind.BulletSpark, hit.Point, hit.Normal);
        }

        _shotCounter++;
    }

    /// <summary>Рукопашная атака: искра появляется, если поверхность ближе вытянутой руки.</summary>
    private void FireMelee()
    {
        (Vec3 eye, Vec3 direction) = ViewRay();
        if (_level.RayCast(eye, direction, GameConfig.MeleeRange, out RayHit hit))
            _projectiles.SpawnHitSpark(ImpactKind.MeleeSpark, hit.Point, hit.Normal);
    }

    /// <summary>Удерживается ли клавиша. Без фокуса окна — нет: опрос клавиатуры глобальный.</summary>
    private bool IsHeld(ConsoleKey key, long nowMs) => _hasFocus && _keyboard.IsHeld(key, nowMs);

    /// <summary>Ограничивает частоту кадров ~60 FPS.</summary>
    private void SleepToTargetFrameRate()
    {
        // Thread.Sleep точен до ~1 мс только с timeBeginPeriod(1) (см. Run); остаток кадра
        // досыпаем короткими паузами, чтобы не проспать дедлайн.
        while (true)
        {
            double remainingMs = GameConfig.TargetFrameMs - _frameTimer.Elapsed.TotalMilliseconds;
            if (remainingMs <= 0.25)
                return;

            Thread.Sleep(remainingMs > 2.0 ? (int)(remainingMs - 1.0) : 0);
        }
    }
}

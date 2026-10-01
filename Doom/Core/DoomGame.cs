using System.Diagnostics;
using Doom.Configuration;
using Doom.Gameplay;
using Doom.Input;
using Doom.Platform;
using Doom.Rendering;
using Doom.World;

namespace Doom.Core;

/// <summary>
///     Главный цикл игры: ввод, симуляция игрока, рендер кадра и отправка его в терминал.
/// </summary>
internal sealed class DoomGame
{
    private readonly Terminal _terminal = new();
    private readonly KeyboardState _keyboard = new();
    private readonly Level _level = Level.CreateDefault();
    private readonly Player _player = new();
    private readonly List<Pickup> _pickups = new();
    private readonly SceneRenderer _renderer = new();
    private readonly ProjectileSystem _projectiles = new();
    private readonly ConsolePresenter _presenter;
    private readonly Stopwatch _totalTimer = new();
    private readonly Stopwatch _frameTimer = new();

    private Framebuffer _framebuffer = new(1, 1);
    private Viewport _viewport;
    private double _previousFrameTime;

    /// <summary>Счётчик выстрелов: по нему считается детерминированный разброс дробинок.</summary>
    private int _shotCounter;

    public DoomGame()
    {
        _presenter = new ConsolePresenter(_terminal);
        _pickups.AddRange(PickupLayout.CreateDefault());
    }

    public void Run()
    {
        if (!PrepareTerminal())
            return;

        _terminal.HideCursorAndClearScreen();
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
            _terminal.Restore();
        }
    }

    /// <summary>
    ///     Подбирает шрифт и размер окна. Возвращает <c>false</c>, если окно слишком маленькое.
    /// </summary>
    private bool PrepareTerminal()
    {
        bool fontChanged = _terminal.TrySetFontSize(GameConfig.DesiredFontSize);
        _terminal.TryResize(GameConfig.DesiredColumns, GameConfig.DesiredRows);
        Thread.Sleep(GameConfig.StartupDelayMs);

        Terminal.ReadWindowSize(out int columns, out int rows);
        Resize(columns, rows);

        if (columns < GameConfig.MinimumColumns || rows < GameConfig.MinimumRows)
        {
            Console.WriteLine("Окно консоли слишком маленькое. Нужно минимум 40x15.");
            return false;
        }

        if (Terminal.IsRunningInWindowsTerminal && !fontChanged)
            _terminal.ShowZoomHint();

        // После подсказки и ресайза окно могло измениться — читаем размер ещё раз.
        Terminal.ReadWindowSize(out columns, out rows);
        Resize(columns, rows);

        return true;
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
        _terminal.Restore();
    }

    /// <summary>Обрабатывает нажатия клавиш. Возвращает <c>true</c>, если игру нужно завершить.</summary>
    private bool HandleKeys(long nowMs)
    {
        double nowSeconds = nowMs / 1000.0;

        while (Console.KeyAvailable)
        {
            ConsoleKey key = Console.ReadKey(true).Key;

            switch (key)
            {
                case ConsoleKey.Escape:
                    _terminal.Restore();
                    return true;
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

        return false;
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

        while (true)
        {
            _frameTimer.Restart();

            if (TryApplyWindowSize())
                hasRendered = false;

            long nowMs = Environment.TickCount64;
            double nowSeconds = nowMs / 1000.0;

            if (HandleKeys(nowMs))
                return;

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

            if (!hasRendered || anyInput || positionChanged || animationRunning)
            {
                lastX = _player.X;
                lastY = _player.Y;
                lastAngle = _player.Angle;

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

                _presenter.Present(_framebuffer);
                hasRendered = true;
            }

            SleepToTargetFrameRate();
        }
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

        if (IsHeld(ConsoleKey.Spacebar, nowMs) && _player.TryFire(nowSeconds, out WeaponInfo info))
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
        _player.UpdateWeaponBob(deltaSeconds, isMoving);

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

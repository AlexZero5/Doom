using Doom.Configuration;
using Doom.World;

namespace Doom.Gameplay;

/// <summary>
///     Состояние игрока: позиция, направление взгляда, арсенал, боеприпасы
///     и анимации оружия от первого лица.
/// </summary>
internal sealed class Player
{
    private readonly int[] _ammo = new int[AmmoCatalog.Count];
    private readonly bool[] _owned = new bool[WeaponCatalog.Count];

    public Player() => Reset();

    public double X { get; private set; }

    public double Y { get; private set; }

    /// <summary>Высота ног (метры).</summary>
    public double Z { get; private set; }

    /// <summary>Высота глаз: ноги (сглаженные на ступенях) + рост глаз.</summary>
    public double EyeZ => _eyeFeet + GameConfig.EyeHeight;

    /// <summary>Вертикальная скорость (м/с): прыжок, падение, лестница.</summary>
    public double VelocityZ { get; private set; }

    /// <summary>Стоит на опоре.</summary>
    public bool OnGround { get; private set; } = true;

    /// <summary>В объёме вертикальной лестницы.</summary>
    public bool Climbing { get; private set; }

    private double _eyeFeet;

    /// <summary>Направление взгляда в радианах.</summary>
    public double Angle { get; private set; }

    /// <summary>Наклон взгляда вверх (плюс) или вниз (минус) в радианах.</summary>
    public double Pitch { get; private set; }

    public WeaponKind CurrentWeapon { get; private set; }

    /// <summary>Время последнего выстрела в секундах от старта.</summary>
    public double LastFireTime { get; private set; }

    /// <summary>Время последней смены оружия (для анимации подъёма ствола).</summary>
    public double LastSwitchTime { get; private set; }

    public double MuzzleFlashUntil { get; private set; }

    /// <summary>Фаза покачивания оружия при ходьбе.</summary>
    public double WeaponBobTime { get; private set; }

    /// <summary>Текущая амплитуда покачивания (0..1).</summary>
    public double BobIntensity { get; private set; }

    /// <summary>Фаза вращения стволов пулемёта и полотна бензопилы.</summary>
    public double SpinPhase { get; private set; }

    /// <summary>Кулак бьёт по очереди слева и справа (как в Doom).</summary>
    public bool SwingFromLeft { get; private set; }

    /// <summary>Сколько выстрелов сделано (чередует кадры пулемёта).</summary>
    public int ShotsFired { get; private set; }

    /// <summary>Здоровье и броня — для строки состояния (урона пока нет: врагов нет).</summary>
    public int Health { get; private set; }

    public int Armor { get; private set; }

    /// <summary>Когда подобрано новое оружие (лицо в строке состояния ухмыляется).</summary>
    public double WeaponPickupTime { get; private set; }

    /// <summary>С какого момента зажат спуск (NaN — отпущен): долгая стрельба злит лицо.</summary>
    public double TriggerHeldSince { get; private set; } = double.NaN;

    /// <summary>Отмечает, зажат ли спуск в этом кадре.</summary>
    public void SetTrigger(bool pressed, double nowSeconds)
    {
        if (!pressed)
            TriggerHeldSince = double.NaN;
        else if (double.IsNaN(TriggerHeldSince))
            TriggerHeldSince = nowSeconds;
    }

    public int Bullets => AmmoOf(AmmoKind.Bullets);

    public int Shells => AmmoOf(AmmoKind.Shells);

    public int Rockets => AmmoOf(AmmoKind.Rockets);

    public int Cells => AmmoOf(AmmoKind.Cells);

    public int AmmoOf(AmmoKind kind) => _ammo[(int)kind];

    public bool Owns(WeaponKind kind) => _owned[(int)kind];

    /// <summary>
    ///     Возвращает игрока в стартовое состояние: кулак, пистолет и 50 патронов. Точка старта —
    ///     из карты (без карты — запасная из GameConfig).
    /// </summary>
    public void Reset(Level? level = null)
    {
        PlaceAt(level?.StartX ?? GameConfig.StartX, level?.StartY ?? GameConfig.StartY, level?.StartZ ?? 0,
            level?.StartAngle ?? GameConfig.StartAngle);

        Array.Clear(_ammo);
        Array.Clear(_owned);
        _ammo[(int)AmmoKind.Bullets] = GameConfig.InitialBullets;
        _owned[(int)WeaponKind.Fist] = true;
        _owned[(int)WeaponKind.Pistol] = true;

        CurrentWeapon = WeaponKind.Pistol;
        LastFireTime = -10.0;
        LastSwitchTime = double.NegativeInfinity;
        MuzzleFlashUntil = 0;
        WeaponBobTime = 0;
        BobIntensity = 0;
        SpinPhase = 0;
        SwingFromLeft = false;
        ShotsFired = 0;
        Health = 100;
        Armor = 0;
        WeaponPickupTime = -10;
        TriggerHeldSince = double.NaN;
    }

    // ============================================================
    //   Движение и физика
    // ============================================================

    /// <summary>
    ///     Шаг физики: ходьба (<paramref name="forward" />, <paramref name="strafe" /> ∈ [−1, 1]),
    ///     прыжок, гравитация, подъём на ступени, лазанье по лестницам. Игрок — вертикальный
    ///     цилиндр: радиус <see cref="GameConfig.PlayerRadius" />, рост <see cref="GameConfig.PlayerHeight" />.
    /// </summary>
    public void Simulate(Level level, double forward, double strafe, bool jump, double speed, double deltaSeconds)
    {
        const double radius = GameConfig.PlayerRadius;
        const double height = GameConfig.PlayerHeight;

        // Лестница: в её объёме гравитации нет, W/S — вверх/вниз.
        Climbing = level.LadderAt(X, Y, radius, Z, Z + height) is not null;

        double moveX = Math.Cos(Angle) * forward + Math.Cos(Angle + Math.PI / 2) * strafe;
        double moveY = Math.Sin(Angle) * forward + Math.Sin(Angle + Math.PI / 2) * strafe;
        double length = Math.Sqrt(moveX * moveX + moveY * moveY);
        if (length > 1)
        {
            moveX /= length;
            moveY /= length;
        }

        // Мелкими шагами, чтобы не проскочить тонкую стенку на большой скорости.
        double distance = speed * deltaSeconds;
        int substeps = Math.Max(1, (int)Math.Ceiling(distance * Math.Max(Math.Abs(moveX), Math.Abs(moveY)) / 0.1));
        for (int step = 0; step < substeps; step++)
        {
            MoveAxis(level, moveX * distance / substeps, 0);
            MoveAxis(level, 0, moveY * distance / substeps);
        }

        // Вертикаль.
        if (Climbing)
            VelocityZ = forward * GameConfig.ClimbSpeed;
        else
            VelocityZ -= GameConfig.Gravity * deltaSeconds;

        if (jump && (OnGround || Climbing))
        {
            VelocityZ = GameConfig.JumpSpeed;
            OnGround = false;
        }

        double nextZ = Z + VelocityZ * deltaSeconds;
        if (VelocityZ > 0)
        {
            double ceiling = level.CeilingHeight(X, Y, radius, Z + height - 0.05);
            if (nextZ + height > ceiling)
            {
                nextZ = Math.Max(Z, ceiling - height);
                VelocityZ = 0;
            }
        }

        double ground = level.GroundHeight(X, Y, radius, Math.Max(Z, nextZ) + 0.05);
        bool wasOnGround = OnGround;
        if (nextZ <= ground)
        {
            Z = ground;
            VelocityZ = 0;
            OnGround = true;
        }
        else if (wasOnGround && VelocityZ <= 0 && !Climbing && Z - ground <= GameConfig.StepHeight)
        {
            // Вниз по ступеням — не подпрыгивая на каждой.
            Z = ground;
            VelocityZ = 0;
            OnGround = true;
        }
        else
        {
            Z = nextZ;
            OnGround = false;
        }

        // Камера догоняет ноги плавно на ступенях и мгновенно — при падении и прыжке.
        double lag = Z - _eyeFeet;
        _eyeFeet = OnGround && Math.Abs(lag) < 0.5 ? _eyeFeet + lag * Math.Min(1, deltaSeconds * 14) : Z;
    }

    /// <summary>Сдвиг по одной оси со скольжением вдоль стен и подъёмом на невысокие уступы.</summary>
    private void MoveAxis(Level level, double dx, double dy)
    {
        if (dx == 0 && dy == 0)
            return;

        double nextX = X + dx, nextY = Y + dy;
        if (!BlockedAt(level, nextX, nextY, Z))
        {
            X = nextX;
            Y = nextY;
            return;
        }

        if (!OnGround && !Climbing)
            return;

        // Ступенька: опора впереди не выше StepHeight и над ней есть место.
        double step = level.GroundHeight(nextX, nextY, GameConfig.PlayerRadius, Z + GameConfig.StepHeight);
        if (step > Z && !BlockedAt(level, nextX, nextY, step))
        {
            X = nextX;
            Y = nextY;
            Z = step;
        }
    }

    private static bool BlockedAt(Level level, double x, double y, double feet) =>
        level.Blocked(x, y, GameConfig.PlayerRadius, feet + 0.01, feet + GameConfig.PlayerHeight);

    /// <summary>Ставит игрока в точку (старт, рестарт, телепорт стенда).</summary>
    public void PlaceAt(double x, double y, double z, double angle, double pitch = 0)
    {
        X = x;
        Y = y;
        Z = z;
        _eyeFeet = z;
        Angle = angle;
        Pitch = pitch;
        VelocityZ = 0;
        OnGround = true;
    }

    /// <summary>Поворачивает взгляд; угол держится в [0, 2π), чтобы не терять точность за долгую игру.</summary>
    public void Turn(double deltaRadians)
    {
        double angle = (Angle + deltaRadians) % Math.Tau;
        Angle = angle < 0 ? angle + Math.Tau : angle;
    }

    /// <summary>Наклоняет взгляд по вертикали, удерживая угол в разумных пределах.</summary>
    public void TurnPitch(double deltaRadians) =>
        Pitch = Math.Clamp(Pitch + deltaRadians, -GameConfig.MaxPitchRadians, GameConfig.MaxPitchRadians);

    // ============================================================
    //   Читы: оружие и боеприпасы (используются экраном «Читы»)
    // ============================================================

    /// <summary>Выдаёт оружие.</summary>
    public void Grant(WeaponKind kind) => _owned[(int)kind] = true;

    /// <summary>Забирает оружие. Кулак отобрать нельзя, а вместо текущего ствола берётся другой.</summary>
    public void Revoke(WeaponKind kind)
    {
        if (kind == WeaponKind.Fist)
            return;

        _owned[(int)kind] = false;

        if (kind == CurrentWeapon)
            SelectFallback();
    }

    /// <summary>Выдаёт или забирает оружие.</summary>
    public void SetOwned(WeaponKind kind, bool owned)
    {
        if (owned)
            Grant(kind);
        else
            Revoke(kind);
    }

    /// <summary>Задаёт запас боеприпасов, зажимая значение в допустимый диапазон.</summary>
    public void SetAmmo(AmmoKind kind, int amount)
    {
        if (kind == AmmoKind.None)
            return;

        _ammo[(int)kind] = Math.Clamp(amount, 0, AmmoCatalog.Capacity(kind));
    }

    /// <summary>«Дать всё»: всё оружие и полный боезапас.</summary>
    public void GiveAll()
    {
        for (int index = 0; index < _owned.Length; index++)
            _owned[index] = true;

        for (int index = 1; index < _ammo.Length; index++)
            _ammo[index] = AmmoCatalog.Capacity((AmmoKind)index);
    }

    /// <summary>Берёт лучший доступный ствол, если текущий отобрали (мгновенно, без подъёма).</summary>
    private void SelectFallback()
    {
        for (int index = WeaponCatalog.Count - 1; index >= 0; index--)
        {
            var candidate = (WeaponKind)index;
            if (!Owns(candidate))
                continue;

            CurrentWeapon = candidate;
            LastSwitchTime = double.NegativeInfinity;
            return;
        }

        CurrentWeapon = WeaponKind.Fist;
    }

    /// <summary>Берёт оружие, если оно есть у игрока. Возвращает <c>true</c>, если ствол сменился.</summary>
    public bool SelectWeapon(WeaponKind kind, double nowSeconds)
    {
        if (!Owns(kind) || kind == CurrentWeapon)
            return false;

        CurrentWeapon = kind;
        LastSwitchTime = nowSeconds;
        return true;
    }

    /// <summary>Выбирает оружие слота клавиши 1..7 — первое из имеющихся.</summary>
    public bool SelectSlot(int slot, double nowSeconds)
    {
        foreach (WeaponKind kind in WeaponCatalog.SlotWeapons(slot))
        {
            if (Owns(kind))
                return SelectWeapon(kind, nowSeconds);
        }

        return false;
    }

    /// <summary>Переключает оружие на следующее (<paramref name="direction" /> = ±1) из имеющихся.</summary>
    public bool CycleWeapon(int direction, double nowSeconds)
    {
        int current = (int)CurrentWeapon;

        for (int step = 1; step <= WeaponCatalog.Count; step++)
        {
            int index = ((current + direction * step) % WeaponCatalog.Count + WeaponCatalog.Count)
                        % WeaponCatalog.Count;
            var candidate = (WeaponKind)index;

            if (Owns(candidate))
                return SelectWeapon(candidate, nowSeconds);
        }

        return false;
    }

    /// <summary>
    ///     Если у текущего оружия кончились патроны, берёт лучшее из доступных
    ///     (в худшем случае — кулак). Возвращает <c>true</c>, если ствол сменился.
    /// </summary>
    public bool EnsureUsableWeapon(double nowSeconds)
    {
        if (HasAmmoFor(WeaponCatalog.Get(CurrentWeapon)))
            return false;

        for (int index = WeaponCatalog.Count - 1; index >= 0; index--)
        {
            var candidate = (WeaponKind)index;
            if (candidate == CurrentWeapon || !Owns(candidate))
                continue;

            if (HasAmmoFor(WeaponCatalog.Get(candidate)))
                return SelectWeapon(candidate, nowSeconds);
        }

        return false;
    }

    /// <summary>Выстрел с учётом подъёма ствола, перезарядки и наличия боеприпасов.</summary>
    public bool TryFire(double nowSeconds, out WeaponInfo info)
    {
        info = WeaponCatalog.Get(CurrentWeapon);

        if (IsRaisingWeapon(nowSeconds) || nowSeconds - LastFireTime < info.Cooldown)
            return false;

        if (info.Ammo != AmmoKind.None)
        {
            if (_ammo[(int)info.Ammo] < info.AmmoPerShot)
                return false;

            _ammo[(int)info.Ammo] -= info.AmmoPerShot;
        }

        LastFireTime = nowSeconds;
        ShotsFired++;
        MuzzleFlashUntil = nowSeconds + info.MuzzleFlashSeconds;

        if (info.IsMelee)
            SwingFromLeft = !SwingFromLeft;

        if (CurrentWeapon == WeaponKind.Chaingun)
            SpinPhase = WrapPhase(SpinPhase + GameConfig.ChaingunSpinPerShot);
        else if (CurrentWeapon == WeaponKind.Chainsaw)
            SpinPhase = WrapPhase(SpinPhase + GameConfig.ChainsawSpinPerShot);

        return true;
    }

    /// <summary>
    ///     Подбирает бонусы рядом с игроком. Возвращает <c>true</c>, если что-то подобрано.
    ///     Боеприпасы при полном запасе остаются лежать, а найденное оружие поднимается сразу
    ///     (как в Doom).
    /// </summary>
    public bool CollectPickups(IReadOnlyList<Pickup> pickups, double nowSeconds)
    {
        bool collected = false;
        const double radiusSquared = GameConfig.PickupRadius * GameConfig.PickupRadius;

        foreach (Pickup pickup in pickups)
        {
            if (pickup.Taken)
                continue;

            double dx = pickup.X - X;
            double dy = pickup.Y - Y;
            if (dx * dx + dy * dy > radiusSquared || pickup.Z < Z - 0.6 || pickup.Z > Z + 1.2)
                continue;

            PickupInfo info = PickupCatalog.Get(pickup.Kind);

            if (info.Weapon is WeaponKind weapon)
            {
                bool alreadyOwned = Owns(weapon);
                _owned[(int)weapon] = true;
                AddAmmo(info.Ammo, WeaponCatalog.Get(weapon).PickupAmmo);

                // Как в Doom: найденное оружие поднимается сразу, а знакомое — нет.
                if (!alreadyOwned)
                {
                    SelectWeapon(weapon, nowSeconds);
                    WeaponPickupTime = nowSeconds;
                }
            }
            else
            {
                if (IsAmmoFull(info.Ammo))
                    continue;

                AddAmmo(info.Ammo, pickup.Amount);
            }

            pickup.Taken = true;
            collected = true;
        }

        return collected;
    }

    /// <summary>Прогресс подъёма ствола после смены оружия: 0 — только что выбрано, 1 — ствол поднят.</summary>
    public double RaiseProgress(double nowSeconds)
    {
        double progress = (nowSeconds - LastSwitchTime) / GameConfig.WeaponSwitchSeconds;
        return progress <= 0 ? 0 : progress >= 1 ? 1 : progress;
    }

    public bool IsRaisingWeapon(double nowSeconds) => RaiseProgress(nowSeconds) < 1.0;

    /// <summary>Плавно разгоняет и гасит покачивание оружия в зависимости от ходьбы.</summary>
    public void UpdateWeaponBob(double deltaSeconds, bool isMoving)
    {
        if (isMoving)
        {
            WeaponBobTime += deltaSeconds;
            BobIntensity = Math.Min(1.0, BobIntensity + deltaSeconds * GameConfig.BobRisePerSecond);
        }
        else
        {
            BobIntensity = Math.Max(0.0, BobIntensity - deltaSeconds * GameConfig.BobFallPerSecond);
        }

        if (WeaponBobTime > GameConfig.BobTimeWrap)
            WeaponBobTime -= GameConfig.BobTimeWrap;
    }

    private bool HasAmmoFor(WeaponInfo info) =>
        info.Ammo == AmmoKind.None || _ammo[(int)info.Ammo] >= info.AmmoPerShot;

    private bool IsAmmoFull(AmmoKind kind) =>
        kind != AmmoKind.None && _ammo[(int)kind] >= AmmoCatalog.Capacity(kind);

    private void AddAmmo(AmmoKind kind, int amount)
    {
        if (kind == AmmoKind.None || amount <= 0)
            return;

        _ammo[(int)kind] = Math.Min(AmmoCatalog.Capacity(kind), _ammo[(int)kind] + amount);
    }

    /// <summary>Вращение стволов и полотна пилы: фаза растёт с каждым выстрелом.</summary>
    private static double WrapPhase(double phase) =>
        phase > GameConfig.SpinPhaseWrap ? phase - GameConfig.SpinPhaseWrap : phase;
}


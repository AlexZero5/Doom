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

    /// <summary>Направление взгляда в радианах.</summary>
    public double Angle { get; private set; }

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

    public int Bullets => AmmoOf(AmmoKind.Bullets);

    public int Shells => AmmoOf(AmmoKind.Shells);

    public int Rockets => AmmoOf(AmmoKind.Rockets);

    public int Cells => AmmoOf(AmmoKind.Cells);

    public int AmmoOf(AmmoKind kind) => _ammo[(int)kind];

    public bool Owns(WeaponKind kind) => _owned[(int)kind];

    /// <summary>Возвращает игрока в стартовое состояние: кулак, пистолет и 50 патронов.</summary>
    public void Reset()
    {
        X = GameConfig.StartX;
        Y = GameConfig.StartY;
        Angle = GameConfig.StartAngle;

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
    }

    /// <summary>Двигает игрока в направлении <paramref name="angle" />, скользя по стенам.</summary>
    public void Move(Level level, double angle, double distance)
    {
        double nextX = X + Math.Cos(angle) * distance;
        double nextY = Y + Math.Sin(angle) * distance;

        if (level.IsWalkable(nextX, Y))
            X = nextX;

        if (level.IsWalkable(X, nextY))
            Y = nextY;
    }

    public void Turn(double deltaRadians) => Angle += deltaRadians;

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
            if (dx * dx + dy * dy > radiusSquared)
                continue;

            PickupInfo info = PickupCatalog.Get(pickup.Kind);

            if (info.Weapon is WeaponKind weapon)
            {
                bool alreadyOwned = Owns(weapon);
                _owned[(int)weapon] = true;
                AddAmmo(info.Ammo, WeaponCatalog.Get(weapon).PickupAmmo);

                // Как в Doom: найденное оружие поднимается сразу, а знакомое — нет.
                if (!alreadyOwned)
                    SelectWeapon(weapon, nowSeconds);
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


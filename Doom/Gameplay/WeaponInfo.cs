using Doom.Configuration;

namespace Doom.Gameplay;

/// <summary>Характеристики оружия: боеприпасы, темп стрельбы, разброс и отдача.</summary>
internal readonly struct WeaponInfo
{
    public WeaponInfo(
        string name,
        AmmoKind ammo,
        int ammoPerShot,
        double cooldown,
        int pellets = 1,
        double spread = 0.0,
        bool melee = false,
        ProjectileKind projectile = ProjectileKind.None,
        double recoil = 5.0,
        int pickupAmmo = 0,
        double muzzleFlashSeconds = GameConfig.MuzzleFlashSeconds,
        double muzzleFlash = 0.0,
        int muzzleFlashColor = 0)
    {
        Name = name;
        Ammo = ammo;
        AmmoPerShot = ammoPerShot;
        Cooldown = cooldown;
        Pellets = pellets;
        Spread = spread;
        IsMelee = melee;
        Projectile = projectile;
        Recoil = recoil;
        PickupAmmo = pickupAmmo;
        MuzzleFlashSeconds = muzzleFlashSeconds;
        MuzzleFlash = muzzleFlash;
        MuzzleFlashColor = muzzleFlashColor;
    }

    public string Name { get; }

    /// <summary>Тип расходуемых боеприпасов (<see cref="AmmoKind.None" /> — рукопашная).</summary>
    public AmmoKind Ammo { get; }

    public int AmmoPerShot { get; }

    /// <summary>Минимальный интервал между выстрелами в секундах.</summary>
    public double Cooldown { get; }

    /// <summary>Сколько лучей трассирует выстрел (у дробовика — несколько дробинок).</summary>
    public int Pellets { get; }

    /// <summary>Полный угол разброса в радианах (0 — точный выстрел).</summary>
    public double Spread { get; }

    public bool IsMelee { get; }

    /// <summary>Тип снаряда, если оружие стреляет не мгновенно.</summary>
    public ProjectileKind Projectile { get; }

    /// <summary>Амплитуда отдачи в базовых строках экрана.</summary>
    public double Recoil { get; }

    /// <summary>Сколько боеприпасов даёт подбор самого оружия.</summary>
    public int PickupAmmo { get; }

    public double MuzzleFlashSeconds { get; }

    /// <summary>Насколько выстрел подсвечивает кадр (0 — без вспышки).</summary>
    public double MuzzleFlash { get; }

    /// <summary>Цвет вспышки выстрела — часть описания оружия.</summary>
    public int MuzzleFlashColor { get; }
}

namespace Doom.Gameplay;

/// <summary>
///     Каталог оружия: порядок элементов совпадает с <see cref="WeaponKind" />,
///     характеристики близки к оригинальному Doom (темп стрельбы, дробь, разброс).
/// </summary>
internal static class WeaponCatalog
{
    /// <summary>Количество видов оружия.</summary>
    public const int Count = 8;

    private static readonly WeaponInfo[] Weapons =
    {
        // Слот 1: кулак — медленный, но бесконечный.
        new("FIST", AmmoKind.None, ammoPerShot: 0, cooldown: 0.40, spread: 0.05, melee: true, recoil: 2.0),

        // Слот 1: бензопила — та же рукопашная, но вдвое быстрее кулака.
        new("CHAINSAW", AmmoKind.None, ammoPerShot: 0, cooldown: 0.11, spread: 0.10, melee: true, recoil: 0.0),

        // Слот 2: пистолет — стартовое оружие.
        new("PISTOL", AmmoKind.Bullets, ammoPerShot: 1, cooldown: 0.35, spread: 0.012, recoil: 5.0,
            muzzleFlash: 0.04, muzzleFlashColor: 0xFFE0A0),

        // Слот 3: дробовик — семь дробинок в широком конусе.
        new("SHOTGUN", AmmoKind.Shells, ammoPerShot: 1, cooldown: 0.85, pellets: 7, spread: 0.26, recoil: 7.0,
            pickupAmmo: 8, muzzleFlash: 0.08, muzzleFlashColor: 0xFFD070),

        // Слот 4: пулемёт — очень частый огонь, небольшой разброс.
        new("CHAINGUN", AmmoKind.Bullets, ammoPerShot: 1, cooldown: 0.09, spread: 0.045, recoil: 3.0,
            pickupAmmo: 20, muzzleFlash: 0.04, muzzleFlashColor: 0xFFE0A0),

        // Слот 5: ракетница — снаряд с взрывом и засветкой экрана.
        new("ROCKET LAUNCHER", AmmoKind.Rockets, ammoPerShot: 1, cooldown: 0.80, projectile: ProjectileKind.Rocket,
            recoil: 9.0, pickupAmmo: 2, muzzleFlashSeconds: 0.12, muzzleFlash: 0.14, muzzleFlashColor: 0xFFA050),

        // Слот 6: плазменная винтовка — быстрые светящиеся снаряды.
        new("PLASMA RIFLE", AmmoKind.Cells, ammoPerShot: 1, cooldown: 0.10, projectile: ProjectileKind.PlasmaBolt,
            recoil: 2.0, pickupAmmo: 40, muzzleFlashSeconds: 0.08, muzzleFlash: 0.08, muzzleFlashColor: 0x60FFA0),

        // Слот 7: BFG9000 — сорок ячеек за выстрел и самая сильная вспышка.
        new("BFG9000", AmmoKind.Cells, ammoPerShot: 40, cooldown: 1.50, projectile: ProjectileKind.BfgBall,
            recoil: 12.0, pickupAmmo: 40, muzzleFlashSeconds: 0.22, muzzleFlash: 0.30, muzzleFlashColor: 0x90FF60)
    };

    private static readonly WeaponKind[] SlotOne = { WeaponKind.Chainsaw, WeaponKind.Fist };
    private static readonly WeaponKind[] SlotTwo = { WeaponKind.Pistol };
    private static readonly WeaponKind[] SlotThree = { WeaponKind.Shotgun };
    private static readonly WeaponKind[] SlotFour = { WeaponKind.Chaingun };
    private static readonly WeaponKind[] SlotFive = { WeaponKind.RocketLauncher };
    private static readonly WeaponKind[] SlotSix = { WeaponKind.PlasmaRifle };
    private static readonly WeaponKind[] SlotSeven = { WeaponKind.Bfg9000 };
    private static readonly WeaponKind[] NoWeapons = Array.Empty<WeaponKind>();

    public static WeaponInfo Get(WeaponKind kind) => Weapons[(int)kind];

    /// <summary>Номер слота клавиши (1..7). У кулака и бензопилы общий слот 1.</summary>
    public static int Slot(WeaponKind kind) => kind switch
    {
        WeaponKind.Fist or WeaponKind.Chainsaw => 1,
        WeaponKind.Pistol => 2,
        WeaponKind.Shotgun => 3,
        WeaponKind.Chaingun => 4,
        WeaponKind.RocketLauncher => 5,
        WeaponKind.PlasmaRifle => 6,
        _ => 7
    };

    /// <summary>
    ///     Оружие слота в порядке предпочтения: если есть бензопила, клавиша 1 выбирает её,
    ///     иначе — кулак (как в Doom).
    /// </summary>
    public static WeaponKind[] SlotWeapons(int slot) => slot switch
    {
        1 => SlotOne,
        2 => SlotTwo,
        3 => SlotThree,
        4 => SlotFour,
        5 => SlotFive,
        6 => SlotSix,
        7 => SlotSeven,
        _ => NoWeapons
    };
}

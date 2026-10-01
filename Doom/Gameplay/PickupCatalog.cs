using Doom.World;

namespace Doom.Gameplay;

/// <summary>Что даёт бонус: боеприпасы или оружие (вместе с оружием — немного патронов).</summary>
internal readonly struct PickupInfo
{
    public PickupInfo(string name, AmmoKind ammo, WeaponKind? weapon = null)
    {
        Name = name;
        Ammo = ammo;
        Weapon = weapon;
    }

    /// <summary>Название для строки состояния.</summary>
    public string Name { get; }

    /// <summary>Тип пополняемых боеприпасов.</summary>
    public AmmoKind Ammo { get; }

    /// <summary>Оружие, если бонус — это оружие, а не боеприпасы.</summary>
    public WeaponKind? Weapon { get; }
}

/// <summary>
///     Связь бонусов уровня с оружием и боеприпасами игрока:
///     порядок элементов совпадает с <see cref="PickupKind" />.
/// </summary>
internal static class PickupCatalog
{
    private static readonly PickupInfo[] Items =
    {
        new("CLIP", AmmoKind.Bullets),
        new("BULLET BOX", AmmoKind.Bullets),
        new("SHELLS", AmmoKind.Shells),
        new("SHELL BOX", AmmoKind.Shells),
        new("ROCKET", AmmoKind.Rockets),
        new("ROCKET BOX", AmmoKind.Rockets),
        new("ENERGY CELL", AmmoKind.Cells),
        new("CELL PACK", AmmoKind.Cells),
        new("CHAINSAW", AmmoKind.None, WeaponKind.Chainsaw),
        new("SHOTGUN", AmmoKind.Shells, WeaponKind.Shotgun),
        new("CHAINGUN", AmmoKind.Bullets, WeaponKind.Chaingun),
        new("ROCKET LAUNCHER", AmmoKind.Rockets, WeaponKind.RocketLauncher),
        new("PLASMA RIFLE", AmmoKind.Cells, WeaponKind.PlasmaRifle),
        new("BFG9000", AmmoKind.Cells, WeaponKind.Bfg9000)
    };

    public static PickupInfo Get(PickupKind kind) => Items[(int)kind];
}

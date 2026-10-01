namespace Doom.World;

/// <summary>Тип бонуса, лежащего на карте: боеприпасы или новое оружие.</summary>
internal enum PickupKind
{
    /// <summary>Обойма патронов.</summary>
    Clip,

    /// <summary>Ящик патронов.</summary>
    BulletBox,

    /// <summary>Четыре дроби.</summary>
    Shells,

    /// <summary>Ящик дроби.</summary>
    ShellBox,

    /// <summary>Одна ракета.</summary>
    Rocket,

    /// <summary>Ящик ракет.</summary>
    RocketBox,

    /// <summary>Батарея энергоячеек.</summary>
    Cell,

    /// <summary>Большая упаковка энергоячеек.</summary>
    CellPack,

    Chainsaw,
    Shotgun,
    Chaingun,
    RocketLauncher,
    PlasmaRifle,
    Bfg9000
}

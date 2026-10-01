namespace Doom.Gameplay;

/// <summary>
///     Оружие игрока: полный арсенал Doom. Порядок совпадает с индексами в
///     <see cref="WeaponCatalog" />, а слоты клавиш 1..7 задаёт <see cref="WeaponCatalog.Slot" />
///     (у кулака и бензопилы общий слот 1).
/// </summary>
internal enum WeaponKind
{
    /// <summary>Кулак: рукопашная атака, есть всегда.</summary>
    Fist,

    /// <summary>Бензопила: быстрая рукопашная атака (слот 1 вместо кулака).</summary>
    Chainsaw,

    Pistol,
    Shotgun,

    /// <summary>Пулемёт: тот же патрон, что у пистолета, но с высокой скорострельностью.</summary>
    Chaingun,

    RocketLauncher,
    PlasmaRifle,
    Bfg9000
}

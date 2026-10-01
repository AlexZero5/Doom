namespace Doom.Gameplay;

/// <summary>
///     Тип боеприпасов. Порядок совпадает с индексами в <see cref="AmmoCatalog" />
///     и с индексами массива запаса в <see cref="Player" />.
/// </summary>
internal enum AmmoKind
{
    /// <summary>Оружие без боеприпасов (кулак, бензопила).</summary>
    None,

    Bullets,
    Shells,
    Rockets,
    Cells
}

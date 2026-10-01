namespace Doom.Gameplay;

/// <summary>Летящий снаряд. Значение совпадает с индексом в <see cref="ProjectileCatalog" />.</summary>
internal enum ProjectileKind
{
    /// <summary>Оружие стреляет мгновенно (hitscan) или бьёт вблизи.</summary>
    None,

    Rocket,
    PlasmaBolt,
    BfgBall
}

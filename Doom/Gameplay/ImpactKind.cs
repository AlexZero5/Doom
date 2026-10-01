namespace Doom.Gameplay;

/// <summary>
///     Кратковременный эффект в мире. Значение совпадает с индексом в <see cref="ImpactCatalog" />,
///     а внешний вид эффекта описывает <c>ImpactPalette</c> в слое отрисовки.
/// </summary>
internal enum ImpactKind
{
    /// <summary>Искра от пули, попавшей в стену.</summary>
    BulletSpark,

    /// <summary>Искра от удара кулаком или пилой.</summary>
    MeleeSpark,

    /// <summary>Дымный след ракеты.</summary>
    RocketTrail,

    /// <summary>Зелёный след плазмы.</summary>
    PlasmaTrail,

    /// <summary>Зелёное облако следа BFG.</summary>
    BfgTrail,

    /// <summary>Взрыв ракеты: вспышка, огонь и разлетающийся дым.</summary>
    RocketExplosion,

    PlasmaImpact,
    BfgImpact,

    /// <summary>Клуб дыма, разлетающийся от взрыва.</summary>
    ExplosionPuff
}

namespace Doom.Gameplay;

/// <summary>Лётные характеристики снаряда: скорость, время жизни, след и параметры взрыва.</summary>
internal readonly struct ProjectileInfo
{
    public ProjectileInfo(
        double speed,
        double lifetime,
        double trailSeconds,
        ImpactKind trailImpact,
        ImpactKind impact)
    {
        Speed = speed;
        Lifetime = lifetime;
        TrailSeconds = trailSeconds;
        TrailImpact = trailImpact;
        Impact = impact;
    }

    /// <summary>Скорость в клетках за секунду.</summary>
    public double Speed { get; }

    /// <summary>Через сколько секунд снаряд взрывается сам.</summary>
    public double Lifetime { get; }

    /// <summary>Интервал между следами дыма (0 — снаряд без следа).</summary>
    public double TrailSeconds { get; }

    public ImpactKind TrailImpact { get; }

    /// <summary>Эффект, возникающий при попадании снаряда в стену.</summary>
    public ImpactKind Impact { get; }
}

/// <summary>Каталог снарядов: порядок элементов совпадает с <see cref="ProjectileKind" />.</summary>
internal static class ProjectileCatalog
{
    private static readonly ProjectileInfo[] Projectiles =
    {
        default,
        new(speed: 9.0, lifetime: 6.0, trailSeconds: 0.055, trailImpact: ImpactKind.RocketTrail,
            impact: ImpactKind.RocketExplosion),
        new(speed: 14.0, lifetime: 4.0, trailSeconds: 0.035, trailImpact: ImpactKind.PlasmaTrail,
            impact: ImpactKind.PlasmaImpact),
        new(speed: 8.0, lifetime: 6.0, trailSeconds: 0.09, trailImpact: ImpactKind.BfgTrail,
            impact: ImpactKind.BfgImpact)
    };

    public static ProjectileInfo Get(ProjectileKind kind) => Projectiles[(int)kind];
}

namespace Doom.Gameplay;

/// <summary>Лётные характеристики снаряда: скорость, время жизни, след и параметры взрыва.</summary>
internal readonly struct ProjectileInfo
{
    public ProjectileInfo(
        double speed,
        double lifetime,
        double trailSeconds,
        ImpactKind trailImpact,
        ImpactKind impact,
        double spawnZCenter,
        double bandHalf)
    {
        Speed = speed;
        Lifetime = lifetime;
        TrailSeconds = trailSeconds;
        TrailImpact = trailImpact;
        Impact = impact;
        SpawnZCenter = spawnZCenter;
        BandHalf = bandHalf;
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

    /// <summary>
    ///     Высота ЦЕНТРА спрайта снаряда при запуске (0 — пол, 1 — потолок). Снаряд летит
    ///     на этой высоте: стрельба вверх/вниз сдвигает её, но спрайт рождается ровно там,
    ///     где рисуется ствол, — а не у потолка.
    /// </summary>
    public double SpawnZCenter { get; }

    /// <summary>Половина высоты спрайта: нижний край = центр − половина, верхний = центр + половина.</summary>
    public double BandHalf { get; }
}

/// <summary>Каталог снарядов: порядок элементов совпадает с <see cref="ProjectileKind" />.
/// Высоты согласованы с полосами спрайтов в ProjectilePalette (Rocket 0.32..0.48, Plasma 0.30..0.46, BFG 0.22..0.62).</summary>
internal static class ProjectileCatalog
{
    private static readonly ProjectileInfo[] Projectiles =
    {
        default,
        new(speed: 9.0, lifetime: 6.0, trailSeconds: 0.055, trailImpact: ImpactKind.RocketTrail,
            impact: ImpactKind.RocketExplosion, spawnZCenter: 0.40, bandHalf: 0.08),
        new(speed: 14.0, lifetime: 4.0, trailSeconds: 0.035, trailImpact: ImpactKind.PlasmaTrail,
            impact: ImpactKind.PlasmaImpact, spawnZCenter: 0.38, bandHalf: 0.08),
        new(speed: 8.0, lifetime: 6.0, trailSeconds: 0.09, trailImpact: ImpactKind.BfgTrail,
            impact: ImpactKind.BfgImpact, spawnZCenter: 0.42, bandHalf: 0.20)
    };

    public static ProjectileInfo Get(ProjectileKind kind) => Projectiles[(int)kind];
}

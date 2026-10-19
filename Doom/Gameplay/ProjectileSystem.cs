using Doom.Configuration;
using Doom.World;

namespace Doom.Gameplay;

/// <summary>
///     Система снарядов: единственное место, где движутся ракеты, плазменные заряды и шар BFG,
///     где рождаются искры, дым и взрывы и где копится засветка кадра от выстрелов.
///     Снаряд летит строго по направлению взгляда (с наклоном) и взрывается там, где его путь
///     за кадр пересёк мир: стену, пол, потолок, дверь, мебель.
/// </summary>
internal sealed class ProjectileSystem
{
    /// <summary>Эффект чуть отодвигается от поверхности навстречу лучу, чтобы не тонуть в ней.</summary>
    private const double SurfaceOffset = 0.12;

    private readonly List<Projectile> _projectiles = new();

    /// <summary>Взорвавшиеся снаряды для переиспользования: стрельба не мусорит в куче.</summary>
    private readonly Stack<Projectile> _freeProjectiles = new();
    private readonly List<Impact> _impacts = new();

    private double _flashPeak;
    private double _flashSeconds;
    private double _flashRemaining;
    private int _impactSeed;
    private int _puffSeed;

    public IReadOnlyList<Projectile> Projectiles => _projectiles;

    public IReadOnlyList<Impact> Impacts => _impacts;

    /// <summary>Текущая яркость засветки всего кадра (0 — без засветки).</summary>
    public double FlashIntensity { get; private set; }

    public int FlashColor { get; private set; }

    /// <summary>Есть ли что обновлять и дорисовывать: снаряды, живые эффекты или засветка.</summary>
    public bool IsActive
    {
        get
        {
            if (_projectiles.Count > 0 || FlashIntensity > 0.001)
                return true;

            foreach (Impact impact in _impacts)
            {
                if (!impact.IsExpired)
                    return true;
            }

            return false;
        }
    }

    public void Reset()
    {
        foreach (Projectile projectile in _projectiles)
            _freeProjectiles.Push(projectile);

        _projectiles.Clear();
        _impacts.Clear();
        FlashIntensity = 0;
        FlashColor = 0;
        _flashPeak = 0;
        _flashSeconds = 0;
        _flashRemaining = 0;
        _impactSeed = 0;
        _puffSeed = 0;
    }

    /// <summary>Запускает снаряд из глаз игрока по направлению взгляда.</summary>
    public void Launch(ProjectileKind kind, Vec3 eye, Vec3 direction)
    {
        if (kind == ProjectileKind.None || _projectiles.Count >= GameConfig.MaxProjectiles)
            return;

        ProjectileInfo info = ProjectileCatalog.Get(kind);
        AcquireProjectile().Reset(kind, eye + direction * GameConfig.ProjectileSpawnOffset, direction, info.TrailSeconds);
    }

    /// <summary>Искра в точке попадания пули или удара (чуть перед поверхностью).</summary>
    public void SpawnHitSpark(ImpactKind kind, Vec3 point, Vec3 normal) => AddImpact(kind, point + normal * 0.06);

    public void AddFlash(double intensity, double seconds, int color)
    {
        if (intensity <= 0 || seconds <= 0)
            return;

        // Слабая вспышка не должна перебивать уже идущую сильную.
        if (FlashIntensity > intensity && _flashRemaining > 0)
            return;

        _flashPeak = intensity;
        _flashSeconds = seconds;
        _flashRemaining = seconds;
        FlashIntensity = intensity;
        FlashColor = color;
    }

    public void Update(double deltaSeconds, Level level)
    {
        UpdateProjectiles(deltaSeconds, level);
        UpdateImpacts(deltaSeconds);
        UpdateFlash(deltaSeconds);
    }

    private void UpdateProjectiles(double deltaSeconds, Level level)
    {
        for (int index = _projectiles.Count - 1; index >= 0; index--)
        {
            Projectile projectile = _projectiles[index];
            ProjectileInfo info = ProjectileCatalog.Get(projectile.Kind);
            projectile.Advance(deltaSeconds);

            double step = info.Speed * deltaSeconds;
            if (level.RayCast(projectile.Position, projectile.Direction, step, out RayHit hit))
            {
                Explode(info, hit.Point - projectile.Direction * SurfaceOffset);
                Release(index);
                continue;
            }

            projectile.MoveTo(projectile.Position + projectile.Direction * step);

            if (projectile.Age >= info.Lifetime || projectile.Z < -20)
            {
                Explode(info, projectile.Position);
                Release(index);
                continue;
            }

            if (info.TrailSeconds > 0 && projectile.TryEmitTrail(deltaSeconds, info.TrailSeconds))
                AddImpact(info.TrailImpact, projectile.Position - projectile.Direction * 0.2);
        }
    }

    private void Explode(ProjectileInfo info, Vec3 point)
    {
        ImpactInfo impact = ImpactCatalog.Get(info.Impact);

        AddImpact(info.Impact, point);
        AddFlash(impact.Flash, impact.FlashSeconds, impact.FlashColor);

        for (int index = 0; index < impact.PuffCount; index++)
        {
            double angle = index * (Math.Tau / impact.PuffCount) + _puffSeed * 1.13;
            double distance = impact.PuffDistance * 2.6 * (0.55 + 0.45 * SpreadNoise.Unit(index, _puffSeed));
            AddImpact(ImpactKind.ExplosionPuff,
                point + new Vec3(Math.Cos(angle) * distance, Math.Sin(angle) * distance, 0.2 * SpreadNoise.Unit(_puffSeed, index)));
        }

        _puffSeed++;
    }

    private void UpdateImpacts(double deltaSeconds)
    {
        foreach (Impact impact in _impacts)
            impact.Advance(deltaSeconds);
    }

    private void UpdateFlash(double deltaSeconds)
    {
        if (_flashRemaining <= 0)
        {
            FlashIntensity = 0;
            return;
        }

        _flashRemaining -= deltaSeconds;

        if (_flashRemaining <= 0)
        {
            _flashRemaining = 0;
            FlashIntensity = 0;
            FlashColor = 0;
            return;
        }

        FlashIntensity = _flashPeak * (_flashRemaining / _flashSeconds);
    }

    private void AddImpact(ImpactKind kind, Vec3 position)
    {
        ImpactInfo info = ImpactCatalog.Get(kind);
        double scale = 0.85 + 0.30 * SpreadNoise.Unit(_impactSeed, (int)kind * 31);
        _impactSeed++;

        AcquireImpact().Reset(kind, position.X, position.Y, info.Duration, scale, position.Z);
    }

    private Projectile AcquireProjectile()
    {
        Projectile projectile = _freeProjectiles.Count > 0 ? _freeProjectiles.Pop() : new Projectile();
        _projectiles.Add(projectile);
        return projectile;
    }

    private void Release(int index)
    {
        Projectile projectile = _projectiles[index];
        projectile.Kill();
        _projectiles.RemoveAt(index);
        _freeProjectiles.Push(projectile);
    }

    /// <summary>Эффект из кольцевого буфера: при переполнении заменяется самый старый.</summary>
    private Impact AcquireImpact()
    {
        if (_impacts.Count < GameConfig.MaxImpacts)
        {
            var created = new Impact();
            _impacts.Add(created);
            return created;
        }

        Impact oldest = _impacts[0];
        for (int index = 1; index < _impacts.Count; index++)
        {
            if (_impacts[index].Age > oldest.Age)
                oldest = _impacts[index];
        }

        return oldest;
    }
}

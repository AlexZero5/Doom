using Doom.Configuration;
using Doom.World;

namespace Doom.Gameplay;

/// <summary>
///     Система снарядов: единственное место, где движутся ракеты, плазменные заряды и шар BFG,
///     где рождаются искры, дым и взрывы и где копится засветка кадра от выстрелов.
/// </summary>
internal sealed class ProjectileSystem
{
    private readonly List<Projectile> _projectiles = new();
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

    /// <summary>Запускает снаряд из точки игрока в направлении взгляда.</summary>
    public void Launch(ProjectileKind kind, double x, double y, double angle)
    {
        if (kind == ProjectileKind.None || _projectiles.Count >= GameConfig.MaxProjectiles)
            return;

        double directionX = Math.Cos(angle);
        double directionY = Math.Sin(angle);
        ProjectileInfo info = ProjectileCatalog.Get(kind);

        AcquireProjectile().Reset(
            kind,
            x + directionX * GameConfig.ProjectileSpawnOffset,
            y + directionY * GameConfig.ProjectileSpawnOffset,
            directionX,
            directionY,
            info.TrailSeconds);
    }

    /// <summary>Искра от мгновенного выстрела: точка попадания чуть отодвинута от стены к игроку.</summary>
    public void SpawnHitSpark(ImpactKind kind, double originX, double originY, double angle, double distance)
    {
        double offset = Math.Max(0.0, distance - GameConfig.SparkOffsetFromWall);
        AddImpact(kind, originX + Math.Cos(angle) * offset, originY + Math.Sin(angle) * offset);
    }

    /// <summary>Добавляет засветку кадра: вспышку выстрела или взрыва.</summary>
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

    /// <summary>Двигает снаряды, старит эффекты и гасит засветку.</summary>
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
            if (!projectile.Alive)
            {
                _projectiles.RemoveAt(index);
                continue;
            }

            projectile.Advance(deltaSeconds);
            ProjectileInfo info = ProjectileCatalog.Get(projectile.Kind);

            if (AdvanceThroughLevel(projectile, deltaSeconds, info, level) || projectile.Age >= info.Lifetime)
            {
                Explode(info, projectile);
                _projectiles.RemoveAt(index);
                continue;
            }

            if (info.TrailSeconds > 0 && projectile.TryEmitTrail(deltaSeconds, info.TrailSeconds))
                AddImpact(info.TrailImpact, projectile.X, projectile.Y);
        }
    }

    /// <summary>Двигает снаряд мелкими шагами. Возвращает <c>true</c>, если путь упёрся в стену.</summary>
    private static bool AdvanceThroughLevel(
        Projectile projectile, double deltaSeconds, ProjectileInfo info, Level level)
    {
        double remaining = info.Speed * deltaSeconds;

        while (remaining > 0)
        {
            double step = Math.Min(remaining, GameConfig.MaxProjectileStep);
            remaining -= step;

            double nextX = projectile.X + projectile.DirectionX * step;
            double nextY = projectile.Y + projectile.DirectionY * step;

            if (!level.IsWalkable(nextX, nextY))
                return true;

            projectile.MoveTo(nextX, nextY);
        }

        return false;
    }

    /// <summary>Взрыв снаряда: огонь, засветка кадра и разлёт клубов дыма.</summary>
    private void Explode(ProjectileInfo info, Projectile projectile)
    {
        ImpactInfo impact = ImpactCatalog.Get(info.Impact);
        AddImpact(info.Impact, projectile.X, projectile.Y);
        AddFlash(impact.Flash, impact.FlashSeconds, impact.FlashColor);

        for (int index = 0; index < impact.PuffCount; index++)
        {
            double angle = index * (Math.Tau / impact.PuffCount) + _puffSeed * 1.13;
            double distance = impact.PuffDistance * (0.55 + 0.45 * SpreadNoise.Unit(index, _puffSeed));

            AddImpact(
                ImpactKind.ExplosionPuff,
                projectile.X + Math.Cos(angle) * distance,
                projectile.Y + Math.Sin(angle) * distance);
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

    private void AddImpact(ImpactKind kind, double x, double y)
    {
        ImpactInfo info = ImpactCatalog.Get(kind);
        double scale = 0.85 + 0.30 * SpreadNoise.Unit(_impactSeed, (int)kind * 31);
        _impactSeed++;

        AcquireImpact().Reset(kind, x, y, info.Duration, scale);
    }

    /// <summary>Берёт снаряд для переиспользования, чтобы не мусорить в куче.</summary>
    private Projectile AcquireProjectile()
    {
        foreach (Projectile projectile in _projectiles)
        {
            if (!projectile.Alive)
                return projectile;
        }

        var created = new Projectile();
        _projectiles.Add(created);
        return created;
    }

    /// <summary>
    ///     Берёт слот эффекта: пока список не заполнен — создаёт новый, иначе перезаписывает
    ///     самый давний эффект (они короткоживущие, поэтому вытесненные уже не видны).
    /// </summary>
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

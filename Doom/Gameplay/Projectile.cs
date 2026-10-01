namespace Doom.Gameplay;

/// <summary>Летящий снаряд: ракета, плазменный заряд или шар BFG.</summary>
internal sealed class Projectile
{
    public ProjectileKind Kind { get; private set; }

    public double X { get; private set; }

    public double Y { get; private set; }

    public double DirectionX { get; private set; }

    public double DirectionY { get; private set; }

    public double Age { get; private set; }

    /// <summary>Снаряд ещё летит (после взрыва слот переиспользуется).</summary>
    public bool Alive { get; private set; }

    private double _trailCountdown;

    public void Reset(ProjectileKind kind, double x, double y, double directionX, double directionY, double trailSeconds)
    {
        Kind = kind;
        X = x;
        Y = y;
        DirectionX = directionX;
        DirectionY = directionY;
        Age = 0;
        Alive = true;
        _trailCountdown = trailSeconds;
    }

    public void Advance(double deltaSeconds) => Age += deltaSeconds;

    public void MoveTo(double x, double y)
    {
        X = x;
        Y = y;
    }

    public void Kill() => Alive = false;

    /// <summary>Пора ли оставить очередной след (дым или искру).</summary>
    public bool TryEmitTrail(double deltaSeconds, double interval)
    {
        _trailCountdown -= deltaSeconds;
        if (_trailCountdown > 0)
            return false;

        _trailCountdown = interval;
        return true;
    }
}

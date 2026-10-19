using Doom.World;

namespace Doom.Gameplay;

/// <summary>Летящий снаряд: позиция и направление в 3D, возраст, таймер следа.</summary>
internal sealed class Projectile
{
    public ProjectileKind Kind { get; private set; }

    public Vec3 Position { get; private set; }

    /// <summary>Единичное направление полёта.</summary>
    public Vec3 Direction { get; private set; }

    public double X => Position.X;

    public double Y => Position.Y;

    public double Z => Position.Z;

    public double Age { get; private set; }

    public bool Alive { get; private set; }

    private double _trailCountdown;

    public void Reset(ProjectileKind kind, Vec3 position, Vec3 direction, double trailSeconds)
    {
        Kind = kind;
        Position = position;
        Direction = direction;
        Age = 0;
        Alive = true;
        _trailCountdown = trailSeconds;
    }

    public void Advance(double deltaSeconds) => Age += deltaSeconds;

    public void MoveTo(Vec3 position) => Position = position;

    public void Kill() => Alive = false;

    /// <summary>Пора ли оставить очередной клуб следа.</summary>
    public bool TryEmitTrail(double deltaSeconds, double interval)
    {
        _trailCountdown -= deltaSeconds;
        if (_trailCountdown > 0)
            return false;

        _trailCountdown = interval;
        return true;
    }
}

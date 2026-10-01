namespace Doom.Gameplay;

/// <summary>
///     Кратковременный эффект в мире: искра, дым или взрыв. Внешний вид берётся из
///     <c>ImpactPalette</c> по <see cref="Kind" />, длительность — из <see cref="ImpactCatalog" />.
///     Объект переиспользуется системой снарядов, поэтому настройка выполняется через <see cref="Reset" />.
/// </summary>
internal sealed class Impact
{
    public ImpactKind Kind { get; private set; }

    public double X { get; private set; }

    public double Y { get; private set; }

    public double Age { get; private set; }

    public double Duration { get; private set; } = 1.0;

    /// <summary>Разброс размера (0.85..1.15), чтобы искры и дым не выглядели клонами.</summary>
    public double Scale { get; private set; } = 1.0;

    public void Reset(ImpactKind kind, double x, double y, double duration, double scale)
    {
        Kind = kind;
        X = x;
        Y = y;
        Age = 0;
        Duration = duration;
        Scale = scale;
    }

    public void Advance(double deltaSeconds) => Age += deltaSeconds;

    public bool IsExpired => Age >= Duration;

    /// <summary>Доля прожитого времени: 0 — только возник, 1 — погас.</summary>
    public double Progress => Duration <= 0 ? 1.0 : Math.Min(1.0, Age / Duration);
}

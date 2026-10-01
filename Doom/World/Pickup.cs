namespace Doom.World;

/// <summary>Бонус на карте: патроны или дробь.</summary>
internal sealed class Pickup
{
    public required double X { get; init; }

    public required double Y { get; init; }

    public required PickupKind Kind { get; init; }

    public required int Amount { get; init; }

    /// <summary>Бонус уже подобран игроком.</summary>
    public bool Taken { get; set; }
}

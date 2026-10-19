namespace Doom.World;

/// <summary>Бонус на карте: оружие или боеприпасы.</summary>
internal sealed class Pickup
{
    public required double X { get; init; }

    public required double Y { get; init; }

    /// <summary>Высота, на которой лежит бонус (пол этажа, стол, ящик).</summary>
    public double Z { get; init; }

    public required PickupKind Kind { get; init; }

    public required int Amount { get; init; }

    /// <summary>Бонус уже подобран игроком.</summary>
    public bool Taken { get; set; }
}

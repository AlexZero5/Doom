namespace Doom.World;

/// <summary>
///     Расстановка бонусов на стандартном уровне: оружие спрятано в четырёх комнатах
///     (в каждой прорублена дверь), боеприпасы разложены по открытым коридорам.
/// </summary>
internal static class PickupLayout
{
    public static IEnumerable<Pickup> CreateDefault()
    {
        // Боеприпасы для стартового пистолета.
        yield return Create(2.5, 8.5, PickupKind.Clip, 10);
        yield return Create(15.5, 10.5, PickupKind.ShellBox, 20);
        yield return Create(10.5, 17.5, PickupKind.BulletBox, 50);
        yield return Create(2.5, 17.5, PickupKind.Shells, 4);
        yield return Create(10.5, 3.5, PickupKind.Shells, 4);
        yield return Create(15.5, 23.5, PickupKind.BulletBox, 50);

        // Оружие: каждая комната — своя награда.
        yield return Create(5.5, 5.5, PickupKind.Shotgun, 1);
        yield return Create(18.5, 5.5, PickupKind.RocketLauncher, 1);
        yield return Create(12.5, 13.5, PickupKind.PlasmaRifle, 1);
        yield return Create(23.5, 20.5, PickupKind.Bfg9000, 1);
        yield return Create(19.5, 8.5, PickupKind.Chaingun, 1);
        yield return Create(2.5, 22.5, PickupKind.Chainsaw, 1);

        // Боеприпасы для нового оружия.
        yield return Create(28.5, 5.5, PickupKind.CellPack, 100);
        yield return Create(28.5, 12.5, PickupKind.Cell, 20);
        yield return Create(19.5, 16.5, PickupKind.Rocket, 1);
        yield return Create(28.5, 22.5, PickupKind.RocketBox, 5);
    }

    private static Pickup Create(double x, double y, PickupKind kind, int amount) =>
        new() { X = x, Y = y, Kind = kind, Amount = amount };
}


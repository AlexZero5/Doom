using Doom.Configuration;

namespace Doom.Gameplay;

/// <summary>Названия, обозначения и предельные запасы боеприпасов.</summary>
internal static class AmmoCatalog
{
    /// <summary>Количество типов боеприпасов (включая <see cref="AmmoKind.None" />).</summary>
    public const int Count = 5;

    private static readonly string[] Names = { "-", "BULLETS", "SHELLS", "ROCKETS", "CELLS" };

    private static readonly char[] Letters = { '-', 'B', 'S', 'R', 'C' };

    public static string Name(AmmoKind kind) => Names[(int)kind];

    /// <summary>Короткое обозначение для строки состояния.</summary>
    public static char Letter(AmmoKind kind) => Letters[(int)kind];

    /// <summary>Предельный запас боеприпасов этого типа.</summary>
    public static int Capacity(AmmoKind kind) => kind switch
    {
        AmmoKind.Bullets => GameConfig.MaxBullets,
        AmmoKind.Shells => GameConfig.MaxShells,
        AmmoKind.Rockets => GameConfig.MaxRockets,
        AmmoKind.Cells => GameConfig.MaxCells,
        _ => 0
    };
}

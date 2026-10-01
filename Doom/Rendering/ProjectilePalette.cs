using Doom.Gameplay;

namespace Doom.Rendering;

/// <summary>Внешний вид летящего снаряда.</summary>
internal readonly struct ProjectileVisual
{
    public ProjectileVisual(int color, double width, double bottomZ, double topZ)
    {
        Color = color;
        Width = width;
        BottomZ = bottomZ;
        TopZ = topZ;
    }

    public int Color { get; }

    public double Width { get; }

    public double BottomZ { get; }

    public double TopZ { get; }
}

/// <summary>Внешний вид кратковременного эффекта: цвет, размер, рост и затухание.</summary>
internal readonly struct ImpactVisual
{
    public ImpactVisual(
        int color,
        double width,
        double bottomZ,
        double topZ,
        double growth,
        double alpha,
        double rise)
    {
        Color = color;
        Width = width;
        BottomZ = bottomZ;
        TopZ = topZ;
        Growth = growth;
        Alpha = alpha;
        Rise = rise;
    }

    public int Color { get; }

    /// <summary>Ширина в клетках в начале жизни эффекта.</summary>
    public double Width { get; }

    public double BottomZ { get; }

    public double TopZ { get; }

    /// <summary>Во сколько раз расширяется эффект к концу жизни.</summary>
    public double Growth { get; }

    /// <summary>Начальная прозрачность.</summary>
    public double Alpha { get; }

    /// <summary>Подъём вверх за секунду (у дыма).</summary>
    public double Rise { get; }
}

/// <summary>Цвета и размеры снарядов и эффектов: порядок совпадает с <see cref="ProjectileKind" />.</summary>
internal static class ProjectilePalette
{
    private static readonly ProjectileVisual[] Projectiles =
    {
        default,
        new(0xD8D8E0, width: 0.18, bottomZ: 0.32, topZ: 0.48),
        new(0x4AFF8C, width: 0.14, bottomZ: 0.30, topZ: 0.46),
        new(0x9CFF5A, width: 0.40, bottomZ: 0.22, topZ: 0.62)
    };

    public static ProjectileVisual Get(ProjectileKind kind) => Projectiles[(int)kind];
}

/// <summary>Цвета и размеры эффектов: порядок совпадает с <see cref="ImpactKind" />.</summary>
internal static class ImpactPalette
{
    private static readonly ImpactVisual[] Effects =
    {
        // BulletSpark — маленькая жёлтая искра от пули.
        new(0xFFE9A0, width: 0.13, bottomZ: 0.34, topZ: 0.46, growth: 1.7, alpha: 1.0, rise: 0.0),

        // MeleeSpark — оранжевая искра от удара.
        new(0xFFB040, width: 0.16, bottomZ: 0.30, topZ: 0.50, growth: 1.6, alpha: 1.0, rise: 0.0),

        // RocketTrail — серый дым за ракетой.
        new(0x6E6E72, width: 0.20, bottomZ: 0.30, topZ: 0.46, growth: 2.4, alpha: 0.55, rise: 0.25),

        // PlasmaTrail — зелёная искра за плазменным зарядом.
        new(0x66FFA0, width: 0.11, bottomZ: 0.34, topZ: 0.44, growth: 1.8, alpha: 0.80, rise: 0.0),

        // BfgTrail — зелёное облако за шаром BFG.
        new(0x86E060, width: 0.26, bottomZ: 0.30, topZ: 0.50, growth: 2.2, alpha: 0.50, rise: 0.20),

        // RocketExplosion — огненный шар ракеты.
        new(0xFFA040, width: 0.60, bottomZ: 0.10, topZ: 0.78, growth: 2.6, alpha: 1.0, rise: 0.0),

        // PlasmaImpact — вспышка плазмы.
        new(0x50FF90, width: 0.34, bottomZ: 0.25, topZ: 0.60, growth: 2.2, alpha: 1.0, rise: 0.0),

        // BfgImpact — огромная зелёная вспышка.
        new(0xB0FF70, width: 0.72, bottomZ: 0.10, topZ: 0.88, growth: 2.4, alpha: 1.0, rise: 0.0),

        // ExplosionPuff — клуб дыма, поднимающийся вверх.
        new(0x707076, width: 0.34, bottomZ: 0.25, topZ: 0.62, growth: 2.5, alpha: 0.60, rise: 0.35)
    };

    public static ImpactVisual Get(ImpactKind kind) => Effects[(int)kind];
}

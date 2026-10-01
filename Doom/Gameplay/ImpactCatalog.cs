namespace Doom.Gameplay;

/// <summary>Длительность эффекта и засветка экрана, которую он вызывает.</summary>
internal readonly struct ImpactInfo
{
    public ImpactInfo(
        double duration,
        double flash = 0.0,
        double flashSeconds = 0.0,
        int flashColor = 0,
        int puffCount = 0,
        double puffDistance = 0.0)
    {
        Duration = duration;
        Flash = flash;
        FlashSeconds = flashSeconds;
        FlashColor = flashColor;
        PuffCount = puffCount;
        PuffDistance = puffDistance;
    }

    /// <summary>Сколько секунд живёт эффект.</summary>
    public double Duration { get; }

    /// <summary>Яркость засветки всего кадра (0 — без засветки).</summary>
    public double Flash { get; }

    public double FlashSeconds { get; }

    /// <summary>Цвет засветки — часть описания эффекта, поэтому хранится здесь.</summary>
    public int FlashColor { get; }

    /// <summary>Сколько клубов дыма разлетается от точки попадания.</summary>
    public int PuffCount { get; }

    /// <summary>Радиус разлёта клубов дыма.</summary>
    public double PuffDistance { get; }
}

/// <summary>Каталог эффектов: порядок элементов совпадает с <see cref="ImpactKind" />.</summary>
internal static class ImpactCatalog
{
    private static readonly ImpactInfo[] Effects =
    {
        // BulletSpark — короткая искра от пули.
        new(duration: 0.10),

        // MeleeSpark — искра от удара в стену.
        new(duration: 0.16),

        // RocketTrail — дымный след ракеты.
        new(duration: 0.55),

        // PlasmaTrail — искра следа плазмы.
        new(duration: 0.14),

        // BfgTrail — облако следа BFG.
        new(duration: 0.40),

        // RocketExplosion — вспышка с засветкой и разлётом дыма.
        new(duration: 0.45, flash: 0.45, flashSeconds: 0.30, flashColor: 0xFFC070, puffCount: 6,
            puffDistance: 0.45),

        // PlasmaImpact — зелёная вспышка.
        new(duration: 0.24, flash: 0.26, flashSeconds: 0.16, flashColor: 0x66FFA0, puffCount: 3,
            puffDistance: 0.22),

        // BfgImpact — крупная зелёно-белая вспышка.
        new(duration: 0.70, flash: 0.75, flashSeconds: 0.45, flashColor: 0xB0FF70, puffCount: 8,
            puffDistance: 0.7),

        // ExplosionPuff — медленный дым взрыва.
        new(duration: 0.80)
    };

    public static ImpactInfo Get(ImpactKind kind) => Effects[(int)kind];
}

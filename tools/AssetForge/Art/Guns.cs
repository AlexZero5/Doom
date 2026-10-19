using AssetForge.Core;

namespace AssetForge.Art;

/// <summary>
///     Оружие от первого лица: 3D-модели (поле расстояний), отрендеренные из глаза игрока и
///     переведённые в пиксель-арт. Стиль — «индустриальный sci-fi»: тёмный оружейный металл,
///     шлифованная сталь, рёбра радиаторов, болты, жёлто-чёрные полосы, светящиеся индикаторы,
///     бронированные перчатки.
/// </summary>
/// <remarks>
///     Камера в нуле смотрит вдоль +Z, Y — вверх; экран — Doom 640×400 (×2), фокус 320 (90°).
///     Холст 400×240 — нижняя середина этого экрана (x 120..520, y 160..400): игра растягивает
///     его так, что 400 пикселей = высота вида, и прижимает к низу по центру. Стволы параллельны
///     взгляду — поэтому сходятся к центру экрана и смотрят «вперёд».
///     Проекция: x_холста = 200 + 320·x/z, y_холста = 40 − 320·y/z.
/// </remarks>
internal static class Guns
{
    public const int Width = 400;
    public const int Height = 240;

    private static readonly Camera ViewCamera = new()
    {
        Focal = 320, CenterX = 320, CenterY = 200, OffsetX = 120, OffsetY = 160
    };

    // ============================================================
    //   Материалы
    // ============================================================

    private static readonly Material Gunmetal = Material.Metal(Rgb.Hex(0x434a56), 0.8);
    private static readonly Material Steel = Material.Metal(Rgb.Hex(0x9aa4b2), 0.9);
    private static readonly Material DarkSteel = Material.Metal(Rgb.Hex(0x23272e), 0.65);
    private static readonly Material Polymer = new(Rgb.Hex(0x2c2c30), 0.3, 14, Metallic: 0.1,
        Pattern: p => 0.85 + 0.25 * Noise.Unit((int)(p.X * 900), (int)(p.Y * 900 + p.Z * 700), 951));
    private static readonly Material GloveRubber = new(Rgb.Hex(0x6a4630), 0.3, 12,
        Pattern: p => 0.9 + 0.15 * Noise.Unit((int)(p.X * 700), (int)(p.Z * 700 + p.Y * 300), 952));
    private static readonly Material ArmorPlate = Material.Painted(Rgb.Hex(0x56663a)) with
    {
        Pattern = p => Seam(p.Z, 0.06) * (0.92 + 0.12 * Noise.Unit((int)(p.X * 400), (int)(p.Z * 400), 953))
    };
    private static readonly Material Sleeve = new(Rgb.Hex(0x2e3a24), 0.1, 6, Pattern: p => 0.85 + 0.15 * Math.Sin(p.Z * 300));
    private static readonly Material OrangeAccent = Material.Painted(Rgb.Hex(0xd8661c));
    private static readonly Material Brass = Material.Metal(Rgb.Hex(0xc8a030), 0.85);
    private static readonly Material ShellRed = Material.Painted(Rgb.Hex(0xa82820));
    private static readonly Material Hole = Material.Glow(Rgb.Hex(0x050506));

    private static readonly Material Hazard = Material.Painted(Rgb.Hex(0xe0b020)) with
    {
        Pattern = p => ((int)Math.Floor((p.X + p.Y + p.Z) * 45) & 1) == 0 ? 1.0 : 0.13
    };

    /// <summary>Тёмные «швы» между панелями через каждые <paramref name="spacing" /> по координате.</summary>
    private static double Seam(double coordinate, double spacing)
    {
        double f = coordinate / spacing - Math.Floor(coordinate / spacing);
        return f < 0.04 ? 0.5 : 1.0;
    }

    private static Material Glow(Rgb dark, Rgb bright, double level) => Material.Glow(dark.Lerp(bright, level));

    // ============================================================
    //   Кадры
    // ============================================================

    public static IEnumerable<(string Weapon, string Frame, Canvas Image)> All()
    {
        yield return ("fist", "idle", Fist(new Vec3(0.12, -0.165, 0.33), 0));
        yield return ("fist", "punch1", Fist(new Vec3(0.08, -0.135, 0.4), 0.1));
        yield return ("fist", "punch2", Fist(new Vec3(0.03, -0.095, 0.56), 0.2));
        yield return ("fist", "punch3", Fist(new Vec3(0.1, -0.155, 0.36), 0.05));

        yield return ("chainsaw", "idle1", Chainsaw(0, 0, 0));
        yield return ("chainsaw", "idle2", Chainsaw(0.002, 0.002, 1));
        yield return ("chainsaw", "saw1", Chainsaw(0.004, 0.05, 0));
        yield return ("chainsaw", "saw2", Chainsaw(-0.004, 0.044, 1));

        yield return ("pistol", "idle", Pistol(0, 0, 0));
        yield return ("pistol", "fire1", Pistol(-0.035, 0.07, 40));
        yield return ("pistol", "fire2", Pistol(-0.02, 0.04, 22));
        yield return ("pistol", "fire3", Pistol(-0.006, 0.012, 0));

        yield return ("shotgun", "idle", Shotgun(0, 0, 0, 0));
        yield return ("shotgun", "fire1", Shotgun(-0.035, 0.06, 0, 60));
        yield return ("shotgun", "fire2", Shotgun(-0.02, 0.035, 0, 30));
        yield return ("shotgun", "pump1", Shotgun(-0.01, 0.01, -0.12, 0));
        yield return ("shotgun", "pump2", Shotgun(-0.004, 0, -0.06, 0));

        yield return ("chaingun", "idle", Chaingun(0, 0, 0));
        yield return ("chaingun", "fire1", Chaingun(20, -0.012, 46));
        yield return ("chaingun", "fire2", Chaingun(40, -0.006, 28));

        yield return ("rocketlauncher", "idle", RocketLauncher(0, true, 0));
        yield return ("rocketlauncher", "fire1", RocketLauncher(-0.07, false, 80));
        yield return ("rocketlauncher", "fire2", RocketLauncher(-0.04, false, 40));
        yield return ("rocketlauncher", "fire3", RocketLauncher(-0.015, true, 0));

        yield return ("plasmarifle", "idle", Plasma(0, 0.35, 0));
        yield return ("plasmarifle", "fire1", Plasma(-0.025, 1.0, 40));
        yield return ("plasmarifle", "fire2", Plasma(-0.012, 0.7, 22));

        yield return ("bfg9000", "idle", Bfg(0, 0.2, 0));
        yield return ("bfg9000", "charge1", Bfg(0.005, 0.5, 0));
        yield return ("bfg9000", "charge2", Bfg(0.01, 0.85, 0));
        yield return ("bfg9000", "fire1", Bfg(-0.06, 1.0, 110));
        yield return ("bfg9000", "fire2", Bfg(-0.03, 0.4, 0));
    }

    /// <summary>Вспышка позади модели (дуло дальше всего), модель поверх, палитра.</summary>
    private static Canvas Frame(Scene scene, Vec3? muzzle, double flash, Rgb[]? flashColors = null, int seed = 1)
    {
        Canvas model = Renderer3D.Render(scene, ViewCamera, Width, Height);
        var sprite = new Sprite(Width, Height);

        if (flash > 0 && muzzle is { } m)
        {
            (double fx, double fy) = ViewCamera.Project(m);
            sprite.Flash(fx, fy - flash * 0.25, flash, 9, seed, flashColors ?? Sprite.FireFlash);
        }

        sprite.Canvas.Blit(model, 0, 0);
        return sprite.Finish(dither: 5);
    }

    /// <summary>Отдача: поворот вокруг точки хвата, дуло уходит вверх на заданный угол (радианы).</summary>
    private sealed class Kick(Scene scene, Vec3 pivot, double angle)
    {
        public void Add(Sdf shape, Material material) =>
            scene.Add(angle == 0 ? shape : new Rotated(shape, pivot, new Vec3(1, 0, 0), -angle), material);

        public Vec3 Apply(Vec3 point) => angle == 0 ? point : point.RotateAround(pivot, new Vec3(1, 0, 0), -angle);
    }

    // ============================================================
    //   Бронированная перчатка
    // ============================================================

    /// <summary>
    ///     Сжатый кулак в бронированной перчатке: резиновая основа (ладонь, согнутые пальцы,
    ///     большой палец), стальная пластина на тыльной стороне, щиток на костяшках, накладки
    ///     на фалангах, манжета с оранжевой полосой и бронированное предплечье.
    ///     <paramref name="origin" /> — середина тыльной стороны ладони, <paramref name="forward" /> —
    ///     куда смотрят костяшки, <paramref name="side" />: 1 — правая рука, −1 — левая.
    /// </summary>
    private static void Glove(Action<Sdf, Material> add, Vec3 origin, Vec3 forward, Vec3 up, double side, double size,
        Vec3 elbow)
    {
        Vec3 z = forward.Normalized;
        Vec3 x = up.Cross(z).Normalized;
        Vec3 y = z.Cross(x).Normalized;
        Vec3 xs = x * side;

        Vec3 L(double lx, double ly, double lz) => origin + (xs * lx + y * ly + z * lz) * size;
        double S(double r) => r * size;

        var hand = new List<Sdf>
        {
            new Box(L(0, 0, 0), new Vec3(S(0.040), S(0.017), S(0.040)), S(0.012), x, y),
            new RoundCone(L(0, -0.004, -0.035), L(0.004, -0.01, -0.085), S(0.027), S(0.026)),
        };

        for (int i = 0; i < 4; i++)
        {
            double fx = -0.03 + i * 0.02;
            hand.Add(new RoundCone(L(fx, 0.002, 0.04), L(fx, -0.026, 0.058), S(0.012), S(0.0115)));
            hand.Add(new RoundCone(L(fx, -0.026, 0.058), L(fx, -0.042, 0.028), S(0.0115), S(0.0105)));
        }

        hand.Add(new RoundCone(L(-0.046, -0.012, -0.02), L(-0.036, -0.036, 0.03), S(0.014), S(0.0125)));
        hand.Add(new RoundCone(L(-0.036, -0.036, 0.03), L(-0.006, -0.046, 0.046), S(0.0125), S(0.011)));
        add(new Blend(S(0.008), hand.ToArray()), GloveRubber);

        // Броня кисти: пластина на тыльной стороне, щиток на костяшках, накладки на фалангах.
        add(Shapes.ChamferBox(L(0, 0.014, 0.042), new Vec3(S(0.045), S(0.006), S(0.009)), S(0.004), xs, y), Steel);
        for (int i = 0; i < 4; i++)
        {
            double fx = -0.03 + i * 0.02;
            add(new Box(L(fx, 0.0, 0.058), new Vec3(S(0.008), S(0.004), S(0.007)), S(0.002), xs, y), DarkSteel);
        }

        foreach (double bx in new[] { -0.036, 0.036 })
            add(new Sphere(L(bx, 0.023, 0.042), S(0.0035)), Steel);

        // Манжета и бронированное предплечье.
        Vec3 wrist = L(0.004, -0.008, -0.082);
        Vec3 armDir = (elbow - wrist).Normalized;
        add(new Cylinder(wrist - armDir * S(0.004), wrist + armDir * S(0.022), S(0.033)), DarkSteel);
        add(new Cylinder(wrist + armDir * S(0.006), wrist + armDir * S(0.013), S(0.034)), OrangeAccent);
        add(new RoundCone(wrist + armDir * S(0.02), elbow, S(0.032), S(0.05)), Sleeve);

        Vec3 bracerU = up.Cross(armDir).Normalized;
        Vec3 bracerV = armDir.Cross(bracerU).Normalized;
        Vec3 bracerCenter = wrist + armDir * S(0.1) + bracerV * S(0.02);
        add(Shapes.ChamferBox(bracerCenter, new Vec3(S(0.036), S(0.02), S(0.07)), S(0.012), bracerU, bracerV), ArmorPlate);
    }

    // ============================================================
    //   Кулак (перчатка)
    // ============================================================

    private static Canvas Fist(Vec3 position, double lift)
    {
        var scene = new Scene();
        Glove(scene.Add, position, new Vec3(-0.22, 0.3 + lift, 1), new Vec3(0, 1, -0.3 - lift), 1, 1.25,
            elbow: position + new Vec3(0.2, -0.32, -0.3));
        return Frame(scene, null, 0);
    }

    // ============================================================
    //   Пистолет: тяжёлый боевой, затвор с насечками, курок, целики
    // ============================================================

    private static Canvas Pistol(double slideBack, double kick, double flash)
    {
        var scene = new Scene();
        var k = new Kick(scene, new Vec3(0, -0.17, 0.27), kick);
        const double y = -0.105;
        double s = slideBack;

        // Затвор: скошенные рёбра, насечки сзади, окно выброса справа.
        Material slideMetal = Material.Metal(Rgb.Hex(0x58606e), 0.85) with
        {
            Pattern = p => p.Z < 0.33 + s && ((int)Math.Floor(p.Z * 280) & 1) == 0 ? 0.55 : 1.0
        };
        k.Add(new Cut(Shapes.ChamferBox(new Vec3(0, y, 0.42 + s), new Vec3(0.019, 0.017, 0.18), 0.007),
            new Box(new Vec3(0.02, y + 0.004, 0.41 + s), new Vec3(0.008, 0.007, 0.03))), slideMetal);
        k.Add(new Box(new Vec3(0.011, y + 0.002, 0.41 + s), new Vec3(0.004, 0.005, 0.026), 0.001), Brass);
        k.Add(new Box(new Vec3(0, y + 0.0175, 0.43 + s), new Vec3(0.008, 0.0015, 0.15), 0.001), DarkSteel);

        // Целик со светлыми точками и мушка.
        k.Add(new Cut(new Box(new Vec3(0, y + 0.022, 0.262 + s), new Vec3(0.015, 0.006, 0.007), 0.001),
            new Box(new Vec3(0, y + 0.027, 0.262 + s), new Vec3(0.003, 0.005, 0.01))), DarkSteel);
        foreach (double dx in new[] { -0.008, 0.008 })
            k.Add(new Sphere(new Vec3(dx, y + 0.024, 0.255 + s), 0.0018), Material.Glow(Rgb.Hex(0xe8f0d8)));
        k.Add(new Box(new Vec3(0, y + 0.022, 0.59 + s), new Vec3(0.0025, 0.005, 0.006), 0.001), DarkSteel);
        k.Add(new Sphere(new Vec3(0, y + 0.026, 0.584 + s), 0.0017), Material.Glow(Rgb.Hex(0xff6a20)));

        // Рамка с направляющей, курок, ствол в торце.
        k.Add(Shapes.ChamferBox(new Vec3(0, y - 0.027, 0.44), new Vec3(0.017, 0.011, 0.15), 0.004), Gunmetal);
        k.Add(new Box(new Vec3(0, y - 0.04, 0.5), new Vec3(0.012, 0.004, 0.08), 0.001), DarkSteel);
        k.Add(new Box(new Vec3(0, y + 0.006, 0.236 + s * 0.5), new Vec3(0.006, 0.011, 0.006), 0.002, null, new Vec3(0, 1, -0.4)), DarkSteel);
        k.Add(new Cylinder(new Vec3(0, y - 0.002, 0.59 + s), new Vec3(0, y - 0.002, 0.605 + s), 0.008), Hole);

        // Рукоять с насечкой (почти вся в перчатке).
        k.Add(new Box(new Vec3(0, y - 0.07, 0.29), new Vec3(0.016, 0.04, 0.028), 0.006, null, new Vec3(0, 1, 0.25)), Polymer);

        Glove(k.Add, new Vec3(0.004, -0.163, 0.27), new Vec3(-0.05, 0.3, 1), new Vec3(0, 1, -0.3), 1, 1.0,
            elbow: new Vec3(0.12, -0.45, 0.05));
        return Frame(scene, k.Apply(new Vec3(0, y, 0.62)), flash, seed: 3);
    }

    // ============================================================
    //   Дробовик: помповый, кожух с прорезями, патроны на боку
    // ============================================================

    private static Canvas Shotgun(double kickBack, double kick, double pump, double flash)
    {
        var scene = new Scene();
        var k = new Kick(scene, new Vec3(0, -0.15, 0.25), kick);
        const double y = -0.098;
        double z0 = kickBack;

        k.Add(new Cylinder(new Vec3(0, y, 0.3 + z0), new Vec3(0, y, 1.0 + z0), 0.0145), Gunmetal);
        k.Add(new Cylinder(new Vec3(0, y, 0.985 + z0), new Vec3(0, y, 1.0 + z0), 0.009), Hole);

        // Кожух-радиатор: верхняя половина трубы с прорезями.
        Sdf shield = new Cut(new Cylinder(new Vec3(0, y, 0.45 + z0), new Vec3(0, y, 0.95 + z0), 0.0195),
            new Cylinder(new Vec3(0, y, 0.4 + z0), new Vec3(0, y, 1.0 + z0), 0.0165),
            new Box(new Vec3(0, y - 0.05, 0.7 + z0), new Vec3(0.05, 0.046, 0.4)),
            Shapes.Repeat(i => new Box(new Vec3(0, y + 0.02, 0.48 + z0 + i * 0.045), new Vec3(0.007, 0.01, 0.013)), 10));
        k.Add(shield, DarkSteel);
        k.Add(new Sphere(new Vec3(0, y + 0.021, 0.94 + z0), 0.0035), Material.Glow(Rgb.Hex(0xff6a20)));

        // Подствольный магазин и цевьё с рёбрами.
        k.Add(new Cylinder(new Vec3(0, y - 0.028, 0.3 + z0), new Vec3(0, y - 0.028, 0.92 + z0), 0.011), DarkSteel);
        Material ribbed = Polymer with { Pattern = p => ((int)Math.Floor((p.Z - pump) * 130) & 1) == 0 ? 0.6 : 1.05 };
        k.Add(new Cylinder(new Vec3(0, y - 0.027, 0.52 + z0 + pump), new Vec3(0, y - 0.027, 0.72 + z0 + pump), 0.024), ribbed);

        // Ствольная коробка, окно выброса, кольцевой целик, патроны на левом боку.
        k.Add(new Cut(Shapes.ChamferBox(new Vec3(0, y - 0.018, 0.29 + z0), new Vec3(0.027, 0.03, 0.11), 0.009),
            new Box(new Vec3(0.028, y - 0.01, 0.3 + z0), new Vec3(0.006, 0.01, 0.035))), Gunmetal);
        k.Add(new Cylinder(new Vec3(0.016, y - 0.01, 0.28 + z0), new Vec3(0.016, y - 0.01, 0.32 + z0), 0.008), ShellRed);
        k.Add(new Torus(new Vec3(0, y + 0.022, 0.24 + z0), new Vec3(0, 0, 1), 0.008, 0.0025), DarkSteel);
        k.Add(new Box(new Vec3(0, y + 0.014, 0.24 + z0), new Vec3(0.003, 0.006, 0.003), 0.001), DarkSteel);
        for (int i = 0; i < 4; i++)
        {
            double z = 0.24 + z0 + i * 0.026;
            k.Add(new Cylinder(new Vec3(-0.034, y - 0.04, z), new Vec3(-0.034, y + 0.004, z), 0.0085), ShellRed);
            k.Add(new Cylinder(new Vec3(-0.034, y + 0.004, z), new Vec3(-0.034, y + 0.01, z), 0.009), Brass);
        }

        // Левая рука на цевье, правая — внизу на рукояти.
        Glove(k.Add, new Vec3(-0.042, -0.15, 0.6 + z0 + pump), new Vec3(0.35, 0.35, 1), new Vec3(0.35, 1, -0.2), -1, 0.95,
            elbow: new Vec3(-0.22, -0.42, 0.15 + pump));
        Glove(k.Add, new Vec3(0.01, -0.205, 0.24 + z0), new Vec3(-0.05, 0.3, 1), new Vec3(0, 1, -0.3), 1, 1.0,
            elbow: new Vec3(0.14, -0.5, 0.0));
        return Frame(scene, k.Apply(new Vec3(0, y, 1.0 + z0)), flash, seed: 5);
    }

    // ============================================================
    //   Пулемёт: роторный, основной блок с мотором, подача ленты, рукоять-дуга
    // ============================================================

    private static Canvas Chaingun(double phaseDegrees, double kickBack, double flash)
    {
        var scene = new Scene();
        const double y = -0.098;
        double z0 = kickBack;
        const double ring = 0.032;

        for (int i = 0; i < 6; i++)
        {
            double angle = (phaseDegrees + i * 60) * Math.PI / 180;
            var offset = new Vec3(Math.Cos(angle) * ring, Math.Sin(angle) * ring, 0);
            var start = new Vec3(0, y, 0.42 + z0) + offset;
            var end = new Vec3(0, y, 1.05 + z0) + offset;
            scene.Add(new Cylinder(start, end, 0.0105), Steel);
            scene.Add(new Cylinder(end - new Vec3(0, 0, 0.004), end + new Vec3(0, 0, 0.001), 0.0055), Hole);
        }

        scene.Add(new Cylinder(new Vec3(0, y, 0.42 + z0), new Vec3(0, y, 1.02 + z0), 0.013), DarkSteel);

        // Хомуты блока стволов с болтами.
        foreach (double z in new[] { 0.6, 0.83, 1.0 })
        {
            scene.Add(new Cut(new Cylinder(new Vec3(0, y, z + z0), new Vec3(0, y, z + 0.022 + z0), ring + 0.017),
                new Cylinder(new Vec3(0, y, z - 0.01 + z0), new Vec3(0, y, z + 0.04 + z0), ring - 0.015)), Gunmetal);
            for (int b = 0; b < 6; b++)
            {
                double angle = (phaseDegrees + 30 + b * 60) * Math.PI / 180;
                scene.Add(new Sphere(new Vec3(Math.Cos(angle) * (ring + 0.012), y + Math.Sin(angle) * (ring + 0.012), z - 0.002 + z0), 0.0032), Steel);
            }
        }

        // Барабан мотора с рёбрами охлаждения.
        scene.Add(new Cylinder(new Vec3(0, y, 0.4 + z0), new Vec3(0, y, 0.46 + z0), 0.046), DarkSteel);
        for (int i = 0; i < 3; i++)
            scene.Add(new Cylinder(new Vec3(0, y, 0.405 + i * 0.02 + z0), new Vec3(0, y, 0.41 + i * 0.02 + z0), 0.05), Gunmetal);

        // Основной блок: скошенный корпус, полоса предупреждения, индикатор.
        Material housing = Gunmetal with { Pattern = p => Seam(p.Z, 0.05) * Seam(p.X + 1, 0.045) };
        scene.Add(Shapes.ChamferBox(new Vec3(0, y - 0.075, 0.3 + z0), new Vec3(0.075, 0.05, 0.14), 0.02), housing);
        scene.Add(new Box(new Vec3(0, y - 0.075, 0.441 + z0), new Vec3(0.06, 0.03, 0.002), 0.001), Hazard);
        scene.Add(new Box(new Vec3(0.05, y - 0.03, 0.3 + z0), new Vec3(0.006, 0.003, 0.004), 0.001), Material.Glow(Rgb.Hex(0x40ff60)));

        // Короб с лентой слева: латунные патроны уходят в корпус.
        scene.Add(Shapes.ChamferBox(new Vec3(-0.098, y - 0.08, 0.28 + z0), new Vec3(0.024, 0.04, 0.07), 0.006), ArmorPlate);
        for (int i = 0; i < 5; i++)
            scene.Add(new Cylinder(new Vec3(-0.088 + i * 0.008, y - 0.03 + i * 0.002, 0.29 + z0),
                new Vec3(-0.088 + i * 0.008, y - 0.03 + i * 0.002, 0.31 + z0), 0.0055), Brass);

        Glove(scene.Add, new Vec3(-0.135, -0.165, 0.3 + z0), new Vec3(0.3, 0.35, 1), new Vec3(0.25, 1, -0.3), -1, 0.95,
            elbow: new Vec3(-0.28, -0.45, 0.05));
        Glove(scene.Add, new Vec3(0.135, -0.165, 0.3 + z0), new Vec3(-0.3, 0.35, 1), new Vec3(-0.25, 1, -0.3), 1, 0.95,
            elbow: new Vec3(0.28, -0.45, 0.05));
        return Frame(scene, new Vec3(0, y, 1.06 + z0), flash, seed: 7);
    }

    // ============================================================
    //   Ракетница: плечевая труба справа, без рук
    // ============================================================

    private static Canvas RocketLauncher(double kickBack, bool loaded, double flash)
    {
        var scene = new Scene();
        const double x = 0.085, y = -0.09;
        double z0 = kickBack;
        Material tubePaint = Material.Painted(Rgb.Hex(0x4e5a3a)) with
        {
            Pattern = p => Seam(p.Z, 0.11) * (0.9 + 0.14 * Noise.Unit((int)(p.X * 300), (int)(p.Z * 300), 961))
        };

        scene.Add(new Cut(new Cylinder(new Vec3(x, y, 0.1 + z0), new Vec3(x, y, 0.9 + z0), 0.062),
            new Cylinder(new Vec3(x, y, 0.8 + z0), new Vec3(x, y, 1.1 + z0), 0.052)), tubePaint);
        scene.Add(new Cut(new Cone(new Vec3(x, y, 0.88 + z0), new Vec3(x, y, 0.97 + z0), 0.064, 0.074),
            new Cylinder(new Vec3(x, y, 0.8 + z0), new Vec3(x, y, 1.1 + z0), 0.054)), DarkSteel);
        scene.Add(new Cylinder(new Vec3(x, y, 0.8 + z0), new Vec3(x, y, 0.805 + z0), 0.052), Hole);
        if (loaded)
        {
            scene.Add(new Cylinder(new Vec3(x, y, 0.8 + z0), new Vec3(x, y, 0.88 + z0), 0.034), Steel);
            scene.Add(new Cone(new Vec3(x, y, 0.88 + z0), new Vec3(x, y, 0.94 + z0), 0.034, 0.006), Material.Painted(Rgb.Hex(0xb02820)));
        }

        // Усиливающие кольца, полоса предупреждения у дула.
        foreach (double z in new[] { 0.32, 0.56 })
            scene.Add(new Torus(new Vec3(x, y, z + z0), new Vec3(0, 0, 1), 0.063, 0.006), Steel);
        scene.Add(new Cylinder(new Vec3(x, y, 0.76 + z0), new Vec3(x, y, 0.8 + z0), 0.0635), Hazard);

        // Прицельный блок слева сверху и блок электроники с огоньками.
        scene.Add(Shapes.ChamferBox(new Vec3(x - 0.052, y + 0.05, 0.42 + z0), new Vec3(0.014, 0.02, 0.08), 0.005, null, new Vec3(0.3, 1, 0)), DarkSteel);
        scene.Add(new Cylinder(new Vec3(x - 0.052, y + 0.055, 0.335 + z0), new Vec3(x - 0.052, y + 0.055, 0.34 + z0), 0.008),
            Material.Glow(Rgb.Hex(0xff3020)));
        scene.Add(Shapes.ChamferBox(new Vec3(x - 0.065, y - 0.02, 0.36 + z0), new Vec3(0.012, 0.03, 0.09), 0.005), Gunmetal);
        for (int i = 0; i < 3; i++)
            scene.Add(new Box(new Vec3(x - 0.078, y - 0.005 - i * 0.015, 0.3 + z0), new Vec3(0.002, 0.004, 0.006), 0.001),
                Material.Glow(i == 0 ? Rgb.Hex(0x40ff60) : Rgb.Hex(0x34c4f4)));

        return Frame(scene, new Vec3(x, y, 0.98 + z0), flash, seed: 9);
    }

    // ============================================================
    //   Плазменная винтовка
    // ============================================================

    private static Canvas Plasma(double kickBack, double glow, double flash)
    {
        var scene = new Scene();
        const double x = 0.012, y = -0.1;
        double z0 = kickBack;
        Material body = Material.Metal(Rgb.Hex(0x3c4a5e), 0.75) with { Pattern = p => Seam(p.Z, 0.07) };
        Material cyan = Glow(Rgb.Hex(0x00708e), Rgb.Hex(0xe8ffff), glow);

        scene.Add(Shapes.ChamferBox(new Vec3(x, y - 0.042, 0.36 + z0), new Vec3(0.042, 0.038, 0.17), 0.015), body);

        // Камера с энергией сверху: светящаяся трубка в стальной клетке.
        scene.Add(new Cylinder(new Vec3(x, y + 0.006, 0.24 + z0), new Vec3(x, y + 0.006, 0.5 + z0), 0.011), cyan);
        foreach (double dx in new[] { -0.016, 0.016 })
            scene.Add(new Box(new Vec3(x + dx, y + 0.006, 0.37 + z0), new Vec3(0.003, 0.012, 0.14), 0.001), Steel);
        for (int i = 0; i < 6; i++)
            scene.Add(new Box(new Vec3(x, y + 0.019, 0.25 + i * 0.048 + z0), new Vec3(0.019, 0.002, 0.003), 0.001), Steel);

        // Ствол с рёбрами радиатора, катушки, излучатель с двумя зубцами.
        scene.Add(new Cylinder(new Vec3(x, y - 0.01, 0.5 + z0), new Vec3(x, y - 0.01, 0.95 + z0), 0.014), Steel);
        for (int i = 0; i < 8; i++)
            scene.Add(new Cylinder(new Vec3(x, y - 0.01, 0.55 + i * 0.03 + z0), new Vec3(x, y - 0.01, 0.556 + i * 0.03 + z0), 0.028), DarkSteel);
        foreach (double z in new[] { 0.82, 0.86 })
            scene.Add(new Torus(new Vec3(x, y - 0.01, z + z0), new Vec3(0, 0, 1), 0.02, 0.0055), cyan);
        scene.Add(Shapes.ChamferBox(new Vec3(x, y - 0.01, 0.92 + z0), new Vec3(0.024, 0.024, 0.03), 0.008), Gunmetal);
        foreach (double dy in new[] { -0.02, 0.02 })
            scene.Add(new Box(new Vec3(x, y - 0.01 + dy, 0.97 + z0), new Vec3(0.008, 0.005, 0.025), 0.002), DarkSteel);
        scene.Add(new Sphere(new Vec3(x, y - 0.01, 0.975 + z0), 0.009), cyan);

        // Индикаторы заряда на корпусе.
        for (int i = 0; i < 3; i++)
            scene.Add(new Box(new Vec3(x + 0.043, y - 0.04, 0.3 + i * 0.03 + z0), new Vec3(0.002, 0.004, 0.008), 0.001),
                i < (int)Math.Round(glow * 3) ? cyan : Hole);

        Glove(scene.Add, new Vec3(x + 0.004, -0.2, 0.3 + z0), new Vec3(-0.05, 0.3, 1), new Vec3(0, 1, -0.3), 1, 1.0,
            elbow: new Vec3(0.13, -0.48, 0.05));
        return Frame(scene, new Vec3(x, y - 0.01, 0.99 + z0), flash, Sprite.PlasmaFlash, seed: 11);
    }

    // ============================================================
    //   BFG9000: реактор под куполом, конденсаторы, излучатель с зубцами
    // ============================================================

    private static Canvas Bfg(double kickBack, double glow, double flash)
    {
        var scene = new Scene();
        const double y = -0.11;
        double z0 = kickBack;
        Material body = Material.Metal(Rgb.Hex(0x3e4a40), 0.7) with { Pattern = p => Seam(p.Z, 0.08) * Seam(p.X + 1, 0.07) };
        Material green = Glow(Rgb.Hex(0x1a4c1c), Rgb.Hex(0xe8ffc0), glow);

        scene.Add(Shapes.ChamferBox(new Vec3(0, y - 0.065, 0.4 + z0), new Vec3(0.13, 0.06, 0.2), 0.03), body);
        scene.Add(Shapes.ChamferBox(new Vec3(0, y - 0.01, 0.38 + z0), new Vec3(0.085, 0.02, 0.16), 0.012), Gunmetal);

        // Реактор под стальным куполом-клеткой.
        scene.Add(new Sphere(new Vec3(0, y + 0.012, 0.37 + z0), 0.045), green);
        foreach (double angle in new[] { -0.6, 0.0, 0.6 })
            scene.Add(new Torus(new Vec3(0, y + 0.012, 0.37 + z0), new Vec3(Math.Cos(angle), 0, Math.Sin(angle)), 0.05, 0.004), Steel);
        scene.Add(new Torus(new Vec3(0, y + 0.004, 0.37 + z0), new Vec3(0, 1, 0), 0.054, 0.006), DarkSteel);

        // Конденсаторы вдоль боков.
        foreach (double side in new[] { -1.0, 1.0 })
        {
            scene.Add(new Cylinder(new Vec3(side * 0.118, y - 0.03, 0.25 + z0), new Vec3(side * 0.118, y - 0.03, 0.56 + z0), 0.013), green);
            foreach (double z in new[] { 0.3, 0.42, 0.52 })
                scene.Add(new Cylinder(new Vec3(side * 0.118, y - 0.03, z + z0), new Vec3(side * 0.118, y - 0.03, z + 0.012 + z0), 0.017), DarkSteel);
        }

        // Излучатель: кольцо с зубцами и свечением внутри, полоса предупреждения.
        var mouth = new Vec3(0, y - 0.04, 0.6 + z0);
        scene.Add(new Cut(new Cylinder(mouth, mouth + new Vec3(0, 0, 0.08), 0.085),
            new Cylinder(mouth - new Vec3(0, 0, 0.01), mouth + new Vec3(0, 0, 0.1), 0.062)), DarkSteel);
        scene.Add(new Cylinder(mouth + new Vec3(0, 0, 0.02), mouth + new Vec3(0, 0, 0.025), 0.062), green);
        for (int i = 0; i < 8; i++)
        {
            double angle = i * Math.PI / 4 + Math.PI / 8;
            var radial = new Vec3(Math.Cos(angle), Math.Sin(angle), 0);
            scene.Add(new Box(mouth + new Vec3(0, 0, 0.09) + radial * 0.072, new Vec3(0.012, 0.008, 0.016), 0.002,
                radial, new Vec3(0, 0, 1).Cross(radial)), Steel);
        }

        scene.Add(new Box(new Vec3(0, y - 0.1, 0.601 + z0), new Vec3(0.1, 0.012, 0.002), 0.001), Hazard);

        Glove(scene.Add, new Vec3(-0.19, -0.19, 0.36 + z0), new Vec3(0.3, 0.35, 1), new Vec3(0.25, 1, -0.3), -1, 0.95,
            elbow: new Vec3(-0.32, -0.45, 0.05));
        Glove(scene.Add, new Vec3(0.19, -0.19, 0.36 + z0), new Vec3(-0.3, 0.35, 1), new Vec3(-0.25, 1, -0.3), 1, 0.95,
            elbow: new Vec3(0.32, -0.45, 0.05));
        return Frame(scene, mouth + new Vec3(0, 0, 0.1), flash, Sprite.BfgFlash, seed: 13);
    }

    // ============================================================
    //   Бензопила: промышленная, шина выходит из корпуса и уходит вдаль
    // ============================================================

    private static Canvas Chainsaw(double shakeX, double thrust, int chainPhase)
    {
        var scene = new Scene();
        double x0 = 0.015 + shakeX, z0 = thrust;
        const double y = -0.13;

        // Шина уходит вперёд и влево по диагонали — так видна вся её длина с цепью;
        // пластина повёрнута на ребро, чтобы были видны и бок, и цепь по кромке.
        Vec3 barDir = new Vec3(-0.5, 0.12, 1).Normalized;
        Vec3 barBase = new Vec3(x0, y - 0.02, 0.34 + z0);
        Vec3 barSide = new Vec3(0, 1, 0).Cross(barDir).Normalized;
        Vec3 barUp = barDir.Cross(barSide).Normalized;
        const double tilt = 1.3;
        (barSide, barUp) = (barSide * Math.Cos(tilt) + barUp * Math.Sin(tilt), barUp * Math.Cos(tilt) - barSide * Math.Sin(tilt));
        const double barLength = 0.42;
        Vec3 barCenter = barBase + barDir * barLength;
        Material barSteel = Steel with { Pattern = p => 0.9 + 0.1 * Math.Sin(p.Z * 120) };
        scene.Add(new Box(barCenter, new Vec3(0.005, 0.05, barLength), 0.003, barSide, barUp), barSteel);
        Vec3 nose = barCenter + barDir * barLength;
        scene.Add(new Cylinder(nose - barSide * 0.006, nose + barSide * 0.006, 0.05), barSteel);

        // Цепь: звенья по кромкам, зубья наружу; через кадр звенья сдвигаются на полшага.
        for (int i = 0; i < 46; i++)
        {
            double along = 0.02 + i * 0.018 + chainPhase * 0.009;
            foreach (int sign in new[] { 1, -1 })
            {
                bool tooth = (i + chainPhase) % 2 == 0;
                Vec3 center = barBase + barDir * along + barUp * ((0.052 + (tooth ? 0.004 : 0)) * sign);
                scene.Add(new Box(center, new Vec3(0.007, tooth ? 0.007 : 0.005, 0.007), 0.001, barSide, barUp),
                    tooth ? Steel : DarkSteel);
            }
        }

        // Корпус: жёлтый с чёрными полосами, двигатель с рёбрами, глушитель.
        scene.Add(Shapes.ChamferBox(new Vec3(x0, y - 0.085, 0.22 + z0), new Vec3(0.06, 0.05, 0.12), 0.015), Hazard);
        scene.Add(Shapes.ChamferBox(new Vec3(x0 - 0.05, y - 0.09, 0.21 + z0), new Vec3(0.022, 0.045, 0.1), 0.008), DarkSteel);
        for (int i = 0; i < 6; i++)
            scene.Add(new Box(new Vec3(x0 - 0.073, y - 0.09, 0.14 + i * 0.026 + z0), new Vec3(0.002, 0.04, 0.005), 0.001), Gunmetal);
        scene.Add(new Cylinder(new Vec3(x0 + 0.065, y - 0.1, 0.17 + z0), new Vec3(x0 + 0.065, y - 0.1, 0.3 + z0), 0.012), Steel);
        scene.Add(Shapes.ChamferBox(new Vec3(x0 - 0.01, y - 0.03, 0.33 + z0), new Vec3(0.035, 0.03, 0.035), 0.01), DarkSteel); // крепление шины

        // Правая рука на задней рукояти (левая за кадром — так шина не закрыта рукавом).
        Glove(scene.Add, new Vec3(x0 + 0.07, -0.215, 0.2 + z0), new Vec3(-0.1, 0.3, 1), new Vec3(0, 1, -0.3), 1, 1.0,
            elbow: new Vec3(0.3, -0.5, 0.0));
        return Frame(scene, null, 0);
    }
}

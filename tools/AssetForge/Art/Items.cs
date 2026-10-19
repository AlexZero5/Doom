using AssetForge.Core;

namespace AssetForge.Art;

/// <summary>
///     Бонусы на полу и декорации (спрайты-билборды): 3D-модели, отрендеренные сбоку чуть сверху
///     почти без перспективы. Основание модели — на полу (y = 0), центр — x = z = 0.
///     Рядом с картинкой важен масштаб: <c>PixelsPerUnit</c> пикселей на единицу мира
///     (1 = высота стены) — по нему игра считает размер спрайта.
/// </summary>
internal static class Items
{
    public const int PickupPixelsPerUnit = 80;
    public const int DecorPixelsPerUnit = 48;

    private static readonly Material Brass = Material.Metal(Rgb.Hex(0xd6b222), 0.6);
    private static readonly Material Gunmetal = Material.Metal(Rgb.Hex(0x3e444e));
    private static readonly Material Steel = Material.Metal(Rgb.Hex(0x8a96a6), 0.7);
    private static readonly Material Dark = Material.Metal(Rgb.Hex(0x262a30), 0.45);
    private static readonly Material Wood = new(Rgb.Hex(0x8c5530), 0.15, 8, Pattern: p => 0.85 + 0.15 * Math.Sin(p.X * 90 + Math.Sin(p.Y * 40)));
    private static readonly Material Olive = Material.Metal(Rgb.Hex(0x5c6648), 0.3);
    private static readonly Material Red = Material.Metal(Rgb.Hex(0xac2c20), 0.35);
    private static readonly Material Orange = Material.Metal(Rgb.Hex(0xe0701c), 0.35);

    public static IEnumerable<(string Name, Canvas Image)> Pickups()
    {
        yield return ("pickup_clip", Render(Clip()));
        yield return ("pickup_bulletbox", Render(BulletBox()));
        yield return ("pickup_shells", Render(Shells()));
        yield return ("pickup_shellbox", Render(ShellBox()));
        yield return ("pickup_rocket", Render(Rocket()));
        yield return ("pickup_rocketbox", Render(RocketBox()));
        yield return ("pickup_cell", Render(Cell(0.16)));
        yield return ("pickup_cellpack", Render(Cell(0.28)));
        yield return ("pickup_chainsaw", Render(ChainsawSide()));
        yield return ("pickup_shotgun", Render(ShotgunSide()));
        yield return ("pickup_chaingun", Render(ChaingunSide()));
        yield return ("pickup_rocketlauncher", Render(LauncherSide()));
        yield return ("pickup_plasmarifle", Render(PlasmaSide()));
        yield return ("pickup_bfg9000", Render(BfgSide()));
    }

    public static IEnumerable<(string Name, Canvas Image, int PixelsPerUnit)> Decorations()
    {
        yield return ("decor_tree", Render(Tree(), DecorPixelsPerUnit), DecorPixelsPerUnit);
        yield return ("decor_pine", Render(Pine(), DecorPixelsPerUnit), DecorPixelsPerUnit);
        yield return ("decor_bush", Render(Bush(), DecorPixelsPerUnit), DecorPixelsPerUnit);
        yield return ("decor_plant", Render(PottedPlant(), PickupPixelsPerUnit), PickupPixelsPerUnit);
        yield return ("decor_floorlamp", Render(FloorLamp(), PickupPixelsPerUnit), PickupPixelsPerUnit);
        yield return ("decor_streetlamp", Render(StreetLamp(), DecorPixelsPerUnit), DecorPixelsPerUnit);
        yield return ("decor_barrel", Render(Barrel(), PickupPixelsPerUnit), PickupPixelsPerUnit);
        yield return ("decor_flowers", Render(Flowers(), PickupPixelsPerUnit), PickupPixelsPerUnit);
    }

    /// <summary>
    ///     Рендер сбоку: камера далеко (почти без перспективы) и чуть выше. Холст с запасом,
    ///     потом обрезается по силуэту; низ спрайта = пол.
    /// </summary>
    private static Canvas Render(Scene scene, int pixelsPerUnit = PickupPixelsPerUnit)
    {
        const double distance = 8;
        bool large = pixelsPerUnit == DecorPixelsPerUnit;
        int size = large ? 200 : 160;
        double aimY = large ? 1.1 : 0.12;
        var camera = new Camera
        {
            Position = new Vec3(0, aimY + 1.0, -distance),
            Forward = new Vec3(0, -1.0, distance).Normalized,
            Focal = pixelsPerUnit * distance,
            CenterX = size / 2.0,
            CenterY = size * 0.5,
        };

        Canvas image = Renderer3D.Render(scene, camera, size, size, ambient: 0.38);
        image.Outline(Rgb.Hex(0x0e0a08));
        Palette.Apply(image, dither: 5);
        return image.Trim();
    }

    // ============================================================
    //   Боеприпасы
    // ============================================================

    private static Scene Clip()
    {
        var scene = new Scene();
        scene.Add(new Box(new Vec3(0, 0.1, 0), new Vec3(0.045, 0.1, 0.025), 0.008), Gunmetal);
        scene.Add(new RoundCone(new Vec3(0, 0.2, 0), new Vec3(0, 0.24, 0), 0.012, 0.006), Brass);
        return scene;
    }

    private static Scene BulletBox()
    {
        var scene = new Scene();
        scene.Add(new Box(new Vec3(0, 0.08, 0), new Vec3(0.17, 0.08, 0.11), 0.01), Olive);
        scene.Add(new Box(new Vec3(0, 0.09, -0.111), new Vec3(0.12, 0.03, 0.002), 0.001), Material.Matte(Rgb.Hex(0xd6b222)));
        for (int i = 0; i < 6; i++)
        for (int j = 0; j < 2; j++)
            scene.Add(new RoundCone(new Vec3(-0.12 + i * 0.048, 0.16, -0.04 + j * 0.08), new Vec3(-0.12 + i * 0.048, 0.2, -0.04 + j * 0.08), 0.014, 0.006), Brass);
        return scene;
    }

    private static void Shell(Scene scene, Vec3 a, Vec3 b)
    {
        scene.Add(new Cylinder(a, b, 0.022), Red);
        scene.Add(new Cylinder(a, a + (b - a) * 0.25, 0.023), Brass);
    }

    private static Scene Shells()
    {
        var scene = new Scene();
        for (int i = 0; i < 4; i++)
        {
            double z = -0.06 + i * 0.045;
            Shell(scene, new Vec3(-0.06 + i * 0.01, 0.022, z), new Vec3(0.08 + i * 0.01, 0.022, z));
        }

        return scene;
    }

    private static Scene ShellBox()
    {
        var scene = new Scene();
        scene.Add(new Box(new Vec3(0, 0.07, 0), new Vec3(0.18, 0.07, 0.1), 0.01), Material.Metal(Rgb.Hex(0x6c2018), 0.25));
        for (int i = 0; i < 5; i++)
            Shell(scene, new Vec3(-0.12 + i * 0.06, 0.14, -0.02), new Vec3(-0.12 + i * 0.06, 0.2, -0.02));
        return scene;
    }

    private static void RocketModel(Scene scene, Vec3 tail, Vec3 tip)
    {
        Vec3 axis = (tip - tail).Normalized;
        scene.Add(new Cylinder(tail, tip - axis * 0.1, 0.035), Material.Metal(Rgb.Hex(0x9a9e94), 0.5));
        scene.Add(new Cone(tip - axis * 0.1, tip, 0.035, 0.004), Red);
        foreach (Vec3 fin in new[] { new Vec3(0, 1, 0), new Vec3(0, -1, 0), new Vec3(0, 0, 1), new Vec3(0, 0, -1) })
            scene.Add(new Box(tail + axis * 0.04 + fin * 0.045, new Vec3(0.04, 0.003, 0.02), 0.002, axis, fin), Dark);
    }

    private static Scene Rocket()
    {
        var scene = new Scene();
        RocketModel(scene, new Vec3(-0.22, 0.05, 0), new Vec3(0.22, 0.05, 0));
        return scene;
    }

    private static Scene RocketBox()
    {
        var scene = new Scene();
        scene.Add(new Box(new Vec3(0, 0.1, 0), new Vec3(0.22, 0.1, 0.13), 0.008), Wood);
        for (int i = 0; i < 3; i++)
            scene.Add(new Cone(new Vec3(-0.12 + i * 0.12, 0.2, 0), new Vec3(-0.12 + i * 0.12, 0.29, 0), 0.035, 0.004), Red);
        return scene;
    }

    private static Scene Cell(double size)
    {
        var scene = new Scene();
        scene.Add(new Box(new Vec3(0, size * 0.5, 0), new Vec3(size * 0.45, size * 0.5, size * 0.3), 0.01), Material.Metal(Rgb.Hex(0x5e6e82), 0.5));
        for (int i = 0; i < 2; i++)
            scene.Add(new Box(new Vec3(0, size * (0.3 + i * 0.35), -size * 0.3), new Vec3(size * 0.38, size * 0.06, 0.004), 0.002),
                Material.Glow(Rgb.Hex(0x34c4f4)));
        scene.Add(new Cylinder(new Vec3(0, size, 0), new Vec3(0, size + 0.025, 0), size * 0.15), Dark);
        return scene;
    }

    // ============================================================
    //   Оружие на полу (вид сбоку)
    // ============================================================

    private static Scene ShotgunSide()
    {
        var scene = new Scene();
        scene.Add(new Cylinder(new Vec3(-0.05, 0.07, 0), new Vec3(0.45, 0.07, 0), 0.018), Gunmetal);
        scene.Add(new Cylinder(new Vec3(0.12, 0.04, 0), new Vec3(0.3, 0.04, 0), 0.026), Wood);
        scene.Add(new Box(new Vec3(-0.1, 0.05, 0), new Vec3(0.07, 0.035, 0.022), 0.006), Gunmetal);
        scene.Add(new Box(new Vec3(-0.3, 0.04, 0), new Vec3(0.14, 0.035, 0.022), 0.012, new Vec3(1, 0.15, 0)), Wood);
        return scene;
    }

    private static Scene ChaingunSide()
    {
        var scene = new Scene();
        for (int i = 0; i < 3; i++)
            scene.Add(new Cylinder(new Vec3(0, 0.05 + i * 0.022, 0), new Vec3(0.42, 0.05 + i * 0.022, 0), 0.011), Steel);
        scene.Add(new Cylinder(new Vec3(0.3, 0.072, 0), new Vec3(0.32, 0.072, 0), 0.045), Gunmetal);
        scene.Add(new Box(new Vec3(-0.08, 0.07, 0), new Vec3(0.11, 0.06, 0.045), 0.012), Dark);
        return scene;
    }

    private static Scene LauncherSide()
    {
        var scene = new Scene();
        scene.Add(new Cylinder(new Vec3(-0.3, 0.07, 0), new Vec3(0.3, 0.07, 0), 0.065), Olive);
        scene.Add(new Torus(new Vec3(0.05, 0.07, 0), new Vec3(1, 0, 0), 0.066, 0.008), Dark);
        scene.Add(new Box(new Vec3(0.02, 0.15, 0), new Vec3(0.05, 0.02, 0.012), 0.004), Dark);
        return scene;
    }

    private static Scene PlasmaSide()
    {
        var scene = new Scene();
        scene.Add(new Box(new Vec3(-0.08, 0.06, 0), new Vec3(0.16, 0.05, 0.04), 0.012), Material.Metal(Rgb.Hex(0x4a5a72), 0.5));
        scene.Add(new Cylinder(new Vec3(0.05, 0.08, 0), new Vec3(0.34, 0.08, 0), 0.02), Steel);
        foreach (double x in new[] { 0.15, 0.22, 0.29 })
            scene.Add(new Torus(new Vec3(x, 0.08, 0), new Vec3(1, 0, 0), 0.026, 0.008), Material.Glow(Rgb.Hex(0x34c4f4)));
        return scene;
    }

    private static Scene BfgSide()
    {
        var scene = new Scene();
        scene.Add(new Box(new Vec3(-0.05, 0.1, 0), new Vec3(0.24, 0.09, 0.1), 0.03), Material.Metal(Rgb.Hex(0x50604a), 0.35));
        scene.Add(new Cylinder(new Vec3(0.18, 0.12, 0), new Vec3(0.3, 0.12, 0), 0.075), Dark);
        scene.Add(new Box(new Vec3(-0.05, 0.12, -0.101), new Vec3(0.15, 0.025, 0.002), 0.001), Material.Glow(Rgb.Hex(0xb0ec70)));
        return scene;
    }

    private static Scene ChainsawSide()
    {
        var scene = new Scene();
        scene.Add(new Box(new Vec3(-0.12, 0.08, 0), new Vec3(0.12, 0.07, 0.05), 0.02), Orange);
        scene.Add(new Box(new Vec3(0.2, 0.08, 0), new Vec3(0.22, 0.03, 0.005), 0.004), Steel);
        scene.Add(new Torus(new Vec3(-0.12, 0.15, 0), new Vec3(0, 0, 1), 0.06, 0.008), Dark);
        return scene;
    }

    // ============================================================
    //   Декорации
    // ============================================================

    /// <summary>Лиственная крона: кучка шаров, слитых плавно, с «листвяным» узором яркости.</summary>
    private static Material Leaves(Rgb color, int seed) => new(color, 0.05, 4,
        Pattern: p => 0.75 + 0.5 * Noise.Unit((int)(p.X * 60) + 500, (int)(p.Y * 60 + p.Z * 37) + 500, seed));

    private static Scene Tree()
    {
        var scene = new Scene();
        scene.Add(new RoundCone(new Vec3(0, 0, 0), new Vec3(0.03, 1.3, 0), 0.11, 0.07),
            new Material(Rgb.Hex(0x5a3a22), 0.05, 4, Pattern: p => 0.8 + 0.3 * Noise.Unit((int)(p.X * 80) + 100, (int)(p.Y * 25), 701)));
        var crown = new List<Sdf>();
        for (int i = 0; i < 9; i++)
        {
            double a = i * 2.4;
            double r = 0.35 + 0.1 * Noise.Unit(i, 0, 702);
            crown.Add(new Sphere(new Vec3(Math.Cos(a) * 0.35, 1.55 + Math.Sin(i * 1.3) * 0.3, Math.Sin(a) * 0.3), r));
        }

        crown.Add(new Sphere(new Vec3(0, 1.85, 0), 0.45));
        scene.Add(new Blend(0.12, crown.ToArray()), Leaves(Rgb.Hex(0x3e8032), 703));
        return scene;
    }

    private static Scene Pine()
    {
        var scene = new Scene();
        scene.Add(new Cylinder(new Vec3(0, 0, 0), new Vec3(0, 0.5, 0), 0.07), Material.Matte(Rgb.Hex(0x4a2e1a)));
        for (int i = 0; i < 4; i++)
        {
            double y = 0.35 + i * 0.42;
            scene.Add(new Cone(new Vec3(0, y, 0), new Vec3(0, y + 0.6, 0), 0.55 - i * 0.11, 0.02), Leaves(Rgb.Hex(0x245a2c), 711 + i));
        }

        return scene;
    }

    private static Scene Bush()
    {
        var scene = new Scene();
        var parts = new List<Sdf>();
        for (int i = 0; i < 6; i++)
        {
            double a = i * 1.9;
            parts.Add(new Sphere(new Vec3(Math.Cos(a) * 0.28, 0.25 + 0.08 * Math.Sin(i * 2.1), Math.Sin(a) * 0.2), 0.24));
        }

        scene.Add(new Blend(0.1, parts.ToArray()), Leaves(Rgb.Hex(0x3a7a2c), 721));
        return scene;
    }

    private static Scene PottedPlant()
    {
        var scene = new Scene();
        scene.Add(new Cone(new Vec3(0, 0, 0), new Vec3(0, 0.16, 0), 0.07, 0.095), Material.Matte(Rgb.Hex(0xb05a30)));
        var parts = new List<Sdf>();
        for (int i = 0; i < 7; i++)
        {
            double a = i * 0.9;
            parts.Add(new RoundCone(new Vec3(0, 0.15, 0), new Vec3(Math.Cos(a) * 0.12, 0.32 + 0.1 * Math.Sin(i), Math.Sin(a) * 0.08), 0.02, 0.045));
        }

        scene.Add(new Blend(0.03, parts.ToArray()), Leaves(Rgb.Hex(0x40903a), 731));
        return scene;
    }

    private static Scene FloorLamp()
    {
        var scene = new Scene();
        scene.Add(new Cylinder(new Vec3(0, 0, 0), new Vec3(0, 0.03, 0), 0.1), Dark);
        scene.Add(new Cylinder(new Vec3(0, 0, 0), new Vec3(0, 0.7, 0), 0.012), Material.Metal(Rgb.Hex(0xb89a62), 0.6));
        scene.Add(new Cut(new Cone(new Vec3(0, 0.62, 0), new Vec3(0, 0.82, 0), 0.16, 0.09),
            new Cone(new Vec3(0, 0.6, 0), new Vec3(0, 0.8, 0), 0.15, 0.08)), Material.Matte(Rgb.Hex(0xe8d8a8)));
        scene.Add(new Sphere(new Vec3(0, 0.68, 0), 0.04), Material.Glow(Rgb.Hex(0xfff6a8)));
        return scene;
    }

    private static Scene StreetLamp()
    {
        var scene = new Scene();
        scene.Add(new Cylinder(new Vec3(0, 0, 0), new Vec3(0, 2.0, 0), 0.04), Dark);
        scene.Add(new RoundCone(new Vec3(0, 1.95, 0), new Vec3(0.3, 2.05, 0), 0.03, 0.025), Dark);
        scene.Add(new Cone(new Vec3(0.3, 2.08, 0), new Vec3(0.3, 1.94, 0), 0.04, 0.12), Dark);
        scene.Add(new Sphere(new Vec3(0.3, 1.94, 0), 0.07), Material.Glow(Rgb.Hex(0xfff0b0)));
        return scene;
    }

    private static Scene Barrel()
    {
        var scene = new Scene();
        scene.Add(new Cylinder(new Vec3(0, 0, 0), new Vec3(0, 0.5, 0), 0.18), Material.Metal(Rgb.Hex(0x40703a), 0.35));
        foreach (double y in new[] { 0.08, 0.25, 0.42 })
            scene.Add(new Torus(new Vec3(0, y, 0), new Vec3(0, 1, 0), 0.182, 0.012), Dark);
        scene.Add(new Cylinder(new Vec3(0, 0.495, 0), new Vec3(0, 0.505, 0), 0.15), Material.Glow(Rgb.Hex(0x80c040)));
        return scene;
    }

    private static Scene Flowers()
    {
        var scene = new Scene();
        Rgb[] petals = { Rgb.Hex(0xf0e060), Rgb.Hex(0xe04a4a), Rgb.Hex(0xdcb4f4), Rgb.Hex(0xffffff) };
        for (int i = 0; i < 7; i++)
        {
            double x = -0.18 + i * 0.06, z = (i % 2) * 0.06 - 0.03;
            double h = 0.18 + 0.06 * Noise.Unit(i, 1, 741);
            scene.Add(new Cylinder(new Vec3(x, 0, z), new Vec3(x, h, z), 0.006), Material.Matte(Rgb.Hex(0x2e6a28)));
            scene.Add(new Sphere(new Vec3(x, h, z), 0.028), Material.Matte(petals[i % petals.Length]));
        }

        scene.Add(new Box(new Vec3(0, 0.03, 0), new Vec3(0.22, 0.03, 0.06), 0.02), Material.Matte(Rgb.Hex(0x5a3a22)));
        return scene;
    }
}

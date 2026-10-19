using System.Globalization;

namespace Doom.World;

/// <summary>
///     Каталог готовых предметов для карт: мебель, крыши, лестницы, ящики, бочки… Каждый
///     собирается из 3D-боксов (с текстурой на каждую грань) и ставится строкой карты
///     <c>prop &lt;имя&gt; x y [угол] [параметры…]</c>. Твёрдые части становятся телами для
///     столкновений — на них можно запрыгнуть.
/// </summary>
/// <remarks>
///     Мебель описана в старых единицах (1 = 2.6 м) и масштабируется при установке; новые
///     предметы (лестницы, ящики, крыши) — сразу в метрах. Угол — в градусах: 0 — «лицом»
///     (фасадом шкафа, подъёмом лестницы) на север (−y), 90 — на восток, 180 — на юг.
/// </remarks>
internal static class Prefabs
{
    /// <summary>Масштаб старой мебели: 1 единица = 2.6 м.</summary>
    private const double Furniture = 2.6;

    private const double Metric = 1.0;

    public static bool TryPlace(Level level, string name, double x, double y, double z, double angleDegrees, string[] args)
    {
        if (!Builders.TryGetValue(name, out (double Scale, Action<Placer, string[]> Build) builder))
            return false;

        builder.Build(new Placer(level, x, y, z, angleDegrees * Math.PI / 180, builder.Scale), args);
        return true;
    }

    public static IEnumerable<string> Names => Builders.Keys;

    private static readonly Dictionary<string, (double, Action<Placer, string[]>)> Builders = new(StringComparer.OrdinalIgnoreCase)
    {
        // ---------------- Мебель (старые единицы) ----------------
        ["table"] = (Furniture, (p, _) =>
        {
            p.Box(0, 0, 0.56, 0.38, 0.03, 0.27, BoxSkin.All(p.T("wood_light")), solid: true);
            foreach ((double lx, double ly) in Corners(0.24, 0.15))
                p.Box(lx, ly, 0.035, 0.035, 0.27, 0, BoxSkin.All(p.T("wood_dark")));
        }),
        ["coffeetable"] = (Furniture, (p, _) =>
        {
            p.Box(0, 0, 0.4, 0.24, 0.025, 0.14, BoxSkin.All(p.T("wood_dark")), solid: true);
            foreach ((double lx, double ly) in Corners(0.17, 0.09))
                p.Box(lx, ly, 0.03, 0.03, 0.14, 0, BoxSkin.All(p.T("wood_dark")));
        }),
        ["chair"] = (Furniture, (p, _) =>
        {
            p.Box(0, 0, 0.17, 0.17, 0.025, 0.17, BoxSkin.All(p.T("fabric_red")), solid: true);
            p.Box(0, 0.075, 0.17, 0.025, 0.22, 0.19, BoxSkin.All(p.T("wood_dark")));
            foreach ((double lx, double ly) in Corners(0.07, 0.07))
                p.Box(lx, ly, 0.022, 0.022, 0.17, 0, BoxSkin.All(p.T("wood_dark")));
        }),
        ["bed"] = (Furniture, (p, _) =>
        {
            p.Box(0, 0, 0.56, 0.82, 0.13, 0.03, BoxSkin.All(p.T("wood_dark")), solid: true);
            p.Box(0, 0.02, 0.52, 0.76, 0.06, 0.16, BoxSkin.All(p.T("bedsheet")), uv: 0.3);
            p.Box(0, -0.31, 0.42, 0.12, 0.05, 0.22, BoxSkin.All(p.T("porcelain")));
            p.Box(0, -0.4, 0.56, 0.04, 0.34, 0.0, BoxSkin.All(p.T("wood_dark")), solid: true);
        }),
        ["sofa"] = (Furniture, (p, args) =>
        {
            int fabric = p.T(Arg(args, 0, "fabric_red"));
            p.Box(0, 0, 0.82, 0.3, 0.13, 0.03, BoxSkin.All(fabric), solid: true, uv: 0.3);
            p.Box(0, 0.12, 0.82, 0.07, 0.3, 0.03, BoxSkin.All(fabric), solid: true, uv: 0.3);
            p.Box(-0.38, 0, 0.07, 0.3, 0.22, 0.03, BoxSkin.All(fabric), uv: 0.3);
            p.Box(0.38, 0, 0.07, 0.3, 0.22, 0.03, BoxSkin.All(fabric), uv: 0.3);
        }),
        ["armchair"] = (Furniture, (p, args) =>
        {
            int fabric = p.T(Arg(args, 0, "fabric_blue"));
            p.Box(0, 0, 0.34, 0.3, 0.13, 0.03, BoxSkin.All(fabric), solid: true, uv: 0.3);
            p.Box(0, 0.12, 0.34, 0.07, 0.3, 0.03, BoxSkin.All(fabric), solid: true, uv: 0.3);
            p.Box(-0.15, 0, 0.06, 0.3, 0.22, 0.03, BoxSkin.All(fabric), uv: 0.3);
            p.Box(0.15, 0, 0.06, 0.3, 0.22, 0.03, BoxSkin.All(fabric), uv: 0.3);
        }),
        ["wardrobe"] = (Furniture, (p, _) =>
            p.Box(0, 0, 0.6, 0.24, 0.84, 0, BoxSkin.Front(p.T("wood_dark"), p.T("wardrobe_front")), solid: true)),
        ["bookshelf"] = (Furniture, (p, _) =>
            p.Box(0, 0, 0.6, 0.15, 0.8, 0, BoxSkin.Front(p.T("wood_dark"), p.T("books")), solid: true)),
        ["fridge"] = (Furniture, (p, _) =>
            p.Box(0, 0, 0.28, 0.26, 0.74, 0, BoxSkin.Front(p.T("enamel_white"), p.T("fridge_front")), solid: true)),
        ["stove"] = (Furniture, (p, _) =>
            p.Box(0, 0, 0.3, 0.26, 0.35, 0, new BoxSkin(p.T("stove_front"), p.T("enamel_white"), p.T("enamel_white"),
                p.T("enamel_white"), p.T("metal_dark"), p.T("metal_dark")), solid: true)),
        ["counter"] = (Furniture, (p, _) =>
            p.Box(0, 0, 0.4, 0.26, 0.35, 0, new BoxSkin(p.T("cabinet_front"), p.T("wood_light"), p.T("wood_light"),
                p.T("wood_light"), p.T("tiles_wall"), p.T("wood_light")), solid: true)),
        ["sink"] = (Furniture, (p, _) =>
        {
            p.Box(0, 0, 0.3, 0.24, 0.3, 0, BoxSkin.Front(p.T("porcelain"), p.T("cabinet_front")), solid: true);
            p.Box(0, 0, 0.26, 0.2, 0.02, 0.3, BoxSkin.All(p.T("water")));
            p.Box(0, 0.1, 0.03, 0.03, 0.12, 0.3, BoxSkin.All(p.T("metal_dark")));
        }),
        ["tv"] = (Furniture, (p, _) =>
        {
            p.Box(0, 0, 0.5, 0.18, 0.18, 0, BoxSkin.All(p.T("wood_dark")), solid: true);
            p.Box(0, 0.02, 0.42, 0.05, 0.26, 0.19, BoxSkin.Front(p.T("metal_dark"), p.T("tv_front")));
        }),
        ["bathtub"] = (Furniture, (p, _) =>
            p.Box(0, 0, 0.34, 0.72, 0.2, 0, new BoxSkin(p.T("porcelain"), p.T("porcelain"), p.T("porcelain"),
                p.T("porcelain"), p.T("water"), p.T("porcelain")), solid: true, uv: 0.3)),
        ["toilet"] = (Furniture, (p, _) =>
        {
            p.Box(0, -0.02, 0.13, 0.16, 0.15, 0, BoxSkin.All(p.T("porcelain")), solid: true);
            p.Box(0, 0.08, 0.15, 0.06, 0.16, 0.12, BoxSkin.All(p.T("porcelain")));
        }),
        ["rug"] = (Furniture, (p, args) =>
        {
            double w = ArgNumber(args, 0, 0.9), d = ArgNumber(args, 1, 0.6);
            p.Box(0, 0, w, d, 0.006, 0, BoxSkin.All(p.T(Arg(args, 2, "carpet_red"))), uv: 0.5);
        }),
        ["bench"] = (Furniture, (p, _) =>
        {
            p.Box(0, 0, 0.6, 0.16, 0.03, 0.15, BoxSkin.All(p.T("wood_light")), solid: true, uv: 0.4);
            p.Box(0, 0.07, 0.6, 0.03, 0.14, 0.2, BoxSkin.All(p.T("wood_light")), uv: 0.4);
            foreach (double lx in new[] { -0.26, 0.26 })
                p.Box(lx, 0, 0.04, 0.14, 0.15, 0, BoxSkin.All(p.T("metal_dark")));
        }),

        // Машина — свой масштаб, чтобы вышли настоящие 4 метра.
        ["car"] = (4.4, (p, _) =>
        {
            int paint = p.T("car_red"), glass = p.T("glass_dark"), tyre = p.T("metal_dark");
            p.Box(0, 0, 0.42, 0.95, 0.17, 0.07, new BoxSkin(paint, paint, paint, paint, paint, tyre), solid: true);
            p.Box(0, 0.05, 0.38, 0.5, 0.14, 0.24, new BoxSkin(glass, glass, glass, glass, paint, paint), solid: true);
            foreach ((double lx, double ly) in Corners(0.2, 0.3))
                p.Box(lx, ly, 0.07, 0.15, 0.15, 0, BoxSkin.All(tyre));
        }),

        // ---------------- Строительное (метры) ----------------

        // roof x y угол длина ширина высота [вынос] [черепица] [фронтон]: двускатная крыша на стенах.
        ["roof"] = (Metric, (p, args) =>
        {
            double length = ArgNumber(args, 0, 6), width = ArgNumber(args, 1, 6), height = ArgNumber(args, 2, 2);
            p.Mesh(0, 0, MeshFactory.Roof(length, width, height, 0, ArgNumber(args, 3, 0.4),
                p.T(Arg(args, 4, "roof_tiles")), p.T(Arg(args, 5, "brick_red")), 1.0), solid: false);
        }),

        // stairs x y угол [ширина] [длина] [высота] [текстура]: ступени, подъём — «лицом» (на север при 0).
        ["stairs"] = (Metric, (p, args) =>
        {
            double width = ArgNumber(args, 0, 1), length = ArgNumber(args, 1, 4), height = ArgNumber(args, 2, 3);
            p.Stairs(width, length, height, p.T(Arg(args, 3, "wood_planks")));
        }),

        // ladder x y угол [высота]: вертикальная лестница; лезть — стоя перед ней (с юга при 0).
        ["ladder"] = (Metric, (p, args) =>
        {
            double height = ArgNumber(args, 0, 3);
            int wood = p.T("wood_dark");
            foreach (double side in new[] { -0.26, 0.26 })
                p.Box(side, 0, 0.07, 0.07, height + 0.6, 0, BoxSkin.All(wood));
            for (double rung = 0.3; rung < height + 0.5; rung += 0.32)
                p.Box(0, 0, 0.5, 0.05, 0.05, rung, BoxSkin.All(wood));
            p.Ladder(0, 0.3, 0.42, 0.38, height + 1.0);

            // Тонкая твёрдая плоскость лестницы: в неё упираешься и лезешь, а не проходишь
            // насквозь (у сеновала за лестницей пусто). Чуть выше площадки — через верх можно шагнуть.
            p.Collider(0, 0, 0.62, 0.06, 0, height + 0.15);
        }),

        // rail x y угол [длина] [высота] [текстура]: перила/ограждение балкона.
        ["rail"] = (Metric, (p, args) =>
        {
            double length = ArgNumber(args, 0, 2), height = ArgNumber(args, 1, 1.0);
            int wood = p.T(Arg(args, 2, "wood_dark"));
            p.Box(0, 0, length, 0.08, 0.08, height - 0.08, BoxSkin.All(wood), uv: 1);
            p.Box(0, 0, length, 0.06, 0.06, height * 0.45, BoxSkin.All(wood), uv: 1);
            for (double post = -length / 2 + 0.04; post <= length / 2; post += Math.Max(0.4, length / Math.Ceiling(length / 1.0)))
                p.Box(post, 0, 0.08, 0.08, height, 0, BoxSkin.All(wood));
            p.Collider(0, 0, length, 0.1, 0, height + 0.1);
        }),

        // crate x y угол [размер] [текстура]
        ["crate"] = (Metric, (p, args) =>
        {
            double size = ArgNumber(args, 0, 0.8);
            p.Box(0, 0, size, size, size, 0, BoxSkin.All(p.T(Arg(args, 1, "crate"))), solid: true);
        }),

        // barrel x y угол [радиус] [высота]
        ["barrel"] = (Metric, (p, args) =>
        {
            double radius = ArgNumber(args, 0, 0.32), height = ArgNumber(args, 1, 0.9);
            p.Mesh(0, 0, MeshFactory.Cylinder(radius, height, 0, p.T("barrel"), 12), solid: false);
            p.Collider(0, 0, radius * 1.8, radius * 1.8, 0, height);
        }),

        // hay x y угол [ширина] [глубина] [высота]: тюк сена.
        ["hay"] = (Metric, (p, args) =>
            p.Box(0, 0, ArgNumber(args, 0, 1.2), ArgNumber(args, 1, 0.8), ArgNumber(args, 2, 0.6), 0,
                BoxSkin.All(p.T("hay")), solid: true, uv: 1)),

        // well x y: каменный колодец с водой.
        ["well"] = (Metric, (p, _) =>
        {
            int stone = p.T("stone_wall");
            foreach (int side in new[] { 0, 1, 2, 3 })
            {
                double angle = side * Math.PI / 2;
                p.Box(Math.Cos(angle) * 0.6, Math.Sin(angle) * 0.6, side % 2 == 0 ? 0.25 : 1.45, side % 2 == 0 ? 1.45 : 0.25,
                    0.8, 0, BoxSkin.All(stone), solid: true, uv: 1);
            }

            p.Box(0, 0, 0.98, 0.98, 0.02, 0.45, BoxSkin.All(p.T("water")), uv: 1);
            foreach (double side in new[] { -0.6, 0.6 })
                p.Box(side, 0, 0.1, 0.1, 1.2, 0.8, BoxSkin.All(p.T("wood_dark")));
            p.Box(0, 0, 1.6, 0.9, 0.08, 2.0, BoxSkin.All(p.T("roof_tiles")), uv: 1);
        }),

        // longtable x y угол [длина]: длинный стол для пира.
        ["longtable"] = (Metric, (p, args) =>
        {
            double length = ArgNumber(args, 0, 3);
            int wood = p.T("wood_planks");
            p.Box(0, 0, 1.0, length, 0.08, 0.72, BoxSkin.All(wood), solid: true, uv: 1);
            foreach ((double lx, double ly) in Corners(0.4, length / 2 - 0.15))
                p.Box(lx, ly, 0.1, 0.1, 0.72, 0, BoxSkin.All(p.T("wood_dark")));
        }),

        // banner x y угол [высота] [текстура]: знамя на стене (лицом от стены).
        ["banner"] = (Metric, (p, args) =>
        {
            double height = ArgNumber(args, 0, 2.2);
            p.Box(0, 0, 1.0, 0.04, height, 0.4, BoxSkin.Front(p.T("wood_dark"), p.T(Arg(args, 1, "banner_red"))));
        }),

        // platform x y угол [ширина] [глубина] [высота] [текстура]: помост/ступень, на которую можно встать.
        ["platform"] = (Metric, (p, args) =>
            p.Box(0, 0, ArgNumber(args, 0, 2), ArgNumber(args, 1, 2), ArgNumber(args, 2, 0.4), 0,
                BoxSkin.All(p.T(Arg(args, 3, "wood_planks"))), solid: true, uv: 1)),

        // mound x y 0 [размер] [высота] [текстура]: травяной холм-рельеф (декорация, проходим).
        ["mound"] = (Metric, (p, args) =>
        {
            double size = ArgNumber(args, 0, 6), height = ArgNumber(args, 1, 0.8);
            double sigma = size * size / 9.0;
            p.Mesh(0, 0, MeshFactory.Heightfield(size, 12, (hx, hy) => height * Math.Exp(-(hx * hx + hy * hy) / sigma),
                p.T(Arg(args, 2, "grass")), uvRepeat: size / 2), solid: false);
        }),
    };

    private static IEnumerable<(double X, double Y)> Corners(double x, double y) =>
        new[] { (-x, -y), (x, -y), (-x, y), (x, y) };

    private static string Arg(string[] args, int index, string fallback) =>
        index < args.Length ? args[index] : fallback;

    private static double ArgNumber(string[] args, int index, double fallback) =>
        index < args.Length && double.TryParse(args[index], NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : fallback;

    /// <summary>Ставит части предмета с общим поворотом и масштабом вокруг его центра.</summary>
    private sealed class Placer(Level level, double x, double y, double z, double yaw, double scale)
    {
        private readonly double _cos = Math.Cos(yaw);
        private readonly double _sin = Math.Sin(yaw);

        public int T(string texture) => level.TextureIndex(texture);

        private Vec3 World(double localX, double localY, double localZ) =>
            new(x + (localX * _cos - localY * _sin) * scale, y + (localX * _sin + localY * _cos) * scale, z + localZ * scale);

        public void Box(double localX, double localY, double width, double depth, double height, double zBottom,
            BoxSkin skin, bool solid = false, double uv = 0)
        {
            Mesh(localX, localY, MeshFactory.Box(width, depth, height, zBottom, skin, uv), solid: false);
            if (solid)
                Collider(localX, localY, width, depth, zBottom, zBottom + height);
        }

        public void Mesh(double localX, double localY, Mesh3D mesh, bool solid)
        {
            foreach (Quad3D quad in mesh.Quads)
                level.Faces.Add(Face.FromQuad(quad, (qx, qy, qz) => World(localX + qx, localY + qy, qz), quad.TextureId));

            if (solid)
                Collider(localX + (mesh.MinX + mesh.MaxX) / 2, localY + (mesh.MinY + mesh.MaxY) / 2,
                    mesh.MaxX - mesh.MinX, mesh.MaxY - mesh.MinY, mesh.MinZ, mesh.MaxZ);
        }

        /// <summary>Твёрдое тело в локальных координатах предмета.</summary>
        public void Collider(double localX, double localY, double width, double depth, double zBottom, double zTop)
        {
            Vec3 center = World(localX, localY, zBottom);
            level.Solids.Add(Solid.Box(center.X, center.Y, width * 0.5 * scale, depth * 0.5 * scale,
                center.Z, z + zTop * scale, yaw));
        }

        /// <summary>Объём, в котором игрок лезет по лестнице.</summary>
        public void Ladder(double localX, double localY, double width, double depth, double height)
        {
            Vec3 center = World(localX, localY, 0);
            level.Ladders.Add(new Ladder(Solid.Box(center.X, center.Y, width * 0.5, depth * 0.5, z, z + height, yaw)));
        }

        /// <summary>Лестница из ступеней высотой не больше 0.2 м: подъём к локальному −y.</summary>
        public void Stairs(double width, double length, double height, int texture)
        {
            int steps = Math.Max(1, (int)Math.Ceiling(height / 0.2));
            double depth = length / steps, rise = height / steps;
            for (int step = 0; step < steps; step++)
            {
                double near = length / 2 - step * depth;
                double top = (step + 1) * rise;
                Box(0, near - depth / 2, width, depth, top, 0, BoxSkin.All(texture), solid: true, uv: 1);
            }
        }
    }
}

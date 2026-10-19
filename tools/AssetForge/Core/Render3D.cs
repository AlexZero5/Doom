namespace AssetForge.Core;

/// <summary>
///     Материал поверхности. <see cref="Metallic" /> — доля «металла»: металл не просто освещается,
///     а отражает окружение (светлое небо сверху, яркая полоса горизонта, тёмный пол снизу) —
///     отсюда характерные полосы и блики стали вместо ровного «пластика».
/// </summary>
internal sealed record Material(
    Rgb Color,
    double Specular = 0.15,
    double Shininess = 12,
    double Emissive = 0,
    Func<Vec3, double>? Pattern = null,
    double Metallic = 0)
{
    /// <summary>Металл: отражения окружения, резкий блик.</summary>
    public static Material Metal(Rgb color, double metallic = 0.75) => new(color, 0.7, 40, Metallic: metallic);

    /// <summary>Краска по металлу: в основном матовая, немного отражает.</summary>
    public static Material Painted(Rgb color) => new(color, 0.35, 18, Metallic: 0.25);

    public static Material Matte(Rgb color) => new(color, 0.05, 6);

    public static Material Glow(Rgb color) => new(color, 0, 1, 1.0);
}

/// <summary>Сцена: набор фигур с материалами.</summary>
internal sealed class Scene
{
    private readonly List<(Sdf Shape, Material Material)> _items = new();

    public void Add(Sdf shape, Material material) => _items.Add((shape, material));

    public int Count => _items.Count;

    public double Distance(Vec3 p)
    {
        double d = double.MaxValue;
        foreach ((Sdf shape, _) in _items)
            d = Math.Min(d, shape.Distance(p));
        return d;
    }

    public (double Distance, int Index) Nearest(Vec3 p)
    {
        double best = double.MaxValue;
        int index = -1;
        for (int i = 0; i < _items.Count; i++)
        {
            double d = _items[i].Shape.Distance(p);
            if (d < best)
            {
                best = d;
                index = i;
            }
        }

        return (best, index);
    }

    public Material MaterialAt(int index) => _items[index].Material;
}

/// <summary>
///     Камера: <c>Focal</c> — фокус в пикселях, (<c>CenterX</c>, <c>CenterY</c>) — центр экрана,
///     <c>OffsetX/OffsetY</c> — где холст лежит на этом экране.
/// </summary>
internal sealed class Camera
{
    public Vec3 Position { get; init; } = new(0, 0, 0);
    public Vec3 Forward { get; init; } = new(0, 0, 1);
    public Vec3 Up { get; init; } = new(0, 1, 0);
    public double Focal { get; init; } = 160;
    public double CenterX { get; init; } = 160;
    public double CenterY { get; init; } = 100;
    public int OffsetX { get; init; }
    public int OffsetY { get; init; }

    private (Vec3 Forward, Vec3 Right, Vec3 Up) Basis()
    {
        Vec3 forward = Forward.Normalized;
        Vec3 right = forward.Cross(Up).Normalized * -1;
        Vec3 up = right.Cross(forward).Normalized * -1;
        return (forward, right, up);
    }

    public Vec3 RayDirection(double screenX, double screenY)
    {
        (Vec3 forward, Vec3 right, Vec3 up) = Basis();
        return (forward + right * ((screenX - CenterX) / Focal) + up * (-(screenY - CenterY) / Focal)).Normalized;
    }

    /// <summary>Точка мира → координаты на холсте.</summary>
    public (double X, double Y) Project(Vec3 point)
    {
        (Vec3 forward, Vec3 right, Vec3 up) = Basis();
        Vec3 d = point - Position;
        double z = d.Dot(forward);
        return (CenterX + d.Dot(right) / z * Focal - OffsetX, CenterY - d.Dot(up) / z * Focal - OffsetY);
    }
}

internal static class Renderer3D
{
    private static readonly Vec3 KeyLight = new Vec3(-0.45, 0.8, -0.55).Normalized;
    private static readonly Vec3 FillLight = new Vec3(0.75, 0.15, -0.45).Normalized;

    /// <summary>
    ///     Рисует сцену в холст: сглаживание 2×2, освещение и отражения, затенение углов, затем
    ///     «пиксельная прорисовка»: тёмные линии на перепадах глубины и материала и светлые/тёмные
    ///     кромки на рёбрах (где резко меняется нормаль).
    /// </summary>
    public static Canvas Render(Scene scene, Camera camera, int width, int height, double ambient = 0.3)
    {
        var canvas = new Canvas(width, height);
        var depth = new double[width * height];
        var material = new int[width * height];
        var normals = new Vec3[width * height];

        Parallel.For(0, height, y =>
        {
            for (int x = 0; x < width; x++)
            {
                double r = 0, g = 0, b = 0, depthSum = 0;
                Vec3 normalSum = default;
                int hits = 0, materialIndex = -1;

                for (int sy = 0; sy < 2; sy++)
                for (int sx = 0; sx < 2; sx++)
                {
                    double screenX = camera.OffsetX + x + 0.25 + sx * 0.5;
                    double screenY = camera.OffsetY + y + 0.25 + sy * 0.5;
                    Vec3 direction = camera.RayDirection(screenX, screenY);

                    if (!March(scene, camera.Position, direction, out double t, out int index))
                        continue;

                    Vec3 point = camera.Position + direction * t;
                    Vec3 n = Normal(scene, point);
                    Rgb color = Shade(scene, point, n, direction, scene.MaterialAt(index), ambient);
                    r += color.R;
                    g += color.G;
                    b += color.B;
                    depthSum += t;
                    normalSum += n;
                    hits++;
                    materialIndex = index;
                }

                int i = y * width + x;
                if (hits >= 2)
                {
                    canvas[x, y] = new Rgb((int)(r / hits), (int)(g / hits), (int)(b / hits)).Argb();
                    depth[i] = depthSum / hits;
                    material[i] = materialIndex;
                    normals[i] = normalSum.Normalized;
                }
                else
                {
                    depth[i] = double.MaxValue;
                    material[i] = -1;
                }
            }
        });

        var result = new Canvas(width, height);
        Array.Copy(canvas.Pixels, result.Pixels, canvas.Pixels.Length);

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int i = y * width + x;
            if (material[i] < 0 || scene.MaterialAt(material[i]).Emissive >= 0.5) continue;

            bool line = false;
            double edgeLight = 0;
            foreach ((int dx, int dy) in new[] { (1, 0), (0, 1), (-1, 0), (0, -1) })
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
                int j = ny * width + nx;
                if (material[j] < 0) continue;

                bool fartherNeighbor = depth[j] > depth[i] * 1.05 + 0.01;
                bool otherMaterial = material[j] != material[i] && depth[j] > depth[i];
                if (fartherNeighbor || otherMaterial) line = true;

                // Ребро: нормаль резко меняется — кромка, повёрнутая к свету, светлеет.
                if (normals[i].Dot(normals[j]) < 0.82)
                    edgeLight += normals[i].Dot(KeyLight) - normals[j].Dot(KeyLight);
            }

            Rgb color = canvas.ColorAt(x, y);
            if (line)
                color = color.Scale(0.5);
            else if (edgeLight > 0.15)
                color = color.Scale(1.3).Add(18);
            else if (edgeLight < -0.15)
                color = color.Scale(0.72);

            result[x, y] = color.Argb();
        }

        return result;
    }

    private static bool March(Scene scene, Vec3 origin, Vec3 direction, out double t, out int index)
    {
        t = 0.02;
        index = -1;
        for (int step = 0; step < 220 && t < 30; step++)
        {
            (double d, int nearest) = scene.Nearest(origin + direction * t);
            if (d < 0.0003 * t + 0.00015)
            {
                index = nearest;
                return true;
            }

            t += d * 0.9;
        }

        return false;
    }

    private static Vec3 Normal(Scene scene, Vec3 p)
    {
        const double e = 0.0006;
        var k1 = new Vec3(1, -1, -1);
        var k2 = new Vec3(-1, -1, 1);
        var k3 = new Vec3(-1, 1, -1);
        var k4 = new Vec3(1, 1, 1);
        return (k1 * scene.Distance(p + k1 * e) + k2 * scene.Distance(p + k2 * e)
                + k3 * scene.Distance(p + k3 * e) + k4 * scene.Distance(p + k4 * e)).Normalized;
    }

    /// <summary>
    ///     Окружение для отражений металла: светлое небо, резкая яркая полоса у горизонта,
    ///     тёмный тёплый «пол». Именно резкий горизонт даёт стали узнаваемые полосы.
    /// </summary>
    private static double Environment(Vec3 r)
    {
        if (r.Y > 0.12) return 0.85 + 0.35 * Math.Min(1, (r.Y - 0.12) * 2) * (r.X < 0 ? 1.2 : 0.8);
        if (r.Y > -0.02) return 1.45;
        return 0.18 + 0.12 * Math.Max(0, 1 + r.Y * 2);
    }

    private static Rgb Shade(Scene scene, Vec3 p, Vec3 n, Vec3 direction, Material material, double ambient)
    {
        Rgb baseColor = material.Pattern is null ? material.Color : material.Color.Scale(material.Pattern(p));
        if (material.Emissive >= 1)
            return baseColor;

        // Затенение углов: насколько близко соседние поверхности вдоль нормали.
        double occlusion = 0, weight = 1;
        for (int i = 1; i <= 4; i++)
        {
            double h = 0.004 * i;
            occlusion += (h - scene.Distance(p + n * h)) * weight;
            weight *= 0.6;
        }

        double ao = Math.Clamp(1 - occlusion * 30, 0.3, 1);
        double key = Math.Max(0, n.Dot(KeyLight));
        double fill = Math.Max(0, n.Dot(FillLight)) * 0.3;
        Vec3 half = (KeyLight - direction).Normalized;
        double specular = Math.Pow(Math.Max(0, n.Dot(half)), material.Shininess) * material.Specular;

        double diffuseLight = ambient * ao + key * 0.85 + fill;
        Rgb diffuse = baseColor.Scale(diffuseLight);

        if (material.Metallic > 0)
        {
            Vec3 reflected = direction - n * (2 * direction.Dot(n));
            double env = Environment(reflected) * (0.55 + 0.45 * ao);
            Rgb metal = baseColor.Scale(0.25 + env * 1.05);
            diffuse = diffuse.Lerp(metal, material.Metallic);
        }

        Rgb color = diffuse.Add((int)(specular * 255 * ao));
        return material.Emissive > 0 ? color.Lerp(baseColor, material.Emissive) : color;
    }
}

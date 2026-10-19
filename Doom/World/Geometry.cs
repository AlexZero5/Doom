namespace Doom.World;

/// <summary>Точка или вектор в мире: x — на восток, y — на юг, z — вверх (метры).</summary>
internal readonly record struct Vec3(double X, double Y, double Z)
{
    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Vec3 operator *(Vec3 a, double k) => new(a.X * k, a.Y * k, a.Z * k);

    public static Vec3 operator -(Vec3 a) => new(-a.X, -a.Y, -a.Z);

    public double Dot(Vec3 other) => X * other.X + Y * other.Y + Z * other.Z;

    public Vec3 Cross(Vec3 other) =>
        new(Y * other.Z - Z * other.Y, Z * other.X - X * other.Z, X * other.Y - Y * other.X);

    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

    public Vec3 Normalized
    {
        get
        {
            double length = Length;
            return length < 1e-12 ? new Vec3(0, 0, 1) : this * (1.0 / length);
        }
    }

    /// <summary>Направление взгляда по курсу и наклону (0 — на восток, π/2 — на юг; наклон вверх — плюс).</summary>
    public static Vec3 FromAngles(double yaw, double pitch) =>
        new(Math.Cos(pitch) * Math.Cos(yaw), Math.Cos(pitch) * Math.Sin(yaw), Math.Sin(pitch));
}

[Flags]
internal enum FaceFlags
{
    None = 0,

    /// <summary>Видна с обеих сторон (листва, решётка, спрайт).</summary>
    DoubleSided = 1,

    /// <summary>Прозрачные тексели пропускаются (решётка ворот, спрайты).</summary>
    AlphaTest = 2,

    /// <summary>Светится сама: без затенения по дальности (вспышки, плазма).</summary>
    FullBright = 4,

    /// <summary>UV не повторяются, а упираются в край (спрайты).</summary>
    Clamp = 8
}

/// <summary>
///     Грань мира — выпуклый треугольник или четырёхугольник с текстурными координатами вершин.
///     Нормаль задаёт лицевую сторону: с изнанки (если грань не двусторонняя) она не рисуется.
/// </summary>
internal struct Face
{
    public Vec3 P0, P1, P2, P3;
    public double U0, V0, U1, V1, U2, V2, U3, V3;

    /// <summary>Число вершин: 3 или 4.</summary>
    public int Count;

    public int Texture;

    /// <summary>Освещённость грани от солнца (0..1): верх светлее, боковины и низ темнее.</summary>
    public double Light;

    public FaceFlags Flags;

    public Vec3 Normal;

    /// <summary>Уравнение плоскости: Normal · p = Plane.</summary>
    public double Plane;

    /// <summary>Сколько повторов текстуры приходится на метр поверхности (для выбора мип-уровня).</summary>
    public double RepeatsPerMeter;

    public readonly Vec3 Vertex(int index) => index switch { 0 => P0, 1 => P1, 2 => P2, _ => P3 };

    public readonly (double U, double V) Uv(int index) => index switch
    {
        0 => (U0, V0),
        1 => (U1, V1),
        2 => (U2, V2),
        _ => (U3, V3)
    };

    /// <summary>
    ///     Четырёхугольник (или треугольник при <paramref name="count" /> = 3) с нормалью,
    ///     развёрнутой в сторону <paramref name="facing" /> (если задана).
    /// </summary>
    public static Face Create(Vec3 p0, Vec3 p1, Vec3 p2, Vec3 p3,
        (double U, double V) t0, (double U, double V) t1, (double U, double V) t2, (double U, double V) t3,
        int texture, Vec3? facing = null, FaceFlags flags = FaceFlags.None, int count = 4)
    {
        var face = new Face
        {
            P0 = p0, P1 = p1, P2 = p2, P3 = count == 3 ? p2 : p3,
            U0 = t0.U, V0 = t0.V, U1 = t1.U, V1 = t1.V, U2 = t2.U, V2 = t2.V,
            U3 = count == 3 ? t2.U : t3.U, V3 = count == 3 ? t2.V : t3.V,
            Count = count,
            Texture = texture,
            Flags = flags
        };

        Vec3 normal = (p1 - p0).Cross(p2 - p0).Normalized;
        if (facing is { } wanted && normal.Dot(wanted) < 0)
            normal = -normal;

        face.Finish(normal);
        return face;
    }

    /// <summary>Грань из квада меша (<see cref="Quad3D" />) — нормаль по его соглашению e03 × e01.</summary>
    public static Face FromQuad(in Quad3D quad, Func<double, double, double, Vec3> transform, int texture)
    {
        Vec3 p0 = transform(quad.X0, quad.Y0, quad.Z0);
        Vec3 p1 = transform(quad.X1, quad.Y1, quad.Z1);
        Vec3 p2 = transform(quad.X2, quad.Y2, quad.Z2);
        Vec3 p3 = transform(quad.X3, quad.Y3, quad.Z3);

        // Треугольник квада — вершины [0], [1], [3] (у него [2] = [3]).
        bool triangle = quad.IsTriangle;
        var face = new Face
        {
            P0 = p0, P1 = p1, P2 = triangle ? p3 : p2, P3 = p3,
            U0 = quad.U0, V0 = quad.V0,
            U1 = quad.U1, V1 = quad.V0,
            U2 = triangle ? quad.U0 : quad.U1, V2 = quad.V1,
            U3 = quad.U0, V3 = quad.V1,
            Count = triangle ? 3 : 4,
            Texture = texture
        };

        Vec3 normal = (p3 - p0).Cross(p1 - p0).Normalized;
        face.Finish(normal);
        return face;
    }

    private void Finish(Vec3 normal)
    {
        Normal = normal;
        Plane = normal.Dot(P0);

        // Солнце сверху-сбоку: верх светлее, стены по сторонам света немного разные, низ темнее.
        double sun = Math.Max(0, normal.Dot(SunDirection));
        Light = Math.Clamp(0.58 + 0.42 * sun - (normal.Z < -0.5 ? 0.12 : 0), 0.3, 1.0);

        double repeats = 0;
        for (int i = 0; i < Count; i++)
        {
            int j = (i + 1) % Count;
            double length = (Vertex(j) - Vertex(i)).Length;
            if (length < 1e-6)
                continue;

            (double ua, double va) = Uv(i);
            (double ub, double vb) = Uv(j);
            repeats = Math.Max(repeats, Math.Sqrt((ub - ua) * (ub - ua) + (vb - va) * (vb - va)) / length);
        }

        RepeatsPerMeter = repeats;
    }

    private static readonly Vec3 SunDirection = new Vec3(-0.45, -0.3, 0.84).Normalized;

    /// <summary>Попадание луча в грань (с любой стороны): дистанция вдоль луча.</summary>
    public readonly bool Intersect(Vec3 origin, Vec3 direction, out double distance)
    {
        distance = 0;
        double denominator = Normal.Dot(direction);
        if (Math.Abs(denominator) < 1e-9)
            return false;

        distance = (Plane - Normal.Dot(origin)) / denominator;
        if (distance < 0)
            return false;

        Vec3 point = origin + direction * distance;

        // Точка внутри выпуклого многоугольника: по одну сторону от всех рёбер.
        for (int i = 0; i < Count; i++)
        {
            Vec3 a = Vertex(i), b = Vertex((i + 1) % Count);
            if ((b - a).Cross(point - a).Dot(Normal) < -1e-7)
                return false;
        }

        return true;
    }
}

/// <summary>
///     Твёрдое тело для столкновений: прямоугольник в плане (повёрнутый на <c>Yaw</c>) между
///     высотами <c>ZMin</c>..<c>ZMax</c>. Стены, перекрытия, мебель, двери — всё такие тела.
/// </summary>
internal struct Solid
{
    public double X, Y, HalfX, HalfY, Cos, Sin, ZMin, ZMax;

    public static Solid Box(double x, double y, double halfX, double halfY, double zMin, double zMax, double yaw = 0) =>
        new()
        {
            X = x, Y = y, HalfX = halfX, HalfY = halfY, ZMin = zMin, ZMax = zMax,
            Cos = Math.Cos(yaw), Sin = Math.Sin(yaw)
        };

    /// <summary>Описанный прямоугольник в плане (для сетки поиска).</summary>
    public readonly (double MinX, double MinY, double MaxX, double MaxY) Bounds
    {
        get
        {
            double ex = Math.Abs(HalfX * Cos) + Math.Abs(HalfY * Sin);
            double ey = Math.Abs(HalfX * Sin) + Math.Abs(HalfY * Cos);
            return (X - ex, Y - ey, X + ex, Y + ey);
        }
    }

    /// <summary>Пересекается ли с кругом в плане.</summary>
    public readonly bool OverlapsCircle(double x, double y, double radius)
    {
        double dx = x - X, dy = y - Y;
        double localX = dx * Cos + dy * Sin;
        double localY = -dx * Sin + dy * Cos;
        double nearestX = Math.Clamp(localX, -HalfX, HalfX);
        double nearestY = Math.Clamp(localY, -HalfY, HalfY);
        double ox = localX - nearestX, oy = localY - nearestY;
        return ox * ox + oy * oy < radius * radius;
    }

    public readonly bool Contains(Vec3 point) =>
        point.Z > ZMin && point.Z < ZMax && OverlapsCircle(point.X, point.Y, 1e-6);
}

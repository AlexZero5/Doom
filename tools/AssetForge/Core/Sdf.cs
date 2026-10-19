namespace AssetForge.Core;

/// <summary>Трёхмерный вектор для моделирования и трассировки.</summary>
internal readonly record struct Vec3(double X, double Y, double Z)
{
    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator -(Vec3 a) => new(-a.X, -a.Y, -a.Z);
    public static Vec3 operator *(Vec3 a, double k) => new(a.X * k, a.Y * k, a.Z * k);
    public static Vec3 operator *(double k, Vec3 a) => a * k;

    public double Dot(Vec3 b) => X * b.X + Y * b.Y + Z * b.Z;
    public Vec3 Cross(Vec3 b) => new(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);
    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
    public Vec3 Normalized => this * (1.0 / Math.Max(Length, 1e-12));
    public Vec3 Abs() => new(Math.Abs(X), Math.Abs(Y), Math.Abs(Z));
    public Vec3 Max(double v) => new(Math.Max(X, v), Math.Max(Y, v), Math.Max(Z, v));

    /// <summary>Поворот вокруг оси (через центр) на угол в радианах (формула Родрига).</summary>
    public Vec3 RotateAround(Vec3 center, Vec3 axis, double angle)
    {
        Vec3 k = axis.Normalized;
        Vec3 v = this - center;
        double cos = Math.Cos(angle), sin = Math.Sin(angle);
        Vec3 rotated = v * cos + k.Cross(v) * sin + k * (k.Dot(v) * (1 - cos));
        return center + rotated;
    }
}

/// <summary>Поле расстояний со знаком: отрицательно внутри фигуры.</summary>
internal abstract class Sdf
{
    public abstract double Distance(Vec3 p);

    public static double SmoothMin(double a, double b, double k)
    {
        if (k <= 0) return Math.Min(a, b);
        double h = Math.Clamp(0.5 + 0.5 * (b - a) / k, 0, 1);
        return b + (a - b) * h - k * h * (1 - h);
    }
}

internal sealed class Sphere(Vec3 center, double radius) : Sdf
{
    public override double Distance(Vec3 p) => (p - center).Length - radius;
}

/// <summary>Капсула с разными радиусами на концах (палец, конический ствол).</summary>
internal sealed class RoundCone(Vec3 a, Vec3 b, double r1, double r2) : Sdf
{
    private readonly Vec3 _ba = b - a;
    private readonly double _l2 = (b - a).Dot(b - a);

    public override double Distance(Vec3 p)
    {
        double rr = r1 - r2;
        double a2 = _l2 - rr * rr;
        double il2 = 1.0 / _l2;
        Vec3 pa = p - a;
        double y = pa.Dot(_ba);
        double z = y - _l2;
        Vec3 xv = pa * _l2 - _ba * y;
        double x2 = xv.Dot(xv);
        double y2 = y * y * _l2;
        double z2 = z * z * _l2;
        double k = Math.Sign(rr) * rr * rr * x2;

        if (Math.Sign(z) * a2 * z2 > k) return Math.Sqrt(x2 + z2) * il2 - r2;
        if (Math.Sign(y) * a2 * y2 < k) return Math.Sqrt(x2 + y2) * il2 - r1;
        return (Math.Sqrt(x2 * a2 * il2) + y * rr) * il2 - r1;
    }
}

/// <summary>Цилиндр между точками a и b (торцы плоские).</summary>
internal sealed class Cylinder(Vec3 a, Vec3 b, double radius) : Sdf
{
    public override double Distance(Vec3 p)
    {
        Vec3 ba = b - a, pa = p - a;
        double baba = ba.Dot(ba), paba = pa.Dot(ba);
        double x = (pa * baba - ba * paba).Length - radius * baba;
        double y = Math.Abs(paba - baba * 0.5) - baba * 0.5;
        double x2 = x * x, y2 = y * y * baba;
        double d = Math.Max(x, y) < 0 ? -Math.Min(x2, y2) : (x > 0 ? x2 : 0) + (y > 0 ? y2 : 0);
        return Math.Sign(d) * Math.Sqrt(Math.Abs(d)) / baba;
    }
}

/// <summary>Усечённый конус между a (радиус ra) и b (радиус rb).</summary>
internal sealed class Cone(Vec3 a, Vec3 b, double ra, double rb) : Sdf
{
    public override double Distance(Vec3 p)
    {
        double rba = rb - ra;
        double baba = (b - a).Dot(b - a);
        double papa = (p - a).Dot(p - a);
        double paba = (p - a).Dot(b - a) / baba;
        double x = Math.Sqrt(Math.Max(0, papa - paba * paba * baba));
        double cax = Math.Max(0, x - (paba < 0.5 ? ra : rb));
        double cay = Math.Abs(paba - 0.5) - 0.5;
        double k = rba * rba + baba;
        double f = Math.Clamp((rba * (x - ra) + paba * baba) / k, 0, 1);
        double cbx = x - ra - f * rba, cby = paba - f;
        double s = cbx < 0 && cay < 0 ? -1 : 1;
        return s * Math.Sqrt(Math.Min(cax * cax + cay * cay * baba, cbx * cbx + cby * cby * baba));
    }
}

/// <summary>Скруглённый бокс с произвольными осями (u, v, w — единичные, взаимно перпендикулярные).</summary>
internal sealed class Box : Sdf
{
    private readonly Vec3 _center, _u, _v, _w, _half;
    private readonly double _round;

    public Box(Vec3 center, Vec3 half, double round = 0, Vec3? u = null, Vec3? v = null)
    {
        _center = center;
        _half = half;
        _round = round;
        _u = (u ?? new Vec3(1, 0, 0)).Normalized;
        Vec3 vv = (v ?? new Vec3(0, 1, 0));
        vv = (vv - _u * vv.Dot(_u)).Normalized;
        _v = vv;
        _w = _u.Cross(_v);
    }

    public override double Distance(Vec3 p)
    {
        Vec3 d = p - _center;
        Vec3 local = new Vec3(d.Dot(_u), d.Dot(_v), d.Dot(_w)).Abs() - _half + new Vec3(_round, _round, _round);
        return local.Max(0).Length + Math.Min(Math.Max(local.X, Math.Max(local.Y, local.Z)), 0) - _round;
    }
}

/// <summary>Тор вокруг оси.</summary>
internal sealed class Torus(Vec3 center, Vec3 axis, double major, double minor) : Sdf
{
    private readonly Vec3 _axis = axis.Normalized;

    public override double Distance(Vec3 p)
    {
        Vec3 q = p - center;
        double h = q.Dot(_axis);
        double radial = (q - _axis * h).Length - major;
        return Math.Sqrt(radial * radial + h * h) - minor;
    }
}

/// <summary>Плавное объединение (кисть из пальцев, сварные швы).</summary>
internal sealed class Blend(double k, params Sdf[] parts) : Sdf
{
    public override double Distance(Vec3 p)
    {
        double d = parts[0].Distance(p);
        for (int i = 1; i < parts.Length; i++)
            d = SmoothMin(d, parts[i].Distance(p), k);
        return d;
    }
}

/// <summary>Эллипсоид (приближённое поле, для голов и крон).</summary>
internal sealed class Ellipsoid(Vec3 center, Vec3 radii) : Sdf
{
    public override double Distance(Vec3 p)
    {
        Vec3 d = p - center;
        Vec3 q = new(d.X / radii.X, d.Y / radii.Y, d.Z / radii.Z);
        double k0 = q.Length;
        Vec3 q2 = new(d.X / (radii.X * radii.X), d.Y / (radii.Y * radii.Y), d.Z / (radii.Z * radii.Z));
        double k1 = q2.Length;
        return k1 < 1e-12 ? -Math.Min(radii.X, Math.Min(radii.Y, radii.Z)) : k0 * (k0 - 1) / k1;
    }
}

/// <summary>Пересечение фигур (причёска = верх эллипсоида).</summary>
internal sealed class Intersect(params Sdf[] parts) : Sdf
{
    public override double Distance(Vec3 p)
    {
        double d = double.MinValue;
        foreach (Sdf part in parts)
            d = Math.Max(d, part.Distance(p));
        return d;
    }
}

/// <summary>Поворот фигуры вокруг оси через центр (поворот головы).</summary>
internal sealed class Rotated(Sdf shape, Vec3 center, Vec3 axis, double angle) : Sdf
{
    public override double Distance(Vec3 p) => shape.Distance(p.RotateAround(center, axis, -angle));
}

/// <summary>Полупространство: всё «под» плоскостью с нормалью n через точку (для клиньев и срезов).</summary>
internal sealed class HalfSpace(Vec3 point, Vec3 normal) : Sdf
{
    private readonly Vec3 _normal = normal.Normalized;

    public override double Distance(Vec3 p) => (p - point).Dot(_normal);
}

/// <summary>Фигуры-заготовки для «бронированного» дизайна.</summary>
internal static class Shapes
{
    /// <summary>
    ///     Бокс со скошенными рёбрами вдоль оси w (u × v): пересечение бокса и бокса, повёрнутого
    ///     на 45° вокруг той же оси. <paramref name="chamfer" /> — ширина фаски.
    /// </summary>
    public static Sdf ChamferBox(Vec3 center, Vec3 half, double chamfer, Vec3? u = null, Vec3? v = null)
    {
        Vec3 uu = (u ?? new Vec3(1, 0, 0)).Normalized;
        Vec3 vv = (v ?? new Vec3(0, 1, 0));
        vv = (vv - uu * vv.Dot(uu)).Normalized;
        double diagonal = (half.X + half.Y - chamfer) / Math.Sqrt(2);
        Vec3 du = (uu + vv).Normalized, dv = (vv - uu).Normalized;
        return new Intersect(
            new Box(center, half, 0.001, uu, vv),
            new Box(center, new Vec3(diagonal, diagonal, half.Z), 0.001, du, dv));
    }

    /// <summary>Повтор фигуры вдоль направления: n копий с шагом (рёбра радиатора, прорези).</summary>
    public static Sdf Repeat(Func<int, Sdf> make, int count)
    {
        var parts = new Sdf[count];
        for (int i = 0; i < count; i++)
            parts[i] = make(i);
        return new Union(parts);
    }
}

/// <summary>Обычное объединение (минимум расстояний).</summary>
internal sealed class Union(params Sdf[] parts) : Sdf
{
    public override double Distance(Vec3 p)
    {
        double d = double.MaxValue;
        foreach (Sdf part in parts)
            d = Math.Min(d, part.Distance(p));
        return d;
    }
}

/// <summary>Вычитание: A без B (отверстия, пазы).</summary>
internal sealed class Cut(Sdf a, params Sdf[] holes) : Sdf
{
    public override double Distance(Vec3 p)
    {
        double d = a.Distance(p);
        foreach (Sdf hole in holes)
            d = Math.Max(d, -hole.Distance(p));
        return d;
    }
}

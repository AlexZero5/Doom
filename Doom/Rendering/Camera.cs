using Doom.World;

namespace Doom.Rendering;

/// <summary>
///     Камера-обскура: глаз, курс, наклон (до ±89° — можно смотреть прямо в пол и в небо) и
///     фокусное расстояние в пикселях внутреннего кадра. Проекция настоящая перспективная —
///     без «сдвига горизонта» и без искажений у края кадра и вблизи предметов.
/// </summary>
internal readonly struct Camera
{
    public Camera(Vec3 eye, double yaw, double pitch, double fieldOfView, int width, int height)
    {
        Position = eye;
        Width = width;
        Height = height;
        CenterX = width * 0.5;
        CenterY = height * 0.5;
        Focal = width * 0.5 / Math.Tan(fieldOfView * 0.5);

        double cp = Math.Cos(pitch), sp = Math.Sin(pitch), cy = Math.Cos(yaw), sy = Math.Sin(yaw);
        Forward = new Vec3(cp * cy, cp * sy, sp);
        Right = new Vec3(-sy, cy, 0);
        Up = new Vec3(-sp * cy, -sp * sy, cp);
    }

    public Vec3 Position { get; }

    public Vec3 Forward { get; }

    /// <summary>Вправо по экрану (всегда горизонтально).</summary>
    public Vec3 Right { get; }

    /// <summary>Вверх по экрану.</summary>
    public Vec3 Up { get; }

    public double Focal { get; }

    public int Width { get; }

    public int Height { get; }

    public double CenterX { get; }

    public double CenterY { get; }

    /// <summary>Точка мира → координаты камеры (x — вправо, y — вверх, z — вперёд).</summary>
    public (double X, double Y, double Z) ToView(Vec3 point)
    {
        Vec3 d = point - Position;
        return (d.Dot(Right), d.Dot(Up), d.Dot(Forward));
    }

    /// <summary>Луч через центр пикселя внутреннего кадра (не нормирован).</summary>
    public Vec3 RayThrough(double x, double y) =>
        Forward * Focal + Right * (x - CenterX) - Up * (y - CenterY);
}

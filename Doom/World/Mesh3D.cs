namespace Doom.World;

/// <summary>Форма «следа» объекта на полу — для столкновений игрока, снарядов и пуль.</summary>
internal enum Footprint
{
    /// <summary>Прямоугольник по габаритам меша (поворачивается вместе с объектом).</summary>
    Box,

    /// <summary>Круг вокруг центра (башни, диски).</summary>
    Circle
}

/// <summary>
///     Меш 3D-объекта: набор ориентированных квадов. Создаётся фабрикой (Box, Cylinder,
///     Ramp, Heightfield), инвариантен после создания — экземпляры лишь вращают/двигают его.
///     Габариты считаются один раз из вершин и нужны для отсечения и столкновений.
/// </summary>
internal sealed class Mesh3D
{
    public Mesh3D(Quad3D[] quads, Footprint footprint = Footprint.Box)
    {
        Quads = quads;
        Footprint = footprint;

        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
        double radiusSquared = 0;

        foreach (Quad3D quad in quads)
        {
            for (int corner = 0; corner < 4; corner++)
            {
                (double x, double y, double z) = quad.Vertex(corner);
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                minZ = Math.Min(minZ, z);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
                maxZ = Math.Max(maxZ, z);
                radiusSquared = Math.Max(radiusSquared, x * x + y * y);
            }
        }

        if (quads.Length == 0)
            minX = minY = minZ = maxX = maxY = maxZ = 0;

        MinX = minX;
        MinY = minY;
        MinZ = minZ;
        MaxX = maxX;
        MaxY = maxY;
        MaxZ = maxZ;
        HorizontalRadius = Math.Sqrt(radiusSquared);
    }

    public Quad3D[] Quads { get; }

    public Footprint Footprint { get; }

    /// <summary>Габариты в локальных координатах меша.</summary>
    public double MinX { get; }

    public double MinY { get; }

    public double MinZ { get; }

    public double MaxX { get; }

    public double MaxY { get; }

    public double MaxZ { get; }

    /// <summary>Радиус круга вокруг локального центра, накрывающего меш сверху.</summary>
    public double HorizontalRadius { get; }
}

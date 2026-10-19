namespace Doom.World;

/// <summary>
///     Ориентированный полигон: четыре вершины и UV-координаты углов. Порядок вершин:
///     [0] → [1] — «верхний» край (ось U), [0] → [3] — «левый» край (ось V). Лицевая сторона —
///     та, куда смотрит нормаль (e03 × e01): с неё полигон виден, с обратной — отсекается.
///     Если вершины [2] и [3] совпадают, полигон — треугольник [0], [1], [3]: так задаются
///     веера крыш, диски и боковины пандуса.
/// </summary>
internal readonly struct Quad3D
{
    public Quad3D(
        double x0, double y0, double z0,
        double x1, double y1, double z1,
        double x2, double y2, double z2,
        double x3, double y3, double z3,
        double u0, double v0,
        double u1, double v1,
        int textureId,
        int shadeBias = 0)
    {
        X0 = x0; Y0 = y0; Z0 = z0;
        X1 = x1; Y1 = y1; Z1 = z1;
        X2 = x2; Y2 = y2; Z2 = z2;
        X3 = x3; Y3 = y3; Z3 = z3;
        U0 = u0; V0 = v0;
        U1 = u1; V1 = v1;
        TextureId = textureId;
        ShadeBias = shadeBias;
    }

    public double X0 { get; } public double Y0 { get; } public double Z0 { get; }
    public double X1 { get; } public double Y1 { get; } public double Z1 { get; }
    public double X2 { get; } public double Y2 { get; } public double Z2 { get; }
    public double X3 { get; } public double Y3 { get; } public double Z3 { get; }

    /// <summary>UV углов: (U0,V0) — у вершины [0], U1 — вдоль ребра [0]→[1], V1 — вдоль [0]→[3].</summary>
    public double U0 { get; } public double V0 { get; }
    public double U1 { get; } public double V1 { get; }

    /// <summary>Номер текстуры (assets/walls/), 0 — без текстуры, цвет экземпляра или палитры.</summary>
    public int TextureId { get; }

    /// <summary>Поправка затенения: −1 темнее (боковая грань), +1 светлее (крыша).</summary>
    public int ShadeBias { get; }

    /// <summary>Вершины [2] и [3] совпадают — это треугольник.</summary>
    public bool IsTriangle => X2 == X3 && Y2 == Y3 && Z2 == Z3;

    public (double X, double Y, double Z) Vertex(int index) => index switch
    {
        0 => (X0, Y0, Z0),
        1 => (X1, Y1, Z1),
        2 => (X2, Y2, Z2),
        _ => (X3, Y3, Z3)
    };
}

namespace Doom.World;

internal enum DoorKind
{
    /// <summary>Распашная: поворачивается на петлях на 90°.</summary>
    Swing,

    /// <summary>Подъёмная решётка ворот: уезжает вверх.</summary>
    Slide
}

/// <summary>
///     Дверь или решётка ворот: открывается и закрывается клавишей «использовать» (E).
///     Распашная дверь открывается от игрока, двери одной группы (створки ворот, двойные двери)
///     открываются вместе.
/// </summary>
internal sealed class Door
{
    private const double SecondsToOpen = 0.55;
    private const double SlideSecondsToOpen = 1.4;

    public required DoorKind Kind { get; init; }

    /// <summary>Петля (распашная) или середина низа (решётка).</summary>
    public required double PivotX { get; init; }

    public required double PivotY { get; init; }

    public required double BaseZ { get; init; }

    /// <summary>Направление створки от петли в закрытом состоянии (радианы).</summary>
    public required double ClosedYaw { get; init; }

    public required double Width { get; init; }

    public required double Height { get; init; }

    public double Thickness { get; init; } = 0.08;

    public required int Texture { get; init; }

    public int Group { get; init; }

    /// <summary>0 — закрыта, 1 — открыта.</summary>
    public double Openness { get; private set; }

    public bool IsOpen => _target > 0.5;

    public bool IsMoving => Math.Abs(Openness - _target) > 1e-6;

    private double _target;
    private double _openSign = 1;

    /// <summary>Середина двери в закрытом положении (для поиска «на что смотрит игрок»).</summary>
    public Vec3 Center => Kind == DoorKind.Swing
        ? new Vec3(PivotX + Math.Cos(ClosedYaw) * Width * 0.5, PivotY + Math.Sin(ClosedYaw) * Width * 0.5, BaseZ + Height * 0.5)
        : new Vec3(PivotX, PivotY, BaseZ + Height * 0.5);

    /// <summary>Переключает дверь; распашная открывается в сторону от точки (игрока).</summary>
    public void Toggle(double fromX, double fromY)
    {
        if (_target < 0.5 && Kind == DoorKind.Swing)
        {
            // С какой стороны полотна игрок: дверь поворачивается от него.
            double side = -Math.Sin(ClosedYaw) * (fromX - PivotX) + Math.Cos(ClosedYaw) * (fromY - PivotY);
            _openSign = side > 0 ? -1 : 1;
        }

        _target = _target < 0.5 ? 1 : 0;
    }

    public void Update(double deltaSeconds)
    {
        double speed = deltaSeconds / (Kind == DoorKind.Slide ? SlideSecondsToOpen : SecondsToOpen);
        Openness = Openness < _target ? Math.Min(_target, Openness + speed) : Math.Max(_target, Openness - speed);
    }

    /// <summary>Текущее твёрдое тело створки.</summary>
    public Solid CurrentSolid()
    {
        (double cx, double cy, double yaw, double z) = Placement();
        return Solid.Box(cx, cy, Width * 0.5, Thickness * 0.5, z, z + Height, yaw);
    }

    /// <summary>Грани створки в текущем положении.</summary>
    public void AppendFaces(List<Face> output)
    {
        (double cx, double cy, double yaw, double z) = Placement();
        double cos = Math.Cos(yaw), sin = Math.Sin(yaw);
        Mesh3D mesh = MeshFactory.Box(Width, Thickness, Height, 0,
            new BoxSkin(Texture, Texture, Texture, Texture, Texture, Texture));

        Vec3 Transform(double x, double y, double localZ) =>
            new(cx + x * cos - y * sin, cy + x * sin + y * cos, z + localZ);

        foreach (Quad3D quad in mesh.Quads)
        {
            Face face = Face.FromQuad(quad, Transform, Texture);
            face.Flags |= Kind == DoorKind.Slide ? FaceFlags.AlphaTest | FaceFlags.DoubleSided : FaceFlags.None;
            output.Add(face);
        }
    }

    /// <summary>Центр, поворот и высота низа створки сейчас.</summary>
    private (double X, double Y, double Yaw, double Z) Placement()
    {
        double eased = Openness * Openness * (3 - 2 * Openness);
        if (Kind == DoorKind.Slide)
            return (PivotX, PivotY, ClosedYaw, BaseZ + eased * (Height - 0.25));

        double yaw = ClosedYaw + _openSign * eased * Math.PI / 2 * 0.95;
        return (PivotX + Math.Cos(yaw) * Width * 0.5, PivotY + Math.Sin(yaw) * Width * 0.5, yaw, BaseZ);
    }
}

/// <summary>Вертикальная лестница: в её объёме игрок лезет вверх/вниз клавишами W/S.</summary>
internal sealed record Ladder(Solid Volume);

/// <summary>Декорация-спрайт: дерево, куст, лампа (картинка assets/sprites/&lt;Sprite&gt;.png).</summary>
internal sealed record Decoration(string Sprite, double X, double Y, double Z, bool Solid, double Radius);

/// <summary>Точка появления бонуса.</summary>
internal sealed record PickupSpawn(PickupKind Kind, double X, double Y, double Z, int Amount);

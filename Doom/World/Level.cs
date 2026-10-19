namespace Doom.World;

/// <summary>
///     Уровень в 3D: грани для рендера, твёрдые тела для столкновений, двери, вертикальные
///     лестницы, декорации и бонусы. Собирается загрузчиком карты (<see cref="MapLoader" />),
///     после чего <see cref="Finish" /> строит сетки поиска: по ним физика игрока и луч выстрела
///     проверяют только то, что рядом, а рендер отсекает целые куски уровня вне поля зрения.
/// </summary>
/// <remarks>
///     Единицы — метры: клетка карты 1×1 м, этаж <see cref="StoreyHeight" /> м (3 м: 2.8 м
///     комнаты и 0.2 м перекрытия). Оси: x — на восток, y — на юг, z — вверх.
/// </remarks>
internal sealed class Level
{
    /// <summary>Сторона клетки сеток поиска (метры).</summary>
    private const double GridCell = 2.0;

    /// <summary>Сторона куска уровня, отсекаемого рендером целиком (метры).</summary>
    private const double ChunkSize = 8.0;

    private readonly List<string> _textureNames = new() { string.Empty };
    private readonly Dictionary<string, int> _textureIndex = new(StringComparer.OrdinalIgnoreCase);

    private SpatialGrid _solidGrid = SpatialGrid.Empty;
    private SpatialGrid _faceGrid = SpatialGrid.Empty;

    public Level(int columns, int rows)
    {
        Columns = columns;
        Rows = rows;
    }

    public int Columns { get; }

    public int Rows { get; }

    /// <summary>Высота этажа: от пола до пола следующего этажа.</summary>
    public double StoreyHeight { get; set; } = 3.0;

    public string Name { get; set; } = "Уровень";

    /// <summary>Имя панорамы неба (assets/sky/).</summary>
    public string Sky { get; set; } = "day";

    public double StartX { get; set; } = 1.5;

    public double StartY { get; set; } = 1.5;

    /// <summary>Высота ног на старте (пол этажа).</summary>
    public double StartZ { get; set; }

    /// <summary>Направление взгляда на старте (радианы; 0 — восток, π/2 — юг).</summary>
    public double StartAngle { get; set; }

    /// <summary>Таблица текстур: индекс → имя файла (assets/textures/). Индекс 0 — «нет».</summary>
    public IReadOnlyList<string> TextureNames => _textureNames;

    /// <summary>Неподвижные грани (стены, полы, мебель, крыши).</summary>
    public List<Face> Faces { get; } = new();

    /// <summary>Неподвижные твёрдые тела.</summary>
    public List<Solid> Solids { get; } = new();

    public List<Door> Doors { get; } = new();

    public List<Ladder> Ladders { get; } = new();

    public List<Decoration> Decorations { get; } = new();

    /// <summary>Расстановка бонусов (каждый рестарт создаёт свежие копии).</summary>
    public List<PickupSpawn> PickupSpawns { get; } = new();

    /// <summary>Куски уровня для отсечения рендером: индексы граней и габариты.</summary>
    public IReadOnlyList<FaceChunk> Chunks { get; private set; } = Array.Empty<FaceChunk>();

    /// <summary>Индекс текстуры по имени (добавляет в таблицу при первом обращении).</summary>
    public int TextureIndex(string name)
    {
        if (string.IsNullOrEmpty(name))
            return 0;

        if (_textureIndex.TryGetValue(name, out int index))
            return index;

        index = _textureNames.Count;
        _textureNames.Add(name);
        _textureIndex[name] = index;
        return index;
    }

    public List<Pickup> CreatePickups() =>
        PickupSpawns.Select(spawn => new Pickup
        {
            X = spawn.X, Y = spawn.Y, Z = spawn.Z, Kind = spawn.Kind, Amount = spawn.Amount
        }).ToList();

    /// <summary>Строит сетки поиска и куски для рендера. Вызывается после загрузки.</summary>
    public void Finish()
    {
        double width = Columns + 4, height = Rows + 4;

        _solidGrid = new SpatialGrid(-2, -2, width, height, GridCell);
        for (int i = 0; i < Solids.Count; i++)
        {
            (double minX, double minY, double maxX, double maxY) = Solids[i].Bounds;
            _solidGrid.Add(i, minX, minY, maxX, maxY);
        }

        _faceGrid = new SpatialGrid(-2, -2, width, height, GridCell);
        for (int i = 0; i < Faces.Count; i++)
        {
            (Vec3 min, Vec3 max) = FaceBounds(Faces[i]);
            _faceGrid.Add(i, min.X, min.Y, max.X, max.Y);
        }

        _solidGrid.Freeze();
        _faceGrid.Freeze();
        Chunks = BuildChunks();
    }

    private List<FaceChunk> BuildChunks()
    {
        var buckets = new Dictionary<(int, int), List<int>>();
        for (int i = 0; i < Faces.Count; i++)
        {
            (Vec3 min, Vec3 max) = FaceBounds(Faces[i]);
            var key = ((int)Math.Floor((min.X + max.X) * 0.5 / ChunkSize), (int)Math.Floor((min.Y + max.Y) * 0.5 / ChunkSize));
            if (!buckets.TryGetValue(key, out List<int>? list))
                buckets[key] = list = new List<int>();
            list.Add(i);
        }

        var chunks = new List<FaceChunk>();
        foreach (List<int> indices in buckets.Values)
        {
            var min = new Vec3(double.MaxValue, double.MaxValue, double.MaxValue);
            var max = new Vec3(double.MinValue, double.MinValue, double.MinValue);
            foreach (int index in indices)
            {
                (Vec3 a, Vec3 b) = FaceBounds(Faces[index]);
                min = new Vec3(Math.Min(min.X, a.X), Math.Min(min.Y, a.Y), Math.Min(min.Z, a.Z));
                max = new Vec3(Math.Max(max.X, b.X), Math.Max(max.Y, b.Y), Math.Max(max.Z, b.Z));
            }

            chunks.Add(new FaceChunk(indices.ToArray(), min, max));
        }

        return chunks;
    }

    private static (Vec3 Min, Vec3 Max) FaceBounds(in Face face)
    {
        Vec3 min = face.P0, max = face.P0;
        for (int i = 1; i < face.Count; i++)
        {
            Vec3 p = face.Vertex(i);
            min = new Vec3(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
            max = new Vec3(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
        }

        return (min, max);
    }

    // ============================================================
    //   Столкновения
    // ============================================================

    /// <summary>
    ///     Мешает ли что-то вертикальному цилиндру (круг радиуса <paramref name="radius" /> между
    ///     высотами <paramref name="bottom" />..<paramref name="top" />).
    /// </summary>
    public bool Blocked(double x, double y, double radius, double bottom, double top)
    {
        bool blocked = false;
        _solidGrid.Visit(x - radius, y - radius, x + radius, y + radius, index =>
        {
            Solid solid = Solids[index];
            if (solid.ZMax > bottom && solid.ZMin < top && solid.OverlapsCircle(x, y, radius))
                blocked = true;
            return !blocked;
        });

        if (blocked)
            return true;

        foreach (Door door in Doors)
        {
            Solid solid = door.CurrentSolid();
            if (solid.ZMax > bottom && solid.ZMin < top && solid.OverlapsCircle(x, y, radius))
                return true;
        }

        foreach (Decoration decoration in Decorations)
        {
            if (!decoration.Solid || decoration.Z >= top || decoration.Z + 2.5 <= bottom)
                continue;

            double dx = decoration.X - x, dy = decoration.Y - y, reach = decoration.Radius + radius;
            if (dx * dx + dy * dy < reach * reach)
                return true;
        }

        return false;
    }

    /// <summary>
    ///     Высота опоры под кругом: самый высокий верх тела, не выше <paramref name="maxTop" />.
    ///     <see cref="double.NegativeInfinity" /> — опоры нет (пропасть).
    /// </summary>
    public double GroundHeight(double x, double y, double radius, double maxTop)
    {
        double ground = double.NegativeInfinity;
        _solidGrid.Visit(x - radius, y - radius, x + radius, y + radius, index =>
        {
            Solid solid = Solids[index];
            if (solid.ZMax <= maxTop && solid.ZMax > ground && solid.OverlapsCircle(x, y, radius))
                ground = solid.ZMax;
            return true;
        });

        foreach (Door door in Doors)
        {
            Solid solid = door.CurrentSolid();
            if (solid.ZMax <= maxTop && solid.ZMax > ground && solid.OverlapsCircle(x, y, radius))
                ground = solid.ZMax;
        }

        return ground;
    }

    /// <summary>Нижняя граница ближайшего тела над головой (не ниже <paramref name="minBottom" />).</summary>
    public double CeilingHeight(double x, double y, double radius, double minBottom)
    {
        double ceiling = double.PositiveInfinity;
        _solidGrid.Visit(x - radius, y - radius, x + radius, y + radius, index =>
        {
            Solid solid = Solids[index];
            if (solid.ZMin >= minBottom && solid.ZMin < ceiling && solid.OverlapsCircle(x, y, radius))
                ceiling = solid.ZMin;
            return true;
        });

        return ceiling;
    }

    /// <summary>Лестница, в объёме которой стоит игрок.</summary>
    public Ladder? LadderAt(double x, double y, double radius, double bottom, double top)
    {
        foreach (Ladder ladder in Ladders)
        {
            Solid volume = ladder.Volume;
            if (volume.ZMax > bottom && volume.ZMin < top && volume.OverlapsCircle(x, y, radius))
                return ladder;
        }

        return null;
    }

    // ============================================================
    //   Луч (выстрелы, снаряды, «использовать»)
    // ============================================================

    /// <summary>
    ///     Ближайшее попадание луча в мир (грани уровня, двери, стволы деревьев) не дальше
    ///     <paramref name="maxDistance" />. Направление — единичное.
    /// </summary>
    public bool RayCast(Vec3 origin, Vec3 direction, double maxDistance, out RayHit hit)
    {
        double best = maxDistance;
        Vec3 bestNormal = default;
        bool found = false;

        // Грани уровня — по клеткам сетки вдоль луча (2D DDA), пока ближайшее попадание не
        // окажется ближе выхода из текущей клетки.
        _faceGrid.Trace(origin.X, origin.Y, direction.X, direction.Y, maxDistance, index =>
        {
            Face face = Faces[index];
            if (face.Intersect(origin, direction, out double distance) && distance < best && distance > 1e-4)
            {
                best = distance;
                bestNormal = face.Normal;
                found = true;
            }
        }, exitDistance => found && best <= exitDistance);

        var doorFaces = new List<Face>(6);
        foreach (Door door in Doors)
        {
            doorFaces.Clear();
            door.AppendFaces(doorFaces);
            foreach (Face face in doorFaces)
            {
                if ((face.Flags & FaceFlags.AlphaTest) != 0)
                    continue; // сквозь решётку пули пролетают

                if (face.Intersect(origin, direction, out double distance) && distance < best && distance > 1e-4)
                {
                    best = distance;
                    bestNormal = face.Normal;
                    found = true;
                }
            }
        }

        foreach (Decoration decoration in Decorations)
        {
            if (decoration.Solid && RayCylinder(origin, direction, decoration, out double distance) && distance < best)
            {
                best = distance;
                Vec3 point = origin + direction * distance;
                bestNormal = new Vec3(point.X - decoration.X, point.Y - decoration.Y, 0).Normalized;
                found = true;
            }
        }

        // Нормаль — навстречу лучу (грани с изнанки тоже ловят пули).
        if (found && bestNormal.Dot(direction) > 0)
            bestNormal = -bestNormal;

        hit = new RayHit(best, origin + direction * best, bestNormal);
        return found;
    }

    private static bool RayCylinder(Vec3 origin, Vec3 direction, Decoration decoration, out double distance)
    {
        distance = 0;
        double ox = origin.X - decoration.X, oy = origin.Y - decoration.Y;
        double a = direction.X * direction.X + direction.Y * direction.Y;
        if (a < 1e-12)
            return false;

        double b = ox * direction.X + oy * direction.Y;
        double c = ox * ox + oy * oy - decoration.Radius * decoration.Radius;
        double discriminant = b * b - a * c;
        if (discriminant < 0 || c < 0)
            return false;

        distance = (-b - Math.Sqrt(discriminant)) / a;
        double z = origin.Z + direction.Z * distance;
        return distance > 0 && z >= decoration.Z && z <= decoration.Z + 2.5;
    }
}

/// <summary>Попадание луча: дистанция, точка и нормаль поверхности (навстречу лучу).</summary>
internal readonly record struct RayHit(double Distance, Vec3 Point, Vec3 Normal);

/// <summary>Кусок уровня для отсечения рендером: грани и их общие габариты.</summary>
internal sealed record FaceChunk(int[] Faces, Vec3 Min, Vec3 Max);

/// <summary>Равномерная сетка в плане: в каждой клетке — индексы объектов, задевающих её.</summary>
internal sealed class SpatialGrid
{
    public static readonly SpatialGrid Empty = new(0, 0, 1, 1, 1);

    private readonly double _originX, _originY, _cell;
    private readonly int _columns, _rows;
    private readonly List<int>[] _building;
    private int[][] _cells;
    private int[] _stamps = Array.Empty<int>();
    private int _stamp;

    public SpatialGrid(double originX, double originY, double width, double height, double cell)
    {
        _originX = originX;
        _originY = originY;
        _cell = cell;
        _columns = Math.Max(1, (int)Math.Ceiling(width / cell));
        _rows = Math.Max(1, (int)Math.Ceiling(height / cell));
        _building = new List<int>[_columns * _rows];
        _cells = new int[_columns * _rows][];
    }

    public void Add(int index, double minX, double minY, double maxX, double maxY)
    {
        (int c0, int r0) = CellOf(minX, minY);
        (int c1, int r1) = CellOf(maxX, maxY);
        for (int r = r0; r <= r1; r++)
        for (int c = c0; c <= c1; c++)
            (_building[r * _columns + c] ??= new List<int>()).Add(index);

        _stamps = _stamps.Length > index ? _stamps : new int[Math.Max(index + 1, _stamps.Length * 2)];
    }

    public void Freeze()
    {
        for (int i = 0; i < _building.Length; i++)
            _cells[i] = _building[i]?.ToArray() ?? Array.Empty<int>();
    }

    private (int Column, int Row) CellOf(double x, double y) =>
        (Math.Clamp((int)Math.Floor((x - _originX) / _cell), 0, _columns - 1),
            Math.Clamp((int)Math.Floor((y - _originY) / _cell), 0, _rows - 1));

    /// <summary>Обходит объекты в прямоугольнике (каждый — один раз). Визитёр возвращает false, чтобы остановиться.</summary>
    public void Visit(double minX, double minY, double maxX, double maxY, Func<int, bool> visitor)
    {
        if (++_stamp == int.MaxValue)
        {
            Array.Clear(_stamps);
            _stamp = 1;
        }

        (int c0, int r0) = CellOf(minX, minY);
        (int c1, int r1) = CellOf(maxX, maxY);
        for (int r = r0; r <= r1; r++)
        for (int c = c0; c <= c1; c++)
        {
            foreach (int index in _cells[r * _columns + c])
            {
                if (_stamps[index] == _stamp)
                    continue;

                _stamps[index] = _stamp;
                if (!visitor(index))
                    return;
            }
        }
    }

    /// <summary>
    ///     Обходит объекты клеток вдоль луча в плане по порядку. После каждой клетки спрашивает
    ///     <paramref name="stopAfter" /> с дистанцией выхода луча из неё: true — дальше не идти.
    /// </summary>
    public void Trace(double x, double y, double dx, double dy, double maxDistance, Action<int> visitor,
        Func<double, bool> stopAfter)
    {
        if (++_stamp == int.MaxValue)
        {
            Array.Clear(_stamps);
            _stamp = 1;
        }

        double gx = (x - _originX) / _cell, gy = (y - _originY) / _cell;
        int column = (int)Math.Floor(gx), row = (int)Math.Floor(gy);
        int stepX = dx > 0 ? 1 : -1, stepY = dy > 0 ? 1 : -1;
        double deltaX = Math.Abs(dx) < 1e-12 ? double.PositiveInfinity : Math.Abs(_cell / dx);
        double deltaY = Math.Abs(dy) < 1e-12 ? double.PositiveInfinity : Math.Abs(_cell / dy);
        double sideX = double.IsPositiveInfinity(deltaX) ? double.PositiveInfinity
            : (dx > 0 ? column + 1 - gx : gx - column) * deltaX;
        double sideY = double.IsPositiveInfinity(deltaY) ? double.PositiveInfinity
            : (dy > 0 ? row + 1 - gy : gy - row) * deltaY;

        for (int guard = 0; guard < 4096; guard++)
        {
            double exit = Math.Min(Math.Min(sideX, sideY), maxDistance);
            if (column >= 0 && row >= 0 && column < _columns && row < _rows)
            {
                foreach (int index in _cells[row * _columns + column])
                {
                    if (_stamps[index] == _stamp)
                        continue;

                    _stamps[index] = _stamp;
                    visitor(index);
                }
            }

            if (exit >= maxDistance || stopAfter(exit))
                return;

            if (sideX < sideY)
            {
                sideX += deltaX;
                column += stepX;
            }
            else
            {
                sideY += deltaY;
                row += stepY;
            }
        }
    }
}

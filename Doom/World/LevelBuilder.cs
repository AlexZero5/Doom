namespace Doom.World;

/// <summary>
///     Превращает клетки карты в 3D: в каждой клетке — вертикальные отрезки твёрдого (стены,
///     подоконники, перемычки, перекрытия этажей). Из них получаются:
///     <list type="bullet">
///         <item>грани — только там, где твёрдое граничит с пустотой (соседние одинаковые грани
///         склеиваются в длинные полосы и большие прямоугольники: меньше работы рендеру);</item>
///         <item>твёрдые тела для столкновений (склеены по рядам);</item>
///         <item>двери и решётки ворот.</item>
///     </list>
/// </summary>
/// <remarks>
///     Сторона стены, обращённая в комнату, берёт «обои» этой комнаты (из легенды полов), если
///     стена не помечена keep: так одна перегородка с двух сторон оклеена по-разному.
/// </remarks>
internal sealed class LevelBuilder
{
    /// <summary>Толщина перекрытия между этажами (и земли под первым этажом).</summary>
    public const double SlabThickness = 0.2;

    private const double Epsilon = 1e-4;

    private readonly Level _level;
    private readonly TextureMetrics _metrics;
    private readonly int _storeys;
    private readonly double _storeyHeight;
    private readonly int _columns;
    private readonly int _rows;

    private readonly List<Span>[] _spans;
    private readonly string?[,,] _floor;
    private readonly string?[,,] _skin;
    private readonly string?[,,] _ceiling;
    private readonly string?[,,] _ceilingTop;
    private readonly List<DoorCell> _doorCells = new();

    private Segment[][] _segments = Array.Empty<Segment[]>();

    public LevelBuilder(Level level, TextureMetrics metrics, int storeys)
    {
        _level = level;
        _metrics = metrics;
        _storeys = storeys;
        _storeyHeight = level.StoreyHeight;
        _columns = level.Columns;
        _rows = level.Rows;
        _spans = new List<Span>[_columns * _rows];
        _floor = new string?[storeys, _columns, _rows];
        _skin = new string?[storeys, _columns, _rows];
        _ceiling = new string?[storeys, _columns, _rows];
        _ceilingTop = new string?[storeys, _columns, _rows];
    }

    public void AddWall(int storey, int column, int row, char symbol, WallDef def)
    {
        double baseZ = storey * _storeyHeight;
        int texture = _level.TextureIndex(def.Texture);
        foreach ((double bottom, double top) in def.Spans)
        {
            double z1 = Math.Min(top, _storeyHeight);
            if (z1 - bottom > Epsilon)
                SpansOf(column, row).Add(new Span(baseZ + bottom, baseZ + z1, false, texture, def.Keep, 0, 0, storey));
        }

        if (def.Door is not null)
            _doorCells.Add(new DoorCell(storey, column, row, symbol, def));
    }

    public void SetFloor(int storey, int column, int row, string texture, string? skin)
    {
        _floor[storey, column, row] = texture;
        _skin[storey, column, row] = skin;
    }

    public void SetCeiling(int storey, int column, int row, string texture, string? top)
    {
        _ceiling[storey, column, row] = texture;
        _ceilingTop[storey, column, row] = top;
    }

    private List<Span> SpansOf(int column, int row) => _spans[row * _columns + column] ??= new List<Span>();

    public void Build()
    {
        AddSlabs();

        _segments = new Segment[_spans.Length][];
        for (int i = 0; i < _spans.Length; i++)
            _segments[i] = Resolve(_spans[i]);

        EmitFaces();
        EmitSolids();
        EmitDoors();
    }

    // ============================================================
    //   Перекрытия: пол этажа N и потолок этажа N−1 — одна плита
    // ============================================================

    private void AddSlabs()
    {
        for (int boundary = 0; boundary <= _storeys; boundary++)
        for (int row = 0; row < _rows; row++)
        for (int column = 0; column < _columns; column++)
        {
            string? floor = boundary < _storeys ? _floor[boundary, column, row] : null;
            string? ceiling = boundary > 0 ? _ceiling[boundary - 1, column, row] : null;
            string? roof = boundary > 0 ? _ceilingTop[boundary - 1, column, row] : null;
            if (floor is null && ceiling is null)
                continue;

            int top = _level.TextureIndex(floor ?? roof ?? ceiling!);
            int bottom = _level.TextureIndex(ceiling ?? floor!);
            double z1 = boundary * _storeyHeight;
            SpansOf(column, row).Add(new Span(z1 - SlabThickness, z1, true, 0, false, top, bottom, boundary));
        }
    }

    /// <summary>
    ///     Отрезки клетки → непересекающиеся участки с «хозяином»: где стена и плита перекрываются,
    ///     побеждает стена (её текстура на боковине).
    /// </summary>
    private static Segment[] Resolve(List<Span>? spans)
    {
        if (spans is null || spans.Count == 0)
            return Array.Empty<Segment>();

        var points = spans.SelectMany(span => new[] { span.Z0, span.Z1 }).Distinct().OrderBy(z => z).ToList();
        var segments = new List<Segment>();

        for (int i = 0; i + 1 < points.Count; i++)
        {
            double a = points[i], b = points[i + 1];
            if (b - a < Epsilon)
                continue;

            int owner = -1;
            for (int s = 0; s < spans.Count; s++)
            {
                if (spans[s].Z0 > a + Epsilon || spans[s].Z1 < b - Epsilon)
                    continue;

                if (owner < 0 || (spans[owner].Slab && !spans[s].Slab))
                    owner = s;
            }

            if (owner < 0)
                continue;

            if (segments.Count > 0 && segments[^1].Owner.Equals(spans[owner]) && Math.Abs(segments[^1].Z1 - a) < Epsilon)
                segments[^1] = segments[^1] with { Z1 = b };
            else
                segments.Add(new Segment(a, b, spans[owner]));
        }

        return segments.ToArray();
    }

    private Segment[] SegmentsAt(int column, int row) =>
        column >= 0 && row >= 0 && column < _columns && row < _rows ? _segments[row * _columns + column] : Array.Empty<Segment>();

    /// <summary>Сплошные интервалы клетки (склеенные участки).</summary>
    private static List<(double Z0, double Z1)> Runs(Segment[] segments)
    {
        var runs = new List<(double, double)>();
        foreach (Segment segment in segments)
        {
            if (runs.Count > 0 && Math.Abs(runs[^1].Item2 - segment.Z0) < Epsilon)
                runs[^1] = (runs[^1].Item1, segment.Z1);
            else
                runs.Add((segment.Z0, segment.Z1));
        }

        return runs;
    }

    // ============================================================
    //   Грани
    // ============================================================

    private static readonly (int Dx, int Dy)[] Directions = { (0, -1), (1, 0), (0, 1), (-1, 0) };

    private void EmitFaces()
    {
        var sides = new Dictionary<SideKey, List<int>>();
        var flats = new Dictionary<FlatKey, HashSet<(int, int)>>();

        for (int row = 0; row < _rows; row++)
        for (int column = 0; column < _columns; column++)
        {
            Segment[] segments = SegmentsAt(column, row);
            if (segments.Length == 0)
                continue;

            // Верх и низ каждого сплошного куска.
            int first = 0;
            for (int i = 0; i < segments.Length; i++)
            {
                bool lastOfRun = i == segments.Length - 1 || Math.Abs(segments[i + 1].Z0 - segments[i].Z1) > Epsilon;
                if (!lastOfRun)
                    continue;

                // Если кусок кончается перекрытием, сверху — пол этажа, снизу — потолок (даже
                // когда боковину «забрала» стена: пол второго этажа над перегородкой первого).
                List<Span> spans = _spans[row * _columns + column];
                double topZ = segments[i].Z1, bottomZ = segments[first].Z0;
                Span? topSlab = spans.Where(span => span.Slab && Math.Abs(span.Z1 - topZ) < Epsilon).Cast<Span?>().FirstOrDefault();
                Span? bottomSlab = spans.Where(span => span.Slab && Math.Abs(span.Z0 - bottomZ) < Epsilon).Cast<Span?>().FirstOrDefault();

                AddFlat(flats, topZ, true, topSlab?.TopTexture ?? segments[i].Owner.WallTexture, column, row);
                if (bottomZ > -SlabThickness + 0.01)
                    AddFlat(flats, bottomZ, false, bottomSlab?.BottomTexture ?? segments[first].Owner.WallTexture, column, row);

                first = i + 1;
            }

            // Боковины: участки минус то, что закрыто соседом.
            for (int direction = 0; direction < 4; direction++)
            {
                (int dx, int dy) = Directions[direction];
                int neighborColumn = column + dx, neighborRow = row + dy;
                List<(double Z0, double Z1)> covered = Runs(SegmentsAt(neighborColumn, neighborRow));

                foreach (Segment segment in segments)
                {
                    foreach ((double z0, double z1) in Subtract(segment.Z0, segment.Z1, covered))
                    {
                        Span owner = segment.Owner;
                        int texture = owner.Slab ? owner.TopTexture : SideTexture(owner, neighborColumn, neighborRow);
                        double vBase = owner.Slab ? 0 : owner.Storey * _storeyHeight;
                        int line = direction switch { 0 => row, 1 => column + 1, 2 => row + 1, _ => column };
                        int position = direction is 0 or 2 ? column : row;

                        var key = new SideKey(direction, line, Key(z0), Key(z1), texture, Key(vBase));
                        if (!sides.TryGetValue(key, out List<int>? positions))
                            sides[key] = positions = new List<int>();
                        positions.Add(position);
                    }
                }
            }
        }

        foreach ((SideKey key, List<int> positions) in sides)
        {
            positions.Sort();
            int start = 0;
            for (int i = 1; i <= positions.Count; i++)
            {
                if (i < positions.Count && positions[i] == positions[i - 1] + 1)
                    continue;

                EmitSide(key, positions[start], positions[i - 1] + 1);
                start = i;
            }
        }

        foreach ((FlatKey key, HashSet<(int, int)> cells) in flats)
            foreach ((int c0, int r0, int c1, int r1) in Rectangles(cells))
                EmitFlat(key, c0, r0, c1, r1);
    }

    private int SideTexture(Span owner, int neighborColumn, int neighborRow)
    {
        if (owner.Keep || neighborColumn < 0 || neighborRow < 0 || neighborColumn >= _columns || neighborRow >= _rows)
            return owner.WallTexture;

        string? skin = _skin[Math.Min(owner.Storey, _storeys - 1), neighborColumn, neighborRow];
        return skin is null ? owner.WallTexture : _level.TextureIndex(skin);
    }

    private static void AddFlat(Dictionary<FlatKey, HashSet<(int, int)>> flats, double z, bool up, int texture, int column, int row)
    {
        var key = new FlatKey(Key(z), up, texture);
        if (!flats.TryGetValue(key, out HashSet<(int, int)>? cells))
            flats[key] = cells = new HashSet<(int, int)>();
        cells.Add((column, row));
    }

    private void EmitSide(SideKey key, int from, int to)
    {
        double z0 = key.Z0 / 1000.0, z1 = key.Z1 / 1000.0, vBase = key.VBase / 1000.0;
        (double tileW, double tileH) = TileOf(key.Texture);
        double V(double z) => (vBase + tileH - z) / tileH;

        Vec3 a, b, facing;
        double ua, ub;
        switch (key.Direction)
        {
            case 0: // север: смотрим с севера на юг, вправо — запад
                a = new Vec3(to, key.Line, 0); b = new Vec3(from, key.Line, 0); facing = new Vec3(0, -1, 0);
                ua = -to / tileW; ub = -from / tileW;
                break;
            case 1: // восток
                a = new Vec3(key.Line, to, 0); b = new Vec3(key.Line, from, 0); facing = new Vec3(1, 0, 0);
                ua = -to / tileW; ub = -from / tileW;
                break;
            case 2: // юг
                a = new Vec3(from, key.Line, 0); b = new Vec3(to, key.Line, 0); facing = new Vec3(0, 1, 0);
                ua = from / tileW; ub = to / tileW;
                break;
            default: // запад
                a = new Vec3(key.Line, from, 0); b = new Vec3(key.Line, to, 0); facing = new Vec3(-1, 0, 0);
                ua = from / tileW; ub = to / tileW;
                break;
        }

        _level.Faces.Add(Face.Create(
            a with { Z = z1 }, b with { Z = z1 }, b with { Z = z0 }, a with { Z = z0 },
            (ua, V(z1)), (ub, V(z1)), (ub, V(z0)), (ua, V(z0)),
            key.Texture, facing));
    }

    private void EmitFlat(FlatKey key, int c0, int r0, int c1, int r1)
    {
        double z = key.Z / 1000.0;
        (double tileW, double tileH) = TileOf(key.Texture);
        var facing = new Vec3(0, 0, key.Up ? 1 : -1);

        _level.Faces.Add(Face.Create(
            new Vec3(c0, r0, z), new Vec3(c1, r0, z), new Vec3(c1, r1, z), new Vec3(c0, r1, z),
            (c0 / tileW, r0 / tileH), (c1 / tileW, r0 / tileH), (c1 / tileW, r1 / tileH), (c0 / tileW, r1 / tileH),
            key.Texture, facing));
    }

    private (double Width, double Height) TileOf(int texture) => _metrics.SizeOf(_level.TextureNames[texture]);

    /// <summary>Жадное разбиение множества клеток на прямоугольники [c0..c1)×[r0..r1).</summary>
    private static IEnumerable<(int, int, int, int)> Rectangles(HashSet<(int, int)> cells)
    {
        var remaining = new HashSet<(int, int)>(cells);
        foreach ((int column, int row) in cells.OrderBy(cell => cell.Item2).ThenBy(cell => cell.Item1))
        {
            if (!remaining.Contains((column, row)))
                continue;

            int right = column + 1;
            while (remaining.Contains((right, row)))
                right++;

            int bottom = row + 1;
            while (Enumerable.Range(column, right - column).All(c => remaining.Contains((c, bottom))))
                bottom++;

            for (int r = row; r < bottom; r++)
            for (int c = column; c < right; c++)
                remaining.Remove((c, r));

            yield return (column, row, right, bottom);
        }
    }

    /// <summary>Интервал [z0, z1] минус объединение интервалов (отсортированных).</summary>
    private static IEnumerable<(double, double)> Subtract(double z0, double z1, List<(double Z0, double Z1)> covered)
    {
        double cursor = z0;
        foreach ((double a, double b) in covered)
        {
            if (b <= cursor + Epsilon)
                continue;
            if (a >= z1 - Epsilon)
                break;
            if (a > cursor + Epsilon)
                yield return (cursor, a);
            cursor = Math.Max(cursor, b);
            if (cursor >= z1 - Epsilon)
                yield break;
        }

        if (z1 - cursor > Epsilon)
            yield return (cursor, z1);
    }

    private static long Key(double value) => (long)Math.Round(value * 1000);

    // ============================================================
    //   Твёрдые тела
    // ============================================================

    private void EmitSolids()
    {
        var rows = new Dictionary<(long, long, int), List<int>>();
        for (int row = 0; row < _rows; row++)
        for (int column = 0; column < _columns; column++)
        {
            foreach ((double z0, double z1) in Runs(SegmentsAt(column, row)))
            {
                var key = (Key(z0), Key(z1), row);
                if (!rows.TryGetValue(key, out List<int>? columns))
                    rows[key] = columns = new List<int>();
                columns.Add(column);
            }
        }

        foreach (((long z0, long z1, int row), List<int> columns) in rows)
        {
            columns.Sort();
            int start = 0;
            for (int i = 1; i <= columns.Count; i++)
            {
                if (i < columns.Count && columns[i] == columns[i - 1] + 1)
                    continue;

                double from = columns[start], to = columns[i - 1] + 1;
                _level.Solids.Add(Solid.Box((from + to) * 0.5, row + 0.5, (to - from) * 0.5, 0.5, z0 / 1000.0, z1 / 1000.0));
                start = i;
            }
        }
    }

    // ============================================================
    //   Двери
    // ============================================================

    private void EmitDoors()
    {
        var cells = _doorCells.ToDictionary(cell => (cell.Storey, cell.Column, cell.Row));
        int group = 0;
        var done = new HashSet<(int, int, int)>();

        foreach (DoorCell cell in _doorCells)
        {
            if (done.Contains((cell.Storey, cell.Column, cell.Row)))
                continue;

            double doorBase = cell.Storey * _storeyHeight;
            double probe = doorBase + Math.Min(1.0, cell.Def.DoorHeight * 0.5);
            bool alongX = Covers(cell.Column - 1, cell.Row, probe) || Covers(cell.Column + 1, cell.Row, probe)
                          || cells.ContainsKey((cell.Storey, cell.Column + 1, cell.Row))
                          || cells.ContainsKey((cell.Storey, cell.Column - 1, cell.Row));
            (int dx, int dy) = alongX ? (1, 0) : (0, 1);

            // Группа — ряд соседних клеток той же двери вдоль её оси.
            var run = new List<DoorCell> { cell };
            for (int step = 1; cells.TryGetValue((cell.Storey, cell.Column + dx * step, cell.Row + dy * step), out DoorCell? next)
                               && next.Symbol == cell.Symbol; step++)
                run.Add(next);

            group++;
            for (int i = 0; i < run.Count; i++)
            {
                DoorCell leaf = run[i];
                done.Add((leaf.Storey, leaf.Column, leaf.Row));
                AddDoor(leaf, alongX, highEnd: run.Count == 2 && i == 1, group);
            }
        }
    }

    private void AddDoor(DoorCell cell, bool alongX, bool highEnd, int group)
    {
        WallDef def = cell.Def;
        double baseZ = cell.Storey * _storeyHeight;
        int texture = _level.TextureIndex(def.DoorTexture ?? "door_wood");
        double height = Math.Min(def.DoorHeight, _storeyHeight);

        if (def.Door == DoorKind.Slide)
        {
            _level.Doors.Add(new Door
            {
                Kind = DoorKind.Slide, PivotX = cell.Column + 0.5, PivotY = cell.Row + 0.5, BaseZ = baseZ,
                ClosedYaw = alongX ? 0 : Math.PI / 2, Width = 1.0, Height = height, Thickness = 0.12,
                Texture = texture, Group = group
            });
            return;
        }

        double pivotX, pivotY, yaw;
        if (alongX)
        {
            pivotX = highEnd ? cell.Column + 1 : cell.Column;
            pivotY = cell.Row + 0.5;
            yaw = highEnd ? Math.PI : 0;
        }
        else
        {
            pivotX = cell.Column + 0.5;
            pivotY = highEnd ? cell.Row + 1 : cell.Row;
            yaw = highEnd ? -Math.PI / 2 : Math.PI / 2;
        }

        _level.Doors.Add(new Door
        {
            Kind = DoorKind.Swing, PivotX = pivotX, PivotY = pivotY, BaseZ = baseZ, ClosedYaw = yaw,
            Width = 0.98, Height = height - 0.02, Texture = texture, Group = group
        });
    }

    private bool Covers(int column, int row, double z) =>
        SegmentsAt(column, row).Any(segment => segment.Z0 <= z && segment.Z1 >= z);

    private readonly record struct Span(
        double Z0, double Z1, bool Slab, int WallTexture, bool Keep, int TopTexture, int BottomTexture, int Storey);

    private readonly record struct Segment(double Z0, double Z1, Span Owner);

    private readonly record struct SideKey(int Direction, int Line, long Z0, long Z1, int Texture, long VBase);

    private readonly record struct FlatKey(long Z, bool Up, int Texture);

    private sealed record DoorCell(int Storey, int Column, int Row, char Symbol, WallDef Def);
}

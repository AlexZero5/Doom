using System.Globalization;

namespace Doom.World;

/// <summary>
///     Загрузка карты из текстового файла (assets/maps/*.map). Формат — секции:
///     <code>
///     [map]              name = …, sky = day, start = x y угол [этаж], storey = 3
///     [walls]            символ = текстура [пролёты] [keep]       — стена; пролёты «0-0.9 2.1-3» в метрах
///                        символ = door створка перемычка [высота]  — распашная дверь (E)
///                        символ = gate решётка перемычка [высота]  — подъёмная решётка (E)
///     [floors]           символ = текстура [обои_комнаты]
///     [ceilings]         символ = текстура [текстура_крыши_сверху]
///     [grid walls N]     сетка стен этажа N (0 — первый этаж; без номера — тоже 0)
///     [grid floors N]    сетка полов этажа N (нет пола — пустота: лестничный проём, двор)
///     [grid ceilings N]  сетка потолков этажа N ('.' и пробел — небо)
///     [things N]         pickup &lt;вид&gt; x y [кол-во] · decor &lt;спрайт&gt; x y [solid радиус] ·
///                        prop &lt;предмет&gt; x y [угол] [параметры]; «z=0.8» — высота над полом этажа
///     </code>
///     Строки, начинающиеся с «#», — комментарии (кроме сеток). Координаты — в метрах: клетка
///     1×1 м, её центр — 3.5 5.5.
/// </summary>
internal static class MapLoader
{
    public static Level Parse(string text, TextureMetrics? metrics = null)
    {
        metrics ??= TextureMetrics.Default;
        var sections = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        List<string>? current = null;

        foreach (string rawLine in text.Replace("\r", "").Split('\n'))
        {
            string trimmed = rawLine.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                current = new List<string>();
                sections[trimmed[1..^1].Trim()] = current;
                continue;
            }

            current?.Add(rawLine);
        }

        Dictionary<string, string> header = Header(Lines(sections, "map"));
        double storeyHeight = header.TryGetValue("storey", out string? storeyText) ? Number(storeyText, 3.0) : 3.0;

        int storeys = 1 + sections.Keys
            .Select(name => name.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length == 3 && parts[0].Equals("grid", StringComparison.OrdinalIgnoreCase))
            .Select(parts => int.TryParse(parts[2], out int index) ? index : 0)
            .DefaultIfEmpty(0)
            .Max();

        var wallGrids = new List<string>[storeys];
        var floorGrids = new List<string>[storeys];
        var ceilingGrids = new List<string>[storeys];
        for (int storey = 0; storey < storeys; storey++)
        {
            wallGrids[storey] = Grid(sections, "grid walls", storey);
            floorGrids[storey] = Grid(sections, "grid floors", storey);
            ceilingGrids[storey] = Grid(sections, "grid ceilings", storey);
        }

        IEnumerable<List<string>> allGrids = wallGrids.Concat(floorGrids).Concat(ceilingGrids);
        int rows = allGrids.Max(grid => grid.Count);
        int columns = wallGrids.Concat(floorGrids).Concat(ceilingGrids)
            .SelectMany(grid => grid).Select(line => line.Length).DefaultIfEmpty(1).Max();
        if (rows == 0)
            throw new FormatException("В карте нет сеток [grid walls]");

        var level = new Level(columns, rows) { StoreyHeight = storeyHeight };
        ApplyHeader(level, header);

        var wallLegend = Legend(sections, "walls");
        var floorLegend = Legend(sections, "floors");
        var ceilingLegend = Legend(sections, "ceilings");
        var builder = new LevelBuilder(level, metrics, storeys);

        for (int storey = 0; storey < storeys; storey++)
        for (int row = 0; row < rows; row++)
        for (int column = 0; column < columns; column++)
        {
            char wall = At(wallGrids[storey], column, row);
            if (wallLegend.TryGetValue(wall, out string[]? wallDef))
                builder.AddWall(storey, column, row, wall, ParseWall(wallDef, storeyHeight));

            if (floorLegend.TryGetValue(At(floorGrids[storey], column, row), out string[]? floorDef))
                builder.SetFloor(storey, column, row, floorDef[0], floorDef.Length > 1 ? floorDef[1] : null);

            if (ceilingLegend.TryGetValue(At(ceilingGrids[storey], column, row), out string[]? ceilingDef))
                builder.SetCeiling(storey, column, row, ceilingDef[0], ceilingDef.Length > 1 ? ceilingDef[1] : null);
        }

        builder.Build();

        foreach (string name in sections.Keys)
        {
            string[] parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (!parts[0].Equals("things", StringComparison.OrdinalIgnoreCase))
                continue;

            int storey = parts.Length > 1 && int.TryParse(parts[1], out int index) ? index : 0;
            foreach (string line in Lines(sections, name))
                ParseThing(level, line, storey * storeyHeight);
        }

        level.Finish();
        SettleOnGround(level);
        return level;
    }

    /// <summary>Бонусы и старт опускаются на ближайшую опору под ними (пол, стол, ящик).</summary>
    private static void SettleOnGround(Level level)
    {
        for (int i = 0; i < level.PickupSpawns.Count; i++)
        {
            PickupSpawn spawn = level.PickupSpawns[i];
            double ground = level.GroundHeight(spawn.X, spawn.Y, 0.05, spawn.Z + 1.2);
            if (!double.IsNegativeInfinity(ground))
                level.PickupSpawns[i] = spawn with { Z = ground };
        }

        double start = level.GroundHeight(level.StartX, level.StartY, 0.05, level.StartZ + 1.0);
        if (!double.IsNegativeInfinity(start))
            level.StartZ = start;
    }

    // ============================================================
    //   Легенда стен
    // ============================================================

    private static WallDef ParseWall(string[] values, double storeyHeight)
    {
        string kind = values[0].ToLowerInvariant();
        if (kind is "door" or "gate")
        {
            double height = values.Length > 3 ? Number(values[3], 2.2) : kind == "gate" ? storeyHeight : 2.2;
            var spans = new List<(double, double)>();
            if (height < storeyHeight - 0.01)
                spans.Add((height, storeyHeight));

            return new WallDef(
                values.Length > 2 ? values[2] : "brick_red", spans, Keep: false,
                kind == "door" ? DoorKind.Swing : DoorKind.Slide,
                values.Length > 1 ? values[1] : "door_wood", height);
        }

        var parsed = new List<(double, double)>();
        bool keep = false;
        foreach (string token in values.Skip(1))
        {
            if (token.Equals("keep", StringComparison.OrdinalIgnoreCase))
            {
                keep = true;
                continue;
            }

            int dash = token.IndexOf('-', 1);
            if (dash > 0)
                parsed.Add((Number(token[..dash], 0), Number(token[(dash + 1)..], storeyHeight)));
            else
                parsed.Add((0, Number(token, storeyHeight)));
        }

        if (parsed.Count == 0)
            parsed.Add((0, storeyHeight));

        return new WallDef(values[0], parsed, keep, null, null, 0);
    }

    // ============================================================
    //   Предметы
    // ============================================================

    private static void ParseThing(Level level, string line, double baseZ)
    {
        var tokens = line.Split('#')[0].Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        double lift = 0;
        for (int i = tokens.Count - 1; i >= 0; i--)
        {
            if (!tokens[i].StartsWith("z=", StringComparison.OrdinalIgnoreCase))
                continue;

            lift = Number(tokens[i][2..], 0);
            tokens.RemoveAt(i);
        }

        string[] parts = tokens.ToArray();
        if (parts.Length < 4)
            return;

        double x = Number(parts[2], 0), y = Number(parts[3], 0), z = baseZ + lift;

        switch (parts[0].ToLowerInvariant())
        {
            case "pickup" when Enum.TryParse(parts[1], ignoreCase: true, out PickupKind kind):
                level.PickupSpawns.Add(new PickupSpawn(kind, x, y, z,
                    parts.Length > 4 ? (int)Number(parts[4], 1) : DefaultAmount(kind)));
                break;

            case "decor":
                bool solid = parts.Length > 4 && parts[4].Equals("solid", StringComparison.OrdinalIgnoreCase);
                double radius = parts.Length > 5 ? Number(parts[5], 0.25) : 0.25;
                level.Decorations.Add(new Decoration(parts[1], x, y, z, solid, radius));
                break;

            case "prop":
                double angle = parts.Length > 4 ? Number(parts[4], 0) : 0;
                if (!Prefabs.TryPlace(level, parts[1], x, y, z, angle, parts.Skip(5).ToArray()))
                    Console.WriteLine($"[map] неизвестный предмет '{parts[1]}'");
                break;
        }
    }

    /// <summary>Количество боеприпасов в бонусе по умолчанию (как в Doom).</summary>
    private static int DefaultAmount(PickupKind kind) => kind switch
    {
        PickupKind.Clip => 10,
        PickupKind.BulletBox => 50,
        PickupKind.Shells => 4,
        PickupKind.ShellBox => 20,
        PickupKind.Rocket => 1,
        PickupKind.RocketBox => 5,
        PickupKind.Cell => 20,
        PickupKind.CellPack => 100,
        _ => 1
    };

    // ============================================================
    //   Разбор секций
    // ============================================================

    private static IEnumerable<string> Lines(Dictionary<string, List<string>> sections, string name) =>
        sections.TryGetValue(name, out List<string>? lines)
            ? lines.Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith('#'))
            : Enumerable.Empty<string>();

    /// <summary>Сетка этажа: строки как есть (внутри сеток «#» — обычный символ).</summary>
    private static List<string> Grid(Dictionary<string, List<string>> sections, string name, int storey)
    {
        if (!sections.TryGetValue($"{name} {storey}", out List<string>? lines)
            && !(storey == 0 && sections.TryGetValue(name, out lines)))
            return new List<string>();

        var grid = lines.Select(line => line.TrimEnd()).ToList();
        while (grid.Count > 0 && grid[^1].Length == 0)
            grid.RemoveAt(grid.Count - 1);
        while (grid.Count > 0 && grid[0].Length == 0)
            grid.RemoveAt(0);

        return grid;
    }

    private static char At(List<string> grid, int column, int row) =>
        row < grid.Count && column < grid[row].Length ? grid[row][column] : ' ';

    /// <summary>Легенда «символ = значения через пробел».</summary>
    private static Dictionary<char, string[]> Legend(Dictionary<string, List<string>> sections, string name)
    {
        var legend = new Dictionary<char, string[]>();
        foreach (string line in Lines(sections, name))
        {
            int equals = line.IndexOf('=');
            if (equals < 1)
                continue;

            char key = line[..equals].Trim()[0];
            string[] values = line[(equals + 1)..].Split('#')[0]
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (values.Length > 0 && key != ' ' && key != '.')
                legend[key] = values;
        }

        return legend;
    }

    private static Dictionary<string, string> Header(IEnumerable<string> lines)
    {
        var header = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in lines)
        {
            int equals = line.IndexOf('=');
            if (equals > 0)
                header[line[..equals].Trim()] = line[(equals + 1)..].Split('#')[0].Trim();
        }

        return header;
    }

    private static void ApplyHeader(Level level, Dictionary<string, string> header)
    {
        if (header.TryGetValue("name", out string? name))
            level.Name = name;
        if (header.TryGetValue("sky", out string? sky))
            level.Sky = sky;
        if (header.TryGetValue("start", out string? start))
        {
            string[] parts = start.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            level.StartX = Number(parts[0], 1.5);
            level.StartY = parts.Length > 1 ? Number(parts[1], 1.5) : 1.5;
            level.StartAngle = (parts.Length > 2 ? Number(parts[2], 0) : 0) * Math.PI / 180;
            level.StartZ = (parts.Length > 3 ? Number(parts[3], 0) : 0) * level.StoreyHeight;
        }
    }

    internal static double Number(string text, double fallback) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : fallback;
}

/// <summary>Описание стены из легенды: текстура, пролёты (метры от пола этажа), дверь.</summary>
internal sealed record WallDef(
    string Texture,
    List<(double Bottom, double Top)> Spans,
    bool Keep,
    DoorKind? Door,
    string? DoorTexture,
    double DoorHeight);

using AssetForge.Core;

namespace AssetForge.Art;

/// <summary>
///     Строка состояния как в Doom: панель 320×32 (игра растягивает её на ширину экрана),
///     крупные красные цифры, мелкие жёлтые/серые цифры и лицо героя (3D-голова с мимикой).
/// </summary>
/// <remarks>
///     Раскладка (в пикселях панели) — та же, что в игре (HudRenderer):
///     патроны 2..48, здоровье 49..104, оружие 105..140, лицо 142..177, броня 179..234,
///     таблица боезапаса 236..318.
/// </remarks>
internal static class Hud
{
    public const int Width = 320;
    public const int Height = 32;

    // ============================================================
    //   Панель
    // ============================================================

    public static Canvas StatusBar()
    {
        var bar = new Canvas(Width, Height);
        Rgb metal = Rgb.Hex(0x5a5046);

        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            double n = Noise.Fbm(x, y, 64, 801, 8, 3);
            double factor = 0.8 + n * 0.45 + (y == 0 ? 0.35 : y == 1 ? 0.15 : y == Height - 1 ? -0.4 : 0);
            bar[x, y] = metal.Scale(factor).Argb();
        }

        // Заклёпки вдоль верхней кромки.
        for (int x = 6; x < Width; x += 26)
            Rivet(bar, x, 3);

        // Утопленные окна секций.
        Inset(bar, 2, 4, 46, 20);
        Inset(bar, 49, 4, 55, 20);
        Inset(bar, 105, 4, 35, 24);
        Inset(bar, 142, 1, 35, 31, dark: 0.25);
        Inset(bar, 179, 4, 55, 20);
        Inset(bar, 236, 3, 82, 27);

        Label(bar, "AMMO", 25, 26);
        Label(bar, "HEALTH", 76, 26);
        Label(bar, "ARMOR", 206, 26);

        // Таблица боезапаса: подписи строк и разделители «/».
        string[] rows = { "BULL", "SHEL", "RCKT", "CELL" };
        for (int i = 0; i < rows.Length; i++)
        {
            Label(bar, rows[i], 250, 5 + i * 6, center: false, x0: 239);
            Label(bar, "/", 293, 5 + i * 6, center: false, x0: 290);
        }

        Palette.Apply(bar, dither: 6);
        return bar;
    }

    private static void Rivet(Canvas bar, int x, int y)
    {
        bar.Plot(x, y, Rgb.Hex(0xd8c8a8));
        bar.Plot(x + 1, y, Rgb.Hex(0x8a7a62));
        bar.Plot(x, y + 1, Rgb.Hex(0x8a7a62));
        bar.Plot(x + 1, y + 1, Rgb.Hex(0x1a140e));
    }

    /// <summary>Утопленное окно: тёмная середина, тень сверху-слева, блик снизу-справа.</summary>
    private static void Inset(Canvas bar, int x0, int y0, int width, int height, double dark = 0.32)
    {
        for (int y = y0; y < y0 + height && y < Height; y++)
        for (int x = x0; x < x0 + width; x++)
        {
            Rgb color = Rgb.Hex(0x2a2622).Scale(dark / 0.32 * (0.9 + Noise.Unit(x, y, 811) * 0.15));
            if (y == y0 || x == x0) color = Rgb.Hex(0x0c0a08);
            else if (y == y0 + height - 1 || x == x0 + width - 1) color = Rgb.Hex(0x8a7c68);
            bar.Plot(x, y, color);
        }
    }

    // ============================================================
    //   Крошечный шрифт подписей (3×5)
    // ============================================================

    private static readonly Dictionary<char, string[]> Tiny = new()
    {
        ['A'] = new[] { "###", "#.#", "###", "#.#", "#.#" },
        ['B'] = new[] { "##.", "#.#", "##.", "#.#", "##." },
        ['C'] = new[] { "###", "#..", "#..", "#..", "###" },
        ['E'] = new[] { "###", "#..", "##.", "#..", "###" },
        ['H'] = new[] { "#.#", "#.#", "###", "#.#", "#.#" },
        ['K'] = new[] { "#.#", "#.#", "##.", "#.#", "#.#" },
        ['L'] = new[] { "#..", "#..", "#..", "#..", "###" },
        ['M'] = new[] { "#.#", "###", "###", "#.#", "#.#" },
        ['O'] = new[] { "###", "#.#", "#.#", "#.#", "###" },
        ['R'] = new[] { "##.", "#.#", "##.", "#.#", "#.#" },
        ['S'] = new[] { "###", "#..", "###", "..#", "###" },
        ['T'] = new[] { "###", ".#.", ".#.", ".#.", ".#." },
        ['U'] = new[] { "#.#", "#.#", "#.#", "#.#", "###" },
        ['/'] = new[] { "..#", "..#", ".#.", "#..", "#.." },
    };

    private static void Label(Canvas bar, string text, int centerX, int y, bool center = true, int x0 = 0)
    {
        int width = text.Length * 4 - 1;
        int x = center ? centerX - width / 2 : x0;
        foreach (char c in text)
        {
            string[] glyph = Tiny[c];
            for (int gy = 0; gy < 5; gy++)
            for (int gx = 0; gx < 3; gx++)
            {
                if (glyph[gy][gx] != '#') continue;
                bar.Plot(x + gx + 1, y + gy + 1, Rgb.Hex(0x14100c));
                bar.Plot(x + gx, y + gy, Rgb.Hex(0xc8b890));
            }

            x += 4;
        }
    }

    // ============================================================
    //   Цифры
    // ============================================================

    /// <summary>Крупные цифры (как STTNUM): глифы шрифта ×2, красная заливка, объём, обводка.</summary>
    public static IEnumerable<(string Name, Canvas Image)> BigDigits()
    {
        Rgb[] red = { Rgb.Hex(0xf48466), Rgb.Hex(0xd04030), Rgb.Hex(0x8c1810) };
        foreach (char c in "0123456789%-")
        {
            string[] glyph = FontGlyphs.Glyphs[c == '-' ? '-' : c];
            int w = glyph[0].Length * 2 + 2, h = FontGlyphs.Height * 2 + 2;
            var mask = new Mask(w, h);
            for (int y = 0; y < FontGlyphs.Height; y++)
            for (int x = 0; x < glyph[0].Length; x++)
            {
                if (glyph[y][x] != '#') continue;
                for (int s = 0; s < 4; s++)
                    mask[1 + x * 2 + s % 2, 1 + y * 2 + s / 2] = true;
            }

            double[] light = Relief.Light(mask, 1.8, 1.0);
            var canvas = new Canvas(w, h);
            canvas.Paint(mask, (x, y) => Rgb.Ramp((y - 1) / (double)(h - 2), red).Scale(light[y * w + x]));
            canvas.Outline(Rgb.Hex(0x1a0604));
            Palette.Apply(canvas, dither: 4);
            string name = c switch { '%' => "percent", '-' => "minus", _ => c.ToString() };
            yield return (name, canvas);
        }
    }

    private static readonly Dictionary<char, string[]> Small = new()
    {
        ['0'] = new[] { "###", "#.#", "#.#", "#.#", "###" },
        ['1'] = new[] { ".#.", "##.", ".#.", ".#.", "###" },
        ['2'] = new[] { "###", "..#", "###", "#..", "###" },
        ['3'] = new[] { "###", "..#", ".##", "..#", "###" },
        ['4'] = new[] { "#.#", "#.#", "###", "..#", "..#" },
        ['5'] = new[] { "###", "#..", "###", "..#", "###" },
        ['6'] = new[] { "###", "#..", "###", "#.#", "###" },
        ['7'] = new[] { "###", "..#", ".#.", ".#.", ".#." },
        ['8'] = new[] { "###", "#.#", "###", "#.#", "###" },
        ['9'] = new[] { "###", "#.#", "###", "..#", "###" },
    };

    /// <summary>Мелкие цифры 4×6 (с тенью): жёлтые — есть/значение, серые — нет.</summary>
    public static IEnumerable<(string Name, Canvas Image)> SmallDigits()
    {
        foreach ((string variant, Rgb color) in new[] { ("yellow", Rgb.Hex(0xffd84a)), ("gray", Rgb.Hex(0x8a8478)) })
        foreach ((char c, string[] glyph) in Small)
        {
            var canvas = new Canvas(4, 6);
            for (int y = 0; y < 5; y++)
            for (int x = 0; x < 3; x++)
            {
                if (glyph[y][x] != '#') continue;
                canvas.Plot(x + 1, y + 1, Rgb.Hex(0x100c08));
                canvas.Plot(x, y, color.Scale(y == 0 ? 1.15 : y == 4 ? 0.8 : 1));
            }

            Palette.Apply(canvas, dither: 0);
            yield return ($"{variant}_{c}", canvas);
        }
    }

    // ============================================================
    //   Лицо
    // ============================================================

    private enum Mood
    {
        Calm,
        Grin,
        Angry,
        Ouch
    }

    /// <summary>Кадры лица: прямо, влево, вправо, ухмылка, злость (стрельба), боль.</summary>
    public static IEnumerable<(string Name, Canvas Image)> Faces()
    {
        yield return ("face_center", Face(0, 0, Mood.Calm));
        yield return ("face_left", Face(0.38, 1, Mood.Calm));
        yield return ("face_right", Face(-0.38, -1, Mood.Calm));
        yield return ("face_grin", Face(0, 0, Mood.Grin));
        yield return ("face_angry", Face(0, 0, Mood.Angry));
        yield return ("face_ouch", Face(0, 0, Mood.Ouch));
    }

    private static Canvas Face(double yaw, int look, Mood mood)
    {
        var scene = new Scene();
        var center = new Vec3(0, 0, 0);
        var up = new Vec3(0, 1, 0);
        void Add(Sdf shape, Material material) => scene.Add(new Rotated(shape, center, up, yaw), material);

        var skin = new Material(Rgb.Hex(0xc8845e), 0.1, 8);
        var hair = new Material(Rgb.Hex(0x5a3a22), 0.05, 4, Pattern: p => 0.8 + 0.3 * Noise.Unit((int)(p.X * 300), (int)(p.Y * 300), 821));
        var dark = Material.Glow(Rgb.Hex(0x1a0c08));

        Add(new Blend(0.03,
            new Ellipsoid(new Vec3(0, 0.01, 0), new Vec3(0.085, 0.105, 0.095)),
            new Box(new Vec3(0, -0.06, -0.005), new Vec3(0.062, 0.04, 0.065), 0.03),
            new Sphere(new Vec3(-0.088, 0.0, 0.01), 0.018),
            new Sphere(new Vec3(0.088, 0.0, 0.01), 0.018),
            new RoundCone(new Vec3(0, 0.005, -0.095), new Vec3(0, -0.03, -0.112), 0.011, 0.016),
            new Cylinder(new Vec3(0, -0.1, 0.01), new Vec3(0, -0.2, 0.01), 0.055)), skin);

        Add(new Intersect(new Ellipsoid(new Vec3(0, 0.025, 0.006), new Vec3(0.091, 0.105, 0.1)),
            new Box(new Vec3(0, 0.12, 0.02), new Vec3(0.2, 0.075, 0.2))), hair);

        // Глаза: белки, зрачки смотрят в сторону поворота.
        foreach (int side in new[] { -1, 1 })
        {
            var eye = new Vec3(side * 0.033, 0.012, -0.082);
            Add(new Ellipsoid(eye, new Vec3(0.014, 0.008, 0.008)), Material.Matte(Rgb.Hex(0xe8e4dc)));
            Add(new Sphere(eye + new Vec3(look * 0.005, 0, -0.004), 0.0055), Material.Glow(Rgb.Hex(0x3a5a8a)));

            // Брови: при злости внутренние концы опущены, при боли — подняты.
            double inner = mood switch { Mood.Angry => -0.012, Mood.Ouch => 0.012, _ => 0 };
            Add(new RoundCone(new Vec3(side * 0.012, 0.032 + inner, -0.088), new Vec3(side * 0.05, 0.034, -0.078), 0.006, 0.005), hair);
        }

        // Рот.
        switch (mood)
        {
            case Mood.Calm:
                Add(new RoundCone(new Vec3(-0.02, -0.06, -0.1), new Vec3(0.02, -0.06, -0.1), 0.005, 0.005), dark);
                break;
            case Mood.Grin:
                Add(new Box(new Vec3(0, -0.058, -0.1), new Vec3(0.032, 0.008, 0.01), 0.006), Material.Matte(Rgb.Hex(0xece6d8)));
                Add(new RoundCone(new Vec3(-0.034, -0.05, -0.096), new Vec3(-0.026, -0.058, -0.1), 0.004, 0.004), dark);
                Add(new RoundCone(new Vec3(0.034, -0.05, -0.096), new Vec3(0.026, -0.058, -0.1), 0.004, 0.004), dark);
                break;
            case Mood.Angry:
                Add(new Box(new Vec3(0, -0.062, -0.1), new Vec3(0.026, 0.007, 0.01), 0.004), Material.Matte(Rgb.Hex(0xd8d0c0)));
                Add(new Box(new Vec3(0, -0.062, -0.102), new Vec3(0.026, 0.0012, 0.01), 0.001), dark);
                break;
            case Mood.Ouch:
                Add(new Ellipsoid(new Vec3(0, -0.065, -0.098), new Vec3(0.014, 0.017, 0.01)), dark);
                break;
        }

        var camera = new Camera { Position = new Vec3(0, 0.02, -1), Focal = 260, CenterX = 24, CenterY = 27 };
        Canvas face = Renderer3D.Render(scene, camera, 48, 58, ambient: 0.4);
        face.Outline(Rgb.Hex(0x100806));
        Palette.Apply(face, dither: 4);
        return face;
    }
}

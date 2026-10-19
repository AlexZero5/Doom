using AssetForge.Core;

namespace AssetForge.Art;

/// <summary>
///     Материалы: тайлящиеся текстуры 64×64 (одна клетка карты = одна текстура по ширине,
///     высота стены = высота текстуры). Нарисованы как пиксель-арт: блоки, фаски в один пиксель,
///     зерно шума в 2–3 оттенка; потом всё квантуется в общую палитру.
/// </summary>
internal static partial class Materials
{
    public const int Size = 64;

    public static IEnumerable<(string Name, Func<Canvas> Build)> All()
    {
        // Стены снаружи.
        yield return ("brick_red", () => Bricks(Rgb.Hex(0x9a3424), Rgb.Hex(0x8c8478), 11));
        yield return ("brick_yellow", () => Bricks(Rgb.Hex(0xb8924a), Rgb.Hex(0x6c645c), 12));
        yield return ("stone_wall", StoneWall);
        yield return ("plaster_cream", () => Plaster(Rgb.Hex(0xd8c8a0), 21));
        yield return ("wood_siding", WoodSiding);
        yield return ("fence_wood", Fence);
        yield return ("hedge", Hedge);
        yield return ("concrete", Concrete);
        yield return ("metal_panel", MetalPanel);
        yield return ("door_wood", Door);
        yield return ("roof_tiles", RoofTiles);

        // Стены внутри (у обоев внизу плинтус, вверху карниз).
        yield return ("wallpaper_green", () => Wallpaper(Rgb.Hex(0x5c7a4a), Rgb.Hex(0x48623a), Rgb.Hex(0xc8b878), 31, 0));
        yield return ("wallpaper_beige", () => Wallpaper(Rgb.Hex(0xc8b48c), Rgb.Hex(0xb8a27a), Rgb.Hex(0x9c4a3a), 32, 1));
        yield return ("wallpaper_blue", () => Wallpaper(Rgb.Hex(0x5a7aa8), Rgb.Hex(0x4c6a96), Rgb.Hex(0xdcd0b0), 33, 2));
        yield return ("tiles_wall", WallTiles);
        yield return ("wood_panel", WoodPanel);

        // Полы.
        yield return ("grass", Grass);
        yield return ("dirt", Dirt);
        yield return ("gravel", Gravel);
        yield return ("paving", Paving);
        yield return ("parquet", Parquet);
        yield return ("tiles_checker", CheckerTiles);
        yield return ("tiles_bath", BathTiles);
        yield return ("carpet_red", Carpet);
        yield return ("water", Water);

        // Потолки.
        yield return ("ceiling_plaster", () => Plaster(Rgb.Hex(0xc8c4b8), 41, cracks: false));
        yield return ("ceiling_wood", CeilingWood);

        // Мебель и предметы.
        yield return ("wood_light", () => FurnitureWood(Rgb.Hex(0xb48450), 51));
        yield return ("wood_dark", () => FurnitureWood(Rgb.Hex(0x5c3820), 52));
        yield return ("fabric_red", () => Fabric(Rgb.Hex(0x8c2a2a), 61));
        yield return ("fabric_blue", () => Fabric(Rgb.Hex(0x34508a), 62));
        yield return ("fabric_green", () => Fabric(Rgb.Hex(0x3c6a40), 63));
        yield return ("bedsheet", Bedsheet);
        yield return ("enamel_white", () => Enamel(Rgb.Hex(0xd8dcd8)));
        yield return ("metal_dark", () => Enamel(Rgb.Hex(0x3a3e44)));
        yield return ("fridge_front", FridgeFront);
        yield return ("stove_front", StoveFront);
        yield return ("cabinet_front", CabinetFront);
        yield return ("wardrobe_front", WardrobeFront);
        yield return ("books", Books);
        yield return ("tv_front", TvFront);
        yield return ("porcelain", () => Enamel(Rgb.Hex(0xe8ecee)));
        yield return ("car_red", CarPaint);
        yield return ("glass_dark", DarkGlass);

        // Замок, деревня, склад (Art/MaterialsCastle.cs).
        foreach ((string name, Func<Canvas> build) in Extra())
            yield return (name, build);
    }

    // ============================================================
    //   Общие приёмы
    // ============================================================

    private static Canvas New() => new(Size, Size);

    /// <summary>Пиксель с переносом по краям (тайлинг).</summary>
    private static void Put(Canvas canvas, int x, int y, Rgb color) =>
        canvas[((x % Size) + Size) % Size, ((y % Size) + Size) % Size] = color.Argb();

    private static double Grain(int x, int y, int seed, int cells = 8, int octaves = 3) =>
        Noise.Fbm(x, y, Size, seed, cells, octaves) - 0.5;

    /// <summary>
    ///     Блоки (кирпичи, камни, плитка): шов, цвет блока с разбросом, зерно и фаска
    ///     в пиксель — светлая сверху-слева, тёмная снизу-справа.
    /// </summary>
    private static void Block(Canvas canvas, int x0, int y0, int width, int height, Rgb color, int seed,
        double grain = 0.18, int bevel = 1, double chip = 0.0)
    {
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            double n = Grain(x0 + x, y0 + y, seed, 16, 3) * grain * 2;
            double speck = Noise.Unit(x0 + x, y0 + y, seed + 7);
            double factor = 1 + n + (speck > 0.93 ? -0.18 : speck < 0.05 ? 0.12 : 0);

            if (y < bevel || x < bevel) factor += 0.16;
            if (y >= height - bevel || x >= width - bevel) factor -= 0.22;

            // Сколы на углах: пара тёмных пикселей.
            bool corner = (x < 2 || x >= width - 2) && (y < 2 || y >= height - 2);
            if (corner && chip > 0 && Noise.Unit(x0 + x, y0 + y, seed + 13) < chip)
                factor -= 0.35;

            Put(canvas, x0 + x, y0 + y, color.Scale(factor));
        }
    }

    private static Rgb Vary(Rgb color, int seed, int index, double amount) =>
        color.Scale(1 + (Noise.Unit(index, 0, seed) - 0.5) * amount);

    // ============================================================
    //   Стены снаружи
    // ============================================================

    private static Canvas Bricks(Rgb brick, Rgb mortar, int seed)
    {
        Canvas canvas = New();
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
            Put(canvas, x, y, mortar.Scale(0.9 + Grain(x, y, seed + 1) * 0.4));

        int index = 0;
        for (int row = 0; row < 8; row++)
        {
            int shift = row % 2 == 0 ? 0 : 8;
            for (int column = 0; column < 4; column++)
            {
                Rgb color = Vary(brick, seed, index++, 0.32);
                Block(canvas, column * 16 + shift, row * 8, 15, 7, color, seed + index, 0.16, 1, 0.5);
            }
        }

        return canvas;
    }

    private static Canvas StoneWall()
    {
        Canvas canvas = New();
        Rgb mortar = Rgb.Hex(0x2e2a26);
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
            Put(canvas, x, y, mortar);

        int[] rows = { 12, 10, 14, 12, 16 };
        int top = 0, index = 0;
        foreach (int height in rows)
        {
            int x = (int)(Noise.Unit(top, 3, 71) * 20);
            int start = x;
            while (x < start + Size)
            {
                int width = 12 + (int)(Noise.Unit(index, top, 72) * 16);
                if (x + width > start + Size) width = start + Size - x;
                Rgb color = Vary(Rgb.Hex(0x8a8274), 73, index, 0.35);
                Block(canvas, x, top, width - 1, height - 1, color, 74 + index, 0.22, 1, 0.7);
                x += width;
                index++;
            }

            top += height;
        }

        return canvas;
    }

    private static Canvas Plaster(Rgb color, int seed, bool cracks = true)
    {
        Canvas canvas = New();
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            double n = Grain(x, y, seed, 8, 4) * 0.22 + (Noise.Unit(x, y, seed + 1) - 0.5) * 0.08;
            Put(canvas, x, y, color.Scale(1 + n));
        }

        if (cracks)
            Crack(canvas, 10, 20, seed + 5, color.Scale(0.6), 14);

        return canvas;
    }

    /// <summary>Трещина: ломаная тёмная линия, случайно блуждающая.</summary>
    private static void Crack(Canvas canvas, int x, int y, int seed, Rgb color, int length)
    {
        for (int step = 0; step < length; step++)
        {
            Put(canvas, x, y, color);
            double r = Noise.Unit(step, 0, seed);
            x += r < 0.4 ? 1 : r < 0.7 ? 0 : -1;
            y += 1;
        }
    }

    private static Canvas WoodSiding()
    {
        Canvas canvas = New();
        Rgb wood = Rgb.Hex(0x7c5432);
        for (int plank = 0; plank < 8; plank++)
        {
            Rgb tone = Vary(wood, 81, plank, 0.25);
            for (int y = 0; y < 8; y++)
            for (int x = 0; x < Size; x++)
            {
                double grainLine = Noise.Streaks(x, plank * 8 + y, Size, 82, 1, 24) - 0.5;
                double factor = 1 + grainLine * 0.5;
                if (y == 0) factor += 0.2;
                if (y >= 6) factor -= 0.25 + (y - 6) * 0.15; // тень под нахлёстом
                Put(canvas, x, plank * 8 + y, tone.Scale(factor));
            }

            // Стык досок.
            int joint = (int)(Noise.Unit(plank, 1, 83) * Size);
            for (int y = 0; y < 7; y++)
                Put(canvas, joint, plank * 8 + y, tone.Scale(0.45));
        }

        return canvas;
    }

    private static Canvas Fence()
    {
        Canvas canvas = New();
        Rgb gap = Rgb.Hex(0x1c2414);
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
            Put(canvas, x, y, gap.Scale(1 + Grain(x, y, 90) * 0.5));

        Rgb wood = Rgb.Hex(0x9c7448);
        for (int picket = 0; picket < 8; picket++)
        {
            Rgb tone = Vary(wood, 91, picket, 0.25);
            int top = 6 + (picket % 2) * 2;
            for (int y = top; y < Size; y++)
            for (int x = 0; x < 7; x++)
            {
                // Заострённый верх штакетины.
                if (y < top + 3 && Math.Abs(x - 3) > y - top + 1)
                    continue;

                double factor = 1 + (Noise.Streaks(picket * 8 + x, y, Size, 92, 24, 2, 2) - 0.5) * 0.45;
                if (x == 0) factor += 0.18;
                if (x == 6) factor -= 0.25;
                Put(canvas, picket * 8 + x, y, tone.Scale(factor));
            }
        }

        // Две поперечины.
        foreach (int railY in new[] { 22, 48 })
        {
            for (int y = 0; y < 5; y++)
            for (int x = 0; x < Size; x++)
            {
                double factor = 0.85 + (y == 0 ? 0.25 : y == 4 ? -0.3 : 0) + Grain(x, railY + y, 93) * 0.4;
                Put(canvas, x, railY + y, wood.Scale(factor * 0.92));
            }
        }

        return canvas;
    }

    private static Canvas Hedge()
    {
        Canvas canvas = New();
        Rgb dark = Rgb.Hex(0x163a16), mid = Rgb.Hex(0x2e6a28), light = Rgb.Hex(0x5c9c3c);
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
            Put(canvas, x, y, Noise.Fbm(x, y, Size, 101, 8, 3) > 0.5 ? mid.Scale(0.85) : dark.Scale(1.2));

        // Листья: мелкие «капли» с бликом.
        for (int leaf = 0; leaf < 140; leaf++)
        {
            int cx = (int)(Noise.Unit(leaf, 1, 102) * Size);
            int cy = (int)(Noise.Unit(leaf, 2, 102) * Size);
            Rgb tone = Noise.Unit(leaf, 3, 102) > 0.6 ? light : mid;
            Put(canvas, cx, cy, tone);
            Put(canvas, cx + 1, cy, tone.Scale(0.85));
            Put(canvas, cx, cy + 1, tone.Scale(0.7));
            if (tone == light) Put(canvas, cx, cy - 1, light.Scale(1.15));
        }

        return canvas;
    }

    private static Canvas Concrete()
    {
        Canvas canvas = Plaster(Rgb.Hex(0x84847c), 111);
        // Швы опалубки и пятна.
        for (int x = 0; x < Size; x++)
            Put(canvas, x, 31, Rgb.Hex(0x84847c).Scale(0.6));
        for (int spot = 0; spot < 18; spot++)
        {
            int sx = (int)(Noise.Unit(spot, 1, 112) * Size), sy = (int)(Noise.Unit(spot, 2, 112) * Size);
            Put(canvas, sx, sy, Rgb.Hex(0x84847c).Scale(0.7));
        }

        return canvas;
    }

    private static Canvas MetalPanel()
    {
        Canvas canvas = New();
        Rgb steel = Rgb.Hex(0x5e6c7c);
        for (int py = 0; py < 2; py++)
        for (int px = 0; px < 2; px++)
        {
            Block(canvas, px * 32, py * 32, 32, 32, Vary(steel, 121, px + py * 2, 0.12), 122, 0.08, 2);
            foreach ((int rx, int ry) in new[] { (3, 3), (28, 3), (3, 28), (28, 28) })
            {
                Put(canvas, px * 32 + rx, py * 32 + ry, steel.Scale(1.5));
                Put(canvas, px * 32 + rx + 1, py * 32 + ry + 1, steel.Scale(0.5));
            }
        }

        return canvas;
    }

    private static Canvas Door()
    {
        Canvas canvas = New();
        Rgb frame = Rgb.Hex(0x3a2414), wood = Rgb.Hex(0x7a4a26);
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
            Put(canvas, x, y, frame.Scale(1 + Grain(x, y, 131) * 0.4));

        // Полотно двери с филёнками.
        for (int y = 4; y < Size; y++)
        for (int x = 12; x < 52; x++)
        {
            double factor = 1 + (Noise.Streaks(x, y, Size, 132, 16, 2) - 0.5) * 0.4;
            if (x == 12) factor += 0.2;
            if (x == 51) factor -= 0.3;
            Put(canvas, x, y, wood.Scale(factor));
        }

        foreach ((int top, int height) in new[] { (9, 20), (34, 26) })
        {
            for (int y = top; y < top + height; y++)
            for (int x = 17; x < 47; x++)
            {
                bool edgeLight = y == top + height - 1 || x == 46;
                bool edgeDark = y == top || x == 17;
                if (edgeLight || edgeDark)
                    Put(canvas, x, y, wood.Scale(edgeLight ? 1.3 : 0.55));
            }
        }

        // Ручка.
        Put(canvas, 46, 33, Rgb.Hex(0xe0c060));
        Put(canvas, 47, 33, Rgb.Hex(0xa08030));
        Put(canvas, 46, 34, Rgb.Hex(0x806020));
        return canvas;
    }

    private static Canvas RoofTiles()
    {
        Canvas canvas = New();
        Rgb clay = Rgb.Hex(0x5c3e2e); // тёмно-коричневая черепица: не сливается ни с кирпичом, ни с дымкой неба
        for (int row = 0; row < 8; row++)
        {
            int shift = row % 2 == 0 ? 0 : 4;
            for (int tile = 0; tile < 8; tile++)
            {
                Rgb tone = Vary(clay, 141, row * 8 + tile, 0.28);
                for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                {
                    // Полукруглый низ черепицы.
                    double dx = x - 3.5;
                    bool inside = y < 5 || dx * dx + (y - 4) * (y - 4) * 1.6 <= 14;
                    if (!inside) continue;

                    double factor = 1.1 - y * 0.06 + (x == 0 ? 0.15 : x == 7 ? -0.2 : 0)
                                    + Grain(tile * 8 + x, row * 8 + y, 142) * 0.3;
                    Put(canvas, tile * 8 + x + shift, row * 8 + y, tone.Scale(factor));
                }

                Put(canvas, tile * 8 + shift + 3, row * 8 + 7, tone.Scale(0.4));
            }
        }

        return canvas;
    }

    // ============================================================
    //   Стены внутри
    // ============================================================

    /// <summary>Обои с узором, плинтусом и карнизом. <paramref name="pattern" />: 0 — полосы, 1 — цветы, 2 — ромбы.</summary>
    private static Canvas Wallpaper(Rgb baseColor, Rgb stripe, Rgb motif, int seed, int pattern)
    {
        Canvas canvas = New();
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            Rgb color = pattern == 0 && x % 16 >= 10 ? stripe : baseColor;
            Put(canvas, x, y, color.Scale(1 + Grain(x, y, seed) * 0.12));
        }

        for (int cy = 0; cy < Size; cy += 16)
        for (int cx = 0; cx < Size; cx += 16)
        {
            int ox = cx + ((cy / 16) % 2 == 0 ? 3 : 11);
            int oy = cy + 6;
            switch (pattern)
            {
                case 0:
                    Put(canvas, cx + 13, oy, motif);
                    Put(canvas, cx + 12, oy + 1, motif.Scale(0.8));
                    Put(canvas, cx + 14, oy + 1, motif.Scale(0.8));
                    Put(canvas, cx + 13, oy + 2, motif.Scale(0.7));
                    break;
                case 1:
                    foreach ((int dx, int dy) in new[] { (0, -1), (-1, 0), (1, 0), (0, 1) })
                        Put(canvas, ox + dx, oy + dy, motif);
                    Put(canvas, ox, oy, Rgb.Hex(0xe0c040));
                    Put(canvas, ox + 1, oy + 2, Rgb.Hex(0x4c6a30));
                    Put(canvas, ox + 2, oy + 3, Rgb.Hex(0x4c6a30));
                    break;
                default:
                    for (int d = 0; d < 3; d++)
                    {
                        Put(canvas, ox + d, oy - 2 + d, motif);
                        Put(canvas, ox - d, oy - 2 + d, motif);
                        Put(canvas, ox + d, oy + 2 - d, motif);
                        Put(canvas, ox - d, oy + 2 - d, motif);
                    }
                    break;
            }
        }

        Moldings(canvas, Rgb.Hex(0x5a3a22), Rgb.Hex(0xd8d0c0));
        return canvas;
    }

    /// <summary>Деревянный плинтус внизу (6 пикселей) и светлый карниз вверху (3 пикселя).</summary>
    private static void Moldings(Canvas canvas, Rgb baseboard, Rgb crown)
    {
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < 3; y++)
                Put(canvas, x, y, crown.Scale(y == 2 ? 0.7 : 1.0 - y * 0.08));

            for (int y = Size - 6; y < Size; y++)
            {
                double factor = y == Size - 6 ? 1.35 : y == Size - 5 ? 0.6 : 1 + Grain(x * 3, y, 151) * 0.3;
                Put(canvas, x, y, baseboard.Scale(factor));
            }
        }
    }

    private static Canvas WallTiles()
    {
        Canvas canvas = New();
        Rgb grout = Rgb.Hex(0x9a9e9c);
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
            Put(canvas, x, y, grout);

        int index = 0;
        for (int ty = 0; ty < 8; ty++)
        for (int tx = 0; tx < 8; tx++)
        {
            Rgb tile = ty < 2 ? Rgb.Hex(0x7ca8b8) : Rgb.Hex(0xdce4e2); // бордюр сверху
            Block(canvas, tx * 8, ty * 8, 7, 7, Vary(tile, 161, index++, 0.08), 162, 0.05);
        }

        return canvas;
    }

    private static Canvas WoodPanel()
    {
        Canvas canvas = New();
        Rgb wood = Rgb.Hex(0x5a3820);
        for (int board = 0; board < 4; board++)
        {
            Rgb tone = Vary(wood, 171, board, 0.25);
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < 16; x++)
            {
                double grain = Noise.Streaks(board * 16 + x, y, Size, 172, 24, 2) - 0.5;
                double factor = 1 + grain * 0.5 + (x == 0 ? 0.2 : x == 15 ? -0.4 : 0);
                Put(canvas, board * 16 + x, y, tone.Scale(factor));
            }
        }

        Moldings(canvas, Rgb.Hex(0x3a2212), Rgb.Hex(0x6a4a2c));
        return canvas;
    }

    // ============================================================
    //   Полы
    // ============================================================

    private static Canvas Grass()
    {
        Canvas canvas = New();
        Rgb dark = Rgb.Hex(0x2a5a20), mid = Rgb.Hex(0x3e7a2c), light = Rgb.Hex(0x6aa040);
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            double n = Noise.Fbm(x, y, Size, 181, 4, 4);
            Rgb color = n < 0.42 ? dark : n < 0.62 ? mid : mid.Lerp(light, 0.4);
            Put(canvas, x, y, color);
        }

        // Травинки: короткие светлые и тёмные штрихи.
        for (int blade = 0; blade < 180; blade++)
        {
            int x = (int)(Noise.Unit(blade, 1, 182) * Size), y = (int)(Noise.Unit(blade, 2, 182) * Size);
            Rgb tone = Noise.Unit(blade, 3, 182) > 0.5 ? light : dark.Scale(0.8);
            Put(canvas, x, y, tone);
            Put(canvas, x, y + 1, tone.Scale(0.85));
        }

        // Редкие цветочки.
        for (int flower = 0; flower < 5; flower++)
        {
            int x = (int)(Noise.Unit(flower, 1, 183) * Size), y = (int)(Noise.Unit(flower, 2, 183) * Size);
            Put(canvas, x, y, flower % 2 == 0 ? Rgb.Hex(0xf0e060) : Rgb.Hex(0xf0f0f0));
        }

        return canvas;
    }

    private static Canvas Dirt()
    {
        Canvas canvas = New();
        Rgb soil = Rgb.Hex(0x6a4a2e);
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            double n = Noise.Fbm(x, y, Size, 191, 4, 4);
            Put(canvas, x, y, soil.Scale(0.75 + n * 0.5));
        }

        Pebbles(canvas, 40, Rgb.Hex(0x8c7a64), 192, 1);
        return canvas;
    }

    /// <summary>Камушки: эллипсы с бликом сверху и тенью снизу.</summary>
    private static void Pebbles(Canvas canvas, int count, Rgb color, int seed, int maxRadius)
    {
        for (int i = 0; i < count; i++)
        {
            int cx = (int)(Noise.Unit(i, 1, seed) * Size), cy = (int)(Noise.Unit(i, 2, seed) * Size);
            int r = 1 + (int)(Noise.Unit(i, 3, seed) * maxRadius);
            Rgb tone = Vary(color, seed + 1, i, 0.35);
            for (int y = -r; y <= r; y++)
            for (int x = -r - 1; x <= r + 1; x++)
            {
                if (x * x * 0.7 + y * y > r * r + 0.5) continue;
                double factor = y < 0 ? 1.2 : y > 0 ? 0.8 : 1.0;
                Put(canvas, cx + x, cy + y, tone.Scale(factor));
            }

            Put(canvas, cx, cy + r + 1, tone.Scale(0.45));
        }
    }

    private static Canvas Gravel()
    {
        Canvas canvas = New();
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
            Put(canvas, x, y, Rgb.Hex(0x5a5248).Scale(0.85 + Grain(x, y, 201) * 0.5));

        Pebbles(canvas, 150, Rgb.Hex(0x9a9284), 202, 2);
        return canvas;
    }

    private static Canvas Paving()
    {
        Canvas canvas = New();
        Rgb joint = Rgb.Hex(0x3e3a30);
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
            Put(canvas, x, y, joint);

        int index = 0;
        for (int row = 0; row < 4; row++)
        {
            int shift = row % 2 == 0 ? 0 : 16;
            for (int column = 0; column < 2; column++)
                Block(canvas, column * 32 + shift, row * 16, 31, 15, Vary(Rgb.Hex(0x9a8c74), 211, index++, 0.22), 212, 0.2, 1, 0.6);
        }

        return canvas;
    }

    private static Canvas Parquet()
    {
        Canvas canvas = New();
        Rgb wood = Rgb.Hex(0x9c6a3c);
        int index = 0;

        // Ёлочка: блоки 16×8, через ряд — повёрнутые.
        for (int by = 0; by < 4; by++)
        for (int bx = 0; bx < 4; bx++)
        {
            bool horizontal = (bx + by) % 2 == 0;
            Rgb tone = Vary(wood, 221, index++, 0.3);
            for (int half = 0; half < 2; half++)
            for (int y = 0; y < (horizontal ? 8 : 16); y++)
            for (int x = 0; x < (horizontal ? 16 : 8); x++)
            {
                int px = bx * 16 + (horizontal ? x : half * 8 + x);
                int py = by * 16 + (horizontal ? half * 8 + y : y);
                double grain = horizontal
                    ? Noise.Streaks(px, py, Size, 222 + half, 2, 24, 2)
                    : Noise.Streaks(px, py, Size, 222 + half, 24, 2, 2);
                double factor = 1 + (grain - 0.5) * 0.45;
                bool edge = horizontal ? y == 7 || x == 15 : x == 7 || y == 15;
                if (edge) factor -= 0.35;
                Put(canvas, px, py, (half == 0 ? tone : tone.Scale(0.9)).Scale(factor));
            }
        }

        return canvas;
    }

    private static Canvas CheckerTiles()
    {
        Canvas canvas = New();
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
            Put(canvas, x, y, Rgb.Hex(0x6a6a62));

        for (int ty = 0; ty < 4; ty++)
        for (int tx = 0; tx < 4; tx++)
        {
            Rgb tile = (tx + ty) % 2 == 0 ? Rgb.Hex(0xd8d4c8) : Rgb.Hex(0x2a2a2e);
            Block(canvas, tx * 16, ty * 16, 15, 15, tile, 231 + tx + ty * 4, 0.06, 1, 0.3);
        }

        return canvas;
    }

    private static Canvas BathTiles()
    {
        Canvas canvas = New();
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
            Put(canvas, x, y, Rgb.Hex(0xb4bcbc));

        int index = 0;
        for (int ty = 0; ty < 8; ty++)
        for (int tx = 0; tx < 8; tx++)
            Block(canvas, tx * 8, ty * 8, 7, 7, Vary(Rgb.Hex(0x5c8a9c), 241, index++, 0.2), 242, 0.06);

        return canvas;
    }

    private static Canvas Carpet()
    {
        Canvas canvas = New();
        Rgb red = Rgb.Hex(0x8a2420), dark = Rgb.Hex(0x4c1010), gold = Rgb.Hex(0xc89a3c);
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            // Ромбическая решётка и медальоны.
            int dx = Math.Abs((x % 32) - 16), dy = Math.Abs((y % 32) - 16);
            int diamond = dx + dy;
            Rgb color = diamond == 15 || diamond == 16 ? gold
                : diamond < 5 ? dark
                : diamond == 7 ? gold.Scale(0.8)
                : red;
            double fiber = (Noise.Unit(x, y, 251) - 0.5) * 0.16;
            Put(canvas, x, y, color.Scale(1 + fiber));
        }

        return canvas;
    }

    private static Canvas Water()
    {
        Canvas canvas = New();
        Rgb deep = Rgb.Hex(0x1c4a6a), light = Rgb.Hex(0x6aa0c8);
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            double n = Noise.Fbm(x, y, Size, 261, 4, 3);
            double wave = Math.Sin((y + n * 12) * Math.Tau / 16.0);
            Rgb color = wave > 0.85 ? light : deep.Scale(0.85 + n * 0.4);
            Put(canvas, x, y, color);
        }

        return canvas;
    }

    // ============================================================
    //   Потолки
    // ============================================================

    private static Canvas CeilingWood()
    {
        Canvas canvas = New();
        Rgb wood = Rgb.Hex(0x7a5634);
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            int board = y / 8;
            double grain = Noise.Streaks(x, y, Size, 271, 2, 24) - 0.5;
            double factor = 1 + grain * 0.4 + (y % 8 == 7 ? -0.35 : 0) + (Noise.Unit(board, 0, 272) - 0.5) * 0.2;
            Put(canvas, x, y, wood.Scale(factor));
        }

        // Балка поперёк.
        for (int y = 0; y < Size; y++)
        for (int x = 28; x < 36; x++)
            Put(canvas, x, y, Rgb.Hex(0x4a321c).Scale(x == 28 ? 1.3 : x == 35 ? 0.6 : 1));

        return canvas;
    }

    // ============================================================
    //   Мебель
    // ============================================================

    private static Canvas FurnitureWood(Rgb wood, int seed)
    {
        Canvas canvas = New();
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            double grain = Noise.Streaks(x, y, Size, seed, 2, 12, 4);
            double rings = Math.Sin(grain * 18) * 0.12;
            Put(canvas, x, y, wood.Scale(0.9 + (grain - 0.5) * 0.35 + rings));
        }

        return canvas;
    }

    private static Canvas Fabric(Rgb color, int seed)
    {
        Canvas canvas = New();
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            double weave = ((x + y) % 4 < 2 ? 0.06 : -0.06) + ((x - y) % 6 == 0 ? -0.08 : 0);
            Put(canvas, x, y, color.Scale(1 + weave + Grain(x, y, seed) * 0.2));
        }

        return canvas;
    }

    private static Canvas Bedsheet()
    {
        Canvas canvas = New();
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            bool check = (x / 8 + y / 8) % 2 == 0;
            Rgb color = check ? Rgb.Hex(0xdcdcd4) : Rgb.Hex(0xa8bcd8);
            double fold = Math.Sin((x + Noise.Fbm(x, y, Size, 281, 2, 2) * 20) * Math.Tau / 32) * 0.07;
            Put(canvas, x, y, color.Scale(1 + fold));
        }

        return canvas;
    }

    private static Canvas Enamel(Rgb color)
    {
        Canvas canvas = New();
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
            Put(canvas, x, y, color.Scale(1.05 - y / (double)Size * 0.12 + (Noise.Unit(x, y, 291) - 0.5) * 0.04));
        return canvas;
    }

    private static Canvas FridgeFront()
    {
        Canvas canvas = Enamel(Rgb.Hex(0xd8dcd8));
        for (int x = 0; x < Size; x++)
        {
            Put(canvas, x, 22, Rgb.Hex(0x5a5e60));
            Put(canvas, x, 23, Rgb.Hex(0xf0f0ee));
        }

        for (int y = 6; y < 18; y++) Put(canvas, 54, y, Rgb.Hex(0x7a7e80));
        for (int y = 28; y < 50; y++) Put(canvas, 54, y, Rgb.Hex(0x7a7e80));
        return canvas;
    }

    private static Canvas StoveFront()
    {
        Canvas canvas = Enamel(Rgb.Hex(0xc8ccc8));
        for (int x = 6; x < 58; x += 12)
        {
            Put(canvas, x, 5, Rgb.Hex(0x202020));
            Put(canvas, x + 1, 5, Rgb.Hex(0x202020));
            Put(canvas, x, 6, Rgb.Hex(0x404040));
        }

        // Дверца духовки со стеклом.
        for (int y = 14; y < 58; y++)
        for (int x = 6; x < 58; x++)
        {
            bool frame = y < 16 || y > 55 || x < 8 || x > 55;
            bool glass = y > 22 && y < 46 && x > 14 && x < 50;
            if (frame) Put(canvas, x, y, Rgb.Hex(0x8a8e8c));
            else if (glass) Put(canvas, x, y, Rgb.Hex(0x1a1c20).Scale(1 + (x - y > 0 && x - y < 6 ? 0.8 : 0)));
        }

        for (int x = 18; x < 46; x++) Put(canvas, x, 18, Rgb.Hex(0x404448));
        return canvas;
    }

    private static Canvas CabinetFront()
    {
        Canvas canvas = FurnitureWood(Rgb.Hex(0xa87c4c), 301);
        foreach (int top in new[] { 2, 22, 42 })
        {
            for (int y = top; y < top + 19; y++)
            for (int x = 2; x < 62; x++)
            {
                if (y == top || x == 2) Put(canvas, x, y, Rgb.Hex(0xd4a870));
                else if (y == top + 18 || x == 61) Put(canvas, x, y, Rgb.Hex(0x4a2e18));
            }

            for (int x = 26; x < 38; x++)
                Put(canvas, x, top + 8, Rgb.Hex(0xb8b8b0));
        }

        return canvas;
    }

    private static Canvas WardrobeFront()
    {
        Canvas canvas = FurnitureWood(Rgb.Hex(0x6a4428), 311);
        for (int y = 0; y < Size; y++)
        {
            Put(canvas, 31, y, Rgb.Hex(0x2a180c));
            Put(canvas, 32, y, Rgb.Hex(0x9a6a40));
        }

        foreach (int left in new[] { 5, 37 })
        foreach ((int top, int height) in new[] { (6, 24), (34, 24) })
        {
            for (int y = top; y < top + height; y++)
            for (int x = left; x < left + 22; x++)
            {
                if (y == top || x == left) Put(canvas, x, y, Rgb.Hex(0x2e1c0e));
                else if (y == top + height - 1 || x == left + 21) Put(canvas, x, y, Rgb.Hex(0xa87448));
            }
        }

        Put(canvas, 28, 32, Rgb.Hex(0xe0c060));
        Put(canvas, 35, 32, Rgb.Hex(0xe0c060));
        return canvas;
    }

    private static Canvas Books()
    {
        Canvas canvas = FurnitureWood(Rgb.Hex(0x5a3a20), 321);
        Rgb[] spines =
        {
            Rgb.Hex(0x8a2420), Rgb.Hex(0x24467e), Rgb.Hex(0x2e6a28), Rgb.Hex(0xc89a3c),
            Rgb.Hex(0x5c3a6a), Rgb.Hex(0xb8b0a0), Rgb.Hex(0x3a3a3a), Rgb.Hex(0x9c5a2a)
        };

        int index = 0;
        foreach (int shelf in new[] { 2, 23, 44 })
        {
            int x = 2;
            while (x < 62)
            {
                int width = 3 + (int)(Noise.Unit(index, 1, 322) * 4);
                int height = 14 + (int)(Noise.Unit(index, 2, 322) * 5);
                if (x + width > 62) break;
                Rgb spine = spines[(int)(Noise.Unit(index, 3, 322) * spines.Length)];
                for (int y = shelf + 19 - height; y < shelf + 19; y++)
                for (int dx = 0; dx < width; dx++)
                {
                    double factor = dx == 0 ? 1.25 : dx == width - 1 ? 0.6 : 1.0;
                    if (y == shelf + 19 - height + 3) factor *= 1.4; // полоска на корешке
                    Put(canvas, x + dx, y, spine.Scale(factor));
                }

                x += width;
                index++;
            }

            for (int sx = 0; sx < Size; sx++)
            {
                Put(canvas, sx, shelf + 19, Rgb.Hex(0x8a5a30));
                Put(canvas, sx, shelf + 20, Rgb.Hex(0x3a2210));
            }
        }

        return canvas;
    }

    private static Canvas TvFront()
    {
        Canvas canvas = Enamel(Rgb.Hex(0x2a2a2e));
        for (int y = 6; y < 50; y++)
        for (int x = 6; x < 58; x++)
        {
            double glow = 0.3 + 0.12 * Math.Sin(y * 1.7) + (x - y > 10 && x - y < 18 ? 0.3 : 0);
            Put(canvas, x, y, Rgb.Hex(0x3c6a8a).Scale(glow + Noise.Unit(x, y, 331) * 0.15));
        }

        Put(canvas, 52, 55, Rgb.Hex(0xe02020));
        return canvas;
    }

    /// <summary>Крашеный кузов: красная эмаль, светлая полоса блика, к низу — дорожная грязь и рыжие точки ржавчины.</summary>
    private static Canvas CarPaint()
    {
        Canvas canvas = Enamel(Rgb.Hex(0x9c2420));
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            Rgb color = canvas.ColorAt(x, y);
            if (y > 12 && y < 16)
                color = color.Scale(1.25);
            double dirt = Math.Clamp((y - 44) / 20.0, 0, 1) * (0.5 + Noise.Value(x, y, Size, 343, 8) * 0.6);
            color = color.Lerp(Rgb.Hex(0x5a4a38), dirt);
            if (Noise.Unit(x, y, 347) > 0.985 && y > 30)
                color = Rgb.Hex(0x8a4a1c);
            Put(canvas, x, y, color);
        }

        return canvas;
    }

    /// <summary>Тёмное стекло: почти чёрная синева, две косые полосы отражения и пыль по краю.</summary>
    private static Canvas DarkGlass()
    {
        Canvas canvas = New();
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            int diagonal = (x + y) % Size;
            double reflect = diagonal is > 14 and < 22 ? 0.35 : diagonal is > 26 and < 29 ? 0.2 : 0;
            double dust = Math.Clamp((y - 52) / 12.0, 0, 1) * 0.4;
            Rgb color = Rgb.Hex(0x1c2834).Scale(0.9 + y / (double)Size * 0.3 + (Noise.Unit(x, y, 351) - 0.5) * 0.06);
            color = color.Lerp(Rgb.Hex(0x8ea4b4), reflect);
            Put(canvas, x, y, color.Lerp(Rgb.Hex(0x6a6458), dust));
        }

        return canvas;
    }
}

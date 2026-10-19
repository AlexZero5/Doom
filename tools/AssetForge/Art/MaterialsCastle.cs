using AssetForge.Core;

namespace AssetForge.Art;

/// <summary>
///     Материалы для замка и деревни: крупный кладочный камень, каменные плиты, дощатый пол,
///     ящики, бочки, сено, знамя, дверь, решётка ворот, сланцевая черепица.
/// </summary>
internal static partial class Materials
{
    /// <summary>
    ///     Мировой размер текстур (метры на один повтор): пишется в assets/textures/textures.txt.
    ///     Обои и панели — на всю высоту комнаты (2.8 м): плинтус внизу, карниз вверху.
    /// </summary>
    public static readonly (string Name, double Width, double Height)[] WorldSizes =
    {
        ("wallpaper_green", 1.4, 2.8), ("wallpaper_beige", 1.4, 2.8), ("wallpaper_blue", 1.4, 2.8),
        ("wood_panel", 1.4, 2.8), ("tiles_wall", 1.4, 2.8), ("plaster_cream", 2, 2),
        ("fence_wood", 1.2, 1.2), ("hedge", 1.5, 1.5), ("stone_wall", 1.5, 1.5), ("concrete", 2, 2),
        ("castle_stone", 2, 2), ("castle_floor", 2, 2), ("roof_tiles", 1.2, 1.2), ("roof_slate", 1.2, 1.2),
        ("grass", 2.5, 2.5), ("dirt", 2.5, 2.5), ("gravel", 1.5, 1.5), ("paving", 2, 2), ("water", 3, 3),
        ("carpet_red", 2, 2), ("ceiling_plaster", 2, 2), ("ceiling_wood", 2, 2), ("wood_planks", 1.2, 1.2),
        ("hay", 1, 1), ("brick_red", 1.2, 1.2), ("brick_yellow", 1.2, 1.2), ("wood_siding", 1.2, 1.2),
    };

    private static IEnumerable<(string Name, Func<Canvas> Build)> Extra()
    {
        yield return ("castle_stone", CastleStone);
        yield return ("castle_floor", CastleFloor);
        yield return ("wood_planks", WoodPlanks);
        yield return ("crate", Crate);
        yield return ("barrel", Barrel);
        yield return ("hay", Hay);
        yield return ("banner_red", () => Banner(Rgb.Hex(0x8c1c18), Rgb.Hex(0xe0b040)));
        yield return ("banner_blue", () => Banner(Rgb.Hex(0x22386c), Rgb.Hex(0xd8d8c8)));
        yield return ("door_leaf", DoorLeaf);
        yield return ("gate_iron", GateIron);
        yield return ("roof_slate", RoofSlate);
    }

    /// <summary>Крепостная кладка: крупные серые блоки в три ряда, тёмные швы, мох у основания.</summary>
    private static Canvas CastleStone()
    {
        Canvas canvas = New();
        Rgb mortar = Rgb.Hex(0x24221e);
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
            Put(canvas, x, y, mortar);

        int[] rows = { 22, 20, 22 };
        int top = 0, index = 0;
        foreach (int height in rows)
        {
            int x = index % 2 == 0 ? 0 : 13;
            int start = x;
            while (x < start + Size)
            {
                int width = 18 + (int)(Noise.Unit(index, top, 301) * 14);
                if (x + width > start + Size) width = start + Size - x;
                Rgb color = Vary(Rgb.Hex(0x7c786e), 302, index, 0.3);
                Block(canvas, x, top, width - 2, height - 2, color, 303 + index, 0.28, 2, 0.8);
                x += width;
                index++;
            }

            top += height;
        }

        // Мох и потёки в швах.
        for (int i = 0; i < 40; i++)
        {
            int x = (int)(Noise.Unit(i, 1, 304) * Size), y = (int)(Noise.Unit(i, 2, 304) * Size);
            Put(canvas, x, y, Rgb.Hex(0x3e4a2a));
        }

        return canvas;
    }

    /// <summary>Каменные плиты двора и залов.</summary>
    private static Canvas CastleFloor()
    {
        Canvas canvas = New();
        Rgb joint = Rgb.Hex(0x2a2824);
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
            Put(canvas, x, y, joint);

        int index = 0;
        foreach ((int x, int y, int w, int h) in new[]
                 {
                     (0, 0, 24, 20), (24, 0, 22, 28), (46, 0, 18, 20), (0, 20, 24, 24), (46, 20, 18, 26),
                     (24, 28, 22, 18), (0, 44, 30, 20), (30, 46, 34, 18)
                 })
            Block(canvas, x, y, w - 1, h - 1, Vary(Rgb.Hex(0x6e6a60), 311, index++, 0.25), 312 + index, 0.24, 1, 0.7);

        return canvas;
    }

    /// <summary>Дощатый пол: шесть досок с волокнами и шляпками гвоздей.</summary>
    private static Canvas WoodPlanks()
    {
        Canvas canvas = New();
        Rgb wood = Rgb.Hex(0x8a6038);
        for (int plank = 0; plank < 6; plank++)
        {
            Rgb tone = Vary(wood, 321, plank, 0.3);
            int top = plank * Size / 6, height = (plank + 1) * Size / 6 - top;
            int joint = (int)(Noise.Unit(plank, 3, 322) * Size);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < Size; x++)
            {
                double factor = 1 + (Noise.Streaks(x, top + y, Size, 323, 1, 20) - 0.5) * 0.55;
                if (y == 0) factor += 0.15;
                if (y == height - 1) factor -= 0.45;
                if (x == joint) factor -= 0.5;
                Put(canvas, x, top + y, tone.Scale(factor));
            }

            Put(canvas, joint + 3, top + 2, Rgb.Hex(0x3a3430));
            Put(canvas, joint - 4, top + height - 3, Rgb.Hex(0x3a3430));
        }

        return canvas;
    }

    /// <summary>Ящик: рамка из досок и косая распорка.</summary>
    private static Canvas Crate()
    {
        Canvas canvas = WoodPlanks();
        Rgb frame = Rgb.Hex(0x6a4424);
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            bool border = x < 7 || y < 7 || x >= Size - 7 || y >= Size - 7;
            bool brace = Math.Abs(x - y) < 5 && !border;
            if (!border && !brace)
                continue;

            double factor = 1 + (Noise.Streaks(x, y, Size, 331, 6, 6) - 0.5) * 0.4;
            if (x == 0 || y == 0 || (brace && x - y == -4)) factor += 0.25;
            if (x == Size - 1 || y == Size - 1 || (brace && x - y == 4)) factor -= 0.35;
            Put(canvas, x, y, frame.Scale(factor));
        }

        foreach ((int x, int y) in new[] { (3, 3), (60, 3), (3, 60), (60, 60) })
            Put(canvas, x, y, Rgb.Hex(0xb0a890));
        return canvas;
    }

    /// <summary>Бочка: вертикальные клёпки и два железных обруча (текстура по окружности).</summary>
    private static Canvas Barrel()
    {
        Canvas canvas = New();
        Rgb wood = Rgb.Hex(0x7a4c26);
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            int stave = x / 8;
            double factor = 1 + (Noise.Streaks(x, y, Size, 341, 16, 1) - 0.5) * 0.4 + (Noise.Unit(stave, 0, 342) - 0.5) * 0.25;
            if (x % 8 == 0) factor -= 0.4;
            double bulge = Math.Sin(y / (double)Size * Math.PI);
            Put(canvas, x, y, wood.Scale(factor * (0.8 + 0.25 * bulge)));
        }

        foreach (int hoop in new[] { 9, 52 })
            for (int y = hoop; y < hoop + 4; y++)
            for (int x = 0; x < Size; x++)
                Put(canvas, x, y, Rgb.Hex(0x3c3a38).Scale(y == hoop ? 1.5 : y == hoop + 3 ? 0.7 : 1.0));

        return canvas;
    }

    /// <summary>Сено: жёлтые соломинки вперемешку.</summary>
    private static Canvas Hay()
    {
        Canvas canvas = New();
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            double streak = Noise.Streaks(x, y, Size, 351, 24, 3, 3);
            Rgb color = Rgb.Hex(0xc8a048).Lerp(Rgb.Hex(0x8a6a28), 1 - streak);
            Put(canvas, x, y, color.Scale(1 + (Noise.Unit(x, y, 352) - 0.5) * 0.25));
        }

        return canvas;
    }

    /// <summary>Знамя: полотнище с каймой и гербом-черепом.</summary>
    private static Canvas Banner(Rgb cloth, Rgb trim)
    {
        Canvas canvas = New();
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            double fold = Math.Sin(x / (double)Size * Math.PI * 4) * 0.12;
            bool edge = x < 4 || x >= Size - 4 || y < 4 || y >= Size - 4;
            Put(canvas, x, y, (edge ? trim : cloth).Scale(1 + fold + Grain(x, y, 361) * 0.2));
        }

        // Череп: круг, глазницы, челюсть.
        for (int y = 18; y < 46; y++)
        for (int x = 18; x < 46; x++)
        {
            double dx = x - 32, dy = y - 28;
            bool skull = dx * dx + dy * dy < 120 || (y >= 36 && y < 44 && Math.Abs(dx) < 7);
            bool eye = (dx + 5) * (dx + 5) + (dy + 1) * (dy + 1) < 10 || (dx - 5) * (dx - 5) + (dy + 1) * (dy + 1) < 10;
            bool teeth = y >= 40 && y < 44 && x % 3 == 0 && Math.Abs(dx) < 7;
            if (skull)
                Put(canvas, x, y, eye || teeth ? cloth.Scale(0.4) : trim.Scale(1 - dy * 0.01));
        }

        return canvas;
    }

    /// <summary>Дверное полотно: вертикальные доски, две поперечины, кованая ручка.</summary>
    private static Canvas DoorLeaf()
    {
        Canvas canvas = New();
        Rgb wood = Rgb.Hex(0x6c4222);
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            double factor = 1 + (Noise.Streaks(x, y, Size, 371, 16, 1) - 0.5) * 0.45;
            if (x % 16 == 0) factor -= 0.45;
            if (x % 16 == 1) factor += 0.15;
            Put(canvas, x, y, wood.Scale(factor));
        }

        foreach (int bar in new[] { 10, 50 })
            for (int y = bar; y < bar + 5; y++)
            for (int x = 2; x < Size - 2; x++)
                Put(canvas, x, y, Rgb.Hex(0x4a2c14).Scale(y == bar ? 1.4 : y == bar + 4 ? 0.6 : 1));

        for (int y = 30; y < 36; y++)
        {
            Put(canvas, 54, y, Rgb.Hex(0x2a2a2a));
            Put(canvas, 55, y, Rgb.Hex(0x5a5a5a));
        }

        return canvas;
    }

    /// <summary>Решётка ворот: кованые прутья, между ними — прозрачно.</summary>
    private static Canvas GateIron()
    {
        var canvas = new Canvas(Size, Size);
        Rgb iron = Rgb.Hex(0x3e3c3a);
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            bool vertical = x % 16 < 4;
            bool horizontal = y % 21 < 3;
            if (!vertical && !horizontal)
                continue;

            double factor = 1 + (vertical && x % 16 == 0 ? 0.35 : 0) - (vertical && x % 16 == 3 ? 0.3 : 0)
                            + (Noise.Unit(x, y, 381) - 0.5) * 0.2;
            canvas[x, y] = iron.Scale(factor).Argb();
        }

        // Острия снизу.
        for (int x = 0; x < Size; x++)
            if (x % 16 < 4)
                canvas[x, Size - 1] = iron.Scale(0.6).Argb();

        return canvas;
    }

    /// <summary>Сланцевая черепица (замковые башни): тёмно-сизые прямоугольные пластины.</summary>
    private static Canvas RoofSlate()
    {
        Canvas canvas = New();
        Rgb slate = Rgb.Hex(0x3e4652);
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
            Put(canvas, x, y, slate.Scale(0.45));

        for (int row = 0; row < 8; row++)
        {
            int shift = row % 2 == 0 ? 0 : 5;
            for (int tile = 0; tile < 7; tile++)
            {
                Rgb tone = Vary(slate, 391, row * 8 + tile, 0.3);
                for (int y = 0; y < 8; y++)
                for (int x = 0; x < 9; x++)
                {
                    double factor = 1.08 - y * 0.05 + (x == 0 ? 0.12 : x == 8 ? -0.3 : 0) + Grain(tile * 9 + x, row * 8 + y, 392) * 0.3;
                    if (y == 7) factor -= 0.4;
                    Put(canvas, tile * 9 + x + shift, row * 8 + y, tone.Scale(factor));
                }
            }
        }

        return canvas;
    }
}

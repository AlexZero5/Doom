using System.Text;
using AssetForge.Core;

namespace AssetForge.Art;

/// <summary>
///     Интерфейс: атлас шрифта, заливки текста, логотип и элементы меню.
/// </summary>
/// <remarks>
///     Атлас шрифта хранит не цвет, а «объём» в оттенках серого: 200 — ровная поверхность буквы,
///     светлее — освещённая фаска, ~25 — обводка, ~8 — тень. Игра умножает заливку на этот
///     объём, поэтому одна и та же буква рисуется любой заливкой: обычной, при наведении, заголовка.
/// </remarks>
internal static class Ui
{
    public const int CellWidth = 13;
    public const int CellHeight = 12;
    private const int AtlasColumns = 16;

    public const int BodyGray = 200;
    private const int OutlineGray = 26;
    private const int ShadowGray = 8;

    /// <summary>Атлас шрифта и описание (код символа и ширина буквы).</summary>
    public static (Canvas Atlas, string Description) FontAtlas()
    {
        List<char> characters = FontGlyphs.Glyphs.Keys.OrderBy(c => c).ToList();
        int rows = (characters.Count + AtlasColumns - 1) / AtlasColumns;
        var atlas = new Canvas(AtlasColumns * CellWidth, rows * CellHeight);
        var description = new StringBuilder();

        description.AppendLine($"cell {CellWidth} {CellHeight}");
        description.AppendLine("origin 1 1");
        description.AppendLine($"height {FontGlyphs.Height}");

        for (int index = 0; index < characters.Count; index++)
        {
            char character = characters[index];
            string[] rowsOfGlyph = FontGlyphs.Glyphs[character];
            Canvas cell = RenderGlyph(rowsOfGlyph);
            atlas.Blit(cell, index % AtlasColumns * CellWidth, index / AtlasColumns * CellHeight);
            description.AppendLine($"{(int)character} {rowsOfGlyph[0].Length}");
        }

        return (atlas, description.ToString());
    }

    /// <summary>Одна буква: тело с фаской, обводка, тень (в серых «объёмах»).</summary>
    private static Canvas RenderGlyph(string[] rows)
    {
        var mask = new Mask(CellWidth, CellHeight);
        for (int y = 0; y < rows.Length; y++)
        for (int x = 0; x < rows[y].Length; x++)
            mask[x + 1, y + 1] = rows[y][x] == '#';

        double[] light = Relief.Light(mask, depth: 1.6, strength: 0.9);
        var cell = new Canvas(CellWidth, CellHeight);

        for (int y = 0; y < CellHeight; y++)
        for (int x = 0; x < CellWidth; x++)
        {
            if (mask[x, y])
            {
                // Нижняя строка буквы чуть темнее — «тяжёлое» основание, как у вырезанных букв.
                double factor = light[y * CellWidth + x] * (y == FontGlyphs.Height ? 0.85 : 1.0);
                int gray = Math.Clamp((int)(BodyGray * factor), 90, 255);
                cell[x, y] = new Rgb(gray, gray, gray).Argb();
            }
        }

        // Обводка — восемь соседей.
        var outline = new Canvas(CellWidth, CellHeight);
        for (int y = 0; y < CellHeight; y++)
        for (int x = 0; x < CellWidth; x++)
        {
            if (mask[x, y]) continue;
            bool near = false;
            for (int dy = -1; dy <= 1 && !near; dy++)
            for (int dx = -1; dx <= 1 && !near; dx++)
                near = mask[x + dx, y + dy];
            if (near) outline[x, y] = new Rgb(OutlineGray, OutlineGray, OutlineGray).Argb();
        }

        cell.Blit(outline, 0, 0);

        // Тень справа-снизу от обводки.
        Mask solid = cell.ToMask();
        for (int y = CellHeight - 1; y >= 1; y--)
        for (int x = CellWidth - 1; x >= 1; x--)
        {
            if (!solid[x, y] && solid[x - 1, y - 1])
                cell[x, y] = new Rgb(ShadowGray, ShadowGray, ShadowGray).Argb();
        }

        return cell;
    }

    /// <summary>
    ///     Заливка текста: строка y соответствует строке ячейки атласа (вертикальный градиент
    ///     по букве), по x — тайлится, с зерном.
    /// </summary>
    public static Canvas Fill(int seed, params Rgb[] stops)
    {
        var fill = new Canvas(32, CellHeight);
        for (int y = 0; y < CellHeight; y++)
        for (int x = 0; x < fill.Width; x++)
        {
            double t = Math.Clamp((y - 1) / (double)(FontGlyphs.Height - 1), 0, 1);
            double grain = (Noise.Streaks(x, y, 32, seed, 8, 4, 2) - 0.5) * 0.22;
            fill[x, y] = Rgb.Ramp(t, stops).Scale(1 + grain).Argb();
        }

        Palette.Apply(fill, dither: 8);
        return fill;
    }

    /// <summary>
    ///     Логотип: буквы шрифта крупно, огненная заливка, объём, толстая обводка и лёгкая
    ///     перспектива (низ шире верха), как у логотипа Doom.
    /// </summary>
    public static Canvas Logo(string text)
    {
        const int scale = 3;
        int width = text.Sum(c => FontGlyphs.Glyphs[c][0].Length + 2) * scale + 12;
        int height = FontGlyphs.Height * scale + 12;

        var mask = new Mask(width, height);
        int cursor = 6;
        foreach (char c in text)
        {
            string[] rows = FontGlyphs.Glyphs[c];
            for (int y = 0; y < rows.Length; y++)
            for (int x = 0; x < rows[y].Length; x++)
            {
                if (rows[y][x] != '#') continue;
                for (int sy = 0; sy < scale; sy++)
                for (int sx = 0; sx < scale; sx++)
                    mask[cursor + x * scale + sx, 6 + y * scale + sy] = true;
            }

            cursor += (rows[0].Length + 2) * scale;
        }

        // Перспектива: верх сжат к центру.
        var warped = new Mask(width, height);
        double center = width / 2.0;
        for (int y = 0; y < height; y++)
        {
            double squeeze = 0.82 + 0.18 * y / height;
            for (int x = 0; x < width; x++)
                warped[x, y] = mask[(int)Math.Round(center + (x - center) / squeeze), y];
        }

        double[] light = Relief.Light(warped, depth: 2.5, strength: 1.2);
        var logo = new Canvas(width, height);
        Rgb[] fire = { Rgb.Hex(0xfff6a8), Rgb.Hex(0xffb030), Rgb.Hex(0xd04010), Rgb.Hex(0x6a1008) };

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (!warped[x, y]) continue;
            double t = Math.Clamp((y - 6) / (double)(FontGlyphs.Height * scale), 0, 1);
            double grain = (Noise.Unit(x / 2, y / 2, 501) - 0.5) * 0.25;
            logo[x, y] = Rgb.Ramp(t, fire).Scale(light[y * width + x] * (1 + grain)).Argb();
        }

        logo.Outline(Rgb.Hex(0x3a0804), diagonals: true);
        logo.Outline(Rgb.Hex(0x0c0202), diagonals: false);
        Palette.Apply(logo, dither: 8);
        return logo.Trim(1);
    }

    // ============================================================
    //   Элементы меню
    // ============================================================

    /// <summary>
    ///     Панель для растяжения «9 частями»: уголки 12×12, края тайлятся, середина 24×24 тайлится.
    ///     Рамка — клёпаный металл, середина — тёмный камень.
    /// </summary>
    public static Canvas Panel()
    {
        const int size = 48, border = 12;
        var panel = new Canvas(size, size);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            bool inner = x >= border && y >= border && x < size - border && y < size - border;
            if (inner)
            {
                double n = Noise.Fbm(x - border, y - border, 24, 511, 4, 3);
                Rgb stone = Rgb.Hex(0x1c1e24).Scale(0.85 + n * 0.45);
                panel[x, y] = stone.Argb();
                continue;
            }

            // Рамка: два «бруска» с фаской и тёмный зазор между рамкой и серединой.
            int fromEdge = Math.Min(Math.Min(x, y), Math.Min(size - 1 - x, size - 1 - y));
            Rgb metal = Rgb.Hex(0x6c5a48);
            double factor = 0.9 + (Noise.Unit(x, y, 512) - 0.5) * 0.18;
            if (fromEdge == 0) factor = 0.35;
            else if (fromEdge == 1) factor += x < size / 2 && y < size / 2 ? 0.45 : 0.25;
            else if (fromEdge == border - 2) factor -= 0.35;
            else if (fromEdge == border - 1) factor = 0.2;
            else if (fromEdge == 6) factor += 0.15;

            panel[x, y] = metal.Scale(factor).Argb();
        }

        // Заклёпки в уголках и на серединах краёв.
        foreach ((int rx, int ry) in new[] { (5, 5), (42, 5), (5, 42), (42, 42), (24, 5), (24, 42), (5, 24), (42, 24) })
        {
            panel[rx, ry] = Rgb.Hex(0xe8d8b0).Argb();
            panel[rx + 1, ry] = Rgb.Hex(0xa08868).Argb();
            panel[rx, ry + 1] = Rgb.Hex(0xa08868).Argb();
            panel[rx + 1, ry + 1] = Rgb.Hex(0x2a2018).Argb();
        }

        Palette.Apply(panel, dither: 6);
        return panel;
    }

    /// <summary>Череп-указатель пункта меню (как в Doom): кадр 1 — глаза горят.</summary>
    public static Canvas Skull(bool eyesLit)
    {
        const int w = 13, h = 14;
        var skull = new Canvas(w, h);
        Mask head = Mask.Ellipse(w, h, 6.5, 5.5, 5.5, 5.2)
            .Union(Mask.RoundRect(w, h, 3, 8, 7, 5, 1.5));
        double[] light = Relief.Light(head, 2.0);
        skull.Paint(head, (x, y) => Rgb.Hex(0xd8ccae).Scale(light[y * w + x]));

        Rgb eye = eyesLit ? Rgb.Hex(0xff3a10) : Rgb.Hex(0x1a0c08);
        skull.Paint(Mask.Ellipse(w, h, 4.2, 6, 1.7, 1.6), eye);
        skull.Paint(Mask.Ellipse(w, h, 8.8, 6, 1.7, 1.6), eye);
        if (eyesLit)
        {
            skull.Plot(4, 6, Rgb.Hex(0xffe060));
            skull.Plot(8, 6, Rgb.Hex(0xffe060));
        }

        skull.Plot(6, 8, Rgb.Hex(0x2a1810));
        skull.Plot(6, 9, Rgb.Hex(0x2a1810));
        for (int x = 4; x <= 8; x += 2)
            skull.Plot(x, 11, Rgb.Hex(0x3a2a1c));

        skull.Outline(Rgb.Hex(0x100806));
        Palette.Apply(skull, dither: 4);
        return skull;
    }

    /// <summary>Жёлоб ползунка (тайлится по горизонтали), 16×7.</summary>
    public static Canvas SliderTrack()
    {
        var track = new Canvas(16, 7);
        for (int y = 0; y < 7; y++)
        for (int x = 0; x < 16; x++)
        {
            Rgb color = y == 0 ? Rgb.Hex(0x0a0a0c) : y == 6 ? Rgb.Hex(0x6a5a48) : Rgb.Hex(0x1a1a20).Scale(0.9 + (y == 1 ? -0.3 : 0));
            if (y is > 1 and < 6 && x % 4 == 0) color = color.Scale(0.7);
            track[x, y] = color.Argb();
        }

        Palette.Apply(track, dither: 2);
        return track;
    }

    /// <summary>Заполнение ползунка (тайлится), 16×5: раскалённый металл.</summary>
    public static Canvas SliderFill()
    {
        var fill = new Canvas(16, 5);
        Rgb[] heat = { Rgb.Hex(0xffe080), Rgb.Hex(0xf07018), Rgb.Hex(0x8c2008) };
        for (int y = 0; y < 5; y++)
        for (int x = 0; x < 16; x++)
            fill[x, y] = Rgb.Ramp(y / 4.0, heat).Scale(x % 4 == 0 ? 0.85 : 1).Argb();

        Palette.Apply(fill, dither: 4);
        return fill;
    }

    /// <summary>Ручка ползунка 7×11: стальной брусок с насечками.</summary>
    public static Canvas SliderKnob()
    {
        var knob = new Canvas(7, 11);
        Mask body = Mask.Rect(7, 11, 0, 0, 7, 11);
        double[] light = Relief.Light(body, 2.0, 1.3);
        knob.Paint(body, (x, y) => Rgb.Hex(0x9aa4b0).Scale(light[y * 7 + x] * (y % 3 == 1 && x is > 1 and < 5 ? 0.7 : 1)));
        Palette.Apply(knob, dither: 2);
        return knob;
    }

    /// <summary>Переключатель 11×11: утопленный квадрат, во включённом — горящая метка.</summary>
    public static Canvas Check(bool on)
    {
        var box = new Canvas(11, 11);
        for (int y = 0; y < 11; y++)
        for (int x = 0; x < 11; x++)
        {
            bool edge = x == 0 || y == 0 || x == 10 || y == 10;
            Rgb color = edge
                ? (x == 0 || y == 0 ? Rgb.Hex(0x0c0c0e) : Rgb.Hex(0x7a6a56))
                : Rgb.Hex(0x16161c);
            box[x, y] = color.Argb();
        }

        if (on)
        {
            Mask mark = Mask.Line(11, 11, 2.5, 5.5, 4.5, 8.0, 2.2).Union(Mask.Line(11, 11, 4.5, 8.0, 8.5, 2.5, 2.2));
            box.Paint(mark, (x, y) => Rgb.Ramp(y / 10.0, Rgb.Hex(0xfff0a0), Rgb.Hex(0xf07018)));
        }

        Palette.Apply(box, dither: 2);
        return box;
    }
}

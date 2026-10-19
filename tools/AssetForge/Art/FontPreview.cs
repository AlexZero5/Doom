using AssetForge.Core;

namespace AssetForge.Art;

/// <summary>Рисует строку атласом шрифта с заливкой — так же, как это делает игра (для превью).</summary>
internal static class FontPreview
{
    public static Canvas Render(Canvas atlas, IReadOnlyList<(string Text, Canvas Fill)> lines)
    {
        List<char> characters = FontGlyphs.Glyphs.Keys.OrderBy(c => c).ToList();
        int width = lines.Max(line => Measure(line.Text)) + 8;
        var canvas = new Canvas(width, lines.Count * (Ui.CellHeight + 2) + 6);
        canvas.FillRect(0, 0, canvas.Width, canvas.Height, Rgb.Hex(0x2a2a30));

        int y = 3;
        foreach ((string text, Canvas fill) in lines)
        {
            int x = 4;
            foreach (char raw in text)
            {
                char c = char.ToUpperInvariant(raw);
                if (!FontGlyphs.Glyphs.TryGetValue(c, out string[]? glyph))
                {
                    x += 4;
                    continue;
                }

                int index = characters.IndexOf(c);
                int ax = index % 16 * Ui.CellWidth, ay = index / 16 * Ui.CellHeight;
                for (int cy = 0; cy < Ui.CellHeight; cy++)
                for (int cx = 0; cx < Ui.CellWidth; cx++)
                {
                    if (!atlas.Opaque(ax + cx, ay + cy)) continue;
                    int gray = Rgb.FromArgb(atlas[ax + cx, ay + cy]).R;
                    Rgb color = gray < 64
                        ? new Rgb(gray / 2, gray / 2, gray / 2)
                        : Rgb.FromArgb(fill[(x + cx) % fill.Width, cy]).Scale(gray / (double)Ui.BodyGray);
                    canvas.Plot(x + cx, y + cy, color);
                }

                x += glyph[0].Length + 2;
            }

            y += Ui.CellHeight + 2;
        }

        return canvas;
    }

    private static int Measure(string text) =>
        text.Sum(c => FontGlyphs.Glyphs.TryGetValue(char.ToUpperInvariant(c), out string[]? g) ? g[0].Length + 2 : 4) + 4;
}

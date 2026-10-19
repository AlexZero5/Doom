using System.Globalization;
using Doom.Assets;

namespace Doom.UI;

/// <summary>
///     Растровый шрифт меню и HUD из картинки ui/font.png (её рисует tools/AssetForge): атлас
///     хранит не цвет, а «объём» буквы в оттенках серого (фаска, обводка, тень), а цвет даёт
///     заливка — отдельная картинка (ui/fill_normal, fill_hover, fill_title…). Поэтому одна и та
///     же буква рисуется в любом состоянии. Описание атласа — ui/font.txt.
/// </summary>
internal sealed class BitmapFont
{
    /// <summary>Серый «объём» поверхности буквы: заливка умножается на gray / BodyGray.</summary>
    private const double BodyGray = 200.0;

    private readonly Texture _atlas;
    private readonly Dictionary<char, (int X, int Y, int Width)> _glyphs = new();
    private readonly int _cellWidth;
    private readonly int _originX;
    private readonly int _originY;

    private BitmapFont(Texture atlas, string description)
    {
        _atlas = atlas;
        int index = 0;
        int columns = 16;

        foreach (string raw in description.Split('\n'))
        {
            string[] parts = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                continue;

            switch (parts[0])
            {
                case "cell":
                    _cellWidth = int.Parse(parts[1], CultureInfo.InvariantCulture);
                    Height = int.Parse(parts[2], CultureInfo.InvariantCulture);
                    columns = Math.Max(1, atlas.Width / _cellWidth);
                    break;
                case "origin":
                    _originX = int.Parse(parts[1], CultureInfo.InvariantCulture);
                    _originY = int.Parse(parts[2], CultureInfo.InvariantCulture);
                    break;
                case "height":
                    break;
                default:
                    if (int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int code)
                        && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int width))
                    {
                        _glyphs[(char)code] = (index % columns * _cellWidth, index / columns * Height, width);
                        index++;
                    }

                    break;
            }
        }
    }

    /// <summary>Высота строки (ячейка атласа) в пикселях шрифта.</summary>
    public int Height { get; } = 12;

    /// <summary>Загружает шрифт из ассетов; null — картинок нет (меню возьмёт встроенный шрифт).</summary>
    public static BitmapFont? Load(AssetStore? assets)
    {
        Texture? atlas = assets?.GetUi("font");
        string? description = assets?.ReadText("ui/font.txt");
        return atlas is null || description is null ? null : new BitmapFont(atlas, description);
    }

    /// <summary>Ширина строки в пикселях шрифта (заглавными: шрифт только заглавный).</summary>
    public int Measure(string text)
    {
        int width = 0;
        foreach (char raw in text)
            width += Advance(char.ToUpperInvariant(raw));
        return Math.Max(0, width + 2);
    }

    private int Advance(char c) => _glyphs.TryGetValue(c, out var glyph) ? glyph.Width + 2 : 5;

    /// <summary>
    ///     Рисует строку в холст (координаты и масштаб — в пикселях холста): буква = объём из атласа ×
    ///     заливка (её строка берётся по высоте буквы, столбец — по экрану, с повтором).
    /// </summary>
    public void Draw(PixelCanvas canvas, int x, int y, string text, Texture fill, int scale)
    {
        int cursor = x;
        foreach (char raw in text)
        {
            char c = char.ToUpperInvariant(raw);
            if (!_glyphs.TryGetValue(c, out var glyph))
            {
                cursor += 5 * scale;
                continue;
            }

            int cellWidth = glyph.Width + _originX * 2 + 1;
            for (int gy = 0; gy < Height; gy++)
            for (int gx = 0; gx < cellWidth && gx < _cellWidth; gx++)
            {
                int argb = _atlas.SamplePixel(glyph.X + gx, glyph.Y + gy);
                if (Texture.IsTransparent(argb))
                    continue;

                int gray = argb & 0xFF;
                int color;
                if (gray < 64)
                {
                    color = (gray / 2) * 0x010101;
                }
                else
                {
                    int fillArgb = fill.SamplePixel(((cursor / scale + gx) % fill.Width + fill.Width) % fill.Width,
                        Math.Min(gy, fill.Height - 1));
                    color = Rendering.Lighting.Shade(fillArgb, gray / BodyGray);
                }

                canvas.Rect(cursor + (gx - _originX) * scale, y + (gy - _originY) * scale, scale, scale, color);
            }

            cursor += (glyph.Width + 2) * scale;
        }
    }
}

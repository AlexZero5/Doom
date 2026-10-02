using Doom.Assets;
using Doom.Rendering;

namespace Doom.UI;

/// <summary>
///     Рисование поверх кадра на уровне «пикселей» игры: один пиксель — это верхняя
///     или нижняя половина символьной ячейки, поэтому вся графика меню масштабируется
///     ровно так же, как сцена (вертикальное разрешение вдвое выше числа строк).
/// </summary>
internal sealed class PixelCanvas
{
    private readonly Framebuffer _framebuffer;

    public PixelCanvas(Framebuffer framebuffer) => _framebuffer = framebuffer;

    /// <summary>Ширина холста в пикселях (равна числу колонок).</summary>
    public int Width => _framebuffer.Columns;

    /// <summary>Высота холста в пикселях (два пикселя на строку ячеек).</summary>
    public int Height => _framebuffer.Rows * 2;

    /// <summary>Рисует один пиксель указанного цвета. Пиксель за пределами кадра игнорируется.</summary>
    public void Pixel(int x, int y, int color)
    {
        if (x < 0 || x >= _framebuffer.Columns || y < 0 || y >= Height)
            return;

        int cell = (y >> 1) * _framebuffer.Columns + x;

        // Верхняя половина ячейки — цвет символа, нижняя — цвет фона.
        if ((y & 1) == 0)
            _framebuffer.Foreground[cell] = color;
        else
            _framebuffer.Background[cell] = color;

        _framebuffer.Chars[cell] = Framebuffer.UpperHalfBlock;
    }

    /// <summary>
    ///     Рисует полупрозрачный пиксель: смешивает цвет с тем, что уже нарисовано
    ///     в этой половине ячейки. Используется для тени под панелью меню.
    /// </summary>
    public void BlendPixel(int x, int y, int color, double alpha)
    {
        if (x < 0 || x >= _framebuffer.Columns || y < 0 || y >= Height)
            return;

        int cell = (y >> 1) * _framebuffer.Columns + x;
        int[] target = (y & 1) == 0 ? _framebuffer.Foreground : _framebuffer.Background;
        target[cell] = ColorRgb.Blend(target[cell], color, alpha);
        _framebuffer.Chars[cell] = Framebuffer.UpperHalfBlock;
    }

    /// <summary>Заливает прямоугольник сплошным цветом.</summary>
    public void Rect(int x, int y, int width, int height, int color)
    {
        for (int row = y; row < y + height; row++)
        {
            for (int column = x; column < x + width; column++)
                Pixel(column, row, color);
        }
    }

    /// <summary>Прямоугольная рамка толщиной в один пиксель.</summary>
    public void FrameRect(int x, int y, int width, int height, int color)
    {
        Rect(x, y, width, 1, color);
        Rect(x, y + height - 1, width, 1, color);
        Rect(x, y, 1, height, color);
        Rect(x + width - 1, y, 1, height, color);
    }

    /// <summary>Рисует строку пиксельным шрифтом. Неизвестные символы пропускаются.</summary>
    public void Text(int x, int y, string text, int color) => TextScaled(x, y, text, color, 1);

    /// <summary>Рисует строку шрифтом с увеличением <paramref name="scale" /> (см. <see cref="TextScaled" />).</summary>
    public void Text(int x, int y, string text, int color, int scale) => TextScaled(x, y, text, color, scale);

    /// <summary>Рисует строку, увеличив каждый «пиксель» глифа в <paramref name="scale" /> раз.</summary>
    public void TextScaled(int x, int y, string text, int color, int scale)
    {
        for (int index = 0; index < text.Length; index++)
        {
            string[]? glyph = PixelFont.Get(text[index]);
            if (glyph is null)
                continue;

            int glyphX = x + index * PixelFont.Advance * scale;

            for (int row = 0; row < PixelFont.GlyphHeight; row++)
            {
                string line = glyph[row];

                for (int column = 0; column < PixelFont.GlyphWidth; column++)
                {
                    if (line[column] != '#')
                        continue;

                    int pixelX = glyphX + column * scale;
                    int pixelY = y + row * scale;

                    for (int dy = 0; dy < scale; dy++)
                    {
                        for (int dx = 0; dx < scale; dx++)
                            Pixel(pixelX + dx, pixelY + dy, color);
                    }
                }
            }
        }
    }

    /// <summary>Ширина строки при отрисовке с увеличением <paramref name="scale" />.</summary>
    public static int Measure(string text, int scale = 1) => PixelFont.Measure(text) * scale;

    /// <summary>
    ///     Рисует строку, заливая «пиксели» глифов текстурой (замощение по экранным
    ///     координатам): обычное и hover-состояния шрифта меню. Прозрачные пиксели
    ///     текстуры заменяются <paramref name="fallbackColor" />.
    /// </summary>
    public void TextFilled(int x, int y, string text, Texture fill, int fallbackColor, int scale = 1)
    {
        for (int index = 0; index < text.Length; index++)
        {
            string[]? glyph = PixelFont.Get(text[index]);
            if (glyph is null)
                continue;

            int glyphX = x + index * PixelFont.Advance * scale;

            for (int row = 0; row < PixelFont.GlyphHeight; row++)
            {
                string line = glyph[row];

                for (int column = 0; column < PixelFont.GlyphWidth; column++)
                {
                    if (line[column] != '#')
                        continue;

                    for (int dy = 0; dy < scale; dy++)
                    {
                        for (int dx = 0; dx < scale; dx++)
                        {
                            int pixelX = glyphX + column * scale + dx;
                            int pixelY = y + row * scale + dy;

                            int argb = fill.SamplePixel(
                                pixelX % fill.Width,
                                pixelY % fill.Height);

                            Pixel(pixelX, pixelY,
                                Texture.IsTransparent(argb) ? fallbackColor : Texture.RgbOf(argb));
                        }
                    }
                }
            }
        }
    }
}

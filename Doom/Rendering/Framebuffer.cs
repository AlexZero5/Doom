namespace Doom.Rendering;

/// <summary>
///     Кадровый буфер: символ ячейки и два цвета. Цвет символа описывает верхний
///     «пиксель» ячейки, цвет фона — нижний, поэтому вертикальное разрешение вдвое выше.
/// </summary>
internal sealed class Framebuffer
{
    public const char UpperHalfBlock = '▀';
    public const char Empty = ' ';

    public Framebuffer(int columns, int rows)
    {
        Resize(columns, rows);
    }

    public int Columns { get; private set; }

    public int Rows { get; private set; }

    public char[] Chars { get; private set; } = Array.Empty<char>();

    public int[] Foreground { get; private set; } = Array.Empty<int>();

    public int[] Background { get; private set; } = Array.Empty<int>();

    public void Resize(int columns, int rows)
    {
        Columns = columns;
        Rows = rows;

        int cells = columns * rows;
        Chars = new char[cells];
        Foreground = new int[cells];
        Background = new int[cells];
    }

    public void Set(int x, int y, char value, int foreground, int background)
    {
        int index = y * Columns + x;
        Chars[index] = value;
        Foreground[index] = foreground;
        Background[index] = background;
    }

    public void FillRow(int y, char value, int foreground, int background)
    {
        int rowBase = y * Columns;
        for (int x = 0; x < Columns; x++)
        {
            int index = rowBase + x;
            Chars[index] = value;
            Foreground[index] = foreground;
            Background[index] = background;
        }
    }

    /// <summary>Заливает прямоугольник сплошным цветом. Последняя строка (HUD) не затрагивается.</summary>
    public void PaintRect(int x0, int y0, int width, int height, int color)
    {
        int lastRow = Rows - 1;

        for (int y = y0; y < y0 + height; y++)
        {
            if (y < 0 || y >= lastRow)
                continue;

            int rowBase = y * Columns;

            for (int x = x0; x < x0 + width; x++)
            {
                if (x < 0 || x >= Columns)
                    continue;

                int index = rowBase + x;
                Chars[index] = UpperHalfBlock;
                Foreground[index] = color;
                Background[index] = color;
            }
        }
    }
}

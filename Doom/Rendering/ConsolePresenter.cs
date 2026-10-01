using Doom.Platform;

namespace Doom.Rendering;

/// <summary>
///     Отправляет кадр в терминал одним пакетом: переход в начало экрана, смена цветов
///     только при изменении и символ «▀» на каждую ячейку.
/// </summary>
internal sealed class ConsolePresenter
{
    private const int BytesPerCell = 50;
    private const int ReservedBytes = 16384;

    private const byte ForegroundSelector = (byte)'3';
    private const byte BackgroundSelector = (byte)'4';

    private readonly Terminal _terminal;
    private byte[] _buffer = Array.Empty<byte>();
    private int _position;

    public ConsolePresenter(Terminal terminal) => _terminal = terminal;

    /// <summary>Готовит буфер под кадр указанного размера (число ячеек).</summary>
    public void EnsureCapacity(int cells)
    {
        int required = cells * BytesPerCell + ReservedBytes;
        if (_buffer.Length < required)
            _buffer = new byte[required];
    }

    public void Present(Framebuffer framebuffer)
    {
        _position = 0;

        _buffer[_position++] = 0x1B;
        _buffer[_position++] = (byte)'[';
        _buffer[_position++] = (byte)'H';

        int lastForeground = -1;
        int lastBackground = -1;

        int columns = framebuffer.Columns;
        int rows = framebuffer.Rows;
        int[] foreground = framebuffer.Foreground;
        int[] background = framebuffer.Background;

        for (int y = 0; y < rows; y++)
        {
            int rowBase = y * columns;

            for (int x = 0; x < columns; x++)
            {
                int index = rowBase + x;
                int cellForeground = foreground[index];
                int cellBackground = background[index];

                if (cellForeground != lastForeground)
                {
                    EmitColor(ForegroundSelector, cellForeground);
                    lastForeground = cellForeground;
                }

                if (cellBackground != lastBackground)
                {
                    EmitColor(BackgroundSelector, cellBackground);
                    lastBackground = cellBackground;
                }

                // В исходной версии символы кадра (буквы строки состояния) не отправлялись,
                // поэтому текст статуса был невидим. Теперь символ ячейки берётся из буфера,
                // а «▀» используется для всех ячеек игрового поля (так же, как раньше).
                char cell = framebuffer.Chars[index];
                EmitChar(cell);
            }

            if (y < rows - 1)
            {
                _buffer[_position++] = (byte)'\r';
                _buffer[_position++] = (byte)'\n';
            }
        }

        _buffer[_position++] = 0x1B;
        _buffer[_position++] = (byte)'[';
        _buffer[_position++] = (byte)'0';
        _buffer[_position++] = (byte)'m';

        _terminal.Write(_buffer, _position);
    }

    /// <summary>Пишет «\x1b[38;2;r;g;bm» (символ) или «\x1b[48;2;r;g;bm» (фон).</summary>
    private void EmitColor(byte selector, int color)
    {
        _buffer[_position++] = 0x1B;
        _buffer[_position++] = (byte)'[';
        _buffer[_position++] = selector;
        _buffer[_position++] = (byte)'8';
        _buffer[_position++] = (byte)';';
        _buffer[_position++] = (byte)'2';
        _buffer[_position++] = (byte)';';
        EmitInt(ColorRgb.Red(color));
        _buffer[_position++] = (byte)';';
        EmitInt(ColorRgb.Green(color));
        _buffer[_position++] = (byte)';';
        EmitInt(ColorRgb.Blue(color));
        _buffer[_position++] = (byte)'m';
    }

    /// <summary>
    ///     Кодирует символ ячейки в UTF-8: ASCII, кириллица меню и рамки выводятся одинаково
    ///     (раньше все не-ASCII символы заменялись на «▀»).
    /// </summary>
    private void EmitChar(char value)
    {
        if (value < 0x80)
        {
            _buffer[_position++] = (byte)value;
            return;
        }

        if (value < 0x800)
        {
            _buffer[_position++] = (byte)(0xC0 | (value >> 6));
            _buffer[_position++] = (byte)(0x80 | (value & 0x3F));
            return;
        }

        _buffer[_position++] = (byte)(0xE0 | (value >> 12));
        _buffer[_position++] = (byte)(0x80 | ((value >> 6) & 0x3F));
        _buffer[_position++] = (byte)(0x80 | (value & 0x3F));
    }

    private void EmitInt(int value)
    {
        if (value >= 100)
        {
            _buffer[_position++] = (byte)('0' + value / 100);
            value %= 100;
            _buffer[_position++] = (byte)('0' + value / 10);
            _buffer[_position++] = (byte)('0' + value % 10);
        }
        else if (value >= 10)
        {
            _buffer[_position++] = (byte)('0' + value / 10);
            _buffer[_position++] = (byte)('0' + value % 10);
        }
        else
        {
            _buffer[_position++] = (byte)('0' + value);
        }
    }
}

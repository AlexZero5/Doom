using Doom.Platform;

namespace Doom.Rendering;

/// <summary>
///     Отправляет кадр в терминал. Два режима:
///     полный — как раньше: переход в начало экрана, смена цветов только при изменении,
///     символ «▀» на каждую ячейку; и дифф — позиционируем курсор (ESC[строка;колонкаH)
///     только на изменившиеся с прошлого кадра ячейки и отправляем их. Небо, пол и
///     статичные области при поворотах камеры не меняются, поэтому дифф срезает
///     основной объём потока — именно он и узкое место Windows Terminal.
///     Состояние цветов SGR в терминале сохраняется между кадрами и отслеживается.
/// </summary>
internal sealed class ConsolePresenter
{
    private const int BytesPerCell = 50;
    private const int ReservedBytes = 16384;

    /// <summary>Раз в сколько кадров слать полный кадр — страховка от рассинхрона с терминалом.</summary>
    private const int FullRepaintInterval = 120;

    // Синхронизированные обновления (DEC 2026): кадр между BSU и ESU терминал
    // рисует атомарно — без разрывов и мерцания. Дублируем обе формы (DECSET и $t):
    // Windows Terminal понимает первую, kitty — вторую, незнающие терминалы игнорируют.
    private static readonly byte[] SyncBegin =
    {
        0x1B, (byte)'[', (byte)'?', (byte)'2', (byte)'0', (byte)'2', (byte)'6', (byte)'h',
        0x1B, (byte)'[', (byte)'?', (byte)'2', (byte)'0', (byte)'2', (byte)'6', (byte)';',
        (byte)'1', (byte)'$', (byte)'t'
    };

    private static readonly byte[] SyncEnd =
    {
        0x1B, (byte)'[', (byte)'?', (byte)'2', (byte)'0', (byte)'2', (byte)'6', (byte)';',
        (byte)'2', (byte)'$', (byte)'t',
        0x1B, (byte)'[', (byte)'?', (byte)'2', (byte)'0', (byte)'2', (byte)'6', (byte)'l'
    };

    private const byte ForegroundSelector = (byte)'3';
    private const byte BackgroundSelector = (byte)'4';

    private readonly Terminal _terminal;
    private byte[] _buffer = Array.Empty<byte>();
    private int _position;

    // Снимок последнего отправленного кадра: по нему считается дифф.
    private int[] _sentForeground = Array.Empty<int>();
    private int[] _sentBackground = Array.Empty<int>();
    private char[] _sentChars = Array.Empty<char>();
    private int _sentCells;
    private bool _hasSnapshot;
    private int _framesSinceFull;

    // Текущее состояние цветов терминала (-1 — неизвестно, надо отправить).
    private int _lastForeground = -1;
    private int _lastBackground = -1;

    public ConsolePresenter(Terminal terminal) => _terminal = terminal;

    /// <summary>
    ///     Включает продвинутый вывод (дифф + синхронизированные обновления). Он рассчитан
    ///     на Windows Terminal: классическая консоль не знает DEC 2026 и капризничает на
    ///     диффах — там включается «старый добрый» полный кадр, который работает чисто.
    /// </summary>
    public ConsolePresenter(Terminal terminal, bool enableDiffAndSync) : this(terminal)
    {
        _diffEnabled = enableDiffAndSync;
    }

    private bool _diffEnabled = true;

    /// <summary>
    ///     Переключает продвинутый вывод (дифф + синхронизированные обновления) после того,
    ///     как игра точно узнала, кто её показывает: переменной <c>WT_SESSION</c> для этого
    ///     мало — при делегировании консоли терминалу она не выставляется, и Windows Terminal
    ///     принимался за классическую консоль. Снимок экрана сбрасывается: первый кадр в новом
    ///     режиме должен быть полным.
    /// </summary>
    public void UseSynchronizedOutput(bool enabled)
    {
        if (_diffEnabled == enabled)
            return;

        _diffEnabled = enabled;
        Reset();
    }

    /// <summary>Готовит буфер под кадр указанного размера (число ячеек).</summary>
    public void EnsureCapacity(int cells)
    {
        int required = cells * BytesPerCell + ReservedBytes;
        if (_buffer.Length < required)
            _buffer = new byte[required];

        if (_sentCells < cells)
        {
            _sentForeground = new int[cells];
            _sentBackground = new int[cells];
            _sentChars = new char[cells];
            _sentCells = cells;
        }

        // Размер кадра мог измениться — старый снимок больше не соответствует экрану.
        _hasSnapshot = false;
    }

    /// <summary>
    ///     Забывает снимок экрана (после ClearScreen или смены размера): следующий
    ///     кадр будет отправлен целиком.
    /// </summary>
    public void Reset()
    {
        _hasSnapshot = false;
        _lastForeground = -1;
        _lastBackground = -1;
    }

    public void Present(Framebuffer framebuffer)
    {
        int length = BuildPacket(framebuffer);
        _terminal.Write(_buffer, length);
    }

    /// <summary>
    ///     Собирает пакет байтов кадра (полный или дифф) в _buffer и возвращает его длину.
    ///     internal: проверяется симулятором терминала в тестовом прогоне.
    /// </summary>
    internal int BuildPacket(Framebuffer framebuffer)
    {
        _position = 0;

        if (!_diffEnabled)
        {
            // Классическая консоль: полный кадр тем же сплошным потоком, как было
            // изначально — этот путь в conhost работает без артефактов.
            PresentFull(framebuffer);
            return _position;
        }

        // Синхронизированное обновление: терминал применит кадр атомарно,
        // не показывая промежуточное состояние потока (разрывы, «гребёнку»).
        EmitRaw(SyncBegin);

        bool fullPaint = !_hasSnapshot || _framesSinceFull >= FullRepaintInterval;
        if (fullPaint)
        {
            PresentFull(framebuffer);
            _framesSinceFull = 0;
        }
        else
        {
            PresentDiff(framebuffer);
            _framesSinceFull++;
        }

        EmitRaw(SyncEnd);
        return _position;
    }

    private void EmitRaw(byte[] bytes)
    {
        Array.Copy(bytes, 0, _buffer, _position, bytes.Length);
        _position += bytes.Length;
    }

    /// <summary>Доступ к собранному пакету — для симулятора терминала в тестах.</summary>
    internal ReadOnlySpan<byte> TestBuffer(int length) => _buffer.AsSpan(0, length);

    /// <summary>Копирует снимок отправленного кадра — инвариант «терминал == снимок» проверяется тестами.</summary>
    internal void CopySnapshot(int[] foreground, int[] background, char[] chars)
    {
        Array.Copy(_sentForeground, foreground, _sentCells);
        Array.Copy(_sentBackground, background, _sentCells);
        Array.Copy(_sentChars, chars, _sentCells);
    }

    // ============================================================
    //   Полный кадр
    // ============================================================

    private void PresentFull(Framebuffer framebuffer)
    {
        _buffer[_position++] = 0x1B;
        _buffer[_position++] = (byte)'[';
        _buffer[_position++] = (byte)'H';

        int columns = framebuffer.Columns;
        int rows = framebuffer.Rows;
        int[] foreground = framebuffer.Foreground;
        int[] background = framebuffer.Background;
        char[] chars = framebuffer.Chars;

        for (int y = 0; y < rows; y++)
        {
            int rowBase = y * columns;

            for (int x = 0; x < columns; x++)
            {
                int index = rowBase + x;
                int cellForeground = foreground[index];
                int cellBackground = background[index];

                if (cellForeground != _lastForeground)
                {
                    EmitColor(ForegroundSelector, cellForeground);
                    _lastForeground = cellForeground;
                }

                if (cellBackground != _lastBackground)
                {
                    EmitColor(BackgroundSelector, cellBackground);
                    _lastBackground = cellBackground;
                }

                EmitChar(chars[index]);
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

        // После сброса цветов терминал в дефолтном состоянии — помечаем как неизвестное.
        _lastForeground = -1;
        _lastBackground = -1;

        int cells = columns * rows;
        framebuffer.Foreground.AsSpan(0, cells).CopyTo(_sentForeground);
        framebuffer.Background.AsSpan(0, cells).CopyTo(_sentBackground);
        framebuffer.Chars.AsSpan(0, cells).CopyTo(_sentChars);
        _hasSnapshot = true;
    }

    // ============================================================
    //   Дифф: только изменившиеся ячейки
    // ============================================================

    /// <summary>Доля изменений, выше которой кадр считается тяжёлым (поворот камеры, стена вплотную).</summary>
    private const int DiffChangeDenominator = 10;

    private const int DiffChangeThreshold = 3;

    /// <summary>Зазор между изменёнными ячейками, который дешевле перекрыть, чем прыгать курсором.</summary>
    private const int MaxMergeGap = 3;

    private void PresentDiff(Framebuffer framebuffer)
    {
        int columns = framebuffer.Columns;
        int rows = framebuffer.Rows;
        int cells = columns * rows;
        int[] foreground = framebuffer.Foreground;
        int[] background = framebuffer.Background;
        char[] chars = framebuffer.Chars;

        // Быстрая предпроверка: если изменилась большая часть кадра (поворот камеры,
        // стена вплотную, затемнение меню), поячеечный дифф с прыжками курсора дороже
        // сплошного вывода — шлём полный кадр (внутри синхронизированного обновления
        // он применяется атомарно, без разрывов).
        int changed = 0;
        for (int index = 0; index < cells; index++)
        {
            if (!IsSame(foreground, background, chars, index))
                changed++;
        }

        if (changed > cells * DiffChangeThreshold / DiffChangeDenominator)
        {
            PresentFull(framebuffer);
            _framesSinceFull = 0;
            return;
        }

        for (int y = 0; y < rows; y++)
        {
            int rowBase = y * columns;
            int x = 0;

            while (x < columns)
            {
                // Пропускаем неизменившиеся ячейки.
                while (x < columns && IsSame(foreground, background, chars, rowBase + x))
                    x++;

                if (x >= columns)
                    break;

                // Диапазон изменённых ячеек; малые зазоры перекрываем целиком —
                // это дешевле отдельного позиционирования курсора.
                int runStart = x;
                int lastChanged = x;
                int gap = 0;
                int scan = x + 1;

                while (scan < columns)
                {
                    if (!IsSame(foreground, background, chars, rowBase + scan))
                    {
                        lastChanged = scan;
                        gap = 0;
                    }
                    else if (++gap > MaxMergeGap)
                    {
                        break;
                    }

                    scan++;
                }

                EmitCursorPosition(y, runStart);

                for (int cell = runStart; cell <= lastChanged; cell++)
                {
                    int index = rowBase + cell;
                    int cellForeground = foreground[index];
                    int cellBackground = background[index];

                    if (cellForeground != _lastForeground)
                    {
                        EmitColor(ForegroundSelector, cellForeground);
                        _lastForeground = cellForeground;
                    }

                    if (cellBackground != _lastBackground)
                    {
                        EmitColor(BackgroundSelector, cellBackground);
                        _lastBackground = cellBackground;
                    }

                    EmitChar(chars[index]);

                    _sentForeground[index] = cellForeground;
                    _sentBackground[index] = cellBackground;
                    _sentChars[index] = chars[index];
                }

                x = lastChanged + 1;
            }
        }
    }

    private bool IsSame(int[] foreground, int[] background, char[] chars, int index) =>
        _sentChars[index] == chars[index]
        && _sentForeground[index] == foreground[index]
        && _sentBackground[index] == background[index];

    /// <summary>Пишет «ESC[строка;колонкаH» (координаты терминала начинаются с 1).</summary>
    private void EmitCursorPosition(int row, int column)
    {
        _buffer[_position++] = 0x1B;
        _buffer[_position++] = (byte)'[';
        EmitInt(row + 1);
        _buffer[_position++] = (byte)';';
        EmitInt(column + 1);
        _buffer[_position++] = (byte)'H';
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

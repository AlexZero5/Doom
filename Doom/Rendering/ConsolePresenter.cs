using Doom.Platform;

namespace Doom.Rendering;

/// <summary>
///     Превращает кадр в поток байтов для терминала. Узкое место всей игры — не рендер
///     (он занимает пару миллисекунд), а пропускная способность терминала: каждый байт
///     нужно разобрать и нарисовать. Поэтому здесь всё подчинено экономии байтов:
///     <list type="bullet">
///         <item>цвет меняется только при отличии от текущего, а смена обоих цветов
///         ячейки идёт одной последовательностью <c>ESC[38;2;…;48;2;…m</c>;</item>
///         <item>для ячейки «полублока» выбирается тот из равноценных глифов (<c>▀</c>,
///         <c>▄</c>, <c>█</c>, пробел), которому нужно меньше всего смен цвета: однотонная
///         ячейка — это пробел с цветом фона, без цвета символа;</item>
///         <item>в режиме диффа отправляются только изменившиеся ячейки, а промежутки
///         перепрыгиваются короткой командой <c>ESC[nC</c>.</item>
///     </list>
///     Состояние цветов и курсора терминала отслеживается между кадрами.
/// </summary>
internal sealed class ConsolePresenter
{
    private const char UpperHalf = Framebuffer.UpperHalfBlock;
    private const char LowerHalf = '▄';
    private const char FullBlock = '█';

    /// <summary>Худший случай на ячейку: две смены цвета (по 19 байт) и символ.</summary>
    private const int BytesPerCell = 48;

    private const int ReservedBytes = 16384;

    /// <summary>Раз в сколько кадров слать полный кадр — страховка от рассинхрона с терминалом.</summary>
    private const int FullRepaintInterval = 600;

    /// <summary>
    ///     Доля изменившихся ячеек (в процентах), выше которой полный кадр не дороже диффа:
    ///     прыжки курсора между короткими участками перестают окупаться.
    /// </summary>
    private const int FullFrameChangePercent = 80;

    // Синхронизированные обновления (DEC 2026): кадр между BSU и ESU терминал
    // рисует атомарно — без разрывов и мерцания. Дублируем обе формы (DECSET и $t):
    // Windows Terminal понимает первую, kitty — вторую, незнающие терминалы игнорируют.
    private static readonly byte[] SyncBegin = "\e[?2026h\e[?2026;1$t"u8.ToArray();
    private static readonly byte[] SyncEnd = "\e[?2026;2$t\e[?2026l"u8.ToArray();

    private readonly Terminal _terminal;
    private byte[] _buffer = Array.Empty<byte>();
    private int _position;

    // Снимок последнего отправленного кадра (логические цвета верхней и нижней
    // половин ячейки и символ): по нему считается дифф.
    private int[] _sentTop = Array.Empty<int>();
    private int[] _sentBottom = Array.Empty<int>();
    private char[] _sentChars = Array.Empty<char>();
    private int _sentCells;
    private bool _hasSnapshot;
    private int _framesSinceFull;

    // Текущее состояние терминала (-1 — неизвестно).
    private int _lastForeground = -1;
    private int _lastBackground = -1;
    private int _cursorRow = -1;
    private int _cursorColumn = -1;

    private bool _diffEnabled;

    /// <summary>
    ///     <paramref name="enableDiffAndSync" /> включает продвинутый вывод (дифф +
    ///     синхронизированные обновления + «перевёрнутые» полублоки). Он рассчитан на
    ///     Windows Terminal: классическая консоль не знает DEC 2026 и капризничает на
    ///     диффах — там идёт полный кадр, который работает чисто.
    /// </summary>
    public ConsolePresenter(Terminal terminal, bool enableDiffAndSync = true)
    {
        _terminal = terminal;
        _diffEnabled = enableDiffAndSync;
    }

    /// <summary>
    ///     Переключает продвинутый вывод после того, как игра точно узнала, кто её
    ///     показывает (переменной <c>WT_SESSION</c> для этого мало). Снимок экрана
    ///     сбрасывается: первый кадр в новом режиме должен быть полным.
    /// </summary>
    public void UseSynchronizedOutput(bool enabled)
    {
        if (_diffEnabled == enabled)
            return;

        _diffEnabled = enabled;
        Reset();
    }

    /// <summary>Готовит буферы под кадр указанного размера (число ячеек).</summary>
    public void EnsureCapacity(int cells)
    {
        int required = cells * BytesPerCell + ReservedBytes;
        if (_buffer.Length < required)
            _buffer = new byte[required];

        if (_sentCells < cells)
        {
            _sentTop = new int[cells];
            _sentBottom = new int[cells];
            _sentChars = new char[cells];
            _sentCells = cells;
        }

        // Размер кадра мог измениться — старый снимок больше не соответствует экрану.
        Reset();
    }

    /// <summary>
    ///     Забывает снимок экрана и состояние терминала (после ClearScreen, смены размера
    ///     или режима): следующий кадр будет отправлен целиком.
    /// </summary>
    public void Reset()
    {
        _hasSnapshot = false;
        _lastForeground = -1;
        _lastBackground = -1;
        _cursorRow = -1;
        _cursorColumn = -1;
    }

    public void Present(Framebuffer framebuffer)
    {
        int length = BuildPacket(framebuffer);
        _terminal.Write(_buffer, length);
    }

    /// <summary>
    ///     Собирает пакет байтов кадра (полный или дифф) в буфер и возвращает его длину.
    ///     internal: проверяется симулятором терминала в тестовом прогоне.
    /// </summary>
    internal int BuildPacket(Framebuffer framebuffer)
    {
        EnsureBufferFor(framebuffer.Columns * framebuffer.Rows);
        _position = 0;

        if (!_diffEnabled)
        {
            // Классическая консоль: полный кадр сплошным потоком — этот путь
            // в conhost работает без артефактов.
            PresentFull(framebuffer, allowFlippedBlocks: false);
            return _position;
        }

        // Синхронизированное обновление: терминал применит кадр атомарно,
        // не показывая промежуточное состояние потока (разрывы, «гребёнку»).
        EmitRaw(SyncBegin);

        if (!_hasSnapshot || _framesSinceFull >= FullRepaintInterval || !TryPresentDiff(framebuffer))
        {
            PresentFull(framebuffer, allowFlippedBlocks: true);
            _framesSinceFull = 0;
        }
        else
        {
            _framesSinceFull++;
        }

        EmitRaw(SyncEnd);
        return _position;
    }

    /// <summary>Доступ к собранному пакету — для симулятора терминала в тестах.</summary>
    internal ReadOnlySpan<byte> TestBuffer(int length) => _buffer.AsSpan(0, length);

    private void EnsureBufferFor(int cells)
    {
        if (_buffer.Length < cells * BytesPerCell + ReservedBytes || _sentCells < cells)
            EnsureCapacity(cells);
    }

    private void EmitRaw(byte[] bytes)
    {
        bytes.CopyTo(_buffer, _position);
        _position += bytes.Length;
    }

    // ============================================================
    //   Полный кадр
    // ============================================================

    private void PresentFull(Framebuffer framebuffer, bool allowFlippedBlocks)
    {
        // Полный кадр начинается с чистого листа: сбрасываем цвета, чтобы состояние
        // терминала было точно известно (его мог поменять чужой вывод).
        EmitAscii("\e[0m\e[H");
        _lastForeground = -1;
        _lastBackground = -1;

        int columns = framebuffer.Columns;
        int rows = framebuffer.Rows;

        for (int y = 0; y < rows; y++)
        {
            int rowBase = y * columns;

            for (int x = 0; x < columns; x++)
                EmitCell(framebuffer, rowBase + x, allowFlippedBlocks);

            if (y < rows - 1)
            {
                _buffer[_position++] = (byte)'\r';
                _buffer[_position++] = (byte)'\n';
            }
        }

        // Курсор стоит в последней строке (автоперенос выключен) — где именно,
        // точно не известно: первый дифф спозиционирует его явно.
        _cursorRow = -1;
        _cursorColumn = -1;

        int cells = columns * rows;
        framebuffer.Foreground.AsSpan(0, cells).CopyTo(_sentTop);
        framebuffer.Background.AsSpan(0, cells).CopyTo(_sentBottom);
        framebuffer.Chars.AsSpan(0, cells).CopyTo(_sentChars);
        _hasSnapshot = true;
    }

    // ============================================================
    //   Дифф: только изменившиеся ячейки
    // ============================================================

    /// <summary>Отправляет только изменённые ячейки. false — изменилось почти всё, нужен полный кадр.</summary>
    private bool TryPresentDiff(Framebuffer framebuffer)
    {
        int columns = framebuffer.Columns;
        int rows = framebuffer.Rows;
        int cells = columns * rows;
        int[] top = framebuffer.Foreground;
        int[] bottom = framebuffer.Background;
        char[] chars = framebuffer.Chars;

        int changed = 0;
        for (int index = 0; index < cells; index++)
        {
            if (top[index] != _sentTop[index] || bottom[index] != _sentBottom[index] || chars[index] != _sentChars[index])
                changed++;
        }

        if (changed == 0)
            return true;

        if (changed * 100L > cells * (long)FullFrameChangePercent)
            return false;

        for (int y = 0; y < rows; y++)
        {
            int rowBase = y * columns;

            for (int x = 0; x < columns; x++)
            {
                int index = rowBase + x;
                if (top[index] == _sentTop[index] && bottom[index] == _sentBottom[index] && chars[index] == _sentChars[index])
                    continue;

                MoveCursor(y, x);
                EmitCell(framebuffer, index, allowFlippedBlocks: true);

                _sentTop[index] = top[index];
                _sentBottom[index] = bottom[index];
                _sentChars[index] = chars[index];

                // Без автопереноса курсор в последней колонке остаётся на месте.
                _cursorColumn = x + 1 < columns ? x + 1 : -1;
            }
        }

        return true;
    }

    /// <summary>
    ///     Ставит курсор в (row, column) самой короткой командой: в той же строке —
    ///     сдвиг вправо <c>ESC[nC</c>, иначе абсолютная позиция <c>ESC[r;cH</c>.
    /// </summary>
    private void MoveCursor(int row, int column)
    {
        if (row == _cursorRow && _cursorColumn >= 0)
        {
            int gap = column - _cursorColumn;
            if (gap == 0)
                return;

            if (gap > 0)
            {
                _buffer[_position++] = 0x1B;
                _buffer[_position++] = (byte)'[';
                if (gap > 1)
                    EmitInt(gap);
                _buffer[_position++] = (byte)'C';
                _cursorColumn = column;
                return;
            }
        }

        _buffer[_position++] = 0x1B;
        _buffer[_position++] = (byte)'[';
        EmitInt(row + 1);
        _buffer[_position++] = (byte)';';
        EmitInt(column + 1);
        _buffer[_position++] = (byte)'H';
        _cursorRow = row;
        _cursorColumn = column;
    }

    // ============================================================
    //   Ячейка: выбор глифа и цветов
    // ============================================================

    /// <summary>
    ///     Выводит ячейку. «Пиксельная» ячейка (▀) описывается двумя логическими цветами —
    ///     верхним и нижним, — а на экран её можно вывести несколькими равноценными
    ///     способами; выбирается тот, что требует меньше смен цвета терминала.
    /// </summary>
    private void EmitCell(Framebuffer framebuffer, int index, bool allowFlippedBlocks)
    {
        char value = framebuffer.Chars[index];
        int top = framebuffer.Foreground[index];
        int bottom = framebuffer.Background[index];

        if (value == UpperHalf)
        {
            if (top == bottom)
            {
                // Однотонная ячейка: пробел цветом фона (1 байт) или, если уже стоит
                // нужный цвет символа, сплошной блок — тогда смена цвета не нужна вовсе.
                if (_lastBackground == top || !allowFlippedBlocks || _lastForeground != top)
                {
                    SetColors(_lastForeground, top);
                    _buffer[_position++] = (byte)' ';
                }
                else
                {
                    EmitChar(FullBlock);
                }

                return;
            }

            if (allowFlippedBlocks)
            {
                int straight = (_lastForeground != top ? 1 : 0) + (_lastBackground != bottom ? 1 : 0);
                int flipped = (_lastForeground != bottom ? 1 : 0) + (_lastBackground != top ? 1 : 0);

                if (flipped < straight)
                {
                    SetColors(bottom, top);
                    EmitChar(LowerHalf);
                    return;
                }
            }

            SetColors(top, bottom);
            EmitChar(UpperHalf);
            return;
        }

        if (value == ' ')
        {
            // У пробела виден только фон — цвет символа можно не трогать.
            SetColors(_lastForeground, bottom);
            _buffer[_position++] = (byte)' ';
            return;
        }

        SetColors(top, bottom);
        EmitChar(value);
    }

    /// <summary>Приводит цвета терминала к нужным; оба сразу — одной последовательностью.</summary>
    private void SetColors(int foreground, int background)
    {
        bool foregroundChanged = foreground != _lastForeground && foreground >= 0;
        bool backgroundChanged = background != _lastBackground;

        if (!foregroundChanged && !backgroundChanged)
            return;

        _buffer[_position++] = 0x1B;
        _buffer[_position++] = (byte)'[';

        if (foregroundChanged)
        {
            EmitRgb((byte)'3', foreground);
            _lastForeground = foreground;
        }

        if (backgroundChanged)
        {
            if (foregroundChanged)
                _buffer[_position++] = (byte)';';

            EmitRgb((byte)'4', background);
            _lastBackground = background;
        }

        _buffer[_position++] = (byte)'m';
    }

    /// <summary>Пишет «38;2;r;g;b» (символ) или «48;2;r;g;b» (фон) без префикса и «m».</summary>
    private void EmitRgb(byte selector, int color)
    {
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
    }

    private void EmitAscii(string text)
    {
        foreach (char value in text)
            _buffer[_position++] = (byte)value;
    }

    /// <summary>Кодирует символ ячейки в UTF-8 (ASCII, кириллица меню, блоки и рамки).</summary>
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
            if (value >= 1000)
            {
                _buffer[_position++] = (byte)('0' + value / 1000 % 10);
                value %= 1000;
                _buffer[_position++] = (byte)('0' + value / 100);
                value %= 100;
                _buffer[_position++] = (byte)('0' + value / 10);
                _buffer[_position++] = (byte)('0' + value % 10);
                return;
            }

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

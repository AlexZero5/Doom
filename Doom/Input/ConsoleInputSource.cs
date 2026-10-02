using Doom.Platform;

namespace Doom.Input;

/// <summary>Кнопка мыши или колесо в событии терминала.</summary>
internal enum VtMouseButton
{
    /// <summary>Без кнопки: движение курсора.</summary>
    None = -1,

    Left = 0,

    Middle = 1,

    Right = 2,

    /// <summary>Колесо прокрутили вверх.</summary>
    WheelUp = 64,

    /// <summary>Колесо прокрутили вниз.</summary>
    WheelDown = 65
}

/// <summary>Нажатие клавиши, полученное из событий консоли.</summary>
internal readonly record struct VtKeyEvent(ConsoleKey Key);

/// <summary>
///     Событие мыши в координатах ячеек экрана (1-based, как в SGR-последовательностях).
/// </summary>
internal readonly record struct VtMouseEvent(
    int Column,
    int Row,
    VtMouseButton Button,
    bool Pressed,
    bool IsMotion);

/// <summary>
///     Опрос ввода консоли: клавиши и мышь читаются событиями (ReadConsoleInput) в
///     неблокирующем режиме — это тот же механизм, на котором работают
///     Console.KeyAvailable и Console.ReadKey. Стрелки приходят с готовым кодом
///     виртуальной клавиши, мышь — событиями MOUSE_EVENT (в Windows Terminal отчёт
///     о мыши включается DECSET-последовательностью на время меню).
/// </summary>
internal sealed class ConsoleInputSource
{
    private readonly Queue<VtKeyEvent> _keys = new();
    private readonly Queue<VtMouseEvent> _mice = new();
    private readonly NativeMethods.InputRecord[] _records = new NativeMethods.InputRecord[256];
    private IntPtr _inputHandle;
    private uint _leftDown;
    private uint _rightDown;

    /// <summary>Забирает все накопившиеся события консоли во внутренние очереди.</summary>
    public void Poll()
    {
        if (!OperatingSystem.IsWindows())
            return;

        if (!IsValidHandle(_inputHandle))
            _inputHandle = NativeMethods.GetStdHandle(NativeMethods.StdInputHandle);

        if (!IsValidHandle(_inputHandle))
            return;

        if (!NativeMethods.GetNumberOfConsoleInputEvents(_inputHandle, out uint count) || count == 0)
            return;

        // Видимая область экрана: координаты мыши приходят в координатах буфера,
        // а кадр рисуется ровно в размер видимого окна — переводим в окно.
        bool hasWindow = NativeMethods.GetConsoleScreenBufferInfo(
            NativeMethods.GetStdHandle(NativeMethods.StdOutputHandle),
            out NativeMethods.ConsoleScreenBufferInfo info);

        uint toRead = Math.Min(count, (uint)_records.Length);
        if (!NativeMethods.ReadConsoleInput(_inputHandle, _records, toRead, out uint read))
            return;

        for (int index = 0; index < read; index++)
            Process(_records[index], hasWindow, info.Window);
    }

    /// <summary>Выбрасывает накопившиеся события (например, при смене экрана).</summary>
    public void Clear()
    {
        _keys.Clear();
        _mice.Clear();
    }

    public bool TryReadKey(out VtKeyEvent key)
    {
        if (_keys.Count > 0)
        {
            key = _keys.Dequeue();
            return true;
        }

        key = default;
        return false;
    }

    public bool TryReadMouse(out VtMouseEvent mouse)
    {
        if (_mice.Count > 0)
        {
            mouse = _mice.Dequeue();
            return true;
        }

        mouse = default;
        return false;
    }

    // internal, а не private: конвертация записей проверяется тестовым прогоном.
    internal void Process(in NativeMethods.InputRecord record, bool hasWindow, NativeMethods.SmallRect window)
    {
        switch (record.EventType)
        {
            case NativeMethods.InputRecord.KeyEventFlag:
                ProcessKey(record.KeyEvent);
                break;
            case NativeMethods.InputRecord.MouseEventFlag:
                ProcessMouse(record.MouseEvent, hasWindow, window);
                break;
        }
    }

    private void ProcessKey(in NativeMethods.KeyEventRecord key)
    {
        if (key.KeyDown == 0 || key.VirtualKeyCode == 0)
            return;

        // Коды виртуальных клавиш совпадают со значениями ConsoleKey.
        _keys.Enqueue(new VtKeyEvent((ConsoleKey)key.VirtualKeyCode));
    }

    private void ProcessMouse(in NativeMethods.MouseEventRecord mouse, bool hasWindow,
        NativeMethods.SmallRect window)
    {
        int column = mouse.PositionX + 1;
        int row = mouse.PositionY + 1;

        if (hasWindow)
        {
            column -= window.Left;
            row -= window.Top;
        }

        if (column <= 0 || row <= 0)
            return;

        uint state = mouse.ButtonState;
        uint flags = mouse.EventFlags;

        if ((flags & NativeMethods.MouseWheeledFlag) != 0)
        {
            short delta = (short)(mouse.ButtonState >> 16);
            _mice.Enqueue(new VtMouseEvent(column, row,
                delta > 0 ? VtMouseButton.WheelUp : VtMouseButton.WheelDown, Pressed: true, IsMotion: false));
        }
        else if ((flags & NativeMethods.MouseMovedFlag) != 0)
        {
            // Движение: если зажата левая кнопка — это перетаскивание (ползунок).
            bool leftHeld = (state & NativeMethods.FromLeft1stButtonPressed) != 0;
            _mice.Enqueue(new VtMouseEvent(column, row,
                leftHeld ? VtMouseButton.Left : VtMouseButton.None, leftHeld, IsMotion: true));
        }
        else
        {
            // Нажатие или отпускание кнопки (0 — обычный клик, 2 — двойной).
            uint leftNow = state & NativeMethods.FromLeft1stButtonPressed;
            uint rightNow = state & NativeMethods.RightmostButtonPressed;

            if ((leftNow != 0) != (_leftDown != 0))
                _mice.Enqueue(new VtMouseEvent(column, row, VtMouseButton.Left, leftNow != 0, IsMotion: false));

            if ((rightNow != 0) != (_rightDown != 0))
                _mice.Enqueue(new VtMouseEvent(column, row, VtMouseButton.Right, rightNow != 0, IsMotion: false));

            _leftDown = leftNow;
            _rightDown = rightNow;
        }
    }

    private static bool IsValidHandle(IntPtr handle) =>
        handle != IntPtr.Zero && handle != new IntPtr(-1);
}

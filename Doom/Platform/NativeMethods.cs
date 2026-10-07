using System.Runtime.InteropServices;
using System.Text;

namespace Doom.Platform;

/// <summary>
///     P/Invoke-объявления Win32, необходимые для управления шрифтом консоли
///     и опроса состояния клавиш.
/// </summary>
internal static class NativeMethods
{
    public const int StdOutputHandle = -11;

    [StructLayout(LayoutKind.Sequential)]
    public struct Coord
    {
        public short X;
        public short Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct ConsoleFontInfoEx
    {
        public uint Size;
        public uint Font;
        public Coord FontSize;
        public int FontFamily;
        public int FontWeight;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string FaceName;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr GetStdHandle(int stdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetCurrentConsoleFontEx(
        IntPtr consoleOutput, bool maximumWindow, ref ConsoleFontInfoEx consoleCurrentFontEx);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetCurrentConsoleFontEx(
        IntPtr consoleOutput, bool maximumWindow, ref ConsoleFontInfoEx consoleCurrentFontEx);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int virtualKey);

    // ============================================================
    //   Эмуляция ввода (Ctrl + «−») для масштабирования Windows Terminal
    // ============================================================

    public const uint InputKeyboard = 1;

    public const uint KeyEventKeyUp = 0x0002;

    public const ushort VirtualKeyControl = 0x11;
    public const ushort VirtualKey0 = 0x30;

    /// <summary>Клавиша «−» основной части клавиатуры (в терминале ей соответствует Ctrl+−).</summary>
    public const ushort VirtualKeyOemMinus = 0xBD;
    public const ushort VirtualKeyOemPlus = 0xBB;

    /// <summary>
    ///     Входит в объединение и нужен, чтобы размер <see cref="Input" /> совпал с нативным
    ///     <c>INPUT</c> (40 байт на x64): иначе <c>SendInput</c> откажется принимать пакет.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Input
    {
        public uint Type;
        public InputUnion Union;
    }

    /// <summary>Событие нажатия или отпускания клавиши.</summary>
    public static Input KeyInput(ushort virtualKey, bool keyUp) => new()
    {
        Type = InputKeyboard,
        Union = new InputUnion
        {
            Keyboard = new KeyboardInput
            {
                VirtualKey = virtualKey,
                Flags = keyUp ? KeyEventKeyUp : 0
            }
        }
    };

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint numberOfInputs, Input[] inputs, int sizeOfInputStructure);

    // ============================================================
    //   Мышь и курсор
    // ============================================================

    public const int StdInputHandle = -10;

    public const int VirtualKeyLButton = 0x01;
    public const int VirtualKeyRButton = 0x02;

    // Режимы консольного ввода (SetConsoleMode).
    public const uint EnableProcessedInput = 0x0001;
    public const uint EnableLineInput = 0x0002;
    public const uint EnableEchoInput = 0x0004;
    public const uint EnableWindowInput = 0x0008;
    public const uint EnableQuickEditMode = 0x0040;
    public const uint EnableExtendedFlags = 0x0080;
    public const uint EnableVirtualTerminalInput = 0x0200;

    [StructLayout(LayoutKind.Sequential)]
    public struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    public static extern int ShowCursor(bool show);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetConsoleMode(IntPtr consoleHandle, out uint mode);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetConsoleMode(IntPtr consoleHandle, uint mode);

    // ============================================================
    //   События консоли (неблокирующий ввод: клавиши и мышь)
    // ============================================================

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetNumberOfConsoleInputEvents(IntPtr consoleInput, out uint numberOfEvents);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadConsoleInput(
        IntPtr consoleInput, [Out] InputRecord[] records, uint length, out uint eventsRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetConsoleScreenBufferInfo(
        IntPtr consoleOutput, out ConsoleScreenBufferInfo consoleScreenBufferInfo);

    [StructLayout(LayoutKind.Sequential)]
    public struct KeyEventRecord
    {
        public int KeyDown;              // BOOL
        public ushort RepeatCount;
        public ushort VirtualKeyCode;
        public ushort VirtualScanCode;
        public ushort UnicodeChar;
        public uint ControlKeyState;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MouseEventRecord
    {
        public short PositionX;
        public short PositionY;
        public uint ButtonState;
        public uint ControlKeyState;
        public uint EventFlags;
    }

    /// <summary>Запись очереди ввода консоли: объединение ключевого и мышиного событий.</summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct InputRecord
    {
        public const ushort KeyEventFlag = 0x0001;
        public const ushort MouseEventFlag = 0x0002;

        [FieldOffset(0)] public ushort EventType;
        [FieldOffset(4)] public KeyEventRecord KeyEvent;
        [FieldOffset(4)] public MouseEventRecord MouseEvent;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SmallRect
    {
        public short Left;
        public short Top;
        public short Right;
        public short Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ConsoleScreenBufferInfo
    {
        public Coord BufferSize;
        public Coord CursorPosition;
        public ushort Attributes;
        public SmallRect Window;
        public Coord MaximumWindowSize;
    }

    /// <summary>Флаги dwButtonState мыши.</summary>
    public const uint FromLeft1stButtonPressed = 0x0001;
    public const uint RightmostButtonPressed = 0x0002;

    /// <summary>Флаги dwEventFlags мыши.</summary>
    public const uint MouseMovedFlag = 0x0001;
    public const uint MouseWheeledFlag = 0x0004;

    // ============================================================
    //   Окно консоли (разворачивание на весь экран при старте)
    // ============================================================

    public const uint SwRestore = 9;
    public const uint SwMaximize = 3;
    public const uint SwMinimize = 6;

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr window, uint command);

    [DllImport("user32.dll")]
    public static extern bool IsZoomed(IntPtr window);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr window);

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public bool SameAs(Rect other) =>
            Left == other.Left && Top == other.Top && Right == other.Right && Bottom == other.Bottom;
    }

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr window, out Rect rect);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    public static extern bool BringWindowToTop(IntPtr window);

    /// <summary>
    ///     Принудительная активация окна (тем же механизмом, что Alt+Tab). Не документирована,
    ///     но живёт в user32.dll десятилетиями; если её вдруг не окажется — вызов отловится
    ///     вызывающим кодом, и останется запасной приём со сворачиванием окна.
    /// </summary>
    [DllImport("user32.dll")]
    public static extern void SwitchToThisWindow(IntPtr window, bool altTab);

    // Вывести чужое окно вперёд простым SetForegroundWindow система не даёт фоновому
    // процессу: нужно временно «присоединить» очередь ввода своего потока к потоку окна.
    [DllImport("user32.dll")]
    public static extern bool AttachThreadInput(uint attachTo, uint attachFrom, bool attach);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetConsoleScreenBufferSize(IntPtr consoleOutput, Coord size);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr window, [Out] StringBuilder className, int maxCount);

    /// <summary>
    ///     Открывает консоль напрямую по имени <c>CONOUT$</c>. В отличие от стандартного
    ///     вывода, этот хэндл работает, даже если stdout перенаправлен — именно поэтому
    ///     размер окна читается и меняется корректно в любом случае.
    /// </summary>
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr CreateFile(
        string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes,
        uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    public const uint GenericRead = 0x80000000;
    public const uint GenericWrite = 0x40000000;
    public const uint FileShareWrite = 0x00000002;
    public const uint OpenExisting = 3;

    // ============================================================
    //   Поиск «своего» окна терминала (для разворота в Windows Terminal)
    // ============================================================

    public delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(string? className, string windowName);

    public const uint Th32csSnapProcess = 0x00000002;

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool Process32First(IntPtr snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry32 entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct ProcessEntry32
    {
        public uint Size;
        public uint Usage;
        public uint ProcessId;
        public IntPtr DefaultHeapId;
        public uint ModuleId;
        public uint Threads;
        public uint ParentProcessId;
        public int PriorityClassBase;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExeFile;
    }
}

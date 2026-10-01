using System.Runtime.InteropServices;

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

    /// <summary>Клавиша «−» основной части клавиатуры (в терминале ей соответствует Ctrl+−).</summary>
    public const ushort VirtualKeyOemMinus = 0xBD;

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

    public const uint EnableQuickEditMode = 0x0040;
    public const uint EnableExtendedFlags = 0x0080;

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
}

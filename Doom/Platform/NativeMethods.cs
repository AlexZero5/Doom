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
}

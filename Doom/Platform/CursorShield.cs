using System.Runtime.InteropServices;

namespace Doom.Platform;

/// <summary>
///     «Шторка» для курсора: крошечное почти прозрачное окно поверх терминала ровно под
///     удерживаемым курсором. Курсор над ним не рисуется (окно отвечает «курсора нет»), а клики
///     достаются ему, а не терминалу — значит, мышь не выделяет текст и не ставит игру на паузу.
///     Окно не забирает фокус (терминал остаётся активным) и живёт в отдельном потоке со своим
///     циклом сообщений. Процесс завершился — окно исчезло само: в системе ничего не меняется
///     (в отличие от подмены системного курсора).
/// </summary>
internal sealed class CursorShield : IDisposable
{
    private const int Size = 96;

    private const uint WsPopup = 0x80000000;
    private const uint WsExLayered = 0x00080000;
    private const uint WsExToolWindow = 0x00000080;
    private const uint WsExTopmost = 0x00000008;
    private const uint WsExNoActivate = 0x08000000;
    private const uint LwaAlpha = 0x2;
    private const uint WmSetCursor = 0x0020;
    private const uint WmMouseActivate = 0x0021;
    private const uint WmClose = 0x0010;
    private const uint WmDestroy = 0x0002;
    private const int MaNoActivate = 3;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const int SwHide = 0;
    private static readonly IntPtr HwndTopmost = new(-1);

    private readonly WndProc _procedure;
    private readonly Thread? _thread;
    private readonly ManualResetEventSlim _ready = new();
    private IntPtr _window;
    private bool _visible;
    private int _x = int.MinValue, _y = int.MinValue;

    public CursorShield()
    {
        _procedure = Procedure;
        if (!OperatingSystem.IsWindows())
            return;

        _thread = new Thread(Run) { IsBackground = true, Name = "cursor-shield" };
        _thread.Start();
        _ready.Wait(1000);
    }

    /// <summary>Ставит шторку под точку экрана (курсор удерживается в ней).</summary>
    public void Show(int x, int y)
    {
        if (_window == IntPtr.Zero || (_visible && x == _x && y == _y))
            return;

        _x = x;
        _y = y;
        _visible = true;
        SetWindowPos(_window, HwndTopmost, x - Size / 2, y - Size / 2, Size, Size, SwpNoActivate | SwpShowWindow);
    }

    public void Hide()
    {
        if (_window == IntPtr.Zero || !_visible)
            return;

        _visible = false;
        ShowWindow(_window, SwHide);
    }

    public void Dispose()
    {
        if (_window != IntPtr.Zero)
            PostMessageW(_window, WmClose, IntPtr.Zero, IntPtr.Zero);
        _thread?.Join(500);
        _window = IntPtr.Zero;
    }

    private void Run()
    {
        try
        {
            IntPtr instance = GetModuleHandleW(null);
            var windowClass = new WindowClass
            {
                Size = (uint)Marshal.SizeOf<WindowClass>(),
                Procedure = Marshal.GetFunctionPointerForDelegate(_procedure),
                Instance = instance,
                ClassName = "FunDoomCursorShield"
            };
            RegisterClassExW(ref windowClass);

            _window = CreateWindowExW(WsExLayered | WsExToolWindow | WsExTopmost | WsExNoActivate,
                windowClass.ClassName, "", WsPopup, 0, 0, Size, Size, IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);
            if (_window != IntPtr.Zero)
                SetLayeredWindowAttributes(_window, 0, 1, LwaAlpha); // почти прозрачное, но ловит мышь
        }
        finally
        {
            _ready.Set();
        }

        if (_window == IntPtr.Zero)
            return;

        while (GetMessageW(out Message message, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref message);
            DispatchMessageW(ref message);
        }
    }

    private IntPtr Procedure(IntPtr window, uint message, IntPtr wParam, IntPtr lParam)
    {
        switch (message)
        {
            case WmSetCursor:
                SetCursor(IntPtr.Zero);
                return new IntPtr(1);
            case WmMouseActivate:
                return new IntPtr(MaNoActivate);
            case WmClose:
                DestroyWindow(window);
                return IntPtr.Zero;
            case WmDestroy:
                PostQuitMessage(0);
                return IntPtr.Zero;
            default:
                return DefWindowProcW(window, message, wParam, lParam);
        }
    }

    private delegate IntPtr WndProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Size;
        public uint Style;
        public IntPtr Procedure;
        public int ClassExtra;
        public int WindowExtra;
        public IntPtr Instance;
        public IntPtr Icon;
        public IntPtr Cursor;
        public IntPtr Background;
        public string? MenuName;
        public string ClassName;
        public IntPtr SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        public IntPtr Window;
        public uint Id;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public int X;
        public int Y;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassExW(ref WindowClass windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(uint exStyle, string className, string windowName, uint style,
        int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr window, uint colorKey, byte alpha, uint flags);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    private static extern int GetMessageW(out Message message, IntPtr window, uint filterMin, uint filterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref Message message);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessageW(ref Message message);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProcW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool PostMessageW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int exitCode);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr SetCursor(IntPtr cursor);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string? moduleName);
}

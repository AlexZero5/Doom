using System.Diagnostics;
using Doom.Configuration;
using Doom.Platform;
using Point = Doom.Platform.NativeMethods.Point;

namespace Doom.Input;

/// <summary>
///     Состояние мыши: сдвиг курсора за кадр и кнопки. На Windows позиция читается через Win32
///     (<c>GetCursorPos</c>), кнопки — через <c>GetAsyncKeyState</c>. При захвате курсор скрывается
///     и удерживается в исходной точке, поэтому обзор можно продолжать бесконечно.
/// </summary>
internal sealed class MouseState
{
    /// <summary>Процессы, владеющие окном терминала: при них захват курсора уместен.</summary>
    private static readonly string[] TerminalProcesses =
    {
        "WindowsTerminal", "OpenConsole", "conhost", "cmd", "powershell", "pwsh", "wt"
    };

    private bool _initialized;
    private int _lastX;
    private int _lastY;

    private bool _capturing;
    private bool _pinning;
    private bool _cursorHidden;
    private Point _origin;
    private Point _savedCursor;
    private bool _savedCursorValid;

    private uint _foregroundPid;
    private bool _foregroundIsTerminal;

    /// <summary>Сдвиг курсора по горизонтали за кадр (в пикселях).</summary>
    public int DeltaX { get; private set; }

    /// <summary>Сдвиг курсора по вертикали за кадр (в пикселях).</summary>
    public int DeltaY { get; private set; }

    /// <summary>Левая кнопка мыши удерживается (для стрельбы).</summary>
    public bool LeftButton { get; private set; }

    /// <summary>Правая кнопка мыши удерживается.</summary>
    public bool RightButton { get; private set; }

    /// <summary>Включает захват: во время игры курсор будет скрыт и удержан на месте.</summary>
    public void BeginCapture()
    {
        if (!OperatingSystem.IsWindows() || _capturing)
            return;

        if (NativeMethods.GetCursorPos(out Point pos))
        {
            _savedCursor = pos;
            _savedCursorValid = true;
        }

        _capturing = true;
    }

    /// <summary>Отпускает захват: снова показывает курсор и возвращает его на прежнее место.</summary>
    public void EndCapture()
    {
        if (_cursorHidden)
        {
            NativeMethods.ShowCursor(true);
            _cursorHidden = false;
        }

        if (_capturing && _savedCursorValid)
            NativeMethods.SetCursorPos(_savedCursor.X, _savedCursor.Y);

        _capturing = false;
        _pinning = false;
        _savedCursorValid = false;
    }

    /// <summary>Читает состояние мыши за текущий кадр.</summary>
    public void Update()
    {
        DeltaX = 0;
        DeltaY = 0;

        if (!OperatingSystem.IsWindows())
        {
            LeftButton = false;
            RightButton = false;
            return;
        }

        LeftButton = IsButtonDown(NativeMethods.VirtualKeyLButton);
        RightButton = IsButtonDown(NativeMethods.VirtualKeyRButton);

        if (!NativeMethods.GetCursorPos(out Point pos))
            return;

        if (!_initialized)
        {
            _initialized = true;
            _lastX = pos.X;
            _lastY = pos.Y;
            return;
        }

        // Захват держим только пока на переднем плане окно терминала, чтобы не «красть»
        // курсор у пользователя, переключившегося в другую программу.
        bool captureActive = _capturing && IsTerminalForeground();

        if (captureActive)
        {
            if (!_cursorHidden)
            {
                NativeMethods.ShowCursor(false);
                _cursorHidden = true;
            }

            if (!_pinning)
            {
                _origin = pos;
                _pinning = true;
            }

            DeltaX = pos.X - _lastX;
            DeltaY = pos.Y - _lastY;

            NativeMethods.SetCursorPos(_origin.X, _origin.Y);
            _lastX = _origin.X;
            _lastY = _origin.Y;
        }
        else
        {
            if (_cursorHidden)
            {
                NativeMethods.ShowCursor(true);
                _cursorHidden = false;
            }

            _pinning = false;
            DeltaX = pos.X - _lastX;
            DeltaY = pos.Y - _lastY;
            _lastX = pos.X;
            _lastY = pos.Y;
        }
    }

    private static bool IsButtonDown(int virtualKey) =>
        (NativeMethods.GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    /// <summary>
    ///     Проверяет, что окно на переднем плане принадлежит терминалу или оболочке.
    ///     Имя процесса кэшируется и пересчитывается только при смене активного окна.
    /// </summary>
    private bool IsTerminalForeground()
    {
        IntPtr foreground = NativeMethods.GetForegroundWindow();
        if (foreground == IntPtr.Zero)
            return false;

        NativeMethods.GetWindowThreadProcessId(foreground, out uint processId);
        if (processId == 0)
            return false;

        if (processId == _foregroundPid)
            return _foregroundIsTerminal;

        _foregroundPid = processId;
        _foregroundIsTerminal = MatchesTerminalProcess(processId);
        return _foregroundIsTerminal;
    }

    private static bool MatchesTerminalProcess(uint processId)
    {
        try
        {
            using Process process = Process.GetProcessById((int)processId);
            foreach (string name in TerminalProcesses)
            {
                if (process.ProcessName.Contains(name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }
}
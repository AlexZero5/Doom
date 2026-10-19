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
    private bool _initialized;
    private int _lastX;
    private int _lastY;

    private bool _capturing;
    private bool _pinning;
    private bool _cursorHidden;

    /// <summary>Шторка, прячущая курсор и клики от терминала (null — не прятать).</summary>
    public CursorShield? Shield { get; set; }

    /// <summary>Прятать ли курсор при захвате (настройка).</summary>
    public bool HideCursor { get; set; } = true;
    private Point _origin;
    private Point _savedCursor;
    private bool _savedCursorValid;

    /// <summary>Сдвиг курсора по горизонтали за кадр (в пикселях).</summary>
    public int DeltaX { get; private set; }

    /// <summary>Сдвиг курсора по вертикали за кадр (в пикселях).</summary>
    public int DeltaY { get; private set; }

    /// <summary>Левая кнопка мыши удерживается (для стрельбы).</summary>
    public bool LeftButton { get; private set; }

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
            Shield?.Hide();
            _cursorHidden = false;
        }

        if (_capturing && _savedCursorValid)
            NativeMethods.SetCursorPos(_savedCursor.X, _savedCursor.Y);

        _capturing = false;
        _pinning = false;
        _savedCursorValid = false;
    }

    /// <summary>
    ///     Читает состояние мыши за текущий кадр. <paramref name="hasFocus" /> — окно игры
    ///     на переднем плане: без фокуса кнопки и сдвиг игнорируются (клик в браузере не должен
    ///     стрелять), а захват курсора отпускается, чтобы не «красть» его у другой программы.
    /// </summary>
    public void Update(bool hasFocus)
    {
        DeltaX = 0;
        DeltaY = 0;
        LeftButton = false;

        if (!OperatingSystem.IsWindows())
            return;

        if (!NativeMethods.GetCursorPos(out Point pos))
            return;

        if (!_initialized)
        {
            _initialized = true;
            _lastX = pos.X;
            _lastY = pos.Y;
            return;
        }

        if (!hasFocus)
        {
            ReleasePin();
            _lastX = pos.X;
            _lastY = pos.Y;
            return;
        }

        LeftButton = (NativeMethods.GetAsyncKeyState(NativeMethods.VirtualKeyLButton) & 0x8000) != 0;

        if (_capturing)
        {
            if (!HideCursor && _cursorHidden)
            {
                Shield?.Hide();
                _cursorHidden = false;
            }

            if (!_pinning)
            {
                // Только что получили фокус: точка удержания — там, где курсор сейчас,
                // а сдвиг этого кадра не считаем (иначе камера дёрнется на весь путь курсора).
                _origin = pos;
                _pinning = true;
                if (HideCursor && Shield is not null)
                {
                    Shield.Show(pos.X, pos.Y);
                    _cursorHidden = true;
                }

                _lastX = pos.X;
                _lastY = pos.Y;
                return;
            }

            DeltaX = pos.X - _lastX;
            DeltaY = pos.Y - _lastY;

            NativeMethods.SetCursorPos(_origin.X, _origin.Y);
            _lastX = _origin.X;
            _lastY = _origin.Y;
        }
        else
        {
            ReleasePin();
            DeltaX = pos.X - _lastX;
            DeltaY = pos.Y - _lastY;
            _lastX = pos.X;
            _lastY = pos.Y;
        }
    }

    private void ReleasePin()
    {
        if (_cursorHidden)
        {
            Shield?.Hide();
            _cursorHidden = false;
        }

        _pinning = false;
    }
}

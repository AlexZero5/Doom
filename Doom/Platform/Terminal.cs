using System.Runtime.InteropServices;
using System.Text;
using Doom.Configuration;

namespace Doom.Platform;

/// <summary>
///     Доступ к консоли: поток стандартного вывода, размер окна, размер шрифта
///     (только Windows) и запись управляющих последовательностей.
/// </summary>
internal sealed class Terminal
{
    private readonly Stream _stdout;

    private uint _originalInputMode;
    private bool _inputModeSaved;

    /// <summary>Окно терминала, в котором нас показывают (ищется один раз за запуск).</summary>
    private IntPtr _terminalWindow;

    /// <summary>Каким способом нашли окно терминала (для отчёта).</summary>
    private string _terminalFoundBy = "не искали";

    /// <summary>
    ///     Отчёт о последней попытке применить масштаб: режим, сколько нажатий, вывелось ли
    ///     окно на передний план и как изменился размер. Пишется в лог диагностики, потому
    ///     что на экране эти строки сразу затираются кадром.
    /// </summary>
    public string LastZoomReport { get; private set; } = "не применялся";

    /// <summary>Что произошло при попытке сменить шрифт консоли (для отчёта).</summary>
    private string _fontReport = "не пробовали";

    /// <summary>Что произошло при попытке сменить масштаб терминала клавишами (для отчёта).</summary>
    private string _keyReport = "не пробовали";

    public Terminal()
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch
        {
            // Консоль может быть недоступна (перенаправленный вывод) — продолжаем.
        }

        _stdout = Console.OpenStandardOutput();
    }

    /// <summary>Игра запущена в Windows Terminal?</summary>
    public static bool IsRunningInWindowsTerminal =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WT_SESSION"));

    /// <summary>
    ///     Короткое описание консоли для диагностики. Важно различать настоящую консоль и
    ///     консоль, отданную терминалу (ConPTY): у второй окно класса <c>PseudoConsoleWindow</c>,
    ///     шрифта нет, и размер «пикселя» задаётся масштабом терминала, а не шрифтом.
    /// </summary>
    public static string DescribeConsole()
    {
        if (!OperatingSystem.IsWindows())
            return "не Windows";

        string classOfWindow = GetWindowClass(NativeMethods.GetConsoleWindow());

        return classOfWindow switch
        {
            "" => "окно консоли не найдено",
            "ConsoleWindowClass" => $"окно '{classOfWindow}'",
            _ => $"окно '{classOfWindow}' — консоль отдана терминалу (шрифт не переключается)"
        };
    }

    /// <summary>Записывает управляющую последовательность (только ASCII).</summary>
    public void Write(string escapeSequence)
    {
        try
        {
            byte[] bytes = Encoding.ASCII.GetBytes(escapeSequence);
            _stdout.Write(bytes, 0, bytes.Length);
        }
        catch
        {
            // Вывод может быть закрыт: молча игнорируем, чтобы не ронять игру.
        }
    }

    /// <summary>Записывает подготовленный пакет байт (используется рендером кадра).</summary>
    public void Write(byte[] buffer, int length)
    {
        try
        {
            _stdout.Write(buffer, 0, length);
        }
        catch
        {
        }
    }

    public void HideCursor() => Write(Ansi.HideCursor);

    public void ClearScreen() => Write(Ansi.ClearScreen + Ansi.Home);

    public void HideCursorAndClearScreen() => Write(Ansi.HideCursor + Ansi.ClearScreen + Ansi.Home);

    /// <summary>
    ///     Отключает автоперенос строк (DECAWM): запись в последнюю ячейку строки не
    ///     переносит курсор и не скроллит экран. Обязательно для дифф-вывода кадра.
    /// </summary>
    public void DisableAutoWrap() => Write(Ansi.DisableAutoWrap);

    /// <summary>Возвращает автоперенос строк (при выходе из игры).</summary>
    public void EnableAutoWrap() => Write(Ansi.EnableAutoWrap);

    /// <summary>
    ///     Включает отчёт о мыши: терминал присылает клики, движение и колесо в координатах
    ///     ячеек (SGR-режим). Нужен только в меню — во время игры обзор мышью работает
    ///     напрямую через <c>GetCursorPos</c>.
    /// </summary>
    public void EnableMouseReporting() => Write(Ansi.EnableMouseReporting);

    /// <summary>Выключает отчёт о мыши (обратно в нормальный режим терминала).</summary>
    public void DisableMouseReporting() => Write(Ansi.DisableMouseReporting);

    /// <summary>Возвращает терминал в нормальное состояние.</summary>
    public void Restore()
    {
        Write(Ansi.ResetColors + Ansi.ShowCursor + Ansi.ClearScreen + Ansi.Home);

        try
        {
            _stdout.Flush();
        }
        catch
        {
        }
    }

    /// <summary>
    ///     Настраивает ввод консоли для игры: отключает QuickEdit (иначе клик мышью в
    ///     классической консоли выделяет текст и останавливает игру), а также построчный
    ///     ввод и эхо. Клавиши и мышь игра читает событиями через ReadConsoleInput
    ///     (см. <see cref="Input.ConsoleInputSource" />).
    /// </summary>
    public void ConfigureInput()
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            IntPtr handle = NativeMethods.GetStdHandle(NativeMethods.StdInputHandle);
            if (handle == IntPtr.Zero || handle == new IntPtr(-1))
                return;

            if (!NativeMethods.GetConsoleMode(handle, out uint mode))
                return;

            _originalInputMode = mode;
            _inputModeSaved = true;

            uint updated = mode
                & ~(NativeMethods.EnableQuickEditMode
                    | NativeMethods.EnableLineInput
                    | NativeMethods.EnableEchoInput)
                | NativeMethods.EnableExtendedFlags;

            NativeMethods.SetConsoleMode(handle, updated);
        }
        catch
        {
            // Консоль может быть недоступна (перенаправленный ввод) — продолжаем играть.
        }
    }

    /// <summary>Возвращает прежний режим ввода консоли (включая QuickEdit).</summary>
    public void RestoreInputMode()
    {
        if (!OperatingSystem.IsWindows() || !_inputModeSaved)
            return;

        try
        {
            IntPtr handle = NativeMethods.GetStdHandle(NativeMethods.StdInputHandle);
            if (handle != IntPtr.Zero && handle != new IntPtr(-1))
                NativeMethods.SetConsoleMode(handle, _originalInputMode);
        }
        catch
        {
        }
        finally
        {
            _inputModeSaved = false;
        }
    }

    /// <summary>
    ///     Разворачивает окно консоли — то же, что кнопка «развернуть» в заголовке.
    ///     В классической консоли это прямое ShowWindow её окна; в Windows Terminal
    ///     (где GetConsoleWindow возвращает скрытое окно-прослойку) разворачивается
    ///     окно терминала на переднем плане — при запуске игры это её собственное окно.
    /// </summary>
    public void MaximizeWindow()
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            IntPtr consoleWindow = NativeMethods.GetConsoleWindow();
            if (consoleWindow != IntPtr.Zero
                && GetWindowClass(consoleWindow) == "ConsoleWindowClass")
            {
                NativeMethods.ShowWindow(consoleWindow, NativeMethods.SwMaximize);
                return;
            }

            // Windows Terminal: окно ищем по цепочке процессов (см. ConsoleWindowLocator),
            // а не по «активному окну» — при запуске из IDE или скрипта активным может быть
            // совсем другое окно, и разворачивалось не то.
            if (ConsoleWindowLocator.TryMaximizeOwnTerminalWindow())
                return;

            IntPtr foreground = NativeMethods.GetForegroundWindow();
            if (foreground != IntPtr.Zero
                && GetWindowClass(foreground) == "CASCADIA_HOSTING_WINDOW_CLASS")
            {
                NativeMethods.ShowWindow(foreground, NativeMethods.SwMaximize);
            }
        }
        catch
        {
            // Не развернулось — окно останется прежнего размера, игре это не мешает.
        }
    }

    /// <summary>
    ///     Применяет выбранное «Качество графики». Есть два разных мира, и в каждом размер
    ///     «пикселя» задаётся по-своему: у настоящей консоли (окно <c>ConsoleWindowClass</c>)
    ///     это размер шрифта, а у консоли, отданной терминалу (окно <c>PseudoConsoleWindow</c>,
    ///     ConPTY) шрифта нет вообще — там работает только масштаб терминала.
    /// </summary>
    public bool TryApplyQualityZoom()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        if (IsRealConsoleWindow())
        {
            if (TryFontZoom())
            {
                LastZoomReport = $"шрифт: {_fontReport}";
                return true;
            }
        }
        else
        {
            IntPtr consoleWindow = NativeMethods.GetConsoleWindow();
            _fontReport =
                $"шрифта у консоли нет (окно '{GetWindowClass(consoleWindow)}' — консоль отдана терминалу)";
        }

        bool ok = TryKeyZoom();
        LastZoomReport = $"шрифт: {_fontReport} · клавиши: {_keyReport}";
        return ok;
    }

    /// <summary>
    ///     Настоящая ли это консоль (а не консоль, отданная терминалу). Различие принципиально:
    ///     у ConPTY-консоли <c>SetCurrentConsoleFontEx</c> возвращает TRUE и ничего не делает,
    ///     поэтому «успех» по коду возврата там означает ровно ничего.
    /// </summary>
    private static bool IsRealConsoleWindow()
    {
        IntPtr window = NativeMethods.GetConsoleWindow();
        return window != IntPtr.Zero && GetWindowClass(window) == "ConsoleWindowClass";
    }

    /// <summary>
    ///     Пробует поставить шрифт, соответствующий качеству. Точный размер для выбранного
    ///     качества может быть неподдерживаемым — тогда берём ближайший рабочий, но всегда
    ///     проверяем результат по фактическому размеру окна: без проверки игра «думала», что
    ///     зум применился, и стартовала с крупным шрифтом.
    /// </summary>
    private bool TryFontZoom()
    {
        short target = FontSizeForQuality(GameSettings.Current.Quality);
        short current = TryReadFontSize();

        if (current != 0 && current == target)
        {
            _fontReport = $"шрифт уже {target}pt";
            RetuneWindow();
            return true;
        }

        ReadWindowSize(out int columnsBefore, out int rowsBefore);

        // Сначала целевой размер, затем ближайшие сверху (если целевой не поддерживается),
        // затем снизу — тоже в пределах допустимого.
        for (short size = target; size <= GameConfig.MaximumFontSize; size++)
            if (TrySetFontVerified(size, target, columnsBefore, rowsBefore))
                return true;

        for (short size = (short)(target - 1); size >= GameConfig.MinimumFontSize; size--)
            if (TrySetFontVerified(size, target, columnsBefore, rowsBefore))
                return true;

        _fontReport = $"цель={target}pt (сейчас {current}pt) — ни один размер шрифта не подошёл";
        return false;
    }

    /// <summary>Ставит шрифт указанного размера и проверяет, что кадр реально перестроился.</summary>
    private bool TrySetFontVerified(short size, short target, int columnsBefore, int rowsBefore)
    {
        if (!TrySetFontSize(size))
            return false;

        ReadWindowSize(out int columnsAfter, out int rowsAfter);
        if (columnsAfter == columnsBefore && rowsAfter == rowsBefore)
            return false;

        _fontReport =
            $"цель={target}pt поставил={size}pt {columnsBefore}x{rowsBefore}->{columnsAfter}x{rowsAfter}";
        RetuneWindow();
        return true;
    }

    /// <summary>
    ///     Меняет масштаб терминала эмуляцией клавиш. Нужен для консолей, отданных терминалу
    ///     (Windows Terminal и т.п.): у них нет своего шрифта, и размер «пикселя» задаётся
    ///     масштабом терминала.
    /// </summary>
    private bool TryKeyZoom()
    {
        double columnsOfBase = GameSettings.Current.ZoomColumnsOfBase;

        if (!TryDetectTerminalWindow())
        {
            _keyReport = $"окно терминала не найдено ({_terminalFoundBy}) — нажатия не отправлял";
            return false;
        }

        IntPtr terminalWindow = _terminalWindow;
        string foundBy = _terminalFoundBy;

        if (!EnsureFocused(terminalWindow))
        {
            _keyReport = $"окно найдено ({foundBy}), но не вывелось на передний план — " +
                         "нажатия не отправлял (иначе они ушли бы в чужое окно)";
            return false;
        }

        bool ok = ApplyTerminalZoom(terminalWindow, columnsOfBase, out string report);
        _keyReport = $"{foundBy}, доля от заводского={columnsOfBase:0.00}, " +
                     $"{(ok ? "применил" : "НЕ применил")} · {report}";
        return ok;
    }

    /// <summary>
    ///     Ищет окно терминала один раз за запуск: и по нему же подтверждается, что нас
    ///     показывает именно Windows Terminal. Повторять поиск не нужно — найденное окно
    ///     за время работы не меняется, а поиск по заголовку пишет метку в консоль.
    /// </summary>
    public bool TryDetectTerminalWindow()
    {
        if (_terminalWindow != IntPtr.Zero)
            return true;

        _terminalWindow = FindTerminalWindow(out string foundBy);
        _terminalFoundBy = foundBy;

        return _terminalWindow != IntPtr.Zero;
    }

    /// <summary>
    ///     Нас показывает Windows Terminal: это ПОДТВЕРЖДЕНО найденным окном терминала по
    ///     заголовку нашей консоли. Признак точнее переменной <c>WT_SESSION</c> — та не
    ///     выставляется, когда консоль делегирована терминалу, и Windows Terminal принимался
    ///     за классическую консоль (со всеми последствиями: шрифт вместо масштаба и медленный
    ///     полный кадр вместо диффа).
    /// </summary>
    public bool TerminalIsWindowsTerminal => ConsoleWindowLocator.IsWindowsTerminalWindow(_terminalWindow);

    /// <summary>Что известно про окно терминала — для диагностики.</summary>
    public string TerminalWindowReport =>
        _terminalWindow == IntPtr.Zero ? $"не найдено ({_terminalFoundBy})" : _terminalFoundBy;

    /// <summary>
    ///     Ищет окно терминала, в котором нас показывают. В обычном запуске Windows Terminal —
    ///     предок нашего процесса; при делегировании консоли терминалу (в том числе когда
    ///     <c>WT_SESSION</c> не выставлен) предка нет, зато терминал показывает заголовок
    ///     консоли в заголовке окна — по нему и находим.
    /// </summary>
    private IntPtr FindTerminalWindow(out string foundBy)
    {
        // Заголовок точнее всего: метка написана ИМЕННО в нашу консоль, а терминал показывает
        // её в своём окне. Поэтому этот способ пробуем первым.
        IntPtr window = FindTerminalWindowByTitle();
        if (window != IntPtr.Zero)
        {
            foundBy = "по заголовку консоли";
            return window;
        }

        window = ConsoleWindowLocator.FindOwnTerminalWindow();
        if (window != IntPtr.Zero)
        {
            foundBy = "по дереву процессов";
            return window;
        }

        window = NativeMethods.GetForegroundWindow();
        if (ConsoleWindowLocator.IsWindowsTerminalWindow(window))
        {
            foundBy = "по активному окну";
            return window;
        }

        foundBy = "не найдено";
        return IntPtr.Zero;
    }

    /// <summary>
    ///     Находит окно терминала по заголовку: игра пишет в заголовок консоли метку
    ///     (<c>ESC ] 0 ; … BEL</c>), Windows Terminal показывает её в заголовке окна.
    ///     Заголовок обновляется не мгновенно, поэтому метку ищем несколько раз.
    /// </summary>
    private IntPtr FindTerminalWindowByTitle()
    {
        string marker = $"FunDoom terminal {Environment.ProcessId}";
        string previous = string.Empty;

        try
        {
            if (OperatingSystem.IsWindows())
                previous = Console.Title;
        }
        catch
        {
            // Заголовок недоступен — не страшно, просто потом не восстановим.
        }

        Write($"\u001b]0;{marker}\u0007");

        IntPtr found = IntPtr.Zero;
        for (int attempt = 0; attempt < 5 && found == IntPtr.Zero; attempt++)
        {
            Thread.Sleep(70);

            IntPtr candidate = NativeMethods.FindWindow(null, marker);
            if (ConsoleWindowLocator.IsWindowsTerminalWindow(candidate)
                && NativeMethods.IsWindowVisible(candidate))
                found = candidate;
        }

        if (!string.IsNullOrEmpty(previous))
            Write($"\u001b]0;{previous}\u0007");

        return found;
    }

    /// <summary>
    ///     Применяет «Качество графики» на лету. Windows Terminal: масштаб переставляется
    ///     заново (от подтверждённого минимума вверх до нужной доли ширины кадра), классическая
    ///     консоль — подбором размера шрифта. В обоих случаях дожидается стабилизации размера
    ///     окна и выравнивает буфер по рамке.
    /// </summary>
    public bool ApplyQualityOnFly()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        // И для Windows Terminal, и для классической консоли зум ставится одинаково —
        // процедура сама выбирает способ под терминал.
        bool changed = TryApplyQualityZoom();

        WaitForSizeStable();
        SyncBufferToWindow();
        if (changed)
            LockCurrentZoom();
        return changed;
    }

    // ============================================================
    //   Запрет ручного зума (Ctrl + колесо, Ctrl + − / Ctrl + =)
    // ============================================================
    // Терминал позволяет игроку менять масштаб в любой момент, но в игре кадр калибруется
    // под выбранное «Качество графики»: ручной зум ломает и размер «пикселей», и всю
    // калибровку. Поэтому игра запоминает размер кадра И пиксельный прямоугольник окна:
    // если кадр изменился, а окно — нет, значит, это ручной зум, и масштаб перекалибруется
    // обратно. Настоящее изменение размера окна (рамку потянули, развернули) не трогаем.

    /// <summary>Кадр, который получился после последней нашей калибровки масштаба.</summary>
    private int _lockedColumns;
    private int _lockedRows;

    /// <summary>Пиксельный прямоугольник окна терминала на момент калибровки.</summary>
    private NativeMethods.Rect _lockedWindowRect;

    /// <summary>Есть ли что охранять: после первой удачной калибровки.</summary>
    private bool _zoomLocked;

    /// <summary>Запоминает текущий кадр и прямоугольник окна как эталон калибровки.</summary>
    public void LockCurrentZoom()
    {
        ReadWindowSize(out int columns, out int rows);
        if (columns <= 0 || rows <= 0)
            return;

        _lockedColumns = columns;
        _lockedRows = rows;
        _lockedWindowRect = TerminalWindowRect();
        _zoomLocked = true;
    }

    /// <summary>
    ///     Проверяет, не зумил ли игрок терминал вручную, и восстанавливает калибровку.
    ///     Возвращает <c>true</c>, если зум был перехвачен и кадр перестроился.
    /// </summary>
    public bool TryRestoreUserZoom()
    {
        if (!_zoomLocked)
            return false;

        ReadWindowSize(out int columns, out int rows);
        NativeMethods.Rect rect = TerminalWindowRect();

        if (columns == _lockedColumns && rows == _lockedRows)
        {
            // Кадр на месте. Если изменился сам прямоугольник окна — окно тянули или
            // разворачивали: это законно, обновляем эталон и пересобирать кадр не нужно.
            if (!rect.SameAs(_lockedWindowRect))
                _lockedWindowRect = rect;
            return false;
        }

        if (!rect.SameAs(_lockedWindowRect))
        {
            // Окно изменилось, и кадр изменился — обычный ресайз: кадр следует за рамкой.
            _lockedColumns = columns;
            _lockedRows = rows;
            _lockedWindowRect = rect;
            return false;
        }

        // Кадр изменился при неизменном окне — терминал перезумили клавишами или колесом.
        bool restored = TryApplyQualityZoom();
        LockCurrentZoom();
        return restored;
    }

    /// <summary>Пиксельный прямоугольник окна терминала (или консольного окна).</summary>
    private NativeMethods.Rect TerminalWindowRect()
    {
        IntPtr window = _terminalWindow != IntPtr.Zero ? _terminalWindow : NativeMethods.GetConsoleWindow();

        if (window == IntPtr.Zero || !NativeMethods.GetWindowRect(window, out NativeMethods.Rect rect))
            return default;

        return rect;
    }

    /// <summary>Читает текущий размер шрифта консоли (0 — не удалось).</summary>
    private static short TryReadFontSize()
    {
        try
        {
            IntPtr handle = ConsoleOutput();
            if (handle == IntPtr.Zero || handle == new IntPtr(-1))
                return 0;

            var info = new NativeMethods.ConsoleFontInfoEx
            {
                Size = (uint)Marshal.SizeOf<NativeMethods.ConsoleFontInfoEx>()
            };

            return NativeMethods.GetCurrentConsoleFontEx(handle, false, ref info)
                ? info.FontSize.Y
                : (short)0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    ///     Размер шрифта классической консоли для качества: чем выше качество, тем мельче
    ///     шрифт (7 уровней с шагом 2 pt: Низкое 16 pt … Максимум 4 pt).
    /// </summary>
    private static short FontSizeForQuality(GraphicsQuality quality) =>
        (short)(GameConfig.MinimumFontSize + (6 - (int)quality) * 2);

    /// <summary>
    ///     Ждёт, пока размер игровой области перестанет меняться (терминал анимирует
    ///     ресайз после разворота окна и смены масштаба). Без этого кадр рисуется под
    ///     ещё не устоявшийся размер — картинка «едет», меню уезжает за экран.
    /// </summary>
    public static void WaitForSizeStable()
    {
        ReadWindowSize(out int lastColumns, out int lastRows);

        for (int attempt = 0; attempt < GameConfig.StablePollAttempts; attempt++)
        {
            Thread.Sleep(GameConfig.StablePollMs);
            ReadWindowSize(out int columns, out int rows);

            if (columns == lastColumns && rows == lastRows)
                return;

            lastColumns = columns;
            lastRows = rows;
        }
    }

    /// <summary>
    ///     Приводит игровую область в соответствие с рамкой окна после смены шрифта:
    ///     выравнивает буфер по видимой области и пересчитывает развёрнутое окно
    ///     (restore + maximize), иначе после уменьшения шрифта буфер остаётся старого
    ///     размера — кадр рисуется «немыслимым масштабом», а меню уезжает за экран.
    /// </summary>
    public void RetuneWindow()
    {
        if (!OperatingSystem.IsWindows())
            return;

        SyncBufferToWindow();

        try
        {
            IntPtr window = NativeMethods.GetConsoleWindow();
            if (window != IntPtr.Zero && GetWindowClass(window) == "ConsoleWindowClass"
                                      && NativeMethods.IsZoomed(window))
            {
                NativeMethods.ShowWindow(window, NativeMethods.SwRestore);
                NativeMethods.ShowWindow(window, NativeMethods.SwMaximize);
            }
        }
        catch
        {
        }

        Thread.Sleep(GameConfig.ResizeDelayMs);
    }

    /// <summary>
    ///     Выравнивает буфер консоли по видимой рамке (только классическая консоль):
    ///     без этого буфер шире/выше окна, и вывод в нижнюю строку окна прокручивает
    ///     буфер — картинка «размазывается», рендер уходит за рамку.
    /// </summary>
    public void SyncBufferToWindow()
    {
        if (!OperatingSystem.IsWindows() || IsRunningInWindowsTerminal)
            return;

        try
        {
            IntPtr handle = ConsoleOutput();
            if (handle == IntPtr.Zero || handle == new IntPtr(-1))
                return;

            if (!NativeMethods.GetConsoleScreenBufferInfo(handle, out NativeMethods.ConsoleScreenBufferInfo info))
                return;

            int windowWidth = info.Window.Right - info.Window.Left + 1;
            int windowHeight = info.Window.Bottom - info.Window.Top + 1;

            if (info.BufferSize.X == windowWidth && info.BufferSize.Y == windowHeight)
                return;

            NativeMethods.SetConsoleScreenBufferSize(handle, new NativeMethods.Coord
            {
                X = (short)Math.Max(1, windowWidth),
                Y = (short)Math.Max(1, windowHeight)
            });
        }
        catch
        {
            // Не выровнялось — игра продолжит, но окно может прокручиваться.
        }
    }

    private static string GetWindowClass(IntPtr window)
    {
        var className = new System.Text.StringBuilder(64);
        return NativeMethods.GetClassName(window, className, className.Capacity) > 0
            ? className.ToString()
            : string.Empty;
    }

    /// <summary>Пробует уменьшить размер шрифта консоли. Возвращает <c>false</c>, если не удалось.</summary>
    public bool TrySetFontSize(short size)
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            IntPtr handle = ConsoleOutput();
            if (handle == IntPtr.Zero || handle == new IntPtr(-1))
                return false;

            var info = new NativeMethods.ConsoleFontInfoEx
            {
                Size = (uint)Marshal.SizeOf<NativeMethods.ConsoleFontInfoEx>()
            };

            if (!NativeMethods.GetCurrentConsoleFontEx(handle, false, ref info))
                return false;

            info.FontSize = new NativeMethods.Coord { X = 0, Y = size };
            info.Size = (uint)Marshal.SizeOf<NativeMethods.ConsoleFontInfoEx>();

            return NativeMethods.SetCurrentConsoleFontEx(handle, false, ref info);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Применяет масштаб при старте (обратная совместимость): то же, что
    ///     <see cref="TryApplyQualityZoom" />, — с проверкой, что размер действительно изменился.
    /// </summary>
    public bool TryAutoZoomOut()
    {
        bool ok = TryApplyQualityZoom();
        if (ok)
            LockCurrentZoom();
        return ok;
    }

    /// <summary>
    ///     Ставит масштаб терминала под качество: сначала сбрасывает его к заводскому
    ///     («Ctrl + 0») — это точка отсчёта, — затем жмёт «Ctrl + −» по одному разу и после
    ///     каждого нажатия меряет кадр, пока ширина не станет нужной доли от заводской.
    /// </summary>
    /// <remarks>
    ///     Так результат не зависит ни от того, с какого масштаба мы начали, ни от величины
    ///     ступеней терминала. Прежний вариант «нажать N раз» был неверен: у Windows Terminal
    ///     шаг масштаба мелкий (несколько процентов), поэтому разные качества давали один и тот
    ///     же результат, а любые догадки о «минимуме» не имеют смысла — предел зависит от окна.
    /// </remarks>
    private static bool ApplyTerminalZoom(IntPtr window, double columnsOfBase, out string report)
    {
        ReadWindowSize(out int baseColumns, out int baseRows);
        bool changed = false;

        SendKeyChord(NativeMethods.VirtualKey0);
        if (WaitForSizeChange(baseColumns, baseRows, out int resetColumns, out int resetRows))
        {
            baseColumns = resetColumns;
            baseRows = resetRows;
            changed = true;
        }

        int targetColumns = Math.Max(1, (int)Math.Round(baseColumns * columnsOfBase));
        int columns = baseColumns;
        int rows = baseRows;
        int presses = 0;
        bool hitLimit = false;

        while (presses < GameConfig.MaxZoomDownPresses && columns < targetColumns)
        {
            // Фокус может уехать за время процедуры — перед каждым нажатием подтверждаем его,
            // иначе нажатия уходят в чужое окно и «зум не применяется».
            EnsureFocused(window);

            SendKeyChord(NativeMethods.VirtualKeyOemMinus);
            presses++;

            if (WaitForSizeChange(columns, rows, out int nextColumns, out int nextRows))
            {
                columns = nextColumns;
                rows = nextRows;
                changed = true;
            }
            else
            {
                hitLimit = true; // Мельче терминал уже не может.
                break;
            }
        }

        bool atTarget = columns >= targetColumns - 1;

        // Упереться в предел терминала — это «настолько мелко, насколько можно»: считаем
        // применённым, иначе игра показала бы подсказку настройки, которая уже на пределе.
        bool ok = changed || atTarget || hitLimit;

        report =
            $"заводской={baseColumns}x{baseRows} цель={targetColumns} нажатий={presses} " +
            $"итог={columns}x{rows} изменение={(changed ? "есть" : "нет")}" +
            (hitLimit ? " (предел терминала)" : "");
        return ok;
    }

    /// <summary>
    ///     Ждёт, пока размер кадра изменится: терминал пересчитывает раскладку не мгновенно.
    /// </summary>
    private static bool WaitForSizeChange(int columns, int rows, out int newColumns, out int newRows)
    {
        for (int attempt = 0; attempt < GameConfig.ZoomSettlePolls; attempt++)
        {
            Thread.Sleep(GameConfig.AutoZoomOutSettleMs);
            ReadWindowSize(out newColumns, out newRows);

            if (newColumns != columns || newRows != rows)
                return true;
        }

        newColumns = columns;
        newRows = rows;
        return false;
    }

    /// <summary>Выводит окно терминала вперёд и подтверждает, что фокус действительно наш.</summary>
    private static bool EnsureFocused(IntPtr window)
    {
        for (int attempt = 0; attempt < 4; attempt++)
        {
            if (ConsoleWindowLocator.FocusWindow(window) || NativeMethods.GetForegroundWindow() == window)
                return true;

            Thread.Sleep(100);
        }

        return NativeMethods.GetForegroundWindow() == window;
    }

    /// <summary>Одна посылка «Ctrl + клавиша».</summary>
    private static void SendKeyChord(ushort virtualKey)
    {
        var inputs = new NativeMethods.Input[4];
        inputs[0] = NativeMethods.KeyInput(NativeMethods.VirtualKeyControl, keyUp: false);
        inputs[1] = NativeMethods.KeyInput(virtualKey, keyUp: false);
        inputs[2] = NativeMethods.KeyInput(virtualKey, keyUp: true);
        inputs[3] = NativeMethods.KeyInput(NativeMethods.VirtualKeyControl, keyUp: true);

        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.Input>());
    }

    /// <summary>
    ///     Читает размер игровой области (видимой рамки окна). Сначала пробует консоль
    ///     напрямую (<c>CONOUT$</c>) — это работает и при перенаправленном выводе; если
    ///     не вышло, берёт размер через <see cref="Console" />.
    /// </summary>
    public static void ReadWindowSize(out int columns, out int rows)
    {
        if (OperatingSystem.IsWindows() && TryReadConsoleWindowSize(out columns, out rows))
            return;

        try
        {
            columns = Console.WindowWidth;
            rows = Math.Max(GameConfig.MinimumRows, Console.WindowHeight - 1);
        }
        catch
        {
            columns = GameConfig.FallbackColumns;
            rows = GameConfig.FallbackRows;
        }
    }

    /// <summary>Размер видимой области окна из консольного буфера (нижняя строка — HUD).</summary>
    private static bool TryReadConsoleWindowSize(out int columns, out int rows)
    {
        columns = 0;
        rows = 0;

        IntPtr handle = ConsoleOutput();

        if (!NativeMethods.GetConsoleScreenBufferInfo(handle, out NativeMethods.ConsoleScreenBufferInfo info))
            return false;

        columns = info.Window.Right - info.Window.Left + 1;
        rows = info.Window.Bottom - info.Window.Top + 1;

        if (rows > 1)
            rows -= 1; // последняя видимая строка отводится под строку состояния

        return columns >= 1 && rows >= 1;
    }

    /// <summary>
    ///     Хэндл консольного вывода. Открывается напрямую (<c>CONOUT$</c>) и кэшируется:
    ///     стандартный вывод может быть перенаправлен в файл, и тогда и размер окна, и
    ///     смена шрифта «не видели» консоль — кадр рисовался не под реальную рамку.
    /// </summary>
    private static IntPtr ConsoleOutput()
    {
        if (_consoleOutput == IntPtr.Zero)
        {
            IntPtr direct = NativeMethods.CreateFile(
                "CONOUT$",
                NativeMethods.GenericRead | NativeMethods.GenericWrite,
                NativeMethods.FileShareWrite,
                IntPtr.Zero,
                NativeMethods.OpenExisting,
                0,
                IntPtr.Zero);

            _consoleOutput = direct != IntPtr.Zero && direct != new IntPtr(-1)
                ? direct
                : NativeMethods.GetStdHandle(NativeMethods.StdOutputHandle);
        }

        return _consoleOutput;
    }

    private static IntPtr _consoleOutput;

    /// <summary>
    ///     Показывает экран загрузки. Пока он на экране, игра успевает «отдалить» картинку,
    ///     поэтому настройка масштаба (в том числе эмуляция «Ctrl + −») выглядит как загрузка.
    /// </summary>
    public void ShowLoadingScreen()
    {
        Write(Ansi.ClearScreen + Ansi.Home);

        Console.WriteLine();
        Console.WriteLine("        D O O M  —  консольный рейкастер");
        Console.WriteLine();
        Console.WriteLine("        Загрузка уровня…");
        Console.WriteLine("        Настройка масштаба терминала…");
        Console.WriteLine();
        Console.WriteLine("        Мышь: обзор и огонь (ЛКМ) · WASD: шаги · Esc: выход");
        Console.WriteLine();
    }

    /// <summary>
    ///     Подсказывает, как уменьшить «пиксели». Текст печатается через <see cref="Console" />,
    ///     потому что нужна кириллица (кодировка уже переключена на UTF-8).
    /// </summary>
    public void ShowZoomHint(Func<bool>? anyKeyPressed = null)
    {
        Write(Ansi.ClearScreen + Ansi.Home);

        Console.WriteLine();
        Console.WriteLine("  Для более мелких «пикселей» нажмите несколько раз:");
        Console.WriteLine();
        Console.WriteLine("      Ctrl + −    (уменьшить шрифт терминала)");
        Console.WriteLine();
        Console.WriteLine("  Игра начнётся автоматически через 4 секунды…");

        long start = Environment.TickCount64;
        while (Environment.TickCount64 - start < GameConfig.ZoomHintMilliseconds)
        {
            bool pressed;
            if (anyKeyPressed is not null)
            {
                pressed = anyKeyPressed();
            }
            else if (Console.KeyAvailable)
            {
                Console.ReadKey(true);
                pressed = true;
            }
            else
            {
                pressed = false;
            }

            if (pressed)
                break;

            Thread.Sleep(GameConfig.ZoomHintPollMs);
        }

        Write(Ansi.ClearScreen + Ansi.Home);
    }
}

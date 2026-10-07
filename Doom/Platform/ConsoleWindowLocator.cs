using System.Runtime.InteropServices;
using System.Text;

namespace Doom.Platform;

/// <summary>
///     Поиск окна терминала, которому принадлежит наша консоль. Нужен для разворота окна
///     при старте: в Windows Terminal <c>GetConsoleWindow</c> возвращает скрытую прослойку
///     conhost, поэтому «своё» окно ищем по цепочке процессов — от нашего процесса вверх
///     до WindowsTerminal.exe — и разворачиваем найденное окно этого процесса.
/// </summary>
internal static class ConsoleWindowLocator
{
    /// <summary>Класс окна Windows Terminal.</summary>
    public const string WindowsTerminalClass = "CASCADIA_HOSTING_WINDOW_CLASS";

    /// <summary>Окно — это окно Windows Terminal?</summary>
    public static bool IsWindowsTerminalWindow(IntPtr window) =>
        window != IntPtr.Zero && IsClass(window, WindowsTerminalClass);

    /// <summary>Разворачивает окно нашего терминала. false — окно не найдено.</summary>
    public static bool TryMaximizeOwnTerminalWindow()
    {
        IntPtr window = FindOwnTerminalWindow();
        if (window == IntPtr.Zero)
            return false;

        NativeMethods.ShowWindow(window, NativeMethods.SwMaximize);
        return true;
    }

    /// <summary>Выводит указанное окно вперёд (с обходом запрета для фоновых процессов).</summary>
    public static bool FocusWindow(IntPtr window)
    {
        if (window == IntPtr.Zero)
            return false;

        try
        {
            bool wasMaximized = NativeMethods.IsZoomed(window);

            if (NativeMethods.IsIconic(window))
                NativeMethods.ShowWindow(window, NativeMethods.SwRestore);

            if (ForceForeground(window))
                return true;

            // Принудительная активация тем же механизмом, что Alt+Tab.
            try
            {
                NativeMethods.SwitchToThisWindow(window, true);
                Thread.Sleep(30);

                if (ForceForeground(window))
                    return Restored(window, wasMaximized);
            }
            catch
            {
                // Функция может отсутствовать — переходим к следующему приёму.
            }

            // Последний приём: сворачивание и разворот система активацией разрешает.
            NativeMethods.ShowWindow(window, NativeMethods.SwMinimize);
            NativeMethods.ShowWindow(window, NativeMethods.SwRestore);
            Thread.Sleep(30);

            bool focused = ForceForeground(window);
            return Restored(window, wasMaximized) && focused;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Возвращает окно в развёрнутое состояние, если оно было развёрнуто до активации:
    ///     приёмы со сворачиванием/разворотом в некоторых случаях снимают разворот, а игра
    ///     должна занимать весь экран.
    /// </summary>
    private static bool Restored(IntPtr window, bool wasMaximized)
    {
        if (wasMaximized && !NativeMethods.IsZoomed(window))
            NativeMethods.ShowWindow(window, NativeMethods.SwMaximize);

        return true;
    }

    /// <summary>Просит систему активировать окно и сообщает, получилось ли.</summary>
    private static bool ForceForeground(IntPtr window)
    {
        NativeMethods.BringWindowToTop(window);

        // Классический приём: на время присоединить очередь ввода своего потока к потоку
        // активного окна, затем отцепить.
        uint foregroundThread = NativeMethods.GetWindowThreadProcessId(
            NativeMethods.GetForegroundWindow(), out _);
        uint currentThread = NativeMethods.GetCurrentThreadId();

        bool attached = foregroundThread != currentThread
                        && NativeMethods.AttachThreadInput(foregroundThread, currentThread, true);

        NativeMethods.SetForegroundWindow(window);
        NativeMethods.BringWindowToTop(window);

        if (attached)
            NativeMethods.AttachThreadInput(foregroundThread, currentThread, false);

        return NativeMethods.GetForegroundWindow() == window;
    }

    /// <summary>Ищет окно терминала, чей процесс — предок нашего консольного хостера.</summary>
    public static IntPtr FindOwnTerminalWindow()
    {
        HashSet<uint> ancestors = CollectAncestorProcessIds();
        if (ancestors.Count == 0)
            return IntPtr.Zero;

        IntPtr found = IntPtr.Zero;
        IntPtr hidden = IntPtr.Zero;

        NativeMethods.EnumWindows((window, _) =>
        {
            if (!IsClass(window, WindowsTerminalClass))
                return true;

            NativeMethods.GetWindowThreadProcessId(window, out uint processId);
            if (!ancestors.Contains(processId))
                return true;

            // Окон с этим классом у процесса может быть несколько. EnumWindows идёт сверху
            // вниз, поэтому первое видимое несвёрнутое — самое верхнее: наше окно только что
            // открыто и активно, а свёрнутые — заведомо не наши.
            if (NativeMethods.IsWindowVisible(window) && !NativeMethods.IsIconic(window))
            {
                found = window;
                return false;
            }

            if (hidden == IntPtr.Zero)
                hidden = window;

            return true;
        }, IntPtr.Zero);

        return found != IntPtr.Zero ? found : hidden;
    }

    /// <summary>PID-ы предков нашего процесса (наш → conhost → WindowsTerminal…).</summary>
    private static HashSet<uint> CollectAncestorProcessIds()
    {
        var result = new HashSet<uint>();
        Dictionary<uint, uint> parents = ReadParentMap();

        uint current = (uint)Environment.ProcessId;
        for (int depth = 0; depth < 6; depth++)
        {
            if (!parents.TryGetValue(current, out uint parent) || parent == 0 || parent == current)
                break;

            result.Add(parent);
            current = parent;
        }

        return result;
    }

    private static Dictionary<uint, uint> ReadParentMap()
    {
        var map = new Dictionary<uint, uint>();
        IntPtr snapshot = NativeMethods.CreateToolhelp32Snapshot(NativeMethods.Th32csSnapProcess, 0);
        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
            return map;

        try
        {
            var entry = new NativeMethods.ProcessEntry32
            {
                Size = (uint)Marshal.SizeOf<NativeMethods.ProcessEntry32>()
            };

            if (!NativeMethods.Process32First(snapshot, ref entry))
                return map;

            do
            {
                map[entry.ProcessId] = entry.ParentProcessId;
                entry.Size = (uint)Marshal.SizeOf<NativeMethods.ProcessEntry32>();
            }
            while (NativeMethods.Process32Next(snapshot, ref entry));
        }
        catch
        {
            // Не удалось прочитать список процессов — вернём то, что успели.
        }
        finally
        {
            NativeMethods.CloseHandle(snapshot);
        }

        return map;
    }

    private static bool IsClass(IntPtr window, string expected)
    {
        var buffer = new StringBuilder(64);
        return NativeMethods.GetClassName(window, buffer, buffer.Capacity) > 0
               && buffer.ToString() == expected;
    }
}

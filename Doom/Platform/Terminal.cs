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
    ///     Отключает режим QuickEdit: иначе клик мышью в классической консоли выделяет текст и
    ///     останавливает игру до нажатия клавиши, из-за чего стрелять мышью было бы нельзя.
    /// </summary>
    public void DisableQuickEdit()
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

            uint updated = (mode & ~NativeMethods.EnableQuickEditMode) | NativeMethods.EnableExtendedFlags;
            NativeMethods.SetConsoleMode(handle, updated);
        }
        catch
        {
            // Консоль может быть недоступна (перенаправленный ввод) — продолжаем играть.
        }
    }

    /// <summary>Возвращает прежний режим ввода консоли (включая QuickEdit).</summary>
    public void RestoreQuickEdit()
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

    /// <summary>Пробует уменьшить размер шрифта консоли. Возвращает <c>false</c>, если не удалось.</summary>
    public bool TrySetFontSize(short size)
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            IntPtr handle = NativeMethods.GetStdHandle(NativeMethods.StdOutputHandle);
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
    ///     «Отдаляет» картинку при старте: уменьшает шрифт консольным API, а если это не дало
    ///     видимого эффекта (Windows Terminal игнорирует такой способ) — многократно нажимает
    ///     «Ctrl + −» за пользователя. Возвращает <c>false</c>, если ничего не вышло.
    /// </summary>
    public bool TryAutoZoomOut()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        // В классической консоли шрифт задаётся напрямую — это самый надёжный путь.
        if (!IsRunningInWindowsTerminal && TrySetSmallestFont())
            return true;

        // Windows Terminal игнорирует консольный API, поэтому жмём «Ctrl + −» за пользователя.
        return TryZoomOutWithKeys();
    }

    /// <summary>
    ///     Пытается поставить мелкий шрифт и подтверждает, что он действительно применился: успех
    ///     считается только если окно стало вмещать больше колонок. Иначе возвращает <c>false</c>,
    ///     чтобы вызывающий код перешёл к эмуляции «Ctrl + −» (иначе метод «врал» про успех).
    /// </summary>
    private bool TrySetSmallestFont()
    {
        ReadWindowSize(out int columnsBefore, out _);

        for (short size = GameConfig.MinimumFontSize; size <= GameConfig.DesiredFontSize; size++)
        {
            if (!TrySetFontSize(size))
                continue;

            ReadWindowSize(out int columnsAfter, out _);
            if (columnsAfter > columnsBefore)
                return true;
        }

        return false;
    }

    /// <summary>
    ///     Отправляет в активное окно <see cref="GameSettings.ZoomOutSteps" /> нажатий «Ctrl + −».
    ///     Клавиатура (в отличие от колеса) всегда уходит в активное окно, поэтому приём срабатывает,
    ///     даже если курсор не над терминалом. Успех проверяется по числу колонок: шрифт стал мельче —
    ///     окно вмещает больше символов.
    /// </summary>
    private static bool TryZoomOutWithKeys()
    {
        int steps = GameSettings.Current.ZoomOutSteps;
        if (steps <= 0)
            return false;

        ReadWindowSize(out int columnsBefore, out _);

        var inputs = new NativeMethods.Input[steps * 2 + 2];
        int count = 0;

        inputs[count++] = NativeMethods.KeyInput(NativeMethods.VirtualKeyControl, keyUp: false);
        for (int i = 0; i < steps; i++)
        {
            inputs[count++] = NativeMethods.KeyInput(NativeMethods.VirtualKeyOemMinus, keyUp: false);
            inputs[count++] = NativeMethods.KeyInput(NativeMethods.VirtualKeyOemMinus, keyUp: true);
        }

        inputs[count++] = NativeMethods.KeyInput(NativeMethods.VirtualKeyControl, keyUp: true);

        uint injected = NativeMethods.SendInput((uint)count, inputs, Marshal.SizeOf<NativeMethods.Input>());
        if (injected != count)
            return false;

        Thread.Sleep(GameConfig.AutoZoomOutSettleMs);

        ReadWindowSize(out int columnsAfter, out _);
        return columnsAfter > columnsBefore;
    }

    /// <summary>Просит терминал изменить размер окна.</summary>
    public void TryResize(int columns, int rows)
    {
        Write(Ansi.Resize(columns, rows));
        Thread.Sleep(GameConfig.ResizeDelayMs);
    }

    /// <summary>
    ///     Читает размер игровой области. Если консоль недоступна, возвращает размер по умолчанию.
    /// </summary>
    public static void ReadWindowSize(out int columns, out int rows)
    {
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
    public void ShowZoomHint()
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
            if (Console.KeyAvailable)
            {
                Console.ReadKey(true);
                break;
            }

            Thread.Sleep(GameConfig.ZoomHintPollMs);
        }

        Write(Ansi.ClearScreen + Ansi.Home);
    }
}

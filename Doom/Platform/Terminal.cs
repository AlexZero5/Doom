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

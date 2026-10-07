namespace Doom.Platform;

/// <summary>Управляющие последовательности ANSI, которые использует игра.</summary>
internal static class Ansi
{
    public const string Home = "\x1b[H";
    public const string ClearScreen = "\x1b[2J";
    public const string ResetColors = "\x1b[0m";
    public const string HideCursor = "\x1b[?25l";
    public const string ShowCursor = "\x1b[?25h";

    /// <summary>Отчёт о мыши: клики (1000), любое движение (1003) и SGR-кодирование (1006).</summary>
    public const string EnableMouseReporting = "\x1b[?1000;1003;1006h";
    public const string DisableMouseReporting = "\x1b[?1000;1003;1006l";

    /// <summary>
    ///     Автоперенос строки (DECAWM). Игра отключает его: с включённым переносом запись
    ///     в последнюю ячейку строки переводит курсор на следующую строку, а на последней
    ///     строке скроллит экран — всё содержимое сдвигается и рассинхронизируется с кадром.
    /// </summary>
    public const string DisableAutoWrap = "\x1b[?7l";
    public const string EnableAutoWrap = "\x1b[?7h";

    /// <summary>Запрос на изменение размеров окна терминала.</summary>
    public static string Resize(int columns, int rows) => $"\x1b[8;{rows};{columns}t";
}

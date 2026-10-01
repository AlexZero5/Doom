namespace Doom.Platform;

/// <summary>Управляющие последовательности ANSI, которые использует игра.</summary>
internal static class Ansi
{
    public const string Home = "\x1b[H";
    public const string ClearScreen = "\x1b[2J";
    public const string ResetColors = "\x1b[0m";
    public const string HideCursor = "\x1b[?25l";
    public const string ShowCursor = "\x1b[?25h";

    /// <summary>Запрос на изменение размеров окна терминала.</summary>
    public static string Resize(int columns, int rows) => $"\x1b[8;{rows};{columns}t";
}

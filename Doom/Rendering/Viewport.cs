using Doom.Configuration;

namespace Doom.Rendering;

/// <summary>
///     Область трёхмерного вида на экране: столбцы и строки терминала над строкой состояния.
///     «Пиксель» — половина ячейки по вертикали, поэтому пиксельных строк вдвое больше.
/// </summary>
internal readonly struct Viewport
{
    public Viewport(int columns, int rows)
    {
        Columns = columns;
        Rows = rows;
        PixelRows = rows * 2;
        FieldOfViewRadians = GameSettings.Current.FieldOfViewRadians;
    }

    public int Columns { get; }

    public int Rows { get; }

    public int PixelRows { get; }

    /// <summary>Горизонтальный угол обзора.</summary>
    public double FieldOfViewRadians { get; }
}

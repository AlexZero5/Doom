using Doom.Configuration;

namespace Doom.Rendering;

/// <summary>
///     Размеры игровой области в символах и предвычисленные параметры камеры.
///     Один символ по вертикали равен двум «пикселям» (символ ▀).
/// </summary>
internal readonly struct Viewport
{
    public Viewport(int columns, int rows)
    {
        Columns = columns;
        Rows = rows;
        PixelRows = rows * 2;
        TanHalfFieldOfView = Math.Tan(GameConfig.HalfFieldOfView);
    }

    public int Columns { get; }

    public int Rows { get; }

    /// <summary>Вертикальное разрешение в пикселях (два пикселя на символ).</summary>
    public int PixelRows { get; }

    public double TanHalfFieldOfView { get; }

    /// <summary>Сколько лучей трассировать на одну колонку при текущей ширине кадра.</summary>
    public int RaysPerColumn => GameConfig.RaysPerColumn(Columns);
}

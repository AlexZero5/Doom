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

        FieldOfViewRadians = GameSettings.Current.FieldOfViewRadians;
        HalfFieldOfViewRadians = GameSettings.Current.HalfFieldOfViewRadians;
        TanHalfFieldOfView = Math.Tan(HalfFieldOfViewRadians);
    }

    public int Columns { get; }

    public int Rows { get; }

    /// <summary>Вертикальное разрешение в пикселях (два пикселя на символ).</summary>
    public int PixelRows { get; }

    public double TanHalfFieldOfView { get; }

    /// <summary>Угол обзора по горизонтали в радианах (берётся из настроек).</summary>
    public double FieldOfViewRadians { get; }

    /// <summary>Половина угла обзора в радианах.</summary>
    public double HalfFieldOfViewRadians { get; }

    /// <summary>Сколько лучей трассировать на одну колонку при текущей ширине кадра.</summary>
    public int RaysPerColumn => GameConfig.RaysPerColumn(Columns);

    /// <summary>
    ///     Фокусное расстояние в «пикселях» кадра (пиксели квадратные): связь угла и строки экрана.
    ///     Нужно для вертикального обзора и семплирования неба.
    /// </summary>
    public double FocalPixels => Columns * 0.5 / TanHalfFieldOfView;

    /// <summary>
    ///     Строка кадра («пиксель»), на которой лежит горизонт при данном наклоне взгляда.
    ///     Взгляд вверх сдвигает горизонт вниз по экрану — вертикальный обзор сделан сдвигом
    ///     кадра (Y-shear), поэтому геометрия стен не искажается. Предел — полторы высоты
    ///     кадра: при таком сдвиге стены (даже вплотную) уезжают с экрана и видно сплошной
    ///     потолок (взгляд вверх) или сплошной пол (вниз).
    /// </summary>
    public double HorizonPixels(double pitch)
    {
        double shift = Math.Tan(pitch) * FocalPixels;
        double limit = PixelRows * 1.5;

        if (shift > limit) shift = limit;
        if (shift < -limit) shift = -limit;

        return PixelRows * 0.5 + shift;
    }
}

namespace Doom.World;

/// <summary>
///     Генераторы мешей 3D-объектов. Все размеры — в единицах мира (1.0 = высота стены);
///     меш центрируется по горизонтали вокруг (0,0), по вертикали Z указывается явно.
///     Текстуры — номера файлов assets/walls/.
/// </summary>
/// <remarks>
///     Лицевая сторона полигона определяется порядком вершин (нормаль e03 × e01), а
///     растеризатор отбрасывает грани, повёрнутые к камере спиной. Поэтому порядок здесь
///     важен: вертикальные грани обходятся так, чтобы «снаружи» было справа по ходу
///     ребра [0]→[1] (если смотреть сверху), а горизонтальные, смотрящие вверх, — по часовой
///     стрелке (см. <see cref="Up" />).
/// </remarks>
internal static class MeshFactory
{
    /// <summary>Прямоугольный бокс: четыре стенки, крыша (вверх) и дно (вниз).</summary>
    public static Mesh3D Box(
        double width, double depth, double height, double zBottom,
        int textureId, int topTextureId = 0, int bottomTextureId = 0) =>
        Box(width, depth, height, zBottom,
            new BoxSkin(textureId, textureId, textureId, textureId,
                topTextureId != 0 ? topTextureId : textureId, bottomTextureId != 0 ? bottomTextureId : textureId));

    /// <summary>
    ///     Бокс с текстурой на каждую грань (фасад шкафа, дверца холодильника). Если задан
    ///     <paramref name="uvScale" />, текстура повторяется в мировом масштабе (одна текстура на
    ///     <paramref name="uvScale" /> единиц), иначе растягивается на грань целиком.
    /// </summary>
    public static Mesh3D Box(double width, double depth, double height, double zBottom, BoxSkin skin, double uvScale = 0)
    {
        double hw = width * 0.5;
        double hd = depth * 0.5;
        double zTop = zBottom + height;

        double U(double size) => uvScale > 0 ? size / uvScale : 1.0;

        var quads = new Quad3D[6];

        // Стенки (обход основания против часовой стрелки — «снаружи» справа по ходу):
        // север (−y), восток (+x), юг (+y), запад (−x).
        quads[0] = Face(-hw, -hd, +hw, -hd, zBottom, zTop, skin.North, -1, U(width), U(height));
        quads[1] = Face(+hw, -hd, +hw, +hd, zBottom, zTop, skin.East, -1, U(depth), U(height));
        quads[2] = Face(+hw, +hd, -hw, +hd, zBottom, zTop, skin.South, -1, U(width), U(height));
        quads[3] = Face(-hw, +hd, -hw, -hd, zBottom, zTop, skin.West, -1, U(depth), U(height));

        quads[4] = Up(-hw, -hd, +hw, +hd, zTop, skin.Top, +1, U(depth), U(width));
        quads[5] = Down(-hw, -hd, +hw, +hd, zBottom, skin.Bottom, -1, U(width), U(depth));

        return new Mesh3D(quads);
    }

    /// <summary>
    ///     Двускатная крыша: конёк вдоль локальной оси X, скаты на север и юг, треугольные фронтоны
    ///     на концах. Размеры — по основанию (с выносом карниза <paramref name="overhang" />).
    /// </summary>
    public static Mesh3D Roof(double length, double width, double height, double zBottom, double overhang,
        int roofTexture, int gableTexture, double uvScale = 0.5)
    {
        double hl = length * 0.5 + overhang;
        double hw = width * 0.5 + overhang;
        double gl = length * 0.5; // фронтоны стоят на стенах, без выноса
        double gw = width * 0.5;
        double zTop = zBottom + height;
        double slope = Math.Sqrt(hw * hw + height * height);
        double eave = zBottom - overhang * height / gw; // карниз продолжает скат вниз

        double uLength = hl * 2 / uvScale, vSlope = slope / uvScale;

        return new Mesh3D(new[]
        {
            // Скат на север и скат на юг (лицом вверх-наружу).
            new Quad3D(-hl, 0, zTop, hl, 0, zTop, hl, -hw, eave, -hl, -hw, eave, 0, 0, uLength, vSlope, roofTexture, 1),
            new Quad3D(hl, 0, zTop, -hl, 0, zTop, -hl, hw, eave, hl, hw, eave, 0, 0, uLength, vSlope, roofTexture, 1),

            // Фронтоны: каждый — два треугольника от конька к краю.
            new Quad3D(-gl, 0, zTop, -gl, -gw, zBottom, -gl, 0, zBottom, -gl, 0, zBottom, 0, 0, gw / uvScale, height / uvScale, gableTexture, -1),
            new Quad3D(-gl, gw, zBottom, -gl, 0, zTop, -gl, 0, zBottom, -gl, 0, zBottom, 0, 0, gw / uvScale, height / uvScale, gableTexture, -1),
            new Quad3D(gl, -gw, zBottom, gl, 0, zTop, gl, 0, zBottom, gl, 0, zBottom, 0, 0, gw / uvScale, height / uvScale, gableTexture, -1),
            new Quad3D(gl, 0, zTop, gl, gw, zBottom, gl, 0, zBottom, gl, 0, zBottom, 0, 0, gw / uvScale, height / uvScale, gableTexture, -1),
        });
    }

    /// <summary>Цилиндр из <paramref name="segments" /> граней (12–24 выглядят круглым) с крышей-веером.</summary>
    public static Mesh3D Cylinder(
        double radius, double height, double zBottom, int textureId, int segments = 16)
    {
        segments = Math.Clamp(segments, 6, 64);
        double zTop = zBottom + height;

        var quads = new Quad3D[segments * 2];
        int quadIndex = 0;

        for (int segment = 0; segment < segments; segment++)
        {
            double angle0 = Math.Tau * segment / segments;
            double angle1 = Math.Tau * (segment + 1) / segments;

            double x0 = Math.Cos(angle0) * radius;
            double y0 = Math.Sin(angle0) * radius;
            double x1 = Math.Cos(angle1) * radius;
            double y1 = Math.Sin(angle1) * radius;

            // Боковая грань: обход против часовой стрелки, нормаль наружу.
            quads[quadIndex++] = new Quad3D(
                x0, y0, zTop,
                x1, y1, zTop,
                x1, y1, zBottom,
                x0, y0, zBottom,
                (double)segment / segments, 0.0, (double)(segment + 1) / segments, 1.0,
                textureId, shadeBias: -1);

            quads[quadIndex++] = FanSector(x0, y0, x1, y1, zTop, angle0, angle1, textureId);
        }

        return new Mesh3D(quads, Footprint.Circle);
    }

    /// <summary>
    ///     Пандус: наклонная плоскость вдоль локальной оси X — от низа (−length/2) до верха
    ///     (+length/2), полосами по <paramref name="steps" />; на высоком конце — стенка,
    ///     по бокам — треугольные боковины.
    /// </summary>
    public static Mesh3D Ramp(
        double width, double length, double height, double zBottom,
        int textureId, int steps = 8)
    {
        steps = Math.Clamp(steps, 1, 64);
        double hw = width * 0.5;
        double hl = length * 0.5;
        double zTop = zBottom + height;

        var quads = new Quad3D[steps + 3];
        double stepLength = length / steps;
        double stepHeight = height / steps;

        for (int step = 0; step < steps; step++)
        {
            double x0 = -hl + step * stepLength;
            double x1 = x0 + stepLength;
            double zA = zBottom + step * stepHeight;
            double zB = zA + stepHeight;

            // Лицом вверх: ребро [0]→[1] — поперёк склона, [0]→[3] — вверх по склону.
            quads[step] = new Quad3D(
                x0, -hw, zA,
                x0, +hw, zA,
                x1, +hw, zB,
                x1, -hw, zB,
                0.0, (double)step / steps, 1.0, (double)(step + 1) / steps,
                textureId);
        }

        // Стенка на высоком конце, лицом в +X.
        quads[steps] = Face(+hl, -hw, +hl, +hw, zBottom, zTop, textureId, shadeBias: -1);

        // Боковины-треугольники: верхний край идёт по склону, нижний — по полу.
        quads[steps + 1] = new Quad3D(
            -hl, -hw, zBottom,
            +hl, -hw, zTop,
            +hl, -hw, zBottom,
            +hl, -hw, zBottom,
            0.0, 0.0, 1.0, 1.0,
            textureId, shadeBias: -1);

        quads[steps + 2] = new Quad3D(
            +hl, +hw, zTop,
            -hl, +hw, zBottom,
            +hl, +hw, zBottom,
            +hl, +hw, zBottom,
            0.0, 0.0, 1.0, 1.0,
            textureId, shadeBias: -1);

        return new Mesh3D(quads);
    }

    /// <summary>
    ///     Рельеф: сетка <paramref name="subdivisions" />×<paramref name="subdivisions" /> клеток,
    ///     высота каждой вершины из функции. Клетка режется на два треугольника: четыре точки
    ///     рельефа почти никогда не лежат в одной плоскости, и квадом между соседями
    ///     оставались бы щели.
    /// </summary>
    public static Mesh3D Heightfield(
        double size,
        int subdivisions,
        Func<double, double, double> heightAt,
        int textureId,
        double zBase = 0.0,
        double uvRepeat = 1.0)
    {
        subdivisions = Math.Clamp(subdivisions, 1, 64);

        var heights = new double[subdivisions + 1, subdivisions + 1];
        double half = size * 0.5;
        double cell = size / subdivisions;

        for (int row = 0; row <= subdivisions; row++)
        {
            for (int column = 0; column <= subdivisions; column++)
                heights[row, column] = zBase + heightAt(-half + column * cell, -half + row * cell);
        }

        var quads = new Quad3D[subdivisions * subdivisions * 2];
        int quadIndex = 0;

        for (int row = 0; row < subdivisions; row++)
        {
            for (int column = 0; column < subdivisions; column++)
            {
                double x0 = -half + column * cell;
                double x1 = x0 + cell;
                double y0 = -half + row * cell;
                double y1 = y0 + cell;

                double z00 = heights[row, column];
                double z10 = heights[row, column + 1];
                double z11 = heights[row + 1, column + 1];
                double z01 = heights[row + 1, column];

                double u0 = uvRepeat * column / subdivisions;
                double u1 = uvRepeat * (column + 1) / subdivisions;
                double v0 = uvRepeat * row / subdivisions;
                double v1 = uvRepeat * (row + 1) / subdivisions;

                // Оба треугольника лицом вверх и делят диагональ (x1,y0)–(x0,y1).
                quads[quadIndex++] = new Quad3D(
                    x0, y0, z00,
                    x0, y1, z01,
                    x1, y0, z10,
                    x1, y0, z10,
                    v0, u0, v1, u1,
                    textureId);

                quads[quadIndex++] = new Quad3D(
                    x1, y1, z11,
                    x1, y0, z10,
                    x0, y1, z01,
                    x0, y1, z01,
                    v1, u1, v0, u0,
                    textureId);
            }
        }

        return new Mesh3D(quads);
    }

    /// <summary>Плоский диск лицом вверх (круглая площадка): веер треугольников.</summary>
    public static Mesh3D Disc(double radius, double z, int textureId, int segments = 16)
    {
        segments = Math.Clamp(segments, 6, 64);

        var quads = new Quad3D[segments];

        for (int segment = 0; segment < segments; segment++)
        {
            double angle0 = Math.Tau * segment / segments;
            double angle1 = Math.Tau * (segment + 1) / segments;

            quads[segment] = FanSector(
                Math.Cos(angle0) * radius, Math.Sin(angle0) * radius,
                Math.Cos(angle1) * radius, Math.Sin(angle1) * radius,
                z, angle0, angle1, textureId);
        }

        return new Mesh3D(quads, Footprint.Circle);
    }

    /// <summary>Сектор веера (треугольник центр–край) лицом вверх.</summary>
    private static Quad3D FanSector(
        double x0, double y0, double x1, double y1, double z, double angle0, double angle1, int textureId) =>
        new(
            0, 0, z,
            x1, y1, z,
            x0, y0, z,
            x0, y0, z,
            0.5, 0.5,
            0.5 + 0.5 * Math.Cos(angle1), 0.5 + 0.5 * Math.Sin(angle0),
            textureId, shadeBias: +1);

    /// <summary>Вертикальная грань от (x0,y0) до (x1,y1) между zBottom и zTop; «снаружи» — справа по ходу.</summary>
    private static Quad3D Face(
        double x0, double y0, double x1, double y1,
        double zBottom, double zTop, int textureId, int shadeBias, double uMax = 1, double vMax = 1) =>
        new(
            x0, y0, zTop,
            x1, y1, zTop,
            x1, y1, zBottom,
            x0, y0, zBottom,
            0.0, 0.0, uMax, vMax,
            textureId, shadeBias);

    /// <summary>Горизонтальный прямоугольник [minX..maxX]×[minY..maxY] лицом вверх.</summary>
    private static Quad3D Up(double minX, double minY, double maxX, double maxY, double z, int textureId,
        int shadeBias, double uMax = 1, double vMax = 1) =>
        new(
            minX, minY, z,
            minX, maxY, z,
            maxX, maxY, z,
            maxX, minY, z,
            0.0, 0.0, uMax, vMax,
            textureId, shadeBias);

    /// <summary>Горизонтальный прямоугольник лицом вниз (дно парящего объекта).</summary>
    private static Quad3D Down(double minX, double minY, double maxX, double maxY, double z, int textureId,
        int shadeBias, double uMax = 1, double vMax = 1) =>
        new(
            minX, minY, z,
            maxX, minY, z,
            maxX, maxY, z,
            minX, maxY, z,
            0.0, 0.0, uMax, vMax,
            textureId, shadeBias);
}

/// <summary>Текстуры граней бокса: север (−y), восток (+x), юг (+y), запад (−x), верх, низ.</summary>
internal readonly record struct BoxSkin(int North, int East, int South, int West, int Top, int Bottom)
{
    public static BoxSkin All(int texture) => new(texture, texture, texture, texture, texture, texture);

    /// <summary>Все грани одной текстурой, «лицо» (север) — другой.</summary>
    public static BoxSkin Front(int body, int front) => new(front, body, body, body, body, body);
}

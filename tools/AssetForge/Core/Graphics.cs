namespace AssetForge.Core;

/// <summary>Цвет RGB (0..255) с удобной арифметикой для «ручной» раскраски.</summary>
internal readonly record struct Rgb(int R, int G, int B)
{
    public static Rgb Hex(int value) => new((value >> 16) & 0xFF, (value >> 8) & 0xFF, value & 0xFF);

    public int Argb(int alpha = 255) =>
        (alpha << 24) | (Clamp(R) << 16) | (Clamp(G) << 8) | Clamp(B);

    public static Rgb FromArgb(int argb) => new((argb >> 16) & 0xFF, (argb >> 8) & 0xFF, argb & 0xFF);

    public Rgb Scale(double factor) => new((int)(R * factor), (int)(G * factor), (int)(B * factor));

    public Rgb Lerp(Rgb other, double t) => new(
        (int)Math.Round(R + (other.R - R) * t),
        (int)Math.Round(G + (other.G - G) * t),
        (int)Math.Round(B + (other.B - B) * t));

    public Rgb Add(int amount) => new(R + amount, G + amount, B + amount);

    public double Luma => (R * 0.299 + G * 0.587 + B * 0.114) / 255.0;

    private static int Clamp(int value) => value < 0 ? 0 : value > 255 ? 255 : value;

    /// <summary>Цвет вдоль «рампы»: t = 0 — первый опорный цвет, 1 — последний.</summary>
    public static Rgb Ramp(double t, params Rgb[] stops)
    {
        t = Math.Clamp(t, 0, 1) * (stops.Length - 1);
        int index = Math.Min((int)t, stops.Length - 2);
        return stops[index].Lerp(stops[index + 1], t - index);
    }
}

/// <summary>Изображение ARGB с примитивами рисования.</summary>
internal sealed class Canvas
{
    public Canvas(int width, int height)
    {
        Width = width;
        Height = height;
        Pixels = new int[width * height];
    }

    public int Width { get; }

    public int Height { get; }

    public int[] Pixels { get; }

    public int this[int x, int y]
    {
        get => Pixels[y * Width + x];
        set => Pixels[y * Width + x] = value;
    }

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public int AlphaAt(int x, int y) => InBounds(x, y) ? (int)((uint)this[x, y] >> 24) : 0;

    public bool Opaque(int x, int y) => AlphaAt(x, y) >= 128;

    public Rgb ColorAt(int x, int y) => Rgb.FromArgb(this[x, y]);

    public void Plot(int x, int y, Rgb color)
    {
        if (InBounds(x, y))
            this[x, y] = color.Argb();
    }

    public void Clear() => Array.Clear(Pixels);

    public void FillRect(int x0, int y0, int width, int height, Rgb color)
    {
        for (int y = y0; y < y0 + height; y++)
        for (int x = x0; x < x0 + width; x++)
            Plot(x, y, color);
    }

    /// <summary>Закрашивает маску цветом, который даёт шейдер (x, y → цвет).</summary>
    public void Paint(Mask mask, Func<int, int, Rgb> shader, int offsetX = 0, int offsetY = 0)
    {
        for (int y = 0; y < mask.Height; y++)
        for (int x = 0; x < mask.Width; x++)
        {
            if (mask[x, y])
                Plot(x + offsetX, y + offsetY, shader(x, y));
        }
    }

    public void Paint(Mask mask, Rgb color) => Paint(mask, (_, _) => color);

    /// <summary>Накладывает другой холст (непрозрачные пиксели) со сдвигом.</summary>
    public void Blit(Canvas source, int dx, int dy)
    {
        for (int y = 0; y < source.Height; y++)
        for (int x = 0; x < source.Width; x++)
        {
            if (source.Opaque(x, y) && InBounds(x + dx, y + dy))
                this[x + dx, y + dy] = source[x, y];
        }
    }

    /// <summary>Обводка: прозрачные пиксели, соседние с непрозрачными, красятся цветом.</summary>
    public void Outline(Rgb color, bool diagonals = false)
    {
        var copy = (int[])Pixels.Clone();
        bool OpaqueIn(int x, int y) =>
            x >= 0 && y >= 0 && x < Width && y < Height && (uint)copy[y * Width + x] >> 24 >= 128;

        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            if (OpaqueIn(x, y))
                continue;

            bool near = OpaqueIn(x - 1, y) || OpaqueIn(x + 1, y) || OpaqueIn(x, y - 1) || OpaqueIn(x, y + 1)
                        || diagonals && (OpaqueIn(x - 1, y - 1) || OpaqueIn(x + 1, y - 1)
                                         || OpaqueIn(x - 1, y + 1) || OpaqueIn(x + 1, y + 1));
            if (near)
                this[x, y] = color.Argb();
        }
    }

    /// <summary>Маска непрозрачных пикселей.</summary>
    public Mask ToMask()
    {
        var mask = new Mask(Width, Height);
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            mask[x, y] = Opaque(x, y);
        return mask;
    }

    public Canvas Crop(int x0, int y0, int width, int height)
    {
        var result = new Canvas(width, height);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (InBounds(x + x0, y + y0))
                result[x, y] = this[x + x0, y + y0];
        }

        return result;
    }

    /// <summary>Подрезает прозрачные поля, оставляя <paramref name="margin" /> пикселей.</summary>
    public Canvas Trim(int margin = 0)
    {
        int minX = Width, minY = Height, maxX = -1, maxY = -1;
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            if (!Opaque(x, y))
                continue;
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }

        if (maxX < 0)
            return new Canvas(1, 1);

        return Crop(minX - margin, minY - margin, maxX - minX + 1 + margin * 2, maxY - minY + 1 + margin * 2);
    }

    public Canvas FlipHorizontal()
    {
        var result = new Canvas(Width, Height);
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            result[Width - 1 - x, y] = this[x, y];
        return result;
    }
}

/// <summary>Битовая маска фигуры: из неё рисуется заливка, рельеф и обводка.</summary>
internal sealed class Mask
{
    private readonly bool[] _bits;

    public Mask(int width, int height)
    {
        Width = width;
        Height = height;
        _bits = new bool[width * height];
    }

    public int Width { get; }

    public int Height { get; }

    public bool this[int x, int y]
    {
        get => x >= 0 && y >= 0 && x < Width && y < Height && _bits[y * Width + x];
        set
        {
            if (x >= 0 && y >= 0 && x < Width && y < Height)
                _bits[y * Width + x] = value;
        }
    }

    public Mask Union(Mask other)
    {
        var result = new Mask(Width, Height);
        for (int i = 0; i < _bits.Length; i++)
            result._bits[i] = _bits[i] || other._bits[i];
        return result;
    }

    public Mask Subtract(Mask other)
    {
        var result = new Mask(Width, Height);
        for (int i = 0; i < _bits.Length; i++)
            result._bits[i] = _bits[i] && !other._bits[i];
        return result;
    }

    public Mask Intersect(Mask other)
    {
        var result = new Mask(Width, Height);
        for (int i = 0; i < _bits.Length; i++)
            result._bits[i] = _bits[i] && other._bits[i];
        return result;
    }

    public bool Any() => _bits.Any(bit => bit);

    /// <summary>Многоугольник (чётно-нечётное правило), вершины в пикселях.</summary>
    public static Mask Polygon(int width, int height, params (double X, double Y)[] points)
    {
        var mask = new Mask(width, height);
        var crossings = new List<double>();

        for (int y = 0; y < height; y++)
        {
            double sampleY = y + 0.5;
            crossings.Clear();

            for (int i = 0; i < points.Length; i++)
            {
                (double x0, double y0) = points[i];
                (double x1, double y1) = points[(i + 1) % points.Length];
                if (y0 <= sampleY && y1 > sampleY || y1 <= sampleY && y0 > sampleY)
                    crossings.Add(x0 + (sampleY - y0) / (y1 - y0) * (x1 - x0));
            }

            crossings.Sort();
            for (int i = 0; i + 1 < crossings.Count; i += 2)
            {
                int from = (int)Math.Ceiling(crossings[i] - 0.5);
                int to = (int)Math.Floor(crossings[i + 1] - 0.5);
                for (int x = from; x <= to; x++)
                    mask[x, y] = true;
            }
        }

        return mask;
    }

    public static Mask Ellipse(int width, int height, double centerX, double centerY, double radiusX, double radiusY)
    {
        var mask = new Mask(width, height);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            double dx = (x + 0.5 - centerX) / radiusX;
            double dy = (y + 0.5 - centerY) / radiusY;
            mask[x, y] = dx * dx + dy * dy <= 1.0;
        }

        return mask;
    }

    public static Mask Rect(int width, int height, double x0, double y0, double rectWidth, double rectHeight) =>
        Polygon(width, height, (x0, y0), (x0 + rectWidth, y0), (x0 + rectWidth, y0 + rectHeight), (x0, y0 + rectHeight));

    /// <summary>Скруглённый прямоугольник.</summary>
    public static Mask RoundRect(int width, int height, double x0, double y0, double rectWidth, double rectHeight,
        double radius)
    {
        var mask = new Mask(width, height);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            double px = x + 0.5, py = y + 0.5;
            if (px < x0 || py < y0 || px > x0 + rectWidth || py > y0 + rectHeight)
                continue;

            double cx = Math.Clamp(px, x0 + radius, x0 + rectWidth - radius);
            double cy = Math.Clamp(py, y0 + radius, y0 + rectHeight - radius);
            double dx = px - cx, dy = py - cy;
            mask[x, y] = dx * dx + dy * dy <= radius * radius;
        }

        return mask;
    }

    /// <summary>Толстая линия (капсула) от A до B.</summary>
    public static Mask Line(int width, int height, double ax, double ay, double bx, double by, double thickness)
    {
        var mask = new Mask(width, height);
        double r = thickness * 0.5;
        double lengthSquared = (bx - ax) * (bx - ax) + (by - ay) * (by - ay);

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            double px = x + 0.5, py = y + 0.5;
            double t = lengthSquared < 1e-9 ? 0 : Math.Clamp(((px - ax) * (bx - ax) + (py - ay) * (by - ay)) / lengthSquared, 0, 1);
            double dx = px - (ax + t * (bx - ax));
            double dy = py - (ay + t * (by - ay));
            mask[x, y] = dx * dx + dy * dy <= r * r;
        }

        return mask;
    }

    /// <summary>Расстояние (в пикселях) от каждого пикселя маски до её края.</summary>
    public double[] DistanceToEdge()
    {
        const double inf = 1e9;
        var distance = new double[Width * Height];
        for (int i = 0; i < distance.Length; i++)
            distance[i] = _bits[i] ? inf : 0;

        double At(int x, int y) => x < 0 || y < 0 || x >= Width || y >= Height ? 0 : distance[y * Width + x];

        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            int i = y * Width + x;
            if (distance[i] == 0) continue;
            distance[i] = Math.Min(distance[i], Math.Min(At(x - 1, y) + 1, At(x, y - 1) + 1));
            distance[i] = Math.Min(distance[i], Math.Min(At(x - 1, y - 1) + 1.414, At(x + 1, y - 1) + 1.414));
        }

        for (int y = Height - 1; y >= 0; y--)
        for (int x = Width - 1; x >= 0; x--)
        {
            int i = y * Width + x;
            if (distance[i] == 0) continue;
            distance[i] = Math.Min(distance[i], Math.Min(At(x + 1, y) + 1, At(x, y + 1) + 1));
            distance[i] = Math.Min(distance[i], Math.Min(At(x + 1, y + 1) + 1.414, At(x - 1, y + 1) + 1.414));
        }

        return distance;
    }
}

/// <summary>
///     Рельеф «подушкой»: из маски строится высота (расстояние до края, срезанное на
///     <c>depth</c>), из неё — нормали и освещение светом сверху-слева. Множитель: 1 — плоская
///     середина, больше — освещённый скос, меньше — теневой.
/// </summary>
internal static class Relief
{
    public static double[] Light(Mask mask, double depth, double strength = 1.0, double lightX = -0.55, double lightY = -0.75)
    {
        double[] distance = mask.DistanceToEdge();
        int width = mask.Width, height = mask.Height;
        var result = new double[width * height];

        double Height(int x, int y) =>
            x < 0 || y < 0 || x >= width || y >= height ? 0 : Math.Min(distance[y * width + x], depth) / depth;

        double lz = 0.9;
        double length = Math.Sqrt(lightX * lightX + lightY * lightY + lz * lz);
        lightX /= length;
        lightY /= length;
        lz /= length;

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            if (!mask[x, y])
                continue;

            double gx = (Height(x + 1, y) - Height(x - 1, y)) * 0.5 * strength * 2.2;
            double gy = (Height(x, y + 1) - Height(x, y - 1)) * 0.5 * strength * 2.2;
            double nx = -gx, ny = -gy, nz = 1.0;
            double n = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            double lambert = (nx * lightX + ny * lightY + nz * lz) / n;
            result[y * width + x] = Math.Clamp(lambert / lz, 0.35, 1.6);
        }

        return result;
    }
}

/// <summary>Детерминированный шум: хэш, значение-шум с плавной интерполяцией, fBm. Всё тайлится.</summary>
internal static class Noise
{
    public static uint Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }
    }

    /// <summary>Случайное 0..1 для целой точки.</summary>
    public static double Unit(int x, int y, int seed) => (Hash(x, y, seed) & 0xFFFFFF) / 16777215.0;

    /// <summary>Значение-шум 0..1 с периодом <paramref name="period" /> клеток (повторяется).</summary>
    public static double Value(double x, double y, int period, int seed, int cells)
    {
        double fx = x / period * cells, fy = y / period * cells;
        int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
        double tx = fx - x0, ty = fy - y0;
        tx = tx * tx * (3 - 2 * tx);
        ty = ty * ty * (3 - 2 * ty);

        double Corner(int cx, int cy) => Unit(((cx % cells) + cells) % cells, ((cy % cells) + cells) % cells, seed);

        double a = Corner(x0, y0), b = Corner(x0 + 1, y0);
        double c = Corner(x0, y0 + 1), d = Corner(x0 + 1, y0 + 1);
        return (a + (b - a) * tx) + ((c + (d - c) * tx) - (a + (b - a) * tx)) * ty;
    }

    /// <summary>
    ///     Анизотропный фрактальный шум (волокна дерева, травинки): разное число ячеек по осям,
    ///     при этом тайлится с периодом <paramref name="period" /> по обеим.
    /// </summary>
    public static double Streaks(double x, double y, int period, int seed, int cellsX, int cellsY, int octaves = 3)
    {
        double sum = 0, amplitude = 1, total = 0;

        for (int octave = 0; octave < octaves; octave++)
        {
            int cx = cellsX << octave, cy = cellsY << octave;
            double fx = x / period * cx, fy = y / period * cy;
            int x0 = (int)Math.Floor(fx), y0 = (int)Math.Floor(fy);
            double tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3 - 2 * tx);
            ty = ty * ty * (3 - 2 * ty);

            int s = seed + octave * 101;
            double Corner(int px, int py) => Unit(((px % cx) + cx) % cx, ((py % cy) + cy) % cy, s);

            double a = Corner(x0, y0), b = Corner(x0 + 1, y0);
            double c = Corner(x0, y0 + 1), d = Corner(x0 + 1, y0 + 1);
            double top = a + (b - a) * tx, bottom = c + (d - c) * tx;
            sum += (top + (bottom - top) * ty) * amplitude;
            total += amplitude;
            amplitude *= 0.5;
        }

        return sum / total;
    }

    /// <summary>Фрактальный шум 0..1: октавы с удвоением частоты, тайлится с периодом <paramref name="period" />.</summary>
    public static double Fbm(double x, double y, int period, int seed, int baseCells = 4, int octaves = 4,
        double persistence = 0.5)
    {
        double sum = 0, amplitude = 1, total = 0;
        int cells = baseCells;

        for (int octave = 0; octave < octaves; octave++)
        {
            sum += Value(x, y, period, seed + octave * 101, cells) * amplitude;
            total += amplitude;
            amplitude *= persistence;
            cells *= 2;
        }

        return sum / total;
    }
}

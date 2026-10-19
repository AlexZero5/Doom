using Doom.Assets;
using Doom.World;

namespace Doom.Rendering;

/// <summary>
///     Программный растеризатор треугольников с буфером глубины.
///     <list type="number">
///         <item><see cref="AddFace" />: отсечение изнанки, перевод в координаты камеры,
///         отсечение ближней плоскостью, проекция, подготовка градиентов (1/z, u/z, v/z линейны
///         по экрану — так текстура ложится с правильной перспективой);</item>
///         <item><see cref="Rasterize" />: кадр режется на горизонтальные полосы, каждая полоса
///         рисуется своим потоком (треугольники к ней обрезаются), пустые пиксели — небо.</item>
///     </list>
///     Свет ступенчатый (как COLORMAP в Doom): яркость грани × затухание по дальности, оба —
///     из таблиц, соседние пиксели одной поверхности получают одинаковый цвет.
/// </summary>
internal sealed class Rasterizer
{
    private const double NearPlane = 0.04;
    private const int MipLevels = 5;
    private const double FogStep = 0.25;
    private const int FogEntries = 1024;

    private static readonly int[] Fog = BuildFog();

    private ScreenTriangle[] _triangles = new ScreenTriangle[1024];
    private int _triangleCount;

    private Texture?[][] _mips = Array.Empty<Texture?[]>();
    private IReadOnlyList<Texture?> _textures = Array.Empty<Texture?>();
    private Camera _camera;

    // Вершины многоугольника при отсечении (на поток вызова AddFace — главный поток).
    private readonly ClipVertex[] _polygon = new ClipVertex[8];
    private readonly ClipVertex[] _clipped = new ClipVertex[8];

    public int Width { get; private set; }

    public int Height { get; private set; }

    /// <summary>Цвета внутреннего кадра 0xRRGGBB.</summary>
    public int[] Color { get; private set; } = Array.Empty<int>();

    /// <summary>1/z ближайшей поверхности (0 — пусто, там небо).</summary>
    public float[] Depth { get; private set; } = Array.Empty<float>();

    public int TriangleCount => _triangleCount;

    /// <summary>Начинает кадр: камера, таблица текстур, очистка глубины.</summary>
    public void Begin(Camera camera, IReadOnlyList<Texture?> textures)
    {
        _camera = camera;
        _textures = textures;

        if (Width != camera.Width || Height != camera.Height)
        {
            Width = camera.Width;
            Height = camera.Height;
            Color = new int[Width * Height];
            Depth = new float[Width * Height];
        }
        else
        {
            Array.Clear(Depth);
        }

        if (_mips.Length < textures.Count)
            Array.Resize(ref _mips, textures.Count);

        for (int i = 0; i < textures.Count; i++)
        {
            Texture? texture = textures[i];
            if (texture is null)
            {
                _mips[i] = null!;
                continue;
            }

            Texture?[]? chain = _mips[i];
            if (chain is null || !ReferenceEquals(chain[0], texture))
            {
                chain = new Texture?[MipLevels];
                for (int level = 0; level < MipLevels; level++)
                    chain[level] = texture.Mip(level);
                _mips[i] = chain;
            }
        }

        _triangleCount = 0;
    }

    /// <summary>Отсекает и проецирует грань; видимые треугольники встают в очередь растеризации.</summary>
    public void AddFace(in Face face)
    {
        Vec3 eye = _camera.Position;
        if ((face.Flags & FaceFlags.DoubleSided) == 0 && face.Normal.Dot(eye) - face.Plane <= 0)
            return;

        int count = face.Count;
        bool anyInFront = false;
        for (int i = 0; i < count; i++)
        {
            (double x, double y, double z) = _camera.ToView(face.Vertex(i));
            (double u, double v) = face.Uv(i);
            _polygon[i] = new ClipVertex(x, y, z, u, v);
            anyInFront |= z > NearPlane;
        }

        if (!anyInFront)
            return;

        // Отсечение ближней плоскостью (Сазерленд — Ходжман).
        int clippedCount = 0;
        for (int i = 0; i < count; i++)
        {
            ClipVertex a = _polygon[i], b = _polygon[(i + 1) % count];
            bool aIn = a.Z > NearPlane, bIn = b.Z > NearPlane;
            if (aIn)
                _clipped[clippedCount++] = a;
            if (aIn != bIn)
                _clipped[clippedCount++] = ClipVertex.Lerp(a, b, (NearPlane - a.Z) / (b.Z - a.Z));
        }

        if (clippedCount < 3)
            return;

        // Грубое отсечение по краям кадра: все вершины за одним краем — грань не видна.
        double focal = _camera.Focal, cx = _camera.CenterX, cy = _camera.CenterY;
        bool allLeft = true, allRight = true, allTop = true, allBottom = true;
        for (int i = 0; i < clippedCount; i++)
        {
            ref ClipVertex vertex = ref _clipped[i];
            double inverse = 1.0 / vertex.Z;
            vertex.ScreenX = cx + focal * vertex.X * inverse;
            vertex.ScreenY = cy - focal * vertex.Y * inverse;
            vertex.W = inverse;
            allLeft &= vertex.ScreenX < 0;
            allRight &= vertex.ScreenX > Width;
            allTop &= vertex.ScreenY < 0;
            allBottom &= vertex.ScreenY > Height;
        }

        if (allLeft || allRight || allTop || allBottom)
            return;

        int texture = face.Texture;
        Texture?[]? mips = texture > 0 && texture < _textures.Count ? _mips[texture] : null;
        if (mips is null)
            return;

        int textureSize = mips[0]!.Width;
        double mipBase = face.RepeatsPerMeter > 1e-9 ? focal / (face.RepeatsPerMeter * textureSize) : double.MaxValue;
        int light = (face.Flags & FaceFlags.FullBright) != 0 ? 256 : (int)(Lighting.Quantize(face.Light) * 256);

        for (int i = 1; i + 1 < clippedCount; i++)
            AddTriangle(_clipped[0], _clipped[i], _clipped[i + 1], mips, mipBase, light, face.Flags);
    }

    private void AddTriangle(in ClipVertex a, in ClipVertex b, in ClipVertex c, Texture?[] mips, double mipBase, int light,
        FaceFlags flags)
    {
        double x0 = a.ScreenX, y0 = a.ScreenY, x1 = b.ScreenX, y1 = b.ScreenY, x2 = c.ScreenX, y2 = c.ScreenY;
        double area = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0);
        if (Math.Abs(area) < 1e-9)
            return;

        double inverseArea = 1.0 / area;

        if (_triangleCount == _triangles.Length)
            Array.Resize(ref _triangles, _triangles.Length * 2);

        ref ScreenTriangle t = ref _triangles[_triangleCount];

        // Сортировка вершин по y для обхода строк.
        (double ax, double ay, double bx, double by, double cx, double cy) = (x0, y0, x1, y1, x2, y2);
        if (ay > by) (ax, ay, bx, by) = (bx, by, ax, ay);
        if (by > cy) (bx, by, cx, cy) = (cx, cy, bx, by);
        if (ay > by) (ax, ay, bx, by) = (bx, by, ax, ay);

        int rowStart = Math.Max(0, (int)Math.Ceiling(ay - 0.5));
        int rowEnd = Math.Min(Height - 1, (int)Math.Ceiling(cy - 0.5) - 1);
        if (rowStart > rowEnd)
            return;

        t.Ax = ax; t.Ay = ay; t.Bx = bx; t.By = by; t.Cx = cx; t.Cy = cy;
        t.RowStart = rowStart;
        t.RowEnd = rowEnd;

        // Градиенты атрибутов (1/z, u/z, v/z) — плоскости над экраном.
        Gradient(a.W, b.W, c.W, out t.WdX, out t.WdY);
        Gradient(a.U * a.W, b.U * b.W, c.U * c.W, out t.UdX, out t.UdY);
        Gradient(a.V * a.W, b.V * b.W, c.V * c.W, out t.VdX, out t.VdY);
        t.OriginX = x0;
        t.OriginY = y0;
        t.W0 = a.W;
        t.U0 = a.U * a.W;
        t.V0 = a.V * a.W;

        t.Mips = mips;
        t.MipBase = mipBase;
        t.Light = light;
        t.Flags = flags;
        _triangleCount++;

        void Gradient(double p0, double p1, double p2, out double dx, out double dy)
        {
            dx = ((p1 - p0) * (y2 - y0) - (p2 - p0) * (y1 - y0)) * inverseArea;
            dy = ((p2 - p0) * (x1 - x0) - (p1 - p0) * (x2 - x0)) * inverseArea;
        }
    }

    /// <summary>Рисует очередь треугольников полосами в несколько потоков; пустые пиксели — небо.</summary>
    public void Rasterize(Func<int, int, int> sky)
    {
        int bands = Math.Clamp(Environment.ProcessorCount * 2, 1, Math.Max(1, Height / 4));
        int rowsPerBand = (Height + bands - 1) / bands;

        Parallel.For(0, bands, band =>
        {
            int from = band * rowsPerBand, to = Math.Min(Height, from + rowsPerBand);
            if (from >= to)
                return;

            for (int i = 0; i < _triangleCount; i++)
            {
                ref ScreenTriangle triangle = ref _triangles[i];
                if (triangle.RowEnd >= from && triangle.RowStart < to)
                    DrawTriangle(ref triangle, Math.Max(from, triangle.RowStart), Math.Min(to - 1, triangle.RowEnd));
            }

            for (int y = from; y < to; y++)
            {
                int row = y * Width;
                for (int x = 0; x < Width; x++)
                {
                    if (Depth[row + x] == 0)
                        Color[row + x] = sky(x, y);
                }
            }
        });
    }

    private void DrawTriangle(ref ScreenTriangle t, int firstRow, int lastRow)
    {
        int width = Width;
        int[] color = Color;
        float[] depth = Depth;
        bool alphaTest = (t.Flags & FaceFlags.AlphaTest) != 0;
        bool clamp = (t.Flags & FaceFlags.Clamp) != 0;
        bool fullBright = (t.Flags & FaceFlags.FullBright) != 0;
        int light = t.Light;
        Texture?[] mips = t.Mips;
        double mip1 = t.MipBase * 2, mip2 = t.MipBase * 4, mip3 = t.MipBase * 8, mip4 = t.MipBase * 16;

        for (int y = firstRow; y <= lastRow; y++)
        {
            double yc = y + 0.5;
            double xLong = t.Ax + (yc - t.Ay) * (t.Cx - t.Ax) / (t.Cy - t.Ay);
            double xShort = yc < t.By
                ? t.Ax + (yc - t.Ay) * (t.Bx - t.Ax) / Math.Max(1e-12, t.By - t.Ay)
                : t.Bx + (yc - t.By) * (t.Cx - t.Bx) / Math.Max(1e-12, t.Cy - t.By);

            double left = Math.Min(xLong, xShort), right = Math.Max(xLong, xShort);
            int xStart = Math.Max(0, (int)Math.Ceiling(left - 0.5));
            int xEnd = Math.Min(width - 1, (int)Math.Ceiling(right - 0.5) - 1);
            if (xStart > xEnd)
                continue;

            double dx = xStart + 0.5 - t.OriginX, dy = yc - t.OriginY;
            double w = t.W0 + t.WdX * dx + t.WdY * dy;
            double uw = t.U0 + t.UdX * dx + t.UdY * dy;
            double vw = t.V0 + t.VdX * dx + t.VdY * dy;
            int index = y * width + xStart;

            for (int x = xStart; x <= xEnd; x++, index++, w += t.WdX, uw += t.UdX, vw += t.VdX)
            {
                if (w <= depth[index])
                    continue;

                double z = 1.0 / w;
                double u = uw * z, v = vw * z;

                Texture texture = z < mip1 ? mips[0]! : z < mip2 ? mips[1]! : z < mip3 ? mips[2]! : z < mip4 ? mips[3]! : mips[4]!;
                int tw = texture.Width, th = texture.Height;
                int tx, ty;
                if (clamp)
                {
                    tx = Math.Clamp((int)(u * tw), 0, tw - 1);
                    ty = Math.Clamp((int)(v * th), 0, th - 1);
                }
                else
                {
                    tx = (int)Math.Floor(u * tw) % tw;
                    ty = (int)Math.Floor(v * th) % th;
                    if (tx < 0) tx += tw;
                    if (ty < 0) ty += th;
                }

                int argb = texture.Pixels[ty * tw + tx];
                if (alphaTest && (argb >>> 24) < 128)
                    continue;

                int brightness = fullBright ? 256 : light * Fog[Math.Min(FogEntries - 1, (int)(z * (1 / FogStep)))] >> 8;
                int r = ((argb >> 16) & 0xFF) * brightness >> 8;
                int g = ((argb >> 8) & 0xFF) * brightness >> 8;
                int b = (argb & 0xFF) * brightness >> 8;
                color[index] = (Math.Min(255, r) << 16) | (Math.Min(255, g) << 8) | Math.Min(255, b);
                depth[index] = (float)w;
            }
        }
    }

    private static int[] BuildFog()
    {
        var table = new int[FogEntries];
        for (int i = 0; i < FogEntries; i++)
            table[i] = (int)(Lighting.Brightness((i + 0.5) * FogStep) * 256);
        return table;
    }

    private struct ClipVertex(double x, double y, double z, double u, double v)
    {
        public double X = x, Y = y, Z = z, U = u, V = v;
        public double ScreenX, ScreenY, W;

        public static ClipVertex Lerp(in ClipVertex a, in ClipVertex b, double t) =>
            new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t,
                a.U + (b.U - a.U) * t, a.V + (b.V - a.V) * t);
    }

    private struct ScreenTriangle
    {
        public double Ax, Ay, Bx, By, Cx, Cy;
        public int RowStart, RowEnd;
        public double OriginX, OriginY;
        public double W0, WdX, WdY, U0, UdX, UdY, V0, VdX, VdY;
        public Texture?[] Mips;
        public double MipBase;
        public int Light;
        public FaceFlags Flags;
    }
}

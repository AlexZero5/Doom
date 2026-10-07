namespace Doom.Rendering;

/// <summary>
///     Цвета неба и пола: вертикальные градиенты по «пикселям» кадра. Градиент
///     зависит только от вертикали, поэтому считается один раз на размер кадра
///     и дальше читается из кэша — вместо Math.Pow на каждую строку каждый кадр.
/// </summary>
internal static class EnvironmentPalette
{
    private const int SkyTopR = 0x06, SkyTopG = 0x06, SkyTopB = 0x10;
    private const int SkyHorizonR = 0x24, SkyHorizonG = 0x24, SkyHorizonB = 0x3A;
    private const int FloorR = 0x6E, FloorG = 0x56, FloorB = 0x3E;

    private const double SkyGamma = 0.55;
    private const double FloorLightRange = 7.0;
    private const double FloorLightOffset = 2.0;
    private const double FloorMinBrightness = 0.02;
    private const double Gamma = 1.0 / 1.6;

    private static int _cachedPixelRows = -1;
    private static int[] _skyCache = Array.Empty<int>();
    private static int[] _floorCache = Array.Empty<int>();

    /// <summary>Кэш цветов неба по вертикальным «пикселям» кадра.</summary>
    public static ReadOnlySpan<int> Sky(int pixelRows)
    {
        EnsureCache(pixelRows);
        return _skyCache;
    }

    /// <summary>Кэш цветов пола по вертикальным «пикселям» кадра.</summary>
    public static ReadOnlySpan<int> Floor(int pixelRows)
    {
        EnsureCache(pixelRows);
        return _floorCache;
    }

    private static void EnsureCache(int pixelRows)
    {
        if (_cachedPixelRows == pixelRows)
            return;

        _skyCache = new int[pixelRows];
        _floorCache = new int[pixelRows];

        for (int pixelY = 0; pixelY < pixelRows; pixelY++)
        {
            _skyCache[pixelY] = SkyColor(pixelY, pixelRows);
            _floorCache[pixelY] = FloorColor(pixelY, pixelRows);
        }

        _cachedPixelRows = pixelRows;
    }

    public static int SkyColor(int pixelY, int pixelRows)
    {
        double middle = pixelRows / 2.0;
        double t = middle > 0 ? 1.0 - Math.Abs(pixelY - middle) / middle : 0.0;
        if (t < 0) t = 0;
        if (t > 1) t = 1;
        t = Math.Pow(t, SkyGamma);

        int r = (int)(SkyTopR + (SkyHorizonR - SkyTopR) * t);
        int g = (int)(SkyTopG + (SkyHorizonG - SkyTopG) * t);
        int b = (int)(SkyTopB + (SkyHorizonB - SkyTopB) * t);

        return ColorRgb.Pack(r, g, b);
    }

    public static int FloorColor(int pixelY, int pixelRows)
    {
        int belowMiddle = pixelY - pixelRows / 2;
        if (belowMiddle < 1) belowMiddle = 1;

        double distance = 0.5 * pixelRows / belowMiddle;

        double brightness = FloorLightRange / (distance + FloorLightOffset);
        if (brightness > 1) brightness = 1;
        if (brightness < FloorMinBrightness) brightness = FloorMinBrightness;
        brightness = Math.Pow(brightness, Gamma);

        return ColorRgb.Pack(
            (int)(FloorR * brightness),
            (int)(FloorG * brightness),
            (int)(FloorB * brightness));
    }
}

using Doom.Assets;
using Doom.Configuration;

namespace Doom.Rendering;

/// <summary>
///     Освещение стены: затемнение по расстоянию, тень на горизонтальных гранях
///     и текстурирование — из PNG-ассета (assets/walls/&lt;тип&gt;.png), а если его нет,
///     процедурной кирпичной кладкой.
/// </summary>
internal static class WallShader
{
    private const double LightRange = 7.0;
    private const double LightOffset = 2.0;
    private const double MinBrightness = 0.03;
    private const double SideShadow = 0.78;
    private const double Gamma = 1.0 / 1.6;

    /// <summary>
    ///     Предвычисленные на всю колонку величины: яркость (зависит только от дистанции
    ///     и грани) и колонка текстуры (зависит только от wallX). Раньше и то и другое
    ///     считалось заново на каждый пиксель — на больших разрешениях это были
    ///     сотни тысяч Math.Pow за кадр.
    /// </summary>
    public readonly struct ColumnShader
    {
        private readonly double _brightness;
        private readonly int _texX;
        private readonly Texture? _texture;
        private readonly int _baseR;
        private readonly int _baseG;
        private readonly int _baseB;
        private readonly double _wallX;

        public ColumnShader(
            int baseR, int baseG, int baseB, Texture? texture,
            double distance, int side, double wallX)
        {
            double brightness = LightRange / (distance + LightOffset);
            if (brightness > 1) brightness = 1;
            if (brightness < MinBrightness) brightness = MinBrightness;
            if (side == 1) brightness *= SideShadow;
            _brightness = Math.Pow(brightness, Gamma);

            _texture = texture;
            _texX = texture is null
                ? 0
                : Math.Clamp((int)(wallX * texture.Width), 0, texture.Width - 1);

            _baseR = baseR;
            _baseG = baseG;
            _baseB = baseB;
            _wallX = wallX;
        }

        /// <summary>Цвет одного «пикселя» стены в этой колонке (полная стена от top до top+height).</summary>
        public int ShadePixel(int pixelY, double wallTop, double wallHeight)
        {
            double startY = (pixelY - wallTop) / wallHeight;
            double endY = (pixelY + 1 - wallTop) / wallHeight;
            if (startY < 0) startY = 0;
            if (endY > 1) endY = 1;
            if (endY < startY) endY = startY;

            return ShadeSpan(startY, endY);
        }

        /// <summary>
        ///     Цвет «пикселя» по диапазону текстурной V: <paramref name="vStart" /> — верхний край
        ///     пикселя в координатах текстуры (0 — верх текстуры, 1 — низ). Нужен стенам произвольной
        ///     высоты: низкая стена показывает только нижнюю часть текстуры (кирпичи продолжаются),
        ///     а потолок — верхнюю.
        /// </summary>
        public int ShadeSpan(double vStart, double vEnd)
        {
            double startY = vStart;
            double endY = vEnd;
            if (startY < 0) startY = 0;
            if (endY > 1) endY = 1;
            if (endY < startY) endY = startY;

            double sumR = 0, sumG = 0, sumB = 0;

            Texture? texture = _texture;
            int width = texture?.Width ?? 0;
            int height = texture?.Height ?? 0;
            int[] pixels = texture?.Pixels ?? Array.Empty<int>();

            for (int sample = 0; sample < GameConfig.TextureSamples; sample++)
            {
                double k = (sample + 0.5) / GameConfig.TextureSamples;
                double sampleY = startY + (endY - startY) * k;

                double r, g, b;

                if (texture is not null)
                {
                    int texY = Math.Clamp((int)(sampleY * height), 0, height - 1);
                    int argb = pixels[texY * width + _texX];

                    // «Дырка» в текстуре стены показывает базовый цвет клетки.
                    if (Texture.IsTransparent(argb))
                    {
                        r = _baseR;
                        g = _baseG;
                        b = _baseB;
                    }
                    else
                    {
                        r = (argb >>> 16) & 0xFF;
                        g = (argb >>> 8) & 0xFF;
                        b = argb & 0xFF;
                    }
                }
                else
                {
                    (r, g, b) = BrickWallTexture.Sample(_baseR, _baseG, _baseB, _wallX, sampleY);
                }

                sumR += r;
                sumG += g;
                sumB += b;
            }

            double inverse = 1.0 / GameConfig.TextureSamples;

            return ColorRgb.Pack(
                (int)(sumR * inverse * _brightness),
                (int)(sumG * inverse * _brightness),
                (int)(sumB * inverse * _brightness));
        }
    }
}

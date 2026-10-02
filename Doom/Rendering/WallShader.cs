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

    /// <summary>Цвет одного «пикселя» стены.</summary>
    public static int Shade(
        int baseR, int baseG, int baseB, Texture? texture,
        double distance, int side, double wallX,
        int pixelY, double wallTop, double wallHeight)
    {
        double brightness = LightRange / (distance + LightOffset);
        if (brightness > 1) brightness = 1;
        if (brightness < MinBrightness) brightness = MinBrightness;
        if (side == 1) brightness *= SideShadow;
        brightness = Math.Pow(brightness, Gamma);

        double startY = (pixelY - wallTop) / wallHeight;
        double endY = (pixelY + 1 - wallTop) / wallHeight;
        if (startY < 0) startY = 0;
        if (endY > 1) endY = 1;
        if (endY < startY) endY = startY;

        double sumR = 0, sumG = 0, sumB = 0;

        for (int sample = 0; sample < GameConfig.TextureSamples; sample++)
        {
            double k = (sample + 0.5) / GameConfig.TextureSamples;
            double sampleY = startY + (endY - startY) * k;

            double r, g, b;

            if (texture is not null)
            {
                int argb = texture.Sample(wallX, sampleY);

                // «Дырка» в текстуре стены показывает базовый цвет клетки.
                if (Texture.IsTransparent(argb))
                {
                    r = baseR;
                    g = baseG;
                    b = baseB;
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
                (r, g, b) = BrickWallTexture.Sample(baseR, baseG, baseB, wallX, sampleY);
            }

            sumR += r;
            sumG += g;
            sumB += b;
        }

        double inverse = 1.0 / GameConfig.TextureSamples;

        return ColorRgb.Pack(
            (int)(sumR * inverse * brightness),
            (int)(sumG * inverse * brightness),
            (int)(sumB * inverse * brightness));
    }
}

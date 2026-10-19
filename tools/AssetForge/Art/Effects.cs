using AssetForge.Core;

namespace AssetForge.Art;

/// <summary>
///     Анимированные эффекты и снаряды: каждый — набор кадров (папка anims/&lt;имя&gt;/0.png, 1.png…).
///     Форма огня и дыма — шумовые «клубы»; край двоичный (без полупрозрачной каймы).
/// </summary>
internal static class Effects
{
    public static IEnumerable<(string Clip, IReadOnlyList<Canvas> Frames, double FrameTime, bool Loop)> All()
    {
        yield return ("explosion", Range(6, i => Fireball(48, i / 5.0, 601)), 0.075, false);
        yield return ("bfg_impact", Range(6, i => Burst(56, i / 5.0, BfgColors, 611)), 0.1, false);
        yield return ("plasma_impact", Range(4, i => Burst(24, i / 3.0, PlasmaColors, 621)), 0.06, false);
        yield return ("puff", Range(4, i => Puff(14, i / 3.0, 631)), 0.05, false);
        yield return ("smoke", Range(4, i => Smoke(16, i / 3.0, 641)), 0.14, false);
        yield return ("plasma_ball", Range(3, i => Orb(14, i, PlasmaColors, 651)), 0.06, true);
        yield return ("bfg_ball", Range(3, i => Orb(30, i, BfgColors, 661)), 0.08, true);
        yield return ("rocket", Range(2, i => Rocket(18, i)), 0.05, true);
    }

    private static readonly Rgb[] FireColors =
        { Rgb.Hex(0xffffff), Rgb.Hex(0xfff6a8), Rgb.Hex(0xffc040), Rgb.Hex(0xe46c10), Rgb.Hex(0xc0401a), Rgb.Hex(0x8c2414) };

    private static readonly Rgb[] PlasmaColors =
        { Rgb.Hex(0xffffff), Rgb.Hex(0xe8ffff), Rgb.Hex(0x34c4f4), Rgb.Hex(0x24467e) };

    private static readonly Rgb[] BfgColors =
        { Rgb.Hex(0xffffff), Rgb.Hex(0xe8ffc0), Rgb.Hex(0xb0ec70), Rgb.Hex(0x40903a), Rgb.Hex(0x1a4c1c) };

    private static IReadOnlyList<Canvas> Range(int count, Func<int, Canvas> frame) =>
        Enumerable.Range(0, count).Select(frame).ToList();

    private static Canvas Done(Canvas canvas, Rgb? outline = null)
    {
        if (outline is { } color)
            canvas.Outline(color);
        Palette.Apply(canvas, dither: 8);
        return canvas;
    }

    /// <summary>
    ///     Огненный шар: растёт, к концу распадается на клочья дыма (порог шума повышается,
    ///     цвет уходит от белого ядра к тёмному дыму).
    /// </summary>
    private static Canvas Fireball(int size, double t, int seed)
    {
        var canvas = new Canvas(size, size);
        double c = size / 2.0;
        double radius = size * (0.22 + 0.26 * Math.Sqrt(t));
        double cool = t * 0.75;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            double dx = x + 0.5 - c, dy = y + 0.5 - c;
            double r = Math.Sqrt(dx * dx + dy * dy) / radius;
            double n = Noise.Fbm(x + t * 20, y - t * 30, size, seed, 6, 3);
            double edge = 0.55 + n * 0.9;
            if (r > edge) continue;

            // Поздние кадры: «дыры» в облаке — клочья.
            if (t > 0.5 && n < (t - 0.5) * 1.1) continue;

            double heat = Math.Clamp(r / edge * 0.85 + cool - (n - 0.5) * 0.6, 0, 1);
            Rgb color = Rgb.Ramp(heat, FireColors);
            if (t > 0.55 && heat > 0.8)
                color = Rgb.Hex(0x6a6660).Scale(0.7 + n * 0.6); // остывшие клочья — серый дым
            canvas[x, y] = color.Argb();
        }

        return Done(canvas);
    }

    /// <summary>Вспышка-разлёт (плазма, BFG): кольцо с лучами расширяется и гаснет.</summary>
    private static Canvas Burst(int size, double t, Rgb[] colors, int seed)
    {
        var canvas = new Canvas(size, size);
        double c = size / 2.0;
        double radius = size * (0.18 + 0.3 * t);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            double dx = x + 0.5 - c, dy = y + 0.5 - c;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            double angle = Math.Atan2(dy, dx);
            double rays = 0.7 + 0.3 * Math.Abs(Math.Sin(angle * 4 + seed));
            double n = Noise.Unit((int)(angle * 12), (int)(t * 10), seed);
            double reach = radius * rays * (0.85 + n * 0.3);
            if (distance > reach) continue;

            double ring = distance / reach;
            if (t > 0.4 && ring < (t - 0.4) * 1.2) continue; // середина выгорает — остаётся кольцо

            canvas[x, y] = Rgb.Ramp(Math.Clamp(ring * 0.7 + t * 0.4, 0, 1), colors).Argb();
        }

        return Done(canvas);
    }

    /// <summary>Пулевая искра → серое облачко.</summary>
    private static Canvas Puff(int size, double t, int seed)
    {
        var canvas = new Canvas(size, size);
        double c = size / 2.0;
        double radius = size * (0.2 + 0.25 * t);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            double dx = x + 0.5 - c, dy = y + 0.5 - c - t * 2;
            double r = Math.Sqrt(dx * dx + dy * dy) / radius;
            double n = Noise.Fbm(x, y, size, seed + (int)(t * 3), 4, 2);
            if (r > 0.7 + n * 0.5) continue;

            Rgb color = t < 0.2
                ? Rgb.Ramp(r, Rgb.Hex(0xffffff), Rgb.Hex(0xfff0a0), Rgb.Hex(0xffa040))
                : Rgb.Hex(0xb4b0a8).Scale(1.1 - r * 0.4 - t * 0.3);
            canvas[x, y] = color.Argb();
        }

        return Done(canvas);
    }

    /// <summary>Клуб дыма следа ракеты: светлый, расплывается и редеет.</summary>
    private static Canvas Smoke(int size, double t, int seed)
    {
        var canvas = new Canvas(size, size);
        double c = size / 2.0;
        double radius = size * (0.25 + 0.2 * t);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            double dx = x + 0.5 - c, dy = y + 0.5 - c;
            double r = Math.Sqrt(dx * dx + dy * dy) / radius;
            double n = Noise.Fbm(x, y, size, seed, 4, 3);
            if (r > 0.75 + n * 0.4) continue;
            if ((x + y) % 2 == 0 && t > 0.6) continue; // редеет «сеточкой», как в старых играх

            double light = 1.15 - r * 0.35 - (dy > 0 ? 0.15 : 0) - t * 0.2;
            canvas[x, y] = Rgb.Hex(0x9a9a96).Scale(light).Argb();
        }

        return Done(canvas);
    }

    /// <summary>Энергетический шар: ядро, ореол и искры, кадры поворачивают искры.</summary>
    private static Canvas Orb(int size, int frame, Rgb[] colors, int seed)
    {
        var canvas = new Canvas(size, size);
        double c = size / 2.0;
        double radius = size * 0.32;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            double dx = x + 0.5 - c, dy = y + 0.5 - c;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            double angle = Math.Atan2(dy, dx) + frame * 0.7;
            double flicker = 0.85 + 0.25 * Math.Abs(Math.Sin(angle * 3 + frame));
            double reach = radius * flicker;

            if (distance <= reach)
            {
                double t = distance / reach;
                canvas[x, y] = Rgb.Ramp(t * t, colors).Argb();
            }
            else if (distance < radius * 1.5 && Noise.Unit(x + frame * 7, y, seed) > 0.86)
            {
                canvas[x, y] = colors[1].Argb(); // искры вокруг
            }
        }

        return Done(canvas);
    }

    /// <summary>Ракета со спины: корпус, четыре стабилизатора и пламя дюзы (два кадра).</summary>
    private static Canvas Rocket(int size, int frame)
    {
        var sprite = new Sprite(size, size);
        double c = size / 2.0;
        foreach ((double fx, double fy) in new[] { (1.0, 0.0), (-1.0, 0.0), (0.0, 1.0), (0.0, -1.0) })
            sprite.Part(sprite.Line(c, c, c + fx * 7.5, c + fy * 7.5, 2.4), Rgb.Hex(0x6a6a62), depth: 1);

        sprite.Part(sprite.Ellipse(c, c, 4.6, 4.6), Rgb.Hex(0xb8b8b0), depth: 2.5, gloss: 0.4);
        double flame = frame == 0 ? 3.0 : 2.2;
        sprite.Flat(sprite.Ellipse(c, c, flame, flame), Rgb.Hex(0xffc040));
        sprite.Flat(sprite.Ellipse(c, c, flame * 0.5, flame * 0.5), Rgb.Hex(0xffffff));
        return sprite.Finish(Rgb.Hex(0x1a1410), dither: 4);
    }
}

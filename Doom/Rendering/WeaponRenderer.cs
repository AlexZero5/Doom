using Doom.Assets;
using Doom.Configuration;
using Doom.Gameplay;

namespace Doom.Rendering;

/// <summary>
///     Оружие от первого лица: кадры из assets/weapons/&lt;ствол&gt;/ (их рисует tools/AssetForge).
///     У каждого ствола своя анимация выстрела (<see cref="WeaponAnimations" />): кулак бьёт
///     поочерёдно правой и левой, пила пилит, у дробовика передёргивается цевьё, у пулемёта
///     вращается блок стволов. Покачивание — только при ходьбе; при смене ствол поднимается снизу.
/// </summary>
/// <remarks>
///     Кадр — нижняя середина экрана Doom 640×400: 400 его пикселей по вертикали = высота вида.
///     «Пиксель» кадра квадратный: по горизонтали — ячейка, по вертикали — половина ячейки.
/// </remarks>
internal sealed class WeaponRenderer(AssetStore? assets)
{
    private const double ScreenHeight = 400.0;
    private const double BobSpeed = 7.0;
    private const double BobAmplitudeX = 9.0;
    private const double BobAmplitudeY = 6.0;
    private const double RaiseDistance = 150.0;

    public void Draw(Framebuffer framebuffer, Viewport viewport, Player player, double nowSeconds)
    {
        if (assets is null)
            return;

        WeaponAnimation animation = WeaponAnimations.Get(player.CurrentWeapon);
        (string frameName, bool mirrored) = animation.FrameAt(player, nowSeconds);
        Texture? frame = assets.GetWeaponFrame(animation.Folder, frameName)
                         ?? assets.GetWeaponFrame(animation.Folder, animation.Idle);
        if (frame is null)
            return;

        double scale = viewport.PixelRows / ScreenHeight;
        double bobPhase = player.WeaponBobTime * BobSpeed;
        double bobX = Math.Sin(bobPhase) * BobAmplitudeX * scale * player.BobIntensity;
        double bobY = Math.Abs(Math.Cos(bobPhase)) * BobAmplitudeY * scale * player.BobIntensity;
        double raise = (1.0 - player.RaiseProgress(nowSeconds)) * RaiseDistance * scale;

        // Ствол всегда стоит на нижнем крае вида: наклон взгляда его не двигает (раньше он
        // отрывался от низа экрана при взгляде вниз и уходил под HUD при взгляде вверх).
        DrawFrame(framebuffer, viewport, frame, scale, viewport.Columns * 0.5 + bobX,
            viewport.PixelRows + bobY + raise, mirrored);
    }

    /// <summary>Рисует кадр с масштабом: низ кадра на <paramref name="bottom" />, центр — <paramref name="centerX" />.</summary>
    private static void DrawFrame(Framebuffer framebuffer, Viewport viewport, Texture frame, double scale,
        double centerX, double bottom, bool mirrored)
    {
        double width = frame.Width * scale;
        double height = frame.Height * scale;
        double left = centerX - width * 0.5;
        double top = bottom - height;

        int firstPixel = Math.Max(0, (int)Math.Floor(top));
        int lastPixel = Math.Min(viewport.PixelRows - 1, (int)Math.Ceiling(bottom) - 1);
        int firstColumn = Math.Max(0, (int)Math.Floor(left));
        int lastColumn = Math.Min(viewport.Columns - 1, (int)Math.Ceiling(left + width) - 1);
        double inverseScale = 1.0 / scale;

        for (int pixel = firstPixel; pixel <= lastPixel; pixel++)
        {
            int sourceY = (int)((pixel + 0.5 - top) * inverseScale);
            if (sourceY < 0 || sourceY >= frame.Height)
                continue;

            int rowBase = (pixel >> 1) * framebuffer.Columns;
            int[] target = (pixel & 1) == 0 ? framebuffer.Foreground : framebuffer.Background;

            for (int column = firstColumn; column <= lastColumn; column++)
            {
                int sourceX = (int)((column + 0.5 - left) * inverseScale);
                if (sourceX < 0 || sourceX >= frame.Width)
                    continue;
                if (mirrored)
                    sourceX = frame.Width - 1 - sourceX;

                int argb = frame.SamplePixel(sourceX, sourceY);
                if (Texture.IsTransparent(argb))
                    continue;

                target[rowBase + column] = argb & 0xFFFFFF;
                framebuffer.Chars[rowBase + column] = Framebuffer.UpperHalfBlock;
            }
        }
    }
}

/// <summary>Анимация ствола: кадр покоя, последовательность выстрела и особые правила.</summary>
internal sealed class WeaponAnimation
{
    public required string Folder { get; init; }

    public string Idle { get; init; } = "idle";

    /// <summary>Кадры покоя по кругу (бензопила тарахтит), иначе — один кадр <see cref="Idle" />.</summary>
    public (string Frame, double Seconds)[] IdleLoop { get; init; } = Array.Empty<(string, double)>();

    /// <summary>Кадры после выстрела по порядку (время каждого, сек).</summary>
    public (string Frame, double Seconds)[] Fire { get; init; } = Array.Empty<(string, double)>();

    /// <summary>Чередовать кадры выстрела по номеру выстрела (пулемёт: блок стволов вращается).</summary>
    public bool AlternateByShot { get; init; }

    /// <summary>Каждый второй удар — зеркально (кулак: левой и правой).</summary>
    public bool MirrorAlternate { get; init; }

    public (string Frame, bool Mirrored) FrameAt(Player player, double now)
    {
        double sinceFire = now - player.LastFireTime;
        bool mirrored = MirrorAlternate && !player.SwingFromLeft;

        if (Fire.Length > 0 && sinceFire >= 0)
        {
            if (AlternateByShot)
            {
                double total = Fire.Sum(step => step.Seconds);
                if (sinceFire < total)
                    return (Fire[player.ShotsFired % Fire.Length].Frame, mirrored);
            }
            else
            {
                double elapsed = 0;
                foreach ((string frame, double seconds) in Fire)
                {
                    elapsed += seconds;
                    if (sinceFire < elapsed)
                        return (frame, mirrored);
                }
            }
        }

        if (IdleLoop.Length > 0)
        {
            double cycle = IdleLoop.Sum(step => step.Seconds);
            double t = now % cycle;
            foreach ((string frame, double seconds) in IdleLoop)
            {
                if (t < seconds)
                    return (frame, false);
                t -= seconds;
            }
        }

        return (Idle, false);
    }
}

/// <summary>Анимации всех стволов (кадры — в assets/weapons/&lt;Folder&gt;/).</summary>
internal static class WeaponAnimations
{
    private static readonly WeaponAnimation[] Animations =
    {
        new()
        {
            Folder = "fist", MirrorAlternate = true,
            Fire = new[] { ("punch1", 0.06), ("punch2", 0.12), ("punch3", 0.1), ("punch1", 0.06) }
        },
        new()
        {
            Folder = "chainsaw", Idle = "idle1",
            IdleLoop = new[] { ("idle1", 0.07), ("idle2", 0.07) },
            Fire = new[] { ("saw1", 0.055), ("saw2", 0.055) }
        },
        new()
        {
            Folder = "pistol",
            Fire = new[] { ("fire1", 0.05), ("fire2", 0.06), ("fire3", 0.09) }
        },
        new()
        {
            Folder = "shotgun",
            Fire = new[] { ("fire1", 0.06), ("fire2", 0.08), ("idle", 0.12), ("pump1", 0.16), ("pump2", 0.14) }
        },
        new()
        {
            Folder = "chaingun", AlternateByShot = true,
            Fire = new[] { ("fire1", GameConfig.TargetFrameMs / 1000.0 * 5), ("fire2", GameConfig.TargetFrameMs / 1000.0 * 5) }
        },
        new()
        {
            Folder = "rocketlauncher",
            Fire = new[] { ("fire1", 0.06), ("fire2", 0.1), ("fire3", 0.16) }
        },
        new()
        {
            Folder = "plasmarifle",
            Fire = new[] { ("fire1", 0.04), ("fire2", 0.05) }
        },
        new()
        {
            Folder = "bfg9000",
            Fire = new[] { ("fire1", 0.1), ("fire2", 0.3), ("charge1", 0.4), ("charge2", 0.4) }
        },
    };

    public static WeaponAnimation Get(WeaponKind kind) => Animations[(int)kind];
}

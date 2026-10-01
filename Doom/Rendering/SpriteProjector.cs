using Doom.Configuration;
using Doom.Gameplay;
using Doom.World;

namespace Doom.Rendering;

/// <summary>
///     Проекция бонусов, снарядов и кратковременных эффектов на экран
///     (билборды, затенение по расстоянию, сортировка по глубине).
/// </summary>
internal static class SpriteProjector
{
    private const double CameraZ = 0.5;

    private const double MinDistance = 0.15;
    private const double AngleMargin = 0.4;

    private const double LightRange = 7.0;
    private const double LightOffset = 2.0;
    private const double MinBrightness = 0.12;
    private const double Gamma = 1.0 / 1.6;

    /// <summary>Добавляет в <paramref name="output" /> бонусы, лежащие на карте.</summary>
    public static void ProjectPickups(
        Viewport viewport,
        Player player,
        IReadOnlyList<Pickup> pickups,
        List<SpriteQuad> output)
    {
        foreach (Pickup pickup in pickups)
        {
            if (pickup.Taken)
                continue;

            PickupVisual visual = PickupPalette.Get(pickup.Kind);

            foreach (SpriteBand band in visual.Bands)
            {
                AddQuad(
                    viewport,
                    player,
                    pickup.X,
                    pickup.Y,
                    band.BottomZ,
                    band.TopZ,
                    visual.Width * band.WidthFactor,
                    band.Color,
                    alpha: 1.0,
                    output);
            }
        }
    }

    /// <summary>Добавляет в <paramref name="output" /> снаряды и кратковременные эффекты.</summary>
    public static void ProjectDynamics(
        Viewport viewport,
        Player player,
        IReadOnlyList<Projectile> projectiles,
        IReadOnlyList<Impact> impacts,
        List<SpriteQuad> output)
    {
        foreach (Projectile projectile in projectiles)
        {
            if (!projectile.Alive)
                continue;

            ProjectileVisual visual = ProjectilePalette.Get(projectile.Kind);

            AddQuad(viewport, player, projectile.X, projectile.Y, visual.BottomZ, visual.TopZ, visual.Width,
                visual.Color, alpha: 1.0, output);
        }

        foreach (Impact impact in impacts)
        {
            if (impact.IsExpired)
                continue;

            ImpactVisual visual = ImpactPalette.Get(impact.Kind);
            double progress = impact.Progress;
            double alpha = visual.Alpha * (1.0 - progress);
            if (alpha <= 0.01)
                continue;

            double width = visual.Width * impact.Scale * (1.0 + (visual.Growth - 1.0) * progress);
            double rise = visual.Rise * impact.Age;

            AddQuad(viewport, player, impact.X, impact.Y, visual.BottomZ + rise, visual.TopZ + rise, width,
                visual.Color, alpha, output);
        }
    }

    /// <summary>Проекция одного билборда: экранная позиция, высота и глубина.</summary>
    private static void AddQuad(
        Viewport viewport,
        Player player,
        double x,
        double y,
        double bottomZ,
        double topZ,
        double width,
        int baseColor,
        double alpha,
        List<SpriteQuad> output)
    {
        double dx = x - player.X;
        double dy = y - player.Y;
        double distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance < MinDistance)
            return;

        double relativeAngle = Normalize(Math.Atan2(dy, dx) - player.Angle);
        if (Math.Abs(relativeAngle) > viewport.HalfFieldOfViewRadians + AngleMargin)
            return;

        double screenX = viewport.Columns * 0.5
                         + viewport.Columns * Math.Tan(relativeAngle) / (2.0 * viewport.TanHalfFieldOfView);

        if (double.IsNaN(screenX) || double.IsInfinity(screenX))
            return;

        if (screenX < -viewport.Columns || screenX > 2 * viewport.Columns)
            return;

        double halfAngle = Math.Atan2(width * 0.5, distance);
        double halfColumns = viewport.Columns * halfAngle / viewport.FieldOfViewRadians;

        double topY = viewport.PixelRows * 0.5 + viewport.PixelRows * (CameraZ - topZ) / distance;
        double bottomY = viewport.PixelRows * 0.5 + viewport.PixelRows * (CameraZ - bottomZ) / distance;

        double perpendicularDistance = distance * Math.Cos(relativeAngle);

        output.Add(new SpriteQuad(
            minX: screenX - halfColumns,
            maxX: screenX + halfColumns,
            topY: topY,
            bottomY: bottomY,
            perpendicularDistance: perpendicularDistance,
            color: Shade(baseColor, perpendicularDistance),
            alpha: alpha));
    }

    private static double Normalize(double angle)
    {
        while (angle > Math.PI)
            angle -= 2 * Math.PI;

        while (angle < -Math.PI)
            angle += 2 * Math.PI;

        return angle;
    }

    private static int Shade(int baseColor, double distance)
    {
        double brightness = LightRange / (distance + LightOffset);
        if (brightness > 1) brightness = 1;
        if (brightness < MinBrightness) brightness = MinBrightness;
        brightness = Math.Pow(brightness, Gamma);

        return ColorRgb.Pack(
            (int)(ColorRgb.Red(baseColor) * brightness),
            (int)(ColorRgb.Green(baseColor) * brightness),
            (int)(ColorRgb.Blue(baseColor) * brightness));
    }
}


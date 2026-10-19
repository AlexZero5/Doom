using System.Globalization;

namespace Doom.World;

/// <summary>
///     Мировой размер текстур: сколько метров по ширине и высоте занимает один повтор картинки.
///     Кирпич — 1×1 м, обои — 1×2.8 м (плинтус внизу, карниз вверху стены), трава — 2×2 м.
///     Таблица — assets/textures/textures.txt (строки «имя ширина высота»), остальное — 1×1 м.
/// </summary>
internal sealed class TextureMetrics
{
    private readonly Dictionary<string, (double Width, double Height)> _sizes = new(StringComparer.OrdinalIgnoreCase);

    public static TextureMetrics Default { get; } = new();

    public static TextureMetrics Parse(string? text)
    {
        var metrics = new TextureMetrics();
        if (text is null)
            return metrics;

        foreach (string raw in text.Split('\n'))
        {
            string[] parts = raw.Split('#')[0].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 3
                && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double width)
                && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double height)
                && width > 0 && height > 0)
                metrics._sizes[parts[0]] = (width, height);
        }

        return metrics;
    }

    public (double Width, double Height) SizeOf(string name) => _sizes.GetValueOrDefault(name, (1.0, 1.0));
}

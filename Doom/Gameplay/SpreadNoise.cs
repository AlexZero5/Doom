namespace Doom.Gameplay;

/// <summary>Детерминированный «шум» для разброса дробинок, разлёта искр и дыма.</summary>
internal static class SpreadNoise
{
    /// <summary>Псевдослучайное число 0..1 по двум целым ключам (без <see cref="Random" />).</summary>
    public static double Unit(int first, int second)
    {
        unchecked
        {
            uint hash = (uint)(first * 374761393 + second * 668265263);
            hash = (hash ^ (hash >> 13)) * 1274126177u;
            hash ^= hash >> 16;
            return (hash & 0xFFFFFF) / 16777215.0;
        }
    }
}

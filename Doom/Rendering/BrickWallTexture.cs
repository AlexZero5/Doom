namespace Doom.Rendering;

/// <summary>Процедурная текстура кирпичной кладки (без внешних ресурсов).</summary>
internal static class BrickWallTexture
{
    private const int Rows = 6;
    private const double Mortar = 0.06;
    private const double BricksPerRow = 4.0;

    /// <summary>Возвращает множитель яркости для точки текстуры (wallX, wallY).</summary>
    public static (double R, double G, double B) Sample(
        int baseR, int baseG, int baseB, double wallX, double wallY)
    {
        int brickRow = (int)(wallY * Rows);
        if (brickRow >= Rows)
            brickRow = Rows - 1;

        double shift = (brickRow & 1) == 0 ? 0.0 : 0.5;
        double u = wallX * BricksPerRow + shift;
        int brickColumn = (int)u;
        double fractionU = u - brickColumn;
        double fractionV = wallY * Rows - brickRow;

        bool isMortar = fractionU < Mortar || fractionU > 1 - Mortar
                        || fractionV < Mortar || fractionV > 1 - Mortar;

        int seed = brickRow * 73 + brickColumn * 19;
        double variation = 0.85 + (seed * 9301 + 49297) % 233 / 233.0 * 0.30;

        if (isMortar)
            variation *= 0.45;

        variation *= 0.85 + fractionV * 0.25;

        return (baseR * variation, baseG * variation, baseB * variation);
    }
}

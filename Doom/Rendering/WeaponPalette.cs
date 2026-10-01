namespace Doom.Rendering;

/// <summary>Цвета всех стволов от первого лица.</summary>
internal static class WeaponPalette
{
    // Пистолет и общая сталь (значения совпадают с исходной отрисовкой).
    public const int GunBody = 0x3A3A42;
    public const int GunHighlight = 0x6E6E7A;
    public const int GunDark = 0x222228;
    public const int GunMid = 0x35353E;
    public const int GunSteel = 0x5E5E6A;
    public const int WoodStock = 0x6A4526;
    public const int MuzzleCore = 0xFFFFFF;
    public const int MuzzleWarm = 0xFFF070;
    public const int MuzzleShotgun = 0xFFD040;

    // Кулак.
    public const int Skin = 0xC8956C;
    public const int SkinShade = 0x8A6242;

    // Бензопила.
    public const int SawBody = 0xC06020;
    public const int SawGrip = 0x8A4518;
    public const int SawBlade = 0xA8AEB8;
    public const int SawTeeth = 0x2A2E34;

    // Пулемёт.
    public const int ChaingunBody = 0x2E3238;
    public const int ChaingunHousing = 0x3A4048;
    public const int ChaingunMount = 0x24282E;
    public const int ChaingunBarrel = 0x9AA0A8;
    public const int ChaingunRing = 0xB08A30;
    public const int MuzzleChaingun = 0xFFE850;

    // Ракетница.
    public const int RocketTube = 0x5E6A44;
    public const int RocketDark = 0x3A4030;
    public const int RocketBore = 0x14161A;
    public const int RocketSight = 0xC03028;
    public const int FlameOuter = 0xFFA030;
    public const int FlameInner = 0xFFF0A0;

    // Плазменная винтовка.
    public const int PlasmaBody = 0x33485E;
    public const int PlasmaHousing = 0x1E2A3A;
    public const int PlasmaCore = 0x40FF88;
    public const int PlasmaGlow = 0xE8FFD0;

    // BFG9000.
    public const int BfgBody = 0x2A5A22;
    public const int BfgHousing = 0x16320F;
    public const int BfgMuzzle = 0x8CFF3A;
    public const int BfgCore = 0xE8FFD0;
}

using Doom.Gameplay;

namespace Doom.Rendering;

/// <summary>Последняя строка экрана: текущее оружие, боезапас и индикатор стволов.</summary>
internal static class HudRenderer
{
    private const int TextLeft = 2;
    private const int PaddingColor = 0x000000;

    /// <summary>Цвет текста строки состояния.</summary>
    public const int TextColor = 0xE8E8E8;

    /// <summary>Фон строки состояния (его же проверяет автотест).</summary>
    public const int TextBackground = 0x101018;

    /// <summary>Фон подсвеченного слота текущего оружия.</summary>
    public const int SlotHighlight = 0xFFD24A;

    private const int DimColor = 0x707078;
    private const int SlotMissingColor = 0x505058;

    public static void Draw(Framebuffer framebuffer, Player player)
    {
        int y = framebuffer.Rows - 1;
        framebuffer.FillRow(y, Framebuffer.Empty, PaddingColor, TextBackground);

        int x = TextLeft;
        x = Paint(framebuffer, y, x, WeaponCatalog.Get(player.CurrentWeapon).Name, TextColor);
        x = Paint(framebuffer, y, x, "   ", TextColor);

        x = Paint(framebuffer, y, x, AmmoText(player, AmmoKind.Bullets), AmmoColor(AmmoKind.Bullets));
        x = Paint(framebuffer, y, x, AmmoText(player, AmmoKind.Shells), AmmoColor(AmmoKind.Shells));
        x = Paint(framebuffer, y, x, AmmoText(player, AmmoKind.Rockets), AmmoColor(AmmoKind.Rockets));
        x = Paint(framebuffer, y, x, AmmoText(player, AmmoKind.Cells), AmmoColor(AmmoKind.Cells));

        x = Paint(framebuffer, y, x, "  ", TextColor);
        x = PaintSlots(framebuffer, y, x, player);
        Paint(framebuffer, y, x, "  [SPACE/LMB] fire  [1-7] weapon  [Q/E] cycle  [R] restart  [ESC] menu", DimColor);
    }

    /// <summary>Рисует текст сегмента и возвращает позицию следующего символа.</summary>
    private static int Paint(Framebuffer framebuffer, int y, int x, string text, int color)
    {
        for (int index = 0; index < text.Length && x < framebuffer.Columns; index++, x++)
            framebuffer.Set(x, y, text[index], color, TextBackground);

        return x;
    }

    /// <summary>Индикатор слотов: поднятые стволы светлые, текущий — на ярком фоне.</summary>
    private static int PaintSlots(Framebuffer framebuffer, int y, int x, Player player)
    {
        for (int slot = 1; slot <= 7 && x < framebuffer.Columns; slot++)
        {
            bool owned = IsSlotOwned(player, slot);
            int currentSlot = WeaponCatalog.Slot(player.CurrentWeapon);
            int foreground = slot == currentSlot ? TextBackground : owned ? TextColor : SlotMissingColor;
            int background = slot == currentSlot ? SlotHighlight : TextBackground;

            framebuffer.Set(x++, y, (char)('0' + slot), foreground, background);

            if (slot < 7 && x < framebuffer.Columns)
                framebuffer.Set(x++, y, Framebuffer.Empty, TextColor, TextBackground);
        }

        return x;
    }

    private static bool IsSlotOwned(Player player, int slot)
    {
        foreach (WeaponKind kind in WeaponCatalog.SlotWeapons(slot))
        {
            if (player.Owns(kind))
                return true;
        }

        return false;
    }

    private static string AmmoText(Player player, AmmoKind kind) =>
        $" {AmmoCatalog.Letter(kind)} {player.AmmoOf(kind),3}/{AmmoCatalog.Capacity(kind),3}";

    /// <summary>Цвет счётчика совпадает с цветом соответствующего бонуса на карте.</summary>
    private static int AmmoColor(AmmoKind kind) => kind switch
    {
        AmmoKind.Bullets => 0xFFD24A,
        AmmoKind.Shells => 0xE04030,
        AmmoKind.Rockets => 0xC8C8D0,
        AmmoKind.Cells => 0x3AD2FF,
        _ => TextColor
    };
}


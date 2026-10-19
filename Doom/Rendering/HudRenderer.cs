using Doom.Assets;
using Doom.Gameplay;

namespace Doom.Rendering;

/// <summary>
///     Строка состояния как в Doom: панель hud/statusbar.png (320×32) растягивается на ширину
///     кадра; поверх — крупные цифры (патроны, здоровье, броня), слоты оружия, таблица боезапаса
///     и живое лицо героя. Без картинок — текстовая строка, как раньше.
/// </summary>
/// <remarks>
///     Раскладка в пикселях панели совпадает с рисунком (tools/AssetForge/Art/Hud.cs):
///     патроны 2..48, здоровье 49..104, оружие 105..140, лицо 142..177, броня 179..234,
///     таблица боезапаса 236..318.
/// </remarks>
internal sealed class HudRenderer(AssetStore? assets)
{
    private const int BarWidth = 320;
    private const int BarHeight = 32;

    /// <summary>Строка состояния не займёт больше этой доли высоты кадра.</summary>
    private const double MaxShareOfScreen = 0.22;

    private const int TextColor = 0xE8E8E8;
    private const int TextBackground = 0x101018;
    private const int DimColor = 0x707078;

    private readonly int[] _canvas = new int[BarWidth * BarHeight];

    /// <summary>Сколько строк кадра займёт строка состояния при данном размере.</summary>
    public int RowsFor(int columns, int rows)
    {
        if (assets?.GetHud("statusbar") is null)
            return 1;

        double scale = Math.Min(columns / (double)BarWidth, rows * MaxShareOfScreen * 2 / BarHeight);
        return Math.Max(2, (int)Math.Ceiling(BarHeight * scale / 2));
    }

    public void Draw(Framebuffer framebuffer, int viewRows, Player player, double nowSeconds)
    {
        Texture? bar = assets?.GetHud("statusbar");
        if (bar is null)
        {
            DrawText(framebuffer, player);
            return;
        }

        Array.Copy(bar.Pixels, _canvas, Math.Min(_canvas.Length, bar.Pixels.Length));

        WeaponInfo weapon = WeaponCatalog.Get(player.CurrentWeapon);
        if (weapon.Ammo != AmmoKind.None)
            BigNumber(player.AmmoOf(weapon.Ammo), 45, false);

        BigNumber(player.Health, 102, true);
        BigNumber(player.Armor, 232, true);

        // Слоты оружия 2..7: жёлтые — есть, серые — нет (1 — кулак, он есть всегда).
        for (int slot = 2; slot <= 7; slot++)
        {
            int column = (slot - 2) % 3, row = (slot - 2) / 3;
            string variant = SlotOwned(player, slot) ? "yellow" : "gray";
            Blit(assets!.GetHud($"small_{variant}_{slot}"), 112 + column * 10, 8 + row * 9);
        }

        // Таблица боезапаса: текущее / предельное.
        AmmoKind[] kinds = { AmmoKind.Bullets, AmmoKind.Shells, AmmoKind.Rockets, AmmoKind.Cells };
        for (int i = 0; i < kinds.Length; i++)
        {
            SmallNumber(player.AmmoOf(kinds[i]), 288, 5 + i * 6);
            SmallNumber(AmmoCatalog.Capacity(kinds[i]), 316, 5 + i * 6);
        }

        Texture? face = assets!.GetHud(FaceFor(player, nowSeconds));
        if (face is not null)
            BlitScaled(face, 147, 2, 25, 29);

        Present(framebuffer, viewRows);
    }

    /// <summary>Счётчик FPS в правом верхнем углу вида (мелкими цифрами, крупнее обычного).</summary>
    public void DrawFps(Framebuffer framebuffer, int fps)
    {
        if (assets?.GetHud("small_yellow_0") is null)
            return;

        string text = Math.Clamp(fps, 0, 999).ToString();
        int scale = Math.Max(1, framebuffer.Columns / 160);
        int x = framebuffer.Columns - (text.Length * 5 + 2) * scale;
        foreach (char c in text)
        {
            Texture? digit = assets.GetHud($"small_yellow_{c}");
            if (digit is not null)
                StampScaled(framebuffer, digit, x, 2 * scale, scale);
            x += 5 * scale;
        }
    }

    // ============================================================
    //   Лицо
    // ============================================================

    /// <summary>
    ///     Какое лицо показать: ухмылка после находки оружия, злость при долгой стрельбе,
    ///     в покое — оглядывается по сторонам.
    /// </summary>
    private static string FaceFor(Player player, double now)
    {
        if (now - player.WeaponPickupTime < 1.6)
            return "face_grin";

        if (!double.IsNaN(player.TriggerHeldSince) && now - player.TriggerHeldSince > 0.8)
            return "face_angry";

        double cycle = now % 3.2;
        if (cycle < 1.8)
            return "face_center";
        if (cycle < 2.5)
            return ((int)(now / 3.2) & 1) == 0 ? "face_left" : "face_right";
        return "face_center";
    }

    private static bool SlotOwned(Player player, int slot)
    {
        foreach (WeaponKind kind in WeaponCatalog.SlotWeapons(slot))
        {
            if (player.Owns(kind))
                return true;
        }

        return false;
    }

    // ============================================================
    //   Сборка панели в её собственной сетке 320×32
    // ============================================================

    /// <summary>Крупное число, выровненное по правому краю <paramref name="right" />.</summary>
    private void BigNumber(int value, int right, bool percent)
    {
        string text = Math.Max(0, value).ToString();
        int x = right;

        if (percent)
        {
            Texture? sign = assets!.GetHud("big_percent");
            if (sign is not null)
            {
                x -= sign.Width - 2;
                Blit(sign, x, 3);
            }
        }

        for (int i = text.Length - 1; i >= 0; i--)
        {
            Texture? digit = assets!.GetHud($"big_{text[i]}");
            if (digit is null)
                continue;

            x -= digit.Width - 2;
            Blit(digit, x, 3);
        }
    }

    /// <summary>Мелкое число, выровненное по правому краю.</summary>
    private void SmallNumber(int value, int right, int y)
    {
        string text = Math.Max(0, value).ToString();
        int x = right - text.Length * 4;
        foreach (char c in text)
        {
            Blit(assets!.GetHud($"small_yellow_{c}"), x, y);
            x += 4;
        }
    }

    private void Blit(Texture? image, int x0, int y0)
    {
        if (image is null)
            return;

        for (int y = 0; y < image.Height; y++)
        for (int x = 0; x < image.Width; x++)
        {
            int argb = image.SamplePixel(x, y);
            int cx = x0 + x, cy = y0 + y;
            if (!Texture.IsTransparent(argb) && cx >= 0 && cy >= 0 && cx < BarWidth && cy < BarHeight)
                _canvas[cy * BarWidth + cx] = argb;
        }
    }

    private void BlitScaled(Texture image, int x0, int y0, int width, int height)
    {
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int argb = image.SamplePixel(x * image.Width / width, y * image.Height / height);
            int cx = x0 + x, cy = y0 + y;
            if (!Texture.IsTransparent(argb) && cx < BarWidth && cy < BarHeight)
                _canvas[cy * BarWidth + cx] = argb;
        }
    }

    /// <summary>Растягивает панель на строки кадра под видом (по центру, по бокам — тёмный металл).</summary>
    private void Present(Framebuffer framebuffer, int viewRows)
    {
        int rows = framebuffer.Rows - viewRows;
        if (rows <= 0)
            return;

        int pixelRows = rows * 2;
        double scale = Math.Min(framebuffer.Columns / (double)BarWidth, pixelRows / (double)BarHeight);
        int width = (int)Math.Round(BarWidth * scale);
        int left = (framebuffer.Columns - width) / 2;
        int top = pixelRows - (int)Math.Round(BarHeight * scale);

        for (int pixel = 0; pixel < pixelRows; pixel++)
        {
            int rowBase = (viewRows + (pixel >> 1)) * framebuffer.Columns;
            int[] target = (pixel & 1) == 0 ? framebuffer.Foreground : framebuffer.Background;
            int sourceY = Math.Clamp((int)((pixel - top + 0.5) / scale), 0, BarHeight - 1);

            for (int column = 0; column < framebuffer.Columns; column++)
            {
                int sourceX = (int)((column - left + 0.5) / scale);
                int color = sourceX < 0 || sourceX >= BarWidth || pixel < top
                    ? 0x2a2622
                    : _canvas[sourceY * BarWidth + sourceX] & 0xFFFFFF;

                target[rowBase + column] = color;
                framebuffer.Chars[rowBase + column] = Framebuffer.UpperHalfBlock;
            }
        }
    }

    /// <summary>Мелкая картинка прямо в кадр с целым увеличением.</summary>
    private static void StampScaled(Framebuffer framebuffer, Texture image, int x0, int pixelY0, int scale)
    {
        for (int y = 0; y < image.Height * scale; y++)
        for (int x = 0; x < image.Width * scale; x++)
        {
            int argb = image.SamplePixel(x / scale, y / scale);
            int column = x0 + x, pixel = pixelY0 + y;
            if (Texture.IsTransparent(argb) || column < 0 || column >= framebuffer.Columns || pixel >= framebuffer.Rows * 2)
                continue;

            int index = (pixel >> 1) * framebuffer.Columns + column;
            ((pixel & 1) == 0 ? framebuffer.Foreground : framebuffer.Background)[index] = argb & 0xFFFFFF;
            framebuffer.Chars[index] = Framebuffer.UpperHalfBlock;
        }
    }

    // ============================================================
    //   Запасной текстовый вариант (без картинок)
    // ============================================================

    private static void DrawText(Framebuffer framebuffer, Player player)
    {
        int y = framebuffer.Rows - 1;
        framebuffer.FillRow(y, Framebuffer.Empty, 0, TextBackground);

        string text = $"  {WeaponCatalog.Get(player.CurrentWeapon).Name}   B {player.Bullets}  S {player.Shells}" +
                      $"  R {player.Rockets}  C {player.Cells}   HP {player.Health}%";
        for (int x = 0; x < text.Length && x < framebuffer.Columns; x++)
            framebuffer.Set(x, y, text[x], x < 2 ? DimColor : TextColor, TextBackground);
    }
}

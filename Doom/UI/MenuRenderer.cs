using Doom.Gameplay;
using Doom.Rendering;

namespace Doom.UI;

/// <summary>
///     Рисует меню прямо в кадре (в стиле игры): сцену затемняет, а поверх выводит рамку,
///     заголовок и пункты. У настроек и читов у каждого пункта есть строка-значение:
///     ползунок, список опций или переключатель.
/// </summary>
internal static class MenuRenderer
{
    private const int PanelBackground = 0x141B2E;
    private const int BorderColor = 0x4A90D9;
    private const int TitleColor = 0xFFD24A;
    private const int TextColor = 0xE8E8E8;
    private const int ValueColor = 0x9EE8FF;
    private const int HintColor = 0x8A93A8;
    private const int HintBackground = 0x05070C;
    private const int SelectedForeground = 0x0A0A14;
    private const int SelectedBackground = 0xFFD24A;
    private const int SliderFill = 0x3AD2FF;
    private const int SliderTrack = 0x33405C;

    /// <summary>Отрисовывает меню поверх уже готового кадра.</summary>
    public static void Draw(Framebuffer framebuffer, MenuController menu, Player player)
    {
        Dim(framebuffer);

        IReadOnlyList<MenuEntry> entries = menu.BuildEntries(player);
        int selected = menu.SelectedIndex;
        bool detailed = menu.Detailed;

        int lineHeight = detailed ? 3 : 1;
        int columns = framebuffer.Columns;
        int rows = framebuffer.Rows;

        int availableForEntries = Math.Max(lineHeight, rows - 10);
        int maxVisible = Math.Max(1, availableForEntries / lineHeight);
        int shownCount = Math.Min(maxVisible, entries.Count);
        int panelHeight = 4 + shownCount * lineHeight;
        int panelWidth = ComputeWidth(entries, columns);
        int first = entries.Count <= maxVisible
            ? 0
            : Math.Clamp(selected - maxVisible / 2, 0, entries.Count - maxVisible);

        int left = Math.Max(0, (columns - panelWidth) / 2);
        int top = Math.Max(1, (rows - panelHeight) / 2 - 1);

        DrawFrame(framebuffer, left, top, panelWidth, panelHeight, menu.Title);
        DrawEntries(framebuffer, left, top, panelWidth, lineHeight, entries, first, shownCount, selected, detailed);
        DrawHint(framebuffer, menu.Hint, top + panelHeight + 1);
    }

    /// <summary>Ширина панели: по самой длинной подписи, но не шире окна.</summary>
    private static int ComputeWidth(IReadOnlyList<MenuEntry> entries, int columns)
    {
        int maxLabel = 12;
        foreach (MenuEntry entry in entries)
            maxLabel = Math.Max(maxLabel, entry.Label.Length);

        return Math.Clamp(maxLabel + 16, 34, Math.Max(34, columns - 2));
    }

    /// <summary>Затемняет готовый кадр, чтобы меню читалось поверх сцены.</summary>
    private static void Dim(Framebuffer framebuffer)
    {
        int cells = framebuffer.Columns * framebuffer.Rows;

        for (int index = 0; index < cells; index++)
        {
            framebuffer.Foreground[index] = ColorRgb.Blend(framebuffer.Foreground[index], 0x000000, 0.55);
            framebuffer.Background[index] = ColorRgb.Blend(framebuffer.Background[index], 0x000000, 0.55);
        }
    }

    /// <summary>Рисует рамку с заголовком.</summary>
    private static void DrawFrame(Framebuffer framebuffer, int left, int top, int width, int height, string title)
    {
        for (int y = top; y < top + height; y++)
            FillRow(framebuffer, left, y, width, PanelBackground);

        int right = left + width - 1;
        int bottom = top + height - 1;

        for (int x = left + 1; x < right; x++)
        {
            Put(framebuffer, x, top, '═', BorderColor, PanelBackground);
            Put(framebuffer, x, bottom, '═', BorderColor, PanelBackground);
            Put(framebuffer, x, top + 2, '═', BorderColor, PanelBackground);
        }

        for (int y = top + 1; y < bottom; y++)
        {
            Put(framebuffer, left, y, '║', BorderColor, PanelBackground);
            Put(framebuffer, right, y, '║', BorderColor, PanelBackground);
        }

        Put(framebuffer, left, top, '╔', BorderColor, PanelBackground);
        Put(framebuffer, right, top, '╗', BorderColor, PanelBackground);
        Put(framebuffer, left, bottom, '╚', BorderColor, PanelBackground);
        Put(framebuffer, right, bottom, '╝', BorderColor, PanelBackground);
        Put(framebuffer, left, top + 2, '╠', BorderColor, PanelBackground);
        Put(framebuffer, right, top + 2, '╣', BorderColor, PanelBackground);

        int titleX = left + Math.Max(2, (width - title.Length) / 2);
        Text(framebuffer, titleX, top + 1, title, TitleColor, PanelBackground);
    }

    /// <summary>Подсказка по управлению под панелью.</summary>
    private static void DrawHint(Framebuffer framebuffer, string hint, int y)
    {
        if (y < 0 || y >= framebuffer.Rows)
            return;

        FillRow(framebuffer, 0, y, framebuffer.Columns, HintBackground);
        int x = Math.Max(1, (framebuffer.Columns - hint.Length) / 2);
        Text(framebuffer, x, y, hint, HintColor, HintBackground);
    }

    /// <summary>Рисует видимые пункты: подпись и, для подробных экранов, строку-значение.</summary>
    private static void DrawEntries(Framebuffer framebuffer, int left, int top, int width, int lineHeight,
        IReadOnlyList<MenuEntry> entries, int first, int shownCount, int selected, bool detailed)
    {
        int y = top + 3;

        for (int i = first; i < first + shownCount; i++)
        {
            MenuEntry entry = entries[i];
            bool isSelected = i == selected;

            DrawLabel(framebuffer, left, width, y, entry.Label, isSelected);

            if (detailed)
            {
                DrawControl(framebuffer, left, width, y + 1, entry, isSelected);
                y += lineHeight;
            }
            else
            {
                y += 1;
            }
        }
    }

    /// <summary>Подпись пункта; у выбранного вся строка подсвечивается.</summary>
    private static void DrawLabel(Framebuffer framebuffer, int left, int width, int y, string label, bool selected)
    {
        int foreground = selected ? SelectedForeground : TextColor;
        int background = selected ? SelectedBackground : PanelBackground;

        FillRow(framebuffer, left + 1, y, width - 2, background);
        Text(framebuffer, left + 2, y, (selected ? "> " : "  ") + label, foreground, background);
    }

    /// <summary>Строка-значение: ползунок, список опций, переключатель или действие.</summary>
    private static void DrawControl(Framebuffer framebuffer, int left, int width, int y, MenuEntry entry, bool selected)
    {
        FillRow(framebuffer, left + 1, y, width - 2, PanelBackground);
        int x = left + 4;

        switch (entry.Kind)
        {
            case MenuEntryKind.Slider:
                DrawSlider(framebuffer, x, y, width, entry);
                break;
            case MenuEntryKind.Option:
                Text(framebuffer, x, y, "< " + entry.Value + " >", ValueColor, PanelBackground);
                break;
            case MenuEntryKind.Toggle:
                Text(framebuffer, x, y, "[ " + entry.Value + " ]",
                    selected ? SelectedBackground : ValueColor, PanelBackground);
                break;
            case MenuEntryKind.Action:
                Text(framebuffer, x, y, "[ " + entry.Value + " ]", TitleColor, PanelBackground);
                break;
        }
    }

    /// <summary>Ползунок: заполненная часть яркая, дорожка тёмная, значение — справа.</summary>
    private static void DrawSlider(Framebuffer framebuffer, int x, int y, int panelWidth, MenuEntry entry)
    {
        int innerWidth = panelWidth - 8;
        int barWidth = Math.Clamp(innerWidth - entry.Value.Length - 2, 8, 28);
        int filled = (int)Math.Round(entry.Fraction * barWidth);

        for (int i = 0; i < barWidth; i++)
        {
            bool on = i < filled;
            Put(framebuffer, x + i, y, on ? '█' : '░', on ? SliderFill : SliderTrack, PanelBackground);
        }

        Text(framebuffer, x + barWidth + 2, y, entry.Value, ValueColor, PanelBackground);
    }

    private static void Text(Framebuffer framebuffer, int x, int y, string text, int foreground, int background)
    {
        if (y < 0 || y >= framebuffer.Rows)
            return;

        for (int i = 0; i < text.Length; i++)
        {
            int cellX = x + i;
            if (cellX < 0 || cellX >= framebuffer.Columns)
                continue;

            framebuffer.Set(cellX, y, text[i], foreground, background);
        }
    }

    private static void FillRow(Framebuffer framebuffer, int x, int y, int width, int color)
    {
        if (y < 0 || y >= framebuffer.Rows)
            return;

        for (int i = 0; i < width; i++)
        {
            int cellX = x + i;
            if (cellX < 0 || cellX >= framebuffer.Columns)
                continue;

            framebuffer.Set(cellX, y, Framebuffer.Empty, color, color);
        }
    }

    private static void Put(Framebuffer framebuffer, int x, int y, char value, int foreground, int background)
    {
        if (x < 0 || x >= framebuffer.Columns || y < 0 || y >= framebuffer.Rows)
            return;

        framebuffer.Set(x, y, value, foreground, background);
    }
}
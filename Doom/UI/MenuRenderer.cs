using Doom.Assets;
using Doom.Gameplay;
using Doom.Rendering;

namespace Doom.UI;

/// <summary>
///     Рисует меню теми же «пикселями» (символ ▀), что и игровая сцена: панель, рамка,
///     заголовок и пункты собираются из пикселей и растрового шрифта, поэтому меню
///     масштабируется вместе с экраном и выглядит частью игры, а не Console.WriteLine.
/// </summary>
internal static class MenuRenderer
{
    private const int PanelBackground = 0x0E1526;
    private const int PanelShadow = 0x000000;
    private const int BorderColor = 0x4A90D9;
    private const int DividerColor = 0x24405E;
    private const int TitleColor = 0xFFD24A;
    private const int TextColor = 0xD8DEEA;
    private const int ValueColor = 0x8FD8FF;
    private const int HintColor = 0x8A93A8;
    private const int HintBackground = 0x05070C;
    private const int SelectedForeground = 0x10131F;
    private const int SelectedBackground = 0xFFD24A;
    private const int HoverBackground = 0x1D2C4E;
    private const int HoverForeground = 0xFFFFFF;
    private const int SliderFill = 0x3AD2FF;
    private const int SliderTrack = 0x2A3550;
    private const int KnobColor = 0xFFFFFF;
    private const int KnobBorder = 0x0A0A14;
    private const int BoxOffColor = 0x5A6B8C;
    private const int ScrollColor = 0x5A6B8C;

    // Метрики раскладки в «пикселях» меню.
    private const int Padding = 6;
    private const int TitleHeight = 14;
    private const int TitleGap = 6;
    private const int ItemGap = 2;
    private const int BarHeight = 9;
    private const int SimpleStep = BarHeight + ItemGap;
    private const int DetailedStep = BarHeight + 8 + ItemGap;
    private const int TrackWidth = 56;

    /// <summary>Отрисовывает меню поверх уже готового кадра и сохраняет раскладку для мыши.</summary>
    public static void Draw(Framebuffer framebuffer, MenuController menu, Player player, AssetStore? assets = null)
    {
        Dim(framebuffer);

        IReadOnlyList<MenuEntry> entries = menu.BuildEntries(player);
        var canvas = new PixelCanvas(framebuffer);

        // Заливки шрифта: обычное состояние и hover. Нет текстур — сплошные цвета.
        Texture? normalFill = assets?.GetFont("menu_normal");
        Texture? hoverFill = assets?.GetFont("menu_hover");

        bool detailed = menu.Detailed;
        int step = detailed ? DetailedStep : SimpleStep;

        int contentWidth = MeasureContent(entries, detailed);
        int panelWidth = contentWidth + Padding * 2;

        // Масштаб: самый крупный, при котором панель влезает и не съедает весь экран.
        int scale = 1;
        int maxVisible = Math.Max(1, (canvas.Height - (TitleHeight + TitleGap + Padding)) / step);

        foreach (int candidate in new[] { 4, 3, 2 })
        {
            int visible = Math.Min(entries.Count,
                Math.Max(1, (canvas.Height / candidate - (TitleHeight + TitleGap + Padding)) / step));
            int candidateHeight = TitleHeight + TitleGap + visible * step + Padding;

            if (panelWidth * candidate <= canvas.Width
                && candidateHeight * candidate <= canvas.Height
                && panelWidth * candidate <= canvas.Width * 68 / 100
                && candidateHeight * candidate <= canvas.Height * 95 / 100)
            {
                scale = candidate;
                maxVisible = visible;
                break;
            }
        }

        maxVisible = Math.Min(maxVisible, entries.Count);
        menu.MaxVisible = maxVisible;

        int panelHeight = TitleHeight + TitleGap + maxVisible * step + Padding;
        int panelLeft = Math.Max(0, (canvas.Width / scale - panelWidth) / 2);
        int panelTop = Math.Max(1, (canvas.Height / scale - panelHeight) / 2);

        var layout = new MenuLayout
        {
            Scale = scale,
            PanelLeft = panelLeft,
            PanelTop = panelTop,
            PanelWidth = panelWidth,
            PanelHeight = panelHeight
        };

        DrawPanel(canvas, panelLeft, panelTop, panelWidth, panelHeight, menu.Title, scale, hoverFill);

        int contentLeft = panelLeft + Padding;
        int contentRight = panelLeft + panelWidth - Padding;
        int contentTop = panelTop + TitleHeight + TitleGap;
        int first = Math.Clamp(menu.First, 0, Math.Max(0, entries.Count - maxVisible));

        for (int visible = 0; visible < maxVisible; visible++)
        {
            int entryIndex = first + visible;
            if (entryIndex >= entries.Count)
                break;

            MenuEntry entry = entries[entryIndex];
            bool isSelected = entryIndex == menu.SelectedIndex;
            bool isHovered = entryIndex == menu.HoverIndex;
            int barTop = contentTop + visible * step;
            int controlTop = barTop + BarHeight + 1;

            var geometry = new MenuLayout.MenuItemGeom
            {
                EntryIndex = entryIndex,
                Id = entry.Id,
                Param = entry.Param,
                BarTop = barTop,
                BarBottom = entry.Kind == MenuEntryKind.Action || !detailed
                    ? barTop + BarHeight
                    : controlTop + 8,
                ContentLeft = contentLeft,
                ContentRight = contentRight
            };

            layout.Items.Add(geometry);

            switch (entry.Kind)
            {
                case MenuEntryKind.Action:
                    DrawActionButton(canvas, geometry, entry, isSelected, isHovered, scale,
                        normalFill, hoverFill);
                    break;

                case MenuEntryKind.Slider when detailed:
                    DrawLabelRow(canvas, geometry, entry.Label, isSelected, isHovered, scale,
                        normalFill, hoverFill);
                    DrawSlider(canvas, geometry, entry, controlTop, scale);
                    break;

                case MenuEntryKind.Option when detailed:
                    DrawLabelRow(canvas, geometry, entry.Label, isSelected, isHovered, scale,
                        normalFill, hoverFill);
                    DrawOption(canvas, geometry, entry, controlTop, isHovered, scale);
                    break;

                case MenuEntryKind.Toggle when detailed:
                    DrawLabelRow(canvas, geometry, entry.Label, isSelected, isHovered, scale,
                        normalFill, hoverFill);
                    DrawToggle(canvas, geometry, entry, controlTop, scale);
                    break;

                default:
                    DrawLabelRow(canvas, geometry, entry.Label, isSelected, isHovered, scale,
                        normalFill, hoverFill, withPointer: true);
                    break;
            }
        }

        DrawScrollIndicators(canvas, panelLeft, panelWidth, contentTop, first, maxVisible,
            entries.Count, step, scale);
        DrawHint(canvas, menu.Hint, canvas.Width / scale, canvas.Height / scale, scale);

        menu.Layout = layout;
    }

    /// <summary>Считает ширину содержимого: подписи, ползунки, значения.</summary>
    private static int MeasureContent(IReadOnlyList<MenuEntry> entries, bool detailed)
    {
        int width = 24;

        foreach (MenuEntry entry in entries)
        {
            int controlWidth = entry.Kind switch
            {
                MenuEntryKind.Action => PixelFont.Measure(entry.Label) + 14,
                MenuEntryKind.Slider when detailed => TrackWidth + 4 + PixelFont.Measure(entry.Value),
                MenuEntryKind.Option when detailed => 5 + 8 + PixelFont.Measure(entry.Value) + 8 + 5,
                MenuEntryKind.Toggle when detailed => 7 + 4 + PixelFont.Measure(entry.Value),
                _ => 0
            };

            int labelWidth = 8 + PixelFont.Measure(entry.Label);
            width = Math.Max(width, Math.Max(controlWidth, labelWidth));
        }

        return width;
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

    /// <summary>Панель с тенью, рамкой, заголовком и разделителем.</summary>
    private static void DrawPanel(PixelCanvas canvas, int left, int top, int width, int height,
        string title, int scale, Texture? titleFill)
    {
        int s = scale;

        // Тень: смещена вправо-вниз и полупрозрачна.
        for (int y = top + 2; y < top + height + 2; y++)
        {
            for (int x = left + 2; x < left + width + 2; x++)
                canvas.BlendPixel(x * s, y * s, PanelShadow, 0.5);
        }

        canvas.Rect(left * s, top * s, width * s, height * s, PanelBackground);
        canvas.FrameRect(left * s, top * s, width * s, height * s, BorderColor);

        int titleWidth = PixelFont.Measure(title) * 2;
        int titleX = (left + Math.Max(1, (width - titleWidth) / 2)) * s;
        int titleY = (top + 2) * s;

        if (titleFill is not null)
            canvas.TextFilled(titleX, titleY, title, titleFill, TitleColor, 2 * s);
        else
            canvas.TextScaled(titleX, titleY, title, TitleColor, 2 * s);

        int dividerY = (top + TitleHeight + TitleGap - 2) * s;
        canvas.Rect((left + 1) * s, dividerY, (width - 2) * s, s, DividerColor);
    }

    /// <summary>
    ///     Строка подписи пункта: выбранная — тёмный текст на золоте, под курсором —
    ///     hover-заливка, обычная — заливка menu_normal. Нет текстур — сплошные цвета.
    /// </summary>
    private static void DrawLabelRow(PixelCanvas canvas, MenuLayout.MenuItemGeom geometry, string label,
        bool selected, bool hovered, int scale, Texture? normalFill, Texture? hoverFill,
        bool withPointer = false)
    {
        int s = scale;
        string text = label.ToUpperInvariant();

        (int background, int foreground) = (selected, hovered) switch
        {
            (true, _) => (SelectedBackground, SelectedForeground),
            (false, true) => (HoverBackground, HoverForeground),
            _ => (PanelBackground, TextColor)
        };

        canvas.Rect(geometry.ContentLeft * s, geometry.BarTop * s,
            (geometry.ContentRight - geometry.ContentLeft) * s, BarHeight * s, background);

        int x = geometry.ContentLeft;
        int textY = (geometry.BarTop + 1) * s;

        if (withPointer)
        {
            if (selected || hovered)
                canvas.Text(x * s, textY, "►",
                    selected ? SelectedForeground : HoverForeground, s);

            x += 8;
        }

        if (selected || normalFill is null && hoverFill is null)
        {
            canvas.Text(x * s, textY, text, foreground, s);
        }
        else
        {
            // Выбранная строка остаётся тёмной на золоте; остальным — заливки.
            Texture? fill = hovered ? hoverFill ?? normalFill : normalFill ?? hoverFill;
            if (fill is not null && !selected)
                canvas.TextFilled(x * s, textY, text, fill, foreground, s);
            else
                canvas.Text(x * s, textY, text, foreground, s);
        }
    }

    /// <summary>Кнопка-действие (например «Дать всё»).</summary>
    private static void DrawActionButton(PixelCanvas canvas, MenuLayout.MenuItemGeom geometry,
        MenuEntry entry, bool selected, bool hovered, int scale,
        Texture? normalFill, Texture? hoverFill)
    {
        int s = scale;
        string text = entry.Label.ToUpperInvariant();
        int buttonWidth = PixelFont.Measure(text) + 12;
        int contentWidth = geometry.ContentRight - geometry.ContentLeft;
        int x = geometry.ContentLeft + Math.Max(0, (contentWidth - buttonWidth) / 2);
        int y = geometry.BarTop;
        int textX = (x + 6) * s;
        int textY = (y + 1) * s;

        if (selected || hovered)
        {
            canvas.Rect(x * s, y * s, buttonWidth * s, BarHeight * s, SelectedBackground);
            canvas.Text(textX, textY, text, SelectedForeground, s);
        }
        else
        {
            canvas.FrameRect(x * s, y * s, buttonWidth * s, BarHeight * s, TitleColor);

            if (hoverFill is not null)
                canvas.TextFilled(textX, textY, text, hoverFill, TitleColor, s);
            else if (normalFill is not null)
                canvas.TextFilled(textX, textY, text, normalFill, TitleColor, s);
            else
                canvas.Text(textX, textY, text, TitleColor, s);
        }
    }

    /// <summary>Ползунок: дорожка, заполнение, ручка и числовое значение справа.</summary>
    private static void DrawSlider(PixelCanvas canvas, MenuLayout.MenuItemGeom geometry, MenuEntry entry,
        int controlTop, int scale)
    {
        int s = scale;
        int trackLeft = geometry.ContentLeft + 2;
        int trackY = controlTop + 2;
        int valueWidth = PixelFont.Measure(entry.Value);
        int valueX = geometry.ContentRight - valueWidth;

        canvas.Rect(trackLeft * s, trackY * s, TrackWidth * s, 3 * s, SliderTrack);

        int filled = (int)Math.Round(entry.Fraction * (TrackWidth - 1));
        canvas.Rect(trackLeft * s, trackY * s, (filled + 1) * s, 3 * s, SliderFill);

        int knobX = trackLeft + filled - 2;
        canvas.Rect(knobX * s, controlTop * s, 5 * s, 7 * s, KnobColor);
        canvas.FrameRect(knobX * s, controlTop * s, 5 * s, 7 * s, KnobBorder);

        canvas.Text(valueX * s, controlTop * s, entry.Value, ValueColor, s);

        geometry.TrackLeft = trackLeft;
        geometry.TrackRight = trackLeft + TrackWidth;
    }

    /// <summary>Список опций: «◄ значение ►» со стрелками по краям.</summary>
    private static void DrawOption(PixelCanvas canvas, MenuLayout.MenuItemGeom geometry, MenuEntry entry,
        int controlTop, bool hovered, int scale)
    {
        int s = scale;
        string value = entry.Value.ToUpperInvariant();

        int arrowLeftX = geometry.ContentLeft + 2;
        int arrowRightX = geometry.ContentRight - 7;
        int arrowColor = hovered ? HoverForeground : BorderColor;

        canvas.Text(arrowLeftX * s, controlTop * s, "◄", arrowColor, s);
        canvas.Text(arrowRightX * s, controlTop * s, "►", arrowColor, s);

        int betweenLeft = arrowLeftX + 8;
        int betweenRight = arrowRightX - 4;
        int valueWidth = PixelFont.Measure(value);
        int valueX = betweenLeft + Math.Max(0, (betweenRight - betweenLeft - valueWidth) / 2);
        canvas.Text(valueX * s, controlTop * s, value, ValueColor, s);

        geometry.ArrowLeft = arrowLeftX + 2;
        geometry.ArrowRight = arrowRightX + 2;
    }

    /// <summary>Переключатель: квадрат с галочкой и подпись состояния справа.</summary>
    private static void DrawToggle(PixelCanvas canvas, MenuLayout.MenuItemGeom geometry, MenuEntry entry,
        int controlTop, int scale)
    {
        int s = scale;
        int boxX = geometry.ContentLeft + 2;

        if (entry.Fraction >= 0.5)
        {
            canvas.Rect(boxX * s, controlTop * s, 7 * s, 7 * s, SelectedBackground);
            canvas.Text((boxX + 1) * s, controlTop * s, "✓", SelectedForeground, s);
        }
        else
        {
            canvas.FrameRect(boxX * s, controlTop * s, 7 * s, 7 * s, BoxOffColor);
        }

        canvas.Text((geometry.ContentRight - PixelFont.Measure(entry.Value)) * s, controlTop * s,
            entry.Value, ValueColor, s);

        geometry.BoxLeft = boxX;
        geometry.BoxRight = boxX + 7;
    }

    /// <summary>Стрелки прокрутки, если пункты не поместились целиком.</summary>
    private static void DrawScrollIndicators(PixelCanvas canvas, int panelLeft, int panelWidth,
        int contentTop, int first, int maxVisible, int total, int step, int scale)
    {
        int s = scale;
        int x = panelLeft + panelWidth - 8;

        if (first > 0)
        {
            int y = contentTop - 3;
            canvas.Pixel((x + 2) * s, y * s, ScrollColor);

            for (int dx = 1; dx <= 3; dx++)
                canvas.Pixel((x + dx) * s, (y + 1) * s, ScrollColor);

            for (int dx = 0; dx < 5; dx++)
                canvas.Pixel((x + dx) * s, (y + 2) * s, ScrollColor);
        }

        if (first + maxVisible < total)
        {
            int y = contentTop + maxVisible * step + 1;

            for (int dx = 0; dx < 5; dx++)
                canvas.Pixel((x + dx) * s, y * s, ScrollColor);

            for (int dx = 1; dx <= 3; dx++)
                canvas.Pixel((x + dx) * s, (y + 1) * s, ScrollColor);

            canvas.Pixel((x + 2) * s, (y + 2) * s, ScrollColor);
        }
    }

    /// <summary>Полоса подсказки по управлению внизу экрана.</summary>
    private static void DrawHint(PixelCanvas canvas, string hint, int areaWidth, int areaHeight, int scale)
    {
        int s = scale;
        string text = hint.ToUpperInvariant();
        int barHeight = BarHeight + 2;
        int y = Math.Max(0, areaHeight - barHeight - 1);

        canvas.Rect(0, y * s, canvas.Width, barHeight * s, HintBackground);

        int x = Math.Max(2, (areaWidth - PixelFont.Measure(text)) / 2);
        canvas.Text(x * s, (y + 1) * s, text, HintColor, s);
    }
}

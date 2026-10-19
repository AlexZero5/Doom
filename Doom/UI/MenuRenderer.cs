using Doom.Assets;
using Doom.Gameplay;
using Doom.Rendering;

namespace Doom.UI;

/// <summary>
///     Меню поверх затемнённой сцены: логотип, металлическая рамка-панель (ui/panel, растяжение
///     «9 частями»), анимированный череп-указатель, пункты растровым шрифтом с заливками
///     (обычная / при наведении / заголовок / значения), ползунки и переключатели из картинок.
///     Всё рисуется «пикселями» кадра и масштабируется целым множителем.
/// </summary>
internal static class MenuRenderer
{
    // Цвета запасного вида (если картинок нет) и подложек.
    private const int PanelBackground = 0x1c1e24;
    private const int BorderColor = 0x6c5a48;
    private const int TitleColor = 0xFFD24A;
    private const int TextColor = 0xD8DEEA;
    private const int ValueColor = 0x8FD8FF;
    private const int HintColor = 0x8A93A8;
    private const int HintBackground = 0x05070C;
    private const int SelectedBackground = 0x3a1810;
    private const int HoverBackground = 0x24201c;

    private const int PanelBorder = 12;
    private const int TrackWidth = 64;
    private const int ApplyButtonGap = 4;

    private static BitmapFont? _font;
    private static AssetStore? _fontAssets;
    private static int _fontVersion = -1;

    /// <summary>Отрисовывает меню поверх готового кадра и сохраняет раскладку для мыши.</summary>
    public static void Draw(Framebuffer framebuffer, MenuController menu, Player player, AssetStore? assets = null,
        double nowSeconds = 0)
    {
        Dim(framebuffer);

        BitmapFont? font = FontFor(assets);
        var text = new MenuText(font, assets);
        IReadOnlyList<MenuEntry> entries = menu.BuildEntries(player);
        var canvas = new PixelCanvas(framebuffer);

        int lineHeight = text.LineHeight;
        int padding = PanelBorder;
        int barHeight = lineHeight + 2;
        int titleHeight = lineHeight + 4;
        const int titleGap = 4;
        int simpleStep = barHeight + 4;
        int detailedStep = barHeight + 15;

        bool detailed = menu.Detailed;
        int step = detailed ? detailedStep : simpleStep;
        bool showApplyButton = menu.Screen == GameScreen.Settings;
        int buttonZone = showApplyButton ? ApplyButtonGap + barHeight : 0;

        int contentWidth = MeasureContent(entries, detailed, text);
        int panelWidth = Math.Max(contentWidth + padding * 2, text.Measure("ПРИМЕНИТЬ") + 40);

        Texture? logo = menu.Screen == GameScreen.MainMenu ? assets?.GetUi("logo") : null;
        int hintZone = lineHeight + 3;
        int usableHeight = Math.Max(1, canvas.Height - hintZone);
        int chrome = titleHeight + titleGap + padding * 2 + buttonZone;

        // Масштаб: самый крупный, при котором все пункты влезают над подсказкой и не съедают
        // экран целиком. Не влезает даже в ×1 — сначала убираем логотип, потом включаем прокрутку.
        int scale = 1;
        int logoZone = 0;
        int maxVisible = Math.Clamp((usableHeight - chrome) / step, 1, entries.Count);
        bool found = false;

        foreach (bool withLogo in logo is null ? new[] { false } : new[] { true, false })
        {
            int zone = withLogo ? logo!.Height + 6 : 0;
            int width = Math.Max(panelWidth, withLogo ? logo!.Width : 0);
            foreach (int candidate in new[] { 4, 3, 2, 1 })
            {
                int height = chrome + entries.Count * step + zone;
                bool fitsWidth = width * candidate <= canvas.Width * (candidate == 1 ? 100 : 80) / 100;
                bool fitsHeight = height * candidate <= usableHeight * (candidate == 1 ? 100 : 94) / 100;
                if (fitsWidth && fitsHeight)
                {
                    scale = candidate;
                    logoZone = zone;
                    maxVisible = entries.Count;
                    found = true;
                    break;
                }
            }

            if (found)
                break;
        }

        if (!found)
            logo = null;

        menu.MaxVisible = maxVisible;

        int panelHeight = titleHeight + titleGap + maxVisible * step + padding * 2 + buttonZone;
        int totalHeight = panelHeight + logoZone;
        int panelLeft = Math.Max(0, (canvas.Width / scale - panelWidth) / 2);
        int top = Math.Max(1, (usableHeight / scale - totalHeight) / 2);
        int panelTop = top + logoZone;

        if (logo is not null)
            canvas.Image(logo, (canvas.Width / scale - logo.Width) / 2 * scale, top * scale, scale);

        var layout = new MenuLayout
        {
            Scale = scale,
            PanelLeft = panelLeft,
            PanelTop = panelTop,
            PanelWidth = panelWidth,
            PanelHeight = panelHeight
        };

        DrawPanel(canvas, assets, panelLeft, panelTop, panelWidth, panelHeight, scale);

        // Заголовок — огненной заливкой, по центру.
        int titleWidth = text.Measure(menu.Title);
        text.Draw(canvas, (panelLeft + (panelWidth - titleWidth) / 2) * scale, (panelTop + padding - 2) * scale,
            menu.Title, "fill_title", TitleColor, scale);

        int contentLeft = panelLeft + padding;
        int contentRight = panelLeft + panelWidth - padding;
        int contentTop = panelTop + padding + titleHeight + titleGap;
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
            int controlTop = barTop + barHeight + 1;

            var geometry = new MenuLayout.MenuItemGeom
            {
                EntryIndex = entryIndex,
                Id = entry.Id,
                Param = entry.Param,
                BarTop = barTop,
                BarBottom = entry.Kind == MenuEntryKind.Action || !detailed ? barTop + barHeight : controlTop + 12,
                ContentLeft = contentLeft,
                ContentRight = contentRight
            };

            layout.Items.Add(geometry);

            bool lit = isSelected || isHovered;
            if (detailed && lit)
                canvas.Rect(contentLeft * scale, barTop * scale, (contentRight - contentLeft) * scale, (geometry.BarBottom - barTop) * scale,
                    isSelected ? SelectedBackground : HoverBackground);

            switch (entry.Kind)
            {
                case MenuEntryKind.Action:
                {
                    int width = text.Measure(entry.Label) + 12;
                    int x = contentLeft + Math.Max(0, (contentRight - contentLeft - width) / 2);
                    canvas.FrameRect(x * scale, barTop * scale, width * scale, barHeight * scale, lit ? TitleColor : BorderColor);
                    text.Draw(canvas, (x + 6) * scale, (barTop + 1) * scale, entry.Label, lit ? "fill_hover" : "fill_normal", TextColor, scale);
                    break;
                }

                case MenuEntryKind.Slider when detailed:
                    text.Draw(canvas, contentLeft * scale, (barTop + 1) * scale, entry.Label, lit ? "fill_hover" : "fill_normal", TextColor, scale);
                    DrawSlider(canvas, assets, text, geometry, entry, controlTop, scale);
                    break;

                case MenuEntryKind.Option when detailed:
                    text.Draw(canvas, contentLeft * scale, (barTop + 1) * scale, entry.Label, lit ? "fill_hover" : "fill_normal", TextColor, scale);
                    DrawOption(canvas, text, geometry, entry, controlTop, scale);
                    break;

                case MenuEntryKind.Toggle when detailed:
                    text.Draw(canvas, contentLeft * scale, (barTop + 1) * scale, entry.Label, lit ? "fill_hover" : "fill_normal", TextColor, scale);
                    DrawToggle(canvas, assets, text, geometry, entry, controlTop, scale);
                    break;

                default:
                {
                    // Главное меню: пункт по центру, череп-указатель слева от выбранного.
                    int width = text.Measure(entry.Label);
                    int x = contentLeft + Math.Max(16, (contentRight - contentLeft - width) / 2);
                    if (lit)
                        DrawSkull(canvas, assets, (x - 17) * scale, (barTop - 1) * scale, scale, nowSeconds);
                    text.Draw(canvas, x * scale, (barTop + 1) * scale, entry.Label, lit ? "fill_hover" : "fill_normal", TextColor, scale);
                    break;
                }
            }
        }

        if (showApplyButton)
        {
            int buttonTop = panelTop + panelHeight - padding - barHeight;
            var button = new MenuLayout.MenuItemGeom
            {
                EntryIndex = entries.Count,
                Id = MenuEntryId.Apply,
                BarTop = buttonTop,
                BarBottom = buttonTop + barHeight,
                ContentLeft = contentLeft,
                ContentRight = contentRight
            };

            layout.Items.Add(button);
            bool lit = menu.HoverIndex == entries.Count || menu.SelectedIndex == entries.Count;
            int width = text.Measure("ПРИМЕНИТЬ") + 14;
            int x = contentLeft + Math.Max(0, (contentRight - contentLeft - width) / 2);
            if (lit)
                canvas.Rect(x * scale, buttonTop * scale, width * scale, barHeight * scale, SelectedBackground);
            canvas.FrameRect(x * scale, buttonTop * scale, width * scale, barHeight * scale, lit ? TitleColor : BorderColor);
            text.Draw(canvas, (x + 7) * scale, (buttonTop + 1) * scale, "ПРИМЕНИТЬ", lit ? "fill_hover" : "fill_normal", TextColor, scale);
        }

        DrawScrollMarks(canvas, panelLeft, panelWidth, contentTop, first, maxVisible, entries.Count, step, scale);
        DrawHint(canvas, text, menu.Hint);

        menu.Layout = layout;
    }

    private static BitmapFont? FontFor(AssetStore? assets)
    {
        int version = assets?.Version ?? -2;
        if (!ReferenceEquals(assets, _fontAssets) || version != _fontVersion)
        {
            _font = BitmapFont.Load(assets);
            _fontAssets = assets;
            _fontVersion = version;
        }

        return _font;
    }

    /// <summary>Считает ширину содержимого: подписи, ползунки, значения.</summary>
    private static int MeasureContent(IReadOnlyList<MenuEntry> entries, bool detailed, MenuText text)
    {
        int width = 60;
        foreach (MenuEntry entry in entries)
        {
            int control = entry.Kind switch
            {
                MenuEntryKind.Slider when detailed => TrackWidth + 8 + text.Measure(entry.Value),
                MenuEntryKind.Option when detailed => 30 + text.Measure(entry.Value),
                MenuEntryKind.Toggle when detailed => 16 + text.Measure(entry.Value),
                _ => 0
            };

            width = Math.Max(width, Math.Max(control, text.Measure(entry.Label) + (detailed ? 4 : 40)));
        }

        return width;
    }

    /// <summary>Затемняет кадр, чтобы меню читалось поверх сцены (с тёплым оттенком).</summary>
    private static void Dim(Framebuffer framebuffer)
    {
        int cells = framebuffer.Columns * framebuffer.Rows;
        for (int index = 0; index < cells; index++)
        {
            framebuffer.Foreground[index] = ColorRgb.Blend(framebuffer.Foreground[index], 0x0c0606, 0.62);
            framebuffer.Background[index] = ColorRgb.Blend(framebuffer.Background[index], 0x0c0606, 0.62);
        }
    }

    /// <summary>Рамка-панель «9 частями»: уголки как есть, края и середина повторяются.</summary>
    private static void DrawPanel(PixelCanvas canvas, AssetStore? assets, int left, int top, int width, int height, int scale)
    {
        // Тень панели.
        for (int y = top + 3; y < top + height + 3; y++)
        for (int x = left + 3; x < left + width + 3; x++)
            canvas.BlendPixel(x * scale, y * scale, 0, 0.6);

        Texture? panel = assets?.GetUi("panel");
        if (panel is null)
        {
            canvas.Rect(left * scale, top * scale, width * scale, height * scale, PanelBackground);
            canvas.FrameRect(left * scale, top * scale, width * scale, height * scale, BorderColor);
            return;
        }

        int b = PanelBorder;
        int inner = panel.Width - b * 2;
        int[] sx = { 0, b, panel.Width - b };
        int[] sw = { b, inner, b };
        int[] dx = { left, left + b, left + width - b };
        int[] dw = { b, width - b * 2, b };
        int[] sy = { 0, b, panel.Height - b };
        int[] sh = { b, panel.Height - b * 2, b };
        int[] dy = { top, top + b, top + height - b };
        int[] dh = { b, height - b * 2, b };

        for (int row = 0; row < 3; row++)
        for (int column = 0; column < 3; column++)
            canvas.Tile(panel, sx[column], sy[row], sw[column], sh[row],
                dx[column] * scale, dy[row] * scale, dw[column] * scale, dh[row] * scale, scale);
    }

    /// <summary>Череп-указатель: два кадра, глаза мигают раз в 0.3 секунды.</summary>
    private static void DrawSkull(PixelCanvas canvas, AssetStore? assets, int x, int y, int scale, double now)
    {
        Texture? skull = assets?.GetUi(((int)(now / 0.3) & 1) == 0 ? "skull_0" : "skull_1");
        if (skull is not null)
            canvas.Image(skull, x, y, scale);
        else
            canvas.Text(x + 6 * scale, y + 3 * scale, "►", TitleColor, scale);
    }

    /// <summary>Ползунок: жёлоб, раскалённое заполнение, стальная ручка и значение справа.</summary>
    private static void DrawSlider(PixelCanvas canvas, AssetStore? assets, MenuText text, MenuLayout.MenuItemGeom geometry,
        MenuEntry entry, int controlTop, int scale)
    {
        int trackLeft = geometry.ContentLeft + 2;
        int trackY = controlTop + 2;
        int filled = (int)Math.Round(entry.Fraction * (TrackWidth - 1));

        Texture? track = assets?.GetUi("slider_track");
        Texture? fill = assets?.GetUi("slider_fill");
        Texture? knob = assets?.GetUi("slider_knob");

        if (track is not null && fill is not null && knob is not null)
        {
            canvas.Tile(track, 0, 0, track.Width, track.Height, trackLeft * scale, trackY * scale, TrackWidth * scale, track.Height * scale, scale);
            canvas.Tile(fill, 0, 0, fill.Width, fill.Height, trackLeft * scale, (trackY + 1) * scale, (filled + 1) * scale, fill.Height * scale, scale);
            canvas.Image(knob, (trackLeft + filled - knob.Width / 2) * scale, (controlTop - 0) * scale, scale);
        }
        else
        {
            canvas.Rect(trackLeft * scale, trackY * scale, TrackWidth * scale, 3 * scale, 0x2A3550);
            canvas.Rect(trackLeft * scale, trackY * scale, (filled + 1) * scale, 3 * scale, 0x3AD2FF);
        }

        int valueWidth = text.Measure(entry.Value);
        text.Draw(canvas, (geometry.ContentRight - valueWidth) * scale, controlTop * scale, entry.Value, "fill_value", ValueColor, scale);

        geometry.TrackLeft = trackLeft;
        geometry.TrackRight = trackLeft + TrackWidth;
    }

    /// <summary>Список опций: «◄ значение ►».</summary>
    private static void DrawOption(PixelCanvas canvas, MenuText text, MenuLayout.MenuItemGeom geometry, MenuEntry entry,
        int controlTop, int scale)
    {
        int arrowLeftX = geometry.ContentLeft + 2;
        int arrowRightX = geometry.ContentRight - 10;
        text.Draw(canvas, arrowLeftX * scale, controlTop * scale, "◄", "fill_normal", TitleColor, scale);
        text.Draw(canvas, arrowRightX * scale, controlTop * scale, "►", "fill_normal", TitleColor, scale);

        int valueWidth = text.Measure(entry.Value);
        int betweenLeft = arrowLeftX + 12, betweenRight = arrowRightX - 3;
        text.Draw(canvas, (betweenLeft + Math.Max(0, (betweenRight - betweenLeft - valueWidth) / 2)) * scale, controlTop * scale,
            entry.Value, "fill_value", ValueColor, scale);

        geometry.ArrowLeft = arrowLeftX + 6;
        geometry.ArrowRight = arrowRightX + 2;
    }

    /// <summary>Переключатель: гнездо с горящей галочкой и подпись состояния.</summary>
    private static void DrawToggle(PixelCanvas canvas, AssetStore? assets, MenuText text, MenuLayout.MenuItemGeom geometry,
        MenuEntry entry, int controlTop, int scale)
    {
        int boxX = geometry.ContentLeft + 2;
        Texture? box = assets?.GetUi(entry.Fraction >= 0.5 ? "check_on" : "check_off");
        if (box is not null)
            canvas.Image(box, boxX * scale, controlTop * scale, scale);
        else
            canvas.FrameRect(boxX * scale, controlTop * scale, 9 * scale, 9 * scale, entry.Fraction >= 0.5 ? TitleColor : BorderColor);

        int valueWidth = text.Measure(entry.Value);
        text.Draw(canvas, (geometry.ContentRight - valueWidth) * scale, controlTop * scale, entry.Value, "fill_value", ValueColor, scale);

        geometry.BoxLeft = boxX;
        geometry.BoxRight = boxX + 11;
    }

    /// <summary>Отметки прокрутки, если пункты не поместились.</summary>
    private static void DrawScrollMarks(PixelCanvas canvas, int panelLeft, int panelWidth, int contentTop, int first,
        int maxVisible, int total, int step, int scale)
    {
        int x = panelLeft + panelWidth - PanelBorder - 6;
        if (first > 0)
            canvas.Rect(x * scale, (contentTop - 3) * scale, 5 * scale, 1 * scale, TitleColor);
        if (first + maxVisible < total)
            canvas.Rect(x * scale, (contentTop + maxVisible * step) * scale, 5 * scale, 1 * scale, TitleColor);
    }

    /// <summary>Подсказка по управлению внизу экрана (масштаб 1 — мелко и не мешает).</summary>
    private static void DrawHint(PixelCanvas canvas, MenuText text, string hint)
    {
        int height = text.LineHeight + 2;
        int y = canvas.Height - height;
        canvas.Rect(0, y, canvas.Width, height, HintBackground);

        // Узкое окно: подсказка из частей «…   …» теряет первые части, «Esc …» остаётся.
        var parts = new List<string>(hint.Split("   ", StringSplitOptions.RemoveEmptyEntries));
        while (parts.Count > 1 && text.Measure(string.Join("   ", parts)) > canvas.Width - 4)
            parts.RemoveAt(0);

        hint = string.Join("   ", parts);
        int width = text.Measure(hint);
        text.Draw(canvas, Math.Max(2, (canvas.Width - width) / 2), y + 1, hint, "fill_dim", HintColor, 1);
    }

    /// <summary>Текст меню: растровый шрифт с заливкой, а без картинок — встроенный пиксельный.</summary>
    private sealed class MenuText(BitmapFont? font, AssetStore? assets)
    {
        public int LineHeight => font?.Height ?? 9;

        public int Measure(string text) => font?.Measure(text) ?? PixelFont.Measure(text.ToUpperInvariant());

        public void Draw(PixelCanvas canvas, int x, int y, string text, string fillName, int fallbackColor, int scale)
        {
            Texture? fill = assets?.GetUi(fillName);
            if (font is not null && fill is not null)
                font.Draw(canvas, x, y, text, fill, scale);
            else
                canvas.Text(x, y, text.ToUpperInvariant(), fallbackColor, scale);
        }
    }
}

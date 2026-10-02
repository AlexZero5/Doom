namespace Doom.UI;

/// <summary>
///     Геометрия нарисованного меню в «пикселях» игры. Заполняется отрисовщиком
///     каждый кадр и используется контроллером для попаданий мыши в пункты.
/// </summary>
internal sealed class MenuLayout
{
    /// <summary>Сколько пикселей кадра занимает один «пиксель» меню (по каждой оси).</summary>
    public int Scale { get; set; }

    public int PanelLeft { get; set; }

    public int PanelTop { get; set; }

    public int PanelWidth { get; set; }

    public int PanelHeight { get; set; }

    /// <summary>Геометрия видимых пунктов (в порядке отрисовки).</summary>
    public List<MenuItemGeom> Items { get; } = new();

    /// <summary>Геометрия одного пункта: строка подсветки и зоны управления.</summary>
    public sealed class MenuItemGeom
    {
        public int EntryIndex { get; set; }

        public MenuEntryId Id { get; set; }

        public int Param { get; set; }

        /// <summary>Верх строки подсветки пункта.</summary>
        public int BarTop { get; set; }

        /// <summary>Низ строки подсветки пункта (не включительно).</summary>
        public int BarBottom { get; set; }

        /// <summary>Левая граница содержимого пункта.</summary>
        public int ContentLeft { get; set; }

        /// <summary>Правая граница содержимого пункта.</summary>
        public int ContentRight { get; set; }

        /// <summary>Зона ползунка (0, если пункта-ползунка нет).</summary>
        public int TrackLeft { get; set; }

        public int TrackRight { get; set; }

        /// <summary>Центры стрелок «◄» и «►» у пункта-опции (0, если их нет).</summary>
        public int ArrowLeft { get; set; }

        public int ArrowRight { get; set; }

        /// <summary>Зона галочки-переключателя (0, если её нет).</summary>
        public int BoxLeft { get; set; }

        public int BoxRight { get; set; }
    }

    /// <summary>Определяет пункт под координатами мыши (в пикселях меню) или <c>null</c>.</summary>
    public MenuItemGeom? HitTest(int x, int y)
    {
        foreach (MenuItemGeom item in Items)
        {
            if (y >= item.BarTop && y < item.BarBottom && x >= PanelLeft && x < PanelLeft + PanelWidth)
                return item;
        }

        return null;
    }
}

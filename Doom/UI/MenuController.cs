using Doom.Configuration;
using Doom.Gameplay;
using Doom.Input;

namespace Doom.UI;

/// <summary>Что игрок выбрал в меню.</summary>
internal enum MenuCommand
{
    /// <summary>Ничего не изменилось.</summary>
    Nothing,

    /// <summary>Нужно перерисовать меню.</summary>
    Redraw,

    /// <summary>Вернуться в игру.</summary>
    Resume,

    /// <summary>Выйти из игры.</summary>
    Quit
}

/// <summary>
///     Состояние меню и обработка ввода: главное меню, настройки и читы. Менем клавиатурой
///     и мышью: наведение выбирает пункт, клик активирует, ползунки тянутся мышью.
///     Изменения применяются к <see cref="GameSettings" /> и <see cref="Player" /> сразу,
///     а в файл настройки сбрасываются при выходе из экрана настроек.
/// </summary>
internal sealed class MenuController
{
    private const int MainItemCount = 4;
    private const int SettingsItemCount = 9;
    private const int CheatsItemCount = 1 + WeaponCatalog.Count + 4;

    private static readonly string[] QualityNames = { "Низкое", "Среднее", "Высокое", "Ультра" };

    private static readonly string[] WeaponNames =
    {
        "Кулак", "Бензопила", "Пистолет", "Дробовик",
        "Пулемёт", "Ракетница", "Плазма", "BFG9000"
    };

    private static readonly string[] AmmoNames = { "Патроны", "Дробь", "Ракеты", "Ячейки" };

    private static readonly AmmoKind[] AmmoOrder =
    {
        AmmoKind.Bullets, AmmoKind.Shells, AmmoKind.Rockets, AmmoKind.Cells
    };

    private static readonly int[] AmmoStep = { 10, 2, 2, 20 };

    private readonly List<MenuEntry> _entries = new();

    private int _mainIndex;
    private int _settingsIndex;
    private int _cheatsIndex;

    /// <summary>Пункт, на который указывает мышь (-1 — ни на какой).</summary>
    private int _hoverIndex = -1;

    /// <summary>Ползунок, который сейчас тянут мышью (-1 — никого).</summary>
    private int _dragIndex = -1;

    /// <summary>Первый видимый пункт при прокрутке.</summary>
    private int _first;

    /// <summary>Текущий экран меню.</summary>
    public GameScreen Screen { get; private set; } = GameScreen.MainMenu;

    /// <summary>Раскладка последнего кадра: по ней мышь попадает в пункты.</summary>
    public MenuLayout? Layout { get; set; }

    /// <summary>Сколько пунктов видно (считает отрисовщик после кадра).</summary>
    public int MaxVisible { get; set; } = 8;

    /// <summary>Первый видимый пункт (прокрутка).</summary>
    public int First => _first;

    /// <summary>Индекс пункта под курсором мыши.</summary>
    public int HoverIndex => _hoverIndex;

    /// <summary>Сбрасывает меню в главный экран.</summary>
    public void Open()
    {
        Screen = GameScreen.MainMenu;
        _mainIndex = 0;
        _settingsIndex = 0;
        _cheatsIndex = 0;
        _hoverIndex = -1;
        _dragIndex = -1;
        _first = 0;
    }

    public string Title => Screen switch
    {
        GameScreen.Settings => "НАСТРОЙКИ",
        GameScreen.Cheats => "ЧИТЫ",
        _ => "DOOM - ПАУЗА"
    };

    public string Hint => Screen switch
    {
        GameScreen.Settings => "Мышь: кликни по ползунку   Esc назад и сохранить",
        GameScreen.Cheats => "Мышь: клик и тяни ползунок   Esc назад",
        _ => "Мышь: наведи и кликни   Esc продолжить"
    };

    /// <summary>Подробный ли экран (в настройках и читах у каждого пункта есть строка-значение).</summary>
    public bool Detailed => Screen != GameScreen.MainMenu;

    public int SelectedIndex => Screen switch
    {
        GameScreen.Settings => _settingsIndex,
        GameScreen.Cheats => _cheatsIndex,
        _ => _mainIndex
    };

    /// <summary>Строит список пунктов текущего экрана (читы читают состояние игрока).</summary>
    public IReadOnlyList<MenuEntry> BuildEntries(Player player)
    {
        _entries.Clear();

        switch (Screen)
        {
            case GameScreen.Settings:
                BuildSettings();
                break;
            case GameScreen.Cheats:
                BuildCheats(player);
                break;
            default:
                BuildMain();
                break;
        }

        return _entries;
    }

    /// <summary>Обрабатывает нажатие клавиши в меню.</summary>
    public MenuCommand HandleKey(ConsoleKey key, Player player)
    {
        _hoverIndex = -1;

        switch (Screen)
        {
            case GameScreen.Settings:
                return HandleSettings(key);
            case GameScreen.Cheats:
                return HandleCheats(key, player);
            default:
                return HandleMain(key);
        }
    }

    /// <summary>Обрабатывает событие мыши в меню (координаты терминала, с 1).</summary>
    public MenuCommand HandleMouse(in VtMouseEvent mouse, Player player)
    {
        MenuLayout? layout = Layout;
        if (layout is null || layout.Items.Count == 0)
            return MenuCommand.Nothing;

        // Координаты SGR начинаются с 1; кадр рисуется от домашней позиции курсора.
        int column = mouse.Column - 1;
        int row = mouse.Row - 1;
        if (column < 0 || row < 0)
            return MenuCommand.Nothing;

        // Ячейка терминала -> пиксель меню (вертикально в ячейке два пикселя кадра).
        int x = (int)((column + 0.5) / layout.Scale);
        int y = (int)((row * 2 + 1.0) / layout.Scale);

        MenuLayout.MenuItemGeom? hit = layout.HitTest(x, y);
        int hitIndex = hit?.EntryIndex ?? -1;

        bool hoverChanged = hitIndex != _hoverIndex;
        if (hoverChanged)
        {
            _hoverIndex = hitIndex;

            // Курсор уехал с тянутого ползунка — тянем дальше только если кнопка зажата.
            if (!mouse.Pressed && _dragIndex >= 0 && hitIndex != _dragIndex)
                _dragIndex = -1;
        }

        switch (mouse.Button)
        {
            case VtMouseButton.Left when mouse.Pressed:
                return HandleLeftPress(hit, x, player);
            case VtMouseButton.Left:
                _dragIndex = -1;
                return hoverChanged ? MenuCommand.Redraw : MenuCommand.Nothing;
            case VtMouseButton.Right when mouse.Pressed:
                return GoBack();
            case VtMouseButton.WheelUp:
                MoveSelection(-1);
                return MenuCommand.Redraw;
            case VtMouseButton.WheelDown:
                MoveSelection(1);
                return MenuCommand.Redraw;
        }

        // Движение мыши: продолжаем тянуть ползунок, если он был захвачен кликом.
        if (mouse.IsMotion && mouse.Pressed && _dragIndex >= 0)
        {
            ApplySliderFraction(_dragIndex, x, player);
            return MenuCommand.Redraw;
        }

        return hoverChanged ? MenuCommand.Redraw : MenuCommand.Nothing;
    }

    private MenuCommand HandleLeftPress(MenuLayout.MenuItemGeom? hit, int x, Player player)
    {
        if (hit is null)
            return MenuCommand.Nothing;

        SelectIndex(hit.EntryIndex);

        // Клик по ползунку: сразу ставим значение и начинаем перетаскивание.
        MenuEntry entry = BuildEntries(player)[hit.EntryIndex];
        if (entry.Kind == MenuEntryKind.Slider && x >= hit.TrackLeft && x <= hit.TrackRight)
        {
            _dragIndex = hit.EntryIndex;
            ApplySliderFraction(hit.EntryIndex, x, player);
            return MenuCommand.Redraw;
        }

        if (entry.Kind == MenuEntryKind.Option && x < hit.ArrowLeft)
        {
            AdjustEntry(entry, -1, player);
            return MenuCommand.Redraw;
        }

        if (entry.Kind == MenuEntryKind.Option && x > hit.ArrowRight)
        {
            AdjustEntry(entry, 1, player);
            return MenuCommand.Redraw;
        }

        // Переключатели и кнопки активируются кликом; клик по значению опции
        // листает варианты вперёд; в главном меню клик открывает пункт.
        if (entry.Kind == MenuEntryKind.Toggle || entry.Kind == MenuEntryKind.Action
                                                || entry.Kind == MenuEntryKind.Option || !Detailed)
        {
            return ActivateEntry(entry, player);
        }

        return MenuCommand.Redraw;
    }

    // ============================================================
    //   Клавиатура
    // ============================================================

    private MenuCommand HandleMain(ConsoleKey key)
    {
        switch (key)
        {
            case ConsoleKey.UpArrow or ConsoleKey.W:
                MoveSelection(-1);
                return MenuCommand.Redraw;
            case ConsoleKey.DownArrow or ConsoleKey.S:
                MoveSelection(1);
                return MenuCommand.Redraw;
            case ConsoleKey.Enter or ConsoleKey.Spacebar:
            {
                IReadOnlyList<MenuEntry> entries = CurrentEntries();
                if (SelectedIndex < entries.Count)
                    return ActivateEntry(entries[SelectedIndex], null);
                return MenuCommand.Nothing;
            }
            case ConsoleKey.Escape:
                return MenuCommand.Resume;
            default:
                return MenuCommand.Nothing;
        }
    }

    private MenuCommand HandleSettings(ConsoleKey key)
    {
        switch (key)
        {
            case ConsoleKey.UpArrow or ConsoleKey.W:
                MoveSelection(-1);
                return MenuCommand.Redraw;
            case ConsoleKey.DownArrow or ConsoleKey.S:
                MoveSelection(1);
                return MenuCommand.Redraw;
            case ConsoleKey.LeftArrow or ConsoleKey.A:
                return AdjustSelected(-1, null) ? MenuCommand.Redraw : MenuCommand.Nothing;
            case ConsoleKey.RightArrow or ConsoleKey.D:
                return AdjustSelected(1, null) ? MenuCommand.Redraw : MenuCommand.Nothing;
            case ConsoleKey.Enter or ConsoleKey.Spacebar:
            {
                IReadOnlyList<MenuEntry> entries = CurrentEntries();
                if (SelectedIndex < entries.Count)
                    return ActivateEntry(entries[SelectedIndex], null);
                return MenuCommand.Nothing;
            }
            case ConsoleKey.Escape:
                GameSettings.Current.Save();
                Screen = GameScreen.MainMenu;
                ResetScroll();
                return MenuCommand.Redraw;
            default:
                return MenuCommand.Nothing;
        }
    }

    private MenuCommand HandleCheats(ConsoleKey key, Player player)
    {
        switch (key)
        {
            case ConsoleKey.UpArrow or ConsoleKey.W:
                MoveSelection(-1);
                return MenuCommand.Redraw;
            case ConsoleKey.DownArrow or ConsoleKey.S:
                MoveSelection(1);
                return MenuCommand.Redraw;
            case ConsoleKey.LeftArrow or ConsoleKey.A:
                return AdjustSelected(-1, player) ? MenuCommand.Redraw : MenuCommand.Nothing;
            case ConsoleKey.RightArrow or ConsoleKey.D:
                return AdjustSelected(1, player) ? MenuCommand.Redraw : MenuCommand.Nothing;
            case ConsoleKey.Enter or ConsoleKey.Spacebar:
            {
                IReadOnlyList<MenuEntry> entries = BuildEntries(player);
                if (SelectedIndex < entries.Count)
                    return ActivateEntry(entries[SelectedIndex], player);
                return MenuCommand.Nothing;
            }
            case ConsoleKey.Escape:
                Screen = GameScreen.MainMenu;
                ResetScroll();
                return MenuCommand.Redraw;
            default:
                return MenuCommand.Nothing;
        }
    }

    // ============================================================
    //   Построение пунктов
    // ============================================================

    private IReadOnlyList<MenuEntry> CurrentEntries()
    {
        _entries.Clear();

        switch (Screen)
        {
            case GameScreen.Settings:
                BuildSettings();
                break;
            case GameScreen.Cheats:
                throw new InvalidOperationException("Для читов нужен игрок: используйте BuildEntries(player).");
            default:
                BuildMain();
                break;
        }

        return _entries;
    }

    private void BuildMain()
    {
        _entries.Add(new MenuEntry { Label = "Продолжить", Kind = MenuEntryKind.Item, Id = MenuEntryId.Resume });
        _entries.Add(new MenuEntry { Label = "Читы", Kind = MenuEntryKind.Item, Id = MenuEntryId.OpenCheats });
        _entries.Add(new MenuEntry { Label = "Настройки", Kind = MenuEntryKind.Item, Id = MenuEntryId.OpenSettings });
        _entries.Add(new MenuEntry { Label = "Выход", Kind = MenuEntryKind.Item, Id = MenuEntryId.Quit });
    }

    private void BuildSettings()
    {
        GameSettings settings = GameSettings.Current;

        _entries.Add(new MenuEntry
        {
            Label = "Качество графики",
            Kind = MenuEntryKind.Option,
            Id = MenuEntryId.Quality,
            Value = QualityNames[(int)settings.Quality]
        });

        _entries.Add(Slider("Чувствительность мыши", MenuEntryId.MouseSensitivity, settings.MouseSensitivity,
            SettingsRanges.MouseSensitivityMin, SettingsRanges.MouseSensitivityMax, "0.####"));
        _entries.Add(Slider("Скорость движения", MenuEntryId.MoveSpeed, settings.MoveSpeed,
            SettingsRanges.MoveSpeedMin, SettingsRanges.MoveSpeedMax, "0.##"));
        _entries.Add(Slider("Скорость поворота", MenuEntryId.RotationSpeed, settings.RotationSpeed,
            SettingsRanges.RotationSpeedMin, SettingsRanges.RotationSpeedMax, "0.##"));
        _entries.Add(Slider("Угол обзора", MenuEntryId.FieldOfView, settings.FieldOfViewDegrees,
            SettingsRanges.FieldOfViewMin, SettingsRanges.FieldOfViewMax, "0"));

        _entries.Add(Toggle("Захват мыши", MenuEntryId.CaptureMouse, settings.CaptureMouse));
        _entries.Add(Toggle("Покачивание оружия", MenuEntryId.WeaponBob, settings.WeaponBob));
        _entries.Add(Toggle("Счётчик FPS", MenuEntryId.ShowFps, settings.ShowFps));
        _entries.Add(Toggle("Авто-отдаление при старте", MenuEntryId.AutoZoom, settings.AutoZoom));
    }

    private void BuildCheats(Player player)
    {
        _entries.Add(new MenuEntry
        {
            Label = "Дать всё (оружие и патроны)",
            Kind = MenuEntryKind.Action,
            Id = MenuEntryId.GiveAll,
            Value = "ENTER"
        });

        foreach (WeaponKind kind in Enum.GetValues<WeaponKind>())
        {
            _entries.Add(new MenuEntry
            {
                Label = WeaponNames[(int)kind],
                Kind = MenuEntryKind.Toggle,
                Id = MenuEntryId.Weapon,
                Param = (int)kind,
                Value = player.Owns(kind) ? "ЕСТЬ" : "НЕТ",
                Fraction = player.Owns(kind) ? 1 : 0
            });
        }

        for (int i = 0; i < AmmoOrder.Length; i++)
        {
            AmmoKind ammo = AmmoOrder[i];
            int capacity = AmmoCatalog.Capacity(ammo);
            int amount = player.AmmoOf(ammo);

            _entries.Add(new MenuEntry
            {
                Label = AmmoNames[i],
                Kind = MenuEntryKind.Slider,
                Id = MenuEntryId.Ammo,
                Param = (int)ammo,
                Value = $"{amount}/{capacity}",
                Fraction = capacity == 0 ? 0 : amount / (double)capacity
            });
        }
    }

    private static MenuEntry Slider(string label, MenuEntryId id, double value, double min, double max,
        string format) => new()
    {
        Label = label,
        Kind = MenuEntryKind.Slider,
        Id = id,
        Value = value.ToString(format),
        Fraction = max > min ? Math.Clamp((value - min) / (max - min), 0, 1) : 0
    };

    private static MenuEntry Toggle(string label, MenuEntryId id, bool isOn) => new()
    {
        Label = label,
        Kind = MenuEntryKind.Toggle,
        Id = id,
        Value = isOn ? "ВКЛ" : "ВЫКЛ",
        Fraction = isOn ? 1 : 0
    };

    // ============================================================
    //   Изменение значений
    // ============================================================

    /// <summary>Меняет выбранное значение на шаг (стрелки влево/вправо).</summary>
    private bool AdjustSelected(int direction, Player? player)
    {
        IReadOnlyList<MenuEntry> entries = Screen == GameScreen.Cheats
            ? BuildEntries(player!)
            : CurrentEntries();

        if (SelectedIndex >= entries.Count)
            return false;

        AdjustEntry(entries[SelectedIndex], direction, player);
        return true;
    }

    private void AdjustEntry(MenuEntry entry, int direction, Player? player)
    {
        switch (entry.Id)
        {
            case MenuEntryId.Quality:
                GameSettings.Current.Quality = (GraphicsQuality)Wrap(
                    (int)GameSettings.Current.Quality + direction, QualityNames.Length);
                break;

            case MenuEntryId.MouseSensitivity:
                GameSettings.Current.MouseSensitivity = Step(GameSettings.Current.MouseSensitivity,
                    direction * 0.0005, SettingsRanges.MouseSensitivityMin, SettingsRanges.MouseSensitivityMax);
                break;

            case MenuEntryId.MoveSpeed:
                GameSettings.Current.MoveSpeed = Step(GameSettings.Current.MoveSpeed,
                    direction * 0.5, SettingsRanges.MoveSpeedMin, SettingsRanges.MoveSpeedMax);
                break;

            case MenuEntryId.RotationSpeed:
                GameSettings.Current.RotationSpeed = Step(GameSettings.Current.RotationSpeed,
                    direction * 0.25, SettingsRanges.RotationSpeedMin, SettingsRanges.RotationSpeedMax);
                break;

            case MenuEntryId.FieldOfView:
                GameSettings.Current.FieldOfViewDegrees = Step(GameSettings.Current.FieldOfViewDegrees,
                    direction * 5.0, SettingsRanges.FieldOfViewMin, SettingsRanges.FieldOfViewMax);
                break;

            case MenuEntryId.Weapon when player != null:
            {
                var kind = (WeaponKind)entry.Param;
                player.SetOwned(kind, direction > 0);
                break;
            }

            case MenuEntryId.Ammo when player != null:
            {
                var ammo = (AmmoKind)entry.Param;
                int capacity = AmmoCatalog.Capacity(ammo);
                int amount = Math.Clamp(player.AmmoOf(ammo) + direction * AmmoStep[(int)ammo], 0, capacity);
                player.SetAmmo(ammo, amount);
                break;
            }
        }
    }

    /// <summary>Активирует пункт: Enter или клик мышью.</summary>
    private MenuCommand ActivateEntry(MenuEntry entry, Player? player)
    {
        switch (entry.Id)
        {
            case MenuEntryId.Resume:
                return MenuCommand.Resume;

            case MenuEntryId.OpenCheats:
                Screen = GameScreen.Cheats;
                ResetScroll();
                return MenuCommand.Redraw;

            case MenuEntryId.OpenSettings:
                Screen = GameScreen.Settings;
                ResetScroll();
                return MenuCommand.Redraw;

            case MenuEntryId.Quit:
                return MenuCommand.Quit;

            case MenuEntryId.Quality:
                AdjustEntry(entry, 1, player);
                return MenuCommand.Redraw;

            case MenuEntryId.CaptureMouse:
                GameSettings.Current.CaptureMouse = !GameSettings.Current.CaptureMouse;
                return MenuCommand.Redraw;

            case MenuEntryId.WeaponBob:
                GameSettings.Current.WeaponBob = !GameSettings.Current.WeaponBob;
                return MenuCommand.Redraw;

            case MenuEntryId.ShowFps:
                GameSettings.Current.ShowFps = !GameSettings.Current.ShowFps;
                return MenuCommand.Redraw;

            case MenuEntryId.AutoZoom:
                GameSettings.Current.AutoZoom = !GameSettings.Current.AutoZoom;
                return MenuCommand.Redraw;

            case MenuEntryId.GiveAll when player != null:
                player.GiveAll();
                return MenuCommand.Redraw;

            case MenuEntryId.Weapon when player != null:
            {
                var kind = (WeaponKind)entry.Param;
                player.SetOwned(kind, !player.Owns(kind));
                return MenuCommand.Redraw;
            }

            case MenuEntryId.Ammo when player != null:
            {
                var ammo = (AmmoKind)entry.Param;
                player.SetAmmo(ammo, AmmoCatalog.Capacity(ammo));
                return MenuCommand.Redraw;
            }

            default:
                // Ползунки не «активируются» — их значение меняется стрелками и мышью.
                return MenuCommand.Nothing;
        }
    }

    /// <summary>Ставит значение ползунка по позиции мыши (0..1 вдоль дорожки).</summary>
    private void ApplySliderFraction(int entryIndex, int x, Player player)
    {
        MenuLayout? layout = Layout;
        MenuLayout.MenuItemGeom? geometry = layout?.Items.Find(item => item.EntryIndex == entryIndex);
        if (geometry is null || geometry.TrackRight <= geometry.TrackLeft)
            return;

        IReadOnlyList<MenuEntry> entries = BuildEntries(player);
        if (entryIndex >= entries.Count)
            return;

        MenuEntry entry = entries[entryIndex];
        if (entry.Kind != MenuEntryKind.Slider)
            return;

        double fraction = Math.Clamp(
            (x - geometry.TrackLeft) / (double)(geometry.TrackRight - geometry.TrackLeft), 0, 1);

        switch (entry.Id)
        {
            case MenuEntryId.MouseSensitivity:
                GameSettings.Current.MouseSensitivity = FromFraction(fraction,
                    SettingsRanges.MouseSensitivityMin, SettingsRanges.MouseSensitivityMax);
                break;

            case MenuEntryId.MoveSpeed:
                GameSettings.Current.MoveSpeed = FromFraction(fraction,
                    SettingsRanges.MoveSpeedMin, SettingsRanges.MoveSpeedMax);
                break;

            case MenuEntryId.RotationSpeed:
                GameSettings.Current.RotationSpeed = FromFraction(fraction,
                    SettingsRanges.RotationSpeedMin, SettingsRanges.RotationSpeedMax);
                break;

            case MenuEntryId.FieldOfView:
                GameSettings.Current.FieldOfViewDegrees = FromFraction(fraction,
                    SettingsRanges.FieldOfViewMin, SettingsRanges.FieldOfViewMax);
                break;

            case MenuEntryId.Ammo:
            {
                var ammo = (AmmoKind)entry.Param;
                int capacity = AmmoCatalog.Capacity(ammo);
                player.SetAmmo(ammo, (int)Math.Round(fraction * capacity));
                break;
            }
        }
    }

    private static double FromFraction(double fraction, double min, double max) =>
        min + Math.Clamp(fraction, 0, 1) * (max - min);

    // ============================================================
    //   Навигация
    // ============================================================

    private void SelectIndex(int index)
    {
        switch (Screen)
        {
            case GameScreen.Settings:
                _settingsIndex = index;
                break;
            case GameScreen.Cheats:
                _cheatsIndex = index;
                break;
            default:
                _mainIndex = index;
                break;
        }

        EnsureVisible();
    }

    private void MoveSelection(int delta)
    {
        int count = Screen switch
        {
            GameScreen.Settings => SettingsItemCount,
            GameScreen.Cheats => CheatsItemCount,
            _ => MainItemCount
        };

        int current = SelectedIndex;
        SelectIndex(Wrap(current + delta, count));
    }

    /// <summary>Сдвигает окно прокрутки так, чтобы выбранный пункт был виден.</summary>
    private void EnsureVisible()
    {
        int selected = SelectedIndex;

        if (selected < _first)
            _first = selected;
        else if (selected >= _first + MaxVisible)
            _first = selected - MaxVisible + 1;

        _first = Math.Max(0, _first);
    }

    private void ResetScroll()
    {
        _first = 0;
        _hoverIndex = -1;
        _dragIndex = -1;
    }

    private MenuCommand GoBack()
    {
        switch (Screen)
        {
            case GameScreen.Settings:
                GameSettings.Current.Save();
                Screen = GameScreen.MainMenu;
                ResetScroll();
                return MenuCommand.Redraw;
            case GameScreen.Cheats:
                Screen = GameScreen.MainMenu;
                ResetScroll();
                return MenuCommand.Redraw;
            default:
                return MenuCommand.Resume;
        }
    }

    private static double Step(double value, double delta, double min, double max) =>
        Math.Clamp(value + delta, min, max);

    private static int Wrap(int value, int count) => ((value % count) + count) % count;
}

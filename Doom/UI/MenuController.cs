using Doom.Configuration;
using Doom.Gameplay;

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
///     Состояние меню и обработка навигации: главное меню, настройки и читы. Изменения
///     применяются к <see cref="GameSettings" /> и <see cref="Player" /> сразу, а в файл
///     настройки сбрасываются при выходе из экрана настроек.
/// </summary>
internal sealed class MenuController
{
    private const int MainItemCount = 4;
    private const int SettingsItemCount = 6;
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

    /// <summary>Текущий экран меню.</summary>
    public GameScreen Screen { get; private set; } = GameScreen.MainMenu;

    /// <summary>Сбрасывает меню в главный экран.</summary>
    public void Open()
    {
        Screen = GameScreen.MainMenu;
        _mainIndex = 0;
        _settingsIndex = 0;
        _cheatsIndex = 0;
    }

    public string Title => Screen switch
    {
        GameScreen.Settings => "НАСТРОЙКИ",
        GameScreen.Cheats => "ЧИТЫ",
        _ => "DOOM - ПАУЗА"
    };

    public string Hint => Screen switch
    {
        GameScreen.Settings => "Up/Down выбор   Left/Right изменить   Esc назад",
        GameScreen.Cheats => "Up/Down выбор   Left/Right изменить   Enter действие   Esc назад",
        _ => "Up/Down выбор   Enter открыть   Esc продолжить"
    };

    /// <summary>Подробный ли экран (в настройках и читах у каждого пункта есть строка-значение).</summary>
    public bool Detailed => Screen != GameScreen.MainMenu;

    public int SelectedIndex => Screen switch
    {
        GameScreen.Settings => _settingsIndex,
        GameScreen.Cheats => _cheatsIndex,
        _ => _mainIndex
    };

    /// <summary>Строит список пунктов текущего экрана (чины читают состояние игрока).</summary>
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

    /// <summary>Обрабатывает нажатие в меню.</summary>
    public MenuCommand HandleKey(ConsoleKey key, Player player)
    {
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

    private MenuCommand HandleMain(ConsoleKey key)
    {
        switch (key)
        {
            case ConsoleKey.UpArrow or ConsoleKey.W:
                _mainIndex = Wrap(_mainIndex - 1, MainItemCount);
                return MenuCommand.Redraw;
            case ConsoleKey.DownArrow or ConsoleKey.S:
                _mainIndex = Wrap(_mainIndex + 1, MainItemCount);
                return MenuCommand.Redraw;
            case ConsoleKey.Enter or ConsoleKey.Spacebar:
                switch (_mainIndex)
                {
                    case 0:
                        return MenuCommand.Resume;
                    case 1:
                        Screen = GameScreen.Cheats;
                        _cheatsIndex = 0;
                        return MenuCommand.Redraw;
                    case 2:
                        Screen = GameScreen.Settings;
                        _settingsIndex = 0;
                        return MenuCommand.Redraw;
                    default:
                        return MenuCommand.Quit;
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
                _settingsIndex = Wrap(_settingsIndex - 1, SettingsItemCount);
                return MenuCommand.Redraw;
            case ConsoleKey.DownArrow or ConsoleKey.S:
                _settingsIndex = Wrap(_settingsIndex + 1, SettingsItemCount);
                return MenuCommand.Redraw;
            case ConsoleKey.LeftArrow or ConsoleKey.A:
                return AdjustSetting(-1) ? MenuCommand.Redraw : MenuCommand.Nothing;
            case ConsoleKey.RightArrow or ConsoleKey.D:
                return AdjustSetting(1) ? MenuCommand.Redraw : MenuCommand.Nothing;
            case ConsoleKey.Enter or ConsoleKey.Spacebar:
                return ToggleSetting() ? MenuCommand.Redraw : MenuCommand.Nothing;
            case ConsoleKey.Escape:
                GameSettings.Current.Save();
                Screen = GameScreen.MainMenu;
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
                _cheatsIndex = Wrap(_cheatsIndex - 1, CheatsItemCount);
                return MenuCommand.Redraw;
            case ConsoleKey.DownArrow or ConsoleKey.S:
                _cheatsIndex = Wrap(_cheatsIndex + 1, CheatsItemCount);
                return MenuCommand.Redraw;
            case ConsoleKey.LeftArrow or ConsoleKey.A:
                return AdjustCheat(player, -1) ? MenuCommand.Redraw : MenuCommand.Nothing;
            case ConsoleKey.RightArrow or ConsoleKey.D:
                return AdjustCheat(player, 1) ? MenuCommand.Redraw : MenuCommand.Nothing;
            case ConsoleKey.Enter or ConsoleKey.Spacebar:
                return ActivateCheat(player) ? MenuCommand.Redraw : MenuCommand.Nothing;
            case ConsoleKey.Escape:
                Screen = GameScreen.MainMenu;
                return MenuCommand.Redraw;
            default:
                return MenuCommand.Nothing;
        }
    }

    private void BuildMain()
    {
        _entries.Add(new MenuEntry { Label = "Продолжить", Kind = MenuEntryKind.Item });
        _entries.Add(new MenuEntry { Label = "Читы", Kind = MenuEntryKind.Item });
        _entries.Add(new MenuEntry { Label = "Настройки", Kind = MenuEntryKind.Item });
        _entries.Add(new MenuEntry { Label = "Выход", Kind = MenuEntryKind.Item });
    }

    private void BuildSettings()
    {
        GameSettings settings = GameSettings.Current;

        _entries.Add(new MenuEntry
        {
            Label = "Качество графики",
            Kind = MenuEntryKind.Option,
            Value = QualityNames[(int)settings.Quality]
        });

        _entries.Add(Slider("Чувствительность мыши", settings.MouseSensitivity,
            SettingsRanges.MouseSensitivityMin, SettingsRanges.MouseSensitivityMax, "0.####"));
        _entries.Add(Slider("Скорость движения", settings.MoveSpeed,
            SettingsRanges.MoveSpeedMin, SettingsRanges.MoveSpeedMax, "0.##"));
        _entries.Add(Slider("Скорость поворота", settings.RotationSpeed,
            SettingsRanges.RotationSpeedMin, SettingsRanges.RotationSpeedMax, "0.##"));
        _entries.Add(Slider("Угол обзора", settings.FieldOfViewDegrees,
            SettingsRanges.FieldOfViewMin, SettingsRanges.FieldOfViewMax, "0"));

        _entries.Add(new MenuEntry
        {
            Label = "Захват мыши",
            Kind = MenuEntryKind.Toggle,
            Value = settings.CaptureMouse ? "ВКЛ" : "ВЫКЛ"
        });
    }

    private void BuildCheats(Player player)
    {
        _entries.Add(new MenuEntry
        {
            Label = "Дать всё (оружие и патроны)",
            Kind = MenuEntryKind.Action,
            Value = "ENTER"
        });

        foreach (WeaponKind kind in Enum.GetValues<WeaponKind>())
        {
            _entries.Add(new MenuEntry
            {
                Label = WeaponNames[(int)kind],
                Kind = MenuEntryKind.Toggle,
                Value = player.Owns(kind) ? "ЕСТЬ" : "НЕТ"
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
                Value = $"{amount}/{capacity}",
                Fraction = capacity == 0 ? 0 : amount / (double)capacity
            });
        }
    }

    private static MenuEntry Slider(string label, double value, double min, double max, string format) => new()
    {
        Label = label,
        Kind = MenuEntryKind.Slider,
        Value = value.ToString(format),
        Fraction = max > min ? Math.Clamp((value - min) / (max - min), 0, 1) : 0
    };

    private bool AdjustSetting(int direction)
    {
        GameSettings settings = GameSettings.Current;

        switch (_settingsIndex)
        {
            case 0:
                settings.Quality = (GraphicsQuality)Wrap((int)settings.Quality + direction, QualityNames.Length);
                return true;
            case 1:
                settings.MouseSensitivity = Step(settings.MouseSensitivity, direction * 0.0005,
                    SettingsRanges.MouseSensitivityMin, SettingsRanges.MouseSensitivityMax);
                return true;
            case 2:
                settings.MoveSpeed = Step(settings.MoveSpeed, direction * 0.5,
                    SettingsRanges.MoveSpeedMin, SettingsRanges.MoveSpeedMax);
                return true;
            case 3:
                settings.RotationSpeed = Step(settings.RotationSpeed, direction * 0.25,
                    SettingsRanges.RotationSpeedMin, SettingsRanges.RotationSpeedMax);
                return true;
            case 4:
                settings.FieldOfViewDegrees = Step(settings.FieldOfViewDegrees, direction * 5.0,
                    SettingsRanges.FieldOfViewMin, SettingsRanges.FieldOfViewMax);
                return true;
            case 5:
                settings.CaptureMouse = direction > 0;
                return true;
            default:
                return false;
        }
    }

    private bool ToggleSetting()
    {
        if (_settingsIndex != 5)
            return false;

        GameSettings.Current.CaptureMouse = !GameSettings.Current.CaptureMouse;
        return true;
    }

    private bool AdjustCheat(Player player, int direction)
    {
        if (_cheatsIndex == 0)
            return false;

        if (_cheatsIndex <= WeaponCatalog.Count)
        {
            var kind = (WeaponKind)(_cheatsIndex - 1);
            player.SetOwned(kind, direction > 0);
            return true;
        }

        int ammoIndex = _cheatsIndex - 1 - WeaponCatalog.Count;
        AmmoKind ammo = AmmoOrder[ammoIndex];
        int capacity = AmmoCatalog.Capacity(ammo);
        int updated = Math.Clamp(player.AmmoOf(ammo) + direction * AmmoStep[ammoIndex], 0, capacity);
        player.SetAmmo(ammo, updated);
        return true;
    }

    private bool ActivateCheat(Player player)
    {
        if (_cheatsIndex == 0)
        {
            player.GiveAll();
            return true;
        }

        if (_cheatsIndex <= WeaponCatalog.Count)
        {
            var kind = (WeaponKind)(_cheatsIndex - 1);
            player.SetOwned(kind, !player.Owns(kind));
            return true;
        }

        int ammoIndex = _cheatsIndex - 1 - WeaponCatalog.Count;
        player.SetAmmo(AmmoOrder[ammoIndex], AmmoCatalog.Capacity(AmmoOrder[ammoIndex]));
        return true;
    }

    private static double Step(double value, double delta, double min, double max) =>
        Math.Clamp(value + delta, min, max);

    private static int Wrap(int value, int count) => ((value % count) + count) % count;
}
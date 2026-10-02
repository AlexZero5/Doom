namespace Doom.UI;

/// <summary>Тип пункта меню: обычный выбор, ползунок, переключатель, список опций или действие.</summary>
internal enum MenuEntryKind
{
    /// <summary>Пункт главного меню (открывает экран или выполняет действие).</summary>
    Item,

    /// <summary>Числовое значение с ползунком.</summary>
    Slider,

    /// <summary>Выбор из нескольких вариантов стрелками.</summary>
    Option,

    /// <summary>Переключатель «вкл/выкл».</summary>
    Toggle,

    /// <summary>Действие (выполняется по Enter или клику).</summary>
    Action
}

/// <summary>Смысл пункта меню: по нему мышь и клавиатура применяют изменения.</summary>
internal enum MenuEntryId
{
    /// <summary>Продолжить игру.</summary>
    Resume,

    /// <summary>Открыть экран читов.</summary>
    OpenCheats,

    /// <summary>Открыть экран настроек.</summary>
    OpenSettings,

    /// <summary>Выйти из игры.</summary>
    Quit,

    /// <summary>Качество графики (список опций).</summary>
    Quality,

    /// <summary>Чувствительность мыши (ползунок).</summary>
    MouseSensitivity,

    /// <summary>Скорость движения (ползунок).</summary>
    MoveSpeed,

    /// <summary>Скорость поворота (ползунок).</summary>
    RotationSpeed,

    /// <summary>Угол обзора (ползунок).</summary>
    FieldOfView,

    /// <summary>Захват мыши (переключатель).</summary>
    CaptureMouse,

    /// <summary>Покачивание оружия (переключатель).</summary>
    WeaponBob,

    /// <summary>Счётчик FPS (переключатель).</summary>
    ShowFps,

    /// <summary>Авто-отдаление при старте (переключатель).</summary>
    AutoZoom,

    /// <summary>Кнопка «дать всё» на экране читов.</summary>
    GiveAll,

    /// <summary>Наличие оружия (переключатель, <see cref="MenuEntry.Param" /> = вид оружия).</summary>
    Weapon,

    /// <summary>Запас патронов (ползунок, <see cref="MenuEntry.Param" /> = тип патронов).</summary>
    Ammo
}

/// <summary>Готовый к отрисовке пункт меню: подпись, значение и заполнение ползунка.</summary>
internal sealed class MenuEntry
{
    public string Label { get; init; } = string.Empty;

    public MenuEntryKind Kind { get; init; }

    /// <summary>Смысл пункта: что происходит при изменении.</summary>
    public MenuEntryId Id { get; init; }

    /// <summary>Дополнительный параметр (вид оружия или тип патронов).</summary>
    public int Param { get; init; }

    /// <summary>Текст значения (для опций, переключателей и ползунков).</summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>Заполнение ползунка в диапазоне 0..1.</summary>
    public double Fraction { get; init; }
}

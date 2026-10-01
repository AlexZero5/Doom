namespace Doom.UI;

/// <summary>Тип пункта меню: обычный выбор, ползунок, переключатель, список опций или действие.</summary>
internal enum MenuEntryKind
{
    /// <summary>Пункт главного меню (открывает экран или действие).</summary>
    Item,

    /// <summary>Числовое значение с ползунком.</summary>
    Slider,

    /// <summary>Выбор из нескольких вариантов стрелками.</summary>
    Option,

    /// <summary>Переключатель «вкл/выкл».</summary>
    Toggle,

    /// <summary>Действие (выполняется по Enter).</summary>
    Action
}

/// <summary>Готовый к отрисовке пункт меню: подпись, значение и заполнение ползунка.</summary>
internal sealed class MenuEntry
{
    public string Label { get; init; } = string.Empty;

    public MenuEntryKind Kind { get; init; }

    /// <summary>Текст значения (для опций, переключателей и ползунков).</summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>Заполнение ползунка в диапазоне 0..1.</summary>
    public double Fraction { get; init; }
}
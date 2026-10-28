using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using static Shade.Look;

namespace Shade;

/// <summary>The theme of Field and the fields built on it (date, host:port, search, suggestion): the border shows hover,
/// focus, an override, a pending value, a wrong value and an attached open list, in that order of strength. State is painted
/// by pseudo-classes, so it follows a mode or accent switch with no code.</summary>
internal static class FieldTheme
{
    static Setter Set(AvaloniaProperty property, object? value) => new(property, value);
    static void Style(ControlTheme theme, string state, params Setter[] setters)
    {
        var style = new Style(x => x.Nesting().Class(state)); foreach (var setter in setters) style.Setters.Add(setter); theme.Children.Add(style);
    }

    internal static ControlTheme Create()
    {
        var theme = new ControlTheme(typeof(Field)) { Setters = { Set(Border.BorderBrushProperty, B(Look.Separator)), Set(Border.BackgroundProperty, B(Raised)) } };
        // later styles win: the order is the order of strength
        Style(theme, ":pointerover", Set(Border.BorderBrushProperty, B(Disabled)));
        Style(theme, ":overridden", Set(Border.BorderBrushProperty, B(Info)));
        Style(theme, ":focus-within", Set(Border.BorderBrushProperty, B(FocusBorder)));
        Style(theme, ":pending", Set(Border.BorderBrushProperty, B(Warn)));
        Style(theme, ":invalid", Set(Border.BorderBrushProperty, B(Danger)));
        Style(theme, ":open", Set(Border.BorderBrushProperty, B(Outline)));
        Style(theme, ":disabled", Set(Border.BorderBrushProperty, B(Look.Separator)), Set(Border.BackgroundProperty, B(Look.Background)));
        return theme;
    }
}

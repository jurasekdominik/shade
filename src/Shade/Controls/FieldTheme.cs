using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using static Shade.Look;
using static Shade.ThemeKit;

namespace Shade;

/// <summary>The theme of Field and the fields built on it (date, host:port, search, suggestion): the border shows hover,
/// focus, an override, a pending value, a wrong value and an attached open list, in that order of strength. State is painted
/// by pseudo-classes, so it follows a mode or accent switch with no code.</summary>
internal static class FieldTheme
{

    internal static ControlTheme Create()
    {
        var theme = new ControlTheme(typeof(Field))
        {
            Setters = { Set(Border.BorderBrushProperty, B(Look.Separator)), Set(Border.BackgroundProperty, B(Raised)), Set(Border.BorderThicknessProperty, new Thickness(1)),
            Set(Border.CornerRadiusProperty, new CornerRadius(Radius)), Set(Border.PaddingProperty, new Thickness(M(8), 0)) }
        };
        // later styles win: the order is the order of strength
        theme.State(":pointerover", Set(Border.BorderBrushProperty, B(Disabled)));
        theme.State(":overridden", Set(Border.BorderBrushProperty, B(Info)));
        theme.State(":focus-within", Set(Border.BorderBrushProperty, B(FocusBorder)));
        theme.State(":pending", Set(Border.BorderBrushProperty, B(Warn)));
        theme.State(":invalid", Set(Border.BorderBrushProperty, B(Danger)));
        // its list is open below it: square bottom, no bottom line (its pixel goes to the padding, so the text doesn't move)
        theme.State(":open", Set(Border.BorderBrushProperty, B(Outline)), Set(Border.CornerRadiusProperty, new CornerRadius(Radius, Radius, 0, 0)),
            Set(Border.BorderThicknessProperty, new Thickness(1, 1, 1, 0)), Set(Border.PaddingProperty, new Thickness(M(8), 0, M(8), 1)));
        theme.State(":disabled", Set(Border.BorderBrushProperty, B(Look.Separator)), Set(Border.BackgroundProperty, B(Look.Background)));
        return theme;
    }
}

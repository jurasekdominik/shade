using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Styling;
using static Shade.Look;

namespace Shade;

/// <summary>The theme of the sidebar's NavItem: hover, keyboard focus, the selected entry (and the parent of a
/// selected child) and disabled are painted by pseudo-classes, so they follow a mode or accent switch with no code.</summary>
internal static class NavTheme
{
    static Setter Set(AvaloniaProperty property, object? value) => new(property, value);
    static void Style(ControlTheme theme, System.Func<Selector?, Selector> selector, params Setter[] setters)
    {
        var style = new Style(selector); foreach (var setter in setters) style.Setters.Add(setter); theme.Children.Add(style);
    }
    static Selector Part<T>(Selector? s, string name) where T : StyledElement => s!.Template().OfType<T>().Name(name);

    internal static ControlTheme Item()
    {
        var theme = new ControlTheme(typeof(NavItem));
        // the label and the icon take one colour: Text2 at rest, Text when selected or the parent of the selected, Disabled
        void Ink(System.Func<Selector?, Selector> state, Tone tone)
        {
            Style(theme, x => Part<TextBlock>(state(x), "PART_Label"), Set(TextBlock.ForegroundProperty, B(tone)));
            Style(theme, x => Part<Path>(state(x), "PART_Glyph"), Set(Shape.StrokeProperty, B(tone)));
        }
        Style(theme, x => Part<Border>(x.Nesting(), "PART_Surface"), Set(Border.BackgroundProperty, B(Raised)));
        Style(theme, x => Part<Border>(x.Nesting(), "PART_Marker"), Set(Border.BackgroundProperty, B(Accent)), Set(Visual.IsVisibleProperty, false));
        Style(theme, x => Part<Border>(x.Nesting(), "PART_Focus"), Set(Border.BorderBrushProperty, B(Accent)), Set(Visual.IsVisibleProperty, false));
        Ink(x => x.Nesting(), Text2);
        // later styles win: hover, the parent of the selected, selected, disabled
        Style(theme, x => Part<Border>(x.Nesting().Class(":pointerover"), "PART_Surface"), Set(Border.BackgroundProperty, B(Fill)));
        Ink(x => x.Nesting().Class(":child-selected"), Look.Text);
        Style(theme, x => Part<Border>(x.Nesting().Class(":selected"), "PART_Surface"), Set(Border.BackgroundProperty, B(AccentTint)));
        Style(theme, x => Part<Border>(x.Nesting().Class(":selected"), "PART_Marker"), Set(Visual.IsVisibleProperty, true));
        Ink(x => x.Nesting().Class(":selected"), Look.Text);
        Ink(x => x.Nesting().Class(":disabled"), Disabled);
        Style(theme, x => Part<Border>(x.Nesting().Class(":focus-visible"), "PART_Focus"), Set(Visual.IsVisibleProperty, true));
        return theme;
    }
}

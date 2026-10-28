using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Styling;
using static Shade.Look;
using static Shade.ThemeKit;

namespace Shade;

/// <summary>The theme of the sidebar's NavItem: hover, keyboard focus, the selected entry (and the parent of a
/// selected child) and disabled are painted by pseudo-classes, so they follow a mode or accent switch with no code.</summary>
internal static class NavTheme
{

    internal static ControlTheme Item()
    {
        var theme = new ControlTheme(typeof(NavItem));
        // the label and the icon take one colour: Text2 at rest, Text when selected or the parent of the selected, Disabled
        void Ink(System.Func<Selector?, Selector> state, Tone tone)
        {
            theme.Style(x => state(x).Part<TextBlock>("PART_Label"), Set(TextBlock.ForegroundProperty, B(tone)));
            theme.Style(x => state(x).Part<Path>("PART_Glyph"), Set(Shape.StrokeProperty, B(tone)));
        }
        theme.Style(x => x.Nesting().Part<Border>("PART_Surface"), Set(Border.BackgroundProperty, B(Raised)));
        theme.Style(x => x.Nesting().Part<Border>("PART_Marker"), Set(Border.BackgroundProperty, B(Accent)), Set(Visual.IsVisibleProperty, false));
        theme.Style(x => x.Nesting().Part<Border>("PART_Focus"), Set(Border.BorderBrushProperty, B(Accent)), Set(Visual.IsVisibleProperty, false));
        Ink(x => x.Nesting(), Text2);
        // later styles win: hover, the parent of the selected, selected, disabled
        theme.Style(x => x.Nesting().Class(":pointerover").Part<Border>("PART_Surface"), Set(Border.BackgroundProperty, B(Fill)));
        Ink(x => x.Nesting().Class(":child-selected"), Look.Text);
        theme.Style(x => x.Nesting().Class(":selected").Part<Border>("PART_Surface"), Set(Border.BackgroundProperty, B(AccentTint)));
        theme.Style(x => x.Nesting().Class(":selected").Part<Border>("PART_Marker"), Set(Visual.IsVisibleProperty, true));
        Ink(x => x.Nesting().Class(":selected"), Look.Text);
        Ink(x => x.Nesting().Class(":disabled"), Disabled);
        theme.Style(x => x.Nesting().Class(":focus-visible").Part<Border>("PART_Focus"), Set(Visual.IsVisibleProperty, true));
        return theme;
    }
}

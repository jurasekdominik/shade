using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;

namespace Shade;

/// <summary>What every code-first theme (ControlTheme) is built from: a setter, a style on the theme's own pseudo-class or part.</summary>
internal static class ThemeKit
{
    internal static Setter Set(AvaloniaProperty property, object? value) => new(property, value);

    /// <summary>A style of the theme: the selector starts at x.Nesting(); later styles win over earlier ones.</summary>
    internal static void Style(this ControlTheme theme, Func<Selector?, Selector> selector, params Setter[] setters)
    {
        var style = new Style(selector); foreach (var setter in setters) style.Setters.Add(setter); theme.Children.Add(style);
    }

    /// <summary>A style for one pseudo-class of the control (":pointerover").</summary>
    internal static void State(this ControlTheme theme, string state, params Setter[] setters) => theme.Style(x => x.Nesting().Class(state), setters);

    /// <summary>The pointer is over the control: :pointerover, or the class "hover" that Menu.OwnPopup gives an anchor whose list a press just closed.</summary>
    internal static void Hover(this ControlTheme theme, params Setter[] setters) { theme.State(":pointerover", setters); theme.Style(x => x.Nesting().Class("hover"), setters); }

    /// <summary>The heading cells of a table or a designer (Border.head): Raised, Fill under the pointer when the heading is live
    /// (class "live"), the accent tint for the current column (class "current"), and a lifted tint when both.</summary>
    internal static void Headings(Avalonia.Styling.Styles styles)
    {
        void Add(Func<Selector?, Selector> selector, Tone tone) => styles.Add(new Style(selector) { Setters = { Set(Border.BackgroundProperty, Look.B(tone)) } });
        Add(x => x.OfType<Border>().Class("head"), Look.Raised);
        Add(x => x.OfType<Border>().Class("head").Class("live").Class(":pointerover"), Look.Fill);
        Add(x => x.OfType<Border>().Class("head").Class("current"), Look.AccentTint);
        Add(x => x.OfType<Border>().Class("head").Class("current").Class("live").Class(":pointerover"), Look.AccentTintHover);
    }

    /// <summary>A named part of the control's template.</summary>
    internal static Selector Part<T>(this Selector? s, string name) where T : StyledElement => s!.Template().OfType<T>().Name(name);

    /// <summary>A control that is a template part: named and registered in the template's scope.</summary>
    internal static T Named<T>(this T control, string name, INameScope names) where T : Control
    {
        control.Name = name; names.Register(name, control); return control;
    }
}

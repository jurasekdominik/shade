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

    /// <summary>A named part of the control's template.</summary>
    internal static Selector Part<T>(this Selector? s, string name) where T : StyledElement => s!.Template().OfType<T>().Name(name);

    /// <summary>A control that is a template part: named and registered in the template's scope.</summary>
    internal static T Named<T>(this T control, string name, INameScope names) where T : Control
    {
        control.Name = name; names.Register(name, control); return control;
    }
}

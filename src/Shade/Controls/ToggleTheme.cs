using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using static Shade.Look;

namespace Shade;

internal static class ToggleTheme
{
    internal static ControlTheme Drawn(Type type)
    {
        var theme = new ControlTheme(type);
        theme.Setters.Add(new Setter(TemplatedControl.MinHeightProperty, 0d));
        theme.Setters.Add(new Setter(TemplatedControl.MinWidthProperty, 0d));
        theme.Setters.Add(new Setter(TemplatedControl.TemplateProperty, type == typeof(StateToggle) ? new FuncControlTemplate<StateToggle>((toggle, _) => toggle.CreateTemplate()) : null));
        return theme;
    }
    internal static ControlTheme Chip()
    {
        var theme = new ControlTheme(typeof(FilterChip));
        theme.Setters.Add(new Setter(TemplatedControl.TemplateProperty, new FuncControlTemplate<FilterChip>((chip, _) => chip.CreateTemplate())));
        theme.Setters.Add(new Setter(Layoutable.HeightProperty, ControlHeight));
        theme.Setters.Add(new Setter(Layoutable.MinHeightProperty, 0d)); theme.Setters.Add(new Setter(Layoutable.MinWidthProperty, 0d));
        theme.Setters.Add(new Setter(TemplatedControl.PaddingProperty, new Thickness(M(10), 0)));
        theme.Setters.Add(new Setter(TemplatedControl.BackgroundProperty, B(Raised))); theme.Setters.Add(new Setter(TemplatedControl.BorderBrushProperty, B(Look.Separator)));
        theme.Setters.Add(new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(1))); theme.Setters.Add(new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(Radius)));
        theme.Setters.Add(new Setter(TemplatedControl.ForegroundProperty, B(Text2))); theme.Setters.Add(new Setter(TemplatedControl.FontFamilyProperty, UiFont));
        theme.Setters.Add(new Setter(TemplatedControl.FontSizeProperty, TextSize.Label)); theme.Setters.Add(new Setter(TemplatedControl.FontWeightProperty, FontWeight.Medium));
        State(":pointerover", new Setter(TemplatedControl.BackgroundProperty, B(Fill)), new Setter(TemplatedControl.BorderBrushProperty, B(Disabled)));
        State(":checked", new Setter(TemplatedControl.BackgroundProperty, B(AccentTint)), new Setter(TemplatedControl.BorderBrushProperty, B(AccentTintLine)), new Setter(TemplatedControl.ForegroundProperty, B(AccentText)));
        State(":disabled", new Setter(TemplatedControl.ForegroundProperty, B(Disabled)));
        State(":focus-visible", new Setter(TemplatedControl.BorderBrushProperty, B(FocusBorder)));
        void State(string state, params Setter[] setters) { var style = new Style(x => x.Nesting().Class(state)); foreach (var setter in setters) style.Setters.Add(setter); theme.Children.Add(style); }
        return theme;
    }
}

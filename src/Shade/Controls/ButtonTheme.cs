using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Input;
using Avalonia.Data;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Styling;
using static Shade.Look;

namespace Shade;

internal static class ButtonTheme
{
    static Setter Set(AvaloniaProperty property, object? value) => new(property, value);
    static void State(ControlTheme theme, Func<Selector?, Selector> selector, params Setter[] setters)
    {
        var style = new Style(selector); foreach (var setter in setters) style.Setters.Add(setter); theme.Children.Add(style);
    }
    internal static ControlTheme Create()
    {
        var theme = new ControlTheme(typeof(ActionButton));
        foreach (var setter in new[] {
            Set(TemplatedControl.TemplateProperty, new FuncControlTemplate<ActionButton>((button, names) => button.CreateTemplate(names))),
            Set(Layoutable.HeightProperty, ControlHeight), Set(Layoutable.MinHeightProperty, 0d), Set(Layoutable.MinWidthProperty, M(28)),
            Set(TemplatedControl.PaddingProperty, new Thickness(M(12), 0)), Set(TemplatedControl.BorderThicknessProperty, new Thickness(1)),
            Set(TemplatedControl.CornerRadiusProperty, new CornerRadius(Radius)), Set(TemplatedControl.BackgroundProperty, B(Fill)),
            Set(TemplatedControl.ForegroundProperty, B(Look.Text)), Set(TemplatedControl.BorderBrushProperty, B(Look.Separator)),
            Set(TemplatedControl.FontFamilyProperty, UiFont), Set(TemplatedControl.FontSizeProperty, TextSize.Body), Set(TemplatedControl.FontWeightProperty, FontWeight.Medium),
            Set(ContentControl.HorizontalContentAlignmentProperty, HorizontalAlignment.Center), Set(ContentControl.VerticalContentAlignmentProperty, VerticalAlignment.Center),
            Set(Layoutable.HorizontalAlignmentProperty, HorizontalAlignment.Stretch), Set(InputElement.CursorProperty, Build.Hand),
            Set(Visual.RenderTransformProperty, TransformOperations.Parse("scale(1)")) }) theme.Setters.Add(setter);
        State(theme, x => x.Nesting().Class(":labelled"), Set(Layoutable.MinWidthProperty, M(72)));
        State(theme, x => x.Nesting().Class(":icon-only"), Set(TemplatedControl.PaddingProperty, new Thickness(0)));
        State(theme, x => x.Nesting().Class(":pointerover"), Set(TemplatedControl.BackgroundProperty, B(ButtonHover)), Set(TemplatedControl.BorderBrushProperty, B(Outline)));
        State(theme, x => x.Nesting().Class(":primary"), Set(TemplatedControl.BackgroundProperty, B(Accent)), Set(TemplatedControl.ForegroundProperty, B(OnAccent)), Set(TemplatedControl.BorderBrushProperty, B(Accent)));
        State(theme, x => x.Nesting().Class(":primary").Class(":pointerover"), Set(TemplatedControl.BackgroundProperty, B(AccentHi)), Set(TemplatedControl.BorderBrushProperty, B(AccentHi)));
        State(theme, x => x.Nesting().Class(":danger"), Set(TemplatedControl.BackgroundProperty, B(DangerMuted)), Set(TemplatedControl.ForegroundProperty, B(DangerText)), Set(TemplatedControl.BorderBrushProperty, B(DangerMuted)));
        State(theme, x => x.Nesting().Class(":danger").Class(":pointerover"), Set(TemplatedControl.BackgroundProperty, B(DangerHover)), Set(TemplatedControl.ForegroundProperty, B(DangerTextHover)), Set(TemplatedControl.BorderBrushProperty, B(DangerHover)));
        State(theme, x => x.Nesting().Class(":ghost"), Set(TemplatedControl.BackgroundProperty, Brushes.Transparent), Set(TemplatedControl.ForegroundProperty, B(Text2)), Set(TemplatedControl.BorderBrushProperty, Brushes.Transparent));
        State(theme, x => x.Nesting().Class(":ghost").Class(":pointerover"), Set(TemplatedControl.BackgroundProperty, B(Fill)), Set(TemplatedControl.ForegroundProperty, B(Look.Text)));
        State(theme, x => x.Nesting().Class(":ghost").Class(":icon-only").Class(":pointerover"), Set(TemplatedControl.BackgroundProperty, Brushes.Transparent));
        State(theme, x => x.Nesting().Class(":dimmed"), Set(TemplatedControl.ForegroundProperty, B(Text3)));
        State(theme, x => x.Nesting().Class(":dimmed").Class(":pointerover"), Set(TemplatedControl.ForegroundProperty, B(Text2)));
        State(theme, x => x.Nesting().Class(":armed"), Set(TemplatedControl.BackgroundProperty, B(Armed)), Set(TemplatedControl.ForegroundProperty, B(Warn)), Set(TemplatedControl.BorderBrushProperty, B(Armed)));
        State(theme, x => x.Nesting().Class(":armed").Class(":pointerover"), Set(TemplatedControl.BackgroundProperty, B(ArmedHover)), Set(TemplatedControl.BorderBrushProperty, B(ArmedHover)));
        State(theme, x => x.Nesting().Class(":armed").Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"), Set(ContentPresenter.ContentProperty, new TemplateBinding(ActionButton.ConfirmTextProperty)), Set(ContentPresenter.ContentTemplateProperty, null), Set(Visual.IsVisibleProperty, true));
        State(theme, x => x.Nesting().Class(":disabled"), Set(TemplatedControl.BackgroundProperty, B(Raised)), Set(TemplatedControl.ForegroundProperty, B(Disabled)), Set(TemplatedControl.BorderBrushProperty, B(Look.Separator)));
        State(theme, x => x.Nesting().Class(":ghost").Class(":disabled"), Set(TemplatedControl.BackgroundProperty, Brushes.Transparent), Set(TemplatedControl.BorderBrushProperty, Brushes.Transparent));
        State(theme, x => x.Nesting().Class(":focus-visible"), Set(TemplatedControl.BorderBrushProperty, B(FocusBorder)));
        if (!ReducedMotion)
        {
            theme.Setters.Add(Set(Animatable.TransitionsProperty, new Transitions { new TransformOperationsTransition { Property = Visual.RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(90) } }));
            State(theme, x => x.Nesting().Class(":pressed"), Set(Visual.RenderTransformProperty, TransformOperations.Parse("scale(.97)")));
        }
        return theme;
    }
    internal static ControlTheme Variant(Type type, ControlTheme basis)
    {
        var icon = type == typeof(IconButton); var theme = new ControlTheme(type) { BasedOn = basis };
        theme.Setters.Add(Set(TemplatedControl.BackgroundProperty, Brushes.Transparent)); theme.Setters.Add(Set(TemplatedControl.BorderBrushProperty, Brushes.Transparent));
        theme.Setters.Add(Set(TemplatedControl.ForegroundProperty, B(icon ? Accent : AccentText))); theme.Setters.Add(Set(Layoutable.MinWidthProperty, 0d));
        theme.Setters.Add(Set(TemplatedControl.PaddingProperty, icon ? new Thickness(0) : new Thickness(M(8), 0, M(10), 0)));
        if (icon) { theme.Setters.Add(Set(Layoutable.WidthProperty, M(28))); theme.Setters.Add(Set(ActionButton.IconSizeProperty, M(18))); }
        State(theme, x => x.Nesting().Class(":pointerover"), Set(TemplatedControl.BackgroundProperty, icon ? Brushes.Transparent : B(Fill)), Set(TemplatedControl.ForegroundProperty, B(AccentHi)));
        State(theme, x => x.Nesting().Class(":disabled"), Set(TemplatedControl.BackgroundProperty, Brushes.Transparent), Set(TemplatedControl.ForegroundProperty, B(Disabled)));
        State(theme, x => x.Nesting().Class(":dimmed"), Set(TemplatedControl.ForegroundProperty, B(Disabled)));
        return theme;
    }
}

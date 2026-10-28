using System;
using System.Globalization;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Styling;
using static Shade.Look;

namespace Shade;

/// <summary>The theme of Avalonia's ToggleSwitch: a capsule thumb with a soft shadow on a pill that turns from grey to the accent.
/// The thumb slides with an ease-out and stretches toward its travel while pressed; both are template transitions, and state is
/// painted by pseudo-classes.</summary>
internal static class ToggleSwitchTheme
{
    static readonly FuncValueConverter<object?, bool> HasContent = new(value => value != null && value is not "");
    static Setter Set(AvaloniaProperty property, object? value) => new(property, value);
    static void Style(ControlTheme theme, Func<Selector?, Selector> selector, params Setter[] setters)
    {
        var style = new Style(selector); foreach (var setter in setters) style.Setters.Add(setter); theme.Children.Add(style);
    }
    static Selector Part<T>(Selector? s, string name) where T : Control => s!.Template().OfType<T>().Name(name);
    static TransformOperations Slide(double x) => TransformOperations.Parse("translateX(" + x.ToString(CultureInfo.InvariantCulture) + "px)");

    internal static ControlTheme Create()
    {
        double w = M(32), h = M(18), inset = Math.Max(2, Math.Round(h * 0.07)), d = h - 2 * inset;
        double thumb = Math.Min(w - 2 * inset, Math.Round(w * 0.625)), travel = w - 2 * inset - thumb, stretch = Math.Min(d * 0.28, travel);  // the pressed thumb stays inside the track
        var theme = new ControlTheme(typeof(ToggleSwitch));
        foreach (var setter in new[] {
            Set(TemplatedControl.TemplateProperty, new FuncControlTemplate<ToggleSwitch>((toggle, names) => CreateTemplate(toggle, names, w, h, inset, d, thumb))),
            Set(Layoutable.MinWidthProperty, 0d), Set(Layoutable.MinHeightProperty, 0d),
            Set(Layoutable.HorizontalAlignmentProperty, HorizontalAlignment.Left), Set(Layoutable.VerticalAlignmentProperty, VerticalAlignment.Center),
            Set(TemplatedControl.PaddingProperty, new Thickness(M(8), 0, 0, 0)), Set(Avalonia.Input.InputElement.CursorProperty, Build.Hand),
            Set(TemplatedControl.ForegroundProperty, B(Look.Text)), Set(TemplatedControl.FontFamilyProperty, UiFont), Set(TemplatedControl.FontSizeProperty, TextSize.Body) })
            theme.Setters.Add(setter);
        Style(theme, x => Part<Border>(x.Nesting(), "PART_Track"), Set(Border.BackgroundProperty, B(SwitchOff)));
        Style(theme, x => Part<Border>(x.Nesting().Class(":pointerover"), "PART_Track"), Set(Border.BackgroundProperty, B(SwitchOffHover)));
        Style(theme, x => Part<Border>(x.Nesting().Class(":checked"), "PART_Track"), Set(Border.BackgroundProperty, B(Accent)));
        Style(theme, x => Part<Border>(x.Nesting().Class(":checked").Class(":pointerover"), "PART_Track"), Set(Border.BackgroundProperty, B(AccentHi)));
        Style(theme, x => Part<Border>(x.Nesting(), "PART_Knob"), Set(Layoutable.WidthProperty, thumb), Set(Visual.RenderTransformProperty, Slide(0)));
        Style(theme, x => Part<Border>(x.Nesting().Class(":checked"), "PART_Knob"), Set(Visual.RenderTransformProperty, Slide(travel)));
        Style(theme, x => Part<Border>(x.Nesting().Class(":pressed"), "PART_Knob"), Set(Layoutable.WidthProperty, thumb + stretch));
        Style(theme, x => Part<Border>(x.Nesting().Class(":checked").Class(":pressed"), "PART_Knob"), Set(Visual.RenderTransformProperty, Slide(travel - stretch)));
        Style(theme, x => Part<Border>(x.Nesting().Class(":disabled"), "PART_Track"), Set(Visual.OpacityProperty, 0.4));
        Style(theme, x => Part<Border>(x.Nesting().Class(":disabled"), "PART_Knob"), Set(Visual.OpacityProperty, 0.6));
        Style(theme, x => x.Nesting().Class(":disabled"), Set(TemplatedControl.ForegroundProperty, B(Disabled)));
        Style(theme, x => Part<Border>(x.Nesting(), "PART_Focus"), Set(Visual.IsVisibleProperty, false));
        Style(theme, x => Part<Border>(x.Nesting().Class(":focus-visible"), "PART_Focus"), Set(Visual.IsVisibleProperty, true));
        return theme;
    }

    static Control CreateTemplate(ToggleSwitch owner, INameScope names, double w, double h, double inset, double d, double thumb)
    {
        Border Named(string name, Border border) { border.Name = name; names.Register(name, border); return border; }
        var track = Named("PART_Track", new Border { CornerRadius = new CornerRadius(h / 2) });
        // reduced motion is read when the switch is built: no transitions, the thumb and colour jump
        if (!ReducedMotion) track.Transitions = new Transitions { new BrushTransition { Property = Border.BackgroundProperty, Duration = TimeSpan.FromMilliseconds(240), Easing = new CubicEaseOut() } };
        var knob = Named("PART_Knob", new Border
        {
            Height = d,
            Margin = new Thickness(inset, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            CornerRadius = new CornerRadius(d / 2),
            Background = Brushes.White,
            IsHitTestVisible = false,
            BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 1.5, Blur = 4, Color = Color.FromArgb(90, 0, 0, 0) }),
            RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute)
        });
        if (!ReducedMotion) knob.Transitions = new Transitions
        {
            new TransformOperationsTransition { Property = Visual.RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(240), Easing = new CubicEaseOut() },
            new DoubleTransition { Property = Layoutable.WidthProperty, Duration = TimeSpan.FromMilliseconds(140), Easing = new QuadraticEaseOut() },
        };
        var focus = Named("PART_Focus", new Border { CornerRadius = new CornerRadius(Radius), BorderThickness = new Thickness(1), BorderBrush = B(FocusBorder), IsHitTestVisible = false });
        var presenter = new ContentPresenter { Name = "PART_ContentPresenter", VerticalAlignment = VerticalAlignment.Center };
        names.Register("PART_ContentPresenter", presenter);
        presenter.Bind(ContentPresenter.ContentProperty, new TemplateBinding(ContentControl.ContentProperty));
        presenter.Bind(ContentPresenter.ContentTemplateProperty, new TemplateBinding(ContentControl.ContentTemplateProperty));
        presenter.Bind(Layoutable.MarginProperty, new TemplateBinding(TemplatedControl.PaddingProperty));
        presenter.Bind(Visual.IsVisibleProperty, new Binding(nameof(ContentControl.Content)) { Source = owner, Converter = HasContent });
        // PART_MovingKnobs and PART_SwitchKnob are the parts Avalonia's ToggleSwitch looks for (it drags it); the thumb is not draggable here, a click toggles
        var switchKnob = new Panel { Name = "PART_SwitchKnob", Children = { knob } }; names.Register("PART_SwitchKnob", switchKnob);
        var moving = new Panel { Name = "PART_MovingKnobs", IsHitTestVisible = false, Children = { switchKnob } }; names.Register("PART_MovingKnobs", moving);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Children = { new Panel { Width = w, Height = h, VerticalAlignment = VerticalAlignment.Center, Children = { track, moving, focus } }, presenter } };
        return new Border { Background = Brushes.Transparent, Child = row };
    }

    /// <summary>A switch with no label, only drawn: the face inside a row that toggles itself (a TickGrid row).</summary>
    internal static ToggleSwitch Glyph() => new()
    {
        IsHitTestVisible = false,
        Focusable = false,
        VerticalAlignment = VerticalAlignment.Center,
        [Avalonia.Automation.AutomationProperties.AccessibilityViewProperty] = Avalonia.Automation.AccessibilityView.Raw
    };
}

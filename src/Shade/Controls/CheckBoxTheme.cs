using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using static Shade.Look;

namespace Shade;

/// <summary>The theme of Avalonia's CheckBox: a 16 px box (outline = off, accent fill with a check = on, with a dash = some)
/// and the label beside it. State is painted by pseudo-classes, so it follows a mode or accent switch with no code.</summary>
internal static class CheckBoxTheme
{
    static readonly FuncValueConverter<object?, bool> HasContent = new(value => value != null && value is not "");
    static Setter Set(AvaloniaProperty property, object? value) => new(property, value);
    static void Style(ControlTheme theme, System.Func<Selector?, Selector> selector, params Setter[] setters)
    {
        var style = new Style(selector); foreach (var setter in setters) style.Setters.Add(setter); theme.Children.Add(style);
    }
    static Selector Box(Selector? s) => s!.Template().OfType<Border>().Name("PART_Box");

    internal static ControlTheme Create()
    {
        var theme = new ControlTheme(typeof(CheckBox));
        foreach (var setter in new[] {
            Set(TemplatedControl.TemplateProperty, new FuncControlTemplate<CheckBox>((box, names) => CreateTemplate(box, names))),
            Set(Layoutable.MinWidthProperty, 0d), Set(Layoutable.MinHeightProperty, 0d),
            Set(Layoutable.HorizontalAlignmentProperty, HorizontalAlignment.Left), Set(Layoutable.VerticalAlignmentProperty, VerticalAlignment.Center),
            Set(TemplatedControl.PaddingProperty, new Thickness(M(8), 0, 0, 0)), Set(Avalonia.Input.InputElement.CursorProperty, Build.Hand),
            Set(TemplatedControl.ForegroundProperty, B(Look.Text)), Set(TemplatedControl.FontFamilyProperty, UiFont), Set(TemplatedControl.FontSizeProperty, TextSize.Body) })
            theme.Setters.Add(setter);
        Style(theme, x => Box(x.Nesting()), Set(Border.BackgroundProperty, B(Raised)), Set(Border.BorderBrushProperty, B(Disabled)));
        Style(theme, x => Box(x.Nesting().Class(":pointerover")), Set(Border.BorderBrushProperty, B(Text2)));
        foreach (var on in new[] { ":checked", ":indeterminate" })
        {
            string state = on;
            Style(theme, x => Box(x.Nesting().Class(state)), Set(Border.BackgroundProperty, B(Accent)), Set(Border.BorderBrushProperty, B(Accent)));
            Style(theme, x => Box(x.Nesting().Class(state).Class(":pointerover")), Set(Border.BackgroundProperty, B(AccentHi)), Set(Border.BorderBrushProperty, B(AccentHi)));
        }
        Style(theme, x => Box(x.Nesting().Class(":disabled")), Set(Visual.OpacityProperty, 0.4));
        Style(theme, x => x.Nesting().Class(":disabled"), Set(TemplatedControl.ForegroundProperty, B(Disabled)));
        foreach (var part in new[] { "PART_Check", "PART_Dash" }) { string name = part; Style(theme, x => x.Nesting().Template().OfType<Path>().Name(name), Set(Visual.IsVisibleProperty, false)); }
        Style(theme, x => x.Nesting().Template().OfType<Border>().Name("PART_Focus"), Set(Visual.IsVisibleProperty, false));
        Style(theme, x => x.Nesting().Class(":checked").Template().OfType<Path>().Name("PART_Check"), Set(Visual.IsVisibleProperty, true));
        Style(theme, x => x.Nesting().Class(":indeterminate").Template().OfType<Path>().Name("PART_Dash"), Set(Visual.IsVisibleProperty, true));
        Style(theme, x => x.Nesting().Class(":focus-visible").Template().OfType<Border>().Name("PART_Focus"), Set(Visual.IsVisibleProperty, true));
        return theme;
    }

    static Control CreateTemplate(CheckBox owner, INameScope names)
    {
        double size = M(16);
        var box = new Border { Width = size, Height = size, CornerRadius = new CornerRadius(Radius), BorderThickness = new Thickness(1), Name = "PART_Box" };
        names.Register("PART_Box", box);
        Path Mark(string name, string data) { var p = new Path { Data = Geometry.Parse(data), Stroke = B(OnAccent), StrokeThickness = 1.8, StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round, Name = name }; names.Register(name, p); return p; }
        // the check and the dash sit on a 16 x 16 grid, scaled with the box
        var marks = new Viewbox { Width = size, Height = size, IsHitTestVisible = false, Child = new Canvas { Width = 16, Height = 16, Children = { Mark("PART_Check", "M4.16 8.32 L6.88 10.88 L11.84 5.44"), Mark("PART_Dash", "M4.48 8 L11.52 8") } } };
        var focus = new Border { Width = size, Height = size, CornerRadius = new CornerRadius(Radius), BorderThickness = new Thickness(1), BorderBrush = B(FocusBorder), IsHitTestVisible = false, Name = "PART_Focus" };
        names.Register("PART_Focus", focus);
        var presenter = new ContentPresenter { Name = "PART_ContentPresenter", VerticalAlignment = VerticalAlignment.Center };
        names.Register("PART_ContentPresenter", presenter);
        presenter.Bind(ContentPresenter.ContentProperty, new TemplateBinding(ContentControl.ContentProperty));
        presenter.Bind(ContentPresenter.ContentTemplateProperty, new TemplateBinding(ContentControl.ContentTemplateProperty));
        presenter.Bind(Layoutable.MarginProperty, new TemplateBinding(TemplatedControl.PaddingProperty));
        presenter.Bind(Visual.IsVisibleProperty, new Binding(nameof(ContentControl.Content)) { Source = owner, Converter = HasContent });
        var row = new StackPanel { Orientation = Orientation.Horizontal, Children = { new Panel { Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center, Children = { box, marks, focus } }, presenter } };
        return new Border { Background = Brushes.Transparent, Child = row };
    }

    /// <summary>A box with no label, only drawn: the face inside a row that toggles itself (a TickGrid or MultiChoice row).</summary>
    internal static CheckBox Glyph(bool? isChecked) => new()
    {
        Width = M(16),
        Height = M(16),
        IsChecked = isChecked,
        IsHitTestVisible = false,
        Focusable = false,
        VerticalAlignment = VerticalAlignment.Center,
        [Avalonia.Automation.AutomationProperties.AccessibilityViewProperty] = Avalonia.Automation.AccessibilityView.Raw
    };
}

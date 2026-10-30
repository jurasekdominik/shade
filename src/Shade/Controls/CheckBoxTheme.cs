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
using static Shade.ThemeKit;

namespace Shade;

/// <summary>The theme of Avalonia's CheckBox: a 16 px box (outline = off, accent fill with a check = on, with a dash = some)
/// and the label beside it. State is painted by pseudo-classes, so it follows a mode or accent switch with no code.</summary>
internal static class CheckBoxTheme
{
    static readonly FuncValueConverter<object?, bool> HasContent = new(value => value != null && value is not "");
    static Selector Box(Selector? s) => s!.Template().OfType<Border>().Name("PART_Box");

    internal static ControlTheme Create()
    {
        var theme = new ControlTheme(typeof(CheckBox));
        foreach (var setter in new[] {
            Set(TemplatedControl.TemplateProperty, new FuncControlTemplate<CheckBox>((box, names) => CreateTemplate(box, names))),
            Set(Layoutable.MinWidthProperty, 0d), Set(Layoutable.MinHeightProperty, 0d),
            Set(Layoutable.HorizontalAlignmentProperty, HorizontalAlignment.Left), Set(Layoutable.VerticalAlignmentProperty, VerticalAlignment.Center),
            Set(TemplatedControl.PaddingProperty, new Thickness(M(8), 0, 0, 0)), Set(Avalonia.Input.InputElement.CursorProperty, Build.Hand),
            Set(TemplatedControl.ForegroundProperty, B(Look.Text)), Set(TemplatedControl.FontFamilyProperty, UiFont), Set(TemplatedControl.FontSizeProperty, TextSize.Body),
            Set(CheckBoxAppearance.IndicatorSizeProperty, M(16)), Set(CheckBoxAppearance.MarkBrushProperty, B(OnAccent)), Set(CheckBoxAppearance.FocusBorderBrushProperty, B(FocusBorder)),
            Set(TemplatedControl.BackgroundProperty, B(Raised)), Set(TemplatedControl.BorderBrushProperty, B(Disabled)),
            Set(TemplatedControl.BorderThicknessProperty, new Thickness(1)), Set(TemplatedControl.CornerRadiusProperty, new CornerRadius(Radius)) })
            theme.Setters.Add(setter);
        theme.State(":pointerover", Set(TemplatedControl.BorderBrushProperty, B(Text2)));
        foreach (var on in new[] { ":checked", ":indeterminate" })
        {
            string state = on;
            theme.State(state, Set(TemplatedControl.BackgroundProperty, B(Accent)), Set(TemplatedControl.BorderBrushProperty, B(Accent)));
            theme.Style(x => x.Nesting().Class(state).Class(":pointerover"), Set(TemplatedControl.BackgroundProperty, B(AccentHi)), Set(TemplatedControl.BorderBrushProperty, B(AccentHi)));
        }
        theme.Style(x => Box(x.Nesting().Class(":disabled")), Set(Visual.OpacityProperty, new TemplateBinding(CheckBoxAppearance.DisabledOpacityProperty)));
        theme.Style(x => x.Nesting().Class(":disabled"), Set(TemplatedControl.ForegroundProperty, B(Disabled)));
        foreach (var part in new[] { "PART_Check", "PART_Dash", "PART_Square", "PART_Cross" }) { string name = part; theme.Style(x => x.Nesting().Template().OfType<Path>().Name(name), Set(Visual.IsVisibleProperty, false)); }
        theme.Style(x => x.Nesting().Template().OfType<Border>().Name("PART_Focus"), Set(Visual.IsVisibleProperty, false));
        theme.Style(x => x.Nesting().Class(":checked").Template().OfType<Path>().Name("PART_Check"), Set(Visual.IsVisibleProperty, true));
        // Classes="square" or Classes="cross" swap the check mark for a filled square or an X
        theme.Style(x => x.Nesting().Class("square").Class(":checked").Template().OfType<Path>().Name("PART_Check"), Set(Visual.IsVisibleProperty, false));
        theme.Style(x => x.Nesting().Class("cross").Class(":checked").Template().OfType<Path>().Name("PART_Check"), Set(Visual.IsVisibleProperty, false));
        theme.Style(x => x.Nesting().Class("square").Class(":checked").Template().OfType<Path>().Name("PART_Square"), Set(Visual.IsVisibleProperty, true));
        theme.Style(x => x.Nesting().Class("cross").Class(":checked").Template().OfType<Path>().Name("PART_Cross"), Set(Visual.IsVisibleProperty, true));
        theme.Style(x => x.Nesting().Class(":indeterminate").Template().OfType<Path>().Name("PART_Dash"), Set(Visual.IsVisibleProperty, true));
        theme.Style(x => x.Nesting().Class(":focus-visible").Template().OfType<Border>().Name("PART_Focus"), Set(Visual.IsVisibleProperty, true));
        return theme;
    }

    static Control CreateTemplate(CheckBox owner, INameScope names)
    {
        void Bind(AvaloniaObject target, AvaloniaProperty property, AvaloniaProperty source) => target.Bind(property, new TemplateBinding(source));
        void Size(Control target) { Bind(target, Layoutable.WidthProperty, CheckBoxAppearance.IndicatorSizeProperty); Bind(target, Layoutable.HeightProperty, CheckBoxAppearance.IndicatorSizeProperty); }
        var box = new Border { Name = "PART_Box" };
        Size(box);
        Bind(box, Border.BorderBrushProperty, TemplatedControl.BorderBrushProperty);
        Bind(box, Border.BorderThicknessProperty, TemplatedControl.BorderThicknessProperty);
        Bind(box, Border.CornerRadiusProperty, TemplatedControl.CornerRadiusProperty);
        box.Bind(Border.BackgroundProperty, new MultiBinding
        {
            Converter = new FuncMultiValueConverter<object?, IBrush?>(v => { var values = System.Linq.Enumerable.ToArray(v); return values[0] is not false && values[2] is IBrush on ? on : values[1] as IBrush; }),
            Bindings = { new TemplateBinding(CheckBox.IsCheckedProperty), new TemplateBinding(TemplatedControl.BackgroundProperty), new TemplateBinding(CheckBoxAppearance.CheckedBackgroundProperty) }
        });
        names.Register("PART_Box", box);
        Path Mark(string name, string data) { var p = new Path { Data = Geometry.Parse(data), StrokeThickness = 1.8, StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round, Name = name }; Bind(p, Shape.StrokeProperty, CheckBoxAppearance.MarkBrushProperty); names.Register(name, p); return p; }
        Path Square() { var p = new Path { Data = Geometry.Parse("M4.8 4.8 H11.2 V11.2 H4.8 Z"), Name = "PART_Square" }; Bind(p, Shape.FillProperty, CheckBoxAppearance.MarkBrushProperty); names.Register("PART_Square", p); return p; }
        // the check and the dash sit on a 16 x 16 grid, scaled with the box
        var marks = new Viewbox { IsHitTestVisible = false, Child = new Canvas { Width = 16, Height = 16, [!Visual.RenderTransformProperty] = new TemplateBinding(CheckBoxAppearance.MarkScaleProperty) { Converter = new FuncValueConverter<double, ITransform?>(k => new ScaleTransform(k, k)) }, Children = { Mark("PART_Check", "M4.16 8.32 L6.88 10.88 L11.84 5.44"), Mark("PART_Dash", "M4.48 8 L11.52 8"), Mark("PART_Cross", "M4.8 4.8 L11.2 11.2 M11.2 4.8 L4.8 11.2"), Square() } } }; Size(marks);
        var focus = new Border { BorderThickness = new Thickness(1), IsHitTestVisible = false, Name = "PART_Focus" }; Size(focus);
        Bind(focus, Border.CornerRadiusProperty, TemplatedControl.CornerRadiusProperty); Bind(focus, Border.BorderBrushProperty, CheckBoxAppearance.FocusBorderBrushProperty);
        names.Register("PART_Focus", focus);
        var presenter = new ContentPresenter { Name = "PART_ContentPresenter", VerticalAlignment = VerticalAlignment.Center };
        names.Register("PART_ContentPresenter", presenter);
        presenter.Bind(ContentPresenter.ContentProperty, new TemplateBinding(ContentControl.ContentProperty));
        presenter.Bind(ContentPresenter.ContentTemplateProperty, new TemplateBinding(ContentControl.ContentTemplateProperty));
        presenter.Bind(Layoutable.MarginProperty, new TemplateBinding(TemplatedControl.PaddingProperty));
        presenter.Bind(Visual.IsVisibleProperty, new Binding(nameof(ContentControl.Content)) { Source = owner, Converter = HasContent });
        var indicator = new Panel { VerticalAlignment = VerticalAlignment.Center, Children = { box, marks, focus } }; Size(indicator);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Children = { indicator, presenter } };
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

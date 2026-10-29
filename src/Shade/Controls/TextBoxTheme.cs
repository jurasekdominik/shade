using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using static Shade.Look;
using static Shade.ThemeKit;

namespace Shade;

/// <summary>The theme of Avalonia's TextBox: a Shade text field (DESIGN.md text-field), the frame showing hover, focus and a
/// binding error. A Field's own box (Field.Plain) sets no frame or fill, so the field around it draws them. The template has
/// Fluent's parts, so typing, selection, scrolling, IME and validation behave as before.</summary>
internal static class TextBoxTheme
{
    static Selector Frame(Selector? s) => s!.Template().OfType<Border>().Name("PART_BorderElement");

    internal static ControlTheme Create()
    {
        var theme = new ControlTheme(typeof(TextBox));
        foreach (var setter in new[] {
            Set(TemplatedControl.TemplateProperty, new FuncControlTemplate<TextBox>((box, names) => CreateTemplate(box, names))),
            Set(TemplatedControl.ForegroundProperty, B(Look.Text)), Set(TemplatedControl.BackgroundProperty, B(Raised)),
            Set(TemplatedControl.BorderBrushProperty, B(Look.Separator)), Set(TemplatedControl.BorderThicknessProperty, new Thickness(1)),
            Set(TemplatedControl.CornerRadiusProperty, new CornerRadius(4)), Set(TemplatedControl.PaddingProperty, new Thickness(M(8), 0)),
            Set(TextBox.CaretBrushProperty, B(Look.Text)), Set(TextBox.SelectionBrushProperty, B(Selection)), Set(TextBox.SelectionForegroundBrushProperty, B(Look.Text)),
            Set(TextBox.PlaceholderForegroundProperty, B(Text3)), Set(TemplatedControl.FontFamilyProperty, UiFont), Set(TemplatedControl.FontSizeProperty, TextSize.Body),
            Set(Layoutable.MinHeightProperty, ControlHeight), Set(Layoutable.MinWidthProperty, 0d), Set(TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Center),
            Set(InputElement.CursorProperty, new Cursor(StandardCursorType.Ibeam)), Set(ScrollViewer.IsScrollChainingEnabledProperty, true),
            Set(Control.FocusAdornerProperty, null), Set(Control.ContextFlyoutProperty, ContextMenu()) })
            theme.Setters.Add(setter);
        // a multi-line box (Workbench.Lines) starts at the top and pads all round
        theme.Style(x => x.Nesting().PropertyEquals(TextBox.AcceptsReturnProperty, true), Set(TextBox.VerticalContentAlignmentProperty, VerticalAlignment.Top), Set(TemplatedControl.PaddingProperty, new Thickness(M(8))));
        // later styles win: hover, focus, a binding error, disabled
        theme.Style(x => Frame(x.Nesting().Class(":pointerover")), Set(Border.BorderBrushProperty, B(Disabled)));
        theme.Style(x => Frame(x.Nesting().Class(":focus")), Set(Border.BorderBrushProperty, B(FocusBorder)));
        theme.Style(x => Frame(x.Nesting().Class(":error")), Set(Border.BorderBrushProperty, B(Danger)));
        theme.Style(x => Frame(x.Nesting().Class(":disabled")), Set(Border.BorderBrushProperty, B(Look.Separator)), Set(Border.BackgroundProperty, B(Look.Background)));
        theme.Style(x => x.Nesting().Class(":disabled"), Set(TemplatedControl.ForegroundProperty, B(Disabled)));
        // a field's own box: the field draws frame and fill, in any state
        theme.Style(x => Frame(x.Nesting().Class("plain")), Set(Border.BackgroundProperty, Brushes.Transparent), Set(Border.BorderThicknessProperty, new Thickness(0)));
        theme.Style(x => x.Nesting().Template().OfType<TextBlock>().Name("PART_FloatingPlaceholder"), Set(Visual.IsVisibleProperty, false));
        theme.Style(x => x.Nesting().PropertyEquals(TextBox.UseFloatingPlaceholderProperty, true).Not(y => y.Class(":empty")).Template().OfType<TextBlock>().Name("PART_FloatingPlaceholder"), Set(Visual.IsVisibleProperty, true));
        return theme;
    }

    /// <summary>Cut, Copy and Paste, shared by every box: the target is the box it opens on, the labels are read at opening.</summary>
    static MenuFlyout ContextMenu()
    {
        var cut = new MenuItem { InputGesture = TextBox.CutGesture }; var copy = new MenuItem { InputGesture = TextBox.CopyGesture }; var paste = new MenuItem { InputGesture = TextBox.PasteGesture };
        var flyout = new MenuFlyout { Items = { cut, copy, paste } };
        TextBox? Box() => flyout.Target as TextBox;
        flyout.Opening += (_, _) =>
        {
            cut.Header = T("Cut"); copy.Header = T("Copy"); paste.Header = T("Paste");
            if (Box() is { } box) { cut.IsEnabled = box.CanCut; copy.IsEnabled = box.CanCopy; paste.IsEnabled = box.CanPaste; }
        };
        cut.Click += (_, _) => Box()?.Cut(); copy.Click += (_, _) => Box()?.Copy(); paste.Click += (_, _) => Box()?.Paste();
        return flyout;
    }

    internal static Control CreateTemplate(TextBox owner, INameScope names, TextPresenter? presenter = null)
    {
        T Named<T>(T control, string name) where T : Control { control.Name = name; names.Register(name, control); return control; }
        static void Bind(Control control, AvaloniaProperty target, AvaloniaProperty source) => control.Bind(target, new TemplateBinding(source));

        var frame = Named(new Border(), "PART_BorderElement");
        foreach (var (target, source) in new (AvaloniaProperty, AvaloniaProperty)[] { (Border.BackgroundProperty, TemplatedControl.BackgroundProperty), (Border.BorderBrushProperty, TemplatedControl.BorderBrushProperty),
            (Border.BorderThicknessProperty, TemplatedControl.BorderThicknessProperty), (Border.CornerRadiusProperty, TemplatedControl.CornerRadiusProperty),
            (Layoutable.MinWidthProperty, Layoutable.MinWidthProperty), (Layoutable.MinHeightProperty, Layoutable.MinHeightProperty) })
            Bind(frame, target, source);

        var text = Named(presenter ?? new TextPresenter(), "PART_TextPresenter");
        text.Bind(TextPresenter.TextProperty, new TemplateBinding(TextBox.TextProperty) { Mode = BindingMode.TwoWay });
        foreach (var (target, source) in new (AvaloniaProperty, AvaloniaProperty)[] { (TextPresenter.CaretBlinkIntervalProperty, TextBox.CaretBlinkIntervalProperty),
            (TextPresenter.CaretIndexProperty, TextBox.CaretIndexProperty), (TextPresenter.SelectionStartProperty, TextBox.SelectionStartProperty),
            (TextPresenter.SelectionEndProperty, TextBox.SelectionEndProperty), (TextPresenter.TextAlignmentProperty, TextBox.TextAlignmentProperty),
            (TextPresenter.TextWrappingProperty, TextBox.TextWrappingProperty), (TextPresenter.LineHeightProperty, TextBox.LineHeightProperty),
            (TextPresenter.LetterSpacingProperty, TextBox.LetterSpacingProperty), (TextPresenter.PasswordCharProperty, TextBox.PasswordCharProperty),
            (TextPresenter.RevealPasswordProperty, TextBox.RevealPasswordProperty), (TextPresenter.SelectionBrushProperty, TextBox.SelectionBrushProperty),
            (TextPresenter.SelectionForegroundBrushProperty, TextBox.SelectionForegroundBrushProperty), (TextPresenter.CaretBrushProperty, TextBox.CaretBrushProperty),
            (Layoutable.HorizontalAlignmentProperty, TextBox.HorizontalContentAlignmentProperty), (Layoutable.VerticalAlignmentProperty, TextBox.VerticalContentAlignmentProperty) })
            Bind(text, target, source);

        var placeholder = Named(new TextBlock(), "PART_Placeholder");
        foreach (var (target, source) in new (AvaloniaProperty, AvaloniaProperty)[] { (TextBlock.ForegroundProperty, TextBox.PlaceholderForegroundProperty),
            (TextBlock.TextProperty, TextBox.PlaceholderTextProperty), (TextBlock.TextAlignmentProperty, TextBox.TextAlignmentProperty),
            (TextBlock.TextWrappingProperty, TextBox.TextWrappingProperty), (Layoutable.HorizontalAlignmentProperty, TextBox.HorizontalContentAlignmentProperty),
            (Layoutable.VerticalAlignmentProperty, TextBox.VerticalContentAlignmentProperty) })
            Bind(placeholder, target, source);
        placeholder.Bind(Visual.IsVisibleProperty, new MultiBinding
        {
            Converter = BoolConverters.And,
            Bindings = { new Binding(nameof(TextPresenter.PreeditText)) { Source = text, Converter = StringConverters.IsNullOrEmpty }, new Binding(nameof(TextBox.Text)) { Source = owner, Converter = StringConverters.IsNullOrEmpty } }
        });

        var scroll = Named(new ScrollViewer { Content = new Panel { Children = { placeholder, text } } }, "PART_ScrollViewer");
        foreach (var property in new AvaloniaProperty[] { ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollViewer.VerticalScrollBarVisibilityProperty,
            ScrollViewer.IsScrollChainingEnabledProperty, ScrollViewer.AllowAutoHideProperty, ScrollViewer.BringIntoViewOnFocusChangeProperty })
            Bind(scroll, property, property);
        var ibeam = new Style(x => x.OfType<ScrollContentPresenter>().Name("PART_ContentPresenter")); ibeam.Setters.Add(Set(InputElement.CursorProperty, new Cursor(StandardCursorType.Ibeam))); scroll.Styles.Add(ibeam);

        var floating = Named(new TextBlock(), "PART_FloatingPlaceholder");
        Bind(floating, TextBlock.ForegroundProperty, TextBox.PlaceholderForegroundProperty); Bind(floating, TextBlock.TextProperty, TextBox.PlaceholderTextProperty);
        DockPanel.SetDock(floating, Dock.Top);
        var inner = Named(new DockPanel { Children = { floating, scroll } }, "PART_InnerDockPanel");
        Bind(inner, Layoutable.MarginProperty, TemplatedControl.PaddingProperty);
        Grid.SetColumn(inner, 1);

        ContentPresenter Side(AvaloniaProperty content, int column)
        {
            var side = new ContentPresenter(); Grid.SetColumn(side, column);
            Bind(side, ContentPresenter.ContentProperty, content); Bind(side, ContentPresenter.VerticalContentAlignmentProperty, TextBox.VerticalContentAlignmentProperty);
            return side;
        }
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Children = { Side(TextBox.InnerLeftContentProperty, 0), inner, Side(TextBox.InnerRightContentProperty, 2) } };
        var content = new Border { Child = row };
        if (owner is not CharacterGridField { InsetFrame: true }) Bind(content, Layoutable.MarginProperty, TemplatedControl.BorderThicknessProperty);
        return new DataValidationErrors { Content = new Panel { Children = { frame, content } } };
    }
}

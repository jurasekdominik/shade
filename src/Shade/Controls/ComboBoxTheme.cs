using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
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

/// <summary>The theme of Avalonia's ComboBox, and of ChoiceButton, which is one (DESIGN.md dropdown): a 28 px field whose frame
/// shows hover, keyboard focus and an open list; the list opens under it as one shape with it, a faint line between them, the
/// rows of ListTheme. State is painted by pseudo-classes (:pointerover, :focus-visible, :dropdownopen, :disabled, and
/// Shade's :overridden), so it follows a mode or accent switch with no code.</summary>
internal static class ComboBoxTheme
{
    internal static ControlTheme Create(Type type)
    {
        var theme = new ControlTheme(type);
        theme.Setters.Add(Set(TemplatedControl.TemplateProperty, new FuncControlTemplate<ComboBox>(Template)));
        Chrome(theme);
        theme.Setters.Add(Set(ComboBox.PlaceholderForegroundProperty, B(Text3))); theme.Setters.Add(Set(ComboBox.MaxDropDownHeightProperty, Menu.MaximumHeight + 2));
        theme.Setters.Add(Set(Control.FocusAdornerProperty, null));
        theme.Setters.Add(Set(ComboBox.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch)); theme.Setters.Add(Set(ComboBox.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        theme.State(":focus-visible", Set(TemplatedControl.BorderBrushProperty, B(FocusBorder)));  // after the open state: keyboard focus is the strongest
        return theme;
    }

    /// <summary>The field of a dropdown, whatever it is a dropdown of: size, colours, and the frame in each state.</summary>
    static void Chrome(ControlTheme theme)
    {
        theme.Setters.Add(Set(Layoutable.HeightProperty, ControlHeight)); theme.Setters.Add(Set(Layoutable.MinHeightProperty, 0d)); theme.Setters.Add(Set(Layoutable.MinWidthProperty, 0d));
        theme.Setters.Add(Set(TemplatedControl.PaddingProperty, Pad)); theme.Setters.Add(Set(TemplatedControl.BorderThicknessProperty, new Thickness(1))); theme.Setters.Add(Set(TemplatedControl.CornerRadiusProperty, new CornerRadius(Radius)));
        theme.Setters.Add(Set(TemplatedControl.BackgroundProperty, B(Raised))); theme.Setters.Add(Set(TemplatedControl.BorderBrushProperty, B(Look.Separator))); theme.Setters.Add(Set(TemplatedControl.ForegroundProperty, B(Look.Text)));
        theme.Setters.Add(Set(TemplatedControl.FontFamilyProperty, UiFont)); theme.Setters.Add(Set(TemplatedControl.FontSizeProperty, TextSize.Body)); theme.Setters.Add(Set(TemplatedControl.FontWeightProperty, FontWeight.Normal));
        theme.Setters.Add(Set(InputElement.CursorProperty, Build.Hand));
        // later styles win: an override, the pointer over it, its list open (the field's bottom edge is the list's, whose line takes the pixel)
        theme.State(":overridden", Set(TemplatedControl.BorderBrushProperty, B(Info)));
        theme.Hover(Set(TemplatedControl.BorderBrushProperty, B(Outline)));
        theme.State(":dropdownopen", Set(TemplatedControl.BorderBrushProperty, B(Outline)), Set(TemplatedControl.CornerRadiusProperty, new CornerRadius(Radius, Radius, 0, 0)),
            Set(TemplatedControl.BorderThicknessProperty, new Thickness(1, 1, 1, 0)), Set(TemplatedControl.PaddingProperty, new Thickness(Pad.Left, Pad.Top, Pad.Right, Pad.Bottom + 1)));
        theme.State(":disabled", Set(TemplatedControl.ForegroundProperty, B(Text3)));
        theme.Style(x => x.Nesting().Template().OfType<Path>().Name("PART_Chevron"), Set(Shape.StrokeProperty, B(Text3)));
        foreach (var state in new[] { ":pointerover", "hover", ":dropdownopen" }) theme.Style(x => x.Nesting().Class(state).Template().OfType<Path>().Name("PART_Chevron"), Set(Shape.StrokeProperty, B(Text2)));
    }

    /// <summary>MultiChoice: the same field, with the focus ring of an activated control (it is not a ComboBox) and the disabled text in Disabled.</summary>
    internal static ControlTheme Multi()
    {
        var theme = new ControlTheme(typeof(MultiChoice));
        theme.Setters.Add(Set(TemplatedControl.TemplateProperty, new FuncControlTemplate<MultiChoice>((_, names) =>
        {
            var chevron = Build.Icon24(Icons.ChevronDown, M(16), 2).Named("PART_Chevron", names); chevron.Margin = new Thickness(M(4), 0, 0, 0); DockPanel.SetDock(chevron, Dock.Right);
            var caption = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            caption.Bind(TextBlock.TextProperty, new TemplateBinding(MultiChoice.CaptionProperty));
            return Frame(new DockPanel { Children = { chevron, caption } });
        })));
        Chrome(theme);
        foreach (var state in new[] { ":pointerover", "hover", ":dropdownopen" }) theme.Style(x => x.Nesting().Class(state).Template().OfType<Path>().Name("PART_Chevron"), Set(Shape.StrokeProperty, B(Text3)));  // its arrow never brightens
        theme.State(":disabled", Set(TemplatedControl.ForegroundProperty, B(Disabled)));
        theme.Style(x => x.Nesting().Class(":disabled").Template().OfType<Path>().Name("PART_Chevron"), Set(Shape.StrokeProperty, B(Disabled)));
        return theme;
    }

    static Thickness Pad => new(M(8), 0, M(4), 0);

    /// <summary>A row of the list: ListTheme's, and the one the keyboard is on is lit too; the current one is medium weight.</summary>
    internal static ControlTheme Item()
    {
        var theme = ListTheme.Item(typeof(ComboBoxItem));
        theme.Style(x => x.Nesting().Class(":focus").Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"), Set(ContentPresenter.BackgroundProperty, B(Fill)));
        theme.State(":selected", Set(TemplatedControl.FontWeightProperty, FontWeight.Medium));
        return theme;
    }

    static Control Template(ComboBox combo, INameScope names)
    {
        var self = new RelativeSource(RelativeSourceMode.TemplatedParent);
        var chevron = Build.Icon24(Icons.ChevronDown, M(16), 2).Named("PART_Chevron", names); chevron.Margin += new Thickness(M(4), 0, 0, 0); DockPanel.SetDock(chevron, Dock.Right);
        var placeholder = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        placeholder.Bind(TextBlock.TextProperty, new TemplateBinding(ComboBox.PlaceholderTextProperty)); placeholder.Bind(TextBlock.ForegroundProperty, new TemplateBinding(ComboBox.PlaceholderForegroundProperty));
        placeholder.Bind(Visual.IsVisibleProperty, new MultiBinding { Converter = BoolConverters.And, Bindings = { new Binding(nameof(ComboBox.SelectionBoxItem)) { RelativeSource = self, Converter = ObjectConverters.IsNull }, new Binding("!" + nameof(ComboBox.IsEditable)) { RelativeSource = self } } });
        var typed = Field.Plain().Named("PART_EditableTextBox", names);  // IsEditable: the field is a text box, as in Avalonia's own template
        typed.Bind(TextBox.TextProperty, new TemplateBinding(ComboBox.TextProperty) { Mode = BindingMode.TwoWay }); typed.Bind(TextBox.PlaceholderTextProperty, new TemplateBinding(ComboBox.PlaceholderTextProperty));
        typed.Bind(Visual.IsVisibleProperty, new TemplateBinding(ComboBox.IsEditableProperty));
        var shown = new ContentPresenter { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch };
        shown.Bind(Visual.IsVisibleProperty, new Binding("!" + nameof(ComboBox.IsEditable)) { RelativeSource = self });
        shown.Bind(ContentPresenter.ContentProperty, new TemplateBinding(ComboBox.SelectionBoxItemProperty)); shown.Bind(ContentPresenter.ContentTemplateProperty, new TemplateBinding(ComboBox.SelectionBoxItemTemplateProperty));
        var frame = Frame(new DockPanel { Children = { chevron, new Panel { Children = { new WidestItem(combo), placeholder, shown, typed } } } });

        // the list: a faint line under the field, then the rows, at least the field's width and as wide as its widest row
        var rows = new ItemsPresenter { Margin = new Thickness(0, 0, 0, 4) }.Named("PART_ItemsPresenter", names);
        rows.Bind(ItemsPresenter.ItemsPanelProperty, new TemplateBinding(ItemsControl.ItemsPanelProperty));
        var line = new Border { Height = 1, Background = B(Look.Separator), Margin = new Thickness(0, 0, 0, 4) }; DockPanel.SetDock(line, Dock.Top);
        var card = new Border { Background = B(Raised), BorderBrush = B(Outline), BorderThickness = new Thickness(1, 0, 1, 1), CornerRadius = new CornerRadius(0, 0, Radius, Radius), ClipToBounds = true, Child = new DockPanel { Children = { line, MenuTheme.Scrolling(rows) } } };
        card.Bind(Layoutable.MaxHeightProperty, new TemplateBinding(ComboBox.MaxDropDownHeightProperty)); card.Bind(Layoutable.MinWidthProperty, new Binding("Bounds.Width") { Source = combo });
        var popup = new Popup { PlacementTarget = combo, Placement = PlacementMode.BottomEdgeAlignedLeft, IsLightDismissEnabled = true, WindowManagerAddShadowHint = false, Child = card }.Named("PART_Popup", names);
        popup.Bind(Popup.IsOpenProperty, new Binding(nameof(ComboBox.IsDropDownOpen)) { Source = combo, Mode = BindingMode.TwoWay });
        popup.Opened += (_, _) => Menu.OwnPopup(popup, combo, () => combo.SetCurrentValue(ComboBox.IsDropDownOpenProperty, false));
        card.KeyDown += (_, e) =>  // Tab leaves the list toward the host, as from the field
        {
            if (e.Key != Key.Tab) return;
            e.Handled = true; combo.SetCurrentValue(ComboBox.IsDropDownOpenProperty, false);
            if (combo.IsEffectivelyEnabled && combo.IsEffectivelyVisible) { combo.Focus(NavigationMethod.Tab); TopLevel.GetTopLevel(combo)?.FocusManager?.TryMoveFocus(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? NavigationDirection.Previous : NavigationDirection.Next); }
        };
        return new Panel { Children = { frame, popup } };
    }
}

/// <summary>Reserves the widest item's width in a closed dropdown, so the field never cuts a choice or grows when another one
/// is picked (DESIGN.md dropdown). Measures text items, or the ItemTemplate; up to 200 items, so long lists stay cheap.</summary>
internal sealed class WidestItem(ComboBox combo) : Control
{
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) { base.OnAttachedToVisualTree(e); combo.Items.CollectionChanged += Changed; InvalidateMeasure(); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) { combo.Items.CollectionChanged -= Changed; base.OnDetachedFromVisualTree(e); }
    void Changed(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => InvalidateMeasure();
    protected override Size MeasureOverride(Size availableSize)
    {
        double widest = 0; var face = new Typeface(combo.FontFamily, combo.FontStyle, combo.FontWeight);
        int count = 0;
        foreach (var item in combo.Items)
        {
            if (++count > 200) break;
            var content = item is ContentControl container ? container.Content : item;
            if (content is Control) continue;  // a control can live in one place only
            if (combo.ItemTemplate?.Build(content) is { } built) { built.DataContext = content; built.Measure(Size.Infinity); widest = Math.Max(widest, built.DesiredSize.Width); }
            else widest = Math.Max(widest, new FormattedText(content?.ToString() ?? "", Culture, FlowDirection.LeftToRight, face, combo.FontSize, null).WidthIncludingTrailingWhitespace);
        }
        return new Size(Math.Ceiling(widest), 0);
    }
}

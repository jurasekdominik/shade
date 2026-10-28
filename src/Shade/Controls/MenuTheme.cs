using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Converters;
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

/// <summary>The theme of Avalonia's menus (DESIGN.md menu): a ContextMenu, a MenuFlyout and a MenuItem look like Shade's own
/// Menu: a rounded card of 28 px rows, the lit one on Fill, a shortcut hint in Text 3, a check or icon column that every row
/// shares, a line for a separator. The lit row is the item's :selected (pointer or keyboard), so it follows a mode or accent
/// switch with no code. Classes="danger" paints an item in danger text.</summary>
internal static class MenuTheme
{
    /// <summary>The scroll area of a menu or a list: it keeps its content clear of the bar when it overflows.</summary>
    internal static ScrollViewer Scrolling(Control content)
    {
        var scroll = new ScrollViewer { Content = content, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        scroll.PropertyChanged += (_, e) => { if (e.Property == ScrollViewer.ScrollBarMaximumProperty) scroll.Padding = scroll.ScrollBarMaximum.Y > 0 ? new Thickness(0, 0, Build.Pad, 0) : default; };
        return scroll;
    }

    static ItemsPresenter Rows(INameScope names)
    {
        var rows = new ItemsPresenter { Margin = new Thickness(0, 4) }.Named("PART_ItemsPresenter", names);
        rows.Bind(ItemsPresenter.ItemsPanelProperty, new TemplateBinding(ItemsControl.ItemsPanelProperty));
        Grid.SetIsSharedSizeScope(rows, true);  // the icon column and the shortcut column are as wide as the widest row's
        return rows;
    }

    /// <summary>ContextMenu and MenuFlyoutPresenter: the card, its brushes from the control.</summary>
    internal static ControlTheme Card(System.Type type)
    {
        var theme = new ControlTheme(type);
        theme.Setters.Add(Set(TemplatedControl.TemplateProperty, new FuncControlTemplate<MenuBase>((_, names) =>
        {
            var card = new Border { Child = Scrolling(Rows(names)) };
            foreach (var property in new AvaloniaProperty[] { Border.BackgroundProperty, Border.BorderBrushProperty, Border.BorderThicknessProperty, Border.CornerRadiusProperty })
                card.Bind(property, new TemplateBinding(property));
            return card;
        })));
        theme.Setters.Add(Set(TemplatedControl.BackgroundProperty, B(Raised))); theme.Setters.Add(Set(TemplatedControl.BorderBrushProperty, B(Look.Separator)));
        theme.Setters.Add(Set(TemplatedControl.BorderThicknessProperty, new Thickness(1))); theme.Setters.Add(Set(TemplatedControl.CornerRadiusProperty, new CornerRadius(Radius)));
        theme.Setters.Add(Set(Layoutable.MinWidthProperty, 160d)); theme.Setters.Add(Set(Layoutable.MaxHeightProperty, Menu.MaximumHeight + 2));
        theme.Setters.Add(Set(Layoutable.HorizontalAlignmentProperty, HorizontalAlignment.Stretch)); theme.Setters.Add(Set(InputElement.FocusableProperty, true));
        return theme;
    }

    internal static ControlTheme Item()
    {
        var theme = new ControlTheme(typeof(MenuItem));
        theme.Setters.Add(Set(TemplatedControl.TemplateProperty, new FuncControlTemplate<MenuItem>(ItemTemplate)));
        theme.Setters.Add(Set(TemplatedControl.BackgroundProperty, Brushes.Transparent)); theme.Setters.Add(Set(TemplatedControl.ForegroundProperty, B(Look.Text)));
        theme.Setters.Add(Set(TemplatedControl.FontFamilyProperty, UiFont)); theme.Setters.Add(Set(TemplatedControl.FontSizeProperty, TextSize.Body)); theme.Setters.Add(Set(TemplatedControl.FontWeightProperty, FontWeight.Normal));
        theme.Setters.Add(Set(TemplatedControl.PaddingProperty, new Thickness(M(8), 0))); theme.Setters.Add(Set(Layoutable.MinHeightProperty, ControlHeight));
        theme.Setters.Add(Set(Layoutable.HorizontalAlignmentProperty, HorizontalAlignment.Stretch)); theme.Setters.Add(Set(InputElement.CursorProperty, Build.Hand));
        theme.Style(x => x.Nesting().Template().OfType<ContentPresenter>().Name("PART_HeaderPresenter"), Set(ContentPresenter.RecognizesAccessKeyProperty, true));
        theme.Style(x => x.Nesting().Template().OfType<Path>().Name("PART_ChevronPath"), Set(Shape.StrokeProperty, B(Text3)));
        theme.Style(x => x.Nesting().Template().OfType<TextBlock>().Name("PART_InputGestureText"), Set(TextBlock.ForegroundProperty, B(Text3)));
        theme.Style(x => x.Nesting().Template().OfType<Panel>().Name("PART_Lead"), Set(Visual.IsVisibleProperty, false));  // until the item has an icon or a check
        theme.Style(x => x.Nesting().Template().OfType<Path>().Name("PART_ToggleIcon"), Set(Visual.IsVisibleProperty, false));
        theme.Style(x => x.Nesting().Class(":icon").Template().OfType<Panel>().Name("PART_Lead"), Set(Visual.IsVisibleProperty, true));
        theme.Style(x => x.Nesting().Class(":checked").Template().OfType<Panel>().Name("PART_Lead"), Set(Visual.IsVisibleProperty, true));
        theme.Style(x => x.Nesting().Class(":checked").Template().OfType<Path>().Name("PART_ToggleIcon"), Set(Visual.IsVisibleProperty, true));
        theme.Style(x => x.Nesting().Class(":checked").Template().OfType<ContentPresenter>().Name("PART_IconPresenter"), Set(Visual.IsVisibleProperty, false));  // the check replaces the icon
        theme.Style(x => x.Nesting().Class(":empty").Template().OfType<Path>().Name("PART_ChevronPath"), Set(Visual.IsVisibleProperty, false));
        // later styles win: the lit row (pointer or keyboard), a sub menu open, an item in danger text, disabled
        theme.Style(x => x.Nesting().Class(":selected").Template().OfType<Border>().Name("PART_LayoutRoot"), Set(Border.BackgroundProperty, B(Fill)));
        theme.Style(x => x.Nesting().Class(":open").Template().OfType<Border>().Name("PART_LayoutRoot"), Set(Border.BackgroundProperty, B(Fill)));
        theme.Style(x => x.Nesting().Class("danger"), Set(TemplatedControl.ForegroundProperty, B(DangerText)));
        theme.State(":disabled", Set(TemplatedControl.ForegroundProperty, B(Text3)));
        theme.State(":separator", Set(Layoutable.MinHeightProperty, 0d), Set(TemplatedControl.TemplateProperty, new FuncControlTemplate<MenuItem>((_, _) => new Avalonia.Controls.Separator())));
        return theme;
    }

    static Control ItemTemplate(MenuItem item, INameScope names)
    {
        var foreground = new Binding(nameof(TemplatedControl.Foreground)) { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) };
        var check = Build.Icon24(Icons.Check, 16).Named("PART_ToggleIcon", names); check.Bind(Shape.StrokeProperty, foreground);
        var icon = new ContentPresenter { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center }.Named("PART_IconPresenter", names);
        icon.Bind(ContentPresenter.ContentProperty, new TemplateBinding(MenuItem.IconProperty));
        var lead = new Panel { Children = { icon, check }, Width = 16, Margin = new Thickness(0, 0, M(8), 0) }.Named("PART_Lead", names);

        var header = new ContentPresenter { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch }.Named("PART_HeaderPresenter", names);
        header.Bind(ContentPresenter.ContentProperty, new TemplateBinding(HeaderedSelectingItemsControl.HeaderProperty));
        header.Bind(ContentPresenter.ContentTemplateProperty, new TemplateBinding(HeaderedSelectingItemsControl.HeaderTemplateProperty));

        var gesture = new TextBlock { Margin = new Thickness(M(24), 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center }.Named("PART_InputGestureText", names);
        var source = new RelativeSource(RelativeSourceMode.TemplatedParent);
        gesture.Bind(TextBlock.TextProperty, new Binding(nameof(MenuItem.InputGesture)) { RelativeSource = source, Converter = new PlatformKeyGestureConverter() });
        gesture.Bind(Visual.IsVisibleProperty, new Binding(nameof(MenuItem.InputGesture)) { RelativeSource = source, Converter = ObjectConverters.IsNotNull });
        var chevron = Build.Icon24(Icons.ChevronRight, M(16), 2).Named("PART_ChevronPath", names); chevron.Margin = new Thickness(M(8), 0, 0, 0);

        var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto) { SharedSizeGroup = "lead" }, new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) { SharedSizeGroup = "gesture" }, new ColumnDefinition(GridLength.Auto) { SharedSizeGroup = "chevron" } } };
        foreach (var (part, column) in new (Control, int)[] { (lead, 0), (header, 1), (gesture, 2), (chevron, 3) }) { Grid.SetColumn(part, column); row.Children.Add(part); }
        var root = new Border { Child = row }.Named("PART_LayoutRoot", names);
        root.Bind(Border.PaddingProperty, new TemplateBinding(TemplatedControl.PaddingProperty)); root.Bind(Border.BackgroundProperty, new TemplateBinding(TemplatedControl.BackgroundProperty));
        root.Bind(Border.BorderBrushProperty, new TemplateBinding(TemplatedControl.BorderBrushProperty)); root.Bind(Border.BorderThicknessProperty, new TemplateBinding(TemplatedControl.BorderThicknessProperty));

        var submenu = new Border { Background = B(Raised), BorderBrush = B(Look.Separator), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(Radius), MinWidth = 160, Child = Scrolling(Rows(names)) };
        var popup = new Popup { Placement = PlacementMode.RightEdgeAlignedTop, HorizontalOffset = -4, IsLightDismissEnabled = false, WindowManagerAddShadowHint = false, Child = submenu }.Named("PART_Popup", names);
        popup.Bind(Popup.IsOpenProperty, new TemplateBinding(MenuItem.IsSubMenuOpenProperty) { Mode = BindingMode.TwoWay });
        return new Panel { Children = { root, popup } };
    }

    /// <summary>The line between groups: a separator control, and a menu item whose header is "-".</summary>
    internal static ControlTheme Line()
    {
        var theme = new ControlTheme(typeof(Avalonia.Controls.Separator));
        theme.Setters.Add(Set(TemplatedControl.TemplateProperty, new FuncControlTemplate<Avalonia.Controls.Separator>((_, _) =>
        {
            var line = new Border(); line.Bind(Border.BackgroundProperty, new TemplateBinding(TemplatedControl.BackgroundProperty)); return line;
        })));
        theme.Setters.Add(Set(TemplatedControl.BackgroundProperty, B(Look.Separator))); theme.Setters.Add(Set(Layoutable.HeightProperty, 1d)); theme.Setters.Add(Set(Layoutable.MarginProperty, new Thickness(0, 4)));
        theme.Setters.Add(Set(InputElement.FocusableProperty, false)); theme.Setters.Add(Set(InputElement.IsHitTestVisibleProperty, false));
        return theme;
    }
}

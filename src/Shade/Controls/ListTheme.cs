using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using static Shade.Look;
using static Shade.ThemeKit;

namespace Shade;

/// <summary>The theme of Avalonia's ListBox and ListBoxItem (DESIGN.md list): a list is its rows, with no frame of its own
/// (the page or card around it draws one); a row is 28 px, lit on Fill when the pointer is over it or it is selected, as the
/// results of a SearchChoice and the rows of a menu are. Keyboard focus shows the accent ring. Classes are not needed; an
/// item container theme (ListTheme.Rows) gives the accent-tinted rows of the table.</summary>
internal static class ListTheme
{
    static Selector Row(Selector? s) => s.Part<ContentPresenter>("PART_ContentPresenter");

    /// <summary>The ListBox: a scroll area with the rows in it.</summary>
    internal static ControlTheme List()
    {
        var theme = new ControlTheme(typeof(ListBox));
        theme.Setters.Add(Set(TemplatedControl.TemplateProperty, new FuncControlTemplate<ListBox>((_, names) =>
        {
            var rows = new ItemsPresenter().Named("PART_ItemsPresenter", names);
            rows.Bind(ItemsPresenter.ItemsPanelProperty, new Avalonia.Data.TemplateBinding(ItemsControl.ItemsPanelProperty));
            var scroll = new ScrollViewer { Content = rows }.Named("PART_ScrollViewer", names);
            foreach (var property in new AvaloniaProperty[] { ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollViewer.IsScrollChainingEnabledProperty, ScrollViewer.IsDeferredScrollingEnabledProperty })
                scroll.Bind(property, new Avalonia.Data.TemplateBinding(property));
            var frame = new Border { Child = scroll };
            foreach (var property in new AvaloniaProperty[] { Border.BackgroundProperty, Border.BorderBrushProperty, Border.BorderThicknessProperty, Border.CornerRadiusProperty, Border.PaddingProperty })
                frame.Bind(property, new Avalonia.Data.TemplateBinding(property));
            return frame;
        })));
        theme.Setters.Add(Set(TemplatedControl.BackgroundProperty, Brushes.Transparent)); theme.Setters.Add(Set(TemplatedControl.BorderThicknessProperty, new Thickness(0)));
        theme.Setters.Add(Set(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled)); theme.Setters.Add(Set(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto));
        theme.Setters.Add(Set(TemplatedControl.ForegroundProperty, B(Look.Text))); theme.Setters.Add(Set(TemplatedControl.FontFamilyProperty, UiFont)); theme.Setters.Add(Set(TemplatedControl.FontSizeProperty, TextSize.Body));
        return theme;
    }

    /// <summary>A row of a ListBox or ComboBox (type = ListBoxItem or ComboBoxItem): the lit one on Fill.</summary>
    internal static ControlTheme Item(Type type)
    {
        var theme = new ControlTheme(type);
        theme.Setters.Add(Set(TemplatedControl.TemplateProperty, new FuncControlTemplate<ContentControl>((_, names) =>
        {
            var row = new ContentPresenter().Named("PART_ContentPresenter", names);
            foreach (var (target, source) in new (AvaloniaProperty, AvaloniaProperty)[] { (ContentPresenter.BackgroundProperty, TemplatedControl.BackgroundProperty), (ContentPresenter.BorderBrushProperty, TemplatedControl.BorderBrushProperty),
                (ContentPresenter.BorderThicknessProperty, TemplatedControl.BorderThicknessProperty), (ContentPresenter.CornerRadiusProperty, TemplatedControl.CornerRadiusProperty),
                (ContentPresenter.ContentProperty, ContentControl.ContentProperty), (ContentPresenter.ContentTemplateProperty, ContentControl.ContentTemplateProperty), (ContentPresenter.PaddingProperty, TemplatedControl.PaddingProperty),
                (ContentPresenter.HorizontalContentAlignmentProperty, ContentControl.HorizontalContentAlignmentProperty), (ContentPresenter.VerticalContentAlignmentProperty, ContentControl.VerticalContentAlignmentProperty) })
                row.Bind(target, new Avalonia.Data.TemplateBinding(source));
            return row;
        })));
        theme.Setters.Add(Set(TemplatedControl.BackgroundProperty, Brushes.Transparent)); theme.Setters.Add(Set(TemplatedControl.ForegroundProperty, B(Look.Text)));
        theme.Setters.Add(Set(TemplatedControl.FontFamilyProperty, UiFont)); theme.Setters.Add(Set(TemplatedControl.FontSizeProperty, TextSize.Body)); theme.Setters.Add(Set(TemplatedControl.FontWeightProperty, FontWeight.Normal));
        theme.Setters.Add(Set(TemplatedControl.PaddingProperty, new Thickness(M(8), 0))); theme.Setters.Add(Set(Layoutable.MinHeightProperty, ControlHeight));
        theme.Setters.Add(Set(ContentControl.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch)); theme.Setters.Add(Set(ContentControl.VerticalContentAlignmentProperty, VerticalAlignment.Center));
        theme.Setters.Add(Set(InputElement.CursorProperty, Build.Hand)); theme.Setters.Add(Set(Control.FocusAdornerProperty, FocusRingTemplate));
        // later styles win: the pointer over it, the selected row, disabled
        foreach (var state in new[] { ":pointerover", ":selected" }) theme.Style(x => Row(x.Nesting().Class(state)), Set(ContentPresenter.BackgroundProperty, B(Fill)));
        theme.State(":disabled", Set(TemplatedControl.ForegroundProperty, B(Text3)));
        return theme;
    }

    /// <summary>The rows of a table-like list (Workbench): no padding or minimum height of their own, the selected row on the
    /// accent tint and the one under the pointer on the current-row tint. Set it as the ListBox's ItemContainerTheme.</summary>
    internal static ControlTheme Rows()
    {
        var theme = new ControlTheme(typeof(ListBoxItem)) { BasedOn = Item(typeof(ListBoxItem)) };
        theme.Setters.Add(Set(TemplatedControl.PaddingProperty, new Thickness(0))); theme.Setters.Add(Set(Layoutable.MinHeightProperty, 0d));
        theme.Style(x => Row(x.Nesting().Class(":pointerover")), Set(ContentPresenter.BackgroundProperty, B(CurrentRow)));
        theme.Style(x => Row(x.Nesting().Class(":selected")), Set(ContentPresenter.BackgroundProperty, B(AccentTint)));
        theme.Style(x => Row(x.Nesting().Class(":selected").Class(":pointerover")), Set(ContentPresenter.BackgroundProperty, B(AccentTintHover)));
        return theme;
    }
}

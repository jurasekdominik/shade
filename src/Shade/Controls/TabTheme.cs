using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using static Shade.Look;
using static Shade.ThemeKit;

namespace Shade;

/// <summary>The theme of Avalonia's TabControl and TabItem (DESIGN.md text-tabs): the tabs look like TextTabs, 12 px text in
/// Text 3, the selected one in Text with an accent line under its text. TextTabs stays the light strip with no content host; a
/// TabControl is the one that shows a page for the selected tab.</summary>
internal static class TabTheme
{
    internal static ControlTheme Tabs()
    {
        var theme = new ControlTheme(typeof(TabControl));
        theme.Setters.Add(Set(TemplatedControl.TemplateProperty, new FuncControlTemplate<TabControl>((_, names) =>
        {
            var strip = new ItemsPresenter().Named("PART_ItemsPresenter", names);
            strip.Bind(ItemsPresenter.ItemsPanelProperty, new TemplateBinding(ItemsControl.ItemsPanelProperty));
            strip.Bind(DockPanel.DockProperty, new TemplateBinding(TabControl.TabStripPlacementProperty));
            ContentPresenter Page(string name)
            {
                var page = new ContentPresenter().Named(name, names);
                page.Bind(Layoutable.MarginProperty, new TemplateBinding(TemplatedControl.PaddingProperty));
                page.Bind(ContentPresenter.HorizontalContentAlignmentProperty, new TemplateBinding(ContentControl.HorizontalContentAlignmentProperty));
                page.Bind(ContentPresenter.VerticalContentAlignmentProperty, new TemplateBinding(ContentControl.VerticalContentAlignmentProperty));
                return page;
            }
            var hidden = Page("PART_SelectedContentHost2"); hidden.IsVisible = false;
            return new DockPanel { Children = { strip, new Panel { ClipToBounds = true, Children = { hidden, Page("PART_SelectedContentHost") } } } };
        })));
        theme.Setters.Add(Set(TemplatedControl.BackgroundProperty, Brushes.Transparent)); theme.Setters.Add(Set(TemplatedControl.PaddingProperty, new Thickness(0, M(12), 0, 0)));
        theme.Setters.Add(Set(ItemsControl.ItemsPanelProperty, new FuncTemplate<Panel?>(() => new StackPanel { Orientation = Orientation.Horizontal, Spacing = M(24) })));
        foreach (var side in new[] { Dock.Left, Dock.Right })  // a vertical strip
            theme.Style(x => x.Nesting().PropertyEquals(TabControl.TabStripPlacementProperty, side).Template().OfType<ItemsPresenter>().Name("PART_ItemsPresenter").Child().OfType<StackPanel>(),
                Set(StackPanel.OrientationProperty, Orientation.Vertical), Set(StackPanel.SpacingProperty, M(8)));
        return theme;
    }

    internal static ControlTheme Tab()
    {
        var theme = new ControlTheme(typeof(TabItem));
        theme.Setters.Add(Set(TemplatedControl.TemplateProperty, new FuncControlTemplate<TabItem>((_, names) =>
        {
            var text = new ContentPresenter { RecognizesAccessKey = true, Padding = new Thickness(0, 0, 0, M(6)) }.Named("PART_ContentPresenter", names);  // room for the line under the text
            text.Bind(ContentPresenter.ContentProperty, new TemplateBinding(HeaderedContentControl.HeaderProperty));
            text.Bind(ContentPresenter.ContentTemplateProperty, new TemplateBinding(HeaderedContentControl.HeaderTemplateProperty));
            text.Bind(ContentPresenter.HorizontalAlignmentProperty, new TemplateBinding(ContentControl.HorizontalContentAlignmentProperty));
            var line = new Border { Height = 2, VerticalAlignment = VerticalAlignment.Bottom, Background = B(Accent) }.Named("PART_SelectedPipe", names);
            return new Border { Child = new Panel { Children = { text, line } } }.Named("PART_LayoutRoot", names);
        })));
        theme.Setters.Add(Set(TemplatedControl.BackgroundProperty, Brushes.Transparent)); theme.Setters.Add(Set(TemplatedControl.ForegroundProperty, B(Text3)));
        theme.Setters.Add(Set(TemplatedControl.FontFamilyProperty, UiFont)); theme.Setters.Add(Set(TemplatedControl.FontSizeProperty, TextSize.Label)); theme.Setters.Add(Set(TemplatedControl.FontWeightProperty, FontWeight.Medium));
        theme.Setters.Add(Set(TemplatedControl.PaddingProperty, new Thickness(0))); theme.Setters.Add(Set(InputElement.CursorProperty, Build.Hand)); theme.Setters.Add(Set(Control.FocusAdornerProperty, FocusRingTemplate));
        theme.Style(x => x.Nesting().Template().OfType<Border>().Name("PART_SelectedPipe"), Set(Visual.IsVisibleProperty, false));
        theme.State(":selected", Set(TemplatedControl.ForegroundProperty, B(Look.Text)));
        theme.Style(x => x.Nesting().Class(":selected").Template().OfType<Border>().Name("PART_SelectedPipe"), Set(Visual.IsVisibleProperty, true));
        theme.State(":disabled", Set(TemplatedControl.ForegroundProperty, B(Disabled)));
        return theme;
    }
}

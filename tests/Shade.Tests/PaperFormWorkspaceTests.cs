using System;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
using static Shade.Look;

namespace Shade.Tests;

static partial class Program
{
    static void PaperFormWorkspace()
    {
        var child = new PaperStructure { FieldKey = "child", Label = "Child field", Bounds = new(21, 21, 19, 7) };
        var builder = new PaperFormBuilder(); builder.AddStructure(child);
        var group = new PaperStructure { Kind = "group", Caption = "Outer group", Bounds = new(10, 10, 60, 40) }; builder.AddStructure(group);
        var root = builder.Definition.Pages[0].Structures.Single();
        Ok(root.Id == group.Id && root.Children.Single().Id == child.Id && root.Children[0].Bounds == new PaperBounds(12, 12, 20, 8), "drawing a group around an existing field adopts it with local grid coordinates");
        var nested = new PaperStructure { Kind = "group", Caption = "Inner group", Bounds = new(18, 18, 30, 20) }; builder.AddStructure(nested);
        root = builder.Definition.Pages[0].Structures.Single();
        Ok(root.Children.Single().Id == nested.Id && root.Children[0].Children.Single().Id == child.Id, "nested groups adopt existing enclosed siblings without changing identities or keys");
        var window = new Window { Width = 1320, Height = 850, Content = builder }; window.Show(); Settle(); builder.Zoom = 1; Settle();
        var page = builder.GetVisualDescendants().OfType<Canvas>().Single(c => c.Width > 700);
        Point At(double x, double y) => page.TranslatePoint(new Point(x * 96 / 25.4, y * 96 / 25.4), window)!.Value;
        builder.SelectedId = null; Settle();
        window.MouseDown(At(24, 24), MouseButton.Left); window.MouseMove(At(32, 30)); window.MouseUp(At(32, 30), MouseButton.Left); Settle();
        root = builder.Definition.Pages[0].Structures.Single();
        Ok(root.Id == group.Id && root.Bounds.X == 18 && root.Bounds.Y == 16 && root.Children[0].Children[0].Bounds.X == 4, "dragging over a group's contents moves the whole group while preserving child offsets");
        var layerList = builder.GetVisualDescendants().OfType<ListBox>().Single(l => AutomationProperties.GetName(l) == "Page elements");
        var labels = layerList.GetVisualDescendants().OfType<TextBlock>().ToArray();
        var outer = labels.Single(t => t.Text == "Outer group"); var inner = labels.Single(t => t.Text == "Inner group"); var leaf = labels.Single(t => t.Text == "Child field");
        Ok(layerList.ItemCount == 4 && outer.Margin.Left < inner.Margin.Left && inner.Margin.Left < leaf.Margin.Left, "layers list contains every page/group/field with increasing nesting indentation");
        layerList.SelectedIndex = 3; Settle();
        Ok(builder.SelectedId == child.Id, "selecting a nested layer selects the child directly");
        var selected = builder.Definition.Pages[0].Structures[0].Children[0].Children[0];
        builder.ReplaceSelected(selected with { Bounds = selected.Bounds with { X = 100, Y = 60, Width = 25, Height = 9 } }); Settle();
        var ungrouped = builder.Definition.Pages[0].Structures.Single(s => s.Id == child.Id);
        Ok(ungrouped.Bounds.Width == 26 && ungrouped.Bounds.Height == 10 && builder.Definition.Pages[0].Structures[0].Children[0].Children.Count == 0, "moving a field outside its group reparents it on the page and quantizes its size");
        builder.Undo(); Settle();
        Ok(builder.Definition.Pages[0].Structures.Single().Children[0].Children.Single().Id == child.Id, "one undo restores the field's original hierarchy and geometry");
        builder.SelectedId = child.Id; Settle();
        var fonts = builder.GetVisualDescendants().OfType<SearchChoice>().Single();
        var widthField = builder.GetVisualDescendants().OfType<StackPanel>().Where(p => p.Children.Count >= 2 && p.Children[0] is TextBlock { Text: "Width (mm)" }).Select(p => p.Children[1]).OfType<Field>().Single();
        widthField.Box.Focus(); widthField.Box.SelectAll(); window.KeyTextInput("25"); fonts.Focus(); Settle();
        Ok(widthField.Text == "26", "leaving a valid dimension draft displays its snapped grid size");
        var installed = FontManager.Current.SystemFonts.Select(f => f.Name).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.CurrentCultureIgnoreCase).ToArray();
        Ok(fonts.Items.Cast<string>().SequenceEqual(installed), "font picker offers installed machine fonts without adding bundled choices");
        if (installed.Length > 0)
        {
            fonts.SelectedItem = installed[0]; fonts.RaiseEvent(new RoutedEventArgs(SearchChoice.SelectionCommittedEvent)); Settle();
            Ok(builder.Definition.Pages[0].Structures[0].Children[0].Children[0].FontFamily == installed[0], "choosing an installed font immediately updates the selected field");
        }
        var colour = builder.GetVisualDescendants().OfType<ActionButton>().Single(b => AutomationProperties.GetName(b) == "Text colour");
        Ok(colour.GetVisualDescendants().OfType<Border>().Any(b => b.Width == 20 && b.Background != null), "colour selector displays its current colour swatch");
        colour.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Settle();
        var picker = (window.FocusManager!.GetFocusedElement() as Control)!.GetLogicalAncestors().OfType<Popup>().Single(p => p.IsOpen);
        Ok(picker.Child!.GetVisualDescendants().OfType<ColorGrid>().Any(), "colour selector opens the shared colour palette");
        var hex = picker.Child!.GetVisualDescendants().OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == "Hex colour");
        hex.Focus(); hex.SelectAll(); window.KeyTextInput("#123456"); Settle();
        Ok(builder.Definition.Pages[0].Structures[0].Children[0].Children[0].Foreground == "#123456" && hex.IsFocused, "typed palette colour applies immediately and keeps the popup input focused");
        hex.SelectAll(); window.KeyTextInput("#zzzzzz"); Settle();
        Ok(DataValidationErrors.GetHasErrors(hex) && hex.Text == "#zzzzzz" && builder.Definition.Pages[0].Structures[0].Children[0].Children[0].Foreground == "#123456", "invalid colour drafts remain visible without replacing the last valid colour");
        picker.Child!.GetVisualDescendants().OfType<ActionButton>().Single(b => b.Content?.ToString() == "Default").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Settle();
        var mode = builder.GetVisualDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == "Mode");
        double longest = mode.Items.Cast<string>().Max(t => new FormattedText(t, Culture, FlowDirection.LeftToRight, new Typeface(UiFont), TextSize.Body, B(Text)).Width);
        Ok(mode.Bounds.Width >= longest + 32, "mode dropdown reserves room for its longest translated option and chevron");
        builder.AddPage(); builder.AddPage();
        var third = new PaperStructure { Label = "Third page field", Bounds = new(12, 12, 40, 8) }; builder.AddStructure(third); Settle(); builder.FitTwoPages(); Settle();
        var pages = builder.GetVisualDescendants().OfType<Canvas>().Where(c => c.Width > 700).ToArray();
        var scroll = builder.GetVisualDescendants().OfType<ScrollViewer>().Single(v => v.Content is Border b && b.Padding.Left == 24);
        Ok(pages.Length == 3 && scroll.Extent.Height > scroll.Viewport.Height * 1.4 && scroll.Extent.Height < scroll.Viewport.Height * 2, "three pages stay in one bounded canvas: fit two pages then scroll for the third");
        double floor = builder.Zoom; builder.Zoom = .01; Settle();
        Ok(builder.Zoom >= floor - .001 && builder.Zoom > .1, "zoom cannot shrink the whole document into a tiny distant sheet");
        builder.FitPage(); Settle();
        Ok(builder.Zoom > floor && scroll.Offset.Y > 0, "fit page zooms to the active page and brings it into view");
        layerList.SelectedIndex = 3; Settle();
        Ok(builder.PageIndex == 0 && builder.SelectedId == child.Id && scroll.Offset.Y < 1, "a layer on another page activates and reveals that page");
        builder.Mode = PaperFormMode.Fill; Settle();
        Ok(builder.GetVisualDescendants().OfType<Canvas>().Count(c => c.Width > 700) == 3 && !layerList.IsEffectivelyVisible, "fill mode keeps all pages visible and hides design layers");
        builder.PageIndex = 2; builder.FitPage(); Settle();
        var thirdInput = pages[2].GetVisualDescendants().OfType<TextBox>().Single();
        builder.PageIndex = 0; Settle(); scroll.ScrollToEnd(); Settle();
        var point = thirdInput.TranslatePoint(new Point(8, 8), window)!.Value;
        window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left); window.KeyTextInput("Third"); Settle();
        Ok(builder.PageIndex == 2 && builder.Values.Single(v => v.Key == builder.Definition.Pages[2].Structures[0].FieldKey).Text == "Third", $"native input on another visible page activates that page and edits its own value: index={builder.PageIndex}, text={builder.Values.Last().Text}, focus={window.FocusManager?.GetFocusedElement()?.GetType()}");
        window.Close();
    }
}

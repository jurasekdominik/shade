using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using static Shade.Look;

namespace Shade.Tests;

static partial class Program
{
    static void PaperFormAuthoring()
    {
        void Complete(Task task) { var clock = System.Diagnostics.Stopwatch.StartNew(); while (!task.IsCompleted && clock.ElapsedMilliseconds < 3000) Settle(); task.GetAwaiter().GetResult(); Settle(); }
        var check = new PaperStructure { Kind = "check", FieldKey = "check", Bounds = new(12, 12, 8, 8) };
        var text = new PaperStructure { FieldKey = "text", Bounds = new(40, 20, 30, 8) };
        var notes = new PaperStructure { Kind = "multiline", FieldKey = "notes", Bounds = new(12, 40, 50, 16) };
        var builder = new PaperFormBuilder { Definition = new() { Pages = new[] { new PaperPage { Structures = new[] { check, text, notes } } } } };
        var window = new Window { Width = 1600, Height = 1000, Content = builder }; window.Show(); Settle();
        var page = builder.GetVisualDescendants().OfType<Canvas>().Single(c => c.Width > 700);
        Point At(double x, double y) => page.TranslatePoint(new Point(x * 96 / 25.4, y * 96 / 25.4), window)!.Value;
        PaperStructure Item(string id) => builder.Definition.Pages[0].Structures.Single(s => s.Id == id);
        Field Property(string label) => builder.GetVisualDescendants().OfType<StackPanel>().Where(p => p.Children.Count >= 2 && p.Children[0] is TextBlock t && t.Text == label).Select(p => p.Children[1]).OfType<Field>().Single();
        builder.SelectedId = check.Id; builder.ReplaceSelected(Item(check.Id) with { Bounds = check.Bounds with { Width = 14 } }); Settle();
        Ok(Item(check.Id).Bounds.Width == 14 && Item(check.Id).Bounds.Height == 14, "checkbox width edits resize both dimensions together");
        builder.ReplaceSelected(Item(check.Id) with { Bounds = Item(check.Id).Bounds with { Height = 6 } }); Settle();
        Ok(Item(check.Id).Bounds.Width == 6 && Item(check.Id).Bounds.Height == 6, "checkbox height edits preserve square geometry");
        var corner = At(17.7, 17.7); window.MouseDown(corner, MouseButton.Left); window.MouseMove(corner + new Vector(8 * 96 / 25.4, 0)); window.MouseUp(corner + new Vector(8 * 96 / 25.4, 0), MouseButton.Left); Settle();
        Ok(Item(check.Id).Bounds.Width == 14 && Item(check.Id).Bounds.Height == 14, "one-axis checkbox drag resizes its width and height together");
        var width = Property("Width (mm)"); var arrows = width.GetVisualDescendants().OfType<IconButton>().ToArray();
        Ok(arrows.Length == 2 && arrows.All(a => a.Width >= 28 && a.Height >= 28) && Math.Abs(arrows[0].TranslatePoint(default, width)!.Value.Y - arrows[1].TranslatePoint(default, width)!.Value.Y) < 1, "numeric arrows use full-height adjacent targets");
        width.Box.Focus(); builder.NumberButtonsPlacement = PaperNumberButtonsPlacement.Left; builder.NumberButtonSize = 32; Settle();
        Ok(ReferenceEquals(width, Property("Width (mm)")) && width.Box.IsFocused && arrows[0].TranslatePoint(default, width)!.Value.X < width.Box.TranslatePoint(default, width)!.Value.X && arrows[0].Width == 32, "numeric button placement and size APIs preserve the live editor");
        builder.NumberButtonsPlacement = PaperNumberButtonsPlacement.Top; Settle();
        Ok(arrows[0].TranslatePoint(default, width)!.Value.Y < width.Box.TranslatePoint(default, width)!.Value.Y, "numeric arrows support top placement");
        builder.InspectorPlacement = PaperInspectorPlacement.SidebarLeft; builder.InspectorWidth = 260; Settle();
        Ok(Property("Width (mm)").TranslatePoint(default, window)!.Value.X < page.TranslatePoint(default, window)!.Value.X, "property panels support left placement and configurable width");
        builder.InspectorPlacement = PaperInspectorPlacement.RibbonBottom; builder.RibbonFieldWidth = 180; builder.PropertyRibbonHeight = 160; Settle();
        Ok(Property("Width (mm)").TranslatePoint(default, window)!.Value.Y > page.TranslatePoint(default, window)!.Value.Y, "property ribbons support bottom placement through API");
        builder.InspectorPlacement = PaperInspectorPlacement.Sidebar;
        builder.NumberButtonsPlacement = PaperNumberButtonsPlacement.Right; builder.NumberButtonSize = 28;
        builder.LayersPlacement = Dock.Right; builder.GridSettingsPlacement = Dock.Bottom; builder.ToolbarPlacement = Dock.Top; Settle();
        var layers = builder.GetVisualDescendants().OfType<ListBox>().Single(c => AutomationProperties.GetName(c) == "Page elements");
        Ok(layers.TranslatePoint(default, window)!.Value.X > page.TranslatePoint(default, window)!.Value.X, "apps can dock layers on the right and grid settings below the canvas");
        builder.LayersPlacement = Dock.Left; builder.GridSettingsPlacement = Dock.Top; Settle();
        window.MouseDown(At(14, 14), MouseButton.Right); window.MouseUp(At(14, 14), MouseButton.Right); Settle();
        var popup = (window.FocusManager!.GetFocusedElement() as Control)!.GetLogicalAncestors().OfType<Popup>().Single(p => p.IsOpen);
        var menu = popup.Child!.GetVisualDescendants().OfType<Button>().Select(c => AutomationProperties.GetName(c)).ToArray();
        Ok(menu.Contains("Copy") && menu.Contains("Duplicate") && menu.Contains("Delete") && !menu.Any(n => n?.StartsWith("Insert ", StringComparison.Ordinal) == true), "existing elements get edit actions instead of the canvas insert menu"); popup.IsOpen = false;
        var line = new PaperStructure { Kind = "line", Bounds = new(12, 80, 100, 2), BorderWidth = .3 }; builder.AddStructure(line); Settle();
        Ok(Item(line.Id).FieldKey == "" && builder.Values.Count == 3, "separator lines do not allocate an input value");
        builder.AddGuide(PaperGuideOrientation.Vertical, 36); builder.AddGuide(PaperGuideOrientation.Horizontal, 72); Settle();
        string guideId = builder.Definition.Pages[0].Guides[0].Id; builder.MoveGuide(guideId, 38); builder.Undo(); Settle();
        Ok(builder.Definition.Pages[0].Guides[0].Position == 36, "guide positions are undoable editor-only metadata");
        var rulerPoint = page.TranslatePoint(new Point(60 * 96 / 25.4, -12 / builder.Zoom), window)!.Value;
        window.MouseDown(rulerPoint, MouseButton.Left); window.MouseMove(At(90, 60)); window.MouseUp(At(90, 60), MouseButton.Left); Settle();
        Ok(builder.Definition.Pages[0].Guides.Count == 3 && Math.Abs(builder.Definition.Pages[0].Guides.Last().Position - 60) < .2, "dragging from the top ruler creates a horizontal guide at the drop position");
        window.MouseDown(At(36, 110), MouseButton.Right); window.MouseUp(At(36, 110), MouseButton.Right); Settle();
        var guidePopup = (window.FocusManager!.GetFocusedElement() as Control)!.GetLogicalAncestors().OfType<Popup>().Single(p => p.IsOpen);
        guidePopup.Child!.GetVisualDescendants().OfType<Button>().Single(c => AutomationProperties.GetName(c) == "Remove guide").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Settle();
        Ok(builder.Definition.Pages[0].Guides.Count == 2, "guide context actions remove guides without deleting page elements"); builder.Undo(); Settle();
        builder.ShowRulers = false; Settle();
        Ok(!builder.ShowRulers && builder.ShowGrid && builder.ShowGuides && !builder.GetVisualDescendants().OfType<CheckBox>().Any(c => c.Content?.ToString() == "Show rulers"), "rulers can be disabled independently from grid and guide visibility; the toggles live in the View menu");
        builder.SelectedId = text.Id; builder.ReplaceSelected(Item(text.Id) with { SnapToGrid = false, Bounds = Item(text.Id).Bounds with { X = 40.5, Y = 20.5 } }); builder.MoveSelection(.3, .7); Settle();
        Ok(Math.Abs(Item(text.Id).Bounds.X - 40.8) < .001 && Math.Abs(Item(text.Id).Bounds.Y - 21.2) < .001 && Item(text.Id).Bounds.Width == 30, "free move preserves fractional positions while keeping dimensions on the drawing grid");
        builder.SnapToElements = false;
        var alignStart = At(Item(text.Id).Bounds.X + 4, Item(text.Id).Bounds.Y + 4);
        window.MouseDown(alignStart, MouseButton.Left); window.MouseMove(At(39.7, Item(text.Id).Bounds.Y + 4)); window.MouseUp(At(39.7, Item(text.Id).Bounds.Y + 4), MouseButton.Left); Settle();
        Ok(Math.Abs(Item(text.Id).Bounds.X - 36) < .01, "free-moving elements still align to visible guides independently of field snapping"); builder.Undo(); Settle();
        builder.ShowGuides = false; builder.SnapToElements = false;
        var start = At(Item(text.Id).Bounds.X + 4, Item(text.Id).Bounds.Y + 4); var end = start + new Vector(1.1 * 96 / 25.4, .5 * 96 / 25.4);
        window.MouseDown(start, MouseButton.Left); window.MouseMove(end); window.MouseUp(end, MouseButton.Left); Settle();
        Ok(Math.Abs(Item(text.Id).Bounds.X - 41.9) < .01, "pointer free move bypasses drawing-grid rounding");
        string html = builder.ExportHtml();
        Ok(html.Contains("structure line") && !html.Contains(guideId) && !html.Contains("Show rulers"), "HTML exports the separator but excludes rulers and alignment guides");
        builder.Mode = PaperFormMode.Fill; Settle();
        var input = Editor<TextBox>(builder, notes.Id); var value = builder.Values.Single(v => v.Key == "notes");
        value.Text = "one\ntwo\nthree"; Settle(); input.Focus(); input.CaretIndex = input.Text!.Length; window.KeyTextInput("\nfour\nfive\nsix"); Settle();
        Ok(value.Text == "one\ntwo\nthree" && ScrollViewer.GetVerticalScrollBarVisibility(input) == ScrollBarVisibility.Disabled, "non-scrolling multiline fields reject typing beyond their paper viewport");
        Complete(window.Clipboard!.SetTextAsync(string.Join("\n", Enumerable.Repeat("long", 30)))); input.SelectAll(); input.Paste(); Settle();
        Ok(value.Text == "one\ntwo\nthree", "oversized multiline paste is rejected without losing the existing draft");
        input.CaretIndex = input.Text!.Length; window.KeyTextInput("!"); Settle(); input.Undo(); Settle();
        Ok(value.Text == "one\ntwo\nthree", "accepted bounded multiline editing retains native undo");
        value.Text = "Short"; Settle(); input.CaretIndex = input.Text!.Length;
        for (int i = 0; i < 30; i++) { window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.None, null); window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.None, null); Settle(); }
        builder.RefreshProblems();
        Ok(!builder.Problems.Any(p => p.StructureId == notes.Id), "Enter cannot grow a bounded multiline field beyond its visible height");
        value.Text = string.Join("\n", Enumerable.Repeat("external", 20)); Settle(); builder.RefreshProblems();
        Ok(value.Text.Contains("external") && builder.Problems.Any(p => p.StructureId == notes.Id), "external multiline overflow is preserved and reported rather than truncated");
        input.SelectAll(); window.KeyTextInput("Short"); Settle();
        Ok(value.Text == "Short", "an invalid external multiline draft remains replaceable");
        builder.Mode = PaperFormMode.Design; builder.SelectedId = notes.Id; builder.ReplaceSelected(Item(notes.Id) with { AllowMultilineScrolling = true }); builder.Mode = PaperFormMode.Fill; Settle();
        input = Editor<TextBox>(builder, notes.Id); input.Focus(); input.SelectAll(); window.KeyTextInput(string.Join("\n", Enumerable.Repeat("draft", 20))); Settle(); builder.RefreshProblems();
        Ok(value.Text.Contains("draft") && ScrollViewer.GetVerticalScrollBarVisibility(input) == ScrollBarVisibility.Auto && builder.Problems.Any(p => p.StructureId == notes.Id), "scroll-enabled multiline fields accept longer drafts but still report unprintable overflow");
        window.Close();
    }
}

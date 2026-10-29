using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.VisualTree;
using static Shade.Look;

namespace Shade.Tests;

static partial class Program
{
    static void PaperFormFeedback()
    {
        void Complete(Task task)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (!task.IsCompleted && clock.ElapsedMilliseconds < 3000) Settle();
            if (!task.IsCompleted) throw new Exception("Clipboard task did not complete.");
            task.GetAwaiter().GetResult(); Settle();
        }
        var serial = new PaperStructure { Kind = "characters", CellCount = 16, Bounds = new(12, 12, 64.4, 8), Alignment = TextAlignment.Center };
        var text = new PaperStructure { Kind = "caption", Bounds = new(12, 36, 75, 22), Caption = "Test", Subtitle = "Supporting text" };
        var builder = new PaperFormBuilder { Zoom = 1, Height = 760 };
        builder.AddStructure(serial); serial = builder.Definition.Pages[0].Structures[0];
        builder.AddStructure(text); text = builder.Definition.Pages[0].Structures[1];
        Ok(serial.FieldKey.Length > 0 && text.FieldKey.Length > 0 && serial.FieldKey != text.FieldKey, "new fields receive automatic unique keys through the public insert path");
        var window = new Window { Width = 1100, Height = 900, Content = builder }; window.Show(); Settle();
        Field Inspector(string label) => builder.GetVisualDescendants().OfType<StackPanel>().Where(p => p.Children.Count >= 2 && p.Children[0] is TextBlock t && t.Text == label).Select(p => p.Children[1]).OfType<Field>().Single();
        builder.SelectedId = serial.Id; Settle();
        var count = Inspector("Cell count"); count.Box.Focus(); count.Box.SelectAll(); window.KeyTextInput("20"); Settle();
        var updated = builder.Definition.Pages[0].Structures[0];
        Ok(updated.CellCount == 20 && Math.Abs(updated.Bounds.Width - 80) < .001 && count.Box.IsFocused && ReferenceEquals(count, Inspector("Cell count")), "cell-count edits immediately resize the field without rebuilding the focused sidebar input");
        Ok(Inspector("Field key").IsReadOnly && Inspector("Width (automatic, mm)").IsReadOnly, "generated keys and computed character-field width are clearly read-only");
        builder.ReplaceSelected(updated with { Bounds = updated.Bounds with { Width = 49 } }); Settle();
        updated = builder.Definition.Pages[0].Structures[0];
        Ok(updated.CellCount == 12 && Math.Abs(updated.Bounds.Width - 48) < .001 && updated.CellWidth == 4, "manual character width changes cell capacity and snaps to complete fixed-pitch cells");
        var resizePage = builder.GetVisualDescendants().OfType<Canvas>().First(c => c.Width > 700);
        Point ResizeAt(double x, double y) => resizePage.TranslatePoint(new Point(x * 96 / 25.4, y * 96 / 25.4), window)!.Value;
        var corner = ResizeAt(updated.Bounds.X + updated.Bounds.Width - .4, updated.Bounds.Y + updated.Bounds.Height - .4);
        window.MouseDown(corner, MouseButton.Left); window.MouseMove(corner + new Vector(12.2 * 96 / 25.4, 10 * 96 / 25.4)); window.MouseUp(corner + new Vector(12.2 * 96 / 25.4, 10 * 96 / 25.4), MouseButton.Left); Settle();
        var resized = builder.Definition.Pages[0].Structures[0];
        Ok(resized.CellCount == 15 && resized.CellWidth == 4 && resized.Bounds.Height == updated.Bounds.Height && Math.Abs(resized.Bounds.Width - 60) < .01, $"drag resizing adds whole cells while preserving cell height and pitch: {resized.Bounds} cells {resized.CellCount}, expected height {updated.Bounds.Height}");
        builder.SelectedId = text.Id; Settle();
        var caption = Inspector("Caption"); caption.Box.Focus(); caption.Box.SelectAll(); window.KeyTextInput("Updated"); Settle(); window.KeyTextInput(" caption"); Settle();
        Ok(builder.Definition.Pages[0].Structures[1].Caption == "Updated caption" && caption.Box.IsFocused && caption.Box.CaretIndex == 15, "sidebar text applies on every edit and preserves caret/focus across successive edits");
        builder.Undo(); Settle(); Ok(builder.Definition.Pages[0].Structures[1].Caption == "Test", "successive live sidebar edits form one layout undo step");
        builder.SelectedId = null; Settle();
        var margin = Inspector("Left margin (mm)"); margin.Box.Focus(); margin.Box.SelectAll(); window.KeyTextInput("15"); Settle();
        Ok(builder.Definition.MarginLeft == 15 && margin.Box.IsFocused, "page settings apply immediately without Enter or a Save button");
        builder.Undo(); Settle();

        builder.AddPage(); Settle();
        Ok(builder.Definition.Pages.Count == 2 && builder.PageIndex == 1 && builder.GetVisualDescendants().OfType<Canvas>().Count(c => c.Width > 700) == 2 && builder.GetVisualDescendants().OfType<CharacterGridField>().Any(), "Add page activates the new blank page while keeping previous pages in the canvas");
        builder.Undo(); Settle(); Ok(builder.PageIndex == 0 && builder.Definition.Pages.Count == 1, "undoing Add page restores the previous page and its fields");
        builder.Redo(); Settle(); Ok(builder.PageIndex == 1 && builder.Definition.Pages.Count == 2, "redoing Add page restores the new active page"); builder.Undo(); Settle();

        var page = builder.GetVisualDescendants().OfType<Canvas>().First(c => c.Width > 700);
        Point At(double x, double y) => page.TranslatePoint(new Point(x * 96 / 25.4, y * 96 / 25.4), window)!.Value;
        var focusPoint = At(16, 16); window.MouseDown(focusPoint, MouseButton.Left); window.MouseUp(focusPoint, MouseButton.Left); Settle();
        builder.SelectedId = serial.Id;
        var value = builder.Values.Single(v => v.Key == serial.FieldKey); value.Text = "0026018342";
        window.KeyPress(Key.C, RawInputModifiers.Control, PhysicalKey.None, null); window.KeyRelease(Key.C, RawInputModifiers.Control, PhysicalKey.None, null); Settle();
        var copiedText = window.Clipboard!.TryGetTextAsync(); Complete(copiedText);
        Ok(copiedText.Result?.StartsWith("Shade.PaperStructure/1\n", StringComparison.Ordinal) == true, "Ctrl+C copies the selected design block instead of text from a sidebar field");
        int before = builder.Definition.Pages[0].Structures.Count;
        window.KeyPress(Key.V, RawInputModifiers.Control, PhysicalKey.None, null); window.KeyRelease(Key.V, RawInputModifiers.Control, PhysicalKey.None, null);
        for (int i = 0; i < 40 && builder.Definition.Pages[0].Structures.Count == before; i++) Settle();
        var pasted = builder.Definition.Pages[0].Structures.Last();
        Ok(builder.Definition.Pages[0].Structures.Count == before + 1 && pasted.Id != serial.Id && pasted.FieldKey != serial.FieldKey && builder.Values.Single(v => v.Key == pasted.FieldKey).Text == value.Text, "Ctrl+V inserts the copied block with new identity/key and preserved values");
        builder.Undo(); Settle();
        var contextPoint = At(110, 70); window.MouseDown(contextPoint, MouseButton.Right); window.MouseUp(contextPoint, MouseButton.Right); Settle();
        var focused = window.FocusManager!.GetFocusedElement() as Control;
        var popup = focused?.GetLogicalAncestors().OfType<Popup>().SingleOrDefault(p => p.IsOpen);
        Ok(popup != null, "right-click opens the shared insert-block menu");
        if (popup?.Child is Control menu)
        {
            Ok(!menu.GetVisualDescendants().OfType<ScrollViewer>().Any() && menu.Bounds.Height > ControlHeight * 10 + 8, "insert menu shows every action at full height without an internal scrollbar");
            var insert = menu.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Insert text field");
            insert.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Settle();
            var inserted = builder.Definition.Pages[0].Structures.Last();
            Ok(inserted.Kind == "text" && inserted.FieldKey.Length > 0 && !popup.IsOpen, "right-click insertion creates a named block at the pointer and closes its menu");
            builder.Undo(); Settle();
        }
        var anchor = new Point(80 * 96 / 25.4, 90 * 96 / 25.4); var screen = page.TranslatePoint(anchor, window)!.Value;
        window.MouseWheel(screen, new Vector(0, 1), RawInputModifiers.Control); Settle();
        var after = page.TranslatePoint(anchor, window)!.Value;
        Ok(builder.Zoom > 1 && Math.Abs(after.X - screen.X) < 1 && Math.Abs(after.Y - screen.Y) < 1, $"Ctrl+wheel zooms around the pointer instead of moving its document location (zoom={builder.Zoom}, before={screen}, after={after})");

        builder.Zoom = 1; builder.Mode = PaperFormMode.Fill; Settle();
        var grid = builder.GetVisualDescendants().OfType<CharacterGridField>().Single();
        value.Text = new string('0', grid.CellCount); grid.Focus(); grid.CaretIndex = grid.Text!.Length; Settle();
        window.KeyTextInput("9"); Settle(); Ok(grid.Text == new string('0', grid.CellCount) && value.Text == grid.Text, "fill mode rejects typing beyond character capacity without mutating the binding");
        Complete(window.Clipboard!.SetTextAsync(new string('9', grid.CellCount + 1))); grid.SelectAll(); grid.Paste(); Settle();
        Ok(grid.Text == new string('0', grid.CellCount) && grid.SelectionEnd - grid.SelectionStart == grid.CellCount, "oversized paste is rejected atomically, preserving the original value and selection");
        grid.SelectionStart = 0; grid.SelectionEnd = 1; Complete(window.Clipboard!.SetTextAsync("e\u0301")); grid.Paste(); Settle();
        Ok(grid.Text == "e\u0301" + new string('0', grid.CellCount - 1), "a composed grapheme fits one cell during native clipboard replacement");
        grid.Undo(); Settle(); Ok(grid.Text == new string('0', grid.CellCount), "native undo restores one accepted clipboard edit");
        grid.CaretIndex = 4; Settle();
        var presenter = grid.GetVisualDescendants().OfType<TextPresenter>().Single();
        using (var glyph = new TextLayout("0", new Typeface(grid.FontFamily), grid.FontSize, grid.Foreground))
        {
            double caret = presenter.TranslatePoint(presenter.TextLayout.HitTestTextPosition(4).Position, grid)!.Value.X;
            double expected = 4 * grid.CellSize.Width + (grid.CellSize.Width - glyph.WidthIncludingTrailingWhitespace) / 2;
            Ok(Math.Abs(caret - expected) < .6, $"the native caret sits at the centred character's insertion origin within pixel rounding (caret={caret},expected={expected})");
        }
        var ordinary = page.GetVisualDescendants().OfType<TextBox>().Single(t => t is not CharacterGridField);
        Ok(Math.Abs(ordinary.Padding.Left - 96 / 25.4) < .01, "plain/captioned values have configurable one-millimetre padding inside the border");
        window.Close();

        PaperStructure Block(double x, double y) => new() { Bounds = new(x, y, 19, 8) };
        var snapping = new PaperFormBuilder { Zoom = .75, GridPitch = 3, ShowToolbar = false, ShowInspector = false };
        snapping.AddStructure(Block(10, 20)); snapping.AddStructure(Block(39, 20)); snapping.AddStructure(Block(78, 38));
        var host = new Window { Width = 1000, Height = 750, Content = snapping }; host.Show(); Settle();
        var canvas = snapping.GetVisualDescendants().OfType<Canvas>().First(c => c.Width > 700);
        Point SnapAt(double x, double y) => canvas.TranslatePoint(new Point(x * 96 / 25.4, y * 96 / 25.4), host)!.Value;
        host.MouseDown(SnapAt(83, 41), MouseButton.Left); host.MouseMove(SnapAt(74, 23)); host.MouseUp(SnapAt(74, 23), MouseButton.Left); Settle();
        var aligned = snapping.Definition.Pages[0].Structures.Last();
        Ok(Math.Abs(aligned.Bounds.X - 69) < .01 && Math.Abs(aligned.Bounds.Y - 21) < .01, "moving fields snaps to matching row edges and the existing ten-millimetre gap, on the drawing grid");
        host.Close();
    }
}

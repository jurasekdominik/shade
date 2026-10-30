using System;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.TextInput;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Shade.Tests;

static partial class Program
{
    static void PaperFormPooling()
    {
        PaperStructure Field(string key, string kind, double y) => new() { Id = key, FieldKey = key, Label = key, Kind = kind, Bounds = new(12, y, 60, 8) };
        var first = Field("first", "text", 20);
        var second = Field("second", "text", 38);
        var check = Field("check", "check", 56) with { Bounds = new(12, 56, 5, 5) };
        var date = Field("date", "date", 74);
        var cells = Field("cells", "characters", 92) with { CellCount = 10, Bounds = new(12, 92, 40, 8) };
        var caption = Field("caption", "caption", 110) with { Title = "Caption", Subtitle = "Subtitle", Bounds = new(12, 110, 60, 24) };
        var multiline = Field("multiline", "multiline", 144) with { Bounds = new(12, 144, 80, 32) };
        var last = Field("last", "text", 20);
        var labels = Enumerable.Range(0, 70).Select(i => new PaperStructure { Kind = "label", Title = "Label", Bounds = new(100, 10 + i * 3, 20, 3) });
        var form = new PaperFormView { ShowAllPages = true };
        form.UsePainter(true);
        form.Definition = new() { Pages = new[] { new PaperPage { Structures = new[] { first }.Concat(labels).Concat(new[] { second, check, date, cells, caption, multiline }).ToArray() }, new PaperPage { Structures = new[] { last } } } };
        form.Values.Single(v => v.Key == "first").Text = "Original";
        form.Values.Single(v => v.Key == "second").Text = "Second";
        var before = new ActionButton("Before"); var after = new ActionButton("After");
        var panel = new StackPanel { Children = { before, form, after } };
        var window = new Window { Width = 1100, Height = 1000, Content = new ScrollViewer { Content = panel } }; window.Show(); Settle();
        TextBox Box() => form.ActiveEditor!.GetSelfAndVisualDescendants().OfType<TextBox>().Single();
        void PressKey(Key key, RawInputModifiers modifiers = RawInputModifiers.None) { window.KeyPress(key, modifiers, PhysicalKey.None, null); window.KeyRelease(key, modifiers, PhysicalKey.None, null); Settle(); }
        void Click(PaperStructure field, double x = 2)
        {
            var at = form.Pages[0].TranslatePoint(new Point((field.Bounds.X + x) * 96 / 25.4, (field.Bounds.Y + 4) * 96 / 25.4), window)!.Value;
            window.MouseDown(at, MouseButton.Left); window.MouseUp(at, MouseButton.Left); Settle();
        }
        void Host(PaperStructure changed) { form.Definition = form.Definition with { Pages = new[] { form.Definition.Pages[0] with { Structures = form.Definition.Pages[0].Structures.Select(s => s.Id == changed.Id ? changed : s).ToArray() }, form.Definition.Pages[1] } }; Settle(); }
        Click(first);
        Ok(form.ActiveField == first.Id && Box().IsFocused, "drawn paper pointer input activates the first drawing band");
        var originalEditor = Box(); int caret = originalEditor.CaretIndex;
        form.UsePainter(false); Settle(); Click(first);
        var native = form.Pages[0].GetVisualDescendants().OfType<TextBox>().Single(c => AutomationProperties.GetName(c) == "first");
        Ok(native.CaretIndex == caret, "pooled paper pointer input places the caret at the native click position");
        form.UsePainter(true); Settle(); Click(first); originalEditor = Box();
        originalEditor.SelectAll(); window.KeyTextInput("Edited"); Settle();
        var value = form.Values.Single(v => v.Key == "first");
        Ok(value.Text == "Edited", "pooled paper edits commit through a native two-way value binding");
        int selection = originalEditor.CaretIndex;
        form.Definition = form.Definition with { Pages = new[] { form.Definition.Pages[0] with { Structures = form.Definition.Pages[0].Structures.Select(s => s.Id == first.Id ? s with { Title = "Host title" } : s).ToArray() }, form.Definition.Pages[1] } }; Settle();
        Ok(ReferenceEquals(originalEditor, Box()) && originalEditor.IsFocused && originalEditor.CaretIndex == selection, "paper host updates retain the active native editor and caret");
        originalEditor.Undo(); Settle();
        Ok(value.Text == "Original", "paper host updates retain the active editor's native undo");
        originalEditor.SelectAll(); window.KeyTextInput("Styled"); Settle(); selection = originalEditor.CaretIndex;
        Host(first with { FontSize = 14, Title = "Styled title", Bounds = first.Bounds with { Width = 70 } });
        form.Definition = form.Definition with { Foreground = "#203040" }; Settle();
        Ok(ReferenceEquals(originalEditor, Box()) && Box().IsFocused && Box().FontSize == 14 && Box().CaretIndex == selection, "paper font, size and document-colour edits restyle the active input without replacing it");
        originalEditor.Undo(); Settle();
        Ok(value.Text == "Original", "paper restyling retains native input undo");
        var request = new TextInputMethodClientRequestedEventArgs { RoutedEvent = InputElement.TextInputMethodClientRequestedEvent };
        originalEditor.RaiseEvent(request); var client = request.Client!;
        client.SetPreeditText("仮");
        Host(first with { FontSize = 15, Title = "Composition title", Bounds = first.Bounds with { Width = 70 } });
        var windows = Look.Windows; var mode = Look.Current;
        try { Look.Windows = () => new[] { window }; Look.SetMode(mode == Look.Mode.Dark ? Look.Mode.Light : Look.Mode.Dark); Look.RefreshLanguage(); Settle(); }
        finally { Look.SetMode(mode); Look.Windows = windows; }
        var nextClient = new TextInputMethodClientRequestedEventArgs { RoutedEvent = InputElement.TextInputMethodClientRequestedEvent }; originalEditor.RaiseEvent(nextClient);
        Ok(ReferenceEquals(client, nextClient.Client) && originalEditor.IsFocused && value.Text == "Original"
            && originalEditor.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.TextPresenter>().Single().PreeditText == "仮", "paper restyle, theme and language updates preserve the native IME client and uncommitted composition");
        client.SetPreeditText(null);
        value.Error = "First error"; Settle(); Click(second);
        Ok(form.ActiveField == second.Id && ReferenceEquals(originalEditor, Box()) && Box().Text == "Second", "paper editors are reused across fields and drawing bands");
        Ok(!(DataValidationErrors.GetErrors(Box())?.Cast<object>().Any(e => e.ToString()!.Contains("First error")) ?? false), "pooled paper rebinding clears validation from the previous field");
        Box().SelectAll(); window.KeyTextInput("Other"); Settle();
        Ok(value.Text == "Original" && form.Values.Single(v => v.Key == "second").Text == "Other", "pooled paper rebinding cannot write into the previous field");
        value.Error = null; value.Text = "Host changed"; Settle();
        Ok(Box().Text == "Other", "a detached paper value cannot overwrite the reused editor");
        Click(check);
        Ok(form.ActiveField == check.Id && form.Values.Single(v => v.Key == "check").Checked == true, "forwarded paper checkbox pointer input toggles once");
        form.ActivateEditor(first.Id); Settle(); PressKey(Key.Tab);
        Ok(form.ActiveField == second.Id, "paper Tab follows definition order across drawing bands");
        PressKey(Key.Tab, RawInputModifiers.Shift);
        Ok(form.ActiveField == first.Id, "paper Shift+Tab returns to the previous field");
        form.ActivateEditor(multiline.Id); Settle(); PressKey(Key.Tab);
        Ok(form.ActiveField == last.Id && form.PageIndex == 1 && Box().IsFocused, "paper Tab activates and reveals a field on another page");
        PressKey(Key.Tab);
        Ok(after.IsFocused && form.ActiveField == null, "paper Tab leaves the final field without looping through page entry points");
        before.Focus(); PressKey(Key.Tab);
        Ok(form.ActiveField == first.Id, "paper Tab enters the drawn document at its first field");
        form.ActivateEditor(date.Id); Settle();
        form.ActiveEditor!.GetVisualDescendants().OfType<IconButton>().Single().RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Settle();
        var popup = (window.FocusManager!.GetFocusedElement() as Control)!.GetLogicalAncestors().OfType<Popup>().Single(p => p.IsOpen);
        Ok(form.ActiveField == date.Id && popup.IsOpen, "a paper calendar keeps its pooled editor while the popup owns focus");
        form.ActivateEditor(second.Id); Settle();
        Ok(!popup.IsOpen && form.ActiveField == second.Id, "switching pooled paper fields closes the previous native popup");
        form.ActivateEditor(cells.Id); Settle(); var grid = (CharacterGridField)Box();
        grid.Text = "0007"; grid.CaretIndex = 4;
        Host(cells with { GridAlignment = Avalonia.Media.TextAlignment.Right, CellCount = 6 });
        Ok(ReferenceEquals(grid, Box()) && grid.IsFocused && grid.CaretIndex == 4 && grid.CellCount == 6 && Math.Abs(grid.Width - 24 * 96 / 25.4) < 1, "paper grid geometry and alignment changes keep the native input and caret");
        form.ActivateEditor(caption.Id); Settle(); var captionBox = Box();
        captionBox.Text = "Name"; captionBox.CaretIndex = 4;
        Host(caption with { Title = "New caption", Subtitle = "New subtitle" });
        Ok(ReferenceEquals(captionBox, Box()) && captionBox.IsFocused && captionBox.CaretIndex == 4 && form.ActiveEditor!.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "New caption"), "paper caption changes keep the focused native editor");
        form.ActivateEditor(date.Id); Settle(); var dateBox = Box();
        form.ActiveEditor!.GetVisualDescendants().OfType<IconButton>().Single().RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Settle();
        popup = (window.FocusManager!.GetFocusedElement() as Control)!.GetLogicalAncestors().OfType<Popup>().Single(p => p.IsOpen);
        Host(date with { Title = "New date title", FontSize = 14 });
        Ok(ReferenceEquals(dateBox, Box()) && popup.IsOpen, "an unrelated paper restyle preserves an open native date calendar");
        panel.IsEnabled = false; Settle();
        Ok(form.ActiveField == null && !popup.IsOpen, "disabling a paper ancestor releases the editor and closes its popup");
        panel.IsEnabled = true;
        form.StructureTypes = new[] { new PaperStructureTemplate("custom-input", "Custom input", (_, input, readOnly) =>
        {
            var box = new TextBox { IsReadOnly = readOnly };
            box.Bind(TextBox.TextProperty, new Avalonia.Data.Binding(nameof(PaperFormValue.Text)) { Source = input, Mode = Avalonia.Data.BindingMode.TwoWay }); return box;
        }, (_, _) => "Custom") };
        var custom = Field("custom", "custom-input", 210);
        form.Definition = form.Definition with { Pages = new[] { form.Definition.Pages[0] with { Structures = form.Definition.Pages[0].Structures.Append(custom).ToArray() }, form.Definition.Pages[1] } }; Settle();
        form.ActivateEditor(multiline.Id); PressKey(Key.Tab);
        Ok(form.ActiveField == null && (window.FocusManager!.GetFocusedElement() as Control)?.GetVisualAncestors().Contains(form.Pages[0]) == true, "paper Tab enters a native custom template between drawn fields");
        PressKey(Key.Tab);
        Ok(form.ActiveField == last.Id && form.PageIndex == 1, "paper Tab leaves a native custom template for the next drawn page");
        form.Mode = PaperFormMode.Preview; form.ActivateEditor(first.Id); Settle();
        string text = value.Text; Box().SelectAll(); window.KeyTextInput("Forbidden"); Settle();
        Ok(Box().IsReadOnly && Box().IsFocused && value.Text == text && Box().SelectionEnd > Box().SelectionStart, "drawn paper Preview uses a focusable selectable read-only editor");
        form.Mode = PaperFormMode.Fill; form.ActivateEditor(first.Id); Settle(); panel.IsVisible = false; Settle();
        Ok(form.ActiveField == null && form.ActiveEditor == null, "hiding a paper ancestor releases its active editor");
        panel.IsVisible = true; form.UsePainter(false); Settle();
        Ok(form.Pages.All(p => !p.Focusable) && form.ActiveField == null && form.Page.GetVisualDescendants().OfType<TextBox>().Count() > 1, "switching to native paper rendering releases virtual tab stops and restores native editors");
        form.UsePainter(true); form.ActivateEditor(first.Id); Settle(); window.Content = null; Settle();
        Ok(form.ActiveField == null && originalEditor.Parent == null, "detaching paper releases the editor host and old value bindings");
        window.Close();
    }
}

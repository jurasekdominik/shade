using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using static Shade.Look;

namespace Shade.Tests;

static partial class Program
{
    static void PaperForms()
    {
        var serial = new PaperStructure { Kind = "characters", FieldKey = "serial", Label = "Serial number", Bounds = new(12, 12, 64.4, 7), Alignment = TextAlignment.Center };
        var boolean = new PaperStructure { Kind = "check", FieldKey = "checked", Label = "Checked", Bounds = new(12, 30, 3.175, 3.175) };
        var definition = new PaperFormDefinition { Background = HexOf(White), Foreground = HexOf(Ink), Pages = new[] { new PaperPage { Structures = new[] { serial, boolean } } } };
        var restored = PaperFormDefinition.FromJson(definition.ToJson());
        Ok(restored.ToJson() == definition.ToJson() && restored.PageSize == new Size(210, 297), "paper layout round-trips its versioned millimetre contract without serializing input values");
        void Reject(PaperFormDefinition invalid, string reason) { bool threw = false; try { invalid.Validate(); } catch (ArgumentException) { threw = true; } Ok(threw, reason); }
        Reject(definition with { ShowPageNumbers = true, MarginBottom = 2 }, "printed page numbers require a usable footer margin");
        Reject(definition with { Pages = new[] { new PaperPage { Structures = new[] { new PaperStructure { Kind = "date", FieldKey = "date", DateFormat = "yyyy-QQ-dd" } } } } }, "unsupported date-format tokens are rejected at the layout boundary");
        Reject(definition with { Pages = new[] { new PaperPage { Guides = new[] { new PaperGuide("invalid", PaperGuideOrientation.Vertical, double.NaN) } } } }, "non-finite guide positions are rejected");
        Reject(definition with { Pages = new[] { new PaperPage { Guides = new[] { new PaperGuide("same", PaperGuideOrientation.Vertical, 10), new PaperGuide("same", PaperGuideOrientation.Horizontal, 20) } } } }, "duplicate guide identities are rejected");
        Reject(definition with { Version = 2 }, "unsupported paper schema versions are rejected");
        Reject(definition with { MarginLeft = 205 }, "invalid printable margins are rejected");
        Reject(definition with { Pages = new[] { new PaperPage { Structures = new[] { serial with { FieldKey = "" } } } } }, "inputs without storage keys are rejected rather than silently losing edits on export");
        Reject(definition with { Pages = new[] { new PaperPage { Structures = new[] { serial, serial with { Id = "other" } } } } }, "duplicate field keys are rejected before changing the model");
        var values = new ObservableCollection<PaperFormValue> { new("serial") { Text = "0026018342" }, new("checked") { Checked = true } };
        var model = new Vm { PaperDefinition = definition };
        var builder = new PaperFormBuilder { Values = values, ShowInspector = false, ShowToolbar = false, ShowLayers = false };
        using var binding = builder.Bind(PaperFormView.DefinitionProperty, new Binding(nameof(Vm.PaperDefinition)) { Source = model });
        var window = new Window { Width = 1000, Height = 800, Content = builder }; window.Show(); Settle();
        Ok(builder.ActiveEditor == null, "Design draws the page and keeps no live editor");
        builder.SelectedId = serial.Id;
        builder.ReplaceSelected(serial with { Bounds = serial.Bounds with { X = 20 } }); Settle();
        Ok(model.PaperDefinition.Pages[0].Structures[0].Bounds.X == 20 && builder.CanUndo, "inspector/API layout edits preserve default two-way definition bindings");
        builder.Undo(); Settle(); Ok(builder.Definition.Pages[0].Structures[0].Bounds.X == 12 && builder.CanRedo, "paper layout undo restores geometry");
        builder.Redo(); Settle(); Ok(builder.Definition.Pages[0].Structures[0].Bounds.X == 20, "paper layout redo restores geometry");
        builder.DuplicateSelected(); Settle();
        Ok(builder.Definition.Pages[0].Structures.Count == 3 && values.Single(v => v.Key == "serial").Text == "0026018342" && builder.Definition.Pages[0].Structures.Select(s => s.FieldKey).Distinct().Count() == 3, "duplication assigns new identities and field keys without overwriting values");
        builder.Undo(); Settle();
        model.PaperDefinition = definition with { Landscape = true }; Settle();
        Ok(builder.Definition.PageSize == new Size(297, 210) && !builder.CanUndo, "later external model updates still work and reset stale layout history");

        builder.Mode = PaperFormMode.Fill; Settle(); var grid = Editor<CharacterGridField>(builder, serial.Id);
        builder.ReplaceSelected(builder.Definition.Pages[0].Structures[0] with { Bounds = serial.Bounds with { X = 24 } }); Settle();
        Ok(ReferenceEquals(grid, builder.ActiveEditor!.GetSelfAndVisualDescendants().OfType<CharacterGridField>().Single()) && builder.ActiveField == serial.Id, "moving the active structure retains its native editor, caret and value binding");
        builder.ReplaceSelected(builder.Definition.Pages[0].Structures[0] with { Bounds = serial.Bounds with { X = 20 } }); Settle();
        grid.Focus(); grid.CaretIndex = grid.Text!.Length; window.KeyTextInput("9"); Settle();
        Ok(values[0].Text == "00260183429", "fill mode uses native text input and preserves leading zeroes in separate values");
        values[0].Text = "0026018342"; Settle(); Ok(grid.Text == "0026018342", "external value changes still reach a paper input after native editing");
        values[0].Error = "Required"; Settle(); Ok(grid.ValidationMessage?.Contains("Required") == true && builder.Problems.Any(p => p.Message.EndsWith("Required")), "host validation reaches both native field validation and the external problem list");
        values[0].Error = null; Settle();
        builder.Mode = PaperFormMode.Preview; Settle(); grid = Editor<CharacterGridField>(builder, serial.Id); grid.Focus(); window.KeyTextInput("9"); Settle();
        bool gridReadOnly = grid.IsReadOnly; string gridText = grid.Text!;
        var check = Editor<CheckBox>(builder, boolean.Id);
        bool? ticked = check.IsChecked; check.Focus(); window.KeyPress(Avalonia.Input.Key.Space, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); window.KeyRelease(Avalonia.Input.Key.Space, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null);
        ((Avalonia.Automation.Provider.IToggleProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(check)!).Toggle(); Settle();
        Ok(gridReadOnly && gridText == "0026018342" && values[0].Text == "0026018342" && check.IsEffectivelyEnabled && check.IsFocused && check.IsChecked == ticked, "print preview prevents native input and checkbox mutation (keyboard and automation) while the box stays focusable without fading paper ink");
        builder.Mode = PaperFormMode.Design; Settle();
        foreach (double zoom in new[] { .75, 1d, 1.5 })
        {
            builder.Zoom = zoom; builder.ToolKind = "text"; Settle();
            var page = builder.GetVisualDescendants().OfType<Canvas>().First(c => c.Width > 700);
            var start = page.TranslatePoint(new Point(100 * 96 / 25.4, 60 * 96 / 25.4), window)!.Value;
            var end = page.TranslatePoint(new Point(130 * 96 / 25.4, 70 * 96 / 25.4), window)!.Value;
            window.MouseDown(start, MouseButton.Left); window.MouseMove(end); window.MouseUp(end, MouseButton.Left); Settle();
            var drawn = builder.Definition.Pages[0].Structures.Last();
            Ok(Math.Abs(drawn.Bounds.X - 100) < .01 && Math.Abs(drawn.Bounds.Width - 30) < .01 && drawn.Kind == "text", $"drawing and hit testing use physical snap coordinates at {zoom:P0} zoom: {drawn.Bounds}, actual zoom {builder.Zoom}");
            builder.Undo(); Settle();
        }
        builder.Zoom = 1; builder.ToolKind = null; Settle();
        var surface = builder.GetVisualDescendants().OfType<Canvas>().First(c => c.Width > 700);
        Point At(double x, double y) => surface.TranslatePoint(new Point(x * 96 / 25.4, y * 96 / 25.4), window)!.Value;
        window.MouseDown(At(15, 15), MouseButton.Left); window.MouseMove(At(23, 19)); window.MouseUp(At(23, 19), MouseButton.Left); Settle();
        Ok(builder.Definition.Pages[0].Structures[0].Bounds.X == 20 && builder.Definition.Pages[0].Structures[0].Bounds.Y == 16, "pointer move survives native pointer-capture release and commits once");
        builder.Undo(); Settle();
        var group = new PaperStructure { Kind = "group", Bounds = new(90, 50, 80, 30), Children = new[] { new PaperStructure { FieldKey = "child", Bounds = new(4, 4, 30, 8) } } };
        builder.AddStructure(group); builder.SelectedId = group.Children[0].Id;
        builder.ReplaceSelected(group.Children[0] with { Title = "Child" }); builder.DuplicateSelected(); Settle();
        Ok(builder.Definition.Pages[0].Structures.Last().Children.Count == 2 && builder.Definition.Pages[0].Structures.Count == 3, "group children can be selected, edited and duplicated without escaping their group");
        builder.Undo(); builder.Undo(); builder.Undo(); Settle();
        builder.Mode = PaperFormMode.Fill; Settle();
        var html = builder.ExportHtml();
        Ok(html.Contains("0026018342") && html.Contains("Intl.Segmenter") && html.Contains("createObjectURL") && !html.Contains("__overflow__"), "standalone HTML preserves values, fixed grapheme geometry and editable save behavior");
        values[0].Text = "01234567890123456789"; Settle(); builder.RefreshProblems();
        bool blocked = false; try { builder.ExportHtml(); } catch (InvalidOperationException) { blocked = true; }
        Ok(blocked && values[0].Text.Length == 20 && builder.Problems.Any(), "overflow blocks export and retains the complete invalid draft");
        values[0].Text = "</script><img onerror=alert(1)>"; serial = serial with { CellCount = 64, CellWidth = .5 };
        model.PaperDefinition = definition with { Pages = new[] { new PaperPage { Structures = new[] { serial, boolean } } } }; Settle();
        html = builder.ExportHtml(); Ok(!html.Contains("<img onerror") && html.Contains("&lt;/script&gt;"), "HTML export encodes untrusted field values");
        builder.StructureTypes = new[] { new PaperStructureTemplate("custom", "Custom", (_, value, readOnly) => {
            var input=new TextBox { IsReadOnly=readOnly,Tag="custom" }; input.Bind(TextBox.TextProperty,new Binding(nameof(PaperFormValue.Text)) { Source=value,Mode=BindingMode.TwoWay }); return input;
        }, (_,value)=>"<input data-custom value='"+System.Net.WebUtility.HtmlEncode(value?.Text)+"'>") };
        builder.Mode = PaperFormMode.Design; builder.AddStructure(new PaperStructure { Kind = "custom", FieldKey = "custom", Bounds = new(100, 60, 40, 10) });
        builder.Values.Single(v => v.Key == "custom").Text = "Custom value"; Settle();
        Ok(builder.GetVisualDescendants().OfType<TextBox>().Single(t => Equals(t.Tag, "custom")).Text == "Custom value" && builder.ExportHtml().Contains("data-custom"), "a registered structure supplies native bindings and HTML without changes to the designer core");
        window.Close();

        foreach (var mode in new[] { Mode.Dark, Mode.Light })
        {
            SetMode(mode); var box = new CheckBox { Width = 12, Height = 12, Background = new SolidColorBrush(White), Foreground = new SolidColorBrush(Ink), BorderBrush = new SolidColorBrush(Ink), CornerRadius = new CornerRadius(0), BorderThickness = new Thickness(.5) };
            CheckBoxAppearance.SetIndicatorSize(box, 12); CheckBoxAppearance.SetMarkBrush(box, new SolidColorBrush(Ink));
            var host = new Window { Width = 200, Height = 100, Content = box }; host.Show(); Settle();
            var face = box.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "PART_Box");
            foreach (bool? state in new bool?[] { false, true, null })
            {
                box.IsChecked = state; Settle();
                Ok(face.Bounds.Size == new Size(12, 12) && face.CornerRadius == new CornerRadius(0) && (face.Background as ISolidColorBrush)?.Color == White, $"{mode}/{state}: compact checkbox geometry and explicit fill survive every state");
            }
            box.Focus(); host.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.None, null); host.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.None, null); Settle();
            Ok(ControlAutomationPeer.CreatePeerForElement(box)?.GetProvider<IToggleProvider>() != null, "compact checkbox retains native toggle automation");
            host.Close();
        }
        SetMode(Mode.Dark);
    }
}

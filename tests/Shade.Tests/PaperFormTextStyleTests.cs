using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Shade.Tests;

static partial class Program
{
    static void PaperFormTextStyles()
    {
        var off = new PaperStructure { Kind = "characters", FieldKey = "serial", CellCount = 16, CellWidth = 4.1, SnapToGrid = false, Bounds = new(11.3, 12.7, 65.6, 8) };
        var heading = new PaperStructure { Kind = "text", FieldKey = "heading", Bounds = new(10, 30, 60, 10), FontWeight = FontWeight.Bold, FontStyle = FontStyle.Italic, VerticalAlignment = PaperVerticalAlignment.Bottom, BorderStyle = PaperBorderStyle.BottomTicks, EndTickHeight = 3 };
        var empty = new PaperStructure { Kind = "text", FieldKey = "empty", Bounds = new(10, 50, 60, 10) };
        var group = new PaperStructure { Kind = "group", Bounds = new(10, 70, 40, 10) };
        var cross = new PaperStructure { Kind = "check", FieldKey = "cross", Bounds = new(10, 90, 5, 5), CheckMark = PaperCheckMark.Cross, CheckMarkSize = .7 };
        var right = new PaperStructure { Kind = "characters", FieldKey = "right", CellCount = 4, CellWidth = 4, GridAlignment = Avalonia.Media.TextAlignment.Right, Bounds = new(10, 100, 40, 8), Title = "Right" };
        var definition = new PaperFormDefinition { ActiveFieldBackground = "#DCEBFF", Pages = new[] { new PaperPage { Structures = new[] { off, heading, empty, group, cross, right } } } };
        var values = new ObservableCollection<PaperFormValue> { new("serial") { Text = "111111" }, new("heading") { Text = "Title" }, new("empty") };
        var builder = new PaperFormBuilder { Definition = definition, Values = values, Zoom = 1, Height = 760 };
        var window = new Window { Width = 1100, Height = 900, Content = builder }; window.Show(); Settle();

        var kept = builder.Definition.Pages[0].Structures[0];
        Ok(kept.Bounds == off.Bounds && kept.CellWidth == 4.1, "an imported structure that does not snap keeps its size and cell pitch");

        var back = PaperFormDefinition.FromJson(definition.ToJson()).Pages[0].Structures[1];
        Ok(back.FontWeight == FontWeight.Bold && back.FontStyle == FontStyle.Italic && back.VerticalAlignment == PaperVerticalAlignment.Bottom && back.BorderStyle == PaperBorderStyle.BottomTicks && back.EndTickHeight == 3, "font weight and style, vertical alignment and border style survive save and reload");
        bool refused = false; try { (definition with { Pages = new[] { new PaperPage { Structures = new[] { heading with { BorderStyle = (PaperBorderStyle)9 } } } } }).ToJson(); } catch (ArgumentException) { refused = true; }
        Ok(refused, "an unknown border style is refused");

        var html = builder.ExportHtml();
        Ok(html.Contains("font-weight:700") && html.Contains("font-style:italic") && html.Contains("border-bottom-color:") && html.Contains("linear-gradient") && html.Contains("justify-content:flex-end") && html.Contains("rgba(220,235,255", StringComparison.Ordinal), "HTML carries weight, style, bottom rule with ticks and vertical alignment");

        string[] Rows() => builder.GetVisualDescendants().OfType<ListBox>().Single(l => Avalonia.Automation.AutomationProperties.GetName(l) == "Page elements").GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? "").ToArray();
        bool Lists(string name) => Rows().Any(r => r.Contains(name));
        Ok(Lists("empty") && Lists("Group") && !Lists("serial") && !Lists("heading"), "the review list shows empty elements and hides filled ones by default");
        builder.LayerFilter = PaperLayerFilter.All; Settle();
        Ok(Lists("serial") && Lists("heading") && Lists("empty"), "All lists every element");
        builder.LayerFilter = PaperLayerFilter.Attention; builder.SelectedId = heading.Id; Settle();
        Ok(Lists("heading"), "the selected element stays listed when it no longer matches the filter");
        // one edit changes one row: no page of the list is rebuilt
        builder.SelectedId = null; Settle();
        int rebuilt = builder.LayerPageUpdateCount;
        values.Single(v => v.Key == "serial").Text = ""; Settle();
        Ok(Lists("serial") && builder.LayerPageUpdateCount == rebuilt, "a field turning empty adds only its own row to the review list");
        values.Single(v => v.Key == "serial").Text = "111111"; Settle();
        Ok(!Lists("serial") && builder.LayerPageUpdateCount == rebuilt, "a field being filled removes only its own row");
        builder.SelectedId = heading.Id; Settle(); builder.SelectedId = null; Settle();
        Ok(!Lists("heading") && builder.LayerPageUpdateCount == rebuilt, "moving the selection adds and removes only the selected row");
        Ok(builder.Definition.Pages[0].Structures.Count == 6, "filtering never removes an element from the page");

        var html2 = builder.ExportHtml();
        Ok(html2.Contains("mark-cross") && PaperFormDefinition.FromJson(definition.ToJson()).Pages[0].Structures[4].CheckMark == PaperCheckMark.Cross, "the tick style reaches HTML and survives save and reload");
        var fitted = builder.Definition.Pages[0].Structures.Single(x => x.Id == right.Id);
        Ok(fitted.Bounds.Width == 16 && Math.Abs(fitted.Bounds.X + fitted.Bounds.Width - 50) < .001 && PaperFormView.GridInset(fitted) == 0, "a right-aligned grid is exactly its cells wide and keeps its right edge, like a left-aligned one keeps its left");
        bool tooBig = false; try { (definition with { Pages = new[] { new PaperPage { Structures = new[] { cross with { CheckMarkSize = .9 } } } } }).ToJson(); } catch (ArgumentException) { tooBig = true; }
        Ok(tooBig && html2.Contains("--mark-scale:1.75"), "a tick larger than its box is refused and the size reaches the HTML");
        builder.Mode = PaperFormMode.Design; builder.SelectedId = right.Id; Settle();
        Field Row(string label) => builder.GetVisualDescendants().OfType<StackPanel>().Where(p => p.Children.Count >= 2 && p.Children[0] is Avalonia.Controls.TextBlock t && t.Text == label).Select(p => p.Children[1]).OfType<Field>().Single();
        var cells = Row("Cell count"); cells.Box.Focus(); cells.Box.SelectAll(); window.KeyTextInput("6"); Settle();
        var grown = builder.Definition.Pages[0].Structures.Single(x => x.Id == right.Id);
        Ok(grown.CellCount == 6 && grown.Bounds.Width == 24 && Math.Abs(grown.Bounds.X + grown.Bounds.Width - 50) < .001, "more cells in a right-aligned grid grow to the left");
        var count = Row("Highlighted cells"); count.Box.Focus(); count.Box.SelectAll(); window.KeyTextInput("2"); Settle();
        var ranged = builder.Definition.Pages[0].Structures.Single(x => x.Id == right.Id).CellRanges;
        Ok(ranged.Count == 1 && ranged[0].Length == 2, "highlighted cells of a character field are editable in the inspector");
        count = Row("Highlighted cells"); count.Box.Focus(); count.Box.SelectAll(); window.KeyTextInput("0"); Settle();
        Ok(builder.Definition.Pages[0].Structures.Single(x => x.Id == right.Id).CellRanges.Count == 0, "zero highlighted cells removes the highlight");
        // a right-aligned grid with the caret inside its last cell: the text must not make the field scroll its cells off the frame
        var wide = new PaperStructure { Kind = "characters", FieldKey = "wide", CellCount = 12, CellWidth = 5, GridAlignment = Avalonia.Media.TextAlignment.Right, Alignment = Avalonia.Media.TextAlignment.Center, Bounds = new(10, 10, 60, 8) };
        var fill = new PaperFormView { Definition = new PaperFormDefinition { Pages = new[] { new PaperPage { Structures = new[] { wide } } } }, Values = new ObservableCollection<PaperFormValue> { new("wide") { Text = "1123" } }, Mode = PaperFormMode.Fill };
        var host = new Window { Width = 900, Height = 600, Content = fill }; host.Show(); Settle();
        fill.ActivateEditor(wide.Id); Settle();
        var cellsField = fill.GetVisualDescendants().OfType<CharacterGridField>().First(); cellsField.Focus(); cellsField.CaretIndex = 3; Settle();
        var viewer = cellsField.GetVisualDescendants().OfType<ScrollViewer>().First();
        Ok(viewer.Extent.Width <= viewer.Viewport.Width + .001 && viewer.Offset.X == 0, "text in the last cell of a right-aligned grid never overhangs the field or scrolls it");
        host.Close();
        window.Close();
    }
}

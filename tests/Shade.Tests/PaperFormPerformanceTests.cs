using System;
using System.Linq;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.VisualTree;
using Avalonia.Headless;
using Avalonia;
using Avalonia.Input;

namespace Shade.Tests;

static partial class Program
{
    static void PaperFormPerformance()
    {
        var flat = PaperScaleDefinition(200, 1).Pages[0].Structures;
        _ = PaperFormView.All(flat).Count();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        int walked = 0; foreach (var _ in PaperFormView.All(flat)) walked++;
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Ok(walked == flat.Count && allocated < 1024, $"walking flat paper structures avoids per-field traversal allocations ({allocated} bytes)");
        var leaf = new PaperStructure { FieldKey = "edited", Bounds = new(2, 2, 20, 8) };
        var sibling = new PaperStructure { FieldKey = "sibling", Bounds = new(2, 20, 20, 8) };
        var group = new PaperStructure { Kind = "group", Bounds = new(10, 10, 60, 60), Children = new[] { leaf, sibling } };
        var untouched = new PaperStructure { Kind = "group", Bounds = new(100, 10, 60, 60), Children = new[] { new PaperStructure { FieldKey = "other", Bounds = new(2, 2, 20, 8) } } };
        var second = new PaperPage { Structures = new[] { new PaperStructure { FieldKey = "second", Bounds = new(10, 10, 20, 8) } } };
        var builder = new PaperFormBuilder { LayerFilter = PaperLayerFilter.All, Definition = new() { Pages = new[] { new PaperPage { Structures = new[] { group, untouched } }, second } } };
        var window = new Window { Content = builder, Width = 1400, Height = 1000 }; window.Show(); Settle();
        Ok(!builder.Pages.SelectMany(p => p.GetVisualDescendants()).OfType<TextBox>().Any(), "drawn Design pages contain no per-field native text editors");
        var designPeer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(builder.Pages[0]);
        Ok(designPeer.GetChildren().Any(p => p.GetName() == "Paper design grid" && p.IsControlElement()), "drawn page automation retains the native design surface");
        builder.SelectedId = leaf.Id;
        var original = builder.Definition;
        PaperStructure Current() => builder.Definition.Pages[0].Structures[0].Children.Single(s => s.Id == leaf.Id);
        bool Shared() => ReferenceEquals(builder.Definition.Pages[0].Structures[1], untouched)
            && ReferenceEquals(builder.Definition.Pages[0].Structures[0].Children.Single(s => s.Id == sibling.Id), sibling)
            && ReferenceEquals(builder.Definition.Pages[1], second);
        int updates = builder.DesignerUpdateCount;
        int prepared = builder.DrawingPreparationCount;
        int layerUpdates = builder.LayerPageUpdateCount;
        builder.ReplaceSelected(Current() with { Title = "Changed" });
        Ok(builder.DesignerUpdateCount == updates + 1, "paper property edit flushes one designer update synchronously");
        Ok(builder.DrawingPreparationCount - prepared == PaperFormView.All(builder.Definition.Pages[0].Structures).Count(), "paper drawing preparation skips pages whose structure lists are unchanged");
        Ok(builder.LayerPageUpdateCount == layerUpdates + 1, "paper layer reconciliation skips unchanged pages");
        Ok(Shared() && Current().Title == "Changed", "paper property edit preserves unrelated nested structures and pages");
        builder.Undo();
        Ok(ReferenceEquals(builder.Definition, original), "paper undo restores the original snapshot by reference");
        builder.Redo();
        Ok(Shared() && Current().Title == "Changed", "paper redo retains shared branches");
        for (int i = 0; i < 10; i++) builder.ReplaceSelected(Current() with { Bounds = Current().Bounds with { Width = 22 + i * 2 } });
        Ok(Shared() && Current().Bounds.Width == 40, "repeated paper resizes copy only the edited path");
        var otherValue = builder.Values.Single(v => v.Key == "second");
        otherValue.Error = "Pending on another page";
        builder.ReplaceSelected(Current() with { Title = "With pending validation" });
        Ok(builder.Problems.Any(p => p.StructureId == second.Structures[0].Id && p.Message.Contains("Pending on another page")), "paper layout edits include queued value errors on an unchanged page");
        otherValue.Error = null; Settle();
        updates = builder.DesignerUpdateCount;
        builder.MoveSelection(2, 0);
        Ok(builder.DesignerUpdateCount == updates + 1, "paper movement flushes one designer update synchronously");
        Ok(Shared() && Current().Bounds.X == 4, "paper movement shares unaffected groups and pages");
        builder.MoveSelection(70, 0);
        Ok(builder.Definition.Pages[0].Structures[0].Children.Count == 1
            && ReferenceEquals(builder.Definition.Pages[0].Structures[0].Children[0], sibling)
            && builder.Definition.Pages[0].Structures.Any(s => s.Id == leaf.Id && s.Bounds.X == 84)
            && ReferenceEquals(builder.Definition.Pages[1], second), "paper movement out of a group preserves offsets and untouched pages");
        builder.Undo(); builder.DeleteSelected();
        Ok(Shared() && builder.Definition.Pages[0].Structures[0].Children.Count == 1, "paper deletion shares unrelated branches");
        int publications = 0;
        builder.PropertyChanged += (_, change) => { if (change.Property == PaperFormView.ProblemsProperty) publications++; };
        var problems = builder.Problems;
        builder.RefreshProblems(); builder.RefreshProblems();
        Ok(publications == 0 && ReferenceEquals(problems, builder.Problems), "unchanged paper problems keep their list and do not notify the designer");
        var value = builder.Values.Single(v => v.Key == "sibling");
        layerUpdates = builder.LayerPageUpdateCount;
        value.Error = "Correct this"; Settle();
        Ok(publications == 1 && builder.Problems.Any(p => p.StructureId == sibling.Id && p.Message.Contains("Correct this")), "changed paper validation publishes once");
        Ok(builder.LayerPageUpdateCount == layerUpdates + 1, "a paper value error refreshes only its own page's layer rows");
        value.Error = null; Settle();
        Ok(publications == 2, "clearing a paper validation error publishes the recovered problems");
        var values = new ObservableCollection<PaperFormValue>();
        prepared = builder.DrawingPreparationCount;
        builder.Values = values; Settle();
        Ok(builder.DrawingPreparationCount - prepared >= PaperFormView.All(builder.Definition.Pages.SelectMany(p => p.Structures)).Count(), "replacing paper values refreshes artwork even when every page layout is unchanged");
        Ok(ReferenceEquals(builder.Values, values) && values.Select(v => v.Key).Distinct().Count() == 3, "paper missing-value batching preserves an application-owned observable collection");
        values.Single(v => v.Key == "sibling").Error = "Observed"; Settle();
        Ok(builder.Problems.Any(p => p.Message.Contains("Observed")), "batched paper values retain their validation subscriptions");
        var beforeInvalid = builder.Definition;
        bool rejected = false;
        try { builder.Definition = beforeInvalid with { MarginLeft = -1 }; } catch (ArgumentException) { rejected = true; }
        Ok(rejected && ReferenceEquals(builder.Definition, beforeInvalid), "paper assignment still rejects invalid snapshots before changing the model");
        rejected = false;
        builder.SelectedId = sibling.Id;
        try { builder.ReplaceSelected(sibling with { FontSize = -1 }); } catch (ArgumentException) { rejected = true; }
        Ok(rejected && ReferenceEquals(builder.Definition, beforeInvalid), "paper edits still validate before entering undo history");
        ComboBox ToolbarChoice(string name) => builder.GetVisualDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == name);
        var orientation = ToolbarChoice("Orientation");
        orientation.IsDropDownOpen = true; Settle();
        builder.Zoom = 1.5;
        builder.ReplaceSelected(sibling with { Title = "Toolbar stays" });
        Ok(ReferenceEquals(orientation, ToolbarChoice("Orientation")) && orientation.IsDropDownOpen, "paper zoom and definition edits preserve toolbar controls and an open unrelated dropdown");
        orientation.IsDropDownOpen = false;
        var width = builder.GetVisualDescendants().OfType<StackPanel>().Where(p => p.Children.Count >= 2 && p.Children[0] is TextBlock t && t.Text == "Width (mm)").Select(p => p.Children[1]).OfType<Field>().Single();
        var layerList = builder.GetVisualDescendants().OfType<ListBox>().Single(c => AutomationProperties.GetName(c) == "Page elements");
        var rowsSource = layerList.ItemsSource; var lastRow = layerList.Items[layerList.Items.Count - 1];
        var selectedLayer = layerList.SelectedItem; int rowChanges = 0;
        ((System.Collections.Specialized.INotifyCollectionChanged)rowsSource!).CollectionChanged += (_, _) => rowChanges++;
        width.Box.Focus(); width.Box.SelectAll(); window.KeyTextInput("bad");
        var currentSibling = PaperFormView.All(builder.Definition.Pages[0].Structures).Single(s => s.Id == sibling.Id);
        builder.ReplaceSelected(currentSibling with { Title = "Host update" });
        Settle();
        Ok(rowChanges == 0 && ReferenceEquals(layerList.SelectedItem, selectedLayer)
            && builder.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Host update"), $"paper title edits update the selected layer label without collection or selection churn (changes {rowChanges}, selected {ReferenceEquals(layerList.SelectedItem, selectedLayer)}, label {builder.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Host update")})");
        Ok(width.Box.IsFocused && width.Text == "bad" && width.Error != null && builder.GetVisualDescendants().Contains(width), "paper host edits retain the inspector's focused invalid draft");
        Ok(ReferenceEquals(layerList.ItemsSource, rowsSource) && ReferenceEquals(layerList.Items[layerList.Items.Count - 1], lastRow), "paper layer updates retain the observable source and unchanged row objects");
        updates = builder.DesignerUpdateCount;
        builder.MoveLayers(new[] { sibling.Id }, untouched.Id, 0);
        Ok(builder.DesignerUpdateCount == updates + 1 && ReferenceEquals(builder.Definition.Pages[1], second)
            && ReferenceEquals(builder.Definition.Pages[0].Structures.Single(s => s.Id == untouched.Id), untouched), "layer reorder copies only changed paths and flushes once");
        void OneUpdate(Action action, string name)
        {
            int before = builder.DesignerUpdateCount; action();
            Ok(builder.DesignerUpdateCount == before + 1, "paper " + name + " flushes exactly once before returning");
        }
        OneUpdate(builder.Undo, "undo"); OneUpdate(builder.Redo, "redo");
        OneUpdate(builder.AddPage, "add page"); OneUpdate(() => builder.RemovePage(2), "remove page");
        window.Content = null;
        values.Single(v => v.Key == "sibling").Text = "While detached";
        prepared = builder.DrawingPreparationCount;
        window.Content = builder; Settle();
        Ok(builder.DrawingPreparationCount - prepared >= PaperFormView.All(builder.Definition.Pages.SelectMany(p => p.Structures)).Count(), "reattaching paper pages refreshes values changed while subscriptions were detached");
        window.Close();

        var drawingBuilder = new PaperFormBuilder { Definition = PaperScaleDefinition(200, 2) };
        var drawingWindow = new Window { Width = 1400, Height = 1000, Content = drawingBuilder }; drawingWindow.Show(); Settle();
        var field = drawingBuilder.Definition.Pages[0].Structures.First(s => s.Kind == PaperStructureKind.Text);
        int drawn = drawingBuilder.DrawnFieldCount;
        drawingBuilder.Values.Single(v => v.Key == field.FieldKey).Text = "Typed"; Settle();
        Ok(drawingBuilder.DrawnFieldCount > drawn && drawingBuilder.DrawnFieldCount - drawn <= 64, "typing repaints a bounded drawing band instead of the whole paper page");
        drawingBuilder.SelectedId = field.Id; Settle(); drawn = drawingBuilder.DrawnFieldCount;
        drawingBuilder.ReplaceSelected(field with { Title = "Renamed" }); Settle();
        Ok(drawingBuilder.DrawnFieldCount > drawn && drawingBuilder.DrawnFieldCount - drawn <= 64, "a paper title edit preserves the retained drawings of unchanged bands");
        drawingWindow.Close();

        var zoomBuilder = new PaperFormBuilder { Definition = new() { Pages = new[] { second } }, Zoom = 2 };
        var zoomWindow = new Window { Width = 1400, Height = 1000, Content = zoomBuilder }; zoomWindow.Show(); Settle();
        var onPage = new Point(40 * 96 / 25.4, 50 * 96 / 25.4);
        var anchor = zoomBuilder.Page.TranslatePoint(onPage, zoomWindow)!.Value;
        int commits = zoomBuilder.ZoomLayoutCommitCount;
        for (int i = 0; i < 4; i++) zoomWindow.MouseWheel(anchor, new Vector(0, 1), RawInputModifiers.Control);
        Ok(zoomBuilder.ZoomLayoutCommitCount == commits && zoomBuilder.Zoom > 2, "paper wheel bursts render-scale without committing field layout");
        var movedAnchor = zoomBuilder.Page.TranslatePoint(onPage, zoomWindow)!.Value;
        Ok(Math.Abs(movedAnchor.X - anchor.X) < 2 && Math.Abs(movedAnchor.Y - anchor.Y) < 2, $"transient paper zoom preserves the pointer anchor ({anchor} -> {movedAnchor})");
        using (var idle = new System.Threading.CancellationTokenSource(220)) Avalonia.Threading.Dispatcher.UIThread.MainLoop(idle.Token);
        Settle();
        Ok(zoomBuilder.ZoomLayoutCommitCount == commits + 1, $"paper wheel burst commits one sharp layout after idle ({commits} -> {zoomBuilder.ZoomLayoutCommitCount})");
        zoomWindow.MouseWheel(anchor, new Vector(0, 1), RawInputModifiers.Control);
        commits = zoomBuilder.ZoomLayoutCommitCount;
        zoomBuilder.Mode = PaperFormMode.Preview;
        Ok(zoomBuilder.ZoomLayoutCommitCount == commits + 1, "leaving paper Design commits transient zoom immediately");
        zoomWindow.Close();

        var random = new Random(115);
        var items = Enumerable.Range(0, 60).Select(i => new PaperStructure
        {
            Kind = "check",
            FieldKey = "field" + i,
            Bounds = new(10 + random.Next(100), 10 + random.Next(100), 4 + random.Next(12), 4 + random.Next(12))
        }).ToArray();
        var view = new PaperFormView { Definition = new() { Pages = new[] { new PaperPage { Structures = items } } } };
        var expected = items.SelectMany((item, i) => items.Take(i).Where(other =>
                item.Bounds.X < other.Bounds.X + other.Bounds.Width && item.Bounds.X + item.Bounds.Width > other.Bounds.X
                && item.Bounds.Y < other.Bounds.Y + other.Bounds.Height && item.Bounds.Y + item.Bounds.Height > other.Bounds.Y)
            .Select(other => (item.Id, message: $"“{item.FieldKey}” overlaps “{other.FieldKey}”."))).ToArray();
        Ok(view.Problems.Select(p => (p.StructureId, p.Message)).SequenceEqual(expected), "paper overlap sweep matches the original pair order on a mixed layout");
        var renamed = items.ToArray(); renamed[0] = renamed[0] with { Title = "Renamed" };
        view.Definition = view.Definition with { Pages = new[] { new PaperPage { Structures = renamed } } };
        var cachedProblems = view.Problems.ToArray(); view.RefreshProblems();
        Ok(view.Problems.SequenceEqual(cachedProblems), "incremental paper problem cache agrees with a full check after a neighbouring label changes");
        var horizontal = items.Select((s, i) => s with { Bounds = new(i * 2, 0, 4, 4) }).ToArray();
        var vertical = items.Select((s, i) => s with { Bounds = new(0, i * 2, 4, 4) }).ToArray();
        foreach (var line in new[] { horizontal, vertical })
        {
            view.Definition = new() { Pages = new[] { new PaperPage { Structures = line } } };
            var overlaps = view.Problems.Where(p => p.Message.Contains("overlaps")).ToArray();
            Ok(overlaps.Length == line.Length - 1 && overlaps.Select(p => p.StructureId).SequenceEqual(line.Skip(1).Select(s => s.Id)), "paper overlap sweeps preserve pair order along either crowded axis");
        }
        var pageOne = new PaperPage { Structures = new[] { horizontal[0] } };
        var pageTwo = new PaperPage { Structures = new[] { vertical[1] } };
        view.Definition = new() { Pages = new[] { pageOne, pageTwo } };
        var firstProblems = view.ProblemsOnPage(0); var secondProblems = view.ProblemsOnPage(1);
        view.RefreshProblems();
        Ok(ReferenceEquals(firstProblems, view.ProblemsOnPage(0)) && ReferenceEquals(secondProblems, view.ProblemsOnPage(1)), "unchanged paper page problem lists retain their identity after a full check");
        view.Definition = view.Definition with { Pages = new[] { pageTwo, pageOne } };
        Ok(view.ProblemsOnPage(0).All(p => p.StructureId == vertical[1].Id) && view.ProblemsOnPage(1).All(p => p.StructureId == horizontal[0].Id)
            && view.Problems.Select(p => p.StructureId).SequenceEqual(new[] { vertical[1].Id, horizontal[0].Id }), "paper problem pages and global ordering follow page reordering");
        view.Definition = view.Definition with { Pages = new[] { pageTwo } };
        Ok(view.Problems.All(p => p.StructureId == vertical[1].Id) && view.ProblemsOnPage(1).Count == 0, "removing a paper page releases its cached problems");
        using var painter = new PaperPainter(view);
        var painted = new PaperStructure { FieldKey = "one", Title = "Shared artwork" };
        var same = painted with { Id = "two", FieldKey = "two", Label = "Another name", Bounds = painted.Bounds with { X = 50, Y = 30 } };
        painter.PrepareDrawing(painted, null); painter.PrepareDrawing(same, null);
        Ok(painter.RecordingCount == 1, "paper fields share recorded artwork across identities, keys, labels and positions");
        painter.PrepareDrawing(painted with { Id = "three", Title = "Another title" }, null);
        painter.PrepareDrawing(painted with { Id = "untitled", Title = "" }, null);
        Ok(painter.RecordingCount == 1, "different paper titles and an untitled field share the same recorded input body");
        painter.Retain(new System.Collections.Generic.HashSet<string> { same.Id });
        painter.PrepareDrawing(same with { Bounds = same.Bounds with { Y = 40 } }, null);
        Ok(painter.RecordingCount == 1, "moving the remaining user retains shared artwork after another field is removed");
        painter.PrepareDrawing(same with { Title = "Renamed title" }, null);
        Ok(painter.RecordingCount == 1, "renaming a paper title keeps the input body's owned glyphs alive");
        painter.PrepareDrawing(same, new PaperFormValue("two") { Text = "Changed" });
        Ok(painter.RecordingCount == 2, "changing a paper value records its new artwork");
        painter.Clear(); painter.PrepareDrawing(same, null);
        Ok(painter.RecordingCount == 3, "clearing shared paper artwork releases body and title recordings together");
    }
}

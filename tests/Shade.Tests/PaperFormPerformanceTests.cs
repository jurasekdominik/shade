using System;
using System.Linq;
using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace Shade.Tests;

static partial class Program
{
    static void PaperFormPerformance()
    {
        var leaf = new PaperStructure { FieldKey = "edited", Bounds = new(2, 2, 20, 8) };
        var sibling = new PaperStructure { FieldKey = "sibling", Bounds = new(2, 20, 20, 8) };
        var group = new PaperStructure { Kind = "group", Bounds = new(10, 10, 60, 60), Children = new[] { leaf, sibling } };
        var untouched = new PaperStructure { Kind = "group", Bounds = new(100, 10, 60, 60), Children = new[] { new PaperStructure { FieldKey = "other", Bounds = new(2, 2, 20, 8) } } };
        var second = new PaperPage { Structures = new[] { new PaperStructure { FieldKey = "second", Bounds = new(10, 10, 20, 8) } } };
        var builder = new PaperFormBuilder { Definition = new() { Pages = new[] { new PaperPage { Structures = new[] { group, untouched } }, second } } };
        var window = new Window { Content = builder, Width = 1400, Height = 1000 }; window.Show(); Settle();
        builder.SelectedId = leaf.Id;
        var original = builder.Definition;
        PaperStructure Current() => builder.Definition.Pages[0].Structures[0].Children.Single(s => s.Id == leaf.Id);
        bool Shared() => ReferenceEquals(builder.Definition.Pages[0].Structures[1], untouched)
            && ReferenceEquals(builder.Definition.Pages[0].Structures[0].Children.Single(s => s.Id == sibling.Id), sibling)
            && ReferenceEquals(builder.Definition.Pages[1], second);
        builder.ReplaceSelected(Current() with { Title = "Changed" });
        Ok(Shared() && Current().Title == "Changed", "paper property edit preserves unrelated nested structures and pages");
        builder.Undo();
        Ok(ReferenceEquals(builder.Definition, original), "paper undo restores the original snapshot by reference");
        builder.Redo();
        Ok(Shared() && Current().Title == "Changed", "paper redo retains shared branches");
        for (int i = 0; i < 10; i++) builder.ReplaceSelected(Current() with { Bounds = Current().Bounds with { Width = 22 + i * 2 } });
        Ok(Shared() && Current().Bounds.Width == 40, "repeated paper resizes copy only the edited path");
        builder.MoveSelection(2, 0);
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
        value.Error = "Correct this"; Settle();
        Ok(publications == 1 && builder.Problems.Any(p => p.StructureId == sibling.Id && p.Message.Contains("Correct this")), "changed paper validation publishes once");
        value.Error = null; Settle();
        Ok(publications == 2, "clearing a paper validation error publishes the recovered problems");
        var values = new ObservableCollection<PaperFormValue>();
        builder.Values = values; Settle();
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
        window.Close();

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
    }
}

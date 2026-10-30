using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;

namespace Shade.Tests;

// Regressions from the paper-forms audit: what a single-page view prepares, what a focus change redoes, the undo limit, guides past the last page.
static partial class Program
{
    static void PaperFormAudit()
    {
        PaperStructure Field(int page, int i) => new() { Kind = "text", FieldKey = $"f{page}_{i}", Bounds = new(10, 10 + i * 12, 60, 8) };
        PaperPage Page(int n, int fields) => new() { Structures = Enumerable.Range(0, fields).Select(i => Field(n, i)).ToArray() };
        var definition = new PaperFormDefinition { Pages = new[] { Page(0, 5), Page(1, 5), Page(2, 5) } };
        var values = new Avalonia.Collections.AvaloniaList<PaperFormValue>(definition.Pages.SelectMany(p => p.Structures).Select((s, i) => new PaperFormValue(s.FieldKey) { Text = "Value " + i }));

        var view = new PaperFormView { Definition = definition, Values = values, Mode = PaperFormMode.Fill };
        var window = new Window { Width = 1000, Height = 1000, Content = view }; window.Show(); Settle();
        Ok(view.DrawingPreparationCount == 5, $"a single-page view prepares only the page it shows ({view.DrawingPreparationCount} of 15 fields)");
        view.PageIndex = 2; Settle();
        Ok(view.DrawingPreparationCount == 10, "turning to another page prepares that page and not the rest");

        int prepared = view.DrawingPreparationCount;
        var ids = view.Definition.Pages[2].Structures.Select(s => s.Id).ToArray();
        view.ActivateEditor(ids[0], NavigationMethod.Tab); Settle();
        values.First(v => v.Key == "f2_0").Text = "Typed while active"; Settle();
        view.ActivateEditor(ids[1], NavigationMethod.Tab); Settle();
        Ok(view.DrawingPreparationCount == prepared && view.ActiveField == ids[1], "moving the live editor to the next field does not re-prepare the page");
        var released = view.DrawnText(ids[0]);
        Ok(released == "Typed while active", $"the field the editor left is drawn again with what was typed ({released})");
        window.Close();

        var builder = new PaperFormBuilder { Definition = definition };
        var host = new Window { Width = 1400, Height = 1000, Content = builder }; host.Show(); Settle();
        builder.SelectedId = builder.Definition.Pages[0].Structures[0].Id;
        for (int i = 0; i < 520; i++) builder.ReplaceSelected(builder.Definition.Pages[0].Structures[0] with { Title = "Edit " + i });
        int steps = 0; while (builder.CanUndo && steps < 1000) { builder.Undo(); steps++; }
        Ok(steps == 500, $"undo keeps the last 500 edits, not every edit of the session ({steps})");

        builder.SetCurrentValue(PaperFormView.PageIndexProperty, 9);
        bool added;
        try { builder.AddGuide(PaperGuideOrientation.Vertical, 30); added = true; } catch (ArgumentOutOfRangeException) { added = false; } catch (IndexOutOfRangeException) { added = false; }
        Ok(added && builder.Definition.Pages[^1].Guides.Count == 1, "a guide added while PageIndex points past the last page lands on the last page");
        host.Close();
    }
}

using System;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Shade.Tests;

static partial class Program
{
    // `--paper-scale [fields per page] [pages]`: how a large form loads, edits and validates (headless, no rendering).
    static int PaperScale(int perPage, int pages)
    {
        static PaperStructure Field(int page, int i) => new()
        {
            Kind = i % 5 == 0 ? PaperStructureKind.Characters : i % 5 == 1 ? PaperStructureKind.Check : PaperStructureKind.Text,
            FieldKey = $"p{page}f{i}",
            Title = $"Field {i}",
            CellCount = 10,
            CellWidth = 4,
            Bounds = new(10 + i % 4 * 46, 14 + i / 4 * 12 % 260, i % 5 == 1 ? 4 : 40, i % 5 == 1 ? 4 : 8)
        };
        var definition = new PaperFormDefinition { Pages = Enumerable.Range(0, pages).Select(p => new PaperPage { Structures = Enumerable.Range(0, perPage).Select(i => Field(p, i)).ToArray() }).ToArray() };
        long Time(Action action) { var watch = Stopwatch.StartNew(); action(); Dispatcher.UIThread.RunJobs(); return watch.ElapsedMilliseconds; }
        PaperFormBuilder builder = null!; Window window = null!;
        long load = Time(() => { builder = new PaperFormBuilder { Definition = definition }; window = new Window { Width = 1400, Height = 1000, Content = builder }; window.Show(); });
        var first = builder.Definition.Pages[0].Structures.First(s => s.Kind == PaperStructureKind.Text);
        builder.SelectedId = first.Id; Dispatcher.UIThread.RunJobs();
        long edit = Time(() => { for (int i = 0; i < 10; i++) builder.ReplaceSelected(builder.Definition.Pages[0].Structures.First(s => s.Id == first.Id) with { Title = "Edited " + i }); }) / 10;
        long move = Time(() => { for (int i = 0; i < 10; i++) builder.MoveSelection(2, 0); }) / 10;
        var value = builder.Values.First(v => v.Key == first.FieldKey);
        long type = Time(() => { for (int i = 0; i < 10; i++) { value.Text = "typed " + i; Dispatcher.UIThread.RunJobs(); } }) / 10;
        long problems = Time(() => builder.RefreshProblems());
        long json = Time(() => PaperFormDefinition.FromJson(builder.Definition.ToJson()));
        builder.Mode = PaperFormMode.Fill; Dispatcher.UIThread.RunJobs();
        long html = Time(() => builder.ExportHtml());
        int visuals = builder.GetVisualDescendants().Count();
        Console.WriteLine($"{perPage} fields x {pages} pages: load {load} ms, inspector edit {edit} ms, move {move} ms, keystroke {type} ms, problems {problems} ms, JSON round trip {json} ms, HTML {html} ms; {visuals} visuals");
        window.Close();
        // budgets for a form of this size on a developer machine; a regression past them is a finding, not noise
        return edit < 400 && move < 400 && type < 100 ? 0 : 1;
    }
}

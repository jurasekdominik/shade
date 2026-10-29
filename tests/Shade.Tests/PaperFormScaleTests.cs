using System;
using System.Diagnostics;
using System.Collections.Generic;
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
        double Time(Action action) { var watch = Stopwatch.StartNew(); action(); Dispatcher.UIThread.RunJobs(); return watch.Elapsed.TotalMilliseconds; }
        PaperFormBuilder builder = null!; Window window = null!;
        double load = Time(() => { builder = new PaperFormBuilder { Definition = definition }; window = new Window { Width = 1400, Height = 1000, Content = builder }; window.Show(); });
        (double ms, int updates) Measure(Action<int> action)
        {
            double total = 0; int maximum = 0;
            for (int i = 0; i < 10; i++)
            {
                int before = builder.DesignerUpdateCount;
                total += Time(() => action(i));
                maximum = Math.Max(maximum, builder.DesignerUpdateCount - before);
            }
            return (total / 10, maximum);
        }
        var first = builder.Definition.Pages[0].Structures.First(s => s.Kind == PaperStructureKind.Text);
        builder.SelectedId = first.Id; Dispatcher.UIThread.RunJobs();
        var edit = Measure(i => builder.ReplaceSelected(builder.Definition.Pages[0].Structures.First(s => s.Id == first.Id) with { Title = "Edited " + i }));
        var move = Measure(_ => builder.MoveSelection(2, 0));
        var value = builder.Values.First(v => v.Key == first.FieldKey);
        var type = Measure(i => value.Text = "typed " + i);
        double problems = Time(() => builder.RefreshProblems());
        double json = Time(() => PaperFormDefinition.FromJson(builder.Definition.ToJson()));
        var snapshots = new List<PaperFormDefinition>();
        long heapBefore = GC.GetTotalMemory(true);
        for (int i = 0; i < 100; i++)
        {
            var current = builder.Definition.Pages[0].Structures.First(s => s.Id == first.Id);
            builder.ReplaceSelected(current with { Bounds = current.Bounds with { Width = 40 + i % 2 * 2 } });
            snapshots.Add(builder.Definition);
        }
        Dispatcher.UIThread.RunJobs();
        long retained = GC.GetTotalMemory(true) - heapBefore;
        IEnumerable<PaperStructure> All(PaperStructure s) => new[] { s }.Concat(s.Children.SelectMany(All));
        int unique = snapshots.SelectMany(d => d.Pages).SelectMany(p => p.Structures).SelectMany(All).Distinct(ReferenceEqualityComparer.Instance).Count();
        builder.Mode = PaperFormMode.Fill; Dispatcher.UIThread.RunJobs();
        double html = Time(() => builder.ExportHtml());
        int visuals = builder.GetVisualDescendants().Count();
        Console.WriteLine(FormattableString.Invariant($"{perPage} fields x {pages} pages: load {load:F2} ms, inspector edit {edit.ms:F2} ms ({edit.updates} updates max), move {move.ms:F2} ms ({move.updates} updates max), keystroke {type.ms:F2} ms ({type.updates} updates max), problems {problems:F2} ms, JSON round trip {json:F2} ms, HTML {html:F2} ms; {visuals} visuals"));
        Console.WriteLine(FormattableString.Invariant($"100 resizes: {retained / 1024.0:F1} KiB managed heap retained ({retained / 100.0:F0} bytes/entry); {unique} distinct structures across snapshots (expected {perPage * pages + 99} with shared unchanged structures). Heap includes UI/history changes; reference count is deterministic."));
        GC.KeepAlive(snapshots); GC.KeepAlive(builder);
        window.Close();
        bool passed = load <= 1500 && edit.ms <= 30 && move.ms <= 30 && type.ms <= 5 && edit.updates <= 1 && move.updates <= 1 && type.updates <= 1 && unique == perPage * pages + 99;
        Console.WriteLine(passed ? "paper scale budgets passed" : "FAIL paper scale budgets: load <= 1500 ms; edit/move <= 30 ms; typing <= 5 ms; <= 1 designer update per operation; only changed structures retained");
        return passed ? 0 : 1;
    }
}

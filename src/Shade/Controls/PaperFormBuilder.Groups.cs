using System;
using System.Collections.Generic;
using System.Linq;
using static Shade.PaperFormDefinition;

namespace Shade;

public sealed partial class PaperFormBuilder
{
    static IReadOnlyList<PaperStructure> MapStructures(IReadOnlyList<PaperStructure> source, Func<PaperStructure, PaperStructure?> map)
    {
        List<PaperStructure>? changed = null;
        for (int i = 0; i < source.Count; i++)
        {
            var next = map(source[i]);
            if (changed == null && !ReferenceEquals(next, source[i])) changed = source.Take(i).ToList();
            if (changed != null && next != null) changed.Add(next);
        }
        return changed == null ? source : changed.ToArray();
    }
    static PaperStructure WithChildren(PaperStructure source, IReadOnlyList<PaperStructure> children) =>
        ReferenceEquals(source.Children, children) ? source : source with { Children = children };
    static IReadOnlyList<PaperStructure> OrderStructures(IReadOnlyList<PaperStructure> items, IReadOnlyDictionary<string, int> order)
    {
        var sorted = items.OrderBy(s => order.GetValueOrDefault(s.Id, int.MaxValue)).ToArray();
        var source = sorted.SequenceEqual(items, ReferenceEqualityComparer.Instance) ? items : sorted;
        return MapStructures(source, s => WithChildren(s, OrderStructures(s.Children, order)));
    }

    double Grid(double value) => Math.Round(value / GridPitch, MidpointRounding.AwayFromZero) * GridPitch;
    // A structure that does not snap keeps its imported size and cell pitch; only its invariants are applied.
    PaperStructure FitGrid(PaperStructure s)
    {
        var b = s.Bounds;
        bool snap = s.SnapToGrid;
        double least = snap ? GridPitch : .1;
        double G(double value) => snap ? Grid(value) : value;
        double cell = Math.Max(least, G(s.CellWidth));
        double width = s.Kind == "characters" ? s.GridAlignment != Avalonia.Media.TextAlignment.Center ? s.CellCount * cell : Math.Max(s.CellCount * cell, G(b.Width)) : Math.Max(least, G(b.Width));
        double height = Math.Max(least, G(b.Height));
        if (s.Kind == "check") width = height = Math.Max(width, height);
        if (KeepsAspect(s) is { } ratio) height = Math.Max(.1, Math.Round(width * ratio, 2));  // a picture keeps its proportions rather than the grid
        if (s.Kind == "group" && s.Children.Count > 0)
        {
            double Reach(double v) => snap ? Math.Ceiling(v / GridPitch) * GridPitch : v;
            width = Math.Max(width, Reach(s.Children.Max(c => c.Bounds.X + c.Bounds.Width)));
            height = Math.Max(height, Reach(s.Children.Max(c => c.Bounds.Y + c.Bounds.Height)));
        }
        // a right-aligned grid keeps its right edge when it shrinks to its cells
        double left = s.Kind == "characters" && s.GridAlignment == Avalonia.Media.TextAlignment.Right ? b.X + b.Width - width : b.X;
        var bounds = new PaperBounds(G(left), G(b.Y), width, height);
        double cellWidth = s.Kind == "characters" ? cell : s.CellWidth;
        return bounds == b && cellWidth == s.CellWidth ? s : s with { CellWidth = cellWidth, Bounds = bounds };
    }
    void NormalizeGrid()
    {
        PaperStructure Normalize(PaperStructure s) => FitGrid(WithChildren(s, MapStructures(s.Children, Normalize)));
        var pages = Definition.Pages.Select(p =>
        {
            var structures = MapStructures(p.Structures, Normalize);
            return ReferenceEquals(structures, p.Structures) ? p : p with { Structures = structures };
        }).ToArray();
        if (pages.Where((p, i) => !ReferenceEquals(p, Definition.Pages[i])).Any())
            Apply(Definition with { Pages = pages });
    }
    PaperStructure[] InsertInto(IEnumerable<PaperStructure> source, PaperStructure item)
    {
        var items = source.ToList();
        int parent = items.FindLastIndex(s => s.Kind == "group" && s.Bounds.Rect.Contains(item.Bounds.Rect));
        if (parent >= 0)
        {
            var group = items[parent];
            var local = item with { Bounds = item.Bounds with { X = item.Bounds.X - group.Bounds.X, Y = item.Bounds.Y - group.Bounds.Y } };
            items[parent] = group with { Children = InsertInto(group.Children, local) };
        }
        else
        {
            if (item.Kind == "group")
            {
                var inside = items.Where(s => item.Bounds.Rect.Contains(s.Bounds.Rect)).ToArray();
                items.RemoveAll(s => inside.Contains(s));
                item = item with { Children = item.Children.Concat(inside.Select(s => s with { Bounds = s.Bounds with { X = s.Bounds.X - item.Bounds.X, Y = s.Bounds.Y - item.Bounds.Y } })).ToArray() };
            }
            items.Add(item);
        }
        return items.ToArray();
    }
    void PlaceSelected(PaperStructure item)
    {
        if (Selected is not { } current || Mode != PaperFormMode.Design) return;
        if (item.Kind == "characters" && item.GridAlignment != Avalonia.Media.TextAlignment.Center) item = item with { CellCount = Math.Clamp((int)Math.Round(item.Bounds.Width / item.CellWidth), 1, 1000) };
        item = FitGrid(item);
        IReadOnlyList<PaperStructure> Remove(IReadOnlyList<PaperStructure> items) => MapStructures(items, s => s.Id == current.Id ? null : WithChildren(s, Remove(s.Children)));
        var absolute = Placements(Structures).First(p => p.structure.Id == current.Id).bounds;
        if (item == current with { Bounds = absolute }) return;
        var order = All(Structures).Select((s, index) => (s.Id, index)).ToDictionary(p => p.Id, p => p.index);
        Edit(WithStructures(OrderStructures(InsertInto(Remove(Definition.Pages[Math.Min(PageIndex, Definition.Pages.Count - 1)].Structures), item), order)));
    }
}

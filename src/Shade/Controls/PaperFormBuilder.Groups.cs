using System;
using System.Collections.Generic;
using System.Linq;
using static Shade.PaperFormDefinition;

namespace Shade;

public sealed partial class PaperFormBuilder
{
    double Grid(double value) => Math.Round(value / GridPitch, MidpointRounding.AwayFromZero) * GridPitch;
    PaperStructure FitGrid(PaperStructure s)
    {
        var b = s.Bounds;
        double cell = Math.Max(GridPitch, Grid(s.CellWidth));
        double width = s.Kind == "characters" ? s.GridAlignment == Avalonia.Media.TextAlignment.Left ? s.CellCount * cell : Math.Max(s.CellCount * cell, Grid(b.Width)) : Math.Max(GridPitch, Grid(b.Width));
        double height = Math.Max(GridPitch, Grid(b.Height));
        if (s.Kind == "check") width = height = Math.Max(width, height);
        if (KeepsAspect(s) is { } ratio) height = Math.Max(.1, Math.Round(width * ratio, 2));  // a picture keeps its proportions rather than the grid
        if (s.Kind == "group" && s.Children.Count > 0)
        {
            width = Math.Max(width, Math.Ceiling(s.Children.Max(c => c.Bounds.X + c.Bounds.Width) / GridPitch) * GridPitch);
            height = Math.Max(height, Math.Ceiling(s.Children.Max(c => c.Bounds.Y + c.Bounds.Height) / GridPitch) * GridPitch);
        }
        return s with { CellWidth = s.Kind == "characters" ? cell : s.CellWidth, Bounds = new(s.SnapToGrid ? Grid(b.X) : b.X, s.SnapToGrid ? Grid(b.Y) : b.Y, width, height) };
    }
    void NormalizeGrid()
    {
        PaperStructure Normalize(PaperStructure s) => FitGrid(s with { Children = s.Children.Select(Normalize).ToArray() });
        var pages = Definition.Pages.Select(p => p with { Structures = p.Structures.Select(Normalize).ToArray() }).ToArray();
        if (Definition.Pages.SelectMany(p => All(p.Structures)).Zip(pages.SelectMany(p => All(p.Structures))).Any(pair => pair.First.Bounds != pair.Second.Bounds || pair.First.CellWidth != pair.Second.CellWidth))
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
        if (item.Kind == "characters" && item.GridAlignment == Avalonia.Media.TextAlignment.Left) item = item with { CellCount = Math.Clamp((int)Math.Round(item.Bounds.Width / item.CellWidth), 1, 1000) };
        item = FitGrid(item);
        PaperStructure[] Remove(IEnumerable<PaperStructure> items) => items.Where(s => s.Id != current.Id).Select(s => s with { Children = Remove(s.Children) }).ToArray();
        var absolute = Placements(Structures).First(p => p.structure.Id == current.Id).bounds;
        if (item == current with { Bounds = absolute }) return;
        var order = All(Structures).Select((s, index) => (s.Id, index)).ToDictionary(p => p.Id, p => p.index);
        PaperStructure[] Ordered(IEnumerable<PaperStructure> items) => items.OrderBy(s => order.GetValueOrDefault(s.Id, int.MaxValue)).Select(s => s with { Children = Ordered(s.Children) }).ToArray();
        Edit(WithStructures(Ordered(InsertInto(Remove(Structures), item))));
    }
}

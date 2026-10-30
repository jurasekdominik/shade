using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using static Shade.PaperFormDefinition;

namespace Shade;

public sealed partial class PaperFormBuilder
{
    public static readonly StyledProperty<IReadOnlyList<string>> SelectedIdsProperty = AvaloniaProperty.Register<PaperFormBuilder, IReadOnlyList<string>>(nameof(SelectedIds), Array.Empty<string>(), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay, validate: ids => ids != null && ids.Count <= 10000 && ids.All(id => id != null) && ids.Distinct(StringComparer.Ordinal).Count() == ids.Count);
    public IReadOnlyList<string> SelectedIds { get => GetValue(SelectedIdsProperty); set => SetValue(SelectedIdsProperty, value); }
    bool updatingSelection;
    void SetSelection(IEnumerable<string> ids, int? page = null)
    {
        using var update = new UpdateScope(this);
        var selected = ids.Distinct(StringComparer.Ordinal).ToArray();
        if (page is { } active)
        {
            var local = All(Definition.Pages[active].Structures).Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
            selected = selected.Where(local.Contains).Concat(selected.Where(id => !local.Contains(id))).ToArray();
        }
        updatingSelection = true;
        try
        {
            if (page is { } index) SetCurrentValue(PageIndexProperty, index);
            if (!SelectedIds.SequenceEqual(selected)) SetCurrentValue(SelectedIdsProperty, selected);
            SetCurrentValue(SelectedIdProperty, selected.FirstOrDefault());
        }
        finally { updatingSelection = false; }
        if (ready) { BuildInspector(); Update(); }
    }
    void SelectionChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (updatingSelection) return;
        if (change.Property == SelectedIdProperty) SetSelection(SelectedId == null ? Array.Empty<string>() : new[] { SelectedId });
        else if (change.Property == SelectedIdsProperty) SetSelection(SelectedIds);
        else if (change.Property == DefinitionProperty)
        {
            var known = All(Definition.Pages.SelectMany(p => p.Structures)).Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
            if (SelectedIds.Any(id => !known.Contains(id))) SetSelection(SelectedIds.Where(known.Contains));
        }
    }
    IReadOnlyList<(PaperStructure structure, PaperBounds bounds)> SelectedRoots(IEnumerable<PaperStructure> items)
    {
        var placements = Placements(items).ToArray(); var selected = SelectedIds.ToHashSet(StringComparer.Ordinal);
        var children = placements.Where(p => selected.Contains(p.structure.Id)).SelectMany(p => All(p.structure.Children)).Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        return placements.Where(p => selected.Contains(p.structure.Id) && !children.Contains(p.structure.Id)).ToArray();
    }
    void SelectCanvas(PaperStructure? hit, KeyModifiers modifiers)
    {
        if (hit == null) { if (!modifiers.HasFlag(KeyModifiers.Control) && !modifiers.HasFlag(KeyModifiers.Shift)) SetSelection(Array.Empty<string>()); return; }
        if (modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Shift))
            SetSelection(SelectedIds.Contains(hit.Id) ? SelectedIds.Where(id => id != hit.Id) : SelectedIds.Append(hit.Id));
        else if (!SelectedIds.Contains(hit.Id)) SetSelection(new[] { hit.Id });
    }
    (PaperStructure structure, PaperBounds bounds) HitCanvas(Point point, bool deepest)
    {
        var placements = Placements(Structures).ToArray();
        if (deepest) return placements.Reverse().FirstOrDefault(p => p.bounds.Rect.Contains(point));
        var current = placements.FirstOrDefault(p => p.structure.Id == SelectedId);
        if (SelectedIds.Count == 1 && current.structure?.Kind == "group" && current.bounds.Rect.Contains(point))
        {
            var child = Placements(current.structure.Children, current.bounds.X, current.bounds.Y).Reverse().FirstOrDefault(p => p.bounds.Rect.Contains(point));
            if (child.structure != null) return child;
        }
        foreach (var root in Structures.Reverse())
        {
            if (!root.Bounds.Rect.Contains(point)) continue;
            if (root.Kind == "group" && All(root.Children).Any(s => SelectedIds.Contains(s.Id)))
            {
                var child = Placements(root.Children, root.Bounds.X, root.Bounds.Y).Reverse().FirstOrDefault(p => p.bounds.Rect.Contains(point));
                if (child.structure != null) return child;
            }
            return (root, root.Bounds);
        }
        return default;
    }
    void SetSelectionSnapping(bool enabled)
    {
        var selected = SelectedIds.ToHashSet(StringComparer.Ordinal);
        PaperStructure Set(PaperStructure s)
        {
            var updated = WithChildren(s, MapStructures(s.Children, Set));
            return selected.Contains(s.Id) ? FitGrid(updated with { SnapToGrid = enabled }) : updated;
        }
        Edit(Definition with
        {
            Pages = Definition.Pages.Select(p =>
        {
            var structures = MapStructures(p.Structures, Set);
            return ReferenceEquals(structures, p.Structures) ? p : p with { Structures = structures };
        }).ToArray()
        });
    }
    /// <summary>Move the selected roots together in millimetres, preserving group offsets and assigning containing groups.</summary>
    public void MoveSelection(double x, double y)
    {
        using var update = new UpdateScope(this);
        if (Mode != PaperFormMode.Design || SelectedIds.Count == 0) return;
        if (Definition.Pages.Any(p => SelectedRoots(p.Structures).Any(s => s.structure.SnapToGrid))) { x = Grid(x); y = Grid(y); }
        if (x == 0 && y == 0) return;
        var pages = Definition.Pages.Select(page =>
        {
            var roots = SelectedRoots(page.Structures); var ids = roots.Select(p => p.structure.Id).ToHashSet(StringComparer.Ordinal);
            if (roots.Count == 0) return page;
            IReadOnlyList<PaperStructure> Remove(IReadOnlyList<PaperStructure> items) => MapStructures(items, s => ids.Contains(s.Id) ? null : WithChildren(s, Remove(s.Children)));
            var order = All(page.Structures).Select((s, index) => (s.Id, index)).ToDictionary(p => p.Id, p => p.index);
            var remaining = Remove(page.Structures);
            foreach (var (s, b) in roots) remaining = InsertInto(remaining, s with { Bounds = b with { X = b.X + x, Y = b.Y + y } });
            return page with { Structures = OrderStructures(remaining, order) };
        }).ToArray();
        var selected = SelectedIds.ToArray(); Edit(Definition with { Pages = pages }); SetSelection(selected);
    }
}

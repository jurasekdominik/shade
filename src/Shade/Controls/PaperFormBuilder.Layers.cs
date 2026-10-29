using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using static Shade.Look;

namespace Shade;

public sealed partial class PaperFormBuilder
{
    Point layerDragStart;
    bool draggingLayers;
    string[] draggedLayers = Array.Empty<string>();
    Layer? layerDragOrigin;
    readonly Canvas layerDropCanvas = new() { IsHitTestVisible = false, ClipToBounds = true };
    readonly Border layerDropLine = new() { Background = B(FocusBorder), Height = M(2), IsVisible = false };
    readonly Border layerDropInside = new() { Background = B(AccentTint), BorderBrush = B(FocusBorder), BorderThickness = new Thickness(1), IsVisible = false };
    void ConfigureLayerDragging()
    {
        layerDropCanvas.Children.Add(layerDropInside); layerDropCanvas.Children.Add(layerDropLine);
        AutomationProperties.SetName(layerDropLine, T("Layer insertion position")); AutomationProperties.SetName(layerDropInside, T("Move into group"));
        layers.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (!e.GetCurrentPoint(layers).Properties.IsLeftButtonPressed || Mode != PaperFormMode.Design) return;
            var row = (e.Source as Visual)?.GetSelfAndVisualAncestors().OfType<ListBoxItem>().FirstOrDefault();
            layerDragOrigin = row?.Content as Layer ?? row?.DataContext as Layer;
            layerDragStart = e.GetPosition(layers); draggingLayers = false;
            draggedLayers = layerDragOrigin?.Id is { } id && SelectedIds.Contains(id) ? SelectedIds.ToArray() : layerDragOrigin?.Id is { } one ? new[] { one } : Array.Empty<string>();
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        layers.AddHandler(PointerMovedEvent, (_, e) =>
        {
            if (draggedLayers.Length == 0) return;
            if (!draggingLayers && Math.Abs(e.GetPosition(layers).X - layerDragStart.X) + Math.Abs(e.GetPosition(layers).Y - layerDragStart.Y) < M(4)) return;
            draggingLayers = true; e.Pointer.Capture(layers); SetSelection(draggedLayers);
            var row = LayerAt(e.GetPosition(layers));
            ShowLayerDrop(row, row == null ? 0 : e.GetPosition(row).Y);
            if (e.GetPosition(layers).Y < M(24)) row?.BringIntoView();
            else if (e.GetPosition(layers).Y > layers.Bounds.Height - M(24)) row?.BringIntoView();
            e.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        layers.AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (!draggingLayers) { draggedLayers = Array.Empty<string>(); return; }
            var row = LayerAt(e.GetPosition(layers));
            var (target, inside, before) = DropDetails(row, row == null ? 0 : e.GetPosition(row).Y); var ids = draggedLayers;
            CancelLayerDrag(); e.Pointer.Capture(null);
            if (target != null) MoveLayers(ids, target.Id, target.Page, inside, before);
            e.Handled = true;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        layers.PointerCaptureLost += (_, _) => CancelLayerDrag();
        layers.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { CancelLayerDrag(); e.Handled = true; }
            if (e.KeyModifiers.HasFlag(KeyModifiers.Alt) && e.Key is Key.Up or Key.Down)
            {
                MoveLayerStep(e.Key == Key.Up); e.Handled = true;
            }
        };
        ToolTip.SetTip(layers, T("Drag layers to reorder. Drop in the middle of a group to move inside it. Alt+Up/Down changes layer order."));
    }
    ListBoxItem? LayerAt(Point point) => layers.GetVisualDescendants().OfType<ListBoxItem>().FirstOrDefault(row => row.TranslatePoint(default, layers) is { } at && new Rect(at, row.Bounds.Size).Contains(point));
    (Layer? target, bool inside, bool before) DropDetails(ListBoxItem? row, double y)
    {
        var target = row?.Content as Layer ?? row?.DataContext as Layer;
        bool inside = target != null && (target.Id == null || All(Definition.Pages[target.Page].Structures).Any(s => s.Id == target.Id && s.Kind == "group") && y > row!.Bounds.Height / 4 && y < row.Bounds.Height * .75);
        return (target, inside, row != null && y < row.Bounds.Height / 2);
    }
    void ShowLayerDrop(ListBoxItem? row, double y)
    {
        layerDropLine.IsVisible = false; layerDropInside.IsVisible = false;
        var (target, inside, before) = DropDetails(row, y);
        if (row == null || target == null) return;
        if (target.Id != null && All(Definition.Pages.SelectMany(p => p.Structures)).Where(s => draggedLayers.Contains(s.Id)).SelectMany(s => All(new[] { s })).Any(s => s.Id == target.Id)) return;
        if (row.TranslatePoint(default, layerDropCanvas) is not { } at) return;
        double indent = M(8 + (target.Depth + (inside ? 1 : 0)) * 16);
        layerDropLine.Width = Math.Max(0, row.Bounds.Width - indent); layerDropLine.IsVisible = true;
        Canvas.SetLeft(layerDropLine, at.X + indent); Canvas.SetTop(layerDropLine, at.Y + (inside || !before ? row.Bounds.Height : 0) - M(1));
        if (inside)
        {
            layerDropInside.Width = row.Bounds.Width; layerDropInside.Height = row.Bounds.Height; layerDropInside.IsVisible = true;
            Canvas.SetLeft(layerDropInside, at.X); Canvas.SetTop(layerDropInside, at.Y);
        }
    }
    void CancelLayerDrag()
    {
        layerDropLine.IsVisible = false; layerDropInside.IsVisible = false;
        draggingLayers = false; draggedLayers = Array.Empty<string>(); layerDragOrigin = null;
    }
    void MoveLayerStep(bool up)
    {
        if (Selected is not { } selected) return;
        PaperStructure? parent = All(Structures).LastOrDefault(s => s.Children.Any(c => c.Id == selected.Id));
        var siblings = parent?.Children ?? Structures.ToArray(); int index = siblings.ToList().FindIndex(s => s.Id == selected.Id), target = index + (up ? 1 : -1);
        while (target >= 0 && target < siblings.Count && SelectedIds.Contains(siblings[target].Id)) target += up ? 1 : -1;
        if (target >= 0 && target < siblings.Count) MoveLayers(SelectedIds, siblings[target].Id, PageIndex, false, up);
    }
    /// <summary>Move layers before/after a displayed row, or inside a group/page. Coordinates, keys and values are retained.</summary>
    public void MoveLayers(IReadOnlyList<string> ids, string? targetId, int pageIndex, bool inside = false, bool before = true)
    {
        using var update = new UpdateScope(this);
        if (Mode != PaperFormMode.Design || pageIndex < 0 || pageIndex >= Definition.Pages.Count || ids.Count == 0) return;
        var selected = ids.ToHashSet(StringComparer.Ordinal);
        var all = Definition.Pages.SelectMany(p => Placements(p.Structures)).ToArray();
        var nested = all.Where(p => selected.Contains(p.structure.Id)).SelectMany(p => All(p.structure.Children)).Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        var moving = all.Where(p => selected.Contains(p.structure.Id) && !nested.Contains(p.structure.Id)).ToArray();
        if (moving.Length == 0 || targetId != null && (selected.Contains(targetId) || nested.Contains(targetId))) return;
        var targetPlacement = Placements(Definition.Pages[pageIndex].Structures).FirstOrDefault(p => p.structure.Id == targetId);
        if (targetId != null && targetPlacement.structure == null || inside && targetId != null && targetPlacement.structure.Kind != "group") return;
        string? parentId = inside ? targetId : targetId == null ? null : All(Definition.Pages[pageIndex].Structures).LastOrDefault(s => s.Children.Any(c => c.Id == targetId))?.Id;
        var parentBounds = all.FirstOrDefault(p => p.structure.Id == parentId).bounds;
        double x = parentBounds?.X ?? 0, y = parentBounds?.Y ?? 0;
        var block = moving.Select(p => p.structure with { Bounds = p.bounds with { X = p.bounds.X - x, Y = p.bounds.Y - y } }).ToArray();
        var movingIds = moving.Select(p => p.structure.Id).ToHashSet(StringComparer.Ordinal);
        IReadOnlyList<PaperStructure> Remove(IReadOnlyList<PaperStructure> items) => MapStructures(items, s => movingIds.Contains(s.Id) ? null : WithChildren(s, Remove(s.Children)));
        PaperStructure[] Insert(IEnumerable<PaperStructure> items)
        {
            var list = items.ToList(); int index = targetId == null || inside ? list.Count : list.FindIndex(s => s.Id == targetId) + (before ? 1 : 0);
            list.InsertRange(Math.Clamp(index, 0, list.Count), block); return list.ToArray();
        }
        PaperStructure Replace(PaperStructure s) => s.Id == parentId ? s with { Children = Insert(s.Children) } : WithChildren(s, MapStructures(s.Children, Replace));
        var pages = Definition.Pages.Select(p =>
        {
            var structures = Remove(p.Structures);
            return ReferenceEquals(structures, p.Structures) ? p : p with { Structures = structures };
        }).ToArray();
        pages[pageIndex] = pages[pageIndex] with { Structures = parentId == null ? Insert(pages[pageIndex].Structures) : MapStructures(pages[pageIndex].Structures, Replace) };
        Edit(Definition with { Pages = pages }); SetSelection(moving.Select(p => p.structure.Id), pageIndex);
    }
}

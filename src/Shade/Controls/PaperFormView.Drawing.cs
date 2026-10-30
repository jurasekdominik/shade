using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Threading;
using static Shade.PaperFormDefinition;

namespace Shade;

public partial class PaperFormView
{
    // Test reference: false builds one native control per field (what the pixel comparisons measure the drawn page against). The product always draws.
    bool? drawPages;
    internal bool DrawPages => drawPages ?? true;
    PaperPainter? painter;
    readonly Dictionary<Canvas, List<PaperPageVisual>> drawnPages = new();
    readonly Dictionary<Canvas, (IReadOnlyList<PaperStructure> structures, PaperPlacement[] placements)> pageLayouts = new();
    readonly Dictionary<int, IReadOnlyList<PaperStructure>> preparedPages = new();
    internal int DrawingPreparationCount { get; private set; }
    internal int DrawnFieldCount { get; private set; }
    internal string? DrawnText(string id) => painter?.DrawnText(id);
    object? drawingAppearance;
    bool drawingsPending, preparingDrawings, prepareScheduled;
    internal void UsePainter(bool? enabled) { changingEditor = true; try { DeactivateEditor(); drawPages = enabled; Refresh(true); } finally { changingEditor = false; } }
    internal void PrepareExportDrawings() => PrepareDrawings(true);
    internal void DisposeExportDrawings() { painter?.Dispose(); painter = null; }
    internal void InvalidateDrawings()
    {
        preparedPages.Clear();
        drawingsPending = true; foreach (var page in drawnPages.Values.SelectMany(p => p)) page.InvalidateVisual();
        if (prepareScheduled) return;
        prepareScheduled = true;
        Dispatcher.UIThread.Post(() => { prepareScheduled = false; PrepareDrawings(); }, DispatcherPriority.Send);
    }
    void PrepareDrawings(bool detached = false)
    {
        if (!DrawPages || painter == null || !painter.Attached && !detached || preparingDrawings) return;
        var appearance = (Definition.Background, Definition.Foreground, Mode, Avalonia.Layout.LayoutHelper.GetLayoutScale(painter.Templates), painter.Templates.UseLayoutRounding, IsEffectivelyEnabled, FlowDirection);
        if (!Equals(drawingAppearance, appearance)) { painter.Clear(); drawingAppearance = appearance; InvalidateDrawings(); }
        if (!drawingsPending) return;
        preparingDrawings = true; drawingsPending = false;
        try
        {
            for (int i = 0; i < Definition.Pages.Count; i++)
            {
                if (!PageShown(i)) { preparedPages.Remove(i); continue; }
                var structures = Definition.Pages[i].Structures;
                if (!preparedPages.TryGetValue(i, out var previous) || !ReferenceEquals(previous, structures))
                {
                    foreach (var s in All(structures))
                        if (DrawStructure(s)) { DrawingPreparationCount++; painter.PrepareDrawing(s, Value(s), s.Id == hoveredField); }
                    preparedPages[i] = structures;
                }
                painter.PreparePageNumber(i);
            }
            foreach (int stale in preparedPages.Keys.Where(i => i >= Definition.Pages.Count).ToArray()) preparedPages.Remove(stale);
        }
        finally { preparingDrawings = false; }
    }
    void PrepareChangedDrawings(IReadOnlyList<string> keys)
    {
        if (!DrawPages || painter == null) return;
        if (!drawingsPending && painter.Attached && !preparingDrawings)
        {
            preparingDrawings = true;
            try
            {
                foreach (string key in keys)
                    if (sitesByKey.TryGetValue(key, out var site) && PageShown(site.Page) && DrawStructure(site.Structure)) painter.PrepareDrawing(site.Structure, Value(site.Structure), site.Structure.Id == hoveredField);
            }
            finally { preparingDrawings = false; }
            var ids = keys.Where(sitesByKey.ContainsKey).Select(key => sitesByKey[key].Structure.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var page in drawnPages.Values.SelectMany(p => p)) page.InvalidateFields(ids);
        }
        else { InvalidateDrawings(); PrepareDrawings(); }
    }
    /// <summary>A single-page view draws (and keeps recordings for) the page it shows; the builder shows every page.</summary>
    bool PageShown(int index) => ShowAllPages || index == Math.Min(PageIndex, Definition.Pages.Count - 1);
    /// <summary>After a field stops being the live editor, draw it again with the value it now has.</summary>
    void PrepareReleased(string? id)
    {
        if (id == null || painter == null || !fieldIndex.TryGetValue(id, out var field) || !PageShown(field.Page) || !DrawStructure(field.Structure)) return;
        painter.PrepareDrawing(field.Structure, Value(field.Structure), id == hoveredField);
        foreach (var band in drawnPages.Values.SelectMany(p => p)) band.InvalidateFields(new HashSet<string> { id });
    }
    /// <summary>Focus moved between fields: the document did not change, so only the drawing bands, the live editor and the automation tree follow.</summary>
    void RefreshEditor(string? released)
    {
        if (refreshing || scaled == null || Values == null) return;
        refreshing = true;
        try { RefreshDrawings(retain: false); PlaceControls(); PrepareReleased(released); PrepareDrawings(); RefreshAutomation(); }
        finally { refreshing = false; }
    }
    bool DrawStructure(PaperStructure structure) => BuiltIn(structure.Kind) && FindTemplate(structure.Kind) == null && structure.Id != activeField;
    internal readonly record struct PaperPlacement(PaperStructure Structure, PaperBounds Bounds, int Order);
    internal static PaperPlacement[] LayoutPage(PaperPage page)
    {
        var result = new List<PaperPlacement>();
        void Walk(IReadOnlyList<PaperStructure> items, double x, double y)
        {
            foreach (var s in items)
            {
                var bounds = s.Bounds with { X = x + s.Bounds.X, Y = y + s.Bounds.Y };
                result.Add(new(s, bounds, result.Count));
                if (s.Kind == PaperStructureKind.Group) Walk(s.Children, bounds.X, bounds.Y);
            }
        }
        Walk(page.Structures, 0, 0); return result.ToArray();
    }
    void RefreshDrawings(bool retain = true)
    {
        if (!DrawPages)
        {
            foreach (var page in drawnPages) foreach (var band in page.Value) page.Key.Children.Remove(band);
            drawnPages.Clear(); pageLayouts.Clear(); preparedPages.Clear();
            indexedPages = null; fieldIndex.Clear(); tabFields = Array.Empty<PaperField>(); ResetRetiredPeers(); UpdateTabStops();
            painter?.Dispose(); painter = null; return;
        }
        painter ??= new PaperPainter(this);
        if (painter.Templates.Parent != pageStack) pageStack.Children.Add(painter.Templates);
        drawingsPending = true;
        foreach (var stale in drawnPages.Keys.Where(p => !Pages.Contains(p)).ToArray()) { drawnPages.Remove(stale); pageLayouts.Remove(stale); }
        int order = 0;
        for (int i = 0; i < Pages.Count; i++)
        {
            var page = Pages[i];
            if (!drawnPages.TryGetValue(page, out var bands)) drawnPages[page] = bands = new();
            if (!pageLayouts.TryGetValue(page, out var layout) || !ReferenceEquals(layout.structures, Definition.Pages[i].Structures))
                pageLayouts[page] = layout = (Definition.Pages[i].Structures, LayoutPage(Definition.Pages[i]));
            var ranges = new List<(int start, int end)>(); int start = 0;
            void AddRange(int from, int to)
            {
                // Keep a field edit from rebuilding every drawing command on a large page.
                do { int end = Math.Min(to, from + 64); ranges.Add((from, end)); from = end; } while (from < to);
            }
            for (int j = 0; j < layout.placements.Length; j++)
            {
                if (DrawStructure(layout.placements[j].Structure)) continue;
                if (start < j) AddRange(start, j); start = j + 1;
            }
            if (start < layout.placements.Length || ranges.Count == 0 || Definition.ShowPageNumbers) AddRange(start, layout.placements.Length);
            while (bands.Count > ranges.Count) { page.Children.Remove(bands[^1]); bands.RemoveAt(bands.Count - 1); }
            while (bands.Count < ranges.Count) { var visual = new PaperPageVisual(this, painter) { IsHitTestVisible = false }; bands.Add(visual); page.Children.Add(visual); }
            for (int j = 0; j < bands.Count; j++)
            {
                var visual = bands[j]; var range = ranges[j]; visual.ZIndex = (ShowAllPages ? order : 0) + range.start;
                visual.Width = page.Width; visual.Height = page.Height; visual.Refresh(layout.placements, range.start, range.end, i, j == bands.Count - 1);
            }
            order += layout.placements.Length;
        }
        IndexFields(); UpdateTabStops();
        if (retain) painter.Retain(All(Definition.Pages.Where((_, i) => PageShown(i)).SelectMany(p => p.Structures)).Select(s => s.Id).ToHashSet(StringComparer.Ordinal));
    }
    sealed class PaperPageVisual(PaperFormView owner, PaperPainter painter) : Control, IRestyle
    {
        PaperPlacement[] placements = Array.Empty<PaperPlacement>();
        int start, end;
        int pageIndex; bool numbers;
        internal void Refresh(PaperPlacement[] next, int from, int to, int page, bool last)
        {
            if (ReferenceEquals(placements, next) && start == from && end == to && pageIndex == page && numbers == last) return;
            bool changed = start != from || end != to || pageIndex != page || numbers != last;
            if (!changed)
                for (int i = from; i < to; i++)
                    if (!ReferenceEquals(placements[i].Structure, next[i].Structure) || placements[i].Bounds != next[i].Bounds) { changed = true; break; }
            placements = next; start = from; end = to; pageIndex = page; numbers = last;
            if (changed) InvalidateVisual();
        }
        internal void InvalidateFields(ISet<string> ids)
        {
            for (int i = start; i < end; i++) if (ids.Contains(placements[i].Structure.Id)) { InvalidateVisual(); return; }
        }
        public void Restyle() { painter.Clear(); owner.InvalidateDrawings(); }
        public override void Render(DrawingContext context)
        {
            owner.DrawnFieldCount += end - start;
            double scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
            double Round(double n) => UseLayoutRounding ? Math.Round(n * scale) / scale : n;
            for (int i = start; i < end; i++)
            {
                var placement = placements[i];
                using var translated = context.PushTransform(Matrix.CreateTranslation(Round(placement.Bounds.X * DipsPerMm), Round(placement.Bounds.Y * DipsPerMm)));
                painter.Draw(context, placement.Structure, owner.Value(placement.Structure), placement.Structure.Id == owner.hoveredField, fieldY: placement.Bounds.Y * DipsPerMm,
                    pageHeightCorrection: Parent is Canvas page ? page.Bounds.Height - page.Height : 0);
            }
            if (numbers) painter.DrawPageNumber(context, pageIndex);
        }
    }
}

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
    bool? drawPages;
    internal bool DrawPages => drawPages ?? Mode != PaperFormMode.Fill;
    PaperPainter? painter;
    readonly Dictionary<Canvas, List<PaperPageVisual>> drawnPages = new();
    readonly Dictionary<Canvas, (IReadOnlyList<PaperStructure> structures, PaperPlacement[] placements)> pageLayouts = new();
    object? drawingAppearance;
    bool drawingsPending, preparingDrawings, prepareScheduled;
    internal void UsePainter(bool enabled) { drawPages = enabled; Refresh(true); }
    void InvalidateDrawings()
    {
        drawingsPending = true; foreach (var page in drawnPages.Values.SelectMany(p => p)) page.InvalidateVisual();
        if (prepareScheduled) return;
        prepareScheduled = true;
        Dispatcher.UIThread.Post(() => { prepareScheduled = false; PrepareDrawings(); }, DispatcherPriority.Send);
    }
    void PrepareDrawings()
    {
        if (!DrawPages || painter == null || !painter.Attached || preparingDrawings) return;
        var appearance = (Definition.Background, Definition.Foreground, Mode, Avalonia.Layout.LayoutHelper.GetLayoutScale(painter.Templates), painter.Templates.UseLayoutRounding, IsEffectivelyEnabled, FlowDirection);
        if (!Equals(drawingAppearance, appearance)) { painter.Clear(); drawingAppearance = appearance; InvalidateDrawings(); }
        if (!drawingsPending) return;
        preparingDrawings = true; drawingsPending = false;
        try
        {
            foreach (var s in All(Definition.Pages.SelectMany(p => p.Structures))) if (DrawStructure(s)) painter.PrepareDrawing(s, Value(s));
            for (int i = 0; i < Definition.Pages.Count; i++) painter.PreparePageNumber(i);
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
                    if (sitesByKey.TryGetValue(key, out var sites)) foreach (var site in sites) if (DrawStructure(site.Structure)) painter.PrepareDrawing(site.Structure, Value(site.Structure));
            }
            finally { preparingDrawings = false; }
            var ids = keys.Where(sitesByKey.ContainsKey).SelectMany(key => sitesByKey[key]).Select(site => site.Structure.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var page in drawnPages.Values.SelectMany(p => p)) page.InvalidateFields(ids);
        }
        else { InvalidateDrawings(); PrepareDrawings(); }
    }
    bool DrawStructure(PaperStructure structure) => BuiltIn(structure.Kind) && FindTemplate(structure.Kind) == null;
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
    void RefreshDrawings()
    {
        if (!DrawPages)
        {
            foreach (var page in drawnPages) foreach (var band in page.Value) page.Key.Children.Remove(band);
            drawnPages.Clear(); pageLayouts.Clear();
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
            for (int j = 0; j < layout.placements.Length; j++)
            {
                if (DrawStructure(layout.placements[j].Structure)) continue;
                if (start < j) ranges.Add((start, j)); start = j + 1;
            }
            if (start < layout.placements.Length || ranges.Count == 0 || Definition.ShowPageNumbers) ranges.Add((start, layout.placements.Length));
            while (bands.Count > ranges.Count) { page.Children.Remove(bands[^1]); bands.RemoveAt(bands.Count - 1); }
            while (bands.Count < ranges.Count) { var visual = new PaperPageVisual(this, painter); bands.Add(visual); page.Children.Add(visual); }
            for (int j = 0; j < bands.Count; j++)
            {
                var visual = bands[j]; var range = ranges[j]; visual.ZIndex = (ShowAllPages ? order : 0) + range.start;
                visual.Width = page.Width; visual.Height = page.Height; visual.Refresh(layout.placements, range.start, range.end, i, j == bands.Count - 1);
            }
            order += layout.placements.Length;
        }
        painter.Retain(All(Definition.Pages.SelectMany(p => p.Structures)).Select(s => s.Id).ToHashSet(StringComparer.Ordinal));
    }
    sealed class PaperPageVisual(PaperFormView owner, PaperPainter painter) : Control, IRestyle
    {
        PaperPlacement[] placements = Array.Empty<PaperPlacement>();
        int start, end;
        int pageIndex; bool numbers;
        internal void Refresh(PaperPlacement[] next, int from, int to, int page, bool last)
        {
            if (ReferenceEquals(placements, next) && start == from && end == to && pageIndex == page && numbers == last) return;
            placements = next; start = from; end = to; pageIndex = page; numbers = last; InvalidateVisual();
        }
        internal void InvalidateFields(ISet<string> ids)
        {
            for (int i = start; i < end; i++) if (ids.Contains(placements[i].Structure.Id)) { InvalidateVisual(); return; }
        }
        public void Restyle() { painter.Clear(); owner.InvalidateDrawings(); }
        public override void Render(DrawingContext context)
        {
            double scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
            double Round(double n) => UseLayoutRounding ? Math.Round(n * scale) / scale : n;
            for (int i = start; i < end; i++)
            {
                var placement = placements[i];
                using var translated = context.PushTransform(Matrix.CreateTranslation(Round(placement.Bounds.X * DipsPerMm), Round(placement.Bounds.Y * DipsPerMm)));
                painter.Draw(context, placement.Structure, owner.Value(placement.Structure), fieldY: placement.Bounds.Y * DipsPerMm,
                    pageHeightCorrection: Parent is Canvas page ? page.Bounds.Height - page.Height : 0);
            }
            if (numbers) painter.DrawPageNumber(context, pageIndex);
        }
    }
}

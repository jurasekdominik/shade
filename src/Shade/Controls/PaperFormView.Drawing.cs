using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using static Shade.PaperFormDefinition;

namespace Shade;

public partial class PaperFormView
{
    internal bool DrawPages;
    PaperPainter? painter;
    readonly Dictionary<Canvas, PaperPageVisual> drawnPages = new();
    internal void UsePainter(bool enabled) { DrawPages = enabled; Refresh(true); }
    void InvalidateDrawings() { foreach (var page in drawnPages.Values) page.InvalidateVisual(); }
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
            foreach (var page in drawnPages) page.Key.Children.Remove(page.Value);
            drawnPages.Clear();
            painter?.Dispose(); painter = null; return;
        }
        painter ??= new PaperPainter(this);
        foreach (var stale in drawnPages.Keys.Where(p => !Pages.Contains(p)).ToArray()) drawnPages.Remove(stale);
        for (int i = 0; i < Pages.Count; i++)
        {
            var page = Pages[i];
            if (!drawnPages.TryGetValue(page, out var visual))
            {
                visual = new PaperPageVisual(this, painter); drawnPages[page] = visual; page.Children.Add(visual);
            }
            visual.Width = page.Width; visual.Height = page.Height; visual.Refresh(Definition.Pages[i]);
        }
        painter.Retain(All(Definition.Pages.SelectMany(p => p.Structures)).Select(s => s.Id).ToHashSet(StringComparer.Ordinal));
    }
    sealed class PaperPageVisual(PaperFormView owner, PaperPainter painter) : Control, IRestyle
    {
        IReadOnlyList<PaperStructure>? structures;
        PaperPlacement[] placements = Array.Empty<PaperPlacement>();
        internal void Refresh(PaperPage page)
        {
            if (ReferenceEquals(structures, page.Structures)) return;
            structures = page.Structures; placements = LayoutPage(page); InvalidateVisual();
        }
        public void Restyle() { painter.Clear(); InvalidateVisual(); }
        public override void Render(DrawingContext context)
        {
            double scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
            double Round(double n) => UseLayoutRounding ? Math.Round(n * scale) / scale : n;
            foreach (var placement in placements)
            {
                if (!BuiltIn(placement.Structure.Kind)) continue;
                using var translated = context.PushTransform(Matrix.CreateTranslation(Round(placement.Bounds.X * DipsPerMm), Round(placement.Bounds.Y * DipsPerMm)));
                painter.Draw(context, placement.Structure, owner.Value(placement.Structure));
            }
        }
    }
}

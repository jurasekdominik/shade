using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using static Shade.PaperFormDefinition;

namespace Shade;

public sealed partial class PaperFormBuilder
{
    public static readonly StyledProperty<bool> SnapToElementsProperty = AvaloniaProperty.Register<PaperFormBuilder, bool>(nameof(SnapToElements), true, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    /// <summary>Snap moving/resizing blocks to other edges/centres and repeated gaps, with visible guides.</summary>
    public bool SnapToElements { get => GetValue(SnapToElementsProperty); set => SetValue(SnapToElementsProperty, value); }

    PaperBounds SnapToNeighbours(PaperBounds draft, PaperStructure moving, bool resize, out IReadOnlyList<(Point a, Point b)> marks)
    {
        var lines = new List<(Point, Point)>(); marks = lines;
        if (!SnapToElements && (!ShowGuides || !Guides.Any())) return draft;
        var excluded = All(SelectedRoots(Structures).Select(p => p.structure)).Select(s => s.Id).Append(moving.Id).ToHashSet();
        var others = Placements(SnapToElements ? Structures : Array.Empty<PaperStructure>()).Where(p => !excluded.Contains(p.structure.Id) && !(p.structure.Kind == "group" && All(p.structure.Children).Any(s => s.Id == moving.Id))).Select(p => p.bounds.Rect).ToArray();
        double tolerance = 6 / (DipsPerMm * Zoom);
        double Axis(bool horizontal)
        {
            double start = horizontal ? draft.X : draft.Y, size = horizontal ? draft.Width : draft.Height;
            double best = resize ? start + size : start, distance = tolerance;
            var guides = new List<(Point, Point)>();
            double L(Rect r) => horizontal ? r.Left : r.Top;
            double R(Rect r) => horizontal ? r.Right : r.Bottom;
            double OtherL(Rect r) => horizontal ? r.Top : r.Left;
            double OtherR(Rect r) => horizontal ? r.Bottom : r.Right;
            double otherStart = horizontal ? draft.Y : draft.X, otherEnd = otherStart + (horizontal ? draft.Height : draft.Width);
            void Candidate(double value, IEnumerable<(Point, Point)> candidateLines)
            {
                if (moving.SnapToGrid && Math.Abs(Grid(value) - value) > .001) return;
                double delta = Math.Abs(value - (resize ? start + size : start));
                if (delta > distance || resize && value <= start) return;
                best = value; distance = delta; guides = candidateLines.ToList();
            }
            (Point, Point) Alignment(double coordinate, Rect r) => horizontal
                ? (new(coordinate, Math.Min(draft.Y, r.Top)), new(coordinate, Math.Max(draft.Y + draft.Height, r.Bottom)))
                : (new(Math.Min(draft.X, r.Left), coordinate), new(Math.Max(draft.X + draft.Width, r.Right), coordinate));
            (Point, Point) Gap(double a, double b, double cross) => horizontal ? (new(a, cross), new(b, cross)) : (new(cross, a), new(cross, b));
            if (ShowGuides)
                foreach (var guide in Guides.Where(g => (g.Orientation == PaperGuideOrientation.Vertical) == horizontal))
                    foreach (double anchor in resize ? new[] { 0d } : new[] { 0d, size / 2, size })
                        Candidate(guide.Position - anchor, new[] { Alignment(guide.Position, new Rect(0, 0, Definition.PageSize.Width, Definition.PageSize.Height)) });
            foreach (var r in others)
                foreach (double edge in new[] { L(r), (L(r) + R(r)) / 2, R(r) })
                    foreach (double anchor in resize ? new[] { 0d } : new[] { 0d, size / 2, size }) Candidate(edge - anchor, new[] { Alignment(edge, r) });
            if (!resize)
            {
                var band = others.Where(r => OtherL(r) < otherEnd && OtherR(r) > otherStart).OrderBy(L).ToArray();
                for (int i = 1; i < band.Length; i++)
                {
                    var a = band[i - 1]; var b = band[i]; double gap = L(b) - R(a);
                    if (gap < 0) continue;
                    double cross = (Math.Max(OtherL(a), OtherL(b)) + Math.Min(OtherR(a), OtherR(b))) / 2;
                    var existing = Gap(R(a), L(b), cross);
                    Candidate(R(b) + gap, new[] { existing, Gap(R(b), R(b) + gap, (otherStart + otherEnd) / 2) });
                    Candidate(L(a) - gap - size, new[] { existing, Gap(L(a) - gap, L(a), (otherStart + otherEnd) / 2) });
                    if (gap >= size)
                    {
                        double left = R(a) + (gap - size) / 2;
                        Candidate(left, new[] { Gap(R(a), left, cross), Gap(left + size, L(b), cross) });
                    }
                }
            }
            if (distance < tolerance) lines.AddRange(guides);
            return resize ? best - start : best;
        }
        double x = Axis(true), y = Axis(false);
        return resize ? draft with { Width = x, Height = y } : draft with { X = x, Y = y };
    }
}

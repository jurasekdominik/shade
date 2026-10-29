using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using static Shade.Look;
using static Shade.PaperFormDefinition;

namespace Shade;

public sealed partial class PaperFormBuilder
{
    public static readonly StyledProperty<bool> ShowRulersProperty = AvaloniaProperty.Register<PaperFormBuilder, bool>(nameof(ShowRulers), true, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public static readonly StyledProperty<bool> ShowGuidesProperty = AvaloniaProperty.Register<PaperFormBuilder, bool>(nameof(ShowGuides), true, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public bool ShowRulers { get => GetValue(ShowRulersProperty); set => SetValue(ShowRulersProperty, value); }
    public bool ShowGuides { get => GetValue(ShowGuidesProperty); set => SetValue(ShowGuidesProperty, value); }
    double RulerInset => ShowRulers && Mode == PaperFormMode.Design ? M(24) / Zoom : 0;
    IEnumerable<PaperGuide> Guides => Definition.Pages[PageIndex].Guides.Where(g => g.Position <= (g.Orientation == PaperGuideOrientation.Vertical ? Definition.PageSize.Width : Definition.PageSize.Height));
    PaperGuide? GuideAt(Point point) => ShowGuides ? Guides.LastOrDefault(g => Math.Abs((g.Orientation == PaperGuideOrientation.Vertical ? point.X : point.Y) - g.Position) * DipsPerMm * Zoom <= M(5)) : null;
    /// <summary>Add a nonprinting alignment guide on the active page.</summary>
    public void AddGuide(PaperGuideOrientation orientation, double position)
    {
        if (Mode != PaperFormMode.Design) return;
        var pages = Definition.Pages.ToArray();
        pages[PageIndex] = pages[PageIndex] with { Guides = pages[PageIndex].Guides.Append(new PaperGuide(Guid.NewGuid().ToString("N"), orientation, position)).ToArray() };
        Edit(Definition with { Pages = pages });
    }
    public void MoveGuide(string id, double position)
    {
        if (Mode != PaperFormMode.Design || !Definition.Pages.SelectMany(p => p.Guides).Any(g => g.Id == id && g.Position != position)) return;
        Edit(Definition with { Pages = Definition.Pages.Select(p => p with { Guides = p.Guides.Select(g => g.Id == id ? g with { Position = position } : g).ToArray() }).ToArray() });
    }
    public void RemoveGuide(string id)
    {
        if (Mode != PaperFormMode.Design || !Definition.Pages.SelectMany(p => p.Guides).Any(g => g.Id == id)) return;
        Edit(Definition with { Pages = Definition.Pages.Select(p => p with { Guides = p.Guides.Where(g => g.Id != id).ToArray() }).ToArray() });
    }
    void PaintRulers(DrawingContext context, Size page)
    {
        if (!ShowRulers) return;
        double inset = RulerInset, stroke = 1 / Zoom;
        context.FillRectangle(B(Raised), new Rect(-inset, -inset, page.Width + inset, inset));
        context.FillRectangle(B(Raised), new Rect(-inset, 0, inset, page.Height));
        var pen = new Pen(B(Outline), stroke);
        context.DrawText(new FormattedText(T("mm"), Culture, FlowDirection.LeftToRight, new Typeface(UiFont), TextSize.Caption / Zoom, B(Text2)), new Point(-inset + 2 * stroke, -inset + 2 * stroke));
        double major = Math.Max(10, Math.Ceiling(M(40) / (DipsPerMm * Zoom * 10)) * 10), minor = major / 5;
        void Axis(bool vertical)
        {
            double extent = (vertical ? page.Height : page.Width) / DipsPerMm;
            for (double mm = 0; mm <= extent; mm += minor)
            {
                double at = mm * DipsPerMm; bool labelled = Math.Abs(mm / major - Math.Round(mm / major)) < .001;
                double tick = M(labelled ? 7 : 4) / Zoom;
                context.DrawLine(pen, vertical ? new Point(-tick, at) : new Point(at, -tick), vertical ? new Point(0, at) : new Point(at, 0));
                if (!labelled) continue;
                var text = new FormattedText(mm.ToString("0", Culture), Culture, FlowDirection.LeftToRight, new Typeface(UiFont), TextSize.Caption / Zoom, B(Text2));
                context.DrawText(text, vertical ? new Point(-inset + stroke, at + stroke) : new Point(at + stroke, -inset + stroke));
            }
        }
        Axis(false); Axis(true);
    }
}

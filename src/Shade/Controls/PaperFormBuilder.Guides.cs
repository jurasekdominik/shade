using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
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
    public void RemoveGuide(string id) => RemoveGuides(new[] { id });
    /// <summary>Remove several guides as one undo step.</summary>
    void RemoveGuides(IReadOnlyCollection<string> ids)
    {
        if (Mode != PaperFormMode.Design || !Definition.Pages.SelectMany(p => p.Guides).Any(g => ids.Contains(g.Id))) return;
        Edit(Definition with { Pages = Definition.Pages.Select(p => p with { Guides = p.Guides.Where(g => !ids.Contains(g.Id)).ToArray() }).ToArray() });
        selectedGuides.ExceptWith(ids);
    }
    void ReplaceGuide(string id, PaperGuideOrientation orientation, double position)
    {
        if (Mode != PaperFormMode.Design || !Definition.Pages.SelectMany(p => p.Guides).Any(g => g.Id == id && (g.Orientation != orientation || g.Position != position))) return;
        Edit(Definition with { Pages = Definition.Pages.Select(p => p with { Guides = p.Guides.Select(g => g.Id == id ? g with { Orientation = orientation, Position = position } : g).ToArray() }).ToArray() });
    }

    // The Guides tab: every page's guides, multi-selection, Delete, and an editor for one selected guide.
    sealed record GuideRow(string? Id, int Page, string Caption);
    internal readonly HashSet<string> selectedGuides = new(StringComparer.Ordinal);
    readonly ListBox guideList = new() { SelectionMode = SelectionMode.Multiple, MinHeight = 0, BorderThickness = new Thickness(0), Padding = new Thickness(0) };
    readonly ComboBox guideOrientation = new() { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch };
    readonly Field guidePosition = new();
    readonly StackPanel guideEditor = new() { Spacing = M(6), Margin = new Thickness(0, M(8), 0, 0) };
    bool updatingGuides;
    Control ConfigureGuideList()
    {
        AutomationProperties.SetName(guideList, T("Page guides"));
        guideList.ItemTemplate = new FuncDataTemplate<GuideRow>((row, _) => row == null ? null : new TextBlock { Text = row.Caption, Margin = new Thickness(M(row.Id == null ? 0 : 16), 0, 0, 0) });
        guideList.SelectionChanged += (_, _) =>
        {
            if (updatingGuides) return;
            var rows = guideList.SelectedItems?.OfType<GuideRow>().ToArray() ?? Array.Empty<GuideRow>();
            selectedGuides.Clear(); selectedGuides.UnionWith(rows.Where(r => r.Id != null).Select(r => r.Id!));
            if (rows.LastOrDefault() is { } last) ActivatePage(last.Page);
            RefreshGuideEditor(); foreach (var surface in surfaces) surface.Redraw();
        };
        guideList.AddHandler(KeyDownEvent, (_, e) => { if (e.Key is Key.Delete or Key.Back && selectedGuides.Count > 0) { RemoveGuides(selectedGuides.ToArray()); e.Handled = true; } }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Words(guideOrientation, c => { int at = c.SelectedIndex; updatingGuides = true; c.ItemsSource = new[] { T("Horizontal"), T("Vertical") }; c.SelectedIndex = at; updatingGuides = false; AutomationProperties.SetName(c, T("Guide orientation")); });
        guideOrientation.SelectionChanged += (_, _) => { if (!updatingGuides && SelectedGuide is { } g && guideOrientation.SelectedIndex >= 0) ReplaceGuide(g.Id, (PaperGuideOrientation)guideOrientation.SelectedIndex, g.Position); };
        guidePosition.Edited += () =>
        {
            if (updatingGuides || SelectedGuide is not { } g) return;
            if (!double.TryParse(guidePosition.Text, NumberStyles.Float, Culture, out double mm) || mm < 0 || mm > 2000) { guidePosition.Error = T("Enter a valid value."); return; }
            guidePosition.Error = null; ReplaceGuide(g.Id, g.Orientation, mm);
        };
        NumberControls(guidePosition, .5, 0, () => SelectedGuide?.Position ?? 0, () => GridPitch);
        var actions = new WrapPanel { ItemSpacing = M(4), LineSpacing = M(4) };
        void Button(string label, Action action, ButtonKind kind = ButtonKind.Ghost) { var b = Words(new ActionButton(T(label), kind), c => c.Content = T(label)); b.Click += (_, _) => action(); actions.Children.Add(b); }
        Button("Add vertical", () => AddGuide(PaperGuideOrientation.Vertical, Math.Round(Definition.PageSize.Width / 2)));
        Button("Add horizontal", () => AddGuide(PaperGuideOrientation.Horizontal, Math.Round(Definition.PageSize.Height / 2)));
        Button("Remove", () => RemoveGuides(selectedGuides.ToArray()));
        guideEditor.Children.Add(Labelled("Orientation", guideOrientation)); guideEditor.Children.Add(Labelled("Position (mm)", guidePosition));
        var host = new DockPanel();
        var bottom = new StackPanel { Children = { guideEditor, actions } }; DockPanel.SetDock(bottom, Dock.Bottom);
        host.Children.Add(bottom); host.Children.Add(guideList); return host;
    }
    PaperGuide? SelectedGuide => selectedGuides.Count == 1 ? Definition.Pages.SelectMany(p => p.Guides).FirstOrDefault(g => g.Id == selectedGuides.First()) : null;
    void RefreshGuideList()
    {
        var rows = new List<GuideRow>();
        for (int i = 0; i < Definition.Pages.Count; i++)
        {
            rows.Add(new(null, i, T("Page {0}", i + 1)));
            foreach (var g in Definition.Pages[i].Guides.OrderBy(g => g.Orientation).ThenBy(g => g.Position))
                rows.Add(new(g.Id, i, T(g.Orientation == PaperGuideOrientation.Vertical ? "Vertical · {0} mm" : "Horizontal · {0} mm", g.Position.ToString("0.#", Culture))));
        }
        selectedGuides.IntersectWith(rows.Where(r => r.Id != null).Select(r => r.Id!));
        updatingGuides = true;
        try
        {
            if (guideList.ItemsSource is not IReadOnlyList<GuideRow> old || !old.SequenceEqual(rows)) guideList.ItemsSource = rows;
            var selected = rows.Where(r => r.Id != null && selectedGuides.Contains(r.Id)).ToArray();
            if (guideList.SelectedItems != null && !guideList.SelectedItems.OfType<GuideRow>().SequenceEqual(selected)) { guideList.SelectedItems.Clear(); foreach (var r in selected) guideList.SelectedItems.Add(r); }
        }
        finally { updatingGuides = false; }
        RefreshGuideEditor();
    }
    void RefreshGuideEditor()
    {
        var g = SelectedGuide; guideEditor.IsEnabled = g != null && Mode == PaperFormMode.Design;
        updatingGuides = true;
        try
        {
            guideOrientation.SelectedIndex = g == null ? -1 : (int)g.Orientation;
            if (!guidePosition.Box.IsFocused) { guidePosition.Text = g?.Position.ToString(Culture) ?? ""; guidePosition.Error = null; }
        }
        finally { updatingGuides = false; }
    }
    /// <summary>Select guides from the canvas; the Guides tab follows.</summary>
    internal void SelectGuide(string? id) { selectedGuides.Clear(); if (id != null) selectedGuides.Add(id); RefreshGuideList(); foreach (var surface in surfaces) surface.Redraw(); }
    void PaintRulers(DrawingContext context, Size page)
    {
        if (!ShowRulers) return;
        double inset = RulerInset, stroke = 1 / Zoom;
        context.FillRectangle(B(Raised), new Rect(-inset, -inset, page.Width + inset, inset));
        context.FillRectangle(B(Raised), new Rect(-inset, 0, inset, page.Height));
        var pen = new Pen(B(Outline), stroke);
        context.DrawText(RulerLabel(T("mm")), new Point(-inset + 2 * stroke, -inset + 2 * stroke));
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
                var text = RulerLabel(mm.ToString("0", Culture));
                context.DrawText(text, vertical ? new Point(-inset + stroke, at + stroke) : new Point(at + stroke, -inset + stroke));
            }
        }
        Axis(false); Axis(true);
    }
    readonly Dictionary<string, FormattedText> rulerLabels = new(StringComparer.Ordinal);
    double rulerZoom;
    FormattedText RulerLabel(string text)
    {
        if (rulerZoom != Zoom) { rulerLabels.Clear(); rulerZoom = Zoom; }
        if (!rulerLabels.TryGetValue(text, out var label)) rulerLabels[text] = label = new FormattedText(text, Culture, FlowDirection.LeftToRight, new Typeface(UiFont), TextSize.Caption / Zoom, B(Text2));
        return label;
    }
}

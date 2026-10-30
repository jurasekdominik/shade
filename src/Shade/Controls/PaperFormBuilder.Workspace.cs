using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Data;
using static Shade.Look;
using static Shade.PaperFormDefinition;

namespace Shade;

public sealed partial class PaperFormBuilder
{
    public static readonly StyledProperty<bool> ShowLayersProperty = AvaloniaProperty.Register<PaperFormBuilder, bool>(nameof(ShowLayers), true);
    public bool ShowLayers { get => GetValue(ShowLayersProperty); set => SetValue(ShowLayersProperty, value); }
    /// <summary>A Layers row; Severity 1 is a warning, 2 an error, with the messages as its tooltip.</summary>
    sealed class Layer(string? id, int page, string caption, int depth) : INotifyPropertyChanged
    {
        public string? Id { get; } = id;
        public int Page { get; } = page;
        public int Depth { get; } = depth;
        public string Caption { get; private set; } = caption;
        int severity;
        public Avalonia.Media.IBrush Foreground => B(severity == 2 ? DangerText : severity == 1 ? Warn : Text);
        public string? Problem { get; private set; }
        public event PropertyChangedEventHandler? PropertyChanged;
        internal void Update(string text, int nextSeverity, string? problem)
        {
            if (Caption == text && severity == nextSeverity && Problem == problem) return;
            Caption = text; severity = nextSeverity; Problem = problem;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        }
    }
    readonly TextTabs panelTabs = new("Layers", "Guides");
    readonly ListBox layers = new() { SelectionMode = SelectionMode.Multiple, MinHeight = 0, BorderThickness = new Thickness(0), Padding = new Thickness(0) };
    readonly DockPanel layersPanel = new() { Width = M(192), Margin = new Thickness(0, 0, M(16), 0) };
    bool updatingLayers;
    readonly AvaloniaList<Layer> layerRows = new();
    PaperFormDefinition? layerDefinition;
    IReadOnlyList<PaperFormProblem>? layerProblems;
    readonly Dictionary<int, (IReadOnlyList<PaperStructure> source, IReadOnlyList<PaperFormProblem> problems, Layer[] rows)> layerPages = new();
    internal int LayerPageUpdateCount { get; private set; }
    readonly Dictionary<string, (PaperStructure structure, Layer row, PaperFormProblem[] problems)> layerCache = new(StringComparer.Ordinal);
    void ConfigureLayers()
    {
        layers.ItemsSource = layerRows;
        Words(panelTabs, t => { t.SetName(0, T("Layers")); t.SetName(1, T("Guides")); });
        panelTabs.Margin = new Thickness(0, 0, 0, M(8)); DockPanel.SetDock(panelTabs, Dock.Top); layersPanel.Children.Add(panelTabs);
        var layersHost = new Panel { Children = { layers, layerDropCanvas } };
        var guidesHost = ConfigureGuideList(); guidesHost.IsVisible = false;
        layersPanel.Children.Add(new Panel { Children = { layersHost, guidesHost } });
        panelTabs.Changed += i => { layersHost.IsVisible = i == 0; guidesHost.IsVisible = i == 1; };
        AutomationProperties.SetName(layers, T("Page elements"));
        layers.ItemTemplate = new FuncDataTemplate<Layer>((item, _) =>
        {
            if (item == null) return null;
            var text = new TextBlock { Margin = new Thickness(M(item.Depth * 16), 0, 0, 0), TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis };
            text.Bind(TextBlock.TextProperty, new Binding(nameof(Layer.Caption)) { Source = item });
            text.Bind(TextBlock.ForegroundProperty, new Binding(nameof(Layer.Foreground)) { Source = item });
            text.Bind(ToolTip.TipProperty, new Binding(nameof(Layer.Problem)) { Source = item });
            return text;
        });
        layers.SelectionChanged += (_, e) =>
        {
            if (updatingLayers) return;
            var selected = layers.SelectedItems?.OfType<Layer>().ToArray() ?? Array.Empty<Layer>();
            var primary = e.AddedItems.OfType<Layer>().LastOrDefault() ?? selected.FirstOrDefault();
            if (primary != null) SetSelection(primary.Id == null ? Array.Empty<string>() : selected.Where(l => l.Id != null).Select(l => l.Id!), primary.Page);
        };
        ConfigureLayerDragging();
        PropertyChanged += (_, e) => { if (e.Property == ShowLayersProperty) RefreshLayers(); };
    }
    void RefreshLayers()
    {
        layersPanel.IsVisible = ShowLayers && Mode == PaperFormMode.Design;
        updatingLayers = true;
        try
        {
            if (!ReferenceEquals(layerDefinition, Definition) || !ReferenceEquals(layerProblems, Problems))
            {
                var rows = new List<Layer>();
                if (layerDefinition == null) layerPages.Clear();
                var known = new HashSet<string>(StringComparer.Ordinal);
                void Add(IEnumerable<PaperStructure> items, int page, int depth)
                {
                    foreach (var s in items.Reverse())
                    {
                        known.Add(s.Id);
                        var found = ProblemsForStructure(s.Id);
                        Layer row;
                        if (layerCache.TryGetValue(s.Id, out var cached) && ReferenceEquals(cached.structure, s) && cached.row.Page == page && cached.row.Depth == depth && (ReferenceEquals(cached.problems, found) || cached.problems.SequenceEqual(found))) row = cached.row;
                        else
                        {
                            var messages = found;
                            int severity = messages.Length == 0 ? 0 : messages.Any(p => !p.IsWarning) ? 2 : 1;
                            row = cached.row is { } previous && previous.Page == page && previous.Depth == depth ? previous : new(s.Id, page, DisplayName(s), depth);
                            row.Update(DisplayName(s), severity, messages.Length == 0 ? null : string.Join("\n", messages.Select(p => p.Message).Distinct()));
                            layerCache[s.Id] = (s, row, messages);
                        }
                        rows.Add(row); Add(s.Children, page, depth + 1);
                    }
                }
                for (int i = 0; i < Definition.Pages.Count; i++)
                {
                    var source = Definition.Pages[i].Structures; var problems = ProblemsOnPage(i);
                    if (!layerPages.TryGetValue(i, out var cached) || !ReferenceEquals(cached.source, source) || !ReferenceEquals(cached.problems, problems))
                    {
                        int start = rows.Count; LayerPageUpdateCount++;
                        var heading = cached.rows?.FirstOrDefault() ?? new Layer(null, i, "", 0);
                        heading.Update(T("Page {0}", i + 1), 0, null);
                        rows.Add(heading); Add(source, i, 1);
                        layerPages[i] = (source, problems, rows.Skip(start).ToArray());
                    }
                    else
                    {
                        rows.AddRange(cached.rows);
                        foreach (var row in cached.rows) if (row.Id != null) known.Add(row.Id);
                    }
                }
                foreach (int stale in layerPages.Keys.Where(i => i >= Definition.Pages.Count).ToArray()) layerPages.Remove(stale);
                foreach (string id in layerCache.Keys.Where(id => !known.Contains(id)).ToArray()) layerCache.Remove(id);
                if (!layerRows.SequenceEqual(rows))
                {
                    var keys = rows.Select(row => (row.Id, row.Page)).ToHashSet();
                    for (int i = layerRows.Count - 1; i >= 0; i--) if (!keys.Contains((layerRows[i].Id, layerRows[i].Page))) layerRows.RemoveAt(i);
                    for (int i = 0; i < rows.Count; i++)
                    {
                        if (i == layerRows.Count) { layerRows.AddRange(rows.Skip(i)); break; }
                        if (layerRows[i].Id != rows[i].Id || layerRows[i].Page != rows[i].Page)
                        {
                            int at = -1;
                            for (int j = i + 1; j < layerRows.Count; j++) if (layerRows[j].Id == rows[i].Id && layerRows[j].Page == rows[i].Page) { at = j; break; }
                            if (at >= 0) layerRows.Move(at, i); else layerRows.Insert(i, rows[i]);
                        }
                        if (layerRows[i] != rows[i]) layerRows[i] = rows[i];
                    }
                }
                layerDefinition = Definition; layerProblems = Problems;
            }
            var selected = layerRows.Where(l => l.Id == null ? SelectedIds.Count == 0 && l.Page == PageIndex : SelectedIds.Contains(l.Id)).ToArray();
            if (layers.SelectedItems != null && !layers.SelectedItems.OfType<Layer>().SequenceEqual(selected)) { layers.SelectedItems.Clear(); foreach (var row in selected) layers.SelectedItems.Add(row); }
        }
        finally { updatingLayers = false; }
        RefreshGuideList();
    }
    void ActivatePage(int page)
    {
        activatingPage = true;
        try { SetCurrentValue(PageIndexProperty, page); }
        finally { activatingPage = false; }
    }
    void RevealPage()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (TopLevel.GetTopLevel(this) == null) return;
            viewport.UpdateLayout();
            if (Page.TranslatePoint(default, viewport) is { } point)
            {
                double margin = M(24) + RulerInset * Zoom;
                viewport.Offset += new Vector(point.X - margin, point.Y - margin);
            }
        });
    }
    double FitZoom(int count, bool widthOnly = false)
    {
        var size = Definition.PageSize;
        if (viewport.Bounds.Width <= M(48) || viewport.Bounds.Height <= M(48)) return 1;
        double columns = SideBySidePages && count > 1 ? 2 : 1;
        double width = (viewport.Bounds.Width - M(48) - columns * RulerInset * Zoom) / (size.Width * DipsPerMm * columns + M(24) * (columns - 1));
        int rows = SideBySidePages && count > 1 ? 1 : count;
        double height = (viewport.Bounds.Height - M(48) - rows * RulerInset * Zoom) / (size.Height * DipsPerMm * rows + M(24) * (rows - 1));
        return Math.Clamp(widthOnly ? width : Math.Min(width, height), .01, 4);
    }
    double MinimumZoom => viewport.Bounds.Width <= M(48) || viewport.Bounds.Height <= M(48) ? .01 : Math.Min(1, FitZoom(Math.Min(2, Definition.Pages.Count)));
    void ClampZoom()
    {
        if (!ready || viewport == null) return;
        workspace.MinWidth = Math.Max(0, viewport.Bounds.Width);
        double zoom = Math.Clamp(Zoom, MinimumZoom, 4);
        if (zoom != Zoom) SetCurrentValue(ZoomProperty, zoom);
    }
    /// <summary>Show the active page in full, keeping other pages available by scrolling.</summary>
    public void FitPage() { SetCurrentValue(ZoomProperty, FitZoom(1)); RevealPage(); }
    /// <summary>Fit the paper width to the available workspace.</summary>
    public void FitWidth() { SetCurrentValue(ZoomProperty, FitZoom(1, true)); RevealPage(); }
    /// <summary>Show two consecutive pages, keeping longer documents scrollable.</summary>
    public void FitTwoPages() { SetCurrentValue(ZoomProperty, FitZoom(Math.Min(2, Definition.Pages.Count))); RevealPage(); }
}

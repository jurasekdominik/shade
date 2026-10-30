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
    /// <summary>Which elements the review list shows; the page itself always keeps every element.</summary>
    public static readonly StyledProperty<PaperLayerFilter> LayerFilterProperty = AvaloniaProperty.Register<PaperFormBuilder, PaperLayerFilter>(nameof(LayerFilter), PaperLayerFilter.Attention, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public PaperLayerFilter LayerFilter { get => GetValue(LayerFilterProperty); set => SetValue(LayerFilterProperty, value); }
    /// <summary>An empty input or container, or any element with a warning or error.</summary>
    bool NeedsAttention(PaperStructure s, bool problem) =>
        problem || s.Kind == "group" && s.Children.Count == 0 || PaperStructureKind.HasValue(s.Kind) && s.Kind != "check" && string.IsNullOrEmpty(Value(s)?.Text);
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
    readonly TextTabs panelTabs = new("Review", "Guides");
    readonly CheckBox showAllLayers = new();
    string layerSelection = "";
    readonly HashSet<string> layerRecheckKeys = new(StringComparer.Ordinal);
    PaperFormDefinition? layerIndexFor;
    readonly Dictionary<string, (PaperStructure structure, string? parent, int page, int order)> layerIndex = new(StringComparer.Ordinal);
    readonly Dictionary<string, List<string>> layerKeyIds = new(StringComparer.Ordinal);
    readonly List<List<string>> layerOrder = new();
    readonly Dictionary<string, bool> keyEmpty = new(StringComparer.Ordinal);
    bool clickedLayer, layerRefreshPosted;
    readonly ListBox layers = new() { SelectionMode = SelectionMode.Multiple, MinHeight = 0, BorderThickness = new Thickness(0), Padding = new Thickness(0) };
    readonly DockPanel layersPanel = new() { Width = M(192), Margin = new Thickness(0, 0, M(16), 0) };
    bool updatingLayers;
    readonly AvaloniaList<Layer> layerRows = new();
    PaperFormDefinition? layerDefinition;
    IReadOnlyList<PaperFormProblem>? layerProblems;
    readonly Dictionary<int, (IReadOnlyList<PaperStructure> source, IReadOnlyList<PaperFormProblem> problems, string selection, Layer[] rows)> layerPages = new();
    internal int LayerPageUpdateCount { get; private set; }
    readonly Dictionary<string, (PaperStructure structure, Layer row, PaperFormProblem[] problems)> layerCache = new(StringComparer.Ordinal);
    void ConfigureLayers()
    {
        layers.ItemsSource = layerRows;
        Words(panelTabs, t => { t.SetName(0, T("Review")); t.SetName(1, T("Guides")); });
        panelTabs.Margin = new Thickness(0, 0, 0, M(8)); DockPanel.SetDock(panelTabs, Dock.Top); layersPanel.Children.Add(panelTabs);
        Words(showAllLayers, c => c.Content = T("Show all elements"));
        showAllLayers.IsCheckedChanged += (_, _) => { if (!updatingLayers) SetCurrentValue(LayerFilterProperty, showAllLayers.IsChecked == true ? PaperLayerFilter.All : PaperLayerFilter.Attention); };
        showAllLayers.Margin = new Thickness(0, 0, 0, M(8)); DockPanel.SetDock(showAllLayers, Dock.Top); layersPanel.Children.Add(showAllLayers);
        var layersHost = new Panel { Children = { layers, layerDropCanvas } };
        var guidesHost = ConfigureGuideList(); guidesHost.IsVisible = false;
        layersPanel.Children.Add(new Panel { Children = { layersHost, guidesHost } });
        panelTabs.Changed += i => { layersHost.IsVisible = showAllLayers.IsVisible = i == 0; guidesHost.IsVisible = i == 1; };
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
            if (primary == null) return;
            clickedLayer = true;  // the list is still handling this click: rebuild its rows afterwards, not underneath it
            try { SetSelection(primary.Id == null ? Array.Empty<string>() : selected.Where(l => l.Id != null).Select(l => l.Id!), primary.Page); }
            finally { clickedLayer = false; }
        };
        ConfigureLayerDragging();
        PropertyChanged += (_, e) => { if (e.Property == ShowLayersProperty || e.Property == LayerFilterProperty) { layerDefinition = null; RefreshLayers(); } };
    }
    Layer RowFor(PaperStructure s, int page, int depth, PaperFormProblem[] found)
    {
        if (layerCache.TryGetValue(s.Id, out var cached) && ReferenceEquals(cached.structure, s) && cached.row.Page == page && cached.row.Depth == depth && (ReferenceEquals(cached.problems, found) || cached.problems.SequenceEqual(found))) return cached.row;
        int severity = found.Length == 0 ? 0 : found.Any(p => !p.IsWarning) ? 2 : 1;
        var row = cached.row is { } previous && previous.Page == page && previous.Depth == depth ? previous : new Layer(s.Id, page, DisplayName(s), depth);
        row.Update(DisplayName(s), severity, found.Length == 0 ? null : string.Join("\n", found.Select(p => p.Message).Distinct()));
        layerCache[s.Id] = (s, row, found);
        return row;
    }
    /// <summary>Where every structure sits in the list order (built once per definition, not per edit).</summary>
    void BuildLayerIndex()
    {
        if (ReferenceEquals(layerIndexFor, Definition)) return;
        layerIndexFor = Definition; layerIndex.Clear(); layerKeyIds.Clear(); layerOrder.Clear();
        for (int page = 0; page < Definition.Pages.Count; page++)
        {
            var order = new List<string>(); layerOrder.Add(order); int at = page;
            void Walk(IEnumerable<PaperStructure> items, string? parent)
            {
                foreach (var s in items.Reverse())
                {
                    layerIndex[s.Id] = (s, parent, at, order.Count); order.Add(s.Id);
                    if (s.FieldKey.Length > 0) { if (!layerKeyIds.TryGetValue(s.FieldKey, out var ids)) layerKeyIds[s.FieldKey] = ids = new(); ids.Add(s.Id); }
                    Walk(s.Children, s.Id);
                }
            }
            Walk(Definition.Pages[page].Structures, null);
        }
    }
    /// <summary>Adds, removes or refreshes just these rows of the filtered list.</summary>
    void ApplyLayerDelta(IEnumerable<string> ids, HashSet<string> listed)
    {
        foreach (string id in ids)
        {
            if (!layerIndex.TryGetValue(id, out var at)) continue;
            var found = ProblemsForStructure(id);
            bool shown = id == SelectedId || NeedsAttention(at.structure, found.Length > 0);
            var current = listed.Contains(id) ? layerRows.First(r => r.Id == id) : null;
            if (!shown) { if (current != null) { layerRows.Remove(current); listed.Remove(id); layerCache.Remove(id); } }
            else if (current != null) RowFor(at.structure, at.page, current.Depth, found);
            else
            {
                int depth = 1;
                for (string? p = at.parent; p != null; p = layerIndex[p].parent) if (listed.Contains(p)) depth++;
                var row = RowFor(at.structure, at.page, depth, found);
                string? before = null; var order = layerOrder[at.page];
                for (int i = at.order - 1; i >= 0 && before == null; i--) if (listed.Contains(order[i])) before = order[i];
                int position = before != null ? layerRows.IndexOf(layerRows.First(r => r.Id == before)) + 1 : layerRows.IndexOf(layerRows.First(r => r.Id == null && r.Page == at.page)) + 1;
                layerRows.Insert(position, row); listed.Add(id);
            }
            layerPages.Remove(at.page);  // that page's cached rows are out of date now
        }
    }
    void RefreshLayers()
    {
        if (clickedLayer)
        {
            if (!layerRefreshPosted) { layerRefreshPosted = true; Avalonia.Threading.Dispatcher.UIThread.Post(() => { layerRefreshPosted = false; RefreshLayers(); }); }
            return;
        }
        layersPanel.IsVisible = ShowLayers && Mode == PaperFormMode.Design;
        updatingLayers = true;
        try
        {
            showAllLayers.IsChecked = LayerFilter == PaperLayerFilter.All;
            bool all = LayerFilter == PaperLayerFilter.All;
            string chosenIds = SelectedId ?? "";  // only the primary selection is kept listed: a large selection must not rebuild the list
            // the selected element stays listed, so the rows of a page depend on the selection on it
            var onList = all || SelectedId == null ? null : new HashSet<string>(StringComparer.Ordinal) { SelectedId };
            bool full = !ReferenceEquals(layerDefinition, Definition) || !ReferenceEquals(layerProblems, Problems);
            if (!full && !all)
            {
                // one edit changes one thing: a field turning empty or filled, or the selection moving, adds or removes only its own row
                BuildLayerIndex();
                var delta = new HashSet<string>(StringComparer.Ordinal);
                foreach (string key in layerRecheckKeys) if (layerKeyIds.TryGetValue(key, out var ids)) delta.UnionWith(ids);
                if (layerSelection != chosenIds) { if (layerSelection.Length > 0) delta.Add(layerSelection); if (chosenIds.Length > 0) delta.Add(chosenIds); }
                layerRecheckKeys.Clear(); layerSelection = chosenIds;
                var listed = layerRows.Where(r => r.Id != null).Select(r => r.Id!).ToHashSet(StringComparer.Ordinal);
                // a group that appears or disappears re-indents its children: that one takes the full rebuild
                if (delta.Any(id => layerIndex.TryGetValue(id, out var at) && at.structure.Kind == "group" && listed.Contains(id) != (id == SelectedId || NeedsAttention(at.structure, ProblemsForStructure(id).Length > 0))))
                {
                    full = true;
                    foreach (int page in layerPages.Keys.ToArray()) layerPages.Remove(page);
                }
                else ApplyLayerDelta(delta, listed);
            }
            if (full)
            {
                layerRecheckKeys.Clear();
                var rows = new List<Layer>();
                if (layerDefinition == null) layerPages.Clear();
                var known = new HashSet<string>(StringComparer.Ordinal);
                void Add(IEnumerable<PaperStructure> items, int page, int depth)
                {
                    foreach (var s in items.Reverse())
                    {
                        known.Add(s.Id);
                        var found = ProblemsForStructure(s.Id);
                        if (!all && onList?.Contains(s.Id) != true && !NeedsAttention(s, found.Length > 0)) { Add(s.Children, page, depth); continue; }
                        rows.Add(RowFor(s, page, depth, found)); Add(s.Children, page, depth + 1);
                    }
                }
                for (int i = 0; i < Definition.Pages.Count; i++)
                {
                    var source = Definition.Pages[i].Structures; var problems = ProblemsOnPage(i);
                    string onPage = onList == null ? "" : string.Join(',', All(source).Where(s => onList.Contains(s.Id)).Select(s => s.Id));
                    if (!layerPages.TryGetValue(i, out var cached) || !ReferenceEquals(cached.source, source) || !ReferenceEquals(cached.problems, problems) || cached.selection != onPage)
                    {
                        int start = rows.Count; LayerPageUpdateCount++;
                        var heading = cached.rows?.FirstOrDefault() ?? new Layer(null, i, "", 0);
                        heading.Update(T("Page {0}", i + 1), 0, null);
                        rows.Add(heading); Add(source, i, 1);
                        layerPages[i] = (source, problems, onPage, rows.Skip(start).ToArray());
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
                layerDefinition = Definition; layerProblems = Problems; layerSelection = chosenIds;
            }
            var chosen = SelectedIds.ToHashSet(StringComparer.Ordinal);
            var selection = layers.Selection;
            var wanted = new List<int>();
            for (int i = 0; i < layerRows.Count; i++)
                if (layerRows[i].Id == null ? chosen.Count == 0 && layerRows[i].Page == PageIndex : chosen.Contains(layerRows[i].Id!)) wanted.Add(i);
            if (!selection.SelectedIndexes.OrderBy(i => i).SequenceEqual(wanted))
            {
                // one batch: adding a thousand rows one by one rebuilds the selection (and the list) a thousand times
                selection.BeginBatchUpdate();
                try { selection.Clear(); foreach (int i in wanted) selection.Select(i); }
                finally { selection.EndBatchUpdate(); }
            }
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

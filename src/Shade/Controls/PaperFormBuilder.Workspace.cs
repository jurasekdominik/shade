using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Input;
using static Shade.Look;
using static Shade.PaperFormDefinition;

namespace Shade;

public sealed partial class PaperFormBuilder
{
    public static readonly StyledProperty<bool> ShowLayersProperty = AvaloniaProperty.Register<PaperFormBuilder, bool>(nameof(ShowLayers), true);
    public bool ShowLayers { get => GetValue(ShowLayersProperty); set => SetValue(ShowLayersProperty, value); }
    sealed record Layer(string? Id, int Page, string Caption, int Depth);
    readonly ListBox layers = new() { SelectionMode = SelectionMode.Multiple, MinHeight = 0, BorderThickness = new Thickness(0), Padding = new Thickness(0) };
    readonly DockPanel layersPanel = new() { Width = M(192), Margin = new Thickness(0, 0, M(16), 0) };
    bool updatingLayers;
    void ConfigureLayers()
    {
        var title = Words(Build.Text(T("Layers"), Text2, TextSize.Label), c => c.Text = T("Layers"));
        title.Margin = new Thickness(0, 0, 0, M(8)); DockPanel.SetDock(title, Dock.Top); layersPanel.Children.Add(title); layersPanel.Children.Add(new Panel { Children = { layers, layerDropCanvas } });
        AutomationProperties.SetName(layers, T("Page elements"));
        layers.ItemTemplate = new FuncDataTemplate<Layer>((item, _) => item == null ? null : new TextBlock { Text = item.Caption, Margin = new Thickness(M(item.Depth * 16), 0, 0, 0), TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis });
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
        var rows = new List<Layer>();
        void Add(IEnumerable<PaperStructure> items, int page, int depth)
        {
            foreach (var s in items.Reverse())
            {
                string name = DisplayName(s);
                rows.Add(new(s.Id, page, name, depth)); Add(s.Children, page, depth + 1);
            }
        }
        for (int i = 0; i < Definition.Pages.Count; i++) { rows.Add(new(null, i, T("Page {0}", i + 1), 0)); Add(Definition.Pages[i].Structures, i, 1); }
        updatingLayers = true;
        try
        {
            if (layers.ItemsSource is not IReadOnlyList<Layer> old || !old.SequenceEqual(rows)) layers.ItemsSource = rows;
            var selected = ((IEnumerable<Layer>)layers.ItemsSource!).Where(l => l.Id == null ? SelectedIds.Count == 0 && l.Page == PageIndex : SelectedIds.Contains(l.Id)).ToArray();
            if (layers.SelectedItems != null && !layers.SelectedItems.OfType<Layer>().SequenceEqual(selected)) { layers.SelectedItems.Clear(); foreach (var row in selected) layers.SelectedItems.Add(row); }
        }
        finally { updatingLayers = false; }
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

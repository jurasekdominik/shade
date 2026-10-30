using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using static Shade.Look;
using static Shade.PaperFormDefinition;

namespace Shade;

/// <summary>A paper-grid designer around PaperFormView. Each completed gesture or inspector change is one undoable layout edit.</summary>
public sealed partial class PaperFormBuilder : PaperFormView
{
    public static readonly StyledProperty<string?> SelectedIdProperty = AvaloniaProperty.Register<PaperFormBuilder, string?>(nameof(SelectedId), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public static readonly StyledProperty<string?> ToolKindProperty = AvaloniaProperty.Register<PaperFormBuilder, string?>(nameof(ToolKind), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public static readonly StyledProperty<double> GridPitchProperty = AvaloniaProperty.Register<PaperFormBuilder, double>(nameof(GridPitch), 2, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay, validate: n => double.IsFinite(n) && n >= .5 && n <= 50);
    public static readonly StyledProperty<bool> ShowGridProperty = AvaloniaProperty.Register<PaperFormBuilder, bool>(nameof(ShowGrid), true, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public static readonly StyledProperty<bool> ShowToolbarProperty = AvaloniaProperty.Register<PaperFormBuilder, bool>(nameof(ShowToolbar), true);
    public static readonly StyledProperty<bool> ShowInspectorProperty = AvaloniaProperty.Register<PaperFormBuilder, bool>(nameof(ShowInspector), true);
    public static readonly StyledProperty<Control?> ToolbarContentProperty = AvaloniaProperty.Register<PaperFormBuilder, Control?>(nameof(ToolbarContent));
    public string? SelectedId { get => GetValue(SelectedIdProperty); set => SetValue(SelectedIdProperty, value); }
    public string? ToolKind { get => GetValue(ToolKindProperty); set => SetValue(ToolKindProperty, value); }
    public double GridPitch { get => GetValue(GridPitchProperty); set => SetValue(GridPitchProperty, value); }
    public bool ShowGrid { get => GetValue(ShowGridProperty); set => SetValue(ShowGridProperty, value); }
    /// <summary>Show the developer-level rows of the inspector (field key, accessible label override). Accessible names exist in every mode.</summary>
    public static readonly StyledProperty<bool> ShowAdvancedPropertiesProperty = AvaloniaProperty.Register<PaperFormBuilder, bool>(nameof(ShowAdvancedProperties));
    public bool ShowAdvancedProperties { get => GetValue(ShowAdvancedPropertiesProperty); set => SetValue(ShowAdvancedPropertiesProperty, value); }
    public bool ShowToolbar { get => GetValue(ShowToolbarProperty); set => SetValue(ShowToolbarProperty, value); }
    public bool ShowInspector { get => GetValue(ShowInspectorProperty); set => SetValue(ShowInspectorProperty, value); }
    public Control? ToolbarContent { get => GetValue(ToolbarContentProperty); set => SetValue(ToolbarContentProperty, value); }
    public static readonly DirectProperty<PaperFormBuilder, bool> CanUndoProperty = AvaloniaProperty.RegisterDirect<PaperFormBuilder, bool>(nameof(CanUndo), b => b.CanUndo);
    public static readonly DirectProperty<PaperFormBuilder, bool> CanRedoProperty = AvaloniaProperty.RegisterDirect<PaperFormBuilder, bool>(nameof(CanRedo), b => b.CanRedo);
    bool canUndo, canRedo;
    public bool CanUndo => canUndo;
    public bool CanRedo => canRedo;
    public event Action? Changed;
    readonly Stack<(PaperFormDefinition definition, IReadOnlyList<string> selected, int page)> undo = new(), redo = new();
    // each entry holds a page array of one reference per structure, so an unbounded history grows with the session and the form
    const int HistoryLimit = 500;
    static void Remember(Stack<(PaperFormDefinition definition, IReadOnlyList<string> selected, int page)> history, (PaperFormDefinition definition, IReadOnlyList<string> selected, int page) entry)
    {
        history.Push(entry);
        if (history.Count <= HistoryLimit) return;
        var keep = history.Take(HistoryLimit).Reverse().ToArray();
        history.Clear(); foreach (var item in keep) history.Push(item);
    }
    readonly WrapPanel toolbar = new() { Orientation = Orientation.Horizontal };
    readonly DockPanel shell = new();
    readonly StackPanel inspector = new() { Width = M(224) };
    readonly TextBlock feedback = Build.Text("", Text3, TextSize.Caption);
    readonly ContentControl extra = new();
    readonly List<DesignSurface> surfaces = new();
    static readonly Cursor drawCursor = new(StandardCursorType.Cross);
    readonly ScrollViewer viewport;
    readonly Border workspace;
    bool activatingPage;
    readonly List<Action> inspectorRefresh = new();
    bool inspectorEditing, inspectorHasEdits;
    bool editing, ready;
    int updateDepth;
    bool updatePending, toolbarPending, inspectorPending, flushing;
    internal int DesignerUpdateCount { get; private set; }
    readonly struct UpdateScope : IDisposable
    {
        readonly PaperFormBuilder owner;
        internal UpdateScope(PaperFormBuilder owner) { this.owner = owner; owner.updateDepth++; }
        public void Dispose() { owner.updateDepth--; owner.FlushUpdate(); }
    }
    void FlushUpdate()
    {
        if (!ready || updateDepth > 0 || flushing || !updatePending) return;
        flushing = true;
        try
        {
            bool toolbarChanged = toolbarPending, inspectorChanged = inspectorPending;
            updatePending = toolbarPending = inspectorPending = false;
            if (toolbarChanged) BuildToolbarCore();
            if (inspectorChanged) BuildInspectorCore();
            UpdateCore();
        }
        finally { flushing = false; }
    }
    PaperStructure? Selected => All(Structures).FirstOrDefault(s => s.Id == SelectedId);
    IEnumerable<(PaperStructure structure, PaperBounds bounds)> Placements(IEnumerable<PaperStructure> items, double x = 0, double y = 0)
    {
        foreach (var s in items)
        {
            var b = s.Bounds with { X = s.Bounds.X + x, Y = s.Bounds.Y + y }; yield return (s, b);
            foreach (var child in Placements(s.Children, b.X, b.Y)) yield return child;
        }
    }

    public PaperFormBuilder()
    {
        using var update = new UpdateScope(this);
        SetCurrentValue(ModeProperty, PaperFormMode.Design);
        ShowAllPages = true; Refresh();
        var page = (Control)Content!; Content = null;
        inspectorScroll = new ScrollViewer { Content = inspector, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        inspector.PropertyChanged += (_, e) => { if (e.Property == IsVisibleProperty) inspectorScroll.IsVisible = inspector.IsVisible; };
        var body = new DockPanel();
        DockPanel.SetDock(layersPanel, Dock.Left); body.Children.Add(layersPanel); ConfigureLayers();
        workspace = new Border { Child = page, Padding = new Thickness(M(24)) };
        viewport = new ScrollViewer { Content = workspace, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        viewport.SizeChanged += (_, _) => ClampZoom();
        viewport.AddHandler(PointerWheelChangedEvent, ZoomWheel, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        zoomTimer.Tick += (_, _) => CommitWheelZoom();
        body.Children.Add(viewport);
        var stack = shell; DockPanel.SetDock(toolbar, Dock.Top); toolbar.Margin = new Thickness(0, 0, 0, M(12)); stack.Children.Add(toolbar);
        ConfigurePresentation(); DockPanel.SetDock(gridRibbon, Dock.Top); stack.Children.Add(gridRibbon);
        footer = new DockPanel { Margin = new Thickness(0, M(8), 0, 0) }; DockPanel.SetDock(pageNavigation, Dock.Right); footer.Children.Add(pageNavigation); footer.Children.Add(feedback);
        DockPanel.SetDock(footer, Dock.Bottom); stack.Children.Add(footer); DockPanel.SetDock(inspectorScroll, Dock.Right); stack.Children.Add(inspectorScroll); stack.Children.Add(body); Content = stack;
        PageUpdated = Update;
        ValuesEdited = () => { if (layersPanel.IsVisible && LayerFilter == PaperLayerFilter.Attention) { layerDefinition = null; RefreshLayers(); } };
        Words(this, b => { b.layerDefinition = null; b.layerCache.Clear(); b.rulerLabels.Clear(); foreach (var surface in b.surfaces) { surface.Background.InvalidateVisual(); surface.GridBackground.InvalidateVisual(); } if (b.ready) b.Update(); });
        ready = true; BuildToolbar(); BuildInspector(); Update();
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        using var update = new UpdateScope(this);
        if (ready && !DeferZoomLayout && (change.Property == ZoomProperty || change.Property == DefinitionProperty || change.Property == ModeProperty)) CommitWheelZoom();
        base.OnPropertyChanged(change);
        if (!ready) return;
        SelectionChanged(change);
        if (change.Property == PageIndexProperty) { if (!updatingSelection) SetSelection(Array.Empty<string>()); if (!activatingPage) RevealPage(); }
        if (change.Property == ZoomProperty || change.Property == DefinitionProperty || change.Property == SideBySidePagesProperty) ClampZoom();
        if (change.Property == DefinitionProperty && !editing) { undo.Clear(); redo.Clear(); ResetDocumentColours(); NormalizeGrid(); }
        if (change.Property == GridPitchProperty) NormalizeGrid();
        if (change.Property == ShowAdvancedPropertiesProperty) BuildInspector();
        if (change.Property == SelectedIdProperty || change.Property == DefinitionProperty || change.Property == ModeProperty || change.Property == PageIndexProperty || change.Property == InspectorPlacementProperty || change.Property == RibbonFieldWidthProperty) BuildInspector();
        if (change.Property == StructureTypesProperty || change.Property == PageIndexProperty || change.Property == ModeProperty || change.Property == ZoomProperty || change.Property == ToolKindProperty || change.Property == DefinitionProperty || change.Property == SideBySidePagesProperty || change.Property == InspectorPlacementProperty) BuildToolbar();
        if (change.Property == DefinitionProperty || change.Property == ModeProperty || change.Property == SelectedIdProperty || change.Property == ToolKindProperty || change.Property == GridPitchProperty || change.Property == ShowGridProperty || change.Property == ShowToolbarProperty || change.Property == ShowInspectorProperty || change.Property == ProblemsProperty || change.Property == LayerFilterProperty || change.Property == ToolbarContentProperty || change.Property == InspectorPlacementProperty || change.Property == ShowPageNavigationProperty || change.Property == ShowRulersProperty || change.Property == ShowGuidesProperty || change.Property == NumberButtonsPlacementProperty || change.Property == NumberButtonSizeProperty || change.Property == ToolbarPlacementProperty || change.Property == GridSettingsPlacementProperty || change.Property == LayersPlacementProperty || change.Property == LayersSizeProperty || change.Property == FooterPlacementProperty || change.Property == InspectorWidthProperty || change.Property == RibbonFieldWidthProperty || change.Property == PropertyRibbonHeightProperty) Update();
    }
    void Update()
    {
        updatePending = true;
        FlushUpdate();
    }
    void UpdateCore()
    {
        if (!ready) return;
        DesignerUpdateCount++;
        SetAndRaise(CanUndoProperty, ref canUndo, undo.Count > 0); SetAndRaise(CanRedoProperty, ref canRedo, redo.Count > 0);
        toolbar.IsVisible = ShowToolbar; inspector.IsVisible = ShowInspector && Mode == PaperFormMode.Design;
        UpdatePresentation(); UpdatePageInsets(RulerInset);
        extra.Content = ToolbarContent;
        while (surfaces.Count > Pages.Count) surfaces.RemoveAt(surfaces.Count - 1);
        while (surfaces.Count < Pages.Count)
        {
            int index = surfaces.Count;
            var surface = new DesignSurface(this, index) { ZIndex = int.MaxValue }; Pages[index].Children.Add(surface.Background); Pages[index].Children.Add(surface.GuideLayer); Pages[index].Children.Add(surface.GridBackground); Pages[index].Children.Add(surface); surfaces.Add(surface);
            Pages[index].AddHandler(PointerPressedEvent, (_, _) => { if (Mode != PaperFormMode.Design) ActivatePage(index); }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        }
        for (int i = 0; i < surfaces.Count; i++)
        {
            var guide = surfaces[i]; guide.Width = Pages[i].Width + RulerInset; guide.Height = Pages[i].Height + RulerInset;
            Canvas.SetLeft(guide, -RulerInset); Canvas.SetTop(guide, -RulerInset); foreach (var layer in new Control[] { guide.Background, guide.GridBackground, guide.GuideLayer })
            { layer.Width = guide.Width; layer.Height = guide.Height; Canvas.SetLeft(layer, -RulerInset); Canvas.SetTop(layer, -RulerInset); layer.IsVisible = Mode == PaperFormMode.Design; }
            guide.IsVisible = guide.Background.IsVisible = Mode == PaperFormMode.Design; guide.Cursor = ToolKind == null ? Cursor.Default : drawCursor;
            guide.Background.Refresh(); guide.GridBackground.Refresh(); guide.Redraw();
        }
        RefreshLayers();
        // the drawing hint lives in the footer, so choosing a tool never reflows the toolbar
        // All messages remain in the public Problems list and layer tooltips; shaping thousands in the footer blocks edits.
        feedback.Text = ToolKind != null && Mode == PaperFormMode.Design ? T("Drag on the page to draw") : Problems.Count > 0 ? Problems[0].Message + (Problems.Count > 1 ? $" ({Problems.Count})" : "") : T("{0} × {1} mm · Page {2} of {3}", Definition.PageSize.Width, Definition.PageSize.Height, PageIndex + 1, Definition.Pages.Count);
        feedback.Foreground = B(ToolKind != null && Mode == PaperFormMode.Design ? Text2 : Problems.Any(p => !p.IsWarning) ? Danger : Problems.Count > 0 ? Warn : Text3);
        foreach (var button in toolbar.Children.OfType<ActionButton>())
            if (button.Tag is string tag) button.IsEnabled = Mode == PaperFormMode.Design && (tag == "undo" ? CanUndo : tag == "redo" ? CanRedo : Selected != null);
    }

    void BuildToolbar()
    {
        toolbarPending = updatePending = true;
        FlushUpdate();
    }
    readonly List<Action> toolbarRefresh = new();
    object? toolbarTypes;
    void BuildToolbarCore()
    {
        if (toolbarRefresh.Count > 0 && ReferenceEquals(toolbarTypes, StructureTypes))
        {
            foreach (var refresh in toolbarRefresh) refresh();
            return;
        }
        toolbarTypes = StructureTypes; toolbarRefresh.Clear();
        toolbar.Children.Clear();
        ComboBox Choice(string label, Func<string[]> items, Func<int> selected, Action<int> action)
        {
            var box = new ComboBox { MinWidth = 0 }; bool refreshing = false;
            void Refresh()
            {
                refreshing = true;
                try
                {
                    var captions = items().Select(i => T(i)).ToArray();
                    if (box.ItemsSource is not string[] old || !old.SequenceEqual(captions)) box.ItemsSource = captions;
                    box.SelectedIndex = selected(); AutomationProperties.SetName(box, T(label));
                }
                finally { refreshing = false; }
            }
            toolbarRefresh.Add(Refresh); Words(box, _ => Refresh());
            box.SelectionChanged += (_, _) => { if (!refreshing && box.SelectedIndex >= 0) action(box.SelectedIndex); }; toolbar.Children.Add(box); return box;
        }
        Choice("Mode", () => new[] { "Design", "Fill", "Print preview" }, () => (int)Mode, i => SetCurrentValue(ModeProperty, (PaperFormMode)i));
        var palette = Palette.Concat((StructureTypes ?? Array.Empty<PaperStructureTemplate>()).Select(t => (t.Kind, t.Label))).ToArray();
        var tools = Choice("Structure", () => new[] { "Insert" }.Concat(palette.Select(t => t.Item2)).ToArray(), () => Array.FindIndex(palette, p => p.Item1 == ToolKind) + 1, i => SetCurrentValue(ToolKindProperty, i == 0 ? null : palette[i - 1].Item1));
        toolbarRefresh.Add(() => { tools.IsVisible = Mode == PaperFormMode.Design; ToolTip.SetTip(tools, T("Choose an element, then drag on the page to draw it. Escape returns to selection.")); });
        var formats = Enum.GetValues<EditableTable.PaperKind>().Where(k => k != EditableTable.PaperKind.Screen).ToArray();
        var format = Choice("Paper format", () => formats.Select(k => k.ToString()).ToArray(), () => Array.IndexOf(formats, Definition.PaperFormat), i => Edit(Definition with { PaperFormat = formats[i] }));
        var orientation = Choice("Orientation", () => new[] { "Portrait", "Landscape" }, () => Definition.Landscape ? 1 : 0, i => Edit(Definition with { Landscape = i == 1 }));
        toolbarRefresh.Add(() => format.IsEnabled = orientation.IsEnabled = Mode == PaperFormMode.Design);
        Choice("Page", () => Enumerable.Range(1, Definition.Pages.Count).Select(i => i.ToString(Culture)).ToArray(), () => Math.Min(PageIndex, Definition.Pages.Count - 1), i => SetCurrentValue(PageIndexProperty, i));
        double[] zooms = Array.Empty<double>();
        Choice("Zoom", () => { zooms = new[] { .5, .75, 1, 1.5, 2, 3, 4, Zoom }.Where(z => z >= MinimumZoom).Distinct().Order().ToArray(); return zooms.Select(z => z.ToString("0%", Culture)).ToArray(); }, () => Array.IndexOf(zooms, Zoom), i => SetCurrentValue(ZoomProperty, zooms[i]));
        void Button(string label, string tag, Action action) { var b = Words(new ActionButton(T(label), ButtonKind.Ghost) { Tag = tag }, c => c.Content = T(label)); b.Click += (_, _) => action(); toolbar.Children.Add(b); }
        void History(string label, string tag, Geometry icon, Action action)
        {
            var button = Words(new ActionButton { Icon = icon, Kind = ButtonKind.Ghost, Tag = tag, Width = ControlHeight, Padding = new Thickness(0) }, c => { AutomationProperties.SetName(c, T(label)); ToolTip.SetTip(c, T(label)); });
            button.Click += (_, _) => action(); toolbar.Children.Add(button);
        }
        History("Undo", "undo", Icons.Undo2, Undo); History("Redo", "redo", Icons.Redo2, Redo); Button("Duplicate", "selection", DuplicateSelected); Button("Delete", "selection", DeleteSelected);
        void Action(string label, Action action, Func<bool>? enabled = null, Func<bool>? visible = null)
        {
            var b = Words(new ActionButton(T(label), ButtonKind.Ghost), c => c.Content = T(label)); b.Click += (_, _) => action(); toolbar.Children.Add(b);
            toolbarRefresh.Add(() => { b.IsEnabled = enabled?.Invoke() ?? true; b.IsVisible = visible?.Invoke() ?? true; });
        }
        Action("Add page", AddPage, () => Mode == PaperFormMode.Design);
        Action("Remove page", () => RemovePage(PageIndex), () => Mode == PaperFormMode.Design, () => Definition.Pages.Count > 1);
        Action("Fit page", FitPage); Action("Fit width", FitWidth);
        Action("Fit two pages", FitTwoPages, visible: () => Definition.Pages.Count > 1);
        toolbar.Children.Add(extra); foreach (var child in toolbar.Children) child.Margin = new Thickness(0, 0, M(8), M(8));
        foreach (var refresh in toolbarRefresh) refresh();
    }
    Control Labelled(string key, Control input)
    {
        var row = Build.Labelled(T(key), input); var label = (TextBlock)row.Children[0]; Words(label, c => c.Text = T(key)); return row;
    }
    // The inspector collects (group, label, input) and lays them out once: headed blocks in a sidebar, or fixed-height
    // groups of two rows in a ribbon, so the ribbon keeps its height whatever is selected.
    readonly List<(string group, string? label, Control input)> inspectorItems = new();
    string inspectorGroup = "";
    bool RibbonInspector => InspectorPlacement is PaperInspectorPlacement.Ribbon or PaperInspectorPlacement.RibbonBottom;
    void Group(string name) => inspectorGroup = name;
    void Add(string? label, Control input) => inspectorItems.Add((inspectorGroup, label, input));
    void LayoutInspector()
    {
        inspector.Children.Clear();
        bool ribbon = RibbonInspector;
        inspector.Orientation = ribbon ? Orientation.Horizontal : Orientation.Vertical;
        foreach (var group in inspectorItems.GroupBy(i => i.group))
        {
            var heading = Words(Build.Text(T(group.Key), Text3, TextSize.Caption, 600), c => c.Text = T(group.Key));
            var block = new StackPanel { Spacing = M(4), Children = { heading } };
            var items = group.Select(i => (i.label, i.input, row: Item(i.label, i.input, ribbon))).ToArray();
            if (!ribbon)
            {
                block.Margin = new Thickness(0, 0, 0, M(12));
                foreach (var item in items) block.Children.Add(item.row);
                inspector.Children.Add(block); continue;
            }
            // two rows per column; the labels of one column share a width so the inputs line up
            var grid = new Avalonia.Controls.Grid { RowDefinitions = new RowDefinitions("Auto,Auto"), RowSpacing = M(4), ColumnSpacing = M(12) };
            for (int i = 0; i < items.Length; i++)
            {
                if (i % 2 == 0) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                Avalonia.Controls.Grid.SetRow(items[i].row, i % 2); Avalonia.Controls.Grid.SetColumn(items[i].row, i / 2); grid.Children.Add(items[i].row);
            }
            for (int column = 0; column < (items.Length + 1) / 2; column++)
            {
                var labels = items.Skip(column * 2).Take(2).Select(i => i.row).OfType<StackPanel>().Select(p => p.Children[0]).OfType<TextBlock>().ToArray();
                foreach (var label in labels) label.Measure(Size.Infinity);
                double width = labels.Length == 0 ? 0 : labels.Max(l => l.DesiredSize.Width);
                foreach (var label in labels) label.MinWidth = width;
            }
            block.Children.Add(grid);
            inspector.Children.Add(new Border { Child = block, Padding = new Thickness(0, 0, M(16), 0), Margin = new Thickness(0, 0, M(16), 0), BorderBrush = B(Look.Separator), BorderThickness = new Thickness(0, 0, 1, 0) });
        }
    }
    Control Item(string? key, Control input, bool ribbon)
    {
        if (key == null) { input.VerticalAlignment = VerticalAlignment.Center; return input; }
        var label = Words(Build.Text(T(key), Text2, ribbon ? TextSize.Caption : TextSize.Label), c => c.Text = T(key));
        label.VerticalAlignment = VerticalAlignment.Center;
        if (ribbon)
        { if (numberLayouts.Any(n => n.field == input)) input.Width = M(104); else if (input is not ComboBox || input is SearchChoice) input.Width = M(RibbonFieldWidth); }  // a dropdown sizes to its widest choice
        return new StackPanel { Orientation = ribbon ? Orientation.Horizontal : Orientation.Vertical, Spacing = M(ribbon ? 6 : 4), Children = { label, input } };
    }
    void BuildInspector()
    {
        inspectorPending = updatePending = true;
        FlushUpdate();
    }
    void BuildInspectorCore()
    {
        if (!ready) return;
        var selected = Selected;
        var layout = (string.Join("\n", SelectedIds), selected?.Kind, selected?.FieldKey.Length > 0, selected?.GridAlignment,
            Definition.Pages.Count > 1, Definition.ShowPageNumbers, Definition.PaperFormat == EditableTable.PaperKind.Custom, InspectorPlacement, RibbonFieldWidth);
        if (inspectorEditing || Equals(inspectorLayout, layout))
        {
            refreshingInspector = true;
            try { foreach (var refresh in inspectorRefresh) refresh(); }
            finally { refreshingInspector = false; }
            return;
        }
        inspectorLayout = layout;
        numberLayouts.RemoveAll(n => n.field != gridPitchInput && n.field != guidePosition);
        inspectorItems.Clear();
        inspectorRefresh.Clear();
        try { BuildInspectorItems(); }
        finally { LayoutInspector(); }
    }
    object? inspectorLayout;
    bool refreshingInspector;
    void BuildInspectorItems()
    {
        if (SelectedIds.Count > 1)
        {
            Group("Selection");
            Add(null, Build.Text(T("{0} elements selected", SelectedIds.Count), Text2));
            Add(null, Build.Text(T("Drag to move together. Ctrl-click changes the selection."), Text3, TextSize.Caption)); return;
        }
        if (Selected is not { } selected)
        {
            void Setting(string label, Func<double> get, Action<double> apply)
            {
                var field = new Field { Text = get().ToString(Culture) };
                field.Box.GotFocus += (_, _) => inspectorHasEdits = false;
                field.Edited += () => { if (!double.TryParse(field.Text, System.Globalization.NumberStyles.Float, Culture, out double number)) { field.Error = T("Enter a valid value."); return; } inspectorEditing = true; try { apply(number); field.Error = null; } catch (ArgumentException) { field.Error = T("Enter a valid value."); } finally { inspectorEditing = false; } };
                inspectorRefresh.Add(() => { if (!field.Box.IsFocused && field.Error == null) field.Text = get().ToString(Culture); });
                Words(field, c => { if (c.Error != null) c.Error = T("Enter a valid value."); }, "validation");
                NumberControls(field, GridPitch, 0, get, () => GridPitch); Add(label, field);
            }
            if (Definition.Pages.Count > 1)
            {
                Group("Page");
                void Shift(string label, int by) { var b = Words(new ActionButton(T(label), ButtonKind.Ghost), c => c.Content = T(label)); b.Click += (_, _) => MovePage(PageIndex, PageIndex + by); inspectorRefresh.Add(() => b.IsEnabled = PageIndex + by >= 0 && PageIndex + by < Definition.Pages.Count); b.IsEnabled = PageIndex + by >= 0 && PageIndex + by < Definition.Pages.Count; Add(null, b); }
                Shift("Move page earlier", -1); Shift("Move page later", 1);
            }
            Group("Fill");
            var active = new Field { Text = Definition.ActiveFieldBackground ?? "" };
            active.Edited += () =>
            {
                string? colour = string.IsNullOrWhiteSpace(active.Text) ? null : active.Text.Trim();
                if (colour != null && !Avalonia.Media.Color.TryParse(colour, out _)) { active.Error = T("Enter a valid value."); return; }
                active.Error = null; inspectorEditing = true; try { Edit(Definition with { ActiveFieldBackground = colour }); } finally { inspectorEditing = false; }
            };
            inspectorRefresh.Add(() => { if (!active.Box.IsFocused && active.Error == null) active.Text = Definition.ActiveFieldBackground ?? ""; });
            Add("Active field colour", active);
            Group("Margins");
            Setting("Left margin (mm)", () => Definition.MarginLeft, n => Edit(Definition with { MarginLeft = n }));
            Setting("Top margin (mm)", () => Definition.MarginTop, n => Edit(Definition with { MarginTop = n }));
            Setting("Right margin (mm)", () => Definition.MarginRight, n => Edit(Definition with { MarginRight = n }));
            Setting("Bottom margin (mm)", () => Definition.MarginBottom, n => Edit(Definition with { MarginBottom = n }));
            if (Definition.ShowPageNumbers)
            {
                Group("Page numbers");
                void PageChoice(string label, string[] items, Func<int> index, Func<int, PaperFormDefinition> next)
                {
                    var box = new ComboBox { ItemsSource = items.Select(i => T(i)).ToArray(), SelectedIndex = index() };
                    box.SelectionChanged += (_, _) => { if (refreshingInspector || box.SelectedIndex < 0) return; try { Edit(next(box.SelectedIndex)); } catch (ArgumentException) { box.SelectedIndex = index(); } };
                    inspectorRefresh.Add(() => box.SelectedIndex = index());
                    Add(label, box);
                }
                PageChoice("Position", new[] { "Bottom centre", "Bottom left", "Bottom right", "Top left", "Top centre", "Top right" }, () => (int)Definition.PageNumberPosition, i => Definition with { PageNumberPosition = (PaperPageNumberPosition)i });
                PageChoice("Format", new[] { "Page 1 of 3", "1 / 3", "1", "Page 1", "– 1 –" }, () => (int)Definition.PageNumberFormat, i => Definition with { PageNumberFormat = (PaperPageNumberFormat)i });
            }
            if (Definition.PaperFormat == EditableTable.PaperKind.Custom)
            {
                Group("Paper size");
                Setting("Paper width (mm)", () => Definition.CustomWidth, n => Edit(Definition with { CustomWidth = n })); Setting("Paper height (mm)", () => Definition.CustomHeight, n => Edit(Definition with { CustomHeight = n }));
            }
            return;
        }
        void Input(string label, Func<PaperStructure, string> get, Func<string, PaperStructure, PaperStructure> set, bool readOnly = false)
        {
            string text = get(selected);
            var field = new Field { Text = text };
            field.IsReadOnly = readOnly;
            bool applying = false;
            void Apply()
            {
                if (applying || Selected is not { } current || current.Id != selected.Id || field.Text == text) return;
                applying = true; inspectorEditing = true;
                try { var next = set(field.Text, current); ReplaceSelected(next); text = field.Text; field.Error = null; }
                catch (ArgumentException) { field.Error = T("Enter a valid value."); }
                catch (FormatException) { field.Error = T("Enter a valid value."); }
                catch (OverflowException) { field.Error = T("Enter a valid value."); }
                finally { applying = false; inspectorEditing = false; }
            }
            field.Box.GotFocus += (_, _) => inspectorHasEdits = false;
            field.Box.LostFocus += (_, _) =>
            {
                if (field.Error != null || Selected is not { } s || s.Id != selected.Id) return;
                applying = true;
                try { text = get(s); field.Text = text; }
                finally { applying = false; }
            };
            field.Edited += Apply;
            inspectorRefresh.Add(() => { if (!field.Box.IsFocused && field.Error == null && Selected is { } s) { text = get(s); field.Text = text; } });
            Words(field, c => { if (c.Error != null) c.Error = T("Enter a valid value."); }, "validation");
            if (!readOnly && (label.EndsWith("(mm)", StringComparison.Ordinal) || label is "Cell count" or "Font size (DIP)" or "Tick size (%)" or "Highlight from cell" or "Highlighted cells")) NumberControls(field, NumberStep(label), current: () => Parse(get(Selected ?? selected)), increment: () => NumberStep(label));
            Add(label, field);
        }
        ComboBox Choice(string label, string[] items, Func<PaperStructure, int> index, Action<int> apply)
        {
            var box = new ComboBox { ItemsSource = items.Select(i => T(i)).ToArray(), SelectedIndex = index(selected) };
            bool refreshingWords = false;
            Words(box, c => { int at = c.SelectedIndex; refreshingWords = true; c.ItemsSource = items.Select(i => T(i)).ToArray(); c.SelectedIndex = at; refreshingWords = false; });
            box.SelectionChanged += (_, _) => { if (!refreshingWords && !refreshingInspector && box.SelectedIndex >= 0) apply(box.SelectedIndex); };
            inspectorRefresh.Add(() => { if (Selected is { } s) box.SelectedIndex = index(s); });
            Add(label, box); return box;
        }
        CheckBox Flag(string label, Func<PaperStructure, bool> get, Func<PaperStructure, bool, PaperStructure> set)
        {
            var box = Words(new CheckBox { IsChecked = get(selected) }, c => c.Content = T(label));
            box.IsCheckedChanged += (_, _) => { if (!refreshingInspector && Selected is { } s) ReplaceSelected(set(s, box.IsChecked == true)); };
            inspectorRefresh.Add(() => { if (Selected is { } s) box.IsChecked = get(s); });
            Add(null, box); return box;
        }
        static int AlignmentIndex(TextAlignment a) => a == TextAlignment.Right ? 2 : a == TextAlignment.Center ? 1 : 0;
        static TextAlignment Alignment(int i) => i == 2 ? TextAlignment.Right : i == 1 ? TextAlignment.Center : TextAlignment.Left;
        double Parse(string text) => double.Parse(text, System.Globalization.NumberStyles.Float, Culture);

        Group("Element");
        // The accessible name falls back to the field key, then the title (PaperFormView.Create); the override is an advanced edit.
        if (ShowAdvancedProperties)
        {
            if (selected.FieldKey.Length > 0) Input("Field key", s => s.FieldKey, (v, s) => s, true);
            Input("Accessible label", s => s.Label, (v, s) => s with { Label = v });
        }
        if (selected.Kind != PaperStructureKind.Line) Input(selected.Kind == PaperStructureKind.Label ? "Text" : "Title", s => s.Title, (v, s) => s with { Title = v });
        if (selected.Kind == "caption") Input("Subtitle", s => s.Subtitle, (v, s) => s with { Subtitle = v });
        if (selected.Kind == "date") Input("Date format", s => s.DateFormat, (v, s) => s with { DateFormat = v });

        Group("Position");
        Input("X (mm)", s => s.Bounds.X.ToString(Culture), (v, s) => s with { Bounds = s.Bounds with { X = Parse(v) } });
        Input("Y (mm)", s => s.Bounds.Y.ToString(Culture), (v, s) => s with { Bounds = s.Bounds with { Y = Parse(v) } });
        if (selected.Kind != "characters")
        {
            Input("Width (mm)", s => s.Bounds.Width.ToString(Culture), (v, s) => s with { Bounds = s.Bounds with { Width = Parse(v) } });
            Input("Height (mm)", s => s.Bounds.Height.ToString(Culture), (v, s) => s with { Bounds = s.Bounds with { Height = Parse(v) } });
        }
        Flag("Snap to grid", s => s.SnapToGrid, (s, on) => s with { SnapToGrid = on });

        if (selected.Kind == "characters")
        {
            Group("Cells");
            Input("Cell count", s => s.CellCount.ToString(Culture), (v, s) => s with { CellCount = int.Parse(v, Culture) });
            Input("Cell width (mm)", s => s.CellWidth.ToString(Culture), (v, s) => s with { CellWidth = Parse(v) });
            Input("Cell height (mm)", s => s.Bounds.Height.ToString(Culture), (v, s) => s with { Bounds = s.Bounds with { Height = Parse(v) } });
            Input(selected.GridAlignment != TextAlignment.Center ? "Width (automatic, mm)" : "Field width (mm)", s => s.Bounds.Width.ToString(Culture), (v, s) => s with { Bounds = s.Bounds with { Width = Parse(v) } }, selected.GridAlignment != TextAlignment.Center);
            Choice("Whole grid alignment", new[] { "Left", "Center", "Right" }, s => AlignmentIndex(s.GridAlignment), i => { if (Selected is { } s) ReplaceSelected(s with { GridAlignment = Alignment(i) }); });
            // the first highlighted range is editable here; further ranges a host supplied are kept as they are
            PaperStructure Range(PaperStructure s, Func<PaperCellRange, PaperCellRange?> change)
            {
                var ranges = s.CellRanges.ToList();
                var first = ranges.Count > 0 ? ranges[0] : new PaperCellRange(0, 0, "#E6E6E6");
                int max = Math.Max(1, s.CellCount);
                var next = change(first);
                if (next != null) next = next with { Start = Math.Clamp(next.Start, 0, max - 1), Length = Math.Clamp(next.Length, 1, max - Math.Clamp(next.Start, 0, max - 1)) };
                if (ranges.Count > 0) ranges.RemoveAt(0);
                if (next != null) ranges.Insert(0, next);
                return s with { CellRanges = ranges };
            }
            Group("Highlighted cells");
            Input("Highlight from cell", s => s.CellRanges.Count > 0 ? (s.CellRanges[0].Start + 1).ToString(Culture) : "", (v, s) => Range(s, r => r with { Start = int.Parse(v, Culture) - 1, Length = Math.Max(1, r.Length) }));
            Input("Highlighted cells", s => s.CellRanges.Count > 0 ? s.CellRanges[0].Length.ToString(Culture) : "0", (v, s) => int.Parse(v, Culture) <= 0 ? Range(s, _ => null) : Range(s, r => r with { Length = int.Parse(v, Culture) }));
            ColourPicker("Highlight colour", selected, s => s.CellRanges.Count > 0 ? s.CellRanges[0].Background : null, (s, value) => Range(s, r => r with { Background = value, Length = Math.Max(1, r.Length) }), Paper);
        }
        if (selected.Kind == "line")
        {
            Group("Line");
            Choice("Line orientation", new[] { "Horizontal", "Vertical" }, s => (int)s.LineOrientation, i => { if (Selected is { } s) ReplaceSelected(s with { LineOrientation = (Orientation)i }); });
            ColourPicker("Line colour", selected, s => s.BorderColor, (s, value) => s with { BorderColor = value }, PaperRule);
            Input("Line width (mm)", s => s.BorderWidth.ToString(Culture), (v, s) => s with { BorderWidth = Parse(v) }); return;
        }

        if (selected.Kind == PaperStructureKind.Image)
        {
            Group("Image");
            var choose = Words(new ActionButton(T(selected.Image == null ? "Choose image" : "Replace image")), c => c.Content = T(Selected?.Image == null ? "Choose image" : "Replace image"));
            choose.Click += (_, _) => ChooseImage(); Add(null, choose);
            Flag("Keep aspect ratio", s => s.KeepAspectRatio, (s, on) => on && KeepsAspect(s with { KeepAspectRatio = true }) is { } r ? s with { KeepAspectRatio = true, Bounds = s.Bounds with { Height = s.Bounds.Width * r } } : s with { KeepAspectRatio = on });
            Group("Colours");
            ColourPicker("Border colour", selected, s => s.BorderColor, (s, value) => s with { BorderColor = value }, PaperRule);
            Input("Border (mm)", s => s.BorderWidth.ToString(Culture), (v, s) => s with { BorderWidth = Parse(v) }); return;
        }
        Group("Text");
        FontPicker(selected);
        Input("Font size (DIP)", s => s.FontSize.ToString(Culture), (v, s) => s with { FontSize = Parse(v) });
        if (selected.Kind == "check") Choice("Tick style", new[] { "Check mark", "Filled square", "Cross" }, s => (int)s.CheckMark, i => { if (Selected is { } s) ReplaceSelected(s with { CheckMark = (PaperCheckMark)i }); });
        if (selected.Kind == "check") Input("Tick size (%)", s => Math.Round((s.CheckMarkSize ?? (s.CheckMark == PaperCheckMark.Check ? .48 : .4)) * 100, 1).ToString(Culture), (v, s) => s with { CheckMarkSize = Parse(v) / 100 });
        Choice("Font weight", new[] { "Regular", "Bold" }, s => s.FontWeight >= FontWeight.SemiBold ? 1 : 0, i => { if (Selected is { } s) ReplaceSelected(s with { FontWeight = i == 1 ? FontWeight.Bold : FontWeight.Normal }); });
        Choice("Font style", new[] { "Normal", "Italic" }, s => s.FontStyle == FontStyle.Normal ? 0 : 1, i => { if (Selected is { } s) ReplaceSelected(s with { FontStyle = i == 1 ? FontStyle.Italic : FontStyle.Normal }); });
        Choice("Text alignment", new[] { "Left", "Center", "Right" }, s => AlignmentIndex(s.Alignment), i => { if (Selected is { } s) ReplaceSelected(s with { Alignment = Alignment(i) }); });
        if (selected.Kind is "text" or "multiline" or "caption" or "date" or "label")
            Choice("Vertical alignment", new[] { "Automatic", "Top", "Center", "Bottom" }, s => s.VerticalAlignment is { } v ? (int)v + 1 : 0, i => { if (Selected is { } s) ReplaceSelected(s with { VerticalAlignment = i == 0 ? null : (PaperVerticalAlignment)(i - 1) }); });
        if (selected.Kind is "text" or "multiline" or "caption") Input("Text padding (mm)", s => s.TextPadding.ToString(Culture), (v, s) => s with { TextPadding = Parse(v) });
        if (selected.Kind == "multiline") ToolTip.SetTip(Flag("Allow scrolling", s => s.AllowMultilineScrolling, (s, on) => s with { AllowMultilineScrolling = on }), T("Typing may go past the visible box; export still needs the text to fit."));

        Group("Colours");
        ColourPicker("Fill colour", selected, s => s.Background, (s, value) => s with { Background = value }, Paper, () => Definition.Background);
        ColourPicker("Text colour", selected, s => s.Foreground, (s, value) => s with { Foreground = value }, PaperInk, () => Definition.Foreground);
        ColourPicker("Border colour", selected, s => s.BorderColor, (s, value) => s with { BorderColor = value }, PaperRule);
        Input("Border (mm)", s => s.BorderWidth.ToString(Culture), (v, s) => s with { BorderWidth = Parse(v) });
        if (selected.Kind is "text" or "multiline" or "caption" or "date")
        {
            Choice("Border style", new[] { "Full outline", "Bottom rule", "Bottom rule with end ticks", "No border" }, s => (int)s.BorderStyle, i => { if (Selected is { } s) ReplaceSelected(s with { BorderStyle = (PaperBorderStyle)i }); });
            if (selected.BorderStyle == PaperBorderStyle.BottomTicks) Input("End tick height (mm)", s => s.EndTickHeight.ToString(Culture), (v, s) => s with { EndTickHeight = Parse(v) });
        }
    }

    void Edit(PaperFormDefinition next)
    {
        using var update = new UpdateScope(this);
        if (Mode != PaperFormMode.Design || next == Definition) return;
        next.ValidateForAssignment(); if (!inspectorEditing || !inspectorHasEdits) Remember(undo, (Definition, SelectedIds.ToArray(), PageIndex)); if (inspectorEditing) inspectorHasEdits = true; redo.Clear(); Apply(next); Changed?.Invoke();
    }
    void Apply(PaperFormDefinition next)
    {
        editing = true;
        try { SetCurrentValue(DefinitionProperty, next); }
        finally { editing = false; }  // the Definition change rebuilds the toolbar and inspector once
    }
    PaperFormDefinition WithStructures(IEnumerable<PaperStructure> structures)
    {
        var pages = Definition.Pages.ToArray(); int page = Math.Min(PageIndex, pages.Length - 1);
        pages[page] = pages[page] with { Structures = structures.ToArray() }; return Definition with { Pages = pages };
    }
    public void ReplaceSelected(PaperStructure structure)
    {
        using var update = new UpdateScope(this);
        if (Selected is not { } current) return;
        if (structure.Id != current.Id) throw new ArgumentException("A replacement must preserve its structure ID.", nameof(structure));
        if (structure.Kind == "characters" && structure.GridAlignment != TextAlignment.Center)
        {
            int cells = structure.CellCount; bool sameWidth = structure.Bounds.Width == current.Bounds.Width;
            if (structure.CellCount == current.CellCount && structure.CellWidth == current.CellWidth && structure.BorderWidth == current.BorderWidth && structure.Bounds.Width != current.Bounds.Width)
                cells = Math.Clamp((int)Math.Round(structure.Bounds.Width / structure.CellWidth), 1, 1000);
            // a right-aligned grid mirrors a left-aligned one: it keeps its right edge when the cells change, and resizes by whole cells
            double x = structure.GridAlignment == TextAlignment.Right && sameWidth ? current.Bounds.X + current.Bounds.Width - cells * structure.CellWidth : structure.Bounds.X;
            structure = structure with { CellCount = cells, Bounds = structure.Bounds with { X = x, Width = cells * structure.CellWidth } };
        }
        if (structure.Kind == "characters" && structure.GridAlignment == TextAlignment.Center && structure.Bounds.Width == current.Bounds.Width)
        {
            // more or wider cells than the field holds: grow it from the aligned edge, so the block stays where it was
            double grow = structure.CellCount * structure.CellWidth - structure.Bounds.Width;
            if (grow > 0) structure = structure with { Bounds = structure.Bounds with { X = structure.Bounds.X - (structure.GridAlignment == TextAlignment.Right ? grow : grow / 2), Width = structure.Bounds.Width + grow } };
        }
        if (structure.Kind == "check")
        {
            double side = structure.Bounds.Width != current.Bounds.Width ? structure.Bounds.Width : structure.Bounds.Height;
            structure = structure with { Bounds = structure.Bounds with { Width = side, Height = side } };
        }
        if (KeepsAspect(structure) is { } ratio)
        {
            bool byWidth = structure.Bounds.Width != current.Bounds.Width || structure.Bounds.Height == current.Bounds.Height;
            structure = structure with { Bounds = byWidth ? structure.Bounds with { Height = structure.Bounds.Width * ratio } : structure.Bounds with { Width = structure.Bounds.Height / ratio } };
        }
        structure = FitGrid(structure);
        if (structure == current) return;
        if (structure.Bounds != current.Bounds)
        {
            var absolute = Placements(Structures).First(p => p.structure.Id == current.Id).bounds;
            PlaceSelected(structure with { Bounds = structure.Bounds with { X = absolute.X + structure.Bounds.X - current.Bounds.X, Y = absolute.Y + structure.Bounds.Y - current.Bounds.Y } });
            return;
        }
        PaperStructure Replace(PaperStructure s) => s.Id == current.Id ? structure : WithChildren(s, MapStructures(s.Children, Replace));
        Edit(WithStructures(Structures.Select(Replace)));
    }
    public void AddStructure(PaperStructure structure)
    {
        using var update = new UpdateScope(this);
        if (Mode != PaperFormMode.Design) return;
        structure = NameNewStructures(structure);
        structure = FitGrid(structure);
        Edit(WithStructures(InsertInto(Structures, structure))); SetCurrentValue(SelectedIdProperty, structure.Id);
    }
    public void DeleteSelected()
    {
        using var update = new UpdateScope(this);
        if (SelectedIds.Count == 0 || Mode != PaperFormMode.Design) return;
        var selected = SelectedIds.ToHashSet(StringComparer.Ordinal);
        IReadOnlyList<PaperStructure> Remove(IReadOnlyList<PaperStructure> items) => MapStructures(items, s => selected.Contains(s.Id) ? null : WithChildren(s, Remove(s.Children)));
        Edit(Definition with
        {
            Pages = Definition.Pages.Select(p =>
        {
            var structures = Remove(p.Structures);
            return ReferenceEquals(structures, p.Structures) ? p : p with { Structures = structures };
        }).ToArray()
        }); SetSelection(Array.Empty<string>());
    }
    public void DuplicateSelected()
    {
        if (Mode == PaperFormMode.Design && CopySnapshot() is { } copy) PasteSnapshot(copy, null);
    }
    public void Undo() { using var update = new UpdateScope(this); if (Mode != PaperFormMode.Design || !undo.TryPop(out var previous)) return; Remember(redo, (Definition, SelectedIds.ToArray(), PageIndex)); Apply(previous.definition); SetSelection(previous.selected, previous.page); Changed?.Invoke(); }
    public void Redo() { using var update = new UpdateScope(this); if (Mode != PaperFormMode.Design || !redo.TryPop(out var next)) return; Remember(undo, (Definition, SelectedIds.ToArray(), PageIndex)); Apply(next.definition); SetSelection(next.selected, next.page); Changed?.Invoke(); }
    /// <summary>Remove one page and its structures (undoable); field values stay in Values. The last page cannot be removed.</summary>
    public void RemovePage(int pageIndex)
    {
        using var update = new UpdateScope(this);
        if (Mode != PaperFormMode.Design || Definition.Pages.Count < 2 || pageIndex < 0 || pageIndex >= Definition.Pages.Count) return;
        SetSelection(Array.Empty<string>());
        Edit(Definition with { Pages = Definition.Pages.Where((_, i) => i != pageIndex).ToArray() });
        SetCurrentValue(PageIndexProperty, Math.Min(pageIndex, Definition.Pages.Count - 1));
    }
    /// <summary>The height / width of an image structure that keeps its proportions, or null.</summary>
    double? KeepsAspect(PaperStructure s) => s.Kind == PaperStructureKind.Image && s.KeepAspectRatio && s.Image != null && ImagePixels(s.Image) is { } p ? p.Height / p.Width : null;
    /// <summary>Pick a PNG, JPEG or SVG for the selected image structure; it is stored in the layout as a data URI.</summary>
    async void ChooseImage()
    {
        if (Selected is not { Kind: PaperStructureKind.Image } target || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;
        var files = await storage.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = T("Choose image"),
            AllowMultiple = false,
            FileTypeFilter = new[] { new Avalonia.Platform.Storage.FilePickerFileType(T("Images")) { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.svg" }, MimeTypes = new[] { "image/png", "image/jpeg", "image/svg+xml" } } }
        });
        if (files.Count == 0) return;
        try
        {
            await using var stream = await files[0].OpenReadAsync();
            using var copy = new System.IO.MemoryStream(); var buffer = new byte[81920]; int read;
            // stop at the limit: a huge file must not be read into memory just to be refused
            while ((read = await stream.ReadAsync(buffer)) > 0)
            {
                copy.Write(buffer, 0, read);
                if (copy.Length > MaxImageBytes) throw new ArgumentException(T("Use an image of at most 10 MB."));
            }
            UseImage(target.Id, copy.ToArray());
        }
        // an async void method: an exception that escapes here ends the application
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException or OperationCanceledException or System.Runtime.InteropServices.ExternalException) { feedback.Text = T("The image could not be read."); }
        catch (ArgumentException e) { feedback.Text = e.Message; }
    }
    /// <summary>Put PNG, JPEG or SVG bytes into an image structure; with its aspect kept, the height follows the picture.</summary>
    internal void UseImage(string id, byte[] bytes)
    {
        if (bytes.Length > MaxImageBytes) throw new ArgumentException(T("Use an image of at most 10 MB."));
        string type = IsSvg(bytes) ? "svg+xml" : bytes.Length > 4 && bytes[0] == 0x89 && bytes[1] == 0x50 ? "png" : "jpeg";
        string uri = $"data:image/{type};base64,{Convert.ToBase64String(bytes)}";
        if (ImagePixels(uri) == null) throw new ArgumentException(T("Use a PNG, JPEG or self-contained SVG image."));
        if (Selected is not { } s || s.Id != id) SetSelection(new[] { id });
        if (Selected is not { } current) return;
        var next = current with { Image = uri };
        if (KeepsAspect(next) is { } ratio) next = next with { Bounds = next.Bounds with { Height = next.Bounds.Width * ratio } };
        ReplaceSelected(next);
    }
    /// <summary>Move a page to another position (undoable); its structures, guides and the field values stay as they are.</summary>
    public void MovePage(int from, int to)
    {
        using var update = new UpdateScope(this);
        if (Mode != PaperFormMode.Design || from == to || from < 0 || to < 0 || from >= Definition.Pages.Count || to >= Definition.Pages.Count) return;
        var pages = Definition.Pages.ToList(); var page = pages[from]; pages.RemoveAt(from); pages.Insert(to, page);
        SetSelection(Array.Empty<string>()); Edit(Definition with { Pages = pages.ToArray() }); SetCurrentValue(PageIndexProperty, to);
    }
    public void AddPage() { using var update = new UpdateScope(this); if (Mode != PaperFormMode.Design) return; int page = Definition.Pages.Count; Edit(Definition with { Pages = Definition.Pages.Append(new PaperPage()).ToArray() }); SetCurrentValue(PageIndexProperty, page); SetCurrentValue(SelectedIdProperty, null); }

    sealed class StaticSurface(PaperFormBuilder owner, int pageIndex, bool rulers) : Control, IRestyle
    {
        object? geometry;
        internal void Refresh()
        {
            var next = (owner.Definition.PageSize, owner.Definition.MarginLeft, owner.Definition.MarginTop, owner.Definition.MarginRight, owner.Definition.MarginBottom,
                owner.Definition.Background, owner.Definition.Foreground, owner.GridPitch, owner.Zoom, owner.ShowGrid, owner.ShowRulers);
            if (!Equals(geometry, next)) { geometry = next; InvalidateVisual(); }
        }
        public void Restyle() => InvalidateVisual();
        public override void Render(DrawingContext context)
        {
            using var translated = context.PushTransform(Matrix.CreateTranslation(owner.RulerInset, owner.RulerInset));
            var pageSize = new Size(owner.Pages[pageIndex].Width, owner.Pages[pageIndex].Height);
            if (rulers) { owner.PaintRulers(context, pageSize); return; }
            if (owner.ShowGrid)
            {
                var paper = (owner.Pages[pageIndex].Background as ISolidColorBrush)?.Color ?? Paper.Color;
                var ink = owner.Definition.Foreground == null ? PaperInk.Color : Color.Parse(owner.Definition.Foreground);
                var pen = new Pen(new SolidColorBrush(Blend(paper, ink, .12)), .5); double pitch = owner.GridPitch * DipsPerMm;
                for (double x = 0; x <= pageSize.Width; x += pitch) context.DrawLine(pen, new Point(x, 0), new Point(x, pageSize.Height));
                for (double y = 0; y <= pageSize.Height; y += pitch) context.DrawLine(pen, new Point(0, y), new Point(pageSize.Width, y));
            }
        }
    }
    sealed class GuideSurface(DesignSurface surface) : Control
    {
        public override void Render(DrawingContext context) => surface.PaintGuides(context);
    }
    sealed class DesignSurface : Control
    {
        protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new Avalonia.Automation.Peers.ControlAutomationPeer(this);
        readonly PaperFormBuilder owner;
        readonly int pageIndex;
        internal readonly StaticSurface Background, GridBackground;
        internal readonly GuideSurface GuideLayer;
        Rect[] snapCandidates = Array.Empty<Rect>();
        internal void Redraw() { InvalidateVisual(); GuideLayer.InvalidateVisual(); }
        internal void PaintGuides(DrawingContext context)
        {
            using var translated = context.PushTransform(Matrix.CreateTranslation(owner.RulerInset, owner.RulerInset));
            var pageSize = new Size(owner.Pages[pageIndex].Width, owner.Pages[pageIndex].Height);
            if (owner.ShowGuides)
                foreach (var guide in owner.Definition.Pages[pageIndex].Guides.Concat(guideDraft == null ? Array.Empty<PaperGuide>() : new[] { guideDraft }))
                {
                    double at = guide.Position * DipsPerMm;
                    if (at > (guide.Orientation == PaperGuideOrientation.Vertical ? pageSize.Width : pageSize.Height)) continue;
                    bool chosen = owner.selectedGuides.Contains(guide.Id);
                    context.DrawLine(chosen ? new Pen(B(FocusBorder), 2 / owner.Zoom) : new Pen(B(FocusBorder), 1 / owner.Zoom, DashStyle.Dash), guide.Orientation == PaperGuideOrientation.Vertical ? new Point(at, 0) : new Point(0, at), guide.Orientation == PaperGuideOrientation.Vertical ? new Point(at, pageSize.Height) : new Point(pageSize.Width, at));
                }
        }
        Point start; PaperBounds? draft, originalBounds; PaperStructure? moving; bool resize, captured, marquee, additiveSelection; string[] initialSelection = Array.Empty<string>(); IPointer? pointer;
        PaperGuide? guideDraft; string? guideId;
        IReadOnlyList<(Point a, Point b)> snapMarks = Array.Empty<(Point, Point)>();
        public DesignSurface(PaperFormBuilder owner, int pageIndex)
        {
            this.owner = owner; this.pageIndex = pageIndex; Focusable = true;
            Background = new StaticSurface(owner, pageIndex, true) { IsHitTestVisible = false, ZIndex = int.MaxValue - 4 };
            GuideLayer = new GuideSurface(this) { IsHitTestVisible = false, ZIndex = int.MaxValue - 3 };
            GridBackground = new StaticSurface(owner, pageIndex, false) { IsHitTestVisible = false, ZIndex = int.MaxValue - 2 };
            AddHandler(RequestBringIntoViewEvent, (_, e) => e.Handled = true); Words(this, c => AutomationProperties.SetName(c, T("Paper design grid")));
            ContextRequested += (_, e) => { owner.ActivatePage(pageIndex); Focus(); var point = e.TryGetPosition(this, out var at) ? Mm(at) : start; owner.InsertMenu(this, point); e.Handled = true; };
        }
        double Snap(double n) => Math.Round(n / owner.GridPitch) * owner.GridPitch;
        Point Mm(Point p) => new((p.X - owner.RulerInset) / DipsPerMm, (p.Y - owner.RulerInset) / DipsPerMm);
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e); if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            owner.ActivatePage(pageIndex); Focus(); start = Mm(e.GetPosition(this)); moving = null; additiveSelection = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            var raw = e.GetPosition(this); var guide = owner.GuideAt(start);
            if (guide != null && owner.ToolKind == null && e.ClickCount == 2) { owner.RemoveGuide(guide.Id); e.Handled = true; return; }  // double-click removes a guide
            if (guide != null && owner.ToolKind == null) owner.SelectGuide(guide.Id);
            else if (owner.selectedGuides.Count > 0) owner.SelectGuide(null);
            if (owner.ShowRulers && (raw.X < owner.RulerInset || raw.Y < owner.RulerInset) || guide != null && owner.ToolKind == null)
            {
                var orientation = guide?.Orientation ?? (raw.Y < owner.RulerInset ? PaperGuideOrientation.Horizontal : PaperGuideOrientation.Vertical);
                double position = orientation == PaperGuideOrientation.Vertical ? start.X : start.Y;
                guideId = guide?.Id; guideDraft = guide ?? new PaperGuide("", orientation, Math.Max(0, Snap(position)));
                owner.SetSelection(Array.Empty<string>()); captured = true; pointer = e.Pointer; e.Pointer.Capture(this); e.Handled = true; Redraw(); return;
            }
            if (owner.ToolKind == null)
            {
                var hit = owner.HitCanvas(start, e.ClickCount > 1);
                owner.SelectCanvas(hit.structure, e.KeyModifiers); moving = hit.structure; originalBounds = hit.bounds;
                marquee = moving == null; initialSelection = owner.SelectedIds.ToArray();
                if (marquee) { draft = new(start.X, start.Y, 0, 0); captured = true; pointer = e.Pointer; e.Pointer.Capture(this); e.Handled = true; return; }
                resize = owner.SelectedIds.Count == 1 && Math.Abs(start.X - originalBounds.Rect.Right) * DipsPerMm < 10 && Math.Abs(start.Y - originalBounds.Rect.Bottom) * DipsPerMm < 10;
                draft = originalBounds; snapCandidates = owner.SnapCandidates(moving!);
            }
            else draft = new(Snap(start.X), Snap(start.Y), owner.GridPitch, owner.GridPitch);
            captured = true; pointer = e.Pointer; e.Pointer.Capture(this); e.Handled = true; Redraw();
        }
        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e); if (!captured) return;
            var p = Mm(e.GetPosition(this));
            if (guideDraft is { } guide)
            {
                double maximum = guide.Orientation == PaperGuideOrientation.Vertical ? owner.Definition.PageSize.Width : owner.Definition.PageSize.Height;
                guideDraft = guide with { Position = Math.Clamp(Snap(guide.Orientation == PaperGuideOrientation.Vertical ? p.X : p.Y), 0, maximum) }; Redraw(); e.Handled = true; return;
            }
            if (moving != null && originalBounds is { } b)
                draft = resize ? b with { Width = Math.Max(owner.GridPitch, Snap(b.Width + p.X - start.X)), Height = moving.Kind == "characters" ? b.Height : Math.Max(owner.GridPitch, Snap(b.Height + p.Y - start.Y)) } : b with { X = moving.SnapToGrid ? Snap(b.X + p.X - start.X) : b.X + p.X - start.X, Y = moving.SnapToGrid ? Snap(b.Y + p.Y - start.Y) : b.Y + p.Y - start.Y };
            else draft = new(Math.Min(Snap(start.X), Snap(p.X)), Math.Min(Snap(start.Y), Snap(p.Y)), Math.Max(owner.GridPitch, Math.Abs(Snap(p.X) - Snap(start.X))), Math.Max(owner.GridPitch, Math.Abs(Snap(p.Y) - Snap(start.Y))));
            if (resize && moving != null && owner.KeepsAspect(moving) is { } ratio) draft = draft with { Height = draft.Width * ratio };
            if (resize && moving?.Kind == "check")
            {
                double delta = Math.Abs(p.X - start.X) >= Math.Abs(p.Y - start.Y) ? p.X - start.X : p.Y - start.Y;
                double side = Math.Max(owner.GridPitch, Snap(originalBounds!.Width + delta)); draft = draft with { Width = side, Height = side };
            }
            if (moving != null && (!resize || moving.Kind is not ("characters" or "check") && owner.KeepsAspect(moving) == null)) draft = owner.SnapToNeighbours(draft, moving, resize, snapCandidates, out snapMarks);
            if (resize && moving?.Kind == "characters" && moving.GridAlignment != TextAlignment.Center) draft = draft with { Width = Math.Clamp((int)Math.Round(draft.Width / moving.CellWidth), 1, 1000) * moving.CellWidth };
            Redraw(); e.Handled = true;
        }
        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (captured && guideDraft is { } guide)
            {
                var id = guideId; captured = false; guideDraft = null; e.Pointer.Capture(null);
                if (id == null) owner.AddGuide(guide.Orientation, guide.Position); else owner.MoveGuide(id, guide.Position);
                Redraw(); e.Handled = true; return;
            }
            if (!captured || draft == null) return;
            var bounds = draft; var moved = moving; var origin = originalBounds; bool selecting = marquee, resizing = resize; var initial = initialSelection; captured = false; draft = null; e.Pointer.Capture(null);
            if (selecting)
            {
                var inside = owner.Placements(owner.Structures).Where(p => bounds.Rect.Contains(p.bounds.Rect)).Select(p => p.structure.Id);
                owner.SetSelection(initial.Concat(inside));
            }
            else if (moved is { } s && origin != null)
            {
                if (resizing)
                {
                    if (s.Kind == "characters") s = s with { CellCount = Math.Clamp(s.CellCount + (int)Math.Round((bounds.Width - origin.Width) / s.CellWidth), 1, 1000) };
                    owner.PlaceSelected(s with { Bounds = bounds });
                }
                else if (bounds.X == origin.X && bounds.Y == origin.Y && !additiveSelection) owner.SetSelection(new[] { s.Id });
                else owner.MoveSelection(bounds.X - origin.X, bounds.Y - origin.Y);
            }
            else if (owner.ToolKind is { } kind)
            {
                owner.InsertAt(owner.NewStructure(kind, bounds), new Point(bounds.X, bounds.Y));
                if (kind == PaperStructureKind.Image) owner.ChooseImage();  // a drawn image asks for its picture right away
                owner.SetCurrentValue(ToolKindProperty, null); owner.BuildToolbar();
            }
            moving = null; Redraw(); e.Handled = true;
        }
        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e) { base.OnPointerCaptureLost(e); captured = false; marquee = false; guideDraft = null; guideId = null; draft = null; moving = null; originalBounds = null; pointer = null; snapMarks = Array.Empty<(Point, Point)>(); snapCandidates = Array.Empty<Rect>(); Redraw(); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e); owner.ActivatePage(pageIndex);
            bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
            if (ctrl && e.Key == Key.C) { _ = owner.CopySelectedAsync(); e.Handled = true; return; }
            if (ctrl && e.Key == Key.V) { _ = owner.PasteAsync(); e.Handled = true; return; }
            if (e.Key == Key.Escape) { pointer?.Capture(null); captured = false; draft = null; owner.SetCurrentValue(ToolKindProperty, null); Redraw(); e.Handled = true; return; }
            if (ctrl && e.Key == Key.Z) { if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) owner.Redo(); else owner.Undo(); e.Handled = true; return; }
            if (ctrl && e.Key == Key.Y) { owner.Redo(); e.Handled = true; return; }
            if (ctrl && e.Key == Key.D) { owner.DuplicateSelected(); e.Handled = true; return; }
            if (e.Key == Key.Delete) { owner.DeleteSelected(); e.Handled = true; return; }
            if (e.Key is Key.PageDown or Key.PageUp) { var items = All(owner.Structures).ToArray(); if (items.Length > 0) { int at = Array.FindIndex(items, s => s.Id == owner.SelectedId); owner.SetCurrentValue(SelectedIdProperty, items[(at + (e.Key == Key.PageDown ? 1 : items.Length - 1) + items.Length) % items.Length].Id); } e.Handled = true; return; }
            if (owner.Selected is { } s && e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
            {
                double step = owner.GridPitch * (e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 10 : 1);
                owner.MoveSelection(e.Key == Key.Left ? -step : e.Key == Key.Right ? step : 0, e.Key == Key.Up ? -step : e.Key == Key.Down ? step : 0); e.Handled = true;
            }
        }
        public override void Render(DrawingContext context)
        {
            context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
            using var translated = context.PushTransform(Matrix.CreateTranslation(owner.RulerInset, owner.RulerInset));
            var pageSize = new Size(owner.Pages[pageIndex].Width, owner.Pages[pageIndex].Height);
            var d = owner.Definition; var size = d.PageSize;
            foreach (var (a, end) in snapMarks)
            {
                var pen = new Pen(B(FocusBorder), 1);
                context.DrawLine(pen, new Point(a.X * DipsPerMm, a.Y * DipsPerMm), new Point(end.X * DipsPerMm, end.Y * DipsPerMm));
                context.DrawEllipse(B(FocusBorder), null, new Point(a.X * DipsPerMm, a.Y * DipsPerMm), 2, 2);
                context.DrawEllipse(B(FocusBorder), null, new Point(end.X * DipsPerMm, end.Y * DipsPerMm), 2, 2);
            }
            context.DrawRectangle(null, new Pen(B(Outline), .5, DashStyle.Dash), new Rect(d.MarginLeft * DipsPerMm, d.MarginTop * DipsPerMm, (size.Width - d.MarginLeft - d.MarginRight) * DipsPerMm, (size.Height - d.MarginTop - d.MarginBottom) * DipsPerMm));
            if (marquee && draft is { } selection)
                context.DrawRectangle(B(Selection), new Pen(B(FocusBorder), 1), new Rect(selection.X * DipsPerMm, selection.Y * DipsPerMm, selection.Width * DipsPerMm, selection.Height * DipsPerMm));
            if (owner.PageIndex != pageIndex) return;
            foreach (var (_, original) in owner.SelectedRoots(owner.Structures))
            {
                var b = original;
                if (!marquee && draft is { } preview && originalBounds is { } origin)
                    b = resize ? preview : b with { X = b.X + preview.X - origin.X, Y = b.Y + preview.Y - origin.Y };
                var r = new Rect(b.X * DipsPerMm, b.Y * DipsPerMm, b.Width * DipsPerMm, b.Height * DipsPerMm);
                context.DrawRectangle(null, new Pen(B(FocusBorder), 1), r);
                if (owner.SelectedIds.Count == 1) context.FillRectangle(B(FocusBorder), new Rect(r.Right - 6, r.Bottom - 6, 6, 6));
            }
        }
    }
}

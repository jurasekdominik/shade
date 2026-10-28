using System;
using System.Collections.Generic;
using System.Globalization;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using static Shade.Look;

namespace Shade;

public sealed partial class EditableTable
{
    /// <summary>Show the integrated column toolbar and inline editor. Off preserves the existing table layout.</summary>
    public static readonly DirectProperty<EditableTable, bool> ShowColumnEditorProperty = AvaloniaProperty.RegisterDirect<EditableTable, bool>(nameof(ShowColumnEditor), x => x.ShowColumnEditor, (x, v) => x.ShowColumnEditor = v);
    public bool ShowColumnEditor { get => showColumnEditor; set { if (SetAndRaise(ShowColumnEditorProperty, ref showColumnEditor, value)) { if (value && !columnEditorCreated) BuildColumnEditor(); Rebuild(); } } }
    /// <summary>The built-in toolbar buttons; clear a flag to hide that action (the table still works through code and the column menu).</summary>
    [Flags] public enum ToolbarAction { None = 0, EditColumns = 1, MoveColumn = 2, RemoveColumn = 4, AddColumn = 8, CalculatedColumn = 16, All = 31 }
    /// <summary>Where a bar sits around the grid. Hidden removes the Add rows bar, or the toolbar's buttons (the open column editor stays on top).</summary>
    public enum BarPlacement { Top, Bottom, Hidden }
    public static readonly StyledProperty<ToolbarAction> ToolbarActionsProperty = AvaloniaProperty.Register<EditableTable, ToolbarAction>(nameof(ToolbarActions), ToolbarAction.All, validate: value => (value & ~ToolbarAction.All) == 0);
    public static readonly StyledProperty<BarPlacement> ToolbarPlacementProperty = AvaloniaProperty.Register<EditableTable, BarPlacement>(nameof(ToolbarPlacement), BarPlacement.Top, validate: value => Enum.IsDefined(value));
    public static readonly StyledProperty<BarPlacement> AddRowsPlacementProperty = AvaloniaProperty.Register<EditableTable, BarPlacement>(nameof(AddRowsPlacement), BarPlacement.Bottom, validate: value => Enum.IsDefined(value));
    /// <summary>The ribbon's tabs; clear a flag to hide that tab and its fields.</summary>
    [Flags] public enum RibbonTab { None = 0, Column = 1, Text = 2, Width = 4, Table = 8, All = 15 }
    /// <summary>An app's own setting on the current column, shown at the end of the Column tab as a text field.</summary>
    public sealed record ColumnField(string Label, Func<Column, string> Read, Action<Column, string> Write, double Width = 160);
    public static readonly StyledProperty<RibbonTab> RibbonTabsProperty = AvaloniaProperty.Register<EditableTable, RibbonTab>(nameof(RibbonTabs), RibbonTab.All, validate: value => (value & ~RibbonTab.All) == 0);
    public static readonly StyledProperty<IReadOnlyList<string>?> HiddenRibbonFieldsProperty = AvaloniaProperty.Register<EditableTable, IReadOnlyList<string>?>(nameof(HiddenRibbonFields));
    /// <summary>Which ribbon tabs show (all by default). With one left the tab strip goes away.</summary>
    public RibbonTab RibbonTabs { get => GetValue(RibbonTabsProperty); set => SetValue(RibbonTabsProperty, value); }
    /// <summary>Ribbon fields to hide, by their (English) label, e.g. "Footer"; a label shared by two fields hides both.</summary>
    public IReadOnlyList<string>? HiddenRibbonFields { get => GetValue(HiddenRibbonFieldsProperty); set => SetValue(HiddenRibbonFieldsProperty, value); }
    /// <summary>The app's own column settings in the ribbon. shortcut: fields can be added but not removed once the ribbon is built.</summary>
    public ObservableCollection<ColumnField> ColumnFields { get; } = new();
    bool Hidden(string label) => HiddenRibbonFields?.Contains(label) == true;
    /// <summary>Which built-in toolbar buttons show (all by default); the other groups close up.</summary>
    public ToolbarAction ToolbarActions { get => GetValue(ToolbarActionsProperty); set => SetValue(ToolbarActionsProperty, value); }
    /// <summary>The toolbar above the table (default) or below it, with the open column editor beside it.</summary>
    public BarPlacement ToolbarPlacement { get => GetValue(ToolbarPlacementProperty); set => SetValue(ToolbarPlacementProperty, value); }
    /// <summary>The Add rows bar below the table (default), above it, or removed.</summary>
    public BarPlacement AddRowsPlacement { get => GetValue(AddRowsPlacementProperty); set => SetValue(AddRowsPlacementProperty, value); }
    /// <summary>The app's own controls in the toolbar, between the built-in groups.</summary>
    public Controls ToolbarItems => toolbarItems.Children;
    /// <summary>The app's own controls at the end of the Add rows bar.</summary>
    public Controls AddRowsItems => addRowsItems.Children;
    readonly StackPanel toolbarItems = new() { Orientation = Orientation.Horizontal, Spacing = M(8), IsVisible = false };
    StackPanel? toolbarLeft, toolbarRight;
    bool showColumnEditor, syncingColumnEditor, columnEditorCreated;
    readonly List<Action<Column>> columnEditorSync = new();
    readonly List<(Field Field, int Pane, Func<bool> Applies, Action Validate, Action Commit, Action Reset)> ribbonFields = new();
    readonly StackPanel columnEditorHead = new(), columnRibbon = new();
    readonly Border columnToolbarFrame = new(), columnRibbonFrame = new();
    readonly TextTabs columnTabs = new("Column", "Text", "Width", "Table");
    readonly List<WrapPanel> columnPanes = new();
    readonly StackPanel formulaTerms = new() { Spacing = M(8) };
    readonly StackPanel formulaEditor = new() { Spacing = M(4) };
    Control? unitsEditor, fixedWidthEditor;
    readonly ActionButton editColumns = new("Edit columns", ButtonKind.Ghost), newColumn = new("Add column", ButtonKind.Ghost, Icons.Plus), computedColumn = new("Calculated column", ButtonKind.Ghost);
    readonly ActionButton moveLeft = new("", ButtonKind.Ghost, Icons.ChevronLeft), moveRight = new("", ButtonKind.Ghost, Icons.ChevronRight), removeColumn = new("Remove column", ButtonKind.Ghost);

    Control CreateColumnEditor(Control grid)
    {
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), Children = { columnEditorHead, grid } }; Grid.SetRow(grid, 1);
        return root;
    }
    void BuildColumnEditor()
    {
        columnEditorCreated = true;
        foreach (var name in new[] { "Column", "Text", "Width", "Table" }) columnPanes.Add(new WrapPanel { Orientation = Orientation.Horizontal });
        columnRibbon.Spacing = M(12); columnRibbon.Children.Add(columnTabs);
        columnTabs.Changed += ShowColumnPane;
        Words(editColumns, b => b.Content = T(Design ? "Done" : "Edit columns"));
        Words(newColumn, b => b.Content = T("Add column")); Words(computedColumn, b => b.Content = T("Calculated column")); Words(removeColumn, b => b.Content = T("Remove column"));
        Words(columnTabs, tabs => { var names = new[] { "Column", "Text", "Width", "Table" }; for (int i = 0; i < names.Length; i++) tabs.SetName(i, T(names[i])); });
        var left = toolbarLeft = new StackPanel { Orientation = Orientation.Horizontal, Spacing = M(8), Children = { editColumns, moveLeft, moveRight, removeColumn } };
        var right = toolbarRight = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = M(8), Children = { newColumn, computedColumn } };
        toolbarItems.Children.CollectionChanged += (_, _) => SyncColumnEditor();
        editColumns.MinWidth = M(120);
        foreach (var button in new[] { moveLeft, moveRight }) { button.MinWidth = 0; button.Width = ControlHeight; }
        Words(moveLeft, b => { ToolTip.SetTip(b, T("Move column left")); Avalonia.Automation.AutomationProperties.SetName(b, T("Move column left")); });
        Words(moveRight, b => { ToolTip.SetTip(b, T("Move column right")); Avalonia.Automation.AutomationProperties.SetName(b, T("Move column right")); });
        var toolbar = new RibbonFlow { AlignLast = true, Children = { left, toolbarItems, right } };
        columnToolbarFrame.Child = toolbar; columnToolbarFrame.Padding = new Thickness(M(8)); columnToolbarFrame.BorderThickness = new Thickness(1, 1, 1, 0);
        columnRibbonFrame.Child = columnRibbon; columnRibbonFrame.Padding = new Thickness(M(12), M(8), M(12), 0); columnRibbonFrame.BorderThickness = new Thickness(1, 0, 1, 0);
        columnEditorHead.Children.Add(columnToolbarFrame); columnEditorHead.Children.Add(columnRibbonFrame);
        editColumns.Click += (_, _) => { if (!Design) Design = true; else FinishColumnEditing(); };
        newColumn.Click += (_, _) => { columnTabs.Active = 0; InsertColumn(columns.Count); };
        computedColumn.Click += (_, _) =>
        {
            var sources = columns.Where(Numeric).Take(2).ToArray(); var c = InsertColumn(columns.Count);
            c.Title = T("Calculated column"); c.Kind = Kind.Computed; c.Footer = Total.Sum; c.Decimals = 2;
            foreach (var source in sources) c.Terms.Add(new Term(source.Key));
            columnTabs.Active = 0; Rebuild(); Changed?.Invoke();
        };
        moveLeft.Click += (_, _) => { if (CurrentColumn is Column c && columns.IndexOf(c) > 0) MoveColumn(columns.IndexOf(c), columns.IndexOf(c) - 1, false); };
        moveRight.Click += (_, _) => { if (CurrentColumn is Column c && columns.IndexOf(c) < columns.Count - 1) MoveColumn(columns.IndexOf(c), columns.IndexOf(c) + 1, false); };
        removeColumn.Click += (_, _) => { if (CurrentColumn is Column c && columns.Count > 1) RemoveColumn(c); };
        KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape || !Design || e.Handled || e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
            var draft = ribbonFields.FirstOrDefault(x => x.Field.IsKeyboardFocusWithin);
            if (draft.Field != null) draft.Reset(); else FinishColumnEditing();
            e.Handled = true;
        };

        EditorField(0, "Heading", c => c.Title, (c, v) => c.Title = v, 200);
        EditorChoice(0, "Type", new[] { "Text", "Number", "Value with unit", "Computed", "General" }, c => (int)c.Kind, (c, i) =>
        {
            c.Kind = (Kind)i;
            if (c.Kind == Kind.Unit && c.Units.Count == 0) c.Units = new[] { "pcs", "box", "pallet" };
            if (c.Kind == Kind.Computed && c.Terms.Count == 0) foreach (var source in columns.Where(x => x != c && Numeric(x)).Take(2)) c.Terms.Add(new Term(source.Key));
            if (!Numeric(c) && c.Footer != Total.Count) c.Footer = Total.None;
        }, 152);
        EditorChoice(0, "Footer", Enum.GetNames<Total>(), c => (int)c.Footer, (c, i) => c.Footer = (Total)i);
        EditorChoice(0, "Alignment", new[] { "Auto", "Left", "Center", "Right" }, c => c.Alignment == null ? 0 : c.Alignment == TextAlignment.Left ? 1 : c.Alignment == TextAlignment.Center ? 2 : 3, (c, i) => c.Alignment = i == 0 ? null : i == 1 ? TextAlignment.Left : i == 2 ? TextAlignment.Center : TextAlignment.Right);
        EditorChoice(0, "Decimals", new[] { "Auto", "0", "1", "2", "3", "4" }, c => c.Decimals is int d ? Math.Clamp(d, 0, 4) + 1 : 0, (c, i) => c.Decimals = i == 0 ? null : i - 1, 96);
        EditorField(0, "Suffix", c => c.Suffix ?? "", (c, v) => c.Suffix = v.Length == 0 ? null : v, 96);
        var addTerm = Words(new ActionButton("Add term", ButtonKind.Ghost, Icons.Plus), b => b.Content = T("Add term"));
        columnEditorSync.Add(c => { addTerm.IsEnabled = columns.Any(x => x != c && Numeric(x)); ToolTip.SetTip(addTerm, addTerm.IsEnabled ? null : T("Add a numeric column first")); });
        addTerm.VerticalAlignment = VerticalAlignment.Center;
        var formulaHead = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { Words(Build.Text("", Text2, TextSize.Label), text => { text.Text = T("Formula"); text.VerticalAlignment = VerticalAlignment.Center; }), addTerm } }; Grid.SetColumn(addTerm, 1);
        formulaEditor.Width = M(360); formulaEditor.Margin = new Thickness(0, 0, M(16), M(12)); formulaEditor.Children.Add(formulaHead);
        formulaEditor.Children.Add(new ScrollViewer { Content = formulaTerms, MaxHeight = M(172), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        columnPanes[0].Children.Add(formulaEditor);
        columnPanes[0].SizeChanged += (_, e) => formulaEditor.MaxWidth = Math.Max(0, e.NewSize.Width - formulaEditor.Margin.Left - formulaEditor.Margin.Right);
        addTerm.Click += (_, _) => { if (CurrentColumn is Column c && (columns.FirstOrDefault(x => x != c && Numeric(x) && c.Terms.All(t => t.Key != x.Key)) ?? columns.FirstOrDefault(x => x != c && Numeric(x))) is Column source) { c.Terms.Add(new Term(source.Key)); Rebuild(); Changed?.Invoke(); } };
        unitsEditor = EditorField(0, "Units (comma-separated)", c => string.Join(", ", c.Units), (c, v) => c.Units = v.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).Distinct().ToArray(), 280, v => v.Split(',').Any(x => x.Trim().Length > 0) ? null : T("Define at least one unit"));
        EditorField(1, "Subheading", c => c.Subtitle, (c, v) => c.Subtitle = v, 200);
        EditorSize(1, "Heading size", new[] { 12d, 13, 14 }, c => c.HeadingSize ?? HeadingSize, (c, v) => c.HeadingSize = v);
        EditorSize(1, "Subheading size", new[] { 11d, 12 }, c => c.SubheadingSize ?? SubheadingSize, (c, v) => c.SubheadingSize = v);
        EditorChoice(1, "Values", new[] { "Normal", "Bold", "Italic", "Bold italic" }, c => (c.Bold ? 1 : 0) + (c.Italic ? 2 : 0), (c, i) => { c.Bold = (i & 1) != 0; c.Italic = (i & 2) != 0; });
        EditorChoice(2, "Width", Enum.GetNames<Sizing>(), c => (int)c.Sizing, (c, i) => { c.Sizing = (Sizing)i; if (c.Sizing == Sizing.Fixed) c.Width ??= widths.ElementAtOrDefault(columns.IndexOf(c)); });
        foreach (var key in new[] { "Fixed width", "Min width", "Max width" })
        {
            var widthControl = EditorField(2, key, c => EditorNumber(key == "Fixed width" ? c.Width : key == "Min width" ? c.MinWidth : c.MaxWidth), (c, v) =>
            {
                double? n = TryEditorNumber(v, out var parsed) ? parsed : null;
                if (key == "Fixed width") c.Width = n; else if (key == "Min width") c.MinWidth = n; else c.MaxWidth = n;
                if (c.MaxWidth is double max) c.MaxWidth = Math.Max(40, Math.Max(c.MinWidth ?? 0, max));
                if (c.Width is double width) c.Width = Math.Max(40, Math.Max(c.MinWidth ?? 0, Math.Min(width, c.MaxWidth ?? double.PositiveInfinity)));
            }, 120, v =>
            {
                if (string.IsNullOrWhiteSpace(v)) return null;
                if (!TryEditorNumber(v, out var n) || n <= 0) return T("Enter a positive number");
                if (key == "Fixed width" && CurrentColumn is Column c && (n < Math.Max(40, c.MinWidth ?? 0) || n > (c.MaxWidth ?? double.PositiveInfinity))) return T("Width must stay within the column's minimum and maximum");
                return null;
            });
            if (key == "Fixed width") fixedWidthEditor = widthControl;
        }
        EditorSize(3, "Heading size", new[] { 12d, 13, 14 }, _ => HeadingSize, (_, v) => HeadingSize = v);
        EditorSize(3, "Subheading size", new[] { 11d, 12 }, _ => SubheadingSize, (_, v) => SubheadingSize = v);
        EditorSize(3, "Value size", new[] { 12d, 13, 14 }, _ => ValueSize, (_, v) => ValueSize = v);
        EditorChoice(3, "Sorting", new[] { "On", "Off" }, _ => Sorting ? 0 : 1, (_, i) => Sorting = i == 0);
        BuildPaperEditor();
        BuildRibbonGroups();
        foreach (var field in ColumnFields) EditorField(0, field.Label, field.Read, field.Write, field.Width);
        ColumnFields.CollectionChanged += (_, e) =>
        {
            if (e.NewItems != null) foreach (ColumnField field in e.NewItems) EditorField(0, field.Label, field.Read, field.Write, field.Width);
            SyncColumnEditor();
        };
        ApplyRibbonTabs();
    }

    void ApplyRibbonTabs()
    {
        for (int i = 0; i < 4; i++) columnTabs.SetShown(i, RibbonTabs.HasFlag((RibbonTab)(1 << i)));
        if (!RibbonTabs.HasFlag((RibbonTab)(1 << columnTabs.Active)) && Enumerable.Range(0, 4).FirstOrDefault(i => RibbonTabs.HasFlag((RibbonTab)(1 << i)), -1) is >= 0 and var first) columnTabs.Active = first;
        ribbonGroups.InvalidateMeasure();
    }
    void ApplyRibbonFields()
    {
        foreach (var (_, control) in ribbonControls) control.IsVisible = true;
        SyncColumnEditor();
    }

    static bool TryEditorNumber(string text, out double value) => double.TryParse(text, NumberStyles.Float, Culture, out value) && double.IsFinite(value);
    static string EditorNumber(double? value) => value?.ToString("0.##", Culture) ?? "";

    void FinishColumnEditing()
    {
        if (LastValidationError != null) { editor.Focus(); return; }
        foreach (var draft in ribbonFields.Where(x => x.Applies())) draft.Validate();
        var invalid = ribbonFields.FirstOrDefault(x => x.Applies() && x.Field.Error != null);
        if (invalid.Field != null) { columnTabs.Active = invalid.Pane; ShowColumnPane(invalid.Pane); invalid.Field.Box.Focus(); return; }
        foreach (var draft in ribbonFields.Where(x => x.Applies())) draft.Commit();
        Design = false; editColumns.Focus();
    }

    void EditorSize(int pane, string label, double[] sizes, Func<Column, double> read, Action<Column, double> write)
    {
        var choice = new ChoiceButton();
        var control = RibbonField(label, choice, 96); columnPanes[pane].Children.Add(control);
        void Sync(Column c)
        {
            var value = read(c);
            choice.SetItems(sizes.Append(value).Distinct().OrderBy(x => x).Select(x => x.ToString(Culture)).ToArray(), value.ToString(Culture));
            SizeRibbonField(control, choice, 96);
        }
        columnEditorSync.Add(Sync);
        Words(choice, _ => { if (CurrentColumn is Column c) Sync(c); });
        choice.Picked += value => { if (syncingColumnEditor || IsReadOnly || !Design || CurrentColumn is not Column c || !TryEditorNumber(value, out var size)) return; write(c, size); Rebuild(); Changed?.Invoke(); };
    }

    Control EditorField(int pane, string label, Func<Column, string> read, Action<Column, string> write, double width, Func<string, string?>? validate = null)
    {
        var field = new Field(); var control = RibbonField(label, field, width); columnPanes[pane].Children.Add(control);
        Column? target = null;
        string presented = ""; bool edited = false, presenting = false;
        bool Draft() => edited || field.Text != presented;
        void Show(Column c) { presenting = true; try { field.Text = read(c); presented = field.Text; edited = false; } finally { presenting = false; } }
        // TextChanged is delayed; track actual edits before quiet display updates can overtake it.
        field.Box.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty && !presenting && Design && !IsReadOnly) edited = true; };
        columnEditorSync.Add(c =>
        {
            bool changed = target != c, committed = false;
            if (changed && Design && !IsReadOnly && target != null && columns.Contains(target) && label == "Units (comma-separated)" && Draft() && validate?.Invoke(field.Text) == null) { write(target, field.Text); committed = true; }
            target = c;
            if (changed) field.Error = null;
            if (changed || !field.IsKeyboardFocusWithin && field.Error == null && !Draft()) Show(c);
            if (committed) Changed?.Invoke();
        });
        void Apply() { if (syncingColumnEditor || IsReadOnly || !Design || !Draft() || target is not Column c || !columns.Contains(c)) return; field.Error = validate?.Invoke(field.Text); if (field.Error != null) return; write(c, field.Text); presented = field.Text; edited = false; Rebuild(); Changed?.Invoke(); }
        void Reset() { field.Error = null; if (target is Column c) Show(c); }
        ribbonFields.Add((field, pane,
            () => target != null && target == CurrentColumn && (label == "Units (comma-separated)" ? target.Kind == Kind.Unit : label != "Fixed width" || target.Sizing == Sizing.Fixed),
            () => field.Error = Draft() ? validate?.Invoke(field.Text) : null, Apply, Reset));
        Words(field, f => { if (f.Error != null) f.Error = validate?.Invoke(f.Text); }, "validation");
        field.Edited += () => { if (label != "Units (comma-separated)") Apply(); };
        field.Submit += Apply;
        field.LostFocus += (_, _) =>
        {
            if (IsReadOnly || !Design || field.IsKeyboardFocusWithin || target is not Column c || !columns.Contains(c)) return;
            if (label == "Units (comma-separated)") Apply();
            if (label == "Fixed width" && Draft() && TryEditorNumber(field.Text, out var n) && n > 0)
            {
                var next = Math.Max(40, Math.Max(c.MinWidth ?? 0, Math.Min(n, c.MaxWidth ?? double.PositiveInfinity)));
                bool changed = c.Width != next; c.Width = next; field.Error = null; presented = field.Text; edited = false; Rebuild(); if (changed) Changed?.Invoke();
            }
            if (field.Error == null) Show(c);
        };
        return control;
    }
    void EditorChoice(int pane, string label, string[] values, Func<Column, int> read, Action<Column, int> write, double width = 120)
    {
        var choice = new ChoiceButton(); var translated = values.Select(T).ToArray();
        var control = RibbonField(label, choice, width); columnPanes[pane].Children.Add(control);
        Words(choice, _ => { translated = values.Select(T).ToArray(); if (CurrentColumn is Column c) Sync(c); });
        void Sync(Column c) { choice.SetItems(label == "Footer" && !Numeric(c) ? new[] { translated[0], translated[5] } : translated, translated[Math.Clamp(read(c), 0, translated.Length - 1)]); SizeRibbonField(control, choice, width); }
        columnEditorSync.Add(Sync);
        choice.Picked += value => { if (syncingColumnEditor || IsReadOnly || !Design || CurrentColumn is not Column c) return; write(c, Array.IndexOf(translated, value)); Rebuild(); Changed?.Invoke(); };
    }
    void ShowColumnPane(int index) { for (int i = 0; i < ribbonGroups.Children.Count; i++) ribbonGroups.Children[i].IsVisible = i == index; ribbonGroups.InvalidateMeasure(); }
    void SyncColumnEditor()
    {
        columnEditorHead.IsVisible = ShowColumnEditor;
        if (!columnEditorCreated) return;
        columnRibbonFrame.IsVisible = Design && !IsReadOnly && RibbonTabs != RibbonTab.None;
        SyncToolbarLayout();
        editColumns.Content = T(Design ? "Done" : "Edit columns"); editColumns.Kind = Design ? ButtonKind.Normal : ButtonKind.Ghost;
        editColumns.IsEnabled = !IsReadOnly;
        var mutable = Design && !IsReadOnly && !columns.IsReadOnly;
        newColumn.IsEnabled = computedColumn.IsEnabled = mutable;
        var reason = IsReadOnly ? T("The table is read only") : !Design ? T("Choose Edit columns first") : columns.IsReadOnly ? T("The columns collection is read only") : null;
        ToolTip.SetTip(editColumns, IsReadOnly ? reason : null); ToolTip.SetTip(newColumn, reason); ToolTip.SetTip(computedColumn, reason);
        moveLeft.IsEnabled = mutable && CurrentColumn is Column left && columns.IndexOf(left) > 0;
        moveRight.IsEnabled = mutable && CurrentColumn is Column right && columns.IndexOf(right) < columns.Count - 1;
        removeColumn.IsEnabled = mutable && columns.Count > 1 && CurrentColumn != null;
        PaintColumnEditor();
        ribbonGroups.InvalidateMeasure();
        foreach (var sync in paperEditorSync) sync();
        HideRibbonFields();
        if (!ShowColumnEditor || !Design || CurrentColumn is not Column c) return;
        syncingColumnEditor = true;
        try
        {
            foreach (var sync in columnEditorSync) sync(c);
            if (fixedWidthEditor != null) fixedWidthEditor.IsVisible = c.Sizing == Sizing.Fixed;
            formulaEditor.IsVisible = c.Kind == Kind.Computed; if (unitsEditor != null) unitsEditor.IsVisible = c.Kind == Kind.Unit;
            formulaTerms.Children.Clear();
            var sources = columns.Where(x => x != c && Numeric(x)).ToArray();
            var labels = sources.Select(x => sources.Count(y => y.Title == x.Title) > 1 && !string.IsNullOrWhiteSpace(x.Subtitle) ? x.Title + " · " + x.Subtitle : x.Title).ToArray();
            var fullLabels = labels.ToArray();
            for (int i = 0; i < labels.Length; i++) if (fullLabels.Count(x => x == fullLabels[i]) > 1) labels[i] += " (" + (columns.IndexOf(sources[i]) + 1).ToString(Culture) + ")";
            foreach (var term in c.Terms.ToArray())
            {
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions { new ColumnDefinition(ControlHeight * 2, GridUnitType.Pixel), new ColumnDefinition(1, GridUnitType.Star), new ColumnDefinition(ControlHeight, GridUnitType.Pixel) } };
                Control op;
                if (ReferenceEquals(term, c.Terms.FirstOrDefault())) op = Build.Text("=", Text2);
                else { var choice = new ChoiceButton(); choice.SetItems(OpText, OpText[(int)term.Op]); choice.Picked += value => { term.Op = (Op)Array.IndexOf(OpText, value); Rebuild(); Changed?.Invoke(); }; op = choice; }
                var source = new ChoiceButton(); source.SetItems(labels, Array.FindIndex(sources, x => x.Key == term.Key) is int selected && selected >= 0 ? labels[selected] : null);
                source.Picked += value => { var at = source.Items.ToList().IndexOf(value); if (at >= 0 && at < sources.Length) term.Key = sources[at].Key; Rebuild(); Changed?.Invoke(); };
                var remove = new ActionButton("", ButtonKind.Ghost, Icons.X) { MinWidth = 0, Width = ControlHeight }; ToolTip.SetTip(remove, T("Remove this term")); Avalonia.Automation.AutomationProperties.SetName(remove, T("Remove this term"));
                remove.IsVisible = c.Terms.Count > 1; remove.Click += (_, _) => { c.Terms.Remove(term); Rebuild(); Changed?.Invoke(); };
                row.Children.Add(op); row.Children.Add(source); row.Children.Add(remove); Grid.SetColumn(source, 1); Grid.SetColumn(remove, 2); formulaTerms.Children.Add(row);
            }
        }
        finally { syncingColumnEditor = false; }
        HideRibbonFields();
        PaintColumnEditor();
    }
    void HideRibbonFields() { foreach (var (label, control) in ribbonControls) if (Hidden(label)) control.IsVisible = false; }
    void SyncToolbarLayout()
    {
        var shown = ToolbarActions;
        editColumns.IsVisible = shown.HasFlag(ToolbarAction.EditColumns);
        moveLeft.IsVisible = moveRight.IsVisible = shown.HasFlag(ToolbarAction.MoveColumn);
        removeColumn.IsVisible = shown.HasFlag(ToolbarAction.RemoveColumn);
        newColumn.IsVisible = shown.HasFlag(ToolbarAction.AddColumn);
        computedColumn.IsVisible = shown.HasFlag(ToolbarAction.CalculatedColumn);
        toolbarLeft!.IsVisible = editColumns.IsVisible || moveLeft.IsVisible || removeColumn.IsVisible;
        toolbarRight!.IsVisible = newColumn.IsVisible || computedColumn.IsVisible;
        toolbarItems.IsVisible = toolbarItems.Children.Count > 0;
        columnToolbarFrame.IsVisible = ToolbarPlacement != BarPlacement.Hidden && (toolbarLeft.IsVisible || toolbarItems.IsVisible || toolbarRight.IsVisible);
        bool bottom = ToolbarPlacement == BarPlacement.Bottom;
        Grid.SetRow(columnEditorHead, bottom ? 2 : 0);
        // the editor opens on the table's side of the toolbar
        if ((columnEditorHead.Children[0] == columnToolbarFrame) == bottom) columnEditorHead.Children.Move(0, 1);
        // toolbar and ribbon make one closed box; its edge toward the table is left to the table's border only where it sits on it
        // (above the table with no Add rows bar between); one line between the two when both show
        bool ribbon = columnRibbonFrame.IsVisible; double edge = !bottom && AddRowsPlacement != BarPlacement.Top ? 0 : 1;
        columnToolbarFrame.BorderThickness = bottom ? new Thickness(1, ribbon ? 0 : 1, 1, 1) : new Thickness(1, 1, 1, ribbon ? 0 : edge);
        columnRibbonFrame.BorderThickness = new Thickness(1, bottom ? 1 : 0, 1, bottom ? 0 : edge);
    }
    void PaintColumnEditor()
    {
        columnToolbarFrame.Background = columnRibbonFrame.Background = B(Raised);
        columnToolbarFrame.BorderBrush = columnRibbonFrame.BorderBrush = B(Look.Separator);
        removeColumn.Foreground = B(removeColumn.IsEnabled ? DangerText : Disabled);
    }
}

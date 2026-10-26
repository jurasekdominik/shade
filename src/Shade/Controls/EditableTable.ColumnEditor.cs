using System;
using System.Collections.Generic;
using System.Globalization;
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
    bool showColumnEditor, syncingColumnEditor, columnEditorCreated;
    readonly List<Action<Column>> columnEditorSync = new();
    readonly StackPanel columnEditorHead = new(), columnRibbon = new();
    readonly Border columnToolbarFrame = new(), columnRibbonFrame = new();
    readonly TextTabs columnTabs = new("Column", "Text", "Width", "Table");
    readonly List<WrapPanel> columnPanes = new();
    readonly StackPanel formulaTerms = new() { Spacing = M(8) };
    readonly StackPanel formulaEditor = new() { Spacing = M(4) };
    Control? unitsEditor;
    readonly ActionButton editColumns = new("Edit columns", ButtonKind.Ghost), newColumn = new("Add column", ButtonKind.Ghost, Icons.Plus), computedColumn = new("Calculated column", ButtonKind.Ghost);
    readonly ActionButton moveLeft = new("", ButtonKind.Ghost, Icons.ChevronLeft), moveRight = new("", ButtonKind.Ghost, Icons.ChevronRight), removeColumn = new("Remove column", ButtonKind.Ghost);

    Control CreateColumnEditor(Control grid)
    {
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Children = { columnEditorHead, grid } }; Grid.SetRow(grid, 1);
        return root;
    }
    void BuildColumnEditor()
    {
        columnEditorCreated = true;
        foreach (var name in new[] { "Column", "Text", "Width", "Table" }) columnPanes.Add(new WrapPanel { Orientation = Orientation.Horizontal });
        columnRibbon.Spacing = M(12); columnRibbon.Children.Add(columnTabs);
        foreach (var pane in columnPanes) columnRibbon.Children.Add(pane);
        columnTabs.Changed += ShowColumnPane;
        Words(editColumns, b => b.Content = T(Design ? "Done" : "Edit columns"));
        Words(newColumn, b => b.Content = T("Add column")); Words(computedColumn, b => b.Content = T("Calculated column")); Words(removeColumn, b => b.Content = T("Remove column"));
        Words(columnTabs, tabs => { var names = new[] { "Column", "Text", "Width", "Table" }; for (int i = 0; i < names.Length; i++) tabs.SetName(i, T(names[i])); });
        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = M(8), Children = { editColumns, moveLeft, moveRight, removeColumn } };
        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = M(8), Children = { newColumn, computedColumn } };
        editColumns.MinWidth = M(120);
        foreach (var button in new[] { moveLeft, moveRight }) { button.MinWidth = 0; button.Width = ControlHeight; }
        Words(moveLeft, b => { ToolTip.SetTip(b, T("Move column left")); Avalonia.Automation.AutomationProperties.SetName(b, T("Move column left")); });
        Words(moveRight, b => { ToolTip.SetTip(b, T("Move column right")); Avalonia.Automation.AutomationProperties.SetName(b, T("Move column right")); });
        var toolbar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { left, right } }; Grid.SetColumn(right, 1);
        columnToolbarFrame.Child = toolbar; columnToolbarFrame.Padding = new Thickness(M(8)); columnToolbarFrame.BorderThickness = new Thickness(1, 1, 1, 0);
        columnRibbonFrame.Child = columnRibbon; columnRibbonFrame.Padding = new Thickness(M(12), M(8), M(12), 0); columnRibbonFrame.BorderThickness = new Thickness(1, 0, 1, 0);
        columnEditorHead.Children.Add(columnToolbarFrame); columnEditorHead.Children.Add(columnRibbonFrame);
        editColumns.Click += (_, _) => { if (LastValidationError != null) { editor.Focus(); return; } Design = !Design; };
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
        KeyDown += (_, e) => { if (e.Key == Key.Escape && Design && !e.Handled && !e.KeyModifiers.HasFlag(KeyModifiers.Control)) { if (LastValidationError == null) { Design = false; editColumns.Focus(); e.Handled = true; } } };

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
        var addTerm = Words(new ActionButton("Add term", ButtonKind.Ghost, Icons.Plus), b => b.Content = T("Add term"));
        columnEditorSync.Add(c => { addTerm.IsEnabled = columns.Any(x => x != c && Numeric(x)); ToolTip.SetTip(addTerm, addTerm.IsEnabled ? null : T("Add a numeric column first")); });
        var formulaHead = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { Words(Build.Text("", Text2, TextSize.Label), text => text.Text = T("Formula")), addTerm } }; Grid.SetColumn(addTerm, 1);
        formulaEditor.Width = M(360); formulaEditor.Margin = new Thickness(0, 0, M(16), M(12)); formulaEditor.Children.Add(formulaHead);
        formulaEditor.Children.Add(new ScrollViewer { Content = formulaTerms, MaxHeight = M(172), HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        columnPanes[0].Children.Add(formulaEditor);
        addTerm.Click += (_, _) => { if (CurrentColumn is Column c && columns.FirstOrDefault(x => x != c && Numeric(x)) is Column source) { c.Terms.Add(new Term(source.Key)); Rebuild(); Changed?.Invoke(); } };
        unitsEditor = EditorField(0, "Units (comma-separated)", c => string.Join(", ", c.Units), (c, v) => c.Units = v.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).Distinct().ToArray(), 280, v => v.Split(',').Any(x => x.Trim().Length > 0) ? null : T("Define at least one unit"));
        EditorField(1, "Subheading", c => c.Subtitle, (c, v) => c.Subtitle = v, 200);
        EditorChoice(1, "Heading size", new[] { "12", "13", "14" }, c => (int)(c.HeadingSize ?? HeadingSize) - 12, (c, i) => c.HeadingSize = i + 12, 96);
        EditorChoice(1, "Subheading size", new[] { "11", "12" }, c => (int)(c.SubheadingSize ?? SubheadingSize) - 11, (c, i) => c.SubheadingSize = i + 11, 96);
        EditorChoice(1, "Values", new[] { "Normal", "Bold", "Italic", "Bold italic" }, c => (c.Bold ? 1 : 0) + (c.Italic ? 2 : 0), (c, i) => { c.Bold = (i & 1) != 0; c.Italic = (i & 2) != 0; });
        EditorChoice(2, "Width", Enum.GetNames<Sizing>(), c => (int)c.Sizing, (c, i) => { c.Sizing = (Sizing)i; if (c.Sizing == Sizing.Fixed) c.Width ??= widths.ElementAtOrDefault(columns.IndexOf(c)); });
        foreach (var key in new[] { "Fixed width", "Min width", "Max width" })
            EditorField(2, key, c => (key == "Fixed width" ? c.Width : key == "Min width" ? c.MinWidth : c.MaxWidth)?.ToString(CultureInfo.InvariantCulture) ?? "", (c, v) =>
            {
                double? n = double.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
                if (key == "Fixed width") c.Width = n; else if (key == "Min width") c.MinWidth = n; else c.MaxWidth = n;
                if (c.MaxWidth is double max) c.MaxWidth = Math.Max(40, Math.Max(c.MinWidth ?? 0, max));
                if (c.Width is double width) c.Width = Math.Max(40, Math.Max(c.MinWidth ?? 0, Math.Min(width, c.MaxWidth ?? double.PositiveInfinity)));
            }, 120, v => string.IsNullOrWhiteSpace(v) || double.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) && n > 0 ? null : T("Enter a positive number"));
        EditorChoice(3, "Heading size", new[] { "12", "13", "14" }, _ => (int)HeadingSize - 12, (_, i) => HeadingSize = i + 12, 96);
        EditorChoice(3, "Subheading size", new[] { "11", "12" }, _ => (int)SubheadingSize - 11, (_, i) => SubheadingSize = i + 11, 96);
        EditorChoice(3, "Value size", new[] { "12", "13", "14" }, _ => (int)ValueSize - 12, (_, i) => ValueSize = i + 12, 96);
        EditorChoice(3, "Sorting", new[] { "On", "Off" }, _ => Sorting ? 0 : 1, (_, i) => Sorting = i == 0);
    }

    Control EditorField(int pane, string label, Func<Column, string> read, Action<Column, string> write, double width, Func<string, string?>? validate = null)
    {
        var field = new Field(); var control = FormLayout.Field(label, field, M(width)); Words((TextBlock)((StackPanel)control).Children[0], text => text.Text = T(label)); columnPanes[pane].Children.Add(control);
        columnEditorSync.Add(c => { if (!field.IsKeyboardFocusWithin) field.Text = read(c); });
        field.Edited += () => { if (syncingColumnEditor || CurrentColumn is not Column c) return; field.Error = validate?.Invoke(field.Text); if (field.Error != null) return; write(c, field.Text); Rebuild(); Changed?.Invoke(); };
        field.LostFocus += (_, _) => { if (CurrentColumn is Column c && field.Error == null) field.Text = read(c); };
        return control;
    }
    void EditorChoice(int pane, string label, string[] values, Func<Column, int> read, Action<Column, int> write, double width = 120)
    {
        var choice = new ChoiceButton(); var translated = values.Select(T).ToArray();
        var control = FormLayout.Field(label, choice, M(width)); Words((TextBlock)((StackPanel)control).Children[0], text => text.Text = T(label)); columnPanes[pane].Children.Add(control);
        Words(choice, _ => { translated = values.Select(T).ToArray(); if (CurrentColumn is Column c) Sync(c); });
        void Sync(Column c) => choice.SetItems(label == "Footer" && !Numeric(c) ? new[] { translated[0], translated[5] } : translated, translated[Math.Clamp(read(c), 0, translated.Length - 1)]);
        columnEditorSync.Add(Sync);
        choice.Picked += value => { if (syncingColumnEditor || CurrentColumn is not Column c) return; write(c, Array.IndexOf(translated, value)); Rebuild(); Changed?.Invoke(); };
    }
    void ShowColumnPane(int index) { for (int i = 0; i < columnPanes.Count; i++) columnPanes[i].IsVisible = i == index; }
    void SyncColumnEditor()
    {
        columnEditorHead.IsVisible = ShowColumnEditor;
        if (!columnEditorCreated) return;
        columnRibbonFrame.IsVisible = Design && !IsReadOnly;
        editColumns.Content = T(Design ? "Done" : "Edit columns"); editColumns.Kind = Design ? ButtonKind.Normal : ButtonKind.Ghost;
        editColumns.IsEnabled = !IsReadOnly;
        var mutable = Design && !IsReadOnly && !columns.IsReadOnly;
        newColumn.IsEnabled = computedColumn.IsEnabled = mutable;
        moveLeft.IsEnabled = mutable && CurrentColumn is Column left && columns.IndexOf(left) > 0;
        moveRight.IsEnabled = mutable && CurrentColumn is Column right && columns.IndexOf(right) < columns.Count - 1;
        removeColumn.IsEnabled = mutable && columns.Count > 1 && CurrentColumn != null;
        PaintColumnEditor();
        ShowColumnPane(columnTabs.Active);
        if (!ShowColumnEditor || !Design || CurrentColumn is not Column c) return;
        syncingColumnEditor = true;
        try
        {
            foreach (var sync in columnEditorSync) sync(c);
            formulaEditor.IsVisible = c.Kind == Kind.Computed; if (unitsEditor != null) unitsEditor.IsVisible = c.Kind == Kind.Unit;
            formulaTerms.Children.Clear();
            var sources = columns.Where(x => x != c && Numeric(x)).ToArray();
            var labels = sources.Select(x => sources.Count(y => y.Title == x.Title) > 1 ? x.Title + " (" + x.Key + ")" : x.Title).ToArray();
            foreach (var term in c.Terms.ToArray())
            {
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions { new ColumnDefinition(M(48), GridUnitType.Pixel), new ColumnDefinition(1, GridUnitType.Star), new ColumnDefinition(ControlHeight, GridUnitType.Pixel) } };
                Control op;
                if (ReferenceEquals(term, c.Terms.FirstOrDefault())) op = Build.Text("=", Text2);
                else { var choice = new ChoiceButton(); choice.SetItems(OpText, OpText[(int)term.Op]); choice.Picked += value => { term.Op = (Op)Array.IndexOf(OpText, value); Rebuild(); Changed?.Invoke(); }; op = choice; }
                var source = new ChoiceButton(); source.SetItems(labels, Array.FindIndex(sources, x => x.Key == term.Key) is int selected && selected >= 0 ? labels[selected] : null);
                source.Picked += value => { var at = source.Items.ToList().IndexOf(value); if (at >= 0 && at < sources.Length) term.Key = sources[at].Key; Rebuild(); Changed?.Invoke(); };
                var remove = new ActionButton("", ButtonKind.Ghost, Icons.X) { MinWidth = 0, Width = ControlHeight }; ToolTip.SetTip(remove, T("Remove this term")); Avalonia.Automation.AutomationProperties.SetName(remove, T("Remove this term"));
                remove.IsEnabled = c.Terms.Count > 1; remove.Click += (_, _) => { c.Terms.Remove(term); Rebuild(); Changed?.Invoke(); };
                row.Children.Add(op); row.Children.Add(source); row.Children.Add(remove); Grid.SetColumn(source, 1); Grid.SetColumn(remove, 2); formulaTerms.Children.Add(row);
            }
        }
        finally { syncingColumnEditor = false; }
        PaintColumnEditor();
    }
    void PaintColumnEditor()
    {
        columnToolbarFrame.Background = columnRibbonFrame.Background = B(Raised);
        columnToolbarFrame.BorderBrush = columnRibbonFrame.BorderBrush = B(Look.Separator);
        removeColumn.Foreground = B(removeColumn.IsEnabled ? DangerText : Disabled);
    }
}

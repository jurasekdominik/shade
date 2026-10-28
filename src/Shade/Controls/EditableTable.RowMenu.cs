// Shade - shared control: EditableTable's row menu (right-click, the row's "more" button, Menu key).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using static Shade.Look;

namespace Shade;

public sealed partial class EditableTable
{
    /// <summary>The built-in row menu items; clear a flag to hide that item.</summary>
    [Flags] public enum RowAction { None = 0, Duplicate = 1, InsertAbove = 2, InsertBelow = 4, Delete = 8, All = 15 }
    /// <summary>An item the app adds to the row menu; Run gets the selected rows, in table order.</summary>
    public sealed record RowMenuItem(MenuEntry Entry, Action<IReadOnlyList<Row>> Run);
    public static readonly StyledProperty<RowAction> RowActionsProperty = AvaloniaProperty.Register<EditableTable, RowAction>(nameof(RowActions), RowAction.All, validate: value => (value & ~RowAction.All) == 0);
    /// <summary>Which built-in row menu items show (all by default). With none and no RowMenuItems the table has no row menu.</summary>
    public RowAction RowActions { get => GetValue(RowActionsProperty); set => SetValue(RowActionsProperty, value); }
    /// <summary>The app's own row menu items, between the insert items and Delete.</summary>
    public IList<RowMenuItem> RowMenuItems { get; } = new List<RowMenuItem>();

    void RowMenu(Line l, Control anchorControl, bool atPointer)
    {
        if (readOnly || rows.IsReadOnly) return;
        var row = l.Row;
        if (!selected.Contains(row)) { selected.Clear(); selected.Add(row); anchor = row; PaintAll(); SelectionChanged?.Invoke(Selected); }
        var targets = Selected; int n = targets.Count; var shown = RowActions;
        var items = new List<(MenuEntry Entry, Func<Task>? Run)>();
        void Add(RowAction action, MenuEntry entry, Func<Task> run) { if (shown.HasFlag(action)) items.Add((entry, run)); }
        int Last() => rows.IndexOf(targets[^1]);  // read when the item runs
        Add(RowAction.Duplicate, new MenuEntry(n > 1 ? T("Duplicate {0} rows", n) : "Duplicate"), () =>
        {
            var copies = targets.Select(t => { var r = new Row(); foreach (var kv in t.Values) r.Values[kv.Key] = kv.Value; foreach (var kv in t.Units) r.Units[kv.Key] = kv.Value; foreach (var kv in t.Alignments) r.Alignments[kv.Key] = kv.Value; return r; }).ToList();
            for (int k = 0; k < copies.Count; k++) rows.Insert(Last() + 1 + k, copies[k]);
            selected.Clear(); foreach (var r in copies) selected.Add(r);
            Reflow(); PaintAll(); Changed?.Invoke(); SelectionChanged?.Invoke(Selected);
            return Task.CompletedTask;
        });
        Add(RowAction.InsertAbove, new MenuEntry("Insert row above"), () => { var b = Blank(); rows.Insert(rows.IndexOf(targets[0]), b); Reflow(); Changed?.Invoke(); Start(b); return Task.CompletedTask; });
        Add(RowAction.InsertBelow, new MenuEntry("Insert row below"), () => { var b = Blank(); rows.Insert(Last() + 1, b); Reflow(); Changed?.Invoke(); Start(b); return Task.CompletedTask; });
        if (RowMenuItems.Count > 0)
        {
            if (items.Count > 0) items.Add((MenuEntry.Line, null));
            foreach (var item in RowMenuItems) items.Add((item.Entry, () => { item.Run(targets); return Task.CompletedTask; }));
        }
        Add(RowAction.Delete, new MenuEntry(n > 1 ? T("Delete {0} rows", n) : "Delete row") { IsDanger = true }, async () =>
        {
            bool yes = TopLevel.GetTopLevel(this) is Window w ? await Ask.ConfirmDelete(w, n > 1 ? T("Delete {0} rows?", n) : T("Delete this row?"), n > 1 ? T("Delete {0} rows", n) : T("Delete row")) : true;
            if (!yes) return;
            foreach (var t in targets) rows.Remove(t);
            selected.Clear(); Reflow(); Changed?.Invoke(); SelectionChanged?.Invoke(Selected);
        });
        if (items.Count == 0) return;
        Shade.Menu.Show(anchorControl, items.Select(x => x.Entry).ToList(), async e => { var hit = items.Find(x => ReferenceEquals(x.Entry, e)); if (hit.Run != null) await hit.Run(); }, atPointer);
    }
}

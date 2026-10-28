// Shade - shared control: EditableTable's headings: resize, sort and the designer (drag to a gap, insert, remove).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using static Shade.Look;

namespace Shade
{
    public sealed partial class EditableTable
    {
        // ---- headings: resize, sort, design ----------------------------------------------------------------------------

        /// <summary>A heading's edge: a drag sets a Fill column's minimum, any other column's fixed width; a double-click fits it again.</summary>
        void Resize(Border grip, Column c, Border edge)
        {
            double start = 0, from = 0; bool dragging = false;
            grip.PointerPressed += (s, e) =>
            {
                if (!e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed) return;
                e.Handled = true;
                if (design) { heads[columns.IndexOf(c)].Focus(NavigationMethod.Pointer); Current(c.Key); }
                if (e.ClickCount == 2) { if (c.Sizing == Sizing.Fill) c.MinWidth = null; else c.Width = null; Layout(); Changed?.Invoke(); return; }
                dragging = true; start = e.GetPosition(this).X; from = widths[columns.IndexOf(c)]; edge.IsVisible = true; e.Pointer.Capture(grip);
            };
            grip.PointerMoved += (s, e) =>
            {
                if (!dragging) return;
                double w = Math.Max(40, Math.Round(from + e.GetPosition(this).X - start));
                if (c.Sizing == Sizing.Fill) c.MinWidth = w; else c.Width = Math.Max(c.MinWidth ?? 40, Math.Min(w, c.MaxWidth ?? double.PositiveInfinity));
                Layout();
            };
            grip.PointerReleased += (s, e) => { if (!dragging) return; dragging = false; e.Pointer.Capture(null); edge.IsVisible = grip.IsPointerOver; e.Handled = true; Changed?.Invoke(); };
            grip.PointerCaptureLost += (s, e) => { dragging = false; edge.IsVisible = false; };
        }

        /// <summary>A heading: a click sorts (or, designing, makes it current); designing, Ctrl+press starts a drag once it
        /// moves, a right-click opens the column menu, Ctrl+Left/Right moves it.</summary>
        void Wire(Border head, Column c, int at)
        {
            Point start = default; bool pressed = false, moving = false, reorder = false;
            void CursorFor(bool ctrl) => head.Cursor = design && ctrl && !columns.IsReadOnly ? Build.Move : Cursor.Default;
            head.PointerPressed += (s, e) =>
            {
                if (!e.GetCurrentPoint(head).Properties.IsLeftButtonPressed || e.Handled) return;
                pressed = true; moving = false; reorder = e.KeyModifiers.HasFlag(KeyModifiers.Control); start = e.GetPosition(head); e.Handled = true;
                if (design) { head.Focus(NavigationMethod.Pointer); e.Pointer.Capture(head); }
            };
            head.PointerMoved += (s, e) =>
            {
                CursorFor(e.KeyModifiers.HasFlag(KeyModifiers.Control));
                if (!pressed || !design || columns.IsReadOnly || !reorder) return;
                if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) { if (moving) EndDrag(); moving = false; reorder = false; return; }
                var p = e.GetPosition(head);
                if (!moving && Math.Abs(p.X - start.X) + Math.Abs(p.Y - start.Y) >= Threshold) { moving = true; Dim(at, true); }
                if (moving) ShowDrop(GapAt(e), at, c.Title, e.GetPosition(overlay));
            };
            head.PointerReleased += (s, e) =>
            {
                if (e.InitialPressMouseButton == MouseButton.Right && design) { e.Handled = true; ColumnMenu(c, head, true); return; }
                if (!pressed) return;
                bool dragged = moving;  // read first: letting the capture go raises PointerCaptureLost, which resets it
                pressed = false; moving = false; e.Pointer.Capture(null);
                if (dragged && !columns.IsReadOnly) { int g = e.KeyModifiers.HasFlag(KeyModifiers.Control) ? GapAt(e) : -1; EndDrag(); int to = g > at ? g - 1 : g; if (g >= 0 && to != at) MoveColumn(at, to, true); return; }
                if (design) { Current(c.Key); return; }
                if (!sorting) return;
                sortDir = sortKey != c.Key ? 1 : sortDir == 1 ? -1 : 0; sortKey = sortDir == 0 ? null : c.Key;
                Heads(); Arrange(); Layout();  // the rows move to their places; nothing else is drawn again
            };
            head.PointerCaptureLost += (s, e) => { if (moving) EndDrag(); pressed = false; moving = false; };
            head.PointerExited += (_, _) => CursorFor(false);
            head.KeyUp += (_, e) => { if (e.Key is Key.LeftCtrl or Key.RightCtrl) { CursorFor(false); if (moving) EndDrag(); moving = false; reorder = false; } };
            head.GotFocus += (s, e) => { Current(c.Key); if (Look.IsKeyboard(e)) head.BringIntoView(); };
            head.KeyDown += (s, e) =>
            {
                if (!design) return;
                if (e.Key == Key.Escape && pressed) { if (moving) EndDrag(); moving = false; reorder = false; CursorFor(false); e.Handled = true; return; }
                bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
                CursorFor(ctrl);
                if (ctrl && !columns.IsReadOnly && e.Key == Key.Left && at > 0) MoveColumn(at, at - 1, true);
                else if (ctrl && !columns.IsReadOnly && e.Key == Key.Right && at < columns.Count - 1) MoveColumn(at, at + 1, true);
                else if (!ctrl && e.Key == Key.Left && at > 0) heads[at - 1].Focus(NavigationMethod.Tab);
                else if (!ctrl && e.Key == Key.Right && at < heads.Count - 1) heads[at + 1].Focus(NavigationMethod.Tab);
                else if (e.Key == Key.Apps || e.Key == Key.F10 && e.KeyModifiers == KeyModifiers.Shift) ColumnMenu(c, head, false);
                else if (e.Key == Key.Delete && !columns.IsReadOnly) RemoveColumn(c);
                else return;
                e.Handled = true;
            };
            HeadBack(at);
        }

        /// <summary>A heading's fill: the current column (the focused cell's) is tinted with the accent; a hover lifts it.</summary>
        void HeadBack(int i)
        {
            heads[i].Classes.Set("live", design || sorting); heads[i].Classes.Set("current", columns[i].Key == current);
        }

        void Dim(int at, bool on)
        {
            foreach (var g in Grids())
                foreach (var b in g.Children.OfType<Control>().Where(b => Grid.GetColumn(b) == at + 1 && b != ring)) b.Opacity = on ? 0.35 : 1;
        }

        void MoveColumn(int from, int to, bool focus)
        {
            var c = columns[from]; columns.RemoveAt(from); columns.Insert(to, c);
            Rebuild(); Changed?.Invoke();
            if (focus) Dispatcher.UIThread.Post(() => { if (to < heads.Count) heads[to].Focus(NavigationMethod.Tab); }, DispatcherPriority.Loaded);
        }

        void ColumnMenu(Column c, Control head, bool atPointer)
        {
            if (columns.IsReadOnly) return;
            int at = columns.IndexOf(c);
            Shade.Menu.Show(head, new[] { "Insert column before", "Insert column after", "Remove column" }, i => { if (i == 2) RemoveColumn(c); else InsertColumn(at + i); }, atPointer: atPointer);
        }

        /// <summary>A new Text column at index (named "New column"); it becomes the current column.</summary>
        public Column InsertColumn(int index)
        {
            if (columns.IsReadOnly) throw new InvalidOperationException("Inserting columns requires a mutable Columns collection.");
            int n = 1; while (columns.Any(x => x.Key == "c" + n)) n++;
            var c = new Column("c" + n, T("New column"));
            columns.Insert(Math.Clamp(index, 0, columns.Count), c);
            current = null; Rebuild(); Current(c.Key); Changed?.Invoke();
            Dispatcher.UIThread.Post(() => { int i = columns.IndexOf(c); if (i >= 0 && i < heads.Count) heads[i].Focus(NavigationMethod.Tab); }, DispatcherPriority.Loaded);
            return c;
        }

        async void RemoveColumn(Column c)
        {
            if (CanRemoveColumn?.Invoke(c) == false) return;
            bool yes = TopLevel.GetTopLevel(this) is Window w ? await Ask.ConfirmDelete(w, T("Remove the column “{0}” and its values?", c.Title), T("Remove column")) : true;
            if (!yes || readOnly || columns.IsReadOnly || !columns.Contains(c) || CanRemoveColumn?.Invoke(c) == false) return;
            columns.Remove(c);
            foreach (var x in columns) { x.Terms.RemoveAll(t => t.Key == c.Key); }
            foreach (var r in rows) { r.Values.Remove(c.Key); r.Units.Remove(c.Key); r.Alignments.Remove(c.Key); }
            if (current == c.Key) current = columns.FirstOrDefault()?.Key;
            Rebuild(); Changed?.Invoke(); CurrentColumnChanged?.Invoke(CurrentColumn);
        }

        /// <summary>The gap nearest the pointer (0..Count), or -1 off the table.</summary>
        int GapAt(PointerEventArgs e)
        {
            var p = e.GetPosition(overlay);
            var top = headView.TranslatePoint(default, overlay) ?? default;
            var bottom = footGrid.TranslatePoint(new Point(0, footGrid.Bounds.Height), overlay) ?? top;
            var bounds = new Rect(top, new Size(Math.Min(headGrid.Bounds.Width, headView.Bounds.Width), Math.Max(0, bottom.Y - top.Y)));
            if (!bounds.Inflate(new Thickness(40, 24)).Contains(p)) return -1;
            double x = e.GetPosition(headGrid).X, edge = Gutter; int best = 0; double bestD = Math.Abs(x - edge);
            for (int i = 0; i < widths.Length; i++) { edge += widths[i]; double d = Math.Abs(x - edge); if (d < bestD) { bestD = d; best = i + 1; } }
            return best;
        }

        void ShowDrop(int g, int from, string label, Point p)
        {
            bool show = g >= 0 && g != from && g != from + 1;
            if (ghost == null)
            {
                var grip = Build.Icon24(Icons.GripVertical, 14, 2); grip.Stroke = B(Text2); grip.VerticalAlignment = VerticalAlignment.Center;
                ghost = new Border
                {
                    Height = 28,
                    Padding = new Thickness(6, 0, 10, 0),
                    Background = B(Fill),
                    BorderBrush = B(Accent),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(Radius),
                    Opacity = 0.95,
                    BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 4, Blur = 12, Color = Color.FromArgb(90, 0, 0, 0) }),
                    Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { grip, Build.Text(label, Look.Text, TextSize.Label, 600) } },
                };
                overlay.Children.Add(ghost);
            }
            ghost.IsVisible = g >= 0;
            Canvas.SetLeft(ghost, p.X - 12); Canvas.SetTop(ghost, p.Y - 14);
            line.IsVisible = show;
            if (!show) return;
            var at = headGrid.TranslatePoint(new Point(Gutter + widths.Take(g).Sum(), 0), overlay) ?? default;
            var bottom = footGrid.TranslatePoint(new Point(0, footGrid.Bounds.Height), overlay) ?? at;
            Canvas.SetLeft(line, at.X - 1); Canvas.SetTop(line, at.Y); line.Height = Math.Max(0, bottom.Y - at.Y);
        }

        void EndDrag()
        {
            if (ghost != null) { overlay.Children.Remove(ghost); ghost = null; }
            line.IsVisible = false;
            foreach (var g in Grids()) foreach (var b in g.Children.OfType<Control>()) b.Opacity = 1;
            if (editing != null) editing.Texts[editCol].Opacity = 0;
        }
    }
}

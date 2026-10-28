// Shade - the table designer: a table's columns with preview rows, reordered by Ctrl-dragging a header (or Ctrl+Left/Right),
// with fields dropped in from other controls (TableDesigner.DragSource); an accent line marks the gap where it lands.
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using static Shade.Look;

namespace Shade
{
    /// <summary>A table's design (DESIGN.md): its columns over a few preview rows. A header is Ctrl-dragged
    /// from anywhere on it to the gap between two columns (or an end): the column dims, its header follows the pointer and a
    /// 2 px accent line marks the gap. Fields from other controls drop the same way (DragSource). A click selects a header;
    /// a right-click (or the Menu key, Shift+F10) offers Insert column before, Insert column after and Remove column;
    /// Ctrl+Left/Right moves the column, Left/Right moves between headers. The app owns the columns: it changes them on the
    /// events and sets Columns again.</summary>
    public sealed class TableDesigner : Border, IRestyle
    {
        /// <summary>A column: its heading; numbers sit at the right.</summary>
        public sealed class Column
        {
            public string Heading { get; }
            public bool Number { get; }
            public Column(string heading, bool number = false) { Heading = heading; Number = number; }
        }

        public static readonly DirectProperty<TableDesigner, int> SelectedProperty =
            AvaloniaProperty.RegisterDirect<TableDesigner, int>(nameof(Selected), o => o.Selected, (o, v) => o.Selected = v, -1, Avalonia.Data.BindingMode.TwoWay);
        public static readonly DirectProperty<TableDesigner, IReadOnlyList<Column>> ColumnsProperty = AvaloniaProperty.RegisterDirect<TableDesigner, IReadOnlyList<Column>>(nameof(Columns), x => x.Columns, (x, value) => x.Columns = value);
        public static readonly DirectProperty<TableDesigner, IReadOnlyList<string[]>> RowsProperty = AvaloniaProperty.RegisterDirect<TableDesigner, IReadOnlyList<string[]>>(nameof(Rows), x => x.Rows, (x, value) => x.Rows = value);
        public static readonly DirectProperty<TableDesigner, bool> LockedProperty = AvaloniaProperty.RegisterDirect<TableDesigner, bool>(nameof(Locked), x => x.Locked, (x, value) => x.Locked = value);
        public static readonly DirectProperty<TableDesigner, string> EmptyTextProperty = AvaloniaProperty.RegisterDirect<TableDesigner, string>(nameof(EmptyText), x => x.EmptyText, (x, value) => x.EmptyText = value);
        const double MinWidth0 = 72, MaxWidth0 = 360, Threshold = 4;
        IReadOnlyList<Column> columns = Array.Empty<Column>();
        IReadOnlyList<string[]> rows = Array.Empty<string[]>();
        int selected = -1, focusAfter = -1;
        readonly StackPanel grid = new StackPanel { Orientation = Orientation.Horizontal };
        readonly Border frame, empty;
        readonly ScrollViewer scroller;
        readonly Canvas overlay = new Canvas { IsHitTestVisible = false };  // not clipped: the line's dots and the dragged header may stand out
        readonly Border line = new Border { Width = 2, CornerRadius = new CornerRadius(1), IsVisible = false };
        readonly Border[] dots = { new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), IsVisible = false }, new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), IsVisible = false } };
        Border? ghost;
        double[] widths = Array.Empty<double>();
        readonly List<Border> heads = new List<Border>();
        readonly List<TextBlock> headings = new List<TextBlock>();

        /// <summary>A column was dragged or moved by the keyboard: from its index to its new index.</summary>
        public event Action<int, int>? Moved;
        /// <summary>Something from a DragSource was dropped: the payload and the gap (0 = before the first column, Count = after the last).</summary>
        public event Action<object, int>? Dropped;
        /// <summary>The menu's Insert column before / after: the index the new column takes, and the header to anchor a picker to.</summary>
        public event Action<int, Control>? InsertAsked;
        /// <summary>The menu's Remove column: its index.</summary>
        public event Action<int>? RemoveAsked;
        /// <summary>A header was selected (a click, Enter or Space): its index.</summary>
        public event Action<int>? SelectionChanged;

        public IReadOnlyList<Column> Columns { get => columns; set { ArgumentNullException.ThrowIfNull(value); SetAndRaise(ColumnsProperty, ref columns, value); if (selected >= columns.Count) Selected = -1; Rebuild(); } }
        /// <summary>The preview rows: a value per column.</summary>
        public IReadOnlyList<string[]> Rows { get => rows; set { ArgumentNullException.ThrowIfNull(value); SetAndRaise(RowsProperty, ref rows, value); Rebuild(); } }
        /// <summary>The selected column (a tinted header), or -1.</summary>
        public int Selected { get => selected; set { if (SetAndRaise(SelectedProperty, ref selected, value)) Paint(); } }
        /// <summary>A row's fill (a rule's colour), tinted as a selection is; null = none.</summary>
        public Func<string[], Color?>? RowFill { get => rowFill; set { rowFill = value; Rebuild(); } }
        Func<string[], Color?>? rowFill;
        /// <summary>No dragging, menu or keyboard moves: the structure is fixed.</summary>
        public bool Locked { get => locked; set { if (SetAndRaise(LockedProperty, ref locked, value)) Rebuild(); } }
        bool locked;
        /// <summary>Shown while there are no columns; the area takes drops.</summary>
        public string EmptyText { get => emptyText ?? T("Drop a field here to add a column"); set { var old = EmptyText; emptyText = value; RaisePropertyChanged(EmptyTextProperty, old, EmptyText); Rebuild(); } }
        string? emptyText;

        public TableDesigner()
        {
            frame = new Border { BorderBrush = B(Look.Separator), BorderThickness = new Thickness(1, 1, 0, 0), Child = grid, HorizontalAlignment = HorizontalAlignment.Left };
            empty = new Border { BorderBrush = B(Look.Separator), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(Radius), Padding = new Thickness(16), IsVisible = false };
            scroller = new ScrollViewer { Content = new StackPanel { Children = { frame, empty } }, BringIntoViewOnFocusChange = false, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
            overlay.Children.Add(line); foreach (var d in dots) overlay.Children.Add(d);
            Child = new Panel { Children = { scroller, overlay } };
            Words(this, designer => designer.Rebuild(), "structure");
        }

        /// <summary>Makes a control (a field list's row) a source for drags into any table designer in its window: the payload
        /// arrives with Dropped. label: the name the dragged chip shows (null = the payload's text).</summary>
        public static void DragSource(Control source, object payload, string? label = null)
        {
            Point start = default; bool pressed = false, moving = false;
            TableDesigner[] targets = Array.Empty<TableDesigner>();
            source.PointerPressed += (s, e) =>
            {
                if (!e.GetCurrentPoint(source).Properties.IsLeftButtonPressed) return;
                pressed = true; moving = false; start = e.GetPosition(source); e.Pointer.Capture(source); e.Handled = true;
                targets = (TopLevel.GetTopLevel(source)?.GetVisualDescendants().OfType<TableDesigner>().Where(t => !t.Locked && t.IsEffectivelyVisible).ToArray()) ?? Array.Empty<TableDesigner>();
            };
            source.PointerMoved += (s, e) =>
            {
                if (!pressed) return;
                var p = e.GetPosition(source);
                moving |= Math.Abs(p.X - start.X) + Math.Abs(p.Y - start.Y) >= Threshold;
                if (moving) foreach (var t in targets) t.Hover(e, -1, label ?? payload.ToString() ?? "");
            };
            source.PointerReleased += (s, e) =>
            {
                if (!pressed) return;
                bool dragged = moving;  // read first: letting the capture go raises PointerCaptureLost, which resets it
                pressed = false; moving = false; e.Pointer.Capture(null);
                foreach (var t in targets) { int at = dragged ? t.GapAt(e) : -1; t.EndDrag(); if (at >= 0) t.Dropped?.Invoke(payload, at); }
            };
            source.PointerCaptureLost += (s, e) => { pressed = false; moving = false; foreach (var t in targets) t.EndDrag(); };
        }

        static double TextWidth(string s, double size, int weight = 400) => Math.Ceiling(Look.Measure(s, Ui(size, weight))) + 2;

        void Rebuild()
        {
            grid.Children.Clear(); heads.Clear(); headings.Clear();
            frame.IsVisible = columns.Count > 0; empty.IsVisible = columns.Count == 0;
            empty.Child = Build.Text(EmptyText, Text3, TextSize.Body);
            widths = columns.Select((c, i) => Math.Clamp(Math.Max(TextWidth(c.Heading, TextSize.Label, 500) + M(16) + 1,
                rows.Select(r => TextWidth(i < r.Length ? r[i] : "", TextSize.Body) + M(16) + 1).DefaultIfEmpty(0).Max()), M(MinWidth0), M(MaxWidth0))).ToArray();  // padding, the cell's line
            for (int i = 0; i < columns.Count; i++)
            {
                var c = columns[i]; int at = i;
                var cells = new StackPanel { Width = widths[i] };
                var heading = Build.Text(c.Heading, i == selected ? Look.Text : Text2, TextSize.Label, 500); heading.TextTrimming = TextTrimming.CharacterEllipsis;
                var head = new Border
                {
                    Height = ControlHeight,
                    Padding = new Thickness(M(8), 0),
                    Focusable = true,
                    Cursor = Cursor.Default,
                    Background = B(i == selected ? AccentTint : Raised),
                    BorderBrush = B(Look.Separator),
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    Child = heading,
                };
                Words(head, word => ToolTip.SetTip(word, c.Heading + (Locked ? "" : " · " + T("Ctrl+drag to move"))), "tip");
                Words(head, word => Avalonia.Automation.AutomationProperties.SetName(word, T("Column · {0}", c.Heading)), "name");
                FocusRing(head);
                Wire(head, at);
                heads.Add(head); headings.Add(heading); cells.Children.Add(head);
                foreach (var r in rows)
                {
                    string v = i < r.Length ? r[i] : "";
                    var t = Build.Text(v, Look.Text, TextSize.Body); t.TextTrimming = TextTrimming.CharacterEllipsis; t.TextAlignment = c.Number ? TextAlignment.Right : TextAlignment.Left;
                    if (v != "") ToolTip.SetTip(t, v);
                    cells.Children.Add(new Border
                    {
                        Height = ControlHeight,
                        Padding = new Thickness(M(8), 0),
                        Child = t,
                        BorderBrush = B(Look.Separator),
                        BorderThickness = new Thickness(0, 0, 1, 1),
                        Background = RowFill?.Invoke(r) is Color fill ? B(Tint(fill)) : Brushes.Transparent
                    });
                }
                grid.Children.Add(cells);
            }
            line.Background = B(Accent); foreach (var d in dots) d.Background = B(Accent);
            if (focusAfter >= 0 && focusAfter < heads.Count) { var h = heads[focusAfter]; focusAfter = -1; Avalonia.Threading.Dispatcher.UIThread.Post(() => h.Focus(NavigationMethod.Tab)); }
        }

        /// <summary>A header: Ctrl+press starts a drag once it moves; else a click selects, a right-click opens the menu.</summary>
        void Wire(Border head, int at)
        {
            Point start = default; bool pressed = false, moving = false, reorder = false;
            void CursorFor(bool ctrl) => head.Cursor = !Locked && ctrl ? Build.Move : Cursor.Default;
            head.PointerPressed += (s, e) =>
            {
                var props = e.GetCurrentPoint(head).Properties;
                if (!props.IsLeftButtonPressed) return;
                pressed = true; moving = false; reorder = e.KeyModifiers.HasFlag(KeyModifiers.Control); start = e.GetPosition(head); head.Focus(NavigationMethod.Pointer);
                if (!Locked) e.Pointer.Capture(head);
                e.Handled = true;
            };
            head.PointerMoved += (s, e) =>
            {
                CursorFor(e.KeyModifiers.HasFlag(KeyModifiers.Control));
                if (!pressed || Locked || !reorder) return;
                if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) { if (moving) EndDrag(); moving = false; reorder = false; return; }
                var p = e.GetPosition(head);
                if (!moving && Math.Abs(p.X - start.X) + Math.Abs(p.Y - start.Y) >= Threshold) { moving = true; ((Control)grid.Children[at]).Opacity = 0.35; }
                if (moving) Hover(e, at, columns[at].Heading);
            };
            head.PointerReleased += (s, e) =>
            {
                if (e.InitialPressMouseButton == MouseButton.Right) { e.Handled = true; Menu(head, at, atPointer: true); return; }
                if (!pressed) return;
                bool dragged = moving;  // read first: letting the capture go raises PointerCaptureLost, which resets it
                pressed = false; moving = false; e.Pointer.Capture(null);
                if (dragged) { int g = e.KeyModifiers.HasFlag(KeyModifiers.Control) ? GapAt(e) : -1; EndDrag(); int to = g > at ? g - 1 : g; if (g >= 0 && to != at) Moved?.Invoke(at, to); }
                else Select(at);
            };
            head.PointerCaptureLost += (s, e) => { if (moving) EndDrag(); pressed = false; moving = false; };
            head.KeyUp += (_, e) => { if (e.Key is Key.LeftCtrl or Key.RightCtrl) { CursorFor(false); if (moving) EndDrag(); moving = false; reorder = false; } };
            head.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape && pressed) { if (moving) EndDrag(); moving = false; reorder = false; CursorFor(false); e.Handled = true; return; }
                bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
                CursorFor(ctrl);
                if (e.Key is Key.Enter or Key.Space) Select(at);
                else if (ctrl && e.Key == Key.Left && !Locked && at > 0) { focusAfter = at - 1; Moved?.Invoke(at, at - 1); }
                else if (ctrl && e.Key == Key.Right && !Locked && at < columns.Count - 1) { focusAfter = at + 1; Moved?.Invoke(at, at + 1); }
                else if (!ctrl && e.Key == Key.Left && at > 0) heads[at - 1].Focus(NavigationMethod.Tab);
                else if (!ctrl && e.Key == Key.Right && at < heads.Count - 1) heads[at + 1].Focus(NavigationMethod.Tab);
                else if (e.Key == Key.Apps || e.Key == Key.F10 && e.KeyModifiers == KeyModifiers.Shift) Menu(head, at, atPointer: false);
                else return;
                e.Handled = true;
            };
            head.GotFocus += (s, e) => { if (Look.IsKeyboard(e)) { head.BorderBrush = B(FocusBorder); head.BringIntoView(); } };
            head.LostFocus += (s, e) => head.BorderBrush = B(Look.Separator);
            head.PointerEntered += (s, e) => { if (at != selected) head.Background = B(Fill); };
            head.PointerExited += (s, e) => { head.Background = B(at == selected ? AccentTint : Raised); CursorFor(false); };
        }

        void Select(int at) { Selected = at; SelectionChanged?.Invoke(at); }

        /// <summary>The selected header tinted, its heading in Text; the others plain (a header keeps its focus).</summary>
        void Paint()
        {
            for (int i = 0; i < heads.Count; i++)
            {
                heads[i].Background = B(i == selected ? AccentTint : heads[i].IsPointerOver ? Fill : Raised);
                headings[i].Foreground = B(i == selected ? Look.Text : Text2);
            }
        }

        void Menu(Control head, int at, bool atPointer)
        {
            if (Locked) return;
            Shade.Menu.Show(head, new[] { "Insert column before", "Insert column after", "Remove column" },
                i => { if (i == 2) RemoveAsked?.Invoke(at); else InsertAsked?.Invoke(at + i, head); }, atPointer: atPointer);
        }

        /// <summary>The gap nearest the pointer (0..Count), or -1 when the pointer is off the designer.</summary>
        internal int GapAt(PointerEventArgs e)
        {
            var p = e.GetPosition(this);
            if (!new Rect(Bounds.Size).Inflate(new Thickness(0, 16)).Contains(p)) return -1;
            if (columns.Count == 0) return 0;
            double x = e.GetPosition(grid).X, edge = 0; int best = 0; double bestD = Math.Abs(x);
            for (int i = 0; i < widths.Length; i++) { edge += widths[i]; double d = Math.Abs(x - edge); if (d < bestD) { bestD = d; best = i + 1; } }
            return best;
        }

        /// <summary>A drag over the designer: the gap's line (none where the move would change nothing), the header at the pointer.</summary>
        internal void Hover(PointerEventArgs e, int from, string label) => ShowDrop(GapAt(e), from, label, e.GetPosition(this));

        /// <summary>The drag's picture: gap g's line (-1 = off the designer), the header label at p (the gallery draws it still).</summary>
        internal void ShowDrop(int g, int from, string label, Point p)
        {
            if (from >= 0 && from < grid.Children.Count) ((Control)grid.Children[from]).Opacity = 0.35;
            bool show = g >= 0 && (from < 0 || g != from && g != from + 1);
            if (ghost == null)
            {
                var grip = Build.Icon24(Icons.GripVertical, M(14), 2); grip.Stroke = B(Text2); grip.VerticalAlignment = VerticalAlignment.Center;
                ghost = new Border
                {
                    Height = ControlHeight,
                    Padding = new Thickness(M(6), 0, M(10), 0),
                    Background = B(Fill),
                    BorderBrush = B(Accent),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(Radius),
                    Opacity = 0.95,
                    BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 4, Blur = 12, Color = Color.FromArgb(90, 0, 0, 0) }),
                    Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = M(6), Children = { grip, Build.Text(label, Look.Text, TextSize.Label, 600) } },
                };
                overlay.Children.Add(ghost);
            }
            ghost.IsVisible = g >= 0;
            Canvas.SetLeft(ghost, p.X - 12); Canvas.SetTop(ghost, p.Y - 14);
            line.IsVisible = show; foreach (var d in dots) d.IsVisible = show;
            if (!show) return;
            if (columns.Count == 0) { line.IsVisible = false; foreach (var d in dots) d.IsVisible = false; empty.BorderBrush = B(Accent); return; }
            double x = widths.Take(g).Sum(), height = ControlHeight * (1 + rows.Count) + 1;
            var at = grid.TranslatePoint(new Point(x, 0), this) ?? new Point(x, 0);
            Canvas.SetLeft(line, at.X - 1); Canvas.SetTop(line, at.Y - 1); line.Height = height;
            Canvas.SetLeft(dots[0], at.X - 4); Canvas.SetTop(dots[0], at.Y - 5);
            Canvas.SetLeft(dots[1], at.X - 4); Canvas.SetTop(dots[1], at.Y + height - 4);
        }

        internal void EndDrag()
        {
            if (ghost != null) { overlay.Children.Remove(ghost); ghost = null; }
            line.IsVisible = false; foreach (var d in dots) d.IsVisible = false;
            empty.BorderBrush = B(Look.Separator);
            foreach (Control c in grid.Children) c.Opacity = 1;
        }

        /// <summary>Row fills are tints worked out from their colours: built again for a new mode or accent.</summary>
        public void Restyle() => Rebuild();
    }
}

// Shade - the editable table: rows edited in place under rich headings, values with units, computed columns of several
// columns, a footer that adds up, column resizing, three-state sorting, row selection and a row menu; in design mode its
// headings are the table designer (drag to a gap, insert, remove).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using static Shade.Look;

namespace Shade
{
    /// <summary>A table to edit in place (DESIGN_SYSTEM.md §6 Editable table): a grid of 1 px lines under headings with a
    /// subtitle; cells edit where they are (Enter and Shift+Enter go down and up the column) and wrap long text. A value with a
    /// unit cycles its unit on a click (the value stays as typed). A computed column joins any number of columns with + − × ÷
    /// (× and ÷ first). The footer shows a Sum, Average, Min, Max or Count per column, chosen from its menu. A heading's edge
    /// resizes its column (a double-click fits it); when the columns don't fit, text columns give way down to their longest word
    /// and the growing column takes or gives what a resize frees or uses. With Sorting, a heading click sorts up, down, then
    /// back to the original order. The row numbers select rows (Ctrl adds, Shift takes a range); a right-click or ⋯ opens
    /// Duplicate, Insert row above, Insert row below and Delete (which asks first); Add takes several rows at once. With Design,
    /// the headings are the table designer: drag one to a gap, right-click to insert or remove, Ctrl+Left/Right to move.
    /// The table edits the Columns and Rows lists it is given; Changed follows every edit.</summary>
    public sealed class EditableTable : Border, IRestyle
    {
        /// <summary>What a column holds.</summary>
        public enum Kind { Text, Number, Unit, Computed }
        /// <summary>How a term joins the formula: + − × ÷.</summary>
        public enum Op { Add, Subtract, Multiply, Divide }
        /// <summary>What a column's footer shows.</summary>
        public enum Total { None, Sum, Average, Min, Max, Count }

        /// <summary>A column: its heading and subtitle, what it holds and how it shows.</summary>
        public sealed class Column
        {
            public Column(string key, string title, Kind kind = Kind.Text) { Key = key; Title = title; Kind = kind; }
            /// <summary>The name its values are kept under in each row.</summary>
            public string Key { get; }
            public string Title { get; set; }
            /// <summary>The smaller, muted line under the heading ("€ / unit"); "" for none.</summary>
            public string Subtitle { get; set; } = "";
            public Kind Kind { get; set; }
            /// <summary>A Unit column's units, in the order a click cycles them.</summary>
            public IReadOnlyList<string> Units { get; set; } = Array.Empty<string>();
            /// <summary>Text after a number in its cell ("€").</summary>
            public string? Suffix { get; set; }
            /// <summary>Decimal places a computed value or a footer shows; null = as many as it has, up to four.</summary>
            public int? Decimals { get; set; }
            public bool Bold { get; set; }
            /// <summary>The column that takes the room left over (a name or description).</summary>
            public bool Grow { get; set; }
            /// <summary>Set by a resize; null = as wide as its content.</summary>
            public double? Width { get; set; }
            /// <summary>A Computed column's formula: the first term's column, then each term's operator and column.</summary>
            public List<Term> Terms { get; } = new List<Term>();
            public Total Footer { get; set; }
        }

        /// <summary>One step of a formula: an operator and a column (the first term's operator is not used).</summary>
        public sealed class Term
        {
            public Term(string key, Op op = Op.Add) { Key = key; Op = op; }
            public string Key { get; set; }
            public Op Op { get; set; }
        }

        /// <summary>A row: its values and its units, by column key.</summary>
        public sealed class Row
        {
            public Dictionary<string, string> Values { get; } = new Dictionary<string, string>();
            public Dictionary<string, string> Units { get; } = new Dictionary<string, string>();
        }

        public static readonly DirectProperty<EditableTable, bool> SortingProperty =
            AvaloniaProperty.RegisterDirect<EditableTable, bool>(nameof(Sorting), o => o.Sorting, (o, v) => o.Sorting = v, true, Avalonia.Data.BindingMode.TwoWay);
        public static readonly DirectProperty<EditableTable, bool> DesignProperty =
            AvaloniaProperty.RegisterDirect<EditableTable, bool>(nameof(Design), o => o.Design, (o, v) => o.Design = v, false, Avalonia.Data.BindingMode.TwoWay);
        public static readonly DirectProperty<EditableTable, double> HeadingSizeProperty =
            AvaloniaProperty.RegisterDirect<EditableTable, double>(nameof(HeadingSize), o => o.HeadingSize, (o, v) => o.HeadingSize = v, TextSize.Label);
        public static readonly DirectProperty<EditableTable, double> SubheadingSizeProperty =
            AvaloniaProperty.RegisterDirect<EditableTable, double>(nameof(SubheadingSize), o => o.SubheadingSize, (o, v) => o.SubheadingSize = v, TextSize.Caption);
        public static readonly DirectProperty<EditableTable, double> ValueSizeProperty =
            AvaloniaProperty.RegisterDirect<EditableTable, double>(nameof(ValueSize), o => o.ValueSize, (o, v) => o.ValueSize = v, TextSize.Body);

        const double Gutter = 36, Actions = 32, Threshold = 4;
        static readonly string[] OpText = { "+", "−", "×", "÷" };
        IList<Column> columns = new List<Column>();
        IList<Row> rows = new List<Row>();
        bool sorting = true, design, readOnly;
        double headingSize = TextSize.Label, subheadingSize = TextSize.Caption, valueSize = TextSize.Body;
        string? sortKey; int sortDir;
        readonly HashSet<Row> selected = new HashSet<Row>();
        Row? anchor;
        string? current;
        readonly Grid grid = new Grid();
        readonly ScrollViewer scroller;
        readonly Canvas overlay = new Canvas { IsHitTestVisible = false };
        readonly Border line = new Border { Width = 2, CornerRadius = new CornerRadius(1), IsVisible = false };
        readonly Field count = new Field("") { Width = 48, Text = "1" };
        readonly ActionButton addRows = new ActionButton("Add", ButtonKind.Ghost, Icons.Plus), addColumn = new ActionButton("Add column", ButtonKind.Ghost, Icons.Plus);
        Border? ghost;
        readonly Dictionary<(Row, string), TextBlock> computed = new Dictionary<(Row, string), TextBlock>();
        readonly Dictionary<string, Avalonia.Controls.Documents.Run> totals = new Dictionary<string, Avalonia.Controls.Documents.Run>();
        readonly List<Border> heads = new List<Border>(), underlines = new List<Border>();
        double[] widths = Array.Empty<double>();

        /// <summary>A value, a unit, a row or a column changed.</summary>
        public event Action? Changed;
        /// <summary>The column of the focused cell or heading changed (an app's column controls follow it).</summary>
        public event Action<Column?>? CurrentColumnChanged;
        /// <summary>The selected rows changed.</summary>
        public event Action<IReadOnlyList<Row>>? SelectionChanged;

        /// <summary>The columns, in order; the table moves, inserts and removes them in this list.</summary>
        public IList<Column> Columns { get => columns; set { columns = value; current = columns.FirstOrDefault()?.Key; Rebuild(); } }
        /// <summary>The rows, in their original order; the table adds, duplicates and deletes them in this list.</summary>
        public IList<Row> Rows { get => rows; set { rows = value; selected.Clear(); Rebuild(); } }
        /// <summary>A heading click sorts (up, down, back); off, the rows return to their original order.</summary>
        public bool Sorting { get => sorting; set { if (SetAndRaise(SortingProperty, ref sorting, value)) { if (!value) sortKey = null; Rebuild(); } } }
        /// <summary>The headings are the table designer (sorting pauses).</summary>
        public bool Design { get => design; set { if (SetAndRaise(DesignProperty, ref design, value)) { sortKey = null; Rebuild(); } } }
        /// <summary>Text sizes, from the type scale (TextSize): headings 12–14, subtitles 11–12, values 12–14.</summary>
        public double HeadingSize { get => headingSize; set { if (SetAndRaise(HeadingSizeProperty, ref headingSize, value)) Rebuild(); } }
        public double SubheadingSize { get => subheadingSize; set { if (SetAndRaise(SubheadingSizeProperty, ref subheadingSize, value)) Rebuild(); } }
        public double ValueSize { get => valueSize; set { if (SetAndRaise(ValueSizeProperty, ref valueSize, value)) Rebuild(); } }
        /// <summary>Values show but can't be edited; no row menu, no adding.</summary>
        public bool IsReadOnly { get => readOnly; set { readOnly = value; Rebuild(); } }
        /// <summary>The column of the focused cell or heading.</summary>
        public Column? CurrentColumn => columns.FirstOrDefault(c => c.Key == current);
        /// <summary>The selected rows, in the order shown.</summary>
        public IReadOnlyList<Row> Selected => Order().Where(selected.Contains).ToList();

        public EditableTable()
        {
            grid.HorizontalAlignment = HorizontalAlignment.Left;
            var frame = new Border { Child = grid, BorderThickness = new Thickness(1, 1, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            scroller = new ScrollViewer { Content = frame, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
            overlay.Children.Add(line);
            var rowsLabel = Build.Text("rows", Text2, TextSize.Label);
            addColumn.Margin = new Thickness(8, 0, 0, 0);
            var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 8, 0, 0), Children = { addRows, count, rowsLabel, addColumn } };
            DockPanel.SetDock(bar, Dock.Bottom);
            Child = new DockPanel { Children = { bar, new Panel { Children = { scroller, overlay } } } };
            addRows.Click += () => AddRows(int.TryParse(count.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 1, focus: true);
            count.Submit += () => addRows.PerformClick();
            addColumn.Click += () => InsertColumn(columns.Count);
            scroller.SizeChanged += (s, e) => { if (Math.Abs(e.NewSize.Width - e.PreviousSize.Width) > 0.5) Layout(); };
            KeyDown += (s, e) => { if (e.Key == Key.Escape && selected.Count > 0) { selected.Clear(); Rebuild(); SelectionChanged?.Invoke(Selected); e.Handled = true; } };
            Rebuild();
        }

        // ---- values -------------------------------------------------------------------------------------------------

        static double Number(string? text)
        {
            var t = (text ?? "").Trim().Replace(" ", "").Replace(',', '.');
            return t != "" && double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : double.NaN;
        }

        Column? Find(string key) => columns.FirstOrDefault(c => c.Key == key);

        /// <summary>A cell's number: typed, or worked out by its column's formula (× and ÷ before + and −); NaN when empty.</summary>
        public double Value(Row row, Column column) => Value(row, column, 0);

        double Value(Row row, Column column, int depth)
        {
            if (column.Kind != Kind.Computed) return Number(row.Values.GetValueOrDefault(column.Key));
            if (depth > 8 || column.Terms.Count == 0) return double.NaN;  // a formula that points at itself stops here
            double Of(Term t) => Find(t.Key) is Column c ? Value(row, c, depth + 1) : double.NaN;
            var sums = new List<double> { Of(column.Terms[0]) }; var ops = new List<Op>();
            for (int i = 1; i < column.Terms.Count; i++)
            {
                var t = column.Terms[i];
                if (t.Op == Op.Multiply) sums[^1] *= Of(t);
                else if (t.Op == Op.Divide) { double d = Of(t); sums[^1] = d == 0 ? double.NaN : sums[^1] / d; }
                else { ops.Add(t.Op); sums.Add(Of(t)); }
            }
            double n = sums[0];
            for (int i = 0; i < ops.Count; i++) n = ops[i] == Op.Add ? n + sums[i + 1] : n - sums[i + 1];
            return n;
        }

        static string Format(Column c, double n) => double.IsFinite(n) ? n.ToString(c.Decimals is int d ? "N" + d : "#,##0.####", CultureInfo.InvariantCulture) : "—";
        string Shown(Row r, Column c) => c.Kind == Kind.Computed ? Format(c, Value(r, c)) : r.Values.GetValueOrDefault(c.Key) ?? "";
        static string UnitOf(Row r, Column c) => r.Units.GetValueOrDefault(c.Key) is string u && c.Units.Contains(u) ? u : c.Units.FirstOrDefault() ?? "";
        static bool Numeric(Column c) => c.Kind != Kind.Text;
        /// <summary>The formula as words: "Total − Discount + Shipping".</summary>
        public string Formula(Column c) => string.Join("", c.Terms.Select((t, i) => (i > 0 ? " " + OpText[(int)t.Op] + " " : "") + (Find(t.Key)?.Title ?? "?")));

        /// <summary>The footer's value for a column ("" for None).</summary>
        public string Footer(Column c)
        {
            if (c.Footer == Total.None) return "";
            if (c.Footer == Total.Count) return rows.Count(r => c.Kind == Kind.Computed ? double.IsFinite(Value(r, c)) : (r.Values.GetValueOrDefault(c.Key) ?? "").Trim() != "").ToString(CultureInfo.InvariantCulture);
            var ns = rows.Select(r => Value(r, c)).Where(double.IsFinite).ToList();
            if (ns.Count == 0) return "—";
            return Format(c, c.Footer switch { Total.Sum => ns.Sum(), Total.Average => ns.Average(), Total.Min => ns.Min(), _ => ns.Max() });
        }

        IEnumerable<Row> Order()
        {
            if (sortKey == null || sortDir == 0 || Find(sortKey) is not Column c) return rows;
            if (Numeric(c))
            {
                var with = rows.Where(r => double.IsFinite(Value(r, c))); var without = rows.Where(r => !double.IsFinite(Value(r, c)));  // empty cells last, either way
                return (sortDir > 0 ? with.OrderBy(r => Value(r, c)) : with.OrderByDescending(r => Value(r, c))).Concat(without);
            }
            return sortDir > 0 ? rows.OrderBy(r => Shown(r, c), StringComparer.CurrentCultureIgnoreCase) : rows.OrderByDescending(r => Shown(r, c), StringComparer.CurrentCultureIgnoreCase);
        }

        // ---- widths ---------------------------------------------------------------------------------------------------

        static double TextWidth(string s, double size, int weight = 400) => Math.Ceiling(Look.Measure(s ?? "", Ui(size, weight)));

        /// <summary>A column's width from its whole content: the heading, every value with its unit or suffix, the footer.</summary>
        double Natural(Column c)
        {
            double w = Math.Max(TextWidth(c.Title, headingSize, 600), TextWidth(c.Subtitle, subheadingSize)) + (sortKey == c.Key ? 22 : 0) + (design ? 20 : 0);
            double extra = c.Kind == Kind.Unit ? c.Units.Select(u => TextWidth(u, TextSize.Label, 500)).DefaultIfEmpty(0).Max() + 16 : c.Suffix != null ? TextWidth(c.Suffix, TextSize.Caption) + 6 : 0;
            foreach (var r in rows) w = Math.Max(w, TextWidth(Shown(r, c), valueSize, c.Bold ? 600 : 400) + extra);
            if (c.Footer != Total.None) w = Math.Max(w, TextWidth(Footer(c), valueSize, 600) + TextWidth(c.Footer.ToString(), TextSize.Caption, 500) + 6);
            return Math.Ceiling(Math.Max(40, w + 20));
        }

        /// <summary>A text column can give way down to its longest word (its text wraps); numbers and resized columns can't.</summary>
        double Least(Column c)
        {
            double w = 0;
            foreach (var t in rows.Select(r => Shown(r, c)).Prepend(c.Title))
                foreach (var word in t.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)) w = Math.Max(w, TextWidth(word, valueSize, 600));
            return Math.Ceiling(Math.Max(60, w + 20));
        }

        /// <summary>The table fills its box: when the columns don't fit, text columns shrink toward their least width, sharing the
        /// cut by how much each can give; when there's room, the growing column takes it. Past every least width, it scrolls.</summary>
        void Layout()
        {
            var w = columns.Select(c => c.Width ?? Natural(c)).ToArray();
            double room = (scroller.Bounds.Width > 0 ? scroller.Bounds.Width : Bounds.Width) - 1 - Gutter - Actions, sum = w.Sum();
            int grow = columns.ToList().FindIndex(c => c.Grow && c.Width == null);
            if (room > 0 && sum > room)
            {
                var give = columns.Select((c, i) => c.Width != null || Numeric(c) ? 0 : Math.Max(0, w[i] - Least(c))).ToArray();
                double slack = give.Sum(), cut = Math.Min(slack, sum - room);
                if (slack > 0) for (int i = 0; i < w.Length; i++) w[i] -= give[i] * cut / slack;
            }
            else if (room > 0 && grow >= 0) w[grow] += room - sum;
            widths = w.Select(Math.Floor).ToArray();
            for (int i = 0; i < widths.Length && i + 1 < grid.ColumnDefinitions.Count; i++) grid.ColumnDefinitions[i + 1].Width = new GridLength(widths[i]);
        }

        // ---- drawing --------------------------------------------------------------------------------------------------

        Border Cell(Control child, int col, int row, IBrush? background = null)
        {
            var cell = new Border { Child = child, BorderBrush = B(Look.Separator), BorderThickness = new Thickness(0, 0, 1, 1), Background = background ?? B(Look.Background), MinHeight = 28 };
            Grid.SetColumn(cell, col); Grid.SetRow(cell, row); grid.Children.Add(cell);
            return cell;
        }

        /// <summary>Draws the table again from Columns and Rows (an app calls it after changing a column's properties).</summary>
        public void Refresh() => Rebuild();

        void Rebuild()
        {
            grid.Children.Clear(); grid.ColumnDefinitions.Clear(); grid.RowDefinitions.Clear(); computed.Clear(); totals.Clear(); heads.Clear(); underlines.Clear();
            ((Border)scroller.Content!).BorderBrush = B(Look.Separator);
            line.Background = B(Accent);
            addRows.IsVisible = count.IsVisible = !readOnly; ((Control)((StackPanel)addRows.Parent!).Children[2]).IsVisible = !readOnly;
            addColumn.IsVisible = design && !readOnly;
            grid.ColumnDefinitions.Add(new ColumnDefinition(Gutter, GridUnitType.Pixel));
            foreach (var _ in columns) grid.ColumnDefinitions.Add(new ColumnDefinition(80, GridUnitType.Pixel));
            grid.ColumnDefinitions.Add(new ColumnDefinition(Actions, GridUnitType.Pixel));
            var order = Order().ToList();
            for (int i = 0; i < order.Count + 2; i++) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            // headings
            bool all = order.Count > 0 && order.All(selected.Contains);
            var pickAll = Build.Text("#", Text3, TextSize.Caption); pickAll.HorizontalAlignment = HorizontalAlignment.Center;
            var corner = Cell(pickAll, 0, 0, B(Raised)); corner.Cursor = Build.Hand; ToolTip.SetTip(corner, all ? "Select none" : "Select all");
            corner.PointerReleased += (s, e) => { if (e.InitialPressMouseButton != MouseButton.Left) return; selected.Clear(); if (!all) foreach (var r in rows) selected.Add(r); Rebuild(); SelectionChanged?.Invoke(Selected); };
            for (int i = 0; i < columns.Count; i++) Heading(columns[i], i);
            Cell(new Panel(), columns.Count + 1, 0, B(Raised));
            // rows
            for (int r = 0; r < order.Count; r++)
            {
                var row = order[r]; bool on = selected.Contains(row);
                var number = Build.Text((r + 1).ToString(CultureInfo.InvariantCulture), on ? Look.Text : Text3, TextSize.Caption, on ? 600 : 400); number.HorizontalAlignment = HorizontalAlignment.Center;
                var marker = new Border { Width = 3, HorizontalAlignment = HorizontalAlignment.Left, Background = B(Accent), IsVisible = on };
                var gut = Cell(new Panel { Children = { marker, number } }, 0, r + 1, B(on ? AccentTint : Raised)); gut.Cursor = Build.Hand;
                Avalonia.Automation.AutomationProperties.SetName(gut, "Select row " + (r + 1));
                gut.PointerReleased += (s, e) => { if (e.InitialPressMouseButton == MouseButton.Left) Pick(row, e.KeyModifiers); };
                for (int i = 0; i < columns.Count; i++) Body(row, columns[i], r + 1, i + 1, on);
                var more = new ActionButton("", ButtonKind.Ghost, Icons.Ellipsis) { MinWidth = 0, Width = 24, Height = 24, KeepPointerFocus = true, HorizontalAlignment = HorizontalAlignment.Center, IsVisible = !readOnly };
                ToolTip.SetTip(more, "Row actions"); Avalonia.Automation.AutomationProperties.SetName(more, "Row actions");
                more.Click += () => RowMenu(row, more, false);
                Cell(more, columns.Count + 1, r + 1, B(on ? AccentTint : Look.Background));
            }
            // the footer
            int foot = order.Count + 1;
            Cell(new Panel(), 0, foot, B(Fill));
            for (int i = 0; i < columns.Count; i++)
            {
                var c = columns[i];
                // the function and its value on one line: two runs of one text share its baseline, whatever their sizes
                var label = new Avalonia.Controls.Documents.Run((c.Footer == Total.None ? "Calculate" : c.Footer.ToString()) + "\u2002") { FontSize = TextSize.Caption, FontWeight = Weight(500), Foreground = c.Footer == Total.None ? Brushes.Transparent : B(Text3) };
                var value = new Avalonia.Controls.Documents.Run(Footer(c)) { FontSize = valueSize, FontWeight = Weight(600), Foreground = B(Look.Text) }; totals[c.Key] = value;
                var line2 = Build.Text("", Look.Text, valueSize); line2.Text = null; line2.Inlines = new Avalonia.Controls.Documents.InlineCollection { label, value };
                line2.TextAlignment = Numeric(c) ? TextAlignment.Right : TextAlignment.Left; line2.HorizontalAlignment = HorizontalAlignment.Stretch;
                var cell = Cell(new Border { Padding = new Thickness(8, 4), Child = line2, Background = Brushes.Transparent }, i + 1, foot, B(Fill));
                cell.Cursor = Build.Hand; ToolTip.SetTip(cell, "Choose what the footer shows");
                cell.PointerEntered += (s, e) => { if (c.Footer == Total.None) label.Foreground = B(Text3); };
                cell.PointerExited += (s, e) => { if (c.Footer == Total.None) label.Foreground = Brushes.Transparent; };
                cell.PointerReleased += (s, e) => { if (e.InitialPressMouseButton == MouseButton.Left) FooterMenu(c, cell); };
            }
            Cell(new Panel(), columns.Count + 1, foot, B(Fill));
            Layout();
        }

        void Heading(Column c, int i)
        {
            bool sorted = sortKey == c.Key && sortDir != 0;
            var title = Build.Text(c.Title, Look.Text, headingSize, 600); title.TextWrapping = TextWrapping.Wrap; title.TextTrimming = TextTrimming.None;
            var words = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Children = { title } };
            if (c.Subtitle != "") { var sub = Build.Text(c.Subtitle, Text2, subheadingSize); sub.TextWrapping = TextWrapping.Wrap; sub.TextTrimming = TextTrimming.None; words.Children.Add(sub); }
            if (Numeric(c)) { words.HorizontalAlignment = HorizontalAlignment.Right; foreach (var t in words.Children.OfType<TextBlock>()) t.TextAlignment = TextAlignment.Right; }
            var row = new DockPanel { LastChildFill = true };
            if (design) { var grip = Build.Icon24(Icons.GripVertical, 14, 2); grip.Stroke = B(Text3); grip.VerticalAlignment = VerticalAlignment.Bottom; grip.Margin = new Thickness(0, 0, 4, 2); DockPanel.SetDock(grip, Dock.Left); row.Children.Add(grip); }
            if (sorted) { var arrow = Build.Icon24(sortDir > 0 ? Icons.ChevronUp : Icons.ChevronDown, 14, 2); arrow.Stroke = B(Text2); arrow.VerticalAlignment = VerticalAlignment.Bottom; arrow.Margin = new Thickness(4, 0, 0, 2); DockPanel.SetDock(arrow, Dock.Right); row.Children.Add(arrow); }
            row.Children.Add(words);
            var grip2 = new Border { Width = 7, Background = Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, -4, 0), Cursor = new Cursor(StandardCursorType.SizeWestEast) };
            var edge = new Border { Width = 2, HorizontalAlignment = HorizontalAlignment.Right, Background = B(Accent), IsVisible = false, Margin = new Thickness(0, 0, -1, 0), IsHitTestVisible = false };
            ToolTip.SetTip(grip2, "Drag to resize; double-click to fit");
            var underline = new Border { Height = 2, VerticalAlignment = VerticalAlignment.Bottom, Background = B(Accent), IsVisible = c.Key == current, IsHitTestVisible = false };  // the current column
            underlines.Add(underline);
            var inner = new Panel { Children = { new Border { Padding = new Thickness(8, 4), Child = row }, underline, edge, grip2 } };
            var head = Cell(inner, i + 1, 0, B(Raised));
            head.Focusable = design; heads.Add(head);
            if (c.Kind == Kind.Computed) ToolTip.SetTip(head, "= " + Formula(c));
            Avalonia.Automation.AutomationProperties.SetName(head, "Column · " + c.Title);
            grip2.PointerEntered += (s, e) => edge.IsVisible = true;
            grip2.PointerExited += (s, e) => edge.IsVisible = false;
            Resize(grip2, c, edge);
            Wire(head, c, i);
        }

        void Body(Row row, Column c, int r, int col, bool on)
        {
            IBrush back = B(on ? AccentTint : Look.Background);
            var ring = new Border { BorderBrush = B(Accent), BorderThickness = new Thickness(1), IsHitTestVisible = false, IsVisible = false };
            var face = new Panel();
            if (c.Kind == Kind.Computed || readOnly)
            {
                var t = Build.Text(Shown(row, c), c.Kind == Kind.Computed ? Text2 : Look.Text, valueSize, c.Bold ? 600 : 400);
                t.TextWrapping = TextWrapping.Wrap; t.TextTrimming = TextTrimming.None; t.Margin = new Thickness(8, 4, Room(c), 4);
                t.TextAlignment = Numeric(c) ? TextAlignment.Right : TextAlignment.Left; t.VerticalAlignment = VerticalAlignment.Top;
                if (c.Kind == Kind.Computed) computed[(row, c.Key)] = t;
                var holder = new Border { Child = t, Focusable = true, Background = Brushes.Transparent };  // read only, still focusable: the column's controls follow
                face.Children.Add(holder);
            }
            else
            {
                var box = Field.Plain();
                box.Text = row.Values.GetValueOrDefault(c.Key) ?? ""; box.FontSize = valueSize; box.FontWeight = c.Bold ? FontWeight.SemiBold : FontWeight.Normal;
                box.TextWrapping = TextWrapping.Wrap; box.AcceptsReturn = false; box.Padding = new Thickness(8, 4, Room(c), 4); box.VerticalAlignment = VerticalAlignment.Stretch;
                box.VerticalContentAlignment = VerticalAlignment.Top; box.TextAlignment = Numeric(c) ? TextAlignment.Right : TextAlignment.Left;
                Avalonia.Automation.AutomationProperties.SetName(box, c.Title);
                box.ContextFlyout = null; box.ContextMenu = null;
                box.TextChanged += (s, e) =>
                {
                    if ((row.Values.GetValueOrDefault(c.Key) ?? "") == (box.Text ?? "")) return;
                    row.Values[c.Key] = box.Text ?? ""; Recount(row); Changed?.Invoke();
                };
                box.AddHandler(KeyDownEvent, (s, e) =>
                {
                    if (e.Key != Key.Enter) return;
                    e.Handled = true;  // no new lines: Enter goes down the column, Shift+Enter up
                    var list = Order().ToList(); int at = list.IndexOf(row) + (e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1);
                    if (at >= 0 && at < list.Count) FocusCell(list[at], c.Key);
                }, RoutingStrategies.Tunnel);
                face.Children.Add(box);
                if (c.Kind == Kind.Unit && c.Units.Count > 0)
                {
                    var unit = new ActionButton(UnitOf(row, c), ButtonKind.Ghost) { MinWidth = 0, Height = 22, Padding = new Thickness(6, 0), KeepPointerFocus = true, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 1, 2, 0) };  // centred on the value's first line
                    ToolTip.SetTip(unit, "Click to change the unit"); Avalonia.Automation.AutomationProperties.SetName(unit, "Unit");
                    unit.Click += () =>
                    {
                        var units = c.Units.ToList(); var next = units[(units.IndexOf(UnitOf(row, c)) + 1) % units.Count];
                        row.Units[c.Key] = next; unit.Text = next; Recount(row); Changed?.Invoke();
                    };
                    face.Children.Add(unit);
                }
                else if (c.Suffix != null)
                {
                    var suffix = Build.Text(c.Suffix, Text3, TextSize.Caption); suffix.HorizontalAlignment = HorizontalAlignment.Right; suffix.VerticalAlignment = VerticalAlignment.Top; suffix.Margin = new Thickness(0, 6, 8, 0); suffix.IsHitTestVisible = false;
                    face.Children.Add(suffix);
                }
            }
            face.Children.Add(ring);
            var cell = Cell(face, col, r, back);
            cell.GotFocus += (s, e) => { ring.IsVisible = true; Current(c.Key); };
            cell.LostFocus += (s, e) => ring.IsVisible = cell.IsKeyboardFocusWithin;
            // a right-click anywhere in a row is its menu (the text box's own Cut/Copy/Paste flyout would hide it)
            cell.AddHandler(PointerReleasedEvent, (s, e) => { if (e.InitialPressMouseButton == MouseButton.Right && !readOnly) { e.Handled = true; RowMenu(row, cell, true); } }, RoutingStrategies.Bubble, handledEventsToo: true);
        }

        /// <summary>Room at a cell's right for its unit or suffix, so a value never runs under it.</summary>
        double Room(Column c) => c.Kind == Kind.Unit && c.Units.Count > 0 ? c.Units.Select(u => TextWidth(u, TextSize.Label, 500)).Max() + 20 : c.Suffix != null ? TextWidth(c.Suffix, TextSize.Caption) + 14 : 8;

        void FocusCell(Row row, string key)
        {
            var box = grid.Children.OfType<Border>().Where(b => Grid.GetRow(b) == Order().ToList().IndexOf(row) + 1 && Grid.GetColumn(b) == columns.ToList().FindIndex(c => c.Key == key) + 1)
                .SelectMany(b => b.GetVisualDescendants().OfType<TextBox>()).FirstOrDefault();
            if (box != null) { box.Focus(NavigationMethod.Tab); box.SelectAll(); }
        }

        void Current(string key)
        {
            if (current == key) return;
            current = key;
            for (int i = 0; i < underlines.Count && i < columns.Count; i++) underlines[i].IsVisible = columns[i].Key == key;
            CurrentColumnChanged?.Invoke(CurrentColumn);
        }

        /// <summary>After an edit: the row's computed cells, the footers and the widths, without drawing the table again (the caret stays).</summary>
        void Recount(Row row)
        {
            foreach (var c in columns)
            {
                if (c.Kind == Kind.Computed && computed.TryGetValue((row, c.Key), out var t)) t.Text = Shown(row, c);
                if (totals.TryGetValue(c.Key, out var f)) f.Text = Footer(c);
            }
            Layout();
        }

        // ---- rows -----------------------------------------------------------------------------------------------------

        Row Blank() { var r = new Row(); foreach (var c in columns.Where(c => c.Kind == Kind.Unit && c.Units.Count > 0)) r.Units[c.Key] = c.Units[0]; return r; }

        /// <summary>Adds n empty rows (1–100) at the end; focus: the first new row's first cell takes the keyboard.</summary>
        public void AddRows(int n, bool focus = false)
        {
            n = Math.Clamp(n, 1, 100); count.Text = n.ToString(CultureInfo.InvariantCulture);
            Row? first = null;
            for (int i = 0; i < n; i++) { var r = Blank(); rows.Add(r); first ??= r; }
            sortKey = null;  // new rows show at the end, where they were added
            Rebuild(); Changed?.Invoke();
            if (focus && first != null && columns.Count > 0) Avalonia.Threading.Dispatcher.UIThread.Post(() => FocusCell(first, columns[0].Key), Avalonia.Threading.DispatcherPriority.Loaded);
        }

        void Pick(Row row, KeyModifiers mods)
        {
            var list = Order().ToList();
            if (mods.HasFlag(KeyModifiers.Shift) && anchor != null && list.Contains(anchor))
            {
                if (!mods.HasFlag(KeyModifiers.Control)) selected.Clear();
                int a = list.IndexOf(anchor), b = list.IndexOf(row);
                for (int i = Math.Min(a, b); i <= Math.Max(a, b); i++) selected.Add(list[i]);
            }
            else if (mods.HasFlag(KeyModifiers.Control)) { if (!selected.Remove(row)) selected.Add(row); anchor = row; }
            else { bool only = selected.Count == 1 && selected.Contains(row); selected.Clear(); if (!only) selected.Add(row); anchor = row; }
            Rebuild(); SelectionChanged?.Invoke(Selected);
        }

        void RowMenu(Row row, Control anchorControl, bool atPointer)
        {
            if (!selected.Contains(row)) { selected.Clear(); selected.Add(row); anchor = row; Rebuild(); SelectionChanged?.Invoke(Selected); }
            var targets = Selected; int n = targets.Count; string many = n > 1 ? " " + n + " rows" : "";
            var items = new[] { "Duplicate" + many, "Insert row above", "Insert row below", Shade.Menu.Divider, "Delete" + (n > 1 ? many : " row") };
            var anchorNow = grid.Children.OfType<Control>().Contains(anchorControl) ? anchorControl : this;
            Shade.Menu.Show(anchorNow, items, async i =>
            {
                int last = rows.IndexOf(targets[^1]);
                if (i == 0)
                {
                    var copies = targets.Select(t => { var r = new Row(); foreach (var kv in t.Values) r.Values[kv.Key] = kv.Value; foreach (var kv in t.Units) r.Units[kv.Key] = kv.Value; return r; }).ToList();
                    for (int k = 0; k < copies.Count; k++) rows.Insert(last + 1 + k, copies[k]);
                    selected.Clear(); foreach (var r in copies) selected.Add(r);
                    Rebuild(); Changed?.Invoke(); SelectionChanged?.Invoke(Selected);
                }
                else if (i == 1 || i == 2)
                {
                    var b = Blank(); rows.Insert(i == 1 ? rows.IndexOf(targets[0]) : last + 1, b);
                    Rebuild(); Changed?.Invoke();
                    if (columns.Count > 0) Avalonia.Threading.Dispatcher.UIThread.Post(() => FocusCell(b, columns[0].Key), Avalonia.Threading.DispatcherPriority.Loaded);
                }
                else if (i == 4)
                {
                    bool yes = TopLevel.GetTopLevel(this) is Window w ? await Ask.ConfirmDelete(w, n > 1 ? "Delete " + n + " rows?" : "Delete this row?", n > 1 ? "Delete " + n + " rows" : "Delete row") : true;
                    if (!yes) return;
                    foreach (var t in targets) rows.Remove(t);
                    selected.Clear(); Rebuild(); Changed?.Invoke(); SelectionChanged?.Invoke(Selected);
                }
            }, atPointer: atPointer);
        }

        void FooterMenu(Column c, Control cell)
        {
            var choices = Numeric(c) ? Enum.GetValues<Total>() : new[] { Total.None, Total.Count };
            Shade.Menu.Show(cell, choices.Select(t => t.ToString()).ToList(), i => { c.Footer = choices[i]; Rebuild(); Changed?.Invoke(); }, Array.IndexOf(choices, c.Footer));
        }

        // ---- headings: resize, sort, design ----------------------------------------------------------------------------

        void Resize(Border grip, Column c, Border edge)
        {
            double start = 0, from = 0; bool dragging = false;
            grip.PointerPressed += (s, e) =>
            {
                if (!e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed) return;
                e.Handled = true;
                if (e.ClickCount == 2) { c.Width = null; Layout(); return; }  // a double-click fits the column to its content
                dragging = true; start = e.GetPosition(this).X; from = widths[columns.IndexOf(c)]; edge.IsVisible = true; e.Pointer.Capture(grip);
            };
            grip.PointerMoved += (s, e) => { if (!dragging) return; c.Width = Math.Max(40, Math.Round(from + e.GetPosition(this).X - start)); Layout(); };
            grip.PointerReleased += (s, e) => { if (!dragging) return; dragging = false; e.Pointer.Capture(null); edge.IsVisible = grip.IsPointerOver; e.Handled = true; Changed?.Invoke(); };
            grip.PointerCaptureLost += (s, e) => { dragging = false; edge.IsVisible = false; };
        }

        /// <summary>A heading: a click sorts (or, designing, makes it current); designing, a press anywhere starts a drag once it
        /// moves, a right-click opens the column menu, Ctrl+Left/Right moves it.</summary>
        void Wire(Border head, Column c, int at)
        {
            Point start = default; bool pressed = false, moving = false;
            head.PointerPressed += (s, e) =>
            {
                if (!e.GetCurrentPoint(head).Properties.IsLeftButtonPressed || e.Handled) return;
                pressed = true; moving = false; start = e.GetPosition(head); e.Handled = true;
                if (design) { head.Focus(NavigationMethod.Pointer); e.Pointer.Capture(head); }
            };
            head.PointerMoved += (s, e) =>
            {
                if (!pressed || !design) return;
                var p = e.GetPosition(head);
                if (!moving && Math.Abs(p.X - start.X) + Math.Abs(p.Y - start.Y) >= Threshold) { moving = true; Dim(at, true); }
                if (moving) ShowDrop(GapAt(e), at, c.Title, e.GetPosition(this));
            };
            head.PointerReleased += (s, e) =>
            {
                if (e.InitialPressMouseButton == MouseButton.Right && design) { e.Handled = true; ColumnMenu(c, head, true); return; }
                if (!pressed) return;
                bool dragged = moving;  // read first: letting the capture go raises PointerCaptureLost, which resets it
                pressed = false; moving = false; e.Pointer.Capture(null);
                if (dragged) { int g = GapAt(e); EndDrag(); int to = g > at ? g - 1 : g; if (g >= 0 && to != at) MoveColumn(at, to, true); return; }
                if (design) { Current(c.Key); return; }
                if (!sorting) return;
                sortDir = sortKey != c.Key ? 1 : sortDir == 1 ? -1 : 0; sortKey = sortDir == 0 ? null : c.Key;
                Rebuild();
            };
            head.PointerCaptureLost += (s, e) => { if (moving) EndDrag(); pressed = false; moving = false; };
            head.GotFocus += (s, e) => Current(c.Key);
            head.KeyDown += (s, e) =>
            {
                if (!design) return;
                bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
                if (ctrl && e.Key == Key.Left && at > 0) MoveColumn(at, at - 1, true);
                else if (ctrl && e.Key == Key.Right && at < columns.Count - 1) MoveColumn(at, at + 1, true);
                else if (!ctrl && e.Key == Key.Left && at > 0) heads[at - 1].Focus(NavigationMethod.Tab);
                else if (!ctrl && e.Key == Key.Right && at < heads.Count - 1) heads[at + 1].Focus(NavigationMethod.Tab);
                else if (e.Key == Key.Apps || e.Key == Key.F10 && e.KeyModifiers == KeyModifiers.Shift) ColumnMenu(c, head, false);
                else if (e.Key == Key.Delete) RemoveColumn(c);
                else return;
                e.Handled = true;
            };
            head.PointerEntered += (s, e) => { if (design || sorting) head.Background = B(Fill); };
            head.PointerExited += (s, e) => head.Background = B(Raised);
        }

        void Dim(int at, bool on) { foreach (var b in grid.Children.OfType<Border>().Where(b => Grid.GetColumn(b) == at + 1)) b.Opacity = on ? 0.35 : 1; }

        void MoveColumn(int from, int to, bool focus)
        {
            var c = columns[from]; columns.RemoveAt(from); columns.Insert(to, c);
            Rebuild(); Changed?.Invoke();
            if (focus) Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (to < heads.Count) heads[to].Focus(NavigationMethod.Tab); }, Avalonia.Threading.DispatcherPriority.Loaded);
        }

        void ColumnMenu(Column c, Control head, bool atPointer)
        {
            int at = columns.IndexOf(c);
            Shade.Menu.Show(head, new[] { "Insert column before", "Insert column after", "Remove column" }, i => { if (i == 2) RemoveColumn(c); else InsertColumn(at + i); }, atPointer: atPointer);
        }

        /// <summary>A new Text column at index (named "New column"); it becomes the current column.</summary>
        public Column InsertColumn(int index)
        {
            int n = 1; while (columns.Any(x => x.Key == "c" + n)) n++;
            var c = new Column("c" + n, "New column");
            columns.Insert(Math.Clamp(index, 0, columns.Count), c);
            current = null; Rebuild(); Current(c.Key); Changed?.Invoke();
            Avalonia.Threading.Dispatcher.UIThread.Post(() => { int i = columns.IndexOf(c); if (i >= 0 && i < heads.Count) heads[i].Focus(NavigationMethod.Tab); }, Avalonia.Threading.DispatcherPriority.Loaded);
            return c;
        }

        async void RemoveColumn(Column c)
        {
            bool yes = TopLevel.GetTopLevel(this) is Window w ? await Ask.ConfirmDelete(w, "Remove the column “" + c.Title + "” and its values?", "Remove column") : true;
            if (!yes) return;
            columns.Remove(c);
            foreach (var x in columns) { x.Terms.RemoveAll(t => t.Key == c.Key); }
            foreach (var r in rows) { r.Values.Remove(c.Key); r.Units.Remove(c.Key); }
            if (current == c.Key) current = columns.FirstOrDefault()?.Key;
            Rebuild(); Changed?.Invoke(); CurrentColumnChanged?.Invoke(CurrentColumn);
        }

        /// <summary>The gap nearest the pointer (0..Count), or -1 off the table.</summary>
        int GapAt(PointerEventArgs e)
        {
            var p = e.GetPosition(this);
            if (!new Rect(Bounds.Size).Inflate(new Thickness(40, 24)).Contains(p)) return -1;
            double x = e.GetPosition(grid).X, edge = Gutter; int best = 0; double bestD = Math.Abs(x - edge);
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
            var at = grid.TranslatePoint(new Point(Gutter + widths.Take(g).Sum(), 0), this) ?? default;
            Canvas.SetLeft(line, at.X - 1); Canvas.SetTop(line, at.Y); line.Height = grid.Bounds.Height;
        }

        void EndDrag()
        {
            if (ghost != null) { overlay.Children.Remove(ghost); ghost = null; }
            line.IsVisible = false;
            foreach (var b in grid.Children.OfType<Border>()) b.Opacity = 1;
        }

        internal bool LineShown => line.IsVisible;

        /// <summary>Tints and the accent marker are worked out from the mode and accent: drawn again on a switch.</summary>
        public void Restyle() => Rebuild();
    }
}

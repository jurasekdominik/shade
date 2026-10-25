// Shade - the editable table: rows edited in place under rich headings, values with units, computed columns of several
// columns, a footer that adds up, column widths that fit, fill or stay fixed, three-state sorting, row selection and a row
// menu; in design mode its headings are the table designer (drag to a gap, insert, remove).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using static Shade.Look;

namespace Shade
{
    /// <summary>A table to edit in place (DESIGN_SYSTEM.md §6 Editable table): a grid of 1 px lines under headings with a
    /// subtitle; a click on a cell edits it there, the caret where it was clicked (Enter and Shift+Enter go down and up the
    /// column, Tab to the next cell); long text wraps. A value with a unit cycles its unit on a click (the value stays as typed).
    /// A computed column joins any number of columns with + − × ÷ (× and ÷ first), in decimals, so money adds up to the cent;
    /// an app with its own money rules supplies the values (Calculate). The footer shows a Sum, Average, Min, Max or Count per
    /// column, chosen from its menu. A column fits its content (heading included, never cut), fills the room left over, or
    /// keeps a fixed width; each can have a minimum and a maximum. A heading's edge resizes its column (a fill column's
    /// minimum); a double-click fits it again. The heading and the footer stay put while the rows scroll, the scroll bars
    /// outside the grid. With Sorting, a heading click sorts up, down, then back to the original order. The row numbers select
    /// rows (Ctrl adds, Shift takes a range); a right-click or ⋯ opens Duplicate, Insert row above, Insert row below and Delete
    /// (which asks first); Add takes several rows at once. With Design, the headings are the table designer: drag one to a
    /// gap, right-click to insert or remove, Ctrl+Left/Right to move. The table edits the Columns and Rows lists it is given;
    /// Changed follows every edit.</summary>
    public sealed class EditableTable : Border, IRestyle
    {
        /// <summary>What a column holds.</summary>
        public enum Kind { Text, Number, Unit, Computed }
        /// <summary>How a term joins the formula: + − × ÷.</summary>
        public enum Op { Add, Subtract, Multiply, Divide }
        /// <summary>What a column's footer shows.</summary>
        public enum Total { None, Sum, Average, Min, Max, Count }
        /// <summary>How a column takes its width: Fit (its heading and every value, never cut), Fill (shares the room the
        /// others leave), Fixed (Width).</summary>
        public enum Sizing { Fit, Fill, Fixed }

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
            /// <summary>How it takes its width; Fit unless set (Grow is Fill, a Width is Fixed).</summary>
            public Sizing Sizing { get; set; }
            /// <summary>The column that takes the room left over (a name or description): Sizing Fill.</summary>
            public bool Grow { get => Sizing == Sizing.Fill; set => Sizing = value ? Sizing.Fill : Sizing == Sizing.Fill ? Sizing.Fit : Sizing; }
            /// <summary>A Fixed column's width (a resize sets it); setting it makes a Fit column Fixed, null makes it Fit again.</summary>
            public double? Width
            {
                get => width;
                set { width = value; if (value != null && Sizing == Sizing.Fit) Sizing = Sizing.Fixed; else if (value == null && Sizing == Sizing.Fixed) Sizing = Sizing.Fit; }
            }
            double? width;
            /// <summary>The narrowest it gets, whatever its sizing (a resize of a Fill column sets it); null = 40 px, a Fill
            /// column its longest word.</summary>
            public double? MinWidth { get; set; }
            /// <summary>The widest it gets (its text wraps past it); null = no limit.</summary>
            public double? MaxWidth { get; set; }
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

        /// <summary>A row as drawn: a grid of its own (an edit measures this row, not the table) and each cell's measured text
        /// (NaN until measured).</summary>
        sealed class Line
        {
            public Line(Row row, int n) { Row = row; Cells = new Border[n]; Texts = new TextBlock[n]; Units = new ActionButton?[n]; Full = new double[n]; Word = new double[n]; Forget(); }
            public readonly Row Row;
            public readonly Grid Panel = new Grid();
            public Border Gutter = null!, Marker = null!;
            public TextBlock Number = null!;
            public readonly Border[] Cells;
            public readonly TextBlock[] Texts;
            public readonly ActionButton?[] Units;
            public readonly double[] Full, Word;
            public void Forget() { Array.Fill(Full, double.NaN); Array.Fill(Word, double.NaN); }
        }

        /// <summary>The heading, the rows, the footer and the horizontal bar, top to bottom; the rows get the height left (they
        /// scroll) and the vertical bar sits at their right, both bars outside the grid.</summary>
        sealed class Pinned : Panel
        {
            readonly Control head, body, foot, across, down;
            public Pinned(Control head, Control body, Control foot, Control across, Control down)
            {
                this.head = head; this.body = body; this.foot = foot; this.across = across; this.down = down;
                Children.AddRange(new[] { head, body, foot, across, down });
            }

            double Side => down.IsVisible ? down.DesiredSize.Width : 0;

            protected override Size MeasureOverride(Size room)
            {
                down.Measure(room);
                var inner = new Size(Math.Max(0, room.Width - Side), double.PositiveInfinity);
                head.Measure(inner); foot.Measure(inner); across.Measure(inner);
                double still = head.DesiredSize.Height + foot.DesiredSize.Height + across.DesiredSize.Height;
                body.Measure(new Size(inner.Width, Math.Max(0, room.Height - still)));
                double w = Math.Max(Math.Max(head.DesiredSize.Width, body.DesiredSize.Width), foot.DesiredSize.Width) + Side;
                return new Size(w, still + body.DesiredSize.Height);
            }

            protected override Size ArrangeOverride(Size size)
            {
                double w = Math.Max(0, size.Width - Side), y = 0;
                double still = head.DesiredSize.Height + foot.DesiredSize.Height + across.DesiredSize.Height;
                double rows = Math.Max(0, Math.Min(body.DesiredSize.Height, size.Height - still));
                head.Arrange(new Rect(0, y, w, head.DesiredSize.Height)); y += head.DesiredSize.Height;
                body.Arrange(new Rect(0, y, w, rows)); down.Arrange(new Rect(w, y, Side, rows)); y += rows;
                foot.Arrange(new Rect(0, y, w, foot.DesiredSize.Height)); y += foot.DesiredSize.Height;
                across.Arrange(new Rect(0, y, w, across.DesiredSize.Height));
                return size;
            }
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

        // Bar: a scroll bar (8 px) and its 4 px gap from the grid
        const double Gutter = 36, Actions = 32, Threshold = 4, Bar = 12;
        static readonly string[] OpText = { "+", "−", "×", "÷" };
        IList<Column> columns = new List<Column>();
        IList<Row> rows = new List<Row>();
        bool sorting = true, design, readOnly, syncing;
        double headingSize = TextSize.Label, subheadingSize = TextSize.Caption, valueSize = TextSize.Body;
        string? sortKey; int sortDir;
        readonly HashSet<Row> selected = new HashSet<Row>();
        Row? anchor;
        string? current;
        readonly Grid headGrid = new Grid(), bodyGrid = new Grid(), footGrid = new Grid();
        readonly ScrollViewer headView, bodyView, footView;
        readonly ScrollBar across = new ScrollBar { Orientation = Orientation.Horizontal, AllowAutoHide = false, IsVisible = false, Margin = new Thickness(0, 4, 0, 0) };
        readonly ScrollBar down = new ScrollBar { Orientation = Orientation.Vertical, AllowAutoHide = false, IsVisible = false, Margin = new Thickness(4, 0, 0, 0) };
        readonly Pinned area;
        readonly Canvas overlay = new Canvas { IsHitTestVisible = false, ClipToBounds = true };
        readonly Border line = new Border { Width = 2, CornerRadius = new CornerRadius(1), IsVisible = false };
        readonly Field count = new Field("") { Width = 48, Text = "1" };
        readonly TextBlock rowsLabel = Build.Text("rows", Text2, TextSize.Label);
        readonly ActionButton addRows = new ActionButton("Add", ButtonKind.Ghost, Icons.Plus), addColumn = new ActionButton("Add column", ButtonKind.Ghost, Icons.Plus);
        /// <summary>The one text box: it moves to the cell being edited, over that cell's text.</summary>
        readonly TextBox editor = Field.Plain();
        /// <summary>The focused cell's outline, one for the table.</summary>
        readonly Border ring = new Border { BorderThickness = new Thickness(1), IsHitTestVisible = false, IsVisible = false, ZIndex = 2, Margin = new Thickness(0, 0, 1, 1) };
        readonly Cursor ibeam = new Cursor(StandardCursorType.Ibeam);
        Border? ghost;
        readonly Dictionary<Row, Line> lines = new Dictionary<Row, Line>();
        List<Line> shown = new List<Line>();
        Line? editing; int editCol;
        readonly Dictionary<string, Avalonia.Controls.Documents.Run> totals = new Dictionary<string, Avalonia.Controls.Documents.Run>();
        readonly List<Border> heads = new List<Border>();
        double[] widths = Array.Empty<double>(), rooms = Array.Empty<double>();

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
            static Border Frame(Grid g, Thickness lines) { g.HorizontalAlignment = HorizontalAlignment.Left; return new Border { Child = g, BorderThickness = lines, HorizontalAlignment = HorizontalAlignment.Left }; }
            headGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto)); footGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            headView = new ScrollViewer { Content = Frame(headGrid, new Thickness(1, 1, 0, 0)), HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
            bodyView = new ScrollViewer { Content = Frame(bodyGrid, new Thickness(1, 0, 0, 0)), HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Hidden };
            footView = new ScrollViewer { Content = Frame(footGrid, new Thickness(1, 0, 0, 0)), HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
            area = new Pinned(headView, bodyView, footView, across, down);
            overlay.Children.Add(line);
            addColumn.Margin = new Thickness(8, 0, 0, 0);
            var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 8, 0, 0), Children = { addRows, count, rowsLabel, addColumn } };
            DockPanel.SetDock(bar, Dock.Bottom);
            Child = new DockPanel { Children = { bar, new Panel { Children = { area, overlay } } } };
            addRows.Click += () => AddRows(int.TryParse(count.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 1, focus: true);
            count.Submit += () => addRows.PerformClick();
            addColumn.Click += () => InsertColumn(columns.Count);
            area.SizeChanged += (s, e) => { if (Math.Abs(e.NewSize.Width - e.PreviousSize.Width) > 0.5) Layout(); };
            // the heading and the footer follow the rows across; the bars follow both ways
            bodyView.ScrollChanged += (s, e) => Bars();
            headView.ScrollChanged += (s, e) => { if (!syncing && Math.Abs(headView.Offset.X - bodyView.Offset.X) > 0.5) bodyView.Offset = bodyView.Offset.WithX(headView.Offset.X); };
            across.PropertyChanged += (s, e) => { if (e.Property == RangeBase.ValueProperty && !syncing) bodyView.Offset = bodyView.Offset.WithX(across.Value); };
            down.PropertyChanged += (s, e) => { if (e.Property == RangeBase.ValueProperty && !syncing) bodyView.Offset = bodyView.Offset.WithY(down.Value); };
            KeyDown += (s, e) => { if (e.Key == Key.Escape && selected.Count > 0) { selected.Clear(); PaintAll(); SelectionChanged?.Invoke(Selected); e.Handled = true; } };
            Editor();
            bodyGrid.AddHandler(PointerPressedEvent, Pressed, RoutingStrategies.Bubble);
            // a right-click anywhere in a row, its number included, is its menu (the text box's Cut/Copy/Paste flyout would hide it)
            bodyGrid.AddHandler(PointerReleasedEvent, Released, RoutingStrategies.Bubble, handledEventsToo: true);
            bodyGrid.AddHandler(GotFocusEvent, (s, e) => Settle(), RoutingStrategies.Bubble);
            bodyGrid.AddHandler(LostFocusEvent, (s, e) => Dispatcher.UIThread.Post(Settle), RoutingStrategies.Bubble);
            Rebuild();
        }

        void Bars()
        {
            var ext = bodyView.Extent; var view = bodyView.Viewport; var at = bodyView.Offset;
            bool tall = ext.Height > view.Height + 0.5, wide = ext.Width > view.Width + 0.5, was = down.IsVisible;
            syncing = true;
            down.IsVisible = tall; across.IsVisible = wide;
            down.Maximum = Math.Max(0, ext.Height - view.Height); down.ViewportSize = view.Height; down.Value = at.Y;
            across.Maximum = Math.Max(0, ext.Width - view.Width); across.ViewportSize = view.Width; across.Value = at.X;
            headView.Offset = new Vector(at.X, 0); footView.Offset = new Vector(at.X, 0);
            syncing = false;
            if (was != tall) Layout();  // the vertical bar takes its gutter from the room
        }

        // ---- values -------------------------------------------------------------------------------------------------

        /// <summary>A typed number, exactly (decimal: money adds up to the cent); "4,90" and "4.90" both read; null when empty.</summary>
        static decimal? Number(string? text)
        {
            var t = (text ?? "").Trim().Replace(" ", "").Replace(',', '.');
            return t != "" && decimal.TryParse(t, NumberStyles.Number | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out decimal v) ? v : null;
        }

        Column? Find(string key) => columns.FirstOrDefault(c => c.Key == key);

        /// <summary>A computed column's values from the app instead of the table's own formula (money with its own rounding,
        /// a price list's rules), so what the table shows and adds up is what the app exports. Return null for an empty cell;
        /// FromTerms gives the table's own result for the columns the app leaves to it. Setting it redraws the table.</summary>
        public Func<Row, Column, decimal?>? Calculate { get => calculate; set { calculate = value; Rebuild(); } }
        Func<Row, Column, decimal?>? calculate;

        /// <summary>A cell's number, exactly: typed, or worked out for a computed column (by Calculate when set, else by its
        /// Terms); null when empty or when a formula can't be worked out (a division by zero, a column it doesn't find).</summary>
        public decimal? Exact(Row row, Column column) => Exact(row, column, 0);

        /// <summary>A cell's number as a double (Exact, rounded to a double); NaN when empty.</summary>
        public double Value(Row row, Column column) => Exact(row, column) is decimal d ? (double)d : double.NaN;

        /// <summary>A computed column's own formula, in decimals: × and ÷ before + and −, the referenced columns as Exact gives
        /// them (so through Calculate too).</summary>
        public decimal? FromTerms(Row row, Column column) => Terms(row, column, 0);

        decimal? Exact(Row row, Column column, int depth)
        {
            if (column.Kind != Kind.Computed) return Number(row.Values.GetValueOrDefault(column.Key));
            if (depth > 8) return null;  // a formula that points at itself stops here
            return calculate != null ? calculate(row, column) : Terms(row, column, depth);
        }

        decimal? Terms(Row row, Column column, int depth)
        {
            if (column.Terms.Count == 0) return null;
            decimal? Of(Term t) => Find(t.Key) is Column c ? Exact(row, c, depth + 1) : null;
            try
            {
                var sums = new List<decimal?> { Of(column.Terms[0]) }; var ops = new List<Op>();
                for (int i = 1; i < column.Terms.Count; i++)
                {
                    var t = column.Terms[i];
                    if (t.Op == Op.Multiply) sums[^1] *= Of(t);
                    else if (t.Op == Op.Divide) { var d = Of(t); sums[^1] = d is null or 0 ? null : sums[^1] / d; }
                    else { ops.Add(t.Op); sums.Add(Of(t)); }
                }
                var n = sums[0];
                for (int i = 0; i < ops.Count; i++) n = ops[i] == Op.Add ? n + sums[i + 1] : n - sums[i + 1];
                return n;
            }
            catch (OverflowException) { return null; }  // past decimal's range (about 7.9e28): shown as —
        }

        static string Format(Column c, decimal? n) => n is decimal v ? v.ToString(c.Decimals is int d ? "N" + d : "#,##0.####", CultureInfo.InvariantCulture) : "—";
        string Shown(Row r, Column c) => c.Kind == Kind.Computed ? Format(c, Exact(r, c)) : r.Values.GetValueOrDefault(c.Key) ?? "";
        static string UnitOf(Row r, Column c) => r.Units.GetValueOrDefault(c.Key) is string u && c.Units.Contains(u) ? u : c.Units.FirstOrDefault() ?? "";
        static bool Numeric(Column c) => c.Kind != Kind.Text;
        /// <summary>The formula as words: "Total − Discount + Shipping".</summary>
        public string Formula(Column c) => string.Join("", c.Terms.Select((t, i) => (i > 0 ? " " + OpText[(int)t.Op] + " " : "") + (Find(t.Key)?.Title ?? "?")));

        /// <summary>The footer's value, exactly (null for None, or with no numbers); Count counts the filled cells.</summary>
        public decimal? FooterValue(Column c)
        {
            if (c.Footer == Total.None) return null;
            if (c.Footer == Total.Count) return rows.Count(r => c.Kind == Kind.Computed ? Exact(r, c) != null : (r.Values.GetValueOrDefault(c.Key) ?? "").Trim() != "");
            var ns = rows.Select(r => Exact(r, c)).OfType<decimal>().ToList();
            if (ns.Count == 0) return null;
            try { return c.Footer switch { Total.Sum => ns.Sum(), Total.Average => ns.Sum() / ns.Count, Total.Min => ns.Min(), _ => ns.Max() }; }
            catch (OverflowException) { return null; }
        }

        /// <summary>The footer's value as shown ("" for None, "—" with no numbers).</summary>
        public string Footer(Column c) =>
            c.Footer == Total.None ? "" : c.Footer == Total.Count ? FooterValue(c)!.Value.ToString(CultureInfo.InvariantCulture) : Format(c, FooterValue(c));

        IEnumerable<Row> Order()
        {
            if (sortKey == null || sortDir == 0 || Find(sortKey) is not Column c) return rows;
            if (Numeric(c))
            {
                var with = rows.Where(r => Exact(r, c) != null); var without = rows.Where(r => Exact(r, c) == null);  // empty cells last, either way
                return (sortDir > 0 ? with.OrderBy(r => Exact(r, c)) : with.OrderByDescending(r => Exact(r, c))).Concat(without);
            }
            return sortDir > 0 ? rows.OrderBy(r => Shown(r, c), StringComparer.CurrentCultureIgnoreCase) : rows.OrderByDescending(r => Shown(r, c), StringComparer.CurrentCultureIgnoreCase);
        }

        bool Editable(Column c) => !readOnly && c.Kind != Kind.Computed;

        // ---- widths ---------------------------------------------------------------------------------------------------

        static double TextWidth(string s, double size, int weight = 400) => Math.Ceiling(Look.Measure(s ?? "", Ui(size, weight)));
        static double Longest(string s, double size, int weight) => s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(w => TextWidth(w, size, weight)).DefaultIfEmpty(0).Max();

        /// <summary>Room at a cell's right for its unit or suffix, so a value never runs under it.</summary>
        double Room(Column c) => c.Kind == Kind.Unit && c.Units.Count > 0 ? c.Units.Select(u => TextWidth(u, TextSize.Label, 500)).Max() + 20 : c.Suffix != null ? TextWidth(c.Suffix, TextSize.Caption) + 14 : 8;

        double Full(Line l, int i)
        {
            if (double.IsNaN(l.Full[i])) l.Full[i] = TextWidth(Shown(l.Row, columns[i]), valueSize, columns[i].Bold ? 600 : 400);
            return l.Full[i];
        }

        double Word(Line l, int i)
        {
            if (double.IsNaN(l.Word[i])) l.Word[i] = columns[i].Kind == Kind.Text ? Longest(Shown(l.Row, columns[i]), valueSize, columns[i].Bold ? 600 : 400) : Full(l, i);
            return l.Word[i];
        }

        /// <summary>A column's width from its whole content: the heading, every value with its unit or suffix, the footer.</summary>
        double Natural(int i)
        {
            var c = columns[i];
            double w = Math.Max(TextWidth(c.Title, headingSize, 600), TextWidth(c.Subtitle, subheadingSize)) + 20 + (sortKey == c.Key ? 22 : 0);
            foreach (var l in shown) w = Math.Max(w, Full(l, i) + 12 + rooms[i]);
            if (c.Footer != Total.None) w = Math.Max(w, TextWidth(Footer(c), valueSize, 600) + TextWidth(c.Footer.ToString(), TextSize.Caption, 500) + 26);
            return Math.Ceiling(w);
        }

        /// <summary>A fill column's least width: its longest word (its text wraps), the heading's included.</summary>
        double Least(int i)
        {
            double w = Longest(columns[i].Title, headingSize, 600) + 20;
            foreach (var l in shown) w = Math.Max(w, Word(l, i) + 12 + rooms[i]);
            return Math.Ceiling(Math.Max(60, w));
        }

        double Lo(int i) => Math.Max(40, columns[i].MinWidth ?? (columns[i].Sizing == Sizing.Fill ? Least(i) : 0));
        double Clamp(int i, double w, double lo) => Math.Max(lo, Math.Min(w, columns[i].MaxWidth ?? double.PositiveInfinity));

        /// <summary>Fit and Fixed columns take their width; the Fill columns share what is left of the box, equally, each held to
        /// its minimum and maximum. Past every minimum, the rows scroll across.</summary>
        void Layout()
        {
            int n = columns.Count;
            var w = new double[n]; var lo = new double[n]; var open = new List<int>();
            for (int i = 0; i < n; i++)
            {
                var c = columns[i]; lo[i] = Lo(i);
                if (c.Sizing == Sizing.Fill) open.Add(i);
                else w[i] = Clamp(i, c.Sizing == Sizing.Fixed && c.Width is double fixedW ? fixedW : Natural(i), lo[i]);
                if (editing != null && c.Sizing == Sizing.Fit && i < widths.Length) w[i] = Math.Max(w[i], widths[i]);  // while typing, a column only grows: nothing jumps back and forth
            }
            double room = (area.Bounds.Width > 0 ? area.Bounds.Width : Bounds.Width) - (down.IsVisible ? Bar : 0) - 1 - Gutter - Actions - w.Sum();
            while (open.Count > 0)
            {
                double share = Math.Max(0, room) / open.Count;
                var held = open.Where(i => lo[i] > share).ToList();  // the ones that need more than a share first, then the ones capped below it
                if (held.Count == 0) held = open.Where(i => (columns[i].MaxWidth ?? double.PositiveInfinity) < share).ToList();
                if (held.Count == 0) { foreach (var i in open) w[i] = share; break; }
                foreach (var i in held) { w[i] = Clamp(i, share, lo[i]); room -= w[i]; open.Remove(i); }
            }
            widths = w.Select(Math.Floor).ToArray();
            foreach (var g in Grids())
                for (int i = 0; i < n && i + 1 < g.ColumnDefinitions.Count; i++) g.ColumnDefinitions[i + 1].Width = new GridLength(widths[i]);
        }

        // ---- drawing --------------------------------------------------------------------------------------------------

        /// <summary>The heading, the footer and every row's grid: the columns are set on each.</summary>
        IEnumerable<Grid> Grids() => shown.Select(l => l.Panel).Prepend(headGrid).Append(footGrid);

        void Define(Grid g)
        {
            g.ColumnDefinitions.Clear();
            g.ColumnDefinitions.Add(new ColumnDefinition(Gutter, GridUnitType.Pixel));
            for (int i = 0; i < columns.Count; i++) g.ColumnDefinitions.Add(new ColumnDefinition(i < widths.Length ? widths[i] : 80, GridUnitType.Pixel));
            g.ColumnDefinitions.Add(new ColumnDefinition(Actions, GridUnitType.Pixel));
        }

        /// <summary>The editor or the outline goes into a row's grid (null: out of the table).</summary>
        static void Move(Control c, Grid? to)
        {
            if (c.Parent == to) return;
            (c.Parent as Panel)?.Children.Remove(c);
            to?.Children.Add(c);
        }

        Border Cell(Grid g, Control? child, int col, int row, IBrush background)
        {
            var cell = new Border { Child = child, BorderBrush = B(Look.Separator), BorderThickness = new Thickness(0, 0, 1, 1), Background = background, MinHeight = 28 };
            Grid.SetColumn(cell, col); Grid.SetRow(cell, row); g.Children.Add(cell);
            return cell;
        }

        /// <summary>Draws the table again from Columns and Rows (an app calls it after changing a column's properties).</summary>
        public void Refresh() => Rebuild();

        void Rebuild()
        {
            // the cell being edited keeps the keyboard and its caret
            var keep = editing != null && editor.IsFocused ? (editing.Row, columns.ElementAtOrDefault(editCol)?.Key, editor.CaretIndex) : default;
            Park();
            foreach (var g in new[] { headGrid, footGrid }) { g.Children.Clear(); Define(g); }
            foreach (var g in new[] { headGrid, bodyGrid, footGrid }) ((Border)g.Parent!).BorderBrush = B(Look.Separator);
            bodyGrid.Children.Clear(); bodyGrid.RowDefinitions.Clear(); lines.Clear(); shown.Clear();
            line.Background = B(Accent); ring.BorderBrush = B(Accent);
            addRows.IsVisible = count.IsVisible = rowsLabel.IsVisible = !readOnly;
            addColumn.IsVisible = design && !readOnly;
            rooms = columns.Select(Room).ToArray();
            Sync();
            Heads(); Foot(); Layout();
            if (keep.Row != null && lines.TryGetValue(keep.Row, out var l) && columns.ToList().FindIndex(c => c.Key == keep.Key) is int i and >= 0 && Editable(columns[i]))
            { Edit(l, i); editor.SelectionStart = editor.SelectionEnd = editor.CaretIndex = Math.Min(keep.CaretIndex, (editor.Text ?? "").Length); }
        }

        /// <summary>The rows' lines follow Rows: new rows drawn, gone ones taken out, the rest kept (focus and caret with them).</summary>
        void Sync()
        {
            var now = new HashSet<Row>(rows);
            foreach (var l in lines.Values.Where(l => !now.Contains(l.Row)).ToList())
            {
                if (editing == l) Park();
                bodyGrid.Children.Remove(l.Panel);
                lines.Remove(l.Row); selected.Remove(l.Row);
            }
            foreach (var r in rows) if (!lines.ContainsKey(r)) lines[r] = Make(r);
            while (bodyGrid.RowDefinitions.Count < rows.Count) bodyGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            while (bodyGrid.RowDefinitions.Count > rows.Count) bodyGrid.RowDefinitions.RemoveAt(bodyGrid.RowDefinitions.Count - 1);
            Arrange();
        }

        /// <summary>The lines in the order shown: each part moves to its grid row and the numbers follow (nothing is drawn again).</summary>
        void Arrange()
        {
            shown = Order().Select(r => lines[r]).ToList();
            for (int i = 0; i < shown.Count; i++)
            {
                var l = shown[i];
                Grid.SetRow(l.Panel, i);
                l.Number.Text = (i + 1).ToString(CultureInfo.InvariantCulture);
                Avalonia.Automation.AutomationProperties.SetName(l.Gutter, "Select row " + (i + 1));
            }
            Settle();
        }

        void Heads()
        {
            headGrid.Children.Clear(); heads.Clear();
            var pickAll = Build.Text("#", Text3, TextSize.Caption); pickAll.HorizontalAlignment = HorizontalAlignment.Center;
            var corner = Cell(headGrid, pickAll, 0, 0, B(Raised)); corner.Cursor = Build.Hand; ToolTip.SetTip(corner, "Select all or none");
            corner.PointerReleased += (s, e) =>
            {
                if (e.InitialPressMouseButton != MouseButton.Left) return;
                bool all = rows.Count > 0 && rows.All(selected.Contains);
                selected.Clear(); if (!all) foreach (var r in rows) selected.Add(r);
                PaintAll(); SelectionChanged?.Invoke(Selected);
            };
            for (int i = 0; i < columns.Count; i++) Heading(columns[i], i);
            Cell(headGrid, null, columns.Count + 1, 0, B(Raised));
        }

        void Foot()
        {
            footGrid.Children.Clear(); totals.Clear();
            Cell(footGrid, null, 0, 0, B(Fill));
            for (int i = 0; i < columns.Count; i++)
            {
                var c = columns[i];
                // the function and its value on one line: two runs of one text share its baseline, whatever their sizes
                var label = new Avalonia.Controls.Documents.Run((c.Footer == Total.None ? "Calculate" : c.Footer.ToString()) + " ") { FontSize = TextSize.Caption, FontWeight = Weight(500), Foreground = c.Footer == Total.None ? Brushes.Transparent : B(Text3) };
                var value = new Avalonia.Controls.Documents.Run(Footer(c)) { FontSize = valueSize, FontWeight = Weight(600), Foreground = B(Look.Text) }; totals[c.Key] = value;
                var text = Build.Text("", Look.Text, valueSize); text.Text = null; text.Inlines = new Avalonia.Controls.Documents.InlineCollection { label, value };
                text.TextAlignment = Numeric(c) ? TextAlignment.Right : TextAlignment.Left; text.HorizontalAlignment = HorizontalAlignment.Stretch;
                var cell = Cell(footGrid, new Border { Padding = new Thickness(8, 4), Child = text, Background = Brushes.Transparent }, i + 1, 0, B(Fill));
                cell.Cursor = Build.Hand; ToolTip.SetTip(cell, "Choose what the footer shows");
                cell.PointerEntered += (s, e) => { if (c.Footer == Total.None) label.Foreground = B(Text3); };
                cell.PointerExited += (s, e) => { if (c.Footer == Total.None) label.Foreground = Brushes.Transparent; };
                cell.PointerReleased += (s, e) => { if (e.InitialPressMouseButton == MouseButton.Left) FooterMenu(c, cell); };
            }
            Cell(footGrid, null, columns.Count + 1, 0, B(Fill));
        }

        void Heading(Column c, int i)
        {
            bool sorted = sortKey == c.Key && sortDir != 0;
            var title = Build.Text(c.Title, Look.Text, headingSize, 600); title.TextWrapping = TextWrapping.Wrap; title.TextTrimming = TextTrimming.None;
            var words = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Children = { title } };
            if (c.Subtitle != "") { var sub = Build.Text(c.Subtitle, Text2, subheadingSize); sub.TextWrapping = TextWrapping.Wrap; sub.TextTrimming = TextTrimming.None; words.Children.Add(sub); }
            if (Numeric(c)) { words.HorizontalAlignment = HorizontalAlignment.Right; foreach (var t in words.Children.OfType<TextBlock>()) t.TextAlignment = TextAlignment.Right; }
            var row = new DockPanel { LastChildFill = true };
            if (sorted) { var arrow = Build.Icon24(sortDir > 0 ? Icons.ChevronUp : Icons.ChevronDown, 14, 2); arrow.Stroke = B(Text2); arrow.VerticalAlignment = VerticalAlignment.Bottom; arrow.Margin = new Thickness(4, 0, 0, 2); DockPanel.SetDock(arrow, Dock.Right); row.Children.Add(arrow); }
            row.Children.Add(words);
            // designing, the drag grip shows on hover over the heading's left padding: it takes no width, so the heading reads as it will
            var drag = Build.Icon24(Icons.GripVertical, 12, 2); drag.Stroke = B(Text3); drag.HorizontalAlignment = HorizontalAlignment.Left; drag.VerticalAlignment = VerticalAlignment.Center; drag.IsVisible = false; drag.IsHitTestVisible = false;
            var grip = new Border { Width = 7, Background = Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, -4, 0), Cursor = new Cursor(StandardCursorType.SizeWestEast) };
            var edge = new Border { Width = 2, HorizontalAlignment = HorizontalAlignment.Right, Background = B(Accent), IsVisible = false, Margin = new Thickness(0, 0, -1, 0), IsHitTestVisible = false };
            ToolTip.SetTip(grip, c.Sizing == Sizing.Fill ? "Drag to set the least width; double-click to clear it" : "Drag to resize; double-click to fit");
            var inner = new Panel { Children = { new Border { Padding = new Thickness(8, 4), Child = row }, drag, edge, grip } };
            var head = Cell(headGrid, inner, i + 1, 0, B(Raised));
            head.Focusable = design; heads.Add(head);
            if (c.Kind == Kind.Computed) ToolTip.SetTip(head, "= " + Formula(c));
            Avalonia.Automation.AutomationProperties.SetName(head, "Column · " + c.Title);
            grip.PointerEntered += (s, e) => edge.IsVisible = true;
            grip.PointerExited += (s, e) => edge.IsVisible = false;
            head.PointerEntered += (s, e) => drag.IsVisible = design;
            head.PointerExited += (s, e) => drag.IsVisible = false;
            Resize(grip, c, edge);
            Wire(head, c, i);
        }

        /// <summary>A row's parts: its backdrop (the selection tint), the number, a text per cell, the unit or suffix, ⋯.</summary>
        Line Make(Row row)
        {
            var l = new Line(row, columns.Count);
            Define(l.Panel); l.Panel.Tag = l;
            l.Number = Build.Text("", Text3, TextSize.Caption); l.Number.HorizontalAlignment = HorizontalAlignment.Center; l.Number.VerticalAlignment = VerticalAlignment.Center;
            l.Marker = new Border { Width = 3, HorizontalAlignment = HorizontalAlignment.Left, Background = B(Accent) };
            l.Gutter = new Border { Child = new Panel { Children = { l.Marker, l.Number } }, BorderBrush = B(Look.Separator), BorderThickness = new Thickness(0, 0, 1, 1), MinHeight = 28, Cursor = Build.Hand };
            l.Panel.Children.Add(l.Gutter);
            for (int i = 0; i < columns.Count; i++)
            {
                var c = columns[i];
                var t = Build.Text(Shown(row, c), c.Kind == Kind.Computed ? Text2 : Look.Text, valueSize, c.Bold ? 600 : 400);
                t.TextWrapping = TextWrapping.Wrap; t.TextTrimming = TextTrimming.None; t.Padding = new Thickness(8, 4, rooms[i], 4);
                t.TextAlignment = Numeric(c) ? TextAlignment.Right : TextAlignment.Left; t.VerticalAlignment = VerticalAlignment.Top;
                l.Texts[i] = t;
                Control face = t;
                if (c.Kind == Kind.Unit && c.Units.Count > 0 && !readOnly)
                {
                    int at = i;
                    var unit = new ActionButton(UnitOf(row, c), ButtonKind.Ghost) { MinWidth = 0, Height = 22, Padding = new Thickness(6, 0), KeepPointerFocus = true, Focusable = false, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 1, 2, 0) };  // centred on the value's first line
                    ToolTip.SetTip(unit, "Click to change the unit (Alt+Down while editing)"); Avalonia.Automation.AutomationProperties.SetName(unit, "Unit");
                    unit.Click += () => Cycle(l, at);
                    l.Units[i] = unit;
                    face = new Panel { Children = { t, unit } };
                }
                else if (c.Suffix != null || c.Kind == Kind.Unit && c.Units.Count > 0)
                {
                    var suffix = Build.Text(c.Kind == Kind.Unit ? UnitOf(row, c) : c.Suffix!, Text3, TextSize.Caption); suffix.HorizontalAlignment = HorizontalAlignment.Right; suffix.VerticalAlignment = VerticalAlignment.Top; suffix.Margin = new Thickness(0, 6, 8, 0); suffix.IsHitTestVisible = false;
                    face = new Panel { Children = { t, suffix } };
                }
                var cell = new Border { Child = face, BorderBrush = B(Look.Separator), BorderThickness = new Thickness(0, 0, 1, 1), Background = Brushes.Transparent, MinHeight = 28 };
                if (Editable(c)) cell.Cursor = ibeam; else cell.Focusable = true;  // read only, still focusable: the column's controls follow
                Grid.SetColumn(cell, i + 1);
                l.Cells[i] = cell; l.Panel.Children.Add(cell);
            }
            var more = new ActionButton("", ButtonKind.Ghost, Icons.Ellipsis) { MinWidth = 0, Width = 24, Height = 24, KeepPointerFocus = true, Focusable = false, HorizontalAlignment = HorizontalAlignment.Center, IsVisible = !readOnly };
            ToolTip.SetTip(more, "Row actions"); Avalonia.Automation.AutomationProperties.SetName(more, "Row actions");
            more.Click += () => RowMenu(l, more, false);
            var end = new Border { Child = more, BorderBrush = B(Look.Separator), BorderThickness = new Thickness(0, 0, 1, 1), Background = Brushes.Transparent, MinHeight = 28 };
            Grid.SetColumn(end, columns.Count + 1); l.Panel.Children.Add(end);
            bodyGrid.Children.Add(l.Panel);
            Paint(l);
            return l;
        }

        void Paint(Line l)
        {
            bool on = selected.Contains(l.Row);
            l.Panel.Background = B(on ? AccentTint : Look.Background);
            l.Gutter.Background = B(on ? AccentTint : Raised);
            l.Number.Foreground = B(on ? Look.Text : Text3); l.Number.FontWeight = Weight(on ? 600 : 400);
            l.Marker.IsVisible = on;
        }

        void PaintAll() { foreach (var l in lines.Values) Paint(l); }

        /// <summary>The line and the column (-1 the number, Columns.Count the ⋯) of a pointer event in the rows.</summary>
        (Line? line, int col) Under(RoutedEventArgs e)
        {
            var v = e.Source as Visual;
            while (v != null && v.GetVisualParent() is not Grid { Tag: Line }) v = v.GetVisualParent();
            if (v is not Control part || part == ring || part.Parent is not Grid { Tag: Line l }) return (null, 0);
            return (l, Grid.GetColumn(part) - 1);
        }

        void Pressed(object? s, PointerPressedEventArgs e)
        {
            var (l, col) = Under(e);
            if (l == null || !e.GetCurrentPoint(bodyGrid).Properties.IsLeftButtonPressed) return;
            e.Handled = true;  // a row number's press keeps the keyboard where it is
            if (col < 0 || col >= columns.Count) return;
            if (Editable(columns[col])) Edit(l, col, e.GetPosition(l.Texts[col]));
            else l.Cells[col].Focus(NavigationMethod.Pointer);
        }

        void Released(object? s, PointerReleasedEventArgs e)
        {
            var (l, col) = Under(e);
            if (l == null) return;
            if (e.InitialPressMouseButton == MouseButton.Right && !readOnly) { e.Handled = true; RowMenu(l, col < 0 ? l.Gutter : col < columns.Count ? l.Cells[col] : l.Gutter, true); }
            else if (e.InitialPressMouseButton == MouseButton.Left && col < 0 && !e.Handled) Pick(l.Row, e.KeyModifiers);
        }

        // ---- the editor -----------------------------------------------------------------------------------------------

        void Editor()
        {
            editor.TextWrapping = TextWrapping.Wrap; editor.AcceptsReturn = false; editor.VerticalAlignment = VerticalAlignment.Stretch;
            editor.VerticalContentAlignment = VerticalAlignment.Top; editor.ZIndex = 1;
            editor.ContextFlyout = null; editor.ContextMenu = null;
            // written as it changes (not on TextChanged, which comes later): a key and a Tab straight after it can't lose the key
            editor.PropertyChanged += (s, e) =>
            {
                if (e.Property != TextBox.TextProperty || editing == null) return;
                var c = columns[editCol]; var t = editor.Text ?? "";
                if ((editing.Row.Values.GetValueOrDefault(c.Key) ?? "") == t) return;
                editing.Row.Values[c.Key] = t; editing.Texts[editCol].Text = t; Recount(editing); Changed?.Invoke();
            };
            editor.AddHandler(KeyDownEvent, (s, e) =>
            {
                if (editing == null) return;
                bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
                if (e.Key == Key.Enter)
                {
                    int at = shown.IndexOf(editing) + (shift ? -1 : 1);  // no new lines: Enter goes down the column, Shift+Enter up
                    if (at >= 0 && at < shown.Count) Edit(shown[at], editCol);
                }
                else if (e.Key == Key.Tab && Next(shift) is var (l, i) && l != null) Edit(l, i);
                else if (e.Key == Key.Down && e.KeyModifiers == KeyModifiers.Alt && editing.Units[editCol] != null) Cycle(editing, editCol);
                else if (e.Key == Key.Apps || e.Key == Key.F10 && shift) RowMenu(editing, editing.Cells[editCol], false);
                else return;
                e.Handled = true;
            }, RoutingStrategies.Tunnel);
        }

        /// <summary>The editable cell after (or before) the one being edited, across then down; none at either end (Tab leaves).</summary>
        (Line? line, int col) Next(bool back)
        {
            int n = columns.Count, at = shown.IndexOf(editing!) * n + editCol;
            for (int k = at + (back ? -1 : 1); k >= 0 && k < shown.Count * n; k += back ? -1 : 1)
                if (Editable(columns[k % n])) return (shown[k / n], k % n);
            return (null, 0);
        }

        /// <summary>The editor moves to a cell: at a point, the caret goes where it was clicked; from the keyboard, the value is selected.</summary>
        void Edit(Line l, int col, Point? at = null)
        {
            var c = columns[col];
            if (editing != null) editing.Texts[editCol].Opacity = 1;
            editing = l; editCol = col;
            var t = l.Texts[col];
            editor.Text = l.Row.Values.GetValueOrDefault(c.Key) ?? "";
            editor.FontSize = valueSize; editor.FontWeight = c.Bold ? FontWeight.SemiBold : FontWeight.Normal; editor.TextAlignment = t.TextAlignment;
            // the editor's text sits exactly on the cell's: same padding, clear of the grid lines and of the unit (still clickable)
            bool unit = l.Units[col] != null;
            editor.Margin = new Thickness(0, 0, 1 + (unit ? rooms[col] : 0), 1); editor.Padding = new Thickness(8, 4, unit ? 0 : rooms[col], 4);
            Avalonia.Automation.AutomationProperties.SetName(editor, c.Title);
            Grid.SetColumn(editor, col + 1); Move(editor, l.Panel);
            t.Opacity = 0;
            editor.Focus(at == null ? NavigationMethod.Tab : NavigationMethod.Pointer);
            int len = (editor.Text ?? "").Length;
            if (at is Point p && t.TextLayout is { } layout)
            {
                int caret = Math.Clamp(layout.HitTestPoint(new Point(p.X - t.Padding.Left, p.Y - t.Padding.Top)).TextPosition, 0, len);
                editor.SelectionStart = editor.SelectionEnd = editor.CaretIndex = caret;
            }
            else { editor.SelectionStart = 0; editor.SelectionEnd = editor.CaretIndex = len; }
            Settle();
            Current(c.Key);
        }

        /// <summary>The editor leaves its cell (the cell's text shows again).</summary>
        void Park()
        {
            if (editing == null) return;
            editing.Texts[editCol].Opacity = 1;
            editing = null;
            Move(editor, null);
            Layout();  // the widths held while typing settle
        }

        /// <summary>After focus moves: the outline goes to the focused cell or hides; an editor that lost the keyboard leaves.</summary>
        void Settle()
        {
            var f = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
            if (editing != null && f != editor) Park();
            var cell = f == editor ? editor : f?.Parent is Grid { Tag: Line } ? f : null;
            ring.IsVisible = cell != null;
            if (cell == null) return;
            Grid.SetColumn(ring, Grid.GetColumn(cell)); Move(ring, (Grid)cell.Parent!);
            if (Grid.GetColumn(cell) - 1 is int i && i >= 0 && i < columns.Count) Current(columns[i].Key);
        }

        void Cycle(Line l, int col)
        {
            var c = columns[col]; var units = c.Units.ToList(); var next = units[(units.IndexOf(UnitOf(l.Row, c)) + 1) % units.Count];
            l.Row.Units[c.Key] = next; l.Units[col]!.Text = next; Recount(l); Changed?.Invoke();
        }

        void FocusCell(Row row, string key)
        {
            int i = columns.ToList().FindIndex(c => c.Key == key);
            if (i < 0 || !lines.TryGetValue(row, out var l)) return;
            if (Editable(columns[i])) Edit(l, i); else l.Cells[i].Focus(NavigationMethod.Tab);
        }

        void Current(string key)
        {
            if (current == key) return;
            current = key;
            for (int i = 0; i < heads.Count && i < columns.Count; i++) HeadBack(i);
            CurrentColumnChanged?.Invoke(CurrentColumn);
        }

        /// <summary>After an edit: the row's computed cells, the footers and the widths, without drawing the table again (the caret stays).</summary>
        void Recount(Line l)
        {
            l.Forget();
            for (int i = 0; i < columns.Count; i++) if (columns[i].Kind == Kind.Computed) l.Texts[i].Text = Shown(l.Row, columns[i]);
            Totals(); Layout();
        }

        void Totals() { foreach (var c in columns) if (totals.TryGetValue(c.Key, out var f)) f.Text = Footer(c); }

        /// <summary>Rows were added or taken out: their lines follow, the footers and widths too.</summary>
        void Reflow() { Sync(); Totals(); Layout(); }

        // ---- rows -----------------------------------------------------------------------------------------------------

        Row Blank() { var r = new Row(); foreach (var c in columns.Where(c => c.Kind == Kind.Unit && c.Units.Count > 0)) r.Units[c.Key] = c.Units[0]; return r; }

        /// <summary>The first cell of a row a command just made takes the keyboard.</summary>
        void Start(Row r) { if (columns.Count > 0) Dispatcher.UIThread.Post(() => FocusCell(r, (columns.FirstOrDefault(Editable) ?? columns[0]).Key), DispatcherPriority.Loaded); }

        /// <summary>Adds n empty rows (1–100) at the end; focus: the first new row's first cell takes the keyboard.</summary>
        public void AddRows(int n, bool focus = false)
        {
            n = Math.Clamp(n, 1, 100); count.Text = n.ToString(CultureInfo.InvariantCulture);
            Row? first = null;
            for (int i = 0; i < n; i++) { var r = Blank(); rows.Add(r); first ??= r; }
            bool wasSorted = sortKey != null;
            sortKey = null;  // new rows show at the end, where they were added
            if (wasSorted) Heads();
            Reflow(); Changed?.Invoke();
            if (focus && first != null) Start(first);
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
            PaintAll(); SelectionChanged?.Invoke(Selected);
        }

        void RowMenu(Line l, Control anchorControl, bool atPointer)
        {
            var row = l.Row;
            if (!selected.Contains(row)) { selected.Clear(); selected.Add(row); anchor = row; PaintAll(); SelectionChanged?.Invoke(Selected); }
            var targets = Selected; int n = targets.Count; string many = n > 1 ? " " + n + " rows" : "";
            var items = new[] { "Duplicate" + many, "Insert row above", "Insert row below", Shade.Menu.Divider, "Delete" + (n > 1 ? many : " row") };
            Shade.Menu.Show(anchorControl, items, async i =>
            {
                int last = rows.IndexOf(targets[^1]);
                if (i == 0)
                {
                    var copies = targets.Select(t => { var r = new Row(); foreach (var kv in t.Values) r.Values[kv.Key] = kv.Value; foreach (var kv in t.Units) r.Units[kv.Key] = kv.Value; return r; }).ToList();
                    for (int k = 0; k < copies.Count; k++) rows.Insert(last + 1 + k, copies[k]);
                    selected.Clear(); foreach (var r in copies) selected.Add(r);
                    Reflow(); PaintAll(); Changed?.Invoke(); SelectionChanged?.Invoke(Selected);
                }
                else if (i == 1 || i == 2)
                {
                    var b = Blank(); rows.Insert(i == 1 ? rows.IndexOf(targets[0]) : last + 1, b);
                    Reflow(); Changed?.Invoke(); Start(b);
                }
                else if (i == 4)
                {
                    bool yes = TopLevel.GetTopLevel(this) is Window w ? await Ask.ConfirmDelete(w, n > 1 ? "Delete " + n + " rows?" : "Delete this row?", n > 1 ? "Delete " + n + " rows" : "Delete row") : true;
                    if (!yes) return;
                    foreach (var t in targets) rows.Remove(t);
                    selected.Clear(); Reflow(); Changed?.Invoke(); SelectionChanged?.Invoke(Selected);
                }
            }, atPointer: atPointer);
        }

        void FooterMenu(Column c, Control cell)
        {
            var choices = Numeric(c) ? Enum.GetValues<Total>() : new[] { Total.None, Total.Count };
            Shade.Menu.Show(cell, choices.Select(t => t.ToString()).ToList(), i => { c.Footer = choices[i]; Foot(); Layout(); Changed?.Invoke(); }, Array.IndexOf(choices, c.Footer));
        }

        // ---- headings: resize, sort, design ----------------------------------------------------------------------------

        /// <summary>A heading's edge: a drag sets a Fill column's minimum, any other column's fixed width; a double-click fits it again.</summary>
        void Resize(Border grip, Column c, Border edge)
        {
            double start = 0, from = 0; bool dragging = false;
            grip.PointerPressed += (s, e) =>
            {
                if (!e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed) return;
                e.Handled = true;
                if (e.ClickCount == 2) { if (c.Sizing == Sizing.Fill) c.MinWidth = null; else c.Width = null; Layout(); Changed?.Invoke(); return; }
                dragging = true; start = e.GetPosition(this).X; from = widths[columns.IndexOf(c)]; edge.IsVisible = true; e.Pointer.Capture(grip);
            };
            grip.PointerMoved += (s, e) =>
            {
                if (!dragging) return;
                double w = Math.Max(40, Math.Round(from + e.GetPosition(this).X - start));
                if (c.Sizing == Sizing.Fill) c.MinWidth = w; else c.Width = w;
                Layout();
            };
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
                if (moving) ShowDrop(GapAt(e), at, c.Title, e.GetPosition(overlay));
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
                Heads(); Arrange(); Layout();  // the rows move to their places; nothing else is drawn again
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
            head.PointerEntered += (s, e) => HeadBack(at);
            head.PointerExited += (s, e) => HeadBack(at);
            HeadBack(at);
        }

        /// <summary>A heading's fill: the current column (the focused cell's) is tinted with the accent; a hover lifts it.</summary>
        void HeadBack(int i)
        {
            bool hot = heads[i].IsPointerOver && (design || sorting);
            heads[i].Background = B(columns[i].Key == current ? hot ? AccentTintHover : AccentTint : hot ? Fill : Raised);
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
            Dispatcher.UIThread.Post(() => { int i = columns.IndexOf(c); if (i >= 0 && i < heads.Count) heads[i].Focus(NavigationMethod.Tab); }, DispatcherPriority.Loaded);
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
            Canvas.SetLeft(line, at.X - 1); Canvas.SetTop(line, at.Y); line.Height = area.Bounds.Height - (across.IsVisible ? Bar : 0);
        }

        void EndDrag()
        {
            if (ghost != null) { overlay.Children.Remove(ghost); ghost = null; }
            line.IsVisible = false;
            foreach (var g in Grids()) foreach (var b in g.Children.OfType<Control>()) b.Opacity = 1;
            if (editing != null) editing.Texts[editCol].Opacity = 0;
        }

        internal bool LineShown => line.IsVisible;

        /// <summary>The selection is painted with the mode's roles (AccentTint, Raised): painted again on a switch.</summary>
        public void Restyle() => PaintAll();
    }
}

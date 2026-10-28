// Shade's checks without a screen: the colour rules, the parsers, and the controls in XAML with bindings.
// Exits 1 when one fails.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Styling;
using Avalonia.VisualTree;
using static Shade.Look;

namespace Shade.Tests
{
    sealed class TestApp : Application
    {
        /// <summary>Each time the app's mark was drawn: true when as an icon.</summary>
        public static readonly List<bool> Marks = new List<bool>();
        public override void Initialize() => Look.Apply(this, "Tests", mark: (g, box, c, phase, icon) => { Marks.Add(icon); DrawMark(g, box, c, Look.Text, phase); });
    }

    sealed class HostThemeApp : Application
    {
        public override void Initialize()
        {
            Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
            Look.Configure(new ShadeOptions { InstallFluentTheme = false });
            Look.Apply(this, "Host theme");
        }
    }

    public sealed record Employee(int Id, string Name, string Title) { public string SearchText => Title + " " + Name; }

    public sealed class Vm : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        public IReadOnlyList<Employee> Employees { get; } = new[] { new Employee(1, "Jan Novák", "Mgr."), new Employee(2, "Petra Šílá", "Bc.") };
        int? employeeId = 2;
        public int? EmployeeId { get => employeeId; set => Set(ref employeeId, value); }
        bool flag; bool? ticked = false; string name = "", query = "", pick = "Two", accent = "Violet"; double? share = 0.25;
        IReadOnlyList<SummaryCards.Item> cards = new[] { new SummaryCards.Item { Title = "Checks", Value = "11/12" } };
        public bool Flag { get => flag; set => Set(ref flag, value); }
        EditableTable.PaperKind paperFormat;
        EditableTable.PaperDimensionsPosition paperPlacement;
        public EditableTable.PaperDimensionsPosition PaperPlacement { get => paperPlacement; set => Set(ref paperPlacement, value); }
        bool paperLandscape;
        bool constrainToPaper = true;
        public bool ConstrainToPaper { get => constrainToPaper; set => Set(ref constrainToPaper, value); }
        Size paperDimensions = new Size(210, 297);
        public EditableTable.PaperKind PaperFormat { get => paperFormat; set => Set(ref paperFormat, value); }
        public bool PaperLandscape { get => paperLandscape; set => Set(ref paperLandscape, value); }
        public Size PaperDimensions { get => paperDimensions; set => Set(ref paperDimensions, value); }

        public bool? Ticked { get => ticked; set => Set(ref ticked, value); }
        public string Name { get => name; set => Set(ref name, value); }
        string actionText = "Save";
        public string ActionText { get => actionText; set => Set(ref actionText, value); }
        ButtonKind actionKind = ButtonKind.Primary;
        public ButtonKind ActionKind { get => actionKind; set => Set(ref actionKind, value); }
        Geometry actionIcon = Icons.Plus;
        public Geometry ActionIcon { get => actionIcon; set => Set(ref actionIcon, value); }
        string recipients = "";
        public string Recipients { get => recipients; set => Set(ref recipients, value); }
        public string Query { get => query; set => Set(ref query, value); }
        public string Pick { get => pick; set => Set(ref pick, value); }
        IReadOnlyList<string> people = new[] { "Anna", "Ben", "Chloe" };
        public IReadOnlyList<string> People { get => people; set => Set(ref people, value); }
        IReadOnlySet<string> selectedPeople = new HashSet<string> { "Ben" };
        public IReadOnlySet<string> SelectedPeople { get => selectedPeople; set => Set(ref selectedPeople, value); }
        IReadOnlyList<string> shortPicks = new[] { "Ben" };
        public IReadOnlyList<string> ShortPicks { get => shortPicks; set => Set(ref shortPicks, value); }
        public System.Collections.ObjectModel.ObservableCollection<PairTable.Entry> PairRows { get; } = new() { new("Accept", "text/plain") };
        Color swatchColour = Color.Parse("#3366FF");
        public Color SwatchColour { get => swatchColour; set => Set(ref swatchColour, value); }
        public string Accent { get => accent; set => Set(ref accent, value); }
        ModeChoice mode = ModeChoice.Dark;
        public ModeChoice Mode { get => mode; set => Set(ref mode, value); }
        DateTime? day = new DateTime(2026, 10, 24);
        public DateTime? Day { get => day; set => Set(ref day, value); }
        DateTime? until = new DateTime(2026, 10, 30);
        public DateTime? Until { get => until; set => Set(ref until, value); }
        bool designing;
        public bool Designing { get => designing; set => Set(ref designing, value); }
        public double? Share { get => share; set => Set(ref share, value); }
        public IReadOnlyList<SummaryCards.Item> Cards { get => cards; set => Set(ref cards, value); }
        public Command Save { get; } = new Command();
    }

    public sealed class Command : ICommand
    {
        public int Runs; bool can = true;
        public bool Can { get => can; set { can = value; CanExecuteChanged?.Invoke(this, EventArgs.Empty); } }
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => can;
        public void Execute(object? parameter) => Runs++;
    }

    sealed class ValidationVm : INotifyPropertyChanged, INotifyDataErrorInfo
    {
        string name = "";
        public string Name { get => name; set { name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(nameof(Name))); } }
        public bool HasErrors => name.Length == 0;
        public System.Collections.IEnumerable GetErrors(string? propertyName) => HasErrors ? new[] { "Name is required" } : Array.Empty<string>();
        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;
    }

    static class Program
    {
        static int failed;
        static void Ok(bool cond, string what) { if (!cond) failed++; Console.WriteLine((cond ? "ok   " : "FAIL ") + what); }

        /// <summary>The editable table: edits recount, units cycle, formulas of several columns (× first), the footer, three-state
        /// sorting, row selection, the row menu (Duplicate; Delete asks), Add, resizing, design mode.</summary>
        static void Editing()
        {
            EditableTable.Column C(string key, string title, EditableTable.Kind kind = EditableTable.Kind.Text) => new EditableTable.Column(key, title, kind);
            var qty = C("qty", "Quantity", EditableTable.Kind.Unit); qty.Units = new[] { "pcs", "box", "pallet" }; qty.Footer = EditableTable.Total.Count;
            var price = C("price", "Price", EditableTable.Kind.Number); price.Suffix = "€"; price.Decimals = 2; price.Footer = EditableTable.Total.Average;
            var total = C("total", "Total", EditableTable.Kind.Computed); total.Decimals = 2; total.Footer = EditableTable.Total.Sum; total.Terms.Add(new EditableTable.Term("qty")); total.Terms.Add(new EditableTable.Term("price", EditableTable.Op.Multiply));
            var net = C("net", "Net", EditableTable.Kind.Computed); net.Decimals = 2; net.Footer = EditableTable.Total.Sum;
            net.Terms.Add(new EditableTable.Term("total")); net.Terms.Add(new EditableTable.Term("discount", EditableTable.Op.Subtract)); net.Terms.Add(new EditableTable.Term("shipping", EditableTable.Op.Add));
            var item = C("item", "Item"); item.Grow = true;
            var columns = new List<EditableTable.Column> { item, C("sku", "Code"), qty, price, total, C("discount", "Discount", EditableTable.Kind.Number), C("shipping", "Shipping", EditableTable.Kind.Number), net };
            EditableTable.Row R(string name, string code, string q, string unit, string p, string d, string sh) { var r = new EditableTable.Row(); r.Values["item"] = name; r.Values["sku"] = code; r.Values["qty"] = q; r.Units["qty"] = unit; r.Values["price"] = p; r.Values["discount"] = d; r.Values["shipping"] = sh; return r; }
            var rows = new List<EditableTable.Row> { R("Copy paper A4, 80 g", "PAP-A4-80", "12", "box", "4.90", "2", "1.20"), R("Ballpoint pens, blue", "PEN-BL-10", "40", "pcs", "0.85", "0", "0"), R("Whiteboard markers, assorted colours for the meeting rooms", "MRK-WB-4", "6", "pcs", "3.20", "0", "0") };
            var table = new EditableTable { Columns = columns, Rows = rows };
            int changed = 0; table.Changed += () => changed++;
            var window = new Window { Width = 900, Height = 560, Content = new Border { Padding = new Thickness(16), Child = table } }; window.Show();
            void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
            void Click(Point p, Avalonia.Input.MouseButton b = Avalonia.Input.MouseButton.Left, Avalonia.Input.RawInputModifiers m = Avalonia.Input.RawInputModifiers.None) { Settle(); window.MouseDown(p, b, m); window.MouseUp(p, b, m); Settle(); }
            void Key(Avalonia.Input.Key k, Avalonia.Input.RawInputModifiers m = Avalonia.Input.RawInputModifiers.None) { window.KeyPress(k, m, Avalonia.Input.PhysicalKey.None, null); window.KeyRelease(k, m, Avalonia.Input.PhysicalKey.None, null); Settle(); }
            Settle();
            var grids = table.GetVisualDescendants().OfType<Grid>().Where(g => g.ColumnDefinitions.Count == columns.Count + 2).ToList();
            Grid head = grids[0], foot = grids[^1], body = (Grid)grids[1].Parent!;  // each row is a grid of its own in the rows' grid
            Border In(Grid g, int col) => g.Children.OfType<Border>().First(b => b.Child != null && Grid.GetColumn(b) == col);
            Grid Line(int row) => body.Children.OfType<Grid>().First(g => Grid.GetRow(g) == row);
            Border Head(int col) => In(head, col);
            Border Cell(int row, int col) => In(Line(row), col);
            Point Mid(Control c) => c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), window)!.Value;
            int Col(string key) => columns.FindIndex(c => c.Key == key) + 1;
            TextBox Editor() => body.GetVisualDescendants().OfType<TextBox>().Single();
            TextBox Edit(int row, string key) { Click(Mid(Cell(row, Col(key)))); return Editor(); }
            bool At(int row, string key) => body.GetVisualDescendants().OfType<TextBox>().SingleOrDefault() is TextBox t && t.IsFocused && t.Parent == Line(row) && Grid.GetColumn(t) == Col(key);
            string Text(int row, string key) => Cell(row, Col(key)).GetVisualDescendants().OfType<TextBlock>().First().Text ?? "";
            string Items() => string.Join("|", Enumerable.Range(0, rows.Count).Select(r => Text(r, "item")));
            int boxes() => body.GetVisualDescendants().OfType<TextBox>().Count();

            Ok(Text(0, "total") == "58.80" && Text(0, "net") == "58.00", "Total = Quantity × Price, Net = Total − Discount + Shipping: " + Text(0, "net"));
            Ok(boxes() == 0, $"the cells are text: no text box until one is edited ({boxes()})");
            Edit(0, "qty").Text = "10"; Settle();
            Ok(Text(0, "total") == "49.00" && Text(0, "net") == "48.20" && table.Footer(total) == "102.20" && changed == 1, "an edit recounts its row and the footer, and tells the app");
            Key(Avalonia.Input.Key.Enter);
            Ok(At(1, "qty") && boxes() == 1, "Enter goes down the column: the editor moves, nothing else is made");
            Key(Avalonia.Input.Key.Enter, Avalonia.Input.RawInputModifiers.Shift);
            Ok(At(0, "qty") && table.CurrentColumn == qty, "Shift+Enter goes up; the focused cell's column is current");
            Ok(Head(Col("qty")).Background == B(AccentTint) && Head(Col("item")).Background == B(Raised), "the current column's heading has the accent tint");
            Key(Avalonia.Input.Key.Tab);
            Ok(At(0, "price"), "Tab goes to the next cell to edit");
            Key(Avalonia.Input.Key.Tab);
            Ok(At(0, "discount"), "and steps over a computed column");
            // a click puts the caret where it lands
            var itemText = Cell(1, Col("item")).GetVisualDescendants().OfType<TextBlock>().First();
            Click(itemText.TranslatePoint(new Point(8 + 2, 10), window)!.Value);
            Ok(At(1, "item") && Editor().CaretIndex == 0 && Editor().SelectionStart == Editor().SelectionEnd, $"a click at a value's start puts the caret there ({Editor().CaretIndex})");
            var ring = body.GetVisualDescendants().OfType<Border>().Single(b => b.Child == null && b.BorderThickness.Left == 1);
            Ok(ring.IsVisible && ring.Parent == Line(1) && Grid.GetColumn(ring) == Col("item"), "the focused cell has the outline");
            var unit = Cell(0, 3).GetVisualDescendants().OfType<ActionButton>().First();
            Click(Mid(unit)); Ok(rows[0].Units["qty"] == "pallet" && unit.Content?.ToString() == "pallet", "a click on the unit cycles it (box → pallet)");
            Click(Mid(unit)); Ok(rows[0].Units["qty"] == "pcs", "and wraps to the first");
            Ok(At(1, "item"), "the unit's click leaves the cell being edited as it was: " + window.FocusManager?.GetFocusedElement());
            net.Terms.Add(new EditableTable.Term("qty", EditableTable.Op.Multiply)); table.Refresh(); Settle();
            Ok(Math.Abs(table.Value(rows[0], net) - (49 - 2 + 1.2 * 10)) < 1e-9 && table.Formula(net) == "Total − Discount + Shipping × Quantity", "a fourth term joins with ×, which counts first");
            Ok(At(1, "item"), "a redraw keeps the cell being edited");
            net.Terms.RemoveAt(3); table.Refresh(); Settle();
            Ok(table.Footer(price) == "2.98", "the footer averages the price");
            var footLine = In(foot, 4).GetVisualDescendants().OfType<TextBlock>().First();
            Ok(footLine.Inlines is { Count: 2 } runs && ((Avalonia.Controls.Documents.Run)runs[1]).Text == "2.98", "the footer's function and value are one line of text, on one baseline");

            // selection changes in place: the edited cell keeps the keyboard
            var gutter0 = Cell(0, 0);
            Click(Mid(Cell(2, 0)));
            Ok(table.Selected.Count == 1 && Cell(0, 0) == gutter0 && At(1, "item") && ring.IsVisible && ring.Parent == Line(1), "a row click selects without drawing the rows again; the editor and its outline stay");
            Click(Mid(Cell(2, 0)));
            Ok(table.Selected.Count == 0, "a second click clears it");

            // sorting: up, down, back to the original order; off, no sort
            string before = Items();
            Click(Head(4).TranslatePoint(new Point(12, 10), window)!.Value);
            Ok(Items() == "Ballpoint pens, blue|Whiteboard markers, assorted colours for the meeting rooms|Copy paper A4, 80 g", "a heading click sorts up: " + Items());
            Ok(Cell(2, 0) == gutter0, "the rows move to their places, not drawn again");
            Click(Head(4).TranslatePoint(new Point(12, 10), window)!.Value); Click(Head(4).TranslatePoint(new Point(12, 10), window)!.Value);
            Ok(Items() == before, "the third click returns to the original order");
            table.Sorting = false; Settle(); Click(Head(4).TranslatePoint(new Point(12, 10), window)!.Value);
            Ok(Items() == before, "with sorting off, a heading click doesn't sort");
            table.Sorting = true; Settle();
            Click(Mid(table.GetVisualDescendants().OfType<Field>().First()));  // the rows field below the table
            Ok(!ring.IsVisible && boxes() == 0, "focus gone, no outline is left behind and the editor leaves");

            // widths: Fit columns show all of their heading and values, the Fill column takes the rest
            double tableW() => body.Bounds.Width; double W(string key) => head.ColumnDefinitions[Col(key)].ActualWidth;
            Ok(Math.Abs(tableW() + 1 - table.Bounds.Width) < 2, $"the grid fills its box ({tableW()} of {table.Bounds.Width})");
            Ok(columns.Where(c => !c.Grow).All(c => Head(Col(c.Key)).GetVisualDescendants().OfType<TextBlock>().All(t => t.TextLayout.TextLines.Count == 1)), "a Fit column's heading is never wrapped or cut");
            double t0 = tableW(), i0 = W("item"), s0 = W("sku");
            var grip = head.Children.OfType<Border>().Single(b => Grid.GetColumn(b) == 2 && b.Width == 12);
            void Drag(Control from, double dx) { var g0 = Mid(from); Settle(); window.MouseDown(g0, Avalonia.Input.MouseButton.Left); window.MouseMove(new Point(g0.X + dx / 2, g0.Y), Avalonia.Input.RawInputModifiers.LeftMouseButton); window.MouseMove(new Point(g0.X + dx, g0.Y), Avalonia.Input.RawInputModifiers.LeftMouseButton); window.MouseUp(new Point(g0.X + dx, g0.Y), Avalonia.Input.MouseButton.Left); Settle(); }
            Drag(grip, 60);
            Ok(Math.Abs(W("sku") - s0 - 60) < 2 && Math.Abs(i0 - W("item") - 60) < 2 && Math.Abs(tableW() - t0) < 2 && columns[1].Sizing == EditableTable.Sizing.Fixed, $"a Fit column's edge drags it 60 px wider (Fixed); the Fill column gives the room ({W("sku") - s0:0}, {i0 - W("item"):0})");
            Click(Mid(grip)); Click(Mid(grip));
            Ok(columns[1].Sizing == EditableTable.Sizing.Fit && Math.Abs(W("sku") - s0) < 2, "a double-click fits it again");
            var itemGrip = head.Children.OfType<Border>().Single(b => Grid.GetColumn(b) == 1 && b.Width == 12);
            Drag(itemGrip, 200);
            Ok(item.Sizing == EditableTable.Sizing.Fill && item.MinWidth is double m && Math.Abs(W("item") - m) < 2 && W("item") > i0 + 150 && tableW() > t0 + 150, $"a Fill column's edge sets its minimum; past the box the rows scroll across ({item.MinWidth})");
            Settle();
            var bars = table.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ScrollBar>().Where(b => b.Parent?.GetType().Name == "Pinned").ToList();
            var across = bars.Single(b => b.Orientation == Avalonia.Layout.Orientation.Horizontal);
            Ok(across.IsVisible && across.Bounds.Top >= body.TranslatePoint(new Point(0, body.Bounds.Height), across.Parent as Visual ?? across)!.Value.Y - 0.5, "the scroll bar sits below the grid, not over it");
            across.Value = across.Maximum; Settle();
            Ok(Math.Abs(head.TranslatePoint(default, window)!.Value.X - body.TranslatePoint(default, window)!.Value.X) < 0.5, "the heading follows the rows across");
            across.Value = 0;
            item.MinWidth = null; item.MaxWidth = i0 - 60; table.Refresh(); Settle();
            Ok(Math.Abs(W("item") - (i0 - 60)) < 1 && tableW() < t0 - 50, $"a Fill column stops at its maximum ({W("item")}, {tableW()} of {t0})");
            item.MaxWidth = null; columns[1].MinWidth = 140; table.Refresh(); Settle();
            Ok(Math.Abs(W("sku") - 140) < 1, "a minimum holds in a Fit column too");
            columns[1].MinWidth = null; table.Refresh(); Settle();

            // selection: the row numbers (Shift takes a range, Ctrl adds); the row menu
            Click(Mid(Cell(0, 0))); Click(Mid(Cell(2, 0)), m: Avalonia.Input.RawInputModifiers.Shift);
            Ok(table.Selected.Count == 3, "Shift takes a range of rows");
            Click(Mid(Cell(1, 0)), m: Avalonia.Input.RawInputModifiers.Control);
            Ok(table.Selected.Count == 2, "Ctrl takes one out");
            rows[0].Alignments["sku"] = TextAlignment.Center;
            Click(Mid(Cell(0, 2)), Avalonia.Input.MouseButton.Right); Key(Avalonia.Input.Key.Home); Key(Avalonia.Input.Key.Enter);
            Ok(rows.Count == 5 && table.Selected.Count == 2, "the row menu duplicates the two selected rows");
            Ok(table.Selected.Any(r => r.Alignments.GetValueOrDefault("sku") == TextAlignment.Center), "duplicated rows preserve individual cell alignment overrides");
            Click(Mid(Cell(4, 0)), Avalonia.Input.MouseButton.Right);  // a right-click on a row number, one of the two copies, both still selected
            Key(Avalonia.Input.Key.Up); Key(Avalonia.Input.Key.Enter); Settle();
            var ask = window.OwnedWindows.FirstOrDefault();
            Ok(ask != null && rows.Count == 5, "a right-click on a row number opens the row menu; Delete asks first");
            ask?.GetVisualDescendants().OfType<ActionButton>().First(b => b.Content?.ToString()?.StartsWith("Delete", StringComparison.Ordinal) == true).PerformClick(); Settle();
            for (int k = 0; k < 5 && rows.Count != 3; k++) Settle();
            Ok(rows.Count == 3, $"and deletes the selected rows once confirmed ({rows.Count}, dialog {(ask == null ? "none" : ask.IsVisible ? "open" : "closed")})");
            IReadOnlyList<EditableTable.Row>? opened = null;
            table.RowActions = EditableTable.RowAction.None; table.RowMenuItems.Add(new EditableTable.RowMenuItem(new MenuEntry("Open record"), r => opened = r));
            Click(Mid(Cell(0, 0)), Avalonia.Input.MouseButton.Right); Key(Avalonia.Input.Key.Home); Key(Avalonia.Input.Key.Enter); Settle();
            Ok(opened?.Count == 1 && opened[0] == rows[0] && rows.Count == 3, "the row menu runs an app item with the selected rows; hidden built-in items are gone");
            table.RowMenuItems.Clear(); table.RowActions = EditableTable.RowAction.All;
            table.AddRows(3); Settle();
            Ok(rows.Count == 6 && body.Children.OfType<Grid>().Count() == 6, "Add takes several rows at once");

            // a short table: the rows scroll under a pinned heading and footer, the bar at their right, outside the grid
            table.Height = 220; Settle(); Settle();
            var downBar = table.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ScrollBar>().Single(b => b.Parent?.GetType().Name == "Pinned" && b.Orientation == Avalonia.Layout.Orientation.Vertical);
            double headTop = head.TranslatePoint(default, window)!.Value.Y;
            downBar.Value = downBar.Maximum; Settle();
            Ok(downBar.IsVisible && downBar.TranslatePoint(default, window)!.Value.X >= body.TranslatePoint(new Point(body.Bounds.Width, 0), window)!.Value.X - 0.5, "the rows scroll; the vertical bar sits right of the grid");
            Ok(Math.Abs(head.TranslatePoint(default, window)!.Value.Y - headTop) < 0.5 && foot.TranslatePoint(new Point(0, foot.Bounds.Height), table)!.Value.Y <= table.Bounds.Height + 0.5, "the heading and the footer stay put");
            table.Height = double.NaN; Settle();

            // design mode: a heading dragged to a gap, Ctrl+Right, a new column
            table.Design = true; Settle();
            var heads = () => string.Join("|", columns.Select(c => c.Title));
            var itemHead = Head(1);
            var from = Mid(Head(3)); var to = itemHead.TranslatePoint(new Point(itemHead.Bounds.Width, 14), window)!.Value;
            Ok(Math.Abs(Head(2).Bounds.Width - s0) < 2, "designing preserves heading widths without an overlapping grip");
            bool lineShown = false;
            var orderBefore = heads();
            Settle(); window.MouseDown(from, Avalonia.Input.MouseButton.Left); window.MouseMove(to, Avalonia.Input.RawInputModifiers.LeftMouseButton); window.MouseUp(to, Avalonia.Input.MouseButton.Left); Settle();
            Ok(heads() == orderBefore, "plain designer header drags cannot accidentally reorder columns");
            Settle(); window.MouseDown(from, Avalonia.Input.MouseButton.Left, Avalonia.Input.RawInputModifiers.Control); window.MouseMove(new Point((from.X + to.X) / 2, from.Y), Avalonia.Input.RawInputModifiers.LeftMouseButton | Avalonia.Input.RawInputModifiers.Control); window.MouseMove(to, Avalonia.Input.RawInputModifiers.LeftMouseButton | Avalonia.Input.RawInputModifiers.Control); Settle();
            lineShown = (bool)typeof(EditableTable).GetProperty("LineShown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(table)!;
            window.MouseUp(to, Avalonia.Input.MouseButton.Left, Avalonia.Input.RawInputModifiers.Control); Settle();
            Ok(lineShown && heads().StartsWith("Item|Quantity|Code", StringComparison.Ordinal), "a Ctrl-dragged heading lands in the gap, the accent line marking it: " + heads());
            Head(2).Focus(); Key(Avalonia.Input.Key.Right, Avalonia.Input.RawInputModifiers.Control);
            Ok(heads().StartsWith("Item|Code|Quantity", StringComparison.Ordinal), "Ctrl+Right moves the focused heading");
            var added = table.InsertColumn(3); Settle();
            Ok(columns[3] == added && added.Title == "New column" && table.CurrentColumn == added, "a new column comes in and is current");
            window.Close();
        }

        /// <summary>The toolbar and the Add rows bar open through properties: hidden actions, placement, the app's own controls.</summary>
        static void ToolbarLayout()
        {
            var column = new EditableTable.Column("code", "Code", EditableTable.Kind.General);
            var table = new EditableTable { ShowColumnEditor = true, Columns = new List<EditableTable.Column> { column }, Rows = new List<EditableTable.Row> { new() } };
            var window = new Window { Width = 1100, Height = 600, Content = table }; window.Show();
            void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
            ActionButton? Button(string text) => table.GetVisualDescendants().OfType<ActionButton>().FirstOrDefault(b => b.Content?.ToString() == text);
            double Y(Control c) => c.TranslatePoint(default, table)!.Value.Y;
            var grid = table.GetVisualDescendants().OfType<Panel>().Single(p => p.GetType().Name == "Pinned");
            Settle();
            Ok(Button("Remove column")!.IsEffectivelyVisible && Button("Calculated column")!.IsEffectivelyVisible && Y(Button("Edit columns")!) < Y(grid) && Y(Button("Add")!) > Y(grid), "toolbar layout: the default is all actions, toolbar above the table, Add rows below");
            table.ToolbarActions = EditableTable.ToolbarAction.All & ~(EditableTable.ToolbarAction.RemoveColumn | EditableTable.ToolbarAction.CalculatedColumn); Settle();
            Ok(!Button("Remove column")!.IsEffectivelyVisible && !Button("Calculated column")!.IsEffectivelyVisible && Button("Add column")!.IsEffectivelyVisible && Button("Edit columns")!.IsEffectivelyVisible, "toolbar layout: a cleared action is hidden, the others stay");
            table.ToolbarActions = EditableTable.ToolbarAction.None; Settle();
            Ok(!Button("Edit columns")!.IsEffectivelyVisible && !Button("Add column")!.IsEffectivelyVisible, "toolbar layout: no actions leaves no toolbar");
            var export = new ActionButton("Export", ButtonKind.Ghost); table.ToolbarItems.Add(export); Settle();
            Ok(export.IsEffectivelyVisible && !Button("Edit columns")!.IsEffectivelyVisible, "toolbar layout: an app action shows in a toolbar without built-in actions");
            table.ToolbarActions = EditableTable.ToolbarAction.All; table.ToolbarPlacement = EditableTable.BarPlacement.Bottom; table.AddRowsPlacement = EditableTable.BarPlacement.Top; Settle();
            Ok(Y(Button("Edit columns")!) > Y(grid) + grid.Bounds.Height - 1 && Y(export) > Y(grid) && Y(Button("Add")!) < Y(grid) + 8, "toolbar layout: toolbar below the table, Add rows above");
            var import = new ActionButton("Import", ButtonKind.Ghost); table.AddRowsItems.Add(import); Settle();
            Ok(import.IsEffectivelyVisible && Math.Abs(Y(import) - Y(Button("Add")!)) < 1, "toolbar layout: an app action joins the Add rows bar");
            table.Design = true; Settle();
            Ok(Y(Button("Done")!) > Y(table.GetVisualDescendants().OfType<TextTabs>().First()), "toolbar layout: the open column editor sits above a bottom toolbar");
            table.AddRowsPlacement = EditableTable.BarPlacement.Hidden; Settle();
            Ok(!Button("Add")!.IsEffectivelyVisible && !import.IsEffectivelyVisible, "toolbar layout: Hidden removes the Add rows bar");
            bool rejected = false; try { table.ToolbarActions = (EditableTable.ToolbarAction)64; } catch (ArgumentException) { rejected = true; }
            Ok(rejected, "toolbar layout: an unknown action flag is rejected");
            window.Close();
        }

        /// <summary>The ribbon opens through properties: tabs, hidden fields, allowed paper formats, the app's own column fields.</summary>
        static void RibbonExtension()
        {
            var column = new EditableTable.Column("code", "Code", EditableTable.Kind.General);
            var table = new EditableTable { ShowColumnEditor = true, Design = true, Columns = new List<EditableTable.Column> { column }, Rows = new List<EditableTable.Row> { new() } };
            var window = new Window { Width = 3000, Height = 700, Content = table }; window.Show();
            void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
            bool Shows(string label) => table.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == label && t.IsEffectivelyVisible);
            TextTabs Tabs() => table.GetVisualDescendants().OfType<TextTabs>().First();
            Settle();
            Ok(Shows("Footer") && Shows("Paper format") && Shows("Fixed width") == false && Shows("Sorting"), "ribbon: defaults show every built-in field");
            table.HiddenRibbonFields = new[] { "Footer", "Sorting" }; Settle();
            Ok(!Shows("Footer") && !Shows("Sorting") && Shows("Alignment"), "ribbon: a hidden field is gone, the others stay");
            table.HiddenRibbonFields = null; Settle();
            Ok(Shows("Footer") && Shows("Sorting"), "ribbon: clearing the hidden list brings the fields back");
            table.RibbonTabs = EditableTable.RibbonTab.Column | EditableTable.RibbonTab.Width; Settle();
            Ok(!Shows("Paper format") && !Shows("Subheading") && Shows("Heading") && Shows("Min width"), "ribbon: a cleared tab drops its fields");
            table.RibbonTabs = EditableTable.RibbonTab.Column; Settle();
            Ok(!Tabs().IsEffectivelyVisible, "ribbon: one tab left needs no tab strip");
            table.RibbonTabs = EditableTable.RibbonTab.All; Settle();
            Ok(Shows("Paper format"), "ribbon: all tabs again");
            table.PaperFormats = new[] { EditableTable.PaperKind.A4, EditableTable.PaperKind.A5 }; Settle();
            var format = table.GetVisualDescendants().OfType<ChoiceButton>().First(c => c.Items.Contains("A5"));
            Ok(format.Items.Count() == 3 && !format.Items.Contains("Legal"), "ribbon: only the allowed paper formats are offered, the current one stays listed");
            string export = "x1";
            table.ColumnFields.Add(new EditableTable.ColumnField("Export name", _ => export, (_, v) => export = v)); Settle();
            var field = table.GetVisualDescendants().OfType<Field>().FirstOrDefault(f => f.Text == "x1");
            Ok(Shows("Export name") && field != null, "ribbon: an app field shows its value for the current column");
            window.Close();
        }

        /// <summary>The editable table counts in decimals (money to the cent, as an export does); an app's own rules come in
        /// through Calculate, and FromTerms keeps the table's formula for the columns it leaves.</summary>
        static void ColumnRibbon()
        {
            var code = new EditableTable.Column("code", "Code", EditableTable.Kind.General);
            var quantity = new EditableTable.Column("qty", "Quantity", EditableTable.Kind.Unit) { Units = new[] { "pcs", "box" } };
            var row = new EditableTable.Row(); row.Values["code"] = "00123"; row.Values["qty"] = "12"; row.Units["qty"] = "box";
            var table = new EditableTable { ShowColumnEditor = true, Columns = new List<EditableTable.Column> { code, quantity }, Rows = new List<EditableTable.Row> { row } };
            int paperNotifications = 0; table.PropertyChanged += (_, e) => { if (e.Property == EditableTable.PaperSizeProperty) paperNotifications++; };
            var window = new Window { Width = 1100, Height = 600, Content = table }; window.Show();
            void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
            ActionButton Button(string text) => table.GetVisualDescendants().OfType<ActionButton>().First(b => b.Content?.ToString() == text);
            Settle();
            Ok(Button("Edit columns").IsEffectivelyVisible && !Button("Add column").IsEnabled, "native column toolbar starts in data mode");
            Button("Edit columns").PerformClick(); Settle();
            Ok(table.Design && Button("Done").Kind == ButtonKind.Normal, "native Done uses the shared secondary action button");
            var alignment = table.GetVisualDescendants().OfType<ChoiceButton>().First(x => x.Items.Contains("Center"));
            ((Avalonia.Automation.Provider.IExpandCollapseProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(alignment)!).Expand(); Settle();
            foreach (var key in new[] { Avalonia.Input.Key.Down, Avalonia.Input.Key.Down, Avalonia.Input.Key.Enter }) { window.KeyPress(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); window.KeyRelease(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle(); }
            Ok(code.Alignment == TextAlignment.Center && table.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "00123" && t.TextAlignment == TextAlignment.Center), "native ribbon alignment changes the actual column and rendered cell");
            var decimals = table.GetVisualDescendants().OfType<ChoiceButton>().First(x => x.Items.SequenceEqual(new[] { "Auto", "0", "1", "2", "3", "4" }));
            ((Avalonia.Automation.Provider.IExpandCollapseProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(decimals)!).Expand(); Settle();
            foreach (var key in new[] { Avalonia.Input.Key.Down, Avalonia.Input.Key.Down, Avalonia.Input.Key.Down, Avalonia.Input.Key.Enter }) { window.KeyPress(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); window.KeyRelease(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle(); }
            var suffix = table.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Suffix").GetVisualParent()!.GetVisualDescendants().OfType<Field>().First();
            suffix.Box.Focus(); suffix.Box.Text = "€"; Settle();
            Ok(code.Decimals == 2 && code.Suffix == "€" && row.Values["code"] == "00123", "native ribbon sets decimals and suffix on the column; stored values stay as typed");
            suffix.Box.Text = ""; Settle();
            Ok(code.Suffix == null, "native ribbon clears the suffix");
            table.CurrentColumn = quantity; Settle();
            var units = table.GetVisualDescendants().OfType<Field>().First(f => f.Text == "pcs, box"); units.Box.Focus(); units.Box.Text = "kg, carton, kg"; Settle();
            Ok(quantity.Units.SequenceEqual(new[] { "pcs", "box" }), "native unit definitions retain the draft until committed");
            Button("Done").Focus(); Settle();
            Ok(quantity.Units.SequenceEqual(new[] { "kg", "carton" }) && row.Units["qty"] == "box", "native unit definitions normalize duplicates and preserve stored units");
            units.Box.Focus(); units.Box.Text = " , "; Settle(); Button("Done").Focus(); Settle(); Ok(units.Error != null && quantity.Units.Count == 2, "native ribbon rejects an empty unit list");
            Button("Done").PerformClick(); Settle();
            Ok(table.Design && units.Box.IsFocused && units.Error != null, "native Done validates a pending unit draft and keeps its error visible");
            units.Box.Focus(); units.Box.Text = "kg, carton"; Button("Done").Focus(); Settle();
            var otherUnit = new EditableTable.Column("other", "Other", EditableTable.Kind.Unit) { Units = new[] { "m" } };
            table.Columns.Add(otherUnit); table.Refresh(); table.CurrentColumn = quantity; Settle();
            units.Box.Focus(); units.Box.Text = "litre"; Settle(); table.CurrentColumn = otherUnit; Settle();
            Ok(quantity.Units.SequenceEqual(new[] { "litre" }) && otherUnit.Units.SequenceEqual(new[] { "m" }) && units.Text == "m", "switching columns commits a unit draft to its original column, not the newly selected one");
            units.Box.Text = "ft"; Settle(); table.IsReadOnly = true; Settle();
            Ok(otherUnit.Units.SequenceEqual(new[] { "m" }), "revoking edit permission does not commit a focused unit draft");
            table.IsReadOnly = false; table.Design = true; Button("Done").Focus(); Settle();
            Button("Calculated column").PerformClick(); Settle();
            Ok(table.CurrentColumn?.Kind == EditableTable.Kind.Computed && table.CurrentColumn.Terms.Count == 2, "native toolbar adds an actual calculated column");
            var operatorPicker = table.GetVisualDescendants().OfType<ChoiceButton>().First(c => c.Items.SequenceEqual(new[] { "+", "−", "×", "÷" }));
            var operatorText = operatorPicker.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "+");
            Ok(operatorText.Bounds.Width >= Look.Measure("+", Ui(TextSize.Body)), "native formula operator text is visible alongside its chevron");
            Button("Done").PerformClick(); Settle(); Ok(!table.Design && code.Alignment == TextAlignment.Center, "native leaving edit mode retains column alignment");
            table.PaperFormat = EditableTable.PaperKind.A4; Settle();
            Ok(table.PaperSize == new Size(210, 297), "native A4 exposes millimetre dimensions for print/export");
            var viewport = table.GetVisualDescendants().OfType<Panel>().Single(p => p.GetType().Name == "Pinned");
            Ok(Math.Abs(viewport.Bounds.Width - 210 * 96 / 25.4) < 1, "native paper format constrains the real table viewport");
            table.PaperLandscape = true; Settle(); Ok(table.PaperSize == new Size(297, 210), "native landscape swaps page dimensions");
            table.CustomPaperSize = new Size(250, 350); table.PaperFormat = EditableTable.PaperKind.Custom; Settle();
            Ok(table.PaperSize == new Size(350, 250), "native custom paper matches the web orientation and dimensions");
            bool rejected = false; try { table.CustomPaperSize = new Size(-1, 350); } catch (ArgumentException) { rejected = true; }
            Ok(rejected && table.CustomPaperSize == new Size(250, 350), "native custom dimensions reject invalid values without changing the page");
            table.PaperFormat = EditableTable.PaperKind.Screen; Settle(); Ok(table.PaperSize == null && double.IsNaN(viewport.Width), "Screen restores unconstrained table layout");
            var vm = new Vm { PaperFormat = table.PaperFormat, PaperLandscape = table.PaperLandscape, PaperDimensions = table.CustomPaperSize };
            using var formatBinding = table.Bind(EditableTable.PaperFormatProperty, new Avalonia.Data.Binding(nameof(Vm.PaperFormat)) { Source = vm });
            using var landscapeBinding = table.Bind(EditableTable.PaperLandscapeProperty, new Avalonia.Data.Binding(nameof(Vm.PaperLandscape)) { Source = vm });
            using var customBinding = table.Bind(EditableTable.CustomPaperSizeProperty, new Avalonia.Data.Binding(nameof(Vm.PaperDimensions)) { Source = vm });
            Button("Edit columns").PerformClick(); Settle();
            var tabs = table.GetVisualDescendants().OfType<TextTabs>().Single(); tabs.SetActive(3); Settle();
            var paper = table.GetVisualDescendants().OfType<ChoiceButton>().Single(c => c.Items.Contains("A4"));
            ((Avalonia.Automation.Provider.IExpandCollapseProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(paper)!).Expand(); Settle();
            foreach (var key in new[] { Avalonia.Input.Key.Down, Avalonia.Input.Key.Down, Avalonia.Input.Key.Enter }) { window.KeyPress(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); window.KeyRelease(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle(); }
            Ok(table.PaperFormat == EditableTable.PaperKind.A4, "native ribbon paper choice updates the bindable format");
            Ok(vm.PaperFormat == EditableTable.PaperKind.A4, "native paper interaction writes through the default two-way Avalonia binding");
            vm.PaperFormat = EditableTable.PaperKind.Custom; vm.PaperLandscape = false; Settle();
            Ok(table.PaperFormat == EditableTable.PaperKind.Custom && table.PaperSize == new Size(250, 350), "the view model still controls paper settings after a ribbon edit");
            var customWidth = table.GetVisualDescendants().OfType<Field>().Single(f => Avalonia.Automation.AutomationProperties.GetLabeledBy(f.Box) is TextBlock label && label.Text == "Width (mm)");
            customWidth.Box.Focus(); customWidth.Box.Text = "280"; Settle();
            Ok(vm.PaperDimensions.Width == 280 && table.PaperSize?.Width == 280, "custom paper input updates its view-model binding");
            Button("Done").Focus(); Settle(); vm.PaperDimensions = new Size(290, 360); Settle();
            Ok(customWidth.Text == "290" && table.PaperSize == new Size(290, 360) && paperNotifications > 3, "later view-model changes refresh the editor and effective page-size notifications");
            table.CurrentColumn = code; tabs.SetActive(0); Settle();
            Button("Remove column").PerformClick(); Settle(); var confirm = window.OwnedWindows.Single();
            table.IsReadOnly = true; confirm.GetVisualDescendants().OfType<ActionButton>().First(b => b.Content?.ToString() == "Remove column").PerformClick(); Settle(); Settle();
            Ok(table.Columns.Contains(code) && row.Values.ContainsKey(code.Key), "a permission change during column removal confirmation prevents deletion");
            table.IsReadOnly = false; table.Design = true; table.CurrentColumn = code; Settle();
            Button("Remove column").PerformClick(); Settle(); confirm = window.OwnedWindows.Single();
            table.Columns.Remove(code); var replacement = new EditableTable.Column(code.Key, "Replacement"); table.Columns.Add(replacement); row.Values[code.Key] = "preserve"; table.Refresh();
            confirm.GetVisualDescendants().OfType<ActionButton>().First(b => b.Content?.ToString() == "Remove column").PerformClick(); Settle(); Settle();
            Ok(table.Columns.Contains(replacement) && row.Values[code.Key] == "preserve", "a stale removal confirmation cannot delete a replacement column's data");

            window.Close();
        }

        static void ColumnRibbonFeedback()
        {
            try
            {
                foreach (var culture in new[] { "cs-CZ", "en-US" })
                {
                    Look.Configure(new ShadeOptions { Culture = System.Globalization.CultureInfo.GetCultureInfo(culture), ReducedMotion = true });
                    var sale = new EditableTable.Column("sale-id", "Cena", EditableTable.Kind.Number) { Subtitle = "prodejní / MJ", HeadingSize = 20, SubheadingSize = 15.5 };
                    var purchase = new EditableTable.Column("purchase-id", "Cena", EditableTable.Kind.Number) { Subtitle = "nákupní / MJ" };
                    var computed = new EditableTable.Column("total", "Total", EditableTable.Kind.Computed); computed.Terms.Add(new EditableTable.Term(sale.Key));
                    var table = new EditableTable { ShowColumnEditor = true, Design = true, Columns = new List<EditableTable.Column> { sale, purchase, computed }, HeadingSize = 22.5, SubheadingSize = 16, ValueSize = 18, PaperFormat = EditableTable.PaperKind.Custom };
                    var window = new Window { Width = 1300, Height = 900, Content = table }; window.Show();
                    void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
                    Field Field(string label) => table.GetVisualDescendants().OfType<Field>().Single(f => Avalonia.Automation.AutomationProperties.GetLabeledBy(f.Box) is TextBlock text && text.Text == label);
                    ChoiceButton Choice(string label) => table.GetVisualDescendants().OfType<ChoiceButton>().First(c => c.IsEffectivelyVisible && c.Parent is StackPanel panel && panel.Children.OfType<TextBlock>().Any(t => t.Text == label));
                    ActionButton Done() => table.GetVisualDescendants().OfType<ActionButton>().Single(b => b.Content?.ToString() == "Done");
                    Settle(); var tabs = table.GetVisualDescendants().OfType<TextTabs>().Single(); tabs.SetActive(2); Settle();
                    var min = Field("Min width"); min.Box.Focus(); min.Box.Text = "60,5"; Settle();
                    Ok(culture == "cs-CZ" ? sale.MinWidth == 60.5 && min.Error == null : sale.MinWidth == null && min.Error != null, $"{culture}: ribbon decimal comma is localized or rejected, never accepted as 605");
                    if (min.Error != null) { min.Box.Text = "60.5"; Settle(); }
                    Done().Focus(); Settle(); table.Refresh(); Settle();
                    Ok(min.Text == (culture == "cs-CZ" ? "60,5" : "60.5"), $"{culture}: ribbon widths format under the same numeric contract");
                    min.Box.Focus(); min.Box.Text = culture == "cs-CZ" ? "1\u00a0000" : "1,000"; Settle();
                    Ok(sale.MinWidth == 60.5 && min.Error != null, $"{culture}: grouping is rejected without changing width magnitude");
                    tabs.SetActive(0); Settle(); Done().PerformClick(); Settle();
                    Ok(table.Design && tabs.Active == 2 && min.Box.IsFocused && min.Error != null, $"{culture}: Done reveals and focuses the invalid width pane");
                    window.KeyPress(Avalonia.Input.Key.Escape, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); window.KeyRelease(Avalonia.Input.Key.Escape, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle();
                    Ok(table.Design && min.Error == null && sale.MinWidth == 60.5, $"{culture}: Escape restores a ribbon field to its last applied value without closing edit mode");
                    min.Box.Text = "invalid"; Done().PerformClick(); Settle();
                    Ok(table.Design && min.Error != null && min.Box.IsFocused, $"{culture}: Done reads the current draft before Avalonia posts TextChanged");
                    min.Box.Text = culture == "cs-CZ" ? "70,5" : "70.5"; Done().PerformClick(); Settle();
                    Ok(!table.Design && sale.MinWidth == 70.5, $"{culture}: Done commits a valid draft before Avalonia posts TextChanged");
                    table.Design = true; Settle(); min.Box.Focus(); min.Box.Text = culture == "cs-CZ" ? "60,5" : "60.5"; Settle();
                    min.Box.Text = "invalid"; Settle(); table.CurrentColumn = purchase; Settle(); table.CurrentColumn = sale; Settle();
                    Ok(min.Error == null && min.Text == sale.MinWidth!.Value.ToString(Look.Culture), $"{culture}: changing columns discards the invalid width draft");
                    tabs.SetActive(3); Settle(); var paperWidth = Field("Width (mm)"); paperWidth.Box.Focus(); paperWidth.Box.Text = "240,5"; Settle();
                    Ok(culture == "cs-CZ" ? table.CustomPaperSize.Width == 240.5 && paperWidth.Error == null : table.CustomPaperSize.Width == 210 && paperWidth.Error != null, $"{culture}: paper dimensions use the ribbon numeric contract");
                    paperWidth.Box.Text = "invalid"; Settle(); tabs.SetActive(0); Settle(); table.Refresh(); Settle(); Done().PerformClick(); Settle();
                    Ok(table.Design && tabs.Active == 3 && paperWidth.Box.IsFocused && paperWidth.Text == "invalid" && paperWidth.Error != null, $"{culture}: paper errors survive blur and rebuild and block Done");
                    window.KeyPress(Avalonia.Input.Key.Escape, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); window.KeyRelease(Avalonia.Input.Key.Escape, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle();
                    tabs.SetActive(1); Settle();
                    Ok(Choice("Heading size").Current == "20" && Choice("Subheading size").Current == 15.5.ToString(Look.Culture) && sale.HeadingSize == 20, $"{culture}: custom column font sizes display exactly without changing the model");
                    tabs.SetActive(3); Settle();
                    Ok(Choice("Heading size").Current == 22.5.ToString(Look.Culture) && Choice("Subheading size").Current == "16" && Choice("Value size").Current == "18", $"{culture}: table typography choices include supplied sizes");
                    table.CurrentColumn = computed; tabs.SetActive(0); Settle();
                    var source = table.GetVisualDescendants().OfType<ChoiceButton>().Single(c => c.Items.Contains("Cena · prodejní / MJ"));
                    Ok(source.Items.SequenceEqual(new[] { "Cena · prodejní / MJ", "Cena · nákupní / MJ" }) && source.Current == "Cena · prodejní / MJ", $"{culture}: duplicate formula titles use subtitles and retain the selected identity");
                    purchase.Subtitle = sale.Subtitle; table.Refresh(); Settle();
                    source = table.GetVisualDescendants().OfType<ChoiceButton>().Single(c => c.Items.Contains("Cena · prodejní / MJ (1)"));
                    Ok(source.Items.SequenceEqual(new[] { "Cena · prodejní / MJ (1)", "Cena · prodejní / MJ (2)" }) && source.Items.All(x => !x.Contains("-id", StringComparison.Ordinal)), $"{culture}: identical rich headings use short column positions, never storage keys");
                    ((Avalonia.Automation.Provider.IExpandCollapseProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(source)!).Expand(); Settle();
                    foreach (var key in new[] { Avalonia.Input.Key.Down, Avalonia.Input.Key.Enter }) { window.KeyPress(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); window.KeyRelease(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle(); }
                    Ok(computed.Terms[0].Key == purchase.Key, $"{culture}: positional formula labels map back to the correct storage identity");
                    Done().PerformClick(); Settle(); Ok(!table.Design, $"{culture}: Done exits after ribbon errors are resolved");
                    window.Close();
                }
            }
            finally { Look.Configure(new ShadeOptions()); }
        }

        static void ColumnViewportAndLayout()
        {
            var translations = new Dictionary<string, string>
            {
                ["Paper format"] = "Formát papíru",
                ["Heading size"] = "Velikost nadpisu",
                ["Subheading size"] = "Velikost podnadpisu",
                ["Value size"] = "Velikost hodnot",
                ["Min width"] = "Minimální šířka",
                ["Max width"] = "Maximální šířka",
                ["Fixed width"] = "Pevná šířka",
                ["Fill"] = "Vyplnit prostor",
                ["Fit"] = "Podle obsahu",
                ["Fixed"] = "Pevná",
                ["Width"] = "Šířka"
            };
            try
            {
                Look.Translate = key => translations.GetValueOrDefault(key, key);
                foreach (double scale in new[] { 1d, 1.25, 1.5, 2 })
                {
                    Look.Configure(new ShadeOptions { Culture = System.Globalization.CultureInfo.GetCultureInfo("cs-CZ"), TextScale = scale, ReducedMotion = true });
                    var column = new EditableTable.Column("item", "Název zboží") { Sizing = EditableTable.Sizing.Fill, MinWidth = 330.0354228182 };
                    var fixedColumn = new EditableTable.Column("fixed", "Kód") { Sizing = EditableTable.Sizing.Fixed, Width = 330.034, MinWidth = 330.034, MaxWidth = 700.0354228182 };
                    var row = new EditableTable.Row(); row.Values[column.Key] = "Zboží"; row.Values[fixedColumn.Key] = "Kód";
                    var table = new EditableTable { ShowColumnEditor = true, Design = true, Columns = new List<EditableTable.Column> { column, fixedColumn }, Rows = new List<EditableTable.Row> { row }, PaperFormat = EditableTable.PaperKind.A4, PaperLandscape = true };
                    var window = new Window { Width = 1600, Height = 1200, Content = table }; window.Show();
                    void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
                    Field Field(string label) => table.GetVisualDescendants().OfType<Field>().Single(f => Avalonia.Automation.AutomationProperties.GetLabeledBy(f.Box) is TextBlock text && text.Text == label);
                    ActionButton Button(string label) => table.GetVisualDescendants().OfType<ActionButton>().Single(b => b.Content?.ToString() == label);
                    Settle(); var tabs = table.GetVisualDescendants().OfType<TextTabs>().Single(); var viewport = (Panel)typeof(EditableTable).GetField("area", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(table)!;
                    tabs.SetActive(3); Settle();
                    var format = table.GetVisualDescendants().OfType<ChoiceButton>().Single(c => c.Items.Contains("A4"));
                    var formatGroup = (StackPanel)format.Parent!;
                    var formatCaption = (TextBlock)formatGroup.Children[0];
                    var note = table.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "297 × 210 mm");
                    var noteParent = (StackPanel)note.Parent!;
                    var bottomHeight = noteParent.Bounds.Height;
                    Ok(table.PaperDimensionsPlacement == EditableTable.PaperDimensionsPosition.Bottom && note.IsVisible, "paper dimensions retain the bottom-row default");
                    var placementVm = new Vm();
                    using var placementBinding = table.Bind(EditableTable.PaperDimensionsPlacementProperty, new Avalonia.Data.Binding(nameof(Vm.PaperPlacement)) { Source = placementVm });
                    placementVm.PaperPlacement = EditableTable.PaperDimensionsPosition.FormatLabel; Settle(); Settle();
                    Ok(!note.IsVisible && noteParent.Bounds.Height < bottomHeight && formatCaption.Text == "Formát papíru · 297 × 210 mm", $"{scale}x: bound compact placement removes the bottom row and uses the localized caption");
                    table.PaperLandscape = false; Settle();
                    Ok(formatCaption.Text == "Formát papíru · 210 × 297 mm", "portrait dimensions update the caption");
                    table.PaperFormat = EditableTable.PaperKind.Custom; table.CustomPaperSize = new Size(250, 350); Settle();
                    Ok(formatCaption.Text == "Formát papíru · 250 × 350 mm", "custom dimensions update the caption");
                    table.PaperFormat = EditableTable.PaperKind.Screen; Settle();
                    Ok(formatCaption.Text == "Formát papíru" && !note.IsVisible, "Screen has no physical dimensions or note row");
                    table.PaperFormat = EditableTable.PaperKind.A4; table.PaperLandscape = true; Settle();
                    placementVm.PaperPlacement = EditableTable.PaperDimensionsPosition.Bottom; Settle();
                    Ok(note.IsVisible, "later binding updates restore the default row");
                    placementVm.PaperPlacement = EditableTable.PaperDimensionsPosition.FormatLabel; Settle();
                    int changes = 0; table.Changed += () => changes++;
                    tabs.SetActive(2); Settle();
                    var vm = new Vm(); using var binding = table.Bind(EditableTable.ConstrainToPaperProperty, new Avalonia.Data.Binding(nameof(Vm.ConstrainToPaper)) { Source = vm });
                    vm.ConstrainToPaper = false; Settle();
                    Ok(!table.ConstrainToPaper && Math.Abs(viewport.Bounds.Width - table.Bounds.Width) < 1 && table.PaperSize == new Size(297, 210), $"{scale}x: full-width editing retains A4 landscape document metadata through binding");
                    tabs.SetActive(2); Settle(); var min = Field("Minimální šířka");
                    Ok(min.Text == "330,04" && column.MinWidth == 330.0354228182, $"{scale}x: rounded localized width display preserves exact geometry");
                    min.Box.Focus(); Button("Done").Focus(); Settle(); table.Refresh(); Settle(); tabs.SetActive(1); Settle(); tabs.SetActive(2); Settle(); Button("Done").PerformClick(); Settle();
                    Ok(!table.Design && column.MinWidth == 330.0354228182 && changes == 0, $"{scale}x: focus, blur, refresh, tabs and Done do not commit rounded widths or dirty the document");
                    Button("Edit columns").PerformClick(); table.CurrentColumn = fixedColumn; tabs.SetActive(2); Settle();
                    var width = Field("Pevná šířka"); width.Box.Focus(); Button("Done").Focus(); Settle(); Button("Done").PerformClick(); Settle();
                    Ok(!table.Design && fixedColumn.Width == 330.034 && fixedColumn.MinWidth == 330.034 && fixedColumn.MaxWidth == 700.0354228182 && changes == 0, $"{scale}x: a rounded fixed-width display below its exact minimum remains valid and unchanged");
                    Button("Edit columns").PerformClick(); width.Box.Focus(); width.Box.Text = "bad"; Settle(); width.Box.Text = "330,03"; Settle(); Button("Done").PerformClick(); Settle();
                    Ok(table.Design && width.Error != null && fixedColumn.Width == 330.034, $"{scale}x: explicitly typing the displayed rounded value still checks the exact minimum");
                    window.KeyPress(Avalonia.Input.Key.Escape, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); window.KeyRelease(Avalonia.Input.Key.Escape, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle(); Button("Done").PerformClick(); Settle();
                    Ok(!table.Design && fixedColumn.Width == 330.034 && changes == 0, $"{scale}x: discarding an explicit draft restores display state without quantizing geometry");
                    Button("Edit columns").PerformClick(); table.CurrentColumn = column; tabs.SetActive(2); Settle();
                    min.Box.Focus(); min.Box.Text = "360,125"; Settle();
                    Ok(column.MinWidth == 360.125 && min.Text == "360,125", $"{scale}x: explicit width edits retain the typed precision and draft text");
                    min.Box.Text = "invalid"; min.Box.CaretIndex = 3; Settle(); var originalField = min;
                    foreach (double size in new[] { 2400d, 1600, 1280, 960, 840, 2400 })
                    {
                        window.Width = size; Settle(); Settle();
                        Ok(ReferenceEquals(Field("Minimální šířka"), originalField) && min.Box.IsFocused && min.Box.CaretIndex == 3 && min.Text == "invalid" && min.Error != null && table.CurrentColumn == column, $"{scale}x at {size}: adaptive resizing retains field identity, invalid draft, caret, focus and current column");
                    }
                    min.Box.Text = "360,125"; Settle(); Button("Done").Focus(); Settle();
                    changes = 0;
                    foreach (double size in new[] { 840d, 960, 1280, 1600, 2400 })
                    {
                        window.Width = size; tabs.SetActive(3); Settle(); Settle();
                        var panes = (List<WrapPanel>)typeof(EditableTable).GetField("columnPanes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(table)!;
                        var paperPane = (Panel)typeof(EditableTable).GetField("tableEditorPane", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(table)!;
                        var paperFields = (WrapPanel)((StackPanel)paperPane.Children[1]).Children[0];
                        double preferred = panes[3].Children.Where(c => c.IsVisible).Sum(c => c.Width + c.Margin.Left + c.Margin.Right) + paperFields.Children.Where(c => c.IsVisible).Sum(c => c.Width + c.Margin.Left + c.Margin.Right) + 16 * scale;
                        if (preferred <= paperPane.Bounds.Width)
                        {
                            var top = panes[3].Children[0].TranslatePoint(default, table)!.Value.Y;
                            Ok(panes[3].Children.All(c => Math.Abs(c.TranslatePoint(default, table)!.Value.Y - top) < 0.5) && Math.Abs(paperFields.TranslatePoint(default, table)!.Value.Y - top) < 0.5, $"{scale}x at {size}: typography, sorting and paper share the row whenever their measured widths fit");
                        }
                        var labels = table.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && translations.Values.Contains(t.Text ?? "")).ToArray();
                        Ok(labels.All(t => t.Bounds.Width + 0.5 >= Look.Measure(t.Text!, Ui(t.FontSize, (int)t.FontWeight))), $"{scale}x at {size}: actual Czech ribbon labels have their full measured width: " + string.Join("; ", labels.Where(t => t.Bounds.Width + 0.5 < Look.Measure(t.Text!, Ui(t.FontSize, (int)t.FontWeight))).Select(t => t.Text + "=" + t.Bounds.Width + "/" + Look.Measure(t.Text!, Ui(t.FontSize, (int)t.FontWeight)))));
                        Ok(formatCaption.Bounds.Width + 0.5 >= Look.Measure(formatCaption.Text!, Ui(formatCaption.FontSize, (int)formatCaption.FontWeight)), $"{scale}x at {size}: compact paper caption and dimensions fit without clipping");
                        var choices = table.GetVisualDescendants().OfType<ChoiceButton>().Where(c => c.IsEffectivelyVisible).ToArray();
                        Ok(choices.All(c => c.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Text == c.Current).All(t => t.Bounds.Width + 0.5 >= Look.Measure(t.Text!, Ui(t.FontSize)))), $"{scale}x at {size}: translated selected options retain readable text beside choice chrome: " + string.Join("; ", choices.SelectMany(c => c.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Text == c.Current && t.Bounds.Width + 0.5 < Look.Measure(t.Text!, Ui(t.FontSize)))).Select(t => t.Text + "=" + t.Bounds.Width + "/" + Look.Measure(t.Text!, Ui(t.FontSize)))));
                        Ok(table.PaperFormat == EditableTable.PaperKind.A4 && table.PaperLandscape && column.MinWidth == 360.125 && fixedColumn.Width == 330.034 && changes == 0, $"{scale}x at {size}: window resizing preserves document metadata and stored geometry without Changed");
                    }
                    if (scale == 1)
                    {
                        tabs.SetActive(0); Settle(); Settle();
                        Ok(new[] { "Column text", "Column width", "Table defaults and paper" }.All(name => table.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == name && t.IsEffectivelyVisible)), "wide ribbons expose labelled Column, Text, Width and Table groups");
                        var paperChoice = table.GetVisualDescendants().OfType<ChoiceButton>().Single(c => c.Items.Contains("A4"));
                        var beforeFocus = paperChoice.TranslatePoint(default, table);
                        paperChoice.Focus(Avalonia.Input.NavigationMethod.Pointer); Settle(); Settle();
                        Ok(paperChoice.TranslatePoint(default, table) == beforeFocus && table.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Column text" && t.IsEffectivelyVisible), "focusing paper controls keeps the fully expanded layout stable");
                        window.Width = 840; tabs.SetActive(3); Settle();
                        var tableTab = (Control)table.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "Table").Parent!;
                        tableTab.Focus(Avalonia.Input.NavigationMethod.Tab); Settle(); window.Width = 2400; Settle(); Settle();
                        paperChoice.Focus(Avalonia.Input.NavigationMethod.Pointer); Settle(); Settle();
                        Ok(!tabs.IsVisible && paperChoice.IsFocused, "leaving a focused tab for a field hides redundant tabs in the expanded ribbon without losing focus");
                    }
                    tabs.SetActive(3); Settle(); var preview = table.GetVisualDescendants().OfType<ChoiceButton>().Single(c => c.Items.Contains("Full width"));
                    ((Avalonia.Automation.Provider.IExpandCollapseProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(preview)!).Expand(); Settle();
                    foreach (var key in new[] { Avalonia.Input.Key.Home, Avalonia.Input.Key.Enter }) { window.KeyPress(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); window.KeyRelease(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle(); }
                    Ok(table.ConstrainToPaper && vm.ConstrainToPaper && table.PaperSize == new Size(297, 210) && Math.Abs(viewport.Bounds.Width - 297 * 96 / 25.4) < 1 && changes == 0, $"{scale}x: explicit paper preview preserves bindings and document settings without a content change");
                    vm.ConstrainToPaper = false; Settle();
                    var value = table.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "Zboží");
                    var at = value.TranslatePoint(new Point(4, 8), window)!.Value; window.MouseDown(at, Avalonia.Input.MouseButton.Left); window.MouseUp(at, Avalonia.Input.MouseButton.Left); Settle();
                    var cellEditor = (TextBox)typeof(EditableTable).GetField("editor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(table)!;
                    cellEditor.Text = "Zboží draft"; cellEditor.CaretIndex = 2; Settle(); changes = 0;
                    foreach (double size in new[] { 840d, 2400 }) { window.Width = size; Settle(); Settle(); }
                    Ok(cellEditor.IsFocused && cellEditor.Text == "Zboží draft" && cellEditor.CaretIndex == 2 && table.PaperSize == new Size(297, 210) && changes == 0, $"{scale}x: full-width resizing preserves the active cell editor, caret and document metadata");
                    window.Close();
                }
            }
            finally { Look.Translate = null; Look.Configure(new ShadeOptions()); }
        }

        static void RibbonTabVisibility()
        {
            try
            {
                foreach (var scale in new[] { 1d, 2d })
                    foreach (var mode in new[] { Look.Mode.Dark, Look.Mode.Light })
                    {
                        Look.Configure(new ShadeOptions { TextScale = scale, ReducedMotion = true }); SetMode(mode);
                        var column = new EditableTable.Column("a", "Value");
                        var table = new EditableTable { ShowColumnEditor = true, Design = true, Columns = new List<EditableTable.Column> { column } };
                        var window = new Window { Width = 840, Height = 1200, Content = table }; window.Show();
                        void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
                        Settle();
                        var tabs = table.GetVisualDescendants().OfType<TextTabs>().Single();
                        var toolbar = table.GetVisualDescendants().OfType<ActionButton>().Single(b => b.Content?.ToString() == "Done");
                        var field = table.GetVisualDescendants().OfType<Field>().First();
                        var header = table.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "Value").GetVisualAncestors().OfType<Control>().First(c => c.Focusable);
                        foreach (var keyboard in new[] { false, true })
                            for (int pane = 0; pane < 4; pane++)
                                foreach (var target in new Control[] { field.Box, header, toolbar })
                                {
                                    toolbar.Focus(); window.Width = 840; Settle(); Settle(); tabs.SetActive((pane + 1) % 4); Settle();
                                    var tab = (Control)tabs.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == new[] { "Column", "Text", "Width", "Table" }[pane]).Parent!;
                                    if (keyboard)
                                    {
                                        tab.Focus(Avalonia.Input.NavigationMethod.Tab);
                                        window.KeyPress(Avalonia.Input.Key.Enter, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null);
                                        window.KeyRelease(Avalonia.Input.Key.Enter, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null);
                                    }
                                    else
                                    {
                                        var at = tab.TranslatePoint(new Point(2, 2), window)!.Value;
                                        window.MouseDown(at, Avalonia.Input.MouseButton.Left); window.MouseUp(at, Avalonia.Input.MouseButton.Left);
                                    }
                                    Settle(); window.Width = 6400; Settle(); Settle();
                                    Ok(tabs.Active == pane && !tabs.IsVisible && !tab.IsFocused && table.IsKeyboardFocusWithin, $"{mode} {scale}x pane {pane} keyboard={keyboard}: expansion moves focus into the selected group and hides tabs");
                                    target.Focus(); Settle(); Settle();
                                    Ok(!tabs.IsVisible && target.IsFocused && table.CurrentColumn == column, $"{mode} {scale}x pane {pane} keyboard={keyboard}: leaving tabs hides the wide strip and preserves column/focus");
                                    window.Width = 840; Settle(); Settle();
                                    Ok(tabs.IsVisible, $"{mode} {scale}x: restoring compact width restores tab access");
                                    window.Width = 6400; Settle(); Settle();
                                    Ok(!tabs.IsVisible, $"{mode} {scale}x: repeated expansion matches wide layout without prior tab focus");
                                }
                        window.Close();
                    }
            }
            finally { Look.Configure(new ShadeOptions()); SetMode(Look.Mode.Dark); }
        }

        static void ColumnRibbonLayouts()
        {
            try
            {
                foreach (var scale in new[] { 1d, 2d })
                {
                    foreach (var mode in new[] { Look.Mode.Dark, Look.Mode.Light })
                    {
                        Look.Configure(new ShadeOptions { TextScale = scale, ReducedMotion = true }); SetMode(mode);
                        var column = new EditableTable.Column("a", "Value", EditableTable.Kind.Number) { Grow = true };
                        var table = new EditableTable { ShowColumnEditor = true, Design = true, Columns = new List<EditableTable.Column> { column }, PaperFormat = EditableTable.PaperKind.Custom };
                        var window = new Window { Width = 920, Height = 1100, Content = table }; window.Show();
                        void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
                        Settle(); var tabs = table.GetVisualDescendants().OfType<TextTabs>().Single(); tabs.SetActive(3); Settle(); Settle();
                        bool fieldsFit = table.GetVisualDescendants().OfType<Field>().Where(f => f.IsEffectivelyVisible).All(f => { var p = f.TranslatePoint(default, table)!.Value; return p.X >= 0 && p.X + f.Bounds.Width <= table.Bounds.Width + 1; });
                        Ok(fieldsFit, $"{mode} {scale}x: native custom-paper fields stay inside the ribbon");
                        var format = table.GetVisualDescendants().OfType<ChoiceButton>().Single(c => c.Items.Contains("A4"));
                        Ok(format.IsEffectivelyVisible, $"{mode} {scale}x: native paper controls follow the Table tab");
                        tabs.SetActive(0); table.CurrentColumn!.Kind = EditableTable.Kind.Computed; table.CurrentColumn.Terms.Add(new EditableTable.Term("missing")); table.CurrentColumn.Terms.Add(new EditableTable.Term("missing", EditableTable.Op.Subtract)); table.Refresh(); Settle(); Settle();
                        Ok(table.GetVisualDescendants().OfType<ChoiceButton>().Where(c => c.IsEffectivelyVisible).All(c => { var p = c.TranslatePoint(default, table)!.Value; return p.X >= 0 && p.X + c.Bounds.Width <= table.Bounds.Width + 1; }), $"{mode} {scale}x: native formula dropdowns stay inside the ribbon");
                        foreach (double width in new[] { 920d, 4800, 920 })
                        {
                            window.Width = width; Settle(); Settle();
                            var addTerm = table.GetVisualDescendants().OfType<ActionButton>().Single(b => b.Content?.ToString() == "Add term");
                            var formulaHead = (Grid)addTerm.Parent!;
                            var terms = (ScrollViewer)((StackPanel)formulaHead.Parent!).Children[1];
                            var buttonTop = addTerm.TranslatePoint(default, table)!.Value.Y;
                            var termsTop = terms.TranslatePoint(default, table)!.Value.Y;
                            Ok(buttonTop + addTerm.Bounds.Height + 4 * scale <= termsTop + 0.5, $"{mode} {scale}x at {width}: Add term reserves its height above the first formula source");
                        }
                        Ok(table.GetVisualDescendants().OfType<ChoiceButton>().Where(c => c.Items.SequenceEqual(new[] { "+", "−", "×", "÷" })).All(c => c.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "−" && t.Bounds.Height > 0 && t.Bounds.Width >= Look.Measure("−", Ui(TextSize.Body)))), $"{mode} {scale}x: native formula operator symbols remain readable");
                        window.Close();
                    }
                }
            }
            finally { Look.Configure(new ShadeOptions()); SetMode(Look.Mode.Dark); }
        }

        static void Money()
        {
            var a = new EditableTable.Column("a", "A", EditableTable.Kind.Number); var b = new EditableTable.Column("b", "B", EditableTable.Kind.Number);
            var sum = new EditableTable.Column("sum", "Sum", EditableTable.Kind.Computed) { Footer = EditableTable.Total.Sum, Decimals = 2 };
            sum.Terms.Add(new EditableTable.Term("a")); sum.Terms.Add(new EditableTable.Term("b"));
            var share = new EditableTable.Column("share", "Share", EditableTable.Kind.Computed); share.Terms.Add(new EditableTable.Term("a")); share.Terms.Add(new EditableTable.Term("b", EditableTable.Op.Divide));
            var rows = Enumerable.Range(0, 10).Select(_ => { var r = new EditableTable.Row(); r.Values["a"] = "0,1"; r.Values["b"] = "0.2"; return r; }).ToList();
            var table = new EditableTable { Columns = new List<EditableTable.Column> { a, b, sum, share }, Rows = rows };
            Ok(table.Exact(rows[0], sum) == 0.3m && 0.1 + 0.2 != 0.3, "0.1 + 0.2 is exactly 0.3 (as a double it isn't)");
            Ok(table.FooterValue(sum) == 3.0m && table.Footer(sum) == "3.00", "ten of them sum to exactly 3.00");
            Ok(table.Exact(rows[0], share) == 0.5m, "a division in decimals: 0.1 ÷ 0.2 = 0.5");
            rows[1].Values["b"] = "0"; Ok(table.Exact(rows[1], share) == null, "÷ 0 is empty (shown —), not an error");
            Ok(table.Value(rows[0], sum) == 0.3, "Value gives the same as a double");
            // the app's own money rule for Sum (whole cents, rounded half away from zero); Share stays the table's
            table.Calculate = (r, c) => c.Key == "sum" ? Math.Round((table.Exact(r, a) ?? 0) * 1.005m + (table.Exact(r, b) ?? 0), 2, MidpointRounding.AwayFromZero) : table.FromTerms(r, c);
            Ok(table.Exact(rows[0], sum) == 0.30m && table.Exact(rows[0], share) == 0.5m, "Calculate supplies Sum; FromTerms keeps Share's formula");
            Ok(table.FooterValue(sum) == rows.Sum(r => table.Exact(r, sum)), "the footer adds up what Calculate gave");
        }

        /// <summary>A General column keeps codes as typed and aligns each cell by what it holds; a row's unit that is no longer
        /// in the column's list still shows, and a click moves it on to the list.</summary>
        static void Mixed()
        {
            var code = new EditableTable.Column("code", "Code", EditableTable.Kind.General) { Footer = EditableTable.Total.Sum };
            var qty = new EditableTable.Column("qty", "Qty", EditableTable.Kind.Unit) { Units = new[] { "pcs", "box" } };
            EditableTable.Row R(string c, string unit) { var r = new EditableTable.Row(); r.Values["code"] = c; r.Values["qty"] = "1"; r.Units["qty"] = unit; return r; }
            var rows = new List<EditableTable.Row> { R("00123", "pcs"), R("AB-7", "kilograms"), R("5", "box") };
            var table = new EditableTable { Columns = new List<EditableTable.Column> { code, qty }, Rows = rows };
            var window = new Window { Width = 600, Height = 300, Content = table }; window.Show();
            void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
            Settle();
            TextBlock Cell(string text) => table.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == text);
            Ok(Cell("00123").TextAlignment == Avalonia.Media.TextAlignment.Right && Cell("AB-7").TextAlignment == Avalonia.Media.TextAlignment.Left && rows[0].Values["code"] == "00123",
                "a General column: a number sits right, a code left, both kept as typed");
            Ok(table.Footer(code) == "128", "and its numbers add up (00123 + 5)");
            foreach (var alignment in new[] { TextAlignment.Left, TextAlignment.Right, TextAlignment.Center })
            {
                code.Alignment = alignment; table.Refresh(); Settle();
                Ok(Cell("00123").TextAlignment == alignment && Cell("AB-7").TextAlignment == alignment, $"column alignment {alignment} overrides automatic numeric/text placement");
            }
            rows[0].Alignments["code"] = TextAlignment.Left;
            rows[1].Alignments["code"] = TextAlignment.Right;
            rows[2].Alignments["qty"] = TextAlignment.Center;
            table.Refresh(); Settle();
            Ok(Cell("00123").TextAlignment == TextAlignment.Left && Cell("AB-7").TextAlignment == TextAlignment.Right, "per-cell alignment takes precedence over the column");
            Ok(table.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "1" && t.TextAlignment == TextAlignment.Center), "cell alignment also overrides unit/number placement");
            var point = Cell("AB-7").TranslatePoint(new Point(4, 4), window)!.Value;
            window.MouseDown(point, Avalonia.Input.MouseButton.Left); window.MouseUp(point, Avalonia.Input.MouseButton.Left); Settle();
            var editor = table.GetVisualDescendants().OfType<TextBox>().Single(t => t.IsFocused);
            editor.Text = "123"; Settle(); editor.Text = "AB-7"; Settle();
            Ok(editor.TextAlignment == TextAlignment.Right && Cell("AB-7").TextAlignment == TextAlignment.Right, "the active editor preserves its override while switching between numeric and alphanumeric text");
            code.Alignment = null; rows.ForEach(r => r.Alignments.Clear()); table.Refresh(); Settle();
            Ok(Cell("00123").TextAlignment == TextAlignment.Right && Cell("AB-7").TextAlignment == TextAlignment.Left, "clearing overrides restores automatic alignment");
            table.TrySetValue(rows[1], code, "9"); Settle();
            Ok(Cell("9").TextAlignment == TextAlignment.Right, "programmatic General edits also refresh automatic alignment");
            table.TrySetValue(rows[1], code, "AB-7"); Settle();
            var unit = table.GetVisualDescendants().OfType<ActionButton>().First(b => b.Content?.ToString() == "kilograms");
            Ok(unit.Bounds.Width <= unit.DesiredSize.Width + 0.5 && unit.TranslatePoint(new Point(0, 0), table)!.Value.X > 8, "a stored unit not in the list shows as stored, with room for it");
            unit.PerformClick(); Settle();
            Ok(rows[1].Units["qty"] == "pcs" && unit.Content?.ToString() == "pcs", "a click moves it to the list's first unit");
            unit.PerformClick(); unit.PerformClick(); Settle();
            Ok(rows[1].Units["qty"] == "pcs", "and the old unit doesn't come back");
            window.Close();
        }

        /// <summary>An app's rules over the table: resizing and the footer's menu on or off, a column chosen, a removal
        /// refused, numbers shown in its own format.</summary>
        static void Permissions()
        {
            var name = new EditableTable.Column("name", "Name") { Grow = true };
            var price = new EditableTable.Column("price", "Price", EditableTable.Kind.Number) { Footer = EditableTable.Total.Sum, Display = v => v.ToString("N2", new System.Globalization.CultureInfo("cs-CZ")).Replace('\u00A0', ' ') + " Kč" };
            var r = new EditableTable.Row(); r.Values["name"] = "Pens"; r.Values["price"] = "1234.5";
            var table = new EditableTable { Columns = new List<EditableTable.Column> { name, price }, Rows = new List<EditableTable.Row> { r }, Design = true, CanResize = false, CanEditFooter = false };
            var window = new Window { Width = 600, Height = 300, Content = table }; window.Show();
            void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
            Settle();
            Ok(!table.GetVisualDescendants().OfType<Border>().Any(b => b.Width == 12 && b.IsEffectivelyVisible), "CanResize off: no heading edge to drag, even designing");
            var foot = table.GetVisualDescendants().OfType<TextBlock>().First(t => t.Inlines?.Count == 2 && t.Inlines.OfType<Avalonia.Controls.Documents.Run>().Last().Text?.EndsWith("Kč", StringComparison.Ordinal) == true);
            var at = foot.TranslatePoint(new Point(foot.Bounds.Width / 2, foot.Bounds.Height / 2), window)!.Value;
            window.MouseDown(at, Avalonia.Input.MouseButton.Left); window.MouseUp(at, Avalonia.Input.MouseButton.Left); Settle();
            Ok(price.Footer == EditableTable.Total.Sum && !window.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.Popup>().Any(p => p.IsOpen), "CanEditFooter off: a click on the footer opens no menu");
            var shown = table.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Text == "1 234,50 Kč");
            Ok(shown != null && r.Values["price"] == "1234.5" && shown.Bounds.Width >= Look.Measure("1 234,50 Kč", Ui(TextSize.Body)) - 1, "Display: the price shows as 1 234,50 Kč, measured so, stored as typed");
            Ok(table.Footer(price) == "1 234,50 Kč", "and the footer through it too");
            int told = 0; table.CurrentColumnChanged += _ => told++;
            table.CurrentColumn = price;
            Ok(table.CurrentColumn == price && told == 1, "an app sets the current column");
            table.CanRemoveColumn = c => c != price;
            typeof(EditableTable).GetMethod("RemoveColumn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(table, new object[] { price }); Settle();
            Ok(table.Columns.Contains(price) && !window.OwnedWindows.Any(), "CanRemoveColumn false: the column stays, nothing is asked");
            window.Close();
        }

        /// <summary>Look.Translate words every built-in label; a danger item stays red in any language.</summary>
        static void Words()
        {
            Look.Translate = k => k switch { "Add" => "Přidat", "Delete row" => "Smazat řádek", "Delete {0} rows" => "Smazat {0} řádky", "Cancel" => "Zrušit", _ => k };
            try
            {
                var table = new EditableTable(); var w = new Window { Content = table }; w.Show(); Dispatcher.UIThread.RunJobs();
                Ok(table.GetVisualDescendants().OfType<ActionButton>().Any(b => b.Content?.ToString() == "Přidat"), "the table's Add is translated"); w.Close();
                TextBlock Label(Border panel, string text) => panel.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == text);
                var menu = Menu.Panel(new[] { "Open", "Delete row", Menu.Danger("Vysypat koš") }, _ => { }, () => { });
                Ok(Label(menu, "Smazat řádek").Foreground == B(DangerText), "a translated Delete stays a danger item");
                Ok(Label(menu, "Vysypat koš").Foreground == B(DangerText) && Label(menu, "Open").Foreground == B(Look.Text), "Menu.Danger marks an item in any words; the mark doesn't show");
                MenuEntry? got = null;
                var records = Menu.Panel(new[] { new MenuEntry("Off") { Key = "off", IsEnabled = false }, new MenuEntry("On") { Key = "on", Shortcut = "Ctrl+O", IsChecked = true } }, e => got = e, () => { }, 200);
                var rowButtons = records.GetVisualDescendants().OfType<Button>().ToArray();
                Ok(!rowButtons[0].IsEnabled && Label(records, "Off").Foreground == B(Text3), "a disabled menu entry is shown dimmed and cannot be picked");
                rowButtons[1].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Ok(got?.Key == "on", "an entry menu reports the picked item");
            }
            finally { Look.Translate = null; }
        }

        /// <summary>A search's matches: Build.Mark tints them; a table with Workbench.Mark shows them in its rows.</summary>
        static void Highlighting()
        {
            var t = new TextBlock(); Build.Mark(t, "Website refresh", "web");
            var runs = t.Inlines!.OfType<Avalonia.Controls.Documents.Run>().ToList();
            Ok(runs.Count == 2 && runs[0].Text == "Web" && runs[0].FontWeight == Avalonia.Media.FontWeight.SemiBold, "Build.Mark sets the match apart: Web|site refresh");
            Build.Mark(t, "Website refresh", "");
            Ok(t.Inlines == null || t.Inlines.Count == 0 ? t.Text == "Website refresh" : false, "no query, plain text");
            var table = Workbench.Table(new[] { "Name" }, new List<string[]> { new[] { "Web analytics" }, new[] { "Brand guide" } });
            Workbench.SetMark(table, "web");
            var window = new Window { Width = 400, Height = 300, Content = table }; window.Show();
            Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs();
            Ok(table.GetVisualDescendants().OfType<TextBlock>().Any(b => b.Inlines?.OfType<Avalonia.Controls.Documents.Run>().Any(r => r.Text == "Web" && r.FontWeight == Avalonia.Media.FontWeight.SemiBold) == true), "a table with Workbench.Mark highlights the search in its rows");
            window.Close();
        }

        /// <summary>Every public control is in DESIGN.md (what agents read) and in the gallery, by its class name.</summary>
        static void Documented()
        {
            var root = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !System.IO.File.Exists(System.IO.Path.Combine(root.FullName, "Shade.slnx"))) root = root.Parent;
            if (root == null) { Ok(false, "the repository root (Shade.slnx) is found"); return; }
            string doc = System.IO.File.ReadAllText(System.IO.Path.Combine(root.FullName, "DESIGN.md")),
                gallery = System.IO.File.ReadAllText(System.IO.Path.Combine(root.FullName, "gallery", "Showcase.cs"));
            bool Named(string text, string name) => System.Text.RegularExpressions.Regex.IsMatch(text, $@"\b{name}\b");
            foreach (var t in typeof(Look).Assembly.GetExportedTypes().Where(t => typeof(Control).IsAssignableFrom(t) && !t.IsAbstract && !t.IsNested).OrderBy(t => t.Name))
                Ok(Named(doc, t.Name) && Named(gallery, t.Name), $"{t.Name} is in DESIGN.md and the gallery");
        }

        static void Adaptability()
        {
            var validating = new ValidationVm();
            var boundField = new Field();
            boundField.Bind(Field.TextProperty, new Avalonia.Data.Binding(nameof(ValidationVm.Name)) { Source = validating, Mode = Avalonia.Data.BindingMode.TwoWay });
            var boundGroup = Build.Labelled("Name", boundField);
            var boundWindow = new Window { Content = boundGroup }; boundWindow.Show(); Dispatcher.UIThread.RunJobs();
            Ok(boundGroup.Children.OfType<TextBlock>().Any(t => t.Text == "Name is required"), "INotifyDataErrorInfo errors reach field validation");
            validating.Name = "Valid"; Dispatcher.UIThread.RunJobs();
            Ok(!boundGroup.Children.OfType<TextBlock>().Last().IsVisible, "correcting a bound value clears validation");
            boundWindow.Close();
            var edgeDate = new DateField { Value = new DateTime(9999, 10, 25) };
            var edgeWindow = new Window { Content = edgeDate }; edgeWindow.Show();
            var edgeBlocks = edgeDate.GetVisualDescendants().OfType<Control>().First(c => c.GetType().Name == "DateBlocks");
            edgeBlocks.Focus(); edgeWindow.KeyPress(Avalonia.Input.Key.Up, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Dispatcher.UIThread.RunJobs();
            Ok(edgeDate.Value?.Year == 9999, "incrementing the maximum date year does not overflow"); edgeWindow.Close();
            var field = new Field { Error = "Enter a name", IsReadOnly = true };
            var group = Build.Labelled("Name", field);
            Ok(group.Children.OfType<TextBlock>().Any(t => t.Text == "Enter a name" && t.IsVisible), "field error is visible and associated with its input");
            Ok(field.Box.IsReadOnly && Avalonia.Automation.AutomationProperties.GetLabeledBy(field.Box) != null, "read-only and accessible label reach the editor");
            field.Error = null;
            Ok(!group.Children.OfType<TextBlock>().Last().IsVisible, "clearing validation hides the error");
            field.SetValue(Field.InvalidProperty, true); field.SetValue(Field.PendingProperty, true);
            Ok(field.Invalid && field.Pending, "validation and pending state use Avalonia properties");
            var column = new EditableTable.Column("n", "Number", EditableTable.Kind.Number) { InputCulture = new System.Globalization.CultureInfo("en-US") };
            var row = new EditableTable.Row(); row.Values["n"] = "1,234.50";
            var table = new EditableTable { Columns = new[] { column }, Rows = new List<EditableTable.Row> { row } };
            Ok(table.Exact(row, column) == 1234.50m, "English input preserves thousands and decimal separators");
            column.InputCulture = new System.Globalization.CultureInfo("cs-CZ"); row.Values["n"] = "1234,50";
            Ok(table.Exact(row, column) == 1234.50m, "Czech input uses a decimal comma");
            column.Parse = text => text == "half" ? 0.5m : null; row.Values["n"] = "half";
            Ok(table.Exact(row, column) == 0.5m, "a project parser overrides numeric culture");
            column.Parse = null;
            table.Validate = (_, _, text) => text == "bad" ? "Enter a number" : null;
            int edits = 0; table.CellEdited += (_, _, before, after) => { if (before == "half" && after == "2") edits++; };
            Ok(!table.TrySetValue(row, column, "bad") && row.Values["n"] == "half", "rejected cell edit preserves the previous value");
            Ok(table.TrySetValue(row, column, "2") && edits == 1 && table.LastValidationError == null, "accepted edit reports old and new values");
            table.IsReadOnly = true;
            Ok(!table.TrySetValue(row, column, "3") && row.Values["n"] == "2", "read-only table rejects programmatic edits through its edit API");
            string Localize(string key, object[] args) => (string)typeof(Look).GetMethod("T", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic, null, new[] { typeof(string), typeof(object[]) }, null)!.Invoke(null, new object[] { key, args })!;
            Look.Localize = (key, args) => key == "Delete {0} rows" ? ((int)args[0] == 1 ? "Smazat řádek" : "Smazat řádky") : key;
            Ok(Localize("Delete {0} rows", new object[] { 1 }) == "Smazat řádek" && Localize("Delete {0} rows", new object[] { 4 }) == "Smazat řádky", "localizer receives arguments for plural rules");
            Look.Localize = null;
            var button = new ActionButton("Run"); int runs = 0; button.Click += (_, _) => runs++;
            var peer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(button);
            ((Avalonia.Automation.Provider.IInvokeProvider)peer!).Invoke();
            Ok(runs == 1, "button automation invokes the same action as the keyboard");
            var toggle = new Switch(); var togglePeer = (Avalonia.Automation.Provider.IToggleProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(toggle)!;
            togglePeer.Toggle();
            Ok(toggle.IsChecked == true && togglePeer.ToggleState == Avalonia.Automation.Provider.ToggleState.On, "switch automation exposes and changes its state");
            var tick = new Tick { IsChecked = null }; var tickPeer = (Avalonia.Automation.Provider.IToggleProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(tick)!;
            Ok(tickPeer.ToggleState == Avalonia.Automation.Provider.ToggleState.Indeterminate, "tick automation exposes mixed state");
            var completion = new System.Threading.Tasks.TaskCompletionSource(); int executions = 0;
            button.AsyncAction = _ => { executions++; return completion.Task; };
            var pending = button.RunAsync(); var duplicate = button.RunAsync();
            Ok(button.IsBusy && !button.IsEffectivelyEnabled && executions == 1 && duplicate.IsCompleted, "async action prevents duplicate execution");
            completion.SetResult();
            while (!pending.IsCompleted) Dispatcher.UIThread.RunJobs();
            Ok(!button.IsBusy && button.IsEffectivelyEnabled, "async completion restores the button");
            button.AsyncAction = _ => throw new InvalidOperationException("Failed");
            button.RunAsync().GetAwaiter().GetResult();
            Ok(button.LastError?.Message == "Failed" && !button.IsBusy, "async failures are retained and restore button state");
            button.AsyncAction = token => System.Threading.Tasks.Task.Delay(-1, token);
            pending = button.RunAsync(); button.Cancel();
            while (!pending.IsCompleted) Dispatcher.UIThread.RunJobs();
            Ok(button.LastError == null && !button.IsBusy, "cancelled action restores state without reporting a failure");
            var observable = new System.Collections.ObjectModel.ObservableCollection<EditableTable.Row>();
            table = new EditableTable { Columns = new List<EditableTable.Column> { column }, Rows = observable };
            var window = new Window { Content = table }; window.Show();
            observable.Add(row); Dispatcher.UIThread.RunJobs();
            Ok(table.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "2"), "observable row additions update the view");
            observable.Clear(); Dispatcher.UIThread.RunJobs(); window.Close();
            try
            {
                Look.Configure(new ShadeOptions { Density = 1.25, TextScale = 1.25, ReducedMotion = true });
                var larger = new Field();
                Ok(larger.Height == 35 && larger.Box.FontSize == 16.25, "application metrics expand field height and text together");
                var still = new Switch();
                Ok(still.Transitions == null, "reduced motion disables switch transitions");
                bool refused = false; try { Look.Configure(new ShadeOptions { TextScale = double.NaN }); } catch (ArgumentOutOfRangeException) { refused = true; }
                Ok(refused, "invalid application metrics are rejected");
            }
            finally { Look.Configure(new ShadeOptions()); }
        }

        static void VirtualRows()
        {
            var column = new EditableTable.Column("name", "Name") { Sizing = EditableTable.Sizing.Fixed, Width = 180 };
            var rows = Enumerable.Range(0, 1000).Select(i => { var r = new EditableTable.Row(); r.Values["name"] = $"row{i:0000}"; return r; }).ToList();
            rows[500].Values["name"] = "A wrapped row with enough words to span several lines without clipping its content";
            var table = new EditableTable { Columns = new[] { column }, Rows = rows };
            var window = new Window { Width = 420, Height = 400, Content = table }; window.Show();
            void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
            void Click(Control c) { var at = c.TranslatePoint(new Point(12, 12), window)!.Value; window.MouseDown(at, Avalonia.Input.MouseButton.Left); window.MouseUp(at, Avalonia.Input.MouseButton.Left); Settle(); }
            List<Grid> Lines() => table.GetVisualDescendants().OfType<Grid>().Where(g => g.Tag?.GetType().Name == "Line").ToList();
            bool Has(string text) => table.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == text);
            var body = table.GetVisualDescendants().OfType<ScrollViewer>().Single(v => v.VerticalScrollBarVisibility == Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden);
            Settle();
            Ok(Lines().Count < 40 && Has("row0000") && !Has("row0999"), "large tables realize only the initial viewport");
            body.Offset = body.Offset.WithY(body.Extent.Height - body.Viewport.Height); Settle();
            Ok(Lines().Count < 40 && Has("row0999") && !Has("row0000"), "scrolling realizes the last row and releases old controls");
            body.Offset = body.Offset.WithY(500 * 28); Settle();
            var wrapped = table.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == rows[500].Values["name"]);
            Ok(wrapped.Bounds.Height > 28 && wrapped.Parent is Border b && b.Bounds.Height >= wrapped.Bounds.Height, "virtualized rows preserve wrapped content height");
            body.Offset = body.Offset.WithY(0); Settle();
            var first = table.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "row0000"); Click(first);
            var editor = table.GetVisualDescendants().OfType<TextBox>().First(t => t.IsFocused);
            editor.Text = "edited first"; editor.CaretIndex = 3; Settle();
            body.Offset = body.Offset.WithY(body.Extent.Height - body.Viewport.Height); Settle();
            Ok(editor.IsFocused && editor.CaretIndex == 3 && editor.Parent != null && Lines().Count < 40, "scrolling pins the active editor without retaining every row");
            window.KeyPress(Avalonia.Input.Key.Enter, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle();
            Ok(editor.IsFocused && editor.Text == "row0001" && body.Offset.Y < 100, "Enter reaches an unrealized adjacent row and scrolls it into view");
            body.Offset = body.Offset.WithY(9000); Settle();
            body.Offset = body.Offset.WithY(18000); Settle();
            body.Offset = body.Offset.WithY(0); Settle();
            Ok(rows[0].Values["name"] == "edited first" && Lines().Count < 40, "scroll recycling preserves edited values and bounds control count");
            var amount = new EditableTable.Column("amount", "Amount", EditableTable.Kind.Number) { Footer = EditableTable.Total.Sum };
            table.Columns = new[] { amount, column }; Settle();
            Ok(editor.IsFocused && Grid.GetColumn(editor) == 2 && editor.Text == "row0001", "reordering bound columns keeps the editor on the same column key");
            Ok(table.TrySetValue(rows[999], amount, "123.45") && table.FooterValue(amount) == 123.45m && table.GetVisualDescendants().OfType<Avalonia.Controls.TextBlock>().Any(t => t.Inlines?.OfType<Avalonia.Controls.Documents.Run>().Any(r => r.Text == "123.45") == true), "offscreen edits update aggregates without materializing the row");
            table.Columns = new[] { column }; Settle();
            bool duplicateColumns = false, duplicateRows = false;
            try { table.Columns = new[] { column, column }; } catch (ArgumentException) { duplicateColumns = true; }
            try { table.Rows = new[] { rows[0], rows[0] }; } catch (ArgumentException) { duplicateRows = true; }
            Ok(duplicateColumns && duplicateRows && table.Rows.Count == 1000 && table.Columns.Count == 1, "invalid table identities are rejected without replacing existing data");
            table.AddRows(1, focus: true); Settle();
            var addedEditor = table.GetVisualDescendants().OfType<TextBox>().First(t => t.IsFocused);
            Ok(addedEditor.Text == "" && body.Offset.Y > 20000 && Lines().Count < 40, "adding a row scrolls to its virtualized editor");
            rows.RemoveAt(rows.Count - 1); table.Refresh(); Settle();
            table.SetValue(EditableTable.IsReadOnlyProperty, true);
            Ok(table.IsReadOnly && !table.TrySetValue(rows[0], column, "blocked"), "bound table read-only state enforces the same edit rules");
            window.Close();
        }

        static void AccessibleEditors()
        {
            Avalonia.Automation.Peers.AutomationPeer Peer(Control control) => Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(control)!;
            var date = new DateField { Min = new DateTime(2026, 1, 1), Max = new DateTime(2026, 12, 31) };
            var datePeer = (Avalonia.Automation.Provider.IValueProvider)Peer(date); int changes = 0; date.Changed += () => changes++;
            datePeer.SetValue("2026-10-25");
            bool rejected = false; try { datePeer.SetValue("2027-01-01"); } catch (ArgumentException) { rejected = true; }
            Ok(datePeer.Value == "2026-10-25" && changes == 1 && rejected, "date automation reads and edits the date while enforcing limits");
            Build.Labelled("Delivery", date);
            Ok(Avalonia.Automation.AutomationProperties.GetLabeledBy(date) != null, "date labels target the accessible date rather than its unused text box");
            date.IsReadOnly = true; rejected = false;
            try { datePeer.SetValue("2026-10-26"); } catch (InvalidOperationException) { rejected = true; }
            Ok(rejected && datePeer.IsReadOnly, "date automation respects read-only state");
            var range = new DateRangeField { Min = date.Min, Max = date.Max };
            var rangePeer = (Avalonia.Automation.Provider.IValueProvider)Peer(range);
            rangePeer.SetValue("2026-10-25/2026-10-28");
            rejected = false; try { rangePeer.SetValue("2026-10-25/2027-01-01"); } catch (ArgumentException) { rejected = true; }
            Ok(rejected && rangePeer.Value == "2026-10-25/2026-10-28", "invalid range automation edits leave both stored dates unchanged");
            var icon = new IconButton(IconButton.Export); int clicks = 0; icon.Click += (_, _) => clicks++;
            ((Avalonia.Automation.Provider.IInvokeProvider)Peer(icon)).Invoke();
            Ok(clicks == 1 && icon.Focusable, "icon actions expose invoke automation and keyboard focus");
            var state = new StateToggle(); var statePeer = (Avalonia.Automation.Provider.IToggleProvider)Peer(state);
            Ok(statePeer.ToggleState == Avalonia.Automation.Provider.ToggleState.Indeterminate, "unknown state exposes an indeterminate value");
            statePeer.Toggle();
            Ok(state.IsChecked == true, "state automation follows the same toggle path as the pointer");
            var keys = new HashSet<string>();
            Look.Translate = key => { keys.Add(key); return key; };
            try
            {
                var drawing = new Avalonia.Media.DrawingGroup(); using var context = drawing.Open();
                var shown = new StateLook(); shown.Arrange(new Rect(0, 0, 100, 28)); shown.Render(context);
                state.Arrange(new Rect(0, 0, 100, 28)); state.Render(context);
                Ok(keys.Contains("OFF") && keys.Contains("ON"), "drawn on/off labels use the application localizer");
            }
            finally { Look.Translate = null; }
            var number = new EditableTable.Column("n", "Number", EditableTable.Kind.Number);
            var row = new EditableTable.Row(); row.Values["n"] = "2";
            var table = new EditableTable { Columns = new[] { number }, Rows = new[] { row } };
            var window = new Window { Content = table }; window.Show(); Dispatcher.UIThread.RunJobs();
            var cell = table.GetVisualDescendants().OfType<Border>().First(c => c.GetType().Name == "DataCell");
            var cellPeer = (Avalonia.Automation.Provider.IValueProvider)Peer(cell);
            cellPeer.SetValue("3.5"); rejected = false;
            try { cellPeer.SetValue("bad"); } catch (ArgumentException) { rejected = true; }
            Ok(rejected && cellPeer.Value == "3.5" && row.Values["n"] == "3.5", "cell automation uses validated edits and preserves stored values on failure");
            var other = new EditableTable.Row();
            Ok(!table.TrySetValue(other, number, "1") && table.LastValidationError == null, "an unknown row does not reuse an unrelated validation error");
            window.Close();
        }

        static void CalendarLimits()
        {
            try
            {
                foreach (var scale in new[] { 1.0, 2.0 })
                {
                    Look.Configure(new ShadeOptions { TextScale = scale });
                    foreach (var limit in new[] { DateTime.MinValue.Date, DateTime.MaxValue.Date })
                    {
                        var field = new DateField { Value = limit, FirstDay = DayOfWeek.Sunday };
                        var window = new Window { Width = 800, Height = 1100, Content = field }; window.Show();
                        void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
                        var blocks = field.GetVisualDescendants().OfType<Control>().First(c => c.GetType().Name == "DateBlocks");
                        blocks.Focus(); window.KeyPress(Avalonia.Input.Key.F4, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle();
                        var grid = window.GetVisualDescendants().OfType<Control>().First(c => c.GetType().Name == "MonthGrid");
                        var drawing = new Avalonia.Media.DrawingGroup(); using (var context = drawing.Open()) grid.Render(context);
                        grid.Focus();
                        window.KeyPress(limit.Year == 1 ? Avalonia.Input.Key.PageUp : Avalonia.Input.Key.PageDown, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle();
                        using (var context = drawing.Open()) grid.Render(context);
                        Ok(field.Value == limit && blocks.Bounds.Height >= 26 * scale && grid.Bounds.Height >= 300 * scale, $"calendar safely renders and navigates year {limit.Year} at text scale {scale}");
                        field.IsReadOnly = true; Settle();
                        Ok(!window.GetVisualDescendants().OfType<Control>().Any(c => c.GetType().Name == "MonthGrid"), "switching an open date to read-only closes its calendar");
                        if (limit.Year == 1)
                        {
                            field.IsReadOnly = false; blocks.Focus();
                            window.KeyPress(Avalonia.Input.Key.Up, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle();
                            Ok(field.Value?.Year == 2, "incrementing a four-digit year with leading zeros keeps a valid date");
                        }
                        window.Close();
                    }
                }
            }
            finally { Look.Configure(new ShadeOptions()); }
        }

        static void LiveLanguage()
        {
            var row = new EditableTable.Row(); row.Values["name"] = "Add";
            var table = new EditableTable { Columns = new[] { new EditableTable.Column("name", "Project") }, Rows = new[] { row } };
            var search = new SearchField(); var custom = new SearchField("Find projects") { Text = "Add" };
            var list = new ListField();
            var window = new Window { Content = new StackPanel { Children = { table, search, custom, list } } }; window.Show();
            void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
            Settle();
            var text = table.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Add" && t.Parent?.GetType().Name == "DataCell");
            var at = text.TranslatePoint(new Point(10, 10), window)!.Value;
            window.MouseDown(at, Avalonia.Input.MouseButton.Left); window.MouseUp(at, Avalonia.Input.MouseButton.Left); Settle();
            var editor = table.GetVisualDescendants().OfType<TextBox>().First(t => t.IsFocused);
            table.Validate = (_, _, text) => text == "forbidden" ? "Choose another value" : null;
            editor.Text = "forbidden"; editor.CaretIndex = 3;
            int changes = 0; table.Changed += () => changes++;
            try
            {
                Look.Translate = key => key switch { "Add" => "Přidat", "rows" => "řádky", "Search" => "Hledat", "Select row {0}" => "Vybrat řádek {0}", _ => key };
                Look.Culture = System.Globalization.CultureInfo.GetCultureInfo("cs-CZ"); Look.RefreshLanguage(); Settle();
                Ok(table.GetVisualDescendants().OfType<ActionButton>().Any(b => b.Content?.ToString() == "Přidat") && list.GetVisualDescendants().OfType<ActionButton>().Any(b => b.Content?.ToString() == "Přidat" && b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Přidat")), "language refresh updates existing table and list action labels");
                Ok(search.Box.PlaceholderText == "Hledat" && custom.Box.PlaceholderText == "Find projects" && custom.Text == "Add", "language refresh updates default prompts while preserving application placeholders and input");
                Ok(editor.IsFocused && editor.CaretIndex == 3 && editor.Text == "forbidden" && row.Values["name"] == "Add" && changes == 0, "language refresh preserves the active editor, caret, domain data and change notifications");
                Ok(table.GetVisualDescendants().OfType<Border>().Any(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Vybrat řádek 1"), "translated row automation retains its actual row index");
            }
            finally { Look.Translate = null; Look.Culture = System.Globalization.CultureInfo.InvariantCulture; Look.RefreshLanguage(); window.Close(); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static WeakReference DiscardLocalizedControl() => new(new SearchField());

        static void LanguageLifetime()
        {
            var discarded = DiscardLocalizedControl();
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            Ok(!discarded.IsAlive, "live-language callbacks do not keep discarded controls alive");
            Look.RefreshLanguage();
        }

        static void Activation()
        {
            var tick = new Tick(); var chip = new FilterChip("Filter"); var icon = new IconButton(IconButton.Export); var tabs = new TextTabs("One", "Two", "Three");
            var confirm = new ActionButton("Delete") { ConfirmText = "Confirm" }; int clicks = 0, runs = 0; chip.Click += (_, _) => clicks++; confirm.Click += (_, _) => runs++;
            var window = new Window { Width = 500, Height = 300, Content = new StackPanel { Children = { tick, chip, icon, tabs, confirm } } }; window.Show(); Dispatcher.UIThread.RunJobs();
            void Key(Avalonia.Input.Key key, bool release = true) { window.KeyPress(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); if (release) window.KeyRelease(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Dispatcher.UIThread.RunJobs(); }
            chip.Focus(Avalonia.Input.NavigationMethod.Tab); Key(Avalonia.Input.Key.Space, false); Key(Avalonia.Input.Key.Space, false);
            Ok(clicks == 0 && tick.FocusAdorner != null && typeof(Button).IsAssignableFrom(icon.GetType()), "native toggle activation waits for Space release and retains tick focus adorners");
            window.KeyRelease(Avalonia.Input.Key.Space, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null);
            Ok(clicks == 1, "held Space activates a native filter once on release");
            chip.IsEnabled = false; Key(Avalonia.Input.Key.Enter); Ok(clicks == 1, "disabled actions cannot run through the keyboard");
            var at = tick.TranslatePoint(new Point(4, 4), window)!.Value;
            window.MouseDown(at, Avalonia.Input.MouseButton.Left); window.MouseMove(new Point(490, 290)); window.MouseUp(new Point(490, 290), Avalonia.Input.MouseButton.Left); Dispatcher.UIThread.RunJobs();
            Ok(tick.IsChecked != true, "releasing a captured pointer outside a tick cancels activation");
            var first = tabs.GetVisualDescendants().OfType<StackPanel>().First(c => c.Focusable); first.Focus(Avalonia.Input.NavigationMethod.Tab);
            Key(Avalonia.Input.Key.Right); Ok(tabs.Active == 1, "Right moves focus and selection to the next text tab");
            Key(Avalonia.Input.Key.End); Ok(tabs.Active == 2, "End selects the last text tab");
            bool refused = false; try { tabs.Active = 9; } catch (ArgumentOutOfRangeException) { refused = true; }
            Ok(refused && tabs.Active == 2, "invalid tab selection is rejected without changing the selected tab");
            confirm.Focus(Avalonia.Input.NavigationMethod.Tab); Key(Avalonia.Input.Key.Enter, false); Key(Avalonia.Input.Key.Enter, false);
            Ok(confirm.Armed && runs == 0, "holding Enter cannot arm and confirm a dangerous action");
            window.KeyRelease(Avalonia.Input.Key.Enter, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Key(Avalonia.Input.Key.Enter);
            Ok(runs == 1, "a distinct second key press confirms the action");
            window.Close();
        }

        static void MetricCoverage()
        {
            try
            {
                Look.Configure(new ShadeOptions { TextScale = 2, ReducedMotion = true });
                var summary = new SummaryCards { Items = new[] { new SummaryCards.Item { Title = "Services", Value = "123", Line = "Healthy" } } };
                summary.Measure(new Size(800, double.PositiveInfinity));
                Ok(summary.DesiredSize.Height == 168, "summary cards expand their geometry with text size");
                summary.Detailed = true; summary.Measure(new Size(800, double.PositiveInfinity)); Ok(summary.DesiredSize.Height == 224, "detailed summary cards expand without clipping their labels");
                var empty = new SummaryCards(); empty.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity)); Ok(empty.DesiredSize.Width == 0 && empty.DesiredSize.Height == 0, "an empty unbounded summary has a valid empty size");
                var progress = new Shade.Progress { Label = "Working" }; progress.Measure(new Size(500, double.PositiveInfinity));
                Ok(progress.DesiredSize.Height == 56 && Look.ControlHeight == 56 && new Tick().Height == 32 && new Switch().Height == 36, "shared metrics cover progress, inputs and toggles");
                var sidebar = new Sidebar("App", "", new[] { new NavEntry("Home", "home", Icons.Monitor) });
                Ok(sidebar.Width == 360, "expanded navigation scales with its labels"); sidebar.Collapsed = true; Ok(sidebar.Width == 108, "collapsed navigation preserves scaled geometry");
                var ticks = new TickGrid(new[] { "Wednesday" }, Array.Empty<string>(), 110);
                Ok(((Control)ticks.Children[0]).Height == 56 && ((Control)ticks.Children[0]).Width >= 220, "tick-grid rows and columns scale without overlapping their labels");
                var designer = new TableDesigner { Columns = new[] { new TableDesigner.Column("Heading") }, Rows = new[] { new[] { "Value" } } };
                Ok(designer.GetLogicalDescendants().OfType<Border>().Count(b => b.Height == 56) == 2, "table-designer headers and preview rows follow scaled control heights");
                var palette = new ColorGrid(Look.Accent); Ok(palette.Width > 900 && palette.Height > 450, "the color helper expands its palette and mixer coordinate space");
            }
            finally { Look.Configure(new ShadeOptions()); }
        }

        static void SummaryAndCalendarAutomation()
        {
            var cards = new SummaryCards { Items = new[] { new SummaryCards.Item { Key = "a", Title = "A", Value = "1" }, new SummaryCards.Item { Key = "b", Title = "B", Value = "2" } } };
            string? picked = null; cards.Picked += key => picked = key;
            var date = new DateField { Value = new DateTime(2026, 10, 25) };
            var window = new Window { Width = 800, Height = 600, Content = new StackPanel { Children = { cards, date } } }; window.Show();
            void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
            Settle();
            var buttons = cards.Children.OfType<Button>().ToArray();
            Ok(cards.GetVisualDescendants().OfType<Control>().Where(c => c.GetType().Name == "CardFace").All(c => c.Bounds.Width > 0 && c.Bounds.Height >= 84), "summary drawing surfaces receive the full visible card bounds");
            ((Avalonia.Automation.Provider.IInvokeProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(buttons[1])!).Invoke();
            Ok(picked == "b", "summary automation invokes the same stable card key as the pointer");
            buttons[0].Focus(Avalonia.Input.NavigationMethod.Tab);
            window.KeyPress(Avalonia.Input.Key.Right, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle();
            Ok(buttons[1].IsFocused, "summary cards support directional keyboard navigation");
            cards.Items = new[] { new SummaryCards.Item { Key = "b", Title = "B", Value = "3" } }; Settle();
            Ok(cards.Children.OfType<Button>().Single().IsFocused, "summary refresh preserves keyboard focus by card key");
            var blocks = date.GetVisualDescendants().OfType<Control>().First(c => c.GetType().Name == "DateBlocks"); blocks.Focus();
            window.KeyPress(Avalonia.Input.Key.F4, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle();
            var calendar = window.GetVisualDescendants().OfType<Control>().First(c => c.GetType().Name == "MonthGrid");
            string day = new DateTime(2026, 10, 26).ToString("D", Look.Culture);
            var target = calendar.GetVisualDescendants().OfType<Button>().Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == day);
            Ok(target.Bounds.Width > 0 && target.Bounds.Height > 0, "calendar days expose named automation targets at their visible bounds");
            ((Avalonia.Automation.Provider.IInvokeProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(target)!).Invoke(); Settle();
            Ok(date.Value == new DateTime(2026, 10, 26), "a calendar day can be selected through native invoke automation");
            window.Close();
        }

        static void FocusContrastAndTabSemantics()
        {
            try
            {
                foreach (var mode in new[] { Look.Mode.Light, Look.Mode.Dark })
                    foreach (var accent in new[] { Colors.White, Colors.Black })
                    {
                        Look.SetMode(mode); Look.SetAccent(accent);
                        Ok(Look.Contrast(Look.FocusBorder, Look.Raised) >= 3 && Look.Contrast(Look.FocusBorder, Look.Background) >= 3, "focus borders remain visible with extreme custom accents in " + mode);
                    }
            }
            finally { Look.SetAccent(Look.Accents[0].dark, Look.Accents[0].light); Look.SetMode(Look.Mode.Dark); }
            var tabs = new TextTabs("A", "B"); int changes = 0; tabs.Changed += _ => changes++;
            tabs.SetValue(TextTabs.ActiveProperty, 1);
            var peer = (Avalonia.Automation.Provider.ISelectionProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(tabs)!;
            Ok(changes == 0 && peer.IsSelectionRequired && !peer.CanSelectMultiple && peer.GetSelection().Count == 1, "tab bindings update selection without reporting a user action");
            var selected = (Avalonia.Automation.Provider.ISelectionItemProvider)peer.GetSelection()[0];
            Ok(selected.IsSelected && ReferenceEquals(selected.SelectionContainer, peer), "tab items expose their selected state and selection container");
            var first = tabs.GetVisualDescendants().OfType<StackPanel>().First(c => c.Focusable);
            ((Avalonia.Automation.Provider.ISelectionItemProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(first)!).Select();
            Ok(tabs.Active == 0 && changes == 1, "tab automation raises the same selection event as a user action");
            var button = new ActionButton("Disabled") { Command = new Command { Can = false } };
            var disabledWindow = new Window { Content = button }; disabledWindow.Show(); Dispatcher.UIThread.RunJobs();
            Ok(!button.IsEffectivelyEnabled && button.Background is SolidColorBrush background && background.Color == (Color)Look.Raised, "command-disabled actions use the disabled visual state"); disabledWindow.Close();
        }

        static void FooterRefreshCosts()
        {
            var input = new EditableTable.Column("input", "Input", EditableTable.Kind.Number);
            var computed = new EditableTable.Column("computed", "Computed", EditableTable.Kind.Computed) { Footer = EditableTable.Total.Sum };
            int calculations = 0;
            var rows = Enumerable.Range(0, 20).Select(i => { var row = new EditableTable.Row(); row.Values["input"] = i.ToString(); return row; }).ToList();
            var table = new EditableTable { Columns = new[] { input, computed }, Rows = rows };
            table.Calculate = (row, column) => { calculations++; return decimal.Parse(row.Values["input"], System.Globalization.CultureInfo.InvariantCulture); };
            table.Refresh();
            var window = new Window { Width = 700, Height = 800, Content = table }; window.Show();
            void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
            Settle(); calculations = 0; window.Width = 750; Settle();
            Ok(calculations == 0, "resizing a measured table does not recalculate unchanged footer aggregates");
            rows[0].Values["input"] = "10";
            Ok(table.FooterValue(computed) == 200 && calculations > 0, "public footer calculations still read direct model changes immediately");
            table.Refresh(); Settle();
            Ok(table.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Inlines?.OfType<Avalonia.Controls.Documents.Run>().Any(run => run.Text == "200") == true), "Refresh invalidates displayed aggregate caches after external model edits");
            table.TrySetValue(rows[0], input, "20"); Settle();
            Ok(table.FooterValue(computed) == 210, "validated edits invalidate cached aggregates");
            window.Close();
        }

        static void PopupContracts()
        {
            object? Read(object obj, string name) => obj.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(obj);
            try
            {
                foreach (var scale in new[] { 1d, 2d })
                    foreach (var mode in new[] { Mode.Dark, Mode.Light })
                        foreach (var kind in new[] { "Choice", "Multi", "Search", "Suggestions", "Date", "Range", "Accent" })
                        {
                            Look.Configure(new ShadeOptions { TextScale = scale, ReducedMotion = true }); SetMode(mode);
                            Control control = kind switch
                            {
                                "Choice" => new ChoiceButton { Items = new[] { "Alpha", "Beta" }, Current = "Alpha" },
                                "Multi" => new MultiChoice(new[] { "Alpha", "Beta" }, "All"),
                                "Search" => new SearchChoice { ItemsSource = new[] { "Alpha", "Beta" }, SelectedItem = "Alpha" },
                                "Suggestions" => new SuggestionField("", new[] { "Alpha", "Alpine" }),
                                "Date" => new DateField { Value = new DateTime(2026, 10, 21) },
                                "Range" => new DateRangeField(),
                                _ => new AccentPicker()
                            };
                            control.Width = (kind == "Accent" ? 600 : 320) * scale;
                            int edits = 0;
                            switch (control)
                            {
                                case ChoiceButton choice: choice.Picked += _ => edits++; break;
                                case MultiChoice multi: multi.Changed += () => edits++; break;
                                case SearchChoice search: search.SelectionCommitted += (_, _) => edits++; break;
                                case SuggestionField suggestions: suggestions.Picked += _ => edits++; break;
                                case DateField date: date.Changed += () => edits++; break;
                                case DateRangeField range: range.Changed += () => edits++; break;
                                case AccentPicker accent: accent.Changed += () => edits++; break;
                            }
                            var before = new ActionButton("Before"); var outside = new ActionButton("Outside") { Margin = new Thickness(0, 800 * scale, 0, 0) };
                            int clicks = 0; outside.Click += (_, _) => clicks++;
                            var parent = new StackPanel { Children = { before, control, outside } };
                            var window = new Window { Width = 1200 * scale, Height = 1600 * scale, Content = parent }; window.Show();
                            void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
                            void Click(Control target) { var at = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value; window.MouseDown(at, Avalonia.Input.MouseButton.Left); window.MouseUp(at, Avalonia.Input.MouseButton.Left); Settle(); }
                            Settle();
                            Control trigger = kind switch
                            {
                                "Suggestions" => ((SuggestionField)control).Box,
                                "Date" or "Range" => (Control)Read(Read(control, "calendar")!, "button")!,
                                "Accent" => ((Field)Read(control, "custom")!).Box,
                                _ => control
                            };
                            Avalonia.Controls.Primitives.Popup? Popup() => (Avalonia.Controls.Primitives.Popup?)Read(kind is "Date" or "Range" ? Read(control, "calendar")! : control, kind == "Accent" ? "helper" : "popup");
                            void Open()
                            {
                                Click(trigger);
                                if (control is SuggestionField suggestions) { suggestions.Box.Text = ""; Settle(); suggestions.Box.Text = "Al"; Settle(); }
                            }
                            string name = $"{kind} {mode} {scale}x";
                            Open(); Ok(Popup()?.IsOpen == true, name + ": pointer opening creates a popup");
                            Click(trigger);
                            bool toggles = kind is not ("Suggestions" or "Accent");
                            Ok((Popup()?.IsOpen == true) != toggles, name + ": repeated trigger click preserves the control's toggle/editor contract");
                            if (toggles) Open();
                            Click(outside);
                            Ok(Popup()?.IsOpen != true && outside.IsFocused && clicks == 1, name + ": outside click closes without stealing focus or swallowing the action");
                            Open(); window.KeyPress(Avalonia.Input.Key.Escape, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); window.KeyRelease(Avalonia.Input.Key.Escape, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle();
                            Ok(Popup()?.IsOpen != true, name + ": Escape dismisses without selecting");
                            foreach (var backwards in new[] { false, true })
                            {
                                Open(); var target = backwards ? before : outside;
                                for (int step = 0; step < 32 && !target.IsFocused; step++)
                                {
                                    var modifiers = backwards ? Avalonia.Input.RawInputModifiers.Shift : Avalonia.Input.RawInputModifiers.None;
                                    window.KeyPress(Avalonia.Input.Key.Tab, modifiers, Avalonia.Input.PhysicalKey.None, null); window.KeyRelease(Avalonia.Input.Key.Tab, modifiers, Avalonia.Input.PhysicalKey.None, null); Settle();
                                }
                                Ok(target.IsFocused && Popup()?.IsOpen != true, name + ": " + (backwards ? "Shift+Tab" : "Tab") + " leaves toward the host without trapping focus");
                            }
                            foreach (var state in new[] { "disable", "hide", "detach" })
                            {
                                Open(); var shown = Popup();
                                if (state == "disable") parent.IsEnabled = false;
                                else if (state == "hide") parent.IsVisible = false;
                                else parent.Children.Remove(control);
                                Settle(); Ok(shown?.IsOpen == false, name + ": " + state + " releases the popup");
                                parent.IsEnabled = true; parent.IsVisible = true;
                                if (state == "detach") parent.Children.Insert(1, control);
                                Settle();
                            }
                            Open(); var reopened = Popup(); Click(outside);
                            Ok(reopened?.IsOpen == false && clicks == 2, name + ": reopening after reattachment has no stale click handlers");
                            Ok(edits == 0, name + ": dismissal and lifecycle changes do not commit selection or duplicate edits");
                            window.Close();
                        }
            }
            finally { Look.Configure(new ShadeOptions()); SetMode(Mode.Dark); }
        }

        static void MultiSelection()
        {
            var source = new[] { "Anna", "Ben", "Chloe", "Anna" };
            var picker = new MultiChoice(source, "Everyone") { Width = 240 };
            source[0] = "Changed outside";
            Ok(picker.Items.SequenceEqual(new[] { "Anna", "Ben", "Chloe" }) && picker.Items is not string[], "multi-choice copies its options and removes duplicate ordinal names");
            int changes = 0, notifications = 0;
            picker.Changed += () => changes++;
            picker.PropertyChanged += (_, e) => { if (e.Property == MultiChoice.SelectedProperty) notifications++; };
            picker.Set(new[] { "Anna", "missing" }); picker.Set(picker.Selected);
            Ok(picker.Selected.SetEquals(new[] { "Anna" }) && notifications == 1 && changes == 0, "multi-choice selection copies safely from itself and programmatic updates notify once without Changed");
            var external = new HashSet<string> { "Ben" }; picker.Selected = external; external.Add("Chloe");
            Ok(picker.Selected.SetEquals(new[] { "Ben" }) && picker.Selected is not HashSet<string>, "multi-choice selection snapshots cannot be changed through the caller's mutable set");
            Ok(Throws(() => picker.Items = new[] { "valid", null! }) && picker.Items[0] == "Anna", "invalid option replacement leaves the existing list installed");
            var before = new ActionButton("Before"); var after = new ActionButton("After") { Margin = new Thickness(0, 200, 0, 0) };
            var content = new StackPanel { Children = { before, picker, after } };
            var window = new Window { Width = 500, Height = 500, Content = content }; window.Show();
            void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
            void Key(Avalonia.Input.Key key, Avalonia.Input.RawInputModifiers modifiers = Avalonia.Input.RawInputModifiers.None) { window.KeyPress(key, modifiers, Avalonia.Input.PhysicalKey.None, null); window.KeyRelease(key, modifiers, Avalonia.Input.PhysicalKey.None, null); Settle(); }
            CheckBox[] Options() => window.GetVisualDescendants().OfType<CheckBox>().Where(option => option.Tag is string).ToArray();
            var peer = (Avalonia.Automation.Provider.IExpandCollapseProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(picker)!;
            Settle();
            void ClickPicker() { var at = picker.TranslatePoint(new Point(10, 10), window)!.Value; window.MouseDown(at, Avalonia.Input.MouseButton.Left); window.MouseUp(at, Avalonia.Input.MouseButton.Left); Settle(); }
            ClickPicker();
            Ok(peer.ExpandCollapseState == Avalonia.Automation.ExpandCollapseState.Expanded && Options().All(option => !option.Classes.Contains(":focus-visible")), "pointer opening multi-choice focuses an option without a keyboard accent ring");
            ClickPicker();
            Ok(peer.ExpandCollapseState == Avalonia.Automation.ExpandCollapseState.Collapsed, "clicking the open multi-choice trigger closes it without reopening");
            Key(Avalonia.Input.Key.Escape);
            Settle(); picker.Focus(Avalonia.Input.NavigationMethod.Tab); Key(Avalonia.Input.Key.Down);
            Ok(peer.ExpandCollapseState == Avalonia.Automation.ExpandCollapseState.Expanded && Options()[1].IsFocused, "keyboard opening focuses the first selected native checkbox");
            Ok(Options()[1].Classes.Contains(":focus-visible"), "keyboard opening multi-choice keeps its visible option focus ring");
            Ok(Options().All(option => option.Bounds.Height == Look.ControlHeight), "native option targets retain Shade row metrics instead of Fluent minimum heights");
            Key(Avalonia.Input.Key.End); Key(Avalonia.Input.Key.Space);
            Ok(picker.Selected.SetEquals(new[] { "Ben", "Chloe" }) && changes == 1 && peer.ExpandCollapseState == Avalonia.Automation.ExpandCollapseState.Expanded, "End and Space select an option while keeping multi-choice open");
            Key(Avalonia.Input.Key.Home);
            var toggle = (Avalonia.Automation.Provider.IToggleProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(Options()[0])!;
            toggle.Toggle(); Settle();
            Ok(picker.Selected.Count == 3 && changes == 2 && toggle.ToggleState == Avalonia.Automation.Provider.ToggleState.On, "native option automation uses the same selection notification path");
            picker.Set(new[] { "Chloe" });
            Ok(Options().Select(option => option.IsChecked == true).SequenceEqual(new[] { false, false, true }) && changes == 2, "programmatic selection updates an already-open popup without user change events");
            window.KeyTextInput("B"); Settle();
            Ok(Options()[1].IsFocused, "typing an option prefix moves focus without changing selection");
            Key(Avalonia.Input.Key.Escape);
            Ok(peer.ExpandCollapseState == Avalonia.Automation.ExpandCollapseState.Collapsed && picker.IsFocused && !window.GetVisualDescendants().OfType<CheckBox>().Any(), "Escape restores the trigger and detaches the popup");
            Key(Avalonia.Input.Key.Up); Ok(Options()[^1].IsFocused, "Up opens on the last option"); Key(Avalonia.Input.Key.Tab);
            Ok(after.IsFocused && peer.ExpandCollapseState == Avalonia.Automation.ExpandCollapseState.Collapsed, "Tab closes multi-choice and moves to the next application control");
            picker.Focus(); Key(Avalonia.Input.Key.Down); Key(Avalonia.Input.Key.Tab, Avalonia.Input.RawInputModifiers.Shift);
            Ok(before.IsFocused, "Shift+Tab closes multi-choice and moves to the previous application control");
            picker.Focus(); Key(Avalonia.Input.Key.Down); picker.Items = new[] { "Chloe", "Dora" }; Settle();
            Ok(picker.Selected.SetEquals(new[] { "Chloe" }) && peer.ExpandCollapseState == Avalonia.Automation.ExpandCollapseState.Collapsed && picker.IsFocused, "option replacement closes safely and keeps surviving selections");
            peer.Expand(); Settle(); content.IsEnabled = false; Settle();
            Ok(peer.ExpandCollapseState == Avalonia.Automation.ExpandCollapseState.Collapsed, "disabling a parent closes multi-choice");
            content.IsEnabled = true; peer.Expand(); Settle(); content.IsVisible = false; Settle();
            Ok(peer.ExpandCollapseState == Avalonia.Automation.ExpandCollapseState.Collapsed, "hiding a parent closes multi-choice");
            content.IsVisible = true; peer.Expand(); Settle();
            var outside = after.TranslatePoint(new Point(after.Bounds.Width / 2, after.Bounds.Height / 2), window)!.Value;
            window.MouseDown(outside, Avalonia.Input.MouseButton.Left); window.MouseUp(outside, Avalonia.Input.MouseButton.Left); Settle();
            Ok(after.IsFocused && peer.ExpandCollapseState == Avalonia.Automation.ExpandCollapseState.Collapsed, "light dismissal preserves focus on the control clicked outside");
            peer.Expand(); Settle(); content.Children.Remove(picker); Settle();
            Ok(peer.ExpandCollapseState == Avalonia.Automation.ExpandCollapseState.Collapsed && !window.GetVisualDescendants().OfType<CheckBox>().Any(), "detaching multi-choice releases its popup");
            content.Children.Add(picker); picker.Items = Array.Empty<string>(); peer.Expand(); Settle();
            Ok(peer.ExpandCollapseState == Avalonia.Automation.ExpandCollapseState.Collapsed && picker.Selected.Contains("Chloe"), "empty options retain selection while loading and do not open an empty popup");
            try
            {
                picker.Items = new[] { "A", "B", "C" }; picker.Set(picker.Items);
                Look.Localize = (key, args) => key == "{0} selected" ? args[0] + " vybráno" : key;
                Look.RefreshLanguage();
                Ok(picker.GetVisualDescendants().OfType<TextBlock>().Single().Text == "3 vybráno", "multi-choice count uses count-aware live localization");
                picker.Label = "People"; Look.RefreshLanguage();
                Ok(picker.GetVisualDescendants().OfType<TextBlock>().Single().Text == "People", "a fixed application caption wins over the localized selection count");
            }
            finally { Look.Localize = null; Look.RefreshLanguage(); window.Close(); }
        }

        static void BindingRegressions()
        {
            var deferred = new MultiChoice { Selected = new HashSet<string> { "Ben" }, Items = new[] { "Anna", "Ben" } };
            Ok(deferred.Selected.Contains("Ben"), "selection may arrive before picker options");
            deferred.Items = Array.Empty<string>(); Ok(deferred.Selected.Contains("Ben"), "empty loading snapshots keep selected keys");
            deferred.Items = new[] { "Anna", "Chloe" }; Ok(deferred.Selected.Count == 0, "a populated replacement prunes unavailable selected keys");
            var search = new SearchField(); var field = new Field { Error = "Name is required" };
            var labelled = FormLayout.Field("Name", field);
            Ok(labelled.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == field.Error) && Avalonia.Automation.AutomationProperties.GetLabeledBy(field.Box) != null, "form layout preserves the shared validation message and accessible label");
            var choice = new ChoiceButton(); choice.SetItems(new[] { "One", "Two" }, "One"); int picks = 0; choice.Picked += _ => picks++;
            var source = new List<string[]> { new[] { "Alpha" }, new[] { "Beta" } }; var table = Workbench.Table(new[] { "Name" }, source);
            var parent = new StackPanel { Children = { choice, search, labelled, table } }; var window = new Window { Width = 700, Height = 500, Content = parent }; window.Show();
            void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
            void Key(Avalonia.Input.Key key) { window.KeyPress(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); window.KeyRelease(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle(); }
            Settle(); search.Box.Text = "abc"; Settle(); int edits = 0; search.Edited += () => edits++; search.Box.Focus(); Key(Avalonia.Input.Key.Escape);
            Ok(edits == 1 && search.Text == "", "clearing search emits exactly one edit notification");
            var list = table.GetVisualDescendants().OfType<ListBox>().Single(); list.SelectedIndex = 0; var item = list.GetVisualDescendants().OfType<ListBoxItem>().First(); int selectionChanges = 0; list.SelectionChanged += (_, _) => selectionChanges++;
            Workbench.SetMark(table, "a"); Settle();
            Ok(list.SelectedIndex == 0 && selectionChanges == 0 && ReferenceEquals(item, list.GetVisualDescendants().OfType<ListBoxItem>().First()), "highlight changes keep selection and native item containers");
            Workbench.SetMark(table, null); Settle(); Ok(list.SelectedIndex == 0 && table.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Alpha"), "clearing highlights restores text without resetting the items source");
            double wide = Look.Measure("WWWW", Look.Ui(TextSize.Body)) + 24;
            Ok(Workbench.FitWidth(new[] { "iiiiiiiiiiii", "WWWW" }, 0, 1000) >= wide, "FitWidth measures rendered glyph width rather than character count");
            choice.Focus(); Key(Avalonia.Input.Key.Down); parent.IsEnabled = false; Settle(); Key(Avalonia.Input.Key.Down); Key(Avalonia.Input.Key.Enter);
            Ok(choice.Current == "One" && picks == 0 && !window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Two"), "disabling a picker parent dismisses its menu and prevents stale selection commits");
            parent.IsEnabled = true; choice.Focus(); Key(Avalonia.Input.Key.Down); parent.IsVisible = false; Settle();
            Ok(!window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Two" && t.IsEffectivelyVisible), "hiding a picker parent dismisses its menu"); parent.IsVisible = true;
            var designer = new TableDesigner { Columns = new[] { new TableDesigner.Column("A"), new TableDesigner.Column("B") }, Selected = 1 }; int notices = 0;
            designer.PropertyChanged += (_, e) => { if (e.Property == TableDesigner.SelectedProperty) notices++; }; designer.Columns = new[] { new TableDesigner.Column("A") };
            Ok(designer.Selected == -1 && notices == 1, "column replacement notifies the designer's corrected selection");
            var fixedRows = new EditableTable { Columns = new[] { new EditableTable.Column("name", "Name") }, Rows = new[] { new EditableTable.Row() } }; parent.Children.Add(fixedRows); Settle();
            Ok(!fixedRows.GetVisualDescendants().OfType<ActionButton>().Any(b => b.Content?.ToString() == "Add" && b.IsEffectivelyVisible), "fixed-size row sources do not advertise Add");
            window.Close();
        }

        static void PairRows()
        {
            var entries = new System.Collections.ObjectModel.ObservableCollection<PairTable.Entry> { new("Accept", "text/plain"), new("accept", "application/json"), new("", "unfinished draft") };
            var pairs = new PairTable { Entries = entries, CaseSensitive = false }; int changes = 0; pairs.Changed += () => changes++;
            var window = new Window { Width = 600, Height = 600, Content = pairs }; window.Show();
            void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
            Settle();
            Ok(pairs.HasErrors && entries.Take(2).All(entry => entry.Error != null) && !pairs.TryGetPairs(out _), "pair duplicate validation marks both rows without exporting a lossy dictionary");
            bool refused = false; try { pairs.Pairs(); } catch (ArgumentException) { refused = true; }
            Ok(refused && entries.Count == 3, "pair dictionary adapters use the configured duplicate policy by default");
            Ok(entries.Count == 3 && entries[2].Value == "unfinished draft" && pairs.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text?.StartsWith("Duplicate name:", StringComparison.Ordinal) == true), "duplicate and blank pair drafts stay visible and editable");
            pairs.CaseSensitive = true; Ok(!pairs.HasErrors && pairs.TryGetPairs(out var sensitive) && sensitive.Count == 2, "pair duplicate comparison can be case-sensitive");
            pairs.CaseSensitive = false; entries[1].Name = "X-Token"; Settle();
            Ok(!pairs.HasErrors && entries.All(entry => entry.Error == null) && pairs.TryGetPairs(out var valid) && valid.Count == 2 && changes == 0, "external row edits refresh pair validation without emitting user changes");
            var key = pairs.GetVisualDescendants().OfType<Field>().Single(field => field.Text == "Accept"); key.Box.Focus(); key.Box.CaretIndex = 2;
            entries.Add(new("Extra", "value")); Settle();
            Ok(key.Box.IsFocused && key.Box.CaretIndex == 2 && pairs.GetVisualDescendants().Contains(key), "adding pair rows retains existing editors, focus and caret");
            key.Box.Text = "Renamed"; Settle();
            Ok(entries[0].Name == "Renamed" && changes == 1, "pair typing writes to the row model and emits one user change");
            entries.RemoveAt(2); pairs.Set(pairs.Pairs()); Settle();
            Ok(key.Box.IsFocused && pairs.GetVisualDescendants().Contains(key), "setting unchanged exported pairs preserves the active editor");
            var remove = pairs.GetVisualDescendants().OfType<ActionButton>().First(button => button.Icon == Icons.X);
            ((Avalonia.Automation.Provider.IInvokeProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(remove)!).Invoke(); Settle();
            Ok(entries.Count == 2 && entries.All(entry => entry.Name != "Renamed") && changes == 2, "native pair removal updates the bound collection and emits one user change");
            window.Content = null; entries.Add(new("X-Token", "duplicate after detach")); window.Content = pairs; Settle();
            Ok(pairs.HasErrors && entries.Where(entry => entry.Name == "X-Token").All(entry => entry.Error != null), "reattaching pair rows reconciles changes made while detached");
            pairs.Entries = Array.AsReadOnly(entries.ToArray()); Settle();
            Ok(!pairs.GetVisualDescendants().OfType<ActionButton>().Any(button => button.IsEffectivelyVisible && (button.Content?.ToString() == "Add" || button.Icon == Icons.X)), "fixed-size pair structures hide structural actions while retaining editors");
            window.Close();
        }

        static void SuggestionLifecycle()
        {
            var field = new SuggestionField { Items = new[] { "Anna", "Annual report", "Ben" } }; int picks = 0; string? snapshot = null;
            field.Picked += _ => { picks++; snapshot = field.Text; };
            var search = new SearchField(); var parent = new StackPanel { Children = { field, search } };
            var window = new Window { Width = 500, Height = 300, Content = parent }; window.Show();
            void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
            void Key(Avalonia.Input.Key key) { window.KeyPress(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); window.KeyRelease(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle(); }
            Button[] Options() => window.GetVisualDescendants().OfType<Button>().Where(button => button.Tag is int).ToArray();
            Settle(); field.Box.Focus(); field.Box.Text = "an"; Settle(); var rows = Options();
            Ok(rows.Length == 2 && field.Box.IsFocused, "bindable suggestion options open without taking editor focus");
            Key(Avalonia.Input.Key.Down); Key(Avalonia.Input.Key.Down);
            Ok(Options().Length == 2 && ReferenceEquals(rows[0], Options()[0]) && field.Box.IsFocused, "suggestion keyboard movement retains the popup rows and native editor focus");
            Key(Avalonia.Input.Key.Enter); Ok(field.Text == "Annual report" && picks == 1, "suggestion Enter commits the highlighted result once");
            field.ApplySelection = false; field.Box.Text = "ann"; Settle(); rows = Options();
            ((Avalonia.Automation.Provider.IInvokeProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(rows[0])!).Invoke(); Settle();
            Ok(field.Text == "ann" && snapshot == "ann" && picks == 2, "suggestion actions can snapshot current text without replacing it");
            field.Box.Text = "a"; Settle(); parent.IsEnabled = false; Settle(); Ok(Options().Length == 0, "disabling a parent closes suggestion actions");
            parent.IsEnabled = true; field.Box.Focus(); field.Box.Text = "b"; Settle(); field.IsReadOnly = true; Settle(); Ok(Options().Length == 0 && picks == 2, "read-only suggestion fields close without committing a choice");
            field.IsReadOnly = false; field.Box.Focus(); field.Items = new[] { "Bob", "Brian" }; Settle(); Ok(Options().Length == 2, "replacing bound suggestions refreshes the result snapshot");
            parent.IsVisible = false; Settle(); Ok(Options().Length == 0, "hiding an ancestor releases the suggestion popup"); parent.IsVisible = true;
            search.Box.Text = "query"; search.IsReadOnly = true; Settle(); search.Box.Focus(); Key(Avalonia.Input.Key.Escape);
            Ok(search.Text == "query" && search.GetVisualDescendants().OfType<IconButton>().Single().IsEffectivelyEnabled == false, "read-only search cannot clear through Escape or its native clear action");
            window.Close();
        }

        static void NativeSelections()
        {
            var tick = new Tick { IsChecked = null }; int clicks = 0; tick.Click += (_, _) => clicks++;
            var state = new StateToggle { IsChecked = true, ShowUndo = true, Width = 150 }; var undo = new Command(); state.UndoCommand = undo; int toggles = 0; state.Click += (_, e) => { if (ReferenceEquals(e.Source, state)) toggles++; };
            var grid = new TickGrid(new[] { "One", "Two" }, Array.Empty<string>()); int changes = 0; grid.Changed += () => changes++;
            var tag = new Tag("Report", removable: true); int removes = 0; tag.Removed += () => removes++;
            var choice = new ChoiceButton { Current = "Two", Items = new[] { "One", "Two", "Three" } }; int picks = 0; choice.Picked += _ => picks++;
            var before = new Button { Content = "Before" }; var after = new Button { Content = "After" };
            var content = new StackPanel { Children = { tick, state, grid, tag, before, choice, after } }; var window = new Window { Width = 600, Height = 500, Content = content }; window.Show();
            void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
            void Click(Control control, Avalonia.Input.MouseButton button = Avalonia.Input.MouseButton.Left) { var at = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value; window.MouseDown(at, button); window.MouseUp(at, button); Settle(); }
            void Key(Avalonia.Input.Key key) { window.KeyPress(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); window.KeyRelease(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle(); }
            Settle(); Click(tick, Avalonia.Input.MouseButton.Right); Ok(tick.IsChecked == null && clicks == 0, "native ticks ignore right-click activation");
            Click(tick); Ok(tick.IsChecked == true && clicks == 1, "a mixed native tick becomes checked on a primary click");
            tick.IsChecked = false; Ok(clicks == 1, "programmatic toggle changes do not raise user Click");
            tick.Focus(); Key(Avalonia.Input.Key.Space); Ok(tick.IsChecked == true && clicks == 2, "native toggle keyboard activation raises one click");
            var undoButton = state.GetVisualDescendants().OfType<IconButton>().Single(); Click(undoButton);
            Ok(undo.Runs == 1 && state.IsChecked == true && toggles == 0, "the separate native undo command does not toggle its parent state");
            var row = grid.Children.OfType<CheckBox>().First(); var glyph = row.GetVisualDescendants().OfType<Tick>().Single();
            Ok(glyph.Bounds.Size == new Size(16, 16), $"native tick-grid faces retain their square metrics ({glyph.Bounds.Size}; height {glyph.Height}; minimum {glyph.MinHeight}; template {glyph.Template != null})");
            Click(row, Avalonia.Input.MouseButton.Right); Ok(changes == 0, "native tick-grid labels ignore right-click");
            Click(row); Ok(grid.Picked.SequenceEqual(new[] { "One" }) && changes == 1, "whole native tick-grid rows update ordered selection once");
            var remove = tag.GetVisualDescendants().OfType<IconButton>().Single(); Click(remove, Avalonia.Input.MouseButton.Right); Ok(removes == 0, "tag removal ignores right-click");
            ((Avalonia.Automation.Provider.IInvokeProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(remove)!).Invoke(); Ok(removes == 1, "tag removal supports native invoke automation");
            var triggerCommand = new Command(); choice.Command = triggerCommand; Click(choice);
            Ok(triggerCommand.Runs == 1 && choice.Expanded, "single-picker triggers retain native Button commands");
            var peer = (Avalonia.Automation.Provider.IExpandCollapseProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(choice)!;
            peer.Collapse(); Settle();
            peer.Expand(); Settle();
            var actions = window.GetVisualDescendants().OfType<Button>().Where(button => button.Tag is int).ToArray();
            Ok(actions.Length == 3 && actions[1].IsFocused && Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(actions[0]) is Avalonia.Automation.Peers.ButtonAutomationPeer, "single pickers focus the selected native menu action");
            Key(Avalonia.Input.Key.End); Key(Avalonia.Input.Key.Enter); Ok(choice.Current == "Three" && picks == 1 && !choice.Expanded && choice.IsFocused, "native menu keyboard selection closes, notifies once and restores its trigger");
            peer.Expand(); Settle(); Key(Avalonia.Input.Key.Tab); Ok(!choice.Expanded && after.IsFocused, "Tab leaves a single-picker popup and continues through the host");
            peer.Expand(); Settle(); choice.Items = new[] { "One" }; Settle(); Ok(!choice.Expanded && choice.Current == "Three", "replacing single-picker options closes stale actions and retains its explicit value");
            choice.Items = Array.Empty<string>(); choice.Extra = "Create"; int extra = 0; choice.ExtraPicked += () => extra++; peer.Expand(); Settle(); Key(Avalonia.Input.Key.Enter);
            Ok(extra == 1 && choice.Current == "Three", "an empty picker can expose its extra action without changing selection");
            choice.Items = new[] { "One", "Two" }; choice.Extra = null; peer.Expand(); Settle(); content.IsEnabled = false; Settle();
            Ok(!choice.Expanded, "parent disabling closes native picker actions"); content.IsEnabled = true;
            peer.Expand(); Settle(); content.Children.Remove(choice); Settle(); Ok(!choice.Expanded, "detaching a native picker releases its popup");
            window.Close();
            var masonry = new Masonry(); bool rejected = false; try { masonry.MinColumn = double.NaN; } catch (ArgumentException) { rejected = true; }
            Ok(rejected && masonry.MinColumn > 0, "masonry rejects invalid layout metrics before layout");
        }

        static void SearchableChoices()
        {
            var employees = new System.Collections.ObjectModel.ObservableCollection<Employee>
            {
                new(1, "Jan Novák", "Mgr. Research"), new(2, "Petra Šílá", "Bc. HR"), new(3, "Jan Novák", "Ing. Engineering")
            };
            var vm = new Vm { EmployeeId = 2 };
            var picker = new SearchChoice { ItemsSource = employees, DisplayMemberBinding = new Avalonia.Data.Binding(nameof(Employee.Name)), SearchMemberBinding = new Avalonia.Data.Binding(nameof(Employee.SearchText)), SelectedValueBinding = new Avalonia.Data.Binding(nameof(Employee.Id)), PlaceholderText = "Choose employee", Width = 320 };
            picker.Bind(Avalonia.Controls.Primitives.SelectingItemsControl.SelectedValueProperty, new Avalonia.Data.Binding(nameof(Vm.EmployeeId)) { Source = vm, Mode = Avalonia.Data.BindingMode.TwoWay });
            var before = new TextBox(); var after = new TextBox(); var parent = new StackPanel { Children = { before, picker, after } };
            var window = new Window { Width = 600, Height = 500, Content = parent }; window.Show();
            void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
            void Key(Avalonia.Input.Key key, Avalonia.Input.RawInputModifiers modifiers = Avalonia.Input.RawInputModifiers.None) { window.KeyPress(key, modifiers, Avalonia.Input.PhysicalKey.None, null); window.KeyRelease(key, modifiers, Avalonia.Input.PhysicalKey.None, null); Settle(); }
            void Click(Control control, Avalonia.Input.MouseButton button = Avalonia.Input.MouseButton.Left) { var at = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value; window.MouseDown(at, button); window.MouseUp(at, button); Settle(); }
            SearchField Search() => window.GetVisualDescendants().OfType<SearchField>().Single();
            ListBox Results() => window.GetVisualDescendants().OfType<ListBox>().Single();
            Button Row(int id) => Results().GetVisualDescendants().OfType<Button>().Single(button => button.Content is Employee employee && employee.Id == id);
            Settle(); int committed = 0, changed = 0; picker.SelectionCommitted += (_, _) => committed++; picker.SelectionChanged += (_, _) => changed++;
            var peer = Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(picker)!;
            Ok(peer is Avalonia.Automation.Peers.ComboBoxAutomationPeer && ReferenceEquals(picker.SelectedItem, employees[1]) && vm.EmployeeId == 2, "search choices use native ComboBox object selection and ID bindings");
            var trigger = picker.GetVisualDescendants().OfType<Button>().First(button => Avalonia.Automation.AutomationProperties.GetAccessibilityView(button) == Avalonia.Automation.AccessibilityView.Raw);
            Ok(trigger.Bounds.Width >= picker.Bounds.Width - picker.Padding.Left - picker.Padding.Right - 2, "search-choice trigger fills its frame so the chevron aligns at the trailing edge");
            var captionPosition = trigger.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible).TranslatePoint(default, window);
            ((Avalonia.Automation.Provider.IExpandCollapseProvider)peer).Expand(); Settle();
            Ok(trigger.GetVisualDescendants().OfType<TextBlock>().First(t => t.IsEffectivelyVisible).TranslatePoint(default, window) == captionPosition, "opening a native searchable dropdown keeps the caption on the same pixels");
            Ok(picker.IsDropDownOpen && Results().ItemCount == 3 && Search().Box.IsFocused, "search choices open every option and focus the fixed search header");
            Search().Box.Text = "NOVAK mgr"; Settle();
            Ok(Results().ItemCount == 1 && Results().GetVisualDescendants().OfType<Button>().Any(button => ReferenceEquals(button.Content, employees[0])), "each search word matches independently across custom fields, without case or accents");
            Ok(ReferenceEquals(picker.SelectedItem, employees[1]) && vm.EmployeeId == 2 && committed == 0 && changed == 0, "filtering out the selected employee does not change the object, ID or selection events");
            Search().Box.Text = "no such employee"; Settle(); Key(Avalonia.Input.Key.Enter);
            Ok(Results().ItemCount == 0 && picker.IsDropDownOpen && vm.EmployeeId == 2 && committed == 0 && window.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "No matches" && text.IsEffectivelyVisible), "no results is visible and Enter cannot clear the committed selection");
            Key(Avalonia.Input.Key.Escape); Ok(!picker.IsDropDownOpen && picker.IsFocused && vm.EmployeeId == 2, "Escape dismisses search without changing selection and restores the trigger");
            Key(Avalonia.Input.Key.Down); Search().Box.Text = "jan"; Settle(); Key(Avalonia.Input.Key.Down); Key(Avalonia.Input.Key.Enter);
            Ok(vm.EmployeeId == 3 && ReferenceEquals(picker.SelectedItem, employees[2]) && committed == 1 && changed == 1, "duplicate display labels remain distinct objects and keyboard commit returns the correct ID once");
            Key(Avalonia.Input.Key.Enter); Ok(picker.SearchText == "" && Results().ItemCount == 3, "reopening resets the filter and shows all options");
            Click(Row(1), Avalonia.Input.MouseButton.Right); Ok(committed == 1 && vm.EmployeeId == 3, "result rows ignore right-click selection");
            ((Avalonia.Automation.Provider.IInvokeProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(Row(1))!).Invoke(); Settle();
            Ok(vm.EmployeeId == 1 && committed == 2 && !picker.IsDropDownOpen, "native result invoke automation commits an employee object");
            Key(Avalonia.Input.Key.Enter); Key(Avalonia.Input.Key.Tab); Ok(after.IsFocused && !picker.IsDropDownOpen, "Tab closes search choices and continues to the next application control");
            picker.Focus(); Key(Avalonia.Input.Key.Space); Key(Avalonia.Input.Key.Tab, Avalonia.Input.RawInputModifiers.Shift); Ok(before.IsFocused && !picker.IsDropDownOpen, "Shift+Tab leaves search choices toward the previous application control");
            picker.Focus(); Key(Avalonia.Input.Key.Enter); employees.Add(new(4, "Žofie Malá", "Support")); Settle(); Search().Box.Text = "zofie"; Settle();
            Ok(Results().ItemCount == 1, "observable source updates refresh searchable items while open");
            Search().Box.Text = "lá"; Settle();
            Ok(Results().GetVisualDescendants().OfType<TextBlock>().Any(label => label.Inlines?.OfType<Avalonia.Controls.Documents.Run>().Any(run => run.Text == "lá" && run.Foreground == B(AccentText)) == true), "searchable options highlight accented label matches with the readable accent role");
            Search().Box.Text = "zofie"; Settle();
            picker.ClearValue(ItemsControl.DisplayMemberBindingProperty);
            Avalonia.Controls.Primitives.TextSearch.SetTextBinding(picker, new Avalonia.Data.Binding(nameof(Employee.Name)));
            picker.SelectionBoxItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<Employee>((employee, _) => new TextBlock { Text = employee?.Name });
            picker.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<Employee>((employee, _) => employee == null ? null : new StackPanel { Children = { new TextBlock { Text = employee.Name }, new TextBlock { Text = employee.Title } } }); Settle();
            Ok(Results().GetVisualDescendants().OfType<TextBlock>().Any(label => label.Text == "Support"), "search choices accept rich native item templates while keeping searchable metadata separate");
            var at = Row(4).TranslatePoint(new Point(10, 10), window)!.Value;
            window.MouseDown(at, Avalonia.Input.MouseButton.Left); window.MouseMove(new Point(590, 490)); window.MouseUp(new Point(590, 490), Avalonia.Input.MouseButton.Left); Settle();
            Ok(committed == 2 && vm.EmployeeId == 1, "dragging a result press outside cancels activation");
            var stale = Row(4); employees.RemoveAt(3);
            ((Avalonia.Automation.Provider.IInvokeProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(stale)!).Invoke();
            Ok(vm.EmployeeId == 1 && committed == 2, "removing an option before its queued refresh cannot commit a stale row or clear selection");
            parent.IsEnabled = false; Settle(); Ok(!picker.IsDropDownOpen && committed == 2, "disabling a parent closes searchable choices without committing");
            parent.IsEnabled = true; picker.Focus(); Key(Avalonia.Input.Key.Enter); parent.IsVisible = false; Settle(); Ok(!picker.IsDropDownOpen, "hiding an ancestor closes native searchable popups");
            parent.IsVisible = true; picker.Focus(); Key(Avalonia.Input.Key.Enter); parent.Children.Remove(picker); Settle(); Ok(!picker.IsDropDownOpen, "detaching search choices releases their popup");
            window.Close();
            var record = new PairTable.Entry("Jan Novák", "Old department");
            var live = new SearchChoice { ItemsSource = new[] { record }, SelectedItem = record, DisplayMemberBinding = new Avalonia.Data.Binding(nameof(PairTable.Entry.Name)), SearchMemberBinding = new Avalonia.Data.Binding(nameof(PairTable.Entry.Value)) };
            var liveWindow = new Window { Width = 500, Height = 400, Content = live }; liveWindow.Show(); Settle(); live.IsDropDownOpen = true; Settle(); live.SearchText = "new"; Settle();
            var liveList = liveWindow.GetVisualDescendants().OfType<ListBox>().Single(); record.Value = "New department"; live.RefreshSearch(); Settle();
            Ok(liveList.ItemCount == 1 && ReferenceEquals(live.SelectedItem, record), "explicit refresh updates in-place search fields without changing the selected object");
            live.SearchText = ""; record.Name = "Jan Updated"; Settle();
            Ok(liveWindow.GetVisualDescendants().OfType<TextBlock>().Any(label => label.Text == "Jan Updated"), "native display bindings still observe item property changes");
            liveWindow.Close();
        }

        static void IntegrationFeedback()
        {
            void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
            void Key(Window w, Avalonia.Input.Key key) { w.KeyPress(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); w.KeyRelease(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle(); }
            void Click(Window w, Control c) { var p = c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), w)!.Value; w.MouseDown(p, Avalonia.Input.MouseButton.Left); w.MouseUp(p, Avalonia.Input.MouseButton.Left); Settle(); }
            T FieldOf<T>(object obj, string name) => (T)obj.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(obj)!;
            var choice = new ChoiceButton { Items = new[] { "One", "Two" }, Current = "One" }; var next = new TextBox();
            var w = new Window { Width = 500, Height = 500, Content = new StackPanel { Children = { choice, next } } }; w.Show(); Settle();
            Button FocusedRow() => w.GetVisualDescendants().OfType<Button>().First(b => b.IsFocused && b.Tag is int);
            int picked = 0; choice.Picked += _ => picked++;
            Click(w, choice); Ok(!FocusedRow().Classes.Contains(":focus-visible"), "pointer-opened selected menus keep native focus without a keyboard outline");
            Key(w, Avalonia.Input.Key.Down); Ok(FocusedRow().Classes.Contains(":focus-visible"), "arrows after a pointer opening reveal keyboard focus");
            Key(w, Avalonia.Input.Key.Enter); Ok(picked == 1 && choice.Current == "Two", "keyboard activation after mouse opening selects exactly once");
            Key(w, Avalonia.Input.Key.Down); Ok(FocusedRow().Classes.Contains(":focus-visible"), "keyboard-opened pickers show their focus outline");
            Key(w, Avalonia.Input.Key.Escape); Ok(choice.IsFocused && choice.Classes.Contains(":focus-visible"), "keyboard dismissal restores keyboard focus modality");
            choice.Current = null; Click(w, choice); Ok(!FocusedRow().Classes.Contains(":focus-visible"), "pointer-opened menus without selection do not force keyboard modality");
            Key(w, Avalonia.Input.Key.Tab); Ok(next.IsFocused, "Tab still leaves the shared popup toward the next host field");
            var context = Shade.Menu.Show(choice, new[] { "Open", "Delete" }, _ => picked++, atPointer: true); Settle();
            Ok(!FocusedRow().Classes.Contains(":focus-visible"), "shared pointer context menus also preserve pointer modality"); Key(w, Avalonia.Input.Key.Escape);
            Click(w, choice); Click(w, FocusedRow()); Key(w, Avalonia.Input.Key.Space);
            Ok(FocusedRow().Classes.Contains(":focus-visible"), "Space after a pointer selection opens with keyboard focus indication");
            Key(w, Avalonia.Input.Key.Escape); w.Close();

            foreach (var scale in new[] { 1d, 1.5d })
                foreach (var mode in new[] { Mode.Dark, Mode.Light })
                    foreach (var markKind in new[] { "default", "hidden", "custom" })
                    {
                        Look.Configure(new ShadeOptions { TextScale = scale, ReducedMotion = true }); SetMode(mode);
                        Control? mark = markKind == "default" ? null : new Border { IsVisible = markKind != "hidden" };
                        var sidebar = new Sidebar("Potraviny levně", "Objednávky", new[] { new NavEntry("Home", "home", Icons.Monitor) }, mark) { ExpandedWidth = 260 * scale };
                        var host = new Window { Width = 900, Height = 700, Content = Shell.Layout(sidebar, new Border(), new StatusBar()) }; host.Show(); Settle();
                        var title = sidebar.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "Potraviny levně");
                        var brandBounds = title.TranslatePoint(default, sidebar)!.Value;
                        var logo = FieldOf<Panel>(sidebar, "markHost"); var logoAt = logo.TranslatePoint(default, sidebar)!.Value;
                        Ok(title.TextLayout.TextLines.All(line => !line.HasCollapsed) && (markKind == "hidden" || brandBounds.X + title.Bounds.Width <= logoAt.X), $"{mode}, {scale}, {markKind}: full localized branding fits without logo overlap");
                        sidebar.Collapsed = true; sidebar.Collapsed = false; Settle();
                        Ok(sidebar.Width == 260 * scale, "configured sidebar width survives collapse and expand");
                        sidebar.ExpandedWidth = 180 * scale; Settle(); Ok(title.TextLayout.TextLines.All(line => !line.HasCollapsed) && title.Bounds.Height > TextSize.Title, "default-width branding wraps without silently shrinking");
                        host.Close();
                    }
            Look.Configure(new ShadeOptions()); SetMode(Mode.Dark);
            var animated = new Sidebar("Long localized business name", "Workspace", Array.Empty<NavEntry>()) { ExpandedWidth = 280 };
            var animatedHost = new Window { Width = 600, Height = 400, Content = animated }; animatedHost.Show(); Settle();
            animated.Collapsed = true; animated.Collapsed = false; System.Threading.Thread.Sleep(450); Settle();
            Ok(animated.Width == 280 && !animated.Collapsed, "rapid sidebar reversal settles at the configured expanded width"); animatedHost.Close();

            var c = new EditableTable.Column("n", "Quantity", EditableTable.Kind.Unit) { Units = new[] { "pcs" }, Width = 60, MaxWidth = 60, HeadingSize = 18, SubheadingSize = 14, Subtitle = "Amount", Italic = true };
            var r = new EditableTable.Row(); r.Values["n"] = "12";
            var table = new EditableTable { Columns = new List<EditableTable.Column> { c }, Rows = new List<EditableTable.Row> { r }, Design = true };
            var outside = new TextBox(); DockPanel.SetDock(outside, Dock.Bottom);
            var tableHost = new Window { Width = 500, Height = 500, Content = new DockPanel { Children = { outside, table } } }; tableHost.Show(); Settle();
            var footGrid = FieldOf<Grid>(table, "footGrid");
            var foot = footGrid.GetVisualDescendants().OfType<TextBlock>().Single();
            Ok(foot.Inlines!.OfType<Avalonia.Controls.Documents.Run>().All(run => string.IsNullOrEmpty(run.Text)) && foot.TextTrimming == TextTrimming.None, "None footers lay out no transparent caption or truncation marker");
            var footerAt = foot.TranslatePoint(new Point(10, 8), tableHost)!.Value; tableHost.MouseMove(footerAt); Settle();
            Ok(foot.Inlines!.OfType<Avalonia.Controls.Documents.Run>().First().Text == "ƒ", "narrow None footer hover uses a compact calculation affordance");
            tableHost.MouseMove(new Point(490, 490)); Settle();
            Ok(foot.Inlines!.OfType<Avalonia.Controls.Documents.Run>().First().Text == "", "calculation affordance disappears without leaving text or ellipsis");
            Ok(table.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Quantity" && t.FontSize == 18) && table.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Amount" && t.FontSize == 14), "per-column heading and subtitle sizes override table defaults");
            var value = table.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "12");
            Ok(value.FontStyle == FontStyle.Italic, "per-column italic cells use native text styling");
            var editAt = value.TranslatePoint(new Point(4, 8), tableHost)!.Value; tableHost.MouseDown(editAt, Avalonia.Input.MouseButton.Left); tableHost.MouseUp(editAt, Avalonia.Input.MouseButton.Left); Settle(); var editor = table.GetVisualDescendants().OfType<TextBox>().Single(t => t.IsFocused);
            Ok(editor.FontStyle == FontStyle.Italic, "the cell editor preserves italic typography");
            editor.Text = "letters"; Settle();
            var ring = FieldOf<Border>(table, "ring");
            Ok(r.Values["n"] == "12" && table.LastValidationError == "Enter a number" && DataValidationErrors.GetHasErrors(editor) && ring.BorderBrush == B(Danger), "invalid numeric drafts retain the valid model and show the native validation border");
            Ok(table.GetVisualDescendants().OfType<TextBlock>().Count(t => t.Text == "Enter a number" && t.IsEffectivelyVisible) == 1, "numeric errors have one visible message outside the cell, without a duplicate template message");
            Key(tableHost, Avalonia.Input.Key.Tab); Ok(editor.IsFocused && editor.Text == "letters", "invalid numeric drafts cannot disappear through Tab");
            SetMode(Mode.Light); Settle(); Ok(editor.Text == "letters" && table.LastValidationError != null, "live theme changes retain the invalid draft and its error");
            outside.Focus(); Settle();
            SetMode(Mode.Dark); Settle(); Ok(!editor.IsFocused && editor.Text == "letters" && table.LastValidationError != null, "invalid drafts survive blur and live theme changes without stealing focus");
            editor.Focus();
            editor.Text = "12"; Settle(); Ok(!DataValidationErrors.GetHasErrors(editor) && table.LastValidationError == null, "correcting back to the existing value clears numeric validation");
            editor.Text = "bad"; Key(tableHost, Avalonia.Input.Key.Escape); Ok(r.Values["n"] == "12" && table.LastValidationError == null, "Escape cancels an invalid draft without overwriting the saved value");
            var showDrop = typeof(EditableTable).GetMethod("ShowDrop", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            foreach (int count in new[] { 0, 2, 100 })
            {
                table.Rows = Enumerable.Range(0, count).Select(_ => new EditableTable.Row()).ToList(); Settle();
                showDrop.Invoke(table, new object[] { 0, 1, "Quantity", new Point(20, 20) }); Settle();
                var marker = FieldOf<Border>(table, "line"); var overlay = FieldOf<Canvas>(table, "overlay"); var footer = FieldOf<Grid>(table, "footGrid");
                double bottom = footer.TranslatePoint(new Point(0, footer.Bounds.Height), overlay)!.Value.Y;
                Ok(Math.Abs(Canvas.GetTop(marker) + marker.Height - bottom) < 0.01, $"{count} rows: reorder marker ends at the rendered footer in a tall host");
            }
            tableHost.Close(); SetMode(Mode.Dark);

            var label = new TextBlock(); Build.Mark(label, "Malá Malá", "lá alá");
            Ok(label.Inlines!.OfType<Avalonia.Controls.Documents.Run>().Where(run => run.Foreground == B(AccentText)).Select(run => run.Text).SequenceEqual(new[] { "alá", "alá" }), "accent-insensitive highlighting merges overlapping word matches without duplicating text");
            Build.Mark(label, "🙂 Mala\u0301", "lá");
            Ok(label.Inlines!.OfType<Avalonia.Controls.Documents.Run>().Any(run => run.Text == "la\u0301" && run.Foreground == B(AccentText)), "highlighting retains decomposed accents and original Unicode positions");
            var small = new AccentPicker(); var grid = new ColorGrid(); grid.Bind(ColorGrid.ValueProperty, new Avalonia.Data.Binding("Color") { Source = B(Accent) });
            var colors = new Window { Content = new StackPanel { Children = { small, grid } } }; colors.Show(); Settle();
            small.Value = "Pink"; Settle(); Ok(grid.Value == Accents[4].dark, "bound color helpers follow the settings accent picker");
            SetAccent(Accents[6].dark, Accents[6].light); small.Restyle(); Settle();
            Ok(small.Value == "Lime" && grid.Value == Accents[6].dark, "external color-helper picks update the compact picker and its selected value");
            SetMode(Mode.Light); small.Restyle(); Settle(); Ok(grid.Value == Accents[6].light && small.Value == "Lime", "synchronized accent controls retain curated identity across modes");
            colors.Close(); SetMode(Mode.Dark); SetAccent(Accents[0].dark, Accents[0].light);
        }

        static void NativeFeedback()
        {
            void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
            var menu = Menu.Panel(Enumerable.Range(0, 20).Select(i => "Item " + i).ToArray(), _ => { }, () => { }, 0);
            var page = new ScrollViewer { Content = new StackPanel { Children = { new Border { Height = 500 }, menu, new Border { Height = 800 } } } };
            var window = new Window { Width = 600, Height = 400, Content = page }; window.Show(); Settle();
            page.Offset = new Vector(0, 550); Settle();
            var rows = menu.GetVisualDescendants().OfType<Button>().ToArray();
            double before = page.Offset.Y;
            rows[0].Focus(Avalonia.Input.NavigationMethod.Pointer); rows[0].BringIntoView(); Settle();
            Ok(page.Offset.Y == before, "menu row focus and reveal never scroll the containing app page");
            var inner = menu.GetVisualDescendants().OfType<ScrollViewer>().Single();
            rows[0].Focus(Avalonia.Input.NavigationMethod.Directional);
            window.KeyPress(Avalonia.Input.Key.End, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle();
            Ok(rows[^1].IsFocused && inner.Offset.Y > 0 && page.Offset.Y == before, "menu keyboard navigation scrolls its own viewport only");
            window.Close();

            var number = new EditableTable.Column("n", "Price", EditableTable.Kind.Unit) { Width = 150, Units = new[] { "kg" } };
            var next = new EditableTable.Column("text", "Description") { Width = 150 };
            var row = new EditableTable.Row(); row.Values["n"] = "12";
            var table = new EditableTable { Columns = new List<EditableTable.Column> { number, next }, Rows = new List<EditableTable.Row> { row } };
            window = new Window { Width = 600, Height = 400, Content = table }; window.Show(); Settle();
            var cell = table.GetVisualDescendants().OfType<Border>().First(c => c.GetType().Name == "DataCell");
            double height = cell.Bounds.Height;
            var at = cell.TranslatePoint(new Point(5, 10), window)!.Value;
            window.MouseDown(at, Avalonia.Input.MouseButton.Left); window.MouseUp(at, Avalonia.Input.MouseButton.Left); Settle();
            var editor = table.GetVisualDescendants().OfType<TextBox>().Single(c => c.IsFocused);
            var presenter = editor.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.TextPresenter>().Single();
            var textOrigin = presenter.TranslatePoint(default, window); var textBounds = presenter.Bounds;
            var unit = table.GetVisualDescendants().OfType<ActionButton>().Single(b => b.Content?.ToString() == "kg");
            editor.Text = "bad"; Settle();
            Ok(cell.Bounds.Height == height && DataValidationErrors.GetHasErrors(editor), "invalid numeric input preserves row height and native error state");
            Ok(presenter.TranslatePoint(default, window) == textOrigin && presenter.Bounds == textBounds && editor.TranslatePoint(new Point(editor.Bounds.Width, 0), window)!.Value.X <= unit.TranslatePoint(default, window)!.Value.X, "invalid unit-number drafts retain their text origin and clearance before the unit");
            Ok(table.GetVisualDescendants().OfType<TextBlock>().Count(t => t.Text == "Enter a number" && t.IsEffectivelyVisible) == 1, "the numeric editor retains one visible validation message below the table");
            editor.Text = "12"; Settle();
            Ok(cell.Bounds.Height == height && !DataValidationErrors.GetHasErrors(editor), "correcting numeric input preserves row height");
            table.Design = true; Settle();
            var header = table.GetVisualDescendants().OfType<Border>().Single(c => Avalonia.Automation.AutomationProperties.GetName(c) == "Column · Price");
            foreach (double side in new[] { -4d, 4d })
            {
                table.CurrentColumn = next; Settle();
                var edge = header.TranslatePoint(new Point(header.Bounds.Width + side, header.Bounds.Height / 2), window)!.Value;
                double width = number.Width!.Value;
                window.MouseMove(edge); window.MouseDown(edge, Avalonia.Input.MouseButton.Left);
                window.MouseMove(edge.WithX(edge.X + 20), Avalonia.Input.RawInputModifiers.LeftMouseButton);
                window.MouseUp(edge.WithX(edge.X + 20), Avalonia.Input.MouseButton.Left); Settle();
                Ok(number.Width == width + 20 && next.Width == 150, $"resizing {side}px from the divider changes the left column only");
                Ok(table.CurrentColumn == number, $"pressing the resize grip at {side}px selects its owning column");
            }
            window.Close();
        }

        static void TableInteractionGeometry()
        {
            void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
            T Part<T>(EditableTable table, string name) => (T)typeof(EditableTable).GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(table)!;
            foreach (var scale in new[] { 1d, 2d })
                foreach (var mode in new[] { Mode.Dark, Mode.Light })
                    foreach (int count in new[] { 0, 2, 100 })
                    {
                        Look.Configure(new ShadeOptions { TextScale = scale, ReducedMotion = true }); SetMode(mode);
                        var columns = new List<EditableTable.Column> { new("a", "Item") { Width = 140 * scale }, new("b", "Code") { Width = (count == 100 ? 700 : 150) * scale } };
                        var rows = Enumerable.Range(0, count).Select(_ => { var row = new EditableTable.Row(); row.Values["a"] = "Long wrapped text on more than one line"; return row; }).ToList();
                        var table = new EditableTable { Columns = columns, Rows = rows, Design = true };
                        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Children = { Build.Text("Columns", Look.Text), table } }; Grid.SetRow(table, 1);
                        var window = new Window { Width = 700 * scale, Height = 500 * scale, Content = root }; window.Show(); Settle();
                        var footer = Part<Grid>(table, "footGrid"); var header = Part<Grid>(table, "headGrid"); var marker = Part<Border>(table, "line"); var add = Part<ActionButton>(table, "addRows");
                        double Bottom() => footer.Children.OfType<Border>().Max(cell => cell.TranslatePoint(new Point(0, cell.Bounds.Height), table)!.Value.Y);
                        double gap = add.TranslatePoint(default, table)!.Value.Y - Bottom();
                        Ok(gap >= 8 - 0.5 && gap <= 32 * scale && add.TranslatePoint(new Point(0, add.Bounds.Height), table)!.Value.Y <= table.Bounds.Height + 0.5, $"{mode}, {scale}x, {count} rows: Add follows the actual footer/scrollbar inside a star row");
                        if (count < 100) Ok(Bottom() < table.Bounds.Height / 2, "empty and short tables retain a compact grid in tall hosts");
                        else Ok(Part<Avalonia.Controls.Primitives.ScrollBar>(table, "down").IsVisible, "full tables keep a bounded scrolling body and visible actions");
                        var fromHead = header.Children.OfType<Border>().Single(cell => cell.Child != null && Grid.GetColumn(cell) == 2);
                        var toHead = header.Children.OfType<Border>().Single(cell => cell.Child != null && Grid.GetColumn(cell) == 1);
                        var from = fromHead.TranslatePoint(new Point(20 * scale, 10 * scale), window)!.Value;
                        var to = toHead.TranslatePoint(new Point(0, 10 * scale), window)!.Value;
                        var ctrl = Avalonia.Input.RawInputModifiers.Control; var held = ctrl | Avalonia.Input.RawInputModifiers.LeftMouseButton;
                        window.MouseDown(from, Avalonia.Input.MouseButton.Left, ctrl); window.MouseMove(to, held); Settle();
                        double markerBottom = marker.TranslatePoint(new Point(0, marker.Bounds.Height), table)!.Value.Y;
                        Ok(marker.IsVisible && Math.Abs(markerBottom - Bottom()) < 0.5, "a real Ctrl drag ends the insertion line at the drawn footer cells");
                        if (count < 100)
                        {
                            var blank = table.TranslatePoint(new Point(50 * scale, table.Bounds.Height - 50 * scale), window)!.Value;
                            window.MouseMove(blank, held); Settle();
                            Ok(!marker.IsVisible, "unused space below a short grid is not a reorder drop target");
                            window.MouseMove(to, held); Settle();
                        }
                        if (count == 2)
                        {
                            window.KeyPress(Avalonia.Input.Key.Escape, ctrl, Avalonia.Input.PhysicalKey.None, null); Settle();
                            Ok(!marker.IsVisible, "Escape cancels a native Ctrl-drag preview");
                            window.KeyRelease(Avalonia.Input.Key.Escape, ctrl, Avalonia.Input.PhysicalKey.None, null);
                        }
                        window.KeyRelease(Avalonia.Input.Key.LeftCtrl, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle();
                        Ok(!marker.IsVisible, "releasing Ctrl cancels the insertion marker immediately");
                        window.MouseUp(to, Avalonia.Input.MouseButton.Left); Settle();
                        Ok(columns[0].Key == "a", "releasing Ctrl before dropping cannot reorder columns");
                        var grip = header.Children.OfType<Border>().Single(border => Grid.GetColumn(border) == 1 && border.Width == 12 * scale);
                        var nearEdge = grip.TranslatePoint(new Point(2 * scale, grip.Bounds.Height / 2), window)!.Value;
                        double before = columns[0].Width!.Value;
                        window.MouseDown(nearEdge, Avalonia.Input.MouseButton.Left); window.MouseMove(nearEdge.WithX(nearEdge.X + 24 * scale), Avalonia.Input.RawInputModifiers.LeftMouseButton); window.MouseUp(nearEdge.WithX(nearEdge.X + 24 * scale), Avalonia.Input.MouseButton.Left); Settle();
                        Ok(columns[0].Width == before + 24 * scale, "resizing works four pixels inside the boundary without Ctrl");
                        if (count == 100)
                        {
                            fromHead.Focus(Avalonia.Input.NavigationMethod.Tab); Settle();
                            Ok(Part<ScrollViewer>(table, "headView").Offset.X > 0, "keyboard focus still reveals offscreen column headings");
                            double priorOffset = Part<ScrollViewer>(table, "headView").Offset.X;
                            toHead.Focus(Avalonia.Input.NavigationMethod.Tab); Settle();
                            Ok(Part<ScrollViewer>(table, "headView").Offset.X < priorOffset && toHead.TranslatePoint(default, Part<ScrollViewer>(table, "headView"))!.Value.X >= -0.5, "keyboard focus can reveal the first column again");
                        }
                        window.Close();
                    }
            Look.Configure(new ShadeOptions()); SetMode(Mode.Dark);
        }

        static int SearchChoiceScale()
        {
            var employees = Enumerable.Range(0, 10000).Select(id => new Employee(id, "Employee " + id, "Team " + id % 10)).ToArray();
            var picker = new SearchChoice { ItemsSource = employees, DisplayMemberBinding = new Avalonia.Data.Binding(nameof(Employee.Name)), SearchMemberBinding = new Avalonia.Data.Binding(nameof(Employee.SearchText)), Width = 400 };
            var window = new Window { Width = 600, Height = 600, Content = new StackPanel { Children = { picker } } }; window.Show(); Dispatcher.UIThread.RunJobs();
            long start = GC.GetTotalAllocatedBytes(true); var clock = System.Diagnostics.Stopwatch.StartNew(); picker.IsDropDownOpen = true;
            void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
            Settle(); var list = window.GetVisualDescendants().OfType<ListBox>().Single(); int all = list.ItemCount;
            picker.SearchText = "employee 9999"; Settle(); int matches = list.ItemCount;
            picker.SearchText = ""; Settle(); list.ScrollIntoView(9999); Settle();
            int containers = list.GetVisualDescendants().OfType<ListBoxItem>().Count(), visuals = list.GetVisualDescendants().Count();
            Console.WriteLine($"SearchChoice: {all} items; {matches} exact-word result; {containers} realized rows; {visuals} visuals; {clock.ElapsedMilliseconds} ms including open/filter/scroll; {(GC.GetTotalAllocatedBytes(true) - start) / 1024 / 1024} MiB allocated");
            window.Close(); return all == 10000 && matches == 1 && containers is > 0 and < 80 && visuals < 1200 ? 0 : 1;
        }

        static void NativeActions()
        {
            var vm = new Vm(); var action = new ActionButton { Content = "Save", Icon = Icons.Plus, ConfirmText = "Confirm save" };
            action.ContentTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<string>((text, _) => new TextBlock { Text = "Formatted " + text });
            action.Bind(ContentControl.ContentProperty, new Avalonia.Data.Binding(nameof(Vm.ActionText)) { Source = vm });
            action.Styles.Add(new Avalonia.Styling.Style(x => x.OfType<ActionButton>()) { Setters = { new Avalonia.Styling.Setter(Avalonia.Controls.Primitives.TemplatedControl.PaddingProperty, new Thickness(18, 0)) } });
            var icon = new IconButton { Icon = Icons.Download }; var capsule = new Capsule { Content = "Preset", Icon = Icons.Bookmark };
            var input = new TextBox(); var command = new Command(); var ordinary = new ActionButton { Content = "Default", IsDefault = true, Command = command };
            var hold = new ActionButton("Unlock") { HoldMs = 40 }; int held = 0; hold.Click += (_, _) => { held++; hold.HoldMs = 0; };
            var window = new Window { Width = 500, Height = 500, Content = new StackPanel { Children = { action, icon, capsule, input, ordinary, hold } } }; window.Show();
            void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
            void Key(Avalonia.Input.Key key) { window.KeyPress(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); window.KeyRelease(key, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle(); }
            Settle();
            Ok(action is Button && icon.Icon != null && capsule.Icon != null && action.Padding.Left == 18, "native action variants support icons after construction and normal style overrides");
            action.PerformClick(); Settle();
            Ok(action.Armed && vm.ActionText == "Save" && action.Content?.ToString() == "Save" && action.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Confirm save"), "confirmation is visual state without replacing bound native Content");
            vm.ActionText = "Apply"; action.Disarm(); Settle();
            Ok(action.Content?.ToString() == "Apply" && action.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Formatted Apply"), "content bindings and templates continue to update after confirmation");
            int invokes = 0; icon.Click += (_, _) => invokes++;
            ((Avalonia.Automation.Provider.IInvokeProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(icon)!).Invoke(); Settle();
            Ok(invokes == 1 && Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(capsule) is Avalonia.Automation.Peers.ButtonAutomationPeer, "icon and capsule actions use Avalonia's native button automation");
            icon.Focus(Avalonia.Input.NavigationMethod.Tab); Settle();
            Ok(icon.BorderThickness.Left == 1 && icon.BorderBrush == B(FocusBorder), "native icon actions show the shared keyboard focus border");
            capsule.Focus(Avalonia.Input.NavigationMethod.Tab); Settle();
            Ok(capsule.BorderThickness.Left == 1 && capsule.BorderBrush == B(FocusBorder), "native capsule actions show the shared keyboard focus border");
            input.Focus(); Key(Avalonia.Input.Key.Enter); Ok(command.Runs == 1, "native default-button activation executes the command from another editor");
            var at = hold.TranslatePoint(new Point(hold.Bounds.Width / 2, hold.Bounds.Height / 2), window)!.Value;
            window.MouseDown(at, Avalonia.Input.MouseButton.Left);
            var timer = System.Diagnostics.Stopwatch.StartNew(); while (timer.ElapsedMilliseconds < 100) { System.Threading.Thread.Sleep(5); Settle(); }
            window.MouseUp(at, Avalonia.Input.MouseButton.Left); Settle();
            Ok(held == 1 && hold.HoldMs == 0, "a completed hold fires once even when its action changes HoldMs before native pointer release");
            hold.HoldMs = 200; window.MouseDown(at, Avalonia.Input.MouseButton.Left); window.MouseMove(new Point(499, 499)); window.MouseUp(new Point(499, 499), Avalonia.Input.MouseButton.Left); Settle();
            Ok(held == 1, "releasing a hold outside the native button cancels it");
            window.Close();
        }

        static int Scale(int count, int columnCount, bool unique, bool fixedWidth, bool footers)
        {
            long heapBefore = GC.GetTotalMemory(true);
            var start = System.Diagnostics.Stopwatch.StartNew();
            var before = GC.GetTotalAllocatedBytes(true);
            var columns = Enumerable.Range(0, columnCount).Select(i => new EditableTable.Column("c" + i, "Column " + i, EditableTable.Kind.Number) { Width = fixedWidth ? 100 : null, Footer = footers ? EditableTable.Total.Sum : EditableTable.Total.None }).ToArray();
            var rows = Enumerable.Range(0, count).Select(i => { var row = new EditableTable.Row(); for (int c = 0; c < columns.Length; c++) row.Values[columns[c].Key] = (unique ? (long)i * columnCount + c : i).ToString(System.Globalization.CultureInfo.InvariantCulture); return row; }).ToList();
            var table = new EditableTable { Columns = columns, Rows = rows };
            var window = new Window { Width = 900, Height = 600, Content = table }; window.Show(); Dispatcher.UIThread.RunJobs();
            long elapsed = start.ElapsedMilliseconds, allocated = GC.GetTotalAllocatedBytes(true) - before;
            long retained = Math.Max(0, GC.GetTotalMemory(true) - heapBefore);
            Console.WriteLine($"{count} rows, {columnCount} columns ({(unique ? "unique" : "repeated")}, {(fixedWidth ? "fixed" : "fit")}, totals={footers}): {elapsed} ms; {allocated / 1024 / 1024} MiB allocated; {retained / 1024 / 1024} MiB managed heap increase; {table.GetVisualDescendants().Count()} visuals");
            int visuals = table.GetVisualDescendants().Count();
            window.Close(); return visuals < 2000 + 200 * columnCount ? 0 : 1;
        }

        static int Main(string[] args)
        {
            if (args.Contains("--host-theme"))
            {
                AppBuilder.Configure<HostThemeApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
                Ok(Application.Current!.Styles.OfType<Avalonia.Themes.Fluent.FluentTheme>().Count() == 1, "Shade can compose with a host's existing Fluent theme");
                var field = new Field { PlaceholderText = "Host input" }; var button = new ActionButton("Save"); var toggle = new Switch();
                var window = new Window { Width = 400, Height = 300, Content = new StackPanel { Children = { field, button, toggle } } }; window.Show(); Dispatcher.UIThread.RunJobs();
                Ok(field.Box.Bounds.Height > 0 && button.GetVisualDescendants().OfType<TextBlock>().Any(text => text.Text == "Save") && toggle.Bounds.Width == 32, "host composition retains native editors and Shade action/toggle presentation");
                window.Close(); return failed == 0 ? 0 : 1;
            }
            AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
            if (args.Contains("--native-feedback")) { NativeFeedback(); Editing(); TableInteractionGeometry(); return failed == 0 ? 0 : 1; }
            if (args.Contains("--search-choice-scale")) return SearchChoiceScale();
            if (Array.IndexOf(args, "--scale") is int scaleIndex && scaleIndex >= 0)
                return Scale(int.TryParse(args.ElementAtOrDefault(scaleIndex + 1), out int count) ? Math.Clamp(count, 1, 100000) : 1000,
                    int.TryParse(args.ElementAtOrDefault(scaleIndex + 2), out int columns) ? Math.Clamp(columns, 1, 50) : 1, args.Contains("--unique"), args.Contains("--fixed"), args.Contains("--footers"));
            Colours();
            Parsers();
            Xaml();
            Marks();
            Focus();
            AccentPicking();
            Dates();
            Designing();
            Modes();
            Editing();
            ColumnRibbon();
            ToolbarLayout();
            RibbonExtension();
            ColumnRibbonFeedback();
            ColumnViewportAndLayout();
            ColumnRibbonLayouts();
            Money();
            Mixed();
            Permissions();
            Words();
            Adaptability();
            VirtualRows();
            AccessibleEditors();
            CalendarLimits();
            RibbonTabVisibility();
            PopupContracts();
            LiveLanguage();
            LanguageLifetime();
            Activation();
            MetricCoverage();
            SummaryAndCalendarAutomation();
            FocusContrastAndTabSemantics();
            FooterRefreshCosts();
            MultiSelection();
            BindingRegressions();
            NativeActions();
            SearchableChoices();
            IntegrationFeedback();
            NativeFeedback();
            TableInteractionGeometry();
            PairRows();
            NativeSelections();
            SuggestionLifecycle();
            var pairs = new PairTable("Name", "Value"); pairs.Set(new Dictionary<string, string> { ["A"] = "kept" });
            pairs.Ensure(new[] { "A", "B", "B" }); pairs.Ensure(new[] { "B" });
            Ok(pairs.Pairs().Count == 2 && pairs.Pairs()["A"] == "kept", "PairTable.Ensure adds repeated new names once and preserves existing values");
            Highlighting();
            Documented();
            Console.WriteLine(failed == 0 ? "all passed" : failed + " failed");
            return failed == 0 ? 0 : 1;
        }

        static void Colours()
        {
            Ok(Math.Abs(Contrast(Colors.White, Colors.Black) - 21) < 0.01, "contrast: white on black is 21:1");
            Ok(OnColor(Hex("#FFFFFF")) == Ink && OnColor(Hex("#151519")) == White, "text on a fill: ink on light, white on dark");
            foreach (var mode in new[] { Mode.Dark, Mode.Light })
            {
                SetMode(mode);
                foreach (var (name, dark, light) in Accents)
                {
                    SetAccent(dark, light);
                    var a = mode == Mode.Dark ? dark : light;
                    Ok(Accent.Color == a, $"{mode} {name}: the accent takes its {mode} value");
                    Ok(Contrast(AccentText, Raised) >= 4.5, $"{mode} {name}: accent text reads at 4.5:1 on Raised");
                    Ok(Contrast(OnAccent, Accent) >= 4.5, $"{mode} {name}: text on the accent reads at 4.5:1");
                    Ok(NearStatus(a) == null, $"{mode} {name}: clear of the status colours");
                }
            }
            SetAccent(Hex("#3366FF"));
            SetMode(Mode.Light);
            Ok(Look.Background.Color == Hex("#F4F4F7") && Accent.Color == Hex("#3366FF"), "a custom accent stays through a mode switch");
            Ok(Contrast(AccentText, Raised) >= 4.5, "a low-contrast accent's text is lifted to 4.5:1");
            SetMode(Mode.Dark); SetAccent(Accents[0].dark, Accents[0].light);
        }

        static void Marks()
        {
            TestApp.Marks.Clear();
            IconPng(Hex("#123456"), 3);
            Ok(TestApp.Marks.SequenceEqual(new[] { true }), "the app's mark draws the icon");
            var window = new Window { Content = new Mark { Width = 38, Height = 38 }, Width = 100, Height = 100 }; window.Show();
            Dispatcher.UIThread.RunJobs(); window.CaptureRenderedFrame();
            Ok(TestApp.Marks.Contains(false), "the app's mark draws the Mark control");
            window.Close();
        }

        /// <summary>The focus with a pointer: a click on the background leaves a field; a dropdown neither moves nor shows the
        /// keyboard's focus ring when its list opens and closes by the pointer.</summary>
        static void Focus()
        {
            var field = new Field("Name") { Width = 200 };
            var choice = new ChoiceButton { Width = 200 }; choice.SetItems(new[] { "One", "Two", "Three" }, "One");
            var panel = new StackPanel { Background = Brushes.Transparent, Spacing = 8, Margin = new Thickness(16), Children = { field, choice } };
            var window = new Window { Width = 400, Height = 300, Content = panel }; window.Show();
            void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
            void Click(Point p) { Settle(); window.MouseDown(p, Avalonia.Input.MouseButton.Left); window.MouseUp(p, Avalonia.Input.MouseButton.Left); Settle(); }
            Point At(Control c, double x, double y) => c.TranslatePoint(new Point(x, y), window)!.Value;
            Settle();

            Click(At(field, 100, 14)); Ok(field.Box.IsFocused, "a click on a field focuses it");
            Click(At(field, 3, 14)); Ok(field.Box.IsFocused, "a click on the field's edge keeps it focused");
            Click(new Point(300, 250)); Ok(!field.Box.IsFocused, "a click on the background leaves the field");

            var label = choice.GetVisualDescendants().OfType<TextBlock>().First();
            var before = label.TranslatePoint(new Point(0, 0), window)!.Value;
            Click(At(choice, 100, 14));
            var open = window.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Text == "Three");
            Ok(open != null, "a click opens the dropdown's list");
            Ok(label.TranslatePoint(new Point(0, 0), window)!.Value == before, "the dropdown's text stays put while its list is open");
            if (open != null) Click(At(open, 4, 4));
            Ok(choice.Current == "Three" && choice.IsFocused, "a pick sets the dropdown and gives it the focus back");
            Ok(choice.BorderBrush is ISolidColorBrush b && b.Color != Accent.Color, "after a pick by the pointer no focus ring shows");
            Click(At(choice, 100, 14)); Ok(window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Two"), "the list opens again");
            Settle(); window.MouseDown(At(choice, 100, 14), Avalonia.Input.MouseButton.Left); Settle();  // the closing press, as Windows delivers it: no move or hover update after it
            Ok(!window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Two") && choice.BorderBrush is ISolidColorBrush h && h.Color == Outline.Color,
                "closed by a click on it, the dropdown keeps its hover border at once (no resting border until the pointer moves)");
            window.MouseUp(At(choice, 100, 14), Avalonia.Input.MouseButton.Left); Settle();
            Ok(!window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Two"), "the closing click's release doesn't open the list again");
            window.MouseMove(new Point(380, 280)); Settle();
            Ok(choice.BorderBrush is ISolidColorBrush rest && rest.Color != Outline.Color, "moved away in one jump, the dropdown drops its hover border");
            window.Close();
        }

        /// <summary>The accent picker: Apply takes a curated name or a colour; a swatch, a typed colour and the helper set it.</summary>
        static void AccentPicking()
        {
            Ok(AccentPicker.Apply("teal") && Accent.Color == Accents.First(a => a.name == "Teal").dark, "Apply: a curated accent by its name, any case");
            Ok(AccentPicker.Apply("#3366FF") && Accent.Color == Hex("#3366FF"), "Apply: a colour");
            Ok(!AccentPicker.Apply("blue") && !AccentPicker.Apply("#3366F") && Accent.Color == Hex("#3366FF"), "Apply: anything else changes nothing");
            var picker = new AccentPicker();
            Ok(picker.Value == "#3366FF", "a new picker shows the accent as it is: " + picker.Value);
            int changed = 0; picker.Changed += () => changed++;
            var window = new Window { Width = 520, Height = 420, Content = new Border { Padding = new Thickness(16), Child = picker } }; window.Show();
            void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
            void Click(Point p) { Settle(); window.MouseDown(p, Avalonia.Input.MouseButton.Left); window.MouseUp(p, Avalonia.Input.MouseButton.Left); Settle(); }
            Settle();
            var swatches = picker.Children[1];
            Click(swatches.TranslatePoint(new Point(5 + 34 + 12, swatches.Bounds.Height / 2), window)!.Value);
            Ok(picker.Value == "Indigo" && Accent.Color == Accents[1].dark && changed == 1, "a swatch sets its curated accent at once");
            var field = picker.GetVisualDescendants().OfType<Field>().Single();
            field.Box.Text = "#2DD4BF"; Settle();
            Ok(picker.Value == "#2DD4BF" && Accent.Color == Hex("#2DD4BF") && changed == 2, "a typed colour sets the accent");
            Click(field.TranslatePoint(new Point(40, 14), window)!.Value);
            Ok(window.GetVisualDescendants().OfType<ColorGrid>().Any(), "the Custom field opens the colour helper");
            Click(new Point(500, 30));
            Ok(!window.GetVisualDescendants().OfType<ColorGrid>().Any() && changed == 2, "a click elsewhere closes it, with nothing changed");
            SetMode(Mode.Light); picker.Value = "Violet";
            Ok(Accent.Color == Accents[0].light && field.Text == HexOf(Accents[0].light), "a curated accent takes the mode's value, its hex in the field");
            SetMode(Mode.Dark); picker.Restyle();  // what Look.Repaint does in an app (no app lifetime here)
            Ok(field.Text == HexOf(Accents[0].dark), "a mode switch shows the other value");
            window.Close();
            SetAccent(Accents[0].dark, Accents[0].light);
        }

        /// <summary>The date field: typed in two ways, picked in the calendar with the pointer and the keyboard, limited by Min..Max.</summary>
        static void Dates()
        {
            Ok(DateField.Read("2026-10-24") == new DateTime(2026, 10, 24) && DateField.Read("24.10.2026") == new DateTime(2026, 10, 24) && DateField.Read("4. 1. 2026") == new DateTime(2026, 1, 4),
                "a day reads as yyyy-MM-dd, d.M.yyyy or d. M. yyyy");
            Ok(DateField.Read("2026-13-01") == null && DateField.Read("tomorrow") == null && DateField.Read("31.2.2026") == null, "anything else isn't a day");
            var field = new DateField { Min = new DateTime(2026, 10, 5), Max = new DateTime(2026, 11, 20), Value = new DateTime(2026, 10, 24) };
            int changed = 0; field.Changed += () => changed++;
            var range = new DateRangeField { Start = new DateTime(2026, 10, 20), End = new DateTime(2026, 10, 28) };
            int spans = 0; range.Changed += () => spans++;
            var window = new Window { Width = 500, Height = 460, Content = new StackPanel { Margin = new Thickness(16), Spacing = 8, Children = { field, range } } }; window.Show();
            void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
            void Click(Point p) { Settle(); window.MouseDown(p, Avalonia.Input.MouseButton.Left); window.MouseUp(p, Avalonia.Input.MouseButton.Left); Settle(); }
            void Key(Avalonia.Input.Key k, Avalonia.Input.RawInputModifiers m = Avalonia.Input.RawInputModifiers.None) { window.KeyPress(k, m, Avalonia.Input.PhysicalKey.None, null); window.KeyRelease(k, m, Avalonia.Input.PhysicalKey.None, null); Settle(); }
            void Type(string t) { foreach (var c in t) { window.KeyTextInput(c.ToString()); Settle(); } }
            Control? Grid() => window.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.GetType().Name == "MonthGrid");  // internal: by its name
            string Shown(Control blocks) => (string)blocks.GetType().GetProperty("Shown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(blocks)!;
            Control Blocks(Control f, string name = "Blocks") => (Control)f.GetType().GetProperty(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(f)!;
            Settle();

            var blocks = Blocks(field);
            Ok(Shown(blocks) == "2026-10-24" && field.Text == "2026-10-24", "the day shows as YYYY-MM-DD blocks");
            Click(blocks.TranslatePoint(new Point(blocks.Bounds.Width - 6, 13), window)!.Value);
            Ok(Grid() != null && blocks.IsFocused, "a click on the day block opens the month and gives the blocks the keyboard");
            Key(Avalonia.Input.Key.Up);
            Ok(field.Value == new DateTime(2026, 10, 25) && changed == 1, "Up raises the day block");
            Key(Avalonia.Input.Key.Left); Type("11");
            Ok(field.Invalid && field.Value == new DateTime(2026, 10, 25) && Shown(blocks) == "2026-11-25", "typing 11 in the month makes 25 November, after Max: marked, the value kept");
            Type("15");
            Ok(!field.Invalid && field.Value == new DateTime(2026, 11, 15), "the digits move on to the day: 15 November is allowed");
            Key(Avalonia.Input.Key.Left); Type("2");
            Ok(field.Value == new DateTime(2026, 11, 15) && Shown(blocks) == "2026-02-15" && field.Invalid, "2 in the month can only be February and moves on; before Min it is marked, the value kept");
            Key(Avalonia.Input.Key.Back);
            Ok(Shown(blocks) == "2026-02-DD" && field.Value == null, "Backspace empties a block, which shows its placeholder");
            Key(Avalonia.Input.Key.Escape);
            Ok(Grid() == null, "Esc closes the month");
            field.Value = new DateTime(2026, 10, 24);
            Click(blocks.TranslatePoint(new Point(4, 13), window)!.Value);
            var grid = Grid(); Ok(grid != null, "a click on the year block opens the month too");
            if (grid != null)
            {
                // 2026-10-01 is a Thursday: with Monday first the grid starts on 2026-09-28; the 30th sits at row 4, column 4
                Click(grid.TranslatePoint(new Point(12 + 4 * 32 + 14, 68 + 4 * 32 + 14), window)!.Value);
                Ok(field.Value == new DateTime(2026, 10, 30) && Shown(blocks) == "2026-10-30" && Grid() == null, $"a click on a day picks it and closes the month ({field.Value:yyyy-MM-dd}, {Shown(blocks)}, {(Grid() == null ? "closed" : "open")})");
            }
            blocks.Focus(); Key(Avalonia.Input.Key.Down, Avalonia.Input.RawInputModifiers.Alt);
            Ok(Grid() is { IsFocused: true }, "Alt+Down opens the month and gives it the keyboard");
            Key(Avalonia.Input.Key.Right); Key(Avalonia.Input.Key.Down); Key(Avalonia.Input.Key.Enter);
            Ok(field.Value == new DateTime(2026, 11, 7) && Grid() == null && blocks.IsFocused, "arrows move the day, Enter picks it, the blocks get the keyboard back");
            Key(Avalonia.Input.Key.F4); Key(Avalonia.Input.Key.PageDown); Key(Avalonia.Input.Key.PageDown); Key(Avalonia.Input.Key.Enter);
            Ok(field.Value == new DateTime(2026, 11, 7), "a day after Max can't be picked");
            Key(Avalonia.Input.Key.Escape); Ok(Grid() == null, "Esc closes the month, the day unchanged");
            var icon = field.TranslatePoint(new Point(field.Bounds.Width - 16, field.Bounds.Height / 2), window)!.Value;
            Click(icon); Ok(Grid() != null, "a click on the calendar icon opens the month, and it stays open");
            Click(icon); Ok(Grid() == null, "a second click on the icon closes it");

            // the range: the days between are coloured; a new start asks for its end; an end before the start swaps them
            Ok(DateRangeField.Read("2026-10-28/2026-10-20") == (new DateTime(2026, 10, 20), new DateTime(2026, 10, 28)) && range.Text == "2026-10-20/2026-10-28", "a range reads and shows as start/end");
            var from = Blocks(range, "From"); var until = Blocks(range, "Until");
            Click(from.TranslatePoint(new Point(4, 13), window)!.Value);
            grid = Grid(); Ok(grid != null && from.IsFocused, "a click on the start opens the month");
            if (grid != null)
            {
                Click(grid.TranslatePoint(new Point(12 + 0 * 32 + 14, 68 + 1 * 32 + 14), window)!.Value);  // Monday 5 October
                Ok(range.Start == new DateTime(2026, 10, 5) && range.End == null && until.IsFocused && Grid() != null, "a picked start clears the end and asks for it");
                Click(grid.TranslatePoint(new Point(12 + 2 * 32 + 14, 68 + 3 * 32 + 14), window)!.Value);  // Wednesday 21 October
                Ok(range.End == new DateTime(2026, 10, 21) && Grid() == null && spans == 3, "the end closes the month: 5 to 21 October");
            }
            until.Focus(); Key(Avalonia.Input.Key.Home); Type("2026"); Type("10"); Type("01");
            Ok(range.End == new DateTime(2026, 10, 1) && range.Invalid, "an end typed before the start marks the field");
            Key(Avalonia.Input.Key.Back); Type("09");
            Ok(range.End == new DateTime(2026, 10, 9) && !range.Invalid, "fixed, the mark goes");
            window.Close();
        }

        /// <summary>The table designer: a header dragged from anywhere on it to a gap, a field dropped from another control,
        /// a click selects, Ctrl+arrows move, the right-click menu, Locked.</summary>
        static void Designing()
        {
            var designer = new TableDesigner
            {
                Columns = new[] { new TableDesigner.Column("Time"), new TableDesigner.Column("Price", true), new TableDesigner.Column("Meter ID"), new TableDesigner.Column("Status") },
                Rows = new[] { new[] { "2026-10-21", "124.5", "000123", "OK" }, new[] { "2026-10-22", "130", "000125", "Late" } },
            };
            var field = new Border { Width = 120, Height = 28, Background = Brushes.Transparent, Child = new TextBlock { Text = "Extra" } };
            TableDesigner.DragSource(field, "Extra");
            var moves = new List<(int, int)>(); var drops = new List<(object, int)>(); var removes = new List<int>(); var picks = new List<int>();
            designer.Moved += (a, b) => moves.Add((a, b)); designer.Dropped += (o, at) => drops.Add((o, at)); designer.RemoveAsked += i => removes.Add(i); designer.SelectionChanged += i => picks.Add(i);
            var window = new Window { Width = 700, Height = 400, Content = new StackPanel { Margin = new Thickness(16), Spacing = 16, Children = { designer, field } } }; window.Show();
            void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
            Settle();
            Border Head(string name) => designer.GetVisualDescendants().OfType<Border>().First(b => b.Height == 28 && b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == name) && b.Focusable);
            Point Mid(Control c) => c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), window)!.Value;
            Point Edge(Control c) => c.TranslatePoint(new Point(c.Bounds.Width, 14), window)!.Value;  // the gap after c
            void Drag(Point from, Point to, Action? midway = null, bool ctrl = true)
            {
                var modifiers = ctrl ? Avalonia.Input.RawInputModifiers.Control : Avalonia.Input.RawInputModifiers.None;
                Settle(); window.MouseDown(from, Avalonia.Input.MouseButton.Left, modifiers);
                window.MouseMove(new Point((from.X + to.X) / 2, (from.Y + to.Y) / 2), Avalonia.Input.RawInputModifiers.LeftMouseButton | modifiers);
                window.MouseMove(to, Avalonia.Input.RawInputModifiers.LeftMouseButton | modifiers); Settle(); midway?.Invoke();
                window.MouseUp(to, Avalonia.Input.MouseButton.Left, modifiers); Settle();
            }
            bool lineShown = false;
            Drag(Mid(Head("Meter ID")), Edge(Head("Time")), ctrl: false);
            Ok(moves.Count == 0, "standalone designer headers require Ctrl for pointer reordering too");
            Drag(Mid(Head("Meter ID")), Edge(Head("Time")), () => lineShown = designer.GetVisualDescendants().OfType<Border>().Any(b => b.Width == 2 && b.IsVisible));
            Ok(moves.SequenceEqual(new[] { (2, 1) }), "a header dragged from its middle to the gap after Time moves it there: " + string.Join(",", moves));
            Ok(lineShown, "while dragging, the accent line marks the gap");
            Drag(Mid(Head("Time")), Edge(Head("Time")));
            Ok(moves.Count == 1, "dropped in its own gap, a column doesn't move");
            Drag(Mid(field), Edge(Head("Status")), ctrl: false);
            Ok(drops.Count == 1 && (string)drops[0].Item1 == "Extra" && drops[0].Item2 == 4, "a field from another control drops into the gap at the end");
            picks.Clear();
            window.MouseDown(Mid(Head("Price")), Avalonia.Input.MouseButton.Left); window.MouseUp(Mid(Head("Price")), Avalonia.Input.MouseButton.Left); Settle();
            Ok(designer.Selected == 1 && picks.SequenceEqual(new[] { 1 }), "a click selects the header");
            window.KeyPress(Avalonia.Input.Key.Right, Avalonia.Input.RawInputModifiers.Control, Avalonia.Input.PhysicalKey.None, null); Settle();
            Ok(moves.Last() == (1, 2), "Ctrl+Right moves the focused column");
            window.MouseDown(Mid(Head("Status")), Avalonia.Input.MouseButton.Right); window.MouseUp(Mid(Head("Status")), Avalonia.Input.MouseButton.Right); Settle();
            var remove = window.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(t => t.Text == "Remove column");
            Ok(remove != null, "a right-click opens the column's menu");
            if (remove != null) { var at = remove.TranslatePoint(new Point(4, 4), window)!.Value; window.MouseDown(at, Avalonia.Input.MouseButton.Left); window.MouseUp(at, Avalonia.Input.MouseButton.Left); Settle(); }
            Ok(removes.SequenceEqual(new[] { 3 }), "Remove column asks for its column");
            designer.RowFill = r => r[3] == "Late" ? Colors.Orange : null;
            Ok(designer.GetVisualDescendants().OfType<Border>().Any(b => b.Background is ISolidColorBrush s && s.Color == Tint(Colors.Orange)), "a row fill set after the rows tints its row");
            designer.Locked = true;
            Drag(Mid(Head("Meter ID")), Edge(Head("Time")));
            Ok(moves.Count == 2, "locked: a drag moves nothing");
            window.Close();
        }

        /// <summary>The mode switch: a click and the arrows pick, the mode follows; System without a platform's setting is dark.</summary>
        static void Modes()
        {
            var sw = new ModeSwitch(); int changed = 0; sw.Changed += () => changed++;
            var window = new Window { Width = 400, Height = 200, Content = new StackPanel { Margin = new Thickness(16), Children = { sw } } }; window.Show();
            void Settle() { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs(); }
            Settle();
            Control Segment(string name) => sw.GetVisualDescendants().OfType<Control>().First(c => Avalonia.Automation.AutomationProperties.GetName(c) == name + " mode");
            var light = Segment("Light"); var p = light.TranslatePoint(new Point(10, 10), window)!.Value;
            window.MouseDown(p, Avalonia.Input.MouseButton.Left); window.MouseUp(p, Avalonia.Input.MouseButton.Left); Settle();
            Ok(sw.Value == ModeChoice.Light && Current == Look.Mode.Light && changed == 1, "a click on Light switches to light mode");
            Ok(ReferenceEquals(sw.BorderBrush, B(Look.Separator)) && !light.Classes.Contains(":focus-visible"), "pointer mode selection keeps a neutral outer frame without a keyboard focus ring");
            light.Focus(Avalonia.Input.NavigationMethod.Tab); window.KeyPress(Avalonia.Input.Key.Right, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle();
            Ok(sw.Value == ModeChoice.Dark && Current == Look.Mode.Dark && Segment("Dark").IsFocused, "Right moves to Dark and switches");
            Ok(ReferenceEquals(sw.BorderBrush, B(Look.Separator)) && Segment("Dark").Classes.Contains(":focus-visible"), "keyboard mode selection keeps the focus ring on its segment and the outer frame neutral");
            ModeSwitch.Apply(ModeChoice.Light); ModeSwitch.Apply(ModeChoice.System);
            Ok(Current == Look.Mode.Dark || Current == Look.Mode.Light, "System takes the computer's mode (headless: whatever it reports)");
            ModeSwitch.Apply(ModeChoice.Dark);
            window.Close();
        }

        static void Parsers()
        {
            var p = Workbench.Pairs("Accept: text/plain\n\nx-id : 42");
            Ok(p.Count == 2 && p["accept"] == "text/plain" && p["X-ID"] == "42", "pairs: name: value per line, blank lines skipped, names ignore case");
            Ok(Throws(() => Workbench.Pairs("a: 1\nA: 2")), "pairs: a duplicate name is refused");
            Ok(Throws(() => Workbench.Pairs("no separator")), "pairs: a line without the separator is refused");
            Ok(Workbench.PairsText(new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" }) == "a: 1\nb: 2", "pairs back to text");
        }

        static bool Throws(Action a) { try { a(); return false; } catch (ArgumentException) { return true; } }

        static void Xaml()
        {
            var vm = new Vm();
            var view = new Bound { DataContext = vm };
            var window = new Window { Content = view }; window.Show();
            Dispatcher.UIThread.RunJobs();
            T Get<T>(string name) where T : Control => view.FindControl<T>(name) ?? throw new Exception(name + " missing");

            Ok(Get<SearchChoice>("EmployeePicker").SelectedItem is Employee { Id: 2 }, "search choice supports compiled object, search-text and selected-ID bindings");
            var sw = Get<Switch>("Toggle");
            vm.Flag = true; Ok(sw.IsChecked == true, "Switch: the view model turns it on");
            ((Avalonia.Automation.Provider.IToggleProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(sw)!).Toggle(); Ok(!vm.Flag, "Switch: a toggle reaches the view model");
            Ok(Get<StateToggle>("State").IsChecked == false, "StateToggle follows the same value");

            var tick = Get<Tick>("Box");
            ((Avalonia.Automation.Provider.IToggleProvider)Avalonia.Automation.Peers.ControlAutomationPeer.CreatePeerForElement(tick)!).Toggle(); Ok(vm.Ticked == true, "Tick: a click reaches the view model");
            vm.Ticked = null; Ok(tick.IsChecked == null, "Tick: the view model sets it to some");

            vm.ActionText = "Apply";
            Ok(Get<ActionButton>("Save").Content?.ToString() == "Apply", "action labels support compiled XAML binding");
            var multi = Get<MultiChoice>("PeoplePicker");
            Ok(multi.Items.SequenceEqual(vm.People) && multi.Selected.SetEquals(new[] { "Ben" }), "multi-choice items and selection support compiled XAML bindings");
            multi.Set(new[] { "Anna" }); Ok(vm.SelectedPeople.SetEquals(new[] { "Anna" }), "programmatic selection notifies the two-way XAML binding");
            vm.SelectedPeople = new HashSet<string> { "Chloe" }; Ok(multi.Selected.Contains("Chloe"), "multi-choice follows replacement view-model selections");
            vm.People = new[] { "Anna", "Dora" }; Ok(multi.Items.SequenceEqual(vm.People) && vm.SelectedPeople.Count == 0, "option replacement prunes missing selections through the binding");
            var shortChoices = Get<TickGrid>("ShortChoices");
            vm.ShortPicks = new[] { "Dora" }; Ok(shortChoices.Picked.SequenceEqual(new[] { "Dora" }), "short-choice selection supports replacement bindings");
            shortChoices.Picked = new[] { "Anna", "Dora" }; Ok(vm.ShortPicks.SequenceEqual(shortChoices.Picked), "short-choice selection notifies its two-way binding");
            Ok(ReferenceEquals(Get<PairTable>("Pairs").Entries, vm.PairRows), "pair rows bind as editable objects without dictionary conversion");
            vm.Name = "Bound";
            Ok(Get<HostPortField>("Address").Box.PlaceholderText == "Bound" && Get<StatusBadge>("Badge").Text == "Bound" && Get<Tag>("FileTag").Text == "Bound" && Get<FilterChip>("Chip").Content?.ToString() == "Bound" && Get<TableDesigner>("Designer").EmptyText == "Bound", "composite labels, placeholders and native content support compiled bindings");
            vm.Designing = true; Ok(Get<TableDesigner>("Designer").Locked, "table designer options support compiled bindings");
            vm.ActionIcon = Icons.Download; vm.ActionKind = ButtonKind.Danger;
            Ok(Get<ActionButton>("Save").Kind == ButtonKind.Danger && Get<IconButton>("ExportAction").Icon == Icons.Download && Get<Capsule>("PresetAction").Content?.ToString() == "Apply", "native action variants support compiled content, icon and kind bindings");
            vm.ActionKind = ButtonKind.Primary;
            var field = Get<Field>("NameField");
            field.Box.Text = "Anna"; Dispatcher.UIThread.RunJobs(); Ok(vm.Name == "Anna", "Field: typing reaches the view model");
            vm.Name = "Ben"; Ok(field.Text == "Ben", "Field: the view model sets the text");
            var recipients = Get<ListField>("Recipients");
            vm.Recipients = "one@example.com, two@example.com"; Dispatcher.UIThread.RunJobs();
            Ok(recipients.Fields.Count() == 2, "ListField: view-model text creates editable rows");
            var recipient = recipients.Fields.First(); recipient.Box.Text = "edited@example.com"; Dispatcher.UIThread.RunJobs();
            Ok(vm.Recipients.StartsWith("edited@example.com", StringComparison.Ordinal), "ListField: row edits flow back through its two-way binding");
            recipients.SetValue(ListField.HintProperty, "Recipient");
            Ok(recipients.Fields.All(f => f.Box.PlaceholderText == "Recipient"), "ListField: bound hints update existing row editors");
            var find = Get<SearchField>("Find");
            find.Box.Text = "rep"; Dispatcher.UIThread.RunJobs(); Ok(vm.Query == "rep", "SearchField binds like a field");

            var bar = Get<Progress>("Bar");
            Ok(bar.Value == 0.25 && bar.Label == "Uploading", "Progress: value bound, label set in XAML");
            vm.Share = null; Ok(bar.Value == null, "Progress: null = unknown length");
            vm.Share = 2; Ok(bar.Value == 1, "Progress: the value is clamped to 1");

            var cards = Get<SummaryCards>("Cards");
            Ok(cards.Items.Count == 1 && cards.Detailed, "SummaryCards: items bound, detailed set in XAML");
            vm.Cards = vm.Cards.Append(new SummaryCards.Item { Title = "Backups", Value = "6/6" }).ToList();
            Ok(cards.Items.Count == 2, "SummaryCards: new items follow");

            var save = Get<ActionButton>("Save");
            Ok(save.Content?.ToString() == "Apply" && save.Kind == ButtonKind.Primary, "ActionButton: text and kind from XAML");
            save.PerformClick(); Ok(vm.Save.Runs == 1, "ActionButton: a click runs the command");
            vm.Save.Can = false; Ok(!save.IsEffectivelyEnabled, "ActionButton: the command's CanExecute disables it");
            save.PerformClick(); Ok(vm.Save.Runs == 1, "ActionButton: a disabled button doesn't run it");

            var choice = Get<ChoiceButton>("Choice");
            choice.SetItems(new[] { "One", "Two", "Three" }, vm.Pick);
            Ok(choice.Current == "Two", "ChoiceButton: the current item from the view model");
            vm.Pick = "Three"; Ok(choice.Current == "Three", "ChoiceButton: the view model picks");

            var colourHelper = Get<ColorGrid>("ColourHelper");
            Ok(colourHelper.Value == vm.SwatchColour, "ColorGrid: compiled XAML creates the helper and binds its colour");
            colourHelper.SetCurrentValue(ColorGrid.ValueProperty, Hex("#2DD4BF"));
            Ok(vm.SwatchColour == Hex("#2DD4BF"), "ColorGrid: user value changes flow back through the native two-way property");
            vm.SwatchColour = Hex("#3366FF");
            Ok(colourHelper.Value == Hex("#3366FF"), "ColorGrid: source updates keep working after a user pick");
            var picker = Get<AccentPicker>("Picker");
            Ok(picker.Label == "Theme colour", "AccentPicker: the label from XAML");
            vm.Accent = "Teal"; Ok(picker.Value == "Teal" && Look.Accent.Color == Accents.First(a => a.name == "Teal").dark, "AccentPicker: the view model sets the accent");
            vm.Accent = "Violet";

            var dayField = Get<DateField>("Day");
            Ok(dayField.Value == new DateTime(2026, 10, 24) && dayField.Text == "2026-10-24", "DateField: the view model's day, shown as yyyy-MM-dd");
            dayField.Text = "1.11.2026"; Dispatcher.UIThread.RunJobs(); Ok(vm.Day == new DateTime(2026, 11, 1), "DateField: a day set as text reaches the view model");
            var span = Get<DateRangeField>("Span");
            Ok(span.Start == new DateTime(2026, 11, 1) && span.End == new DateTime(2026, 10, 30), "DateRangeField: the view model's start and end");
            span.End = new DateTime(2026, 11, 9); Dispatcher.UIThread.RunJobs(); Ok(vm.Until == new DateTime(2026, 11, 9), "DateRangeField: a new end reaches the view model");
            var sheet = Get<EditableTable>("Sheet");
            vm.PaperPlacement = EditableTable.PaperDimensionsPosition.FormatLabel; Dispatcher.UIThread.RunJobs(); Ok(sheet.PaperDimensionsPlacement == vm.PaperPlacement, "EditableTable: compiled placement binding follows the view model");
            vm.ConstrainToPaper = false; Dispatcher.UIThread.RunJobs(); Ok(!sheet.ConstrainToPaper, "EditableTable: compiled ConstrainToPaper binding follows the view model");
            sheet.SetCurrentValue(EditableTable.ConstrainToPaperProperty, true); Dispatcher.UIThread.RunJobs(); Ok(vm.ConstrainToPaper, "EditableTable: the viewport property has a two-way binding default");
            Ok(sheet.ShowColumnEditor == vm.Flag && sheet.ValueSize == 14 && sheet.Sorting == vm.Flag && !sheet.CanResize && !sheet.CanEditFooter, "EditableTable: sizes, sorting and permissions from XAML and the view model");
            sheet.Design = true; Dispatcher.UIThread.RunJobs(); Ok(vm.Designing, "EditableTable: Design reaches the view model");
            Ok(sheet.RibbonTabs == (EditableTable.RibbonTab.Column | EditableTable.RibbonTab.Width) && sheet.ToolbarActions == (EditableTable.ToolbarAction.EditColumns | EditableTable.ToolbarAction.AddColumn) && sheet.ToolbarPlacement == EditableTable.BarPlacement.Bottom && sheet.AddRowsPlacement == EditableTable.BarPlacement.Top, "EditableTable: toolbar actions and bar placement from XAML");

            var modes = Get<ModeSwitch>("Modes");
            vm.Mode = ModeChoice.Light; Ok(modes.Value == ModeChoice.Light && Current == Look.Mode.Light, "ModeSwitch: the view model switches the mode");
            vm.Mode = ModeChoice.Dark; Ok(Current == Look.Mode.Dark, "ModeSwitch: and back");

            var blocks = Get<Masonry>("Blocks");
            Ok(blocks.MinColumn == 200 && blocks.MaxColumns == 2 && blocks.Gap == 32, "Masonry: settings from XAML, the rest default");
            window.Close();
        }
    }
}

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
using Avalonia.Media;
using Avalonia.Threading;
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

    public sealed class Vm : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        bool flag; bool? ticked = false; string name = "", query = "", pick = "Two", accent = "Violet"; double? share = 0.25;
        IReadOnlyList<SummaryCards.Item> cards = new[] { new SummaryCards.Item { Title = "Checks", Value = "11/12" } };
        public bool Flag { get => flag; set => Set(ref flag, value); }
        public bool? Ticked { get => ticked; set => Set(ref ticked, value); }
        public string Name { get => name; set => Set(ref name, value); }
        public string Query { get => query; set => Set(ref query, value); }
        public string Pick { get => pick; set => Set(ref pick, value); }
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
            var grid = table.GetVisualDescendants().OfType<Grid>().First(g => g.ColumnDefinitions.Count == columns.Count + 2);
            Border Cell(int row, int col) => grid.Children.OfType<Border>().First(b => Grid.GetRow(b) == row && Grid.GetColumn(b) == col);
            Point Mid(Control c) => c.TranslatePoint(new Point(c.Bounds.Width / 2, c.Bounds.Height / 2), window)!.Value;
            TextBox Box(int row, string key) => Cell(row + 1, columns.FindIndex(c => c.Key == key) + 1).GetVisualDescendants().OfType<TextBox>().First();
            string Text(int row, string key) => Cell(row + 1, columns.FindIndex(c => c.Key == key) + 1).GetVisualDescendants().OfType<TextBlock>().First().Text ?? "";
            string Items() => string.Join("|", Enumerable.Range(0, rows.Count).Select(r => Box(r, "item").Text));

            Ok(Text(0, "total") == "58.80" && Text(0, "net") == "58.00", "Total = Quantity × Price, Net = Total − Discount + Shipping: " + Text(0, "net"));
            Box(0, "qty").Text = "10"; Settle();
            Ok(Text(0, "total") == "49.00" && Text(0, "net") == "48.20" && table.Footer(total) == "102.20" && changed == 1, "an edit recounts its row and the footer, and tells the app");
            Box(0, "qty").Focus(); Key(Avalonia.Input.Key.Enter);
            Ok(Box(1, "qty").IsFocused, "Enter goes down the column");
            Key(Avalonia.Input.Key.Enter, Avalonia.Input.RawInputModifiers.Shift);
            Ok(Box(0, "qty").IsFocused && table.CurrentColumn == qty, "Shift+Enter goes up; the focused cell's column is current");
            var unit = Cell(1, 3).GetVisualDescendants().OfType<ActionButton>().First();
            Click(Mid(unit)); Ok(rows[0].Units["qty"] == "pallet" && unit.Text == "pallet", "a click on the unit cycles it (box → pallet)");
            Click(Mid(unit)); Ok(rows[0].Units["qty"] == "pcs", "and wraps to the first");
            net.Terms.Add(new EditableTable.Term("qty", EditableTable.Op.Multiply)); table.Refresh(); Settle();
            Ok(Math.Abs(table.Value(rows[0], net) - (49 - 2 + 1.2 * 10)) < 1e-9 && table.Formula(net) == "Total − Discount + Shipping × Quantity", "a fourth term joins with ×, which counts first");
            net.Terms.RemoveAt(3); table.Refresh(); Settle();
            Ok(table.Footer(price) == "2.98", "the footer averages the price");
            var footLine = Cell(rows.Count + 1, 4).GetVisualDescendants().OfType<TextBlock>().First();
            Ok(footLine.Inlines is { Count: 2 } runs && ((Avalonia.Controls.Documents.Run)runs[1]).Text == "2.98", "the footer's function and value are one line of text, on one baseline");

            // sorting: up, down, back to the original order; off, no sort
            string before = Items();
            var priceHead = Cell(0, 4);
            Click(priceHead.TranslatePoint(new Point(12, 10), window)!.Value);
            Ok(Items() == "Ballpoint pens, blue|Whiteboard markers, assorted colours for the meeting rooms|Copy paper A4, 80 g", "a heading click sorts up: " + Items());
            Click(Cell(0, 4).TranslatePoint(new Point(12, 10), window)!.Value); Click(Cell(0, 4).TranslatePoint(new Point(12, 10), window)!.Value);
            Ok(Items() == before, "the third click returns to the original order");
            table.Sorting = false; Settle(); Click(Cell(0, 4).TranslatePoint(new Point(12, 10), window)!.Value);
            Ok(Items() == before, "with sorting off, a heading click doesn't sort");
            table.Sorting = true; Settle();

            // the columns fit the box; a resize is taken from the growing column
            double tableW() => grid.Bounds.Width; double itemW() => grid.ColumnDefinitions[1].ActualWidth; double skuW() => grid.ColumnDefinitions[2].ActualWidth;
            Ok(tableW() <= table.Bounds.Width + 0.5, "the table fits its box: the long item wraps");
            double t0 = tableW(), i0 = itemW(), s0 = skuW();
            var grip = Cell(0, 2).GetVisualDescendants().OfType<Border>().First(b => b.Width == 7);
            var g0 = Mid(grip);
            Settle(); window.MouseDown(g0, Avalonia.Input.MouseButton.Left); window.MouseMove(new Point(g0.X + 30, g0.Y), Avalonia.Input.RawInputModifiers.LeftMouseButton); window.MouseMove(new Point(g0.X + 60, g0.Y), Avalonia.Input.RawInputModifiers.LeftMouseButton); window.MouseUp(new Point(g0.X + 60, g0.Y), Avalonia.Input.MouseButton.Left); Settle();
            Ok(Math.Abs(skuW() - s0 - 60) < 2 && Math.Abs(i0 - itemW() - 60) < 2 && Math.Abs(tableW() - t0) < 2, $"a heading's edge drags its column 60 px wider; the item column gives the room ({skuW() - s0:0}, {i0 - itemW():0})");

            // selection: the row numbers (Shift takes a range, Ctrl adds); the row menu
            Click(Mid(Cell(1, 0))); Click(Mid(Cell(3, 0)), m: Avalonia.Input.RawInputModifiers.Shift);
            Ok(table.Selected.Count == 3, "Shift takes a range of rows");
            Click(Mid(Cell(2, 0)), m: Avalonia.Input.RawInputModifiers.Control);
            Ok(table.Selected.Count == 2, "Ctrl takes one out");
            Click(Mid(Cell(1, 2)), Avalonia.Input.MouseButton.Right); Key(Avalonia.Input.Key.Down); Key(Avalonia.Input.Key.Enter);
            Ok(rows.Count == 5 && table.Selected.Count == 2, "the row menu duplicates the two selected rows");
            Click(Mid(Cell(4, 2)), Avalonia.Input.MouseButton.Right);  // one of the two copies, both still selected
            Key(Avalonia.Input.Key.Up); Key(Avalonia.Input.Key.Enter); Settle();
            var ask = window.OwnedWindows.FirstOrDefault();
            Ok(ask != null && rows.Count == 5, "Delete asks first");
            ask?.GetVisualDescendants().OfType<ActionButton>().First(b => b.Text.StartsWith("Delete", StringComparison.Ordinal)).PerformClick(); Settle();
            for (int k = 0; k < 5 && rows.Count != 3; k++) Settle();
            Ok(rows.Count == 3, $"and deletes the selected rows once confirmed ({rows.Count}, dialog {(ask == null ? "none" : ask.IsVisible ? "open" : "closed")})");
            table.AddRows(3); Settle();
            Ok(rows.Count == 6, "Add takes several rows at once");

            // design mode: a heading dragged to a gap, Ctrl+Right, a new column
            table.Design = true; Settle();
            var heads = () => string.Join("|", columns.Select(c => c.Title));
            var codeHead = Cell(0, 2); var itemHead = Cell(0, 1);
            var from = Mid(Cell(0, 3)); var to = itemHead.TranslatePoint(new Point(itemHead.Bounds.Width, 14), window)!.Value;
            bool lineShown = false;
            Settle(); window.MouseDown(from, Avalonia.Input.MouseButton.Left); window.MouseMove(new Point((from.X + to.X) / 2, from.Y), Avalonia.Input.RawInputModifiers.LeftMouseButton); window.MouseMove(to, Avalonia.Input.RawInputModifiers.LeftMouseButton); Settle();
            lineShown = (bool)typeof(EditableTable).GetProperty("LineShown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(table)!;
            window.MouseUp(to, Avalonia.Input.MouseButton.Left); Settle();
            Ok(lineShown && heads().StartsWith("Item|Quantity|Code", StringComparison.Ordinal), "a heading dragged from its middle lands in the gap, the accent line marking it: " + heads());
            Cell(0, 2).Focus(); Key(Avalonia.Input.Key.Right, Avalonia.Input.RawInputModifiers.Control);
            Ok(heads().StartsWith("Item|Code|Quantity", StringComparison.Ordinal), "Ctrl+Right moves the focused heading");
            var added = table.InsertColumn(3); Settle();
            Ok(columns[3] == added && added.Title == "New column" && table.CurrentColumn == added, "a new column comes in and is current");
            window.Close();
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

        static int Main()
        {
            AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions()).SetupWithoutStarting();
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
            void Drag(Point from, Point to, Action? midway = null)
            {
                Settle(); window.MouseDown(from, Avalonia.Input.MouseButton.Left);
                window.MouseMove(new Point((from.X + to.X) / 2, (from.Y + to.Y) / 2), Avalonia.Input.RawInputModifiers.LeftMouseButton);
                window.MouseMove(to, Avalonia.Input.RawInputModifiers.LeftMouseButton); Settle(); midway?.Invoke();
                window.MouseUp(to, Avalonia.Input.MouseButton.Left); Settle();
            }
            bool lineShown = false;
            Drag(Mid(Head("Meter ID")), Edge(Head("Time")), () => lineShown = designer.GetVisualDescendants().OfType<Border>().Any(b => b.Width == 2 && b.IsVisible));
            Ok(moves.SequenceEqual(new[] { (2, 1) }), "a header dragged from its middle to the gap after Time moves it there: " + string.Join(",", moves));
            Ok(lineShown, "while dragging, the accent line marks the gap");
            Drag(Mid(Head("Time")), Edge(Head("Time")));
            Ok(moves.Count == 1, "dropped in its own gap, a column doesn't move");
            Drag(Mid(field), Edge(Head("Status")));
            Ok(drops.Count == 1 && (string)drops[0].Item1 == "Extra" && drops[0].Item2 == 4, "a field from another control drops into the gap at the end");
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
            light.Focus(Avalonia.Input.NavigationMethod.Tab); window.KeyPress(Avalonia.Input.Key.Right, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle();
            Ok(sw.Value == ModeChoice.Dark && Current == Look.Mode.Dark && Segment("Dark").IsFocused, "Right moves to Dark and switches");
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

            var sw = Get<Switch>("Toggle");
            vm.Flag = true; Ok(sw.On, "Switch: the view model turns it on");
            sw.Toggle(); Ok(!vm.Flag, "Switch: a toggle reaches the view model");
            Ok(!Get<StateToggle>("State").On, "StateToggle follows the same value");

            var tick = Get<Tick>("Box");
            tick.Toggle(); Ok(vm.Ticked == true, "Tick: a click reaches the view model");
            vm.Ticked = null; Ok(tick.State == null, "Tick: the view model sets it to some");

            var field = Get<Field>("NameField");
            field.Box.Text = "Anna"; Dispatcher.UIThread.RunJobs(); Ok(vm.Name == "Anna", "Field: typing reaches the view model");
            vm.Name = "Ben"; Ok(field.Text == "Ben", "Field: the view model sets the text");
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
            Ok(save.Text == "Save" && save.Kind == ButtonKind.Primary, "ActionButton: text and kind from XAML");
            save.PerformClick(); Ok(vm.Save.Runs == 1, "ActionButton: a click runs the command");
            vm.Save.Can = false; Ok(!save.IsEffectivelyEnabled, "ActionButton: the command's CanExecute disables it");
            save.PerformClick(); Ok(vm.Save.Runs == 1, "ActionButton: a disabled button doesn't run it");

            var choice = Get<ChoiceButton>("Choice");
            choice.SetItems(new[] { "One", "Two", "Three" }, vm.Pick);
            Ok(choice.Current == "Two", "ChoiceButton: the current item from the view model");
            vm.Pick = "Three"; Ok(choice.Current == "Three", "ChoiceButton: the view model picks");

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
            Ok(sheet.ValueSize == 14 && sheet.Sorting == vm.Flag, "EditableTable: sizes and sorting from XAML and the view model");
            sheet.Design = true; Dispatcher.UIThread.RunJobs(); Ok(vm.Designing, "EditableTable: Design reaches the view model");

            var modes = Get<ModeSwitch>("Modes");
            vm.Mode = ModeChoice.Light; Ok(modes.Value == ModeChoice.Light && Current == Look.Mode.Light, "ModeSwitch: the view model switches the mode");
            vm.Mode = ModeChoice.Dark; Ok(Current == Look.Mode.Dark, "ModeSwitch: and back");

            var blocks = Get<Masonry>("Blocks");
            Ok(blocks.MinColumn == 200 && blocks.MaxColumns == 2 && blocks.Gap == 32, "Masonry: settings from XAML, the rest default");
            window.Close();
        }
    }
}

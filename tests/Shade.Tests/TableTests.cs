// Table checks.
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
    static partial class Program
    {
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

        static void VirtualRows()
        {
            var column = new EditableTable.Column("name", "Name") { Sizing = EditableTable.Sizing.Fixed, Width = 180 };
            var rows = Enumerable.Range(0, 1000).Select(i => { var r = new EditableTable.Row(); r.Values["name"] = $"row{i:0000}"; return r; }).ToList();
            rows[500].Values["name"] = "A wrapped row with enough words to span several lines without clipping its content";
            var table = new EditableTable { Columns = new[] { column }, Rows = rows };
            var window = new Window { Width = 420, Height = 400, Content = table }; window.Show();
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

        static void TableInteractionGeometry()
        {
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

        /// <summary>A column takes the app's own cell, heading and footer content; only realized rows build it, an edit rebuilds it,
        /// and the editor covers it.</summary>
        static void TableTemplates()
        {
            var status = new EditableTable.Column("status", "Status", EditableTable.Kind.General) { Sizing = EditableTable.Sizing.Fixed, Width = 160, Footer = EditableTable.Total.Count };
            int built = 0; EditableTable table = null!;
            status.CellTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<EditableTable.Row>((r, _) => { built++; return new StatusBadge("badge " + r.Values["status"], "ok"); });
            status.HeaderTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<EditableTable.Column>((c, _) => new Tag("head " + c.Title));
            status.FooterTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<EditableTable.Column>((c, _) => new StatusBadge("count " + table.Footer(c), "ok"));
            var rows = Enumerable.Range(0, 1000).Select(i => { var r = new EditableTable.Row(); r.Values["status"] = $"s{i:0000}"; return r; }).ToList();
            table = new EditableTable { Rows = rows }; table.Columns = new[] { status };
            var window = new Window { Width = 420, Height = 400, Content = table }; window.Show();
            Settle();
            IEnumerable<StatusBadge> Badges() => table.GetVisualDescendants().OfType<StatusBadge>();
            bool Has(string text) => Badges().Any(b => b.Text == text);
            Ok(Has("badge s0000") && Has("count 1000") && !Has("badge s0999") && built < 100 && Badges().Count() < 40, "table templates: only the realized rows build cell content");
            Ok(table.GetVisualDescendants().OfType<Tag>().Any(t => t.Text == "head Status"), "table templates: a heading shows the app's content");
            var text = table.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "s0000");
            Ok(text.Opacity == 0, "table templates: the text stays under the content");
            Ok(table.TrySetValue(rows[0], status, "s9999") && Has("badge s9999") && !Has("badge s0000"), "table templates: an edit rebuilds the cell's content");
            var body = table.GetVisualDescendants().OfType<ScrollViewer>().Single(v => v.VerticalScrollBarVisibility == Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden);
            body.Offset = body.Offset.WithY(body.Extent.Height - body.Viewport.Height); Settle();
            Ok(Has("badge s0999") && !Has("badge s9999") && Badges().Count() < 40, "table templates: scrolling releases old content");
            rows[999].Values["status"] = ""; table.Refresh(); Settle();
            Ok(Has("count 999"), "table templates: Refresh rebuilds the footer content");
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
    }
}

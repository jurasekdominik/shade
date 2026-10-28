// TableRibbon checks.
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
        /// <summary>The toolbar and the Add rows bar open through properties: hidden actions, placement, the app's own controls.</summary>
        static void ToolbarLayout()
        {
            var column = new EditableTable.Column("code", "Code", EditableTable.Kind.General);
            var table = new EditableTable { ShowColumnEditor = true, Columns = new List<EditableTable.Column> { column }, Rows = new List<EditableTable.Row> { new() } };
            var window = new Window { Width = 1100, Height = 600, Content = table }; window.Show();
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
            Border Frame(string name) => (Border)typeof(EditableTable).GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(table)!;
            Thickness Toolbar() => Frame("columnToolbarFrame").BorderThickness; Thickness Ribbon() => Frame("columnRibbonFrame").BorderThickness;
            bool bottomOpen = Ribbon() == new Thickness(1, 1, 1, 0) && Toolbar() == new Thickness(1, 0, 1, 1);
            table.Design = false; Settle(); bool bottomClosed = Toolbar() == new Thickness(1);
            table.ToolbarPlacement = EditableTable.BarPlacement.Top; table.AddRowsPlacement = EditableTable.BarPlacement.Top; Settle(); bool topApart = Toolbar() == new Thickness(1);
            table.Design = true; Settle(); bool topApartOpen = Toolbar() == new Thickness(1, 1, 1, 0) && Ribbon() == new Thickness(1, 0, 1, 1);
            table.AddRowsPlacement = EditableTable.BarPlacement.Bottom; Settle(); bool onTable = Ribbon() == new Thickness(1, 0, 1, 0);
            table.Design = false; Settle();
            Ok(bottomOpen && bottomClosed && topApart && topApartOpen && onTable && Toolbar() == new Thickness(1, 1, 1, 0), "toolbar layout: toolbar and ribbon are one closed box, open only where it sits on the table's border");
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
    }
}

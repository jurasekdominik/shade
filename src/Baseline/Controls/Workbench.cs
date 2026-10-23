// Baseline - the shared workbench tools: the data table, a multiline box, name/value lines, export.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using static Baseline.Look;

namespace Baseline
{
    /// <summary>The data table (DESIGN_SYSTEM.md §6) and the small tools around it.</summary>
    public static class Workbench
    {
        public static Border Rule(Control content, Thickness? padding = null) => new Border { BorderBrush = B(Look.Border), BorderThickness = new Thickness(0, 0, 0, 1), Padding = padding ?? new Thickness(0, 0, 0, 8), Child = content };
        public static TextBox Lines(string hint) => new TextBox
        {
            PlaceholderText = hint,
            AcceptsReturn = true,
            FontFamily = Mono,
            FontSize = 12,
            Background = B(Surface),
            Foreground = B(Look.Text),
            BorderBrush = B(Look.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8),
            MinHeight = 65
        };  // a field's look: plainly a box to type in
        public static Dictionary<string, string> Pairs(string text, char separator = ':', bool caseSensitive = false)
        {
            var result = new Dictionary<string, string>(caseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
            foreach (string line in (text ?? "").Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)))
            {
                int split = line.IndexOf(separator);
                if (split < 1) throw new ArgumentException("Enter each line as Name" + separator + "Value.");
                string key = line.Substring(0, split).Trim();
                if (result.ContainsKey(key)) throw new ArgumentException("Duplicate name: " + key);
                result.Add(key, line.Substring(split + 1).Trim());
            }
            return result;
        }
        public static string PairsText(Dictionary<string, string> pairs, char separator = ':') => string.Join("\n", (pairs ?? new Dictionary<string, string>()).Select(p => p.Key + separator + " " + p.Value));
        /// <summary>A column as wide as its longest cell (the table's text and padding), within min..max: names show whole, the
        /// other columns keep their room.</summary>
        public static double FitWidth(IEnumerable<string> cells, double min, double max)
        {
            var longest = Kit.Text(cells.DefaultIfEmpty("").OrderByDescending(n => n?.Length ?? 0).First() ?? "", Look.Text, 9.75); longest.Measure(Avalonia.Size.Infinity);
            return Math.Clamp(Math.Ceiling(longest.DesiredSize.Width) + 24, min, max);
        }
        /// <summary>The shared table (DESIGN_SYSTEM.md §6). open: a click on a row; menu: its right-click menu (label, action; a null
        /// action is shown but not clickable); color: a cell's text colour (row, column), null = the default. select: a click of either button on a row (its index).</summary>
        /// <summary>selection: the rows selected, whenever they change; it also turns on Ctrl and Shift multi-select (a click still selects one row).</summary>
        /// <summary>checkable: rows for which it is true start with a tick box that follows the row's selection (Chosen files); toggle: a click selects or unselects a row, without Ctrl.</summary>
        /// <summary>fill: a row's background colour (the Transfer Preview's green and yellow), null = none.</summary>
        /// <summary>widths: Grid column widths ("2*,96,*"), one per column; null = equal. openOnDoubleClick: a click only selects the row (editors).</summary>
        public static Control Table(string[] columns, IReadOnlyList<string[]> rows, Action<int> open = null, Func<int, IList<(string label, Action act)>> menu = null, Func<string[], int, Color?> color = null, string widths = null, bool openOnDoubleClick = false, Action<int> headerClick = null, Func<string[], int, string> tip = null, Action<int> select = null, Action<IReadOnlyList<int>> selection = null, Func<string[], Color?> fill = null, Func<string[], bool> checkable = null, bool toggle = false)
        {
            if (columns.Length == 0) return Kit.Text("No mapped rows", Second, 9.75);
            // DESIGN_SYSTEM.md §6: 28 px header and rows, one cell padding for both, numbers (and their header) right-aligned
            static bool Number(string v) => decimal.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _);
            var numeric = columns.Select((_, i) => rows.Count > 0 && rows.All(r => i >= r.Length || r[i] == "" || Number(r[i]))).ToArray();
            var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
            Border Cells(string[] values, bool head)
            {
                var line = new Grid { ColumnDefinitions = new ColumnDefinitions((checkable != null ? "24," : "") + (widths ?? string.Join(",", columns.Select(_ => "*")))) };
                for (int i = 0; i < values.Length && i < columns.Length; i++)
                {
                    var text = Kit.Text(values[i], head ? Second : (color?.Invoke(values, i) ?? Look.Text), head ? 9 : 9.75, head ? 500 : 400);
                    text.Margin = new Thickness(8, 0); text.TextAlignment = numeric[i] ? TextAlignment.Right : TextAlignment.Left;
                    text.TextTrimming = TextTrimming.CharacterEllipsis; text.VerticalAlignment = VerticalAlignment.Center;  // long values end in "…"; the tooltip has them whole
                    Grid.SetColumn(text, i + (checkable != null ? 1 : 0)); line.Children.Add(text); ToolTip.SetTip(text, head ? values[i] : tip?.Invoke(values, i) ?? values[i]);
                    if (head && headerClick != null) { int k = i; text.Cursor = Kit.Hand; text.Background = Brushes.Transparent; text.PointerReleased += (s, e) => headerClick(k); }
                }
                var cell = new Border
                {
                    Child = line,
                    Height = FormLayout.RowHeight,
                    Background = head ? B(Surface) : values.Length > 0 && fill?.Invoke(values) is Color tinted ? B(tinted) : Brushes.Transparent,
                    BorderBrush = B(Look.Border),
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Padding = new Thickness(0, 0, 10, 0)
                };  // clear of the 8 px scroll bar; the header keeps the same columns
                if (checkable != null && !head && values.Length > 0 && checkable(values))
                {
                    var box = new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
                    var mark = Kit.Icon24(Icons.Check, 12, 2.5); mark.Stroke = B(OnAccent); box.Child = mark; Grid.SetColumn(box, 0); line.Children.Add(box);
                    bool wired = false;
                    cell.AttachedToVisualTree += (s, e) =>
                    {
                        var item = cell.FindAncestorOfType<ListBoxItem>(); if (item == null || wired) return; wired = true;
                        void Show() { box.Background = item.IsSelected ? B(Accent) : Brushes.Transparent; box.BorderBrush = B(item.IsSelected ? Accent : Look.Border); mark.IsVisible = item.IsSelected; }
                        Show(); item.PropertyChanged += (o, a) => { if (a.Property == ListBoxItem.IsSelectedProperty) Show(); };
                    };
                }
                if (head || open == null && menu == null && select == null) return cell;
                cell.Cursor = Kit.Hand;
                int IndexOf() { for (int k = 0; k < rows.Count; k++) if (ReferenceEquals(rows[k], values)) return k; return -1; }  // recycled rows: find by reference
                if (openOnDoubleClick) cell.DoubleTapped += (s, e) => { int index = IndexOf(); if (index >= 0) open?.Invoke(index); };
                cell.PointerReleased += (s, e) =>
                {
                    int index = IndexOf();
                    if (index < 0) return;
                    select?.Invoke(index);
                    if (e.InitialPressMouseButton == Avalonia.Input.MouseButton.Left) { if (!openOnDoubleClick) open?.Invoke(index); }
                    else if (e.InitialPressMouseButton == Avalonia.Input.MouseButton.Right && menu != null)
                    {
                        var items = menu(index);
                        Menu.Show(cell, items.Select(i => i.label).ToList(), k => items[k].act?.Invoke(), atPointer: true);
                    }
                };
                return cell;
            }
            grid.Children.Add(Cells(columns, true));
            var list = new ListBox
            {
                ItemsSource = rows,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<string[]>((row, names) => Cells(row ?? Array.Empty<string>(), false))
            };  // a recycled row is built once with no item
            if (selection != null)
            {
                list.SelectionMode = toggle ? SelectionMode.Multiple | SelectionMode.Toggle : SelectionMode.Multiple;
                Dictionary<string[], int> at = null;  // row -> its index, built on the first change (a scan per selected row is quadratic)
                list.SelectionChanged += (s, e) =>
                {
                    if (at == null) { at = new Dictionary<string[], int>(ReferenceEqualityComparer.Instance); for (int k = 0; k < rows.Count; k++) at[rows[k]] = k; }
                    selection(list.SelectedItems.Cast<string[]>().Select(r => at.TryGetValue(r, out int k) ? k : -1).Where(k => k >= 0).OrderBy(k => k).ToList());
                };
            }
            list.Styles.Add(new Avalonia.Styling.Style(x => Avalonia.Styling.Selectors.OfType<ListBoxItem>(x)) { Setters = { new Avalonia.Styling.Setter(ListBoxItem.PaddingProperty, new Thickness(0)), new Avalonia.Styling.Setter(ListBoxItem.MinHeightProperty, 0.0) } });
            // selection and hover use the accent tints, not the theme's blue (the colour sits on the row's presenter)
            foreach (var (state, colour) in new[] { (":selected", Soft), (":pointerover", CurrentRow) })
            {
                list.Styles.Add(new Avalonia.Styling.Style(x => Avalonia.Styling.Selectors.OfType<Avalonia.Controls.Presenters.ContentPresenter>(Avalonia.Styling.Selectors.Template(Avalonia.Styling.Selectors.Class(Avalonia.Styling.Selectors.OfType<ListBoxItem>(x), state)))) { Setters = { new Avalonia.Styling.Setter(Avalonia.Controls.Presenters.ContentPresenter.BackgroundProperty, B(colour)) } });
            }
            list.Styles.Add(new Avalonia.Styling.Style(x => Avalonia.Styling.Selectors.OfType<Avalonia.Controls.Presenters.ContentPresenter>(Avalonia.Styling.Selectors.Template(Avalonia.Styling.Selectors.Class(Avalonia.Styling.Selectors.Class(Avalonia.Styling.Selectors.OfType<ListBoxItem>(x), ":selected"), ":pointerover")))) { Setters = { new Avalonia.Styling.Setter(Avalonia.Controls.Presenters.ContentPresenter.BackgroundProperty, B(SoftHi)) } });
            Grid.SetRow(list, 1); grid.Children.Add(list); return new Border { Child = grid, BorderBrush = B(Look.Border), BorderThickness = new Thickness(1) };
        }
        public static async System.Threading.Tasks.Task Export(Control owner, string text, string filename)
        {
            var provider = TopLevel.GetTopLevel(owner)?.StorageProvider;
            if (provider == null || !provider.CanSave) throw new IOException("File saving is unavailable.");
            var file = await provider.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = filename, ShowOverwritePrompt = true });
            if (file == null) return;
            await using var stream = await file.OpenWriteAsync(); stream.SetLength(0);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false)); await writer.WriteAsync(text);
        }
    }
}

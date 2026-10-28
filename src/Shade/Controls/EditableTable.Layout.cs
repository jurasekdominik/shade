// Shade - shared control: EditableTable's widths and viewport: which columns fit, fill or stay fixed, which rows are realized.
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
        // ---- widths ---------------------------------------------------------------------------------------------------

        readonly Dictionary<(string text, double size, int weight, FontFamily font, bool italic), double> textWidths = new();
        double TextWidth(string s, double size, int weight = 400, bool italic = false)
        {
            var key = (s ?? "", size, weight, UiFont, italic);
            if (textWidths.TryGetValue(key, out double width)) return width;
            // Repeated numeric values and units share shaping work; bound the cache for live data feeds.
            if (textWidths.Count >= 16384) textWidths.Clear();
            return textWidths[key] = Math.Ceiling(Look.Measure(key.Item1, new Font(new Typeface(UiFont, italic ? FontStyle.Italic : FontStyle.Normal, Weight(weight)), size)));
        }
        double Longest(string s, double size, int weight, bool italic = false) => s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(w => TextWidth(w, size, weight, italic)).DefaultIfEmpty(0).Max();

        /// <summary>Room at a cell's right for its unit or suffix, so a value never runs under it.</summary>
        double Room(Column c) => c.Kind == Kind.Unit && c.Units.Count > 0 ? c.Units.Concat(rows.Select(r => UnitOf(r, c))).Distinct().Select(u => TextWidth(u, TextSize.Label, 500)).Max() + 20 : c.Suffix != null ? TextWidth(c.Suffix, TextSize.Caption) + 14 : 8;

        double Full(Line l, int i)
        {
            if (double.IsNaN(l.Full[i])) l.Full[i] = TextWidth(Shown(l.Row, columns[i]), valueSize, columns[i].Bold ? 600 : 400, columns[i].Italic);
            return l.Full[i];
        }

        double Word(Line l, int i)
        {
            if (double.IsNaN(l.Word[i])) l.Word[i] = columns[i].Kind is Kind.Text or Kind.General ? Longest(Shown(l.Row, columns[i]), valueSize, columns[i].Bold ? 600 : 400, columns[i].Italic) : Full(l, i);
            return l.Word[i];
        }

        /// <summary>A column's width from its whole content: the heading, every value with its unit or suffix, the footer.</summary>
        double Natural(int i)
        {
            var c = columns[i];
            double w = Math.Max(TextWidth(c.Title, c.HeadingSize ?? headingSize, 600), TextWidth(c.Subtitle, c.SubheadingSize ?? subheadingSize)) + 20 + (sortKey == c.Key ? 22 : 0);
            foreach (var l in shown) w = Math.Max(w, Full(l, i) + 12 + rooms[i]);
            if (c.Footer != Total.None) w = Math.Max(w, TextWidth(ShownFooter(c), valueSize, 600) + TextWidth(T(c.Footer.ToString()), TextSize.Caption, 500) + 26);
            return Math.Ceiling(w);
        }

        /// <summary>A fill column's least width: its longest word (its text wraps), the heading's included.</summary>
        double Least(int i)
        {
            double w = Longest(columns[i].Title, columns[i].HeadingSize ?? headingSize, 600) + 20;
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
            var nextWidths = w.Select(Math.Floor).ToArray();
            if (!widths.SequenceEqual(nextWidths)) foreach (var l in shown) l.Height = ControlHeight;
            widths = nextWidths;
            foreach (var g in Grids())
                for (int i = 0; i < n && i + 1 < g.ColumnDefinitions.Count; i++) g.ColumnDefinitions[i + 1].Width = new GridLength(widths[i]);
            RealizeViewport();
        }

        // ---- viewport -----------------------------------------------------------------------------------------------

        void UpdateOffsets()
        {
            if (offsets.Length != shown.Count + 1) { offsets = new double[shown.Count + 1]; offsetVersion++; }
            bool changed = false;
            for (int i = 0; i < shown.Count; i++)
            {
                double next = offsets[i] + shown[i].Height;
                if (offsets[i + 1] != next) { offsets[i + 1] = next; changed = true; }
            }
            if (changed) offsetVersion++;
        }

        int RowAt(double y)
        {
            int index = Array.BinarySearch(offsets, y);
            return Math.Clamp(index >= 0 ? index : ~index - 1, 0, Math.Max(0, shown.Count - 1));
        }

        // Keep data and width caches for all rows, but controls only for the viewport and the focused row.
        void RealizeViewport()
        {
            if (realizing) return;
            realizing = true;
            try
            {
                int previousOffsets = offsetVersion;
                UpdateOffsets();
                double viewport = bodyView.Viewport.Height;
                if (viewport <= 0 || !double.IsFinite(viewport)) viewport = 560;
                int first = shown.Count <= 64 ? 0 : Math.Max(0, RowAt(bodyView.Offset.Y) - 4);
                int last = shown.Count <= 64 ? shown.Count : Math.Min(shown.Count, RowAt(bodyView.Offset.Y + viewport) + 5);
                var wanted = Enumerable.Range(first, last - first).ToList();
                var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
                var focusLine = editing ?? (focused?.Parent as Grid)?.Tag as Line;
                int pin = focusLine == null ? -1 : shown.IndexOf(focusLine);
                if (pin >= 0 && !wanted.Contains(pin)) { wanted.Add(pin); wanted.Sort(); }
                var keep = wanted.Select(i => shown[i]).ToHashSet();
                foreach (var l in lines.Values.Where(l => l.Realized && !keep.Contains(l)).ToList())
                {
                    if (ring.Parent == l.Panel) Move(ring, null);
                    bodyGrid.Children.Remove(l.Panel); l.Release();
                }
                foreach (int i in wanted)
                {
                    var l = shown[i];
                    if (!l.Realized) Make(l);
                    l.Number.Text = (i + 1).ToString(CultureInfo.InvariantCulture);
                    int rowNumber = i + 1;
                    if (!Equals(l.Gutter.Tag, rowNumber))
                    {
                        l.Gutter.Tag = rowNumber;
                        Words(l.Gutter, word => Avalonia.Automation.AutomationProperties.SetName(word, T("Select row {0}", rowNumber)), "name");
                    }
                    for (int c = 0; c < widths.Length && c < columns.Count; c++) l.Panel.ColumnDefinitions[c + 1].Width = new GridLength(widths[c]);
                    l.Panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    l.Height = Math.Max(ControlHeight, l.Panel.DesiredSize.Height);
                }
                UpdateOffsets();
                // Spacer rows preserve the scroll extent without a Grid row definition for every data row.
                if (!visible.SequenceEqual(wanted) || previousOffsets != offsetVersion || wanted.Any(i => Math.Abs(shown[i].Panel.Bounds.Height - shown[i].Height) > 0.5))
                {
                    bodyGrid.RowDefinitions.Clear();
                    int previous = 0;
                    foreach (int i in wanted)
                    {
                        if (i > previous) bodyGrid.RowDefinitions.Add(new RowDefinition(offsets[i] - offsets[previous], GridUnitType.Pixel));
                        Grid.SetRow(shown[i].Panel, bodyGrid.RowDefinitions.Count);
                        bodyGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                        previous = i + 1;
                    }
                    if (previous < shown.Count) bodyGrid.RowDefinitions.Add(new RowDefinition(offsets[^1] - offsets[previous], GridUnitType.Pixel));
                    visible = wanted;
                }
                bodyGrid.MinWidth = Gutter + Actions + widths.Sum();
            }
            finally { realizing = false; }
        }

        void EnsureVisible(Line l)
        {
            int index = shown.IndexOf(l);
            if (index < 0) return;
            UpdateOffsets();
            double viewport = bodyView.Viewport.Height;
            if (viewport > 0)
            {
                double y = bodyView.Offset.Y;
                if (offsets[index] < y) bodyView.Offset = bodyView.Offset.WithY(offsets[index]);
                else if (offsets[index + 1] > y + viewport) bodyView.Offset = bodyView.Offset.WithY(offsets[index + 1] - viewport);
            }
            RealizeViewport();
            if (!l.Realized) Make(l);
        }
    }
}

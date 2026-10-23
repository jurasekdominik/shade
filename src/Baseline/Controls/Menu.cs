// Baseline - shared control: Menu, the dropdown and context menus.
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using Avalonia.Animation;
using Avalonia.Controls.Primitives;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media.Transformation;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia;
using static Baseline.Look;

namespace Baseline
{
    /// <summary>The menus: a rounded card of 28 px rows, the hovered one an accent-tinted fill, the current one in the accent.
    /// In a popup under a picker, at the pointer as a context menu, or in the tray's own window.</summary>
    public static class Menu
    {
        internal const double RowHeight = 28, MaximumHeight = RowHeight * 10 + Kit.Gap;
        /// <summary>An item that is a line between groups (not clickable).</summary>
        public const string Divider = "\u2014";
        /// <summary>Items that remove or cut something: red, and kept below a line.</summary>
        static bool Danger_(string item) => item.StartsWith("Remove", StringComparison.Ordinal) || item.StartsWith("Delete", StringComparison.Ordinal) || item.StartsWith("Disconnect", StringComparison.Ordinal);

        /// <summary>The menu's card (DESIGN_SYSTEM.md §6): 28 px rows whose text lines up with the field's, the current item highlighted,
        /// a colour per item when given, danger items last in red. Arrow keys move, Enter picks, Esc closes, a letter jumps.
        /// close runs before picked.</summary>
        /// <summary>attached: the open part of a dropdown (DESIGN_SYSTEM.md §6): at least the field's width, growing to fit every option; its side and bottom
        /// borders continuing the field's, a faint line between field and options.</summary>
        public static Border Panel(IList<string> items, Action<int> picked, Action close, int current = -1, double width = 0, Func<int, Color?> color = null, bool attached = false, bool scrollable = true)
        {
            var rows = new StackPanel { Margin = new Thickness(0, attached ? 0 : 4, 0, 4) };
            if (attached) rows.Children.Add(new Border { Height = 1, Background = B(Look.Border), Margin = new Thickness(0, 0, 0, 4) });
            var pickable = new List<(int index, Border row, TextBlock text, Color fg)>();
            int lit = -1;
            void Light(int k)
            {
                lit = k;
                foreach (var p in pickable) p.row.Background = p.index == k ? B(Surface2) : Brushes.Transparent;
            }
            for (int i = 0; i < items.Count; i++)
            {
                int k = i;
                bool danger = Danger_(items[i]);
                if (items[i] == Divider || danger && i > 0 && items[i - 1] != Divider)  // groups apart; removing things always last, below a line
                {
                    rows.Children.Add(new Border { Height = 1, Background = B(Look.Border), Margin = new Thickness(0, 4) });
                    if (items[i] == Divider) continue;
                }
                bool cur = i == current;
                var fg = danger ? DangerHi : color?.Invoke(i) ?? Look.Text;
                var t = Kit.Text(items[i], fg, 9.75, cur ? 500 : 400);
                var row = new Border { Height = RowHeight, Padding = new Thickness(8, 0), Background = Brushes.Transparent, Child = t, Cursor = Kit.Hand };
                pickable.Add((k, row, t, fg));
                row.PointerEntered += (s, e) => Light(k);
                row.PointerReleased += (s, e) => { if (e.InitialPressMouseButton == MouseButton.Left) { close(); picked(k); } };
                rows.Children.Add(row);
            }
            rows.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double naturalWidth = rows.DesiredSize.Width + 2 + (scrollable && rows.DesiredSize.Height > MaximumHeight ? Kit.Pad : 0);
            width = Math.Max(width > 0 ? width : 160, naturalWidth);
            Control content = ScrollRows(rows, width, scrollable);
            var card = new Border
            {
                Background = B(Surface),
                BorderBrush = B(attached ? MenuEdge : Look.Border),
                BorderThickness = attached ? new Thickness(1, 0, 1, 1) : new Thickness(1),
                CornerRadius = attached ? new CornerRadius(0, 0, 4, 4) : new CornerRadius(4),
                Child = content,
                MaxHeight = scrollable ? MaximumHeight + 2 : double.PositiveInfinity,
                VerticalAlignment = VerticalAlignment.Top,
                MinWidth = width > 0 ? width : 160,
                Focusable = true,
                ClipToBounds = true,
            };
            if (attached && width > 0) card.Width = width;
            card.KeyDown += (s, e) =>
            {
                int at = pickable.FindIndex(p => p.index == lit);
                if (e.Key == Key.Down || e.Key == Key.Up) { int n = pickable.Count; if (n == 0) return; at = at < 0 ? (e.Key == Key.Down ? 0 : n - 1) : (at + (e.Key == Key.Down ? 1 : n - 1)) % n; Light(pickable[at].index); pickable[at].row.BringIntoView(); e.Handled = true; }
                else if (e.Key == Key.Enter && at >= 0) { e.Handled = true; close(); picked(lit); }
                else if (e.Key == Key.Escape) { e.Handled = true; close(); }
            };
            card.TextInput += (s, e) =>
            {
                var c = (e.Text ?? "").Trim();
                if (c.Length == 0) return;
                int from = Math.Max(0, pickable.FindIndex(p => p.index == lit) + 1);
                var hit = pickable.Skip(from).Concat(pickable.Take(from)).FirstOrDefault(p => items[p.index].StartsWith(c, StringComparison.OrdinalIgnoreCase));
                if (hit.row != null) { Light(hit.index); hit.row.BringIntoView(); }
            };
            if (current >= 0 && pickable.Any(p => p.index == current))
            {
                Light(current);
                Dispatcher.UIThread.Post(() => pickable.First(p => p.index == current).row.BringIntoView(), DispatcherPriority.Loaded);
            }
            return card;
        }

        internal static Control ScrollRows(StackPanel rows, double width, bool scrollable = true)
        {
            rows.Measure(new Size(width > 0 ? width : double.PositiveInfinity, double.PositiveInfinity));
            Control content = rows;
            if (scrollable && rows.DesiredSize.Height > MaximumHeight) content = new ScrollViewer
            {
                Content = rows,
                Padding = new Thickness(0, 0, Kit.Pad, 0),
                Height = Math.Min(MaximumHeight, rows.DesiredSize.Height),
                MaxHeight = MaximumHeight,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            return content;
        }

        public static Popup Show(Control anchor, IList<string> items, Action<int> picked, int current = -1, double width = 0, bool atPointer = false, Action closed = null, Func<int, Color?> color = null, bool attached = false)
        {
            var pop = new Popup { PlacementTarget = anchor, Placement = atPointer ? PlacementMode.Pointer : PlacementMode.BottomEdgeAlignedLeft, VerticalOffset = atPointer || attached ? 0 : 2, IsLightDismissEnabled = true };
            var card = Panel(items, picked, () => pop.IsOpen = false, current, width, color, attached);
            pop.Child = card;
            pop.Opened += (s, e) => card.Focus();
            pop.Closed += (s, e) => { closed?.Invoke(); ((ISetLogicalParent)pop).SetParent(null); };
            ((ISetLogicalParent)pop).SetParent(anchor);
            pop.IsOpen = true;
            return pop;
        }
    }
}

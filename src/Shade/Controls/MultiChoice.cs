// Shade - shared control: a picker of several items.
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using Avalonia.Animation;
using Avalonia.Controls.Primitives;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media.Transformation;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia;
using static Shade.Look;

namespace Shade
{
    /// <summary>A picker of several items: a tick box per item in its list, which stays open while ticking. Nothing ticked =
    /// everything (Empty names it).</summary>
    public sealed class MultiChoice : Border
    {
        readonly List<string> items;
        public IReadOnlyList<string> Items => items;
        /// <summary>Shown instead of the chosen items (a filter whose choices show as chips beside it).</summary>
        public string? Label { get; set; }
        readonly HashSet<string> selected = new HashSet<string>();
        /// <summary>The ticked items; Set changes them.</summary>
        public IReadOnlySet<string> Selected => selected;
        readonly TextBlock text = Kit.Text("", Look.Text);
        readonly string empty;
        bool open;
        public event Action? Changed;

        public MultiChoice(IEnumerable<string> list, string empty)
        {
            items = list.ToList(); this.empty = empty;
            Height = 28; CornerRadius = new CornerRadius(4); BorderThickness = new Thickness(1); Background = B(Surface); BorderBrush = B(Look.Border);
            Padding = new Thickness(8, 0, 4, 0); Cursor = Kit.Hand;
            text.FontSize = Body; text.TextTrimming = TextTrimming.CharacterEllipsis;
            var chevron = Kit.Icon24(Icons.ChevronDown, 16, 2);
            chevron.Stroke = B(Faint); chevron.VerticalAlignment = VerticalAlignment.Center; chevron.Margin = new Thickness(4, 0, 0, 0);
            var d = new DockPanel();
            DockPanel.SetDock(chevron, Dock.Right);
            d.Children.Add(chevron); d.Children.Add(text);
            Child = d;
            PointerEntered += (s, e) => BorderBrush = B(MenuEdge);
            PointerExited += (s, e) => { if (!open) BorderBrush = B(Look.Border); };  // open: the field keeps the list's edge colour
            PointerReleased += (s, e) => { if (e.InitialPressMouseButton == MouseButton.Left && !open) Open(); };  // the list, ticked as it stands
            Show();
        }

        /// <summary>Selects these items (others off), without raising Changed.</summary>
        public void Set(IEnumerable<string> picked) { selected.Clear(); foreach (var i in picked.Where(items.Contains)) selected.Add(i); Show(); }

        void Show() => text.Text = Label != null ? Label : Selected.Count == 0 ? empty : Selected.Count <= 2 ? string.Join(", ", items.Where(Selected.Contains)) : $"{Selected.Count} selected";

        Popup Open()
        {
            var rows = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };
            rows.Children.Add(new Border { Height = 1, Background = B(Look.Border), Margin = new Thickness(0, 0, 0, 4) });
            var pop = new Popup
            {
                PlacementTarget = this,
                Placement = PlacementMode.BottomEdgeAlignedLeft,
                IsLightDismissEnabled = true,
                Child = new Border { Background = B(Surface), BorderBrush = B(MenuEdge), BorderThickness = new Thickness(1, 0, 1, 1), CornerRadius = new CornerRadius(0, 0, 4, 4), Width = Bounds.Width, ClipToBounds = true },
            };
            open = true; CornerRadius = new CornerRadius(4, 4, 0, 0); BorderThickness = new Thickness(1, 1, 1, 0); BorderBrush = B(MenuEdge);
            foreach (var item in items)
            {
                var tick = new Tick { State = Selected.Contains(item), IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Center };
                var t = Kit.Text(item, Selected.Contains(item) ? Look.Text : Second, 9.75);
                t.Margin = new Thickness(8, 0, 0, 0);
                var line = new DockPanel();
                DockPanel.SetDock(tick, Dock.Left);
                line.Children.Add(tick); line.Children.Add(t);
                var row = new Border { Height = 28, Padding = new Thickness(8, 0), Background = Brushes.Transparent, Child = line, Cursor = Kit.Hand };
                row.PointerEntered += (s, e) => row.Background = B(Surface2);
                row.PointerExited += (s, e) => row.Background = Brushes.Transparent;
                row.PointerReleased += (s, e) =>
                {
                    if (e.InitialPressMouseButton != MouseButton.Left) return;
                    if (!selected.Remove(item)) selected.Add(item);
                    tick.State = Selected.Contains(item);
                    t.Foreground = B(tick.On ? Look.Text : Second);
                    Show(); Changed?.Invoke();
                };
                rows.Children.Add(row);
            }
            ((Border)pop.Child).Child = Menu.ScrollRows(rows, Bounds.Width);
            pop.Closed += (s, e) => { open = false; ((ISetLogicalParent)pop).SetParent(null); CornerRadius = new CornerRadius(4); BorderThickness = new Thickness(1); BorderBrush = B(Look.Border); };
            ((ISetLogicalParent)pop).SetParent(this);
            pop.IsOpen = true;
            return pop;
        }
    }
}

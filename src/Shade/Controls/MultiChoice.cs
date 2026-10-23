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
        readonly TextBlock text = Build.Text("", Look.Text);
        readonly string empty;
        bool open;
        static readonly Thickness Pad = new Thickness(8, 0, 4, 0);
        public event Action? Changed;

        public MultiChoice(IEnumerable<string> list, string empty)
        {
            items = list.ToList(); this.empty = empty;
            Height = 28; CornerRadius = new CornerRadius(4); BorderThickness = new Thickness(1); Background = B(Raised); BorderBrush = B(Look.Separator);
            Padding = Pad; Cursor = Build.Hand;
            text.FontSize = TextSize.Body; text.TextTrimming = TextTrimming.CharacterEllipsis;
            var chevron = Build.Icon24(Icons.ChevronDown, 16, 2);
            chevron.Stroke = B(Text3); chevron.VerticalAlignment = VerticalAlignment.Center; chevron.Margin = new Thickness(4, 0, 0, 0);
            var d = new DockPanel();
            DockPanel.SetDock(chevron, Dock.Right);
            d.Children.Add(chevron); d.Children.Add(text);
            Child = d;
            PointerEntered += (s, e) => BorderBrush = B(Outline);
            PointerExited += (s, e) => { if (!open) BorderBrush = B(Look.Separator); };  // open: the field keeps the list's edge colour
            PointerReleased += (s, e) => { if (e.InitialPressMouseButton == MouseButton.Left && !open) Open(); };  // the list, ticked as it stands
            Show();
        }

        /// <summary>Selects these items (others off), without raising Changed.</summary>
        public void Set(IEnumerable<string> picked) { selected.Clear(); foreach (var i in picked.Where(items.Contains)) selected.Add(i); Show(); }

        void Show() => text.Text = Label != null ? Label : Selected.Count == 0 ? empty : Selected.Count <= 2 ? string.Join(", ", items.Where(Selected.Contains)) : $"{Selected.Count} selected";

        Popup Open()
        {
            var rows = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };
            rows.Children.Add(new Border { Height = 1, Background = B(Look.Separator), Margin = new Thickness(0, 0, 0, 4) });
            var pop = new Popup
            {
                PlacementTarget = this,
                Placement = PlacementMode.BottomEdgeAlignedLeft,
                IsLightDismissEnabled = true,
                Child = new Border { Background = B(Raised), BorderBrush = B(Outline), BorderThickness = new Thickness(1, 0, 1, 1), CornerRadius = new CornerRadius(0, 0, 4, 4), Width = Bounds.Width, ClipToBounds = true },
            };
            open = true; Build.Attached(this, true, Pad); BorderBrush = B(Outline);
            foreach (var item in items)
            {
                var tick = new Tick { State = Selected.Contains(item), IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Center };
                var t = Build.Text(item, Selected.Contains(item) ? Look.Text : Text2, TextSize.Body);
                t.Margin = new Thickness(8, 0, 0, 0);
                var line = new DockPanel();
                DockPanel.SetDock(tick, Dock.Left);
                line.Children.Add(tick); line.Children.Add(t);
                var row = new Border { Height = 28, Padding = new Thickness(8, 0), Background = Brushes.Transparent, Child = line, Cursor = Build.Hand };
                row.PointerEntered += (s, e) => row.Background = B(Fill);
                row.PointerExited += (s, e) => row.Background = Brushes.Transparent;
                row.PointerReleased += (s, e) =>
                {
                    if (e.InitialPressMouseButton != MouseButton.Left) return;
                    if (!selected.Remove(item)) selected.Add(item);
                    tick.State = Selected.Contains(item);
                    t.Foreground = B(tick.On ? Look.Text : Text2);
                    Show(); Changed?.Invoke();
                };
                rows.Children.Add(row);
            }
            ((Border)pop.Child).Child = Menu.ScrollRows(rows, Bounds.Width);
            pop.Closed += (s, e) => { open = false; ((ISetLogicalParent)pop).SetParent(null); Build.Attached(this, false, Pad); BorderBrush = B(Look.Separator); };
            ((ISetLogicalParent)pop).SetParent(this);
            pop.IsOpen = true;
            return pop;
        }
    }
}

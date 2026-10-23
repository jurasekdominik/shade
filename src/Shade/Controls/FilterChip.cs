// Shade - shared control: a filter pill.
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
using static Shade.Look;

namespace Shade
{
    /// <summary>A filter pill: a label and a count in its own colour; the active one is tinted with the accent.</summary>
    public sealed class FilterChip : Border
    {
        readonly TextBlock label, count = Kit.Text("", Second, 9, 500);
        bool active, hot;
        public event Action Click;

        public FilterChip(string text, Color? dot = null)
        {
            label = Kit.Text(text, Second, 9, 500);
            count.Margin = new Thickness(6, 0, 0, 0);
            var p = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (dot != null) p.Children.Add(new Avalonia.Controls.Shapes.Ellipse { Width = 8, Height = 8, Fill = B(dot.Value), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
            p.Children.Add(label); p.Children.Add(count);
            Child = p;
            Height = 28; CornerRadius = new CornerRadius(4); BorderThickness = new Thickness(1); Padding = new Thickness(10, 0);
            Cursor = Kit.Hand;
            PointerEntered += (s, e) => { hot = true; Restyle(); };
            PointerExited += (s, e) => { hot = false; Restyle(); };
            PointerReleased += (s, e) => { if (e.InitialPressMouseButton == MouseButton.Left) Click?.Invoke(); };
            Restyle();
        }

        public bool Active { get => active; set { if (active != value) { active = value; Restyle(); } } }
        public void Set(string text, Color c) => count.Set(text, c);

        void Restyle()
        {
            Background = B(active ? Soft : hot ? Surface2 : Surface);
            BorderBrush = B(active ? SoftLine : hot ? Disabled : Look.Border);
            label.Foreground = B(active ? Accent : Second);
        }
    }
}

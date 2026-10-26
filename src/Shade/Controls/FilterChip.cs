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
    public sealed class FilterChip : Border, IRestyle
    {
        readonly TextBlock label, count = Build.Text("", Text2, TextSize.Label, 500);
        bool active, hot;
        public static readonly DirectProperty<FilterChip, bool> ActiveProperty = AvaloniaProperty.RegisterDirect<FilterChip, bool>(nameof(Active), o => o.Active, (o, v) => o.Active = v);
        protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new ChipPeer(this);
        internal void Invoke() { if (IsEffectivelyEnabled) Click?.Invoke(); }
        public event Action? Click;

        public FilterChip(string text, Tone? dot = null)
        {
            label = Build.Text(text, Text2, TextSize.Label, 500);
            count.Margin = new Thickness(6, 0, 0, 0);
            var p = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (dot != null) p.Children.Add(new Avalonia.Controls.Shapes.Ellipse { Width = 8, Height = 8, Fill = B(dot), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
            p.Children.Add(label); p.Children.Add(count);
            Child = p;
            Height = ControlHeight; CornerRadius = new CornerRadius(4); BorderThickness = new Thickness(1); Padding = new Thickness(10, 0);
            Cursor = Build.Hand; Activate(this, Invoke);
            Avalonia.Automation.AutomationProperties.SetName(this, text);
            PointerEntered += (s, e) => { hot = true; Restyle(); };
            PointerExited += (s, e) => { hot = false; Restyle(); };
            Restyle();
        }

        public bool Active { get => active; set { if (SetAndRaise(ActiveProperty, ref active, value)) Restyle(); } }
        public void Set(string text, Tone c) => count.Set(text, c);

        public void Restyle()
        {
            Background = B(active ? AccentTint : hot ? Fill : Raised);
            BorderBrush = B(active ? AccentTintLine : hot ? Disabled : Look.Separator);
            label.Foreground = B(active ? AccentText : Text2);
        }
    }
}

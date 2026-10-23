// Shade - shared control: a small labelled capsule (an icon and a word, accent on a soft tint) for actions beside a heading.
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
    /// <summary>A small labelled capsule (an icon and a word, accent on a soft tint) for actions beside a heading: Add, Steps,
    /// Presets, Saved views. Hover brightens it; disabled greys it.</summary>
    public sealed class Capsule : Border, IRestyle
    {
        public static readonly Geometry Plus = Icons.Plus, List = Icons.List, Bookmark = Icons.Bookmark;
        readonly Avalonia.Controls.Shapes.Path icon;
        readonly TextBlock label;
        bool hot;
        public event Action? Click;

        public Capsule(string text, Geometry glyph)
        {
            icon = Kit.Icon24(glyph, 16, 2);
            label = Kit.Text(text, Accent, 9.75, 500);
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 10, 0) };
            row.Children.Add(icon); row.Children.Add(label);
            Child = row;
            Height = 28; CornerRadius = new CornerRadius(4);
            VerticalAlignment = VerticalAlignment.Center;
            Cursor = Kit.Hand;
            PointerEntered += (s, e) => { hot = true; Restyle(); };
            PointerExited += (s, e) => { hot = false; Restyle(); };
            PointerReleased += (s, e) => { if (e.InitialPressMouseButton == MouseButton.Left && IsEnabled) Click?.Invoke(); };
            PropertyChanged += (s, e) => { if (e.Property == IsEnabledProperty) Restyle(); };
            Restyle();
        }

        public void Restyle()
        {
            var fg = !IsEnabled ? Disabled : hot ? AccentHi : AccentText;
            Background = hot && IsEnabled ? B(Surface2) : Brushes.Transparent;
            label.Foreground = icon.Stroke = B(fg);
        }
    }
}

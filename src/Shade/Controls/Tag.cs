// Shade - shared control: a small label chip, 24 px high (a count, a chosen file); optionally with a remove cross.
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using static Shade.Look;

namespace Shade
{
    /// <summary>A read-only label chip: a tint of its tone behind the tone's text (a count, "3 new"), or Fill with plain text when it has no tone.
    /// Removable: a cross at its end raises Removed (a chosen file in the Transfer bar).</summary>
    public sealed class Tag : Border
    {
        readonly TextBlock label;
        public event Action Removed;

        public Tag(string text, Color? tone = null, bool removable = false)
        {
            Height = 24; CornerRadius = new CornerRadius(4); VerticalAlignment = VerticalAlignment.Center;
            Background = B(tone is Color c ? Tint(c) : Surface2);
            Padding = new Thickness(8, 0, removable ? 4 : 8, 0);
            label = Kit.Text(text, tone ?? Look.Text, 9, 500); label.VerticalAlignment = VerticalAlignment.Center;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 }; row.Children.Add(label);
            if (removable)
            {
                var cross = Kit.Icon24(Icons.X, 12, 2); cross.Stroke = B(Faint);
                var button = new Border { Child = cross, Width = 16, Height = 16, Background = Brushes.Transparent, Cursor = Kit.Hand, VerticalAlignment = VerticalAlignment.Center };
                button.PointerEntered += (s, e) => cross.Stroke = B(Look.Text); button.PointerExited += (s, e) => cross.Stroke = B(Faint);
                button.PointerReleased += (s, e) => Removed?.Invoke();
                ToolTip.SetTip(button, "Remove"); row.Children.Add(button);
            }
            Child = row;
        }

        public string Text { get => label.Text ?? ""; set => label.Text = value; }
    }
}

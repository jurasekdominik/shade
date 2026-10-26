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
    public sealed class Tag : Border, IRestyle
    {
        readonly TextBlock label;
        readonly Tone? tone;
        public event Action? Removed;

        public Tag(string text, Tone? tone = null, bool removable = false)
        {
            this.tone = tone;
            Height = 24; CornerRadius = new CornerRadius(4); VerticalAlignment = VerticalAlignment.Center;
            Restyle();
            Padding = new Thickness(8, 0, removable ? 4 : 8, 0);
            label = Build.Text(text, tone ?? Look.Text, TextSize.Label, 500); label.VerticalAlignment = VerticalAlignment.Center;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 }; row.Children.Add(label);
            if (removable)
            {
                var cross = Build.Icon24(Icons.X, 12, 2); cross.Stroke = B(Text3);
                var button = new Border { Child = cross, Width = 16, Height = 16, Background = Brushes.Transparent, Cursor = Build.Hand, VerticalAlignment = VerticalAlignment.Center };
                button.PointerEntered += (s, e) => cross.Stroke = B(Look.Text); button.PointerExited += (s, e) => cross.Stroke = B(Text3);
                button.PointerReleased += (s, e) => Removed?.Invoke();
                Words(button, word => ToolTip.SetTip(word, T("Remove")), "tip"); row.Children.Add(button);
            }
            Child = row;
        }

        public string Text { get => label.Text ?? ""; set => label.Text = value; }
        public void Restyle() => Background = B(tone != null ? Tint(tone) : Fill);
    }
}

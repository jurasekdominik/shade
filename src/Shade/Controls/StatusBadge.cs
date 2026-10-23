// Shade - shared control: a state as a dot and a word (DESIGN_SYSTEM.md §7).
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
    /// <summary>A state as a dot and a word (DESIGN_SYSTEM.md §7): the colour supports the word, never replaces it.
    /// Kind: ok | warn | err | busy | "" (waiting) | off (disabled, a hollow dot).</summary>
    public sealed class StatusBadge : StackPanel
    {
        readonly Avalonia.Controls.Shapes.Ellipse dot = new Avalonia.Controls.Shapes.Ellipse { Width = 8, Height = 8, VerticalAlignment = VerticalAlignment.Center };
        readonly TextBlock word = Build.Text("", Look.Text, TextSize.Body);
        public StatusBadge(string text = "", string kind = "") { Orientation = Orientation.Horizontal; Spacing = 6; Children.Add(dot); Children.Add(word); Set(text, kind); }
        public void Set(string text, string kind)
        {
            var c = kind switch { "ok" => Success, "warn" => Warn, "err" => Danger, "busy" => Accent, _ => Text3 };
            word.Text = text; ToolTip.SetTip(this, text);
            word.Foreground = B(kind == "off" ? Text3 : Look.Text);
            dot.Fill = kind == "off" ? Brushes.Transparent : B(c);
            dot.Stroke = kind == "off" ? B(Text3) : null; dot.StrokeThickness = kind == "off" ? 1.2 : 0;
        }
    }
}

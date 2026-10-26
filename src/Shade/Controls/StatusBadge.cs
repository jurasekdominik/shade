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
        readonly Avalonia.Controls.Shapes.Ellipse dot = new Avalonia.Controls.Shapes.Ellipse { Width = M(8), Height = M(8), VerticalAlignment = VerticalAlignment.Center };
        readonly TextBlock word = Build.Text("", Look.Text, TextSize.Body);
        public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<StatusBadge, string>(nameof(Text), "");
        public static readonly StyledProperty<string> KindProperty = AvaloniaProperty.Register<StatusBadge, string>(nameof(Kind), "");
        public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
        public string Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
        public StatusBadge() : this("") { }
        public StatusBadge(string text, string kind = "") { Orientation = Orientation.Horizontal; Spacing = M(6); Children.Add(dot); Children.Add(word); Set(text, kind); }
        public void Set(string text, string kind) { Text = text; Kind = kind; Paint(); }
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if ((change.Property == TextProperty || change.Property == KindProperty) && word != null && dot != null) Paint();
        }
        void Paint()
        {
            string text = Text, kind = Kind;
            var c = kind switch { "ok" => Success, "warn" => Warn, "err" => Danger, "busy" => Accent, _ => Text3 };
            word.Text = text; ToolTip.SetTip(this, text);
            word.Foreground = B(kind == "off" ? Text3 : Look.Text);
            dot.Fill = kind == "off" ? Brushes.Transparent : B(c);
            dot.Stroke = kind == "off" ? B(Text3) : null; dot.StrokeThickness = kind == "off" ? 1.2 : 0;
        }
    }
}

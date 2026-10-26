// Shade - shared control: on/off switch.
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
    /// <summary>On/off switch: a white capsule thumb with a soft shadow on a pill that turns from grey
    /// to the accent. The thumb slides with an ease-out and stretches toward its travel while pressed.</summary>
    public sealed class Switch : ToggleButton
    {
        protected override Type StyleKeyOverride => typeof(Switch);

        /// <summary>Where the thumb is, 0 (off) to 1 (on); animated, the render follows it.</summary>
        static readonly StyledProperty<double> PosProperty = AvaloniaProperty.Register<Switch, double>(nameof(Pos));
        /// <summary>0..1: how far the thumb is stretched (pressed).</summary>
        static readonly StyledProperty<double> SquashProperty = AvaloniaProperty.Register<Switch, double>(nameof(Squash));
        static Switch() => AffectsRender<Switch>(PosProperty, SquashProperty);
        double Pos { get => GetValue(PosProperty); set => SetValue(PosProperty, value); }
        double Squash { get => GetValue(SquashProperty); set => SetValue(SquashProperty, value); }
        public Switch() : this(false) { }

        public Switch(bool on, double w = 32, double h = 18)
        {
            Width = M(w); Height = M(h);
            Cursor = Build.Hand;
            FocusRing(this);
            IsChecked = on;
            Pos = on ? 1 : 0;  // no slide at start
            Transitions = ReducedMotion ? null : new Transitions
            {
                new DoubleTransition { Property = PosProperty, Duration = TimeSpan.FromMilliseconds(240), Easing = new Avalonia.Animation.Easings.CubicEaseOut() },
                new DoubleTransition { Property = SquashProperty, Duration = TimeSpan.FromMilliseconds(140), Easing = new Avalonia.Animation.Easings.QuadraticEaseOut() },
            };
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == IsCheckedProperty) Pos = IsChecked == true ? 1 : 0;
            if (change.Property == IsPressedProperty) Squash = IsPressed ? 1 : 0;
            if (change.Property == IsPointerOverProperty || change.Property == IsEffectivelyEnabledProperty) InvalidateVisual();
        }

        public override void Render(DrawingContext g)
        {
            double w = Bounds.Width, h = Bounds.Height, p = Pos, a = IsEffectivelyEnabled ? 1 : 0.4;
            var off = Blend(Look.Background, Look.Text, IsPointerOver && IsEffectivelyEnabled ? 0.18 : 0.13);
            var track = Blend(off, IsPointerOver && IsEffectivelyEnabled ? AccentHi : Accent, p);
            g.DrawRectangle(B(Alpha(track, a)), null, new Rect(0, 0, w, h), h / 2, h / 2);
            double inset = Math.Max(2, Math.Round(h * 0.07)), d = h - 2 * inset;
            double thumbWidth = Math.Min(w - 2 * inset, Math.Round(w * 0.625));
            double travel = w - 2 * inset - thumbWidth;
            double stretch = Squash * Math.Min(d * 0.28, travel);  // keep the pressed capsule inside the track
            double x = inset + p * travel - stretch * p;
            var thumb = new Rect(x, inset, thumbWidth + stretch, d);
            var shadow = new BoxShadows(new BoxShadow { OffsetY = 1.5, Blur = 4, Color = Color.FromArgb((byte)(90 * a), 0, 0, 0) });
            g.DrawRectangle(B(Alpha(White, IsEffectivelyEnabled ? 1 : 0.6)), null, new RoundedRect(thumb, d / 2), shadow);
        }
    }
}

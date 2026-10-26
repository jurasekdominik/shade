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
    public sealed class Switch : Control
    {
        protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new SwitchPeer(this);

        /// <summary>Where the thumb is, 0 (off) to 1 (on); animated, the render follows it.</summary>
        static readonly StyledProperty<double> PosProperty = AvaloniaProperty.Register<Switch, double>(nameof(Pos));
        /// <summary>0..1: how far the thumb is stretched (pressed).</summary>
        static readonly StyledProperty<double> SquashProperty = AvaloniaProperty.Register<Switch, double>(nameof(Squash));
        static Switch() => AffectsRender<Switch>(PosProperty, SquashProperty);
        double Pos { get => GetValue(PosProperty); set => SetValue(PosProperty, value); }
        double Squash { get => GetValue(SquashProperty); set => SetValue(SquashProperty, value); }
        bool on, hot, pressed;
        public static readonly DirectProperty<Switch, bool> OnProperty =
            AvaloniaProperty.RegisterDirect<Switch, bool>(nameof(On), o => o.On, (o, v) => o.On = v, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
        public event Action<bool>? Toggled;

        public Switch() : this(false) { }

        public Switch(bool on, double w = 32, double h = 18)
        {
            Width = M(w); Height = M(h);
            Cursor = Build.Hand;
            FocusRing(this);
            KeyDown += (_, e) => { if (e.Key is Key.Space or Key.Enter) { Toggle(); e.Handled = true; } };
            this.on = on;
            Pos = on ? 1 : 0;  // no slide at start
            Transitions = ReducedMotion ? null : new Transitions
            {
                new DoubleTransition { Property = PosProperty, Duration = TimeSpan.FromMilliseconds(240), Easing = new Avalonia.Animation.Easings.CubicEaseOut() },
                new DoubleTransition { Property = SquashProperty, Duration = TimeSpan.FromMilliseconds(140), Easing = new Avalonia.Animation.Easings.QuadraticEaseOut() },
            };
            PointerEntered += (s, e) => { hot = true; InvalidateVisual(); };
            PointerExited += (s, e) => { hot = false; if (pressed) { pressed = false; Squash = 0; } InvalidateVisual(); };
            PointerPressed += (s, e) => { if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && IsEffectivelyEnabled) { pressed = true; Squash = 1; e.Handled = true; } };
            PointerReleased += (s, e) =>
            {
                if (!pressed) return;
                pressed = false; Squash = 0; e.Handled = true;
                if (e.InitialPressMouseButton == MouseButton.Left) Toggle();
            };
            PropertyChanged += (s, e) => { if (e.Property == IsEnabledProperty || e.Property == IsEffectivelyEnabledProperty) InvalidateVisual(); };
        }

        public bool On { get => on; set { if (value == on) return; SetAndRaise(OnProperty, ref on, value); Pos = on ? 1 : 0; Toggled?.Invoke(on); } }
        public void Toggle() { if (IsEffectivelyEnabled) On = !On; }

        public override void Render(DrawingContext g)
        {
            double w = Bounds.Width, h = Bounds.Height, p = Pos, a = IsEffectivelyEnabled ? 1 : 0.4;
            var off = Blend(Look.Background, Look.Text, hot && IsEffectivelyEnabled ? 0.18 : 0.13);
            var track = Blend(off, hot && IsEffectivelyEnabled ? AccentHi : Accent, p);
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

// Shade - progress: a 4 px bar with its label, for known progress, work of unknown length, and a countdown.
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using static Shade.Look;

namespace Shade
{
    /// <summary>A progress bar (DESIGN_SYSTEM.md §11 Busy): a 6 px pill, the track in Fill and its part in the accent, both
    /// with round ends (the part is never shorter than its own height). Value 0..1 shows
    /// how far (Detail on the right, "64 %"); Value null is work of unknown length (a segment slides). Countdown: the part
    /// empties toward the next event in Text 3. With a Label it takes 22 px for the line above the bar; without, just the bar
    /// (the status bar's).</summary>
    public sealed class Progress : Control
    {
        double? value = 0;
        string? label, detail;
        bool countdown;
        /// <summary>The sliding segment stays put (renders for the docs and checks compare pixels).</summary>
        internal static bool Still;

        public static readonly DirectProperty<Progress, double?> ValueProperty =
            AvaloniaProperty.RegisterDirect<Progress, double?>(nameof(Value), o => o.Value, (o, v) => o.Value = v, 0);
        public static readonly DirectProperty<Progress, string?> LabelProperty =
            AvaloniaProperty.RegisterDirect<Progress, string?>(nameof(Label), o => o.Label, (o, v) => o.Label = v);
        public static readonly DirectProperty<Progress, string?> DetailProperty =
            AvaloniaProperty.RegisterDirect<Progress, string?>(nameof(Detail), o => o.Detail, (o, v) => o.Detail = v);
        public static readonly DirectProperty<Progress, bool> CountdownProperty =
            AvaloniaProperty.RegisterDirect<Progress, bool>(nameof(Countdown), o => o.Countdown, (o, v) => o.Countdown = v);
        static Progress()
        {
            AffectsRender<Progress>(ValueProperty, LabelProperty, DetailProperty, CountdownProperty);
            AffectsMeasure<Progress>(LabelProperty);
        }

        /// <summary>0..1; null = work of unknown length.</summary>
        public double? Value { get => value; set => SetAndRaise(ValueProperty, ref this.value, value is double v ? Math.Clamp(v, 0, 1) : null); }
        public string? Label { get => label; set => SetAndRaise(LabelProperty, ref label, value); }
        public string? Detail { get => detail; set => SetAndRaise(DetailProperty, ref detail, value); }
        public bool Countdown { get => countdown; set => SetAndRaise(CountdownProperty, ref countdown, value); }

        const double H = 6, R = H / 2;
        protected override Size MeasureOverride(Size available) => new Size(double.IsInfinity(available.Width) ? 240 : available.Width, label == null ? H : 22 + H);

        public override void Render(DrawingContext g)
        {
            double w = Bounds.Width, y = label == null ? 0 : 22;
            if (label != null)
            {
                double dw = detail == null ? 0 : Look.Measure(detail, Ui(TextSize.Label)) + 2;
                Draw(g, label, Ui(TextSize.Label), Text2, new Rect(0, 0, w - dw - 8, 16));
                if (detail != null) Draw(g, detail, Ui(TextSize.Label), Text2, new Rect(w - dw, 0, dw, 16), TextAlignment.Right);
            }
            var track = new Rect(0, y, w, H);
            g.DrawRectangle(B(Fill), null, track, R, R);
            Color fill = countdown ? Text3 : Accent;
            if (value is double v) { if (v > 0) g.DrawRectangle(B(fill), null, new Rect(0, y, Math.Max(H, w * v), H), R, R); return; }
            // the next frame paints the segment on: only while it is drawn, so a hidden or closed bar costs nothing
            if (!Still) TopLevel.GetTopLevel(this)?.RequestAnimationFrame(_ => InvalidateVisual());
            double t = Still ? 0.45 : Environment.TickCount64 % 1400 / 1400.0, seg = w * 0.28;
            double x = -seg + (w + seg) * ((1 - Math.Cos(Math.PI * t)) / 2);  // eased across and off the end, then again
            using (g.PushClip(new RoundedRect(track, R))) g.DrawRectangle(B(fill), null, new Rect(x, y, seg, H), R, R);
        }
    }
}

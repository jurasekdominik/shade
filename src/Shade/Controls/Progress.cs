// Shade - progress: a 4 px bar with its label, for known progress, work of unknown length, and a countdown.
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using static Shade.Look;

namespace Shade
{
    /// <summary>A progress bar (DESIGN_SYSTEM.md §11 Busy): a 4 px track in Fill and its part in the accent. Value 0..1 shows
    /// how far (Detail on the right, "64 %"); Value null is work of unknown length (a segment slides). Countdown: the part
    /// empties toward the next event in Text 3. With a Label it takes 22 px for the line above the bar; without, just the bar
    /// (the status bar's).</summary>
    public sealed class Progress : Control
    {
        double? value = 0;
        string label, detail;
        bool countdown;
        readonly DispatcherTimer tick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        /// <summary>The sliding segment stays put (renders for the docs and checks compare pixels).</summary>
        internal static bool Still;

        public Progress()
        {
            tick.Tick += (s, e) => InvalidateVisual();
            AttachedToVisualTree += (s, e) => Run();
            DetachedFromVisualTree += (s, e) => tick.Stop();
        }

        public double? Value { get => value; set { this.value = value is double v ? Math.Clamp(v, 0, 1) : null; Run(); InvalidateVisual(); } }
        public string Label { get => label; set { label = value; InvalidateMeasure(); InvalidateVisual(); } }
        public string Detail { get => detail; set { detail = value; InvalidateVisual(); } }
        public bool Countdown { get => countdown; set { countdown = value; InvalidateVisual(); } }

        void Run() { if (value == null && !Still && VisualRoot != null) tick.Start(); else tick.Stop(); }

        protected override Size MeasureOverride(Size available) => new Size(double.IsInfinity(available.Width) ? 240 : available.Width, label == null ? 4 : 26);

        public override void Render(DrawingContext g)
        {
            double w = Bounds.Width, y = label == null ? 0 : 22;
            if (label != null)
            {
                double dw = detail == null ? 0 : Look.Measure(detail, Ui(9)) + 2;
                Draw(g, label, Ui(9), Second, new Rect(0, 0, w - dw - 8, 16));
                if (detail != null) Draw(g, detail, Ui(9), Second, new Rect(w - dw, 0, dw, 16), TextAlignment.Right);
            }
            var track = new Rect(0, y, w, 4);
            g.DrawRectangle(B(Surface2), null, track, 2, 2);
            Color fill = countdown ? Faint : Accent;
            if (value is double v) { if (v > 0) g.DrawRectangle(B(fill), null, new Rect(0, y, Math.Max(4, w * v), 4), 2, 2); return; }
            double t = Still ? 0.45 : Environment.TickCount64 % 1400 / 1400.0, seg = w * 0.28;
            double x = -seg + (w + seg) * ((1 - Math.Cos(Math.PI * t)) / 2);  // eased across and off the end, then again
            using (g.PushClip(new RoundedRect(track, 2))) g.DrawRectangle(B(fill), null, new Rect(x, y, seg, 4), 2, 2);
        }
    }
}

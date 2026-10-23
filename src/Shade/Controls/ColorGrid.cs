// Shade - the accent helper under the custom colour field: a palette of Tailwind shades (low-contrast ones faded) and a
// mixer (saturation / brightness field, hue bar) with the contrast line dotted in.
using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using static Shade.Look;

namespace Shade
{
    public sealed class ColorGrid : Control
    {
        internal static readonly (string hue, string[] shades)[] Palette = {
            ("red", new[] { "#fecaca", "#fca5a5", "#f87171", "#ef4444", "#dc2626" }),
            ("orange", new[] { "#fed7aa", "#fdba74", "#fb923c", "#f97316", "#ea580c" }),
            ("amber", new[] { "#fde68a", "#fcd34d", "#fbbf24", "#f59e0b", "#d97706" }),
            ("yellow", new[] { "#fef08a", "#fde047", "#facc15", "#eab308", "#ca8a04" }),
            ("lime", new[] { "#d9f99d", "#bef264", "#a3e635", "#84cc16", "#65a30d" }),
            ("green", new[] { "#bbf7d0", "#86efac", "#4ade80", "#22c55e", "#16a34a" }),
            ("emerald", new[] { "#a7f3d0", "#6ee7b7", "#34d399", "#10b981", "#059669" }),
            ("teal", new[] { "#99f6e4", "#5eead4", "#2dd4bf", "#14b8a6", "#0d9488" }),
            ("cyan", new[] { "#a5f3fc", "#67e8f9", "#22d3ee", "#06b6d4", "#0891b2" }),
            ("sky", new[] { "#bae6fd", "#7dd3fc", "#38bdf8", "#0ea5e9", "#0284c7" }),
            ("blue", new[] { "#bfdbfe", "#93c5fd", "#60a5fa", "#3b82f6", "#2563eb" }),
            ("indigo", new[] { "#c7d2fe", "#a5b4fc", "#818cf8", "#6366f1", "#4f46e5" }),
            ("violet", new[] { "#ddd6fe", "#c4b5fd", "#a78bfa", "#8b5cf6", "#7c3aed" }),
            ("purple", new[] { "#e9d5ff", "#d8b4fe", "#c084fc", "#a855f7", "#9333ea" }),
            ("fuchsia", new[] { "#f5d0fe", "#f0abfc", "#e879f9", "#d946ef", "#c026d3" }),
            ("pink", new[] { "#fbcfe8", "#f9a8d4", "#f472b6", "#ec4899", "#db2777" }),
            ("rose", new[] { "#fecdd3", "#fda4af", "#fb7185", "#f43f5e", "#e11d48" }),
        };
        static readonly int[] ShadeNames = { 200, 300, 400, 500, 600 };
        const int Rows = 5;
        const double MinContrast = 4.5, Cell = 24, Gap = 4, Pad = 16, GridTop = 34;
        static int Cols => Palette.Length;
        static double GridH => Rows * Cell + (Rows - 1) * Gap;

        public event Action<Color, bool> Picked;
        int mode, hover = -1, drag;            // drag: 0 none, 1 field, 2 hue bar
        double hue, sat, val;
        Color current;
        readonly bool background;
        readonly Rect[] tabs = new Rect[2];

        Rect FieldRect => new Rect(Pad, GridTop, Width - 2 * Pad, GridH - 24);
        Rect HueRect => new Rect(Pad, FieldRect.Bottom + 10, Width - 2 * Pad, 14);

        public ColorGrid(Color start, bool background = false)
        {
            this.background = background; current = start;
            var hsv = start.ToHsv();
            (hue, sat, val) = (hsv.H, hsv.S, hsv.V);
            Width = 2 * Pad + Cols * Cell + (Cols - 1) * Gap;
            Height = GridTop + GridH + 58;
            PointerPressed += Down;
            PointerMoved += Move;
            PointerReleased += Up;
            PointerExited += (s, e) => { hover = -1; InvalidateVisual(); };
        }

        public static Color Hsv(double h, double s, double v)
        {
            double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c, r, g, b;
            if (h < 60) (r, g, b) = (c, x, 0);
            else if (h < 120) (r, g, b) = (x, c, 0);
            else if (h < 180) (r, g, b) = (0, c, x);
            else if (h < 240) (r, g, b) = (0, x, c); else if (h < 300) (r, g, b) = (x, 0, c); else (r, g, b) = (c, 0, x);
            return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
        }

        static Color At(int i) => Color.Parse(Palette[i % Cols].shades[i / Cols]);
        static string ShadeName(int i) => $"{Palette[i % Cols].hue}-{ShadeNames[i / Cols]}";
        /// <summary>Same rounding as the label, so "4.5 : 1" is never shown as low contrast.</summary>
        bool Readable(Color c) => background || Math.Round(Contrast(c, Bg), 1) >= MinContrast;
        Color Mixed => Hsv(hue, sat, val);

        static int CellAt(Point p)
        {
            int col = (int)((p.X - Pad) / (Cell + Gap)), row = (int)((p.Y - GridTop) / (Cell + Gap));
            return p.X >= Pad && p.Y >= GridTop && col < Cols && row < Rows ? row * Cols + col : -1;
        }

        void Down(object s, PointerPressedEventArgs e)
        {
            var p = e.GetPosition(this);
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            e.Handled = true;
            for (int t = 0; t < 2; t++)
                if (tabs[t].Contains(p)) { mode = t; hover = -1; InvalidateVisual(); return; }
            if (mode == 0) { int i = CellAt(p); if (i >= 0) { current = At(i); Picked?.Invoke(current, true); } return; }
            drag = FieldRect.Contains(p) ? 1 : HueRect.Inflate(new Thickness(0, 6)).Contains(p) ? 2 : 0;
            if (drag != 0) { e.Pointer.Capture(this); DragTo(p); }
        }

        void Move(object s, PointerEventArgs e)
        {
            var p = e.GetPosition(this);
            if (drag != 0) { DragTo(p); return; }
            bool overTab = tabs[0].Contains(p) || tabs[1].Contains(p);
            if (mode == 0)
            {
                int h = CellAt(p);
                if (h != hover) { hover = h; InvalidateVisual(); }
                Cursor = h >= 0 || overTab ? Kit.Hand : Avalonia.Input.Cursor.Default;
            }
            else Cursor = overTab ? Kit.Hand : FieldRect.Contains(p) || HueRect.Contains(p) ? new Cursor(StandardCursorType.Cross) : Avalonia.Input.Cursor.Default;
        }

        void Up(object s, PointerReleasedEventArgs e)
        {
            if (drag == 0) return;
            drag = 0;
            e.Pointer.Capture(null);
            current = Mixed;
            Picked?.Invoke(current, false);  // the whole app recolours once, on release
        }

        void DragTo(Point p)
        {
            if (drag == 1)
            {
                var f = FieldRect;
                sat = Math.Max(0, Math.Min(1, (p.X - f.X) / (f.Width - 1)));
                val = Math.Max(0, Math.Min(1, 1 - (p.Y - f.Y) / (f.Height - 1)));
            }
            else hue = Math.Max(0, Math.Min(359.9, (p.X - HueRect.X) / HueRect.Width * 360));
            InvalidateVisual();
        }

        public override void Render(DrawingContext g)
        {
            var box = new Rect(Bounds.Size);
            FillRound(g, box, 12, Surface, MenuEdge);
            var tf = Ui(9, 600);
            double tx = Pad;
            for (int t = 0; t < 2; t++)
            {
                string label = t == 0 ? "P A L E T T E" : "M I X E R";
                double w = Look.Measure(label, tf);
                tabs[t] = new Rect(tx - 4, 4, w + 8, 26);
                Draw(g, label, tf, t == mode ? Look.Text : Faint, new Rect(tx, 9, w + 4, 16));
                if (t == mode) g.FillRectangle(B(Accent), new Rect(tx, 25, w, 2));
                tx += w + 18;
            }
            Draw(g, background ? "Row background" : mode == 0 ? "faded = low contrast" : "below the dotted line = low contrast", Ui(9), Faint,
                 new Rect(Bounds.Width - Pad - 260, 9, 260, 16), TextAlignment.Right);
            if (mode == 0) PaintPalette(g); else PaintMixer(g);

            // footer: the colour in question, its contrast, a sample button
            var show = mode == 1 ? Mixed : hover >= 0 ? At(hover) : current;
            double cr = Math.Round(background ? Contrast(OnColor(show), show) : Contrast(show, Bg), 1);
            double fy = GridTop + GridH + 16;
            g.DrawLine(new Pen(B(Look.Border)), new Point(Pad, fy - 8.5), new Point(Bounds.Width - Pad, fy - 8.5));
            g.DrawEllipse(B(show), null, new Rect(Pad, fy + 6, 22, 22));
            Draw(g, HexOf(show), MonoF(14), Look.Text, new Rect(Pad + 32, fy + 4, 90, 26));
            string what = mode == 1 ? "mixed" : hover >= 0 ? ShadeName(hover) : "current";
            Draw(g, what, Ui(9), Faint, new Rect(Pad + 116, fy + 4, 90, 26));
            Draw(g, cr.ToString("0.0", CultureInfo.InvariantCulture) + " : 1  " + (cr >= MinContrast ? "readable" : cr >= 3 ? "borderline" : Current == Mode.Dark ? "too dark" : "too light"), Ui(9, 600),
                 cr >= MinContrast ? Second : DangerHi, new Rect(Pad + 206, fy + 4, 150, 26));
            var btn = new Rect(Bounds.Width - Pad - 84, fy + 2, 84, 30);
            FillRound(g, btn, 8, show);
            Draw(g, "Sample", Ui(10.5, 600), OnColor(show), btn, TextAlignment.Center);
        }

        void PaintPalette(DrawingContext g)
        {
            for (int i = 0; i < Cols * Rows; i++)
            {
                double x = Pad + i % Cols * (Cell + Gap), y = GridTop + i / Cols * (Cell + Gap);
                var c = At(i);
                bool ok = Readable(c), cur = HexOf(c) == HexOf(current);
                var r = new Rect(x, y, Cell, Cell);
                if (!ok) r = r.Deflate(4);
                FillRound(g, r, ok ? 6 : 4, ok ? c : Blend(Surface, c, 0.45));
                if (i == hover || cur)
                    g.DrawRectangle(null, new Pen(B(i == hover ? Look.Text : c), 1.6), new Rect(x - 3, y - 3, Cell + 6, Cell + 6), 8, 8);
            }
        }

        void PaintMixer(DrawingContext g)
        {
            var f = FieldRect;
            using (g.PushClip(new RoundedRect(f, 8)))
            {
                // saturation left to right (white to the hue), brightness top to bottom (to black): exactly HSV
                g.FillRectangle(new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(Colors.White, 0), new GradientStop(Hsv(hue, 1, 1), 1) },
                }, f);
                g.FillRectangle(new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(Color.FromArgb(0, 0, 0, 0), 0), new GradientStop(Colors.Black, 1) },
                }, f);
                var pts = new List<Point>();  // per column, the lowest brightness that still reaches the contrast minimum
                for (double x = 0; !background && x < f.Width; x += 3)
                {
                    double s = x / (f.Width - 1), lo = 0, hi = 1;
                    for (int k = 0; k < 16; k++) { double mid = (lo + hi) / 2; if (Contrast(Hsv(hue, s, mid), Bg) >= MinContrast) hi = mid; else lo = mid; }
                    pts.Add(new Point(f.X + x, f.Y + (1 - hi) * (f.Height - 1)));
                }
                var dots = new Pen(B(Color.FromArgb(220, 255, 255, 255)), 1.3) { DashStyle = DashStyle.Dot };
                for (int i = 1; i < pts.Count; i++) g.DrawLine(dots, pts[i - 1], pts[i]);
            }
            double mx = f.X + sat * (f.Width - 1), my = f.Y + (1 - val) * (f.Height - 1), r = 6;
            g.DrawEllipse(null, new Pen(B(Color.FromArgb(120, 0, 0, 0)), 1), new Point(mx, my), r + 1.5, r + 1.5);
            g.DrawEllipse(null, new Pen(Brushes.White, 2), new Point(mx, my), r, r);
            var hb = HueRect;
            var bar = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative) };
            for (int i = 0; i < 7; i++) bar.GradientStops.Add(new GradientStop(Hsv(i * 60 % 360, 1, 1), i / 6.0));
            g.DrawRectangle(bar, null, hb, hb.Height / 2, hb.Height / 2);
            double hx = hb.X + hue / 360 * hb.Width, hr = 8, hy = hb.Y + hb.Height / 2;
            g.DrawEllipse(B(Hsv(hue, 1, 1)), new Pen(Brushes.White, 2), new Point(hx, hy), hr, hr);
        }
    }
}

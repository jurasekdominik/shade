// Shade - summary cards: a row of compact status cards over a list (monitoring style: a label, a value, one line).
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using static Shade.Look;

namespace Shade
{
    /// <summary>A row of equal cards (DESIGN_SYSTEM.md §6 Summary cards), 84 px high: the title and a corner value on top;
    /// below, an optional 40 px ring (a share) or trend line at the left, and beside it the value in its status colour with
    /// its unit and one line on what needs a look.
    /// Detailed: 112 px, for a row that says more (the Jobs page's cards): the title in Section with a subtitle under it and
    /// the corner value, the value at 24 px with its meaning, the line, and the ring or trend line at the right.
    /// With Picked set, a click picks a card (a filter); an active card has an accent frame. Cards wrap into rows below
    /// 160 px (200 px detailed).</summary>
    public sealed class SummaryCards : Control
    {
        public sealed record Item
        {
            /// <summary>What Picked hands back; the title when unset.</summary>
            public string? Key { get; init; }
            public required string Title { get; init; }
            public required string Value { get; init; }
            public string? Unit { get; init; }
            /// <summary>One line on what needs a look; Corner: a small value at the top right (next run, a window).</summary>
            public string? Line { get; init; }
            public string? Corner { get; init; }
            /// <summary>Detailed cards only: one line under the title (what the card counts).</summary>
            public string? Sub { get; init; }
            /// <summary>0..1: a ring in the value's colour. Trend: a small line of the last values, instead of the ring.</summary>
            public double? Share { get; init; }
            public IReadOnlyList<double>? Trend { get; init; }
            /// <summary>The value's colour (Success, Warn, Danger…); the line's colour; null = Text and Text 3.</summary>
            public Tone? Tone { get; init; }
            public Tone? LineTone { get; init; }
            public bool Active { get; init; }
        }

        public static readonly DirectProperty<SummaryCards, IReadOnlyList<Item>> ItemsProperty =
            AvaloniaProperty.RegisterDirect<SummaryCards, IReadOnlyList<Item>>(nameof(Items), o => o.Items, (o, v) => o.Items = v);
        public static readonly DirectProperty<SummaryCards, bool> DetailedProperty =
            AvaloniaProperty.RegisterDirect<SummaryCards, bool>(nameof(Detailed), o => o.Detailed, (o, v) => o.Detailed = v);
        static SummaryCards() { AffectsMeasure<SummaryCards>(ItemsProperty, DetailedProperty); AffectsRender<SummaryCards>(ItemsProperty, DetailedProperty); }

        public IReadOnlyList<Item> Items { get => items; set => SetAndRaise(ItemsProperty, ref items, value ?? Array.Empty<Item>()); }
        IReadOnlyList<Item> items = Array.Empty<Item>();
        /// <summary>A card was clicked (its Key); unset = the cards only show.</summary>
        public event Action<string>? Picked;
        int hot = -1;
        bool detailed;
        /// <summary>The larger card, with a subtitle and a 24 px value (112 px high instead of 84).</summary>
        public bool Detailed { get => detailed; set => SetAndRaise(DetailedProperty, ref detailed, value); }
        const double Gap = 12;
        double CardH => detailed ? 112 : 84;
        double MinCard => detailed ? 200 : 160;

        public SummaryCards()
        {
            PointerMoved += (s, e) => { int h = Picked == null ? -1 : At(e.GetPosition(this)); if (h != hot) { hot = h; Cursor = h >= 0 ? Kit.Hand : Cursor.Default; InvalidateVisual(); } };
            PointerExited += (s, e) => { hot = -1; InvalidateVisual(); };
            PointerReleased += (s, e) => { int i = At(e.GetPosition(this)); if (i >= 0 && e.InitialPressMouseButton == MouseButton.Left) Picked?.Invoke(items[i].Key ?? items[i].Title); };
        }

        int Cols(double width) => Math.Max(1, Math.Min(items.Count, (int)((width + Gap) / (MinCard + Gap))));
        int Rows(double width) => items.Count == 0 ? 0 : (items.Count + Cols(width) - 1) / Cols(width);
        Rect CardAt(int i, double width)
        {
            int cols = Cols(width); double w = (width - Gap * (cols - 1)) / cols;
            return new Rect(i % cols * (w + Gap), i / cols * (CardH + Gap), w, CardH);
        }
        int At(Point p) { for (int i = 0; i < items.Count; i++) if (CardAt(i, Bounds.Width).Contains(p)) return i; return -1; }

        protected override Size MeasureOverride(Size available)
        {
            double w = double.IsInfinity(available.Width) ? items.Count * (200 + Gap) - Gap : available.Width;
            int rows = Rows(w);
            return new Size(w, rows == 0 ? 0 : rows * CardH + (rows - 1) * Gap);
        }

        public override void Render(DrawingContext g)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i]; var r = CardAt(i, Bounds.Width);
                FillRound(g, r, CardRadius, i == hot ? Surface2 : Surface, it.Active ? Accent : i == hot ? MenuEdge : Look.Border);
                if (detailed) { Detail(g, it, r); continue; }
                double x = r.X + 12, right = r.Right - 12, inner = right - x;
                // the title (Label) and the corner value (Caption), on one line
                double cw = it.Corner == null ? 0 : Math.Min(inner * 0.5, Look.Measure(it.Corner, Ui(8.25)) + 2);
                Draw(g, it.Title, Ui(9, 500), Second, new Rect(x, r.Y + 10, inner - cw - 6, 16));
                if (it.Corner != null) Draw(g, it.Corner, Ui(8.25), Faint, new Rect(right - cw, r.Y + 10, cw, 16), TextAlignment.Right);
                // the figure at the bottom left (a 40 px ring or a 64 px trend line), the value and its line beside it
                double figW = it.Trend is { Count: > 1 } ? 64 : it.Share != null ? 40 : 0;
                if (it.Trend is { Count: > 1 } t) Spark(g, t, new Rect(x, r.Y + 40, figW, 30), it.Tone ?? Accent);
                else if (it.Share is double sh) Ring(g, new Point(x + 20, r.Y + 54), 18, sh, it.Tone ?? Accent);
                double tx = x + (figW > 0 ? figW + 12 : 0), space = right - tx;
                var valueF = Ui(15, 600);
                double vw = Math.Min(space, Look.Measure(it.Value, valueF) + 2);
                Draw(g, it.Value, valueF, it.Tone ?? Look.Text, new Rect(tx, r.Y + 30, vw, 26), ellipsis: false);
                if (it.Unit != null) Draw(g, it.Unit, Ui(9), Second, new Rect(tx + vw + 6, r.Y + 36, Math.Max(0, space - vw - 6), 16));
                if (it.Line != null) Draw(g, it.Line, Ui(8.25), it.LineTone ?? Faint, new Rect(tx, r.Y + 58, space, 16));
            }
        }

        /// <summary>A detailed card: the Jobs page's layout on Shade's sizes.</summary>
        static void Detail(DrawingContext g, Item it, Rect r)
        {
            double x = r.X + 16, right = r.Right - 16;
            bool trend = it.Trend is { Count: > 1 };
            double figW = trend ? 72 : it.Share != null ? 44 : 0, textW = right - x - (figW > 0 ? figW + 12 : 0);
            double cw = it.Corner == null ? 0 : Math.Min(textW * 0.6, Look.Measure(it.Corner, Ui(8.25)) + 2);
            Draw(g, it.Title, Ui(10.5, 600), Look.Text, new Rect(x, r.Y + 12, right - x - cw - 8, 20));
            if (it.Corner != null) Draw(g, it.Corner, Ui(8.25), Faint, new Rect(right - cw, r.Y + 14, cw, 16), TextAlignment.Right);
            if (it.Sub != null) Draw(g, it.Sub, Ui(8.25), Faint, new Rect(x, r.Y + 32, textW, 16));
            var valueF = Ui(18, 600);  // 24 px
            double vw = Math.Min(textW, Look.Measure(it.Value, valueF) + 2);
            Draw(g, it.Value, valueF, it.Tone ?? Look.Text, new Rect(x, r.Y + 50, vw, 32), ellipsis: false);
            if (it.Unit != null) Draw(g, it.Unit, Ui(9), Second, new Rect(x + vw + 8, r.Y + 58, Math.Max(0, textW - vw - 8), 18));
            if (it.Line != null) Draw(g, it.Line, Ui(9), it.LineTone ?? Faint, new Rect(x, r.Y + 84, right - x, 18));
            if (it.Trend is { Count: > 1 } tr) Spark(g, tr, new Rect(right - figW, r.Y + 46, figW, 32), it.Tone ?? Accent);
            else if (it.Share is double sh) Ring(g, new Point(right - 20, r.Y + 62), 19, sh, it.Tone ?? Accent);
        }

        static void Ring(DrawingContext g, Point c, double r, double share, Color col)
        {
            g.DrawEllipse(null, new Pen(B(Surface2), 5), c, r, r);
            share = Math.Clamp(share, 0, 1);
            if (share >= 0.999) { g.DrawEllipse(null, new Pen(B(col), 5), c, r, r); return; }
            if (share <= 0) return;
            double a0 = -Math.PI / 2, a1 = a0 + 2 * Math.PI * share;
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(new Point(c.X + r * Math.Cos(a0), c.Y + r * Math.Sin(a0)), false);
                ctx.ArcTo(new Point(c.X + r * Math.Cos(a1), c.Y + r * Math.Sin(a1)), new Size(r, r), 0, share > 0.5, SweepDirection.Clockwise);
            }
            g.DrawGeometry(null, new Pen(B(col), 5, lineCap: PenLineCap.Round), geo);
        }

        static void Spark(DrawingContext g, IReadOnlyList<double> values, Rect box, Color col)
        {
            double lo = values.Min(), hi = values.Max(), span = hi - lo < 1e-9 ? 1 : hi - lo;
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
                for (int i = 0; i < values.Count; i++)
                {
                    var p = new Point(box.X + box.Width * i / (values.Count - 1), box.Bottom - (values[i] - lo) / span * box.Height);
                    if (i == 0) ctx.BeginFigure(p, false); else ctx.LineTo(p);
                }
            g.DrawGeometry(null, new Pen(B(col), 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), geo);
        }
    }
}

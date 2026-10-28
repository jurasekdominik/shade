// Shade - summary cards: a row of compact status cards over a list (monitoring style: a label, a value, one line).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
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
    /// Detailed: 112 px, for a row that says more (an overview page's cards): the title in Section with a subtitle under it and
    /// the corner value, the value at 24 px with its meaning, the line, and the ring or trend line at the right.
    /// With Picked set, a click picks a card (a filter); an active card has an accent frame. Cards wrap into rows below
    /// 160 px (200 px detailed).</summary>
    public sealed class SummaryCards : Panel
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
            /// <summary>The tooltip, also on a disabled card (say why it is off).</summary>
            public string? ToolTip { get; init; }
            /// <summary>False: the card is dimmed, takes no click and no focus.</summary>
            public bool IsEnabled { get; init; } = true;
            /// <summary>Runs on a click, with CommandParameter, next to Picked and SelectedKey; a card with one is clickable.</summary>
            public ICommand? Command { get; init; }
            public object? CommandParameter { get; init; }
        }

        public static readonly DirectProperty<SummaryCards, IReadOnlyList<Item>> ItemsProperty =
            AvaloniaProperty.RegisterDirect<SummaryCards, IReadOnlyList<Item>>(nameof(Items), o => o.Items, (o, v) => o.Items = v);
        public static readonly DirectProperty<SummaryCards, bool> DetailedProperty =
            AvaloniaProperty.RegisterDirect<SummaryCards, bool>(nameof(Detailed), o => o.Detailed, (o, v) => o.Detailed = v);
        public static readonly DirectProperty<SummaryCards, string?> SelectedKeyProperty =
            AvaloniaProperty.RegisterDirect<SummaryCards, string?>(nameof(SelectedKey), o => o.SelectedKey, (o, v) => o.SelectedKey = v, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
        public static readonly DirectProperty<SummaryCards, bool> SelectableProperty =
            AvaloniaProperty.RegisterDirect<SummaryCards, bool>(nameof(Selectable), o => o.Selectable, (o, v) => o.Selectable = v);
        static SummaryCards() { AffectsMeasure<SummaryCards>(ItemsProperty, DetailedProperty); AffectsRender<SummaryCards>(ItemsProperty, DetailedProperty); }

        string? selectedKey;
        /// <summary>The key (the title when a card has none) of the selected card, which gets the accent frame; a click on a card
        /// sets it, so it binds two-way. Null selects none; an Item's own Active still frames it.</summary>
        public string? SelectedKey { get => selectedKey; set => SetAndRaise(SelectedKeyProperty, ref selectedKey, value); }
        bool selectable;
        /// <summary>Cards can be clicked to select (SelectedKey) without a Picked handler; Picked or a card's Command make them clickable too.</summary>
        public bool Selectable { get => selectable; set => SetAndRaise(SelectableProperty, ref selectable, value); }
        bool Pickable => picked != null || selectable || items.Any(i => i.Command != null);
        bool Selected(Item it) => it.Active || selectedKey != null && (it.Key ?? it.Title) == selectedKey;

        public IReadOnlyList<Item> Items { get => items; set => SetAndRaise(ItemsProperty, ref items, value ?? Array.Empty<Item>()); }
        IReadOnlyList<Item> items = Array.Empty<Item>();
        /// <summary>A card was clicked (its Key); unset = the cards only show.</summary>
        Action<string>? picked;
        public event Action<string>? Picked
        {
            add { picked += value; BuildCards(); }
            remove { picked -= value; BuildCards(); }
        }
        int hot = -1;
        bool detailed;
        /// <summary>The larger card, with a subtitle and a 24 px value (112 px high instead of 84).</summary>
        public bool Detailed { get => detailed; set => SetAndRaise(DetailedProperty, ref detailed, value); }
        static double Gap => M(12);
        double CardH => M(detailed ? 112 : 84);
        double MinCard => M(detailed ? 200 : 160);

        public SummaryCards()
        {
            PointerMoved += (s, e) => { int h = Pickable ? At(e.GetPosition(this)) : -1; if (h >= 0 && !items[h].IsEnabled) h = -1; if (h != hot) { hot = h; Cursor = h >= 0 ? Build.Hand : Cursor.Default; PaintCards(); } };
            PointerExited += (s, e) => { hot = -1; PaintCards(); };
            BuildCards();
        }

        readonly List<CardFace> faces = new();
        void PaintCards() { foreach (var face in faces) face.InvalidateVisual(); }
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == ItemsProperty || change.Property == DetailedProperty || change.Property == SelectableProperty) BuildCards();
            else if (change.Property == SelectedKeyProperty) PaintCards();
        }
        void BuildCards()
        {
            if (faces == null) return;
            var focused = Children.OfType<Button>().FirstOrDefault(button => button.IsFocused)?.Tag as Item;
            faces.Clear(); Children.Clear(); hot = -1;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i]; int index = i;
                var face = new CardFace(this, item, index); faces.Add(face);
                if (!Pickable) { Children.Add(face); continue; }
                var button = new Button
                {
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
                    Tag = item,
                    Padding = new Thickness(0),
                    BorderThickness = new Thickness(0),
                    MinHeight = 0,
                    MinWidth = 0,
                    IsEnabled = item.IsEnabled,
                    Template = new Avalonia.Controls.Templates.FuncControlTemplate<Button>((_, _) => face)
                };
                Avalonia.Automation.AutomationProperties.SetName(button, face.Description);
                button.Click += (_, _) =>
                {
                    if (!IsEffectivelyEnabled || !item.IsEnabled || !items.Contains(item)) return;
                    if (item.Command is { } command && command.CanExecute(item.CommandParameter)) command.Execute(item.CommandParameter);
                    if (selectable) SelectedKey = item.Key ?? item.Title;
                    picked?.Invoke(item.Key ?? item.Title);
                };
                button.KeyDown += (_, e) =>
                {
                    int step = e.Key switch { Key.Left => -1, Key.Right => 1, Key.Up => -Cols(Bounds.Width), Key.Down => Cols(Bounds.Width), _ => 0 };
                    int next = e.Key == Key.Home ? 0 : e.Key == Key.End ? Children.Count - 1 : Math.Clamp(index + step, 0, Children.Count - 1);
                    if (step != 0 || e.Key is Key.Home or Key.End) { Children[next].Focus(NavigationMethod.Directional); e.Handled = true; }
                };
                FocusRing(button); Children.Add(button);
            }
            if (focused != null) Children.OfType<Button>().FirstOrDefault(button => button.Tag is Item item && (focused.Key != null ? item.Key == focused.Key : item.Title == focused.Title))?.Focus(NavigationMethod.Directional);
            InvalidateMeasure();
        }
        sealed class CardFace : Control
        {
            readonly SummaryCards owner;
            readonly Item item;
            readonly int index;
            public string Description => string.Join(" · ", new[] { item.Title, item.Value, item.Unit, item.Line }.Where(s => !string.IsNullOrEmpty(s)));
            public CardFace(SummaryCards owner, Item item, int index)
            {
                this.owner = owner; this.item = item; this.index = index;
                Avalonia.Automation.AutomationProperties.SetName(this, Description);
                if (item.ToolTip != null) { ToolTip.SetTip(this, item.ToolTip); ToolTip.SetShowOnDisabled(this, true); }
            }
            protected override Size MeasureOverride(Size availableSize) => new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : owner.MinCard, owner.CardH);
            protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new CardTextPeer(this, owner.Pickable);
            public override void Render(DrawingContext context) => owner.DrawCard(context, item, index, Bounds.Size);
        }
        sealed class CardTextPeer(CardFace owner, bool decorative) : Avalonia.Automation.Peers.ControlAutomationPeer(owner)
        {
            protected override Avalonia.Automation.Peers.AutomationControlType GetAutomationControlTypeCore() => Avalonia.Automation.Peers.AutomationControlType.Text;
            protected override bool IsControlElementCore() => !decorative;
            protected override bool IsContentElementCore() => !decorative;
        }
        protected override Size ArrangeOverride(Size finalSize)
        {
            for (int i = 0; i < Children.Count; i++) Children[i].Arrange(CardAt(i, finalSize.Width));
            return finalSize;
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
            double w = double.IsInfinity(available.Width) ? Math.Max(0, items.Count * (M(200) + Gap) - Gap) : available.Width;
            for (int i = 0; i < Children.Count; i++) Children[i].Measure(CardAt(i, w).Size);
            int rows = Rows(w);
            return new Size(w, rows == 0 ? 0 : rows * CardH + (rows - 1) * Gap);
        }

        static Font CardFont(double size, int weight = 400) => Ui(size / MetricScale, weight);
        void DrawCard(DrawingContext g, Item it, int i, Size size)
        {
            using var scaled = g.PushTransform(Matrix.CreateScale(MetricScale, MetricScale));
            using var dim = g.PushOpacity(it.IsEnabled ? 1 : 0.5);
            var r = new Rect(0, 0, size.Width / MetricScale, size.Height / MetricScale);
            FillRound(g, r, CardRadius, i == hot ? Fill : Raised, Selected(it) ? Accent : i == hot ? Outline : Look.Separator);
            if (detailed) { Detail(g, it, r); return; }
            double x = r.X + 12, right = r.Right - 12, inner = right - x;
            // the title (Label) and the corner value (Caption), on one line
            double cw = it.Corner == null ? 0 : Math.Min(inner * 0.5, Look.Measure(it.Corner, CardFont(TextSize.Caption)) + 2);
            Draw(g, it.Title, CardFont(TextSize.Label, 500), Text2, new Rect(x, r.Y + 10, inner - cw - 6, 16));
            if (it.Corner != null) Draw(g, it.Corner, CardFont(TextSize.Caption), Text3, new Rect(right - cw, r.Y + 10, cw, 16), TextAlignment.Right);
            // the figure at the bottom left (a 40 px ring or a 64 px trend line), the value and its line beside it
            double figW = it.Trend is { Count: > 1 } ? 64 : it.Share != null ? 40 : 0;
            if (it.Trend is { Count: > 1 } t) Spark(g, t, new Rect(x, r.Y + 40, figW, 30), it.Tone ?? Accent);
            else if (it.Share is double sh) Ring(g, new Point(x + 20, r.Y + 54), 18, sh, it.Tone ?? Accent);
            double tx = x + (figW > 0 ? figW + 12 : 0), space = right - tx;
            var valueF = CardFont(TextSize.Title, 600);
            double vw = Math.Min(space, Look.Measure(it.Value, valueF) + 2);
            Draw(g, it.Value, valueF, it.Tone ?? Look.Text, new Rect(tx, r.Y + 30, vw, 26), ellipsis: false);
            if (it.Unit != null) Draw(g, it.Unit, CardFont(TextSize.Label), Text2, new Rect(tx + vw + 6, r.Y + 36, Math.Max(0, space - vw - 6), 16));
            if (it.Line != null) Draw(g, it.Line, CardFont(TextSize.Caption), it.LineTone ?? Text3, new Rect(tx, r.Y + 58, space, 16));
        }

        /// <summary>A detailed card: title, subtitle, a 24 px value, its line, and the figure at the right.</summary>
        static void Detail(DrawingContext g, Item it, Rect r)
        {
            double x = r.X + 16, right = r.Right - 16;
            bool trend = it.Trend is { Count: > 1 };
            double figW = trend ? 72 : it.Share != null ? 44 : 0, textW = right - x - (figW > 0 ? figW + 12 : 0);
            double cw = it.Corner == null ? 0 : Math.Min(textW * 0.6, Look.Measure(it.Corner, CardFont(TextSize.Caption)) + 2);
            Draw(g, it.Title, CardFont(TextSize.Section, 600), Look.Text, new Rect(x, r.Y + 12, right - x - cw - 8, 20));
            if (it.Corner != null) Draw(g, it.Corner, CardFont(TextSize.Caption), Text3, new Rect(right - cw, r.Y + 14, cw, 16), TextAlignment.Right);
            if (it.Sub != null) Draw(g, it.Sub, CardFont(TextSize.Caption), Text3, new Rect(x, r.Y + 32, textW, 16));
            var valueF = CardFont(24 * TextScale, 600);  // 24 px
            double vw = Math.Min(textW, Look.Measure(it.Value, valueF) + 2);
            Draw(g, it.Value, valueF, it.Tone ?? Look.Text, new Rect(x, r.Y + 50, vw, 32), ellipsis: false);
            if (it.Unit != null) Draw(g, it.Unit, CardFont(TextSize.Label), Text2, new Rect(x + vw + 8, r.Y + 58, Math.Max(0, textW - vw - 8), 18));
            if (it.Line != null) Draw(g, it.Line, CardFont(TextSize.Label), it.LineTone ?? Text3, new Rect(x, r.Y + 84, right - x, 18));
            if (it.Trend is { Count: > 1 } tr) Spark(g, tr, new Rect(right - figW, r.Y + 46, figW, 32), it.Tone ?? Accent);
            else if (it.Share is double sh) Ring(g, new Point(right - 20, r.Y + 62), 19, sh, it.Tone ?? Accent);
        }

        static void Ring(DrawingContext g, Point c, double r, double share, Color col)
        {
            g.DrawEllipse(null, new Pen(B(Fill), 5), c, r, r);
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

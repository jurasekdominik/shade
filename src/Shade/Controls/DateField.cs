// Shade - the date field: a field for one day (typed as 2026-10-24 or 24.10.2026) with a calendar icon that opens a
// month calendar under it (MonthGrid), keyboard included.
using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using static Shade.Look;

namespace Shade
{
    /// <summary>One day (DESIGN_SYSTEM.md §6 Date field): typed (yyyy-MM-dd, or d.M.yyyy) or picked in the month calendar a click
    /// opens under the field (from its icon). Down or Alt+Down opens it from the keyboard: arrows move a day or a week, Page Up/Down a month,
    /// Enter picks, Esc closes. Value is null while empty; a day outside Min..Max is refused.</summary>
    public sealed class DateField : Field
    {
        public static readonly DirectProperty<DateField, DateTime?> ValueProperty =
            AvaloniaProperty.RegisterDirect<DateField, DateTime?>(nameof(Value), o => o.Value, (o, v) => o.Value = v, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
        /// <summary>The way a day is shown, and the first way it is read back.</summary>
        public const string Format = "yyyy-MM-dd";
        static readonly string[] Reads = { Format, "yyyy-M-d", "d.M.yyyy", "d. M. yyyy" };
        readonly Avalonia.Controls.Shapes.Path icon = Build.Icon24(Icons.Calendar, 16);
        DateTime? value;
        bool typing;
        Popup? calendar;

        /// <summary>The day was changed by a pick, by typing or by Clear.</summary>
        public event Action? Changed;

        /// <summary>The day (no time), or null.</summary>
        public DateTime? Value
        {
            get => value;
            set { SetAndRaise(ValueProperty, ref this.value, value?.Date); if (!typing) { Text = this.value?.ToString(Format, CultureInfo.InvariantCulture) ?? ""; Invalid = false; } }
        }
        /// <summary>The earliest and latest days that can be picked or typed; null = no limit.</summary>
        public DateTime? Min { get; set; }
        public DateTime? Max { get; set; }
        /// <summary>The calendar's first column.</summary>
        public DayOfWeek FirstDay { get; set; } = DayOfWeek.Monday;
        /// <summary>How the calendar marks Saturday and Sunday.</summary>
        public WeekendMark Weekends { get; set; } = WeekendMark.Red;

        public DateField() : base("yyyy-mm-dd", false)
        {
            Width = 140;
            icon.Stroke = B(Text3); icon.HorizontalAlignment = HorizontalAlignment.Center; icon.VerticalAlignment = VerticalAlignment.Center;
            // a 20 px target around the icon (its lines alone are thin to hit), as the search field's ×
            var button = new Border { Child = icon, Width = 20, Height = 20, Background = Brushes.Transparent, Cursor = Build.Hand, Margin = new Thickness(4, 0, -4, 0), VerticalAlignment = VerticalAlignment.Center };
            ToolTip.SetTip(button, "Calendar");
            button.PointerEntered += (s, e) => icon.Stroke = B(Text2);
            button.PointerExited += (s, e) => icon.Stroke = B(Text3);
            var d = new DockPanel();
            DockPanel.SetDock(button, Dock.Right);
            d.Children.Add(button); d.Children.Add(Box);
            Child = d;
            Box.MaxLength = 12;
            Edited += () =>
            {
                string t = Text.Trim();
                typing = true;
                if (t == "") Set(null);
                else if (Read(t) is DateTime day && Allowed(day)) Set(day);
                typing = false;
            };
            Box.LostFocus += (s, e) => { if (calendar == null) Settle(); };
            Box.AddHandler(KeyDownEvent, (s, e) =>
            {
                if (e.Key == Key.Down) { e.Handled = true; ShowCalendar(keyboard: true); }
                else if (e.Key == Key.Enter) Settle();
            }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            // the icon opens the calendar; a click on the text places the caret, for typing
            button.PointerPressed += (s, e) => e.Handled = true;
            button.PointerReleased += (s, e) => { if (e.InitialPressMouseButton == MouseButton.Left) ShowCalendar(keyboard: false); };
        }

        /// <summary>A typed day: yyyy-MM-dd, yyyy-M-d, d.M.yyyy or d. M. yyyy.</summary>
        public static DateTime? Read(string text) =>
            DateTime.TryParseExact(text.Trim(), Reads, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : null;

        internal bool Allowed(DateTime day) => (Min == null || day >= Min.Value.Date) && (Max == null || day <= Max.Value.Date);

        void Set(DateTime? day)
        {
            if (Nullable.Equals(day, value)) return;
            Value = day;
            Changed?.Invoke();
        }

        /// <summary>Leaving the field: what doesn't read as an allowed day is marked; a day read is shown in Format.</summary>
        void Settle()
        {
            string t = Text.Trim();
            if (t == "") { Invalid = false; return; }
            if (Read(t) is DateTime day && Allowed(day)) { Set(day); Text = day.ToString(Format, CultureInfo.InvariantCulture); Invalid = false; }
            else Invalid = true;
        }

        internal Popup? ShowCalendar(bool keyboard)
        {
            if (calendar != null || !IsEnabled) return null;
            var grid = new MonthGrid(this, Value ?? Read(Text) ?? MonthGrid.Now());
            var card = new Border { Background = B(Raised), BorderBrush = B(Outline), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(CardRadius), Child = grid };
            calendar = new Popup { PlacementTarget = this, Placement = PlacementMode.BottomEdgeAlignedLeft, VerticalOffset = 4, IsLightDismissEnabled = true, OverlayDismissEventPassThrough = true, Child = card };
            ((ISetLogicalParent)calendar).SetParent(this);
            grid.Picked += day => { Set(day); if (day == null) Text = ""; Invalid = false; Close(); Box.Focus(); };
            grid.Cancel += () => { Close(); Box.Focus(); };
            calendar.Closed += (s, e) => { if (calendar != null) ((ISetLogicalParent)calendar).SetParent(null); calendar = null; };
            calendar.IsOpen = true;
            if (keyboard) grid.Focus(NavigationMethod.Tab);
            return calendar;
        }

        void Close() { if (calendar != null) calendar.IsOpen = false; }
    }

    /// <summary>How a calendar marks Saturday and Sunday: on a faint red band with their days in a muted red (as printed calendars
    /// do), on a faint grey band, or not at all.</summary>
    public enum WeekendMark { Red, Grey, None }

    /// <summary>A month of days to pick from (the date field's calendar): the month and ‹ ›, the weekdays, 6 × 7 days of 28 px,
    /// Today and Clear. Drawn, so every state follows a new mode or accent at the next paint.</summary>
    internal sealed class MonthGrid : Control
    {
        const double Pad = 12, Cell = 28, Gap = 4, Step = Cell + Gap, Head = Cell, DaysTop = Pad + Head + 8 + 16 + 4, FootTop = DaysTop + 6 * Step - Gap + 8;
        const int Prev = -2, Next = -3, Today = -4, Clear = -5;
        readonly DateField owner;
        DateTime month, cursor;
        int hover = -1;
        bool keys;  // the cursor shows: the calendar is used from the keyboard
        /// <summary>Today: the gallery fixes it, so its pictures don't change from day to day.</summary>
        internal static Func<DateTime> Now = () => DateTime.Today;
        public event Action<DateTime?>? Picked;
        public event Action? Cancel;

        public MonthGrid(DateField owner, DateTime start)
        {
            this.owner = owner;
            cursor = start.Date; month = new DateTime(cursor.Year, cursor.Month, 1);
            Width = 2 * Pad + 7 * Step - Gap; Height = FootTop + Cell + Pad;
            Focusable = true;
            PointerMoved += (s, e) => { int h = At(e.GetPosition(this)); if (h != hover) { hover = h; Cursor = h != -1 ? Build.Hand : Cursor.Default; InvalidateVisual(); } };
            PointerExited += (s, e) => { hover = -1; InvalidateVisual(); };
            PointerPressed += (s, e) => e.Handled = true;  // a press inside isn't the background (Look's blur)
            PointerReleased += (s, e) => { if (e.InitialPressMouseButton == MouseButton.Left) Act(At(e.GetPosition(this))); };
            KeyDown += (s, e) =>
            {
                int days = e.Key switch { Key.Left => -1, Key.Right => 1, Key.Up => -7, Key.Down => 7, _ => 0 };
                if (days != 0) Move(cursor.AddDays(days));
                else if (e.Key == Key.PageUp) Move(cursor.AddMonths(-1));
                else if (e.Key == Key.PageDown) Move(cursor.AddMonths(1));
                else if (e.Key == Key.Home) Move(cursor.AddDays(-Column(cursor)));
                else if (e.Key == Key.End) Move(cursor.AddDays(6 - Column(cursor)));
                else if (e.Key is Key.Enter or Key.Space) { if (owner.Allowed(cursor)) Picked?.Invoke(cursor); }
                else if (e.Key == Key.Escape) Cancel?.Invoke();
                else return;
                e.Handled = true;
            };
            GotFocus += (s, e) => { keys = e.NavigationMethod != NavigationMethod.Pointer; InvalidateVisual(); };
        }

        int Column(DateTime day) => ((int)day.DayOfWeek - (int)owner.FirstDay + 7) % 7;
        DateTime First => month.AddDays(-Column(month));
        void Move(DateTime day) { cursor = day; month = new DateTime(day.Year, day.Month, 1); keys = true; InvalidateVisual(); }

        Rect CellRect(int i) => new Rect(Pad + i % 7 * Step, DaysTop + i / 7 * Step, Cell, Cell);
        Rect PrevRect => new Rect(Width - Pad - 2 * Cell - Gap, Pad, Cell, Cell);
        Rect NextRect => new Rect(Width - Pad - Cell, Pad, Cell, Cell);
        Rect TodayRect => new Rect(Pad, FootTop, 56, Cell);
        Rect ClearRect => new Rect(Width - Pad - 56, FootTop, 56, Cell);

        int At(Point p)
        {
            if (PrevRect.Contains(p)) return Prev;
            if (NextRect.Contains(p)) return Next;
            if (TodayRect.Contains(p) && owner.Allowed(Now())) return Today;
            if (ClearRect.Contains(p)) return Clear;
            for (int i = 0; i < 42; i++) if (CellRect(i).Contains(p)) return owner.Allowed(First.AddDays(i)) ? i : -1;
            return -1;
        }

        void Act(int what)
        {
            if (what == Prev) Move(cursor.AddMonths(-1));
            else if (what == Next) Move(cursor.AddMonths(1));
            else if (what == Today) Picked?.Invoke(Now());
            else if (what == Clear) Picked?.Invoke(null);
            else if (what >= 0) Picked?.Invoke(First.AddDays(what));
            keys = false;
        }

        public override void Render(DrawingContext g)
        {
            g.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
            Draw(g, month.ToString("MMMM yyyy", CultureInfo.InvariantCulture), Ui(TextSize.Section, 600), Look.Text, new Rect(Pad + 4, Pad, 150, Cell));
            foreach (var (r, which, data) in new[] { (PrevRect, Prev, Icons.ChevronLeft), (NextRect, Next, Icons.ChevronRight) })
            {
                if (hover == which) FillRound(g, r, Radius, Fill);
                using (g.PushTransform(Matrix.CreateScale(16 / 24.0, 16 / 24.0) * Matrix.CreateTranslation(r.X + 6, r.Y + 6)))
                    g.DrawGeometry(null, new Pen(B(hover == which ? Look.Text : Text2), 2 * 24 / 16.0, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), data);
            }
            // weekends: red mixed into the text colours, so they read as Saturday and Sunday, not as an error, in both modes
            bool red = owner.Weekends == WeekendMark.Red;
            Color weekendText = Blend(Text2, Danger, 0.55), weekendHead = Blend(Text3, Danger, 0.55);
            for (int c = 0; c < 7; c++)
            {
                var weekday = (DayOfWeek)(((int)owner.FirstDay + c) % 7);
                bool weekend = owner.Weekends != WeekendMark.None && weekday is DayOfWeek.Saturday or DayOfWeek.Sunday;
                if (weekend) FillRound(g, new Rect(Pad + c * Step - Gap / 2, DaysTop - 22, Step, 6 * Step + 20), Radius, red ? Tint(Danger, 0.07) : Alpha(Fill, 0.7));
                Draw(g, weekday.ToString().Substring(0, 2), Ui(TextSize.Caption, 500), weekend && red ? weekendHead : Text3, new Rect(Pad + c * Step, DaysTop - 20, Cell, 16), TextAlignment.Center);
            }
            var first = First;
            for (int i = 0; i < 42; i++)
            {
                var day = first.AddDays(i); var r = CellRect(i);
                bool picked = owner.Value == day, allowed = owner.Allowed(day), inMonth = day.Month == month.Month, today = day == Now();
                if (picked) FillRound(g, r, Radius, Accent);
                else if (hover == i || keys && day == cursor) FillRound(g, r, Radius, Fill);
                if (today && !picked) FillRound(g, r, Radius, Colors.Transparent, Accent);
                if (keys && day == cursor && picked) FillRound(g, r.Inflate(2), Radius + 2, Colors.Transparent, Look.Text);
                bool weekend = red && day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                Color text = picked ? OnAccent : !allowed ? Disabled : weekend ? (inMonth ? weekendText : Alpha(weekendText, 0.55)) : !inMonth ? Text3 : Look.Text;
                Draw(g, day.Day.ToString(CultureInfo.InvariantCulture), Ui(TextSize.Body, picked || today ? 600 : 400), text, r, TextAlignment.Center, false);
            }
            if (owner.Allowed(Now())) Draw(g, "Today", Ui(TextSize.Label, 600), hover == Today ? AccentHi : AccentText, TodayRect.Deflate(new Thickness(4, 0)));
            Draw(g, "Clear", Ui(TextSize.Label, 500), hover == Clear ? Look.Text : Text2, ClearRect.Deflate(new Thickness(4, 0)), TextAlignment.Right);
        }
    }
}

// Shade - the date fields: a day, or a range of days, typed as YYYY-MM-DD blocks or picked in a month calendar (MonthGrid)
// that a click anywhere in the field opens, keyboard included.
using System;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using static Shade.Look;

namespace Shade
{
    /// <summary>One day (DESIGN.md). Year, month and day are blocks: digits fill a block and move on ("4" in
    /// the month is April), Up/Down change it, Left/Right move between blocks, Backspace empties it, "-", "." or "/" move on, Ctrl+V
    /// takes 2026-10-24 or 24.10.2026. A click anywhere opens the month calendar under the field; Alt+Down or F4 opens it for the
    /// keyboard (arrows move a day or a week, Page Up/Down a month, Enter picks, Esc closes). Value is null while a block is
    /// empty; a day outside Min..Max marks the field (Invalid) and leaves Value as it was.</summary>
    public sealed class DateField : Field
    {
        protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new DatePeer(this);
        public static readonly DirectProperty<DateField, DateTime?> ValueProperty =
            AvaloniaProperty.RegisterDirect<DateField, DateTime?>(nameof(Value), o => o.Value, (o, v) => o.Value = v, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
        /// <summary>The way a day is shown, and the first way it is read back.</summary>
        public const string Format = "yyyy-MM-dd";
        static readonly string[] Reads = { Format, "yyyy-M-d", "d.M.yyyy", "d. M. yyyy" };
        readonly DateBlocks blocks = new DateBlocks();
        readonly Calendar calendar;
        DateTime? value;
        bool fromBlocks;

        /// <summary>The day was changed by a pick, by typing or by Clear.</summary>
        public event Action? Changed;

        /// <summary>The day (no time), or null.</summary>
        public DateTime? Value
        {
            get => value;
            set
            {
                SetAndRaise(ValueProperty, ref this.value, value?.Date);
                if (!fromBlocks) { blocks.Set(this.value); Invalid = false; }
                RaiseText(); calendar.Follow();
            }
        }
        /// <summary>The earliest and latest days that can be picked or typed; null = no limit.</summary>
        public static readonly StyledProperty<DateTime?> MinProperty = AvaloniaProperty.Register<DateField, DateTime?>(nameof(Min));
        public DateTime? Min { get => GetValue(MinProperty); set => SetValue(MinProperty, value); }
        public static readonly StyledProperty<DateTime?> MaxProperty = AvaloniaProperty.Register<DateField, DateTime?>(nameof(Max));
        public DateTime? Max { get => GetValue(MaxProperty); set => SetValue(MaxProperty, value); }
        /// <summary>The calendar's first column.</summary>
        public DayOfWeek FirstDay { get; set; } = DayOfWeek.Monday;
        /// <summary>How the calendar marks Saturday and Sunday.</summary>
        public WeekendMark Weekends { get; set; } = WeekendMark.Red;

        public DateField() : base("", false)
        {
            HorizontalAlignment = HorizontalAlignment.Left;
            calendar = new Calendar(this, () => new[] { blocks }, () => (value, null), () => false, day => { Set(day, true); return true; });
            Child = calendar.Frame(blocks);
            blocks.Changed += () =>
            {
                var day = blocks.Day;
                bool refused = day is DateTime d && !Allowed(d);
                Invalid = refused;
                if (!refused) Set(day);
            };
            blocks.Pasted += text => { if (Read(text) is DateTime d && Allowed(d)) Set(d, true); else Invalid = true; };
        }

        /// <summary>A typed day: yyyy-MM-dd, yyyy-M-d, d.M.yyyy or d. M. yyyy.</summary>
        public static DateTime? Read(string text) =>
            DateTime.TryParseExact((text ?? "").Trim(), Reads, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : null;

        internal bool Input(string text)
        {
            if (IsReadOnly) return false;
            if (text == "") { Set(null, true); return true; }
            if (Read(text) is not DateTime day || !Allowed(day)) return false;
            Set(day, true); return true;
        }
        internal bool Allowed(DateTime day) => (Min == null || day >= Min.Value.Date) && (Max == null || day <= Max.Value.Date);

        /// <summary>The day as yyyy-MM-dd ("" while empty); setting it reads a typed day.</summary>
        public override string Text { get => value?.ToString(Format, CultureInfo.InvariantCulture) ?? ""; set => Value = Read(value ?? ""); }

        void Set(DateTime? day, bool show = false)
        {
            if (show) blocks.Set(day);
            if (Nullable.Equals(day?.Date, value)) return;
            fromBlocks = !show; Value = day; fromBlocks = false;
            Changed?.Invoke();
        }

        internal Popup? ShowCalendar(bool keyboard) => calendar.Show(keyboard);
        internal DateBlocks Blocks => blocks;
    }

    /// <summary>A range of days in one field (DESIGN.md): the start's blocks, "–", the end's, as DateField's.
    /// A click opens the month; it colours the days between Start and End. A pick in the start sets it and asks for the end
    /// (hovering previews the range); a pick in the end closes the month; a picked end before the start swaps them, a typed
    /// one marks the field (Invalid).</summary>
    public sealed class DateRangeField : Field
    {
        protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new DatePeer(this);
        public static readonly DirectProperty<DateRangeField, DateTime?> StartProperty =
            AvaloniaProperty.RegisterDirect<DateRangeField, DateTime?>(nameof(Start), o => o.Start, (o, v) => o.Start = v, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
        public static readonly DirectProperty<DateRangeField, DateTime?> EndProperty =
            AvaloniaProperty.RegisterDirect<DateRangeField, DateTime?>(nameof(End), o => o.End, (o, v) => o.End = v, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
        readonly DateBlocks from = new DateBlocks(), until = new DateBlocks();
        readonly Calendar calendar;
        DateTime? start, end;
        bool fromBlocks;
        int target;  // the end a pick sets: 0 the start, 1 the end

        /// <summary>Start or End was changed by a pick, by typing or by Clear.</summary>
        public event Action? Changed;

        /// <summary>The first day, or null.</summary>
        public DateTime? Start { get => start; set { SetAndRaise(StartProperty, ref start, value?.Date); if (!fromBlocks) from.Set(start); RaiseText(); calendar.Follow(); } }
        /// <summary>The last day, or null.</summary>
        public DateTime? End { get => end; set { SetAndRaise(EndProperty, ref end, value?.Date); if (!fromBlocks) until.Set(end); RaiseText(); calendar.Follow(); } }
        /// <summary>The earliest and latest days that can be picked or typed; null = no limit.</summary>
        public static readonly StyledProperty<DateTime?> MinProperty = AvaloniaProperty.Register<DateRangeField, DateTime?>(nameof(Min));
        public DateTime? Min { get => GetValue(MinProperty); set => SetValue(MinProperty, value); }
        public static readonly StyledProperty<DateTime?> MaxProperty = AvaloniaProperty.Register<DateRangeField, DateTime?>(nameof(Max));
        public DateTime? Max { get => GetValue(MaxProperty); set => SetValue(MaxProperty, value); }
        /// <summary>The calendar's first column.</summary>
        public DayOfWeek FirstDay { get; set; } = DayOfWeek.Monday;
        /// <summary>How the calendar marks Saturday and Sunday.</summary>
        public WeekendMark Weekends { get; set; } = WeekendMark.Red;

        public DateRangeField() : base("", false)
        {
            HorizontalAlignment = HorizontalAlignment.Left;
            calendar = new Calendar(this, () => new[] { from, until }, () => (start, end), () => target == 1, Pick);
            var to = Build.Text("–", Text3, TextSize.Body); to.Margin = new Thickness(6, 0); to.VerticalAlignment = VerticalAlignment.Center;
            var both = new StackPanel { Orientation = Orientation.Horizontal, Children = { from, to, until } };
            Child = calendar.Frame(both);
            foreach (var (b, g) in new[] { (from, 0), (until, 1) })
            {
                b.GotFocus += (s, e) => { target = g; calendar.Follow(); };
                b.Changed += () => { var day = b.Day; bool refused = day is DateTime d && !Allowed(d); if (!refused) Set(g, day); Invalid = refused || Backwards; };
                b.Pasted += text => { Invalid = !Input(text); };
            }
            from.Past += dir => { if (dir > 0) { until.At = 0; until.Focus(); } };
            until.Past += dir => { if (dir < 0) { from.At = 2; from.Focus(); } };
        }

        /// <summary>A typed range: two days (as DateField.Read) between "/", "–" or " - ".</summary>
        public static (DateTime start, DateTime end)? Read(string text)
        {
            var parts = (text ?? "").Split(new[] { "/", "–", " - " }, StringSplitOptions.None);
            return parts.Length == 2 && DateField.Read(parts[0]) is DateTime a && DateField.Read(parts[1]) is DateTime b ? (a <= b ? (a, b) : (b, a)) : null;
        }

        internal bool Input(string text)
        {
            if (IsReadOnly) return false;
            if (text == "") { Set(0, null, true); Set(1, null, true); return true; }
            if (Read(text) is not var (a, b) || !Allowed(a) || !Allowed(b)) return false;
            Set(0, a, true); Set(1, b, true); return true;
        }
        internal bool Allowed(DateTime day) => (Min == null || day >= Min.Value.Date) && (Max == null || day <= Max.Value.Date);
        bool Backwards => start is DateTime a && end is DateTime b && b < a;  // typed so: marked until fixed (a pick swaps them)

        /// <summary>"yyyy-MM-dd/yyyy-MM-dd" ("" until both are set); setting it reads a typed range.</summary>
        public override string Text
        {
            get => start is DateTime a && end is DateTime b ? a.ToString(DateField.Format, CultureInfo.InvariantCulture) + "/" + b.ToString(DateField.Format, CultureInfo.InvariantCulture) : "";
            set { var r = Read(value ?? ""); Start = r?.start; End = r?.end; }
        }

        void Set(int g, DateTime? day, bool show = false)
        {
            if (show) (g == 0 ? from : until).Set(day);
            if (Nullable.Equals(day?.Date, g == 0 ? start : end)) return;
            fromBlocks = !show;
            if (g == 0) Start = day; else End = day;
            fromBlocks = false;
            Changed?.Invoke();
        }

        /// <summary>A pick in the month: true when the month should close.</summary>
        bool Pick(DateTime? day)
        {
            if (day == null) { Set(0, null, true); Set(1, null, true); target = 0; from.Focus(); return false; }
            if (target == 0) { Set(0, day, true); Set(1, null, true); target = 1; until.Focus(); return false; }  // a new start asks for its end
            Set(1, day, true);
            if (start is DateTime a && end is DateTime b && b < a) { Set(0, b, true); Set(1, a, true); }
            return true;
        }

        internal Popup? ShowCalendar(bool keyboard) => calendar.Show(keyboard);
        internal DateBlocks From => from;
        internal DateBlocks Until => until;
    }

    /// <summary>How a calendar marks Saturday and Sunday: on a faint red band with their days in a muted red (as printed calendars
    /// do), on a faint grey band, or not at all.</summary>
    public enum WeekendMark { Red, Grey, None }

    /// <summary>The month popup of a date field and its calendar icon: a click anywhere in the field opens it (the icon closes it
    /// again), Alt+Down or F4 in a block opens it for the keyboard, typing in a block moves it.</summary>
    internal sealed class Calendar
    {
        readonly Field owner;
        readonly Func<DateBlocks[]> blocks;
        readonly Func<(DateTime? start, DateTime? end)> days;
        readonly Func<bool> pickingEnd;
        readonly Func<DateTime?, bool> picked;
        readonly Avalonia.Controls.Shapes.Path icon = Build.Icon24(Icons.Calendar, 16);
        Border? button;
        Popup? popup;
        MonthGrid? grid;

        public Calendar(Field owner, Func<DateBlocks[]> blocks, Func<(DateTime?, DateTime?)> days, Func<bool> pickingEnd, Func<DateTime?, bool> picked)
        {
            this.owner = owner; this.blocks = blocks; this.days = days; this.pickingEnd = pickingEnd; this.picked = day => !owner.IsReadOnly && owner.IsEffectivelyEnabled && picked(day);
        }

        bool Range => owner is DateRangeField;
        DateField? Day => owner as DateField;
        DateRangeField? Span => owner as DateRangeField;
        bool Allowed(DateTime day) => Day != null ? Day.Allowed(day) : Span!.Allowed(day);
        DayOfWeek FirstDay => Day != null ? Day.FirstDay : Span!.FirstDay;
        WeekendMark Weekends => Day != null ? Day.Weekends : Span!.Weekends;

        /// <summary>The owner's content: the blocks, then the calendar icon on a 20 px target.</summary>
        public Control Frame(Control content)
        {
            icon.HorizontalAlignment = HorizontalAlignment.Center; icon.VerticalAlignment = VerticalAlignment.Center;
            button = new Border { Child = icon, Width = 20, Height = 20, Background = Brushes.Transparent, Cursor = Build.Hand, Margin = new Thickness(6, 0, -4, 0), VerticalAlignment = VerticalAlignment.Center };
            Words(button, word => ToolTip.SetTip(word, T("Calendar")), "tip");
            foreach (var (state, tone) in new[] { ("", Text3), (":pointerover", Text2) })  // the icon brightens with the pointer over its target
                button.Styles.Add(new Avalonia.Styling.Style(x => (state == "" ? x.OfType<Border>() : x.OfType<Border>().Class(state)).Child().OfType<Avalonia.Controls.Shapes.Path>()) { Setters = { new Avalonia.Styling.Setter(Avalonia.Controls.Shapes.Shape.StrokeProperty, B(tone)) } });
            content.VerticalAlignment = VerticalAlignment.Center;
            var d = new DockPanel();
            DockPanel.SetDock(button, Dock.Right);
            d.Children.Add(button); d.Children.Add(content);
            owner.Cursor = new Cursor(StandardCursorType.Ibeam);
            owner.PropertyChanged += (_, e) => { if (e.Property == Field.IsReadOnlyProperty && owner.IsReadOnly) Close(); };
            // a press anywhere: the block under it (or the one in use) takes the keyboard and the month opens; the icon closes it
            owner.AddHandler(InputElement.PointerPressedEvent, (s, e) =>
            {
                if (owner.IsReadOnly || !owner.IsEnabled || e.GetCurrentPoint(owner).Properties.PointerUpdateKind != PointerUpdateKind.LeftButtonPressed) return;
                e.Handled = true;
                if (popup != null && button.IsPointerOver) { Close(); return; }
                var all = blocks();
                var hit = all.FirstOrDefault(b => b.IsPointerOver);
                if (hit != null) hit.PickAt(e.GetPosition(hit).X);
                else if (!all.Any(b => b.IsFocused)) all[0].Focus();
                Show(false);
            }, RoutingStrategies.Tunnel, handledEventsToo: true);  // once a press: run on the way back up too, it closed what it had just opened
            foreach (var b in blocks()) b.OpenAsked += () => Show(true);
            owner.AddHandler(InputElement.KeyDownEvent, (s, e) => { if (e.Key == Key.Escape && popup != null) { e.Handled = true; Close(); } }, RoutingStrategies.Tunnel);
            return d;
        }

        public Popup? Show(bool keyboard)
        {
            if (owner.IsReadOnly || !owner.IsEnabled) return null;
            if (popup != null) { if (keyboard) grid?.Focus(NavigationMethod.Tab); return popup; }
            var (a, b) = days();
            var at = (pickingEnd() ? b ?? a : a) ?? MonthGrid.Now();
            grid = new MonthGrid(at, Allowed, FirstDay, Weekends, Range) { Start = a, End = b, PickingEnd = pickingEnd() };
            var card = new Border { Background = B(Raised), BorderBrush = B(Outline), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(CardRadius), Child = grid };
            popup = new Popup { PlacementTarget = owner, Placement = PlacementMode.BottomEdgeAlignedLeft, VerticalOffset = 4, IsLightDismissEnabled = true, OverlayInputPassThroughElement = owner, Child = card };
            Menu.OwnPopup(popup, owner);
            ((ISetLogicalParent)popup).SetParent(owner);
            grid.Picked += day =>
            {
                bool close = picked(day);
                if (close) { Close(); Focused().Focus(); }
                else Follow();
            };
            grid.Cancel += () => { Close(); Focused().Focus(); };
            var shown = popup;
            popup.Closed += (s, e) => { ((ISetLogicalParent)shown).SetParent(null); if (popup == shown) { popup = null; grid = null; } };
            popup.IsOpen = true;
            if (keyboard) grid.Focus(NavigationMethod.Tab);
            return popup;
        }

        DateBlocks Focused() { var all = blocks(); return all.FirstOrDefault(b => b.IsFocused) ?? all[pickingEnd() && all.Length > 1 ? 1 : 0]; }

        /// <summary>The owner's days changed (typed, picked, set): the month shows them, on the day being edited.</summary>
        public void Follow()
        {
            if (grid == null) return;
            var (a, b) = days();
            grid.Start = a; grid.End = b; grid.PickingEnd = pickingEnd();
            if ((pickingEnd() ? b : a) is DateTime d) grid.Show(d);
            grid.InvalidateVisual();
        }

        public void Close() { if (popup != null) popup.IsOpen = false; }
    }

    /// <summary>A date as three drawn blocks, YYYY-MM-DD (a date field's text): the block in use has the accent tint while the
    /// blocks have the keyboard. Each block is as wide as its widest form, digits or placeholder, so nothing moves while typing.</summary>
    internal sealed class DateBlocks : Control
    {
        static readonly (string ph, int n, int max)[] Spec = { ("YYYY", 4, 9999), ("MM", 2, 12), ("DD", 2, 31) };
        readonly int?[] parts = new int?[3];
        int at;
        string typed = "";
        /// <summary>A block changed by the keyboard.</summary>
        public event Action? Changed;
        /// <summary>Alt+Down or F4: open the month for the keyboard.</summary>
        public event Action? OpenAsked;
        /// <summary>Left from the year (-1) or Right from the day (+1): the next blocks of a range.</summary>
        public event Action<int>? Past;
        /// <summary>Ctrl+V: the text on the clipboard, for the field to read.</summary>
        public event Action<string>? Pasted;

        protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new BlocksPeer(this);

        public DateBlocks()
        {
            Focusable = true; Height = 26 * Look.TextScale; VerticalAlignment = VerticalAlignment.Center;
            GotFocus += (s, e) => InvalidateVisual();
            LostFocus += (s, e) => { typed = ""; for (int i = 0; i < 3; i++) if (parts[i] == 0) parts[i] = null; InvalidateVisual(); };
        }

        /// <summary>The block in use: 0 year, 1 month, 2 day.</summary>
        public int At { get => at; set { at = Math.Clamp(value, 0, 2); typed = ""; InvalidateVisual(); } }
        static int DaysIn(int? y, int? m) => DateTime.DaysInMonth(y is int v && v >= 1 ? v : 2000, m is int n && n >= 1 && n <= 12 ? n : 1);  // no year yet: a leap year, so 29 February can be typed
        /// <summary>The day the blocks make, once all three are set (a four-digit year), or null.</summary>
        public DateTime? Day => parts[0] >= 1 && parts[1] >= 1 && parts[2] >= 1 && (at != 0 || typed.Length == 0) ? new DateTime(parts[0]!.Value, parts[1]!.Value, Math.Min(parts[2]!.Value, DaysIn(parts[0], parts[1]))) : null;
        internal string Shown => string.Join("-", Enumerable.Range(0, 3).Select(i => parts[i] is int v ? v.ToString(new string('0', Spec[i].n), CultureInfo.InvariantCulture) : Spec[i].ph));

        /// <summary>Shows a day (or empties the blocks) without Changed.</summary>
        public void Set(DateTime? day)
        {
            parts[0] = day?.Year; parts[1] = day?.Month; parts[2] = day?.Day; typed = "";
            InvalidateVisual();
        }

        static Font Face => Ui(TextSize.Body);
        double BlockWidth(int i) => Math.Ceiling(Math.Max(Look.Measure(Spec[i].ph, Face), Look.Measure(new string('0', Spec[i].n), Face))) + 2;
        double SepWidth => Math.Ceiling(Look.Measure("-", Face)) + 2;
        protected override Size MeasureOverride(Size available) => new Size(BlockWidth(0) + BlockWidth(1) + BlockWidth(2) + 2 * SepWidth, 26 * Look.TextScale);

        /// <summary>A click at x makes the block under it the one in use.</summary>
        public void PickAt(double x)
        {
            double edge = 0;
            for (int i = 0; i < 3; i++) { edge += BlockWidth(i) + (i < 2 ? SepWidth / 2 : 0); if (x < edge || i == 2) { At = i; break; } edge += i < 2 ? SepWidth / 2 : 0; }
            Focus(NavigationMethod.Pointer);
        }

        public override void Render(DrawingContext g)
        {
            g.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
            double x = 0;
            for (int i = 0; i < 3; i++)
            {
                if (i > 0) { Draw(g, "-", Face, Text3, new Rect(x, 0, SepWidth, Bounds.Height), TextAlignment.Center, false); x += SepWidth; }
                var r = new Rect(x, 3, BlockWidth(i), Bounds.Height - 6);
                if (IsFocused && i == at) FillRound(g, r, 2, AccentTint);
                Draw(g, parts[i] is int v ? v.ToString(new string('0', Spec[i].n), CultureInfo.InvariantCulture) : Spec[i].ph, Face, parts[i] == null ? Text3 : Look.Text, new Rect(x, 0, r.Width, Bounds.Height), TextAlignment.Center, false);
                x += r.Width;
            }
        }

        void Fix() { if (parts[2] is int d && parts[1] is int m && m >= 1) parts[2] = Math.Min(d, DaysIn(parts[0], m)); }  // 31 in a 30-day month becomes 30
        void Done() { Fix(); InvalidateVisual(); Changed?.Invoke(); }

        protected override void OnTextInput(TextInputEventArgs e)
        {
            base.OnTextInput(e);
            var t = e.Text ?? "";
            if (t.Length == 1 && char.IsDigit(t[0]))
            {
                e.Handled = true;
                typed += t; int n = int.Parse(typed, CultureInfo.InvariantCulture);
                var (_, len, max) = Spec[at];
                bool full = typed.Length >= len || n * 10 > max;  // "4" in the month can only be April: move on
                parts[at] = full ? Math.Clamp(n, 1, max) : n;  // a lone "0" waits for its second digit
                if (full) { typed = ""; if (at < 2) at++; }
                Done();
            }
            else if (t is "-" or "." or "/") { e.Handled = true; typed = ""; if (at < 2) { at++; InvalidateVisual(); } }
        }

        protected override async void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.F4 || e.Key == Key.Down && e.KeyModifiers.HasFlag(KeyModifiers.Alt)) { e.Handled = true; OpenAsked?.Invoke(); return; }
            if (e.Key == Key.V && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                e.Handled = true;
                var clip = TopLevel.GetTopLevel(this)?.Clipboard;
                var text = clip == null ? null : await Avalonia.Input.Platform.ClipboardExtensions.TryGetTextAsync(clip);
                if (!string.IsNullOrWhiteSpace(text)) Pasted?.Invoke(text);
                return;
            }
            if (e.Key is Key.Up or Key.Down)
            {
                e.Handled = true; typed = "";
                int step = e.Key == Key.Up ? 1 : -1, max = at == 2 ? DaysIn(parts[0], parts[1]) : Spec[at].max;
                var now = MonthGrid.Now();
                parts[at] = parts[at] is int v && v >= 1 ? (at == 0 ? Math.Clamp(v + step, 1, 9999) : (v - 1 + step + max) % max + 1) : at == 0 ? now.Year : at == 1 ? now.Month : now.Day;
                Done();
            }
            else if (e.Key is Key.Left or Key.Right)
            {
                int to = at + (e.Key == Key.Left ? -1 : 1);
                if (to >= 0 && to <= 2) { e.Handled = true; At = to; }
                else if (Past != null) { e.Handled = true; Past(to < 0 ? -1 : 1); }
            }
            else if (e.Key is Key.Back or Key.Delete) { e.Handled = true; typed = ""; parts[at] = null; Done(); }
            else if (e.Key == Key.Home) { e.Handled = true; At = 0; }
            else if (e.Key == Key.End) { e.Handled = true; At = 2; }
        }
    }

    /// <summary>A month of days to pick from (the date fields' calendar): the month and ‹ ›, the weekdays, 6 × 7 days of 28 px,
    /// Today and Clear; a range shows the days between Start and End on the accent tint, its ends filled, and how many days.
    /// Drawn, so every state follows a new mode or accent at the next paint.</summary>
    internal sealed class MonthGrid : Control
    {
        static double Scale => Math.Max(Look.TextScale, Look.Density);
        static double Pad => 12 * Scale;
        static double Cell => 28 * Scale;
        static double Gap => 4 * Scale;
        static double Step => Cell + Gap;
        static double DaysTop => Pad + Cell + 28 * Scale;
        static double FootTop => DaysTop + 6 * Step - Gap + 8 * Scale;
        const int Prev = -2, Next = -3, Today = -4, Clear = -5;
        readonly Func<DateTime, bool> allowed;
        readonly DayOfWeek firstDay;
        readonly WeekendMark weekends;
        readonly bool range;
        DateTime month, cursor;
        DateTime? hoverDay;
        int hover = -1;
        bool keys;  // the cursor shows: the calendar is used from the keyboard
        /// <summary>Today: the gallery fixes it, so its pictures don't change from day to day.</summary>
        internal static Func<DateTime> Now = () => DateTime.Today;
        public event Action<DateTime?>? Picked;
        public event Action? Cancel;
        /// <summary>The picked day (a range: its first day) and a range's last day.</summary>
        public DateTime? Start { get; set; }
        public DateTime? End { get; set; }
        /// <summary>A range's next pick is its end: the day under the pointer (or the cursor) previews it.</summary>
        public bool PickingEnd { get; set; }

        readonly Canvas semantics = new();
        readonly Button[] dayButtons = new Button[42];
        readonly Button[] actions = new Button[4];
        static readonly Avalonia.Controls.Templates.FuncControlTemplate<Button> emptyTemplate = new((_, _) => new Panel());
        void AccessibleDays()
        {
            for (int i = 0; i < 42; i++)
            {
                int index = i;
                var button = new Button { Template = emptyTemplate, Focusable = false, IsHitTestVisible = false };
                button.Click += (_, _) => Act(index); dayButtons[i] = button; semantics.Children.Add(button);
            }
            for (int i = 0; i < 4; i++)
            {
                int action = new[] { Prev, Next, Today, Clear }[i];
                var button = new Button { Template = emptyTemplate, Focusable = false, IsHitTestVisible = false };
                button.Click += (_, _) => Act(action); actions[i] = button; semantics.Children.Add(button);
            }
            Words(this, grid => grid.UpdateAccessibleDays(), "calendar");
        }
        void UpdateAccessibleDays()
        {
            for (int i = 0; i < 42; i++)
            {
                var button = dayButtons[i]; var day = DayAt(i); button.IsVisible = day != null;
                button.IsEnabled = day is DateTime d && allowed(d);
                Avalonia.Automation.AutomationProperties.SetName(button, day?.ToString("D", Culture) ?? "");
                Avalonia.Automation.AutomationProperties.SetHelpText(button, day != null && (day == Start || day == End) ? T("Selected") : "");
            }
            for (int i = 0; i < actions.Length; i++)
                Avalonia.Automation.AutomationProperties.SetName(actions[i], T(new[] { "Previous month", "Next month", "Today", "Clear" }[i]));
            actions[0].IsEnabled = Shift(cursor, months: -1) != cursor;
            actions[1].IsEnabled = Shift(cursor, months: 1) != cursor;
            actions[2].IsEnabled = allowed(Now());
        }
        protected override Size MeasureOverride(Size availableSize)
        {
            semantics.Measure(availableSize); return new Size(Width, Height);
        }
        protected override Size ArrangeOverride(Size finalSize)
        {
            UpdateAccessibleDays();
            void Place(Button button, Rect rect)
            {
                button.Width = rect.Width; button.Height = rect.Height;
                Canvas.SetLeft(button, rect.X); Canvas.SetTop(button, rect.Y);
            }
            for (int i = 0; i < 42; i++) Place(dayButtons[i], CellRect(i));
            var rects = new[] { PrevRect, NextRect, TodayRect, ClearRect };
            for (int i = 0; i < actions.Length; i++) Place(actions[i], rects[i]);
            semantics.Arrange(new Rect(finalSize));
            return finalSize;
        }

        public MonthGrid(DateField owner, DateTime start) : this(start, owner.Allowed, owner.FirstDay, owner.Weekends) { Start = owner.Value; }

        public MonthGrid(DateTime start, Func<DateTime, bool> allowed, DayOfWeek firstDay, WeekendMark weekends, bool range = false)
        {
            this.allowed = allowed; this.firstDay = firstDay; this.weekends = weekends; this.range = range;
            cursor = start.Date; month = new DateTime(cursor.Year, cursor.Month, 1);
            Width = 2 * Pad + 7 * Step - Gap; Height = FootTop + Cell + Pad;
            FocusRing(this);
            VisualChildren.Add(semantics); LogicalChildren.Add(semantics);
            AccessibleDays();
            PointerMoved += (s, e) =>
            {
                int h = At(e.GetPosition(this));
                if (h == hover) return;
                hover = h; Cursor = h != -1 ? Build.Hand : Cursor.Default;
                hoverDay = h >= 0 ? DayAt(h) : null; InvalidateVisual();
            };
            PointerExited += (s, e) => { hover = -1; hoverDay = null; InvalidateVisual(); };
            PointerPressed += (s, e) => e.Handled = true;  // a press inside isn't the background (Look's blur)
            PointerReleased += (s, e) => { if (e.InitialPressMouseButton == MouseButton.Left) Act(At(e.GetPosition(this))); };
            KeyDown += (s, e) =>
            {
                int days = e.Key switch { Key.Left => -1, Key.Right => 1, Key.Up => -7, Key.Down => 7, _ => 0 };
                if (days != 0) Move(Shift(cursor, days));
                else if (e.Key == Key.PageUp) Move(Shift(cursor, months: -1));
                else if (e.Key == Key.PageDown) Move(Shift(cursor, months: 1));
                else if (e.Key == Key.Home) Move(Shift(cursor, -Column(cursor)));
                else if (e.Key == Key.End) Move(Shift(cursor, 6 - Column(cursor)));
                else if (e.Key is Key.Enter or Key.Space) { if (allowed(cursor)) Picked?.Invoke(cursor); }
                else if (e.Key == Key.Escape) Cancel?.Invoke();
                else return;
                e.Handled = true;
            };
            GotFocus += (s, e) => { keys = Look.IsKeyboard(e); InvalidateVisual(); };
        }

        int Column(DateTime day) => ((int)day.DayOfWeek - (int)firstDay + 7) % 7;
        DateTime? DayAt(int index)
        {
            long ticks = month.Ticks + (index - Column(month)) * TimeSpan.TicksPerDay;
            return ticks >= DateTime.MinValue.Ticks && ticks <= DateTime.MaxValue.Ticks ? new DateTime(ticks) : null;
        }
        static DateTime Shift(DateTime day, int days = 0, int months = 0)
        {
            try { return day.AddMonths(months).AddDays(days); }
            catch (ArgumentOutOfRangeException) { return day; }
        }
        void Move(DateTime day) { cursor = day; month = new DateTime(day.Year, day.Month, 1); keys = true; if (range && PickingEnd) hoverDay = day; UpdateAccessibleDays(); InvalidateVisual(); }
        /// <summary>Turns to the month of day, which becomes the cursor.</summary>
        internal void Show(DateTime day) { cursor = day.Date; month = new DateTime(day.Year, day.Month, 1); UpdateAccessibleDays(); InvalidateVisual(); }

        Rect CellRect(int i) => new Rect(Pad + i % 7 * Step, DaysTop + i / 7 * Step, Cell, Cell);
        Rect PrevRect => new Rect(Width - Pad - 2 * Cell - Gap, Pad, Cell, Cell);
        Rect NextRect => new Rect(Width - Pad - Cell, Pad, Cell, Cell);
        Rect TodayRect => new Rect(Pad, FootTop, 56 * Scale, Cell);
        Rect ClearRect => new Rect(Width - Pad - 56 * Scale, FootTop, 56 * Scale, Cell);

        int At(Point p)
        {
            if (PrevRect.Contains(p)) return Prev;
            if (NextRect.Contains(p)) return Next;
            if (TodayRect.Contains(p) && allowed(Now())) return Today;
            if (ClearRect.Contains(p)) return Clear;
            for (int i = 0; i < 42; i++) if (CellRect(i).Contains(p)) return DayAt(i) is DateTime day && allowed(day) ? i : -1;
            return -1;
        }

        void Act(int what)
        {
            if (what == Prev) Move(Shift(cursor, months: -1));
            else if (what == Next) Move(Shift(cursor, months: 1));
            else if (what == Today && allowed(Now())) Picked?.Invoke(Now());
            else if (what == Clear) Picked?.Invoke(null);
            else if (what >= 0 && DayAt(what) is DateTime day && allowed(day)) Picked?.Invoke(day);
            keys = false;
        }

        /// <summary>A range's two ends, in order, with the preview standing in for a missing end.</summary>
        (DateTime? a, DateTime? b) Ends()
        {
            DateTime? a = Start, b = range ? End ?? (PickingEnd && Start != null ? hoverDay : null) : null;
            return a is DateTime x && b is DateTime y && y < x ? (y, x) : (a, b);
        }

        public override void Render(DrawingContext g)
        {
            g.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
            Draw(g, month.ToString("MMMM yyyy", Culture), Ui(TextSize.Section, 600), Look.Text, new Rect(Pad + 4 * Scale, Pad, Math.Max(0, PrevRect.X - Pad - 8 * Scale), Cell));
            foreach (var (r, which, data) in new[] { (PrevRect, Prev, Icons.ChevronLeft), (NextRect, Next, Icons.ChevronRight) })
            {
                if (hover == which) FillRound(g, r, Radius, Fill);
                using (g.PushTransform(Matrix.CreateScale(16 * Scale / 24.0, 16 * Scale / 24.0) * Matrix.CreateTranslation(r.X + 6 * Scale, r.Y + 6 * Scale)))
                    g.DrawGeometry(null, new Pen(B(hover == which ? Look.Text : Text2), 2 * 24 / 16.0, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), data);
            }
            // weekends: red mixed into the text colours, so they read as Saturday and Sunday, not as an error, in both modes
            bool red = weekends == WeekendMark.Red;
            Color weekendText = WeekendText, weekendHead = WeekendHead;
            for (int c = 0; c < 7; c++)
            {
                var weekday = (DayOfWeek)(((int)firstDay + c) % 7);
                bool weekend = weekends != WeekendMark.None && weekday is DayOfWeek.Saturday or DayOfWeek.Sunday;
                if (weekend) FillRound(g, new Rect(Pad + c * Step - Gap / 2, DaysTop - 22 * Scale, Step, 6 * Step + 20 * Scale), Radius, red ? WeekendBand : WeekendBandGrey);
                Draw(g, Culture.DateTimeFormat.ShortestDayNames[(int)weekday], Ui(TextSize.Caption, 500), weekend && red ? weekendHead : Text3, new Rect(Pad + c * Step, DaysTop - 20 * Scale, Cell, 16 * Scale), TextAlignment.Center);
            }
            var (a, b) = Ends();
            for (int i = 0; i < 42; i++)
            {
                if (DayAt(i) is not DateTime day) continue;
                var r = CellRect(i);
                bool edge = range ? day == a || day == b : day == Start, inside = range && a != null && b != null && day > a && day < b;
                bool allowedDay = allowed(day), inMonth = day.Month == month.Month, today = day == Now();
                // a range: the tint runs through the gaps between its days, within the week
                if (range && a != null && b != null && (inside || edge && a != b))
                {
                    int col = i % 7;
                    double left = inside || day == b ? (col == 0 ? r.X : r.X - Gap) : r.Center.X, right = inside || day == a ? (col == 6 ? r.Right : r.Right + Gap) : r.Center.X;
                    g.FillRectangle(B(AccentTint), new Rect(left, r.Y, right - left, r.Height));
                }
                if (edge) FillRound(g, r, Radius, Accent);
                else if (hover == i || keys && day == cursor) FillRound(g, r, Radius, inside ? AccentTintHover : Fill);
                if (today && !edge) FillRound(g, r, Radius, Colors.Transparent, Accent);
                if (keys && day == cursor && edge) FillRound(g, r.Inflate(2), Radius + 2, Colors.Transparent, Look.Text);
                bool weekend = red && !inside && day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                Color text = edge ? OnAccent : !allowedDay ? Disabled : weekend ? (inMonth ? weekendText : WeekendTextDim) : !inMonth ? Text3 : Look.Text;
                Draw(g, day.Day.ToString(CultureInfo.InvariantCulture), Ui(TextSize.Body, edge || today ? 600 : 400), text, r, TextAlignment.Center, false);
            }
            if (allowed(Now())) Draw(g, T("Today"), Ui(TextSize.Label, 600), hover == Today ? AccentHi : AccentText, TodayRect.Deflate(new Thickness(4, 0)));
            if (range)
            {
                int span = a is DateTime x && b is DateTime y ? (int)(y - x).TotalDays + 1 : 0;
                string note = span > 0 ? T(span == 1 ? "{0} day" : "{0} days", span) : T(PickingEnd ? "Pick the end" : "Pick the start");
                Draw(g, note, Ui(TextSize.Label), Text2, new Rect(TodayRect.Right, FootTop, ClearRect.X - TodayRect.Right, Cell), TextAlignment.Center);
            }
            Draw(g, T("Clear"), Ui(TextSize.Label, 500), hover == Clear ? Look.Text : Text2, ClearRect.Deflate(new Thickness(4, 0)), TextAlignment.Right);
        }
    }
}

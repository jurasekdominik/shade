// Shade - the accent picker for a settings page: the accent now (a dot and its name), a Custom hex field, the curated
// accents as swatches; the field opens the colour helper (ColorGrid: curated, palette, mixer).
using System;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using static Shade.Look;

namespace Shade
{
    /// <summary>The app's accent in a settings page (DESIGN_SYSTEM.md §2): a row with its label, the accent's dot and name
    /// and a Custom hex field, the curated accents as swatches under it, a warning when the accent reads badly. The field
    /// opens the colour helper (ColorGrid). A pick sets the accent at once (Look.SetAccent) and Value; Changed follows,
    /// once the helper is closed.</summary>
    public sealed class AccentPicker : StackPanel, IRestyle
    {
        public static readonly DirectProperty<AccentPicker, string> ValueProperty =
            AvaloniaProperty.RegisterDirect<AccentPicker, string>(nameof(Value), o => o.Value, (o, v) => o.Value = v, Accents[0].name, Avalonia.Data.BindingMode.TwoWay);
        string value;
        readonly TextBlock label = Words(Build.Text("", Look.Text, TextSize.Body), text => text.Text = T("Accent")), name = Build.Text("", Look.Text, TextSize.Body),
            warning = Build.Text("", Warn, TextSize.Caption);
        readonly Field custom = new Field("#RRGGBB") { Width = 96 };
        readonly Swatches swatches;
        Popup? helper;
        string? opened;  // the Value when the helper opened: Changed only if it differs at its close

        /// <summary>A pick changed the accent (with the helper open: when it closes).</summary>
        public event Action? Changed;

        /// <summary>The accent: a curated accent's name ("Violet"; both its values) or a colour ("#3366FF"). Setting it
        /// applies it (Apply).</summary>
        public string Value { get => value; set { Apply(value); SetAndRaise(ValueProperty, ref this.value, value); Show(); } }

        /// <summary>The row's label at the left.</summary>
        public string Label { get => label.Text ?? ""; set { UnbindWords(label); label.Text = value; } }

        public AccentPicker()
        {
            Spacing = 4;
            var dot = new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(8), Background = B(Accent), VerticalAlignment = VerticalAlignment.Center };
            name.VerticalAlignment = VerticalAlignment.Center;
            var customLabel = Words(Build.Text("", Text2, TextSize.Body), text => text.Text = T("Custom")); customLabel.VerticalAlignment = VerticalAlignment.Center; customLabel.Margin = new Thickness(12, 0, 0, 0);
            var current = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { dot, name, customLabel, custom } };
            label.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(current, 1);
            Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), MinHeight = 32, Children = { label, current } });
            swatches = new Swatches(this) { Margin = new Thickness(0, 0, 0, 4) };
            Children.Add(swatches);
            warning.TextWrapping = TextWrapping.Wrap;
            Children.Add(warning);
            // the accent as it is: a curated one by its name, any other by its colour
            int at = Accents.ToList().FindIndex(x => (Current == Mode.Dark ? x.dark : x.light) == Accent.Color);
            value = at >= 0 ? Accents[at].name : HexOf(Accent);
            custom.Box.MaxLength = 7;
            custom.Edited += () =>
            {
                string t = custom.Text.Trim();
                if (!t.StartsWith("#", StringComparison.Ordinal)) t = "#" + t;
                if (Hex6(t)) Pick(t.ToUpperInvariant(), fromField: true);
                else custom.Invalid = t.Length >= 7;
            };
            custom.Box.GotFocus += (s, e) => OpenHelper();
            // a click on the field while it keeps the focus (after the helper closed) opens it again
            custom.Box.AddHandler(PointerReleasedEvent, (s, e) => OpenHelper(), Avalonia.Interactivity.RoutingStrategies.Tunnel | Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
            Show();
        }

        /// <summary>Sets the accent from a Value (at start-up, from saved settings): a curated accent's name or a colour
        /// (#RRGGBB). Returns false, and changes nothing, for anything else.</summary>
        public static bool Apply(string? value)
        {
            var curated = Accents.FirstOrDefault(a => string.Equals(a.name, value, StringComparison.OrdinalIgnoreCase));
            if (curated.name != null) { SetAccent(curated.dark, curated.light); return true; }
            if (value == null || !Hex6(value)) return false;
            SetAccent(Color.Parse(value));
            return true;
        }

        static bool Hex6(string s) => s.Length == 7 && s[0] == '#' && s.Skip(1).All(Uri.IsHexDigit);

        /// <summary>The curated accent Value names, or -1.</summary>
        int CuratedIndex => Accents.ToList().FindIndex(a => string.Equals(a.name, value, StringComparison.OrdinalIgnoreCase));

        void Pick(string v, bool fromField = false)
        {
            Apply(v);
            SetAndRaise(ValueProperty, ref value, v);
            Show(fromField);
            if (helper == null) Changed?.Invoke();
        }

        void Show(bool fromField = false)
        {
            int i = CuratedIndex;
            string hex = HexOf(Accent);
            name.Text = i >= 0 ? T(Accents[i].name) : hex;
            if (!fromField) { custom.Text = hex; custom.Invalid = false; }
            double cr = Contrast(Accent, Look.Background);
            var near = NearStatus(Accent);
            warning.Text = cr < 4.5 ? T("Low contrast ({0} : 1): text in this colour is hard to read", cr.ToString("0.0", Culture))
                : near != null ? T("Close to the {0} status colour: states may be hard to tell apart", T(near)) : "";
            warning.Foreground = B(cr < 4.5 ? DangerText : Warn);
            warning.IsVisible = warning.Text != "";
            swatches.InvalidateVisual();
        }

        /// <summary>A new mode: the curated accent's other value, its hex in the field.</summary>
        public void Restyle() => Show(custom.Box.IsFocused && custom.Invalid);

        void OpenHelper()
        {
            if (helper != null) return;
            var grid = new ColorGrid(Accent);
            opened = value;
            helper = new Popup { PlacementTarget = custom, Placement = PlacementMode.BottomEdgeAlignedLeft, IsLightDismissEnabled = true, OverlayDismissEventPassThrough = true, Child = grid, HorizontalOffset = -14, VerticalOffset = 6 };
            ((ISetLogicalParent)helper).SetParent(this);
            grid.Picked += (c, close) => { Pick(HexOf(c)); if (close && helper != null) helper.IsOpen = false; };
            grid.PickedCurated += i => { Pick(Accents[i].name); if (helper != null) helper.IsOpen = false; };
            helper.Closed += (s, e) =>
            {
                if (helper != null) ((ISetLogicalParent)helper).SetParent(null);
                helper = null;
                if (value != opened) Changed?.Invoke();
            };
            helper.IsOpen = true;
        }

        /// <summary>The curated accents in the mode's value; the current one has a ring.</summary>
        sealed class Swatches : Control
        {
            const double Dot = 24, Step = 34;
            readonly AccentPicker owner;
            int hover = -1;

            public Swatches(AccentPicker owner)
            {
                this.owner = owner;
                Width = 10 + Accents.Count * Step; Height = Dot + 12; HorizontalAlignment = HorizontalAlignment.Left;
                PointerMoved += (s, e) => { int h = At(e.GetPosition(this)); if (h != hover) { hover = h; Cursor = h >= 0 ? Build.Hand : Cursor.Default; ToolTip.SetTip(this, h >= 0 ? T(Accents[h].name) : null); InvalidateVisual(); } };
                PointerExited += (s, e) => { hover = -1; InvalidateVisual(); };
                PointerReleased += (s, e) => { int i = At(e.GetPosition(this)); if (i >= 0 && e.InitialPressMouseButton == MouseButton.Left) owner.Pick(Accents[i].name); };
            }

            int At(Point p)
            {
                int i = (int)((p.X - 5 + (Step - Dot) / 2) / Step);
                return i >= 0 && i < Accents.Count && Math.Abs(p.Y - Bounds.Height / 2) <= Dot / 2 + 4 ? i : -1;
            }

            public override void Render(DrawingContext g)
            {
                g.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
                double y = (Bounds.Height - Dot) / 2;
                int current = owner.CuratedIndex;
                for (int i = 0; i < Accents.Count; i++)
                {
                    var c = Current == Mode.Dark ? Accents[i].dark : Accents[i].light;
                    var center = new Point(5 + i * Step + Dot / 2, y + Dot / 2);
                    if (i == current || i == hover) g.DrawEllipse(null, new Pen(B(i == current ? c : Disabled), 2), center, Dot / 2 + 4, Dot / 2 + 4);
                    g.DrawEllipse(B(c), null, center, Dot / 2, Dot / 2);
                }
            }
        }
    }
}

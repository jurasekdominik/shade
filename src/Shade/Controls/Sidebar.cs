// Shade - the app shell: the sidebar navigation (brand, entries, Back/Forward) and the status bar.
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using static Shade.Look;

namespace Shade
{
    /// <summary>A sidebar entry: a top-level row with a Lucide icon (Icons.*), or a child row (no icon). An entry with
    /// children opens on its first child (Route null) and shows them while one of them is current.</summary>
    public sealed class NavEntry
    {
        public string Label { get; }
        public string? Route { get; }
        public Geometry? Icon { get; }
        public IReadOnlyList<NavEntry> Children { get; }
        public NavEntry(string label, string? route, Geometry? icon = null, params NavEntry[] children) { Label = label; Route = route; Icon = icon; Children = children; }
    }

    /// <summary>Shared desktop navigation (DESIGN_SYSTEM.md §10). The marker occupies the row's full height at x=0.</summary>
    public sealed class Sidebar : Border
    {
        readonly StackPanel items = new StackPanel { Spacing = 0 };
        readonly StackPanel brandText = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        public NavigationButtons History { get; } = new NavigationButtons();
        readonly IReadOnlyList<NavEntry> entries;
        readonly Button brand;
        readonly Panel markHost = new Panel { Width = MarkSize, Height = MarkSize, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(Rest, 4, 0, 0) };
        readonly DispatcherTimer roll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
        readonly System.Diagnostics.Stopwatch clock = new System.Diagnostics.Stopwatch();
        readonly Control mark;
        double fromW, toW;
        string? route;
        bool collapsed;
        public static readonly StyledProperty<double> ExpandedWidthProperty = AvaloniaProperty.Register<Sidebar, double>(nameof(ExpandedWidth), M(180), validate: value => double.IsFinite(value) && value >= M(150));
        /// <summary>Expanded navigation width in device-independent pixels; preserved through collapse and animation. Branding wraps within it.</summary>
        public double ExpandedWidth { get => GetValue(ExpandedWidthProperty); set => SetValue(ExpandedWidthProperty, value); }
        public event Action<string>? Navigate;
        public event Action? CollapsedChanged;
        public bool Collapsed { get => collapsed; set { collapsed = value; History.Collapse(value); Rebuild(); Move(value); } }

        // The name sits at the left, the mark at the right edge. The mark rolls one turn along the bar's edge, the two moving
        // together on one ease-in-out curve; the name fades while the mark rolls past it.
        double Wide => ExpandedWidth;
        static double Narrow => M(54);
        static double MarkSize => M(38);
        static double Rest => (Narrow - MarkSize) / 2;
        double Far => Wide - M(16) - MarkSize - Rest;
        static readonly TimeSpan Slide = TimeSpan.FromMilliseconds(420);
        /// <summary>The roll's progress: speeds up gently and settles to a stop (ease-in-out sine).</summary>
        public static double Ease(double t) => (1 - Math.Cos(Math.PI * t)) / 2;

        /// <param name="mark">The app's logo, 38 px; null = the app's mark (Look.Mark).</param>
        public Sidebar(string name, string tagline, IReadOnlyList<NavEntry> entries, Control? mark = null)
        {
            SetCurrentValue(ExpandedWidthProperty, M(180));
            this.entries = entries;
            this.mark = mark ?? new Mark();
            this.mark.Width = this.mark.Height = MarkSize;
            Background = B(Raised); BorderBrush = B(Look.Separator); BorderThickness = new Thickness(0, 0, 1, 0); Width = Wide; ClipToBounds = true;
            var title = Build.Text(name, Look.Text, TextSize.Title, 600); title.TextWrapping = TextWrapping.Wrap; title.TextTrimming = TextTrimming.None;
            brandText.Children.Add(title);
            if (!string.IsNullOrEmpty(tagline)) { var sub = Build.Text(tagline, Text3, TextSize.Caption); sub.TextWrapping = TextWrapping.Wrap; sub.TextTrimming = TextTrimming.None; brandText.Children.Add(sub); }
            brandText.VerticalAlignment = VerticalAlignment.Top; brandText.Margin = new Thickness(M(16), M(2), 0, 0);
            brandText.HorizontalAlignment = HorizontalAlignment.Left;
            markHost.Children.Add(this.mark);
            var head = new Panel { MinHeight = M(50), Background = Brushes.Transparent, Children = { brandText, markHost } };
            brand = new Button { Padding = new Thickness(0), Margin = new Thickness(0, M(14), 0, M(18)), Background = Brushes.Transparent, Cursor = Build.Hand, HorizontalAlignment = HorizontalAlignment.Stretch };
            brand.Template = new FuncControlTemplate<Button>((b, n) => head);
            Words(brand, word => Avalonia.Automation.AutomationProperties.SetName(word, T("Collapse or expand navigation")), "name");
            brand.Click += (s, e) => { Collapsed = !Collapsed; CollapsedChanged?.Invoke(); };
            roll.Tick += (s, e) => Roll();
            void Branding() => brandText.Width = Wide - M(32) - (this.mark.IsVisible ? MarkSize + M(8) : 0);
            Branding();
            PropertyChanged += (_, change) => { if (change.Property == ExpandedWidthProperty) { Branding(); Move(collapsed); } };
            this.mark.PropertyChanged += (_, change) => { if (change.Property == IsVisibleProperty) Branding(); };
            DetachedFromVisualTree += (_, _) => roll.Stop();
            ToolTip.SetTip(title, name);
            Place(Wide);
            var root = new DockPanel(); DockPanel.SetDock(brand, Dock.Top);
            root.Children.Add(brand);
            var footer = new Border { Child = History, Padding = new Thickness(8, 12) };
            DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
            root.Children.Add(new ScrollViewer { Content = items, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
            Child = root;
            Rebuild();
        }

        void Move(bool narrow)
        {
            toW = narrow ? Narrow : Wide;
            if (VisualRoot == null || ReducedMotion) { roll.Stop(); Place(toW); return; }  // before the window shows (the saved state): no motion
            fromW = Width; clock.Restart(); roll.Start();  // from where it is, so a second click mid-way turns it back smoothly
        }

        void Roll()
        {
            double t = Math.Min(1, clock.Elapsed.TotalMilliseconds / Slide.TotalMilliseconds);
            Place(fromW + (toW - fromW) * Ease(t));
            if (t >= 1) roll.Stop();
        }

        /// <summary>The bar's width; the mark keeps its place against the bar's right edge, centred when narrow, and its turn
        /// follows its place, so it rolls without sliding and stands upright at both ends.</summary>
        void Place(double width)
        {
            Width = width;
            double x = Far * (width - Narrow) / (Wide - Narrow);
            markHost.RenderTransform = new TranslateTransform(x, 0);
            mark.RenderTransform = new RotateTransform(x / Far * 360 - 360);
            brandText.Opacity = Math.Clamp(((width - Narrow) / (Wide - Narrow) - 0.5) * 2, 0, 1);
        }

        public void Select(string location) { route = location; Rebuild(); }
        void Rebuild()
        {
            Words(brand, word => ToolTip.SetTip(word, T(collapsed ? "Expand navigation" : "Collapse navigation")), "tip");
            items.Children.Clear();
            foreach (var e in entries)
            {
                if (e.Children.Count == 0) Add(e.Label, e.Route, e.Icon, false, route == e.Route);
                else
                {
                    bool active = route == e.Route || Contains(e, route);
                    Add(e.Label, e.Route ?? e.Children[0].Route, e.Icon, false, collapsed && active, active, opens: true);
                    if (active && !collapsed)
                        foreach (var c in e.Children) Add(c.Label, c.Route, null, true, route == c.Route);
                }
                items.Children.Add(new Border { Height = 4 });  // the same space after every top-level entry, with or without children
            }
        }
        static bool Contains(NavEntry e, string? route) { foreach (var c in e.Children) if (c.Route == route) return true; return false; }
        /// <param name="opens">An entry with sub-entries: clicked in the narrow bar, it opens the bar too, so they show.</param>
        void Add(string label, string? target, Geometry? icon, bool child, bool selected, bool parentActive = false, bool opens = false)
        {
            var item = new NavItem(label, icon, child) { Active = selected, ParentActive = parentActive };
            item.Click += (s, e) => { if (opens && collapsed) { Collapsed = false; CollapsedChanged?.Invoke(); } if (target != null) Navigate?.Invoke(target); };
            items.Children.Add(item);
            item.ShowText = !collapsed;
        }
    }

    public sealed class NavItem : Button, IRestyle
    {
        readonly Border surface, marker, focus;
        readonly TextBlock label;
        readonly Control? glyph;
        bool active, parentActive, hot;
        public bool Active { get => active; set { active = value; Restyle(); } }
        public bool ParentActive { get => parentActive; set { parentActive = value; Restyle(); } }
        public bool ShowText { get => label.IsVisible; set => label.IsVisible = value; }
        /// <param name="icon">A 24 px Lucide icon (Icons.*); null for a child row.</param>
        public NavItem(string text, Geometry? icon, bool child)
        {
            Height = M(child ? 26 : 28); MinHeight = 0; Padding = new Thickness(0); HorizontalAlignment = HorizontalAlignment.Stretch;
            label = Build.Text(text, Text2, TextSize.Body); label.Margin = new Thickness(M(child ? 34 : 42), 0, M(10), 0);
            var grid = new Panel(); grid.Children.Add(label);
            if (icon != null)
            {
                glyph = Build.Icon24(icon, M(18), 1.8); glyph.HorizontalAlignment = HorizontalAlignment.Left; glyph.Margin = new Thickness(M(16), 0, 0, 0); grid.Children.Add(glyph);
            }
            marker = new Border { Width = 3, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Stretch, IsHitTestVisible = false };
            grid.Children.Add(marker);
            focus = new Border { BorderBrush = B(Accent), BorderThickness = new Thickness(1), IsHitTestVisible = false };
            grid.Children.Add(focus);
            surface = new Border { Child = grid, CornerRadius = new CornerRadius(0) };
            Template = new FuncControlTemplate<Button>((button, names) => surface);
            ToolTip.SetTip(this, text);
            Avalonia.Automation.AutomationProperties.SetName(this, text);
            PointerEntered += (s, e) => { hot = true; Restyle(); };
            PointerExited += (s, e) => { hot = false; Restyle(); };
            GotFocus += (s, e) => Restyle(); LostFocus += (s, e) => Restyle();
            Restyle();
        }
        public void Restyle()
        {
            if (surface == null) return;
            surface.Background = B(active ? AccentTint : hot ? Fill : Raised);
            focus.BorderBrush = B(Accent); focus.IsVisible = IsKeyboardFocusWithin;
            label.Foreground = B(active || parentActive ? Look.Text : Text2);
            marker.Background = B(Accent); marker.IsVisible = active;
            if (glyph is Avalonia.Controls.Shapes.Path path) path.Stroke = B(active || parentActive ? Look.Text : Text2);
        }
    }

    /// <summary>The window's bottom bar, 28 px (DESIGN_SYSTEM.md §10): left, a dot and the latest message (a click: OpenLeft);
    /// right, a live count (a click: OpenRight).</summary>
    public sealed class StatusBar : Border
    {
        readonly Avalonia.Controls.Shapes.Ellipse dot = new Avalonia.Controls.Shapes.Ellipse { Width = 8, Height = 8, VerticalAlignment = VerticalAlignment.Center };
        readonly TextBlock message = Build.Text("", Text3, TextSize.Label);
        readonly TextBlock right = Build.Text("", Text2, TextSize.Label);
        readonly Avalonia.Controls.Shapes.Ellipse live = new Avalonia.Controls.Shapes.Ellipse { Width = 8, Height = 8, VerticalAlignment = VerticalAlignment.Center };
        readonly Progress bar = new Progress { Width = 56, VerticalAlignment = VerticalAlignment.Center };
        readonly TextBlock work = Build.Text("", Text2, TextSize.Label);
        readonly StackPanel busy = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(M(16), 0, 0, 0), IsVisible = false };
        public event Action? OpenLeft, OpenRight;

        public StatusBar()
        {
            Height = ControlHeight; Background = B(Raised); BorderBrush = B(Look.Separator); BorderThickness = new Thickness(0, 1, 0, 0); Padding = new Thickness(12, 0);
            message.TextTrimming = TextTrimming.CharacterEllipsis; message.VerticalAlignment = right.VerticalAlignment = VerticalAlignment.Center;
            var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Cursor = Build.Hand, Background = Brushes.Transparent };
            left.Children.Add(dot); left.Children.Add(message);
            left.PointerReleased += (s, e) => { if (e.InitialPressMouseButton == MouseButton.Left) OpenLeft?.Invoke(); };
            var counts = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Cursor = Build.Hand, Background = Brushes.Transparent, Margin = new Thickness(M(16), 0, 0, 0) };
            counts.Children.Add(live); counts.Children.Add(right);
            counts.PointerReleased += (s, e) => { if (e.InitialPressMouseButton == MouseButton.Left) OpenRight?.Invoke(); };
            work.VerticalAlignment = VerticalAlignment.Center;
            busy.Children.Add(bar); busy.Children.Add(work);
            var d = new DockPanel(); DockPanel.SetDock(counts, Dock.Right); DockPanel.SetDock(busy, Dock.Right);
            d.Children.Add(counts); d.Children.Add(busy); d.Children.Add(left);
            Child = d;
            Words(this, status => { if (status.defaultMessage) { status.Show(T("No problems"), Success); status.defaultMessage = true; } }, "defaultMessage");
        }

        /// <summary>The left message; dot: Success, Warn, Danger or Accent (work in progress). Text3 text unless it is a problem or a notice.</summary>
        bool defaultMessage = true;
        public void Show(string text, Tone dotColor, bool loud = false)
        {
            defaultMessage = false;
            dot.Fill = B(dotColor); message.Text = text; message.Foreground = B(loud ? Look.Text : Text3); ToolTip.SetTip(message, text);
        }

        /// <summary>Work under way, right of the message: a small bar and what it is ("Uploading 3 files · 64 %"); share null =
        /// unknown length, countdown: the time to the next event. text null hides it.</summary>
        public void Busy(string text, double? share = null, bool countdown = false)
        {
            busy.IsVisible = text != null; work.Text = text ?? ""; bar.Value = share; bar.Countdown = countdown;
        }

        /// <summary>The right count ("2 people online"); on: a green dot, else faint.</summary>
        public void Count(string text, bool on) { right.Text = text; live.Fill = B(on ? Success : Text3); }
    }

    /// <summary>The window layout: the sidebar left, the page right, the status bar under both.</summary>
    public static class Shell
    {
        public static Control Layout(Sidebar sidebar, Control page, StatusBar status)
        {
            var shell = new DockPanel(); DockPanel.SetDock(sidebar, Dock.Left); shell.Children.Add(sidebar); shell.Children.Add(page);
            var outer = new DockPanel(); DockPanel.SetDock(status, Dock.Bottom); outer.Children.Add(status); outer.Children.Add(shell);
            return outer;
        }
    }
}

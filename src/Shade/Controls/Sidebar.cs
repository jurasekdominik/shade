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
        public string Label, Route;
        public Geometry Icon;
        public IReadOnlyList<NavEntry> Children = Array.Empty<NavEntry>();
        public NavEntry(string label, string route, Geometry icon = null, params NavEntry[] children) { Label = label; Route = route; Icon = icon; Children = children; }
    }

    /// <summary>Shared desktop navigation (DESIGN_SYSTEM.md §10). The marker occupies the row's full height at x=0.</summary>
    public sealed class Sidebar : Border
    {
        readonly StackPanel items = new StackPanel { Spacing = 0 };
        readonly StackPanel brandText = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        public readonly NavigationButtons History = new NavigationButtons();
        readonly IReadOnlyList<NavEntry> entries;
        readonly Button brand;
        readonly Panel markHost = new Panel { Width = MarkSize, Height = MarkSize, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(Rest, 4, 0, 0) };
        readonly DispatcherTimer roll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
        readonly System.Diagnostics.Stopwatch clock = new System.Diagnostics.Stopwatch();
        readonly Control mark;
        double fromW, toW;
        string route;
        bool collapsed;
        public event Action<string> Navigate;
        public event Action CollapsedChanged;
        public bool Collapsed { get => collapsed; set { collapsed = value; History.Collapse(value); Build(); Move(value); } }

        // The name sits at the left, the mark at the right edge. The mark rolls one turn along the bar's edge, the two moving
        // together on one ease-in-out curve; the name fades while the mark rolls past it.
        const double Wide = 180, Narrow = 54, MarkSize = 38, Rest = (Narrow - MarkSize) / 2, Far = Wide - 16 - MarkSize - Rest;
        static readonly TimeSpan Slide = TimeSpan.FromMilliseconds(420);
        /// <summary>The roll's progress: speeds up gently and settles to a stop (ease-in-out sine).</summary>
        public static double Ease(double t) => (1 - Math.Cos(Math.PI * t)) / 2;

        /// <param name="mark">The app's logo, 38 px; null = the node mark (Look.Mark).</param>
        public Sidebar(string name, string tagline, IReadOnlyList<NavEntry> entries, Control mark = null)
        {
            this.entries = entries;
            this.mark = mark ?? new Mark();
            this.mark.Width = this.mark.Height = MarkSize;
            Background = B(Surface); BorderBrush = B(Look.Border); BorderThickness = new Thickness(0, 0, 1, 0); Width = Wide; ClipToBounds = true;
            brandText.Children.Add(Kit.Text(name, Look.Text, 17, 600));
            if (!string.IsNullOrEmpty(tagline)) brandText.Children.Add(Kit.Text(tagline, Faint, 8));
            brandText.VerticalAlignment = VerticalAlignment.Top; brandText.Margin = new Thickness(16, 2, 0, 0);
            markHost.Children.Add(this.mark);
            var head = new Panel { Height = 50, Background = Brushes.Transparent, Children = { brandText, markHost } };
            brand = new Button { Padding = new Thickness(0), Margin = new Thickness(0, 14, 0, 18), Background = Brushes.Transparent, Cursor = Kit.Hand, HorizontalAlignment = HorizontalAlignment.Stretch };
            brand.Template = new FuncControlTemplate<Button>((b, n) => head);
            Avalonia.Automation.AutomationProperties.SetName(brand, "Collapse or expand navigation");
            brand.Click += (s, e) => { Collapsed = !Collapsed; CollapsedChanged?.Invoke(); };
            roll.Tick += (s, e) => Roll();
            Place(Wide);
            var root = new DockPanel(); DockPanel.SetDock(brand, Dock.Top);
            root.Children.Add(brand);
            var footer = new Border { Child = History, Padding = new Thickness(8, 12) };
            DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
            root.Children.Add(new ScrollViewer { Content = items, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
            Child = root;
            Build();
        }

        void Move(bool narrow)
        {
            toW = narrow ? Narrow : Wide;
            if (VisualRoot == null) { roll.Stop(); Place(toW); return; }  // before the window shows (the saved state): no motion
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
            brandText.Opacity = Math.Clamp((x - 60) / 50, 0, 1);  // the name shows only where the mark has passed it
        }

        public void Select(string location) { route = location; Build(); }
        void Build()
        {
            ToolTip.SetTip(brand, collapsed ? "Expand navigation" : "Collapse navigation");
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
        static bool Contains(NavEntry e, string route) { foreach (var c in e.Children) if (c.Route == route) return true; return false; }
        /// <param name="opens">An entry with sub-entries: clicked in the narrow bar, it opens the bar too, so they show.</param>
        void Add(string label, string target, Geometry icon, bool child, bool selected, bool parentActive = false, bool opens = false)
        {
            var item = new NavItem(label, icon, child) { Active = selected, ParentActive = parentActive };
            item.Click += (s, e) => { if (opens && collapsed) { Collapsed = false; CollapsedChanged?.Invoke(); } Navigate?.Invoke(target); };
            items.Children.Add(item);
            item.ShowText = !collapsed;
        }
    }

    public sealed class NavItem : Button
    {
        readonly Border surface, marker, focus;
        readonly TextBlock label;
        readonly Control glyph;
        bool active, parentActive, hot;
        public bool Active { get => active; set { active = value; Restyle(); } }
        public bool ParentActive { get => parentActive; set { parentActive = value; Restyle(); } }
        public bool ShowText { set { label.IsVisible = value; } }
        /// <param name="icon">A 24 px Lucide icon (Icons.*); null for a child row.</param>
        public NavItem(string text, Geometry icon, bool child)
        {
            Height = child ? 26 : 28; MinHeight = 0; Padding = new Thickness(0); HorizontalAlignment = HorizontalAlignment.Stretch;
            label = Kit.Text(text, Second, 9.75); label.Margin = new Thickness(child ? 34 : 42, 0, 10, 0);
            var grid = new Panel(); grid.Children.Add(label);
            if (icon != null)
            {
                glyph = Kit.Icon24(icon, 18, 1.8); glyph.HorizontalAlignment = HorizontalAlignment.Left; glyph.Margin = new Thickness(16, 0, 0, 0); grid.Children.Add(glyph);
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
            surface.Background = B(active ? Soft : hot ? Surface2 : Surface);
            focus.BorderBrush = B(Accent); focus.IsVisible = IsKeyboardFocusWithin;
            label.Foreground = B(active || parentActive ? Look.Text : Second);
            marker.Background = B(Accent); marker.IsVisible = active;
            if (glyph is Avalonia.Controls.Shapes.Path path) path.Stroke = B(active || parentActive ? Look.Text : Second);
        }
    }

    /// <summary>The window's bottom bar, 28 px (DESIGN_SYSTEM.md §10): left, a dot and the latest message (a click: OpenLeft);
    /// right, a live count (a click: OpenRight).</summary>
    public sealed class StatusBar : Border
    {
        readonly Avalonia.Controls.Shapes.Ellipse dot = new Avalonia.Controls.Shapes.Ellipse { Width = 8, Height = 8, VerticalAlignment = VerticalAlignment.Center };
        readonly TextBlock message = Kit.Text("", Faint, 9);
        readonly TextBlock right = Kit.Text("", Second, 9);
        readonly Avalonia.Controls.Shapes.Ellipse live = new Avalonia.Controls.Shapes.Ellipse { Width = 8, Height = 8, VerticalAlignment = VerticalAlignment.Center };
        public event Action OpenLeft, OpenRight;

        public StatusBar()
        {
            Height = 28; Background = B(Surface); BorderBrush = B(Look.Border); BorderThickness = new Thickness(0, 1, 0, 0); Padding = new Thickness(12, 0);
            message.TextTrimming = TextTrimming.CharacterEllipsis; message.VerticalAlignment = right.VerticalAlignment = VerticalAlignment.Center;
            var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Cursor = Kit.Hand, Background = Brushes.Transparent };
            left.Children.Add(dot); left.Children.Add(message);
            left.PointerReleased += (s, e) => { if (e.InitialPressMouseButton == MouseButton.Left) OpenLeft?.Invoke(); };
            var counts = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Cursor = Kit.Hand, Background = Brushes.Transparent, Margin = new Thickness(16, 0, 0, 0) };
            counts.Children.Add(live); counts.Children.Add(right);
            counts.PointerReleased += (s, e) => { if (e.InitialPressMouseButton == MouseButton.Left) OpenRight?.Invoke(); };
            var d = new DockPanel(); DockPanel.SetDock(counts, Dock.Right); d.Children.Add(counts); d.Children.Add(left);
            Child = d;
            Show("No problems", Success);
        }

        /// <summary>The left message; dot: Success, Warn, Danger or Accent (work in progress). Faint text unless it is a problem or a notice.</summary>
        public void Show(string text, Tone dotColor, bool loud = false)
        {
            dot.Fill = B(dotColor); message.Text = text; message.Foreground = B(loud ? Look.Text : Faint); ToolTip.SetTip(message, text);
        }

        /// <summary>The right count ("2 people online"); on: a green dot, else faint.</summary>
        public void Count(string text, bool on) { right.Text = text; live.Fill = B(on ? Success : Faint); }
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

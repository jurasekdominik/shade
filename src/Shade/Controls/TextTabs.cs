// Shade - shared control: text tabs, the active one bright with an accent underline.
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using Avalonia.Animation;
using Avalonia.Controls.Primitives;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media.Transformation;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia;
using static Shade.Look;

namespace Shade
{
    /// <summary>R E A D   W R I T E - the active tab bright with an accent underline.</summary>
    public sealed class TextTabs : Panel, IRestyle
    {
        protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new TabsPeer(this);
        sealed class Segment(TextTabs owner, int index) : StackPanel
        {
            protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new SegmentPeer(this, owner, index);
        }
        sealed class TabsPeer(TextTabs owner) : Avalonia.Automation.Peers.ControlAutomationPeer(owner), Avalonia.Automation.Provider.ISelectionProvider
        {
            protected override Avalonia.Automation.Peers.AutomationControlType GetAutomationControlTypeCore() => Avalonia.Automation.Peers.AutomationControlType.Tab;
            public bool CanSelectMultiple => false;
            public bool IsSelectionRequired => owner.tabs.Count > 0;
            public IReadOnlyList<Avalonia.Automation.Peers.AutomationPeer> GetSelection() => owner.tabs.Count == 0 ? Array.Empty<Avalonia.Automation.Peers.AutomationPeer>() : new[] { CreatePeerForElement(owner.tabs[owner.active].item)! };
        }
        sealed class SegmentPeer : Avalonia.Automation.Peers.ControlAutomationPeer, Avalonia.Automation.Provider.ISelectionItemProvider
        {
            readonly TextTabs owner;
            readonly int index;
            public SegmentPeer(Segment segment, TextTabs owner, int index) : base(segment)
            {
                this.owner = owner; this.index = index;
                owner.PropertyChanged += (_, e) => { if (e.Property == ActiveProperty) RaisePropertyChangedEvent(Avalonia.Automation.SelectionItemPatternIdentifiers.IsSelectedProperty, (int)e.OldValue! == index, (int)e.NewValue! == index); };
            }
            protected override Avalonia.Automation.Peers.AutomationControlType GetAutomationControlTypeCore() => Avalonia.Automation.Peers.AutomationControlType.TabItem;
            public bool IsSelected => owner.active == index;
            public Avalonia.Automation.Provider.ISelectionProvider SelectionContainer => (Avalonia.Automation.Provider.ISelectionProvider)CreatePeerForElement(owner)!;
            public void Select() { EnsureEnabled(); owner.SetActive(index); }
            public void AddToSelection() => Select();
            public void RemoveFromSelection() { if (IsSelected) throw new InvalidOperationException("A tab selection is required."); }
        }
        readonly List<(StackPanel item, TextBlock text)> tabs = new List<(StackPanel, TextBlock)>();
        readonly StackPanel row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = M(24) };
        /// <summary>One underline for all tabs: it slides (and takes the new tab's width) to the one picked.</summary>
        readonly Border line = new Border { Height = 2, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, Width = 0 };
        readonly string[] names;
        int active;
        bool placed;
        public event Action<int>? Changed;
        public static readonly DirectProperty<TextTabs, int> ActiveProperty =
            AvaloniaProperty.RegisterDirect<TextTabs, int>(nameof(Active), o => o.Active, (o, v) => o.Active = v, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

        public TextTabs(params string[] names)
        {
            this.names = names;
            for (int i = 0; i < names.Length; i++)
            {
                int k = i;
                var t = Build.Text(Spaced(names[i]), Text3, TextSize.Label, 500);
                var item = new Segment(this, k) { Cursor = Build.Hand, Background = Brushes.Transparent };
                item.Children.Add(t); item.Children.Add(new Border { Height = M(6) });  // room for the line under the text
                Activate(item, () => SetActive(k));
                Avalonia.Automation.AutomationProperties.SetName(item, names[i]);
                item.KeyDown += (_, e) =>
                {
                    int step = (e.Key == Key.Right ? 1 : e.Key == Key.Left ? -1 : 0) * (FlowDirection == FlowDirection.RightToLeft ? -1 : 1);
                    int next = e.Key == Key.Home ? Shown(0, 1) : e.Key == Key.End ? Shown(tabs.Count - 1, -1) : Shown(k + step, step);
                    if (next < 0) next = k;
                    if (step != 0 || e.Key is Key.Home or Key.End) { SetActive(next); tabs[next].item.Focus(NavigationMethod.Directional); e.Handled = true; }
                };
                tabs.Add((item, t));
                row.Children.Add(item);
            }
            line.RenderTransform = TransformOperations.Parse("translateX(0px)");
            Children.Add(row); Children.Add(line);
            row.LayoutUpdated += (s, e) => Place();  // a renamed tab (a count) changes the widths
            Restyle();
        }

        int Shown(int from, int step) { for (int i = from; i >= 0 && i < tabs.Count && step != 0; i += step) if (tabs[i].item.IsVisible) return i; return -1; }
        internal void SetShown(int i, bool shown) => tabs[i].item.IsVisible = shown;

        static string Spaced(string s) => s;  // sentence-case labels without decorative letter spacing

        /// <summary>The tab shown; set by the code, without Changed (SetActive raises it).</summary>
        public int Active { get => active; set { if (value < 0 || value >= tabs.Count) throw new ArgumentOutOfRangeException(nameof(value)); SetAndRaise(ActiveProperty, ref active, value); Restyle(); } }
        public void SetActive(int i) { if (i == active) return; Active = i; Changed?.Invoke(i); }
        public void SetName(int i, string name) { if (names[i] == name) return; names[i] = name; tabs[i].text.Text = Spaced(name); Avalonia.Automation.AutomationProperties.SetName(tabs[i].item, name); }

        /// <summary>The line under the active tab's text: slides there (ease-out, 220 ms) once the tabs were first laid out.</summary>
        void Place()
        {
            if (active < 0 || active >= tabs.Count) return;
            var b = tabs[active].item.Bounds;
            var t = tabs[active].text.Bounds;
            if (t.Width <= 0) return;
            double x = b.X, w = t.Width;
            if (Math.Abs(line.Width - w) < 0.1 && line.Tag is double at && Math.Abs(at - x) < 0.1) return;
            line.Tag = x;
            line.Width = w;
            line.RenderTransform = TransformOperations.Parse(FormattableString.Invariant($"translateX({x}px)"));
            if (!placed)
            {
                placed = true;  // from now on it slides
                var ease = new Avalonia.Animation.Easings.CubicEaseOut();
                line.Transitions = ReducedMotion ? null : new Transitions
                {
                    new TransformOperationsTransition { Property = RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(220), Easing = ease },
                    new DoubleTransition { Property = WidthProperty, Duration = TimeSpan.FromMilliseconds(220), Easing = ease },
                    new BrushTransition { Property = Avalonia.Controls.Border.BackgroundProperty, Duration = TimeSpan.FromMilliseconds(220) },
                };
            }
        }

        public void Restyle()
        {
            for (int i = 0; i < tabs.Count; i++) { tabs[i].text.Foreground = B(i == active ? Look.Text : Text3); tabs[i].item.IsTabStop = i == active; }
            line.Background = B(Accent);
            Place();
        }
    }
}

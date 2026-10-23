// Shade - shared control: Btn, the button in its kinds (primary, secondary, ghost, danger).
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using System.Diagnostics;
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
    /// <summary>Normal = Secondary (DESIGN_SYSTEM.md §6); Soft is kept as a name for Secondary; Ghost has no fill.</summary>
    public enum BtnKind { Normal, Primary, Soft, Del, Ghost }

    /// <summary>A button in its states: normal, primary, soft (accent tint), delete, a colour tint (Live),
    /// dimmed (greyed but still clickable, to say why it can't be used).</summary>
    public sealed class Btn : Border, IRestyle
    {
        /// <summary>Text starting with this shows the drawn undo arrow (alone, or before a word: "\u21b6 Undo").</summary>
        public const string UndoText = "\u21b6";
        /// <summary>Text that shows the drawn list icon; "+" shows a drawn plus.</summary>
        public const string ListText = "\u2630";
        readonly TextBlock label;
        readonly Avalonia.Controls.Shapes.Path? undoIcon;
        readonly Border fill = new Border { HorizontalAlignment = HorizontalAlignment.Left, Width = 0, CornerRadius = new CornerRadius(7) };  // hold to confirm: the fill so far
        BtnKind kind;
        Tone? live;
        bool hot, down, dimmed, keyboardFocus;
        public event Action? Click;

        /// <summary>Press and hold this long (ms) to fire Click (Unlock): a fill runs across while held, letting go early
        /// cancels. 0 = a normal click.</summary>
        public int HoldMs;
        public bool KeepPointerFocus;
        DispatcherTimer? holdTimer;
        readonly Stopwatch holdClock = new Stopwatch();
        IPointer? pressedPointer;
        bool holdPress;
        Window? holdWindow;
        /// <summary>Two-step confirm (Write): the first click turns the button into this text, a second click within 4 s fires
        /// Click; otherwise it goes back. null = a normal click.</summary>
        public string? ConfirmText;
        string? restText;
        DispatcherTimer? armTimer;
        public bool Armed => restText != null;

        /// <summary>icon: a Lucide shape before the label, 6 px apart (DESIGN_SYSTEM.md §6).</summary>
        public Btn(string text, BtnKind kind = BtnKind.Normal, double pt = 0, Geometry? icon = null)
        {
            this.kind = kind;
            Focusable = true; FocusAdorner = null;
            Avalonia.Automation.AutomationProperties.SetName(this, text);
            GotFocus += (_, e) => { keyboardFocus = e.NavigationMethod != NavigationMethod.Pointer; Restyle(); };
            LostFocus += (_, _) => { keyboardFocus = false; Restyle(); };
            KeyDown += (_, e) =>
            {
                if (IsEnabled && HoldMs == 0 && (e.Key == Key.Enter || e.Key == Key.Space)) { PerformClick(); e.Handled = true; }
            };
            label = Kit.Text(text, Second, 9.75, 500);  // Body; a 36 px button takes Section (OnPropertyChanged)
            label.HorizontalAlignment = HorizontalAlignment.Center;
            var p = new Panel();
            p.Children.Add(fill);
            if (text == "+" || text == ListText)  // drawn, not a font glyph: centred at any size
            {
                undoIcon = Kit.Icon24(text == "+" ? Icons.Plus : Icons.List, 16, 2);
                label.Text = "";
                p.Children.Add(undoIcon);
            }
            else if (text.StartsWith(UndoText, StringComparison.Ordinal))  // the drawn undo arrow (16 px), then the word: the one undo look
            {
                undoIcon = Kit.Icon24(Field.UndoGeometry, 16, 2);
                label.Text = text.Substring(1).Trim();
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                row.Children.Add(undoIcon);
                if (label.Text != "") row.Children.Add(label);
                p.Children.Add(row);
            }
            else if (icon != null)
            {
                undoIcon = Kit.Icon24(icon, 16, 2);
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                row.Children.Add(undoIcon); if (text != "") row.Children.Add(label);  // icon only: centred, 28 px square
                p.Children.Add(row);
            }
            else p.Children.Add(label);
            Child = p;
            ClipToBounds = true;
            CornerRadius = new CornerRadius(4);
            BorderThickness = new Thickness(1);
            Padding = new Thickness(12, 0);
            MinWidth = label.Text == "" && undoIcon != null ? 28 : 72;  // an explicit Width still wins
            if (MinWidth == 28) Padding = new Thickness(0);
            Height = 28;
            Cursor = Kit.Hand;
            RenderTransform = TransformOperations.Parse("scale(1)");
            Transitions = new Transitions { new TransformOperationsTransition { Property = RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(90) } };
            PointerEntered += (s, e) => { hot = true; Restyle(); };
            PointerExited += (s, e) =>
            {
                hot = false;
                // Capture can invalidate pointer-over while it is still inside the button (Avalonia 12).
                if (!new Rect(Bounds.Size).Contains(e.GetPosition(this))) CancelPress();
                else Restyle();
            };
            PointerCaptureLost += (s, e) => { if (pressedPointer?.Captured != this) CancelPress(); };
            PointerMoved += (s, e) => { if (pressedPointer == e.Pointer && !new Rect(Bounds.Size).Contains(e.GetPosition(this))) CancelPress(); };
            PointerPressed += (s, e) =>
            {
                if (!IsEffectivelyEnabled || pressedPointer != null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
                keyboardFocus = false;
                if (!KeepPointerFocus) Focus(NavigationMethod.Pointer);
                holdPress = HoldMs > 0;
                pressedPointer = e.Pointer;
                e.Pointer.Capture(this);
                down = true; Restyle();
                if (!holdPress) RenderTransform = TransformOperations.Parse("scale(0.97)");
                if (holdPress) StartHold();
                e.Handled = true;
            };
            PointerReleased += (s, e) =>
            {
                if (pressedPointer != e.Pointer || e.InitialPressMouseButton != MouseButton.Left) return;
                bool was = down;
                bool wasHold = holdPress;  // Unlock may already have changed HoldMs to 0 while still pressed
                CancelPress();
                e.Handled = true;  // a button inside a clickable row must not click the row too
                if (was && !wasHold && IsEffectivelyEnabled && new Rect(Bounds.Size).Contains(e.GetPosition(this))) PerformClick();
            };
            PropertyChanged += (s, e) =>
            {
                if (e.Property == IsEnabledProperty || e.Property == IsVisibleProperty)
                {
                    if (!IsEffectivelyEnabled || !IsEffectivelyVisible) { CancelPress(); Disarm(); }
                    Restyle();
                }
            };
            AttachedToVisualTree += (s, e) =>
            {
                holdWindow = TopLevel.GetTopLevel(this) as Window;
                if (holdWindow != null) holdWindow.Deactivated += CancelInteraction;
            };
            DetachedFromVisualTree += (s, e) =>
            {
                if (holdWindow != null) holdWindow.Deactivated -= CancelInteraction;
                holdWindow = null;
                CancelInteraction(this, EventArgs.Empty);
            };
            Restyle();
        }

        /// <summary>A click: the first one only arms a two-step button.</summary>
        public void PerformClick()
        {
            if (!IsEffectivelyEnabled || HoldMs > 0) return;
            if (ConfirmText != null && restText == null)
            {
                restText = Text; Text = ConfirmText;
                if (armTimer == null) { armTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) }; armTimer.Tick += (s, a) => Disarm(); }
                armTimer.Stop(); armTimer.Start();
                Restyle();
                return;
            }
            Disarm();
            Click?.Invoke();
        }

        public void Disarm() { if (restText == null) return; Text = restText; restText = null; armTimer?.Stop(); Restyle(); }

        void StartHold()
        {
            holdClock.Restart();
            fill.Margin = new Thickness(-Padding.Left, 0, 0, 0);  // from the button's left edge, not the label's padding
            if (holdTimer == null)
            {
                holdTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
                holdTimer.Tick += (s, a) =>
                {
                    if (!down || !holdPress || !IsEffectivelyEnabled || !IsEffectivelyVisible || HoldMs <= 0) { CancelPress(); return; }
                    double held = holdClock.Elapsed.TotalMilliseconds / HoldMs;
                    fill.Width = Math.Max(0, Bounds.Width - 2) * Math.Min(1, held);  // from the left edge: the fill sits outside the padding
                    if (held < 1) return;
                    down = false;  // consume this press even if Click changes the button into Write
                    StopHold();
                    Restyle();
                    Click?.Invoke();
                };
            }
            holdTimer.Start();
        }

        void StopHold() { holdTimer?.Stop(); holdClock.Reset(); fill.Width = 0; }

        void CancelPress()
        {
            var pointer = pressedPointer;
            pressedPointer = null;
            down = holdPress = false;
            StopHold();
            if (pointer?.Captured == this) pointer.Capture(null);
            RenderTransform = TransformOperations.Parse("scale(1)");
            Restyle();
        }

        void CancelInteraction(object? sender, EventArgs e) { CancelPress(); Disarm(); }

        Tone? iconColor;
        /// <summary>The icon in its own colour (Upload, Download); null = the label's.</summary>
        public Tone? IconColor { get => iconColor; set { iconColor = value; Restyle(); } }
        /// <summary>Swaps the icon (a button that switches between two states).</summary>
        public Geometry Icon { set { if (undoIcon != null) undoIcon.Data = value; } }
        public string Text { get => label.Text ?? ""; set { label.Text = value; Avalonia.Automation.AutomationProperties.SetName(this, value); } }
        /// <summary>List-like actions can align their label with neighboring rows; ordinary buttons stay centered.</summary>
        public HorizontalAlignment LabelAlignment { get => label.HorizontalAlignment; set => label.HorizontalAlignment = value; }
        public BtnKind Kind { get => kind; set { kind = value; Restyle(); } }
        public Tone? Live { get => live; set { live = value; Restyle(); } }
        public bool Dimmed { get => dimmed; set { dimmed = value; Restyle(); } }

        /// <summary>One action height, 28 px, for every button (DESIGN_SYSTEM.md §4); tiny icon buttons (under 26) stay as set.</summary>
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property != HeightProperty || label == null) return;
            double h = Height;
            if (double.IsNaN(h) || h < 26) return;
            if (h != 28) { Height = 28; return; }
            label.FontSize = Body;
            double r = 4;
            CornerRadius = new CornerRadius(r);
            fill.CornerRadius = new CornerRadius(r);
            Restyle();
        }

        /// <summary>Flat fills, no outline: filled accent = the main action, tinted accent = ordinary ones,
        /// grey = neutral, tinted red = changes a device or removes something. Live supplies a tint colour.</summary>
        public void Restyle()
        {
            Color bg, fg, edge;
            bool h = hot && IsEnabled;
            var grey = h ? Blend(Surface2, Look.Text, 0.06) : Surface2;
            edge = h ? MenuEdge : Look.Border;
            if (!IsEnabled) { bg = kind == BtnKind.Ghost ? Colors.Transparent : Surface; fg = Disabled; if (kind == BtnKind.Ghost) edge = Colors.Transparent; }
            else if (restText != null) { bg = Blend(Surface, Warn, h ? 0.32 : 0.22); fg = Warn; }  // armed two-step: amber
            else if (dimmed) { bg = grey; fg = h ? Second : Faint; }
            else if (live is Tone lc) { bg = Blend(Surface, lc, h ? 0.26 : 0.18); fg = h ? Blend(lc, Far, 0.25) : Blend(lc, Far, 0.12); }
            else switch (kind)
                {
                    case BtnKind.Primary: bg = h ? AccentHi : Accent; fg = OnAccent; edge = bg; break;
                    case BtnKind.Del: bg = Blend(Surface, Danger, h ? 0.26 : 0.18); fg = Blend(Danger, Far, h ? 0.25 : 0.12); edge = bg; break;  // the one red style: deletes and device actions
                    case BtnKind.Ghost: bg = h && !(label.Text == "" && undoIcon != null) ? Surface2 : Colors.Transparent; fg = h ? Look.Text : Second; edge = Colors.Transparent; break;
                    default: bg = grey; fg = Look.Text; break;
                }
            if (live != null || dimmed || restText != null) edge = bg;
            fill.Background = B(Alpha(live ?? Accent, 0.45));
            Background = B(bg); BorderBrush = B(keyboardFocus ? Accent : edge); label.Foreground = B(fg);
            if (undoIcon != null) undoIcon.Stroke = B(iconColor != null && IsEnabled ? iconColor : fg);
        }
    }
}

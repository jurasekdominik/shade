// Shade - shared control: ActionButton, the button in its kinds (primary, secondary, ghost, danger).
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
    /// <summary>Normal = Secondary (DESIGN_SYSTEM.md §6); AccentTint is kept as a name for Secondary; Ghost has no fill.</summary>
    public enum ButtonKind { Normal, Primary, Danger, Ghost }

    /// <summary>A button in its states: normal, primary, delete, a colour tint (Live),
    /// dimmed (greyed but still clickable, to say why it can't be used).</summary>
    public sealed class ActionButton : Border, IRestyle
    {
        protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new ActionPeer(this);

        /// <summary>Text starting with this shows the drawn undo arrow (alone, or before a word: "\u21b6 Undo").</summary>
        public const string UndoText = "\u21b6";
        /// <summary>Text that shows the drawn list icon; "+" shows a drawn plus.</summary>
        public const string ListText = "\u2630";
        readonly TextBlock label;
        readonly bool undoLabel;
        readonly Avalonia.Controls.Shapes.Path? undoIcon;
        readonly Border fill = new Border { HorizontalAlignment = HorizontalAlignment.Left, Width = 0, CornerRadius = new CornerRadius(7) };  // hold to confirm: the fill so far
        ButtonKind kind;
        Tone? live;
        bool hot, down, dimmed, keyboardFocus, keyPressed, adjustingHeight;
        public event Action? Click;
        /// <summary>Run on a click, after Click (MVVM); its CanExecute enables the button.</summary>
        public static readonly StyledProperty<System.Windows.Input.ICommand?> CommandProperty =
            AvaloniaProperty.Register<ActionButton, System.Windows.Input.ICommand?>(nameof(Command));
        public static readonly StyledProperty<object?> CommandParameterProperty = AvaloniaProperty.Register<ActionButton, object?>(nameof(CommandParameter));
        public System.Windows.Input.ICommand? Command { get => GetValue(CommandProperty); set => SetValue(CommandProperty, value); }
        public object? CommandParameter { get => GetValue(CommandParameterProperty); set => SetValue(CommandParameterProperty, value); }

        /// <summary>Press and hold this long (ms) to fire Click (Unlock): a fill runs across while held, letting go early
        /// cancels. 0 = a normal click.</summary>
        public static readonly StyledProperty<int> HoldMsProperty = AvaloniaProperty.Register<ActionButton, int>(nameof(HoldMs), validate: v => v >= 0);
        public int HoldMs { get => GetValue(HoldMsProperty); set => SetValue(HoldMsProperty, value); }
        public bool KeepPointerFocus { get; set; }
        DispatcherTimer? holdTimer;
        readonly Stopwatch holdClock = new Stopwatch();
        IPointer? pressedPointer;
        bool holdPress;
        Window? holdWindow;
        /// <summary>Two-step confirm (an action that changes something outside the app): the first click turns the button into this text, a second click within 4 s fires
        /// Click; otherwise it goes back. null = a normal click.</summary>
        public static readonly StyledProperty<string?> ConfirmTextProperty = AvaloniaProperty.Register<ActionButton, string?>(nameof(ConfirmText));
        public string? ConfirmText { get => GetValue(ConfirmTextProperty); set => SetValue(ConfirmTextProperty, value); }
        /// <summary>Optional cancellable action. Do not also set Command. Errors are retained and reported through Failed.</summary>
        public Func<System.Threading.CancellationToken, Task>? AsyncAction { get; set; }
        public static readonly DirectProperty<ActionButton, bool> IsBusyProperty = AvaloniaProperty.RegisterDirect<ActionButton, bool>(nameof(IsBusy), o => o.IsBusy);
        bool busy;
        public bool IsBusy => busy;
        public Exception? LastError { get; private set; }
        public event Action<Exception>? Failed;
        System.Threading.CancellationTokenSource? cancellation;
        public void Cancel() => cancellation?.Cancel();
        public async Task RunAsync()
        {
            if (busy || !IsEffectivelyEnabled || AsyncAction is not { } action) return;
            LastError = null;
            using var source = new System.Threading.CancellationTokenSource();
            cancellation = source;
            SetAndRaise(IsBusyProperty, ref busy, true); UpdateIsEffectivelyEnabled(); Restyle();
            try { await action(source.Token); }
            catch (OperationCanceledException) when (source.IsCancellationRequested) { }
            catch (Exception error) { LastError = error; Failed?.Invoke(error); }
            finally { cancellation = null; SetAndRaise(IsBusyProperty, ref busy, false); UpdateIsEffectivelyEnabled(); Restyle(); }
        }
        string? restText;
        DispatcherTimer? armTimer;
        public bool Armed => restText != null;

        /// <summary>icon: a Lucide shape before the label, 6 px apart (DESIGN_SYSTEM.md §6).</summary>
        public ActionButton() : this("") { }

        public ActionButton(string text, ButtonKind kind = ButtonKind.Normal, Geometry? icon = null)
        {
            this.kind = kind; undoLabel = text.StartsWith(UndoText, StringComparison.Ordinal);
            Focusable = true; FocusAdorner = null;
            Avalonia.Automation.AutomationProperties.SetName(this, text);
            GotFocus += (_, e) => { keyboardFocus = Look.IsKeyboard(e); Restyle(); };
            LostFocus += (_, _) => { keyboardFocus = false; keyPressed = false; Restyle(); };
            KeyUp += (_, e) => { if (e.Key is Key.Enter or Key.Space) keyPressed = false; };
            KeyDown += (_, e) =>
            {
                if (IsEffectivelyEnabled && HoldMs == 0 && (e.Key == Key.Enter || e.Key == Key.Space)) { if (!keyPressed) { keyPressed = true; PerformClick(); } e.Handled = true; }
            };
            label = Build.Text(text, Text2, TextSize.Body, 500);  // Body; a 36 px button takes Section (OnPropertyChanged)
            label.HorizontalAlignment = HorizontalAlignment.Center;
            var p = new Panel();
            p.Children.Add(fill);
            if (text == "+" || text == ListText)  // drawn, not a font glyph: centred at any size
            {
                undoIcon = Build.Icon24(text == "+" ? Icons.Plus : Icons.List, 16, 2);
                label.Text = "";
                p.Children.Add(undoIcon);
            }
            else if (text.StartsWith(UndoText, StringComparison.Ordinal))  // the drawn undo arrow (16 px), then the word: the one undo look
            {
                undoIcon = Build.Icon24(Field.UndoGeometry, 16, 2);
                label.Text = text.Substring(1).Trim();
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                row.Children.Add(undoIcon);
                label.IsVisible = label.Text != ""; row.Children.Add(label);
                p.Children.Add(row);
            }
            else if (icon != null)
            {
                undoIcon = Build.Icon24(icon, 16, 2);
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                row.Children.Add(undoIcon); label.IsVisible = text != ""; row.Children.Add(label);  // icon only: centred, 28 px square
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
            Height = ControlHeight;
            Cursor = Build.Hand;
            RenderTransform = TransformOperations.Parse("scale(1)");
            Transitions = ReducedMotion ? null : new Transitions { new TransformOperationsTransition { Property = RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(90) } };
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
                if (e.Property == IsEffectivelyEnabledProperty || e.Property == IsEnabledProperty || e.Property == IsVisibleProperty)
                {
                    if (!IsEffectivelyEnabled || !IsEffectivelyVisible) { CancelPress(); Disarm(); }
                    Restyle();
                }
            };
            AttachedToVisualTree += (s, e) =>
            {
                SubscribeCommand();
                CanExecuteChanged(null, EventArgs.Empty);
                holdWindow = TopLevel.GetTopLevel(this) as Window;
                if (holdWindow != null) holdWindow.Deactivated += CancelInteraction;
            };
            DetachedFromVisualTree += (s, e) =>
            {
                if (holdWindow != null) holdWindow.Deactivated -= CancelInteraction;
                holdWindow = null;
                if (Command is { } command && commandHandler != null) command.CanExecuteChanged -= commandHandler;
                Cancel();
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
            Fire();
        }

        void Fire()
        {
            if (AsyncAction != null) { _ = RunAsync(); return; }
            Click?.Invoke();
            if (Command is { } c && c.CanExecute(CommandParameter)) c.Execute(CommandParameter);
        }

        bool canExecute = true;
        protected override bool IsEnabledCore => base.IsEnabledCore && canExecute && !busy;
        void CanExecuteChanged(object? s, EventArgs e) { canExecute = Command?.CanExecute(CommandParameter) ?? true; UpdateIsEffectivelyEnabled(); }

        EventHandler? commandHandler;
        void SubscribeCommand()
        {
            if (Command is not { } command) return;
            if (commandHandler != null) command.CanExecuteChanged -= commandHandler;
            var weak = new WeakReference<ActionButton>(this);
            commandHandler = (_, e) => { if (weak.TryGetTarget(out var button)) button.CanExecuteChanged(null, e); };
            command.CanExecuteChanged += commandHandler;
        }
        void CommandChanged(AvaloniaPropertyChangedEventArgs change)
        {
            if (change.Property == CommandProperty)
            {
                if (change.OldValue is System.Windows.Input.ICommand was && commandHandler != null) was.CanExecuteChanged -= commandHandler;
                SubscribeCommand();
                CanExecuteChanged(null, EventArgs.Empty);
            }
            else if (change.Property == CommandParameterProperty) CanExecuteChanged(null, EventArgs.Empty);
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
                    down = false;  // consume this press even if Click changes the button
                    StopHold();
                    Restyle();
                    Fire();
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
        public Geometry? Icon { get => undoIcon?.Data; set { if (undoIcon != null) undoIcon.Data = value; } }
        public static readonly DirectProperty<ActionButton, string> TextProperty = AvaloniaProperty.RegisterDirect<ActionButton, string>(nameof(Text), o => o.Text, (o, v) => o.Text = v, "");
        public string Text
        {
            get => label.Text ?? "";
            set
            {
                var before = Text;
                label.Text = undoLabel && value.StartsWith(UndoText, StringComparison.Ordinal) ? value.Substring(1).Trim() : value;
                label.IsVisible = !string.IsNullOrEmpty(label.Text);
                Avalonia.Automation.AutomationProperties.SetName(this, value);
                if (before != Text) RaisePropertyChanged(TextProperty, before, Text);
            }
        }
        /// <summary>List-like actions can align their label with neighboring rows; ordinary buttons stay centered.</summary>
        public HorizontalAlignment LabelAlignment { get => label.HorizontalAlignment; set => label.HorizontalAlignment = value; }
        public ButtonKind Kind { get => kind; set { kind = value; Restyle(); } }
        public Tone? Live { get => live; set { live = value; Restyle(); } }
        public bool Dimmed { get => dimmed; set { dimmed = value; Restyle(); } }

        /// <summary>One action height, 28 px, for every button (DESIGN_SYSTEM.md §4); tiny icon buttons (under 26) stay as set.</summary>
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            CommandChanged(change);
            if (change.Property != HeightProperty || label == null || adjustingHeight) return;
            double h = Height;
            if (double.IsNaN(h)) return;
            if (h < 26)
            {
                if (MetricScale <= 1) return;
                adjustingHeight = true;
                try { Height = M(h); }
                finally { adjustingHeight = false; }
                label.FontSize = TextSize.Body; return;
            }
            if (h != ControlHeight) { Height = ControlHeight; return; }
            label.FontSize = TextSize.Body;
            double r = 4;
            CornerRadius = new CornerRadius(r);
            fill.CornerRadius = new CornerRadius(r);
            Restyle();
        }

        /// <summary>Flat fills, no outline: filled accent = the main action, tinted accent = ordinary ones,
        /// grey = neutral, tinted red = removes something or changes the outside world. Live supplies a tint colour.</summary>
        public void Restyle()
        {
            Color bg, fg, edge;
            bool h = hot && IsEffectivelyEnabled;
            var grey = h ? Blend(Fill, Look.Text, 0.06) : Fill;
            edge = h ? Outline : Look.Separator;
            if (!IsEffectivelyEnabled) { bg = kind == ButtonKind.Ghost ? Colors.Transparent : Raised; fg = Disabled; if (kind == ButtonKind.Ghost) edge = Colors.Transparent; }
            else if (restText != null) { bg = Blend(Raised, Warn, h ? 0.32 : 0.22); fg = Warn; }  // armed two-step: amber
            else if (dimmed) { bg = grey; fg = h ? Text2 : Text3; }
            else if (live is Tone lc) { bg = Blend(Raised, lc, h ? 0.26 : 0.18); fg = h ? Blend(lc, Far, 0.25) : Blend(lc, Far, 0.12); }
            else switch (kind)
                {
                    case ButtonKind.Primary: bg = h ? AccentHi : Accent; fg = OnAccent; edge = bg; break;
                    case ButtonKind.Danger: bg = h ? Blend(Raised, Danger, 0.26) : DangerMuted; fg = h ? Blend(Danger, Far, 0.25) : DangerText; edge = bg; break;  // the one red style: deletes and actions on the outside world
                    case ButtonKind.Ghost: bg = h && !(label.Text == "" && undoIcon != null) ? Fill : Colors.Transparent; fg = h ? Look.Text : Text2; edge = Colors.Transparent; break;
                    default: bg = grey; fg = Look.Text; break;
                }
            if (live != null || dimmed || restText != null) edge = bg;
            fill.Background = B(Alpha(live ?? Accent, 0.45));
            Background = B(bg); BorderBrush = B(keyboardFocus && IsEffectivelyEnabled ? FocusBorder : edge); label.Foreground = B(fg);
            if (undoIcon != null) undoIcon.Stroke = B(iconColor != null && IsEffectivelyEnabled ? iconColor : fg);
        }
    }
}

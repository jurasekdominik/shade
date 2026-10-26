// Shade actions extend Avalonia Button; the theme owns their presentation.
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using static Shade.Look;

namespace Shade;

public enum ButtonKind { Normal, Primary, Danger, Ghost }

/// <summary>A native Button with Shade visuals and optional confirmation, hold or cancellable asynchronous execution.</summary>
public class ActionButton : Button, IRestyle
{
    public const string UndoText = "\u21b6", ListText = "\u2630";
    public static readonly StyledProperty<ButtonKind> KindProperty = AvaloniaProperty.Register<ActionButton, ButtonKind>(nameof(Kind));
    public static readonly StyledProperty<Geometry?> IconProperty = AvaloniaProperty.Register<ActionButton, Geometry?>(nameof(Icon));
    public static readonly StyledProperty<double> IconSizeProperty = AvaloniaProperty.Register<ActionButton, double>(nameof(IconSize), M(16), validate: value => double.IsFinite(value) && value > 0);
    public static readonly StyledProperty<Tone?> IconColorProperty = AvaloniaProperty.Register<ActionButton, Tone?>(nameof(IconColor));
    public static readonly StyledProperty<Tone?> LiveProperty = AvaloniaProperty.Register<ActionButton, Tone?>(nameof(Live));
    public static readonly StyledProperty<bool> DimmedProperty = AvaloniaProperty.Register<ActionButton, bool>(nameof(Dimmed));
    public static readonly StyledProperty<int> HoldMsProperty = AvaloniaProperty.Register<ActionButton, int>(nameof(HoldMs), validate: value => value >= 0);
    public static readonly StyledProperty<string?> ConfirmTextProperty = AvaloniaProperty.Register<ActionButton, string?>(nameof(ConfirmText));
    public static readonly DirectProperty<ActionButton, bool> ArmedProperty = AvaloniaProperty.RegisterDirect<ActionButton, bool>(nameof(Armed), button => button.Armed);
    public static readonly DirectProperty<ActionButton, bool> IsBusyProperty = AvaloniaProperty.RegisterDirect<ActionButton, bool>(nameof(IsBusy), button => button.IsBusy);
    public ButtonKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public Geometry? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }
    public double IconSize { get => GetValue(IconSizeProperty); set => SetValue(IconSizeProperty, value); }
    public Tone? IconColor { get => GetValue(IconColorProperty); set => SetValue(IconColorProperty, value); }
    public Tone? Live { get => GetValue(LiveProperty); set => SetValue(LiveProperty, value); }
    public bool Dimmed { get => GetValue(DimmedProperty); set => SetValue(DimmedProperty, value); }
    public int HoldMs { get => GetValue(HoldMsProperty); set => SetValue(HoldMsProperty, value); }
    public string? ConfirmText { get => GetValue(ConfirmTextProperty); set => SetValue(ConfirmTextProperty, value); }
    public bool Armed => armed;
    public bool IsBusy => busy;
    /// <summary>Optional callback instead of Click/Command. Failure presentation belongs to the app.</summary>
    public Func<CancellationToken, Task>? AsyncAction { get; set; }
    public Exception? LastError { get; private set; }
    public event Action<Exception>? Failed;
    bool armed, busy, keyPressed, holdPress, suppressRelease;
    DispatcherTimer? armTimer, holdTimer;
    Stopwatch? holdClock;
    CancellationTokenSource? cancellation;
    Window? window;
    Border? fill;
    Avalonia.Controls.Shapes.Path? icon;
    SolidColorBrush? liveFill, liveHover, liveText, liveTextHover;
    static readonly FuncValueConverter<object?, bool> HasContent = new(value => value != null && value is not "");
    static readonly FuncValueConverter<object?, bool> HasIcon = new(value => value != null);
    protected override Type StyleKeyOverride => typeof(ActionButton);

    public ActionButton()
    {
        FocusAdorner = null;
        AttachedToVisualTree += (_, _) => { window = TopLevel.GetTopLevel(this) as Window; if (window != null) window.Deactivated += CancelInteraction; };
        DetachedFromVisualTree += (_, _) => { if (window != null) window.Deactivated -= CancelInteraction; window = null; Cancel(); CancelInteraction(this, EventArgs.Empty); };
        LostFocus += (_, _) => keyPressed = false;
        Restyle();
    }
    public ActionButton(string text, ButtonKind kind = ButtonKind.Normal, Geometry? icon = null) : this()
    {
        Kind = kind;
        if (text == "+" || text == ListText) { Icon = text == "+" ? Icons.Plus : Icons.List; Content = null; }
        else if (text.StartsWith(UndoText, StringComparison.Ordinal)) { Icon = Field.UndoGeometry; Content = text.Substring(1).Trim(); }
        else { Content = text; Icon = icon; }
    }
    internal Control CreateTemplate(INameScope names)
    {
        icon = new Avalonia.Controls.Shapes.Path { Width = 24, Height = 24, Stretch = Stretch.None, StrokeThickness = 2, StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round };
        icon[!Avalonia.Controls.Shapes.Path.DataProperty] = this[!IconProperty];
        var glyph = new Viewbox { Child = icon, VerticalAlignment = VerticalAlignment.Center };
        glyph[!WidthProperty] = this[!IconSizeProperty]; glyph[!HeightProperty] = this[!IconSizeProperty];
        glyph.Bind(IsVisibleProperty, new Binding(nameof(Icon)) { Source = this, Converter = HasIcon });
        var presenter = new ContentPresenter { Name = "PART_ContentPresenter" };
        names.Register("PART_ContentPresenter", presenter);
        presenter.Bind(ContentPresenter.ContentProperty, new TemplateBinding(ContentProperty));
        presenter.Bind(ContentPresenter.ContentTemplateProperty, new TemplateBinding(ContentTemplateProperty));
        presenter.Bind(IsVisibleProperty, new Binding(nameof(Content)) { Source = this, Converter = HasContent });
        presenter.Bind(ContentPresenter.HorizontalContentAlignmentProperty, new TemplateBinding(HorizontalContentAlignmentProperty));
        presenter.Bind(ContentPresenter.VerticalContentAlignmentProperty, new TemplateBinding(VerticalContentAlignmentProperty));
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = M(6), VerticalAlignment = VerticalAlignment.Center, Children = { glyph, presenter } };
        row[!HorizontalAlignmentProperty] = this[!HorizontalContentAlignmentProperty];
        fill = new Border { HorizontalAlignment = HorizontalAlignment.Left, Width = 0, CornerRadius = new CornerRadius(Radius) };
        var panel = new Panel { Children = { fill, row } };
        var surface = new Border { Child = panel };
        surface.Bind(Border.BackgroundProperty, new TemplateBinding(BackgroundProperty)); surface.Bind(Border.BorderBrushProperty, new TemplateBinding(BorderBrushProperty));
        surface.Bind(Border.BorderThicknessProperty, new TemplateBinding(BorderThicknessProperty)); surface.Bind(Border.CornerRadiusProperty, new TemplateBinding(CornerRadiusProperty));
        surface.Bind(Border.PaddingProperty, new TemplateBinding(PaddingProperty));
        Restyle(); return surface;
    }
    protected override bool IsEnabledCore => base.IsEnabledCore && !busy;
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEffectivelyEnabledProperty || change.Property == IsVisibleProperty)
            if (!IsEffectivelyEnabled || !IsEffectivelyVisible) CancelInteraction(this, EventArgs.Empty);
        if (change.Property == ConfirmTextProperty && ConfirmText == null) Disarm();
        if (change.Property == HoldMsProperty) StopHold();
        if (change.Property == KindProperty || change.Property == IconProperty || change.Property == IconColorProperty || change.Property == ContentProperty || change.Property == LiveProperty || change.Property == DimmedProperty || change.Property == ForegroundProperty) Restyle();
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Space)
        {
            if (keyPressed || HoldMs > 0) { e.Handled = true; return; }
            keyPressed = true; suppressRelease = false;
        }
        base.OnKeyDown(e);
    }
    protected override void OnKeyUp(KeyEventArgs e) { base.OnKeyUp(e); if (e.Key is Key.Enter or Key.Space) keyPressed = false; }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) { holdPress = HoldMs > 0; suppressRelease = holdPress; }
        base.OnPointerPressed(e);
        if (holdPress && IsPressed && IsEffectivelyEnabled)
        {
            holdClock ??= new Stopwatch(); holdClock.Restart();
            holdTimer ??= new DispatcherTimer(TimeSpan.FromMilliseconds(15), DispatcherPriority.Normal, (_, _) => HoldTick()); holdTimer.Start();
        }
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e) { base.OnPointerReleased(e); StopHold(); suppressRelease = false; }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e) { base.OnPointerCaptureLost(e); StopHold(); }
    protected override void OnPointerMoved(PointerEventArgs e) { base.OnPointerMoved(e); if (holdPress && !new Rect(Bounds.Size).Contains(e.GetPosition(this))) StopHold(); }
    void HoldTick()
    {
        if (!holdPress || !IsPressed || !IsEffectivelyEnabled || !IsEffectivelyVisible || HoldMs == 0) { StopHold(); return; }
        double share = holdClock!.Elapsed.TotalMilliseconds / HoldMs;
        if (fill != null) { fill.Margin = new Thickness(-Padding.Left, 0, 0, 0); fill.Width = Math.Max(0, Bounds.Width - 2) * Math.Min(1, share); }
        if (share < 1) return;
        StopHold(); Fire();
    }
    void StopHold() { holdPress = false; holdTimer?.Stop(); holdClock?.Reset(); if (fill != null) fill.Width = 0; }
    void CancelInteraction(object? sender, EventArgs args) { StopHold(); Disarm(); keyPressed = false; }
    /// <summary>Uses the same enabled/confirmation policy as native pointer, keyboard, default-button and automation activation.</summary>
    public void PerformClick() { if (!holdPress) { suppressRelease = false; OnClick(); } }
    protected override void OnClick()
    {
        if (!IsEffectivelyEnabled || HoldMs > 0 || suppressRelease) return;
        if (ConfirmText != null && !armed)
        {
            SetAndRaise(ArmedProperty, ref armed, true); Restyle();
            armTimer ??= new DispatcherTimer(TimeSpan.FromSeconds(4), DispatcherPriority.Normal, (_, _) => Disarm()); armTimer.Stop(); armTimer.Start(); return;
        }
        Disarm(); Fire();
    }
    void Fire() { if (AsyncAction != null) _ = RunAsync(); else base.OnClick(); }
    public void Disarm() { if (SetAndRaise(ArmedProperty, ref armed, false)) Restyle(); armTimer?.Stop(); }
    public void Cancel() => cancellation?.Cancel();
    public async Task RunAsync()
    {
        if (busy || !IsEffectivelyEnabled || AsyncAction is not { } action) return;
        LastError = null; using var source = new CancellationTokenSource(); cancellation = source;
        SetAndRaise(IsBusyProperty, ref busy, true); UpdateIsEffectivelyEnabled();
        try { await action(source.Token); }
        catch (OperationCanceledException) when (source.IsCancellationRequested) { }
        catch (Exception error) { LastError = error; Failed?.Invoke(error); }
        finally { cancellation = null; SetAndRaise(IsBusyProperty, ref busy, false); UpdateIsEffectivelyEnabled(); }
    }
    public void Restyle()
    {
        PseudoClasses.Set(":primary", Kind == ButtonKind.Primary); PseudoClasses.Set(":danger", Kind == ButtonKind.Danger); PseudoClasses.Set(":ghost", Kind == ButtonKind.Ghost);
        PseudoClasses.Set(":dimmed", Dimmed); PseudoClasses.Set(":armed", armed);
        bool labelled = Content != null && Content is not ""; PseudoClasses.Set(":labelled", labelled); PseudoClasses.Set(":icon-only", !labelled && Icon != null);
        if (icon != null) icon.Stroke = IconColor?.Brush ?? Foreground;
        if (fill != null) fill.Background = B(Alpha(Live ?? Accent, .45));
        if (Live != null)
        {
            if (liveFill == null)
            {
                liveFill = new(); liveHover = new(); liveText = new(); liveTextHover = new();
                var normal = new Style(x => x.Is<ActionButton>().Class(":live").Not(y => y.Class(":disabled")).Not(y => y.Class(":armed")).Not(y => y.Class(":dimmed")));
                normal.Setters.Add(new Setter(BackgroundProperty, liveFill)); normal.Setters.Add(new Setter(BorderBrushProperty, liveFill)); normal.Setters.Add(new Setter(ForegroundProperty, liveText)); Styles.Add(normal);
                var hover = new Style(x => x.Is<ActionButton>().Class(":live").Class(":pointerover").Not(y => y.Class(":disabled")).Not(y => y.Class(":armed")).Not(y => y.Class(":dimmed")));
                hover.Setters.Add(new Setter(BackgroundProperty, liveHover)); hover.Setters.Add(new Setter(BorderBrushProperty, liveHover)); hover.Setters.Add(new Setter(ForegroundProperty, liveTextHover)); Styles.Add(hover);
            }
            liveFill.Color = Tint(Live, .18); liveHover!.Color = Tint(Live, .26); liveText!.Color = Blend(Live, Far, .12); liveTextHover!.Color = Blend(Live, Far, .25);
        }
        PseudoClasses.Set(":live", Live != null);
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using static Shade.Look;
using static Shade.ThemeKit;

namespace Shade;

/// <summary>The compact, square paper grid (A) or the roomier Shade field (C).</summary>
public enum CharacterGridAppearance { Paper, Field }

/// <summary>A presentation override for a fixed, zero-based range of character cells.</summary>
public sealed class CharacterCellDecoration : StyledElement
{
    public static readonly StyledProperty<int> StartProperty = AvaloniaProperty.Register<CharacterCellDecoration, int>(nameof(Start), validate: n => n >= 0);
    public static readonly StyledProperty<int> LengthProperty = AvaloniaProperty.Register<CharacterCellDecoration, int>(nameof(Length), 1, validate: n => n > 0);
    public static readonly StyledProperty<IBrush?> BackgroundProperty = AvaloniaProperty.Register<CharacterCellDecoration, IBrush?>(nameof(Background));
    public static readonly StyledProperty<IBrush?> ForegroundProperty = AvaloniaProperty.Register<CharacterCellDecoration, IBrush?>(nameof(Foreground));
    public static readonly StyledProperty<IBrush?> BorderBrushProperty = AvaloniaProperty.Register<CharacterCellDecoration, IBrush?>(nameof(BorderBrush));
    public int Start { get => GetValue(StartProperty); set => SetValue(StartProperty, value); }
    public int Length { get => GetValue(LengthProperty); set => SetValue(LengthProperty, value); }
    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public IBrush? BorderBrush { get => GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }
}

/// <summary>One native string editor, with a fixed advance per grapheme and two paper-grid appearances.</summary>
public sealed partial class CharacterGridField : TextBox
{
    public static readonly StyledProperty<int> CellCountProperty = AvaloniaProperty.Register<CharacterGridField, int>(nameof(CellCount), 16, validate: n => n > 0);
    public static readonly StyledProperty<Size> CellSizeProperty = AvaloniaProperty.Register<CharacterGridField, Size>(nameof(CellSize), new Size(20, 26), validate: s => double.IsFinite(s.Width) && double.IsFinite(s.Height) && s.Width > 0 && s.Height > 0);
    public static readonly StyledProperty<double> CellGapProperty = AvaloniaProperty.Register<CharacterGridField, double>(nameof(CellGap), validate: n => double.IsFinite(n) && n >= 0);
    public static readonly StyledProperty<Thickness> CellPaddingProperty = AvaloniaProperty.Register<CharacterGridField, Thickness>(nameof(CellPadding), validate: t => double.IsFinite(t.Left + t.Right + t.Top + t.Bottom) && t.Left >= 0 && t.Top >= 0 && t.Right >= 0 && t.Bottom >= 0);
    public static readonly StyledProperty<CharacterGridAppearance> AppearanceProperty = AvaloniaProperty.Register<CharacterGridField, CharacterGridAppearance>(nameof(Appearance), CharacterGridAppearance.Field, validate: Enum.IsDefined);
    public static readonly StyledProperty<TextAlignment> CharacterAlignmentProperty = AvaloniaProperty.Register<CharacterGridField, TextAlignment>(nameof(CharacterAlignment), TextAlignment.Center, validate: a => a is TextAlignment.Left or TextAlignment.Center or TextAlignment.Right);
    public static readonly StyledProperty<IBrush?> CellBorderBrushProperty = AvaloniaProperty.Register<CharacterGridField, IBrush?>(nameof(CellBorderBrush));
    public static readonly StyledProperty<double> CellBorderThicknessProperty = AvaloniaProperty.Register<CharacterGridField, double>(nameof(CellBorderThickness), 1, validate: n => double.IsFinite(n) && n >= 0);
    public static readonly StyledProperty<IBrush?> FocusBorderBrushProperty = AvaloniaProperty.Register<CharacterGridField, IBrush?>(nameof(FocusBorderBrush));
    public static readonly StyledProperty<bool> InvalidProperty = AvaloniaProperty.Register<CharacterGridField, bool>(nameof(Invalid));
    public static readonly StyledProperty<bool> PendingProperty = AvaloniaProperty.Register<CharacterGridField, bool>(nameof(Pending));
    public static readonly StyledProperty<string?> ErrorProperty = AvaloniaProperty.Register<CharacterGridField, string?>(nameof(Error));
    public static readonly StyledProperty<IList<CharacterCellDecoration>?> CellDecorationsProperty = AvaloniaProperty.Register<CharacterGridField, IList<CharacterCellDecoration>?>(nameof(CellDecorations));
    public static readonly DirectProperty<CharacterGridField, string?> ValidationMessageProperty = AvaloniaProperty.RegisterDirect<CharacterGridField, string?>(nameof(ValidationMessage), c => c.ValidationMessage);
    internal static readonly StyledProperty<IBrush?> FrameBrushProperty = AvaloniaProperty.Register<CharacterGridField, IBrush?>("FrameBrush");
    public int CellCount { get => GetValue(CellCountProperty); set => SetValue(CellCountProperty, value); }
    public Size CellSize { get => GetValue(CellSizeProperty); set => SetValue(CellSizeProperty, value); }
    public double CellGap { get => GetValue(CellGapProperty); set => SetValue(CellGapProperty, value); }
    public Thickness CellPadding { get => GetValue(CellPaddingProperty); set => SetValue(CellPaddingProperty, value); }
    public CharacterGridAppearance Appearance { get => GetValue(AppearanceProperty); set => SetValue(AppearanceProperty, value); }
    public TextAlignment CharacterAlignment { get => GetValue(CharacterAlignmentProperty); set => SetValue(CharacterAlignmentProperty, value); }
    public IBrush? CellBorderBrush { get => GetValue(CellBorderBrushProperty); set => SetValue(CellBorderBrushProperty, value); }
    public double CellBorderThickness { get => GetValue(CellBorderThicknessProperty); set => SetValue(CellBorderThicknessProperty, value); }
    public IBrush? FocusBorderBrush { get => GetValue(FocusBorderBrushProperty); set => SetValue(FocusBorderBrushProperty, value); }
    public bool Invalid { get => GetValue(InvalidProperty); set => SetValue(InvalidProperty, value); }
    public bool Pending { get => GetValue(PendingProperty); set => SetValue(PendingProperty, value); }
    public string? Error { get => GetValue(ErrorProperty); set => SetValue(ErrorProperty, value); }
    public IList<CharacterCellDecoration>? CellDecorations { get => GetValue(CellDecorationsProperty); set => SetValue(CellDecorationsProperty, value); }
    string? validationMessage, bindingError;
    public string? ValidationMessage => validationMessage;
    GridPresenter? presenter;
    ScrollViewer? scroll;
    Control? surface, lines;
    readonly HashSet<CharacterCellDecoration> watched = new();
    INotifyCollectionChanged? collection;
    bool attached;
    internal double Pitch => CellSize.Width + CellGap;
    // Paper forms count the frame inside their physical cell bounds.
    internal bool InsetFrame;
    internal Point GridOrigin => new((InsetFrame ? 0 : BorderThickness.Left) + Padding.Left, (InsetFrame ? 0 : BorderThickness.Top) + Padding.Top);
    protected override Type StyleKeyOverride => typeof(CharacterGridField);

    public CharacterGridField()
    {
        Classes.Add("plain");
        SetCurrentValue(CellDecorationsProperty, new ObservableCollection<CharacterCellDecoration>());
        Words(this, c => c.RefreshValidation(), "validation");
        PastingFromClipboard += PasteIntoCells;
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (e.Handled) return;
        string current = Text ?? "", input = e.Text ?? "";
        int start = Math.Min(SelectionStart, SelectionEnd), length = Math.Abs(SelectionEnd - SelectionStart);
        string next = current.Remove(start, length).Insert(start, input);
        int count = new StringInfo(next).LengthInTextElements;
        if (!IsEffectivelyEnabled || IsReadOnly || input.Any(c => c is '\r' or '\n' or '\t') || count > CellCount && count > new StringInfo(current).LengthInTextElements)
        {
            e.Handled = true; return;
        }
        base.OnTextInput(e);
    }

    async void PasteIntoCells(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        e.Handled = true;
        var root = TopLevel.GetTopLevel(this);
        if (root?.Clipboard is not { } clipboard || IsReadOnly || !IsEffectivelyEnabled) return;
        var before = (Text, SelectionStart, SelectionEnd); bool focused = IsFocused;
        try
        {
            string? text = await clipboard.TryGetTextAsync();
            if (TopLevel.GetTopLevel(this) != root || before != (Text, SelectionStart, SelectionEnd) || focused && !IsFocused || string.IsNullOrEmpty(text)) return;
            RaiseEvent(new TextInputEventArgs { RoutedEvent = TextInputEvent, Text = text });
        }
        catch (Exception error) when (error is System.IO.IOException or System.Runtime.InteropServices.ExternalException or InvalidOperationException or NotSupportedException or OperationCanceledException) { }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        base.MeasureOverride(availableSize);
        return new Size(CellCount * CellSize.Width + (CellCount - 1) * CellGap + (InsetFrame ? 0 : BorderThickness.Left + BorderThickness.Right) + Padding.Left + Padding.Right,
            CellSize.Height + (InsetFrame ? 0 : BorderThickness.Top + BorderThickness.Bottom) + Padding.Top + Padding.Bottom);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        if (scroll != null) scroll.PropertyChanged -= ScrollChanged;
        base.OnApplyTemplate(e);
        presenter = e.NameScope.Find<GridPresenter>("PART_TextPresenter");
        surface = e.NameScope.Find<Control>("PART_CellSurface");
        lines = e.NameScope.Find<Control>("PART_CellLines");
        scroll = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
        if (scroll != null) scroll.PropertyChanged += ScrollChanged;
        presenter?.Refresh();
    }

    void ScrollChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ScrollViewer.OffsetProperty) { surface?.InvalidateVisual(); lines?.InvalidateVisual(); }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CellDecorationsProperty) WatchDecorations();
        if (change.Property == IsReadOnlyProperty) PseudoClasses.Set(":read-only", IsReadOnly);
        if (change.Property == TextProperty || change.Property == CellCountProperty || change.Property == ErrorProperty || change.Property == InvalidProperty || change.Property == PendingProperty) RefreshValidation();
        if (change.Property == CellCountProperty || change.Property == CellSizeProperty || change.Property == CellGapProperty || change.Property == PaddingProperty || change.Property == BorderThicknessProperty) InvalidateMeasure();
        presenter?.Refresh(); surface?.InvalidateVisual(); lines?.InvalidateVisual();
    }

    protected override void UpdateDataValidation(AvaloniaProperty property, BindingValueType state, Exception? error)
    {
        base.UpdateDataValidation(property, state, error);
        if (property == TextProperty) { bindingError = error?.Message; RefreshValidation(); }
    }

    void RefreshValidation()
    {
        string? message = Error ?? bindingError;
        if (message == null && new StringInfo(Text ?? "").LengthInTextElements > CellCount) message = T("Use at most {0} characters.", CellCount);
        if (message == null && (Text ?? "").Any(c => c is '\r' or '\n' or '\t')) message = T("Use a single line without tabs.");
        SetAndRaise(ValidationMessageProperty, ref validationMessage, message);
        PseudoClasses.Set(":invalid", Invalid || message != null);
        PseudoClasses.Set(":pending", Pending);
    }

    void WatchDecorations()
    {
        if (collection != null) collection.CollectionChanged -= DecorationsChanged;
        foreach (var decoration in watched)
        {
            decoration.PropertyChanged -= DecorationChanged;
            LogicalChildren.Remove(decoration);
        }
        watched.Clear(); collection = null;
        if (!attached) return;
        collection = CellDecorations as INotifyCollectionChanged;
        if (collection != null) collection.CollectionChanged += DecorationsChanged;
        foreach (var decoration in CellDecorations ?? Array.Empty<CharacterCellDecoration>())
            if (watched.Add(decoration))
            {
                if (decoration.Parent == null) LogicalChildren.Add(decoration);
                decoration.PropertyChanged += DecorationChanged;
            }
        presenter?.Refresh(); surface?.InvalidateVisual(); lines?.InvalidateVisual();
    }
    void DecorationsChanged(object? sender, NotifyCollectionChangedEventArgs e) => WatchDecorations();
    void DecorationChanged(object? sender, AvaloniaPropertyChangedEventArgs e) { presenter?.Refresh(); surface?.InvalidateVisual(); lines?.InvalidateVisual(); }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) { base.OnAttachedToVisualTree(e); attached = true; WatchDecorations(); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) { attached = false; WatchDecorations(); base.OnDetachedFromVisualTree(e); }

    internal IBrush? Decoration(int cell, Func<CharacterCellDecoration, IBrush?> brush)
    {
        IBrush? result = null;
        foreach (var decoration in CellDecorations ?? Array.Empty<CharacterCellDecoration>())
            if (cell >= decoration.Start && cell - decoration.Start < decoration.Length && brush(decoration) is { } value) result = value;
        return result;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new GridAutomationPeer(this);

    sealed class GridAutomationPeer(CharacterGridField owner) : TextBoxAutomationPeer(owner), IValueProvider
    {
        bool IValueProvider.IsReadOnly => owner.IsReadOnly || !owner.IsEffectivelyEnabled;
        string? IValueProvider.Value => owner.Text;
        void IValueProvider.SetValue(string? value)
        {
            if (owner.IsReadOnly || !owner.IsEffectivelyEnabled) throw new InvalidOperationException(T("The field cannot be edited."));
            owner.SetCurrentValue(TextProperty, value);
        }
        protected override string? GetItemStatusCore() => owner.ValidationMessage ?? (owner.Pending ? T("Pending") : base.GetItemStatusCore());
        protected override string? GetHelpTextCore() => owner.ValidationMessage ?? base.GetHelpTextCore();
        protected override void OwnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            base.OwnerPropertyChanged(sender, e);
            if (e.Property == ValidationMessageProperty) RaisePropertyChangedEvent(AutomationElementIdentifiers.ItemStatusProperty, e.OldValue, e.NewValue);
        }
    }

    internal static ControlTheme CreateTheme()
    {
        var theme = new ControlTheme(typeof(CharacterGridField)) { BasedOn = TextBoxTheme.Create() };
        theme.Setters.Add(Set(TemplateProperty, new FuncControlTemplate<CharacterGridField>((field, names) =>
        {
            var background = new CellSurface(field, false) { IsHitTestVisible = false }.Named("PART_CellSurface", names);
            var foreground = new CellSurface(field, true) { IsHitTestVisible = false }.Named("PART_CellLines", names);
            var presenter = new GridPresenter(field);
            var editor = TextBoxTheme.CreateTemplate(field, names, presenter);
            presenter.SelectionBrush = Brushes.Transparent; // CellSurface paints whole-cell selection while the native caret follows the glyph inset.
            return new Panel { Children = { background, editor, foreground } };
        })));
        foreach (var setter in new[] { Set(FontFamilyProperty, Mono), Set(FontSizeProperty, TextSize.Label), Set(FontFeaturesProperty, NoLigatures),
            Set(PaddingProperty, new Thickness(0)), Set(MinHeightProperty, 0d), Set(HorizontalAlignmentProperty, HorizontalAlignment.Left), Set(CornerRadiusProperty, new CornerRadius(Radius)),
            Set(CellSizeProperty, new Size(M(20), Math.Max(1, ControlHeight - 2))), Set(CellBorderBrushProperty, B(Look.Separator)), Set(FocusBorderBrushProperty, B(FocusBorder)),
            Set(FrameBrushProperty, new Binding(nameof(BorderBrush)) { RelativeSource = new RelativeSource(RelativeSourceMode.Self) }),
            Set(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Hidden), Set(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled) }) theme.Setters.Add(setter);
        theme.State(":pointerover", Set(FrameBrushProperty, B(Outline)));
        theme.State(":focus", Set(FrameBrushProperty, new Binding(nameof(FocusBorderBrush)) { RelativeSource = new RelativeSource(RelativeSourceMode.Self) }));
        theme.State(":pending", Set(FrameBrushProperty, B(Warn)));
        theme.State(":invalid", Set(FrameBrushProperty, B(Danger)));
        theme.State(":disabled", Set(FrameBrushProperty, B(Look.Separator)));
        theme.Style(s => s.Nesting().PropertyEquals(AppearanceProperty, CharacterGridAppearance.Paper),
            Set(CellSizeProperty, new Size(M(16), M(20))), Set(CornerRadiusProperty, new CornerRadius(0)));
        return theme;
    }

    sealed class CellSurface(CharacterGridField field, bool rules) : Control
    {
        public override void Render(DrawingContext context)
        {
            var size = field.Bounds.Size;
            var origin = field.GridOrigin;
            var viewport = new Rect(origin, new Size(field.CellCount * field.CellSize.Width + (field.CellCount - 1) * field.CellGap, field.CellSize.Height));
            double offset = field.scroll?.Offset.X ?? 0;
            int first = Math.Max(0, (int)(offset / field.Pitch));
            string text = field.Text ?? ""; var starts = StringInfo.ParseCombiningCharacters(text);
            int last = Math.Max(field.CellCount, starts.Length);
            last = Math.Min(last, first + field.CellCount + 2);
            double X(int cell) => origin.X + cell * field.Pitch - (cell >= field.CellCount ? field.CellGap : 0) - offset;
            if (!rules)
            {
                context.DrawRectangle(field.Background, null, new RoundedRect(new Rect(size), field.CornerRadius));
                using var clip = context.PushClip(viewport);
                for (int i = first; i < last; i++)
                {
                    if (field.Decoration(i, d => d.Background) is { } fill)
                        context.FillRectangle(fill, new Rect(X(i), origin.Y, field.CellSize.Width, field.CellSize.Height));
                    if (field.presenter?.ShowSelectionHighlight == true && i < starts.Length && starts[i] >= Math.Min(field.SelectionStart, field.SelectionEnd)
                        && (i + 1 < starts.Length ? starts[i + 1] : text.Length) <= Math.Max(field.SelectionStart, field.SelectionEnd) && field.SelectionBrush is { } selection)
                        context.FillRectangle(selection, new Rect(X(i), origin.Y, field.CellSize.Width, field.CellSize.Height));
                }
                return;
            }
            using (context.PushClip(viewport))
                for (int i = first; field.CellBorderThickness > 0 && i < last; i++)
                {
                    var pen = new Pen(field.Decoration(i, d => d.BorderBrush) ?? field.CellBorderBrush, field.CellBorderThickness);
                    double x = X(i), bottom = origin.Y + field.CellSize.Height;
                    double top = origin.Y;
                    if (i > 0 || field.CellGap > 0) context.DrawLine(pen, new Point(x, top), new Point(x, bottom));
                    if (field.CellGap > 0) context.DrawLine(pen, new Point(x + field.CellSize.Width, top), new Point(x + field.CellSize.Width, bottom));
                }
            var brush = field.GetValue(FrameBrushProperty);
            var thickness = field.BorderThickness;
            double width = thickness.Left;
            var frame = new Pen(brush, width);
            var box = new Rect(width / 2, field.BorderThickness.Top / 2, Math.Max(0, size.Width - width), Math.Max(0, size.Height - field.BorderThickness.Top));
            if (!thickness.IsUniform)
            {
                double top = 0;
                if (thickness.Bottom > 0) context.DrawLine(new Pen(brush, thickness.Bottom), new Point(0, size.Height - thickness.Bottom / 2), new Point(size.Width, size.Height - thickness.Bottom / 2));
                if (thickness.Top > 0) context.DrawLine(new Pen(brush, thickness.Top), new Point(0, thickness.Top / 2), new Point(size.Width, thickness.Top / 2));
                if (thickness.Left > 0) context.DrawLine(new Pen(brush, thickness.Left), new Point(thickness.Left / 2, top), new Point(thickness.Left / 2, size.Height));
                if (thickness.Right > 0) context.DrawLine(new Pen(brush, thickness.Right), new Point(size.Width - thickness.Right / 2, top), new Point(size.Width - thickness.Right / 2, size.Height));
            }
            else if (width > 0) context.DrawRectangle(null, frame, new RoundedRect(box, field.CornerRadius));
        }
    }
}

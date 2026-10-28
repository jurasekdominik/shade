using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Data;
using static Shade.Look;

namespace Shade;

/// <summary>A native filter toggle with optional count and status dot.</summary>
public sealed class FilterChip : ToggleButton
{
    public static readonly StyledProperty<string?> CountProperty = AvaloniaProperty.Register<FilterChip, string?>(nameof(Count));
    public static readonly StyledProperty<Tone?> CountColorProperty = AvaloniaProperty.Register<FilterChip, Tone?>(nameof(CountColor));
    public static readonly StyledProperty<Tone?> DotProperty = AvaloniaProperty.Register<FilterChip, Tone?>(nameof(Dot));
    public string? Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }
    public Tone? CountColor { get => GetValue(CountColorProperty); set => SetValue(CountColorProperty, value); }
    public Tone? Dot { get => GetValue(DotProperty); set => SetValue(DotProperty, value); }
    protected override Type StyleKeyOverride => typeof(FilterChip);
    TextBlock? count;
    Avalonia.Controls.Shapes.Ellipse? dot;
    public FilterChip() { Cursor = Build.Hand; FocusAdorner = null; }
    public FilterChip(string text, Tone? dot = null) : this() { Content = text; Dot = dot; }
    public void Set(string text, Tone color) { Count = text; CountColor = color; }
    internal Control CreateTemplate()
    {
        var label = new ContentPresenter(); label.Bind(ContentPresenter.ContentProperty, new TemplateBinding(ContentProperty));
        label.Bind(ContentPresenter.ContentTemplateProperty, new TemplateBinding(ContentTemplateProperty));
        count = Build.Text("", Text2, TextSize.Label, 500); count.Margin = new Thickness(M(6), 0, 0, 0);
        dot = new Avalonia.Controls.Shapes.Ellipse { Width = M(8), Height = M(8), Margin = new Thickness(0, 0, M(6), 0), VerticalAlignment = VerticalAlignment.Center };
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { dot, label, count } };
        var surface = new Border { Child = row };
        surface.Bind(Border.BackgroundProperty, new TemplateBinding(BackgroundProperty)); surface.Bind(Border.BorderBrushProperty, new TemplateBinding(BorderBrushProperty));
        surface.Bind(Border.BorderThicknessProperty, new TemplateBinding(BorderThicknessProperty)); surface.Bind(Border.CornerRadiusProperty, new TemplateBinding(CornerRadiusProperty)); surface.Bind(Border.PaddingProperty, new TemplateBinding(PaddingProperty));
        Refresh(); return surface;
    }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CountProperty || change.Property == CountColorProperty || change.Property == DotProperty) Refresh();
    }
    void Refresh()
    {
        if (count != null) { count.Text = Count; count.IsVisible = !string.IsNullOrEmpty(Count); count.Foreground = B(CountColor ?? Text2); }
        if (dot != null) { dot.IsVisible = Dot != null; dot.Fill = B(Dot ?? Text2); }
    }
}

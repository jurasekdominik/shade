using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Shade;

/// <summary>Optional indicator settings for Shade's native CheckBox theme. Surface, border and corners use inherited properties.</summary>
public sealed class CheckBoxAppearance : AvaloniaObject
{
    private CheckBoxAppearance() { }
    public static readonly AttachedProperty<double> IndicatorSizeProperty = AvaloniaProperty.RegisterAttached<CheckBoxAppearance, CheckBox, double>("IndicatorSize", 16, validate: n => double.IsFinite(n) && n > 0);
    public static readonly AttachedProperty<IBrush?> CheckedBackgroundProperty = AvaloniaProperty.RegisterAttached<CheckBoxAppearance, CheckBox, IBrush?>("CheckedBackground");
    public static readonly AttachedProperty<IBrush?> MarkBrushProperty = AvaloniaProperty.RegisterAttached<CheckBoxAppearance, CheckBox, IBrush?>("MarkBrush");
    /// <summary>Scales the mark about the centre of the box (1 is the normal size). Marks and box are drawn on a 16 x 16 grid, so up to about 1.9 stays inside the box.</summary>
    public static readonly AttachedProperty<double> MarkScaleProperty = AvaloniaProperty.RegisterAttached<CheckBoxAppearance, CheckBox, double>("MarkScale", 1, validate: n => double.IsFinite(n) && n > 0 && n <= 2);
    public static readonly AttachedProperty<IBrush?> FocusBorderBrushProperty = AvaloniaProperty.RegisterAttached<CheckBoxAppearance, CheckBox, IBrush?>("FocusBorderBrush");
    public static readonly AttachedProperty<double> DisabledOpacityProperty = AvaloniaProperty.RegisterAttached<CheckBoxAppearance, CheckBox, double>("DisabledOpacity", .4, validate: n => double.IsFinite(n) && n >= 0 && n <= 1);
    public static double GetIndicatorSize(CheckBox box) => box.GetValue(IndicatorSizeProperty);
    public static void SetIndicatorSize(CheckBox box, double value) => box.SetValue(IndicatorSizeProperty, value);
    public static IBrush? GetCheckedBackground(CheckBox box) => box.GetValue(CheckedBackgroundProperty);
    public static void SetCheckedBackground(CheckBox box, IBrush? value) => box.SetValue(CheckedBackgroundProperty, value);
    public static IBrush? GetMarkBrush(CheckBox box) => box.GetValue(MarkBrushProperty);
    public static void SetMarkBrush(CheckBox box, IBrush? value) => box.SetValue(MarkBrushProperty, value);
    public static double GetMarkScale(CheckBox box) => box.GetValue(MarkScaleProperty);
    public static void SetMarkScale(CheckBox box, double value) => box.SetValue(MarkScaleProperty, value);
    public static IBrush? GetFocusBorderBrush(CheckBox box) => box.GetValue(FocusBorderBrushProperty);
    public static void SetFocusBorderBrush(CheckBox box, IBrush? value) => box.SetValue(FocusBorderBrushProperty, value);
    public static double GetDisabledOpacity(CheckBox box) => box.GetValue(DisabledOpacityProperty);
    public static void SetDisabledOpacity(CheckBox box, double value) => box.SetValue(DisabledOpacityProperty, value);
}

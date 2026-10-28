using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using static Shade.Look;

namespace Shade;

/// <summary>The theme of Avalonia's ProgressBar: a 6 px pill, the track in Fill and its part in the accent, both with round ends
/// (the part is never shorter than its height). Unknown length (IsIndeterminate) slides a segment across; the class "countdown"
/// empties the part in Text 3 toward the next event.</summary>
internal static class ProgressBarTheme
{
    /// <summary>The sliding segment stays put (renders for the docs and checks compare pixels).</summary>
    internal static bool Still;
    static Setter Set(AvaloniaProperty property, object? value) => new(property, value);
    static void Style(ControlTheme theme, Func<Selector?, Selector> selector, params Setter[] setters)
    {
        var style = new Style(selector); foreach (var setter in setters) style.Setters.Add(setter); theme.Children.Add(style);
    }

    internal static ControlTheme Create()
    {
        double h = M(6), r = h / 2;
        var theme = new ControlTheme(typeof(ProgressBar));
        foreach (var setter in new[] {
            Set(TemplatedControl.TemplateProperty, new FuncControlTemplate<ProgressBar>((_, names) => CreateTemplate(names, h, r))),
            Set(Layoutable.HeightProperty, h), Set(Layoutable.MinHeightProperty, 0d), Set(Layoutable.MinWidthProperty, 0d),
            Set(Layoutable.VerticalAlignmentProperty, VerticalAlignment.Center) })
            theme.Setters.Add(setter);
        Style(theme, x => x.Nesting().Template().OfType<Border>().Name("PART_Track"), Set(Border.BackgroundProperty, B(Fill)));
        Style(theme, x => x.Nesting().Template().OfType<Border>().Name("PART_Indicator"), Set(Border.BackgroundProperty, B(Accent)), Set(Layoutable.MinWidthProperty, h));  // a part never shorter than its height
        Style(theme, x => x.Nesting().Template().OfType<Border>().Name("PART_StillSegment"), Set(Border.BackgroundProperty, B(Accent)));
        Style(theme, x => x.Nesting().Class("countdown").Template().OfType<Border>().Name("PART_StillSegment"), Set(Border.BackgroundProperty, B(Text3)));
        Style(theme, x => x.Nesting().Template().OfType<Grid>().Name("PART_Still"), Set(Visual.IsVisibleProperty, false));
        Style(theme, x => x.Nesting().Class(":indeterminate").Template().OfType<Border>().Name("PART_Indicator"), Set(Layoutable.MinWidthProperty, 0d));
        Style(theme, x => x.Nesting().Class("countdown").Template().OfType<Border>().Name("PART_Indicator"), Set(Border.BackgroundProperty, B(Text3)));
        // unknown length: Avalonia sizes the container (0.4 of the bar) as TemplateSettings.ContainerWidth; the segment is 28 % of the
        // bar, eased across and off the end, then again (a still segment for the docs and for reduced motion)
        static Binding Of(Func<double, double> map) => new("TemplateSettings.ContainerWidth") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent), Converter = new FuncValueConverter<double, double>(map) };
        if (Still || ReducedMotion)
        {
            // a still segment at 26 % to 54 % of the bar (the sliding one's pose at 45 % of its way), in proportional columns
            Style(theme, x => x.Nesting().Class(":indeterminate").Template().OfType<Border>().Name("PART_Indicator"), Set(Visual.IsVisibleProperty, false));
            Style(theme, x => x.Nesting().Class(":indeterminate").Template().OfType<Grid>().Name("PART_Still"), Set(Visual.IsVisibleProperty, true));
        }
        else
        {
            var segment = new Style(x => x.Nesting().Class(":indeterminate").Template().OfType<Border>().Name("PART_Indicator"));
            segment.Setters.Add(new Setter(Layoutable.WidthProperty, Of(c => c * 0.7)));
            var slide = new Animation { Duration = TimeSpan.FromMilliseconds(1400), IterationCount = IterationCount.Infinite, Easing = new SineEaseInOut() };
            var from = new KeyFrame { Cue = new Cue(0) }; from.Setters.Add(new Setter(TranslateTransform.XProperty, Of(c => c * -0.7)));
            var to = new KeyFrame { Cue = new Cue(1) }; to.Setters.Add(new Setter(TranslateTransform.XProperty, Of(c => c * 2.5)));
            slide.Children.Add(from); slide.Children.Add(to); segment.Animations.Add(slide);
            theme.Children.Add(segment);
        }
        return theme;
    }

    static Control CreateTemplate(INameScope names, double h, double r)
    {
        var indicator = new Border { Name = "PART_Indicator", HorizontalAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(r), RenderTransform = new TranslateTransform() };
        names.Register("PART_Indicator", indicator);
        var segment = new Border { Name = "PART_StillSegment", CornerRadius = new CornerRadius(r), [Grid.ColumnProperty] = 1 }; names.Register("PART_StillSegment", segment);
        var still = new Grid { Name = "PART_Still", ColumnDefinitions = new ColumnDefinitions("26*,28*,46*"), Children = { segment } }; names.Register("PART_Still", still);
        var track = new Border { Name = "PART_Track", CornerRadius = new CornerRadius(r), ClipToBounds = true, Child = new Panel { Children = { indicator, still } } };
        names.Register("PART_Track", track);
        return track;
    }
}

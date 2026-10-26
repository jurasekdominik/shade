using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using static Shade.Look;

namespace Shade;

internal static class SearchChoiceTheme
{
    internal static ControlTheme Create()
    {
        var theme = new ControlTheme(typeof(SearchChoice));
        theme.Setters.Add(new Setter(TemplatedControl.TemplateProperty, new FuncControlTemplate<SearchChoice>((picker, names) => picker.CreateTemplate(names))));
        theme.Setters.Add(new Setter(Layoutable.HeightProperty, ControlHeight)); theme.Setters.Add(new Setter(Layoutable.MinHeightProperty, 0d)); theme.Setters.Add(new Setter(Layoutable.MinWidthProperty, 0d));
        theme.Setters.Add(new Setter(TemplatedControl.PaddingProperty, new Thickness(M(8), 0)));
        theme.Setters.Add(new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(1))); theme.Setters.Add(new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(Radius)));
        theme.Setters.Add(new Setter(TemplatedControl.BackgroundProperty, B(Raised))); theme.Setters.Add(new Setter(TemplatedControl.BorderBrushProperty, B(Look.Separator))); theme.Setters.Add(new Setter(TemplatedControl.ForegroundProperty, B(Look.Text)));
        theme.Setters.Add(new Setter(TemplatedControl.FontWeightProperty, FontWeight.Normal));
        theme.Setters.Add(new Setter(TemplatedControl.FontFamilyProperty, UiFont)); theme.Setters.Add(new Setter(TemplatedControl.FontSizeProperty, TextSize.Body));
        theme.Setters.Add(new Setter(ComboBox.MaxDropDownHeightProperty, M(324)));
        theme.Setters.Add(new Setter(InputElement.CursorProperty, Build.Hand));
        State(":pointerover", new Setter(TemplatedControl.BorderBrushProperty, B(Outline)));
        State(":focus-visible", new Setter(TemplatedControl.BorderBrushProperty, B(FocusBorder)));
        State(":dropdownopen", new Setter(TemplatedControl.BorderBrushProperty, B(Outline)), new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(Radius, Radius, 0, 0)), new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(1, 1, 1, 0)), new Setter(TemplatedControl.PaddingProperty, new Thickness(M(8), 0, M(8), 1)));
        State(":disabled", new Setter(TemplatedControl.ForegroundProperty, B(Disabled)));
        void State(string state, params Setter[] setters) { var style = new Style(x => x.Nesting().Class(state)); foreach (var setter in setters) style.Setters.Add(setter); theme.Children.Add(style); }
        return theme;
    }
    internal static ControlTheme ResultItem()
    {
        var theme = new ControlTheme(typeof(ListBoxItem));
        theme.Setters.Add(new Setter(TemplatedControl.TemplateProperty, new FuncControlTemplate<ListBoxItem>((_, _) =>
        {
            var presenter = new ContentPresenter(); presenter.Bind(ContentPresenter.ContentProperty, new TemplateBinding(ContentControl.ContentProperty)); presenter.Bind(ContentPresenter.ContentTemplateProperty, new TemplateBinding(ContentControl.ContentTemplateProperty));
            var surface = new Border { Child = presenter }; surface.Bind(Border.BackgroundProperty, new TemplateBinding(TemplatedControl.BackgroundProperty)); return surface;
        })));
        theme.Setters.Add(new Setter(Layoutable.MinHeightProperty, ControlHeight)); theme.Setters.Add(new Setter(TemplatedControl.PaddingProperty, new Thickness(0))); theme.Setters.Add(new Setter(InputElement.FocusableProperty, false));
        theme.Setters.Add(new Setter(TemplatedControl.BackgroundProperty, Brushes.Transparent));
        foreach (var state in new[] { ":pointerover", ":selected" }) theme.Children.Add(new Style(x => x.Nesting().Class(state)) { Setters = { new Setter(TemplatedControl.BackgroundProperty, B(Fill)) } });
        return theme;
    }
}

// Shade - selectable reference code that grows with its content and shares the document's scrolling.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using static Shade.Look;

namespace Shade
{
    public sealed class InlineCode : StackPanel
    {
        readonly SelectableTextBlock code;
        public string Text => code.Text ?? "";
        public InlineCode(string text, string copyLabel = "Copy example")
        {
            Spacing = Kit.Gap;
            code = new SelectableTextBlock
            {
                Text = text,
                FontFamily = Mono,
                FontSize = Look.Label,
                Foreground = B(Look.Text),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = Kit.Pad,
                SelectionBrush = B(Alpha(Accent, .45)),
                SelectionForegroundBrush = B(Look.Text)
            };
            Children.Add(new Border
            {
                Child = code,
                Background = B(Surface),
                BorderBrush = B(Look.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(Kit.Gap)
            });
            if (copyLabel == null) return;
            var copy = new Btn(copyLabel) { HorizontalAlignment = HorizontalAlignment.Left };
            copy.Click += async () =>
            {
                try
                {
                    var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                    if (clipboard != null) { await clipboard.SetTextAsync(Text); copy.Text = "Copied"; }
                }
                catch { copy.Text = "Copy unavailable"; }
            };
            Children.Add(copy);
        }
    }
}

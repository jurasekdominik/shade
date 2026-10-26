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
            Spacing = Build.Gap;
            code = new SelectableTextBlock
            {
                Text = text,
                FontFamily = Mono,
                FontSize = TextSize.Label,
                Foreground = B(Look.Text),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = Build.Pad,
                SelectionBrush = B(Selection),
                SelectionForegroundBrush = B(Look.Text)
            };
            Children.Add(new Border
            {
                Child = code,
                Background = B(Raised),
                BorderBrush = B(Look.Separator),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(Build.Gap)
            });
            if (copyLabel == null) return;
            string copyState = copyLabel;
            var copy = Words(new ActionButton(T(copyState)) { HorizontalAlignment = HorizontalAlignment.Left }, button => { button.Disarm(); button.Content = T(copyState); });
            copy.Click += async (_, _) =>
            {
                try
                {
                    var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                    if (clipboard != null) { await clipboard.SetTextAsync(Text); copyState = "Copied"; copy.Content = T(copyState); }
                }
                catch { copyState = "Copy unavailable"; copy.Content = T(copyState); }
            };
            Children.Add(copy);
        }
    }
}

// Shade - the search field: a field with a magnifier, a clear button once typed, Esc to clear, an optional count.
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using static Shade.Look;

namespace Shade
{
    /// <summary>A search box (DESIGN_SYSTEM.md §6 Text field, Search): a 16 px magnifier in front, × at the end once something
    /// is typed (a click or Esc clears it), and an optional count before it ("3 of 12"). Edited fires on every change, a clear
    /// included.</summary>
    public sealed class SearchField : Field
    {
        readonly Avalonia.Controls.Shapes.Path glass = Build.Icon24(Icons.Search, 16);
        readonly TextBlock count = Build.Text("", Text3, TextSize.Label);
        readonly Border clear;

        public SearchField() : this("Search") { }

        public SearchField(string placeholder) : base(placeholder, false)
        {
            glass.Stroke = B(Text3); glass.Margin = new Thickness(0, 0, 6, 0); glass.VerticalAlignment = VerticalAlignment.Center;
            var x = Build.Icon24(Icons.X, 14, 2); x.Stroke = B(Text3);
            clear = new Border
            {
                Child = x,
                Width = 20,
                Height = 20,
                Background = Brushes.Transparent,
                Cursor = Build.Hand,
                IsVisible = false,
                CornerRadius = new CornerRadius(Radius),
                Margin = new Thickness(4, 0, -4, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(clear, "Clear");
            clear.PointerEntered += (s, e) => x.Stroke = B(Look.Text);
            clear.PointerExited += (s, e) => x.Stroke = B(Text3);
            clear.PointerPressed += (s, e) => { e.Handled = true; Clear(); Box.Focus(); };
            count.Margin = new Thickness(6, 0, 0, 0);
            var d = new DockPanel();
            DockPanel.SetDock(glass, Dock.Left); DockPanel.SetDock(clear, Dock.Right); DockPanel.SetDock(count, Dock.Right);
            d.Children.Add(glass); d.Children.Add(clear); d.Children.Add(count); d.Children.Add(Box);
            Child = d;
            Box.TextChanged += (s, e) => Show();
            Box.AddHandler(KeyDownEvent, (s, e) => { if (e.Key == Key.Escape && Text != "") { e.Handled = true; Clear(); } }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        }

        /// <summary>The matches, shown before the ×: "3 of 12"; null hides it.</summary>
        public string? Count { get => count.Text == "" ? null : count.Text; set { count.Text = value ?? ""; Show(); } }

        void Clear() { Box.Text = ""; RaiseEdited(); }
        void Show() { bool any = Text != ""; clear.IsVisible = any; count.IsVisible = any && count.Text != ""; }
    }
}

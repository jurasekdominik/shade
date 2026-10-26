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
        readonly Avalonia.Controls.Shapes.Path glass = Build.Icon24(Icons.Search, M(16));
        readonly TextBlock count = Build.Text("", Text3, TextSize.Label);
        readonly IconButton clear;
        public static readonly StyledProperty<string?> CountProperty = AvaloniaProperty.Register<SearchField, string?>(nameof(Count));

        public SearchField() : this("")
        {
            string? previous = null;
            Words(this, field => { if (previous == null || field.PlaceholderText == previous) { previous = T("Search"); field.PlaceholderText = previous; } }, "placeholder");
        }

        public SearchField(string placeholder) : base(placeholder, false)
        {
            glass.Stroke = B(Text3); glass.Margin = new Thickness(0, 0, M(6), 0); glass.VerticalAlignment = VerticalAlignment.Center;
            clear = new IconButton { Icon = Icons.X, IconSize = M(14), IconColor = Text3, Width = M(20), Height = M(20), MinWidth = 0, IsVisible = false, Margin = new Thickness(M(4), 0, -M(4), 0), VerticalAlignment = VerticalAlignment.Center };
            Words(clear, word => { ToolTip.SetTip(word, T("Clear")); Avalonia.Automation.AutomationProperties.SetName(word, T("Clear search")); });
            clear.PointerEntered += (_, _) => clear.IconColor = Look.Text; clear.PointerExited += (_, _) => clear.IconColor = Text3;
            clear.Click += (_, _) => { Clear(); Box.Focus(); };
            count.Margin = new Thickness(M(6), 0, 0, 0);
            var d = new DockPanel();
            DockPanel.SetDock(glass, Dock.Left); DockPanel.SetDock(clear, Dock.Right); DockPanel.SetDock(count, Dock.Right);
            d.Children.Add(glass); d.Children.Add(clear); d.Children.Add(count); d.Children.Add(Box);
            Child = d;
            Box.TextChanged += (s, e) => Show();
            Box.AddHandler(KeyDownEvent, (s, e) => { if (e.Key == Key.Escape && Text != "") { e.Handled = true; Clear(); } }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        }

        /// <summary>The matches, shown before the ×: "3 of 12"; null hides it.</summary>
        public string? Count { get => GetValue(CountProperty); set => SetValue(CountProperty, value); }
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == IsReadOnlyProperty && clear != null) Show();
            if (change.Property == CountProperty && count != null) { count.Text = Count; Show(); }
        }

        void Clear() { if (!IsReadOnly && IsEffectivelyEnabled) Box.Text = ""; }
        void Show() { clear.IsEnabled = !IsReadOnly; bool any = Text != ""; clear.IsVisible = any; count.IsVisible = any && count.Text != ""; }
    }
}

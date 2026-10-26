// Shade - global navigation arrows in the sidebar footer, using the shared ghost button.
using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using static Shade.Look;

namespace Shade
{
    public sealed class NavigationButtons : StackPanel
    {
        readonly ActionButton back = new ActionButton("", ButtonKind.Ghost, icon: Icons.ChevronLeft), forward = new ActionButton("", ButtonKind.Ghost, icon: Icons.ChevronRight);
        public static readonly StyledProperty<bool> CanGoBackProperty = AvaloniaProperty.Register<NavigationButtons, bool>(nameof(CanGoBack));
        public static readonly StyledProperty<bool> CanGoForwardProperty = AvaloniaProperty.Register<NavigationButtons, bool>(nameof(CanGoForward));
        public static readonly StyledProperty<bool> CollapsedProperty = AvaloniaProperty.Register<NavigationButtons, bool>(nameof(Collapsed));
        public static readonly StyledProperty<ICommand?> BackCommandProperty = AvaloniaProperty.Register<NavigationButtons, ICommand?>(nameof(BackCommand));
        public static readonly StyledProperty<ICommand?> ForwardCommandProperty = AvaloniaProperty.Register<NavigationButtons, ICommand?>(nameof(ForwardCommand));
        public bool CanGoBack { get => GetValue(CanGoBackProperty); set => SetValue(CanGoBackProperty, value); }
        public bool CanGoForward { get => GetValue(CanGoForwardProperty); set => SetValue(CanGoForwardProperty, value); }
        public bool Collapsed { get => GetValue(CollapsedProperty); set => SetValue(CollapsedProperty, value); }
        public ICommand? BackCommand { get => GetValue(BackCommandProperty); set => SetValue(BackCommandProperty, value); }
        public ICommand? ForwardCommand { get => GetValue(ForwardCommandProperty); set => SetValue(ForwardCommandProperty, value); }
        public event Action? Back, Forward;
        public NavigationButtons()
        {
            Orientation = Orientation.Horizontal; Spacing = 8; HorizontalAlignment = HorizontalAlignment.Center;
            foreach (var button in new[] { back, forward }) { button.Width = 28; button.IsEnabled = false; Children.Add(button); }
            Words(back, word => Avalonia.Automation.AutomationProperties.SetName(word, T("Back")), "name"); Words(forward, word => Avalonia.Automation.AutomationProperties.SetName(word, T("Forward")), "name");
            Words(back, word => ToolTip.SetTip(word, T("Back · Alt+Left · mouse back button")), "tip"); Words(forward, word => ToolTip.SetTip(word, T("Forward · Alt+Right · mouse forward button")), "tip");
            back[!IsEnabledProperty] = this[!CanGoBackProperty]; forward[!IsEnabledProperty] = this[!CanGoForwardProperty];
            back[!Button.CommandProperty] = this[!BackCommandProperty]; forward[!Button.CommandProperty] = this[!ForwardCommandProperty];
            back.Click += (_, _) => Back?.Invoke(); forward.Click += (_, _) => Forward?.Invoke();
        }
        public void Update(bool canBack, bool canForward) { SetCurrentValue(CanGoBackProperty, canBack); SetCurrentValue(CanGoForwardProperty, canForward); }
        public void Collapse(bool collapsed) => SetCurrentValue(CollapsedProperty, collapsed);
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == CollapsedProperty) Orientation = Collapsed ? Orientation.Vertical : Orientation.Horizontal;
        }
    }
}

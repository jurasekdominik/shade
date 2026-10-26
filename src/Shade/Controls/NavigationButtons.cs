// Shade - global navigation arrows in the sidebar footer, using the shared ghost button.
using System;
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
        public event Action? Back, Forward;
        public NavigationButtons()
        {
            Orientation = Orientation.Horizontal; Spacing = 8; HorizontalAlignment = HorizontalAlignment.Center;
            foreach (var button in new[] { back, forward }) { button.Width = 28; button.IsEnabled = false; Children.Add(button); }
            Words(back, word => Avalonia.Automation.AutomationProperties.SetName(word, T("Back")), "name"); Words(forward, word => Avalonia.Automation.AutomationProperties.SetName(word, T("Forward")), "name");
            Words(back, word => ToolTip.SetTip(word, T("Back · Alt+Left · mouse back button")), "tip"); Words(forward, word => ToolTip.SetTip(word, T("Forward · Alt+Right · mouse forward button")), "tip");
            back.Click += (_, _) => Back?.Invoke(); forward.Click += (_, _) => Forward?.Invoke();
        }
        public void Update(bool canBack, bool canForward) { back.IsEnabled = canBack; forward.IsEnabled = canForward; }
        public void Collapse(bool collapsed) => Orientation = collapsed ? Orientation.Vertical : Orientation.Horizontal;
    }
}

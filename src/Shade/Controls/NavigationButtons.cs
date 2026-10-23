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
        readonly ActionButton back = new ActionButton("", ButtonKind.Ghost, icon: Icons.ChevronLeft), forward = new ActionButton("", ButtonKind.Ghost, icon: Icons.ChevronLeft);
        public event Action? Back, Forward;
        public NavigationButtons()
        {
            Orientation = Orientation.Horizontal; Spacing = 8; HorizontalAlignment = HorizontalAlignment.Center;
            forward.Child!.RenderTransformOrigin = new RelativePoint(.5, .5, RelativeUnit.Relative);
            forward.Child.RenderTransform = new RotateTransform(180);
            foreach (var button in new[] { back, forward }) { button.Width = 28; button.IsEnabled = false; Children.Add(button); }
            Avalonia.Automation.AutomationProperties.SetName(back, "Back"); Avalonia.Automation.AutomationProperties.SetName(forward, "Forward");
            ToolTip.SetTip(back, "Back · Alt+Left · mouse back button"); ToolTip.SetTip(forward, "Forward · Alt+Right · mouse forward button");
            back.Click += () => Back?.Invoke(); forward.Click += () => Forward?.Invoke();
        }
        public void Update(bool canBack, bool canForward) { back.IsEnabled = canBack; forward.IsEnabled = canForward; }
        public void Collapse(bool collapsed) => Orientation = collapsed ? Orientation.Vertical : Orientation.Horizontal;
    }
}

// Shade - the tray menu: the system tray icon's right-click menu in the app's own look.
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Shade
{
    /// <summary>The tray's right-click menu (DESIGN.md): the shared menu card in a borderless window by the
    /// pointer, every item at its natural height (no scrolling), closed by a pick, Esc or a click anywhere else. Show it from
    /// the TrayIcon's click with the pointer's screen position.</summary>
    public sealed class TrayMenu : Window
    {
        static TrayMenu? open;

        TrayMenu(IList<string> items, Action<int> picked, int current)
        {
            WindowDecorations = WindowDecorations.None;
            ShowInTaskbar = false; Topmost = true; CanResize = false;
            Background = Brushes.Transparent;
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
            var card = Menu.Panel(items, picked, Close, current, 220, scrollable: false);
            Content = card;
            card.Measure(Size.Infinity);
            Width = card.DesiredSize.Width; Height = card.DesiredSize.Height;
            Deactivated += (s, e) => Close();
            KeyDown += (s, e) => { if (e.Key == Key.Escape) Close(); };
            Closed += (s, e) => { if (open == this) open = null; };
        }

        /// <summary>Opens the menu with its corner at the pointer (x, y in screen pixels), kept on the screen's work area: up and
        /// to the left, as the tray sits at the bottom right. current: the highlighted item (the page shown), or -1.</summary>
        public static void Show(int x, int y, IList<string> items, Action<int> picked, int current = -1)
        {
            open?.Close();
            var m = open = new TrayMenu(items, picked, current);
            var at = new PixelPoint(x, y);
            var screen = m.Screens.ScreenFromPoint(at) ?? m.Screens.Primary;
            double scale = screen?.Scaling ?? 1;
            int w = (int)Math.Ceiling(m.Width * scale), h = (int)Math.Ceiling(m.Height * scale);
            var area = screen?.WorkingArea ?? new PixelRect(0, 0, x + w, y + h);
            int left = Math.Clamp(x - w >= area.X ? x - w : x, area.X, area.Right - w);
            int top = Math.Clamp(y - h >= area.Y ? y - h : y, area.Y, area.Bottom - h);
            m.Position = new PixelPoint(left, top);
            m.Show();
            m.Activate();
        }
    }
}

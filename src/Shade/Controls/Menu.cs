// Shade - shared control: Menu, the dropdown and context menus.
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using Avalonia.Animation;
using Avalonia.Controls.Primitives;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media.Transformation;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia;
using Avalonia.VisualTree;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using static Shade.Look;

namespace Shade
{
    /// <summary>The menus: a rounded card of 28 px rows, the hovered one an accent-tinted fill, the current one in the accent.
    /// In a popup under a picker, at the pointer as a context menu, or in the tray's own window.</summary>
    public static class Menu
    {
        sealed class MenuCard : Border { internal Action<int>? Highlight; }
        internal static void Highlight(Border card, int current) => ((MenuCard)card).Highlight?.Invoke(current);
        internal static double RowHeight => ControlHeight;
        internal static double MaximumHeight => RowHeight * 10 + M(Build.Gap);
        /// <summary>An item that is a line between groups (not clickable).</summary>
        public const string Divider = "\u2014";
        /// <summary>Items that remove or cut something: red, and kept below a line. Told by the item as given, before
        /// Look.Translate: a translated "Delete" stays a danger item.</summary>
        static bool Danger_(string item) => item.StartsWith(DangerMark) || item.StartsWith("Remove", StringComparison.Ordinal) || item.StartsWith("Delete", StringComparison.Ordinal) || item.StartsWith("Disconnect", StringComparison.Ordinal);
        const char DangerMark = '\u2063';  // an invisible separator: never typed, stripped before the item shows
        /// <summary>An item that removes or cuts something, whatever its words ("Smazat", "Delete 3 rows" translated): red, below a line.</summary>
        public static string Danger(string item) => DangerMark + item;
        static string Shown(string item) => T(item.TrimStart(DangerMark));

        /// <summary>The menu's card (DESIGN_SYSTEM.md §6): 28 px rows whose text lines up with the field's, the current item highlighted,
        /// a colour per item when given, danger items last in red. Arrow keys move, Enter picks, Esc closes, a letter jumps.
        /// close runs before picked.</summary>
        /// <summary>attached: the open part of a dropdown (DESIGN_SYSTEM.md §6): at least the field's width, growing to fit every option; its side and bottom
        /// borders continuing the field's, a faint line between field and options.</summary>
        public static Border Panel(IList<string> items, Action<int> picked, Action close, int current = -1, double width = 0, Func<int, Tone?>? color = null, bool attached = false, bool scrollable = true) =>
            Panel(items, picked, close, current, width, color, attached, scrollable, null);

        /// <summary>mark: a query whose matches each row shows (a suggestion list, Build.Mark).</summary>
        internal static Border Panel(IList<string> items, Action<int> picked, Action close, int current, double width, Func<int, Tone?>? color, bool attached, bool scrollable, string? mark)
        {
            var rows = new StackPanel { Margin = new Thickness(0, attached ? 0 : 4, 0, 4) };
            if (attached) rows.Children.Add(new Border { Height = 1, Background = B(Look.Separator), Margin = new Thickness(0, 0, 0, 4) });
            var pickable = new List<(int index, Button row, TextBlock text, Color fg)>();
            int lit = -1;
            void Light(int k)
            {
                lit = k;
                foreach (var p in pickable) p.row.Background = p.index == k ? B(Fill) : Brushes.Transparent;
            }
            for (int i = 0; i < items.Count; i++)
            {
                int k = i;
                bool danger = Danger_(items[i]);
                if (items[i] == Divider || danger && i > 0 && items[i - 1] != Divider)  // groups apart; removing things always last, below a line
                {
                    rows.Children.Add(new Border { Height = 1, Background = B(Look.Separator), Margin = new Thickness(0, 4) });
                    if (items[i] == Divider) continue;
                }
                bool cur = i == current;
                var fg = danger ? DangerText : color?.Invoke(i) ?? Look.Text;
                var t = Words(Build.Text("", fg, TextSize.Body, cur ? 500 : 400), text => { text.Text = Shown(items[k]); if (mark != null) Build.Mark(text, text.Text, mark); });
                if (mark != null) Build.Mark(t, Shown(items[i]), mark);
                var surface = new Border { Padding = new Thickness(M(8), 0), Child = t };
                var row = new Button { Focusable = mark == null, Tag = k, Height = RowHeight, MinHeight = 0, MinWidth = 0, Padding = new Thickness(0), Background = Brushes.Transparent, HorizontalAlignment = HorizontalAlignment.Stretch, Cursor = Build.Hand, Template = new FuncControlTemplate<Button>((_, _) => surface) };
                surface.Bind(Border.BackgroundProperty, new Binding(nameof(Button.Background)) { Source = row });
                if (mark == null) FocusRing(row); Words(row, button => Avalonia.Automation.AutomationProperties.SetName(button, Shown(items[k])));
                row.GotFocus += (_, _) => Light(k);
                pickable.Add((k, row, t, fg));
                row.PointerEntered += (_, _) => { Light(k); row.Focus(NavigationMethod.Pointer); };
                row.Click += (_, _) => { close(); picked(k); };
                rows.Children.Add(row);
            }
            rows.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double naturalWidth = rows.DesiredSize.Width + 2 + (scrollable && rows.DesiredSize.Height > MaximumHeight ? Build.Pad : 0);
            width = Math.Max(width > 0 ? width : 160, naturalWidth);
            Control content = ScrollRows(rows, width, scrollable);
            var card = new MenuCard
            {
                Background = B(Raised),
                BorderBrush = B(attached ? Outline : Look.Separator),
                BorderThickness = attached ? new Thickness(1, 0, 1, 1) : new Thickness(1),
                CornerRadius = attached ? new CornerRadius(0, 0, 4, 4) : new CornerRadius(4),
                Child = content,
                MaxHeight = scrollable ? MaximumHeight + 2 : double.PositiveInfinity,
                VerticalAlignment = VerticalAlignment.Top,
                MinWidth = width > 0 ? width : 160,
                Focusable = true,
                ClipToBounds = true,
            };
            // Reveal rows inside the menu; embedded gallery cards must never scroll their host page.
            card.AddHandler(Control.RequestBringIntoViewEvent, (_, e) => e.Handled = true);
            card.Highlight = Light;
            if (attached && width > 0) card.Width = width;
            card.KeyDown += (s, e) =>
            {
                int at = pickable.FindIndex(p => p.index == lit);
                if (e.Key is Key.Down or Key.Up or Key.Home or Key.End) { int n = pickable.Count; if (n == 0) return; at = e.Key == Key.Home ? 0 : e.Key == Key.End ? n - 1 : at < 0 ? (e.Key == Key.Down ? 0 : n - 1) : (at + (e.Key == Key.Down ? 1 : n - 1)) % n; Light(pickable[at].index); pickable[at].row.Focus(NavigationMethod.Directional); pickable[at].row.BringIntoView(); e.Handled = true; }
                else if (e.Key == Key.Enter && card.IsFocused && at >= 0) { e.Handled = true; close(); picked(lit); }
                else if (e.Key == Key.Escape) { e.Handled = true; close(); }
            };
            card.TextInput += (s, e) =>
            {
                var c = (e.Text ?? "").Trim();
                if (c.Length == 0) return;
                int from = Math.Max(0, pickable.FindIndex(p => p.index == lit) + 1);
                var hit = pickable.Skip(from).Concat(pickable.Take(from)).FirstOrDefault(p => Shown(items[p.index]).StartsWith(c, StringComparison.OrdinalIgnoreCase));
                if (hit.row != null) { Light(hit.index); hit.row.Focus(NavigationMethod.Directional); hit.row.BringIntoView(); }
            };
            if (current >= 0 && pickable.Any(p => p.index == current))
            {
                Light(current);
                Dispatcher.UIThread.Post(() => pickable.First(p => p.index == current).row.BringIntoView(), DispatcherPriority.Loaded);
            }
            return card;
        }

        internal static Control ScrollRows(StackPanel rows, double width, bool scrollable = true)
        {
            rows.Measure(new Size(width > 0 ? width : double.PositiveInfinity, double.PositiveInfinity));
            Control content = rows;
            if (scrollable && rows.DesiredSize.Height > MaximumHeight) content = new ScrollViewer
            {
                Content = rows,
                Padding = new Thickness(0, 0, Build.Pad, 0),
                Height = Math.Min(MaximumHeight, rows.DesiredSize.Height),
                MaxHeight = MaximumHeight,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            return content;
        }

        internal static void OwnPopup(Popup popup, Control anchor)
        {
            var ancestors = anchor.GetVisualAncestors().OfType<Control>().Append(anchor).ToArray();
            EventHandler<AvaloniaPropertyChangedEventArgs> state = (_, e) => { if ((e.Property == Control.IsVisibleProperty || e.Property == Control.IsEffectivelyEnabledProperty) && (!anchor.IsEffectivelyVisible || !anchor.IsEffectivelyEnabled)) popup.IsOpen = false; };
            EventHandler<VisualTreeAttachmentEventArgs> detached = (_, _) => popup.IsOpen = false;
            foreach (var owner in ancestors) owner.PropertyChanged += state;
            anchor.DetachedFromVisualTree += detached;
            popup.Closed += (_, _) => { foreach (var owner in ancestors) owner.PropertyChanged -= state; anchor.DetachedFromVisualTree -= detached; };
        }

        public static Popup Show(Control anchor, IList<string> items, Action<int> picked, int current = -1, double width = 0, bool atPointer = false, Action? closed = null, Func<int, Tone?>? color = null, bool attached = false)
            => Show(anchor, items, picked, current, width, atPointer, closed, color, attached, !atPointer && anchor.Classes.Contains(":focus-visible") ? NavigationMethod.Tab : NavigationMethod.Pointer);

        internal static Popup Show(Control anchor, IList<string> items, Action<int> picked, int current, double width, bool atPointer, Action? closed, Func<int, Tone?>? color, bool attached, NavigationMethod navigation)
        {
            var pop = new Popup { PlacementTarget = anchor, Placement = atPointer ? PlacementMode.Pointer : PlacementMode.BottomEdgeAlignedLeft, VerticalOffset = atPointer || attached ? 0 : 2, IsLightDismissEnabled = true };
            bool restoreFocus = false;
            void Close()
            {
                restoreFocus = true;
                if (anchor.IsEffectivelyEnabled && anchor.IsEffectivelyVisible) anchor.Focus(navigation);
                pop.IsOpen = false;
            }
            var card = Panel(items, index => { if (anchor.IsEffectivelyEnabled && anchor.IsEffectivelyVisible) picked(index); }, Close, current, width, color, attached);
            pop.Child = card;
            OwnPopup(pop, anchor);
            card.AddHandler(InputElement.PointerPressedEvent, (_, _) => navigation = NavigationMethod.Pointer, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            card.AddHandler(InputElement.KeyDownEvent, (_, _) => navigation = NavigationMethod.Directional, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            card.AddHandler(InputElement.TextInputEvent, (_, _) => navigation = NavigationMethod.Directional, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            card.KeyDown += (_, e) =>
            {
                if (e.Key != Key.Tab) return;
                e.Handled = true; restoreFocus = false; pop.IsOpen = false;
                if (anchor.IsEffectivelyEnabled && anchor.IsEffectivelyVisible)
                {
                    anchor.Focus(NavigationMethod.Tab);
                    TopLevel.GetTopLevel(anchor)?.FocusManager?.TryMoveFocus(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? NavigationDirection.Previous : NavigationDirection.Next);
                }
            };
            pop.Opened += (_, _) => { var actions = card.GetVisualDescendants().OfType<Button>().ToArray(); var target = actions.FirstOrDefault(row => row.Tag is int index && index == current) ?? actions.FirstOrDefault(); if (target != null) { target.Focus(navigation); target.BringIntoView(); } else card.Focus(navigation); };
            pop.Closed += (_, _) =>
            {
                closed?.Invoke(); ((ISetLogicalParent)pop).SetParent(null);
                if (restoreFocus && anchor.IsEffectivelyEnabled && anchor.IsEffectivelyVisible && TopLevel.GetTopLevel(anchor) != null) anchor.Focus(navigation);
            };
            ((ISetLogicalParent)pop).SetParent(anchor); pop.IsOpen = true;
            return pop;
        }
    }
}

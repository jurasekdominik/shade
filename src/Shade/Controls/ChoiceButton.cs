// Shade - shared control: a button showing the chosen item; a click opens the items under it.
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
using static Shade.Look;

namespace Shade
{
    /// <summary>A button showing the chosen item; a click opens the items under it.</summary>
    public sealed class ChoiceButton : Border, IRestyle
    {
        List<string> items = new List<string>();
        readonly TextBlock text = Build.Text("", Look.Text);
        readonly Avalonia.Controls.Shapes.Path chevron = Build.Icon24(Icons.ChevronDown, M(16), 2), icon = Build.Icon24(Icons.Check, M(16), 2);
        bool hot, open, overridden, keyboardFocus;
        static Thickness Pad => new Thickness(M(8), 0, M(4), 0);
        /// <summary>Drawn as while its list is open (the gallery shows the open state without a popup).</summary>
        internal bool ShowOpen { set { open = value; Restyle(); } }
        string? extra;
        double optionWidth;
        public event Action<string>? Picked;
        public event Action? Opening;
        /// <summary>The last menu item (SetItems' extra, like "+ New project"): an action, not a choice.</summary>
        public event Action? ExtraPicked;
        public static readonly DirectProperty<ChoiceButton, string?> CurrentProperty =
            AvaloniaProperty.RegisterDirect<ChoiceButton, string?>(nameof(Current), o => o.Current, (o, v) => o.SetItems(o.items, v, o.extra), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
        string? current;
        /// <summary>The chosen item; a pick sets it (and raises Picked), SetItems or the property set it from the code.</summary>
        public string? Current { get => current; private set => SetAndRaise(CurrentProperty, ref current, value); }
        public Tone? TextColor { get; set; }
        /// <summary>A colour per item, in the field and in its menu (an HTTP method, a level); null = the usual text colour.</summary>
        public Func<string, Tone?>? ItemColor { get; set; }
        /// <summary>A Lucide icon before the chosen item in the field (Upload, Pattern); null = none.</summary>
        public Func<string, Geometry?>? ItemIcon { get; set; }
        /// <summary>Differs from what it inherits: the amber border.</summary>
        public bool Overridden { get => overridden; set { overridden = value; Restyle(); } }

        public ChoiceButton()
        {
            Height = ControlHeight;
            CornerRadius = new CornerRadius(4);
            BorderThickness = new Thickness(1);
            Background = B(Raised);
            Focusable = true;
            Padding = Pad;
            text.FontSize = TextSize.Body;
            chevron.VerticalAlignment = VerticalAlignment.Center; chevron.Margin = new Thickness(4, 0, 0, 0);
            Cursor = Build.Hand;
            var d = new DockPanel();
            DockPanel.SetDock(chevron, Dock.Right);
            icon.VerticalAlignment = VerticalAlignment.Center; icon.Margin = new Thickness(0, 0, 6, 0); icon.IsVisible = false; DockPanel.SetDock(icon, Dock.Left);
            d.Children.Add(chevron); d.Children.Add(icon); d.Children.Add(text);
            Child = d;
            PointerEntered += (s, e) => { hot = true; Restyle(); };
            PointerExited += (s, e) => { hot = false; Restyle(); };
            PointerReleased += (s, e) =>
            {
                if (e.InitialPressMouseButton != MouseButton.Left) return;
                Focus(NavigationMethod.Pointer); OpenMenu();
            };
            KeyDown += (s, e) => { if (e.Key is Key.Down or Key.Up or Key.Enter or Key.Space) { OpenMenu(); e.Handled = true; } };
            // the focus ring is for the keyboard: a click focuses it without one (and the menu's close gives the focus back the same way)
            GotFocus += (s, e) => { keyboardFocus = Look.IsKeyboard(e); Restyle(); };
            LostFocus += (s, e) => { keyboardFocus = false; Restyle(); };
            PropertyChanged += (s, e) => { if (e.Property == IsEnabledProperty) Restyle(); };
            Restyle();
        }

        public void SetItems(IEnumerable<string> list, string? current, string? extra = null)
        {
            items = list.ToList(); this.extra = extra; Current = current; text.Text = current ?? "";
            icon.Data = current == null ? null : ItemIcon?.Invoke(current); icon.IsVisible = icon.Data != null; Restyle();
            optionWidth = items.Append(current ?? "").Append(extra ?? "").Select(item => Math.Ceiling(Look.Measure(item, Ui(TextSize.Body))) + FormLayout.ChoiceChrome + (ItemIcon == null ? 0 : M(22))).DefaultIfEmpty(0).Max(); InvalidateMeasure(); Restyle();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var size = base.MeasureOverride(availableSize);
            double width = optionWidth;
            return new Size(Math.Min(availableSize.Width, Math.Max(size.Width, width)), size.Height);
        }

        internal Popup? OpenMenu()
        {
            if (!IsEnabled || open) return null;
            Opening?.Invoke();
            if (items.Count == 0) return null;
            open = true; Restyle();
            bool byKeyboard = keyboardFocus;
            var hover = Build.WatchHover(this, over => { hot = over; Restyle(); });
            var shown = extra == null ? items : items.Concat(new[] { extra }).ToList();
            return Menu.Show(this, shown, i => { if (i == items.Count) ExtraPicked?.Invoke(); else { var pick = items[i]; SetItems(items, pick, extra); Picked?.Invoke(pick); } }, Current == null ? -1 : items.IndexOf(Current), Bounds.Width, closed: () => { hover(); open = false; Focus(); keyboardFocus = byKeyboard; Restyle(); },
                color: ItemColor is { } colorOf ? i => colorOf(shown[i]) : null, attached: true);
        }

        public void Restyle()
        {
            BorderBrush = B(open || hot && IsEnabled ? Outline : IsFocused && keyboardFocus ? Accent : overridden ? Info : Look.Separator);
            Build.Attached(this, open, Pad);  // open: one shape with the list below it
            chevron.Stroke = B(hot || open ? Text2 : Text3); icon.Stroke = B(IsEnabled ? Text2 : Text3);
            text.Foreground = B(IsEnabled ? TextColor ?? (Current != null ? ItemColor?.Invoke(Current) : null) ?? Look.Text : Text3);
        }
    }
}

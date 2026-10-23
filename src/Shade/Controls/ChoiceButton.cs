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
        readonly TextBlock text = Kit.Text("", Look.Text);
        readonly Avalonia.Controls.Shapes.Path chevron = Kit.Icon24(Icons.ChevronDown, 16, 2), icon = Kit.Icon24(Icons.Check, 16, 2);
        bool hot, open, overridden;
        /// <summary>Drawn as while its list is open (the gallery shows the open state without a popup).</summary>
        internal bool ShowOpen { set { open = value; Restyle(); } }
        string? extra;
        double optionWidth;
        public event Action<string>? Picked;
        public event Action? Opening;
        /// <summary>The last menu item (SetItems' extra, like "+ New model"): an action, not a choice.</summary>
        public event Action? ExtraPicked;
        public static readonly DirectProperty<ChoiceButton, string?> CurrentProperty =
            AvaloniaProperty.RegisterDirect<ChoiceButton, string?>(nameof(Current), o => o.Current, (o, v) => o.SetItems(o.items, v, o.extra), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
        string? current;
        /// <summary>The chosen item; a pick sets it (and raises Picked), SetItems or the property set it from the code.</summary>
        public string? Current { get => current; private set => SetAndRaise(CurrentProperty, ref current, value); }
        public Tone? TextColor { get; set; }
        /// <summary>A colour per item, in the field and in its menu (REST methods); null = the usual text colour.</summary>
        public Func<string, Tone?>? ItemColor { get; set; }
        /// <summary>A Lucide icon before the chosen item in the field (Upload, Pattern); null = none.</summary>
        public Func<string, Geometry?>? ItemIcon { get; set; }
        /// <summary>Differs from what it inherits: the amber border.</summary>
        public bool Overridden { get => overridden; set { overridden = value; Restyle(); } }

        public ChoiceButton()
        {
            Height = 28;
            CornerRadius = new CornerRadius(4);
            BorderThickness = new Thickness(1);
            Background = B(Surface);
            Focusable = true;
            Padding = new Thickness(8, 0, 4, 0);
            text.FontSize = Body;
            chevron.VerticalAlignment = VerticalAlignment.Center; chevron.Margin = new Thickness(4, 0, 0, 0);
            Cursor = Kit.Hand;
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
                Focus(); OpenMenu();
            };
            KeyDown += (s, e) => { if (e.Key is Key.Down or Key.Up or Key.Enter or Key.Space) { OpenMenu(); e.Handled = true; } };
            GotFocus += (s, e) => Restyle(); LostFocus += (s, e) => Restyle();
            PropertyChanged += (s, e) => { if (e.Property == IsEnabledProperty) Restyle(); };
            Restyle();
        }

        public void SetItems(IEnumerable<string> list, string? current, string? extra = null)
        {
            items = list.ToList(); this.extra = extra; Current = current; text.Text = current ?? "";
            icon.Data = current == null ? null : ItemIcon?.Invoke(current); icon.IsVisible = icon.Data != null; Restyle();
            optionWidth = items.Append(current ?? "").Append(extra ?? "").Select(item => { var label = Kit.Text(item, Look.Text); label.Measure(Avalonia.Size.Infinity); return label.DesiredSize.Width + FormLayout.ChoiceChrome + (ItemIcon == null ? 0 : 22); }).DefaultIfEmpty(0).Max(); InvalidateMeasure(); Restyle();
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
            var shown = extra == null ? items : items.Concat(new[] { extra }).ToList();
            return Menu.Show(this, shown, i => { if (i == items.Count) ExtraPicked?.Invoke(); else { var pick = items[i]; SetItems(items, pick, extra); Picked?.Invoke(pick); } }, Current == null ? -1 : items.IndexOf(Current), Bounds.Width, closed: () => { open = false; Restyle(); Focus(); },
                color: ItemColor is { } colorOf ? i => colorOf(shown[i]) : null, attached: true);
        }

        public void Restyle()
        {
            BorderBrush = B(open || hot && IsEnabled ? MenuEdge : IsFocused ? Accent : overridden ? Override : Look.Border);
            // open: one shape with the list below it (square bottom corners, no bottom border)
            CornerRadius = open ? new CornerRadius(4, 4, 0, 0) : new CornerRadius(4);
            BorderThickness = open ? new Thickness(1, 1, 1, 0) : new Thickness(1);
            chevron.Stroke = B(hot || open ? Second : Faint); icon.Stroke = B(IsEnabled ? Second : Faint);
            text.Foreground = B(IsEnabled ? TextColor ?? (Current != null ? ItemColor?.Invoke(Current) : null) ?? Look.Text : Faint);
        }
    }
}

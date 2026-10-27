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
using Avalonia.VisualTree;
using static Shade.Look;

namespace Shade
{
    /// <summary>A button showing the chosen item; a click opens the items under it.</summary>
    public sealed class ChoiceButton : Button, IRestyle
    {
        IReadOnlyList<string> items = Array.Empty<string>();
        readonly TextBlock text = Build.Text("", Look.Text);
        readonly Avalonia.Controls.Shapes.Path chevron = Build.Icon24(Icons.ChevronDown, M(16), 2), icon = Build.Icon24(Icons.Check, M(16), 2);
        bool hot, open, keyboardFocus;
        Popup? popup;
        static Thickness Pad => new Thickness(M(8), 0, M(4), 0);
        /// <summary>Drawn as while its list is open (the gallery shows the open state without a popup).</summary>
        internal bool ShowOpen { set { open = value; Restyle(); } }
        double optionWidth;
        public event Action<string>? Picked;
        public event Action? Opening;
        /// <summary>The last menu item (SetItems' extra, like "+ New project"): an action, not a choice.</summary>
        public event Action? ExtraPicked;
        public static readonly DirectProperty<ChoiceButton, IReadOnlyList<string>> ItemsProperty = AvaloniaProperty.RegisterDirect<ChoiceButton, IReadOnlyList<string>>(nameof(Items), x => x.Items, (x, value) => x.Items = value);
        public static readonly DirectProperty<ChoiceButton, string?> CurrentProperty = AvaloniaProperty.RegisterDirect<ChoiceButton, string?>(nameof(Current), x => x.Current, (x, value) => x.Current = value, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
        public static readonly DirectProperty<ChoiceButton, bool> ExpandedProperty = AvaloniaProperty.RegisterDirect<ChoiceButton, bool>(nameof(Expanded), x => x.Expanded);
        public static readonly StyledProperty<Tone?> TextColorProperty = AvaloniaProperty.Register<ChoiceButton, Tone?>(nameof(TextColor));
        public static readonly StyledProperty<bool> OverriddenProperty = AvaloniaProperty.Register<ChoiceButton, bool>(nameof(Overridden));
        public static readonly StyledProperty<string?> ExtraProperty = AvaloniaProperty.Register<ChoiceButton, string?>(nameof(Extra));
        public static readonly StyledProperty<Func<string, Tone?>?> ItemColorProperty = AvaloniaProperty.Register<ChoiceButton, Func<string, Tone?>?>(nameof(ItemColor));
        public static readonly StyledProperty<Func<string, Geometry?>?> ItemIconProperty = AvaloniaProperty.Register<ChoiceButton, Func<string, Geometry?>?>(nameof(ItemIcon));
        string? current;
        public IReadOnlyList<string> Items { get => items; set { ArgumentNullException.ThrowIfNull(value); CloseMenu(); SetAndRaise(ItemsProperty, ref items, Array.AsReadOnly(value.Distinct(StringComparer.Ordinal).ToArray())); RefreshOptions(); } }
        /// <summary>The selected key; it may arrive before options. Programmatic changes do not raise Picked.</summary>
        public string? Current { get => current; set { if (SetAndRaise(CurrentProperty, ref current, value)) { CloseMenu(); RefreshOptions(); } } }
        public bool Expanded => open;
        public Tone? TextColor { get => GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }
        public bool Overridden { get => GetValue(OverriddenProperty); set => SetValue(OverriddenProperty, value); }
        public string? Extra { get => GetValue(ExtraProperty); set => SetValue(ExtraProperty, value); }
        public Func<string, Tone?>? ItemColor { get => GetValue(ItemColorProperty); set => SetValue(ItemColorProperty, value); }
        public Func<string, Geometry?>? ItemIcon { get => GetValue(ItemIconProperty); set => SetValue(ItemIconProperty, value); }
        protected override Type StyleKeyOverride => typeof(ChoiceButton);
        protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new PickerPeer(this);
        sealed class PickerPeer : Avalonia.Automation.Peers.ButtonAutomationPeer, Avalonia.Automation.Provider.IExpandCollapseProvider
        {
            readonly ChoiceButton owner;
            public PickerPeer(ChoiceButton owner) : base(owner)
            {
                this.owner = owner;
                owner.PropertyChanged += (_, e) => { if (e.Property == ExpandedProperty) RaisePropertyChangedEvent(Avalonia.Automation.ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty, e.OldValue is true ? Avalonia.Automation.ExpandCollapseState.Expanded : Avalonia.Automation.ExpandCollapseState.Collapsed, ExpandCollapseState); };
            }
            protected override Avalonia.Automation.Peers.AutomationControlType GetAutomationControlTypeCore() => Avalonia.Automation.Peers.AutomationControlType.ComboBox;
            protected override string GetNameCore() { var name = base.GetNameCore(); return string.IsNullOrEmpty(name) ? owner.Current ?? "" : name; }
            public Avalonia.Automation.ExpandCollapseState ExpandCollapseState => owner.Expanded ? Avalonia.Automation.ExpandCollapseState.Expanded : Avalonia.Automation.ExpandCollapseState.Collapsed;
            public bool ShowsMenu => false;
            public void Expand() { EnsureEnabled(); owner.OpenMenu(); }
            public void Collapse() { EnsureEnabled(); owner.CloseMenu(); }
        }
        internal Control CreateTemplate()
        {
            var content = new DockPanel(); DockPanel.SetDock(chevron, Dock.Right); DockPanel.SetDock(icon, Dock.Left);
            content.Children.Add(chevron); content.Children.Add(icon); content.Children.Add(text);
            var surface = new Border { Child = content };
            surface.Bind(Border.BackgroundProperty, new Avalonia.Data.TemplateBinding(BackgroundProperty)); surface.Bind(Border.BorderBrushProperty, new Avalonia.Data.TemplateBinding(BorderBrushProperty));
            surface.Bind(Border.BorderThicknessProperty, new Avalonia.Data.TemplateBinding(BorderThicknessProperty)); surface.Bind(Border.CornerRadiusProperty, new Avalonia.Data.TemplateBinding(CornerRadiusProperty)); surface.Bind(Border.PaddingProperty, new Avalonia.Data.TemplateBinding(PaddingProperty));
            return surface;
        }
        protected override void OnClick() { if (!IsEffectivelyEnabled) return; if (Expanded) CloseMenu(); else OpenMenu(); base.OnClick(); }
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == ExtraProperty) CloseMenu();
            if (text != null && (change.Property == ExtraProperty || change.Property == TextColorProperty || change.Property == ItemColorProperty || change.Property == ItemIconProperty || change.Property == OverriddenProperty)) RefreshOptions();
        }

        public ChoiceButton()
        {
            Height = ControlHeight;
            CornerRadius = new CornerRadius(4);
            BorderThickness = new Thickness(1);
            Background = B(Raised);
            Focusable = true;
            Padding = Pad;
            text.FontSize = TextSize.Body;
            chevron.VerticalAlignment = VerticalAlignment.Center; chevron.Margin += new Thickness(M(4), 0, 0, 0);
            Cursor = Build.Hand;
            icon.VerticalAlignment = VerticalAlignment.Center; icon.Margin += new Thickness(0, 0, M(6), 0); icon.IsVisible = false;
            text.TextTrimming = TextTrimming.CharacterEllipsis;
            FocusAdorner = null;
            PointerEntered += (s, e) => { hot = true; Restyle(); };
            PointerExited += (s, e) => { hot = false; Restyle(); };
            AddHandler(PointerPressedEvent, (_, e) => { if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) keyboardFocus = false; }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            AddHandler(KeyDownEvent, (_, e) =>
            {
                if (e.Key is Key.Enter or Key.Space or Key.Down or Key.Up) keyboardFocus = true;
                if (e.Key is Key.Down or Key.Up) { OpenMenu(); e.Handled = true; }
            }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            // the focus ring is for the keyboard: a click focuses it without one (and the menu's close gives the focus back the same way)
            GotFocus += (s, e) => { keyboardFocus = Look.IsKeyboard(e); Restyle(); };
            LostFocus += (s, e) => { keyboardFocus = false; Restyle(); };
            PropertyChanged += (s, e) => { if (e.Property == IsEffectivelyEnabledProperty || e.Property == IsVisibleProperty) { if (!IsEffectivelyEnabled || !IsVisible) CloseMenu(); Restyle(); } };
            DetachedFromVisualTree += (_, _) => CloseMenu();
            Restyle();
        }

        public void SetItems(IEnumerable<string> list, string? current, string? extra = null)
        {
            Items = list.ToArray(); Extra = extra; Current = current; RefreshOptions();
        }
        void RefreshOptions()
        {
            text.Text = current ?? ""; icon.Data = current == null ? null : ItemIcon?.Invoke(current); icon.IsVisible = icon.Data != null;
            optionWidth = items.Append(current ?? "").Append(Extra ?? "").Select(item => Math.Ceiling(Look.Measure(item, Ui(TextSize.Body))) + FormLayout.ChoiceChrome + (ItemIcon == null ? 0 : M(22))).DefaultIfEmpty(0).Max(); InvalidateMeasure(); Restyle();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var size = base.MeasureOverride(availableSize);
            double width = optionWidth;
            return new Size(Math.Min(availableSize.Width, Math.Max(size.Width, width)), size.Height);
        }

        internal Popup? OpenMenu()
        {
            if (!IsEffectivelyEnabled || !IsEffectivelyVisible || open || TopLevel.GetTopLevel(this) == null) return null;
            Opening?.Invoke();
            if ((items.Count == 0 && Extra == null) || !IsEffectivelyEnabled || !IsEffectivelyVisible) return null;
            SetAndRaise(ExpandedProperty, ref open, true); Restyle();
            bool byKeyboard = keyboardFocus;
            var hover = Build.WatchHover(this, over => { hot = over; Restyle(); });
            var shown = Extra == null ? items.ToArray() : items.Concat(new[] { Extra }).ToArray();
            popup = Menu.Show(this, shown, i => { if (!IsEffectivelyEnabled || !IsEffectivelyVisible) return; if (i == items.Count) ExtraPicked?.Invoke(); else { var pick = items[i]; Current = pick; Picked?.Invoke(pick); } }, Current == null ? -1 : items.ToList().IndexOf(Current), Bounds.Width, closed: () => { popup = null; hover(); SetAndRaise(ExpandedProperty, ref open, false); Restyle(); },
                atPointer: false, color: ItemColor is { } colorOf ? i => colorOf(shown[i]) : null, attached: true, navigation: byKeyboard ? NavigationMethod.Tab : NavigationMethod.Pointer);
            return popup;
        }

        void CloseMenu() => popup?.SetCurrentValue(Popup.IsOpenProperty, false);

        public void Restyle()
        {
            BorderBrush = B(IsFocused && keyboardFocus ? FocusBorder : open || hot && IsEffectivelyEnabled ? Outline : Overridden ? Info : Look.Separator);
            CornerRadius = open ? new CornerRadius(Radius, Radius, 0, 0) : new CornerRadius(Radius);
            BorderThickness = open ? new Thickness(1, 1, 1, 0) : new Thickness(1);
            Padding = open ? new Thickness(Pad.Left, Pad.Top, Pad.Right, Pad.Bottom + 1) : Pad;
            chevron.Stroke = B(hot || open ? Text2 : Text3); icon.Stroke = B(IsEffectivelyEnabled ? Text2 : Text3);
            text.Foreground = B(IsEffectivelyEnabled ? TextColor ?? (Current != null ? ItemColor?.Invoke(Current) : null) ?? Look.Text : Text3);
        }
    }
}

// Shade - shared control: a dropdown showing the chosen item; the items open under it.
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using static Shade.Look;

namespace Shade
{
    /// <summary>A dropdown of text items, themed as Avalonia's ComboBox (ComboBoxTheme): a native selection, keyboard,
    /// IsDropDownOpen and automation, and Shade's own additions: Items and Current as plain strings, a colour or an icon per item,
    /// an extra action at the end of the list, an override mark and the events a form needs.</summary>
    public sealed class ChoiceButton : ComboBox
    {
        sealed record ExtraEntry(string Text) { public override string ToString() => Text; }
        IReadOnlyList<string> items = Array.Empty<string>();
        string? current;
        ExtraEntry? extra;
        bool syncing, batching, opening, restoreFocus;
        Avalonia.Input.NavigationMethod navigation = Avalonia.Input.NavigationMethod.Pointer;  // how the list was last driven: its rows show the focus ring only for the keyboard
        double optionWidth;
        Popup? popup;
        /// <summary>The user picked an item from the list (not a change of Current by code, and not the item already current).</summary>
        public event Action<string>? Picked;
        /// <summary>The list is about to open: the place to bring Items up to date.</summary>
        public event Action? Opening;
        /// <summary>The last menu item (SetItems' extra, like "+ New project"): an action, not a choice.</summary>
        public event Action? ExtraPicked;
        public static readonly DirectProperty<ChoiceButton, IReadOnlyList<string>> ItemsProperty = AvaloniaProperty.RegisterDirect<ChoiceButton, IReadOnlyList<string>>(nameof(Items), x => x.Items, (x, value) => x.Items = value);
        public static readonly DirectProperty<ChoiceButton, string?> CurrentProperty = AvaloniaProperty.RegisterDirect<ChoiceButton, string?>(nameof(Current), x => x.Current, (x, value) => x.Current = value, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
        public static readonly StyledProperty<bool> OverriddenProperty = AvaloniaProperty.Register<ChoiceButton, bool>(nameof(Overridden));
        public static readonly StyledProperty<string?> ExtraProperty = AvaloniaProperty.Register<ChoiceButton, string?>(nameof(Extra));
        public static readonly StyledProperty<Func<string, Tone?>?> ItemColorProperty = AvaloniaProperty.Register<ChoiceButton, Func<string, Tone?>?>(nameof(ItemColor));
        public static readonly StyledProperty<Func<string, Geometry?>?> ItemIconProperty = AvaloniaProperty.Register<ChoiceButton, Func<string, Geometry?>?>(nameof(ItemIcon));
        /// <summary>The choices, distinct and compared ordinally; replacing them closes an open list and keeps Current.</summary>
        public new IReadOnlyList<string> Items { get => items; set { ArgumentNullException.ThrowIfNull(value); SetAndRaise(ItemsProperty, ref items, Array.AsReadOnly(value.Distinct(StringComparer.Ordinal).ToArray())); Close(); Sync(); } }
        /// <summary>The selected key; it may arrive before the options. Changes by code do not raise Picked.</summary>
        public string? Current { get => current; set { if (SetAndRaise(CurrentProperty, ref current, value)) { Close(); Sync(); } } }
        /// <summary>The field is drawn as one that differs from what it inherits (the Info border).</summary>
        public bool Overridden { get => GetValue(OverriddenProperty); set => SetValue(OverriddenProperty, value); }
        public string? Extra { get => GetValue(ExtraProperty); set => SetValue(ExtraProperty, value); }
        public Func<string, Tone?>? ItemColor { get => GetValue(ItemColorProperty); set => SetValue(ItemColorProperty, value); }
        public Func<string, Geometry?>? ItemIcon { get => GetValue(ItemIconProperty); set => SetValue(ItemIconProperty, value); }
        protected override Type StyleKeyOverride => typeof(ChoiceButton);
        /// <summary>Drawn as while its list is open (the gallery shows the open state without a popup).</summary>
        internal bool ShowOpen { set => PseudoClasses.Set(":dropdownopen", value); }

        public ChoiceButton()
        {
            RefreshRows(); SelectionChanged += SelectionPicked;
            AddHandler(PointerPressedEvent, (_, _) => navigation = Avalonia.Input.NavigationMethod.Pointer, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
            AddHandler(KeyDownEvent, (_, _) => navigation = Avalonia.Input.NavigationMethod.Tab, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
            DetachedFromVisualTree += (_, _) => Close();
        }

        public void SetItems(IEnumerable<string> list, string? current, string? extra = null)
        {
            batching = true;
            try { Items = list.ToArray(); Extra = extra; Current = current; } finally { batching = false; }
            Sync();
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            popup = e.NameScope.Find<Popup>("PART_Popup");
            if (popup == null) return;
            popup.Child!.GotFocus += (_, _) => restoreFocus = true;  // the focus was in the list when it closed: give it back
            var top = default(TopLevel);
            void Press(object? sender, Avalonia.Input.PointerPressedEventArgs press) { if (!(press.Source is Visual source && (source == this || this.IsVisualAncestorOf(source) || popup.IsInsidePopup(source)))) restoreFocus = false; }
            popup.Opened += (_, _) =>  // the list takes the focus on the current row (or the first) the way it was opened: a ring for the keyboard only
            {
                restoreFocus = false; top = TopLevel.GetTopLevel(this);
                top?.AddHandler(PointerPressedEvent, Press, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
                if (ContainerFromIndex(Math.Max(SelectedIndex, 0)) is { } row) { Focus(); row.Focus(navigation); }  // through the field: focusing the row it already has would keep the way the list opened it
            };
            popup.Closed += (_, _) =>  // a pick or Escape gives the focus back to the field; an outside click does not take it
            {
                top?.RemoveHandler(PointerPressedEvent, Press);
                bool back = restoreFocus; restoreFocus = false;
                if (!back || !IsEffectivelyEnabled || !IsEffectivelyVisible || TopLevel.GetTopLevel(this) is not { } host) return;
                if (IsFocused) host.FocusManager?.Focus(null);  // Avalonia already handed the focus back, with no ring: hand it again the way the list was driven
                Focus(navigation);
            };
        }

        protected override void OnPointerPressed(Avalonia.Input.PointerPressedEventArgs e) { navigation = Avalonia.Input.NavigationMethod.Pointer; base.OnPointerPressed(e); }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            var property = change.Property;
            if (property == ExtraProperty) { extra = Extra == null ? null : new ExtraEntry(Extra); Close(); Sync(); }
            else if (property == ItemColorProperty || property == ItemIconProperty) { RefreshRows(); Sync(); }
            else if (property == IsEffectivelyEnabledProperty || property == IsVisibleProperty) { if (!IsEffectivelyEnabled || !IsVisible) Close(); RefreshRows(); }
            else if (property == OverriddenProperty) PseudoClasses.Set(":overridden", Overridden);
            else if (property == IsDropDownOpenProperty)
            {
                if (!IsEffectivelyEnabled || !IsEffectivelyVisible || TopLevel.GetTopLevel(this) == null) { Close(); return; }
                opening = true;
                try { Opening?.Invoke(); } finally { opening = false; }
                if ((items.Count == 0 && extra == null) || !IsEffectivelyEnabled || !IsEffectivelyVisible) Close();
            }
        }

        /// <summary>Arrows and Enter open the list, as the menus of Shade always did; a native ComboBox changes its selection instead.</summary>
        protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
        {
            if (!IsDropDownOpen && e.Key is Avalonia.Input.Key.Down or Avalonia.Input.Key.Up or Avalonia.Input.Key.Enter or Avalonia.Input.Key.Space)
            {
                if (IsEffectivelyEnabled) SetCurrentValue(IsDropDownOpenProperty, true);
                e.Handled = true; return;
            }
            base.OnKeyDown(e);
        }

        void SelectionPicked(object? sender, SelectionChangedEventArgs e)
        {
            if (syncing) return;
            if (SelectedItem is ExtraEntry) { Sync(); ExtraPicked?.Invoke(); return; }  // an action: the selection stays where it was
            if (SelectedItem is string pick && pick != current) { Current = pick; Picked?.Invoke(pick); }
        }

        void Close() { if (!opening) SetCurrentValue(IsDropDownOpenProperty, false); }

        /// <summary>The list and the selection follow Items, Extra and Current; the selection change is not the user's.</summary>
        void Sync()
        {
            if (batching) return;
            bool was = syncing; syncing = true;
            try
            {
                var shown = items.Cast<object>().Concat(extra == null ? Array.Empty<object>() : new object[] { extra }).ToArray();
                if (ItemsSource is not IEnumerable<object> now || !now.SequenceEqual(shown)) ItemsSource = shown;  // an unchanged list keeps its rows
                bool known = current != null && items.Contains(current);
                if (!Equals(SelectedItem, known ? current : null)) SetCurrentValue(SelectedItemProperty, known ? current : null);
                SetCurrentValue(PlaceholderTextProperty, known ? null : current);  // a key that has no item yet is still shown
                SetCurrentValue(PlaceholderForegroundProperty, B(Look.Text));
            }
            finally { syncing = was; }
            optionWidth = items.Append(current ?? "").Append(Extra ?? "").Select(item => Math.Ceiling(Look.Measure(item, Ui(TextSize.Body))) + FormLayout.ChoiceChrome + (ItemIcon == null ? 0 : M(22))).DefaultIfEmpty(0).Max();
            InvalidateMeasure();
        }

        /// <summary>The row of an item, in the list and in the field: its icon, its text in its own colour. A disabled field is plain.</summary>
        void RefreshRows() => ItemTemplate = new FuncDataTemplate<object>((item, _) =>
        {
            if (item is not (string or ExtraEntry)) return null;
            var key = item as string;
            var label = new TextBlock { Text = item.ToString(), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            if (key != null && IsEffectivelyEnabled && ItemColor?.Invoke(key) is { } tone) label.Foreground = B(tone);
            if (key == null || ItemIcon?.Invoke(key) is not { } geometry) return label;
            var icon = Build.Icon24(geometry, M(16), 2); icon.Stroke = B(IsEffectivelyEnabled ? Text2 : Text3); icon.Margin += new Thickness(0, 0, M(6), 0);
            DockPanel.SetDock(icon, Dock.Left);
            return new DockPanel { Children = { icon, label } };
        });

        protected override Size MeasureOverride(Size availableSize)
        {
            var size = base.MeasureOverride(availableSize);
            return new Size(Math.Min(availableSize.Width, Math.Max(size.Width, optionWidth)), size.Height);
        }
    }
}

// Shade - shared control: a picker of several items; selection stays open until dismissed.
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using static Shade.Look;

namespace Shade
{
    /// <summary>A picker of several items: a checkbox per item, kept open while selecting. Nothing selected = everything.
    /// Items and Selected are snapshots; replace them to update bindings. Set changes selection without Changed.</summary>
    public sealed class MultiChoice : TemplatedControl
    {
        public static readonly DirectProperty<MultiChoice, IReadOnlyList<string>> ItemsProperty =
            AvaloniaProperty.RegisterDirect<MultiChoice, IReadOnlyList<string>>(nameof(Items), o => o.Items, (o, v) => o.Items = v);
        public static readonly DirectProperty<MultiChoice, IReadOnlySet<string>> SelectedProperty =
            AvaloniaProperty.RegisterDirect<MultiChoice, IReadOnlySet<string>>(nameof(Selected), o => o.Selected, (o, v) => o.Selected = v, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
        public static readonly StyledProperty<string?> LabelProperty = AvaloniaProperty.Register<MultiChoice, string?>(nameof(Label));
        public static readonly StyledProperty<string?> EmptyTextProperty = AvaloniaProperty.Register<MultiChoice, string?>(nameof(EmptyText));

        IReadOnlyList<string> items = Array.Empty<string>();
        IReadOnlySet<string> selected = FrozenSet<string>.Empty;
        readonly List<CheckBox> choices = new();
        Popup? popup;
        bool synchronizing, keyboardFocus;
        string caption = "";
        internal static readonly DirectProperty<MultiChoice, string> CaptionProperty = AvaloniaProperty.RegisterDirect<MultiChoice, string>(nameof(Caption), o => o.caption);
        /// <summary>What the field shows: the label, or the chosen names, or the count.</summary>
        internal string Caption => caption;
        public event Action? Changed;

        /// <summary>Loaded option names, distinct and compared ordinally. A non-empty replacement removes unavailable selections.</summary>
        public IReadOnlyList<string> Items
        {
            get => items;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                var next = value.Select(item => item ?? throw new ArgumentException("An option cannot be null.", nameof(value))).Distinct(StringComparer.Ordinal).ToArray();
                if (items.SequenceEqual(next)) return;
                CloseMenu(true);
                SetAndRaise(ItemsProperty, ref items, Array.AsReadOnly(next));
                Set(selected); Refresh();
            }
        }
        /// <summary>Selected names; empty options retain keys during loading. Programmatic changes notify bindings without raising Changed.</summary>
        public IReadOnlySet<string> Selected
        {
            get => selected;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                var next = (items.Count == 0 ? value : value.Where(item => items.Contains(item, StringComparer.Ordinal))).ToFrozenSet(StringComparer.Ordinal);
                if (!selected.SetEquals(next)) SetAndRaise(SelectedProperty, ref selected, next);
                synchronizing = true;
                try { foreach (var choice in choices) choice.SetCurrentValue(ToggleButton.IsCheckedProperty, selected.Contains((string)choice.Tag!)); }
                finally { synchronizing = false; }
                Refresh();
            }
        }
        /// <summary>Optional fixed caption, for filters whose chosen values are shown separately.</summary>
        public string? Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
        /// <summary>Caption with no choices selected; null uses the localized "All".</summary>
        public string? EmptyText { get => GetValue(EmptyTextProperty); set => SetValue(EmptyTextProperty, value); }

        public MultiChoice()
        {
            Activate(this, () => { if (popup == null) OpenMenu(); else CloseMenu(true); });
            GotFocus += (_, e) => { if (e.Source == this) keyboardFocus = Look.IsKeyboard(e); };
            AddHandler(PointerPressedEvent, (_, _) => keyboardFocus = false, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            AddHandler(KeyDownEvent, (_, e) => { if (e.Key is Key.Enter or Key.Space or Key.Down or Key.Up or Key.F4) keyboardFocus = true; }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            KeyDown += (_, e) => { if (e.Key is Key.Down or Key.Up or Key.F4) { OpenMenu(e.Key == Key.Up); e.Handled = true; } };
            DetachedFromVisualTree += (_, _) => CloseMenu(false);
            Words(this, control => control.Refresh());
        }
        public MultiChoice(IEnumerable<string> list, string empty) : this()
        {
            ArgumentNullException.ThrowIfNull(list);
            Items = list.ToArray(); EmptyText = empty;
        }
        /// <summary>Selects loaded names, without raising Changed. Passing Selected itself is safe.</summary>
        public void Set(IEnumerable<string> picked)
        {
            ArgumentNullException.ThrowIfNull(picked);
            Selected = picked.ToFrozenSet(StringComparer.Ordinal);
        }
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == LabelProperty || change.Property == EmptyTextProperty) Refresh();
            if (change.Property == IsEffectivelyEnabledProperty || change.Property == IsVisibleProperty)
            {
                if (!IsEffectivelyEnabled || !IsVisible) CloseMenu(false);
            }
        }
        internal bool Expanded => popup?.IsOpen == true;
        internal event Action<bool>? ExpandedChanged;
        protected override AutomationPeer OnCreateAutomationPeer() => new PickerPeer(this);
        sealed class PickerPeer : ControlAutomationPeer, IExpandCollapseProvider
        {
            readonly MultiChoice owner;
            public PickerPeer(MultiChoice owner) : base(owner)
            {
                this.owner = owner;
                owner.ExpandedChanged += expanded => RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                    expanded ? ExpandCollapseState.Collapsed : ExpandCollapseState.Expanded, ExpandCollapseState);
            }
            protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ComboBox;
            protected override string GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : owner.Label ?? owner.caption;
            public ExpandCollapseState ExpandCollapseState => owner.Expanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;
            public bool ShowsMenu => false;
            public void Expand() { EnsureEnabled(); owner.OpenMenu(); }
            public void Collapse() { EnsureEnabled(); owner.CloseMenu(true); }
        }
        internal void CloseMenu(bool returnFocus)
        {
            if (popup is not { } open) return;
            bool focused = open.Child?.IsKeyboardFocusWithin == true;
            open.IsOpen = false;
            if (returnFocus && focused && IsEffectivelyEnabled && IsEffectivelyVisible && TopLevel.GetTopLevel(this) != null) Focus(NavigationMethod.Pointer);
        }
        internal Popup? OpenMenu(bool last = false)
        {
            if (!IsEffectivelyEnabled || !IsEffectivelyVisible || popup != null || items.Count == 0 || TopLevel.GetTopLevel(this) == null) return null;
            var rows = new StackPanel { Margin = new Thickness(0, 0, 0, M(4)) };
            rows.Children.Add(new Border { Height = 1, Background = B(Look.Separator), Margin = new Thickness(0, 0, 0, M(4)) });
            foreach (var item in items)
            {
                var tick = CheckBoxTheme.Glyph(selected.Contains(item));
                var label = Build.Text(item, selected.Contains(item) ? Look.Text : Text2, TextSize.Body); label.TextTrimming = TextTrimming.CharacterEllipsis;
                label.Margin = new Thickness(M(8), 0, 0, 0);
                var line = new DockPanel(); DockPanel.SetDock(tick, Dock.Left); line.Children.Add(tick); line.Children.Add(label);
                var surface = new Border { Padding = new Thickness(M(8), 0), Child = line, Name = "PART_Row" };
                var row = new CheckBox
                {
                    Tag = item,
                    MinHeight = 0,
                    MinWidth = 0,
                    Padding = new Thickness(0),
                    IsChecked = selected.Contains(item),
                    Height = ControlHeight,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Template = new FuncControlTemplate<CheckBox>((_, names) => { names.Register("PART_Row", surface); return surface; })
                };
                AutomationProperties.SetName(row, item); ToolTip.SetTip(row, item); FocusRing(row);
                row.PropertyChanged += (_, e) =>
                {
                    if (e.Property != ToggleButton.IsCheckedProperty) return;
                    tick.IsChecked = row.IsChecked; label.Foreground = B(row.IsChecked == true ? Look.Text : Text2);
                    if (synchronizing || !IsEffectivelyEnabled || !Expanded || !choices.Contains(row)) return;
                    var next = selected.ToHashSet(StringComparer.Ordinal);
                    if (row.IsChecked == true) next.Add(item); else next.Remove(item);
                    Selected = next; Changed?.Invoke();
                };
                int index = choices.Count;
                row.KeyDown += (_, e) =>
                {
                    int next = e.Key switch { Key.Down => Math.Min(index + 1, choices.Count - 1), Key.Up => Math.Max(0, index - 1), Key.Home => 0, Key.End => choices.Count - 1, _ => -1 };
                    if (next >= 0) { choices[next].Focus(NavigationMethod.Directional); choices[next].BringIntoView(); e.Handled = true; }
                };
                choices.Add(row); rows.Children.Add(row);
            }
            var card = new Border
            {
                Background = B(Raised),
                BorderBrush = B(Outline),
                BorderThickness = new Thickness(1, 0, 1, 1),
                CornerRadius = new CornerRadius(0, 0, Radius, Radius),
                MinWidth = Bounds.Width,
                MaxWidth = Bounds.Width,
                ClipToBounds = true,
                Child = Menu.ScrollRows(rows, Bounds.Width)
            };
            card.Styles.Add(new Avalonia.Styling.Style(x => x.OfType<CheckBox>().Template().OfType<Border>().Name("PART_Row")) { Setters = { new Avalonia.Styling.Setter(Border.BackgroundProperty, Brushes.Transparent) } });
            card.Styles.Add(new Avalonia.Styling.Style(x => x.OfType<CheckBox>().Class(":pointerover").Template().OfType<Border>().Name("PART_Row")) { Setters = { new Avalonia.Styling.Setter(Border.BackgroundProperty, B(Fill)) } });
            card.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape) { CloseMenu(true); e.Handled = true; }
                else if (e.Key == Key.Tab)
                {
                    CloseMenu(true);
                    TopLevel.GetTopLevel(this)?.FocusManager?.TryMoveFocus(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? NavigationDirection.Previous : NavigationDirection.Next);
                    e.Handled = true;
                }
            };
            string prefix = ""; long typedAt = 0;
            card.TextInput += (_, e) =>
            {
                if (string.IsNullOrEmpty(e.Text)) return;
                long now = Environment.TickCount64; prefix = now - typedAt > 700 ? e.Text : prefix + e.Text; typedAt = now;
                int from = Math.Max(0, choices.FindIndex(choice => choice.IsFocused) + 1);
                var candidates = choices.Skip(from).Concat(choices.Take(from));
                var match = candidates.FirstOrDefault(choice => Culture.CompareInfo.IsPrefix((string)choice.Tag!, prefix, System.Globalization.CompareOptions.IgnoreCase));
                if (match == null && prefix.Length > e.Text.Length) { prefix = e.Text; match = candidates.FirstOrDefault(choice => Culture.CompareInfo.IsPrefix((string)choice.Tag!, prefix, System.Globalization.CompareOptions.IgnoreCase)); }
                if (match != null) { match.Focus(NavigationMethod.Directional); match.BringIntoView(); }
                e.Handled = true;
            };
            var pop = new Popup { PlacementTarget = this, Placement = PlacementMode.BottomEdgeAlignedLeft, IsLightDismissEnabled = true, OverlayDismissEventPassThrough = true, Child = card };
            popup = pop;
            Menu.OwnPopup(pop, this, () => CloseMenu(true));
            pop.Opened += (_, _) => { var target = last ? choices.Last() : choices.FirstOrDefault(choice => choice.IsChecked == true) ?? choices[0]; target.Focus(keyboardFocus ? NavigationMethod.Tab : NavigationMethod.Pointer); target.BringIntoView(); ExpandedChanged?.Invoke(true); };
            pop.Closed += (_, _) => { popup = null; choices.Clear(); ((ISetLogicalParent)pop).SetParent(null); PseudoClasses.Set(":dropdownopen", false); ExpandedChanged?.Invoke(false); };
            ((ISetLogicalParent)pop).SetParent(this); pop.IsOpen = true; PseudoClasses.Set(":dropdownopen", true);
            return pop;
        }
        void Refresh()
        {
            var shown = Label ?? (selected.Count == 0 ? EmptyText ?? T("All") : selected.Count <= 2 ? string.Join(", ", items.Count == 0 ? selected.Order(StringComparer.Ordinal) : items.Where(selected.Contains)) : T("{0} selected", selected.Count));
            SetAndRaise(CaptionProperty, ref caption, shown);
        }
    }
}

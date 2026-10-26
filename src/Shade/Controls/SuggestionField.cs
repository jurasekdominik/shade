// Shade - a regular editable field with bounded, keyboard-accessible prefix suggestions.
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Shade
{
    public sealed class SuggestionField : Field
    {
        string[] items = Array.Empty<string>();
        public static readonly DirectProperty<SuggestionField, IReadOnlyList<string>> ItemsProperty = AvaloniaProperty.RegisterDirect<SuggestionField, IReadOnlyList<string>>(nameof(Items), x => x.Items, (x, value) => x.Items = value);
        public static readonly StyledProperty<bool> ApplySelectionProperty = AvaloniaProperty.Register<SuggestionField, bool>(nameof(ApplySelection), true);
        public IReadOnlyList<string> Items
        {
            get => Array.AsReadOnly(items);
            set { ArgumentNullException.ThrowIfNull(value); var old = Items; items = value.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.Ordinal).ToArray(); RaisePropertyChanged(ItemsProperty, old, Items); RefreshSuggestions(); }
        }
        Popup? popup;
        int selected = -1;
        string? programmaticText;
        public event Action<string>? Picked;
        /// <summary>False when the consumer opens a separate document and must snapshot the current text first.</summary>
        public bool ApplySelection { get => GetValue(ApplySelectionProperty); set => SetValue(ApplySelectionProperty, value); }
        internal IReadOnlyList<string> Suggestions { get; private set; } = Array.Empty<string>();
        internal bool SuggestionsOpen => popup?.IsOpen == true;

        public SuggestionField() : this("", Array.Empty<string>()) { }
        public SuggestionField(string placeholder, IEnumerable<string> items) : base(placeholder)
        {
            Items = items.ToArray();
            // Field.Edited suppresses queued programmatic changes. Suggestions follow the actual
            // current text instead, so coalesced native TextChanged events cannot leave stale results.
            Box.TextChanged += (s, e) =>
            {
                if (programmaticText != null && Text == programmaticText) { programmaticText = null; CloseSuggestions(); return; }
                programmaticText = null; RefreshSuggestions();
            };
            Box.AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
            Box.LostFocus += (s, e) => Dispatcher.UIThread.Post(() => { if (!Box.IsFocused) CloseSuggestions(); });
            PropertyChanged += (_, e) => { if (e.Property == IsReadOnlyProperty && IsReadOnly) CloseSuggestions(); };
            DetachedFromVisualTree += (s, e) => CloseSuggestions();
        }

        public override string Text { get => base.Text; set { programmaticText = value ?? ""; CloseSuggestions(); base.Text = value ?? ""; } }

        internal static IReadOnlyList<string> PrefixMatches(IEnumerable<string> items, string prefix) =>
            string.IsNullOrWhiteSpace(prefix) ? Array.Empty<string>() : NameSearch.Match(items, prefix).Take(8).ToArray();

        void RefreshSuggestions()
        {
            selected = -1;
            Suggestions = PrefixMatches(items, Text);
            if (IsReadOnly || !IsEffectivelyEnabled || !IsEffectivelyVisible || !Box.IsFocused || Suggestions.Count == 0 || Suggestions.Count == 1 && Suggestions[0].Equals(Text.Trim(), StringComparison.OrdinalIgnoreCase)) { CloseSuggestions(); return; }
            RenderSuggestions();
        }

        void RenderSuggestions()
        {
            // Rebuild only when result data changes; keyboard movement updates the existing surface.
            CloseSuggestions();
            string[] shown = Suggestions.ToArray();
            var next = new Popup
            {
                PlacementTarget = this,
                Placement = PlacementMode.BottomEdgeAlignedLeft,
                IsLightDismissEnabled = true,
                OverlayInputPassThroughElement = this
            };
            var card = Menu.Panel(shown, i => Pick(shown[i]), CloseSuggestions, selected, Bounds.Width, null, true, true, Text);  // the typed letters marked
            card.Focusable = false;
            popup = next;
            next.Child = card;
            next.Closed += (s, e) =>
            {
                if (!ReferenceEquals(popup, next)) return;
                popup = null; ((ISetLogicalParent)next).SetParent(null); Open = false;
            };
            ((ISetLogicalParent)next).SetParent(this);
            Menu.OwnPopup(next, this);
            next.IsOpen = true;
            Open = true;
        }

        internal void Pick(int index)
        {
            if (index < 0 || index >= Suggestions.Count) return;
            Pick(Suggestions[index]);
        }
        void Pick(string choice)
        {
            if (!IsEffectivelyEnabled || IsReadOnly || !IsEffectivelyVisible) return;
            CloseSuggestions();
            if (ApplySelection) Text = choice;
            Picked?.Invoke(choice);
        }

        void OnKey(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && !SuggestionsOpen) { RefreshSuggestions(); if (SuggestionsOpen) { selected = 0; Menu.Highlight((Border)popup!.Child!, selected); e.Handled = true; } return; }
            if (!SuggestionsOpen) return;
            if (e.Key is Key.Down or Key.Up)
            {
                selected = selected < 0 ? (e.Key == Key.Down ? 0 : Suggestions.Count - 1) : (selected + (e.Key == Key.Down ? 1 : Suggestions.Count - 1)) % Suggestions.Count;
                Menu.Highlight((Border)popup!.Child!, selected); e.Handled = true;
            }
            else if (e.Key == Key.Enter && selected >= 0) { Pick(selected); e.Handled = true; }
            else if (e.Key == Key.Escape) { CloseSuggestions(); e.Handled = true; }
        }

        void CloseSuggestions()
        {
            if (popup == null) return;
            var previous = popup; popup = null;
            previous.IsOpen = false;
            ((ISetLogicalParent)previous).SetParent(null);
            Open = false;
        }
    }
}

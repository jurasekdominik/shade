using System;
using System.Linq;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using static Shade.Look;

namespace Shade;

public sealed partial class PaperFormBuilder
{
    readonly List<Color> recentDocumentColours = new();
    object colourDocument = new();
    void ResetDocumentColours() { colourDocument = new(); recentDocumentColours.Clear(); }
    IEnumerable<Color> FormColours()
    {
        var structures = All(Definition.Pages.SelectMany(p => p.Structures)).ToArray();
        var values = new[] { Definition.Background, Definition.Foreground }
            .Concat(structures.SelectMany(s => new[] { s.Background, s.Foreground, s.BorderWidth > 0 ? s.BorderColor : null }
                .Concat(s.Kind == "characters" ? s.CellRanges.SelectMany(r => new[] { r.Background, r.Foreground }) : Array.Empty<string?>())));
        return values.Where(v => v != null).Select(v => Color.Parse(v!))
            .Concat(new[] { (Ink(Definition.Background, Raised) as ISolidColorBrush)!.Color, (Ink(Definition.Foreground, Text) as ISolidColorBrush)!.Color })
            .Concat(structures.Any(s => s.BorderWidth > 0 && s.BorderColor == null) ? new[] { Look.Separator.Color } : Array.Empty<Color>()).Distinct();
    }
    void RememberDocumentColour(Color colour)
    {
        recentDocumentColours.Remove(colour); recentDocumentColours.Insert(0, colour);
        if (recentDocumentColours.Count > 12) recentDocumentColours.RemoveAt(12);
    }
    void FontPicker(PaperStructure selected)
    {
        var names = FontManager.Current.SystemFonts.Select(f => f.Name).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.CurrentCultureIgnoreCase).ToArray();
        var picker = new SearchChoice { ItemsSource = names, SelectedItem = selected.FontFamily ?? DocumentFont.Name, PlaceholderText = selected.FontFamily ?? DocumentFont.Name, HorizontalAlignment = HorizontalAlignment.Stretch };
        AutomationProperties.SetName(picker, T("Installed font"));
        picker.SelectionCommitted += (_, _) => { if (Selected is { } s && picker.SelectedItem is string name) ReplaceSelected(s with { FontFamily = name }); };
        inspector.Children.Add(Labelled("Font family", picker));
    }
    void ColourPicker(string label, PaperStructure selected, Func<PaperStructure, string?> get, Func<PaperStructure, string?, PaperStructure> set, Tone fallback, Func<string?>? inherited = null)
    {
        string ColourText(Color colour) => colour.A == 255 ? HexOf(colour) : colour.ToString();
        IBrush Preview(string? value) => Ink(value ?? inherited?.Invoke(), fallback);
        var swatch = new Border { Width = M(20), Height = M(16), BorderThickness = new Thickness(1), BorderBrush = B(Outline), Margin = new Thickness(0, 0, M(8), 0), Background = Preview(get(selected)) };
        var caption = Build.Text(get(selected) ?? T("Default"), Text, TextSize.Body);
        var row = new StackPanel { Orientation = Orientation.Horizontal, Children = { swatch, caption } };
        var button = new ActionButton { Content = row, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left };
        AutomationProperties.SetName(button, T(label));
        Color? lastPicked = null;
        void Apply(string? value)
        {
            if (Selected is not { } s || s.Id != selected.Id) return;
            inspectorEditing = true;
            try { ReplaceSelected(set(s, value)); lastPicked = value == null ? null : Color.Parse(value); swatch.Background = Preview(value); caption.Text = value ?? T("Default"); }
            finally { inspectorEditing = false; }
        }
        Popup? helper = null;
        button.Click += (_, _) =>
        {
            if (helper?.IsOpen == true) { helper.IsOpen = false; return; }
            if (Selected is not { } s || s.Id != selected.Id) return;
            inspectorHasEdits = false; lastPicked = null; var document = colourDocument;
            var grid = new ColorGrid((Preview(get(s)) as ISolidColorBrush)!.Color) { IsDocumentPalette = true };
            var reset = new ActionButton(T("Default"), ButtonKind.Ghost) { Margin = new Thickness(M(8), 0, 0, 0) };
            var hex = new Field { Text = ColourText((Preview(get(s)) as ISolidColorBrush)!.Color) };
            AutomationProperties.SetName(hex.Box, T(label));
            var controls = new DockPanel(); DockPanel.SetDock(reset, Dock.Right); controls.Children.Add(reset); controls.Children.Add(hex);
            var footer = new Border { Padding = new Thickness(M(8)), Background = B(Raised), BorderBrush = B(Outline), BorderThickness = new Thickness(1, 0, 1, 1), CornerRadius = new CornerRadius(0, 0, Radius, Radius), Child = controls };
            var panel = new StackPanel { Width = grid.Width };
            void Colours(string title, IEnumerable<Color> colours)
            {
                var items = colours.ToArray(); if (items.Length == 0) return;
                var heading = Words(Build.Text(T(title), Text2, TextSize.Label), c => c.Text = T(title)); heading.Margin = new Thickness(M(8), M(8), M(8), M(4)); panel.Children.Add(heading);
                var swatches = new WrapPanel { Margin = new Thickness(M(8), 0, M(8), M(8)), ItemSpacing = M(4), LineSpacing = M(4) };
                foreach (var colour in items)
                {
                    string text = ColourText(colour);
                    var face = new StackPanel { Orientation = Orientation.Horizontal, Spacing = M(4), Children = { new Border { Width = M(16), Height = M(16), BorderThickness = new Thickness(1), BorderBrush = B(Outline), Background = new SolidColorBrush(colour) }, Build.Text(text, Text, TextSize.Caption) } };
                    var pick = new ActionButton { Content = face, Kind = ButtonKind.Ghost, Padding = new Thickness(M(4), 0) };
                    Words(pick, c => AutomationProperties.SetName(c, T("Use {0}", text))); ToolTip.SetTip(pick, text);
                    pick.Click += (_, _) => { hex.Text = text; hex.Error = null; grid.Value = colour; Apply(text); };
                    swatches.Children.Add(pick);
                }
                var scroll = new ScrollViewer { Content = swatches, MaxHeight = M(96), HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
                Words(scroll, c => AutomationProperties.SetName(c, T(title))); panel.Children.Add(scroll);
            }
            Colours("Form colours", FormColours()); Colours("Recent colours", recentDocumentColours);
            panel.Children.Add(grid); panel.Children.Add(footer);
            var card = new Border { Background = B(Raised), BorderBrush = B(Outline), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(Radius), Child = panel };
            var popup = new Popup { PlacementTarget = button, Placement = PlacementMode.BottomEdgeAlignedLeft, IsLightDismissEnabled = true, Child = card };
            helper = popup; Menu.OwnPopup(popup, button); popup.OverlayDismissEventPassThrough = false; ((ISetLogicalParent)popup).SetParent(button);
            grid.Picked += (colour, close) => { hex.Text = ColourText(colour); Apply(ColourText(colour)); };
            reset.Click += (_, _) => { Apply(null); popup.IsOpen = false; };
            hex.Edited += () =>
            {
                if (hex.Text.Length == 0) { Apply(null); grid.Value = (Preview(null) as ISolidColorBrush)!.Color; hex.Error = null; }
                else if (Color.TryParse(hex.Text, out var colour)) { Apply(ColourText(colour)); grid.Value = colour; hex.Error = null; }
                else hex.Error = T("Enter a valid colour.");
            };
            popup.Opened += (_, _) => hex.Box.Focus();
            popup.Closed += (_, _) =>
            {
                if (ReferenceEquals(document, colourDocument) && lastPicked is { } colour) RememberDocumentColour(colour);
                ((ISetLogicalParent)popup).SetParent(null); helper = null;
            };
            popup.IsOpen = true;
        };
        inspector.Children.Add(Labelled(label, button));
    }
}

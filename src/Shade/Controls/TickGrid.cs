using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using static Shade.Look;

namespace Shade;

/// <summary>A short option list with native checkbox rows. Picked preserves the order of selection.</summary>
public sealed class TickGrid : WrapPanel
{
    public static readonly DirectProperty<TickGrid, IReadOnlyList<string>> ItemsProperty = AvaloniaProperty.RegisterDirect<TickGrid, IReadOnlyList<string>>(nameof(Items), x => x.Items, (x, value) => x.Items = value);
    public static readonly DirectProperty<TickGrid, IReadOnlyList<string>> PickedProperty = AvaloniaProperty.RegisterDirect<TickGrid, IReadOnlyList<string>>(nameof(Picked), x => x.Picked, (x, value) => x.Picked = value, defaultBindingMode: BindingMode.TwoWay);
    IReadOnlyList<string> items = Array.Empty<string>(), picked = Array.Empty<string>();
    readonly List<(string key, CheckBox row)> rows = new();
    readonly double column;
    readonly Func<string, string>? name, detail;
    readonly bool switches;
    public event Action? Changed;
    public IReadOnlyList<string> Items { get => items; set { ArgumentNullException.ThrowIfNull(value); SetAndRaise(ItemsProperty, ref items, Array.AsReadOnly(value.Distinct(StringComparer.Ordinal).ToArray())); Rebuild(); } }
    public IReadOnlyList<string> Picked
    {
        get => picked;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            var next = value.Distinct(StringComparer.Ordinal).ToArray();
            if (picked.SequenceEqual(next)) return;
            SetAndRaise(PickedProperty, ref picked, Array.AsReadOnly(next));
            foreach (var (key, row) in rows) row.SetCurrentValue(ToggleButton.IsCheckedProperty, picked.Contains(key, StringComparer.Ordinal));
        }
    }
    public TickGrid() : this(Array.Empty<string>(), Array.Empty<string>()) { }
    public TickGrid(IEnumerable<string> items, IEnumerable<string> picked, double column = 150, Func<string, string>? name = null, bool switches = false, Func<string, string>? detail = null)
    {
        this.column = column; this.name = name; this.switches = switches; this.detail = detail;
        Picked = picked.ToArray(); Items = items.ToArray();
    }
    void Rebuild()
    {
        rows.Clear(); Children.Clear();
        foreach (var key in picked.Concat(items).Distinct(StringComparer.Ordinal))
        {
            ToggleButton face = switches ? ToggleSwitchTheme.Glyph() : CheckBoxTheme.Glyph(null); double faceWidth = M(switches ? 32 : 16);
            var label = Build.Text(name?.Invoke(key) ?? key, Look.Text, TextSize.Body);
            var words = new StackPanel { Orientation = Orientation.Horizontal, Spacing = M(12), Margin = new Thickness(M(8), 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Children = { label } };
            string? more = detail?.Invoke(key);
            if (!string.IsNullOrEmpty(more)) { label.Width = M(160); var caption = Build.Text(more, Text2, TextSize.Label); caption.FontFamily = Mono; words.Children.Add(caption); }
            var line = new StackPanel { Background = Avalonia.Media.Brushes.Transparent, Orientation = Orientation.Horizontal, Children = { face, words } }; words.Measure(Size.Infinity);
            var row = new CheckBox { IsChecked = picked.Contains(key), MinWidth = 0, MinHeight = 0, Padding = new Thickness(0), Width = Math.Max(M(column), faceWidth + words.DesiredSize.Width + M(8)), Height = ControlHeight, Template = new FuncControlTemplate<CheckBox>((_, _) => line), Cursor = Build.Hand };
            face[!ToggleButton.IsCheckedProperty] = row[!ToggleButton.IsCheckedProperty];
            FocusRing(row); AutomationProperties.SetName(row, label.Text); if (name != null) ToolTip.SetTip(row, key);
            row.Click += (_, _) => { var next = picked.Where(item => item != key).ToList(); if (row.IsChecked == true) next.Add(key); Picked = next; Changed?.Invoke(); };
            rows.Add((key, row)); Children.Add(row);
        }
    }
}

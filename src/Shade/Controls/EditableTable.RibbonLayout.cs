using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using static Shade.Look;

namespace Shade;

public sealed partial class EditableTable
{
    readonly RibbonFlow ribbonGroups = new();
    readonly List<TextBlock> ribbonGroupLabels = new();
    bool ribbonExpanded;

    static StackPanel RibbonField(string label, Control input, double minimum)
    {
        var group = (StackPanel)FormLayout.Field(label, input, M(minimum));
        Words((TextBlock)group.Children[0], text => { text.Text = T(label); SizeRibbonField(group, input, minimum); });
        return group;
    }

    static void SizeRibbonField(StackPanel group, Control input, double minimum)
    {
        var label = (TextBlock)group.Children[0];
        double width = Math.Max(M(minimum), Math.Ceiling(Look.Measure(label.Text ?? "", Ui(TextSize.Label, 500))));
        if (input is ChoiceButton choice)
            width = Math.Max(width, choice.Items.Append(choice.Current ?? "").Select(x => Math.Ceiling(Look.Measure(x, Ui(TextSize.Body))) + FormLayout.ChoiceChrome).DefaultIfEmpty(0).Max());
        group.Width = width;
    }

    void BuildRibbonGroups()
    {
        var names = new[] { "Column", "Column text", "Column width", "Table defaults and paper" };
        for (int i = 0; i < columnPanes.Count; i++)
        {
            var name = names[i];
            var label = Words(Build.Text("", Text2, TextSize.Label, 600), text => text.Text = T(name));
            ribbonGroupLabels.Add(label);
            var body = i == 3 ? tableEditorPane! : columnPanes[i];
            ribbonGroups.Children.Add(new StackPanel { Spacing = M(8), IsVisible = i == columnTabs.Active, Children = { label, body } });
        }
        ribbonGroups.Adapt = AdaptRibbon;
        columnRibbon.Children.Add(ribbonGroups);
        columnRibbon.GotFocus += (_, _) => ribbonGroups.InvalidateMeasure();
    }

    void AdaptRibbon(double width)
    {
        var content = columnPanes.Cast<Control>().ToArray(); content[3] = tableEditorPane!;
        var natural = content.Select((p, i) => { p.Measure(Size.Infinity); return Math.Max(p.DesiredSize.Width, Look.Measure(ribbonGroupLabels[i].Text ?? "", Ui(TextSize.Label, 600))); }).ToArray();
        // Keep a focused group available when a resize collapses the ribbon.
        int focus = ribbonGroups.Children.ToList().FindIndex(c => c.IsKeyboardFocusWithin);
        if (focus >= 0) columnTabs.Active = focus;
        double primary = natural.Take(3).Sum() + M(48);
        bool expanded = primary + (ribbonExpanded ? 0 : M(24)) <= width;
        ribbonExpanded = expanded;
        var visible = new bool[4]; visible[columnTabs.Active] = true;
        double used = expanded ? primary : natural[columnTabs.Active];
        if (expanded) Array.Fill(visible, true);
        else
        {
            foreach (int i in new[] { 0, 2, 1 })
            {
                if (visible[i]) continue;
                double extra = ribbonGroups.Children[i].IsVisible ? 0 : M(24);
                if (used + M(24) + natural[i] + extra <= width) { visible[i] = true; used += M(24) + natural[i]; }
            }
        }
        columnTabs.IsVisible = !expanded || columnTabs.IsKeyboardFocusWithin;
        ribbonGroups.LastOnOwnRow = expanded && used + M(24) + natural[3] > width;
        bool labels = visible.Count(x => x) > 1;
        for (int i = 0; i < visible.Length; i++) { ribbonGroupLabels[i].IsVisible = labels; ribbonGroups.Children[i].IsVisible = visible[i]; }
        // Hand focus to the expanded group before removing its tab stop.
        if (expanded && columnTabs.IsKeyboardFocusWithin)
            Dispatcher.UIThread.Post(() =>
            {
                if (!ribbonExpanded || !columnTabs.IsKeyboardFocusWithin) return;
                var target = ribbonGroups.Children[columnTabs.Active].GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Focusable && c.IsTabStop && c.IsEffectivelyEnabled && c.IsEffectivelyVisible);
                (target ?? editColumns).Focus(NavigationMethod.Tab);
                ribbonGroups.InvalidateMeasure();
            });
    }

    // Content-sized flow is local to the ribbon; form-card Masonry keeps its equal-column contract.
    sealed class RibbonFlow : Panel
    {
        public bool AlignLast { get; init; }
        public bool LastOnOwnRow { get; set; }
        public Action<double>? Adapt { get; set; }
        readonly Dictionary<Control, Rect> places = new();

        Size Place(double width)
        {
            if (double.IsFinite(width)) Adapt?.Invoke(width);
            var children = Children.Where(c => c.IsVisible).ToArray(); places.Clear();
            foreach (var child in children) child.Measure(Size.Infinity);
            if (!double.IsFinite(width)) width = children.Sum(c => c.DesiredSize.Width) + Math.Max(0, children.Length - 1) * M(16);
            double x = 0, y = 0, height = 0;
            foreach (var child in children)
            {
                bool last = child == children[^1];
                double w = Math.Min(width, child.DesiredSize.Width);
                if (children.Length == 1 || last && LastOnOwnRow) w = width;
                if (x > 0 && (x + w > width + 0.1 || last && LastOnOwnRow)) { x = 0; y += height + M(8); height = 0; }
                child.Measure(new Size(w, double.PositiveInfinity));
                double at = AlignLast && last ? Math.Max(x, width - w) : x;
                places[child] = new Rect(at, y, w, child.DesiredSize.Height);
                x += w + M(16); height = Math.Max(height, child.DesiredSize.Height);
            }
            return new Size(width, y + height);
        }
        protected override Size MeasureOverride(Size available) => Place(available.Width);
        protected override Size ArrangeOverride(Size final)
        {
            Place(final.Width);
            foreach (var child in Children) child.Arrange(places.GetValueOrDefault(child));
            return final;
        }
    }
}

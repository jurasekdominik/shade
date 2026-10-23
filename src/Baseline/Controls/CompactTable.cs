// Baseline - small configuration tables grow with their rows and use the page's scrolling.
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using static Baseline.Look;

namespace Baseline
{
    public sealed class CompactTable : Border
    {
        public CompactTable(string[] columns, IReadOnlyList<string[]> rows, Action<int> select = null, int selected = -1,
            Func<int, Control> actions = null, Func<int, int, Color?> color = null, Func<int, bool> faded = null, Func<int, Color?> background = null)
        {
            var stack = new StackPanel(); Child = stack;
            BorderBrush = B(Look.Border); BorderThickness = new Thickness(1);
            Border Row(string[] values, int index)
            {
                bool header = index < 0;
                var grid = new Grid { Height = FormLayout.RowHeight, ColumnDefinitions = new ColumnDefinitions(string.Join(",", columns.Select(_ => "*")) + (actions == null ? "" : ",Auto")) };
                for (int col = 0; col < columns.Length; col++)
                {
                    var text = Kit.Text(values.ElementAtOrDefault(col) ?? "", header ? Second : color?.Invoke(index, col) ?? Look.Text, header ? 9 : 9.75, header ? 500 : 400);
                    text.Margin = new Thickness(Kit.Gap, 0); text.TextTrimming = TextTrimming.CharacterEllipsis;
                    ToolTip.SetTip(text, text.Text); Grid.SetColumn(text, col); grid.Children.Add(text);
                }
                if (actions != null)
                {
                    var tools = header ? new Border { Width = FormLayout.RowHeight * 2 + Kit.Gap } : actions(index);
                    Grid.SetColumn(tools, columns.Length); grid.Children.Add(tools);
                }
                var row = new Border { Child = grid, BorderBrush = B(Look.Border), BorderThickness = new Thickness(0, 0, 0, index == rows.Count - 1 ? 0 : 1), Background = header ? B(Surface) : index == selected ? B(Tint(Accent)) : background?.Invoke(index) is Color rowBackground ? B(rowBackground) : Brushes.Transparent };
                if (!header && faded?.Invoke(index) == true) grid.Opacity = FormLayout.InactiveRowOpacity;
                if (!header && select != null)
                {
                    row.Cursor = Kit.Hand; row.Focusable = true;
                    Avalonia.Automation.AutomationProperties.SetName(row, string.Join(" · ", values));
                    row.KeyDown += (_, e) => { if (ReferenceEquals(e.Source, row) && (e.Key == Key.Enter || e.Key == Key.Space)) { e.Handled = true; select(index); } };
                    row.PointerReleased += (_, e) => { if (!e.Handled && e.InitialPressMouseButton == MouseButton.Left) select(index); };
                }
                return row;
            }
            stack.Children.Add(Row(columns, -1));
            for (int i = 0; i < rows.Count; i++) stack.Children.Add(Row(rows[i], i));
        }
    }
}

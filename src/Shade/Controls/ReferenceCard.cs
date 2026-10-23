// Shade - the reference card: documentation beside a form (syntax, placeholders, variables).
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using static Shade.Look;

namespace Shade
{
    /// <summary>A reference card (DESIGN_SYSTEM.md §6 Reference cards): Raised, 8 px corners, 12 px padding, a Section
    /// heading with an optional help action at its right, then columns of Mono codes and their meaning, a line of codes,
    /// examples to pick (chips: a click hands the example back), and a note. The one box inside a form: it isn't a setting.
    /// Built by chaining: new ReferenceCard("Syntax").Columns(…).Codes(…).Examples(…).Note(…).</summary>
    public sealed class ReferenceCard : Border
    {
        readonly StackPanel body = new StackPanel { Spacing = 8 };
        readonly DockPanel head = new DockPanel();

        public ReferenceCard(string title)
        {
            Background = B(Surface); BorderBrush = B(Look.Border); BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(CardRadius); Padding = new Thickness(12);
            head.Children.Add(Kit.Text(title, Look.Text, 10.5, 600));
            body.Children.Add(head);
            Child = body;
        }

        /// <summary>A ghost help action at the heading's right (Full syntax, Open the docs).</summary>
        public ReferenceCard Help(string text, Action open, string? tip = null)
        {
            var b = new Btn(text, BtnKind.Ghost, icon: Icons.CircleHelp) { MinWidth = 0, Height = 24, Padding = new Thickness(6, 0), Margin = new Thickness(0, -4, -6, 0) };
            if (tip != null) ToolTip.SetTip(b, tip);
            b.Click += open;
            DockPanel.SetDock(b, Dock.Right); head.Children.Insert(0, b);
            return this;
        }

        /// <summary>Tables of a code and its meaning, side by side (one array a column).</summary>
        public ReferenceCard Columns(params (string code, string meaning)[][] tables)
        {
            var row = new Grid();
            for (int c = 0; c < tables.Length; c++)
            {
                if (c > 0) row.ColumnDefinitions.Add(new ColumnDefinition(12, GridUnitType.Pixel));
                row.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
                var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
                for (int i = 0; i < tables[c].Length; i++)
                {
                    g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                    var a = Kit.MonoText(tables[c][i].code, Look.Text, 12); a.Margin = new Thickness(0, 1, 8, 1);
                    var b = Kit.Text(tables[c][i].meaning, Second, 9.75); b.Margin = new Thickness(0, 1);
                    Grid.SetRow(a, i); Grid.SetRow(b, i); Grid.SetColumn(b, 1); g.Children.Add(a); g.Children.Add(b);
                }
                Grid.SetColumn(g, c * 2); row.Children.Add(g);
            }
            body.Children.Add(row);
            return this;
        }

        /// <summary>A line of codes (macros, keywords), wrapped.</summary>
        public ReferenceCard Codes(params string[] codes)
        {
            var t = Kit.MonoText(string.Join("  ", codes), Look.Text, 12); t.TextWrapping = TextWrapping.Wrap; t.TextTrimming = TextTrimming.None;
            body.Children.Add(t);
            return this;
        }

        /// <summary>Examples as chips; a click hands one to use (it fills the field the card documents). tip: each one in words.</summary>
        public ReferenceCard Examples(IEnumerable<string> examples, Action<string> use, Func<string, string>? tip = null)
        {
            var wrap = new WrapPanel();
            foreach (var e in examples)
            {
                var chip = new FilterChip(e) { Margin = new Thickness(0, 0, 4, 4) };
                if (tip != null) ToolTip.SetTip(chip, tip(e));
                chip.Click += () => use?.Invoke(e);
                wrap.Children.Add(chip);
            }
            body.Children.Add(wrap);
            return this;
        }

        /// <summary>One line under it all: a rule the codes don't show.</summary>
        public ReferenceCard Note(string text)
        {
            var t = Kit.Text(text, Second, 9); t.TextWrapping = TextWrapping.Wrap; t.TextTrimming = TextTrimming.None;
            body.Children.Add(t);
            return this;
        }
    }
}

// Shade - shared form layout: page and sub-page headers, blocks, labelled fields and the responsive Masonry columns.
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using static Shade.Look;

namespace Shade
{
    /// <summary>Shared compact form vocabulary. Controls keep their validation and event handlers.</summary>
    public static class FormLayout
    {
        public const double Gap = 12, InactiveRowOpacity = 0.65;
        public const double RowHeight = 28, TextAreaHeight = 112, EditorColumn = 300, OptionColumn = 100, StepFormatWidth = 220;
        public const double BuilderWidth = 1280, BuilderHeight = 820, BuilderMinWidth = 800, BuilderMinHeight = 540;
        public const double BuilderShelf = 220, BuilderInspector = 280, BuilderWide = 1120, BuilderCell = 156;
        public const double BuilderCanvas = 440, ReportCellMin = 72, ReportCellMax = 360, ChoiceChrome = 34;
        public const int PreviewRows = 8;
        public static void Detach(Control control)
        {
            if (control.Parent is Panel panel) panel.Children.Remove(control);
            else if (control.Parent is Decorator decorator) decorator.Child = null;
            else if (control.Parent is ContentControl content) content.Content = null;
        }
        /// <summary>The page header (DESIGN_SYSTEM.md §8): a 28 px title row with the actions right-aligned on it (primary last),
        /// the description under it. Every page uses it, so titles and actions sit on the same pixels everywhere.</summary>
        public static Control Header(string title, string? hint, params Control[] actions) => Head(null, title, hint, actions);

        /// <summary>A subpage's header: a back button (the parent's name) at the fixed left of the title row.</summary>
        public static Control SubHeader(string parent, Action back, string title, string hint, params Control[] actions)
        {
            var b = new Btn(parent, BtnKind.Ghost, icon: Icons.ChevronLeft) { MinWidth = 0, Padding = new Thickness(4, 0, 8, 0), Margin = new Thickness(-4, 0, 8, 0) };
            ToolTip.SetTip(b, "Back to " + parent); b.Click += back;
            return Head(b, title, hint, actions);
        }

        static Control Head(Btn? back, string title, string? hint, Control[] actions)
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), RowDefinitions = new RowDefinitions("28,Auto"), Margin = new Thickness(0, 0, 0, 16) };
            if (back != null) grid.Children.Add(back);
            var heading = Kit.Text(title, Look.Text, 13.5, 600); Grid.SetColumn(heading, 1); grid.Children.Add(heading);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(16, 0, 0, 0) };
            foreach (var action in actions) buttons.Children.Add(action);
            Grid.SetColumn(buttons, 2); grid.Children.Add(buttons);
            if (!string.IsNullOrEmpty(hint))
            {
                var note = Kit.Text(hint, Second, 9); note.TextWrapping = TextWrapping.Wrap; note.Margin = new Thickness(0, 4, 0, 0);
                Grid.SetRow(note, 1); Grid.SetColumnSpan(note, 3); grid.Children.Add(note);
            }
            return grid;
        }
        /// <summary>A numbered chapter of a long page ("01 / Colors"): the number in Text 3, the title, one line of what it
        /// holds, and a full-width line under it. Its blocks follow in a Masonry.</summary>
        public static Control Chapter(int number, string title, string note)
        {
            var head = new StackPanel { Spacing = 4 };
            var name = new TextBlock { FontSize = Look.Title, FontWeight = FontWeight.SemiBold };
            name.Inlines!.Add(new Avalonia.Controls.Documents.Run(number.ToString("00") + " / ") { Foreground = B(Faint) });
            name.Inlines.Add(new Avalonia.Controls.Documents.Run(title) { Foreground = B(Look.Text) });
            head.Children.Add(name);
            if (note != null) { var n = Kit.Text(note, Second, 9.75); n.TextWrapping = TextWrapping.Wrap; head.Children.Add(n); }
            return new Border { Child = head, BorderBrush = B(Look.Border), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 0, 0, 12), Margin = new Thickness(0, 0, 0, 20) };
        }

        /// <summary>A block of a page (DESIGN_SYSTEM.md §9): its heading and at most one line of help, then its body. No box:
        /// the space around it groups it. Blocks sit in a Masonry, which sizes them.</summary>
        public static Border Block(string title, string help, Control body, bool line = true)
        {
            var stack = new StackPanel { Spacing = 6 };
            var head = new StackPanel { Spacing = 2 };
            head.Children.Add(Kit.Text(title, Look.Text, 10.5, 600));
            if (help != null) { var h = Kit.Text(help, Faint, 8.5); h.TextWrapping = TextWrapping.Wrap; head.Children.Add(h); }
            stack.Children.Add(head); stack.Children.Add(body);
            return new Border { Child = stack, BorderBrush = B(Hairline), BorderThickness = new Thickness(0, line ? 1 : 0, 0, 0), Padding = new Thickness(0, line ? 12 : 0, 0, 0) };  // a faint line above: blocks apart without boxes
        }

        public static Control Disclosure(string title, Func<Control> build)
        {
            var panel = new StackPanel { Spacing = Kit.Gap };
            var button = new Btn(title, BtnKind.Ghost, icon: Icons.ChevronDown) { HorizontalAlignment = HorizontalAlignment.Left };
            var body = new Border { IsVisible = false };
            button.Click += () => { body.Child ??= build(); body.IsVisible = !body.IsVisible; button.Text = body.IsVisible ? "Hide " + title : title; };
            panel.Children.Add(button); panel.Children.Add(body); return panel;
        }

        public static Control Field(string label, Control input, double width = 240)
        {
            Detach(input);
            if (input is Switch) input.HorizontalAlignment = HorizontalAlignment.Left;
            else { input.Width = double.NaN; input.HorizontalAlignment = HorizontalAlignment.Stretch; }
            var stack = new StackPanel { Width = width, Spacing = 4, Margin = new Thickness(0, 0, 16, 12), IsVisible = input.IsVisible }; stack.Children.Add(Kit.Text(label, Second, 9)); stack.Children.Add(input);  // §4: label 4, fields 16 across, 12 down
            input.PropertyChanged += (s, e) => { if (e.Property == Control.IsVisibleProperty) stack.IsVisible = input.IsVisible; }; return stack;
        }
    }

    /// <summary>Responsive columns, like a CSS masonry grid: as many columns as fit at MinColumn wide (1 to MaxColumns), each
    /// child in the shortest column so far. Resizing the window moves the blocks.</summary>
    public sealed class Masonry : Panel
    {
        public static readonly StyledProperty<double> MinColumnProperty = AvaloniaProperty.Register<Masonry, double>(nameof(MinColumn), 300);
        public static readonly StyledProperty<double> GapProperty = AvaloniaProperty.Register<Masonry, double>(nameof(Gap), 32);
        public static readonly StyledProperty<double> RowGapProperty = AvaloniaProperty.Register<Masonry, double>(nameof(RowGap), 20);
        public static readonly StyledProperty<int> MaxColumnsProperty = AvaloniaProperty.Register<Masonry, int>(nameof(MaxColumns), 3);
        static Masonry() => AffectsMeasure<Masonry>(MinColumnProperty, GapProperty, RowGapProperty, MaxColumnsProperty);
        public double MinColumn { get => GetValue(MinColumnProperty); set => SetValue(MinColumnProperty, value); }
        public double Gap { get => GetValue(GapProperty); set => SetValue(GapProperty, value); }
        public double RowGap { get => GetValue(RowGapProperty); set => SetValue(RowGapProperty, value); }
        public int MaxColumns { get => GetValue(MaxColumnsProperty); set => SetValue(MaxColumnsProperty, value); }
        readonly List<Rect> places = new List<Rect>();

        Size Place(double width)
        {
            places.Clear();
            if (double.IsInfinity(width)) width = MaxColumns * (MinColumn + Gap) - Gap;
            int cols = Math.Clamp((int)((width + Gap) / (MinColumn + Gap)), 1, MaxColumns);
            double w = (width - Gap * (cols - 1)) / cols;
            var heights = new double[cols];
            foreach (var child in Children)
            {
                if (!child.IsVisible) { places.Add(default); continue; }
                child.Measure(new Size(w, double.PositiveInfinity));
                int col = Array.IndexOf(heights, heights.Min());
                places.Add(new Rect(col * (w + Gap), heights[col], w, child.DesiredSize.Height));
                heights[col] += child.DesiredSize.Height + RowGap;
            }
            return new Size(width, Math.Max(0, heights.Max() - RowGap));
        }

        /// <summary>Arranged at another width than offered in Measure (a scroll viewer's padding or bar): the width the blocks
        /// really get, so they are measured at it and the scroll height matches what is drawn.</summary>
        double arranged = double.NaN, offered = double.NaN;

        protected override Size MeasureOverride(Size available)
        {
            if (available.Width != offered) { offered = available.Width; arranged = double.NaN; }  // resized: start from what is offered
            return Place(double.IsNaN(arranged) ? available.Width : arranged);
        }

        protected override Size ArrangeOverride(Size final)
        {
            double measured = double.IsNaN(arranged) ? offered : arranged;
            if (Math.Abs(final.Width - measured) > 0.5) { arranged = final.Width; InvalidateMeasure(); }  // once: the next measure uses this width
            var used = Place(final.Width);
            for (int i = 0; i < Children.Count; i++) Children[i].Arrange(places[i]);
            return new Size(final.Width, Math.Max(final.Height, used.Height));
        }
    }
}

// Shade - Build: the building blocks every page uses (texts, cards, groups, labelled rows, empty states, enter and rise animations).
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
using static Shade.Look;

namespace Shade
{
    public static class Build
    {
        /// <summary>The top of every main page: fields at TopY, buttons at TopBtnY, the page's status line at NoteY, column
        /// titles at TitleY, the table card at CardY; M from the window's sides.</summary>
        public const double M = 16, TopY = 12, NoteY = 46, TitleY = 68, CardY = 90;
        internal const double TopBtnY = 12;
        /// <summary>The 8 px spacing grid: heading to its card, inside a card, between cards and at the edges.</summary>
        public const double Gap = 8, Pad = 16, Space = 24;
        public static readonly Cursor Hand = new Cursor(StandardCursorType.Hand);

        public static TextBlock Text(string s, Tone c, double size = 0, int weight = 400) => new TextBlock
        {
            Text = s,
            Foreground = B(c),
            FontFamily = UiFont,
            FontSize = size == 0 ? TextSize.Section : size,
            FontWeight = Weight(weight),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        /// <summary>A search's matches in a result (DESIGN_SYSTEM.md §6 Search): text in t, each match of query's words (any case)
        /// in readable accent text on the accent tint and in 600; case and accents are ignored. No query, plain text.</summary>
        public static void Mark(TextBlock t, string text, string? query)
        {
            text ??= "";
            var terms = (query ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (terms.Length == 0) { t.Inlines = null; t.Text = text; return; }
            var hits = new bool[text.Length];
            foreach (var term in terms)
            {
                int start = 0;
                while (start < text.Length)
                {
                    int hit = Culture.CompareInfo.IndexOf(text.AsSpan(start), term.AsSpan(), System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace, out int length);
                    if (hit < 0 || length == 0) break;
                    int end = start + hit + length;
                    Array.Fill(hits, true, start + hit, length);
                    start = end;
                }
            }
            var inlines = new Avalonia.Controls.Documents.InlineCollection();
            int at = 0;
            while (at < text.Length)
            {
                int end = at + 1;
                while (end < text.Length && hits[end] == hits[at]) end++;
                var run = new Avalonia.Controls.Documents.Run(text.Substring(at, end - at));
                if (hits[at]) { run.Background = B(AccentTint); run.Foreground = B(AccentText); run.FontWeight = Weight(600); }
                inlines.Add(run); at = end;
            }
            t.Text = null; t.Inlines = inlines;
        }

        /// <summary>The embedded monospace font, sized in pixels (Look.Mono).</summary>
        public static TextBlock MonoText(string s, Tone c, double px, bool bold = false) => new TextBlock
        {
            Text = s,
            Foreground = B(c),
            FontFamily = Mono,
            FontFeatures = NoLigatures,
            FontSize = px,
            FontWeight = bold ? FontWeight.Bold : FontWeight.Normal,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        public static void Set(this TextBlock t, string s, Tone c) { t.Text = s; t.Foreground = B(c); }

        /// <summary>A flat section, bounded by one bottom separator.</summary>
        public static Border Card(Control? child = null, Thickness? pad = null) => new Border
        {
            Background = Brushes.Transparent,
            BorderBrush = B(Look.Separator),
            BorderThickness = new Thickness(0, 0, 0, 1),
            CornerRadius = new CornerRadius(0),
            Padding = pad ?? new Thickness(Pad, 12),
            Child = child,
        };

        /// <summary>An uppercase section label over a card.</summary>
        public static StackPanel Group(string section, Control content)
        {
            var p = new StackPanel { Spacing = Gap };
            p.Children.Add(Text(section, Look.Text, TextSize.Section, 600));
            p.Children.Add(Card(content, new Thickness(Pad)));
            return p;
        }

        /// <summary>A labelled field.</summary>
        public static StackPanel Labelled(string label, Control field)
        {
            var p = new StackPanel { Spacing = 2 };
            var caption = Text(label, Text2, TextSize.Label);
            p.Children.Add(caption);
            Avalonia.Automation.AutomationProperties.SetLabeledBy(field is Field f ? f.AccessibleInput : field, caption);
            p.Children.Add(field);
            if (field is Field input)
            {
                var error = Text("", DangerText, TextSize.Caption);
                void Update()
                {
                    error.Text = input.ValidationMessage;
                    error.IsVisible = !string.IsNullOrEmpty(error.Text);
                    Avalonia.Automation.AutomationProperties.SetHelpText(input.AccessibleInput, error.Text ?? "");
                }
                input.ValidationChanged += Update;
                Update(); p.Children.Add(error);
            }
            return p;
        }

        /// <summary>x and width of each column (widths, 0 = takes the rest, at least 100) across width, 6 apart.</summary>
        public static (double x, double w)[] Columns(int[] widths, double width)
        {
            double gap = 6, fixedW = widths.Sum() + gap * (widths.Length - 1), rest = Math.Max(100, width - fixedW), x = 0;
            var r = new (double, double)[widths.Length];
            for (int i = 0; i < widths.Length; i++) { double w = widths[i] == 0 ? rest : widths[i]; r[i] = (x, w); x += w + gap; }
            return r;
        }

        /// <summary>Column titles over a table: uppercase, faint, at the columns' x.</summary>
        public static Canvas Titles(string[] titles, Func<double, (double x, double w)[]> columns)
        {
            var c = new Canvas { Height = 18 };
            var labels = titles.Select(t => Text(t, Text3, TextSize.Caption, 600)).ToArray();
            foreach (var l in labels) c.Children.Add(l);
            c.SizeChanged += (s, e) =>
            {
                var cols = columns(e.NewSize.Width);
                for (int i = 0; i < labels.Length; i++) { Canvas.SetLeft(labels[i], cols[i].x); labels[i].Width = cols[i].w; labels[i].Height = 18; }
            };
            return c;
        }

        /// <summary>A row arriving: it fades in and rises 6 px into place (220 ms, ease-out); order staggers a list's rows
        /// (25 ms apart, at most 10 steps) so they settle one after the other.</summary>
        public static void Enter(Control c, int order = 0)
        {
            if (ReducedMotion) return;
            var ease = new Avalonia.Animation.Easings.CubicEaseOut();
            var delay = TimeSpan.FromMilliseconds(25 * Math.Min(order, 10));
            c.Opacity = 0;
            c.RenderTransform = Avalonia.Media.Transformation.TransformOperations.Parse("translateY(6px)");
            c.Transitions = new Avalonia.Animation.Transitions
            {
                new Avalonia.Animation.DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(220), Delay = delay, Easing = ease },
                new Avalonia.Animation.TransformOperationsTransition { Property = Visual.RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(220), Delay = delay, Easing = ease },
            };
            void Go(object? s, VisualTreeAttachmentEventArgs e)
            {
                c.AttachedToVisualTree -= Go;
                Dispatcher.UIThread.Post(() => { c.Opacity = 1; c.RenderTransform = Avalonia.Media.Transformation.TransformOperations.Parse("translateY(0px)"); }, DispatcherPriority.Background);
            }
            c.AttachedToVisualTree += Go;
        }

        /// <summary>A dialog's content as it opens: fades in and grows from 97 % (160 ms), as a macOS sheet.</summary>
        internal static void Rise(Control c)
        {
            if (ReducedMotion) return;
            var ease = new Avalonia.Animation.Easings.CubicEaseOut();
            c.Opacity = 0;
            c.RenderTransform = Avalonia.Media.Transformation.TransformOperations.Parse("scale(0.97)");
            c.Transitions = new Avalonia.Animation.Transitions
            {
                new Avalonia.Animation.DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(160), Easing = ease },
                new Avalonia.Animation.TransformOperationsTransition { Property = Visual.RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(160), Easing = ease },
            };
            Dispatcher.UIThread.Post(() => { c.Opacity = 1; c.RenderTransform = Avalonia.Media.Transformation.TransformOperations.Parse("scale(1)"); }, DispatcherPriority.Background);
        }

        /// <summary>An empty list: what's missing, a line on what to do, and the button that does it, centred in the card.</summary>
        public static StackPanel Empty(string title, string note, string action, Action act)
        {
            var p = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 6, Margin = new Thickness(0, 40) };
            var t = Text(title, Look.Text, TextSize.Section, 600); t.HorizontalAlignment = HorizontalAlignment.Center;
            var n = Text(note, Text3, TextSize.Label); n.HorizontalAlignment = HorizontalAlignment.Center; n.TextAlignment = TextAlignment.Center;
            n.TextWrapping = TextWrapping.Wrap; n.TextTrimming = TextTrimming.None; n.MaxWidth = 460;
            p.Children.Add(t); p.Children.Add(n);
            if (action != null)
            {
                var b = new ActionButton(action) { Height = ControlHeight, MinWidth = 140, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
                b.Click += (_, _) => act();
                p.Children.Add(b);
            }
            return p;
        }

        /// <summary>A width that moves to each new value over 100 ms (the countdown pills tick every 100 ms): smooth, not stepped.</summary>
        /// <summary>A field's frame with its list attached below it (open) or on its own: open = square bottom corners and no
        /// bottom line, whose pixel goes to the padding, so the content doesn't move.</summary>
        internal static void Attached(Border b, bool open, Thickness padding)
        {
            b.CornerRadius = open ? new CornerRadius(4, 4, 0, 0) : new CornerRadius(4);
            b.BorderThickness = open ? new Thickness(1, 1, 1, 0) : new Thickness(1);
            b.Padding = open ? new Thickness(padding.Left, padding.Top, padding.Right, padding.Bottom + 1) : padding;
        }

        /// <summary>Whether the pointer is over c while c's attached list is open, and just after: the open list hides c from the
        /// pointer, a press on c closes the list, and the pointer is back over c without c being told until it moves (its
        /// hover border would come only then). Call at opening; the returned action at closing. hot gets the answers.</summary>
        internal static Action WatchHover(Control c, Action<bool> hot)
        {
            var top = TopLevel.GetTopLevel(c);
            bool Over(PointerEventArgs e) => new Rect(c.Bounds.Size).Contains(e.GetPosition(c));
            EventHandler<PointerPressedEventArgs> press = (s, e) => hot(Over(e));
            top?.AddHandler(InputElement.PointerPressedEvent, press, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
            return () =>
            {
                top?.RemoveHandler(InputElement.PointerPressedEvent, press);
                // and the next move says it again: leaving in one jump raises no PointerExited on a control never entered
                EventHandler<PointerEventArgs>? moved = null;
                moved = (s, e) => { top?.RemoveHandler(InputElement.PointerMovedEvent, moved!); hot(Over(e)); };
                top?.AddHandler(InputElement.PointerMovedEvent, moved, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
            };
        }

        public static Avalonia.Animation.Transitions Glide() => new Avalonia.Animation.Transitions
        {
            new Avalonia.Animation.DoubleTransition { Property = Layoutable.WidthProperty, Duration = TimeSpan.FromMilliseconds(100) },
        };

        /// <summary>A stroked icon drawn on a 24-unit grid (Tabler icons), size px square: the shape keeps its margins inside the
        /// box instead of being stretched to fill it.</summary>
        public static Avalonia.Controls.Shapes.Path Icon24(Geometry data, double size, double stroke = 1.8)
        {
            double k = size / 24;
            return new Avalonia.Controls.Shapes.Path
            {
                Data = data,
                Stretch = Stretch.None,
                Width = 24,
                Height = 24,
                Margin = new Thickness(-12 * (1 - k)),  // laid out at its scaled size
                StrokeThickness = Math.Max(1.2, stroke * k) / k,
                StrokeLineCap = PenLineCap.Round,
                StrokeJoin = PenLineJoin.Round,
                RenderTransform = new ScaleTransform(k, k),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
        }

        // ---- fixed layouts (the settings and library pages): coordinates on a Canvas -------------------------------------
        public static T Put<T>(Canvas host, T c, double x, double y, double w, double h) where T : Control
        {
            Canvas.SetLeft(c, x); Canvas.SetTop(c, y);
            c.Width = w; c.Height = h;
            // a one-line label is centred in its box, so a switch's label sits level with the switch
            if (c is TextBlock t && t.TextWrapping == TextWrapping.NoWrap && t.VerticalAlignment == VerticalAlignment.Center)
                t.Padding = new Thickness(0, Math.Max(0, (h - t.FontSize * 1.33) / 2), 0, 0);
            host.Children.Add(c);
            return c;
        }

        /// <summary>A label of several lines ("\n"), top-aligned.</summary>
        public static TextBlock Lines(string s, Tone c, double size = 0)
        {
            var t = Text(s, c, size == 0 ? TextSize.Label : size);
            t.VerticalAlignment = VerticalAlignment.Top;
            t.TextTrimming = TextTrimming.None;
            t.TextWrapping = TextWrapping.Wrap;
            return t;
        }

        /// <summary>A thin scroll list (StackList): rows touching, scrolled by the wheel.</summary>
        public static ScrollViewer List(Panel rows)
        {
            rows.Margin = new Thickness(rows.Margin.Left, rows.Margin.Top, Math.Max(rows.Margin.Right, 14), rows.Margin.Bottom);  // clear of the scroll bar, which lies over the content
            return new ScrollViewer { Content = rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        }
    }
}

// Shade - shared control: the log's text (the main log, a WRITE window's log).
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using Avalonia.Animation;
using Avalonia.Controls.Documents;
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
    /// <summary>The log's text (the main log, a WRITE window's log): wrapped lines in the mono font, coloured by level.
    /// Drag selects as usual; a double-click selects the whole line, Shift+click takes whole lines from the selection's
    /// first line to the clicked one, Ctrl+A everything; Ctrl+C copies.</summary>
    public sealed class LogText : SelectableTextBlock
    {
        protected override Type StyleKeyOverride => typeof(SelectableTextBlock);
        readonly List<int> starts = new List<int>();  // where each line begins in the text (each ends with "\n")
        int length;

        public LogText()
        {
            FontFamily = Mono; FontFeatures = NoLigatures; FontSize = Pt(9.5); Foreground = B(Second);
            SelectionBrush = B(Alpha(Accent, .45)); TextWrapping = TextWrapping.Wrap;
            Inlines = new InlineCollection();
        }

        public void Clear() { Inlines.Clear(); starts.Clear(); length = 0; ClearSelection(); }

        /// <summary>Past KeepLines the oldest Trim lines go, so a window open for weeks keeps a bounded log.</summary>
        const int KeepLines = 3000, Trim = 500;

        public void Add(string line, Color c)
        {
            if (starts.Count >= KeepLines)
            {
                int cut = starts[Trim];
                Inlines.RemoveRange(0, Trim);
                starts.RemoveRange(0, Trim);
                for (int i = 0; i < starts.Count; i++) starts[i] -= cut;
                length -= cut;
                ClearSelection();
            }
            starts.Add(length);
            line += "\n";
            length += line.Length;
            Inlines.Add(new Run(line) { Foreground = B(c) });
        }

        int LineAt(int pos) { int i = starts.BinarySearch(pos); return i >= 0 ? i : Math.Max(0, ~i - 1); }
        int EndOf(int line) => (line + 1 < starts.Count ? starts[line + 1] : length) - 1;  // before its "\n"

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            var pt = e.GetCurrentPoint(this);
            bool lines = e.ClickCount == 2 || e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            if (!pt.Properties.IsLeftButtonPressed || starts.Count == 0 || !lines) { base.OnPointerPressed(e); return; }
            int at = LineAt(TextLayout.HitTestPoint(e.GetPosition(this)).TextPosition);
            int from = e.ClickCount == 2 || SelectionStart == SelectionEnd ? at : LineAt(Math.Min(SelectionStart, SelectionEnd));
            int a = Math.Min(from, at), b = Math.Max(from, at);
            SelectionStart = starts[a]; SelectionEnd = EndOf(b);
            Focus();
            e.Handled = true;
        }
    }
}

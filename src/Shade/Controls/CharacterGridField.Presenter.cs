using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace Shade;

public sealed partial class CharacterGridField
{
    /// <summary>Only supplies native shaped runs; TextPresenter still owns hit testing, selection, caret and IME.</summary>
    internal sealed class GridPresenter(CharacterGridField field) : TextPresenter
    {
        internal double Inset { get; private set; }
        internal void Refresh()
        {
            string text = field.Text ?? "";
            int position = Math.Clamp(field.CaretIndex, 0, text.Length);
            if (!string.IsNullOrEmpty(PreeditText)) { text = text.Insert(position, PreeditText); position += PreeditTextCursorPosition ?? PreeditText.Length; }
            var starts = StringInfo.ParseCombiningCharacters(text);
            int shift = field.Shift(starts.Length), cell = starts.Count(i => i < position);
            string element = cell < starts.Length ? text.Substring(starts[cell], (cell + 1 < starts.Length ? starts[cell + 1] : text.Length) - starts[cell]) : "0";
            using var measured = new TextLayout(element, new Typeface(field.FontFamily, field.FontStyle, field.FontWeight, field.FontStretch), field.FontSize, field.Foreground, fontFeatures: field.FontFeatures);
            double room = Math.Max(0, field.CellSize.Width - field.CellPadding.Left - field.CellPadding.Right - measured.WidthIncludingTrailingWhitespace);
            double inset = cell + shift >= field.CellCount ? 0 : field.CellPadding.Left + (field.CharacterAlignment == TextAlignment.Right ? room : field.CharacterAlignment == TextAlignment.Center ? room / 2 : 0);
            // the shift moves text, caret and hit testing together; a click in the empty cells before it lands at the start
            // Layout rounds a fractional margin to device pixels while glyph offsets stay exact, so a margin that changes with the caret
            // cell moved every glyph by up to half a pixel. Round it here and hand the exact remainder to the glyph offsets.
            double scale = TopLevel.GetTopLevel(field)?.RenderScaling ?? 1, left = Math.Round((inset + shift * field.Pitch) * scale) / scale;
            Inset = left - shift * field.Pitch;
            // the glyphs sit Inset inside their cells, so the text overhangs the grid by that much when it fills it; a negative right margin
            // keeps the extent equal to the viewport, or the field would scroll its cells away from the frame
            Margin = new Thickness(left, field.CellPadding.Top, -Math.Max(0, Inset), field.CellPadding.Bottom);
            InvalidateTextLayout();
        }
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == PreeditTextProperty || change.Property == PreeditTextCursorPositionProperty) Refresh();
        }
        protected override TextLayout CreateTextLayout()
        {
            string text = Text ?? "";
            int insertion = Math.Clamp(CaretIndex, 0, text.Length);
            if (!string.IsNullOrEmpty(PreeditText)) text = text.Insert(insertion, PreeditText);
            var properties = new GenericTextRunProperties(new Typeface(field.FontFamily, field.FontStyle, field.FontWeight, field.FontStretch), field.FontSize, foregroundBrush: field.Foreground, fontFeatures: field.FontFeatures);
            return new TextLayout(new CellTextSource(field, this, text, insertion, PreeditText?.Length ?? 0), new GenericTextParagraphProperties(properties));
        }
    }

    sealed class CellTextSource : ITextSource
    {
        readonly Dictionary<int, TextRun> runs = new();
        internal CellTextSource(CharacterGridField field, GridPresenter presenter, string text, int preeditStart, int preeditLength)
        {
            var starts = StringInfo.ParseCombiningCharacters(text);
            int shift = field.Shift(starts.Length);
            for (int cell = 0; cell < starts.Length; cell++)
            {
                int start = starts[cell], end = cell + 1 < starts.Length ? starts[cell + 1] : text.Length;
                var ink = field.Decoration(cell + shift, d => d.Foreground) ?? field.Foreground;
                if (presenter.ShowSelectionHighlight && start >= Math.Min(presenter.SelectionStart, presenter.SelectionEnd) && end <= Math.Max(presenter.SelectionStart, presenter.SelectionEnd)) ink = presenter.SelectionForegroundBrush ?? ink;
                bool composing = preeditLength > 0 && start < preeditStart + preeditLength && end > preeditStart;
                string element = text.Substring(start, end - start);
                string displayed = element is "\r" or "\n" or "\r\n" ? "↵" : element == "\t" ? "⇥" : element;
                using var shaped = new TextLayout(displayed, new Typeface(field.FontFamily, field.FontStyle, field.FontWeight, field.FontStretch), field.FontSize, ink,
                    textDecorations: composing ? TextDecorations.Underline : null, fontFeatures: field.FontFeatures);
                var parts = shaped.TextLines.SelectMany(line => line.TextRuns).OfType<ShapedTextRun>().ToArray();
                double natural = parts.Sum(part => part.ShapedBuffer.Sum(g => g.GlyphAdvance));
                double advance = field.CellSize.Width + (cell + shift == field.CellCount - 1 ? 0 : field.CellGap);
                double room = Math.Max(0, field.CellSize.Width - field.CellPadding.Left - field.CellPadding.Right - natural);
                double offset = field.CellPadding.Left + (field.CharacterAlignment == TextAlignment.Right ? room : field.CharacterAlignment == TextAlignment.Center ? room / 2 : 0) - presenter.Inset;
                int position = start;
                for (int partIndex = 0; partIndex < parts.Length; partIndex++)
                {
                    var part = parts[partIndex]; var original = part.ShapedBuffer;
                    int length = partIndex == parts.Length - 1 ? end - position : part.Length;
                    var buffer = new ShapedBuffer(text.AsMemory(position, length), original.Length, original.GlyphTypeface, original.FontRenderingEmSize, 0);
                    for (int glyphIndex = 0; glyphIndex < original.Length; glyphIndex++)
                    {
                        var glyph = original[glyphIndex];
                        bool last = partIndex == parts.Length - 1 && glyphIndex == original.Length - 1;
                        buffer[glyphIndex] = new GlyphInfo(glyph.GlyphIndex, start,
                            glyph.GlyphAdvance + (last ? advance - natural : 0), glyph.GlyphOffset + new Vector(offset, 0));
                    }
                    runs[position] = new ShapedTextRun(buffer, part.Properties);
                    position += length;
                }
            }
        }
        public TextRun? GetTextRun(int textSourceIndex) => runs.TryGetValue(textSourceIndex, out var run) ? run : new TextEndOfParagraph();
    }
}

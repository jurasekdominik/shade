using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace Shade;

// Native editing owns selection, IME and undo; only reject growth beyond the paper viewport.
internal sealed class PaperTextBox : TextBox
{
    protected override Type StyleKeyOverride => typeof(TextBox);
    internal bool ConstrainInput;
    public PaperTextBox() => PastingFromClipboard += (_, e) => { if (!ConstrainInput) return; e.Handled = true; PasteGuard.Paste(this, () => ConstrainInput); };
    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (ConstrainInput && !e.Handled && !Accepts(e.Text ?? "")) { e.Handled = true; return; }
        base.OnTextInput(e);
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Native TextBox inserts Enter directly, without raising TextInput.
        if (ConstrainInput && !e.Handled && e.Key == Key.Enter && AcceptsReturn && !Accepts(Environment.NewLine)) { e.Handled = true; return; }
        base.OnKeyDown(e);
    }
    bool Accepts(string insertion)
    {
        if (IsReadOnly || !IsEffectivelyEnabled) return false;
        double width = Math.Min(Bounds.Width, double.IsFinite(Width) ? Width : Bounds.Width) - Padding.Left - Padding.Right - BorderThickness.Left - BorderThickness.Right;
        double height = Math.Min(Bounds.Height, double.IsFinite(Height) ? Height : Bounds.Height) - Padding.Top - Padding.Bottom - BorderThickness.Top - BorderThickness.Bottom;
        if (width <= 0 || height <= 0) return true;
        string current = Text ?? "";
        int start = Math.Clamp(Math.Min(SelectionStart, SelectionEnd), 0, current.Length), end = Math.Clamp(Math.Max(SelectionStart, SelectionEnd), start, current.Length);
        string next = current.Remove(start, end - start).Insert(start, insertion);
        Size Measure(string value)
        {
            using var layout = new TextLayout(value, new Typeface(FontFamily, FontStyle, FontWeight, FontStretch), FontSize, Foreground, textWrapping: TextWrapping.Wrap, maxWidth: width);
            return new Size(layout.WidthIncludingTrailingWhitespace, layout.Height);
        }
        var size = Measure(next);
        if (size.Width <= width + .5 && size.Height <= height + .5) return true;
        var before = Measure(current);
        return next.Length <= current.Length && size.Height <= before.Height && size.Width <= Math.Max(width, before.Width);
    }
}

// Native CheckBox has no read-only state; Preview keeps the box focusable and its value readable but never toggles it.
internal sealed class PaperCheckBox : CheckBox
{
    protected override Type StyleKeyOverride => typeof(CheckBox);
    internal bool IsReadOnly;
    protected override void Toggle() { if (!IsReadOnly) base.Toggle(); }
}

internal static class PasteGuard
{
    /// <summary>Feed clipboard text through TextInput (so the box's own checks and native undo apply), unless the box, its text,
    /// selection or focus changed while the clipboard was read.</summary>
    internal static async void Paste(TextBox box, Func<bool>? still = null)
    {
        var root = TopLevel.GetTopLevel(box);
        if (root?.Clipboard is not { } clipboard || box.IsReadOnly || !box.IsEffectivelyEnabled) return;
        var before = (box.Text, box.SelectionStart, box.SelectionEnd); bool focused = box.IsFocused;
        try
        {
            string? text = await clipboard.TryGetTextAsync();
            if (still?.Invoke() == false || TopLevel.GetTopLevel(box) != root || before != (box.Text, box.SelectionStart, box.SelectionEnd) || focused && !box.IsFocused || string.IsNullOrEmpty(text)) return;
            box.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = text });
        }
        catch (Exception error) when (error is System.IO.IOException or System.Runtime.InteropServices.ExternalException or InvalidOperationException or NotSupportedException or OperationCanceledException) { }
    }
}

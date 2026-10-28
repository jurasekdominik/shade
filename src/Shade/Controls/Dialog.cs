// Shade - shared control: Dialog, a window in the Shade look, and Ask, its message and question boxes.
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
    /// <summary>A button of a dialog the app composes: the key it returns, its label, whether it is the main action (Primary:
    /// Enter picks it) and whether it changes or removes something (Danger: red, and only a click picks it).</summary>
    public sealed record ChoiceOption(string Key, string Label, bool Primary = false, bool Danger = false);

    /// <summary>A dialog in the Shade look: dark, fixed size, centred on its owner, Esc cancels. Note: the message line left
    /// of the buttons (errors in red). Subclasses build their own content; or give it a body and buttons (ChoiceOption).</summary>
    public class Dialog : Window
    {
        protected readonly TextBlock Note = Build.Text("", Text2, TextSize.Label);

        /// <summary>The key of the button picked; null when the dialog was closed with Esc or its window.</summary>
        public string? Chosen { get; private set; }

        /// <summary>Your own body (a message, a field, a list, an icon beside text) above buttons from the options, left to right
        /// with the last at the right edge. Show it with Ask.Choose, or ShowDialog and read Chosen.</summary>
        public Dialog(string title, Control body, IReadOnlyList<ChoiceOption> options, double width = 460) : this(title)
        {
            if (options.Count == 0) throw new ArgumentException("A dialog needs at least one button.", nameof(options));
            var row = new DockPanel { Margin = new Thickness(0, 24, 0, 0), LastChildFill = false };
            foreach (var option in options.Reverse())
            {
                var button = Words(new ActionButton(T(option.Label), option.Primary && !option.Danger ? ButtonKind.Primary : ButtonKind.Normal) { MinWidth = 110, Height = ControlHeight, Margin = new Thickness(8, 0, 0, 0) }, b => { b.Disarm(); b.Content = T(option.Label); });
                if (option.Danger) button.Live = Danger;
                button.Click += (_, _) => Pick(option.Key);
                DockPanel.SetDock(button, Dock.Right); row.Children.Add(button);
            }
            Content = new StackPanel { Margin = new Thickness(24, 20), Width = width - 48, Children = { body, row } };
            if (options.FirstOrDefault(o => o.Primary && !o.Danger) is { } main) KeyDown += (_, e) => { if (e.Key == Key.Enter && !e.Handled) { e.Handled = true; Pick(main.Key); } };
        }
        void Pick(string key) { Chosen = key; Close(); }

        /// <summary>Over its owner; with none to sit on (the app in the tray, or before its window) on its own, in the taskbar.</summary>
        internal static async Task Present(Window? owner, Window d)
        {
            if (owner != null && owner.IsVisible) { await d.ShowDialog(owner); return; }
            d.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            d.ShowInTaskbar = true;
            var done = new TaskCompletionSource<bool>();
            d.Closed += (s, e) => done.TrySetResult(true);
            d.Show();
            await done.Task;
        }

        protected Dialog(string title)
        {
            Title = title;
            Background = B(Look.Background);
            CanResize = false;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SizeToContent = SizeToContent.WidthAndHeight;
            Icon = Look.Icon(Accent);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.SubpixelAntialias);
            Note.TextWrapping = TextWrapping.Wrap;
            KeyDown += (s, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
            Opened += (s, e) => { if (Content is Control c) Build.Rise(c); };
        }

        /// <summary>Cancel and the main button at the right, the note in the room left of them.</summary>
        protected DockPanel Buttons(string okText, Action ok, bool cancel = true, bool danger = false)
        {
            var row = new DockPanel { Margin = new Thickness(0, 24, 0, 0), LastChildFill = true };
            var okBtn = Words(new ActionButton(T(okText), danger ? ButtonKind.Normal : ButtonKind.Primary) { MinWidth = 110, Height = ControlHeight }, button => { button.Disarm(); button.Content = T(okText); });  // at least 110: a long action name keeps its words
            if (danger) okBtn.Live = Danger;
            okBtn.Click += (_, _) => ok();
            DockPanel.SetDock(okBtn, Dock.Right);
            row.Children.Add(okBtn);
            if (cancel)
            {
                var c = Words(new ActionButton(T("Cancel")) { MinWidth = 110, Height = ControlHeight, Margin = new Thickness(0, 0, 8, 0) }, button => { button.Disarm(); button.Content = T("Cancel"); });
                c.Click += (_, _) => Close();
                DockPanel.SetDock(c, Dock.Right);
                row.Children.Add(c);
            }
            row.Children.Add(Note);
            return row;
        }

        protected void Fail(Field f, string msg)
        {
            if (f != null) { f.Invalid = true; f.Box.Focus(); }
            Note.Set(msg, DangerText);
        }
    }

    /// <summary>The dark message box: a message and OK (+ Cancel when asking). Enter = OK unless it changes or removes
    /// something (danger: only a click confirms).</summary>
    public sealed class Ask : Dialog
    {
        bool answer;

        internal Ask(string text, string ok, bool question, double width, bool danger) : base(Look.AppName)
        {
            var p = new StackPanel { Margin = new Thickness(24, 20), Width = width - 48 };
            p.Children.Add(Body(text));
            p.Children.Add(Buttons(ok, Yes, question, danger));
            Content = p;
            if (!danger) KeyDown += (s, e) => { if (e.Key == Key.Enter) { e.Handled = true; Yes(); } };
        }

        /// <summary>The message: a short first line before a blank line is the title, the rest the text.</summary>
        static StackPanel Body(string text)
        {
            var p = new StackPanel();
            int cut = text.IndexOf("\n\n", StringComparison.Ordinal);
            string? head = cut > 0 && cut <= 80 && text.IndexOf('\n') == cut ? text.Substring(0, cut) : null;
            if (head != null)
            {
                var title = Build.Text(head, Look.Text, TextSize.Section, 600);
                title.TextWrapping = TextWrapping.Wrap;
                title.Margin = new Thickness(0, 0, 0, 8);
                p.Children.Add(title);
                text = text.Substring(cut + 2);
            }
            var msg = Build.Text(text, head != null ? Text2 : Look.Text);
            msg.TextWrapping = TextWrapping.Wrap;
            msg.TextTrimming = TextTrimming.None;
            msg.LineHeight = 22;
            p.Children.Add(msg);
            return p;
        }

        void Yes() { answer = true; Close(); }

        public static Task<bool> Confirm(Window owner, string text, string ok = "OK", double width = 460, bool danger = false) => Show(owner, new Ask(text, ok, true, width, danger));
        public static Task<bool> ConfirmDelete(Window owner, string text, string ok) => Confirm(owner, text, ok, 460, true);
        public static Task Info(Window owner, string text, double width = 460) => Show(owner, new Ask(text, "OK", false, width, false));

        /// <summary>Asks with the app's own buttons ("Save", "Don't save", "Cancel") and returns the key of the one picked, null
        /// for Esc or the window's close. Mark the main action Primary (Enter picks it) and a destructive one Danger.</summary>
        public static Task<string?> Choose(Window owner, string text, params ChoiceOption[] options) => Choose(owner, Body(text), options);
        /// <summary>The same with a body of your own: a message with a field, a list, an icon.</summary>
        public static async Task<string?> Choose(Window owner, Control body, params ChoiceOption[] options)
        {
            var d = new Dialog(Look.AppName, body, options);
            await Present(owner, d);
            return d.Chosen;
        }

        static async Task<bool> Show(Window owner, Ask d) { await Present(owner, d); return d.answer; }
    }
}

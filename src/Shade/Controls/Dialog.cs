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
    /// <summary>A dialog in the Shade look: dark, fixed size, centred on its owner, Esc cancels. Note: the message line left
    /// of the buttons (errors in red).</summary>
    public class Dialog : Window
    {
        protected readonly TextBlock Note = Kit.Text("", Second, 9);

        protected Dialog(string title)
        {
            Title = title;
            Background = B(Bg);
            CanResize = false;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SizeToContent = SizeToContent.WidthAndHeight;
            Icon = Look.Icon(Accent);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.SubpixelAntialias);
            Note.TextWrapping = TextWrapping.Wrap;
            KeyDown += (s, e) => { if (e.Key == Key.Escape) { e.Handled = true; Close(); } };
            Opened += (s, e) => { if (Content is Control c) Kit.Rise(c); };
        }

        /// <summary>Cancel and the main button at the right, the note in the room left of them.</summary>
        protected DockPanel Buttons(string okText, Action ok, bool cancel = true, bool danger = false)
        {
            var row = new DockPanel { Margin = new Thickness(0, 24, 0, 0), LastChildFill = true };
            var okBtn = new Btn(okText, danger ? BtnKind.Normal : BtnKind.Primary) { MinWidth = 110, Height = 28 };  // at least 110: a long action name keeps its words
            if (danger) okBtn.Live = Danger;
            okBtn.Click += ok;
            DockPanel.SetDock(okBtn, Dock.Right);
            row.Children.Add(okBtn);
            if (cancel)
            {
                var c = new Btn("Cancel") { MinWidth = 110, Height = 28, Margin = new Thickness(0, 0, 8, 0) };
                c.Click += () => Close();
                DockPanel.SetDock(c, Dock.Right);
                row.Children.Add(c);
            }
            row.Children.Add(Note);
            return row;
        }

        protected void Fail(Field f, string msg)
        {
            if (f != null) { f.Invalid = true; f.Box.Focus(); }
            Note.Set(msg, DangerHi);
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
            // a short first line is the alert's title, the rest its message
            int cut = text.IndexOf("\n\n", StringComparison.Ordinal);
            string? head = cut > 0 && cut <= 80 && text.IndexOf('\n') == cut ? text.Substring(0, cut) : null;
            if (head != null)
            {
                var title = Kit.Text(head, Look.Text, 11, 600);
                title.TextWrapping = TextWrapping.Wrap;
                title.Margin = new Thickness(0, 0, 0, 8);
                p.Children.Add(title);
                text = text.Substring(cut + 2);
            }
            var msg = Kit.Text(text, head != null ? Second : Look.Text);
            msg.TextWrapping = TextWrapping.Wrap;
            msg.TextTrimming = TextTrimming.None;
            msg.LineHeight = 22;
            p.Children.Add(msg);
            p.Children.Add(Buttons(ok, Yes, question, danger));
            Content = p;
            if (!danger) KeyDown += (s, e) => { if (e.Key == Key.Enter) { e.Handled = true; Yes(); } };
        }

        void Yes() { answer = true; Close(); }

        public static Task<bool> Confirm(Window owner, string text, string ok = "OK", double width = 460, bool danger = false) => Show(owner, new Ask(text, ok, true, width, danger));
        public static Task<bool> ConfirmDelete(Window owner, string text, string ok) => Confirm(owner, text, ok, 460, true);
        public static Task Info(Window owner, string text, double width = 460) => Show(owner, new Ask(text, "OK", false, width, false));

        /// <summary>Over its owner; with none to sit on (the app in the tray, or before its window) on its own, in the taskbar.</summary>
        static async Task<bool> Show(Window owner, Ask d)
        {
            if (owner != null && owner.IsVisible) { await d.ShowDialog(owner); return d.answer; }
            d.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            d.ShowInTaskbar = true;
            var done = new TaskCompletionSource<bool>();
            d.Closed += (s, e) => done.TrySetResult(d.answer);
            d.Show();
            return await done.Task;
        }
    }
}

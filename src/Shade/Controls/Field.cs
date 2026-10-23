// Shade - shared control: Field, the single-line input, the host:port field and the field error.
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
    /// <summary>A compact single-line input: the border shows hover, focus and a wrong value (Invalid).</summary>
    public class Field : Border
    {
        public readonly TextBox Box = Plain();
        public event Action Edited, Submit;
        bool hot, invalid;
        /// <summary>Changes the code made whose TextChanged hasn't come yet: Avalonia posts one per change, later, so a flag
        /// around the set can't tell them from typing.</summary>
        readonly Dictionary<TextBox, int> quiet = new Dictionary<TextBox, int>();

        /// <summary>Sets a box's text without raising Edited.</summary>
        protected void SetQuiet(TextBox t, string v)
        {
            v = v ?? "";
            if ((t.Text ?? "") == v) return;
            quiet[t] = (quiet.TryGetValue(t, out int n) ? n : 0) + 1;
            t.Text = v;
        }

        /// <summary>True for the TextChanged of a change SetQuiet made.</summary>
        protected bool Quiet(TextBox t)
        {
            if (!quiet.TryGetValue(t, out int n) || n == 0) return false;
            quiet[t] = n - 1;
            return true;
        }

        /// <summary>A bare TextBox: no frame or fill of its own (the field draws them).</summary>
        public static TextBox Plain() => new TextBox
        {
            Classes = { "plain" },
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Foreground = B(Look.Text),
            CaretBrush = B(Look.Text),
            SelectionBrush = B(Selection),
            SelectionForegroundBrush = B(Look.Text),
            FontFamily = UiFont,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            MinHeight = 0,
            MinWidth = 0,
        };

        public Field(string placeholder = "", bool own = true)
        {
            Box.PlaceholderText = placeholder;
            Height = 28;
            CornerRadius = new CornerRadius(4);
            BorderThickness = new Thickness(1);
            Padding = new Thickness(8, 0);
            Background = B(Surface);
            Cursor = new Cursor(StandardCursorType.Ibeam);
            if (own) Child = Box;
            Watch(Box);
            Box.TextChanged += (s, e) => { if (!Quiet(Box)) { Invalid = false; Edited?.Invoke(); } };
            Box.KeyDown += (s, e) => { if (e.Key == Key.Enter) { e.Handled = true; Submit?.Invoke(); } };
            PointerEntered += (s, e) => { hot = true; Restyle(); };
            PointerExited += (s, e) => { hot = false; Restyle(); };
            PointerPressed += (s, e) => { if (!Box.IsFocused) Box.Focus(); };
            PropertyChanged += (s, e) => { if (e.Property == IsEnabledProperty) Restyle(); };
            Restyle();
        }

        /// <summary>A text box inside the field whose focus lights the border.</summary>
        protected void Watch(TextBox t) { t.GotFocus += (s, e) => Restyle(); t.LostFocus += (s, e) => Restyle(); }
        protected void RaiseEdited() => Edited?.Invoke();
        protected void RaiseSubmit() => Submit?.Invoke();
        protected virtual bool HasFocus => Box.IsFocused;

        public virtual string Text { get => inherit && (Box.Text ?? "").Trim() == Box.PlaceholderText ? "" : Box.Text ?? ""; set => SetQuiet(Box, value); }
        public bool Invalid { get => invalid; set { if (invalid != value) { invalid = value; Restyle(); } } }

        bool inherit, pending, showUndo;
        Control undo;
        /// <summary>Holds a value not written yet (WRITE): a yellow border until it is written or put back.</summary>
        public bool Pending { get => pending; set { if (pending != value) { pending = value; Restyle(); } } }
        /// <summary>An undo icon inside the field's right end (WRITE: drop what was typed, or the value before the last write).</summary>
        public Action Undo;
        public bool ShowUndo { get => showUndo; set { if (showUndo == value) return; showUndo = value; EnsureUndo(); Restyle(); } }
        /// <summary>Empty = the inherited value (shown as the placeholder); anything typed overrides it: an amber border and the
        /// undo arrow at the right end that clears it again.</summary>
        public bool Inherit
        {
            get => inherit;
            set
            {
                inherit = value;
                if (value) EnsureUndo();
            }
        }

        void EnsureUndo()
        {
            if (undo != null || Child != Box) return;  // the address field has its own layout: no undo there
            undo = UndoIcon();
            undo.PointerPressed += (s, e) => { e.Handled = true; if (showUndo) Undo?.Invoke(); else Box.Text = ""; };  // undo arrow: back to the inherited value
            var d = new DockPanel();
            Child = null;
            DockPanel.SetDock(undo, Dock.Right);
            d.Children.Add(undo); d.Children.Add(Box);
            Child = d;
            Box.TextChanged += (s, e) => Restyle();
            Restyle();
        }

        /// <summary>With Inherit, typing the inherited value (the placeholder) counts as empty: not an override.</summary>
        bool Overridden => pinned || inherit && (Box.Text ?? "").Trim() != "" && (Box.Text ?? "").Trim() != Box.PlaceholderText;
        bool pinned;
        /// <summary>Set by the code: differs from what it inherits (the amber border), no undo arrow of its own.</summary>
        public bool Pinned { get => pinned; set { if (pinned != value) { pinned = value; Restyle(); } } }

        public static readonly Geometry UndoGeometry = Icons.Undo2;

        /// <summary>The undo arrow (Tabler arrow-back-up), 16 px, on a transparent 24 px square that takes the click: a bare
        /// path only reacts on its thin lines.</summary>
        public static Control UndoIcon()
        {
            var p = Kit.Icon24(UndoGeometry, 16);
            p.Stroke = B(Accent);
            return new Border { Child = p, Background = Brushes.Transparent, Cursor = Kit.Hand, Width = 24, Margin = new Thickness(2, 0, -4, 0) };
        }

        bool open;
        /// <summary>Its list is open below it (an attached menu, DESIGN_SYSTEM.md §6): square bottom, no bottom line, the menu's edge colour.</summary>
        public bool Open
        {
            get => open;
            set { open = value; CornerRadius = value ? new CornerRadius(4, 4, 0, 0) : new CornerRadius(4); BorderThickness = value ? new Thickness(1, 1, 1, 0) : new Thickness(1); Restyle(); }
        }

        protected void Restyle()
        {
            if (undo != null) undo.IsVisible = showUndo || Overridden;
            Color border = !IsEnabled ? Look.Border : open ? MenuEdge : invalid ? Danger : pending ? Warn : HasFocus ? Accent : Overridden ? Override : hot ? Disabled : Look.Border;
            BorderBrush = B(border);
            Background = B(IsEnabled ? Surface : Bg);
        }
    }

    /// <summary>A device address as one field: the host, a fixed ":" and the port (DefaultPort unless changed). Typing ":" or
    /// pasting "host:port" in the host part fills the port. Text is "host:port" ("" while there is no host).</summary>
    public sealed class HostPortField : Field
    {
        readonly TextBox port = Plain();
        string defaultPort = "502";

        public HostPortField(string placeholder) : base(placeholder, false)
        {
            port.Text = defaultPort;
            port.MaxLength = 5;
            port.Width = 44;
            var colon = Kit.Text(":", Faint);
            colon.Margin = new Thickness(2, 0, 4, 0);
            var g = new DockPanel();
            DockPanel.SetDock(port, Dock.Right); DockPanel.SetDock(colon, Dock.Right);
            g.Children.Add(port); g.Children.Add(colon); g.Children.Add(Box);
            Child = g;
            Watch(port);
            port.GotFocus += (s, e) => port.SelectAll();
            port.LostFocus += (s, e) => { if ((port.Text ?? "").Trim() == "") port.Text = defaultPort; };
            port.TextInput += (s, e) => { if (e.Text != null && !e.Text.All(char.IsDigit)) e.Handled = true; };
            port.KeyDown += (s, e) => { if (e.Key == Key.Enter) { e.Handled = true; RaiseSubmit(); } };
            port.TextChanged += (s, e) => { if (!Quiet(port)) { Invalid = false; RaiseEdited(); } };
            Box.AddHandler(TextInputEvent, (s, e) => { if (e.Text == ":") { e.Handled = true; port.Focus(); } }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            Box.TextChanged += (s, e) =>
            {
                string t = Box.Text ?? "";
                int i = t.LastIndexOf(':');
                if (i > 0 && int.TryParse(t.Substring(i + 1), out _)) { string p = t.Substring(i + 1); Box.Text = t.Substring(0, i); port.Text = p; }  // typed or pasted: Edited as typing
            };
            PropertyChanged += (s, e) => { if (e.Property == IsEnabledProperty) port.IsEnabled = IsEnabled; };
        }

        /// <summary>The port an empty or new address gets (the read or the write port).</summary>
        public int DefaultPort { set { string v = value.ToString(System.Globalization.CultureInfo.InvariantCulture); if (port.Text == defaultPort) SetQuiet(port, v); defaultPort = v; } }

        protected override bool HasFocus => Box.IsFocused || port.IsFocused;

        public override string Text
        {
            get => (Box.Text ?? "").Trim() == "" ? "" : $"{Box.Text.Trim()}:{((port.Text ?? "").Trim() == "" ? defaultPort : port.Text.Trim())}";
            set
            {
                string t = value ?? "";
                int i = t.LastIndexOf(':');
                bool split = i > 0 && int.TryParse(t.Substring(i + 1), out _);
                base.Text = split ? t.Substring(0, i) : t;
                SetQuiet(port, split ? t.Substring(i + 1) : defaultPort);
            }
        }
    }

    /// <summary>A field's value is wrong: the field to mark and the reason.</summary>
    public sealed class FieldError : System.Exception { public readonly Field Field; public FieldError(Field f, string msg) : base(msg) { Field = f; } }
}

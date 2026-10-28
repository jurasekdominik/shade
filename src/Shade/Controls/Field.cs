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
    /// <summary>A compact single-line input: the border shows hover, focus and a wrong value (Invalid), painted by FieldTheme
    /// on pseudo-classes (:pointerover, :focus-within, :disabled and Shade's :invalid, :pending, :overridden, :open).</summary>
    public class Field : Border, IRestyle
    {
        public TextBox Box { get; } = Plain();
        public event Action? Edited, Submit;
        protected override Type StyleKeyOverride => typeof(Field);
        public static readonly StyledProperty<string?> PlaceholderTextProperty = TextBox.PlaceholderTextProperty.AddOwner<Field>();
        public string? PlaceholderText { get => GetValue(PlaceholderTextProperty); set => SetValue(PlaceholderTextProperty, value); }

        public static readonly StyledProperty<bool> InvalidProperty = AvaloniaProperty.Register<Field, bool>(nameof(Invalid));
        public static readonly StyledProperty<bool> PendingProperty = AvaloniaProperty.Register<Field, bool>(nameof(Pending));
        public static readonly StyledProperty<string?> ErrorProperty = AvaloniaProperty.Register<Field, string?>(nameof(Error));
        public static readonly StyledProperty<bool> IsReadOnlyProperty = AvaloniaProperty.Register<Field, bool>(nameof(IsReadOnly));
        /// <summary>Application validation message; use Build.Labelled to show it under the field.</summary>
        public string? Error { get => GetValue(ErrorProperty); set => SetValue(ErrorProperty, value); }
        public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
        string? bindingError;
        internal string? ValidationMessage => Error ?? bindingError;
        internal event Action? ValidationChanged;
        internal Control AccessibleInput => this is DateField or DateRangeField ? this : Box;
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
            FontSize = TextSize.Body,
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            MinHeight = 0,
            MinWidth = 0,
        };

        public static readonly DirectProperty<Field, string> TextProperty =
            AvaloniaProperty.RegisterDirect<Field, string>(nameof(Text), o => o.Text, (o, v) => o.Text = v, "", Avalonia.Data.BindingMode.TwoWay, enableDataValidation: true);
        string shownText = "";
        /// <summary>Text changed (typed or set): bindings to Text follow.</summary>
        protected void RaiseText() { var now = Text; if (now == shownText) return; var was = shownText; shownText = now; RaisePropertyChanged(TextProperty, was, now); }

        public Field() : this("") { }

        public Field(string placeholder, bool own = true)
        {
            PlaceholderText = placeholder;
            Height = ControlHeight;
            CornerRadius = new CornerRadius(4);
            BorderThickness = new Thickness(1);
            Padding = new Thickness(M(8), 0);
            Cursor = new Cursor(StandardCursorType.Ibeam);
            if (own) Child = Box;
            Box.TextChanged += (s, e) => { RaiseText(); if (!Quiet(Box)) { Invalid = false; Edited?.Invoke(); } };
            Box.KeyDown += (s, e) => { if (e.Key == Key.Enter) { e.Handled = true; Submit?.Invoke(); } };
            PointerPressed += (s, e) => { if (!Box.IsFocused) Box.Focus(); e.Handled = true; };  // handled: the window doesn't take it for a click on the background
            Restyle();
        }

        /// <summary>No longer needed: any focus inside the field lights the border (:focus-within).</summary>
        [Obsolete("The border follows :focus-within; a text box inside the field needs no call. Removed in a later minor.")]
        protected void Watch(TextBox t) { }
        protected void RaiseEdited() => Edited?.Invoke();
        protected void RaiseSubmit() => Submit?.Invoke();
        protected virtual bool HasFocus => Box.IsFocused;

        public virtual string Text { get => inherit && (Box.Text ?? "").Trim() == Box.PlaceholderText ? "" : Box.Text ?? ""; set => SetQuiet(Box, value); }
        public bool Invalid { get => GetValue(InvalidProperty); set => SetValue(InvalidProperty, value); }
        protected override void UpdateDataValidation(AvaloniaProperty property, Avalonia.Data.BindingValueType state, Exception? error)
        {
            base.UpdateDataValidation(property, state, error);
            if (property != TextProperty) return;
            bindingError = error?.Message;
            ValidationChanged?.Invoke(); Restyle();
        }
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == PlaceholderTextProperty && Box != null) Box.SetCurrentValue(TextBox.PlaceholderTextProperty, PlaceholderText);
            if (change.Property == IsReadOnlyProperty && Box != null)
            {
                Box.IsReadOnly = IsReadOnly;
                if (this is DateField || this is DateRangeField) { if (Child != null) Child.IsEnabled = !IsReadOnly; }
            }
            if (change.Property == ErrorProperty) ValidationChanged?.Invoke();
            if (Box != null && (change.Property == IsReadOnlyProperty || change.Property == InvalidProperty || change.Property == PendingProperty || change.Property == ErrorProperty)) Restyle();
        }

        bool inherit, showUndo;
        IconButton? undo;
        /// <summary>Holds a value not applied yet: a yellow border until it is applied or put back.</summary>
        public bool Pending { get => GetValue(PendingProperty); set => SetValue(PendingProperty, value); }
        /// <summary>An undo icon inside the field's right end: drop what was typed, or go back to the value before the last apply.</summary>
        public Action? Undo { get; set; }
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
            undo.Click += (_, _) => { if (IsReadOnly) return; if (showUndo) Undo?.Invoke(); else Box.Text = ""; };  // undo arrow: back to the inherited value
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
        public static IconButton UndoIcon() => Words(new IconButton { Icon = UndoGeometry, IconSize = M(16), Width = M(24), Height = M(24), MinWidth = 0, Margin = new Thickness(M(2), 0, -M(4), 0) }, button => { ToolTip.SetTip(button, T("Undo")); Avalonia.Automation.AutomationProperties.SetName(button, T("Undo")); });

        bool open;
        /// <summary>Its list is open below it (an attached menu, DESIGN.md): square bottom, no bottom line, the menu's edge colour.</summary>
        public bool Open
        {
            get => open;
            set { open = value; Build.Attached(this, value, new Thickness(M(8), 0)); Restyle(); }
        }

        public void Restyle()
        {
            if (undo != null) { undo.IsVisible = showUndo || Overridden; undo.IsEnabled = !IsReadOnly; }
            PseudoClasses.Set(":invalid", Invalid || ValidationMessage != null);
            PseudoClasses.Set(":pending", Pending);
            PseudoClasses.Set(":overridden", Overridden);
            PseudoClasses.Set(":open", open);
        }
    }

    /// <summary>A server address as one field: the host, a fixed ":" and the port (DefaultPort unless changed). Typing ":" or
    /// pasting "host:port" in the host part fills the port. Text is "host:port" ("" while there is no host).</summary>
    public sealed class HostPortField : Field
    {
        readonly TextBox port = Plain();
        string defaultPort = "443";

        public static readonly StyledProperty<int> DefaultPortProperty = AvaloniaProperty.Register<HostPortField, int>(nameof(DefaultPort), 443, validate: value => value >= 0 && value <= 65535);
        public HostPortField() : this("") { }
        public HostPortField(string placeholder) : base(placeholder, false)
        {
            port.Text = defaultPort;
            port.MaxLength = 5;
            port.Width = M(44);
            var colon = Build.Text(":", Text3);
            colon.Margin = new Thickness(M(2), 0, M(4), 0);
            var g = new DockPanel();
            DockPanel.SetDock(port, Dock.Right); DockPanel.SetDock(colon, Dock.Right);
            g.Children.Add(port); g.Children.Add(colon); g.Children.Add(Box);
            Child = g;
            port.GotFocus += (s, e) => port.SelectAll();
            port.LostFocus += (s, e) => { if ((port.Text ?? "").Trim() == "") port.Text = defaultPort; };
            port.TextInput += (s, e) => { if (e.Text != null && !e.Text.All(char.IsDigit)) e.Handled = true; };
            port.KeyDown += (s, e) => { if (e.Key == Key.Enter) { e.Handled = true; RaiseSubmit(); } };
            port.TextChanged += (s, e) => { RaiseText(); if (!Quiet(port)) { Invalid = false; RaiseEdited(); } };
            Box.AddHandler(TextInputEvent, (s, e) => { if (e.Text == ":") { e.Handled = true; port.Focus(); } }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            Box.TextChanged += (s, e) =>
            {
                string t = Box.Text ?? "";
                int i = t.LastIndexOf(':');
                if (i > 0 && int.TryParse(t.Substring(i + 1), out _)) { string p = t.Substring(i + 1); Box.Text = t.Substring(0, i); port.Text = p; }  // typed or pasted: Edited as typing
            };
            PropertyChanged += (_, e) => { if (e.Property == IsReadOnlyProperty) port.IsReadOnly = IsReadOnly; };
        }

        /// <summary>The port an empty or new address gets (the service's usual port).</summary>
        public int DefaultPort { get => GetValue(DefaultPortProperty); set => SetValue(DefaultPortProperty, value); }
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property != DefaultPortProperty || port == null) return;
            string next = DefaultPort.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (port.Text == defaultPort) SetQuiet(port, next); defaultPort = next;
        }

        protected override bool HasFocus => Box.IsFocused || port.IsFocused;

        public override string Text
        {
            get => (Box.Text ?? "").Trim() == "" ? "" : $"{Box.Text!.Trim()}:{((port.Text ?? "").Trim() == "" ? defaultPort : port.Text!.Trim())}";
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
    public sealed class FieldError : System.Exception { public Field Field { get; } public FieldError(Field f, string msg) : base(msg) { Field = f; } }
}

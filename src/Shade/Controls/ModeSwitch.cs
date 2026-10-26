// Shade - the mode switch: Light, Dark or System (the computer's setting, followed live), as one segmented control.
using System;
using System.Linq;
using Avalonia.VisualTree;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using static Shade.Look;

namespace Shade
{
    /// <summary>Which mode the app shows: light, dark, or the computer's (System, followed as it changes).</summary>
    public enum ModeChoice { Dark, Light, System }

    /// <summary>The colour mode (DESIGN_SYSTEM.md §2): Light, Dark and System as one segmented control, the current one tinted.
    /// A pick switches at once (Look.SetMode); Left/Right move between them. Apply restores a saved choice at start-up.</summary>
    public sealed class ModeSwitch : Border, IRestyle
    {
        public static readonly DirectProperty<ModeSwitch, ModeChoice> ValueProperty =
            AvaloniaProperty.RegisterDirect<ModeSwitch, ModeChoice>(nameof(Value), o => o.Value, (o, v) => o.Value = v, ModeChoice.Dark, Avalonia.Data.BindingMode.TwoWay);
        static ModeChoice applied = ModeChoice.Dark;
        static bool watching;
        ModeChoice value = applied == ModeChoice.System ? ModeChoice.System : Current == Mode.Light ? ModeChoice.Light : ModeChoice.Dark;  // the mode as it is, however it was set
        readonly RadioButton[] segments = new RadioButton[3];
        bool synchronizing;
        static readonly (ModeChoice choice, string name, Geometry icon)[] Choices = { (ModeChoice.Light, "Light", Icons.Sun), (ModeChoice.Dark, "Dark", Icons.Moon), (ModeChoice.System, "System", Icons.Monitor) };

        /// <summary>A pick changed the mode.</summary>
        public event Action? Changed;

        /// <summary>The choice shown; setting it applies it (Apply).</summary>
        public ModeChoice Value { get => value; set { Apply(value); SetAndRaise(ValueProperty, ref this.value, value); Restyle(); } }

        public ModeSwitch()
        {
            BorderThickness = new Thickness(1); CornerRadius = new CornerRadius(Radius); ClipToBounds = true; HorizontalAlignment = HorizontalAlignment.Left;
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            for (int i = 0; i < Choices.Length; i++)
            {
                var (choice, name, icon) = Choices[i]; int at = i;
                var glyph = Build.Icon24(icon, M(14), 2); glyph.VerticalAlignment = VerticalAlignment.Center;
                var label = Words(Build.Text("", Text2, TextSize.Label, 500), text => text.Text = T(name));
                var surface = new Border
                {
                    Height = M(26),
                    Padding = new Thickness(12, 0),
                    Focusable = false,
                    Cursor = Build.Hand,
                    BorderThickness = new Thickness(i == 0 ? 0 : 1, 0, 0, 0),
                    Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = M(6), Children = { glyph, label } },
                };
                var segment = new RadioButton { MinHeight = 0, MinWidth = 0, Padding = new Thickness(0), Template = new Avalonia.Controls.Templates.FuncControlTemplate<RadioButton>((_, _) => surface) };
                FocusRing(segment);
                surface.AttachedToVisualTree += (_, _) => Restyle();
                segment.PropertyChanged += (_, e) => { if (e.Property == Avalonia.Controls.Primitives.ToggleButton.IsCheckedProperty && segment.IsChecked == true && !synchronizing) Pick(choice); };
                Words(segment, word => Avalonia.Automation.AutomationProperties.SetName(word, T("{0} mode", T(name))), "name");
                segment.PointerEntered += (s, e) => Restyle();
                segment.PointerExited += (s, e) => Restyle();
                segment.GotFocus += (s, e) => Restyle();
                segment.LostFocus += (s, e) => Restyle();
                segment.KeyDown += (s, e) =>
                {
                    int to = e.Key switch { Key.Left => at - 1, Key.Right => at + 1, _ => -1 };
                    if (to >= 0 && to < Choices.Length) { Pick(Choices[to].choice); segments[to].Focus(NavigationMethod.Directional); }
                    else return;
                    e.Handled = true;
                };
                segments[i] = segment; row.Children.Add(segment);
            }
            Child = row;
            Restyle();
        }

        /// <summary>Shows a choice at start-up (or from anywhere): Light, Dark, or System, which follows the computer's setting
        /// from now on, as it changes.</summary>
        public static void Apply(ModeChoice choice)
        {
            applied = choice;
            var platform = Application.Current?.PlatformSettings;
            if (choice == ModeChoice.System && platform != null && !watching)
            {
                watching = true;
                platform.ColorValuesChanged += (s, e) => Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (applied == ModeChoice.System && Os(e) != Current) SetMode(Os(e)); });
            }
            var mode = choice == ModeChoice.Light ? Mode.Light : choice == ModeChoice.Dark ? Mode.Dark : platform == null ? Mode.Dark : Os(platform.GetColorValues());
            if (mode != Current) SetMode(mode);
        }

        static Mode Os(PlatformColorValues v) => v.ThemeVariant == PlatformThemeVariant.Light ? Mode.Light : Mode.Dark;

        void Pick(ModeChoice choice)
        {
            if (choice == value) return;
            Value = choice;
            Changed?.Invoke();
        }

        /// <summary>The current segment tinted, its text and icon in Text; hover Fill; a keyboard focus ring on the segment.</summary>
        public void Restyle()
        {
            // the mode set some other way (Look.SetMode, another switch) shows here too; System stays System
            if (value != ModeChoice.System) { var now = Current == Mode.Light ? ModeChoice.Light : ModeChoice.Dark; if (now != value) SetAndRaise(ValueProperty, ref value, now); }
            synchronizing = true;
            for (int i = 0; i < segments.Length; i++)
            {
                var s = segments[i]; if (s == null) continue;
                bool on = Choices[i].choice == value;
                s.SetCurrentValue(Avalonia.Controls.Primitives.ToggleButton.IsCheckedProperty, on);
                s.Background = B(on ? AccentTint : s.IsPointerOver ? Fill : Raised);
                var surface = (Border)s.GetVisualChildren().FirstOrDefault()!;
                if (surface == null) continue;
                surface.Background = s.Background; surface.BorderBrush = B(Look.Separator);
                var parts = ((StackPanel)surface.Child!).Children;
                ((Avalonia.Controls.Shapes.Path)parts[0]).Stroke = B(on || s.IsPointerOver ? Look.Text : Text2);
                ((TextBlock)parts[1]).Foreground = B(on || s.IsPointerOver ? Look.Text : Text2);
            }
            synchronizing = false;
            BorderBrush = B(Array.Exists(segments, s => s != null && s.IsKeyboardFocusWithin) ? FocusBorder : Look.Separator);
        }
    }
}

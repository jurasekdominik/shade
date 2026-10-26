// Shade - shared control: an on/off state as shown, and the toggle that changes it.
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
    /// <summary>An on/off state, shown: a block with ON (accent) or OFF (red) in a track tinted the same way (display only;
    /// StateToggle changes it).</summary>
    public sealed class StateLook : Control
    {
        internal const double Radius = 4;
        public static readonly StyledProperty<bool> OnProperty = AvaloniaProperty.Register<StateLook, bool>(nameof(On));
        static StateLook() => AffectsRender<StateLook>(OnProperty);
        public bool On { get => GetValue(OnProperty); set => SetValue(OnProperty, value); }

        public override void Render(DrawingContext g)
        {
            var box = new Rect(Bounds.Size).Deflate(0.5);
            Color state = On ? Accent : Danger;
            g.DrawRectangle(B(Blend(Raised, state, 0.16)), new Pen(B(Blend(Look.Separator, state, 0.45)), 1), box, StateLook.Radius, StateLook.Radius);
            double inset = M(3), w = (box.Width - 2 * inset) * 0.52, h = box.Height - 2 * inset;
            var block = new Rect(box.X + inset + (On ? box.Width - 2 * inset - w : 0), box.Y + inset, w, h);
            g.DrawRectangle(B(state), null, block, StateLook.Radius - 3, StateLook.Radius - 3);
            var t = new FormattedText(T(On ? "ON" : "OFF"), Culture, FlowDirection,
                new Typeface(UiFont, FontStyle.Normal, FontWeight.Bold), TextSize.Label, B(On ? OnAccent : White));
            g.DrawText(t, new Point(block.X + (block.Width - t.Width) / 2, block.Y + (block.Height - t.Height) / 2));
        }
    }

    /// <summary>A native toggle with Shade's ON/OFF face. IsChecked null means unknown.</summary>
    public sealed class StateToggle : ToggleButton
    {
        public static readonly StyledProperty<bool> PendingProperty = AvaloniaProperty.Register<StateToggle, bool>(nameof(Pending));
        public static readonly StyledProperty<bool> ShowUndoProperty = AvaloniaProperty.Register<StateToggle, bool>(nameof(ShowUndo));
        public static readonly StyledProperty<string> UnknownTextProperty = AvaloniaProperty.Register<StateToggle, string>(nameof(UnknownText), "unknown");
        public static readonly StyledProperty<System.Windows.Input.ICommand?> UndoCommandProperty = AvaloniaProperty.Register<StateToggle, System.Windows.Input.ICommand?>(nameof(UndoCommand));
        public static readonly StyledProperty<object?> UndoCommandParameterProperty = AvaloniaProperty.Register<StateToggle, object?>(nameof(UndoCommandParameter));
        public bool Pending { get => GetValue(PendingProperty); set => SetValue(PendingProperty, value); }
        public bool ShowUndo { get => GetValue(ShowUndoProperty); set => SetValue(ShowUndoProperty, value); }
        public string UnknownText { get => GetValue(UnknownTextProperty); set => SetValue(UnknownTextProperty, value); }
        public System.Windows.Input.ICommand? UndoCommand { get => GetValue(UndoCommandProperty); set => SetValue(UndoCommandProperty, value); }
        public object? UndoCommandParameter { get => GetValue(UndoCommandParameterProperty); set => SetValue(UndoCommandParameterProperty, value); }
        protected override Type StyleKeyOverride => typeof(StateToggle);
        static StateToggle()
        {
            IsCheckedProperty.OverrideDefaultValue<StateToggle>(null);
            AffectsRender<StateToggle>(IsCheckedProperty, PendingProperty, UnknownTextProperty, IsEffectivelyEnabledProperty);
        }
        IconButton? undo;
        public StateToggle() { Height = ControlHeight; Cursor = Build.Hand; FocusRing(this); }
        protected override void Toggle()
        {
            if (IsChecked == null && !IsThreeState) SetCurrentValue(IsCheckedProperty, true);
            else base.Toggle();
        }
        internal Control CreateTemplate()
        {
            undo = new IconButton { Icon = Field.UndoGeometry, Width = M(24), Height = M(24), MinWidth = 0, Margin = new Thickness(M(6), 0), VerticalAlignment = VerticalAlignment.Center };
            undo[!Button.CommandProperty] = this[!UndoCommandProperty];
            undo[!Button.CommandParameterProperty] = this[!UndoCommandParameterProperty];
            Words(undo, word => { ToolTip.SetTip(word, T("Undo")); Avalonia.Automation.AutomationProperties.SetName(word, T("Undo")); });
            UpdateUndo();
            return new Panel { Children = { undo } };
        }
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == IsCheckedProperty || change.Property == ShowUndoProperty) UpdateUndo();
        }
        void UpdateUndo()
        {
            if (undo == null) return;
            undo.IsVisible = ShowUndo && IsChecked != null;
            undo.HorizontalAlignment = IsChecked == true ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        }
        public override void Render(DrawingContext g)
        {
            var box = new Rect(Bounds.Size).Deflate(0.5);
            if (IsChecked == null)
            {
                g.DrawRectangle(B(Raised), new Pen(B(Pending ? Warn : Look.Separator), 1), box, StateLook.Radius, StateLook.Radius);
                Look.Draw(g, UnknownText == "unknown" ? T(UnknownText) : UnknownText, Look.Ui(TextSize.Caption), Text3, new Rect(Bounds.Size), TextAlignment.Center, false);
                return;
            }
            bool on = IsChecked == true;
            Color state = on ? Accent : Danger;
            g.DrawRectangle(B(Blend(Raised, state, 0.16)), new Pen(B(Pending ? Warn : Blend(Look.Separator, state, 0.45)), 1), box, StateLook.Radius, StateLook.Radius);
            double inset = M(3), w = (box.Width - 2 * inset) * 0.52, h = box.Height - 2 * inset;
            var block = new Rect(box.X + inset + (on ? box.Width - 2 * inset - w : 0), box.Y + inset, w, h);
            g.DrawRectangle(B(IsEffectivelyEnabled ? state : Alpha(state, 0.4)), null, block, StateLook.Radius - 3, StateLook.Radius - 3);
            Look.Draw(g, T(on ? "ON" : "OFF"), Look.Ui(TextSize.Label, 700), on ? OnAccent : White, block, TextAlignment.Center, false);
        }
    }
}

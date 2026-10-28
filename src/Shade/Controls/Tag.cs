using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using static Shade.Look;

namespace Shade;

/// <summary>A small value label with an optional native remove action.</summary>
public sealed class Tag : Border, IRestyle
{
    public static readonly StyledProperty<string> TextProperty = AvaloniaProperty.Register<Tag, string>(nameof(Text), "");
    public static readonly StyledProperty<Tone?> ToneProperty = AvaloniaProperty.Register<Tag, Tone?>(nameof(Tone));
    public static readonly StyledProperty<bool> RemovableProperty = AvaloniaProperty.Register<Tag, bool>(nameof(Removable));
    public static readonly StyledProperty<ICommand?> RemoveCommandProperty = AvaloniaProperty.Register<Tag, ICommand?>(nameof(RemoveCommand));
    public static readonly StyledProperty<object?> RemoveCommandParameterProperty = AvaloniaProperty.Register<Tag, object?>(nameof(RemoveCommandParameter));
    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public Tone? Tone { get => GetValue(ToneProperty); set => SetValue(ToneProperty, value); }
    public bool Removable { get => GetValue(RemovableProperty); set => SetValue(RemovableProperty, value); }
    public ICommand? RemoveCommand { get => GetValue(RemoveCommandProperty); set => SetValue(RemoveCommandProperty, value); }
    public object? RemoveCommandParameter { get => GetValue(RemoveCommandParameterProperty); set => SetValue(RemoveCommandParameterProperty, value); }
    readonly TextBlock label = Build.Text("", Look.Text, TextSize.Label, 500);
    readonly StackPanel row = new() { Orientation = Orientation.Horizontal, Spacing = M(4) };
    IconButton? remove;
    public event Action? Removed;
    public Tag()
    {
        Height = M(24); CornerRadius = new CornerRadius(Radius); VerticalAlignment = VerticalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        row.Children.Add(label); Child = row; Refresh(); Restyle();
    }
    void EnsureRemove()
    {
        if (remove != null) return;
        remove = new IconButton { Icon = Icons.X, IconSize = M(12), Classes = { "quiet" }, Width = M(16), Height = M(16), MinWidth = 0, VerticalAlignment = VerticalAlignment.Center };
        remove[!Button.CommandProperty] = this[!RemoveCommandProperty]; remove[!Button.CommandParameterProperty] = this[!RemoveCommandParameterProperty];
        remove.Click += (_, _) => Removed?.Invoke();
        Words(remove, button => { ToolTip.SetTip(button, T("Remove")); Avalonia.Automation.AutomationProperties.SetName(button, T("Remove {0}", Text)); });
        row.Children.Add(remove);
    }
    public Tag(string text, Tone? tone = null, bool removable = false) : this() { Text = text; Tone = tone; Removable = removable; }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty || change.Property == RemovableProperty) Refresh();
        if (change.Property == ToneProperty) Restyle();
    }
    /// <summary>Text, the remove action and the padding follow the properties.</summary>
    void Refresh()
    {
        if (label == null || row == null) return;
        if (Removable) EnsureRemove();
        label.Text = Text; Padding = new Thickness(M(8), 0, M(Removable ? 4 : 8), 0);
        if (remove != null) { remove.IsVisible = Removable; Avalonia.Automation.AutomationProperties.SetName(remove, T("Remove {0}", Text)); }
    }
    /// <summary>The tone's tint is worked out per tag (a blend with Raised): painted again on a mode or accent switch.</summary>
    public void Restyle()
    {
        if (label == null) return;
        Background = Tone != null ? B(Tint(Tone)) : B(Fill); label.Foreground = B(Tone ?? Look.Text);
    }
}

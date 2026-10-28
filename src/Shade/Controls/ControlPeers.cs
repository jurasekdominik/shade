using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
namespace Shade;

internal sealed class DatePeer : ControlAutomationPeer, IValueProvider
{
    readonly Field owner;
    public DatePeer(Field owner) : base(owner)
    {
        this.owner = owner;
        owner.PropertyChanged += (_, e) => { if (e.Property == Field.TextProperty) RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, e.OldValue, e.NewValue); };
    }
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Edit;
    public bool IsReadOnly => owner.IsReadOnly || !owner.IsEffectivelyEnabled;
    public string Value => owner.Text;
    public void SetValue(string? value)
    {
        value ??= "";
        EnsureEnabled();
        if (IsReadOnly) throw new System.InvalidOperationException("The date is read-only.");
        bool accepted = owner is DateField day ? day.Input(value) : ((DateRangeField)owner).Input(value);
        if (!accepted) throw new System.ArgumentException("Enter a date within the allowed range.", nameof(value));
    }
}

/// <summary>A date field's typed blocks, which take the keyboard focus: they speak for the field (its name, help text and
/// value), so a screen reader announces the whole field when Tab reaches it.</summary>
internal sealed class BlocksPeer(Avalonia.Controls.Control owner) : ControlAutomationPeer(owner), IValueProvider
{
    AutomationPeer? Field => Avalonia.VisualTree.VisualExtensions.FindAncestorOfType<Field>(Owner) is { } input ? CreatePeerForElement(input) : null;
    IValueProvider? FieldValue => Field as IValueProvider;
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Edit;
    protected override string? GetNameCore() => Field?.GetName() ?? base.GetNameCore();
    protected override string? GetHelpTextCore() => Field?.GetHelpText() ?? base.GetHelpTextCore();
    public bool IsReadOnly => FieldValue?.IsReadOnly ?? true;
    public string Value => FieldValue?.Value ?? "";
    public void SetValue(string? value) { if (FieldValue is not { } input) throw new System.InvalidOperationException("The date blocks are not in a field."); input.SetValue(value); }
}

/// <summary>The colour helper as a value: its hex text ("#8C7AE6"), settable with a colour Avalonia can parse.</summary>
internal sealed class ColorPeer : ControlAutomationPeer, IValueProvider
{
    readonly ColorGrid owner;
    public ColorPeer(ColorGrid owner) : base(owner)
    {
        this.owner = owner;
        owner.PropertyChanged += (_, e) => { if (e.Property == ColorGrid.ValueProperty) RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, Look.HexOf((Avalonia.Media.Color)e.OldValue!), Look.HexOf((Avalonia.Media.Color)e.NewValue!)); };
    }
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;
    protected override string GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Look.T("Colour");
    public bool IsReadOnly => !owner.IsEffectivelyEnabled;
    public string Value => Look.HexOf(owner.Value);
    public void SetValue(string? value)
    {
        EnsureEnabled();
        if (!Avalonia.Media.Color.TryParse(value, out var colour)) throw new System.ArgumentException("Enter a colour such as #8C7AE6.", nameof(value));
        owner.Value = colour;
    }
}

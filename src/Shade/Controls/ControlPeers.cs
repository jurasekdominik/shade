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

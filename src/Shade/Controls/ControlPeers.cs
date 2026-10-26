using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
namespace Shade;

internal sealed class SwitchPeer : ControlAutomationPeer, IToggleProvider
{
    readonly Switch owner;
    public SwitchPeer(Switch owner) : base(owner)
    {
        this.owner = owner;
        owner.PropertyChanged += (_, e) =>
        {
            if (e.Property == Switch.OnProperty)
                RaisePropertyChangedEvent(Avalonia.Automation.TogglePatternIdentifiers.ToggleStateProperty, State(e.OldValue), State(e.NewValue));
        };
    }
    static ToggleState State(object? value) => value is true ? ToggleState.On : ToggleState.Off;
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.CheckBox;
    protected override string GetClassNameCore() => nameof(Switch);
    public ToggleState ToggleState => owner.On ? ToggleState.On : ToggleState.Off;
    public void Toggle() { EnsureEnabled(); owner.Toggle(); }
}

internal sealed class TickPeer : ControlAutomationPeer, IToggleProvider
{
    readonly Tick owner;
    public TickPeer(Tick owner) : base(owner)
    {
        this.owner = owner;
        owner.PropertyChanged += (_, e) =>
        {
            if (e.Property == Tick.StateProperty)
                RaisePropertyChangedEvent(Avalonia.Automation.TogglePatternIdentifiers.ToggleStateProperty, State(e.OldValue), State(e.NewValue));
        };
    }
    static ToggleState State(object? value) => value == null ? ToggleState.Indeterminate : value is true ? ToggleState.On : ToggleState.Off;
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.CheckBox;
    protected override string GetClassNameCore() => nameof(Tick);
    public ToggleState ToggleState => owner.State == null ? ToggleState.Indeterminate : owner.On ? ToggleState.On : ToggleState.Off;
    public void Toggle() { EnsureEnabled(); owner.Toggle(); }
}

internal sealed class StatePeer : ControlAutomationPeer, IToggleProvider
{
    readonly StateToggle owner;
    public StatePeer(StateToggle owner) : base(owner)
    {
        this.owner = owner;
        owner.PropertyChanged += (_, e) => { if (e.Property == StateToggle.OnProperty || e.Property == StateToggle.UnknownProperty) RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty, null, ToggleState); };
    }
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.CheckBox;
    public ToggleState ToggleState => owner.Unknown ? ToggleState.Indeterminate : owner.On ? ToggleState.On : ToggleState.Off;
    public void Toggle() { EnsureEnabled(); owner.Toggle(); }
}

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

internal sealed class ChipPeer(FilterChip owner) : ControlAutomationPeer(owner), IInvokeProvider
{
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;
    public void Invoke() { EnsureEnabled(); owner.Invoke(); }
}

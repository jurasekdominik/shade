using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Shade;

public partial class PaperFormView
{
    readonly Dictionary<string, PaperFieldPeer> fieldPeers = new(StringComparer.Ordinal);
    Control? automationAnchor;
    protected override AutomationPeer OnCreateAutomationPeer() => new PaperViewPeer(this);
    PaperFieldPeer FieldPeer(string id)
    {
        if (automationAnchor == null) { automationAnchor = new Control { IsVisible = false }; pageStack.Children.Add(automationAnchor); }
        if (!fieldPeers.TryGetValue(id, out var peer)) { fieldPeers[id] = peer = new(this, id, automationAnchor); peer.Notify(); }
        return peer;
    }
    void ResetRetiredPeers()
    {
        if (fieldPeers.Count == 0 || fieldPeers.Keys.Any(fieldIndex.ContainsKey)) return;
        // Native peers subscribe to their coordinate owner; retire that owner with the document, not the form.
        if (automationAnchor != null) pageStack.Children.Remove(automationAnchor);
        automationAnchor = null; fieldPeers.Clear();
    }
    void RefreshAutomation()
    {
        if (ControlAutomationPeer.FromElement(this) is PaperViewPeer root) root.Refresh();
        foreach (var page in Pages) if (ControlAutomationPeer.FromElement(page) is PaperPagePeer peer) peer.Refresh();
        // Keep removed identities for undo while this document remains open.
        foreach (var peer in fieldPeers.Values) peer.Notify();
    }
    void NotifyValuePeers(IEnumerable<string> keys)
    {
        foreach (string key in keys)
            if (sitesByKey.TryGetValue(key, out var site) && fieldPeers.TryGetValue(site.Structure.Id, out var peer)) peer.Notify();
    }
    sealed class PaperViewPeer(PaperFormView owner) : Avalonia.Controls.Automation.Peers.UserControlAutomationPeer(owner)
    {
        internal void Refresh() => InvalidateChildren();
        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore()
        {
            var children = base.GetChildrenCore();
            if (!owner.DrawPages) return children;
            owner.IndexFields(true);
            if (owner.ShowAllPages) return children;
            return (children ?? Array.Empty<AutomationPeer>()).Concat(owner.fieldIndex.Values.Where(f => f.Page != owner.PageIndex)
                .Select(f => owner.FindTemplate(f.Structure.Kind) == null ? owner.FieldPeer(f.Structure.Id) : null).OfType<AutomationPeer>()).ToArray();
        }
    }
    sealed class PaperPagePeer(PaperFormView owner, PaperPageCanvas page) : ControlAutomationPeer(page)
    {
        internal void Refresh() => InvalidateChildren();
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Document;
        protected override string GetNameCore() => Look.T("Page {0} of {1}", owner.Pages.IndexOf(page) + 1, owner.Pages.Count);
        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore()
        {
            if (!owner.DrawPages || !owner.pageLayouts.TryGetValue(page, out var layout)) return base.GetChildrenCore();
            owner.IndexFields(true);
            var result = new List<AutomationPeer>();
            foreach (var p in layout.placements)
            {
                if (owner.FindTemplate(p.Structure.Kind) != null)
                {
                    if (owner.editors.TryGetValue(p.Structure.Id, out var native)) result.Add(GetOrCreate(native.control));
                }
                else result.Add(owner.FieldPeer(p.Structure.Id));
            }
            if (owner.Mode == PaperFormMode.Design)
                foreach (var chrome in page.Children.Where(c => c.Focusable))
                {
                    var peer = GetOrCreate(chrome);
                    if (!result.Contains(peer)) result.Add(peer);
                }
            return result;
        }
    }
    sealed class PaperEditorPeer(PaperFormView owner, PaperEditorHost host) : ControlAutomationPeer(host)
    {
        protected override AutomationPeer? GetParentCore() => owner.activeField is { } id ? owner.FieldPeer(id) : base.GetParentCore();
        protected override bool IsContentElementCore() => false;
        protected override bool IsControlElementCore() => false;
    }
    // A control-backed peer supplies native screen conversion without creating a control for each field.
    sealed class PaperFieldPeer(PaperFormView owner, string id, Control coordinates) : ControlAutomationPeer(coordinates), IValueProvider, IToggleProvider
    {
        PaperField? Field => owner.fieldIndex.TryGetValue(id, out var site) ? site : null;
        PaperFormValue? InputValue => Field is { } site ? owner.Value(site.Structure) : null;
        bool Active => owner.activeField == id && owner.ActiveEditor != null;
        bool InputField => Field is { } site && PaperStructureKind.HasValue(site.Structure.Kind);
        string name = "", value = "", help = "";
        ToggleState toggle;
        bool wasActive;
        internal void Notify()
        {
            string nextName = GetName() ?? "", nextValue = Value, nextHelp = GetHelpText() ?? "";
            if (name != nextName) RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, name, nextName);
            if (value != nextValue) RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, value, nextValue);
            if (help != nextHelp) RaisePropertyChangedEvent(AutomationElementIdentifiers.HelpTextProperty, help, nextHelp);
            if (toggle != ToggleState) RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty, toggle, ToggleState);
            if (wasActive != Active) RaiseChildrenChangedEvent();
            name = nextName; value = nextValue; help = nextHelp; toggle = ToggleState; wasActive = Active;
        }
        protected override string GetAcceleratorKeyCore() => "";
        protected override string GetAccessKeyCore() => "";
        protected override string GetAutomationIdCore() => id;
        protected override string GetClassNameCore() => "PaperField";
        protected override AutomationPeer? GetLabeledByCore() => null;
        protected override AutomationPeer? GetParentCore() => Field is { } site && (owner.ShowAllPages || site.Page == owner.PageIndex)
            ? ControlAutomationPeer.CreatePeerForElement(owner.Pages[site.Page]) : ControlAutomationPeer.CreatePeerForElement(owner);
        protected override string GetNameCore() => Field is { } site ? owner.DisplayName(site.Structure) : "";
        protected override AutomationControlType GetAutomationControlTypeCore() => Field?.Structure.Kind switch
        {
            PaperStructureKind.Check => AutomationControlType.CheckBox,
            PaperStructureKind.Image => AutomationControlType.Image,
            PaperStructureKind.Group => AutomationControlType.Group,
            _ => InputField ? AutomationControlType.Edit : AutomationControlType.Text
        };
        protected override string GetHelpTextCore() => InputValue?.Error ?? (owner.found.TryGetValue(id, out var result)
            ? string.Join(" ", result.problems.Where(p => !p.IsWarning).Select(p => p.Message)) : "");
        protected override string GetItemStatusCore() => GetHelpTextCore() is { Length: > 0 } error ? error : IsReadOnly ? Look.T("Read only") : "";
        protected override string GetPlaceholderTextCore() => Field is { Structure.Kind: PaperStructureKind.Date } site ? site.Structure.DateFormat : "";
        protected override bool IsContentElementCore() => true;
        protected override bool IsControlElementCore() => true;
        protected override bool IsEnabledCore() => owner.DrawPages && owner.IsEffectivelyEnabled && Field != null;
        protected override bool IsKeyboardFocusableCore() => owner.InteractiveDrawing && InputField && Field != null;
        protected override bool HasKeyboardFocusCore() => Active && owner.ActiveEditor!.IsKeyboardFocusWithin;
        protected override void SetFocusCore() { EnsureEnabled(); owner.ActivateEditor(id); }
        protected override void BringIntoViewCore() { if (Field is { } site) { owner.SetCurrentValue(PageIndexProperty, site.Page); owner.Pages[site.Page].BringIntoView(); } }
        protected override Rect GetBoundingRectangleCore()
        {
            if (Field is not { } site || TopLevel.GetTopLevel(owner.Pages[site.Page]) is not Visual root || owner.Pages[site.Page].TransformToVisual(root) is not { } transform) return default;
            return new Rect(site.Bounds.X * PaperFormDefinition.DipsPerMm, site.Bounds.Y * PaperFormDefinition.DipsPerMm,
                site.Bounds.Width * PaperFormDefinition.DipsPerMm, site.Bounds.Height * PaperFormDefinition.DipsPerMm).TransformToAABB(transform);
        }
        protected override bool IsOffscreenCore()
        {
            if (!owner.IsEffectivelyVisible || Field is not { } site || owner.Pages[site.Page].GetTransformedBounds() is not { } bounds) return true;
            return !GetBoundingRectangleCore().Intersects(bounds.Clip);
        }
        protected override IReadOnlyList<AutomationPeer> GetOrCreateChildrenCore() => Active
            ? new[] { ControlAutomationPeer.CreatePeerForElement(owner.editorHost) } : Array.Empty<AutomationPeer>();
        protected override object? GetProviderCore(Type providerType)
        {
            if (providerType == typeof(IValueProvider)) return InputField && Field?.Structure.Kind != PaperStructureKind.Check ? this : null;
            if (providerType == typeof(IToggleProvider)) return Field?.Structure.Kind == PaperStructureKind.Check ? this : null;
            return base.GetProviderCore(providerType);
        }
        public bool IsReadOnly => owner.Mode != PaperFormMode.Fill || Field is not { } site || site.Structure.IsReadOnly || !owner.IsEffectivelyEnabled;
        public string Value => InputValue?.Text ?? "";
        public void SetValue(string? text)
        {
            EnsureEnabled();
            if (IsReadOnly) throw new InvalidOperationException(Look.T("The field is read-only."));
            if (owner.ActivateEditor(id) is { } editor && Input(editor) is Control input && ControlAutomationPeer.CreatePeerForElement(input).GetProvider<IValueProvider>() is { } provider) provider.SetValue(text);
        }
        public ToggleState ToggleState => InputValue?.Checked switch { true => ToggleState.On, false => ToggleState.Off, _ => ToggleState.Indeterminate };
        public void Toggle()
        {
            EnsureEnabled();
            if (IsReadOnly) throw new InvalidOperationException(Look.T("The field is read-only."));
            if (owner.ActivateEditor(id) is { } editor) ControlAutomationPeer.CreatePeerForElement(editor).GetProvider<IToggleProvider>()?.Toggle();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Shade;

public partial class PaperFormView
{
    string? activeField, hoveredField;
    sealed class PooledInput(PaperStructure style, PaperFormMode mode, Control control)
    {
        internal PaperStructure Style = style;
        internal readonly PaperFormMode Mode = mode;
        internal readonly Control Control = control;
        internal PaperFormValue? Value;
        internal readonly List<IDisposable> Bindings = new();
        internal void Unbind() { foreach (var binding in Bindings) binding.Dispose(); Bindings.Clear(); Value = null; }
    }
    readonly Dictionary<string, PooledInput> editorPool = new(StringComparer.Ordinal);
    readonly PaperEditorHost editorHost;
    readonly List<Control> editorAncestors = new();
    static readonly IReadOnlyDictionary<string, string> EditorProperties = new Dictionary<string, string>();
    internal readonly record struct PaperField(PaperStructure Structure, PaperBounds Bounds, int Page);
    readonly Dictionary<string, PaperField> fieldIndex = new(StringComparer.Ordinal);
    PaperField[] tabFields = Array.Empty<PaperField>();
    IReadOnlyList<PaperPage>? indexedPages;
    object? indexedTypes;
    bool changingEditor;
    internal string? ActiveField => activeField;
    internal Control? ActiveEditor => activeField != null ? editorHost.Child : null;
    bool InteractiveDrawing => DrawPages && Mode != PaperFormMode.Design;
    PaperStructure EditorStyle(PaperStructure s) => s with
    {
        Id = "",
        FieldKey = "",
        Label = "",
        Bounds = s.Bounds with { X = 0, Y = 0 },
        Title = TitleAbove(s) ? "" : s.Title,
        Children = Array.Empty<PaperStructure>(),
        Properties = EditorProperties,
        Background = s.Background ?? Definition.Background,
        Foreground = s.Foreground ?? Definition.Foreground
    };
    Control PooledEditor(PaperStructure s, PaperFormValue? value)
    {
        var style = EditorStyle(s);
        if (!editorPool.TryGetValue(s.Kind, out var pooled) || pooled.Mode != Mode)
        {
            pooled?.Unbind();
            var control = Create(s, null);
            if (control is CharacterGridField) control = new Border { Background = Avalonia.Media.Brushes.Transparent, Child = control };
            editorPool[s.Kind] = pooled = new(style, Mode, control);
        }
        else if (pooled.Style with { CellRanges = style.CellRanges } != style || !pooled.Style.CellRanges.SequenceEqual(style.CellRanges))
        {
            if (pooled.Control is PaperCheckBox check) StyleCheckBox(check, s);
            else if (pooled.Control.GetSelfAndVisualDescendants().OfType<TextBox>().FirstOrDefault() is { } box) StyleTextBox(box, s, pooled.Style);
            if (pooled.Control is DockPanel panel) StyleCaption(panel, s);
            pooled.Style = style;
        }
        var result = pooled.Control;
        if (!ReferenceEquals(pooled.Value, value) || pooled.Bindings.Count == 0)
        {
            pooled.Unbind(); pooled.Value = value;
            foreach (var box in result.GetSelfAndVisualDescendants().OfType<TextBox>())
            {
                box.IsUndoEnabled = false;
                if (value != null) pooled.Bindings.Add(box.Bind(TextBox.TextProperty, new Binding(nameof(PaperFormValue.Text)) { Source = value, Mode = BindingMode.TwoWay }));
                box.CaretIndex = box.SelectionStart = box.SelectionEnd = 0; box.IsUndoEnabled = true;
            }
            if (result is CheckBox check && value != null) pooled.Bindings.Add(check.Bind(CheckBox.IsCheckedProperty, new Binding(nameof(PaperFormValue.Checked)) { Source = value, Mode = BindingMode.TwoWay }));
        }
        foreach (var box in result.GetSelfAndVisualDescendants().OfType<TextBox>()) AutomationProperties.SetName(box, DisplayName(s));
        AutomationProperties.SetName(result, DisplayName(s));
        if (editorHost.Child != result) editorHost.Child = result;
        return editorHost;
    }
    void IndexFields(bool force = false)
    {
        if (!force && Mode == PaperFormMode.Design && fieldPeers.Count == 0) return;
        if (ReferenceEquals(indexedPages, Definition.Pages) && ReferenceEquals(indexedTypes, StructureTypes)) return;
        indexedPages = Definition.Pages; indexedTypes = StructureTypes; fieldIndex.Clear();
        var inputs = new List<PaperField>();
        for (int i = 0; i < Pages.Count; i++)
            foreach (var p in pageLayouts[Pages[i]].placements)
            {
                var field = new PaperField(p.Structure, p.Bounds, i); fieldIndex.Add(p.Structure.Id, field);
                if (PaperStructureKind.HasValue(p.Structure.Kind) || FindTemplate(p.Structure.Kind) != null) inputs.Add(field);
            }
        tabFields = inputs.ToArray();
        ResetRetiredPeers();
        if (activeField != null && !fieldIndex.ContainsKey(activeField)) DeactivateEditor();
    }
    static InputElement? Input(Control control) => control.Focusable ? control : control.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c.Focusable);
    internal Control? ActivateEditor(string id, NavigationMethod method = NavigationMethod.Unspecified)
    {
        if (!InteractiveDrawing || !IsEffectivelyEnabled || !IsEffectivelyVisible || !fieldIndex.TryGetValue(id, out var field)) return null;
        var structure = field.Structure; bool custom = FindTemplate(structure.Kind) != null;
        if (!custom && !PaperStructureKind.HasValue(structure.Kind)) return null;
        if (custom)
        {
            changingEditor = true;
            try { DeactivateEditor(); SetCurrentValue(PageIndexProperty, field.Page); Refresh(); }
            finally { changingEditor = false; }
            if (!editors.TryGetValue(id, out var native) || Input(native.control) is not { } target) return null;
            native.control.BringIntoView(); target.Focus(method); return native.control;
        }
        if (activeField != id)
        {
            changingEditor = true;
            try
            {
                DeactivateEditor(); activeField = id;
                SetCurrentValue(PageIndexProperty, field.Page); Refresh();
            }
            finally { changingEditor = false; }
        }
        var editor = ActiveEditor;
        if (editor == null) return null;
        var focus = Input(editor);
        foreach (var ancestor in this.GetSelfAndVisualAncestors().OfType<Control>()) { ancestor.PropertyChanged -= EditorState; ancestor.PropertyChanged += EditorState; if (!editorAncestors.Contains(ancestor)) editorAncestors.Add(ancestor); }
        editor.BringIntoView(); focus?.Focus(method); UpdateTabStops(); return editor;
    }
    void DeactivateEditor()
    {
        if (activeField == null) return;
        string old = activeField; activeField = null;
        foreach (var ancestor in editorAncestors) ancestor.PropertyChanged -= EditorState; editorAncestors.Clear();
        editorHost.Child = null;
        foreach (var pooled in editorPool.Values) pooled.Unbind();
        if (editors.Remove(old, out var editor) && editor.control.Parent is Canvas page) page.Children.Remove(editor.control);
        if (titles.Remove(old, out var title) && title.Parent is Canvas titlePage) titlePage.Children.Remove(title);
        if (!refreshing && !changingEditor) Refresh();
    }
    void CheckEditorFocus()
    {
        if (changingEditor) return;
        UpdateTabStops();
        if (ActiveEditor is not { } editor) return;
        var focus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        if (focus != null && (focus.GetSelfAndVisualAncestors().Contains(editor) || focus.GetSelfAndLogicalAncestors().Contains(editor))) return;
        DeactivateEditor();
    }
    void EditorState(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if ((e.Property == IsVisibleProperty || e.Property == IsEffectivelyEnabledProperty) && (!IsEffectivelyVisible || !IsEffectivelyEnabled)) DeactivateEditor();
    }
    void UpdateTabStops()
    {
        bool inside = activeField != null || editors.Values.Any(e => e.control.IsKeyboardFocusWithin);
        foreach (var page in Pages) page.Focusable = InteractiveDrawing && !inside;
    }
    void EditorKey(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Tab || e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Alt)) return;
        if (!InteractiveDrawing) return;
        string? current = activeField ?? editors.FirstOrDefault(p => p.Value.control.IsKeyboardFocusWithin).Key;
        int index = Array.FindIndex(tabFields, p => p.Structure.Id == current), direction = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1;
        if (index < 0) return;
        for (int next = index + direction; next >= 0 && next < tabFields.Length; next += direction)
        {
            if (ActivateEditor(tabFields[next].Structure.Id, NavigationMethod.Tab) == null) continue;
            e.Handled = true; break;
        }
    }
    PaperPlacement? HitField(Canvas page, Point point)
    {
        if (!pageLayouts.TryGetValue(page, out var layout)) return null;
        for (int i = layout.placements.Length - 1; i >= 0; i--) if (layout.placements[i].Bounds.Rect.Contains(point / PaperFormDefinition.DipsPerMm)) return layout.placements[i];
        return null;
    }
    void PressField(Canvas page, PointerPressedEventArgs e)
    {
        var buttons = e.GetCurrentPoint(page).Properties;
        if (!InteractiveDrawing || changingEditor || !buttons.IsLeftButtonPressed && !buttons.IsRightButtonPressed) return;
        var hit = HitField(page, e.GetPosition(page));
        if (hit is not { } field || !PaperStructureKind.HasValue(field.Structure.Kind) && FindTemplate(field.Structure.Kind) == null) { DeactivateEditor(); return; }
        var editor = ActivateEditor(field.Structure.Id, NavigationMethod.Pointer);
        if (editor == null) return;
        TopLevel.GetTopLevel(this)?.UpdateLayout();
        var at = e.GetPosition(editor);
        var target = editor.InputHitTest(at) as Interactive ?? editor;
        var forwarded = new PointerPressedEventArgs(target, e.Pointer, editor, at, e.Timestamp, e.GetCurrentPoint(editor).Properties, e.KeyModifiers, e.ClickCount);
        target.RaiseEvent(forwarded);
        if (e.Pointer.Captured is not Visual captured || !captured.GetSelfAndVisualAncestors().Contains(editor)) e.Pointer.Capture(editor);
        e.Handled = true;
    }
    void HoverField(Canvas page, Point? at)
    {
        string? next = InteractiveDrawing && at is { } point ? HitField(page, point)?.Structure.Id : null;
        if (hoveredField == next) return;
        var changed = new[] { hoveredField, next }.OfType<string>().ToHashSet(StringComparer.Ordinal); hoveredField = next;
        if (painter != null)
            foreach (var s in All(Definition.Pages.SelectMany(p => p.Structures)).Where(s => changed.Contains(s.Id) && DrawStructure(s))) painter.PrepareDrawing(s, Value(s), s.Id == hoveredField);
        foreach (var band in drawnPages.Values.SelectMany(p => p)) band.InvalidateFields(changed);
    }
    sealed class PaperEditorHost(PaperFormView owner) : Decorator
    {
        protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new PaperEditorPeer(owner, this);
    }
    sealed class PaperPageCanvas : Canvas
    {
        readonly PaperFormView owner;
        internal PaperPageCanvas(PaperFormView owner) { this.owner = owner; AddHandler(KeyDownEvent, owner.EditorKey, RoutingStrategies.Tunnel); }
        protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new PaperPagePeer(owner, this);
        protected override void OnPointerPressed(PointerPressedEventArgs e) { base.OnPointerPressed(e); if (!e.Handled) owner.PressField(this, e); }
        protected override void OnPointerMoved(PointerEventArgs e) { base.OnPointerMoved(e); owner.HoverField(this, e.GetPosition(this)); }
        protected override void OnPointerExited(PointerEventArgs e) { base.OnPointerExited(e); owner.HoverField(this, null); }
        protected override void OnGotFocus(FocusChangedEventArgs e)
        {
            base.OnGotFocus(e); owner.UpdateTabStops();
            if (e.Source != this || e.NavigationMethod != NavigationMethod.Tab) return;
            var fields = owner.tabFields.Where(f => f.Page == owner.Pages.IndexOf(this));
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) fields = fields.Reverse();
            foreach (var field in fields) if (owner.ActivateEditor(field.Structure.Id, NavigationMethod.Tab) != null) break;
        }
        protected override void OnLostFocus(FocusChangedEventArgs e) { base.OnLostFocus(e); Dispatcher.UIThread.Post(owner.CheckEditorFocus); }
    }
}

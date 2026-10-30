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
    readonly Dictionary<string, (PaperStructure style, Control control)> editorPool = new(StringComparer.Ordinal);
    bool changingEditor;
    internal string? ActiveField => activeField;
    internal Control? ActiveEditor => activeField != null && editors.TryGetValue(activeField, out var editor) ? editor.control : null;
    static PaperStructure EditorStyle(PaperStructure s) => s with
    {
        Id = "", FieldKey = "", Label = "", Bounds = s.Bounds with { X = 0, Y = 0 },
        Title = TitleAbove(s) ? "" : s.Title, Children = Array.Empty<PaperStructure>()
    };
    Control PooledEditor(PaperStructure s, PaperFormValue? value)
    {
        var style = EditorStyle(s);
        if (!editorPool.TryGetValue(s.Kind, out var pooled) || pooled.style != style)
        {
            var control = Create(s, value);
            control.AddHandler(LostFocusEvent, (_, _) => Dispatcher.UIThread.Post(CheckEditorFocus));
            control.AddHandler(KeyDownEvent, EditorKey, RoutingStrategies.Tunnel);
            editorPool[s.Kind] = pooled = (style, control);
        }
        var result = pooled.control;
        foreach (var box in result.GetSelfAndVisualDescendants().OfType<TextBox>())
        {
            box.IsUndoEnabled = false;
            box.Bind(TextBox.TextProperty, new Binding(nameof(PaperFormValue.Text)) { Source = value, Mode = BindingMode.TwoWay });
            box.CaretIndex = box.SelectionStart = box.SelectionEnd = 0; box.IsUndoEnabled = true;
            AutomationProperties.SetName(box, DisplayName(s));
        }
        if (result is CheckBox check) check.Bind(CheckBox.IsCheckedProperty, new Binding(nameof(PaperFormValue.Checked)) { Source = value, Mode = BindingMode.TwoWay });
        AutomationProperties.SetName(result, DisplayName(s));
        return result;
    }
    internal Control? ActivateEditor(string id, NavigationMethod method = NavigationMethod.Unspecified)
    {
        if (!DrawPages || Mode != PaperFormMode.Fill || !IsEffectivelyEnabled || !IsEffectivelyVisible) return null;
        var field = Definition.Pages.SelectMany((p, i) => LayoutPage(p).Select(s => (placement: s, page: i))).FirstOrDefault(p => p.placement.Structure.Id == id);
        if (field.placement.Structure is not { } structure || !PaperStructureKind.HasValue(structure.Kind)) return null;
        if (activeField != id)
        {
            changingEditor = true;
            try
            {
                DeactivateEditor(); activeField = id;
                SetCurrentValue(PageIndexProperty, field.page); Refresh();
            }
            finally { changingEditor = false; }
        }
        var editor = ActiveEditor;
        if (editor == null) return null;
        var focus = editor.GetSelfAndVisualDescendants().OfType<TextBox>().FirstOrDefault() as InputElement ?? editor;
        editor.BringIntoView(); focus.Focus(method); return editor;
    }
    void DeactivateEditor()
    {
        if (activeField == null) return;
        string old = activeField; activeField = null;
        if (editors.Remove(old, out var editor) && editor.control.Parent is Canvas page) page.Children.Remove(editor.control);
        if (titles.Remove(old, out var title) && title.Parent is Canvas titlePage) titlePage.Children.Remove(title);
        if (!refreshing && !changingEditor) Refresh();
    }
    void CheckEditorFocus()
    {
        if (changingEditor || ActiveEditor is not { } editor) return;
        var focus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control;
        if (focus != null && (focus.GetSelfAndVisualAncestors().Contains(editor) || focus.GetSelfAndLogicalAncestors().Contains(editor))) return;
        DeactivateEditor();
    }
    void EditorKey(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Tab || e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Alt)) return;
        var fields = Definition.Pages.SelectMany(p => LayoutPage(p)).Where(p => PaperStructureKind.HasValue(p.Structure.Kind)).ToArray();
        int index = Array.FindIndex(fields, p => p.Structure.Id == activeField), direction = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1;
        if (index + direction >= 0 && index + direction < fields.Length)
        {
            var target = fields[index + direction].Structure;
            if (FindTemplate(target.Kind) == null) ActivateEditor(target.Id, NavigationMethod.Tab);
            else if (editors.TryGetValue(target.Id, out var native)) native.control.Focus(NavigationMethod.Tab);
            e.Handled = true;
        }
    }
    void PressField(PaperPageVisual visual, PointerPressedEventArgs e)
    {
        if (Mode != PaperFormMode.Fill || changingEditor) return;
        var point = e.GetPosition(visual);
        var hit = visual.HitField(point);
        if (hit is not { } field || !PaperStructureKind.HasValue(field.Structure.Kind)) { DeactivateEditor(); return; }
        var editor = ActivateEditor(field.Structure.Id, NavigationMethod.Pointer);
        if (editor == null) return;
        TopLevel.GetTopLevel(this)?.UpdateLayout();
        var at = e.GetPosition(editor);
        var target = editor.InputHitTest(at) as Interactive ?? editor;
        var forwarded = new PointerPressedEventArgs(target, e.Pointer, editor, at, e.Timestamp, e.GetCurrentPoint(editor).Properties, e.KeyModifiers, e.ClickCount);
        target.RaiseEvent(forwarded); e.Handled = true;
    }
    void HoverField(PaperPageVisual visual, Point? at)
    {
        string? next = Mode == PaperFormMode.Fill && at is { } point ? visual.HitField(point)?.Structure.Id : null;
        if (hoveredField == next) return;
        var changed = new[] { hoveredField, next }.OfType<string>().ToHashSet(StringComparer.Ordinal); hoveredField = next;
        if (painter != null)
            foreach (var s in All(Definition.Pages.SelectMany(p => p.Structures)).Where(s => changed.Contains(s.Id) && DrawStructure(s))) painter.PrepareDrawing(s, Value(s), s.Id == hoveredField);
        foreach (var band in drawnPages.Values.SelectMany(p => p)) band.InvalidateFields(changed);
    }
}

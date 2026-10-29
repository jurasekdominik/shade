using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using static Shade.Look;
using static Shade.PaperFormDefinition;

namespace Shade;

public sealed partial class PaperFormBuilder
{
    const string ClipboardHeader = "Shade.PaperStructure/1\n";
    sealed record CopiedValue(string Key, string Text, bool? Checked);
    sealed record CopiedBlock(PaperStructure Structure, IReadOnlyList<CopiedValue> Values, IReadOnlyList<PaperStructure>? Others = null);

    PaperStructure NameNewStructures(PaperStructure source, bool duplicate = false, Dictionary<string, string>? renamed = null)
    {
        var used = All(Definition.Pages.SelectMany(p => p.Structures)).Select(s => s.FieldKey).Concat(Values.Select(v => v.Key)).ToHashSet(StringComparer.Ordinal);
        used.UnionWith(All(new[] { source }).Select(s => s.FieldKey).Where(k => k.Length > 0));
        PaperStructure Name(PaperStructure s)
        {
            string key = s.FieldKey;
            if ((duplicate && key.Length > 0) || key.Length == 0 && s.Kind is not ("label" or "group" or "line"))
            {
                string stem = key.Length > 0 ? key + "_" : "field"; int i = 1; while (!used.Add(key = stem + i++)) { }
                if (s.FieldKey.Length > 0 && renamed != null) renamed[s.FieldKey] = key;
            }
            if (s.Kind == "characters" && s.GridAlignment == Avalonia.Media.TextAlignment.Left) s = s with { Bounds = s.Bounds with { Width = s.CellCount * s.CellWidth } };
            return s with { Id = duplicate ? Guid.NewGuid().ToString("N") : s.Id, FieldKey = key, Children = s.Children.Select(Name).ToArray() };
        }
        return Name(source);
    }

    void InsertAt(PaperStructure structure, Point point)
    {
        if (Mode != PaperFormMode.Design) return;
        structure = NameNewStructures(structure);
        structure = structure with { Bounds = structure.Bounds with { X = point.X, Y = point.Y } };
        AddStructure(structure);
    }

    PaperStructure NewStructure(string kind, PaperBounds bounds)
    {
        var result = new PaperStructure { Kind = kind, Bounds = bounds, Title = kind == "label" ? T("Label") : kind == "caption" ? T("Title") : "", Alignment = kind == "characters" ? Avalonia.Media.TextAlignment.Center : Avalonia.Media.TextAlignment.Left };
        if (kind == "line") result = result with { LineOrientation = bounds.Height > bounds.Width ? Avalonia.Layout.Orientation.Vertical : Avalonia.Layout.Orientation.Horizontal };
        if (kind == "characters")
        {
            int count = Math.Clamp((int)Math.Round(bounds.Width / result.CellWidth), 1, 1000);
            result = result with { CellCount = count, Bounds = bounds with { Width = count * result.CellWidth } };
        }
        return result;
    }

    void InsertMenu(Control anchor, Point point)
    {
        if (Mode != PaperFormMode.Design) return;
        var hit = HitCanvas(point, true).structure;
        if (hit != null && !SelectedIds.Contains(hit.Id)) SetSelection(new[] { hit.Id });
        var entries = new List<MenuEntry>(); var actions = new List<Action>();
        void Entry(MenuEntry item, Action action) { entries.Add(item); actions.Add(action); }
        if (hit != null)
        {
            Entry(new MenuEntry("Copy") { Shortcut = "Ctrl+C" }, () => _ = CopySelectedAsync());
            Entry(new MenuEntry("Duplicate") { Shortcut = "Ctrl+D" }, DuplicateSelected);
            bool snapping = All(Definition.Pages.SelectMany(p => p.Structures)).Where(s => SelectedIds.Contains(s.Id)).All(s => s.SnapToGrid);
            Entry(new MenuEntry("Snap to grid") { IsChecked = snapping }, () => SetSelectionSnapping(!snapping));
            Entry(MenuEntry.Line, () => { });
            Entry(new MenuEntry("Delete") { IsDanger = true }, DeleteSelected);
            Menu.Show(anchor, entries, item => actions[entries.IndexOf(item)](), atPointer: true, scrollable: false); return;
        }
        if (GuideAt(point) is { } guide)
        {
            Entry(new MenuEntry("Remove guide"), () => RemoveGuide(guide.Id));
            Menu.Show(anchor, entries, item => actions[entries.IndexOf(item)](), atPointer: true, scrollable: false); return;
        }
        SetSelection(Array.Empty<string>());
        foreach (var (kind, label) in Palette.Concat((StructureTypes ?? Array.Empty<PaperStructureTemplate>()).Select(t => (t.Kind, t.Label))))
            Entry(new MenuEntry("Insert " + label.ToLowerInvariant()), () =>
            {
                double w = kind == "characters" ? 64.4 : kind == "check" ? 5 : kind == "group" ? 80 : 50;
                double h = kind is "multiline" or "caption" ? 20 : kind == "check" ? 5 : kind == "group" ? 30 : kind == "line" ? GridPitch : 8;
                var size = Definition.PageSize;
                var at = new Point(Math.Clamp(Math.Round(point.X / GridPitch) * GridPitch, Definition.MarginLeft, Math.Max(Definition.MarginLeft, size.Width - Definition.MarginRight - w)), Math.Clamp(Math.Round(point.Y / GridPitch) * GridPitch, Definition.MarginTop, Math.Max(Definition.MarginTop, size.Height - Definition.MarginBottom - h)));
                InsertAt(NewStructure(kind, new PaperBounds(at.X, at.Y, w, h)), at); if (kind == PaperStructureKind.Image) ChooseImage();
            });
        Entry(MenuEntry.Line, () => { });
        Entry(new MenuEntry("Add vertical guide"), () => AddGuide(PaperGuideOrientation.Vertical, Math.Clamp(Grid(point.X), 0, Definition.PageSize.Width)));
        Entry(new MenuEntry("Add horizontal guide"), () => AddGuide(PaperGuideOrientation.Horizontal, Math.Clamp(Grid(point.Y), 0, Definition.PageSize.Height)));
        Entry(new MenuEntry("Paste") { Shortcut = "Ctrl+V" }, () => _ = PasteAtAsync(point));

        Menu.Show(anchor, entries, item => actions[entries.IndexOf(item)](), atPointer: true, scrollable: false);
    }

    CopiedBlock? CopySnapshot()
    {
        var roots = Definition.Pages.SelectMany(p => SelectedRoots(p.Structures)).Select(p => p.structure with { Bounds = p.bounds }).ToArray();
        if (roots.Length == 0) return null;
        var keys = All(roots).Select(s => s.FieldKey).ToHashSet(StringComparer.Ordinal);
        return new(roots[0], Values.Where(v => keys.Contains(v.Key)).Select(v => new CopiedValue(v.Key, v.Text, v.Checked)).ToArray(), roots.Skip(1).ToArray());
    }

    /// <summary>Copy the selected block/group and its input values to the system clipboard.</summary>
    public async Task CopySelectedAsync()
    {
        if (Mode != PaperFormMode.Design || CopySnapshot() is not { } block) return;
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) return;
        try { await clipboard.SetTextAsync(ClipboardHeader + JsonSerializer.Serialize(block)); }
        catch (Exception error) when (ClipboardFailure(error)) { feedback.Text = T("Clipboard is not available."); }
    }

    /// <summary>Paste a copied Shade block on the current page, with new IDs and automatically generated keys.</summary>
    public Task PasteAsync() => PasteAtAsync(null);
    async Task PasteAtAsync(Point? position)
    {
        if (Mode != PaperFormMode.Design || TopLevel.GetTopLevel(this) is not { } root || root.Clipboard is not { } clipboard) return;
        var definition = Definition; int page = PageIndex;
        try
        {
            string? text = await clipboard.TryGetTextAsync();
            if (Definition != definition || PageIndex != page || Mode != PaperFormMode.Design || TopLevel.GetTopLevel(this) != root || !IsEffectivelyEnabled) return;
            if (text == null || text.Length > 10_000_000 || !text.StartsWith(ClipboardHeader, StringComparison.Ordinal)) return;
            var copy = JsonSerializer.Deserialize<CopiedBlock>(text.Substring(ClipboardHeader.Length));
            if (copy?.Structure == null || copy.Values == null) return;
            var structures = new[] { copy.Structure }.Concat(copy.Others ?? Array.Empty<PaperStructure>()).ToArray();
            new PaperFormDefinition { Pages = new[] { new PaperPage { Structures = structures } } }.Validate();
            if (copy.Values.Any(v => v == null || v.Key == null || v.Text == null)) return;
            if (All(structures).Any(s => !BuiltIn(s.Kind) && FindTemplate(s.Kind) == null)) { feedback.Text = T("This block type is not available."); return; }
            PasteSnapshot(copy, position);
        }
        catch (Exception error) when (ClipboardFailure(error) || error is JsonException or ArgumentException) { feedback.Text = T("The clipboard does not contain a valid Shade block."); }
    }
    static bool ClipboardFailure(Exception error) => error is System.IO.IOException or System.Runtime.InteropServices.ExternalException or InvalidOperationException or NotSupportedException or OperationCanceledException;

    void PasteSnapshot(CopiedBlock copy, Point? point)
    {
        using var update = new UpdateScope(this);
        var renamed = new Dictionary<string, string>();
        var roots = new[] { copy.Structure }.Concat(copy.Others ?? Array.Empty<PaperStructure>()).ToArray();
        double x = point?.X - copy.Structure.Bounds.X ?? GridPitch, y = point?.Y - copy.Structure.Bounds.Y ?? GridPitch;
        var clones = roots.Select(s => NameNewStructures(s, true, renamed)).Select(s => FitGrid(s with { Bounds = s.Bounds with { X = s.Bounds.X + x, Y = s.Bounds.Y + y } })).ToArray();
        var structures = Structures.ToArray(); foreach (var clone in clones) structures = InsertInto(structures, clone);
        Edit(WithStructures(structures)); SetSelection(clones.Select(s => s.Id));
        foreach (var value in copy.Values)
            if (renamed.TryGetValue(value.Key, out string? key) && Values.FirstOrDefault(v => v.Key == key) is { } target) { target.Text = value.Text; target.Checked = value.Checked; }
    }

    void ZoomWheel(object? sender, PointerWheelEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            var page = Page; var point = e.GetPosition(page); var before = page.TranslatePoint(point, viewport);
            SetCurrentValue(ZoomProperty, Math.Clamp(Zoom * Math.Pow(1.1, e.Delta.Y), MinimumZoom, 4)); viewport.UpdateLayout();
            if (before is { } anchor && page.TranslatePoint(point, viewport) is { } after)
                viewport.Offset += after - anchor;
            e.Handled = true;
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) { viewport.Offset = new Vector(Math.Max(0, viewport.Offset.X - e.Delta.Y * M(48)), viewport.Offset.Y); e.Handled = true; }
    }
}

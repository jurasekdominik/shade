using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using static Shade.Look;

namespace Shade;

public sealed partial class EditableTable
{
    /// <summary>Where the native ribbon shows effective paper dimensions.</summary>
    public enum PaperDimensionsPosition { Bottom, FormatLabel }
    public static readonly StyledProperty<PaperDimensionsPosition> PaperDimensionsPlacementProperty = AvaloniaProperty.Register<EditableTable, PaperDimensionsPosition>(nameof(PaperDimensionsPlacement), PaperDimensionsPosition.Bottom, validate: value => Enum.IsDefined(value));
    /// <summary>Bottom keeps the separate note row; FormatLabel appends dimensions to the paper-format caption.</summary>
    public PaperDimensionsPosition PaperDimensionsPlacement { get => GetValue(PaperDimensionsPlacementProperty); set => SetValue(PaperDimensionsPlacementProperty, value); }
    /// <summary>The paper layout used by the preview and the application's print/export pipeline.</summary>
    public enum PaperKind { Screen, A5, A4, A3, Letter, Legal, Custom }
    public static readonly StyledProperty<PaperKind> PaperFormatProperty = AvaloniaProperty.Register<EditableTable, PaperKind>(nameof(PaperFormat), PaperKind.Screen, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay, validate: value => Enum.IsDefined(value));
    public static readonly StyledProperty<bool> PaperLandscapeProperty = AvaloniaProperty.Register<EditableTable, bool>(nameof(PaperLandscape), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public static readonly StyledProperty<bool> ConstrainToPaperProperty = AvaloniaProperty.Register<EditableTable, bool>(nameof(ConstrainToPaper), true, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public static readonly StyledProperty<Size> CustomPaperSizeProperty = AvaloniaProperty.Register<EditableTable, Size>(nameof(CustomPaperSize), new Size(210, 297), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay, validate: size => ValidDimension(size.Width) && ValidDimension(size.Height));
    public static readonly DirectProperty<EditableTable, Size?> PaperSizeProperty = AvaloniaProperty.RegisterDirect<EditableTable, Size?>(nameof(PaperSize), x => x.PaperSize);
    public PaperKind PaperFormat { get => GetValue(PaperFormatProperty); set => SetValue(PaperFormatProperty, value); }
    public bool PaperLandscape { get => GetValue(PaperLandscapeProperty); set => SetValue(PaperLandscapeProperty, value); }
    /// <summary>Cap the screen viewport to PaperSize. False fills the host while retaining document paper settings. Defaults to true for existing previews.</summary>
    public bool ConstrainToPaper { get => GetValue(ConstrainToPaperProperty); set => SetValue(ConstrainToPaperProperty, value); }
    /// <summary>Unrotated custom dimensions in millimetres, each from 1 to 2000.</summary>
    public Size CustomPaperSize { get => GetValue(CustomPaperSizeProperty); set => SetValue(CustomPaperSizeProperty, value); }
    /// <summary>Effective page dimensions in millimetres, after orientation; null for Screen. Use for print/export page setup.</summary>
    public Size? PaperSize => paperSize;
    Size? paperSize;
    Control? tableEditorPane;
    readonly List<Action> paperEditorSync = new();
    static bool ValidDimension(double value) => double.IsFinite(value) && value >= 1 && value <= 2000;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ToolbarActionsProperty || change.Property == ToolbarPlacementProperty || change.Property == AddRowsPlacementProperty) { Rebuild(); return; }
        if (change.Property == PaperDimensionsPlacementProperty) { foreach (var sync in paperEditorSync) sync(); return; }
        if (change.Property == ConstrainToPaperProperty) { UpdatePaperViewport(); foreach (var sync in paperEditorSync) sync(); return; }
        if (change.Property != PaperFormatProperty && change.Property != PaperLandscapeProperty && change.Property != CustomPaperSizeProperty) return;
        Size? next = PaperFormat switch
        {
            PaperKind.Screen => null,
            PaperKind.A5 => new Size(148, 210),
            PaperKind.A4 => new Size(210, 297),
            PaperKind.A3 => new Size(297, 420),
            PaperKind.Letter => new Size(215.9, 279.4),
            PaperKind.Legal => new Size(215.9, 355.6),
            PaperKind.Custom => CustomPaperSize,
            _ => null
        };
        if (next is Size size && PaperLandscape) next = new Size(size.Height, size.Width);
        SetAndRaise(PaperSizeProperty, ref paperSize, next);
        UpdatePaperViewport();
        Rebuild();
    }

    void UpdatePaperViewport()
    {
        var width = ConstrainToPaper && PaperSize is Size page ? page.Width * 96 / 25.4 : double.NaN;
        area.MaxWidth = double.IsNaN(width) ? double.PositiveInfinity : width;
        area.Width = double.IsNaN(width) ? double.NaN : Math.Min(width, Bounds.Width > 0 ? Bounds.Width : width);
        area.HorizontalAlignment = double.IsNaN(width) ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
    }

    void BuildPaperEditor()
    {
        var fields = new WrapPanel { Orientation = Orientation.Horizontal };
        string Dimensions() => PaperSize is Size size ? T("{0} × {1} mm", Math.Round(size.Width, 2), Math.Round(size.Height, 2)) : "";
        var note = Words(Build.Text("", Text3, TextSize.Caption), text => text.Text = Dimensions());
        string FormatLabel() => PaperDimensionsPlacement == PaperDimensionsPosition.FormatLabel && PaperSize != null ? T("{0} · {1}", T("Paper format"), Dimensions()) : T("Paper format");
        void Choice(string label, string[] values, Func<int> read, Action<int> write, Func<bool>? visible = null, bool notify = true, Func<string>? caption = null)
        {
            var choice = new ChoiceButton();
            var control = RibbonField(label, choice, 104, caption); fields.Children.Add(control);
            void Sync()
            {
                var translated = values.Select(T).ToArray(); choice.SetItems(translated, translated[read()]); control.IsVisible = visible?.Invoke() ?? true;
                if (caption != null) ((TextBlock)control.Children[0]).Text = caption();
                SizeRibbonField(control, choice, 104);
            }
            Words(choice, _ => Sync()); paperEditorSync.Add(Sync);
            choice.Picked += value => { if (syncingColumnEditor || IsReadOnly || !Design) return; write(Array.IndexOf(values.Select(T).ToArray(), value)); if (notify) Changed?.Invoke(); };
        }
        Choice("Paper format", Enum.GetNames<PaperKind>(), () => (int)PaperFormat, i => SetCurrentValue(PaperFormatProperty, (PaperKind)i), caption: FormatLabel);
        Choice("Orientation", new[] { "Portrait", "Landscape" }, () => PaperLandscape ? 1 : 0, i => SetCurrentValue(PaperLandscapeProperty, i == 1), () => PaperFormat != PaperKind.Screen);
        Choice("Viewport", new[] { "Paper preview", "Full width" }, () => ConstrainToPaper ? 0 : 1, i => SetCurrentValue(ConstrainToPaperProperty, i == 0), () => PaperFormat != PaperKind.Screen, notify: false);
        foreach (var width in new[] { true, false })
        {
            var field = new Field(); var label = width ? "Width (mm)" : "Height (mm)";
            var control = RibbonField(label, field, 104); fields.Children.Add(control);
            string presented = ""; bool edited = false, presenting = false;
            bool Draft() => edited || field.Text != presented;
            string Read() => EditorNumber(width ? CustomPaperSize.Width : CustomPaperSize.Height);
            void Reset() { presenting = true; try { field.Error = null; field.Text = Read(); presented = field.Text; edited = false; } finally { presenting = false; } }
            field.Box.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty && !presenting && Design && !IsReadOnly) edited = true; };
            void Validate() => field.Error = TryEditorNumber(field.Text, out var value) && ValidDimension(value) ? null : T("Enter a dimension from 1 to 2000 mm");
            void Apply()
            {
                if (syncingColumnEditor || IsReadOnly || !Design || !Draft()) return;
                Validate(); if (field.Error != null || !TryEditorNumber(field.Text, out var value)) return;
                presented = field.Text; edited = false;
                SetCurrentValue(CustomPaperSizeProperty, width ? new Size(value, CustomPaperSize.Height) : new Size(CustomPaperSize.Width, value)); Changed?.Invoke();
            }
            ribbonFields.Add((field, 3, () => PaperFormat == PaperKind.Custom, () => { if (Draft()) Validate(); }, Apply, Reset));
            paperEditorSync.Add(() => { control.IsVisible = PaperFormat == PaperKind.Custom; if (PaperFormat != PaperKind.Custom || !field.IsKeyboardFocusWithin && field.Error == null && !Draft()) Reset(); });
            Words(field, f => { if (f.Error != null) f.Error = T("Enter a dimension from 1 to 2000 mm"); }, "validation");
            field.Edited += Apply;
            field.Submit += Apply;
            field.LostFocus += (_, _) => { if (IsReadOnly || !Design || field.IsKeyboardFocusWithin) return; Apply(); if (field.Error == null && !Draft()) Reset(); };
        }
        paperEditorSync.Add(() => { note.Text = Dimensions(); note.IsVisible = PaperSize != null && PaperDimensionsPlacement == PaperDimensionsPosition.Bottom; });
        note.HorizontalAlignment = HorizontalAlignment.Right; note.Margin = new Thickness(0, 0, M(16), M(12));
        var right = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, Children = { fields, note } };
        tableEditorPane = new RibbonFlow { AlignLast = true, Children = { columnPanes[3], right } };
    }
}

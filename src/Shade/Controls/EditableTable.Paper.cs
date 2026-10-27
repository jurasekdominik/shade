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
    /// <summary>The paper layout used by the preview and the application's print/export pipeline.</summary>
    public enum PaperKind { Screen, A5, A4, A3, Letter, Legal, Custom }
    public static readonly StyledProperty<PaperKind> PaperFormatProperty = AvaloniaProperty.Register<EditableTable, PaperKind>(nameof(PaperFormat), PaperKind.Screen, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay, validate: value => Enum.IsDefined(value));
    public static readonly StyledProperty<bool> PaperLandscapeProperty = AvaloniaProperty.Register<EditableTable, bool>(nameof(PaperLandscape), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public static readonly StyledProperty<Size> CustomPaperSizeProperty = AvaloniaProperty.Register<EditableTable, Size>(nameof(CustomPaperSize), new Size(210, 297), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay, validate: size => ValidDimension(size.Width) && ValidDimension(size.Height));
    public static readonly DirectProperty<EditableTable, Size?> PaperSizeProperty = AvaloniaProperty.RegisterDirect<EditableTable, Size?>(nameof(PaperSize), x => x.PaperSize);
    public PaperKind PaperFormat { get => GetValue(PaperFormatProperty); set => SetValue(PaperFormatProperty, value); }
    public bool PaperLandscape { get => GetValue(PaperLandscapeProperty); set => SetValue(PaperLandscapeProperty, value); }
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
        var width = PaperSize is Size page ? page.Width * 96 / 25.4 : double.NaN;
        area.MaxWidth = double.IsNaN(width) ? double.PositiveInfinity : width;
        area.Width = double.IsNaN(width) ? double.NaN : Math.Min(width, Bounds.Width > 0 ? Bounds.Width : width);
        area.HorizontalAlignment = double.IsNaN(width) ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
    }

    void BuildPaperEditor()
    {
        var fields = new WrapPanel { Orientation = Orientation.Horizontal, MaxWidth = M(480) };
        var note = Words(Build.Text("", Text3, TextSize.Caption), text => text.Text = PaperSize is Size size ? T("{0} × {1} mm", size.Width, size.Height) : "");
        void Choice(string label, string[] values, Func<int> read, Action<int> write, Func<bool>? visible = null)
        {
            var choice = new ChoiceButton();
            var control = FormLayout.Field(label, choice, M(104));
            Words((TextBlock)((StackPanel)control).Children[0], text => text.Text = T(label)); fields.Children.Add(control);
            void Sync()
            {
                var translated = values.Select(T).ToArray(); choice.SetItems(translated, translated[read()]); control.IsVisible = visible?.Invoke() ?? true;
            }
            Words(choice, _ => Sync()); paperEditorSync.Add(Sync);
            choice.Picked += value => { if (syncingColumnEditor || IsReadOnly || !Design) return; write(Array.IndexOf(values.Select(T).ToArray(), value)); Changed?.Invoke(); };
        }
        Choice("Paper format", Enum.GetNames<PaperKind>(), () => (int)PaperFormat, i => SetCurrentValue(PaperFormatProperty, (PaperKind)i));
        Choice("Orientation", new[] { "Portrait", "Landscape" }, () => PaperLandscape ? 1 : 0, i => SetCurrentValue(PaperLandscapeProperty, i == 1), () => PaperFormat != PaperKind.Screen);
        foreach (var width in new[] { true, false })
        {
            var field = new Field(); var label = width ? "Width (mm)" : "Height (mm)";
            var control = FormLayout.Field(label, field, M(104)); Words((TextBlock)((StackPanel)control).Children[0], text => text.Text = T(label)); fields.Children.Add(control);
            void Reset() { field.Error = null; field.Text = (width ? CustomPaperSize.Width : CustomPaperSize.Height).ToString(Culture); }
            void Validate() => field.Error = TryEditorNumber(field.Text, out var value) && ValidDimension(value) ? null : T("Enter a dimension from 1 to 2000 mm");
            void Apply()
            {
                if (syncingColumnEditor || IsReadOnly || !Design) return;
                Validate(); if (field.Error != null || !TryEditorNumber(field.Text, out var value)) return;
                SetCurrentValue(CustomPaperSizeProperty, width ? new Size(value, CustomPaperSize.Height) : new Size(CustomPaperSize.Width, value)); Changed?.Invoke();
            }
            ribbonFields.Add((field, 3, () => PaperFormat == PaperKind.Custom, Validate,
                () => { if (field.Text != (width ? CustomPaperSize.Width : CustomPaperSize.Height).ToString(Culture)) Apply(); }, Reset));
            paperEditorSync.Add(() => { control.IsVisible = PaperFormat == PaperKind.Custom; if (PaperFormat != PaperKind.Custom || !field.IsKeyboardFocusWithin && field.Error == null) Reset(); });
            Words(field, f => { if (f.Error != null) f.Error = T("Enter a dimension from 1 to 2000 mm"); }, "validation");
            field.Edited += Apply;
        }
        paperEditorSync.Add(() => { note.Text = PaperSize is Size size ? T("{0} × {1} mm", size.Width, size.Height) : ""; note.IsVisible = PaperSize != null; });
        note.HorizontalAlignment = HorizontalAlignment.Right; note.Margin = new Thickness(0, 0, M(16), M(12));
        var right = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, Children = { fields, note } };
        columnRibbon.Children.Remove(columnPanes[3]);
        tableEditorPane = new Masonry { MaxColumns = 2, MinColumn = M(450), Gap = M(16), RowGap = M(8), Children = { columnPanes[3], right } };
        columnRibbon.Children.Add(tableEditorPane);
    }
}

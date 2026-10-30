using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.VisualTree;
using static Shade.Look;
using static Shade.PaperFormDefinition;

namespace Shade;

/// <summary>A custom palette entry. Factories own their value bindings; export callbacks must produce equivalent HTML.</summary>
public sealed record PaperStructureTemplate(string Kind, string Label, Func<PaperStructure, PaperFormValue?, bool, Control> Create, Func<PaperStructure, PaperFormValue?, string> ExportHtml);

/// <summary>A physical page composed from one layout and separate native input values.</summary>
public partial class PaperFormView : UserControl
{
    public static readonly StyledProperty<PaperFormDefinition> DefinitionProperty = AvaloniaProperty.Register<PaperFormView, PaperFormDefinition>(nameof(Definition), new PaperFormDefinition(), defaultBindingMode: BindingMode.TwoWay, validate: ValidDefinition);
    public static readonly StyledProperty<IList<PaperFormValue>> ValuesProperty = AvaloniaProperty.Register<PaperFormView, IList<PaperFormValue>>(nameof(Values), Array.Empty<PaperFormValue>(), validate: values => values != null && values.All(v => v != null) && values.Select(v => v.Key).Distinct(StringComparer.Ordinal).Count() == values.Count);
    public static readonly StyledProperty<PaperFormMode> ModeProperty = AvaloniaProperty.Register<PaperFormView, PaperFormMode>(nameof(Mode), PaperFormMode.Fill, defaultBindingMode: BindingMode.TwoWay, validate: Enum.IsDefined);
    public static readonly StyledProperty<double> ZoomProperty = AvaloniaProperty.Register<PaperFormView, double>(nameof(Zoom), 1, defaultBindingMode: BindingMode.TwoWay, validate: n => double.IsFinite(n) && n >= .01 && n <= 4);
    public static readonly StyledProperty<int> PageIndexProperty = AvaloniaProperty.Register<PaperFormView, int>(nameof(PageIndex), defaultBindingMode: BindingMode.TwoWay, validate: n => n >= 0);
    public static readonly StyledProperty<IReadOnlyList<PaperStructureTemplate>?> StructureTypesProperty = AvaloniaProperty.Register<PaperFormView, IReadOnlyList<PaperStructureTemplate>?>(nameof(StructureTypes));
    public static readonly DirectProperty<PaperFormView, IReadOnlyList<PaperFormProblem>> ProblemsProperty = AvaloniaProperty.RegisterDirect<PaperFormView, IReadOnlyList<PaperFormProblem>>(nameof(Problems), v => v.Problems);
    public PaperFormDefinition Definition { get => GetValue(DefinitionProperty); set => SetValue(DefinitionProperty, value); }
    public IList<PaperFormValue> Values { get => GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }
    public PaperFormMode Mode { get => GetValue(ModeProperty); set => SetValue(ModeProperty, value); }
    public double Zoom { get => GetValue(ZoomProperty); set => SetValue(ZoomProperty, value); }
    public int PageIndex { get => GetValue(PageIndexProperty); set => SetValue(PageIndexProperty, value); }
    public IReadOnlyList<PaperStructureTemplate>? StructureTypes { get => GetValue(StructureTypesProperty); set => SetValue(StructureTypesProperty, value); }
    public IReadOnlyList<PaperFormProblem> Problems => problems;
    IReadOnlyList<PaperFormProblem> problems = Array.Empty<PaperFormProblem>();
    internal readonly List<Canvas> Pages = new();
    internal Canvas Page => Pages[Math.Min(PageIndex, Pages.Count - 1)];
    internal bool ShowAllPages;
    double pageChromeInset;
    internal void UpdatePageInsets(double inset)
    {
        pageChromeInset = inset;
        int columns = ShowAllPages && SideBySidePages ? 2 : 1, i = 0;
        foreach (var page in pageStack.Children.OfType<Canvas>().Where(Pages.Contains))
        {
            page.Margin = new Thickness(inset + (i % columns > 0 ? M(24) : 0), inset + (i / columns > 0 ? M(24) : 0), 0, 0); i++;
        }
    }
    readonly Grid pageStack = new();
    public static readonly StyledProperty<bool> SideBySidePagesProperty = AvaloniaProperty.Register<PaperFormView, bool>(nameof(SideBySidePages), defaultBindingMode: BindingMode.TwoWay);
    public bool SideBySidePages { get => GetValue(SideBySidePagesProperty); set => SetValue(SideBySidePagesProperty, value); }
    readonly Dictionary<Canvas, TextBlock> pageNumbers = new();
    readonly LayoutTransformControl scaled;
    readonly Dictionary<string, (PaperStructure structure, PaperFormValue? value, Control control)> editors = new();
    readonly Dictionary<string, TextBlock> titles = new();
    /// <summary>Inputs whose Title prints just above their bounds; a check box, titled box, label and group show it inside.</summary>
    internal static bool TitleAbove(PaperStructure s) => s.Title.Length > 0 && s.Kind is PaperStructureKind.Characters or PaperStructureKind.Text or PaperStructureKind.Multiline or PaperStructureKind.Date;
    readonly HashSet<PaperFormValue> watched = new();
    INotifyCollectionChanged? valuesCollection;
    IList<PaperFormValue>? watchedSource;
    bool attached, refreshing, pendingProblems;
    internal bool SkipProblemCheck;
    internal Action? PageUpdated;
    /// <summary>Values changed: the keys that changed, or null when the whole list did.</summary>
    internal Action<IReadOnlyCollection<string>?>? ValuesEdited;

    public PaperFormView()
    {
        Pages.Add(new PaperPageCanvas(this) { ClipToBounds = false });
        editorHost = new PaperEditorHost(this);
        SetCurrentValue(ValuesProperty, new AvaloniaList<PaperFormValue>());
        pageStack.Children.Add(Page);
        scaled = new LayoutTransformControl { Child = pageStack, LayoutTransform = new ScaleTransform(1, 1), RenderTransformOrigin = RelativePoint.TopLeft };
        zoomHost = new TransientScaleHost(scaled) { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };
        Content = zoomHost;
        LayoutUpdated += (_, _) => PrepareDrawings();
        Words(this, v => { v.painter?.Reset(); v.InvalidateDrawings(); v.RefreshProblems(); }, "validation");
        Refresh();
    }
    static bool ValidDefinition(PaperFormDefinition d) { if (d == null) return false; d.ValidateForAssignment(); return true; }
    static FontFamily? documentFont;
    internal static FontFamily DocumentFont => documentFont ??= FontManager.Current.SystemFonts.FirstOrDefault(f => f.Name.Equals("Segoe UI", StringComparison.OrdinalIgnoreCase))
        ?? FontManager.Current.SystemFonts.FirstOrDefault() ?? FontManager.Current.DefaultFontFamily;
    internal static IBrush Ink(string? color, Tone fallback) => color == null ? B(fallback) : new SolidColorBrush(Color.Parse(color));
    internal static readonly (string kind, string label)[] Palette = { (PaperStructureKind.Characters, "Character field"), (PaperStructureKind.Text, "Text field"), (PaperStructureKind.Multiline, "Multiline field"),
        (PaperStructureKind.Caption, "Titled box"), (PaperStructureKind.Check, "Checkbox"), (PaperStructureKind.Date, "Date"), (PaperStructureKind.Line, "Line"), (PaperStructureKind.Label, "Static text"), (PaperStructureKind.Group, "Group"), (PaperStructureKind.Image, "Image") };
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<string, IImage> pictures = new();
    /// <summary>The decoded picture of an image structure (a bitmap, or an SVG drawn as vectors), shared by every editor showing the same data.</summary>
    internal static IImage? Picture(PaperStructure s)
    {
        if (s.Image == null) return null;
        if (pictures.TryGetValue(s.Image, out var known)) return known;
        if (ImagePixels(s.Image) == null || ImageBytes(s.Image) is not { } bytes) return null;  // ImagePixels also refuses unsafe SVG
        try
        {
            IImage image = IsSvg(bytes) ? new Avalonia.Svg.Skia.SvgImage { Source = Avalonia.Svg.Skia.SvgSource.LoadFromStream(new System.IO.MemoryStream(bytes)) } : new Avalonia.Media.Imaging.Bitmap(new System.IO.MemoryStream(bytes));
            pictures.AddOrUpdate(s.Image, image); return image;
        }
        catch (Exception) { return null; }  // not decodable: shown as a missing picture
    }
    /// <summary>How problems and layers name a structure: its label, caption or key, else its translated kind.</summary>
    internal string DisplayName(PaperStructure s) => s.Label.Length > 0 ? s.Label : s.Title.Length > 0 ? s.Title : s.FieldKey.Length > 0 ? s.FieldKey
        : T(Palette.FirstOrDefault(p => p.kind == s.Kind).label ?? FindTemplate(s.Kind)?.Label ?? s.Kind);
    // values by key, rebuilt when the list or its length changes (an observable list also clears it on any change)
    Dictionary<string, PaperFormValue>? byKey; IList<PaperFormValue>? byKeySource; int byKeyCount;
    internal PaperFormValue? Value(PaperStructure s) => ValueFor(s.FieldKey);
    internal PaperFormValue? ValueFor(string key)
    {
        if (key.Length == 0) return null;
        if (byKey == null || !ReferenceEquals(byKeySource, Values) || byKeyCount != Values.Count) { byKey = Values.ToDictionary(v => v.Key, StringComparer.Ordinal); byKeySource = Values; byKeyCount = Values.Count; }
        return byKey.GetValueOrDefault(key);
    }
    internal IEnumerable<PaperStructure> Structures => Definition.Pages[Math.Min(PageIndex, Definition.Pages.Count - 1)].Structures;
    internal PaperStructureTemplate? FindTemplate(string kind) => StructureTypes?.FirstOrDefault(t => t.Kind == kind);
    internal static bool BuiltIn(string kind) => PaperStructureKind.IsBuiltIn(kind);
    internal static IEnumerable<PaperStructure> All(IEnumerable<PaperStructure> structures)
    {
        foreach (var structure in structures)
        {
            yield return structure;
            if (structure.Children.Count > 0) foreach (var child in All(structure.Children)) yield return child;
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValuesProperty) { WatchValues(); ValuesEdited?.Invoke(null); }
        if (scaled == null) return;
        if (change.Property == ValuesProperty) InvalidateDrawings();
        if (activeField != null && (change.Property == ModeProperty || change.Property == StructureTypesProperty || change.Property == ValuesProperty || change.Property == IsEnabledProperty || change.Property == IsVisibleProperty))
        {
            changingEditor = true; try { DeactivateEditor(); } finally { changingEditor = false; }
        }
        if (change.Property == IsEffectivelyEnabledProperty || change.Property == FlowDirectionProperty || change.Property == UseLayoutRoundingProperty) InvalidateDrawings();
        if (change.Property == DefinitionProperty && PageIndex >= Definition.Pages.Count) SetCurrentValue(PageIndexProperty, Definition.Pages.Count - 1);
        if (change.Property == DefinitionProperty || change.Property == ValuesProperty || change.Property == ModeProperty || change.Property == StructureTypesProperty || change.Property == PageIndexProperty || change.Property == SideBySidePagesProperty)
            Refresh(change.Property == ModeProperty || change.Property == StructureTypesProperty);
        if (change.Property == ZoomProperty) { if (DeferZoomLayout) zoomHost.SetRatio(Zoom / layoutZoom); else CommitZoomLayout(); }
    }
    void WatchValues(bool force = false)
    {
        if (!force && attached && ReferenceEquals(watchedSource, Values) && watched.Count == Values.Count) return;
        if (valuesCollection != null) valuesCollection.CollectionChanged -= ValuesChanged;
        foreach (var value in watched) value.PropertyChanged -= ValueChanged;
        watched.Clear(); valuesCollection = null;
        watchedSource = null;
        if (!attached) return;
        if (Values == null || Values.Any(v => v == null) || Values.Select(v => v.Key).Distinct(StringComparer.Ordinal).Count() != Values.Count) throw new ArgumentException("Values must have unique, non-null keys.");
        valuesCollection = Values as INotifyCollectionChanged;
        if (valuesCollection != null) valuesCollection.CollectionChanged += ValuesChanged;
        foreach (var value in Values) if (watched.Add(value)) value.PropertyChanged += ValueChanged;
        watchedSource = Values;
    }
    void ValuesChanged(object? sender, NotifyCollectionChangedEventArgs e) { byKey = null; if (refreshing) return; WatchValues(true); InvalidateDrawings(); Refresh(); ValuesEdited?.Invoke(null); }
    void ValueChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is PaperFormValue value) changedKeys.Add(value.Key);
        if (pendingProblems) return;
        pendingProblems = true;
        Dispatcher.UIThread.Post(() => { pendingProblems = false; var keys = changedKeys.ToArray(); changedKeys.Clear(); if (attached) { PrepareChangedDrawings(keys); RefreshProblems(keys); NotifyValuePeers(keys); ValuesEdited?.Invoke(keys); } });
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) { base.OnAttachedToVisualTree(e); attached = true; WatchValues(); InvalidateDrawings(); Refresh(); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) { attached = false; changingEditor = true; try { DeactivateEditor(); } finally { changingEditor = false; } WatchValues(); base.OnDetachedFromVisualTree(e); }

    internal void Refresh(bool recreate = false)
    {
        if (refreshing || scaled == null || Values == null) return;
        refreshing = true;
        try
        {
            void Remove(Control c) { if (c.Parent is Canvas parent) parent.Children.Remove(c); }
            if (recreate) { foreach (var editor in editors.Values) Remove(editor.control); editors.Clear(); }
            var size = Definition.PageSize;
            while (Pages.Count < Definition.Pages.Count) Pages.Add(new PaperPageCanvas(this) { ClipToBounds = false });
            while (Pages.Count > Definition.Pages.Count) Pages.RemoveAt(Pages.Count - 1);
            var visible = ShowAllPages ? Pages.ToArray() : new[] { Page };
            if (!pageStack.Children.Where(child => !ReferenceEquals(child, painter?.Templates) && !ReferenceEquals(child, automationAnchor)).SequenceEqual(visible))
            {
                pageStack.Children.Clear(); pageStack.Children.AddRange(visible);
                if (automationAnchor != null) pageStack.Children.Add(automationAnchor);
            }
            int columns = ShowAllPages && SideBySidePages ? 2 : 1;
            if (pageStack.ColumnDefinitions.Count != columns) pageStack.ColumnDefinitions = new ColumnDefinitions(columns == 2 ? "Auto,Auto" : "Auto");
            int rows = (visible.Length + columns - 1) / columns;
            if (pageStack.RowDefinitions.Count != rows) pageStack.RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("Auto", rows)));
            for (int i = 0; i < visible.Length; i++)
            {
                Grid.SetColumn(visible[i], i % columns); Grid.SetRow(visible[i], i / columns);

            }
            UpdatePageInsets(pageChromeInset);
            foreach (var stale in pageNumbers.Keys.Where(p => !Pages.Contains(p)).ToArray()) pageNumbers.Remove(stale);
            for (int i = 0; i < Pages.Count; i++)
            {
                var page = Pages[i];
                if (DrawPages) { if (pageNumbers.Remove(page, out var oldNumber)) page.Children.Remove(oldNumber); continue; }
                if (!pageNumbers.TryGetValue(page, out var number)) { number = new TextBlock { IsHitTestVisible = false, ZIndex = int.MaxValue - 5, TextAlignment = TextAlignment.Center }; pageNumbers[page] = number; page.Children.Add(number); }
                number.IsVisible = Definition.ShowPageNumbers; number.Text = Definition.PageNumberText(i + 1, Pages.Count); number.TextAlignment = Definition.PageNumberAlignment; number.FontFamily = DocumentFont; number.FontSize = TextSize.Caption; number.Foreground = Ink(Definition.Foreground, PaperInk);
                // inside the margin band of its edge, between the side margins
                double band = Definition.PageNumbersOnTop ? Definition.MarginTop : Definition.MarginBottom;
                number.Width = Math.Max(1, (size.Width - Definition.MarginLeft - Definition.MarginRight) * DipsPerMm); number.Height = Math.Max(1, band * DipsPerMm);
                number.Padding = new Thickness(0, Math.Max(0, (band * DipsPerMm - TextSize.Caption * 1.4) / 2), 0, 0);
                Canvas.SetLeft(number, Definition.MarginLeft * DipsPerMm); Canvas.SetTop(number, Definition.PageNumbersOnTop ? 0 : (size.Height - band) * DipsPerMm);
            }
            foreach (var page in Pages) { page.Width = size.Width * DipsPerMm; page.Height = size.Height * DipsPerMm; page.Background = Ink(Definition.Background, Paper); }
            if (recreate) { painter?.Clear(); preparedPages.Clear(); }
            RefreshDrawings();
            if (!DeferZoomLayout) CommitZoomLayout();
            var keys = Values.Select(v => v.Key).ToHashSet(StringComparer.Ordinal);
            var missing = All(ShowAllPages ? Definition.Pages.SelectMany(p => p.Structures) : Structures)
                .Where(s => s.FieldKey.Length > 0 && keys.Add(s.FieldKey)).Select(s => new PaperFormValue(s.FieldKey)).ToArray();
            if (missing.Length > 0)
            {
                if (Values.IsReadOnly) throw new ArgumentException("Values must be writable or contain every field key.");
                if (Values is AvaloniaList<PaperFormValue> list) list.AddRange(missing);
                else foreach (var value in missing) Values.Add(value);
                byKey = null;
            }
            PlaceControls();
            WatchValues(); RefreshProblems(null); PrepareDrawings(); PageUpdated?.Invoke(); RefreshAutomation();
        }
        finally { refreshing = false; }
    }

    static void Detach(Control c) { if (c.Parent is Canvas parent) parent.Children.Remove(c); }
    /// <summary>Put the live controls on their pages: custom templates always, built-in fields only in the native comparison renderer, and the one pooled editor over the active field.</summary>
    void PlaceControls()
    {
        void Remove(Control c) => Detach(c);
        var present = new HashSet<string>(); int tab = 0;
        void Place(Canvas page, IEnumerable<PaperStructure> structures, double x, double y)
        {
            foreach (var s in structures)
            {
                if (DrawPages && DrawStructure(s))
                {
                    tab++;
                    if (s.Kind == PaperStructureKind.Group) Place(page, s.Children, x + s.Bounds.X, y + s.Bounds.Y);
                    continue;
                }
                var value = Value(s); present.Add(s.Id);
                bool pooledField = DrawPages && s.Id == activeField;
                if (!editors.TryGetValue(s.Id, out var existing) || pooledField || existing.value != value || !ReferenceEquals(existing.structure, s) && existing.structure with { Bounds = s.Bounds, Children = s.Children, CellCount = s.CellCount, CellWidth = s.CellWidth } != s)
                {
                    var control = DrawPages && s.Id == activeField ? PooledEditor(s, value) : Create(s, value);
                    if (existing.control != null && existing.control != control) Remove(existing.control);
                    if (control.Parent != page) page.Children.Add(control);
                    existing = (s, value, control); editors[s.Id] = existing;
                }
                var c = existing.control;
                if (c.Parent != page) { Remove(c); page.Children.Add(c); }
                Canvas.SetLeft(c, (x + s.Bounds.X) * DipsPerMm); Canvas.SetTop(c, (y + s.Bounds.Y) * DipsPerMm);
                c.Width = s.Bounds.Width * DipsPerMm; c.Height = s.Bounds.Height * DipsPerMm;
                // the pooled editor has the exact size a field of its own had: input limits measure the control, not its rounded layout box
                if (c == editorHost && editorHost.Child is { } sized) { sized.Width = c.Width; sized.Height = c.Height; }
                var input = c == editorHost ? editorHost.Child! : c;
                if ((input is CharacterGridField direct ? direct : (input as Border)?.Child as CharacterGridField) is { } grid && s.Kind == "characters") { grid.SetCurrentValue(CharacterGridField.CellCountProperty, s.CellCount); grid.CellSize = new Size(s.CellWidth * DipsPerMm, Math.Max(1, s.Bounds.Height * DipsPerMm)); if (input != grid) { grid.Width = s.GridAlignment == TextAlignment.Left ? double.NaN : s.CellCount * s.CellWidth * DipsPerMm; grid.HorizontalAlignment = s.GridAlignment == TextAlignment.Left ? HorizontalAlignment.Stretch : s.GridAlignment == TextAlignment.Right ? HorizontalAlignment.Right : HorizontalAlignment.Center; grid.Height = s.Bounds.Height * DipsPerMm; } }
                if (input is CheckBox check) CheckBoxAppearance.SetIndicatorSize(check, Math.Max(1, Math.Min(s.Bounds.Width, s.Bounds.Height) * DipsPerMm));
                editors[s.Id] = (s, value, c);
                c.IsHitTestVisible = Mode != PaperFormMode.Design;
                c.ZIndex = tab; Avalonia.Input.KeyboardNavigation.SetTabIndex(c, tab++);
                if (TitleAbove(s))
                {
                    if (!titles.TryGetValue(s.Id, out var title)) { title = new TextBlock { IsHitTestVisible = false }; titles[s.Id] = title; }
                    if (title.Parent != page) { Remove(title); page.Children.Add(title); }
                    title.Text = s.Title; title.FontFamily = s.FontFamily == null ? DocumentFont : new FontFamily(s.FontFamily); title.FontSize = s.FontSize; title.FontWeight = s.FontWeight; title.FontStyle = s.FontStyle; title.ZIndex = c.ZIndex;
                    title.Foreground = Ink(s.Foreground ?? Definition.Foreground, PaperInk);
                    Canvas.SetLeft(title, (x + s.Bounds.X) * DipsPerMm + GridInset(s)); Canvas.SetBottom(title, page.Height - (y + s.Bounds.Y) * DipsPerMm);
                }
                else if (titles.Remove(s.Id, out var gone)) Remove(gone);
                if (s.Kind == "group") Place(page, s.Children, x + s.Bounds.X, y + s.Bounds.Y);
            }
        }
        if (ShowAllPages) { for (int i = 0; i < Pages.Count; i++) Place(Pages[i], Definition.Pages[i].Structures, 0, 0); }
        else Place(Page, Structures, 0, 0);
        foreach (var key in editors.Keys.Where(key => !present.Contains(key)).ToArray()) { Remove(editors[key].control); editors.Remove(key); }
        foreach (var key in titles.Keys.Where(key => !present.Contains(key)).ToArray()) { Remove(titles[key]); titles.Remove(key); }
    }

    /// <summary>A field with a bottom rule or no border draws its rule in a sibling, so it is a different editor from a fully framed one.</summary>
    internal static bool Ruled(PaperStructure s) => s.Kind is "text" or "multiline" or "date" or "caption" && s.BorderStyle != PaperBorderStyle.Full;
    internal static string EditorKey(PaperStructure s) => Ruled(s) ? s.Kind + "+rule" : s.Kind;
    internal static PaperVerticalAlignment EffectiveVertical(PaperStructure s) => s.VerticalAlignment ?? (s.Kind is "multiline" or "label" or "group" ? PaperVerticalAlignment.Top : PaperVerticalAlignment.Center);
    static VerticalAlignment Vertical(PaperStructure s) => EffectiveVertical(s) switch { PaperVerticalAlignment.Center => VerticalAlignment.Center, PaperVerticalAlignment.Bottom => VerticalAlignment.Bottom, _ => VerticalAlignment.Top };
    /// <summary>The tick's scale against its normal size (the marks are 0.48 and 0.4 of the box's side).</summary>
    internal static double MarkScale(PaperStructure s) => s.CheckMarkSize is { } size ? size / (s.CheckMark == PaperCheckMark.Check ? .48 : .4) : 1;
    /// <summary>Where an aligned character grid starts inside its bounds, in DIPs; its title sits above the grid, not above the empty space.</summary>
    internal static double GridInset(PaperStructure s) => s.Kind == PaperStructureKind.Characters && s.GridAlignment != TextAlignment.Left
        ? Math.Max(0, s.Bounds.Width - s.CellCount * s.CellWidth) * (s.GridAlignment == TextAlignment.Right ? 1 : .5) * DipsPerMm : 0;
    internal Control Create(PaperStructure s, PaperFormValue? value)
    {
        bool readOnly = Mode == PaperFormMode.Preview || s.IsReadOnly;
        Control result;
        IBrush ink = Ink(s.Foreground ?? Definition.Foreground, PaperInk), fill = Ink(s.Background ?? Definition.Background, Paper), line = Ink(s.BorderColor, PaperRule);
        var font = s.FontFamily == null ? DocumentFont : new FontFamily(s.FontFamily);
        if (FindTemplate(s.Kind) is { } custom) result = custom.Create(s, value, readOnly);
        else if (s.Kind is "label" or "group")
        {
            result = new Border { Background = s.Background == null ? Brushes.Transparent : fill, BorderBrush = line, BorderThickness = new Thickness(s.BorderWidth * DipsPerMm), Child = new TextBlock { Text = s.Title, Foreground = ink, FontFamily = font, FontSize = s.FontSize, FontWeight = s.FontWeight, FontStyle = s.FontStyle, TextAlignment = s.Alignment, TextWrapping = TextWrapping.Wrap, VerticalAlignment = Vertical(s) } };
        }
        else if (s.Kind == PaperStructureKind.Image)
        {
            // the picture itself carries the name: a screen reader skips the frame around it
            var shown = new Avalonia.Controls.Image { Source = Picture(s), Stretch = s.KeepAspectRatio ? Stretch.Uniform : Stretch.Fill };
            AutomationProperties.SetName(shown, s.Label.Length > 0 ? s.Label : s.Title.Length > 0 ? s.Title : T("Image"));
            result = shown.Source != null
                ? new Border { Background = Brushes.Transparent, BorderBrush = line, BorderThickness = new Thickness(s.BorderWidth * DipsPerMm), Child = shown }
                : new Border { Background = B(Fill), BorderBrush = line, BorderThickness = new Thickness(Math.Max(.2, s.BorderWidth) * DipsPerMm), Child = new TextBlock { Text = T("No image"), Foreground = B(Text3), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        }
        else if (s.Kind == "line")
        {
            bool vertical = s.LineOrientation == Orientation.Vertical;
            var stroke = new Border { Background = line, Width = vertical ? s.BorderWidth * DipsPerMm : double.NaN, Height = vertical ? double.NaN : s.BorderWidth * DipsPerMm, HorizontalAlignment = vertical ? HorizontalAlignment.Center : HorizontalAlignment.Stretch, VerticalAlignment = vertical ? VerticalAlignment.Stretch : VerticalAlignment.Center };
            result = new Border { Background = Brushes.Transparent, Child = stroke };
        }
        else if (s.Kind == "check")
        {
            var box = new PaperCheckBox(); StyleCheckBox(box, s);
            if (value != null) box.Bind(CheckBox.IsCheckedProperty, new Binding(nameof(PaperFormValue.Checked)) { Source = value, Mode = BindingMode.TwoWay });
            result = box;
        }
        else if (BuiltIn(s.Kind))
        {
            TextBox box = s.Kind == "characters" ? new CharacterGridField
            {
                InsetFrame = true,
                Appearance = CharacterGridAppearance.Paper,
                CellCount = s.CellCount,
                CellSize = new Size(s.CellWidth * DipsPerMm, Math.Max(1, s.Bounds.Height * DipsPerMm)),
                CellBorderBrush = line,
                CellBorderThickness = s.BorderWidth * DipsPerMm,
                CharacterAlignment = s.Alignment
            } : new PaperTextBox { ConstrainInput = s.Kind == "multiline" && !s.AllowMultilineScrolling };
            // a field's printed rule (bottom line, end ticks) is drawn by a sibling, so one field type serves every border style
            Panel? ruleHost = Ruled(s) ? new Panel { Children = { box, new PaperRulePanel() } } : null;
            StyleTextBox(box, s);
            if (value != null) box.Bind(TextBox.TextProperty, new Binding(nameof(PaperFormValue.Text)) { Source = value, Mode = BindingMode.TwoWay });
            AutomationProperties.SetName(box, s.Label.Length > 0 ? s.Label : s.Title.Length > 0 ? s.Title : s.FieldKey);
            result = ruleHost != null ? ruleHost : box;
            if (s.Kind == "characters" && s.GridAlignment != TextAlignment.Left)
            {
                box.Width = s.CellCount * s.CellWidth * DipsPerMm; box.HorizontalAlignment = s.GridAlignment == TextAlignment.Right ? HorizontalAlignment.Right : HorizontalAlignment.Center;
                result = new Border { Background = Brushes.Transparent, Child = box };
            }
            if (s.Kind == "caption")
            {
                var panel = new DockPanel();
                StyleCaption(panel, s);
                panel.Children.Add(result); result = panel;
            }
        }
        else result = new TextBlock { Text = T("Unknown structure: {0}", s.Kind), Foreground = B(Danger), TextWrapping = TextWrapping.Wrap };
        AutomationProperties.SetName(result, s.Label.Length > 0 ? s.Label : s.FieldKey.Length > 0 ? s.FieldKey : s.Title.Length > 0 ? s.Title : s.Kind == "line" ? T("Line") : "");
        return result;
    }
    void StyleCheckBox(PaperCheckBox box, PaperStructure s)
    {
        var ink = Ink(s.Foreground ?? Definition.Foreground, PaperInk);
        box.IsReadOnly = Mode == PaperFormMode.Preview || s.IsReadOnly; box.Content = s.Title.Length == 0 ? null : s.Title;
        box.Background = Ink(s.Background ?? Definition.Background, Paper); box.Foreground = ink; box.BorderBrush = Ink(s.BorderColor, PaperRule);
        box.BorderThickness = new Thickness(s.BorderWidth * DipsPerMm); box.CornerRadius = new CornerRadius(0); box.FontSize = s.FontSize; box.FontFamily = s.FontFamily == null ? DocumentFont : new FontFamily(s.FontFamily); box.FontWeight = s.FontWeight; box.FontStyle = s.FontStyle;
        CheckBoxAppearance.SetIndicatorSize(box, Math.Max(1, Math.Min(s.Bounds.Width, s.Bounds.Height) * DipsPerMm)); CheckBoxAppearance.SetMarkBrush(box, ink); CheckBoxAppearance.SetMarkScale(box, MarkScale(s));
        box.Classes.Remove("square"); box.Classes.Remove("cross");
        if (s.CheckMark != PaperCheckMark.Check) box.Classes.Add(s.CheckMark == PaperCheckMark.Square ? "square" : "cross");
    }
    void StyleCaption(DockPanel panel, PaperStructure s)
    {
        foreach (var label in panel.Children.OfType<TextBlock>().ToArray()) panel.Children.Remove(label);
        int i = 0;
        foreach (var (text, dock) in new[] { (s.Title, Dock.Top), (s.Subtitle, Dock.Bottom) })
            if (text.Length > 0)
            {
                var label = new TextBlock { Text = text, FontSize = s.FontSize, FontFamily = s.FontFamily == null ? DocumentFont : new FontFamily(s.FontFamily), FontWeight = s.FontWeight, FontStyle = s.FontStyle, Foreground = Ink(s.Foreground ?? Definition.Foreground, PaperInk), TextAlignment = s.Alignment };
                DockPanel.SetDock(label, dock); panel.Children.Insert(i++, label);
            }
    }
    sealed class ActiveFillState { public IBrush? Active, Fill; }
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<TextBox, ActiveFillState> activeFills = new();
    /// <summary>The focused editable field takes the document's ActiveFieldBackground; blur restores its own fill. Restyling a pooled editor reuses the one pair of handlers.</summary>
    void StyleActiveFill(TextBox box, IBrush fill, bool readOnly)
    {
        var state = activeFills.GetValue(box, b =>
        {
            var created = new ActiveFillState();
            b.GotFocus += (_, _) => { if (created.Active != null) b.Background = created.Active; };
            b.LostFocus += (_, _) => b.Background = created.Fill;
            return created;
        });
        state.Fill = fill; state.Active = Definition.ActiveFieldBackground is { } active && !readOnly ? Ink(active, Paper) : null;
        if (box.IsFocused && state.Active != null) box.Background = state.Active;
    }
    void StyleTextBox(TextBox box, PaperStructure s, PaperStructure? previous = null)
    {
        var ink = Ink(s.Foreground ?? Definition.Foreground, PaperInk); var line = Ink(s.BorderColor, PaperRule);
        bool readOnly = Mode == PaperFormMode.Preview || s.IsReadOnly;
        box.MinHeight = 0; box.Padding = new Thickness(s.Kind == "characters" ? 0 : s.TextPadding * DipsPerMm); box.CornerRadius = new CornerRadius(0); box.Background = Ink(s.Background ?? Definition.Background, Paper); box.Foreground = ink;
        bool ruled = Ruled(s);
        box.BorderBrush = ruled ? Brushes.Transparent : line;  // the thickness stays, so bounds, padding and text origin are the same in every style
        box.BorderThickness = new Thickness(s.BorderWidth * DipsPerMm); box.FontSize = s.FontSize; box.FontFamily = s.FontFamily == null ? DocumentFont : new FontFamily(s.FontFamily); box.FontWeight = s.FontWeight; box.FontStyle = s.FontStyle;
        if (s.Kind != "characters") box.VerticalContentAlignment = Vertical(s);
        if (box.Parent is Panel host && host.Children.OfType<PaperRulePanel>().FirstOrDefault() is { } rule) rule.Set(s, line);
        StyleActiveFill(box, box.Background!, readOnly);
        box.IsReadOnly = readOnly; box.CaretBrush = ink; box.SelectionForegroundBrush = ink; box.TextAlignment = s.Kind == PaperStructureKind.Characters ? s.GridAlignment : s.Alignment;
        if (s.Kind == "multiline")
        {
            ScrollViewer.SetVerticalScrollBarVisibility(box, s.AllowMultilineScrolling && Mode != PaperFormMode.Preview ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled);
            ScrollViewer.SetHorizontalScrollBarVisibility(box, ScrollBarVisibility.Disabled);
            if (box is PaperTextBox paper) paper.ConstrainInput = !s.AllowMultilineScrolling;
        }
        box.AcceptsReturn = s.Kind == "multiline"; box.TextWrapping = s.Kind == "multiline" ? TextWrapping.Wrap : TextWrapping.NoWrap;
        if (box is CharacterGridField grid)
        {
            grid.CharacterAlignment = s.Alignment; grid.CellBorderBrush = line; grid.CellBorderThickness = s.BorderWidth * DipsPerMm;
            if (previous == null || !previous.CellRanges.SequenceEqual(s.CellRanges))
            {
                grid.CellDecorations!.Clear();
                foreach (var range in s.CellRanges) grid.CellDecorations.Add(new CharacterCellDecoration { Start = range.Start, Length = range.Length, Background = range.Background == null ? null : Ink(range.Background, Paper), Foreground = range.Foreground == null ? null : Ink(range.Foreground, PaperInk) });
            }
        }
        if (s.Kind == "date")
        {
            box.PlaceholderText = s.DateFormat;
            if (previous == null || previous.DateFormat != s.DateFormat || previous.IsReadOnly != s.IsReadOnly) box.InnerRightContent = readOnly ? null : DateButton(box, s.DateFormat, ink);
        }
    }

    /// <summary>A calendar button inside a date field (never in Preview, so it never prints): a picked day is written in the field's format.</summary>
    static Control DateButton(TextBox box, string format, IBrush ink)
    {
        var button = new IconButton { Icon = Icons.Calendar, IconSize = M(14), Width = M(20), Height = M(20), MinHeight = 0, Padding = new Thickness(0), Focusable = false, VerticalAlignment = VerticalAlignment.Center };
        button.Classes.Add("quiet");
        // on paper the button is a bare icon in the document's ink (paper is white in both modes, a themed hover would vanish)
        button.Foreground = ink; button.Background = Brushes.Transparent; button.BorderThickness = new Thickness(0); button.CornerRadius = new CornerRadius(0);
        Words(button, b => { AutomationProperties.SetName(b, T("Calendar")); ToolTip.SetTip(b, T("Calendar")); });
        Avalonia.Controls.Primitives.Popup? open = null;
        button.Click += (_, _) =>
        {
            if (open != null) { open.IsOpen = false; return; }
            var invariant = System.Globalization.CultureInfo.InvariantCulture;
            var start = DateTime.TryParseExact(box.Text, format, invariant, System.Globalization.DateTimeStyles.None, out var typed) ? typed : MonthGrid.Now();
            var grid = new MonthGrid(start, _ => true, DayOfWeek.Monday, WeekendMark.Red) { Start = typed == default ? null : typed };
            var card = new Border { Background = B(Raised), BorderBrush = B(Outline), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(CardRadius), Child = grid };
            var popup = new Avalonia.Controls.Primitives.Popup { PlacementTarget = box, Placement = Avalonia.Controls.PlacementMode.BottomEdgeAlignedLeft, VerticalOffset = 4, IsLightDismissEnabled = true, Child = card };
            Menu.OwnPopup(popup, box); ((Avalonia.Controls.ISetLogicalParent)popup).SetParent(box); open = popup;
            grid.Picked += day => { box.SetCurrentValue(TextBox.TextProperty, day?.ToString(format, invariant) ?? ""); popup.IsOpen = false; box.Focus(); };
            grid.Cancel += () => { popup.IsOpen = false; box.Focus(); };
            popup.Closed += (_, _) => { ((Avalonia.Controls.ISetLogicalParent)popup).SetParent(null); open = null; };
            popup.IsOpen = true; grid.Focus(Avalonia.Input.NavigationMethod.Tab);
        };
        return button;
    }

    /// <summary>Re-evaluate placement, missing templates, field errors and overflowing native text. Export refuses errors;
    /// warnings (overlaps, structures outside the printable area or their group) name the structures and still allow export.</summary>
    public void RefreshProblems() { found.Clear(); RefreshProblems(null); }

    // One site per structure, recorded by a full pass; a typed value re-checks only the sites that show it, so a keystroke in a
    // large form costs one field, not the whole document.
    readonly record struct ProblemSite(PaperStructure Structure, PaperStructure[] Overlaps, Rect Area, Rect Container, bool InGroup, int Page);
    readonly List<ProblemSite> sites = new();
    readonly Dictionary<int, (IReadOnlyList<PaperStructure> source, Rect container, ProblemSite[] sites)> problemPages = new();
    readonly Dictionary<int, PaperFormProblem[]> pageProblems = new();
    internal IReadOnlyList<PaperFormProblem> ProblemsOnPage(int index) => pageProblems.GetValueOrDefault(index) ?? Array.Empty<PaperFormProblem>();
    readonly Dictionary<string, ProblemSite> sitesByKey = new(StringComparer.Ordinal);
    readonly Dictionary<string, (ProblemSite site, string? text, string? error, PaperFormProblem[] problems)> found = new(StringComparer.Ordinal);
    internal PaperFormProblem[] ProblemsForStructure(string id) => found.TryGetValue(id, out var cached) ? cached.problems : Array.Empty<PaperFormProblem>();
    PaperFormDefinition? checkedDefinition; object? checkedTypes;
    readonly HashSet<string> changedKeys = new(StringComparer.Ordinal);
    void RefreshProblems(ICollection<string>? keys)
    {
        if (SkipProblemCheck) return;
        var dirtyPages = new HashSet<int>();
        bool full = keys == null || !ReferenceEquals(checkedDefinition, Definition) || !ReferenceEquals(checkedTypes, StructureTypes);
        bool changedPagesOnly = full && !ReferenceEquals(checkedDefinition, Definition) && ReferenceEquals(checkedTypes, StructureTypes)
            && attached && ReferenceEquals(watchedSource, Values) && watched.Count == Values.Count && found.Count == sites.Count;
        var changedSites = new List<ProblemSite>();
        if (full)
        {
            if (!ReferenceEquals(checkedTypes, StructureTypes)) found.Clear();
            sites.Clear(); sitesByKey.Clear(); checkedDefinition = Definition; checkedTypes = StructureTypes;
            var pageSites = new List<ProblemSite>();
            int pageIndex = 0;
            void Walk(IEnumerable<PaperStructure> structures, double x, double y, Rect container, bool inGroup)
            {
                var siblings = structures.ToArray();
                var overlaps = new List<int>[siblings.Length];
                var active = new List<int>();
                // Sweep the less crowded axis: a column of fields should not keep the whole column active.
                bool sweepY = siblings.Length > 1 && siblings.Sum(s => s.Bounds.Width) / (siblings.Max(s => s.Bounds.Rect.Right) - siblings.Min(s => s.Bounds.X))
                    > siblings.Sum(s => s.Bounds.Height) / (siblings.Max(s => s.Bounds.Rect.Bottom) - siblings.Min(s => s.Bounds.Y));
                foreach (int i in Enumerable.Range(0, siblings.Length).OrderBy(i => sweepY ? siblings[i].Bounds.Y : siblings[i].Bounds.X))
                {
                    var bounds = siblings[i].Bounds.Rect;
                    active.RemoveAll(j => sweepY ? siblings[j].Bounds.Rect.Bottom < bounds.Top : siblings[j].Bounds.Rect.Right < bounds.Left);
                    foreach (int j in active)
                        if (bounds.Intersects(siblings[j].Bounds.Rect))
                        {
                            int later = Math.Max(i, j), earlier = Math.Min(i, j);
                            (overlaps[later] ??= new()).Add(earlier);
                        }
                    active.Add(i);
                }
                for (int i = 0; i < siblings.Length; i++)
                {
                    var s = siblings[i]; var r = new Rect(s.Bounds.X + x, s.Bounds.Y + y, s.Bounds.Width, s.Bounds.Height);
                    var site = new ProblemSite(s, overlaps[i]?.Order().Select(j => siblings[j]).ToArray() ?? Array.Empty<PaperStructure>(), r, container, inGroup, pageIndex);
                    pageSites.Add(site);
                    if (s.Kind == "group") Walk(s.Children, r.X, r.Y, r, true);
                }
            }
            var size = Definition.PageSize;
            var container = new Rect(Definition.MarginLeft, Definition.MarginTop, size.Width - Definition.MarginLeft - Definition.MarginRight, size.Height - Definition.MarginTop - Definition.MarginBottom);
            for (int i = 0; i < Definition.Pages.Count; i++)
            {
                var page = Definition.Pages[i];
                pageIndex = i;
                if (!problemPages.TryGetValue(i, out var cachedPage) || !ReferenceEquals(cachedPage.source, page.Structures) || cachedPage.container != container)
                {
                    pageSites.Clear(); Walk(page.Structures, 0, 0, container, false);
                    problemPages[i] = cachedPage = (page.Structures, container, pageSites.ToArray());
                    dirtyPages.Add(i); changedSites.AddRange(cachedPage.sites);
                }
                if (!changedPagesOnly) dirtyPages.Add(i);
                sites.AddRange(cachedPage.sites);
            }
            foreach (int stale in problemPages.Keys.Where(i => i >= Definition.Pages.Count).ToArray()) problemPages.Remove(stale);
            foreach (int stale in pageProblems.Keys.Where(i => i >= Definition.Pages.Count).ToArray()) { pageProblems.Remove(stale); dirtyPages.Add(0); }
            foreach (var site in sites)
                if (site.Structure.FieldKey.Length > 0)
                {
                    sitesByKey.Add(site.Structure.FieldKey, site);
                }
            var known = sites.Select(site => site.Structure.Id).ToHashSet(StringComparer.Ordinal);
            foreach (string id in found.Keys.Where(id => !known.Contains(id)).ToArray()) found.Remove(id);
        }
        IEnumerable<ProblemSite> affected = full ? changedPagesOnly ? changedSites : sites : keys!.Where(sitesByKey.ContainsKey).Select(key => sitesByKey[key]);
        if (changedPagesOnly) affected = affected.Concat((keys ?? Array.Empty<string>()).Concat(changedKeys).Distinct(StringComparer.Ordinal).Where(sitesByKey.ContainsKey).Select(key => sitesByKey[key]));
        foreach (var site in affected)
        {
            var value = Value(site.Structure);
            if (found.TryGetValue(site.Structure.Id, out var cached) && ReferenceEquals(cached.site.Structure, site.Structure)
                && cached.text == value?.Text && cached.error == value?.Error && cached.site.Area == site.Area && cached.site.Container == site.Container
                && cached.site.InGroup == site.InGroup && cached.site.Overlaps.SequenceEqual(site.Overlaps, ReferenceEqualityComparer.Instance)) continue;
            var result = ProblemsAt(site);
            if (!found.ContainsKey(site.Structure.Id) || !cached.problems.SequenceEqual(result)) dirtyPages.Add(site.Page);
            found[site.Structure.Id] = (site, value?.Text, value?.Error, result);
        }
        if (dirtyPages.Count == 0) return;
        foreach (int page in dirtyPages)
        {
            var result = problemPages[page].sites.SelectMany(site => found[site.Structure.Id].problems).ToArray();
            if (!pageProblems.TryGetValue(page, out var previous) || !previous.SequenceEqual(result)) pageProblems[page] = result;
        }
        var next = Enumerable.Range(0, Definition.Pages.Count).SelectMany(i => ProblemsOnPage(i)).ToArray();
        if (!problems.SequenceEqual(next)) SetAndRaise(ProblemsProperty, ref problems, next);
    }

    PaperFormProblem[] ProblemsAt(ProblemSite site)
    {
        var list = new List<PaperFormProblem>();
        var s = site.Structure; var value = Value(s);
        void Problem(string message) => list.Add(new(s.Id, T("“{0}”: {1}", DisplayName(s), message)));
        void Warning(string message) => list.Add(new(s.Id, message, true));
        if (!site.Container.Contains(site.Area)) Warning(site.InGroup ? T("“{0}” is outside its group.", DisplayName(s)) : T("“{0}” is outside the printable area.", DisplayName(s)));
        else if (TitleAbove(s) && site.Area.Y - s.FontSize * 1.4 / DipsPerMm < site.Container.Y) Warning(T("The title of “{0}” is outside the printable area.", DisplayName(s)));
        if (!BuiltIn(s.Kind) && FindTemplate(s.Kind) == null) Problem(T("Unknown structure: {0}", s.Kind));
        if (value?.Error is { } error) Problem(error);
        if (s.Kind == PaperStructureKind.Image && Picture(s) == null) Warning(T("“{0}” has no image.", DisplayName(s)));
        if (s.Kind == "date" && value?.Text is { Length: > 0 } date && !DateTime.TryParseExact(date, s.DateFormat, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
            Problem(T("Enter a valid date using {0}.", s.DateFormat));
        if (s.Kind == "characters")
        {
            if (new System.Globalization.StringInfo(value?.Text ?? "").LengthInTextElements > s.CellCount) Problem(T("Use at most {0} characters.", s.CellCount));
            if ((value?.Text ?? "").Any(c => c is '\r' or '\n' or '\t')) Problem(T("Use a single line without tabs."));
            if (s.CellCount * s.CellWidth > s.Bounds.Width + .001) Problem(T("Character cells exceed the field width."));
        }
        foreach (var other in site.Overlaps) Warning(T("“{0}” overlaps “{1}”.", DisplayName(s), DisplayName(other)));
        if (s.Kind is "text" or "date" or "multiline" or "caption" or "label")
        {
            var font = s.FontFamily == null ? DocumentFont : new FontFamily(s.FontFamily);
            double padding = s.Kind == "label" ? 0 : s.TextPadding;
            double width = Math.Max(1, (s.Bounds.Width - 2 * (s.BorderWidth + padding)) * DipsPerMm);
            double height = (s.Bounds.Height - 2 * (s.BorderWidth + padding)) * DipsPerMm;
            TextLayout Layout(string text, bool wrap) => new(text, new Typeface(font, s.FontStyle, s.FontWeight), s.FontSize, B(Look.Text), textAlignment: s.Alignment, textWrapping: wrap ? TextWrapping.Wrap : TextWrapping.NoWrap, maxWidth: width);
            if (s.Kind == "caption")
                foreach (string caption in new[] { s.Title, s.Subtitle }.Where(t => t.Length > 0)) { using var label = Layout(caption, false); height -= label.Height; if (label.WidthIncludingTrailingWhitespace > width + .5) Problem(T("Text exceeds the field bounds.")); }
            string content = s.Kind == "label" ? s.Title : value?.Text ?? "";
            if (s.Kind is "text" or "caption" && content.Any(c => c is '\r' or '\n' or '\t')) Problem(T("Use a single line without tabs."));
            using var layout = Layout(content, s.Kind is "multiline" or "label");
            if (layout.Height > height + .5 || layout.WidthIncludingTrailingWhitespace > width + .5) Problem(T("Text exceeds the field bounds."));
        }
        return list.ToArray();
    }
}

/// <summary>The printed rule of a field whose frame is not the full outline: a bottom line, optionally with short vertical end ticks.</summary>
internal sealed class PaperRulePanel : Panel
{
    public PaperRulePanel() => IsHitTestVisible = false;
    internal void Set(PaperStructure s, IBrush line)
    {
        Children.Clear();
        double t = s.BorderWidth * PaperFormDefinition.DipsPerMm;
        if (s.BorderStyle is PaperBorderStyle.Full or PaperBorderStyle.None || t <= 0) return;
        Children.Add(new Border { Background = line, Height = t, VerticalAlignment = VerticalAlignment.Bottom });
        if (s.BorderStyle == PaperBorderStyle.BottomTicks)
            foreach (var side in new[] { HorizontalAlignment.Left, HorizontalAlignment.Right })
                Children.Add(new Border { Background = line, Width = t, Height = Math.Max(t, s.EndTickHeight * PaperFormDefinition.DipsPerMm), HorizontalAlignment = side, VerticalAlignment = VerticalAlignment.Bottom });
    }
}

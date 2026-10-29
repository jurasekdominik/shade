using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
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
public class PaperFormView : UserControl
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
    internal readonly List<Canvas> Pages = new() { new Canvas { ClipToBounds = false } };
    internal Canvas Page => Pages[Math.Min(PageIndex, Pages.Count - 1)];
    internal bool ShowAllPages;
    double pageChromeInset;
    internal void UpdatePageInsets(double inset)
    {
        pageChromeInset = inset;
        int columns = ShowAllPages && SideBySidePages ? 2 : 1, i = 0;
        foreach (var page in pageStack.Children.OfType<Canvas>())
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
    bool attached, refreshing, pendingProblems;
    internal Action? PageUpdated;

    public PaperFormView()
    {
        SetCurrentValue(ValuesProperty, new ObservableCollection<PaperFormValue>());
        pageStack.Children.Add(Page);
        scaled = new LayoutTransformControl { Child = pageStack, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };
        Content = scaled;
        Words(this, v => v.RefreshProblems(), "validation");
        Refresh();
    }
    static bool ValidDefinition(PaperFormDefinition d) { if (d == null) return false; d.Validate(); return true; }
    internal static FontFamily DocumentFont => FontManager.Current.SystemFonts.FirstOrDefault(f => f.Name.Equals("Segoe UI", StringComparison.OrdinalIgnoreCase))
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
    internal PaperFormValue? Value(PaperStructure s) => s.FieldKey.Length == 0 ? null : Values.FirstOrDefault(v => v.Key == s.FieldKey);
    internal IEnumerable<PaperStructure> Structures => Definition.Pages[Math.Min(PageIndex, Definition.Pages.Count - 1)].Structures;
    internal PaperStructureTemplate? FindTemplate(string kind) => StructureTypes?.FirstOrDefault(t => t.Kind == kind);
    internal static bool BuiltIn(string kind) => PaperStructureKind.IsBuiltIn(kind);
    internal static IEnumerable<PaperStructure> All(IEnumerable<PaperStructure> structures) => structures.SelectMany(s => new[] { s }.Concat(All(s.Children)));

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValuesProperty) WatchValues();
        if (scaled == null) return;
        if (change.Property == DefinitionProperty && PageIndex >= Definition.Pages.Count) SetCurrentValue(PageIndexProperty, Definition.Pages.Count - 1);
        if (change.Property == DefinitionProperty || change.Property == ValuesProperty || change.Property == ModeProperty || change.Property == StructureTypesProperty || change.Property == PageIndexProperty || change.Property == SideBySidePagesProperty)
            Refresh(change.Property == ModeProperty || change.Property == StructureTypesProperty);
        if (change.Property == ZoomProperty) scaled.LayoutTransform = new ScaleTransform(Zoom, Zoom);
    }
    void WatchValues()
    {
        if (valuesCollection != null) valuesCollection.CollectionChanged -= ValuesChanged;
        foreach (var value in watched) value.PropertyChanged -= ValueChanged;
        watched.Clear(); valuesCollection = null;
        if (!attached) return;
        if (Values == null || Values.Any(v => v == null) || Values.Select(v => v.Key).Distinct(StringComparer.Ordinal).Count() != Values.Count) throw new ArgumentException("Values must have unique, non-null keys.");
        valuesCollection = Values as INotifyCollectionChanged;
        if (valuesCollection != null) valuesCollection.CollectionChanged += ValuesChanged;
        foreach (var value in Values) if (watched.Add(value)) value.PropertyChanged += ValueChanged;
    }
    void ValuesChanged(object? sender, NotifyCollectionChangedEventArgs e) { WatchValues(); if (!refreshing) Refresh(); }
    void ValueChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (pendingProblems) return;
        pendingProblems = true;
        Dispatcher.UIThread.Post(() => { pendingProblems = false; if (attached) RefreshProblems(); });
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) { base.OnAttachedToVisualTree(e); attached = true; WatchValues(); Refresh(); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) { attached = false; WatchValues(); base.OnDetachedFromVisualTree(e); }

    internal void Refresh(bool recreate = false)
    {
        if (refreshing || scaled == null || Values == null) return;
        refreshing = true;
        try
        {
            void Remove(Control c) { if (c.Parent is Canvas parent) parent.Children.Remove(c); }
            if (recreate) { foreach (var editor in editors.Values) Remove(editor.control); editors.Clear(); }
            var size = Definition.PageSize;
            while (Pages.Count < Definition.Pages.Count) Pages.Add(new Canvas { ClipToBounds = false });
            while (Pages.Count > Definition.Pages.Count) Pages.RemoveAt(Pages.Count - 1);
            var visible = ShowAllPages ? Pages.ToArray() : new[] { Page };
            if (!pageStack.Children.SequenceEqual(visible)) { pageStack.Children.Clear(); pageStack.Children.AddRange(visible); }
            int columns = ShowAllPages && SideBySidePages ? 2 : 1;
            pageStack.ColumnDefinitions = new ColumnDefinitions(columns == 2 ? "Auto,Auto" : "Auto");
            pageStack.RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("Auto", (visible.Length + columns - 1) / columns)));
            for (int i = 0; i < visible.Length; i++)
            {
                Grid.SetColumn(visible[i], i % columns); Grid.SetRow(visible[i], i / columns);

            }
            UpdatePageInsets(pageChromeInset);
            foreach (var stale in pageNumbers.Keys.Where(p => !Pages.Contains(p)).ToArray()) pageNumbers.Remove(stale);
            for (int i = 0; i < Pages.Count; i++)
            {
                var page = Pages[i];
                if (!pageNumbers.TryGetValue(page, out var number)) { number = new TextBlock { IsHitTestVisible = false, ZIndex = int.MaxValue - 1, TextAlignment = TextAlignment.Center }; pageNumbers[page] = number; page.Children.Add(number); }
                number.IsVisible = Definition.ShowPageNumbers; number.Text = Definition.PageNumberText(i + 1, Pages.Count); number.TextAlignment = Definition.PageNumberAlignment; number.FontFamily = DocumentFont; number.FontSize = TextSize.Caption; number.Foreground = Ink(Definition.Foreground, PaperInk);
                // inside the margin band of its edge, between the side margins
                double band = Definition.PageNumbersOnTop ? Definition.MarginTop : Definition.MarginBottom;
                number.Width = Math.Max(1, (size.Width - Definition.MarginLeft - Definition.MarginRight) * DipsPerMm); number.Height = Math.Max(1, band * DipsPerMm);
                number.Padding = new Thickness(0, Math.Max(0, (band * DipsPerMm - TextSize.Caption * 1.4) / 2), 0, 0);
                Canvas.SetLeft(number, Definition.MarginLeft * DipsPerMm); Canvas.SetTop(number, Definition.PageNumbersOnTop ? 0 : (size.Height - band) * DipsPerMm);
            }
            foreach (var page in Pages) { page.Width = size.Width * DipsPerMm; page.Height = size.Height * DipsPerMm; page.Background = Ink(Definition.Background, Paper); }
            scaled.LayoutTransform = new ScaleTransform(Zoom, Zoom);
            var present = new HashSet<string>(); int tab = 0;
            void Place(Canvas page, IEnumerable<PaperStructure> structures, double x, double y)
            {
                foreach (var s in structures)
                {
                    if (s.FieldKey.Length > 0 && Value(s) == null)
                    {
                        if (Values.IsReadOnly) throw new ArgumentException("Values must be writable or contain every field key.");
                        Values.Add(new PaperFormValue(s.FieldKey));
                    }
                    var value = Value(s); present.Add(s.Id);
                    if (!editors.TryGetValue(s.Id, out var existing) || existing.value != value || existing.structure with { Bounds = s.Bounds, Children = s.Children, CellCount = s.CellCount, CellWidth = s.CellWidth } != s)
                    {
                        if (existing.control != null) Remove(existing.control);
                        var control = Create(s, value); page.Children.Add(control);
                        existing = (s, value, control); editors[s.Id] = existing;
                    }
                    var c = existing.control;
                    if (c.Parent != page) { Remove(c); page.Children.Add(c); }
                    Canvas.SetLeft(c, (x + s.Bounds.X) * DipsPerMm); Canvas.SetTop(c, (y + s.Bounds.Y) * DipsPerMm);
                    c.Width = s.Bounds.Width * DipsPerMm; c.Height = s.Bounds.Height * DipsPerMm;
                    if ((c is CharacterGridField direct ? direct : (c as Border)?.Child as CharacterGridField) is { } grid && s.Kind == "characters") { grid.SetCurrentValue(CharacterGridField.CellCountProperty, s.CellCount); grid.CellSize = new Size(s.CellWidth * DipsPerMm, Math.Max(1, s.Bounds.Height * DipsPerMm)); if (c != grid) { grid.Width = s.CellCount * s.CellWidth * DipsPerMm; grid.Height = s.Bounds.Height * DipsPerMm; } }
                    if (c is CheckBox check) CheckBoxAppearance.SetIndicatorSize(check, Math.Max(1, Math.Min(s.Bounds.Width, s.Bounds.Height) * DipsPerMm));
                    editors[s.Id] = (s, value, c);
                    c.IsHitTestVisible = Mode != PaperFormMode.Design;
                    c.ZIndex = tab; Avalonia.Input.KeyboardNavigation.SetTabIndex(c, tab++);
                    if (TitleAbove(s))
                    {
                        if (!titles.TryGetValue(s.Id, out var title)) { title = new TextBlock { IsHitTestVisible = false }; titles[s.Id] = title; }
                        if (title.Parent != page) { Remove(title); page.Children.Add(title); }
                        title.Text = s.Title; title.FontFamily = s.FontFamily == null ? DocumentFont : new FontFamily(s.FontFamily); title.FontSize = s.FontSize; title.ZIndex = c.ZIndex;
                        title.Foreground = Ink(s.Foreground ?? Definition.Foreground, PaperInk);
                        Canvas.SetLeft(title, (x + s.Bounds.X) * DipsPerMm); Canvas.SetBottom(title, page.Height - (y + s.Bounds.Y) * DipsPerMm);
                    }
                    else if (titles.Remove(s.Id, out var gone)) Remove(gone);
                    if (s.Kind == "group") Place(page, s.Children, x + s.Bounds.X, y + s.Bounds.Y);
                }
            }
            if (ShowAllPages) { for (int i = 0; i < Pages.Count; i++) Place(Pages[i], Definition.Pages[i].Structures, 0, 0); }
            else Place(Page, Structures, 0, 0);
            foreach (var key in editors.Keys.Where(key => !present.Contains(key)).ToArray()) { Remove(editors[key].control); editors.Remove(key); }
            foreach (var key in titles.Keys.Where(key => !present.Contains(key)).ToArray()) { Remove(titles[key]); titles.Remove(key); }
            WatchValues(); RefreshProblems(); PageUpdated?.Invoke();
        }
        finally { refreshing = false; }
    }

    Control Create(PaperStructure s, PaperFormValue? value)
    {
        bool readOnly = Mode == PaperFormMode.Preview || s.IsReadOnly;
        Control result;
        IBrush ink = Ink(s.Foreground ?? Definition.Foreground, PaperInk), fill = Ink(s.Background ?? Definition.Background, Paper), line = Ink(s.BorderColor, PaperRule);
        var font = s.FontFamily == null ? DocumentFont : new FontFamily(s.FontFamily);
        if (FindTemplate(s.Kind) is { } custom) result = custom.Create(s, value, readOnly);
        else if (s.Kind is "label" or "group")
        {
            result = new Border { Background = s.Background == null ? Brushes.Transparent : fill, BorderBrush = line, BorderThickness = new Thickness(s.BorderWidth * DipsPerMm), Child = new TextBlock { Text = s.Title, Foreground = ink, FontFamily = font, FontSize = s.FontSize, TextAlignment = s.Alignment, TextWrapping = TextWrapping.Wrap } };
        }
        else if (s.Kind == PaperStructureKind.Image)
        {
            result = Picture(s) is { } picture
                ? new Border { Background = Brushes.Transparent, BorderBrush = line, BorderThickness = new Thickness(s.BorderWidth * DipsPerMm), Child = new Avalonia.Controls.Image { Source = picture, Stretch = s.KeepAspectRatio ? Stretch.Uniform : Stretch.Fill } }
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
            var box = new PaperCheckBox { IsReadOnly = readOnly, Content = s.Title.Length == 0 ? null : s.Title, Background = fill, Foreground = ink, BorderBrush = line, BorderThickness = new Thickness(s.BorderWidth * DipsPerMm), CornerRadius = new CornerRadius(0), FontSize = s.FontSize, FontFamily = font };
            CheckBoxAppearance.SetIndicatorSize(box, Math.Max(1, Math.Min(s.Bounds.Width, s.Bounds.Height) * DipsPerMm));
            CheckBoxAppearance.SetMarkBrush(box, ink);
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
            box.MinHeight = 0; box.Padding = new Thickness(s.Kind == "characters" ? 0 : s.TextPadding * DipsPerMm); box.CornerRadius = new CornerRadius(0); box.Background = fill; box.Foreground = ink;
            box.BorderBrush = line; box.BorderThickness = new Thickness(s.BorderWidth * DipsPerMm); box.FontSize = s.FontSize; box.FontFamily = font;
            box.IsReadOnly = readOnly; box.CaretBrush = ink; box.SelectionForegroundBrush = ink; box.TextAlignment = s.Kind == PaperStructureKind.Characters ? s.GridAlignment : s.Alignment;
            if (s.Kind == "multiline")
            {
                ScrollViewer.SetVerticalScrollBarVisibility(box, s.AllowMultilineScrolling && Mode != PaperFormMode.Preview ? Avalonia.Controls.Primitives.ScrollBarVisibility.Auto : Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled);
                ScrollViewer.SetHorizontalScrollBarVisibility(box, Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled);
            }
            box.AcceptsReturn = s.Kind == "multiline"; box.TextWrapping = s.Kind == "multiline" ? TextWrapping.Wrap : TextWrapping.NoWrap;
            if (value != null) box.Bind(TextBox.TextProperty, new Binding(nameof(PaperFormValue.Text)) { Source = value, Mode = BindingMode.TwoWay });
            AutomationProperties.SetName(box, s.Label.Length > 0 ? s.Label : s.Title.Length > 0 ? s.Title : s.FieldKey);
            if (box is CharacterGridField grid)
                foreach (var range in s.CellRanges) grid.CellDecorations!.Add(new CharacterCellDecoration { Start = range.Start, Length = range.Length, Background = range.Background == null ? null : Ink(range.Background, Paper), Foreground = range.Foreground == null ? null : Ink(range.Foreground, PaperInk) });
            result = box;
            if (s.Kind == "date") { box.PlaceholderText = s.DateFormat; if (!readOnly) box.InnerRightContent = DateButton(box, s.DateFormat); }
            if (s.Kind == "characters" && s.GridAlignment != TextAlignment.Left)
            {
                box.Width = s.CellCount * s.CellWidth * DipsPerMm; box.HorizontalAlignment = s.GridAlignment == TextAlignment.Right ? HorizontalAlignment.Right : HorizontalAlignment.Center;
                result = new Border { Background = Brushes.Transparent, Child = box };
            }
            if (s.Kind == "caption")
            {
                var panel = new DockPanel();
                foreach (var (text, dock) in new[] { (s.Title, Dock.Top), (s.Subtitle, Dock.Bottom) })
                    if (text.Length > 0) { var label = new TextBlock { Text = text, FontSize = s.FontSize, FontFamily = font, Foreground = ink, TextAlignment = s.Alignment }; DockPanel.SetDock(label, dock); panel.Children.Add(label); }
                panel.Children.Add(box); result = panel;
            }
        }
        else result = new TextBlock { Text = T("Unknown structure: {0}", s.Kind), Foreground = B(Danger), TextWrapping = TextWrapping.Wrap };
        AutomationProperties.SetName(result, s.Label.Length > 0 ? s.Label : s.FieldKey.Length > 0 ? s.FieldKey : s.Title.Length > 0 ? s.Title : s.Kind == "line" ? T("Line") : "");
        return result;
    }

    /// <summary>A calendar button inside a date field (never in Preview, so it never prints): a picked day is written in the field's format.</summary>
    static Control DateButton(TextBox box, string format)
    {
        var button = new IconButton { Icon = Icons.Calendar, IconSize = M(14), Width = M(20), Height = M(20), MinHeight = 0, Padding = new Thickness(0), Focusable = false, VerticalAlignment = VerticalAlignment.Center };
        button.Classes.Add("quiet");
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
    public void RefreshProblems()
    {
        var list = new List<PaperFormProblem>();
        void Check(IEnumerable<PaperStructure> structures, double x, double y, Rect container, bool inGroup)
        {
            var siblings = structures.ToArray();
            for (int i = 0; i < siblings.Length; i++)
            {
                var s = siblings[i]; var r = new Rect(s.Bounds.X + x, s.Bounds.Y + y, s.Bounds.Width, s.Bounds.Height);
                void Problem(string message) => list.Add(new(s.Id, T("“{0}”: {1}", DisplayName(s), message)));
                void Warning(string message) => list.Add(new(s.Id, message, true));
                if (!container.Contains(r)) Warning(inGroup ? T("“{0}” is outside its group.", DisplayName(s)) : T("“{0}” is outside the printable area.", DisplayName(s)));
                if (!BuiltIn(s.Kind) && FindTemplate(s.Kind) == null) Problem(T("Unknown structure: {0}", s.Kind));
                if (Value(s)?.Error is { } error) Problem(error);
                if (s.Kind == PaperStructureKind.Image && Picture(s) == null) Warning(T("“{0}” has no image.", DisplayName(s)));
                if (s.Kind == "date" && Value(s)?.Text is { Length: > 0 } date && !DateTime.TryParseExact(date, s.DateFormat, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                    Problem(T("Enter a valid date using {0}.", s.DateFormat));
                if (s.Kind == "characters")
                {
                    if (new System.Globalization.StringInfo(Value(s)?.Text ?? "").LengthInTextElements > s.CellCount) Problem(T("Use at most {0} characters.", s.CellCount));
                    if ((Value(s)?.Text ?? "").Any(c => c is '\r' or '\n' or '\t')) Problem(T("Use a single line without tabs."));
                    if (s.CellCount * s.CellWidth > s.Bounds.Width + .001) Problem(T("Character cells exceed the field width."));
                }
                for (int j = 0; j < i; j++) if (s.Bounds.Rect.Intersects(siblings[j].Bounds.Rect)) Warning(T("“{0}” overlaps “{1}”.", DisplayName(s), DisplayName(siblings[j])));
                if (s.Kind == "group") Check(s.Children, r.X, r.Y, r, true);
                if (s.Kind is "text" or "date" or "multiline" or "caption" or "label")
                {
                    var font = s.FontFamily == null ? DocumentFont : new FontFamily(s.FontFamily);
                    double padding = s.Kind == "label" ? 0 : s.TextPadding;
                    double width = Math.Max(1, (s.Bounds.Width - 2 * (s.BorderWidth + padding)) * DipsPerMm);
                    double height = (s.Bounds.Height - 2 * (s.BorderWidth + padding)) * DipsPerMm;
                    TextLayout Layout(string text, bool wrap) => new(text, new Typeface(font), s.FontSize, B(Look.Text), textAlignment: s.Alignment, textWrapping: wrap ? TextWrapping.Wrap : TextWrapping.NoWrap, maxWidth: width);
                    if (s.Kind == "caption")
                        foreach (string caption in new[] { s.Title, s.Subtitle }.Where(t => t.Length > 0)) { using var label = Layout(caption, false); height -= label.Height; if (label.WidthIncludingTrailingWhitespace > width + .5) Problem(T("Text exceeds the field bounds.")); }
                    string content = s.Kind == "label" ? s.Title : Value(s)?.Text ?? "";
                    if (s.Kind is "text" or "caption" && content.Any(c => c is '\r' or '\n' or '\t')) Problem(T("Use a single line without tabs."));
                    using var layout = Layout(content, s.Kind is "multiline" or "label");
                    if (layout.Height > height + .5 || layout.WidthIncludingTrailingWhitespace > width + .5) Problem(T("Text exceeds the field bounds."));
                }
            }
        }
        var size = Definition.PageSize;
        foreach (var page in Definition.Pages) Check(page.Structures, 0, 0, new Rect(Definition.MarginLeft, Definition.MarginTop, size.Width - Definition.MarginLeft - Definition.MarginRight, size.Height - Definition.MarginTop - Definition.MarginBottom), false);
        SetAndRaise(ProblemsProperty, ref problems, list.ToArray());
    }
}

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Metadata;
using Avalonia.Metadata;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using static Shade.Look;

namespace Shade;

/// <summary>A native single-selection ComboBox with a fixed search header and virtualized, word-filtered results.</summary>
public sealed class SearchChoice : ComboBox
{
    public static readonly StyledProperty<string> SearchTextProperty = AvaloniaProperty.Register<SearchChoice, string>(nameof(SearchText), "", defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<BindingBase?> SearchMemberBindingProperty = AvaloniaProperty.Register<SearchChoice, BindingBase?>(nameof(SearchMemberBinding));
    public static readonly StyledProperty<string?> SearchPlaceholderTextProperty = AvaloniaProperty.Register<SearchChoice, string?>(nameof(SearchPlaceholderText));
    public static readonly StyledProperty<string?> NoResultsTextProperty = AvaloniaProperty.Register<SearchChoice, string?>(nameof(NoResultsText));
    public static readonly RoutedEvent<RoutedEventArgs> SelectionCommittedEvent = RoutedEvent.Register<SearchChoice, RoutedEventArgs>(nameof(SelectionCommitted), RoutingStrategies.Bubble);
    /// <summary>Local filter query. Opening starts with all options; filtering does not change the committed selection.</summary>
    public string SearchText { get => GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value ?? ""); }
    /// <summary>Optional searchable fields on each item. Defaults to DisplayMemberBinding, then ToString.</summary>
    [AssignBinding]
    [InheritDataTypeFromItems(nameof(ItemsSource))]
    public BindingBase? SearchMemberBinding { get => GetValue(SearchMemberBindingProperty); set => SetValue(SearchMemberBindingProperty, value); }
    public string? SearchPlaceholderText { get => GetValue(SearchPlaceholderTextProperty); set => SetValue(SearchPlaceholderTextProperty, value); }
    public string? NoResultsText { get => GetValue(NoResultsTextProperty); set => SetValue(NoResultsTextProperty, value); }
    /// <summary>A result was explicitly activated. Native SelectionChanged also observes programmatic changes.</summary>
    public event EventHandler<RoutedEventArgs>? SelectionCommitted { add => AddHandler(SelectionCommittedEvent, value); remove => RemoveHandler(SelectionCommittedEvent, value); }
    protected override Type StyleKeyOverride => typeof(SearchChoice);
    sealed record Entry(object? Item, string Label, string Search);
    Entry[] entries = Array.Empty<Entry>();
    Entry[] matches = Array.Empty<Entry>();
    bool dirty = true, queued;
    SearchField? search;
    ListBox? results;
    TextBlock? empty, placeholder;
    ContentPresenter? caption;
    Border? shell;
    Popup? popup;
    static SearchChoice() => IsTextSearchEnabledProperty.OverrideDefaultValue<SearchChoice>(false);
    public SearchChoice()
    {
        Items.CollectionChanged += ItemsChanged;
        DetachedFromVisualTree += (_, _) => SetCurrentValue(IsDropDownOpenProperty, false);
        SizeChanged += (_, _) => { if (shell != null && IsDropDownOpen) shell.Width = Bounds.Width; };
        Words(this, picker => { picker.dirty = true; picker.Refresh(); if (picker.IsDropDownOpen) picker.Filter(); }, "search");
    }
    void ItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        dirty = true;
        if (!IsDropDownOpen || queued) return;
        queued = true;
        Dispatcher.UIThread.Post(() => { queued = false; if (IsDropDownOpen) Filter(); });
    }
    /// <summary>Refresh cached search fields after in-place item edits. Collection changes and opening refresh automatically.</summary>
    public void RefreshSearch() { dirty = true; if (IsDropDownOpen) Filter(); }
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEffectivelyEnabledProperty && !IsEffectivelyEnabled) SetCurrentValue(IsDropDownOpenProperty, false);
        if (change.Property == SearchMemberBindingProperty || change.Property == DisplayMemberBindingProperty || change.Property == ItemTemplateProperty || change.Property == TextSearch.TextBindingProperty)
        {
            if (results != null && change.Property != SearchMemberBindingProperty) results.ItemTemplate = ResultTemplate();
            RefreshSearch();
        }
        if (change.Property == SearchTextProperty && IsDropDownOpen) Filter();
        if (change.Property == SelectedItemProperty || change.Property == PlaceholderTextProperty || change.Property == SearchPlaceholderTextProperty || change.Property == NoResultsTextProperty || change.Property == IsEffectivelyEnabledProperty) Refresh();
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!IsDropDownOpen && e.Key is Key.Down or Key.Up or Key.Enter or Key.Space)
        {
            if (IsEffectivelyEnabled) SetCurrentValue(IsDropDownOpenProperty, true);
            e.Handled = true; return;
        }
        base.OnKeyDown(e);
    }
    internal Control CreateTemplate(INameScope names)
    {
        caption = new ContentPresenter { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Stretch };
        caption[!ContentPresenter.ContentProperty] = this[!SelectionBoxItemProperty];
        caption[!ContentPresenter.ContentTemplateProperty] = this[!SelectionBoxItemTemplateProperty];
        placeholder = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
        var arrow = Build.Icon24(Icons.ChevronDown, M(16), 2); arrow.Stroke = B(Text3); arrow.Margin = new Thickness(M(4), 0, 0, 0);
        var text = new Panel { Children = { caption, placeholder } };
        var face = new DockPanel(); DockPanel.SetDock(arrow, Dock.Right); face.Children.Add(arrow); face.Children.Add(text);
        var trigger = new Button { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Focusable = false, MinHeight = 0, MinWidth = 0, Padding = new Thickness(0), Background = Brushes.Transparent, Template = new FuncControlTemplate<Button>((_, _) => face) };
        AutomationProperties.SetAccessibilityView(trigger, AccessibilityView.Raw);
        trigger.Click += (_, _) => { Focus(NavigationMethod.Pointer); SetCurrentValue(IsDropDownOpenProperty, !IsDropDownOpen); };
        var border = new Border { Child = trigger };
        border.Bind(Border.BackgroundProperty, new TemplateBinding(BackgroundProperty)); border.Bind(Border.BorderBrushProperty, new TemplateBinding(BorderBrushProperty));
        border.Bind(Border.BorderThicknessProperty, new TemplateBinding(BorderThicknessProperty)); border.Bind(Border.CornerRadiusProperty, new TemplateBinding(CornerRadiusProperty)); border.Bind(Border.PaddingProperty, new TemplateBinding(PaddingProperty));
        search = new SearchField { Margin = new Thickness(M(8)) };
        search.Bind(Field.TextProperty, new Binding(nameof(SearchText)) { Source = this, Mode = BindingMode.TwoWay });
        results = new ListBox { MinHeight = 0, MinWidth = 0, BorderThickness = new Thickness(0), Padding = new Thickness(0), Background = Brushes.Transparent, Focusable = false, ItemContainerTheme = SearchChoiceTheme.ResultItem(), ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel()) };
        AutomationProperties.SetName(results, T("Options"));
        results.SelectionChanged += (_, e) => e.Handled = true;
        results.ItemTemplate = ResultTemplate();
        empty = new TextBlock { Foreground = B(Text3), Margin = new Thickness(M(8)), TextWrapping = TextWrapping.Wrap };
        var header = new Border { BorderBrush = B(Look.Separator), BorderThickness = new Thickness(0, 0, 0, 1), Child = search };
        var body = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Children = { header, results, empty } }; Grid.SetRow(results, 1); Grid.SetRow(empty, 1);
        shell = new Border { Background = B(Raised), BorderBrush = B(Outline), BorderThickness = new Thickness(1, 0, 1, 1), CornerRadius = new CornerRadius(0, 0, Radius, Radius), Child = body };
        shell[!MaxHeightProperty] = this[!MaxDropDownHeightProperty];
        popup = new Popup { Name = "PART_Popup", Child = shell, PlacementTarget = this, Placement = PlacementMode.BottomEdgeAlignedLeft, IsLightDismissEnabled = true };
        names.Register("PART_Popup", popup);
        popup.Bind(Popup.IsOpenProperty, new Binding(nameof(IsDropDownOpen)) { Source = this, Mode = BindingMode.TwoWay });
        popup.Opened += (_, _) =>
        {
            Menu.OwnPopup(popup, this, () => Close(true, NavigationMethod.Pointer));
            shell.Width = Bounds.Width; dirty = true;
            SetCurrentValue(SearchTextProperty, ""); Filter();
            Dispatcher.UIThread.Post(() => { if (IsDropDownOpen) search.Box.Focus(NavigationMethod.Tab); }, DispatcherPriority.Input);
        };
        popup.Closed += (_, _) => { results.ItemsSource = null; entries = matches = Array.Empty<Entry>(); dirty = true; };
        body.AddHandler(KeyDownEvent, PopupKey, RoutingStrategies.Tunnel);
        Refresh(); return new Panel { Children = { border, popup } };
    }
    IDataTemplate ResultTemplate() => new FuncDataTemplate<Entry>((entry, _) => entry == null ? null : ResultButton(entry));
    Button ResultButton(Entry entry)
    {
        var button = new Button { Content = entry.Item, ContentTemplate = ItemTemplate ?? LabelTemplate(), MinHeight = ControlHeight, MinWidth = 0, Padding = new Thickness(M(8), 0), Background = Brushes.Transparent, Cursor = Build.Hand, Focusable = false, FontWeight = FontWeight.Normal, FontFamily = UiFont, FontSize = TextSize.Body, Foreground = B(Look.Text), HorizontalContentAlignment = HorizontalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        button.Template = new FuncControlTemplate<Button>((row, _) =>
        {
            var label = new ContentPresenter { VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
            label.Bind(ContentPresenter.ContentProperty, new TemplateBinding(ContentControl.ContentProperty)); label.Bind(ContentPresenter.ContentTemplateProperty, new TemplateBinding(ContentControl.ContentTemplateProperty));
            var surface = new Border { Child = label }; surface.Bind(Border.PaddingProperty, new TemplateBinding(PaddingProperty)); surface.Bind(Border.BackgroundProperty, new TemplateBinding(BackgroundProperty)); return surface;
        });
        AutomationProperties.SetName(button, entry.Label); button.Click += (_, _) => Commit(entry); return button;
    }
    sealed class MatchLabel : TextBlock { }
    IDataTemplate LabelTemplate() => new FuncDataTemplate<object?>((item, _) =>
    {
        var label = new MatchLabel { TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        label.PropertyChanged += (_, change) => { if (change.Property == TagProperty) Build.Mark(label, label.Tag?.ToString() ?? "", SearchText); };
        if ((TextSearch.GetTextBinding(this) ?? DisplayMemberBinding) is { } binding) label.Bind(TagProperty, binding); else label.Tag = item?.ToString() ?? PlaceholderText ?? "";
        return label;
    }, supportsRecycling: true);
    void BuildEntries()
    {
        using var display = new TextReader(TextSearch.GetTextBinding(this) ?? DisplayMemberBinding);
        using var searchable = new TextReader(SearchMemberBinding);
        entries = ItemsView.Select(item => { string label = display.Read(item); return new Entry(item, label, SearchMemberBinding == null ? label : searchable.Read(item)); }).ToArray();
        dirty = false;
    }
    void Filter()
    {
        if (results == null) return;
        if (dirty) BuildEntries();
        string[] words = (SearchText ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var compare = Culture.CompareInfo;
        matches = words.Length == 0 ? entries : entries.Where(entry => words.All(word => compare.IndexOf(entry.Search, word, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0)).ToArray();
        results.ItemsSource = matches;
        results.SelectedIndex = words.Length == 0 ? Array.FindIndex(matches, entry => Equals(entry.Item, SelectedItem)) : matches.Length > 0 ? 0 : -1;
        results.IsVisible = matches.Length > 0; empty!.IsVisible = matches.Length == 0;
        if (results.SelectedIndex >= 0) results.ScrollIntoView(results.SelectedIndex);
        foreach (var label in results.GetVisualDescendants().OfType<MatchLabel>()) Build.Mark(label, label.Tag?.ToString() ?? "", SearchText);
    }
    void PopupKey(object? sender, KeyEventArgs e)
    {
        if (results == null) return;
        if (e.Key is Key.Down or Key.Up)
        {
            int at = results.SelectedIndex;
            results.SelectedIndex = matches.Length == 0 ? -1 : at < 0 ? (e.Key == Key.Down ? 0 : matches.Length - 1) : Math.Clamp(at + (e.Key == Key.Down ? 1 : -1), 0, matches.Length - 1);
            if (results.SelectedIndex >= 0) results.ScrollIntoView(results.SelectedIndex);
        }
        else if (e.Key == Key.Enter) { if (results.SelectedItem is Entry entry) Commit(entry, NavigationMethod.Directional); }
        else if (e.Key == Key.Escape) Close(true);
        else if (e.Key == Key.Tab)
        {
            Close(true);
            TopLevel.GetTopLevel(this)?.FocusManager?.TryMoveFocus(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? NavigationDirection.Previous : NavigationDirection.Next);
        }
        else return;
        e.Handled = true;
    }
    void Commit(Entry entry, NavigationMethod navigation = NavigationMethod.Pointer)
    {
        if (!IsDropDownOpen || !IsEffectivelyEnabled || !IsEffectivelyVisible || !matches.Contains(entry)) return;
        int index = Items.IndexOf(entry.Item);
        if (index < 0) return;
        SetCurrentValue(SelectedIndexProperty, index);
        Close(true, navigation); RaiseEvent(new RoutedEventArgs(SelectionCommittedEvent));
    }
    void Close(bool focus, NavigationMethod navigation = NavigationMethod.Directional)
    {
        if (focus && IsEffectivelyEnabled && IsEffectivelyVisible) Focus(navigation);
        SetCurrentValue(IsDropDownOpenProperty, false);
    }
    void Refresh()
    {
        if (placeholder != null) { placeholder.Text = PlaceholderText; placeholder.Foreground = B(IsEffectivelyEnabled ? Text3 : Disabled); placeholder.IsVisible = SelectedItem == null; }
        if (caption != null) caption.IsVisible = SelectedItem != null;
        if (search != null) { search.PlaceholderText = SearchPlaceholderText ?? T("Search"); AutomationProperties.SetName(search.Box, search.PlaceholderText); }
        if (empty != null) empty.Text = NoResultsText ?? T("No matches");
    }
    sealed class TextReader : StyledElement, IDisposable
    {
        static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<TextReader, string?>("Value");
        readonly BindingExpressionBase? expression;
        public TextReader(BindingBase? binding) { if (binding != null) expression = Bind(ValueProperty, binding); }
        public string Read(object? item) { if (expression == null) return item?.ToString() ?? ""; DataContext = item; return GetValue(ValueProperty) ?? ""; }
        public void Dispose() { expression?.Dispose(); DataContext = null; }
    }
}

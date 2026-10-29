using System;
using System.Globalization;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using static Shade.Look;

namespace Shade;

public enum PaperInspectorPlacement { Sidebar, Ribbon, SidebarLeft, RibbonBottom }
public enum PaperNumberButtonsPlacement { Right, Left, Top, Bottom, Hidden }

public sealed partial class PaperFormBuilder
{
    public static readonly StyledProperty<PaperInspectorPlacement> InspectorPlacementProperty = AvaloniaProperty.Register<PaperFormBuilder, PaperInspectorPlacement>(nameof(InspectorPlacement), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay, validate: Enum.IsDefined);
    public static readonly StyledProperty<bool> ShowPageNavigationProperty = AvaloniaProperty.Register<PaperFormBuilder, bool>(nameof(ShowPageNavigation), true);
    public PaperInspectorPlacement InspectorPlacement { get => GetValue(InspectorPlacementProperty); set => SetValue(InspectorPlacementProperty, value); }
    public bool ShowPageNavigation { get => GetValue(ShowPageNavigationProperty); set => SetValue(ShowPageNavigationProperty, value); }
    public static readonly StyledProperty<PaperNumberButtonsPlacement> NumberButtonsPlacementProperty = AvaloniaProperty.Register<PaperFormBuilder, PaperNumberButtonsPlacement>(nameof(NumberButtonsPlacement), validate: Enum.IsDefined);
    public static readonly StyledProperty<double> NumberButtonSizeProperty = AvaloniaProperty.Register<PaperFormBuilder, double>(nameof(NumberButtonSize), 28, validate: n => double.IsFinite(n) && n >= 16 && n <= 80);
    public static readonly StyledProperty<Dock> ToolbarPlacementProperty = AvaloniaProperty.Register<PaperFormBuilder, Dock>(nameof(ToolbarPlacement), Dock.Top, validate: Enum.IsDefined);
    public static readonly StyledProperty<Dock> GridSettingsPlacementProperty = AvaloniaProperty.Register<PaperFormBuilder, Dock>(nameof(GridSettingsPlacement), Dock.Top, validate: Enum.IsDefined);
    public static readonly StyledProperty<Dock> LayersPlacementProperty = AvaloniaProperty.Register<PaperFormBuilder, Dock>(nameof(LayersPlacement), Dock.Left, validate: Enum.IsDefined);
    public static readonly StyledProperty<double> LayersSizeProperty = AvaloniaProperty.Register<PaperFormBuilder, double>(nameof(LayersSize), 192, validate: n => double.IsFinite(n) && n >= 120 && n <= 800);
    public static readonly StyledProperty<Dock> FooterPlacementProperty = AvaloniaProperty.Register<PaperFormBuilder, Dock>(nameof(FooterPlacement), Dock.Bottom, validate: Enum.IsDefined);
    public PaperNumberButtonsPlacement NumberButtonsPlacement { get => GetValue(NumberButtonsPlacementProperty); set => SetValue(NumberButtonsPlacementProperty, value); }
    public double NumberButtonSize { get => GetValue(NumberButtonSizeProperty); set => SetValue(NumberButtonSizeProperty, value); }
    public Dock ToolbarPlacement { get => GetValue(ToolbarPlacementProperty); set => SetValue(ToolbarPlacementProperty, value); }
    public Dock GridSettingsPlacement { get => GetValue(GridSettingsPlacementProperty); set => SetValue(GridSettingsPlacementProperty, value); }
    public Dock LayersPlacement { get => GetValue(LayersPlacementProperty); set => SetValue(LayersPlacementProperty, value); }
    public double LayersSize { get => GetValue(LayersSizeProperty); set => SetValue(LayersSizeProperty, value); }
    public Dock FooterPlacement { get => GetValue(FooterPlacementProperty); set => SetValue(FooterPlacementProperty, value); }
    readonly DockPanel footer;
    readonly List<(Field field, StackPanel arrows)> numberLayouts = new();
    public static readonly StyledProperty<double> InspectorWidthProperty = AvaloniaProperty.Register<PaperFormBuilder, double>(nameof(InspectorWidth), 224, validate: n => double.IsFinite(n) && n >= 160 && n <= 800);
    public static readonly StyledProperty<double> RibbonFieldWidthProperty = AvaloniaProperty.Register<PaperFormBuilder, double>(nameof(RibbonFieldWidth), 160, validate: n => double.IsFinite(n) && n >= 96 && n <= 600);
    public static readonly StyledProperty<double> PropertyRibbonHeightProperty = AvaloniaProperty.Register<PaperFormBuilder, double>(nameof(PropertyRibbonHeight), 176, validate: n => double.IsFinite(n) && n >= 80 && n <= 800);
    public double InspectorWidth { get => GetValue(InspectorWidthProperty); set => SetValue(InspectorWidthProperty, value); }
    public double RibbonFieldWidth { get => GetValue(RibbonFieldWidthProperty); set => SetValue(RibbonFieldWidthProperty, value); }
    public double PropertyRibbonHeight { get => GetValue(PropertyRibbonHeightProperty); set => SetValue(PropertyRibbonHeightProperty, value); }
    readonly WrapPanel gridRibbon = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, M(8)) };
    readonly StackPanel pageNavigation = new() { Orientation = Orientation.Horizontal, Spacing = M(8) };
    readonly TextBlock pageCaption = Build.Text("", Text2, TextSize.Caption);
    readonly IconButton previousPage = new() { Icon = Icons.ChevronLeft }, nextPage = new() { Icon = Icons.ChevronRight };
    readonly Field gridPitchInput = new() { Width = M(112) };
    readonly ScrollViewer inspectorScroll;
    void ConfigurePresentation()
    {
        var gridLabel = Labelled("Grid pitch (mm)", gridPitchInput); gridLabel.Margin = new Thickness(0, 0, M(16), 0); gridRibbon.Children.Add(gridLabel);
        gridPitchInput.Text = GridPitch.ToString(Culture);
        gridPitchInput.Edited += () =>
        {
            if (!double.TryParse(gridPitchInput.Text, NumberStyles.Float, Culture, out double n)) { gridPitchInput.Error = T("Enter a valid value."); return; }
            try { SetCurrentValue(GridPitchProperty, n); gridPitchInput.Error = null; }
            catch (ArgumentException) { gridPitchInput.Error = T("Enter a valid value."); }
        };
        NumberControls(gridPitchInput, .5, .5, () => GridPitch);
        void Flag(string label, AvaloniaProperty property)
        {
            var box = Words(new CheckBox { Margin = new Thickness(0, M(16), M(16), 0) }, c => c.Content = T(label));
            box.Bind(CheckBox.IsCheckedProperty, new Avalonia.Data.Binding(property.Name) { Source = this, Mode = Avalonia.Data.BindingMode.TwoWay }); gridRibbon.Children.Add(box);
        }
        Flag("Show grid", ShowGridProperty); Flag("Snap to fields", SnapToElementsProperty); Flag("Show guides", ShowGuidesProperty); Flag("Show rulers", ShowRulersProperty);
        Words(previousPage, c => { ToolTip.SetTip(c, T("Previous page")); AutomationProperties.SetName(c, T("Previous page")); });
        Words(nextPage, c => { ToolTip.SetTip(c, T("Next page")); AutomationProperties.SetName(c, T("Next page")); });
        previousPage.Click += (_, _) => SetCurrentValue(PageIndexProperty, Math.Max(0, PageIndex - 1));
        nextPage.Click += (_, _) => SetCurrentValue(PageIndexProperty, Math.Min(Definition.Pages.Count - 1, PageIndex + 1));
        pageNavigation.Children.Add(previousPage); pageNavigation.Children.Add(pageCaption); pageNavigation.Children.Add(nextPage);
        pageCaption.VerticalAlignment = VerticalAlignment.Center;
    }
    void UpdatePresentation()
    {
        DockPanel.SetDock(toolbar, ToolbarPlacement); DockPanel.SetDock(gridRibbon, GridSettingsPlacement); DockPanel.SetDock(layersPanel, LayersPlacement); DockPanel.SetDock(footer, FooterPlacement);
        toolbar.Orientation = ToolbarPlacement is Dock.Left or Dock.Right ? Orientation.Vertical : Orientation.Horizontal;
        toolbar.Width = ToolbarPlacement is Dock.Left or Dock.Right ? M(208) : double.NaN;
        gridRibbon.Orientation = GridSettingsPlacement is Dock.Left or Dock.Right ? Orientation.Vertical : Orientation.Horizontal;
        gridRibbon.Width = GridSettingsPlacement is Dock.Left or Dock.Right ? M(160) : double.NaN;
        layersPanel.Width = LayersPlacement is Dock.Left or Dock.Right ? M(LayersSize) : double.NaN;
        layersPanel.Height = LayersPlacement is Dock.Top or Dock.Bottom ? M(LayersSize) : double.NaN;
        LayoutNumbers();
        bool ribbon = InspectorPlacement is PaperInspectorPlacement.Ribbon or PaperInspectorPlacement.RibbonBottom;
        DockPanel.SetDock(inspectorScroll, InspectorPlacement switch { PaperInspectorPlacement.Ribbon => Dock.Top, PaperInspectorPlacement.RibbonBottom => Dock.Bottom, PaperInspectorPlacement.SidebarLeft => Dock.Left, _ => Dock.Right });
        inspector.Width = ribbon ? double.NaN : M(InspectorWidth); inspector.ItemWidth = M(ribbon ? RibbonFieldWidth : InspectorWidth);
        inspector.Margin = ribbon ? new Thickness(0) : new Thickness(M(16), 0, 0, 0);
        inspectorScroll.MaxHeight = ribbon ? M(PropertyRibbonHeight) : double.PositiveInfinity;
        inspectorScroll.Margin = ribbon ? new Thickness(0, 0, 0, M(8)) : new Thickness(0);
        inspectorScroll.IsVisible = ShowInspector && Mode == PaperFormMode.Design;
        gridRibbon.IsVisible = ShowToolbar && Mode == PaperFormMode.Design;
        if (!gridPitchInput.Box.IsFocused) gridPitchInput.Text = GridPitch.ToString(Culture);
        pageNavigation.IsVisible = ShowPageNavigation;
        pageCaption.Text = T("Page {0} of {1}", PageIndex + 1, Definition.Pages.Count);
        previousPage.IsEnabled = PageIndex > 0; nextPage.IsEnabled = PageIndex + 1 < Definition.Pages.Count;
    }
    double NumberStep(string label) => label is "Cell count" or "Font size (DIP)" ? 1 : label is "Border (mm)" or "Line width (mm)" ? .1 : label == "Text padding (mm)" ? .5 : label is "X (mm)" or "Y (mm)" && Selected?.SnapToGrid == false ? .1 : GridPitch;
    void LayoutNumbers()
    {
        bool stacked = NumberButtonsPlacement is PaperNumberButtonsPlacement.Top or PaperNumberButtonsPlacement.Bottom;
        var dock = NumberButtonsPlacement switch { PaperNumberButtonsPlacement.Left => Dock.Left, PaperNumberButtonsPlacement.Top => Dock.Top, PaperNumberButtonsPlacement.Bottom => Dock.Bottom, _ => Dock.Right };
        foreach (var (field, arrows) in numberLayouts)
        {
            DockPanel.SetDock(arrows, dock); arrows.IsVisible = NumberButtonsPlacement != PaperNumberButtonsPlacement.Hidden;
            arrows.HorizontalAlignment = HorizontalAlignment.Left;
            foreach (var child in arrows.Children) { child.Width = M(NumberButtonSize); child.Height = M(NumberButtonSize); }
            field.SetCurrentValue(HeightProperty, stacked ? ControlHeight + M(NumberButtonSize) : NumberButtonsPlacement == PaperNumberButtonsPlacement.Hidden ? ControlHeight : Math.Max(ControlHeight, M(NumberButtonSize)));
        }
    }
    void NumberControls(Field field, double step, double minimum = -10000, Func<double>? current = null, Func<double>? increment = null)
    {
        var previous = field.Child; field.Child = null;
        var arrows = new StackPanel { Orientation = Orientation.Horizontal };
        void Step(int direction)
        {
            if (field.IsReadOnly || !double.TryParse(field.Text, NumberStyles.Float, Culture, out double n)) return;
            n = current?.Invoke() ?? n;
            field.Box.Focus(); field.Box.SetCurrentValue(TextBox.TextProperty, Math.Max(minimum, Math.Round(n + direction * (increment?.Invoke() ?? step), 6)).ToString(Culture));
        }
        foreach (int direction in new[] { -1, 1 })
        {
            var button = new IconButton { Icon = direction > 0 ? Icons.ChevronUp : Icons.ChevronDown, Width = M(NumberButtonSize), Height = M(NumberButtonSize), MinHeight = 0, MinWidth = 0, IconSize = M(16), Padding = new Thickness(0), Focusable = false };
            string label = direction > 0 ? "Increase" : "Decrease";
            Words(button, c => { AutomationProperties.SetName(c, T(label)); ToolTip.SetTip(c, T(label)); });
            button.Click += (_, _) => Step(direction); arrows.Children.Add(button);
        }
        var body = new DockPanel(); DockPanel.SetDock(arrows, Dock.Right); body.Children.Add(arrows); if (previous != null) body.Children.Add(previous); field.Child = body; numberLayouts.Add((field, arrows)); LayoutNumbers();
        field.Box.AddHandler(KeyDownEvent, (_, e) => { if (e.Key is Key.Up or Key.Down) { Step(e.Key == Key.Up ? 1 : -1); e.Handled = true; } }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        field.AddHandler(PointerWheelChangedEvent, (_, e) => { if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.Delta.Y == 0) return; Step(Math.Sign(e.Delta.Y)); e.Handled = true; }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }
}

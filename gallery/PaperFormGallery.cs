using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Automation;
using Avalonia.LogicalTree;
using Avalonia.Input;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Shade;
using static Shade.Look;

internal static class PaperFormGallery
{
    // The gallery acts as a host: this neutral paper palette belongs to the sample document, never to a control.
    internal static PaperFormDefinition Definition()
    {
        string ink = HexOf(Ink), paper = HexOf(White), rule = HexOf(Blend(White, Ink, .6));
        PaperStructure Input(string key, string kind, double x, double y, double w, double h, string label) => new() { FieldKey = key, Kind = kind, Bounds = new(x, y, w, h), Label = label, BorderColor = rule };
        return new PaperFormDefinition
        {
            ShowPageNumbers = true,
            Background = paper,
            Foreground = ink,
            Pages = new[] { new PaperPage { Guides = new[] { new PaperGuide("sample-vertical", PaperGuideOrientation.Vertical, 90), new PaperGuide("sample-horizontal", PaperGuideOrientation.Horizontal, 72) }, Structures = new[] {
            new PaperStructure { Kind = "label", Bounds = new(12,12,130,8), Title = "Equipment inspection", FontSize = 18, BorderWidth = 0 },
            Input("asset", "characters",12,32,64,8,"Asset identifier") with { Title = "Asset identifier", Alignment = TextAlignment.Center },
            Input("reading","characters",90,32,48,8,"Reading") with { Title = "Reading", GridAlignment = TextAlignment.Right, CellCount = 9, Alignment = TextAlignment.Center, CellRanges = new[] { new PaperCellRange(7,2,HexOf(Blend(White, Ink, .12))) } },
            Input("contact","caption",12,46,170,20,"Contact") with { Title = "Contact", Subtitle = "Name and location" },
            new PaperStructure { Kind = "line", Bounds = new(12, 152, 170, 2), BorderWidth = .3, BorderColor = rule },
            Input("date", "date", 12, 164, 60, 8, "Inspection date") with { Title = "Inspection date", DateFormat = "dd.MM.yyyy" },
            Input("notes","multiline",12,76,170,26,"Inspection notes"),
            Input("checked","check",12,112,5,5,"Inspection completed"),
            new PaperStructure { Kind = "label", Bounds = new(21,111,125,7), Title = "Inspection completed", BorderWidth = 0 },
            new PaperStructure { Kind = "group", Bounds = new(12,132,170,18), BorderColor = rule, Children = new[] {
                new PaperStructure { Kind = "label", Bounds = new(3,3,30,10), Title = "Reference", BorderWidth = 0 },
                Input("reference","text",36,3,85,10,"Reference")
            } }
        } } }
        };
    }
    internal static ObservableCollection<PaperFormValue> Values() => new() {
        new("asset") { Text = "0026018342" }, new("reading") { Text = "024581,36" },
        new("date") { Text = "29.10.2026" }, new("contact") { Text = "Jan Novák · Praha" }, new("notes") { Text = "Inspected on site.\nNo visible damage." }, new("checked") { Checked = true }, new("reference") { Text = "REF / 026" }
    };
    internal static PaperFormBuilder Builder() => new() { Definition = Definition(), Values = Values(), Zoom = .75, Height = 620 };
    internal static Window Window()
    {
        var builder = Builder(); builder.Height = double.NaN;
        var status = Build.Text("Choose Insert, then drag to draw. Click a selected group to pick a child; drag empty canvas to select several. Ctrl-click adds to selection.", Text3, TextSize.Caption);
        var mode = new ActionButton("Light / dark", ButtonKind.Ghost); mode.Click += (_, _) => SetMode(Current == Mode.Dark ? Mode.Light : Mode.Dark);
        var html = new ActionButton("Export HTML"); var pdf = new ActionButton("Export PDF");
        // Layout preferences are the host's: the gallery offers them here, the builder only exposes the properties.
        var properties = new ComboBox { ItemsSource = new[] { "Properties: sidebar", "Properties: ribbon", "Properties: left sidebar", "Properties: bottom ribbon" }, SelectedIndex = (int)builder.InspectorPlacement };
        properties.SelectionChanged += (_, _) => builder.InspectorPlacement = (PaperInspectorPlacement)properties.SelectedIndex;
        var pages = new ComboBox { ItemsSource = new[] { "Pages: vertical", "Pages: side by side" }, SelectedIndex = builder.SideBySidePages ? 1 : 0 };
        pages.SelectionChanged += (_, _) => builder.SideBySidePages = pages.SelectedIndex == 1;
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = M(8), Children = { mode, html, pdf, properties, pages } };
        var page = new DockPanel { Margin = new Thickness(M(16)) };
        DockPanel.SetDock(actions, Dock.Top); actions.Margin = new Thickness(0, 0, 0, M(12)); page.Children.Add(actions);
        DockPanel.SetDock(status, Dock.Bottom); page.Children.Add(status); page.Children.Add(builder);
        var window = new Window { Title = "Shade · Paper form builder", Width = 1280, Height = 900, Background = B(Look.Background), Content = page };
        async void Save(bool isPdf)
        {
            if (!await builder.ConfirmExport(window)) return;
            var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = isPdf ? "inspection.pdf" : "inspection.html", DefaultExtension = isPdf ? "pdf" : "html" });
            if (file == null) return;
            try
            {
                // Prepare first: an invalid form must not replace a previously saved document.
                using var prepared = new MemoryStream();
                if (isPdf) builder.ExportPdf(prepared);
                else { using var writer = new StreamWriter(prepared, leaveOpen: true); writer.Write(builder.ExportHtml()); writer.Flush(); }
                prepared.Position = 0;
                await using var stream = await file.OpenWriteAsync(); stream.SetLength(0); await prepared.CopyToAsync(stream);
                status.Text = "Exported " + file.Name;
            }
            catch (Exception e) { status.Text = e.Message; }
        }
        html.Click += (_, _) => Save(false); pdf.Click += (_, _) => Save(true); return window;
    }
    internal static void Previews(string directory)
    {
        Directory.CreateDirectory(directory);
        foreach (var mode in new[] { Mode.Dark, Mode.Light })
        {
            SetMode(mode); var builder = Builder();
            var window = new Window { Width = 1240, Height = 740, Background = B(Look.Background), Content = new Border { Padding = new Thickness(16), Child = builder } };
            window.Show(); Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs();
            builder.SelectedId = builder.Definition.Pages[0].Structures.First(s => s.Kind == "characters").Id;
            Dispatcher.UIThread.RunJobs();
            using (var image = new RenderTargetBitmap(new PixelSize(1240, 740))) { image.Render(window); image.Save(Path.Combine(directory, "paper-builder-" + mode.ToString().ToLowerInvariant() + ".png"), PngBitmapEncoderOptions.Default); }
            builder.Mode = PaperFormMode.Fill; builder.Zoom = 1; Dispatcher.UIThread.RunJobs();
            var reading = builder.GetVisualDescendants().OfType<CharacterGridField>().Skip(1).First(); reading.Focus(); reading.CaretIndex = 2;
            Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Dispatcher.UIThread.RunJobs();
            void State(string state)
            {
                var size = builder.Definition.PageSize;
                using var image = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(size.Width * 96 / 25.4), (int)Math.Ceiling(size.Height * 96 / 25.4)));
                image.Render(builder.Page); image.Save(Path.Combine(directory, "paper-" + state + "-" + mode.ToString().ToLowerInvariant() + ".png"), PngBitmapEncoderOptions.Default);
            }
            State("caret");
            reading.SelectionStart = 2; reading.SelectionEnd = 4; Dispatcher.UIThread.RunJobs(); State("selection");
            reading.CaretIndex = 2; reading.SelectionStart = reading.SelectionEnd = 2; Dispatcher.UIThread.RunJobs();
            using (var editing = new RenderTargetBitmap(new PixelSize(1240, 740))) { editing.Render(window); editing.Save(Path.Combine(directory, "paper-editing-" + mode.ToString().ToLowerInvariant() + ".png"), PngBitmapEncoderOptions.Default); }
            using (var image = builder.RenderPage(0, 96)) image.Save(Path.Combine(directory, "paper-page-" + mode.ToString().ToLowerInvariant() + ".png"), PngBitmapEncoderOptions.Default);
            File.WriteAllText(Path.Combine(directory, "paper-form-" + mode.ToString().ToLowerInvariant() + ".html"), builder.ExportHtml());
            using (var pdf = File.Create(Path.Combine(directory, "paper-form-" + mode.ToString().ToLowerInvariant() + ".pdf"))) builder.ExportPdf(pdf);
            void Capture(Control control, string name)
            {
                control.UpdateLayout(); Gallery.Settle(250);
                using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(control.Bounds.Width), (int)Math.Ceiling(control.Bounds.Height)));
                bitmap.Render(control); bitmap.Save(Path.Combine(directory, name + "-" + mode.ToString().ToLowerInvariant() + ".png"), PngBitmapEncoderOptions.Default);
            }
            builder.Mode = PaperFormMode.Design; Dispatcher.UIThread.RunJobs();
            var layerList = builder.GetVisualDescendants().OfType<ListBox>().Single(c => AutomationProperties.GetName(c) == "Page elements");
            layerList.ScrollIntoView(0); Dispatcher.UIThread.RunJobs();
            var layerRows = layerList.GetVisualDescendants().OfType<ListBoxItem>().Where(c => c.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Inspection completed")).ToArray();
            var targetRow = layerList.GetVisualDescendants().OfType<ListBoxItem>().First(c => c.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Group"));
            if (layerRows.Length > 0)
            {
                var from = layerRows[0].TranslatePoint(new Point(50, 14), window)!.Value; var to = targetRow.TranslatePoint(new Point(50, 3), window)!.Value;
                window.MouseDown(from, MouseButton.Left); window.MouseMove(from + new Vector(6, 0)); window.MouseMove(to); Capture(window, "paper-layer-drop");
                window.MouseMove(new Point(500, 5)); window.MouseUp(new Point(500, 5), MouseButton.Left); Dispatcher.UIThread.RunJobs();
            }
            builder.SelectedIds = new[] { builder.Definition.Pages[0].Structures.First(s => s.Kind == "characters").Id };
            builder.InspectorPlacement = PaperInspectorPlacement.Ribbon; Dispatcher.UIThread.RunJobs(); Capture(window, "paper-ribbon");
            builder.InspectorPlacement = PaperInspectorPlacement.Sidebar; builder.SelectedId = null;
            builder.Definition = builder.Definition with { Pages = builder.Definition.Pages.Append(new PaperPage()).ToArray() }; builder.SideBySidePages = true;
            Dispatcher.UIThread.RunJobs(); builder.FitTwoPages(); Dispatcher.UIThread.RunJobs(); Capture(window, "paper-spread");
            builder.SelectedId = builder.Definition.Pages[0].Structures.First(s => s.Kind == "characters").Id; Dispatcher.UIThread.RunJobs();
            var fonts = builder.GetVisualDescendants().OfType<SearchChoice>().Single(); fonts.BringIntoView(); Dispatcher.UIThread.RunJobs(); fonts.IsDropDownOpen = true; Dispatcher.UIThread.RunJobs();
            fonts.SearchText = "Segoe"; Dispatcher.UIThread.RunJobs();
            var fontPopup = fonts.GetVisualDescendants().OfType<Popup>().Single(); Capture(fontPopup.Child!, "paper-font-picker"); fonts.IsDropDownOpen = false;
            var colour = builder.GetVisualDescendants().OfType<ActionButton>().Single(c => AutomationProperties.GetName(c) == "Text colour"); colour.BringIntoView(); Dispatcher.UIThread.RunJobs();
            colour.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs();
            var popup = (window.FocusManager!.GetFocusedElement() as Control)!.GetLogicalAncestors().OfType<Popup>().Single(p => p.IsOpen);
            var palette = popup.Child!.GetVisualDescendants().OfType<ColorGrid>().Single(); var root = TopLevel.GetTopLevel(palette)!; var at = palette.TranslatePoint(new Point(40, 50), root)!.Value;
            root.MouseDown(at, MouseButton.Left); root.MouseUp(at, MouseButton.Left); Dispatcher.UIThread.RunJobs(); popup.IsOpen = false;
            colour.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs();
            popup = (window.FocusManager.GetFocusedElement() as Control)!.GetLogicalAncestors().OfType<Popup>().Single(p => p.IsOpen);
            Capture(popup.Child!, "paper-colour-picker"); popup.IsOpen = false;
            window.Close();
        }
        var layout = Definition();
        var multiple = new PaperFormView { Definition = layout with { Landscape = true, Pages = new[] { layout.Pages[0], new PaperPage() } }, Values = Values() };
        var scrollingLayout = layout with { Pages = layout.Pages.Select(p => p with { Structures = p.Structures.Select(s => s.Kind == "multiline" ? s with { AllowMultilineScrolling = true } : s).ToArray() }).ToArray() };
        File.WriteAllText(Path.Combine(directory, "paper-form-scrolling.html"), new PaperFormView { Definition = scrollingLayout, Values = Values() }.ExportHtml());
        using (var pdf = File.Create(Path.Combine(directory, "paper-two-pages.pdf"))) multiple.ExportPdf(pdf);
        SetMode(Mode.Dark);
    }
}

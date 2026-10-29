using System;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.VisualTree;
using Avalonia.LogicalTree;
using Avalonia.Headless;
using Avalonia.Controls;
using static Shade.Look;

namespace Shade.Tests;

static partial class Program
{
    // 0.12.0 review fixes: warnings never block export, paper ignores the shell mode, the saved layout names its enums.
    static void PaperFormRelease()
    {
        var a = new PaperStructure { Kind = PaperStructureKind.Text, FieldKey = "a", Label = "Serial", Bounds = new(20, 20, 40, 8) };
        var b = new PaperStructure { Kind = PaperStructureKind.Text, FieldKey = "b", Label = "Reading", Bounds = new(30, 22, 40, 8) };
        var outside = new PaperStructure { Kind = PaperStructureKind.Label, Title = "Footer note", Bounds = new(2, 2, 40, 10) };
        var definition = new PaperFormDefinition { Pages = new[] { new PaperPage { Structures = new[] { a, b, outside } }, new PaperPage() } };
        var view = new PaperFormView { Definition = definition };
        var window = new Window { Width = 1000, Height = 1200, Content = view }; window.Show(); Settle();
        view.RefreshProblems();
        Ok(view.Problems.Count == 2 && view.Problems.All(p => p.IsWarning), "overlap and outside-the-margin are warnings");
        Ok(view.Problems.Any(p => p.Message.Contains("Reading") && p.Message.Contains("Serial")) && view.Problems.Any(p => p.Message.Contains("Footer note")), "warnings name the structures involved");
        string? html = null; try { html = view.ExportHtml(); } catch (InvalidOperationException) { }
        Ok(html != null, "warnings still allow export");

        SetMode(Mode.Light); string light = view.ExportHtml(), paper = HexOf(Raised);
        SetMode(Mode.Dark); string dark = view.ExportHtml(), shell = HexOf(Raised);
        Ok(dark.Contains("background:" + paper) && !dark.Contains(shell) && !light.Contains(shell), "a form without colours exports white paper in dark mode too (only focus and selection follow the accent)");

        view.Values.Single(v => v.Key == "a").Text = new string('x', 400); Settle(); view.RefreshProblems();
        bool blocked = false; try { view.ExportHtml(); } catch (InvalidOperationException) { blocked = true; }
        Ok(blocked && view.Problems.Any(p => !p.IsWarning), "overflow is still an error that blocks export");

        string json = (definition with { Landscape = true }).ToJson();
        Ok(json.Contains("\"PaperFormat\": \"A4\"") && json.Contains("\"Alignment\": \"Left\"") && PaperFormDefinition.FromJson(json).Landscape, "the layout JSON stores enums by name and reads them back");
        bool named = false; try { PaperFormDefinition.FromJson(json.Replace("\"FieldKey\": \"b\"", "\"FieldKey\": \"a\"")); } catch (ArgumentException e) { named = e.Message.Contains("'a'") && e.Message.Contains("twice"); }
        Ok(named, "validation names the failing rule and key");
        window.Close();

        var builder = new PaperFormBuilder { Definition = definition };
        window = new Window { Width = 1400, Height = 1000, Content = builder }; window.Show(); Settle();
        builder.PageIndex = 1; builder.RemovePage(1); Settle();
        Ok(builder.Definition.Pages.Count == 1 && builder.PageIndex == 0 && builder.CanUndo, "a page can be removed");
        builder.Undo(); Settle();
        Ok(builder.Definition.Pages.Count == 2, "removing a page is undoable");
        builder.RemovePage(0); builder.RemovePage(0); Settle();
        Ok(builder.Definition.Pages.Count == 1, "the last page stays");
        builder.AddPage(); Settle(); var firstPage = builder.Definition.Pages[0]; builder.MovePage(0, 1); Settle();
        Ok(ReferenceEquals(builder.Definition.Pages[1], firstPage) && builder.PageIndex == 1, "a page can be moved");
        builder.Undo(); Settle(); Ok(ReferenceEquals(builder.Definition.Pages[0], firstPage), "moving a page is undoable");
        window.Close();

        // ribbon, View menu, layer problems, Guides tab
        builder = new PaperFormBuilder { Definition = definition with { Pages = new[] { definition.Pages[0] with { Guides = new[] { new PaperGuide("g1", PaperGuideOrientation.Vertical, 100), new PaperGuide("g2", PaperGuideOrientation.Horizontal, 150) } } } }, InspectorPlacement = PaperInspectorPlacement.Ribbon };
        window = new Window { Width = 1600, Height = 1000, Content = builder }; window.Show(); Settle();
        var ribbon = builder.GetVisualDescendants().OfType<ScrollViewer>().Single(v => v.Content is StackPanel p && p.Orientation == Avalonia.Layout.Orientation.Horizontal);
        double empty = ribbon.Bounds.Height;
        builder.SelectedId = a.Id; Settle(); double one = ribbon.Bounds.Height;
        builder.SelectedId = outside.Id; Settle();
        Ok(empty == one && one == ribbon.Bounds.Height && one < 120, "the property ribbon keeps one fixed height for page, field and label selections");
        Ok(!builder.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text?.StartsWith("Choose a structure") == true), "the ribbon has no placeholder sentence");
        Ok(!builder.GetVisualDescendants().OfType<CheckBox>().Any(c => c.Content?.ToString() is "Show grid" or "Page numbers in footer"), "view toggles and page numbers moved into the View menu");
        var viewButton = builder.GetVisualDescendants().OfType<ActionButton>().Single(b => AutomationProperties.GetName(b) == "View");
        viewButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Settle();
        var menu = (window.FocusManager!.GetFocusedElement() as Control)!.GetLogicalAncestors().OfType<Avalonia.Controls.Primitives.Popup>().Single(p => p.IsOpen);
        menu.Child!.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Page numbers").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Settle();
        Ok(builder.Definition.ShowPageNumbers && builder.CanUndo, "the View menu toggles printed page numbers as an undoable edit");
        var rows = builder.GetVisualDescendants().OfType<ListBox>().Single(l => AutomationProperties.GetName(l) == "Page elements").GetVisualDescendants().OfType<TextBlock>().ToArray();
        Ok(rows.Single(t => t.Text == "Reading").Foreground == B(Warn) && rows.Single(t => t.Text == "Footer note").Foreground == B(Warn) && rows.Single(t => t.Text == "Serial").Foreground != B(Warn), "layers mark structures with warnings in the warning colour");

        var guides = builder.GetVisualDescendants().OfType<ListBox>().Single(l => AutomationProperties.GetName(l) == "Page guides");
        builder.GetVisualDescendants().OfType<TextTabs>().Single().SetActive(1); Settle();
        var guideRows = guides.ItemsSource!.Cast<object>().ToArray();
        Ok(guides.IsEffectivelyVisible && guideRows.Length == 3, "the Guides tab lists the page and its guides");
        guides.SelectedItems!.Add(guideRows[1]); guides.SelectedItems.Add(guideRows[2]); Settle();
        Ok(guides.SelectedItems.Count == 2, "guides support multiple selection"); guides.ContainerFromIndex(1)!.Focus(); window.KeyPress(Avalonia.Input.Key.Delete, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.None, null); Settle();
        Ok(builder.Definition.Pages[0].Guides.Count == 0, "several selected guides are removed together");
        builder.Undo(); Settle();
        Ok(builder.Definition.Pages[0].Guides.Count == 2, "removing guides is one undo step");
        guides.SelectedItems!.Clear(); guides.SelectedItems.Add(guides.ItemsSource!.Cast<object>().ElementAt(2)); Settle();
        var position = builder.GetVisualDescendants().OfType<StackPanel>().Where(p => p.Children.Count >= 2 && p.Children[0] is TextBlock t && t.Text == "Position (mm)").Select(p => p.Children[1]).OfType<Field>().Single();
        position.Box.Focus(); position.Box.SelectAll(); window.KeyTextInput("120"); Settle();
        Ok(builder.Definition.Pages[0].Guides.Single(g => g.Id == "g1").Position == 120, "the Guides tab edits a selected guide's position");

        var page = builder.GetVisualDescendants().OfType<Canvas>().First(c => c.Width > 700);
        var at = page.TranslatePoint(new Point(120 * 96 / 25.4, 60 * 96 / 25.4), window)!.Value;
        window.MouseDown(at, Avalonia.Input.MouseButton.Left); window.MouseUp(at, Avalonia.Input.MouseButton.Left);
        window.MouseDown(at, Avalonia.Input.MouseButton.Left); window.MouseUp(at, Avalonia.Input.MouseButton.Left); Settle();
        Ok(builder.Definition.Pages[0].Guides.All(g => g.Id != "g1") && builder.Definition.Pages[0].Guides.Count == 1, "double-clicking a guide on the page removes it");
        window.Close();
        PaperFormBatchThree();
    }

    // right-aligned cells, titles, page-number formats, the date calendar, images and dropdown widths
    static void PaperFormBatchThree()
    {
        var grid = new CharacterGridField { CellCount = 8, CellSize = new Size(16, 20), Text = "123", TextAlignment = Avalonia.Media.TextAlignment.Right };
        var host = new Window { Width = 400, Height = 200, Content = grid }; host.Show(); Settle();
        var presenter = grid.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.TextPresenter>().Single();
        Ok(presenter.Margin.Left >= 5 * 16 - .5, "TextAlignment Right fills a character grid's last cells");
        grid.TextAlignment = Avalonia.Media.TextAlignment.Left; Settle();
        Ok(presenter.Margin.Left < 16, "left-aligned text starts in the first cell");
        host.Close();

        var serial = new PaperStructure { Kind = PaperStructureKind.Characters, FieldKey = "serial", Title = "Serial number", CellCount = 8, CellWidth = 4, GridAlignment = Avalonia.Media.TextAlignment.Right, Bounds = new(20, 30, 32, 8) };
        var when = new PaperStructure { Kind = PaperStructureKind.Date, FieldKey = "when", Title = "Date", DateFormat = "dd.MM.yyyy", Bounds = new(20, 50, 40, 8) };
        var picture = new PaperStructure { Kind = PaperStructureKind.Image, Label = "Logo", Bounds = new(100, 30, 40, 40) };
        var layout = new PaperFormDefinition { ShowPageNumbers = true, PageNumberPosition = PaperPageNumberPosition.TopRight, PageNumberFormat = PaperPageNumberFormat.NumberOfTotal, Pages = new[] { new PaperPage { Structures = new[] { serial, when, picture } } } };
        var builder = new PaperFormBuilder { Definition = layout, Mode = PaperFormMode.Fill };
        var window = new Window { Width = 1400, Height = 1000, Content = builder }; window.Show(); Settle();
        var page = builder.GetVisualDescendants().OfType<Canvas>().First(c => c.Width > 700);
        var title = page.Children.OfType<TextBlock>().Single(t => t.Text == "Serial number");
        Ok(title.TranslatePoint(new Point(0, title.Bounds.Height), page)!.Value.Y <= 30 * 96 / 25.4 + 1, "a character field's title prints just above its cells");
        var number = page.Children.OfType<TextBlock>().Single(t => t.Text == "1 / 1");
        Ok(Canvas.GetTop(number) == 0 && number.TextAlignment == Avalonia.Media.TextAlignment.Right, "page numbers take the chosen format and position (top right)");
        var html = builder.ExportHtml();
        Ok(html.Contains("data-align=\"right\"") && html.Contains(">Serial number</span>") && html.Contains("1 / 1") && html.Contains("top:0"), "HTML keeps right-aligned cells, titles and the page-number format and position");
        var dateBox = builder.GetVisualDescendants().OfType<TextBox>().Single(t => t.PlaceholderText == "dd.MM.yyyy");
        var calendar = dateBox.GetVisualDescendants().OfType<IconButton>().Single(b => AutomationProperties.GetName(b) == "Calendar");
        calendar.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Settle();
        var popup = (window.FocusManager!.GetFocusedElement() as Control)!.GetLogicalAncestors().OfType<Avalonia.Controls.Primitives.Popup>().Single(p => p.IsOpen);
        Ok(popup.Child!.GetVisualDescendants().Any(c => c.GetType().Name == "MonthGrid"), "a date field's calendar button opens the month");
        popup.IsOpen = false; builder.Mode = PaperFormMode.Preview; Settle();
        Ok(!builder.GetVisualDescendants().OfType<IconButton>().Any(b => AutomationProperties.GetName(b) == "Calendar"), "Preview and print have no calendar button");
        Ok(builder.Problems.Any(p => p.IsWarning && p.Message.Contains("Logo")), "an image without a picture is a named warning");

        // a PNG header saying 40 × 20 px (headless tests read the size; they do not decode pixels)
        var png = new byte[33]; new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R', 0, 0, 0, 40, 0, 0, 0, 20, 8, 6, 0, 0, 0 }.CopyTo(png, 0);
        string uri = "data:image/png;base64," + Convert.ToBase64String(png);
        builder.Mode = PaperFormMode.Design; builder.SelectedId = picture.Id; Settle();
        builder.ReplaceSelected(builder.Definition.Pages[0].Structures.Single(s => s.Id == picture.Id) with { Image = uri }); Settle();
        var placed = builder.Definition.Pages[0].Structures.Single(s => s.Id == picture.Id);
        Ok(Math.Abs(placed.Bounds.Height - placed.Bounds.Width / 2) < .05, "an image keeps its picture's proportions");
        builder.ReplaceSelected(placed with { Bounds = placed.Bounds with { Width = 60 } }); Settle();
        placed = builder.Definition.Pages[0].Structures.Single(s => s.Id == picture.Id);
        Ok(placed.Bounds.Width == 60 && Math.Abs(placed.Bounds.Height - 30) < .05, "resizing an image changes both sides");
        Ok(builder.GetVisualDescendants().OfType<Avalonia.Controls.Image>().Any(i => i.Stretch == Avalonia.Media.Stretch.Uniform) && builder.ExportHtml().Contains("<img alt=\"Logo\""), "the image shows on the page and in HTML");
        Ok(PaperFormDefinition.FromJson(builder.Definition.ToJson()).Pages[0].Structures.Single(s => s.Id == picture.Id).Image == uri, "the picture is saved in the layout");
        bool rejected = false; try { (builder.Definition with { Pages = new[] { new PaperPage { Structures = new[] { picture with { Image = "data:image/png;base64,PGI+" } } } } }).Validate(); } catch (ArgumentException) { rejected = true; }
        Ok(rejected, "only PNG, JPEG and SVG data is accepted");
        string Svg(string body) => "data:image/svg+xml;base64," + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(body));
        builder.ReplaceSelected(builder.Definition.Pages[0].Structures.Single(s => s.Id == picture.Id) with { Image = Svg("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 30 10'><rect width='30' height='10'/></svg>") }); Settle();
        placed = builder.Definition.Pages[0].Structures.Single(s => s.Id == picture.Id);
        Ok(placed.Image!.StartsWith("data:image/svg+xml") && Math.Abs(placed.Bounds.Height - placed.Bounds.Width / 3) < .05, "an SVG image takes its proportions from its viewBox");
        foreach (var unsafeSvg in new[] { "<svg><script>alert(1)</script></svg>", "<svg viewBox='0 0 1 1'><image href='file:///c:/secret.png'/></svg>", "<!DOCTYPE svg [<!ENTITY x SYSTEM 'file:///etc/passwd'>]><svg viewBox='0 0 1 1'/>", "<svg viewBox='0 0 1 1' onload='x()'/>" })
        {
            bool refused = false; try { (builder.Definition with { Pages = new[] { new PaperPage { Structures = new[] { picture with { Image = Svg(unsafeSvg) } } } } }).Validate(); } catch (ArgumentException) { refused = true; }
            Ok(refused, "an SVG that could run or fetch something is refused: " + unsafeSvg[..Math.Min(30, unsafeSvg.Length)]);
        }
        window.Close();

        // audit: CSS links in an SVG, a title over the top margin, PDF export without Skia
        bool cssLink = false; try { (layout with { Pages = new[] { new PaperPage { Structures = new[] { picture with { Image = Svg("<svg viewBox='0 0 1 1'><rect style='fill:url(http://example.com/x)'/></svg>") } } } } }).Validate(); } catch (ArgumentException) { cssLink = true; }
        Ok(cssLink, "an SVG with a CSS link to another file is refused");
        var top = new PaperFormView { Definition = layout with { Pages = new[] { new PaperPage { Structures = new[] { serial with { Bounds = serial.Bounds with { Y = 11 } } } } } } };
        top.RefreshProblems();
        Ok(top.Problems.Any(p => p.IsWarning && p.Message.Contains("title")), "a title above the printable area is a named warning");
        host = new Window { Width = 900, Height = 1200, Content = top }; host.Show(); Settle();
        string? pdfError = null; try { top.ExportPdf(new System.IO.MemoryStream()); } catch (InvalidOperationException e) { pdfError = e.Message; }
        Ok(pdfError?.Contains("UseSkia") == true, "PDF export without the Skia renderer says what it needs");
        host.Close();

        var named = new ComboBox { ItemsSource = new[] { new Choice("A"), new Choice("A much longer choice") }, DisplayMemberBinding = new Avalonia.Data.Binding(nameof(Choice.Name)), SelectedIndex = 0, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        host = new Window { Width = 600, Height = 200, Content = new StackPanel { Children = { named } } }; host.Show(); Settle();
        double longText = new Avalonia.Media.FormattedText("A much longer choice", System.Globalization.CultureInfo.InvariantCulture, Avalonia.Media.FlowDirection.LeftToRight, new Avalonia.Media.Typeface(named.FontFamily), named.FontSize, null).Width;
        Ok(named.Bounds.Width >= longText + 16 && named.Bounds.Width < longText + 120, "a dropdown with a display member sizes to the shown text, not the item type's name");
        host.Close();

        var combo = new ComboBox { ItemsSource = new[] { "A", "A much longer choice" }, SelectedIndex = 0, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        host = new Window { Width = 600, Height = 200, Content = new StackPanel { Children = { combo } } }; host.Show(); Settle();
        double wide = new Avalonia.Media.FormattedText("A much longer choice", System.Globalization.CultureInfo.InvariantCulture, Avalonia.Media.FlowDirection.LeftToRight, new Avalonia.Media.Typeface(combo.FontFamily), combo.FontSize, null).Width;
        Ok(combo.Bounds.Width >= wide + 16 && combo.Bounds.Width < wide + 120, "a closed dropdown is as wide as its widest item");
        host.Close();
    }
    sealed record Choice(string Name) { public override string ToString() => "Shade.Tests.Program+Choice { a very long type name for a closed dropdown }"; }
}

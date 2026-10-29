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
        var outside = new PaperStructure { Kind = PaperStructureKind.Label, Caption = "Footer note", Bounds = new(2, 2, 40, 10) };
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
        menu.Child!.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Page numbers in footer").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Settle();
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
    }
}

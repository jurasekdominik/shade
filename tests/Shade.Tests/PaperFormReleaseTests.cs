using System;
using System.Linq;
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
    }
}

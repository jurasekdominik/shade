using System;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;

namespace Shade.Tests;

static partial class Program
{
    static AutomationPeer[] PaperPeers(AutomationPeer peer) => new[] { peer }.Concat(peer.GetChildren().SelectMany(PaperPeers)).ToArray();
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static WeakReference<AutomationPeer> RetirePaperDocument(PaperFormView form)
    {
        var field = new PaperStructure { Id = "retired", FieldKey = "retired" };
        form.Definition = new() { Pages = new[] { new PaperPage { Structures = new[] { field } } } };
        var root = ControlAutomationPeer.CreatePeerForElement(form);
        var weak = new WeakReference<AutomationPeer>(PaperPeers(root).Single(p => p.GetAutomationId() == field.Id));
        form.Definition = new() { Pages = new[] { new PaperPage { Structures = new[] { field with { Id = "replacement", FieldKey = "replacement" } } } } };
        _ = PaperPeers(root); Settle(); return weak;
    }
    static void PaperFormAutomation()
    {
        var text = new PaperStructure { Id = "text", FieldKey = "text", Label = "Serial", Bounds = new(12, 20, 60, 8) };
        var check = new PaperStructure { Id = "check", FieldKey = "check", Kind = "check", Label = "Checked", Bounds = new(12, 40, 5, 5) };
        var cells = new PaperStructure { Id = "cells", FieldKey = "cells", Kind = "characters", CellCount = 4, Bounds = new(12, 60, 16, 8) };
        var other = text with { Id = "other", FieldKey = "other", Label = "Other page" };
        var form = new PaperFormView(); form.UsePainter(true);
        form.Definition = new() { Pages = new[] { new PaperPage { Structures = new[] { text, check, cells } }, new PaperPage { Structures = new[] { other } } } };
        var window = new Window { Width = 1100, Height = 1000, Content = form }; window.Show(); Settle();
        AutomationPeer[] AllPeers(AutomationPeer peer) => new[] { peer }.Concat(peer.GetChildren().SelectMany(AllPeers)).ToArray();
        var root = ControlAutomationPeer.CreatePeerForElement(form)!;
        AutomationPeer Find(string id) => AllPeers(root).Single(p => p.GetAutomationId() == id);
        Ok(AllPeers(root).Count(p => p.IsControlElement() && p.GetAutomationControlType() == AutomationControlType.Edit) == 3, "paper artwork prototypes are absent from the accessibility tree");
        var peer = Find(text.Id); var value = peer.GetProvider<IValueProvider>()!;
        Ok(peer.GetAutomationControlType() == AutomationControlType.Edit && peer.GetName() == "Serial" && !value.IsReadOnly && peer.GetBoundingRectangle().Width > 100, "drawn paper automation exposes field role, name, bounds and writable value");
        Ok(peer.ToScreen(peer.GetBoundingRectangle()) is { Width: > 100 }, "virtual paper peers supply the native screen conversion required by Windows UIA");
        var offscreen = Find(other.Id);
        Ok(offscreen.IsOffscreen() && offscreen.GetName() == "Other page", "paper automation exposes fields on an unrealized page");
        int notifications = 0;
        peer.PropertyChanged += (_, e) => { if (e.Property == ValuePatternIdentifiers.ValueProperty) notifications++; };
        form.Values.Single(v => v.Key == text.FieldKey).Text = "Host value"; Settle();
        Ok(value.Value == "Host value" && notifications == 1, "paper automation reports one value notification for a host update");
        var saved = form.Definition;
        form.Definition = saved with { Pages = new[] { saved.Pages[0] with { Structures = new[] { check, cells } }, saved.Pages[1] } };
        bool removed = !peer.IsEnabled();
        form.Definition = saved;
        Ok(removed && ReferenceEquals(peer, Find(text.Id)), "paper automation retains a removed field's identity when undo restores it");
        peer.SetFocus(); Settle();
        Ok(form.ActiveField == text.Id && peer.HasKeyboardFocus(), "paper automation focus uses the native editor activation path");
        var host = peer.GetChildren().Single(); var input = host.GetChildren().Single();
        Ok(ReferenceEquals(host.GetParent(), peer) && ReferenceEquals(input.GetParent(), host) && input.GetProvider<IValueProvider>() != null, "the active native text automation subtree remains connected beneath its virtual field");
        value.SetValue("Automation"); Settle();
        Ok(form.Values.Single(v => v.Key == text.FieldKey).Text == "Automation" && ReferenceEquals(peer, Find(text.Id)), "paper automation edits keep a stable virtual field identity");
        form.Values.Single(v => v.Key == text.FieldKey).Error = "Correct serial"; Settle();
        Ok(peer.GetHelpText() == "Correct serial" && peer.GetItemStatus() == "Correct serial", "paper automation exposes host validation errors");
        var tick = Find(check.Id); tick.GetProvider<IToggleProvider>()!.Toggle(); Settle();
        Ok(form.ActiveField == check.Id && tick.GetAutomationControlType() == AutomationControlType.CheckBox && tick.GetProvider<IValueProvider>() == null && tick.GetProvider<IToggleProvider>()!.ToggleState == ToggleState.On, "paper checkbox automation activates and toggles the native checkbox");
        Find(cells.Id).GetProvider<IValueProvider>()!.SetValue("12345"); Settle();
        Ok(form.Values.Single(v => v.Key == cells.FieldKey).Text == "12345" && form.Problems.Any(p => p.StructureId == cells.Id && !p.IsWarning)
            && Find(cells.Id).GetHelpText() is { Length: > 0 }, "paper automation retains and reports over-capacity values like the native character grid");
        offscreen.SetFocus(); Settle();
        Ok(form.PageIndex == 1 && form.ActiveField == other.Id && !offscreen.IsOffscreen() && offscreen.HasKeyboardFocus(), "paper automation focus realizes and reveals another page");
        form.Mode = PaperFormMode.Preview; peer = Find(text.Id); value = peer.GetProvider<IValueProvider>()!;
        bool rejected = false; try { value.SetValue("Forbidden"); } catch (InvalidOperationException) { rejected = true; }
        peer.SetFocus(); Settle();
        Ok(rejected && value.IsReadOnly && form.ActiveField == text.Id && peer.HasKeyboardFocus(), "drawn Preview automation keeps read-only values focusable for native text selection");
        rejected = false; try { Find(check.Id).GetProvider<IToggleProvider>()!.Toggle(); } catch (InvalidOperationException) { rejected = true; }
        Ok(rejected, "drawn Preview automation cannot toggle a read-only checkbox");
        form.IsEnabled = false; Settle();
        rejected = false; try { peer.SetFocus(); } catch (ElementNotEnabledException) { rejected = true; }
        Ok(rejected && !peer.IsEnabled() && form.ActiveField == null, "disabled paper automation cannot activate an editor");
        var lifetime = new PaperFormView(); lifetime.UsePainter(true); window.Content = lifetime; Settle();
        var retired = RetirePaperDocument(lifetime);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        Ok(!retired.TryGetTarget(out _), "replacing a document releases retired native coordinate peers while the form remains alive");
        window.Close();
    }
}

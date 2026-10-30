using System;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Shade.Tests;

static partial class Program
{
    static byte[] PaperPixels(RenderTargetBitmap bitmap)
    {
        var data = new byte[bitmap.PixelSize.Width * bitmap.PixelSize.Height * 4];
        var memory = Marshal.AllocHGlobal(data.Length);
        try { bitmap.CopyPixels(new PixelRect(bitmap.PixelSize), memory, data.Length, bitmap.PixelSize.Width * 4); Marshal.Copy(memory, data, 0, data.Length); }
        finally { Marshal.FreeHGlobal(memory); }
        return data;
    }
    static (int different, int maximum) PaperDifference(byte[] original, byte[] painted, int width)
    {
        int height = original.Length / 4 / width;
        bool Edge(int pixel)
        {
            int x = pixel % width, y = pixel / width;
            foreach (int neighbour in new[] { x > 0 ? pixel - 1 : pixel, x < width - 1 ? pixel + 1 : pixel, y > 0 ? pixel - width : pixel, y < height - 1 ? pixel + width : pixel })
                for (int c = 0; c < 4; c++)
                    if (Math.Abs(original[pixel * 4 + c] - original[neighbour * 4 + c]) >= 3 || Math.Abs(painted[pixel * 4 + c] - painted[neighbour * 4 + c]) >= 3) return true;
            return false;
        }
        int different = 0, maximum = 0;
        for (int i = 0; i < original.Length; i++)
        {
            int difference = Math.Abs(original[i] - painted[i]); maximum = Math.Max(maximum, difference);
            if (difference > 2 && (difference > 64 || !Edge(i / 4))) different++;
        }
        return (different, maximum);
    }
    static int PaperParity(string? filter = null, bool highDpi = false)
    {
        Directory.CreateDirectory("out/paper-parity");
        using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(30, 10));
        surface.Canvas.Clear(SkiaSharp.SKColors.CadetBlue);
        using var raster = surface.Snapshot(); using var encoded = raster.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        string png = "data:image/png;base64," + Convert.ToBase64String(encoded.ToArray());
        const int width = 480, height = 280;
        var view = new PaperFormView();
        var window = new Window { Width = 1000, Height = 1300, Content = view }; window.Show(); Settle();
        int compared = 0;
        foreach (double dpi in highDpi ? new[] { 1.25, 1.5, 2 } : new[] { 1d })
        {
            // The headless platform's DPI callback is omitted from Avalonia's reference API.
            var scalingChanged = (Action<double>)window.PlatformImpl!.GetType().GetProperty("ScalingChanged")!.GetValue(window.PlatformImpl)!;
            scalingChanged(dpi); window.UpdateLayout();
            foreach (var mode in new[] { Look.Mode.Dark, Look.Mode.Light })
            {
                Look.SetMode(mode);
                foreach (var paperMode in new[] { PaperFormMode.Design, PaperFormMode.Preview })
                    foreach (double zoom in new[] { .75, 1, 1.5 })
                        foreach (string kind in new[] { "text", "characters", "multiline", "caption", "check", "date", "line", "label", "group", "image" })
                            foreach (string state in new[] { "empty", "filled", "overflow", "error", "right", "center", "decorated" })
                            {
                                string name = $"{mode}-{paperMode}-{zoom.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}-{kind}-{state}-dpi{dpi.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                                if (filter != null && !name.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
                                compared++;
                                var structure = new PaperStructure
                                {
                                    Id = "field",
                                    FieldKey = PaperStructureKind.HasValue(kind) ? "value" : "",
                                    Kind = kind,
                                    Title = state == "overflow" && kind is "label" or "caption" or "group" ? string.Join(" ", Enumerable.Repeat("Long title", 30)) : "Title",
                                    Subtitle = "Subtitle",
                                    Bounds = new(12, 14, kind == "check" ? 8 : 60, kind is "caption" or "multiline" or "image" ? 24 : 8),
                                    CellCount = 12,
                                    CellWidth = 4,
                                    Alignment = state == "right" ? TextAlignment.Right : state == "center" ? TextAlignment.Center : TextAlignment.Left,
                                    GridAlignment = state == "right" ? TextAlignment.Right : state == "center" ? TextAlignment.Center : TextAlignment.Left,
                                    LineOrientation = state == "right" ? Avalonia.Layout.Orientation.Vertical : Avalonia.Layout.Orientation.Horizontal,
                                    Image = kind == "image" && state != "empty" ? state == "decorated" ? png : "data:image/svg+xml;base64," + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 30 10'><rect width='30' height='10' fill='#336699'/><circle cx='15' cy='5' r='4' fill='#eecc55'/></svg>")) : null,
                                    KeepAspectRatio = state != "right",
                                    CellRanges = kind == "characters" && state == "decorated" ? new[] { new PaperCellRange(2, 2, "#DDDDDD", "#331122") } : Array.Empty<PaperCellRange>()
                                };
                                var value = new PaperFormValue("value") { Text = state == "empty" ? "" : state == "overflow" ? string.Join(" ", Enumerable.Repeat("Value 0123", 30)) : kind == "date" ? "2026-10-29" : "Value 0123", Checked = state == "empty" ? false : state == "center" ? null : true, Error = state == "error" ? "Error" : null };
                                bool shared = state == "filled" && PaperStructureKind.HasValue(kind);
                                var structures = shared ? new[] { structure, structure with { Id = "second", FieldKey = "second", Title = "Another title", Bounds = structure.Bounds with { Y = structure.Bounds.Y + 34 } } } : new[] { structure };
                                var values = shared ? new[] { value, new PaperFormValue("second") { Text = value.Text, Checked = value.Checked } } : new[] { value };
                                view.UsePainter(false); view.StructureTypes = null;
                                if (kind == "group" && state == "filled")
                                {
                                    var children = Enumerable.Range(0, 70).Select(i => new PaperStructure
                                    {
                                        Id = "child" + i,
                                        FieldKey = i == 66 ? "" : "child" + i,
                                        Kind = i == 66 ? "custom" : "text",
                                        Title = "Title " + i,
                                        Bounds = i == 66 ? new(0, 0, 20, 8) : i > 66 ? new(10, 4, 20, 8) : new(i % 8 * 6, i / 8 * 4, 10, 4)
                                    }).ToArray();
                                    children[10] = new() { Id = "nested", Kind = "group", Bounds = new(12, 4, 20, 12), Children = new[] { children[10] with { Bounds = new(0, 2, 12, 4) } } };
                                    structures = new[] { structure with { Bounds = structure.Bounds with { Height = 48 }, Children = children } };
                                    values = PaperFormView.All(children).Where(s => s.FieldKey.Length > 0).Select(s => new PaperFormValue(s.FieldKey) { Text = "Value" }).ToArray();
                                    view.StructureTypes = new[] { new PaperStructureTemplate("custom", "Custom", (_, _, _) => new Border { Background = Look.B(Look.Accent), Child = new TextBlock { Text = "Custom", Foreground = Look.B(Look.Text) } }, (_, _) => "Custom") };
                                }
                                view.Values = values.ToList();
                                view.Definition = new() { ShowPageNumbers = state == "decorated", PageNumberPosition = PaperPageNumberPosition.TopLeft, Pages = new[] { new PaperPage { Structures = structures } } }; view.Mode = paperMode; view.Zoom = zoom;
                                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                                using var live = new RenderTargetBitmap(new PixelSize(width, height)); live.Render((Visual)view.Content!);
                                view.UsePainter(true); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                                using var drawn = new RenderTargetBitmap(new PixelSize(width, height)); drawn.Render((Visual)view.Content!);
                                var (different, maximum) = PaperDifference(PaperPixels(live), PaperPixels(drawn), width);
                                if (different > 0) { live.Save($"out/paper-parity/{name}-live.png", PngBitmapEncoderOptions.Default); drawn.Save($"out/paper-parity/{name}-drawn.png", PngBitmapEncoderOptions.Default); }
                                Ok(different == 0, $"{name}: live/drawn pixels match (2/255 interiors, 64/255 at antialiased edges; different channels {different}, max {maximum})");
                            }
            }
        }
        if (filter == null) PaperExportLifecycle();
        Ok(compared > 0, $"paper parity exercised {compared} cases");
        window.Close(); return failed == 0 ? 0 : 1;
    }

    static void PaperExportLifecycle()
    {
        int attached = 0, detached = 0;
        bool fail = false;
        var windows = new System.Collections.Generic.HashSet<TopLevel>();
        var template = new PaperStructureTemplate("export-test", "Export test", (structure, _, readOnly) =>
        {
            if (readOnly && fail && structure.Id == "second") throw new InvalidOperationException("Custom export failed");
            var text = new TextBlock { Text = "Before attachment", Foreground = Look.B(Look.PaperInk) };
            var control = new Border { Background = Look.B(Look.Paper), Child = text };
            if (readOnly)
            {
                control.AttachedToVisualTree += (_, _) => { attached++; windows.Add(TopLevel.GetTopLevel(control)!); text.Text = "Attached export"; };
                control.DetachedFromVisualTree += (_, _) => detached++;
            }
            return control;
        }, (_, _) => "Custom export");
        var first = new PaperStructure { Id = "first", Kind = template.Kind, Bounds = new(20, 20, 50, 10) };
        var second = first with { Id = "second" };
        var source = new PaperFormView { StructureTypes = new[] { template }, Definition = new() { Pages = new[] { new PaperPage { Structures = new[] { first } }, new PaperPage { Structures = new[] { second } } } } };
        using (var bitmap = source.RenderPage(0, 96))
            Ok(attached == 1 && detached == 1 && windows.Count == 1 && windows.All(w => !w.IsVisible), "custom PNG export receives native attachment and closes its hidden window");
        attached = detached = 0; windows.Clear();
        using (var pdf = new MemoryStream())
        {
            source.ExportPdf(pdf);
            File.WriteAllBytes("out/paper-parity/custom-export.pdf", pdf.ToArray());
            Ok(pdf.Length > 0 && attached == 2 && detached == 2 && windows.Count == 1 && windows.All(w => !w.IsVisible), "multipage custom PDF export reuses one window and detaches every native control");
        }
        attached = detached = 0; windows.Clear(); fail = true;
        using var destination = new MemoryStream(); destination.WriteByte(42);
        bool rejected = false;
        try { source.ExportPdf(destination); } catch (InvalidOperationException error) { rejected = error.Message == "Custom export failed"; }
        Ok(rejected && destination.Length == 1 && destination.ToArray()[0] == 42 && attached == detached && windows.All(w => !w.IsVisible), "a later custom export failure closes its window and leaves the destination untouched");
    }
}

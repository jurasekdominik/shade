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
    static int PaperParity(string? filter = null, bool highDpi = false)
    {
        Directory.CreateDirectory("out/paper-parity");
        byte[] Pixels(RenderTargetBitmap bitmap)
        {
            var data = new byte[bitmap.PixelSize.Width * bitmap.PixelSize.Height * 4];
            var memory = Marshal.AllocHGlobal(data.Length);
            try { bitmap.CopyPixels(new PixelRect(bitmap.PixelSize), memory, data.Length, bitmap.PixelSize.Width * 4); Marshal.Copy(memory, data, 0, data.Length); }
            finally { Marshal.FreeHGlobal(memory); }
            return data;
        }
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
                                view.UsePainter(false); view.Values = new[] { value };
                                view.Definition = new() { ShowPageNumbers = state == "decorated", PageNumberPosition = PaperPageNumberPosition.TopLeft, Pages = new[] { new PaperPage { Structures = new[] { structure } } } }; view.Mode = paperMode; view.Zoom = zoom;
                                Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                                using var live = new RenderTargetBitmap(new PixelSize(width, height)); live.Render((Visual)view.Content!);
                                view.UsePainter(true); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                                using var drawn = new RenderTargetBitmap(new PixelSize(width, height)); drawn.Render((Visual)view.Content!);
                                var original = Pixels(live); var painted = Pixels(drawn);
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
                                if (different > 0) { live.Save($"out/paper-parity/{name}-live.png", PngBitmapEncoderOptions.Default); drawn.Save($"out/paper-parity/{name}-drawn.png", PngBitmapEncoderOptions.Default); }
                                Ok(different == 0, $"{name}: live/drawn pixels match (2/255 interiors, 64/255 at antialiased edges; different channels {different}, max {maximum})");
                            }
            }
        }
        Ok(compared > 0, $"paper parity exercised {compared} cases");
        window.Close(); return failed == 0 ? 0 : 1;
    }
}

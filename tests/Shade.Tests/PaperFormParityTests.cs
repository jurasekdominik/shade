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
    static int PaperParity()
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
        var view = new PaperFormView();
        var window = new Window { Width = 1000, Height = 1300, Content = view }; window.Show(); Settle();
        foreach (var mode in new[] { Look.Mode.Dark, Look.Mode.Light })
        {
            Look.SetMode(mode);
            foreach (string kind in new[] { "text", "characters", "multiline", "caption", "check", "date", "line", "label", "group", "image" })
                foreach (string state in new[] { "empty", "filled", "error" })
                {
                    var structure = new PaperStructure
                    {
                        Id = "field",
                        FieldKey = PaperStructureKind.HasValue(kind) ? "value" : "",
                        Kind = kind,
                        Title = "Title",
                        Subtitle = "Subtitle",
                        Bounds = new(12, 14, kind == "check" ? 8 : 60, kind is "caption" or "multiline" or "image" ? 24 : 8),
                        CellCount = 12,
                        CellWidth = 4,
                        Alignment = TextAlignment.Center,
                        CellRanges = kind == "characters" ? new[] { new PaperCellRange(2, 2, "#DDDDDD", "#331122") } : Array.Empty<PaperCellRange>()
                    };
                    var value = new PaperFormValue("value") { Text = state == "empty" ? "" : kind == "date" ? "2026-10-29" : "Value 0123", Checked = state == "empty" ? false : true, Error = state == "error" ? "Error" : null };
                    view.UsePainter(false); view.Values = new[] { value };
                    view.Definition = new() { Pages = new[] { new PaperPage { Structures = new[] { structure } } } }; view.Mode = PaperFormMode.Preview;
                    Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                    using var live = new RenderTargetBitmap(new PixelSize(360, 200)); live.Render((Visual)view.Content!);
                    view.UsePainter(true); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                    using var drawn = new RenderTargetBitmap(new PixelSize(360, 200)); drawn.Render((Visual)view.Content!);
                    var original = Pixels(live); var painted = Pixels(drawn);
                    bool Edge(int pixel)
                    {
                        int x = pixel % 360, y = pixel / 360;
                        foreach (int neighbour in new[] { x > 0 ? pixel - 1 : pixel, x < 359 ? pixel + 1 : pixel, y > 0 ? pixel - 360 : pixel, y < 199 ? pixel + 360 : pixel })
                            for (int c = 0; c < 4; c++)
                                if (Math.Abs(original[pixel * 4 + c] - original[neighbour * 4 + c]) >= 8 || Math.Abs(painted[pixel * 4 + c] - painted[neighbour * 4 + c]) >= 8) return true;
                        return false;
                    }
                    int different = 0, maximum = 0;
                    for (int i = 0; i < original.Length; i++)
                    {
                        int difference = Math.Abs(original[i] - painted[i]); maximum = Math.Max(maximum, difference);
                        if (difference > 2 && (difference > 64 || !Edge(i / 4))) different++;
                    }
                    string name = $"{mode}-{kind}-{state}";
                    if (different > 0) { live.Save($"out/paper-parity/{name}-live.png", PngBitmapEncoderOptions.Default); drawn.Save($"out/paper-parity/{name}-drawn.png", PngBitmapEncoderOptions.Default); }
                    Ok(different == 0, $"{name}: live/drawn pixels match (2/255 interiors, 64/255 at antialiased edges; different channels {different}, max {maximum})");
                }
        }
        window.Close(); return failed == 0 ? 0 : 1;
    }
}

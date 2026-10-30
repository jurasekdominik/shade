using System;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Shade.Tests;

static partial class Program
{
    static int PaperExportParity()
    {
        Directory.CreateDirectory("out/paper-export-parity");
        string svg = "data:image/svg+xml;base64," + Convert.ToBase64String(Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 30 10'><rect width='30' height='10' fill='#336699'/><circle cx='15' cy='5' r='4' fill='#eecc55'/></svg>"));
        int compared = 0;
        foreach (var mode in new[] { Look.Mode.Dark, Look.Mode.Light })
            foreach (var alignment in new[] { TextAlignment.Left, TextAlignment.Center, TextAlignment.Right })
                foreach (bool custom in new[] { false, true })
                {
                    Look.SetMode(mode);
                    var kinds = new[] { "label", "text", "characters", "multiline", "caption", "check", "date", "line", "group", "image" };
                    var structures = kinds.Select((kind, i) => new PaperStructure
                    {
                        Id = kind,
                        FieldKey = PaperStructureKind.HasValue(kind) ? kind : "",
                        Kind = kind,
                        Title = "Title",
                        Subtitle = "Subtitle",
                        GridAlignment = alignment,
                        CellCount = 12,
                        CellWidth = 4,
                        Bounds = new(12, 14 + i * 24, kind == "check" ? 8 : 60, kind is "multiline" or "caption" or "group" or "image" ? 20 : 8),
                        Image = kind == "image" ? svg : null,
                        CellRanges = kind == "characters" ? new[] { new PaperCellRange(2, 2, "#DDDDDD", "#331122") } : Array.Empty<PaperCellRange>(),
                        Children = kind == "group" ? new[] { new PaperStructure { Id = "nested", FieldKey = "nested", Bounds = new(3, 8, 36, 8) } } : Array.Empty<PaperStructure>()
                    }).ToList();
                    var templates = custom ? new[] { new PaperStructureTemplate("custom-export", "Custom", (_, _, _) =>
            {
                var text = new TextBlock { Text = "Before attachment", FontFamily = PaperFormView.DocumentFont, FontSize = 12, Foreground = Look.B(Look.PaperInk) };
                var root = new Border { Background = Look.B(Look.Accent), Child = text };
                root.AttachedToVisualTree += (_, _) => text.Text = "Attached custom"; return root;
            }, (_, _) => "Custom") } : Array.Empty<PaperStructureTemplate>();
                    if (custom) structures.Insert(4, new PaperStructure { Id = "custom", Kind = "custom-export", Bounds = new(20, 96, 40, 20) });
                    var view = new PaperFormView { Mode = PaperFormMode.Preview, StructureTypes = templates, Definition = new() { Pages = new[] { new PaperPage { Structures = structures } } } };
                    view.UsePainter(false);
                    foreach (var value in view.Values) value.Text = value.Key == "date" ? "2026-10-29" : value.Key == "multiline" ? "First\nSecond" : "001234";
                    view.Values.Single(v => v.Key == "check").Checked = true;
                    var window = new Window { Width = 1000, Height = 1300, Content = view }; window.Show(); Settle();
                    foreach (int dpi in new[] { 96, 144 })
                    {
                        string name = $"{mode}-{alignment}-custom{custom}-dpi{dpi}";
                        using var exported = view.RenderPage(0, dpi);
                        using var live = new RenderTargetBitmap(exported.PixelSize, new Vector(dpi, dpi)); live.Render(view.Page);
                        var (different, maximum) = PaperDifference(PaperPixels(live), PaperPixels(exported), live.PixelSize.Width);
                        if (different > 0) { live.Save($"out/paper-export-parity/{name}-native.png", PngBitmapEncoderOptions.Default); exported.Save($"out/paper-export-parity/{name}-export.png", PngBitmapEncoderOptions.Default); }
                        Ok(different == 0, $"paper export {name}: {different} different channels, max {maximum}"); compared++;
                    }
                    window.Close();
                }
        Ok(compared == 24, $"paper export parity exercised {compared} cases"); return failed == 0 ? 0 : 1;
    }
}

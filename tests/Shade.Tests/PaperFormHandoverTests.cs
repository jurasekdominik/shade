using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

namespace Shade.Tests;

static partial class Program
{
    static int PaperHandover(bool highDpi)
    {
        Directory.CreateDirectory("out/paper-handover");
        var view = new PaperFormView(); var outside = new ActionButton("Outside");
        var window = new Window { Width = 1000, Height = 1300, Content = new StackPanel { Children = { view, outside } } }; window.Show(); Settle();
        int compared = 0;
        foreach (double dpi in highDpi ? new[] { 1.25, 1.5, 2 } : new[] { 1d })
        {
            ((Action<double>)window.PlatformImpl!.GetType().GetProperty("ScalingChanged")!.GetValue(window.PlatformImpl)!)(dpi);
            foreach (var mode in new[] { Look.Mode.Dark, Look.Mode.Light })
                foreach (var paperMode in new[] { PaperFormMode.Fill, PaperFormMode.Preview })
                    foreach (double zoom in new[] { .75, 1, 1.5 })
                        foreach (string kind in new[] { "text", "characters", "multiline", "caption", "check", "date" })
                            foreach (var align in kind == "characters" ? new[] { TextAlignment.Left, TextAlignment.Center, TextAlignment.Right } : new[] { TextAlignment.Left })
                                foreach (string state in new[] { "caret", "selection", "unfocused", "error" })
                                {
                                    Look.SetMode(mode);
                                    string name = FormattableString.Invariant($"{mode}-{paperMode}-{zoom}-{kind}-{align}-{state}-dpi{dpi}");
                                    var structure = new PaperStructure
                                    {
                                        Id = "field",
                                        Kind = kind,
                                        FieldKey = "value",
                                        Title = "Title",
                                        Subtitle = "Subtitle",
                                        CellCount = 12,
                                        CellWidth = 4,
                                        GridAlignment = align,
                                        Bounds = new(12, 14, kind == "check" ? 8 : 60, kind is "caption" or "multiline" ? 24 : 8),
                                        CellRanges = kind == "characters" ? new[] { new PaperCellRange(2, 2, "#DDDDDD", "#331122") } : Array.Empty<PaperCellRange>()
                                    };
                                    view.UsePainter(false); view.Values = new[] { new PaperFormValue("value") { Text = kind == "date" ? "2026-10-29" : "001234", Checked = true, Error = state == "error" ? "Error" : null } };
                                    view.Definition = new() { Pages = new[] { new PaperPage { Structures = new[] { structure } } } }; view.Mode = paperMode; view.Zoom = zoom; Settle();
                                    void Focus(Control input)
                                    {
                                        var box = input.GetSelfAndVisualDescendants().OfType<TextBox>().FirstOrDefault();
                                        if (box != null)
                                        {
                                            box.CaretBlinkInterval = TimeSpan.FromHours(1); box.Focus(NavigationMethod.Tab); box.CaretIndex = 3;
                                            box.SelectionStart = state is "selection" or "unfocused" ? 1 : 3; box.SelectionEnd = state is "selection" or "unfocused" ? 4 : 3;
                                        }
                                        else input.Focus(NavigationMethod.Tab);
                                        if (state == "unfocused") outside.Focus();
                                        Settle();
                                    }
                                    var native = view.Pages[0].Children.First(c => c is TextBox or CheckBox or Border or DockPanel);
                                    Focus(native);
                                    if (state == "unfocused" && native.GetSelfAndVisualDescendants().OfType<TextBox>().FirstOrDefault() is { } inactive)
                                        Ok(!inactive.IsFocused && (!inactive.IsInactiveSelectionHighlightEnabled || inactive.SelectionStart == inactive.SelectionEnd), "native unfocused paper selection is not highlighted: " + name);
                                    using var live = new RenderTargetBitmap(new PixelSize(480, 280)); live.Render((Visual)view.Content!);
                                    view.UsePainter(true); Settle(); var active = view.ActivateEditor(structure.Id, NavigationMethod.Tab)!; Focus(active);
                                    if (state == "unfocused") Ok(view.ActiveField == null, "paper handover releases the unfocused native editor: " + name);
                                    using var pooled = new RenderTargetBitmap(new PixelSize(480, 280)); pooled.Render((Visual)view.Content!);
                                    var (different, maximum) = PaperDifference(PaperPixels(live), PaperPixels(pooled), 480);
                                    if (different > 0) { live.Save($"out/paper-handover/{name}-native.png", PngBitmapEncoderOptions.Default); pooled.Save($"out/paper-handover/{name}-pooled.png", PngBitmapEncoderOptions.Default); }
                                    Ok(different == 0, $"paper handover {name}: {different} different channels, max {maximum}"); compared++;
                                }
        }
        window.Close(); Ok(compared > 0, $"paper handover exercised {compared} cases"); return failed == 0 ? 0 : 1;
    }
}

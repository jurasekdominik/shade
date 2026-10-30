using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.VisualTree;
using static Shade.PaperFormDefinition;

namespace Shade;

// Reuse native layout and artwork, retaining only vector drawings instead of a control tree for every field.
internal sealed class PaperPainter : IDisposable
{
    readonly PaperFormView owner;
    internal int RecordingCount { get; private set; }
    internal readonly Canvas Templates = new PrototypeCanvas() { Width = 0, Height = 0, Opacity = 0, IsHitTestVisible = false, ClipToBounds = true, ZIndex = int.MinValue };
    sealed class PrototypeCanvas : Canvas
    {
        protected override Avalonia.Automation.Peers.AutomationPeer OnCreateAutomationPeer() => new PrototypePeer(this);
        sealed class PrototypePeer(Control owner) : Avalonia.Automation.Peers.ControlAutomationPeer(owner)
        {
            protected override bool IsContentElementCore() => false;
            protected override bool IsControlElementCore() => false;
            protected override IReadOnlyList<Avalonia.Automation.Peers.AutomationPeer> GetChildrenCore() => Array.Empty<Avalonia.Automation.Peers.AutomationPeer>();
        }
    }
    internal bool Attached => TopLevel.GetTopLevel(Templates) != null;
    internal PaperPainter(PaperFormView owner) { this.owner = owner; Avalonia.Input.KeyboardNavigation.SetTabNavigation(Templates, Avalonia.Input.KeyboardNavigationMode.None); }
    readonly Dictionary<string, Prototype> prototypes = new(StringComparer.Ordinal);
    readonly Dictionary<string, Snapshot> drawings = new(StringComparer.Ordinal);
    readonly record struct PaintKey(PaperStructure Style, string? Text, string? Error, bool? Checked, bool Hover, PaperFormMode Mode);
    readonly Dictionary<PaintKey, (Snapshot drawing, int users)> recordings = new();
    readonly Dictionary<string, PaintKey> drawingKeys = new(StringComparer.Ordinal);
    readonly TextBlock title = new() { IsHitTestVisible = false };
    readonly TextBlock number = new() { IsHitTestVisible = false };
    readonly Dictionary<int, (object key, Point at, DrawingGroup drawing, GlyphRun[] glyphs)> numbers = new();
    static readonly IReadOnlyDictionary<string, string> NoProperties = new Dictionary<string, string>();
    sealed record Prototype(PaperStructure Style, PaperFormMode Mode, PaperFormValue Value, Control Control);
    sealed record Snapshot(PaperStructure Source, string? Text, string? Error, bool? Checked, bool Hover, DrawingGroup Drawing, DrawingGroup? Title, double TitleHeight, GlyphRun[] Glyphs, IImage? Image, Rect ImageBounds, PaintKey? Body = null)
    {
        internal void Dispose() { foreach (var glyph in Glyphs) glyph.Dispose(); }
    }
    PaintKey Key(PaperStructure structure, PaperFormValue? value, bool hover)
    {
        var style = structure with { Id = "", FieldKey = "", Label = "", Bounds = structure.Bounds with { X = 0, Y = 0 }, Children = Array.Empty<PaperStructure>(), Properties = NoProperties };
        return new(style, value?.Text, value?.Error, value?.Checked, hover, owner.Mode);
    }
    internal void PrepareDrawing(PaperStructure structure, PaperFormValue? value, bool hover = false)
    {
        if (!drawings.TryGetValue(structure.Id, out var snapshot) || !ReferenceEquals(snapshot.Source, structure)
            || snapshot.Text != value?.Text || snapshot.Error != value?.Error || snapshot.Checked != value?.Checked || snapshot.Hover != hover)
        {
            var key = Key(structure, value, hover);
            if (!recordings.TryGetValue(key, out var cached)) cached = (Record(structure, value, hover), 0);
            recordings[key] = (cached.drawing, cached.users + 1);
            Release(structure.Id); drawingKeys[structure.Id] = key;
            drawings[structure.Id] = cached.drawing with { Source = structure };
        }
    }
    internal void Draw(DrawingContext context, PaperStructure structure, PaperFormValue? value, bool hover = false, double fieldY = 0, double pageHeightCorrection = 0)
    {
        if (!Attached) PrepareDrawing(structure, value, hover);
        if (!drawings.TryGetValue(structure.Id, out var snapshot)) throw new InvalidOperationException("Paper drawings must be prepared before rendering.");
        DrawVector(context, snapshot.Drawing);
        if (snapshot.Image != null) context.DrawImage(snapshot.Image, snapshot.ImageBounds);
        if (snapshot.Title != null)
        {
            double scale = Avalonia.Layout.LayoutHelper.GetLayoutScale(Templates);
            double Round(double n) => Templates.UseLayoutRounding ? Math.Round(n * scale) / scale : n;
            double offset = Round(fieldY + pageHeightCorrection - snapshot.TitleHeight) - Round(fieldY);
            using var translated = context.PushTransform(Matrix.CreateTranslation(0, offset)); DrawVector(context, snapshot.Title);
        }
    }
    Prototype Template(PaperStructure source)
    {
        var style = source with
        {
            Id = "template",
            FieldKey = "template",
            Label = "",
            Bounds = source.Bounds with { X = 0, Y = 0 },
            Title = PaperFormView.TitleAbove(source) ? "" : source.Title,
            Children = Array.Empty<PaperStructure>(),
            Properties = NoProperties,
            Background = source.Background ?? owner.Definition.Background,
            Foreground = source.Foreground ?? owner.Definition.Foreground
        };
        if (prototypes.TryGetValue(source.Kind, out var known))
        {
            if (known.Mode == owner.Mode && known.Style with { CellRanges = style.CellRanges } == style && known.Style.CellRanges.SequenceEqual(style.CellRanges)) return known;
            if (known.Mode == owner.Mode && known.Style with { Title = style.Title } == style)
            {
                if (known.Control is CheckBox check) check.Content = style.Title.Length == 0 ? null : style.Title;
                else if (source.Kind is PaperStructureKind.Label or PaperStructureKind.Group && known.Control is Border { Child: TextBlock text }) text.Text = style.Title;
                else goto Replace;
                return prototypes[source.Kind] = known with { Style = style };
            }
        Replace:
            Templates.Children.Remove(known.Control);
        }
        var value = new PaperFormValue("template");
        var control = owner.Create(style, value);
        control.Focusable = false;
        control.Width = style.Bounds.Width * DipsPerMm; control.Height = style.Bounds.Height * DipsPerMm;
        if (Templates.Parent == null) ((ISetLogicalParent)Templates).SetParent(owner);
        Templates.Children.Add(control); Prepare(control, !Attached);
        return prototypes[source.Kind] = new(style, owner.Mode, value, control);
    }
    internal static void Prepare(Control control, bool detached)
    {
        if (detached && control.Theme == null && Application.Current?.TryFindResource(control.StyleKey, out var theme) == true && theme is Avalonia.Styling.ControlTheme found) control.Theme = found;
        control.ApplyStyling();
        if (detached && control is ScrollViewer scroll && scroll.Template is not PaperViewportTemplate) scroll.Template = new PaperViewportTemplate();
        if (control is TemplatedControl templated) templated.ApplyTemplate();
        if (control is ContentPresenter presenter) presenter.UpdateChild();
        foreach (var child in control.GetVisualChildren().OfType<Control>().ToArray()) Prepare(child, detached);
    }
    sealed class PaperViewportTemplate : IControlTemplate
    {
        public TemplateResult<Control> Build(TemplatedControl control)
        {
            var viewer = (ScrollViewer)control; var names = new NameScope();
            var viewport = new ScrollContentPresenter
            {
                Name = "PART_ContentPresenter",
                Content = viewer.Content,
                CanHorizontallyScroll = viewer.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled,
                CanVerticallyScroll = viewer.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled
            };
            names.Register(viewport.Name, viewport); return new TemplateResult<Control>(viewport, names);
        }
    }
    Snapshot Record(PaperStructure source, PaperFormValue? value, bool hover)
    {
        if (PaperFormView.TitleAbove(source))
        {
            var body = source with { Title = "" };
            var key = Key(body, value, hover);
            if (!recordings.TryGetValue(key, out var cached)) cached = (Record(body, value, hover), 0);
            recordings[key] = (cached.drawing, cached.users + 1);
            try
            {
                if (title.Parent == null) Templates.Children.Add(title);
                title.Text = source.Title; title.FontFamily = source.FontFamily == null ? PaperFormView.DocumentFont : new FontFamily(source.FontFamily);
                title.FontSize = source.FontSize; title.Foreground = PaperFormView.Ink(source.Foreground ?? owner.Definition.Foreground, Look.PaperInk);
                title.Measure(Size.Infinity); title.Arrange(new Rect(title.DesiredSize));
                var titleDrawing = new DrawingGroup();
                using (var context = titleDrawing.Open()) Render(context, title, true);
                return cached.drawing with { Source = source, Title = titleDrawing, TitleHeight = title.Bounds.Height, Glyphs = OwnGlyphs(titleDrawing), Body = key };
            }
            catch { Release(key); throw; }
        }
        RecordingCount++;
        var template = Template(source);
        template.Value.Text = value?.Text ?? ""; template.Value.Checked = value?.Checked; template.Value.Error = value?.Error;
        ((IPseudoClasses)template.Control.Classes).Set(":pointerover", hover);
        foreach (var box in template.Control.GetSelfAndVisualDescendants().OfType<TextBox>())
        {
            box.IsUndoEnabled = false; box.CaretIndex = box.SelectionStart = box.SelectionEnd = 0;
        }
        Prepare(template.Control, !Attached);
        if (!Attached)
            foreach (var box in template.Control.GetSelfAndVisualDescendants().OfType<TextBox>()) box.SetCurrentValue(TextBox.TextProperty, value?.Text ?? "");
        if (!Attached && template.Control is CheckBox check) check.SetCurrentValue(CheckBox.IsCheckedProperty, value?.Checked);
        template.Control.InvalidateMeasure();
        for (int pass = 0; pass < 4; pass++)
        {
            template.Control.Measure(Size.Infinity);
            template.Control.Arrange(new Rect(0, 0, source.Bounds.Width * DipsPerMm, source.Bounds.Height * DipsPerMm));
            if (template.Control.IsMeasureValid && template.Control.IsArrangeValid) break;
        }
        var drawing = new DrawingGroup();
        using (var context = drawing.Open()) Render(context, template.Control, true);
        Simplify(drawing);
        var glyphs = OwnGlyphs(drawing);
        var image = template.Control.GetVisualDescendants().OfType<Image>().FirstOrDefault(i => i.Source != null);
        Rect imageBounds = default;
        if (image?.Source is { } picture)
        {
            double x = image.Bounds.Width / picture.Size.Width, y = image.Bounds.Height / picture.Size.Height;
            if (image.Stretch == Stretch.Uniform) x = y = Math.Min(x, y);
            var at = image.TranslatePoint(default, template.Control) ?? default;
            imageBounds = new Rect(at.X + (image.Bounds.Width - picture.Size.Width * x) / 2, at.Y + (image.Bounds.Height - picture.Size.Height * y) / 2, picture.Size.Width * x, picture.Size.Height * y);
        }
        return new(source, value?.Text, value?.Error, value?.Checked, hover, drawing, null, 0, glyphs, image?.Source, imageBounds);
    }
    void Release(string id)
    {
        if (!drawingKeys.Remove(id, out var key)) return;
        Release(key);
    }
    void Release(PaintKey key)
    {
        var cached = recordings[key];
        if (cached.users == 1)
        {
            recordings.Remove(key); cached.drawing.Dispose();
            if (cached.drawing.Body is { } body) Release(body);
        }
        else recordings[key] = (cached.drawing, cached.users - 1);
    }
    static void Simplify(DrawingGroup group)
    {
        for (int i = group.Children.Count - 1; i >= 0; i--)
        {
            if (group.Children[i] is not DrawingGroup child) continue;
            Simplify(child);
            if (child.Children.Count == 0) group.Children.RemoveAt(i);
            else if (child.Opacity == 1 && child.ClipGeometry == null && (child.Transform == null || child.Transform.Value.IsIdentity))
            {
                group.Children.RemoveAt(i);
                for (int j = child.Children.Count - 1; j >= 0; j--) group.Children.Insert(i, child.Children[j]);
            }
        }
        while (group.Children.Count == 1 && group.Children[0] is DrawingGroup only && group.ClipGeometry == null && group.Opacity == 1)
        {
            group.Transform = new MatrixTransform((only.Transform?.Value ?? Matrix.Identity) * (group.Transform?.Value ?? Matrix.Identity));
            group.Opacity = only.Opacity; group.ClipGeometry = only.ClipGeometry;
            group.Children.Clear(); group.Children.AddRange(only.Children);
        }
    }
    static GlyphRun[] OwnGlyphs(Drawing drawing)
    {
        var result = new List<GlyphRun>();
        void Own(Drawing item)
        {
            if (item is GlyphRunDrawing { GlyphRun: { } run } glyph)
            {
                var owned = new GlyphRun(run.GlyphTypeface, run.FontRenderingEmSize, run.Characters.ToArray(), run.GlyphInfos.ToArray(), run.BaselineOrigin, run.BiDiLevel);
                glyph.GlyphRun = owned; result.Add(owned);
            }
            if (item is DrawingGroup group) foreach (var child in group.Children) Own(child);
        }
        Own(drawing); return result.ToArray();
    }
    internal void PreparePageNumber(int index)
    {
        var d = owner.Definition;
        if (!d.ShowPageNumbers) return;
        string text = d.PageNumberText(index + 1, d.Pages.Count);
        double band = d.PageNumbersOnTop ? d.MarginTop : d.MarginBottom;
        var size = new Size(Math.Max(1, (d.PageSize.Width - d.MarginLeft - d.MarginRight) * DipsPerMm), Math.Max(1, band * DipsPerMm));
        var at = new Point(d.MarginLeft * DipsPerMm, d.PageNumbersOnTop ? 0 : (d.PageSize.Height - band) * DipsPerMm);
        var key = (text, size, at, d.PageNumberAlignment, d.Foreground);
        if (numbers.TryGetValue(index, out var known) && Equals(known.key, key)) return;
        if (number.Parent == null) Templates.Children.Add(number);
        number.Text = text; number.TextAlignment = d.PageNumberAlignment; number.FontFamily = PaperFormView.DocumentFont; number.FontSize = TextSize.Caption;
        number.Foreground = PaperFormView.Ink(d.Foreground, Look.PaperInk); number.Width = size.Width; number.Height = size.Height;
        number.Padding = new Thickness(0, Math.Max(0, (band * DipsPerMm - TextSize.Caption * 1.4) / 2), 0, 0);
        number.Measure(Size.Infinity); number.Arrange(new Rect(size));
        var drawing = new DrawingGroup(); using (var context = drawing.Open()) Render(context, number, true);
        if (known.glyphs != null) foreach (var glyph in known.glyphs) glyph.Dispose();
        double scale = Avalonia.Layout.LayoutHelper.GetLayoutScale(number);
        numbers[index] = (key, new Point(Math.Round(at.X * scale) / scale, Math.Round(at.Y * scale) / scale), drawing, OwnGlyphs(drawing));
    }
    internal void DrawPageNumber(DrawingContext context, int index)
    {
        if (!owner.Definition.ShowPageNumbers) return;
        if (!Attached) PreparePageNumber(index);
        if (!numbers.TryGetValue(index, out var known)) throw new InvalidOperationException("Paper page numbers must be prepared before rendering.");
        using var translated = context.PushTransform(Matrix.CreateTranslation(known.at)); DrawVector(context, known.drawing);
    }
    static void DrawVector(DrawingContext context, Drawing drawing)
    {
        if (drawing is not DrawingGroup group) { drawing.Draw(context); return; }
        // Restore the logical transform after clips: Skia's clip pop exposes its DPI-scaled device matrix.
        using var transform = context.PushTransform(group.Transform?.Value ?? Matrix.Identity);
        using var opacity = group.Opacity != 1 ? context.PushOpacity(group.Opacity) : default;
        using var clip = group.ClipGeometry is RectangleGeometry rectangle ? context.PushClip(rectangle.Rect)
            : group.ClipGeometry != null ? context.PushGeometryClip(group.ClipGeometry) : default;
        foreach (var child in group.Children) DrawVector(context, child);
    }
    static void Render(DrawingContext context, Visual visual, bool root = false)
    {
        if (!visual.IsVisible || visual.Opacity <= 0) return;
        if (visual.ClipToBounds && (visual.Bounds.Width <= 0 || visual.Bounds.Height <= 0)) return;
        var position = root ? default : visual.Bounds.Position;
        var origin = visual.RenderTransformOrigin.ToPixels(visual.Bounds.Size);
        var transform = Matrix.CreateTranslation(-origin.X, -origin.Y) * (visual.RenderTransform?.Value ?? Matrix.Identity) * Matrix.CreateTranslation(origin.X + position.X, origin.Y + position.Y);
        using var translated = !transform.IsIdentity ? context.PushTransform(transform) : default;
        using var opacity = visual.Opacity != 1 ? context.PushOpacity(visual.Opacity) : default;
        using var clip = visual.ClipToBounds ? context.PushClip(new Rect(visual.Bounds.Size)) : default;
        using var geometryClip = visual.Clip != null ? context.PushGeometryClip(visual.Clip) : default;
        if (visual is not Image && visual.Bounds.Width > 0 && visual.Bounds.Height > 0) visual.Render(context);
        foreach (var child in visual.GetVisualChildren().OrderBy(v => v.ZIndex)) Render(context, child);
    }
    internal void Retain(ISet<string> ids)
    {
        foreach (string id in drawings.Keys.Where(id => !ids.Contains(id)).ToArray()) { Release(id); drawings.Remove(id); }
    }
    internal void Clear()
    {
        foreach (var entry in recordings.Values) entry.drawing.Dispose(); recordings.Clear(); drawingKeys.Clear(); drawings.Clear();
        foreach (var entry in numbers.Values) foreach (var glyph in entry.glyphs) glyph.Dispose(); numbers.Clear();
    }
    internal void Reset() { Clear(); Templates.Children.Clear(); prototypes.Clear(); }
    public void Dispose()
    {
        Clear();
        if (Templates.Parent is Panel parent) parent.Children.Remove(Templates); else ((ISetLogicalParent)Templates).SetParent(null);
        Templates.Children.Clear(); prototypes.Clear();
    }
}

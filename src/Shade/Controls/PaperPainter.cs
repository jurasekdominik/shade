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
    readonly Canvas templates = new();
    internal PaperPainter(PaperFormView owner) { this.owner = owner; ((ISetLogicalParent)templates).SetParent(owner); }
    readonly Dictionary<string, Prototype> prototypes = new(StringComparer.Ordinal);
    readonly Dictionary<string, Snapshot> drawings = new(StringComparer.Ordinal);
    readonly TextBlock title = new() { IsHitTestVisible = false };
    static readonly IReadOnlyDictionary<string, string> NoProperties = new Dictionary<string, string>();
    sealed record Prototype(PaperStructure Style, PaperFormMode Mode, PaperFormValue Value, Control Control);
    sealed record Snapshot(PaperStructure Source, string? Text, string? Error, bool? Checked, bool Hover, DrawingGroup Drawing, GlyphRun[] Glyphs, IImage? Image, Rect ImageBounds)
    {
        internal void Dispose() { foreach (var glyph in Glyphs) glyph.Dispose(); }
    }
    internal void Draw(DrawingContext context, PaperStructure structure, PaperFormValue? value, bool hover = false)
    {
        if (!drawings.TryGetValue(structure.Id, out var snapshot) || !ReferenceEquals(snapshot.Source, structure)
            || snapshot.Text != value?.Text || snapshot.Error != value?.Error || snapshot.Checked != value?.Checked || snapshot.Hover != hover)
        {
            var recorded = Record(structure, value, hover);
            snapshot?.Dispose(); drawings[structure.Id] = snapshot = recorded;
        }
        snapshot.Drawing.Draw(context);
        if (snapshot.Image != null) context.DrawImage(snapshot.Image, snapshot.ImageBounds);
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
            templates.Children.Remove(known.Control);
        }
        var value = new PaperFormValue("template");
        var control = owner.Create(style, value);
        control.Width = style.Bounds.Width * DipsPerMm; control.Height = style.Bounds.Height * DipsPerMm;
        templates.Children.Add(control); Prepare(control);
        return prototypes[source.Kind] = new(style, owner.Mode, value, control);
    }
    static void Prepare(Control control)
    {
        control.ApplyStyling();
        if (control is ScrollViewer scroll && scroll.Template is not PaperViewportTemplate) scroll.Template = new PaperViewportTemplate();
        if (control is TemplatedControl templated) templated.ApplyTemplate();
        if (control is ScrollViewer viewer && viewer.GetVisualDescendants().OfType<ScrollContentPresenter>().FirstOrDefault() is { } viewport)
        {
            viewport.Content = viewer.Content;
            foreach (var bar in viewer.GetVisualDescendants().OfType<ScrollBar>())
            {
                var visibility = bar.Orientation == Avalonia.Layout.Orientation.Horizontal ? viewer.HorizontalScrollBarVisibility : viewer.VerticalScrollBarVisibility;
                if (visibility is ScrollBarVisibility.Hidden or ScrollBarVisibility.Disabled) bar.IsVisible = false;
            }
        }
        if (control is ContentPresenter presenter) presenter.UpdateChild();
        foreach (var child in control.GetVisualChildren().OfType<Control>().ToArray()) Prepare(child);
    }
    sealed class PaperViewportTemplate : IControlTemplate
    {
        public TemplateResult<Control> Build(TemplatedControl control)
        {
            var viewer = (ScrollViewer)control;
            var names = new NameScope();
            var viewport = new ScrollContentPresenter
            {
                Name = "PART_ContentPresenter",
                Content = viewer.Content,
                CanHorizontallyScroll = viewer.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled,
                CanVerticallyScroll = viewer.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled
            };
            names.Register(viewport.Name, viewport);
            return new TemplateResult<Control>(viewport, names);
        }
    }
    Snapshot Record(PaperStructure source, PaperFormValue? value, bool hover)
    {
        var template = Template(source);
        template.Value.Text = value?.Text ?? ""; template.Value.Checked = value?.Checked; template.Value.Error = value?.Error;
        ((IPseudoClasses)template.Control.Classes).Set(":pointerover", hover);
        foreach (var box in template.Control.GetSelfAndVisualDescendants().OfType<TextBox>())
        {
            box.IsUndoEnabled = false; box.CaretIndex = box.SelectionStart = box.SelectionEnd = 0;
        }
        Prepare(template.Control);
        template.Control.InvalidateMeasure();
        for (int pass = 0; pass < 4; pass++)
        {
            template.Control.Measure(Size.Infinity);
            template.Control.Arrange(new Rect(0, 0, source.Bounds.Width * DipsPerMm, source.Bounds.Height * DipsPerMm));
            if (template.Control.IsMeasureValid && template.Control.IsArrangeValid) break;
        }
        var drawing = new DrawingGroup();
        using (var context = drawing.Open())
        {
            Render(context, template.Control, true);
            if (PaperFormView.TitleAbove(source))
            {
                if (title.Parent == null) templates.Children.Add(title);
                title.Text = source.Title; title.FontFamily = source.FontFamily == null ? PaperFormView.DocumentFont : new FontFamily(source.FontFamily);
                title.FontSize = source.FontSize; title.Foreground = PaperFormView.Ink(source.Foreground ?? owner.Definition.Foreground, Look.PaperInk);
                title.Measure(Size.Infinity); title.Arrange(new Rect(title.DesiredSize));
                using var translated = context.PushTransform(Matrix.CreateTranslation(0, -title.Bounds.Height));
                title.Render(context);
            }
        }
        var glyphs = new List<GlyphRun>();
        void OwnGlyphs(Drawing item)
        {
            if (item is GlyphRunDrawing { GlyphRun: { } run } glyph)
            {
                var owned = new GlyphRun(run.GlyphTypeface, run.FontRenderingEmSize, run.Characters.ToArray(), run.GlyphInfos.ToArray(), run.BaselineOrigin, run.BiDiLevel);
                glyph.GlyphRun = owned; glyphs.Add(owned);
            }
            if (item is DrawingGroup group) foreach (var child in group.Children) OwnGlyphs(child);
        }
        OwnGlyphs(drawing);
        var image = template.Control.GetVisualDescendants().OfType<Image>().FirstOrDefault(i => i.Source != null);
        Rect imageBounds = default;
        if (image?.Source is { } picture)
        {
            double x = image.Bounds.Width / picture.Size.Width, y = image.Bounds.Height / picture.Size.Height;
            if (image.Stretch == Stretch.Uniform) x = y = Math.Min(x, y);
            var at = image.TranslatePoint(default, template.Control) ?? default;
            imageBounds = new Rect(at.X + (image.Bounds.Width - picture.Size.Width * x) / 2, at.Y + (image.Bounds.Height - picture.Size.Height * y) / 2, picture.Size.Width * x, picture.Size.Height * y);
        }
        return new(source, value?.Text, value?.Error, value?.Checked, hover, drawing, glyphs.ToArray(), image?.Source, imageBounds);
    }
    static void Render(DrawingContext context, Visual visual, bool root = false)
    {
        if (!visual.IsVisible || visual.Opacity <= 0) return;
        if (visual.ClipToBounds && (visual.Bounds.Width <= 0 || visual.Bounds.Height <= 0)) return;
        var position = root ? default : visual.Bounds.Position;
        var origin = visual.RenderTransformOrigin.ToPixels(visual.Bounds.Size);
        var transform = Matrix.CreateTranslation(-origin.X, -origin.Y) * (visual.RenderTransform?.Value ?? Matrix.Identity) * Matrix.CreateTranslation(origin.X + position.X, origin.Y + position.Y);
        using var translated = context.PushTransform(transform);
        using var opacity = visual.Opacity != 1 ? context.PushOpacity(visual.Opacity) : default;
        using var clip = visual.ClipToBounds ? context.PushClip(new Rect(visual.Bounds.Size)) : default;
        using var geometryClip = visual.Clip != null ? context.PushGeometryClip(visual.Clip) : default;
        if (visual is not Image && visual.Bounds.Width > 0 && visual.Bounds.Height > 0) visual.Render(context);
        foreach (var child in visual.GetVisualChildren().OrderBy(v => v.ZIndex)) Render(context, child);
    }
    internal void Retain(ISet<string> ids)
    {
        foreach (string id in drawings.Keys.Where(id => !ids.Contains(id)).ToArray()) { drawings[id].Dispose(); drawings.Remove(id); }
    }
    internal void Clear() { foreach (var drawing in drawings.Values) drawing.Dispose(); drawings.Clear(); }
    public void Dispose() { Clear(); templates.Children.Clear(); prototypes.Clear(); ((ISetLogicalParent)templates).SetParent(null); }
}

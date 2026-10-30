using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using static Shade.Look;
using static Shade.PaperFormDefinition;

namespace Shade;

/// <summary>Standalone editable HTML and a vector PDF, both using the authored page coordinates.</summary>
public static class PaperFormExport
{
    // Built-ins export detached; custom controls retain the native attachment lifecycle in one hidden window.
    sealed class Session : IDisposable
    {
        readonly PaperFormView view;
        readonly Window? window;
        internal Session(PaperFormView source)
        {
            source.Definition.ValidateForAssignment(); source.RefreshProblems(); ThrowOnErrors(source);
            view = new PaperFormView { SkipProblemCheck = true, Mode = PaperFormMode.Preview };
            view.UsePainter(true);
            view.Values = source.Values; view.StructureTypes = source.StructureTypes; view.Definition = source.Definition;
            if (PaperFormView.All(source.Definition.Pages.SelectMany(p => p.Structures)).Any(s => source.FindTemplate(s.Kind) != null))
            {
                var size = view.Definition.PageSize;
                window = new Window { Content = view, Width = size.Width * DipsPerMm, Height = size.Height * DipsPerMm, CanResize = false, ShowInTaskbar = false, ShowActivated = false, WindowDecorations = WindowDecorations.None, Opacity = 0 };
                try { window.Show(); Dispatcher.UIThread.RunJobs(); }
                catch { Dispose(); throw; }
            }
        }
        internal Canvas Page(int index)
        {
            view.PageIndex = index;
            var size = view.Definition.PageSize;
            PaperPainter.Prepare(view, window == null);
            if (window != null) Dispatcher.UIThread.RunJobs();
            view.Measure(new Size(size.Width * DipsPerMm, size.Height * DipsPerMm)); view.Arrange(new Rect(view.DesiredSize));
            if (window != null) view.UpdateLayout();
            view.PrepareExportDrawings(); return view.Page;
        }
        public void Dispose() { try { window?.Close(); } finally { view.DisposeExportDrawings(); } }
    }

    /// <summary>Render a read-only page at its physical size. Must run on the UI thread after Look.Apply; the caller disposes the bitmap.</summary>
    public static RenderTargetBitmap RenderPage(this PaperFormView source, int pageIndex, int dpi = 144)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (pageIndex < 0 || pageIndex >= source.Definition.Pages.Count) throw new ArgumentOutOfRangeException(nameof(pageIndex));
        if (dpi is < 72 or > 600) throw new ArgumentOutOfRangeException(nameof(dpi));
        var size = source.Definition.PageSize;
        var pixels = new PixelSize((int)Math.Ceiling(size.Width * dpi / 25.4), (int)Math.Ceiling(size.Height * dpi / 25.4));
        if ((long)pixels.Width * pixels.Height > 40_000_000) throw new ArgumentException("Page raster exceeds 40 million pixels; reduce DPI.");
        using var session = new Session(source);
        var page = session.Page(pageIndex);
        var bitmap = new RenderTargetBitmap(pixels, new Vector(dpi, dpi));
        try { bitmap.Render(page); return bitmap; }
        catch { bitmap.Dispose(); throw; }
    }

    /// <summary>A read-only vector PDF: text stays text (embedded font subsets, selectable and searchable), lines and boxes stay
    /// paths, pictures keep their resolution. Needs the Skia renderer (UseSkia) and the UI thread. Invalid or overflowing
    /// forms are rejected before anything is written; warnings do not block.</summary>
    public static void ExportPdf(this PaperFormView source, Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination); Dispatcher.UIThread.VerifyAccess();
        var size = source.Definition.PageSize;
        float width = (float)(size.Width * 72 / 25.4), height = (float)(size.Height * 72 / 25.4);
        // staged in memory, so a later invalid page cannot leave the host with a partial PDF
        using var staged = new MemoryStream();
        // Skia snaps the page to whole pixels at RasterDpi: 1270 DPI is 0.02 mm, so paper sizes stay exact (it rasterizes only
        // effects PDF cannot express)
        const float rasterDpi = 1270;
        using (var session = new Session(source))
        using (var pdf = SkiaSharp.SKDocument.CreatePdf(staged, new SkiaSharp.SKDocumentPdfMetadata { Title = T("Paper form"), Creator = "Shade", Producer = "Shade", RasterDpi = rasterDpi, EncodingQuality = 100 }))
        {
            for (int i = 0; i < source.Definition.Pages.Count; i++)
            {
                var page = session.Page(i);
                // Avalonia sets absolute device transforms while it draws (bypassing the PDF canvas's point space), so the page is
                // recorded in DIPs first; a picture plays back relative to the canvas it is drawn on, still as vectors
                var bounds = new Rect(page.Bounds.Size);
                using var recorder = new SkiaSharp.SKPictureRecorder();
                var recording = recorder.BeginRecording(new SkiaSharp.SKRect(0, 0, (float)bounds.Width, (float)bounds.Height));
                try { Avalonia.Skia.Helpers.DrawingContextHelper.RenderAsync(recording, page, bounds, new Vector(96, 96)).GetAwaiter().GetResult(); }
                catch (Exception e) when (e is InvalidCastException or NullReferenceException or NotSupportedException)
                { throw new InvalidOperationException("PDF export needs Avalonia's Skia renderer (UseSkia()); this application renders without it.", e); }
                using var picture = recorder.EndRecording();
                var canvas = pdf.BeginPage(width, height);
                canvas.Scale(72f / 96f);  // page DIPs (96 per inch) to PDF points (72 per inch)
                canvas.DrawPicture(picture);
                pdf.EndPage();
            }
            pdf.Close();
        }
        staged.Position = 0; staged.CopyTo(destination);
    }

    // Warnings (overlap, outside the printable area) are the host's call: ConfirmExport asks the user about them.
    static void ThrowOnErrors(PaperFormView view)
    {
        if (view.Problems.Any(p => !p.IsWarning)) throw new InvalidOperationException(string.Join(" ", view.Problems.Where(p => !p.IsWarning).Select(p => p.Message).Distinct()));
    }

    /// <summary>Before an export: refresh the problems; list errors and return false, or list warnings (which structures overlap or
    /// lie outside the printable area) and return whether the user exports anyway. Returns true when there is nothing to report.</summary>
    public static async System.Threading.Tasks.Task<bool> ConfirmExport(this PaperFormView source, Window owner)
    {
        source.RefreshProblems();
        string List(bool warnings) => string.Join("\n", source.Problems.Where(p => p.IsWarning == warnings).Select(p => "• " + p.Message).Distinct());
        if (source.Problems.Any(p => !p.IsWarning)) { await Ask.Info(owner, T("Correct these fields before exporting:") + "\n" + List(false)); return false; }
        if (source.Problems.Count == 0) return true;
        return await Ask.Confirm(owner, T("Some elements overlap or lie outside the printable area:") + "\n" + List(true), T("Export anyway"));
    }

    /// <summary>Self-contained editable HTML. Save HTML in the exported page persists current inputs into a new standalone file.</summary>
    public static string ExportHtml(this PaperFormView source)
    {
        source.Definition.Validate(); source.RefreshProblems();
        ThrowOnErrors(source);
        var definition = source.Definition; var page = definition.PageSize;
        string H(string? value) => WebUtility.HtmlEncode(value ?? "");
        string Color(string? value, Tone role) => H(value == null ? HexOf(role) : CssColor(value, HexOf(role)));
        var html = new StringBuilder("<!doctype html><html lang=\"").Append(H(Culture.Name.Length > 0 ? Culture.Name : "en")).Append("\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>Paper form</title><style>");
        html.Append("*{box-sizing:border-box}body{margin:0;padding:16px;background:").Append(H(HexOf(PaperDesk))).Append(";color:").Append(H(HexOf(PaperInk))).Append(";font-family:system-ui,sans-serif}nav{display:flex;gap:8px;margin-bottom:16px}button{font:inherit;padding:4px 12px;cursor:pointer}.page{position:relative;margin:0 auto 16px;break-after:page;overflow:visible}.structure{position:absolute;box-sizing:border-box}.structure input,.structure textarea{display:block;width:100%;height:100%;min-width:0;min-height:0;padding:0;border:0;border-radius:0;background:transparent;color:inherit;font:inherit;text-align:inherit;outline:none}.structure:focus-within{outline:1px solid ").Append(H(HexOf(FocusBorder))).Append("}.structure textarea{resize:none;line-height:normal}.caption{display:flex;flex-direction:column}.caption label,.caption small{font:inherit;flex:none}.caption input{flex:1;min-height:0}.check{display:flex;align-items:center;gap:8px}.check input{appearance:none;width:var(--indicator);height:var(--indicator);flex:none;border:inherit;background:inherit;position:relative}.check input:checked:after{content:'✓';position:absolute;inset:0;text-align:center;font:inherit;line-height:var(--indicator)}.character-container:focus-within{outline:none}.characters{overflow:visible}.calendar{position:absolute;right:2px;top:50%;transform:translateY(-50%);padding:2px;border:0;background:none;color:inherit;opacity:.6;cursor:pointer;line-height:0}.calendar:hover{opacity:1}@media print{.calendar{display:none}}.structure>.title{position:absolute;left:0;bottom:100%;white-space:nowrap;pointer-events:none}.characters:after{content:'';position:absolute;inset:0;pointer-events:none;border:calc(var(--cell-border)*1px) solid var(--cell-border-color)}.characters canvas{position:absolute;inset:0;width:100%;height:100%;pointer-events:none}.characters input{color:transparent;caret-color:transparent;position:absolute;inset:0;opacity:.01}.error{outline:2px solid ").Append(H(HexOf(Danger))).Append("}.notice{white-space:pre-wrap} @page{size:").Append(Number(page.Width)).Append("mm ").Append(Number(page.Height)).Append("mm;margin:0}@media print{body{padding:0;background:none}nav,.notice{display:none}.page{margin:0;break-after:page}.page:last-of-type{break-after:auto}.structure:focus-within{outline:none}*{print-color-adjust:exact;-webkit-print-color-adjust:exact}}</style></head><body><nav><button id=\"save\" type=\"button\">").Append(H(T("Save HTML"))).Append("</button><button id=\"print\" type=\"button\">").Append(H(T("Print"))).Append("</button></nav><p class=\"notice\" role=\"status\"></p>");
        void Structure(PaperStructure s, double parentBorder = 0)
        {
            var value = source.Value(s); string text = H(value?.Text), readOnly = s.IsReadOnly ? " readonly" : "";
            string family = "'" + (s.FontFamily ?? PaperFormView.DocumentFont.Name).Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", "").Replace("\n", "") + "'";
            string style = $"left:{Number(s.Bounds.X - parentBorder)}mm;top:{Number(s.Bounds.Y - parentBorder)}mm;width:{Number(s.Bounds.Width)}mm;height:{Number(s.Bounds.Height)}mm;background:{Color(s.Background ?? definition.Background, Paper)};color:{Color(s.Foreground ?? definition.Foreground, PaperInk)};border:{Number(s.BorderWidth)}mm solid {Color(s.BorderColor, PaperRule)};font-size:{Number(s.FontSize)}px;font-family:{H(family)};text-align:{s.Alignment.ToString().ToLowerInvariant()};";
            string kind = s.Kind, name = H(s.Label.Length > 0 ? s.Label : s.Title.Length > 0 ? s.Title : s.FieldKey);
            style += "--selection:" + H(CssColor(((Avalonia.Media.Color)Selection).ToString(), "transparent")) + ";--selection-text:" + Color(s.Foreground ?? definition.Foreground, PaperInk) + ";";
            if (kind == "characters") style += $"--cell-border:{Number(s.BorderWidth * DipsPerMm)};border:0 solid {Color(s.BorderColor, PaperRule)};--cell-border-color:{Color(s.BorderColor, PaperRule)};";
            if (kind == "characters" && s.GridAlignment != Avalonia.Media.TextAlignment.Left) style += "background:transparent;";
            if (kind is "caption" or "check" or "line") style += "border:0;background:transparent;";
            if (kind == PaperStructureKind.Image) style += "background:transparent;";
            if (kind == "check") style += "--indicator:" + Number(Math.Min(s.Bounds.Width, s.Bounds.Height)) + "mm;";
            html.Append("<div class=\"structure ").Append(H(kind == "characters" && s.GridAlignment != Avalonia.Media.TextAlignment.Left ? "character-container" : kind)).Append("\" style=\"").Append(style).Append("\" data-id=\"").Append(H(s.Id)).Append("\">");
            string frame = $"border:{Number(s.BorderWidth)}mm solid {Color(s.BorderColor, PaperRule)};background:{Color(s.Background ?? definition.Background, Paper)};";
            string inset = $"padding:{Number(s.TextPadding)}mm;";
            string dateAttributes = kind == "date" ? $" data-date-format=\"{H(s.DateFormat)}\" placeholder=\"{H(s.DateFormat)}\"" : "";
            string input = $"<input aria-label=\"{name}\" name=\"{H(s.FieldKey)}\" value=\"{text}\"{dateAttributes}{(kind is "caption" or "text" or "date" ? " style=\"" + (kind == "caption" ? frame : "") + inset + "\"" : "")}{readOnly}>";
            if (source.FindTemplate(kind) is { } template) html.Append(template.ExportHtml(s, value));
            else if (kind == "group") { html.Append(H(s.Title)); foreach (var child in s.Children) Structure(child, s.BorderWidth); }
            else if (kind == "label") html.Append(H(s.Title));
            else if (kind == PaperStructureKind.Image) { if (s.Image != null) html.Append("<img alt=\"").Append(name).Append("\" src=\"").Append(H(s.Image)).Append("\" style=\"display:block;width:100%;height:100%;object-fit:").Append(s.KeepAspectRatio ? "contain" : "fill").Append("\">"); }
            else if (kind == "line")
            {
                bool vertical = s.LineOrientation == Avalonia.Layout.Orientation.Vertical;
                html.Append("<span style=\"position:absolute;background:").Append(Color(s.BorderColor, PaperRule)).Append(vertical ? ";height:100%;width:" : ";width:100%;height:").Append(Number(s.BorderWidth)).Append(vertical ? "mm;left:50%;top:0;transform:translateX(-50%)\"></span>" : "mm;top:50%;left:0;transform:translateY(-50%)\"></span>");
            }
            else if (kind == "check") html.Append("<input type=\"checkbox\" aria-label=\"").Append(name).Append("\" name=\"").Append(H(s.FieldKey)).Append("\" style=\"").Append(frame).Append('"').Append(value?.Checked == true ? " checked" : "").Append(value != null && value.Checked == null ? " data-mixed=\"true\"" : "").Append(s.IsReadOnly ? " disabled" : "").Append("><svg aria-hidden=\"true\" viewBox=\"0 0 16 16\"><path class=\"checkmark\" d=\"M4.16 8.32 L6.88 10.88 L11.84 5.44\"/><path class=\"dash\" d=\"M4.48 8 L11.52 8\"/></svg><span>").Append(H(s.Title)).Append("</span>");
            else if (kind == "multiline") html.Append("<textarea aria-label=\"").Append(name).Append("\" name=\"").Append(H(s.FieldKey)).Append("\" data-scroll=\"").Append(s.AllowMultilineScrolling ? "true" : "false").Append("\" style=\"overflow:").Append(s.AllowMultilineScrolling ? "auto;" : "hidden;").Append(inset).Append('"').Append(readOnly).Append('>').Append(text).Append("</textarea>");
            else if (kind == "caption") html.Append("<label>").Append(H(s.Title)).Append("</label>").Append(input).Append("<small>").Append(H(s.Subtitle)).Append("</small>");
            else if (kind == "characters")
            {
                bool aligned = s.GridAlignment != Avalonia.Media.TextAlignment.Left;
                if (aligned)
                {
                    double insetMm = Math.Max(0, s.Bounds.Width - s.CellCount * s.CellWidth) * (s.GridAlignment == Avalonia.Media.TextAlignment.Right ? 1 : .5);
                    html.Append("<div class=\"structure characters\" style=\"position:absolute;top:0;left:").Append(Number(insetMm)).Append("mm;width:").Append(Number(s.CellCount * s.CellWidth)).Append("mm;height:100%;background:").Append(Color(s.Background ?? definition.Background, Paper)).Append(";border:0 solid ").Append(Color(s.BorderColor, PaperRule)).Append("\">");
                }
                html.Append("<canvas data-align=\"").Append(s.GridAlignment.ToString().ToLowerInvariant()).Append("\" data-count=\"").Append(s.CellCount).Append("\" data-width=\"").Append(Number(s.CellWidth * DipsPerMm)).Append("\" data-ranges=\"").Append(H(JsonSerializer.Serialize(s.CellRanges.Select(r => r with { Background = r.Background == null ? null : CssColor(r.Background, "transparent"), Foreground = r.Foreground == null ? null : CssColor(r.Foreground, "inherit") })))).Append("\"></canvas>").Append(input);
                if (aligned) html.Append("</div>");
            }
            else html.Append(input);
            if (PaperFormView.TitleAbove(s)) html.Append("<span class=\"title\" aria-hidden=\"true\">").Append(H(s.Title)).Append("</span>");
            html.Append("</div>");
        }
        int pageNumber = 0;
        foreach (var p in definition.Pages)
        {
            html.Append("<section class=\"page\" style=\"width:").Append(Number(page.Width)).Append("mm;height:").Append(Number(page.Height)).Append("mm;background:").Append(Color(definition.Background, Paper)).Append(";color:").Append(Color(definition.Foreground, PaperInk)).Append("\">");
            foreach (var s in p.Structures) Structure(s);
            pageNumber++;
            if (definition.ShowPageNumbers)
            {
                double band = definition.PageNumbersOnTop ? definition.MarginTop : definition.MarginBottom;
                html.Append("<footer style=\"position:absolute;display:flex;align-items:center;").Append(definition.PageNumbersOnTop ? "top:0" : "bottom:0").Append(";left:").Append(Number(definition.MarginLeft)).Append("mm;right:").Append(Number(definition.MarginRight))
                    .Append("mm;justify-content:").Append(definition.PageNumberAlignment == Avalonia.Media.TextAlignment.Left ? "flex-start" : definition.PageNumberAlignment == Avalonia.Media.TextAlignment.Right ? "flex-end" : "center")
                    .Append(";height:").Append(Number(band)).Append("mm;font-size:").Append(Number(TextSize.Caption)).Append("px;font-family:'").Append(H(PaperFormView.DocumentFont.Name)).Append("'\">")
                    .Append(H(definition.PageNumberText(pageNumber, definition.Pages.Count))).Append("</footer>");
            }
            html.Append("</section>");
        }
        html.Append("<script>").Append(HtmlScript.Replace("__calendar__", JsonSerializer.Serialize(T("Calendar"))).Replace("__overflow__", JsonSerializer.Serialize(T("Some values exceed their field bounds. Correct the highlighted fields before printing.")))).Append("</script></body></html>");
        html.Replace("</style>", ".structure input::selection,.structure textarea::selection{background:var(--selection);color:var(--selection-text)}</style>");
        html.Replace("</style>", ".structure input,.structure textarea{margin:0}.check input:disabled{opacity:1}.check input:checked:after{content:none}.check svg{position:absolute;left:0;top:50%;transform:translateY(-50%);width:var(--indicator);height:var(--indicator);pointer-events:none}.check svg path{display:none;fill:none;stroke:currentColor;stroke-width:1.8;stroke-linecap:round;stroke-linejoin:round}.check input:checked+svg .checkmark,.check input:indeterminate+svg .dash{display:block}@media print{body[data-invalid] .page{display:none}body[data-invalid] .notice{display:block;white-space:pre-wrap;overflow-wrap:anywhere}}</style>");
        html.Replace("<title>Paper form</title>", "<title>" + H(T("Paper form")) + "</title>"); return html.ToString();
    }

    // The input owns browser editing, composition, clipboard and undo. Canvas supplies the same fixed grapheme pitch for every font.
    const string HtmlScript = """
    (()=>{
      const segmenter=new Intl.Segmenter(undefined,{granularity:'grapheme'}), parts=s=>[...segmenter.segment(s)];
      document.querySelectorAll('input[type=checkbox][data-mixed=true]').forEach(input=>input.indeterminate=true);
      const drawAll=[],gridDraw=new WeakMap(),dirtyDraw=new Set();let redrawFrame=0;
      const schedule=draw=>{if(!draw)return;dirtyDraw.add(draw);if(!redrawFrame)redrawFrame=requestAnimationFrame(()=>{redrawFrame=0;const pending=[...dirtyDraw];dirtyDraw.clear();pending.forEach(draw=>draw());});};
      const redrawAll=()=>drawAll.forEach(schedule);
      function validDate(input){
        const format=input.dataset.dateFormat;if(!format||!input.value)return true;
        const tokens=format.match(/yyyy|MM|M|dd|d/g),escape=s=>s.replace(/[.*+?^${}()|[\]\\]/g,'\\$&');
        let pattern='',at=0;for(const match of format.matchAll(/yyyy|MM|M|dd|d/g)){pattern+=escape(format.slice(at,match.index))+(match[0]==='yyyy'?'(\\d{4})':match[0].length===2?'(\\d{2})':'(\\d{1,2})');at=match.index+match[0].length;}pattern+=escape(format.slice(at));
        const match=input.value.match(new RegExp('^'+pattern+'$'));if(!match)return false;
        const parts=Object.fromEntries(tokens.map((t,i)=>[t[0],+match[i+1]])),date=new Date(0);date.setUTCFullYear(parts.y,parts.M-1,parts.d);
        return parts.y>=1&&parts.y<=9999&&date.getUTCFullYear()===parts.y&&date.getUTCMonth()===parts.M-1&&date.getUTCDate()===parts.d;
      }
      document.querySelectorAll('[data-date-format]').forEach(input=>input.addEventListener('input',()=>{const bad=!validDate(input);input.closest('.structure').classList.toggle('error',bad);input.setAttribute('aria-invalid',String(bad));}));
      // a calendar button opens the browser's date picker and writes the day in the field's own format; it never prints
      document.querySelectorAll('[data-date-format]').forEach(input=>{
        if(input.readOnly)return;
        input.parentElement.querySelectorAll('.calendar,.calendar-picker').forEach(old=>old.remove());  // a saved and reopened page builds them again
        const picker=document.createElement('input'),button=document.createElement('button'),f=input.dataset.dateFormat;
        picker.type='date';picker.className='calendar-picker';picker.tabIndex=-1;picker.setAttribute('aria-hidden','true');picker.style.cssText='position:absolute;right:0;bottom:0;width:1px;height:1px;opacity:0;border:0;padding:0';
        button.type='button';button.className='calendar';button.tabIndex=-1;button.setAttribute('aria-label',__calendar__);button.innerHTML='<svg viewBox="0 0 24 24" width="12" height="12" fill="none" stroke="currentColor" stroke-width="2"><rect x="3" y="4" width="18" height="18" rx="2"/><path d="M16 2v4M8 2v4M3 10h18"/></svg>';
        button.onclick=()=>{try{picker.showPicker();}catch{picker.focus();picker.click();}};
        picker.onchange=()=>{const [y,m,d]=picker.value.split('-');if(!y)return;input.value=f.replace(/yyyy|MM|M|dd|d/g,t=>t==='yyyy'?y:t==='MM'?m:t==='M'?String(+m):t==='dd'?d:String(+d));input.dispatchEvent(new Event('input',{bubbles:true}));input.focus();};
        input.parentElement.append(picker,button);
      });
      document.querySelectorAll('.characters').forEach(field=>{
        const input=field.querySelector('input'),canvas=field.querySelector('canvas'),ctx=canvas.getContext('2d'),count=+canvas.dataset.count,pitch=+canvas.dataset.width,ranges=JSON.parse(canvas.dataset.ranges),align=canvas.dataset.align||'left';
        const shiftOf=n=>n>=count?0:align==='right'?count-n:align==='center'?Math.floor((count-n)/2):0;
        let offset=0,drag=false,anchor=0,composing=false,composition;
        const accepts=(value,old=input.value)=>!/[\r\n\t]/.test(value)&&(parts(value).length<=count||parts(value).length<=parts(old).length);
        const replacement=text=>input.value.slice(0,input.selectionStart)+text+input.value.slice(input.selectionEnd);
        input.addEventListener('beforeinput',event=>{if(!composing&&!event.isComposing&&event.inputType.startsWith('insert')&&event.data!=null&&!accepts(replacement(event.data)))event.preventDefault();});
        input.addEventListener('paste',event=>{const text=event.clipboardData?.getData('text/plain');if(text!=null&&!accepts(replacement(text)))event.preventDefault();});
        input.addEventListener('compositionstart',()=>{composing=true;composition=[input.value,input.selectionStart,input.selectionEnd];});
        input.addEventListener('compositionend',()=>{composing=false;if(composition&&!accepts(input.value,composition[0])){input.value=composition[0];input.setSelectionRange(composition[1],composition[2]);}composition=null;schedule(draw);});
        function draw(){
          const style=getComputedStyle(field),w=field.clientWidth,h=field.clientHeight,dpr=devicePixelRatio||1;
          const width=Math.round(w*dpr),height=Math.round(h*dpr);if(canvas.width!==width)canvas.width=width;if(canvas.height!==height)canvas.height=height;
          ctx.setTransform(dpr,0,0,dpr,0,0);ctx.clearRect(0,0,w,h);
          ctx.font=style.font;ctx.textBaseline='middle';
          const chars=parts(input.value),at=index=>chars.filter(c=>c.index<index).length,start=at(Math.min(input.selectionStart,input.selectionEnd)),end=at(Math.max(input.selectionStart,input.selectionEnd)),caret=at(input.selectionDirection==='backward'?input.selectionStart:input.selectionEnd),focused=document.activeElement===input;
          const shift=shiftOf(chars.length),inset=caret+shift>=count?0:Math.max(0,pitch-ctx.measureText(chars[caret]?.segment??'0').width)*(style.textAlign==='left'?0:style.textAlign==='right'?1:.5),caretX=(caret+shift)*pitch+inset;
          if(focused){if(caretX-offset>w-2)offset=caretX-w+2;if(caretX<offset)offset=caretX;}else offset=0;
          for(let i=0;i<Math.max(count,chars.length);i++){
            const g=i-shift,x=i*pitch-offset,r=ranges.filter(r=>i>=r.Start&&i-r.Start<r.Length),bg=r.filter(r=>r.Background).at(-1)?.Background,fg=r.filter(r=>r.Foreground).at(-1)?.Foreground;
            if(bg){ctx.fillStyle=bg;ctx.fillRect(x,0,pitch,h);}
            if(focused&&g>=start&&g<end){ctx.fillStyle=style.getPropertyValue('--selection');ctx.fillRect(x,0,pitch,h);}
            ctx.strokeStyle=style.borderColor;ctx.lineWidth=parseFloat(style.getPropertyValue('--cell-border'));if(i>0&&ctx.lineWidth>0){ctx.beginPath();ctx.moveTo(x,0);ctx.lineTo(x,h);ctx.stroke();}
            ctx.fillStyle=focused&&g>=start&&g<end?style.getPropertyValue('--selection-text'):fg||style.color;ctx.textAlign=style.textAlign==='left'?'left':style.textAlign==='right'?'right':'center';
            if(chars[g])ctx.fillText(chars[g].segment,x+(ctx.textAlign==='left'?0:ctx.textAlign==='right'?pitch:pitch/2),h/2);
          }
          if(focused&&start===end){ctx.fillStyle=style.color;ctx.fillRect(Math.min(w-1,caretX-offset),0,1,h);}
          const bad=chars.length>count||/[\r\n\t]/.test(input.value);field.classList.toggle('error',bad);input.setAttribute('aria-invalid',String(bad));
        }
        function hit(e){
          const rect=field.getBoundingClientRect(),style=getComputedStyle(field),x=(e.clientX-rect.left)*field.offsetWidth/rect.width+offset,chars=parts(input.value);
          let best=0,distance=Infinity;const shift=shiftOf(chars.length);
          for(let i=0;i<=chars.length;i++){const inset=i+shift>=count?0:Math.max(0,pitch-ctx.measureText(chars[i]?.segment??'0').width)*(style.textAlign==='left'?0:style.textAlign==='right'?1:.5),d=Math.abs(x-(i+shift)*pitch-inset);if(d<distance){best=i;distance=d;}}
          return chars[best]?.index??input.value.length;
        }
        input.addEventListener('pointerdown',e=>{e.preventDefault();input.focus();anchor=e.shiftKey?input.selectionStart:hit(e);input.setSelectionRange(anchor,hit(e));drag=true;input.setPointerCapture(e.pointerId);schedule(draw);});
        input.addEventListener('pointermove',e=>{if(drag){const end=hit(e);input.setSelectionRange(Math.min(anchor,end),Math.max(anchor,end),end<anchor?'backward':'forward');schedule(draw);}});
        input.addEventListener('pointerup',()=>{drag=false;});input.addEventListener('lostpointercapture',()=>{drag=false;});
        ['input','select','keyup','focus','blur','compositionupdate','compositionend'].forEach(event=>input.addEventListener(event,()=>schedule(draw)));gridDraw.set(input,draw);drawAll.push(draw);draw();
      });
      let selectedGrid;
      document.addEventListener('selectionchange',()=>{const draw=gridDraw.get(document.activeElement);schedule(selectedGrid);schedule(draw);selectedGrid=draw;});
      window.addEventListener('resize',redrawAll);(function watch(){const q=matchMedia('(resolution: '+(devicePixelRatio||1)+'dppx)');q.addEventListener('change',()=>{redrawAll();watch();},{once:true});})();document.fonts.ready.then(redrawAll);
      const themeChanges=new MutationObserver(redrawAll);[document.documentElement,document.body].forEach(root=>themeChanges.observe(root,{attributes:true,attributeFilter:['class','style']}));
      document.querySelectorAll('textarea[data-scroll=false]').forEach(input=>{
        const fits=value=>{
          const clone=input.cloneNode(false),style=getComputedStyle(input);clone.value=value;
          Object.assign(clone.style,{position:'fixed',left:'-100000px',top:'0',visibility:'hidden',font:style.font,border:'0',resize:'none',minWidth:'0',minHeight:'0',width:input.offsetWidth+'px',height:input.offsetHeight+'px'});
          document.body.append(clone);const size={width:clone.scrollWidth,height:clone.scrollHeight,fits:clone.scrollWidth<=clone.clientWidth+1&&clone.scrollHeight<=clone.clientHeight+1};clone.remove();return size;
        };
        const accepts=(next,old=input.value)=>{const after=fits(next),before=fits(old);return after.fits||next.length<=old.length&&after.height<=before.height&&after.width<=before.width;};
        const replace=text=>input.value.slice(0,input.selectionStart)+text+input.value.slice(input.selectionEnd);
        let composing=false,composition;
        input.addEventListener('beforeinput',event=>{if(!composing&&!event.isComposing&&event.inputType.startsWith('insert')){const text=event.data??(event.inputType==='insertLineBreak'||event.inputType==='insertParagraph'?'\n':null);if(text!=null&&!accepts(replace(text)))event.preventDefault();}});
        input.addEventListener('paste',event=>{const text=event.clipboardData?.getData('text/plain');if(text!=null&&!accepts(replace(text)))event.preventDefault();});
        input.addEventListener('compositionstart',()=>{composing=true;composition=[input.value,input.selectionStart,input.selectionEnd];});
        input.addEventListener('compositionend',()=>{composing=false;if(composition&&!accepts(input.value,composition[0])){input.value=composition[0];input.setSelectionRange(composition[1],composition[2]);}composition=null;});
      });
      const validate=()=>{drawAll.forEach(draw=>draw());document.querySelectorAll('.structure:not(.characters) > input:not([type=checkbox]):not(.calendar-picker),.structure > textarea').forEach(input=>input.closest('.structure').classList.toggle('error',input.scrollWidth>input.clientWidth+1||input.scrollHeight>input.clientHeight+1||!validDate(input)));return !document.querySelector('.error');};
      document.querySelector('#save').onclick=()=>{
        document.querySelectorAll('input').forEach(input=>{input.setAttribute('value',input.value);if(input.type==='checkbox'){input.toggleAttribute('checked',input.checked);input.setAttribute('data-mixed',String(input.indeterminate));}});document.querySelectorAll('textarea').forEach(input=>input.textContent=input.value);
        const url=URL.createObjectURL(new Blob(['<!doctype html>\n'+document.documentElement.outerHTML],{type:'text/html;charset=utf-8'})),a=document.createElement('a');a.href=url;a.download='paper-form.html';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);
      };
      document.querySelector('#print').onclick=()=>{if(validate())window.print();else document.querySelector('.notice').textContent=__overflow__;};
      let previousFocus;
      window.addEventListener('beforeprint',()=>{
        previousFocus=document.activeElement;previousFocus?.blur();const valid=validate();document.body.toggleAttribute('data-invalid',!valid);
        if(!valid)document.querySelector('.notice').textContent=__overflow__+'\n'+[...document.querySelectorAll('.error input,.error textarea')].map(input=>(input.getAttribute('aria-label')||input.name)+': '+input.value).join('\n');
      });
      window.addEventListener('afterprint',()=>{document.body.removeAttribute('data-invalid');previousFocus?.focus({preventScroll:true});});
    })();
    """;
}

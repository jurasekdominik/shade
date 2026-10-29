using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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

/// <summary>Standalone editable HTML and flattened PDF, both using the authored page coordinates.</summary>
public static class PaperFormExport
{
    /// <summary>Render a read-only page at its physical size. Must run on the UI thread after Look.Apply; the caller disposes the bitmap.</summary>
    public static RenderTargetBitmap RenderPage(this PaperFormView source, int pageIndex, int dpi = 144)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (pageIndex < 0 || pageIndex >= source.Definition.Pages.Count) throw new ArgumentOutOfRangeException(nameof(pageIndex));
        if (dpi is < 72 or > 600) throw new ArgumentOutOfRangeException(nameof(dpi));
        var size = source.Definition.PageSize;
        var pixels = new PixelSize((int)Math.Ceiling(size.Width * dpi / 25.4), (int)Math.Ceiling(size.Height * dpi / 25.4));
        if ((long)pixels.Width * pixels.Height > 40_000_000) throw new ArgumentException("Page raster exceeds 40 million pixels; reduce DPI.");
        var view = new PaperFormView { Definition = source.Definition, Values = source.Values, StructureTypes = source.StructureTypes, Mode = PaperFormMode.Preview, PageIndex = pageIndex };
        var window = new Window { Content = view, Width = size.Width * DipsPerMm, Height = size.Height * DipsPerMm, CanResize = false, ShowInTaskbar = false, ShowActivated = false, WindowDecorations = WindowDecorations.None, Opacity = 0 };
        try
        {
            window.Show(); Dispatcher.UIThread.RunJobs();
            view.Measure(new Size(size.Width * DipsPerMm, size.Height * DipsPerMm)); view.Arrange(new Rect(view.DesiredSize)); view.UpdateLayout();
            view.RefreshProblems();
            if (view.Problems.Count > 0) throw new InvalidOperationException(string.Join(" ", view.Problems.Select(p => p.Message).Distinct()));
            var bitmap = new RenderTargetBitmap(pixels, new Vector(dpi, dpi));
            try { bitmap.Render(view.Page); return bitmap; }
            catch { bitmap.Dispose(); throw; }
        }
        finally { window.Close(); }
    }

    /// <summary>A read-only PDF with one flattened image per page. Text is rasterized, not searchable. Invalid/overflowing forms are rejected before writing.</summary>
    public static void ExportPdf(this PaperFormView source, Stream destination, int dpi = 144)
    {
        ArgumentNullException.ThrowIfNull(destination); Dispatcher.UIThread.VerifyAccess();
        // Stage the export so a later invalid page cannot leave the host with a partial PDF.
        using var document = new MemoryStream(); var offsets = new List<long> { 0 };
        void Write(string text) { var bytes = Encoding.ASCII.GetBytes(text); document.Write(bytes); }
        void Object(int id, string body) { offsets.Add(document.Position); Write($"{id} 0 obj\n{body}\nendobj\n"); }
        void StreamObject(int id, string header, byte[] data) { offsets.Add(document.Position); Write($"{id} 0 obj\n<< {header} /Length {data.Length} >>\nstream\n"); document.Write(data); Write("\nendstream\nendobj\n"); }
        int count = source.Definition.Pages.Count; var size = source.Definition.PageSize;
        Write("%PDF-1.4\n"); Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
        Object(2, $"<< /Type /Pages /Count {count} /Kids [ {string.Join(" ", Enumerable.Range(0, count).Select(i => (3 + i * 3) + " 0 R"))} ] >>");
        for (int i = 0; i < count; i++)
        {
            using var bitmap = source.RenderPage(i, dpi);
            using var rgb = new WriteableBitmap(bitmap.PixelSize, bitmap.Dpi, PixelFormat.Rgba8888, AlphaFormat.Unpremul);
            using var compressed = new MemoryStream();
            using (var framebuffer = rgb.Lock())
            {
                bitmap.CopyPixels(framebuffer);
                var row = new byte[framebuffer.Size.Width * 4]; var output = new byte[framebuffer.Size.Width * 3];
                using var zip = new ZLibStream(compressed, CompressionLevel.Optimal, true);
                for (int y = 0; y < framebuffer.Size.Height; y++)
                {
                    System.Runtime.InteropServices.Marshal.Copy(framebuffer.Address + y * framebuffer.RowBytes, row, 0, row.Length);
                    for (int x = 0; x < framebuffer.Size.Width; x++)
                        for (int c = 0; c < 3; c++) output[x * 3 + c] = (byte)((row[x * 4 + c] * row[x * 4 + 3] + 255 * (255 - row[x * 4 + 3])) / 255);
                    zip.Write(output);
                }
            }
            int id = 3 + i * 3; string width = Number(size.Width * 72 / 25.4), height = Number(size.Height * 72 / 25.4);
            Object(id, $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {width} {height}] /Resources << /XObject << /Im0 {id + 2} 0 R >> >> /Contents {id + 1} 0 R >>");
            StreamObject(id + 1, "", Encoding.ASCII.GetBytes($"q\n{width} 0 0 {height} 0 0 cm\n/Im0 Do\nQ"));
            StreamObject(id + 2, $"/Type /XObject /Subtype /Image /Width {bitmap.PixelSize.Width} /Height {bitmap.PixelSize.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode", compressed.ToArray());
        }
        long xref = document.Position; Write($"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
        foreach (long offset in offsets.Skip(1)) Write(offset.ToString("D10", System.Globalization.CultureInfo.InvariantCulture) + " 00000 n \n");
        Write($"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n"); document.Position = 0; document.CopyTo(destination);
    }

    /// <summary>Self-contained editable HTML. Save HTML in the exported page persists current inputs into a new standalone file.</summary>
    public static string ExportHtml(this PaperFormView source)
    {
        source.Definition.Validate(); source.RefreshProblems();
        if (source.Problems.Count > 0) throw new InvalidOperationException(string.Join(" ", source.Problems.Select(p => p.Message).Distinct()));
        var definition = source.Definition; var page = definition.PageSize;
        string H(string? value) => WebUtility.HtmlEncode(value ?? "");
        string Color(string? value, Tone role) => H(value == null ? HexOf(role) : CssColor(value, HexOf(role)));
        var html = new StringBuilder("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>Paper form</title><style>");
        html.Append("*{box-sizing:border-box}body{margin:0;padding:16px;background:").Append(H(HexOf(Look.Background))).Append(";color:").Append(H(HexOf(Look.Text))).Append(";font-family:Inter,sans-serif}nav{display:flex;gap:8px;margin-bottom:16px}button{font:inherit;padding:4px 12px;cursor:pointer}.page{position:relative;margin:0 auto 16px;break-after:page;overflow:visible}.structure{position:absolute;box-sizing:border-box}.structure input,.structure textarea{display:block;width:100%;height:100%;min-width:0;min-height:0;padding:0;border:0;border-radius:0;background:transparent;color:inherit;font:inherit;text-align:inherit;outline:none}.structure:focus-within{outline:1px solid ").Append(H(HexOf(FocusBorder))).Append("}.structure textarea{resize:none;line-height:normal}.caption{display:flex;flex-direction:column}.caption label,.caption small{font:inherit;flex:none}.caption input{flex:1;min-height:0}.check{display:flex;align-items:center;gap:8px}.check input{appearance:none;width:var(--indicator);height:var(--indicator);flex:none;border:inherit;background:inherit;position:relative}.check input:checked:after{content:'✓';position:absolute;inset:0;text-align:center;font:inherit;line-height:var(--indicator)}.character-container:focus-within{outline:none}.characters{overflow:hidden}.characters:after{content:'';position:absolute;inset:0;pointer-events:none;border:calc(var(--cell-border)*1px) solid var(--cell-border-color)}.characters canvas{position:absolute;inset:0;width:100%;height:100%;pointer-events:none}.characters input{color:transparent;caret-color:transparent;position:absolute;inset:0;opacity:.01}.error{outline:2px solid ").Append(H(HexOf(Danger))).Append("}.notice{white-space:pre-wrap} @page{size:").Append(Number(page.Width)).Append("mm ").Append(Number(page.Height)).Append("mm;margin:0}@media print{body{padding:0;background:none}nav,.notice{display:none}.page{margin:0;break-after:page}.page:last-of-type{break-after:auto}.structure:focus-within{outline:none}*{print-color-adjust:exact;-webkit-print-color-adjust:exact}}</style></head><body><nav><button id=\"save\" type=\"button\">").Append(H(T("Save HTML"))).Append("</button><button id=\"print\" type=\"button\">").Append(H(T("Print"))).Append("</button></nav><p class=\"notice\" role=\"status\"></p>");
        void Structure(PaperStructure s, double parentBorder = 0)
        {
            var value = source.Value(s); string text = H(value?.Text), readOnly = s.IsReadOnly ? " readonly" : "";
            string family = "'" + (s.FontFamily ?? PaperFormView.DocumentFont.Name).Replace("\\", "\\\\").Replace("'", "\\'").Replace("\r", "").Replace("\n", "") + "'";
            string style = $"left:{Number(s.Bounds.X - parentBorder)}mm;top:{Number(s.Bounds.Y - parentBorder)}mm;width:{Number(s.Bounds.Width)}mm;height:{Number(s.Bounds.Height)}mm;background:{Color(s.Background ?? definition.Background, Raised)};color:{Color(s.Foreground ?? definition.Foreground, Look.Text)};border:{Number(s.BorderWidth)}mm solid {Color(s.BorderColor, Look.Separator)};font-size:{Number(s.FontSize)}px;font-family:{H(family)};text-align:{s.Alignment.ToString().ToLowerInvariant()};";
            string kind = s.Kind, name = H(s.Label.Length > 0 ? s.Label : s.FieldKey.Length > 0 ? s.FieldKey : s.Caption);
            style += "--selection:" + H(CssColor(((Avalonia.Media.Color)Selection).ToString(), "transparent")) + ";--selection-text:" + Color(s.Foreground ?? definition.Foreground, Look.Text) + ";";
            if (kind == "characters") style += $"--cell-border:{Number(s.BorderWidth * DipsPerMm)};border:0 solid {Color(s.BorderColor, Look.Separator)};--cell-border-color:{Color(s.BorderColor, Look.Separator)};";
            if (kind == "characters" && s.GridAlignment != Avalonia.Media.TextAlignment.Left) style += "background:transparent;";
            if (kind is "caption" or "check" or "line") style += "border:0;background:transparent;";
            if (kind == "check") style += "--indicator:" + Number(Math.Min(s.Bounds.Width, s.Bounds.Height)) + "mm;";
            html.Append("<div class=\"structure ").Append(H(kind == "characters" && s.GridAlignment != Avalonia.Media.TextAlignment.Left ? "character-container" : kind)).Append("\" style=\"").Append(style).Append("\" data-id=\"").Append(H(s.Id)).Append("\">");
            string frame = $"border:{Number(s.BorderWidth)}mm solid {Color(s.BorderColor, Look.Separator)};background:{Color(s.Background ?? definition.Background, Raised)};";
            string inset = $"padding:{Number(s.TextPadding)}mm;";
            string dateAttributes = kind == "date" ? $" data-date-format=\"{H(s.DateFormat)}\" placeholder=\"{H(s.DateFormat)}\"" : "";
            string input = $"<input aria-label=\"{name}\" name=\"{H(s.FieldKey)}\" value=\"{text}\"{dateAttributes}{(kind is "caption" or "text" or "date" ? " style=\"" + (kind == "caption" ? frame : "") + inset + "\"" : "")}{readOnly}>";
            if (source.FindTemplate(kind) is { } template) html.Append(template.ExportHtml(s, value));
            else if (kind == "group") { html.Append(H(s.Caption)); foreach (var child in s.Children) Structure(child, s.BorderWidth); }
            else if (kind == "label") html.Append(H(s.Caption));
            else if (kind == "line")
            {
                bool vertical = s.LineOrientation == Avalonia.Layout.Orientation.Vertical;
                html.Append("<span style=\"position:absolute;background:").Append(Color(s.BorderColor, Look.Separator)).Append(vertical ? ";height:100%;width:" : ";width:100%;height:").Append(Number(s.BorderWidth)).Append(vertical ? "mm;left:50%;top:0;transform:translateX(-50%)\"></span>" : "mm;top:50%;left:0;transform:translateY(-50%)\"></span>");
            }
            else if (kind == "check") html.Append("<input type=\"checkbox\" aria-label=\"").Append(name).Append("\" name=\"").Append(H(s.FieldKey)).Append("\" style=\"").Append(frame).Append('"').Append(value?.Checked == true ? " checked" : "").Append(value != null && value.Checked == null ? " data-mixed=\"true\"" : "").Append(s.IsReadOnly ? " disabled" : "").Append("><svg aria-hidden=\"true\" viewBox=\"0 0 16 16\"><path class=\"checkmark\" d=\"M4.16 8.32 L6.88 10.88 L11.84 5.44\"/><path class=\"dash\" d=\"M4.48 8 L11.52 8\"/></svg><span>").Append(H(s.Caption)).Append("</span>");
            else if (kind == "multiline") html.Append("<textarea aria-label=\"").Append(name).Append("\" name=\"").Append(H(s.FieldKey)).Append("\" data-scroll=\"").Append(s.AllowMultilineScrolling ? "true" : "false").Append("\" style=\"overflow:").Append(s.AllowMultilineScrolling ? "auto;" : "hidden;").Append(inset).Append('"').Append(readOnly).Append('>').Append(text).Append("</textarea>");
            else if (kind == "caption") html.Append("<label>").Append(H(s.Caption)).Append("</label>").Append(input).Append("<small>").Append(H(s.Subtitle)).Append("</small>");
            else if (kind == "characters")
            {
                bool aligned = s.GridAlignment != Avalonia.Media.TextAlignment.Left;
                if (aligned)
                {
                    double insetMm = Math.Max(0, s.Bounds.Width - s.CellCount * s.CellWidth) * (s.GridAlignment == Avalonia.Media.TextAlignment.Right ? 1 : .5);
                    html.Append("<div class=\"structure characters\" style=\"position:absolute;top:0;left:").Append(Number(insetMm)).Append("mm;width:").Append(Number(s.CellCount * s.CellWidth)).Append("mm;height:100%;background:").Append(Color(s.Background ?? definition.Background, Raised)).Append(";border:0 solid ").Append(Color(s.BorderColor, Look.Separator)).Append("\">");
                }
                html.Append("<canvas data-count=\"").Append(s.CellCount).Append("\" data-width=\"").Append(Number(s.CellWidth * DipsPerMm)).Append("\" data-ranges=\"").Append(H(JsonSerializer.Serialize(s.CellRanges.Select(r => r with { Background = r.Background == null ? null : CssColor(r.Background, "transparent"), Foreground = r.Foreground == null ? null : CssColor(r.Foreground, "inherit") })))).Append("\"></canvas>").Append(input);
                if (aligned) html.Append("</div>");
            }
            else html.Append(input);
            html.Append("</div>");
        }
        int pageNumber = 0;
        foreach (var p in definition.Pages)
        {
            html.Append("<section class=\"page\" style=\"width:").Append(Number(page.Width)).Append("mm;height:").Append(Number(page.Height)).Append("mm;background:").Append(Color(definition.Background, Raised)).Append(";color:").Append(Color(definition.Foreground, Look.Text)).Append("\">");
            foreach (var s in p.Structures) Structure(s);
            pageNumber++;
            if (definition.ShowPageNumbers) html.Append("<footer style=\"position:absolute;left:0;bottom:0;width:100%;text-align:center;height:").Append(Number(definition.MarginBottom)).Append("mm;font-size:").Append(Number(TextSize.Caption)).Append("px;font-family:'").Append(H(PaperFormView.DocumentFont.Name)).Append("'\">").Append(H(T("Page {0} of {1}", pageNumber, definition.Pages.Count))).Append("</footer>");
            html.Append("</section>");
        }
        foreach (var file in new[] { "OFL.txt", "Inter/OFL.txt" })
        {
            using var license = new StreamReader(AssetLoader.Open(new Uri("avares://Shade/Assets/" + file)));
            html.Append("<template class=\"font-license\">").Append(H(license.ReadToEnd())).Append("</template>");
        }
        html.Append("<script>").Append(HtmlScript.Replace("__overflow__", JsonSerializer.Serialize(T("Some values exceed their field bounds. Correct the highlighted fields before printing.")))).Append("</script></body></html>");
        html.Replace("font-family:Inter,sans-serif", "font-family:ShadeInter,sans-serif");
        html.Replace("</style>", ".structure input::selection,.structure textarea::selection{background:var(--selection);color:var(--selection-text)}</style>");
        html.Replace("</style>", ".structure input,.structure textarea{margin:0}.check input:disabled{opacity:1}.check input:checked:after{content:none}.check svg{position:absolute;left:0;top:50%;transform:translateY(-50%);width:var(--indicator);height:var(--indicator);pointer-events:none}.check svg path{display:none;fill:none;stroke:currentColor;stroke-width:1.8;stroke-linecap:round;stroke-linejoin:round}.check input:checked+svg .checkmark,.check input:indeterminate+svg .dash{display:block}@media print{body[data-invalid] .page{display:none}body[data-invalid] .notice{display:block;white-space:pre-wrap;overflow-wrap:anywhere}}</style>");
        html.Replace("<title>Paper form</title>", "<title>" + H(T("Paper form")) + "</title>"); return html.ToString();
    }

    // The input owns browser editing, composition, clipboard and undo. Canvas supplies the same fixed grapheme pitch for every font.
    const string HtmlScript = """
    (()=>{
      const segmenter=new Intl.Segmenter(undefined,{granularity:'grapheme'}), parts=s=>[...segmenter.segment(s)];
      document.querySelectorAll('input[type=checkbox][data-mixed=true]').forEach(input=>input.indeterminate=true);
      const drawAll=[];
      function validDate(input){
        const format=input.dataset.dateFormat;if(!format||!input.value)return true;
        const tokens=format.match(/yyyy|MM|M|dd|d/g),escape=s=>s.replace(/[.*+?^${}()|[\]\\]/g,'\\$&');
        let pattern='',at=0;for(const match of format.matchAll(/yyyy|MM|M|dd|d/g)){pattern+=escape(format.slice(at,match.index))+(match[0]==='yyyy'?'(\\d{4})':match[0].length===2?'(\\d{2})':'(\\d{1,2})');at=match.index+match[0].length;}pattern+=escape(format.slice(at));
        const match=input.value.match(new RegExp('^'+pattern+'$'));if(!match)return false;
        const parts=Object.fromEntries(tokens.map((t,i)=>[t[0],+match[i+1]])),date=new Date(0);date.setUTCFullYear(parts.y,parts.M-1,parts.d);
        return parts.y>=1&&parts.y<=9999&&date.getUTCFullYear()===parts.y&&date.getUTCMonth()===parts.M-1&&date.getUTCDate()===parts.d;
      }
      document.querySelectorAll('[data-date-format]').forEach(input=>input.addEventListener('input',()=>{const bad=!validDate(input);input.closest('.structure').classList.toggle('error',bad);input.setAttribute('aria-invalid',String(bad));}));
      document.querySelectorAll('.characters').forEach(field=>{
        const input=field.querySelector('input'),canvas=field.querySelector('canvas'),ctx=canvas.getContext('2d'),count=+canvas.dataset.count,pitch=+canvas.dataset.width,ranges=JSON.parse(canvas.dataset.ranges);
        let offset=0,drag=false,anchor=0,composing=false,composition;
        const accepts=(value,old=input.value)=>!/[\r\n\t]/.test(value)&&(parts(value).length<=count||parts(value).length<=parts(old).length);
        const replacement=text=>input.value.slice(0,input.selectionStart)+text+input.value.slice(input.selectionEnd);
        input.addEventListener('beforeinput',event=>{if(!composing&&!event.isComposing&&event.inputType.startsWith('insert')&&event.data!=null&&!accepts(replacement(event.data)))event.preventDefault();});
        input.addEventListener('paste',event=>{const text=event.clipboardData?.getData('text/plain');if(text!=null&&!accepts(replacement(text)))event.preventDefault();});
        input.addEventListener('compositionstart',()=>{composing=true;composition=[input.value,input.selectionStart,input.selectionEnd];});
        input.addEventListener('compositionend',()=>{composing=false;if(composition&&!accepts(input.value,composition[0])){input.value=composition[0];input.setSelectionRange(composition[1],composition[2]);}composition=null;draw();});
        function draw(){
          const style=getComputedStyle(field),w=field.clientWidth,h=field.clientHeight,dpr=devicePixelRatio||1;
          canvas.width=Math.round(w*dpr);canvas.height=Math.round(h*dpr);ctx.scale(dpr,dpr);ctx.clearRect(0,0,w,h);
          ctx.font=style.font;ctx.textBaseline='middle';
          const chars=parts(input.value),at=index=>chars.filter(c=>c.index<index).length,start=at(Math.min(input.selectionStart,input.selectionEnd)),end=at(Math.max(input.selectionStart,input.selectionEnd)),caret=at(input.selectionDirection==='backward'?input.selectionStart:input.selectionEnd),focused=document.activeElement===input;
          const inset=caret>=count?0:Math.max(0,pitch-ctx.measureText(chars[caret]?.segment??'0').width)*(style.textAlign==='left'?0:style.textAlign==='right'?1:.5),caretX=caret*pitch+inset;
          if(focused){if(caretX-offset>w-2)offset=caretX-w+2;if(caretX<offset)offset=caretX;}else offset=0;
          for(let i=0;i<Math.max(count,chars.length);i++){
            const x=i*pitch-offset,r=ranges.filter(r=>i>=r.Start&&i-r.Start<r.Length),bg=r.filter(r=>r.Background).at(-1)?.Background,fg=r.filter(r=>r.Foreground).at(-1)?.Foreground;
            if(bg){ctx.fillStyle=bg;ctx.fillRect(x,0,pitch,h);}
            if(focused&&i>=start&&i<end){ctx.fillStyle=style.getPropertyValue('--selection');ctx.fillRect(x,0,pitch,h);}
            ctx.strokeStyle=style.borderColor;ctx.lineWidth=parseFloat(style.getPropertyValue('--cell-border'));if(i>0&&ctx.lineWidth>0){ctx.beginPath();ctx.moveTo(x,0);ctx.lineTo(x,h);ctx.stroke();}
            ctx.fillStyle=focused&&i>=start&&i<end?style.getPropertyValue('--selection-text'):fg||style.color;ctx.textAlign=style.textAlign==='left'?'left':style.textAlign==='right'?'right':'center';
            if(chars[i])ctx.fillText(chars[i].segment,x+(ctx.textAlign==='left'?0:ctx.textAlign==='right'?pitch:pitch/2),h/2);
          }
          if(focused&&start===end){ctx.fillStyle=style.color;ctx.fillRect(Math.min(w-1,caretX-offset),0,1,h);}
          const bad=chars.length>count||/[\r\n\t]/.test(input.value);field.classList.toggle('error',bad);input.setAttribute('aria-invalid',String(bad));
        }
        function hit(e){
          const rect=field.getBoundingClientRect(),style=getComputedStyle(field),x=(e.clientX-rect.left)*field.offsetWidth/rect.width+offset,chars=parts(input.value);
          let best=0,distance=Infinity;
          for(let i=0;i<=chars.length;i++){const inset=i>=count?0:Math.max(0,pitch-ctx.measureText(chars[i]?.segment??'0').width)*(style.textAlign==='left'?0:style.textAlign==='right'?1:.5),d=Math.abs(x-i*pitch-inset);if(d<distance){best=i;distance=d;}}
          return chars[best]?.index??input.value.length;
        }
        input.addEventListener('pointerdown',e=>{e.preventDefault();input.focus();anchor=e.shiftKey?input.selectionStart:hit(e);input.setSelectionRange(anchor,hit(e));drag=true;input.setPointerCapture(e.pointerId);draw();});
        input.addEventListener('pointermove',e=>{if(drag){const end=hit(e);input.setSelectionRange(Math.min(anchor,end),Math.max(anchor,end),end<anchor?'backward':'forward');draw();}});
        input.addEventListener('pointerup',()=>{drag=false;});input.addEventListener('lostpointercapture',()=>{drag=false;});
        ['input','select','keyup','focus','blur','compositionupdate','compositionend'].forEach(event=>input.addEventListener(event,draw));drawAll.push(draw);draw();
      });
      document.addEventListener('selectionchange',()=>drawAll.forEach(draw=>draw()));window.addEventListener('resize',()=>drawAll.forEach(draw=>draw()));document.fonts.ready.then(()=>drawAll.forEach(draw=>draw()));
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
      const validate=()=>{drawAll.forEach(draw=>draw());document.querySelectorAll('.structure:not(.characters) > input:not([type=checkbox]),.structure > textarea').forEach(input=>input.closest('.structure').classList.toggle('error',input.scrollWidth>input.clientWidth+1||input.scrollHeight>input.clientHeight+1||!validDate(input)));return !document.querySelector('.error');};
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

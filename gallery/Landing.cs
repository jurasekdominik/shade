// Shade gallery - the landing page (docs/index.html, for GitHub Pages): gallery/landing.html filled with the tokens of both
// modes, so its colours, type and spacing are read from the code, and the chapters' renders for each mode.
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Avalonia.Media;
using Shade;
using static Shade.Look;

static class Landing
{
    static string Vars(Mode mode)
    {
        var hex = DesignDoc.Hexes(mode);
        var sb = new StringBuilder($"  :root[data-theme=\"{mode.ToString().ToLowerInvariant()}\"] {{ color-scheme: {mode.ToString().ToLowerInvariant()};");
        for (int i = 0; i < hex.Length; i++) sb.Append($" --{DesignDoc.Roles[i].key}: {hex[i]};");
        sb.Append($" --far: {HexOf(Far)}; --danger-text: {HexOf(DangerHi)};");
        Color line = Hairline;
        sb.Append($" --hairline: rgba({line.R}, {line.G}, {line.B}, {line.A / 255.0:0.##}); }}\n");
        return sb.ToString();
    }

    static string Colors()
    {
        var (dark, light) = (DesignDoc.Hexes(Mode.Dark), DesignDoc.Hexes(Mode.Light));
        SetMode(Mode.Dark);
        var sb = new StringBuilder();
        foreach (var (group, title, help) in new[] { ("Surface", "Surfaces", "From the page to the lines on it."), ("Text", "Text", "Three steps of emphasis, and Disabled."),
                     ("Accent", "Accent", "The app's one colour and what the rules make from it."), ("Semantic", "Status", "Fixed per mode; never the accent.") })
        {
            sb.Append($"      <div class=\"block\"><h3>{title}</h3><p class=\"help\">{help}</p><div class=\"swatches\">\n");
            for (int i = 0; i < DesignDoc.Roles.Length; i++)
            {
                var r = DesignDoc.Roles[i];
                if (r.group != group) continue;
                sb.Append($"        <div class=\"swatch\"><div class=\"chip\" style=\"background:var(--{r.key})\"></div><div class=\"meta\"><b>{r.name}</b>" +
                          $"<span class=\"mono only-dark\">{dark[i]}</span><span class=\"mono only-light\">{light[i]}</span><span class=\"use\">{WebUtility.HtmlEncode(r.use)}</span></div></div>\n");
            }
            sb.Append("      </div></div>\n");
        }
        return sb.ToString();
    }

    static string Type() => string.Concat(new[] { ("Title", Title, 600, "Page and dialog titles"), ("Section", Section, 600, "Block and card headings"),
            ("Body", Body, 400, "Fields, table cells, running text"), ("Label", Label, 500, "Labels, table headers, buttons"), ("Caption", Caption, 400, "Help, metadata") }
        .Select(t => $"        <div class=\"row\"><div class=\"sample\" style=\"font-size:{t.Item2}px;font-weight:{t.Item3}\">Quarterly total 1 234,56 €</div>" +
                     $"<div class=\"spec\">{t.Item1} · {t.Item2} px · {t.Item3} · {t.Item4}</div></div>\n"));

    static string Spacing() => string.Concat(Spaces.Select(s => $"        <div class=\"space-bar\"><span class=\"mono\">{s} px</span><i style=\"width:{s * 4}px\"></i></div>\n"));

    static string Corners() => "        <div class=\"corners\">" + string.Concat(new[] { ("Rows, sidebar", 0.0), ("Controls, menus", Radius), ("Dialogs, cards", CardRadius) }
        .Select(c => $"<div class=\"corner\"><div style=\"border-radius:{c.Item2}px\"></div><b>{c.Item1}</b><span class=\"mono\">{c.Item2} px</span></div>")) + "</div>\n";

    /// <summary>A link to the chapter as the Avalonia controls render it, in the mode being viewed.</summary>
    static string Compare(string name) => $"<span class=\"compare\">Compare with the Avalonia render: <a class=\"only-dark\" href=\"preview/dark/{name}.png\">dark</a>" +
                                          $"<a class=\"only-light\" href=\"preview/light/{name}.png\">light</a>.</span>";

    /// <summary>Every Lucide icon in Icons.cs as an SVG symbol ("i-chevron-down"): the page draws the same paths as the app.</summary>
    static string Icons(string repo)
    {
        var source = File.ReadAllText(Path.Combine(repo, "src", "Shade", "Controls", "Icons.cs"));
        return string.Concat(System.Text.RegularExpressions.Regex.Matches(source, @"(\w+) = Geometry\.Parse\(""([^""]+)""\)").Select(m =>
            $"  <symbol id=\"i-{Kebab(m.Groups[1].Value)}\" viewBox=\"0 0 24 24\"><path d=\"{m.Groups[2].Value}\"/></symbol>\n"));
    }
    static string Kebab(string name) => System.Text.RegularExpressions.Regex.Replace(name, "(?<=[a-z0-9])([A-Z])", "-$1").ToLowerInvariant();
    static string Icon(string name, string cls = "") => $"<svg class=\"s-i {cls}\" aria-hidden=\"true\"><use href=\"#i-{name}\"/></svg>";

    /// <summary>The accent picker (ColorGrid's three tabs): Look.Accents with both values, ColorGrid's palette, a mixer.</summary>
    static string Picker()
    {
        var sb = new StringBuilder("        <div class=\"s-picker\">\n          <div class=\"s-tabs\"><button aria-selected=\"true\" data-show=\"curated\">Curated</button>" +
                                   "<button aria-selected=\"false\" data-show=\"palette\">Palette</button><button aria-selected=\"false\" data-show=\"mixer\">Mixer</button></div>\n");
        sb.Append("          <div class=\"curated\" data-pane=\"curated\">");
        for (int i = 0; i < Accents.Length; i++)
        {
            var (name, d, l) = Accents[i];
            sb.Append($"<button class=\"cur{(i == 0 ? " current" : "")}\" data-name=\"{name}\" data-dark=\"{HexOf(d)}\" data-light=\"{HexOf(l)}\" style=\"--d:{HexOf(d)};--l:{HexOf(l)}\">" +
                      $"<i></i><span><b>{name}</b><em style=\"background:{HexOf(d)}\"></em><em style=\"background:{HexOf(l)}\"></em></span></button>");
        }
        sb.Append("</div>\n          <div class=\"palette\" data-pane=\"palette\" hidden>");
        var p = Shade.ColorGrid.Palette;
        int[] names = { 200, 300, 400, 500, 600 };
        for (int row = 0; row < p[0].shades.Length; row++)
            foreach (var hue in p) sb.Append($"<button type=\"button\" data-c=\"{hue.shades[row].ToUpperInvariant()}\" aria-label=\"{hue.hue}-{names[row]}\"></button>");
        sb.Append("</div>\n          <div data-pane=\"mixer\" hidden><div class=\"field\"><i class=\"knob\"></i></div><div class=\"hue\"><i class=\"knob\"></i></div></div>\n");
        return sb.Append("          <div class=\"foot\"><i></i><span class=\"mono\"></span><span class=\"what\"></span><span class=\"ratio\"></span></div>\n        </div>\n").ToString();
    }

    static string Rows() => string.Concat(Enumerable.Range(1, 8).Select(i =>
        $"          <tr data-menu=\"Open|Copy row|-|Delete\"><td>2026-10-{13 + i % 3}</td><td class=\"num\">{i}</td><td class=\"num\">{(80 + (i * 37 % 11) * 3.25).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}</td></tr>\n"));

    public static void Write(string repo)
    {
        string html = File.ReadAllText(Path.Combine(repo, "gallery", "landing.html"));
        var nav = string.Concat(new[] { ("colors", "Colors"), ("typography", "Typography"), ("components", "Components"), ("spacing", "Spacing · Radii"), ("responsive", "Responsive"), ("navigation", "Navigation") }
            .Select((c, i) => $"<a href=\"#{c.Item1}\"><span>{i + 1:00}</span>{c.Item2}</a>"));
        html = html.Replace("{{vars}}", Vars(Mode.Dark) + Vars(Mode.Light)).Replace("{{nav}}", nav).Replace("{{colors}}", Colors())
            .Replace("{{type}}", Type()).Replace("{{spacing}}", Spacing()).Replace("{{corners}}", Corners());
        html = html.Replace("{{icons}}", Icons(repo)).Replace("{{palette}}", Picker()).Replace("{{rows}}", Rows());
        foreach (var c in Showcase.Chapters.Select((c, i) => $"{i + 1:00}-{c.slug}")) html = html.Replace("{{compare:" + c + "}}", Compare(c));
        html = System.Text.RegularExpressions.Regex.Replace(html, @"\{\{icon:([a-z0-9-]+)(?::([a-z-]+))?\}\}", m => Icon(m.Groups[1].Value, m.Groups[2].Value));
        if (html.Contains("{{")) throw new InvalidOperationException("landing.html has a placeholder nothing fills: " + html.Substring(html.IndexOf("{{"), 30));
        SetMode(Mode.Dark);
        File.WriteAllText(Path.Combine(repo, "docs", "index.html"), html);
    }
}

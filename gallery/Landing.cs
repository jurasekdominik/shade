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
        .Select(t => $"        <div class=\"row\"><div class=\"sample\" style=\"font-size:{t.Item2}px;font-weight:{t.Item3}\">Daily yield 1 234,56 kWh</div>" +
                     $"<div class=\"spec\">{t.Item1} · {t.Item2} px · {t.Item3} · {t.Item4}</div></div>\n"));

    static string Spacing() => string.Concat(Spaces.Select(s => $"        <div class=\"bar\"><span class=\"mono\">{s} px</span><i style=\"width:{s * 4}px\"></i></div>\n"));

    static string Corners() => "        <div class=\"corners\">" + string.Concat(new[] { ("Rows, sidebar", 0.0), ("Controls, menus", Radius), ("Dialogs, cards", CardRadius) }
        .Select(c => $"<div class=\"corner\"><div style=\"border-radius:{c.Item2}px\"></div><b>{c.Item1}</b><span class=\"mono\">{c.Item2} px</span></div>")) + "</div>\n";

    /// <summary>A chapter's render for each mode; the page shows the one for its mode.</summary>
    static string Shot(string name) => $"<div class=\"shot\"><img class=\"only-dark\" src=\"preview/dark/{name}.png\" alt=\"{name}, dark\" loading=\"lazy\">" +
                                       $"<img class=\"only-light\" src=\"preview/light/{name}.png\" alt=\"{name}, light\" loading=\"lazy\"></div>";

    public static void Write(string repo)
    {
        string html = File.ReadAllText(Path.Combine(repo, "gallery", "landing.html"));
        var nav = string.Concat(new[] { ("colors", "Colors"), ("typography", "Typography"), ("components", "Components"), ("spacing", "Spacing · Radii"), ("responsive", "Responsive"), ("navigation", "Navigation") }
            .Select((c, i) => $"<a href=\"#{c.Item1}\"><span>{i + 1:00}</span>{c.Item2}</a>"));
        html = html.Replace("{{vars}}", Vars(Mode.Dark) + Vars(Mode.Light)).Replace("{{nav}}", nav).Replace("{{colors}}", Colors())
            .Replace("{{type}}", Type()).Replace("{{spacing}}", Spacing()).Replace("{{corners}}", Corners());
        foreach (var c in Showcase.Chapters.Select((c, i) => $"{i + 1:00}-{c.slug}")) html = html.Replace("{{shot:" + c + "}}", Shot(c));
        if (html.Contains("{{")) throw new InvalidOperationException("landing.html has a placeholder nothing fills: " + html.Substring(html.IndexOf("{{"), 30));
        SetMode(Mode.Dark);
        File.WriteAllText(Path.Combine(repo, "docs", "index.html"), html);
    }
}

// Shade gallery - DESIGN.md's front matter (the design.md token format) written from Look, so the doc can't drift from the code.
using System;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Media;
using Shade;
using static Shade.Look;

static class DesignDoc
{
    /// <summary>Every colour role as the docs name it: key, name, group, use. One list for the tokens, DESIGN.md's colour
    /// section and the landing page's swatches.</summary>
    public static readonly (string key, string name, string group, Tone tone, string use)[] Roles =
    {
        ("background", "Background", "Surface", Look.Background, "The page canvas"),
        ("raised", "Raised", "Surface", Raised, "Sidebar, fields, menus, table headers, dialogs"),
        ("fill", "Fill", "Surface", Fill, "Hover, Secondary buttons, tooltips"),
        ("separator", "Separator", "Surface", Separator, "Every resting 1 px line"),
        ("outline", "Outline", "Surface", Outline, "A control's border on hover or while open"),
        ("text", "Text", "Text", Text, "Main text"),
        ("text-2", "Text 2", "Text", Text2, "Labels, supporting text"),
        ("text-3", "Text 3", "Text", Text3, "Placeholders, metadata, hints"),
        ("disabled", "Disabled", "Text", Disabled, "Text of a control that can't be used"),
        ("accent", "Accent", "Accent", Accent, "Primary action, selection, focus, the active tab"),
        ("accent-hover", "Accent hover", "Accent", AccentHi, "The Primary button under the pointer"),
        ("accent-tint", "Accent tint", "Accent", AccentTint, "Selected rows, active chips, the sidebar's current row"),
        ("accent-text", "Accent text", "Accent", AccentText, "Small text in the accent, at 4.5:1"),
        ("on-accent", "On accent", "Accent", OnAccent, "Text on an accent fill"),
        ("success", "Success", "Semantic", Success, "OK, completed"),
        ("warning", "Warning", "Semantic", Warn, "Retrying, incomplete"),
        ("danger", "Danger", "Semantic", Danger, "Failed, deletes, actions that change the outside world"),
        ("danger-text", "Danger text", "Semantic", DangerText, "Danger as small text (the Danger button's label), at 4.5:1"),
        ("danger-tint", "Danger tint", "Semantic", DangerMuted, "The Danger button's fill"),
        ("info", "Info", "Semantic", Info, "Overrides, notices"),
    };

    /// <summary>Every role's hex in one mode.</summary>
    public static string[] Hexes(Mode mode) { SetMode(mode); return Roles.Select(r => HexOf(r.tone)).ToArray(); }

    static string Colors(Mode mode)
    {
        var hex = Hexes(mode);
        // primary: design.md's name for the main colour, which is Shade's accent
        return $"  primary: \"{hex[Array.FindIndex(Roles, r => r.key == "accent")]}\"\n" + string.Concat(Roles.Select((r, i) => $"  {r.key}: \"{hex[i]}\"\n"));
    }

    /// <summary>The token block between the file's first two "---" lines; the prose under it is kept.</summary>
    public static void Write(string path)
    {
        var sb = new StringBuilder("---\nversion: 1.0.0\nname: Shade\n");
        sb.Append("description: A dark-first, dense design system for desktop work tools, with a matching light mode. Thirteen colour roles named by job, one user-chosen accent that every shade is derived from by rule, Inter at five sizes, a 4 px grid, 4 px corners and 28 px controls.\n\n");
        sb.Append("colors:\n").Append(Colors(Mode.Dark)).Append("\ncolors-light:\n").Append(Colors(Mode.Light)).Append('\n');
        SetMode(Mode.Dark);
        sb.Append("typography:\n");
        foreach (var (key, px, w) in new[] { ("title", TextSize.Title, 600), ("section", TextSize.Section, 600), ("body", TextSize.Body, 400), ("label", TextSize.Label, 500), ("caption", TextSize.Caption, 400) })
            sb.Append($"  {key}:\n    fontFamily: Inter\n    fontSize: {px}px\n    fontWeight: {w}\n    letterSpacing: 0\n");
        sb.Append("  mono:\n    fontFamily: Cascadia Code\n    fontSize: 12px\n    fontWeight: 400\n    fontFeature: '\"liga\" 0, \"calt\" 0'\n\n");
        sb.Append($"rounded:\n  none: 0px\n  control: {Radius}px\n  card: {CardRadius}px\n\n");
        sb.Append("spacing:\n");
        foreach (var (key, v) in new[] { "xs", "sm", "md", "lg", "xl", "xxl" }.Zip(Spaces)) sb.Append($"  {key}: {v}px\n");
        sb.Append('\n');
        sb.Append(Components);
        sb.Append("---\n");
        string body = File.Exists(path) ? File.ReadAllText(path) : "\n";
        if (body.StartsWith("---\n", StringComparison.Ordinal)) { int end = body.IndexOf("\n---\n", 4, StringComparison.Ordinal); body = end < 0 ? body : body.Substring(end + 5); }
        File.WriteAllText(path, sb + Generated(body, "colors", ColorSection()));
    }

    /// <summary>The text between "&lt;!-- generated: name --&gt;" and "&lt;!-- /generated --&gt;" replaced; prose around it kept.</summary>
    static string Generated(string body, string name, string content)
    {
        string open = $"<!-- generated: {name} -->\n", close = "<!-- /generated -->";
        int a = body.IndexOf(open, StringComparison.Ordinal), b = a < 0 ? -1 : body.IndexOf(close, a, StringComparison.Ordinal);
        if (b < 0) throw new InvalidOperationException($"DESIGN.md has no {open.Trim()} ... {close} block");
        return body.Substring(0, a + open.Length) + content + body.Substring(b);
    }

    /// <summary>DESIGN.md's colour bullets, by group: name, token, dark hex, light hex, use.</summary>
    static string ColorSection()
    {
        var (dark, light) = (Hexes(Mode.Dark), Hexes(Mode.Light));
        SetMode(Mode.Dark);
        var sb = new StringBuilder();
        foreach (var g in new[] { ("Accent", "Brand & Accent"), ("Surface", "Surface"), ("Text", "Text"), ("Semantic", "Semantic") })
        {
            sb.Append($"### {g.Item2}\n");
            for (int i = 0; i < Roles.Length; i++)
                if (Roles[i].group == g.Item1) sb.Append($"- **{Roles[i].name}** (`{{colors.{Roles[i].key}}}` — {dark[i]} · light {light[i]}): {Roles[i].use}.\n");
            sb.Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>The components in design.md's own sub-tokens only (backgroundColor, textColor, typography, rounded, padding,
    /// size, height, width); a state is its own key (button-primary-hover). Borders, rings and layout are in the prose.</summary>
    const string Components = @"components:
  button-primary:
    backgroundColor: ""{colors.primary}""
    textColor: ""{colors.on-accent}""
    typography: ""{typography.label}""
    rounded: ""{rounded.control}""
    height: 28px
    padding: 12px
  button-primary-hover:
    backgroundColor: ""{colors.accent-hover}""
    textColor: ""{colors.on-accent}""
  button-secondary:
    backgroundColor: ""{colors.fill}""
    textColor: ""{colors.text}""
    typography: ""{typography.label}""
    rounded: ""{rounded.control}""
    height: 28px
    padding: 12px
  button-ghost:
    textColor: ""{colors.text-2}""
    typography: ""{typography.label}""
    rounded: ""{rounded.control}""
    height: 28px
    padding: 12px
  button-ghost-hover:
    backgroundColor: ""{colors.fill}""
    textColor: ""{colors.text}""
  button-danger:
    backgroundColor: ""{colors.danger-tint}""
    textColor: ""{colors.danger-text}""
    typography: ""{typography.label}""
    rounded: ""{rounded.control}""
    height: 28px
    padding: 12px
  button-disabled:
    backgroundColor: ""{colors.raised}""
    textColor: ""{colors.disabled}""
    typography: ""{typography.label}""
    rounded: ""{rounded.control}""
    height: 28px
  text-field:
    backgroundColor: ""{colors.raised}""
    textColor: ""{colors.text}""
    typography: ""{typography.body}""
    rounded: ""{rounded.control}""
    height: 28px
    padding: 8px
  text-field-placeholder:
    textColor: ""{colors.text-3}""
  label:
    textColor: ""{colors.text-2}""
    typography: ""{typography.label}""
  help:
    textColor: ""{colors.text-3}""
    typography: ""{typography.caption}""
  dropdown:
    backgroundColor: ""{colors.raised}""
    textColor: ""{colors.text}""
    typography: ""{typography.body}""
    rounded: ""{rounded.control}""
    height: 28px
    padding: 8px
  dropdown-item-current:
    backgroundColor: ""{colors.fill}""
    textColor: ""{colors.text}""
    height: 28px
  date-field:
    backgroundColor: ""{colors.raised}""
    textColor: ""{colors.text}""
    typography: ""{typography.body}""
    rounded: ""{rounded.control}""
    height: 28px
  date-field-picked-day:
    backgroundColor: ""{colors.accent}""
    textColor: ""{colors.on-accent}""
    rounded: ""{rounded.control}""
  divider:
    backgroundColor: ""{colors.separator}""
    height: 1px
  date-range-day:
    backgroundColor: ""{colors.accent-tint}""
    textColor: ""{colors.text}""
    size: 28px
  editable-table-cell:
    backgroundColor: ""{colors.background}""
    textColor: ""{colors.text}""
    typography: ""{typography.body}""
    rounded: ""{rounded.none}""
    height: 28px
    padding: 8px
  editable-table-footer:
    backgroundColor: ""{colors.fill}""
    textColor: ""{colors.text}""
    typography: ""{typography.body}""
    height: 28px
  menu:
    backgroundColor: ""{colors.raised}""
    textColor: ""{colors.text}""
    typography: ""{typography.body}""
    rounded: ""{rounded.control}""
  switch-on:
    backgroundColor: ""{colors.accent}""
    width: 32px
    height: 18px
  switch-off:
    backgroundColor: ""{colors.fill}""
    width: 32px
    height: 18px
  tick-on:
    backgroundColor: ""{colors.accent}""
    textColor: ""{colors.on-accent}""
    rounded: ""{rounded.control}""
    size: 16px
  state-toggle:
    backgroundColor: ""{colors.raised}""
    rounded: ""{rounded.control}""
    height: 28px
  mode-switch:
    backgroundColor: ""{colors.raised}""
    textColor: ""{colors.text-2}""
    typography: ""{typography.label}""
    rounded: ""{rounded.control}""
    height: 28px
  mode-switch-active:
    backgroundColor: ""{colors.fill}""
    textColor: ""{colors.text}""
  filter-chip:
    backgroundColor: ""{colors.raised}""
    textColor: ""{colors.text-2}""
    typography: ""{typography.label}""
    rounded: ""{rounded.control}""
    height: 24px
  filter-chip-active:
    backgroundColor: ""{colors.accent-tint}""
    textColor: ""{colors.accent-text}""
  tab-active:
    textColor: ""{colors.text}""
    typography: ""{typography.label}""
  tag:
    backgroundColor: ""{colors.fill}""
    textColor: ""{colors.text}""
    typography: ""{typography.label}""
    rounded: ""{rounded.control}""
    height: 24px
  tag-success:
    textColor: ""{colors.success}""
  tag-warning:
    textColor: ""{colors.warning}""
  tag-info:
    textColor: ""{colors.info}""
  status-badge:
    textColor: ""{colors.text}""
    typography: ""{typography.body}""
  table-header:
    backgroundColor: ""{colors.raised}""
    textColor: ""{colors.text-2}""
    typography: ""{typography.label}""
    height: 28px
    padding: 8px
  table-row:
    textColor: ""{colors.text}""
    typography: ""{typography.body}""
    rounded: ""{rounded.none}""
    height: 28px
    padding: 8px
  table-row-hover:
    backgroundColor: ""{colors.fill}""
  table-row-selected:
    backgroundColor: ""{colors.accent-tint}""
  table-designer-header:
    backgroundColor: ""{colors.raised}""
    textColor: ""{colors.text-2}""
    typography: ""{typography.label}""
    height: 28px
  tooltip:
    backgroundColor: ""{colors.fill}""
    textColor: ""{colors.text}""
    typography: ""{typography.label}""
    rounded: ""{rounded.control}""
    padding: 8px
  dialog:
    backgroundColor: ""{colors.background}""
    textColor: ""{colors.text}""
    rounded: ""{rounded.card}""
    padding: 24px
  dialog-button:
    width: 110px
    height: 28px
  color-picker:
    backgroundColor: ""{colors.raised}""
    rounded: ""{rounded.card}""
  accent-swatch:
    size: 24px
  empty-state:
    textColor: ""{colors.text-2}""
    typography: ""{typography.section}""
  summary-card:
    backgroundColor: ""{colors.raised}""
    textColor: ""{colors.text}""
    typography: ""{typography.label}""
    rounded: ""{rounded.card}""
    height: 84px
  summary-card-detailed:
    backgroundColor: ""{colors.raised}""
    typography: ""{typography.section}""
    rounded: ""{rounded.card}""
    height: 112px
  search-field:
    backgroundColor: ""{colors.raised}""
    textColor: ""{colors.text}""
    typography: ""{typography.body}""
    rounded: ""{rounded.control}""
    height: 28px
  progress-track:
    backgroundColor: ""{colors.fill}""
    height: 6px
  progress-bar:
    backgroundColor: ""{colors.accent}""
    height: 6px
  log:
    textColor: ""{colors.text-2}""
    typography: ""{typography.mono}""
  reference-card:
    backgroundColor: ""{colors.raised}""
    textColor: ""{colors.text}""
    typography: ""{typography.body}""
    rounded: ""{rounded.card}""
    padding: 12px
  sidebar:
    backgroundColor: ""{colors.raised}""
    textColor: ""{colors.text-2}""
    typography: ""{typography.body}""
    width: 180px
  sidebar-collapsed:
    width: 54px
  sidebar-row-current:
    backgroundColor: ""{colors.accent-tint}""
    textColor: ""{colors.text}""
    height: 28px
  status-bar:
    backgroundColor: ""{colors.raised}""
    textColor: ""{colors.text-2}""
    typography: ""{typography.caption}""
    height: 28px
  page-header:
    textColor: ""{colors.text}""
    typography: ""{typography.title}""
    padding: 16px
  block-heading:
    textColor: ""{colors.text}""
    typography: ""{typography.section}""
";
}

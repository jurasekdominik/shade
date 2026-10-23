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
        ("background", "Background", "Surface", Bg, "The page canvas"),
        ("raised", "Raised", "Surface", Surface, "Sidebar, fields, menus, table headers, dialogs"),
        ("fill", "Fill", "Surface", Surface2, "Hover, Secondary buttons, tooltips"),
        ("separator", "Separator", "Surface", Border, "Every resting 1 px line"),
        ("outline", "Outline", "Surface", MenuEdge, "A control's border on hover or while open"),
        ("text", "Text", "Text", Text, "Main text"),
        ("text-2", "Text 2", "Text", Second, "Labels, supporting text"),
        ("text-3", "Text 3", "Text", Faint, "Placeholders, metadata, hints"),
        ("disabled", "Disabled", "Text", Disabled, "Text of a control that can't be used"),
        ("accent", "Accent", "Accent", Accent, "Primary action, selection, focus, the active tab"),
        ("accent-hover", "Accent hover", "Accent", AccentHi, "The Primary button under the pointer"),
        ("accent-tint", "Accent tint", "Accent", Soft, "Selected rows, active chips, the sidebar's current row"),
        ("accent-text", "Accent text", "Accent", AccentText, "Small text in the accent, at 4.5:1"),
        ("on-accent", "On accent", "Accent", OnAccent, "Text on an accent fill"),
        ("success", "Success", "Semantic", Success, "OK, completed"),
        ("warning", "Warning", "Semantic", Warn, "Retrying, incomplete"),
        ("danger", "Danger", "Semantic", Danger, "Failed, deletes, actions that change the outside world"),
        ("danger-tint", "Danger tint", "Semantic", DangerMuted, "The Danger button's fill"),
        ("info", "Info", "Semantic", Override, "Overrides, notices"),
    };

    /// <summary>Every role's hex in one mode.</summary>
    public static string[] Hexes(Mode mode) { SetMode(mode); return Roles.Select(r => HexOf(r.tone)).ToArray(); }

    static string Colors(Mode mode)
    {
        var hex = Hexes(mode);
        return string.Concat(Roles.Select((r, i) => $"  {r.key}: \"{hex[i]}\"\n"));
    }

    /// <summary>The token block between the file's first two "---" lines; the prose under it is kept.</summary>
    public static void Write(string path)
    {
        var sb = new StringBuilder("---\nversion: 1.0.0\nname: Shade\n");
        sb.Append("description: A dark-first, dense design system for desktop work tools, with a matching light mode. Thirteen colour roles named by job, one user-chosen accent that every shade is derived from by rule, Inter at five sizes, a 4 px grid, 4 px corners and 28 px controls.\n\n");
        sb.Append("colors:\n").Append(Colors(Mode.Dark)).Append("\ncolors-light:\n").Append(Colors(Mode.Light)).Append('\n');
        SetMode(Mode.Dark);
        sb.Append("typography:\n");
        foreach (var (key, px, w) in new[] { ("title", Title, 600), ("section", Section, 600), ("body", Body, 400), ("label", Label, 500), ("caption", Caption, 400) })
            sb.Append($"  {key}:\n    fontFamily: Inter\n    fontSize: {px}px\n    fontWeight: {w}\n    letterSpacing: 0\n");
        sb.Append("  mono:\n    fontFamily: Cascadia Code\n    fontSize: 12px\n    fontWeight: 400\n    fontFeatures: no ligatures\n\n");
        sb.Append($"rounded:\n  none: 0px\n  control: {Radius}px\n  card: {CardRadius}px\n\n");
        sb.Append("spacing:\n");
        foreach (var (key, v) in new[] { "xs", "sm", "md", "lg", "xl", "xxl" }.Zip(Spaces)) sb.Append($"  {key}: {v}px\n");
        sb.Append($"\nsizes:\n  control-height: {ControlHeight}px\n  chip-height: {ChipHeight}px\n  sidebar: 180px\n  sidebar-collapsed: 54px\n  status-bar: 28px\n\n");
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

    const string Components = @"components:
  button-primary:
    backgroundColor: ""{colors.accent}""
    textColor: ""{colors.on-accent}""
    typography: ""{typography.label}""
    rounded: ""{rounded.control}""
    height: 28px
    padding: 0 12px
  button-primary-hover:
    backgroundColor: ""{colors.accent-hover}""
  button-secondary:
    backgroundColor: ""{colors.fill}""
    textColor: ""{colors.text}""
    borderColor: ""{colors.separator}""
    typography: ""{typography.label}""
    rounded: ""{rounded.control}""
    height: 28px
  button-ghost:
    textColor: ""{colors.text-2}""
    hoverBackgroundColor: ""{colors.fill}""
    typography: ""{typography.label}""
    rounded: ""{rounded.control}""
    height: 28px
  button-danger:
    backgroundColor: ""{colors.danger-tint}""
    textColor: ""{colors.danger}""
    typography: ""{typography.label}""
    rounded: ""{rounded.control}""
    height: 28px
  button-disabled:
    backgroundColor: ""{colors.raised}""
    textColor: ""{colors.disabled}""
    typography: ""{typography.label}""
    rounded: ""{rounded.control}""
    height: 28px
  text-field:
    backgroundColor: ""{colors.raised}""
    textColor: ""{colors.text}""
    placeholderColor: ""{colors.text-3}""
    borderColor: ""{colors.separator}""
    hoverBorderColor: ""{colors.outline}""
    focusBorderColor: ""{colors.accent}""
    invalidBorderColor: ""{colors.danger}""
    typography: ""{typography.body}""
    rounded: ""{rounded.control}""
    height: 28px
    padding: 0 8px
  suggestion-field:
    backgroundColor: ""{colors.raised}""
    borderColor: ""{colors.outline}""
    rounded: ""{rounded.control} {rounded.control} 0 0""
    maxSuggestions: 8
  label:
    textColor: ""{colors.text-2}""
    typography: ""{typography.label}""
    gapToField: 4px
  dropdown:
    backgroundColor: ""{colors.raised}""
    textColor: ""{colors.text}""
    borderColor: ""{colors.separator}""
    typography: ""{typography.body}""
    rounded: ""{rounded.control}""
    height: 28px
  dropdown-open:
    backgroundColor: ""{colors.raised}""
    borderColor: ""{colors.outline}""
    rounded: ""{rounded.control} {rounded.control} 0 0""
    itemHeight: 28px
    currentItemBackground: ""{colors.fill}""
  menu:
    backgroundColor: ""{colors.raised}""
    borderColor: ""{colors.separator}""
    rounded: ""{rounded.control}""
    itemHeight: 28px
  switch:
    width: 32px
    height: 18px
    onColor: ""{colors.accent}""
    offColor: ""{colors.fill}""
  tick:
    size: 16px
    rounded: ""{rounded.control}""
    onColor: ""{colors.accent}""
  state-toggle:
    height: 28px
    rounded: ""{rounded.control}""
    onColor: ""{colors.accent}""
    offColor: ""{colors.danger}""
    pendingBorder: ""{colors.warning}""
  filter-chip:
    height: 24px
    rounded: ""{rounded.control}""
  filter-chip-active:
    backgroundColor: ""{colors.accent-tint}""
    textColor: ""{colors.accent}""
  tab-active:
    textColor: ""{colors.text}""
    underline: 2px {colors.accent}
    typography: ""{typography.label}""
  tag:
    height: 24px
    rounded: ""{rounded.control}""
    backgroundColor: tint of its colour
  status-badge:
    dot: 8px
    gap: 6px
    typography: ""{typography.body}""
  table:
    headerBackground: ""{colors.raised}""
    headerText: ""{colors.text-2}""
    rowHeight: 28px
    rowDivider: 1px {colors.separator}
    hoverBackground: ""{colors.fill}""
    selectedBackground: ""{colors.accent-tint}""
  compact-table:
    rowHeight: 28px
    rowDivider: 1px {colors.separator}
    fadedRowOpacity: 0.65
  tooltip:
    backgroundColor: ""{colors.fill}""
    borderColor: ""{colors.outline}""
    textColor: ""{colors.text}""
    typography: ""{typography.label}""
    rounded: ""{rounded.control}""
    padding: 5px 8px
  dialog:
    backgroundColor: ""{colors.background}""
    rounded: ""{rounded.card}""
    padding: 24px
    buttons: Cancel then the action, bottom right, at least 96px
  color-picker:
    backgroundColor: ""{colors.raised}""
    borderColor: ""{colors.outline}""
    rounded: ""{rounded.card}""
  empty-state:
    title: ""{typography.section}""
    note: ""{colors.text-2}""
  disclosure:
    textColor: ""{colors.text-2}""
    typography: ""{typography.label}""
  summary-cards:
    backgroundColor: ""{colors.raised}""
    borderColor: ""{colors.separator}""
    activeBorderColor: ""{colors.accent}""
    rounded: ""{rounded.card}""
    height: 84px
    title: ""{typography.label}""
    value: 18px 600
    line: ""{typography.caption}""
  summary-cards-detailed:
    height: 112px
    title: ""{typography.section}""
    subtitle: ""{typography.caption}""
    value: 24px 600
    ring: 44px at the right
  search-field:
    backgroundColor: ""{colors.raised}""
    borderColor: ""{colors.separator}""
    iconColor: ""{colors.text-3}""
    rounded: ""{rounded.control}""
    height: 28px
  progress:
    height: 6px
    rounded: 3px
    trackColor: ""{colors.fill}""
    barColor: ""{colors.accent}""
    countdownColor: ""{colors.text-3}""
  reference-card:
    backgroundColor: ""{colors.raised}""
    borderColor: ""{colors.separator}""
    rounded: ""{rounded.card}""
    padding: 12px
    title: ""{typography.section}""
    code: ""{typography.mono}""
  tray-menu:
    backgroundColor: ""{colors.raised}""
    borderColor: ""{colors.separator}""
    rounded: ""{rounded.control}""
    itemHeight: 28px
  sidebar:
    backgroundColor: ""{colors.raised}""
    borderRight: 1px {colors.separator}
    width: 180px
    collapsedWidth: 54px
    itemHeight: 28px
    activeBackground: ""{colors.accent-tint}""
    activeMarker: 3px {colors.accent}
  status-bar:
    backgroundColor: ""{colors.raised}""
    borderTop: 1px {colors.separator}
    height: 28px
    typography: ""{typography.caption}""
  page-header:
    title: ""{typography.title}""
    hint: ""{colors.text-2}""
    padding: 16px
  block:
    title: ""{typography.section}""
    help: ""{colors.text-3}""
    lineAbove: 1px {colors.separator} at 60%
    layout: masonry, 32px between columns, 20px between blocks
";
}

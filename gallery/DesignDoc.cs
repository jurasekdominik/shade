// Shade gallery - DESIGN.md's front matter (the design.md token format) written from Look, so the doc can't drift from the code.
using System;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Media;
using static Shade.Look;

static class DesignDoc
{
    static string Colors(Mode mode)
    {
        SetMode(mode);
        var sb = new StringBuilder();
        foreach (var (key, c) in new[] { ("background", Bg), ("raised", Surface), ("fill", Surface2), ("separator", Border), ("outline", MenuEdge),
                     ("text", Text), ("text-2", Second), ("text-3", Faint), ("disabled", Disabled),
                     ("accent", Accent), ("accent-hover", AccentHi), ("accent-tint", Soft), ("accent-text", AccentText), ("on-accent", OnAccent),
                     ("success", Success), ("warning", Warn), ("danger", Danger), ("danger-tint", DangerMuted), ("info", Override) })
            sb.Append($"  {key}: \"{HexOf(c)}\"\n");
        return sb.ToString();
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
        File.WriteAllText(path, sb + body);
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
  filter-chip:
    height: 24px
    rounded: ""{rounded.control}""
    activeBackground: ""{colors.accent-tint}""
  tab-active:
    textColor: ""{colors.text}""
    underline: 2px {colors.accent}
    typography: ""{typography.label}""
  tag:
    height: 24px
    rounded: ""{rounded.control}""
    backgroundColor: tint of its colour
  table:
    headerBackground: ""{colors.raised}""
    headerText: ""{colors.text-2}""
    rowHeight: 28px
    rowDivider: 1px {colors.separator}
    hoverBackground: ""{colors.fill}""
    selectedBackground: ""{colors.accent-tint}""
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

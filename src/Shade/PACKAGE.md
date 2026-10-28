# Shade

A dark-first design system for Avalonia 12 desktop apps (.NET 10), with a matching light mode that switches live:
colour roles, type, spacing and the shared controls (buttons, fields, dropdowns, switches, tables, menus, dialogs,
sidebar, summary cards, progress), so an app stops rebuilding them.

```csharp
public override void Initialize() => Look.Apply(this, "MyApp");    // Fluent + Shade's styles, dark by default
// anywhere, live: Look.SetMode(Look.Mode.Light); Look.SetAccent(Look.Accents[5].dark, Look.Accents[5].light);
var save = new ActionButton("Save", ButtonKind.Primary);
var name = new Field("Name");
```

Or in XAML (`xmlns:shade="using:Shade"`): `<ToggleSwitch IsChecked="{Binding Enabled}" />`, `<shade:Field Text="{Binding Name}" />`.
Actions use native Avalonia Button contracts: `<shade:ActionButton Content="Save" Command="{Binding Save}" />`.
See `docs/ADOPTION.md`.

- Live page with every component, both modes: https://jurasekdominik.github.io/shade/
- The tokens and rules: `DESIGN.md` in this package (the design.md format, for people and coding agents), and the code: https://github.com/jurasekdominik/shade

For an app's coding agents: link the app's `AGENTS.md` to `docs/APP_AGENTS.md` in this package and read `DESIGN.md` plus
`docs/ADOPTION.md`. The guide covers building on Shade and filing a Shade issue directly when the API does not fit (apps never patch
controls). Use the docs matching the installed version.

Inter and Cascadia Code are embedded (SIL Open Font License); icons from Lucide (ISC). MIT licensed.

Editable tables can opt into the integrated column ribbon with `ShowColumnEditor = true`. It includes alignment, units, formulas, typography, width constraints and bindable paper formats. See `docs/ADOPTION.md` for bindings and print/export page-size integration.


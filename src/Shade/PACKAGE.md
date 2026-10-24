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

Or in XAML (`xmlns:shade="using:Shade"`): `<shade:Switch On="{Binding Enabled}" />`, `<shade:Field Text="{Binding Name}" />`.

- Live page with every component, both modes: https://jurasekdominik.github.io/shade/
- The tokens and rules: `DESIGN.md` in this package (the design.md format, for people and coding agents), and the code: https://github.com/jurasekdominik/shade

For an app's coding agents: UI uses only Shade. Find the control in `DESIGN.md` → *Choosing a component*; no raw Avalonia
input controls, literal colours or sizes in views. If nothing fits, propose a Shade component; never build one in the app.
The README has the rules block to paste and a one-line CI check.

Inter and Cascadia Code are embedded (SIL Open Font License); icons from Lucide (ISC). MIT licensed.

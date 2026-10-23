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
- The tokens and rules (DESIGN.md) and the code: https://github.com/jurasekdominik/shade

Inter and Cascadia Code are embedded (SIL Open Font License); icons from Lucide (ISC). MIT licensed.

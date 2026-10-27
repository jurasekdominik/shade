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

Or in XAML (`xmlns:shade="using:Shade"`): `<shade:Switch IsChecked="{Binding Enabled}" />`, `<shade:Field Text="{Binding Name}" />`.
Actions use native Avalonia Button contracts: `<shade:ActionButton Content="Save" Command="{Binding Save}" />`.
See `docs/ADOPTION.md` for the breaking 0.6 action/toggle migration and editable pair-row API and `docs/RELEASE_PLAN.md` for remaining release gates.

- Live page with every component, both modes: https://jurasekdominik.github.io/shade/
- The tokens and rules: `DESIGN.md` in this package (the design.md format, for people and coding agents), and the code: https://github.com/jurasekdominik/shade

For an app's coding agents: UI uses only Shade. Find the control in `DESIGN.md` → *Choosing a component*; no raw Avalonia
input controls, literal colours or sizes in views. If nothing fits, propose a Shade component; never build one in the app.
The README has the rules block to paste and a one-line CI check.

Inter and Cascadia Code are embedded (SIL Open Font License); icons from Lucide (ISC). MIT licensed.

Editable tables can opt into the integrated column ribbon with `ShowColumnEditor = true`. It includes alignment, units, formulas, typography, width constraints and bindable paper formats. See `docs/ADOPTION.md` for bindings and print/export page-size integration.

Shade 0.6.4 corrects localized ribbon numbers, invalid-draft completion, supplied font-size choices and duplicate formula-source labels. See `docs/ADOPTION.md` for the numeric and draft contracts.

Shade 0.6.5 adds `ConstrainToPaper` (true by default). Set false to edit across the host while retaining A4/A3/custom paper metadata. Ribbon fields measure localized content and expand into labelled groups when space permits. Applied layout numbers use friendly precision while preserving exact untouched geometry.

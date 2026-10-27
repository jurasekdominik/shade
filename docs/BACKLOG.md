# Backlog

Open gaps only; shipped work is in [CHANGELOG.md](../CHANGELOG.md). A backlog entry is a known gap, not a supported
feature or an exception to current rules ([CONTRIBUTING.md](../CONTRIBUTING.md)). Shade is a desktop component library,
not an application framework: improve the existing controls before adding component families. Report integration gaps
using [APP_AGENTS.md](APP_AGENTS.md).

## Next changes after app integration

| Priority | Concrete remaining gap | Next useful change | Acceptance evidence |
| --- | --- | --- | --- |
| 1 | Native platform input and screen-reader behavior are not certified by headless checks. | Exercise actual application forms, menus, toggles, dates and tables on the target OS; patch reproduced failures. | Real IME, focus, clipboard, drag/drop and Narrator/VoiceOver/Orca runs. |
| 2 | EditableTable cell value providers do not expose full grid/row/column relationships; row dictionaries do not notify arbitrary in-place edits. | Add relationships/source adapters when the consuming apps need them; preserve the current single editor and decimal calculation behavior. | Offscreen coordinates after sort/reorder, observable edits and unchanged caret; actual screen-reader runs. |
| 3 | Field/date composites have different native editing requirements; some date presentation and sidebar/navigation item options remain plain properties. | Extend bindings against actual integration needs; evaluate ordinary native TextBox presentation separately from segmented dates. | Compiled XAML, read-only/validation/IME behavior and unchanged visuals on the consuming layouts. |
| 4 | Unique-text Fit, very wide data, font assets and global theme scope have measurable workload-dependent costs. | Profile consuming apps before column virtualization, font/package splitting or scoped tokens. | Same-data before/after timings, allocations, rendering and functionality checks. |

## API standards to apply as each component changes

- Bind changing application state through Avalonia properties, with a parameterless constructor for XAML. Keep current
  constructor/method adapters when they can delegate to the same implementation.
- Separate model updates from user actions: both notify bindings, only user input raises the action/change event.
- State whether a collection is a snapshot or observed, how invalid keys are handled, and whether order is meaningful.
  Use immutable snapshots for small option lists; retain observable collections where table structure already supports them.
- Keep stable identity separate from display text when a consuming project needs translated or renamed domain options.
  Today's string-based pickers use stable names. MultiChoice retains keys while options are empty, then reconciles loaded
  options. Do not add a generic item framework without an integration case.
- Treat opening, dismissal, focus restoration and detach as one owned lifecycle. Prevent disabled input through pointer,
  keyboard and automation paths alike. Avoid parallel keyboard and automation implementations of the same action.
- Ship examples of default, selected, disabled, empty, long-text and scaled states with behavior checks. Gallery size alone
  does not demonstrate integration quality. Do not add a control merely to increase the count.

## EditableTable capability gaps

- The ribbon cannot configure `Column.Decimals` or number/currency/date presentation. Existing precision and `Suffix`/`Display` values remain stored and applied. Design native formatting controls or a public host extension point before asking apps to replace the removed panel.
- Hosts cannot restrict the ribbon paper choices or provide ribbon-level validation callbacks. Hosts still own print/export policy and boundary validation. A future public contract must preserve bindings, show field errors and define completion behavior; do not silently turn domain constraints into library defaults.

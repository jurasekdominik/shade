# Design system

A dark, dense design system for desktop work tools: apps people keep open for hours to configure, monitor and act on
data. It isn't tied to one framework or product. Each project keeps its own decisions (its domain colours, its pages,
its code names) in a companion `docs/DESIGN.md` of its own. The code is the `Shade` library in this repository; the
gallery (`gallery/`) shows every component in every state.

Contents: 1 Principles · 2 Colour · 3 Type · 4 Space and shape · 5 Icons · 6 Components · 7 Status · 8 Page layout ·
9 Forms, settings and editors · 10 App shell · 11 States and motion · 12 Writing · 13 Rules for builders

## 1. Principles

- **One way to do each thing.** A dropdown is always the same dropdown, a list is always the same table. A screen never
  styles a control itself.
- **Tokens, not values.** Colours, sizes, spacing, radii and heights come from named tokens. A literal colour or pixel
  value in a view is a bug.
- **Dense but readable.** Compact rows are fine; text smaller than 11 px is not.
- **Nothing bigger than its content.** Size comes from the content, not from the space available.
- **State in words.** Colour supports a state; it never carries it alone. "Failed" is written somewhere, not only shown red.
- **Space groups, lines separate, boxes are rare.** Things that belong together sit close; groups are apart. A box is
  only for something that really is a separate object: a field, a menu, a dialog, a reference card.
- **Safe actions look safe, risky actions look risky.** Deletes and anything that changes the outside world ask first
  and look distinct.
- **Same thing, same place.** An action sits on the same pixels on every page, so the hand and eye learn one layout.
- **A stable look, not a fixed layout.** Shade fixes the identity (colour, type, motion, details); the app decides what a
  control contains and where its parts go. Every control therefore offers: content or template slots (cells, items,
  headers, footers, extra actions), Avalonia properties for anything an app changes (placement, order and visibility of
  its parts included), events or commands for what it does, and public tokens (`Look`, role brushes, `Build` helpers) so
  an app can paint its own surfaces in the same identity. A control whose layout is fixed in code is unfinished; an app
  that needs more files a Shade issue instead of patching ([§13](#13-rules-for-builders-people-and-agents)).

## 2. Colour

Two modes, dark (the default) and light. Every colour is a role token, so screens never name a colour, and both modes
use the same rules. `Look.SetMode` switches them live, in every open window.

**Thirteen roles** (the approach of Apple's HIG, Material 3 and Radix: colours named by job, shades derived by rule):

| Role | Dark | Light | Use |
| --- | --- | --- | --- |
| `Background` | `#151519` | `#F4F4F7` | Page canvas |
| `Raised` | `#1B1B20` | `#FFFFFF` | Sidebar, fields, menus, table headers, dialogs, reference cards |
| `Fill` | `#29292F` | `#ECECF1` | Hover, Secondary buttons, pressed rows |
| `Separator` | `#35353D` | `#DADAE1` | Every resting 1 px line |
| `Outline` | `#606069` | `#A7A7AF` | A control's border on hover or while open |
| `Text` | `#EDEDF0` | `#18181C` | Main text |
| `Text 2` | `#B1B1BC` | `#474752` | Labels, supporting text |
| `Text 3` | `#9999A7` | `#676775` | Placeholders, metadata, hints |
| `Accent` | user's choice, default `#8C7AE6` | the same | Primary action, selection, focus, active tab |
| `Success` | `#83CEA5` | `#1F8A57` | OK, completed |
| `Warning` | `#FFB142` | `#A86400` | Retrying, incomplete |
| `Danger` | `#E0685C` | `#C4453A` | Failed, deletes, actions that change the outside world |
| `Info` | `#6FA8DC` | `#2C6CB0` | Overrides ("differs from inherited"), notices |

Text roles read at 4.5:1 or better on `Raised`, and status colours at 3:1, in both modes; the gallery checks it.

**Shades are computed, never stored:**

- **Tint(c)** is 15 % of the colour over `Raised`. Used for selected rows, the Danger button and status backgrounds.
- **Hover(c)** is the colour with 10 % white mixed in.
- **On(c)** is dark ink or white text, whichever contrasts more with the solid colour.
- **Small accent text** (and danger text) moves toward the high-contrast end (white in dark, ink in light) until it
  reaches 4.5:1 against `Raised`; accent text also against its own tint (an active chip, a selected row). These are the
  `Accent text` and `Danger text` roles.
- **Focus border** uses the accent when it contrasts at 3:1 on both `Raised` and `Background`, otherwise the contrast-adjusted accent text role.
- **Disabled** text is the `Disabled` role.

**The accent is the user's.** The picker offers curated accents first (`Look.Accents`: a value for each mode, each
readable on its page, with readable text on it and clear of the status hues), then any colour from the palette or the
mixer. The status colours stay fixed; the picker warns when a chosen accent is too close to one of them.
In a settings page it is one row (`AccentPicker`): the accent's dot and name, a Custom hex field and the curated accents
as 24 px swatches under it (the current one ringed); the field opens the full helper (`ColorGrid`). A pick applies at
once, live; the saved value is a curated accent's name (both its values) or a colour.

**The mode is the user's too.** `ModeSwitch` offers Light, Dark and System as one segmented control (28 px, a 14 px
icon and a `Label` word per segment, the current one Tint(accent)); System follows the computer's setting as it changes.
A pick switches at once; Left/Right move between the three; `ModeSwitch.Apply` restores a saved choice at start-up.

**Category colours** (a project's own kinds of thing, e.g. data sources or departments) are allowed, a handful at most,
chosen clear of the accent default and the four status hues. A category colour always sits next to the category's name
(a dot, the name's text); it never stands alone.

## 3. Type

A sans for the interface (Inter) and a monospace (Cascadia Code) for code, payloads, addresses and logs. Sizes in px:

| Token | Size / weight | Use |
| --- | --- | --- |
| `Caption` | 11 / 400 | Metadata, timestamps, table footers, hints under fields |
| `Label` | 12 / 500 | Field labels, table headers, tab labels, chips |
| `Body` | 13 / 400 | Everything else: field text, cells, menu items; buttons at 500 |
| `Section` | 14 / 600 | Block and section headings inside a page or dialog |
| `Title` | 18 / 600 | Page and dialog titles |
| `Mono` | 12 / 400 | Code, XML/JSON, addresses, logs |

- Numbers in tables use tabular figures and are right-aligned.
- Units go in the label or column header ("Timeout (ms)", "Revenue (€)"), not in every value.

## 4. Space and shape

**Spacing** is a 4 px grid, using only 4, 8, 12, 16, 24 and 32. It is compact inside a group and generous between
groups:

| Between | Space |
| --- | --- |
| A label and its field; a field and its help text | 4 |
| Buttons in a row; a field and the button beside it | 8 |
| Fields side by side | 16 |
| Fields stacked (label to label) | 12 |
| Blocks stacked; the page header and the content | 16–20 |
| Columns of blocks | 32 |
| Page edge and content | 16 |
| Dialog edge and content | 24 |

**Heights:** 28 px for every field, dropdown, button, search box and table row; 24 px for chips.

**Corners:** 4 px on controls and menus; 8 px on dialogs, cards and reference cards; 0 on rows, the sidebar and table
selections. No pills.

**Lines:** 1 px `Separator`. A focused control gets a 1 px `Accent` border; nothing glows.

## 5. Icons

- One set: [Lucide](https://lucide.dev) (ISC licence), drawn on its 24 px grid with round-capped strokes.
- 16 px in controls, 18 px in navigation, stroke 1.8.
- A missing icon is taken from Lucide, never drawn by hand, so every icon matches.
- An icon goes before its label with a 6 px gap. An icon-only button has a tooltip.

## 6. Components

Each component has exactly one implementation, and a gallery screen shows every component in every state. Its look, sizes
and class are in [DESIGN.md](../DESIGN.md) (*Choosing a component* and the entries under it); how it behaves and binds is in
[ADOPTION.md](ADOPTION.md). This section keeps the placement rules, which belong to the page and not to one component.

### Button placement

A button's kind says where it goes; the same kind sits in the same place on every page, dialog and editor.

**Order inside a group** (left to right): Ghost, then Secondary, then Primary at the trailing end. The eye ends on the
main action, and Cancel is always to its left. This is Apple's, GNOME's and Material's order; Windows' own dialogs put
OK first, but a product keeps one order everywhere, so Shade uses this one on every platform.

| Where | What sits there | Placement |
| --- | --- | --- |
| Page header | The page's actions; on a list page, Create ("+ New …") as the Primary | Right end of the 28 px title row: Ghost · Secondary · Primary |
| Subpage header | "‹ Parent" back button, then the page's actions | Back at the far left (always the same pixel); actions as a page header |
| Toolbar (under the header) | Bulk actions (`•••`), search, view options; never a Primary | Tabs and filters left; tools and search right |
| Dialog footer | Cancel and the action | Bottom right: Cancel (Ghost) · Primary, each at least 110 px |
| Destructive confirmation | Cancel and the named action ("Delete project") | Bottom right: Cancel · Danger, the Danger button in the Primary's slot; only a click confirms (Enter doesn't) |
| Editor and wizard footer | Delete; Cancel, Back, Next or Save | A full-width bar with a line above: Delete (Danger) alone at the far left; Cancel · Back · Next/Save at the right |
| Block (a group of settings) | The block's own actions ("+ Add", "Test connection") | Under the block's rows, left-aligned with its fields; Secondary or Ghost, never Primary |
| Next to a field | An action on that field (Browse, Test, Copy) | Right of the field, same 28 px height, 8 px gap |
| Table row | The row's actions | Its right-click / `•••` menu in a fixed order (Open, Edit, Duplicate, Delete, Delete last below a line); one or two icon buttons at the row's right end at most |
| Empty state | The one way forward ("+ New project") | Centred under the message, Primary |

**Rules**

- **One Primary per area** (a page header, a dialog, an editor footer). Toolbars, blocks and rows have none.
- **Danger is never next to the Primary.** In a footer it stands alone at the far (left) end; in a menu it is last,
  below a line. The one exception is a destructive confirmation, where the Danger button is the action itself.
- **Enter is the Primary, Esc is Cancel.** Enter never confirms a destructive action, and never acts while the focus
  is in a multi-line field.
- **8 px between buttons in a group**, 16 px or more (or the bar's far end) between groups, so a separate group reads
  as separate.
- **Entering and leaving share a slot** (§8): Edit ↔ Done, Next → Save on a wizard's last step, same place and width.
- **A button that can't be used now stays in place, disabled**, with the reason nearby; buttons don't appear and
  disappear and push the others around.
- **Labels are verbs** naming the action ("Save", "Send", "Delete project"); "OK" only acknowledges a message.
- **A risky action is protected by a confirmation**, not by a bigger button.


## 7. Status

One set of states, everywhere:

| State | Colour | Example words |
| --- | --- | --- |
| OK | `Success` | OK · Completed · Connected |
| Warning | `Warning` | Retry at 14:30 · Incomplete |
| Failed | `Danger` | Failed · Offline |
| Running | `Accent` (pulsing) | Syncing… |
| Waiting | `Text 3` | Scheduled · Waiting for the window |
| Off | hollow `Text 3` | Disabled |

- **On its own** (a page header, a panel): a dot and the word.
- **In a table:** the dot, with the word as tooltip (`DESIGN.md`, *table-header*).
- **In filters:** a chip per state with its dot (`DESIGN.md`, *filter-chip*).

## 8. Page layout

```
16 px ┌──────────────────────────────────────────────────────────────────────────┐
      │ [‹ Parent]  Title                                 [Ghost] [Sec] [Primary] │  28 px title row
      │ One line describing the page                                             │
      │                                                                          │  16
      │ All  Tab  Tab  Tab                                   ••• [Search…     ]  │  28 px tabs + search
      │ ┌ card ┐ ┌ card ┐ ┌ card ┐ ┌ card ┐                                       │  summary cards (optional)
      │ [● OK] [● Warning] [● Failed] …                                          │  filter chips
      │ ┌ table ─────────────────────────────────────────────────────────────┐   │
      │ │                                                                      │ │
      │ └──────────────────────────────────────────────────────────────────────┘ │
      │ 12 of 40 · times in this PC's time zone                                  │  Caption footer
      └──────────────────────────────────────────────────────────────────────────┘
```

- **Header:** the same on every page. A 28 px title row at the top-left inset; actions right-aligned on it, Primary
  rightmost; one line of description under it.
- **Subpages:** a back button ("‹ Parent") at the far left of the title row, always on the same pixel.
- **Entering and leaving share a slot.** A button that opens a subpage, mode or panel and the one that closes it sit in
  the same place at the same width (Edit ↔ Done, Next → Save on a wizard's last step).
- **Toolbar:** directly under the header: category tabs on the left; `•••` (bulk actions) and search on the right.
- **Same action, same place:** Create ("+ New …") is the header's Primary on every list page; a row's actions are in its
  right-click / `•••` menu in a fixed order (Open, Edit, Duplicate, Delete — Delete last, below a divider).
- **Content** fills the remaining height and scrolls inside.

## 9. Forms, settings and editors

**Blocks in responsive columns, not one long column.**

Rows in a compact editor are `FormLayout.RowHeight` (28 px); taller content scrolls in its page, never in a box of its own.

- Each group is a **block**: a `Section` heading, at most one line of help (`Caption`, `Text 3`), then its rows.
- A block has no box. A faint line (`Separator` at 60 %) runs above it, 12 px over its heading.
- Blocks sit in a **masonry** layout, like a CSS masonry grid: as many columns as fit at about 300 px each (up to three
  on a page, two in a dialog or editor), 32 px between columns and 20 px between blocks, each block placed in the
  shortest column so far. Resizing the window moves the blocks; nothing scrolls sideways.

**Inside a block:**

- **Settings rows:** a label on the left, its control at the right end of the row. A row is 32 px (44 px with a one-line
  caption). No line between rows.
- **Value groups** (sharing defaults, a mail server): a grid of **equal-width cells**, two per row, label above each
  field. Equal cells read as a table and keep the edges aligned; widths from content make ragged columns.
- **A field alone** is as wide as its longest value would need (a port 80 px, a time 72 px, a name 240 px); only paths,
  URLs, templates and free text stretch.
- **Help is short:** one line under a heading at most; a row caption only when the label could be misread; anything
  longer goes behind an ⓘ tooltip by the label.
- **Show the current value, expand for the rest:** a rarely changed setting shows its value compactly and opens on click
  (an accent shows one swatch and its name; a click opens the swatches).
- **Say what a setting belongs to:** headings name their scope ("Sharing defaults", "Email server").
- **Lists that live elsewhere aren't repeated:** a row shows their count and links to where they're edited.
- **Switches apply at once and need no banner.** A field saves when you leave the page; only a page with a real Save
  button has one.

**Creating vs editing:**

- **A new item** (a project, a task) is a wizard: one step at a time, Back and Next.
- **An existing item** opens with one Save. Large editors use tabs, General first (name and description, with a compact enable switch in its heading); smaller editors can show every section on one page.
- A large editor opens **inside the main window**, not a separate window, so it gets the room; Esc or navigating away
  closes it.
- **Editor footer:** a bar across the editor's full width with a 1 px line above it: Delete at the left; Cancel · Back ·
  Next/Save at the right edge. The content column has one width on every step (up to about 1100 px, left-aligned), so
  nothing moves between steps or window sizes.
- In an editor that is one long page, section headings are `Section` size with their help line under, not page titles.
- A control that depends on another field (Publish needs a title) updates as soon as that field changes; never
  "save and reopen".

**Dialogs** follow the same rules: blocks in one or two masonry columns, labels above fields, Cancel and Primary bottom
right. Only a table's rows keep fixed columns.

## 10. App shell

- **Sidebar** on the left, 180 px by default (`ExpandedWidth` is configurable; 54 px collapsed). Branding wraps at Title size, reserves mark space and grows its header; expanded geometry survives collapse/expand. Navigation: top-level rows 28 px, child rows 26 px indented 34 px, 4 px after
  every top-level entry. An active row has a Tint(accent) fill and a 3 px accent marker flush at x = 0, the row's full
  height. Only the collapse button sits under the items, in the same place expanded or collapsed.
- **Pages with sections** (Settings, a workspace's tools) expand their sidebar entry into the sections.
- **Status bar** along the window's bottom, 28 px, under the sidebar too: left, the latest warning or error with its time
  (a click opens the log); right, live counts (people online). No logo, no path.
- **No breadcrumb bar, no global gear.** The sidebar says where you are; each page owns its title and actions.
- **Pages stay alive** when you navigate away, so drafts and open sessions survive. The app never reopens on a risky page
  at start.
- **Dangerous capabilities are hidden while off:** a feature that writes to the outside world is absent from the sidebar,
  menus and routes until a setting enables it (off by default).

Global history uses shared Back/Forward ghost arrows in the sidebar footer. Mouse side buttons and Alt+Left/Right
follow the same bounded page history; replay never repeats execution actions.

## 11. States and motion

- **Hover:** `Fill` or a brighter border. **Pressed:** one step darker.
- **Keyboard focus:** a 1 px `FocusBorder` border, shown when focus came by Tab or the arrow keys; a click, or focus handed back
  after a menu or dialog closes, shows none. A text field shows it whenever it takes typing.
- **Disabled:** the `Disabled` role, no hover, and never the only explanation: say why nearby or in a tooltip.
- **Busy:** a button keeps its width and shows "…" or a small spinner. Nothing jumps.
- **Motion:** 120–220 ms ease-out for hover, fades and sliding underlines; 300–450 ms ease-in-out for a panel opening or
  closing (the sidebar). Nothing bounces or slides more than a panel's width.

## 12. Writing

- Sentence case everywhere ("Create project", not "Create Project" or "CREATE PROJECT").
- Plain words over jargon; the technical name goes in a tooltip.
- Errors name what failed and what to do next ("Enter a value for StartDate (Variables).").
- Buttons say what they do ("Delete project", "Send test email"), never "OK" or "Submit".
- Times say their zone once, where they're listed ("times in this PC's time zone").
- **Other languages:** built-in labels, tooltips, errors and automation names use English keys/templates through Shade's
  localization path. `Look.Localize` supports formatted arguments/plurals and takes precedence over `Look.Translate`.
  Configure hooks and `Look.Culture` before constructing views; use `Look.RefreshLanguage()` for live built-in refresh.
  The app owns its translations and custom text. [ADOPTION.md](ADOPTION.md#translation-and-culture) owns the full contract
  and culture limitations; [CONTRIBUTING.md](../CONTRIBUTING.md#translation-and-culture) covers implementation checks.

## 13. Rules for builders (people and agents)

1. Only tokens and the shared components. No colour, font size, radius or height literals in views, and no lookalike of
   a Shade control built from a raw Avalonia one Shade does not theme (`Button`, `DatePicker`).
   Avalonia's own `CheckBox`, `ToggleSwitch`, `ProgressBar`, `TextBox`, `ComboBox`, `ContextMenu`/`MenuFlyout`/`MenuItem`, `ListBox` and `TabControl` are themed by Shade and used directly (the set grows;
   `docs/ADOPTION.md` lists it). Layout is the app's: arrange Shade controls with Shade tokens. `DESIGN.md` → *Choosing a component*
   says which control a need takes.
2. **Controls are open through public API, not local patches.** Defaults carry Shade's identity; structure (placement,
   order, visibility, content, extra actions) is configurable. When an app needs more, it files a Shade issue
   ([APP_AGENTS.md](APP_AGENTS.md)); Shade adds the API. New components reach the controls, `DESIGN.md` (their look) and the
   gallery in one change. A gap in the meantime is marked `shade-gap: #issue`.
3. Before finishing a UI change, inspect affected states in both modes using agent or user-provided screenshots/recordings
   and run relevant final checks. Regenerate artifacts when their sources change; use focused checks during iteration.
   Follow [CONTRIBUTING.md](../CONTRIBUTING.md#iterative-ui-work-and-user-testing) for evidence and final verification.
4. Text sizes come from §3 (under 11 px is a bug); spacing from §4 (off-grid margins are a bug).
5. Every page starts with the shared header (§8); check "same action, same place" and "entering and leaving share a slot".
6. Every list is the shared table; every state uses §7.
7. Anything that deletes data or changes the outside world asks first, names its target, and is logged.
8. An app checks literal colours, off-scale corners and raw controls in its views. The README's one-line C# heuristic
   catches common violations; XAML, other literals and composed substitutes still require review or app-specific checks.
9. Nothing is bigger than its content needs (§9): a field wider than its value, or help longer than one line, is a bug.
10. Anything clickable takes the pointer on its whole box: a lone icon sits on a transparent square (a bare drawn shape
   only reacts on its lines), and a drawn control paints its whole area, transparent where empty.
11. Inside Shade, prefer native Avalonia controls and semantics with Shade artwork. Keep C# and web visually identical
    in equivalent states. [CONTRIBUTING.md](../CONTRIBUTING.md) owns the refactor, binding and paired-verification workflow.


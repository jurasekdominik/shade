---
version: 1.0.0
name: Shade
description: A dark-first, dense design system for desktop work tools, with a matching light mode. Thirteen colour roles named by job, one user-chosen accent that every shade is derived from by rule, Inter at five sizes, a 4 px grid, 4 px corners and 28 px controls.

colors:
  primary: "#8C7AE6"
  background: "#151519"
  raised: "#1B1B20"
  fill: "#29292F"
  separator: "#35353D"
  outline: "#606069"
  text: "#EDEDF0"
  text-2: "#B1B1BC"
  text-3: "#9999A7"
  disabled: "#575760"
  accent: "#8C7AE6"
  accent-hover: "#9887E8"
  accent-tint: "#2C293E"
  accent-text: "#9585E8"
  focus-border: "#8C7AE6"
  on-accent: "#151519"
  success: "#83CEA5"
  warning: "#FFB142"
  danger: "#E0685C"
  danger-text: "#E3776C"
  danger-tint: "#392729"
  info: "#6FA8DC"

colors-light:
  primary: "#6A58D6"
  background: "#F4F4F7"
  raised: "#FFFFFF"
  fill: "#ECECF1"
  separator: "#DADAE1"
  outline: "#A7A7AF"
  text: "#18181C"
  text-2: "#474752"
  text-3: "#676775"
  disabled: "#AEAEB6"
  accent: "#6A58D6"
  accent-hover: "#7969DA"
  accent-tint: "#E9E6F9"
  accent-text: "#6353C7"
  focus-border: "#6A58D6"
  on-accent: "#FFFFFF"
  success: "#1F8A57"
  warning: "#A86400"
  danger: "#C4453A"
  danger-text: "#B24037"
  danger-tint: "#F6E3E1"
  info: "#2C6CB0"

typography:
  title:
    fontFamily: Inter
    fontSize: 18px
    fontWeight: 600
    letterSpacing: 0
  section:
    fontFamily: Inter
    fontSize: 14px
    fontWeight: 600
    letterSpacing: 0
  body:
    fontFamily: Inter
    fontSize: 13px
    fontWeight: 400
    letterSpacing: 0
  label:
    fontFamily: Inter
    fontSize: 12px
    fontWeight: 500
    letterSpacing: 0
  caption:
    fontFamily: Inter
    fontSize: 11px
    fontWeight: 400
    letterSpacing: 0
  mono:
    fontFamily: Cascadia Code
    fontSize: 12px
    fontWeight: 400
    fontFeature: '"liga" 0, "calt" 0'

rounded:
  none: 0px
  control: 4px
  card: 8px

spacing:
  xs: 4px
  sm: 8px
  md: 12px
  lg: 16px
  xl: 24px
  xxl: 32px

components:
  focus-indicator:
    backgroundColor: "{colors.focus-border}"
    height: 1px
  button-primary:
    backgroundColor: "{colors.primary}"
    textColor: "{colors.on-accent}"
    typography: "{typography.label}"
    rounded: "{rounded.control}"
    height: 28px
    padding: 12px
  button-primary-hover:
    backgroundColor: "{colors.accent-hover}"
    textColor: "{colors.on-accent}"
  button-secondary:
    backgroundColor: "{colors.fill}"
    textColor: "{colors.text}"
    typography: "{typography.label}"
    rounded: "{rounded.control}"
    height: 28px
    padding: 12px
  button-ghost:
    textColor: "{colors.text-2}"
    typography: "{typography.label}"
    rounded: "{rounded.control}"
    height: 28px
    padding: 12px
  button-ghost-hover:
    backgroundColor: "{colors.fill}"
    textColor: "{colors.text}"
  button-danger:
    backgroundColor: "{colors.danger-tint}"
    textColor: "{colors.danger-text}"
    typography: "{typography.label}"
    rounded: "{rounded.control}"
    height: 28px
    padding: 12px
  button-disabled:
    backgroundColor: "{colors.raised}"
    textColor: "{colors.disabled}"
    typography: "{typography.label}"
    rounded: "{rounded.control}"
    height: 28px
  text-field:
    backgroundColor: "{colors.raised}"
    textColor: "{colors.text}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    height: 28px
    padding: 8px
  text-field-placeholder:
    textColor: "{colors.text-3}"
  label:
    textColor: "{colors.text-2}"
    typography: "{typography.label}"
  help:
    textColor: "{colors.text-3}"
    typography: "{typography.caption}"
  dropdown:
    backgroundColor: "{colors.raised}"
    textColor: "{colors.text}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    height: 28px
    padding: 8px
  search-choice:
    backgroundColor: "{colors.raised}"
    textColor: "{colors.text}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    height: 28px
    padding: 8px
  dropdown-item-current:
    backgroundColor: "{colors.fill}"
    textColor: "{colors.text}"
    height: 28px
  date-field:
    backgroundColor: "{colors.raised}"
    textColor: "{colors.text}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    height: 28px
  date-field-picked-day:
    backgroundColor: "{colors.accent}"
    textColor: "{colors.on-accent}"
    rounded: "{rounded.control}"
  divider:
    backgroundColor: "{colors.separator}"
    height: 1px
  date-range-day:
    backgroundColor: "{colors.accent-tint}"
    textColor: "{colors.text}"
    size: 28px
  editable-table-cell:
    backgroundColor: "{colors.background}"
    textColor: "{colors.text}"
    typography: "{typography.body}"
    rounded: "{rounded.none}"
    height: 28px
    padding: 8px
  editable-table-footer:
    backgroundColor: "{colors.fill}"
    textColor: "{colors.text}"
    typography: "{typography.body}"
    height: 28px
  menu:
    backgroundColor: "{colors.raised}"
    textColor: "{colors.text}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
  switch-on:
    backgroundColor: "{colors.accent}"
    width: 32px
    height: 18px
  switch-off:
    backgroundColor: "{colors.fill}"
    width: 32px
    height: 18px
  tick-on:
    backgroundColor: "{colors.accent}"
    textColor: "{colors.on-accent}"
    rounded: "{rounded.control}"
    size: 16px
  state-toggle:
    backgroundColor: "{colors.raised}"
    rounded: "{rounded.control}"
    height: 28px
  mode-switch:
    backgroundColor: "{colors.raised}"
    textColor: "{colors.text-2}"
    typography: "{typography.label}"
    rounded: "{rounded.control}"
    height: 28px
  mode-switch-active:
    backgroundColor: "{colors.fill}"
    textColor: "{colors.text}"
  filter-chip:
    backgroundColor: "{colors.raised}"
    textColor: "{colors.text-2}"
    typography: "{typography.label}"
    rounded: "{rounded.control}"
    height: 24px
  filter-chip-active:
    backgroundColor: "{colors.accent-tint}"
    textColor: "{colors.accent-text}"
  tab-active:
    textColor: "{colors.text}"
    typography: "{typography.label}"
  tag:
    backgroundColor: "{colors.fill}"
    textColor: "{colors.text}"
    typography: "{typography.label}"
    rounded: "{rounded.control}"
    height: 24px
  tag-success:
    textColor: "{colors.success}"
  tag-warning:
    textColor: "{colors.warning}"
  tag-info:
    textColor: "{colors.info}"
  status-badge:
    textColor: "{colors.text}"
    typography: "{typography.body}"
  table-header:
    backgroundColor: "{colors.raised}"
    textColor: "{colors.text-2}"
    typography: "{typography.label}"
    height: 28px
    padding: 8px
  table-row:
    textColor: "{colors.text}"
    typography: "{typography.body}"
    rounded: "{rounded.none}"
    height: 28px
    padding: 8px
  table-row-hover:
    backgroundColor: "{colors.fill}"
  table-row-selected:
    backgroundColor: "{colors.accent-tint}"
  table-designer-header:
    backgroundColor: "{colors.raised}"
    textColor: "{colors.text-2}"
    typography: "{typography.label}"
    height: 28px
  tooltip:
    backgroundColor: "{colors.fill}"
    textColor: "{colors.text}"
    typography: "{typography.label}"
    rounded: "{rounded.control}"
    padding: 8px
  dialog:
    backgroundColor: "{colors.background}"
    textColor: "{colors.text}"
    rounded: "{rounded.card}"
    padding: 24px
  dialog-button:
    width: 110px
    height: 28px
  color-picker:
    backgroundColor: "{colors.raised}"
    rounded: "{rounded.card}"
  accent-swatch:
    size: 24px
  empty-state:
    textColor: "{colors.text-2}"
    typography: "{typography.section}"
  summary-card:
    backgroundColor: "{colors.raised}"
    textColor: "{colors.text}"
    typography: "{typography.label}"
    rounded: "{rounded.card}"
    height: 84px
  summary-card-detailed:
    backgroundColor: "{colors.raised}"
    typography: "{typography.section}"
    rounded: "{rounded.card}"
    height: 112px
  search-field:
    backgroundColor: "{colors.raised}"
    textColor: "{colors.text}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    height: 28px
  progress-track:
    backgroundColor: "{colors.fill}"
    height: 6px
  progress-bar:
    backgroundColor: "{colors.accent}"
    height: 6px
  log:
    textColor: "{colors.text-2}"
    typography: "{typography.mono}"
  reference-card:
    backgroundColor: "{colors.raised}"
    textColor: "{colors.text}"
    typography: "{typography.body}"
    rounded: "{rounded.card}"
    padding: 12px
  sidebar:
    backgroundColor: "{colors.raised}"
    textColor: "{colors.text-2}"
    typography: "{typography.body}"
    width: 180px
  sidebar-collapsed:
    width: 54px
  sidebar-row-current:
    backgroundColor: "{colors.accent-tint}"
    textColor: "{colors.text}"
    height: 28px
  status-bar:
    backgroundColor: "{colors.raised}"
    textColor: "{colors.text-2}"
    typography: "{typography.caption}"
    height: 28px
  page-header:
    textColor: "{colors.text}"
    typography: "{typography.title}"
    padding: 16px
  block-heading:
    textColor: "{colors.text}"
    typography: "{typography.section}"
---

## Overview

Shade is a dark-first design system for **desktop work tools**: apps people keep open for hours to configure, watch and act
on data. Think of a well-kept instrument panel: grey, labelled, quiet, with one lit button, the one that matters now. The page is a calm ladder of near-neutral greys with a faint violet cast (`{colors.background}` → `{colors.raised}` →
`{colors.fill}` → `{colors.separator}`), and almost everything on it is text, lines and 28 px controls. Colour is rationed: one
**accent** (`{colors.accent}` — #8C7AE6 by default, chosen by the user) marks the main action, the selection and keyboard
focus, and four status colours appear only beside the word they stand for.

Shades are **computed, never stored**. A tint is 15 % of a colour over `{colors.raised}`, a hover is 10 % lighter, text on a
colour is dark ink or white by contrast, and small accent text moves toward the high-contrast end until it reads at 4.5:1.
So a new accent, or the light mode, needs no new values; the rules make them.

Pages are **masonry blocks without boxes**: a heading, one line of help, the controls, and a faint line above. Boxes are kept
for what floats or stands apart (menus, dialogs, reference cards). The type is Inter at five sizes; values, addresses and
code are Cascadia Code.

The mark is two offset discs ([assets/shade.svg](assets/shade.svg)); in the app the back disc takes the accent and the
front one the text colour, so it follows the accent and the mode. Working, the front disc eclipses the back one.

Shade is a C# library for Avalonia 12 (.NET 10): the tokens in this file are written from its code, and the
[landing page](https://jurasekdominik.github.io/shade/) and [README](README.md) show every chapter rendered from its
components, in both modes.

**Key Characteristics:**
- A near-neutral ladder (`{colors.background}`, `{colors.raised}`, `{colors.fill}`, `{colors.separator}`, `{colors.outline}`) carrying every surface and line.
- One user-chosen accent (`{colors.accent}`), with its tint, hover and text shades made by rule.
- Status colours (`{colors.success}`, `{colors.warning}`, `{colors.danger}`, `{colors.info}`) always next to their word, never decoration.
- One control height, 28 px (`{components.text-field.height}`), for fields, buttons, dropdowns and table rows; 24 px for chips and tags.
- 4 px corners on controls (`{rounded.control}`), 8 px on dialogs and cards (`{rounded.card}`), none on rows. No pills.
- Masonry blocks separated by a faint line, not boxes.
- Dark and light modes from one set of roles, switched live.

## Colors

Thirteen roles named by their job (the approach of Apple's HIG, Material 3 and Radix), plus the shades the rules make from
them. Each role has a dark value (the default) and a light one; `Look.SetMode` switches them live.

<!-- generated: colors -->
### Brand & Accent
- **Accent** (`{colors.accent}` — #8C7AE6 · light #6A58D6): Primary action, selection, focus, the active tab.
- **Accent hover** (`{colors.accent-hover}` — #9887E8 · light #7969DA): The Primary button under the pointer.
- **Accent tint** (`{colors.accent-tint}` — #2C293E · light #E9E6F9): Selected rows, active chips, the sidebar's current row.
- **Accent text** (`{colors.accent-text}` — #9585E8 · light #6353C7): Small text in the accent, at 4.5:1.
- **Focus border** (`{colors.focus-border}` — #8C7AE6 · light #6A58D6): Keyboard focus, at least 3:1 on the page and fields.
- **On accent** (`{colors.on-accent}` — #151519 · light #FFFFFF): Text on an accent fill.

### Surface
- **Background** (`{colors.background}` — #151519 · light #F4F4F7): The page canvas.
- **Raised** (`{colors.raised}` — #1B1B20 · light #FFFFFF): Sidebar, fields, menus, table headers, dialogs.
- **Fill** (`{colors.fill}` — #29292F · light #ECECF1): Hover, Secondary buttons, tooltips.
- **Separator** (`{colors.separator}` — #35353D · light #DADAE1): Every resting 1 px line.
- **Outline** (`{colors.outline}` — #606069 · light #A7A7AF): A control's border on hover or while open.

### Text
- **Text** (`{colors.text}` — #EDEDF0 · light #18181C): Main text.
- **Text 2** (`{colors.text-2}` — #B1B1BC · light #474752): Labels, supporting text.
- **Text 3** (`{colors.text-3}` — #9999A7 · light #676775): Placeholders, metadata, hints.
- **Disabled** (`{colors.disabled}` — #575760 · light #AEAEB6): Text of a control that can't be used.

### Semantic
- **Success** (`{colors.success}` — #83CEA5 · light #1F8A57): OK, completed.
- **Warning** (`{colors.warning}` — #FFB142 · light #A86400): Retrying, incomplete.
- **Danger** (`{colors.danger}` — #E0685C · light #C4453A): Failed, deletes, actions that change the outside world.
- **Danger text** (`{colors.danger-text}` — #E3776C · light #B24037): Danger as small text (the Danger button's label), at 4.5:1.
- **Danger tint** (`{colors.danger-tint}` — #392729 · light #F6E3E1): The Danger button's fill.
- **Info** (`{colors.info}` — #6FA8DC · light #2C6CB0): Overrides, notices.

<!-- /generated -->
### Rules
- **Tint(c):** 15 % of the colour over `{colors.raised}`: selected rows, active chips, status tags, the Danger button.
- **Hover(c):** the colour with 10 % white.
- **On(c):** dark ink (#151519) or white, whichever contrasts more with the fill.
- **Lifted text:** accent and danger text (`{colors.accent-text}`, `{colors.danger-text}`) move toward white (dark mode) or ink (light mode) until they read at 4.5:1 on `{colors.raised}`.
- **Light mode:** every role has a light value, under `colors-light` in the tokens above. design.md has no modes, so
  that block is Shade's own: its tools read only `colors` (the dark mode). `primary` is design.md's name for the accent.
- **Category colours** (a project's own kinds of thing) are allowed, a handful at most, clear of the accent and the status hues, and always beside the category's name.

## Typography

### Font Family
**Inter** (embedded in the package, weights 400, 500, 600 and 700) for the whole interface. **Cascadia Code**, with its
ligatures turned off, for values: addresses, versions, timestamps in logs, code and JSON. Nothing else.

### Hierarchy

| Token | Size | Weight | Use |
|---|---|---|---|
| `{typography.title}` | 18px | 600 | Page and dialog titles |
| `{typography.section}` | 14px | 600 | Block and card headings |
| `{typography.body}` | 13px | 400 | Fields, table cells, running text |
| `{typography.label}` | 12px | 500 | Field labels, table headers, buttons, tooltips |
| `{typography.caption}` | 11px | 400 | Help under a field, metadata, the status bar |
| `{typography.mono}` | 12px | 400 | Values and code, in Cascadia Code |

### Principles
Five sizes and nothing between them: every text takes one, by name (`TextSize.Body`). Weight carries emphasis
before size does; headings are 600, labels 500, text 400. Sentence case everywhere ("Create project"). An action is a verb and
never ends in dots ("Sync"); work in progress keeps them ("Connecting…").

## Layout

### Spacing System
- **Base unit:** 4px.
- **Tokens:** `{spacing.xs}` 4px · `{spacing.sm}` 8px · `{spacing.md}` 12px · `{spacing.lg}` 16px · `{spacing.xl}` 24px · `{spacing.xxl}` 32px. No other gaps.
- **Inside a group:** a label and its field 4px; buttons in a row 8px; fields stacked 12px; fields side by side 16px.
- **Between groups:** blocks 20px; columns of blocks 32px; the page edge 16px; a dialog's edge 24px.

### Grid & Container
- **Page:** a header (title left, actions right, one line of help), then the content filling the rest and scrolling inside.
- **Blocks:** a `Masonry` with as many columns as reach the minimum width (about 300px), up to three on a page and two in a dialog or editor; each block goes to the shortest column.
- **Shell:** the sidebar (`{components.sidebar.width}`, `{components.sidebar-collapsed.width}` collapsed), the page, and the status bar (`{components.status-bar.height}`) under both.

### Whitespace Philosophy
Dense inside a group, generous between groups. Space and a faint line group things; boxes don't. Nothing is bigger than its
content needs: a port field is 80px, a name 240px, and only paths, URLs and free text stretch.

## Elevation & Depth

| Level | Treatment | Use |
|---|---|---|
| Flat | No shadow, no border | The page, blocks, the sidebar's rows |
| Line | 1px `{colors.separator}` (**`divider`**) | Table rows, the sidebar's edge, the status bar, a block's line (60 %) |
| Raised | `{colors.raised}` fill, 1px `{colors.separator}` | Fields, menus, table headers, the sidebar |
| Floating | `{colors.raised}` or `{colors.fill}`, 1px `{colors.outline}` | Open dropdowns, tooltips, menus |
| Modal | `{colors.background}`, `{rounded.card}` | Dialogs, over their owner window |

There are no shadows on controls and nothing glows. Keyboard focus is a 1px `{colors.focus-border}` border (`focus-indicator`).

## Shapes

### Border Radius Scale

| Token | Value | Use |
|---|---|---|
| `{rounded.none}` | 0px | Table rows, the sidebar and its rows, selections |
| `{rounded.control}` | 4px | Buttons, fields, dropdowns, menus, chips, tags, tooltips |
| `{rounded.card}` | 8px | Dialogs, cards, reference cards |

Switches are the one round shape (their track and thumb).

## Components

In the groups the live page and the gallery use: three of components (actions & inputs, data & status, overlays & help), then the shell they make up (navigation). A key in bold is the token key above; `Code:` names the class to use.

### Choosing a component

Find the need here first. If a control's public API does not fit, **file a Shade issue** using
[the app-agent workflow](docs/APP_AGENTS.md) instead of patching or copying the control or building a lookalike.

| Need | Use |
|---|---|
| The main action of an area | `ActionButton(…, ButtonKind.Primary)`, one per area |
| Any other action; Cancel; a delete | `ActionButton` (`Normal` = Secondary, `Ghost`, `Danger`) |
| An icon-only action | `IconButton`, or `ActionButton` with `icon:` and a tooltip |
| Text, a number, an address | `Field`, `HostPortField`; several lines `Workbench.Lines` |
| A date | `DateField` |
| A period: a start and an end | `DateRangeField` |
| Search a list | `SearchField` |
| One of a list | `ChoiceButton`; several of a long list `MultiChoice`; of a short one `TickGrid` |
| Browse all options and filter by words | `SearchChoice`, native object/ID selection with a search field in its popup |
| Type with ranked matches | `SuggestionField` |
| On or off, at once / on Save | `Switch` / `Tick` |
| A state on another system | `StateToggle` (changes it), `StateLook` (shows it) |
| Light, dark or system | `ModeSwitch` |
| The accent, in settings | `AccentPicker` (its helper is `ColorGrid`) |
| Several short values; name/value pairs | `ListField`; `PairTable` |
| Rows of data | `Workbench.Table`; a short configuration list `CompactTable` |
| Rows to edit in place (an order, a price list) | `EditableTable` (its `Design` mode is the column designer) |
| A search's matches in the results | `Build.Mark`; a table `Workbench.Mark` |
| Arrange the columns of a table | `TableDesigner` |
| A state, a value, a filter | `StatusBadge`; `Tag`; `FilterChip` |
| Figures at a glance | `SummaryCards` |
| Work under way | `Progress`; in the status bar `StatusBar.Busy` |
| A log | `LogText` |
| A menu; the tray's menu | `Menu.Show`; `TrayMenu` |
| Ask, confirm, inform, choose | `Ask.Confirm`, `Ask.ConfirmDelete`, `Ask.Info`, `Ask.Choose`, `Dialog` |
| Help beside a form; on demand | `ReferenceCard`; `FormLayout.Disclosure`, `InlineCode` |
| Nothing to show yet | `Build.Empty` |
| A page's title and actions | `FormLayout.Header`, `FormLayout.SubHeader` |
| A group of a page | `FormLayout.Block` in a `Masonry` |
| Views of one thing | `TextTabs` |
| The window: navigation, page, status | `Shell.Layout` with `Sidebar` (or the app's own of `NavItem` rows), `StatusBar`, `NavigationButtons` |
| Text and colour | `Build.Text(text, role, TextSize.…)`, `B(role)`; never a hex, `Colors.*` or a number |

### Actions & inputs

**`button-primary`** — The one main action of an area. `{colors.primary}` (the accent) fill, `{colors.on-accent}` text, `{typography.label}`, 28px high, 12px side padding, `{rounded.control}`. Hover `{colors.accent-hover}`.

**`button-secondary`** — Every other action (`ButtonKind.Normal`). `{colors.fill}` fill, 1px `{colors.separator}`, `{colors.text}`.

**`button-ghost`** — Toolbars, Cancel, "+ Add". No fill, `{colors.text-2}`; **`button-ghost-hover`** `{colors.fill}`.

**`button-danger`** — Deletes and every action that changes the outside world. `{colors.danger-tint}` fill, `{colors.danger-text}` text. Named for what it does ("Delete project"), never "OK".

**`button-disabled`** — `{colors.raised}` fill, `{colors.disabled}` text, no hover. Something nearby says why.

Code: `ActionButton` (`ButtonKind.Primary`, `Normal`, `Ghost`, `Danger`), `Capsule`, `IconButton` (a glyph in the accent: table, undo, export). Placement: Ghost, then Secondary, then Primary last; dialog buttons at least `{components.dialog-button.width}` wide.

These actions extend native Avalonia `Button` with Shade themes. Use `Content`, `Command`, routed `Click` and
`HorizontalContentAlignment`; bind visual extensions through their Avalonia properties. Confirmation changes the
displayed caption without writing into bound content. Icon-only variants retain a keyboard focus border. Normal
actions use the default control height; explicit heights are respected. See `docs/ADOPTION.md`.

**`text-field`** — 28px, `{colors.raised}`, 1px `{colors.separator}`, 8px padding, `{typography.body}`, placeholder in `{colors.text-3}`. Only the frame changes: `{colors.outline}` on hover, `{colors.focus-border}` with focus, `{colors.danger}` when invalid (with the message below it). Multi-line fields look the same. Code: `Field`, `HostPortField`, `Workbench.Lines`.

**`date-field`** — A date as three blocks, `YYYY-MM-DD`, typed (digits fill a block and move on) or picked in the month a click anywhere opens: today ringed in `{colors.accent}`, **`date-field-picked-day`** filled, weekends marked (`WeekendMark`). Code: `DateField`.

**`date-range-day`** — A period in one field: the start's blocks, "–", the end's. The month shows the days between on `{colors.accent-tint}`, its ends filled. Code: `DateRangeField`.

**`search-field`** — Matches in the results use `{colors.accent-text}` on `{colors.accent-tint}` in 600 (`Build.Mark`, `Workbench.Mark`). A text field with a 16px magnifier in `{colors.text-3}`, × once typed (Esc clears), an optional count ("3 of 12"). Code: `SearchField`.

**`label`** — `{typography.label}` in `{colors.text-2}` above its field, 4px apart; **`help`** under it in `{typography.caption}`. Code: `Build.Labelled`.

**`dropdown`** — Closed, it looks like a field with a chevron. Open, the list hangs below at the field's width and the two are one shape in `{colors.outline}`; items 28px, **`dropdown-item-current`** on `{colors.fill}` without a checkmark. Items may keep their own colour. Code: `ChoiceButton`, `MultiChoice`. Multi-selection keeps native checkboxes open while toggling; arrows/Home/End/typeahead move focus, Space toggles, Esc closes, and Tab continues. Options and selection bind as snapshots; more than two choices show a localized count.

An empty multi-choice option list retains bound selection while loading; a non-empty snapshot removes unavailable keys.
Clear selection explicitly when the application intends to reset it.

**`search-choice`** — A dropdown with a fixed search field above a scrollable list. Opening shows every loaded option;
space-separated words narrow results in any order, ignoring case and accents. The header stays visible while the native
list virtualizes rows. Matching letters use `{colors.accent-text}` on `{colors.accent-tint}` in 600, with the same case/accent rules as filtering. Filtering never changes the committed selection. Enter/click selects; Escape cancels; Tab continues
through the host. Code: `SearchChoice`, a native ComboBox with ItemsSource, object/ID selection and item templates.
SearchMemberBinding can include fields beyond the display label. It filters loaded data; the application owns data fetching.

**`suggestion-field`** — A field with up to eight ranked matches in an attached list; field and list are one shape while open. Code: `SuggestionField`.

**`switch-on`** / **`switch-off`** — 32 × 18px; `{colors.accent}` when on, `{colors.fill}` when off. It acts at once.

**`tick-on`** — A 16px box, `{rounded.control}`, `{colors.accent}` with a check when on, a dash when mixed. A choice that applies on Save.

**`state-toggle`** — Field-sized ON/OFF blocks for a setting on another system, with Unknown, Pending and Undo states; `StateLook` is the same look, shown only.

**`mode-switch`** — Light, Dark and System as one segmented control; **`mode-switch-active`** marks the pick, which switches at once. System follows the computer's setting as it changes. Code: `ModeSwitch`.

**`accent-swatch`** — The accent in settings: its dot and name, a Custom hex field that opens the colour helper, the curated accents as 24px swatches, and a warning when the accent reads badly. `Value` follows external global accent changes too. Code: `AccentPicker`.

**Lists of values** — Several short values (recipients) as one field per row, × removes and + adds; name/value pairs (headers, variables) as a two-field row each. Code: `ListField`, `PairTable`.

Code: `Switch`, `Tick`, `TickGrid` (several picks from a short list), `StateToggle`, `StateLook`.

Interactive toggles use native `IsChecked`, routed `Click`, commands and automation. `Tick` is a native CheckBox;
Switch/StateToggle/FilterChip are native ToggleButtons with Shade presentation. `StateToggle.IsChecked = null` is unknown;
its undo is a separate native command button. Model changes do not raise Click. TickGrid binds ordered snapshot picks.
Mode segments are native radio buttons. Tag removal and field clear/undo actions are native button targets.

PairTable binds editable `Entry` objects through an observable `Entries` collection. Keep blank/duplicate drafts visible;
show duplicate-name errors on both rows. Export with `TryGetPairs` only after validation; the app owns saving. Do not bind
an editable pair list through a dictionary that cannot represent its drafts. ChoiceButton and SuggestionField bind option
snapshots; suggestion navigation retains the text editor and popup rows. See `docs/ADOPTION.md`.

### Data & status

**`summary-card`** — A row of equal 84px cards in monitoring words: the title in `{typography.label}` with a corner value; below, a 40px ring for a share or a 64px trend line at the bottom left, and beside it the figure (18px, 600, its status colour) with its unit and one `{typography.caption}` line on what needs a look. **`summary-card-detailed`** (112px) adds a `{typography.caption}` subtitle under a `{typography.section}` title, a 24px value and the ring or trend line at the right. `{rounded.card}`, `{colors.raised}`; a card that filters gets a `{colors.accent}` frame; a disabled card is drawn at half strength. Action cards are native keyboard/automation targets; arrows move focus. Geometry expands with configured metrics. Code: `SummaryCards`.

**`table-header`** + **`table-row`** — One table for every list. Header 28px on `{colors.raised}` in `{typography.label}` / `{colors.text-2}`; rows 28px, `{typography.body}`, a 1px `{colors.separator}` under each. Numbers right, text left, cut text ends in "…" with the whole value in a tooltip. **`table-row-hover`** `{colors.fill}`, **`table-row-selected`** `{colors.accent-tint}` with a 3px accent marker. Code: `Workbench.Table`.

**`editable-table-cell`** + **`editable-table-footer`** — Rows edited in place under headings with a subtitle: a value with a unit cycles its unit on a click; invalid numeric drafts keep the last accepted value and show a danger border plus an error message (Escape cancels); a computed column joins columns with + − × ÷, in decimals (money to the cent; an app's own rules through `Calculate`); the footer sums, averages or counts. Columns fit their content, fill the room left or keep a fixed width, each with a minimum and a maximum; a 12px target centered across the heading's edge resizes; headings sort up, down, back. The rows scroll under a pinned heading and footer, the bars outside the grid. One editor moves to the clicked cell, caret where clicked. A column's `Display` formats its numbers (culture, currency), measured so; `CanResize`, `CanEditFooter`, a settable `CurrentColumn` and `CanRemoveColumn` carry an app's rules. A General column holds numbers and codes as typed, each aligned by what it is unless `Column.Alignment` or a per-cell `Row.Alignments[columnKey]` overrides it (native `TextAlignment.Left`, `Right` or `Center`, cell before column; `Refresh()` after configuration changes); headings and footers keep their alignment. A row's unit no longer in the list still shows. Row numbers select; a row's menu duplicates, inserts and deletes. Its design mode uses Ctrl+drag to move a column, with Ctrl+arrows for keyboard use and no grip over the text. Releasing Ctrl or Escape cancels a drag. The insertion line and drop region end at the actual grid; Add rows sits directly below the footer/scrollbar. Invalid cells have one danger frame replacing focus. Columns can override `HeadingSize`/`SubheadingSize` and use `Italic` alongside `Bold`; measurement and editing match. An empty None footer has no caption or ellipsis; hover offers a compact calculation glyph and tooltip. Code: `EditableTable`.

The CSS/JS gallery has an inline column ribbon: Edit columns ↔ Done on the left, Add column and Calculated column on the right (the toolbar may sit below the table instead, its frame closing on the bottom edge with the open ribbon above it). Its Column, Text, Width and Table tabs use the shared TextTabs sliding underline and replace the detached configuration forms. Done uses the shared secondary action-button style. Move and Remove sit in the toolbar; formula terms use a readable vertical list with Add term beside its label and scrolling for longer expressions. Unit columns define their comma-separated unit list in the ribbon; existing row units are preserved. Paper settings align right beside the table controls. Ribbon dropdowns float above the table, outside the formula scroll area, while preserving the shared connected field/list border with no gap; upward expansion reverses the joined corners. Fixed width stays within the column’s min/max bounds. Table includes Screen, A5, A4, A3, Letter, Legal and custom paper dimensions in millimetres, with orientation and a print page size. The native editor is opt-in through `ShowColumnEditor`; alignment, units, formulas, text and sizing are implemented with shared controls. Native paper presets, orientation and validated custom millimetre dimensions match the web controls. `PaperSize` reports the effective page dimensions for the app’s platform-specific print/export pipeline.

Ribbon numbers use the configured culture without grouping. Done reveals and focuses invalid ribbon drafts; Escape in a field restores its last applied value, while changing columns discards invalid column drafts. Supplied typography sizes appear accurately as choices. Formula sources distinguish duplicate titles by subtitle, then by table position, without exposing storage keys.

`EditableTable.PaperDimensionsPlacement` defaults to `PaperDimensionsPosition.Bottom`. Set it to
`PaperDimensionsPosition.FormatLabel` (XAML: `PaperDimensionsPlacement="FormatLabel"`) to show effective
millimetre dimensions beside the localized Paper format caption and remove the separate note row.
Screen has no dimensions. Presets, orientation and custom dimensions update the caption automatically.
The web grid matches this with `options.paperDimensionsPlacement: "FormatLabel"` (default `"Bottom"`).

Document paper and screen preview are independent: `ConstrainToPaper` defaults to true for compatibility; false fills the host while preserving paper size/orientation for export. Viewport changes and resizing preserve geometry/focus and do not raise the document's Changed event. Applied widths/dimensions display up to two localized decimal places without rewriting untouched values. Ribbon captions and choices are measured before wrapping. Content-sized flow keeps table settings and paper together whenever they fit; additional labelled column groups appear as width permits, with compact tabs retained for narrower layouts. The same controls retain drafts and focus across layout changes.


**`table-designer-header`** — Arranges a table's columns: Ctrl+drag a header from anywhere on it to a gap (an accent line marks where it lands), right-click for its menu, Ctrl+arrows to move it. Code: `TableDesigner`.

A short configuration list sits at natural height without its own scrolling; disabled rows fade. Code: `CompactTable`.

**`status-badge`** — A dot and a word: OK, Retry at 14:30, Failed, Syncing…, Scheduled, Disabled (a hollow dot). Code: `StatusBadge`.

**`tag`** — 24px, `{rounded.control}`, `{colors.fill}` with plain text ("37 up to date"), or a tint of a status colour behind its text (**`tag-success`** "3 new", **`tag-warning`** "2 updated", **`tag-info`**). A removable tag ends in ×. Code: `Tag`.

**`filter-chip`** + **`filter-chip-active`** — One chip per value, 24px, with the value's colour dot. On: `{colors.accent-tint}` and accent text. Nothing on means everything. Code: `FilterChip`.

**`progress-track`** + **`progress-bar`** — A 6px bar with round ends, `{colors.fill}` track, `{colors.accent}` part, label above and amount at the right; unknown length slides; a countdown empties in `{colors.text-3}`. In the status bar: a 56px bar and the work's name. Code: `Progress`, `StatusBar.Busy`. Never a hand-made bar.

**`log`** — A log's lines in `{typography.mono}`, coloured by level; a double-click takes a whole line, Shift+click a range. It keeps the last 3000 lines. Code: `LogText`.

### Overlays & help

**`menu`** — The same rows as a free card: `{colors.raised}`, 1px `{colors.separator}`, `{rounded.control}`. Danger items last, below a line. A row may carry an icon, a right-aligned shortcut hint in `{colors.text-3}`, a check, or be dimmed when disabled. A long menu scrolls within ten rows, with its bar in a gutter outside the text. Code: `Menu.Panel`, `Menu.Show`.

**`tray-menu`** — The tray icon's menu: the menu card by the pointer, every item at natural height, the current page highlighted. Code: `TrayMenu`.

**`tooltip`** — `{colors.fill}`, 1px `{colors.outline}`, `{typography.label}`, 5 × 8px padding, `{rounded.control}`. Every icon-only button has one. Styled by `Look.Apply`.

**`dialog`** — `{colors.background}`, `{rounded.card}`, 24px padding; a short first line becomes its title. Cancel, then the action named for what it does, bottom right, each **`dialog-button`** at least 110px. Esc cancels; Enter confirms unless the action is destructive. More choices ("Save", "Don't save", "Cancel") sit in the same row, the main one last. Code: `Ask.Confirm`, `Ask.ConfirmDelete`, `Ask.Info`, `Ask.Choose`, `Dialog`.

**`color-picker`** — Three tabs: Curated (eight accents with a value for each mode: Violet, Indigo, Purple, Fuchsia, Pink, Teal, Lime, Graphite), Palette and Mixer. Swatches are controls: 4px corners with a 1px `{colors.separator}` line, a 1px `{colors.text}` ring on the current one. Faded swatches are too low in contrast for the job. Bind `ColorGrid.Value` to a shared colour source to synchronize helpers. Code: `ColorGrid`, `Look.Accents`, `Look.SetAccent(dark, light)`.

**`reference-card`** — Documentation beside a form: `{colors.raised}`, `{rounded.card}`, 12px padding, a `{typography.section}` heading and a help action, Mono codes with their meaning, examples as chips that fill the field, a note. Code: `ReferenceCard`.

**`disclosure`** — Reference help that opens on demand. Code: `FormLayout.Disclosure`, `InlineCode`.

**`empty-state`** — Centred where the content would be: what's missing, one line of why, and the action that fixes it. Code: `Build.Empty`.

### Navigation & shell

**`sidebar`** — The app's navigation, left. `{colors.raised}`, 1px `{colors.separator}` on its right, 180px wide (54px collapsed, icons only). The app's name wraps at Title size with room reserved for its mark; `ExpandedWidth` configures the expanded width and survives collapse/expand. The header grows for localized names. A click on them collapses the bar with a 420ms roll. Top-level rows 28px with an 18px Lucide icon; child rows 26px, indented. The current row has `{colors.accent-tint}` and a 3px `{colors.accent}` marker at the left edge. **`sidebar-row-current`** is the current row. A disabled row is `{colors.disabled}`; a badge is a Tag at the row's right edge (hidden when narrow); a section heading is a 600-weight Caption line in `{colors.text-3}` (a 1px `{colors.separator}` rule when narrow). The app's footer content sits above Back and Forward, which sit in its footer unless hidden. Code: `Sidebar`, `NavEntry`, `NavigationButtons`; an app with its own sidebar builds it from `NavItem` rows.

**`status-bar`** — 28px along the window's bottom, under the sidebar too. `{colors.raised}`, 1px `{colors.separator}` above. The latest problem on the left (a dot in its status colour and the message), a live count on the right. Code: `StatusBar`, `Shell.Layout`.

**`page-header`** — Every page's title (`{typography.title}`) and one line of help (`{colors.text-2}`), its actions right-aligned on the title row with the Primary rightmost. A subpage puts "‹ Parent" at the far left. Code: `FormLayout.Header`, `FormLayout.SubHeader`.

**`block-heading`** — A group of a page: a `{typography.section}` heading, one line of help in `{colors.text-3}`, its controls, and a faint line above. Blocks sit in a masonry. Code: `FormLayout.Block`, `Masonry`, `FormLayout.Chapter` for numbered chapters.

**`tab-active`** — `{typography.label}` with a 2px `{colors.accent}` underline that slides between tabs. Tabs switch views of one thing, never pages. Code: `TextTabs`.

## Do's and Don'ts

### Do
- Build every screen from Shade's components and roles: `B(Raised)`, `Build.Text(text, Text2)`. Find the control in *Choosing a component*.
- File shared bugs and gaps in Shade using [docs/APP_AGENTS.md](docs/APP_AGENTS.md); never patch a control locally.
- Keep one Primary button per area, rightmost in its row.
- Put a word next to every status colour.
- Work a computed colour (a blend, a tint, a state) out inside the control's `Restyle` (`IRestyle`) or `Render`, so a new mode or accent paints it again.
- Check a change in both modes.

### Don't
- Don't write a colour, size or corner as a literal in a view.
- Don't use the accent for status, or a status colour for decoration.
- Don't put a box around a block of a page; the line above it is enough.
- Don't use pills, shadows or glows.
- Don't put a raw Avalonia `Button`, `TextBox`, `ComboBox`, `CheckBox`, `ToggleSwitch`, `DatePicker` or `ProgressBar` in a view; each has its Shade control.
- Don't use corners, typography or spacing outside Shade's tokens and documented component geometry.
- Don't hand-build a component from a `Border` (a card, a chip, a progress bar, a tab strip) or copy a Shade control into the app.
- Don't put a second Primary button in an area, or Danger next to the Primary.
- Don't end an action label in dots.

## Responsive Behavior

### Breakpoints
Shade is for desktop windows, so it has no device breakpoints. Layouts follow the space they get:

| Space | Key Changes |
|---|---|
| A wide page (≥ 3 × 300px) | Blocks in three masonry columns |
| A medium page | Two columns |
| A narrow page or a dialog | One column; page headers wrap their help line |
| A small window | The sidebar collapses to 54px icons (by the user, a click on the mark) |

### Touch Targets
- Standard fields, buttons and rows default to 28px; chips and tags default to 24px. Component-specific geometry and
  supported text/density scaling follow [ADOPTION.md](docs/ADOPTION.md).
- Anything clickable takes the pointer on its whole box: an icon sits on a transparent square.
- Keyboard: Tab order follows reading order, keyboard focus uses the shared focus border, Esc closes/cancels, and Enter
  activates the area's Primary where appropriate. Enter never confirms a destructive action or submits a multi-line editor.

### Collapsing Strategy
- The masonry drops a column rather than shrinking blocks.
- Tables scroll inside their area under a header that stays; the page header never scrolls away.
- Long text ends in "…" with the whole value in a tooltip; nothing runs into the next column.

## Iteration Guide

1. Change one component at a time, in `src/Shade/Controls`, and refer to it by its key here (`{components.dropdown}`).
2. Paint roles with their brush (`B(tone)`); work computed colours out in `Restyle` or `Render`.
3. A new component goes into the shared controls, this file (its key, its prose and a row in *Choosing a component*) and the gallery (`gallery/Showcase.cs`) in one change; `tests/Shade.Tests` fails when a public control is missing from either.
4. Run `dotnet run --project gallery -c Release -- --docs .` and `npx @google/design.md lint DESIGN.md`: it checks contrast in both modes, checks that switching modes live paints exactly what a fresh build does, renders the previews and rewrites this file's tokens and colours.
5. Look at the previews you touched in both modes before committing.

## Known Gaps

- Desktop only: Avalonia on Windows (it builds and renders on Linux and macOS, untested there).
- Motion (the sidebar's roll, a dialog rising in, tab underlines sliding) is described in `docs/DESIGN_SYSTEM.md` but not tokenised.
- A fixed category colour (`new Tone(color)`) is the same in both modes; a project that needs per-mode category colours picks them itself.
- High-contrast and right-to-left layouts are not designed yet.
- The live page's components (`docs/shade.css`, `docs/shade.js`) are a hand-made web copy of the Avalonia controls; they share the roles but not the code, so a change is made in both.

## Application integration

[Adoption guide](docs/ADOPTION.md) describes startup options, validation, culture-aware parsing, async actions, table edits and compatibility limits.

---
version: 1.0.0
name: Shade
description: A dark-first, dense design system for desktop work tools, with a matching light mode. Thirteen colour roles named by job, one user-chosen accent that every shade is derived from by rule, Inter at five sizes, a 4 px grid, 4 px corners and 28 px controls.

colors:
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
  accent-text: "#8C7AE6"
  on-accent: "#151519"
  success: "#83CEA5"
  warning: "#FFB142"
  danger: "#E0685C"
  danger-tint: "#392729"
  info: "#6FA8DC"

colors-light:
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
  accent-text: "#6A58D6"
  on-accent: "#FFFFFF"
  success: "#1F8A57"
  warning: "#A86400"
  danger: "#C4453A"
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
    fontFeatures: no ligatures

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

sizes:
  control-height: 28px
  chip-height: 24px
  sidebar: 180px
  sidebar-collapsed: 54px
  status-bar: 28px

components:
  button-primary:
    backgroundColor: "{colors.accent}"
    textColor: "{colors.on-accent}"
    typography: "{typography.label}"
    rounded: "{rounded.control}"
    height: 28px
    padding: 0 12px
  button-primary-hover:
    backgroundColor: "{colors.accent-hover}"
  button-secondary:
    backgroundColor: "{colors.fill}"
    textColor: "{colors.text}"
    borderColor: "{colors.separator}"
    typography: "{typography.label}"
    rounded: "{rounded.control}"
    height: 28px
  button-ghost:
    textColor: "{colors.text-2}"
    hoverBackgroundColor: "{colors.fill}"
    typography: "{typography.label}"
    rounded: "{rounded.control}"
    height: 28px
  button-danger:
    backgroundColor: "{colors.danger-tint}"
    textColor: "{colors.danger}"
    typography: "{typography.label}"
    rounded: "{rounded.control}"
    height: 28px
  button-disabled:
    backgroundColor: "{colors.raised}"
    textColor: "{colors.disabled}"
    typography: "{typography.label}"
    rounded: "{rounded.control}"
    height: 28px
  text-field:
    backgroundColor: "{colors.raised}"
    textColor: "{colors.text}"
    placeholderColor: "{colors.text-3}"
    borderColor: "{colors.separator}"
    hoverBorderColor: "{colors.outline}"
    focusBorderColor: "{colors.accent}"
    invalidBorderColor: "{colors.danger}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    height: 28px
    padding: 0 8px
  suggestion-field:
    backgroundColor: "{colors.raised}"
    borderColor: "{colors.outline}"
    rounded: "{rounded.control} {rounded.control} 0 0"
    maxSuggestions: 8
  label:
    textColor: "{colors.text-2}"
    typography: "{typography.label}"
    gapToField: 4px
  dropdown:
    backgroundColor: "{colors.raised}"
    textColor: "{colors.text}"
    borderColor: "{colors.separator}"
    typography: "{typography.body}"
    rounded: "{rounded.control}"
    height: 28px
  dropdown-open:
    backgroundColor: "{colors.raised}"
    borderColor: "{colors.outline}"
    rounded: "{rounded.control} {rounded.control} 0 0"
    itemHeight: 28px
    currentItemBackground: "{colors.fill}"
  menu:
    backgroundColor: "{colors.raised}"
    borderColor: "{colors.separator}"
    rounded: "{rounded.control}"
    itemHeight: 28px
  switch:
    width: 32px
    height: 18px
    onColor: "{colors.accent}"
    offColor: "{colors.fill}"
  tick:
    size: 16px
    rounded: "{rounded.control}"
    onColor: "{colors.accent}"
  state-toggle:
    height: 28px
    rounded: "{rounded.control}"
    onColor: "{colors.accent}"
    offColor: "{colors.danger}"
    pendingBorder: "{colors.warning}"
  filter-chip:
    height: 24px
    rounded: "{rounded.control}"
  filter-chip-active:
    backgroundColor: "{colors.accent-tint}"
    textColor: "{colors.accent}"
  tab-active:
    textColor: "{colors.text}"
    underline: 2px {colors.accent}
    typography: "{typography.label}"
  tag:
    height: 24px
    rounded: "{rounded.control}"
    backgroundColor: tint of its colour
  status-badge:
    dot: 8px
    gap: 6px
    typography: "{typography.body}"
  table:
    headerBackground: "{colors.raised}"
    headerText: "{colors.text-2}"
    rowHeight: 28px
    rowDivider: 1px {colors.separator}
    hoverBackground: "{colors.fill}"
    selectedBackground: "{colors.accent-tint}"
  compact-table:
    rowHeight: 28px
    rowDivider: 1px {colors.separator}
    fadedRowOpacity: 0.65
  tooltip:
    backgroundColor: "{colors.fill}"
    borderColor: "{colors.outline}"
    textColor: "{colors.text}"
    typography: "{typography.label}"
    rounded: "{rounded.control}"
    padding: 5px 8px
  dialog:
    backgroundColor: "{colors.background}"
    rounded: "{rounded.card}"
    padding: 24px
    buttons: Cancel then the action, bottom right, at least 96px
  color-picker:
    backgroundColor: "{colors.raised}"
    borderColor: "{colors.outline}"
    rounded: "{rounded.card}"
  empty-state:
    title: "{typography.section}"
    note: "{colors.text-2}"
  disclosure:
    textColor: "{colors.text-2}"
    typography: "{typography.label}"
  summary-cards:
    backgroundColor: "{colors.raised}"
    borderColor: "{colors.separator}"
    activeBorderColor: "{colors.accent}"
    rounded: "{rounded.card}"
    height: 84px
    title: "{typography.label}"
    value: 18px 600
    line: "{typography.caption}"
  search-field:
    backgroundColor: "{colors.raised}"
    borderColor: "{colors.separator}"
    iconColor: "{colors.text-3}"
    rounded: "{rounded.control}"
    height: 28px
  progress:
    height: 6px
    rounded: 3px
    trackColor: "{colors.fill}"
    barColor: "{colors.accent}"
    countdownColor: "{colors.text-3}"
  reference-card:
    backgroundColor: "{colors.raised}"
    borderColor: "{colors.separator}"
    rounded: "{rounded.card}"
    padding: 12px
    title: "{typography.section}"
    code: "{typography.mono}"
  tray-menu:
    backgroundColor: "{colors.raised}"
    borderColor: "{colors.separator}"
    rounded: "{rounded.control}"
    itemHeight: 28px
  sidebar:
    backgroundColor: "{colors.raised}"
    borderRight: 1px {colors.separator}
    width: 180px
    collapsedWidth: 54px
    itemHeight: 28px
    activeBackground: "{colors.accent-tint}"
    activeMarker: 3px {colors.accent}
  status-bar:
    backgroundColor: "{colors.raised}"
    borderTop: 1px {colors.separator}
    height: 28px
    typography: "{typography.caption}"
  page-header:
    title: "{typography.title}"
    hint: "{colors.text-2}"
    padding: 16px
  block:
    title: "{typography.section}"
    help: "{colors.text-3}"
    lineAbove: 1px {colors.separator} at 60%
    layout: masonry, 32px between columns, 20px between blocks
---

## Overview

Shade is a dark-first design system for **desktop work tools**: apps people keep open for hours to configure, watch and act
on data. The page is a calm ladder of near-neutral greys with a faint violet cast (`{colors.background}` → `{colors.raised}` →
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
- One control height, 28 px (`{sizes.control-height}`), for fields, buttons, dropdowns and table rows; 24 px for chips and tags.
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
- **Accent text** (`{colors.accent-text}` — #8C7AE6 · light #6A58D6): Small text in the accent, at 4.5:1.
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
- **Danger tint** (`{colors.danger-tint}` — #392729 · light #F6E3E1): The Danger button's fill.
- **Info** (`{colors.info}` — #6FA8DC · light #2C6CB0): Overrides, notices.

<!-- /generated -->
### Rules
- **Tint(c):** 15 % of the colour over `{colors.raised}`: selected rows, active chips, status tags, the Danger button.
- **Hover(c):** the colour with 10 % white.
- **On(c):** dark ink (#151519) or white, whichever contrasts more with the fill.
- **Lifted text:** accent and danger text move toward white (dark mode) or ink (light mode) until they read at 4.5:1 on `{colors.raised}`.
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
Five sizes and nothing between them: a size given in points is rounded to the nearest step. Weight carries emphasis
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
- **Shell:** the sidebar (`{sizes.sidebar}`, `{sizes.sidebar-collapsed}` collapsed), the page, and the status bar (`{sizes.status-bar}`) under both.

### Whitespace Philosophy
Dense inside a group, generous between groups. Space and a faint line group things; boxes don't. Nothing is bigger than its
content needs: a port field is 80px, a name 240px, and only paths, URLs and free text stretch.

## Elevation & Depth

| Level | Treatment | Use |
|---|---|---|
| Flat | No shadow, no border | The page, blocks, the sidebar's rows |
| Line | 1px `{colors.separator}` | Table rows, the sidebar's edge, the status bar, a block's line (60 %) |
| Raised | `{colors.raised}` fill, 1px `{colors.separator}` | Fields, menus, table headers, the sidebar |
| Floating | `{colors.raised}` or `{colors.fill}`, 1px `{colors.outline}` | Open dropdowns, tooltips, menus |
| Modal | `{colors.background}`, `{rounded.card}` | Dialogs, over their owner window |

There are no shadows on controls and nothing glows. Keyboard focus is a 1px `{colors.accent}` border.

## Shapes

### Border Radius Scale

| Token | Value | Use |
|---|---|---|
| `{rounded.none}` | 0px | Table rows, the sidebar and its rows, selections |
| `{rounded.control}` | 4px | Buttons, fields, dropdowns, menus, chips, tags, tooltips |
| `{rounded.card}` | 8px | Dialogs, cards, reference cards |

Switches are the one round shape (their track and thumb).

## Components

### Navigation & Shell

**`sidebar`** — The app's navigation, left. `{colors.raised}`, 1px `{colors.separator}` on its right, 180px wide (54px collapsed, icons only). The app's name and mark at the top; a click on them collapses the bar with a 420ms roll. Top-level rows 28px with an 18px Lucide icon; child rows 26px, indented. The current row has `{colors.accent-tint}` and a 3px `{colors.accent}` marker at the left edge. Back and Forward sit in its footer. Code: `Sidebar`, `NavEntry`, `NavigationButtons`.

**`status-bar`** — 28px along the window's bottom, under the sidebar too. `{colors.raised}`, 1px `{colors.separator}` above. The latest problem on the left (a dot in its status colour and the message), a live count on the right. Code: `StatusBar`, `Shell.Layout`.

**`page-header`** — Every page's title (`{typography.title}`) and one line of help (`{colors.text-2}`), its actions right-aligned on the title row with the Primary rightmost. A subpage puts "‹ Parent" at the far left. Code: `FormLayout.Header`, `FormLayout.SubHeader`.

**`block`** — A group of a page: a `{typography.section}` heading, one line of help in `{colors.text-3}`, its controls, and a faint line above. Blocks sit in a masonry. Code: `FormLayout.Block`, `Masonry`, `FormLayout.Chapter` for numbered chapters.

### Buttons

**`button-primary`** — The one main action of an area. `{colors.accent}` fill, `{colors.on-accent}` text, `{typography.label}`, 28px high, 12px side padding, `{rounded.control}`. Hover `{colors.accent-hover}`.

**`button-secondary`** — Every other action. `{colors.fill}` fill, 1px `{colors.separator}`, `{colors.text}`.

**`button-ghost`** — Toolbars, Cancel, "+ Add". No fill, `{colors.text-2}`; `{colors.fill}` on hover.

**`button-danger`** — Deletes and every action that changes the outside world. `{colors.danger-tint}` fill, danger text. Named for what it does ("Delete project"), never "OK".

**`button-disabled`** — `{colors.raised}` fill, `{colors.disabled}` text, no hover. Something nearby says why.

Code: `Btn` (`BtnKind.Primary`, `Normal`, `Ghost`, `Del`), `Capsule`, `IconButton`.

### Inputs & Forms

**`text-field`** — 28px, `{colors.raised}`, 1px `{colors.separator}`, 8px padding, `{typography.body}`, placeholder in `{colors.text-3}`. Only the frame changes: `{colors.outline}` on hover, `{colors.accent}` with focus, `{colors.danger}` when invalid (with the message below it). Multi-line fields look the same. Code: `Field`, `HostPortField`, `Workbench.Lines`.

**`suggestion-field`** — A field with up to eight ranked matches in an attached list; field and list are one shape while open. Code: `SuggestionField`.

**`label`** — `{typography.label}` in `{colors.text-2}` above its field, 4px apart; help under it in `{typography.caption}`. Code: `Kit.Labelled`.

### Menus & Dropdowns

**`dropdown`** — Closed, it looks like a field with a chevron. **`dropdown-open`**: the list hangs below at the field's width and the two are one shape in `{colors.outline}`; items 28px, the current one on `{colors.fill}` without a checkmark. Items may keep their own colour. Code: `ChoiceButton`, `MultiChoice`.

**`menu`** — The same rows as a free card: `{colors.raised}`, 1px `{colors.separator}`, `{rounded.control}`. Danger items last, below a line. A long menu scrolls within ten rows, with its bar in a gutter outside the text. Code: `Menu.Panel`, `Menu.Show`.

### Toggles

**`switch`** — 32 × 18px; `{colors.accent}` when on, `{colors.fill}` when off. It acts at once.

**`tick`** — A 16px box, `{rounded.control}`, `{colors.accent}` with a check when on, a dash when mixed. A choice that applies on Save.

**`state-toggle`** — Field-sized ON/OFF blocks for a setting on another system, with Unknown, Pending and Undo states.

Code: `Switch`, `Tick`, `TickGrid`, `StateToggle`, `StateLook`.

### Tags / Badges

**`tag`** — 24px, `{rounded.control}`, a tint of its colour behind that colour's text ("3 new", "2 updated"), or `{colors.fill}` with plain text. A removable tag ends in ×. Code: `Tag`.

**`status-badge`** — A dot and a word: OK, Retry at 14:30, Failed, Syncing…, Scheduled, Disabled (a hollow dot). Code: `StatusBadge`.

### Tab / Filter

**`tab-active`** — `{typography.label}` with a 2px `{colors.accent}` underline that slides between tabs. Tabs switch views of one thing, never pages. Code: `TextTabs`.

**`filter-chip`** + **`filter-chip-active`** — One chip per value, 24px, with the value's colour dot. On: `{colors.accent-tint}` and accent text. Nothing on means everything. Code: `FilterChip`.

### Tables

**`table`** — One table for every list. Header 28px on `{colors.raised}` in `{typography.label}` / `{colors.text-2}`; rows 28px, `{typography.body}`, a 1px `{colors.separator}` under each. Numbers right, text left, cut text ends in "…" with the whole value in a tooltip. Hover `{colors.fill}`, selected `{colors.accent-tint}`. Code: `Workbench.Table`.

**`compact-table`** — A short configuration list at natural height; disabled rows fade. Code: `CompactTable`, `PairTable`.

### Overlays

**`tooltip`** — `{colors.fill}`, 1px `{colors.outline}`, `{typography.label}`, 5 × 8px padding, `{rounded.control}`. Every icon-only button has one. Styled by `Look.Apply`.

**`dialog`** — `{colors.background}`, `{rounded.card}`, 24px padding; a short first line becomes its title. Cancel, then the action named for what it does, bottom right. Esc cancels; Enter confirms unless the action is destructive. Code: `Ask.Confirm`, `Ask.ConfirmDelete`, `Ask.Info`, `Dialog`.

**`color-picker`** — Three tabs: Curated (eight accents with a value for each mode: Violet, Indigo, Purple, Fuchsia, Pink, Teal, Lime, Graphite), Palette and Mixer. Swatches are controls: 4px corners with a 1px `{colors.separator}` line, a 1px `{colors.text}` ring on the current one. Faded swatches are too low in contrast for the job. Code: `ColorGrid`, `Look.Accents`, `Look.SetAccent(dark, light)`.

### Monitoring

**`summary-cards`** — A row of equal 84px cards in monitoring words: the title in `{typography.label}` with a corner value; below, a 40px ring for a share or a 64px trend line at the bottom left, and beside it the figure (18px, 600, its status colour) with its unit and one `{typography.caption}` line on what needs a look. `{rounded.card}`, `{colors.raised}`; a card that filters gets a `{colors.accent}` frame. Code: `SummaryCards`.

**`search-field`** — A text field with a 16px magnifier in `{colors.text-3}`, × once typed (Esc clears), an optional count ("3 of 12"). Code: `SearchField`.

**`progress`** — A 6px pill (round ends), `{colors.fill}` track, `{colors.accent}` part, label above and amount at the right; unknown length slides; a countdown empties in `{colors.text-3}`. In the status bar: a 56px bar and the work's name. Code: `Progress`, `StatusBar.Busy`.

**`reference-card`** — Documentation beside a form: `{colors.raised}`, `{rounded.card}`, 12px padding, a `{typography.section}` heading and a help action, Mono codes with their meaning, examples as chips that fill the field, a note. Code: `ReferenceCard`.

**`tray-menu`** — The tray icon's menu: the menu card by the pointer, every item at natural height, the current page highlighted. Code: `TrayMenu`.

### Feedback

**`empty-state`** — Centred where the content would be: what's missing, one line of why, and the action that fixes it. Code: `Kit.Empty`.

**`disclosure`** — Reference help that opens on demand. Code: `FormLayout.Disclosure`, `InlineCode`.

## Do's and Don'ts

### Do
- Build every screen from Shade's components and roles: `B(Surface)`, `Kit.Text(text, Second)`.
- Keep one Primary button per area, rightmost in its row.
- Put a word next to every status colour.
- Work a computed colour (a blend, a tint, a state) out inside the control's `Restyle` or `Render`, so a new mode or accent paints it again.
- Check a change in both modes.

### Don't
- Don't write a colour, size or corner as a literal in a view.
- Don't use the accent for status, or a status colour for decoration.
- Don't put a box around a block of a page; the line above it is enough.
- Don't use pills, shadows or glows.
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
- Every control is 28px high; chips and tags 24px.
- Anything clickable takes the pointer on its whole box: an icon sits on a transparent square.
- Keyboard: Tab order follows reading order, focus shows a 1px accent border, Esc closes, Enter confirms.

### Collapsing Strategy
- The masonry drops a column rather than shrinking blocks.
- Tables scroll inside their area under a header that stays; the page header never scrolls away.
- Long text ends in "…" with the whole value in a tooltip; nothing runs into the next column.

## Iteration Guide

1. Change one component at a time, in `src/Shade/Controls`, and refer to it by its key here (`{component.dropdown-open}`).
2. Paint roles with their brush (`B(tone)`); work computed colours out in `Restyle` or `Render`.
3. A new component goes into the shared controls, this file and the gallery (`gallery/Showcase.cs`) in one change.
4. Run `dotnet run --project gallery -c Release -- --docs .`: it checks contrast in both modes, checks that switching modes live paints exactly what a fresh build does, renders the previews and rewrites this file's tokens and colours.
5. Look at the previews you touched in both modes before committing.

## Known Gaps

- Desktop only: Avalonia on Windows (it builds and renders on Linux and macOS, untested there).
- Motion (the sidebar's roll, a dialog rising in, tab underlines sliding) is described in `docs/DESIGN_SYSTEM.md` but not tokenised.
- A fixed category colour (`new Tone(color)`) is the same in both modes; a project that needs per-mode category colours picks them itself.
- High-contrast and right-to-left layouts are not designed yet.
- The live page's components (`docs/shade.css`, `docs/shade.js`) are a hand-made web copy of the Avalonia controls; they share the roles but not the code, so a change is made in both.

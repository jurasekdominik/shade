# Design system

A dark, dense design system for desktop work tools: apps people keep open for hours to configure, monitor and act on
data. It isn't tied to one framework or product. Each project keeps its own decisions (its domain colours, its pages,
its code names) in a companion `docs/DESIGN.md` of its own. The code is the `Baseline` library in this repository; the
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

## 2. Colour

Dark only. Every colour is a role token, so a light theme can be added later without touching screens.

**Thirteen roles** (the approach of Apple's HIG, Material 3 and Radix: colours named by job, shades derived by rule):

| Role | Value | Use |
| --- | --- | --- |
| `Background` | `#151519` | Page canvas |
| `Raised` | `#1B1B20` | Sidebar, fields, menus, table headers, dialogs, reference cards |
| `Fill` | `#29292F` | Hover, Secondary buttons, pressed rows |
| `Separator` | `#35353D` | Every resting 1 px line |
| `Outline` | `#606069` | A control's border on hover or while open |
| `Text` | `#EDEDF0` | Main text |
| `Text 2` | `#B1B1BC` | Labels, supporting text |
| `Text 3` | `#9999A7` | Placeholders, metadata, hints |
| `Accent` | user's choice, default `#8C7AE6` | Primary action, selection, focus, active tab |
| `Success` | `#83CEA5` | OK, completed |
| `Warning` | `#FFB142` | Retrying, incomplete |
| `Danger` | `#E0685C` | Failed, deletes, actions that change the outside world |
| `Info` | `#6FA8DC` | Overrides ("differs from inherited"), notices |

**Shades are computed, never stored:**

- **Tint(c)** is 15 % of the colour over `Raised`. Used for selected rows, the Danger button and status backgrounds.
- **Hover(c)** is the colour with 10 % white mixed in.
- **On(c)** is black or white text, whichever contrasts more with the solid colour.
- **Small accent text** is the accent lightened until it reaches 4.5:1 against `Raised`.
- **Disabled** text is `Text 3` at half opacity.

**The accent is the user's.** The status colours stay fixed; the accent picker warns when a chosen accent is too close to
one of them.

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
- Units go in the label or column header ("Timeout (ms)", "Price (EUR/MWh)"), not in every value.

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

Each component has exactly one implementation, and a gallery screen shows every component in every state.

### Buttons

| Kind | Look | Use |
| --- | --- | --- |
| Primary | `Accent` fill, On(accent) text | The one main action of an area (Save, Send, Run now) |
| Secondary | `Fill`, `Separator` line, `Text` | Every other action |
| Ghost | no fill, `Text 2`, `Fill` on hover | Toolbars, Cancel, inline actions, "+ Add" |
| Danger | Tint(danger) fill, danger text | Delete and every action that changes the outside world |

- 28 px high, 12 px side padding, at least 72 px wide (96 px in dialog button rows).
- List-like ghost actions can align their label left; toolbar and form buttons remain centered.
- **One Primary per area.**
- Risky actions are protected by a confirmation, not by a bigger button.

A hold-to-confirm button consumes the full pointer press: release cannot activate a new action when the button changes
its label. Early release, leaving the button, lost capture, disabling, detachment or window deactivation cancels the hold.
Elapsed time uses a monotonic clock. Holds do not shrink the hit target; progress provides the pressed feedback.

### Text field

- 28 px, `Raised` fill, `Separator` line, 8 px padding, `Body` text, `Text 3` placeholder.
- The fill stays `Raised` when hovered or focused; only the frame changes (`Outline`, then `Accent`).
- **Label** above as `Label`; **help** below as `Caption`.
- **Invalid:** a `Danger` border **and** the message below the field. A red border alone is not enough.
- **Multi-line** fields (descriptions, templates, code) look the same, at least 56 px high.
- **Search:** a leading magnifier, and a clear (×) once text is typed.

### Dropdown

- The closed field looks exactly like a text field, with a chevron on the right.
- **Open, field and list are one shape:** the list hangs directly below at the field's width, continuing its sides in
  `Outline`, with a faint line between field and first option.
- Items are 28 px with `Body` text and 8 px padding, lined up with the field's text. The current item has a highlighted fill and stronger text, without a checkmark.
- Items may carry their own colour or icon (a category's colour stays in the list, not only in the field).
- Danger items go last, below a divider, in `Danger`.
- **Keyboard:** arrows move, Enter picks, Esc closes, typing jumps to an item.

### Switch and checkbox

- **Switch:** 32 × 18 px with a capsule thumb. It applies at once.
- **Register state switch:** field-sized ON/OFF blocks use the same 4 px outer corners as buttons and fields,
  with an inset state block. Unknown, pending and undo states keep that shape.
- **Checkbox:** 16 px box, 4 px radius, `Accent` fill with a check when on, a dash for mixed. For choices that apply on Save.
- A switch with a description sits **left** of its label in forms; in a settings row it sits at the row's right end
  (§9).

### Tabs

- `Label` text with a 2 px `Accent` underline under the active tab; the underline slides between tabs.
- Tabs switch views of **one** thing (a request's parts, a list by category). They never navigate between pages; sections
  of a page belong in the sidebar.

### Typed structured parameters

A native parameter tree reuses fields, type dropdowns, include/omit checkboxes and Add/Remove actions. Objects and
arrays preserve unknown properties, IDs as text, empty values, null, zero and false. Advanced JSON shares the same
request; invalid edits remain visible and block sending rather than being silently reset. Sensitive variables are
masked, and previews use redacted copies. Parameter name/index, type, value and row action columns align even
when array indices gain digits. Rows keep an eight-pixel gap; checkboxes and labels center vertically with the
28-pixel fields. Variable editors label their shared columns once above the rows. Include is distinct from Sensitive: an unchecked
Include preserves a draft value while omitting its binding; sensitive values stay masked. JSON formatting and
validation actions share the editor-mode toolbar. Response projections appear with Table only.

### Method suggestions

An editable field may show up to eight ranked name matches in the shared dropdown surface. Typing retains focus;
arrows highlight, Enter selects and Escape dismisses. Custom text stays supported. A selection that opens a new
draft preserves the existing draft before applying the template. Long dropdown menus scroll within ten rows,
show their current item and keep keyboard navigation visible. Every scrolling menu reserves a 16 px gutter outside
its row content; scrollbars never overlay labels or actions. This is shared-control behavior, not a per-screen fix.
Short menus have natural height without a scroll container or reserved gutter. Menus are at least the invoking field's width and grow to fit the widest option.
Tray menus always show all items at natural height and contain no scroll viewer or height limit. Filter pickers show
the active selection in their label.

### Filter chips

- One chip per value, all shown, 24 px high, with the value's colour dot.
- A click switches a chip on (highlighted) and again off. Nothing on = everything.
- A summary card that stands for a filter (§8) switches it the same way and has an `Accent` frame while on.
- No "Filter ▾" dropdowns for short lists; a long list (dozens of values) gets a search instead.

### Choosing several from a known list

- Shown in columns, never as a comma-separated text field.
- Switches for what something does (e.g. which data to read), with the display name and its details on one line and the
  technical key as tooltip; tick boxes for format-like options.

### Lists of values and name/value pairs

- **A list of values** (email recipients): one field per row, × to remove, "+ Add" under the rows.
- **Name/value pairs** (query parameters, headers, variables): a table with Name and Value columns, one row per pair with ×,
  "+ Add", and a "Common ▾" menu where well-known names exist (HTTP headers). The column heading shows only over rows.
  Never "Name: value" lines in a text box. A name twice is refused when saving.

### Tables

Every data table has a 1 px `Separator` outer border. Header/row height includes the row divider.
Data tables can sort and multi-select: a header click sorts by that column (an arrow after the heading shows the direction; click again to reverse), and
`selection` turns on Ctrl/Shift multi-select, reporting the selected rows.
A table can start its file rows with a tick box that follows the row's selection (`checkable`; with `toggle` a click ticks or unticks without Ctrl), and tint a row's background (`fill`).
Small configuration lists use `CompactTable`: natural row height, no internal scrolling or reserved scrollbar gutter;
the page owns scrolling. Header text is 12 px, body text 13 px, matching response tables. Optional row actions align in a right-hand column. Reordering preserves the selected inspector and its field focus; keyboard arrow focus follows its row. Trigger text uses semantic colors alongside
its label; disabled rows use `FormLayout.InactiveRowOpacity`. Large response tables retain their own scrolling.
Short captured-data previews fit their header and rows; scrolling starts above `FormLayout.PreviewRows`.
Ghost icon actions keep square hit areas and transparent backgrounds even when disabled; unavailable icons fade.
`FormLayout.Field` preserves the intrinsic width of a switch instead of stretching it to the field width.
`SqlResultMapping` pairs each result column and example value with a destination column field and key switch,
preserving existing mappings when a capture does not contain their source column.


One table component for every list.

- **Header:** 28 px, `Raised` fill, `Label` text in `Text 2`, kept visible while scrolling.
- **Rows:** 28 px, `Body` text, 8 px cell padding, a 1 px `Separator` under each row.
- **Alignment:** header and cells share one grid, so every column sits on the same pixels. Numbers right, text left.
- **Hover** `Fill`. **Selected** Tint(accent) with a 3 px `Accent` marker at the left.
- **Overflow:** long text ends in "…" and is shown whole in a tooltip; it never runs into the next column.
- **Name column:** as wide as the longest name, between 120 and 320 px.
- **Counts, not lists:** a column of things a row uses shows how many ("3"), not their names.
- **Status column:** a coloured dot in a narrow column (hollow when off); the word is its tooltip and is written out on
  the item's own page.
- **A column with two readings** (a cron expression or its plain-English meaning) switches on a click of its heading.
- **Opening a row:** a list that drills into a view opens on a click; a list whose row opens an editor opens on a
  double-click, so a click only selects.
- **Empty:** the empty state (below) in the middle of the table area.
- The table scrolls inside its area; the page header and toolbar never scroll away.

### Folder picker

`FolderPick` is a small folder browser for a form: a path field above a short list (six rows) of the folder's items,
folders first and in the accent colour, a `..` row to go up, sizes in the second column. A double-click opens a folder;
typing a path and pressing Enter opens it. Files can be selected (Ctrl and Shift for several); folders never are.
A line replaces the list until something is read, and a failure appears under it in the danger colour. Two pickers side
by side make a from/to pair, with an arrow between them.

### Tags

A `Tag` is a small read-only label, 24 px high with 4 px corners (no pill): a tint of its colour behind that colour's text
for a count or a result ("3 matches", "3 new" in `Success`, "2 updated" in `Warning`), or `Fill` with plain text when
it has no colour ("37 up to date"). A removable tag ends in a cross and holds a chosen item (a file in a list of
chosen files). A tag shows a value; a filter chip (above) switches something.

A dropdown can show a Lucide icon before the chosen item (Upload, Pattern): 16 px, `Text 2`, six pixels from the text.

### Empty state

Centred in the area that would hold the content: a `Section` title, one line of why, and the action that fixes it
("Clear filters", "New job"). Never a lone line of grey text in a corner.

### Summary cards

A row of equal cards above a list, one per thing worth watching (e.g. a kind of item, problems, what runs next): a title,
a one-line subtitle, a big number with its meaning, a ring for a share (how many are OK), one line on what's wrong, a
small value in the top-right corner. 8–14 px corners, `Raised`. A card that stands for a filter toggles it (§6 chips).

### Run-condition blocks

`RunConditionBuilder` uses the same choices, fields and ghost icon buttons as other forms. All/Any combines a flat list
of variable/operator/value blocks. Expression mode preserves custom or nested formulas; unsupported expressions are
never replaced automatically. Literal values are quoted as data. Show expression/template reference help through
`FormLayout.Disclosure`, collapsed and created on demand.

### Reference cards

Documentation beside a form (syntax, placeholders, built-in variables) is a card: `Raised`, 8 px corners, 12 px
padding, a `Section` heading and a two-column grid of `Mono` code and its meaning. It's the one place a box is used
inside a form, because it isn't a setting.

### Scroll bars

- One style: an 8 px rounded thumb, no visible track; brighter on hover or drag.
- The bar always matches the content: content lives in a real scroll container and is measured by it, never placed on a
  fixed-size canvas. Dragging the thumb and the mouse wheel both move content **and** thumb, in any order.

### Menus

Same rows as the dropdown list, as a free-standing card (all four borders, 4 px corners). A right-click menu opens at the
pointer; a `•••` menu 2 px under its button.

### Dialogs

- 8 px corners, 24 px padding, `Title` heading.
- Buttons bottom right: Cancel, then the Primary action, each at least 96 px. Same order in every dialog.
- Esc cancels; Enter confirms unless the focus is in a multi-line field.
- **Destructive confirmations** say what will happen, in words, and use a Danger button named for the action ("Delete
  job"), never "OK".

## 7. Status

One set of states, everywhere:

| State | Colour | Example words |
| --- | --- | --- |
| OK | `Success` | OK · Completed · Connected |
| Warning | `Warning` | Retry at 14:30 · Incomplete |
| Failed | `Danger` | Failed · Offline |
| Running | `Accent` (pulsing) | Reading… |
| Waiting | `Text 3` | Scheduled · Waiting for the window |
| Off | hollow `Text 3` | Disabled |

- **On its own** (a page header, a panel): a dot and the word.
- **In a table:** the dot, with the word as tooltip (§6 Tables).
- **In filters:** a chip per state with its dot (§6 Filter chips).

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
  right-click / `•••` menu in a fixed order (Run now, Edit, Duplicate, Delete — Delete last, below a divider).
- **Content** fills the remaining height and scrolls inside.

## 9. Forms, settings and editors

**Blocks in responsive columns, not one long column.**

Compact editors use the shared form constants: 28 px table/control rows, 112 px initial multiline fields and 300 px
minimum editor columns. Reuse `FormLayout.RowHeight`, `TextAreaHeight` and `EditorColumn`; taller content scrolls in
its existing page/container. The gallery includes a compact-editor example.

- Each group is a **block**: a `Section` heading, at most one line of help (`Caption`, `Text 3`), then its rows.
- A block has no box. A faint line (`Separator` at 60 %) runs above it, 12 px over its heading.
- Blocks sit in a **masonry** layout, like a CSS masonry grid: as many columns as fit at about 300 px each (up to three
  on a page, two in a dialog or editor), 32 px between columns and 20 px between blocks, each block placed in the
  shortest column so far. Resizing the window moves the blocks; nothing scrolls sideways.

**Inside a block:**

- **Settings rows:** a label on the left, its control at the right end of the row. A row is 32 px (44 px with a one-line
  caption). No line between rows.
- **Value groups** (connection defaults, a mail server): a grid of **equal-width cells**, two per row, label above each
  field. Equal cells read as a table and keep the edges aligned; widths from content make ragged columns.
- **A field alone** is as wide as its longest value would need (a port 80 px, a time 72 px, a name 240 px); only paths,
  URLs, templates and free text stretch.
- **Help is short:** one line under a heading at most; a row caption only when the label could be misread; anything
  longer goes behind an ⓘ tooltip by the label.
- **Show the current value, expand for the rest:** a rarely changed setting shows its value compactly and opens on click
  (an accent shows one swatch and its name; a click opens the swatches).
- **Say what a setting belongs to:** headings name their scope ("Connection defaults", "Email server").
- **Lists that live elsewhere aren't repeated:** a row shows their count and links to where they're edited.
- **Switches apply at once and need no banner.** A field saves when you leave the page; only a page with a real Save
  button has one.

**Creating vs editing:**

- **A new item** (a job, a connection) is a wizard: one step at a time, Back and Next.
- **An existing item** opens with one Save. Large job forms use General (name/description with a compact enable switch in the heading), Process, Schedule and Actions after run tabs; smaller editors can show every section on one page.
- A large editor opens **inside the main window**, not a separate window, so it gets the room; Esc or navigating away
  closes it.
- **Editor footer:** a bar across the editor's full width with a 1 px line above it: Delete at the left; Cancel · Back ·
  Next/Save at the right edge. The content column has one width on every step (up to about 1100 px, left-aligned), so
  nothing moves between steps or window sizes.
- In an editor that is one long page, section headings are `Section` size with their help line under, not page titles.
- A control that depends on another field (enable needs a connection) updates as soon as that field changes; never
  "save and reopen".

**Dialogs** follow the same rules: blocks in one or two masonry columns, labels above fields, Cancel and Primary bottom
right. Only a table's rows keep fixed columns.

## 10. App shell

- **Sidebar** on the left, 180 px (54 px collapsed): top-level rows 28 px, child rows 26 px indented 34 px, 4 px after
  every top-level entry. An active row has a Tint(accent) fill and a 3 px accent marker flush at x = 0, the row's full
  height. Only the collapse button sits under the items, in the same place expanded or collapsed.
- **Pages with sections** (Settings, a workspace's tools) expand their sidebar entry into the sections.
- **Status bar** along the window's bottom, 28 px, under the sidebar too: left, the latest warning or error with its time
  (a click opens the log); right, live counts (open connections). No logo, no path.
- **No breadcrumb bar, no global gear.** The sidebar says where you are; each page owns its title and actions.
- **Pages stay alive** when you navigate away, so drafts and open sessions survive. The app never reopens on a risky page
  at start.
- **Dangerous capabilities are hidden while off:** a feature that writes to the outside world is absent from the sidebar,
  menus and routes until a setting enables it (off by default).

Global history uses shared Back/Forward ghost arrows in the sidebar footer. Mouse side buttons and Alt+Left/Right
follow the same bounded page history; replay never repeats execution actions.

## 11. States and motion

- **Hover:** `Fill` or a brighter border. **Pressed:** one step darker.
- **Keyboard focus:** a 1 px `Accent` border, shown for keyboard use.
- **Disabled:** `Text 3`, no hover, and never the only explanation: say why nearby or in a tooltip.
- **Busy:** a button keeps its width and shows "…" or a small spinner. Nothing jumps.
- **Motion:** 120–220 ms ease-out for hover, fades and sliding underlines; 300–450 ms ease-in-out for a panel opening or
  closing (the sidebar). Nothing bounces or slides more than a panel's width.

## 12. Writing

- Sentence case everywhere ("Create job", not "Create Job" or "CREATE JOB").
- Plain words over jargon; the technical name goes in a tooltip.
- Errors name what failed and what to do next ("Enter a value for StartDate (Variables).").
- Buttons say what they do ("Delete job", "Send test email"), never "OK" or "Submit".
- Times say their zone once, where they're listed ("times in this PC's time zone").

## 13. Rules for builders (people and agents)

1. Only tokens and the shared components. No colour, font size, radius or height literals in views.
2. A missing component is added to the shared components **and** to this document **and** to the gallery, in one change.
3. Before finishing a UI change, render the gallery and screenshot the affected screens, and look at them.
4. Text sizes come from §3 (under 11 px is a bug); spacing from §4 (off-grid margins are a bug).
5. Every page starts with the shared header (§8); check "same action, same place" and "entering and leaving share a slot".
6. Every list is the shared table; every state uses §7.
7. Anything that deletes data or changes the outside world asks first, names its target, and is logged.
8. Nothing is bigger than its content needs (§9): a field wider than its value, or help longer than one line, is a bug.
9. Anything clickable takes the pointer on its whole box: a lone icon sits on a transparent square (a bare drawn shape
   only reacts on its lines), and a drawn control paints its whole area, transparent where empty.

### Inline reference code and response panes

Inline code is selectable monospace text that wraps and grows with its content. It shares the document's scrollbar
and offers Copy; editable request bodies keep the code editor. JSON examples put object properties and array entries
on separate lines. Continuous reference pages pair explanation and examples in each chapter, separate chapters with
a horizontal rule and keep chapter navigation beside the scrolling document. Parameter pickers support repeated
additions without closing, mark included fields, and reserve a gutter for the scrollbar; requirement labels and
data types use color together with words. A response pane can collapse to its status/action
row, expands on a real response and constrains both split rows during resize. Collapsing preserves data and size.

### Report column editor

A report layout uses a natural-height configuration table for source, heading, type and format, with square transparent
move arrows. A single selected-column inspector uses shared fields and source choices; format settings use the same
form controls. Place the layout at full content width, with the page owning scrolling. Show its scope and data source
above the table; previews reuse captured/stored data.

## Separate composition windows

Use an owned, resizable window for complex table/document composition. Keep delivery settings on the parent form.
Document composition uses available content/outline, canvas and selected-item properties; place properties below
on narrower windows. Table composition uses the full-width canvas with fields/properties below. Reuse fields, choices, ghost square move buttons and table borders. Drag handles have explicit hit areas;
Add and Move controls provide equivalent keyboard operations. Keep scrollbars outside content with reserved padding.
Undo/Redo and Apply/Cancel act on a draft. Apply returns it to the parent; the parent Save persists. Warn on dirty close.

For wide table composition, prioritize canvas width and place available content and wrapping properties below it.
Keep one Add command per list and expose equivalent insertion/removal through column context menus. Size columns
from headings and bounded visible samples; cap long values and show tooltips. Dropdown fields cache the widest
option's measured width when options change, within the space available, so field/menu edges align. Collapsible
appearance rules reuse the palette/mixer with a swatch and keyboard-editable hex value; background colors are not
subject to accent-text fading.

Rendered row backgrounds select black or white text by contrast; the same rule applies to email and workbooks.

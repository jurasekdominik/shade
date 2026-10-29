# Proposal: CharacterGridField

Draft for [Shade issue #105](https://github.com/jurasekdominik/shade/issues/105).
This records the original proposed contract. The first shared implementation now exists; its supported behavior and
remaining limits are owned by [ADOPTION.md](../ADOPTION.md#character-grids), and its appearance by
[DESIGN.md](../../DESIGN.md). API sketches and policies below remain proposals where they differ from those documents.

Runnable [gallery prototypes](character-grid-prototypes.md) compare the approved A (Paper) and C (Field) appearances
with native editing. Use those to judge the component before settling the remaining production contract.

## Purpose and appearance

`CharacterGridField` edits one string inside a fixed row of character boxes. The boxes, text, caret and selection keep
the same positions before, during and after editing. It fits identifiers, printed codes and short values on paper forms.
`Field` remains the ordinary text input; `EditableTable` remains the editor for rows of records.

```text
Serial number
┌───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┬───┐
│ 0 │ 0 │ 2 │ 6 │ 0 │ 1 │ 8 │ 3 │ 4 │ 2 │   │   │   │   │   │   │
└───┴───┴───┴───┴───┴───┴───┴───┴───┴───┴───┴───┴───┴───┴───┴───┘

Reading, with the final two cells given a separate fill
┌───┬───┬───┬───┬───┬───┬───╥───┬───┐
│ 0 │ 2 │ 4 │ 5 │ 8 │ 1 │ , ║ 3 │ 6 │
└───┴───┴───┴───┴───┴───┴───╨───┴───┘
```

The heavier division above only identifies the decorated range in this sketch; actual separator thickness is configurable.
The comma occupies a cell and remains ordinary editable text. Empty trailing boxes are presentation, not stored spaces.

Ordinary defaults use Shade's Mono typography, Raised surface, Text ink, Separator lines and normal control height.
Hover changes the outline; focus adds a FocusBorder outline and a caret. Neither replaces the field's fill. Selection uses
Shade's selection roles, invalid uses Danger plus an explanatory message, and pending uses the existing field treatment.

Paper geometry is explicit: a host can request 16 × 16 DIP cells, square corners, zero padding/gap, fine separators and
its own paper brushes. The same yellow or green fill remains visible while editing, including inside a dark shell.
These document colours belong to the app. Shade supplies no meter-specific palette, labels or form layout.

## Proposed public surface

Prefer `CharacterGridField : TextBox`, with a Shade template and the minimum presentation extension needed for fixed
cell advances. Reuse native properties, editing commands, routed events and automation. The base choice remains subject
to the feasibility check below; a composed native editor is acceptable if the same public contract is preserved.

| Area | Proposed API and meaning |
| --- | --- |
| Value | Native nullable `Text`, two-way by default with native binding validation. Preserve the exact string, including leading zeroes, spaces, punctuation and composed characters. |
| Geometry | `CellCount` is positive capacity in graphemes. `CellSize` is a finite positive `Size` in local DIPs; width is fixed character advance, height is the row height. `CellGap` is nonnegative and defaults to zero. |
| Inset and alignment | Native `Padding` surrounds the grid; `CellPadding` insets each cell's content without changing pitch. `CharacterAlignment` controls horizontal alignment inside each cell; native `VerticalContentAlignment` controls vertical alignment. |
| Surface and outer frame | Native `Background`, `Foreground`, `Opacity`, `BorderBrush`, per-side `BorderThickness` and `CornerRadius`. Transparent fills and zero outer borders work. |
| Cell lines | `CellBorderBrush` and per-side `CellBorderThickness`, independent of the outer frame. Shared edges are painted once. |
| Typography | Native `FontFamily`, `FontSize`, `FontWeight` and `FontStyle`. Changing a font never changes cell count or advance. |
| Caret and selection | Native `CaretBrush`, `SelectionBrush`, `SelectionForegroundBrush`, `CaretIndex`, `SelectionStart` and `SelectionEnd`; add `CaretThickness` only if the native surface lacks it. |
| Editing | `OverwriteMode`, default false; native `IsReadOnly`, `IsEnabled`, clipboard commands, undo/redo and routed keyboard input. |
| State | `Invalid`, `Pending`, `Error`, read-only `ValidationMessage` and `FocusBorderBrush`. Binding errors participate in the same validation presentation. Messages can be placed externally without increasing row height. |
| Cell appearance | Bindable `CellDecorations`, an observable collection of `CharacterCellDecoration` objects with zero-based `Start`, `Length` and optional `Background`, `Foreground`, `BorderBrush`, `BorderThickness`. |

New configuration values are Avalonia styled properties. Directly owned read-only state uses direct properties with
notifications. Decorations expose properties, support bound values and notify changes; adding, removing, replacing or
editing a decoration repaints without rebuilding the editor. Replacing a collection removes subscriptions to the old one.

The grid's desired size is:

```text
width  = CellCount × CellSize.Width + (CellCount − 1) × CellGap + Padding.Left + Padding.Right
height = CellSize.Height + Padding.Top + Padding.Bottom
```

Outer borders are drawn inside those bounds; cell borders are inside their allocated cells. Where edges meet, use the
greater thickness, with the later decoration winning equal-thickness brush conflicts. Outer-frame styling owns the
perimeter. Padding and border thickness must leave usable content space. Stretching a host changes neither cell pitch
nor glyph advance. A constrained host clips/scrolls the viewport without squeezing the cells.

Explicit compact dimensions stay fixed under density changes. Host document zoom transforms the complete control,
including hit testing. Text scaling must never silently shrink text to fit: the host must supply cells large enough for
its chosen typography, or scale the complete document. The implementation must document its minimum fitting metrics.

## Example usage

Proposed XAML; the new members below do not exist yet. Resource brushes are supplied by the host, and the normal Shade
defaults apply when they are omitted.

```xml
<shade:CharacterGridField
    Text="{Binding SerialNumber, Mode=TwoWay}"
    CellCount="16"
    CellSize="16,16"
    CellPadding="0"
    CellGap="0"
    Padding="0"
    CornerRadius="0"
    Background="{DynamicResource PaperFieldFill}"
    Foreground="{DynamicResource PaperInk}"
    BorderBrush="{DynamicResource PaperRule}"
    BorderThickness="0.5"
    CellBorderBrush="{DynamicResource PaperRule}"
    CellBorderThickness="0,0,0.5,0"
    FocusBorderBrush="{DynamicResource ShadeFocusBorder}"
    CaretBrush="{DynamicResource PaperInk}"
    SelectionBrush="{DynamicResource PaperSelection}"
    SelectionForegroundBrush="{DynamicResource PaperSelectionInk}"
    OverwriteMode="True"
    IsReadOnly="{Binding IsPrintPreview}"
    AutomationProperties.Name="Serial number" />
```

For `"024581,36"`, a decoration with `Start = 7`, `Length = 2` gives the decimal tail its own host-supplied fill.
Decorations address fixed cell positions; edits do not move their ranges. They never add punctuation or alter the value.

## Editing decisions

- One editor, string, focus stop and undo history per field. Tab/Shift+Tab leave the whole field. A pointer click places
  the caret at the nearest character boundary in that box; clicking unused trailing boxes places it at the end.
  Clicking does not select all. Dragging and Shift+arrows extend selection across the same geometry.
- Insert is the default: typing shifts following characters. Overwrite with no selection replaces the next input-length
  grapheme range, appending at the end if needed. With a selection, typing replaces that selection using normal text
  semantics. Backspace/Delete remove text and close the gap in either mode; an explicit space preserves a blank position.
- Home/End, arrows, Ctrl+A/C/X/V and undo/redo act on the whole value. A paste or replacement is one undoable edit.
  Single-line input rejects pasted tabs or line breaks with a translated explanation; it does not silently strip them.
- Capacity counts extended grapheme clusters. Native caret/selection indices remain UTF-16 offsets for compatibility;
  a shared mapping translates them to cell positions. Composed Czech text and surrogate pairs must never be split.
  Do not normalize, trim, uppercase or parse the string. Native `MaxLength` must not stand in for grapheme capacity.
- Typing/paste that would exceed capacity is rejected atomically. Preserve the old text and selection and expose a
  translated capacity error externally and to automation. A successful correction clears that capacity error; an app's
  separate validation message remains app-owned. No partial paste or silent truncation.
- External `Text` updates and a reduced `CellCount` preserve the complete value, even when over capacity. Mark it invalid
  and allow selection, copying and corrective edits through a horizontally scrollable fixed-pitch viewport. Keep the
  visible field's measured geometry. While over capacity, allow edits that reduce the overflow; reject edits that grow it.
  The host decides whether an invalid value may enter its persisted model.
- IME preedit is transient and uses the same cell layout. Apply capacity at composition commit, never by truncating or
  cancelling intermediate composition. A rejected commit preserves the prior text. Undo remains one composition edit.
- Read-only retains identical geometry and supports selection/copy. Disabled cannot mutate. Native routed keys let the
  host implement row navigation or submission; Shade does not choose the next row, save policy or Enter/Escape meaning.
- `TextChanged` retains native semantics for every text update. Appearance, language and viewport changes never edit text.
  User edits preserve bindings via the native editing path; no duplicate custom change event is proposed.

## Styling, accessibility and lifecycle

Publish template parts for the outer frame, cell surface and native text presenter. State hooks cover hover, focus,
read-only, disabled, invalid and pending; focused/selected cell styling is derived from the same caret/selection map.
App overrides use public properties and documented selectors without private template traversal.

Paint field fill, range fills, selection, glyphs, cell lines, state outlines and caret in that order. Later decorations
win overlapping properties; unspecified properties inherit the field appearance. Selection foreground overrides a range's
ink. Error wins the outer border over pending, focus and hover; the caret and focused-cell indicator remain visible.
State changes preserve explicitly supplied background brushes. Disabled defaults use the Disabled role; explicit paper
brush overrides remain under host control. Paper hosts must supply contrasting ink, caret, focus and selection brushes.

Use one accessible field name/value and native text selection/caret behavior. Announce capacity and validation errors;
do not expose each painted box as a separate input. Built-in messages and context-menu strings use Shade's localization
path. Live mode, accent and language changes preserve text, invalid drafts, caret, selection, composition and focus.
Brush and collection subscriptions must detach and reattach cleanly.

## Implementation check before committing the API

The current [TextBox theme](../../src/Shade/Controls/TextBoxTheme.cs) already uses native `PART_TextPresenter` for text,
selection and IME. [Field](../../src/Shade/Controls/Field.cs) adds field state around a native editor. Reuse those paths
where possible; keep ordinary fields unchanged.

First build a focused native-editor experiment: a fixed cell pitch with click placement, drag selection, undo/redo,
composed Czech text, font fallback and active IME at fractional zoom. The same layout must serve drawing, hit testing,
selection, caret and the IME candidate-window rectangle. Letter spacing alone must pass this check before being chosen;
do not assume it supplies a fixed advance for every grapheme or fallback font.

If Avalonia 12.1.4's public presenter/layout extension points cannot meet that contract, record the exact missing hook and
resolve it before claiming support. A grid painted under a differently spaced editor does not satisfy the issue.
No replacement clipboard, undo, focus or text-editing engine is proposed. Native text automation must also be verified,
not inferred from inheritance.

## Delivery and acceptance

Implement the component, its documentation and evidence together under the existing
[contribution workflow](../../CONTRIBUTING.md#implementation-and-documentation):

- [ ] Prove the native layout experiment, including the overflow and IME decisions above.
- [ ] Add the control and nullable public API entries, a parameterless constructor, compiled XAML and real two-way binding
  tests. Test external model updates after user edits, range collection changes, validation, undo/redo and read-only copy.
- [ ] Add a `character-grid-field` choosing row, token entries and appearance prose in [DESIGN.md](../../DESIGN.md);
  put the supported behavior and limits in [ADOPTION.md](../ADOPTION.md). Use only the allowed design.md sub-token names;
  give states their own keys. Supersede this draft with links to those owning documents when implemented.
- [ ] Show ordinary defaults and compact paper rows in `gallery/Showcase.cs`, in both modes: empty, populated, focused,
  selected, read-only, disabled, invalid, pending and a decorated tail. Include app-supplied paper fills in a dark shell.
- [ ] Inspect 75%, 100% and 150% document zoom plus common Windows display scaling. Compare fresh construction with live
  mode/accent/language changes, checking cell pitch, borders, clipping, caret, selection and unchanged neighbouring rows.
- [ ] Verify one accessible text field with value, selection, caret and validation; exercise hide/detach/reattach and
  updates during editing. If custom rendering is needed, add its justification to CONTRIBUTING's drawing inventory.
- [ ] Run the applicable final build, tests, docs generation/checks and DESIGN.md lint once on final code; inspect and
  commit affected previews in both modes. List `CharacterGridField` under Web sync for the release parity workflow.

Form composition, pagination, print/PDF/HTML export, masks, domain validation and persistence stay in the host. This
proposal adds one reusable input and leaves those separate concerns out of its API.

## Evidence and status

Reviewed issue #105 (no comments when fetched), its pinned standalone HTML reference, Shade's field/text-box source,
and the design/contribution contracts. The reference uses monospace text, letter spacing and repeating grid backgrounds;
it illustrates the desired presentation but does not verify the native editing contract.

The approved A and C appearances now share a native presenter with fixed grapheme advances, including proportional
fonts and fallback. Focused checks cover the full-capacity caret and IME preedit; the two appearances also have binding,
validation, range-styling and value-automation coverage. The remaining platform and API work is listed in ADOPTION.md
and issue #105; the full original acceptance matrix has not been claimed complete.

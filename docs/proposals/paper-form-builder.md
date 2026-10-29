# Paper form builder: first implementation

Work for [#105](https://github.com/jurasekdominik/shade/issues/105),
[#106](https://github.com/jurasekdominik/shade/issues/106) and
[#108](https://github.com/jurasekdominik/shade/issues/108). This is source-preview work, not a release or a claim that
the complete issue acceptance matrix has passed. Start the mixed-field gallery:

```powershell
dotnet run --project gallery -c Release -- --paper-form
```

The approved Paper/Field CharacterGridField implementation is reused from `feat/105-character-grid-field`. Its native
TextBox editor remains independently usable. The builder composes it with themed text boxes and checkboxes; it does not
introduce another text-editing engine. Compact checkboxes now have a public attached-property contract. The first
builder has physical page settings, drawing/select/move/resize, nested groups, keyboard movement, inspector, layout
undo/redo, separate field values, schema-validated JSON, extensible factories, Fill and Preview.

The requested export scope is **editable standalone HTML and read-only PDF**. Both use the same authored coordinates.
HTML saves current input values into a new standalone file. PDF is initially a flattened raster rendering of the native
page; it has no form fields and its text is not searchable. Storage, business validation and document approval stay in
the host. The supported contract and limits live in [ADOPTION.md](../ADOPTION.md#paper-forms); appearance lives in
[DESIGN.md](../../DESIGN.md#paper-forms).

Checks cover model round trips and rejection, binding preservation after edits/model updates, retained native editors on
moves, undo/redo, group children, drawing at 75/100/150% zoom, native fills, preview input protection, HTML text encoding,
overflow preservation and compact checkbox states/automation providers in both modes. The user feedback checks exercise
whole-cell drag resizing, instant/coalesced sidebar edits, generated keys, pointer-anchored Ctrl+wheel zoom, right-click
insertion, Ctrl+C/Ctrl+V with values, active-page undo/redo, grid-compatible equal-gap/edge snapping, inset text, aligned carets, and atomic
capacity rejection for typing/paste. The next review adds adopted/nested groups, group dragging over child content,
layer selection across pages, reparent/undo, grid sizing, installed-font selection, live palette/hex colours, full-height
menus, widest-caption dropdowns and bounded one/two-page zoom with scrolling for longer documents. The next editor pass
covers normal child/sibling clicks, marquee and native layer multi-selection, multi-field drag/copy/undo, layer reorder and
reparenting, grid-increment numeric spin/wheel/keyboard input, persistent grid settings, sidebar/ribbon property placement,
whole-grid alignment, stable colour-helper hex/footer/toggle behavior, document-used/recent swatches without theme-curated accents, Insert drawing guidance, icon history actions,
side-by-side pages, optional editor paging/printed page numbers and formatted calendar-date validation. The supplied screenshots established the missing text inset and separator-aligned
caret baseline; the focused regressions and regenerated pages record the fix. Browser checks exercise editable
HTML, composed grapheme placement, native undo, pointer zoom, multiline/checkbox values and save/reopen. Generated
PDF pages are inspected at the expected physical size and checked for absence of interactive widgets.

Pending: physical Windows display scaling/IME, actual screen-reader wording and designer selection automation, the
standalone CharacterGridField's remaining acceptance items, automatic distribution, page reorder/remove,
searchable/vector PDF, and the browser designer port. No issue should be closed based on this first increment.

Web sync: SearchChoice full-row pointer handling, CharacterGridField (Paper and Field), CheckBox appearance, ColorGrid document-palette mode, PaperFormView and PaperFormBuilder. The standalone
editable HTML supplies the fill/export surface now; the main web-gallery designer remains a release step.

## Paired page evidence

Windows, Avalonia 12.1.4/.NET 10, headless Skia and Chromium, 100% document zoom at 96 DIPs/inch. These use the same
mixed-field content, the same installed system font, physical geometry and explicit neutral paper palette. The palette intentionally
stays unchanged when switching the shell mode. Native glyph shaping/rasterization and browser canvas antialiasing differ;
this comparison covers the shown fill/preview page, not the pending browser designer or the full interaction matrix.

| Shell mode | Native page | Editable HTML page |
| --- | --- | --- |
| Dark | [Native](../preview/dark/09-paper-form-page.png) | [HTML](../preview/dark/09-paper-form-page-web.png) |
| Light | [Native](../preview/light/09-paper-form-page.png) | [HTML](../preview/light/09-paper-form-page-web.png) |

| Editing state | Native | Editable HTML |
| --- | --- | --- |
| Dark, glyph-aligned caret | [Native](../preview/dark/09-paper-form-caret.png) | [HTML](../preview/dark/09-paper-form-caret-web.png) |
| Light, glyph-aligned caret | [Native](../preview/light/09-paper-form-caret.png) | [HTML](../preview/light/09-paper-form-caret-web.png) |
| Dark, whole-cell selection | [Native](../preview/dark/09-paper-form-selection.png) | [HTML](../preview/dark/09-paper-form-selection-web.png) |
| Light, whole-cell selection | [Native](../preview/light/09-paper-form-selection.png) | [HTML](../preview/light/09-paper-form-selection-web.png) |

## Editor arrangements

| State | Dark | Light |
| --- | --- | --- |
| Property ribbon | [Preview](../preview/dark/09-paper-form-ribbon.png) | [Preview](../preview/light/09-paper-form-ribbon.png) |
| Two-page spread | [Preview](../preview/dark/09-paper-form-spread.png) | [Preview](../preview/light/09-paper-form-spread.png) |
| Layer insertion target | [Preview](../preview/dark/09-paper-form-layer-drop.png) | [Preview](../preview/light/09-paper-form-layer-drop.png) |
| Colour helper after a pick | [Preview](../preview/dark/09-paper-form-colour-picker.png) | [Preview](../preview/light/09-paper-form-colour-picker.png) |

Font result rows use the native button as the pointer target across both text and padding. Focused regressions include
highlighted labels, binding/event counts, pointer-consuming display templates and an 800-item scrolled picker in the editor.
The reported physical Windows interaction still needs a user recheck: the desktop automation helper could not activate the
running preview reliably.

| Font picker | Dark | Light |
| --- | --- | --- |
| Filtered installed fonts | [Preview](../preview/dark/09-paper-form-font-picker.png) | [Preview](../preview/light/09-paper-form-font-picker.png) |

## Authoring feedback batch

Checkboxes now resize as squares. Existing-element menus expose editing actions; empty-paper menus expose insertion and
guide creation. Separator lines print; rulers and draggable guides are editor-only metadata. Rulers, guides and grid each
have visibility controls. A per-structure SnapToGrid flag permits free movement. Numeric arrows use adjacent full-height
targets; placement/size APIs cover those buttons and the toolbar, grid settings, layer pane and property panels. Layer drops
show their insertion line and nesting level. Multiline fields reject typed, Enter-key and pasted overflow at their visible bounds or permit scrolling
drafts; model-loaded overflow remains intact, and export always rejects unprintable values.

The follow-up to #109 identified the remaining font-row issue: the result Button did not occupy the full ListBoxItem.
The corrected pointer regression clicks the container's trailing area, rather than just the Button's own bounds; it failed
before explicitly stretching the result Button and passes after it in both themes. Text, highlights, object/ID binding,
keyboard selection, invoke automation and drag-out cancellation retain their existing paths.

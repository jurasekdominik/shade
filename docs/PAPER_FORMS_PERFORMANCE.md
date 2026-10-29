# Paper forms performance work

Tracking [issue #115](https://github.com/jurasekdominik/shade/issues/115) on
`perf/paper-forms`. This is an implementation and review log, not a supported API contract.
Started 2026-10-29 from `origin/main` at `6b7666b`.

## Review status

Work in progress. No rendering replacement is accepted until the parity and accessibility gates below pass.
Existing local changes to `DESIGN.md`, `docs/index.html` and `inspection.pdf` predate this work and are excluded from commits.
No public API break or release is authorized by this work.

## Solutions and acceptance, in issue order

### Phase 0: measurement

- Extend `--paper-scale` to run both 1,000 × 5 and 500 × 10; count actual designer flushes for each operation, including
  synchronous callbacks. Record cold load separately from repeated edit/move/type timings.
- Enforce load ≤ 1,500 ms, edit/move ≤ 30 ms, typing ≤ 5 ms and at most one designer flush per operation. Wire the large
  cases into `full.yml`. Record failures honestly while the rendering phases are outstanding; do not relax the budgets.
- Add a Skia-rendered drag/zoom measurement separate from headless logic timings. Measure actual frames, not just event
  handler duration. Retain the same document, viewport and scale for comparisons.
- Measure retained undo memory after 100 resizes with full GC and a live builder; also count distinct retained structure
  references so GC noise cannot conceal copying unchanged branches.

### Phase 1: incremental work, unchanged appearance

1. Preserve identity first: remove/reorder/normalize helpers return the original structure and child list when unchanged.
   Skip pages without selected roots. Test nested groups, reparenting, ordering, undo/redo and unchanged page references.
2. Validate once: remember successfully validated immutable instances with weak keys; keep cross-document ID/key checks.
   Invalid definitions must still throw through both direct property assignment and edits.
3. Batch missing values: build one key set, collect all missing values, then publish one collection change. Keep duplicate
   key rejection and observable collection behavior for application-owned values.
4. Problems: compare published sequences; index sites by field key, cache local checks by structure reference plus text,
   and sweep sorted sibling bounds for overlaps. Preserve problem order, localization and global collision checks.
5. Designer updates: dirty flags for layers/toolbar/inspector/feedback/surfaces, nested synchronous edit scopes, one flush
   at the outer boundary. Property assignment and value callbacks need the same path; test counts before draining jobs.
6. Retain controls: construct toolbar choices once and refresh guarded values/items. Reuse inspector inputs when selection
   identity and kind agree; callbacks must read current selection instead of captured stale records. Check open popups,
   focus, invalid drafts and bindings after host updates.
7. Reference diff: skip shared page lists and structure instances in view reconciliation, validation and layers. Cache
   date-format parsing by string without retaining old documents. Normalize only changed paths.
8. Static design drawing: separate grid/rulers/margins from interaction overlay. Cache ruler text by culture/scale and
   invalidate on geometry, theme and language changes. Snapshot snap candidates at drag start; release on cancel/detach.
9. Wheel zoom: transient render scale, one layout commit after 150 ms idle. Preserve pointer anchor and scroll extent;
   flush/cancel the timer on detach, mode changes and export. Test repeated wheel input and programmatic zoom separately.
10. HTML export: track previously/currently selected grids, queue a Set of redraws through one animation frame, and only
    reset backing storage when size/DPR changes. Resize/theme redraw all. Browser checks cover caret and selection moves.
11. PDF/PNG: one reusable export session with one validated snapshot and one hidden view/window; move PageIndex between
    pages. Preserve custom templates, vector PDF output, resource disposal and staged writes on failure.

### Phase 2: drawn Design and Preview

- Extract built-in paint geometry from existing controls into an internal `PaperPainter`; reuse typography, character
  shaping and decorations rather than approximate them. One page visual; custom templates remain native child controls.
- Feed the same painter to PDF/PNG; remove the hidden window only after custom controls still have a supported rendering
  path. Preserve images, vector text, clipping and export failure behavior.
- Gate activation on live/drawn image comparisons for every built-in kind, empty/filled/overflow/error, alignments and
  cell decorations, both modes and multiple zooms. Include fresh/live appearance changes in `--docs`.

### Phase 3: Fill with a live editor

- Use one pooled editor per native kind, with only the active editor attached. Bind the current value with normal native
  semantics and forward the original pointer coordinates for caret placement; keep IME and date popup ownership native.
- Draw hover/error states and tab through a flattened ordered field index across pages. Automation focus/value changes
  use the same activation path as pointer and keyboard. Test editor disposal, rebinding and hiding while a popup is open.
- Supply virtual field peers with role/name/value/read-only/error; focusing or setting through a peer activates its editor.
  Run UIA and Narrator on Windows. Narrator wording cannot be established by headless tests.
- Keep caret, selection and undo with the active native editor; explicitly verify current unfocused selection behavior.
  Compare live/drawn handover pixels and record pointer, keyboard, IME and Narrator evidence before acceptance.

## Work and evidence

- Read issue body and audit comment, repository contract, design-system rules and contribution methodology.
- Confirmed `PlaceSelected` clones every unrelated leaf in both removal and ordering; `MoveSelection` also clones
  untouched groups and pages. Identity preservation is the first implementation step as required by the issue.
- Baseline and implementation results will be appended with the commands and actual outcomes.

### First implementation: shared edit paths

- Added copy-on-change mapping for child lists and retained unchanged branches in resize/reparent, move, property edits,
  deletion and grid normalization. A move skips pages with no selected roots. This is necessary before caching by identity.
- Added regressions for nested sibling/group/page identity, repeated resizes, moving out of a group, and undo/redo.
  `dotnet run --project tests/Shade.Tests -c Release -- --paper-performance`: all seven checks passed.
- Baseline, Windows headless Release, 1,000 × 5 on the starting code: load 8,553 ms; inspector edit 174 ms;
  move 113 ms; keystroke 13 ms; full problems 23 ms; 108,506 visuals. Existing loose budgets passed this slow run.
- Remaining undo cost includes the page's flat array of references; structures off the changed path are shared. This
  change removes retained copies of each unrelated structure, not the unavoidable replacement page/list snapshots.

### Validation, values and problems

- Successful assignment validation is memoised with weak definition keys. `Edit` validates before history changes and
  the property's callback reuses that result; public `Validate()` still explicitly checks a snapshot. Collections must
  remain immutable as documented. Date-format results use weak string keys, so old documents/formats are not rooted.
- Default values use Avalonia's existing range-capable list. Missing keys are collected once and added as a range;
  application-owned lists retain their identity and notification semantics. Internal lookup/subscription rebuilds happen
  once after the batch, including for application-owned ObservableCollection instances.
- Validation sites are indexed by field key. Cached checks include structure identity, value text/error, absolute area,
  container and overlapping neighbour identities, because parent movement and neighbour renaming also affect messages.
  Sibling intersections use an X sweep, retaining the original document/pair order. Dense all-overlapping layouts still
  necessarily produce quadratic numbers of warnings.
- Identical problem sequences keep the existing list and emit no change notification. Explicit `RefreshProblems()`
  invalidates the cache, including on language refresh. Added focused error/recovery, observable-value and invalid-edit
  tests; all 14 focused checks passed before the sweep-specific regression was added.

## Web sync

The editable HTML export is generated by C# and belongs to this work. The hand-written web port stays at its release
baseline per `CONTRIBUTING.md`. Any changed control behavior will be listed here for the later PR/release review.

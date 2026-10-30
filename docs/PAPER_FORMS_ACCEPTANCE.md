# Paper renderer acceptance

Tracking [#115](https://github.com/jurasekdominik/shade/issues/115). Results and remaining work belong in
[the performance log](PAPER_FORMS_PERFORMANCE.md). These are internal fixtures, not application API.
Every mode draws the page and Fill and Preview use the pooled editor by default. The automated gates below pass;
the human checks further down have **not** been performed yet and must be done before a release.

## Automated comparisons

Run Release builds. The default suite includes pooled editing, native undo/preedit and virtual automation tests.
The manual full workflow additionally runs:

```text
dotnet run --project tests/Shade.Tests -c Release -- --paper-parity
dotnet run --project tests/Shade.Tests -c Release -- --paper-parity filled --dpi
dotnet run --project tests/Shade.Tests -c Release -- --paper-handover
dotnet run --project tests/Shade.Tests -c Release -- --paper-handover --dpi
dotnet run --project tests/Shade.Tests -c Release -- --paper-export-parity
dotnet run --project tests/Shade.Tests -c Release -- --paper-scale
dotnet run --project tests/Shade.Tests -c Release -- --paper-frames
```

Run scale timings alone. Keep the 1,500/30/30/5 ms limits. Antialiasing tolerance applies only at existing edges;
neither handover nor export tests permit layout differences. Failure images are retained under `out/`.

## Windows automation and frame recording

Start `dotnet run --project gallery -c Release -- --paper-gates`, then run
`powershell -NoProfile -File gallery/paper-uia-check.ps1` from the repository root. This checks the real Windows
provider, screen bounds, native editor subtree, value/toggle/error, offscreen activation and Preview read-only state.
The fixture uses the internal painter override; production defaults are unaffected.

For frames, close that fixture and start a fresh
`dotnet run --project gallery -c Release -- --paper-desktop-frames`. Run
`powershell -NoProfile -File gallery/paper-desktop-frames.ps1`. It activates the fixture, sends a native drag and
Ctrl+wheel burst, releases Ctrl in `finally`, and checks that both operations happened. It leaves the desktop pointer
in place. Restart the fixture before repeating, so page position and zoom have the same baseline.

The ten-second recording writes `out/paper-desktop-frames.csv` and its exercise summary. Rows distinguish drag,
zoom and idle animation callbacks, including the 250 ms following input. The script reports median/p95/max intervals.
This measures the native Windows/Skia scheduling path, not GPU presentation. Capture the same interaction with
Windows Performance Recorder's GPU Activity profile and inspect DWM/present events in Windows Performance Analyzer
when presentation latency is required; report display refresh rate, DPI, backend and occlusion with that evidence.

## Human acceptance before a release

Use `--paper-gates` in both modes at 100%, 125%, 150% and 200% display scaling. Record Windows, Avalonia and input
method versions with the result. Automated preedit tests do not establish hardware IME or spoken output.

- With Narrator, visit every input and the offscreen second-page field. Verify spoken name, role, value, read-only
  state and validation error; edit through the native subtree, then move away and back without duplicate announcements.
- Tab and Shift+Tab across pages and into/out of the form. Use pointer placement, drag selection, double-click,
  right-click, copy/paste and native undo. Confirm focus/caret/selection stay aligned with the drawn artwork.
- With an installed Japanese/Chinese IME, start composition, change the theme and an unrelated document property,
  commit and cancel. Verify candidate-window placement, no lost or duplicated preedit, and no composition transferred
  to another field. Repeat with the hardware keyboard used by the consuming app.
- Open the date calendar, navigate and select a day, dismiss outside, and disable/hide/detach the form while open.
  Confirm popup focus stays with the editor and closes with it.
- Compare Fill and selectable Preview with the native renderer, including custom controls between drawn bands.
  Check screen-reader focus, hover/error artwork and focus handover for visible shifts or flicker.

Record the result (Windows, Avalonia and input-method versions, scaling, pass/fail per bullet) in the pull request.
If a check fails, the native renderer stays available as the comparison oracle (`UsePainter(false)`, internal): fix the
handover or peer in `PaperFormView.Editing.cs` / `PaperFormView.Automation.cs`, not the gate.

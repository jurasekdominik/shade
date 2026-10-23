# Working on Shade

Instructions for coding agents. `CLAUDE.md` imports this file; edit only this one.

Shade is a design-system library for Avalonia 12 / .NET 10 desktop apps: `src/Shade` (namespace `Shade`) holds
the tokens (`Look`) and every shared control; `gallery/` shows each control in each state.

- Read `docs/DESIGN_SYSTEM.md` first. Its §13 rules apply: only tokens and shared components, no literal colours in a
  control; a new component goes into `src/Shade/Controls`, the design system and the gallery in one change.
- Nothing app-specific goes here (no app's pages, domain colours or routes); an app keeps those in its own `docs/DESIGN.md`.
- No button or menu action ends in dots; status text for work in progress ("Connecting…") keeps them.
- Both modes, always: a control takes its colours from `Look` when it is built and must look right after
  `Look.SetMode(Mode.Light)` as well as Dark. No `Colors.White` or literal hex for text; text lifted for contrast goes
  toward `Look.Far`.
- `DESIGN.md` is the showcase: its token block and `docs/preview/*` are generated. After a change run
  `dotnet run --project gallery -c Release -- --docs .`, look at the previews you touched in both modes, and commit them.
  Only the prose under the token block is written by hand. A new component goes into `gallery/Showcase.cs` too.
- Build: `dotnet build Shade.slnx -c Release -warnaserror`.
- Before committing: `dotnet format whitespace Shade.slnx`.

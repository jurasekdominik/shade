# Working on Baseline

Instructions for coding agents. `CLAUDE.md` imports this file; edit only this one.

Baseline is a design-system library for Avalonia 12 / .NET 10 desktop apps: `src/Baseline` (namespace `Baseline`) holds
the tokens (`Look`) and every shared control; `gallery/` shows each control in each state.

- Read `docs/DESIGN_SYSTEM.md` first. Its §13 rules apply: only tokens and shared components, no literal colours in a
  control; a new component goes into `src/Baseline/Controls`, the design system and the gallery in one change.
- Nothing app-specific goes here (no app's pages, domain colours or routes); an app keeps those in its own `docs/DESIGN.md`.
- No button or menu action ends in dots; status text for work in progress ("Connecting…") keeps them.
- Build: `dotnet build Baseline.slnx -c Release`. After a change, render the gallery
  (`dotnet run --project gallery -c Release -- --png <folder>`) and look at it.
- Before committing: `dotnet format whitespace Baseline.slnx`.

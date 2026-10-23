# Shade

A dark-first design system for Avalonia 12 desktop apps (.NET 10), with a matching light mode: colour roles, type
scale, the theme on top of Fluent, and the shared controls, so each app stops rebuilding buttons, fields and menus.

**[DESIGN.md](DESIGN.md)** shows the whole package in both modes: 01 Colors, 02 Typography, 03 Components,
04 Spacing · Radii, 05 Responsive, 06 Navigation.

![Components](docs/preview/dark/03-components.png)

## What's in it

| Area | Code |
| --- | --- |
| Tokens and theme: two modes, colour roles, accent rules, spacing, corners, fonts (Inter, Cascadia Code), scroll bars, tooltips, text boxes | `Look` (`Look.SetMode`, `Look.Apply`, `Look.SetAccent`, `Look.Recolor`) |
| Buttons | `Btn` (Primary, Normal, Ghost, Del), `IconButton`, `GearButton`, `Capsule`, `NavigationButtons` |
| Fields and pickers | `Field`, `HostPortField`, `SuggestionField`, `ListField`, `ChoiceButton` (dropdown), `MultiChoice`, `Menu`, `ColorGrid` (colour picker) |
| Toggles and tabs | `Switch`, `Tick`, `TickGrid`, `StateToggle`, `StateLook`, `FilterChip`, `TextTabs` |
| Status and tags | `StatusBadge`, `Tag` |
| Data | `Workbench.Table`, `CompactTable`, `PairTable`, `LogText`, `InlineCode` |
| Layout | `FormLayout` (page header, subpage header, numbered chapter, blocks, `Masonry`, disclosure, sizes), `Kit` (text, cards, icons, motion, empty state) |
| Dialogs | `Dialog` (base window), `Ask.Confirm`, `Ask.ConfirmDelete`, `Ask.Info` |
| App shell | `Sidebar` + `NavEntry` (brand, collapsing rail, Back/Forward), `StatusBar`, `Shell.Layout` |
| Icons | `Icons` (Lucide, drawn with `Kit.Icon24`) |

The rules behind them: [docs/DESIGN_SYSTEM.md](docs/DESIGN_SYSTEM.md).

## Use it in an app

Reference the project (a sibling clone or a git submodule):

```xml
<ProjectReference Include="..\shade\src\Shade\Shade.csproj" />
```

or pack it once (`dotnet pack src/Shade -c Release -o ~/nuget-local`) and add `<PackageReference Include="Shade" Version="1.0.0" />`
with `~/nuget-local` as a package source.

Then:

```csharp
using Shade;
using static Shade.Look;

sealed class App : Application
{
    public override void Initialize()
    {
        Look.SetMode(Look.Mode.Dark);                                   // or Light: before anything is built
        Look.Apply(this);                                               // Fluent + the Shade styles
    }
    public override void OnFrameworkInitializationCompleted()
    {
        Look.AppName = "MyApp";                                       // dialog titles
        Look.SetAccent(Color.Parse("#8C7AE6"));                       // optional: the app's accent
        var nav = new[] { new NavEntry("Home", "home", Icons.Monitor), new NavEntry("Settings", "settings", Icons.Settings) };
        var sidebar = new Sidebar("myapp", "Tagline.", nav); sidebar.Select("home");
        var page = new StackPanel { Margin = new Thickness(16), Children = { FormLayout.Header("Home", "What this page is for.", new Btn("Save", BtnKind.Primary)) } };
        ((IClassicDesktopStyleApplicationLifetime)ApplicationLifetime).MainWindow =
            new Window { Background = B(Bg), Icon = Look.Icon(Accent), Content = Shell.Layout(sidebar, page, new StatusBar()) };
        base.OnFrameworkInitializationCompleted();
    }
}
```

Set the app's default font to `avares://Shade/Assets/Inter#Inter` (`.With(new FontManagerOptions { DefaultFamilyName = ... })`).

## Gallery and docs

```bash
dotnet run --project gallery -c Release                  # the showcase window: hover, click, pick an accent, switch the mode
dotnet run --project gallery -c Release -- --docs .      # checks, then the 12 previews in docs/preview and DESIGN.md's tokens
```

## Limits

- A mode applies to controls built after `Look.SetMode`: to switch while running, an app builds its windows again (the
  gallery does). Live switching would need themed resources (Avalonia `ThemeDictionaries`).
- Controls are built in C#, not XAML; there are no XAML styles or resource keys to override, only the `Look` tokens.
- Checked through the headless gallery render on Linux (contrast in both modes, the suggestion field); run the gallery on
  Windows to check it for real.

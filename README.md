# Shade

A dark-first design system for Avalonia 12 desktop apps (.NET 10), with a matching light mode that switches live: colour
roles, type, spacing and every shared control, so each app stops rebuilding buttons, fields and menus.

**[Open the live page](https://jurasekdominik.github.io/shade/)** (Light / Dark switch at the top right; every component works there, in HTML and CSS) ·
**[DESIGN.md](DESIGN.md)** (tokens and components) · **[DESIGN_SYSTEM.md](docs/DESIGN_SYSTEM.md)** (the rules) · **[ADOPTION.md](docs/ADOPTION.md)** (using it) · **[CHANGELOG.md](CHANGELOG.md)** · **[CONTRIBUTING.md](CONTRIBUTING.md)**

The images below follow your GitHub theme.

## Foundations

### 01 / Colors

Thirteen roles named by their job. Tints, hovers and text on colour follow rules, in both modes.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/preview/dark/01-colors.png">
  <img alt="01 / Colors" src="docs/preview/light/01-colors.png">
</picture>

### 02 / Typography

Inter for the interface, Cascadia Code for values. Five sizes, nothing in between.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/preview/dark/02-typography.png">
  <img alt="02 / Typography" src="docs/preview/light/02-typography.png">
</picture>

### 03 / Spacing · Radii

A 4 px grid, two corner sizes, two heights.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/preview/dark/03-spacing-radii.png">
  <img alt="03 / Spacing · Radii" src="docs/preview/light/03-spacing-radii.png">
</picture>

## Components

### 04 / Actions & inputs

What people click and type into.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/preview/dark/04-actions-inputs.png">
  <img alt="04 / Actions & inputs" src="docs/preview/light/04-actions-inputs.png">
</picture>

### 05 / Data & status

What the app shows back: figures, rows, states and work under way.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/preview/dark/05-data-status.png">
  <img alt="05 / Data & status" src="docs/preview/light/05-data-status.png">
</picture>

### 06 / Overlays & help

What opens over a page, and the help beside a form.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/preview/dark/06-overlays-help.png">
  <img alt="06 / Overlays & help" src="docs/preview/light/06-overlays-help.png">
</picture>

## Patterns

### 07 / Navigation

The sidebar says where you are; tabs switch views of one thing; Back returns.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/preview/dark/07-navigation.png">
  <img alt="07 / Navigation" src="docs/preview/light/07-navigation.png">
</picture>

### 08 / Responsive

Blocks reflow into as many columns as fit; the sidebar collapses to its icons.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/preview/dark/08-responsive.png">
  <img alt="08 / Responsive" src="docs/preview/light/08-responsive.png">
</picture>

## Use it in an app

From nuget.org:

```xml
<PackageReference Include="Shade" Version="0.8.0" />
```

Working on Shade and an app together: reference the project instead (`<ProjectReference Include="..\shade\src\Shade\Shade.csproj" />`).

Then a first screen:

```csharp
using Shade;
using static Shade.Look;

sealed class App : Application
{
    public override void Initialize()
    {
        Look.Apply(this, "MyApp");                                      // Fluent + the Shade styles (dark by default); the name titles dialogs
    }
    public override void OnFrameworkInitializationCompleted()
    {
        Look.SetAccent(Color.Parse("#8C7AE6"));                       // optional: the app's accent
        // Look.SetMode(Look.Mode.Light) switches every open window live, at any time
        var nav = new[] { new NavEntry("Home", "home", Icons.Monitor), new NavEntry("Settings", "settings", Icons.Settings) };
        var sidebar = new Sidebar("myapp", "Tagline.", nav); sidebar.Select("home");
        var page = new StackPanel { Margin = new Thickness(16), Children = { FormLayout.Header("Home", "What this page is for.", new ActionButton("Save", ButtonKind.Primary)) } };
        ((IClassicDesktopStyleApplicationLifetime)ApplicationLifetime).MainWindow =
            new Window { Background = B(Background), Icon = Look.Icon(Accent), Content = Shell.Layout(sidebar, page, new StatusBar()) };
        base.OnFrameworkInitializationCompleted();
    }
}
```

Or in XAML, bound to a view model (`xmlns:shade="using:Shade"`):

```xml
<ToggleSwitch IsChecked="{Binding Enabled}" />
<shade:Field Text="{Binding Name}" />
<ProgressBar Maximum="1" Value="{Binding Share}" />
<shade:SummaryCards Items="{Binding Cards}" Detailed="True" />
<shade:ActionButton Content="Save" Kind="Primary" Command="{Binding Save}" />
```

Configuration, binding, translation, painting with roles and the mark are in [ADOPTION.md](docs/ADOPTION.md).

## Rules for an app's agents

Add a pointer to [the shared app-agent rules](docs/APP_AGENTS.md) in the app's `AGENTS.md` (or import its `AGENTS.md`
from `CLAUDE.md`). Adjust paths to the installed package, Shade checkout or versioned vendored docs:

```markdown
UI uses Shade. Read <shade-docs>/DESIGN.md, <shade-docs>/docs/ADOPTION.md and
<shade-docs>/docs/APP_AGENTS.md before UI work. File Shade issues directly (`gh issue create -R jurasekdominik/shade --label from-app`) instead of patching controls.
Keep app-specific decisions in this app's docs/DESIGN.md. Record the installed Shade version;
refresh vendored guidance when upgrading instead of maintaining a separate copy of the rules.
```

This lightweight check catches common C# violations (adjust the folder; scope startup/domain configuration separately):

```bash
! grep -rnE 'Color\.Parse\("#|\bColors\.[A-Z]|CornerRadius\([0-9]|FontSize *= *[0-9]|new (Button|TextBox|ComboBox|CheckBox|ToggleSwitch|DatePicker|ProgressBar)\b' App/ --include=*.cs | grep -v 'shade-gap'
```

It is a heuristic, not a complete enforcement of the contract: it does not inspect XAML, all literal sizes or composed
substitutes, and a missing input folder must fail the app's CI separately.

## Gallery and docs

```bash
dotnet run --project gallery -c Release                  # the showcase window: hover, click, pick an accent, switch the mode live
dotnet run --project tests/Shade.Tests -c Release        # colour rules, parsers, and the controls in XAML with bindings
```

Checks, `--docs` regeneration and contributing: [CONTRIBUTING.md](CONTRIBUTING.md). Open gaps are GitHub issues.

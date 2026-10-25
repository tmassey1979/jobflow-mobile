# JobFlow Mobile — Visual Identity (Signal Mark)

**Status:** Direction B approved; adaptive icon and splash confirmed path.  
**Issue:** [#31](https://github.com/tmassey1979/jobflow-mobile/issues/31)  
**Branch:** `work/visual-identity-signal-mark`

## Direction

**Signal Mark** — precise, technical, confident.

An abstract dual-chevron mark suggesting a verified signal / opportunity without a literal checkmark cliché. Symbol-first for adaptive icons; geometric sans wordmark for splash and marketing.

## Palette

| Role | Hex | Usage |
|------|-----|--------|
| Charcoal | `#1A1A1A` | Adaptive icon background, splash field, primary text |
| Electric blue | `#2563EB` | Symbol, accent actions |
| Warm off-white | `#FAFAF9` | Light surfaces (optional) |
| Cool gray | `#64748B` | Secondary / muted text (aligns with existing UI) |

Existing app resources (`Accent`, `Ink`, `MutedInk` in `App.xaml`) can be aligned to this palette in a follow-up UI pass; this issue does not change application logic or shared UI entry points.

## Shape language

- Angular, geometric strokes with clean miters
- High contrast for small-size legibility
- Symbol works in monochrome (blue-on-dark or blue-on-light)

## Logo variants

1. **Symbol only** — `docs/design/signal-mark-symbol.svg` and adaptive foreground
2. **Wordmark** — `docs/design/signal-mark-wordmark.svg` (symbol + “JobFlow”)

## Android adaptive icon

```xml
<MauiIcon
  Include="Resources\AppIcon\appicon.svg"
  ForegroundFile="Resources\AppIcon\appiconfg.svg"
  ForegroundScale="0.65"
  Color="#1A1A1A" />
```

- **Background:** solid charcoal `#1A1A1A`
- **Foreground:** Signal Mark in `#2563EB` on transparent
- **ForegroundScale:** `0.65` keeps geometry inside Android adaptive safe zones

## Splash screen

```xml
<MauiSplashScreen
  Include="Resources\Splash\splash.svg"
  Color="#1A1A1A"
  BaseSize="128,128" />
```

- Field color: charcoal `#1A1A1A`
- Centered Signal Mark in electric blue
- No wordmark on splash (symbol-only for clean launch)

## Constraints honored

- Native Android-only; no remote image dependencies
- Original bundled assets only
- No edits to `MauiProgram.cs`, `App.xaml`, `MainPage.xaml`, or application logic in this change set (csproj only adds `MauiIcon` / `MauiSplashScreen`)

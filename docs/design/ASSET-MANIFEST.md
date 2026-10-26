# Asset Manifest — Signal Mark

| File | Use | Dimensions / viewBox | Format | Status |
|------|-----|----------------------|--------|--------|
| `Resources/AppIcon/appicon.svg` | MAUI adaptive-icon background | 108×108 | SVG | Wired in `JobFlow.Mobile.csproj` |
| `Resources/AppIcon/appiconfg.svg` | Android launcher icon foreground | 108×108 | SVG | Wired in `JobFlow.Mobile.csproj` |
| `Resources/Splash/splash.svg` | Startup splash mark | 456×456 | SVG | Wired in `JobFlow.Mobile.csproj` |
| `Resources/Images/signal_mark.svg` | In-app brand mark | 108×108 | SVG | Shown on all three tabs |
| `Resources/Images/empty_jobs.svg` | Empty Jobs / Applications illustration | 240×160 | SVG | Wired to both empty states |
| `docs/design/signal-mark-symbol.svg` | Standalone editable symbol reference | 128×128 | SVG | Design source |
| `docs/design/signal-mark-wordmark.svg` | Editable wordmark reference | 420×96 | SVG | Design source |
| `App.xaml` color resources | Ink / MutedInk / Accent palette | — | XAML | Used across all screens |

## Asset pipeline

MAUI converts the source SVGs into platform resources at build time. Runtime XAML references use the generated resource names `signal_mark.png` and `empty_jobs.png`. The launcher icon uses the charcoal base plus the blue dual-chevron foreground; the splash screen uses the same mark on a charcoal field. The same mark now appears in the Jobs, Applications, and Integrations headers.

## Release pipeline

Every successful push build on `main` computes the next patch version, increments Android's integer version code, and publishes a GitHub Release with a single versioned, signed APK. Stable signing uses repository Actions secrets; the keystore itself is never stored in Git.

## Optional future graphics

- Android 13+ monochrome themed icon layer
- Onboarding illustrations

## License

These assets are original JobFlow Mobile artwork; no third-party stock or remote assets are used.

# Grok Instructions — JobFlow Mobile Graphics and Visual Assets

Read this file first when assigned graphics or visual-design work. Also read `CLAUDE.md`, `PROJECT-ROADMAP.md`, `README.md`, and `docs/DATA-CONTRACT.md` for the product context. The roadmap remains the canonical implementation order; this file defines Grok's specialist lane.

## Primary responsibility

**Grok's main job is visual asset creation for JobFlow Mobile**: generate, refine, and deliver a coherent set of polished graphics, especially:

- App brand/logo and wordmark concepts.
- Android launcher icon artwork, including adaptive-icon foreground artwork and background guidance.
- Splash-screen artwork.
- Small supporting illustrations, empty-state graphics, and onboarding visuals when they improve the native Android experience.
- A lightweight visual identity guide covering palette, typography direction, shape language, and usage rules.

Prioritize usable, consistent assets over one-off concept art. Keep the product professional, approachable, trustworthy, and suitable for a job-search/productivity app. Avoid visual clichés where possible (generic briefcases, handshakes, or stock-photo people) unless a concept makes them distinctive and purposeful.

## Deliverables and asset quality

1. Start by proposing a compact visual direction and a small set of concepts; do not generate a huge asset pack before the direction is agreed.
2. When image-generation tools are available, use them to create original artwork. For logos and icons, favor simple, recognizable silhouettes that remain legible at small sizes.
3. Deliver final assets as real files in the repository, not just descriptions or screenshots. Prefer transparent PNG or SVG where appropriate; use raster assets for formats that require them. Do not claim an SVG is editable/vector if the delivered file is only a raster image.
4. Keep artwork free of accidental text, fake UI labels, watermarks, mockup frames, and unnecessary backgrounds. Logo wordmarks with text should also have a symbol-only variant.
5. For Android launcher assets, consider adaptive-icon safe zones, foreground/background separation, high-contrast light/dark contexts, and legibility at small sizes. Follow the project's actual MAUI asset conventions after inspecting the current project; do not assume paths or dimensions without checking.
6. Include an asset manifest listing each file, intended use, source/editability, dimensions, and any generation or conversion steps. Include attribution or license notes for any non-original elements.
7. Check assets at actual target sizes and on both light and dark surfaces where relevant. Avoid excessive detail, thin lines, tiny text, and color combinations with poor contrast.
8. Do not overwrite an existing asset until its current use and impact have been checked. Preserve alternate concepts in a clearly named design-preview location if they are useful, but do not ship unused drafts in the production asset directory.

## Visual design boundaries

- The application is a **native Android-only C#/.NET MAUI app**, not a website. Design for Android navigation, touch targets, responsive phone layouts, accessibility, and platform conventions.
- Use the current app screens and roadmap as context. Do not invent screens or imply that an integration/feature already exists.
- Graphics support the product; do not change application logic, authentication, data models, build configuration, or shared UI entry points as part of a visual task unless the user explicitly assigns that work.
- Do not edit `MauiProgram.cs`, `App.xaml`, `MainPage.xaml`, or `MainPage.xaml.cs` while another agent owns them. Coordinate before changing shared files.
- Do not add remote image dependencies or tracking. Prefer bundled local assets.
- Do not put secrets, personal information, real resumes, or real job-application content into prompts, mockups, or assets.

## Collaboration workflow

1. Inspect the current repository state, recent commits, open issues, and existing assets before starting.
2. Claim or identify one graphics-related issue and state which asset files you intend to own. If no suitable issue exists, create one before substantial work.
3. Coordinate with the coding agent(s). Keep graphics work in assets/design documentation where possible so it can proceed without conflicting with app-code changes.
4. Put proposed concepts/previews in a clearly labeled review location and get user approval before replacing the chosen production identity or making a broad visual rebrand.
5. After approval, integrate the selected assets using the existing MAUI asset conventions and update the asset manifest/documentation.
6. Run any available asset validation and relevant Android build checks. Report the exact files, dimensions, commands, and results; never claim a check ran if it did not.
7. Summarize what was generated, what remains a draft, and any decision needed from the user. Do not mark the issue complete until the agreed assets are present and verified.

## Definition of done

A graphics task is complete when the approved assets are committed in the correct locations, their intended use and dimensions are documented, they remain legible and accessible at target sizes, no existing work was overwritten unexpectedly, and the matching GitHub issue records the files and validation results.

**Grok's default lane is graphics generation and visual assets, especially the logo and app icons.** Claude and other coding agents remain responsible for implementation unless work is explicitly coordinated otherwise.

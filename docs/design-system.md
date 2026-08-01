# Nuotti design system

Neon cyan single-accent UI for customer-facing apps (Audience, Performer, Projector, web).
Structure follows dense data-UI practice (tables first, sharp chrome, contrast floors) without borrowing other brand palettes.

## Tokens

| Role | Dark | Light |
|------|------|-------|
| Primary | `#00FFF5` | `#007A74` |
| OnPrimary | `#001413` | `#FFFFFF` |
| Background | `#05090B` | `#F0F7F8` |
| Surface | `#091115` | `#FFFFFF` |
| Text primary | `#ECFFFF` | `#0A1518` |
| Text secondary | `#91A5AA` | `#4A6068` |
| Divider | `#17363A` | `#C5D4D8` |
| Success / Warning / Error | `#5EC99D` / `#FFA040` / `#FF6B93` | `#1F7A5C` / `#B35C00` / `#C41E4A` |
| Border radius | `0` | `0` |

Source of truth: `Nuotti.Contracts/V1/Design/DesignTokens.cs`. Projector venue display uses `ProjectorVariantBPalette` (same as dark).

## Rules

1. **Sharp chrome** — buttons, chips, inputs, papers, icon buttons use `0` radius. Circular only for true circles (avatars, spinners).
2. **Single accent** — cyan is the interactive color; do not introduce a second brand accent.
3. **Contrast** — body text and primary button ink meet WCAG AA (`ContrastAuditTests`). Prefer `OnPrimary` on filled primary, never assume white on neon.
4. **Dense tables** — library and setlist use Mud `Dense` + striped; title/artist as primary columns; actions in a trailing command cell.
5. **Theme** — default dark neon stage; light is a cool wash with deepened cyan for legibility on white.

## Components (MudBlazor)

- Global overrides in Performer `wwwroot/css/site.css` and Audience `wwwroot/css/app.css` force chip and icon-button sharpness (Mud hardcodes pill radii).
- ThemeService sets `LayoutProperties.DefaultBorderRadius` from `DesignTokens.BorderRadius` and `PrimaryContrastText` from `OnPrimary`.

## Song preparation flow

1. **Song Library** — create and edit catalog entries (title/artist).
2. **Setlist Manager** — order songs for a show (legacy local manifest until modern snapshot UI).
3. **Song Package readiness** — author playback/hints/lyrics, dry-run, publish.

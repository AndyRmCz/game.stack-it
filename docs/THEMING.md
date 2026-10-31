# Theming: Pop and Neon

Milli has two looks, **Pop** (light pastel) and **Neon** (dark glow), switchable from
Settings ▸ Look and from the pause menu. Both share the same layout, shapes, type and motion;
only colour, glow and lighting change. First launch uses Neon.

## One source of truth

`shared/theme/themes.json` holds every token for every look. It is read by:

| Consumer | How |
|---|---|
| Unity | `Resources/StackIt/themes.json`, a byte-for-byte copy made by `ThemesSync` (editor load, Setup Project, every build). `Look.Catalog` parses it with `MiniJson` into `ThemePalette`s. |
| Web pages (`/c/{code}`, `/privacy`, `/terms`) | `backend/app/web/themes.json`, a copy (the Docker build context is `backend/`). `app/web/theme.py` turns tokens into CSS custom properties: Neon on `:root`, Pop inside `@media (prefers-color-scheme: light)`. |
| Link previews and app icons | `app/web/og.py` (`/og/{code}.png`, Neon) and `backend/scripts/generate_app_icons.py` (both looks). |

Tests fail if either copy drifts (`ThemeTokenTests.TheUnityCopyMatchesTheSharedFile`,
`test_backend_copy_of_the_tokens_matches_the_shared_file`).

### Value formats

- Colour: `"#RRGGBB"` or `"#RRGGBBAA"`. A fully transparent colour means "none".
- List of colours: `bg.gradient` (top, middle, bottom), `block.base` / `block.moving` (top, left, right faces), `block.ramp` (6 stops).
- Number: widths, `bg.gradientMid`, post-FX values, `accent.glowPulse` (0/1), `block.emission`.
- Shadow/glow: `{ "color", "y", "blur" }`; radial glows use `{ "color", "radius" }` (fraction of the screen width).
- In CSS: `accent.glow` → `--accent-glow-color`, `--accent-glow-y` (px), `--accent-glow-blur` (px); lists → `--bg-gradient-1..3`.

## Tokens

| Token | Pop | Neon | Used for |
|---|---|---|---|
| `bg.gradient`, `bg.gradientMid` | `#C7C2E4` → `#DCCBE0` → `#EFD6C9` at 53% | `#0B1432` → `#081E2C` → `#06272B` | Every background, the 3D sky behind Home and the game |
| `bg.centerGlow` | none | `#2CF0D6` 20%, radius 52% | Radial glow behind the hero |
| `text.primary` / `text.secondary` | `#1B2440` / `#3A4466` | `#F2FFFD` / `#8FEFE3` | Text, overlines, captions |
| `text.logoGlow` | none | `#2CF0D6` 65%, blur 18 | Logo and big score (TMP underlay) |
| `surface`, `surface.border(+Width)`, `surface.shadow` | white, no border, soft shadow | white 8%, 1 px `#8FEFE3` 30% border, no shadow | Cards, icon buttons, coin pill |
| `surface.raised` | white | `#12303A` | Modal cards and bottom sheets over the game (opaque) |
| `surface.nav`, `surface.navBorder` | white 85% | white 6% + white 10% border | Bottom navigation |
| `surface.sunken` | ink 6% | white 6% | Tracks, inputs, locked prices |
| `surface.highlight(+Border)` | accent 15% | accent border | The player's own leaderboard row |
| `accent`, `accent.onAccent`, `accent.edge`, `accent.glow` | `#108274`, white, `#0B584F`, `#19C9B4` 35% y16 b32 | `#188274`, white, `#10584F`, `#2CF0D6` 50% b36 | The one primary button per screen, accent chips, toggles |
| `accent.glowPulse` | 0 | 1 | Neon's glow breathes with the idle call to action |
| `secondary.*` | outline `#0E8C7E` 2 px, text `#0B6B61`, white 70% | outline `#2CF0D6` 1.5 px, text `#9FF7EC`, teal 8% | Secondary pill buttons |
| `accent2`, `accent2.onAccent2`, `accent2.glow` | `#FF6B5B` | `#FF4FA3` with glow | Notification dot, rival marker, "You passed", "New record" |
| `dot.ring` | white | none | 2 px ring of the notification dot |
| `coin.fill` / `coin.ring` | `#F7B928` / `#C98A06` | `#FFC93C` / `#C98A06` | Coins (drawn, not an icon) |
| `success` / `danger` / `danger.onDanger` | `#14966F` / `#D34242` / white | `#3DF5A0` / `#FF6886` / `#1B0610` | Won duels, destructive actions |
| `overlay.dim` | ink 40% | black 55% | Behind pause, dialogs, sheets |
| `toast.fill` / `toast.text` | ink / white | `#12303A` / `#F2FFFD` | Toasts |
| `medal.gold/silver/bronze`, `medal.ink` | | | Top-3 leaderboard chips |
| `arcade.goldTop` / `arcade.goldBottom`, `arcade.outline`, `arcade.shadow` | `#FFF3A6` → `#FFB321`, `#4A1600`, `#1F0800` 80% | same | Near-miss callout ("0.4 mm from perfect"): gold gradient letters, dark outline, hard drop shadow, no background |
| `block.base`, `block.moving`, `block.ramp`, `block.groundShadow` | pink base, mint moving, pastel ramp | teal base, magenta moving, cyan→coral ramp | Gameplay blocks (exact face colours) |
| `block.emission` | 0 | 0.35 | Emissive blocks for bloom |
| `postFx.bloom*`, `postFx.chromaticPulse` | bloom off, 0.15 | bloom 1.2 / 0.9 / 0.7, 0.35 | URP volume |

The tower climbs `block.ramp`: a stop every 10 blocks, interpolated, looping; side faces are the
top colour × 0.85 (left) and × 0.70 (right). The moving block always uses `block.moving`.

## Rules

- **No raw colours in screens.** UI code binds graphics to token *names* (constants in
  `Client/Theming/T.cs`) through themed components: `ThemedGraphic`, `ThemedSurface`,
  `ThemedButton`, `ThemedGradientBackground`, `ThemedIsoTower`, `TextEffect`. Previews of a
  specific look or skin, and invisible hit areas, are marked `IgnoreTheming`.
- **One primary (accent) button per screen**: `UiKit.Primary`. Everything else is a
  `UiKit.Pill`, an `UiKit.IconButton` or a plain row.
- Layout is in points on the 390 × 844 reference (`Ui` constants); fonts by role (`TextRole`).
- `ScriptsUseTokenConstantsNotStringLiterals` fails on token names written as string literals;
  `NoHardcodedColorsTests` (Unity) builds every screen and fails on any graphic without a binding.

## Adding a token

1. Add it to **both** looks in `shared/theme/themes.json`.
2. Add a constant to `T.cs` and bind to it from a themed component.
3. If it is text on something, add the pair to `ThemeContrastTests`.
4. The Unity copy syncs when the editor loads; copy the file to `backend/app/web/themes.json`
   (the tests tell you if you forget).

## Adding a third look

1. Add `"yourlook": { "dark": …, "tokens": { … } }` with every token (the completeness test
   compares token sets across looks).
2. Add the id to `ThemeId` and `look.yourlook` to `strings.csv`. Settings and the pause menu list
   every look in the catalog, so nothing else changes. The web keeps Neon / Pop by colour scheme.

## Fonts and icons

Fredoka 700/600 and Nunito 700/800 (SIL OFL), Material Symbols Rounded outline and filled
(Apache 2.0), subset with fonttools. To add an icon: add its codepoint to `UI/Fonts.cs`
(`Icons`), then re-subset:

```sh
pip install fonttools
# instance + subset (see the icon names in UI/Fonts.cs)
fonttools varLib.instancer MaterialSymbolsRounded[FILL,GRAD,opsz,wght].ttf FILL=0 wght=400 GRAD=0 opsz=24 -o outline.ttf
pyftsubset outline.ttf --unicodes=U+E037,U+E8F6,... --output-file=MaterialSymbolsRounded-Outline.ttf
```

The TMP atlases in `Resources/StackIt/Fonts` are generated (git-ignored) and rebuild by
themselves when the TTFs or the character set change.

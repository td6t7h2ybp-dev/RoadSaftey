# RoadSafety: UI style guide (US 5.2)

The single reference for every screen of the game (HUD, settings, badges, badge popup, end screen,
"try again" screen). It follows the look of the street and the cars made in Epic 2: soft, rounded,
sunny. **In code, every colour comes from `UIKit` (`Assets/Scripts/UI/UIKit.cs`)**: change it there
and every screen follows.

## 1. Colours

| Role | Name in code | Hex | Used for |
|---|---|---|---|
| Bleu ciel | `UIKit.SkyBlue` | `#1EB6FF` | Steering arrows, settings (gear) button |
| Vert pomme | `UIKit.AppleGreen` | `#58D63C` | Go pedal, badges button, "well done" accents |
| Jaune soleil | `UIKit.SunYellow` | `#FFD400` | Horn, stars, highlights |
| Stop red | `UIKit.StopRed` | `#FF485E` | Stop/brake pedal only (the colour children know from the street) |
| Warm orange | `UIKit.WarmOrange` | `#FF8C1A` | Warnings (danger icon, amber light) |
| Ink | `UIKit.Ink` | `#1E1B2E` | Outlines of every button and icon |
| Paper | `UIKit.Paper` | `#FFFCF5` | Icons on the buttons, speech bubble |
| Cream panel | `UIKit.LimeWhite` | `#FAF7EE` | Panels (counters, menus, end screen) |
| Navy text | `UIKit.Navy` | `#16325C` | All body text |
| Green text | `UIKit.SoftGreen` | `#46962F` | "What you did well" headings (darker apple green so it reads on cream) |
| Orange text | `UIKit.SoftOrange` | `#D66E28` | "What to watch" headings |
| Dim | `UIKit.Dim` | navy 55 % | Background behind a modal window |

Rules:
- The three main colours (sky blue, apple green, sun yellow) cover all the normal buttons.
- Red is kept for **Stop and mistakes only**, never for decoration, so it always means "stop".
- Text is never pure black and never placed straight on the 3D street: always on a cream panel or the bubble.
- Text colour on cream must stay dark (navy or the "text" variants), at least 4.5:1 contrast.

## 2. Typography

| Use | Font | Size (1920x1080 reference) |
|---|---|---|
| All texts | **Baloo Bhaijaan RS Bold** (`Resources/Fonts`, rounded Arabic, OFL) | |
| Big titles (end screen, "try again") | | 84 to 96 |
| Counters (stars) | | 66 |
| Messages, speech bubble, buttons' labels | | 44 to 56 |
| Lists (end screen) | | 42 to 46 |

Rules:
- Modern Standard Arabic only, right to left, always passed through `ArabicText.Fix(...)`.
- Short lines (2 lines max in the bubble), with `\n` at a natural pause.
- Never less than 40 px: the players are 5 to 9 and many cannot read yet; every instruction is **also spoken** (US 5.1).

## 3. Shapes

- Buttons are **round** "stickers": a domed face, a thick ink outline, a solid lip under it (no gradient, no glow). Drawn in code by `HudRestyle`.
- Panels, the speech bubble and the windows use the sliced `ui_panel` sprite: **rounded corners** everywhere, no sharp angles.
- The only exception is the **Stop** button: a red pedal like Go, with two thick white bars ("=").
- Touch targets are at least 140 px (settings, badges) and 205 to 290 px for driving buttons (Go, Stop, arrows, horn, seatbelt).
- A window (settings, badges, end screen) dims the street behind it (`UIKit.Dim`) and pauses the game.

## 4. Layout (landscape tablet, Canvas Scale With Screen Size 1920x1080, match 0.5)

| Zone | Content |
|---|---|
| Bottom left | Steering arrows (left thumb) |
| Bottom right | Go (big) above Stop (right thumb) |
| Bottom centre | Horn |
| Top left | Settings, badges, level |
| Top centre | Stars counter, 3 hearts |
| Top right | Avatar, seatbelt button, danger icon, speech bubble under them |

## 5. Iconography

White ("paper") glyph, thick ink outline, on a palette-coloured round button. One idea per icon, no text on icons.

| Icon | Meaning | Colour |
|---|---|---|
| Arrows | Turn left / right | Sky blue |
| Pedal with arrows | Go (hold) | Apple green |
| Pedal with two bars | Stop (brake) | Stop red |
| Trumpet horn | Horn ("tuut-tuut", never a penalty) | Sun yellow |
| Seatbelt across a child | Put the seatbelt on | Avatar changes with / without belt |
| Gear | Settings (music / sound effects) | Sky blue |
| Star | Badges | Apple green |
| Star (counter) | Stars won | Sun yellow |
| Heart | Lives (3) | Red |
| Triangle with "!" | Danger ahead | Warm orange |

How readable each icon is for 5 to 9 year olds is checked with the protocol in
[`Icon_Readability_Test.md`](Icon_Readability_Test.md).

## 6. Sounds and voice (link with US 5.1 and US 4.1)

- Every instruction and every feedback in the bubble has a voice line (`GameMessages.Lines`, files in `Audio/Resources/Audio/VO`), a **child's voice** in Modern Standard Arabic.
- Gaining a star: the star pops, flies into the counter, with a short "joyful" sound.
- Mistakes: a calm, kind voice line that explains what to do.

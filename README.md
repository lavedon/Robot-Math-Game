# Beep's Number Line Dash

A Super Nintendo–style game that teaches kids to **subtract on a number line**. Beep, a little desk robot with a
screen face and big headphones, solves subtraction problems by hopping backwards along the line, and the player drives
both ends of the hop with an Xbox controller. Answers earn Bolts and XP. Bolts buy RPG upgrades, gadgets and outfits
in Gizmo's Garage, and the World Map is Gear Island, drawn in Mode 7.

The vocabulary is in [CONTEXT.md](CONTEXT.md).

## Running it

Run `.\run.ps1` to build and play, `.\run.ps1 -Editor` to open the Godot editor, or `.\run.ps1 -Test` to run the
rules tests. The script uses the Godot 4.7.2 **.NET** build in `godotPalaceRoomViewer\.tools`; pass `-GodotDir` to
use another copy.

## How a problem works

Every problem, such as **52 − 17 = ?**, is solved in two moves on the number line:

1. **Find the start.** Slide the green **Start flag** to 52 and lock it in. On the big 0–100 lines, the bumpers jump
   by 10, so 52 is five tens and two ones. Locking on the wrong number doesn't count as a mistake: Beep says which way
   to go.
2. **Hop back.** Each press of ◄ adds a hop back of 1, and each press of LB adds a hop of 10. The arcs build up on the
   line (cyan for ones, gold for tens, with "−10" and "−7" labels), and the orange **Land flag** follows the end of the
   chain. When the hops add up to 17, press **A (GO!)**. Beep hops the whole chain and calls out each number he lands
   on: 42… 41, 40, 39… 35.

The Land flag is the answer. A wrong answer shows the right chain in green, with the answer in a bubble, and the
problem comes back once at the end of the Stage.

On the two-digit lines the camera follows the flags, a mini-map shows where you are on 0–100, and the view zooms out
to keep long hop chains on screen.

## Controls

| | Xbox controller | Keyboard |
|---|---|---|
| Slide the Start flag 1 / hop back 1 | D-pad or left stick ◄ ► | ← → (or A D) |
| Slide the Start flag 10 / hop back 10 | LB / RB (or the triggers) | Q / E |
| Lock the Start flag / GO! | A | Space, Enter or Z |
| Take back hops | ► (ones), RB (tens), B (last hop, then unlock the Start flag) | → / E / Backspace |
| Hint (with the Hint Lamp) | Y | H |
| Pause | Start or View | Enter or Tab |
| Walk the World Map | D-pad ◄ ► | ← → |
| Go into the Garage from anywhere on the map | Y or X | H or C |
| Shop: move / change shelf / buy or wear / leave | ▲▼ / LB RB / A / B | ↑↓ / Q E / Space / Backspace |
| Sound on/off · full screen · CRT scanlines | | M · F11 · F2 |

Holding a direction repeats it, faster the longer it's held. Turbo Treads make it quicker still.

## The Stages

| # | Stage | Skill | Line |
|---|---|---|---|
| 1 | Hop Hop | take away 1 or 2 | 0–10 |
| 2 | Ten Town | take away inside 10 | 0–10 |
| 3 | Teen Trail | teens, staying above 10 (17 − 4) | 0–20 |
| 4 | Ten Bridge | teens, hopping past 10 (13 − 5) | 0–20 |
| 5 | Big Leaps | whole tens (64 − 30) | 0–100 |
| 6 | Gear Grove | two-digit − one-digit, same ten (47 − 3) | 0–100 |
| 7 | Spring Falls | two-digit − one-digit, past a ten (42 − 7) | 0–100 |
| 8 | Circuit Cove | two-digit − two-digit (58 − 23) | 0–100 |
| 9 | Volt Peak | two-digit − two-digit, past a ten (52 − 17) | 0–100 |
| 10 | Glitch Core | **boss**: a mix of everything; each right answer zaps the Glitch | 0–100 |

Each Stage gives up to three **Stars**: one for clearing it, one for **no slips**, and one for **beating the clock**,
which ticks only while you solve. Clearing a Stage opens the next one on the map. Gizmo's Garage never blocks the path.

## Rewards and Gizmo's Garage

Each answer right the first time pays 2 Bolts. An answer within 8 seconds earns a Speedy bonus, and the Stage clear,
no slips and beat-the-clock Stars pay 5 Bolts each. The boss pays 20 more. XP raises Beep's level and title, from
Rookie Bot to Legend Bot, and some items wait for a higher level.

| Shelf | Items |
|---|---|
| **Parts** (levels, like RPG stats) | Turbo Treads (SPD: faster flags and hops), Bolt Magnet (LUCK: +10% Bolts per level), Clock Core (TIME: +5 s on the clock per level), Combo Coil (COMBO: bonus Bolts after 3 in a row) |
| **Gadgets** | Number Specs (labels every number on the 0–100 lines), Hint Lamp (Y shows the neat tens-then-ones hops), Oops Shield (forgives your first slip in a Stage) |
| **Style** | headphones (Cherry, Lime, Gold, Rainbow), eyes (Round, Heart, Star, Happy), hats (Propeller, Party, Wizard, Crown), hop trails (Sparkle, Bubble, Rainbow) |

The cursor tries a Style item on Beep before you buy it, and Beep's stat bars show the green pip a Part would add.

## The Super Nintendo look

- **A 256×224 canvas**, the SNES resolution, scaled up by a whole number with no smoothing. Optional CRT scanlines
  (F2).
- **Mode 7** (`src/Game/Snes/Mode7.cs`): a perspective plane drawn row by row in a shader, with a sky panorama that
  turns with the camera, depth fog and a sea that sways. It draws Gear Island on the map, title and results screens
  (sprites are projected onto it and scaled by depth) and the scrolling circuit floor under the number line.
- **The mosaic transition** from Kegel Quest: between screens the picture breaks into growing blocks and darkens, then
  resolves on the new screen. Leaving the Garage mosaics out, then the Mode 7 camera spins down out of the sky onto
  Beep.
- **Wavy text** for titles and cheers, like HDMA line effects. Blue-gradient RPG windows. Kegel Quest's pixel font.
- **Chiptune audio**, synthesized at start-up by the engine-free synth ported from Kegel Quest: 21 effects and 5
  original loops (title, map, play, shop and boss).

## Layout

```
src/Core/            plain .NET: Stages and the problem dealer, the HopLine (number-line model), StageRun scoring,
                     Progress (XP, Bolts, the shop catalog, outfits, JSON save), Gizmo's lines, the pixel font
src/Core/Audio/      the synth, the score notation, sound effects and music (no Godot)
src/Game/Main.cs     the 256×224 viewport, scaling, mosaic + scanline post pass, screen swaps, save, screenshot mode
src/Game/Snes/       Mode 7, the pad (controller + keyboard, auto-repeat), drawing helpers, text-grid sprites, Beep
src/Game/Screens/    Title, Map (Mode 7 island), Play (the number line), Results, Shop (Gizmo's Garage)
tests/               xunit tests for the rules and the audio
```

All the art is written as text grids in `src/Game/Snes/Sprites.cs`, one character per pixel. Beep is drawn in layers
so his headphones can be any colour. The island, sea, skies and circuit floor are painted by code in `WorldArt.cs`.

The save is `user://save.json`, which on Windows is `%APPDATA%\Godot\app_userdata\Beep's Number Line Dash\save.json`.

## Checking it without playing

`.\run.ps1 -Shot <screen>` builds the game, renders one screen with a sample save (it never touches the real save)
and writes `shot-<screen>.png`. `play:auto:<stage>` lets a bot play a whole Stage through the real controller input
path. It gets the first problem wrong on purpose, so the retry runs too, and it prints an `ANSWER` line for each
problem and a `SCREEN` line for each screen change. Then it shows the results screen.

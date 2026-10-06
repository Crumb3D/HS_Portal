# HS Portal

Survival-styled portal gun for **7 Days to Die 3.2**. Place a blue portal and an orange portal, then walk through one to emerge from the other with facing and velocity transformed.

The held gun is an original industrial 7DTD weapon (claws, blue/orange coils, DANGER pack) built from `portal Gun Design.png`. It is **not** a Portal 2 mesh. **Version 0.2.0.**

License: [MIT](LICENSE). Author: Crumb.

## Requirements

- 7 Days to Die **3.2** (PC).
- **Harmony** — `0_TFP_Harmony`, ships with the game.
- **Multiplayer:** the same `HS_Portal` folder on the **dedicated server and every client**.

## Installation

1. Unzip so you have `Mods/HS_Portal/` (game install or `%AppData%/7DaysToDie/Mods/`).
2. `ModInfo.xml` and `HSPortal.dll` must sit directly in that folder.
3. Restart the game (and the dedicated server if you use one).

## Crafting

Same gating as HS Doors / HS Lift / HS Escalator:

1. Read **Wiring** magazines until **Electrician** reaches **25** (or spend the skill the magazines feed).
2. Put perk points in **Advanced Engineering**.
3. Craft **Portal Gun** at a **workbench**.

Ingredients: forged steel, electric parts, mechanical parts, a motion sensor, pistol parts, scrap polymers.

The **Portal Handbook** (one paper) is always craftable and is not consumed when read.

Creative menu still has the gun. Admin `hsportal give` bypasses the unlock.

## Use

Left click blue, right click orange. Firing plays the gun animation; a reject shake plays if the surface is illegal.

One portal is a static coloured opening. Walking into it does nothing until the other colour is placed. Then the pair is linked both ways.

Valid surfaces for this milestone: full non-terrain cubes (walls, floors, ceilings). Terrain, plants, wedges, doors, and plates reject.

Admin console (`hsportal`):

- `hsportal give` — portal gun
- `hsportal room` — concrete test chamber around you, then gives the gun
- `hsportal blue` / `hsportal orange` — place that colour on the aimed surface
- `hsportal clear` — remove your portals
- `hsportal status`
- `hsportal debug`

## Rebuild assets

Unity **2022.3.62f2** and official Blender (not Microsoft Store):

```
powershell -File build_assets.ps1
```

Then rebuild `HSPortal.dll` from `Source/HSPortal.csproj`.

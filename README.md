# HS Portal

Survival-styled portal gun for **7 Days to Die 3.2**. Place a blue portal and an orange portal, then walk through one to emerge from the other with facing and velocity transformed.

This is an early prototype. The held gun is still the vanilla pistol mesh. See-through portal rendering, the custom gun model, firing animation, and gels are not in yet.

License: [MIT](LICENSE). Author: Crumb.

## Requirements

- 7 Days to Die **3.2** (PC).
- **Harmony** — `0_TFP_Harmony`, ships with the game.
- **Multiplayer:** the same `HS_Portal` folder on the **dedicated server and every client**.

## Installation

1. Unzip so you have `Mods/HS_Portal/` (game install or `%AppData%/7DaysToDie/Mods/`).
2. `ModInfo.xml` and `HSPortal.dll` must sit directly in that folder.
3. Restart the game (and the dedicated server if you use one).

## Use

Creative menu: **Portal Gun**. Left click blue, right click orange.

One portal is a static coloured opening. Walking into it does nothing until the other colour is placed. Then the pair is linked both ways.

Valid surfaces for this milestone: full non-terrain cubes (walls, floors, ceilings). Terrain, plants, wedges, doors, and plates reject.

Admin console (`hsportal`):

- `hsportal give` — portal gun
- `hsportal room` — concrete test chamber around you, then gives the gun
- `hsportal blue` / `hsportal orange` — place that colour on the aimed surface
- `hsportal clear` — remove your portals
- `hsportal status`
- `hsportal debug`

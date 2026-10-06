# HS Portal

Survival-styled portal gun, gel, weighted cube, and long-fall boots for **7 Days to Die 3.2**. Portal 2 is a behaviour reference only — no Valve meshes.

The held gun is an original industrial 7DTD weapon (claws, blue/orange coils, DANGER pack) built from `portal Gun Design.png`. **Version 0.3.0.**

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
3. Craft at a **workbench**: Portal Gun, Gel Gun, Weighted Cube, Long-Fall Boots.

The **Portal Handbook** (one paper) is always craftable and is not consumed when read.

Creative menu still has everything. Admin `hsportal give` dumps the whole kit.

## Use

**Portal gun** — left click blue, right click orange. Firing plays the gun animation; a reject shake plays if the surface is illegal. One portal is a static coloured opening. A linked pair teleports both ways and each opening shows the view through the other.

**Gel gun** — left click blue repulsion gel: jump or land on it to bounce; bounce height follows how hard you came in. Right click orange propulsion gel: walking or running on it speeds you up.

**Long-fall boots** — feet slot. Ignore fall damage. Wear them before you bounce off high blue gel.

**Weighted cube** — placeable scrap-steel crate with a heart stencil. E to pick up.

Valid surfaces: full non-terrain cubes (walls, floors, ceilings). Terrain, plants, wedges, doors, and plates reject.

Admin console (`hsportal`):

- `hsportal give` — portal gun, gel gun, cube, boots
- `hsportal room` — concrete test chamber around you, then gives the kit
- `hsportal blue` / `hsportal orange` — place that portal colour on the aimed surface
- `hsportal gel blue` / `hsportal gel orange` — spray gel on the aimed face
- `hsportal clear` — remove your portals
- `hsportal status`
- `hsportal debug`

On a dedicated server, type these in the in-game F1 console after you join (or `hsportal room YourName` from the server window).

## Rebuild assets

Unity **2022.3.62f2** and official Blender (not Microsoft Store):

```
powershell -File build_assets.ps1
```

Then rebuild `HSPortal.dll` from `Source/HSPortal.csproj`.

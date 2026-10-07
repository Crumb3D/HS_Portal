# HS Portal

Survival-styled portal gun, gel, weighted cube, and long-fall boots for **7 Days to Die 3.2**. Portal 2 is a behaviour reference only — no Valve meshes.

The held gun is an original industrial 7DTD weapon (claws, blue/orange coils, DANGER pack) built from `portal Gun Design.png`. **Version 0.3.2.**

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
3. Craft at a **workbench**: Portal Gun, Gel Gun, **Goo ammo**, Weighted Cube, Long-Fall Boots.

The **Portal Handbook** (one paper) is always craftable and is not consumed when read.

Creative menu still has everything. Admin `hsportal give` dumps the whole kit.

## Use

**Portal gun** — left click blue, right click orange, **middle click** fizzles both. The HUD at the top of the screen lights the colours that are currently out; the bar between them lights when the pair is linked.

**Gel gun** — craft **Repulsion/Propulsion Goo** (paint, scrap polymers, acid) and reload with R. The ammo widget on the right of the HUD shows the magazine. Left click blue bounce gel, right click orange speed gel.

**Long-fall boots** — feet slot, like vanilla shoes. They hide the default feet and sit on the character (3rd person and other players). Light-armor protection, durability, and a small run-speed bonus while worn. Fall damage is ignored only while they are equipped.

**Weighted cube** — placeable scrap-steel crate. Diffuse, displacement, and glow maps drive the faces; the heart lights. E to pick up.

Valid surfaces: full non-terrain cubes (walls, floors, ceilings). Terrain, plants, wedges, doors, and plates reject.

Admin console (`hsportal`):

- `hsportal give` — portal gun, gel gun, 32 goo, cube, boots
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

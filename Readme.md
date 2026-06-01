<img width="1536" height="376" alt="image" src="https://github.com/user-attachments/assets/4586a742-7e58-4311-be00-54edef84c6db" />

A narrative campaign mod for Kerbal Space Program built on the Contract Configurator
framework. Red Frontier augments KSP's sandbox universe with a structured career-mode
space program, with missions that build on each other, characters who comment on your
progress, and infrastructure that persists and matters.

Kerbal Space Program gives you everything you need to run a space program. What it
doesn't give you is a reason to.

Red Frontier doesn't replace the sandbox  it gives it direction. A structured sequence
of missions with characters, stakes, and a story that builds toward something. The tools
are still yours. Red Frontier just gives them somewhere to go.

<img width="1492" height="837" alt="image" src="https://github.com/user-attachments/assets/f0aa57a5-78e9-4999-882d-999f4331c6b6" />

---

## v0.1.9  - "Red Rover"

### *Red Rover, Red Rover, send updates on over.*

Both Rubicon rover missions have been completely overhauled. Rubicon 8 and Rubicon 10
now deploy fully equipped survey rovers with a scanning arm, surface scanner, and a
complete instrument suite for seismic, thermal, and pressure measurements. Each mission
generates random waypointed survey sites and requires instruments to be run at each
location, not just anywhere in the biome.

<img width="1325" height="650" alt="image" src="https://github.com/user-attachments/assets/57f6da3f-854e-49e3-876a-38d1bf60cc92" />


This is made possible by **CollectScienceNear**, a new bundled Contract Configurator
extension that ships with Red Frontier. CC's built-in parameters can track waypoint
proximity or science collection, but not both in the same parameter — there is no native
way to require that a specific experiment was collected at a specific location.
CollectScienceNear combines the two: it checks whether the experiment was collected
while the active vessel was within range of a named waypoint. 

A seismic scan at Survey Site Alpha satisfies the Alpha requirement. 
The same scan conducted anywhere else does not. This means each survey site has genuinely independent science objectives, running all your instruments at one location cannot fulfill the requirements of another.
CollectScienceNear will see more use in Chapter 2.


### Changes

- Rubicon 8 fully overhauled: rover now requires scanning arm, seismometer, thermometer,
  and barometer; survey sites waypointed with per-location science requirements; ROC scan
  bonus available for surface features encountered in transit
- Rubicon 10 fully overhauled: rover requirements match Rubicon 8 plus adds surface
  scanner; survey structure changed from any-one-of-three to all-three in sequence;
  return-to-outpost leg added as final mission step
- CollectScienceNear parameter extension added to bundled CC extensions; hooks
  OnExperimentDeployed at scan time with waypoint proximity check
- Mission descriptions, synopses, and objective notes revised across both contracts to
  reflect updated mission profiles

---

## Starting the Campaign

Red Frontier is designed to be started on a fresh or early-game career mode save. The
campaign opens with Project Rubicon, which assumes no prior career experience, and builds
everything from scratch.

If you load it into a mature save, early Rubicon missions may complete immediately based
on existing vessels and technology. This is expected behavior. The campaign should
advance to the appropriate point and continue normally. All missions can be reset by canceling them in mission control and allowing them to re-accept automatically. This will re-roll new waypoints in applicable mission profiles.

---

## Dependencies

### Required

| Mod | Version |
|---|---|
| Kerbal Space Program | 1.12.x |
| Breaking Ground DLC | Any |
| Contract Configurator | 1.30.0+ |
| Module Manager | Latest |

### Bundled

The following Contract Configurator extensions ship inside the Red Frontier mod folder
and load automatically. No separate installation is required.

- **ResourceTransfer**
- **HasInventoryPart**
- **DeployedScienceStation**
- **CollectScienceNear** *(new in v0.1.9)*

### Recommended

These mods are not required. The campaign works without them, but the experience is
better with them.

| Mod | Why |
|---|---|
| Community Tech Tree | Campaign pacing is tuned for CTT progression |
| Near Future Propulsion | Better engine options for Duna transfer vessels |
| Stockalike Station Parts Redux | Station construction feels more natural |
| Planetary Base Systems | Surface habitat missions feel more natural |

---

## Installation

Mod is available through CKAN. Install Contract Configurator, Module Manager, and any
recommended mods through CKAN before proceeding. See the [User guide][2] to get started
with CKAN.

Breaking Ground DLC must be installed through Steam or your KSP storefront.

**To install Red Frontier manually:**

1. Download the latest release from this repository
2. Extract the `RedFrontier` folder into `Kerbal Space Program/GameData/ContractPacks/`
3. Your final path should read: `GameData/ContractPacks/RedFrontier/`
4. Launch KSP and start a new save, or load an existing save where you have reached
   orbit at least once

The first Rubicon contract will appear in Mission Control automatically.

---

## Feedback

This is a playtester release. Your feedback directly shapes the final version.

Please report bugs, broken contracts, dialog issues, and any moment where the campaign
felt unclear or unfair. Feature requests and design opinions are also welcome.

Submit a bug report: https://github.com/Red-Frontier/RedFrontier/issues

For contract bugs, including the contract name (visible in Mission Control) and a brief
description of what went wrong is helpful.

---

## What's Next

Chapter 1 is the foundation. Future releases will expand operations at Duna, introduce
new factions and storylines, and eventually take the campaign to the outer planets.

---

Red Frontier © 2026 by Benjamin Creasy is licensed under CC BY-SA 4.0
Attribution-ShareAlike 4.0 International

[2]: https://github.com/KSP-CKAN/CKAN/wiki/User-guide

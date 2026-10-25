---
name: lowpoly-assets
description: How to find, download, license-check and import free low-poly 3D assets (characters, zombies, weapons, buildings, nature) and keep the POLYGON/Synty-like art style. Use when the user asks for new models, textures, animations, sounds, or wants the game to look better.
---

# Low-poly assets & art style

## Target look
Flat-shaded low-poly (POLYGON / Synty style): simple shapes, a single color-atlas texture per pack, saturated but soft colors, strong directional sun with soft shadows, linear distance fog, light bloom + neutral tonemapping + slight vignette (see `PrototypeBuilder.CreatePostProcessing`). Avoid realistic PBR textures and normal maps — they break the style.

## Free sources (prefer CC0)
| Source | Content | License | Download |
|---|---|---|---|
| kenney.nl/assets | survival-kit, blaster-kit, blocky-characters, city-kit-suburban/commercial/industrial, nature-kit, sounds | CC0 | page HTML contains a direct `https://kenney.nl/media/pages/assets/<slug>/<hash>/<file>.zip` link — fetch the page and grep for `.zip` |
| quaternius.com | zombies, guns, modular buildings, animated characters | CC0 | Google Drive links often hit "Quota exceeded" from servers — check OpenGameArt mirrors first (e.g. `opengameart.org/content/low-poly-guns-pack` → `ultimate_gun_pack_by_quaternius.zip`) |
| kaylousberg.itch.io (KayKit) | city, dungeon, characters with animations | CC0 | itch.io (manual) |
| Unity Asset Store: "POLYGON Starter Pack" (Synty) | the exact screenshot style | Asset Store EULA — **do not commit to a public repo** | user downloads via Package Manager → My Assets |
| mixamo.com | humanoid animations | free for games, not redistributable raw | user downloads (needs Adobe account) |

## Import rules
- Put files in `Assets/ThirdParty/<Author>/<Pack>/` and keep the pack's `License.txt` next to them.
- Commit only the FBX folder + its `Textures/` (skip GLB/OBJ duplicates and previews) to keep the repo small.
- After URP is active, FBX materials import as URP/Lit. If they are pink, reimport the folder (or run *Zombie Survival → 1*).
- Scale is normalized in code (`FitToHeight`, `FitToMaxSize`, `FitToMaxFootprint` in the builder) — do not hand-tune import scale.
- Quaternius FBX materials import with opacity 0 — the builder replaces them with opaque URP materials (`MakeOpaqueMaterials`). Do the same for any new Quaternius model.
- Guns are auto-rotated barrel-forward (`OrientBarrelForward`), no manual yaw needed.
- Zombie skins: tinted copies of the character material (`TintedCopy`) — swap to real zombie models when available.
- Non-CC0 assets (Synty, Mixamo) → add their folder to `.gitignore`.

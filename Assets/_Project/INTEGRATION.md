# FAST VR Integration Notes

## Scene flow
1. `MainMenu` — info card → game selection (WebSocket + VR buttons) on campsite environment
2. `ForestWalk` — on-rails forest walk with dashboard video stream
3. `PaintingGame` — drawing game

## Campsite menu setup (required once)
The campsite must be baked into `MainMenu` as a prefab at the origin — not loaded as a separate scene.

In Unity: **FAST VR → Integrate Campsite Into Main Menu**

This will:
- Build `Assets/_Project/Prefabs/CampsiteEnvironment.prefab` from `MenuCampsite.unity`
- Offset terrain/props so the campsite chair aligns with the XR Origin at `(0, 0, 0)`
- Place the prefab in `MainMenu.unity`
- Disable the template `Plane`
- Remove `MenuCampsite` from Build Settings

Re-run the menu item if you update the campsite source scene.

## MainMenu player placement
- `MenuSpawnPoint` in `MainMenu.unity` defines where the XR Origin is locked (move it in the Scene view to tune).
- `MainMenuXRRigLock` snaps Y to terrain at that XZ and rotates the rig to face the `Canvas`.
- Do not move the XR Origin directly — adjust `MenuSpawnPoint` instead.

## Scene names (see `GameSceneNames.cs`)
- Forest walk: `ForestWalk`

## Dashboard commands (forest)
- `start_forest` — load ForestWalk
- `start_walk` / `pause_walk` / `resume_walk` — control spline walk
- `back_to_menu` — return to MainMenu

## Imported content
Assets from `fastvr-unity` live under `Assets/_Project/Imported/`.
Source campsite layout reference: `Assets/_Project/Scenes/MenuCampsite.unity` (editor only, not loaded at runtime).

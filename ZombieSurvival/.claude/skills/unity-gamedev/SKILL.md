---
name: unity-gamedev
description: Conventions and workflow for writing C# gameplay code in this Unity 6 (URP) project. Use whenever adding or changing scripts, prefabs, scenes, the PrototypeBuilder editor tool, or package versions in ZombieSurvival.
---

# Unity gameplay development (ZombieSurvival)

## Project facts
- Unity **6000.3 LTS**, Universal Render Pipeline 17.3, Input System (new), Netcode for GameObjects 2.x, AI Navigation 2.x.
- Code lives in `Assets/ZombieSurvival/Scripts/<System>/`, namespace `ZombieSurvival` (editor code: `ZombieSurvival.EditorTools`, inside an `Editor/` folder).
- No asmdefs: scripts compile into Assembly-CSharp, which auto-references the package assemblies.
- The scene and prefabs are **generated** by `Scripts/Editor/PrototypeBuilder.cs` (menu *Zombie Survival → 2*). When a new component needs to be on a prefab or in the scene, wire it in the builder instead of telling the user to drag things in the inspector. Private serialized fields are set through `SetFields(...)` / `SerializedObject`.
- Third-party models: `Assets/ThirdParty/<Author>/<Pack>/` with their License.txt.

## Code style
- One MonoBehaviour/NetworkBehaviour per file, file name = class name.
- `[SerializeField]` private fields with sensible defaults, public read-only properties for other systems.
- Russian `<summary>` comments and UI strings (the player-facing language is Russian); identifiers in English.
- Cache components in `Awake`; never `GetComponent` / `Find*` every frame.
- Input: `UnityEngine.InputSystem` (`Keyboard.current`, `Mouse.current`), never the legacy `Input` class.
- Use `FindAnyObjectByType<T>()` / `FindObjectsByType<T>(FindObjectsSortMode.None)` — the old `FindObjectOfType` is obsolete in Unity 6.
- URP materials: shader `Universal Render Pipeline/Lit`, color via `material.color` (maps to `_BaseColor`).
- Temporary UI is `OnGUI`; that is intentional for the prototype — do not mix in uGUI unless asked to build the real UI.

## Verifying without the Unity editor
The cloud container has no Unity. Before committing C# changes, compile-check them:
1. Install .NET SDK (`dotnet-install.sh --channel 8.0`).
2. Get Unity reference DLLs from NuGet package `Unity3D.SDK` (UnityEngine.dll + UnityEditor.dll, Unity 2021 API).
3. Write small stubs for the package APIs used (Netcode, AI Navigation, Input System, URP/Volume) — check signatures against the real package source from `https://download.packages.unity.com/<pkg>/-/<pkg>-<ver>.tgz`.
4. `dotnet build` a netstandard2.1 project including `Assets/**/*.cs`. Only Unity 6-only APIs (e.g. `FindAnyObjectByType`) may fail there — verify those by docs.

## Package versions
Look up real versions before editing `Packages/manifest.json`:
`curl -s https://packages.unity.com/<package-name>` (check `versions[*].unity` compatibility). URP is a core package locked to the editor version (17.3.x for 6000.3).

---
name: unity-netcode-coop
description: Rules for multiplayer code with Netcode for GameObjects 2.x in this co-op survival game — who owns what, how to write RPCs and NetworkVariables, how to spawn objects. Use when touching anything that must sync between players (zombies, loot, building, health, day/night, inventory).
---

# Co-op networking (Netcode for GameObjects 2.x)

## Authority model used in this project
| Thing | Authority | How it syncs |
|---|---|---|
| Player movement & camera | owner | `OwnerNetworkTransform` (overrides `OnIsServerAuthoritative() => false`) |
| Inventory, crafting, ammo in magazine | owner (local) | server grants items via `PlayerInventory.GiveRpc` (`SendTo.Owner`) |
| Health / damage | server | `NetworkVariable<float>`; clients request via `DamageRpc` (`SendTo.Server`) |
| Zombies (AI, spawn, death) | server | `NetworkTransform` (server), `NavMeshAgent` enabled only on server |
| Loot containers | server | `NetworkVariable<bool> Looted`, `LootRpc(RpcParams)` validates distance |
| Building blocks | server | `PlaceBlockRpc` / `DemolishRpc`, spawned with `NetworkObject.Spawn(true)` |
| Day/night time | server | `NetworkVariable<float> TimeOfDay`, `NetworkVariable<int> Day` |

Keep this model: gameplay that affects other players is decided on the server; owners may predict locally only for their own movement and UI.

## Patterns
- Universal RPCs: `[Rpc(SendTo.Server)] void FooRpc(...)` — the method name **must end in `Rpc`**. Sender id: add `RpcParams rpcParams = default` and read `rpcParams.Receive.SenderClientId`.
- Permission: `InvokePermission` (default Everyone). `RequireOwnership` is obsolete.
- Server-side validation for every client request (distance, cost, free cell). On rejection, refund the owner.
- `NetworkVariable` write permission is server by default; subscribe to `OnValueChanged` in `OnNetworkSpawn`, unsubscribe in `OnNetworkDespawn`.
- Do work in `OnNetworkSpawn`, not `Start`, when it depends on `IsServer` / `IsOwner`.
- Runtime-spawned prefabs must be registered before `StartHost/StartClient`: add them to `NetworkMenu.networkPrefabs` (the builder does this). `ForceSamePrefabs` is on.
- Spawn: `Instantiate(prefab)` on the server, then `.Spawn(true)`. Despawn with `NetworkObject.Despawn()`.
- Scale/visual differences that must match on clients belong in the prefab, not set after `Instantiate` (scale is not synced without NetworkTransform).
- Never trust client-reported damage values in PvP; in this PvE co-op it is accepted for simplicity.

## Testing
- Window → Multiplayer → Multiplayer Play Mode (virtual players) for quick 2-player tests in one editor.
- Internet play later: Unity Relay via `com.unity.services.multiplayer` (sessions API) — set `UnityTransport` relay data before `StartHost`.

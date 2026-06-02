# VFX Forge

VFX Forge is a Unity VFX Graph framework for driving instant and persistent visual effects from ECS systems and hybrid MonoBehaviours.

This README is currently a documentation draft. Each section below records the package surface discovered in the repository and what the final README should explain.

## Contents

- [Overview](#overview)
- [Package Layout](#package-layout)
- [Core Concepts](#core-concepts)
- [Getting Started](#getting-started)
- [Authoring VFX Data Types](#authoring-vfx-data-types)
- [Creating VFX Definitions](#creating-vfx-definitions)
- [VFX Graph Requirements](#vfx-graph-requirements)
- [Registering a Visual Effect](#registering-a-visual-effect)
- [Spawning Instant VFX](#spawning-instant-vfx)
- [Spawning Persistent VFX](#spawning-persistent-vfx)
- [Updating and Killing Persistent VFX](#updating-and-killing-persistent-vfx)
- [Decal Projector VFX](#decal-projector-vfx)
- [Editor Preview and Preferences](#editor-preview-and-preferences)
- [System Order](#system-order)
- [API Reference](#api-reference)
- [Parallel Safety](#parallel-safety)
- [Memory Management](#memory-management)
- [Performance Notes](#performance-notes)
- [Validation and Debugging](#validation-and-debugging)
- [Limitations and What to Avoid](#limitations-and-what-to-avoid)
- [Samples](#samples)
- [Future Documentation TODO](#future-documentation-todo)

## Overview

Document the high-level promise here: VFX Forge lets gameplay code spawn GPU-buffer-backed VFX without allocating a new GameObject per effect instance.
The final section should distinguish:

- **Instant VFX**: one-frame spawn requests uploaded to a VFX Graph.
- **Persistent VFX**: tracked entries that keep transform, optional per-instance data, and optional array data alive across frames.
- **Hybrid authoring**: `HybridVisualEffect` owns the `VisualEffect` component and registers a `VFXDefinition` into the ECS singleton.
- **GPU buffer batching**: data is uploaded through `GraphicsBuffer` properties rather than individual exposed fields.

## Core Concepts

Explain the core model before showing code:

- `VFXDefinition` is the ScriptableObject identity for a graph. It stores the key, graph asset, VFX type, capacity, timeout, data type, and array data type.
- `VFXKey` is the compact runtime handle derived from a definition key. (ushort size. Thus no more than sizeof(ushort) definitions can exist. Main bottleneck: Decals which do not share the same texture (as each unique texture is 1 definition). SOlution: use sprite atlasses)
- `VFXSingleton` is the ECS singleton that stores registered graph entries for the current world.
- `InstantVFXEntry` accepts spawn requests and optional payload data for instant graphs.
- `PersistentVFXEntry` returns `TrackedEntity` handles that can be queried, updated, or killed while the effect is alive.
- `TrackedEntity` can be deferred for the current frame until `SyncVFXSystem` resolves it into the persistent backing buffers.
- `VFXTransform` is the persistent transform payload uploaded to VFX Graphs and includes lifetime/alive state bits.

## Getting Started

Draft the final flow:

1. Create unmanaged VFX data structs and mark them with `[VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]`.
2. Create or choose a VFX Graph template from `Shaders/Templates`.
3. Create a `VFXDefinition` asset through the VFX Forge editor menu.
4. VFX Forge editor menu does that -> [Assign the VFX Graph asset], choose instant or persistent mode, set capacity and timeout, and select data types.
5. // Add a `HybridVisualEffect` GameObject with a `VisualEffect` component and assign the definition. <- this is not needed as the VFX Forge editor menu does it 
6. From ECS, fetch `VFXSingleton`, get the registered entry by key, and call `Spawn`.

The final README should include a complete minimal scene setup, including how definitions receive stable IDs through the project object-management workflow.

## Authoring VFX Data Types

Document that VFX payload types must be unmanaged and registered for GraphicsBuffer use.
Examples exist in `Assets/Scripts/Game/Game.Data/VFXTypes` and `Samples~/Demo/DamageNumbers`.

```csharp
[VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
public struct VFXHitSparksRequest
{
    public Vector3 Position;
    public Vector3 Color;
}
```

Also document array-data payloads and custom bakers:

- `VFXDataTypeBaker<T>` bakes one data value for editor preview or authored defaults. <- show code for a basic one
- `VFXArrayDataTypeBaker<T>` bakes a temporary `NativeArray<T>` for array data. <- show code for a basic one
- Default bakers exist for every registered data type.
- Built-in supported Unity types include `int`, `uint`, `float`, `Vector2`, `Vector3`, `Vector4`, and `Matrix4x4`.

Mention the important rule: if a baker targets a custom type, that type must also be registered with `VFXTypeAttribute`.

## Creating VFX Definitions

Describe `VFXDefinition` fields:

- `visualEffectAsset`: VFX Graph asset used by the backing `VisualEffect`.
- `vfxType`: `Instant` or `Persistent`.
- `capacity`: persistent maximum active tracked entries; internally many persistent buffers allocate double capacity to handle holes and reuse.
- `timeoutDuration`: how long an inactive graph remains enabled before buffers are disposed and the GameObject is deactivated.
- `vfxDataType`: optional per-spawn or per-instance payload type.
- `vfxArrayDataType`: optional variable-length array payload type.

## VFX Graph Requirements

List required exposed properties and buffers. The graph must expose `Bounds` as a `Vector3`, because `HybridVisualEffect` validates and expands it.

VFX Forge properties (names must match):

| Property | Purpose |
| --- | --- |
| `SpawnRequestsCount` | Number of single-instance spawn requests uploaded this frame. |
| `SpawnArrayRequestsCount` | Number of array element spawn requests uploaded this frame. |
| `DataBuffer` | Per-spawn or per-instance data payload. |
| `ArrayDataBuffer` | Contiguous array payload data. |
| `ArrayPtrBuffer` | Per-spawn pointer/count metadata into array data. |
| `ArraySpawnIndexBuffer` | Index mapping for array element particles. |
| `SpawnIndexBuffer` | Persistent spawn index list for newly activated entries. |
| `TransformBuffer` | Persistent transform/lifetime data. |

The final README should map these properties to the included VFX blocks:

- `[VFX Forge] Initialize Instant Particle`
- `[VFX Forge] Initialize Array Particle`
- `[VFX Forge] Initialize Persistent Particle`
- `[VFX Forge] Initialize Persistent Particle (With Array)`

## Registering a Visual Effect

Explain `HybridVisualEffect`:

- Requires a `VisualEffect` component.
- Assigns the definition's graph asset to the `VisualEffect`.
- Registers an ECS entity with `RegisteredVFX` and `HybridVisualEffectData`.
- Calls `InitializeVFXSystem` so the runtime entry is available.
- Deactivates the graph GameObject in play mode until there are pending requests or editor preview activity.
- Reinitializes the graph during cleanup.

Warn that each `VFXDefinition` key can only be registered once in a world. Duplicate registrations log an error and are rejected.

## Spawning Instant VFX

Document the instant API (explain what VFXKeys is + show code for it):

```csharp
var vfx = SystemAPI.GetSingleton<VFXSingleton>().AsParallelWriter();
vfx.GetInstant(VFXKeys.Explosion).Spawn(new VFXExplosion
{
    Position = position,
});
```

Available forms to document:

- `Spawn()`
- `Spawn<T>(T spawnData)`
- `Spawn<U>(NativeArray<U> arrayData)`
- `Spawn<T, U>(T spawnData, NativeArray<U> arrayData)`
- `SpawnUnsafe(byte* spawnData, NativeArray<byte> arrayData = default)`
- `SpawnUnsafe(NativeArray<byte> arrayData)`

Explain that instant requests are gathered per worker thread, remapped, uploaded during `SyncVFXSystem`, then cleared.

## Spawning Persistent VFX

Document persistent spawn:

```csharp
var tracked = vfx.GetPersistent(VFXKeys.ElectroArc).Spawn(targetEntity, duration);
```

Available forms to document:

- `Spawn(Entity entityToTrack, float trackingDuration = 0f)`
- `Spawn<T>(Entity entityToTrack, T data, float trackingDuration = 0f)`
- `Spawn<U>(Entity entityToTrack, NativeArray<U> arrayData, float trackingDuration = 0f)`
- `Spawn<T, U>(Entity entityToTrack, T data, NativeArray<U> arrayData, float trackingDuration = 0f)`
- unsafe byte-pointer variants.

Explain behavior:

- Returns `TrackedEntity.Null`/invalid when capacity is exceeded.
- Tracks `LocalToWorld` for the entity when present.
- `Entity.Null` is allowed for non-entity-tracked persistent effects.
- `trackingDuration == 0` means continue tracking until manually killed or the tracked entity dies.
- eplxain positive tracking duration

## Updating and Killing Persistent VFX

Document the post-spawn API:

```csharp
ref var entry = ref vfx.GetPersistent(VFXKeys.ElectroArc);

if (entry.IsAlive(trackedEntity))
{
    entry.TrySetUpdateData(trackedEntity, playerPosition); // TrySet can be used without IsAlive check (as any `Try` methods)
}

entry.TryKill(trackedEntity);
```

Cover:

- `IsAlive(TrackedEntity)`
- `TryGetUpdateDataAsRef<T>(TrackedEntity, out Ref<T>)`
- `TryGetArrayData<T>(TrackedEntity, out UnsafeArray<T>)`
- `TryGetArrayDataUnsafe(TrackedEntity, out UnsafeArray<byte>)`
- `TrySetUpdateData<T>(TrackedEntity, T)`
- `TrySetUpdateDataUnsafe(TrackedEntity, byte*)`
- `TryKill(TrackedEntity)`

Mention that handles spawned this frame are deferred until `SyncVFXSystem` resolves them, but update/kill methods account for deferred handles where possible.

## Decal Projector VFX

Draft a dedicated section for the decal convenience workflow:

- `VFXDecalProjector` is a hybrid authoring component for sprite-backed decals.
- `VFXDecalDefinition` produces persistent `VFXDefinition` instances for sprite/definition pairs.
- Runtime ECS components include `DecalProjectorData`, `RuntimeDecalLookup`, and `DecalProjectorVFX`.
- `InitializeVFXDecalsSystem` creates/cleans backing `HybridVisualEffect` instances per decal lookup.
- `UpdateVFXDecalsSystem` updates draw distance, enabled state, and per-decal data.

Document the Preferences path for selecting the default decal definition: `Preferences/FireAlt/VFX Forge`.

## Editor Preview and Preferences

Describe editor-only features:

- `HybridVisualEffect` supports edit-mode initialization and preview spawning.
- The custom inspector reads definition data and can spawn/update preview payloads through type bakers.
- `VFXSettings.DefaultDecalVFX` is stored in EditorPrefs using the VFX Forge Preferences provider.
- Definition changes trigger editor refresh through `VFXDefinition.OnVFXDefinitionChanged`.

Mention that editor preview uses the same runtime singleton path where possible, so stale preview behavior usually means the definition, baker data, or graph property list is out of sync.

## System Order

Document the runtime groups:

- `InitializeVFXSystemGroup`: registers new `HybridVisualEffect` instances.
- `UpdateVFXSystemGroup`: updates persistent transform state and kill requests.
- `BeforeVFXTransformSystemGroup`: hook point before transform upload data is finalized.
- `AfterVFXTransformSystemGroup`: hook point after transform data is prepared.
- `SyncVFXSystem`: runs at the end of `PresentationSystemGroup` and uploads data to VFX Graph buffers.

Important timing rule to document: do not spawn persistent VFX after `VFXTransformSystem` has run for the frame and before `SyncVFXSystem`.
The code explicitly throws if a persistent spawn reaches sync without transform data, with the current concrete warning: do not spawn persistent VFX in `LateUpdate`.

## API Reference

Keep this section as a concise table in the final README.

| Type | Role |
| --- | --- |
| `VFXSingleton` | World singleton and main lookup API. |
| `VFXSingleton.ParallelWriter` | Job-safe lookup wrapper for registered entries. |
| `InstantVFXEntry` | Per-graph instant spawn request API. |
| `PersistentVFXEntry` | Per-graph persistent spawn, update, query, and kill API. |
| `TrackedEntity` | Persistent VFX handle. |
| `HybridVisualEffect` | MonoBehaviour bridge between `VisualEffect` and ECS. |
| `VFXDefinition` | ScriptableObject graph definition. |
| `VFXDecalProjector` | Hybrid sprite decal authoring component. |
| `VFXDataTypeBaker<T>` | Single payload editor/default baker. |
| `VFXArrayDataTypeBaker<T>` | Array payload editor/default baker. |

## Parallel Safety

Document what is safe:

- Use `SystemAPI.GetSingleton<VFXSingleton>().AsParallelWriter()` inside jobs.
- `ParallelWriter.GetInstant` and `GetPersistent` use read-only hash-map lookup to retrieve registered entries.
- Instant spawn requests are thread-local through `UnsafeThreadData` and `UnsafeThreadToListMapper`.
- Persistent spawn requests use atomic index reservation and per-thread request lists.
- Persistent kill requests are queued and resolved later.

Document what needs care:

- The entry reference returned from `GetInstant`/`GetPersistent` is mutable. Keep usage to the package-supported spawn/update/kill methods.
- Do not store `ref` entries beyond the intended buffer ownership pattern.
- Respect system ordering for persistent spawns so transform data is available before sync.

## Memory Management

Document ownership clearly:

- `VFXSingleton` owns persistent native containers and disposes them in `SyncVFXSystem.OnDestroy`.
- `VFXGraphicsBuffersSingleton` owns managed `GraphicsBuffer` wrappers for active graphs and disposes them before VFX data cleanup.
- Instant request buffers are reused and cleared after upload.
- Persistent buffers live for the definition lifetime and use capacity-based allocation.
- Persistent array payloads are copied into pooled/deferred native arrays first, then moved into `UnsafeHeapMemory` when the request resolves.
- The deferred pooled-array slot is the authoritative owner until sync takes and clears it.

Add an explicit warning for future maintainers: do not dispose a copied `PooledUnsafeArray<byte>` while leaving the original deferred slot populated. Move ownership by ref, clear the slot immediately, then dispose the taken owner after consumption.

## Performance Notes

Explain where performance comes from:

- Effects are batched into VFX Graph `GraphicsBuffer` uploads.
- Instant requests avoid shared contention by gathering per thread and merging once.
- Persistent entries upload only relevant transform/data ranges when possible.
- Persistent graph capacity is preallocated, which avoids per-spawn persistent container allocation.
- Inactive graphs are disabled after timeout to release graphics buffers and stop unnecessary graph updates.

Document tradeoffs:

- Persistent definitions allocate buffers based on capacity, including double-capacity backing storage for transform/data holes and delayed reuse.
- Large array payloads are copied on spawn.
- `SyncVFXSystem` bridges Burst-side data to managed `VisualEffect`/`GraphicsBuffer` upload code and currently completes resolve jobs before managed upload.
- High spawn counts should prefer few definitions with batched payloads rather than many unique graph definitions.

## Validation and Debugging

Document available checks:

- Mismatched generic payload types are checked against the definition stable type hash.
- Zero-sized data expectations are checked for `Spawn()` overloads.
- `HybridVisualEffect` validates that the graph has the expected `Bounds` property.
- Graphics buffer wrappers check required exposed buffers before upload.

## Limitations and What to Avoid

Draft the final caution list:

- Do not register two `HybridVisualEffect` instances with the same `VFXDefinition` key in the same world.
- Do not spawn persistent VFX in `LateUpdate` or any path that runs after `VFXTransformSystem` but before `SyncVFXSystem`.
- Do not pass negative tracking durations.
- Do not assume a persistent spawn always succeeds; check `TrackedEntity.IsValid` when capacity pressure is possible.
- Do not use custom payload structs without `VFXTypeAttribute`.
- Do not mismatch definition payload type and generic `Spawn<T>`/`TrySetUpdateData<T>` calls.
- Do not rely on per-effect GameObject transforms for runtime instances; persistent transform data is driven from tracked entities and `VFXTransformSystem`.

## Samples

Describe the sample package:

- `Samples~/Demo/DEMO.unity`: basic demo scene.
- `Samples~/Demo/DamageNumbers`: damage-number graph, data types, glyph baker, and definition.
- `Samples~/Demo/TemplatesDefinitions`: definition assets for the built-in instant/persistent templates.

The final README should include a short "Import Sample" note for Unity Package Manager and link the sample data type file as a reference implementation.

## Future Documentation TODO

- Add screenshots or GIFs of the demo scene and editor inspector.
- Add a complete instant VFX setup walkthrough.
- Add a complete persistent VFX setup walkthrough.
- Add a VFX Graph node/property checklist per template.
- Add a decal projector setup walkthrough.
- Add a troubleshooting matrix for common graph-property and type-hash errors.
- Add measured benchmark or profiler data before making stronger performance claims.

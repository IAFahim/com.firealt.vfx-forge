# VFX Forge

VFX Forge is a Unity VFX Graph framework for driving instant and persistent visual effects from ECS systems and hybrid MonoBehaviours.

It lets gameplay code submit VFX requests as unmanaged data, batch those requests into GPU buffers, and render many effect instances through a small number of registered VFX Graphs instead of spawning one GameObject per visual effect instance.

## Contents

- [Overview](#overview)
- [Core Concepts](#core-concepts)
- [Getting Started](#getting-started)
- [Authoring VFX Data Types](#authoring-vfx-data-types)
- [Creating VFX Definitions](#creating-vfx-definitions)
- [VFX Graph Requirements](#vfx-graph-requirements)
- [Registering a Visual Effect](#registering-a-visual-effect)
- [Instant VFX](#spawning-instant-vfx)
- [Persistent VFX](#spawning-persistent-vfx)
- [Editor Preview](#editor-preview-and-preferences)
- [System Order](#system-order)
- [Parallel Safety](#parallel-safety)
- [GPU Memory Management](#memory-management)
- [Performance Notes](#performance-notes)
- [Limitations and What to Avoid](#limitations-and-what-to-avoid)
- [Samples](#samples)

## Overview

VFX Forge features:

- **Instant VFX**: one-frame spawn requests. Gameplay systems enqueue requests, `SyncVFXSystem` uploads the request buffers to VFX Graph, then the request buffers are cleared.
- **Persistent VFX**: tracked effect instances. Each spawn returns a `TrackedEntity` handle that can be checked, updated, or killed while the effect is alive.
- **Hybrid authoring**: `HybridVisualEffect` owns the Unity `VisualEffect` component, points it at a `VFXDefinition`, and registers that graph into the current ECS world.
- **GPU buffer batching**: per-instance and array data are uploaded through `GraphicsBuffer` properties such as `DataBuffer`, `ArrayDataBuffer`, and `TransformBuffer`.

The package is split into runtime, data, authoring, editor, optional BovineLabs integration, tests, shaders, and samples.

## Core Concepts

`VFXDefinition` is the ScriptableObject identity for a graph. It stores the necessary information to describe a VFX.

`VFXKey` is the runtime handle used to find registered graph entries. It stores a `ushort`, so a project can have at most `ushort.MaxValue` VFX keys.
Decals can consume keys quickly because each unique texture lookup creates its own definition; prefer sprite atlases where many decals should share a texture source.

`VFXSingleton` is the ECS singleton. It is the public API entry, stores registered instant and persistent entries and exposes `AsParallelWriter()` for job usage.

`InstantVFXEntry` accepts one-frame spawn requests with optional single data and optional array data.

`PersistentVFXEntry` accepts tracked spawn requests and returns `TrackedEntity` handles. A persistent handle may be deferred until `SyncVFXSystem` resolves it into the backing persistent buffers.

`VFXTransform` is the persistent transform payload uploaded to VFX Graph. It includes position, rotation, scale, tracking duration, and packed alive state.

## Getting Started

1. Create unmanaged payload structs and mark custom payloads with `[VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]`.
2. Create or choose a VFX Graph template from `Shaders/Templates`.
3. Select the VFX Graph asset and run `FireAlt/Create VFX Definitions from VFX Assets` on the main toolbar.
4. Configure the generated `VFXDefinition`: choose `Instant` or `Persistent`, set capacity and timeout, and select the single payload and array payload types.
5. Use the generated GameObject with `VisualEffect` and `HybridVisualEffect`. The menu creates and wires it to the generated definition.
6. 
   1. From ECS, fetch `VFXSingleton` using `SystemAPI.GetSingleton<VFXSingleton>()`, get the registered entry by `VFXKey`, and call `Spawn`.  
   2. From MonoBehavior, fetch `VFXSingleton` using `GlobalVFXSingleton.Get()`, get the registered entry by `VFXKey`, and call `Spawn`.

## Authoring VFX Data Types

Payload types must be unmanaged. Custom payload structs should be marked with `VFXTypeAttribute` so VFX Graph would see them and VFX Forge can discover them and expose them in definition dropdowns.

```csharp
using UnityEngine;
using UnityEngine.VFX;

[VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
public struct VFXHitSparksRequest
{
    public Vector3 Position;
    public Vector3 Color;
}
```

Array payloads are authored the same way:

```csharp
using UnityEngine.VFX;

[VFXType(VFXTypeAttribute.Usage.GraphicsBuffer)]
public struct VFXDamageNumberGlyph
{
    public uint GlyphIndex;
}
```

Built-in supported Unity types include `int`, `uint`, `float`, `Vector2`, `Vector3`, `Vector4`, and `Matrix4x4`.

### Data Bakers

`VFXDataTypeBaker<T>` bakes one payload value for editor preview.

```csharp
using System;
using FireAlt.VFXForge.Data;
using UnityEngine;

[Serializable] // Must be marked with System.Serializable to be able to see in the inspector
public class HitSparkBaker : VFXDataTypeBaker<VFXHitSparksRequest>
{
    public Vector3 Position;
    public Color Color; // DataBakers are useful to convert arbitrary data into a VFX ready data

    public override VFXHitSparksRequest Bake()
    {
        return new VFXHitSparksRequest
        {
            Position = Position,
            Color = Color.rgb,
        };
    }
}
```

`VFXArrayDataTypeBaker<T>` bakes variable-length array payloads.

```csharp
using System;
using FireAlt.VFXForge.Data;
using Unity.Collections;

[Serializable] // Must be marked with System.Serializable to be able to see in the inspector
public class DamageGlyphBaker : VFXArrayDataTypeBaker<VFXDamageNumberArrayData>
{
     public int number;
     
     public override NativeArray<VFXDamageNumberArrayData> Bake()
     {
         var list = new NativeList<VFXDamageNumberArrayData>(Allocator.Temp);
         BakeGlyphs(number, list); // Populate the NativeList with any data (the implementation is omitted)
         return list.AsArray();
     }
}
```

Default bakers exist for every registered payload type, but for them to be visible in the inspector, a `[System.Serializable]` is required on the VFX data type.
Custom bakers are only needed when the default field editor is not enough, and do not require `[System.Serializable]` on the VFX data type.

## Creating VFX Definitions

`VFXDefinition` fields:

| Field               | Purpose                                                                                                                 |
|---------------------|-------------------------------------------------------------------------------------------------------------------------|
| `visualEffectAsset` | VFX Graph asset assigned to the backing `VisualEffect`.                                                                 |
| `vfxType`           | `Instant` or `Persistent`.                                                                                              |
| `capacity`          | Maximum active persistent entries. Persistent buffers allocate extra backing storage to handle holes and delayed reuse. |
| `timeoutDuration`   | How long an inactive graph remains enabled before buffers are disposed and the GameObject is deactivated.               |
| `vfxDataType`       | Optional per-spawn or per-instance payload type.                                                                        |
| `vfxArrayDataType`  | Optional variable-length array payload type.                                                                            |

In the editor, `capacity` is clamped to at least `1` and `timeoutDuration` is clamped to at least `0`.

## VFX Graph Requirements

The graph must expose `Bounds` as a `Vector3`. `HybridVisualEffect` uses it to fully remove the need to adjust the bounds in the VFX Asset itself.

VFX Forge provides several VFX Asset properties which can be used in the graph to fetch the VFX Forge data:

| Property                  | Type             | Purpose                                                       |
|---------------------------|------------------|---------------------------------------------------------------|
| `SpawnRequestsCount`      | `uint`           | Number of single-instance spawn requests uploaded this frame. |
| `SpawnArrayRequestsCount` | `uint`           | Number of array element spawn requests uploaded this frame.   |
| `DataBuffer`              | `GraphicsBuffer` | Per-spawn or per-instance data payload.                       |
| `ArrayDataBuffer`         | `GraphicsBuffer` | Contiguous array payload data.                                |
| `ArrayPtrBuffer`          | `GraphicsBuffer` | Per-spawn pointer/count metadata into array data.             |
| `ArraySpawnIndexBuffer`   | `GraphicsBuffer` | Index mapping for array element particles.                    |
| `SpawnIndexBuffer`        | `GraphicsBuffer` | Persistent spawn index list for newly activated entries.      |
| `TransformBuffer`         | `GraphicsBuffer` | Persistent transform/lifetime data.                           |

Required buffers depend on the definition:

- Instant graphs with single payload data need `DataBuffer`.
- Instant graphs with array payload data need `ArrayDataBuffer`, `ArrayPtrBuffer`, and `ArraySpawnIndexBuffer`.
- Persistent graphs always need `TransformBuffer` (to, at the very least, read `IsActive` status).
- Persistent graphs with single payload data need `DataBuffer` and may need `SpawnIndexBuffer` depending on graph shape.
- Persistent graphs with array payload data need `ArrayDataBuffer`, `ArrayPtrBuffer`, and may need `ArraySpawnIndexBuffer` depending on graph shape.

The included VFX Graph templates and blocks will greatly simplify the boilerplate needed to setup a VFX Asset. These are:

- `Shaders/Templates/Instant(Single).vfx`
- `Shaders/Templates/Instant(Array).vfx`
- `Shaders/Templates/Instant(Single+Array).vfx`
- `Shaders/Templates/Persistent(Single).vfx`
- `Shaders/Templates/Persistent(Array).vfx`
- `Shaders/Templates/Persistent(Single+Array).vfx`
- `[VFX Forge] Initialize Instant Particle`
- `[VFX Forge] Initialize Array Particle`
- `[VFX Forge] Initialize Persistent Particle`
- `[VFX Forge] Initialize Persistent Particle (With Array)`

My personal preferred way is to use one of the templates provided and build the graph from there. 
The templates come with all VFX Forge fields needed for a given effect and are orginized in a "Internal" property folder to reduce clutter. 

## Registering a Visual Effect

`HybridVisualEffect` is the bridge between Unity's `VisualEffect` component and VFX Forge.

It:

- Requires a `VisualEffect` component.
- Assigns the definition's graph asset to the `VisualEffect`.
- Registers an ECS entity with `RegisteredVFX` and `HybridVisualEffectData`.
- Calls `InitializeVFXSystem` so the runtime entry is available.
- Deactivates the graph GameObject in play mode until there are pending requests or editor preview activity.
- Reinitializes the graph during cleanup.

Each `VFXDefinition` key can only be registered once in a world. Duplicate registrations are rejected.

Projects usually define a small key wrapper such as `VFXKeys` so gameplay systems do not pass raw numbers around:

```csharp
using FireAlt.VFXForge.Data;

public struct VFXKeys
{
    public const ushort Explosion = 1;

    private ushort _value;

    public static implicit operator VFXKey(VFXKeys value)
    {
        return new VFXKey { Value = value._value };
    }

    public static implicit operator VFXKeys(ushort value)
    {
        return new VFXKeys { _value = value };
    }
}
```

## Instant VFX

Spawn from ECS/MonoBehavior/Job by getting the registered instant entry:

```csharp
using FireAlt.VFXForge;
using Unity.Burst;
using Unity.Entities;

[BurstCompile]
private partial struct SpawnExplosionJob : IJobEntity
{
    public VFXSingleton.ParallelWriter VFX;

    private void Execute(in ExplosionRequest request)
    {
        VFX.GetInstant(VFXKeys.Explosion).Spawn(new VFXExplosion
        {
            Position = request.Position,
        });
    }
}
```

`InstantVFXEntry` overloads:

| Method                                                                | Use                                                           |
|-----------------------------------------------------------------------|---------------------------------------------------------------|
| `Spawn()`                                                             | Spawn with no payload data.                                   |
| `Spawn<T>(T spawnData)`                                               | Spawn with single payload value.                              |
| `Spawn<U>(NativeArray<U> arrayData)`                                  | Spawn with array payload data only.                           |
| `Spawn<T, U>(T spawnData, NativeArray<U> arrayData)`                  | Spawn with single payload and array payload data.             |
| `SpawnUnsafe(byte* spawnData, NativeArray<byte> arrayData = default)` | Unsafe raw byte path for single data and optional array data. |
| `SpawnUnsafe(NativeArray<byte> arrayData)`                            | Unsafe raw byte path for array-only spawns.                   |

Instant requests are gathered per worker thread, merged and remapped during `SyncVFXSystem`, uploaded to VFX Graph, then cleared.
Instant VFX are by design "instant" and thus the only way to supply data to the VFX is to pass it in the request method.

## Persistent VFX

Persistent VFX return a `TrackedEntity` handle:

```csharp
var tracked = vfx.GetPersistent(VFXKeys.ElectroArc).Spawn(targetEntity, duration);
```

`PersistentVFXEntry` overloads:

| Method                                                                                                               | Use                                                           |
|----------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------|
| `Spawn(Entity entityToTrack, float trackingDuration = 0f)`                                                           | Spawn with no additional data.                                |
| `Spawn<T>(Entity entityToTrack, T data, float trackingDuration = 0f)`                                                | Spawn with single payload data.                               |
| `Spawn<U>(Entity entityToTrack, NativeArray<U> arrayData, float trackingDuration = 0f)`                              | Spawn with array payload data.                                |
| `Spawn<T, U>(Entity entityToTrack, T data, NativeArray<U> arrayData, float trackingDuration = 0f)`                   | Spawn with both payload types.                                |
| `SpawnUnsafe(Entity entityToTrack, byte* data, NativeArray<byte> arrayData = default, float trackingDuration = 0f) ` | Unsafe raw byte path for single data and optional array data. |
| `SpawnUnsafe(Entity entityToTrack, NativeArray<byte> arrayData, float trackingDuration = 0f)`                        | Unsafe raw byte path for array-only spawns.                   |

Persistent behavior:

- If capacity is exceeded, the returned `TrackedEntity` is invalid, but the `Entity` inside of it is always valid.
- `Entity.Null` is valid for non-entity-tracked persistent effects.
- `trackingDuration == 0` keeps tracking until the VFX is killed or the tracked entity dies.
- Positive `trackingDuration` keeps the effect alive until `StartTrackingTime + trackingDuration`, then the transform system marks it dead.
- Negative tracking durations assert.

### Updating and Killing Persistent VFX

Persistent entries expose query, update, and kill methods for the returned handle:

```csharp
ref var entry = ref vfx.GetPersistent(VFXKeys.ElectroArc);

entry.TrySetUpdateData(trackedEntity, playerPosition);

if (!entry.IsAlive(trackedEntity))
{
    buffer.RemoveAtSwapBack(i); // Some gameplay buffer which keeps track of VFX instances
}

entry.TryKill(trackedEntity);
```

`Try` methods can be called without a separate `IsAlive` check. They return `false` when the handle cannot be resolved.

| Method                                                        | Use                                                                             |
|---------------------------------------------------------------|---------------------------------------------------------------------------------|
| `IsAlive(TrackedEntity)`                                      | Returns whether a persistent handle currently resolves to alive transform data. |
| `TryGetUpdateDataAsRef<T>(TrackedEntity, out Ref<T>)`         | Returns a mutable reference to the single payload data.                         |
| `TryGetArrayData<T>(TrackedEntity, out UnsafeArray<T>)`       | Returns the typed array payload.                                                |
| `TryGetArrayDataUnsafe(TrackedEntity, out UnsafeArray<byte>)` | Returns raw array payload bytes.                                                |
| `TrySetUpdateData<T>(TrackedEntity, T)`                       | Writes new single payload data.                                                 |
| `TrySetUpdateDataUnsafe(TrackedEntity, byte*)`                | Writes new single payload data from raw bytes.                                  |
| `TryKill(TrackedEntity)`                                      | Requests the persistent effect to be killed.                                    |

Handles spawned this frame are deferred until `SyncVFXSystem` resolves them, but all paths account for deferred handles.

## Editor Preview

`HybridVisualEffect` supports edit-mode initialization and preview spawning through its custom inspector. The inspector reads definition data and uses data bakers to spawn or update preview payloads.

## System Order

Runtime groups:

| Group/System                    | Order                                   | Role                                                                             |
|---------------------------------|-----------------------------------------|----------------------------------------------------------------------------------|
| `InitializeVFXSystemGroup`      | `LateInitializationSystemGroup`         | Registers new `HybridVisualEffect` instances.                                    |
| `UpdateVFXSystemGroup`          | `LateSimulationSystemGroup`, order last | Owns persistent transform and kill work.                                         |
| `BeforeVFXTransformSystemGroup` | Before `VFXTransformSystem`             | Hook point for systems that must run before transform upload data is finalized.  |
| `VFXTransformSystem`            | Inside `UpdateVFXSystemGroup`           | Updates persistent transform/lifetime state.                                     |
| `AfterVFXTransformSystemGroup`  | After `VFXTransformSystem`              | Hook point for systems that update persistent payloads after transform tracking. |
| `SyncVFXSystem`                 | End of `PresentationSystemGroup`        | Resolves requests and uploads data to VFX Graph buffers.                         |

Do not spawn persistent VFX after `VFXTransformSystem` has run for the frame and before `SyncVFXSystem`. 
If a persistent spawn reaches sync without transform data, the code throws with the concrete warning that persistent VFX should not be spawned in `LateUpdate`.

## Parallel Safety

Use `VFXSingleton.ParallelWriter` inside jobs. The writer exposes registered entry lookup through read-only hash maps while the entries themselves own thread-local request structures.

Safe patterns:

- Use `ParallelWriter.GetInstant(...).Spawn(...)` from parallel jobs.
- Use `ParallelWriter.GetPersistent(...).Spawn(...)` from parallel jobs.
- Use persistent `TrySet...`, `TryGet...`, `IsAlive`, and `TryKill` methods through the returned entry reference.
- Store `TrackedEntity` handles in ECS components, buffers or any other places when the effect needs later updates.

Be careful with:

- Ensure to never try to modify a Persistent data for the same `TrackedEntity` on a single `PersistentVFXEntry` in parallel. 
There is no easy way to check this from VFX Forge perspective, and so regular parallelism rules must be followed.
- The returned entry is a mutable `ref`. Do not store entry refs beyond the immediate operation.
- Respect the persistent spawn timing rule relative to `VFXTransformSystem` and `SyncVFXSystem`.

## Memory Management

Instant request buffers are reused and cleared after upload.

Persistent buffers live for the registered definition lifetime and are capacity-based. 
Persistent array payloads are copied into pooled deferred unsafe arrays first, then moved into `UnsafeHeapMemory` when the request resolves, similar to how `memory.alloc` works.

VFX Forge deallocates all GPU memory for VFXs, which were unused for a specified duration. 
This essentially allows to store all VFXs in a single scene without any need for asset management of the VFXs. Only active VFXs occupy GPU memory and are involved in GPU computations.
With Unity 6.6, `VisualEffect` component will be able to completely deallocate its own GPU resources, making the GPU memory management for VFXs with VFX Forge trivial. 

## Performance Notes

VFX Forge is designed around batched uploads and stable backing storage, using ECS, Bursted Jobs, and optimized data structures wherever possible:

- Effects are uploaded through VFX Graph `GraphicsBuffer` properties.
- Instant requests avoid shared contention by gathering per thread and merging once.
- Persistent entries upload only relevant transform/data ranges when possible.
- Inactive graphs are disabled after timeout to release graphics buffers and stop unnecessary graph updates.

Tradeoffs:

- Persistent definitions allocate buffers based on capacity, including double-capacity backing storage for transform/data holes and delayed reuse.
- Array payloads are copied on spawn.
- Prefer fewer batched definitions over many unique graph definitions when spawn counts are high.

## Limitations and What to Avoid

- Do not register two `HybridVisualEffect` instances with the same `VFXDefinition` key.
- Do not spawn persistent VFX in `LateUpdate` or any path that runs after `VFXTransformSystem` but before `SyncVFXSystem`.
- Do not assume a persistent spawn always succeeds; check `TrackedEntity.IsValid` if relevant.
- Do not mismatch definition payload type and generic `Spawn<T>` or `TrySetUpdateData<T>` calls.

## Samples

The package includes a Unity Package Manager sample:

- `Samples~/Demo/DEMO.unity`: basic demo scene.
- `Samples~/Demo/DamageNumbers`: damage-number graph, data types, glyph baker, and definition.
- `Samples~/Demo/TemplatesDefinitions`: definition assets for the built-in instant/persistent templates.

Import the sample from Package Manager to inspect a complete graph/data/definition setup.

# Instant VFX

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

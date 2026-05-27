using FireAlt.VFXForge.Data;
using FireAlt.Core.Collections;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
    
namespace FireAlt.VFXForge.Tests
{
    internal static class VFXTestHelper
    {
        internal static InstantVFXEntry CreateInstantEntry()
        {
            var entry = default(InstantVFXEntry);
            entry.PendingRequestsCount = new UnsafeThreadData<InstantVFXEntry.Requests>(Allocator.Persistent);
            return entry;
        }

        internal static PersistentVFXEntry CreatePersistentEntry(int capacity)
        {
            var doubleCapacity = capacity * 2;
            var entry = default(PersistentVFXEntry);

            entry.Capacity = capacity;
            entry.SpawnIndexBuffer = new UnsafeList<VFXSpawnIndex>(capacity, Allocator.Persistent);
            entry.TransformBuffer = new UnsafeArray<VFXTransform>(doubleCapacity, Allocator.Persistent);
            entry.AliveMask = new UnsafeBitMaskRange(doubleCapacity, Allocator.Persistent);
            entry.TrackedEntities = new UnsafeHashSet<TrackedEntity>(doubleCapacity, Allocator.Persistent);

            entry.SpawnRequests = new UnsafeThreadList<TrackedEntity>(32, Allocator.Persistent);
            entry.KillRequests = new UnsafeThreadList<TrackedEntity>(32, Allocator.Persistent);
            entry.ResolvedToRequestMap = new UnsafeHashMap<TrackedEntity, TrackedEntity>(32, Allocator.Persistent);
            entry.DeferredToResolvedMap = new UnsafeHashMap<TrackedEntity, TrackedEntity>(32, Allocator.Persistent);
            entry.DeferredTransformBuffer = new UnsafeArray<VFXTransform>(capacity, Allocator.Persistent);

            var freeIndices = new NativeArray<int>(doubleCapacity, Allocator.Temp);
            for (var i = 0; i < doubleCapacity; i++)
            {
                freeIndices[i] = i;
            }

            entry.FreeIndices = new UnsafePriorityHeap<int>(freeIndices, Allocator.Persistent);
            entry.DataUploadRange.Reset();
            entry.ArrayDataUploadRange.Reset();

            return entry;
        }

        internal static void AddDeferredSpawnRequest(ref PersistentVFXEntry entry, TrackedEntity trackedEntity, bool didTransformSystemRun)
        {
            ref var spawnRequests = ref entry.SpawnRequests.GetUnsafeList(JobsUtility.ThreadIndex);
            spawnRequests.Add(trackedEntity);

            var deferredTransform = default(VFXTransform);
            deferredTransform.SetAlive(true);
            if (didTransformSystemRun)
            {
                deferredTransform.SetDidTransformSystemRun();
            }

            entry.DeferredTransformBuffer[trackedEntity.IndexInData] = deferredTransform;
        }
    }
}

using System;
using System.Threading;
using FireAlt.VFXForge.Data;
using FireAlt.Core;
using FireAlt.Core.Collections;
using FireAlt.Core.Extensions;
using Unity.Assertions;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;

namespace FireAlt.VFXForge
{
    public struct PersistentVFXEntry : IVFXGraphEntry, IDisposable
    {
        public UnityObjectRef<HybridVisualEffect> HybridVisualEffect { get; }
        public VFXKey VFXKey { get; }
        public ulong DataStableTypeHash { get; }
        public ulong ArrayDataStableTypeHash { get; } 
        public int DataSizeInBytes { get; }
        public int ArrayDataSizeInBytes { get; }
        
        internal int Capacity;
        internal int UsedCapacity;
        internal UploadRange DataUploadRange;
        internal UploadRange ArrayDataUploadRange;
        
        internal UnsafeThreadToListMapper<byte> SpawnDataBuffer;
        
        internal UnsafeList<VFXSpawnIndex> SpawnIndexBuffer;
        internal UnsafeList<VFXArraySpawnIndex> ArraySpawnIndexBuffer;
        internal UnsafeArray<VFXTransform> TransformBuffer;
        internal UnsafeBitMaskRange AliveMask;
        internal UnsafeArray<byte> DataBuffer;
        internal UnsafeHeapMemory ArrayDataMemoryBuffer;
        internal UnsafeArray<VFXArrayPtr> ArrayPtrBuffer;
        internal UnsafePriorityHeap<int> FreeIndices;
        internal UnsafeHashSet<TrackedEntity> TrackedEntities;
        internal UnsafeHashSet<TrackedEntity> TrackedEntityIds; // TODO: Remove with Unity 6.7
        
        internal int NextIndex;
        internal UnsafeArray<VFXTransform> DeferredTransformBuffer;
        internal UnsafeArray<byte> DeferredDataBuffer;
        internal UnsafeArray<PooledUnsafeArray<byte>> DeferredArrayDataBuffer;
        
        internal UnsafeThreadList<TrackedEntity> SpawnRequests;
        internal UnsafeThreadList<TrackedEntity> SpawnEntityIdRequests; // TODO: Remove with Unity 6.7
        internal UnsafeThreadList<TrackedEntity> KillRequests;

        internal UnsafeHashMap<TrackedEntity, TrackedEntity> DeferredToResolvedMap;
        internal UnsafeHashMap<TrackedEntity, TrackedEntity> ResolvedToRequestMap;
        
        public int RequestsCount { get; set; }
        public int ArrayRequestsCount { get; set; }
        public bool HasPendingRequests => NextIndex > 0;

        public void ResetRequestsCount()
        {
            SpawnIndexBuffer.Clear();
            ArraySpawnIndexBuffer.Clear();
            RequestsCount = 0;
            ArrayRequestsCount = 0;
        }

        internal PersistentVFXEntry(HybridVisualEffect hybridVisualEffect)
        {
            this = default;
            var definition = hybridVisualEffect.VFXDefinition;
            HybridVisualEffect = hybridVisualEffect;
            DataStableTypeHash = definition.vfxDataType;
            DataSizeInBytes = definition.DataGpuSize;
            ArrayDataStableTypeHash = definition.vfxArrayDataType;
            ArrayDataSizeInBytes = definition.ArrayDataGpuSize;
            VFXKey = definition;
        }

        public TrackedEntity Spawn(Entity entityToTrack, float trackingDuration = 0f)
        {
            return Spawn(TrackedEntity.FromEntity(entityToTrack), trackingDuration);
        }

        public TrackedEntity Spawn(EntityId gameObjectToTrack, float trackingDuration = 0f)
        {
            return Spawn(TrackedEntity.FromEntityId(gameObjectToTrack), trackingDuration);
        }

        private TrackedEntity Spawn(TrackedEntity trackedEntity, float trackingDuration)
        {
            Assert.IsTrue(trackingDuration >= 0f);
            var nextIndex = Interlocked.Increment(ref NextIndex);
            
            if (nextIndex > Capacity - UsedCapacity) 
                return trackedEntity;
            
            trackedEntity.IndexInData = nextIndex - 1;
            trackedEntity.PackedData.SetIsDeferred(true);
            trackedEntity.PackedData.SetSystemVersion(SyncVFXSystem.SystemVersion);

            VFXTransform transformData = default;
            transformData.SetAlive(true);
            transformData.TrackingDuration = trackingDuration;
            DeferredTransformBuffer[trackedEntity.IndexInData] = transformData;

            if (trackedEntity.IsEntityId)
            {
                SpawnEntityIdRequests.GetUnsafeList(JobsUtility.ThreadIndex).Add(trackedEntity);
            }
            else
            {
                SpawnRequests.GetUnsafeList(JobsUtility.ThreadIndex).Add(trackedEntity);
            }
            
            return trackedEntity;
        }

        public TrackedEntity Spawn<U>(Entity entityToTrack, NativeArray<U> arrayData, float trackingDuration = 0f)
            where U : unmanaged
        {
            return Spawn(TrackedEntity.FromEntity(entityToTrack), arrayData, trackingDuration);
        }

        public TrackedEntity Spawn<U>(EntityId gameObjectToTrack, NativeArray<U> arrayData, float trackingDuration = 0f)
            where U : unmanaged
        {
            return Spawn(TrackedEntity.FromEntityId(gameObjectToTrack), arrayData, trackingDuration);
        }

        private TrackedEntity Spawn<U>(TrackedEntity entityToTrack, NativeArray<U> arrayData, float trackingDuration) 
            where U : unmanaged
        {
            Common.CheckStableTypeHash<U>(ArrayDataStableTypeHash);
            
            var trackedEntity = Spawn(entityToTrack, trackingDuration);
            if (!trackedEntity.IsValid) return trackedEntity;

            SetArray(trackedEntity, arrayData);
            return trackedEntity;
        }

        public TrackedEntity Spawn<T>(Entity entityToTrack, T data, float trackingDuration = 0f)
            where T : unmanaged
        {
            return Spawn(TrackedEntity.FromEntity(entityToTrack), data, trackingDuration);
        }

        public TrackedEntity Spawn<T>(EntityId gameObjectToTrack, T data, float trackingDuration = 0f)
            where T : unmanaged
        {
            return Spawn(TrackedEntity.FromEntityId(gameObjectToTrack), data, trackingDuration);
        }

        private TrackedEntity Spawn<T>(TrackedEntity entityToTrack, T data, float trackingDuration) 
            where T : unmanaged
        {
            Common.CheckStableTypeHash<T>(DataStableTypeHash);
            
            var trackedEntity = Spawn(entityToTrack, trackingDuration);
            if (!trackedEntity.IsValid) return trackedEntity;
            
            DeferredDataBuffer.SetData(trackedEntity.IndexInData, data);
            return trackedEntity;
        }

        public TrackedEntity Spawn<T, U>(Entity entityToTrack, T data, NativeArray<U> arrayData,
            float trackingDuration = 0f)
            where T : unmanaged
            where U : unmanaged
        {
            return Spawn(TrackedEntity.FromEntity(entityToTrack), data, arrayData, trackingDuration);
        }

        public TrackedEntity Spawn<T, U>(EntityId gameObjectToTrack, T data, NativeArray<U> arrayData,
            float trackingDuration = 0f)
            where T : unmanaged
            where U : unmanaged
        {
            return Spawn(TrackedEntity.FromEntityId(gameObjectToTrack), data, arrayData, trackingDuration);
        }

        private TrackedEntity Spawn<T, U>(TrackedEntity entityToTrack, T data, NativeArray<U> arrayData, float trackingDuration) 
            where T : unmanaged
            where U : unmanaged
        {
            Common.CheckStableTypeHash<T>(DataStableTypeHash);
            Common.CheckStableTypeHash<U>(ArrayDataStableTypeHash);
            
            var trackedEntity = Spawn(entityToTrack, trackingDuration);
            if (!trackedEntity.IsValid) return trackedEntity;
            
            SetArray(trackedEntity, arrayData);
            DeferredDataBuffer.SetData(trackedEntity.IndexInData, data);
            return trackedEntity;
        }

        public unsafe TrackedEntity SpawnUnsafe(Entity entityToTrack, byte* data, NativeArray<byte> arrayData = default,
            float trackingDuration = 0f)
        {
            return SpawnUnsafe(TrackedEntity.FromEntity(entityToTrack), data, arrayData, trackingDuration);
        }

        public unsafe TrackedEntity SpawnUnsafe(EntityId gameObjectToTrack, byte* data, NativeArray<byte> arrayData = default,
            float trackingDuration = 0f)
        {
            return SpawnUnsafe(TrackedEntity.FromEntityId(gameObjectToTrack), data, arrayData, trackingDuration);
        }

        private unsafe TrackedEntity SpawnUnsafe(TrackedEntity entityToTrack, byte* data, NativeArray<byte> arrayData = default, float trackingDuration = 0f)
        {
            var trackedEntity = Spawn(entityToTrack, trackingDuration);
            if (!trackedEntity.IsValid) return trackedEntity;

            if (arrayData.IsCreated)
            {
                SetArray(trackedEntity, arrayData);
            }
            DeferredDataBuffer.SetDataUnsafe(trackedEntity.IndexInData, data, DataSizeInBytes);
            return trackedEntity;
        }

        public TrackedEntity SpawnUnsafe(Entity entityToTrack, NativeArray<byte> arrayData, float trackingDuration = 0f)
        {
            return SpawnUnsafe(TrackedEntity.FromEntity(entityToTrack), arrayData, trackingDuration);
        }

        public TrackedEntity SpawnUnsafe(EntityId gameObjectToTrack, NativeArray<byte> arrayData, float trackingDuration = 0f)
        {
            return SpawnUnsafe(TrackedEntity.FromEntityId(gameObjectToTrack), arrayData, trackingDuration);
        }

        private TrackedEntity SpawnUnsafe(TrackedEntity entityToTrack, NativeArray<byte> arrayData, float trackingDuration = 0f)
        {
            var trackedEntity = Spawn(entityToTrack, trackingDuration);
            if (!trackedEntity.IsValid) return trackedEntity;

            if (arrayData.IsCreated)
            {
                SetArray(trackedEntity, arrayData);
            }
            return trackedEntity;
        }
        
        public bool IsAlive(TrackedEntity trackedEntity)
        {
            return TryResolveTransform(trackedEntity, out var transform)
                   && transform.IsAlive();
        }
        
        public bool TryGetUpdateDataAsRef<T>(TrackedEntity trackedEntity, out Ref<T> dataRef)
            where T : unmanaged
        {
            if (TryResolveDataIndex(trackedEntity, out var index, out var isDeferred))
            {
                dataRef = isDeferred
                    ? new Ref<T>(ref DeferredDataBuffer.GetDataAsRef<T>(index))
                    : new Ref<T>(ref DataBuffer.GetDataAsRef<T>(index));
                return true;
            }

            dataRef = new Ref<T>();
            return false;
        }
        
        public bool TryGetArrayData<U>(TrackedEntity trackedEntity, out UnsafeArray<U> array)
            where U : unmanaged
        {
            Common.CheckStableTypeHash<U>(ArrayDataStableTypeHash);
            
            if (TryResolveDataIndex(trackedEntity, out var index, out var isDeferred))
            {
                array = isDeferred 
                    ? DeferredArrayDataBuffer[index].Array.Reinterpret<U>(UnsafeUtility.SizeOf<byte>()) 
                    : ArrayDataMemoryBuffer.ArrayAt<U>(ArrayPtrBuffer[index]);
                return true;
            }
            array = default;
            return false;
        }
        
        public bool TryGetArrayDataUnsafe(TrackedEntity trackedEntity, out UnsafeArray<byte> array)
        {
            if (TryResolveDataIndex(trackedEntity, out var index, out var isDeferred))
            {
                if (isDeferred)
                {
                    array = DeferredArrayDataBuffer[index].Array;
                    return true;
                }

                MemoryPtr memPtr = ArrayPtrBuffer[index];
                if (memPtr.IsValid && ArrayDataMemoryBuffer.Contains(memPtr))
                {
                    array = ArrayDataMemoryBuffer.ArrayAtUnsafe(memPtr);
                    return true;
                }
            }
            array = default;
            return false;
        }

        public bool TrySetUpdateData<T>(TrackedEntity trackedEntity, T updateData)
            where T : unmanaged
        {
            Common.CheckStableTypeHash<T>(DataStableTypeHash);

            if (TryResolveDataIndex(trackedEntity, out var index, out var isDeferred))
            {
                if (isDeferred)
                {
                    DeferredDataBuffer.SetData(index, updateData);
                }
                else
                {
                    DataBuffer.SetData(index, updateData);
                }

                return true;
            }

            return false;
        }
        
        public unsafe bool TrySetUpdateDataUnsafe(TrackedEntity trackedEntity, byte* updateData)
        {
            if (TryResolveDataIndex(trackedEntity, out var index, out var isDeferred))
            {
                if (isDeferred)
                {
                    DeferredDataBuffer.SetDataUnsafe(index, updateData, DataSizeInBytes);
                }
                else
                {
                    DataBuffer.SetDataUnsafe(index, updateData, DataSizeInBytes);
                }

                return true;
            }

            return false;
        }
        
        public bool TryKill(TrackedEntity trackedEntity)
        {
            if (TryResolveCheckIndex(trackedEntity, out var resolved, out var isDeferred))
            {
                if (isDeferred)
                {
                    DeferredTransformBuffer.ElementAt(resolved.IndexInData).Kill();
                }
                else
                {
                    ref var killRequests = ref KillRequests.GetUnsafeList(JobsUtility.ThreadIndex);
                    killRequests.Add(resolved);
                }

                return true;
            }

            return false;
        }
        
        private unsafe void SetArray<T>(TrackedEntity trackedEntity, NativeArray<T> arrayData) where T : unmanaged
        {
            var size = arrayData.Length * UnsafeUtility.SizeOf<T>();
            
            var byteArray = UnsafeArrayPool<byte>.Rent(size);
            UnsafeUtility.MemCpy(byteArray.Array.GetUnsafePtr(), arrayData.GetUnsafeReadOnlyPtr(), size);
            
            DeferredArrayDataBuffer[trackedEntity.IndexInData] = byteArray;
        }
        
        private bool TryResolveCheckIndex(TrackedEntity trackedEntity, out TrackedEntity resolvedKey, out bool isDeferred)
        {
            if (!trackedEntity.IsValid)
            {
                resolvedKey = TrackedEntity.Null;
                isDeferred = false;
                return false;
            }

            if (trackedEntity.IsDeferred(SyncVFXSystem.SystemVersion))
            {
                var index = trackedEntity.IndexInData;
                Assert.IsTrue(index >= 0 && index < Capacity);

                resolvedKey = trackedEntity;
                isDeferred = true;
                return true;
            }

            isDeferred = false;
            if (TryResolve(trackedEntity, out resolvedKey))
            {
                var index = resolvedKey.IndexInData;
                Assert.IsTrue(index >= 0 && index < Capacity * 2);
                return true;
            }

            return false;
        }

        private bool TryResolveDataIndex(TrackedEntity trackedEntity, out int index, out bool isDeferred)
        {
            if (TryResolveCheckIndex(trackedEntity, out var resolvedKey, out isDeferred))
            {
                index = resolvedKey.IndexInData;
                return true;
            }

            index = -1;
            return false;
        }

        private bool TryResolveTransform(TrackedEntity trackedEntity, out VFXTransform transform)
        {
            if (TryResolveCheckIndex(trackedEntity, out var resolvedKey, out var isDeferred))
            {
                transform = isDeferred
                    ? DeferredTransformBuffer[resolvedKey.IndexInData]
                    : TransformBuffer[resolvedKey.IndexInData];
                return true;
            }

            transform = default;
            return false;
        }

        private bool TryResolve(TrackedEntity trackedEntity, out TrackedEntity resolvedKey)
        {
            if (Hint.Unlikely(trackedEntity.PackedData.SystemVersion == 0))
            {
                resolvedKey = TrackedEntity.Null;
                return false;
            }

            if (trackedEntity.PackedData.IsDeferred)
            {
                return DeferredToResolvedMap.TryGetValue(trackedEntity, out resolvedKey);
            }
            resolvedKey = trackedEntity;
            return true;
        }
        
        public void Dispose()
        {
            SpawnIndexBuffer.Dispose();
            TransformBuffer.Dispose();
            AliveMask.Dispose();
            FreeIndices.Dispose();
            TrackedEntities.Dispose();
            TrackedEntityIds.Dispose();
            
            if (DataBuffer.IsCreated) DataBuffer.Dispose();
            if (ArrayDataMemoryBuffer.IsCreated) ArrayDataMemoryBuffer.Dispose();
            if (ArrayPtrBuffer.IsCreated) ArrayPtrBuffer.Dispose();
            if (ArraySpawnIndexBuffer.IsCreated) ArraySpawnIndexBuffer.Dispose();

            // Deferred
            DeferredTransformBuffer.Dispose();
            SpawnRequests.Dispose();
            SpawnEntityIdRequests.Dispose();
            KillRequests.Dispose();
            DeferredToResolvedMap.Dispose();
            ResolvedToRequestMap.Dispose();
            
            if (DeferredDataBuffer.IsCreated) DeferredDataBuffer.Dispose();
            if (DeferredArrayDataBuffer.IsCreated)
            {
                foreach (var pooledArray in DeferredArrayDataBuffer)
                {
                    pooledArray.Dispose();
                }
                DeferredArrayDataBuffer.Dispose();
            }
        }
    }
}

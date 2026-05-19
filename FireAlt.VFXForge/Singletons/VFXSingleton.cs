using System;
using System.Diagnostics;
using FireAlt.VFXForge.Data;
using JetBrains.Annotations;
using KrasCore;
using Unity.Assertions;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace FireAlt.VFXForge
{
    public struct VFXSingleton : IComponentData, IDisposable
    {
        internal readonly struct InternalAPI
        {
            internal TrackedEntity SpawnPersistent(ref PersistentVFXEntry entry, Entity entityToTrack,
                UnsafeArray<byte> arrayData, float trackingDuration)
            {
                Assert.IsTrue(trackingDuration >= 0f);
                var trackedEntity = new TrackedEntity(entityToTrack, -1, 0);
            
                if (entry.RequestsCount >= entry.Capacity
                    || entry.TrackedEntities.Count >= entry.Capacity
                    || !entry.FreeIndices.TryDequeue(out var index))
                {
                    return trackedEntity;
                }
            
                trackedEntity.IndexInData = index;
                trackedEntity.SystemVersion = SyncVFXSystem.SystemVersion;
                
                var transform = default(VFXTransform);
                transform.SetAlive(true);
                transform.TrackingDuration = trackingDuration;
                entry.TransformBuffer[index] = transform;
                entry.TrackedEntities.Add(trackedEntity);
                entry.AliveMask.Set(index);

                if (entry.SpawnIndexBuffer.IsCreated)
                {
                    entry.SpawnIndexBuffer.Add(new VFXSpawnIndex((uint)index));
                }
                if (arrayData.IsCreated && arrayData.Length > 0)
                {
                    var arrayLength = arrayData.Length / entry.ArrayDataSizeInBytes;
                    entry.ArrayPtrBuffer[trackedEntity.IndexInData] = entry.ArrayDataMemoryBuffer.Allocate(arrayData);
                    
                    for (int i = 0; i < arrayLength; i++)
                    {
                        entry.ArraySpawnIndexBuffer.Add(new VFXArraySpawnIndex((uint)index, (uint)i));
                    }
                    entry.ArrayRequestsCount += arrayLength;
                }
                
                entry.RequestsCount++;
                return trackedEntity;
            }
            
            internal unsafe TrackedEntity SpawnPersistentUnsafe(ref PersistentVFXEntry entry, Entity entityToTrack,
                byte* data, UnsafeArray<byte> arrayData, float trackingDuration)
            {
                Assert.IsTrue(data != null);

                var trackedEntity = SpawnPersistent(ref entry, entityToTrack, arrayData, trackingDuration);
                if (!trackedEntity.IsValid) return trackedEntity;

                if (entry.DataSizeInBytes != 0)
                {
                    entry.DataBuffer.SetDataUnsafe(trackedEntity.IndexInData, data, entry.DataSizeInBytes);
                }
                return trackedEntity;
            }
            
            internal void KillPersistent(ref PersistentVFXEntry entry, TrackedEntity resolvedKey)
            {
                Assert.IsTrue(!resolvedKey.IsDeferred(SyncVFXSystem.SystemVersion));
                var index = resolvedKey.IndexInData;
                Assert.IsTrue(index >= 0 && index < entry.Capacity * 2);
                
                if (!entry.TrackedEntities.Remove(resolvedKey)) return;
                
                ref var transform = ref entry.TransformBuffer.ElementAt(index);
                transform.Kill();
                entry.AliveMask.Unset(index);

                if (entry.ArrayDataMemoryBuffer.IsCreated)
                {
                    MemoryPtr ptr = entry.ArrayPtrBuffer[index];
                    if (ptr.IsValid && entry.ArrayDataMemoryBuffer.Contains(ptr))
                    {
                        entry.ArrayDataMemoryBuffer.Free(entry.ArrayPtrBuffer[index]);
                    }
                }
                
                if (entry.ResolvedToRequestMap.TryGetValue(resolvedKey, out var request))
                {
                    entry.ResolvedToRequestMap.Remove(resolvedKey);
                    entry.DeferredToResolvedMap.Remove(request);
                }
                entry.FreeIndices.Enqueue(index);
            }
        }
        
        public struct ParallelWriter
        {
            [ReadOnly]
            private NativeHashMap<VFXKey, InstantVFXEntry> _instantVFXGraphEntries;
            [ReadOnly]
            private NativeHashMap<VFXKey, PersistentVFXEntry> _persistentVFXGraphEntries;

            internal ParallelWriter(NativeHashMap<VFXKey, InstantVFXEntry> instantVFXGraphEntries,
                NativeHashMap<VFXKey, PersistentVFXEntry> persistentVFXGraphEntries)
            {
                _instantVFXGraphEntries = instantVFXGraphEntries;
                _persistentVFXGraphEntries = persistentVFXGraphEntries;
            }

            public ref InstantVFXEntry GetInstant(in VFXKey key)
            {
                CheckContainsInstant(_instantVFXGraphEntries, key);
                ref var entry = ref _instantVFXGraphEntries.GetValueAsRef(key);
                return ref entry;
            }
            
            public ref PersistentVFXEntry GetPersistent(in VFXKey key)
            {
                CheckContainsPersistent(_persistentVFXGraphEntries, key);
                ref var entry = ref _persistentVFXGraphEntries.GetValueAsRef(key);
                return ref entry;
            }
        }
        
        internal NativeHashMap<VFXKey, bool> IsPersistent;
        
        internal NativeHashMap<VFXKey, InstantVFXEntry> InstantVFXGraphEntries;
        internal NativeHashMap<VFXKey, PersistentVFXEntry> PersistentVFXGraphEntries;
        
        internal NativeHashMap<VFXKey, AliveVFX> InstantAliveVFX;
        internal NativeHashMap<VFXKey, AliveVFX> PersistentAliveVFX;
        
        internal VFXSingleton(int capacity)
        {
            IsPersistent = new NativeHashMap<VFXKey, bool>(capacity, Allocator.Persistent);
            
            InstantVFXGraphEntries = new NativeHashMap<VFXKey, InstantVFXEntry>(capacity, Allocator.Persistent);
            PersistentVFXGraphEntries = new NativeHashMap<VFXKey, PersistentVFXEntry>(capacity, Allocator.Persistent);
            
            InstantAliveVFX = new NativeHashMap<VFXKey, AliveVFX>(4, Allocator.Persistent);
            PersistentAliveVFX = new NativeHashMap<VFXKey, AliveVFX>(4, Allocator.Persistent);
        }
        
        internal InternalAPI AsInternal()
        {
            return new InternalAPI();
        }
        
        public ParallelWriter AsParallelWriter()
        {
            return new ParallelWriter(InstantVFXGraphEntries, PersistentVFXGraphEntries);
        }

        public bool IsValid()
        {
            return IsPersistent.IsCreated;
        }

        public ref InstantVFXEntry GetInstant(in VFXKey key)
        {
            CheckContainsInstant(InstantVFXGraphEntries, key);
            ref var entry = ref InstantVFXGraphEntries.GetValueAsRef(key);
            return ref entry;
        }
        
        public ref PersistentVFXEntry GetPersistent(in VFXKey key)
        {
            CheckContainsPersistent(PersistentVFXGraphEntries, key);
            ref var entry = ref PersistentVFXGraphEntries.GetValueAsRef(key);
            return ref entry;
        }

        public void Dispose()
        {
            IsPersistent.Dispose();
            InstantAliveVFX.Dispose();
            PersistentAliveVFX.Dispose();
            
            foreach (var pair in InstantVFXGraphEntries)
            {
                if (pair.Value.HybridVisualEffect.Value != null)
                    pair.Value.HybridVisualEffect.Value.VisualEffect.Reinit();
                pair.Value.Dispose();
            }
            InstantVFXGraphEntries.Dispose();
            foreach (var pair in PersistentVFXGraphEntries)
            {
                if (pair.Value.HybridVisualEffect.Value != null)
                    pair.Value.HybridVisualEffect.Value.VisualEffect.Reinit();
                pair.Value.Dispose();
            }
            PersistentVFXGraphEntries.Dispose();
        }
        
        [AssertionMethod]
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        private static void CheckContainsInstant(NativeHashMap<VFXKey, InstantVFXEntry> hashMap, VFXKey key)
        {
            if (!hashMap.ContainsKey(key))
            {
                throw new ArgumentException($"Instant VFX Graph Entries does not contain {key}. The VFX was not yet registered.");
            }
        }

        [AssertionMethod]
        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        [Conditional("UNITY_DOTS_DEBUG")]
        private static void CheckContainsPersistent(NativeHashMap<VFXKey, PersistentVFXEntry> hashMap, VFXKey key)
        {
            if (!hashMap.ContainsKey(key))
            {
                throw new ArgumentException($"Persistent VFX Graph Entries does not contain {key}. The VFX was not yet registered.");
            }
        }
    }
}

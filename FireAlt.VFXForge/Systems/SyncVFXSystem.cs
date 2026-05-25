using System;
using System.Collections.Generic;
using BovineLabs.Core.Extensions;
using BovineLabs.Core.Pause;
using BovineLabs.Core.Utility;
using FireAlt.VFXForge.Data;
using KrasCore;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;

namespace FireAlt.VFXForge
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderLast = true)]
    [BurstCompile]
    public partial struct SyncVFXSystem : ISystem
    {
        private struct VFXStateChange
        {
            public UnityObjectRef<HybridVisualEffect> HybridVisualEffect;
            public bool Enabled;
        }

        private struct ManagedArgs
        {
            public EntityQuery ManagedSingletonQuery;
            public float WorldDeltaTime;
            public JobHandle ResolvePersistentHandle;
            public NativeList<VFXStateChange> StateChanges;
        }

        private static class Burst
        {
            public static readonly SharedStatic<BurstTrampoline> GetVFXActivityStatus = 
                SharedStatic<BurstTrampoline>.GetOrCreate<SyncVFXSystem, GetVFXActivityStatusContext>();
            public static readonly SharedStatic<BurstTrampoline> UploadData = 
                SharedStatic<BurstTrampoline>.GetOrCreate<SyncVFXSystem, UploadDataContext>();
            public static readonly SharedStatic<int> VFXSystemVersion = 
                SharedStatic<int>.GetOrCreate<SyncVFXSystem, SystemVersionContext>();
        }
        
        public static int SystemVersion => Burst.VFXSystemVersion.Data;
        
        private struct GetVFXActivityStatusContext {}
        private struct UploadDataContext {}
        private struct SystemVersionContext {}
        
        static unsafe SyncVFXSystem()
        {
            Burst.GetVFXActivityStatus.Data = new BurstTrampoline(&GetVFXActivityStatusPacked);
            Burst.UploadData.Data = new BurstTrampoline(&UploadDataPacked);
            Burst.VFXSystemVersion.Data = 1;
        }

        private static unsafe void GetVFXActivityStatusPacked(void* argumentsPtr, int argumentsSize)
        {
            ref var managedArgs = ref BurstTrampoline.ArgumentsFromPtr<BurstManagedPair<UnityObjectRef<HybridVisualEffect>, bool>>(argumentsPtr, argumentsSize);
            ref var visualEffect = ref managedArgs.First;
            ref var isActive = ref managedArgs.Second;
            
            isActive = visualEffect.Value.VisualEffect.aliveParticleCount > 0;
        }
        
        private static unsafe void UploadDataPacked(void* argumentsPtr, int argumentsSize)
        {
            ref var managedArgs = ref BurstTrampoline.ArgumentsFromPtr<BurstManagedPair<VFXSingleton, ManagedArgs>>(argumentsPtr, argumentsSize);
            ref var vfxSingleton = ref managedArgs.First;
            ref var args = ref managedArgs.Second;
            
            var graphicsBuffersSingleton = args.ManagedSingletonQuery.GetSingleton<VFXGraphicsBuffersSingleton>();
            
            foreach (var stateChange in args.StateChanges)
            {
                var hybridVisualEffect = stateChange.HybridVisualEffect.Value;
                hybridVisualEffect.SetVFXActive(stateChange.Enabled);

                if (!stateChange.Enabled) continue;
                VFXKey key = hybridVisualEffect.VFXDefinition;
                var timeoutDuration = hybridVisualEffect.VFXDefinition.timeoutDuration;
                if (hybridVisualEffect.VFXDefinition.IsPersistent)
                {
                    vfxSingleton.PersistentAliveVFX.GetRef(key).SetTimeoutDuration(timeoutDuration);
                }
                else
                {
                    vfxSingleton.InstantAliveVFX.GetRef(key).SetTimeoutDuration(timeoutDuration);
                }
            }
            args.StateChanges.Clear();
            
            var isPlaying = Application.isPlaying;
            var unityDeltaTime = Time.deltaTime;
            var deltaTimeMultiplier = args.WorldDeltaTime / unityDeltaTime;
            if (unityDeltaTime == 0)
            {
                deltaTimeMultiplier = 1f;
            }
            
            Profiler.BeginSample("InstantVFXGraphEntries");
            foreach (var pair in vfxSingleton.InstantAliveVFX)
            {
                ref var entry = ref vfxSingleton.GetInstant(pair.Key);
                var vfxGraph = entry.HybridVisualEffect.Value.VisualEffect;
                var graphicsBuffers = graphicsBuffersSingleton.InstantVFXGraphEntries[pair.Key];
                if (!graphicsBuffers.HasRequiredProperties()) continue;

                var spawnRequestCount = entry.RequestsCount;
                var spawnArrayRequestCount = entry.ArrayRequestsCount;
                
                if (isPlaying)
                {
                    vfxGraph.playRate = deltaTimeMultiplier;
                }
                Common.TrySetInt(vfxGraph, VFXProperties.SpawnRequestsCount, spawnRequestCount);
                Common.TrySetInt(vfxGraph, VFXProperties.SpawnArrayRequestsCount, spawnArrayRequestCount);
                
                if (spawnRequestCount == 0 && spawnArrayRequestCount == 0) continue;

                if (entry.DataSizeInBytes > 0)
                {
                    graphicsBuffers.SetDataBuffer(entry.DataBuffer.List);
                    entry.DataBuffer.Clear();
                }
                if (entry.ArrayDataSizeInBytes > 0)
                {
                    graphicsBuffers.SetArrayDataBuffer(entry.ArrayDataBuffer.List, entry.ArrayPtrBuffer.List, entry.ArraySpawnIndexBuffer.List);
                    entry.ArrayDataBuffer.Clear();
                    entry.ArrayPtrBuffer.Clear();
                    entry.ArraySpawnIndexBuffer.Clear();
                }
                
                entry.ResetRequestsCount();
            }
            Profiler.EndSample();
            
            args.ResolvePersistentHandle.Complete();
            
            Profiler.BeginSample("PersistentVFXGraphEntries");
            foreach (var pair in vfxSingleton.PersistentAliveVFX)
            {
                ref var entry = ref vfxSingleton.GetPersistent(pair.Key);
                var vfxGraph = entry.HybridVisualEffect.Value.VisualEffect;
                var graphicsBuffers = graphicsBuffersSingleton.PersistentVFXGraphEntries[pair.Key];
                if (!graphicsBuffers.HasRequiredProperties()) continue;
                
                if (isPlaying)
                {
                    vfxGraph.playRate = deltaTimeMultiplier;
                }
                Common.TrySetInt(vfxGraph, VFXProperties.SpawnRequestsCount, entry.RequestsCount);
                Common.TrySetInt(vfxGraph, VFXProperties.SpawnArrayRequestsCount, entry.ArrayRequestsCount);
                
                if (entry.DataUploadRange.IsValid())
                {
                    graphicsBuffers.SetTransformBuffer(entry.TransformBuffer, entry.DataUploadRange);
                    
                    if (entry.DataSizeInBytes > 0)
                    {
                        graphicsBuffers.SetDataBuffer(entry.DataBuffer, entry.DataUploadRange);
                    }
                }
                if (entry.ArrayDataSizeInBytes > 0 && entry.ArrayDataUploadRange.IsValid())
                {
                    graphicsBuffers.SetArrayDataBuffer(entry.ArrayDataMemoryBuffer, entry.ArrayPtrBuffer,
                        entry.ArrayDataUploadRange, entry.DataUploadRange);
                }

                if (entry.RequestsCount == 0 && entry.ArrayRequestsCount == 0) continue;
                graphicsBuffers.SetIndexBuffers(entry.SpawnIndexBuffer, entry.ArraySpawnIndexBuffer);
                entry.ResetRequestsCount();
            }
            Profiler.EndSample();
        }

        private NativeList<VFXStateChange> _stateChanges;
        
        public void OnCreate(ref SystemState state)
        {
            const int vfxCapacity = 32;
            
            state.EntityManager.CreateSingleton(new VFXSingleton(vfxCapacity));
            var entity = state.EntityManager.CreateEntity(typeof(VFXGraphicsBuffersSingleton));
            state.EntityManager.AddComponentObject(entity, new VFXGraphicsBuffersSingleton
            {
                InstantVFXGraphEntries = new Dictionary<VFXKey, InstantVFXGraphicsBuffers>(vfxCapacity),
                PersistentVFXGraphEntries = new Dictionary<VFXKey, PersistentVFXGraphicsBuffers>(vfxCapacity)
            });
            
            _stateChanges = new NativeList<VFXStateChange>(4, Allocator.Persistent);
        }

        public void OnDestroy(ref SystemState state)
        {
            // First dispose the graphics buffers, then clear VFX data + reinit VFX Graphs to not have a warning
            SystemAPI.ManagedAPI.GetSingleton<VFXGraphicsBuffersSingleton>().Dispose();
            SystemAPI.GetSingleton<VFXSingleton>().Dispose();
            _stateChanges.Dispose();
        }
        
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.CompleteDependencyBeforeRW<VFXSingleton>();
            
            ref var vfxSingleton = ref SystemAPI.GetSingletonRW<VFXSingleton>().ValueRW;
            Burst.VFXSystemVersion.Data++;

            var deltaTime = SystemAPI.Time.DeltaTime;

            if (Application.isPlaying)
            {
                using var toRemove = PooledNativeList<AliveVFX>.Make();
                RemoveTimedOutVFX(vfxSingleton.InstantAliveVFX, deltaTime, toRemove.List);
                RemoveTimedOutVFX(vfxSingleton.PersistentAliveVFX, deltaTime, toRemove.List);
                
                AddVFX(vfxSingleton.PersistentVFXGraphEntries, vfxSingleton.PersistentAliveVFX);
                AddVFX(vfxSingleton.InstantVFXGraphEntries, vfxSingleton.InstantAliveVFX);
            }
            
            var persistentKeys = vfxSingleton.PersistentAliveVFX.GetKeyArray(state.WorldUpdateAllocator);
            var resolvePersistentHandle = new ResolvePersistentJob
            {
                VFXSingleton = vfxSingleton,
                KeysArray = persistentKeys,
            }.ScheduleParallel(persistentKeys.Length, 1, state.Dependency);
            
            var instantKeys = vfxSingleton.InstantAliveVFX.GetKeyArray(state.WorldUpdateAllocator);
            var resolveInstantHandle = new ResolveInstantJob
            {
                VFXSingleton = vfxSingleton,
                KeysArray = instantKeys,
            }.ScheduleParallel(instantKeys.Length, 1, state.Dependency);
            
            JobHandle.CombineDependencies(resolveInstantHandle, resolvePersistentHandle).Complete();

            var args = new ManagedArgs
            {
                ResolvePersistentHandle = resolvePersistentHandle,
                WorldDeltaTime = deltaTime,
                ManagedSingletonQuery = SystemAPI.QueryBuilder().WithAll<VFXGraphicsBuffersSingleton>().Build(),
                StateChanges = _stateChanges
            };
            Burst.UploadData.Data.Invoke(vfxSingleton, args);
        }

        private void RemoveTimedOutVFX(NativeHashMap<VFXKey, AliveVFX> aliveVfxMap, float deltaTime, NativeList<AliveVFX> toRemove)
        {
            toRemove.Clear();
            foreach (var pair in aliveVfxMap)
            {
                ref var aliveVfx = ref pair.Value;
                Burst.GetVFXActivityStatus.Data.InvokeOut(aliveVfx.HybridVisualEffect, out bool isActive);
                if (isActive)
                {
                    aliveVfx.InactivityTimeRemaining = aliveVfx.TimeoutDuration;
                }
                else
                {
                    aliveVfx.InactivityTimeRemaining -= deltaTime;
                    if (aliveVfx.InactivityTimeRemaining <= 0)
                    {
                        toRemove.Add(aliveVfx);
                    }
                }
            }
            foreach (var aliveVfx in toRemove)
            {
                aliveVfxMap.Remove(aliveVfx.Key);
                _stateChanges.Add(new VFXStateChange
                {
                    HybridVisualEffect = aliveVfx.HybridVisualEffect,
                    Enabled = false,
                });
            }
        }
        
        private void AddVFX<T>(NativeHashMap<VFXKey, T> entryMap, NativeHashMap<VFXKey, AliveVFX> aliveVfxMap) 
            where T : unmanaged, IVFXGraphEntry
        {
            foreach (var pair in entryMap)
            {
                if (pair.Value.HasPendingRequests && !aliveVfxMap.ContainsKey(pair.Key))
                {
                    _stateChanges.Add(new VFXStateChange
                    {
                        HybridVisualEffect = pair.Value.HybridVisualEffect,
                        Enabled = true,
                    });
                    aliveVfxMap.Add(pair.Key, new AliveVFX
                    {
                        Key = pair.Key,
                        HybridVisualEffect = pair.Value.HybridVisualEffect,
                    });
                }
            }
        }
        
        [BurstCompile]
        private struct ResolveInstantJob : IJobFor
        {
            [ReadOnly]
            public NativeArray<VFXKey> KeysArray;
            [NativeDisableContainerSafetyRestriction]
            public VFXSingleton VFXSingleton;
            
            public void Execute(int index)
            {
                ref var entry = ref VFXSingleton.GetInstant(KeysArray[index]);
                
                if (entry.DataSizeInBytes > 0)
                {
                    entry.DataBuffer.CopyParallelToList();
                }
                if (entry.ArrayDataSizeInBytes > 0)
                {
                    entry.ArrayDataBuffer.CopyParallelToList();
                    
                    RemapIndices(ref entry);

                    entry.ArrayPtrBuffer.CopyParallelToList();
                    entry.ArraySpawnIndexBuffer.CopyParallelToList();
                }
            }
            
            private static void RemapIndices(ref InstantVFXEntry entry)
            {
                var arrayPtrBuffer = entry.ArrayPtrBuffer.ThreadList;
                var arraySpawnIndexBuffer = entry.ArraySpawnIndexBuffer.ThreadList;
                uint indexInDataOffset = 0;
                uint ptrOffset = 0;
                    
                for (int thread = 0; thread < JobsUtility.ThreadIndexCount; thread++)
                {
                    var pointers = arrayPtrBuffer.GetUnsafeList(thread);
                    var indices = arraySpawnIndexBuffer.GetUnsafeList(thread);
                    uint elementCount = 0;
                    
                    for (int i = 0; i < pointers.Length; i++)
                    {
                        ref var pointer = ref pointers.ElementAt(i);
                            
                        pointer.StartIndex += ptrOffset;
                        elementCount += pointer.Count;
                    }
                    for (int i = 0; i < indices.Length; i++)
                    {
                        ref var indexData = ref indices.ElementAt(i);
                        indexData.IndexInData += indexInDataOffset;
                    }

                    ptrOffset += elementCount;
                    indexInDataOffset += (uint)pointers.Length;
                }
            }
        }
        
        [BurstCompile]
        private struct ResolvePersistentJob : IJobFor
        {
            [ReadOnly]
            public NativeArray<VFXKey> KeysArray;
            [NativeDisableContainerSafetyRestriction]
            public VFXSingleton VFXSingleton;
            
            public void Execute(int index)
            {
                var internalApi = VFXSingleton.AsInternal();
                ref var entry = ref VFXSingleton.GetPersistent(KeysArray[index]);
                
                SpawnPersistentRequests(ref entry, internalApi);
                // Kill is after spawn, so that the indices will be reused with 1 frame delay for the VFX to react
                KillPersistentRequests(ref entry, internalApi, out var deadRange);
                
                entry.SpawnRequests.Clear();
                entry.KillRequests.Clear();
                entry.NextIndex = 0;

                entry.DataUploadRange = deadRange;
                if (entry.AliveMask.TryGetRange(out var aliveRange))
                {
                    entry.DataUploadRange.Encapsulate(new UploadRange(aliveRange));
                }
                
                entry.ArrayDataUploadRange.Reset();
                if (entry.ArrayDataMemoryBuffer.TryGetValidRange(out aliveRange))
                {
                    entry.ArrayDataUploadRange = new UploadRange(aliveRange);
                }
            }

            private static unsafe void SpawnPersistentRequests(ref PersistentVFXEntry entry,
                VFXSingleton.InternalAPI internalApi)
            {
                foreach (var deferredKey in entry.SpawnRequests)
                {
                    var deferredTransform = entry.DeferredTransformBuffer[deferredKey.IndexInData];
                    if (!deferredTransform.DidTransformSystemRun())
                    {
                        throw new Exception($"A persistent VFXKey({entry.VFXKey.Value}) spawn was requested between `VFXTransformSystem` and `SyncVFXSystem` which means the upload data does not carry Transform information. Do not spawn persistent VFX in `LateUpdate`.");
                    }
                    if (!deferredTransform.IsAlive()) continue;

                    TrackedEntity resolvedKey;
                    var defaultArrayData = default(UnsafeArray<byte>);
                    ref var arrayData = ref defaultArrayData;
                    
                    if (entry.DeferredArrayDataBuffer.IsCreated)
                    {
                        arrayData = ref entry.DeferredArrayDataBuffer.ElementAt(deferredKey.IndexInData);
                    }
                    
                    if (entry.DeferredDataBuffer.IsCreated)
                    {
                        var ptr = (byte*)entry.DeferredDataBuffer.GetUnsafePtr() + deferredKey.IndexInData * entry.DataSizeInBytes;
                        resolvedKey = internalApi.SpawnPersistentUnsafe(ref entry, deferredKey.Entity, ptr, arrayData, deferredTransform.TrackingDuration);
                    }
                    else
                    {
                        resolvedKey = internalApi.SpawnPersistent(ref entry, deferredKey.Entity, arrayData, deferredTransform.TrackingDuration);
                    }

                    if (arrayData.IsCreated) arrayData.Dispose();
                    if (!resolvedKey.IsValid) continue;

                    entry.TransformBuffer[resolvedKey.IndexInData] = deferredTransform;
                    entry.DeferredToResolvedMap.Add(deferredKey, resolvedKey);
                    entry.ResolvedToRequestMap.Add(resolvedKey, deferredKey);
                }
            }
            
            private static void KillPersistentRequests(ref PersistentVFXEntry entry, VFXSingleton.InternalAPI internalApi, out UploadRange deadRange)
            {
                deadRange = default;
                deadRange.Reset();
                foreach (var resolvedKey in entry.KillRequests)
                {
                    deadRange.Encapsulate(resolvedKey.IndexInData);
                    internalApi.KillPersistent(ref entry, resolvedKey);
                }
            }
        }
    }
}

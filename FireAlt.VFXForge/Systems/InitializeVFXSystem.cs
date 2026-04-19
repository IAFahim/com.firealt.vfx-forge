using System;
using FireAlt.VFXForge.Data;
using KrasCore;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;
using UnityEngine.VFX;

namespace FireAlt.VFXForge
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(InitializeVFXSystemGroup), OrderFirst = true)]
    public partial class InitializeVFXSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            EntityManager.CompleteDependencyBeforeRW<VFXSingleton>();
            var vfxSingleton = SystemAPI.GetSingleton<VFXSingleton>();
            var graphicsBuffersSingleton = SystemAPI.ManagedAPI.GetSingleton<VFXGraphicsBuffersSingleton>();

            foreach (var (component, registeredVFX, initializeRW) in SystemAPI.Query<RefRO<HybridVisualEffectData>,
                         RefRW<RegisteredVFX>, EnabledRefRW<HybridVisualEffectData>>())
            {
                var hve = component.ValueRO.HybridVisualEffect.Value;
                if (TryAddVFXEntry(ref vfxSingleton, graphicsBuffersSingleton, hve, out var key))
                {
                    registeredVFX.ValueRW.Key = key;
                    initializeRW.ValueRW = false;
                }
            }
        }
        
        private bool TryAddVFXEntry(ref VFXSingleton vfxSingleton, VFXGraphicsBuffersSingleton graphicsBuffersSingleton, HybridVisualEffect hybridVisualEffect,
            out VFXKey key)
        {
            var definition = hybridVisualEffect.VFXDefinition;
            var target = hybridVisualEffect.VisualEffect;
            
            key = definition;
            if (!vfxSingleton.IsPersistent.TryAdd(key, definition.IsPersistent))
            {
                Debug.LogError($"{key} was already added to the VFX system. There cannot be duplicates.", definition);
                return false;
            }
            
            if (!definition.IsPersistent)
            {
                InstantVFXGraphicsBuffers buffersEntry;
                try
                {
                    buffersEntry = new InstantVFXGraphicsBuffers(target, definition.DataGpuSize, definition.ArrayDataGpuSize);
                }
                catch (Exception e)
                {
                    HandleException(target, vfxSingleton, key, e);
                    return false;
                }

                var entry = new InstantVFXEntry(hybridVisualEffect)
                {
                    PendingRequestsCount = new UnsafeThreadData<InstantVFXEntry.Requests>(Allocator.Persistent),
                };
                
                if (entry.DataSizeInBytes > 0)
                {
                    entry.DataBuffer = new UnsafeThreadToListMapper<byte>(256, Allocator.Persistent);
                }
                if (entry.ArrayDataSizeInBytes > 0)
                {
                    entry.ArrayDataBuffer = new UnsafeThreadToListMapper<byte>(256, Allocator.Persistent);
                    entry.ArrayPtrBuffer = new UnsafeThreadToListMapper<VFXArrayPtr>(64, Allocator.Persistent);
                    entry.SpawnIndexBuffer = new UnsafeThreadToListMapper<VFXSpawnIndex>(64, Allocator.Persistent);
                }
                
                vfxSingleton.InstantVFXGraphEntries.Add(key, entry);
                graphicsBuffersSingleton.InstantVFXGraphEntries.Add(key, buffersEntry);
            }
            else
            {
                // We allocate twice the specified user capacity as the data lives for multiple frames
                // and destruction can create holes that have to exist when publishing the buffer to GPU
                var doubleCapacity = definition.capacity * 2;
                PersistentVFXGraphicsBuffers buffersEntry;
                try
                {
                    buffersEntry = new PersistentVFXGraphicsBuffers(target, definition.DataGpuSize, definition.ArrayDataGpuSize, doubleCapacity);
                }
                catch (Exception e)
                {
                    HandleException(target, vfxSingleton, key, e);
                    return false;
                }
                
                var entry = new PersistentVFXEntry(hybridVisualEffect)
                {
                    Capacity = definition.capacity,
                    SpawnIndexBuffer = new UnsafeList<VFXSpawnIndex>(definition.capacity, Allocator.Persistent),
                    TransformBuffer = new UnsafeArray<VFXTransform>(doubleCapacity, Allocator.Persistent),
                    AliveMask = new UnsafeBitMaskRange(doubleCapacity, Allocator.Persistent),
                    TrackedEntities = new UnsafeHashSet<TrackedEntity>(doubleCapacity, Allocator.Persistent),
                    SpawnRequests = new UnsafeThreadList<TrackedEntity>(32, Allocator.Persistent),
                    KillRequests = new UnsafeThreadList<TrackedEntity>(32, Allocator.Persistent),
                    ResolvedToRequestMap = new UnsafeHashMap<TrackedEntity, TrackedEntity>(32, Allocator.Persistent),
                    DeferredToResolvedMap = new UnsafeHashMap<TrackedEntity, TrackedEntity>(32, Allocator.Persistent),
                    DeferredTransformBuffer = new UnsafeArray<VFXTransform>(definition.capacity, Allocator.Persistent),
                };
                
                if (entry.DataSizeInBytes > 0)
                {
                    entry.DataBuffer = new UnsafeArray<byte>(doubleCapacity * definition.DataGpuSize, Allocator.Persistent);
                    entry.DeferredDataBuffer = new UnsafeArray<byte>(definition.capacity * definition.DataGpuSize, Allocator.Persistent);
                }
                if (entry.ArrayDataSizeInBytes > 0)
                {
                    entry.ArrayDataMemoryBuffer = new UnsafeHeapMemory(definition.ArrayDataGpuSize, doubleCapacity, Allocator.Persistent);
                    entry.ArrayPtrBuffer = new UnsafeArray<VFXArrayPtr>(doubleCapacity, Allocator.Persistent);
                    entry.DeferredArrayDataBuffer = new UnsafeArray<UnsafeArray<byte>>(definition.capacity, Allocator.Persistent);
                }

                // FreeIndices queue also uses doubleCapacity because we still need the indices looping
                var freeIndices = new NativeArray<int>(doubleCapacity, Allocator.Temp);
                for (int i = 0; i < doubleCapacity; i++)
                {
                    freeIndices[i] = i;
                }
                entry.FreeIndices = new UnsafePriorityHeap<int>(freeIndices, Allocator.Persistent);
                
                vfxSingleton.PersistentVFXGraphEntries.Add(key, entry);
                graphicsBuffersSingleton.PersistentVFXGraphEntries.Add(key, buffersEntry);
            }

            if (Application.isPlaying)
            {
                hybridVisualEffect.SetVFXActive(false);
            }
            else
            {
                AddEditorAliveVFX(
                    definition.IsPersistent ? vfxSingleton.PersistentAliveVFX : vfxSingleton.InstantAliveVFX,
                    hybridVisualEffect, key);
            }
            return true;
        }

        private static void HandleException(VisualEffect visualEffect, VFXSingleton vfxSingleton, VFXKey key, Exception e)
        {
            Debug.LogException(e, visualEffect);
            vfxSingleton.IsPersistent.Remove(key);
        }

        private static void AddEditorAliveVFX(NativeHashMap<VFXKey, AliveVFX> aliveVfxMap, HybridVisualEffect hybridVisualEffect, VFXKey key)
        {
            aliveVfxMap.Add(key, new AliveVFX
            {
                HybridVisualEffect = hybridVisualEffect,
                Key = key,
                InactivityTimeRemaining = -1f
            });
        }
    }
}

using FireAlt.VFXForge.Data;
using FireAlt.Core.Extensions;
using FireAlt.Core.Utility;
using Unity.Assertions;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Jobs;

namespace FireAlt.VFXForge
{
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(UpdateVFXSystemGroup))]
    public partial struct VFXTransformSystem : ISystem
    {
        private static class Burst
        {
            public static readonly SharedStatic<BurstInterop> IsEnabled =
                SharedStatic<BurstInterop>.GetOrCreate<VFXTransformSystem>();
        }

        
        private static unsafe void IsEnabledPacked(void* arguments, int argumentsSize)
        {
            ref var args = ref BurstInterop.ArgumentsFromPtr<BurstManagedPair<EntityId, EntityIdState>>(arguments, argumentsSize);
            ref var entityId = ref args.First;
            ref var state = ref args.Second;
            
            var go = Resources.EntityIdToObject(entityId) as GameObject;
            var exists = go != null;
            state = new EntityIdState { TransformHandle = exists ? go.transformHandle : default, IsEnabled = exists && go.activeInHierarchy };
        }
        
        static unsafe VFXTransformSystem()
        {
            Burst.IsEnabled.Data = new BurstInterop(&IsEnabledPacked);
        }
        
        private NativeList<bool> _enabledStates;
        private NativeList<TransformHandle> _transformHandles;

        public void OnCreate(ref SystemState state)
        {
            _enabledStates = new NativeList<bool>(8, Allocator.Persistent);
            _transformHandles = new NativeList<TransformHandle>(8, Allocator.Persistent);
        }

        public void OnDestroy(ref SystemState state)
        {
            _enabledStates.Dispose();
            _transformHandles.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var singleton = SystemAPI.GetSingletonRW<VFXSingleton>().ValueRW;
            var persistentKeys = singleton.PersistentVFXGraphEntries.GetKeyArray(state.WorldUpdateAllocator); // First runs before Alive status is determined

            _enabledStates.Clear();
            _transformHandles.Clear();
            foreach (var key in persistentKeys)
            {
                ref var entry = ref singleton.GetPersistent(key);
                foreach (var trackedEntity in entry.SpawnEntityIdRequests)
                {
                    Burst.IsEnabled.Data.InvokeOut(trackedEntity.EntityId, out EntityIdState entityIdState);

                    if (Hint.Likely(entityIdState.TransformHandle != default))
                    {
                        _enabledStates.Add(entityIdState.IsEnabled);
                        _transformHandles.Add(entityIdState.TransformHandle);
                    }
                }
                foreach (var trackedEntity in entry.TrackedEntityIds)
                {
                    Burst.IsEnabled.Data.InvokeOut(trackedEntity.EntityId, out EntityIdState entityIdState);

                    if (Hint.Unlikely(entityIdState.TransformHandle == default))
                    {
                        // Very unlikely that anyone will be destroying GameObjects
                        ref var transformData = ref entry.TransformBuffer.ElementAt(trackedEntity.IndexInData);
                        transformData.SetAlive(false);
                        transformData.SetEntityAlive(false);
                    }
                    else
                    {
                        _enabledStates.Add(entityIdState.IsEnabled);
                        _transformHandles.Add(entityIdState.TransformHandle);
                    }
                }
            }

            if (!_transformHandles.IsEmpty)
            {
                var transformAccessArray = new TransformAccessArray(_transformHandles.AsArray());
                state.Dependency = new FetchGameObjectTransformJob
                {
                    EnabledStates = _enabledStates,
                    KeysArray = persistentKeys,
                    VFXSingleton = singleton.AsParallelWriter(),
                    ElapsedTime = SystemAPI.Time.ElapsedTime
                }.ScheduleReadOnly(transformAccessArray, 64, state.Dependency);

                state.Dependency = new DisposeTransformAccessArray
                {
                    TransformAccessArray = transformAccessArray
                }.Schedule(state.Dependency);
            }
            
            state.Dependency = new FetchEntityTransformJob
            {
                LocalToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true),
                DisabledLookup = SystemAPI.GetComponentLookup<Disabled>(true),
                KeysArray = persistentKeys,
                VFXSingleton = singleton.AsParallelWriter(),
                ElapsedTime = SystemAPI.Time.ElapsedTime
            }.ScheduleParallel(persistentKeys.Length, 1, state.Dependency);
        }

        [BurstCompile]
        private struct DisposeTransformAccessArray : IJob
        {
            public TransformAccessArray TransformAccessArray;
            
            public void Execute()
            {
                TransformAccessArray.Dispose();
            }
        }
        
        [BurstCompile]
        private struct FetchGameObjectTransformJob : IJobParallelForTransform
        {
            [ReadOnly]
            public NativeList<bool> EnabledStates;
            
            public NativeArray<VFXKey> KeysArray;
            public VFXSingleton.ParallelWriter VFXSingleton;

            public double ElapsedTime;
            
            public void Execute(int index, [ReadOnly] TransformAccess transform)
            {
                ref var entry = ref VFXSingleton.GetPersistent(KeysArray[index]);

                if (entry.NextIndex > 0) // There are deferred requests
                {
                    foreach (var trackedEntity in entry.SpawnEntityIdRequests)
                    {
                        ref var data = ref entry.DeferredTransformBuffer.ElementAt(trackedEntity.IndexInData);
                        SetTransformData(ref data, trackedEntity, index, transform);
                        data.SetDidTransformSystemRun();
                    }
                }
                
                foreach (var trackedEntity in entry.TrackedEntityIds)
                {
                    ref var data = ref entry.TransformBuffer.ElementAt(trackedEntity.IndexInData);
                    SetTransformData(ref data, trackedEntity, index, transform);
                }
            }

            private void SetTransformData(ref VFXTransform data, TrackedEntity trackedEntity, int index, TransformAccess transform)
            {
                var entity = trackedEntity.EntityId;
                if (data.StartTrackingTime == 0f) data.StartTrackingTime = (float)ElapsedTime;

                var isTrackedEntityNull = entity.Equals(EntityId.None);
                var isEntityEnabled = EnabledStates[index];
                
                var isActive = data.IsAlive();
                var isEntityAlive = isTrackedEntityNull; //  || entityExists  <- already filtered out
                var isStillTracking = data.TrackingDuration == 0f
                                      || data.StartTrackingTime + data.TrackingDuration > ElapsedTime;

                data.SetEntityAlive(isEntityAlive);
                data.SetInTrackingDuration(isStillTracking);
                data.SetEntityEnabled(isEntityEnabled);
                    
                var isAlive = isActive && isEntityAlive && isStillTracking;
                if (isAlive)
                {
                    // if (!entityExists) return;  <- already filtered out
                    transform.GetPositionAndRotation(out var position, out var rotation);
                    data.Position = position;
                    data.Rotation = rotation.eulerAngles;
                    data.Scale = transform.localScale;
                }
                else
                {
                    data.Kill();
                }
            }
        }
        
        
        [BurstCompile]
        private struct FetchEntityTransformJob : IJobFor
        {
            [ReadOnly]
            public ComponentLookup<LocalToWorld> LocalToWorldLookup;
            [ReadOnly]
            public ComponentLookup<Disabled> DisabledLookup;
            
            public NativeArray<VFXKey> KeysArray;
            public VFXSingleton.ParallelWriter VFXSingleton;

            public double ElapsedTime;
            
            public void Execute(int index)
            {
                ref var entry = ref VFXSingleton.GetPersistent(KeysArray[index]);

                if (entry.NextIndex > 0) // There are deferred requests
                {
                    foreach (var trackedEntity in entry.SpawnRequests)
                    {
                        ref var data = ref entry.DeferredTransformBuffer.ElementAt(trackedEntity.IndexInData);
                        SetTransformData(ref data, trackedEntity);
                        data.SetDidTransformSystemRun();
                    }
                }
                
                foreach (var trackedEntity in entry.TrackedEntities)
                {
                    ref var data = ref entry.TransformBuffer.ElementAt(trackedEntity.IndexInData);
                    SetTransformData(ref data, trackedEntity);
                }
            }

            private void SetTransformData(ref VFXTransform data, TrackedEntity trackedEntity)
            {
                var entity = trackedEntity.Entity;
                if (data.StartTrackingTime == 0f) data.StartTrackingTime = (float)ElapsedTime;

                var isTrackedEntityNull = entity.Equals(Entity.Null);
                var entityExists = LocalToWorldLookup.TryGetComponent(entity, out var ltw);
                var isEntityEnabled = !DisabledLookup.HasComponent(entity);
                    
                var isActive = data.IsAlive();
                var isEntityAlive = isTrackedEntityNull || entityExists;
                var isStillTracking = data.TrackingDuration == 0f
                                      || data.StartTrackingTime + data.TrackingDuration > ElapsedTime;

                data.SetEntityAlive(isEntityAlive);
                data.SetInTrackingDuration(isStillTracking);
                data.SetEntityEnabled(isEntityEnabled);
                    
                var isAlive = isActive && isEntityAlive && isStillTracking;
                if (isAlive)
                {
                    if (!entityExists) return;
                    data.Position = ltw.Position;
                    data.Rotation = math.degrees(math.EulerZXY(ltw.Rotation));
                    data.Scale = ltw.Value.DecomposeScale();
                }
                else
                {
                    data.Kill();
                }
            }
        }
    }
}
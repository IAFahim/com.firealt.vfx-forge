using FireAlt.VFXForge.Data;
using KrasCore;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace FireAlt.VFXForge
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(UpdateVFXSystemGroup))]
    public partial struct VFXTransformSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var singleton = SystemAPI.GetSingletonRW<VFXSingleton>().ValueRW;
            var persistentKeys = singleton.PersistentVFXGraphEntries.GetKeyArray(state.WorldUpdateAllocator);
            
            state.Dependency = new UpdateJob
            {
                LocalToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true),
                DisabledLookup = SystemAPI.GetComponentLookup<Disabled>(true),
                KeysArray = persistentKeys,
                VFXSingleton = singleton.AsParallelWriter(),
                ElapsedTime = SystemAPI.Time.ElapsedTime
            }.ScheduleParallel(persistentKeys.Length, 1, state.Dependency);
        }

        [BurstCompile]
        private struct UpdateJob : IJobFor
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
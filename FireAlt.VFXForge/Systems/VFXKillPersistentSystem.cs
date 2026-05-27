using FireAlt.VFXForge.Data;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using FireAlt.Core.Collections;
using FireAlt.Core.Extensions;

namespace FireAlt.VFXForge
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(UpdateVFXSystemGroup), OrderLast = true)]
    public partial struct VFXKillPersistentSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var singleton = SystemAPI.GetSingletonRW<VFXSingleton>().ValueRW;
            var persistentKeys = singleton.PersistentVFXGraphEntries.GetKeyArray(state.WorldUpdateAllocator);
            
            state.Dependency = new KillJob
            {
                KeysArray = persistentKeys,
                VFXSingleton = singleton.AsParallelWriter(),
            }.ScheduleParallel(persistentKeys.Length, 1, state.Dependency);
        }
        
        [BurstCompile]
        private struct KillJob : IJobFor
        {
            public NativeArray<VFXKey> KeysArray;
            public VFXSingleton.ParallelWriter VFXSingleton;
            
            public void Execute(int index)
            {
                ref var entry = ref VFXSingleton.GetPersistent(KeysArray[index]);
                if (entry.TrackedEntities.IsEmpty) return;
                using var toRemove = NativeListPool<TrackedEntity>.Rent();
                
                foreach (var entityWithIndex in entry.TrackedEntities)
                {
                    ref var data = ref entry.TransformBuffer.ElementAt(entityWithIndex.IndexInData);
                    
                    if (!data.IsAlive())
                    {
                        toRemove.List.Add(entityWithIndex);
                    }
                }
                
                foreach (var entityWithIndex in toRemove.List)
                {
                    entry.TryKill(entityWithIndex);
                }
            }
        }
    }
}
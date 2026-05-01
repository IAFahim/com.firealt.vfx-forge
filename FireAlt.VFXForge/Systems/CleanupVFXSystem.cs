using System;
using FireAlt.VFXForge.Data;
using KrasCore;
using Unity.Entities;

namespace FireAlt.VFXForge
{
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(CleanupSystemGroup))]
    public partial class CleanupVFXSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            EntityManager.CompleteDependencyBeforeRW<VFXSingleton>();
            var vfxSingleton = SystemAPI.GetSingleton<VFXSingleton>();
            var graphicsBuffersSingleton = SystemAPI.ManagedAPI.GetSingleton<VFXGraphicsBuffersSingleton>();
            
            foreach (var registeredVFX in SystemAPI.Query<RefRO<RegisteredVFX>>()
                         .WithAbsent<HybridVisualEffectData>())
            {
                if (registeredVFX.ValueRO.Key.Equals(VFXKey.Null)) continue;
                RemoveVFXEntry(ref vfxSingleton, graphicsBuffersSingleton, registeredVFX.ValueRO.Key);
            }

            EntityManager.RemoveComponent<RegisteredVFX>(SystemAPI.QueryBuilder().WithAll<RegisteredVFX>()
                .WithAbsent<HybridVisualEffectData>().Build());
        }
        
        private void RemoveVFXEntry(ref VFXSingleton vfxSingleton, VFXGraphicsBuffersSingleton graphicsBuffersSingleton,
            VFXKey key)
        {
            if (!vfxSingleton.IsPersistent.ContainsKey(key))
            {
                throw new Exception($"{key.Value.ToString()} was not added to the VFX system");
            }
            
            if (!vfxSingleton.IsPersistent[key])
            {
                vfxSingleton.InstantAliveVFX.Remove(key);
                vfxSingleton.InstantVFXGraphEntries[key].Dispose();
                vfxSingleton.InstantVFXGraphEntries.Remove(key);
                graphicsBuffersSingleton.InstantVFXGraphEntries[key].Dispose();
                graphicsBuffersSingleton.InstantVFXGraphEntries.Remove(key);
            }
            else
            {
                vfxSingleton.PersistentAliveVFX.Remove(key);
                vfxSingleton.PersistentVFXGraphEntries[key].Dispose();
                vfxSingleton.PersistentVFXGraphEntries.Remove(key);
                graphicsBuffersSingleton.PersistentVFXGraphEntries[key].Dispose();
                graphicsBuffersSingleton.PersistentVFXGraphEntries.Remove(key);
            }

            vfxSingleton.IsPersistent.Remove(key);
        }
    }
}
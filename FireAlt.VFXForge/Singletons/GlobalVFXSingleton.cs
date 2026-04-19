using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace FireAlt.VFXForge
{
    /// <summary>
    /// Provides access to <see cref="VFXSingleton"/> from GameObject code.
    /// </summary>
    /// <remarks>
    /// Cache the result because the query operation is not free.
    /// You can call this in Awake or OnEnable, but any operations with
    /// <see cref="VFXSingleton"/> must happen during or after Start.
    /// The cache is invalidated if <see cref="World"/> is disposed.
    /// </remarks>
    public static class GlobalVFXSingleton
    {
        [BurstDiscard]
        [ExcludeFromBurstCompatTesting("Managed World.DefaultGameObjectInjectionWorld call")]
        public static VFXSingleton Get()
        {
            var query = new EntityQueryBuilder(Allocator.Temp).WithAll<VFXSingleton>()
                .WithOptions(EntityQueryOptions.IncludeSystems)
                .Build(World.DefaultGameObjectInjectionWorld.EntityManager);
            return query.GetSingleton<VFXSingleton>();
        }
    }
}

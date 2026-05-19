using FireAlt.VFXForge.Data;

namespace FireAlt.VFXForge
{
    public static class DecalProjectorVFXExtensions
    {
        public static void TryKillDecal(this ref DecalProjectorVFX data, VFXSingleton.ParallelWriter singleton)
        {
            if (!data.TrackedEntity.Equals(TrackedEntity.Null))
            {
                singleton.GetPersistent(data.Key).TryKill(data.TrackedEntity);
                data.TrackedEntity = TrackedEntity.Null;
            }
        }
    }
}
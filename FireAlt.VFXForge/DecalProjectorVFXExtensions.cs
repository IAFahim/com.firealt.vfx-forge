using FireAlt.VFXForge.Data;

namespace FireAlt.VFXForge
{
    public static class DecalProjectorVFXExtensions
    {
        public static bool TryKillDecal(this DecalProjectorVFX data, VFXSingleton.ParallelWriter singleton)
        {
            if (!data.TrackedEntity.Equals(default))
            {
                singleton.GetPersistent(data.Key).TryKill(data.TrackedEntity);
                data.TrackedEntity = TrackedEntity.Null;
                return true;
            }

            return false;
        }
    }
}
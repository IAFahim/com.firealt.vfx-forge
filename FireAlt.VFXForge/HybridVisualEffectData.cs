using Unity.Entities;

namespace FireAlt.VFXForge
{
    public struct HybridVisualEffectData : IComponentData, IEnableableComponent
    {
        public UnityObjectRef<HybridVisualEffect> HybridVisualEffect;
    }
}